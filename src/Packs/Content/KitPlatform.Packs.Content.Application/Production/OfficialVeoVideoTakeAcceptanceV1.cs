using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoVideoTakeAcceptanceRequest(
    IProductionWorkStore Store,
    IProductionArtifactFileStore Files,
    IProductionAiSelectionStore? Selections,
    IProductionExecutionPermitStore? Permits,
    IVeoMotionTransport Transport,
    Guid ExpectedArtifactId,
    Guid ExpectedTaskId,
    Guid ExpectedAttemptId,
    Guid ExpectedSnapshotId,
    string ExpectedSnapshotHash,
    Guid ExpectedSelectionId,
    string ExpectedAuthorizationHash,
    string ExpectedOperationName,
    string ExpectedEncodedOperationId,
    string ExpectedSha256,
    long ExpectedByteLength,
    string ExpectedMime,
    string ExpectedProviderId = RealVertexExecutionV1Lock.ProviderId,
    string ExpectedModelId = RealVertexExecutionV1Lock.ModelId);

public sealed record AuthoritativeVideoTake(
    Guid BuildId,
    string Shot,
    string Action,
    Guid TaskId,
    Guid AttemptId,
    Guid SnapshotId,
    string SnapshotHash,
    string ProviderId,
    string ModelId,
    Guid SelectionId,
    string OperationId,
    Guid ArtifactId,
    string ArtifactRole,
    string ByteSha256,
    long ByteLength,
    string MimeType,
    string VerificationStatus,
    string AcceptanceStatus);

public sealed record OfficialVeoVideoTakeAcceptanceResult(
    bool Accepted,
    bool Replayed,
    string Outcome,
    string? BlockReason,
    AuthoritativeVideoTake? Take,
    Guid? BindingId,
    int StartCount,
    int PollCount,
    int ProviderJobCount,
    int PermitIssueCount,
    int PermitConsumeCount,
    int SnapshotMutations,
    int SelectionMutations,
    IReadOnlyList<string> RedactionCheck);

/// <summary>
/// CK20 — accept the exact CK19 VIDEO_TAKE as the authoritative SH02 CREATE_VIDEO output.
/// No Start, Poll, permit, freeze, or content mutation.
/// </summary>
public static class OfficialVeoVideoTakeAcceptanceV1
{
    public const string VerdictPass = "PASS — AUTHORITATIVE VIDEO_TAKE ACCEPTED";
    public const string AcceptanceRole = ProductionBindingRoles.Accepted + ":" + ProductionArtifactRoles.VideoTake;
    public const string AcceptedStatus = "ACCEPTED";

    public static async Task<OfficialVeoVideoTakeAcceptanceResult> AcceptAsync(
        OfficialVeoVideoTakeAcceptanceRequest request,
        CancellationToken ct = default)
    {
        GuardTransport(request.Transport);
        var existing = await GetAuthoritativeVideoTakeAsync(request.Store, RealVertexExecutionV1Lock.BuildId, RealVertexExecutionV1Lock.ShotCode, ct);
        if (existing is { AcceptanceStatus: AcceptedStatus }
            && existing.ArtifactId == request.ExpectedArtifactId)
        {
            var replayCheck = await VerifyExactAsync(request, existing.ArtifactId, ct);
            if (replayCheck.Error is { } replayFail)
                return replayFail;
            return Ok(replayCheck.Take!, replayCheck.BindingId, replayed: true);
        }

        var verified = await VerifyExactAsync(request, request.ExpectedArtifactId, ct);
        if (verified.Error is { } fail)
            return fail;

        var task = await request.Store.GetTaskAsync(verified.Take!.TaskId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var attempt = await request.Store.GetAttemptAsync(verified.Take.AttemptId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        await EventAsync(request, task, attempt, verified.Take, ProductionEventTypes.VideoTakeAcceptanceStarted, ct);
        await EventAsync(request, task, attempt, verified.Take, ProductionEventTypes.ArtifactVerified, ct);

        var engine = new ProductionWorkExecutionEngine(request.Store);
        var binding = await engine.BindAsync(verified.Take.ArtifactId, AcceptanceRole, ct);
        var take = verified.Take with { AcceptanceStatus = AcceptedStatus };
        await EventAsync(request, task, attempt, take, ProductionEventTypes.VideoTakeAccepted, ct);
        await EventAsync(request, task, attempt, take, ProductionEventTypes.VideoTakeAcceptanceCompleted, ct);
        return Ok(take, binding.BindingId, replayed: false);
    }

    public static async Task<AuthoritativeVideoTake?> GetAuthoritativeVideoTakeAsync(
        IProductionWorkStore store,
        Guid buildId,
        string shotCode,
        CancellationToken ct = default)
    {
        var bind = await store.GetActiveBindingAsync(buildId, shotCode, AcceptanceRole, ct);
        if (bind is null) return null;
        var artifact = await store.GetArtifactAsync(bind.ArtifactId, ct);
        if (artifact is null || artifact.Role != ProductionArtifactRoles.VideoTake) return null;
        var task = await store.GetTaskAsync(artifact.TaskId, ct);
        var attempt = await store.GetAttemptAsync(artifact.AttemptId, ct);
        var snap = await store.GetSnapshotByTaskAsync(artifact.TaskId, ct);
        if (task is null || attempt is null || snap is null) return null;
        return new AuthoritativeVideoTake(
            task.Scope.BuildId,
            task.Scope.ShotCode,
            task.Action,
            task.TaskId,
            attempt.AttemptId,
            snap.SnapshotId,
            snap.SnapshotHash,
            attempt.ProviderId,
            attempt.ModelId,
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            DecodeOperation(attempt.ProviderJobId) ?? "",
            artifact.ArtifactId,
            artifact.Role,
            artifact.ByteSha256,
            artifact.SizeBytes,
            artifact.MimeType,
            artifact.VerificationStatus,
            AcceptedStatus);
    }

    static async Task<(OfficialVeoVideoTakeAcceptanceResult? Error, AuthoritativeVideoTake? Take, Guid? BindingId)> VerifyExactAsync(
        OfficialVeoVideoTakeAcceptanceRequest request,
        Guid artifactId,
        CancellationToken ct)
    {
        OfficialVeoVideoTakeAcceptanceResult Fail(string reason) =>
            new(false, false, reason, reason, null, null, 0, 0, 0, 0, 0, 0, 0, Array.Empty<string>());

        var artifact = await request.Store.GetArtifactAsync(artifactId, ct);
        if (artifact is null)
            return (Fail(ProductionFailureReasons.OutputArtifactNotFound), null, null);
        if (artifact.ArtifactId != request.ExpectedArtifactId)
            return (Fail(ProductionFailureReasons.OutputArtifactNotFound), null, null);
        if (artifact.Role != ProductionArtifactRoles.VideoTake)
            return (Fail(ProductionFailureReasons.ArtifactRoleMismatch), null, null);
        if (artifact.TaskId != request.ExpectedTaskId)
            return (Fail(ProductionFailureReasons.OutputTaskMismatch), null, null);
        if (artifact.AttemptId != request.ExpectedAttemptId)
            return (Fail(ProductionFailureReasons.OutputAttemptMismatch), null, null);
        if (!string.Equals(artifact.MimeType, request.ExpectedMime, StringComparison.OrdinalIgnoreCase))
            return (Fail(ProductionFailureReasons.ArtifactMimeMismatch), null, null);
        if (artifact.SizeBytes != request.ExpectedByteLength)
            return (Fail(ProductionFailureReasons.ArtifactLengthMismatch), null, null);
        if (!string.Equals(artifact.ByteSha256, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            return (Fail(ProductionFailureReasons.ArtifactHashMismatch), null, null);
        if (artifact.VerificationStatus != ProductionArtifactStatuses.Verified)
            return (Fail(ProductionFailureReasons.ArtifactVerificationFailed), null, null);

        var task = await request.Store.GetTaskAsync(artifact.TaskId, ct);
        if (task is null
            || task.Action != ProductionActions.CreateVideo
            || task.Scope.BuildId != RealVertexExecutionV1Lock.BuildId
            || !string.Equals(task.Scope.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal)
            || task.TaskId != request.ExpectedTaskId)
            return (Fail(ProductionFailureReasons.ShotOutputBindingInvalid), null, null);

        var takes = (await request.Store.ListArtifactsByTaskAsync(task.TaskId, ct))
            .Where(a => a.Role == ProductionArtifactRoles.VideoTake)
            .ToArray();
        if (takes.Length == 0)
            return (Fail(ProductionFailureReasons.OutputArtifactNotFound), null, null);
        if (takes.Length > 1)
            return (Fail(ProductionFailureReasons.OutputArtifactAmbiguous), null, null);
        if (takes[0].ArtifactId != artifact.ArtifactId)
            return (Fail(ProductionFailureReasons.OutputArtifactAmbiguous), null, null);

        var attempt = await request.Store.GetAttemptAsync(artifact.AttemptId, ct);
        if (attempt is null || attempt.TaskId != task.TaskId || attempt.AttemptId != request.ExpectedAttemptId)
            return (Fail(ProductionFailureReasons.OutputAttemptMismatch), null, null);
        if (!string.Equals(attempt.ProviderId, request.ExpectedProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(attempt.ModelId, request.ExpectedModelId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(task.ProviderId, request.ExpectedProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(task.ModelId, request.ExpectedModelId, StringComparison.OrdinalIgnoreCase))
            return (Fail(ProductionFailureReasons.ProviderModelMismatch), null, null);
        if (string.IsNullOrWhiteSpace(attempt.ProviderJobId))
            return (Fail(ProductionFailureReasons.OperationProvenanceMissing), null, null);
        var operation = DecodeOperation(attempt.ProviderJobId);
        if (string.IsNullOrWhiteSpace(operation)
            || !string.Equals(operation, request.ExpectedOperationName, StringComparison.Ordinal)
            || !string.Equals(attempt.ProviderJobId, request.ExpectedEncodedOperationId, StringComparison.Ordinal))
            return (Fail(ProductionFailureReasons.OperationProvenanceMismatch), null, null);

        var snap = await request.Store.GetSnapshotAsync(request.ExpectedSnapshotId, ct)
            ?? await request.Store.GetSnapshotByTaskAsync(task.TaskId, ct);
        if (snap is null)
            return (Fail(ProductionFailureReasons.InputNotFrozen), null, null);
        if (snap.SnapshotId != request.ExpectedSnapshotId
            || !string.Equals(snap.SnapshotHash, request.ExpectedSnapshotHash, StringComparison.Ordinal)
            || !string.Equals(artifact.InputSnapshotHash, request.ExpectedSnapshotHash, StringComparison.Ordinal))
            return (Fail(ProductionFailureReasons.FreezeMismatch), null, null);
        if (!string.Equals(snap.ProviderId, request.ExpectedProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(snap.ModelId, request.ExpectedModelId, StringComparison.OrdinalIgnoreCase))
            return (Fail(ProductionFailureReasons.ProviderModelMismatch), null, null);
        if (snap.DurationSec != OfficialCreateVideoFreezeV1.OfficialDurationSec)
            return (Fail(ProductionFailureReasons.DurationBindingMismatch), null, null);
        if (snap.PerformanceSnapshotId != OfficialCreateVideoFreezeV1.OfficialPerformanceId
            || !string.Equals(snap.PerformanceHash, OfficialCreateVideoFreezeV1.OfficialPerformanceHash, StringComparison.Ordinal)
            || !string.Equals(snap.TimingHash, ShotExecutionCompiler.Hash(OfficialCreateVideoFreezeV1.OfficialTiming), StringComparison.Ordinal)
            || snap.KeyframeArtifactId != OfficialCreateVideoFreezeV1.OfficialKeyframeId
            || string.IsNullOrWhiteSpace(snap.Story)
            || string.IsNullOrWhiteSpace(snap.ActingBeat)
            || string.IsNullOrWhiteSpace(snap.VisualContract))
            return (Fail(ProductionFailureReasons.FreezeMismatch), null, null);

        if (request.Selections is not null)
        {
            var selection = await request.Selections.GetActiveAsync(
                RealVertexExecutionV1Lock.BuildId,
                RealVertexExecutionV1Lock.ShotCode,
                ProductionAiSelectionStages.Video,
                ct);
            if (selection is null || selection.SelectionId != request.ExpectedSelectionId
                || !string.Equals(selection.ProviderId, request.ExpectedProviderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.ModelId, request.ExpectedModelId, StringComparison.OrdinalIgnoreCase))
                return (Fail(ProductionFailureReasons.ProviderModelMismatch), null, null);
        }

        var bytes = request.Files.Read(artifact.StorageKey);
        if (bytes is null || bytes.Length == 0)
            return (Fail(ProductionFailureReasons.OutputArtifactNotFound), null, null);
        if (bytes.Length != request.ExpectedByteLength)
            return (Fail(ProductionFailureReasons.ArtifactLengthMismatch), null, null);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.Equals(sha, request.ExpectedSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(sha, artifact.ByteSha256, StringComparison.OrdinalIgnoreCase))
            return (Fail(ProductionFailureReasons.ArtifactHashMismatch), null, null);
        if (!OfficialVeoOutputBytes.IsSupportedVideo(bytes)
            && !ProductionTestMp4.IsDecodable(bytes, out _))
            return (Fail(ProductionFailureReasons.ArtifactVerificationFailed), null, null);

        var take = new AuthoritativeVideoTake(
            task.Scope.BuildId,
            task.Scope.ShotCode,
            task.Action,
            task.TaskId,
            attempt.AttemptId,
            snap.SnapshotId,
            snap.SnapshotHash,
            attempt.ProviderId,
            attempt.ModelId,
            request.ExpectedSelectionId,
            request.ExpectedOperationName,
            artifact.ArtifactId,
            artifact.Role,
            artifact.ByteSha256,
            artifact.SizeBytes,
            artifact.MimeType,
            artifact.VerificationStatus,
            AcceptedStatus);
        return (null, take, null);
    }

    static void GuardTransport(IVeoMotionTransport transport)
    {
        if (transport is OfficialVeoAcceptanceClosedTransport)
            return;
        throw new InvalidOperationException(ProductionFailureReasons.StartForbidden);
    }

    static OfficialVeoVideoTakeAcceptanceResult Ok(AuthoritativeVideoTake take, Guid? bindingId, bool replayed) =>
        new(true, replayed, replayed ? "REPLAY" : OfficialVeoPollStates.Succeeded, null, take, bindingId,
            0, 0, 0, 0, 0, 0, 0, Array.Empty<string>());

    static Task EventAsync(
        OfficialVeoVideoTakeAcceptanceRequest request,
        ProductionTask task,
        ProductionAttempt attempt,
        AuthoritativeVideoTake take,
        string type,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(new
        {
            take.BuildId,
            Shot = take.Shot,
            take.Action,
            take.TaskId,
            take.AttemptId,
            take.SnapshotId,
            take.SnapshotHash,
            Provider = take.ProviderId,
            Model = take.ModelId,
            take.SelectionId,
            OperationId = take.OperationId,
            take.ArtifactId,
            ArtifactSha256 = take.ByteSha256,
            ArtifactByteLength = take.ByteLength,
            take.VerificationStatus,
            take.AcceptanceStatus,
            AuthorizationHash = request.ExpectedAuthorizationHash,
        });
        return request.Store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(), type, DateTimeOffset.UtcNow, task.RequestId, task.TaskId, attempt.AttemptId,
            take.ArtifactId, task.Scope.ShotCode, task.Action, json), ct);
    }

    static string? DecodeOperation(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return null;
        try { return VeoVertexOperationId.Decode(encoded); }
        catch { return null; }
    }
}

/// <summary>CK20 closed boundary. Any Start/Poll fails deterministically.</summary>
public sealed class OfficialVeoAcceptanceClosedTransport : IVeoMotionTransport
{
    public bool RequiresNetwork => false;
    public int HttpCallCount => 0;
    public int StartCount => 0;
    public int PollCount => 0;

    public Task<VeoMotionTransportCreateResult> CreateImageToVideoAsync(
        VeoMotionTransportCreateRequest request, CancellationToken ct = default) =>
        throw new InvalidOperationException(ProductionFailureReasons.StartForbidden);

    public Task<VeoMotionTransportPollResult> GetOperationAsync(string providerJobId, CancellationToken ct = default) =>
        throw new InvalidOperationException(ProductionFailureReasons.PollForbidden);

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default) =>
        throw new InvalidOperationException(ProductionFailureReasons.PollForbidden);
}

public sealed class OfficialVeoAcceptanceClosedPermitStore : IProductionExecutionPermitStore
{
    public int IssueCalls { get; private set; }
    public int ConsumeCalls { get; private set; }

    public Task<ProductionExecutionPermit> IssueAsync(IssueProductionExecutionPermitCommand command, CancellationToken ct = default)
    {
        IssueCalls++;
        throw new InvalidOperationException(ProductionFailureReasons.PermitMutationForbidden);
    }

    public Task<bool> TryConsumeAsync(Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
    {
        ConsumeCalls++;
        throw new InvalidOperationException(ProductionFailureReasons.PermitMutationForbidden);
    }

    public Task<ProductionExecutionPermit?> GetIssuedAsync(Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default) =>
        Task.FromResult<ProductionExecutionPermit?>(null);

    public Task<ProductionExecutionPermit?> GetLatestAsync(Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default) =>
        Task.FromResult<ProductionExecutionPermit?>(null);

    public Task<IReadOnlyList<ProductionExecutionPermit>> ListByTargetAsync(Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProductionExecutionPermit>>(Array.Empty<ProductionExecutionPermit>());
}
