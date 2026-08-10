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
    decimal MonthSpendEstimateUsd,
    decimal RemainingBudgetUsd,
    DateTimeOffset UpdatedAt);

public sealed record UpdateContentOrgSettingsRequest(
    decimal? MonthlyCeilingUsd,
    int? MaxImageCandidatesPerItem,
    decimal? RegenMultiplier,
    string? DefaultImageTier,
    Dictionary<string, decimal>? ImageRateUsd,
    decimal? TextPackEstimateUsd,
    List<string>? VariantKinds,
    List<string>? ConnectorTypes,
    List<string>? ChannelTypes);

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
    int? SortOrder);

public sealed record ContentSiteTargetDto(
    Guid Id,
    Guid BrandId,
    string Code,
    string Name,
    string ConnectorType,
    string? BaseUrl,
    string ConfigJson,
    string? SecretRef,
    bool IsActive,
    int SortOrder);

public sealed record UpsertContentSiteTargetRequest(
    string Code,
    string Name,
    string ConnectorType,
    string? BaseUrl,
    string? ConfigJson,
    string? SecretRef,
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
    bool IsActive,
    int SortOrder);

public sealed record UpsertContentChannelTargetRequest(
    string Code,
    string Name,
    string ChannelType,
    string? ExternalId,
    string? ConfigJson,
    string? SecretRef,
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
    int? CandidateCount = null);

public sealed record GenerateContentResultDto(
    ContentTopicDto Topic,
    IReadOnlyList<ContentVariantDto> Variants,
    IReadOnlyList<ContentAssetDto> Assets,
    decimal EstimatedSpendUsd,
    bool BudgetBlocked,
    string? Message);

public sealed record PublishContentRequest(
    IReadOnlyList<Guid>? SiteTargetIds = null,
    IReadOnlyList<Guid>? ChannelTargetIds = null,
    bool IncludeManualExport = true,
    bool RunImmediately = true);

public sealed record PublishContentResultDto(
    IReadOnlyList<ContentPublishJobDto> Jobs);

public sealed record UpsertContentTopicRequest(
    Guid BrandId,
    string Title,
    string? Pillar,
    string? Goal,
    string? CtaUrl,
    string? UtmCampaign,
    string? Priority,
    string? Status,
    string? BodyOutline);

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

    Task<ContentPublishJobDto?> RunJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentPublishJobDto>> ListJobsAsync(Guid? topicId, CancellationToken cancellationToken = default);
    Task<(byte[] Bytes, string ContentType, string FileName)?> GetAssetFileAsync(Guid assetId, CancellationToken cancellationToken = default);
}

public sealed class ContentOptions
{
    public const string SectionName = "Content";

    /// <summary>Gemini / Google AI Studio key. Falls back to env GEMINI_API_KEY.</summary>
    public string? GeminiApiKey { get; set; }

    public string TextModel { get; set; } = "gemini-flash-latest";
    public string? ImageModel { get; set; }
    public string AssetRoot { get; set; } = "App_Data/content-assets";
}
