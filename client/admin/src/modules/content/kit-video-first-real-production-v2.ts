export const FIRST_REAL_PRODUCTION_V2_ID = 'FAMIXA_FIRST_REAL_PRODUCTION_V2';
export const HISTORICAL_STILL_ID = '7ed003d7-789a-41ef-bcdf-a88637ff14d2';

export const DIRECTOR_QA_CHECKS = [
  'Nhân vật đúng',
  'Khuôn mặt đúng',
  'Tóc đúng',
  'Trang phục đúng',
  'Tỷ lệ cơ thể đúng',
  'Bối cảnh đúng',
  'Hành động đúng',
  'Camera đúng',
  'Ánh sáng đúng',
  'Không có lỗi hình ảnh rõ ràng',
] as const;

export function isShot001(shotCode?: string) {
  const match = (shotCode || '').toUpperCase().match(/SHOT-(\d+)/);
  return match ? Number(match[1]) === 1 : false;
}

export function crpLockedLabel(usable: boolean) {
  return usable ? '✓ Đã khóa' : 'Chưa hoàn tất';
}

export function pendingReviewLabel() {
  return 'Hình ảnh 1/1 · Chờ duyệt';
}

export function approvedReviewLabel() {
  return 'Hình ảnh 1/1 · Đã duyệt';
}

export function isImageDirectorApproved(status?: string, directorApproval?: string) {
  const s = (status || '').toUpperCase();
  const a = (directorApproval || '').toUpperCase();
  return a === 'APPROVED' || s === 'IMAGE_APPROVED' || s === 'APPROVED';
}

export function isImageDirectorRejected(status?: string, directorApproval?: string) {
  const s = (status || '').toUpperCase();
  const a = (directorApproval || '').toUpperCase();
  return a === 'REJECTED' || s === 'IMAGE_REJECTED' || s === 'REJECTED';
}

export function firstRealConfirmNote(shotLabel: string, provider: string) {
  return [
    `Famixa sẽ tạo 01 hình ảnh cho ${shotLabel} bằng ${provider} dựa trên bộ ảnh chuẩn và yêu cầu sản xuất đã khóa.`,
    'Đây là lần tạo hình production đầu tiên.',
    'Sau khi tạo, hình ảnh sẽ được đưa vào bước Duyệt hình.',
  ].join('\n\n');
}
