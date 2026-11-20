namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionCreateVideoRequestDto(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    string Story,
    string ActingBeat,
    string VisualContract,
    string Timing,
    string AspectRatio,
    decimal DurationSec,
    string? IdempotencyKey,
    string? KeyframeDataUrl,
    Guid? KeyframeArtifactId,
    string? ProviderId = null,
    string? ModelId = null,
    bool AuthorizePaidNetwork = false,
    Guid? PreparedAttemptId = null,
    string Action = ProductionActions.CreateVideo,
    string? LineId = null,
    string? SpeakerCharacterId = null,
    string? VoiceId = null,
    Guid? MotionArtifactId = null,
    Guid? VoiceArtifactId = null,
    Guid? LipsyncArtifactId = null,
    Guid? AcceptArtifactId = null,
    Guid[]? AssemblyArtifactIds = null,
    string[]? AssemblyShotCodes = null,
    Guid? PerformanceSnapshotId = null,
    string? PerformanceHash = null,
    string? ImageSize = null,
    string? PictureStrategy = null,
    string? SpeechDelivery = null,
    bool StrategyConfirmed = false,
    int DirectSpeakerCount = 0);

public sealed record ProductionCreateVideoPrepareDto(
    Guid RequestId,
    Guid TaskId,
    Guid SnapshotId,
    string SnapshotHash,
    Guid AttemptId,
    string ProviderId,
    string ModelId,
    decimal ProviderDuration,
    decimal EstimatedCost,
    decimal MaxCost,
    Guid KeyframeArtifactId,
    string KeyframeSha,
    string KeyframeStatus,
    string Confirmation,
    string Network,
    string FilmMutation);

public sealed record ProductionRecoverVideoTakeRequestDto(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    Guid SourceArtifactId);

public sealed record ProductionRecoverVoiceRequestDto(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    Guid SourceArtifactId);

public sealed record ProductionRegisterVoiceRequestDto(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    string Story,
    string VoiceDataUrl);

public sealed record ProductionRegisterKeyframeRequestDto(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode,
    string Story,
    string KeyframeDataUrl,
    string? ActingBeat = null,
    string? VisualContract = null,
    string? Timing = null,
    decimal DurationSec = 0,
    string? PictureRevisionId = null,
    string? IngressProvenanceJson = null);

public sealed record ProductionCreateVideoResultDto(
    Guid RequestId,
    Guid TaskId,
    Guid SnapshotId,
    Guid AttemptId,
    string Action,
    string ProviderId,
    string ModelId,
    string? ProviderJobId,
    Guid ArtifactId,
    string ByteSha256,
    string StorageKey,
    Guid? CurrentBindingId,
    Guid? VisibleBindingId,
    string PreviewKind);

public sealed record ProductionPreviewDto(
    bool Empty,
    string? Reason,
    Guid? ArtifactId,
    string? ByteSha256,
    string? StorageKey,
    string? FileUrl,
    string? Role = null,
    string? MimeType = null,
    decimal? DurationSec = null);

public sealed record AuthoritativeVideoTakeDto(
    bool Empty,
    string? Reason,
    Guid? ArtifactId,
    string? Role,
    string? AcceptanceStatus,
    string? VerificationStatus,
    string? ByteSha256,
    string? MimeType,
    long? ByteLength,
    Guid? TaskId,
    Guid? AttemptId,
    Guid? SnapshotId,
    string? ProviderId,
    string? ModelId,
    string? FileUrl);

public sealed record ProductionVoiceArtifactDto(
    bool Empty,
    string? Reason,
    Guid? ArtifactId,
    Guid? TaskId,
    Guid? AttemptId,
    string? InputSnapshotHash,
    string? ProviderId,
    string? ModelId,
    string? ByteSha256,
    string? StorageKey,
    string? FileUrl,
    string? Role = null,
    string? MimeType = null,
    decimal? DurationSec = null,
    string? LineId = null);

public sealed record ProductionShotActionsDto(
    Guid BuildId,
    string ShotCode,
    string CurrentState,
    Guid? CurrentArtifactId,
    Guid? VisibleArtifactId,
    Guid? AcceptedArtifactId,
    IReadOnlyList<ProductionActionAvailability> Actions,
    Guid? VideoArtifactId = null,
    Guid? VoiceArtifactId = null,
    Guid? LipsyncArtifactId = null);

public sealed record ProductionRequestDto(
    Guid RequestId,
    string UserAction,
    string RequiredOutputType,
    string Status,
    Guid BuildId,
    string ShotCode,
    string IdempotencyKey);

public sealed record ProductionRegisterTestKeyframeRequestDto(
    string ShotCode,
    string FixtureId);

public sealed record ProductionTestKeyframeDto(
    string FixtureId,
    Guid ArtifactId,
    Guid ScopeId,
    string ShotCode,
    string Role,
    string Mime,
    string ByteSha256,
    long ByteLength,
    int Width,
    int Height,
    string StorageKey,
    string VerificationStatus,
    string Source,
    Guid BindingId,
    bool Reused);

public sealed record ProductionCreateTestRunRequestDto(string Name);

public sealed record ProductionTestRunDto(
    Guid TestRunId,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    Guid ScopeId,
    string Kind,
    Guid? SeriesBuildId,
    Guid TestShotId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode);

public sealed record ProductionTaskDto(
    Guid TaskId,
    Guid RequestId,
    string Action,
    string Status,
    string? ProviderId,
    string? ModelId,
    Guid? InputSnapshotId);

public sealed record PerformanceDraftRequestDto(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    IReadOnlyList<string> CharacterIds,
    IReadOnlyList<string> DialogueSegmentIds,
    IReadOnlyList<PerformanceLineRequestDto> Lines,
    PerformanceActingBeatRequestDto? ActingBeat = null,
    string? GazeTarget = null,
    IReadOnlyList<string>? OffFrameCharacterIds = null,
    IReadOnlyList<string>? VoiceOnlyCharacterIds = null,
    IReadOnlyList<string>? PrimaryCharacterIds = null,
    IReadOnlyList<string>? SecondaryCharacterIds = null,
    string? TimingHash = null,
    string? VisualAuthorityId = null,
    string? VisualContractHash = null,
    string? StoryTextHash = null,
    string? BeatId = null,
    string? ExtraJson = null);

public sealed record PerformanceActingBeatRequestDto(
    string? BeforeAction = null,
    string? BeforeBody = null,
    string? BeforeProp = null,
    string? BeforeGaze = null,
    string? BeforeRoom = null,
    string? DuringEmotion = null,
    int? DuringIntensity = null,
    string? AfterAction = null,
    string? AfterGaze = null);

public sealed record PerformanceLineRequestDto(
    string LineId,
    string CharacterId,
    string Text,
    string? Emotion = null,
    int? Intensity = null,
    string? Pace = null,
    string? Volume = null,
    double? PauseSec = null,
    string? Label = null,
    string? Emphasis = null,
    bool Locked = false);

public sealed record ProductionAiSelectionSupersedeRequestDto(
    Guid ExpectedSelectionId,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Stage,
    string ProviderId,
    string ModelId,
    string? Reason = null);

public sealed record ProductionAiSelectionSupersedeDto(
    ProductionAiSelectionDto Superseded,
    ProductionAiSelectionDto Active);

public sealed record ProductionAiSelectionRequestDto(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Stage,
    string ProviderId,
    string ModelId,
    string? Reason = null);

public sealed record ProductionAiSelectionDto(
    Guid SelectionId,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Stage,
    string ProviderId,
    string ModelId,
    string SelectionMode,
    string SelectedBy,
    DateTimeOffset SelectedAt,
    string Reason,
    string Status,
    string SelectionHash);

public sealed record ProductionExecutionPermitRequestDto(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Action,
    string ProviderId,
    string ModelId,
    string? Reason = null);

public sealed record ProductionExecutionPermitDto(
    Guid PermitId,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string Action,
    string ProviderId,
    string ModelId,
    string Status,
    string IssuedBy,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ConsumedAt,
    string Reason,
    string PermitHash);

public sealed record CreateVideoInputPreviewDto(
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

public sealed record ProductionDirectorTimingRequestDto(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    decimal DurationSec,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);

public sealed record VoiceProductionIngressRequestDto(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode);

public sealed record VoiceProductionIngressDto(
    Guid PerformanceSnapshotId,
    string PerformanceHash,
    Guid? ParentSnapshotId,
    string LineId,
    string LineText,
    string SpeakerCharacterId,
    string VoiceId,
    string ProviderId,
    string ModelId,
    Guid SelectionId,
    string SelectionHash,
    Guid PermitId,
    string PermitStatus,
    string PermitHash);

public sealed record RecoverExistingLipsyncRequestDto(
    Guid AttemptId,
    string ProviderJobId);

public sealed record CloseEmptyLipsyncRequestDto(
    string IdempotencyKey);

public sealed record PerformanceSnapshotDto(
    Guid PerformanceSnapshotId,
    string PerformanceHash,
    DateTimeOffset FrozenAt,
    string CreatedBy,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    string PayloadJson,
    bool VoiceReady,
    IReadOnlyList<string> Issues,
    decimal? ProductionDurationSec = null,
    string? TimingSource = null,
    string? DecisionType = null,
    string? TimingHash = null,
    Guid? KeyframeArtifactId = null,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);

public sealed record RegisterProductionCredentialRequestDto(
    string ProviderId,
    string ModelId,
    string Secret,
    string? DisplayName = null);

public sealed record AddProductionCredentialVersionRequestDto(
    string ProviderId,
    string ModelId,
    string Secret);

public sealed record ProductionCredentialRegistrationDto(
    Guid CredentialId,
    Guid CredentialVersionId,
    string ProviderId,
    string ModelId,
    string Status,
    int Version);

public sealed record RotateProductionCredentialRequestDto(
    string ProviderId,
    string ModelId,
    string Secret);

public sealed record ProductionCredentialRotationDto(
    string ProviderId,
    string ModelId,
    string Status,
    int Version);

public sealed record ProductionCredentialCatalogDto(
    Guid CredentialId,
    Guid CredentialVersionId,
    string ProviderId,
    string ModelId,
    string Status,
    string IdentityStatus,
    int Version,
    DateTimeOffset CreatedAt);

/// <summary>
/// AI-CATALOG-03. Production AI model metadata with tenant credential status.
/// No secret, ciphertext, nonce, tag, credential ID, or version ID.
/// </summary>
public sealed record ProductionAiCatalogModelDto(
    string ProviderId,
    string ModelId,
    string DisplayName,
    string Capability,
    string Action,
    bool Enabled,
    bool IsPaid,
    string CredentialStatus);

/// <summary>
/// AI-CATALOG-03. Production AI catalog grouped by capability.
/// </summary>
public sealed record ProductionAiCatalogDto(
    IReadOnlyList<ProductionAiCatalogModelDto> Video,
    IReadOnlyList<ProductionAiCatalogModelDto> Voice,
    IReadOnlyList<ProductionAiCatalogModelDto> LipSync,
    IReadOnlyList<ProductionAiCatalogModelDto>? Image = null);

public sealed record ProductionTenantAiCatalogEntryDto(
    string ProviderId,
    string ModelId,
    string DisplayName,
    bool IsPaid,
    bool RequiresCredential);

/// <summary>Credential of the caller tenant. No secret, ciphertext, nonce, or tag.</summary>
public sealed record ProductionTenantAiCredentialOptionDto(
    Guid CredentialId,
    string DisplayName,
    string ProviderId,
    string ModelId,
    int Version);

public sealed record ProductionTenantAiConfigDto(
    string ProviderId,
    string ModelId,
    Guid? CredentialId,
    bool Enabled,
    string UpdatedBy,
    DateTimeOffset UpdatedAt);

public sealed record ProductionTenantAiCapabilityDto(
    string Capability,
    ProductionTenantAiConfigDto? Config,
    IReadOnlyList<ProductionTenantAiCatalogEntryDto> Catalog,
    IReadOnlyList<ProductionTenantAiCredentialOptionDto> Credentials);

public sealed record ProductionTenantAiSettingsDto(
    IReadOnlyList<ProductionTenantAiCapabilityDto> Capabilities);

public sealed record SaveProductionTenantAiConfigRequestDto(
    string ProviderId,
    string ModelId,
    Guid? CredentialId,
    bool Enabled = true);
