namespace KitPlatform.Packs.Pharmacy.Sales;

public interface ICustomerReceivablesService
{
    Task<IReadOnlyList<CustomerReceivablesRowDto>> GetSummaryAsync(
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default);

    Task<CustomerReceivablesDetailDto?> GetDetailAsync(
        Guid customerId,
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default);
}

public interface ICustomerPaymentService
{
    Task<IReadOnlyList<CustomerPaymentListItemDto>> GetAllAsync(
        CustomerPaymentListFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentListItemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerPaymentListItemDto> CreateAsync(
        CreateCustomerPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentListItemDto?> UpdateAsync(
        Guid id,
        UpdateCustomerPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<CustomerPaymentListItemDto?> PostAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CustomerPaymentListItemDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
