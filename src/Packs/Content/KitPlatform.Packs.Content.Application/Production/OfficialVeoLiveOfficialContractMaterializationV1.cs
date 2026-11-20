using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoLiveOfficialContractMaterializationRequest(
    IProductionWorkStore Store,
    IProductionExecutionPermitStore Permits,
    ProductionAiSelection? Selection,
    byte[] KeyframeBytes,
    IProductionAiSelectionStore? Selections = null,
    string? ProjectId = null,
    bool DryRun = false,
    bool RequireOfficialLiveKeyframeHash = false,
    IProductionCredentialResolver? Credentials = null,
    ProductionCredentialReference? Credential = null);

public sealed record OfficialVeoLiveOfficialContractMaterializationPreflight(
    bool Pass,
    string? BlockReason,
    bool OfficialTarget,
    bool OfficialSelection,
    int CreateVideoTaskCount,
    bool CreateVideoTaskWillBeCreatedByFreeze,
    int OfficialSnapshotCount,
    int OfficialPermitCount,
    int IssuedPermitCount,
    int ConsumedPermitCount,
    bool KeyframeVerified,
    string? KeyframeHash,
    bool FreezeReconstructable,
    bool PermitReconstructable);

public sealed record OfficialVeoLiveOfficialContractMaterializationResult(
    bool Materialized,
    string State,
    string Verdict,
    string? BlockReason,
    string Handoff,
    OfficialVeoLiveOfficialContractMaterializationPreflight Preflight,
    Guid? SnapshotId,
    string? SnapshotHash,
    bool SnapshotCreated,
    bool SnapshotReused,
    string? SnapshotMethod,
    IReadOnlyList<string> FrozenFieldResults,
    Guid? PermitId,
    string? PermitHash,
    string? PermitReason,
    string? PermitStatus,
    bool PermitCreated,
    bool PermitReused,
    string? PermitMethod,
    string? AuthorizationHash,
    string? AuthorizationState,
    OfficialVeoExecutionAuthorizationResult? Authorization,
    int SnapshotBefore,
    int SnapshotAfter,
    int PermitBefore,
    int PermitAfter);

/// <summary>
/// CK18B: materialize official CK12A snapshot + CK13B permit via existing FreezeAsync / IssueAsync.
/// No Vertex, OAuth, consume, or ProductionAttempt.
/// </summary>
public static class OfficialVeoLiveOfficialContractMaterializationV1
{
    public const string OfficialKeyframeSha256 =
        "9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57";
    public const string ReadyForCk18RealStart = "READY_FOR_CK18_REAL_START";
    public const string NotReadyForCk18RealStart = "NOT_READY_FOR_CK18_REAL_START";
    public const string VerdictPass = "PASS — OFFICIAL SNAPSHOT + PERMIT MATERIALIZED";
    public const string VerdictSnapshotFailed = "BLOCKED — SNAPSHOT MATERIALIZATION FAILED";
    public const string VerdictSnapshotVerifyFailed = "BLOCKED — SNAPSHOT VERIFICATION FAILED";
    public const string VerdictPermitFailed = "BLOCKED — PERMIT MATERIALIZATION FAILED";
    public const string VerdictPermitVerifyFailed = "BLOCKED — PERMIT VERIFICATION FAILED";
    public const string VerdictAuthMismatch = "BLOCKED — AUTHORIZATION CONTRACT MISMATCH";
    public const string VerdictAmbiguous = "BLOCKED — AMBIGUOUS LIVE STATE";
    public const string FreezeMethod = "OfficialCreateVideoFreezeV1.FreezeAsync";
    public const string IssueMethod = "OfficialCreateVideoPermitAuthorizationV1.IssueAsync";

    public static async Task<OfficialVeoLiveOfficialContractMaterializationResult> MaterializeAsync(
        OfficialVeoLiveOfficialContractMaterializationRequest request,
        CancellationToken ct = default)
    {
        var pre = await PreflightAsync(request, ct);
        var snapBefore = pre.OfficialSnapshotCount;
        var permitBefore = pre.OfficialPermitCount;
        if (!pre.Pass)
            return Fail(pre, MapPreflightVerdict(pre), pre.BlockReason, snapBefore, permitBefore);

        if (request.DryRun)
            return Fail(pre, VerdictSnapshotFailed, "DRY_RUN", snapBefore, permitBefore);

        var selection = await ResolveSelectionAsync(request, ct);
        OfficialCreateVideoFreezeResult? frozen;
        try
        {
            var sources = OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(request.KeyframeBytes, KeyframeHashOf(request));
            frozen = await OfficialCreateVideoFreezeV1.FreezeAsync(
                request.Store, sources, request.Credentials, request.Credential, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(pre, VerdictSnapshotFailed, CodeOf(ex.Message), snapBefore, permitBefore);
        }

        var reloaded = await ReloadOfficialSnapshotsAsync(request.Store, ct);
        if (reloaded.Count != 1)
            return Fail(pre, reloaded.Count > 1 ? VerdictAmbiguous : VerdictSnapshotVerifyFailed,
                reloaded.Count > 1
                    ? ProductionFailureReasons.AmbiguousLiveState
                    : ProductionFailureReasons.SnapshotVerificationFailed,
                snapBefore, permitBefore, frozen.Snapshot);

        var snap = reloaded[0];
        if (snap.SnapshotId != frozen.Snapshot.SnapshotId
            || !string.Equals(snap.SnapshotHash, frozen.Snapshot.SnapshotHash, StringComparison.Ordinal))
            return Fail(pre, VerdictSnapshotVerifyFailed, ProductionFailureReasons.InputSnapshotMismatch,
                snapBefore, permitBefore, snap);

        var live = LiveOf(snap);
        var fields = AssertFrozenFields(snap, live, KeyframeHashOf(request));
        if (fields.Any(f => !f.EndsWith("=PASS", StringComparison.Ordinal)))
            return Fail(pre, VerdictSnapshotVerifyFailed, ProductionFailureReasons.InputSnapshotMismatch,
                snapBefore, permitBefore, snap, frozenFields: fields);

        var rows = await request.Permits.ListByTargetAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        var issued = Issued(rows);
        if (issued.Length > 1)
            return Fail(pre, VerdictAmbiguous, ProductionFailureReasons.LiveOfficialPermitAmbiguous,
                snapBefore, permitBefore, snap, frozenFields: fields);
        if (rows.Any(Consumed))
            return Fail(pre, VerdictPermitFailed, PerformanceContractIds.OfficialFilmForbidden,
                snapBefore, permitBefore, snap, frozenFields: fields);

        OfficialCreateVideoPermitIssueResult issuedResult;
        try
        {
            issuedResult = await OfficialCreateVideoPermitAuthorizationV1.IssueAsync(
                request.Permits,
                new OfficialCreateVideoPermitIssueRequest(
                    RealVertexExecutionV1Lock.OfficialScope,
                    selection,
                    snap,
                    live),
                ct);
        }
        catch (InvalidOperationException ex)
        {
            return Fail(pre, VerdictPermitFailed, CodeOf(ex.Message),
                snapBefore, permitBefore, snap, frozenFields: fields);
        }

        var afterPermits = await request.Permits.ListByTargetAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        var afterIssued = Issued(afterPermits);
        if (afterIssued.Length != 1)
            return Fail(pre, afterIssued.Length > 1 ? VerdictAmbiguous : VerdictPermitVerifyFailed,
                afterIssued.Length > 1
                    ? ProductionFailureReasons.LiveOfficialPermitAmbiguous
                    : ProductionFailureReasons.PermitVerificationFailed,
                snapBefore, permitBefore, snap, issuedResult.Permit, fields);
        var permit = afterIssued[0];
        if (permit.PermitId != issuedResult.Permit.PermitId
            || permit.ConsumedAt is not null
            || !string.Equals(permit.Status, ProductionExecutionPermitStatuses.Issued, StringComparison.Ordinal))
            return Fail(pre, VerdictPermitVerifyFailed, ProductionFailureReasons.PermitVerificationFailed,
                snapBefore, permitBefore, snap, permit, fields);

        var quote = FamixaProviderCostCatalog.Quote(
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            null,
            FamixaVeo31OfficialPricing.ProductionDefault);
        var expectedReason = OfficialCreateVideoPermitAuthorizationV1.BindingReason(selection, snap, quote);
        if (!string.Equals(permit.Reason, expectedReason, StringComparison.Ordinal)
            || !string.Equals(permit.Reason, issuedResult.BindingReason, StringComparison.Ordinal))
            return Fail(pre, VerdictPermitVerifyFailed, ProductionFailureReasons.LiveOfficialPermitSnapshotMismatch,
                snapBefore, permitBefore, snap, permit, fields);

        var project = string.IsNullOrWhiteSpace(request.ProjectId)
            ? OfficialVeoHttpCompositionV1.DefaultComposeProject
            : request.ProjectId.Trim();
        var authRequest = new OfficialVeoExecutionAuthorizationRequest(
            RealVertexExecutionV1Lock.OfficialScope,
            ProductionActions.CreateVideo,
            selection,
            snap,
            live,
            request.KeyframeBytes,
            "image/png",
            CostPolicyDecision.FromCatalog(
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                snap.DurationSec),
            FamixaVeo31OfficialPricing.ProductionDefault,
            permit,
            ProjectId: project);
        var authz = OfficialVeoExecutionAuthorizationV1.Evaluate(authRequest);
        var recomputed = OfficialVeoExecutionAuthorizationV1.ComputeAuthorizationHash(
            authRequest,
            authz.PaidNetwork!,
            authz.Authenticator!,
            authz.HttpComposition!);
        if (!authz.Authorized
            || !string.Equals(authz.State, ProductionFailureReasons.ExecutionAuthorized, StringComparison.Ordinal)
            || !string.Equals(authz.AuthorizationHash, recomputed, StringComparison.Ordinal))
        {
            return new OfficialVeoLiveOfficialContractMaterializationResult(
                false,
                ProductionFailureReasons.AuthorizationContractMismatch,
                VerdictAuthMismatch,
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                NotReadyForCk18RealStart,
                pre,
                snap.SnapshotId,
                snap.SnapshotHash,
                frozen.Created,
                frozen.Reused,
                FreezeMethod,
                fields,
                permit.PermitId,
                permit.PermitHash,
                permit.Reason,
                permit.Status,
                issuedResult.Created,
                issuedResult.Reused,
                IssueMethod,
                recomputed,
                authz.State,
                authz,
                snapBefore,
                1,
                permitBefore,
                afterPermits.Count);
        }

        return new OfficialVeoLiveOfficialContractMaterializationResult(
            true,
            ProductionFailureReasons.LiveOfficialContractMaterialized,
            VerdictPass,
            null,
            ReadyForCk18RealStart,
            pre,
            snap.SnapshotId,
            snap.SnapshotHash,
            frozen.Created,
            frozen.Reused,
            FreezeMethod,
            fields,
            permit.PermitId,
            permit.PermitHash,
            permit.Reason,
            permit.Status,
            issuedResult.Created,
            issuedResult.Reused,
            IssueMethod,
            authz.AuthorizationHash,
            authz.State,
            authz,
            snapBefore,
            1,
            permitBefore,
            afterPermits.Count);
    }

    public static async Task<OfficialVeoLiveOfficialContractMaterializationPreflight> PreflightAsync(
        OfficialVeoLiveOfficialContractMaterializationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            RealVertexExecutionV1Lock.RequireAuthorizedTarget(
                RealVertexExecutionV1Lock.OfficialScope,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId);
        }
        catch (InvalidOperationException ex)
        {
            return Blocked(CodeOf(ex.Message));
        }

        ProductionAiSelection selection;
        try { selection = await ResolveSelectionAsync(request, ct); }
        catch (InvalidOperationException ex)
        {
            return Blocked(CodeOf(ex.Message));
        }

        if (selection.SelectionId != OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId
            || !string.Equals(selection.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(selection.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)
            || selection.BuildId != RealVertexExecutionV1Lock.BuildId
            || !string.Equals(selection.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal)
            || !string.Equals(selection.Stage, ProductionAiSelectionStages.Video, StringComparison.Ordinal)
            || !string.Equals(selection.Status, ProductionAiSelectionStatuses.Active, StringComparison.Ordinal))
            return Blocked(ProductionFailureReasons.SelectionConflict, officialSelection: false);

        var tasks = (await request.Store.ListTasksByScopeAsync(
            RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ct))
            .Where(t => t.Action == ProductionActions.CreateVideo).ToArray();
        if (tasks.Length > 1)
            return Blocked(ProductionFailureReasons.AmbiguousLiveState, officialSelection: true,
                taskCount: tasks.Length);

        var snaps = await ReloadOfficialSnapshotsAsync(request.Store, ct);
        if (snaps.Count > 1)
            return Blocked(ProductionFailureReasons.AmbiguousLiveState, officialSelection: true,
                taskCount: tasks.Length, snapCount: snaps.Count);
        if (snaps.Count == 1 && !IsOfficialContract(snaps[0], KeyframeHashOf(request)))
            return Blocked(ProductionFailureReasons.InputSnapshotMismatch, officialSelection: true,
                taskCount: tasks.Length, snapCount: 1);

        var rows = await request.Permits.ListByTargetAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            ct);
        var issued = Issued(rows);
        var consumed = rows.Count(Consumed);
        if (rows.Count > 1 || issued.Length > 1)
            return Blocked(ProductionFailureReasons.LiveOfficialPermitAmbiguous, officialSelection: true,
                taskCount: tasks.Length, snapCount: snaps.Count, permitCount: rows.Count,
                issuedCount: issued.Length, consumedCount: consumed);
        if (consumed > 0)
            return Blocked(PerformanceContractIds.OfficialFilmForbidden, officialSelection: true,
                taskCount: tasks.Length, snapCount: snaps.Count, permitCount: rows.Count,
                issuedCount: issued.Length, consumedCount: consumed);

        var art = await request.Store.GetArtifactAsync(OfficialCreateVideoFreezeV1.OfficialKeyframeId, ct);
        var hash = KeyframeHashOf(request);
        var keyframeOk = art is not null
            && hash.Length == 64
            && string.Equals(hash, art.ByteSha256, StringComparison.OrdinalIgnoreCase)
            && art.ArtifactId == OfficialCreateVideoFreezeV1.OfficialKeyframeId
            && (!request.RequireOfficialLiveKeyframeHash
                || string.Equals(hash, OfficialKeyframeSha256, StringComparison.OrdinalIgnoreCase));
        if (!keyframeOk)
            return Blocked(ProductionFailureReasons.InputSnapshotMismatch, officialSelection: true,
                taskCount: tasks.Length, snapCount: snaps.Count, permitCount: rows.Count,
                issuedCount: issued.Length, consumedCount: consumed, keyframe: false, keyframeHash: hash);

        return new OfficialVeoLiveOfficialContractMaterializationPreflight(
            true,
            null,
            true,
            true,
            tasks.Length,
            tasks.Length == 0,
            snaps.Count,
            rows.Count,
            issued.Length,
            consumed,
            true,
            hash,
            true,
            true);
    }

    static async Task<ProductionAiSelection> ResolveSelectionAsync(
        OfficialVeoLiveOfficialContractMaterializationRequest request, CancellationToken ct)
    {
        if (request.Selection is { } provided)
        {
            if (request.Selections is not null)
            {
                var live = await request.Selections.GetActiveAsync(
                    RealVertexExecutionV1Lock.BuildId,
                    RealVertexExecutionV1Lock.ShotCode,
                    ProductionAiSelectionStages.Video,
                    ct);
                if (live is not null && live.SelectionId != provided.SelectionId)
                    throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
            }
            return provided;
        }

        if (request.Selections is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);
        return await request.Selections.GetActiveAsync(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionAiSelectionStages.Video,
            ct) ?? throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);
    }

    static async Task<IReadOnlyList<ProductionTaskInputSnapshot>> ReloadOfficialSnapshotsAsync(
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

    static string KeyframeHashOf(OfficialVeoLiveOfficialContractMaterializationRequest request) =>
        request.KeyframeBytes is { Length: >= 32 }
            ? KitVideoIntegrityRules.Sha256Hex(request.KeyframeBytes)
            : "";

    static bool IsOfficialContract(ProductionTaskInputSnapshot snap, string keyframeHash) =>
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
        && string.Equals(snap.KeyframeHash, keyframeHash, StringComparison.OrdinalIgnoreCase)
        && CreateVideoActionContractGate.IsPresent(snap.SnapshotHash);

    static IReadOnlyList<string> AssertFrozenFields(
        ProductionTaskInputSnapshot snap, ShotExecutionInput live, string keyframeHash)
    {
        var adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        try { ProductionFrozenCreateVideoMatch.Assert(snap, live, adapter); }
        catch (InvalidOperationException)
        {
            return
            [
                "ProviderId=FAIL", "ModelId=FAIL", "Story=FAIL", "ActingBeat=FAIL", "VisualContract=FAIL",
                "DurationSec=FAIL", "TimingHash=FAIL", "PerformanceSnapshotId=FAIL", "PerformanceHash=FAIL",
                "KeyframeArtifactId=FAIL", "KeyframeHash=FAIL", "SnapshotHash=FAIL",
            ];
        }

        return
        [
            Flag("ProviderId", snap.ProviderId == RealVertexExecutionV1Lock.ProviderId),
            Flag("ModelId", snap.ModelId == RealVertexExecutionV1Lock.ModelId),
            Flag("Story", snap.Story == ProductionEp01Sh02IngressCanon.Story),
            Flag("ActingBeat", snap.ActingBeat == ProductionEp01Sh02IngressCanon.ActingBeat),
            Flag("VisualContract", snap.VisualContract == ProductionEp01Sh02IngressCanon.VisualContract),
            Flag("DurationSec", snap.DurationSec == OfficialCreateVideoFreezeV1.OfficialDurationSec),
            Flag("TimingHash", snap.TimingHash == ShotExecutionCompiler.Hash(OfficialCreateVideoFreezeV1.OfficialTiming)),
            Flag("PerformanceSnapshotId", snap.PerformanceSnapshotId == OfficialCreateVideoFreezeV1.OfficialPerformanceId),
            Flag("PerformanceHash", snap.PerformanceHash == OfficialCreateVideoFreezeV1.OfficialPerformanceHash),
            Flag("KeyframeArtifactId", snap.KeyframeArtifactId == OfficialCreateVideoFreezeV1.OfficialKeyframeId),
            Flag("KeyframeHash", string.Equals(snap.KeyframeHash, keyframeHash, StringComparison.OrdinalIgnoreCase)),
            Flag("SnapshotHash", CreateVideoActionContractGate.IsPresent(snap.SnapshotHash)),
        ];
    }

    static string Flag(string name, bool pass) => name + (pass ? "=PASS" : "=FAIL");

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

    static ProductionExecutionPermit[] Issued(IEnumerable<ProductionExecutionPermit> rows) =>
        rows.Where(p =>
            string.Equals(p.Status, ProductionExecutionPermitStatuses.Issued, StringComparison.Ordinal)
            && p.ConsumedAt is null).ToArray();

    static bool Consumed(ProductionExecutionPermit p) =>
        string.Equals(p.Status, ProductionExecutionPermitStatuses.Consumed, StringComparison.Ordinal)
        || p.ConsumedAt is not null;

    static string MapPreflightVerdict(OfficialVeoLiveOfficialContractMaterializationPreflight pre)
    {
        if (pre.BlockReason == ProductionFailureReasons.AmbiguousLiveState
            || pre.BlockReason == ProductionFailureReasons.LiveOfficialPermitAmbiguous)
            return VerdictAmbiguous;
        if (pre.BlockReason == PerformanceContractIds.OfficialFilmForbidden
            || pre.BlockReason == ProductionFailureReasons.PermitConflict)
            return VerdictPermitFailed;
        if (pre.BlockReason == ProductionFailureReasons.InputSnapshotMismatch
            && !pre.KeyframeVerified)
            return VerdictSnapshotVerifyFailed;
        if (pre.BlockReason == ProductionFailureReasons.InputSnapshotMismatch)
            return VerdictSnapshotVerifyFailed;
        return VerdictSnapshotFailed;
    }

    static OfficialVeoLiveOfficialContractMaterializationPreflight Blocked(
        string reason,
        bool officialSelection = false,
        int taskCount = 0,
        int snapCount = 0,
        int permitCount = 0,
        int issuedCount = 0,
        int consumedCount = 0,
        bool keyframe = false,
        string? keyframeHash = null) =>
        new(false, reason, true, officialSelection, taskCount, taskCount == 0, snapCount, permitCount,
            issuedCount, consumedCount, keyframe, keyframeHash, false, false);

    static OfficialVeoLiveOfficialContractMaterializationResult Fail(
        OfficialVeoLiveOfficialContractMaterializationPreflight pre,
        string verdict,
        string? reason,
        int snapBefore,
        int permitBefore,
        ProductionTaskInputSnapshot? snap = null,
        ProductionExecutionPermit? permit = null,
        IReadOnlyList<string>? frozenFields = null) =>
        new(
            false,
            reason ?? verdict,
            verdict,
            reason,
            NotReadyForCk18RealStart,
            pre,
            snap?.SnapshotId,
            snap?.SnapshotHash,
            false,
            false,
            null,
            frozenFields ?? [],
            permit?.PermitId,
            permit?.PermitHash,
            permit?.Reason,
            permit?.Status,
            false,
            false,
            null,
            null,
            null,
            null,
            snapBefore,
            snap is null ? snapBefore : Math.Max(snapBefore, 1),
            permitBefore,
            permit is null ? permitBefore : Math.Max(permitBefore, 1));

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
