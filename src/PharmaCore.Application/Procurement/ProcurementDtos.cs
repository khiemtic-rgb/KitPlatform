namespace PharmaCore.Application.Procurement;

public sealed record SupplierDto(
    Guid Id,
    string SupplierCode,
    string SupplierName,
    string? TaxCode,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    int PaymentTerms,
    short Status);

public sealed record CreateSupplierRequest(
    string SupplierCode,
    string SupplierName,
    string? TaxCode,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    int PaymentTerms = 30);

public sealed record UpdateSupplierRequest(
    string SupplierName,
    string? TaxCode,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    int PaymentTerms,
    short Status);

public sealed record PurchaseOrderListItemDto(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    short Status,
    DateTime OrderDate,
    decimal TotalAmount,
    int ItemCount,
    DateTime? DeletedAt = null);

public sealed record PurchaseOrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    decimal OrderedQty,
    decimal ReceivedQty,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PurchaseOrderDetailDto(
    Guid Id,
    string PoNumber,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    short Status,
    DateTime OrderDate,
    DateOnly? ExpectedDate,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<PurchaseOrderItemDto> Items,
    DateTime? DeletedAt = null);

public sealed record CreatePurchaseOrderItemRequest(
    Guid ProductId,
    Guid ProductUnitId,
    decimal OrderedQty,
    decimal UnitPrice);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly? ExpectedDate,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderItemRequest> Items);

public sealed record UpdatePurchaseOrderItemRequest(
    Guid? Id,
    Guid ProductId,
    Guid ProductUnitId,
    decimal OrderedQty,
    decimal UnitPrice);

public sealed record UpdatePurchaseOrderRequest(
    DateOnly? ExpectedDate,
    string? Notes,
    IReadOnlyList<UpdatePurchaseOrderItemRequest> Items);

public sealed record GoodsReceiptListItemDto(
    Guid Id,
    string GrnNumber,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    Guid? PurchaseOrderId,
    string? PoNumber,
    short Status,
    DateTime ReceiptDate,
    int ItemCount,
    DateTime? DeletedAt = null);

public sealed record GoodsReceiptItemDto(
    Guid Id,
    Guid? PurchaseOrderItemId,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    string BatchNumber,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record GoodsReceiptDetailDto(
    Guid Id,
    string GrnNumber,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    Guid? PurchaseOrderId,
    string? PoNumber,
    short Status,
    DateTime ReceiptDate,
    string? Notes,
    IReadOnlyList<GoodsReceiptItemDto> Items,
    DateTime? DeletedAt = null);

public sealed record CreateGoodsReceiptItemRequest(
    Guid? PurchaseOrderItemId,
    Guid ProductId,
    Guid ProductUnitId,
    string BatchNumber,
    DateOnly? ManufactureDate,
    DateOnly ExpiryDate,
    decimal Quantity,
    decimal UnitCost);

public sealed record CreateGoodsReceiptRequest(
    Guid? PurchaseOrderId,
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly? ReceiptDate,
    string? Notes,
    IReadOnlyList<CreateGoodsReceiptItemRequest> Items);

public sealed record SupplierPaymentListItemDto(
    Guid Id,
    string PaymentNumber,
    Guid SupplierId,
    string SupplierName,
    decimal Amount,
    short PaymentMethod,
    short Status,
    DateTime PaymentDate,
    DateTime? PostedAt,
    Guid? PurchaseOrderId,
    string? PoNumber,
    Guid? GoodsReceiptId,
    string? GrnNumber,
    string? Notes);

public sealed record CreateSupplierPaymentRequest(
    Guid SupplierId,
    Guid? PurchaseOrderId,
    Guid? GoodsReceiptId,
    decimal Amount,
    short PaymentMethod,
    string? Notes,
    DateOnly? PaymentDate = null);

public sealed record UpdateSupplierPaymentRequest(
    Guid SupplierId,
    Guid? PurchaseOrderId,
    Guid? GoodsReceiptId,
    decimal Amount,
    short PaymentMethod,
    string? Notes,
    DateOnly? PaymentDate = null);

public sealed record SupplierPaymentListFilter(
    string? Search = null,
    Guid? SupplierId = null,
    short? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

public sealed record LastPurchasePriceHintDto(
    decimal? UnitPrice,
    DateTime? PriceDate,
    string? Source,
    string? DocumentNumber);

public sealed record PurchaseOrderListFilter(
    string? Search = null,
    Guid? SupplierId = null,
    Guid? WarehouseId = null,
    short? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    Guid? ProductId = null,
    bool PendingReceiptOnly = false,
    bool IncludeArchived = false);

public sealed record GoodsReceiptListFilter(
    string? Search = null,
    Guid? SupplierId = null,
    Guid? WarehouseId = null,
    short? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    Guid? PurchaseOrderId = null,
    Guid? ProductId = null,
    bool IncludeArchived = false);
