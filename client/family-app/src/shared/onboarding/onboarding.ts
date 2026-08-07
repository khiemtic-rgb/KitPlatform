export type AgeBand = '4-6' | '7-9' | '10-12' | '13+';
export type StruggleCode =
  | 'morning_forget'
  | 'brush_teeth'
  | 'homework'
  | 'screen'
  | 'sleep'
  | 'tidy';
export type GoalCode = 'fewer_nudges' | 'more_autonomy' | 'quality_time' | 'bedtime';

export type StarterMission = {
  title: string;
  windowStart: string;
  windowEnd: string;
  why: string;
  priority: 'high' | 'normal';
};

export type PriorityCode = 'autonomy' | 'study' | 'screen' | 'chores';

export type OnboardingAnswers = {
  childId: string;
  childName: string;
  ageBand: AgeBand;
  struggles: StruggleCode[];
  goal: GoalCode;
  /** AFE wizard extras — optional for backward compat. */
  childCount?: number;
  hasExtraClass?: boolean;
  sleepHour?: string;
  priorities?: PriorityCode[];
};

export type OnboardingProfile = OnboardingAnswers & {
  completedAt: string;
  missionTitles: string[];
  skipped?: boolean;
};

const STORE_KEY = 'famixa.onboarding.v1';

type Store = Record<string, OnboardingProfile>;

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

export function isOnboardingDone(familyId: string): boolean {
  return Boolean(readStore()[familyId]?.completedAt);
}

export function getOnboardingProfile(familyId: string): OnboardingProfile | null {
  return readStore()[familyId] ?? null;
}

export function saveOnboardingProfileLocal(familyId: string, profile: OnboardingProfile) {
  const store = readStore();
  store[familyId] = profile;
  localStorage.setItem(STORE_KEY, JSON.stringify(store));
}

/** Local + fire-and-forget server (prefer syncSaveOnboarding when awaiting). */
export function saveOnboardingProfile(familyId: string, profile: OnboardingProfile) {
  saveOnboardingProfileLocal(familyId, profile);
  void import('@/shared/value/value-sync').then((m) => {
    void m.syncSaveOnboarding(familyId, profile).catch(() => undefined);
  });
}

export function clearOnboardingProfileLocal(familyId: string) {
  const store = readStore();
  delete store[familyId];
  localStorage.setItem(STORE_KEY, JSON.stringify(store));
}

export function clearOnboardingProfile(familyId: string) {
  clearOnboardingProfileLocal(familyId);
  void import('@/shared/value/value-sync').then((m) => {
    void m.syncClearOnboarding(familyId).catch(() => undefined);
  });
}

/** Chuẩn 4 bậc học đường VN — label DNA / onboarding / coach phải khớp. */
export const AGE_OPTIONS: Array<{ value: AgeBand; label: string; hint: string }> = [
  { value: '4-6', label: 'Mầm non', hint: '4–6 tuổi · việc ngắn, hình ảnh, khen ngay' },
  { value: '7-9', label: 'Tiểu học', hint: '7–9 tuổi · 1 việc tiếp theo + phần thưởng nhà' },
  { value: '10-12', label: 'Trung học cơ sở', hint: '10–12 tuổi · tự chủ hơn, ít nhắc hơn' },
  { value: '13+', label: 'Trung học phổ thông', hint: '13+ tuổi · thỏa thuận rõ, tôn trọng không gian' },
];

/** Map nhãn DNA cũ → 4 bậc chuẩn (SPA fallback trước khi API hydrate lại). */
export function canonicalSchoolStageLabelVi(label: string | null | undefined): string {
  const raw = (label ?? '').trim();
  if (!raw) return '';
  const s = raw.toLowerCase();
  if (s.includes('mầm non') || s.includes('mam non') || s.includes('chuẩn bị lớp')) return 'Mầm non';
  if (s.includes('phổ thông') || s.includes('thpt') || s.includes('tuổi teen') || s.includes('teen'))
    return 'Trung học phổ thông';
  if (s.includes('cơ sở') || s.includes('thcs') || s.includes('tiền trung')) return 'Trung học cơ sở';
  if (s.includes('tiểu học') || s.includes('đầu cấp') || s.includes('primary')) return 'Tiểu học';
  return raw;
}

export const STRUGGLE_OPTIONS: Array<{ value: StruggleCode; label: string; icon: string }> = [
  { value: 'morning_forget', label: 'Hay quên việc buổi sáng', icon: '☀️' },
  { value: 'brush_teeth', label: 'Không chịu đánh răng', icon: '🪥' },
  { value: 'homework', label: 'Làm bài tập khó / trì hoãn', icon: '📘' },
  { value: 'screen', label: 'Dán mắt vào màn hình', icon: '📱' },
  { value: 'sleep', label: 'Đi ngủ muộn', icon: '🌙' },
  { value: 'tidy', label: 'Không chịu dọn / chuẩn bị cặp', icon: '🎒' },
];

export const GOAL_OPTIONS: Array<{ value: GoalCode; label: string; icon: string }> = [
  { value: 'fewer_nudges', label: 'Giảm số lần phải nhắc', icon: '🔔' },
  { value: 'more_autonomy', label: 'Con tự giác hơn', icon: '🌟' },
  { value: 'quality_time', label: 'Thêm thời gian chất lượng (Đêm xem phim / đọc)', icon: '🎬' },
  { value: 'bedtime', label: 'Giữ giờ đi ngủ ổn định', icon: '😴' },
];

export const PRIORITY_OPTIONS: Array<{ value: PriorityCode; label: string; icon: string }> = [
  { value: 'autonomy', label: 'Tự giác', icon: '🌟' },
  { value: 'study', label: 'Học tập', icon: '📘' },
  { value: 'screen', label: 'Giảm màn hình', icon: '📱' },
  { value: 'chores', label: 'Việc nhà', icon: '🏠' },
];

export const SLEEP_HOUR_OPTIONS = ['20:00', '20:30', '21:00', '21:30', '22:00'] as const;

/** Suggested weekly screen budget (agreement minutes) from age + mode signals. */
export function suggestStarterWalletMinutes(answers: OnboardingAnswers): number {
  const base =
    answers.ageBand === '4-6'
      ? 120
      : answers.ageBand === '7-9'
        ? 150
        : answers.ageBand === '10-12'
          ? 180
          : 210;
  let n = base;
  if (answers.priorities?.includes('screen') || answers.struggles.includes('screen')) n -= 30;
  if (answers.hasExtraClass) n -= 15;
  return Math.max(60, Math.min(300, n));
}

/** Rule-based “AI” starter plan — age + struggle + goal → concrete missions. */
export function buildStarterPlan(answers: OnboardingAnswers): {
  missions: StarterMission[];
  coachPitch: string;
  focusLine: string;
} {
  const { childName, ageBand, struggles, goal } = answers;
  const short = childName.split(/\s+/).pop() || childName;
  const missions: StarterMission[] = [];
  const push = (m: StarterMission) => {
    if (!missions.some((x) => x.title === m.title)) missions.push(m);
  };

  // Core by age
  if (ageBand === '4-6' || ageBand === '7-9') {
    push({
      title: 'Đánh răng sáng',
      windowStart: '06:45',
      windowEnd: '07:05',
      why: 'Neo thói quen ngắn ngay sau khi dậy',
      priority: 'high',
    });
    push({
      title: 'Ăn sáng',
      windowStart: '07:00',
      windowEnd: '07:25',
      why: 'Giữ nhịp buổi sáng nhẹ',
      priority: 'normal',
    });
    push({
      title: 'Chuẩn bị cặp',
      windowStart: '19:30',
      windowEnd: '20:00',
      why: 'Làm tối = sáng mai đỡ quên',
      priority: 'high',
    });
  } else {
    push({
      title: 'Đánh răng sáng',
      windowStart: '06:30',
      windowEnd: '06:45',
      why: 'Giữ cam kết tối thiểu mỗi ngày',
      priority: 'high',
    });
    push({
      title: 'Chuẩn bị cặp / lịch ngày mai',
      windowStart: '20:00',
      windowEnd: '20:20',
      why: 'Tự chủ buổi tối',
      priority: 'high',
    });
  }

  if (struggles.includes('brush_teeth') || struggles.includes('morning_forget')) {
    push({
      title: 'Đánh răng tối',
      windowStart: '20:30',
      windowEnd: '20:45',
      why: 'Khóa ngày bằng việc vệ sinh ngắn',
      priority: 'high',
    });
  }

  if (struggles.includes('homework') || ageBand === '7-9' || ageBand === '10-12') {
    push({
      title: 'Làm bài tập / học 20 phút',
      windowStart: ageBand === '10-12' || ageBand === '13+' ? '18:00' : '16:30',
      windowEnd: ageBand === '10-12' || ageBand === '13+' ? '18:40' : '17:10',
      why: 'Khung cố định tránh trì hoãn',
      priority: 'high',
    });
  }

  if (struggles.includes('tidy')) {
    push({
      title: 'Dọn góc học / gấp quần áo nhẹ',
      windowStart: '19:00',
      windowEnd: '19:20',
      why: 'Việc nhỏ tạo cảm giác kiểm soát',
      priority: 'normal',
    });
  }

  if (struggles.includes('sleep') || goal === 'bedtime' || answers.sleepHour) {
    const sleep = answers.sleepHour ?? (ageBand === '4-6' ? '20:30' : ageBand === '7-9' ? '21:00' : '21:30');
    const [hh, mm] = sleep.split(':').map(Number);
    const endH = mm >= 30 ? hh + 1 : hh;
    const endM = mm >= 30 ? '00' : '30';
    push({
      title: 'Đi ngủ đúng giờ',
      windowStart: sleep,
      windowEnd: `${String(endH).padStart(2, '0')}:${endM}`,
      why: 'Ngủ đủ → sáng ít quên hơn',
      priority: 'high',
    });
  }

  if (answers.hasExtraClass) {
    push({
      title: 'Học thêm / ôn bài ngắn',
      windowStart: '17:00',
      windowEnd: '17:40',
      why: 'Giữ khung học thêm ổn định',
      priority: 'high',
    });
  }

  if (answers.priorities?.includes('chores') && !struggles.includes('tidy')) {
    push({
      title: 'Việc nhà nhỏ 10 phút',
      windowStart: '18:30',
      windowEnd: '18:45',
      why: 'Ưu tiên việc nhà theo Setup Wizard',
      priority: 'normal',
    });
  }

  if (struggles.includes('screen')) {
    push({
      title: 'Tắt màn hình trước khi ngủ 30 phút',
      windowStart: '20:00',
      windowEnd: '20:30',
      why: 'Giảm kích thích → dễ ngủ',
      priority: 'high',
    });
  }

  if (goal === 'quality_time') {
    push({
      title: 'Đọc sách / kể chuyện cùng nhà 10 phút',
      windowStart: '20:10',
      windowEnd: '20:25',
      why: 'Thời gian chất lượng đo được',
      priority: 'normal',
    });
  }

  // Cap by age — younger = fewer missions
  const max = ageBand === '4-6' ? 4 : ageBand === '7-9' ? 5 : 6;
  const trimmed = missions.slice(0, max);

  const struggleLabel =
    STRUGGLE_OPTIONS.find((s) => s.value === struggles[0])?.label ?? 'nhịp nhà';
  const goalLabel = GOAL_OPTIONS.find((g) => g.value === goal)?.label ?? 'tiến bộ nhẹ';

  return {
    missions: trimmed,
    focusLine: `30 ngày tới: tập trung «${goalLabel.toLowerCase()}» cho ${short}.`,
    coachPitch: `Famixa hiểu ${short} (${AGE_OPTIONS.find((a) => a.value === ageBand)?.label}). Khó khăn lớn: ${struggleLabel}. Mình bắt đầu với ${trimmed.length} việc vừa sức — không nhồi checklist.`,
  };
}
