import type { DayFlowCommitment } from '@/shared/api/family-os.api';
import { shortPersonName, voicePick } from '@/shared/voice/family-voice';

function cleanTime(value?: string): string | null {
  if (!value) return null;
  return value.slice(0, 5);
}

function whoOf(item: DayFlowCommitment): string {
  return shortPersonName(item.memberName?.trim() || 'Con');
}

function titleOf(item: DayFlowCommitment): string {
  return item.title.trim();
}

function windowOf(item: DayFlowCommitment): string {
  const start = cleanTime(item.windowStart);
  const end = cleanTime(item.windowEnd);
  if (start && end) return ` (${start}–${end})`;
  if (start) return ` (lúc ${start})`;
  return '';
}

function seedOf(item: DayFlowCommitment): string {
  const day = (item.completedAt || item.windowStart || '').slice(0, 10);
  return `${day}:${item.id}:${item.reminderState}:${item.title}`;
}

/** Soft Vietnamese nudge for Zalo / Messenger — warm, not surveillance. */
export function buildNudgeText(item: DayFlowCommitment): string {
  const who = whoOf(item);
  const title = titleOf(item);
  const titleLow = title.toLowerCase();
  const window = windowOf(item);
  const seed = seedOf(item);

  if (item.reminderState === 'overdue') {
    return voicePick(seed, [
      `${who} ơi, quá giờ «${titleLow}» rồi${window}. Làm xong tick giúp bố mẹ nhé 💛`,
      `${who} ơi, «${title}» đang trễ một chút${window}. Con làm rồi báo nhà mình nha.`,
      `Nhà mình cần «${title}» xong sớm hơn một chút${window}. ${who} cố giúp bố mẹ nhé!`,
    ]);
  }
  if (item.reminderState === 'due_now') {
    return voicePick(seed, [
      `${who} ơi, đến giờ «${titleLow}» rồi${window}. Làm nhé — bố mẹ tin con!`,
      `${who} ơi, tới khung «${title}» rồi${window}. Xong nhớ tick trên app nha.`,
      `Đến giờ của ${who}: «${title}»${window}. Làm một việc này thôi trước đã!`,
    ]);
  }
  return voicePick(seed, [
    `${who} ơi, sắp tới giờ «${titleLow}»${window}. Chuẩn bị trước một chút nhé.`,
    `${who} ơi, «${title}» sắp đến${window}. Con chủ động là bố mẹ vui lắm!`,
    `Sắp tới «${title}» rồi ${who} ơi${window}. Chuẩn bị sẵn cho nhẹ nhàng hơn nha.`,
  ]);
}

export function buildNudgeBatch(items: DayFlowCommitment[]): string {
  if (items.length === 0) return '';
  if (items.length === 1) return buildNudgeText(items[0]!);

  const seed = items.map((i) => i.id).join('|');
  const lines = items.map((item, index) => {
    const who = whoOf(item);
    const start = cleanTime(item.windowStart);
    const label =
      item.reminderState === 'overdue'
        ? 'quá giờ'
        : item.reminderState === 'due_now'
          ? 'đến giờ'
          : 'sắp tới';
    return `${index + 1}. ${who} — ${label} «${item.title}»${start ? ` (${start})` : ''}`;
  });

  const closer = voicePick(seed, [
    'Làm xong nhớ tick trên Famixa nhé — cả nhà thấy tiến bộ liền.',
    'Chỉ cần từng việc một. Xong rồi báo bố mẹ trên app nha.',
    'Nhà mình cùng giữ nhịp nhẹ thôi — tick khi xong là được.',
  ]);

  return `Nhà mình còn vài việc cần làm:\n${lines.join('\n')}\n${closer}`;
}

export type ShareOrCopyOptions = {
  /**
   * When true, try Web Share API first (explicit “Chia sẻ” actions).
   * Default false: copy to clipboard only — avoids Windows/desktop Share popup
   * when parents tap “Nhắc con”.
   */
  preferShare?: boolean;
};

async function copyText(text: string): Promise<'copied'> {
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return 'copied';
  }

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

/** Windows desktop Share often pops an awkward OS window — keep clipboard there. */
function canUseSystemShare(): boolean {
  if (typeof navigator === 'undefined' || typeof navigator.share !== 'function') return false;
  const ua = navigator.userAgent || '';
  const isWindows = /Windows/i.test(ua);
  const isMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua);
  if (isWindows && !isMobile) return false;
  return true;
}

/** Copy nudge text by default. Only open system Share when preferShare is set (and safe). */
export async function shareOrCopyNudge(
  text: string,
  options?: ShareOrCopyOptions,
): Promise<'shared' | 'copied'> {
  if (options?.preferShare && canUseSystemShare()) {
    try {
      await navigator.share({ text });
      return 'shared';
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        throw err;
      }
      // Fall through to clipboard if share fails.
    }
  }

  return copyText(text);
}
