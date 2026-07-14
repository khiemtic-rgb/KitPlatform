using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Infrastructure.Loyalty;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class SalesService : ISalesService
{
    private readonly SalesRepository _repository;
    private readonly LoyaltyPosService _loyaltyPos;
    private readonly VoucherPosService _voucherPos;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserAccessor _user;
    private readonly IAuditEngine _audit;
    private readonly IBranchAccessService _branchAccess;
    private readonly IPermissionEngine _permissions;

    public SalesService(
        SalesRepository repository,
        LoyaltyPosService loyaltyPos,
        VoucherPosService voucherPos,
        ITenantContext tenant,
        ICurrentUserAccessor user,
        IAuditEngine audit,
        IBranchAccessService branchAccess,
        IPermissionEngine permissions)
    {
        _repository = repository;
        _loyaltyPos = loyaltyPos;
        _voucherPos = voucherPos;
        _tenant = tenant;
        _user = user;
        _audit = audit;
        _branchAccess = branchAccess;
        _permissions = permissions;
    }

    private SalesDiscountPolicy DiscountPolicy => _permissions.GetSalesDiscountPolicy();

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
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var byBarcode = await _repository.LookupByBarcodeAsync(trimmed, warehouseId, priceType, cancellationToken);
        if (byBarcode is not null)
            return byBarcode;

        return await _repository.LookupByProductCodeAsync(trimmed, warehouseId, priceType, cancellationToken);
    }

    public async Task<PosStockCheckDto?> GetPosStockByUnitAsync(
        Guid warehouseId,
        Guid productUnitId,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        return await _repository.GetPosStockByUnitAsync(warehouseId, productUnitId, cancellationToken);
    }

    public async Task<IReadOnlyList<PosStockCheckDto>> GetPosStockBulkAsync(
        Guid warehouseId,
        IReadOnlyList<Guid> productUnitIds,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        return await _repository.GetPosStockBulkAsync(warehouseId, productUnitIds, cancellationToken);
    }

    public async Task<IReadOnlyList<PosProductSearchItemDto>> SearchPosProductsAsync(
        string search,
        Guid warehouseId,
        short priceType = SalesPriceTypes.Retail,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        return await _repository.SearchPosProductsAsync(search, warehouseId, priceType, cancellationToken);
    }

    public async Task<PosAllocationPreviewDto> PreviewPosAllocationAsync(
        PosAllocationPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);
        return await _repository.PreviewPosAllocationAsync(request, cancellationToken);
    }

    public Task<PosCustomerLoyaltyDto?> GetPosCustomerLoyaltyAsync(
        Guid customerId,
        decimal orderTotalBeforeRedeem,
        CancellationToken cancellationToken = default) =>
        _loyaltyPos.GetPosCustomerLoyaltyAsync(
            _tenant.TenantId,
            customerId,
            orderTotalBeforeRedeem,
            cancellationToken);

    public Task<PosCustomerVoucherListResult> GetPosCustomerVouchersAsync(
        Guid customerId,
        decimal orderTotalBeforeVoucher,
        CancellationToken cancellationToken = default) =>
        _voucherPos.GetPosCustomerVouchersAsync(
            _tenant.TenantId,
            customerId,
            orderTotalBeforeVoucher,
            cancellationToken);

    public async Task<SalesOrderPagedListResult> GetOrdersAsync(
        SalesOrderListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new SalesOrderListFilter();
        var (_, allowed) = await _branchAccess.ResolveWarehouseQueryAsync(null, cancellationToken);
        var (items, total) = await _repository.GetSalesOrdersAsync(filter, allowed, cancellationToken);
        return new SalesOrderPagedListResult(
            items, total, Math.Max(1, filter.Page), Math.Clamp(filter.PageSize, 1, 100));
    }

    public async Task<SalesOrderDetailDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (order is not null)
            await _branchAccess.EnsureWarehouseAccessAsync(order.WarehouseId, cancellationToken);
        return order;
    }

    public async Task<SalesOrderDetailDto> CreateSaleAsync(
        CreateSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);
        var id = request.SaveAsDraft
            ? await _repository.CreateDraftSaleAsync(request, _tenant.UserId, DiscountPolicy, cancellationToken)
            : await _repository.CreateCompletedSaleAsync(request, _tenant.UserId, DiscountPolicy, cancellationToken);
        var order = (await _repository.GetSalesOrderAsync(id, cancellationToken, freshSale: !request.SaveAsDraft))!;
        await _audit.WriteAsync(
            "sales_order",
            id,
            request.SaveAsDraft ? "draft_create" : "complete",
            new { order.OrderNumber, order.TotalAmount, draft = request.SaveAsDraft, warehouseId = order.WarehouseId },
            cancellationToken);
        await WritePosDiscountAuditIfNeededAsync(order, cancellationToken);
        return order;
    }

    public async Task<SalesOrderDetailDto?> UpdateDraftSaleAsync(
        Guid id,
        UpdateDraftSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (existing is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(existing.WarehouseId, cancellationToken);
        var updated = await _repository.UpdateDraftSaleAsync(id, request, DiscountPolicy, cancellationToken);
        if (!updated) return null;
        var order = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (order is not null)
        {
            await _audit.WriteAsync(
                "sales_order",
                id,
                "draft_update",
                new { order.OrderNumber, order.TotalAmount, warehouseId = order.WarehouseId },
                cancellationToken);
            await WritePosDiscountAuditIfNeededAsync(order, cancellationToken);
        }
        return order;
    }

    public async Task<SalesOrderDetailDto?> CompleteDraftSaleAsync(
        Guid id,
        CompleteDraftSaleRequest? request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (existing is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(existing.WarehouseId, cancellationToken);
        var completed = await _repository.CompleteDraftSaleAsync(id, request, DiscountPolicy, cancellationToken);
        if (!completed) return null;
        var order = await _repository.GetSalesOrderAsync(id, cancellationToken, freshSale: true);
        if (order is not null)
        {
            await _audit.WriteAsync(
                "sales_order",
                id,
                "complete",
                new { order.OrderNumber, order.TotalAmount, fromDraft = true, warehouseId = order.WarehouseId },
                cancellationToken);
            await WritePosDiscountAuditIfNeededAsync(order, cancellationToken);
        }
        return order;
    }

    public async Task<SalesOrderDetailDto?> CancelDraftSaleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var orderBefore = await _repository.GetSalesOrderAsync(id, cancellationToken);
        if (orderBefore is null) return null;
        await _branchAccess.EnsureWarehouseAccessAsync(orderBefore.WarehouseId, cancellationToken);
        var cancelled = await _repository.CancelDraftSaleAsync(id, cancellationToken);
        if (!cancelled) return null;
        await _audit.WriteAsync(
            "sales_order",
            id,
            "cancel",
            new { orderBefore.OrderNumber, warehouseId = orderBefore.WarehouseId },
            cancellationToken);
        return await _repository.GetSalesOrderAsync(id, cancellationToken);
    }

    public async Task<SalesReturnDetailDto> CreateSaleReturnAsync(
        Guid salesOrderId,
        CreateSaleReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetSalesOrderAsync(salesOrderId, cancellationToken)
            ?? throw new InvalidOperationException("Đơn bán không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(order.WarehouseId, cancellationToken);
        var returnId = await _repository.CreateSaleReturnAsync(salesOrderId, request, cancellationToken);
        var detail = (await _repository.GetSaleReturnAsync(returnId, cancellationToken))!;
        await _audit.WriteAsync(
            "sales_return",
            returnId,
            "complete",
            new
            {
                detail.ReturnNumber,
                detail.OrderNumber,
                detail.TotalRefund,
                salesOrderId,
                warehouseId = order.WarehouseId,
            },
            cancellationToken);
        return detail;
    }

    public async Task<SalesReturnDetailDto?> GetSaleReturnAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetSaleReturnAsync(id, cancellationToken);
        if (item is not null)
        {
            var order = await _repository.GetSalesOrderAsync(item.SalesOrderId, cancellationToken);
            if (order is not null)
                await _branchAccess.EnsureWarehouseAccessAsync(order.WarehouseId, cancellationToken);
        }
        return item;
    }

    public Task<IReadOnlyList<SalesReturnListItemDto>> GetSaleReturnsAsync(
        int limit = 50,
        string? search = null,
        string? customerSearch = null,
        string? documentSearch = null,
        CancellationToken cancellationToken = default) =>
        _repository.GetSaleReturnsAsync(limit, search, customerSearch, documentSearch, cancellationToken);

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

    public async Task<SalesShiftDetailDto?> GetOpenShiftAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(warehouseId, cancellationToken);
        return await _repository.GetOpenShiftAsync(warehouseId, cancellationToken);
    }

    public async Task<SalesShiftDetailDto?> GetShiftAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var shift = await _repository.GetShiftAsync(id, cancellationToken);
        if (shift is not null)
            await _branchAccess.EnsureWarehouseAccessAsync(shift.WarehouseId, cancellationToken);
        return shift;
    }

    public async Task<SalesShiftDetailDto> OpenShiftAsync(
        OpenSalesShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        await _branchAccess.EnsureWarehouseAccessAsync(request.WarehouseId, cancellationToken);
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
        var existing = await _repository.GetShiftAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Ca làm việc không tồn tại.");
        await _branchAccess.EnsureWarehouseAccessAsync(existing.WarehouseId, cancellationToken);
        var shift = await _repository.CloseShiftAsync(id, request, _tenant.UserId, cancellationToken);
        await _audit.WriteAsync(
            "sales_shift",
            id,
            "close",
            new { shift.ShiftNumber, request.ClosingCash, shift.CashVariance },
            cancellationToken);
        return shift;
    }

    public Task<SaleOrderPricingResult> PriceSaleLinesAsync(
        IReadOnlyList<CreateSaleLineRequest> items,
        short priceType,
        SaleDiscountInput? orderDiscount,
        SalesDiscountPolicy discountPolicy,
        CancellationToken cancellationToken = default) =>
        _repository.PriceSaleLinesAsync(items, priceType, orderDiscount, discountPolicy, cancellationToken);

    public Task ReportRxPosBlockAsync(
        ReportRxPosBlockRequest request,
        CancellationToken cancellationToken = default) =>
        _repository.ReportRxPosBlockAsync(request, _tenant.UserId, cancellationToken);

    /// <summary>AC1 — dedicated discount audit when POS order/line discount &gt; 0 (excludes loyalty/voucher fields).</summary>
    private async Task WritePosDiscountAuditIfNeededAsync(
        SalesOrderDetailDto order,
        CancellationToken cancellationToken)
    {
        var posDiscountTotal = order.DiscountAmount + order.LineDiscountTotal;
        if (posDiscountTotal <= 0) return;

        await _audit.WriteAsync(
            "sales_order",
            order.Id,
            "discount",
            new
            {
                order.OrderNumber,
                orderDiscountAmount = order.DiscountAmount,
                lineDiscountTotal = order.LineDiscountTotal,
                posDiscountTotal,
                warehouseId = order.WarehouseId,
            },
            cancellationToken);
    }
}
