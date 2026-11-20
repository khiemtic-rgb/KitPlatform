using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public interface IProductionReadinessAuthority
{
    Task<ProductionAiSelection> UpsertSelectionAsync(UpsertProductionAiSelectionCommand command, CancellationToken ct = default);
    Task<ProductionAiSelectionSupersedeResult> SupersedeSelectionAsync(
        SupersedeProductionAiSelectionCommand command, CancellationToken ct = default);
    Task<ProductionAiSelection?> GetSelectionAsync(Guid buildId, string shotCode, string stage, CancellationToken ct = default);
    Task<ProductionExecutionPermit> IssuePermitAsync(IssueProductionExecutionPermitCommand command, CancellationToken ct = default);
    Task<ProductionExecutionPermit?> GetIssuedPermitAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
    Task<ProductionExecutionPermit?> GetLatestPermitAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
    Task<bool> TryConsumePermitAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default);
    CreateVideoInputPreview Preview(
        ProductionScope scope,
        CreateVideoContractVerdict verdict,
        ProductionAiSelection? selection,
        ProductionExecutionPermit? permit);
    Task<PerformanceSnapshot> MaterializeDirectorTimingAsync(
        MaterializeDirectorTimingCommand command,
        CancellationToken ct = default);
    Task<VoiceProductionIngressResult> MaterializeVoiceProductionIngressAsync(
        MaterializeVoiceProductionIngressCommand command,
        CancellationToken ct = default);
    Task<VoiceProductionIngressResult> MaterializeSh02VoiceProductionIngressAsync(
        MaterializeVoiceProductionIngressCommand command,
        CancellationToken ct = default);
    Task<ProductionExecutionPermit> MaterializeSh02LipsyncProductionIngressAsync(
        MaterializeLipsyncProductionIngressCommand command,
        CancellationToken ct = default);
}

public sealed record MaterializeLipsyncProductionIngressCommand(
    ProductionScope Scope,
    string CreatedBy);

public sealed record MaterializeVoiceProductionIngressCommand(
    ProductionScope Scope,
    string CreatedBy);

public sealed record VoiceProductionIngressResult(
    PerformanceSnapshot Snapshot,
    Guid? ParentSnapshotId,
    ProductionAiSelection Selection,
    ProductionExecutionPermit Permit);

public sealed record MaterializeDirectorTimingCommand(
    ProductionScope Scope,
    decimal DurationSec,
    string Story,
    string ActingBeat,
    string VisualContract,
    Guid KeyframeArtifactId,
    string KeyframeSha256,
    string CreatedBy,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);

public sealed record CreateVideoInputPreview(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    Guid? KeyframeArtifactId,
    string KeyframeSha256,
    string Story,
    string ActingBeat,
    string VisualContract,
    string Timing,
    decimal DurationSec,
    string TimingStatus,
    Guid? PerformanceSnapshotId,
    string PerformanceHash,
    string ProviderId,
    string ModelId,
    Guid? SelectionId,
    string SelectionMode,
    Guid? PermitId,
    string PermitStatus,
    string PermitScope,
    bool CreateVideoAvailable,
    string? BlockReason,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);

public sealed class ProductionReadinessAuthority : IProductionReadinessAuthority
{
    readonly IProductionAiSelectionStore _selections;
    readonly IProductionExecutionPermitStore _permits;
    readonly IPerformanceContractService? _performance;
    readonly IPerformanceSnapshotStore? _snapshots;

    public ProductionReadinessAuthority(
        IProductionAiSelectionStore selections,
        IProductionExecutionPermitStore permits,
        IPerformanceContractService? performance = null,
        IPerformanceSnapshotStore? snapshots = null)
    {
        _selections = selections;
        _permits = permits;
        _performance = performance;
        _snapshots = snapshots;
    }

    public Task<ProductionAiSelection> UpsertSelectionAsync(UpsertProductionAiSelectionCommand command, CancellationToken ct = default)
        => _selections.UpsertActiveAsync(command, ct);

    public Task<ProductionAiSelectionSupersedeResult> SupersedeSelectionAsync(
        SupersedeProductionAiSelectionCommand command, CancellationToken ct = default)
        => _selections.SupersedeActiveAsync(command, ct);

    public Task<ProductionAiSelection?> GetSelectionAsync(Guid buildId, string shotCode, string stage, CancellationToken ct = default)
        => _selections.GetActiveAsync(buildId, shotCode, stage, ct);

    public Task<ProductionExecutionPermit> IssuePermitAsync(IssueProductionExecutionPermitCommand command, CancellationToken ct = default)
        => _permits.IssueAsync(command, ct);

    public Task<ProductionExecutionPermit?> GetIssuedPermitAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
        => _permits.GetIssuedAsync(buildId, shotCode, action, providerId, modelId, ct);

    public Task<ProductionExecutionPermit?> GetLatestPermitAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
        => _permits.GetLatestAsync(buildId, shotCode, action, providerId, modelId, ct);

    public Task<bool> TryConsumePermitAsync(
        Guid buildId, string shotCode, string action, string providerId, string modelId, CancellationToken ct = default)
        => _permits.TryConsumeAsync(buildId, shotCode, action, providerId, modelId, ct);

    public CreateVideoInputPreview Preview(
        ProductionScope scope,
        CreateVideoContractVerdict verdict,
        ProductionAiSelection? selection,
        ProductionExecutionPermit? permit)
    {
        var timingStatus = CreateVideoActionContractGate.IsValidTiming(verdict.Timing, verdict.DurationSec)
            ? "AUTHORITATIVE"
            : ProductionFailureReasons.TimingProductionDecisionRequired;
        var provider = selection?.ProviderId ?? verdict.ProviderId;
        var model = selection?.ModelId ?? verdict.ModelId;
        return new CreateVideoInputPreview(
            scope.BuildId,
            scope.EpisodeCode,
            scope.SceneCode,
            scope.ShotCode,
            verdict.KeyframeArtifactId,
            verdict.KeyframeByteSha256,
            verdict.Story,
            verdict.ActingBeat,
            verdict.VisualContract,
            verdict.Timing,
            verdict.DurationSec,
            timingStatus,
            verdict.PerformanceSnapshotId,
            verdict.PerformanceHash,
            provider,
            model,
            selection?.SelectionId,
            selection?.SelectionMode ?? "",
            permit?.PermitId,
            permit?.Status ?? "",
            permit is null
                ? ""
                : $"{permit.BuildId:D}/{permit.ShotCode}/{permit.Action}/{permit.ProviderId}/{permit.ModelId}",
            verdict.Available,
            verdict.BlockReason,
            verdict.SpeechStartSec,
            verdict.SpeechEndSec);
    }

    public async Task<PerformanceSnapshot> MaterializeDirectorTimingAsync(
        MaterializeDirectorTimingCommand command,
        CancellationToken ct = default)
    {
        if (_performance is null || _snapshots is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (command.DurationSec <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.TimingProductionDecisionRequired);
        if (command.KeyframeArtifactId == Guid.Empty
            || !CreateVideoActionContractGate.IsPresent(command.Story)
            || !CreateVideoActionContractGate.IsPresent(command.ActingBeat)
            || !CreateVideoActionContractGate.IsPresent(command.VisualContract))
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);

        var suppliedSpeech = command.SpeechStartSec is not null || command.SpeechEndSec is not null;
        var approvedSpeech = ProductionDirectorTiming.TryApprovedSpeech(
            command.SpeechStartSec, command.SpeechEndSec, out var speechStart, out var speechEnd);
        if (suppliedSpeech && !approvedSpeech)
            throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);
        var extra = ProductionDirectorTiming.ExtraJson(
            command.DurationSec, command.CreatedBy,
            approvedSpeech ? speechStart : null,
            approvedSpeech ? speechEnd : null);
        var timingHash = approvedSpeech
            ? ProductionDirectorTiming.TimingHash(
                command.Scope.BuildId, command.Scope.ShotCode, command.DurationSec, speechStart, speechEnd)
            : ProductionDirectorTiming.TimingHash(
                command.Scope.BuildId, command.Scope.ShotCode, command.DurationSec);
        var storyHash = "STORY_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(command.Story));
        var visualHash = "VISUAL_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(command.VisualContract));
        var contract = ShortProductionQualityPipelineV1.Ep01Contract(command.Scope.ShotCode);
        var lineId = ProductionScopeAnchorRules.IsTenantOwnedEp01Build(command.Scope.BuildId, command.Scope.EpisodeCode)
            ? ShortProductionQualityPipelineV1.Ep01LineId(command.Scope.ShotCode)
            : null;
        IReadOnlyList<string> lineIds = [];
        IReadOnlyList<LineProjectionSource> lines = [];
        if (contract is { DirectDialogue: true, LineComplete: true }
            && !string.IsNullOrWhiteSpace(contract.SpokenLine)
            && !string.IsNullOrWhiteSpace(lineId))
        {
            lineIds = [lineId];
            lines = [new LineProjectionSource(lineId, contract.SpeakerId, contract.SpokenLine, "neutral", Locked: true)];
        }
        var source = new PerformanceProjectionSource(
            command.Scope.BuildId,
            command.Scope.ShotCode,
            command.Scope.EpisodeCode,
            command.Scope.SceneCode,
            ["CHAR-003", "CHAR-001"],
            lineIds,
            lines,
            new ActingBeatSource(BeforeAction: command.ActingBeat, BeforeGaze: "Linh looks toward Minh."),
            "Linh looks toward Minh.",
            PrimaryCharacterIds: ["CHAR-003"],
            SecondaryCharacterIds: ["CHAR-001"],
            TimingHash: timingHash,
            VisualContractHash: visualHash,
            StoryTextHash: storyHash,
            BeatId: "acting:" + command.Scope.ShotCode);
        var draft = _performance.CreateDraft(source);
        var sourceRef = PerformanceContractRules.SourceOf(source, draft) with
        {
            ProductionDurationSec = command.DurationSec,
            TimingSource = ProductionDirectorTiming.Source,
            DecisionType = ProductionDirectorTiming.DecisionType,
            KeyframeArtifactId = command.KeyframeArtifactId,
            SpeechStartSec = approvedSpeech ? speechStart : null,
            SpeechEndSec = approvedSpeech ? speechEnd : null,
        };
        return await _performance.FreezeAsync(draft, command.CreatedBy, sourceRef, extra, ct);
    }

    public async Task<VoiceProductionIngressResult> MaterializeVoiceProductionIngressAsync(
        MaterializeVoiceProductionIngressCommand command,
        CancellationToken ct = default)
    {
        if (_performance is null || _snapshots is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        var scope = command.Scope;
        if (scope.BuildId != ProductionEp01Sh01VoiceIngressCanon.Scope().BuildId
            || !string.Equals(scope.ShotCode, ProductionEp01Sh01VoiceIngressCanon.Scope().ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(PerformanceContractIds.CrossShotForbidden);

        var parent = await _snapshots.GetByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        if (parent is null || !ProductionDirectorTiming.HasDirectorTiming(parent.Source))
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (parent.Source.KeyframeArtifactId is null || parent.Source.KeyframeArtifactId == Guid.Empty)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);

        var duration = parent.Source.ProductionDurationSec ?? 0;
        if (duration != ProductionDirectorTiming.Ep01Sh01DurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.TimingProductionDecisionRequired);

        var extra = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["durationSec"] = duration,
            ["source"] = ProductionDirectorTiming.Source,
            ["decisionType"] = ProductionDirectorTiming.DecisionType,
            ["stage"] = ProductionDirectorTiming.Stage,
            ["selectedBy"] = (command.CreatedBy ?? "").Trim(),
            ["sourceType"] = ProductionEp01Sh01VoiceIngressCanon.SourceType,
            ["sourceField"] = ProductionEp01Sh01VoiceIngressCanon.SourceField,
            ["sourceLineId"] = ProductionEp01Sh01VoiceIngressCanon.LineId,
            ["parentSnapshotId"] = parent.PerformanceSnapshotId.ToString("D"),
        });
        if (!ProductionDirectorTiming.IsExplicitDecision(extra))
            throw new InvalidOperationException(ProductionFailureReasons.TimingProductionDecisionRequired);

        var timingHash = ProductionDirectorTiming.TimingHash(scope.BuildId, scope.ShotCode, duration);
        var storyHash = "STORY_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(ProductionEp01Sh01IngressCanon.Story));
        var visualHash = "VISUAL_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(ProductionEp01Sh01IngressCanon.VisualContract));
        var source = new PerformanceProjectionSource(
            scope.BuildId,
            scope.ShotCode,
            scope.EpisodeCode,
            scope.SceneCode,
            ["CHAR-003", "CHAR-001"],
            [ProductionEp01Sh01VoiceIngressCanon.LineId],
            [new LineProjectionSource(
                ProductionEp01Sh01VoiceIngressCanon.LineId,
                ProductionEp01Sh01VoiceIngressCanon.SpeakerCharacterId,
                ProductionEp01Sh01VoiceIngressCanon.LineText,
                ProductionEp01Sh01VoiceIngressCanon.LineEmotion,
                Locked: true)],
            new ActingBeatSource(BeforeAction: ProductionEp01Sh01IngressCanon.ActingBeat, BeforeGaze: "Linh looks toward Minh."),
            "Linh looks toward Minh.",
            PrimaryCharacterIds: ["CHAR-003"],
            SecondaryCharacterIds: ["CHAR-001"],
            TimingHash: timingHash,
            VisualContractHash: visualHash,
            StoryTextHash: storyHash,
            BeatId: "acting:" + scope.ShotCode);
        var draft = _performance.CreateDraft(source);
        if (!string.Equals(draft.Lines[0].TextIdentity.Text, ProductionEp01Sh01VoiceIngressCanon.LineText, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        var sourceRef = PerformanceContractRules.SourceOf(source, draft) with
        {
            ProductionDurationSec = duration,
            TimingSource = ProductionDirectorTiming.Source,
            DecisionType = ProductionDirectorTiming.DecisionType,
            KeyframeArtifactId = parent.Source.KeyframeArtifactId,
        };
        var snap = await _performance.FreezeAsync(draft, command.CreatedBy ?? "", sourceRef, extra, ct);

        var selection = await _selections.UpsertActiveAsync(new UpsertProductionAiSelectionCommand(
            scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionAiSelectionStages.Voice,
            ProductionEp01Sh01VoiceIngressCanon.ProviderId,
            ProductionEp01Sh01VoiceIngressCanon.ModelId,
            command.CreatedBy,
            ProductionVoiceSelectionCanon.ReasonOf(
                ProductionEp01Sh01VoiceIngressCanon.VoiceId,
                ProductionEp01Sh01VoiceIngressCanon.LineId,
                ProductionEp01Sh01VoiceIngressCanon.SpeakerCharacterId,
                ProductionEp01Sh01VoiceIngressCanon.SourceField),
            ProductionEp01Sh01VoiceIngressCanon.VoiceId), ct);

        var permit = await _permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionActions.CreateVoice,
            ProductionEp01Sh01VoiceIngressCanon.ProviderId,
            ProductionEp01Sh01VoiceIngressCanon.ModelId,
            command.CreatedBy,
            ProductionVoiceSelectionCanon.ReasonOf(
                ProductionEp01Sh01VoiceIngressCanon.VoiceId,
                ProductionEp01Sh01VoiceIngressCanon.LineId,
                ProductionEp01Sh01VoiceIngressCanon.SpeakerCharacterId,
                ProductionEp01Sh01VoiceIngressCanon.SourceField)));

        return new VoiceProductionIngressResult(snap, parent.PerformanceSnapshotId, selection, permit);
    }

    public async Task<VoiceProductionIngressResult> MaterializeSh02VoiceProductionIngressAsync(
        MaterializeVoiceProductionIngressCommand command,
        CancellationToken ct = default)
    {
        if (_performance is null || _snapshots is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        var scope = command.Scope;
        if (!ProductionEp01Sh02VoiceIngressCanon.MatchesScope(scope))
            throw new InvalidOperationException(PerformanceContractIds.CrossShotForbidden);
        if (string.IsNullOrWhiteSpace(ProductionEp01Sh02VoiceIngressCanon.LineId)
            || string.IsNullOrWhiteSpace(ProductionEp01Sh02VoiceIngressCanon.LineText)
            || string.IsNullOrWhiteSpace(ProductionEp01Sh02VoiceIngressCanon.VoiceId)
            || string.IsNullOrWhiteSpace(ProductionEp01Sh02VoiceIngressCanon.ProviderId)
            || ProductionEp01Sh02VoiceIngressCanon.IsSh01VoiceId(ProductionEp01Sh02VoiceIngressCanon.VoiceId))
            throw new InvalidOperationException(ProductionFailureReasons.VoiceContractMissing);

        var parent = await _snapshots.GetByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        if (parent is null || !ProductionDirectorTiming.HasDirectorTiming(parent.Source))
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (parent.Source.KeyframeArtifactId is null || parent.Source.KeyframeArtifactId == Guid.Empty)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);

        var duration = parent.Source.ProductionDurationSec ?? 0;
        if (duration != ProductionEp01Sh02VoiceIngressCanon.DurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.TimingProductionDecisionRequired);

        var extra = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["durationSec"] = duration,
            ["source"] = ProductionDirectorTiming.Source,
            ["decisionType"] = ProductionDirectorTiming.DecisionType,
            ["stage"] = ProductionDirectorTiming.Stage,
            ["selectedBy"] = (command.CreatedBy ?? "").Trim(),
            ["sourceType"] = ProductionEp01Sh02VoiceIngressCanon.SourceType,
            ["sourceField"] = ProductionEp01Sh02VoiceIngressCanon.SourceField,
            ["sourceLineId"] = ProductionEp01Sh02VoiceIngressCanon.LineId,
            ["parentSnapshotId"] = parent.PerformanceSnapshotId.ToString("D"),
        });
        if (!ProductionDirectorTiming.IsExplicitDecision(extra))
            throw new InvalidOperationException(ProductionFailureReasons.TimingProductionDecisionRequired);

        var timingHash = ProductionDirectorTiming.TimingHash(scope.BuildId, scope.ShotCode, duration);
        var storyHash = "STORY_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(ProductionEp01Sh02IngressCanon.Story));
        var visualHash = "VISUAL_HASH_V1:" + KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(ProductionEp01Sh02IngressCanon.VisualContract));
        var source = new PerformanceProjectionSource(
            scope.BuildId,
            scope.ShotCode,
            scope.EpisodeCode,
            scope.SceneCode,
            [ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId, ProductionEp01Sh02VoiceIngressCanon.ListenerCharacterId],
            [ProductionEp01Sh02VoiceIngressCanon.LineId],
            [new LineProjectionSource(
                ProductionEp01Sh02VoiceIngressCanon.LineId,
                ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId,
                ProductionEp01Sh02VoiceIngressCanon.LineText,
                ProductionEp01Sh02VoiceIngressCanon.LineEmotion,
                Locked: true)],
            new ActingBeatSource(BeforeAction: ProductionEp01Sh02IngressCanon.ActingBeat, BeforeGaze: "Minh looks toward Linh."),
            "Minh looks toward Linh.",
            PrimaryCharacterIds: [ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId],
            SecondaryCharacterIds: [ProductionEp01Sh02VoiceIngressCanon.ListenerCharacterId],
            TimingHash: timingHash,
            VisualContractHash: visualHash,
            StoryTextHash: storyHash,
            BeatId: "acting:" + scope.ShotCode);
        var draft = _performance.CreateDraft(source);
        if (draft.Lines.Count != 1
            || !string.Equals(draft.Lines[0].TextIdentity.Text, PerformanceContractRules.SpokenText(ProductionEp01Sh02VoiceIngressCanon.LineText), StringComparison.Ordinal)
            || !string.Equals(draft.Lines[0].LineId, ProductionEp01Sh02VoiceIngressCanon.LineId, StringComparison.Ordinal)
            || !string.Equals(draft.Lines[0].CharacterId, ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        var sourceRef = PerformanceContractRules.SourceOf(source, draft) with
        {
            ProductionDurationSec = duration,
            TimingSource = ProductionDirectorTiming.Source,
            DecisionType = ProductionDirectorTiming.DecisionType,
            KeyframeArtifactId = parent.Source.KeyframeArtifactId,
        };
        var snap = await _performance.FreezeAsync(draft, command.CreatedBy ?? "", sourceRef, extra, ct);

        var selection = await _selections.UpsertActiveAsync(new UpsertProductionAiSelectionCommand(
            scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionAiSelectionStages.Voice,
            ProductionEp01Sh02VoiceIngressCanon.ProviderId,
            ProductionEp01Sh02VoiceIngressCanon.ModelId,
            command.CreatedBy,
            ProductionVoiceSelectionCanon.ReasonOf(
                ProductionEp01Sh02VoiceIngressCanon.VoiceId,
                ProductionEp01Sh02VoiceIngressCanon.LineId,
                ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId,
                ProductionEp01Sh02VoiceIngressCanon.SourceField),
            ProductionEp01Sh02VoiceIngressCanon.VoiceId), ct);

        var permit = await _permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionActions.CreateVoice,
            ProductionEp01Sh02VoiceIngressCanon.ProviderId,
            ProductionEp01Sh02VoiceIngressCanon.ModelId,
            command.CreatedBy,
            ProductionVoiceSelectionCanon.ReasonOf(
                ProductionEp01Sh02VoiceIngressCanon.VoiceId,
                ProductionEp01Sh02VoiceIngressCanon.LineId,
                ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId,
                ProductionEp01Sh02VoiceIngressCanon.SourceField)));

        return new VoiceProductionIngressResult(snap, parent.PerformanceSnapshotId, selection, permit);
    }

    public async Task<ProductionExecutionPermit> MaterializeSh02LipsyncProductionIngressAsync(
        MaterializeLipsyncProductionIngressCommand command,
        CancellationToken ct = default)
    {
        var scope = command.Scope;
        if (!ProductionEp01Sh02LipsyncCanon.MatchesScope(scope))
            throw new InvalidOperationException(PerformanceContractIds.CrossShotForbidden);
        var reason = ProductionEp01Sh02LipsyncCanon.ExecutionAuthority(
            Guid.Empty, "",
            ProductionEp01Sh02LipsyncCanon.VideoArtifactId,
            ProductionEp01Sh02LipsyncCanon.VideoSha256,
            ProductionEp01Sh02LipsyncCanon.VoiceArtifactId,
            ProductionEp01Sh02LipsyncCanon.VoiceSha256);
        var existing = await _permits.GetIssuedAsync(
            scope.BuildId, scope.ShotCode, ProductionActions.CreateLipsync,
            ProductionEp01Sh02LipsyncCanon.ProviderId, ProductionEp01Sh02LipsyncCanon.ModelId, ct);
        if (existing is not null) return existing;
        return await _permits.IssueAsync(new IssueProductionExecutionPermitCommand(
            scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionActions.CreateLipsync,
            ProductionEp01Sh02LipsyncCanon.ProviderId,
            ProductionEp01Sh02LipsyncCanon.ModelId,
            command.CreatedBy,
            reason), ct);
    }
}
