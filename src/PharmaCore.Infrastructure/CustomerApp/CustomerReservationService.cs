using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerReservationService : ICustomerReservationService
{
    private readonly CustomerReservationRepository _repo;
    private readonly IDbConnectionFactory _db;

    public CustomerReservationService(CustomerReservationRepository repo, IDbConnectionFactory db)
    {
        _repo = repo;
        _db = db;
    }

    public async Task<CustomerReservationListResult> ListForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var items = await _repo.ListForCustomerAsync(tenantId, customerId, cancellationToken);
        return new CustomerReservationListResult(items);
    }

    public Task<CustomerReservationDto?> GetForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        _repo.GetForCustomerAsync(tenantId, customerId, reservationId, cancellationToken);

    public async Task<CustomerReservationDto> CreateForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CreateCustomerReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        if (request.FulfillmentType == CustomerReservationFulfillmentTypes.Delivery)
        {
            if (request.AddressId is null)
                throw new InvalidOperationException("Chọn địa chỉ giao hàng.");
            if (!await _repo.AddressBelongsToCustomerAsync(tenantId, customerId, request.AddressId.Value, conn, tx, cancellationToken))
                throw new InvalidOperationException("Địa chỉ giao hàng không hợp lệ.");
        }

        var resolvedItems = new List<(CustomerReservationLineRequest Request, ResolvedReservationProduct Product)>();
        foreach (var line in request.Items)
        {
            var product = await _repo.ResolveProductAsync(tenantId, line.ProductId, conn, tx, cancellationToken)
                ?? throw new InvalidOperationException($"Không tìm thấy sản phẩm {line.ProductId}.");
            resolvedItems.Add((line, product));
        }

        var reservationNumber = await _repo.NextReservationNumberAsync(tenantId, conn, tx, cancellationToken);
        var reservationId = await _repo.InsertReservationAsync(
            tenantId,
            customerId,
            reservationNumber,
            request.FulfillmentType,
            request.FulfillmentType == CustomerReservationFulfillmentTypes.Delivery ? request.AddressId : null,
            request.Notes?.Trim(),
            conn,
            tx,
            cancellationToken);
        await _repo.InsertItemsAsync(reservationId, resolvedItems, conn, tx, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await GetForCustomerAsync(tenantId, customerId, reservationId, cancellationToken))!;
    }

    public async Task<CustomerReservationDto> CancelForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var status = await _repo.GetStatusAsync(tenantId, reservationId, customerId, conn, tx, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu đặt trước.");
        if (status != CustomerReservationStatuses.Pending)
            throw new InvalidOperationException("Chỉ hủy được yêu cầu đang chờ xác nhận.");

        await _repo.UpdateStatusAsync(tenantId, reservationId, CustomerReservationStatuses.Cancelled, conn, tx, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await GetForCustomerAsync(tenantId, customerId, reservationId, cancellationToken))!;
    }

    public async Task<CustomerReservationStaffListResult> ListForStaffAsync(
        Guid tenantId,
        short[]? statuses,
        CancellationToken cancellationToken = default)
    {
        var items = await _repo.ListForStaffAsync(tenantId, statuses, cancellationToken);
        return new CustomerReservationStaffListResult(items);
    }

    public Task<CustomerReservationDto?> GetForStaffAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        _repo.GetForStaffAsync(tenantId, reservationId, cancellationToken);

    public async Task<CustomerReservationDto> ConfirmAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await TransitionAsync(
            tenantId,
            reservationId,
            CustomerReservationStatuses.Pending,
            CustomerReservationStatuses.Confirmed,
            "Chỉ xác nhận được yêu cầu đang chờ.",
            cancellationToken);

    public async Task<CustomerReservationDto> RejectAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await TransitionAsync(
            tenantId,
            reservationId,
            CustomerReservationStatuses.Pending,
            CustomerReservationStatuses.Rejected,
            "Chỉ từ chối được yêu cầu đang chờ.",
            cancellationToken);

    public async Task<CustomerReservationDto> MarkReadyAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await TransitionAsync(
            tenantId,
            reservationId,
            CustomerReservationStatuses.Confirmed,
            CustomerReservationStatuses.Ready,
            "Chỉ đánh dấu sẵn sàng khi đơn đã xác nhận.",
            cancellationToken);

    public async Task<CustomerReservationDto> MarkCollectedAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        await TransitionAsync(
            tenantId,
            reservationId,
            CustomerReservationStatuses.Ready,
            CustomerReservationStatuses.Collected,
            "Chỉ đánh dấu đã lấy khi thuốc sẵn sàng.",
            cancellationToken);

    public async Task<CustomerReservationDto> UpdateStaffNotesAsync(
        Guid tenantId,
        Guid reservationId,
        UpdateCustomerReservationStaffNotesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repo.GetForStaffAsync(tenantId, reservationId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy yêu cầu đặt trước.");
        await _repo.UpdateStaffNotesAsync(tenantId, reservationId, request.StaffNotes?.Trim(), cancellationToken);
        return (await GetForStaffAsync(tenantId, reservationId, cancellationToken))!;
    }

    private async Task<CustomerReservationDto> TransitionAsync(
        Guid tenantId,
        Guid reservationId,
        short expectedStatus,
        short nextStatus,
        string invalidMessage,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var status = await _repo.GetStatusAsync(tenantId, reservationId, customerId: null, conn, tx, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy yêu cầu đặt trước.");
        if (status != expectedStatus)
            throw new InvalidOperationException(invalidMessage);

        await _repo.UpdateStatusAsync(tenantId, reservationId, nextStatus, conn, tx, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await GetForStaffAsync(tenantId, reservationId, cancellationToken))!;
    }

    public async Task<CustomerReservationPosLoadDto?> GetPosLoadAsync(
        Guid tenantId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var header = await _repo.GetHeaderAsync(tenantId, reservationId, cancellationToken);
        if (header is null
            || header.SalesOrderId is not null
            || !IsPosLoadableStatus(header.Status))
            return null;

        var warehouseId = await _repo.ResolveDefaultWarehouseIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kho bán mặc định.");

        var lineRows = await _repo.ListItemsAsync(reservationId, cancellationToken);

        return new CustomerReservationPosLoadDto(
            header.Id,
            header.ReservationNumber,
            header.CustomerId,
            warehouseId,
            header.Notes,
            lineRows.Select(item => new CustomerReservationPosLineDto(
                item.ProductId,
                item.ProductCode,
                item.ProductName,
                item.ProductUnitId,
                item.UnitName,
                item.Quantity,
                item.CustomerNote)).ToList());
    }

    public async Task LinkSalesOrderAsync(
        Guid tenantId,
        Guid reservationId,
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!await _repo.LinkSalesOrderAsync(tenantId, reservationId, salesOrderId, cancellationToken))
            throw new InvalidOperationException(
                "Không liên kết được đặt trước với đơn bán. Kiểm tra trạng thái và khách hàng trên hóa đơn.");
    }

    private static bool IsPosLoadableStatus(short status) =>
        status is CustomerReservationStatuses.Confirmed or CustomerReservationStatuses.Ready
        || status is CustomerReservationStatuses.Collected;

    private static void ValidateCreateRequest(CreateCustomerReservationRequest request)
    {
        if (request.FulfillmentType != CustomerReservationFulfillmentTypes.Pickup
            && request.FulfillmentType != CustomerReservationFulfillmentTypes.Delivery)
            throw new InvalidOperationException("Chọn hình thức nhận thuốc.");

        if (request.Items is not { Count: > 0 })
            throw new InvalidOperationException("Thêm ít nhất một sản phẩm.");

        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                throw new InvalidOperationException("Số lượng phải lớn hơn 0.");
        }
    }
}
