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
    UpdateContentAiConfigRequest? Ai);

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
    DateTimeOffset UpdatedAt);

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
    DateTimeOffset UpdatedAt);

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
    bool ImagesOnly = false);

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
    DateTimeOffset? DisplayAt,
    int VariantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ContentPackageDetailDto(
    ContentPackageDto Package,
    ContentTopicDetailDto TopicDetail);

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
    string? CtaUrl);

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

    Task<ContentPackageDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BatchApprovePackagesResultDto> ApproveBatchAsync(
        BatchApprovePackagesRequest request,
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

    /// <summary>Creatomate nếu có API key + template; không thì = PrepareStoryboard.</summary>
    Task<ContentVideoJobDto?> QueueRenderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> RefreshRenderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContentVideoJobDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class ContentOptions
{
    public const string SectionName = "Content";

    /// <summary>Gemini / Google AI Studio key. Falls back to env GEMINI_API_KEY.</summary>
    public string? GeminiApiKey { get; set; }

    public string TextModel { get; set; } = "gemini-flash-latest";
    public string? ImageModel { get; set; }
    public string AssetRoot { get; set; } = "App_Data/content-assets";

    /// <summary>Optional Creatomate API key. Falls back to env CREATOMATE_API_KEY.</summary>
    public string? CreatomateApiKey { get; set; }
}
