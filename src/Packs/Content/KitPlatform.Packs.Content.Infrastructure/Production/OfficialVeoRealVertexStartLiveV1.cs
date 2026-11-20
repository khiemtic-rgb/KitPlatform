using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed record OfficialVeoRealVertexStartLiveResult(
    OfficialVeoStartResult Start,
    string Verdict,
    string Handoff,
    string? ContractAuthorizationHash,
    bool ContractHashMatched,
    string? AuthoritativeProject,
    string? Location,
    int OauthCount,
    int NetworkCount,
    int SnapshotMutations,
    int PermitIssueCount,
    int SelectionMutations);

/// <summary>
/// CK18 FINAL RUN host. Reloads live artifacts, verifies CK18B AUTH_HASH, then
/// one real predictLongRunning. No poll.
/// </summary>
public static class OfficialVeoRealVertexStartLiveV1
{
    public static readonly Guid ExpectedSnapshotId = Guid.Parse("fde661bb-a33f-418d-ac68-5c6f4751f81f");
    public const string ExpectedSnapshotHash = "56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb";
    public static readonly Guid ExpectedPermitId = Guid.Parse("97240e2f-e603-495b-863a-ebc9ef27b0a8");
    public const string ExpectedPermitHash = "PERMIT_HASH_V1:5d05d4776680aa4a096bd2701a518db31d557f7b602331e18ebaf599c1580868";
    public const string ExpectedAuthorizationHash =
        "AUTH_HASH_V1:4a9dc37361b531e98d5bbc9fa1a8249a0e1ab0d3788918d2794ca05921155fe3";
    public const string ExpectedKeyframeHash =
        "9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57";
    public const string ReadyForCk19 = "READY_FOR_CK19_POLL_RECOVER";
    public const string NotReady = "NOT_READY_FOR_CK19";

    public static async Task<OfficialVeoRealVertexStartLiveResult> StartDevelopmentAsync(
        string connectionString,
        string videoRoot,
        string? ledgerPath = null,
        CancellationToken ct = default,
        IProductionCredentialResolver? credentials = null)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var store = new ProductionWorkRepository(db);
        var permits = new ProductionExecutionPermitRepository(db);
        var selections = new ProductionAiSelectionRepository(db);

        var selection = await selections.GetActiveAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionAiSelectionStages.Video,
            ct);
        var frozen = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(store, ct);
        var rows = await permits.ListByTargetAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        var permit = rows.SingleOrDefault(p => p.PermitId == ExpectedPermitId);

        if (selection is null
            || selection.SelectionId != OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId
            || frozen is null
            || frozen.SnapshotId != ExpectedSnapshotId
            || !string.Equals(frozen.SnapshotHash, ExpectedSnapshotHash, StringComparison.Ordinal)
            || permit is null
            || !string.Equals(permit.PermitHash, ExpectedPermitHash, StringComparison.Ordinal)
            || !string.Equals(permit.Status, ProductionExecutionPermitStatuses.Issued, StringComparison.Ordinal)
            || permit.ConsumedAt is not null)
        {
            return Blocked(
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                "BLOCKED — PREFLIGHT FAILED",
                null, false, null);
        }

        var art = await store.GetArtifactAsync(OfficialCreateVideoFreezeV1.OfficialKeyframeId, ct);
        var bytes = art is null ? null : ReadExistingKeyframeBytes(videoRoot, art.StorageKey);
        if (bytes is null
            || !string.Equals(KitVideoIntegrityRules.Sha256Hex(bytes), ExpectedKeyframeHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(frozen.KeyframeHash, ExpectedKeyframeHash, StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                ProductionFailureReasons.InputSnapshotMismatch,
                "BLOCKED — PREFLIGHT FAILED",
                null, false, null);
        }

        var live = new ShotExecutionInput(
            frozen.Story, frozen.ActingBeat, frozen.VisualContract, "CHAR-001",
            frozen.KeyframeHash, OfficialCreateVideoFreezeV1.OfficialTiming, "", "",
            frozen.KeyframeArtifactId, frozen.KeyframeHash, frozen.AspectRatio, frozen.DurationSec,
            frozen.CompilerVersion, frozen.KeyframeArtifactId is { } kf ? [kf] : null,
            PerformanceSnapshotId: frozen.PerformanceSnapshotId,
            PerformanceHash: frozen.PerformanceHash);
        try
        {
            ProductionFrozenCreateVideoMatch.Assert(
                frozen, live, new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()));
        }
        catch (InvalidOperationException)
        {
            return Blocked(
                ProductionFailureReasons.InputSnapshotMismatch,
                "BLOCKED — PREFLIGHT FAILED",
                null, false, null);
        }

        var contractAuth = Evaluate(selection, frozen, live, bytes, permit, OfficialVeoHttpCompositionV1.DefaultComposeProject);
        var contractHash = contractAuth.AuthorizationHash ?? "";
        var contractMatch = contractAuth.Authorized
            && string.Equals(contractHash, ExpectedAuthorizationHash, StringComparison.Ordinal);
        if (!contractMatch)
        {
            return Blocked(
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                "BLOCKED — PREFLIGHT FAILED",
                contractHash, false, null);
        }

        _ = ledgerPath;
        return await AuthenticateFrozenAsync(frozen, credentials, contractHash, contractMatch, ct);
    }

    static async Task<OfficialVeoRealVertexStartLiveResult> AuthenticateFrozenAsync(
        ProductionTaskInputSnapshot frozen,
        IProductionCredentialResolver? credentials,
        string contractHash,
        bool contractMatch,
        CancellationToken ct)
    {
        var reference = ProductionFrozenCredential.FromSnapshot(frozen);
        if (credentials is null || reference is null)
        {
            return Blocked(
                ProductionCredentialResolver.Unresolved,
                "BLOCKED — PREFLIGHT FAILED",
                contractHash, contractMatch, null);
        }

        var authenticator = new ProductionVeoHttpAuthenticator();
        try
        {
            var material = await credentials.ResolveAsync(
                reference.CredentialId,
                reference.CredentialVersionId,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                ct);
            if (material.CredentialId != reference.CredentialId
                || material.CredentialVersionId != reference.CredentialVersionId)
            {
                return Blocked(
                    ProductionCredentialResolver.Unresolved,
                    "BLOCKED — PREFLIGHT FAILED",
                    contractHash, contractMatch, null);
            }

            await authenticator.AuthorizeFrozenAsync(material, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            if (authenticator.AcceptedCredentialId != reference.CredentialId
                || authenticator.AcceptedCredentialVersionId != reference.CredentialVersionId)
            {
                return Blocked(
                    ProductionCredentialResolver.Unresolved,
                    "BLOCKED — PREFLIGHT FAILED",
                    contractHash, contractMatch, null);
            }

            return Blocked(ex.Message, "BLOCKED — PREFLIGHT FAILED", contractHash, contractMatch, null);
        }
        catch (InvalidOperationException ex)
        {
            return Blocked(ex.Message, "BLOCKED — PREFLIGHT FAILED", contractHash, contractMatch, null);
        }

        return Blocked(
            ProductionCredentialResolver.Unresolved,
            "BLOCKED — PREFLIGHT FAILED",
            contractHash, contractMatch, null);
    }

    static OfficialVeoExecutionAuthorizationResult Evaluate(
        ProductionAiSelection selection,
        ProductionTaskInputSnapshot frozen,
        ShotExecutionInput live,
        byte[] bytes,
        ProductionExecutionPermit permit,
        string project) =>
        OfficialVeoExecutionAuthorizationV1.Evaluate(new OfficialVeoExecutionAuthorizationRequest(
            RealVertexExecutionV1Lock.OfficialScope,
            ProductionActions.CreateVideo,
            selection,
            frozen,
            live,
            bytes,
            "image/png",
            CostPolicyDecision.FromCatalog(
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                frozen.DurationSec),
            FamixaVeo31OfficialPricing.ProductionDefault,
            permit,
            ProjectId: project));

    static byte[]? ReadExistingKeyframeBytes(string videoRoot, string? storageKey)
    {
        var raw = (storageKey ?? "").Trim().Replace('\\', '/');
        var i = raw.LastIndexOf('/');
        if (i <= 0 || i >= raw.Length - 1) return null;
        var path = Path.Combine(videoRoot, raw[..i], raw[(i + 1)..] + ".mp4");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    static OfficialVeoRealVertexStartLiveResult Blocked(
        string reason, string verdict, string? hash, bool matched, string? project) =>
        new(
            new OfficialVeoStartResult(
                false, false, reason, reason, hash ?? ExpectedAuthorizationHash, null, ExpectedPermitId,
                ProductionExecutionPermitStatuses.Issued, ProductionExecutionPermitStatuses.Issued,
                RealVertexExecutionV1Lock.ProviderId, RealVertexExecutionV1Lock.ModelId,
                VeoVertexEndpoints.VertexModel, project, VeoHttpConfigPrecedence.DefaultLocation,
                null, null, null, 10, 8, OfficialVeoRealVertexStartV1.CostVariantId,
                FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd, null, "NOT_CALLED",
                0, 0, null, null, null, 0, 0, null, Array.Empty<string>()),
            verdict, NotReady, hash, matched, project, VeoHttpConfigPrecedence.DefaultLocation,
            0, 0, 0, 0, 0);
}
