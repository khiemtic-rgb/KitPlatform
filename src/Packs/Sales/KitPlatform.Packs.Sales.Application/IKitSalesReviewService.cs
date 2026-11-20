namespace KitPlatform.Packs.Sales;

public sealed record KitSalesReviewDto(
    Guid Id,
    string TaskType,
    string Status,
    Guid? BusinessId,
    string Payload,
    DateTimeOffset CreatedAt);

public sealed record KitSalesAccountImportResult(int Inserted, int Skipped, int Reviews, int LeadsCreated);

public sealed record SalesReviewResolution(
    string Resolution,
    string? DocNumber,
    string? DocDate,
    string? SourceUrl,
    int? PdfPage);

public interface IKitSalesReviewService
{
    Task<IReadOnlyList<KitSalesReviewDto>> ListAsync(string? type, string? status, CancellationToken cancellationToken = default);

    Task<KitSalesReviewDto> ResolveAsync(Guid taskId, SalesReviewResolution resolution, CancellationToken cancellationToken = default);

    Task<KitSalesReviewDto> DismissAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<KitSalesAccountImportResult> ImportAccountsAsync(
        IReadOnlyList<DirectoryAccount> rows,
        CancellationToken cancellationToken = default);

    Task<KitSalesReviewDto> AcceptGrantAsync(Guid taskId, CancellationToken cancellationToken = default);
}
