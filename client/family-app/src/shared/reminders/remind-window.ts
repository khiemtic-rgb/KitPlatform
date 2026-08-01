import type { DayFlowCommitment } from '@/shared/api/family-os.api';

/** Pilot: parent “Nhắc con” only within 30m lead / due / overdue (server reminderState). */
const REMIND_NOW_STATES = new Set(['upcoming', 'due_now', 'overdue']);

export function canRemindChildNow(item: DayFlowCommitment): boolean {
  if (item.status === 'done' || item.status === 'skipped') return false;
  if (item.reminderSuppressed) return false;
  return REMIND_NOW_STATES.has(item.reminderState);
}

export function remindChildIdleLabel(item: DayFlowCommitment): string {
  if (item.reminderState === 'upcoming') return 'Sắp tới';
  if (!item.windowStart && !item.windowEnd) return 'Trong ngày';
  return 'Chưa tới giờ';
}
