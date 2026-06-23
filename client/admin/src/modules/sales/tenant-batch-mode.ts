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
  label_optional: 'Cho phép ghi nhận nhãn lô khi cần (sẽ mở rộng trên POS).',
  label_required: 'Bắt buộc nhập nhãn lô trước khi bán (sẽ mở rộng trên POS).',
};
