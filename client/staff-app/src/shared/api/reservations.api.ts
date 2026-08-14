import { http } from '@/shared/api/http';

export const RESERVATION_STATUS = {
  Pending: 1,
  Confirmed: 2,
  Ready: 3,
  Collected: 4,
  Cancelled: 5,
  Rejected: 6,
} as const;

export const RESERVATION_STATUS_LABEL: Record<number, string> = {
  1: 'Chờ xác nhận',
  2: 'Đã xác nhận',
  3: 'Sẵn sàng lấy',
  4: 'Đã lấy',
  5: 'Đã hủy',
  6: 'Từ chối',
};

export const RESERVATION_STATUS_COLOR: Record<number, string> = {
  1: 'gold',
  2: 'blue',
  3: 'green',
  4: 'default',
  5: 'default',
  6: 'error',
};

export const RESERVATION_FULFILLMENT = {
  Pickup: 1,
  Delivery: 2,
} as const;

export const RESERVATION_FULFILLMENT_LABEL: Record<number, string> = {
  1: 'Đến lấy',
  2: 'Giao hàng',
};

export interface ReservationListItem {
  id: string;
  reservationNumber: string;
  customerId: string;
  status: number;
  fulfillmentType: number;
  customerName: string;
  customerPhone: string | null;
  itemCount: number;
  submittedAt: string;
  readyAt?: string | null;
}

export interface ReservationPosLoad {
  reservationId: string;
  reservationNumber: string;
  customerId: string;
  warehouseId: string;
  lines: {
    productId: string;
    productCode: string;
    productName: string;
    productUnitId: string;
    unitName: string;
    quantity: number;
  }[];
}

function normalizeListItem(row: Record<string, unknown>): ReservationListItem {
  return {
    id: String(row.id ?? row.Id),
    reservationNumber: String(row.reservationNumber ?? row.ReservationNumber ?? ''),
    customerId: String(row.customerId ?? row.CustomerId ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    fulfillmentType: Number(row.fulfillmentType ?? row.FulfillmentType ?? 1),
    customerName: String(row.customerName ?? row.CustomerName ?? ''),
    customerPhone: (row.customerPhone ?? row.CustomerPhone) as string | null,
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
    submittedAt: String(
      row.submittedAt ?? row.SubmittedAt ?? row.requestedAt ?? row.RequestedAt ?? '',
    ),
    readyAt: (row.readyAt ?? row.ReadyAt) as string | null | undefined,
  };
}

export async function fetchReservations(status?: number[]): Promise<ReservationListItem[]> {
  const { data } = await http.get<{ items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }>(
    '/sales/customer-reservations',
    { params: status?.length ? { status } : undefined },
  );
  const rows = data.items ?? data.Items ?? [];
  return rows.map(normalizeListItem);
}

export async function loadReservationForPos(id: string): Promise<ReservationPosLoad> {
  const { data } = await http.get<Record<string, unknown>>(`/sales/customer-reservations/${id}/pos-load`);
  const lines = ((data.lines ?? data.Lines ?? []) as Record<string, unknown>[]).map((line) => ({
    productId: String(line.productId ?? line.ProductId ?? ''),
    productCode: String(line.productCode ?? line.ProductCode ?? ''),
    productName: String(line.productName ?? line.ProductName ?? ''),
    productUnitId: String(line.productUnitId ?? line.ProductUnitId ?? ''),
    unitName: String(line.unitName ?? line.UnitName ?? ''),
    quantity: Number(line.quantity ?? line.Quantity ?? 0),
  }));
  return {
    reservationId: String(data.reservationId ?? data.ReservationId ?? id),
    reservationNumber: String(data.reservationNumber ?? data.ReservationNumber ?? ''),
    customerId: String(data.customerId ?? data.CustomerId ?? ''),
    warehouseId: String(data.warehouseId ?? data.WarehouseId ?? ''),
    lines,
  };
}

export async function confirmReservation(id: string): Promise<void> {
  await http.post(`/sales/customer-reservations/${id}/confirm`);
}

export async function markReservationReady(id: string): Promise<void> {
  await http.post(`/sales/customer-reservations/${id}/ready`);
}

export async function markReservationCollected(id: string): Promise<void> {
  await http.post(`/sales/customer-reservations/${id}/collected`);
}

export async function rejectReservation(id: string): Promise<void> {
  await http.post(`/sales/customer-reservations/${id}/reject`);
}

export async function updateReservationStaffNotes(id: string, staffNotes?: string): Promise<void> {
  await http.put(`/sales/customer-reservations/${id}/staff-notes`, { staffNotes });
}

export async function linkReservationSale(reservationId: string, salesOrderId: string): Promise<void> {
  await http.post(`/sales/customer-reservations/${reservationId}/link-sale`, { salesOrderId });
}

export function countActiveReservations(items: ReservationListItem[]): number {
  return items.filter((i) =>
    [RESERVATION_STATUS.Pending, RESERVATION_STATUS.Confirmed, RESERVATION_STATUS.Ready].includes(
      i.status as 1 | 2 | 3,
    ),
  ).length;
}
