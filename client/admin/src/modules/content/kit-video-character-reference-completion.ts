export const CHARACTER_REFERENCE_COMPLETION_ID = 'FAMIXA_CHARACTER_REFERENCE_COMPLETION_V1';

export const STAFF_MISSING_FULL_BODY = 'Thiếu ảnh toàn thân.';
export const STAFF_CRP_COMPLETE = 'Bộ ảnh chuẩn đã hoàn tất.';
export const STAFF_CRP_NEXT = 'Có thể chuyển sang bước tạo hình.';

export function completionCoverageLine(ready: number, total: number, missing: string[]) {
  if (ready >= total && total > 0) return STAFF_CRP_COMPLETE;
  if (missing.includes('FULL_BODY')) return 'Thiếu: Toàn thân';
  return missing.length ? `Thiếu: ${missing.join(', ')}` : STAFF_MISSING_FULL_BODY;
}
