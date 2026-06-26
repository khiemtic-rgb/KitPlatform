namespace PharmaCore.Application.Procurement;

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDto?> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<PurchaseOrderListItemDto>> GetAllAsync(
        PurchaseOrderListFilter? filter = null,
        CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto?> UpdateAsync(Guid id, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailDto?> CloseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> PurgeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LastPurchasePriceHintDto> GetLastPurchasePriceHintAsync(
        Guid supplierId,
        Guid productId,
        CancellationToken cancellationToken = default);
}

public interface IGoodsReceiptService
{
    Task<IReadOnlyList<GoodsReceiptListItemDto>> GetAllAsync(
        GoodsReceiptListFilter? filter = null,
        CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailDto> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailDto?> CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> PurgeAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISupplierPaymentService
{
    Task<IReadOnlyList<SupplierPaymentListItemDto>> GetAllAsync(
        SupplierPaymentListFilter? filter = null,
        CancellationToken cancellationToken = default);
    Task<SupplierPaymentListItemDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierPaymentListItemDto> CreateAsync(CreateSupplierPaymentRequest request, CancellationToken cancellationToken = default);
    Task<SupplierPaymentListItemDto?> UpdateAsync(Guid id, UpdateSupplierPaymentRequest request, CancellationToken cancellationToken = default);
    Task<SupplierPaymentListItemDto?> PostAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SupplierPaymentListItemDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ISupplierPayablesService
{
    Task<IReadOnlyList<SupplierPayablesRowDto>> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<SupplierPayablesDetailDto?> GetDetailAsync(Guid supplierId, CancellationToken cancellationToken = default);
}
