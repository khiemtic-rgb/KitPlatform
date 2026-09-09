export const CHARACTER_REFERENCE_REGENERATION_V1_ID = 'FAMIXA_CHARACTER_REFERENCE_REGENERATION_V1';

export const REGEN_PROVIDERS = [
  { id: '', label: 'Chưa chọn', available: false },
  { id: 'GEMINI', label: 'Gemini', available: true },
  { id: 'RUNWAY', label: 'Runway', available: false },
  { id: 'VEO', label: 'Veo', available: false },
] as const;

export const REJECT_REASON_CODES = [
  { id: 'FACE', label: 'Không giống khuôn mặt' },
  { id: 'HAIR', label: 'Sai kiểu tóc' },
  { id: 'PROPORTION', label: 'Sai tỷ lệ cơ thể' },
  { id: 'AGE', label: 'Sai tuổi' },
  { id: 'WARDROBE', label: 'Sai trang phục' },
  { id: 'INCONSISTENT', label: 'Các góc không đồng nhất' },
  { id: 'FULL_BODY', label: 'Toàn thân không đúng' },
  { id: 'QUALITY', label: 'Hình ảnh không đạt chất lượng' },
  { id: 'OTHER', label: 'Khác' },
] as const;

export const STAFF_REJECT = 'Không đạt';
export const STAFF_REJECT_TITLE = 'Đánh giá bộ ảnh';
export const STAFF_REJECT_REASON = 'Lý do không đạt';
export const STAFF_REJECT_CONFIRM = 'Xác nhận không đạt';
export const STAFF_REGENERATE = 'Tạo lại bộ ảnh';
export const STAFF_START = 'Bắt đầu tạo lại';
export const STAFF_CANCEL = 'Hủy';
export const STAFF_REJECTED = 'Không đạt';
export const STAFF_PENDING = 'Đang chờ duyệt';
export const STAFF_HISTORY = 'Lịch sử bộ ảnh';

export function regenProviderLabel(id?: string | null) {
  return REGEN_PROVIDERS.find((x) => x.id === (id || ''))?.label || 'Chưa chọn';
}

export function isRejectedStatus(status?: string | null) {
  const s = (status || '').toUpperCase();
  return s === 'REJECTED' || s === 'CRP_REJECTED';
}

export function isPendingReviewStatus(status?: string | null) {
  const s = (status || '').toUpperCase();
  return s === 'READY_FOR_DIRECTOR' || s === 'CRP_PENDING_REVIEW' || s === 'PENDING_REVIEW' || s === 'VALIDATED' || s === 'REVIEW';
}

export function rejectedSetLabel(ready: number, total = 4) {
  return `Bộ ảnh chuẩn: ${ready}/${total} · ${STAFF_REJECTED}`;
}

export function rejectReasonValid(text?: string | null) {
  return (text || '').trim().length >= 3;
}

export function regenerateConfirmCopy(provider: string) {
  return {
    title: STAFF_REGENERATE,
    body: [
      'Bộ ảnh tham chiếu hiện tại đã bị đánh giá Không đạt.',
      '',
      'Famixa sẽ tạo một bộ ảnh tham chiếu mới gồm 4 góc:',
      '• Trước mặt',
      '• 3/4',
      '• Nghiêng',
      '• Toàn thân',
      '',
      'Nhân vật sẽ được giữ theo Master + DNA + PRP hiện tại.',
      '',
      `Provider:`,
      provider,
      '',
      'Bộ ảnh cũ sẽ được giữ lại trong lịch sử và không bị ghi đè.',
      '',
      'Bạn có muốn tạo lại không?',
    ].join('\n'),
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}
