namespace KitPlatform.Packs.Pharmacy.Sales;

public sealed record CustomerListItemDto(
    Guid Id,
    string CustomerCode,
    string FullName,
    string Phone,
    string? Email,
    bool AllowCredit = false,
    decimal? CreditLimit = null,
    decimal CurrentOutstanding = 0,
    Guid? CustomerGroupId = null,
    string? CustomerGroupName = null,
    decimal GroupDiscountPercent = 0);

public sealed record PosBatchHintDto(
    Guid BatchId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal QuantityAvailable,
    bool IsSuggested);

public sealed record PosProductLookupDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    decimal ConversionFactor,
    decimal UnitPrice,
    decimal StockAvailable,
    string DispensingClass,
    IReadOnlyList<PosBatchHintDto>? BatchHints = null,
    string StockSourceLabel = StockSourceLabels.SystemBook);

public sealed record PosStockCheckDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    decimal ConversionFactor,
    decimal StockAvailable,
    string StockSourceLabel = StockSourceLabels.SystemBook);

public sealed record PosProductSearchItemDto(
    string ProductCode,
    string ProductName,
    string LookupCode,
    string UnitName,
    decimal UnitPrice,
    decimal StockAvailable);

public sealed record PosBulkStockRequest(
    Guid WarehouseId,
    IReadOnlyList<Guid> ProductUnitIds);

public sealed record PosAllocationPreviewLineRequest(
    Guid ProductId,
    Guid ProductUnitId,
    decimal Quantity);

public sealed record PosAllocationPreviewRequest(
    Guid WarehouseId,
    IReadOnlyList<PosAllocationPreviewLineRequest> Items);

public sealed record PosBatchAllocationPreviewDto(
    Guid BatchId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal Quantity,
    decimal BookQuantityAvailable);

public sealed record PosAllocationPreviewLineDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    decimal RequestedQuantity,
    IReadOnlyList<PosBatchAllocationPreviewDto> Allocations);

public sealed record PosAllocationPreviewDto(
    IReadOnlyList<PosAllocationPreviewLineDto> Lines,
    string StockSourceLabel);

public sealed record CreateSaleLineRequest(
    Guid ProductId,
    Guid ProductUnitId,
    decimal Quantity,
    short? DiscountType = null,
    decimal? DiscountValue = null,
    string? BatchNumber = null,
    Guid? PrescriptionLineId = null);

public sealed record CompleteDraftSaleRequest(
    IReadOnlyList<CreateSalePaymentRequest>? Payments = null,
    IReadOnlyList<CreateSaleLineRequest>? Items = null,
    Guid? CustomerId = null,
    short? OrderDiscountType = null,
    decimal? OrderDiscountValue = null,
    string? Notes = null,
    int? LoyaltyPointsToRedeem = null,
    decimal? LoyaltyDiscountAmount = null,
    Guid? CustomerVoucherId = null,
    string? OrderReminderLabel = null,
    int? OrderReminderDaysSupply = null,
    Guid? DiscountOverrideWorkflowTaskId = null,
    Guid? PrescriptionId = null,
    Guid? ConnectRxHandoffId = null);

public sealed record ReportRxPosBlockRequest(Guid ProductId, Guid WarehouseId);

public sealed record CreateSalePaymentRequest(
    short PaymentMethod,
    decimal Amount);

public sealed record CreateSaleRequest(
    Guid WarehouseId,
    Guid? CustomerId,
    short PriceType,
    IReadOnlyList<CreateSaleLineRequest> Items,
    IReadOnlyList<CreateSalePaymentRequest>? Payments = null,
    short? OrderDiscountType = null,
    decimal? OrderDiscountValue = null,
    string? Notes = null,
    bool SaveAsDraft = false,
    int? LoyaltyPointsToRedeem = null,
    decimal? LoyaltyDiscountAmount = null,
    Guid? CustomerVoucherId = null,
    string? OrderReminderLabel = null,
    int? OrderReminderDaysSupply = null,
    Guid? DiscountOverrideWorkflowTaskId = null,
    Guid? PrescriptionId = null,
    Guid? ConnectRxHandoffId = null);

public sealed record UpdateDraftSaleRequest(
    Guid? CustomerId,
    short PriceType,
    IReadOnlyList<CreateSaleLineRequest> Items,
    short? OrderDiscountType = null,
    decimal? OrderDiscountValue = null,
    string? Notes = null,
    Guid? DiscountOverrideWorkflowTaskId = null);

public sealed record CreateSaleReturnLineRequest(
    Guid SalesOrderItemId,
    decimal Quantity);

public sealed record CreateSaleReturnRequest(
    string? Reason,
    IReadOnlyList<CreateSaleReturnLineRequest> Items,
    IReadOnlyList<CreateSalePaymentRequest>? Payments = null);

public sealed record SalesReturnListItemDto(
    Guid Id,
    string ReturnNumber,
    Guid SalesOrderId,
    string OrderNumber,
    DateTime ReturnDate,
    short Status,
    decimal TotalRefund,
    Guid? SalesShiftId = null,
    string? ShiftNumber = null);

public sealed record SalesReturnItemDto(
    Guid Id,
    Guid SalesOrderItemId,
    string ProductCode,
    string ProductName,
    string BatchNumber,
    decimal Quantity,
    decimal RefundAmount);

public sealed record SalesReturnDetailDto(
    Guid Id,
    string ReturnNumber,
    Guid SalesOrderId,
    string OrderNumber,
    DateTime ReturnDate,
    short Status,
    string? Reason,
    decimal TotalRefund,
    IReadOnlyList<SalesReturnItemDto> Items,
    IReadOnlyList<SalesPaymentDto> Payments,
    Guid? SalesShiftId = null,
    string? ShiftNumber = null);

public sealed record ShiftLotComplianceAlertDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string SoldBatchNumber,
    DateOnly? SoldExpiryDate,
    string EarlierBatchNumber,
    DateOnly? EarlierExpiryDate,
    decimal EarlierBookQuantity,
    string StockSourceLabel);

public sealed record SalesShiftPaymentSummaryDto(
    short PaymentMethod,
    decimal SalesAmount,
    decimal RefundAmount,
    decimal NetAmount);

public sealed record SalesShiftSummaryDto(
    DateTime From,
    DateTime To,
    decimal TotalSales,
    decimal TotalRefunds,
    decimal NetTotal,
    IReadOnlyList<SalesShiftPaymentSummaryDto> ByMethod,
    decimal OpeningCash = 0,
    decimal CashSales = 0,
    decimal CashRefunds = 0,
    decimal ExpectedCash = 0,
    decimal? ClosingCash = null,
    decimal? CashVariance = null);

public sealed record SalesShiftListItemDto(
    Guid Id,
    string ShiftNumber,
    Guid WarehouseId,
    string WarehouseName,
    string OpenedByUserName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? CashVariance,
    short Status);

public sealed record SalesShiftDetailDto(
    Guid Id,
    string ShiftNumber,
    Guid WarehouseId,
    string WarehouseName,
    string OpenedByUserName,
    string? ClosedByUserName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? ExpectedCash,
    decimal? CashVariance,
    short Status,
    string? CloseNotes,
    SalesShiftSummaryDto Summary,
    IReadOnlyList<ShiftLotComplianceAlertDto> LotAlerts);

public sealed record OpenSalesShiftRequest(
    Guid WarehouseId,
    decimal OpeningCash);

public sealed record CloseSalesShiftRequest(
    decimal ClosingCash,
    string? CloseNotes = null);

public sealed record SalesOrderListFilter(
    string? Search = null,
    string? CustomerSearch = null,
    string? DocumentSearch = null,
    short? Status = null,
    int Page = 1,
    int PageSize = 50);

public sealed record SalesOrderPagedListResult(
    IReadOnlyList<SalesOrderListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record SalesOrderListItemDto(
    Guid Id,
    string OrderNumber,
    Guid WarehouseId,
    string WarehouseName,
    Guid? CustomerId,
    string? CustomerName,
    short Status,
    DateTime OrderDate,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding,
    int ItemCount,
    decimal TotalRefunded = 0,
    Guid? SalesShiftId = null,
    string? ShiftNumber = null);

public sealed record SalesOrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    Guid ProductUnitId,
    string UnitName,
    Guid? BatchId,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    short? DiscountType,
    decimal DiscountValue,
    decimal LineTotal,
    decimal ReturnedQuantity = 0);

public sealed record SalesPaymentDto(
    Guid Id,
    short PaymentMethod,
    decimal Amount,
    DateTime PaidAt);

public sealed record SalesRefundPaymentSummaryDto(
    short PaymentMethod,
    decimal Amount);

public sealed record SalesOrderDetailDto(
    Guid Id,
    string OrderNumber,
    Guid WarehouseId,
    string WarehouseName,
    Guid? CustomerId,
    string? CustomerName,
    short Status,
    DateTime OrderDate,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal LineDiscountTotal,
    short? OrderDiscountType,
    decimal OrderDiscountValue,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Outstanding,
    decimal TotalRefunded,
    string? Notes,
    IReadOnlyList<SalesOrderItemDto> Items,
    IReadOnlyList<SalesPaymentDto> Payments,
    IReadOnlyList<SalesRefundPaymentSummaryDto> RefundPayments,
    Guid? SalesShiftId = null,
    string? ShiftNumber = null,
    int? LoyaltyPointsEarned = null,
    decimal LoyaltyPointsRedeemed = 0,
    decimal LoyaltyDiscountAmount = 0,
    decimal VoucherDiscountAmount = 0,
    string? VoucherCode = null,
    string? VoucherName = null);

public sealed record CustomerReceivablesAgingBucketsDto(
    decimal Current,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90);

public sealed record CustomerReceivablesRowDto(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string? CustomerPhone,
    decimal TotalReceivable,
    decimal UnappliedCredit,
    CustomerReceivablesAgingBucketsDto Aging,
    int OpenDocumentCount);

public sealed record CustomerReceivablesDetailLineDto(
    Guid SalesOrderId,
    string OrderNumber,
    DateTime OrderDate,
    decimal OrderTotal,
    decimal PaidAmount,
    decimal Outstanding,
    int DaysOutstanding);

public sealed record SalesOrderReceivableSourceRow(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string? CustomerPhone,
    Guid SalesOrderId,
    string OrderNumber,
    DateTime OrderDate,
    decimal OrderTotal,
    decimal AmountPaid,
    decimal Outstanding);

public sealed record CustomerReceivablesDetailDto(
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string? CustomerPhone,
    decimal TotalReceivable,
    decimal UnappliedCredit,
    CustomerReceivablesAgingBucketsDto Aging,
    IReadOnlyList<CustomerReceivablesDetailLineDto> Lines);

public sealed record CustomerPaymentListItemDto(
    Guid Id,
    string PaymentNumber,
    Guid CustomerId,
    string CustomerName,
    decimal Amount,
    short PaymentMethod,
    short Status,
    DateTime PaymentDate,
    DateTime? PostedAt,
    Guid? SalesOrderId,
    string? OrderNumber,
    string? Notes);

public sealed record CreateCustomerPaymentRequest(
    Guid CustomerId,
    Guid? SalesOrderId,
    decimal Amount,
    short PaymentMethod,
    string? Notes,
    DateOnly? PaymentDate = null);

public sealed record UpdateCustomerPaymentRequest(
    Guid CustomerId,
    Guid? SalesOrderId,
    decimal Amount,
    short PaymentMethod,
    string? Notes,
    DateOnly? PaymentDate = null);

public sealed record CustomerPaymentListFilter(
    string? Search = null,
    string? CustomerSearch = null,
    string? DocumentSearch = null,
    Guid? CustomerId = null,
    short? Status = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null);

public sealed record SalesOrderPaymentLink(
    Guid Id,
    Guid CustomerId,
    Guid WarehouseId,
    short Status,
    decimal Outstanding);
