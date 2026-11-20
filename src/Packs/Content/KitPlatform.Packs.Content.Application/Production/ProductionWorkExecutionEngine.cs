using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed class ProductionWorkExecutionEngine
{
    readonly IProductionWorkStore _store;
    readonly IPerformanceSnapshotStore? _performance;
    readonly IProductionCredentialResolver? _credentials;
    readonly bool _official;

    /// <summary>
    /// Test fixture engine. Official Production execution uses <see cref="CreateOfficial"/>.
    /// </summary>
    public ProductionWorkExecutionEngine(
        IProductionWorkStore store,
        IPerformanceSnapshotStore? performance = null,
        IProductionCredentialResolver? credentials = null,
        bool official = false)
    {
        _store = store;
        _performance = performance;
        _credentials = credentials;
        _official = official;
    }

    public static ProductionWorkExecutionEngine CreateOfficial(
        IProductionWorkStore store,
        IProductionCredentialResolver? credentials,
        IPerformanceSnapshotStore? performance = null)
        => new(store, performance, credentials, official: true);

    public async Task<ProductionRequest> CreateRequestAsync(
        ProductionScope scope,
        string userAction,
        string createdBy,
        string? idempotencyKey = null,
        Guid? parentRequestId = null,
        CancellationToken ct = default)
    {
        if (!ProductionActions.IsKnown(userAction))
            throw new InvalidOperationException($"Unknown action: {userAction}");
        var contract = ProductionActionContractRegistry.Require(userAction);
        var key = string.IsNullOrWhiteSpace(idempotencyKey)
            ? $"{scope.BuildId:N}:{scope.ShotCode}:{userAction}:{Guid.NewGuid():N}"
            : idempotencyKey.Trim();
        var existing = await _store.GetRequestByIdempotencyAsync(key, ct);
        if (existing is not null) return existing;

        var request = new ProductionRequest(
            Guid.NewGuid(), scope, userAction, contract.ExpectedOutputType,
            DateTimeOffset.UtcNow, createdBy, ProductionRequestStatuses.Submitted,
            parentRequestId, key, contract.ForbiddenFallbacks);
        await _store.InsertRequestAsync(request, ct);
        await EventAsync(ProductionEventTypes.RequestCreated, request.RequestId, null, null, null, scope.ShotCode, userAction, ct);
        return request;
    }

    public async Task<ProductionTask> CreateTaskAsync(Guid requestId, CancellationToken ct = default)
    {
        var request = await RequireRequest(requestId, ct);
        var task = new ProductionTask(
            Guid.NewGuid(), request.RequestId, null, request.UserAction, "Shot", request.Scope.ShotCode,
            request.Scope, null, null, null, ProductionTaskStatuses.Created,
            DateTimeOffset.UtcNow, null, null, null, null);
        await _store.InsertTaskAsync(task, ct);
        await _store.UpdateRequestAsync(request with { Status = ProductionRequestStatuses.InProgress }, ct);
        await EventAsync(ProductionEventTypes.TaskCreated, request.RequestId, task.TaskId, null, null, request.Scope.ShotCode, request.UserAction, ct);
        return task;
    }

    public async Task<ProductionTask> CreateChildTaskAsync(Guid parentRequestId, string childAction, string createdBy, CancellationToken ct = default)
    {
        var parent = await RequireRequest(parentRequestId, ct);
        if (string.Equals(parent.UserAction, childAction, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden);
        var child = await CreateRequestAsync(parent.Scope, childAction, createdBy, parentRequestId: parent.RequestId, ct: ct);
        return await CreateTaskAsync(child.RequestId, ct);
    }

    public async Task<ProductionTaskInputSnapshot> FreezeInputAsync(
        Guid taskId,
        ShotExecutionInput input,
        string providerId,
        string modelId,
        CancellationToken ct = default,
        ProductionCredentialReference? credential = null)
    {
        var task = await RequireTask(taskId, ct);
        if (task.Action == ProductionActions.CreateVideo && string.IsNullOrWhiteSpace(input.Story))
            throw new InvalidOperationException(ProductionFailureReasons.StoryChanged);
        var bound = await BindRequiredPerformanceAsync(input, task.Scope, task.Action, ct);
        if (bound is not null)
            input = ProductionPerformanceConsumption.ApplyBound(input, bound);
        if (string.Equals(task.Action, ProductionActions.CreateVideo, StringComparison.Ordinal))
            input = ProductionVisualContinuity.Attach(input);
        var compiled = ShotExecutionCompiler.Compile(input);
        var pinned = await PinCredentialAsync(providerId, modelId, credential, task.Scope, ct);
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId,
            compiled.Story, compiled.ActingBeat, compiled.VisualContract, input.MotionRequirements,
            compiled.CompilerOutput, compiled.CompilerOutput,
            ShotExecutionCompiler.Hash(compiled.Story),
            ShotExecutionCompiler.Hash(compiled.ActingBeat),
            ShotExecutionCompiler.Hash(compiled.VisualContract),
            ShotExecutionCompiler.Hash(compiled.CompilerOutput),
            input.KeyframeHash, ShotExecutionCompiler.Hash(input.VoiceRequirements),
            ShotExecutionCompiler.Hash(input.MotionRequirements), "",
            ShotExecutionCompiler.Hash(input.Timing),
            providerId, modelId, "", DateTimeOffset.UtcNow,
            input.KeyframeArtifactId, input.AspectRatio, input.DurationSec, input.CompilerVersion,
            input.ParentArtifactIds,
            bound?.PerformanceSnapshotId,
            bound?.PerformanceHash,
            bound?.PerformancePayloadJson,
            pinned?.CredentialId,
            pinned?.CredentialVersionId);
        snap = snap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(snap) };
        await _store.InsertSnapshotAsync(snap, ct);
        await _store.UpdateTaskAsync(task with
        {
            InputSnapshotId = snap.SnapshotId,
            ProviderId = providerId,
            ModelId = modelId,
            Status = ProductionTaskStatuses.ProviderFrozen,
        }, ct);
        await EventAsync(ProductionEventTypes.InputFrozen, task.RequestId, task.TaskId, null, null, task.Scope.ShotCode, task.Action, ct);
        return snap;
    }

    public async Task<ProductionAttempt> StartAttemptAsync(Guid taskId, string requestPayloadHash, CancellationToken ct = default)
    {
        var task = await RequireTask(taskId, ct);
        if (string.IsNullOrWhiteSpace(task.ProviderId) || string.IsNullOrWhiteSpace(task.ModelId))
            throw new InvalidOperationException("Provider/model must be frozen before an Attempt.");
        if (ProductionFrozenCredential.RequiresPin(task.ProviderId) && (_official || _credentials is not null))
        {
            if (_credentials is null)
                throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
            var frozen = await _store.GetSnapshotByTaskAsync(taskId, ct)
                ?? throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
            var reference = ProductionFrozenCredential.FromSnapshot(frozen);
            if (reference is null)
                throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
            var material = await _credentials.ResolveAsync(
                reference.CredentialId, reference.CredentialVersionId, task.ProviderId, task.ModelId, ct);
            if (material.CredentialId != reference.CredentialId
                || material.CredentialVersionId != reference.CredentialVersionId)
                throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        }
        var prior = await _store.ListAttemptsAsync(taskId, ct);
        var attempt = new ProductionAttempt(
            Guid.NewGuid(), task.TaskId, prior.Count + 1, task.ProviderId, task.ModelId, null,
            ProductionAttemptStatuses.Started, DateTimeOffset.UtcNow, null, requestPayloadHash, null, null);
        await _store.InsertAttemptAsync(attempt, ct);
        await _store.UpdateTaskAsync(task with { Status = ProductionTaskStatuses.Running, StartedAt = DateTimeOffset.UtcNow }, ct);
        await EventAsync(ProductionEventTypes.AttemptStarted, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
        return attempt;
    }

    public async Task<ProductionAttempt> BeginOfficialProviderStartAsync(
        Guid taskId, string requestPayloadHash, Guid? reuseAttemptId = null, CancellationToken ct = default)
    {
        return await _store.InTransactionAsync(async inner =>
        {
            await _store.LockTaskAsync(taskId, inner);
            var attempts = await _store.ListAttemptsAsync(taskId, inner);
            if (reuseAttemptId is Guid reuseId)
            {
                var live = attempts.FirstOrDefault(a => a.AttemptId == reuseId)
                    ?? throw new InvalidOperationException("ATTEMPT_MISSING");
                if (!string.IsNullOrWhiteSpace(live.ProviderJobId))
                    return live;
                if (live.Status != ProductionAttemptStatuses.Started)
                    throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
                var claimed = live with { Status = ProductionAttemptStatuses.ProviderSubmitted };
                await _store.UpdateAttemptAsync(claimed, inner);
                return claimed;
            }

            if (attempts.Any(a => a.Status is ProductionAttemptStatuses.Started or ProductionAttemptStatuses.ProviderSubmitted))
                throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
            var task = await RequireTask(taskId, inner);
            if (task.Status is ProductionTaskStatuses.Running or ProductionTaskStatuses.Succeeded or ProductionTaskStatuses.Superseded)
                throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
            return await StartAttemptAsync(taskId, requestPayloadHash, inner);
        }, ct);
    }

    public async Task<ProductionAttempt> SubmitProviderJobAsync(Guid attemptId, string providerJobId, CancellationToken ct = default)
    {
        var attempt = await RequireAttempt(attemptId, ct);
        if (!ProductionStateMachines.CanTransitionAttempt(attempt.Status, ProductionAttemptStatuses.ProviderSubmitted))
            throw new InvalidOperationException($"Illegal attempt transition {attempt.Status}");
        var next = attempt with { ProviderJobId = providerJobId, Status = ProductionAttemptStatuses.ProviderSubmitted };
        await _store.UpdateAttemptAsync(next, ct);
        var task = await RequireTask(attempt.TaskId, ct);
        await EventAsync(ProductionEventTypes.ProviderSubmitted, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
        return next;
    }

    public async Task<ProductionAttempt> RecordProviderSucceededAsync(Guid attemptId, string responsePayloadHash, CancellationToken ct = default)
    {
        var attempt = await RequireAttempt(attemptId, ct);
        var next = attempt with
        {
            Status = ProductionAttemptStatuses.ProviderSucceeded,
            CompletedAt = DateTimeOffset.UtcNow,
            ResponsePayloadHash = responsePayloadHash,
        };
        await _store.UpdateAttemptAsync(next, ct);
        var task = await RequireTask(attempt.TaskId, ct);
        await EventAsync(ProductionEventTypes.ProviderSucceeded, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
        return next;
    }

    public async Task<ProductionAttempt> RecordProviderFailedAsync(Guid attemptId, string errorCode, CancellationToken ct = default)
    {
        var attempt = await RequireAttempt(attemptId, ct);
        if (attempt.Status != ProductionAttemptStatuses.ProviderFailed
            && !ProductionStateMachines.CanTransitionAttempt(attempt.Status, ProductionAttemptStatuses.ProviderFailed))
            throw new InvalidOperationException($"Illegal attempt transition {attempt.Status}");
        var full = errorCode ?? "";
        var next = attempt with
        {
            Status = ProductionAttemptStatuses.ProviderFailed,
            CompletedAt = DateTimeOffset.UtcNow,
            ErrorCode = BoundAttemptErrorCode(full),
        };
        await _store.UpdateAttemptAsync(next, ct);
        var task = await RequireTask(attempt.TaskId, ct);
        await _store.UpdateTaskAsync(task with
        {
            Status = ProductionTaskStatuses.Failed,
            CompletedAt = DateTimeOffset.UtcNow,
            FailureCode = ProductionFailureReasons.ProviderUnavailable,
            FailureReason = full,
        }, ct);
        var request = await RequireRequest(task.RequestId, ct);
        await _store.UpdateRequestAsync(request with { Status = ProductionRequestStatuses.Failed }, ct);
        await EventAsync(ProductionEventTypes.ProviderFailed, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
        return next;
    }

    public async Task<ProductionAttempt> RecordExecutionProcessFailedAsync(Guid attemptId, string detail, CancellationToken ct = default)
    {
        var full = detail ?? "";
        var errorCode = BoundAttemptErrorCode(full);
        return await _store.InTransactionAsync(async inner =>
        {
            var attempt = await RequireAttempt(attemptId, inner);
            if (attempt.Status != ProductionAttemptStatuses.ProviderFailed
                && !ProductionStateMachines.CanTransitionAttempt(attempt.Status, ProductionAttemptStatuses.ProviderFailed))
                throw new InvalidOperationException($"Illegal attempt transition {attempt.Status}");
            var next = attempt with
            {
                Status = ProductionAttemptStatuses.ProviderFailed,
                CompletedAt = DateTimeOffset.UtcNow,
                ErrorCode = errorCode,
            };
            await _store.UpdateAttemptAsync(next, inner);
            var task = await RequireTask(attempt.TaskId, inner);
            await _store.UpdateTaskAsync(task with
            {
                Status = ProductionTaskStatuses.Failed,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureCode = ProductionFailureReasons.ExecutionProcessFailed,
                FailureReason = full,
            }, inner);
            var request = await RequireRequest(task.RequestId, inner);
            await _store.UpdateRequestAsync(request with { Status = ProductionRequestStatuses.Failed }, inner);
            await EventAsync(ProductionEventTypes.TaskFailed, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, inner);
            return next;
        }, ct);
    }

    public async Task FailClosedProviderUnavailableAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await RequireTask(taskId, ct);
        await _store.UpdateTaskAsync(task with
        {
            Status = ProductionTaskStatuses.Failed,
            CompletedAt = DateTimeOffset.UtcNow,
            FailureCode = ProductionFailureReasons.ProviderUnavailable,
            FailureReason = ProductionFailureReasons.HiddenFallbackForbidden,
        }, ct);
        await EventAsync(ProductionEventTypes.TaskFailed, task.RequestId, task.TaskId, null, null, task.Scope.ShotCode, task.Action, ct);
    }

    public Task<ProductionArtifact> VerifyArtifactAsync(Guid attemptId, byte[] bytes, string mime, decimal durationSec, string storageKey, CancellationToken ct = default)
        => VerifyArtifactCoreAsync(attemptId, bytes, mime, durationSec, storageKey, ct);

    async Task<ProductionArtifact> VerifyArtifactCoreAsync(Guid attemptId, byte[]? bytes, string mime, decimal durationSec, string storageKey, CancellationToken ct)
    {
        var attempt = await RequireAttempt(attemptId, ct);
        var task = await RequireTask(attempt.TaskId, ct);
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct)
            ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");

        return await _store.InTransactionAsync(async inner =>
        {
            if (bytes is null || bytes.Length < 32)
            {
                await _store.UpdateTaskAsync(task with
                {
                    Status = ProductionTaskStatuses.FailedIdentity,
                    CompletedAt = DateTimeOffset.UtcNow,
                    FailureCode = ProductionFailureReasons.FailedIdentity,
                    FailureReason = ProductionFailureReasons.FailedIdentity,
                }, inner);
                await EventAsync(ProductionEventTypes.ArtifactFailedVerification, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, inner);
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            }

            var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
            var contract = ProductionActionContractRegistry.Require(task.Action);
            var verifiedDuration = durationSec;
            var role = contract.OutputArtifactRole;
            if (role is ProductionArtifactRoles.VideoTake or ProductionArtifactRoles.Lipsync or ProductionArtifactRoles.Mix or ProductionArtifactRoles.Assembly)
            {
                var videoOk = string.Equals(mime, ProductionTestMp4.Mime, StringComparison.OrdinalIgnoreCase)
                    && ProductionTestMp4.IsDecodable(bytes, out _)
                    && ProductionTestMp4.TryReadDuration(bytes, out verifiedDuration)
                    && verifiedDuration > 0;
                if (!videoOk)
                {
                    var failed = new ProductionArtifact(
                        Guid.NewGuid(), task.TaskId, attempt.AttemptId, contract.OutputArtifactRole,
                        mime, bytes.Length, durationSec, sha, storageKey, attempt.ProviderJobId,
                        DateTimeOffset.UtcNow, ProductionArtifactStatuses.FailedVerification, snap.SnapshotHash,
                        snap.ParentArtifactIds);
                    await _store.InsertArtifactAsync(failed, inner);
                    await _store.UpdateTaskAsync(task with
                    {
                        Status = ProductionTaskStatuses.Failed,
                        CompletedAt = DateTimeOffset.UtcNow,
                        FailureCode = ProductionFailureReasons.FailedVerification,
                        FailureReason = ProductionFailureReasons.FailedVerification,
                    }, inner);
                    await EventAsync(ProductionEventTypes.ArtifactFailedVerification, task.RequestId, task.TaskId, attempt.AttemptId, failed.ArtifactId, task.Scope.ShotCode, task.Action, inner);
                    throw new InvalidOperationException(ProductionFailureReasons.FailedVerification);
                }
            }
            if (role == ProductionArtifactRoles.Voice)
            {
                var audioOk = ProductionVoicePhysical.TryVerify(bytes, mime, durationSec, out verifiedDuration);
                if (!audioOk)
                {
                    var failed = new ProductionArtifact(
                        Guid.NewGuid(), task.TaskId, attempt.AttemptId, role,
                        mime, bytes.Length, durationSec, sha, storageKey, attempt.ProviderJobId,
                        DateTimeOffset.UtcNow, ProductionArtifactStatuses.FailedVerification, snap.SnapshotHash,
                        snap.ParentArtifactIds);
                    await _store.InsertArtifactAsync(failed, inner);
                    await _store.UpdateTaskAsync(task with
                    {
                        Status = ProductionTaskStatuses.Failed,
                        CompletedAt = DateTimeOffset.UtcNow,
                        FailureCode = ProductionFailureReasons.FailedVerification,
                        FailureReason = ProductionFailureReasons.FailedVerification,
                    }, inner);
                    await EventAsync(ProductionEventTypes.ArtifactFailedVerification, task.RequestId, task.TaskId, attempt.AttemptId, failed.ArtifactId, task.Scope.ShotCode, task.Action, inner);
                    throw new InvalidOperationException(ProductionFailureReasons.FailedVerification);
                }
            }

            var artifact = new ProductionArtifact(
                Guid.NewGuid(), task.TaskId, attempt.AttemptId, contract.OutputArtifactRole,
                mime, bytes.Length, verifiedDuration, sha, storageKey, attempt.ProviderJobId,
                DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, snap.SnapshotHash,
                snap.ParentArtifactIds);
            await _store.InsertArtifactAsync(artifact, inner);
            await _store.UpdateTaskAsync(task with
            {
                Status = ProductionTaskStatuses.Succeeded,
                CompletedAt = DateTimeOffset.UtcNow,
                FailureCode = null,
                FailureReason = null,
            }, inner);
            var request = await RequireRequest(task.RequestId, inner);
            await _store.UpdateRequestAsync(request with { Status = ProductionRequestStatuses.Completed }, inner);
            await EventAsync(ProductionEventTypes.ArtifactVerified, task.RequestId, task.TaskId, attempt.AttemptId, artifact.ArtifactId, task.Scope.ShotCode, task.Action, inner);
            return artifact;
        }, ct);
    }

    public Task<ProductionBinding> BindAsync(Guid artifactId, string role, CancellationToken ct = default)
        => BindCoreAsync(artifactId, role, protectNewerLineage: false, ct);

    public Task<ProductionBinding> BindProtectedAsync(Guid artifactId, string role, CancellationToken ct = default)
        => BindCoreAsync(artifactId, role, protectNewerLineage: true, ct);

    async Task<ProductionBinding> BindCoreAsync(Guid artifactId, string role, bool protectNewerLineage, CancellationToken ct)
    {
        if (!ProductionBindingRoles.IsKnown(role))
            throw new InvalidOperationException($"Unknown binding role: {role}");
        var artifact = await _store.GetArtifactAsync(artifactId, ct)
            ?? throw new InvalidOperationException("ARTIFACT_MISSING");
        if (artifact.VerificationStatus != ProductionArtifactStatuses.Verified)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var task = await RequireTask(artifact.TaskId, ct);
        var attempt = await _store.GetAttemptAsync(artifact.AttemptId, ct);

        return await _store.InTransactionAsync(async inner =>
        {
            var existing = await _store.GetActiveBindingAsync(task.Scope.BuildId, task.Scope.ShotCode, role, inner);
            if (existing is not null && existing.ArtifactId == artifact.ArtifactId)
                return existing;

            if (protectNewerLineage)
                await RejectLateAuthorityAsync(task, attempt, existing, inner);

            if (existing is not null)
                await _store.UnbindAsync(existing.BindingId, DateTimeOffset.UtcNow, inner);
            var binding = new ProductionBinding(
                Guid.NewGuid(), task.Scope.BuildId, task.Scope.ShotCode, role,
                artifact.ArtifactId, task.TaskId, DateTimeOffset.UtcNow, null);
            await _store.InsertBindingAsync(binding, inner);
            var ev = role switch
            {
                ProductionBindingRoles.Current => ProductionEventTypes.CurrentBound,
                ProductionBindingRoles.Visible => ProductionEventTypes.VisibleBound,
                ProductionBindingRoles.Accepted => ProductionEventTypes.AcceptedBound,
                _ => ProductionEventTypes.CurrentBound,
            };
            await EventAsync(ev, task.RequestId, task.TaskId, artifact.AttemptId, artifact.ArtifactId, task.Scope.ShotCode, task.Action, inner);
            return binding;
        }, ct);
    }

    async Task RejectLateAuthorityAsync(
        ProductionTask task, ProductionAttempt? attempt, ProductionBinding? existing, CancellationToken ct)
    {
        if (task.Status == ProductionTaskStatuses.Superseded)
            throw new InvalidOperationException(ProductionFailureReasons.LateResultNotCurrent);
        if (attempt?.Status == ProductionAttemptStatuses.Late)
            throw new InvalidOperationException(ProductionFailureReasons.LateResultNotCurrent);
        if (existing is null || existing.BoundByTaskId == task.TaskId)
            return;
        var owner = await _store.GetTaskAsync(existing.BoundByTaskId, ct);
        if (owner is not null && owner.CreatedAt >= task.CreatedAt)
            throw new InvalidOperationException(ProductionFailureReasons.LateResultNotCurrent);
    }

    public Task<ProductionBinding> AcceptAsync(Guid artifactId, CancellationToken ct = default)
        => BindAsync(artifactId, ProductionBindingRoles.Accepted, ct);

    public async Task CompleteDecisionAsync(Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await RequireAttempt(attemptId, ct);
        if (attempt.Status != ProductionAttemptStatuses.ProviderSucceeded)
            await RecordProviderSucceededAsync(attemptId, attempt.RequestPayloadHash, ct);
        var task = await RequireTask(attempt.TaskId, ct);
        await _store.UpdateTaskAsync(task with
        {
            Status = ProductionTaskStatuses.Succeeded,
            CompletedAt = DateTimeOffset.UtcNow,
            FailureCode = null,
            FailureReason = null,
        }, ct);
        var request = await RequireRequest(task.RequestId, ct);
        await _store.UpdateRequestAsync(request with { Status = ProductionRequestStatuses.Completed }, ct);
    }

    public async Task<ProductionPreview> ResolveVisiblePreviewAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        var resolved = await OfficialVideoAuthorityBridgeV1.ResolveAsync(_store, buildId, shotCode, ct);
        if (resolved.FailClosed || resolved.Hit is null)
            return ProductionPreview.Empty(buildId, shotCode, resolved.Reason ?? ProductionFailureReasons.VisibleMissing);
        var artifact = await _store.GetArtifactAsync(resolved.Hit.ArtifactId, ct)
            ?? throw new InvalidOperationException("VISIBLE_ARTIFACT_MISSING");
        var task = await RequireTask(artifact.TaskId, ct);
        if (task.Scope.BuildId != buildId || !string.Equals(task.Scope.ShotCode, shotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.CrossShotForbidden);
        return new ProductionPreview(
            buildId, shotCode, artifact.ArtifactId, artifact.ByteSha256, artifact.StorageKey, artifact.DurationSec,
            resolved.Hit.BindingRole, artifact.MimeType, null);
    }

    public async Task<ProductionVoiceResolution> ResolveCurrentVoiceArtifactAsync(
        Guid buildId, string shotCode, string lineId, CancellationToken ct = default)
    {
        var wantedLine = (lineId ?? "").Trim();
        if (wantedLine.Length == 0)
            return ProductionVoiceResolution.Empty(buildId, shotCode, null, ProductionFailureReasons.MissingDependency);

        var current = await _store.GetActiveBindingAsync(
            buildId, shotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice), ct);
        if (current is null)
            return ProductionVoiceResolution.Empty(buildId, shotCode, wantedLine, ProductionFailureReasons.MissingDependency);

        var artifact = await _store.GetArtifactAsync(current.ArtifactId, ct)
            ?? throw new InvalidOperationException("VOICE_ARTIFACT_MISSING");
        if (artifact.Role != ProductionArtifactRoles.Voice
            || artifact.VerificationStatus != ProductionArtifactStatuses.Verified
            || string.IsNullOrWhiteSpace(artifact.StorageKey)
            || string.IsNullOrWhiteSpace(artifact.ByteSha256)
            || artifact.StorageKey.Contains("://", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var task = await RequireTask(artifact.TaskId, ct);
        if (task.Scope.BuildId != buildId || !string.Equals(task.Scope.ShotCode, shotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.CrossShotForbidden);

        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct)
            ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        if (!ProductionVoiceLineIdentity.Matches(snap, wantedLine))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);

        return new ProductionVoiceResolution(
            buildId, shotCode, wantedLine, artifact.ArtifactId, artifact.TaskId, artifact.AttemptId,
            snap.SnapshotHash, task.ProviderId ?? snap.ProviderId, task.ModelId ?? snap.ModelId,
            artifact.ByteSha256, artifact.StorageKey, artifact.MimeType, artifact.DurationSec,
            current.Role, null);
    }

    public async Task RejectCrossShotVisibleAsync(Guid artifactId, Guid otherBuildId, string otherShotCode, CancellationToken ct = default)
    {
        var artifact = await _store.GetArtifactAsync(artifactId, ct)
            ?? throw new InvalidOperationException("ARTIFACT_MISSING");
        var task = await RequireTask(artifact.TaskId, ct);
        if (task.Scope.BuildId != otherBuildId || !string.Equals(task.Scope.ShotCode, otherShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.CrossShotForbidden);
    }

    public async Task<ProductionAttempt> ApplyLateResultAsync(Guid attemptId, byte[] bytes, CancellationToken ct = default)
    {
        var attempt = await RequireAttempt(attemptId, ct);
        var task = await RequireTask(attempt.TaskId, ct);
        var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
        var newest = attempts.Max(a => a.AttemptNumber);
        var late = attempt with { Status = ProductionAttemptStatuses.Late, CompletedAt = DateTimeOffset.UtcNow };
        await _store.UpdateAttemptAsync(late, ct);
        if (attempt.AttemptNumber < newest)
        {
            await EventAsync(ProductionEventTypes.LateResultIgnored, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
            return late;
        }

        var current = await _store.GetActiveBindingAsync(task.Scope.BuildId, task.Scope.ShotCode, ProductionBindingRoles.Current, ct);
        if (current is not null)
        {
            await EventAsync(ProductionEventTypes.LateResultIgnored, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
            return late;
        }

        _ = bytes;
        await EventAsync(ProductionEventTypes.LateResultIgnored, task.RequestId, task.TaskId, attempt.AttemptId, null, task.Scope.ShotCode, task.Action, ct);
        return late;
    }

    public async Task<ProductionTask> NewTaskForChangedInputAsync(Guid priorTaskId, ShotExecutionInput live, string createdBy, CancellationToken ct = default)
    {
        var prior = await RequireTask(priorTaskId, ct);
        var frozen = await _store.GetSnapshotByTaskAsync(prior.TaskId, ct)
            ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        var compiled = ShotExecutionCompiler.Compile(live);
        var liveSnap = frozen with
        {
            Story = compiled.Story,
            ActingBeat = compiled.ActingBeat,
            StoryHash = ShotExecutionCompiler.Hash(compiled.Story),
            ActingHash = ShotExecutionCompiler.Hash(compiled.ActingBeat),
            KeyframeHash = live.KeyframeHash,
            PerformanceSnapshotId = live.PerformanceSnapshotId ?? frozen.PerformanceSnapshotId,
            PerformanceHash = live.PerformanceHash ?? frozen.PerformanceHash,
            PerformancePayloadJson = live.PerformancePayloadJson ?? frozen.PerformancePayloadJson,
        };
        liveSnap = liveSnap with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(liveSnap) };
        var reason = ProductionStateMachines.ClassifyInputDrift(frozen, liveSnap);
        if (string.IsNullOrEmpty(reason))
            throw new InvalidOperationException("Input did not change.");
        await _store.UpdateTaskAsync(prior with
        {
            Status = ProductionTaskStatuses.Superseded,
            FailureCode = reason,
            CompletedAt = DateTimeOffset.UtcNow,
        }, ct);
        await EventAsync(ProductionEventTypes.TaskSuperseded, prior.RequestId, prior.TaskId, null, null, prior.Scope.ShotCode, prior.Action, ct);
        var request = await CreateRequestAsync(prior.Scope, prior.Action, createdBy, ct: ct);
        var task = await CreateTaskAsync(request.RequestId, ct);
        await FreezeInputAsync(task.TaskId, live, prior.ProviderId ?? frozen.ProviderId, prior.ModelId ?? frozen.ModelId, ct);
        return await RequireTask(task.TaskId, ct);
    }

    public async Task<ProductionTask> NewTaskForProviderChangeAsync(Guid priorTaskId, string providerId, string modelId, string createdBy, CancellationToken ct = default)
    {
        var prior = await RequireTask(priorTaskId, ct);
        if (prior.ProviderId == providerId && prior.ModelId == modelId)
            throw new InvalidOperationException("Provider/model unchanged.");
        var frozen = await _store.GetSnapshotByTaskAsync(prior.TaskId, ct)
            ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        await _store.UpdateTaskAsync(prior with
        {
            Status = ProductionTaskStatuses.Superseded,
            FailureCode = prior.ProviderId != providerId
                ? ProductionFailureReasons.ProviderChanged
                : ProductionFailureReasons.ModelChanged,
            CompletedAt = DateTimeOffset.UtcNow,
        }, ct);
        var request = await CreateRequestAsync(prior.Scope, prior.Action, createdBy, ct: ct);
        var task = await CreateTaskAsync(request.RequestId, ct);
        var input = new ShotExecutionInput(
            frozen.Story, frozen.ActingBeat, frozen.VisualContract, "",
            frozen.KeyframeHash, frozen.TimingHash, frozen.VoiceHash, frozen.MotionHash,
            PerformanceSnapshotId: frozen.PerformanceSnapshotId,
            PerformanceHash: frozen.PerformanceHash);
        await FreezeInputAsync(task.TaskId, input, providerId, modelId, ct);
        return await RequireTask(task.TaskId, ct);
    }

    public Task<ProductionAttempt> RetryAsync(Guid taskId, string requestPayloadHash, CancellationToken ct = default)
        => StartAttemptAsync(taskId, requestPayloadHash, ct);

    public async Task<IReadOnlyList<ProductionActionAvailability>> ListAvailableActionsAsync(
        Guid buildId, string shotCode, bool keyframeApproved, bool storyPresent, CancellationToken ct = default)
    {
        _ = keyframeApproved;
        _ = storyPresent;
        _ = await _store.ListActiveBindingsAsync(buildId, shotCode, ct);
        var contract = ProductionActionContractRegistry.CreateVideo;
        return
        [
            new ProductionActionAvailability(
                ProductionActions.CreateVideo,
                contract.UiCta,
                false,
                ProductionFailureReasons.MissingKeyframe,
                contract.RequiredInputs,
                ["ApprovedKeyframe"]),
            new ProductionActionAvailability(
                ProductionActions.CreateImage,
                ProductionActionContractRegistry.CreateImage.UiCta,
                true, null),
            new ProductionActionAvailability(
                ProductionActions.CreateLipsync,
                ProductionActionContractRegistry.CreateLipsync.UiCta,
                true, null),
            new ProductionActionAvailability(
                ProductionActions.CreateVoice,
                ProductionActionContractRegistry.CreateVoice.UiCta,
                true, null),
            new ProductionActionAvailability(
                ProductionActions.Accept,
                ProductionActionContractRegistry.Accept.UiCta,
                true, null),
            new ProductionActionAvailability(
                ProductionActions.MixVideo,
                ProductionActionContractRegistry.MixVideo.UiCta,
                true, null),
        ];
    }

    public async Task<ProductionArtifact> MixExplicitAsync(
        Guid requestId, Guid motionOrLipsyncArtifactId, byte[] mixBytes, CancellationToken ct = default)
    {
        var source = await _store.GetArtifactAsync(motionOrLipsyncArtifactId, ct)
            ?? throw new InvalidOperationException("MIX_INPUT_ARTIFACT_MISSING");
        var task = await CreateTaskAsync(requestId, ct);
        if (task.Action != ProductionActions.MixVideo)
            throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden);
        var input = new ShotExecutionInput(
            $"mix-source:{source.ArtifactId:N}", "mix", "mix", "",
            source.ByteSha256, "timing", "", $"inputs={source.ArtifactId:N}");
        await FreezeInputAsync(task.TaskId, input, "assemble", "local-v1", ct);
        var attempt = await StartAttemptAsync(task.TaskId, source.ByteSha256, ct);
        await SubmitProviderJobAsync(attempt.AttemptId, $"mix:{source.ArtifactId:N}", ct);
        await RecordProviderSucceededAsync(attempt.AttemptId, KitVideoIntegrityRules.Sha256Hex(mixBytes), ct);
        return await VerifyArtifactAsync(attempt.AttemptId, mixBytes, "video/mp4", source.DurationSec, $"mix/{Guid.NewGuid():N}.mp4", ct);
    }

    public async Task<FilmAssembly> AssembleAsync(Guid buildId, string title, IReadOnlyList<(string ShotCode, Guid ArtifactId, decimal Duration)> shots, CancellationToken ct = default, int sortOrigin = 0)
    {
        var assembly = new FilmAssembly(Guid.NewGuid(), buildId, title, DateTimeOffset.UtcNow);
        await _store.InsertAssemblyAsync(assembly, ct);
        var order = sortOrigin;
        foreach (var shot in shots)
        {
            var artifact = await _store.GetArtifactAsync(shot.ArtifactId, ct)
                ?? throw new InvalidOperationException("ASSEMBLY_ARTIFACT_MISSING");
            var task = await RequireTask(artifact.TaskId, ct);
            if (task.Scope.BuildId != buildId || !string.Equals(task.Scope.ShotCode, shot.ShotCode, StringComparison.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.CrossShotForbidden);
            await _store.InsertAssemblyShotAsync(new FilmAssemblyShot(
                Guid.NewGuid(), assembly.AssemblyId, buildId, shot.ShotCode, shot.ArtifactId,
                order++, shot.Duration, "CUT", "BOUND"), ct);
        }
        return assembly;
    }

    public async Task RefuseUrlAuthorityAsync(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(ProductionFailureReasons.UrlAuthorityForbidden);
        await Task.CompletedTask;
    }

    public async Task RefuseRemapAsync(string requestAction, string attemptedAction)
    {
        if (!string.Equals(requestAction, attemptedAction, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden);
        await Task.CompletedTask;
    }

    async Task<ProductionCredentialReference?> PinCredentialAsync(
        string providerId, string modelId, ProductionCredentialReference? credential, ProductionScope scope, CancellationToken ct)
    {
        var pin = ProductionFrozenCredential.RequiresPin(providerId);
        if (!pin && credential is null)
            return null;
        if (!_official && _credentials is null && credential is null)
            return null;
        if (credential is null || credential.CredentialId == Guid.Empty || credential.CredentialVersionId == Guid.Empty)
        {
            if (!pin)
                return null;
            if (_credentials is null)
                throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
            var active = await _credentials.ResolveActiveAsync(providerId, modelId, ct);
            return new ProductionCredentialReference(active.CredentialId, active.CredentialVersionId);
        }
        if (_credentials is null)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        var material = await _credentials.ResolveAsync(
            credential.CredentialId, credential.CredentialVersionId, providerId, modelId, ct);
        if (material.CredentialId != credential.CredentialId
            || material.CredentialVersionId != credential.CredentialVersionId)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        return new ProductionCredentialReference(material.CredentialId, material.CredentialVersionId);
    }

    async Task<PerformanceSnapshotBindResult?> BindRequiredPerformanceAsync(
        ShotExecutionInput input, ProductionScope scope, string action, CancellationToken ct)
    {
        if (ProductionPerformanceConsumption.ActionRequiresSnapshot(action, scope)
            && !PerformanceSnapshotBinding.HasClaim(input))
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (!PerformanceSnapshotBinding.HasClaim(input)) return null;
        if (_performance is null)
            throw new InvalidOperationException(PerformanceContractIds.SnapshotNotFound);
        return await PerformanceSnapshotBinding.BindAsync(_performance, input, scope, ct);
    }

    async Task<ProductionRequest> RequireRequest(Guid id, CancellationToken ct) =>
        await _store.GetRequestAsync(id, ct) ?? throw new InvalidOperationException("REQUEST_MISSING");

    async Task<ProductionTask> RequireTask(Guid id, CancellationToken ct) =>
        await _store.GetTaskAsync(id, ct) ?? throw new InvalidOperationException("TASK_MISSING");

    async Task<ProductionAttempt> RequireAttempt(Guid id, CancellationToken ct) =>
        await _store.GetAttemptAsync(id, ct) ?? throw new InvalidOperationException("ATTEMPT_MISSING");

    const int AttemptErrorCodeMaxLength = 64;

    static string BoundAttemptErrorCode(string raw) =>
        raw.Length <= AttemptErrorCodeMaxLength ? raw : raw[..AttemptErrorCodeMaxLength];

    async Task EventAsync(string type, Guid? requestId, Guid? taskId, Guid? attemptId, Guid? artifactId, string shotCode, string action, CancellationToken ct)
    {
        await _store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(), type, DateTimeOffset.UtcNow, requestId, taskId, attemptId, artifactId, shotCode, action, null), ct);
    }
}

public sealed record ProductionPreview(
    Guid BuildId,
    string ShotCode,
    Guid? ArtifactId,
    string? ByteSha256,
    string? StorageKey,
    decimal? DurationSec,
    string? Role = null,
    string? MimeType = null,
    string? Reason = null)
{
    public bool IsEmpty => ArtifactId is null;

    public static ProductionPreview Empty(Guid buildId, string shotCode, string reason) =>
        new(buildId, shotCode, null, null, null, null, null, null, reason);
}
