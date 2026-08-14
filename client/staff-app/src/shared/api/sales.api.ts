import { http } from '@/shared/api/http';
import type {
  CreateSalePayload,
  CustomerListItem,
  PosCustomerLoyalty,
  PosCustomerVoucher,
  PosProductLookup,
  PosProductSearchItem,
  ReceiptStoreSettings,
  SalesDiscountType,
  SalesOrderDetail,
  SalesOrderDetailFull,
  SalesOrderListItem,
  SalesReturnDetail,
  SalesShiftDetail,
  SalesShiftSummary,
  TenantBatchModeValue,
  TenantRxSettings,
  Warehouse,
} from '@/shared/api/sales.types';

export type PrescriptionPosLoadLine = {
  prescriptionLineId: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId?: string;
  unitName?: string;
  unitPrice: number;
  qtyRemaining: number;
  stockAvailable: number;
  lineDispensingClass: string;
};

export type PrescriptionPosLoad = {
  id: string;
  prescriptionCode: string;
  customerId?: string;
  patientName?: string;
  patientPhone?: string;
  lines: PrescriptionPosLoadLine[];
};

function row(data: Record<string, unknown>) {
  return data;
}

function normalizeOrderItem(item: Record<string, unknown>) {
  const batchIdRaw = item.batchId ?? item.BatchId;
  return {
    id: String(item.id ?? item.Id),
    productId: String(item.productId ?? item.ProductId ?? ''),
    productUnitId: String(item.productUnitId ?? item.ProductUnitId ?? ''),
    productCode: String(item.productCode ?? item.ProductCode ?? ''),
    productName: String(item.productName ?? item.ProductName ?? ''),
    unitName: String(item.unitName ?? item.UnitName ?? ''),
    quantity: Number(item.quantity ?? item.Quantity ?? 0),
    unitPrice: Number(item.unitPrice ?? item.UnitPrice ?? 0),
    lineTotal: Number(item.lineTotal ?? item.LineTotal ?? 0),
    batchNumber: (item.batchNumber ?? item.BatchNumber) as string | undefined,
    batchId: batchIdRaw != null && batchIdRaw !== '' ? String(batchIdRaw) : undefined,
    returnedQuantity: Number(item.returnedQuantity ?? item.ReturnedQuantity ?? 0),
    discountType: (item.discountType ?? item.DiscountType) as SalesDiscountType | undefined,
    discountValue: (item.discountValue ?? item.DiscountValue) as number | undefined,
  };
}

function normalizeOrderDetail(data: Record<string, unknown>, rawItems: Record<string, unknown>[]): SalesOrderDetail {
  const base = normalizeOrder(data, rawItems);
  return {
    ...base,
    status: Number(data.status ?? data.Status ?? 1),
    warehouseId: String(data.warehouseId ?? data.WarehouseId ?? ''),
    customerId: (data.customerId ?? data.CustomerId) as string | undefined,
    orderDiscountType: (data.orderDiscountType ?? data.OrderDiscountType) as number | undefined,
    orderDiscountValue: (data.orderDiscountValue ?? data.OrderDiscountValue) as number | undefined,
  };
}

function normalizeOrderListItem(row: Record<string, unknown>): SalesOrderListItem {
  return {
    id: String(row.id ?? row.Id),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    warehouseId: row.warehouseId != null || row.WarehouseId != null
      ? String(row.warehouseId ?? row.WarehouseId)
      : undefined,
    warehouseName: (row.warehouseName ?? row.WarehouseName) as string | undefined,
    customerId: (row.customerId ?? row.CustomerId) as string | null | undefined,
    customerName: (row.customerName ?? row.CustomerName) as string | undefined,
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    status: Number(row.status ?? row.Status ?? 1),
    shiftNumber: (row.shiftNumber ?? row.ShiftNumber) as string | null | undefined,
  };
}

function normalizeOrderFull(data: Record<string, unknown>, rawItems: Record<string, unknown>[]): SalesOrderDetailFull {
  const base = normalizeOrder(data, rawItems);
  return {
    ...base,
    status: Number(data.status ?? data.Status ?? 1),
    warehouseId: String(data.warehouseId ?? data.WarehouseId ?? ''),
    warehouseName: (data.warehouseName ?? data.WarehouseName) as string | undefined,
    customerId: (data.customerId ?? data.CustomerId) as string | undefined,
    amountPaid: Number(data.amountPaid ?? data.AmountPaid ?? base.amountPaid ?? 0),
    outstanding:
      data.outstanding != null || data.Outstanding != null
        ? Number(data.outstanding ?? data.Outstanding)
        : undefined,
    discountAmount: Number(data.discountAmount ?? data.DiscountAmount ?? 0),
    items: rawItems.map(normalizeOrderItem),
  };
}

function normalizeOrder(data: Record<string, unknown>, rawItems: Record<string, unknown>[]): SalesOrderDetail {
  return {
    id: String(data.id ?? data.Id),
    orderNumber: String(data.orderNumber ?? data.OrderNumber ?? ''),
    orderDate: String(data.orderDate ?? data.OrderDate ?? ''),
    totalAmount: Number(data.totalAmount ?? data.TotalAmount ?? 0),
    amountPaid: Number(data.amountPaid ?? data.AmountPaid ?? data.totalAmount ?? 0),
    status: Number(data.status ?? data.Status ?? 1),
    customerName: (data.customerName ?? data.CustomerName) as string | undefined,
    items: rawItems.map((item) => {
      const line = normalizeOrderItem(item);
      return {
        productCode: line.productCode,
        productName: line.productName,
        unitName: line.unitName,
        quantity: line.quantity,
        unitPrice: line.unitPrice,
        lineTotal: line.lineTotal,
        batchNumber: line.batchNumber,
      };
    }),
    payments: ((data.payments ?? data.Payments ?? []) as Record<string, unknown>[]).map((p) => ({
      paymentMethod: Number(p.paymentMethod ?? p.PaymentMethod ?? 1),
      amount: Number(p.amount ?? p.Amount ?? 0),
    })),
  };
}

function normalizeLookup(data: Record<string, unknown>): PosProductLookup {
  const hints = (data.batchHints ?? data.BatchHints) as Record<string, unknown>[] | undefined;
  return {
    productId: String(data.productId ?? data.ProductId),
    productCode: String(data.productCode ?? data.ProductCode ?? ''),
    productName: String(data.productName ?? data.ProductName ?? ''),
    productUnitId: String(data.productUnitId ?? data.ProductUnitId),
    unitName: String(data.unitName ?? data.UnitName ?? ''),
    unitPrice: Number(data.unitPrice ?? data.UnitPrice ?? 0),
    stockAvailable: Number(data.stockAvailable ?? data.StockAvailable ?? 0),
    dispensingClass: String(data.dispensingClass ?? data.DispensingClass ?? 'otc'),
    batchHints: hints?.map((h) => ({
      batchId: String(h.batchId ?? h.BatchId),
      batchNumber: String(h.batchNumber ?? h.BatchNumber ?? ''),
      expiryDate: (h.expiryDate ?? h.ExpiryDate) as string | undefined,
      quantityAvailable: Number(h.quantityAvailable ?? h.QuantityAvailable ?? 0),
      isSuggested: Boolean(h.isSuggested ?? h.IsSuggested),
    })),
  };
}

function normalizePrescriptionPosLoad(data: Record<string, unknown>): PrescriptionPosLoad {
  const rawLines = (data.lines ?? data.Lines ?? []) as Record<string, unknown>[];
  return {
    id: String(data.id ?? data.Id),
    prescriptionCode: String(data.prescriptionCode ?? data.PrescriptionCode ?? ''),
    customerId: (data.customerId ?? data.CustomerId) as string | undefined,
    patientName: (data.patientName ?? data.PatientName) as string | undefined,
    patientPhone: (data.patientPhone ?? data.PatientPhone) as string | undefined,
    lines: rawLines.map((line) => ({
      prescriptionLineId: String(line.prescriptionLineId ?? line.PrescriptionLineId),
      productId: String(line.productId ?? line.ProductId),
      productCode: String(line.productCode ?? line.ProductCode ?? ''),
      productName: String(line.productName ?? line.ProductName ?? ''),
      productUnitId: (line.productUnitId ?? line.ProductUnitId) as string | undefined,
      unitName: (line.unitName ?? line.UnitName) as string | undefined,
      unitPrice: Number(line.unitPrice ?? line.UnitPrice ?? 0),
      qtyRemaining: Number(line.qtyRemaining ?? line.QtyRemaining ?? 0),
      stockAvailable: Number(line.stockAvailable ?? line.StockAvailable ?? 0),
      lineDispensingClass: String(line.lineDispensingClass ?? line.LineDispensingClass ?? 'otc'),
    })),
  };
}

export async function fetchWarehouses(): Promise<Warehouse[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/inventory/warehouses');
  return data.map((w) => ({
    id: String(w.id ?? w.Id),
    warehouseCode: String(w.warehouseCode ?? w.WarehouseCode ?? ''),
    warehouseName: String(w.warehouseName ?? w.WarehouseName ?? ''),
    branchName: (w.branchName ?? w.BranchName) as string | undefined,
  }));
}

export async function searchPosProducts(search: string, warehouseId: string): Promise<PosProductSearchItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/pos/search', {
    params: { search, warehouseId },
  });
  return data.map((item) => ({
    productCode: String(item.productCode ?? item.ProductCode ?? ''),
    productName: String(item.productName ?? item.ProductName ?? ''),
    lookupCode: String(item.lookupCode ?? item.LookupCode ?? item.productCode ?? ''),
    unitName: String(item.unitName ?? item.UnitName ?? ''),
    unitPrice: Number(item.unitPrice ?? item.UnitPrice ?? 0),
    stockAvailable: Number(item.stockAvailable ?? item.StockAvailable ?? 0),
  }));
}

export async function lookupPosProduct(query: string, warehouseId: string): Promise<PosProductLookup> {
  const { data } = await http.get<Record<string, unknown>>('/sales/pos/lookup', {
    params: { query, warehouseId },
  });
  return normalizeLookup(row(data));
}

export async function fetchBatchModeSettings(): Promise<TenantBatchModeValue> {
  const { data } = await http.get<Record<string, unknown>>('/sales/settings/batch-mode');
  const mode = String(data.batchMode ?? data.BatchMode ?? 'suggest');
  if (mode === 'suggest' || mode === 'label_optional' || mode === 'label_required' || mode === 'off') return mode;
  return 'suggest';
}

export async function fetchRxSettings(): Promise<TenantRxSettings> {
  const { data } = await http.get<Record<string, unknown>>('/pharmacy/rx/settings');
  const mode = String(data.enforcementMode ?? data.EnforcementMode ?? 'off').toLowerCase();
  return {
    enforcementMode: mode === 'strict' || mode === 'warn' ? mode : 'off',
    posBlockedAudit: Boolean(data.posBlockedAudit ?? data.PosBlockedAudit ?? true),
  };
}

export async function reportRxPosBlock(productId: string, warehouseId: string): Promise<void> {
  await http.post('/pharmacy/rx/pos-block', { productId, warehouseId });
}

function normalizeShiftSummary(data: Record<string, unknown>): SalesShiftSummary {
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
  };
}

function normalizeOpenShift(data: Record<string, unknown>): SalesShiftDetail {
  const summaryRaw = data.summary ?? data.Summary;
  return {
    id: String(data.id ?? data.Id),
    shiftNumber: String(data.shiftNumber ?? data.ShiftNumber ?? ''),
    warehouseId: String(data.warehouseId ?? data.WarehouseId ?? ''),
    warehouseName: (data.warehouseName ?? data.WarehouseName) as string | undefined,
    status: Number(data.status ?? data.Status ?? 1),
    openedAt: (data.openedAt ?? data.OpenedAt) as string | undefined,
    summary: summaryRaw ? normalizeShiftSummary(summaryRaw as Record<string, unknown>) : undefined,
    lotAlerts: ((data.lotAlerts ?? data.LotAlerts ?? []) as Record<string, unknown>[]).map((row) => ({
      productId: String(row.productId ?? row.ProductId),
      productCode: String(row.productCode ?? row.ProductCode ?? ''),
      productName: String(row.productName ?? row.ProductName ?? ''),
      soldBatchNumber: String(row.soldBatchNumber ?? row.SoldBatchNumber ?? ''),
      soldExpiryDate: (row.soldExpiryDate ?? row.SoldExpiryDate) as string | undefined,
      earlierBatchNumber: String(row.earlierBatchNumber ?? row.EarlierBatchNumber ?? ''),
      earlierExpiryDate: (row.earlierExpiryDate ?? row.EarlierExpiryDate) as string | undefined,
      earlierBookQuantity: Number(row.earlierBookQuantity ?? row.EarlierBookQuantity ?? 0),
      stockSourceLabel: String(row.stockSourceLabel ?? row.StockSourceLabel ?? 'Hệ thống'),
    })),
  };
}

export function normalizePosCustomerLoyalty(row: Record<string, unknown>): PosCustomerLoyalty {
  return {
    loyaltyEnabled: Boolean(row.loyaltyEnabled ?? row.LoyaltyEnabled ?? false),
    pointsBalance: Number(row.pointsBalance ?? row.PointsBalance ?? 0),
    amountPerPoint: Number(row.amountPerPoint ?? row.AmountPerPoint ?? 0),
    pointsPerAmount: Number(row.pointsPerAmount ?? row.PointsPerAmount ?? 0),
    maxRedeemPercent: Number(row.maxRedeemPercent ?? row.MaxRedeemPercent ?? 100),
    maxRedeemDiscountAmount: Number(row.maxRedeemDiscountAmount ?? row.MaxRedeemDiscountAmount ?? 0),
    maxRedeemPoints: Number(row.maxRedeemPoints ?? row.MaxRedeemPoints ?? 0),
  };
}

export async function fetchPosCustomerLoyalty(
  customerId: string,
  orderTotal: number,
): Promise<PosCustomerLoyalty | null> {
  try {
    const { data } = await http.get<Record<string, unknown>>('/sales/pos/customer-loyalty', {
      params: { customerId, orderTotal },
    });
    return normalizePosCustomerLoyalty(data);
  } catch {
    return null;
  }
}

export async function fetchShiftSummary(
  from: string,
  to: string,
  warehouseId?: string,
): Promise<SalesShiftSummary> {
  const { data } = await http.get<Record<string, unknown>>('/sales/shift-summary', {
    params: {
      from,
      to,
      ...(warehouseId ? { warehouseId } : {}),
    },
  });
  return normalizeShiftSummary(row(data));
}

export async function fetchOpenShift(warehouseId: string): Promise<SalesShiftDetail | null> {
  try {
    const { data } = await http.get<Record<string, unknown>>('/sales/shifts/current', {
      params: { warehouseId },
    });
    return normalizeOpenShift(row(data));
  } catch (error) {
    if (typeof error === 'object' && error !== null && 'response' in error) {
      if ((error as { response?: { status?: number } }).response?.status === 404) return null;
    }
    throw error;
  }
}

export async function fetchPrescriptionPosLoad(
  prescriptionId: string,
  warehouseId: string,
  priceType = 1,
): Promise<PrescriptionPosLoad> {
  const { data } = await http.get<Record<string, unknown>>(`/pharmacy/prescriptions/${prescriptionId}/pos-load`, {
    params: { warehouseId, priceType },
  });
  return normalizePrescriptionPosLoad(data);
}

export async function openSalesShift(payload: {
  warehouseId: string;
  openingCash: number;
}): Promise<SalesShiftDetail> {
  const { data } = await http.post<Record<string, unknown>>('/sales/shifts/open', payload);
  return normalizeOpenShift(row(data));
}

export async function closeSalesShift(
  shiftId: string,
  payload: { closingCash: number; closeNotes?: string },
): Promise<SalesShiftDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/shifts/${shiftId}/close`, payload);
  return normalizeOpenShift(row(data));
}

export async function searchCustomers(
  search?: string,
  options?: { page?: number; pageSize?: number },
): Promise<{
  items: CustomerListItem[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}> {
  const page = Math.max(1, options?.page ?? 1);
  const pageSize = Math.min(100, Math.max(1, options?.pageSize ?? 30));
  const { data } = await http.get<Record<string, unknown> | Record<string, unknown>[]>('/sales/customers', {
    params: { search, page, pageSize },
  });

  const mapRow = (c: Record<string, unknown>): CustomerListItem => ({
    id: String(c.id ?? c.Id),
    customerCode: String(c.customerCode ?? c.CustomerCode ?? ''),
    fullName: String(c.fullName ?? c.FullName ?? ''),
    phone: String(c.phone ?? c.Phone ?? ''),
    allowCredit: Boolean(c.allowCredit ?? c.AllowCredit),
    creditLimit:
      c.creditLimit != null || c.CreditLimit != null
        ? Number(c.creditLimit ?? c.CreditLimit)
        : null,
    currentOutstanding: Number(c.currentOutstanding ?? c.CurrentOutstanding ?? 0),
    customerGroupName: (c.customerGroupName ?? c.CustomerGroupName) as string | null | undefined,
    groupDiscountPercent: Number(c.groupDiscountPercent ?? c.GroupDiscountPercent ?? 0),
    pharmacyRelation: String(c.pharmacyRelation ?? c.PharmacyRelation ?? 'member'),
  });

  // Backward-compatible: older API returned a bare array.
  if (Array.isArray(data)) {
    const items = data.map(mapRow);
    return { items, total: items.length, page: 1, pageSize: items.length || pageSize, hasMore: false };
  }

  const rows = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(mapRow);
  const total = Number(data.total ?? data.Total ?? rows.length);
  const resolvedPage = Number(data.page ?? data.Page ?? page);
  const resolvedPageSize = Number(data.pageSize ?? data.PageSize ?? pageSize);
  return {
    items: rows,
    total,
    page: resolvedPage,
    pageSize: resolvedPageSize,
    hasMore: resolvedPage * resolvedPageSize < total,
  };
}

export async function fetchPosStockBulk(
  warehouseId: string,
  productUnitIds: string[],
): Promise<Record<string, number>> {
  const { data } = await http.post<Record<string, unknown>>('/sales/pos/stock/bulk', {
    warehouseId,
    productUnitIds,
  });
  const items = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  const map: Record<string, number> = {};
  for (const item of items) {
    map[String(item.productUnitId ?? item.ProductUnitId)] = Number(
      item.stockAvailable ?? item.StockAvailable ?? 0,
    );
  }
  return map;
}

export async function previewPosAllocation(payload: {
  warehouseId: string;
  items: { productId: string; productUnitId: string; quantity: number; batchNumber?: string }[];
}): Promise<void> {
  await http.post('/sales/pos/preview-allocation', payload);
}

export async function fetchPosCustomerVouchers(
  customerId: string,
  orderTotal: number,
): Promise<PosCustomerVoucher[]> {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/sales/pos/customer-vouchers',
    { params: { customerId, orderTotal } },
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map((row) => ({
    customerVoucherId: String(row.customerVoucherId ?? row.CustomerVoucherId ?? ''),
    voucherId: String(row.voucherId ?? row.VoucherId ?? ''),
    voucherCode: String(row.voucherCode ?? row.VoucherCode ?? ''),
    voucherName: String(row.voucherName ?? row.VoucherName ?? ''),
    discountType: Number(row.discountType ?? row.DiscountType ?? 2),
    discountValue: Number(row.discountValue ?? row.DiscountValue ?? 0),
    minOrderAmount: Number(row.minOrderAmount ?? row.MinOrderAmount ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
  }));
}

export async function createSale(payload: CreateSalePayload): Promise<SalesOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>('/sales/orders', {
    priceType: 1,
    ...payload,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeOrder(row(data), rawItems);
}

export async function fetchReceiptSettings(): Promise<ReceiptStoreSettings> {
  const { data } = await http.get<Record<string, unknown>>('/sales/settings/receipt');
  return {
    name: String(data.name ?? data.Name ?? 'Nhà thuốc'),
    phone: (data.phone ?? data.Phone) as string | undefined,
    address: (data.address ?? data.Address) as string | undefined,
    tagline: (data.tagline ?? data.Tagline) as string | undefined,
  };
}

export async function searchSalesOrders(
  query: string,
  mode: 'document' | 'customer' = 'document',
): Promise<SalesOrderListItem[]> {
  const trimmed = query.trim();
  const params: Record<string, string | number> = { pageSize: 30, page: 1 };
  if (mode === 'customer') {
    params.customerSearch = trimmed;
  } else {
    params.documentSearch = trimmed;
  }
  const { data } = await http.get<Record<string, unknown>>('/sales/orders', { params });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rawItems
    .map(normalizeOrderListItem)
    .filter((item) => item.status === 2 || item.status === 4);
}

export async function fetchSalesOrderById(id: string): Promise<SalesOrderDetailFull> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/orders/${id}`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeOrderFull(row(data), rawItems);
}

export async function fetchSalesOrder(id: string): Promise<SalesOrderDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/orders/${id}`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeOrderDetail(row(data), rawItems);
}

export async function fetchDraftSalesOrders(): Promise<SalesOrderListItem[]> {
  const { data } = await http.get<Record<string, unknown>>('/sales/orders', {
    params: { status: 1, pageSize: 50, page: 1 },
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rawItems.map(normalizeOrderListItem).filter((item) => item.status === 1);
}

export async function cancelDraftSale(id: string): Promise<void> {
  await http.post(`/sales/orders/${id}/cancel`);
}

export async function updateDraftSale(
  id: string,
  payload: {
    customerId?: string | null;
    orderDiscountType?: number | null;
    orderDiscountValue?: number | null;
    notes?: string | null;
    items: CreateSalePayload['items'];
  },
): Promise<SalesOrderDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/sales/orders/${id}`, {
    priceType: 1,
    ...payload,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeOrderDetail(row(data), rawItems);
}

export async function completeDraftSale(
  id: string,
  options: {
    payments?: { paymentMethod: number; amount: number }[];
    customerId?: string | null;
    orderDiscountType?: number | null;
    orderDiscountValue?: number | null;
    loyaltyDiscountAmount?: number;
    customerVoucherId?: string;
    prescriptionId?: string;
    orderReminderLabel?: string | null;
    orderReminderDaysSupply?: number;
    items?: CreateSalePayload['items'];
  },
): Promise<SalesOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${id}/complete`, {
    payments: options.payments ?? null,
    ...(options.customerId !== undefined ? { customerId: options.customerId } : {}),
    ...(options.items?.length
      ? {
          items: options.items,
          orderDiscountType: options.orderDiscountType ?? null,
          orderDiscountValue: options.orderDiscountValue ?? null,
        }
      : {}),
    ...(options.loyaltyDiscountAmount != null && options.loyaltyDiscountAmount > 0
      ? { loyaltyDiscountAmount: options.loyaltyDiscountAmount }
      : {}),
    ...(options.customerVoucherId ? { customerVoucherId: options.customerVoucherId } : {}),
    ...(options.prescriptionId ? { prescriptionId: options.prescriptionId } : {}),
    ...(options.orderReminderDaysSupply != null && options.orderReminderDaysSupply >= 1
      ? {
          orderReminderLabel: options.orderReminderLabel ?? null,
          orderReminderDaysSupply: options.orderReminderDaysSupply,
        }
      : {}),
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeOrder(row(data), rawItems);
}

export async function createSaleReturn(
  orderId: string,
  payload: {
    reason?: string;
    items: { salesOrderItemId: string; quantity: number }[];
    payments: { paymentMethod: number; amount: number }[];
  },
): Promise<SalesReturnDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${orderId}/returns`, payload);
  return {
    id: String(data.id ?? data.Id),
    returnNumber: String(data.returnNumber ?? data.ReturnNumber ?? ''),
    orderNumber: String(data.orderNumber ?? data.OrderNumber ?? ''),
    totalRefund: Number(data.totalRefund ?? data.TotalRefund ?? 0),
  };
}
