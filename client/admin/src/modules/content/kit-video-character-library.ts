export const CHARACTER_PRODUCTION_LIBRARY_ID = 'CHARACTER_PRODUCTION_LIBRARY_V1';

export const LIBRARY_FILTERS = [
  { id: 'all', label: 'Tất cả' },
  { id: 'ready', label: 'Đã sẵn sàng' },
  { id: 'in_progress', label: 'Đang hoàn thiện' },
  { id: 'review', label: 'Cần duyệt' },
  { id: 'problem', label: 'Có vấn đề' },
] as const;

export const LIBRARY_SORTS = [
  { id: 'series', label: 'Thứ tự series' },
  { id: 'name', label: 'Tên' },
  { id: 'scenes', label: 'Số cảnh' },
  { id: 'status', label: 'Trạng thái' },
] as const;

export function viewSlotLabel(type: string) {
  switch ((type || '').toUpperCase()) {
    case 'FRONT':
      return 'Trước mặt';
    case 'THREE_QUARTER':
      return '3/4';
    case 'SIDE':
      return 'Nghiêng';
    case 'FULL_BODY':
      return 'Toàn thân';
    default:
      return type;
  }
}

export function coverageLine(ready?: number, total?: number) {
  const have = Math.max(ready ?? 0, 0);
  const all = Math.max(total ?? 4, 0);
  return `${have}/${all}`;
}

export function pageSlice<T>(rows: T[], page: number, pageSize: number) {
  const start = Math.max(page, 0) * pageSize;
  return rows.slice(start, start + pageSize);
}
