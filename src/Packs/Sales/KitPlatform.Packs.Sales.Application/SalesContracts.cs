namespace KitPlatform.Packs.Sales;

public sealed record KitSalesHealthDto(string Pack, string Version, bool Ok);

public sealed record KitSalesProductDto(string Code, string DisplayName, string Status);

public sealed record KitSalesBusinessDto(
    Guid Id,
    string Name,
    string BusinessType,
    string? Province,
    string? Phone,
    string Status,
    string? Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateKitSalesBusinessRequest(
    string Name,
    string BusinessType = "pharmacy",
    string? Province = null,
    string? District = null,
    string? Phone = null,
    string? Email = null,
    string? Source = null,
    string? Notes = null);

public sealed record KitSalesLeadDto(
    Guid Id,
    Guid BusinessId,
    string BusinessName,
    string ProductCode,
    string LeadStatus,
    string LeadTemperature,
    decimal TotalScore,
    string? Source,
    Guid? OwnerUserId,
    string? NextActionCode,
    DateTimeOffset? NextActionAt,
    DateTimeOffset? LastInteractionAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateKitSalesLeadRequest(
    Guid BusinessId,
    string ProductCode = "novixa",
    string? Source = null,
    string LeadStatus = "discovered",
    string LeadTemperature = "cold",
    string? Notes = null);

public sealed record CreateKitSalesProspectRequest(
    string BusinessName,
    string ProductCode = "novixa",
    string BusinessType = "pharmacy",
    string? Province = null,
    string? Phone = null,
    string? Source = null,
    string? Notes = null);

public sealed record KitSalesPipelineBucketDto(string Status, int Count);

public sealed record KitSalesPipelineSummaryDto(
    int TotalLeads,
    IReadOnlyList<KitSalesPipelineBucketDto> ByStatus);

public interface IKitSalesDeskService
{
    Task<KitSalesHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesProductDto>> ListProductsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesBusinessDto>> ListBusinessesAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<KitSalesBusinessDto> CreateBusinessAsync(
        CreateKitSalesBusinessRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitSalesLeadDto>> ListLeadsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default);

    Task<KitSalesLeadDto> CreateLeadAsync(
        CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesLeadDto> CreateProspectAsync(
        CreateKitSalesProspectRequest request,
        CancellationToken cancellationToken = default);

    Task<KitSalesPipelineSummaryDto> GetPipelineSummaryAsync(
        CancellationToken cancellationToken = default);
}
