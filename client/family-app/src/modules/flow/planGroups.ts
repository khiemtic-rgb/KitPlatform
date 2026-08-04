/** Plan tab helpers — Kế hoạch is life rhythm, not a todo list. */

export type PlanGroupId = 'today' | 'routine' | 'challenge' | 'calendar';

export type PlanCalendarItem = {
  id: string;
  kind: 'birthday' | 'period' | 'study' | 'hint';
  titleVi: string;
  whenVi: string;
  metaVi?: string;
};

export type PlanTimelineRow = {
  id: string;
  time: string;
  title: string;
  who: string;
  done: boolean;
};

function parseYmd(ymd: string): { y: number; m: number; d: number } | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(ymd.trim());
  if (!m) return null;
  return { y: Number(m[1]), m: Number(m[2]), d: Number(m[3]) };
}

function daysBetween(a: Date, b: Date): number {
  const ms = 24 * 60 * 60 * 1000;
  const ua = Date.UTC(a.getFullYear(), a.getMonth(), a.getDate());
  const ub = Date.UTC(b.getFullYear(), b.getMonth(), b.getDate());
  return Math.round((ub - ua) / ms);
}

/** Next birthday within `withinDays` (inclusive), relative to flow date. */
export function upcomingBirthdays(
  members: Array<{ id: string; displayName: string; dateOfBirth?: string | null }>,
  asOfYmd: string,
  withinDays = 60,
): PlanCalendarItem[] {
  const asOf = parseYmd(asOfYmd);
  if (!asOf) return [];
  const base = new Date(asOf.y, asOf.m - 1, asOf.d);
  const out: PlanCalendarItem[] = [];

  for (const mem of members) {
    const dob = mem.dateOfBirth ? parseYmd(mem.dateOfBirth) : null;
    if (!dob) continue;
    let next = new Date(asOf.y, dob.m - 1, dob.d);
    if (daysBetween(base, next) < 0) {
      next = new Date(asOf.y + 1, dob.m - 1, dob.d);
    }
    const days = daysBetween(base, next);
    if (days < 0 || days > withinDays) continue;
    const short =
      mem.displayName.trim().split(/\s+/).filter(Boolean).pop() || mem.displayName;
    const whenVi =
      days === 0
        ? 'Hôm nay'
        : days === 1
          ? 'Ngày mai'
          : `Còn ${days} ngày · ${String(dob.d).padStart(2, '0')}/${String(dob.m).padStart(2, '0')}`;
    out.push({
      id: `bday-${mem.id}`,
      kind: 'birthday',
      titleVi: `Sinh nhật ${short}`,
      whenVi,
      metaVi: days === 0 ? 'Gửi lời chúc ấm' : 'Nhắc nhẹ trong nhà',
    });
  }

  return out.sort((a, b) => a.whenVi.localeCompare(b.whenVi, 'vi'));
}

export function ritualCadenceLabel(cadence: string): string {
  const c = cadence.trim().toLowerCase();
  if (c === 'daily' || c === 'day') return 'Mỗi ngày';
  if (c === 'weekly' || c === 'week') return 'Mỗi tuần';
  if (c === 'monthly' || c === 'month') return 'Mỗi tháng';
  return cadence.trim() || 'Định kỳ';
}

/** Soft schedule strip — window start first, then open items without clock. */
export function buildPlanTimeline(
  commitments: Array<{
    id: string;
    title: string;
    memberName?: string;
    status: string;
    windowStart?: string;
    windowEnd?: string;
  }>,
  fallbackWho = 'Nhà',
): PlanTimelineRow[] {
  const rows = commitments.map((c) => {
    const start = c.windowStart?.slice(0, 5);
    const end = c.windowEnd?.slice(0, 5);
    const time = start ? (end && end !== start ? `${start}–${end}` : start) : '—';
    return {
      id: c.id,
      time,
      title: c.title,
      who: c.memberName?.trim() || fallbackWho,
      done: c.status === 'done' || c.status === 'skipped',
      _sort: start ?? '99:99',
    };
  });
  rows.sort((a, b) => {
    if (a._sort !== b._sort) return a._sort.localeCompare(b._sort);
    return a.title.localeCompare(b.title, 'vi');
  });
  return rows.map(({ _sort: _, ...row }) => row);
}

/** Titles that look like extracurricular / fixed calendar slots. */
export function studyCalendarHints(
  commitments: Array<{
    id: string;
    title: string;
    memberName?: string;
    windowStart?: string;
    windowEnd?: string;
    status: string;
  }>,
): PlanCalendarItem[] {
  const re = /học thêm|gia sư|lớp phụ|clb|câu lạc bộ|bơi|võ|đàn|piano|anh văn|tiếng anh/i;
  return commitments
    .filter((c) => re.test(c.title))
    .map((c) => {
      const start = c.windowStart?.slice(0, 5);
      const end = c.windowEnd?.slice(0, 5);
      const whenVi = start
        ? end && end !== start
          ? `Hôm nay ${start}–${end}`
          : `Hôm nay ${start}`
        : 'Trong kế hoạch hôm nay';
      return {
        id: `study-${c.id}`,
        kind: 'study' as const,
        titleVi: c.title,
        whenVi,
        metaVi: c.memberName?.trim() || undefined,
      };
    });
}

export function unlockStatusLabelVi(status: string): string {
  if (status === 'confirmed') return 'Đã mở';
  if (status === 'pending_confirm') return 'Chờ duyệt';
  if (status === 'deferred') return 'Để sau';
  return status || 'Đang mở';
}
