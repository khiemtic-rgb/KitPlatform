import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  type AccountabilityGlance,
  type ConsequenceEvent,
  type DayFlow,
  type DayFlowCommitment,
  type SkipReasonCode,
  type SoftLockGuide,
  type TeamUnlock,
  confirmTeamUnlock,
  fetchTeamUnlocks,
  fetchChildGratitude,
  markChildGratitudeRead,
  approveCommitmentStars,
  fetchMemberStarBalance,
  fetchRewardCatalog,
  fetchRewardRedemptions,
  fulfillRewardRedemption,
  fetchFamilyMoods,
  fetchFamilyMemories,
  fetchFamilyMemoryRecap,
  createFamilyMemory,
  setFamilyMemoryFavorite,
  scanAdaptiveProposals,
  fetchFamilyScore,
  type FamilyMemberMood,
  type ChildGratitude,
  type FamilyMemoryEntry,
  type FamilyMemoryRecap,
  type RewardCatalogItem,
  type RewardRedemption,
  type FamilyScore,
  fetchFamilyBehaviorTwin,
  type FamilyBehaviorTwin,
  fetchFamilyCoachInsight,
  type FamilyCoachInsight,
  fetchBehaviorCoach,
  type BehaviorCoach,
  fetchFamilySubscription,
  type FamilySubscription,
  fetchParentSuccessEveningCheckin,
  type ParentSuccessCheckin,
  fetchParentCoachActedToday,
  recordParentCoachActed,
} from '@/shared/api/family-os.api';
import { DecisionInboxPanel } from '@/modules/flow/DecisionInboxPanel';
import { FamilyChallengeCard } from '@/modules/flow/FamilyChallengeCard';
import { ParentGoalsPanel } from '@/modules/flow/ParentGoalsPanel';
import { FamilyModeSheet } from '@/modules/flow/FamilyModeSheet';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import {
  getNudgeCount,
  isParentVerified,
  markParentVerified,
  previousCalendarDate,
} from '@/shared/nudge/nudge-stats';
import { QuickNudgeButton } from '@/shared/ui/QuickNudgeButton';
import { ScreenBoundaryPanel } from '@/shared/ui/ScreenBoundaryPanel';
import { ResetParentPinPanel } from '@/shared/ui/ResetParentPinPanel';
import { isScreenBoundaryCode } from '@/shared/screen/screenBoundary';
import {
  avatarEmoji,
  inferGenderFromName,
} from '@/shared/ui/avatarGender';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';
import { clearOnboardingProfile } from '@/shared/onboarding/onboarding';
import { buildParentPulse } from '@/shared/value/parent-pulse';
import { resolveParentCoach } from '@/shared/value/resolve-parenting-coach';
import { buildHomeBrief } from '@/shared/value/home-brief';
import {
  buildHomeFamilyFeed,
  pickMemoryWinVi,
} from '@/shared/value/home-family-feed';
import { FamilyValuePanel } from '@/modules/flow/FamilyValuePanel';
import { BillingBanner } from '@/shared/ui/BillingBanner';
import { PaywallSheet } from '@/shared/ui/PaywallSheet';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import {
  getApiErrorMessage,
  isCapabilityPaywallError,
} from '@/shared/billing/capability-error';
import {
  buildFamilyMemories,
  FAMILY_MEMORY_EMPTY,
  FAMILY_MEMORY_VISIBLE,
  type FamilyMemory,
} from '@/shared/flow/family-memories';
import {
  parentRoleFromName,
  warmTaskSupportNote,
  warmTaskTip,
  voicePick,
} from '@/shared/voice/family-voice';
import { FAMILY_MOODS, moodFromCode } from '@/shared/flow/family-moods';
import {
  formatLateDuration,
  formatLateDurationCaption,
  normalizeLateStarLabelVi,
  stripLateStarSuffixVi,
} from '@/shared/flow/late-duration';

const WEEKDAYS_VI = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
const DIARY_WD_SHORT = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
const DIARY_STAR = 10;
const DIARY_MOODS = FAMILY_MOODS;

const TRUST_CHILD_RE =
  /đánh răng|ăn sáng|ăn trưa|ăn tối|uống sữa|đi ngủ|ngủ|đi học|mặc|đồng phục|tắm|rửa mặt|rửa tay/i;
const NEED_APPROVAL_RE =
  /bài tập|học|dọn|phòng|luyện|đàn|piano|gấp|quần áo|đọc sách|viết|ôn|balo|cặp|chơi đàn/i;

type MissionFilter = 'all' | 'need_help' | 'waiting_child' | 'done';
type DiaryFilter = 'all' | 'tasks' | 'moments' | 'health' | 'study';
type ParentTab = 'home' | 'tasks' | 'rewards' | 'value' | 'diary';

function needsParentApproval(item: DayFlowCommitment): boolean {
  if (NEED_APPROVAL_RE.test(item.title)) return true;
  if (TRUST_CHILD_RE.test(item.title)) return false;
  return Boolean(item.evidenceUrl);
}

function formatFlowDay(flowDate: string): string {
  const d = new Date(`${flowDate}T12:00:00`);
  if (Number.isNaN(d.getTime())) return flowDate;
  const weekday = WEEKDAYS_VI[d.getDay()] ?? '';
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  return `${weekday}, ${dd}/${mm}/${d.getFullYear()}`;
}

function formatClock(iso?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) {
    const m = iso.match(/T(\d{2}:\d{2})/);
    return m ? m[1] : null;
  }
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

function minutesLate(item: DayFlowCommitment, localTime?: string): number | null {
  if (!item.windowEnd || !localTime) return null;
  const [eh, em] = item.windowEnd.slice(0, 5).split(':').map(Number);
  const [nh, nm] = localTime.slice(0, 5).split(':').map(Number);
  if (![eh, em, nh, nm].every(Number.isFinite)) return null;
  const late = nh * 60 + nm - (eh * 60 + em);
  return late > 0 ? late : null;
}

function lateLabel(item: DayFlowCommitment, localTime?: string): string {
  if (item.reminderState === 'due_now') return 'Đến giờ rồi';
  if (item.reminderState !== 'overdue') return 'Chưa làm trong ngày';
  const mins = minutesLate(item, localTime);
  if (mins == null) return 'Quá giờ từ sáng';
  if (mins >= 180) return 'Quá giờ từ sáng';
  return `Trễ ${formatLateDuration(mins)}`;
}

const LATE_ZERO_STAR_TIERS = new Set([
  'late_zero',
  'late_penalty_half',
  'late_penalty_full',
]);

function commitmentStars(item: DayFlowCommitment): number {
  if (item.starReward != null && item.starReward > 0) return item.starReward;
  const t = item.title.toLowerCase();
  if (t.includes('bài') || t.includes('học') || t.includes('toán')) return 20;
  if (t.includes('ngủ') || t.includes('đánh răng')) return 15;
  return DIARY_STAR;
}

function parentStarsForCommitment(c: DayFlowCommitment): number {
  if (c.starDelta != null) return c.starDelta;
  if (c.projectedStarDelta != null) return c.projectedStarDelta;
  if (c.isLateDone) return 0;
  if (c.starReward != null && c.starReward > 0) return c.starReward;
  return commitmentStars(c);
}

function parentDisplayDelta(item: DayFlowCommitment): number {
  if (item.status === 'done') return parentStarsForCommitment(item);
  if (item.status === 'skipped') return 0;
  return item.projectedStarDelta ?? commitmentStars(item);
}

function isLateZeroStarOutcome(item: DayFlowCommitment): boolean {
  if (item.isLateDone) return true;
  if (item.starTier && LATE_ZERO_STAR_TIERS.has(item.starTier)) return true;
  if (item.starLabelVi?.startsWith('Muộn') && (item.starDelta ?? 0) <= 0) return true;
  return false;
}

function lateMinutesAfterGrace(item: DayFlowCommitment, flowDate?: string): number | null {
  if (!item.completedAt || !item.windowEnd) return null;
  const completed = new Date(item.completedAt);
  if (Number.isNaN(completed.getTime())) return null;

  const dateKey = (flowDate || item.completedAt).slice(0, 10);
  const [eh, em] = item.windowEnd.slice(0, 5).split(':').map(Number);
  if (![eh, em].every(Number.isFinite)) return null;

  const grace = Math.max(0, item.onTimeGraceMinutes ?? 0);
  const totalMin = eh * 60 + em + grace;
  const deadline = new Date(`${dateKey}T00:00:00`);
  if (Number.isNaN(deadline.getTime())) return null;
  deadline.setHours(Math.floor(totalMin / 60), totalMin % 60, 0, 0);

  const lateMinutes = Math.floor((completed.getTime() - deadline.getTime()) / 60_000);
  return lateMinutes > 0 ? lateMinutes : null;
}

function parentLateCaption(item: DayFlowCommitment, flowDate?: string): string | null {
  if (item.status !== 'done') return null;
  const delta = parentDisplayDelta(item);
  if (!isLateZeroStarOutcome(item) || delta > 0) return null;
  if (item.starLabelVi?.startsWith('Muộn')) return stripLateStarSuffixVi(item.starLabelVi);
  const lateMin = lateMinutesAfterGrace(item, flowDate);
  if (lateMin != null) return formatLateDurationCaption(lateMin);
  if (item.isLateDone) return 'Làm muộn';
  return null;
}

function parentStarBadgeText(item: DayFlowCommitment, delta: number): string {
  if (item.starLabelVi?.trim()) return normalizeLateStarLabelVi(item.starLabelVi.trim());
  if (delta > 0) return `+${delta}⭐`;
  if (delta === 0) return '0⭐';
  return `${delta}⭐`;
}

function isOpen(c: DayFlowCommitment) {
  return c.status !== 'done' && c.status !== 'skipped';
}

function taskIcon(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng')) return '🪥';
  if (t.includes('ăn') || t.includes('cơm')) return '🥣';
  if (t.includes('bài tập') || t.includes('học')) return '📘';
  if (t.includes('đọc') || t.includes('sách')) return '📖';
  if (t.includes('cặp') || t.includes('balo')) return '🎒';
  if (t.includes('ngủ')) return '🌙';
  if (t.includes('tắm') || t.includes('rửa')) return '🚿';
  if (t.includes('dọn')) return '🧹';
  if (t.includes('đàn') || t.includes('piano')) return '🎹';
  if (t.includes('gấp') || t.includes('quần')) return '👕';
  if (t.includes('tưới') || t.includes('cây')) return '🪴';
  if (t.includes('gia đình') || t.includes('cùng')) return '👨‍👩‍👧';
  return '⭐';
}

function diaryHourOf(item: DayFlowCommitment): number | null {
  const raw = item.completedAt || item.windowStart || item.windowEnd;
  if (!raw) return null;
  if (raw.includes('T')) {
    const d = new Date(raw);
    return Number.isNaN(d.getTime()) ? null : d.getHours();
  }
  const h = Number(raw.slice(0, 2));
  return Number.isFinite(h) ? h : null;
}

function diaryDayPart(item: DayFlowCommitment): 'morning' | 'afternoon' | 'evening' {
  const h = diaryHourOf(item);
  if (h == null) return 'afternoon';
  if (h < 12) return 'morning';
  if (h < 17) return 'afternoon';
  return 'evening';
}

function diaryTimeLabel(item: DayFlowCommitment): string {
  return (
    formatClock(item.completedAt) ||
    item.windowStart?.slice(0, 5) ||
    item.windowEnd?.slice(0, 5) ||
    '--:--'
  );
}

function diaryIconTone(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('rửa') || t.includes('tắm')) return 'sky';
  if (t.includes('ăn') || t.includes('cơm')) return 'peach';
  if (t.includes('học') || t.includes('bài') || t.includes('cặp')) return 'lilac';
  if (t.includes('đọc') || t.includes('sách')) return 'lemon';
  if (t.includes('ngủ')) return 'indigo';
  if (t.includes('tưới') || t.includes('dọn') || t.includes('giúp')) return 'green';
  if (t.includes('gia đình') || t.includes('cùng')) return 'pink';
  return 'lilac';
}

function diaryCategory(title: string): Exclude<DiaryFilter, 'all'> {
  const t = title.toLowerCase();
  if (t.includes('đọc') || t.includes('học') || t.includes('bài') || t.includes('sách')) return 'study';
  if (
    t.includes('đánh răng') ||
    t.includes('ngủ') ||
    t.includes('tắm') ||
    t.includes('ăn') ||
    t.includes('rửa')
  ) {
    return 'health';
  }
  if (t.includes('cùng') || t.includes('gia đình') || t.includes('movie')) return 'moments';
  return 'tasks';
}

function diaryTag(title: string): { label: string; tone: string } {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('ngủ') || t.includes('tắm') || t.includes('rửa')) {
    return { label: 'Thói quen tốt', tone: 'green' };
  }
  if (t.includes('đọc') || t.includes('học') || t.includes('sách') || t.includes('bài')) {
    return { label: 'Học tập', tone: 'lemon' };
  }
  if (t.includes('tưới') || t.includes('dọn') || t.includes('giúp') || t.includes('cây')) {
    return { label: 'Trách nhiệm', tone: 'sky' };
  }
  if (t.includes('cùng') || t.includes('gia đình') || t.includes('movie')) {
    return { label: 'Khoảnh khắc', tone: 'pink' };
  }
  return { label: 'Nhiệm vụ', tone: 'lilac' };
}

function diaryNote(title: string, childShort: string, done: boolean): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng')) {
    return done
      ? `${childShort} đã tự đánh răng mà không cần mẹ nhắc!`
      : `${childShort} cần hoàn thành đánh răng`;
  }
  if (t.includes('đọc') || t.includes('sách')) {
    return done
      ? `${childShort} đọc sách rất chăm chỉ hôm nay`
      : `Thời gian đọc sách của ${childShort}`;
  }
  if (t.includes('tưới') || t.includes('cây')) {
    return done
      ? `${childShort} đã tưới cây giúp mẹ rất tốt`
      : `${childShort} sẽ tưới cây giúp mẹ`;
  }
  if (t.includes('ngủ')) {
    return done
      ? `${childShort} đi ngủ đúng giờ — giấc ngủ ngon!`
      : `${childShort} sắp đến giờ đi ngủ`;
  }
  if (t.includes('gia đình') || t.includes('cùng')) {
    return `Khoảnh khắc ấm áp bên gia đình`;
  }
  return done
    ? `${childShort} đã hoàn thành «${title}» rất tốt!`
    : `${childShort} đang làm «${title}»`;
}

function formatWindow(start?: string, end?: string): string | null {
  if (!start && !end) return null;
  const clean = (value?: string) => (value ? value.slice(0, 5) : '');
  if (start && end) return `${clean(start)} – ${clean(end)}`;
  return clean(start || end);
}

function taskCtaLabel(title: string, kind: 'overdue' | 'awaiting', flowDate: string): string {
  if (kind === 'awaiting') {
    return voicePick(`${flowDate}:cta:await:${title}`, ['Kiểm tra', 'Xác nhận', 'Duyệt sao']);
  }
  const t = title.toLowerCase();
  if (t.includes('cặp') || t.includes('balo') || t.includes('dọn')) {
    return voicePick(`${flowDate}:cta:tidy`, ['Hỗ trợ ngay', 'Neo tối nay', 'Nhắc nhẹ']);
  }
  if (t.includes('ngủ') || t.includes('thói quen')) {
    return voicePick(`${flowDate}:cta:sleep`, ['Nhắc giờ ngủ', 'Giữ neo giờ', 'Nhắc nhẹ']);
  }
  return voicePick(`${flowDate}:cta:nudge:${title}`, ['Nhắc ngay', 'Nhắc con', 'Gửi nhắc']);
}

function greetName(viewerName: string): string {
  const n = viewerName.trim();
  if (!n) return 'bố mẹ';
  if (/^mẹ/i.test(n) || /mẹ$/i.test(n)) return 'mẹ';
  if (/^bố/i.test(n) || /bố$/i.test(n) || /^ba/i.test(n)) return 'bố';
  return n;
}

type MemberSnap = {
  key: string;
  name: string;
  total: number;
  done: number;
  open: number;
  overdue: number;
  mood: 'great' | 'ok' | 'low';
};

function buildMemberSnaps(commitments: DayFlowCommitment[]): MemberSnap[] {
  const map = new Map<string, MemberSnap>();
  for (const c of commitments) {
    const key = c.memberId ?? c.memberName ?? 'house';
    const name = c.memberName?.trim() || 'Cả nhà';
    let row = map.get(key);
    if (!row) {
      row = { key, name, total: 0, done: 0, open: 0, overdue: 0, mood: 'ok' };
      map.set(key, row);
    }
    row.total += 1;
    if (c.status === 'done') row.done += 1;
    if (isOpen(c)) {
      row.open += 1;
      if (c.reminderState === 'overdue') row.overdue += 1;
    }
  }
  for (const row of map.values()) {
    const ratio = row.total > 0 ? row.done / row.total : 1;
    row.mood = row.overdue > 0 || ratio < 0.35 ? 'low' : ratio >= 0.7 ? 'great' : 'ok';
  }
  return [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

type MissionUxState = 'open' | 'awaiting_check' | 'done' | 'upcoming' | 'skipped';

function missionUxState(
  item: DayFlowCommitment,
  flowDate: string,
  verifiedTick: number,
): MissionUxState {
  void verifiedTick;
  if (item.status === 'skipped') return 'skipped';
  if (item.status === 'done') {
    if (
      needsParentApproval(item) &&
      item.evidenceUrl &&
      !isParentVerified(flowDate, item.id)
    ) {
      return 'awaiting_check';
    }
    return 'done';
  }
  if (item.evidenceUrl && needsParentApproval(item) && !isParentVerified(flowDate, item.id)) {
    return 'awaiting_check';
  }
  if (item.reminderState === 'overdue' || item.reminderState === 'due_now') return 'open';
  return 'upcoming';
}

type AttentionItem =
  | { kind: 'awaiting'; id: string; item: DayFlowCommitment }
  | { kind: 'overdue'; id: string; item: DayFlowCommitment }
  | { kind: 'consequence'; id: string; event: ConsequenceEvent };

type Props = {
  flow: DayFlow;
  viewerName: string;
  familyId: string;
  familyName?: string;
  /** Parent membership id — required to confirm Team Unlock. */
  parentMembershipId?: string;
  busyId: string | null;
  consequenceEvents: ConsequenceEvent[];
  glance: AccountabilityGlance | null;
  /** Children in the family — used for focus dropdown on parent Home. */
  children?: Array<{ id: string; displayName: string }>;
  parentPushSubscribed?: boolean;
  onEnableParentPush?: () => void;
  offerLocalReminders?: boolean;
  onEnableLocalReminders?: () => void;
  inAppChimeEnabled?: boolean;
  onToggleInAppChime?: () => void;
  onMarkDone: (item: DayFlowCommitment) => void | Promise<void>;
  onReflect: (item: DayFlowCommitment, reason: SkipReasonCode) => void;
  onReopen: (item: DayFlowCommitment) => void;
  onDecideConsequence: (
    eventId: string,
    status: 'applied' | 'waived',
  ) => Promise<SoftLockGuide | undefined>;
  onApproveStars?: (item: DayFlowCommitment) => Promise<void>;
  /** Leave parent board → Who-are-you (pick child). */
  onSwitchUser?: () => void;
  /** Reload day flow after Inbox approve (e.g. child day_mission). */
  onRefreshFlow?: () => void;
};

function commitmentMatchesChild(c: DayFlowCommitment, childKey: string): boolean {
  if (childKey === 'all') return true;
  if (c.memberId && c.memberId === childKey) return true;
  if (c.memberName?.trim() === childKey) return true;
  return false;
}

function starBalanceNote(balance: number): string {
  if (balance <= 0) return 'Con chưa có sao — khích lệ nhiệm vụ nhé!';
  if (balance < 100) return 'Đang tích lũy!';
  return 'Tiến bộ tuyệt vời!';
}

export function ParentBoardView({
  flow,
  viewerName,
  familyId,
  familyName = 'Nhà mình',
  parentMembershipId,
  busyId,
  consequenceEvents,
  glance,
  children: childrenProp = [],
  parentPushSubscribed = false,
  onEnableParentPush,
  offerLocalReminders = false,
  onEnableLocalReminders,
  inAppChimeEnabled = true,
  onToggleInAppChime,
  onMarkDone,
  onReflect: _onReflect,
  onReopen: _onReopen,
  onDecideConsequence,
  onApproveStars,
  onSwitchUser,
  onRefreshFlow,
}: Props) {
  void _onReflect;
  void _onReopen;

  const navigate = useNavigate();
  const [softGuide, setSoftGuide] = useState<SoftLockGuide | null>(null);
  const [missionFilter, setMissionFilter] = useState<MissionFilter>('all');
  const [tab, setTab] = useState<ParentTab>(() => {
    try {
      const raw = sessionStorage.getItem('famixa.parentTab');
      if (
        raw === 'home' ||
        raw === 'tasks' ||
        raw === 'rewards' ||
        raw === 'value' ||
        raw === 'diary'
      ) {
        sessionStorage.removeItem('famixa.parentTab');
        return raw;
      }
    } catch {
      /* ignore */
    }
    return 'home';
  });
  const [valueFocus, setValueFocus] = useState<string | null>(null);
  const [verifiedTick, setVerifiedTick] = useState(0);
  const [nudgeTick, setNudgeTick] = useState(0);
  const [moreOpen, setMoreOpen] = useState(false);
  const [childFocus, setChildFocus] = useState<string>('');
  const [childMenuOpen, setChildMenuOpen] = useState(false);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [unlockBusy, setUnlockBusy] = useState(false);
  const [unlockMsg, setUnlockMsg] = useState<string | null>(null);
  const [taskQuery, setTaskQuery] = useState('');
  const [taskSearchOpen, setTaskSearchOpen] = useState(false);
  const [waitingOpen, setWaitingOpen] = useState(true);
  const [treasureToast, setTreasureToast] = useState<string | null>(null);
  const [diaryToast, setDiaryToast] = useState<string | null>(null);
  const [actionToast, setActionToast] = useState<string | null>(null);
  const [modeSheetOpen, setModeSheetOpen] = useState(false);
  const [familyScore, setFamilyScore] = useState<FamilyScore | null>(null);
  const [familyTwin, setFamilyTwin] = useState<FamilyBehaviorTwin | null>(null);
  const [coachInsight, setCoachInsight] = useState<FamilyCoachInsight | null>(null);
  const [behaviorCoach, setBehaviorCoach] = useState<BehaviorCoach | null>(null);
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [paywallOpen, setPaywallOpen] = useState(false);
  const [paywallReason, setPaywallReason] = useState<string | null>(null);
  const [eveningCheckin, setEveningCheckin] = useState<ParentSuccessCheckin | null>(null);
  const [actedTipIds, setActedTipIds] = useState<string[]>([]);
  const [coachActBusyId, setCoachActBusyId] = useState<string | null>(null);
  const [inboxTick, setInboxTick] = useState(0);
  const [coachOpen, setCoachOpen] = useState(false);
  const [diaryDayIdx, setDiaryDayIdx] = useState(2);
  const [diaryFilter, setDiaryFilter] = useState<DiaryFilter>('all');
  const [diaryExpanded, setDiaryExpanded] = useState(false);
  const [diaryMomentIdx, setDiaryMomentIdx] = useState(0);
  const [diarySearchOpen, setDiarySearchOpen] = useState(false);
  const [diaryQuery, setDiaryQuery] = useState('');
  const [diaryMemoriesOpen, setDiaryMemoriesOpen] = useState(false);
  const [childGratitudes, setChildGratitudes] = useState<ChildGratitude[]>([]);
  const [savedMemories, setSavedMemories] = useState<FamilyMemoryEntry[]>([]);
  const [memoryRecap, setMemoryRecap] = useState<FamilyMemoryRecap | null>(null);
  const [memoryHeartBusy, setMemoryHeartBusy] = useState<string | null>(null);
  const [addMemoryOpen, setAddMemoryOpen] = useState(false);
  const [addMemoryTitle, setAddMemoryTitle] = useState('');
  const [addMemoryNote, setAddMemoryNote] = useState('');
  const [addMemoryBusy, setAddMemoryBusy] = useState(false);
  const [diaryFavoritesOnly, setDiaryFavoritesOnly] = useState(false);
  const achievementsRef = useRef<HTMLElement | null>(null);
  const [childStarBalance, setChildStarBalance] = useState(0);
  const [rewardCatalog, setRewardCatalog] = useState<RewardCatalogItem[]>([]);
  const [childRedemptions, setChildRedemptions] = useState<RewardRedemption[]>([]);
  const [fulfillBusyId, setFulfillBusyId] = useState<string | null>(null);
  const [familyMoods, setFamilyMoods] = useState<FamilyMemberMood[]>([]);
  const [treasureHistoryOpen, setTreasureHistoryOpen] = useState(false);
  const [verifyingId, setVerifyingId] = useState<string | null>(null);
  const childMenuRef = useRef<HTMLDivElement>(null);
  const diaryDatesRef = useRef<HTMLDivElement>(null);

  const childOptions = useMemo(() => {
    const map = new Map<string, { key: string; name: string }>();
    for (const c of childrenProp) {
      map.set(c.id, { key: c.id, name: c.displayName });
    }
    for (const c of flow.commitments) {
      if (!c.memberId && !c.memberName) continue;
      const key = c.memberId ?? c.memberName!.trim();
      const name = c.memberName?.trim() || 'Con';
      if (!map.has(key)) map.set(key, { key, name });
    }
    return [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
  }, [childrenProp, flow.commitments]);

  const effectiveChildFocus =
    childFocus || (childOptions.length > 0 ? childOptions[0].key : 'all');

  useEffect(() => {
    if (!childFocus && childOptions[0]) setChildFocus(childOptions[0].key);
  }, [childFocus, childOptions]);

  useEffect(() => {
    if (effectiveChildFocus === 'all') return;
    if (!childOptions.some((c) => c.key === effectiveChildFocus)) {
      setChildFocus(childOptions[0]?.key ?? 'all');
    }
  }, [effectiveChildFocus, childOptions]);

  useEffect(() => {
    setChildMenuOpen(false);
  }, [tab]);

  useEffect(() => {
    if (!childMenuOpen) return;
    const onDoc = (e: MouseEvent) => {
      if (!childMenuRef.current?.contains(e.target as Node)) setChildMenuOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [childMenuOpen]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        await fetchTeamUnlocks(familyId, flow.flowDate, true);
        const rows = await fetchTeamUnlocks(familyId);
        if (!cancelled) setTeamUnlocks(rows);
      } catch {
        if (!cancelled) setTeamUnlocks([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate, flow.doneCount, flow.pendingCount]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void (async () => {
      try {
        const [rows, recap] = await Promise.all([
          fetchFamilyMemories(familyId, { limit: 40 }),
          fetchFamilyMemoryRecap(familyId).catch(() => null),
        ]);
        if (cancelled) return;
        setSavedMemories(rows);
        setMemoryRecap(recap);
      } catch {
        if (!cancelled) {
          setSavedMemories([]);
          setMemoryRecap(null);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate, flow.doneCount]);

  const openMemoriesSheet = () => setDiaryMemoriesOpen(true);

  const toggleMemoryFavorite = async (memory: FamilyMemoryEntry) => {
    const next = !memory.isFavorite;
    setSavedMemories((prev) =>
      prev.map((m) => (m.id === memory.id ? { ...m, isFavorite: next } : m)),
    );
    try {
      await setFamilyMemoryFavorite(familyId, memory.id, next);
    } catch {
      setSavedMemories((prev) =>
        prev.map((m) => (m.id === memory.id ? { ...m, isFavorite: !next } : m)),
      );
    }
  };

  const treasureMemberId =
    effectiveChildFocus !== 'all'
      ? effectiveChildFocus
      : childOptions[0]?.key ?? null;

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void fetchRewardCatalog(familyId)
      .then((rows) => {
        if (!cancelled) setRewardCatalog(rows);
      })
      .catch(() => {
        if (!cancelled) setRewardCatalog([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId]);

  useEffect(() => {
    if (!familyId || !treasureMemberId) {
      setChildStarBalance(0);
      setChildRedemptions([]);
      return;
    }
    let cancelled = false;
    void Promise.all([
      fetchMemberStarBalance(familyId, treasureMemberId),
      fetchRewardRedemptions(familyId, treasureMemberId),
    ])
      .then(([balance, rows]) => {
        if (cancelled) return;
        setChildStarBalance(balance);
        setChildRedemptions(rows);
      })
      .catch(() => {
        if (!cancelled) {
          setChildStarBalance(0);
          setChildRedemptions([]);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, treasureMemberId, flow.doneCount]);

  useEffect(() => {
    let cancelled = false;
    void fetchChildGratitude(familyId, flow.flowDate)
      .then((rows) => {
        if (cancelled) return;
        setChildGratitudes(rows);
        for (const row of rows) {
          if (!row.readAt) {
            void markChildGratitudeRead(familyId, row.id).catch(() => {});
          }
        }
      })
      .catch(() => {
        if (!cancelled) setChildGratitudes([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate, flow.doneCount]);

  useEffect(() => {
    let cancelled = false;
    void fetchFamilySubscription(familyId)
      .then((s) => {
        if (!cancelled) setSubscription(s);
      })
      .catch(() => {
        if (!cancelled) setSubscription(null);
      });
    void fetchFamilyScore(familyId)
      .then((s) => {
        if (!cancelled) setFamilyScore(s);
      })
      .catch(() => {
        if (!cancelled) setFamilyScore(null);
      });
    void fetchFamilyBehaviorTwin(familyId)
      .then((t) => {
        if (!cancelled) setFamilyTwin(t);
      })
      .catch(() => {
        if (!cancelled) setFamilyTwin(null);
      });
    void fetchFamilyCoachInsight(familyId, flow.flowDate)
      .then((c) => {
        if (!cancelled) setCoachInsight(c);
      })
      .catch(() => {
        if (!cancelled) setCoachInsight(null);
      });
    void fetchBehaviorCoach(familyId, flow.flowDate)
      .then((c) => {
        if (!cancelled) setBehaviorCoach(c);
      })
      .catch(() => {
        if (!cancelled) setBehaviorCoach(null);
      });
    if (parentMembershipId) {
      void fetchParentSuccessEveningCheckin(familyId, parentMembershipId, flow.flowDate)
        .then((r) => {
          if (cancelled) return;
          setEveningCheckin(r);
        })
        .catch(() => {
          if (!cancelled) setEveningCheckin(null);
        });
      void fetchParentCoachActedToday(familyId, parentMembershipId, flow.flowDate)
        .then((r) => {
          if (!cancelled) setActedTipIds(r.actedTipIdsToday);
        })
        .catch(() => {
          if (!cancelled) setActedTipIds([]);
        });
    } else if (!cancelled) {
      setEveningCheckin(null);
      setActedTipIds([]);
    }
    void scanAdaptiveProposals(familyId)
      .then((n) => {
        if (!cancelled && n > 0) setInboxTick((t) => t + 1);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate, flow.doneCount, parentMembershipId]);

  const todayUnlock = useMemo(
    () =>
      teamUnlocks.find((u) => u.flowDate === flow.flowDate) ??
      teamUnlocks[0] ??
      null,
    [teamUnlocks, flow.flowDate],
  );

  const onDecideUnlockById = async (
    unlockId: string,
    status: 'confirmed' | 'deferred',
  ) => {
    if (!parentMembershipId) {
      setUnlockMsg('Thiếu hồ sơ phụ huynh để xác nhận.');
      return;
    }
    setUnlockBusy(true);
    setUnlockMsg(null);
    try {
      const updated = await confirmTeamUnlock(familyId, unlockId, {
        confirmedBy: parentMembershipId,
        status,
      });
      setTeamUnlocks((prev) => prev.map((u) => (u.id === updated.id ? updated : u)));
      setUnlockMsg(
        status === 'confirmed'
          ? `Đã mở ${updated.labelVi} — cả nhà cùng tận hưởng!`
          : 'Đã để sau — vẫn giữ phần thưởng khi nhà sẵn sàng.',
      );
      setInboxTick((t) => t + 1);
    } catch {
      setUnlockMsg('Chưa xác nhận được. Thử lại nhé.');
    } finally {
      setUnlockBusy(false);
    }
  };

  const onDecideUnlock = async (status: 'confirmed' | 'deferred') => {
    if (!todayUnlock) {
      setUnlockMsg('Chưa có phần thưởng đội để xác nhận.');
      return;
    }
    await onDecideUnlockById(todayUnlock.id, status);
  };

  const scopedCommitments = useMemo(
    () => flow.commitments.filter((c) => commitmentMatchesChild(c, effectiveChildFocus)),
    [flow.commitments, effectiveChildFocus],
  );

  const members = useMemo(() => buildMemberSnaps(scopedCommitments), [scopedCommitments]);
  const allMembers = useMemo(() => buildMemberSnaps(flow.commitments), [flow.commitments]);

  const scopedDone = scopedCommitments.filter((c) => c.status === 'done').length;
  const scopedTotal = scopedCommitments.length;
  const percent =
    scopedTotal > 0 ? Math.round((scopedDone / scopedTotal) * 100) : 0;
  const unlockGap = Math.max(0, Math.min(3, Math.max(0, flow.totalCommitments - flow.doneCount)));
  const focusChild =
    effectiveChildFocus === 'all'
      ? [...allMembers].sort((a, b) => b.done - a.done)[0]
      : members[0] ?? allMembers.find((m) => m.key === effectiveChildFocus);
  const selectedChild =
    effectiveChildFocus === 'all'
      ? null
      : childOptions.find((c) => c.key === effectiveChildFocus) ?? null;
  const parentAvatar =
    greetName(viewerName) === 'mẹ'
      ? '👩'
      : greetName(viewerName) === 'bố'
        ? '👨'
        : avatarEmoji(inferGenderFromName(viewerName), 'parent');

  const pendingEvents = useMemo(
    () => consequenceEvents.filter((e) => e.status === 'pending_confirm'),
    [consequenceEvents],
  );
  const appliedScreen = useMemo(
    () =>
      consequenceEvents.filter(
        (e) => e.status === 'applied' && isScreenBoundaryCode(e.consequenceCode),
      ),
    [consequenceEvents],
  );

  /** Refresh nudge KPI UI after a nudge was already recorded locally/API. */
  const onParentNudged = (_count?: number) => {
    setNudgeTick((t) => t + 1);
  };

  const nudgeToday = useMemo(() => {
    void nudgeTick;
    return getNudgeCount(familyId, flow.flowDate);
  }, [familyId, flow.flowDate, nudgeTick]);

  const nudgeYesterday = useMemo(() => {
    void nudgeTick;
    const yDate = previousCalendarDate(flow.flowDate);
    const stored = getNudgeCount(familyId, yDate);
    if (stored > 0) return stored;
    const y = glance?.days.find((d) => d.date === yDate);
    if (y) return Math.max(0, y.childOpen + y.childLateDone);
    return 0;
  }, [familyId, flow.flowDate, glance?.days, nudgeTick]);

  const rewardPoints = childStarBalance;
  const streak = glance?.currentStreak ?? 0;
  const familyXp = Math.min(500, rewardPoints * 2 + scopedDone * 40);
  const familyLevel = Math.max(
    1,
    Math.min(4, Math.floor(familyXp / 100) + (rewardPoints >= 80 ? 2 : 1)),
  );
  const explorerLevel = Math.max(
    1,
    Math.min(12, familyLevel + Math.floor(rewardPoints / 40) + Math.min(4, Math.max(0, streak))),
  );
  const treasureLevel = explorerLevel;
  const treasureXpHave = Math.min(3000, Math.max(0, rewardPoints));
  const treasureXpPct = Math.min(100, Math.round((treasureXpHave / 3000) * 100));
  const pendingRedemptions = useMemo(
    () => childRedemptions.filter((r) => r.status === 'pending'),
    [childRedemptions],
  );

  const attentionItems = useMemo(() => {
    const list: AttentionItem[] = [];
    for (const e of pendingEvents.slice(0, 2)) {
      if (
        effectiveChildFocus !== 'all' &&
        e.memberName &&
        selectedChild &&
        e.memberName.trim() !== selectedChild.name
      ) {
        continue;
      }
      list.push({ kind: 'consequence', id: e.id, event: e });
    }
    for (const c of scopedCommitments) {
      const state = missionUxState(c, flow.flowDate, verifiedTick);
      if (state === 'awaiting_check') {
        list.push({ kind: 'awaiting', id: `await-${c.id}`, item: c });
      }
    }
    for (const c of scopedCommitments) {
      if (missionUxState(c, flow.flowDate, verifiedTick) !== 'open') continue;
      if (list.some((x) => x.kind !== 'consequence' && x.item.id === c.id)) continue;
      list.push({ kind: 'overdue', id: `hot-${c.id}`, item: c });
    }
    return list.slice(0, 6);
  }, [
    pendingEvents,
    scopedCommitments,
    flow.flowDate,
    verifiedTick,
    effectiveChildFocus,
    selectedChild,
  ]);

  const parentRole = useMemo(() => parentRoleFromName(viewerName), [viewerName]);

  const resolvedCoach = useMemo(
    () =>
      resolveParentCoach({
        familyId,
        flow: { ...flow, commitments: scopedCommitments },
        glance,
        nudgeToday,
        focusChildName: selectedChild?.name ?? null,
        coachInsight,
        familyTwin,
        behaviorCoach,
      }),
    [
      familyId,
      flow,
      scopedCommitments,
      glance,
      nudgeToday,
      selectedChild?.name,
      coachInsight,
      familyTwin,
      behaviorCoach,
    ],
  );

  const coach = resolvedCoach.primary;

  const parentPulse = useMemo(
    () =>
      buildParentPulse({
        flow: { ...flow, commitments: scopedCommitments },
        twin: familyTwin,
        familyScore,
        nudgeToday,
        nudgeYesterday,
        coachInsight: coachInsight
          ? {
              headline: coachInsight.headline,
              strength: coachInsight.strength ?? coach.insight,
              proposal: coachInsight.proposal ?? coach.doThis,
            }
          : {
              headline: coach.insight,
              strength: coach.insight,
              proposal: coach.doThis,
            },
      }),
    [
      flow,
      scopedCommitments,
      familyTwin,
      familyScore,
      nudgeToday,
      nudgeYesterday,
      coachInsight,
      coach.insight,
      coach.doThis,
    ],
  );

  const memoryWinVi = useMemo(
    () => pickMemoryWinVi(savedMemories, flow.flowDate),
    [savedMemories, flow.flowDate],
  );

  const homeBrief = useMemo(() => {
    const who =
      (selectedChild?.name ?? focusChild?.name ?? 'Con').trim().split(/\s+/).pop() || 'Con';
    const top = attentionItems[0];
    const topAttention = top
      ? {
          kind: top.kind,
          id: top.id,
          titleVi:
            top.kind === 'consequence'
              ? top.event.labelVi
              : top.item.title,
          detailVi:
            top.kind === 'consequence'
              ? `${(top.event.memberName?.trim() || who)} · chờ quyết định`
              : top.kind === 'awaiting'
                ? 'Cần xác nhận bằng chứng / sao'
                : top.item.windowEnd
                  ? `Chưa xong · trước ${top.item.windowEnd.slice(0, 5)}`
                  : 'Chưa xong / quá giờ',
        }
      : null;
    return buildHomeBrief({
      pulse: parentPulse,
      coach,
      attentionCount: attentionItems.length,
      topAttention,
      localTime: flow.localTime,
      eveningCheckinDone: Boolean(eveningCheckin),
      memoryWinVi,
    });
  }, [
    parentPulse,
    coach,
    attentionItems,
    flow.localTime,
    eveningCheckin,
    selectedChild?.name,
    focusChild?.name,
    memoryWinVi,
  ]);

  const homeAttention = useMemo(
    () => attentionItems.slice(0, 3),
    [attentionItems],
  );

  const homeFeed = useMemo(
    () =>
      buildHomeFamilyFeed({
        flowDate: flow.flowDate,
        gratitudes: childGratitudes,
        memories: savedMemories,
        doneCommitments: scopedCommitments,
        redemptions: childRedemptions,
        max: 4,
      }),
    [flow.flowDate, childGratitudes, savedMemories, scopedCommitments, childRedemptions],
  );

  const parentHelloLabel =
    parentRole === 'bố' ? 'Bố' : parentRole === 'mẹ' ? 'Mẹ' : 'Bố mẹ';

  const homeMoment = useMemo(() => {
    const gratitude = childGratitudes[0];
    if (gratitude) {
      return {
        id: `grat-${gratitude.id}`,
        titleVi: gratitude.messageVi,
        detailVi:
          (gratitude.fromMemberName || 'Con') +
          (gratitude.praiseContext ? ` · vì «${gratitude.praiseContext}»` : ''),
        photoUrl: undefined as string | undefined,
      };
    }
    const mem = savedMemories.find((m) => m.flowDate === flow.flowDate) ?? savedMemories[0];
    if (mem) {
      return {
        id: `mem-${mem.id}`,
        titleVi: mem.titleVi,
        detailVi: mem.noteVi || mem.memberName,
        photoUrl: mem.photoUrl,
      };
    }
    const feed = homeFeed[0];
    if (feed) {
      return {
        id: feed.id,
        titleVi: feed.titleVi,
        detailVi: feed.detailVi,
        photoUrl: undefined as string | undefined,
      };
    }
    return null;
  }, [childGratitudes, savedMemories, flow.flowDate, homeFeed]);

  const progressSegments = useMemo(() => {
    const total = Math.min(Math.max(scopedTotal, 0), 10);
    const segs = total > 0 ? total : 10;
    const filled =
      scopedTotal > 0
        ? Math.round((scopedDone / Math.max(scopedTotal, 1)) * segs)
        : 0;
    return { segs, filled };
  }, [scopedDone, scopedTotal]);

  const peaceDaysLeft = useMemo(() => {
    if (!subscription) return null;
    if (subscription.trialDaysRemaining != null) return subscription.trialDaysRemaining;
    const iso = subscription.trialEndsAt || subscription.currentPeriodEnd;
    if (!iso) return null;
    const t = Date.parse(iso);
    if (Number.isNaN(t)) return null;
    return Math.max(0, Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000)));
  }, [subscription]);

  const goPeaceCheckout = () => {
    navigate(
      buildCheckoutPath({
        productCode: 'family_os',
        subjectType: 'family',
        subjectId: familyId,
        planCode: subscription?.recommendedUpgradePlanCode || 'family_pro_month',
        returnPath: '/today',
      }),
    );
  };

  const buckets = useMemo(() => {
    const done: DayFlowCommitment[] = [];
    const waiting: DayFlowCommitment[] = [];
    const upcoming: DayFlowCommitment[] = [];
    const open: DayFlowCommitment[] = [];
    for (const c of scopedCommitments) {
      const state = missionUxState(c, flow.flowDate, verifiedTick);
      if (state === 'done') done.push(c);
      else if (state === 'awaiting_check') waiting.push(c);
      else if (state === 'upcoming') upcoming.push(c);
      else if (state === 'open') open.push(c);
      else waiting.push(c);
    }
    return { done, waiting, upcoming, open };
  }, [scopedCommitments, flow.flowDate, verifiedTick]);

  const needHelpItems = useMemo(
    () => [...buckets.open, ...buckets.waiting],
    [buckets.open, buckets.waiting],
  );
  const waitingChildItems = useMemo(() => buckets.upcoming, [buckets.upcoming]);
  const doneTodayItems = useMemo(() => buckets.done, [buckets.done]);

  const filteredMissions = useMemo(() => {
    const q = taskQuery.trim().toLowerCase();
    const match = (c: DayFlowCommitment) => !q || c.title.toLowerCase().includes(q);
    if (missionFilter === 'done') return doneTodayItems.filter(match);
    if (missionFilter === 'need_help') return needHelpItems.filter(match);
    if (missionFilter === 'waiting_child') return waitingChildItems.filter(match);
    return [...needHelpItems, ...waitingChildItems, ...doneTodayItems].filter(match);
  }, [missionFilter, needHelpItems, waitingChildItems, doneTodayItems, taskQuery]);

  const verifyItem = async (item: DayFlowCommitment) => {
    if (verifyingId === item.id) return;
    if (busyId && busyId !== item.id) {
      throw new Error('approve_stars_busy');
    }
    setVerifyingId(item.id);
    try {
      if (isOpen(item)) {
        await Promise.resolve(onMarkDone(item));
      }
      if (onApproveStars) {
        await onApproveStars(item);
      } else {
        await approveCommitmentStars(familyId, item.id);
      }
      markParentVerified(flow.flowDate, item.id);
      setVerifiedTick((t) => t + 1);
      showDiaryToast(`Đã xác nhận «${item.title}»!`);
    } catch (err) {
      showDiaryToast('Chưa duyệt được sao — thử lại nhé.');
      throw err;
    } finally {
      setVerifyingId(null);
    }
  };

  const scrollToMissions = (filter: MissionFilter = 'all') => {
    setTab('tasks');
    setMissionFilter(filter);
    window.requestAnimationFrame(() => {
      document.getElementById('ph-missions')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  };

  const childShort =
    (selectedChild?.name ?? focusChild?.name ?? 'Con').trim().split(/\s+/).pop() || 'Con';
  const childAvatar = avatarEmoji(
    inferGenderFromName(selectedChild?.name ?? focusChild?.name ?? childShort),
    'child',
  );

  const diaryDays = useMemo(() => {
    const base = new Date(`${flow.flowDate}T12:00:00`);
    if (Number.isNaN(base.getTime())) return [];
    const dow = base.getDay();
    const mondayOffset = dow === 0 ? -6 : 1 - dow;
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(base);
      d.setDate(base.getDate() + mondayOffset + i);
      const dd = String(d.getDate()).padStart(2, '0');
      const mm = String(d.getMonth() + 1).padStart(2, '0');
      const key = `${d.getFullYear()}-${mm}-${dd}`;
      const isToday = key === flow.flowDate;
      return {
        key,
        shortLabel: isToday
          ? `Hôm nay ${dd}/${mm}`
          : `${DIARY_WD_SHORT[d.getDay()]} ${dd}/${mm}`,
        fullLabel: isToday
          ? `Hôm nay, ${dd}/${mm}/${d.getFullYear()}`
          : `${WEEKDAYS_VI[d.getDay()]}, ${dd}/${mm}/${d.getFullYear()}`,
        isToday,
      };
    });
  }, [flow.flowDate]);

  useEffect(() => {
    const idx = diaryDays.findIndex((d) => d.isToday);
    if (idx >= 0) setDiaryDayIdx(idx);
  }, [diaryDays]);

  const selectedDiaryDay =
    diaryDays[Math.min(Math.max(diaryDayIdx, 0), Math.max(diaryDays.length - 1, 0))] ??
    diaryDays[0];

  const moodFetchDate =
    tab === 'diary' && selectedDiaryDay ? selectedDiaryDay.key : flow.flowDate;

  useEffect(() => {
    let cancelled = false;
    void fetchFamilyMoods(familyId, moodFetchDate)
      .then((rows) => {
        if (!cancelled) setFamilyMoods(rows);
      })
      .catch(() => {
        if (!cancelled) setFamilyMoods([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, moodFetchDate, flow.doneCount]);

  const focusedChildMood = useMemo(() => {
    if (!treasureMemberId) return null;
    return familyMoods.find((m) => m.memberId === treasureMemberId) ?? null;
  }, [familyMoods, treasureMemberId]);

  const focusedMoodDisplay = useMemo(
    () => (focusedChildMood ? moodFromCode(focusedChildMood.moodCode) : null),
    [focusedChildMood],
  );

  const diaryEntries = useMemo(() => {
    const rows = [...scopedCommitments]
      .sort((a, b) => {
        const aw = a.windowStart || a.windowEnd || '99:99';
        const bw = b.windowStart || b.windowEnd || '99:99';
        return aw.localeCompare(bw);
      })
      .map((item) => {
        const state = missionUxState(item, flow.flowDate, verifiedTick);
        const done = state === 'done';
        const wait = state === 'awaiting_check';
        const skipped = item.status === 'skipped';
        const tag = diaryTag(item.title);
        return {
          item,
          part: diaryDayPart(item),
          time: diaryTimeLabel(item),
          done,
          wait,
          skipped,
          pending: !done && !wait && !skipped,
          note: diaryNote(item.title, childShort, done),
          reward: parentDisplayDelta(item),
          starLabel: item.starLabelVi
            ? normalizeLateStarLabelVi(item.starLabelVi)
            : parentStarBadgeText(item, parentDisplayDelta(item)),
          lateCaption: parentLateCaption(item, flow.flowDate),
          category: diaryCategory(item.title),
          tag,
          tone: diaryIconTone(item.title),
        };
      });
    if (diaryFilter === 'all') return rows;
    return rows.filter((r) => r.category === diaryFilter);
  }, [scopedCommitments, flow.flowDate, verifiedTick, childShort, diaryFilter]);

  const diaryFilteredEntries = useMemo(() => {
    const q = diaryQuery.trim().toLowerCase();
    if (!q) return diaryEntries;
    return diaryEntries.filter(
      (e) =>
        e.item.title.toLowerCase().includes(q) ||
        e.note.toLowerCase().includes(q) ||
        e.tag.label.toLowerCase().includes(q),
    );
  }, [diaryEntries, diaryQuery]);

  const diaryVisible = diaryExpanded ? diaryFilteredEntries : diaryFilteredEntries.slice(0, 5);
  const diaryStarsEarned = useMemo(
    () =>
      scopedCommitments
        .filter((c) => c.status === 'done' && c.starPosted)
        .reduce((s, c) => s + (c.starDelta ?? 0), 0),
    [scopedCommitments],
  );
  const diaryStarsPending = useMemo(
    () =>
      scopedCommitments
        .filter((c) => {
          const state = missionUxState(c, flow.flowDate, verifiedTick);
          return state === 'awaiting_check' || (c.status === 'done' && !c.starPosted);
        })
        .reduce((s, c) => s + (c.projectedStarDelta ?? parentDisplayDelta(c)), 0),
    [scopedCommitments, flow.flowDate, verifiedTick],
  );

  const diaryPrettyMemories = useMemo(() => {
    const all = buildFamilyMemories({
      childShort,
      redemptions: childRedemptions,
      teamUnlocks,
      doneItems: scopedCommitments.filter((c) => c.status === 'done'),
      saved: savedMemories,
      voice: 'parent',
    });
    return diaryFavoritesOnly ? all.filter((m) => m.entry?.isFavorite) : all;
  }, [
    childShort,
    childRedemptions,
    teamUnlocks,
    scopedCommitments,
    savedMemories,
    diaryFavoritesOnly,
  ]);

  const diaryFeatureMoments = useMemo(
    () =>
      diaryPrettyMemories.slice(0, 5).map((m) => ({
        id: m.id,
        icon: m.icon,
        title: m.title,
        date: m.date,
        caption: m.pending
          ? 'Chờ bố mẹ xác nhận'
          : m.entry?.noteVi || (m.entry?.isFavorite ? 'Đã gắn tim' : 'Kỷ niệm gia đình'),
        memory: m,
      })),
    [diaryPrettyMemories],
  );

  useEffect(() => {
    setDiaryMomentIdx(0);
  }, [diaryFeatureMoments.length, effectiveChildFocus]);

  const diaryMemoriesVisible = useMemo(
    () => diaryPrettyMemories.slice(0, FAMILY_MEMORY_VISIBLE),
    [diaryPrettyMemories],
  );

  const showDiaryToast = (msg: string) => {
    setDiaryToast(msg);
    window.setTimeout(() => setDiaryToast(null), 2200);
  };

  const showTreasureToast = (msg: string) => {
    setTreasureToast(msg);
    window.setTimeout(() => setTreasureToast(null), 2200);
  };

  const showActionToast = (msg: string) => {
    setActionToast(msg);
    window.setTimeout(() => setActionToast(null), 2800);
  };

  /** Heart works for saved memories; for derived cards, pin them into Family Memories first. */
  const heartMemory = async (mem: FamilyMemory) => {
    if (memoryHeartBusy) return;
    setMemoryHeartBusy(mem.id);
    try {
      if (mem.entry) {
        await toggleMemoryFavorite(mem.entry);
        return;
      }
      const created = await createFamilyMemory(familyId, {
        titleVi: mem.title,
        kind: 'manual',
        icon: mem.icon,
        noteVi: 'Bố mẹ lưu từ khoảnh khắc trong ngày',
        flowDate: flow.flowDate,
      });
      await setFamilyMemoryFavorite(familyId, created.id, true);
      setSavedMemories((prev) => [{ ...created, isFavorite: true }, ...prev]);
      showDiaryToast('Đã lưu vào kỷ niệm gia đình ❤️');
    } catch {
      showDiaryToast('Chưa lưu được — thử lại nhé');
    } finally {
      setMemoryHeartBusy(null);
    }
  };

  const submitManualMemory = async () => {
    const title = addMemoryTitle.trim();
    if (!title || addMemoryBusy) return;
    setAddMemoryBusy(true);
    try {
      const created = await createFamilyMemory(familyId, {
        titleVi: title,
        noteVi: addMemoryNote.trim() || undefined,
        kind: 'manual',
        icon: '💛',
        flowDate: flow.flowDate,
      });
      setSavedMemories((prev) => [created, ...prev]);
      setAddMemoryTitle('');
      setAddMemoryNote('');
      setAddMemoryOpen(false);
      showDiaryToast('Đã thêm kỷ niệm mới');
      setTab('diary');
      openMemoriesSheet();
    } catch {
      showDiaryToast('Chưa thêm được — thử lại nhé');
    } finally {
      setAddMemoryBusy(false);
    }
  };

  const goDiaryDay = (delta: -1 | 1) => {
    setTab('diary');
    setDiaryDayIdx((idx) => {
      const next = Math.min(Math.max(idx + delta, 0), Math.max(diaryDays.length - 1, 0));
      return next;
    });
    window.setTimeout(() => {
      diaryDatesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }, 50);
  };

  const childFocusLabel =
    effectiveChildFocus === 'all'
      ? 'Cả nhà'
      : selectedChild?.name ?? childShort;

  const renderChildPicker = (tone: 'home' | 'module' = 'home') => (
    <div
      className={`ph-child-picker${tone === 'module' ? ' is-module' : ''}`}
      ref={childMenuRef}
    >
      <button
        type="button"
        className={tone === 'module' ? 'ph-child-picker-btn' : 'ph-cal-btn'}
        aria-haspopup="listbox"
        aria-expanded={childMenuOpen}
        aria-label="Chọn thành viên đang xem"
        title="Đổi xem thành viên / sang màn hình con"
        onClick={() => setChildMenuOpen((v) => !v)}
      >
        <span aria-hidden>
          {effectiveChildFocus === 'all'
            ? '🏡'
            : avatarEmoji(inferGenderFromName(childFocusLabel), 'child')}
        </span>
        {tone === 'module' ? <em>{childFocusLabel}</em> : null}
      </button>
      {childMenuOpen ? (
        <ul className="ph-child-menu" role="listbox" aria-label="Chọn thành viên">
          <li role="option" aria-selected={effectiveChildFocus === 'all'}>
            <button
              type="button"
              className={effectiveChildFocus === 'all' ? 'is-on' : undefined}
              onClick={() => {
                setChildFocus('all');
                setChildMenuOpen(false);
              }}
            >
              <span aria-hidden>🏡</span>
              Cả nhà
            </button>
          </li>
          {childOptions.map((c) => (
            <li key={c.key} role="option" aria-selected={effectiveChildFocus === c.key}>
              <button
                type="button"
                className={effectiveChildFocus === c.key ? 'is-on' : undefined}
                onClick={() => {
                  setChildFocus(c.key);
                  setChildMenuOpen(false);
                }}
              >
                <span aria-hidden>{avatarEmoji(inferGenderFromName(c.name), 'child')}</span>
                {c.name}
              </button>
            </li>
          ))}
          {onSwitchUser ? (
            <li className="ph-child-menu-switch" role="presentation">
              <button
                type="button"
                onClick={() => {
                  setChildMenuOpen(false);
                  onSwitchUser();
                }}
              >
                <span aria-hidden>🔄</span>
                Sang màn hình con…
              </button>
            </li>
          ) : null}
        </ul>
      ) : null}
    </div>
  );

  const treasureMemories = useMemo(
    () =>
      buildFamilyMemories({
        childShort,
        redemptions: childRedemptions,
        teamUnlocks,
        doneItems: scopedCommitments.filter((c) => c.status === 'done'),
        saved: savedMemories,
        voice: 'parent',
      }).map((m) => ({
        id: m.id,
        icon: m.icon,
        title: m.title,
        time: m.date,
        pending: m.pending,
        memory: m,
        redemptionId:
          m.id.startsWith('redeem-') ? m.id.slice('redeem-'.length) : undefined,
      })),
    [childRedemptions, teamUnlocks, scopedCommitments, childShort, savedMemories],
  );

  const treasureFamilyGoals = useMemo(() => {
    const unlockGoals = teamUnlocks
      .filter((u) => u.status !== 'deferred')
      .slice(0, 5)
      .map((u) => {
        const isToday = u.flowDate === flow.flowDate;
        const pct =
          u.status === 'confirmed'
            ? 100
            : isToday
              ? percent
              : u.status === 'pending_confirm'
                ? 100
                : 0;
        const code = u.rewardCode.toLowerCase();
        const label = u.labelVi.toLowerCase();
        const icon =
          code.includes('movie') || label.includes('phim') || label.includes('movie')
            ? '🍿'
            : '🎉';
        return {
          id: u.id,
          icon,
          title: u.labelVi || 'Phần thưởng nhà',
          pct,
          locked: false,
        };
      });
    if (unlockGoals.length > 0) return unlockGoals;
    if (todayUnlock) {
      return [
        {
          id: todayUnlock.id,
          icon: '🍿',
          title: todayUnlock.labelVi || 'Movie Night',
          pct: percent,
          locked: false,
        },
      ];
    }
    return [];
  }, [teamUnlocks, flow.flowDate, percent, todayUnlock]);

  const bigAchievements = useMemo(() => {
    const movieTimes = teamUnlocks.filter((u) => u.status === 'confirmed').length;
    const readTimes = scopedCommitments.filter(
      (c) => c.status === 'done' && /đọc|sách/i.test(c.title),
    ).length;
    return [
      {
        id: 'a1',
        icon: '🎬',
        title: 'Movie Night',
        value: movieTimes > 0 ? `${movieTimes} lần` : '—',
      },
      {
        id: 'a2',
        icon: '📘',
        title: 'Đọc sách cùng mẹ',
        value: readTimes > 0 ? `${readTimes} lần` : '—',
      },
      {
        id: 'a3',
        icon: '🌱',
        title: 'Khu vườn',
        value: `Cấp ${explorerLevel}`,
      },
      {
        id: 'a4',
        icon: '🦊',
        title: 'Foxy',
        value: `${rewardPoints.toLocaleString('vi-VN')} — ${starBalanceNote(rewardPoints)}`,
      },
      {
        id: 'a5',
        icon: '⭐',
        title: 'Sao đã tích lũy',
        value: `${rewardPoints.toLocaleString('vi-VN')}`,
      },
    ];
  }, [teamUnlocks, scopedCommitments, explorerLevel, rewardPoints]);

  const hasCap = (cap: string) => {
    const caps = subscription?.capabilities;
    if (!caps || caps.length === 0) return true; // optimistic until pack loads
    return caps.some((c) => c.toLowerCase() === cap.toLowerCase());
  };

  const openPaywall = (reason?: string | null) => {
    setPaywallReason(reason?.trim() || null);
    setPaywallOpen(true);
  };

  const openCoachOrPaywall = () => {
    if (!hasCap('parenting_coach') && !hasCap('behavior_coach')) {
      openPaywall(
        subscription?.upgradeHintVi ||
          'Gói hiện tại chưa gồm AI Parenting Coach — nâng Family Peace Plan để Famixa đồng hành.',
      );
      return;
    }
    setCoachOpen(true);
  };

  const goValueAnchor = (anchorId: string) => {
    setValueFocus(anchorId);
    setTab('value');
  };

  const goReportHub = () => {
    setValueFocus(null);
    setTab('value');
  };

  const goRewardsSection = (sectionId: string) => {
    setTab('rewards');
    window.setTimeout(() => {
      document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 80);
  };

  const runBriefPrimary = () => {
    const action = homeBrief.primaryAction;
    if (action.kind === 'evening_checkin') {
      goValueAnchor('fv-3q');
      return;
    }
    if (action.kind === 'attention') {
      const hit = attentionItems.find((x) => x.id === action.attentionId) ?? attentionItems[0];
      if (!hit) {
        scrollToMissions('need_help');
        return;
      }
      if (hit.kind === 'awaiting') {
        void verifyItem(hit.item).catch(() => undefined);
        return;
      }
      scrollToMissions('need_help');
      return;
    }
    openCoachOrPaywall();
  };

  const runBriefReason = () => {
    const action = homeBrief.primaryAction;
    if (action.kind === 'evening_checkin') {
      goValueAnchor('fv-3q');
      return;
    }
    if (action.kind === 'attention') {
      const el = document.getElementById('ph-brief-attn');
      if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        return;
      }
      scrollToMissions('need_help');
      return;
    }
    openCoachOrPaywall();
  };

  const handleFulfillRedemption = async (redemptionId: string) => {
    if (!parentMembershipId || fulfillBusyId) return;
    setFulfillBusyId(redemptionId);
    try {
      const updated = await fulfillRewardRedemption(familyId, redemptionId, parentMembershipId);
      setChildRedemptions((prev) =>
        prev.map((r) => (r.id === updated.id ? updated : r)),
      );
      showTreasureToast('Đã xác nhận con nhận quà!');
    } catch {
      showTreasureToast('Chưa xác nhận được — thử lại nhé.');
    } finally {
      setFulfillBusyId(null);
    }
  };

  return (
    <section className="ph-home ph-v2 ph-v3">
      {actionToast ? (
        <div className="ph-action-toast" role="status">
          {actionToast}
        </div>
      ) : null}

      {tab === 'home' ? (
      <header className="ph-b4-top">
        <div className="ph-b4-identity">
          <div className="ph-b4-avatar" aria-hidden>
            {parentAvatar}
          </div>
          <div>
            <h1 className="ph-b4-hello">
              Xin chào, {parentHelloLabel} <span aria-hidden>👋</span>
            </h1>
            <button
              type="button"
              className="ph-b4-family"
              onClick={() => setModeSheetOpen(true)}
            >
              {familyName || 'Gia đình mình'}
              <span aria-hidden>▾</span>
            </button>
          </div>
        </div>
        <div className="ph-b4-top-right">
          <button
            type="button"
            className="ph-b4-icon-btn"
            aria-label="Việc cần xử lý"
            onClick={() => scrollToMissions('need_help')}
          >
            <span aria-hidden>🔔</span>
            {attentionItems.length > 0 ? (
              <i>{Math.min(attentionItems.length, 9)}</i>
            ) : null}
          </button>
          <button
            type="button"
            className="ph-b4-icon-btn"
            aria-label="Cài đặt"
            onClick={() => setMoreOpen(true)}
          >
            <span aria-hidden>⚙️</span>
          </button>
        </div>
      </header>
      ) : null}

      {tab === 'home' ? (
        <div className="ph-b4-home">
          <article className="ph-b4-brief" aria-label="Morning Brief">
            <div className="ph-b4-brief-main">
              <p className="ph-b4-brief-eyebrow">
                <span className="ph-b4-spark" aria-hidden>
                  ✦
                </span>
                {homeBrief.period === 'evening' ? 'EVENING BRIEF' : 'MORNING BRIEF'}
              </p>
              <h2 className="ph-b4-brief-title">
                {homeBrief.period === 'evening'
                  ? 'Tối nay chỉ có 1 việc bạn nên làm.'
                  : 'Hôm nay chỉ có 1 việc bạn nên làm.'}
              </h2>
              <div className="ph-b4-brief-task">
                <span className="ph-b4-brief-check" aria-hidden>
                  ✓
                </span>
                <strong>{homeBrief.primaryAction.doThisVi}</strong>
              </div>
              <button type="button" className="ph-b4-brief-cta" onClick={runBriefPrimary}>
                <span aria-hidden>⚡</span>
                Thực hiện ngay
              </button>
            </div>
            <div className="ph-b4-brief-art" aria-hidden>
              <img
                src="/home/morning-brief-art.png"
                alt=""
                width={180}
                height={180}
                onError={(e) => {
                  (e.currentTarget as HTMLImageElement).style.display = 'none';
                }}
              />
            </div>
          </article>

          {(homeBrief.eveningCheckinHintVi ||
            homeBrief.period === 'evening' ||
            homeBrief.primaryAction.kind === 'coach') &&
          !eveningCheckin ? (
            <button
              type="button"
              className="ph-b4-brief-ai"
              onClick={() => {
                if (homeBrief.primaryAction.kind === 'coach' && homeBrief.period !== 'evening') {
                  runBriefReason();
                  return;
                }
                goValueAnchor('fv-3q');
              }}
            >
              <span aria-hidden>🎯</span>
              <span>
                {homeBrief.primaryAction.kind === 'coach' &&
                homeBrief.period !== 'evening' &&
                !homeBrief.eveningCheckinHintVi ? (
                  <>
                    {homeBrief.primaryAction.reasonVi.slice(0, 110)}
                    {homeBrief.primaryAction.reasonVi.length > 110 ? '…' : ''}{' '}
                    <em>Xem lý do →</em>
                  </>
                ) : (
                  <>
                    Gợi ý từ AI: 3Q tối giúp duy trì thói quen tốt.{' '}
                    <em>Xem gợi ý phù hợp với gia đình →</em>
                  </>
                )}
              </span>
            </button>
          ) : null}

          <div className="ph-b4-mid">
            <section className="ph-b4-priority" id="ph-brief-attn" aria-label="Ưu tiên hôm nay">
              <header className="ph-b4-col-head">
                <h3>
                  <span aria-hidden>🎯</span> ƯU TIÊN HÔM NAY
                </h3>
              </header>
              {homeAttention.length === 0 ? (
                <p className="ph-b4-empty">Không việc nóng — nhà đang ổn.</p>
              ) : (
                <ul className="ph-b4-priority-list">
                  {homeAttention.map((a) => {
                    if (a.kind === 'consequence') {
                      return (
                        <li key={a.id}>
                          <button
                            type="button"
                            className="ph-b4-priority-item"
                            onClick={() => scrollToMissions('need_help')}
                          >
                            <span className="ph-b4-priority-ico" aria-hidden>
                              ⚠️
                            </span>
                            <span>
                              <strong>{a.event.labelVi}</strong>
                              <em>
                                {(a.event.memberName?.trim() || childShort) +
                                  ' · chờ quyết định'}
                              </em>
                            </span>
                            <i aria-hidden />
                          </button>
                        </li>
                      );
                    }
                    const label =
                      a.kind === 'awaiting'
                        ? 'Cần xác nhận'
                        : a.kind === 'overdue'
                          ? 'Chưa xong / quá giờ'
                          : 'Cần chú ý';
                    return (
                      <li key={a.id}>
                        <button
                          type="button"
                          className="ph-b4-priority-item"
                          onClick={() => {
                            if (a.kind === 'awaiting') {
                              void verifyItem(a.item).catch(() => undefined);
                              return;
                            }
                            scrollToMissions('need_help');
                          }}
                        >
                          <span className="ph-b4-priority-ico" aria-hidden>
                            {a.kind === 'awaiting' ? '💊' : '📖'}
                          </span>
                          <span>
                            <strong>{a.item.title}</strong>
                            <em>
                              {label}
                              {a.item.windowEnd
                                ? ` · trước ${a.item.windowEnd.slice(0, 5)}`
                                : ''}
                            </em>
                          </span>
                          <i aria-hidden />
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
              <button
                type="button"
                className="ph-b4-see-all"
                onClick={() => scrollToMissions('need_help')}
              >
                Xem tất cả ›
              </button>
            </section>

            <DecisionInboxPanel
              variant="homeB4"
              maxItems={2}
              familyId={familyId}
              parentMembershipId={parentMembershipId}
              refreshKey={`${flow.flowDate}-${flow.doneCount}-${inboxTick}`}
              onApproveStars={async (commitmentId) => {
                await approveCommitmentStars(familyId, commitmentId);
                markParentVerified(flow.flowDate, commitmentId);
                setVerifiedTick((t) => t + 1);
              }}
              onConsequence={async (eventId, status) => {
                const guide = await onDecideConsequence(eventId, status);
                if (guide) setSoftGuide(guide);
              }}
              onTeamUnlock={(unlockId, status) => onDecideUnlockById(unlockId, status)}
              onRewardFulfill={(id) => handleFulfillRedemption(id)}
              onChanged={() => {
                setInboxTick((t) => t + 1);
                onRefreshFlow?.();
              }}
              onSeeAll={() => scrollToMissions('need_help')}
            />
          </div>

          <section className="ph-b4-progress" aria-label="Tiến độ cả nhà">
            <header className="ph-b4-col-head">
              <h3>
                <span aria-hidden>👨‍👩‍👧‍👦</span> TIẾN ĐỘ CẢ NHÀ
              </h3>
            </header>
            <p className="ph-b4-progress-copy">
              {scopedDone}/{Math.max(scopedTotal, 0)} việc quan trọng đã hoàn thành hôm nay
            </p>
            <div className="ph-b4-progress-row">
              <div className="ph-b4-segments" aria-hidden>
                {Array.from({ length: progressSegments.segs }, (_, idx) => (
                  <i
                    key={idx}
                    className={idx < progressSegments.filled ? 'is-on' : undefined}
                  />
                ))}
              </div>
              <strong>{percent}%</strong>
            </div>
            <button
              type="button"
              className="ph-b4-see-all"
              onClick={() => scrollToMissions()}
            >
              Xem chi tiết ›
            </button>
          </section>

          {(homeMoment || todayUnlock) ? (
            <div className="ph-b4-bottom-grid">
              {homeMoment ? (
                <section className="ph-b4-moment" aria-label="Khoảnh khắc gia đình">
                  <header className="ph-b4-col-head">
                    <h3>
                      <span aria-hidden>❤️</span> KHOẢNH KHẮC GIA ĐÌNH
                    </h3>
                  </header>
                  <button
                    type="button"
                    className="ph-b4-moment-card"
                    onClick={() => {
                      setTab('diary');
                      openMemoriesSheet();
                    }}
                  >
                    {homeMoment.photoUrl ? (
                      <img src={homeMoment.photoUrl} alt="" />
                    ) : (
                      <span className="ph-b4-moment-ph" aria-hidden>
                        🏆
                      </span>
                    )}
                    <strong>{homeMoment.titleVi}</strong>
                    {homeMoment.detailVi ? <em>{homeMoment.detailVi}</em> : null}
                  </button>
                </section>
              ) : null}

              {todayUnlock ? (
                <section className="ph-b4-challenge" aria-label="Challenge đang diễn ra">
                  <header className="ph-b4-col-head">
                    <h3>CHALLENGE ĐANG DIỄN RA</h3>
                  </header>
                  <button
                    type="button"
                    className="ph-b4-challenge-card"
                    onClick={() => setTab('rewards')}
                  >
                    <span className="ph-b4-challenge-pop" aria-hidden>
                      🍿
                    </span>
                    <strong>{todayUnlock.labelVi || 'Movie Night'}</strong>
                    <p>Cùng xem phim và thưởng thức cuối tuần cùng gia đình!</p>
                    <em>
                      {todayUnlock.teamDone}/{Math.max(todayUnlock.teamTotal, 1)} thành viên
                      {todayUnlock.status === 'confirmed'
                        ? ' · đã mở'
                        : todayUnlock.status === 'pending_confirm'
                          ? ' · chờ duyệt'
                          : ''}
                    </em>
                  </button>
                </section>
              ) : null}
            </div>
          ) : null}

          {subscription ? (
            <section className="ph-b4-plan" aria-label="Gói dịch vụ">
              <span aria-hidden>👑</span>
              <div>
                <strong>
                  {subscription.displayNameVi ||
                    subscription.outcomeNameVi ||
                    'Peace Plan - Family'}
                </strong>
                {peaceDaysLeft != null ? (
                  <em className="ph-b4-plan-pill">Còn {peaceDaysLeft} ngày</em>
                ) : null}
              </div>
              <button type="button" onClick={goPeaceCheckout}>
                Gia hạn ›
              </button>
            </section>
          ) : (
            <BillingBanner familyId={familyId} />
          )}

          <nav className="ph-b4-explore-row" aria-label="Lối tắt tính năng">
            <button type="button" onClick={openCoachOrPaywall}>
              <i className="is-green" aria-hidden>
                🤖
              </i>
              Coach AI
            </button>
            <button type="button" onClick={() => goValueAnchor('fv-3q')}>
              <i className="is-purple" aria-hidden>
                🎯
              </i>
              3Q tối
            </button>
            <button type="button" onClick={() => setTab('tasks')}>
              <i className="is-blue" aria-hidden>
                ✅
              </i>
              Nhiệm vụ
            </button>
            <button type="button" onClick={() => setTab('rewards')}>
              <i className="is-yellow" aria-hidden>
                ⭐
              </i>
              Kho báu
            </button>
            <button type="button" onClick={() => goRewardsSection('ph-treasure-challenge')}>
              <i className="is-pink" aria-hidden>
                🏆
              </i>
              Challenge
            </button>
          </nav>
        </div>
      ) : null}

      {tab === 'tasks' ? (
        <div className="ph-tasks" id="ph-missions">
          <header className="ph-tasks-top">
            <div>
              <h1>Nhiệm vụ</h1>
              <p>
                Đang xem: <strong>{childFocusLabel}</strong>
              </p>
            </div>
            <div className="ph-tasks-actions">
              {renderChildPicker('module')}
              <button
                type="button"
                className="ph-tasks-icon-btn"
                aria-label="Tìm kiếm"
                aria-pressed={taskSearchOpen}
                onClick={() => setTaskSearchOpen((v) => !v)}
              >
                <span aria-hidden>🔍</span>
              </button>
              <button
                type="button"
                className="ph-tasks-filter-btn"
                onClick={() =>
                  setMissionFilter((f) => (f === 'all' ? 'need_help' : 'all'))
                }
              >
                <span aria-hidden>▤</span> Lọc
              </button>
            </div>
          </header>

          {taskSearchOpen ? (
            <label className="ph-tasks-search">
              <span className="sr-only">Tìm nhiệm vụ</span>
              <input
                value={taskQuery}
                onChange={(e) => setTaskQuery(e.target.value)}
                placeholder="Tìm theo tên nhiệm vụ…"
              />
            </label>
          ) : null}

          <div className="ph-tasks-tabs" role="tablist" aria-label="Lọc nhiệm vụ">
            {(
              [
                { key: 'all' as const, label: 'Tất cả', count: scopedTotal, tone: 'purple' },
                {
                  key: 'need_help' as const,
                  label: 'Cần mẹ hỗ trợ',
                  count: needHelpItems.length,
                  tone: 'orange',
                },
                {
                  key: 'waiting_child' as const,
                  label: 'Chờ con hoàn thành',
                  count: waitingChildItems.length,
                  tone: 'blue',
                },
                {
                  key: 'done' as const,
                  label: 'Đã hoàn thành',
                  count: doneTodayItems.length,
                  tone: 'green',
                },
              ] as const
            ).map((t) => (
              <button
                key={t.key}
                type="button"
                role="tab"
                aria-selected={missionFilter === t.key}
                className={`ph-tasks-tab tone-${t.tone}${missionFilter === t.key ? ' is-on' : ''}`}
                onClick={() => setMissionFilter(t.key)}
              >
                {t.label} <b>{t.count}</b>
              </button>
            ))}
          </div>

          <div className="ph-tasks-date">
            <button
              type="button"
              className="ph-tasks-nav"
              aria-label="Xem nhật ký ngày trước"
              title="Mở nhật ký ngày trước"
              onClick={() => goDiaryDay(-1)}
            >
              ‹
            </button>
            <button
              type="button"
              className="ph-tasks-date-pill"
              onClick={() => {
                setTab('diary');
                const todayIdx = diaryDays.findIndex((d) => d.isToday);
                if (todayIdx >= 0) setDiaryDayIdx(todayIdx);
                window.setTimeout(() => {
                  diaryDatesRef.current?.scrollIntoView({
                    behavior: 'smooth',
                    block: 'nearest',
                  });
                }, 50);
              }}
            >
              <span aria-hidden>📅</span>
              Hôm nay, {flow.flowDate.slice(8, 10)}/{flow.flowDate.slice(5, 7)}/
              {flow.flowDate.slice(0, 4)}
              <em aria-hidden>▾</em>
            </button>
            <button
              type="button"
              className="ph-tasks-nav"
              aria-label="Xem nhật ký ngày sau"
              title="Mở nhật ký ngày sau"
              onClick={() => goDiaryDay(1)}
            >
              ›
            </button>
          </div>

          <article className="ph-tasks-banner">
            <div className="ph-tasks-foxy" aria-hidden>
              🦊
            </div>
            <div className="ph-tasks-bubble">
              <p>
                {voicePick(`${flow.flowDate}:tasks-banner:${needHelpItems.length}`, [
                  needHelpItems.length === 0
                    ? `${parentRole === 'bố' ? 'Bố' : parentRole === 'mẹ' ? 'Mẹ' : 'Bố mẹ'} ơi, hôm nay ${childShort} đang giữ nhịp ổn! 💪`
                    : `${parentRole === 'bố' ? 'Bố' : parentRole === 'mẹ' ? 'Mẹ' : 'Bố mẹ'} ơi, hôm nay ${childShort} có ${needHelpItems.length} việc cần hỗ trợ nhẹ nhé! 💪`,
                  needHelpItems.length === 0
                    ? `Không việc nóng — ${childShort} đang tự chủ tốt hôm nay.`
                    : `Chỉ ${needHelpItems.length} việc cần ${parentRole} — khoảng 15 giây mỗi việc.`,
                  needHelpItems.length === 0
                    ? `Tiến độ ${percent}% — nhà mình đang đi đúng hướng.`
                    : `Ưu tiên ${needHelpItems.length} việc nóng của ${childShort}, rồi dừng.`,
                ])}
              </p>
            </div>
            <div className="ph-tasks-progress">
              <span>Tiến độ nhiệm vụ hôm nay</span>
              <strong>
                {scopedDone} / {Math.max(scopedTotal, 1)}
              </strong>
              <div className="ph-tasks-bar" aria-hidden>
                <b style={{ width: `${percent}%` }} />
              </div>
              <em>
                <span aria-hidden>🏆</span> {percent}%
              </em>
            </div>
          </article>

          {(missionFilter === 'all' || missionFilter === 'need_help') && (
            <section className="ph-tasks-sec">
              <header className="ph-tasks-sec-head">
                <h2>
                  <span aria-hidden>🚨</span> Cần mẹ hỗ trợ
                  <b className="is-red">{needHelpItems.length}</b>
                </h2>
                <button
                  type="button"
                  className="ph-text-link"
                  onClick={() => setMissionFilter('need_help')}
                >
                  Xem tất cả →
                </button>
              </header>
              <ul className="ph-tasks-cards">
                {(missionFilter === 'need_help' ? filteredMissions : needHelpItems)
                  .filter((c) => !taskQuery || c.title.toLowerCase().includes(taskQuery.toLowerCase()))
                  .slice(0, missionFilter === 'all' ? 3 : 20)
                  .map((item) => {
                    const state = missionUxState(item, flow.flowDate, verifiedTick);
                    const kind = state === 'awaiting_check' ? 'awaiting' : 'overdue';
                    const deadline = item.windowEnd
                      ? `Trước ${item.windowEnd.slice(0, 5)}`
                      : lateLabel(item, flow.localTime);
                    return (
                      <li key={item.id} className="ph-task-card is-help">
                        <span className="ph-task-card-ico" aria-hidden>
                          {taskIcon(item.title)}
                        </span>
                        <div className="ph-task-card-body">
                          <strong>{item.title}</strong>
                          <p>
                            {warmTaskSupportNote({
                              title: item.title,
                              childShort,
                              parentRole,
                              kind,
                              flowDate: flow.flowDate,
                              itemId: item.id,
                            })}
                          </p>
                          <em>
                            <span aria-hidden>🕒</span> {deadline}
                          </em>
                        </div>
                        <div className="ph-task-card-side">
                          {kind === 'awaiting' && item.evidenceUrl ? (
                            <a
                              className="ph-task-evidence"
                              href={withEvidenceAuth(item.evidenceUrl)}
                              target="_blank"
                              rel="noreferrer"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <img
                                src={withEvidenceAuth(item.evidenceUrl)}
                                alt={`Ảnh ${childShort} gửi — ${item.title}`}
                                className="evidence-thumb is-board"
                              />
                            </a>
                          ) : null}
                          <span className="ph-task-who" aria-hidden>
                            {parentAvatar}
                          </span>
                          {kind === 'awaiting' ? (
                            <button
                              type="button"
                              className="ph-task-cta is-check"
                              disabled={busyId === item.id || verifyingId === item.id}
                              onClick={() => void verifyItem(item)}
                            >
                              {busyId === item.id || verifyingId === item.id
                                ? 'Đang…'
                                : taskCtaLabel(item.title, kind, flow.flowDate)}
                            </button>
                          ) : (
                            <QuickNudgeButton
                              items={item}
                              familyId={familyId}
                              flowDate={flow.flowDate}
                              label={taskCtaLabel(item.title, kind, flow.flowDate)}
                              className="ph-task-cta is-nudge"
                              onNudged={(count) => {
                                onParentNudged(count);
                                showActionToast(
                                  'Đã chuẩn bị tin nhắc — gửi Zalo/Messenger cho con',
                                );
                              }}
                            />
                          )}
                        </div>
                      </li>
                    );
                  })}
                {needHelpItems.length === 0 ? (
                  <li className="ph-empty-soft">Không có việc cần mẹ hỗ trợ ngay.</li>
                ) : null}
              </ul>
            </section>
          )}

          {(missionFilter === 'all' || missionFilter === 'waiting_child') && (
            <section className="ph-tasks-sec">
              <header className="ph-tasks-sec-head">
                <h2>
                  <span aria-hidden>⏱️</span> Chờ con hoàn thành
                  <b className="is-blue">{waitingChildItems.length}</b>
                </h2>
                <button
                  type="button"
                  className="ph-text-link"
                  onClick={() => setMissionFilter('waiting_child')}
                >
                  Xem tất cả →
                </button>
              </header>
              {waitingOpen ? (
                <ul className="ph-tasks-cards">
                  {(missionFilter === 'waiting_child' ? filteredMissions : waitingChildItems)
                    .filter(
                      (c) => !taskQuery || c.title.toLowerCase().includes(taskQuery.toLowerCase()),
                    )
                    .slice(0, missionFilter === 'all' ? 3 : 20)
                    .map((item) => (
                      <li key={item.id} className="ph-task-card is-wait">
                        <span className="ph-task-card-ico" aria-hidden>
                          {taskIcon(item.title)}
                        </span>
                        <div className="ph-task-card-body">
                          <strong>{item.title}</strong>
                          <p>
                            {warmTaskTip({
                              title: item.title,
                              childShort,
                              parentRole,
                              flowDate: flow.flowDate,
                              itemId: item.id,
                            })}
                          </p>
                          {item.windowStart || item.windowEnd ? (
                            <em>
                              <span aria-hidden>🕒</span>{' '}
                              {formatWindow(item.windowStart, item.windowEnd)}
                            </em>
                          ) : null}
                        </div>
                        <div className="ph-task-card-side">
                          <span className="ph-task-who" aria-hidden>
                            {childAvatar}
                          </span>
                          <QuickNudgeButton
                            items={item}
                            familyId={familyId}
                            flowDate={flow.flowDate}
                            label="Nhắc con"
                            className="ph-task-cta is-nudge"
                            onNudged={(count) => {
                              onParentNudged(count);
                              showActionToast(
                                'Đã copy tin nhắc — dán Zalo/Messenger gửi cho con',
                              );
                            }}
                          />
                        </div>
                      </li>
                    ))}
                  {waitingChildItems.length === 0 ? (
                    <li className="ph-empty-soft">Không còn việc chờ con làm.</li>
                  ) : null}
                </ul>
              ) : null}
              {waitingChildItems.length > 3 &&
              (missionFilter === 'all' || missionFilter === 'waiting_child') ? (
                <button
                  type="button"
                  className="ph-tasks-expand"
                  onClick={() => setWaitingOpen((v) => !v)}
                  aria-label={waitingOpen ? 'Thu gọn' : 'Mở rộng'}
                >
                  {waitingOpen ? '▴' : '▾'}
                </button>
              ) : null}
            </section>
          )}

          {(missionFilter === 'all' || missionFilter === 'done') && (
            <section className="ph-tasks-sec">
              <header className="ph-tasks-sec-head">
                <h2>
                  <span aria-hidden>✅</span> Đã hoàn thành hôm nay
                  <b className="is-green">{doneTodayItems.length}</b>
                </h2>
                <button
                  type="button"
                  className="ph-text-link"
                  onClick={() => setMissionFilter('done')}
                >
                  Xem tất cả →
                </button>
              </header>
              <ul className="ph-tasks-cards">
                {(missionFilter === 'done' ? filteredMissions : doneTodayItems)
                  .filter((c) => !taskQuery || c.title.toLowerCase().includes(taskQuery.toLowerCase()))
                  .slice(0, missionFilter === 'all' ? 4 : 30)
                  .map((item) => {
                    const clock = formatClock(item.completedAt);
                    return (
                      <li key={item.id} className="ph-task-card is-done">
                        <span className="ph-task-card-ico" aria-hidden>
                          {taskIcon(item.title)}
                        </span>
                        <div className="ph-task-card-body">
                          <strong>{item.title}</strong>
                          <p>
                            {warmTaskTip({
                              title: item.title,
                              childShort,
                              parentRole,
                              flowDate: flow.flowDate,
                              itemId: item.id,
                            })}
                          </p>
                        </div>
                        <div className="ph-task-card-side">
                          <span className="ph-task-who" aria-hidden>
                            {childAvatar}
                          </span>
                          <span className="ph-task-status is-ok">
                            Hoàn thành{clock ? ` lúc ${clock}` : ''}
                            <i aria-hidden>✓</i>
                          </span>
                        </div>
                      </li>
                    );
                  })}
                {doneTodayItems.length === 0 ? (
                  <li className="ph-empty-soft">Hôm nay chưa có việc hoàn thành.</li>
                ) : null}
              </ul>
            </section>
          )}
        </div>
      ) : null}

      {tab === 'value' ? (
        <div className="ph-report">
          <header className="ph-report-top">
            <button
              type="button"
              className="ph-report-back"
              aria-label="Về trang chủ"
              onClick={() => setTab('home')}
            >
              ‹
            </button>
            <div className="ph-report-titles">
              <h1>
                {valueFocus === 'fv-3q'
                  ? '3Q tối'
                  : valueFocus === 'fv-rop'
                    ? 'ROP · Family Report'
                    : 'Báo cáo gia đình'}
              </h1>
              <p>
                {valueFocus === 'fv-3q'
                  ? 'Ba câu hỏi nhanh — nhịp nhà hôm nay'
                  : valueFocus === 'fv-rop'
                    ? 'Tăng trưởng bố mẹ theo hành vi'
                    : 'ROP · 3Q · ghi nhận · replay'}
              </p>
            </div>
            <div className="ph-report-tools">{renderChildPicker('module')}</div>
          </header>
          {!valueFocus ? (
            <div className="ph-report-jumps" aria-label="Mục trong báo cáo">
              <button type="button" onClick={() => goValueAnchor('fv-rop')}>
                📘 ROP
              </button>
              <button type="button" onClick={() => goValueAnchor('fv-3q')}>
                🎯 3Q tối
              </button>
              <button type="button" onClick={() => setTab('diary')}>
                📖 Nhật ký →
              </button>
            </div>
          ) : null}
          <div className="ph-report-body">
            <FamilyValuePanel
              familyId={familyId}
              familyName={familyName}
              flow={flow}
              glance={glance}
              nudgeToday={nudgeToday}
              momentCount={savedMemories.length + childGratitudes.length}
              onOpenPaywall={(reason) => openPaywall(reason)}
              parentMembershipId={parentMembershipId}
              eveningCheckin={eveningCheckin}
              onEveningCheckinChange={setEveningCheckin}
              focusAnchorId={valueFocus}
            />
          </div>
          <p className="ph-report-diary-hint">
            Muốn xem timeline việc & kỷ niệm?{' '}
            <button type="button" className="ph-text-link" onClick={() => setTab('diary')}>
              Mở Nhật ký →
            </button>
          </p>
        </div>
      ) : null}

      {tab === 'diary' ? (
        <div className="ph-diary">
          {diaryToast ? (
            <div className="ph-diary-toast" role="status">
              {diaryToast}
            </div>
          ) : null}

          <header className="ph-diary-top">
            <button
              type="button"
              className="ph-diary-back"
              aria-label="Về trang chủ"
              onClick={() => setTab('home')}
            >
              ‹
            </button>
            <div className="ph-diary-titles">
              <h1>
                Nhật ký của {childShort} <span aria-hidden>💜</span>
              </h1>
              <p>Đang xem: {childFocusLabel}</p>
            </div>
            <div className="ph-diary-tools">
              {renderChildPicker('module')}
              <button
                type="button"
                className={`ph-diary-tool${diarySearchOpen ? ' is-on' : ''}`}
                aria-label="Tìm kiếm"
                onClick={() => {
                  setDiarySearchOpen((v) => {
                    if (v) setDiaryQuery('');
                    return !v;
                  });
                }}
              >
                🔍
              </button>
              <button
                type="button"
                className="ph-diary-tool"
                aria-label="Lịch"
                onClick={() => {
                  const todayIdx = diaryDays.findIndex((d) => d.isToday);
                  if (todayIdx >= 0) {
                    setDiaryDayIdx(todayIdx);
                    setDiaryExpanded(false);
                  }
                  diaryDatesRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }}
              >
                📅
              </button>
            </div>
          </header>

          {diarySearchOpen ? (
            <label className="ph-diary-search">
              <span className="sr-only">Tìm trong nhật ký</span>
              <input
                type="search"
                value={diaryQuery}
                placeholder={`Tìm việc của ${childShort}…`}
                onChange={(e) => {
                  setDiaryQuery(e.target.value);
                  setDiaryExpanded(true);
                }}
                autoFocus
              />
            </label>
          ) : null}

          <div
            ref={diaryDatesRef}
            className="ph-diary-dates"
            role="tablist"
            aria-label="Chọn ngày"
          >
            {diaryDays.map((d, i) => (
              <button
                key={d.key}
                type="button"
                role="tab"
                aria-selected={i === diaryDayIdx}
                aria-disabled={!d.isToday}
                title={d.isToday ? d.fullLabel : 'Nhật ký các ngày khác sẽ mở khi có lịch sử lưu'}
                className={`ph-diary-date${i === diaryDayIdx ? ' is-on' : ''}${
                  d.isToday ? '' : ' is-muted'
                }`}
                disabled={!d.isToday}
                onClick={() => {
                  if (!d.isToday) return;
                  setDiaryDayIdx(i);
                  setDiaryExpanded(false);
                }}
              >
                {d.shortLabel}
              </button>
            ))}
          </div>

          <div className="ph-diary-filters">
            {(
              [
                { id: 'all', icon: '▦', label: 'Tất cả' },
                { id: 'tasks', icon: '📋', label: 'Nhiệm vụ' },
                { id: 'moments', icon: '❤️', label: 'Khoảnh khắc' },
                { id: 'health', icon: '😊', label: 'Sức khỏe' },
                { id: 'study', icon: '📚', label: 'Học tập' },
              ] as const
            ).map((f) => (
              <button
                key={f.id}
                type="button"
                className={`ph-diary-chip${diaryFilter === f.id ? ' is-on' : ''}`}
                onClick={() => {
                  setDiaryFilter(f.id);
                  setDiaryExpanded(false);
                }}
              >
                <span aria-hidden>{f.icon}</span> {f.label}
              </button>
            ))}
            <button
              type="button"
              className={`ph-diary-chip is-filter${diaryFavoritesOnly ? ' is-on' : ''}`}
              aria-pressed={diaryFavoritesOnly}
              title={
                diaryFavoritesOnly
                  ? 'Đang chỉ hiện kỷ niệm đã gắn tim — bấm để xem tất cả'
                  : 'Chỉ hiện kỷ niệm đã gắn tim'
              }
              onClick={() => {
                setDiaryFavoritesOnly((v) => !v);
                setDiaryFilter('moments');
                setDiaryExpanded(false);
              }}
            >
              <span aria-hidden>{diaryFavoritesOnly ? '❤️' : '▾'}</span>{' '}
              {diaryFavoritesOnly ? 'Đã tim' : 'Lọc'}
            </button>
          </div>

          <div className="ph-diary-layout">
            <aside className="ph-diary-side">
              <article className="ph-diary-summary">
                <h3>Tổng kết ngày</h3>
                <div className="ph-diary-summary-body">
                  <span className="ph-diary-trophy" aria-hidden>
                    🏆
                  </span>
                  <div>
                    <p>
                      Tuyệt vời! {childShort} đã hoàn thành{' '}
                      <strong>
                        {scopedDone}/{Math.max(scopedTotal, 1)}
                      </strong>{' '}
                      việc
                    </p>
                    <em>
                      +{diaryStarsEarned} ⭐
                      {diaryStarsPending > 0 ? ` (+${diaryStarsPending} chờ duyệt)` : ''}
                    </em>
                  </div>
                </div>
              </article>
            </aside>

            <div className="ph-diary-main">
              <h2 className="ph-diary-day-title">
                {selectedDiaryDay?.fullLabel ?? formatFlowDay(flow.flowDate)}
              </h2>

              {selectedDiaryDay && !selectedDiaryDay.isToday ? (
                <p className="ph-diary-empty">
                  Đang xem hôm nay — lịch sử nhật ký các ngày khác sẽ mở sau khi hệ thống lưu đủ dữ liệu.
                </p>
              ) : diaryFilteredEntries.length === 0 ? (
                <p className="ph-diary-empty">
                  {diaryQuery.trim()
                    ? `Không thấy kết quả cho «${diaryQuery.trim()}».`
                    : `Hôm nay chưa có trang nhật ký — khi ${childShort} làm việc, nhật ký sẽ hiện ở đây.`}
                </p>
              ) : (
                <ol className="ph-diary-timeline">
                  {diaryVisible.map((entry, idx) => {
                    const prevPart = idx > 0 ? diaryVisible[idx - 1].part : null;
                    const showPart = entry.part !== prevPart;
                    const partIcon =
                      entry.part === 'morning' ? '☀️' : entry.part === 'evening' ? '🌙' : '🌤️';
                    const partText =
                      entry.part === 'morning'
                        ? 'Sáng'
                        : entry.part === 'evening'
                          ? 'Tối'
                          : 'Chiều';
                    return (
                      <li
                        key={entry.item.id}
                        className={`ph-diary-node${entry.done ? ' is-done' : ''}${
                          entry.wait ? ' is-wait' : ''
                        }`}
                      >
                        <div className="ph-diary-rail" aria-hidden>
                          {showPart ? (
                            <span className={`ph-diary-part is-${entry.part}`}>
                              {partIcon} {partText}
                            </span>
                          ) : (
                            <span className="ph-diary-dot" />
                          )}
                          <em>{entry.time}</em>
                        </div>
                        <article className="ph-diary-card">
                          <span className={`ph-diary-ico tone-${entry.tone}`} aria-hidden>
                            {taskIcon(entry.item.title)}
                          </span>
                          <div className="ph-diary-card-body">
                            <div className="ph-diary-card-head">
                              <strong>{entry.item.title}</strong>
                              {entry.done ? (
                                <span className="ph-diary-status is-ok">Hoàn thành</span>
                              ) : entry.wait ? (
                                <span className="ph-diary-status is-wait">Chờ kiểm tra</span>
                              ) : entry.skipped ? (
                                <span className="ph-diary-status is-skip">Bỏ qua</span>
                              ) : (
                                <span className="ph-diary-status is-pending">Chưa xong</span>
                              )}
                            </div>
                            <p>{entry.note}</p>
                            <span className={`ph-diary-tag tone-${entry.tag.tone}`}>
                              {entry.tag.label}
                            </span>
                          </div>
                          <div className="ph-diary-card-side">
                            {entry.item.evidenceUrl ? (
                              <a
                                className="ph-diary-photo"
                                href={withEvidenceAuth(entry.item.evidenceUrl)}
                                target="_blank"
                                rel="noreferrer"
                              >
                                <img
                                  src={withEvidenceAuth(entry.item.evidenceUrl)}
                                  alt={`Ảnh ${entry.item.title}`}
                                />
                              </a>
                            ) : (
                              <div className="ph-diary-photo is-placeholder" aria-hidden>
                                {taskIcon(entry.item.title)}
                              </div>
                            )}
                            {entry.done ? (
                              <span className="ph-diary-stars">
                                {entry.starLabel}
                                {entry.lateCaption ? (
                                  <small className="muted ph-diary-late">{entry.lateCaption}</small>
                                ) : null}
                              </span>
                            ) : entry.wait ? (
                              <button
                                type="button"
                                className="ph-diary-mini-cta"
                                disabled={
                                  busyId === entry.item.id || verifyingId === entry.item.id
                                }
                                onClick={() => void verifyItem(entry.item)}
                              >
                                {busyId === entry.item.id || verifyingId === entry.item.id
                                  ? 'Đang…'
                                  : 'Kiểm tra'}
                              </button>
                            ) : null}
                          </div>
                        </article>
                      </li>
                    );
                  })}
                </ol>
              )}

              {selectedDiaryDay?.isToday && diaryFilteredEntries.length > 5 ? (
                <button
                  type="button"
                  className="ph-diary-more"
                  onClick={() => setDiaryExpanded((v) => !v)}
                >
                  {diaryExpanded ? 'Thu gọn' : 'Xem thêm'} <span aria-hidden>▾</span>
                </button>
              ) : null}
            </div>

            <aside className="ph-diary-widgets">
              <article className="ph-diary-mood">
                <header className="ph-diary-side-head">
                  <h3>Tâm trạng của {childShort}</h3>
                  {focusedChildMood && focusedMoodDisplay ? (
                    <span className="ph-diary-mood-current">
                      {focusedMoodDisplay.emoji} {focusedMoodDisplay.label}
                    </span>
                  ) : null}
                </header>
                {focusedChildMood && focusedMoodDisplay ? (
                  <>
                    <div className="ph-diary-mood-row" aria-label="Tâm trạng con">
                      {DIARY_MOODS.map((m) => (
                        <span
                          key={m.code}
                          className={m.code === focusedChildMood.moodCode ? 'is-on' : undefined}
                          aria-hidden
                        >
                          {m.emoji}
                        </span>
                      ))}
                    </div>
                    {focusedChildMood.note?.trim() ? (
                      <p className="ph-diary-mood-bubble">{focusedChildMood.note.trim()}</p>
                    ) : (
                      <p className="ph-diary-mood-bubble">
                        {focusedChildMood.moodCode === 'love' || focusedChildMood.moodCode === 'happy'
                          ? `${childShort} rất vui khi được ở bên gia đình hôm nay! 💜`
                          : focusedChildMood.moodCode === 'ok'
                            ? `${childShort} hơi bình thường — mẹ dành thêm thời gian nhé.`
                            : `${childShort} cần mẹ động viên thêm hôm nay.`}
                      </p>
                    )}
                  </>
                ) : (
                  <p className="ph-diary-mood-bubble">Con chưa ghi tâm trạng hôm nay</p>
                )}
              </article>

              <article className="ph-diary-moment">
                <header className="ph-diary-side-head">
                  <h3>Khoảnh khắc đáng nhớ</h3>
                  <button
                    type="button"
                    className="ph-text-link"
                    onClick={openMemoriesSheet}
                    disabled={diaryPrettyMemories.length === 0}
                  >
                    Xem tất cả →
                  </button>
                </header>
                {diaryFeatureMoments.length === 0 ? (
                  <p className="ph-empty-soft">{FAMILY_MEMORY_EMPTY}</p>
                ) : (
                  <>
                <div className="ph-diary-moment-card">
                  <button
                    type="button"
                    className={
                      diaryFeatureMoments[diaryMomentIdx]?.memory.entry?.isFavorite
                        ? 'ph-diary-moment-heart is-on'
                        : 'ph-diary-moment-heart'
                    }
                    aria-label={
                      diaryFeatureMoments[diaryMomentIdx]?.memory.entry?.isFavorite
                        ? 'Bỏ thích'
                        : 'Lưu / thích kỷ niệm'
                    }
                    disabled={
                      memoryHeartBusy === diaryFeatureMoments[diaryMomentIdx]?.memory.id
                    }
                    onClick={() => {
                      const mem = diaryFeatureMoments[diaryMomentIdx]?.memory;
                      if (mem) void heartMemory(mem);
                    }}
                  >
                    {diaryFeatureMoments[diaryMomentIdx]?.memory.entry?.isFavorite
                      ? '❤️'
                      : '🤍'}
                  </button>
                  <div className="ph-diary-moment-art" aria-hidden>
                    {diaryFeatureMoments[diaryMomentIdx]?.memory.photoUrl ? (
                      <img
                        src={withEvidenceAuth(
                          diaryFeatureMoments[diaryMomentIdx]!.memory.photoUrl!,
                        )}
                        alt=""
                        className="ph-memory-photo"
                      />
                    ) : (
                      diaryFeatureMoments[diaryMomentIdx]?.icon
                    )}
                  </div>
                  <strong>{diaryFeatureMoments[diaryMomentIdx]?.title}</strong>
                  <em>{diaryFeatureMoments[diaryMomentIdx]?.date}</em>
                  <p>{diaryFeatureMoments[diaryMomentIdx]?.caption}</p>
                </div>
                <div className="ph-diary-dots" role="tablist" aria-label="Chuyển khoảnh khắc">
                  {diaryFeatureMoments.map((m, i) => (
                    <button
                      key={m.id}
                      type="button"
                      role="tab"
                      aria-selected={i === diaryMomentIdx}
                      className={i === diaryMomentIdx ? 'is-on' : undefined}
                      onClick={() => setDiaryMomentIdx(i)}
                    />
                  ))}
                </div>
                  </>
                )}
              </article>
            </aside>
          </div>

          <section className="ph-diary-pretty">
            <header className="ph-diary-sec-head">
              <h2>Kỷ niệm đẹp</h2>
              {diaryPrettyMemories.length > FAMILY_MEMORY_VISIBLE ? (
                <button
                  type="button"
                  className="ph-text-link"
                  onClick={() => setDiaryMemoriesOpen(true)}
                >
                  Xem tất cả →
                </button>
              ) : null}
            </header>
            <div className="ph-diary-pretty-row">
              {diaryMemoriesVisible.length === 0 ? (
                <p className="ph-empty-soft">{FAMILY_MEMORY_EMPTY}</p>
              ) : (
                diaryMemoriesVisible.map((m) => (
                  <article
                    key={m.id}
                    className={`ph-diary-pretty-card${m.locked ? ' is-locked' : ''}`}
                  >
                    {m.isNew ? <span className="ph-diary-mem-new">Mới</span> : null}
                    <div className="ph-diary-pretty-art">
                      {m.photoUrl ? (
                        <img
                          src={withEvidenceAuth(m.photoUrl)}
                          alt=""
                          className="ph-memory-photo"
                        />
                      ) : (
                        <span aria-hidden>{m.icon}</span>
                      )}
                      <button
                        type="button"
                        className={
                          m.entry?.isFavorite
                            ? 'ph-diary-pretty-heart is-on'
                            : 'ph-diary-pretty-heart'
                        }
                        aria-label={
                          m.entry?.isFavorite ? 'Bỏ thích' : 'Lưu / thích kỷ niệm'
                        }
                        disabled={memoryHeartBusy === m.id}
                        onClick={() => void heartMemory(m)}
                      >
                        {m.entry?.isFavorite ? '❤️' : '🤍'}
                      </button>
                    </div>
                    <strong>{m.title}</strong>
                    <em>{m.date}</em>
                  </article>
                ))
              )}
            </div>
          </section>
        </div>
      ) : null}

      {tab === 'rewards' ? (
        <div className="ph-treasure">
          {treasureToast || unlockMsg ? (
            <div className="ph-treasure-toast" role="status">
              {treasureToast || unlockMsg}
            </div>
          ) : null}

          <header className="ph-treasure-top">
            <button
              type="button"
              className="ph-treasure-back"
              aria-label="Về trang chủ"
              onClick={() => setTab('home')}
            >
              ‹
            </button>
            <div className="ph-treasure-titles">
              <h1>
                Kho báu · điểm &amp; thưởng <span aria-hidden>✨</span>
              </h1>
              <p>
                {childShort} · đang xem {childFocusLabel}
              </p>
            </div>
            <div className="ph-treasure-top-actions">
              {renderChildPicker('module')}
              <button
                type="button"
                className="ph-treasure-history"
                onClick={() => setTreasureHistoryOpen(true)}
                disabled={childRedemptions.length === 0}
              >
                <span aria-hidden>🕐</span> Lịch sử đổi quà
              </button>
            </div>
          </header>

          <article className="ph-treasure-hero">
            <div className="ph-treasure-chest" aria-hidden>
              🧰
            </div>
            <div className="ph-treasure-hero-copy">
              <div className="ph-treasure-stars">
                <span aria-hidden>⭐</span>
                <strong>{rewardPoints.toLocaleString('vi-VN')}</strong>
                <em>Sao của {childShort}</em>
              </div>
              <p className="ph-treasure-level">
                <span aria-hidden>👑</span> Level {treasureLevel} · Explorer
              </p>
              <div className="ph-treasure-xp">
                <i aria-hidden>
                  <b style={{ width: `${treasureXpPct}%` }} />
                </i>
              </div>
              <span className="ph-treasure-xp-label">
                {treasureXpHave.toLocaleString('vi-VN')} / 3.000 sao để lên level tiếp
              </span>
            </div>
            <div className="ph-treasure-badge" aria-hidden>
              <span>👑</span>
              <strong>{treasureLevel}</strong>
            </div>
          </article>

          <section className="ph-treasure-sec">
            <header className="ph-treasure-sec-head">
              <h2>
                <span aria-hidden>👨‍👩‍👧</span> PHẦN THƯỞNG CẢ GIA ĐÌNH
              </h2>
              <button
                type="button"
                className="ph-text-link"
                onClick={() =>
                  document
                    .getElementById('ph-treasure-family-goals')
                    ?.scrollIntoView({ behavior: 'smooth', block: 'start' })
                }
              >
                Xem mục tiêu →
              </button>
            </header>

            <article className={`ph-treasure-family${todayUnlock?.status === 'confirmed' ? ' is-ready' : ''}`}>
              <div className="ph-treasure-family-art" aria-hidden>
                🍿
              </div>
              <div className="ph-treasure-family-copy">
                <strong>{todayUnlock?.labelVi ?? 'Movie Night'}</strong>
                <p>Cả nhà cùng xem phim yêu thích</p>
                <div className="ph-treasure-family-bar">
                  <i aria-hidden>
                    <b style={{ width: `${percent}%` }} />
                  </i>
                  <em>{percent}%</em>
                </div>
                <span>
                  {todayUnlock?.status === 'pending_confirm'
                    ? 'Sẵn sàng mở — mẹ xác nhận nhé!'
                    : todayUnlock?.status === 'confirmed'
                      ? 'Đã mở thưởng — cả nhà tận hưởng!'
                      : unlockGap > 0
                        ? `Chỉ còn ${unlockGap} nhiệm vụ nữa!`
                        : 'Đang tiến gần phần thưởng chung'}
                </span>
                {todayUnlock?.status === 'pending_confirm' ? (
                  <div className="ph-treasure-unlock-row">
                    <button
                      type="button"
                      className="ph-treasure-cta"
                      disabled={unlockBusy || !parentMembershipId}
                      onClick={() => void onDecideUnlock('confirmed')}
                    >
                      Mở thưởng nhà
                    </button>
                    <button
                      type="button"
                      className="ph-treasure-cta is-soft"
                      disabled={unlockBusy || !parentMembershipId}
                      onClick={() => void onDecideUnlock('deferred')}
                    >
                      Để sau
                    </button>
                  </div>
                ) : (
                  <p className="muted ph-treasure-detail-note">
                    Tiến độ Movie Night: {percent}%
                    {todayUnlock?.status === 'confirmed' ? ' — đã mở thưởng!' : ''}
                  </p>
                )}
              </div>
            </article>

            <div className="ph-treasure-goals" id="ph-treasure-family-goals">
              {treasureFamilyGoals.length === 0 ? (
                <p className="ph-empty-soft">Chưa có phần thưởng nhóm — hoàn thành nhiệm vụ cả nhà nhé!</p>
              ) : (
                treasureFamilyGoals.map((g) => (
                  <article key={g.id} className="ph-treasure-goal">
                    <span aria-hidden>{g.icon}</span>
                    <strong>{g.title}</strong>
                    <i aria-hidden>
                      <b style={{ width: `${g.pct}%` }} />
                    </i>
                    <em>{g.pct}%</em>
                  </article>
                ))
              )}
            </div>
          </section>

          <section
            className="ph-treasure-sec"
            id="ph-treasure-challenge"
            aria-label="Challenge và mục tiêu bố mẹ"
          >
            {parentMembershipId ? (
              <>
                <FamilyChallengeCard
                  familyId={familyId}
                  memberId={parentMembershipId}
                  isParent
                />
                <ParentGoalsPanel
                  familyId={familyId}
                  memberId={parentMembershipId}
                  viewerName={viewerName}
                />
              </>
            ) : null}
          </section>

          {pendingRedemptions.length > 0 ? (
            <section className="ph-treasure-sec">
              <header className="ph-treasure-sec-head">
                <h2>
                  <span aria-hidden>⏳</span> CHỜ XÁC NHẬN
                </h2>
              </header>
              <div className="ph-treasure-redeem">
                {pendingRedemptions.map((r) => (
                  <article key={r.id} className="ph-treasure-gift">
                    <span aria-hidden>{r.icon}</span>
                    <strong>{r.title}</strong>
                    <em>
                      <span aria-hidden>⭐</span> {r.starCost}
                    </em>
                    <button
                      type="button"
                      className="ph-treasure-gift-cta"
                      disabled={fulfillBusyId === r.id || !parentMembershipId}
                      onClick={() => void handleFulfillRedemption(r.id)}
                    >
                      {fulfillBusyId === r.id ? 'Đang xác nhận…' : 'Xác nhận đã trao quà'}
                    </button>
                  </article>
                ))}
              </div>
            </section>
          ) : null}

          <section className="ph-treasure-sec">
            <header className="ph-treasure-sec-head">
              <h2>
                <span aria-hidden>🎁</span> QUÀ CÓ THỂ ĐỔI
              </h2>
            </header>
            <div className="ph-treasure-redeem">
              {rewardCatalog
                .filter((item) => item.cost != null)
                .map((item) => (
                  <article key={item.id} className="ph-treasure-gift">
                    <span aria-hidden>{item.icon}</span>
                    <strong>{item.title}</strong>
                    <em>
                      <span aria-hidden>⭐</span> {item.cost}
                    </em>
                    <button
                      type="button"
                      className="ph-treasure-gift-cta"
                      disabled
                      aria-label={
                        rewardPoints >= (item.cost ?? 0)
                          ? `${childShort} đủ sao để đổi trên màn hình con`
                          : `${childShort} chưa đủ sao`
                      }
                    >
                      {rewardPoints >= (item.cost ?? 0) ? 'Con có thể đổi' : 'Chưa đủ sao'}
                    </button>
                  </article>
                ))}
            </div>
          </section>

          <div className="ph-treasure-mid">
            <article className="ph-treasure-mystery">
              <h3>KHO BÁU BÍ MẬT</h3>
              <div className="ph-treasure-mystery-body">
                <span aria-hidden>📦</span>
                <div>
                  <strong>Mystery Box</strong>
                  <p>Phần thưởng bí mật đang chờ!</p>
                  <em>Mở khi đạt 2.000 ⭐</em>
                  <div className="ph-treasure-mini-bar" aria-hidden>
                    <b
                      style={{
                        width: `${Math.min(100, Math.round((rewardPoints / 2000) * 100))}%`,
                      }}
                    />
                  </div>
                </div>
              </div>
            </article>

            <article className="ph-treasure-badges" id="ph-treasure-badges">
              <header className="ph-treasure-sec-head is-compact">
                <h3>HUY HIỆU CỦA {childShort.toUpperCase()}</h3>
                <button
                  type="button"
                  className="ph-text-link"
                  onClick={() => {
                    const unlocked = [
                      childRedemptions.length > 0,
                      rewardPoints >= 100,
                      rewardPoints >= 500,
                      (glance?.currentStreak ?? 0) >= 3,
                      percent >= 100,
                    ].filter(Boolean).length;
                    showTreasureToast(
                      unlocked === 0
                        ? 'Chưa mở huy hiệu nào — làm việc và đổi quà để mở nhé'
                        : `Đã mở ${unlocked}/5 huy hiệu`,
                    );
                  }}
                >
                  Tóm tắt →
                </button>
              </header>
              <ul>
                {(
                  [
                    { id: 'b1', icon: '🎁', label: 'Đổi quà đầu tiên', unlocked: childRedemptions.length > 0 },
                    { id: 'b2', icon: '⭐', label: '100 sao', unlocked: rewardPoints >= 100 },
                    { id: 'b3', icon: '💎', label: '500 sao', unlocked: rewardPoints >= 500 },
                    { id: 'b4', icon: '🔥', label: 'Chuỗi ngày tốt', unlocked: (glance?.currentStreak ?? 0) >= 3 },
                    { id: 'b5', icon: '🏆', label: 'Team Champion', unlocked: percent >= 100 },
                  ] as const
                ).map((b) => (
                  <li key={b.id} className={b.unlocked ? 'is-on' : 'is-off'}>
                    <span aria-hidden>{b.icon}</span>
                    <em>{b.label}</em>
                  </li>
                ))}
              </ul>
            </article>
          </div>

          <section className="ph-treasure-sec" ref={achievementsRef}>
            <header className="ph-treasure-sec-head">
              <h2>
                <span aria-hidden>🏅</span> THÀNH TỰU LỚN
              </h2>
              <button
                type="button"
                className="ph-text-link"
                onClick={() =>
                  document
                    .getElementById('ph-treasure-badges')
                    ?.scrollIntoView({ behavior: 'smooth', block: 'start' })
                }
              >
                Xem huy hiệu →
              </button>
            </header>
            <div className="ph-treasure-achieve">
              {bigAchievements.map((a) => (
                <article key={a.id}>
                  <span aria-hidden>{a.icon}</span>
                  <strong>{a.title}</strong>
                  <em>{a.value}</em>
                </article>
              ))}
            </div>
          </section>

          <section className="ph-treasure-sec">
            <header className="ph-treasure-sec-head">
              <h2>
                <span aria-hidden>❤️</span> KỶ NIỆM ĐÁNG NHỚ
              </h2>
              <button
                type="button"
                className="ph-text-link"
                onClick={openMemoriesSheet}
                disabled={treasureMemories.length === 0}
              >
                Xem tất cả →
              </button>
            </header>
            <div className="ph-treasure-memories">
              {treasureMemories.length === 0 ? (
                <p className="muted" style={{ padding: '12px 4px', margin: 0 }}>
                  {FAMILY_MEMORY_EMPTY}
                </p>
              ) : (
                treasureMemories.map((m) => (
                  <article key={m.id} className="ph-treasure-mem">
                    <div className="ph-treasure-mem-art">
                      {m.memory.photoUrl ? (
                        <img
                          src={withEvidenceAuth(m.memory.photoUrl)}
                          alt=""
                          className="ph-memory-photo"
                        />
                      ) : (
                        <span aria-hidden>{m.icon}</span>
                      )}
                      <button
                        type="button"
                        className={
                          m.memory.entry?.isFavorite
                            ? 'ph-treasure-mem-heart is-on'
                            : 'ph-treasure-mem-heart'
                        }
                        aria-label={
                          m.memory.entry?.isFavorite ? 'Bỏ thích' : 'Lưu / thích kỷ niệm'
                        }
                        disabled={memoryHeartBusy === m.memory.id}
                        onClick={() => void heartMemory(m.memory)}
                      >
                        {m.memory.entry?.isFavorite ? '❤️' : '🤍'}
                      </button>
                    </div>
                    <strong>{m.title}</strong>
                    <em>{m.time}</em>
                    {m.pending && m.redemptionId ? (
                      <button
                        type="button"
                        className="ph-treasure-gift-cta"
                        style={{ marginTop: 8 }}
                        disabled={fulfillBusyId === m.redemptionId || !parentMembershipId}
                        onClick={() => void handleFulfillRedemption(m.redemptionId!)}
                      >
                        {fulfillBusyId === m.redemptionId ? 'Đang xác nhận…' : 'Xác nhận trao quà'}
                      </button>
                    ) : null}
                  </article>
                ))
              )}
            </div>
          </section>
        </div>
      ) : null}

      <details className="ph-more" open={moreOpen} onToggle={(e) => setMoreOpen(e.currentTarget.open)}>
        <summary>Cài đặt máy · mã bố mẹ</summary>
        <div className="ph-more-body">
          <button
            type="button"
            className="pill"
            onClick={() => navigate('/family-admin')}
          >
            Quản trị gia đình
          </button>
          {!parentPushSubscribed && onEnableParentPush ? (
            <button type="button" className="pill" onClick={onEnableParentPush}>
              Bật nhắc push phụ huynh
            </button>
          ) : null}
          {offerLocalReminders && onEnableLocalReminders ? (
            <button type="button" className="pill is-soft" onClick={onEnableLocalReminders}>
              Bật nhắc trên máy (trình duyệt)
            </button>
          ) : null}
          {onToggleInAppChime ? (
            <button
              type="button"
              className={`pill${inAppChimeEnabled ? '' : ' is-soft'}`}
              onClick={onToggleInAppChime}
            >
              {inAppChimeEnabled
                ? 'Chuông trong app khi đến giờ: Bật'
                : 'Chuông trong app khi đến giờ: Tắt'}
            </button>
          ) : null}
          <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.82rem' }}>
            Push / nhắc trình duyệt dùng âm thanh hệ thống. Chuông trong app chỉ
            phát khi đang mở Daily Flow.
          </p>
          <ResetParentPinPanel />
          {appliedScreen.length > 0 ? (
            <ScreenBoundaryPanel
              flowDate={flow.flowDate}
              labelVi={appliedScreen[0]?.labelVi}
              compact
              title="Checklist cấu hình máy"
              body="Chỉ mở khi thỏa thuận màn hình đang áp dụng."
            />
          ) : null}
          <button
            type="button"
            className="pill is-soft"
            onClick={() => {
              clearOnboardingProfile(familyId);
              navigate('/onboarding');
            }}
          >
            Chạy lại AI Onboarding
          </button>
          <button
            type="button"
            className="pill is-soft"
            onClick={() =>
              void shareOrCopyNudge(
                `Nhà hôm nay: ${flow.doneCount}/${flow.totalCommitments}. Đã nhắc ${nudgeToday} lần.`,
                { preferShare: true },
              )
            }
          >
            Chia sẻ nhanh lên Zalo
          </button>
        </div>
      </details>

      <nav className="ph-tabbar ph-tabbar--b5" aria-label="Điều hướng bố mẹ">
        <button
          type="button"
          className={`ph-tab${tab === 'home' ? ' is-on' : ''}`}
          onClick={() => setTab('home')}
        >
          <span aria-hidden>🏠</span>
          Trang chủ
        </button>
        <button type="button" className="ph-tab" onClick={() => navigate('/who')}>
          <span aria-hidden>👥</span>
          Thành viên
        </button>
        <button
          type="button"
          className="ph-tab ph-tab-add"
          aria-label="Thêm kỷ niệm"
          title="Thêm kỷ niệm gia đình"
          onClick={() => setAddMemoryOpen(true)}
        >
          <span aria-hidden>+</span>
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'value' ? ' is-on' : ''}`}
          onClick={goReportHub}
        >
          <span aria-hidden>📊</span>
          Báo cáo
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'diary' ? ' is-on' : ''}`}
          onClick={() => setTab('diary')}
        >
          <span aria-hidden>📖</span>
          Nhật ký
        </button>
      </nav>

      {treasureHistoryOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setTreasureHistoryOpen(false)}
        >
          <div
            className="sheet ph-diary-mem-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Lịch sử đổi quà"
            onClick={(e) => e.stopPropagation()}
          >
            <h2>Lịch sử đổi quà</h2>
            {childRedemptions.length === 0 ? (
              <p className="muted">Chưa có lần đổi quà nào.</p>
            ) : (
              <div className="ph-diary-mem-sheet-list">
                {childRedemptions.map((r) => (
                  <article key={r.id} className="ph-diary-mem-sheet-card">
                    <span aria-hidden>{r.icon}</span>
                    <div>
                      <strong>{r.title}</strong>
                      <em>
                        {r.status === 'pending'
                          ? 'Chờ xác nhận'
                          : r.fulfilledAt
                            ? 'Đã trao quà'
                            : r.status || '—'}
                        {' · '}
                        {r.starCost}⭐
                      </em>
                    </div>
                  </article>
                ))}
              </div>
            )}
            <button type="button" className="pill is-soft" onClick={() => setTreasureHistoryOpen(false)}>
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      {diaryMemoriesOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setDiaryMemoriesOpen(false)}
        >
          <div
            className="sheet ph-diary-mem-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Kỷ niệm đẹp"
            onClick={(e) => e.stopPropagation()}
          >
            <h2>Kỷ niệm đẹp</h2>
            {memoryRecap && memoryRecap.totalCount > 0 ? (
              <p className="ph-memory-recap">{memoryRecap.headlineVi}</p>
            ) : null}
            {diaryPrettyMemories.length === 0 ? (
              <p className="muted">{FAMILY_MEMORY_EMPTY}</p>
            ) : (
              <div className="ph-diary-mem-sheet-list">
                {diaryPrettyMemories.map((m) => (
                  <article
                    key={m.id}
                    className={`ph-diary-mem-sheet-card${m.locked ? ' is-locked' : ''}`}
                  >
                    {m.photoUrl ? (
                      <img
                        src={withEvidenceAuth(m.photoUrl)}
                        alt=""
                        className="ph-diary-mem-sheet-photo"
                      />
                    ) : (
                      <span aria-hidden>{m.icon}</span>
                    )}
                    <div>
                      <strong>{m.title}</strong>
                      <em>
                        {m.date}
                        {m.entry?.noteVi ? ` · ${m.entry.noteVi}` : ''}
                      </em>
                    </div>
                    <button
                      type="button"
                      className={
                        m.entry?.isFavorite
                          ? 'ph-diary-mem-sheet-heart is-on'
                          : 'ph-diary-mem-sheet-heart'
                      }
                      aria-label={
                        m.entry?.isFavorite ? 'Bỏ thích' : 'Lưu / thích kỷ niệm'
                      }
                      disabled={memoryHeartBusy === m.id}
                      onClick={() => void heartMemory(m)}
                    >
                      {m.entry?.isFavorite ? '❤️' : '🤍'}
                    </button>
                    {m.isNew ? <span className="ph-diary-mem-new">Mới</span> : null}
                  </article>
                ))}
              </div>
            )}
            <div className="ph-diary-mem-sheet-actions">
              <button
                type="button"
                className="pill"
                onClick={() => {
                  setDiaryMemoriesOpen(false);
                  setAddMemoryOpen(true);
                }}
              >
                + Thêm kỷ niệm
              </button>
              <button
                type="button"
                className="pill is-soft"
                onClick={() => setDiaryMemoriesOpen(false)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {coachOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setCoachOpen(false)}
        >
          <div
            className="sheet ph-coach-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Famixa đồng hành"
            onClick={(e) => e.stopPropagation()}
          >
            <h2>Famixa đồng hành</h2>
            <p className="muted ph-coach-based" style={{ marginTop: 0 }}>
              {resolvedCoach.sourceLabelVi}
              {resolvedCoach.tips.length > 1
                ? ` · ${resolvedCoach.tips.length}/2 gợi ý hôm nay`
                : ' · tối đa 2 gợi ý/ngày'}
            </p>
            {resolvedCoach.tips.map((tip) => (
              <div key={tip.id} className="ph-coach-tip-card">
                <p className="ph-coach-tip-title">
                  <strong>{tip.titleVi}</strong>
                  {tip.source !== 'local_fallback' ? (
                    <span className="ph-coach-sot">Famixa</span>
                  ) : (
                    <span className="ph-coach-sot is-local">tạm</span>
                  )}
                </p>
                <p className="ph-coach-insight">{tip.insight}</p>
                <div className="ph-coach-block">
                  <strong>Làm ngay</strong>
                  <p>{tip.doThis}</p>
                </div>
                <div className="ph-coach-block">
                  <strong>Nên tránh</strong>
                  <p>{tip.avoid}</p>
                </div>
                {tip.styleTip ? (
                  <div className="ph-coach-block">
                    <strong>Cách tương tác</strong>
                    <p>{tip.styleTip}</p>
                  </div>
                ) : null}
                {tip.basedOn ? (
                  <p className="muted ph-coach-based">Dựa trên: {tip.basedOn}</p>
                ) : null}
                {parentMembershipId ? (
                  <div className="ph-coach-act">
                    <button
                      type="button"
                      className="pill"
                      disabled={
                        coachActBusyId === tip.id ||
                        actedTipIds.some((id) => id.toLowerCase() === tip.id.toLowerCase())
                      }
                      onClick={() => {
                        if (!parentMembershipId || coachActBusyId) return;
                        setCoachActBusyId(tip.id);
                        void recordParentCoachActed(familyId, {
                          memberId: parentMembershipId,
                          tipId: tip.id,
                          tipSource: tip.source,
                          slot: tip.slot,
                          titleVi: tip.titleVi,
                          flowDate: flow.flowDate,
                        })
                          .then((r) => {
                            setActedTipIds(r.actedTipIdsToday);
                            showActionToast(r.messageVi);
                          })
                          .catch((err: unknown) => {
                            if (isCapabilityPaywallError(err)) {
                              setCoachOpen(false);
                              openPaywall(getApiErrorMessage(err));
                              return;
                            }
                            showActionToast('Chưa ghi nhận được — thử lại nhé');
                          })
                          .finally(() => setCoachActBusyId(null));
                      }}
                    >
                      {actedTipIds.some((id) => id.toLowerCase() === tip.id.toLowerCase())
                        ? 'Đã thử ✓'
                        : 'Đã thử'}
                    </button>
                  </div>
                ) : null}
              </div>
            ))}
            <style>{`
              .ph-coach-tip-card {
                margin: 0 0 14px;
                padding: 12px 12px 8px;
                border-radius: 14px;
                background: #f4f7f5;
              }
              .ph-coach-tip-title {
                display: flex;
                align-items: center;
                justify-content: space-between;
                gap: 8px;
                margin: 0 0 8px;
                font-size: 0.95rem;
              }
              .ph-coach-sot {
                font-size: 10px;
                letter-spacing: 0.06em;
                text-transform: uppercase;
                padding: 2px 8px;
                border-radius: 999px;
                background: #d7eee0;
                color: #1f4f45;
                font-weight: 700;
              }
              .ph-coach-sot.is-local {
                background: #eee6d8;
                color: #6b5420;
              }
              .ph-coach-act {
                margin: 10px 0 4px;
              }
            `}</style>
            <div className="ph-diary-mem-sheet-actions">
              <button
                type="button"
                className="pill is-soft"
                onClick={() =>
                  void shareOrCopyNudge(
                    `Famixa:\n${resolvedCoach.tips
                      .map((t) => `• ${t.titleVi}\n${t.doThis}\nTránh: ${t.avoid}`)
                      .join('\n\n')}`,
                    { preferShare: true },
                  )
                    .then((mode) =>
                      showActionToast(
                        mode === 'shared'
                          ? 'Đã mở chia sẻ gợi ý'
                          : 'Đã copy gợi ý — dán Zalo nếu cần',
                      ),
                    )
                    .catch((err) => {
                      if (err instanceof DOMException && err.name === 'AbortError') return;
                      showActionToast('Chưa chia sẻ được — thử lại nhé');
                    })
                }
              >
                Chia sẻ gợi ý
              </button>
              <button type="button" className="pill" onClick={() => setCoachOpen(false)}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {addMemoryOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => !addMemoryBusy && setAddMemoryOpen(false)}
        >
          <div
            className="sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Thêm kỷ niệm"
            onClick={(e) => e.stopPropagation()}
          >
            <h2>Thêm kỷ niệm gia đình</h2>
            <p className="muted">Ghi lại một khoảnh khắc muốn giữ — không cần hoàn hảo.</p>
            <label className="ph-add-memory-field">
              <span>Tiêu đề</span>
              <input
                type="text"
                value={addMemoryTitle}
                maxLength={200}
                placeholder="VD: Cả nhà ăn tối không điện thoại"
                onChange={(e) => setAddMemoryTitle(e.target.value)}
                autoFocus
              />
            </label>
            <label className="ph-add-memory-field">
              <span>Ghi chú (tuỳ chọn)</span>
              <textarea
                value={addMemoryNote}
                maxLength={600}
                rows={3}
                placeholder="Cảm xúc / chi tiết ngắn…"
                onChange={(e) => setAddMemoryNote(e.target.value)}
              />
            </label>
            <div className="ph-diary-mem-sheet-actions">
              <button
                type="button"
                className="pill"
                disabled={!addMemoryTitle.trim() || addMemoryBusy}
                onClick={() => void submitManualMemory()}
              >
                {addMemoryBusy ? 'Đang lưu…' : 'Lưu kỷ niệm'}
              </button>
              <button
                type="button"
                className="pill is-soft"
                disabled={addMemoryBusy}
                onClick={() => setAddMemoryOpen(false)}
              >
                Huỷ
              </button>
            </div>
          </div>
        </div>
      ) : null}

      <FamilyModeSheet
        familyId={familyId}
        parentMembershipId={parentMembershipId}
        open={modeSheetOpen}
        onClose={() => setModeSheetOpen(false)}
        onActivated={(result) => showActionToast(result.messageVi)}
      />

      {softGuide ? (
        <div className="sheet-backdrop" role="presentation" onClick={() => setSoftGuide(null)}>
          <div
            className="sheet"
            role="dialog"
            aria-modal="true"
            aria-label={softGuide.titleVi}
            onClick={(e) => e.stopPropagation()}
          >
            <h2>{softGuide.titleVi}</h2>
            <p className="muted">{softGuide.bodyVi}</p>
            <ScreenBoundaryPanel
              flowDate={flow.flowDate}
              labelVi={softGuide.titleVi}
              title="Checklist cấu hình máy"
              body="Đánh dấu từng bước sau khi bố mẹ cấu hình trên máy con."
            />
            <button type="button" className="pill is-soft" onClick={() => setSoftGuide(null)}>
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      <PaywallSheet
        open={paywallOpen}
        onClose={() => {
          setPaywallOpen(false);
          setPaywallReason(null);
        }}
        familyId={familyId}
        subscription={subscription}
        reasonVi={paywallReason}
      />
    </section>
  );
}
