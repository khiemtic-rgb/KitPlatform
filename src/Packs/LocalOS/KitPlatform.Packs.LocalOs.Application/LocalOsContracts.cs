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
    DateTimeOffset? ExpiresAt);

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
    string? Status);

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
    string Token,
    string Template,
    List<string>? Categories,
    string Title,
    string? Quantity,
    string PlaceText,
    string WorkingTime,
    string? SalaryText,
    string? Requirements,
    string ContactName);

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

public interface ILocalOsListingService
{
    Task<IReadOnlyList<LocalListingDto>> ListAsync(LocalListingQuery query, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> GetAsync(Guid id, bool publicOnly, CancellationToken cancellationToken = default);
    Task<LocalListingDto> CreateAsync(UpsertLocalListingRequest request, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> UpdateAsync(Guid id, UpsertLocalListingRequest request, CancellationToken cancellationToken = default);
    Task<LocalListingDto?> SetStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}

public interface ILocalOsPublisherService
{
    Task<RequestPublisherOtpResult> RequestOtpAsync(string phone, CancellationToken cancellationToken = default);
    Task<PublisherSessionDto?> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken = default);
    Task<PublishJobResult> PublishJobAsync(PublishJobRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CommunityGroupDto>> RecommendGroupsAsync(string category, string audience, CancellationToken cancellationToken = default);
    Task TrackShareAsync(TrackShareRequest request, CancellationToken cancellationToken = default);
}
