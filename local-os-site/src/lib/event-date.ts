import type { LocalListing } from './api';

function vnToday(): Date {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date());
  const y = Number(parts.find((p) => p.type === 'year')?.value);
  const m = Number(parts.find((p) => p.type === 'month')?.value);
  const d = Number(parts.find((p) => p.type === 'day')?.value);
  return new Date(y, m - 1, d);
}

function parseYmd(day: number, month: number, year: number): Date | null {
  if (month < 1 || month > 12 || day < 1 || day > 31) return null;
  const dt = new Date(year, month - 1, day);
  if (dt.getFullYear() !== year || dt.getMonth() !== month - 1 || dt.getDate() !== day) return null;
  return dt;
}

/** Last calendar date already written on the tin. Does not invent a date. */
export function lastEventDate(text: string, today = vnToday()): Date | null {
  const t = text.replace(/[–—]/g, '-');
  const yearHint = t.match(/năm\s*(20\d{2})/i);
  const year = yearHint ? Number(yearHint[1]) : today.getFullYear();
  const dates: Date[] = [];
  const add = (d: string, m: string, y: string | number) => {
    const parsed = parseYmd(Number(d), Number(m), Number(y));
    if (parsed) dates.push(parsed);
  };
  for (const m of t.matchAll(/\b(\d{1,2})\/(\d{1,2})\s*-\s*(\d{1,2})\/(\d{1,2})\/(\d{4})\b/g)) {
    add(m[3], m[4], m[5]);
  }
  for (const m of t.matchAll(/\b(\d{1,2})\s*-\s*(\d{1,2})\/(\d{1,2})\/(\d{4})\b/g)) {
    add(m[2], m[3], m[4]);
  }
  for (const m of t.matchAll(/\b(\d{1,2})\/(\d{1,2})\/(\d{4})\b/g)) {
    add(m[1], m[2], m[3]);
  }
  for (const m of t.matchAll(/\b(\d{1,2})\s*-\s*(\d{1,2})\/(\d{1,2})(?!\/\d)\b/g)) {
    add(m[2], m[3], year);
  }
  for (const m of t.matchAll(/\b(\d{1,2})\/(\d{1,2})(?!\/\d)\b/g)) {
    add(m[1], m[2], year);
  }
  if (dates.length === 0) return null;
  return dates.reduce((a, b) => (a > b ? a : b));
}

export function isEventPast(item: LocalListing): boolean {
  if (item.kind !== 'event' && item.kind !== 'grant') return false;
  const today = vnToday();
  const stamped = item.endAt || item.startAt;
  if (stamped) {
    const t = new Date(stamped);
    if (!Number.isNaN(t.getTime())) {
      const local = new Date(t.toLocaleString('en-US', { timeZone: 'Asia/Ho_Chi_Minh' }));
      const day = new Date(local.getFullYear(), local.getMonth(), local.getDate());
      if (day < today) return true;
    }
  }
  const title = item.title ?? '';
  const datedEvent =
    /lễ hội|festival|hội chợ|phiên chợ|giao hữu|đêm nhạc|tuần phim|ngày hội|chợ tình/i.test(title);
  const text = datedEvent
    ? [item.title, item.summary, item.workingTime].filter(Boolean).join(' ')
    : [item.title, item.workingTime].filter(Boolean).join(' ');
  const parsed = lastEventDate(text, today);
  return !!parsed && parsed < today;
}
