import type { DayFlowCommitment } from '@/shared/api/family-os.api';

function cleanTime(value?: string): string | null {
  if (!value) return null;
  return value.slice(0, 5);
}

/** Soft Vietnamese nudge for Zalo / Messenger — not surveillance language. */
export function buildNudgeText(item: DayFlowCommitment): string {
  const who = item.memberName?.trim() || 'Con';
  const title = item.title.trim();
  const start = cleanTime(item.windowStart);
  const end = cleanTime(item.windowEnd);
  const window =
    start && end ? ` (${start}–${end})` : start ? ` (lúc ${start})` : '';

  if (item.reminderState === 'overdue') {
    return `${who} ơi, quá giờ ${title.toLowerCase()} rồi${window}. Làm xong rồi báo bố/mẹ nhé.`;
  }
  if (item.reminderState === 'due_now') {
    return `${who} ơi, đến giờ ${title.toLowerCase()} rồi${window}. Làm nhé.`;
  }
  return `${who} ơi, sắp tới giờ ${title.toLowerCase()}${window}. Chuẩn bị nhé.`;
}

export function buildNudgeBatch(items: DayFlowCommitment[]): string {
  if (items.length === 0) return '';
  if (items.length === 1) return buildNudgeText(items[0]);
  const lines = items.map((item, index) => {
    const who = item.memberName?.trim() || 'Con';
    const start = cleanTime(item.windowStart);
    const label = item.reminderState === 'overdue' ? 'quá giờ' : 'đến giờ';
    return `${index + 1}. ${who} — ${label} ${item.title}${start ? ` (${start})` : ''}`;
  });
  return `Nhà mình còn vài việc cần làm:\n${lines.join('\n')}\nLàm xong nhớ tick trên FamilyOS nhé.`;
}

export async function shareOrCopyNudge(text: string): Promise<'shared' | 'copied'> {
  if (typeof navigator !== 'undefined' && typeof navigator.share === 'function') {
    try {
      await navigator.share({ text });
      return 'shared';
    } catch (err) {
      // User cancel — don't fall through as error noisily
      if (err instanceof DOMException && err.name === 'AbortError') {
        throw err;
      }
    }
  }

  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return 'copied';
  }

  // Legacy fallback
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.left = '-9999px';
  document.body.appendChild(area);
  area.select();
  document.execCommand('copy');
  document.body.removeChild(area);
  return 'copied';
}
