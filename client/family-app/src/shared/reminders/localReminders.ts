import type { DayFlow, DayFlowCommitment } from '@/shared/api/family-os.api';

const PERMISSION_HINT_KEY = 'familyos.notify.asked';

function storageKey(flowDate: string): string {
  return `familyos.notified:${flowDate}`;
}

function readNotified(flowDate: string): Set<string> {
  try {
    const raw = sessionStorage.getItem(storageKey(flowDate));
    if (!raw) return new Set();
    const parsed = JSON.parse(raw) as unknown;
    return new Set(Array.isArray(parsed) ? parsed.map(String) : []);
  } catch {
    return new Set();
  }
}

function writeNotified(flowDate: string, ids: Set<string>): void {
  sessionStorage.setItem(storageKey(flowDate), JSON.stringify([...ids]));
}

export function notificationSupport(): boolean {
  return typeof window !== 'undefined' && 'Notification' in window;
}

export function notificationPermission(): NotificationPermission | 'unsupported' {
  if (!notificationSupport()) return 'unsupported';
  return Notification.permission;
}

export async function ensureNotificationPermission(): Promise<NotificationPermission | 'unsupported'> {
  if (!notificationSupport()) return 'unsupported';
  if (Notification.permission === 'granted' || Notification.permission === 'denied') {
    return Notification.permission;
  }
  localStorage.setItem(PERMISSION_HINT_KEY, '1');
  return Notification.requestPermission();
}

export function shouldOfferNotificationOptIn(): boolean {
  if (!notificationSupport()) return false;
  if (Notification.permission !== 'default') return false;
  return localStorage.getItem(PERMISSION_HINT_KEY) !== '1';
}

function isOpen(c: DayFlowCommitment): boolean {
  return c.status !== 'done' && c.status !== 'skipped';
}

function isHot(c: DayFlowCommitment): boolean {
  return isOpen(c) && (c.reminderState === 'due_now' || c.reminderState === 'overdue');
}

function notifyTitle(c: DayFlowCommitment): string {
  return c.reminderState === 'overdue' ? 'Quá giờ rồi' : 'Đến giờ rồi';
}

function notifyBody(c: DayFlowCommitment): string {
  const who = c.memberName ? `${c.memberName} · ` : '';
  return `${who}${c.title}`;
}

/** Fire browser notifications for due_now / overdue (once per state per day, session). */
export function notifyDueCommitments(
  flow: DayFlow,
  opts?: { memberId?: string },
): number {
  if (!notificationSupport() || Notification.permission !== 'granted') return 0;

  const notified = readNotified(flow.flowDate);
  let fired = 0;

  const items = flow.commitments.filter((c) => {
    if (!isHot(c)) return false;
    if (opts?.memberId && c.memberId && c.memberId !== opts.memberId) return false;
    return true;
  });

  for (const item of items) {
    const key = `${item.id}:${item.reminderState}`;
    if (notified.has(key)) continue;
    try {
      new Notification(notifyTitle(item), {
        body: notifyBody(item),
        tag: key,
      });
      notified.add(key);
      fired++;
    } catch {
      // Safari / restricted contexts — ignore
    }
  }

  writeNotified(flow.flowDate, notified);
  return fired;
}
