namespace KitPlatform.Packs.Pharmacy.Procurement;

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
    short Status,
    bool IsPlaceholder,
    string? WholesaleFacilityCode);

public sealed record CreateSupplierRequest(
    string SupplierCode,
    string SupplierName,
    string? TaxCode,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    int PaymentTerms = 30,
    string? WholesaleFacilityCode = null);

public sealed record UpdateSupplierRequest(
    string SupplierName,
    string? TaxCode,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    int PaymentTerms,
    short Status,
    string? WholesaleFacilityCode = null);

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
    short TaxRatePercent,
    Guid VatTreatmentId,
    string VatTreatmentCode,
    string VatTreatmentName,
    bool VatIsNotSubject,
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
    IReadOnlyList<CreatePurchaseOrderItemRequest> Items,
    Guid VatTreatmentId);

public sealed record UpdatePurchaseOrderItemRequest(
    Guid? Id,
    Guid ProductId,
    Guid ProductUnitId,
    decimal OrderedQty,
    decimal UnitPrice);

public sealed record UpdatePurchaseOrderRequest(
    Guid? SupplierId,
    DateOnly? ExpectedDate,
    string? Notes,
    IReadOnlyList<UpdatePurchaseOrderItemRequest> Items,
    Guid VatTreatmentId);

public sealed record ApprovePurchaseOrderRequest(Guid? SupplierId);

public sealed record SubmitPurchaseOrderApprovalResult(Guid WorkflowTaskId);

public sealed record PoWorkflowDecisionDto(
    Guid TaskId,
    string TaskStatus,
    string Decision,
    DateTime? CompletedAt,
    Guid? PurchaseOrderId);

public sealed record ProcurementVatTreatmentDto(
    Guid Id,
    string TreatmentCode,
    string TreatmentName,
    decimal RatePercent,
    bool IsNotSubject,
    int SortOrder,
    bool IsActive,
    bool CanDelete);

public sealed record CreateProcurementVatTreatmentRequest(
    string TreatmentCode,
    string TreatmentName,
    decimal RatePercent,
    bool IsNotSubject,
    int SortOrder = 0);

public sealed record UpdateProcurementVatTreatmentRequest(
    string TreatmentName,
    decimal RatePercent,
    bool IsNotSubject,
    int SortOrder,
    bool IsActive);

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
    short? DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    decimal LineTotal,
    decimal InventoryUnitCost);

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
    string? SupplierInvoiceNumber,
    decimal SubtotalGross,
    decimal LineDiscountTotal,
    decimal MerchandiseNet,
    short? OrderDiscountType,
    decimal OrderDiscountValue,
    decimal OrderDiscountAmount,
    Guid? VatTreatmentId,
    string? VatTreatmentCode,
    string? VatTreatmentName,
    bool VatIsNotSubject,
    short TaxRatePercent,
    decimal TaxAmount,
    decimal TotalAmount,
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
    decimal UnitCost,
    short? DiscountType = null,
    decimal? DiscountValue = null);

public sealed record CreateGoodsReceiptRequest(
    Guid? PurchaseOrderId,
    Guid SupplierId,
    Guid WarehouseId,
    DateOnly? ReceiptDate,
    string? Notes,
    string? SupplierInvoiceNumber,
    Guid VatTreatmentId,
    IReadOnlyList<CreateGoodsReceiptItemRequest> Items,
    short? OrderDiscountType = null,
    decimal? OrderDiscountValue = null);

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
    bool IncludeArchived = false,
    int Page = 1,
    int PageSize = 50);

public sealed record GoodsReceiptListFilter(
    string? Search = null,
    Guid? SupplierId = null,
    Guid? WarehouseId = null,
    short? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    Guid? PurchaseOrderId = null,
    Guid? ProductId = null,
    bool IncludeArchived = false,
    int Page = 1,
    int PageSize = 50);

public sealed record ProcurementPagedListResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record SupplierPayablesAgingBucketsDto(
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90);

public sealed record SupplierPayablesRowDto(
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    int PaymentTerms,
    decimal TotalPayable,
    decimal UnappliedCredit,
    SupplierPayablesAgingBucketsDto Aging,
    int OpenDocumentCount);

public sealed record SupplierPayablesDetailLineDto(
    Guid GoodsReceiptId,
    string GrnNumber,
    DateTime ReceiptDate,
    decimal GrnTotal,
    decimal PaidAmount,
    decimal Outstanding,
    int DaysOutstanding);

public sealed record GrnPayableSourceRow(
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    int PaymentTerms,
    Guid GrnId,
    string GrnNumber,
    DateTime ReceiptDate,
    decimal GrnTotal,
    decimal GrnLinkedPaid);

public sealed record SupplierPayablesDetailDto(
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    int PaymentTerms,
    decimal TotalPayable,
    decimal UnappliedCredit,
    SupplierPayablesAgingBucketsDto Aging,
    IReadOnlyList<SupplierPayablesDetailLineDto> Lines);
