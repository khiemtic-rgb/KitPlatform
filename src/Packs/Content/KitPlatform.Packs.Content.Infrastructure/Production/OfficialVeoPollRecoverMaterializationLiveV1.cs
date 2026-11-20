using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

public sealed record OfficialVeoPollRecoverMaterializationLiveResult(
    OfficialVeoPollResult First,
    OfficialVeoPollResult Second,
    string Verdict,
    string Handoff,
    string? AuthoritativeProject,
    string? Location,
    int OauthCount,
    int NetworkCount);

public static class OfficialVeoPollRecoverMaterializationLiveV1
{
    public static readonly Guid ExpectedTaskId = Guid.Parse("9cd27409-41fe-4194-b5a3-1b90f4d7b2a2");
    public static readonly Guid ExpectedAttemptId = Guid.Parse("5a72a533-ca30-466e-99b0-357fa47bcdd8");
    public static readonly Guid ExpectedSnapshotId = OfficialVeoRealVertexStartLiveV1.ExpectedSnapshotId;
    public const string ExpectedSnapshotHash = OfficialVeoRealVertexStartLiveV1.ExpectedSnapshotHash;
    public static readonly Guid ExpectedPermitId = OfficialVeoRealVertexStartLiveV1.ExpectedPermitId;
    public const string ExpectedPermitHash = OfficialVeoRealVertexStartLiveV1.ExpectedPermitHash;
    public const string ExpectedStartAuthorizationHash =
        "AUTH_HASH_V1:b69ed488b6e2f59868dc366f61589035a00bd1fc31ac87c5b6e9f1bcba560d6a";
    public const string ExpectedOperationName =
        "projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001/operations/633ea037-d719-4e31-9492-e36442bd3e20";
    public const string ExpectedEncodedOperationId =
        "veo_cHJvamVjdHMvZ2VuLWxhbmctY2xpZW50LTA1MDk4NDAxOTUvbG9jYXRpb25zL3VzLWNlbnRyYWwxL3B1Ymxpc2hlcnMvZ29vZ2xlL21vZGVscy92ZW8tMy4xLWdlbmVyYXRlLTAwMS9vcGVyYXRpb25zLzYzM2VhMDM3LWQ3MTktNGUzMS05NDkyLWUzNjQ0MmJkM2UyMA";
    public const string Ck19Complete = "CK19_COMPLETE";
    public const string NotReady = "CK19_NOT_COMPLETE";

    public static async Task<OfficialVeoPollRecoverMaterializationLiveResult> PollDevelopmentAsync(
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
        var files = new DirectoryProductionArtifactFileStore(videoRoot);

        var selection = await selections.GetActiveAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionAiSelectionStages.Video,
            ct);
        var frozen = await store.GetSnapshotAsync(ExpectedSnapshotId, ct);
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
            || frozen.SnapshotHash != ExpectedSnapshotHash
            || permit is null)
        {
            return Blocked("BLOCKED — PREFLIGHT FAILED");
        }

        var art = await store.GetArtifactAsync(OfficialCreateVideoFreezeV1.OfficialKeyframeId, ct);
        var bytes = art is null ? null : ReadExistingKeyframeBytes(videoRoot, art.StorageKey);
        if (bytes is null
            || !string.Equals(KitVideoIntegrityRules.Sha256Hex(bytes), frozen.KeyframeHash, StringComparison.OrdinalIgnoreCase))
            return Blocked("BLOCKED — PREFLIGHT FAILED");

        var live = new ShotExecutionInput(
            frozen.Story, frozen.ActingBeat, frozen.VisualContract, "CHAR-001",
            frozen.KeyframeHash, OfficialCreateVideoFreezeV1.OfficialTiming, "", "",
            frozen.KeyframeArtifactId, frozen.KeyframeHash, frozen.AspectRatio, frozen.DurationSec,
            frozen.CompilerVersion, frozen.KeyframeArtifactId is { } kf ? [kf] : null,
            PerformanceSnapshotId: frozen.PerformanceSnapshotId,
            PerformanceHash: frozen.PerformanceHash);
        _ = (ledgerPath, files, selection, live, bytes, permit);
        return await AuthenticateFrozenAsync(frozen, credentials, ct);
    }

    static async Task<OfficialVeoPollRecoverMaterializationLiveResult> AuthenticateFrozenAsync(
        ProductionTaskInputSnapshot frozen,
        IProductionCredentialResolver? credentials,
        CancellationToken ct)
    {
        var reference = ProductionFrozenCredential.FromSnapshot(frozen);
        if (credentials is null || reference is null)
            return Blocked("BLOCKED — " + ProductionCredentialResolver.Unresolved);

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
                return Blocked("BLOCKED — " + ProductionCredentialResolver.Unresolved);
            await authenticator.AuthorizeFrozenAsync(material, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            if (authenticator.AcceptedCredentialId != reference.CredentialId
                || authenticator.AcceptedCredentialVersionId != reference.CredentialVersionId)
                return Blocked("BLOCKED — " + ProductionCredentialResolver.Unresolved);
            return Blocked("BLOCKED — " + ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Blocked("BLOCKED — " + ex.Message);
        }

        return Blocked("BLOCKED — " + ProductionCredentialResolver.Unresolved);
    }

    static byte[]? ReadExistingKeyframeBytes(string videoRoot, string? storageKey)
    {
        var raw = (storageKey ?? "").Trim().Replace('\\', '/');
        var i = raw.LastIndexOf('/');
        if (i <= 0 || i >= raw.Length - 1) return null;
        var path = Path.Combine(videoRoot, raw[..i], raw[(i + 1)..] + ".mp4");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    static OfficialVeoPollRecoverMaterializationLiveResult Blocked(string verdict) =>
        new(
            new OfficialVeoPollResult(
                false, false, OfficialVeoPollStates.Blocked, ProductionFailureReasons.ExecutionAuthorizationRequired,
                OfficialVeoPollStates.Blocked, ExpectedStartAuthorizationHash, ExpectedTaskId, ExpectedAttemptId,
                ExpectedSnapshotId, ExpectedSnapshotHash, ExpectedPermitId, ExpectedPermitHash, "UNKNOWN",
                ExpectedOperationName, ExpectedEncodedOperationId, null, Array.Empty<int>(),
                0, 0, 1, 0, 0, 0, null, null, null, null, null, null, Array.Empty<string>()),
            new OfficialVeoPollResult(
                false, false, OfficialVeoPollStates.Blocked, ProductionFailureReasons.ExecutionAuthorizationRequired,
                OfficialVeoPollStates.Blocked, ExpectedStartAuthorizationHash, ExpectedTaskId, ExpectedAttemptId,
                ExpectedSnapshotId, ExpectedSnapshotHash, ExpectedPermitId, ExpectedPermitHash, "UNKNOWN",
                ExpectedOperationName, ExpectedEncodedOperationId, null, Array.Empty<int>(),
                0, 0, 1, 0, 0, 0, null, null, null, null, null, null, Array.Empty<string>()),
            verdict, NotReady, null, VeoHttpConfigPrecedence.DefaultLocation, 0, 0);
}

public sealed class DirectoryProductionArtifactFileStore : IProductionArtifactFileStore
{
    readonly string _root;
    public DirectoryProductionArtifactFileStore(string root) => _root = root;

    public string Write(string folder, string fileId, byte[] bytes)
    {
        _ = (folder, fileId, bytes, _root);
        return ProductionArtifactPhysicalWrite.CloseUnprefixedProductionWrite();
    }

    public byte[]? Read(string storageKey)
    {
        var raw = (storageKey ?? "").Trim().Replace('\\', '/');
        var i = raw.LastIndexOf('/');
        if (i <= 0 || i >= raw.Length - 1) return null;
        var path = Path.Combine(_root, raw[..i], raw[(i + 1)..] + ".mp4");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
