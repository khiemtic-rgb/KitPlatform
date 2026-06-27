import { http } from '@/shared/api/http';

export interface CustomerDraftOrderLineInput {
  productId: string;
  productUnitId: string;
  quantity: number;
  discountType?: number;
  discountValue?: number;
  dosageNote?: string;
}

export interface CustomerDraftOrderLine {
  id: string;
  lineNumber: number;
  productId: string;
  productUnitId: string;
  productCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  lineDiscountType?: number;
  lineDiscountValue?: number;
  lineAmount: number;
  dosageNote?: string | null;
}

export interface CustomerDraftOrder {
  id: string;
  draftNumber: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  chatThreadId?: string | null;
  warehouseId: string;
  status: number;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  orderDiscountType?: number;
  orderDiscountValue?: number;
  notes?: string | null;
  sentAt?: string | null;
  confirmedAt?: string | null;
  completedAt?: string | null;
  expiresAt?: string | null;
  salesOrderId?: string | null;
  salesOrderNumber?: string | null;
  items: CustomerDraftOrderLine[];
}

export interface CustomerDraftOrderListItem {
  id: string;
  draftNumber: string;
  customerId: string;
  customerName: string;
  status: number;
  totalAmount: number;
  itemCount: number;
  sentAt?: string | null;
  confirmedAt?: string | null;
  expiresAt?: string | null;
  hiddenByCustomerAt?: string | null;
}

export interface CustomerDraftOrderPosLoad {
  draftOrderId: string;
  draftNumber: string;
  customerId: string;
  warehouseId: string;
  orderDiscountType?: number;
  orderDiscountValue?: number;
  notes?: string | null;
  lines: Array<{
    productId: string;
    productCode: string;
    productName: string;
    productUnitId: string;
    unitName: string;
    quantity: number;
    unitPrice: number;
    discountType?: number;
    discountValue?: number;
    dosageNote?: string | null;
  }>;
}

export const CUSTOMER_DRAFT_ORDER_STATUS = {
  Draft: 1,
  Sent: 2,
  Confirmed: 3,
  Completed: 4,
  Cancelled: 5,
  Expired: 6,
} as const;

export const CUSTOMER_DRAFT_ORDER_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
  2: 'Đã gửi khách',
  3: 'Khách đã xác nhận',
  4: 'Đã bán',
  5: 'Đã hủy',
  6: 'Hết hạn',
};

export const CUSTOMER_DRAFT_ORDER_STATUS_COLORS: Record<number, string> = {
  1: 'default',
  2: 'processing',
  3: 'success',
  4: 'green',
  5: 'error',
  6: 'warning',
};

export const CUSTOMER_DRAFT_ORDER_STATUS_FILTER_OPTIONS = [
  { value: CUSTOMER_DRAFT_ORDER_STATUS.Draft, label: CUSTOMER_DRAFT_ORDER_STATUS_LABELS[1] },
  { value: CUSTOMER_DRAFT_ORDER_STATUS.Sent, label: CUSTOMER_DRAFT_ORDER_STATUS_LABELS[2] },
  { value: CUSTOMER_DRAFT_ORDER_STATUS.Confirmed, label: CUSTOMER_DRAFT_ORDER_STATUS_LABELS[3] },
  { value: CUSTOMER_DRAFT_ORDER_STATUS.Completed, label: CUSTOMER_DRAFT_ORDER_STATUS_LABELS[4] },
  { value: CUSTOMER_DRAFT_ORDER_STATUS.Cancelled, label: CUSTOMER_DRAFT_ORDER_STATUS_LABELS[5] },
  { value: CUSTOMER_DRAFT_ORDER_STATUS.Expired, label: CUSTOMER_DRAFT_ORDER_STATUS_LABELS[6] },
];

function normalizeLine(row: Record<string, unknown>): CustomerDraftOrderLine {
  return {
    id: String(row.id ?? row.Id),
    lineNumber: Number(row.lineNumber ?? row.LineNumber ?? 0),
    productId: String(row.productId ?? row.ProductId),
    productUnitId: String(row.productUnitId ?? row.ProductUnitId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    lineDiscountType: (row.lineDiscountType ?? row.LineDiscountType) as number | undefined,
    lineDiscountValue: (row.lineDiscountValue ?? row.LineDiscountValue) as number | undefined,
    lineAmount: Number(row.lineAmount ?? row.LineAmount ?? 0),
    dosageNote: (row.dosageNote ?? row.DosageNote) as string | null | undefined,
  };
}

function normalizeOrder(row: Record<string, unknown>): CustomerDraftOrder {
  const items = ((row.items ?? row.Items ?? []) as Record<string, unknown>[]).map(normalizeLine);
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null | undefined,
    chatThreadId: (row.chatThreadId ?? row.ChatThreadId) as string | null | undefined,
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    status: Number(row.status ?? row.Status ?? 1),
    subtotal: Number(row.subtotal ?? row.Subtotal ?? 0),
    discountAmount: Number(row.discountAmount ?? row.DiscountAmount ?? 0),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    orderDiscountType: (row.orderDiscountType ?? row.OrderDiscountType) as number | undefined,
    orderDiscountValue: (row.orderDiscountValue ?? row.OrderDiscountValue) as number | undefined,
    notes: (row.notes ?? row.Notes) as string | null | undefined,
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    completedAt: (row.completedAt ?? row.CompletedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
    salesOrderId: (row.salesOrderId ?? row.SalesOrderId) as string | null | undefined,
    salesOrderNumber: (row.salesOrderNumber ?? row.SalesOrderNumber) as string | null | undefined,
    items,
  };
}

function normalizeListItem(row: Record<string, unknown>): CustomerDraftOrderListItem {
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
    hiddenByCustomerAt: (row.hiddenByCustomerAt ?? row.HiddenByCustomerAt) as string | null | undefined,
  };
}

export async function fetchCustomerDraftOrders(customerId?: string, status?: number[]) {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/sales/customer-draft-orders',
    { params: { customerId, status } },
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeListItem);
}

export async function fetchCustomerDraftOrder(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-draft-orders/${id}`);
  return normalizeOrder(data);
}

export async function createCustomerDraftOrder(payload: {
  customerId: string;
  chatThreadId?: string;
  warehouseId?: string;
  priceType?: number;
  items: CustomerDraftOrderLineInput[];
  orderDiscountType?: number;
  orderDiscountValue?: number;
  notes?: string;
}) {
  const { data } = await http.post<Record<string, unknown>>('/sales/customer-draft-orders', payload);
  return normalizeOrder(data);
}

export async function updateCustomerDraftOrder(
  id: string,
  payload: {
    customerId: string;
    chatThreadId?: string;
    warehouseId?: string;
    priceType?: number;
    items: CustomerDraftOrderLineInput[];
    orderDiscountType?: number;
    orderDiscountValue?: number;
    notes?: string;
  },
) {
  const { data } = await http.put<Record<string, unknown>>(`/sales/customer-draft-orders/${id}`, payload);
  return normalizeOrder(data);
}

export async function sendCustomerDraftOrder(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-draft-orders/${id}/send`);
  return normalizeOrder(data);
}

export async function cancelCustomerDraftOrder(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-draft-orders/${id}/cancel`);
  return normalizeOrder(data);
}

export async function loadCustomerDraftOrderForPos(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-draft-orders/${id}/pos-load`);
  const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map((line) => ({
    productId: String(line.productId ?? line.ProductId),
    productCode: String(line.productCode ?? line.ProductCode ?? ''),
    productName: String(line.productName ?? line.ProductName ?? ''),
    productUnitId: String(line.productUnitId ?? line.ProductUnitId),
    unitName: String(line.unitName ?? line.UnitName ?? ''),
    quantity: Number(line.quantity ?? line.Quantity ?? 0),
    unitPrice: Number(line.unitPrice ?? line.UnitPrice ?? 0),
    discountType: (line.discountType ?? line.DiscountType) as number | undefined,
    discountValue: (line.discountValue ?? line.DiscountValue) as number | undefined,
    dosageNote: (line.dosageNote ?? line.DosageNote) as string | null | undefined,
  }));
  return {
    draftOrderId: String(data.draftOrderId ?? data.DraftOrderId),
    draftNumber: String(data.draftNumber ?? data.DraftNumber ?? ''),
    customerId: String(data.customerId ?? data.CustomerId),
    warehouseId: String(data.warehouseId ?? data.WarehouseId),
    orderDiscountType: (data.orderDiscountType ?? data.OrderDiscountType) as number | undefined,
    orderDiscountValue: (data.orderDiscountValue ?? data.OrderDiscountValue) as number | undefined,
    notes: (data.notes ?? data.Notes) as string | null | undefined,
    lines,
  } satisfies CustomerDraftOrderPosLoad;
}

export async function linkCustomerDraftOrderSale(draftOrderId: string, salesOrderId: string) {
  await http.post(`/sales/customer-draft-orders/${draftOrderId}/link-sale`, { salesOrderId });
}
