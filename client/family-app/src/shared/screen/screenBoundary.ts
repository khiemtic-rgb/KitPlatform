/** Screen Boundary A+B — soft-lock detect + OS checklist (prototype). */

export const SCREEN_BOUNDARY_IOS_URL = 'https://support.apple.com/vi-vn/HT208982';
export const SCREEN_BOUNDARY_ANDROID_URL = 'https://families.google.com/familylink/';

const SCREEN_CODES = new Set([
  'screen_no_game_today',
  'screen_reduce_15',
  'screen_reduce_30',
  'screen_reduce_30_weekend',
  'entertain_no_youtube',
]);

export function isScreenBoundaryCode(code?: string | null): boolean {
  const c = (code ?? '').trim().toLowerCase();
  if (!c) return false;
  return SCREEN_CODES.has(c) || c.startsWith('screen_');
}

export type ScreenChecklistItem = {
  id: string;
  label: string;
};

export const SCREEN_CHECKLIST: ScreenChecklistItem[] = [
  {
    id: 'open_os',
    label: 'Mở Screen Time (iPhone) hoặc Family Link (Android) trên máy con',
  },
  {
    id: 'limit_apps',
    label: 'Đặt giới hạn app / thời lượng đúng thỏa thuận nhà (game, YouTube…)',
  },
  {
    id: 'ask_permission',
    label: 'Bật “hỏi bố mẹ” / Ask to Buy nếu nhà đã thống nhất',
  },
  {
    id: 'second_device',
    label: 'Copy hướng dẫn cho máy thứ hai / ông bà (nếu cần)',
  },
  {
    id: 'done_mark',
    label: 'Đã cấu hình xong trên máy con hôm nay',
  },
];

const CHECKLIST_STORAGE_PREFIX = 'familyos-screen-checklist:';

export function loadChecklistDone(flowDate: string): Record<string, boolean> {
  try {
    const raw = localStorage.getItem(CHECKLIST_STORAGE_PREFIX + flowDate);
    if (!raw) return {};
    return JSON.parse(raw) as Record<string, boolean>;
  } catch {
    return {};
  }
}

export function saveChecklistDone(flowDate: string, done: Record<string, boolean>) {
  try {
    localStorage.setItem(CHECKLIST_STORAGE_PREFIX + flowDate, JSON.stringify(done));
  } catch {
    /* ignore */
  }
}

export function screenBoundaryShareText(labelVi: string): string {
  return (
    `Nhà mình áp dụng: ${labelVi}.\n` +
    `FamilyOS khóa nhẹ trong app — giới hạn máy dùng Screen Time / Family Link.\n` +
    `iPhone: ${SCREEN_BOUNDARY_IOS_URL}\n` +
    `Android: ${SCREEN_BOUNDARY_ANDROID_URL}`
  );
}
