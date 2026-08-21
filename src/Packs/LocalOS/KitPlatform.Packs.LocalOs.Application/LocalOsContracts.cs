namespace KitPlatform.Packs.LocalOs;

public sealed record LocalListingDto(
    Guid Id,
    string Kind,
    string Title,
    string? Summary,
    string? OrganizationName,
    string? PlaceText,
    IReadOnlyList<string> Audience,
    string CityCode,
    string SourceKind,
    string? SourceUrl,
    string? ContactPhone,
    string? ContactName,
    string? SalaryText,
    string? WorkingTime,
    string? EmploymentType,
    string? Category,
    string? Requirements,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? RegistrationUrl,
    decimal? PriceMonth,
    string? RoomType,
    string Trust,
    bool SafetyFlag,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? ExpiresAt,
    Guid? SourceId,
    string? SourceName);

public sealed record UpsertLocalListingRequest(
    string Kind,
    string Title,
    string? Summary,
    string? OrganizationName,
    string? PlaceText,
    List<string>? Audience,
    string? CityCode,
    string? SourceKind,
    string? SourceUrl,
    string? ContactPhone,
    string? ContactName,
    string? SalaryText,
    string? WorkingTime,
    string? EmploymentType,
    string? Category,
    string? Requirements,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? RegistrationUrl,
    decimal? PriceMonth,
    string? RoomType,
    string? Trust,
    bool? SafetyFlag,
    string? Status,
    Guid? SourceId = null);

public sealed record LocalListingQuery(
    string? Kind,
    string? Q,
    string? CityCode,
    string? Status,
    bool PublicOnly);

public sealed record SetLocalListingStatusRequest(string Status);

public sealed record RequestPublisherOtpRequest(string Phone);
public sealed record RequestPublisherOtpResult(bool Sent, string? DebugCode);

public sealed record VerifyPublisherOtpRequest(string Phone, string Code);
public sealed record PublisherSessionDto(string Token, Guid PublisherId, string Phone, int ExistingListingCount);

public sealed record PublishJobRequest(
    string? Token,
    string? Kind,
    string? Template,
    List<string>? Categories,
    string Title,
    string? Quantity,
    string PlaceText,
    string? WorkingTime,
    string? SalaryText,
    string? Requirements,
    string ContactName,
    string? Phone = null,
    string? RoomType = null,
    string? StartAt = null,
    string? EndAt = null,
    string? RegistrationUrl = null);

public sealed record CommunityGroupDto(
    Guid Id,
    string Name,
    string Url,
    string Platform,
    string Category,
    string Audience,
    string Geo);

public sealed record PublishJobResult(
    LocalListingDto Listing,
    string ShareText,
    string PublicUrl,
    IReadOnlyList<CommunityGroupDto> Groups,
    string ReviewNote);

public sealed record TrackShareRequest(Guid ListingId, Guid? GroupId, string EventKind);

public sealed record RewriteLocalListingRequest(string Text, string? Kind);

public sealed record RewriteLocalListingResult(
    string Title,
    string Body,
    string? Place,
    string? Phone,
    string? Salary,
    string? ContactName,
    string? WorkingTime,
    string? Requirements,
    string? OrganizationName,
    string? EmploymentType,
    string Via,
    string? Note);

public sealed record IngestFromSourceRequest(
    string? SourceUrl,
    string? PastedText,
    string? Kind,
    Guid? SourceId = null,
    bool FromWatch = false);

public interface ILocalOsHomepagePush
{
    Task PushAfterTrustedPublishAsync(int createdCount, CancellationToken cancellationToken = default);
}

public sealed record IngestFromSourceResult(LocalListingDto Listing, string Note, bool Existing);

public sealed record LocalSourceDto(
    Guid Id,
    string SourceKind,
    string Name,
    string? Url,
    string Status,
    string Platform,
    string Category,
    string Audience,
    string Geo,
    string? Notes,
    bool WatchEnabled = false,
    DateTimeOffset? LastWatchedAt = null);

public sealed record LocalWatchRunDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string Trigger,
    int SourcesScanned,
    int LinksSeen,
    int CreatedCount,
    int SkippedExisting,
    int SkippedFilter,
    int ErrorCount,
    string? Note);

public sealed record UpsertLocalSourceRequest(
    string SourceKind,
    string Name,
    string? Url,
    string? Status,
    string? Platform,
    string? Category,
    string? Audience,
    string? Geo,
    string? Notes);

public sealed record SetLocalSourceStatusRequest(string Status);

public sealed record SubmitLocalListingReportRequest(string Reason, string? Note);

public sealed record LocalListingReportDto(
    Guid Id,
    Guid ListingId,
    string Reason,
    string? Note,
    DateTimeOffset CreatedAt,
    string? ListingTitle,
    string? ListingKind,
    string? ListingStatus);

public interface ILocalOsReportService
{
    Task<LocalListingReportDto?> SubmitAsync(
        Guid listingId,
        SubmitLocalListingReportRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalListingReportDto>> ListAsync(CancellationToken cancellationToken = default);
}

public interface ILocalOsListingService
{
    Task<IReadOnlyList<LocalListingDto>> ListAsync(LocalListingQuery query, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> GetAsync(Guid id, bool publicOnly, CancellationToken cancellationToken = default);
    Task<LocalListingDto> CreateAsync(UpsertLocalListingRequest request, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> UpdateAsync(Guid id, UpsertLocalListingRequest request, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> FindDuplicateAsync(
        string kind,
        string title,
        string? placeText,
        string? contactPhone,
        string? summary,
        string? sourceUrl,
        Guid? excludeId,
        bool onlyActive,
        CancellationToken cancellationToken = default);
}

public interface ILocalOsRewriteService
{
    Task<RewriteLocalListingResult> RewriteAsync(RewriteLocalListingRequest request, CancellationToken cancellationToken = default);
}

public interface ILocalOsIngestService
{
    Task<IngestFromSourceResult> IngestAsync(IngestFromSourceRequest request, CancellationToken cancellationToken = default);
}

public interface ILocalOsSourceService
{
    Task<IReadOnlyList<LocalSourceDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<LocalSourceDto> CreateAsync(UpsertLocalSourceRequest request, CancellationToken cancellationToken = default);
    Task<LocalSourceDto?> UpdateAsync(Guid id, UpsertLocalSourceRequest request, CancellationToken cancellationToken = default);
    Task<LocalSourceDto?> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}

public sealed record LocalWatchStartResult(LocalWatchRunDto Run, bool Began);

public interface ILocalOsWatchService
{
    Task<LocalWatchRunDto> RunAsync(string trigger, CancellationToken cancellationToken = default);
    Task<LocalWatchStartResult> StartAsync(string trigger, CancellationToken cancellationToken = default);
    Task<LocalWatchRunDto> CompleteAsync(Guid runId, string trigger, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalWatchRunDto>> ListRunsAsync(int take = 10, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> LastFinishedAtAsync(CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> LastScheduledFinishedAtAsync(CancellationToken cancellationToken = default);
    Task<bool> HasInFlightAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);
}

public interface ILocalOsPublisherService
{
    Task<RequestPublisherOtpResult> RequestOtpAsync(string phone, CancellationToken cancellationToken = default);
    Task<PublisherSessionDto?> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken = default);
    Task<PublishJobResult> PublishJobAsync(PublishJobRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommunityGroupDto>> RecommendGroupsAsync(string? category, string audience, CancellationToken cancellationToken = default);
    Task TrackShareAsync(TrackShareRequest request, CancellationToken cancellationToken = default);
}
