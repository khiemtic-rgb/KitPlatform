using PharmaCore.Application.Abstractions;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Application.Sales;
using PharmaCore.Infrastructure.Data;
using PharmaCore.Infrastructure.Sales;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerDraftOrderService : ICustomerDraftOrderService
{
    private static readonly short[] CustomerVisibleStatuses =
    [
        CustomerDraftOrderStatuses.Sent,
        CustomerDraftOrderStatuses.Confirmed,
        CustomerDraftOrderStatuses.Completed,
        CustomerDraftOrderStatuses.Expired,
        CustomerDraftOrderStatuses.Cancelled,
    ];

    private static readonly short[] PosLoadableStatuses =
    [
        CustomerDraftOrderStatuses.Sent,
        CustomerDraftOrderStatuses.Confirmed,
    ];

    private readonly CustomerDraftOrderRepository _repo;
    private readonly SalesRepository _sales;
    private readonly ICustomerPushService _push;
    private readonly IDraftOrderEventHub _events;
    private readonly ICurrentUserAccessor _user;
    private readonly IDbConnectionFactory _db;

    public CustomerDraftOrderService(
        CustomerDraftOrderRepository repo,
        SalesRepository sales,
        ICustomerPushService push,
        IDraftOrderEventHub events,
        ICurrentUserAccessor user,
        IDbConnectionFactory db)
    {
        _repo = repo;
        _sales = sales;
        _push = push;
        _events = events;
        _user = user;
        _db = db;
    }

    public Task<CustomerDraftOrderListResult> ListForStaffAsync(
        Guid tenantId,
        Guid? customerId,
        short[]? statuses,
        CancellationToken cancellationToken = default) =>
        MapListAsync(tenantId, customerId, statuses, excludeHiddenByCustomer: false, cancellationToken);

    public async Task<CustomerDraftOrderDto?> GetForStaffAsync(
        Guid tenantId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        var header = await _repo.GetHeaderAsync(tenantId, draftOrderId, cancellationToken);
        return header is null ? null : await MapDetailAsync(header, cancellationToken);
    }

    public async Task<CustomerDraftOrderDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        UpsertCustomerDraftOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var warehouseId = await ResolveWarehouseIdAsync(tenantId, request.WarehouseId, cancellationToken);
        var chatThreadId = request.ChatThreadId ?? await _repo.ResolveChatThreadIdAsync(tenantId, request.CustomerId, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var pricing = await PriceAsync(request, cancellationToken);
        var pricedLines = await BuildPricedLinesAsync(tenantId, request.Items, pricing, conn, tx, cancellationToken);
        var draftNumber = await _repo.NextDraftNumberAsync(tenantId, conn, tx, cancellationToken);
        var draftId = await _repo.InsertDraftAsync(
            tenantId,
            userId,
            draftNumber,
            request with { ChatThreadId = chatThreadId },
            warehouseId,
            CustomerDraftOrderStatuses.Draft,
            pricing.SubtotalGross,
            pricing.OrderDiscountAmount,
            pricing.TotalAmount,
            expiresAt: null,
            conn,
            tx,
            cancellationToken);
        await _repo.InsertItemsAsync(draftId, pricedLines, conn, tx, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await GetForStaffAsync(tenantId, draftId, cancellationToken))!;
    }

    public async Task<CustomerDraftOrderDto> UpdateAsync(
        Guid tenantId,
        Guid userId,
        Guid draftOrderId,
        UpsertCustomerDraftOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var existing = await _repo.GetHeaderAsync(tenantId, draftOrderId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy đơn tạm.");
        if (existing.Status != CustomerDraftOrderStatuses.Draft)
            throw new InvalidOperationException("Chỉ sửa được đơn tạm ở trạng thái nháp.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var pricing = await PriceAsync(request, cancellationToken);
        var pricedLines = await BuildPricedLinesAsync(tenantId, request.Items, pricing, conn, tx, cancellationToken);
        await _repo.ReplaceDraftAsync(
            tenantId,
            draftOrderId,
            request,
            pricing.SubtotalGross,
            pricing.OrderDiscountAmount,
            pricing.TotalAmount,
            conn,
            tx,
            cancellationToken);
        await _repo.InsertItemsAsync(draftOrderId, pricedLines, conn, tx, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await GetForStaffAsync(tenantId, draftOrderId, cancellationToken))!;
    }

    public async Task<CustomerDraftOrderDto> SendAsync(
        Guid tenantId,
        Guid userId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        var header = await _repo.GetHeaderAsync(tenantId, draftOrderId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy đơn tạm.");
        if (header.Status != CustomerDraftOrderStatuses.Draft)
            throw new InvalidOperationException("Chỉ gửi được đơn tạm ở trạng thái nháp.");

        var expiresAt = DateTimeOffset.UtcNow.AddHours(48);
        if (!await _repo.MarkSentAsync(tenantId, draftOrderId, expiresAt, cancellationToken))
            throw new InvalidOperationException("Không gửi được đơn tạm.");

        await _push.SendDraftOrderPushAsync(
            tenantId,
            header.CustomerId,
            header.DraftNumber,
            header.TotalAmount,
            cancellationToken);

        _events.NotifySent(tenantId, header.CustomerId, draftOrderId, header.DraftNumber);

        return (await GetForStaffAsync(tenantId, draftOrderId, cancellationToken))!;
    }

    public async Task<CustomerDraftOrderDto> CancelAsync(
        Guid tenantId,
        Guid userId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.MarkCancelledAsync(tenantId, draftOrderId, cancellationToken))
            throw new InvalidOperationException("Không hủy được đơn tạm.");
        return (await GetForStaffAsync(tenantId, draftOrderId, cancellationToken))!;
    }

    public async Task<CustomerDraftOrderPosLoadDto?> GetPosLoadAsync(
        Guid tenantId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        var header = await _repo.GetHeaderAsync(tenantId, draftOrderId, cancellationToken);
        if (header is null || !PosLoadableStatuses.Contains(header.Status))
            return null;

        var items = await _repo.ListItemsAsync(draftOrderId, cancellationToken);
        return new CustomerDraftOrderPosLoadDto(
            header.Id,
            header.DraftNumber,
            header.CustomerId,
            header.WarehouseId,
            header.OrderDiscountType,
            header.OrderDiscountValue,
            header.Notes,
            items.Select(item => new CustomerDraftOrderPosLineDto(
                item.ProductId,
                item.ProductCode,
                item.ProductName,
                item.ProductUnitId,
                item.UnitName,
                item.Quantity,
                item.UnitPrice,
                item.LineDiscountType,
                item.LineDiscountValue,
                item.DosageNote)).ToList());
    }

    public async Task LinkSalesOrderAsync(
        Guid tenantId,
        Guid draftOrderId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.MarkCompletedAsync(tenantId, draftOrderId, salesOrderId, cancellationToken))
            throw new InvalidOperationException("Không liên kết được đơn tạm với đơn bán.");
    }

    public Task<CustomerDraftOrderListResult> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        MapListAsync(tenantId, customerId, CustomerVisibleStatuses, excludeHiddenByCustomer: true, cancellationToken);

    public async Task<CustomerDraftOrderDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        await _repo.ExpireStaleAsync(tenantId, customerId, cancellationToken);
        var header = await _repo.GetHeaderAsync(tenantId, draftOrderId, cancellationToken);
        if (header is null || header.CustomerId != customerId || !CustomerVisibleStatuses.Contains(header.Status))
            return null;
        if (header.HiddenByCustomerAt is not null)
            return null;

        return await MapDetailAsync(header, cancellationToken);
    }

    public async Task<CustomerDraftOrderDto> ConfirmAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.MarkConfirmedAsync(tenantId, customerId, draftOrderId, cancellationToken))
            throw new InvalidOperationException("Không xác nhận được đơn tạm (có thể đã hết hạn hoặc đã xử lý).");

        var confirmed = (await GetForCustomerAsync(tenantId, customerId, draftOrderId, cancellationToken))!;
        _events.NotifyConfirmed(tenantId, customerId, draftOrderId, confirmed.DraftNumber);
        return confirmed;
    }

    public async Task HideForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.MarkHiddenByCustomerAsync(tenantId, customerId, draftOrderId, cancellationToken))
            throw new InvalidOperationException("Không ẩn được đơn hàng.");
    }

    public async Task<CustomerDraftOrderDto> CancelForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid draftOrderId,
        CancellationToken cancellationToken = default)
    {
        var header = await _repo.GetHeaderAsync(tenantId, draftOrderId, cancellationToken);
        if (header is null || header.CustomerId != customerId)
            throw new InvalidOperationException("Không tìm thấy đơn hàng.");

        if (!await _repo.MarkCancelledByCustomerAsync(tenantId, customerId, draftOrderId, cancellationToken))
            throw new InvalidOperationException("Không hủy được đơn hàng (có thể đã hết hạn hoặc đã xử lý).");

        var cancelled = (await GetForCustomerAsync(tenantId, customerId, draftOrderId, cancellationToken))!;
        _events.NotifyCancelled(tenantId, customerId, draftOrderId, cancelled.DraftNumber);
        return cancelled;
    }

    private async Task<CustomerDraftOrderListResult> MapListAsync(
        Guid tenantId,
        Guid? customerId,
        short[]? statuses,
        bool excludeHiddenByCustomer,
        CancellationToken cancellationToken)
    {
        var rows = await _repo.ListAsync(tenantId, customerId, statuses, excludeHiddenByCustomer, cancellationToken);
        var items = rows.Select(row => new CustomerDraftOrderListItemDto(
            row.Id,
            row.DraftNumber,
            row.CustomerId,
            row.CustomerName,
            row.Status,
            row.TotalAmount,
            row.ItemCount,
            ToOffset(row.SentAt),
            ToOffset(row.ConfirmedAt),
            ToOffset(row.ExpiresAt),
            ToOffset(row.HiddenByCustomerAt))).ToList();
        return new CustomerDraftOrderListResult(items);
    }

    private async Task<CustomerDraftOrderDto> MapDetailAsync(
        DraftOrderHeaderRow header,
        CancellationToken cancellationToken)
    {
        var items = await _repo.ListItemsAsync(header.Id, cancellationToken);
        return new CustomerDraftOrderDto(
            header.Id,
            header.DraftNumber,
            header.CustomerId,
            header.CustomerName,
            header.CustomerPhone,
            header.ChatThreadId,
            header.WarehouseId,
            header.Status,
            header.Subtotal,
            header.DiscountAmount,
            header.TotalAmount,
            header.OrderDiscountType,
            header.OrderDiscountValue,
            header.Notes,
            ToOffset(header.SentAt),
            ToOffset(header.ConfirmedAt),
            ToOffset(header.CompletedAt),
            ToOffset(header.ExpiresAt),
            header.SalesOrderId,
            header.SalesOrderNumber,
            items.Select(item => new CustomerDraftOrderLineDto(
                item.Id,
                item.LineNumber,
                item.ProductId,
                item.ProductUnitId,
                item.ProductCode,
                item.ProductName,
                item.UnitName,
                item.Quantity,
                item.UnitPrice,
                item.LineDiscountType,
                item.LineDiscountValue,
                item.LineAmount,
                item.DosageNote)).ToList());
    }

    private async Task<SaleOrderPricingResult> PriceAsync(
        UpsertCustomerDraftOrderRequest request,
        CancellationToken cancellationToken)
    {
        var lineRequests = request.Items.Select(item => new CreateSaleLineRequest(
            item.ProductId,
            item.ProductUnitId,
            item.Quantity,
            item.DiscountType,
            item.DiscountValue)).ToList();
        var orderDiscount = new SaleDiscountInput(request.OrderDiscountType, request.OrderDiscountValue);
        return await _sales.PriceSaleLinesAsync(
            lineRequests,
            request.PriceType,
            orderDiscount,
            DiscountPolicy,
            cancellationToken);
    }

    private async Task<List<(CustomerDraftOrderLineRequest Request, PricedDraftLine Line)>> BuildPricedLinesAsync(
        Guid tenantId,
        IReadOnlyList<CustomerDraftOrderLineRequest> requests,
        SaleOrderPricingResult pricing,
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var meta = await _repo.LoadProductMetaAsync(tenantId, requests, conn, tx, cancellationToken);
        var result = new List<(CustomerDraftOrderLineRequest, PricedDraftLine)>();
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var priced = pricing.Lines[i];
            var product = meta[i];
            result.Add((request, new PricedDraftLine(
                request.ProductId,
                request.ProductUnitId,
                product.ProductCode,
                product.ProductName,
                product.UnitName,
                priced.UnitPrice,
                priced.LineTotal)));
        }

        return result;
    }

    private async Task<Guid> ResolveWarehouseIdAsync(
        Guid tenantId,
        Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        if (warehouseId is Guid id)
            return id;

        return await _repo.ResolveDefaultWarehouseIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Chưa cấu hình kho mặc định.");
    }

    private SalesDiscountPolicy DiscountPolicy =>
        SalesDiscountPolicy.FromPermissions(_user.Permissions, _user.IsInRole("ADMIN"));

    private static void ValidateRequest(UpsertCustomerDraftOrderRequest request)
    {
        if (request.Items.Count == 0)
            throw new InvalidOperationException("Thêm ít nhất một sản phẩm.");
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;
}
