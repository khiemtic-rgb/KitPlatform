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
  incrementFamilyNudge,
  proposeScreenWallet,
  scanAdaptiveProposals,
  fetchFamilyScore,
  type FamilyMemberMood,
  type ChildGratitude,
  type FamilyMemoryEntry,
  type FamilyMemoryRecap,
  type RewardCatalogItem,
  type RewardRedemption,
  type FamilyScore,
  fetchBehaviorTwin,
  type BehaviorTwin,
  fetchFamilyBehaviorTwin,
  updateRetirementPolicy,
  type FamilyBehaviorTwin,
  fetchFamilyCoachInsight,
  type FamilyCoachInsight,
  fetchBehaviorCoach,
  type BehaviorCoach,
  fetchParentSuccessRop,
  type ParentSuccessRop,
  formatParentSuccessRopShare,
  fetchFamilyAiWinsDigest,
  fetchFamilyAiLetter,
  formatFamilyAiLetterShare,
  type FamilyAiWinsDigest,
  type FamilyAiLetter,
  fetchFamilySubscription,
  type FamilySubscription,
  fetchParentSuccessEveningCheckin,
  upsertParentSuccessEveningCheckin,
  fetchParentAchievements,
  type ParentSuccessCheckin,
  type ParentAchievements,
  fetchParentCoachActedToday,
  recordParentCoachActed,
  fetchFamilyReplay,
  type FamilyReplay,
} from '@/shared/api/family-os.api';
import { DecisionInboxPanel } from '@/modules/flow/DecisionInboxPanel';
import { FamilyModeSheet } from '@/modules/flow/FamilyModeSheet';
import { buildNudgeText, shareOrCopyNudge } from '@/shared/nudge/nudge';
import {
  getNudgeCount,
  isParentVerified,
  markParentVerified,
  previousCalendarDate,
  setNudgeCountLocal,
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
import { FamilyValuePanel } from '@/modules/flow/FamilyValuePanel';
import { BillingBanner } from '@/shared/ui/BillingBanner';
import { PaywallSheet } from '@/shared/ui/PaywallSheet';
import {
  getApiErrorMessage,
  isCapabilityPaywallError,
} from '@/shared/billing/capability-error';
import { buildTrialRopConversion } from '@/shared/billing/trial-rop-conversion';
import {
  buildFamilyMemories,
  FAMILY_MEMORY_EMPTY,
  FAMILY_MEMORY_VISIBLE,
  type FamilyMemory,
} from '@/shared/flow/family-memories';
import {
  buildFoxyNotice,
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
type ParentTab = 'home' | 'tasks' | 'rewards' | 'value';

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
  void familyName;
  const [softGuide, setSoftGuide] = useState<SoftLockGuide | null>(null);
  const [missionFilter, setMissionFilter] = useState<MissionFilter>('all');
  const [tab, setTab] = useState<ParentTab>('home');
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
  const [behaviorTwin, setBehaviorTwin] = useState<BehaviorTwin | null>(null);
  const [familyTwin, setFamilyTwin] = useState<FamilyBehaviorTwin | null>(null);
  const [coachInsight, setCoachInsight] = useState<FamilyCoachInsight | null>(null);
  const [behaviorCoach, setBehaviorCoach] = useState<BehaviorCoach | null>(null);
  const [rop, setRop] = useState<ParentSuccessRop | null>(null);
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [paywallOpen, setPaywallOpen] = useState(false);
  const [paywallReason, setPaywallReason] = useState<string | null>(null);
  const [ropBlocked, setRopBlocked] = useState(false);
  const [twinBlocked, setTwinBlocked] = useState(false);
  const [winsDigest, setWinsDigest] = useState<FamilyAiWinsDigest | null>(null);
  const [aiLetter, setAiLetter] = useState<FamilyAiLetter | null>(null);
  const [eveningCheckin, setEveningCheckin] = useState<ParentSuccessCheckin | null>(null);
  const [parentAchievements, setParentAchievements] = useState<ParentAchievements | null>(null);
  const [qLessNudge, setQLessNudge] = useState(false);
  const [qLessTension, setQLessTension] = useState(false);
  const [qQualityTime, setQQualityTime] = useState(false);
  const [checkinBusy, setCheckinBusy] = useState(false);
  const [actedTipIds, setActedTipIds] = useState<string[]>([]);
  const [coachActBusyId, setCoachActBusyId] = useState<string | null>(null);
  const [familyReplay, setFamilyReplay] = useState<FamilyReplay | null>(null);
  const [observeBusy, setObserveBusy] = useState(false);
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
    void fetchBehaviorTwin(familyId)
      .then((t) => {
        if (!cancelled) {
          setBehaviorTwin(t);
          setTwinBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setBehaviorTwin(null);
          if (isCapabilityPaywallError(err)) setTwinBlocked(true);
        }
      });
    void fetchFamilyBehaviorTwin(familyId)
      .then((t) => {
        if (!cancelled) {
          setFamilyTwin(t);
          setTwinBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setFamilyTwin(null);
          if (isCapabilityPaywallError(err)) setTwinBlocked(true);
        }
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
    void fetchParentSuccessRop(familyId, { days: 30, asOf: flow.flowDate })
      .then((r) => {
        if (!cancelled) {
          setRop(r);
          setRopBlocked(false);
        }
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setRop(null);
          if (isCapabilityPaywallError(err)) setRopBlocked(true);
        }
      });
    void fetchFamilyAiWinsDigest(familyId, { to: flow.flowDate, limit: 5 })
      .then((r) => {
        if (!cancelled) setWinsDigest(r);
      })
      .catch(() => {
        if (!cancelled) setWinsDigest(null);
      });
    void fetchFamilyAiLetter(familyId)
      .then((r) => {
        if (!cancelled) setAiLetter(r);
      })
      .catch(() => {
        if (!cancelled) setAiLetter(null);
      });
    void fetchParentAchievements(familyId, { asOf: flow.flowDate })
      .then((r) => {
        if (!cancelled) setParentAchievements(r);
      })
      .catch(() => {
        if (!cancelled) setParentAchievements(null);
      });
    void fetchFamilyReplay(familyId)
      .then((r) => {
        if (!cancelled) setFamilyReplay(r);
      })
      .catch(() => {
        if (!cancelled) setFamilyReplay(null);
      });
    if (parentMembershipId) {
      void fetchParentSuccessEveningCheckin(familyId, parentMembershipId, flow.flowDate)
        .then((r) => {
          if (cancelled) return;
          setEveningCheckin(r);
          if (r) {
            setQLessNudge(r.qLessNudge);
            setQLessTension(r.qLessTension);
            setQQualityTime(r.qQualityTime);
          }
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

  const nudgeDeltaPct =
    nudgeYesterday > 0
      ? Math.round(((nudgeYesterday - nudgeToday) / nudgeYesterday) * 100)
      : nudgeToday === 0
        ? 100
        : 0;

  const autonomyGain = useMemo(() => {
    const days = [...(glance?.days ?? [])].sort((a, b) => a.date.localeCompare(b.date));
    if (days.length < 7) return null;
    const recent = days.slice(-3);
    const prior = days.slice(-7, -3);
    if (prior.length === 0) return null;
    const ratio = (list: typeof days) => {
      const done = list.reduce((s, d) => s + d.childDone, 0);
      const total = list.reduce(
        (s, d) => s + Math.max(1, d.childDone + d.childSkipped + d.childOpen),
        0,
      );
      return done / total;
    };
    const gain = Math.round((ratio(recent) - ratio(prior)) * 100);
    return Number.isFinite(gain) ? gain : null;
  }, [glance?.days]);

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
  const helloWho = viewerName.trim() || greetName(viewerName);
  const movieLeft = unlockGap;

  const foxyNotice = useMemo(
    () =>
      buildFoxyNotice({
        flowDate: flow.flowDate,
        childShort,
        parentRole,
        done: scopedDone,
        total: scopedTotal,
        open: Math.max(0, scopedTotal - scopedDone),
        nudgeToday,
        nudgeYesterday,
        streak: glance?.currentStreak ?? 0,
        beautifulToday: Boolean(glance?.todayIsBeautifulDay),
        needHelp: attentionItems.length,
      }),
    [
      flow.flowDate,
      childShort,
      parentRole,
      scopedDone,
      scopedTotal,
      nudgeToday,
      nudgeYesterday,
      glance?.currentStreak,
      glance?.todayIsBeautifulDay,
      attentionItems.length,
    ],
  );

  const supportCards = useMemo(() => {
    const tones = ['pink', 'lemon', 'lilac'] as const;
    return attentionItems.slice(0, 2).map((a, i) => {
      if (a.kind === 'consequence') {
        return {
          id: a.id,
          title: a.event.labelVi,
          note: `${a.event.memberName?.trim() || childShort} · thỏa thuận chờ quyết định`,
          deadline: 'Cần quyết định',
          icon: '📄',
          tone: tones[i] ?? 'lilac',
          kind: a.kind,
          raw: a,
        };
      }
      const item = a.item;
      const deadline = item.windowEnd
        ? `Trước ${item.windowEnd.slice(0, 5)}`
        : a.kind === 'overdue'
          ? lateLabel(item, flow.localTime)
          : 'Chờ kiểm tra';
      const note = warmTaskSupportNote({
        title: item.title,
        childShort,
        parentRole,
        kind: a.kind === 'overdue' ? 'overdue' : 'awaiting',
        flowDate: flow.flowDate,
        itemId: item.id,
      });
      return {
        id: a.id,
        title: item.title,
        note,
        deadline,
        icon: taskIcon(item.title),
        tone: tones[i] ?? 'pink',
        kind: a.kind,
        raw: a,
      };
    });
  }, [attentionItems, childShort, flow.localTime, flow.flowDate, parentRole]);

  const memoryCards = useMemo(() => {
    const merged = buildFamilyMemories({
      childShort,
      redemptions: childRedemptions,
      teamUnlocks,
      doneItems: scopedCommitments.filter((c) => c.status === 'done'),
      saved: savedMemories,
      voice: 'parent',
    }).slice(0, 6);
    if (merged.length > 0) {
      return merged.map((m) => ({
        id: m.id,
        icon: m.icon,
        title: m.title,
        time: m.date,
        memory: m,
      }));
    }
    return scopedCommitments
      .filter((c) => c.status === 'done')
      .slice(0, 4)
      .map((c) => ({
        id: c.id,
        icon: taskIcon(c.title),
        title: `${childShort} · ${c.title}`,
        time: formatClock(c.completedAt)
          ? `Hôm nay, ${formatClock(c.completedAt)}`
          : 'Hôm nay',
        memory: undefined as FamilyMemory | undefined,
      }));
  }, [scopedCommitments, childShort, childRedemptions, teamUnlocks, savedMemories]);

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
    tab === 'value' && selectedDiaryDay ? selectedDiaryDay.key : flow.flowDate;

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
      setTab('value');
      openMemoriesSheet();
    } catch {
      showDiaryToast('Chưa thêm được — thử lại nhé');
    } finally {
      setAddMemoryBusy(false);
    }
  };

  const goDiaryDay = (delta: -1 | 1) => {
    setTab('value');
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

  const isTrialSub = subscription?.status === 'trial' && subscription.isEntitled;
  const trialDaysLeft = subscription?.trialDaysRemaining;
  const trialRop =
    isTrialSub && rop ? buildTrialRopConversion(rop) : null;
  const showTrialConversion = Boolean(trialRop);
  const showRopTeaser =
    !rop &&
    (ropBlocked ||
      subscription?.tierCode === 'free' ||
      (subscription != null && !subscription.isEntitled));

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
      {tab === 'home' ? <BillingBanner familyId={familyId} /> : null}
      {tab !== 'tasks' && tab !== 'rewards' && tab !== 'value' ? (
      <header className="ph-top">
        <div className="ph-identity">
          <div className="ph-mom-avatar" aria-hidden>
            {parentAvatar}
          </div>
          <div>
            <h1 className="ph-hello">
              Chào {helloWho}! <span aria-hidden>👋</span>
            </h1>
            <p className="ph-date">{formatFlowDay(flow.flowDate)}</p>
          </div>
        </div>
        <div className="ph-top-right">
          <button
            type="button"
            className="ph-bell"
            aria-label="Thông báo"
            onClick={() => scrollToMissions('need_help')}
          >
            <span aria-hidden>🔔</span>
            {attentionItems.length > 0 ? (
              <i className="ph-bell-badge">{Math.min(attentionItems.length, 9)}</i>
            ) : null}
          </button>
          {renderChildPicker('home')}
        </div>
      </header>
      ) : null}

      {tab === 'home' ? (
        <>
              <article className="ph-pulse-hero" role="status">
                <p className="ph-pulse-eyebrow">Parent Success · hôm nay</p>
                <div className="ph-pulse-score-row">
                  <div className="ph-pulse-score" aria-label={`Family Score ${parentPulse.familyScore}`}>
                    <span className="ph-pulse-score-num">{parentPulse.familyScore}</span>
                    <span className="ph-pulse-score-lbl">Family Score</span>
                  </div>
                  <div className="ph-pulse-copy">
                    <h2>{parentPulse.headlineVi}</h2>
                    <p className="ph-pulse-mood">{parentPulse.dayMoodVi}</p>
                  </div>
                </div>
                <ul className="ph-pulse-lines">
                  <li>{parentPulse.nudgeLineVi}</li>
                  <li>{parentPulse.autonomyLineVi}</li>
                  <li>{parentPulse.peaceLineVi}</li>
                </ul>
                {parentPulse.insightVi ? (
                  <p className="ph-pulse-insight">
                    <strong>Insight hôm nay:</strong> {parentPulse.insightVi}
                  </p>
                ) : null}
                <div className="ph-pulse-actions">
                  <button type="button" className="ph-pulse-cta" onClick={openCoachOrPaywall}>
                    Famixa đồng hành →
                  </button>
                  <button
                    type="button"
                    className="ph-text-link"
                    onClick={() => {
                      document
                        .getElementById('ph-bos-detail')
                        ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
                    }}
                  >
                    Chi tiết tín hiệu nhà
                  </button>
                </div>
                {parentPulse.confidence === 'low' ? (
                  <p className="ph-pulse-note">
                    Đang học nhịp nhà — điểm sẽ rõ hơn sau vài ngày dùng.
                  </p>
                ) : null}
              </article>

              {showTrialConversion && trialRop && rop ? (
                <article
                  className={`ph-rop-card ph-rop-trial${
                    trialDaysLeft != null && trialDaysLeft <= 7 ? ' is-urgent' : ''
                  }`}
                  id="ph-rop-home"
                >
                  <p className="ph-rop-eyebrow">
                    Trial · còn {trialDaysLeft != null ? `${trialDaysLeft} ngày` : 'một chút'}
                  </p>
                  <h2>{trialRop.titleVi}</h2>
                  <p className="ph-rop-score">{trialRop.summaryVi}</p>
                  <div className="ph-rop-metrics" aria-label="Kết quả trial">
                    <div>
                      <span className="ph-rop-metric-label">Lần phải nhắc</span>
                      <strong>
                        {trialRop.nudgeBefore} → {trialRop.nudgeAfter}
                        {trialRop.nudgeDeltaPct != null && trialRop.nudgeDeltaPct > 0
                          ? ` (−${trialRop.nudgeDeltaPct}%)`
                          : ''}
                      </strong>
                    </div>
                    <div>
                      <span className="ph-rop-metric-label">Con tự bắt đầu</span>
                      <strong>
                        {trialRop.selfBefore} → {trialRop.selfAfter}
                        {trialRop.selfDeltaPct != null && trialRop.selfDeltaPct > 0
                          ? ` (+${trialRop.selfDeltaPct}%)`
                          : ''}
                      </strong>
                    </div>
                  </div>
                  {rop.minutesSavedEstimate > 0 ? (
                    <p className="ph-rop-minutes">
                      ~{rop.minutesSavedEstimate} phút nhắc nhở được tiết kiệm (ước tính)
                    </p>
                  ) : null}
                  <div className="ph-rop-actions">
                    <button
                      type="button"
                      className="ph-pulse-cta"
                      onClick={() =>
                        openPaywall(
                          'Giữ nhịp Pro sau trial — Coach, ROP và Letter tiếp tục đo giúp bạn.',
                        )
                      }
                    >
                      {trialRop.ctaVi}
                    </button>
                    <button
                      type="button"
                      className="ph-text-link"
                      onClick={() =>
                        void shareOrCopyNudge(
                          formatParentSuccessRopShare(rop, familyName),
                          { preferShare: true },
                        ).then((mode) =>
                          showActionToast(
                            mode === 'shared' ? 'Đã mở chia sẻ ROP' : 'Đã copy ROP',
                          ),
                        )
                      }
                    >
                      Chia sẻ
                    </button>
                  </div>
                  {rop.isPartial && rop.partialNoteVi ? (
                    <p className="ph-pulse-note">{rop.partialNoteVi}</p>
                  ) : null}
                </article>
              ) : rop ? (
                <article className="ph-rop-card" id="ph-rop-home">
                  <p className="ph-rop-eyebrow">ROP · Return on Parenting</p>
                  <h2>{rop.headlineVi}</h2>
                  {rop.growthScore != null ? (
                    <p className="ph-rop-score">Growth {rop.growthScore}/100 · {rop.windowDays} ngày</p>
                  ) : (
                    <p className="ph-rop-score">{rop.windowDays} ngày · {rop.dataDays} ngày có tín hiệu</p>
                  )}
                  <ul className="ph-rop-bullets">
                    {rop.growthBulletsVi.slice(0, 3).map((b) => (
                      <li key={b}>{b}</li>
                    ))}
                  </ul>
                  {rop.minutesSavedEstimate > 0 ? (
                    <p className="ph-rop-minutes">
                      ~{rop.minutesSavedEstimate} phút nhắc nhở được tiết kiệm (ước tính)
                    </p>
                  ) : null}
                  <div className="ph-rop-actions">
                    <button type="button" className="ph-pulse-cta" onClick={() => setTab('value')}>
                      Xem Growth Report →
                    </button>
                    <button
                      type="button"
                      className="ph-text-link"
                      onClick={() =>
                        void shareOrCopyNudge(
                          formatParentSuccessRopShare(rop, familyName),
                          { preferShare: true },
                        ).then((mode) =>
                          showActionToast(
                            mode === 'shared' ? 'Đã mở chia sẻ ROP' : 'Đã copy ROP',
                          ),
                        )
                      }
                    >
                      Chia sẻ
                    </button>
                  </div>
                  {rop.isPartial && rop.partialNoteVi ? (
                    <p className="ph-pulse-note">{rop.partialNoteVi}</p>
                  ) : null}
                </article>
              ) : showRopTeaser ? (
                <article className="ph-rop-card ph-rop-teaser" id="ph-rop-home">
                  <p className="ph-rop-eyebrow">ROP · có trong Peace Plan</p>
                  <h2>Đo “nhẹ tay hơn” sau 30 ngày</h2>
                  <p className="ph-rop-score">
                    {subscription?.upgradeHintVi ||
                      'Free giữ routine. Growth Report / Coach mở khi nâng Family Peace Plan.'}
                  </p>
                  <div className="ph-rop-actions">
                    <button
                      type="button"
                      className="ph-pulse-cta"
                      onClick={() => openPaywall(subscription?.upgradeHintVi)}
                    >
                      Xem Family Peace Plan · 199.000đ
                    </button>
                  </div>
                </article>
              ) : null}

              {rop?.hasAiPlusDeep && rop.deepPlaybookVi ? (
                <article className="ph-rop-card ph-aiplus-card" id="ph-aiplus-home">
                  <p className="ph-rop-eyebrow">AI+ · Playbook tuần</p>
                  <h2>Đồng hành chuyên sâu</h2>
                  <p className="ph-rop-score">{rop.deepPlaybookVi}</p>
                  {rop.deepActionsVi && rop.deepActionsVi.length > 0 ? (
                    <ul className="ph-rop-bullets">
                      {rop.deepActionsVi.slice(0, 3).map((a) => (
                        <li key={a}>{a}</li>
                      ))}
                    </ul>
                  ) : null}
                </article>
              ) : subscription?.tierCode === 'pro' &&
                subscription.isEntitled &&
                subscription.status !== 'trial' ? (
                <article className="ph-rop-card ph-rop-teaser" id="ph-aiplus-home">
                  <p className="ph-rop-eyebrow">AI+ · sắp mở rộng</p>
                  <h2>Playbook tuần & scan sâu hơn</h2>
                  <p className="ph-rop-score">
                    Peace Plan đã mở Coach/ROP. AI+ thêm playbook tuần và đề xuất anh chị / tối.
                  </p>
                  <div className="ph-rop-actions">
                    <button
                      type="button"
                      className="ph-pulse-cta"
                      onClick={() =>
                        openPaywall(
                          'Nâng AI+ để mở Weekly Deep Playbook và adaptive scan mở rộng.',
                        )
                      }
                    >
                      Xem AI+ · 399.000đ
                    </button>
                  </div>
                </article>
              ) : null}

              {winsDigest && winsDigest.wins.length > 0 ? (
                <article className="ph-wins-card" id="ph-wins-home">
                  <p className="ph-rop-eyebrow">Famixa · Wins</p>
                  <h2>{winsDigest.headlineVi}</h2>
                  {winsDigest.subheadVi ? (
                    <p className="ph-rop-score">{winsDigest.subheadVi}</p>
                  ) : null}
                  <ul className="ph-wins-strip">
                    {winsDigest.wins.slice(0, 4).map((w) => (
                      <li key={w.id}>
                        <span aria-hidden>{w.icon ?? '✨'}</span>
                        <span>{w.titleVi}</span>
                      </li>
                    ))}
                  </ul>
                  <div className="ph-rop-actions">
                    <button type="button" className="ph-pulse-cta" onClick={() => setTab('value')}>
                      Timeline & Letter →
                    </button>
                  </div>
                </article>
              ) : null}

              {aiLetter && !aiLetter.isThinData ? (
                <article className="ph-letter-card" id="ph-letter-home">
                  <p className="ph-rop-eyebrow">Famixa · Letter · {aiLetter.monthLabelVi}</p>
                  <h2>{aiLetter.greetingVi}</h2>
                  <p className="ph-letter-body">
                    {aiLetter.bodyVi.length > 180
                      ? `${aiLetter.bodyVi.slice(0, 180).trim()}…`
                      : aiLetter.bodyVi}
                  </p>
                  <div className="ph-rop-actions">
                    <button type="button" className="ph-pulse-cta" onClick={() => setTab('value')}>
                      Đọc full Letter →
                    </button>
                    <button
                      type="button"
                      className="ph-text-link"
                      onClick={() =>
                        void shareOrCopyNudge(formatFamilyAiLetterShare(aiLetter), {
                          preferShare: true,
                        }).then((mode) =>
                          showActionToast(
                            mode === 'shared' ? 'Đã mở chia sẻ Letter' : 'Đã copy Letter',
                          ),
                        )
                      }
                    >
                      Chia sẻ
                    </button>
                  </div>
                </article>
              ) : null}

              {parentMembershipId ? (
                <article className="ph-checkin-card" id="ph-3q-home">
                  <p className="ph-rop-eyebrow">Famixa · 3 câu tối</p>
                  <h2>
                    {eveningCheckin
                      ? 'Đã trả lời hôm nay'
                      : 'Hôm nay nhà mình thế nào?'}
                  </h2>
                  <div className="ph-3q-list">
                    {(
                      [
                        ['qLessNudge', 'Đã phải nhắc ít hơn?', qLessNudge, setQLessNudge],
                        ['qLessTension', 'Nhà bớt căng thẳng hơn?', qLessTension, setQLessTension],
                        ['qQualityTime', 'Có thời gian chất lượng với con?', qQualityTime, setQQualityTime],
                      ] as const
                    ).map(([key, label, value, setter]) => (
                      <label key={key} className="ph-3q-row">
                        <span>{label}</span>
                        <button
                          type="button"
                          className={`ph-3q-toggle${value ? ' is-on' : ''}`}
                          aria-pressed={value}
                          onClick={() => setter(!value)}
                        >
                          {value ? 'Có' : 'Chưa'}
                        </button>
                      </label>
                    ))}
                  </div>
                  {eveningCheckin?.reflectionVi ? (
                    <p className="ph-letter-body">{eveningCheckin.reflectionVi}</p>
                  ) : null}
                  <div className="ph-rop-actions">
                    <button
                      type="button"
                      className="ph-pulse-cta"
                      disabled={checkinBusy}
                      onClick={() => {
                        if (!parentMembershipId || checkinBusy) return;
                        setCheckinBusy(true);
                        void upsertParentSuccessEveningCheckin(familyId, {
                          memberId: parentMembershipId,
                          flowDate: flow.flowDate,
                          qLessNudge,
                          qLessTension,
                          qQualityTime,
                        })
                          .then((r) => {
                            setEveningCheckin(r);
                            showActionToast('Famixa đã ghi nhận tối nay');
                            return fetchParentAchievements(familyId, { asOf: flow.flowDate });
                          })
                          .then((a) => setParentAchievements(a))
                          .catch(() => showActionToast('Chưa lưu được — thử lại nhé'))
                          .finally(() => setCheckinBusy(false));
                      }}
                    >
                      {eveningCheckin ? 'Cập nhật' : 'Gửi Famixa'}
                    </button>
                  </div>
                </article>
              ) : null}

              {parentAchievements ? (
                <article className="ph-achv-card" id="ph-parent-achv">
                  <p className="ph-rop-eyebrow">Famixa · Ghi nhận bố mẹ</p>
                  <h2>{parentAchievements.headlineVi}</h2>
                  <ul className="ph-achv-list">
                    {parentAchievements.items.map((a) => (
                      <li key={a.code} className={a.unlocked ? 'is-on' : ''}>
                        <span aria-hidden>{a.icon}</span>
                        <div>
                          <strong>
                            {a.titleVi}
                            {a.unlocked ? ' · mở' : ''}
                          </strong>
                          <p>{a.unlocked ? a.detailVi : a.progressHintVi}</p>
                        </div>
                      </li>
                    ))}
                  </ul>
                </article>
              ) : null}

              {familyReplay && !familyReplay.isThinData ? (
                <article className="ph-letter-card" id="ph-replay-home">
                  <p className="ph-rop-eyebrow">Famixa · Replay</p>
                  <h2>{familyReplay.titleVi}</h2>
                  <p className="ph-letter-body">
                    {familyReplay.openingVi.length > 160
                      ? `${familyReplay.openingVi.slice(0, 160).trim()}…`
                      : familyReplay.openingVi}
                  </p>
                  <ul className="ph-wins-strip">
                    {familyReplay.scenes.slice(0, 4).map((s, i) => (
                      <li key={`${s.kind}-${i}`}>
                        <span aria-hidden>{s.icon}</span>
                        <span>{s.titleVi}</span>
                      </li>
                    ))}
                  </ul>
                  <div className="ph-rop-actions">
                    <button type="button" className="ph-pulse-cta" onClick={() => setTab('value')}>
                      Xem Replay đầy đủ →
                    </button>
                    <button
                      type="button"
                      className="ph-text-link"
                      onClick={() =>
                        void shareOrCopyNudge(familyReplay.shareTextVi, {
                          preferShare: true,
                        }).then((mode) =>
                          showActionToast(
                            mode === 'shared' ? 'Đã mở chia sẻ Replay' : 'Đã copy Replay',
                          ),
                        )
                      }
                    >
                      Chia sẻ
                    </button>
                  </div>
                </article>
              ) : null}

              <style>{`
                .ph-rop-card {
                  margin: 0 0 14px;
                  padding: 16px;
                  border-radius: 18px;
                  background: #f3f7f4;
                  color: #1a3344;
                  border: 1px solid #d5e6dc;
                }
                .ph-rop-card.ph-rop-trial {
                  background: linear-gradient(165deg, #eef6f1 0%, #f7f4ef 100%);
                  border-color: #c5ddcf;
                }
                .ph-rop-card.ph-rop-trial.is-urgent {
                  border-color: #c9a882;
                  box-shadow: 0 0 0 1px rgba(180, 120, 60, 0.12);
                }
                .ph-rop-card.ph-rop-teaser {
                  background: #f7f4ef;
                  border-style: dashed;
                }
                .ph-rop-metrics {
                  display: grid;
                  grid-template-columns: 1fr 1fr;
                  gap: 10px;
                  margin: 0 0 12px;
                }
                .ph-rop-metrics > div {
                  padding: 10px 12px;
                  border-radius: 12px;
                  background: rgba(255, 255, 255, 0.65);
                }
                .ph-rop-metric-label {
                  display: block;
                  font-size: 0.72rem;
                  letter-spacing: 0.04em;
                  text-transform: uppercase;
                  opacity: 0.65;
                  margin-bottom: 4px;
                }
                .ph-rop-metrics strong {
                  font-size: 0.95rem;
                  line-height: 1.3;
                }
                .ph-rop-eyebrow {
                  margin: 0 0 6px;
                  font-size: 11px;
                  letter-spacing: 0.07em;
                  text-transform: uppercase;
                  opacity: 0.65;
                }
                .ph-rop-card h2 {
                  margin: 0 0 6px;
                  font-size: 1.08rem;
                  line-height: 1.35;
                }
                .ph-rop-score { margin: 0 0 8px; font-size: 0.9rem; opacity: 0.85; }
                .ph-rop-bullets {
                  margin: 0 0 10px;
                  padding-left: 18px;
                  font-size: 0.88rem;
                  line-height: 1.4;
                }
                .ph-rop-minutes { margin: 0 0 10px; font-size: 0.88rem; font-weight: 650; }
                .ph-rop-actions {
                  display: flex;
                  flex-wrap: wrap;
                  gap: 12px;
                  align-items: center;
                }
                .ph-rop-card .ph-text-link { color: #2a5f4a; }
                .ph-wins-card, .ph-letter-card, .ph-checkin-card, .ph-achv-card {
                  margin: 0 0 14px;
                  padding: 16px;
                  border-radius: 18px;
                  background: #f7f4ef;
                  color: #1a3344;
                  border: 1px solid #e6ddd0;
                }
                .ph-wins-card h2, .ph-letter-card h2, .ph-checkin-card h2, .ph-achv-card h2 {
                  margin: 0 0 6px;
                  font-size: 1.05rem;
                  line-height: 1.35;
                }
                .ph-wins-strip {
                  list-style: none;
                  margin: 0 0 12px;
                  padding: 0;
                  display: flex;
                  flex-direction: column;
                  gap: 8px;
                }
                .ph-wins-strip li {
                  display: flex;
                  gap: 8px;
                  align-items: flex-start;
                  font-size: 0.88rem;
                  line-height: 1.35;
                }
                .ph-letter-body {
                  margin: 0 0 12px;
                  font-size: 0.9rem;
                  line-height: 1.45;
                  opacity: 0.9;
                }
                .ph-wins-card .ph-text-link,
                .ph-letter-card .ph-text-link { color: #2a5f4a; }
                .ph-3q-list {
                  display: flex;
                  flex-direction: column;
                  gap: 10px;
                  margin: 10px 0 12px;
                }
                .ph-3q-row {
                  display: flex;
                  align-items: center;
                  justify-content: space-between;
                  gap: 12px;
                  font-size: 0.9rem;
                }
                .ph-3q-toggle {
                  border: 1px solid #c9b8a0;
                  background: #fff;
                  color: #5a4a3a;
                  border-radius: 999px;
                  padding: 6px 14px;
                  font-weight: 650;
                  min-width: 64px;
                }
                .ph-3q-toggle.is-on {
                  background: #2a5f4a;
                  border-color: #2a5f4a;
                  color: #fff;
                }
                .ph-achv-list {
                  list-style: none;
                  margin: 10px 0 0;
                  padding: 0;
                  display: flex;
                  flex-direction: column;
                  gap: 10px;
                }
                .ph-achv-list li {
                  display: flex;
                  gap: 10px;
                  align-items: flex-start;
                  opacity: 0.55;
                  font-size: 0.88rem;
                  line-height: 1.35;
                }
                .ph-achv-list li.is-on { opacity: 1; }
                .ph-achv-list p { margin: 2px 0 0; }
              `}</style>

              <article className="ph-movie-hero ph-movie-hero--secondary">
                <div className="ph-movie-copy">
                  <p className="ph-movie-eyebrow">
                    Cùng nhau <span aria-hidden>❤️</span>
                  </p>
                  <h2>
                    {todayUnlock?.status === 'confirmed'
                      ? `${todayUnlock.labelVi} đã mở — cả nhà tận hưởng!`
                      : todayUnlock?.status === 'pending_confirm'
                        ? 'Cả nhà đã sẵn sàng mở Movie Night!'
                        : 'Tiến tới Movie Night'}
                  </h2>
                  <div className="ph-movie-progress">
                    <i aria-hidden>
                      <b style={{ width: `${percent}%` }} />
                    </i>
                    <strong>{percent}%</strong>
                  </div>
                  <p className="ph-movie-left">
                    {movieLeft > 0
                      ? `Còn ${movieLeft} nhiệm vụ tới phần thưởng nhà`
                      : scopedTotal > 0 && scopedDone >= scopedTotal
                        ? 'Đã xong cam kết hôm nay!'
                        : 'Cùng hoàn thành nhiệm vụ nhé!'}
                  </p>
                  <button type="button" className="ph-movie-cta" onClick={() => scrollToMissions()}>
                    Xem nhiệm vụ →
                  </button>
                </div>
                <div className="ph-movie-art" aria-hidden>
                  <span className="ph-movie-tv">📺</span>
                  <span className="ph-movie-pop">🍿</span>
                  <span className="ph-movie-label">MOVIE NIGHT</span>
                </div>
              </article>

              <style>{`
                .ph-pulse-hero {
                  margin: 4px 0 14px;
                  padding: 18px 16px 16px;
                  border-radius: 20px;
                  background: #1a3344;
                  color: #f3f7fa;
                }
                .ph-pulse-eyebrow {
                  margin: 0 0 10px;
                  font-size: 11px;
                  letter-spacing: 0.07em;
                  text-transform: uppercase;
                  opacity: 0.72;
                }
                .ph-pulse-score-row {
                  display: flex;
                  gap: 14px;
                  align-items: flex-start;
                  margin-bottom: 12px;
                }
                .ph-pulse-score {
                  flex: 0 0 auto;
                  width: 72px;
                  height: 72px;
                  border-radius: 18px;
                  background: #243f52;
                  display: flex;
                  flex-direction: column;
                  align-items: center;
                  justify-content: center;
                }
                .ph-pulse-score-num {
                  font-size: 1.55rem;
                  font-weight: 750;
                  line-height: 1;
                }
                .ph-pulse-score-lbl {
                  margin-top: 4px;
                  font-size: 9px;
                  letter-spacing: 0.04em;
                  text-transform: uppercase;
                  opacity: 0.7;
                }
                .ph-pulse-copy h2 {
                  margin: 0 0 6px;
                  font-size: 1.15rem;
                  line-height: 1.35;
                  font-weight: 750;
                }
                .ph-pulse-mood {
                  margin: 0;
                  font-size: 0.9rem;
                  line-height: 1.4;
                  opacity: 0.88;
                }
                .ph-pulse-lines {
                  margin: 0 0 10px;
                  padding: 0 0 0 18px;
                  font-size: 0.9rem;
                  line-height: 1.45;
                }
                .ph-pulse-lines li { margin-bottom: 4px; }
                .ph-pulse-insight {
                  margin: 0 0 12px;
                  padding: 10px 12px;
                  border-radius: 12px;
                  background: #243f52;
                  font-size: 0.88rem;
                  line-height: 1.4;
                }
                .ph-pulse-actions {
                  display: flex;
                  flex-wrap: wrap;
                  gap: 12px;
                  align-items: center;
                }
                .ph-pulse-cta {
                  border: 0;
                  border-radius: 999px;
                  padding: 8px 14px;
                  background: #e8f3ea;
                  color: #1a3344;
                  font-weight: 700;
                  font-size: 0.9rem;
                }
                .ph-pulse-hero .ph-text-link { color: #c9e4ff; }
                .ph-pulse-note {
                  margin: 10px 0 0;
                  font-size: 0.8rem;
                  opacity: 0.65;
                }
                .ph-movie-hero--secondary {
                  opacity: 0.95;
                  transform: scale(0.99);
                  transform-origin: top center;
                }
                .ph-movie-hero--secondary h2 { font-size: 1.05rem; }
              `}</style>

              {familyScore ? (
                <p className="ph-family-score-line" role="status">
                  {familyScore.headlineVi}
                </p>
              ) : null}

              <div className="ph-afe-toolbar">
                <button
                  type="button"
                  className="ph-afe-mode-btn"
                  onClick={() => navigate('/family-admin')}
                >
                  Quản trị
                </button>
                <button
                  type="button"
                  className="ph-afe-mode-btn"
                  onClick={() => setModeSheetOpen(true)}
                >
                  Chế độ gia đình
                </button>
                {selectedChild || focusChild ? (
                  <button
                    type="button"
                    className="ph-afe-wallet-btn"
                    onClick={() => {
                      const childId = selectedChild?.key ?? focusChild?.key;
                      if (!childId || !parentMembershipId) {
                        showActionToast('Chọn con và đăng nhập phụ huynh trước.');
                        return;
                      }
                      void proposeScreenWallet(familyId, {
                        memberId: childId,
                        proposedByMemberId: parentMembershipId,
                      })
                        .then(() => {
                          setInboxTick((t) => t + 1);
                          showActionToast('AI đã đề xuất ngân sách tuần — duyệt trong hộp thư.');
                        })
                        .catch((err: unknown) => {
                          if (isCapabilityPaywallError(err)) {
                            openPaywall(getApiErrorMessage(err));
                            return;
                          }
                          showActionToast('Chưa đề xuất được ví tuần.');
                        });
                    }}
                  >
                    Đề xuất ví tuần
                  </button>
                ) : null}
              </div>

              <DecisionInboxPanel
                familyId={familyId}
                parentMembershipId={parentMembershipId}
                refreshKey={`${flow.flowDate}-${flow.doneCount}-${inboxTick}`}
                onApproveStars={async (commitmentId) => {
                  // Call API directly — do not swallow errors via verifyItem early-return.
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
              />

              {familyTwin ? (
                <section className="ph-block" id="ph-bos-detail">
                  <header className="ph-block-head">
                    <h2>AI RETIREMENT</h2>
                  </header>
                  <p className="ph-empty-soft" style={{ marginTop: 0 }}>
                    {familyTwin.disclaimerVi}
                  </p>
                  <p>
                    <strong>{familyTwin.retirementLabelVi}</strong>
                    {' · '}
                    Autonomy {familyTwin.familyAutonomyIndex} · Peace{' '}
                    {familyTwin.familyPeaceIndex} · Intervention{' '}
                    {familyTwin.parentalInterventionIndex}
                  </p>
                  <p className="ph-empty-soft">{familyTwin.retirementAdviceVi}</p>
                  <p className="ph-empty-soft">
                    Anh chị em: {familyTwin.siblingBalanceLabelVi} — {familyTwin.siblingAdviceVi}
                  </p>
                  {familyTwin.dependenceWarning && familyTwin.dependenceWarningVi ? (
                    <p role="alert" style={{ color: '#9a3412', fontSize: '0.9rem' }}>
                      {familyTwin.dependenceWarningVi}
                    </p>
                  ) : null}
                  <label
                    style={{
                      display: 'flex',
                      gap: 8,
                      alignItems: 'center',
                      marginTop: 10,
                      fontWeight: 650,
                      fontSize: '0.9rem',
                    }}
                  >
                    <input
                      type="checkbox"
                      checked={Boolean(familyTwin.observeOnlyActive)}
                      disabled={observeBusy}
                      onChange={(e) => {
                        const next = e.target.checked;
                        setObserveBusy(true);
                        void updateRetirementPolicy(familyId, { observeOnly: next })
                          .then(() => fetchFamilyBehaviorTwin(familyId))
                          .then((t) => {
                            setFamilyTwin(t);
                            onRefreshFlow?.();
                          })
                          .catch((err: unknown) => {
                            if (isCapabilityPaywallError(err)) {
                              openPaywall(getApiErrorMessage(err));
                            }
                          })
                          .finally(() => setObserveBusy(false));
                      }}
                    />
                    Observe-only (tắt nhắc bố mẹ)
                  </label>
                  {familyTwin.recommendObserveOnly && !familyTwin.observeOnlyActive ? (
                    <p className="ph-empty-soft">
                      Gợi ý: nhà đang sẵn sàng Observe-only — bật công tắc phía trên.
                    </p>
                  ) : null}
                </section>
              ) : twinBlocked ||
                (subscription != null &&
                  !hasCap('behavior_twin') &&
                  (subscription.tierCode === 'free' || !subscription.isEntitled)) ? (
                <section className="ph-block ph-cap-teaser">
                  <header className="ph-block-head">
                    <h2>Behavior Twin</h2>
                  </header>
                  <p className="ph-empty-soft">
                    Tín hiệu hành vi / AI Retirement có từ Plus (Growth Plan).
                  </p>
                  <button
                    type="button"
                    className="ph-pulse-cta"
                    onClick={() =>
                      openPaywall(
                        'Twin có từ Plus. Peace Plan thêm Coach/ROP — khuyến nghị 199.000đ.',
                      )
                    }
                  >
                    Xem gói nâng cấp
                  </button>
                </section>
              ) : null}

              {behaviorTwin && behaviorTwin.members.length > 0 ? (
                <section className="ph-block">
                  <header className="ph-block-head">
                    <h2>TÍN HIỆU HÀNH VI</h2>
                  </header>
                  <p className="ph-empty-soft" style={{ marginTop: 0 }}>
                    {behaviorTwin.disclaimerVi}
                  </p>
                  <div className="ph-support-grid">
                    {behaviorTwin.members
                      .filter((m) => {
                        if (effectiveChildFocus === 'all') return true;
                        return (
                          m.memberId === effectiveChildFocus ||
                          m.memberName === selectedChild?.name
                        );
                      })
                      .map((m) => (
                        <article key={m.memberId} className="ph-support-tile tone-lilac">
                          <strong>
                            {m.memberName} · {m.overallLabelVi} ({m.overallScore})
                          </strong>
                          <p>
                            Buổi tối:{' '}
                            {m.eveningRiskLabelVi
                              ? `rủi ro ${m.eveningRiskLabelVi.toLowerCase()}`
                              : 'ổn'}
                            {m.eveningSuggestedActionVi
                              ? ` — ${m.eveningSuggestedActionVi}`
                              : ''}
                          </p>
                          <ul style={{ margin: '8px 0 0', paddingLeft: 18, fontSize: '0.85rem' }}>
                            {m.dimensions.slice(0, 4).map((d) => (
                              <li key={d.code}>
                                {d.labelVi}: {d.score} — {d.whyVi}
                              </li>
                            ))}
                          </ul>
                        </article>
                      ))}
                  </div>
                </section>
              ) : null}

              <section className="ph-block">
                <header className="ph-block-head">
                  <h2>NHẮC NHẸ</h2>
                  <button
                    type="button"
                    className="ph-text-link"
                    onClick={() => scrollToMissions('need_help')}
                  >
                    Xem nhiệm vụ →
                  </button>
                </header>
                {supportCards.filter((c) => {
                  if (c.raw.kind !== 'overdue') return false;
                  return c.raw.item.allowParentPush !== false;
                }).length === 0 ? (
                  <p className="ph-empty-soft">
                    {supportCards.some((c) => {
                      if (c.raw.kind !== 'overdue') return false;
                      return c.raw.item.interventionLevel === 'observe_only';
                    })
                      ? 'Hôm nay nên quan sát — đừng nhắc thêm (thói quen đang tự chủ / hết ngân sách).'
                      : 'Không việc quá giờ cần nhắc.'}
                  </p>
                ) : (
                  <div className="ph-support-grid">
                    {supportCards
                      .filter((c) => {
                        if (c.raw.kind !== 'overdue') return false;
                        return c.raw.item.allowParentPush !== false;
                      })
                      .map((card) => (
                      <button
                        key={card.id}
                        type="button"
                        className={`ph-support-tile tone-${card.tone}`}
                        onClick={() => {
                          const a = card.raw;
                          if (a.kind !== 'overdue') return;
                          void (async () => {
                            try {
                              await shareOrCopyNudge(buildNudgeText(a.item));
                              const next = getNudgeCount(familyId, flow.flowDate) + 1;
                              setNudgeCountLocal(familyId, flow.flowDate, next);
                              onParentNudged(1);
                              try {
                                await incrementFamilyNudge(familyId, flow.flowDate, 1);
                              } catch {
                                /* offline OK */
                              }
                              showActionToast(
                                'Đã copy tin nhắc — dán Zalo/Messenger cho con',
                              );
                            } catch (err) {
                              if (err instanceof DOMException && err.name === 'AbortError') return;
                              setTab('tasks');
                              setMissionFilter('need_help');
                            }
                          })();
                        }}
                      >
                        <span className="ph-support-tile-ico" aria-hidden>
                          {card.icon}
                        </span>
                        <strong>{card.title}</strong>
                        <p>
                          {card.raw.kind === 'overdue' && card.raw.item.parentAdviceVi
                            ? card.raw.item.parentAdviceVi
                            : card.note}
                        </p>
                        <em>
                          <span aria-hidden>🕒</span> {card.deadline}
                        </em>
                        <span className="ph-support-chevron" aria-hidden>
                          ›
                        </span>
                      </button>
                    ))}
                  </div>
                )}
              </section>

              {childGratitudes.length > 0 ? (
                <section className="ph-block ph-gratitude-block">
                  <header className="ph-block-head">
                    <h2>
                      <span aria-hidden>💌</span> CON GỬI LỜI CẢM ƠN
                    </h2>
                  </header>
                  <div className="ph-gratitude-list">
                    {childGratitudes.map((g) => {
                      const sentAt = formatClock(g.createdAt);
                      return (
                        <article key={g.id} className="ph-gratitude-card">
                          <span className="ph-gratitude-heart" aria-hidden>
                            💖
                          </span>
                          <div>
                            <strong>{g.messageVi}</strong>
                            {g.praiseContext ? (
                              <p className="ph-gratitude-context">Vì: «{g.praiseContext}»</p>
                            ) : null}
                            <em>
                              {g.fromMemberName}
                              {sentAt ? ` · ${sentAt}` : ''}
                              {g.toMemberName ? ` → ${g.toMemberName}` : ''}
                            </em>
                          </div>
                        </article>
                      );
                    })}
                  </div>
                </section>
              ) : null}

              <section className="ph-foxy-strip">
                <span className="ph-foxy-strip-mascot" aria-hidden>
                  ✦
                </span>
                <p>
                  {resolvedCoach.isServerSot
                    ? coach.insight
                    : `Famixa nhận thấy ✨ ${foxyNotice}`}
                </p>
                <button
                  type="button"
                  className="ph-foxy-strip-btn"
                  onClick={openCoachOrPaywall}
                >
                  Famixa
                </button>
              </section>

              <section className="ph-block">
                <header className="ph-block-head">
                  <h2>GIA ĐÌNH TUẦN NÀY</h2>
                  <button type="button" className="ph-text-link" onClick={() => setTab('value')}>
                    Xem nhật ký →
                  </button>
                </header>
                <div className="ph-week-grid">
                  <article className="ph-week-tile">
                    <span aria-hidden>🔔</span>
                    <p className="ph-week-cap">Số lần phải nhắc</p>
                    <strong>
                      {nudgeToday} <em>lần</em>
                    </strong>
                    <b className={nudgeDeltaPct >= 0 ? 'is-up' : 'is-down'}>
                      {nudgeYesterday <= 0
                        ? 'Chưa có so sánh'
                        : nudgeDeltaPct >= 0
                          ? `Giảm ${nudgeDeltaPct}% ↓`
                          : `Tăng ${Math.abs(nudgeDeltaPct)}% ↑`}
                    </b>
                    <i className="ph-spark is-green" aria-hidden />
                  </article>
                  <article className="ph-week-tile">
                    <span aria-hidden>🎯</span>
                    <p className="ph-week-cap">Team Streak</p>
                    <strong>
                      {glance?.currentStreak ?? 0} <em>ngày</em>
                    </strong>
                    <div className="ph-streak-flames" aria-hidden>
                      {Array.from({ length: 7 }, (_, i) => (
                        <span
                          key={i}
                          className={
                            i < Math.min(Math.max(glance?.currentStreak ?? 0, 0), 7)
                              ? 'is-on'
                              : undefined
                          }
                        >
                          🔥
                        </span>
                      ))}
                    </div>
                    <em className="ph-week-note">
                      {(glance?.currentStreak ?? 0) > 0 ? 'Tuyệt vời!' : 'Bắt đầu từ hôm nay'}
                    </em>
                  </article>
                  <article className="ph-week-tile">
                    <span aria-hidden>📈</span>
                    <p className="ph-week-cap">Tỷ lệ tự giác</p>
                    <strong className="is-purple">
                      {autonomyGain != null
                        ? `${autonomyGain >= 0 ? '+' : ''}${autonomyGain}%`
                        : '—'}
                    </strong>
                    <em className="ph-week-note">
                      {autonomyGain != null ? 'So với tuần trước' : 'Cần thêm dữ liệu tuần'}
                    </em>
                    <i className="ph-spark is-purple" aria-hidden />
                  </article>
                  <article className="ph-week-tile">
                    <span aria-hidden>⭐</span>
                    <p className="ph-week-cap">Sao của {childShort}</p>
                    <strong>{rewardPoints.toLocaleString('vi-VN')}</strong>
                    <em className="ph-week-note is-star">{starBalanceNote(rewardPoints)}</em>
                  </article>
                </div>
              </section>

              <section className="ph-block ph-mood-today">
                <header className="ph-block-head">
                  <h2>TÂM TRẠNG HÔM NAY</h2>
                  {focusedChildMood && focusedMoodDisplay ? (
                    <span className="ph-mood-hint">{childShort} đã cảm thấy:</span>
                  ) : null}
                </header>
                {focusedChildMood && focusedMoodDisplay ? (
                  <>
                    <div className="ph-mood-picks" aria-label="Tâm trạng con">
                      <span className="ph-mood-pick is-on tone-green">
                        <span aria-hidden>{focusedMoodDisplay.emoji}</span>
                        <em>{focusedMoodDisplay.label}</em>
                      </span>
                    </div>
                    {focusedChildMood.note?.trim() ? (
                      <p className="ph-diary-mood-bubble">{focusedChildMood.note.trim()}</p>
                    ) : null}
                  </>
                ) : (
                  <p className="ph-mood-hint">Con chưa ghi tâm trạng hôm nay</p>
                )}
              </section>

              <section className="ph-block">
                <header className="ph-block-head">
                  <h2>KHOẢNH KHẮC ĐÁNG NHỚ</h2>
                  <button
                    type="button"
                    className="ph-text-link"
                    onClick={openMemoriesSheet}
                    disabled={memoryCards.length === 0}
                  >
                    Xem tất cả →
                  </button>
                </header>
                {memoryRecap && memoryRecap.totalCount > 0 ? (
                  <p className="ph-memory-recap">{memoryRecap.headlineVi}</p>
                ) : null}
                <div className="ph-memory-scroll">
                  {memoryCards.length === 0 ? (
                    <p className="ph-empty-soft">{FAMILY_MEMORY_EMPTY}</p>
                  ) : (
                    memoryCards.map((m) => (
                      <article key={m.id} className="ph-memory-card">
                        <div className="ph-memory-art">
                          {m.memory?.photoUrl ? (
                            <img
                              src={withEvidenceAuth(m.memory.photoUrl)}
                              alt=""
                              className="ph-memory-photo"
                            />
                          ) : (
                            <span aria-hidden>{m.icon}</span>
                          )}
                          {m.memory ? (
                            <button
                              type="button"
                              className={
                                m.memory.entry?.isFavorite
                                  ? 'ph-memory-heart is-on'
                                  : 'ph-memory-heart'
                              }
                              aria-label={
                                m.memory.entry?.isFavorite
                                  ? 'Bỏ thích'
                                  : m.memory.entry
                                    ? 'Thích'
                                    : 'Lưu vào kỷ niệm'
                              }
                              aria-pressed={m.memory.entry?.isFavorite ?? false}
                              disabled={memoryHeartBusy === m.memory.id}
                              onClick={() => void heartMemory(m.memory!)}
                            >
                              {m.memory.entry?.isFavorite ? '❤️' : '🤍'}
                            </button>
                          ) : null}
                        </div>
                        <strong>{m.title}</strong>
                        <em>{m.time}</em>
                      </article>
                    ))
                  )}
                </div>
              </section>
        </>
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
                setTab('value');
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
        <div className="ph-diary">
          {diaryToast ? (
            <div className="ph-diary-toast" role="status">
              {diaryToast}
            </div>
          ) : null}

          <div style={{ padding: '0 12px 8px' }}>
            <FamilyValuePanel
              familyId={familyId}
              familyName={familyName}
              flow={flow}
              glance={glance}
              nudgeToday={nudgeToday}
              momentCount={savedMemories.length + childGratitudes.length}
              onOpenPaywall={(reason) => openPaywall(reason)}
            />
          </div>

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
                Kho báu của {childShort} <span aria-hidden>✨</span>
              </h1>
              <p>Đang xem: {childFocusLabel}</p>
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

      <nav className="ph-tabbar" aria-label="Điều hướng bố mẹ">
        <button
          type="button"
          className={`ph-tab${tab === 'home' ? ' is-on' : ''}`}
          onClick={() => setTab('home')}
        >
          <span aria-hidden>🏠</span>
          Trang chủ
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'tasks' ? ' is-on' : ''}`}
          onClick={() => {
            setTab('tasks');
            setMissionFilter('all');
          }}
        >
          <span aria-hidden>✅</span>
          Nhiệm vụ
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
          className={`ph-tab${tab === 'rewards' ? ' is-on' : ''}`}
          onClick={() => setTab('rewards')}
        >
          <span aria-hidden>🧰</span>
          Kho báu
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'value' ? ' is-on' : ''}`}
          onClick={() => setTab('value')}
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
