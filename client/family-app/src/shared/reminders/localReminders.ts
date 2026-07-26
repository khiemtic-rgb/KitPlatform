import type { DayFlow, DayFlowCommitment } from '@/shared/api/family-os.api';
import { isInAppChimeEnabled, playInAppDueChime } from '@/shared/reminders/inAppChime';

const PERMISSION_HINT_KEY = 'familyos.notify.asked';

function storageKey(flowDate: string): string {
  return `familyos.notified:${flowDate}`;
}

function chimeStorageKey(flowDate: string): string {
  return `familyos.chimed:${flowDate}`;
}

function readSet(key: string): Set<string> {
  try {
    const raw = sessionStorage.getItem(key);
    if (!raw) return new Set();
    const parsed = JSON.parse(raw) as unknown;
    return new Set(Array.isArray(parsed) ? parsed.map(String) : []);
  } catch {
    return new Set();
  }
}

function writeSet(key: string, ids: Set<string>): void {
  sessionStorage.setItem(key, JSON.stringify([...ids]));
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

/**
 * Fire system notifications (with sound) + optional in-app chime for due_now / overdue.
 * Dedupes once per commitment state per day (session).
 */
export function notifyDueCommitments(
  flow: DayFlow,
  opts?: { memberId?: string },
): number {
  const notified = readSet(storageKey(flow.flowDate));
  const chimed = readSet(chimeStorageKey(flow.flowDate));
  let fired = 0;
  let wantChime = false;

  const canNotify =
    notificationSupport() && Notification.permission === 'granted';

  const items = flow.commitments.filter((c) => {
    if (!isHot(c)) return false;
    if (opts?.memberId && c.memberId && c.memberId !== opts.memberId) return false;
    return true;
  });

  for (const item of items) {
    const key = `${item.id}:${item.reminderState}`;
    const alreadyNotified = notified.has(key);
    const alreadyChimed = chimed.has(key);
    if (alreadyNotified && alreadyChimed) continue;

    if (canNotify && !alreadyNotified) {
      try {
        new Notification(notifyTitle(item), {
          body: notifyBody(item),
          tag: key,
          // Explicit: never silence — use OS / browser notification sound.
          silent: false,
          // Best-effort on supporting browsers (Android Chrome, etc.).
          vibrate: item.reminderState === 'overdue' ? [220, 100, 220] : [160, 60, 160],
          renotify: true,
        } as NotificationOptions);
        notified.add(key);
        fired++;
      } catch {
        // Safari / restricted contexts — still allow in-app chime below
      }
    }

    // In-app chime while Daily Flow is open (independent of Notification permission).
    if (!alreadyChimed) {
      wantChime = true;
      chimed.add(key);
    }
  }

  writeSet(storageKey(flow.flowDate), notified);
  writeSet(chimeStorageKey(flow.flowDate), chimed);

  if (wantChime && isInAppChimeEnabled()) {
    void playInAppDueChime();
  }

  return fired;
}
