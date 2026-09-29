using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>Article Content Series Engine V1 — not Famixa video series_pilot.</summary>
public static class ContentArticleSeriesStatuses
{
    public const string Draft = "DRAFT";
    public const string Planned = "PLANNED";
    public const string Active = "ACTIVE";
    public const string Paused = "PAUSED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

public static class ContentArticleEpisodeStatuses
{
    public const string Planned = "PLANNED";
    public const string Briefing = "BRIEFING";
    public const string Ready = "READY";
    public const string Generating = "GENERATING";
    public const string Review = "REVIEW";
    public const string Approved = "APPROVED";
    public const string Scheduled = "SCHEDULED";
    public const string Published = "PUBLISHED";
    public const string Analyzed = "ANALYZED";
    public const string Paused = "PAUSED";
    public const string Cancelled = "CANCELLED";
}

public sealed record ContentArticleSeriesDto(
    Guid Id,
    Guid BrandId,
    string BrandCode,
    string BrandName,
    Guid SourcePackageId,
    string SourcePackageTitle,
    Guid CorePackageId,
    string CoreIdeaTitle,
    string Code,
    string Name,
    string? Description,
    string? Objective,
    string? Audience,
    string? CoreMessage,
    string Status,
    int EpisodeCount,
    int CurrentEpisodeNo,
    int PlannedEpisodeRows,
    int PublishedCount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    JsonElement Blueprint,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ContentArticleSeriesDetailDto(
    ContentArticleSeriesDto Series,
    IReadOnlyList<ContentArticleEpisodeDto> Episodes);

public sealed record ContentArticleEpisodeDto(
    Guid Id,
    Guid SeriesId,
    int EpisodeNo,
    string Code,
    string Title,
    string? Objective,
    string? Angle,
    string? KeyMessage,
    string Status,
    DateTimeOffset? PlannedAt,
    DateTimeOffset? PublishedAt,
    Guid? PreviousEpisodeId,
    Guid? NextEpisodeId,
    JsonElement Continuity,
    Guid? ContentTopicId,
    string? TopicStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ContentArticleEpisodeDetailDto(
    ContentArticleSeriesDto Series,
    ContentArticleEpisodeDto Episode,
    ContentArticleEpisodeDto? Previous,
    ContentArticleEpisodeDto? Next,
    ContentTopicDetailDto? TopicDetail,
    string ContinuityContext);

public sealed record CreateContentArticleSeriesRequest(
    Guid SourcePackageId,
    string? Name = null,
    string? Description = null,
    int EpisodeCount = 10,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null);

public sealed record UpdateContentArticleSeriesRequest(
    string? Name = null,
    string? Description = null,
    string? Objective = null,
    string? Audience = null,
    string? CoreMessage = null,
    string? Status = null,
    int? EpisodeCount = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    JsonElement? Blueprint = null);

public sealed record UpsertContentArticleEpisodeRequest(
    int? EpisodeNo = null,
    string? Title = null,
    string? Objective = null,
    string? Angle = null,
    string? KeyMessage = null,
    string? Status = null,
    DateTimeOffset? PlannedAt = null,
    JsonElement? Continuity = null);

public sealed record GenerateArticleSeriesBlueprintRequest(
    int? EpisodeCount = null,
    int PlanBatch = 10,
    bool ReplaceExistingPlan = false);

public sealed record GenerateArticleEpisodeNextRequest(
    int Count = 10,
    string Mode = "plan");

public sealed record GenerateArticleEpisodeResultDto(
    ContentArticleEpisodeDto Episode,
    EnqueueWorkResultDto? Work,
    string Message);

public sealed record GenerateArticleEpisodeBatchResultDto(
    IReadOnlyList<ContentArticleEpisodeDto> Episodes,
    IReadOnlyList<EnqueueWorkResultDto> Jobs,
    string Message);

public interface IContentArticleSeriesService
{
    Task<IReadOnlyList<ContentArticleSeriesDto>> ListAsync(
        Guid? brandId,
        string? status,
        Guid? corePackageId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<ContentArticleSeriesDetailDto?> GetDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentArticleSeriesDto> CreateAsync(
        CreateContentArticleSeriesRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentArticleSeriesDto?> UpdateAsync(
        Guid id,
        UpdateContentArticleSeriesRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContentArticleSeriesDto> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ContentArticleSeriesDetailDto> GenerateBlueprintAsync(
        Guid id,
        GenerateArticleSeriesBlueprintRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentArticleEpisodeDto>> ListEpisodesAsync(
        Guid seriesId,
        CancellationToken cancellationToken = default);

    Task<ContentArticleEpisodeDetailDto?> GetEpisodeDetailAsync(
        Guid seriesId,
        Guid episodeId,
        CancellationToken cancellationToken = default);

    Task<ContentArticleEpisodeDto> AddEpisodeAsync(
        Guid seriesId,
        UpsertContentArticleEpisodeRequest request,
        CancellationToken cancellationToken = default);

    Task<ContentArticleEpisodeDto?> UpdateEpisodeAsync(
        Guid seriesId,
        Guid episodeId,
        UpsertContentArticleEpisodeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteEpisodeAsync(
        Guid seriesId,
        Guid episodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentArticleEpisodeDto>> ReorderEpisodesAsync(
        Guid seriesId,
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken = default);

    Task<ContentArticleEpisodeDto> GenerateBriefAsync(
        Guid seriesId,
        Guid episodeId,
        CancellationToken cancellationToken = default);

    Task<GenerateArticleEpisodeResultDto> GenerateContentAsync(
        Guid seriesId,
        Guid episodeId,
        GenerateContentRequest? request,
        CancellationToken cancellationToken = default);

    Task<GenerateArticleEpisodeBatchResultDto> GenerateNextAsync(
        Guid seriesId,
        GenerateArticleEpisodeNextRequest request,
        CancellationToken cancellationToken = default);
}
