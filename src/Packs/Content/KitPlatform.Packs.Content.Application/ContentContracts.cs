using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public sealed record ContentOrgSettingsDto(
    Guid Id,
    decimal MonthlyCeilingUsd,
    int MaxImageCandidatesPerItem,
    decimal RegenMultiplier,
    string DefaultImageTier,
    IReadOnlyDictionary<string, decimal> ImageRateUsd,
    decimal TextPackEstimateUsd,
    IReadOnlyList<string> VariantKinds,
    IReadOnlyList<string> ConnectorTypes,
    IReadOnlyList<string> ChannelTypes,
    ContentAiConfigDto Ai,
    ContentVideoConfigDto Video,
    ContentFacebookConfigDto Facebook,
    decimal MonthSpendEstimateUsd,
    decimal RemainingBudgetUsd,
    DateTimeOffset UpdatedAt);

public sealed record ContentAiConfigDto(
    string Provider,
    string TextModel,
    string? ImageModel,
    bool ImagesEnabled,
    string? GeminiApiKeySecretRef,
    bool ApiKeyConfigured);

public sealed record ContentVideoConfigDto(
    string? CreatomateApiKeySecretRef,
    bool CreatomateConfigured,
    string? ElevenLabsApiKeySecretRef,
    bool ElevenLabsConfigured,
    string? ElevenLabsVoiceId,
    string? PublicMediaBaseUrl,
    string? CreatomateTemplateId,
    string? RunwayApiKeySecretRef,
    bool RunwayConfigured,
    string? FalApiKeySecretRef,
    bool FalConfigured);

public sealed record UpdateContentOrgSettingsRequest(
    decimal? MonthlyCeilingUsd,
    int? MaxImageCandidatesPerItem,
    decimal? RegenMultiplier,
    string? DefaultImageTier,
    Dictionary<string, decimal>? ImageRateUsd,
    decimal? TextPackEstimateUsd,
    List<string>? VariantKinds,
    List<string>? ConnectorTypes,
    List<string>? ChannelTypes,
    UpdateContentAiConfigRequest? Ai,
    UpdateContentVideoConfigRequest? Video,
    UpdateContentFacebookConfigRequest? Facebook);

/// <summary>
/// AI knobs. <see cref="GeminiApiKey"/> is write-only (stored server-side, never returned on read).
/// Prefer <see cref="GeminiApiKeySecretRef"/> pointing at an env / vault name.
/// </summary>
public sealed record UpdateContentAiConfigRequest(
    string? Provider,
    string? TextModel,
    string? ImageModel,
    bool? ImagesEnabled,
    string? GeminiApiKeySecretRef,
    string? GeminiApiKey);

public sealed record ContentAiTestResultDto(
    bool Ok,
    string? Message,
    bool ApiKeyConfigured,
    string? TextModel);

/// <summary>
/// Video knobs. API keys are write-only (stored server-side, never returned on read).
/// Prefer secret refs pointing at env / vault names.
/// </summary>
public sealed record UpdateContentVideoConfigRequest(
    string? CreatomateApiKeySecretRef,
    string? CreatomateApiKey,
    string? ElevenLabsApiKeySecretRef,
    string? ElevenLabsApiKey,
    string? ElevenLabsVoiceId,
    string? PublicMediaBaseUrl,
    string? CreatomateTemplateId,
    string? RunwayApiKeySecretRef,
    string? RunwayApiKey,
    string? FalApiKeySecretRef,
    string? FalApiKey);

public sealed record ContentVideoTestResultDto(
    bool CreatomateOk,
    string? CreatomateMessage,
    bool CreatomateConfigured,
    bool ElevenLabsOk,
    string? ElevenLabsMessage,
    bool ElevenLabsConfigured,
    string? VoiceId,
    bool RunwayOk,
    string? RunwayMessage,
    bool RunwayConfigured,
    bool FalOk,
    string? FalMessage,
    bool FalConfigured);

public sealed record ContentSeriesTurboStartRequest(
    string ClipId,
    string Prompt,
    string? NegativePrompt,
    string? ImageDataUrl,
    int Seconds,
    string Ratio,
    string? Engine,
    bool Confirm = false,
    string? LastFrameFromUrl = null,
    decimal? MaxCost = null,
    string? Currency = null);

public sealed record ContentSeriesTurboTaskDto(
    string TaskId,
    string Status,
    string? VideoUrl,
    string? Error,
    bool UsedPlaceholderImage,
    string Model,
    int Seconds,
    string? FailureCode = null,
    long? VideoBytes = null,
    string? VideoMime = null,
    bool VideoVerified = false,
    string? DecisionId = null,
    string? ProviderId = null,
    string? SelectionMode = null,
    string? SelectionReason = null,
    decimal? EstimatedCost = null,
    string? CostKind = null);

public sealed record ContentSeriesLipsyncVoiceDto(
    string AudioBase64,
    double StartSec,
    string? Mime = null);

public sealed record ContentSeriesLipsyncStartRequest(
    string ClipId,
    string VideoUrl,
    string AudioBase64,
    string? Mime = null,
    string? SyncMode = null,
    string? Model = null,
    IReadOnlyList<ContentSeriesLipsyncVoiceDto>? Voices = null,
    double? ProductionDurationSec = null,
    double? PerformanceDurationSec = null,
    string? TakeTaskId = null);

public sealed record ContentSeriesStillRefDto(
    string Name,
    string ImageDataUrl,
    string? Role = null,
    string? VisualMode = null,
    string? ReferenceStatus = null,
    string? AuthorityStatus = null,
    string? CharacterId = null,
    string? ReferenceRole = null);

public sealed record ContentSeriesStillRequest(
    string Prompt,
    string Aspect,
    IReadOnlyList<ContentSeriesStillRefDto> References,
    string? CharacterId = null,
    string? Story = null,
    string? Action = null,
    string? Location = null,
    string? Environment = null,
    string? Props = null,
    string? Wardrobe = null,
    string? Camera = null,
    string? Framing = null,
    string? Composition = null,
    string? Lighting = null,
    string? Continuity = null,
    string? Emotion = null,
    string? IdentityBrief = null,
    string? PvsBuildPrompt = null,
    string? RevisionIntent = null);

public sealed record ContentSeriesStillDto(
    string ImageDataUrl,
    string Model,
    string Aspect,
    string? DecisionId = null,
    string? ProviderId = null);

public sealed record ContentSeriesStillQaRequest(
    string ImageDataUrl,
    string SpecJson);

public sealed record ContentSeriesStillQaDto(
    string Status,
    int? Total,
    IReadOnlyDictionary<string, int>? Axes,
    IReadOnlyList<string> HardFails,
    string? Notes,
    IReadOnlyDictionary<string, string>? HardChecks = null,
    string? Evidence = null,
    int? Confidence = null);

public sealed record ContentSeriesKfNoteRequest(
    string Note,
    string? Action = null,
    string? Location = null);

public sealed record ContentSeriesKfNoteDto(
    string Instruction,
    bool Place,
    bool Lighting,
    bool Wardrobe,
    bool Camera,
    bool Inherit);

public sealed record ContentSeriesScriptDraftRequest(
    string Seed,
    string? CharactersHint,
    string? EpisodeHint,
    Guid? BrandId);

public sealed record ContentSeriesScriptDraftDto(
    string Pack,
    string Model,
    decimal EstimatedUsd,
    string CostNote,
    bool UsedBrandBrain,
    string? BrandCode);

public sealed record ContentSeriesPilotDto(
    string SeriesCode,
    JsonElement Graph,
    DateTimeOffset UpdatedAt);

public sealed record UpsertContentSeriesPilotRequest(
    string SeriesCode,
    JsonElement Graph);

public sealed record ContentSeriesBuildSummaryDto(
    Guid Id,
    string SeriesCode,
    string EpisodeCode,
    string Title,
    string Status,
    int ShotCount,
    int VoiceLines,
    int KfCount,
    int VideoCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ContentSeriesBuildDto(
    Guid Id,
    string SeriesCode,
    string EpisodeCode,
    string Title,
    string Status,
    int ShotCount,
    int VoiceLines,
    int KfCount,
    int VideoCount,
    JsonElement Graph,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpsertContentSeriesBuildRequest(
    Guid? Id,
    string SeriesCode,
    JsonElement Graph);

public sealed record ContentSeriesVoiceDto(
    string VoiceId,
    string Name,
    string? Category = null,
    bool Cloned = false,
    bool Vietnamese = false,
    string? PublicOwnerId = null,
    string? Gender = null,
    string? Age = null,
    string? Accent = null,
    string? PreviewUrl = null);

public sealed record ContentSeriesTtsVoiceSettings(
    double? Stability = null,
    double? SimilarityBoost = null,
    double? Style = null,
    double? Speed = null);

public sealed record ContentSeriesTtsRequest(
    string VoiceId,
    string Text,
    string? PublicOwnerId = null,
    string? VoiceName = null,
    string? Accent = null,
    ContentSeriesTtsVoiceSettings? VoiceSettings = null);

public sealed record ContentSeriesTtsPreviewDto(
    byte[] Bytes,
    string? DecisionId = null,
    string? ProviderId = null,
    string? ModelId = null,
    string? ProviderRequestId = null);

public interface IContentSeriesPilotService
{
    Task<ContentSeriesPilotDto> GetAsync(string seriesCode, CancellationToken cancellationToken = default);
    Task<ContentSeriesPilotDto> UpsertAsync(
        UpsertContentSeriesPilotRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentSeriesVoiceDto>> ListVoicesAsync(CancellationToken cancellationToken = default);
    Task<byte[]?> GetLibraryPreviewAsync(string voiceId, CancellationToken cancellationToken = default);
    Task<ContentSeriesTtsPreviewDto> PreviewTtsAsync(
        string voiceId,
        string text,
        string? publicOwnerId = null,
        string? voiceName = null,
        ContentSeriesTtsVoiceSettings? voiceSettings = null,
        CancellationToken cancellationToken = default,
        string? accent = null);
    Task<IReadOnlyList<ContentSeriesBuildSummaryDto>> ListBuildsAsync(
        string seriesCode,
        CancellationToken cancellationToken = default);
    Task<ContentSeriesBuildDto> GetBuildAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentSeriesBuildDto> UpsertBuildAsync(
        UpsertContentSeriesBuildRequest request,
        CancellationToken cancellationToken = default);
    Task DeleteBuildAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed record ContentFacebookConfigDto(
    string? AppId,
    bool AppSecretConfigured,
    string? AppIdSecretRef,
    string? AppSecretSecretRef,
    string? RedirectUri);

public sealed record UpdateContentFacebookConfigRequest(
    string? AppId,
    string? AppIdSecretRef,
    string? AppSecretSecretRef,
    string? AppSecret,
    string? RedirectUri);

public sealed record ContentFacebookTestResultDto(
    bool Ok,
    string? Message,
    bool AppSecretConfigured,
    string? AppId);

public sealed record ContentFacebookStartDto(
    string Url,
    string State);

public sealed record ContentFacebookPageOptionDto(
    string Id,
    string Name);

public sealed record ContentFacebookPendingDto(
    string SessionId,
    Guid BrandId,
    IReadOnlyList<ContentFacebookPageOptionDto> Pages);

public sealed record ContentFacebookCompleteRequest(
    string Code,
    string State);

public sealed record ContentFacebookSelectRequest(
    string SessionId,
    string PageId);

public sealed record ContentFacebookVerifyDto(
    bool Ok,
    string Status,
    string? PageId,
    string? PageName,
    string? Message,
    DateTimeOffset? LastVerifiedAt);

public sealed record ContentBrandDto(
    Guid Id,
    string Code,
    string Name,
    string? DefaultCtaUrl,
    string? DefaultCtaLabel,
    decimal? MonthlyCeilingUsd,
    string? ImageTier,
    bool PauseWhenExceeded,
    bool IsActive,
    int SortOrder,
    string? OperationalBrief,
    ContentBrandKnowledgeDto Knowledge,
    decimal MonthSpendEstimateUsd,
    DateTimeOffset UpdatedAt,
    bool BrainReady = false,
    IReadOnlyList<string>? BrainMissing = null);

public sealed record UpsertContentBrandRequest(
    string Code,
    string Name,
    string? DefaultCtaUrl,
    string? DefaultCtaLabel,
    decimal? MonthlyCeilingUsd,
    string? ImageTier,
    bool? PauseWhenExceeded,
    bool? IsActive,
    int? SortOrder,
    string? OperationalBrief,
    ContentBrandKnowledgeDto? Knowledge);

public sealed record ContentSiteTargetDto(
    Guid Id,
    Guid BrandId,
    string Code,
    string Name,
    string ConnectorType,
    string? BaseUrl,
    string ConfigJson,
    string? SecretRef,
    bool SecretConfigured,
    bool IsActive,
    int SortOrder);

public sealed record UpsertContentSiteTargetRequest(
    string Code,
    string Name,
    string ConnectorType,
    string? BaseUrl,
    string? ConfigJson,
    string? SecretRef,
    /// <summary>Write-only token/password. null=keep, empty=clear, value=replace. Never returned on GET.</summary>
    string? Secret,
    bool? IsActive,
    int? SortOrder);

public sealed record ContentChannelTargetDto(
    Guid Id,
    Guid BrandId,
    string Code,
    string Name,
    string ChannelType,
    string? ExternalId,
    string ConfigJson,
    string? SecretRef,
    bool SecretConfigured,
    bool IsActive,
    int SortOrder);

public sealed record ContentWriteSlotDto(
    string Key,
    string Label,
    string DestType,
    IReadOnlyList<string> VariantKinds);

public sealed record ContentWritePlanDto(
    Guid BrandId,
    string BrandCode,
    string BrandName,
    IReadOnlyList<ContentWriteSlotDto> Slots,
    IReadOnlyList<string> VariantKinds,
    string Summary);

public sealed record UpsertContentChannelTargetRequest(
    string Code,
    string Name,
    string ChannelType,
    string? ExternalId,
    string? ConfigJson,
    string? SecretRef,
    /// <summary>Write-only token. null=keep, empty=clear, value=replace. Never returned on GET.</summary>
    string? Secret,
    bool? IsActive,
    int? SortOrder);

public sealed record ContentTopicDto(
    Guid Id,
    Guid BrandId,
    string BrandCode,
    string BrandName,
    string Title,
    string? Pillar,
    string Goal,
    string? CtaUrl,
    string? UtmCampaign,
    string Priority,
    string Status,
    string? BodyOutline,
    DateTimeOffset? DisplayAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int VariantCount = 0,
    Guid? CorePackageId = null,
    string? CoreTitle = null);

public sealed record ContentVariantDto(
    Guid Id,
    Guid TopicId,
    string Kind,
    string? Title,
    string BodyMarkdown,
    string MetaJson,
    DateTimeOffset UpdatedAt);

public sealed record ContentAssetDto(
    Guid Id,
    Guid TopicId,
    string Kind,
    string FileName,
    string ContentType,
    string? Prompt,
    string? Model,
    string? ImageTier,
    decimal EstimateUsd,
    bool IsSelected,
    DateTimeOffset CreatedAt);

public sealed record ContentPublishJobDto(
    Guid Id,
    Guid TopicId,
    Guid BrandId,
    string TargetKind,
    Guid? SiteTargetId,
    Guid? ChannelTargetId,
    string ConnectorType,
    string Status,
    DateTimeOffset? PublishAt,
    string? ExternalRef,
    string? LastError,
    string ResultJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ContentTopicDetailDto(
    ContentTopicDto Topic,
    IReadOnlyList<ContentVariantDto> Variants,
    IReadOnlyList<ContentAssetDto> Assets,
    IReadOnlyList<ContentPublishJobDto> Jobs);

public sealed record GenerateContentRequest(
    bool SkipImages = false,
    int? CandidateCount = null,
    /// <summary>Only generate/replace images; keep existing text variants.</summary>
    bool ImagesOnly = false,
    /// <summary>When set, only these variant kinds (intersected with destination plan).</summary>
    IReadOnlyList<string>? VariantKinds = null);

public sealed record GenerateContentResultDto(
    ContentTopicDto Topic,
    IReadOnlyList<ContentVariantDto> Variants,
    IReadOnlyList<ContentAssetDto> Assets,
    decimal EstimatedSpendUsd,
    bool BudgetBlocked,
    string? Message);

public sealed class PublishContentRequest
{
    public IReadOnlyList<Guid>? SiteTargetIds { get; set; }
    public IReadOnlyList<Guid>? ChannelTargetIds { get; set; }
    public bool IncludeManualExport { get; set; } = true;
    public bool RunImmediately { get; set; } = true;
    /// <summary>When set (or topic.DisplayAt), schedule on WP/FB instead of publishing live.</summary>
    public DateTimeOffset? PublishAt { get; set; }
    /// <summary>Ephemeral image (base64) — used only for this publish, not stored as content asset.</summary>
    public string? ImageBase64 { get; set; }
    public string? ImageFileName { get; set; }
    public string? ImageContentType { get; set; }
}

public sealed record PublishContentResultDto(
    IReadOnlyList<ContentPublishJobDto> Jobs);

public sealed record ContentCoreIdeaDto(
    string? Insight,
    string? Problem,
    string? CoreMessage,
    IReadOnlyList<string> Keywords,
    string? Source,
    string? SourceUrl = null,
    string? SourceType = null,
    string? Evidence = null,
    string? FactOrOpinion = null);

public sealed record ContentBrandFitDto(
    Guid BrandId,
    string BrandCode,
    string BrandName,
    string Verdict,
    int Score,
    string? Reason,
    string? Title,
    string? Angle,
    string? Audience,
    string? Cta,
    Guid? PackageId,
    string? Outline = null);

public sealed record ContentPackageDto(
    Guid Id,
    Guid BrandId,
    string BrandCode,
    string BrandName,
    Guid TopicId,
    string Title,
    string? Angle,
    string? Audience,
    string ContentType,
    string? Pillar,
    string Goal,
    string Priority,
    string Status,
    Guid? SourcePackageId,
    string? SourceTitle,
    DateTimeOffset? DisplayAt,
    int VariantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ContentCoreIdeaDto? CoreIdea = null,
    IReadOnlyList<ContentBrandFitDto>? BrandFits = null,
    int AdaptationCount = 0,
    ContentQualityGateDto? QualityGate = null,
    ContentCreativeBriefDto? CreativeBrief = null);

public sealed record ContentPackageDetailDto(
    ContentPackageDto Package,
    ContentTopicDetailDto TopicDetail,
    IReadOnlyList<ContentPackageDto> Adaptations);

public sealed record UpsertContentPackageRequest(
    Guid BrandId,
    string Title,
    string? Angle,
    string? Audience,
    string? ContentType,
    string? Pillar,
    string? Goal,
    string? Priority,
    string? BodyOutline,
    DateTimeOffset? DisplayAt,
    string? CtaUrl,
    string? Insight = null,
    string? Problem = null,
    string? CoreMessage = null,
    List<string>? Keywords = null,
    string? Source = null,
    string? SourceUrl = null,
    string? SourceType = null,
    string? Evidence = null,
    string? FactOrOpinion = null,
    ContentCreativeBriefDto? CreativeBrief = null);

public sealed record AnalyzeAdaptRequest(
    IReadOnlyList<Guid>? BrandIds = null,
    bool IncludeMaybe = true,
    bool GenerateFits = false,
    bool CreatePackages = true,
    bool IncludeSourceBrand = false);

public sealed record PoolIdeaDraft(
    string Title,
    string? Insight = null,
    string? Problem = null,
    string? CoreMessage = null,
    string? Angle = null,
    string? Audience = null,
    string? Goal = null,
    string? Source = null,
    string? SourceUrl = null,
    string? SourceType = null,
    string? Evidence = null,
    string? FactOrOpinion = null);

public sealed record CreatePoolIdeasRequest(
    Guid? HomeBrandId = null,
    IReadOnlyList<PoolIdeaDraft>? Ideas = null);

public sealed record CreatePoolIdeasResultDto(
    IReadOnlyList<ContentPackageDto> Packages,
    string? Message);

public sealed record AnalyzePoolRequest(
    IReadOnlyList<Guid> PackageIds,
    IReadOnlyList<Guid>? BrandIds = null,
    bool IncludeMaybe = true);

public sealed record AnalyzePoolResultDto(
    IReadOnlyList<EnqueueWorkResultDto> Jobs,
    string Message);

public sealed record ApplyPoolFitItem(Guid PackageId, Guid BrandId);

public sealed record ApplyPoolFitsRequest(
    IReadOnlyList<ApplyPoolFitItem> Items,
    bool GenerateFits = false,
    IReadOnlyList<string>? VariantKinds = null);

public sealed record ApplyPoolFitsResultDto(
    int Requested,
    int Created,
    int Skipped,
    IReadOnlyList<ContentBrandFitDto> Fits,
    string? Message);

public sealed record SuggestPoolIdeasRequest(
    int Limit = 6,
    IReadOnlyList<Guid>? PackageIds = null);

public sealed record SuggestPoolIdeaDto(
    string Title,
    string? Insight,
    string? Problem,
    string? CoreMessage,
    string? WhyNext,
    string? FromTitle,
    Guid? FromPackageId,
    string? Gap,
    string? SuggestedBrands,
    string? FactOrOpinion);

public sealed record SuggestPoolIdeasResultDto(
    IReadOnlyList<SuggestPoolIdeaDto> Ideas,
    string? Message);

public sealed record AdaptContentPackageRequest(
    Guid TargetBrandId,
    string? Title,
    string? Angle,
    string? BodyOutline,
    DateTimeOffset? DisplayAt);

public sealed record BatchApprovePackagesRequest(IReadOnlyList<Guid> PackageIds);

public sealed record BatchApprovePackagesResultDto(
    int Requested,
    int Approved,
    IReadOnlyList<Guid> FailedIds,
    string? Message);

public sealed record UpsertContentTopicRequest(
    Guid BrandId,
    string Title,
    string? Pillar,
    string? Goal,
    string? CtaUrl,
    string? UtmCampaign,
    string? Priority,
    string? Status,
    string? BodyOutline,
    DateTimeOffset? DisplayAt);

public sealed record ContentBudgetSnapshotDto(
    decimal GlobalCeilingUsd,
    decimal GlobalSpendUsd,
    decimal GlobalRemainingUsd,
    string DefaultImageTier,
    IReadOnlyList<ContentBrandBudgetDto> Brands);

public sealed record ContentBrandBudgetDto(
    Guid BrandId,
    string BrandCode,
    string BrandName,
    decimal EffectiveCeilingUsd,
    decimal SpendUsd,
    decimal RemainingUsd,
    string EffectiveImageTier,
    bool PauseWhenExceeded);

public interface IContentOrgSettingsService
{
    Task<ContentOrgSettingsDto> GetAsync(CancellationToken cancellationToken = default);
    Task<ContentOrgSettingsDto> UpdateAsync(UpdateContentOrgSettingsRequest request, CancellationToken cancellationToken = default);
    Task<ContentBudgetSnapshotDto> GetBudgetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<ContentAiTestResultDto> TestAiAsync(CancellationToken cancellationToken = default);
    Task<ContentVideoTestResultDto> TestVideoAsync(CancellationToken cancellationToken = default);
    Task<ContentFacebookTestResultDto> TestFacebookAsync(CancellationToken cancellationToken = default);
}

public interface IContentSeriesTurboService
{
    Task<ContentSeriesTurboTaskDto> StartAsync(
        ContentSeriesTurboStartRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentSeriesTurboTaskDto> GetAsync(string taskId, CancellationToken cancellationToken = default);

    Task<ContentSeriesTurboTaskDto> StartLipsyncAsync(
        ContentSeriesLipsyncStartRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ContentSeriesTakeProxyRequest(string Url);

public sealed record ContentSeriesAssembleVoiceDto(
    string LineId,
    double StartSec,
    string AudioBase64,
    string? Mime = null);

public sealed record ContentSeriesAssembleClipDto(
    string Code,
    string? VideoUrl,
    double Seconds,
    IReadOnlyList<ContentSeriesAssembleVoiceDto> Voices,
    double UsableStart = 0,
    double? UsableEnd = null,
    bool UseVideoAudio = false,
    bool RequireVoice = false,
    string? StillBase64 = null);

public sealed record ContentSeriesAssembleMixSfxDto(
    string AssetId,
    double StartSec,
    double GainDb = 0);

public sealed record ContentSeriesAssembleMixDto(
    bool Room = false,
    bool Music = false,
    bool Loudnorm = true,
    string? RoomId = null,
    string? MusicId = null,
    IReadOnlyList<ContentSeriesAssembleMixSfxDto>? Sfx = null,
    bool Grade = false,
    bool ColorMatch = false,
    bool Interpolate = false);

public sealed record ContentSeriesAssembleRequest(
    string FileStem,
    IReadOnlyList<ContentSeriesAssembleClipDto> Clips,
    string? Aspect = null,
    ContentSeriesAssembleMixDto? Mix = null);

public interface IContentSeriesAssembleService
{
    Task<(byte[] Bytes, string ContentType, string FileName)> AssembleAsync(
        ContentSeriesAssembleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ContentSeriesTakeProbeDto(
    bool Ok,
    string? Mime,
    long? Bytes,
    string? Error);

public interface IContentSeriesTakeProxyService
{
    Task<(byte[] Bytes, string ContentType, string FileName)> FetchAsync(
        string url,
        CancellationToken cancellationToken = default);

    Task<ContentSeriesTakeProbeDto> ProbeAsync(
        string url,
        CancellationToken cancellationToken = default);
}

public interface IContentSeriesStillService
{
    Task<ContentSeriesStillDto> GenerateAsync(
        ContentSeriesStillRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentSeriesKfNoteDto> RewriteNoteAsync(
        ContentSeriesKfNoteRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentSeriesStillQaDto> QaAsync(
        ContentSeriesStillQaRequest request,
        CancellationToken cancellationToken = default);
}

public interface IContentSeriesScriptDraftService
{
    Task<ContentSeriesScriptDraftDto> DraftAsync(
        ContentSeriesScriptDraftRequest request,
        CancellationToken cancellationToken = default);
}

public interface IContentFacebookConnectionService
{
    Task<ContentFacebookStartDto> StartAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<ContentFacebookPendingDto> CompleteAsync(string code, string state, CancellationToken cancellationToken = default);
    Task<ContentFacebookPendingDto?> GetPendingAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<ContentChannelTargetDto> SelectPageAsync(string sessionId, string pageId, CancellationToken cancellationToken = default);
    Task<ContentFacebookVerifyDto> VerifyAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task<ContentChannelTargetDto> DisconnectAsync(Guid channelId, CancellationToken cancellationToken = default);
    Task MarkNeedReconnectAsync(Guid channelId, string error, CancellationToken cancellationToken = default);
}

public interface IContentBrandService
{
    Task<IReadOnlyList<ContentBrandDto>> ListAsync(bool? activeOnly = true, CancellationToken cancellationToken = default);
    Task<ContentBrandDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentBrandDto> CreateAsync(UpsertContentBrandRequest request, CancellationToken cancellationToken = default);
    Task<ContentBrandDto?> UpdateAsync(Guid id, UpsertContentBrandRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentSiteTargetDto>> ListSitesAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<ContentSiteTargetDto> UpsertSiteAsync(Guid brandId, UpsertContentSiteTargetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentChannelTargetDto>> ListChannelsAsync(Guid brandId, CancellationToken cancellationToken = default);
    Task<ContentChannelTargetDto> UpsertChannelAsync(Guid brandId, UpsertContentChannelTargetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentWritePlanDto>> ListWritePlansAsync(Guid? brandId = null, CancellationToken cancellationToken = default);
}

public interface IContentTopicService
{
    Task<IReadOnlyList<ContentTopicDto>> ListAsync(Guid? brandId, string? status, CancellationToken cancellationToken = default);
    Task<ContentTopicDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentTopicDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentTopicDto> CreateAsync(UpsertContentTopicRequest request, CancellationToken cancellationToken = default);
    Task<ContentTopicDto?> UpdateAsync(Guid id, UpsertContentTopicRequest request, CancellationToken cancellationToken = default);
    Task<ContentTopicDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SelectAssetAsync(Guid topicId, Guid assetId, CancellationToken cancellationToken = default);
    Task<ContentAssetDto?> UploadAssetAsync(
        Guid topicId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public interface IContentGenerateService
{
    Task<GenerateContentResultDto> GenerateAsync(
        Guid topicId,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ContentLocalOsPublishRequest(
    Guid TopicId,
    string Title,
    string BodyMarkdown,
    string? SeoDescription,
    string? BrandName,
    string? BrandCode,
    byte[]? CoverBytes = null,
    string? CoverContentType = null,
    string? CoverFileName = null);

public sealed record ContentLocalOsPublishResult(Guid ListingId, string PublicPath, string ResultJson);

public interface IContentLocalOsPublisher
{
    Task<ContentLocalOsPublishResult> PublishArticleAsync(
        ContentLocalOsPublishRequest request,
        CancellationToken cancellationToken = default);
}

public interface IContentPublishService
{
    Task<PublishContentResultDto> PublishAsync(
        Guid topicId,
        PublishContentRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentPublishJobDto?> RunJobAsync(
        Guid jobId,
        PublishContentRequest? mediaRequest = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentPublishJobDto>> ListJobsAsync(Guid? topicId, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string ContentType, string FileName)?> GetAssetFileAsync(Guid assetId, CancellationToken cancellationToken = default);
}

public interface IContentPackageService
{
    Task<IReadOnlyList<ContentPackageDto>> ListAsync(
        Guid? brandId,
        string? status,
        bool coresOnly = false,
        CancellationToken cancellationToken = default);

    Task<ContentPackageDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentPackageDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ContentPackageDto> CreateAsync(UpsertContentPackageRequest request, CancellationToken cancellationToken = default);
    Task<ContentPackageDto?> UpdateAsync(Guid id, UpsertContentPackageRequest request, CancellationToken cancellationToken = default);

    /// <summary>B2 — generate all destination variants for the package topic.</summary>
    Task<GenerateContentResultDto> GenerateAllAsync(
        Guid id,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>B3 — clone idea to another brand (Draft package; does not auto-generate).</summary>
    Task<ContentPackageDto> AdaptAsync(
        Guid id,
        AdaptContentPackageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Core idea → brand-fit scores → create a distinct adaptation package per fitting brand.
    /// Does not copy the same article. Skips brands with verdict skip.
    /// </summary>
    Task<IReadOnlyList<ContentBrandFitDto>> AnalyzeAndAdaptAsync(
        Guid id,
        AnalyzeAdaptRequest request,
        CancellationToken cancellationToken = default);

    Task<CreatePoolIdeasResultDto> CreatePoolAsync(
        CreatePoolIdeasRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplyPoolFitsResultDto> ApplyPoolFitsAsync(
        ApplyPoolFitsRequest request,
        CancellationToken cancellationToken = default);

    Task<SuggestPoolIdeasResultDto> SuggestPoolIdeasAsync(
        SuggestPoolIdeasRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentPackageDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BatchApprovePackagesResultDto> ApproveBatchAsync(
        BatchApprovePackagesRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentPackageDto?> UpdateBriefAsync(
        Guid id,
        ContentCreativeBriefDto brief,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentPerformanceDto>> ListPerformanceAsync(
        Guid packageId,
        CancellationToken cancellationToken = default);

    Task<ContentPerformanceDto> IngestPerformanceAsync(
        Guid packageId,
        IngestContentPerformanceRequest request,
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string FileName)> ExportManualPackAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public sealed record ContentVideoTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string Provider,
    string? ExternalTemplateId,
    string AspectRatio,
    int DurationSec,
    string? Description,
    string ConfigJson,
    bool IsActive,
    int SortOrder);

public sealed record ContentVideoJobDto(
    Guid Id,
    Guid BrandId,
    string BrandCode,
    string BrandName,
    Guid? PackageId,
    Guid? TopicId,
    Guid TemplateId,
    string TemplateCode,
    string TemplateName,
    string Title,
    string ScriptBody,
    string Status,
    string Provider,
    string? ExternalRenderId,
    string? PreviewUrl,
    string? OutputUrl,
    string? ErrorMessage,
    string StoryboardJson,
    string ConfigJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? RenderedAt);

public sealed record CreateVideoJobFromPackageRequest(
    Guid PackageId,
    Guid? TemplateId = null,
    string? TemplateCode = null);

public sealed record UpdateVideoJobScriptRequest(string ScriptBody);

public sealed record RunVideoMvpPipelineRequest(
    bool GenerateImages = true,
    bool GenerateVoice = true,
    bool Render = true);

public interface IContentVideoService
{
    Task<IReadOnlyList<ContentVideoTemplateDto>> ListTemplatesAsync(
        bool? activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentVideoJobDto>> ListJobsAsync(
        Guid? brandId,
        string? status,
        CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> GetJobAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tạo job từ package — kéo variant tiktok_script (fallback social_caption / web_long).</summary>
    Task<ContentVideoJobDto> CreateFromPackageAsync(
        CreateVideoJobFromPackageRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> UpdateScriptAsync(
        Guid id,
        UpdateVideoJobScriptRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Parse beats → storyboard_json; status Ready (local) hoặc giữ Draft nếu lỗi parse.</summary>
    Task<ContentVideoJobDto?> PrepareStoryboardAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// MVP V1: storyboard → (optional) scene image URLs → (optional) ElevenLabs voice → Creatomate render.
    /// </summary>
    Task<ContentVideoJobDto?> RunMvpPipelineAsync(
        Guid id,
        RunVideoMvpPipelineRequest? request = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creatomate nếu có API key + template; không thì = PrepareStoryboard.</summary>
    Task<ContentVideoJobDto?> QueueRenderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> RefreshRenderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> ApplyCreatomateWebhookAsync(
        string renderId,
        string status,
        string? url,
        string? snapshotUrl,
        CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ContentOptions
{
    public const string SectionName = "Content";

    /// <summary>Gemini / Google AI Studio key. Falls back to env GEMINI_API_KEY.</summary>
    public string? GeminiApiKey { get; set; }

    public string TextModel { get; set; } = "gemini-3.6-flash";
    public string? ImageModel { get; set; }
    public string AssetRoot { get; set; } = "App_Data/content-assets";
    /// <summary>Phase 05 image provider id. Env KIT_VIDEO_IMAGE_PROVIDER. Never a secret.</summary>
    public string KitVideoImageProvider { get; set; } = "gemini";
    /// <summary>Optional image model override. Env KIT_VIDEO_GEMINI_MODEL. Empty = Content:ImageModel / org AI config.</summary>
    public string? KitVideoGeminiModel { get; set; }
    public int KitVideoGeminiTimeoutSeconds { get; set; } = 90;
    public string KitVideoKeyframeRoot { get; set; } = "App_Data/kit-video-kf";
    public string KitVideoMasterRoot { get; set; } = "App_Data/kit-video-master";
    public string KitVideoIdentityRoot { get; set; } = "App_Data/kit-video-identity";
    public string KitVideoStressRoot { get; set; } = "App_Data/kit-video-identity-stress";
    public string KitVideoVideoRoot { get; set; } = "App_Data/kit-video-i2v";
    public string KitVideoRefRoot { get; set; } = "";
    public string FamixaVisualStyle { get; set; } = "FAMIXA_VISUAL_STYLE_V1";

    /// <summary>Optional Creatomate API key. Falls back to env CREATOMATE_API_KEY.</summary>
    public string? CreatomateApiKey { get; set; }

    /// <summary>Optional Runway API key (Famixa Series Turbo test). Falls back to env RUNWAY_API_KEY.</summary>
    public string? RunwayApiKey { get; set; }

    /// <summary>Optional Fal API key (Wan 2.1 I2V). Falls back to env FAL_KEY.</summary>
    public string? FalApiKey { get; set; }

    /// <summary>Optional ElevenLabs API key. Falls back to env ELEVENLABS_API_KEY.</summary>
    public string? ElevenLabsApiKey { get; set; }

    /// <summary>ElevenLabs voice id (default Rachel-like public voice if empty).</summary>
    public string? ElevenLabsVoiceId { get; set; }

    /// <summary>
    /// Public base URL so Creatomate can fetch generated voice/images (e.g. https://api.example.com).
    /// Localhost is not reachable by Creatomate — use tunnel or CDN in production.
    /// </summary>
    public string? PublicMediaBaseUrl { get; set; }

    public string VideoAssetRoot { get; set; } = "App_Data/content-video";

    /// <summary>Background worker for generate / publish / video. Off in tests if needed.</summary>
    public bool WorkerEnabled { get; set; } = true;

    public int WorkerPollSeconds { get; set; } = 2;

    public int WorkerMaxRetries { get; set; } = 3;

    public string WorkAssetRoot { get; set; } = "App_Data/content-work";

    /// <summary>Meta app id for Facebook Login. Falls back to env FACEBOOK_APP_ID.</summary>
    public string? FacebookAppId { get; set; }

    /// <summary>Meta app secret. Falls back to env FACEBOOK_APP_SECRET. Never returned on GET.</summary>
    public string? FacebookAppSecret { get; set; }

    /// <summary>OAuth redirect — must match Valid OAuth Redirect URIs on the Meta app.</summary>
    public string? FacebookRedirectUri { get; set; }
}

public static class ContentWorkKinds
{
    public const string GenerateTopic = "generate_topic";
    public const string GeneratePackage = "generate_package";
    public const string PublishTopic = "publish_topic";
    public const string VideoMvp = "video_mvp";
    public const string VideoRender = "video_render";
    public const string BrandAdapt = "brand_adapt";
}

public static class ContentWorkStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed record ContentWorkJobDto(
    Guid Id,
    string Kind,
    string Status,
    Guid? BrandId,
    string? BrandCode,
    string? BrandName,
    Guid? TopicId,
    Guid? PackageId,
    Guid? VideoJobId,
    string? Title,
    string? ErrorMessage,
    int RetryCount,
    int MaxRetries,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Message);

public sealed record EnqueueWorkResultDto(
    ContentWorkJobDto Job,
    string Message);

public sealed record ContentOpsBrandRowDto(
    Guid BrandId,
    string BrandCode,
    string BrandName,
    int ReviewCount,
    int ScheduledCount,
    int PublishedMonthCount,
    decimal SpendUsd);

public sealed record ContentOpsSnapshotDto(
    int ReviewCount,
    int GeneratingCount,
    int ScheduledCount,
    int PublishedTodayCount,
    int ErrorCount,
    decimal MonthSpendUsd,
    decimal MonthCeilingUsd,
    IReadOnlyList<ContentOpsBrandRowDto> Brands,
    IReadOnlyList<ContentWorkJobDto> ActiveJobs,
    int CoreIdeaCount,
    int CoreDraftCount,
    int CoreUnscoredCount,
    int AdaptationCount,
    int ScheduledThisWeek,
    int PublishedThisWeek,
    IReadOnlyList<ContentPackageDto> CoreIdeas,
    IReadOnlyList<ContentCalendarItemDto> WeekItems,
    IReadOnlyList<ContentWorkJobDto> RecentErrors,
    int BudgetBlockedCount,
    bool FacebookAppConfigured,
    IReadOnlyList<ContentOpsFailedPublishDto> FailedPublishJobs);

public sealed record ContentOpsFailedPublishDto(
    Guid JobId,
    Guid TopicId,
    string TopicTitle,
    string ConnectorType,
    string? LastError,
    DateTimeOffset UpdatedAt);

public sealed record ContentCalendarItemDto(
    DateTimeOffset At,
    string Kind,
    Guid? PackageId,
    Guid? TopicId,
    Guid? PublishJobId,
    Guid BrandId,
    string BrandCode,
    string BrandName,
    string Title,
    string? Channel,
    string Status);

public interface IContentOpsService
{
    Task<ContentOpsSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentCalendarItemDto>> ListCalendarAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? brandId,
        CancellationToken cancellationToken = default);
}

public interface IContentWorkQueueService
{
    Task<EnqueueWorkResultDto> EnqueueGenerateTopicAsync(
        Guid topicId,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default);

    Task<EnqueueWorkResultDto> EnqueueGeneratePackageAsync(
        Guid packageId,
        GenerateContentRequest request,
        CancellationToken cancellationToken = default);

    Task<EnqueueWorkResultDto> EnqueuePublishTopicAsync(
        Guid topicId,
        PublishContentRequest request,
        CancellationToken cancellationToken = default);

    Task<EnqueueWorkResultDto> EnqueueVideoMvpAsync(
        Guid videoJobId,
        RunVideoMvpPipelineRequest request,
        CancellationToken cancellationToken = default);

    Task<EnqueueWorkResultDto> EnqueueVideoRenderAsync(
        Guid videoJobId,
        CancellationToken cancellationToken = default);

    Task<EnqueueWorkResultDto> EnqueueBrandAdaptAsync(
        Guid packageId,
        AnalyzeAdaptRequest request,
        CancellationToken cancellationToken = default);

    Task<AnalyzePoolResultDto> EnqueueBrandAdaptBatchAsync(
        AnalyzePoolRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentWorkJobDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentWorkJobDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Claim and run at most one due work job. Returns true if a job was processed.</summary>
    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);

    /// <summary>Run leftover <c>publish_job</c> rows that are due (old sync / scheduled path).</summary>
    Task<int> ProcessDuePublishJobsAsync(int limit = 3, CancellationToken cancellationToken = default);
}

public sealed record FamixaCharacterRefDto(string Kind, string Path, string? Label = null);

public sealed record FamixaCharacterDto(
    Guid Id,
    string CharacterCode,
    string Name,
    string Role,
    string Universe,
    string Visual,
    string Lifecycle,
    Guid? CurrentVersionId,
    string CurrentEra,
    string Version,
    bool IsCurrentCanon,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    JsonElement Canon,
    IReadOnlyList<FamixaCharacterRefDto> References,
    DateTimeOffset UpdatedAt);

public sealed record UpsertFamixaCharacterCanonRequest(
    JsonElement Canon,
    bool Unlock = false);

public sealed record CreateFamixaCharacterRequest(
    string? CharacterCode = null,
    string Name = "",
    string? Role = null,
    string? Gender = null,
    int? InitialAge = null,
    string Universe = "CORE",
    string Visual = "frame",
    string? Description = null,
    string? FamilyRole = null,
    bool AllowNew = false,
    bool ForceCreate = false);

public sealed record FamixaCharacterDuplicateRequest(
    string Name,
    string? Role = null,
    string? FamilyRole = null);

public sealed record FamixaCharacterDuplicateHitDto(
    string CharacterCode,
    string Name,
    string Role,
    string Lifecycle);

public sealed record FamixaCharacterDuplicateDto(
    bool Duplicate,
    string Preferred,
    string Message,
    IReadOnlyList<FamixaCharacterDuplicateHitDto> Existing);

public sealed record FamixaCharacterAuditDto(
    Guid Id,
    string CharacterCode,
    string Version,
    string FieldChanged,
    string? OldValue,
    string? NewValue,
    string? ChangedBy,
    DateTimeOffset ChangedAt,
    string? Reason,
    string? Approval);

public sealed record FamixaCharacterVersionDto(
    Guid Id,
    string Version,
    string Era,
    string Status,
    bool IsCurrentCanon,
    DateTimeOffset CreatedAt);

public sealed record FamixaCharacterGuardRequest(
    IReadOnlyList<string> CharacterIds,
    IReadOnlyDictionary<string, bool>? HasDialogue = null,
    IReadOnlyDictionary<string, string>? VoiceIds = null,
    IReadOnlyList<string>? UnknownNames = null);

public sealed record FamixaCharacterGuardItemDto(
    string CharacterCode,
    string Lifecycle,
    string Era,
    string Version,
    string? WardrobeId,
    IReadOnlyList<string> RefIds,
    bool FrontOk);

public sealed record FamixaCharacterGuardDto(
    bool Ok,
    IReadOnlyList<string> Blocked,
    IReadOnlyList<FamixaCharacterGuardItemDto> Resolved,
    string CompilerVersion);

public interface IFamixaCharacterService
{
    Task<IReadOnlyList<FamixaCharacterDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> GetAsync(string characterCode, CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> CreateDraftAsync(
        CreateFamixaCharacterRequest request,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> PutCanonAsync(
        string characterCode,
        UpsertFamixaCharacterCanonRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> ApproveAsync(
        string characterCode,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> LockAsync(
        string characterCode,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> UnlockAsync(
        string characterCode,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> PutReferencesAsync(
        string characterCode,
        IReadOnlyList<FamixaCharacterRefDto> references,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterGuardDto> GuardAsync(
        FamixaCharacterGuardRequest request,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDuplicateDto> DetectDuplicateAsync(
        FamixaCharacterDuplicateRequest request,
        CancellationToken cancellationToken = default);
    Task<FamixaCharacterDto> CreateVersionAsync(
        string characterCode,
        string? actor = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamixaCharacterAuditDto>> ListAuditAsync(
        string characterCode,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FamixaCharacterVersionDto>> ListVersionsAsync(
        string characterCode,
        CancellationToken cancellationToken = default);
}

public sealed record KitVideoUniverseDto(
    Guid Id,
    string UniverseCode,
    string Name,
    string Status,
    JsonElement World);

public sealed record KitVideoProjectDto(
    Guid Id,
    string ProjectCode,
    string Name,
    string BrandCode,
    string Status,
    JsonElement Visual,
    JsonElement Rules,
    IReadOnlyList<KitVideoUniverseDto> Universes);

public sealed record KitVideoShotStateDto(
    string ShotCode,
    string State,
    bool Failed,
    string? LastProvider = null,
    string? LastFailureCode = null);

public sealed record KitVideoProductionDto(
    Guid Id,
    string ProjectCode,
    string UniverseCode,
    string ProductionCode,
    string Title,
    string State,
    string RunStatus,
    Guid? SeriesBuildId,
    IReadOnlyList<KitVideoShotStateDto> Shots,
    DateTimeOffset UpdatedAt);

public sealed record KitVideoProviderTaskDto(
    Guid Id,
    Guid AttemptId,
    string Provider,
    string ProviderTaskId,
    string ProviderStatus,
    string? FailureCode,
    string? OutputUrl,
    DateTimeOffset CreatedAt);

public sealed record KitVideoGenerationAttemptDto(
    Guid Id,
    int AttemptNo,
    string Status,
    string? Error,
    DateTimeOffset CreatedAt,
    IReadOnlyList<KitVideoProviderTaskDto> ProviderTasks);

public sealed record KitVideoGenerationJobDto(
    Guid JobId,
    Guid ProjectId,
    Guid ProductionId,
    string SceneId,
    string ShotId,
    string Provider,
    string Operation,
    string Status,
    string IdempotencyKey,
    bool Confirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<KitVideoGenerationAttemptDto> Attempts);

public sealed record CreateKitVideoJobRequest(
    Guid ProductionId,
    string ShotCode,
    string Provider,
    string Operation,
    string IdempotencyKey,
    bool Confirmed = false,
    string? SceneCode = null,
    bool? HasAction = null);

public sealed record CompleteKitVideoJobRequest(
    string ProviderStatus,
    string? ProviderTaskId = null,
    string? FailureCode = null,
    string? OutputUrl = null,
    int? HttpStatus = null,
    bool? FileExists = null,
    bool? Readable = null,
    bool? ContainerOk = null,
    bool? ProbeOk = null,
    string? ProbeError = null,
    double? DurationSec = null,
    int? Width = null,
    int? Height = null);

public sealed record RetryKitVideoJobRequest(bool Confirmed = false);

public sealed record CreateKitVideoProductionRequest(
    string ProjectCode,
    string UniverseCode,
    string ProductionCode,
    string? Title = null,
    Guid? SeriesBuildId = null);

public sealed record TransitionKitVideoProductionRequest(string ToState, string? Reason = null);

public sealed record UpsertKitVideoShotStateRequest(
    string ToState,
    bool? Failed = null,
    string? LastProvider = null,
    string? LastFailureCode = null);

public interface IKitVideoEngineService
{
    Task<IReadOnlyList<KitVideoProjectDto>> ListProjectsAsync(CancellationToken cancellationToken = default);
    Task<KitVideoProjectDto> GetProjectAsync(string projectCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitVideoProductionDto>> ListProductionsAsync(
        string? projectCode = null,
        CancellationToken cancellationToken = default);
    Task<KitVideoProductionDto> GetProductionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoProductionDto> EnsureProductionAsync(
        CreateKitVideoProductionRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<KitVideoProductionDto> TransitionProductionAsync(
        Guid id,
        TransitionKitVideoProductionRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<KitVideoProductionDto> UpsertShotAsync(
        Guid productionId,
        string shotCode,
        UpsertKitVideoShotStateRequest request,
        CancellationToken cancellationToken = default);
}

public interface IKitVideoJobService
{
    Task<IReadOnlyList<KitVideoGenerationJobDto>> ListAsync(
        Guid productionId,
        CancellationToken cancellationToken = default);
    Task<KitVideoGenerationJobDto> GetAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<KitVideoGenerationJobDto> EnsureAsync(
        CreateKitVideoJobRequest request,
        CancellationToken cancellationToken = default);
    Task<KitVideoGenerationJobDto> ConfirmAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<KitVideoGenerationJobDto> CompleteAsync(
        Guid jobId,
        CompleteKitVideoJobRequest request,
        CancellationToken cancellationToken = default);
    Task<KitVideoGenerationJobDto> RetryAsync(
        Guid jobId,
        RetryKitVideoJobRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record KitVideoAssetRefDto(string Kind, string Path, bool IsPrimary, bool IsSecondary, string QaStatus);

public sealed record KitVideoAssetEraDto(string Era, int? Age, string Status);

public sealed record KitVideoAssetDto(
    Guid Id,
    string ProjectCode,
    string AssetCode,
    string AssetKind,
    string Name,
    string Lifecycle,
    string Version,
    string Era,
    JsonElement Canon,
    IReadOnlyList<KitVideoAssetRefDto> References,
    IReadOnlyList<KitVideoAssetEraDto> Eras);

public sealed record KitVideoAssetUsageDto(
    string AssetCode,
    int ShotCount,
    string Warning,
    IReadOnlyList<string> Shots);

public sealed record KitVideoShotSpecDto(
    string ShotCode,
    string SceneCode,
    IReadOnlyList<string> RequiredCharacters,
    string? Era = null,
    string? RequiredLocation = null,
    IReadOnlyList<string>? RequiredProps = null,
    IReadOnlyDictionary<string, string>? RequiredWardrobe = null,
    string? Action = null,
    string? Camera = null,
    string? Speaker = null,
    string? Emotion = null,
    string? Dialogue = null,
    string? I2vImageSource = null);

public sealed record KitVideoPreviousStateDto(
    string ShotCode,
    bool Approved,
    JsonElement? Characters = null,
    string? Location = null,
    string? Lighting = null);

public sealed record KitVideoSceneMasterDto(
    string SceneCode,
    string Status,
    string LocationCode,
    JsonElement? Canon = null,
    JsonElement? Presence = null);

public sealed record KitVideoShotPackageDto(
    string ShotCode,
    string SceneCode,
    IReadOnlyList<KitVideoAssetDto> Characters,
    IReadOnlyList<KitVideoAssetDto> Locations,
    IReadOnlyList<KitVideoAssetDto> Props,
    IReadOnlyList<KitVideoAssetDto> Wardrobe,
    IReadOnlyList<string> Blocked,
    IReadOnlyList<string> Missing,
    KitVideoSceneMasterDto? SceneMaster = null,
    string? PreviousShot = null);

public sealed record KitVideoCompiledPromptDto(bool Ok, string Prompt, bool HasDialogue, string I2vSource, string? Blocked = null);

public sealed record KitVideoResolveRequest(
    string ProjectCode,
    KitVideoShotSpecDto Spec,
    KitVideoSceneMasterDto? SceneMaster = null,
    KitVideoPreviousStateDto? Previous = null);

public sealed record KitVideoPreflightDto(bool Ok, IReadOnlyList<string> Blocked, IReadOnlyList<string> Missing);

public sealed record CreateKitVideoAssetVersionRequest(string? Reason = null);

public sealed record UpsertKitVideoAssetCanonRequest(JsonElement Canon);

public sealed record UpsertKitVideoSceneMasterRequest(
    Guid ProductionId,
    string SceneCode,
    string LocationCode,
    string Status,
    JsonElement? Canon = null,
    JsonElement? Presence = null);

public sealed record ApproveKitVideoSnapshotRequest(Guid ProductionId, string ShotCode, JsonElement Snapshot);

public interface IKitVideoAssetService
{
    Task<IReadOnlyList<KitVideoAssetDto>> ListAsync(string? projectCode = null, CancellationToken cancellationToken = default);
    Task<KitVideoAssetDto> GetAsync(string projectCode, string assetCode, CancellationToken cancellationToken = default);
    Task<KitVideoAssetDto> CreateVersionAsync(
        string projectCode,
        string assetCode,
        CreateKitVideoAssetVersionRequest request,
        string? actor = null,
        CancellationToken cancellationToken = default);
    Task<KitVideoAssetDto> PutCanonAsync(
        string projectCode,
        string assetCode,
        UpsertKitVideoAssetCanonRequest request,
        CancellationToken cancellationToken = default);
    Task<KitVideoAssetDto> SetLifecycleAsync(
        string projectCode,
        string assetCode,
        string lifecycle,
        CancellationToken cancellationToken = default);
    Task<KitVideoAssetUsageDto> UsageAsync(
        string projectCode,
        string assetCode,
        CancellationToken cancellationToken = default);
    Task<KitVideoShotPackageDto> ResolveAsync(KitVideoResolveRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoPreflightDto> PreflightAsync(KitVideoResolveRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoCompiledPromptDto> CompileAsync(KitVideoResolveRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoKeyframeQa> QaAsync(KitVideoKeyframeQaInput request, CancellationToken cancellationToken = default);
    Task<KitVideoSceneMasterDto> UpsertSceneMasterAsync(
        UpsertKitVideoSceneMasterRequest request,
        CancellationToken cancellationToken = default);
    Task ApproveSnapshotAsync(ApproveKitVideoSnapshotRequest request, CancellationToken cancellationToken = default);
}

public sealed record KitVideoStoryCompileRequest(Guid ProductionId, string Script);

public sealed record KitVideoStoryGraphDto(
    Guid ProductionId,
    string ScriptHash,
    JsonElement Graph,
    IReadOnlyList<string> Unassigned,
    IReadOnlyList<KitVideoContinuityOverrideDto> Overrides);

public sealed record KitVideoContinuityOverrideDto(string ShotId, string Reason, string ApprovedBy, DateTimeOffset CreatedAt);

public sealed record KitVideoContinuityValidateRequest(JsonElement Shot, JsonElement? PreviousSnapshot = null);

public sealed record KitVideoStoryGateRequest(JsonElement Shot, IReadOnlyList<string>? DetectedCharacters = null);

public sealed record KitVideoContinuityOverrideRequest(Guid ProductionId, string ShotId, string Reason);

public sealed record KitVideoScriptImpactRequest(string PreviousScript, string NextScript);

public interface IKitVideoContinuityService
{
    Task<KitVideoStoryGraphDto?> GetAsync(Guid productionId, CancellationToken cancellationToken = default);
    Task<KitVideoStoryGraphDto> CompileAsync(KitVideoStoryCompileRequest request, CancellationToken cancellationToken = default);
    KitVideoContinuityResult Validate(KitVideoContinuityValidateRequest request);
    KitVideoStoryGateResult Gate(KitVideoStoryGateRequest request);
    Task<KitVideoContinuityOverrideDto> OverrideAsync(
        KitVideoContinuityOverrideRequest request,
        string? approvedBy,
        CancellationToken cancellationToken = default);
    KitVideoScriptImpactResult Impact(KitVideoScriptImpactRequest request);
}

public sealed record KitVideoVisualCompileRequest(JsonElement Contract, string? ProjectStyle = null, string? Dialogue = null);

public sealed record KitVideoVisionEvaluateRequest(JsonElement Contract, JsonElement Observation);

public sealed record KitVideoRecordAttemptRequest(
    Guid ProductionId,
    string ShotCode,
    string Fingerprint,
    bool StrategyChanged = false,
    string? PromptJson = null,
    string? ContractJson = null,
    KitVideoVisionQaDto? Qa = null);

public sealed record KitVideoDirectorDecideRequest(Guid ProductionId, string ShotCode, Guid AttemptId, string Decision);

public sealed record KitVideoDirectorFeedbackRequest(string Note);

public sealed record KitVideoKeyframeAttemptDto(
    Guid AttemptId,
    Guid ProductionId,
    string ShotCode,
    int AttemptNo,
    string Status,
    string Fingerprint,
    KitVideoVisionQaDto? Qa = null,
    KitVideoRepairDto? Repair = null,
    string? ImagePath = null,
    DateTimeOffset CreatedAt = default);

public interface IKitVideoVisionService
{
    KitVideoGenerationRequestDto Compile(KitVideoVisualCompileRequest request);
    KitVideoVisionQaDto Evaluate(KitVideoVisionEvaluateRequest request);
    KitVideoRepairDto Diagnose(KitVideoVisionQaDto qa);
    string Revision(string note);
    Task<KitVideoKeyframeAttemptDto> RecordAttemptAsync(KitVideoRecordAttemptRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitVideoKeyframeAttemptDto>> ListAttemptsAsync(Guid productionId, string shotCode, CancellationToken cancellationToken = default);
    Task<KitVideoKeyframeAttemptDto> DecideAsync(KitVideoDirectorDecideRequest request, CancellationToken cancellationToken = default);
    KitVideoI2vReadyPackageDto I2vPackage(KitVideoKeyframeAttemptDto attempt);
}

public sealed record KitVideoProviderStatusDto(
    string ImageProvider,
    string ImageModel,
    bool ApiKeyConfigured,
    string VisionProvider,
    string GeneratorProvider,
    bool RunwayCalled);

public sealed record KitVideoPixelGenerateRequest(
    Guid ProductionId,
    string ShotCode,
    JsonElement Contract,
    bool Confirmed,
    string IdempotencyKey,
    bool StrategyChanged = false,
    string? ProjectStyle = null,
    string? Dialogue = null);

public sealed record KitVideoPixelAnalyzeRequest(
    Guid ProductionId,
    string ShotCode,
    JsonElement Contract,
    string ImageBase64);

public sealed record KitVideoPixelGenerateDto(
    Guid AttemptId,
    Guid ProductionId,
    string ShotCode,
    int AttemptNo,
    string Status,
    string JobState,
    string Fingerprint,
    KitVideoVisionQaDto? Qa,
    KitVideoRepairDto? Repair,
    KitVideoI2vReadyPackageDto? I2v,
    string? ImagePath,
    string Provider,
    string Model,
    bool CostUnknown,
    string FailureClass,
    string? VisionJson,
    bool RunwayCalled,
    string? ArtifactHash = null,
    string? ImageType = null,
    string PersistStatus = "OK");

public sealed record KitVideoPixelRevalidateRequest(Guid AttemptId, JsonElement Contract);

public interface IKitVideoPixelService
{
    Task<KitVideoProviderStatusDto> ProviderAsync(CancellationToken cancellationToken = default);
    Task<KitVideoPixelGenerateDto> GenerateAsync(KitVideoPixelGenerateRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoPixelGenerateDto> AnalyzeBytesAsync(KitVideoPixelAnalyzeRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoPixelGenerateDto> RevalidateAsync(KitVideoPixelRevalidateRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoPixelGenerateDto> DecideAsync(KitVideoDirectorDecideRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoI2vReadyPackageDto> I2vPackageAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadArtifactAsync(Guid attemptId, CancellationToken cancellationToken = default);
}

public sealed record KitVideoTakeDecideRequest(string Decision);

public sealed record KitVideoMotionSubmitRequest(
    Guid KeyframeAttemptId,
    JsonElement MotionContract,
    bool Confirmed,
    string IdempotencyKey,
    string? RetryReason = null);

public sealed record KitVideoMotionTakeDto(
    Guid TakeId,
    Guid ProductionId,
    string ShotCode,
    Guid KeyframeAttemptId,
    int AttemptNo,
    string Status,
    string Model,
    int DurationSec,
    string Prompt,
    string Fingerprint,
    string SourceArtifactHash,
    string? VideoHash,
    string? RunwayTaskId,
    string? OutputUrl,
    string? VideoPath,
    string FailureClass,
    string FailureCode,
    string RetryReason,
    string CreditState,
    KitVideoMotionPreflightDto? Preflight,
    KitVideoVideoQaDto? Qa,
    string Diagnose,
    bool RunwayCalled,
    bool VideoReady,
    bool RunwayAccepted = false,
    string EstimatedCredit = "UNKNOWN",
    string ActualCredit = "UNKNOWN");

public interface IKitVideoMotionService
{
    Task<KitVideoMotionTakeDto> PreflightAsync(KitVideoMotionSubmitRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoMotionTakeDto> SubmitAsync(KitVideoMotionSubmitRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoMotionTakeDto> PollAsync(Guid takeId, CancellationToken cancellationToken = default);
    Task<KitVideoMotionTakeDto> GetAsync(Guid takeId, CancellationToken cancellationToken = default);
    Task<KitVideoMotionTakeDto?> GetLatestByKeyframeAsync(Guid keyframeAttemptId, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadVideoAsync(Guid takeId, CancellationToken cancellationToken = default);
    Task<KitVideoMotionTakeDto> DecideAsync(Guid takeId, string decision, CancellationToken cancellationToken = default);
}

public sealed record KitVideoVisualSystemDto(
    Guid Id,
    Guid ProjectId,
    string ProjectCode,
    string SystemCode,
    string Version,
    string Status,
    JsonElement Rules,
    DateTimeOffset? LockedAt,
    string? LockedBy);

public interface IKitVideoVisualSystemService
{
    Task<KitVideoVisualSystemDto?> GetAsync(string projectCode, string? systemCode = null, string? version = null, CancellationToken cancellationToken = default);
    Task<KitVideoVisualSystemDto> LockAsync(Guid id, string? actor, CancellationToken cancellationToken = default);
}

public sealed record KitVideoMasterCandidateDto(
    Guid Id,
    string CandidateCode,
    string Role,
    int GenerationAttempt,
    string ArtifactPath,
    string Sha256,
    string VisualStyleVersion,
    string CharacterId,
    string EraId,
    string ImageType,
    string Status,
    string QaStatus,
    JsonElement Qa,
    string Fingerprint = "",
    string Diagnosis = "",
    bool CanonEligible = false,
    int Score = 0,
    string Recommendation = "",
    string Lifecycle = "DRAFT",
    bool Eligible = false,
    bool FrontRunner = false,
    Guid? ParentCandidateId = null,
    Guid? SourceCandidateId = null);

public sealed record KitVideoMasterReferenceDto(
    Guid AssetId,
    Guid VersionId,
    string ProjectCode,
    string AssetCode,
    string AssetLifecycle,
    string DocumentId,
    string Status,
    string Era,
    int Age,
    string Version,
    bool GenerateEnabled,
    IReadOnlyList<KitVideoMasterCandidateDto> Candidates,
    JsonElement Spec,
    string? RecommendedCandidateId = null,
    bool AutoSelected = false);

public sealed record KitVideoMasterCandidateRequest(
    string ProjectCode = "FAMIXA",
    string AssetCode = "CHAR-001",
    string Role = "IDENTITY",
    string? ArtifactPath = null,
    string? Sha256 = null,
    string? ImageType = null,
    string? VisualStyleVersion = null);

public sealed record KitVideoMasterQaRequest(
    bool? ArtifactExists = null,
    string? Sha256 = null,
    string? PersistedSha256 = null,
    string? CharacterId = null,
    int? Age = null,
    bool? IdentityMatch = null,
    bool? StyleMatch = null,
    bool? Photoreal = null,
    bool? Anime = null,
    bool? Corrupt = null,
    bool? Watermark = null,
    string? ImageType = null,
    bool? AngleIdentityFail = null,
    bool? ExpressionIdentityFail = null);

public sealed record KitVideoMasterCompileDto(bool Ok, string Prompt, string Provider, string? Blocked);

public sealed record KitVideoMasterGenerateRequest(
    string ProjectCode = "FAMIXA",
    string AssetCode = "CHAR-001",
    string View = "FRONT",
    string Expression = "NEUTRAL",
    bool Confirmed = false,
    bool ControlledTest = false,
    string? Diagnosis = null,
    Guid? ParentCandidateId = null);

public sealed record KitVideoMasterGenerateDto(
    KitVideoMasterReferenceDto Package,
    Guid? CandidateId,
    string CandidateCode,
    bool ArtifactValid,
    string QaStatus,
    string Provider,
    bool Live,
    bool CanonEligible,
    string? Blocked,
    string? Sha256);

public interface IKitVideoMasterReferenceService
{
    Task<KitVideoMasterReferenceDto?> GetAsync(string projectCode, string assetCode, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> RegisterCandidateAsync(KitVideoMasterCandidateRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> ApplyQaAsync(Guid candidateId, KitVideoMasterQaRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> AnalyzeAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> ApproveAsync(string projectCode, string assetCode, Guid? candidateId, string decision, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> LockAsync(string projectCode, string assetCode, CancellationToken cancellationToken = default);
    Task<KitVideoMasterCompileDto> CompileAsync(string role, bool dnaApproved, bool styleReady);
    Task<KitVideoMasterGenerateDto> GenerateCandidateAsync(KitVideoMasterGenerateRequest request, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadCandidateImageAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<KitVideoMasterCompareDto> CompareAsync(string projectCode, string assetCode, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> SelectAsync(string projectCode, string assetCode, Guid candidateId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReferenceDto> MarkFrontRunnerAsync(string projectCode, string assetCode, Guid candidateId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoMasterResolveDto> ResolveAsync(string projectCode, string assetCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitVideoMasterEventDto>> ListEventsAsync(string projectCode, string assetCode, CancellationToken cancellationToken = default);
}

public sealed record KitVideoMasterRankDto(
    Guid Id,
    string CandidateCode,
    bool Eligible,
    string Lifecycle,
    int Score,
    string Recommendation,
    IReadOnlyList<string> P0,
    bool AutoSelected = false);

public sealed record KitVideoMasterCompareDto(
    KitVideoMasterReferenceDto Package,
    IReadOnlyList<KitVideoMasterRankDto> Ranks,
    Guid? RecommendedCandidateId,
    bool AutoSelected,
    string VisualDnaVersion);

public sealed record KitVideoMasterResolveDto(
    bool Ok,
    string? Blocked,
    string CharacterId,
    string EraId,
    string Version,
    string? CandidateCode,
    string? ArtifactPath,
    string? Sha256,
    string Status);

public sealed record KitVideoMasterEventDto(
    Guid Id,
    string EventType,
    string? Actor,
    Guid? CandidateId,
    string ArtifactPath,
    string Sha256,
    string VisualDnaVersion,
    DateTimeOffset CreatedAt);

public sealed record KitVideoIdentityTestArtifactDto(
    Guid Id,
    Guid TestId,
    string TestType,
    string TestVariant,
    int Attempt,
    string ArtifactPath,
    string Sha256,
    string Fingerprint,
    string ImageType,
    string QaStatus,
    JsonElement Qa,
    string Provider,
    string Model);

public sealed record KitVideoIdentityTestDto(
    Guid Id,
    string ProjectCode,
    string CharacterId,
    string EraId,
    Guid CandidateId,
    string CandidateCode,
    string SourceSha256,
    string Status,
    string DocumentId,
    int Have,
    int Required,
    bool Complete,
    bool AutoSelected,
    bool Canon,
    IReadOnlyList<KitVideoIdentityTestArtifactDto> Artifacts,
    string? ViewStability = null,
    string? EmotionStability = null,
    string? IdentityStability = null,
    string? DirectorDecision = null,
    string? Blocked = null);

public sealed record KitVideoIdentityTestCreateRequest(
    Guid CandidateId,
    bool Designated = false,
    string ProjectCode = "FAMIXA",
    string CharacterId = "CHAR-001",
    string EraId = "ERA-01");

public sealed record KitVideoIdentityTestRunRequest(
    string? Variant = null,
    bool Confirmed = false,
    bool Regenerate = false);

public sealed record KitVideoIdentityTestDecisionRequest(
    string Decision,
    string? Reason = null);

public interface IKitVideoIdentityTestService
{
    Task<IReadOnlyList<KitVideoIdentityTestDto>> ListAsync(Guid? candidateId, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> CreateAsync(KitVideoIdentityTestCreateRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KitVideoIdentityTestArtifactDto>> ListArtifactsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> RunAsync(Guid id, KitVideoIdentityTestRunRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> AnalyzeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> CompareAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> DecideAsync(Guid id, KitVideoIdentityTestDecisionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityTestDto> SummaryAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadArtifactImageAsync(Guid artifactId, CancellationToken cancellationToken = default);
}

public sealed record KitVideoIdentityStressArtifactDto(
    Guid Id,
    Guid TestId,
    string TestGroup,
    string TestCase,
    int Attempt,
    string ArtifactPath,
    string Sha256,
    string Fingerprint,
    string ImageType,
    string QaStatus,
    JsonElement Qa,
    string Provider,
    string Model);

public sealed record KitVideoIdentityStressDto(
    Guid Id,
    string ProjectCode,
    string CharacterId,
    string EraId,
    Guid CandidateId,
    string CandidateCode,
    string SourceSha256,
    string Status,
    string DocumentId,
    int Have,
    int Required,
    bool Complete,
    int P0,
    int P1,
    int P2,
    bool AutoSelected,
    bool Canon,
    IReadOnlyList<KitVideoIdentityStressArtifactDto> Artifacts,
    string? Environment = null,
    string? Lighting = null,
    string? Camera = null,
    string? Emotion = null,
    string? Wardrobe = null,
    string? Identity = null,
    string? Age = null,
    string? Hair = null,
    string? Face = null,
    string? DirectorDecision = null,
    string? Blocked = null,
    int IdentityScore = 0,
    int FacialStructureScore = 0,
    int HairScore = 0,
    int EyeScore = 0,
    int AgeConsistencyScore = 0,
    int StyleConsistencyScore = 0,
    int SceneComplianceScore = 0,
    bool AllowMasterReview = false);

public sealed record KitVideoIdentityStressCreateRequest(
    Guid CandidateId,
    string ProjectCode = "FAMIXA",
    string CharacterId = "CHAR-001",
    string EraId = "ERA-01");

public sealed record KitVideoIdentityStressRunRequest(
    string? CaseCode = null,
    bool Confirmed = false,
    bool Regenerate = false,
    string? Diagnosis = null);

public sealed record KitVideoIdentityStressDecisionRequest(
    string Decision,
    string? Reason = null);

public sealed record KitVideoIdentityStressRepairRequest(
    string CaseCode,
    string Diagnosis,
    bool Confirmed = false);

public interface IKitVideoIdentityStressService
{
    Task<IReadOnlyList<KitVideoIdentityStressDto>> ListAsync(Guid? candidateId, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityStressDto> CreateAsync(KitVideoIdentityStressCreateRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityStressDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityStressDto> RunAsync(Guid id, KitVideoIdentityStressRunRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityStressDto> AnalyzeAsync(Guid id, string? caseCode = null, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityStressDto> DecideAsync(Guid id, KitVideoIdentityStressDecisionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoIdentityStressDto> RepairAsync(Guid id, KitVideoIdentityStressRepairRequest request, string actor, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadArtifactImageAsync(Guid artifactId, CancellationToken cancellationToken = default);
}

public sealed record KitVideoMasterReviewShotDto(
    Guid Id,
    string Code,
    string Label,
    string Kind,
    int Attempt,
    string QaStatus,
    int P0,
    int IdentityScore,
    string Sha256);

public sealed record KitVideoMasterReviewDto(
    Guid Id,
    string ProjectCode,
    string CharacterId,
    string EraId,
    Guid CandidateId,
    string CandidateCode,
    string Variation,
    string ParentCode,
    string SourceSha256,
    string Status,
    string DocumentId,
    string MasterRefCode,
    string Note,
    Guid? IdentityTestId,
    Guid? StressTestId,
    int IdentityHave,
    int IdentityRequired,
    int IdentityP0,
    bool IdentityPass,
    int StressHave,
    int StressRequired,
    int StressP0,
    bool StressPass,
    bool St10Pass,
    bool DnaApproved,
    bool CanDirectorPass,
    bool CanSelect,
    bool CanApprove,
    bool CanLock,
    bool AutoSelected,
    bool Canon,
    string? DirectorDecision,
    string? Blocked,
    IReadOnlyList<KitVideoMasterReviewShotDto> IdentityShots,
    IReadOnlyList<KitVideoMasterReviewShotDto> StressShots,
    KitVideoMasterLockDto? Lock = null);

public sealed record KitVideoMasterLockDto(
    Guid MasterReferenceId,
    string MasterCode,
    string CharacterCode,
    string CharacterName,
    string EraCode,
    Guid SourceCandidateId,
    string SourceCandidateCode,
    string SourceVariation,
    string ArtifactPath,
    string Sha256,
    string VisionFingerprint,
    string DnaVersion,
    string IdentityTestResult,
    string StressTestResult,
    string MasterReviewResult,
    string LockedBy,
    DateTimeOffset LockedAt,
    string LockReason,
    string Version,
    string Status,
    Guid? CanonPointerId,
    bool Immutable);

public sealed record KitVideoMasterReviewOpenRequest(
    Guid? CandidateId = null,
    string ProjectCode = "FAMIXA",
    string CharacterId = "CHAR-001",
    string EraId = "ERA-01");

public sealed record KitVideoMasterReviewDecisionRequest(
    string Decision,
    string? Note = null);

public interface IKitVideoMasterReviewService
{
    Task<IReadOnlyList<KitVideoMasterReviewDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> OpenAsync(KitVideoMasterReviewOpenRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> GetByCandidateAsync(Guid candidateId, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> DecideAsync(Guid id, KitVideoMasterReviewDecisionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> SelectMasterAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> ApproveAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoMasterReviewDto> LockAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoMasterLockDto?> ResolveCanonAsync(string characterId, string eraId, CancellationToken cancellationToken = default);
    Task RejectMasterMutationAsync(Guid masterId, string action, CancellationToken cancellationToken = default);
}

public sealed record KitVideoDnaCheckItemDto(string Code, string Label, bool Pass, string? Reason);

public sealed record KitVideoCharacterDnaDto(
    Guid Id,
    string DnaCode,
    string CharacterId,
    string CharacterName,
    string EraId,
    Guid MasterReferenceId,
    string MasterCode,
    string MasterSha256,
    string DnaVersion,
    string Status,
    string DocumentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? LockedAt,
    string? LockedBy,
    string Note,
    JsonElement Spec,
    bool CanApprove,
    bool CanReject,
    bool Immutable,
    string? Blocked,
    KitVideoMasterLockDto? Master,
    string DnaSha256 = "",
    bool IdentityPass = false,
    int IdentityHave = 0,
    bool StressPass = false,
    int StressHave = 0,
    int P0 = 0,
    bool CanEdit = false,
    bool CanReturn = false,
    string? Analysis = null,
    IReadOnlyList<KitVideoDnaCheckItemDto>? SelfCheck = null,
    bool GatePass = false);

public sealed record KitVideoCharacterDnaGetDto(
    KitVideoCharacterDnaDto? Dna,
    KitVideoMasterLockDto? Master,
    bool CanCreate,
    string? Blocked,
    bool IdentityPass = false,
    bool StressPass = false,
    int P0 = 0);

public sealed record KitVideoCharacterDnaNoteRequest(string? Note = null);

public sealed record KitVideoCharacterDnaEditRequest(JsonElement? Spec = null, string? Note = null);

public interface IKitVideoCharacterDnaService
{
    Task<KitVideoCharacterDnaGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> GetVersionAsync(string characterId, string dnaVersion, CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> CreateAsync(string characterId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> ApproveAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> RejectAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> AnalyzeAsync(string characterId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> EditAsync(string characterId, string actor, KitVideoCharacterDnaEditRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoCharacterDnaDto> ReturnToEditAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default);
    Task RejectMutationAsync(string characterId, string action, CancellationToken cancellationToken = default);
}

public sealed record KitVideoProductionReferencePackDto(
    Guid Id,
    string PackCode,
    string CharacterId,
    string CharacterName,
    string EraId,
    Guid MasterReferenceId,
    string MasterCode,
    string MasterSha256,
    Guid CharacterDnaId,
    string DnaCode,
    string DnaSha256,
    string PackVersion,
    string Status,
    string DocumentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? LockedAt,
    string? LockedBy,
    string Note,
    JsonElement Spec,
    bool CanApprove,
    bool CanReject,
    bool Immutable,
    string? Blocked,
    bool CanEdit = false,
    bool CanReturn = false,
    string? Analysis = null,
    IReadOnlyList<KitVideoDnaCheckItemDto>? SelfCheck = null,
    bool GatePass = false,
    bool IdentityPass = false,
    bool StressPass = false,
    int P0 = 0,
    bool ProductionReady = false,
    string? PrpSha256 = null,
    bool DirectorGatePass = false);

public sealed record KitVideoProductionReferencePackGetDto(
    KitVideoProductionReferencePackDto? Pack,
    KitVideoMasterLockDto? Master,
    KitVideoCharacterDnaDto? Dna,
    bool CanCreate,
    string? Blocked,
    bool IdentityPass = false,
    bool StressPass = false,
    int P0 = 0,
    IReadOnlyList<KitVideoDnaCheckItemDto>? SelfCheck = null,
    bool GatePass = false);

public sealed record KitVideoProductionReferencePackNoteRequest(string? Note = null);

public sealed record KitVideoProductionReferencePackEditRequest(JsonElement? Spec = null, string? Note = null);

public interface IKitVideoProductionReferencePackService
{
    Task<KitVideoProductionReferencePackGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> GetVersionAsync(string characterId, string packVersion, CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> CreateAsync(string characterId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> ApproveAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> RejectAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> AnalyzeAsync(string characterId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> EditAsync(string characterId, string actor, KitVideoProductionReferencePackEditRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoProductionReferencePackDto> ReturnToEditAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default);
    Task RejectMutationAsync(string characterId, string action, CancellationToken cancellationToken = default);
}

public sealed record CharacterReferenceItemDto(
    Guid Id,
    string Type,
    bool Required,
    string ArtifactPath,
    string ArtifactSha256,
    string Status,
    JsonElement Metadata);

public sealed record CharacterReferenceIdentityDto(
    string Attribute,
    string Verdict,
    string LockedValue,
    string ReferenceValue,
    string Message);

public sealed record CharacterReferencePackDto(
    Guid Id,
    string PackCode,
    string CharacterId,
    string CharacterName,
    string EraId,
    string PackVersion,
    string Status,
    string StatusLabel,
    Guid MasterId,
    string MasterSha256,
    bool MasterLocked,
    Guid DnaId,
    string DnaSha256,
    bool DnaLocked,
    int RequiredReady,
    int RequiredTotal,
    bool CoverageReady,
    bool IdentityPass,
    bool ArtifactPass,
    bool ReadyForDirector,
    bool ProductionReady,
    bool Immutable,
    string? Blocked,
    string? PackSha256,
    string Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? LockedAt,
    string? LockedBy,
    IReadOnlyList<CharacterReferenceItemDto> Items,
    IReadOnlyList<KitVideoDnaCheckItemDto> Coverage,
    IReadOnlyList<CharacterReferenceIdentityDto> Identity,
    IReadOnlyList<KitVideoDnaCheckItemDto> Authority,
    JsonElement? Spec = null,
    IReadOnlyList<KitVideoDnaCheckItemDto>? Gates = null,
    IReadOnlyList<CharacterReferenceIdentityDto>? Conflicts = null,
    Guid? PrpId = null,
    string PrpSha256 = "",
    bool PrpLocked = false,
    bool CanUse = false,
    IReadOnlyList<string>? MissingTypes = null,
    string? ReferenceAssetMissing = null);

public sealed record CharacterReferencePackGetDto(
    CharacterReferencePackDto? Pack,
    bool CanCreate,
    string? Blocked,
    bool MasterLocked,
    bool DnaLocked,
    string CharacterName,
    IReadOnlyList<KitVideoDnaCheckItemDto> Authority,
    Guid? MasterCandidateId = null,
    string CharacterAge = "",
    string CharacterSummary = "",
    bool PrpLocked = false,
    string PrpSha256 = "",
    bool CanUse = false,
    IReadOnlyList<string>? MissingTypes = null);

public sealed record CharacterReferenceItemRequest(
    string Type,
    string? ArtifactPath = null,
    string? ArtifactSha256 = null,
    JsonElement? Metadata = null);

public sealed record CharacterReferencePackNoteRequest(string? Note = null);

public sealed record CharacterReferenceExistingFullBodyDto(
    string CharacterId,
    string EraId,
    bool Found,
    bool Accepted,
    string? AssetId,
    string? Identity,
    string? ReferenceType,
    string? Path,
    string StaffMessage,
    bool Generate = false,
    bool GeminiCalled = false,
    bool RunwayCalled = false,
    bool VeoCalled = false);

public sealed record CharacterReferenceGenerationRequest(
    bool Confirm = false,
    string? Provider = null,
    string? EraId = null,
    string? ReferenceType = null,
    string? Pose = null,
    string? Framing = null,
    string? Camera = null,
    string? Composition = null,
    string? Lighting = null,
    string? Background = null,
    string? Style = null,
    string? Reason = null,
    Guid? ExecutionId = null,
    Guid? PackId = null,
    bool Regenerate = false);

public sealed record CharacterReferenceGenerationTechnicalDto(
    string CharacterId,
    string EraId,
    string ReferenceType,
    string? IntentSha256,
    string? MasterSha256,
    string? DnaSha256,
    string? PrpSha256,
    string? Provider,
    string? ProviderRequestId,
    Guid? ExecutionId,
    Guid? ArtifactId,
    string? Fingerprint,
    string? CapabilityLevel,
    string? GateStatus,
    string? GateCode);

public sealed record CharacterReferenceGenerationDto(
    string DocumentId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string ReferenceType,
    string SlotState,
    string GateStatus,
    string? GateCode,
    string StaffMessage,
    string NextAction,
    bool AuthorityValid,
    bool ProviderSelected,
    bool CapabilityReady,
    bool GenerationAllowed,
    bool MayCallProvider,
    bool ConfirmRequired,
    bool Generate,
    bool GeminiCalled,
    bool RunwayCalled,
    bool VeoCalled,
    bool ShotGenerated,
    bool FirstRealProduction,
    string? Provider,
    string? CapabilityLevel,
    string? IntentSha256,
    string? MasterSha256,
    string? DnaSha256,
    string? PrpSha256,
    string? Fingerprint,
    Guid? ExecutionId,
    Guid? ArtifactId,
    string? ArtifactPath,
    string? ProviderRequestId,
    string? CandidateStatus,
    string? ReviewStatus,
    int RequiredReady,
    int RequiredTotal,
    string? CrpStatus,
    bool CanUse,
    bool AutoApproved,
    bool AutoLocked,
    CharacterReferenceGenerationTechnicalDto? Technical = null);

public interface ICharacterReferenceGenerationService
{
    Task<CharacterReferenceGenerationDto> GetAsync(
        string characterId, string? referenceType, string? provider, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> PrepareAsync(
        string characterId, CharacterReferenceGenerationRequest request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> ExecuteAsync(
        string characterId, CharacterReferenceGenerationRequest request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> GetExecutionAsync(
        string characterId, Guid executionId, CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> AcceptAsync(
        string characterId, Guid executionId, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> RejectAsync(
        string characterId, Guid executionId, string actor, string? reason,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> RegisterAsync(
        string characterId, Guid executionId, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferenceGenerationDto> ValidatePackAsync(
        string characterId, Guid packId, string actor, Guid? executionId = null, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadCandidateImageAsync(
        string characterId, Guid executionId, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record CharacterReferenceSetRequestDto(
    bool Confirm = false,
    string? Provider = null,
    string? EraId = null);

public sealed record CharacterReferenceSetAssetDto(
    string Type,
    Guid? ItemId,
    string? ArtifactPath,
    string? ArtifactSha256,
    bool Present);

public sealed record CharacterReferenceSetDto(
    string DocumentId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string GateStatus,
    string? GateCode,
    string StaffMessage,
    string NextAction,
    bool AuthorityValid,
    bool ProviderSelected,
    bool CapabilityReady,
    bool GenerationAllowed,
    bool MayCallProvider,
    bool ConfirmRequired,
    bool Generate,
    bool ProviderCalled,
    bool GeminiCalled,
    bool RunwayCalled,
    bool VeoCalled,
    bool ProductionGeneration,
    bool VideoGeneration,
    string? Provider,
    string? CapabilityLevel,
    Guid? ReferenceSetId,
    Guid? PackId,
    string? CrpStatus,
    int Coverage,
    int RequiredTotal,
    IReadOnlyList<string> MissingTypes,
    IReadOnlyList<CharacterReferenceSetAssetDto> Assets,
    string DirectorReview,
    bool Approved,
    bool Locked,
    bool CanUse,
    bool Consistency,
    bool AutoApproved,
    bool AutoLocked,
    string? Fingerprint,
    string? MasterSha256,
    string? DnaSha256,
    string? PrpSha256);

public interface ICharacterReferenceAutoGenerationV2Service
{
    Task<CharacterReferenceSetDto> GetAsync(
        string characterId, string? provider, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceSetDto> PrepareAsync(
        string characterId, CharacterReferenceSetRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceSetDto> ExecuteAsync(
        string characterId, CharacterReferenceSetRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record CharacterAuthorityInitializationRequestDto(
    bool Confirm = false,
    string? Provider = null,
    string? EraId = null);

public sealed record CharacterAuthorityStageDto(
    string Stage,
    string Status,
    string StaffLabel,
    bool CanPrepare,
    bool CanExecute,
    bool CanApprove,
    bool CanReject,
    bool CanLock,
    string? Sha256,
    Guid? ArtifactId);

public sealed record CharacterAuthorityInitializationDto(
    string DocumentId,
    string CharacterId,
    string CharacterName,
    string Role,
    string EraId,
    string? IdentityVersion,
    bool IdentityReady,
    bool AuthorityLocked,
    string GateStatus,
    string? GateCode,
    string StaffMessage,
    string NextAction,
    bool AuthorityReady,
    bool ProviderSelected,
    bool CapabilityReady,
    bool GenerationAllowed,
    bool MayCallProvider,
    bool ConfirmRequired,
    bool Generate,
    bool ProviderCalled,
    bool GeminiCalled,
    bool RunwayCalled,
    bool VeoCalled,
    bool ProductionGeneration,
    bool VideoGeneration,
    bool CrpGeneration,
    bool AutoApproved,
    bool AutoLocked,
    string? Provider,
    string? CapabilityLevel,
    CharacterAuthorityStageDto Master,
    CharacterAuthorityStageDto Dna,
    CharacterAuthorityStageDto Prp,
    string? Fingerprint,
    string? MasterImageUrl,
    object? Technical);

public sealed record CharacterAuthorityObjectDto(
    string CharacterId,
    string EraId,
    string? ProfileVersion,
    string? MasterSha,
    string? DnaSha,
    string? PrpSha,
    string? CrpSha,
    string Status,
    bool CanUse);

public sealed record CharacterAuthorityPipelineDto(
    string DocumentId,
    string CharacterId,
    string CharacterName,
    string Role,
    string EraId,
    string PipelineState,
    string? GateCode,
    string StaffMessage,
    string NextAction,
    bool WorkspaceCreated,
    bool ProfileReady,
    bool AuthorityReady,
    bool ProductionReady,
    bool AuthorityLocked,
    bool PhotorealisticBlocked,
    bool AutoApproved,
    bool AutoLocked,
    bool Generate,
    bool ProviderCalled,
    bool GeminiCalled,
    bool RunwayCalled,
    bool VeoCalled,
    bool ProductionGeneration,
    bool VideoGeneration,
    CharacterAuthorityStageDto Master,
    CharacterAuthorityStageDto Dna,
    CharacterAuthorityStageDto Prp,
    string? CrpStatus,
    int Coverage,
    bool CanUse,
    CharacterAuthorityObjectDto Authority,
    object? Technical);

public interface ICharacterAuthorityPipelineV1Service
{
    Task<CharacterAuthorityPipelineDto> GetAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityPipelineDto> ApproveCrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityPipelineDto> LockCrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record CharacterReferenceRejectRequestDto(
    string? RejectReasonCode = null,
    string? RejectReasonText = null,
    string? EraId = null);

public sealed record CharacterReferenceRegenerateRequestDto(
    bool Confirm = false,
    string? Provider = null,
    string? EraId = null);

public sealed record CharacterReferenceHistoryItemDto(
    string Version,
    string Status,
    string StatusLabel,
    int Coverage,
    bool CanUse,
    string? Sha256,
    string? RejectReasonCode,
    string? RejectReasonText,
    string? RejectedBy,
    string? RejectedAt,
    string? Provider,
    string? SetId);

public sealed record CharacterReferenceRegenerationDto(
    string DocumentId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string Version,
    string Status,
    string StatusLabel,
    string StaffMessage,
    string NextAction,
    string? GateCode,
    bool MayReject,
    bool MayRegenerate,
    bool MayCallProvider,
    bool ConfirmRequired,
    bool Generate,
    bool ProviderCalled,
    bool GeminiCalled,
    bool RunwayCalled,
    bool VeoCalled,
    bool ProductionGeneration,
    bool VideoGeneration,
    bool AutoApproved,
    bool AutoLocked,
    bool CanUse,
    bool Approved,
    bool Locked,
    int Coverage,
    int RequiredTotal,
    IReadOnlyList<string> PresentTypes,
    IReadOnlyList<string> MissingTypes,
    string? RejectReasonCode,
    string? RejectReasonText,
    string? RejectedBy,
    string? RejectedAt,
    string? Provider,
    Guid? ReferenceSetId,
    string? Fingerprint,
    string? MasterSha256,
    string? DnaSha256,
    string? PrpSha256,
    string? CrpSha256,
    IReadOnlyList<CharacterReferenceHistoryItemDto> History);

public interface ICharacterReferenceRegenerationV1Service
{
    Task<CharacterReferenceRegenerationDto> GetAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceRegenerationDto> RejectAsync(
        string characterId, CharacterReferenceRejectRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceRegenerationDto> PrepareAsync(
        string characterId, CharacterReferenceRegenerateRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceRegenerationDto> ExecuteAsync(
        string characterId, CharacterReferenceRegenerateRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterReferenceRegenerationDto> HistoryAsync(
        string characterId, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public interface ICharacterAuthorityInitializationV1Service
{
    Task<CharacterAuthorityInitializationDto> GetAsync(
        string characterId, string? provider, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> PrepareMasterAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> ExecuteMasterAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> ApproveMasterAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> RejectMasterAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> LockMasterAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> PrepareDnaAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> ExecuteDnaAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> ApproveDnaAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> RejectDnaAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> LockDnaAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> PreparePrpAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> ExecutePrpAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> ApprovePrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> RejectPrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterAuthorityInitializationDto> LockPrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadMasterImageAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record CharacterLibraryViewDto(
    string CharacterId,
    string Name,
    string DisplayName,
    string Role,
    string Readiness,
    string ReadinessCode,
    string ReadinessLabel,
    string ReadinessReason,
    string NextAction,
    bool CanUse,
    bool MasterLocked,
    bool DnaLocked,
    bool ProductionReferenceLocked,
    string? ReferencePackStatus,
    string? ReferencePackVersion,
    int RequiredReady,
    int RequiredTotal,
    int SceneCount,
    Guid? FrontPackId,
    Guid? FrontItemId,
    IReadOnlyList<string>? MissingTypes = null);

public sealed record CharacterLibraryViewItemDto(string Type, bool Present, Guid? ItemId);

public sealed record CharacterLibrarySceneDto(Guid ShotId, string ShotCode, string Title, int ShotSeq);

public sealed record CharacterLibraryListDto(
    IReadOnlyList<CharacterLibraryViewDto> Items,
    int Total,
    bool Generate = false);

public sealed record CharacterLibraryDetailDto(
    CharacterLibraryViewDto Character,
    IReadOnlyList<CharacterLibraryViewItemDto> Views,
    IReadOnlyList<CharacterLibrarySceneDto> Scenes,
    object? Technical = null,
    bool Generate = false);

public interface ICharacterProductionLibraryService
{
    Task<CharacterLibraryListDto> ListAsync(string? query, string? filter, string? sort, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterLibraryDetailDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record ProductionProgressStageDto(string Id, string Label, bool Done, string Detail);

public sealed record ProductionProgressShotDto(
    string ShotId,
    string SceneId,
    int ShotNumber,
    IReadOnlyList<string> CharacterIds,
    IReadOnlyList<string> CharacterNames,
    string CurrentStep,
    string NextAction,
    string StaffStatus,
    string? BlockingReason,
    bool ImageMade,
    bool ImageApproved,
    bool VideoMade,
    bool VideoApproved);

public sealed record ProductionProgressSceneDto(
    string SceneId,
    string SceneName,
    int ShotCount,
    int CharacterCount,
    IReadOnlyList<string> CharacterNames,
    int ImageProgress,
    int ImageApprovalProgress,
    int VideoProgress,
    int VideoApprovalProgress,
    string NextAction,
    string? BlockingReason,
    IReadOnlyList<ProductionProgressShotDto> Shots);

public sealed record ProductionProgressDto(
    Guid BuildId,
    string SeriesCode,
    string EpisodeCode,
    string Title,
    int SceneCount,
    int ShotCount,
    int CharacterCount,
    int ImageProgress,
    int ImageApprovalProgress,
    int VideoProgress,
    int VideoApprovalProgress,
    string Finalization,
    string Publication,
    string CurrentStep,
    string NextAction,
    string? BlockingReason,
    string StoryLine,
    int CompletedStages,
    int TotalStages,
    string Tone,
    IReadOnlyList<ProductionProgressStageDto> Stages,
    IReadOnlyList<ProductionProgressSceneDto> Scenes,
    bool Generate = false);

public sealed record ProductionProgressListDto(
    IReadOnlyList<ProductionProgressDto> Items,
    int Total,
    bool Generate = false);

public interface IProductionProgressService
{
    Task<ProductionProgressDto> GetBuildAsync(Guid buildId, CancellationToken cancellationToken = default);
    Task<ProductionProgressListDto> ListAsync(string seriesCode, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public interface ICharacterReferencePackService
{
    Task<CharacterReferencePackGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> CreateAsync(string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> UpsertItemAsync(string characterId, Guid packId, CharacterReferenceItemRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> AttachItemAsync(string characterId, Guid packId, string type, byte[] bytes, string? fileName, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> RegisterExistingAsync(string characterId, Guid packId, string type, string sourcePath, string actor, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadItemImageAsync(string characterId, Guid packId, Guid itemId, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> ValidateAsync(string characterId, Guid packId, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> ApproveAsync(string characterId, Guid packId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> RejectAsync(string characterId, Guid packId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> LockAsync(string characterId, Guid packId, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferencePackDto> SupersedeAsync(string characterId, Guid packId, string actor, CancellationToken cancellationToken = default);
    Task<CharacterReferenceExistingFullBodyDto> FindExistingFullBodyAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<string> RunCompletionRegression();
    IReadOnlyList<string> RunApprovalLockRegression();
}

public sealed record KitVideoProductionShotDto(
    Guid Id,
    string ShotCode,
    string CharacterId,
    string CharacterName,
    string EraId,
    int ShotSeq,
    string ShotVersion,
    string ShotStatus,
    string DocumentId,
    Guid MasterReferenceId,
    string MasterCode,
    string MasterSha256,
    Guid CharacterDnaId,
    string DnaCode,
    string DnaSha256,
    Guid ProductionPackId,
    string PrpCode,
    string PrpSha256,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    DateTimeOffset? LockedAt,
    string? LockedBy,
    string Note,
    JsonElement Spec,
    bool CanApprove,
    bool CanReject,
    bool Immutable,
    string? Blocked,
    bool CanEdit = false,
    bool CanReturn = false,
    string? Analysis = null,
    IReadOnlyList<KitVideoDnaCheckItemDto>? SelfCheck = null,
    bool GatePass = false,
    bool DirectorGatePass = false,
    bool IdentityCheckPass = false,
    bool IdentityPass = false,
    bool StressPass = false,
    bool ShotReady = false,
    string? ShotSha256 = null);

public sealed record KitVideoProductionShotGetDto(
    IReadOnlyList<KitVideoProductionShotDto> Shots,
    KitVideoMasterLockDto? Master,
    KitVideoCharacterDnaDto? Dna,
    KitVideoProductionReferencePackDto? Pack,
    bool CanCreate,
    string? Blocked,
    bool IdentityPass = false,
    bool StressPass = false,
    IReadOnlyList<KitVideoDnaCheckItemDto>? SelfCheck = null,
    bool GatePass = false);

public sealed record KitVideoProductionShotNoteRequest(string? Note = null);

public sealed record KitVideoProductionShotEditRequest(JsonElement? Spec = null, string? Note = null);

public interface IKitVideoProductionShotService
{
    Task<KitVideoProductionShotGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> GetOneAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> CreateAsync(string characterId, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> AnalyzeAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> IdentityCheckAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> EditAsync(Guid id, string actor, KitVideoProductionShotEditRequest request, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> ApproveAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> RejectAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default);
    Task<KitVideoProductionShotDto> ReturnToEditAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default);
    Task RejectMutationAsync(Guid id, string action, CancellationToken cancellationToken = default);
}

public sealed record CharacterIdentityConflictDto(
    string Status,
    string Code,
    string Source,
    string Attribute,
    string? RequestedValue,
    string? AuthoritativeValue,
    string Message);

public sealed record CharacterIdentityPromptContractDto(
    bool Allowed,
    string CharacterId,
    Guid? MasterId,
    string? MasterSha256,
    Guid? DnaId,
    string? DnaSha256,
    Guid? PrpId,
    string? PrpSha256,
    Guid? ShotId,
    string Mode,
    string Rule);

public sealed record CharacterIdentityGovernanceDto(
    string Status,
    string CharacterId,
    string Master,
    string Dna,
    string Prp,
    string Identity,
    string Stress,
    string Continuity,
    string Regression,
    int P0,
    bool ProductionAllowed,
    bool Generate,
    string? Code,
    string? Source,
    string? Attribute,
    string? RequestedValue,
    string? AuthoritativeValue,
    string? Message,
    Guid? MasterId,
    string? MasterSha256,
    Guid? DnaId,
    string? DnaSha256,
    Guid? PrpId,
    string? PrpSha256,
    Guid? ShotId,
    IReadOnlyList<KitVideoDnaCheckItemDto> Gates,
    IReadOnlyList<CharacterIdentityConflictDto> Conflicts,
    CharacterIdentityPromptContractDto PromptContract,
    bool AutoFix = false,
    bool AutoApprove = false,
    bool AutoLock = false,
    string DirectorApproval = "PENDING",
    string GovernanceEngine = "BLOCKED",
    string StressState = "IDENTITY_STRESS_NOT_VERIFIED");

public sealed class CharacterIdentityGovernanceBlockedException : InvalidOperationException
{
    public CharacterIdentityGovernanceDto Result { get; }
    public CharacterIdentityGovernanceBlockedException(CharacterIdentityGovernanceDto result)
        : base("SHOT_GATE_NOT_SATISFIED: " + (result.Conflicts.FirstOrDefault()?.Code ?? result.Code ?? "BLOCKED"))
    {
        Result = result;
    }
}

public sealed record CharacterIdentityGovernanceAuditDto(
    Guid Id,
    string CharacterId,
    string EraId,
    Guid? MasterId,
    Guid? DnaId,
    Guid? PrpId,
    Guid? ShotId,
    string Gate,
    string Result,
    string? Code,
    string? Source,
    string? Attribute,
    string? RequestedValue,
    string? AuthoritativeValue,
    string Reason,
    string? Actor,
    DateTimeOffset CreatedAt);

public sealed record CharacterIdentityGovernanceCheckRequest(
    JsonElement? ShotSpec = null,
    string? UserPrompt = null,
    Guid? ShotId = null);

public interface ICharacterIdentityGovernanceService
{
    Task<CharacterIdentityGovernanceDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterIdentityGovernanceDto> CheckAsync(string characterId, CharacterIdentityGovernanceCheckRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CharacterIdentityGovernanceAuditDto>> ListAuditAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default);
    Task<CharacterIdentityGovernanceDto> ProductionGateAsync(string characterId, CharacterIdentityGovernanceCheckRequest? request, string actor, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record ProductionShotContractDto(
    Guid? Id,
    Guid ShotId,
    string SeriesId,
    string CharacterId,
    string EraId,
    string ContractVersion,
    string Status,
    string DocumentId,
    JsonElement Payload,
    string CanonicalJson,
    string ContractSha256,
    Guid? MasterId,
    string MasterSha256,
    Guid? DnaId,
    string DnaSha256,
    Guid? PrpId,
    string PrpSha256,
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    bool Immutable,
    bool Generate,
    string GovernanceEngine,
    string ContractValidation,
    string DirectorApproval,
    string Master,
    string Dna,
    string Prp,
    IReadOnlyList<KitVideoDnaCheckItemDto> Issues,
    CharacterIdentityGovernanceDto? Governance);

public sealed record ProductionShotContractWriteRequest(JsonElement? Payload = null, string? Note = null);

public sealed class ProductionShotContractException : InvalidOperationException
{
    public string Code { get; }
    public CharacterIdentityGovernanceDto? Governance { get; }
    public IReadOnlyList<ProductionShotContractRules.Issue> Issues { get; }

    public ProductionShotContractException(
        string code,
        string message,
        CharacterIdentityGovernanceDto? governance = null,
        IReadOnlyList<ProductionShotContractRules.Issue>? issues = null)
        : base(message)
    {
        Code = code;
        Governance = governance;
        Issues = issues ?? [];
    }
}

public interface IProductionShotContractService
{
    Task<ProductionShotContractDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<ProductionShotContractDto> SaveAsync(Guid shotId, JsonElement payload, string actor, CancellationToken cancellationToken = default);
    Task<ProductionShotContractDto> ValidateAsync(Guid shotId, JsonElement? payload, string actor, CancellationToken cancellationToken = default);
    Task<ProductionShotContractDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<ProductionShotContractDto> RejectAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record ProductionPromptCompilerDto(
    Guid? Id,
    Guid ShotId,
    Guid? ContractId,
    string SeriesId,
    string CharacterId,
    string EraId,
    string ContractVersion,
    string PromptVersion,
    string Status,
    string DocumentId,
    string? Prompt,
    IReadOnlyList<string> NegativeConstraints,
    string PromptSha256,
    string ContractSha256,
    Guid? MasterId,
    string MasterSha256,
    Guid? DnaId,
    string DnaSha256,
    Guid? PrpId,
    string PrpSha256,
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    bool Immutable,
    bool Generation,
    string Master,
    string Dna,
    string Prp,
    string GovernanceEngine,
    string ShotContract,
    string DirectorApproval,
    ProductionPromptProvenanceDto Provenance,
    IReadOnlyList<ProductionPromptCompilerBlockDto> Blocks,
    CharacterIdentityGovernanceDto? Governance);

public sealed record ProductionPromptProvenanceDto(
    string? MasterSha256,
    string? DnaSha256,
    string? PrpSha256,
    string? ContractSha256,
    string? PromptSha256,
    string? PromptVersion,
    string? ContractVersion,
    Guid? ContractId,
    Guid? ShotId,
    string CharacterId);

public sealed record ProductionPromptCompilerBlockDto(
    string Status,
    string Code,
    string Source,
    string Attribute,
    string? Requested,
    string? Authoritative,
    string Message);

public sealed class ProductionPromptCompilerException : InvalidOperationException
{
    public string Code { get; }
    public string BlockSource { get; }
    public string Attribute { get; }
    public string? Requested { get; }
    public string? Authoritative { get; }
    public CharacterIdentityGovernanceDto? Governance { get; }
    public IReadOnlyList<ProductionPromptCompilerBlockDto> Blocks { get; }

    public ProductionPromptCompilerException(
        string code,
        string message,
        string source = "COMPILER",
        string attribute = "prompt",
        string? requested = null,
        string? authoritative = null,
        CharacterIdentityGovernanceDto? governance = null,
        IReadOnlyList<ProductionPromptCompilerBlockDto>? blocks = null)
        : base(message)
    {
        Code = code;
        BlockSource = source;
        Attribute = attribute;
        Requested = requested;
        Authoritative = authoritative;
        Governance = governance;
        Blocks = blocks ?? [];
    }
}

public interface IProductionPromptCompiler
{
    Task<ProductionPromptCompilerDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<ProductionPromptCompilerDto> CompileAsync(Guid shotId, string actor, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record ImageGenerationContractDto(
    Guid? Id,
    Guid ShotId,
    Guid? ShotContractId,
    Guid? PromptId,
    string SeriesId,
    string CharacterId,
    string EraId,
    string ContractVersion,
    string Status,
    string DocumentId,
    JsonElement Payload,
    string CanonicalJson,
    string ContractSha256,
    Guid? MasterId,
    string MasterSha256,
    Guid? DnaId,
    string DnaSha256,
    Guid? PrpId,
    string PrpSha256,
    string ShotContractSha256,
    string PromptSha256,
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    bool Immutable,
    bool Generation,
    string Master,
    string Dna,
    string Prp,
    string GovernanceEngine,
    string ShotContract,
    string Prompt,
    string DirectorApproval,
    IReadOnlyList<ImageGenerationContractBlockDto> Blocks,
    CharacterIdentityGovernanceDto? Governance);

public sealed record ImageGenerationContractBlockDto(
    string Status,
    string Code,
    string Source,
    string Attribute,
    string? Requested,
    string? Authoritative,
    string Message);

public sealed record ImageGenerationContractWriteRequest(
    string? QualityPolicy = null,
    string? BackgroundPolicy = null,
    string? OutputFormat = null,
    string? Note = null);

public sealed class ImageGenerationContractException : InvalidOperationException
{
    public string Code { get; }
    public string BlockSource { get; }
    public string Attribute { get; }
    public string? Requested { get; }
    public string? Authoritative { get; }
    public CharacterIdentityGovernanceDto? Governance { get; }
    public IReadOnlyList<ImageGenerationContractBlockDto> Blocks { get; }

    public ImageGenerationContractException(
        string code,
        string message,
        string source = "CONTRACT",
        string attribute = "contract",
        string? requested = null,
        string? authoritative = null,
        CharacterIdentityGovernanceDto? governance = null,
        IReadOnlyList<ImageGenerationContractBlockDto>? blocks = null)
        : base(message)
    {
        Code = code;
        BlockSource = source;
        Attribute = attribute;
        Requested = requested;
        Authoritative = authoritative;
        Governance = governance;
        Blocks = blocks ?? [];
    }
}

public interface IImageGenerationContractService
{
    Task<ImageGenerationContractDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<ImageGenerationContractDto> SaveAsync(Guid shotId, ImageGenerationContractWriteRequest? request, string actor, CancellationToken cancellationToken = default);
    Task<ImageGenerationContractDto> ValidateAsync(Guid shotId, ImageGenerationContractWriteRequest? request, string actor, CancellationToken cancellationToken = default);
    Task<ImageGenerationContractDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<ImageGenerationContractDto> RejectAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record ImageGenerationExecutionQaDto(
    string Technical,
    string Character,
    string Identity,
    string Continuity,
    string Composition,
    int P0,
    string Overall,
    IReadOnlyList<string> Reasons);

public sealed record ImageGenerationExecutionBlockDto(
    string Status,
    string Code,
    string Source,
    string Attribute,
    string? Requested,
    string? Authoritative,
    string Message);

public sealed record ImageGenerationExecutionDto(
    Guid? Id,
    Guid ShotId,
    Guid? IgcId,
    Guid? ShotContractId,
    Guid? PromptId,
    string SeriesId,
    string CharacterId,
    string EraId,
    string DocumentId,
    string Status,
    string Provider,
    string ProviderStatus,
    string? ProviderRequestId,
    string ExecutionFingerprint,
    string IdempotencyKey,
    string MasterSha256,
    string DnaSha256,
    string PrpSha256,
    string ShotContractSha256,
    string PromptSha256,
    string IgcSha256,
    string? ArtifactPath,
    string ArtifactSha256,
    string ArtifactMime,
    ImageGenerationExecutionQaDto? Qa,
    string CreditStatus,
    decimal? CreditValue,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    bool PreflightPass,
    bool RunGemini,
    bool Generation,
    bool Immutable,
    string Master,
    string Dna,
    string Prp,
    string GovernanceEngine,
    string ShotContract,
    string Prompt,
    string ImageGenerationContract,
    string DirectorApproval,
    IReadOnlyList<ImageGenerationExecutionBlockDto> Blocks,
    bool CrpUsable = false,
    string CrpStatus = "",
    string? StaffBlock = null,
    bool GenerationAllowed = false);

public sealed record FirstRealProductionExecuteRequest(bool Confirm = false, string? Provider = null);

public sealed record FirstRealProductionDto(
    string DocumentId,
    Guid ShotId,
    string ShotCode,
    string CharacterId,
    string CharacterName,
    string Location,
    string Action,
    string DurationLabel,
    string CrpStatus,
    bool CrpUsable,
    string Provider,
    string CapabilityStatus,
    string IntentStatus,
    string? IntentSha256,
    string CanonicalStatus,
    bool AuthorityValid,
    bool GenerationAllowed,
    bool MayCallProvider,
    string StaffMessage,
    string NextAction,
    bool Generate,
    bool GeminiCalled,
    bool RunwayCalled,
    bool VeoCalled,
    string MasterSha256,
    string DnaSha256,
    string ReferenceSha256,
    string ShotContractSha256,
    string? ExecutionFingerprint);

public interface IFirstRealProductionService
{
    Task<FirstRealProductionDto> GetAsync(Guid shotId, string? provider, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<string> RunV2Regression();
}

public sealed record ImageGenerationReferenceBytes(string Role, string Mime, byte[] Bytes, string Sha256, string Label);

public sealed record ImageGenerationExecutionRequest(
    string Prompt,
    IReadOnlyList<ImageGenerationReferenceBytes> References,
    string AspectRatio,
    string? Resolution,
    string? VisualUniverseSha = null,
    string? PvsSha = null,
    string? CdlSha = null,
    string? CompiledPromptSha = null);

public sealed record ImageGenerationProviderResult(
    bool Accepted,
    bool Succeeded,
    string ProviderStatus,
    byte[]? Bytes,
    string? Mime,
    string Provider,
    string? Model,
    string? ProviderRequestId,
    bool CostUnknown,
    decimal? Credit);

public interface IGeminiImageGenerationProvider : IImageGenerationProvider;

public class ImageGenerationExecutionException : InvalidOperationException
{
    public string Code { get; }
    public string BlockSource { get; }
    public string Attribute { get; }
    public string? Requested { get; }
    public string? Authoritative { get; }
    public IReadOnlyList<ImageGenerationExecutionBlockDto> Blocks { get; }

    public ImageGenerationExecutionException(
        string code,
        string message,
        string source = "EXECUTION",
        string attribute = "execution",
        string? requested = null,
        string? authoritative = null,
        IReadOnlyList<ImageGenerationExecutionBlockDto>? blocks = null)
        : base(message)
    {
        Code = code;
        BlockSource = source;
        Attribute = attribute;
        Requested = requested;
        Authoritative = authoritative;
        Blocks = blocks ?? [];
    }
}

public sealed record ImageGenerationDirectorReviewDto(
    Guid? Id,
    Guid ShotId,
    Guid? ExecutionId,
    string SeriesId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string ShotCode,
    string ReviewVersion,
    string Status,
    string DocumentId,
    string DirectorApproval,
    string? DirectorId,
    DateTimeOffset? DirectorAt,
    string? RejectionReason,
    string Master,
    string Dna,
    string Prp,
    string GovernanceEngine,
    string ShotContract,
    string Prompt,
    string ImageGenerationContract,
    string ExecutionStatus,
    string ProviderStatus,
    ImageGenerationExecutionQaDto? Qa,
    string MasterSha256,
    string DnaSha256,
    string PrpSha256,
    string ShotContractSha256,
    string PromptSha256,
    string IgcSha256,
    string ArtifactSha256,
    string? ArtifactPath,
    string ArtifactUrl,
    bool ArtifactReadable,
    bool CanApprove,
    bool CanReject,
    bool Generation,
    bool Immutable,
    IReadOnlyList<ImageGenerationExecutionBlockDto> Blocks);

public sealed record ImageGenerationDirectorReviewRejectRequest(string? Reason = null, string? Note = null);

public class ImageGenerationDirectorReviewException : InvalidOperationException
{
    public string Code { get; }
    public string BlockSource { get; }
    public string Attribute { get; }
    public string? Requested { get; }
    public string? Authoritative { get; }
    public IReadOnlyList<ImageGenerationExecutionBlockDto> Blocks { get; }

    public ImageGenerationDirectorReviewException(
        string code,
        string message,
        string source = "DIRECTOR",
        string attribute = "review",
        string? requested = null,
        string? authoritative = null,
        IReadOnlyList<ImageGenerationExecutionBlockDto>? blocks = null)
        : base(message)
    {
        Code = code;
        BlockSource = source;
        Attribute = attribute;
        Requested = requested;
        Authoritative = authoritative;
        Blocks = blocks ?? [];
    }
}

public interface IImageGenerationDirectorReviewService
{
    Task<ImageGenerationDirectorReviewDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<ImageGenerationDirectorReviewDto> OpenAsync(Guid shotId, string actor, CancellationToken cancellationToken = default);
    Task<ImageGenerationDirectorReviewDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<ImageGenerationDirectorReviewDto> RejectAsync(Guid shotId, string actor, string? reason, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record ProductionVideoContractDto(
    Guid? Id,
    Guid ShotId,
    Guid? ShotContractId,
    Guid? StillExecutionId,
    string SeriesId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string ShotCode,
    string ContractVersion,
    string Status,
    string DocumentId,
    JsonElement Payload,
    string CanonicalJson,
    string ContractSha256,
    Guid? MasterId,
    string MasterSha256,
    Guid? DnaId,
    string DnaSha256,
    Guid? PrpId,
    string PrpSha256,
    string ShotContractSha256,
    string StillArtifactSha256,
    string StillStatus,
    string DirectorReviewStatus,
    bool StillApproved,
    string ArtifactUrl,
    bool ArtifactReadable,
    string? RejectionReason,
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    DateTimeOffset? ValidatedAt,
    string? ValidatedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    bool Immutable,
    bool Generation,
    bool CanCreate,
    bool CanValidate,
    bool CanApprove,
    bool CanReject,
    string Master,
    string Dna,
    string Prp,
    string GovernanceEngine,
    string ShotContract,
    string DirectorApproval,
    IReadOnlyList<ImageGenerationContractBlockDto> Blocks);

public sealed record ProductionVideoContractWriteRequest(
    Guid? ContractId = null,
    double? DurationSeconds = null,
    string? CameraMovementType = null,
    string? CameraDirection = null,
    string? CameraIntensity = null,
    string? HeadMovementType = null,
    string? HeadDirection = null,
    string? HeadIntensity = null,
    string? StartingExpression = null,
    string? EndingExpression = null,
    string? HairMotion = null,
    string? ClothMotion = null,
    string? Note = null,
    string? Reason = null);

public class ProductionVideoContractException : InvalidOperationException
{
    public string Code { get; }
    public string BlockSource { get; }
    public string Attribute { get; }
    public string? Requested { get; }
    public string? Authoritative { get; }
    public IReadOnlyList<ImageGenerationContractBlockDto> Blocks { get; }

    public ProductionVideoContractException(
        string code,
        string message,
        string source = "VIDEO_CONTRACT",
        string attribute = "contract",
        string? requested = null,
        string? authoritative = null,
        IReadOnlyList<ImageGenerationContractBlockDto>? blocks = null)
        : base(message)
    {
        Code = code;
        BlockSource = source;
        Attribute = attribute;
        Requested = requested;
        Authoritative = authoritative;
        Blocks = blocks ?? [];
    }
}

public interface IProductionVideoContractService
{
    Task<ProductionVideoContractDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<ProductionVideoContractDto> SaveAsync(Guid shotId, ProductionVideoContractWriteRequest? request, string actor, CancellationToken cancellationToken = default);
    Task<ProductionVideoContractDto> UpdateAsync(Guid shotId, Guid contractId, ProductionVideoContractWriteRequest? request, string actor, CancellationToken cancellationToken = default);
    Task<ProductionVideoContractDto> ValidateAsync(Guid shotId, Guid? contractId, ProductionVideoContractWriteRequest? request, string actor, CancellationToken cancellationToken = default);
    Task<ProductionVideoContractDto> ApproveAsync(Guid shotId, Guid contractId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<ProductionVideoContractDto> RejectAsync(Guid shotId, Guid contractId, string actor, string? reason, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public interface IImageGenerationExecutionService
{
    Task<ImageGenerationExecutionDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<ImageGenerationExecutionDto> GetByIdAsync(Guid shotId, Guid executionId, CancellationToken cancellationToken = default);
    Task<ImageGenerationExecutionDto> PreflightAsync(Guid shotId, string actor, CancellationToken cancellationToken = default);
    Task<ImageGenerationExecutionDto> ExecuteAsync(Guid shotId, string actor, bool confirm, string? provider, CancellationToken cancellationToken = default);
    Task<ImageGenerationExecutionDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<ImageGenerationExecutionDto> RejectAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadArtifactAsync(Guid shotId, Guid executionId, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record VideoGenerationExecutionBlockDto(
    string Status, string Code, string Source, string Attribute,
    string? Requested, string? Authoritative, string Message);

public sealed record VideoGenerationExecutionQaDto(
    string Technical,
    string Artifact,
    string Identity,
    string Continuity,
    string Duration,
    string Resolution,
    string Fps,
    string Aspect,
    int P0,
    string Overall,
    IReadOnlyList<string> Reasons);

public sealed record VideoGenerationExecutionDto(
    Guid? Id,
    Guid ShotId,
    Guid? VideoContractId,
    Guid? StillExecutionId,
    string SeriesId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string ShotCode,
    string DocumentId,
    string Status,
    string Provider,
    string ProviderStatus,
    string? ProviderRequestId,
    string ProviderConfigVersion,
    string ExecutionFingerprint,
    string MasterSha256,
    string DnaSha256,
    string PrpSha256,
    string ShotContractSha256,
    string PromptSha256,
    string IgcSha256,
    string VideoContractSha256,
    string StillArtifactSha256,
    double DurationSeconds,
    string Resolution,
    string Fps,
    string AspectRatio,
    string? ArtifactPath,
    string ArtifactSha256,
    string ArtifactMime,
    string ArtifactUrl,
    bool ArtifactReadable,
    VideoGenerationExecutionQaDto? Qa,
    string CreditStatus,
    decimal? CreditValue,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    bool PreflightPass,
    bool RunProvider,
    bool Generation,
    bool Immutable,
    bool CanExecute,
    bool CanApprove,
    bool CanReject,
    string Master,
    string Dna,
    string Prp,
    string GovernanceEngine,
    string ShotContract,
    string Prompt,
    string ImageGenerationContract,
    string ImageDirectorApproval,
    string VideoContract,
    string DirectorApproval,
    IReadOnlyList<VideoGenerationExecutionBlockDto> Blocks);

public sealed record VideoGenerationExecutionRequest(
    byte[] SourceImage,
    string SourceMime,
    string SourceImageSha256,
    double DurationSeconds,
    string Resolution,
    string Fps,
    string AspectRatio,
    string MotionIntent,
    string ProviderConfigVersion,
    string? CompiledPrompt = null,
    string? VisualUniverseSha = null,
    string? PvsSha = null,
    string? CdlSha = null,
    string? CompiledPromptSha = null);

public sealed record VideoGenerationProviderResult(
    bool Accepted,
    bool Succeeded,
    string ProviderStatus,
    byte[]? Bytes,
    string? Mime,
    string Provider,
    string? ProviderRequestId,
    bool CostUnknown,
    decimal? Credit);

public interface IVideoGenerationProvider
{
    string ProviderId { get; }
    string ConfigVersion { get; }
    Task<VideoGenerationProviderResult> GenerateAsync(VideoGenerationExecutionRequest request, CancellationToken cancellationToken);
    Task<VideoGenerationProviderResult> ResumeAsync(string providerRequestId, CancellationToken cancellationToken);
}

public class VideoGenerationExecutionException : InvalidOperationException
{
    public string Code { get; }
    public string BlockSource { get; }
    public string Attribute { get; }
    public string? Requested { get; }
    public string? Authoritative { get; }
    public IReadOnlyList<VideoGenerationExecutionBlockDto> Blocks { get; }

    public VideoGenerationExecutionException(
        string code,
        string message,
        string source = "EXECUTION",
        string attribute = "execution",
        string? requested = null,
        string? authoritative = null,
        IReadOnlyList<VideoGenerationExecutionBlockDto>? blocks = null)
        : base(message)
    {
        Code = code;
        BlockSource = source;
        Attribute = attribute;
        Requested = requested;
        Authoritative = authoritative;
        Blocks = blocks ?? [];
    }
}

public interface IVideoGenerationExecutionService
{
    Task<VideoGenerationExecutionDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default);
    Task<VideoGenerationExecutionDto> PreflightAsync(Guid shotId, string actor, CancellationToken cancellationToken = default);
    Task<VideoGenerationExecutionDto> ExecuteAsync(Guid shotId, string actor, bool confirm = false, CancellationToken cancellationToken = default);
    Task<VideoGenerationExecutionDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default);
    Task<VideoGenerationExecutionDto> RejectAsync(Guid shotId, string actor, string? reason, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadArtifactAsync(Guid shotId, Guid executionId, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<(string Suite, IReadOnlyList<string> Failures)> RunParkRegressions();
}

public sealed record CharacterStudioCreateRequestDto(
    string? Name = null,
    int? Age = null,
    string? Gender = null,
    string? StyleId = null,
    string? Description = null,
    string? CharacterId = null,
    string? ProjectId = null,
    string? EraId = null,
    string? Provider = null,
    bool Confirm = false,
    bool Generate = false,
    string? Role = null,
    string? Personality = null,
    string? ExtraDescription = null);

public sealed record CharacterStudioActionRequestDto(
    bool Confirm = false,
    string? Provider = null,
    string? EraId = null,
    string? RejectReasonCode = null,
    string? RejectReasonText = null,
    bool RegenerateAll = false);

public sealed record CharacterStudioSlotDto(
    string Type,
    string Label,
    string Verdict,
    int Score,
    bool Present,
    string? ArtifactPath,
    string? Sha256);

public sealed record CharacterStudioProgressDto(
    string Step,
    string Label,
    bool Done,
    bool Active,
    bool Failed);

public sealed record CharacterStudioCharacterDto(
    string DocumentId,
    string CharacterId,
    string CharacterName,
    string EraId,
    string StyleId,
    string StyleLabel,
    int? Age,
    string? Gender,
    string? Description,
    string Status,
    string StatusLabel,
    string StaffMessage,
    string NextAction,
    string? GateCode,
    int Coverage,
    int RequiredTotal,
    bool CanUse,
    bool OfficialLocked,
    bool AuthorityLocked,
    bool MayCreate,
    bool MayGenerate,
    bool MayApprove,
    bool MayReject,
    bool MayLock,
    bool MayRegenerate,
    bool ConfirmRequired,
    bool Generate,
    bool ProviderCalled,
    bool GeminiCalled,
    bool AutoApproved,
    bool AutoLocked,
    bool ProductionGeneration,
    bool VideoGeneration,
    string? Provider,
    string? Version,
    string? MasterSha256,
    string? DnaSha256,
    string? PrpSha256,
    string? CrpSha256,
    string? Fingerprint,
    string? RejectReasonCode,
    string? RejectReasonText,
    IReadOnlyList<CharacterStudioSlotDto> Slots,
    IReadOnlyList<CharacterStudioProgressDto> Progress,
    IReadOnlyList<CharacterReferenceHistoryItemDto> History,
    CharacterStudioV1Rules.ReferenceSelection? SuggestedReferences,
    object? Technical,
    string? ProjectVisualStyleId = null,
    string? ProjectVisualStyleSha = null,
    string? ProjectVisualStyleKey = null,
    string? ProjectVisualStyleName = null,
    string VisualStyleInherit = "INHERITED",
    string VisualStyleGate = "VALID",
    string? Role = null,
    string? Personality = null,
    string? ExtraDescription = null,
    string? IdentitySha256 = null,
    string? GenerationExecutionId = null,
    string? ReferenceSetId = null,
    IReadOnlyList<string>? ArtifactIds = null,
    int LogicalGenerationCount = 0,
    bool? ConsistencyPass = null,
    string? ProviderRequestId = null,
    bool DuplicateBlocked = false,
    int? AgeExpressionMinYears = null,
    int? AgeExpressionMaxYears = null,
    string? AgeLifeStage = null,
    string? AgeConsistencyStatus = null,
    int? AgeConsistencyScore = null,
    string? AgeConsistencyReason = null,
    IReadOnlyList<AgeArtifactDto>? AgeArtifacts = null,
    CharacterStudioAgeDto? AgeProfile = null,
    CharacterStudioAppearanceDto? AppearanceProfile = null,
    string? AppearanceConsistencyStatus = null,
    CharacterStudioMasterRevisionCardDto? MasterRevision = null,
    bool MayRequestMasterRevision = false,
    string? StyleConformanceStatus = null,
    string? VisualUniverseGate = null,
    string? VisualUniverseAuthoritySha = null,
    string? VisualUniverseVersion = null,
    bool StyleStale = false,
    string? VisualUniversePvsSha = null,
    string? VisualUniverseCdlSha = null,
    string? VisualCompiler = null,
    string? VisualCompilerStatus = null,
    bool MayScoreIdentity = false);

public sealed record CharacterStudioReferenceGenerationRequestDto(
    string? Provider = "GEMINI",
    bool Confirm = false,
    string? ProjectId = null,
    string? EraId = null);

public sealed record CharacterStudioAuthorityShasDto(
    string? ProjectVisualStyleSha,
    string? IdentitySha,
    string? MasterSha,
    string? DnaSha,
    string? PrpSha,
    string? CrpSha,
    string? Fingerprint);

public sealed record CharacterStudioReferenceGenerationResultDto(
    string DocumentId,
    string Status,
    string CharacterId,
    string? ReferenceSetId,
    string? GenerationExecutionId,
    IReadOnlyList<string> ArtifactIds,
    CharacterStudioAuthorityShasDto AuthorityShas,
    bool Duplicate,
    string? DuplicateStatus,
    bool ProviderCalled,
    bool GeminiCalled,
    bool AutoApproved,
    bool AutoLocked,
    bool ProductionGeneration,
    bool VideoGeneration,
    int LogicalGenerationCount,
    bool? ConsistencyPass,
    string? ProviderRequestId,
    string? GateCode,
    string StaffMessage,
    CharacterStudioCharacterDto Character);

public interface ICharacterStudioGenerationService
{
    Task<CharacterStudioReferenceGenerationResultDto> GenerateCharacterReferenceSet(
        string characterId,
        CharacterStudioReferenceGenerationRequestDto request,
        string actor,
        CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<string> RunAgeConsistencyRegression();
    IReadOnlyList<string> RunAgeGenerationIntegrationRegression();
    IReadOnlyList<string> RunAgeGateRegression();
    IReadOnlyList<string> RunAppearanceProfileRegression();
    IReadOnlyList<string> RunCharacterDesignLanguageRegression();
    IReadOnlyList<string> RunCharacterDesignLanguageV2Regression();
    IReadOnlyList<string> RunVisualUniverseAuthorityRegression();
    IReadOnlyList<string> RunMasterRevisionRegression();
    IReadOnlyList<string> RunMasterRevisionDirectorReviewRegression();
    IReadOnlyList<string> RunFirstMasterVisualIngressRegression();
}

public sealed record CharacterStudioMasterRevisionCardDto(
    string Status,
    string? RevisionReason,
    string? CurrentMasterSha,
    string? CandidateMasterSha,
    string? CandidateVersion,
    bool MayRequest,
    bool MayApprove,
    bool MayReject,
    bool MayLock,
    string? GateCode = null,
    string? Fingerprint = null,
    bool CurrentAuthority = true,
    bool CandidateIsAuthority = false,
    string? CurrentVersion = "V1",
    string? AgeTarget = null,
    string? AppearanceSummary = null,
    string? RejectionReason = null,
    string? ReviewDecision = null,
    string? ReviewedBy = null,
    string? ReviewNotes = null,
    string? HistoricalMasterSha = null,
    string? HistoricalMasterVersion = "V1",
    bool CrpStale = false,
    bool DnaStale = false,
    bool PrpStale = false);

public sealed record CharacterStudioListDto(
    string DocumentId,
    IReadOnlyList<CharacterStudioCharacterDto> Items,
    int Total);

public sealed record CharacterStudioStyleListDto(
    string DocumentId,
    IReadOnlyList<CharacterStudioV1Rules.StylePreset> Items);

public interface ICharacterStudioOrchestrator
{
    Task<CharacterStudioListDto> ListAsync(CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> GetAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> CreateAsync(
        CharacterStudioCreateRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> GenerateAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> RegenerateAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> ScoreIdentityAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> ApproveAsync(
        string characterId, string actor, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> RejectAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> LockAsync(
        string characterId, string actor, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadReferenceAsync(
        string characterId, string type, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadMasterAsync(
        string characterId, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    CharacterStudioStyleListDto Styles();
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<string> RunIdentityLockRegression();
    Task<CharacterAgeConsistencyDto> GetAgeConsistencyAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> ReviewAgeConsistencyAsync(
        string characterId, CharacterAgeConsistencyReviewRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterStudioCharacterDto> ReviewAppearanceConsistencyAsync(
        string characterId, CharacterAppearanceConsistencyReviewRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunAgeConsistencyRegression();
    IReadOnlyList<string> RunAgeGenerationIntegrationRegression();
    IReadOnlyList<string> RunAgeGateRegression();
    IReadOnlyList<string> RunAppearanceProfileRegression();
    IReadOnlyList<string> RunCharacterDesignLanguageRegression();
    IReadOnlyList<string> RunCharacterDesignLanguageV2Regression();
    IReadOnlyList<string> RunVisualUniverseAuthorityRegression();
    IReadOnlyList<string> RunMasterRevisionRegression();
    IReadOnlyList<string> RunMasterRevisionDirectorReviewRegression();
    IReadOnlyList<string> RunFirstMasterVisualIngressRegression();
    Task<CharacterMasterRevisionDto> RequestMasterRevisionAsync(
        string characterId, CharacterMasterRevisionRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<CharacterMasterRevisionDto> ApproveMasterRevisionAsync(
        string characterId, string actor, CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default);
    Task<CharacterMasterRevisionDto> RejectMasterRevisionAsync(
        string characterId, string actor, CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default);
    Task<CharacterMasterRevisionDto> LockMasterRevisionAsync(
        string characterId, string actor, CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadCandidateMasterAsync(
        string characterId, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string Mime)?> ReadPreviousMasterAsync(
        string characterId, string eraId = "ERA-01",
        CancellationToken cancellationToken = default);
}

public sealed record ProjectVisualStyleDto(
    string DocumentId,
    Guid? Id,
    string ProjectId,
    string? StyleKey,
    string? StyleName,
    string? Description,
    string? RenderingStyle,
    string? CharacterStyle,
    string? EnvironmentStyle,
    string? LightingStyle,
    string? ColorStyle,
    string? CameraStyle,
    string? TextureStyle,
    string? RealismLevel,
    string? AgeRepresentationRule,
    string? AnatomyRule,
    string? ConsistencyRules,
    string? NegativeRules,
    string Version,
    string Status,
    string? Sha,
    string Authority,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LockedAt,
    string? LockedBy,
    IReadOnlyList<string> Preview,
    string? Prompt,
    string? Canonical,
    bool Ready,
    string? GateCode,
    string StaffMessage,
    IReadOnlyList<ProjectVisualStylePresetDto> Presets,
    ProjectVisualStyleRevisionCardDto? Revision = null);

public sealed record ProjectVisualStylePresetDto(
    string StyleKey,
    string StyleName,
    string Description,
    IReadOnlyList<string> Preview);

public sealed record ProjectVisualStyleInitializeRequest(
    string? ProjectId = null,
    string? PresetKey = null,
    bool Confirm = false,
    bool Activate = true);

public sealed record ProjectVisualStyleCharacterBindDto(
    string CharacterId,
    string InheritStatus,
    string? ProjectVisualStyleId,
    string? ProjectVisualStyleSha,
    string? StyleKey);

public interface IProjectVisualStyleAuthority
{
    Task<ProjectVisualStyleDto> GetActiveAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleDto?> GetLockedAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleCharacterBindDto> ResolveForCharacterAsync(
        string projectId, string characterId, CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleDto> ValidateAsync(string projectId, CancellationToken cancellationToken = default);
    Task<string?> GetCanonicalAsync(string projectId, CancellationToken cancellationToken = default);
    Task<string?> GetShaAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleDto> InitializeAsync(
        ProjectVisualStyleInitializeRequest request, string actor, CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleDto> CreateVersionAsync(
        ProjectVisualStyleInitializeRequest request, string actor, CancellationToken cancellationToken = default);
    IReadOnlyList<ProjectVisualStylePresetDto> Presets();
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<string> RunRevisionRegression();
    Task<ProjectVisualStyleV2DefinitionDto> GetV2Async(
        string projectId, CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleRevisionResultDto> RequestRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleRevisionResultDto> ApproveRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor,
        CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleRevisionResultDto> RejectRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor,
        CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleRevisionResultDto> LockRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor,
        CancellationToken cancellationToken = default);
    Task<ProjectVisualStyleImpactDto> ImpactAsync(
        string projectId, CancellationToken cancellationToken = default);
}

public sealed record ProjectVisualStyleRevisionRequestDto(
    string? ProjectId = null,
    bool Confirm = false,
    string? Notes = null,
    string? RejectionReason = null);

public sealed record ProjectVisualStyleRevisionCardDto(
    string Status,
    string Version,
    string? StyleName,
    string? StyleIntent,
    string? StylePromptBlock,
    string? NegativeStyleBlock,
    string? HumanRealismBoundary,
    string? FaceStyle,
    string? EyeStyle,
    string? SkinStyle,
    string? HairStyle,
    string? BodyStyle,
    string? LightingStyle,
    string? MaterialStyle,
    string? BackgroundStyle,
    string? CameraStyle,
    string? CandidateSha,
    string? CurrentSha,
    string? CurrentVersion,
    bool MayRequest,
    bool MayApprove,
    bool MayReject,
    bool MayLock,
    bool CurrentIsAuthority,
    bool CandidateIsAuthority,
    string? Fingerprint = null,
    string? GateCode = null,
    string? CanonicalStyleDescription = null,
    string? StylizationLevel = null,
    string? PhotorealismLevel = null,
    string? CharacterReadability = null,
    string? ClothingStyle = null,
    string? EnvironmentStyle = null,
    string? CharacterDesignLanguage = null);

public sealed record ProjectVisualStyleV2DefinitionDto(
    string DocumentId,
    string Version,
    string StyleName,
    string DisplayName,
    string StyleIntent,
    string CanonicalStyleDescription,
    string StylizationLevel,
    string PhotorealismLevel,
    string CharacterReadability,
    string Sha,
    string Status,
    string Canonical,
    string Prompt,
    string NegativeStyleBlock,
    string? CurrentAuthorityVersion,
    string? CurrentAuthoritySha,
    bool CurrentIsAuthority,
    bool CandidateIsAuthority,
    bool ProviderCalled,
    bool GenerationExecuted,
    ProjectVisualStyleRevisionCardDto? Revision = null);

public sealed record ProjectVisualStyleImpactCharacterDto(
    string CharacterId,
    string? CharacterName,
    bool OfficialLocked,
    bool MutationForbidden,
    string? MasterSha,
    string? DnaSha,
    string? PrpSha,
    string? CrpSha,
    bool MasterStale,
    bool DnaStale,
    bool PrpStale,
    bool CrpStale);

public sealed record ProjectVisualStyleImpactDto(
    string DocumentId,
    string ProjectId,
    string? CurrentVersion,
    string? CurrentSha,
    string? CandidateVersion,
    string? CandidateSha,
    string? CandidateStatus,
    IReadOnlyList<ProjectVisualStyleImpactCharacterDto> Characters,
    int LockedCount,
    int WouldStaleCount);

public sealed record ProjectVisualStyleRevisionResultDto(
    string DocumentId,
    string ProjectId,
    string Status,
    string? GateCode,
    string StaffMessage,
    bool ConfirmRequired,
    bool ProviderCalled,
    bool GenerationExecuted,
    bool AutoApproved,
    bool AutoLocked,
    string? CurrentSha,
    string? CandidateSha,
    string? CurrentVersion,
    string? CandidateVersion,
    bool CurrentIsAuthority,
    bool CandidateIsAuthority,
    ProjectVisualStyleDto? Style,
    ProjectVisualStyleRevisionCardDto? Revision,
    ProjectVisualStyleImpactDto? Impact);

public sealed record CharacterDesignLanguageDefinitionDto(
    string DocumentId,
    string Version,
    string Name,
    string Status,
    string CorePrinciple,
    string FaceLanguage,
    string EyeLanguage,
    string SkinLanguage,
    string HairLanguage,
    string BodyLanguage,
    string ExpressionLanguage,
    string GenderLanguage,
    bool AgeAdaptiveStylization,
    bool CharacterSpecificBranching,
    string PositivePromptBlock,
    string NegativePromptBlock,
    string StylizationLevel,
    string PhotorealismLevel,
    string Sha,
    string Canonical,
    bool ProviderCalled,
    bool GenerationExecuted,
    bool GeminiCalled,
    bool AutoApprove,
    bool AutoLock,
    bool Persisted);

public sealed record CharacterDesignLanguageCompilePreviewRequestDto(
    int ChronologicalAge,
    string? Gender = null,
    string? AgeAppearanceProfile = null);

public sealed record CharacterDesignLanguageV2DefinitionDto(
    string DocumentId,
    string Version,
    string Name,
    string Status,
    bool CurrentAuthority,
    string Sha256,
    string StylizationLevel,
    string StylizationTarget,
    int StylizationScale,
    string PhotorealismCeiling,
    string CartoonFloor,
    string FaceLanguage,
    string EyeLanguage,
    string NoseMouthLanguage,
    string HairLanguage,
    string BodyLanguage,
    string HandFootLanguage,
    string SkinLanguage,
    string ClothingLanguage,
    string LightingLanguage,
    string CameraLanguage,
    string RealismCeilingLanguage,
    string CartoonFloorLanguage,
    string CrossCharacterInvariants,
    string PromptBlock,
    string NegativePromptBlock,
    string Canonical,
    bool ProviderCalled,
    bool GenerationExecuted,
    bool GeminiCalled,
    bool AutoApprove,
    bool AutoLock,
    bool Persisted);

public sealed record CharacterDesignLanguageCompileDto(
    string DocumentId,
    string DefinitionSha,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string LifeStage,
    string StylizationBand,
    string AgeAdaptiveBlock,
    string PositivePromptBlock,
    string NegativePromptBlock,
    string CombinedWithAgeAppearance,
    bool ProviderCalled,
    bool GenerationExecuted,
    bool GeminiCalled,
    bool Persisted);

public sealed record VisualUniverseAuthorityDto(
    string DocumentId,
    string AuthorityId,
    string Version,
    string ProjectId,
    string StyleId,
    string StyleName,
    string Status,
    bool CurrentAuthority,
    string Sha256,
    string CharacterDesignLanguageSha,
    string StyleReferencePackSha,
    string StyleCalibrationPackSha,
    string ProjectVisualStyleSha,
    string StylizationLevel,
    string RealismCeiling,
    string PhotorealismCeiling,
    string StyleIntent,
    string FaceLanguage,
    string EyeLanguage,
    string HairLanguage,
    string BodyLanguage,
    string MaterialLanguage,
    string LightingLanguage,
    string PositiveConstraints,
    string NegativeConstraints,
    string StylePrefix,
    bool MayRequest,
    bool MayCalibrate,
    bool MayApprove,
    bool MayReject,
    bool MayLock,
    bool ProviderCalled,
    bool GeminiCalled,
    bool AutoApprove,
    bool AutoLock,
    bool Persisted,
    IReadOnlyList<VisualUniverseCalibrationSlotDto>? Calibration = null,
    string? GateCode = null,
    string? StaffMessage = null);

public sealed record VisualUniverseCalibrationSlotDto(
    string Code,
    string Label,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string View,
    string? Path,
    string? Sha256,
    string Prompt);

public sealed record VisualUniverseCalibrationDto(
    string DocumentId,
    string Sha256,
    string Status,
    IReadOnlyList<VisualUniverseCalibrationSlotDto> Slots,
    bool ProviderCalled,
    bool GeminiCalled,
    bool GenerationExecuted);

public sealed record VisualUniverseAuthorityRequestDto(
    string? ProjectId = null,
    bool Confirm = false,
    bool Generate = false,
    string? Notes = null,
    string? RejectionReason = null);

public interface IVisualUniverseAuthority
{
    Task<VisualUniverseAuthorityDto> GetAsync(string projectId, CancellationToken cancellationToken = default);
    Task<VisualUniverseAuthorityDto> RequestRevisionAsync(
        string projectId, VisualUniverseAuthorityRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<VisualUniverseCalibrationDto> GetCalibrationAsync(
        string projectId, CancellationToken cancellationToken = default);
    Task<VisualUniverseCalibrationDto> RequestCalibrationAsync(
        string projectId, VisualUniverseAuthorityRequestDto request, string actor,
        CancellationToken cancellationToken = default);
    Task<VisualUniverseAuthorityDto> ApproveAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor,
        CancellationToken cancellationToken = default);
    Task<VisualUniverseAuthorityDto> RejectAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor,
        CancellationToken cancellationToken = default);
    Task<VisualUniverseAuthorityDto> LockAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor,
        CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

/// <summary>Read-only resolved visual-universe state. Not a second authority. Not persisted by the resolver.</summary>
public sealed record VisualUniverseSnapshot(
    string ProjectId,
    string VisualUniverseId,
    string VisualUniverseSha,
    string VisualUniverseStatus,
    string PvsSha,
    string CdlSha,
    string? CalibrationPackSha,
    string? CalibrationPackId,
    string StylizationLevel,
    string PhotorealismCeiling,
    string CameraLanguage,
    string RenderingLanguage,
    string SnapshotVersion,
    bool PvsIsCurrentAuthority,
    bool VuaIsCurrentAuthority);

public sealed record UnifiedVisualIdentityLayer(
    string? CharacterId = null,
    string? CharacterMasterSha = null,
    string? CharacterCanonSha = null,
    string? IdentityText = null);

public sealed record UnifiedVisualSceneLayer(
    string? Story = null,
    string? Action = null,
    string? Emotion = null,
    string? Location = null,
    string? SceneObjects = null,
    string? CompositionIntent = null);

public sealed record UnifiedVisualContract(
    string ProjectId,
    string VisualUniverseId,
    string VisualUniverseSha,
    string PvsSha,
    string CdlSha,
    string? CalibrationPackSha,
    string? CharacterId,
    string? CharacterMasterSha,
    string? CharacterCanonSha,
    string? View,
    string CameraLanguage,
    string RenderingLanguage,
    string SceneLanguage,
    string StylizationLevel,
    string PhotorealismCeiling,
    string PromptVersion,
    string CompiledPromptSha,
    string CompiledPrompt,
    string StyleLayer,
    string IdentityLayer);

public interface IVisualUniverseSnapshotResolver
{
    Task<VisualUniverseSnapshot> GetCurrentAsync(string projectId, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
}

public interface IUnifiedVisualCompiler
{
    UnifiedVisualContract Compile(
        VisualUniverseSnapshot snapshot,
        UnifiedVisualIdentityLayer? identity = null,
        UnifiedVisualSceneLayer? scene = null,
        string? view = null);
    IReadOnlyList<string> RunRegression();
}

public sealed record VisualCalibrationPackDto(
    string DocumentId,
    string PackId,
    string Version,
    string Status,
    string Sha256,
    string VisualUniverseSha,
    string ProjectVisualStyleSha,
    string CharacterDesignLanguageSha,
    bool CurrentAuthority,
    bool? DirectorPass,
    string PhotorealismLevel,
    string? RejectionReason,
    IReadOnlyList<VisualCalibrationSubjectDto> Subjects,
    VisualCalibrationImpactDto Impact,
    string? GateCode,
    bool ProviderCalled,
    bool GenerationExecuted,
    bool GeminiCalled,
    bool AutoApprove,
    bool AutoLock,
    string? StaffMessage = null,
    VisualCalibrationCoverageDto? Coverage = null,
    bool AuthorityTransitioned = false,
    string? LockedAt = null,
    string? LockedBy = null,
    string? GenerationExecutionId = null,
    VisualCalibrationIdentityCoverageDto? IdentityCoverage = null,
    IReadOnlyList<IdentityConditionedSlotPlan>? IdentityPlan = null,
    string? CalibrationRunId = null,
    string? LiveStatus = null);

public sealed record VisualCalibrationSubjectDto(
    string CalibrationSubjectId,
    string Label,
    string Gender,
    int ChronologicalAge,
    int TargetAppearanceAgeMin,
    int TargetAppearanceAgeMax,
    string AgeAppearanceProfile,
    string Role,
    string BodyType,
    string GenerationPurpose,
    string Status,
    IReadOnlyList<VisualCalibrationArtifactDto> Artifacts);

public sealed record VisualCalibrationArtifactDto(
    string SubjectId,
    string ViewType,
    string Prompt,
    string? Path,
    string? Sha256,
    string Status,
    string? Kind = null,
    string? SlotId = null,
    string? ImageUrl = null,
    string? GenerationStatus = null,
    string? GeneratedAt = null,
    string? ExecutionId = null,
    string? VisualUniverseSha = null,
    string? ProjectVisualStyleSha = null,
    string? CharacterDesignLanguageSha = null,
    string? IdentityAnchorSlot = null,
    string? IdentityAnchorSha256 = null,
    string? ReferenceRole = null);

public sealed record VisualCalibrationCoverageDto(
    int Required,
    int Generated,
    int Valid,
    int Missing);

public sealed record VisualCalibrationImpactRowDto(
    string CharacterId,
    bool OfficialLocked,
    bool MutationForbidden,
    bool VisualUniverseStale,
    string? BoundVisualUniverseSha);

public sealed record VisualCalibrationImpactDto(
    int CharactersAffected,
    int LockedCharacters,
    int PotentiallyStale,
    IReadOnlyList<VisualCalibrationImpactRowDto>? Characters = null);

public sealed record VisualCalibrationRequestDto(
    string? ProjectId = null,
    string? PackId = null,
    bool Confirm = false,
    bool Generate = false,
    bool DirectorPass = false,
    string? PhotorealismLevel = null,
    string? RejectionReason = null,
    string? Notes = null);

public interface IVisualCalibrationPackService
{
    Task<VisualCalibrationPackDto> CreatePackAsync(
        VisualCalibrationRequestDto request, string actor, CancellationToken cancellationToken = default);
    Task<VisualCalibrationPackDto> GenerateAsync(
        string packId, VisualCalibrationRequestDto request, string actor, CancellationToken cancellationToken = default);
    Task<VisualCalibrationPackDto> GetAsync(string packId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VisualCalibrationArtifactDto>> GetArtifactsAsync(
        string packId, CancellationToken cancellationToken = default);
    Task<VisualCalibrationPackDto> ApproveAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, CancellationToken cancellationToken = default);
    Task<VisualCalibrationPackDto> RejectAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, CancellationToken cancellationToken = default);
    Task<VisualCalibrationPackDto> LockAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, CancellationToken cancellationToken = default);
    Task<VisualCalibrationCoverageDto> GetCoverageAsync(string packId, CancellationToken cancellationToken = default);
    IReadOnlyList<string> RunRegression();
    IReadOnlyList<string> RunHardeningRegression();
    IReadOnlyList<string> RunLiveGenerationReadinessRegression();
    IReadOnlyList<string> RunLiveGenerationRegression();
    Task<(byte[] Bytes, string Mime)?> ReadSlotImageAsync(
        string packId, string subjectId, string viewType, CancellationToken cancellationToken = default);
    Task<VisualCalibrationPackV1Rules.PackSnapshot> GetSnapshotAsync(
        string packId, CancellationToken cancellationToken = default);
}
