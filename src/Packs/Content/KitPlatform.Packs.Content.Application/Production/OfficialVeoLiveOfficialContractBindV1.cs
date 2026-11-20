using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoLiveOfficialContractBindRequest(
    IProductionWorkStore Store,
    IProductionExecutionPermitStore Permits,
    ProductionAiSelection? Selection,
    byte[]? KeyframeBytes = null,
    string? ProjectId = null);

public sealed record OfficialVeoLiveOfficialContractBindResult(
    bool Bound,
    string State,
    string? BlockReason,
    string Handoff,
    Guid? SnapshotId,
    string? SnapshotHash,
    Guid? PermitId,
    string? PermitHash,
    string? PermitStatus,
    string? AuthorizationHash,
    decimal? DurationSec,
    string ProviderId,
    string ModelId,
    Guid SelectionId,
    OfficialVeoExecutionAuthorizationResult? Authorization);

/// <summary>
/// CK18A: bind EXISTING official CK12A snapshot + EXISTING official CK13B permit.
/// Read-only. Does not freeze, issue, consume, OAuth, or Start.
/// </summary>
public static class OfficialVeoLiveOfficialContractBindV1
{
    public const string ReadyForCk18RealStart = "READY_FOR_CK18_REAL_START";
    public const string NotReadyForCk18RealStart = "NOT_READY_FOR_CK18_REAL_START";

    public static async Task<OfficialVeoLiveOfficialContractBindResult> BindAsync(
        OfficialVeoLiveOfficialContractBindRequest request,
        CancellationToken ct = default)
    {
        var selection = request.Selection;
        var snaps = await LoadOfficialCreateVideoSnapshotsAsync(request.Store, ct);
        if (snaps.Count == 0)
            return Fail(ProductionFailureReasons.LiveOfficialSnapshotNotBound, selection);

        var matching = new List<ProductionTaskInputSnapshot>();
        ProductionTaskInputSnapshot? mismatched = null;
        foreach (var snap in snaps)
        {
            if (IsOfficialContract(snap))
                matching.Add(snap);
            else
                mismatched = snap;
        }

        if (matching.Count == 0)
        {
            var reason = mismatched is null
                ? ProductionFailureReasons.LiveOfficialSnapshotNotBound
                : FirstSnapshotBlock(mismatched);
            return Fail(reason, selection);
        }

        if (matching.Count > 1)
            return Fail(ProductionFailureReasons.LiveOfficialSnapshotNotBound, selection);

        var frozen = matching[0];
        var keyframeBlock = await VerifyKeyframeAsync(request.Store, frozen, request.KeyframeBytes, ct);
        if (keyframeBlock is not null)
            return Fail(keyframeBlock, selection, frozen);

        var live = LiveOf(frozen);
        try
        {
            ProductionFrozenCreateVideoMatch.Assert(
                frozen, live, new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()));
        }
        catch (InvalidOperationException ex)
        {
            return Fail(CodeOf(ex.Message), selection, frozen);
        }

        var rows = await request.Permits.ListByTargetAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        var issued = rows.Where(p =>
            string.Equals(p.Status, ProductionExecutionPermitStatuses.Issued, StringComparison.Ordinal)
            && p.ConsumedAt is null).ToArray();
        if (issued.Length > 1)
            return Fail(ProductionFailureReasons.LiveOfficialPermitAmbiguous, selection, frozen);
        if (issued.Length == 0)
        {
            var consumed = rows.Any(p =>
                string.Equals(p.Status, ProductionExecutionPermitStatuses.Consumed, StringComparison.Ordinal)
                || p.ConsumedAt is not null);
            return Fail(
                consumed
                    ? PerformanceContractIds.OfficialFilmForbidden
                    : ProductionFailureReasons.LiveOfficialPermitNotBound,
                selection,
                frozen);
        }

        var permit = issued[0];
        if (selection is null)
            return Fail(ProductionFailureReasons.MissingProductionAiSelection, selection, frozen, permit);
        var quote = FamixaProviderCostCatalog.Quote(
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            null,
            FamixaVeo31OfficialPricing.ProductionDefault);
        var expectedReason = OfficialCreateVideoPermitAuthorizationV1.BindingReason(selection, frozen, quote);
        if (!string.Equals(permit.Reason, expectedReason, StringComparison.Ordinal))
            return Fail(ProductionFailureReasons.LiveOfficialPermitSnapshotMismatch, selection, frozen, permit);

        var authRequest = new OfficialVeoExecutionAuthorizationRequest(
            RealVertexExecutionV1Lock.OfficialScope,
            ProductionActions.CreateVideo,
            selection,
            frozen,
            live,
            request.KeyframeBytes,
            "image/png",
            CostPolicyDecision.FromCatalog(
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                frozen.DurationSec),
            FamixaVeo31OfficialPricing.ProductionDefault,
            permit,
            ProjectId: string.IsNullOrWhiteSpace(request.ProjectId)
                ? OfficialVeoHttpCompositionV1.DefaultComposeProject
                : request.ProjectId.Trim());
        var authz = OfficialVeoExecutionAuthorizationV1.Evaluate(authRequest);
        var recomputed = OfficialVeoExecutionAuthorizationV1.ComputeAuthorizationHash(
            authRequest,
            authz.PaidNetwork ?? new OfficialVeoPaidNetworkAuthorizationResult(
                false, ProductionFailureReasons.PaidNetworkForbidden, ProductionFailureReasons.PaidNetworkForbidden, null),
            authz.Authenticator ?? new OfficialVeoAuthenticatorCompositionResult(
                false, ProductionFailureReasons.AuthenticatorNotReady, ProductionFailureReasons.AuthenticatorNotReady,
                new ProductionVeoHttpAuthenticator(), null),
            authz.HttpComposition ?? new OfficialVeoHttpCompositionResult(
                false, ProductionFailureReasons.HttpCompositionUnavailable, ProductionFailureReasons.HttpCompositionUnavailable,
                null, null, null, null, null));
        if (!authz.Authorized
            || !string.Equals(authz.AuthorizationHash, recomputed, StringComparison.Ordinal))
        {
            return Fail(
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                selection,
                frozen,
                permit,
                recomputed,
                authz);
        }

        return new OfficialVeoLiveOfficialContractBindResult(
            true,
            ProductionFailureReasons.LiveOfficialContractBound,
            null,
            ReadyForCk18RealStart,
            frozen.SnapshotId,
            frozen.SnapshotHash,
            permit.PermitId,
            permit.PermitHash,
            permit.Status,
            authz.AuthorizationHash,
            frozen.DurationSec,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            authz);
    }

    static async Task<IReadOnlyList<ProductionTaskInputSnapshot>> LoadOfficialCreateVideoSnapshotsAsync(
        IProductionWorkStore store, CancellationToken ct)
    {
        var tasks = await store.ListTasksByScopeAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ct);
        var snaps = new List<ProductionTaskInputSnapshot>();
        foreach (var task in tasks.Where(t => t.Action == ProductionActions.CreateVideo))
        {
            var snap = await store.GetSnapshotByTaskAsync(task.TaskId, ct);
            if (snap is not null)
                snaps.Add(snap);
        }

        return snaps;
    }

    static bool IsOfficialContract(ProductionTaskInputSnapshot snap) =>
        string.Equals(snap.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.Ordinal)
        && string.Equals(snap.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.Ordinal)
        && snap.DurationSec == OfficialCreateVideoFreezeV1.OfficialDurationSec
        && string.Equals(snap.TimingHash, ShotExecutionCompiler.Hash(OfficialCreateVideoFreezeV1.OfficialTiming), StringComparison.Ordinal)
        && snap.PerformanceSnapshotId == OfficialCreateVideoFreezeV1.OfficialPerformanceId
        && string.Equals(snap.PerformanceHash, OfficialCreateVideoFreezeV1.OfficialPerformanceHash, StringComparison.Ordinal)
        && snap.KeyframeArtifactId == OfficialCreateVideoFreezeV1.OfficialKeyframeId
        && snap.Story == ProductionEp01Sh02IngressCanon.Story
        && snap.ActingBeat == ProductionEp01Sh02IngressCanon.ActingBeat
        && snap.VisualContract == ProductionEp01Sh02IngressCanon.VisualContract
        && CreateVideoActionContractGate.IsPresent(snap.KeyframeHash)
        && CreateVideoActionContractGate.IsPresent(snap.SnapshotHash);

    static string FirstSnapshotBlock(ProductionTaskInputSnapshot snap)
    {
        if (snap.DurationSec != OfficialCreateVideoFreezeV1.OfficialDurationSec
            || !string.Equals(snap.TimingHash, ShotExecutionCompiler.Hash(OfficialCreateVideoFreezeV1.OfficialTiming), StringComparison.Ordinal)
            || snap.PerformanceSnapshotId != OfficialCreateVideoFreezeV1.OfficialPerformanceId
            || !string.Equals(snap.PerformanceHash, OfficialCreateVideoFreezeV1.OfficialPerformanceHash, StringComparison.Ordinal)
            || snap.KeyframeArtifactId != OfficialCreateVideoFreezeV1.OfficialKeyframeId
            || snap.Story != ProductionEp01Sh02IngressCanon.Story
            || snap.ActingBeat != ProductionEp01Sh02IngressCanon.ActingBeat
            || snap.VisualContract != ProductionEp01Sh02IngressCanon.VisualContract)
            return ProductionFailureReasons.InputSnapshotMismatch;
        if (!string.Equals(snap.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snap.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.ProviderModelMismatch;
        return ProductionFailureReasons.InputSnapshotMismatch;
    }

    static async Task<string?> VerifyKeyframeAsync(
        IProductionWorkStore store,
        ProductionTaskInputSnapshot frozen,
        byte[]? bytes,
        CancellationToken ct)
    {
        if (frozen.KeyframeArtifactId != OfficialCreateVideoFreezeV1.OfficialKeyframeId)
            return ProductionFailureReasons.InputSnapshotMismatch;
        var art = await store.GetArtifactAsync(OfficialCreateVideoFreezeV1.OfficialKeyframeId, ct);
        if (art is null || !string.Equals(art.ByteSha256, frozen.KeyframeHash, StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.InputSnapshotMismatch;
        if (bytes is not null)
        {
            if (bytes.Length < 32)
                return ProductionFailureReasons.KeyframeBytesMissing;
            var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
            if (!string.Equals(hash, frozen.KeyframeHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(hash, art.ByteSha256, StringComparison.OrdinalIgnoreCase))
                return ProductionFailureReasons.InputSnapshotMismatch;
        }

        return null;
    }

    static ShotExecutionInput LiveOf(ProductionTaskInputSnapshot snap) =>
        new(
            snap.Story,
            snap.ActingBeat,
            snap.VisualContract,
            "CHAR-001",
            snap.KeyframeHash,
            OfficialCreateVideoFreezeV1.OfficialTiming,
            "",
            "",
            snap.KeyframeArtifactId,
            snap.KeyframeHash,
            snap.AspectRatio,
            snap.DurationSec,
            snap.CompilerVersion,
            snap.KeyframeArtifactId is { } kf ? [kf] : null,
            PerformanceSnapshotId: snap.PerformanceSnapshotId,
            PerformanceHash: snap.PerformanceHash);

    static OfficialVeoLiveOfficialContractBindResult Fail(
        string reason,
        ProductionAiSelection? selection,
        ProductionTaskInputSnapshot? frozen = null,
        ProductionExecutionPermit? permit = null,
        string? hash = null,
        OfficialVeoExecutionAuthorizationResult? authz = null) =>
        new(
            false,
            reason,
            reason,
            NotReadyForCk18RealStart,
            frozen?.SnapshotId,
            frozen?.SnapshotHash,
            permit?.PermitId,
            permit?.PermitHash,
            permit?.Status,
            hash,
            frozen?.DurationSec,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            selection?.SelectionId ?? OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            authz);

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
