/** Daily Digital Mirror M0/M1 — checklist + live evening summary */

export const MIRROR_FAMILY_SAFETY_URL = 'https://account.microsoft.com/family/';
export const MIRROR_FAMILY_SAFETY_HELP_URL =
  'https://support.microsoft.com/vi-vn/account-billing/microsoft-family-safety';

/** M1: agent is PowerShell under tools/famixa-mirror-agent (no MSI yet). */
export const MIRROR_AGENT_COMING_SOON = false;

export type MirrorChecklistItem = {
  id: string;
  label: string;
};

export const MIRROR_AGENT_CHECKLIST: MirrorChecklistItem[] = [
  {
    id: 'pick_pc',
    label: 'Chọn máy Windows con hay dùng để học / chơi',
  },
  {
    id: 'parent_admin',
    label: 'Đăng nhập Windows bằng tài khoản bố/mẹ (quyền cài phần mềm)',
  },
  {
    id: 'explain_child',
    label: 'Nói với con: Mirror chỉ tổng kết tối ~22:30, không khóa máy, con cũng nhìn thấy',
  },
  {
    id: 'install_agent',
    label: 'Bấm “Tải & cài Agent Windows” trên máy PC của con → double-click file .cmd',
  },
  {
    id: 'family_safety_optional',
    label: 'Tuỳ chọn: mở Microsoft Family Safety nếu nhà cũng dùng lớp chặn sẵn có',
  },
  {
    id: 'ready_mark',
    label: 'Nhà sẵn sàng — chờ Mirror tối khi Agent lên sóng',
  },
];

const STORAGE_PREFIX = 'famixa-mirror-agent-checklist:';
const DISMISS_PREFIX = 'famixa-mirror-home-dismiss:';

export function loadMirrorChecklist(familyId: string): Record<string, boolean> {
  try {
    const raw = localStorage.getItem(STORAGE_PREFIX + familyId);
    if (!raw) return {};
    return JSON.parse(raw) as Record<string, boolean>;
  } catch {
    return {};
  }
}

export function saveMirrorChecklist(familyId: string, done: Record<string, boolean>) {
  try {
    localStorage.setItem(STORAGE_PREFIX + familyId, JSON.stringify(done));
  } catch {
    /* ignore */
  }
}

export function isMirrorHomeDismissed(familyId: string): boolean {
  try {
    return localStorage.getItem(DISMISS_PREFIX + familyId) === '1';
  } catch {
    return false;
  }
}

export function setMirrorHomeDismissed(familyId: string, dismissed: boolean) {
  try {
    if (dismissed) localStorage.setItem(DISMISS_PREFIX + familyId, '1');
    else localStorage.removeItem(DISMISS_PREFIX + familyId);
  } catch {
    /* ignore */
  }
}

export function mirrorChecklistProgress(done: Record<string, boolean>): {
  completed: number;
  total: number;
  ready: boolean;
} {
  const total = MIRROR_AGENT_CHECKLIST.length;
  const completed = MIRROR_AGENT_CHECKLIST.filter((i) => done[i.id]).length;
  return {
    completed,
    total,
    ready: Boolean(done.ready_mark),
  };
}

export function mirrorShareText(childShort: string): string {
  return (
    `Famixa Mirror — gương tối cho nhà mình\n` +
    `Mỗi tối ~22:30: tổng kết app/web ${childShort} đã mở trong ngày.\n` +
    `Không khóa máy. ${childShort} cũng nhìn thấy cùng báo cáo.\n` +
    `Bố mẹ gửi một lời khen hoặc nhắc nhẹ — để cả nhà hiểu nhau hơn.\n` +
    `Cần Famixa Agent trên máy Windows — tải 1 lần từ app Famixa (bố/mẹ).\n` +
    `Family Safety (tuỳ chọn): ${MIRROR_FAMILY_SAFETY_URL}`
  );
}
