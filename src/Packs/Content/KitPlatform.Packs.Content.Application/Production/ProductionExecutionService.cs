using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed class ProductionExecutionService : IProductionExecutionService
{
    readonly ProductionWorkExecutionEngine _engine;
    readonly IProductionWorkStore _store;
    readonly IMotionExecutionAdapter _motion;
    readonly IProductionScopeFoundationStore? _scopes;
    readonly IPerformanceSnapshotStore? _performance;
    readonly IProductionAiSelectionStore? _selections;
    readonly IProductionExecutionPermitStore? _permits;
    readonly IProductionTenantAiResolution? _tenantAi;

    public ProductionExecutionService(
        IProductionWorkStore store,
        IMotionExecutionAdapter? motion = null,
        IProductionScopeFoundationStore? scopes = null,
        IPerformanceSnapshotStore? performance = null,
        IProductionAiSelectionStore? selections = null,
        IProductionExecutionPermitStore? permits = null,
        IProductionCredentialResolver? credentials = null,
        IProductionTenantAiResolution? tenantAi = null)
    {
        _tenantAi = tenantAi;
        _store = store;
        _engine = ProductionWorkExecutionEngine.CreateOfficial(store, credentials, performance);
        _motion = motion ?? new FixtureMotionAdapter();
        _scopes = scopes;
        _performance = performance;
        _selections = selections;
        _permits = permits;
    }

    public Task<ProductionRequest> CreateRequestAsync(ProductionScope scope, string userAction, string createdBy, string? idempotencyKey = null, Guid? parentRequestId = null, CancellationToken ct = default)
        => _engine.CreateRequestAsync(scope, userAction, createdBy, idempotencyKey, parentRequestId, ct);

    public Task<ProductionTask> CreateTaskAsync(Guid requestId, CancellationToken ct = default)
        => _engine.CreateTaskAsync(requestId, ct);

    public async Task<ProductionTaskInputSnapshot> FreezeInputAsync(
        Guid taskId, ShotExecutionInput input, string providerId, string modelId,
        CancellationToken ct = default, ProductionCredentialReference? credential = null,
        bool requireCreateVideoDependencies = true)
    {
        var task = await _store.GetTaskAsync(taskId, ct) ?? throw new InvalidOperationException("TASK_MISSING");
        if (requireCreateVideoDependencies && task.Action == ProductionActions.CreateVideo)
            await RequireCreateVideoDependenciesAsync(task.Scope, input, ct);
        return await _engine.FreezeInputAsync(taskId, input, providerId, modelId, ct, credential);
    }

    public Task<ProductionAttempt> StartAttemptAsync(Guid taskId, string requestPayloadHash, CancellationToken ct = default)
        => _engine.StartAttemptAsync(taskId, requestPayloadHash, ct);

    public Task<ProductionAttempt> BeginOfficialProviderStartAsync(
        Guid taskId, string requestPayloadHash, Guid? reuseAttemptId = null, CancellationToken ct = default)
        => _engine.BeginOfficialProviderStartAsync(taskId, requestPayloadHash, reuseAttemptId, ct);

    public Task<ProductionAttempt> SubmitProviderJobAsync(Guid attemptId, string providerJobId, CancellationToken ct = default)
        => _engine.SubmitProviderJobAsync(attemptId, providerJobId, ct);

    public Task<ProductionAttempt> CompleteAttemptAsync(Guid attemptId, string responsePayloadHash, CancellationToken ct = default)
        => _engine.RecordProviderSucceededAsync(attemptId, responsePayloadHash, ct);

    public Task<ProductionAttempt> FailAttemptAsync(Guid attemptId, string errorCode, CancellationToken ct = default)
        => _engine.RecordProviderFailedAsync(attemptId, errorCode, ct);

    public Task<ProductionAttempt> RecordExecutionProcessFailedAsync(Guid attemptId, string detail, CancellationToken ct = default)
        => _engine.RecordExecutionProcessFailedAsync(attemptId, detail, ct);

    public Task<ProductionArtifact> VerifyArtifactAsync(Guid attemptId, byte[] bytes, string mime, decimal durationSec, string storageKey, CancellationToken ct = default)
        => _engine.VerifyArtifactAsync(attemptId, bytes, mime, durationSec, storageKey, ct);

    public Task<ProductionBinding> BindCurrentAsync(Guid artifactId, CancellationToken ct = default)
        => BindTypedAndAliasAsync(artifactId, ProductionBindingRoles.Current, protectNewerLineage: false, ct);

    public Task<ProductionBinding> BindVisibleAsync(Guid artifactId, CancellationToken ct = default)
        => BindTypedVisibleAsync(artifactId, protectNewerLineage: false, ct);

    public Task<ProductionBinding> AcceptArtifactAsync(Guid artifactId, CancellationToken ct = default)
        => _engine.AcceptAsync(artifactId, ct);

    public Task<ProductionBinding> BindNamedAsync(Guid artifactId, string role, CancellationToken ct = default)
        => _engine.BindAsync(artifactId, role, ct);

    public Task<ProductionBinding> BindCurrentProtectedAsync(Guid artifactId, CancellationToken ct = default)
        => BindTypedAndAliasAsync(artifactId, ProductionBindingRoles.Current, protectNewerLineage: true, ct);

    public Task<ProductionBinding> BindVisibleProtectedAsync(Guid artifactId, CancellationToken ct = default)
        => BindTypedVisibleAsync(artifactId, protectNewerLineage: true, ct);

    public Task<ProductionBinding> BindNamedProtectedAsync(Guid artifactId, string role, CancellationToken ct = default)
        => _engine.BindProtectedAsync(artifactId, role, ct);

    public Task CompleteDecisionAsync(Guid attemptId, CancellationToken ct = default)
        => _engine.CompleteDecisionAsync(attemptId, ct);

    public Task<ProductionTask> CreateChildTaskAsync(Guid parentRequestId, string childAction, string createdBy, CancellationToken ct = default)
        => _engine.CreateChildTaskAsync(parentRequestId, childAction, createdBy, ct);

    public async Task InvalidateDownstreamAsync(ProductionScope scope, string changedNode, CancellationToken ct = default)
    {
        var reason = ShotDependencyGraph.ReasonFor(changedNode);
        var roles = ShotDependencyGraph.BindingRolesInvalidatedBy(changedNode);
        foreach (var role in roles)
        {
            var binding = await _store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, role, ct);
            if (binding is not null)
                await _store.UnbindAsync(binding.BindingId, DateTimeOffset.UtcNow, ct);
        }

        var tasks = await _store.ListTasksByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        var staleActions = StaleActionsFor(changedNode);
        foreach (var task in tasks.Where(t => t.Status == ProductionTaskStatuses.Succeeded && staleActions.Contains(t.Action)))
        {
            await _store.UpdateTaskAsync(task with
            {
                Status = ProductionTaskStatuses.Superseded,
                FailureCode = reason,
                FailureReason = ProductionFailureReasons.StaleTask,
                CompletedAt = DateTimeOffset.UtcNow,
            }, ct);
            await _store.InsertEventAsync(new ProductionTaskEvent(
                Guid.NewGuid(), ProductionEventTypes.TaskStale, DateTimeOffset.UtcNow,
                task.RequestId, task.TaskId, null, null, scope.ShotCode, task.Action, reason), ct);
        }
    }

    public Task<FilmAssembly> CreateAssemblyAsync(Guid buildId, string title, IReadOnlyList<(string ShotCode, Guid ArtifactId, decimal Duration)> shots, CancellationToken ct = default, int sortOrigin = 0)
        => _engine.AssembleAsync(buildId, title, shots, ct, sortOrigin);

    public Task<ProductionAiSelection?> GetActiveVideoSelectionAsync(ProductionScope scope, CancellationToken ct = default)
        => _selections is null
            ? Task.FromResult<ProductionAiSelection?>(null)
            : _selections.GetActiveAsync(scope.BuildId, scope.ShotCode, ProductionAiSelectionStages.Video, ct);

    public async Task<CreateVideoContractVerdict> EvaluateCreateVideoAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        if (claim?.KeyframeArtifactId is { } claimedKf)
            await RequireArtifactInRequestScopeAsync(scope.BuildId, claimedKf, ct);
        var os = await LoadCreateVideoOsFactsAsync(scope, ct);
        if (os.Keyframe is not null)
            await RequireArtifactOwnerScopeAsync(scope.BuildId, os.Keyframe.TaskId, ct);
        var official = ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId);
        var selection = await GetActiveVideoSelectionAsync(scope, ct);
        string provider;
        string model;
        string? tenantBlock = null;
        if (_tenantAi is not null && await _tenantAi.GovernsAsync(scope, ct))
        {
            try
            {
                var resolved = await _tenantAi.ResolveAsync(scope, ProductionTenantAiCapabilities.Video, ct);
                (provider, model) = (resolved.ProviderId, resolved.ModelId);
                if (ProductionCreateVideoProviderBinding.HasProviderModel(claim?.ProviderId, claim?.ModelId)
                    && (!string.Equals(claim!.ProviderId!.Trim(), provider, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(claim.ModelId!.Trim(), model, StringComparison.OrdinalIgnoreCase)))
                    tenantBlock = ProductionFailureReasons.ProviderModelMismatch;
            }
            catch (InvalidOperationException ex)
            {
                (provider, model) = ("", "");
                tenantBlock = ex.Message;
            }
        }
        else
        {
            (provider, model) = ProductionCreateVideoProviderBinding.Authoritative(
                official, selection, claim?.ProviderId, claim?.ModelId, official ? null : _motion);
        }
        var verdict = CreateVideoActionContractGate.Evaluate(scope, os, claim, provider, model);
        if (tenantBlock is not null)
            verdict = verdict with { Available = false, BlockReason = tenantBlock };
        if (FamixaEp01UpstreamHold.BlocksMotion(verdict.KeyframeArtifactId))
            verdict = verdict with { Available = false, BlockReason = FamixaEp01UpstreamHold.MotionBlocked };
        if (official && verdict.BlockReason == ProductionFailureReasons.MissingTiming)
        {
            verdict = verdict with
            {
                Available = false,
                BlockReason = ProductionFailureReasons.TimingProductionDecisionRequired,
            };
        }
        else if (official && selection is null && WhatInputsPresent(verdict))
        {
            var missing = verdict.MissingInputs.Contains("FrozenProviderModel")
                ? verdict.MissingInputs
                : verdict.MissingInputs.Append("FrozenProviderModel").ToArray();
            verdict = verdict with
            {
                Available = false,
                BlockReason = ProductionFailureReasons.MissingProductionAiSelection,
                MissingInputs = missing,
                MissingReasons = missing.Select(m =>
                    m == "FrozenProviderModel"
                        ? ProductionFailureReasons.MissingProductionAiSelection
                        : CreateVideoActionContractGate.ReasonFor(m)).ToArray(),
            };
        }
        else if (verdict.Available
            && official
            && ProductionMotionCatalog.TryGet(provider, model, out var paidCatalog)
            && paidCatalog.IsPaid)
        {
            var issued = _permits is null
                ? null
                : await _permits.GetIssuedAsync(
                    scope.BuildId, scope.ShotCode, ProductionActions.CreateVideo, provider, model, ct);
            if (issued is null
                || !ProductionExecutionPermitCanon.Matches(
                    issued, scope.BuildId, scope.ShotCode, ProductionActions.CreateVideo, provider, model))
            {
                verdict = verdict with
                {
                    Available = false,
                    BlockReason = PerformanceContractIds.OfficialFilmForbidden,
                };
            }
        }
        if (verdict.Available
            && ProductionMotionCatalog.TryGet(provider, model, out var catalog)
            && catalog.IsPaid)
        {
            var cost = ProductionMotionCostGuard.Evaluate(provider, model, verdict.DurationSec, catalog.IsPaid);
            if (!cost.Allowed)
                verdict = verdict with { Available = false, BlockReason = cost.BlockReason };
        }
        return await BindCreateVideoSnapshotAsync(scope, claim, verdict, ct);
    }

    public async Task<ProductionActionInputVerdict> EvaluateCreateVoiceAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default, bool requireIssuedPermit = true)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        if (ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId))
            return await EvaluateOfficialCreateVoiceAsync(scope, claim, requireIssuedPermit, ct);
        if (!ProductionPerformanceConsumption.ActionRequiresSnapshot(ProductionActions.CreateVoice, scope)
            && !ProductionPerformanceConsumption.HasSnapshotClaim(claim))
            return new ProductionActionInputVerdict(true, null, [], []);

        var missing = new List<string>();
        if (!CreateVideoActionContractGate.IsPresent(claim?.LineText) && !CreateVideoActionContractGate.IsPresent(claim?.Story))
            missing.Add("LineText");
        if (!CreateVideoActionContractGate.IsPresent(claim?.VoiceId)) missing.Add("VoiceId");
        if (!CreateVideoActionContractGate.IsPresent(claim?.LineId)) missing.Add("LineId");
        if (!CreateVideoActionContractGate.IsPresent(claim?.CharacterId)) missing.Add("CharacterId");
        ProductionPerformanceConsumption.CollectMissing(scope, ProductionActions.CreateVoice, claim, missing);
        if (missing.Count > 0)
        {
            var reasons = missing.Select(m => m == ProductionPerformanceConsumption.RequiredInput
                ? ProductionPerformanceConsumption.ReasonForMissing(claim)
                : ProductionFailureReasons.MissingDependency).ToArray();
            return new ProductionActionInputVerdict(false, reasons[0], missing, reasons);
        }

        try
        {
            var bound = await ProductionPerformanceConsumption.TryBindClaimAsync(_performance, claim, scope, ct)
                ?? throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
            var lineText = CreateVideoActionContractGate.IsPresent(claim?.LineText) ? claim!.LineText! : claim?.Story ?? "";
            ProductionPerformanceConsumption.RequireVoiceLine(bound.Draft, claim?.LineId ?? "", claim?.CharacterId ?? "", lineText);
            var draft = ShortProductionQualityPipelineV1.PrepareVoiceDraft(scope.ShotCode, lineText);
            if (!draft.Permitted)
                return new ProductionActionInputVerdict(false, draft.Blocker, ["LineText"], [draft.Blocker ?? ShortProductionQualityPipelineV1.SpokenLineIncomplete]);
            return new ProductionActionInputVerdict(
                true, null, [], [], bound.PerformanceSnapshotId, bound.PerformanceHash, bound.PerformancePayloadJson);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(false, ex.Message, [ProductionPerformanceConsumption.RequiredInput], [ex.Message]);
        }
    }

    async Task<ProductionActionInputVerdict> EvaluateOfficialCreateVoiceAsync(
        ProductionScope scope, CreateVideoReadinessClaim? claim, bool requireIssuedPermit, CancellationToken ct)
    {
        PerformanceSnapshot? snap = null;
        if (ProductionPerformanceConsumption.HasSnapshotClaim(claim))
        {
            try
            {
                var bound = await ProductionPerformanceConsumption.TryBindClaimAsync(_performance, claim, scope, ct);
                snap = bound?.Snapshot;
            }
            catch (InvalidOperationException ex)
            {
                return new ProductionActionInputVerdict(false, ex.Message, [ProductionPerformanceConsumption.RequiredInput], [ex.Message]);
            }
        }
        else if (_performance is not null)
            snap = await _performance.GetByScopeAsync(scope.BuildId, scope.ShotCode, ct);

        if (snap is null)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingPerformanceSnapshot,
                [ProductionPerformanceConsumption.RequiredInput],
                [ProductionFailureReasons.MissingPerformanceSnapshot]);

        var spoken = snap.Draft.Lines
            .Where(l => l.Delivery.Locked && !string.IsNullOrWhiteSpace(l.TextIdentity.Text))
            .ToArray();
        if (spoken.Length == 0)
            return new ProductionActionInputVerdict(
                false,
                CreateVideoActionContractGate.IsPresent(claim?.LineId)
                    || CreateVideoActionContractGate.IsPresent(claim?.LineText)
                    || CreateVideoActionContractGate.IsPresent(claim?.Story)
                    ? ProductionFailureReasons.PerformanceLineMismatch
                    : ProductionFailureReasons.MissingDependency,
                ["LineText", "VoiceId", "LineId", "CharacterId"],
                [CreateVideoActionContractGate.IsPresent(claim?.LineId)
                    || CreateVideoActionContractGate.IsPresent(claim?.LineText)
                    || CreateVideoActionContractGate.IsPresent(claim?.Story)
                    ? ProductionFailureReasons.PerformanceLineMismatch
                    : ProductionFailureReasons.MissingDependency]);
        if (spoken.Length > 1
            && !CreateVideoActionContractGate.IsPresent(claim?.LineId))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.VoiceProductionDecisionRequired,
                ["LineId"],
                [ProductionFailureReasons.VoiceProductionDecisionRequired]);

        var line = spoken.Length == 1
            ? spoken[0]
            : spoken.FirstOrDefault(l => string.Equals(l.LineId, claim?.LineId, StringComparison.Ordinal));
        if (line is null)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.PerformanceLineMismatch,
                ["LineId"],
                [ProductionFailureReasons.PerformanceLineMismatch]);

        var claimedText = CreateVideoActionContractGate.IsPresent(claim?.LineText) ? claim!.LineText!
            : CreateVideoActionContractGate.IsPresent(claim?.Story) ? claim!.Story! : line.TextIdentity.Text;
        var claimedLine = CreateVideoActionContractGate.IsPresent(claim?.LineId) ? claim!.LineId! : line.LineId;
        var claimedChar = CreateVideoActionContractGate.IsPresent(claim?.CharacterId) ? claim!.CharacterId! : line.CharacterId;
        try
        {
            ProductionPerformanceConsumption.RequireVoiceLine(snap.Draft, claimedLine, claimedChar, claimedText);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(false, ex.Message, [ProductionPerformanceConsumption.RequiredInput], [ex.Message]);
        }

        var spokenDraft = ShortProductionQualityPipelineV1.PrepareVoiceDraft(scope.ShotCode, line.TextIdentity.Text);
        if (!spokenDraft.Permitted)
            return new ProductionActionInputVerdict(
                false, spokenDraft.Blocker, ["LineText"],
                [spokenDraft.Blocker ?? ShortProductionQualityPipelineV1.SpokenLineIncomplete],
                snap.PerformanceSnapshotId, snap.PerformanceHash, snap.PayloadJson);

        var selection = _selections is null
            ? null
            : await _selections.GetActiveAsync(scope.BuildId, scope.ShotCode, ProductionAiSelectionStages.Voice, ct);
        var voiceId = ProductionVoiceSelectionCanon.VoiceIdOf(selection);
        if (selection is null || string.IsNullOrWhiteSpace(selection.ProviderId) || string.IsNullOrWhiteSpace(selection.ModelId))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingProductionAiSelection,
                ["FrozenProviderModel"],
                [ProductionFailureReasons.MissingProductionAiSelection],
                snap.PerformanceSnapshotId, snap.PerformanceHash, snap.PayloadJson);
        if (voiceId.Length == 0)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.VoiceIdProductionDecisionRequired,
                ["VoiceId"],
                [ProductionFailureReasons.VoiceIdProductionDecisionRequired],
                snap.PerformanceSnapshotId, snap.PerformanceHash, snap.PayloadJson);
        if (CreateVideoActionContractGate.IsPresent(claim?.VoiceId)
            && !string.Equals(claim!.VoiceId!.Trim(), voiceId, StringComparison.Ordinal))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.ProviderModelMismatch,
                ["VoiceId"],
                [ProductionFailureReasons.ProviderModelMismatch],
                snap.PerformanceSnapshotId, snap.PerformanceHash, snap.PayloadJson);

        if (requireIssuedPermit)
        {
            var issued = _permits is null
                ? null
                : await _permits.GetIssuedAsync(
                    scope.BuildId, scope.ShotCode, ProductionActions.CreateVoice, selection.ProviderId, selection.ModelId, ct);
            if (issued is null
                || !ProductionExecutionPermitCanon.Matches(
                    issued, scope.BuildId, scope.ShotCode, ProductionActions.CreateVoice, selection.ProviderId, selection.ModelId))
                return new ProductionActionInputVerdict(
                    false, PerformanceContractIds.OfficialFilmForbidden,
                    ["Permit"],
                    [PerformanceContractIds.OfficialFilmForbidden],
                    snap.PerformanceSnapshotId, snap.PerformanceHash, snap.PayloadJson);
        }

        return new ProductionActionInputVerdict(
            true, null, [], [], snap.PerformanceSnapshotId, snap.PerformanceHash, snap.PayloadJson,
            LineId: line.LineId,
            LineText: line.TextIdentity.Text,
            CharacterId: line.CharacterId,
            VoiceId: voiceId,
            ProviderId: selection.ProviderId,
            ModelId: selection.ModelId);
    }

    public async Task<ProductionActionInputVerdict> EvaluateCreateLipsyncAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        if (ProductionEp01Sh02LipsyncCanon.MatchesScope(scope))
            return await EvaluateOfficialSh02CreateLipsyncAsync(scope, claim, ct);
        var currentVideo = await ResolveCurrentTypedArtifactAsync(scope, ProductionArtifactRoles.VideoTake, ct);
        var currentVoice = await ResolveCurrentTypedArtifactAsync(scope, ProductionArtifactRoles.Voice, ct);
        var motionId = claim?.MotionArtifactId ?? currentVideo?.ArtifactId;
        var voiceId = claim?.VoiceArtifactId ?? currentVoice?.ArtifactId;
        if (FamixaEp01UpstreamHold.BlocksLipsync(motionId))
            return new ProductionActionInputVerdict(
                false, FamixaEp01UpstreamHold.LipsyncBlocked, ["MotionArtifactId"],
                [FamixaEp01UpstreamHold.LipsyncBlocked], VideoArtifactId: motionId, VoiceArtifactId: voiceId);
        var missing = new List<string>();
        var reasons = new List<string>();
        if (motionId is null)
        {
            missing.Add("MotionArtifactId");
            reasons.Add(ProductionFailureReasons.MissingVideoArtifact);
        }
        if (voiceId is null)
        {
            missing.Add("VoiceArtifactId");
            reasons.Add(ProductionFailureReasons.MissingVoiceArtifact);
        }
        if (missing.Count > 0 || motionId is null || voiceId is null)
            return new ProductionActionInputVerdict(false, reasons[0], missing, reasons);

        if (claim?.MotionArtifactId is { } claimedVideo && currentVideo is not null && claimedVideo != currentVideo.ArtifactId)
            return new ProductionActionInputVerdict(false, ProductionFailureReasons.BindingChanged, ["MotionArtifactId"], [ProductionFailureReasons.BindingChanged]);
        if (claim?.VoiceArtifactId is { } claimedVoice && currentVoice is not null && claimedVoice != currentVoice.ArtifactId)
            return new ProductionActionInputVerdict(false, ProductionFailureReasons.BindingChanged, ["VoiceArtifactId"], [ProductionFailureReasons.BindingChanged]);
        if (ProductionEp01Sh01LipsyncCanon.MatchesScope(scope))
        {
            if (currentVideo is null
                || currentVideo.ArtifactId != ProductionEp01Sh01LipsyncCanon.VideoArtifactId
                || !string.Equals(currentVideo.ByteSha256, ProductionEp01Sh01LipsyncCanon.VideoSha256, StringComparison.OrdinalIgnoreCase)
                || currentVoice is null
                || currentVoice.ArtifactId != ProductionEp01Sh01LipsyncCanon.VoiceArtifactId
                || !string.Equals(currentVoice.ByteSha256, ProductionEp01Sh01LipsyncCanon.VoiceSha256, StringComparison.OrdinalIgnoreCase)
                || motionId != ProductionEp01Sh01LipsyncCanon.VideoArtifactId
                || voiceId != ProductionEp01Sh01LipsyncCanon.VoiceArtifactId)
                return new ProductionActionInputVerdict(
                    false, ProductionFailureReasons.BindingChanged, ["MotionArtifactId", "VoiceArtifactId"],
                    [ProductionFailureReasons.BindingChanged],
                    VideoArtifactId: currentVideo?.ArtifactId ?? motionId,
                    VoiceArtifactId: currentVoice?.ArtifactId ?? voiceId);
            if (!ProductionLipsyncSpeakerTarget.TryResolve(ProductionEp01Sh01LipsyncCanon.SpeakerCharacterId, out _))
                return new ProductionActionInputVerdict(
                    false, ProductionFailureReasons.LipsyncTargetUnresolved, ["SpeakerCharacterId"],
                    [ProductionFailureReasons.LipsyncTargetUnresolved],
                    VideoArtifactId: motionId, VoiceArtifactId: voiceId);
        }

        try
        {
            await RequireLipsyncParentIdentityAsync(scope, motionId.Value, ProductionArtifactRoles.VideoTake, ct);
            await RequireLipsyncParentIdentityAsync(scope, voiceId.Value, ProductionArtifactRoles.Voice, ct);
            if (!string.IsNullOrWhiteSpace(claim?.LineId))
            {
                var voiceSnap = await SnapshotForArtifactAsync(voiceId.Value, ct);
                if (!ProductionVoiceLineIdentity.Matches(voiceSnap, claim.LineId))
                    return new ProductionActionInputVerdict(
                        false, ProductionFailureReasons.PerformanceLineMismatch, ["LineId"], [ProductionFailureReasons.PerformanceLineMismatch],
                        VideoArtifactId: motionId, VoiceArtifactId: voiceId);
            }
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(false, ex.Message, missing, [ex.Message], VideoArtifactId: motionId, VoiceArtifactId: voiceId);
        }

        var motionArtifact = await _store.GetArtifactAsync(motionId.Value, ct);
        if (ProductionEp01Sh02LipsyncCanon.IsTenantByteCopy(scope, motionArtifact?.ByteSha256))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.BindingChanged, ["MotionArtifactId"],
                [ProductionFailureReasons.BindingChanged],
                VideoArtifactId: motionId, VoiceArtifactId: voiceId);

        if (!ProductionPerformanceConsumption.ActionRequiresSnapshot(ProductionActions.CreateLipsync, scope)
            && !ProductionPerformanceConsumption.HasSnapshotClaim(claim))
            return new ProductionActionInputVerdict(true, null, [], [], VideoArtifactId: motionId, VoiceArtifactId: voiceId);

        try
        {
            var motionSnap = await SnapshotForArtifactAsync(motionId.Value, ct);
            var voiceSnap = await SnapshotForArtifactAsync(voiceId.Value, ct);
            if (ProductionPerformanceConsumption.HasSnapshotClaim(claim))
            {
                var bound = await ProductionPerformanceConsumption.TryBindClaimAsync(_performance, claim, scope, ct)
                    ?? throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
                ProductionPerformanceConsumption.RequireSharedParentPerformance(motionSnap, voiceSnap, bound.PerformanceHash);
                return new ProductionActionInputVerdict(
                    true, null, [], [], bound.PerformanceSnapshotId, bound.PerformanceHash, bound.PerformancePayloadJson,
                    VideoArtifactId: motionId, VoiceArtifactId: voiceId);
            }

            ProductionPerformanceConsumption.RequireSharedParentPerformance(motionSnap, voiceSnap, null);
            return new ProductionActionInputVerdict(
                true, null, [], [], motionSnap?.PerformanceSnapshotId, motionSnap?.PerformanceHash, motionSnap?.PerformancePayloadJson,
                VideoArtifactId: motionId, VoiceArtifactId: voiceId);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(false, ex.Message, [ProductionPerformanceConsumption.RequiredInput], [ex.Message],
                VideoArtifactId: motionId, VoiceArtifactId: voiceId);
        }
    }

    async Task<ProductionActionInputVerdict> EvaluateOfficialSh02CreateLipsyncAsync(
        ProductionScope scope, CreateVideoReadinessClaim? claim, CancellationToken ct)
    {
        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(
            _store, scope.BuildId, scope.ShotCode, ct);
        if (take is null)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingVideoArtifact, ["MotionArtifactId"],
                [ProductionFailureReasons.MissingVideoArtifact]);
        if (!ProductionEp01Sh02LipsyncCanon.MatchesVideo(take))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.BindingChanged, ["MotionArtifactId"],
                [ProductionFailureReasons.BindingChanged],
                VideoArtifactId: take.ArtifactId);

        var voice = await ResolveCurrentTypedArtifactAsync(scope, ProductionArtifactRoles.Voice, ct)
            ?? await ResolveAcceptedTypedArtifactAsync(scope, ProductionArtifactRoles.Voice, ct);
        if (voice is null)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingVoiceArtifact, ["VoiceArtifactId"],
                [ProductionFailureReasons.MissingVoiceArtifact],
                VideoArtifactId: take.ArtifactId);
        if (!ProductionEp01Sh02LipsyncCanon.MatchesVoice(voice))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.BindingChanged, ["VoiceArtifactId"],
                [ProductionFailureReasons.BindingChanged],
                VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);

        var acceptedVoice = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice), ct);
        if (acceptedVoice is null || acceptedVoice.ArtifactId != voice.ArtifactId)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.BindingChanged, ["VoiceArtifactId"],
                [ProductionFailureReasons.BindingChanged],
                VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);

        if (claim?.MotionArtifactId is { } claimedVideo && claimedVideo != take.ArtifactId)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.BindingChanged, ["MotionArtifactId"],
                [ProductionFailureReasons.BindingChanged],
                VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);
        if (claim?.VoiceArtifactId is { } claimedVoice && claimedVoice != voice.ArtifactId)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.BindingChanged, ["VoiceArtifactId"],
                [ProductionFailureReasons.BindingChanged],
                VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);
        if (!ProductionEp01Sh02LipsyncCanon.TryResolveSpeaker(
                ProductionEp01Sh02LipsyncCanon.LineId, ProductionEp01Sh02LipsyncCanon.SpeakerCharacterId, out _))
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.LipsyncTargetUnresolved, ["SpeakerCharacterId"],
                [ProductionFailureReasons.LipsyncTargetUnresolved],
                VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);

        try
        {
            await RequireLipsyncParentIdentityAsync(scope, take.ArtifactId, ProductionArtifactRoles.VideoTake, ct);
            await RequireLipsyncParentIdentityAsync(scope, voice.ArtifactId, ProductionArtifactRoles.Voice, ct);
            var voiceSnap = await SnapshotForArtifactAsync(voice.ArtifactId, ct);
            if (!ProductionVoiceLineIdentity.Matches(voiceSnap, ProductionEp01Sh02LipsyncCanon.LineId))
                return new ProductionActionInputVerdict(
                    false, ProductionFailureReasons.PerformanceLineMismatch, ["LineId"],
                    [ProductionFailureReasons.PerformanceLineMismatch],
                    VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(
                false, ex.Message, [], [ex.Message],
                VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);
        }

        return new ProductionActionInputVerdict(
            true, null, [], [],
            VideoArtifactId: take.ArtifactId, VoiceArtifactId: voice.ArtifactId);
    }

    public async Task<ProductionActionInputVerdict> EvaluateCreateMixAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        if (NovixaShortProductionStrategyV1.IsConfirmedStaticVoiceOver(claim))
            return await EvaluateStaticVoiceOverMixAsync(scope, ct);
        var currentLipsync = await ResolveCurrentTypedArtifactAsync(scope, ProductionArtifactRoles.Lipsync, ct);
        var lipsyncId = claim?.LipsyncArtifactId ?? currentLipsync?.ArtifactId;
        if (lipsyncId is null)
            return new ProductionActionInputVerdict(false, ProductionFailureReasons.MissingLipsyncArtifact, ["LipSyncArtifactId"], [ProductionFailureReasons.MissingLipsyncArtifact]);

        if (claim?.LipsyncArtifactId is { } claimed && currentLipsync is not null && claimed != currentLipsync.ArtifactId)
            return new ProductionActionInputVerdict(false, ProductionFailureReasons.BindingChanged, ["LipSyncArtifactId"], [ProductionFailureReasons.BindingChanged], LipsyncArtifactId: currentLipsync.ArtifactId);

        try
        {
            await RequireLipsyncParentIdentityAsync(scope, lipsyncId.Value, ProductionArtifactRoles.Lipsync, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(false, ex.Message, ["LipSyncArtifactId"], [ex.Message], LipsyncArtifactId: lipsyncId);
        }

        if (!ProductionMixContract.AppliesTo(scope))
            return new ProductionActionInputVerdict(true, null, [], [], LipsyncArtifactId: lipsyncId);

        var videoResolved = await OfficialVideoAuthorityBridgeV1.ResolveVideoTakeAsync(
            _store, scope.BuildId, scope.ShotCode, ct);
        if (videoResolved.Hit is null)
            return new ProductionActionInputVerdict(
                false,
                videoResolved.FailClosed
                    ? videoResolved.Reason ?? ProductionFailureReasons.MissingVideoArtifact
                    : ProductionFailureReasons.MissingVideoArtifact,
                ["VideoTakeArtifactId"],
                [ProductionFailureReasons.MissingVideoArtifact],
                LipsyncArtifactId: lipsyncId);

        try
        {
            await RequireLipsyncParentIdentityAsync(scope, videoResolved.Hit.ArtifactId, ProductionArtifactRoles.VideoTake, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(
                false, ex.Message, ["VideoTakeArtifactId"], [ex.Message],
                VideoArtifactId: videoResolved.Hit.ArtifactId, LipsyncArtifactId: lipsyncId);
        }

        var video = await _store.GetArtifactAsync(videoResolved.Hit.ArtifactId, ct);
        var lipsync = await _store.GetArtifactAsync(lipsyncId.Value, ct);
        if (video is null || lipsync is null)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingVideoArtifact, ["VideoTakeArtifactId"],
                [ProductionFailureReasons.MissingVideoArtifact], LipsyncArtifactId: lipsyncId);
        if (ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode))
        {
            if (ProductionEp01Sh02LipsyncCanon.IsTenantByteCopy(scope, video.ByteSha256))
                return new ProductionActionInputVerdict(
                    false, ProductionFailureReasons.BindingChanged, ["VideoTakeArtifactId"],
                    [ProductionFailureReasons.BindingChanged],
                    VideoArtifactId: video.ArtifactId, LipsyncArtifactId: lipsync.ArtifactId);
            var lipTask = await _store.GetTaskAsync(lipsync.TaskId, ct);
            if (lipTask is not null && ProductionLipsyncCatalog.IsFixtureProvider(lipTask.ProviderId))
                return new ProductionActionInputVerdict(
                    false, ProductionFailureReasons.FailedIdentity, ["LipSyncArtifactId"],
                    [ProductionFailureReasons.FailedIdentity],
                    VideoArtifactId: video.ArtifactId, LipsyncArtifactId: lipsync.ArtifactId);
        }
        try
        {
            var directorSpeech = ProductionDirectorTiming.ApprovedSpeech(await LoadDirectorTimingAsync(scope, ct));
            _ = ProductionMixContract.Build(
                video, lipsync, videoResolved.Hit.BindingRole,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
                claim?.Timing is { Length: > 0 } t ? ShotExecutionCompiler.Hash(t) : "",
                directorSpeech);
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(
                false, ex.Message, ["Timing"], [ex.Message],
                VideoArtifactId: video.ArtifactId, LipsyncArtifactId: lipsync.ArtifactId);
        }

        return new ProductionActionInputVerdict(
            true, null, [], [],
            VideoArtifactId: video.ArtifactId, LipsyncArtifactId: lipsync.ArtifactId);
    }

    async Task<ProductionActionInputVerdict> EvaluateStaticVoiceOverMixAsync(ProductionScope scope, CancellationToken ct)
    {
        var keyframe = await ResolveAcceptedTypedArtifactAsync(scope, ProductionArtifactRoles.Keyframe, ct);
        if (keyframe is null || keyframe.Role != ProductionArtifactRoles.Keyframe)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingKeyframe, ["KeyframeArtifactId"],
                [ProductionFailureReasons.MissingKeyframe]);
        var voice = await ResolveAcceptedTypedArtifactAsync(scope, ProductionArtifactRoles.Voice, ct);
        if (voice is null || voice.Role != ProductionArtifactRoles.Voice || voice.DurationSec <= 0)
            return new ProductionActionInputVerdict(
                false, ProductionFailureReasons.MissingVoiceArtifact, ["VoiceArtifactId"],
                [ProductionFailureReasons.MissingVoiceArtifact]);
        try
        {
            _ = ProductionMixContract.BuildVoiceOverStill(
                keyframe,
                voice,
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Keyframe),
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice));
        }
        catch (InvalidOperationException ex)
        {
            return new ProductionActionInputVerdict(
                false, ex.Message, ["Timing"], [ex.Message],
                VoiceArtifactId: voice.ArtifactId);
        }
        return new ProductionActionInputVerdict(
            true, null, [], [],
            VoiceArtifactId: voice.ArtifactId);
    }

    public async Task<ShotProductionState> GetShotStateAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
    {
        var verdict = await EvaluateCreateVideoAsync(scope, claim, ct);
        var storyPresent = CreateVideoActionContractGate.IsPresent(verdict.Story);
        var actingPresent = CreateVideoActionContractGate.IsPresent(verdict.ActingBeat);
        var bindings = await _store.ListActiveBindingsAsync(scope.BuildId, scope.ShotCode, ct);
        var visible = bindings.FirstOrDefault(b => b.Role == ProductionBindingRoles.Visible);
        var acceptedTyped = bindings.FirstOrDefault(b => b.Role == ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.VideoTake));
        var accepted = acceptedTyped ?? bindings.FirstOrDefault(b => b.Role == ProductionBindingRoles.Accepted);
        var current = bindings.FirstOrDefault(b => b.Role == ProductionBindingRoles.Current)
            ?? bindings.FirstOrDefault(b => b.Role.StartsWith(ProductionBindingRoles.Current + ":", StringComparison.Ordinal));

        var nodes = new List<ShotNodeState>
        {
            new(ShotNodeIds.Story, storyPresent ? ShotNodeStatuses.Available : ShotNodeStatuses.Missing, null, null, storyPresent ? null : ProductionFailureReasons.MissingStory),
            new(ShotNodeIds.Acting, actingPresent ? ShotNodeStatuses.Available : ShotNodeStatuses.Missing, null, null, actingPresent ? null : ProductionFailureReasons.MissingActingBeat),
            await NodeAsync(scope, ShotNodeIds.Keyframe, ProductionArtifactRoles.Keyframe, bindings, ct),
            await NodeAsync(scope, ShotNodeIds.Motion, ProductionArtifactRoles.VideoTake, bindings, ct),
            await NodeAsync(scope, ShotNodeIds.Voice, ProductionArtifactRoles.Voice, bindings, ct),
            await NodeAsync(scope, ShotNodeIds.Lipsync, ProductionArtifactRoles.Lipsync, bindings, ct),
            await NodeAsync(scope, ShotNodeIds.Mix, ProductionArtifactRoles.Mix, bindings, ct),
            new(ShotNodeIds.Accepted, accepted is null ? ShotNodeStatuses.Missing : ShotNodeStatuses.Accepted, accepted?.ArtifactId, null, null),
        };

        var voiceVerdict = await EvaluateCreateVoiceAsync(scope, claim, ct);
        var lipsyncVerdict = await EvaluateCreateLipsyncAsync(scope, claim, ct);
        var mixVerdict = await EvaluateCreateMixAsync(scope, claim, ct);
        var actions = NovixaShortProductionStrategyV1.Apply(
            BuildActions(storyPresent, nodes, verdict, voiceVerdict, lipsyncVerdict, mixVerdict),
            claim).ToList();
        if (TenantEpisodeDesk(scope))
        {
            var assembly = await _store.GetActiveBindingAsync(
                scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
            nodes.Add(new ShotNodeState(
                ShotNodeIds.Assembly,
                assembly is null ? ShotNodeStatuses.Missing : ShotNodeStatuses.Current,
                assembly?.ArtifactId,
                null,
                assembly is null ? ProductionFailureReasons.MissingDependency : null));
            actions.Add(await EvaluateTenantEpisodeAssemblyAsync(scope, ct));
        }
        var motion = nodes.First(n => n.Node == ShotNodeIds.Motion);
        var videoId = motion.ArtifactId ?? lipsyncVerdict.VideoArtifactId;
        var voiceId = lipsyncVerdict.VoiceArtifactId
            ?? nodes.First(n => n.Node == ShotNodeIds.Voice).ArtifactId;
        var lipsyncId = mixVerdict.LipsyncArtifactId
            ?? nodes.First(n => n.Node == ShotNodeIds.Lipsync).ArtifactId;
        return new ShotProductionState(
            scope, storyPresent, visible?.ArtifactId, current?.ArtifactId, accepted?.ArtifactId, nodes, actions,
            videoId, voiceId, lipsyncId);
    }

    public async Task<IReadOnlyList<ProductionActionAvailability>> GetAvailableActionsAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
        => (await GetShotStateAsync(scope, claim, ct)).Actions;

    public async Task<ProductionPreview> GetPreviewAsync(ProductionScope scope, CancellationToken ct = default)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        var preview = await _engine.ResolveVisiblePreviewAsync(scope.BuildId, scope.ShotCode, ct);
        if (!preview.IsEmpty && preview.ArtifactId is { } artId)
            await RequireArtifactInRequestScopeAsync(scope.BuildId, artId, ct);
        return preview;
    }

    public async Task<AuthoritativeVideoTake?> GetAuthoritativeVideoTakeAsync(ProductionScope scope, CancellationToken ct = default)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(_store, scope.BuildId, scope.ShotCode, ct);
        if (take is not null)
            await RequireArtifactInRequestScopeAsync(scope.BuildId, take.ArtifactId, ct);
        return take;
    }

    public async Task<ProductionVoiceResolution> GetCurrentVoiceAsync(ProductionScope scope, string lineId, CancellationToken ct = default)
    {
        await RequireKnownScopeAsync(scope.BuildId, ct);
        var voice = await _engine.ResolveCurrentVoiceArtifactAsync(scope.BuildId, scope.ShotCode, lineId, ct);
        if (!voice.IsEmpty && voice.ArtifactId is { } artId)
            await RequireArtifactInRequestScopeAsync(scope.BuildId, artId, ct);
        return voice;
    }

    public async Task<CreateVideoSliceResult> ExecuteCreateVideoFixtureAsync(CreateVideoSliceCommand command, CancellationToken ct = default)
    {
        var strategyBlock = NovixaShortProductionStrategyV1.PaidMotionBlock(command.Input);
        if (strategyBlock is not null)
            throw new InvalidOperationException(strategyBlock);
        if (_motion.IsPaid && _motion.RequiresNetwork)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        await RequireCreateVideoDependenciesAsync(command.Scope, command.Input, ct);

        var request = await CreateRequestAsync(command.Scope, ProductionActions.CreateVideo, command.CreatedBy, command.IdempotencyKey, ct: ct);
        var task = await CreateTaskAsync(request.RequestId, ct);
        var parents = command.Input.KeyframeArtifactId is { } kf ? new[] { kf } : command.Input.ParentArtifactIds;
        var input = command.Input with
        {
            ParentArtifactIds = parents,
            CompilerVersion = ShotExecutionCompiler.Version,
        };
        await FreezeInputAsync(task.TaskId, input, _motion.ProviderId, _motion.ModelId, ct);
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct) ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        var attempt = await StartAttemptAsync(task.TaskId, snap.SnapshotHash, ct);
        var started = await _motion.StartFrozenAsync(snap, ct);
        await SubmitProviderJobAsync(attempt.AttemptId, started.ProviderJobId, ct);
        var poll = await _motion.PollFrozenAsync(snap, started.ProviderJobId, ct);
        if (!string.Equals(poll.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal))
            poll = await _motion.RecoverFrozenAsync(snap, started.ProviderJobId, ct);
        if (!string.Equals(poll.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal))
            throw new InvalidOperationException(poll.FailureReason ?? ProductionFailureReasons.ProviderUnavailable);
        var executed = await _motion.FetchOutputAsync(snap, started.ProviderJobId, ct);
        if (executed.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        await CompleteAttemptAsync(attempt.AttemptId, KitVideoIntegrityRules.Sha256Hex(executed.Bytes), ct);
        var artifact = await VerifyArtifactAsync(attempt.AttemptId, executed.Bytes, executed.MimeType, executed.DurationSec, executed.StorageKey, ct);
        ProductionBinding? current = null;
        ProductionBinding? visible = null;
        if (command.BindCurrent)
            current = await TryBindProtectedAsync(() => BindCurrentProtectedAsync(artifact.ArtifactId, ct), ct);
        if (command.BindVisible)
            visible = await TryBindProtectedAsync(() => BindVisibleProtectedAsync(artifact.ArtifactId, ct), ct);
        return new CreateVideoSliceResult(request, task, attempt, artifact, current, visible);
    }

    public IReadOnlyList<string> RunRegression() => ProductionWorkExecutionV1Regression.Run();

    async Task RequireCreateVideoDependenciesAsync(ProductionScope scope, ShotExecutionInput input, CancellationToken ct)
    {
        var verdict = await EvaluateCreateVideoAsync(scope, CreateVideoActionContractGate.ClaimFrom(input), ct);
        if (!verdict.Available)
            throw new InvalidOperationException(verdict.BlockReason ?? ProductionFailureReasons.MissingDependency);
    }

    async Task<CreateVideoContractVerdict> BindCreateVideoSnapshotAsync(
        ProductionScope scope,
        CreateVideoReadinessClaim? claim,
        CreateVideoContractVerdict verdict,
        CancellationToken ct)
    {
        if (!ProductionPerformanceConsumption.HasSnapshotClaim(claim))
        {
            if (_performance is null || !ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId))
                return verdict;
            var osSnap = await _performance.GetByScopeAsync(scope.BuildId, scope.ShotCode, ct);
            if (osSnap is null || !ProductionDirectorTiming.HasDirectorTiming(osSnap.Source))
                return verdict;
            return verdict with
            {
                PerformanceSnapshotId = osSnap.PerformanceSnapshotId,
                PerformanceHash = osSnap.PerformanceHash,
            };
        }
        try
        {
            var bound = await ProductionPerformanceConsumption.TryBindClaimAsync(_performance, claim, scope, ct)
                ?? throw new InvalidOperationException(PerformanceContractIds.SnapshotNotFound);
            var derived = PerformanceActingProjection.FromDraft(bound.Draft);
            if (!verdict.Available)
                return verdict with
                {
                    PerformanceSnapshotId = bound.PerformanceSnapshotId,
                    PerformanceHash = bound.PerformanceHash,
                    DerivedActingBeat = derived,
                };
            return verdict with
            {
                Available = true,
                PerformanceSnapshotId = bound.PerformanceSnapshotId,
                PerformanceHash = bound.PerformanceHash,
                DerivedActingBeat = derived,
            };
        }
        catch (InvalidOperationException ex)
        {
            var missing = verdict.MissingInputs.Contains(ProductionPerformanceConsumption.RequiredInput)
                ? verdict.MissingInputs
                : verdict.MissingInputs.Append(ProductionPerformanceConsumption.RequiredInput).ToArray();
            return verdict with
            {
                Available = false,
                BlockReason = ex.Message,
                MissingInputs = missing,
                MissingReasons = missing.Select(m =>
                    m == ProductionPerformanceConsumption.RequiredInput ? ex.Message : CreateVideoActionContractGate.ReasonFor(m)).ToArray(),
            };
        }
    }

    async Task<ProductionTaskInputSnapshot?> SnapshotForArtifactAsync(Guid artifactId, CancellationToken ct)
    {
        var art = await _store.GetArtifactAsync(artifactId, ct);
        if (art is null) return null;
        return await _store.GetSnapshotByTaskAsync(art.TaskId, ct);
    }

    async Task<CreateVideoOsFacts> LoadCreateVideoOsFactsAsync(ProductionScope scope, CancellationToken ct)
    {
        var typed = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), ct);
        ProductionArtifact? kf = null;
        ProductionScope? ownerScope = null;
        if (typed is not null)
        {
            kf = await _store.GetArtifactAsync(typed.ArtifactId, ct);
            if (kf is not null)
            {
                var owner = await _store.GetTaskAsync(kf.TaskId, ct);
                ownerScope = owner?.Scope;
            }
        }

        var imageSnap = await LatestSnapshotAsync(scope, ProductionActions.CreateImage, ct);
        var videoSnap = await LatestSnapshotAsync(scope, ProductionActions.CreateVideo, ct);
        var director = await LoadDirectorTimingAsync(scope, ct);
        decimal duration;
        string? timing;
        if (director is { DurationSec: > 0 })
        {
            duration = director.DurationSec;
            timing = duration.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
        }
        else
        {
            duration = videoSnap is { DurationSec: > 0 } ? videoSnap.DurationSec
                : imageSnap is { DurationSec: > 0 } ? imageSnap.DurationSec
                : 0;
            timing = FirstPresent(RawTiming(videoSnap), RawTiming(imageSnap));
        }
        var speech = ProductionDirectorTiming.ApprovedSpeech(director);
        return new CreateVideoOsFacts(
            kf,
            ownerScope,
            FirstPresent(videoSnap?.Story, imageSnap?.Story),
            FirstPresent(videoSnap?.ActingBeat, imageSnap?.ActingBeat),
            FirstPresent(videoSnap?.VisualContract, imageSnap?.VisualContract),
            timing,
            duration,
            director is { DurationSec: > 0 },
            speech?.StartSec,
            speech?.EndSec);
    }

    async Task<DirectorTimingDecision?> LoadDirectorTimingAsync(ProductionScope scope, CancellationToken ct)
    {
        if (_performance is null) return null;
        var snap = await _performance.GetByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        if (snap is null || !ProductionDirectorTiming.HasDirectorTiming(snap.Source))
            return null;
        return new DirectorTimingDecision(
            snap.Source.ProductionDurationSec!.Value,
            snap.Source.TimingSource!,
            snap.Source.DecisionType!,
            ProductionDirectorTiming.Stage,
            snap.Source.SpeechStartSec,
            snap.Source.SpeechEndSec);
    }

    async Task<ProductionTaskInputSnapshot?> LatestSnapshotAsync(ProductionScope scope, string action, CancellationToken ct)
    {
        var tasks = await _store.ListTasksByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        var hit = tasks
            .Where(t => t.Action == action
                && t.InputSnapshotId is not null
                && t.Status != ProductionTaskStatuses.Superseded)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefault();
        return hit is null ? null : await _store.GetSnapshotByTaskAsync(hit.TaskId, ct);
    }

    static bool WhatInputsPresent(CreateVideoContractVerdict verdict) =>
        verdict.KeyframeArtifactId is not null
        && CreateVideoActionContractGate.IsPresent(verdict.Story)
        && CreateVideoActionContractGate.IsPresent(verdict.ActingBeat)
        && CreateVideoActionContractGate.IsPresent(verdict.VisualContract)
        && CreateVideoActionContractGate.IsValidTiming(verdict.Timing, verdict.DurationSec);

    static string? FirstPresent(string? left, string? right) =>
        CreateVideoActionContractGate.IsPresent(left) ? left!.Trim()
            : CreateVideoActionContractGate.IsPresent(right) ? right!.Trim()
            : null;

    static string? RawTiming(ProductionTaskInputSnapshot? snap)
    {
        if (snap is null) return null;
        if (snap.DurationSec > 0)
            return snap.DurationSec.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "s";
        return null;
    }

    async Task RequireKnownScopeAsync(Guid scopeId, CancellationToken ct)
    {
        if (_scopes is null) return;
        var kind = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(scopeId, ct), scopeId);
        ProductionScopeGuard.RequireKnown(kind);
    }

    async Task RequireArtifactInRequestScopeAsync(Guid requestScopeId, Guid artifactId, CancellationToken ct)
    {
        var artifact = await _store.GetArtifactAsync(artifactId, ct);
        if (artifact is null) return;
        await RequireArtifactOwnerScopeAsync(requestScopeId, artifact.TaskId, ct);
    }

    async Task RequireArtifactOwnerScopeAsync(Guid requestScopeId, Guid ownerTaskId, CancellationToken ct)
    {
        if (_scopes is null) return;
        var task = await _store.GetTaskAsync(ownerTaskId, ct);
        if (task is null) return;
        var request = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(requestScopeId, ct), requestScopeId);
        var owner = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(task.Scope.BuildId, ct), task.Scope.BuildId);
        ProductionScopeGuard.RequireSameScope(requestScopeId, request, task.Scope.BuildId, owner);
    }

    async Task<ProductionBinding> BindTypedAndAliasAsync(Guid artifactId, string aliasRole, bool protectNewerLineage, CancellationToken ct)
    {
        var artifact = await _store.GetArtifactAsync(artifactId, ct) ?? throw new InvalidOperationException("ARTIFACT_MISSING");
        if (protectNewerLineage)
        {
            await _engine.BindProtectedAsync(artifactId, ProductionBindingRoles.CurrentFor(artifact.Role), ct);
            return await _engine.BindProtectedAsync(artifactId, aliasRole, ct);
        }
        await _engine.BindAsync(artifactId, ProductionBindingRoles.CurrentFor(artifact.Role), ct);
        return await _engine.BindAsync(artifactId, aliasRole, ct);
    }

    async Task<ProductionBinding> BindTypedVisibleAsync(Guid artifactId, bool protectNewerLineage, CancellationToken ct)
    {
        var artifact = await _store.GetArtifactAsync(artifactId, ct) ?? throw new InvalidOperationException("ARTIFACT_MISSING");
        if (protectNewerLineage)
        {
            await _engine.BindProtectedAsync(artifactId, ProductionBindingRoles.VisibleFor(artifact.Role), ct);
            return await _engine.BindProtectedAsync(artifactId, ProductionBindingRoles.Visible, ct);
        }
        await _engine.BindAsync(artifactId, ProductionBindingRoles.VisibleFor(artifact.Role), ct);
        return await _engine.BindAsync(artifactId, ProductionBindingRoles.Visible, ct);
    }

    async Task<ProductionBinding?> TryBindProtectedAsync(Func<Task<ProductionBinding>> bind, CancellationToken ct)
    {
        try
        {
            return await bind();
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent)
        {
            _ = ct;
            return null;
        }
    }

    async Task<ProductionArtifact?> ResolveAcceptedTypedArtifactAsync(ProductionScope scope, string artifactRole, CancellationToken ct)
    {
        var typed = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.AcceptedFor(artifactRole), ct);
        if (typed is null) return null;
        return await _store.GetArtifactAsync(typed.ArtifactId, ct);
    }

    async Task<ProductionArtifact?> ResolveCurrentTypedArtifactAsync(ProductionScope scope, string artifactRole, CancellationToken ct)
    {
        var typed = await _store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(artifactRole), ct);
        if (typed is null && artifactRole == ProductionArtifactRoles.VideoTake)
        {
            var alias = await _store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.Current, ct);
            if (alias is not null)
            {
                var aliasArt = await _store.GetArtifactAsync(alias.ArtifactId, ct);
                if (aliasArt?.Role == ProductionArtifactRoles.VideoTake) typed = alias;
            }
        }
        if (typed is null) return null;
        return await _store.GetArtifactAsync(typed.ArtifactId, ct);
    }

    async Task RequireLipsyncParentIdentityAsync(ProductionScope scope, Guid artifactId, string artifactRole, CancellationToken ct)
    {
        var art = await _store.GetArtifactAsync(artifactId, ct)
            ?? throw new InvalidOperationException(
                artifactRole == ProductionArtifactRoles.Voice
                    ? ProductionFailureReasons.MissingVoiceArtifact
                    : artifactRole == ProductionArtifactRoles.Lipsync
                        ? ProductionFailureReasons.MissingLipsyncArtifact
                        : ProductionFailureReasons.MissingVideoArtifact);
        if (art.Role != artifactRole
            || art.VerificationStatus != ProductionArtifactStatuses.Verified
            || string.IsNullOrWhiteSpace(art.ByteSha256)
            || string.IsNullOrWhiteSpace(art.StorageKey)
            || art.StorageKey.Contains("://", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var task = await _store.GetTaskAsync(art.TaskId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (task.Scope.BuildId != scope.BuildId || !string.Equals(task.Scope.ShotCode, scope.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(
                task.Scope.BuildId != scope.BuildId
                    ? ProductionFailureReasons.CrossScopeForbidden
                    : ProductionFailureReasons.CrossShotForbidden);
        await RequireArtifactInRequestScopeAsync(scope.BuildId, artifactId, ct);
    }

    async Task<ShotNodeState> NodeAsync(ProductionScope scope, string node, string artifactRole, IReadOnlyList<ProductionBinding> bindings, CancellationToken ct)
    {
        if (artifactRole == ProductionArtifactRoles.VideoTake)
        {
            var resolved = await OfficialVideoAuthorityBridgeV1.ResolveVideoTakeAsync(_store, scope.BuildId, scope.ShotCode, ct);
            if (resolved.FailClosed)
                return new ShotNodeState(node, ShotNodeStatuses.Missing, null, null, resolved.Reason ?? ProductionFailureReasons.OutputArtifactNotFound);
            if (resolved.Hit is null || resolved.Hit.ArtifactRole != ProductionArtifactRoles.VideoTake)
                return new ShotNodeState(node, ShotNodeStatuses.Missing, null, null, ProductionFailureReasons.MissingDependency);
            var takeArt = await _store.GetArtifactAsync(resolved.Hit.ArtifactId, ct);
            var takeTask = takeArt is null ? null : await _store.GetTaskAsync(takeArt.TaskId, ct);
            var takeStale = takeTask?.Status == ProductionTaskStatuses.Superseded;
            var status = resolved.Hit.BindingRole == OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole
                ? ShotNodeStatuses.Accepted
                : takeStale ? ShotNodeStatuses.Stale : ShotNodeStatuses.Current;
            return new ShotNodeState(node, status, resolved.Hit.ArtifactId, resolved.Hit.ByteSha256, takeStale ? takeTask?.FailureCode : null);
        }
        var typed = bindings.FirstOrDefault(b => b.Role == ProductionBindingRoles.CurrentFor(artifactRole));
        var alias = artifactRole == ProductionArtifactRoles.VideoTake
            ? bindings.FirstOrDefault(b => b.Role == ProductionBindingRoles.Current)
            : null;
        var hit = typed ?? alias;
        if (hit is null)
            return new ShotNodeState(node, ShotNodeStatuses.Missing, null, null, ProductionFailureReasons.MissingDependency);
        var art = await _store.GetArtifactAsync(hit.ArtifactId, ct);
        var task = art is null ? null : await _store.GetTaskAsync(art.TaskId, ct);
        var stale = task?.Status == ProductionTaskStatuses.Superseded;
        return new ShotNodeState(
            node,
            stale ? ShotNodeStatuses.Stale : ShotNodeStatuses.Current,
            hit.ArtifactId,
            art?.ByteSha256,
            stale ? task?.FailureCode : null);
    }

    static IReadOnlyList<ProductionActionAvailability> BuildActions(
        bool storyPresent,
        IReadOnlyList<ShotNodeState> nodes,
        CreateVideoContractVerdict createVideo,
        ProductionActionInputVerdict createVoice,
        ProductionActionInputVerdict createLipsync,
        ProductionActionInputVerdict createMix)
    {
        var motion = nodes.First(n => n.Node == ShotNodeIds.Motion);
        var voice = nodes.First(n => n.Node == ShotNodeIds.Voice);
        var lipsync = nodes.First(n => n.Node == ShotNodeIds.Lipsync);
        bool Has(ShotNodeState n) => n.Status is ShotNodeStatuses.Current or ShotNodeStatuses.Available or ShotNodeStatuses.Accepted;
        _ = storyPresent;
        _ = lipsync;

        return
        [
            Avail(ProductionActions.CreateImage, ProductionActionContractRegistry.CreateImage, true, null, null),
            Avail(ProductionActions.CreateVideo, ProductionActionContractRegistry.CreateVideo,
                createVideo.Available,
                createVideo.Available ? null : createVideo.BlockReason,
                createVideo.Available ? null : createVideo.MissingInputs),
            Avail(ProductionActions.CreateVoice, ProductionActionContractRegistry.CreateVoice,
                createVoice.Available,
                createVoice.Available ? null : createVoice.BlockReason,
                createVoice.Available ? null : createVoice.MissingInputs),
            Avail(ProductionActions.CreateLipsync, ProductionActionContractRegistry.CreateLipsync,
                createLipsync.Available && Has(motion) && Has(voice),
                createLipsync.Available && Has(motion) && Has(voice)
                    ? null
                    : createLipsync.BlockReason ?? ProductionFailureReasons.MissingDependency,
                createLipsync.Available ? null : createLipsync.MissingInputs),
            Avail(ProductionActions.MixVideo, ProductionActionContractRegistry.MixVideo,
                createMix.Available,
                createMix.Available ? null : createMix.BlockReason ?? ProductionFailureReasons.MissingLipsyncArtifact,
                createMix.Available ? null : createMix.MissingInputs),
            Avail(ProductionActions.Accept, ProductionActionContractRegistry.Accept,
                Has(motion) || Has(lipsync) || Has(nodes.First(n => n.Node == ShotNodeIds.Mix)),
                null, null),
            Avail(ProductionActions.Regenerate, ProductionActionContractRegistry.Regenerate, Has(motion), null, null),
            Avail(ProductionActions.Assemble, ProductionActionContractRegistry.Assemble,
                Has(nodes.First(n => n.Node == ShotNodeIds.Accepted)),
                Has(nodes.First(n => n.Node == ShotNodeIds.Accepted)) ? null : ProductionFailureReasons.MissingDependency, null),
        ];
    }

    static bool TenantEpisodeDesk(ProductionScope scope)
    {
        if (scope.BuildId == Guid.Empty || ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId))
            return false;
        var episode = (scope.EpisodeCode ?? "").Trim();
        var shot = (scope.ShotCode ?? "").Trim();
        if (episode.Length > 0 && !string.Equals(episode, "EP01", StringComparison.Ordinal))
            return false;
        return shot == ProductionEpisodeAssemblyContract.EpisodeShotCode
            || shot.StartsWith("EP01-", StringComparison.Ordinal);
    }

    async Task<ProductionActionAvailability> EvaluateTenantEpisodeAssemblyAsync(ProductionScope scope, CancellationToken ct)
    {
        var contract = ProductionActionContractRegistry.AssembleEpisode;
        foreach (var shot in ProductionEpisodeAssemblyContract.OfficialEp01Order)
        {
            var current = await _store.GetActiveBindingAsync(
                scope.BuildId, shot, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
            var visible = await _store.GetActiveBindingAsync(
                scope.BuildId, shot, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly), ct);
            if (current is null || visible is null || current.ArtifactId != visible.ArtifactId)
                return Avail(ProductionActions.AssembleEpisode, contract, false, ProductionFailureReasons.MissingDependency, null);
            var art = await _store.GetArtifactAsync(current.ArtifactId, ct);
            if (art is null
                || art.Role != ProductionArtifactRoles.Assembly
                || art.VerificationStatus != ProductionArtifactStatuses.Verified
                || art.DurationSec <= 0
                || string.IsNullOrWhiteSpace(art.StorageKey)
                || string.IsNullOrWhiteSpace(art.ByteSha256))
                return Avail(ProductionActions.AssembleEpisode, contract, false, ProductionFailureReasons.FailedIdentity, null);
        }
        return Avail(ProductionActions.AssembleEpisode, contract, true, null, null);
    }

    static ProductionActionAvailability Avail(string action, ProductionActionContract contract, bool ok, string? reason, IReadOnlyList<string>? missing) =>
        new(action, contract.UiCta, ok, reason, contract.RequiredInputs, missing);

    static HashSet<string> StaleActionsFor(string changedNode) => changedNode switch
    {
        ShotNodeIds.Story or ShotNodeIds.Acting =>
            [ProductionActions.CreateVideo, ProductionActions.CreateVoice, ProductionActions.CreateLipsync, ProductionActions.MixVideo],
        ShotNodeIds.Keyframe =>
            [ProductionActions.CreateVideo, ProductionActions.CreateLipsync, ProductionActions.MixVideo],
        ShotNodeIds.Motion => [ProductionActions.CreateLipsync, ProductionActions.MixVideo],
        ShotNodeIds.Voice => [ProductionActions.CreateLipsync, ProductionActions.MixVideo],
        ShotNodeIds.Lipsync => [ProductionActions.MixVideo],
        _ => [],
    };
}
