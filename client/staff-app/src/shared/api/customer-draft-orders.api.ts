import { http } from '@/shared/api/http';

export interface CustomerDraftOrderListItem {
  id: string;
  draftNumber: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  status: number;
  totalAmount: number;
  itemCount: number;
  sentAt?: string | null;
  confirmedAt?: string | null;
  expiresAt?: string | null;
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

function normalizeListItem(row: Record<string, unknown>): CustomerDraftOrderListItem {
  return {
    id: String(row.id ?? row.Id),
    draftNumber: String(row.draftNumber ?? row.DraftNumber ?? ''),
    customerId: String(row.customerId ?? row.CustomerId),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    sentAt: (row.sentAt ?? row.SentAt) as string | null | undefined,
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    expiresAt: (row.expiresAt ?? row.ExpiresAt) as string | null | undefined,
  };
}

export async function fetchCustomerDraftOrders(status?: number[]) {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/sales/customer-draft-orders',
    { params: status?.length ? { status } : undefined },
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeListItem);
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

export async function cancelCustomerDraftOrder(id: string): Promise<void> {
  await http.post(`/sales/customer-draft-orders/${id}/cancel`);
}
