import { http } from '@/shared/api/http';
import type {
  CustomerListItem,
  PosAllocationPreview,
  PosBatchHint,
  PosProductLookup,
  PosProductSearchItem,
  ReceiptStoreSettings,
  SalesOrderDetail,
  SalesOrderListItem,
  SalesReturnDetail,
  SalesReturnListItem,
  SalesShiftDetail,
  SalesShiftListItem,
  SalesShiftSummary,
  SalesPaymentLine,
} from '@/shared/api/sales.types';

function normalizeSalesOrderListItem(row: Record<string, unknown>): SalesOrderListItem {
  return {
    id: String(row.id ?? row.Id),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    customerId: (row.customerId ?? row.CustomerId) as string | undefined,
    customerName: (row.customerName ?? row.CustomerName) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    totalRefunded: Number(row.totalRefunded ?? row.TotalRefunded ?? 0),
    salesShiftId: optionalGuid(row.salesShiftId ?? row.SalesShiftId),
    shiftNumber: (row.shiftNumber ?? row.ShiftNumber) as string | undefined,
  };
}

function normalizeSalesPaymentLine(row: Record<string, unknown>): SalesPaymentLine {
  return {
    id: (row.id ?? row.Id) as string | undefined,
    paymentMethod: Number(row.paymentMethod ?? row.PaymentMethod ?? 1),
    amount: Number(row.amount ?? row.Amount ?? 0),
    paidAt: (row.paidAt ?? row.PaidAt) as string | undefined,
  };
}

function normalizeSalesReturnListItem(row: Record<string, unknown>): SalesReturnListItem {
  return {
    id: String(row.id ?? row.Id),
    returnNumber: String(row.returnNumber ?? row.ReturnNumber ?? ''),
    salesOrderId: String(row.salesOrderId ?? row.SalesOrderId),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    returnDate: String(row.returnDate ?? row.ReturnDate ?? ''),
    status: Number(row.status ?? row.Status ?? 2),
    totalRefund: Number(row.totalRefund ?? row.TotalRefund ?? 0),
    salesShiftId: optionalGuid(row.salesShiftId ?? row.SalesShiftId),
    shiftNumber: (row.shiftNumber ?? row.ShiftNumber) as string | undefined,
  };
}

function normalizeSalesReturnDetail(
  data: Record<string, unknown>,
  rawItems: Record<string, unknown>[],
): SalesReturnDetail {
  const rawPayments = (data.payments ?? data.Payments ?? []) as Record<string, unknown>[];
  return {
    id: String(data.id ?? data.Id),
    returnNumber: String(data.returnNumber ?? data.ReturnNumber ?? ''),
    salesOrderId: String(data.salesOrderId ?? data.SalesOrderId),
    orderNumber: String(data.orderNumber ?? data.OrderNumber ?? ''),
    returnDate: String(data.returnDate ?? data.ReturnDate ?? ''),
    status: Number(data.status ?? data.Status ?? 2),
    reason: (data.reason ?? data.Reason) as string | undefined,
    totalRefund: Number(data.totalRefund ?? data.TotalRefund ?? 0),
    items: rawItems.map((row) => ({
      id: String(row.id ?? row.Id),
      salesOrderItemId: String(row.salesOrderItemId ?? row.SalesOrderItemId),
      productCode: String(row.productCode ?? row.ProductCode ?? ''),
      productName: String(row.productName ?? row.ProductName ?? ''),
      batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
      quantity: Number(row.quantity ?? row.Quantity ?? 0),
      refundAmount: Number(row.refundAmount ?? row.RefundAmount ?? 0),
    })),
    payments: rawPayments.map(normalizeSalesPaymentLine),
    salesShiftId: optionalGuid(data.salesShiftId ?? data.SalesShiftId),
    shiftNumber: (data.shiftNumber ?? data.ShiftNumber) as string | undefined,
  };
}

function optionalGuid(value: unknown): string | undefined {
  if (value == null || value === '') return undefined;
  const text = String(value);
  if (text === '00000000-0000-0000-0000-000000000000') return undefined;
  return text;
}

function normalizeSalesOrderItem(row: Record<string, unknown>) {
  return {
    id: String(row.id ?? row.Id),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    productUnitId: String(row.productUnitId ?? row.ProductUnitId),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    batchId: optionalGuid(row.batchId ?? row.BatchId),
    batchNumber: (row.batchNumber ?? row.BatchNumber) as string | undefined,
    expiryDate: (row.expiryDate ?? row.ExpiryDate) as string | undefined,
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
    discountType: (row.discountType ?? row.DiscountType) as number | undefined,
    discountValue: Number(row.discountValue ?? row.DiscountValue ?? 0),
    lineTotal: Number(row.lineTotal ?? row.LineTotal ?? 0),
    returnedQuantity: Number(row.returnedQuantity ?? row.ReturnedQuantity ?? 0),
  };
}

function normalizeSalesOrderDetail(
  data: Record<string, unknown>,
  rawItems: Record<string, unknown>[],
): SalesOrderDetail {
  const items = rawItems.map(normalizeSalesOrderItem);
  const lineDiscountTotal =
    Number(data.lineDiscountTotal ?? data.LineDiscountTotal ?? 0) ||
    items.reduce((sum, line) => sum + (line.discountAmount ?? 0), 0);

  return {
    ...normalizeSalesOrderListItem(data),
    subtotal: Number(data.subtotal ?? data.Subtotal ?? 0),
    discountAmount: Number(data.discountAmount ?? data.DiscountAmount ?? 0),
    lineDiscountTotal,
    orderDiscountType: (data.orderDiscountType ?? data.OrderDiscountType) as number | undefined,
    orderDiscountValue: Number(data.orderDiscountValue ?? data.OrderDiscountValue ?? 0),
    totalRefunded: Number(data.totalRefunded ?? data.TotalRefunded ?? 0),
    notes: (data.notes ?? data.Notes) as string | undefined,
    salesShiftId: optionalGuid(data.salesShiftId ?? data.SalesShiftId),
    shiftNumber: (data.shiftNumber ?? data.ShiftNumber) as string | undefined,
    items,
    payments: ((data.payments ?? data.Payments ?? []) as Record<string, unknown>[]).map(
      normalizeSalesPaymentLine,
    ),
    refundPayments: ((data.refundPayments ?? data.RefundPayments ?? []) as Record<string, unknown>[]).map(
      (row) => ({
        paymentMethod: Number(row.paymentMethod ?? row.PaymentMethod ?? 1),
        amount: Number(row.amount ?? row.Amount ?? 0),
      }),
    ),
  };
}

function normalizePosBatchHint(row: Record<string, unknown>): PosBatchHint {
  return {
    batchId: String(row.batchId ?? row.BatchId),
    batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
    expiryDate: (row.expiryDate ?? row.ExpiryDate) as string | undefined,
    quantityAvailable: Number(row.quantityAvailable ?? row.QuantityAvailable ?? 0),
    isSuggested: Boolean(row.isSuggested ?? row.IsSuggested),
  };
}

function normalizePosProductLookup(data: Record<string, unknown>): PosProductLookup {
  const rawHints = (data.batchHints ?? data.BatchHints) as Record<string, unknown>[] | undefined;
  return {
    productId: String(data.productId ?? data.ProductId),
    productCode: String(data.productCode ?? data.ProductCode ?? ''),
    productName: String(data.productName ?? data.ProductName ?? ''),
    productUnitId: String(data.productUnitId ?? data.ProductUnitId),
    unitName: String(data.unitName ?? data.UnitName ?? ''),
    conversionFactor: Number(data.conversionFactor ?? data.ConversionFactor ?? 1),
    unitPrice: Number(data.unitPrice ?? data.UnitPrice ?? 0),
    stockAvailable: Number(data.stockAvailable ?? data.StockAvailable ?? 0),
    batchHints: rawHints?.map((row) => normalizePosBatchHint(row)),
    stockSourceLabel: String(data.stockSourceLabel ?? data.StockSourceLabel ?? 'Tồn theo hệ thống'),
  };
}

function normalizePosAllocationPreview(data: Record<string, unknown>): PosAllocationPreview {
  const rawLines = (data.lines ?? data.Lines ?? []) as Record<string, unknown>[];
  return {
    stockSourceLabel: String(data.stockSourceLabel ?? data.StockSourceLabel ?? 'Tồn theo hệ thống'),
    lines: rawLines.map((line) => {
      const rawAllocs = (line.allocations ?? line.Allocations ?? []) as Record<string, unknown>[];
      return {
        productId: String(line.productId ?? line.ProductId),
        productCode: String(line.productCode ?? line.ProductCode ?? ''),
        productName: String(line.productName ?? line.ProductName ?? ''),
        productUnitId: String(line.productUnitId ?? line.ProductUnitId),
        unitName: String(line.unitName ?? line.UnitName ?? ''),
        requestedQuantity: Number(line.requestedQuantity ?? line.RequestedQuantity ?? 0),
        allocations: rawAllocs.map((a) => ({
          batchId: String(a.batchId ?? a.BatchId),
          batchNumber: String(a.batchNumber ?? a.BatchNumber ?? ''),
          expiryDate: (a.expiryDate ?? a.ExpiryDate) as string | undefined,
          quantity: Number(a.quantity ?? a.Quantity ?? 0),
          bookQuantityAvailable: Number(a.bookQuantityAvailable ?? a.BookQuantityAvailable ?? 0),
        })),
      };
    }),
  };
}

export async function searchCustomers(search?: string): Promise<CustomerListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/customers', {
    params: search ? { search } : undefined,
  });
  return data.map((row) => ({
    id: String(row.id ?? row.Id),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: String(row.phone ?? row.Phone ?? ''),
    email: (row.email ?? row.Email) as string | undefined,
  }));
}

export async function lookupPosProduct(
  code: string,
  warehouseId: string,
): Promise<PosProductLookup> {
  const { data } = await http.get<Record<string, unknown>>('/sales/pos/lookup', {
    params: { query: code, warehouseId },
  });
  return normalizePosProductLookup(data);
}

export async function fetchPosStock(
  warehouseId: string,
  productUnitId: string,
): Promise<{ productCode: string; productName: string; unitName: string; stockAvailable: number }> {
  const { data } = await http.get<Record<string, unknown>>('/sales/pos/stock', {
    params: { warehouseId, productUnitId },
  });
  return {
    productCode: String(data.productCode ?? data.ProductCode ?? ''),
    productName: String(data.productName ?? data.ProductName ?? ''),
    unitName: String(data.unitName ?? data.UnitName ?? ''),
    stockAvailable: Number(data.stockAvailable ?? data.StockAvailable ?? 0),
  };
}

export async function fetchPosStockBulk(
  warehouseId: string,
  productUnitIds: string[],
): Promise<
  {
    productId: string;
    productCode: string;
    productName: string;
    productUnitId: string;
    unitName: string;
    stockAvailable: number;
  }[]
> {
  if (productUnitIds.length === 0) return [];
  const { data } = await http.post<Record<string, unknown>[]>('/sales/pos/stock/bulk', {
    warehouseId,
    productUnitIds,
  });
  return data.map((row) => ({
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    productUnitId: String(row.productUnitId ?? row.ProductUnitId),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    stockAvailable: Number(row.stockAvailable ?? row.StockAvailable ?? 0),
  }));
}

export async function searchPosProducts(
  search: string,
  warehouseId: string,
): Promise<PosProductSearchItem[]> {
  const q = search.trim();
  if (!q) return [];
  const { data } = await http.get<Record<string, unknown>[]>('/sales/pos/search', {
    params: { search: q, warehouseId },
  });
  return data.map((row) => ({
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    lookupCode: String(row.lookupCode ?? row.LookupCode ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    stockAvailable: Number(row.stockAvailable ?? row.StockAvailable ?? 0),
  }));
}

export async function fetchReceiptSettings(): Promise<ReceiptStoreSettings> {
  const { data } = await http.get<Record<string, unknown>>('/sales/settings/receipt');
  return {
    name: String(data.name ?? data.Name ?? ''),
    tagline: (data.tagline ?? data.Tagline) as string | undefined,
    phone: (data.phone ?? data.Phone) as string | undefined,
    address: (data.address ?? data.Address) as string | undefined,
  };
}

export async function updateReceiptSettings(
  payload: ReceiptStoreSettings,
): Promise<ReceiptStoreSettings> {
  const { data } = await http.put<Record<string, unknown>>('/sales/settings/receipt', payload);
  return {
    name: String(data.name ?? data.Name ?? ''),
    tagline: (data.tagline ?? data.Tagline) as string | undefined,
    phone: (data.phone ?? data.Phone) as string | undefined,
    address: (data.address ?? data.Address) as string | undefined,
  };
}

export type TenantBatchModeValue = 'off' | 'suggest' | 'label_optional' | 'label_required';

export async function fetchBatchModeSettings(): Promise<TenantBatchModeValue> {
  const { data } = await http.get<Record<string, unknown>>('/sales/settings/batch-mode');
  const mode = String(data.batchMode ?? data.BatchMode ?? 'suggest') as TenantBatchModeValue;
  return mode;
}

export async function updateBatchModeSettings(
  batchMode: TenantBatchModeValue,
): Promise<TenantBatchModeValue> {
  const { data } = await http.put<Record<string, unknown>>('/sales/settings/batch-mode', { batchMode });
  return String(data.batchMode ?? data.BatchMode ?? batchMode) as TenantBatchModeValue;
}

export async function previewPosAllocation(payload: {
  warehouseId: string;
  items: { productId: string; productUnitId: string; quantity: number }[];
}): Promise<PosAllocationPreview> {
  const { data } = await http.post<Record<string, unknown>>('/sales/pos/preview-allocation', payload);
  return normalizePosAllocationPreview(data);
}

export async function fetchSalesOrders(): Promise<SalesOrderListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/orders');
  return data.map((row) => normalizeSalesOrderListItem(row));
}

export async function fetchSalesOrder(id: string): Promise<SalesOrderDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/orders/${id}`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

type SaleLinePayload = {
  productId: string;
  productUnitId: string;
  quantity: number;
  discountType?: number;
  discountValue?: number;
};

type CreateSalePayload = {
  warehouseId: string;
  customerId?: string;
  priceType?: number;
  saveAsDraft?: boolean;
  orderDiscountType?: number;
  orderDiscountValue?: number;
  payments?: { paymentMethod: number; amount: number }[];
  items: SaleLinePayload[];
};

export async function createSale(payload: CreateSalePayload): Promise<SalesOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>('/sales/orders', {
    priceType: 1,
    ...payload,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

export async function completeDraftSale(
  id: string,
  payments?: { paymentMethod: number; amount: number }[],
): Promise<SalesOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${id}/complete`, {
    payments: payments ?? null,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

export async function cancelDraftSale(id: string): Promise<SalesOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${id}/cancel`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

type UpdateDraftPayload = {
  customerId?: string | null;
  priceType?: number;
  orderDiscountType?: number | null;
  orderDiscountValue?: number | null;
  notes?: string | null;
  items: SaleLinePayload[];
};

export async function updateDraftSale(id: string, payload: UpdateDraftPayload): Promise<SalesOrderDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/sales/orders/${id}`, {
    priceType: 1,
    ...payload,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

export async function createSaleReturn(
  orderId: string,
  payload: {
    reason?: string;
    items: { salesOrderItemId: string; quantity: number }[];
    payments?: { paymentMethod: number; amount: number }[];
  },
): Promise<SalesReturnDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${orderId}/returns`, payload);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesReturnDetail(data, rawItems);
}

export async function fetchSalesReturn(id: string): Promise<SalesReturnDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/returns/${id}`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesReturnDetail(data, rawItems);
}

export async function fetchSalesReturns(
  search?: string,
  limit = 50,
): Promise<SalesReturnListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/returns', {
    params: { limit, ...(search?.trim() ? { search: search.trim() } : {}) },
  });
  return data.map((row) => normalizeSalesReturnListItem(row));
}

export async function fetchOrderReturns(orderId: string): Promise<SalesReturnListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>(`/sales/orders/${orderId}/returns`);
  return data.map((row) => normalizeSalesReturnListItem(row));
}

function normalizeSalesShiftSummary(data: Record<string, unknown>): SalesShiftSummary {
  const rawMethods = (data.byMethod ?? data.ByMethod ?? []) as Record<string, unknown>[];
  return {
    from: String(data.from ?? data.From ?? ''),
    to: String(data.to ?? data.To ?? ''),
    totalSales: Number(data.totalSales ?? data.TotalSales ?? 0),
    totalRefunds: Number(data.totalRefunds ?? data.TotalRefunds ?? 0),
    netTotal: Number(data.netTotal ?? data.NetTotal ?? 0),
    byMethod: rawMethods.map((row) => ({
      paymentMethod: Number(row.paymentMethod ?? row.PaymentMethod ?? 1),
      salesAmount: Number(row.salesAmount ?? row.SalesAmount ?? 0),
      refundAmount: Number(row.refundAmount ?? row.RefundAmount ?? 0),
      netAmount: Number(row.netAmount ?? row.NetAmount ?? 0),
    })),
    openingCash: Number(data.openingCash ?? data.OpeningCash ?? 0),
    cashSales: Number(data.cashSales ?? data.CashSales ?? 0),
    cashRefunds: Number(data.cashRefunds ?? data.CashRefunds ?? 0),
    expectedCash: Number(data.expectedCash ?? data.ExpectedCash ?? 0),
    closingCash: (data.closingCash ?? data.ClosingCash) != null
      ? Number(data.closingCash ?? data.ClosingCash)
      : undefined,
    cashVariance: (data.cashVariance ?? data.CashVariance) != null
      ? Number(data.cashVariance ?? data.CashVariance)
      : undefined,
  };
}

function normalizeSalesShiftDetail(data: Record<string, unknown>): SalesShiftDetail {
  const summaryRaw = (data.summary ?? data.Summary ?? {}) as Record<string, unknown>;
  return {
    id: String(data.id ?? data.Id),
    shiftNumber: String(data.shiftNumber ?? data.ShiftNumber ?? ''),
    warehouseId: String(data.warehouseId ?? data.WarehouseId),
    warehouseName: String(data.warehouseName ?? data.WarehouseName ?? ''),
    openedByUserName: String(data.openedByUserName ?? data.OpenedByUserName ?? ''),
    closedByUserName: (data.closedByUserName ?? data.ClosedByUserName) as string | undefined,
    openedAt: String(data.openedAt ?? data.OpenedAt ?? ''),
    closedAt: (data.closedAt ?? data.ClosedAt) as string | undefined,
    openingCash: Number(data.openingCash ?? data.OpeningCash ?? 0),
    closingCash: (data.closingCash ?? data.ClosingCash) != null
      ? Number(data.closingCash ?? data.ClosingCash)
      : undefined,
    expectedCash: (data.expectedCash ?? data.ExpectedCash) != null
      ? Number(data.expectedCash ?? data.ExpectedCash)
      : undefined,
    cashVariance: (data.cashVariance ?? data.CashVariance) != null
      ? Number(data.cashVariance ?? data.CashVariance)
      : undefined,
    status: Number(data.status ?? data.Status ?? 1),
    closeNotes: (data.closeNotes ?? data.CloseNotes) as string | undefined,
    summary: normalizeSalesShiftSummary(summaryRaw),
    lotAlerts: ((data.lotAlerts ?? data.LotAlerts ?? []) as Record<string, unknown>[]).map((row) => ({
      productId: String(row.productId ?? row.ProductId),
      productCode: String(row.productCode ?? row.ProductCode ?? ''),
      productName: String(row.productName ?? row.ProductName ?? ''),
      soldBatchNumber: String(row.soldBatchNumber ?? row.SoldBatchNumber ?? ''),
      soldExpiryDate: (row.soldExpiryDate ?? row.SoldExpiryDate) as string | undefined,
      earlierBatchNumber: String(row.earlierBatchNumber ?? row.EarlierBatchNumber ?? ''),
      earlierExpiryDate: (row.earlierExpiryDate ?? row.EarlierExpiryDate) as string | undefined,
      earlierBookQuantity: Number(row.earlierBookQuantity ?? row.EarlierBookQuantity ?? 0),
      stockSourceLabel: String(row.stockSourceLabel ?? row.StockSourceLabel ?? 'Tồn theo hệ thống'),
    })),
  };
}

function normalizeSalesShiftListItem(data: Record<string, unknown>): SalesShiftListItem {
  return {
    id: String(data.id ?? data.Id),
    shiftNumber: String(data.shiftNumber ?? data.ShiftNumber ?? ''),
    warehouseId: String(data.warehouseId ?? data.WarehouseId),
    warehouseName: String(data.warehouseName ?? data.WarehouseName ?? ''),
    openedByUserName: String(data.openedByUserName ?? data.OpenedByUserName ?? ''),
    openedAt: String(data.openedAt ?? data.OpenedAt ?? ''),
    closedAt: (data.closedAt ?? data.ClosedAt) as string | undefined,
    openingCash: Number(data.openingCash ?? data.OpeningCash ?? 0),
    closingCash: (data.closingCash ?? data.ClosingCash) != null
      ? Number(data.closingCash ?? data.ClosingCash)
      : undefined,
    cashVariance: (data.cashVariance ?? data.CashVariance) != null
      ? Number(data.cashVariance ?? data.CashVariance)
      : undefined,
    status: Number(data.status ?? data.Status ?? 1),
  };
}

export async function fetchSalesShiftSummary(from: string, to: string): Promise<SalesShiftSummary> {
  const { data } = await http.get<Record<string, unknown>>('/sales/shift-summary', {
    params: { from, to },
  });
  return normalizeSalesShiftSummary(data);
}

export async function fetchSalesShifts(limit = 50): Promise<SalesShiftListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/shifts', { params: { limit } });
  return data.map(normalizeSalesShiftListItem);
}

export async function fetchOpenShift(warehouseId: string): Promise<SalesShiftDetail | null> {
  try {
    const { data } = await http.get<Record<string, unknown>>('/sales/shifts/current', {
      params: { warehouseId },
    });
    return normalizeSalesShiftDetail(data);
  } catch (error) {
    if (typeof error === 'object' && error !== null && 'response' in error) {
      const status = (error as { response?: { status?: number } }).response?.status;
      if (status === 404) return null;
    }
    throw error;
  }
}

export async function fetchSalesShift(id: string): Promise<SalesShiftDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/shifts/${id}`);
  return normalizeSalesShiftDetail(data);
}

export async function openSalesShift(payload: {
  warehouseId: string;
  openingCash: number;
}): Promise<SalesShiftDetail> {
  const { data } = await http.post<Record<string, unknown>>('/sales/shifts/open', payload);
  return normalizeSalesShiftDetail(data);
}

export async function closeSalesShift(
  id: string,
  payload: { closingCash: number; closeNotes?: string },
): Promise<SalesShiftDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/shifts/${id}/close`, payload);
  return normalizeSalesShiftDetail(data);
}
