import { isAxiosError } from 'axios';
import { fetchOpenShift } from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { SalesShiftDetail } from '@/shared/api/sales.types';

export function isShiftAlreadyOpenError(error: unknown): boolean {
  if (!isAxiosError(error)) return false;
  const msg = apiErrorMessage(error, '').toLowerCase();
  return msg.includes('ca mở') || msg.includes('ca mo');
}

/** Tải ca đang mở; null = chưa có ca. */
export async function loadOpenShiftForWarehouse(warehouseId: string): Promise<SalesShiftDetail | null> {
  return fetchOpenShift(warehouseId);
}

export function shiftAlreadyOpenMessage(shift: SalesShiftDetail): string {
  return `Kho đã có ca ${shift.shiftNumber} đang mở — có thể tiếp tục bán hàng.`;
}
