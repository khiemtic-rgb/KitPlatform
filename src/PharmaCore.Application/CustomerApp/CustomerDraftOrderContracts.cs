namespace PharmaCore.Application.CustomerApp;

public static class CustomerDraftOrderStatuses
{
    public const short Draft = 1;
    public const short Sent = 2;
    public const short Confirmed = 3;
    public const short Completed = 4;
    public const short Cancelled = 5;
    public const short Expired = 6;
}

public sealed record CustomerDraftOrderLineRequest(
    Guid ProductId,
    Guid ProductUnitId,
    decimal Quantity,
    short? DiscountType = null,
    decimal? DiscountValue = null,
    string? DosageNote = null);

public sealed record UpsertCustomerDraftOrderRequest(
    Guid CustomerId,
    Guid? ChatThreadId,
    Guid? WarehouseId,
    short PriceType,
    IReadOnlyList<CustomerDraftOrderLineRequest> Items,
    short? OrderDiscountType = null,
    decimal? OrderDiscountValue = null,
    string? Notes = null);

public sealed record CustomerDraftOrderLineDto(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    Guid ProductUnitId,
    string ProductCode,
    string ProductName,
    string UnitName,
    decimal Quantity,
    decimal UnitPrice,
    short? LineDiscountType,
    decimal? LineDiscountValue,
    decimal LineAmount,
    string? DosageNote);

public sealed record CustomerDraftOrderDto(
    Guid Id,
    string DraftNumber,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    Guid? ChatThreadId,
    Guid WarehouseId,
    short Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    short? OrderDiscountType,
    decimal? OrderDiscountValue,
    string? Notes,
    DateTimeOffset? SentAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ExpiresAt,
    Guid? SalesOrderId,
    string? SalesOrderNumber,
    IReadOnlyList<CustomerDraftOrderLineDto> Items);

public sealed record CustomerDraftOrderListItemDto(
    Guid Id,
    string DraftNumber,
    Guid CustomerId,
    string CustomerName,
    short Status,
    decimal TotalAmount,
    int ItemCount,
    DateTimeOffset? SentAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? HiddenByCustomerAt);

public sealed record CustomerDraftOrderListResult(IReadOnlyList<CustomerDraftOrderListItemDto> Items);

public sealed record CustomerDraftOrderPosLineDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    decimal Quantity,
    decimal UnitPrice,
    short? DiscountType,
    decimal? DiscountValue,
    string? DosageNote);

public sealed record CustomerDraftOrderPosLoadDto(
    Guid DraftOrderId,
    string DraftNumber,
    Guid CustomerId,
    Guid WarehouseId,
    short? OrderDiscountType,
    decimal? OrderDiscountValue,
    string? Notes,
    IReadOnlyList<CustomerDraftOrderPosLineDto> Lines);

public sealed record LinkCustomerDraftOrderRequest(Guid SalesOrderId);

public interface ICustomerDraftOrderService
{
    Task<CustomerDraftOrderListResult> ListForStaffAsync(
        Guid tenantId,
        Guid? customerId,
        short[]? statuses,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto?> GetForStaffAsync(
        Guid tenantId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        UpsertCustomerDraftOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto> UpdateAsync(
        Guid tenantId,
        Guid userId,
        Guid draftOrderId,
        UpsertCustomerDraftOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto> SendAsync(
        Guid tenantId,
        Guid userId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto> CancelAsync(
        Guid tenantId,
        Guid userId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderPosLoadDto?> GetPosLoadAsync(
        Guid tenantId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task LinkSalesOrderAsync(
        Guid tenantId,
        Guid draftOrderId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderListResult> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto> ConfirmAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task HideForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);

    Task<CustomerDraftOrderDto> CancelForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default);
}
