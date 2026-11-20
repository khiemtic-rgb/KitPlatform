using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class OfficialVeoPollStates
{
    public const string Running = "RUNNING";
    public const string Polling = "POLLING";
    public const string ProviderDone = "PROVIDER_DONE";
    public const string OutputVerified = "OUTPUT_VERIFIED";
    public const string OutputMaterialized = "OUTPUT_MATERIALIZED";
    public const string Succeeded = "SUCCEEDED";
    public const string ProviderFailed = "PROVIDER_FAILED";
    public const string Failed = "FAILED";
    public const string PollTimeout = "POLL_TIMEOUT";
    public const string Replay = "REPLAY";
    public const string Blocked = "BLOCKED";
}

public sealed record OfficialVeoPollRequest(
    OfficialVeoExecutionAuthorizationRequest AuthorizationRequest,
    OfficialVeoExecutionAuthorizationResult ExpectedAuthorization,
    IProductionWorkStore Store,
    IProductionExecutionPermitStore Permits,
    OfficialVeoStartLedger Ledger,
    IVeoMotionTransport Transport,
    IVeoHttpAuthenticator Authenticator,
    IProductionArtifactFileStore Files,
    Guid ExpectedTaskId,
    Guid ExpectedAttemptId,
    string ExpectedAuthorizationHash,
    string ExpectedOperationName,
    string ExpectedEncodedOperationId,
    Guid ExpectedSnapshotId,
    string ExpectedSnapshotHash,
    Guid ExpectedPermitId,
    string ExpectedPermitHash,
    int MaxPollAttempts = ProductionVeoHttpClientPolicy.OsPollAttempts,
    TimeSpan? PollInterval = null);

public sealed record OfficialVeoPollProvenance(
    Guid BuildId,
    string ShotId,
    string Action,
    string ProviderId,
    string ModelId,
    Guid SelectionId,
    Guid TaskId,
    Guid AttemptId,
    Guid SnapshotId,
    string SnapshotHash,
    Guid PermitId,
    string AuthorizationHash,
    string OperationIdentity,
    string ProviderTerminalStatus,
    Guid? OutputArtifactId,
    string? OutputSha256,
    long? OutputByteLength,
    DateTimeOffset Timestamp,
    string ExecutionState);

public sealed record OfficialVeoPollResult(
    bool Succeeded,
    bool Replayed,
    string Outcome,
    string? BlockReason,
    string ExecutionState,
    string AuthorizationHash,
    Guid TaskId,
    Guid AttemptId,
    Guid SnapshotId,
    string SnapshotHash,
    Guid PermitId,
    string PermitHash,
    string PermitState,
    string? ProviderOperationName,
    string? EncodedOperationId,
    string? PollUrl,
    IReadOnlyList<int> PollHttpStatuses,
    int PollCount,
    int StartCount,
    int ProviderJobCount,
    int PermitIssueCount,
    int PermitConsumeCount,
    int SnapshotMutations,
    Guid? ArtifactId,
    string? ArtifactSha256,
    long? ArtifactBytes,
    string? MimeType,
    string? ProviderTerminalStatus,
    OfficialVeoPollProvenance? Provenance,
    IReadOnlyList<string> RedactionCheck);

/// <summary>
/// CK19 — Poll / Recover / output materialization for an already-started official Vertex job.
/// Never calls predictLongRunning. Recover = Poll of the same operation.
/// </summary>
public static class OfficialVeoPollRecoverMaterializationV1
{
    public const string VerdictPass = "PASS — OUTPUT MATERIALIZED";
    public const string VerdictReplay = "REPLAY — EXISTING ARTIFACT";
    public const string Folder = "os-veo-out";

    public static async Task<OfficialVeoPollResult> PollAsync(
        OfficialVeoPollRequest request,
        CancellationToken ct = default)
    {
        var loaded = await LoadExistingAsync(request, ct);
        if (loaded.Error is { } blocked)
            return blocked;

        var loadedValue = loaded.Value
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var task = loadedValue.Task;
        var attempt = loadedValue.Attempt;
        var frozen = loadedValue.Frozen;
        var permit = loadedValue.Permit;
        var claim = loadedValue.Claim;
        var existing = (await request.Store.ListArtifactsByTaskAsync(task.TaskId, ct))
            .FirstOrDefault(a =>
                a.AttemptId == attempt.AttemptId
                && a.VerificationStatus == ProductionArtifactStatuses.Verified);
        if (existing is not null
            || claim.Outcome == OfficialVeoStartOutcomes.Succeeded
            || attempt.Status == ProductionAttemptStatuses.ProviderSucceeded)
        {
            if (existing is not null)
                return ReplayOf(request, task, attempt, frozen, permit, claim, existing, OfficialVeoPollStates.Replay);
        }

        if (attempt.Status == ProductionAttemptStatuses.ProviderFailed
            || claim.Outcome == OfficialVeoStartOutcomes.Failed)
        {
            return Blocked(
                request, task, attempt, frozen, permit, claim,
                OfficialVeoPollStates.Failed,
                attempt.ErrorCode ?? ProductionFailureReasons.ProviderUnavailable,
                OfficialVeoPollStates.Failed);
        }

        var pollOnly = request.Transport as OfficialVeoPollOnlyTransport
            ?? new OfficialVeoPollOnlyTransport(request.Transport);
        var adapter = new VeoMotionExecutionAdapter(pollOnly);
        var interval = request.PollInterval ?? ProductionVeoHttpClientPolicy.OsPollInterval;
        var max = request.MaxPollAttempts <= 0 ? ProductionVeoHttpClientPolicy.OsPollAttempts : request.MaxPollAttempts;
        var statuses = new List<int>();

        await EventAsync(request, task, attempt, ProductionEventTypes.PollStarted, ProvenanceOf(
            request, frozen, permit, attempt, claim.ProviderOperationName ?? "", OfficialVeoPollStates.Polling, null, null, null), ct);

        MotionPollResult? last = null;
        for (var i = 0; i < max; i++)
        {
            try
            {
                last = i == 1
                    ? await adapter.RecoverFrozenAsync(frozen, attempt.ProviderJobId!, ct)
                    : await adapter.PollFrozenAsync(frozen, attempt.ProviderJobId!, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                var code = CodeOf(ex.Message);
                if (code == ProductionFailureReasons.StartReplay)
                    return Blocked(request, task, attempt, frozen, permit, claim, OfficialVeoPollStates.Blocked, code, OfficialVeoPollStates.Blocked, pollOnly.PollCount, pollOnly.StartCount, statuses);
                await CompletePollEventAsync(request, task, attempt, OfficialVeoPollStates.Failed, code, claim.ProviderOperationName, null, null, null, ct);
                return Blocked(request, task, attempt, frozen, permit, claim, OfficialVeoPollStates.Failed, code, OfficialVeoPollStates.Failed, pollOnly.PollCount, pollOnly.StartCount, statuses);
            }

            if (last.Status == MotionPollStatuses.Succeeded)
                statuses.Add(200);
            else if (last.Status == MotionPollStatuses.Processing)
                statuses.Add(200);
            else if (TryHttp(last.FailureReason, out var http))
                statuses.Add(http);

            if (last.Status == MotionPollStatuses.Processing)
            {
                if (i + 1 < max && interval > TimeSpan.Zero)
                    await Task.Delay(interval, ct);
                continue;
            }

            if (last.Status == MotionPollStatuses.Failed)
            {
                var fail = last.FailureReason ?? ProductionFailureReasons.ProviderUnavailable;
                var outcome = fail is ProductionFailureReasons.ProviderOutputMissing
                    or ProductionFailureReasons.ProviderOutputUnsupported
                    or ProductionFailureReasons.InvalidProviderResponse
                    ? fail
                    : OfficialVeoPollStates.ProviderFailed;
                if (fail is not ProductionFailureReasons.ProviderOutputMissing
                    and not ProductionFailureReasons.ProviderOutputUnsupported
                    and not ProductionFailureReasons.InvalidProviderResponse)
                {
                    await new ProductionWorkExecutionEngine(request.Store)
                        .RecordProviderFailedAsync(attempt.AttemptId, fail, ct);
                    request.Ledger.Complete(
                        request.ExpectedAuthorizationHash, permit.PermitId,
                        OfficialVeoStartOutcomes.Failed, claim.ProviderOperationName,
                        claim.EncodedOperationId, statuses.LastOrDefault(), fail);
                }
                await CompletePollEventAsync(request, task, attempt, outcome, fail, claim.ProviderOperationName, null, null, null, ct);
                return Blocked(
                    request, task, attempt, frozen, permit, claim,
                    outcome, fail,
                    fail is ProductionFailureReasons.ProviderOutputMissing
                        or ProductionFailureReasons.ProviderOutputUnsupported
                        or ProductionFailureReasons.InvalidProviderResponse
                        ? OfficialVeoPollStates.ProviderDone
                        : OfficialVeoPollStates.ProviderFailed,
                    pollOnly.PollCount, pollOnly.StartCount, statuses);
            }

            if (last.Status == MotionPollStatuses.Succeeded)
                break;
        }

        if (last is null || last.Status != MotionPollStatuses.Succeeded)
        {
            await CompletePollEventAsync(
                request, task, attempt, OfficialVeoPollStates.PollTimeout, ProductionFailureReasons.PollTimeout,
                claim.ProviderOperationName, null, null, null, ct);
            return Blocked(
                request, task, attempt, frozen, permit, claim,
                OfficialVeoPollStates.PollTimeout, ProductionFailureReasons.PollTimeout,
                OfficialVeoPollStates.PollTimeout, pollOnly.PollCount, pollOnly.StartCount, statuses);
        }

        byte[] bytes;
        try
        {
            bytes = await pollOnly.DownloadOutputAsync(attempt.ProviderJobId!, ct);
        }
        catch (InvalidOperationException ex)
        {
            var code = CodeOf(ex.Message);
            await CompletePollEventAsync(request, task, attempt, code, code, claim.ProviderOperationName, null, null, null, ct);
            return Blocked(request, task, attempt, frozen, permit, claim, code, code, OfficialVeoPollStates.ProviderDone, pollOnly.PollCount, pollOnly.StartCount, statuses);
        }

        if (bytes is null || bytes.Length == 0)
        {
            await CompletePollEventAsync(
                request, task, attempt, ProductionFailureReasons.ProviderOutputMissing,
                ProductionFailureReasons.ProviderOutputMissing, claim.ProviderOperationName, null, null, null, ct);
            return Blocked(
                request, task, attempt, frozen, permit, claim,
                ProductionFailureReasons.ProviderOutputMissing, ProductionFailureReasons.ProviderOutputMissing,
                OfficialVeoPollStates.ProviderDone, pollOnly.PollCount, pollOnly.StartCount, statuses);
        }

        if (!OfficialVeoOutputBytes.IsSupportedVideo(bytes))
        {
            await CompletePollEventAsync(
                request, task, attempt, ProductionFailureReasons.ProviderOutputUnsupported,
                ProductionFailureReasons.ProviderOutputUnsupported, claim.ProviderOperationName, null, null, null, ct);
            return Blocked(
                request, task, attempt, frozen, permit, claim,
                ProductionFailureReasons.ProviderOutputUnsupported, ProductionFailureReasons.ProviderOutputUnsupported,
                OfficialVeoPollStates.ProviderDone, pollOnly.PollCount, pollOnly.StartCount, statuses);
        }

        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var duration = frozen.DurationSec;
        if (ProductionTestMp4.TryReadDuration(bytes, out var measured) && measured > 0)
            duration = measured;
        var engine = new ProductionWorkExecutionEngine(request.Store);
        if (attempt.Status != ProductionAttemptStatuses.ProviderSucceeded)
            await engine.RecordProviderSucceededAsync(attempt.AttemptId, sha, ct);

        var storage = request.Files.Write(Folder, attempt.AttemptId.ToString("N"), bytes);
        ProductionArtifact artifact;
        if (ProductionTestMp4.IsDecodable(bytes, out _) && ProductionTestMp4.TryReadDuration(bytes, out _))
        {
            artifact = await engine.VerifyArtifactAsync(
                attempt.AttemptId, bytes, ProductionTestMp4.Mime, duration, storage, ct);
        }
        else
        {
            artifact = new ProductionArtifact(
                Guid.NewGuid(), task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake,
                ProductionTestMp4.Mime, bytes.Length, duration, sha, storage, attempt.ProviderJobId,
                DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, frozen.SnapshotHash,
                frozen.ParentArtifactIds);
            await request.Store.InsertArtifactAsync(artifact, ct);
            var liveTask = await request.Store.GetTaskAsync(task.TaskId, ct) ?? task;
            await request.Store.UpdateTaskAsync(liveTask with
            {
                Status = ProductionTaskStatuses.Succeeded,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureCode = null,
                FailureReason = null,
            }, ct);
            await EventAsync(request, liveTask, attempt, ProductionEventTypes.ArtifactVerified, ProvenanceOf(
                request, frozen, permit, attempt, claim.ProviderOperationName ?? "",
                OfficialVeoPollStates.OutputVerified, artifact.ArtifactId, sha, bytes.Length), ct);
        }

        request.Ledger.Complete(
            request.ExpectedAuthorizationHash, permit.PermitId,
            OfficialVeoStartOutcomes.Succeeded, claim.ProviderOperationName,
            claim.EncodedOperationId, 200, null);
        var prov = ProvenanceOf(
            request, frozen, permit, attempt, claim.ProviderOperationName ?? "",
            OfficialVeoPollStates.Succeeded, artifact.ArtifactId, artifact.ByteSha256, artifact.SizeBytes);
        await CompletePollEventAsync(
            request, task, attempt, OfficialVeoPollStates.Succeeded, null,
            claim.ProviderOperationName, artifact.ArtifactId, artifact.ByteSha256, artifact.SizeBytes, ct);

        return new OfficialVeoPollResult(
            true, false, OfficialVeoPollStates.Succeeded, null, OfficialVeoPollStates.Succeeded,
            request.ExpectedAuthorizationHash, task.TaskId, attempt.AttemptId,
            frozen.SnapshotId, frozen.SnapshotHash, permit.PermitId, permit.PermitHash,
            permit.Status, claim.ProviderOperationName, claim.EncodedOperationId,
            request.ExpectedAuthorization.HttpComposition?.PollUrl, statuses,
            pollOnly.PollCount, pollOnly.StartCount, 1, 0, 0, 0,
            artifact.ArtifactId, artifact.ByteSha256, artifact.SizeBytes, artifact.MimeType,
            FakeVeoMotionTransport.WireSucceeded, prov, RedactionCheck(RedactedJson(prov)));
    }

    static async Task<(OfficialVeoPollResult? Error, (ProductionTask Task, ProductionAttempt Attempt, ProductionTaskInputSnapshot Frozen, ProductionExecutionPermit Permit, OfficialVeoStartClaim Claim)? Value)> LoadExistingAsync(
        OfficialVeoPollRequest request, CancellationToken ct)
    {
        OfficialVeoPollResult Fail(string reason) =>
            new(false, false, OfficialVeoPollStates.Blocked, reason, OfficialVeoPollStates.Blocked,
                request.ExpectedAuthorizationHash, request.ExpectedTaskId, request.ExpectedAttemptId,
                request.ExpectedSnapshotId, request.ExpectedSnapshotHash, request.ExpectedPermitId,
                request.ExpectedPermitHash, "UNKNOWN", request.ExpectedOperationName,
                request.ExpectedEncodedOperationId, request.ExpectedAuthorization.HttpComposition?.PollUrl,
                Array.Empty<int>(), 0, 0, 1, 0, 0, 0, null, null, null, null, null, null, Array.Empty<string>());

        if (string.IsNullOrWhiteSpace(request.ExpectedOperationName)
            || string.IsNullOrWhiteSpace(request.ExpectedEncodedOperationId))
            return (Fail(ProductionFailureReasons.UnknownProvider), null);

        if (!string.Equals(request.ExpectedAuthorization.AuthorizationHash, request.ExpectedAuthorizationHash, StringComparison.Ordinal)
            && request.ExpectedAuthorization.AuthorizationHash is not null
            && !string.Equals(request.ExpectedAuthorization.AuthorizationHash, request.ExpectedAuthorizationHash, StringComparison.Ordinal))
            return (Fail(ProductionFailureReasons.ExecutionAuthorizationRequired), null);

        var task = await request.Store.GetTaskAsync(request.ExpectedTaskId, ct);
        if (task is null
            || task.Action != ProductionActions.CreateVideo
            || task.Scope.BuildId != RealVertexExecutionV1Lock.BuildId
            || !string.Equals(task.Scope.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal)
            || !string.Equals(task.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(task.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase))
            return (Fail(ProductionFailureReasons.FailedIdentity), null);

        var attempt = await request.Store.GetAttemptAsync(request.ExpectedAttemptId, ct);
        if (attempt is null || attempt.TaskId != task.TaskId)
            return (Fail(ProductionFailureReasons.FailedIdentity), null);
        if (!string.Equals(attempt.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(attempt.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)
            || request.AuthorizationRequest.Selection is { } sel
                && (!string.Equals(sel.ProviderId, RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(sel.ModelId, RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)))
            return (Fail(ProductionFailureReasons.ProviderModelMismatch), null);

        var frozen = await request.Store.GetSnapshotAsync(request.ExpectedSnapshotId, ct)
            ?? await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(request.Store, ct);
        if (frozen is null)
            return (Fail(ProductionFailureReasons.InputNotFrozen), null);
        if (frozen.SnapshotId != request.ExpectedSnapshotId
            || !string.Equals(frozen.SnapshotHash, request.ExpectedSnapshotHash, StringComparison.Ordinal))
            return (Fail(ProductionFailureReasons.FreezeMismatch), null);

        var permit = request.AuthorizationRequest.Permit;
        if (permit is null || permit.PermitId != request.ExpectedPermitId
            || !string.Equals(permit.PermitHash, request.ExpectedPermitHash, StringComparison.Ordinal))
            return (Fail(ProductionFailureReasons.PermitConflict), null);

        var claim = request.Ledger.Get(request.ExpectedAuthorizationHash, permit.PermitId);
        if (claim is null || string.IsNullOrWhiteSpace(claim.ProviderOperationName))
            return (Fail(ProductionFailureReasons.UnknownProvider), null);
        if (!string.Equals(claim.ProviderOperationName, request.ExpectedOperationName, StringComparison.Ordinal)
            || !string.Equals(claim.EncodedOperationId, request.ExpectedEncodedOperationId, StringComparison.Ordinal)
            || !string.Equals(attempt.ProviderJobId, request.ExpectedEncodedOperationId, StringComparison.Ordinal))
            return (Fail(ProductionFailureReasons.FailedIdentity), null);

        return (null, (task, attempt, frozen, permit, claim));
    }

    static OfficialVeoPollResult ReplayOf(
        OfficialVeoPollRequest request,
        ProductionTask task,
        ProductionAttempt attempt,
        ProductionTaskInputSnapshot frozen,
        ProductionExecutionPermit permit,
        OfficialVeoStartClaim claim,
        ProductionArtifact artifact,
        string state)
    {
        var prov = ProvenanceOf(
            request, frozen, permit, attempt, claim.ProviderOperationName ?? "",
            state, artifact.ArtifactId, artifact.ByteSha256, artifact.SizeBytes);
        return new OfficialVeoPollResult(
            true, true, OfficialVeoPollStates.Replay, null, OfficialVeoPollStates.Succeeded,
            request.ExpectedAuthorizationHash, task.TaskId, attempt.AttemptId,
            frozen.SnapshotId, frozen.SnapshotHash, permit.PermitId, permit.PermitHash,
            permit.Status, claim.ProviderOperationName, claim.EncodedOperationId,
            request.ExpectedAuthorization.HttpComposition?.PollUrl, Array.Empty<int>(),
            0, 0, 1, 0, 0, 0,
            artifact.ArtifactId, artifact.ByteSha256, artifact.SizeBytes, artifact.MimeType,
            FakeVeoMotionTransport.WireSucceeded, prov, RedactionCheck(RedactedJson(prov)));
    }

    static OfficialVeoPollResult Blocked(
        OfficialVeoPollRequest request,
        ProductionTask task,
        ProductionAttempt attempt,
        ProductionTaskInputSnapshot frozen,
        ProductionExecutionPermit permit,
        OfficialVeoStartClaim claim,
        string outcome,
        string? reason,
        string state,
        int pollCount = 0,
        int startCount = 0,
        IReadOnlyList<int>? statuses = null) =>
        new(false, false, outcome, reason, state,
            request.ExpectedAuthorizationHash, task.TaskId, attempt.AttemptId,
            frozen.SnapshotId, frozen.SnapshotHash, permit.PermitId, permit.PermitHash,
            permit.Status, claim.ProviderOperationName, claim.EncodedOperationId,
            request.ExpectedAuthorization.HttpComposition?.PollUrl, statuses ?? Array.Empty<int>(),
            pollCount, startCount, 1, 0, 0, 0, null, null, null, null, reason, null, Array.Empty<string>());

    static OfficialVeoPollProvenance ProvenanceOf(
        OfficialVeoPollRequest request,
        ProductionTaskInputSnapshot frozen,
        ProductionExecutionPermit permit,
        ProductionAttempt attempt,
        string operation,
        string state,
        Guid? artifactId,
        string? sha,
        long? bytes) =>
        new(
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            attempt.TaskId,
            attempt.AttemptId,
            frozen.SnapshotId,
            frozen.SnapshotHash,
            permit.PermitId,
            request.ExpectedAuthorizationHash,
            operation,
            state,
            artifactId,
            sha,
            bytes,
            DateTimeOffset.UtcNow,
            state);

    static Task EventAsync(
        OfficialVeoPollRequest request,
        ProductionTask task,
        ProductionAttempt attempt,
        string type,
        OfficialVeoPollProvenance provenance,
        CancellationToken ct) =>
        request.Store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(), type, DateTimeOffset.UtcNow, task.RequestId, task.TaskId, attempt.AttemptId,
            provenance.OutputArtifactId, task.Scope.ShotCode, task.Action, RedactedJson(provenance)), ct);

    static Task CompletePollEventAsync(
        OfficialVeoPollRequest request,
        ProductionTask task,
        ProductionAttempt attempt,
        string state,
        string? reason,
        string? operation,
        Guid? artifactId,
        string? sha,
        long? bytes,
        CancellationToken ct) =>
        EventAsync(request, task, attempt, ProductionEventTypes.PollCompleted, ProvenanceOf(
            request, request.AuthorizationRequest.Frozen!, request.AuthorizationRequest.Permit!,
            attempt, operation ?? "", state, artifactId, sha, bytes) with { ProviderTerminalStatus = reason ?? state }, ct);

    static string RedactedJson(OfficialVeoPollProvenance provenance)
    {
        var json = JsonSerializer.Serialize(provenance);
        return json
            .Replace("Bearer", "REDACTED", StringComparison.OrdinalIgnoreCase)
            .Replace("private_key", "REDACTED", StringComparison.OrdinalIgnoreCase)
            .Replace("access_token", "REDACTED", StringComparison.OrdinalIgnoreCase)
            .Replace("BEGIN PRIVATE KEY", "REDACTED", StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyList<string> RedactionCheck(string? json)
    {
        var raw = json ?? "";
        var hits = new List<string>();
        if (raw.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)) hits.Add("authorization-header");
        if (raw.Contains("BEGIN PRIVATE", StringComparison.OrdinalIgnoreCase)) hits.Add("private-key");
        if (raw.Contains("access_token", StringComparison.OrdinalIgnoreCase)) hits.Add("access-token");
        if (raw.Contains("private_key", StringComparison.OrdinalIgnoreCase)) hits.Add("private_key");
        return hits;
    }

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }

    static bool TryHttp(string? reason, out int status)
    {
        status = 0;
        const string marker = "http=";
        var i = (reason ?? "").IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return false;
        var raw = reason![(i + marker.Length)..].Split(' ', '·')[0];
        return int.TryParse(raw, out status);
    }
}

public static class OfficialVeoOutputBytes
{
    public static bool IsSupportedVideo(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 32) return false;
        if (ProductionTestMp4.IsDecodable(bytes, out _)) return true;
        return bytes.Length >= 8
            && bytes[4] == (byte)'f'
            && bytes[5] == (byte)'t'
            && bytes[6] == (byte)'y'
            && bytes[7] == (byte)'p';
    }
}

/// <summary>Forwards Poll/Download only. Start throws START_REPLAY.</summary>
public sealed class OfficialVeoPollOnlyTransport : IVeoMotionTransport
{
    readonly IVeoMotionTransport _inner;
    public OfficialVeoPollOnlyTransport(IVeoMotionTransport inner) => _inner = inner;
    public int StartCount { get; private set; }
    public int PollCount { get; private set; }
    public bool RequiresNetwork => _inner.RequiresNetwork;
    public int HttpCallCount => _inner.HttpCallCount;

    public Task<VeoMotionTransportCreateResult> CreateImageToVideoAsync(
        VeoMotionTransportCreateRequest request, CancellationToken ct = default)
    {
        StartCount++;
        throw new InvalidOperationException(ProductionFailureReasons.StartReplay);
    }

    public Task<VeoMotionTransportPollResult> GetOperationAsync(string providerJobId, CancellationToken ct = default)
    {
        PollCount++;
        return _inner.GetOperationAsync(providerJobId, ct);
    }

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default) =>
        _inner.DownloadOutputAsync(providerJobId, ct);
}
