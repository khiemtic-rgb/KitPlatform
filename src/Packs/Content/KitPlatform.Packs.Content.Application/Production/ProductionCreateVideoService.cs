using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public interface IProductionCreateVideoService
{
    Task<ProductionArtifact> RegisterKeyframeAsync(RegisterKeyframeCommand command, CancellationToken ct = default);
    Task<ProductionArtifact> RegisterVoiceBytesAsync(RegisterVoiceBytesCommand command, CancellationToken ct = default);
    Task<ProductionArtifact> RegisterExistingVideoTakeAsync(RegisterExistingVideoTakeCommand command, CancellationToken ct = default);
    Task<ProductionArtifact> RegisterExistingVoiceAsync(RegisterExistingVoiceCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> CreateVideoAsync(CreateVideoApiCommand command, CancellationToken ct = default);
    Task<CreateVideoContractVerdict> EvaluateCreateVideoAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<ShotProductionState> GetShotStateAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionActionAvailability>> GetActionsAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<ProductionPreview> GetPreviewAsync(ProductionScope scope, CancellationToken ct = default);
    Task<AuthoritativeVideoTake?> GetAuthoritativeVideoTakeAsync(ProductionScope scope, CancellationToken ct = default);
    Task<byte[]?> ReadPreviewMediaAsync(ProductionScope scope, CancellationToken ct = default);
    Task<ProductionPreview> GetCurrentKeyframeAsync(ProductionScope scope, CancellationToken ct = default);
    Task<byte[]?> ReadCurrentKeyframeMediaAsync(ProductionScope scope, CancellationToken ct = default);
    Task<ProductionVoiceResolution> GetCurrentVoiceAsync(ProductionScope scope, string lineId, CancellationToken ct = default);
    Task<byte[]?> ReadCurrentVoiceMediaAsync(ProductionScope scope, string lineId, CancellationToken ct = default);
    Task<ProductionRequest?> GetRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<ProductionTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default);
    Task<CreateVideoPrepareResult> PrepareCreateVideoAsync(CreateVideoApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> ExecutePreparedCreateVideoAsync(Guid attemptId, CreateVideoApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> CreateImageAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> CreateVoiceAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> CreateLipsyncAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> RecoverOfficialExistingLipsyncAsync(Guid attemptId, string providerJobId, CancellationToken ct = default);
    Task<ProductionRequest> CloseEmptyOfficialLipsyncRequestAsync(string idempotencyKey, CancellationToken ct = default);
    Task<CreateVideoSliceResult> MixVideoAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> AcceptOsAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<CreateVideoSliceResult> RegenerateOsAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<FilmAssemblySliceResult> AssembleOsAsync(ProductionActionApiCommand command, CancellationToken ct = default);
    Task<FilmAssemblySliceResult> AssembleEpisodeOsAsync(ProductionActionApiCommand command, CancellationToken ct = default);
}

public sealed record ProductionActionApiCommand(
    ProductionScope Scope,
    ShotExecutionInput Input,
    string CreatedBy,
    string? IdempotencyKey,
    byte[]? MediaBytes = null,
    string? MediaMime = null,
    IReadOnlyList<(string ShotCode, Guid ArtifactId, decimal DurationSec)>? AssemblyShots = null,
    bool AuthorizePaidNetwork = false,
    ProductionCredentialReference? Credential = null,
    IReadOnlyDictionary<string, ShortQualityVerdict>? QualityVerdicts = null,
    string? ImageSize = null);

public sealed record FilmAssemblySliceResult(
    ProductionRequest Request,
    ProductionTask Task,
    ProductionAttempt Attempt,
    FilmAssembly Assembly,
    IReadOnlyList<Guid> AcceptedArtifactIds,
    ProductionArtifact? Output = null);

public sealed record RegisterVoiceBytesCommand(
    ProductionScope Scope,
    byte[] AudioBytes,
    string MimeType,
    string Story,
    string CreatedBy);

public sealed record RegisterKeyframeCommand(
    ProductionScope Scope,
    byte[] ImageBytes,
    string MimeType,
    string Story,
    string CreatedBy,
    string ActingBeat = "",
    string VisualContract = "",
    string Timing = "",
    decimal DurationSec = 0,
    string? PictureRevisionId = null,
    string? IngressProvenanceJson = null);

public sealed record RegisterExistingVideoTakeCommand(
    ProductionScope Scope,
    Guid SourceArtifactId,
    string CreatedBy);

public sealed record RegisterExistingVoiceCommand(
    ProductionScope Scope,
    Guid SourceArtifactId,
    string CreatedBy);

public sealed record CreateVideoApiCommand(
    ProductionScope Scope,
    ShotExecutionInput Input,
    string CreatedBy,
    string? IdempotencyKey,
    byte[]? KeyframeBytes,
    string? KeyframeMime,
    string? ProviderId = null,
    string? ModelId = null,
    bool RetryAttempt = false,
    bool AuthorizePaidNetwork = false,
    bool PrepareOnly = false,
    Guid? PreparedAttemptId = null,
    ProductionCredentialReference? Credential = null);

public sealed record CreateVideoPrepareResult(
    ProductionRequest Request,
    ProductionTask Task,
    ProductionTaskInputSnapshot Snapshot,
    ProductionAttempt Attempt,
    ProductionCreateVideoPreflight Preflight,
    ProductionCostDecision Cost);

public sealed partial class ProductionCreateVideoService : IProductionCreateVideoService
{
    readonly IProductionExecutionService _os;
    readonly IProductionWorkStore _store;
    readonly IProductionArtifactFileStore _files;
    readonly IMotionExecutionAdapter _motion;
    readonly IProductionScopeFoundationStore? _scopes;
    readonly IProductionMotionAdapterResolver _resolver;
    readonly IProductionPaidNetworkPermit? _permit;
    readonly IProductionExecutionPermitStore? _officialPermits;
    readonly IImageExecutionAdapter _image;
    readonly IVoiceExecutionAdapter _voice;
    readonly IVoiceExecutionAdapter? _officialVoice;
    readonly ILipSyncExecutionAdapter _lipsync;
    readonly ILipSyncExecutionAdapter? _officialLipsync;
    readonly IMixExecutionAdapter _mix;
    readonly IMixExecutionAdapter? _officialMix;
    readonly IAssemblyExecutionAdapter _assembly;
    readonly FfmpegAssemblyExecutionAdapter? _officialAssembly;
    readonly FfmpegEpisodeAssemblyExecutionAdapter? _officialEpisode;
    readonly IUnattributedVideoTakeSource? _historicalVideoTakes;
    readonly IUnattributedVoiceSource? _historicalVoices;
    readonly IProductionTargetShotCheck? _targetShots;
    readonly GeminiImageExecutionAdapter? _geminiImage;
    readonly IProductionTenantAiResolution? _tenantAi;
    readonly TenantGeminiImageExecutionAdapter? _tenantImage;
    readonly IPerformanceSnapshotStore? _performanceSnapshots;
    readonly IProductionLockedMasterSource? _lockedMasters;
    readonly IContentSeriesPilotService? _seriesBuilds;

    public ProductionCreateVideoService(
        IProductionExecutionService os,
        IProductionWorkStore store,
        IProductionArtifactFileStore files,
        IMotionExecutionAdapter motion,
        IProductionScopeFoundationStore? scopes = null,
        IProductionMotionAdapterResolver? resolver = null,
        IProductionPaidNetworkPermit? permit = null,
        IImageExecutionAdapter? image = null,
        IVoiceExecutionAdapter? voice = null,
        ILipSyncExecutionAdapter? lipsync = null,
        IMixExecutionAdapter? mix = null,
        IAssemblyExecutionAdapter? assembly = null,
        IProductionExecutionPermitStore? officialPermits = null,
        ElevenLabsVoiceExecutionAdapter? officialVoice = null,
        FalLipSyncExecutionAdapter? officialLipsync = null,
        FfmpegMixExecutionAdapter? officialMix = null,
        FfmpegAssemblyExecutionAdapter? officialAssembly = null,
        FfmpegEpisodeAssemblyExecutionAdapter? officialEpisode = null,
        IUnattributedVideoTakeSource? historicalVideoTakes = null,
        IUnattributedVoiceSource? historicalVoices = null,
        IProductionTargetShotCheck? targetShots = null,
        GeminiImageExecutionAdapter? geminiImage = null,
        IProductionTenantAiResolution? tenantAi = null,
        TenantGeminiImageExecutionAdapter? tenantImage = null,
        IPerformanceSnapshotStore? performanceSnapshots = null,
        IProductionLockedMasterSource? lockedMasters = null,
        IContentSeriesPilotService? seriesBuilds = null)
    {
        _tenantAi = tenantAi;
        _tenantImage = tenantImage;
        _performanceSnapshots = performanceSnapshots;
        _lockedMasters = lockedMasters;
        _seriesBuilds = seriesBuilds;
        _os = os;
        _store = store;
        _files = files;
        _motion = motion;
        _scopes = scopes;
        _resolver = resolver ?? new ProductionMotionAdapterResolver([motion]);
        _permit = permit;
        _officialPermits = officialPermits;
        _image = image ?? new ImportImageAdapter();
        _voice = voice ?? new FixtureVoiceAdapter();
        _officialVoice = officialVoice;
        _lipsync = lipsync ?? new FixtureLipSyncAdapter();
        _officialLipsync = officialLipsync;
        _mix = mix ?? new FixtureMixAdapter();
        _officialMix = officialMix;
        _assembly = assembly ?? new FixtureAssemblyAdapter();
        _officialAssembly = officialAssembly;
        _officialEpisode = officialEpisode;
        _historicalVideoTakes = historicalVideoTakes;
        _historicalVoices = historicalVoices;
        _targetShots = targetShots;
        _geminiImage = geminiImage;
    }

    public async Task<ProductionArtifact> RegisterKeyframeAsync(RegisterKeyframeCommand command, CancellationToken ct = default)
    {
        if (command.ImageBytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var sha = KitVideoIntegrityRules.Sha256Hex(command.ImageBytes);
        var idem = $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:CREATE_IMAGE:{sha}";
        var existingReq = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existingReq is not null)
        {
            var prior = await ExistingArtifactAsync(existingReq.RequestId, ProductionArtifactRoles.Keyframe, ct);
            if (prior is not null) return prior;
        }

        var request = await _os.CreateRequestAsync(command.Scope, ProductionActions.CreateImage, command.CreatedBy, idem, ct: ct);
        var task = await _os.CreateTaskAsync(request.RequestId, ct);
        var acting = string.IsNullOrWhiteSpace(command.ActingBeat) ? "keyframe" : command.ActingBeat.Trim();
        var visual = string.IsNullOrWhiteSpace(command.VisualContract) ? "visual" : command.VisualContract.Trim();
        var timing = string.IsNullOrWhiteSpace(command.Timing) ? "0" : command.Timing.Trim();
        var provenance = (command.IngressProvenanceJson ?? "").Trim();
        if (provenance.Length == 0 && !string.IsNullOrWhiteSpace(command.PictureRevisionId))
            provenance = command.PictureRevisionId.Trim();
        var input = new ShotExecutionInput(
            command.Story, acting, visual, provenance, sha, timing, "", "",
            null, sha, "16:9", command.DurationSec, ShotExecutionCompiler.Version, null);
        await _os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels", ct);
        var attempt = await _os.StartAttemptAsync(task.TaskId, sha, ct);
        var storage = _files.Write(await StorageFolderAsync(command.Scope, keyframe: true, ct), attempt.AttemptId.ToString("N"), command.ImageBytes);
        await _os.SubmitProviderJobAsync(attempt.AttemptId, "import-" + sha[..12], ct);
        await _os.CompleteAttemptAsync(attempt.AttemptId, sha, ct);
        var artifact = await _os.VerifyArtifactAsync(attempt.AttemptId, command.ImageBytes, command.MimeType, 0, storage, ct);
        await _os.BindNamedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(artifact.Role), ct);
        var current = await _store.GetActiveBindingAsync(
            command.Scope.BuildId, command.Scope.ShotCode, ProductionBindingRoles.Current, ct);
        var currentArtifact = current is null ? null : await _store.GetArtifactAsync(current.ArtifactId, ct);
        var laterMediaHoldsCurrent = currentArtifact is not null
            && !string.Equals(currentArtifact.Role, ProductionArtifactRoles.Keyframe, StringComparison.Ordinal);
        if (!laterMediaHoldsCurrent)
            await _os.BindCurrentAsync(artifact.ArtifactId, ct);
        return artifact;
    }

    public async Task<ProductionArtifact> RegisterVoiceBytesAsync(RegisterVoiceBytesCommand command, CancellationToken ct = default)
    {
        if (command.Scope.BuildId == Guid.Empty || !IsProductionShot(command.Scope.ShotCode))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId))
            throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
        if (_targetShots is not null)
            await _targetShots.RequireAsync(command.Scope.BuildId, command.Scope.ShotCode, ct);
        if (!ProductionVoicePhysical.TryMeasureDuration(command.AudioBytes, command.MimeType, out var duration))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var sha = KitVideoIntegrityRules.Sha256Hex(command.AudioBytes);
        var idem = $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:IMPORT_VOICE:{sha}";
        var existingReq = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existingReq is not null)
        {
            var prior = await ExistingArtifactAsync(existingReq.RequestId, ProductionArtifactRoles.Voice, ct);
            if (prior is not null) return prior;
        }

        var request = await _os.CreateRequestAsync(command.Scope, ProductionActions.CreateVoice, command.CreatedBy, idem, ct: ct);
        var task = await _os.CreateTaskAsync(request.RequestId, ct);
        var story = string.IsNullOrWhiteSpace(command.Story) ? "voice" : command.Story.Trim();
        var input = new ShotExecutionInput(
            story, "existing-audio", "existing-audio", "", sha, duration.ToString("0.###"), "", "",
            null, sha, "16:9", duration, ShotExecutionCompiler.Version, null);
        await _os.FreezeInputAsync(task.TaskId, input, "import", "existing-audio", ct);
        var attempt = await _os.StartAttemptAsync(task.TaskId, sha, ct);
        var storage = _files.Write(await VoiceFolderAsync(command.Scope, ct), attempt.AttemptId.ToString("N"), command.AudioBytes);
        await _os.SubmitProviderJobAsync(attempt.AttemptId, "import-" + sha[..12], ct);
        await _os.CompleteAttemptAsync(attempt.AttemptId, sha, ct);
        var artifact = await _os.VerifyArtifactAsync(attempt.AttemptId, command.AudioBytes, command.MimeType, duration, storage, ct);
        await _os.BindNamedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(artifact.Role), ct);
        var current = await _store.GetActiveBindingAsync(
            command.Scope.BuildId, command.Scope.ShotCode, ProductionBindingRoles.Current, ct);
        var currentArtifact = current is null ? null : await _store.GetArtifactAsync(current.ArtifactId, ct);
        var laterMediaHoldsCurrent = currentArtifact is not null
            && !string.Equals(currentArtifact.Role, ProductionArtifactRoles.Voice, StringComparison.Ordinal);
        if (!laterMediaHoldsCurrent)
            await _os.BindCurrentAsync(artifact.ArtifactId, ct);
        return artifact;
    }

    public async Task<ProductionArtifact> RegisterExistingVideoTakeAsync(
        RegisterExistingVideoTakeCommand command, CancellationToken ct = default)
    {
        if (command.Scope.BuildId == Guid.Empty
            || command.SourceArtifactId == Guid.Empty
            || string.IsNullOrWhiteSpace(command.Scope.ShotCode))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (!string.Equals((command.Scope.EpisodeCode ?? "").Trim(), "EP01", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (_historicalVideoTakes is null)
            throw new InvalidOperationException(ProductionFailureReasons.OutputArtifactNotFound);

        var idem = $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:RECOVER_VIDEO_TAKE:{command.SourceArtifactId:N}";
        var existingReq = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existingReq is not null)
        {
            var prior = await ExistingArtifactAsync(existingReq.RequestId, ProductionArtifactRoles.VideoTake, ct);
            if (prior is not null) return prior;
            throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
        }

        var source = await _historicalVideoTakes.ReadAsync(command.SourceArtifactId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.OutputArtifactNotFound);
        if (source.ArtifactId != command.SourceArtifactId
            || !string.Equals(source.Role, ProductionArtifactRoles.VideoTake, StringComparison.Ordinal)
            || source.Bytes is null
            || source.Bytes.Length < 32
            || source.ByteSha256.Length != 64
            || !string.Equals(KitVideoIntegrityRules.Sha256Hex(source.Bytes), source.ByteSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.MimeType, ProductionTestMp4.Mime, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var sha = KitVideoIntegrityRules.Sha256Hex(source.Bytes);
        var story = "Recovered existing VIDEO_TAKE";
        var provenance = RecoverVideoTakeProvenance(source);
        var timing = source.DurationSec > 0 ? source.DurationSec.ToString("0.###") : "0";
        var input = new ShotExecutionInput(
            story, "existing-media", "existing-media", "",
            sha, timing, "", provenance,
            null, sha, "16:9", source.DurationSec > 0 ? source.DurationSec : 0,
            ShotExecutionCompiler.Version, null);
        var request = await _os.CreateRequestAsync(command.Scope, ProductionActions.CreateVideo, command.CreatedBy, idem, ct: ct);
        var task = await _os.CreateTaskAsync(request.RequestId, ct);
        await _os.FreezeInputAsync(task.TaskId, input, "import", "existing-video", ct, requireCreateVideoDependencies: false);
        var attempt = await _os.StartAttemptAsync(task.TaskId, sha, ct);
        var storage = _files.Write(await StorageFolderAsync(command.Scope, keyframe: false, ct), attempt.AttemptId.ToString("N"), source.Bytes);
        await _os.SubmitProviderJobAsync(attempt.AttemptId, "import-" + sha[..12], ct);
        await _os.CompleteAttemptAsync(attempt.AttemptId, sha, ct);
        var artifact = await _os.VerifyArtifactAsync(attempt.AttemptId, source.Bytes, ProductionTestMp4.Mime, source.DurationSec, storage, ct);
        await _os.BindCurrentAsync(artifact.ArtifactId, ct);
        return artifact;
    }

    public async Task<ProductionArtifact> RegisterExistingVoiceAsync(
        RegisterExistingVoiceCommand command, CancellationToken ct = default)
    {
        if (command.Scope.BuildId == Guid.Empty
            || command.SourceArtifactId == Guid.Empty
            || !IsProductionShot(command.Scope.ShotCode))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId))
            throw new InvalidOperationException(ProductionTenantBoundary.Unattributed);
        if (!string.Equals((command.Scope.EpisodeCode ?? "").Trim(), "EP01", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (_historicalVoices is null)
            throw new InvalidOperationException(ProductionFailureReasons.OutputArtifactNotFound);
        if (_targetShots is not null)
            await _targetShots.RequireAsync(command.Scope.BuildId, command.Scope.ShotCode, ct);

        var idem = $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:RECOVER_VOICE:{command.SourceArtifactId:N}";
        var existingReq = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existingReq is not null)
        {
            var prior = await ExistingArtifactAsync(existingReq.RequestId, ProductionArtifactRoles.Voice, ct);
            if (prior is not null) return prior;
            throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
        }

        var source = await _historicalVoices.ReadAsync(command.SourceArtifactId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.OutputArtifactNotFound);
        var sha = source.Bytes is null ? "" : KitVideoIntegrityRules.Sha256Hex(source.Bytes);
        if (source.ArtifactId != command.SourceArtifactId
            || !string.Equals(source.Role, ProductionArtifactRoles.Voice, StringComparison.Ordinal)
            || source.Bytes is null
            || !ProductionVoicePhysical.IsMpegMime(source.MimeType)
            || !ProductionVoicePhysical.LooksLikeMpeg(source.Bytes)
            || source.ByteSha256.Length != 64
            || !string.Equals(sha, source.ByteSha256, StringComparison.OrdinalIgnoreCase)
            || source.DurationSec <= 0
            || string.IsNullOrWhiteSpace(source.Story)
            || string.IsNullOrWhiteSpace(source.LineId))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);

        var provenance = RecoverVoiceProvenance(source);
        var timing = source.DurationSec.ToString("0.###");
        var input = new ShotExecutionInput(
            source.Story.Trim(), "existing-media", "existing-media", "",
            sha, timing, "", provenance,
            null, sha, "16:9", source.DurationSec,
            ShotExecutionCompiler.Version, null);
        var request = await _os.CreateRequestAsync(command.Scope, ProductionActions.CreateVoice, command.CreatedBy, idem, ct: ct);
        var task = await _os.CreateTaskAsync(request.RequestId, ct);
        await _os.FreezeInputAsync(task.TaskId, input, "import", "existing-voice", ct);
        var attempt = await _os.StartAttemptAsync(task.TaskId, sha, ct);
        var storage = _files.Write(await VoiceFolderAsync(command.Scope, ct), attempt.AttemptId.ToString("N"), source.Bytes);
        await _os.SubmitProviderJobAsync(attempt.AttemptId, "import-" + sha[..12], ct);
        await _os.CompleteAttemptAsync(attempt.AttemptId, sha, ct);
        var artifact = await _os.VerifyArtifactAsync(
            attempt.AttemptId, source.Bytes, ProductionVoicePhysical.MpegMime, source.DurationSec, storage, ct);
        await _os.BindNamedAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice), ct);
        return artifact;
    }

    static bool IsProductionShot(string? shotCode)
    {
        var text = shotCode ?? "";
        if (text.Length == 0 || text != text.Trim()) return false;
        var parts = text.Split('-');
        if (parts.Length != 3) return false;
        return Coded(parts[0], "EP") && Coded(parts[1], "SC") && Coded(parts[2], "SH");
    }

    static bool Coded(string part, string prefix) =>
        part.Length > prefix.Length
        && part.StartsWith(prefix, StringComparison.Ordinal)
        && part[prefix.Length..].All(char.IsDigit);

    static string RecoverVoiceProvenance(UnattributedVoice source)
    {
        static string Clean(string? value)
        {
            var text = (value ?? "").Trim();
            return text.Replace("\\", "", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal);
        }

        return "{\"op\":\"RECOVER_VOICE\""
            + ",\"sourceArtifactId\":\"" + source.ArtifactId.ToString("D") + "\""
            + ",\"sourceRole\":\"" + Clean(source.Role) + "\""
            + ",\"sourceProvider\":\"" + Clean(source.ProviderId) + "\""
            + ",\"sourceModel\":\"" + Clean(source.ModelId) + "\""
            + ",\"sourceSha256\":\"" + Clean(source.ByteSha256) + "\""
            + ",\"lineId\":\"" + Clean(source.LineId) + "\"}";
    }

    static string RecoverVideoTakeProvenance(UnattributedVideoTake source)
    {
        static string Clean(string? value)
        {
            var text = (value ?? "").Trim();
            return text.Replace("\\", "", StringComparison.Ordinal).Replace("\"", "", StringComparison.Ordinal);
        }

        return "{\"op\":\"RECOVER_VIDEO_TAKE\""
            + ",\"sourceArtifactId\":\"" + source.ArtifactId.ToString("D") + "\""
            + ",\"sourceRole\":\"" + Clean(source.Role) + "\""
            + ",\"sourceProvider\":\"" + Clean(source.ProviderId) + "\""
            + ",\"sourceModel\":\"" + Clean(source.ModelId) + "\""
            + ",\"sourceSha256\":\"" + Clean(source.ByteSha256) + "\"}";
    }

    public async Task<CreateVideoSliceResult> CreateVideoAsync(CreateVideoApiCommand command, CancellationToken ct = default)
    {
        var strategyBlock = NovixaShortProductionStrategyV1.PaidMotionBlock(command.Input);
        if (strategyBlock is not null)
            throw new InvalidOperationException(strategyBlock);
        if (command.PreparedAttemptId is { } preparedId)
            return await ExecutePreparedCreateVideoAsync(preparedId, command, ct);
        var pictureRefusal = await PictureRefusalAsync(command.Scope, ct);
        if (pictureRefusal is not null)
            throw new InvalidOperationException(pictureRefusal);
        var (providerId, modelId, adapter, credential) = await BindCreateVideoMotionAsync(command, ct);
        if (adapter is null)
        {
            var blocked = await _os.EvaluateCreateVideoAsync(
                command.Scope, CreateVideoActionContractGate.ClaimFrom(command.Input), ct);
            if (blocked.BlockReason == FamixaEp01UpstreamHold.MotionBlocked)
                await RecordStageHoldAsync(command.Scope, blocked.KeyframeArtifactId, ProductionActions.CreateVideo, blocked.BlockReason, ct);
            throw new InvalidOperationException(blocked.BlockReason ?? ProductionFailureReasons.MissingProviderModel);
        }
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var prior = await _store.GetRequestByIdempotencyAsync(command.IdempotencyKey.Trim(), ct);
            if (prior is not null)
            {
                var live = ProjectReplayInput(command.Scope.ShotCode, command.Input);
                if (command.RetryAttempt)
                    return await RetryCreateVideoAsync(prior, live, adapter, ct);
                var existing = await ReplayOrResumeCreateVideoAsync(prior.RequestId, live, adapter, ct);
                if (existing is not null) return existing;
            }
        }
        if (adapter.IsPaid && ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId))
            await RequireOfficialIssuedPermitAsync(command.Scope, adapter, ct);
        var tenantIssuedPermitMatches = false;
        if (adapter.IsPaid && adapter.RequiresNetwork
            && ProductionPaidNetworkGate.IsTenantEp01PaidVideo(command.Scope.BuildId, command.Scope.EpisodeCode))
        {
            if (_tenantAi is null || !await _tenantAi.GovernsAsync(command.Scope, ct))
                throw new InvalidOperationException(ProductionTenantAiReasons.ConfigurationRequired);
            if (credential is null)
                throw new InvalidOperationException(ProductionTenantAiReasons.CredentialRequired);
            if (!command.AuthorizePaidNetwork || _officialPermits is null)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            var issued = await _officialPermits.GetIssuedAsync(
                command.Scope.BuildId, command.Scope.ShotCode, ProductionActions.CreateVideo,
                adapter.ProviderId, adapter.ModelId, ct);
            tenantIssuedPermitMatches = issued is not null
                && ProductionExecutionPermitCanon.Matches(
                    issued, command.Scope.BuildId, command.Scope.ShotCode,
                    ProductionActions.CreateVideo, adapter.ProviderId, adapter.ModelId);
            if (!tenantIssuedPermitMatches)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        RequirePaidNetworkAuthorization(command, adapter);
        if (adapter.IsPaid && adapter.RequiresNetwork && command.PreparedAttemptId is null
            && !ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(
                command.Scope.BuildId, command.Scope.EpisodeCode,
                command.AuthorizePaidNetwork, tenantIssuedPermitMatches))
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        if (_scopes is not null)
        {
            var kind = ProductionScopeGuard.Resolve(await _scopes.GetAnchorAsync(command.Scope.BuildId, ct), command.Scope.BuildId);
            if (kind == ProductionScopeKind.Test && command.KeyframeBytes is { Length: > 0 })
                throw new InvalidOperationException(ProductionFailureReasons.UrlAuthorityForbidden);
        }

        var kf = command.Input.KeyframeArtifactId is { } id
            ? await _store.GetArtifactAsync(id, ct)
            : null;
        if (kf is null && command.KeyframeBytes is { Length: >= 32 })
        {
            kf = await RegisterKeyframeAsync(new RegisterKeyframeCommand(
                command.Scope, command.KeyframeBytes, command.KeyframeMime ?? "image/png",
                command.Input.Story, command.CreatedBy), ct);
        }
        var claim = CreateVideoActionContractGate.ClaimFrom(command.Input) with
        {
            ProviderId = providerId,
            ModelId = modelId,
        };
        var verdict = await _os.EvaluateCreateVideoAsync(command.Scope, claim, ct);
        if (!verdict.Available)
        {
            if (verdict.BlockReason == FamixaEp01UpstreamHold.MotionBlocked)
                await RecordStageHoldAsync(command.Scope, verdict.KeyframeArtifactId, ProductionActions.CreateVideo, verdict.BlockReason, ct);
            throw new InvalidOperationException(verdict.BlockReason ?? ProductionFailureReasons.MissingDependency);
        }

        var duration = verdict.DurationSec > 0 ? verdict.DurationSec : command.Input.DurationSec;
        if (adapter.IsPaid
            && ProductionPaidNetworkGate.IsTenantEp01PaidVideo(command.Scope.BuildId, command.Scope.EpisodeCode)
            && duration >= 8)
            duration = 5;
        ProductionMotionCostGuard.RequireAllowed(adapter.ProviderId, adapter.ModelId, duration, adapter.IsPaid);

        var input = command.Input with
        {
            Story = verdict.Story,
            ActingBeat = CreateVideoActionContractGate.IsPresent(verdict.DerivedActingBeat) ? verdict.DerivedActingBeat : verdict.ActingBeat,
            VisualContract = verdict.VisualContract,
            Timing = verdict.Timing,
            DurationSec = duration,
            KeyframeArtifactId = verdict.KeyframeArtifactId,
            KeyframeByteSha256 = verdict.KeyframeByteSha256,
            KeyframeHash = verdict.KeyframeByteSha256,
            ParentArtifactIds = [verdict.KeyframeArtifactId!.Value],
            CompilerVersion = ShotExecutionCompiler.Version,
            PerformanceSnapshotId = verdict.PerformanceSnapshotId ?? command.Input.PerformanceSnapshotId,
            PerformanceHash = CreateVideoActionContractGate.IsPresent(verdict.PerformanceHash) ? verdict.PerformanceHash : command.Input.PerformanceHash,
        };
        input = ApplyFutureVideoSpec(command.Scope.ShotCode, input);
        var snapHash = ShotExecutionCompiler.Hash(string.Join('|',
            input.Story, input.ActingBeat, input.VisualContract, verdict.KeyframeByteSha256, input.Timing, providerId, modelId,
            input.PerformanceSnapshotId?.ToString("D") ?? "", input.PerformanceHash ?? ""));
        var idem = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:CREATE_VIDEO:{snapHash}"
            : command.IdempotencyKey.Trim();

        var existingReq = await _store.GetRequestByIdempotencyAsync(idem, ct);
        if (existingReq is not null)
        {
            if (command.RetryAttempt)
                return await RetryCreateVideoAsync(existingReq, input, adapter, ct);
            var replay = await ReplayOrResumeCreateVideoAsync(existingReq.RequestId, input, adapter, ct);
            if (replay is not null) return replay;
            throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
        }

        var request = await _os.CreateRequestAsync(command.Scope, ProductionActions.CreateVideo, command.CreatedBy, idem, ct: ct);
        if (request.UserAction != ProductionActions.CreateVideo)
            throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden);
        var already = await _store.ListTasksByRequestAsync(request.RequestId, ct);
        if (already.Count > 0)
        {
            if (command.RetryAttempt)
                return await RetryCreateVideoAsync(request, input, adapter, ct);
            var raced = await ReplayOrResumeCreateVideoAsync(request.RequestId, input, adapter, ct);
            if (raced is not null) return raced;
            throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
        }

        var task = await _os.CreateTaskAsync(request.RequestId, ct);
        await _os.FreezeInputAsync(task.TaskId, input, providerId, modelId, ct, credential);
        return await RunAttemptLifecycleAsync(request, task, adapter, ct);
    }

    public async Task<CreateVideoPrepareResult> PrepareCreateVideoAsync(CreateVideoApiCommand command, CancellationToken ct = default)
    {
        var strategyBlock = NovixaShortProductionStrategyV1.PaidMotionBlock(command.Input);
        if (strategyBlock is not null)
            throw new InvalidOperationException(strategyBlock);
        var planned = await PlanCreateVideoAsync(command, ct);
        RequirePaidNetworkAuthorization(command, planned.Adapter);
        if (planned.Adapter.IsPaid && planned.Adapter.RequiresNetwork)
        {
            var resolved = ProductionMotionCostGuard.Evaluate(
                planned.Adapter.ProviderId, planned.Adapter.ModelId, planned.Duration, planned.Adapter.IsPaid);
            if (!resolved.Allowed)
                throw new InvalidOperationException(resolved.BlockReason ?? ProductionFailureReasons.UnknownCost);
        }

        var existingReq = await _store.GetRequestByIdempotencyAsync(planned.Idem, ct);
        if (existingReq is not null)
        {
            var tasks = await _store.ListTasksByRequestAsync(existingReq.RequestId, ct);
            var task = tasks.LastOrDefault() ?? throw new InvalidOperationException("TASK_MISSING");
            var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct) ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
            var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
            var attempt = attempts.LastOrDefault() ?? await _os.StartAttemptAsync(task.TaskId, snap.SnapshotHash, ct);
            var preflight = await BuildPreflightAsync(command.Scope, planned, task, snap, attempt, ct);
            ProductionCreateVideoPreflightRules.RequireComplete(preflight);
            return new CreateVideoPrepareResult(existingReq, task, snap, attempt, preflight, planned.Cost);
        }

        var request = await _os.CreateRequestAsync(command.Scope, ProductionActions.CreateVideo, command.CreatedBy, planned.Idem, ct: ct);
        var created = await _os.CreateTaskAsync(request.RequestId, ct);
        await _os.FreezeInputAsync(created.TaskId, planned.Input, planned.Adapter.ProviderId, planned.Adapter.ModelId, ct, planned.Credential);
        var snapshot = await _store.GetSnapshotByTaskAsync(created.TaskId, ct) ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        var started = await _os.StartAttemptAsync(created.TaskId, snapshot.SnapshotHash, ct);
        var frozenTask = await _store.GetTaskAsync(created.TaskId, ct) ?? created;
        var row = await BuildPreflightAsync(command.Scope, planned, frozenTask, snapshot, started, ct);
        ProductionCreateVideoPreflightRules.RequireComplete(row);
        return new CreateVideoPrepareResult(request, frozenTask, snapshot, started, row, planned.Cost);
    }

    public async Task<CreateVideoSliceResult> ExecutePreparedCreateVideoAsync(
        Guid attemptId, CreateVideoApiCommand command, CancellationToken ct = default)
    {
        var strategyBlock = NovixaShortProductionStrategyV1.PaidMotionBlock(command.Input);
        if (strategyBlock is not null)
            throw new InvalidOperationException(strategyBlock);
        var attempt = await _store.GetAttemptAsync(attemptId, ct) ?? throw new InvalidOperationException("ATTEMPT_MISSING");
        var task = await _store.GetTaskAsync(attempt.TaskId, ct) ?? throw new InvalidOperationException("TASK_MISSING");
        var request = await _store.GetRequestAsync(task.RequestId, ct) ?? throw new InvalidOperationException("REQUEST_MISSING");
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct) ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        var adapter = _resolver.Resolve(snap.ProviderId, snap.ModelId);
        if (!string.IsNullOrWhiteSpace(attempt.ProviderJobId) || task.Status == ProductionTaskStatuses.Succeeded)
        {
            if (task.Status == ProductionTaskStatuses.Succeeded)
            {
                var replay = await ReplayOrResumeCreateVideoAsync(
                    request.RequestId, ProjectReplayInput(command.Scope.ShotCode, command.Input), adapter, ct);
                if (replay is not null) return replay;
            }
            if (!string.IsNullOrWhiteSpace(attempt.ProviderJobId))
                return await FinishExistingAttemptAsync(request, task, snap, attempt, adapter, ct);
        }
        var planned = await PlanCreateVideoAsync(command, ct);
        RequirePaidNetworkAuthorization(command, planned.Adapter);
        AssertFrozenMatch(snap, planned.Input, planned.Adapter);
        var preflight = await BuildPreflightAsync(command.Scope, planned, task, snap, attempt, ct);
        ProductionCreateVideoPreflightRules.RequireComplete(preflight);
        return await RunAttemptLifecycleAsync(request, task, planned.Adapter, ct, reuseAttempt: attempt);
    }

    public Task<CreateVideoContractVerdict> EvaluateCreateVideoAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
        => _os.EvaluateCreateVideoAsync(scope, claim, ct);

    public async Task<ShotProductionState> GetShotStateAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
    {
        var state = await _os.GetShotStateAsync(scope, claim, ct);
        var refusal = await PictureRefusalAsync(scope, ct);
        if (refusal is not null)
        {
            state = state with
            {
                Nodes = state.Nodes.Select(node => DownstreamOfPicture(node.Node)
                    ? node with { Status = ShotNodeStatuses.Stale, Reason = refusal }
                    : node).ToArray(),
                VisibleArtifactId = null,
                AcceptedArtifactId = null,
            };
        }
        var actions = await AdjustImageAvailabilityAsync(scope, state.Actions, ct);
        if (actions != state.Actions)
            state = state with { Actions = actions };
        if (ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode))
            state = state with { PaidStageLedger = await SummarizePaidStagesAsync(scope, ct) };
        return state;
    }

    public async Task<IReadOnlyList<ProductionActionAvailability>> GetActionsAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default)
    {
        var actions = await _os.GetAvailableActionsAsync(scope, claim, ct);
        return await AdjustImageAvailabilityAsync(scope, actions, ct);
    }

    async Task<string?> PictureRefusalAsync(ProductionScope scope, CancellationToken ct)
    {
        if (_lockedMasters is null) return null;
        var episode = string.IsNullOrWhiteSpace(scope.EpisodeCode)
            && scope.ShotCode.StartsWith("EP01-", StringComparison.Ordinal)
            ? "EP01"
            : scope.EpisodeCode;
        if (!ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, episode))
            return null;
        if (string.Equals(scope.ShotCode, "EP01-SC01-SH03", StringComparison.Ordinal))
            return null;
        var frame = await GetCurrentKeyframeAsync(scope, ct);
        if (!frame.IsEmpty) return null;
        return frame.Reason is ProductionLockedStillReference.Stale
            or ProductionLockedStillReference.Missing
            or FamixaEp01UpstreamHold.MotionBlocked
            ? frame.Reason
            : null;
    }

    async Task<IReadOnlyList<ProductionActionAvailability>> AdjustImageAvailabilityAsync(
        ProductionScope scope, IReadOnlyList<ProductionActionAvailability> actions, CancellationToken ct)
    {
        if (_lockedMasters is null || _officialPermits is null)
            return actions;
        var episode = string.IsNullOrWhiteSpace(scope.EpisodeCode)
            && scope.ShotCode.StartsWith("EP01-", StringComparison.Ordinal)
            ? "EP01"
            : scope.EpisodeCode;
        if (!ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, episode))
            return actions;
        var image = actions.FirstOrDefault(row => row.Action == ProductionActions.CreateImage);
        if (ShortProductionQualityPipelineV1.Ep01Contract(scope.ShotCode) is { DirectDialogue: true, LineComplete: false })
        {
            if (image is not { Available: true }) return actions;
            return ReplaceAction(actions, image with
            {
                Available = false,
                BlockReason = ShortProductionQualityPipelineV1.SpokenLineIncomplete,
            });
        }
        var frame = await GetCurrentKeyframeAsync(scope, ct);
        if (image is { Available: true })
        {
            var issued = await _officialPermits.GetIssuedAsync(
                scope.BuildId, scope.ShotCode, ProductionActions.CreateImage,
                ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage, ct);
            var latest = issued ?? await _officialPermits.GetLatestAsync(
                scope.BuildId, scope.ShotCode, ProductionActions.CreateImage,
                ProductionImageCatalog.GeminiProviderId, ProductionImageCatalog.GeminiFlashImage, ct);
            var authorized = issued is not null
                || latest?.Status == ProductionExecutionPermitStatuses.Consumed;
            if (!frame.IsEmpty && !authorized)
                actions = ReplaceAction(actions, image with { Available = false, BlockReason = ProductionLockedStillReference.Current });
            else if (!authorized && frame.Reason is ProductionLockedStillReference.Stale or ProductionLockedStillReference.Missing)
                actions = ReplaceAction(actions, image with { Available = false, BlockReason = frame.Reason });
        }
        var refusal = frame.IsEmpty
            && frame.Reason is ProductionLockedStillReference.Stale
                or ProductionLockedStillReference.Missing
                or FamixaEp01UpstreamHold.MotionBlocked
            ? frame.Reason
            : null;
        if (refusal is null) return actions;
        foreach (var action in new[]
        {
            ProductionActions.CreateVideo, ProductionActions.CreateLipsync, ProductionActions.MixVideo,
            ProductionActions.Accept, ProductionActions.Regenerate, ProductionActions.Assemble,
            ProductionActions.AssembleEpisode,
        })
        {
            var row = actions.FirstOrDefault(item => item.Action == action);
            if (row is null) continue;
            var reason = row.BlockReason is FamixaEp01UpstreamHold.LipsyncBlocked or FamixaEp01UpstreamHold.MotionBlocked
                ? row.BlockReason
                : refusal;
            actions = ReplaceAction(actions, row with { Available = false, BlockReason = reason });
        }
        return actions;
    }

    static bool DownstreamOfPicture(string node) =>
        node is ShotNodeIds.Motion or ShotNodeIds.Lipsync or ShotNodeIds.Mix
            or ShotNodeIds.Accepted or ShotNodeIds.Assembly;

    static IReadOnlyList<ProductionActionAvailability> ReplaceAction(
        IReadOnlyList<ProductionActionAvailability> actions, ProductionActionAvailability image) =>
        actions.Select(row => row.Action == image.Action ? image : row).ToArray();

    /// <summary>
    /// The desk plays the newest mix or lip-sync that was made from the current video.
    /// An older mix stays in history and is not the picture on screen.
    /// </summary>
    async Task<ProductionPreview> PreferCurrentVideoTakeAsync(
        ProductionScope scope, ProductionPreview preview, CancellationToken ct)
    {
        if (!ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode))
            return preview;
        var takeBind = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake), ct);
        if (takeBind is null) return preview;
        var take = await _store.GetArtifactAsync(takeBind.ArtifactId, ct);
        if (!Playable(take) || FamixaEp01UpstreamHold.BlocksLipsync(take!.ArtifactId))
            return preview;
        var lipsync = await CurrentChildOfAsync(scope, ProductionArtifactRoles.Lipsync, take.ArtifactId, ct);
        var mixParent = lipsync?.ArtifactId ?? take.ArtifactId;
        var mix = await CurrentChildOfAsync(scope, ProductionArtifactRoles.Mix, mixParent, ct);
        var chosenBind = mix is not null
            ? ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)
            : lipsync is not null
                ? ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync)
                : takeBind.Role;
        var chosen = mix ?? lipsync ?? take;
        if (preview.ArtifactId == chosen.ArtifactId
            && OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(_files, preview.StorageKey, preview.ByteSha256) is not null)
            return preview;
        return new ProductionPreview(
            scope.BuildId, scope.ShotCode, chosen.ArtifactId, chosen.ByteSha256, chosen.StorageKey, chosen.DurationSec,
            chosenBind, chosen.MimeType, null);
    }

    async Task<ProductionArtifact?> CurrentChildOfAsync(
        ProductionScope scope, string role, Guid parentId, CancellationToken ct)
    {
        var bind = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(role), ct);
        if (bind is null) return null;
        var artifact = await _store.GetArtifactAsync(bind.ArtifactId, ct);
        if (!Playable(artifact) || artifact!.ParentArtifactIds?.Contains(parentId) != true)
            return null;
        return artifact;
    }

    static bool Playable(ProductionArtifact? artifact) =>
        artifact is not null
        && artifact.VerificationStatus == ProductionArtifactStatuses.Verified
        && !string.IsNullOrWhiteSpace(artifact.StorageKey)
        && !string.IsNullOrWhiteSpace(artifact.ByteSha256);

    public async Task<ProductionPreview> GetPreviewAsync(ProductionScope scope, CancellationToken ct = default)
    {
        var refusal = await PictureRefusalAsync(scope, ct);
        if (refusal is not null)
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, refusal);
        var preview = await _os.GetPreviewAsync(scope, ct);
        preview = await PreferCurrentVideoTakeAsync(scope, preview, ct);
        if (preview.IsEmpty) return preview;
        if (OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(_files, preview.StorageKey, preview.ByteSha256) is null)
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionFailureReasons.FailedVerification);
        return preview;
    }

    public Task<AuthoritativeVideoTake?> GetAuthoritativeVideoTakeAsync(ProductionScope scope, CancellationToken ct = default)
        => _os.GetAuthoritativeVideoTakeAsync(scope, ct);

    public async Task<byte[]?> ReadPreviewMediaAsync(ProductionScope scope, CancellationToken ct = default)
    {
        var preview = await GetPreviewAsync(scope, ct);
        if (preview.IsEmpty) return null;
        return OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(_files, preview.StorageKey, preview.ByteSha256);
    }

    async Task<string?> SeriesCodeAsync(Guid buildId, CancellationToken ct)
    {
        if (_seriesBuilds is null || buildId == Guid.Empty) return null;
        try
        {
            return (await _seriesBuilds.GetBuildAsync(buildId, ct)).SeriesCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<ProductionPreview> GetCurrentKeyframeAsync(ProductionScope scope, CancellationToken ct = default)
    {
        var state = await _os.GetShotStateAsync(scope, null, ct);
        var node = state.Nodes.FirstOrDefault(n => n.Node == ShotNodeIds.Keyframe);
        if (node?.ArtifactId is not Guid id || node.Status != ShotNodeStatuses.Current)
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionFailureReasons.MissingKeyframe);
        if (FamixaEp01UpstreamHold.BlocksMotion(id))
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, FamixaEp01UpstreamHold.MotionBlocked);
        var art = await _store.GetArtifactAsync(id, ct);
        if (art is null
            || !string.Equals(art.Role, ProductionArtifactRoles.Keyframe, StringComparison.Ordinal)
            || art.VerificationStatus != ProductionArtifactStatuses.Verified
            || string.IsNullOrWhiteSpace(art.MimeType)
            || !art.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(art.StorageKey)
            || string.IsNullOrWhiteSpace(art.ByteSha256))
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionFailureReasons.FailedIdentity);
        var task = await _store.GetTaskAsync(art.TaskId, ct);
        if (task is null
            || task.Scope.BuildId != scope.BuildId
            || !string.Equals(task.Scope.ShotCode, scope.ShotCode, StringComparison.Ordinal))
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionFailureReasons.CrossShotForbidden);
        if (OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(_files, art.StorageKey, art.ByteSha256) is null)
            return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionFailureReasons.FailedVerification);
        if (_lockedMasters is not null)
        {
            var snap = await _store.GetSnapshotByTaskAsync(art.TaskId, ct);
            var seriesCode = await SeriesCodeAsync(scope.BuildId, ct);
            var shotForCanon = ProductionLockedStillReference.InheritFamixaShotCanon(seriesCode)
                ? scope.ShotCode
                : null;
            var people = ProductionLockedStillReference.Characters(snap?.VisualContract, shotForCanon);
            if (people.Count > 0)
            {
                var rows = new List<(string Id, string Sha)>();
                foreach (var (characterId, name) in people)
                {
                    var master = await _lockedMasters.ReadOfficialAsync(characterId, name, ct);
                    if (master is null)
                        return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionLockedStillReference.Missing);
                    rows.Add((characterId, master.Sha256));
                }
                if (!ProductionLockedStillReference.Bound(snap?.Prompt, ProductionLockedStillReference.Token(rows)))
                    return ProductionPreview.Empty(scope.BuildId, scope.ShotCode, ProductionLockedStillReference.Stale);
            }
        }
        return new ProductionPreview(
            scope.BuildId, scope.ShotCode, art.ArtifactId, art.ByteSha256, art.StorageKey,
            0, ProductionArtifactRoles.Keyframe, art.MimeType);
    }

    public async Task<byte[]?> ReadCurrentKeyframeMediaAsync(ProductionScope scope, CancellationToken ct = default)
    {
        var frame = await GetCurrentKeyframeAsync(scope, ct);
        if (frame.IsEmpty) return null;
        return OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(_files, frame.StorageKey, frame.ByteSha256);
    }

    public Task<ProductionVoiceResolution> GetCurrentVoiceAsync(ProductionScope scope, string lineId, CancellationToken ct = default)
        => _os.GetCurrentVoiceAsync(scope, lineId, ct);

    public async Task<byte[]?> ReadCurrentVoiceMediaAsync(ProductionScope scope, string lineId, CancellationToken ct = default)
    {
        var voice = await _os.GetCurrentVoiceAsync(scope, lineId, ct);
        if (voice.IsEmpty) return null;
        return OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(_files, voice.StorageKey, voice.ByteSha256);
    }

    public Task<ProductionRequest?> GetRequestAsync(Guid requestId, CancellationToken ct = default)
        => _store.GetRequestAsync(requestId, ct);

    public Task<ProductionTask?> GetTaskAsync(Guid taskId, CancellationToken ct = default)
        => _store.GetTaskAsync(taskId, ct);

    async Task<CreateVideoSliceResult?> ReplayOrResumeCreateVideoAsync(
        Guid requestId, ShotExecutionInput live, IMotionExecutionAdapter adapter, CancellationToken ct)
    {
        var request = await _store.GetRequestAsync(requestId, ct);
        if (request is null || request.UserAction != ProductionActions.CreateVideo) return null;
        var tasks = await _store.ListTasksByRequestAsync(requestId, ct);
        var task = tasks.LastOrDefault();
        if (task is null) return null;
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct);
        if (task.Status == ProductionTaskStatuses.Succeeded)
        {
            if (snap is null)
                throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
            AssertFrozenMatch(snap, live, adapter);
            var arts = await _store.ListArtifactsByTaskAsync(task.TaskId, ct);
            var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
            var attempt = attempts.LastOrDefault();
            if (attempt is null) return null;
            var selected = ProductionAttemptOutput.Select(arts, attempt.AttemptId, ProductionArtifactRoles.VideoTake);
            if (selected.Ambiguous)
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            var artifact = selected.Artifact;
            if (artifact is null) return null;
            return await SliceOfAsync(request, task, attempt, artifact, ct);
        }

        if (task.Status is ProductionTaskStatuses.Failed or ProductionTaskStatuses.FailedIdentity)
            return null;

        if (snap is not null)
            AssertFrozenMatch(snap, live, adapter);

        var open = await _store.ListAttemptsAsync(task.TaskId, ct);
        var inflight = open.LastOrDefault(a =>
            a.Status is ProductionAttemptStatuses.Started or ProductionAttemptStatuses.ProviderSubmitted);
        if (inflight is null || snap is null) return null;
        if (string.IsNullOrWhiteSpace(inflight.ProviderJobId))
            throw new InvalidOperationException(ProductionFailureReasons.InFlightReplay);
        return await FinishExistingAttemptAsync(request, task, snap, inflight, adapter, ct);
    }

    async Task<CreateVideoSliceResult> RetryCreateVideoAsync(
        ProductionRequest request, ShotExecutionInput input, IMotionExecutionAdapter adapter, CancellationToken ct)
    {
        var tasks = await _store.ListTasksByRequestAsync(request.RequestId, ct);
        var task = tasks.LastOrDefault() ?? throw new InvalidOperationException("TASK_MISSING");
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct)
            ?? throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);
        AssertFrozenMatch(snap, input, adapter);
        return await RunAttemptLifecycleAsync(request, task, adapter, ct);
    }

    async Task<CreateVideoSliceResult> RunAttemptLifecycleAsync(
        ProductionRequest request, ProductionTask task, IMotionExecutionAdapter adapter, CancellationToken ct,
        ProductionAttempt? reuseAttempt = null)
    {
        var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct) ?? throw new InvalidOperationException("INPUT_NOT_FROZEN");
        if (reuseAttempt is null
            && ProductionPaidNetworkGate.IsTenantEp01PaidVideo(task.Scope.BuildId, task.Scope.EpisodeCode)
            && IsPilotPaidProvider(task.ProviderId))
        {
            var priorAttempts = await _store.ListAttemptsAsync(task.TaskId, ct);
            if (priorAttempts.Count > 0)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        var attempt = await _os.BeginOfficialProviderStartAsync(task.TaskId, snap.SnapshotHash, reuseAttempt?.AttemptId, ct);
        if (!string.IsNullOrWhiteSpace(attempt.ProviderJobId))
            return await FinishExistingAttemptAsync(request, task, snap, attempt, adapter, ct);
        await ConsumeOfficialPermitBeforeStartAsync(
            task.Scope, adapter, ct,
            ProductionPaidStageLedger.VideoFingerprint(snap.KeyframeArtifactId, snap.DurationSec, snap.SnapshotHash),
            task.TaskId);
        var kfBytes = await ReadKeyframeBytesAsync(snap.KeyframeArtifactId, ct);
        var kfMime = await ReadKeyframeMimeAsync(snap.KeyframeArtifactId, ct);
        MotionStartResult started;
        try
        {
            started = await adapter.StartFrozenAsync(snap, new MotionStartContext(
                kfBytes, kfMime, attempt.AttemptId, task.Scope, task.Scope.ShotCode), ct);
        }
        catch (Exception ex)
        {
            await FailAttemptAsync(attempt.AttemptId, ex.Message, ct);
            throw;
        }
        await _os.SubmitProviderJobAsync(attempt.AttemptId, started.ProviderJobId, ct);
        var persisted = await _store.GetAttemptAsync(attempt.AttemptId, ct)
            ?? throw new InvalidOperationException("ATTEMPT_MISSING");
        if (string.IsNullOrWhiteSpace(persisted.ProviderJobId))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var poll = await PollUntilDoneAsync(adapter, snap, persisted.ProviderJobId, ct);
        if (!string.Equals(poll.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal))
        {
            await FailAttemptAsync(persisted.AttemptId, poll.FailureReason ?? ProductionFailureReasons.ProviderUnavailable, ct);
            throw new InvalidOperationException(poll.FailureReason ?? ProductionFailureReasons.ProviderUnavailable);
        }
        return await MaterializeOutputAsync(request, task, snap, persisted, adapter, ct);
    }

    async Task<MotionPollResult> PollUntilDoneAsync(
        IMotionExecutionAdapter adapter, ProductionTaskInputSnapshot snap, string providerJobId, CancellationToken ct)
    {
        MotionPollResult? last = null;
        for (var i = 0; i < 90; i++)
        {
            last = i == 1
                ? await adapter.RecoverFrozenAsync(snap, providerJobId, ct)
                : await adapter.PollFrozenAsync(snap, providerJobId, ct);
            if (string.Equals(last.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal)
                || string.Equals(last.Status, MotionPollStatuses.Failed, StringComparison.Ordinal))
                return last;
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
        return last ?? new MotionPollResult(MotionPollStatuses.Failed, providerJobId, ProductionFailureReasons.ProviderUnavailable);
    }

    async Task<CreateVideoSliceResult> FinishExistingAttemptAsync(
        ProductionRequest request,
        ProductionTask task,
        ProductionTaskInputSnapshot snap,
        ProductionAttempt attempt,
        IMotionExecutionAdapter adapter,
        CancellationToken ct)
    {
        var jobId = attempt.ProviderJobId!;
        var poll = await PollUntilDoneAsync(adapter, snap, jobId, ct);
        if (!string.Equals(poll.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal))
        {
            await FailAttemptAsync(attempt.AttemptId, poll.FailureReason ?? ProductionFailureReasons.ProviderUnavailable, ct);
            throw new InvalidOperationException(poll.FailureReason ?? ProductionFailureReasons.ProviderUnavailable);
        }
        return await MaterializeOutputAsync(request, task, snap, attempt, adapter, ct);
    }

    async Task<CreateVideoSliceResult> MaterializeOutputAsync(
        ProductionRequest request,
        ProductionTask task,
        ProductionTaskInputSnapshot snap,
        ProductionAttempt attempt,
        IMotionExecutionAdapter adapter,
        CancellationToken ct)
    {
        var existingArts = await _store.ListArtifactsByTaskAsync(task.TaskId, ct);
        var selected = ProductionAttemptOutput.Select(existingArts, attempt.AttemptId, ProductionArtifactRoles.VideoTake);
        if (selected.Ambiguous)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (selected.Artifact is not null)
            return await SliceOfAsync(request, task, attempt, selected.Artifact, ct);

        var executed = await adapter.FetchOutputAsync(snap, attempt.ProviderJobId ?? "", ct);
        if (executed.Bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var storage = _files.Write(await StorageFolderAsync(task.Scope, keyframe: false, ct), attempt.AttemptId.ToString("N"), executed.Bytes);
        if (attempt.Status != ProductionAttemptStatuses.ProviderSucceeded)
            await _os.CompleteAttemptAsync(attempt.AttemptId, KitVideoIntegrityRules.Sha256Hex(executed.Bytes), ct);
        var artifact = await _os.VerifyArtifactAsync(attempt.AttemptId, executed.Bytes, executed.MimeType, executed.DurationSec, storage, ct);
        var current = await TryBindProtectedAsync(() => _os.BindCurrentProtectedAsync(artifact.ArtifactId, ct), task, ProductionBindingRoles.Current, ct);
        var visible = await TryBindProtectedAsync(() => _os.BindVisibleProtectedAsync(artifact.ArtifactId, ct), task, ProductionBindingRoles.Visible, ct);
        task = await _store.GetTaskAsync(task.TaskId, ct) ?? task;
        attempt = await _store.GetAttemptAsync(attempt.AttemptId, ct) ?? attempt;
        return new CreateVideoSliceResult(request, task, attempt, artifact, current, visible);
    }

    async Task<CreateVideoSliceResult> SliceOfAsync(
        ProductionRequest request, ProductionTask task, ProductionAttempt attempt, ProductionArtifact artifact, CancellationToken ct)
    {
        var current = await _store.GetActiveBindingAsync(task.Scope.BuildId, task.Scope.ShotCode, ProductionBindingRoles.Current, ct);
        var visible = await _store.GetActiveBindingAsync(task.Scope.BuildId, task.Scope.ShotCode, ProductionBindingRoles.Visible, ct);
        return new CreateVideoSliceResult(request, task, attempt, artifact, current, visible);
    }

    async Task<ProductionBinding?> TryBindProtectedAsync(
        Func<Task<ProductionBinding>> bind, ProductionTask task, string role, CancellationToken ct)
    {
        try
        {
            return await bind();
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent)
        {
            return await _store.GetActiveBindingAsync(task.Scope.BuildId, task.Scope.ShotCode, role, ct);
        }
    }

    async Task<ProductionBinding?> TryBindNamedProtectedAsync(Guid artifactId, string role, CancellationToken ct)
    {
        try
        {
            return await _os.BindNamedProtectedAsync(artifactId, role, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.LateResultNotCurrent)
        {
            return null;
        }
    }

    void RequirePaidNetworkAuthorization(CreateVideoApiCommand command, IMotionExecutionAdapter adapter)
    {
        if (!adapter.IsPaid || !adapter.RequiresNetwork)
            return;
        if (ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId))
        {
            if (!command.AuthorizePaidNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            return;
        }
        if (ProductionPaidNetworkGate.IsTenantEp01PaidVideo(command.Scope.BuildId, command.Scope.EpisodeCode))
        {
            if (!command.AuthorizePaidNetwork)
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
            return;
        }
        if (!command.AuthorizePaidNetwork
            || _permit is null
            || !_permit.IsEligible(command.Scope, command.Scope.ShotCode, adapter.ProviderId, adapter.ModelId))
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
    }

    async Task RequireOfficialIssuedPermitAsync(
        ProductionScope scope, IMotionExecutionAdapter adapter, CancellationToken ct)
        => await RequireOfficialIssuedPermitAsync(
            scope, ProductionActions.CreateVideo, adapter.ProviderId, adapter.ModelId, ct);

    async Task RequireOfficialIssuedPermitAsync(
        ProductionScope scope, string action, string providerId, string modelId, CancellationToken ct)
    {
        if (_officialPermits is null)
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        var issued = await _officialPermits.GetIssuedAsync(
            scope.BuildId, scope.ShotCode, action, providerId, modelId, ct);
        if (issued is null
            || !ProductionExecutionPermitCanon.Matches(
                issued, scope.BuildId, scope.ShotCode, action, providerId, modelId))
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
    }

    async Task ConsumeOfficialPermitBeforeStartAsync(
        ProductionScope scope, IMotionExecutionAdapter adapter, CancellationToken ct,
        string? stageFingerprint = null, Guid? excludeTaskId = null)
        => await ConsumeOfficialPermitBeforeStartAsync(
            scope, ProductionActions.CreateVideo, adapter.ProviderId, adapter.ModelId, adapter.IsPaid, ct,
            stageFingerprint, excludeTaskId);

    async Task ConsumeOfficialPermitBeforeStartAsync(
        ProductionScope scope, string action, string providerId, string modelId, bool isPaid, CancellationToken ct,
        string? stageFingerprint = null, Guid? excludeTaskId = null)
    {
        if (!isPaid)
            return;
        if (ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId))
        {
            await RequireOfficialIssuedPermitAsync(scope, action, providerId, modelId, ct);
            var consumed = _officialPermits is not null
                && await _officialPermits.TryConsumeAsync(
                    scope.BuildId, scope.ShotCode, action, providerId, modelId, ct);
            if (!consumed)
                throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
            return;
        }
        if (!(string.Equals(action, ProductionActions.CreateVideo, StringComparison.Ordinal)
                || string.Equals(action, ProductionActions.CreateLipsync, StringComparison.Ordinal)
                || string.Equals(action, ProductionActions.CreateVoice, StringComparison.Ordinal))
            || !ProductionPaidNetworkGate.IsTenantEp01PaidVideo(scope.BuildId, scope.EpisodeCode))
            return;
        await GuardTenantPaidStageAsync(scope, action, stageFingerprint ?? "", 0m, ct, excludeTaskId);
        await RequireTenantIssuedPermitAsync(scope, action, providerId, modelId, ct);
        var tenantConsumed = await _officialPermits!.TryConsumeAsync(
            scope.BuildId, scope.ShotCode, action, providerId, modelId, ct);
        if (!tenantConsumed)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
    }

    async Task GuardTenantPaidStageAsync(
        ProductionScope scope, string action, string fingerprint, decimal nextUsd, CancellationToken ct, Guid? excludeTaskId = null)
    {
        if (!ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode))
            return;
        var read = await ReadPaidStageAsync(scope, action, ct, excludeTaskId);
        var changed = read.Paid == 0
            || !string.Equals(read.Fingerprint, fingerprint, StringComparison.Ordinal)
            || read.BalanceRefusal;
        var cap = ProductionPaidStagePolicy.Cap(scope.ShotCode, action);
        var gate = ProductionPaidStageLedger.Gate(read.Paid, read.InFlight, read.Classified, changed, read.SpentUsd, nextUsd, cap);
        if (gate == ProductionFailureReasons.PaidStageUnchanged)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        if (gate != ProductionPaidStagePolicy.Allow && gate != ProductionPaidStagePolicy.Retry)
            throw new InvalidOperationException(gate);
    }

    async Task<string> SummarizePaidStagesAsync(ProductionScope scope, CancellationToken ct)
    {
        var parts = new List<string>();
        foreach (var action in new[]
        {
            ProductionActions.CreateImage,
            ProductionActions.CreateVideo,
            ProductionActions.CreateVoice,
            ProductionActions.CreateLipsync,
        })
        {
            var read = await ReadPaidStageAsync(scope, action, ct, null);
            parts.Add(action + ":" + read.Paid + "/" + ProductionPaidStagePolicy.Cap(scope.ShotCode, action)
                + " estUsd=" + read.SpentUsd.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return "from=" + ProductionPaidStagePolicy.CountFrom.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture)
            + ";" + string.Join(";", parts);
    }

    async Task<(int Paid, bool InFlight, bool Classified, string? Fingerprint, decimal SpentUsd, bool BalanceRefusal)> ReadPaidStageAsync(
        ProductionScope scope, string action, CancellationToken ct, Guid? excludeTaskId)
    {
        var tasks = await _store.ListTasksByScopeAsync(scope.BuildId, scope.ShotCode, ct);
        var pictureRejected = false;
        if (string.Equals(action, ProductionActions.CreateImage, StringComparison.Ordinal))
        {
            var current = await GetCurrentKeyframeAsync(scope, ct);
            pictureRejected = current.IsEmpty;
        }
        var rows = new List<PaidStageAttemptRow>();
        foreach (var task in tasks.Where(item => string.Equals(item.Action, action, StringComparison.Ordinal)).OrderBy(item => item.CreatedAt))
        {
            if (task.CreatedAt < ProductionPaidStagePolicy.CountFrom)
                continue;
            if (excludeTaskId is Guid skip && task.TaskId == skip)
                continue;
            var attempts = await _store.ListAttemptsAsync(task.TaskId, ct);
            var providerReached = attempts.Any(a => a.Status is ProductionAttemptStatuses.ProviderSubmitted
                or ProductionAttemptStatuses.ProviderSucceeded
                or ProductionAttemptStatuses.ProviderFailed
                or ProductionAttemptStatuses.Late);
            var reached = ProductionPaidStageLedger.CountsTowardCap(
                task.CreatedAt, task.ProviderId, task.ModelId, providerReached);
            var terminal = task.Status is ProductionTaskStatuses.Succeeded
                or ProductionTaskStatuses.Failed
                or ProductionTaskStatuses.FailedIdentity
                or ProductionTaskStatuses.Superseded;
            var open = !terminal && !reached && (
                task.Status is ProductionTaskStatuses.Running or ProductionTaskStatuses.ProviderFrozen
                || attempts.Any(a => a.CompletedAt is null
                    && a.Status is ProductionAttemptStatuses.Started or ProductionAttemptStatuses.ProviderSubmitted));
            var waiting = !terminal && attempts.Any(a => a.CompletedAt is null
                && a.Status == ProductionAttemptStatuses.ProviderSubmitted);
            var snap = await _store.GetSnapshotByTaskAsync(task.TaskId, ct);
            var request = await _store.GetRequestAsync(task.RequestId, ct);
            var failure = task.FailureReason ?? attempts.LastOrDefault(a => !string.IsNullOrWhiteSpace(a.ErrorCode))?.ErrorCode;
            var size = ProductionPaidStageLedger.HistoricalImageSize(request?.IdempotencyKey, failure);
            if (string.IsNullOrEmpty(size))
                size = ProductionQualityTiers.ImageDraft;
            var fingerprint = string.Equals(action, ProductionActions.CreateImage, StringComparison.Ordinal)
                ? ProductionPaidStageLedger.ImageFingerprint(
                    size,
                    ProductionLockedStillReference.Token(ProductionLockedStillReference.Frozen(snap?.Prompt)),
                    ProductionPaidStageLedger.ActingSection(snap?.Prompt),
                    ProductionPaidStageLedger.VisualSection(snap?.Prompt))
                : string.Equals(action, ProductionActions.CreateVideo, StringComparison.Ordinal)
                    ? ProductionPaidStageLedger.VideoFingerprint(snap?.KeyframeArtifactId, snap?.DurationSec ?? 0m, snap?.SnapshotHash)
                    : snap?.SnapshotHash ?? "";
            var artifacts = await _store.ListArtifactsByTaskAsync(task.TaskId, ct);
            var held = artifacts.Any(a => FamixaEp01UpstreamHold.MarksPaidRetry(a.ArtifactId));
            var classified = task.Status != ProductionTaskStatuses.Succeeded
                || !string.IsNullOrWhiteSpace(task.FailureCode)
                || held
                || attempts.Any(a => a.Status == ProductionAttemptStatuses.ProviderFailed)
                || (pictureRejected && reached);
            var usd = string.Equals(action, ProductionActions.CreateImage, StringComparison.Ordinal)
                ? ProductionPaidStageLedger.ImageEstimateUsd(task.Status, size)
                : 0m;
            rows.Add(new PaidStageAttemptRow(reached, open || waiting, fingerprint, classified, usd, failure));
        }
        return ProductionPaidStageLedger.Read(rows);
    }

    async Task RequireTenantImageAuthorizationAsync(
        ProductionScope scope, string providerId, string modelId, CancellationToken ct)
    {
        if (_officialPermits is null)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        var issued = await _officialPermits.GetIssuedAsync(
            scope.BuildId, scope.ShotCode, ProductionActions.CreateImage, providerId, modelId, ct);
        if (issued is not null
            && ProductionExecutionPermitCanon.Matches(
                issued, scope.BuildId, scope.ShotCode, ProductionActions.CreateImage, providerId, modelId))
            return;
        var latest = await _officialPermits.GetLatestAsync(
            scope.BuildId, scope.ShotCode, ProductionActions.CreateImage, providerId, modelId, ct);
        if (latest is not null
            && ProductionExecutionPermitCanon.MatchesConsumed(
                latest, scope.BuildId, scope.ShotCode, ProductionActions.CreateImage, providerId, modelId))
            return;
        throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
    }

    async Task RequireTenantIssuedPermitAsync(
        ProductionScope scope, string action, string providerId, string modelId, CancellationToken ct)
    {
        if (_officialPermits is null)
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        var issued = await _officialPermits.GetIssuedAsync(
            scope.BuildId, scope.ShotCode, action, providerId, modelId, ct);
        if (issued is null
            || !ProductionExecutionPermitCanon.Matches(
                issued, scope.BuildId, scope.ShotCode, action, providerId, modelId))
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
    }

    sealed record PlannedCreateVideo(
        IMotionExecutionAdapter Adapter,
        ShotExecutionInput Input,
        string Idem,
        decimal Duration,
        ProductionCostDecision Cost,
        ProductionArtifact Keyframe,
        ProductionCredentialReference? Credential = null);

    async Task<(string ProviderId, string ModelId, IMotionExecutionAdapter? Adapter, ProductionCredentialReference? Credential)> BindCreateVideoMotionAsync(
        CreateVideoApiCommand command, CancellationToken ct)
    {
        if (_tenantAi is not null && await _tenantAi.GovernsAsync(command.Scope, ct))
        {
            var resolved = await _tenantAi.ResolveAsync(command.Scope, ProductionTenantAiCapabilities.Video, ct);
            if (ProductionCreateVideoProviderBinding.HasProviderModel(command.ProviderId, command.ModelId)
                && (!string.Equals(command.ProviderId!.Trim(), resolved.ProviderId, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(command.ModelId!.Trim(), resolved.ModelId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
            var credential = TenantCredential(command.Credential, resolved);
            return (resolved.ProviderId, resolved.ModelId, _resolver.Resolve(resolved.ProviderId, resolved.ModelId), credential);
        }
        var official = ProductionScopeAnchorRules.IsOfficialFilm(command.Scope.BuildId);
        var selection = await _os.GetActiveVideoSelectionAsync(command.Scope, ct);
        var (providerId, modelId) = ProductionCreateVideoProviderBinding.Authoritative(
            official, selection, command.ProviderId, command.ModelId, official ? null : _motion);
        if (!ProductionCreateVideoProviderBinding.HasProviderModel(providerId, modelId))
            return (providerId, modelId, null, command.Credential);
        return (providerId, modelId, _resolver.Resolve(providerId, modelId), command.Credential);
    }

    static ProductionCredentialReference? TenantCredential(ProductionCredentialReference? claimed, ProductionResolvedAi resolved)
    {
        if (claimed is not null && claimed != resolved.Credential)
            throw new InvalidOperationException(ProductionTenantAiReasons.CredentialMismatch);
        return resolved.Credential;
    }

    async Task RecordStageHoldAsync(
        ProductionScope scope, Guid? artifactId, string action, string reason, CancellationToken ct)
    {
        if (artifactId is not Guid id) return;
        var artifact = await _store.GetArtifactAsync(id, ct);
        if (artifact is null) return;
        var prior = await _store.ListEventsByTaskAsync(artifact.TaskId, ct);
        if (prior.Any(e => e.EventType == ProductionEventTypes.StageGate && e.ArtifactId == id && e.Action == action))
            return;
        var payload = "{\"result\":\"BLOCKED\",\"reason\":\"" + reason
            + "\",\"artifactId\":\"" + artifact.ArtifactId
            + "\",\"sha256\":\"" + artifact.ByteSha256
            + "\",\"maxPaidAttempts\":" + ProductionPaidStagePolicy.MaxPaidAttempts
            + ",\"ceilingUsd\":" + ProductionPaidStagePolicy.EpisodeCeilingUsd.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "}";
        await _store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(), ProductionEventTypes.StageGate, DateTimeOffset.UtcNow,
            null, artifact.TaskId, artifact.AttemptId, artifact.ArtifactId,
            scope.ShotCode, action, payload), ct);
    }

    async Task<PlannedCreateVideo> PlanCreateVideoAsync(CreateVideoApiCommand command, CancellationToken ct)
    {
        var (providerId, modelId, adapter, credential) = await BindCreateVideoMotionAsync(command, ct);
        if (adapter is null)
        {
            var blocked = await _os.EvaluateCreateVideoAsync(
                command.Scope, CreateVideoActionContractGate.ClaimFrom(command.Input), ct);
            if (blocked.BlockReason == FamixaEp01UpstreamHold.MotionBlocked)
                await RecordStageHoldAsync(command.Scope, blocked.KeyframeArtifactId, ProductionActions.CreateVideo, blocked.BlockReason, ct);
            throw new InvalidOperationException(blocked.BlockReason ?? ProductionFailureReasons.MissingProviderModel);
        }
        var kf = command.Input.KeyframeArtifactId is { } id
            ? await _store.GetArtifactAsync(id, ct)
            : null;
        if (kf is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        var claim = CreateVideoActionContractGate.ClaimFrom(command.Input) with
        {
            ProviderId = providerId,
            ModelId = modelId,
        };
        var verdict = await _os.EvaluateCreateVideoAsync(command.Scope, claim, ct);
        if (!verdict.Available)
        {
            if (verdict.BlockReason == FamixaEp01UpstreamHold.MotionBlocked)
                await RecordStageHoldAsync(command.Scope, verdict.KeyframeArtifactId, ProductionActions.CreateVideo, verdict.BlockReason, ct);
            throw new InvalidOperationException(verdict.BlockReason ?? ProductionFailureReasons.MissingDependency);
        }
        var duration = verdict.DurationSec > 0 ? verdict.DurationSec : command.Input.DurationSec;
        var cost = ProductionMotionCostGuard.Evaluate(adapter.ProviderId, adapter.ModelId, duration, adapter.IsPaid);
        if (adapter.IsPaid && !cost.Allowed)
            throw new InvalidOperationException(cost.BlockReason ?? ProductionFailureReasons.UnknownCost);
        var input = command.Input with
        {
            Story = verdict.Story,
            ActingBeat = CreateVideoActionContractGate.IsPresent(verdict.DerivedActingBeat) ? verdict.DerivedActingBeat : verdict.ActingBeat,
            VisualContract = verdict.VisualContract,
            Timing = verdict.Timing,
            DurationSec = duration,
            KeyframeArtifactId = verdict.KeyframeArtifactId,
            KeyframeByteSha256 = verdict.KeyframeByteSha256,
            KeyframeHash = verdict.KeyframeByteSha256,
            ParentArtifactIds = [verdict.KeyframeArtifactId!.Value],
            CompilerVersion = ShotExecutionCompiler.Version,
            PerformanceSnapshotId = verdict.PerformanceSnapshotId ?? command.Input.PerformanceSnapshotId,
            PerformanceHash = CreateVideoActionContractGate.IsPresent(verdict.PerformanceHash) ? verdict.PerformanceHash : command.Input.PerformanceHash,
        };
        input = ApplyFutureVideoSpec(command.Scope.ShotCode, input);
        var snapHash = ShotExecutionCompiler.Hash(string.Join('|',
            input.Story, input.ActingBeat, input.VisualContract, verdict.KeyframeByteSha256, input.Timing, providerId, modelId,
            input.PerformanceSnapshotId?.ToString("D") ?? "", input.PerformanceHash ?? ""));
        var idem = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"{command.Scope.BuildId:N}:{command.Scope.ShotCode}:CREATE_VIDEO:{snapHash}"
            : command.IdempotencyKey.Trim();
        return new PlannedCreateVideo(adapter, input, idem, duration, cost, kf, credential);
    }

    async Task<ProductionCreateVideoPreflight> BuildPreflightAsync(
        ProductionScope scope,
        PlannedCreateVideo planned,
        ProductionTask task,
        ProductionTaskInputSnapshot snap,
        ProductionAttempt attempt,
        CancellationToken ct)
    {
        var anchor = _scopes is null ? null : await _scopes.GetAnchorAsync(scope.BuildId, ct);
        var kind = ProductionScopeGuard.Resolve(anchor, scope.BuildId);
        var binding = await _store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Keyframe), ct);
        var providerDuration = planned.Duration >= 8 ? 10 : 5;
        return new ProductionCreateVideoPreflight(
            kind == ProductionScopeKind.Test ? ProductionScopeAnchorRules.Test : kind.ToString(),
            anchor?.TestRunId ?? Guid.Empty,
            scope.BuildId,
            scope.ShotCode,
            planned.Keyframe.ArtifactId,
            planned.Keyframe.ByteSha256,
            planned.Keyframe.VerificationStatus,
            binding?.Role ?? "",
            snap.Story,
            snap.ActingBeat,
            snap.VisualContract,
            planned.Input.Timing,
            snap.ProviderId,
            snap.ModelId,
            FamixaProviderCapability.Motion.ToString(),
            providerDuration,
            string.IsNullOrWhiteSpace(snap.AspectRatio) ? "16:9" : snap.AspectRatio,
            planned.Cost.EstimatedCredits ?? 0,
            planned.Cost.MaxCredits,
            snap.SnapshotId,
            snap.SnapshotHash,
            task.TaskId,
            attempt.AttemptId,
            ProductionCreateVideoPreflightRules.ConfirmationExplicit,
            ProductionCreateVideoPreflightRules.NetworkRealThisTestOnly,
            ProductionCreateVideoPreflightRules.FilmMutationForbidden);
    }

    async Task<byte[]?> ReadKeyframeBytesAsync(Guid? keyframeArtifactId, CancellationToken ct)
    {
        if (keyframeArtifactId is null) return null;
        var art = await _store.GetArtifactAsync(keyframeArtifactId.Value, ct);
        if (art is null || string.IsNullOrWhiteSpace(art.StorageKey)) return null;
        return _files.Read(art.StorageKey);
    }

    async Task<string> ReadKeyframeMimeAsync(Guid? keyframeArtifactId, CancellationToken ct)
    {
        if (keyframeArtifactId is null) return "image/png";
        var art = await _store.GetArtifactAsync(keyframeArtifactId.Value, ct);
        return string.IsNullOrWhiteSpace(art?.MimeType) ? "image/png" : art.MimeType;
    }

    Task FailAttemptAsync(Guid attemptId, string error, CancellationToken ct)
        => _os.FailAttemptAsync(attemptId, error, ct);

    /// <summary>
    /// Replay compares the same projection Freeze stores. EP01 contract shots replace a stale
    /// hold line with the approved continuity acting before that compare. A real acting change
    /// that survives the projection still fails closed.
    /// </summary>
    static ShotExecutionInput ProjectReplayInput(string shotCode, ShotExecutionInput input)
        => ApplyFutureVideoSpec(shotCode, input);

    static ShotExecutionInput ApplyFutureImageSpec(string shotCode, ShotExecutionInput input)
    {
        var acting = ShortProductionQualityPipelineV1.IsExactHistoricalSh02Replay(shotCode, input.Story, input.ActingBeat)
            ? ""
            : input.ActingBeat;
        return ApplyFutureVideoSpec(shotCode, input with { ActingBeat = acting });
    }

    static ShotExecutionInput ApplyFutureVideoSpec(string shotCode, ShotExecutionInput input)
    {
        var future = ShortProductionQualityPipelineV1.PrepareFutureVideo(
            shotCode, input.Story, input.ActingBeat, input.VisualContract);
        if (future.Story == input.Story && future.ActingBeat == input.ActingBeat && future.VisualContract == input.VisualContract)
            return input;
        return input with
        {
            Story = future.Story,
            ActingBeat = future.ActingBeat,
            VisualContract = future.VisualContract,
        };
    }

    static void AssertFrozenMatch(ProductionTaskInputSnapshot snap, ShotExecutionInput live, IMotionExecutionAdapter adapter)
        => ProductionFrozenCreateVideoMatch.Assert(snap, live, adapter);

    async Task<ProductionArtifact?> ExistingArtifactAsync(Guid requestId, string role, CancellationToken ct)
    {
        var tasks = await _store.ListTasksByRequestAsync(requestId, ct);
        foreach (var task in tasks)
        {
            var arts = await _store.ListArtifactsByTaskAsync(task.TaskId, ct);
            var hit = arts.FirstOrDefault(a => a.Role == role && a.VerificationStatus == ProductionArtifactStatuses.Verified);
            if (hit is not null) return hit;
        }
        return null;
    }

    async Task<string> StorageFolderAsync(ProductionScope scope, bool keyframe, CancellationToken ct)
    {
        if (_scopes is null)
            return keyframe ? ProductionStoragePolicy.FilmKeyframeFolder : ProductionStoragePolicy.FilmVideoFolder;
        var anchor = await _scopes.GetAnchorAsync(scope.BuildId, ct);
        var kind = ProductionScopeGuard.Resolve(anchor, scope.BuildId);
        return keyframe
            ? ProductionStoragePolicy.KeyframeFolder(kind, anchor?.TestRunId)
            : ProductionStoragePolicy.VideoFolder(kind, anchor?.TestRunId);
    }
}
