/**
 * School Season — quiet during school + after-school motivation.
 * Local SoT per child until blueprint hydrates school hours.
 */
import type { DayFlowCommitment } from '@/shared/api/family-os.api';
import { getOnboardingProfile, type AgeBand } from '@/shared/onboarding/onboarding';

const STORE_KEY = 'famixa.school-season.v1';

/** Mon=1 … Sun=7 (ISO-ish). */
export type Weekday = 1 | 2 | 3 | 4 | 5 | 6 | 7;

export type SchoolDayMode = 'off' | 'morning' | 'full';

export type SchoolSeasonSchedule = {
  memberId: string;
  /** Prefer true during school year; parent can turn off for summer. */
  seasonOn: boolean;
  mode: SchoolDayMode;
  weekdays: Weekday[];
  schoolStart: string; // HH:mm
  schoolEnd: string;
  hasExtraClass: boolean;
  /** When hasExtraClass — end of last ca (default schoolEnd+2h). */
  extraEnd?: string;
  updatedAt: string;
  /** SCH-01a API shape (optional on older local rows). */
  schemaVersion?: 1;
  source?: 'parent_settings' | 'onboarding_seed' | 'migrated_local';
  updatedByMemberId?: string;
};

/** Spec V1 payload under layers.members.<id>.schoolSchedule (no memberId). */
export type SchoolScheduleV1 = {
  schemaVersion: 1;
  seasonOn: boolean;
  mode: SchoolDayMode;
  weekdays: Weekday[];
  schoolStart: string;
  schoolEnd: string;
  hasExtraClass: boolean;
  extraEnd?: string;
  source: 'parent_settings' | 'onboarding_seed' | 'migrated_local';
  updatedAt: string;
  updatedByMemberId?: string;
};

export type SchoolPhase =
  | 'weekend'
  | 'season_off'
  | 'before_school'
  | 'at_school'
  | 'after_school'
  | 'evening';

type Store = Record<string, SchoolSeasonSchedule>;

function readStore(): Store {
  try {
    const raw = localStorage.getItem(STORE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as Store;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function writeStore(store: Store) {
  localStorage.setItem(STORE_KEY, JSON.stringify(store));
}

function padTime(t: string): string {
  const m = /^(\d{1,2}):(\d{2})$/.exec(t.trim());
  if (!m) return '07:00';
  const h = Math.min(23, Math.max(0, Number(m[1])));
  const min = Math.min(59, Math.max(0, Number(m[2])));
  return `${String(h).padStart(2, '0')}:${String(min).padStart(2, '0')}`;
}

function minutesOf(hhmm: string): number {
  const [h, m] = padTime(hhmm).split(':').map(Number);
  return h * 60 + m;
}

function nowMinutes(d = new Date()): number {
  return d.getHours() * 60 + d.getMinutes();
}

function isoWeekday(d = new Date()): Weekday {
  const js = d.getDay(); // 0=Sun
  return (js === 0 ? 7 : js) as Weekday;
}

const DEFAULT_WEEKDAYS: Weekday[] = [1, 2, 3, 4, 5];

export function defaultScheduleForAge(
  memberId: string,
  ageBand?: AgeBand | null,
  hasExtraClass?: boolean,
): SchoolSeasonSchedule {
  const band = ageBand ?? '7-9';
  const extra = Boolean(hasExtraClass);
  // Morning-only rare for preschool; primary+ often full day in VN school year.
  let mode: SchoolDayMode = 'full';
  let schoolStart = '07:00';
  let schoolEnd = '16:30';
  if (band === '4-6') {
    mode = 'morning';
    schoolEnd = '11:00';
  } else if (band === '7-9') {
    schoolEnd = extra ? '17:00' : '16:30';
  } else if (band === '10-12') {
    schoolEnd = extra ? '17:30' : '16:45';
  } else {
    // 13+
    schoolEnd = extra ? '18:00' : '17:00';
  }
  const extraEnd = extra
    ? padTime(
        (() => {
          const end = minutesOf(schoolEnd) + 90;
          const h = Math.floor(end / 60) % 24;
          const m = end % 60;
          return `${h}:${String(m).padStart(2, '0')}`;
        })(),
      )
    : undefined;

  return {
    memberId,
    seasonOn: true,
    mode,
    weekdays: [...DEFAULT_WEEKDAYS],
    schoolStart,
    schoolEnd,
    hasExtraClass: extra,
    extraEnd,
    updatedAt: new Date().toISOString(),
  };
}

export function getSchoolSchedule(memberId: string): SchoolSeasonSchedule | null {
  if (!memberId) return null;
  return readStore()[memberId] ?? null;
}

export function saveSchoolSchedule(schedule: SchoolSeasonSchedule) {
  const store = readStore();
  store[schedule.memberId] = {
    ...schedule,
    schoolStart: padTime(schedule.schoolStart),
    schoolEnd: padTime(schedule.schoolEnd),
    extraEnd: schedule.extraEnd ? padTime(schedule.extraEnd) : undefined,
    updatedAt: schedule.updatedAt || new Date().toISOString(),
    schemaVersion: 1,
  };
  writeStore(store);
}

function ts(iso?: string | null): number {
  if (!iso) return 0;
  const n = Date.parse(iso);
  return Number.isFinite(n) ? n : 0;
}

function normalizeWeekdays(raw: unknown): Weekday[] {
  if (!Array.isArray(raw) || raw.length === 0) return [...DEFAULT_WEEKDAYS];
  const out = raw
    .map((x) => Number(x))
    .filter((n) => n >= 1 && n <= 7) as Weekday[];
  return out.length > 0 ? out : [...DEFAULT_WEEKDAYS];
}

function normalizeMode(raw: unknown): SchoolDayMode {
  if (raw === 'off' || raw === 'morning' || raw === 'full') return raw;
  return 'full';
}

export function scheduleToV1(schedule: SchoolSeasonSchedule): SchoolScheduleV1 {
  return {
    schemaVersion: 1,
    seasonOn: Boolean(schedule.seasonOn),
    mode: schedule.mode,
    weekdays: normalizeWeekdays(schedule.weekdays),
    schoolStart: padTime(schedule.schoolStart),
    schoolEnd: padTime(schedule.schoolEnd),
    hasExtraClass: Boolean(schedule.hasExtraClass),
    extraEnd: schedule.extraEnd ? padTime(schedule.extraEnd) : undefined,
    source: schedule.source ?? 'parent_settings',
    updatedAt: schedule.updatedAt || new Date().toISOString(),
    updatedByMemberId: schedule.updatedByMemberId,
  };
}

export function scheduleFromV1(
  memberId: string,
  raw: SchoolScheduleV1 | Record<string, unknown> | null | undefined,
): SchoolSeasonSchedule | null {
  if (!raw || typeof raw !== 'object') return null;
  const r = raw as Record<string, unknown>;
  const schoolStart = typeof r.schoolStart === 'string' ? r.schoolStart : null;
  const schoolEnd = typeof r.schoolEnd === 'string' ? r.schoolEnd : null;
  if (!schoolStart || !schoolEnd) return null;
  const mode = normalizeMode(r.mode);
  const seasonOn = r.seasonOn === false || mode === 'off' ? false : Boolean(r.seasonOn ?? true);
  return {
    memberId,
    seasonOn: mode === 'off' ? false : seasonOn,
    mode,
    weekdays: normalizeWeekdays(r.weekdays),
    schoolStart: padTime(schoolStart),
    schoolEnd: padTime(schoolEnd),
    hasExtraClass: Boolean(r.hasExtraClass),
    extraEnd: typeof r.extraEnd === 'string' ? padTime(r.extraEnd) : undefined,
    updatedAt: typeof r.updatedAt === 'string' ? r.updatedAt : new Date().toISOString(),
    schemaVersion: 1,
    source:
      r.source === 'onboarding_seed' || r.source === 'migrated_local' || r.source === 'parent_settings'
        ? r.source
        : 'parent_settings',
    updatedByMemberId:
      typeof r.updatedByMemberId === 'string' ? r.updatedByMemberId : undefined,
  };
}

export function readMemberSchoolSchedule(
  layers: Record<string, unknown>,
  memberId: string,
): SchoolSeasonSchedule | null {
  const members = layers.members;
  if (!members || typeof members !== 'object') return null;
  const row = (members as Record<string, { schoolSchedule?: unknown }>)[memberId];
  if (!row?.schoolSchedule) return null;
  return scheduleFromV1(
    memberId,
    row.schoolSchedule as SchoolScheduleV1,
  );
}

export function memberSchoolSchedulePatch(
  memberId: string,
  schedule: SchoolSeasonSchedule,
): Record<string, unknown> {
  return {
    members: {
      [memberId]: {
        schoolSchedule: scheduleToV1(schedule),
      },
    },
  };
}

/** Pick newer of local vs layers; write winner back to local mirror. */
export function mergeSchoolSchedulePreferNewer(
  memberId: string,
  familyId: string | null | undefined,
  layers: Record<string, unknown> | null | undefined,
): SchoolSeasonSchedule {
  const local = getSchoolSchedule(memberId);
  const remote = layers ? readMemberSchoolSchedule(layers, memberId) : null;
  if (local && remote) {
    const winner = ts(remote.updatedAt) >= ts(local.updatedAt) ? remote : local;
    saveSchoolSchedule(winner);
    return winner;
  }
  if (remote) {
    saveSchoolSchedule({
      ...remote,
      source: remote.source ?? 'parent_settings',
    });
    return remote;
  }
  if (local) return local;
  const ob = familyId ? getOnboardingProfile(familyId) : null;
  const seeded = {
    ...defaultScheduleForAge(memberId, ob?.ageBand, ob?.hasExtraClass),
    source: 'onboarding_seed' as const,
  };
  saveSchoolSchedule(seeded);
  return seeded;
}

/**
 * Hydrate all children from blueprint layers → local.
 * Returns schedules map; caller should PUT `toPush` (local-only or local newer).
 */
export function hydrateSchoolSchedulesFromLayers(
  familyId: string,
  childMemberIds: string[],
  layers: Record<string, unknown>,
): {
  byMember: Record<string, SchoolSeasonSchedule>;
  /** Local rows missing/newer on server — should PUT */
  toPush: SchoolSeasonSchedule[];
} {
  const byMember: Record<string, SchoolSeasonSchedule> = {};
  const toPush: SchoolSeasonSchedule[] = [];
  for (const id of childMemberIds) {
    const local = getSchoolSchedule(id);
    const remote = readMemberSchoolSchedule(layers, id);
    if (local && !remote) {
      const migrated = {
        ...local,
        source: 'migrated_local' as const,
        updatedAt: local.updatedAt || new Date().toISOString(),
        schemaVersion: 1 as const,
      };
      saveSchoolSchedule(migrated);
      byMember[id] = migrated;
      toPush.push(migrated);
      continue;
    }
    const merged = mergeSchoolSchedulePreferNewer(id, familyId, layers);
    byMember[id] = merged;
    if (local && remote && ts(local.updatedAt) > ts(remote.updatedAt)) {
      toPush.push(merged);
    }
  }
  return { byMember, toPush };
}

/** Resolve schedule: layers/local merge → onboarding seed. */
export function resolveSchoolSchedule(
  memberId: string,
  familyId?: string | null,
  layers?: Record<string, unknown> | null,
): SchoolSeasonSchedule {
  if (layers) return mergeSchoolSchedulePreferNewer(memberId, familyId, layers);
  const saved = getSchoolSchedule(memberId);
  if (saved) return saved;
  const ob = familyId ? getOnboardingProfile(familyId) : null;
  const seeded = {
    ...defaultScheduleForAge(memberId, ob?.ageBand, ob?.hasExtraClass),
    source: 'onboarding_seed' as const,
  };
  saveSchoolSchedule(seeded);
  return seeded;
}

/** Local mirror + blueprint write-through (SCH-01a). */
export async function syncSaveSchoolSchedule(
  familyId: string,
  schedule: SchoolSeasonSchedule,
  updatedByMemberId?: string,
): Promise<SchoolSeasonSchedule> {
  const next: SchoolSeasonSchedule = {
    ...schedule,
    schemaVersion: 1,
    source: schedule.source ?? 'parent_settings',
    updatedByMemberId: updatedByMemberId ?? schedule.updatedByMemberId,
    updatedAt: new Date().toISOString(),
  };
  saveSchoolSchedule(next);
  const { patchFamilyBlueprintLayers } = await import('@/shared/api/family-os.api');
  await patchFamilyBlueprintLayers(familyId, memberSchoolSchedulePatch(next.memberId, next));
  return next;
}

export function effectiveQuietEnd(schedule: SchoolSeasonSchedule): string {
  if (schedule.mode === 'off' || !schedule.seasonOn) return schedule.schoolStart;
  if (schedule.mode === 'morning') return schedule.schoolEnd;
  if (schedule.hasExtraClass && schedule.extraEnd) return schedule.extraEnd;
  return schedule.schoolEnd;
}

export function resolveSchoolPhase(
  schedule: SchoolSeasonSchedule | null | undefined,
  now = new Date(),
): SchoolPhase {
  if (!schedule || !schedule.seasonOn || schedule.mode === 'off') return 'season_off';
  const wd = isoWeekday(now);
  if (!schedule.weekdays.includes(wd)) return 'weekend';

  const t = nowMinutes(now);
  const start = minutesOf(schedule.schoolStart);
  const quietEnd = minutesOf(effectiveQuietEnd(schedule));
  const schoolEnd = minutesOf(schedule.schoolEnd);

  if (t < start) return 'before_school';
  if (t < quietEnd) return 'at_school';
  // Landing window: tan học → ~19:30
  if (t < Math.max(schoolEnd + 30, minutesOf('19:30'))) return 'after_school';
  return 'evening';
}

/** No kid chimes / hot alerts while in school bubble (incl. học thêm). */
export function isSchoolQuietNow(
  schedule: SchoolSeasonSchedule | null | undefined,
  now = new Date(),
): boolean {
  return resolveSchoolPhase(schedule, now) === 'at_school';
}

export function isOpenCommitment(c: DayFlowCommitment): boolean {
  return c.status !== 'done' && c.status !== 'skipped';
}

function isMorningish(c: DayFlowCommitment): boolean {
  const raw = (c.windowStart || c.windowEnd || '').slice(0, 5);
  if (!raw) {
    const t = c.title.toLowerCase();
    return /sáng|đánh răng|rửa mặt|ăn sáng|cặp|balo|đi học|thức/.test(t);
  }
  return minutesOf(raw) < 12 * 60;
}

function isEveningish(c: DayFlowCommitment): boolean {
  const raw = (c.windowStart || c.windowEnd || '').slice(0, 5);
  if (!raw) {
    const t = c.title.toLowerCase();
    return /tối|bài tập|học|đọc|ngủ|dọn|phòng/.test(t);
  }
  return minutesOf(raw) >= 15 * 60;
}

/** Soft UI: morning overdue during/after school is "chưa ghi nhận", not shame. */
export function softOverdueLabel(
  c: DayFlowCommitment,
  phase: SchoolPhase,
): string | null {
  if (!isOpenCommitment(c)) return null;
  if (c.reminderState !== 'overdue' && c.reminderState !== 'due_now') return null;
  if (!isMorningish(c)) return null;
  if (phase === 'at_school' || phase === 'after_school' || phase === 'evening') {
    return 'Chưa ghi nhận — sáng vội không sao';
  }
  return null;
}

/**
 * Hero evening task: one short home win after a long school day.
 * Prefer open evening/home items; avoid stacking with study if other options exist.
 */
export function pickHeroEveningTask(
  items: DayFlowCommitment[],
  phase: SchoolPhase,
): DayFlowCommitment | null {
  if (phase !== 'after_school' && phase !== 'evening') return null;
  const open = items.filter(isOpenCommitment);
  if (open.length === 0) return null;

  const score = (c: DayFlowCommitment): number => {
    let s = 0;
    const t = c.title.toLowerCase();
    if (isEveningish(c)) s += 4;
    if (/dọn|túi|dép|nước|bàn học|phòng|cất|giúp/.test(t)) s += 5;
    if (/đánh răng|rửa|ngủ/.test(t)) s += 3;
    if (/bài tập|học bài|ôn bài|môn/.test(t)) s -= 2; // prefer non-study hero
    if (c.reminderState === 'due_now') s += 2;
    if (c.reminderState === 'overdue') s += 1;
    return s;
  };

  return [...open].sort((a, b) => score(b) - score(a))[0] ?? null;
}

export function morningCatchUpItems(
  items: DayFlowCommitment[],
  phase: SchoolPhase,
): DayFlowCommitment[] {
  if (phase !== 'after_school' && phase !== 'evening') return [];
  return items.filter((c) => isOpenCommitment(c) && isMorningish(c));
}

export type SchoolLandingCopy = {
  kicker: string;
  bubble: string;
  ctaLabel: string | null;
};

export function schoolPhaseLabelVi(phase: SchoolPhase | string | null | undefined): string {
  switch (phase) {
    case 'before_school':
      return 'Sáng vội';
    case 'at_school':
      return 'Đang giờ học · Fami im lặng';
    case 'after_school':
      return 'Tan học';
    case 'evening':
      return 'Buổi tối';
    case 'weekend':
      return 'Cuối tuần';
    case 'season_off':
      return 'Nghỉ mùa học';
    default:
      return 'Chưa có lịch';
  }
}

export function schoolLandingCopy(
  shortName: string,
  phase: SchoolPhase,
  opts: {
    morningLeft: number;
    heroTitle: string | null;
    remaining: number;
  },
): SchoolLandingCopy | null {
  const who = shortName.trim() || 'Con';
  if (phase === 'at_school') {
    return {
      kicker: 'Mùa học · đang ở trường',
      bubble: `${who} ơi — giờ học Fami im lặng. Không cần mở máy. Về nhà rồi mình ghi nhận nhé.`,
      ctaLabel: null,
    };
  }
  if (phase === 'before_school') {
    return {
      kicker: 'Mùa học · buổi sáng',
      bubble:
        opts.remaining > 0
          ? `Sáng vội cũng được — làm thật là đủ. Về nhà hoặc bố mẹ chạm giúp cũng được.`
          : `Sáng nay nhà đã sẵn sàng rồi. Đi học vui nhé, ${who}!`,
      ctaLabel: opts.remaining > 0 ? 'Xem việc sáng' : null,
    };
  }
  if (phase === 'after_school') {
    if (opts.heroTitle) {
      return {
        kicker: 'Tan học · một thắng nhỏ',
        bubble: `Hôm nay dài ghê — ${who} về rồi! Chỉ cần một việc nhỏ: «${opts.heroTitle}». Xong là chiến thắng.`,
        ctaLabel: 'Làm việc nhỏ nào',
      };
    }
    if (opts.morningLeft > 0) {
      return {
        kicker: 'Tan học · ghi nhận sáng',
        bubble: `Sáng vội không sao. ${who} tick giúp ${opts.morningLeft} việc sáng đã làm thật nhé — Fami không trách.`,
        ctaLabel: 'Ghi nhận buổi sáng',
      };
    }
    return {
      kicker: 'Tan học',
      bubble: `${who} về rồi — Fami tự hào vì một ngày dài. Uống nước, nghỉ một chút đã.`,
      ctaLabel: null,
    };
  }
  if (phase === 'evening') {
    if (opts.heroTitle) {
      return {
        kicker: 'Buổi tối · nhẹ thôi',
        bubble: `Tối nay chỉ một việc nhà: «${opts.heroTitle}». Không cần xếp chồng với ngày học.`,
        ctaLabel: 'Làm việc tối',
      };
    }
    return {
      kicker: 'Buổi tối',
      bubble: `Ngày học đã đủ dài. Tối nay nghỉ ngon cũng là giữ nhịp nhà.`,
      ctaLabel: null,
    };
  }
  return null;
}

export const SCHOOL_MODE_OPTIONS: Array<{
  value: SchoolDayMode;
  label: string;
  hint: string;
}> = [
  { value: 'morning', label: 'Học buổi sáng', hint: 'Tan sáng — chiều ở nhà' },
  { value: 'full', label: 'Học cả ngày', hint: 'Có học thêm thì kéo giờ im lặng' },
  { value: 'off', label: 'Nghỉ / hè', hint: 'Tắt mùa học — nhắc bình thường' },
];
