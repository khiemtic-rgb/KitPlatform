import { http } from '@/shared/api/http';

export const CUSTOMER_RESERVATION_STATUS = {
  Pending: 1,
  Confirmed: 2,
  Ready: 3,
  Collected: 4,
  Cancelled: 5,
  Rejected: 6,
} as const;

export const CUSTOMER_RESERVATION_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ xác nhận',
  2: 'Đã xác nhận',
  3: 'Sẵn sàng lấy thuốc',
  4: 'Đã lấy thuốc',
  5: 'Đã hủy',
  6: 'Từ chối',
};

export const CUSTOMER_RESERVATION_STATUS_COLORS: Record<number, string> = {
  1: 'gold',
  2: 'blue',
  3: 'green',
  4: 'success',
  5: 'default',
  6: 'error',
};

export const CUSTOMER_RESERVATION_FULFILLMENT_LABELS: Record<number, string> = {
  1: 'Đến quầy lấy',
  2: 'Giao tận nơi',
};

export interface CustomerReservationLine {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  customerNote?: string | null;
}

export interface CustomerReservation {
  id: string;
  reservationNumber: string;
  status: number;
  fulfillmentType: number;
  addressId?: string | null;
  addressSummary?: string | null;
  notes?: string | null;
  staffNotes?: string | null;
  submittedAt: string;
  confirmedAt?: string | null;
  readyAt?: string | null;
  collectedAt?: string | null;
  salesOrderId?: string | null;
  salesOrderNumber?: string | null;
  items: CustomerReservationLine[];
}

export interface CustomerReservationPosLoad {
  reservationId: string;
  reservationNumber: string;
  customerId: string;
  warehouseId: string;
  notes?: string | null;
  lines: Array<{
    productId: string;
    productCode: string;
    productName: string;
    productUnitId: string;
    unitName: string;
    quantity: number;
    customerNote?: string | null;
  }>;
}

export interface CustomerReservationStaffListItem {
  id: string;
  reservationNumber: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  status: number;
  fulfillmentType: number;
  itemCount: number;
  submittedAt: string;
  readyAt?: string | null;
}

function normalizeLine(row: Record<string, unknown>): CustomerReservationLine {
  return {
    id: String(row.id ?? row.Id),
    lineNumber: Number(row.lineNumber ?? row.LineNumber ?? 0),
    productId: String(row.productId ?? row.ProductId ?? ''),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    customerNote: (row.customerNote ?? row.CustomerNote) as string | null | undefined,
  };
}

function normalizeDetail(row: Record<string, unknown>): CustomerReservation {
  return {
    id: String(row.id ?? row.Id),
    reservationNumber: String(row.reservationNumber ?? row.ReservationNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 0),
    fulfillmentType: Number(row.fulfillmentType ?? row.FulfillmentType ?? 1),
    addressId: (row.addressId ?? row.AddressId) as string | null | undefined,
    addressSummary: (row.addressSummary ?? row.AddressSummary) as string | null | undefined,
    notes: (row.notes ?? row.Notes) as string | null | undefined,
    staffNotes: (row.staffNotes ?? row.StaffNotes) as string | null | undefined,
    submittedAt: String(row.submittedAt ?? row.SubmittedAt ?? ''),
    confirmedAt: (row.confirmedAt ?? row.ConfirmedAt) as string | null | undefined,
    readyAt: (row.readyAt ?? row.ReadyAt) as string | null | undefined,
    collectedAt: (row.collectedAt ?? row.CollectedAt) as string | null | undefined,
    salesOrderId: (row.salesOrderId ?? row.SalesOrderId) as string | null | undefined,
    salesOrderNumber: (row.salesOrderNumber ?? row.SalesOrderNumber) as string | null | undefined,
    items: ((row.items ?? row.Items ?? []) as Record<string, unknown>[]).map(normalizeLine),
  };
}

function normalizeListItem(row: Record<string, unknown>): CustomerReservationStaffListItem {
  return {
    id: String(row.id ?? row.Id),
    reservationNumber: String(row.reservationNumber ?? row.ReservationNumber ?? ''),
    customerId: String(row.customerId ?? row.CustomerId ?? ''),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null | undefined,
    status: Number(row.status ?? row.Status ?? 0),
    fulfillmentType: Number(row.fulfillmentType ?? row.FulfillmentType ?? 1),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    submittedAt: String(row.submittedAt ?? row.SubmittedAt ?? ''),
    readyAt: (row.readyAt ?? row.ReadyAt) as string | null | undefined,
  };
}

export async function fetchCustomerReservations(status?: number[]) {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/sales/customer-reservations',
    { params: status?.length ? { status } : undefined },
  );
  const rows = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return rows.map(normalizeListItem);
}

export async function fetchCustomerReservation(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-reservations/${id}`);
  return normalizeDetail(data);
}

export async function confirmCustomerReservation(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-reservations/${id}/confirm`);
  return normalizeDetail(data);
}

export async function rejectCustomerReservation(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-reservations/${id}/reject`);
  return normalizeDetail(data);
}

export async function markCustomerReservationReady(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-reservations/${id}/ready`);
  return normalizeDetail(data);
}

export async function markCustomerReservationCollected(id: string) {
  const { data } = await http.post<Record<string, unknown>>(`/sales/customer-reservations/${id}/collected`);
  return normalizeDetail(data);
}

export async function updateCustomerReservationStaffNotes(id: string, staffNotes?: string) {
  const { data } = await http.put<Record<string, unknown>>(`/sales/customer-reservations/${id}/staff-notes`, {
    staffNotes,
  });
  return normalizeDetail(data);
}

export async function loadCustomerReservationForPos(id: string) {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-reservations/${id}/pos-load`);
  const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map((line) => ({
    productId: String(line.productId ?? line.ProductId ?? ''),
    productCode: String(line.productCode ?? line.ProductCode ?? ''),
    productName: String(line.productName ?? line.ProductName ?? ''),
    productUnitId: String(line.productUnitId ?? line.ProductUnitId ?? ''),
    unitName: String(line.unitName ?? line.UnitName ?? ''),
    quantity: Number(line.quantity ?? line.Quantity ?? 0),
    customerNote: (line.customerNote ?? line.CustomerNote) as string | null | undefined,
  }));
  return {
    reservationId: String(data.reservationId ?? data.ReservationId ?? id),
    reservationNumber: String(data.reservationNumber ?? data.ReservationNumber ?? ''),
    customerId: String(data.customerId ?? data.CustomerId ?? ''),
    warehouseId: String(data.warehouseId ?? data.WarehouseId ?? ''),
    notes: (data.notes ?? data.Notes) as string | null | undefined,
    lines,
  } satisfies CustomerReservationPosLoad;
}

export async function linkCustomerReservationSale(reservationId: string, salesOrderId: string) {
  await http.post(`/sales/customer-reservations/${reservationId}/link-sale`, { salesOrderId });
}
