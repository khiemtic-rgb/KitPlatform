namespace PharmaCore.Application.Sales;

public interface ISalesService
{
    Task<IReadOnlyList<CustomerListItemDto>> SearchCustomersAsync(
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PosProductLookupDto?> LookupProductAsync(
        string barcode,
        Guid warehouseId,
        short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default);

    Task<PosStockCheckDto?> GetPosStockByUnitAsync(
        Guid warehouseId,
        Guid productUnitId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PosStockCheckDto>> GetPosStockBulkAsync(
        Guid warehouseId,
        IReadOnlyList<Guid> productUnitIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PosProductSearchItemDto>> SearchPosProductsAsync(
        string search,
        Guid warehouseId,
        short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default);

    Task<PosAllocationPreviewDto> PreviewPosAllocationAsync(
        PosAllocationPreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesOrderListItemDto>> GetOrdersAsync(
        CancellationToken cancellationToken = default);

    Task<SalesOrderDetailDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SalesOrderDetailDto> CreateSaleAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<SalesOrderDetailDto?> UpdateDraftSaleAsync(
        Guid id,
        UpdateDraftSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<SalesOrderDetailDto?> CompleteDraftSaleAsync(
        Guid id,
        IReadOnlyList<CreateSalePaymentRequest>? payments,
        CancellationToken cancellationToken = default);

    Task<SalesOrderDetailDto?> CancelDraftSaleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SalesReturnDetailDto> CreateSaleReturnAsync(
        Guid salesOrderId,
        CreateSaleReturnRequest request,
        CancellationToken cancellationToken = default);

    Task<SalesReturnDetailDto?> GetSaleReturnAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsAsync(
        int limit = 50,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsByOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<SalesShiftSummaryDto> GetShiftSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesShiftListItemDto>> GetShiftsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<SalesShiftDetailDto?> GetOpenShiftAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    Task<SalesShiftDetailDto?> GetShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SalesShiftDetailDto> OpenShiftAsync(
        OpenSalesShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<SalesShiftDetailDto> CloseShiftAsync(
        Guid id,
        CloseSalesShiftRequest request,
        CancellationToken cancellationToken = default);
}
