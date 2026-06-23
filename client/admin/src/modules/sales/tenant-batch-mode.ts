import type { TenantBatchModeValue } from '@/shared/api/sales.api';

export const BATCH_MODE_LABELS: Record<TenantBatchModeValue, string> = {
  off: 'Tắt — không gợi ý lô trên POS',
  suggest: 'Gợi ý lô FEFO (mặc định)',
  label_optional: 'Nhãn lô — tùy chọn',
  label_required: 'Nhãn lô — bắt buộc',
};

export const BATCH_MODE_OPTIONS = (
  Object.entries(BATCH_MODE_LABELS) as [TenantBatchModeValue, string][]
).map(([value, label]) => ({ value, label }));

export const BATCH_MODE_HINTS: Record<TenantBatchModeValue, string> = {
  off: 'POS không hiển thị gợi ý lô; backend vẫn xuất FEFO khi chốt đơn.',
  suggest: 'Nhân viên thấy lô FEFO gợi ý (tooltip) khi bán; khách không thấy lô trên hóa đơn.',
  label_optional: 'POS hiển thị cột chọn lô; có thể bỏ trống nếu không cần (không bật cảnh báo FEFO ca).',
  label_required:
    'POS bắt buộc chọn số lô khớp tồn kho; bật cảnh báo tuân thủ FEFO trên ca làm việc (FEFO chuẩn).',
};

/** Cảnh báo lô trên ca — chỉ khi cài nhãn lô bắt buộc. */
export function enablesShiftFefoLotAlerts(mode: TenantBatchModeValue): boolean {
  return mode === 'label_required';
}
