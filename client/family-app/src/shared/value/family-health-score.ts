import type { AccountabilityDayGlance, AccountabilityGlance, DayFlow } from '@/shared/api/family-os.api';
import { getNudgeCount } from '@/shared/nudge/nudge-stats';

const SCORE_HISTORY_KEY = 'famixa.family.health.v1';

export type HealthBreakdown = {
  completion: number;
  nudgeCalm: number;
  streak: number;
  autonomy: number;
};

export type FamilyHealthScore = {
  score: number;
  breakdown: HealthBreakdown;
  label: string;
  deltaVsYesterday: number | null;
  /** Why this score exists — monetization copy */
  promiseLine: string;
};

type ScoreHistory = Record<string, Record<string, number>>; // familyId -> date -> score

function clamp(n: number, min = 0, max = 100) {
  return Math.max(min, Math.min(max, Math.round(n)));
}

function dayRatio(d: AccountabilityDayGlance): number {
  const total = Math.max(1, d.childDone + d.childSkipped + d.childOpen);
  return d.childDone / total;
}

function autonomyRatio(d: AccountabilityDayGlance): number {
  if (d.childDone <= 0) return d.childOpen === 0 ? 1 : 0.4;
  return 1 - Math.min(1, d.childLateDone / Math.max(1, d.childDone));
}

function readHistory(): ScoreHistory {
  try {
    const raw = localStorage.getItem(SCORE_HISTORY_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as ScoreHistory;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

export function persistHealthScoreLocal(familyId: string, flowDate: string, score: number) {
  const store = readHistory();
  const row = store[familyId] ?? {};
  row[flowDate] = score;
  store[familyId] = row;
  localStorage.setItem(SCORE_HISTORY_KEY, JSON.stringify(store));
}

/** @deprecated use persistHealthScoreLocal + value-sync.syncHealthScore */
export function persistHealthScore(familyId: string, flowDate: string, score: number) {
  persistHealthScoreLocal(familyId, flowDate, score);
}

export function getHealthScoreOn(familyId: string, flowDate: string): number | null {
  const v = readHistory()[familyId]?.[flowDate];
  return typeof v === 'number' ? v : null;
}

export function listLocalHealthScores(familyId: string): Record<string, number> {
  return { ...(readHistory()[familyId] ?? {}) };
}

export function mergeHealthScores(familyId: string, scores: Record<string, number>) {
  const store = readHistory();
  const row = { ...(store[familyId] ?? {}) };
  for (const [date, score] of Object.entries(scores)) {
    row[date] = Math.max(Number(row[date] ?? 0), Math.max(0, Math.min(100, score)));
  }
  store[familyId] = row;
  localStorage.setItem(SCORE_HISTORY_KEY, JSON.stringify(store));
}

function scoreLabel(score: number): string {
  if (score >= 85) return 'Gia đình đang rất khỏe';
  if (score >= 70) return 'Nhịp nhà đang tốt';
  if (score >= 55) return 'Đang tiến bộ — giữ nhẹ nhàng';
  if (score >= 40) return 'Cần sát cánh thêm';
  return 'Ưu tiên 1–2 thói quen quan trọng';
}

/**
 * Family Health Score v1 — only signals we can actually measure today.
 * Weights: completion 30% · nudge calm 30% · streak 20% · autonomy/on-time 20%.
 */
export function computeFamilyHealthScore(input: {
  familyId: string;
  flow: DayFlow;
  glance: AccountabilityGlance | null;
  nudgeToday: number;
  momentCount: number;
}): FamilyHealthScore {
  const { familyId, flow, glance, nudgeToday, momentCount } = input;
  const days = [...(glance?.days ?? [])].filter((d) => d.isScored || d.childDone > 0);
  const recent = days.slice(-7);

  const todayCompletion =
    flow.totalCommitments > 0 ? flow.doneCount / flow.totalCommitments : recent.length > 0
      ? recent.reduce((s, d) => s + dayRatio(d), 0) / recent.length
      : 0.5;
  const completion = clamp(todayCompletion * 100);

  // 0 reminders = 100; 8+ = ~0
  const nudgeCalm = clamp(100 - nudgeToday * 12.5);

  const streakDays = glance?.currentStreak ?? 0;
  const streak = clamp(Math.min(100, streakDays * 12.5));

  const todayLate = flow.commitments.filter((c) => c.isLateDone).length;
  const todayDone = flow.commitments.filter((c) => c.status === 'done').length;
  const todayAutonomy =
    todayDone > 0
      ? 1 - Math.min(1, todayLate / todayDone)
      : recent.length > 0
        ? recent.reduce((s, d) => s + autonomyRatio(d), 0) / recent.length
        : 0.55;
  const autonomyBoost = Math.min(10, momentCount * 3);
  const autonomy = clamp(todayAutonomy * 100 + autonomyBoost);

  const score = clamp(
    completion * 0.3 + nudgeCalm * 0.3 + streak * 0.2 + autonomy * 0.2,
  );

  void import('@/shared/value/value-sync').then((m) => {
    m.syncHealthScore(familyId, flow.flowDate, score);
  });

  const y = new Date(`${flow.flowDate}T12:00:00`);
  y.setDate(y.getDate() - 1);
  const yIso = `${y.getFullYear()}-${String(y.getMonth() + 1).padStart(2, '0')}-${String(y.getDate()).padStart(2, '0')}`;
  const yesterday = getHealthScoreOn(familyId, yIso);
  const deltaVsYesterday = yesterday != null ? score - yesterday : null;

  return {
    score,
    breakdown: { completion, nudgeCalm, streak, autonomy },
    label: scoreLabel(score),
    deltaVsYesterday,
    promiseLine:
      nudgeToday === 0
        ? 'Hôm nay nhà gần như không cần nhắc — đây là giá trị Famixa đo được.'
        : 'Giảm số lần nhắc + tăng tự giác = lý do tiếp tục dùng Famixa.',
  };
}

export function averageHealthLastDays(familyId: string, flowDate: string, days = 7): number | null {
  const hist = readHistory()[familyId] ?? {};
  const scores: number[] = [];
  const cursor = new Date(`${flowDate}T12:00:00`);
  for (let i = 0; i < days; i++) {
    const iso = `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}-${String(cursor.getDate()).padStart(2, '0')}`;
    const v = hist[iso];
    if (typeof v === 'number') scores.push(v);
    cursor.setDate(cursor.getDate() - 1);
  }
  if (scores.length === 0) return null;
  return clamp(scores.reduce((a, b) => a + b, 0) / scores.length);
}

/** Proxy nudge load from glance when local nudge log is empty. */
export function estimateNudgeProxy(d: AccountabilityDayGlance): number {
  return Math.max(0, d.childOpen + d.childLateDone);
}

export function sumNudgesInRange(familyId: string, flowDate: string, dayCount: number): number {
  let total = 0;
  const cursor = new Date(`${flowDate}T12:00:00`);
  for (let i = 0; i < dayCount; i++) {
    const iso = `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}-${String(cursor.getDate()).padStart(2, '0')}`;
    total += getNudgeCount(familyId, iso);
    cursor.setDate(cursor.getDate() - 1);
  }
  return total;
}
