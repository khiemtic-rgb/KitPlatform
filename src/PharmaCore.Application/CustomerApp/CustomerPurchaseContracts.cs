namespace PharmaCore.Application.CustomerApp;

public sealed record CustomerPurchaseListItemDto(
    Guid Id,
    string OrderNumber,
    short Status,
    DateTimeOffset OrderDate,
    decimal TotalAmount,
    int ItemCount,
    decimal TotalRefunded);

public sealed record CustomerPurchaseLineDto(
    Guid Id,
    string ProductName,
    string UnitName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal ReturnedQuantity);

public sealed record CustomerPurchasePaymentDto(
    short PaymentMethod,
    decimal Amount);

public sealed record CustomerPurchaseDetailDto(
    Guid Id,
    string OrderNumber,
    short Status,
    DateTimeOffset OrderDate,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal TotalRefunded,
    string? Notes,
    int? LoyaltyPointsEarned,
    decimal LoyaltyPointsRedeemed,
    decimal LoyaltyDiscountAmount,
    decimal VoucherDiscountAmount,
    string? VoucherCode,
    IReadOnlyList<CustomerPurchaseLineDto> Items,
    IReadOnlyList<CustomerPurchasePaymentDto> Payments);

public sealed record CustomerPurchaseListResult(IReadOnlyList<CustomerPurchaseListItemDto> Items);

public interface ICustomerPurchaseService
{
    Task<CustomerPurchaseListResult> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerPurchaseDetailDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default);
}
