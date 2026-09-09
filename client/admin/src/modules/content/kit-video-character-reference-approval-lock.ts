import type { CharacterReferencePackRow } from '@/shared/api/content.api';

export const CHARACTER_REFERENCE_APPROVAL_LOCK_ID = 'FAMIXA_CHARACTER_REFERENCE_APPROVAL_LOCK_V1';

export const STAFF_APPROVE = 'Duyệt bộ ảnh';
export const STAFF_LOCK = 'Khóa bộ ảnh chuẩn';
export const STAFF_DRAFT = 'Bộ ảnh đang hoàn thiện';
export const STAFF_APPROVED = 'Đã duyệt — chờ khóa';
export const STAFF_LOCKED = '✓ Sẵn sàng production';
export const STAFF_COMPLETE = 'Bộ ảnh chuẩn đã hoàn tất.';
export const STAFF_PRODUCTION_READY = 'Sẵn sàng sử dụng trong production.';
export const STAFF_NOT_READY = 'Bộ ảnh chưa sẵn sàng để duyệt';
export const STAFF_AUTHORITY = 'Character Reference Authority';
export const STAFF_CANCEL = 'Hủy';

export function isDirectorApproved(status?: string) {
  const s = (status || '').toUpperCase();
  return s === 'APPROVED' || s === 'DIRECTOR_APPROVED';
}

export function isLocked(status?: string) {
  return (status || '').toUpperCase() === 'LOCKED';
}

export function mayApprove(pack?: CharacterReferencePackRow | null) {
  if (!pack || pack.immutable || isLocked(pack.status) || isDirectorApproved(pack.status)) return false;
  if ((pack.status || '').toUpperCase() === 'REJECTED') return false;
  return pack.requiredReady >= 4 && pack.requiredTotal >= 4 && pack.coverageReady && pack.identityPass && pack.readyForDirector;
}

export function mayLock(pack?: CharacterReferencePackRow | null) {
  return !!pack && !pack.immutable && isDirectorApproved(pack.status) && !isLocked(pack.status);
}

export function notReadyReasons(pack?: CharacterReferencePackRow | null) {
  if (!pack) return ['Chưa có bộ ảnh chuẩn'];
  const rows: string[] = [];
  if ((pack.requiredReady ?? 0) < 4 || !pack.coverageReady) {
    const missing = (pack.missingTypes ?? []).join(', ');
    rows.push(missing ? `Thiếu / lỗi: ${missing}` : 'Thiếu / lỗi: chưa đủ 4 góc');
  }
  if (!pack.identityPass) rows.push('Thiếu / lỗi: nhận diện chưa khớp');
  if (!pack.readyForDirector && pack.blocked) rows.push(`Thiếu / lỗi: ${pack.blocked}`);
  if (!rows.length && !mayApprove(pack) && !isDirectorApproved(pack.status) && !isLocked(pack.status)) {
    rows.push('Bộ ảnh chưa được kiểm tra xong.');
  }
  return rows;
}

export function approveConfirm(character: string) {
  return {
    title: STAFF_APPROVE,
    body: `Bạn đang duyệt bộ ảnh chuẩn của ${character}.`,
    after: 'Sau khi duyệt, bộ ảnh này sẽ được sử dụng làm reference cho các bước sản xuất tiếp theo.',
    check: 'Kiểm tra kỹ 4 góc trước khi tiếp tục.',
    cancel: STAFF_CANCEL,
    confirm: STAFF_APPROVE,
  };
}

export function lockConfirm() {
  return {
    title: 'Khóa bộ ảnh chuẩn?',
    body: 'Sau khi khóa, bộ ảnh sẽ trở thành Character Reference Authority cho production.',
    after: 'Nội dung đã khóa không được chỉnh sửa trực tiếp.',
    check: 'Nếu cần thay đổi, phải tạo phiên bản mới.',
    cancel: STAFF_CANCEL,
    confirm: 'Khóa bộ ảnh',
  };
}

export function approvalStatusLabel(status?: string) {
  if (isLocked(status)) return STAFF_LOCKED;
  if (isDirectorApproved(status)) return STAFF_APPROVED;
  return STAFF_DRAFT;
}
