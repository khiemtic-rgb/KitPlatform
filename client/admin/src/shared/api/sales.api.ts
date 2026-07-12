import { http } from '@/shared/api/http';
import { salesT } from '@/shared/i18n';
import type {
  CompleteDraftSaleRequest,
  CreateSaleLineRequest,
  CreateSaleRequest,
  UpdateDraftSaleRequest,
} from '@/shared/api/generated';
import type {
  CustomerListItem,
  PosAllocationPreview,
  PosBatchHint,
  PosCustomerLoyalty,
  PosCustomerVoucher,
  PosProductLookup,
  PosProductSearchItem,
  ReceiptStoreSettings,
  CustomerAppStoreSettings,
  SalesOrderDetail,
  SalesOrderListItem,
  SalesOrderListFilters,
  SalesOrderPagedListResult,
  SalesReturnDetail,
  SalesReturnListItem,
  SalesShiftDetail,
  SalesShiftListItem,
  SalesShiftSummary,
  SalesPaymentLine,
  TenantRxSettings,
  CustomerPaymentListFilters,
  CustomerPaymentListItem,
  CustomerReceivablesDetail,
  CustomerReceivablesRow,
} from '@/shared/api/sales.types';

export type { TenantRxSettings } from '@/shared/api/sales.types';

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
    amountPaid: Number(row.amountPaid ?? row.AmountPaid ?? row.totalAmount ?? row.TotalAmount ?? 0),
    outstanding: Number(row.outstanding ?? row.Outstanding ?? 0),
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

function optionalInt(value: unknown): number | undefined {
  if (value == null || value === '') return undefined;
  const n = Number(value);
  return Number.isFinite(n) ? n : undefined;
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
    amountPaid: Number(data.amountPaid ?? data.AmountPaid ?? data.totalAmount ?? data.TotalAmount ?? 0),
    outstanding: Number(data.outstanding ?? data.Outstanding ?? 0),
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
    loyaltyPointsEarned: optionalInt(data.loyaltyPointsEarned ?? data.LoyaltyPointsEarned),
    loyaltyPointsRedeemed: Number(data.loyaltyPointsRedeemed ?? data.LoyaltyPointsRedeemed ?? 0),
    loyaltyDiscountAmount: Number(data.loyaltyDiscountAmount ?? data.LoyaltyDiscountAmount ?? 0),
    voucherDiscountAmount: Number(data.voucherDiscountAmount ?? data.VoucherDiscountAmount ?? 0),
    voucherCode: (data.voucherCode ?? data.VoucherCode ?? undefined) as string | undefined,
    voucherName: (data.voucherName ?? data.VoucherName ?? undefined) as string | undefined,
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
    dispensingClass: String(data.dispensingClass ?? data.DispensingClass ?? 'otc'),
    batchHints: rawHints?.map((row) => normalizePosBatchHint(row)),
    stockSourceLabel: String(
      data.stockSourceLabel ?? data.StockSourceLabel ?? salesT()('pos.allocation.stockSourceSystem'),
    ),
  };
}

function normalizePosAllocationPreview(data: Record<string, unknown>): PosAllocationPreview {
  const rawLines = (data.lines ?? data.Lines ?? []) as Record<string, unknown>[];
  return {
    stockSourceLabel: String(
      data.stockSourceLabel ?? data.StockSourceLabel ?? salesT()('pos.allocation.stockSourceSystem'),
    ),
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
    allowCredit: Boolean(row.allowCredit ?? row.AllowCredit),
    creditLimit:
      row.creditLimit != null || row.CreditLimit != null
        ? Number(row.creditLimit ?? row.CreditLimit)
        : undefined,
    currentOutstanding: Number(row.currentOutstanding ?? row.CurrentOutstanding ?? 0),
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

export async function fetchRxSettings(): Promise<TenantRxSettings> {
  const { data } = await http.get<Record<string, unknown>>('/pharmacy/rx/settings');
  return {
    enforcementMode: normalizeRxEnforcementMode(data.enforcementMode ?? data.EnforcementMode),
    posBlockedAudit: Boolean(data.posBlockedAudit ?? data.PosBlockedAudit ?? true),
  };
}

export async function updateRxSettings(settings: TenantRxSettings): Promise<TenantRxSettings> {
  const { data } = await http.put<Record<string, unknown>>('/pharmacy/rx/settings', {
    enforcementMode: settings.enforcementMode,
    posBlockedAudit: settings.posBlockedAudit,
  });
  return {
    enforcementMode: normalizeRxEnforcementMode(data.enforcementMode ?? data.EnforcementMode),
    posBlockedAudit: Boolean(data.posBlockedAudit ?? data.PosBlockedAudit ?? settings.posBlockedAudit),
  };
}

export async function reportRxPosBlock(productId: string, warehouseId: string): Promise<void> {
  await http.post('/pharmacy/rx/pos-block', { productId, warehouseId });
}

function normalizeRxEnforcementMode(value: unknown): TenantRxSettings['enforcementMode'] {
  const mode = String(value ?? 'off').toLowerCase();
  if (mode === 'strict' || mode === 'warn') return mode;
  return 'off';
}

function normalizeCustomerAppSettings(row: Record<string, unknown>): CustomerAppStoreSettings {
  return {
    appName: String(row.appName ?? row.AppName ?? ''),
    shortName: String(row.shortName ?? row.ShortName ?? ''),
    logoUrl: String(row.logoUrl ?? row.LogoUrl ?? '/customer-app/icon.svg'),
    primaryColor: String(row.primaryColor ?? row.PrimaryColor ?? '#0F52BA'),
    secondaryColor: String(row.secondaryColor ?? row.SecondaryColor ?? '#3CB371'),
    supportPhone: String(row.supportPhone ?? row.SupportPhone ?? ''),
    tagline: String(row.tagline ?? row.Tagline ?? ''),
  };
}

export async function fetchCustomerAppSettings(): Promise<CustomerAppStoreSettings> {
  const { data } = await http.get<Record<string, unknown>>('/sales/settings/customer-app');
  return normalizeCustomerAppSettings(data);
}

export async function updateCustomerAppSettings(
  payload: CustomerAppStoreSettings,
): Promise<CustomerAppStoreSettings> {
  const { data } = await http.put<Record<string, unknown>>('/sales/settings/customer-app', {
    appName: payload.appName,
    shortName: payload.shortName,
    logoUrl: payload.logoUrl || undefined,
    primaryColor: payload.primaryColor || undefined,
    secondaryColor: payload.secondaryColor || undefined,
    supportPhone: payload.supportPhone || undefined,
    tagline: payload.tagline || undefined,
  });
  return normalizeCustomerAppSettings(data);
}

export async function previewPosAllocation(payload: {
  warehouseId: string;
  items: { productId: string; productUnitId: string; quantity: number }[];
}): Promise<PosAllocationPreview> {
  const { data } = await http.post<Record<string, unknown>>('/sales/pos/preview-allocation', payload);
  return normalizePosAllocationPreview(data);
}

export async function fetchSalesOrders(
  filters?: SalesOrderListFilters,
): Promise<SalesOrderPagedListResult> {
  const params: Record<string, string | number | undefined> = {};
  if (filters?.search) params.search = filters.search;
  if (filters?.customerSearch?.trim()) params.customerSearch = filters.customerSearch.trim();
  if (filters?.documentSearch?.trim()) params.documentSearch = filters.documentSearch.trim();
  if (filters?.status != null) params.status = filters.status;
  if (filters?.page != null) params.page = filters.page;
  if (filters?.pageSize != null) params.pageSize = filters.pageSize;

  const { data } = await http.get<Record<string, unknown>>('/sales/orders', {
    params: Object.keys(params).length > 0 ? params : undefined,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    items: rawItems.map((row) => normalizeSalesOrderListItem(row)),
    total: Number(data.total ?? data.Total ?? 0),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? rawItems.length),
  };
}

export async function fetchSalesOrder(id: string): Promise<SalesOrderDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/orders/${id}`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

export type SaleLinePayload = Required<
  Pick<CreateSaleLineRequest, 'productId' | 'productUnitId' | 'quantity'>
> &
  Pick<CreateSaleLineRequest, 'batchNumber' | 'discountType' | 'discountValue'> & {
    prescriptionLineId?: string;
  };

type CreateSalePayload = Pick<
  CreateSaleRequest,
  | 'warehouseId'
  | 'customerId'
  | 'saveAsDraft'
  | 'orderDiscountType'
  | 'orderDiscountValue'
  | 'payments'
> & {
  items: SaleLinePayload[];
  loyaltyDiscountAmount?: number;
  customerVoucherId?: string;
  orderReminderLabel?: string | null;
  orderReminderDaysSupply?: number;
  prescriptionId?: string;
  connectRxHandoffId?: string;
};

export type CompleteDraftSaleOptions = CompleteDraftSaleRequest & {
  loyaltyDiscountAmount?: number;
  items?: SaleLinePayload[];
  orderReminderLabel?: string | null;
  orderReminderDaysSupply?: number;
  prescriptionId?: string;
  connectRxHandoffId?: string;
};

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

export function normalizePosCustomerVoucher(row: Record<string, unknown>): PosCustomerVoucher {
  return {
    customerVoucherId: String(row.customerVoucherId ?? row.CustomerVoucherId ?? ''),
    voucherId: String(row.voucherId ?? row.VoucherId ?? ''),
    voucherCode: String(row.voucherCode ?? row.VoucherCode ?? ''),
    voucherName: String(row.voucherName ?? row.VoucherName ?? ''),
    discountType: Number(row.discountType ?? row.DiscountType ?? 2),
    discountValue: Number(row.discountValue ?? row.DiscountValue ?? 0),
    minOrderAmount: Number(row.minOrderAmount ?? row.MinOrderAmount ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
  };
}

export async function fetchPosCustomerVouchers(customerId: string, orderTotal: number) {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/sales/pos/customer-vouchers',
    { params: { customerId, orderTotal } },
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizePosCustomerVoucher);
}

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
  options?: CompleteDraftSaleOptions,
): Promise<SalesOrderDetail> {
  const body: CompleteDraftSaleRequest = {
    payments: options?.payments ?? null,
    ...(options?.items?.length ? { items: options.items } : {}),
    ...(options && 'customerId' in options ? { customerId: options.customerId ?? null } : {}),
    ...(options?.items?.length
      ? {
          orderDiscountType: options.orderDiscountType ?? null,
          orderDiscountValue: options.orderDiscountValue ?? null,
          ...(options.notes != null ? { notes: options.notes } : {}),
        }
      : {}),
    ...(options?.loyaltyDiscountAmount != null && options.loyaltyDiscountAmount > 0
      ? { loyaltyDiscountAmount: options.loyaltyDiscountAmount }
      : {}),
    ...(options?.customerVoucherId ? { customerVoucherId: options.customerVoucherId } : {}),
    ...(options?.prescriptionId ? { prescriptionId: options.prescriptionId } : {}),
    ...(options?.connectRxHandoffId ? { connectRxHandoffId: options.connectRxHandoffId } : {}),
    ...(options?.orderReminderDaysSupply != null && options.orderReminderDaysSupply >= 1
      ? {
          orderReminderLabel: options.orderReminderLabel ?? null,
          orderReminderDaysSupply: options.orderReminderDaysSupply,
        }
      : {}),
  };
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${id}/complete`, body);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

export async function cancelDraftSale(id: string): Promise<SalesOrderDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/orders/${id}/cancel`);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return normalizeSalesOrderDetail(data, rawItems);
}

export type UpdateDraftPayload = Pick<
  UpdateDraftSaleRequest,
  'customerId' | 'orderDiscountType' | 'orderDiscountValue' | 'notes'
> & {
  priceType?: number;
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
  filters?: {
    search?: string;
    customerSearch?: string;
    documentSearch?: string;
    limit?: number;
  },
): Promise<SalesReturnListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/returns', {
    params: {
      limit: filters?.limit ?? 50,
      ...(filters?.search?.trim() ? { search: filters.search.trim() } : {}),
      ...(filters?.customerSearch?.trim() ? { customerSearch: filters.customerSearch.trim() } : {}),
      ...(filters?.documentSearch?.trim() ? { documentSearch: filters.documentSearch.trim() } : {}),
    },
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
      stockSourceLabel: String(
        row.stockSourceLabel ?? row.StockSourceLabel ?? salesT()('pos.allocation.stockSourceSystem'),
      ),
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

function normalizeReceivablesAging(row: Record<string, unknown>) {
  const aging = (row.aging ?? row.Aging ?? {}) as Record<string, unknown>;
  return {
    current: Number(aging.current ?? aging.Current ?? 0),
    days31To60: Number(aging.days31To60 ?? aging.Days31To60 ?? 0),
    days61To90: Number(aging.days61To90 ?? aging.Days61To90 ?? 0),
    over90: Number(aging.over90 ?? aging.Over90 ?? 0),
  };
}

function normalizeReceivablesRow(row: Record<string, unknown>): CustomerReceivablesRow {
  return {
    customerId: String(row.customerId ?? row.CustomerId),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null | undefined,
    totalReceivable: Number(row.totalReceivable ?? row.TotalReceivable ?? 0),
    unappliedCredit: Number(row.unappliedCredit ?? row.UnappliedCredit ?? 0),
    aging: normalizeReceivablesAging(row),
    openDocumentCount: Number(row.openDocumentCount ?? row.OpenDocumentCount ?? 0),
  };
}

export async function fetchCustomerReceivables(): Promise<CustomerReceivablesRow[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/customer-receivables');
  return data.map((row) => normalizeReceivablesRow(row));
}

export async function fetchCustomerReceivablesDetail(customerId: string): Promise<CustomerReceivablesDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-receivables/${customerId}`);
  const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map((line) => ({
    salesOrderId: String(line.salesOrderId ?? line.SalesOrderId),
    orderNumber: String(line.orderNumber ?? line.OrderNumber ?? ''),
    orderDate: String(line.orderDate ?? line.OrderDate ?? ''),
    orderTotal: Number(line.orderTotal ?? line.OrderTotal ?? 0),
    paidAmount: Number(line.paidAmount ?? line.PaidAmount ?? 0),
    outstanding: Number(line.outstanding ?? line.Outstanding ?? 0),
    daysOutstanding: Number(line.daysOutstanding ?? line.DaysOutstanding ?? 0),
  }));
  return {
    ...normalizeReceivablesRow(data),
    lines,
  };
}

function normalizeCustomerPayment(row: Record<string, unknown>): CustomerPaymentListItem {
  return {
    id: String(row.id ?? row.Id),
    paymentNumber: String(row.paymentNumber ?? row.PaymentNumber ?? ''),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    amount: Number(row.amount ?? row.Amount ?? 0),
    paymentMethod: Number(row.paymentMethod ?? row.PaymentMethod ?? 1),
    status: Number(row.status ?? row.Status ?? 1),
    paymentDate: String(row.paymentDate ?? row.PaymentDate ?? ''),
    postedAt: (row.postedAt ?? row.PostedAt) as string | undefined,
    salesOrderId: (row.salesOrderId ?? row.SalesOrderId) as string | undefined,
    orderNumber: (row.orderNumber ?? row.OrderNumber) as string | undefined,
    notes: (row.notes ?? row.Notes) as string | undefined,
  };
}

function customerPaymentListParams(filters?: CustomerPaymentListFilters) {
  if (!filters) return undefined;
  const params: Record<string, string | number> = {};
  if (filters.search?.trim()) params.search = filters.search.trim();
  if (filters.customerSearch?.trim()) params.customerSearch = filters.customerSearch.trim();
  if (filters.documentSearch?.trim()) params.documentSearch = filters.documentSearch.trim();
  if (filters.customerId) params.customerId = filters.customerId;
  if (filters.status != null) params.status = filters.status;
  if (filters.dateFrom) params.dateFrom = filters.dateFrom;
  if (filters.dateTo) params.dateTo = filters.dateTo;
  return Object.keys(params).length > 0 ? params : undefined;
}

function customerPaymentPayload(payload: {
  customerId: string;
  salesOrderId?: string;
  amount: number;
  paymentMethod: number;
  notes?: string;
  paymentDate?: string;
}) {
  return {
    customerId: payload.customerId,
    salesOrderId: payload.salesOrderId ?? null,
    amount: payload.amount,
    paymentMethod: payload.paymentMethod,
    notes: payload.notes?.trim() ? payload.notes.trim() : null,
    paymentDate: payload.paymentDate || null,
  };
}

export async function fetchCustomerPayments(
  filters?: CustomerPaymentListFilters,
): Promise<CustomerPaymentListItem[]> {
  const { data } = await http.get<unknown>('/sales/customer-payments', {
    params: customerPaymentListParams(filters),
  });
  const rows = Array.isArray(data) ? data : [];
  return rows.map((row) => normalizeCustomerPayment(row as Record<string, unknown>));
}

export async function fetchCustomerPayment(id: string): Promise<CustomerPaymentListItem> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-payments/${id}`);
  return normalizeCustomerPayment(data);
}

export async function createCustomerPayment(payload: {
  customerId: string;
  salesOrderId?: string;
  amount: number;
  paymentMethod: number;
  notes?: string;
  paymentDate?: string;
}): Promise<CustomerPaymentListItem> {
  const { data } = await http.post<Record<string, unknown>>('/sales/customer-payments', customerPaymentPayload(payload));
  return normalizeCustomerPayment(data);
}

export async function updateCustomerPayment(
  id: string,
  payload: {
    customerId: string;
    salesOrderId?: string;
    amount: number;
    paymentMethod: number;
    notes?: string;
    paymentDate?: string;
  },
): Promise<CustomerPaymentListItem> {
  const { data } = await http.put<Record<string, unknown>>(`/sales/customer-payments/${id}`, customerPaymentPayload(payload));
  return normalizeCustomerPayment(data);
}

export async function postCustomerPayment(id: string): Promise<CustomerPaymentListItem> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-payments/${id}/post`);
  return normalizeCustomerPayment(data);
}

export async function cancelCustomerPayment(id: string): Promise<CustomerPaymentListItem> {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-payments/${id}/cancel`);
  return normalizeCustomerPayment(data);
}
