import type { TenantBatchModeValue } from '@/shared/api/sales.api';

/** Cảnh báo lô trên ca — chỉ khi cài nhãn lô bắt buộc. */
export function enablesShiftFefoLotAlerts(mode: TenantBatchModeValue): boolean {
  return mode === 'label_required';
}
