using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Sales;

namespace PharmaCore.Infrastructure.Sales;

internal sealed class SalesService : ISalesService
{
    private readonly SalesRepository _repository;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserAccessor _user;
    private readonly IAuditLogService _audit;

    public SalesService(
        SalesRepository repository,
        ITenantContext tenant,
        ICurrentUserAccessor user,
        IAuditLogService audit)
    {
        _repository = repository;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    private SalesDiscountPolicy DiscountPolicy =>
        SalesDiscountPolicy.FromPermissions(_user.Permissions, _user.IsInRole("ADMIN"));

    public Task<IReadOnlyList<CustomerListItemDto>> SearchCustomersAsync(
        string? search = null,
        CancellationToken cancellationToken = default) =>
        _repository.SearchCustomersAsync(search, cancellationToken);

    public async Task<PosProductLookupDto?> LookupProductAsync(
        string query,
        Guid warehouseId,
        short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var byBarcode = await _repository.LookupByBarcodeAsync(trimmed, warehouseId, priceType, cancellationToken);
        if (byBarcode is not null)
            return byBarcode;

        return await _repository.LookupByProductCodeAsync(trimmed, warehouseId, priceType, cancellationToken);
    }

    public Task<PosStockCheckDto?> GetPosStockByUnitAsync(
        Guid warehouseId,
        Guid productUnitId,
        CancellationToken cancellationToken = default) =>
        _repository.GetPosStockByUnitAsync(warehouseId, productUnitId, cancellationToken);

    public Task<PosAllocationPreviewDto> PreviewPosAllocationAsync(
        PosAllocationPreviewRequest request,
        CancellationToken cancellationToken = default) =>
        _repository.PreviewPosAllocationAsync(request, cancellationToken);

    public Task<IReadOnlyList<SalesOrderListItemDto>> GetOrdersAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetSalesOrdersAsync(cancellationToken);

    public Task<SalesOrderDetailDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetSalesOrderAsync(id, cancellationToken);

    public async Task<SalesOrderDetailDto> CreateSaleAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = request.SaveAsDraft
            ? await _repository.CreateDraftSaleAsync(request, _tenant.UserId, DiscountPolicy, cancellationToken)
            : await _repository.CreateCompletedSaleAsync(request, _tenant.UserId, DiscountPolicy, cancellationToken);
        var order = (await _repository.GetSalesOrderAsync(id, cancellationToken))!;
        await _audit.WriteAsync(
            "sales_order",
            id,
            request.SaveAsDraft ? "draft_create" : "complete",
            new { order.OrderNumber, order.TotalAmount, draft = request.SaveAsDraft },
            cancellationToken);
        return order;
    }

    public async Task<SalesOrderDetailDto?> UpdateDraftSaleAsync(
        Guid id,
        UpdateDraftSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var updated = await _repository.UpdateDraftSaleAsync(id, request, DiscountPolicy, cancellationToken);
        if (!updated) return null;
        var order = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (order is not null)
        {
            await _audit.WriteAsync(
                "sales_order",
                id,
                "draft_update",
                new { order.OrderNumber, order.TotalAmount },
                cancellationToken);
        }
        return order;
    }

    public async Task<SalesOrderDetailDto?> CompleteDraftSaleAsync(
        Guid id,
        IReadOnlyList<CreateSalePaymentRequest>? payments,
        CancellationToken cancellationToken = default)
    {
        var completed = await _repository.CompleteDraftSaleAsync(id, payments, cancellationToken);
        if (!completed) return null;
        var order = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (order is not null)
        {
            await _audit.WriteAsync(
                "sales_order",
                id,
                "complete",
                new { order.OrderNumber, order.TotalAmount, fromDraft = true },
                cancellationToken);
        }
        return order;
    }

    public async Task<SalesOrderDetailDto?> CancelDraftSaleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orderBefore = await _repository.GetSalesOrderAsync(id, cancellationToken);
        var cancelled = await _repository.CancelDraftSaleAsync(id, cancellationToken);
        if (!cancelled) return null;
        if (orderBefore is not null)
        {
            await _audit.WriteAsync(
                "sales_order",
                id,
                "cancel",
                new { orderBefore.OrderNumber },
                cancellationToken);
        }
        return await _repository.GetSalesOrderAsync(id, cancellationToken);
    }

    public async Task<SalesReturnDetailDto> CreateSaleReturnAsync(
        Guid salesOrderId,
        CreateSaleReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var returnId = await _repository.CreateSaleReturnAsync(salesOrderId, request, cancellationToken);
        var detail = (await _repository.GetSaleReturnAsync(returnId, cancellationToken))!;
        await _audit.WriteAsync(
            "sales_return",
            returnId,
            "complete",
            new { detail.ReturnNumber, detail.OrderNumber, detail.TotalRefund, salesOrderId },
            cancellationToken);
        return detail;
    }

    public Task<SalesReturnDetailDto?> GetSaleReturnAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetSaleReturnAsync(id, cancellationToken);

    public Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsAsync(
        int limit = 50,
        string? search = null,
        CancellationToken cancellationToken = default) =>
        _repository.GetSaleReturnsAsync(limit, search, cancellationToken);

    public Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsByOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        _repository.GetSaleReturnsByOrderAsync(salesOrderId, cancellationToken);

    public Task<SalesShiftSummaryDto> GetShiftSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default) =>
        _repository.GetShiftSummaryAsync(from, to, cancellationToken);

    public Task<IReadOnlyList<SalesShiftListItemDto>> GetShiftsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _repository.GetShiftsAsync(limit, cancellationToken);

    public Task<SalesShiftDetailDto?> GetOpenShiftAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default) =>
        _repository.GetOpenShiftAsync(warehouseId, cancellationToken);

    public Task<SalesShiftDetailDto?> GetShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        _repository.GetShiftAsync(id, cancellationToken);

    public async Task<SalesShiftDetailDto> OpenShiftAsync(
        OpenSalesShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await _repository.OpenShiftAsync(request, _tenant.UserId, cancellationToken);
        await _audit.WriteAsync(
            "sales_shift",
            shift.Id,
            "open",
            new { shift.ShiftNumber, shift.WarehouseName, request.OpeningCash },
            cancellationToken);
        return shift;
    }

    public async Task<SalesShiftDetailDto> CloseShiftAsync(
        Guid id,
        CloseSalesShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await _repository.CloseShiftAsync(id, request, _tenant.UserId, cancellationToken);
        await _audit.WriteAsync(
            "sales_shift",
            id,
            "close",
            new { shift.ShiftNumber, request.ClosingCash, shift.CashVariance },
            cancellationToken);
        return shift;
    }
}
