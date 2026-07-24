import type { DayFlowCommitment } from '@/shared/api/family-os.api';

function parseClock(value?: string): number | null {
  if (!value) return null;
  const [h, m] = value.slice(0, 5).split(':').map(Number);
  if (![h, m].every(Number.isFinite)) return null;
  return h * 60 + m;
}

function resolveNowMinutes(localTime?: string, now = new Date()): number {
  const parsed = parseClock(localTime);
  if (parsed != null) return parsed;
  return now.getHours() * 60 + now.getMinutes();
}

function resolveUnlockMinutes(item: DayFlowCommitment): number | null {
  const start = parseClock(item.windowStart);
  if (start == null) return null;
  if (item.allowEarlyComplete && (item.earlyLeadMinutes ?? 0) <= 0) return null;
  if (!item.allowEarlyComplete) return start;
  const lead = Math.max(0, item.earlyLeadMinutes ?? 0);
  return Math.max(0, start - lead);
}

export function canCompleteNow(
  item: DayFlowCommitment,
  localTime?: string,
  now = new Date(),
): boolean {
  const unlock = resolveUnlockMinutes(item);
  if (unlock == null) return true;
  return resolveNowMinutes(localTime, now) >= unlock;
}

export function earlyCompleteBlockReason(
  item: DayFlowCommitment,
  localTime?: string,
  now = new Date(),
): string | null {
  if (canCompleteNow(item, localTime, now)) return null;
  const t = item.title.trim().toLowerCase();
  if (t.includes('ăn cơm') || t.includes('ăn tối') || t.includes('bữa tối'))
    return 'Chưa tới giờ — làm lúc ăn tối nhé';
  if (t.includes('ăn sáng') || t.includes('bữa sáng'))
    return 'Chưa tới giờ — làm lúc ăn sáng nhé';
  if (t.includes('đánh răng') && (t.includes('tối') || t.includes('ngủ')))
    return 'Chưa tới giờ — đánh răng trước khi ngủ nhé';
  if (t.includes('đánh răng')) return 'Chưa tới giờ — đánh răng đúng giờ nhé';
  if (t.includes('đi ngủ') || t.includes('ngủ'))
    return 'Chưa tới giờ — đi ngủ đúng giờ nhé';
  if (t.includes('đồng phục') || t.includes('mặc'))
    return 'Chưa tới giờ — mặc đồng phục đúng giờ nhé';
  if (t.includes('cặp') || t.includes('balo') || t.includes('chuẩn bị'))
    return 'Chưa tới giờ — chuẩn bị cặp đúng giờ nhé';
  if (t.includes('dậy')) return 'Chưa tới giờ — dậy đúng giờ nhé';
  if (t.includes('tắm')) return 'Chưa tới giờ — đi tắm đúng giờ nhé';
  const unlock = resolveUnlockMinutes(item);
  if (unlock != null) {
    const hh = String(Math.floor(unlock / 60)).padStart(2, '0');
    const mm = String(unlock % 60).padStart(2, '0');
    return `Chưa tới giờ — làm lúc ${hh}:${mm} nhé`;
  }
  const start = item.windowStart?.slice(0, 5);
  return start ? `Chưa tới giờ — làm lúc ${start} nhé` : 'Chưa tới giờ — chờ đến giờ nhé';
}

export function countdownUntilWindow(
  item: DayFlowCommitment,
  localTime?: string,
  now = new Date(),
): string | null {
  const unlock = resolveUnlockMinutes(item);
  if (unlock == null) return null;
  const diff = unlock - resolveNowMinutes(localTime, now);
  if (diff <= 0) return null;
  if (diff < 60) return `Còn ${diff} phút nữa`;
  const hrs = Math.floor(diff / 60);
  const mins = diff % 60;
  return mins > 0 ? `Còn ${hrs} giờ ${mins} phút nữa` : `Còn ${hrs} giờ nữa`;
}
