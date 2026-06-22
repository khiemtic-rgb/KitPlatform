using PharmaCore.Application.Abstractions;

using PharmaCore.Application.Procurement;



namespace PharmaCore.Infrastructure.Procurement;



internal sealed class GoodsReceiptService : IGoodsReceiptService

{

    private readonly ProcurementRepository _repository;

    private readonly ITenantContext _tenant;

    private readonly IAuditLogService _audit;



    public GoodsReceiptService(

        ProcurementRepository repository,

        ITenantContext tenant,

        IAuditLogService audit)

    {

        _repository = repository;

        _tenant = tenant;

        _audit = audit;

    }



    public Task<IReadOnlyList<GoodsReceiptListItemDto>> GetAllAsync(

        GoodsReceiptListFilter? filter = null,

        CancellationToken cancellationToken = default) =>

        _repository.GetGoodsReceiptsAsync(filter ?? new GoodsReceiptListFilter(), cancellationToken);



    public Task<GoodsReceiptDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>

        _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);



    public async Task<GoodsReceiptDetailDto> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken cancellationToken = default)

    {

        if (request.Items.Count == 0)

            throw new InvalidOperationException("Thêm ít nhất một dòng nhập.");



        if (!await _repository.SupplierExistsAsync(request.SupplierId, cancellationToken))

            throw new InvalidOperationException("NCC không tồn tại.");

        if (!await _repository.WarehouseExistsAsync(request.WarehouseId, cancellationToken))

            throw new InvalidOperationException("Kho không tồn tại.");



        foreach (var item in request.Items)

        {

            if (string.IsNullOrWhiteSpace(item.BatchNumber))

                throw new InvalidOperationException("Số lô không được để trống.");

            if (item.Quantity <= 0)

                throw new InvalidOperationException("Số lượng nhập phải lớn hơn 0.");

            if (item.UnitCost < 0)

                throw new InvalidOperationException("Giá vốn không hợp lệ.");

            if (!await _repository.ProductExistsAsync(item.ProductId, cancellationToken))

                throw new InvalidOperationException($"Sản phẩm không tồn tại: {item.ProductId}");

        }



        var id = await _repository.CreateGoodsReceiptAsync(request, _tenant.UserId, cancellationToken);

        return (await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken))!;

    }



    public async Task<GoodsReceiptDetailDto?> CompleteAsync(Guid id, CancellationToken cancellationToken = default)

    {

        await _repository.CompleteGoodsReceiptAsync(id, _tenant.UserId, cancellationToken);

        await _audit.WriteAsync("goods_receipt", id, "complete", cancellationToken: cancellationToken);

        return await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);

    }



    public async Task<GoodsReceiptDetailDto?> CancelAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var grn = await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);

        await _repository.CancelGoodsReceiptAsync(id, _tenant.UserId, cancellationToken);

        if (grn is not null)

            await _audit.WriteAsync("goods_receipt", id, "cancel", new { grnNumber = grn.GrnNumber }, cancellationToken);

        return await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);

    }



    public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var grn = await _repository.GetGoodsReceiptAsync(id, cancellationToken: cancellationToken);

        if (grn is null) return false;

        if (grn.Status != GoodsReceiptStatuses.Cancelled)

            throw new InvalidOperationException("Chỉ ẩn được phiếu nhập đã hủy.");

        if (grn.DeletedAt is not null)

            throw new InvalidOperationException("Phiếu nhập đã được ẩn.");



        var archived = await _repository.SoftDeleteGoodsReceiptAsync(id, _tenant.UserId, cancellationToken);

        if (archived)

        {

            await _audit.WriteAsync(

                "goods_receipt",

                id,

                "soft_delete",

                new { grnNumber = grn.GrnNumber, status = grn.Status },

                cancellationToken);

        }



        return archived;

    }



    public async Task<bool> PurgeAsync(Guid id, CancellationToken cancellationToken = default)

    {

        var grn = await _repository.GetGoodsReceiptAsync(id, includeArchived: true, cancellationToken);

        if (grn is null || grn.DeletedAt is null) return false;



        var purged = await _repository.PurgeGoodsReceiptAsync(id, cancellationToken);

        if (purged)

        {

            await _audit.WriteAsync(

                "goods_receipt",

                id,

                "purge",

                new { grnNumber = grn.GrnNumber, status = grn.Status, deletedAt = grn.DeletedAt },

                cancellationToken);

        }



        return purged;

    }

}

