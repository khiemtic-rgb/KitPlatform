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
    string? Engine);

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
    bool VideoVerified = false);

public sealed record ContentSeriesLipsyncStartRequest(
    string ClipId,
    string VideoUrl,
    string AudioBase64,
    string? Mime = null,
    string? SyncMode = null);

public sealed record ContentSeriesStillRefDto(
    string Name,
    string ImageDataUrl,
    string? Role = null);

public sealed record ContentSeriesStillRequest(
    string Prompt,
    string Aspect,
    IReadOnlyList<ContentSeriesStillRefDto> References);

public sealed record ContentSeriesStillDto(
    string ImageDataUrl,
    string Model,
    string Aspect);

public sealed record ContentSeriesStillQaRequest(
    string ImageDataUrl,
    string SpecJson);

public sealed record ContentSeriesStillQaDto(
    string Status,
    int? Total,
    IReadOnlyDictionary<string, int>? Axes,
    IReadOnlyList<string> HardFails,
    string? Notes);

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
    string? Accent = null);

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

public interface IContentSeriesPilotService
{
    Task<ContentSeriesPilotDto> GetAsync(string seriesCode, CancellationToken cancellationToken = default);
    Task<ContentSeriesPilotDto> UpsertAsync(
        UpsertContentSeriesPilotRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentSeriesVoiceDto>> ListVoicesAsync(CancellationToken cancellationToken = default);
    Task<byte[]> PreviewTtsAsync(
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
    string VideoUrl,
    double Seconds,
    IReadOnlyList<ContentSeriesAssembleVoiceDto> Voices,
    double UsableStart = 0,
    double? UsableEnd = null,
    bool UseVideoAudio = false);

public sealed record ContentSeriesAssembleRequest(
    string FileStem,
    IReadOnlyList<ContentSeriesAssembleClipDto> Clips,
    string? Aspect = null);

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
