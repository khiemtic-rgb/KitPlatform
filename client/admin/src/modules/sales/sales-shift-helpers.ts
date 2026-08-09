import { isAxiosError } from 'axios';
import { fetchOpenShift, fetchSalesShift, fetchSalesShifts } from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { SalesShiftDetail } from '@/shared/api/sales.types';
import { SALES_SHIFT_STATUSES } from '@/shared/api/sales.types';
import { salesT } from '@/shared/i18n';

export function isShiftAlreadyOpenError(error: unknown): boolean {
  if (!isAxiosError(error)) return false;
  const msg = apiErrorMessage(error, '').toLowerCase();
  return (
    msg.includes('ca mở')
    || msg.includes('ca mo')
    || msg.includes('đang có ca')
    || msg.includes('dang co ca')
    || (msg.includes('shift') && msg.includes('open'))
  );
}

/** Tải ca đang mở; null = chưa có ca. */
export async function loadOpenShiftForWarehouse(warehouseId: string): Promise<SalesShiftDetail | null> {
  return resolveOpenShiftForWarehouse(warehouseId);
}

/** Ưu tiên /current; fallback danh sách ca nếu endpoint current lỗi/thiếu. */
export async function resolveOpenShiftForWarehouse(warehouseId: string): Promise<SalesShiftDetail | null> {
  try {
    const current = await fetchOpenShift(warehouseId);
    if (current) return current;
  } catch {
    /* thử fallback */
  }

  try {
    const items = await fetchSalesShifts({
      limit: 50,
      warehouseId,
      status: SALES_SHIFT_STATUSES.Open,
    });
    const openItem = items.find(
      (row) => row.warehouseId === warehouseId && row.status === SALES_SHIFT_STATUSES.Open,
    );
    if (openItem) return fetchSalesShift(openItem.id);
  } catch {
    return null;
  }

  return null;
}

export function shiftAlreadyOpenMessage(shift: SalesShiftDetail): string {
  return salesT()('pos.shift.alreadyOpen', { number: shift.shiftNumber });
}
