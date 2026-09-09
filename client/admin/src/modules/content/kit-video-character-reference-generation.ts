export const CHARACTER_REFERENCE_GENERATION_ID = 'FAMIXA_CHARACTER_REFERENCE_GENERATION_V1';

export const REFERENCE_GENERATION_PROVIDERS = [
  { id: '', label: 'Chưa chọn' },
  { id: 'GEMINI', label: 'Gemini' },
] as const;

export const STAFF_CREATE_REFERENCE = 'Tạo ảnh tham chiếu';
export const STAFF_CREATE_FULL_BODY = 'TẠO ẢNH TOÀN THÂN';
export const STAFF_UPLOAD_EXISTING = 'UPLOAD ẢNH CÓ SẴN';
export const STAFF_START = 'BẮT ĐẦU TẠO';
export const STAFF_CANCEL = 'HỦY';
export const STAFF_ACCEPT = 'CHẤP NHẬN';
export const STAFF_REJECT = 'TỪ CHỐI';
export const STAFF_WAITING = 'Đang chờ Director kiểm tra.';
export const STAFF_CREATED = 'Ảnh tham chiếu đã được tạo.';
export const STAFF_DETAILS = 'Chi tiết sản xuất';

export const STAFF_REGENERATE_FULL_BODY = 'TẠO LẠI ẢNH TOÀN THÂN';
export const STAFF_REGENERATE = 'Tạo lại bằng Famixa';

export function generateCta(type?: string, replaceExisting = false) {
  if (replaceExisting) {
    return (type || '').toUpperCase() === 'FULL_BODY' ? STAFF_REGENERATE_FULL_BODY : 'TẠO LẠI ẢNH THAM CHIẾU';
  }
  return (type || '').toUpperCase() === 'FULL_BODY' ? STAFF_CREATE_FULL_BODY : 'TẠO ẢNH THAM CHIẾU';
}

export function angleLabel(type?: string) {
  switch ((type || '').toUpperCase()) {
    case 'FRONT':
      return 'Trước mặt';
    case 'THREE_QUARTER':
      return 'Góc 3/4';
    case 'SIDE':
      return 'Nghiêng';
    case 'FULL_BODY':
      return 'Toàn thân';
    default:
      return type || 'Góc ảnh';
  }
}

export function providerLabel(id?: string | null) {
  if (!id) return 'Chưa chọn';
  return id.toUpperCase() === 'GEMINI' ? 'Gemini' : id;
}

export function confirmCopy(input: { character: string; angle: string; provider: string }) {
  return {
    title: 'TẠO ẢNH THAM CHIẾU',
    character: input.character,
    angle: input.angle,
    source: 'Character Canon',
    provider: input.provider,
    willBecome: 'Ứng viên bộ ảnh chuẩn',
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}

export function reviewCopy(status?: string | null) {
  const s = (status || '').toUpperCase();
  if (s === 'ACCEPTED' || s === 'REGISTERED') return 'ĐÃ CHẤP NHẬN';
  if (s === 'REJECTED') return 'ĐÃ TỪ CHỐI';
  return 'CHỜ DUYỆT';
}

export function canOfferGenerate(input: { missing: boolean; provider: string; slotState?: string }) {
  return input.missing && input.provider === 'GEMINI' && input.slotState !== 'READY_FOR_DIRECTOR';
}
