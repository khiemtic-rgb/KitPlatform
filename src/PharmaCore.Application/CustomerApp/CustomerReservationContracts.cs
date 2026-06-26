namespace PharmaCore.Application.CustomerApp;

public static class CustomerReservationStatuses
{
    public const short Pending = 1;
    public const short Confirmed = 2;
    public const short Ready = 3;
    public const short Collected = 4;
    public const short Cancelled = 5;
    public const short Rejected = 6;
}

public static class CustomerReservationFulfillmentTypes
{
    public const short Pickup = 1;
    public const short Delivery = 2;
}

public sealed record CustomerReservationLineRequest(
    Guid ProductId,
    decimal Quantity,
    string? CustomerNote = null);

public sealed record CreateCustomerReservationRequest(
    short FulfillmentType,
    Guid? AddressId,
    string? Notes,
    IReadOnlyList<CustomerReservationLineRequest> Items);

public sealed record CustomerReservationLineDto(
    Guid Id,
    int LineNumber,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string UnitName,
    decimal Quantity,
    string? CustomerNote);

public sealed record CustomerReservationDto(
    Guid Id,
    string ReservationNumber,
    short Status,
    short FulfillmentType,
    Guid? AddressId,
    string? AddressSummary,
    string? Notes,
    string? StaffNotes,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? CollectedAt,
    Guid? SalesOrderId,
    string? SalesOrderNumber,
    IReadOnlyList<CustomerReservationLineDto> Items);

public sealed record CustomerReservationListItemDto(
    Guid Id,
    string ReservationNumber,
    short Status,
    short FulfillmentType,
    int ItemCount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReadyAt);

public sealed record CustomerReservationStaffListItemDto(
    Guid Id,
    string ReservationNumber,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    short Status,
    short FulfillmentType,
    int ItemCount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReadyAt);

public sealed record CustomerReservationListResult(IReadOnlyList<CustomerReservationListItemDto> Items);

public sealed record CustomerReservationStaffListResult(IReadOnlyList<CustomerReservationStaffListItemDto> Items);

public sealed record UpdateCustomerReservationStaffNotesRequest(string? StaffNotes);

public sealed record CustomerReservationPosLineDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    decimal Quantity,
    string? CustomerNote);

public sealed record CustomerReservationPosLoadDto(
    Guid ReservationId,
    string ReservationNumber,
    Guid CustomerId,
    Guid WarehouseId,
    string? Notes,
    IReadOnlyList<CustomerReservationPosLineDto> Lines);

public sealed record LinkCustomerReservationRequest(Guid SalesOrderId);

public interface ICustomerReservationService
{
    Task<CustomerReservationListResult> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> CreateForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CreateCustomerReservationRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> CancelForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationStaffListResult> ListForStaffAsync(
        Guid tenantId,
        short[]? statuses,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto?> GetForStaffAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> ConfirmAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> RejectAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> MarkReadyAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> MarkCollectedAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationDto> UpdateStaffNotesAsync(
        Guid tenantId,
        Guid reservationId,
        UpdateCustomerReservationStaffNotesRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerReservationPosLoadDto?> GetPosLoadAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default);

    Task LinkSalesOrderAsync(
        Guid tenantId,
        Guid reservationId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default);
}
