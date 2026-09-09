import type { CharacterReferencePackGetRow, CharacterReferencePackRow } from '@/shared/api/content.api';

export const CHARACTER_REFERENCE_PACK_ID = 'CHARACTER_REFERENCE_PACK_V1';

export const REQUIRED_REFERENCE_TYPES = ['FRONT', 'THREE_QUARTER', 'SIDE', 'FULL_BODY'] as const;
export const OPTIONAL_REFERENCE_TYPES = [
  'NEUTRAL',
  'HAPPY',
  'SAD',
  'WORRIED',
  'SURPRISED',
  'ANGRY',
  'THINKING',
] as const;

export const IDENTITY_LABELS: Record<string, string> = {
  face: 'Khuôn mặt',
  eyes: 'Mắt',
  hair: 'Tóc',
  age: 'Độ tuổi',
  expression: 'Cảm xúc',
  proportion: 'Tỷ lệ',
  style: 'Phong cách',
};

export function viewLabel(type: string) {
  switch ((type || '').toUpperCase()) {
    case 'FRONT':
      return 'Trước mặt';
    case 'THREE_QUARTER':
      return '3/4';
    case 'SIDE':
      return 'Nghiêng';
    case 'FULL_BODY':
      return 'Toàn thân';
    case 'NEUTRAL':
      return 'Bình thường';
    case 'HAPPY':
      return 'Vui';
    case 'SAD':
      return 'Buồn';
    case 'WORRIED':
      return 'Lo';
    case 'SURPRISED':
      return 'Ngạc nhiên';
    case 'ANGRY':
      return 'Tức';
    case 'THINKING':
      return 'Suy nghĩ';
    default:
      return type;
  }
}

export function lockMark(locked?: boolean) {
  return locked ? '✓ Đã khóa' : '○ Chưa khóa';
}

export function referenceStatusLabel(row?: CharacterReferencePackGetRow | null) {
  const pack = row?.pack;
  if (pack?.productionReady || pack?.status === 'LOCKED') return '✓ Sẵn sàng production';
  if (pack?.status === 'APPROVED' || pack?.status === 'DIRECTOR_APPROVED') return 'Đã duyệt — chờ khóa';
  if (pack?.status === 'VALIDATED' || pack?.status === 'REVIEW') return 'Đã kiểm tra';
  if (pack?.status === 'SUPERSEDED') return 'Đã thay thế';
  if (pack?.status === 'REJECTED') return 'Không đạt';
  if (pack) return 'Bộ ảnh đang hoàn thiện';
  if (row?.masterLocked && row?.dnaLocked) return 'Chưa đủ';
  return 'Chưa tạo';
}

export function nextWork(row?: CharacterReferencePackGetRow | null) {
  const pack = row?.pack;
  if (!row?.masterLocked || !row?.dnaLocked) {
    return { headline: 'Chưa thể làm bộ tham chiếu', hint: 'Nhân vật cần Master và DNA đã khóa.', action: '' };
  }
  if (!pack) {
    return { headline: 'Chưa có bộ tham chiếu', hint: 'Bắt đầu bộ hình tham chiếu cho nhân vật này.', action: 'Tạo bộ tham chiếu' };
  }
  if (pack.status === 'LOCKED') {
    return pack.productionReady
      ? { headline: '🔒 Đã khóa', hint: 'Sẵn sàng sử dụng trong production.', action: '' }
      : { headline: '🔒 Đã khóa', hint: 'Bộ tham chiếu đã khóa.', action: '' };
  }
  if (pack.status === 'APPROVED' || pack.status === 'DIRECTOR_APPROVED') {
    return { headline: 'Đã duyệt — chờ khóa', hint: 'Khóa bộ ảnh chuẩn để dùng cho các cảnh.', action: 'Khóa bộ ảnh chuẩn' };
  }
  if (pack.readyForDirector || pack.status === 'REVIEW' || pack.status === 'VALIDATED') {
    return { headline: '✓ Đã kiểm tra', hint: 'Xem 4 ảnh rồi duyệt hoặc từ chối.', action: 'Duyệt bộ ảnh' };
  }
  if (!pack.coverageReady) {
    const missingTypes = pack.missingTypes?.length
      ? pack.missingTypes
      : (pack.coverage ?? []).filter((x) => !x.pass).map((x) => (x.label || x.code.replace(/^req_/i, '')).toUpperCase());
    const labels = missingTypes.map((t) => viewLabel(t)).filter(Boolean);
    return {
      headline: `Tiến độ: ${pack.requiredReady}/${pack.requiredTotal}`,
      hint: labels.length ? `Thiếu: ${labels.join(', ')}` : 'Cần bổ sung',
      action: missingTypes.includes('FULL_BODY') ? 'TẠO ẢNH TOÀN THÂN' : 'TẠO ẢNH THAM CHIẾU',
    };
  }
  if (pack.canUse) {
    return { headline: '4/4', hint: 'Bộ ảnh chuẩn đã hoàn tất.', action: 'XEM BỘ ẢNH' };
  }
  return { headline: '4/4', hint: 'Bộ ảnh chuẩn đã đủ. Kiểm tra trước khi gửi duyệt.', action: 'KIỂM TRA' };
}

export function identityLine(row?: CharacterReferencePackRow | null) {
  const fails = (row?.identity ?? []).filter((x) => x.verdict === 'FAIL');
  if (fails[0]) {
    return {
      ok: false,
      text: `${IDENTITY_LABELS[fails[0].attribute] || fails[0].attribute}: ảnh tham chiếu đang khác với đặc điểm nhân vật đã khóa.`,
      detail: fails[0],
    };
  }
  const review = (row?.identity ?? []).find((x) => x.verdict === 'NEEDS_REVIEW');
  if (review) {
    return { ok: true, text: 'Có đặc điểm chưa ghi trong DNA — cần xem lại, không tự kết luận.', detail: review };
  }
  return { ok: true, text: 'Đặc điểm khớp với nhân vật đã khóa.', detail: undefined };
}
