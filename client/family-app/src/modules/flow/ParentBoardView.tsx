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
  type TeamNudgeCandidate,
  type TeamNudgeTemplate,
  confirmTeamUnlock,
  fetchTeamUnlocks,
  fetchTeamNudgeFromCandidates,
  createTeamNudge,
  sendTeamNudge,
  fetchCooperationScore,
  fetchFamilyRituals,
  checkinFamilyRitual,
  type CooperationScore,
  type FamilyRitual,
  fetchChildGratitude,
  markChildGratitudeRead,
  fetchRelationshipTriggers,
  fetchRelationshipTriggerStates,
  sendParentVoice,
  fetchParentVoice,
  ackParentVoice,
  type ParentVoiceMessage,
  fetchEveningCircle,
  answerEveningCircle,
  fetchWeeklyStory,
  fetchTeamNudges,
  type RelationshipTrigger,
  type EveningCircle,
  type WeeklyStory,
  approveCommitmentStars,
  verifyCommitmentEvidence,
  rejectCommitmentEvidence,
  EVIDENCE_REJECT_REASONS,
  evidenceRejectChildMessageVi,
  type EvidenceRejectReasonCode,
  fetchMemberStarBalance,
  fetchRewardCatalog,
  fetchRewardRedemptions,
  fulfillRewardRedemption,
  fetchFamilyMoods,
  upsertMemberMood,
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
  fetchWeekPlaybook,
  type WeekPlaybook,
  fetchFamilyCoachInsight,
  type FamilyCoachInsight,
  fetchBehaviorCoach,
  type BehaviorCoach,
  fetchFamilySubscription,
  type FamilySubscription,
  fetchFamilyDnaCard,
  hydrateFamilyBlueprint,
  type FamilyDnaCard,
  fetchParentSuccessEveningCheckin,
  type ParentSuccessCheckin,
  fetchParentCoachActedToday,
  recordParentCoachActed,
  patchFamilyBlueprintLayers,
  fetchFamilyBlueprint,
} from '@/shared/api/family-os.api';
import { DecisionInboxPanel } from '@/modules/flow/DecisionInboxPanel';
import { TodayOpenStack, type TodayOpenCtaEvent } from '@/modules/flow/TodayOpenStack';
import {
  buildMemoryYarn,
  buildPendingActions,
  buildSeenSignals,
  buildWarmthPulse,
  dismissWarmth,
  dismissYarn,
  isRitualDone,
  markRitualDone as markTodayOpenRitualDone,
} from '@/modules/flow/todayOpenSequence';
import { FamilyDnaCardView } from '@/modules/flow/FamilyDnaCard';
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
import {
  canRemindChildNow,
  remindChildIdleLabel,
} from '@/shared/reminders/remind-window';
import { ScreenBoundaryPanel } from '@/shared/ui/ScreenBoundaryPanel';
import {
  avatarEmoji,
  inferGenderFromName,
} from '@/shared/ui/avatarGender';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';
import { buildParentPulse } from '@/shared/value/parent-pulse';
import { resolveParentCoach } from '@/shared/value/resolve-parenting-coach';
import { buildHomeBrief } from '@/shared/value/home-brief';
import { becauseFromDna } from '@/shared/value/blueprint-context';
import {
  becauseFromSoftPrefs,
  hasSoftCalAnsweredThisWeek,
  markSoftCalAnsweredThisWeek,
  parseLayersJson,
  softCalAnswerPatch,
  softCalQuestionForWeek,
  type SoftCalQuestion,
} from '@/shared/value/soft-calibration';
import {
  IconBell,
  IconDiary,
  IconHome,
  IconPlus,
  IconReport,
  IconRobot,
  IconSettings,
  IconStar,
  IconTarget,
  IconTasks,
  IconTrophy,
} from '@/shared/ui/ParentNavIcons';
import {
  buildHomeFamilyFeed,
  pickMemoryWinVi,
} from '@/shared/value/home-family-feed';
import {
  FamilyValuePanel,
  FV_DETAIL_TITLES,
  type FvView,
} from '@/modules/flow/FamilyValuePanel';
import { BillingBanner } from '@/shared/ui/BillingBanner';
import { PaywallSheet } from '@/shared/ui/PaywallSheet';
import {
  getApiErrorMessage,
  isCapabilityPaywallError,
} from '@/shared/billing/capability-error';
import {
  buildFamilyMemories,
  isMovieNightUnlock,
  FAMILY_MEMORY_EMPTY,
  FAMILY_MEMORY_VISIBLE,
  type FamilyMemory,
} from '@/shared/flow/family-memories';
import {
  capitalizeParentRole,
  diaryDaySummaryLine,
  diaryTaskNote,
  familyProgressLine,
  parentRoleFromName,
  parentSupportLabel,
  warmTaskSupportNote,
  warmTaskTip,
  voicePick,
} from '@/shared/voice/family-voice';
import {
  NUDGE_TEMPLATE_OPTIONS,
  familyTeamHeroLine,
  isSiblingComboUnlock,
  nudgeMessagePreview,
  roleMatrixBriefTip,
} from '@/modules/flow/teamPlay';
import {
  filterVisibleParentTriggers,
  hydrateRelTriggerStates,
  isAdultVoiceTrigger,
  isBirthdayWishTrigger,
  isParentVoiceTrigger,
  isUnsentOpenedTrigger,
  markRelTriggerDismissed,
  markRelTriggerOpened,
  markRelTriggerSent,
  parentVoiceIcon,
  parentVoiceKindLabelVi,
  primaryRelationshipTrigger,
} from '@/modules/flow/memberPersonalize';
import {
  ADULT_VOICE_TEMPLATES,
  defaultChildVoiceDraftVi,
  BIRTHDAY_PICKER_OPTIONS,
  formatWeeklyStoryShare,
  shortMemberName,
} from '@/modules/flow/relationshipGraph';
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
type ParentTab = 'home' | 'tasks' | 'rewards' | 'value' | 'diary' | 'challenge';

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

function formatWindow(start?: string, end?: string): string | null {
  if (!start && !end) return null;
  const clean = (value?: string) => (value ? value.slice(0, 5) : '');
  if (start && end) return `${clean(start)} – ${clean(end)}`;
  return clean(start || end);
}

function taskCtaLabel(title: string, kind: 'overdue' | 'awaiting', flowDate: string): string {
  if (kind === 'awaiting') {
    return voicePick(`${flowDate}:cta:await:${title}`, [
      'Xác nhận cam kết',
      'Xác nhận',
      'Duyệt sao',
    ]);
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
      item.commitmentKind === 'study_focus' &&
      item.evidenceSatisfied === false &&
      !item.starPosted
    ) {
      return 'awaiting_check';
    }
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
  /** Other guardians/caregivers — P1.9 adult voice. */
  parents?: Array<{ id: string; displayName: string }>;
  /** House team-day snapshot (API or client derive) — hero never follows child picker. */
  teamDay?: {
    teamPercent: number;
    remainingMissions: number;
    teamComplete: boolean;
    teamTotal: number;
    heroMissionLine: string;
  } | null;
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
  parents: parentsProp = [],
  teamDay = null,
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
        raw === 'diary' ||
        raw === 'challenge'
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
  const [valueView, setValueView] = useState<FvView>('hub');
  const [verifiedTick, setVerifiedTick] = useState(0);
  const [nudgeTick, setNudgeTick] = useState(0);
  const [childFocus, setChildFocus] = useState<string>('');
  const [childMenuOpen, setChildMenuOpen] = useState(false);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [unlockBusy, setUnlockBusy] = useState(false);
  const [unlockMsg, setUnlockMsg] = useState<string | null>(null);
  const [waitingOpen, setWaitingOpen] = useState(true);
  const [treasureToast, setTreasureToast] = useState<string | null>(null);
  const [diaryToast, setDiaryToast] = useState<string | null>(null);
  const [actionToast, setActionToast] = useState<string | null>(null);
  const [modeSheetOpen, setModeSheetOpen] = useState(false);
  const [familyScore, setFamilyScore] = useState<FamilyScore | null>(null);
  const [familyTwin, setFamilyTwin] = useState<FamilyBehaviorTwin | null>(null);
  const [weekPlaybook, setWeekPlaybook] = useState<WeekPlaybook | null>(null);
  const [coachInsight, setCoachInsight] = useState<FamilyCoachInsight | null>(null);
  const [behaviorCoach, setBehaviorCoach] = useState<BehaviorCoach | null>(null);
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [dnaCard, setDnaCard] = useState<FamilyDnaCard | null>(null);
  const [dnaLoading, setDnaLoading] = useState(true);
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
  const [inboxAllOpen, setInboxAllOpen] = useState(false);
  const [siblingNudgeOpen, setSiblingNudgeOpen] = useState(false);
  const [nudgeCandidates, setNudgeCandidates] = useState<TeamNudgeCandidate[]>([]);
  const [nudgeFromId, setNudgeFromId] = useState('');
  const [nudgeToId, setNudgeToId] = useState('');
  const [nudgeTemplate, setNudgeTemplate] = useState<TeamNudgeTemplate>('cheer_up');
  const [nudgeBusy, setNudgeBusy] = useState(false);
  const [nudgeError, setNudgeError] = useState<string | null>(null);
  const [nudgeToast, setNudgeToast] = useState<string | null>(null);
  const [relTriggers, setRelTriggers] = useState<RelationshipTrigger[]>([]);
  const [relTriggerReload, setRelTriggerReload] = useState(0);
  const [voiceSheetOpen, setVoiceSheetOpen] = useState(false);
  /** True only when sheet opened from an RE trigger card (not free compose). */
  const [voiceSheetFromTrigger, setVoiceSheetFromTrigger] = useState(false);
  const [voiceDraft, setVoiceDraft] = useState('');
  const [voiceToId, setVoiceToId] = useState('');
  const [voiceTemplate, setVoiceTemplate] = useState('praise');
  const [voiceBusy, setVoiceBusy] = useState(false);
  const [voiceError, setVoiceError] = useState<string | null>(null);
  const [voiceToast, setVoiceToast] = useState<string | null>(null);
  const [voiceTargetKind, setVoiceTargetKind] = useState<'child' | 'adult'>('child');
  /** Outbound parent→child voices that kids thanked — close the loop on parent home. */
  const [voiceThanksReceipts, setVoiceThanksReceipts] = useState<ParentVoiceMessage[]>([]);
  const [dismissedVoiceThanksIds, setDismissedVoiceThanksIds] = useState<string[]>([]);
  const [birthdayPick, setBirthdayPick] = useState<string | null>(null);
  const [partnerInbox, setPartnerInbox] = useState<ParentVoiceMessage[]>([]);
  const [partnerAckBusy, setPartnerAckBusy] = useState<string | null>(null);
  const [diaryMemberFilter, setDiaryMemberFilter] = useState<string>('all');
  const [relUiTick, setRelUiTick] = useState(0);
  const [eveningCircle, setEveningCircle] = useState<EveningCircle | null>(null);
  const [circleAnswer, setCircleAnswer] = useState('');
  const [circleBusy, setCircleBusy] = useState(false);
  const [weeklyStory, setWeeklyStory] = useState<WeeklyStory | null>(null);
  const [weekReviewOpen, setWeekReviewOpen] = useState(false);
  const [weekReviewLoading, setWeekReviewLoading] = useState(false);
  const [weekReviewError, setWeekReviewError] = useState<string | null>(null);
  const [weekReviewMoments, setWeekReviewMoments] = useState<
    Array<{
      id: string;
      icon: string;
      kindLabel: string;
      titleVi: string;
      bodyVi?: string;
      at: string;
    }>
  >([]);
  const [softCalDismissed, setSoftCalDismissed] = useState(false);
  const [softCalBusy, setSoftCalBusy] = useState(false);
  const [softCalToast, setSoftCalToast] = useState<string | null>(null);
  const [softLayers, setSoftLayers] = useState<Record<string, unknown>>({});
  const softCalQuestion = useMemo(() => softCalQuestionForWeek(), []);
  const nudgePreview = useMemo(() => {
    const shortOf = (id: string, fallback: string) => {
      const name = nudgeCandidates.find((c) => c.memberId === id)?.displayName?.trim();
      if (!name) return fallback;
      const parts = name.split(' ').filter(Boolean);
      return parts.length ? parts[parts.length - 1] : name;
    };
    return nudgeMessagePreview(
      nudgeTemplate,
      shortOf(nudgeFromId, 'anh/chị'),
      shortOf(nudgeToId, 'em'),
    );
  }, [nudgeCandidates, nudgeFromId, nudgeToId, nudgeTemplate]);
  const [coopScore, setCoopScore] = useState<CooperationScore | null>(null);
  const [rituals, setRituals] = useState<FamilyRitual[]>([]);
  const [ritualBusy, setRitualBusy] = useState<string | null>(null);
  const [verifyingId, setVerifyingId] = useState<string | null>(null);
  /** Preview nội dung trước khi xác nhận cam kết / duyệt sao. */
  const [verifyPreview, setVerifyPreview] = useState<DayFlowCommitment | null>(null);
  const [openSeqTick, setOpenSeqTick] = useState(0);
  const [openRitualBusy, setOpenRitualBusy] = useState(false);
  const [verifyListOpen, setVerifyListOpen] = useState(false);
  const [verifyCheckTodays, setVerifyCheckTodays] = useState(false);
  const [verifyCheckWindow, setVerifyCheckWindow] = useState(false);
  const [verifyCheckMatch, setVerifyCheckMatch] = useState(false);
  const [verifyOverrideDuration, setVerifyOverrideDuration] = useState(false);
  const [verifyRejectReason, setVerifyRejectReason] = useState<EvidenceRejectReasonCode | ''>('');
  const [rejectingId, setRejectingId] = useState<string | null>(null);
  const [verifyError, setVerifyError] = useState<string | null>(null);
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
    if (!nudgeToast) return;
    const t = window.setTimeout(() => setNudgeToast(null), 3200);
    return () => window.clearTimeout(t);
  }, [nudgeToast]);

  useEffect(() => {
    if (!voiceToast) return;
    const t = window.setTimeout(() => setVoiceToast(null), 3200);
    return () => window.clearTimeout(t);
  }, [voiceToast]);

  useEffect(() => {
    if (!softCalToast) return;
    const t = window.setTimeout(() => setSoftCalToast(null), 3200);
    return () => window.clearTimeout(t);
  }, [softCalToast]);

  useEffect(() => {
    if (!familyId || !parentMembershipId) {
      setSoftLayers({});
      return;
    }
    let cancelled = false;
    void fetchFamilyBlueprint(familyId)
      .then((bp) => {
        if (!cancelled) setSoftLayers(parseLayersJson(bp?.layersJson));
      })
      .catch(() => {
        if (!cancelled) setSoftLayers({});
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, parentMembershipId]);

  useEffect(() => {
    if (!familyId || !parentMembershipId) {
      setRelTriggers([]);
      return;
    }
    let cancelled = false;
    void (async () => {
      try {
        const [rows, states] = await Promise.all([
          fetchRelationshipTriggers(familyId, parentMembershipId, flow.flowDate),
          fetchRelationshipTriggerStates(
            familyId,
            parentMembershipId,
            flow.flowDate,
          ).catch(() => [] as Awaited<ReturnType<typeof fetchRelationshipTriggerStates>>),
        ]);
        if (cancelled) return;
        hydrateRelTriggerStates(
          familyId,
          parentMembershipId,
          flow.flowDate,
          states,
        );
        setRelTriggers(rows);
        setRelUiTick((n) => n + 1);
      } catch {
        if (!cancelled) setRelTriggers([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [
    familyId,
    parentMembershipId,
    flow.flowDate,
    flow.doneCount,
    flow.pendingCount,
    relTriggerReload,
  ]);

  useEffect(() => {
    if (!familyId) {
      setEveningCircle(null);
      setWeeklyStory(null);
      return;
    }
    let cancelled = false;
    void fetchEveningCircle(familyId, {
      forMemberId: parentMembershipId,
      flowDate: flow.flowDate,
    })
      .then((row) => {
        if (!cancelled) setEveningCircle(row);
      })
      .catch(() => {
        if (!cancelled) setEveningCircle(null);
      });
    void fetchWeeklyStory(familyId, flow.flowDate)
      .then((row) => {
        if (!cancelled) setWeeklyStory(row);
      })
      .catch(() => {
        if (!cancelled) setWeeklyStory(null);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, parentMembershipId, flow.flowDate, flow.doneCount, relTriggerReload]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void fetchCooperationScore(familyId, 'week')
      .then((row) => {
        if (!cancelled) setCoopScore(row);
      })
      .catch(() => {
        if (!cancelled) setCoopScore(null);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate, flow.doneCount, flow.pendingCount]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void fetchFamilyRituals(familyId, flow.flowDate)
      .then((rows) => {
        if (!cancelled) setRituals(rows);
      })
      .catch(() => {
        if (!cancelled) setRituals([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate]);

  const markRitualDone = async (code: string) => {
    setRitualBusy(code);
    try {
      const row = await checkinFamilyRitual(familyId, {
        ritualCode: code,
        notedBy: parentMembershipId,
      });
      setRituals((prev) => prev.map((r) => (r.code === code ? row : r)));
    } catch {
      // keep checklist
    } finally {
      setRitualBusy(null);
    }
  };

  const submitEveningCircle = async () => {
    if (!parentMembershipId || !circleAnswer.trim() || circleBusy) return;
    setCircleBusy(true);
    try {
      const row = await answerEveningCircle(familyId, {
        memberId: parentMembershipId,
        answerVi: circleAnswer.trim(),
        flowDate: flow.flowDate,
      });
      setEveningCircle(row);
      setCircleAnswer('');
      setRelTriggerReload((n) => n + 1);
    } catch {
      // keep
    } finally {
      setCircleBusy(false);
    }
  };

  const openSiblingNudgeSheet = async () => {
    setNudgeError(null);
    setSiblingNudgeOpen(true);
    try {
      const rows = await fetchTeamNudgeFromCandidates(familyId, flow.flowDate);
      setNudgeCandidates(rows);
      const inviters = rows.filter((r) => r.canInvite);
      const fromId = inviters[0]?.memberId ?? '';
      setNudgeFromId(fromId);
      const toId =
        rows.find((r) => r.memberId !== fromId && !r.missionsComplete)?.memberId ??
        rows.find((r) => r.memberId !== fromId)?.memberId ??
        '';
      setNudgeToId(toId);
      setNudgeTemplate(houseTeamRemaining === 1 ? 'one_left' : 'cheer_up');
    } catch {
      setNudgeCandidates([]);
      setNudgeError('Chưa tải được danh sách anh/chị em.');
    }
  };

  const submitSiblingNudge = async () => {
    if (!nudgeFromId || !nudgeToId) {
      setNudgeError('Chọn anh/chị gửi và em nhận.');
      return;
    }
    setNudgeBusy(true);
    setNudgeError(null);
    try {
      const draft = await createTeamNudge(familyId, {
        fromMemberId: nudgeFromId,
        toMemberId: nudgeToId,
        templateCode: nudgeTemplate,
        flowDate: flow.flowDate,
      });
      await sendTeamNudge(familyId, draft.id);
      setSiblingNudgeOpen(false);
      setNudgeToast('Đã gửi lời nhắc anh/chị em.');
    } catch (e) {
      setNudgeError(getApiErrorMessage(e) || 'Không gửi được lời nhắc.');
    } finally {
      setNudgeBusy(false);
    }
  };

  const activeRelTrigger = useMemo(() => {
    if (!parentMembershipId) return null;
    const visible = filterVisibleParentTriggers(
      familyId,
      parentMembershipId,
      flow.flowDate,
      relTriggers,
    );
    if (effectiveChildFocus !== 'all') {
      // Don't surface another child's / partner trigger while focused on one child.
      return visible.find((t) => t.toMemberId === effectiveChildFocus) ?? null;
    }
    return primaryRelationshipTrigger(visible);
  }, [relTriggers, effectiveChildFocus, familyId, parentMembershipId, flow.flowDate, relUiTick]);

  const voiceUnsent = Boolean(
    parentMembershipId &&
      activeRelTrigger &&
      isUnsentOpenedTrigger(
        familyId,
        parentMembershipId,
        flow.flowDate,
        activeRelTrigger,
      ),
  );

  /**
   * @param trigger — gợi ý RE đang mở (CTA trigger).
   * @param compose — soạn tự do: luôn mở đúng đích, KHÔNG kế thừa activeRelTrigger
   *   (trước đây `openParentVoiceSheet(null)` vẫn fallback trigger adult → dropdown chỉ còn mẹ).
   */
  const openParentWeekReview = async () => {
    if (!weeklyStory) return;
    setWeekReviewOpen(true);
    setWeekReviewLoading(true);
    setWeekReviewError(null);
    const from = weeklyStory.from;
    const to = weeklyStory.to;
    const inWeek = (d?: string) => {
      const day = (d ?? '').slice(0, 10);
      if (!day) return false;
      return day >= from && day <= to;
    };
    try {
      const [voices, nudges, memories] = await Promise.all([
        fetchParentVoice(familyId),
        fetchTeamNudges(familyId),
        fetchFamilyMemories(familyId, { from, to, limit: 100 }).catch(
          () => [] as FamilyMemoryEntry[],
        ),
      ]);
      const moments: Array<{
        id: string;
        icon: string;
        kindLabel: string;
        titleVi: string;
        bodyVi?: string;
        at: string;
      }> = [];

      for (const v of voices) {
        if (!inWeek(v.flowDate) && !inWeek(v.sentAt)) continue;
        const toName = shortMemberName(v.toMemberName || 'con');
        moments.push({
          id: `voice-${v.id}`,
          icon: parentVoiceIcon(v.templateCode),
          kindLabel: parentVoiceKindLabelVi(v.templateCode),
          titleVi: `${shortMemberName(v.fromMemberName || 'Bố/mẹ')} → ${toName}`,
          bodyVi: v.bodyVi,
          at: v.sentAt || v.flowDate,
        });
      }

      for (const n of nudges) {
        if (!inWeek(n.flowDate) && !inWeek(n.sentAt) && !inWeek(n.createdAt)) continue;
        if (n.status === 'draft' || n.status === 'deferred') continue;
        const isThanks = n.templateCode === 'thanks_back';
        moments.push({
          id: `nudge-${n.id}`,
          icon: isThanks ? '💌' : '💛',
          kindLabel: isThanks ? 'Cảm ơn anh chị' : 'Cổ vũ anh chị',
          titleVi: `${shortMemberName(n.fromName)} → ${shortMemberName(n.toName)}`,
          bodyVi: n.messageVi,
          at: n.sentAt || n.createdAt || n.flowDate,
        });
      }

      for (const m of memories) {
        if (
          m.kind !== 'gratitude' &&
          m.kind !== 'evening_circle' &&
          m.kind !== 'parent_voice' &&
          m.kind !== 'help'
        ) {
          continue;
        }
        // Prefer live voice/nudge rows when body already listed.
        if (m.kind === 'parent_voice' || m.kind === 'help') continue;
        moments.push({
          id: `mem-${m.id}`,
          icon: m.icon || (m.kind === 'gratitude' ? '💖' : '⭐'),
          kindLabel: m.kind === 'gratitude' ? 'Cảm ơn bố/mẹ' : 'Evening Circle',
          titleVi: m.titleVi,
          bodyVi: m.noteVi,
          at: m.happenedAt || m.flowDate,
        });
      }

      moments.sort((a, b) => b.at.localeCompare(a.at));
      setWeekReviewMoments(moments);
    } catch {
      setWeekReviewError('Chưa tải được tuần này — thử lại nhé.');
      setWeekReviewMoments([]);
    } finally {
      setWeekReviewLoading(false);
    }
  };

  const openParentVoiceSheet = (
    trigger?: RelationshipTrigger | null,
    compose?: 'child' | 'adult',
  ) => {
    if (!parentMembershipId) return;
    const t = compose ? null : (trigger ?? activeRelTrigger);
    setVoiceError(null);
    setBirthdayPick(null);
    if (t && isParentVoiceTrigger(t.code)) {
      setVoiceSheetFromTrigger(true);
      const adult = isAdultVoiceTrigger(t.code);
      setVoiceTargetKind(adult ? 'adult' : 'child');
      setVoiceToId(t.toMemberId ?? '');
      setVoiceTemplate(
        t.templateCode ??
          (adult ? 'thanks_partner' : isBirthdayWishTrigger(t.code) ? 'birthday' : 'praise'),
      );
      const draft = t.draftBodyVi ?? '';
      setVoiceDraft(draft);
      markRelTriggerOpened(familyId, parentMembershipId, flow.flowDate, t, draft);
      setRelUiTick((n) => n + 1);
    } else if (compose === 'adult') {
      setVoiceSheetFromTrigger(false);
      setVoiceTargetKind('adult');
      const firstAdult = adultOptions[0];
      setVoiceToId(firstAdult?.id ?? '');
      setVoiceTemplate('thanks_partner');
      const short = shortMemberName(firstAdult?.displayName || 'bạn');
      setVoiceDraft(`${short} ơi, cảm ơn hôm nay mình cùng giữ nhà nhé.`);
    } else {
      setVoiceSheetFromTrigger(false);
      setVoiceTargetKind('child');
      const firstChild =
        effectiveChildFocus !== 'all'
          ? childOptions.find((c) => c.key === effectiveChildFocus)
          : childOptions[0];
      setVoiceToId(firstChild?.key ?? '');
      setVoiceTemplate('custom');
      const short =
        firstChild?.name?.trim().split(/\s+/).filter(Boolean).slice(-1)[0] || 'con';
      const roleWord = parentRoleFromName(viewerName);
      const childCommitments = scopedCommitments.filter(
        (c) => !firstChild?.key || c.memberId === firstChild.key,
      );
      const childDone = childCommitments.filter((c) => c.status === 'done').length;
      setVoiceDraft(
        defaultChildVoiceDraftVi({
          childShort: short,
          parentRole: roleWord,
          doneCount: childDone,
          totalCount: childCommitments.length,
          streak,
          teamComplete: Boolean(teamDay?.teamComplete),
        }),
      );
    }
    setVoiceSheetOpen(true);
  };

  const dismissParentVoiceTrigger = () => {
    if (!parentMembershipId || !activeRelTrigger) return;
    markRelTriggerDismissed(
      familyId,
      parentMembershipId,
      flow.flowDate,
      activeRelTrigger,
    );
    setRelUiTick((n) => n + 1);
  };

  const answerSoftCal = async (question: SoftCalQuestion, code: string) => {
    if (!familyId || softCalBusy) return;
    setSoftCalBusy(true);
    try {
      const bp = await patchFamilyBlueprintLayers(
        familyId,
        softCalAnswerPatch(question, code),
      );
      setSoftLayers(parseLayersJson(bp.layersJson));
      markSoftCalAnsweredThisWeek(familyId);
      setSoftCalDismissed(true);
      setSoftCalToast('Đã nhớ — Famixa sẽ gợi ý đúng nhà mình hơn.');
    } catch {
      setSoftCalToast('Chưa lưu được. Thử lại sau nhé.');
    } finally {
      setSoftCalBusy(false);
    }
  };

  const submitParentVoice = async () => {
    if (!parentMembershipId || !voiceToId || !voiceDraft.trim()) {
      setVoiceError(
        voiceTargetKind === 'adult'
          ? 'Chọn người nhận và nhập lời gửi.'
          : 'Chọn con và nhập lời gửi.',
      );
      return;
    }
    setVoiceBusy(true);
    setVoiceError(null);
    try {
      await sendParentVoice(familyId, {
        fromMemberId: parentMembershipId,
        toMemberId: voiceToId,
        templateCode: voiceTemplate || 'custom',
        bodyVi: voiceDraft.trim(),
        flowDate: flow.flowDate,
      });
      const picked = BIRTHDAY_PICKER_OPTIONS.find((o) => o.code === birthdayPick);
      if (picked?.treatTitleVi) {
        try {
          await createFamilyMemory(familyId, {
            titleVi: picked.treatTitleVi,
            flowDate: flow.flowDate,
            memberId: voiceToId,
            kind: 'reward',
            noteVi: voiceDraft.trim(),
            icon: '🎂',
          });
        } catch {
          /* optional treat memory */
        }
      }
      if (
        voiceSheetFromTrigger &&
        activeRelTrigger &&
        activeRelTrigger.toMemberId === voiceToId
      ) {
        markRelTriggerSent(
          familyId,
          parentMembershipId,
          flow.flowDate,
          activeRelTrigger,
        );
      }
      setVoiceSheetFromTrigger(false);
      setVoiceSheetOpen(false);
      setVoiceToast(
        voiceTargetKind === 'adult'
          ? 'Đã gửi lời tới người cùng chăm nhà.'
          : 'Đã gửi lời tới con — bạn là người nói.',
      );
      setRelTriggerReload((n) => n + 1);
      setRelUiTick((n) => n + 1);
    } catch (e) {
      setVoiceError(getApiErrorMessage(e) || 'Chưa gửi được. Thử lại nhé.');
    } finally {
      setVoiceBusy(false);
    }
  };

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void (async () => {
      try {
        const [rows, recap] = await Promise.all([
          fetchFamilyMemories(familyId, {
            limit: 40,
            memberId: diaryMemberFilter !== 'all' ? diaryMemberFilter : undefined,
          }),
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
  }, [familyId, flow.flowDate, flow.doneCount, diaryMemberFilter]);

  useEffect(() => {
    if (!familyId || !parentMembershipId) {
      setPartnerInbox([]);
      return;
    }
    let cancelled = false;
    void fetchParentVoice(familyId, {
      forMemberId: parentMembershipId,
      flowDate: flow.flowDate,
    })
      .then((rows) => {
        if (!cancelled) {
          setPartnerInbox(rows.filter((r) => r.status === 'sent'));
        }
      })
      .catch(() => {
        if (!cancelled) setPartnerInbox([]);
      });
    void fetchParentVoice(familyId, {
      fromMemberId: parentMembershipId,
      flowDate: flow.flowDate,
    })
      .then((rows) => {
        if (!cancelled) {
          setVoiceThanksReceipts(rows.filter((r) => r.status === 'thanks'));
        }
      })
      .catch(() => {
        if (!cancelled) setVoiceThanksReceipts([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, parentMembershipId, flow.flowDate, relTriggerReload]);

  const adultOptions = useMemo(
    () => parentsProp.filter((p) => p.id !== parentMembershipId),
    [parentsProp, parentMembershipId],
  );

  const ackPartnerVoice = async (messageId: string, status: 'read' | 'thanks') => {
    setPartnerAckBusy(messageId);
    try {
      await ackParentVoice(familyId, messageId, status);
      setPartnerInbox((prev) => prev.filter((n) => n.id !== messageId));
      setVoiceToast(
        status === 'thanks'
          ? 'Đã gửi cảm ơn người cùng chăm nhà.'
          : 'Đã xem lời từ người cùng chăm.',
      );
      setRelTriggerReload((n) => n + 1);
    } catch {
      /* keep */
    } finally {
      setPartnerAckBusy(null);
    }
  };

  const applyBirthdayPick = (code: string) => {
    const opt = BIRTHDAY_PICKER_OPTIONS.find((o) => o.code === code);
    if (!opt) return;
    setBirthdayPick(code);
    setVoiceTemplate('birthday');
    const childName =
      childOptions.find((c) => c.key === voiceToId)?.name ||
      activeRelTrigger?.toMemberName ||
      'con';
    const roleLabel = capitalizeParentRole(parentRoleFromName(viewerName));
    setVoiceDraft(opt.draftVi(shortMemberName(childName), roleLabel));
  };

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
    setDnaLoading(true);
    void (async () => {
      try {
        let d = await fetchFamilyDnaCard(familyId);
        if (!d.hasBlueprint) {
          try {
            await hydrateFamilyBlueprint(familyId);
            d = await fetchFamilyDnaCard(familyId);
          } catch {
            /* onboarding may be empty — keep empty DNA card */
          }
        }
        if (!cancelled) setDnaCard(d);
      } catch {
        if (!cancelled) setDnaCard(null);
      } finally {
        if (!cancelled) setDnaLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [familyId, flow.flowDate]);

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
    const playbookMemberId =
      effectiveChildFocus !== 'all' ? effectiveChildFocus : undefined;
    void fetchWeekPlaybook(familyId, {
      memberId: playbookMemberId,
      asOf: flow.flowDate,
    })
      .then((p) => {
        if (!cancelled) setWeekPlaybook(p);
      })
      .catch(() => {
        if (!cancelled) setWeekPlaybook(null);
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
  }, [
    familyId,
    flow.flowDate,
    flow.doneCount,
    parentMembershipId,
    effectiveChildFocus,
  ]);

  const todayUnlock = useMemo(() => {
    const sameDay = teamUnlocks.filter((u) => u.flowDate === flow.flowDate);
    const movie =
      sameDay.find((u) => !isSiblingComboUnlock(u.rewardCode)) ??
      teamUnlocks.find((u) => !isSiblingComboUnlock(u.rewardCode));
    return movie ?? null;
  }, [teamUnlocks, flow.flowDate]);

  const todayComboUnlock = useMemo(() => {
    const sameDay = teamUnlocks.filter(
      (u) => u.flowDate === flow.flowDate && isSiblingComboUnlock(u.rewardCode),
    );
    return (
      sameDay.find((u) => u.status === 'pending_confirm') ??
      sameDay[0] ??
      null
    );
  }, [teamUnlocks, flow.flowDate]);

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
  /** Movie Night progress is team unlock %, not today's per-child task %. */
  const moviePct = Math.max(
    0,
    Math.min(100, todayUnlock?.teamPercent ?? percent),
  );
  const movieMembersLeft = todayUnlock
    ? Math.max(0, todayUnlock.teamTotal - todayUnlock.teamDone)
    : Math.max(0, Math.min(3, Math.max(0, flow.totalCommitments - flow.doneCount)));
  const focusChild =
    effectiveChildFocus === 'all'
      ? [...allMembers].sort((a, b) => b.done - a.done)[0]
      : members[0] ?? allMembers.find((m) => m.key === effectiveChildFocus);
  const selectedChild =
    effectiveChildFocus === 'all'
      ? null
      : childOptions.find((c) => c.key === effectiveChildFocus) ?? null;
  const coachScope = useMemo(
    () =>
      effectiveChildFocus === 'all'
        ? ({ kind: 'family', labelVi: 'Cả nhà' } as const)
        : ({
            kind: 'child',
            labelVi: selectedChild?.name ?? 'Con',
            childName: selectedChild?.name ?? 'Con',
            childMemberId: selectedChild?.key,
          } as const),
    [effectiveChildFocus, selectedChild?.name, selectedChild?.key],
  );
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
        scope: coachScope,
        coachInsight,
        familyTwin,
        behaviorCoach,
        dna: dnaCard,
      }),
    [
      familyId,
      flow,
      scopedCommitments,
      glance,
      nudgeToday,
      coachScope,
      selectedChild?.name,
      coachInsight,
      familyTwin,
      behaviorCoach,
      dnaCard,
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
          whoVi:
            top.kind === 'consequence'
              ? top.event.memberName?.trim() || undefined
              : top.item.memberName?.trim() || undefined,
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
      overdueCount: attentionItems.filter((a) => a.kind === 'overdue').length,
      topAttention,
      localTime: flow.localTime,
      eveningCheckinDone: Boolean(eveningCheckin),
      memoryWinVi,
      dna: dnaCard,
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
    dnaCard,
  ]);

  /**
   * Việc ưu tiên luôn thuộc về đúng 1 người — tên chỉ hiện ở danh sách ưu tiên,
   * không gắn lên hero nhà (Team Play Đợt A).
   */
  // House hero uses team-day — never follows the "Đang xem" picker / lagging child name.
  const houseTeamPercent = teamDay?.teamPercent ?? percent;
  const houseTeamRemaining =
    teamDay?.remainingMissions ?? Math.max(0, scopedTotal - scopedDone);
  const houseTeamTotal = teamDay?.teamTotal ?? Math.max(scopedTotal, 0);
  const houseTeamSummary = familyTeamHeroLine(
    houseTeamPercent,
    houseTeamRemaining,
    houseTeamTotal,
  );
  const briefTaskTone =
    homeBrief.primaryAction.kind === 'dna_setup'
      ? 'dna'
      : homeBrief.primaryAction.attentionKind === 'awaiting'
        ? 'awaiting'
        : homeBrief.primaryAction.attentionKind === 'overdue'
          ? 'overdue'
          : homeBrief.primaryAction.attentionKind === 'consequence'
            ? 'decide'
            : 'tip';
  const briefTaskIcon =
    briefTaskTone === 'dna'
      ? '🧬'
      : briefTaskTone === 'awaiting'
        ? '⏳'
        : briefTaskTone === 'overdue'
          ? '⏰'
          : briefTaskTone === 'decide'
            ? '⚖️'
            : '💡';

  const homeAttention = useMemo(() => {
    if (homeBrief.period !== 'evening') return attentionItems.slice(0, 3);
    // Evening: keep awaiting/consequence; collapse overdue into one summary row in UI.
    const hot = attentionItems.filter((a) => a.kind !== 'overdue').slice(0, 2);
    return hot;
  }, [attentionItems, homeBrief.period]);

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
    if (subscription.status === 'trial_grace') {
      return subscription.trialGraceDaysRemaining ?? null;
    }
    if (subscription.trialDaysRemaining != null) return subscription.trialDaysRemaining;
    const iso = subscription.trialEndsAt || subscription.currentPeriodEnd;
    if (!iso) return null;
    const t = Date.parse(iso);
    if (Number.isNaN(t)) return null;
    return Math.max(0, Math.ceil((t - Date.now()) / (24 * 60 * 60 * 1000)));
  }, [subscription]);

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
  const awaitingVerifyItems = useMemo(() => buckets.waiting, [buckets.waiting]);
  const waitingChildItems = useMemo(() => buckets.upcoming, [buckets.upcoming]);
  const doneTodayItems = useMemo(() => buckets.done, [buckets.done]);

  const filteredMissions = useMemo(() => {
    if (missionFilter === 'done') return doneTodayItems;
    if (missionFilter === 'need_help') return needHelpItems;
    if (missionFilter === 'waiting_child') return waitingChildItems;
    return [...needHelpItems, ...waitingChildItems, ...doneTodayItems];
  }, [missionFilter, needHelpItems, waitingChildItems, doneTodayItems]);

  const verifyItem = async (item: DayFlowCommitment) => {
    if (verifyingId === item.id) return;
    if (busyId && busyId !== item.id) {
      throw new Error('approve_stars_busy');
    }
    setVerifyingId(item.id);
    setVerifyError(null);
    try {
      if (isOpen(item)) {
        await Promise.resolve(onMarkDone(item));
      }
      const needsEvidenceVerify =
        item.commitmentKind === 'study_focus' && item.evidenceSatisfied === false;
      if (needsEvidenceVerify) {
        if (!verifyCheckTodays || !verifyCheckWindow || !verifyCheckMatch) {
          setVerifyError('Cần tick đủ 3 mục xác nhận trước khi duyệt.');
          throw new Error('checklist_incomplete');
        }
        await verifyCommitmentEvidence(familyId, item.id, {
          isTodaysWork: verifyCheckTodays,
          withinCommitmentWindow: verifyCheckWindow,
          matchesCommitment: verifyCheckMatch,
          overrideDuration: verifyOverrideDuration,
        });
        onRefreshFlow?.();
      } else if (onApproveStars) {
        await onApproveStars(item);
      } else {
        await approveCommitmentStars(familyId, item.id);
        onRefreshFlow?.();
      }
      markParentVerified(flow.flowDate, item.id);
      setVerifiedTick((t) => t + 1);
      const next =
        awaitingVerifyItems.find((c) => c.id !== item.id) ?? null;
      if (next) {
        openVerifyPreview(next);
        showDiaryToast(
          needsEvidenceVerify
            ? `Đã xác nhận «${item.title}» · còn mục cần duyệt`
            : `Đã xác nhận «${item.title}» · còn mục cần duyệt`,
        );
      } else {
        setVerifyPreview(null);
        setVerifyListOpen(false);
        showDiaryToast(
          needsEvidenceVerify
            ? `Đã xác nhận cam kết «${item.title}»!`
            : `Đã xác nhận «${item.title}»!`,
        );
      }
    } catch (err) {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? String(
              (err as { response?: { data?: { message?: string } } }).response?.data
                ?.message ?? '',
            )
          : '';
      if (msg) setVerifyError(msg);
      else if (!verifyError) showDiaryToast('Chưa duyệt được sao — thử lại nhé.');
      throw err;
    } finally {
      setVerifyingId(null);
    }
  };

  const rejectEvidenceItem = async (item: DayFlowCommitment) => {
    if (!verifyRejectReason) {
      setVerifyError('Chọn lý do từ chối để con biết cần gửi lại gì.');
      return;
    }
    if (rejectingId === item.id || verifyingId === item.id) return;
    setRejectingId(item.id);
    setVerifyError(null);
    try {
      await rejectCommitmentEvidence(familyId, item.id, {
        reasonCode: verifyRejectReason,
      });
      if (parentMembershipId && item.memberId) {
        try {
          await sendParentVoice(familyId, {
            fromMemberId: parentMembershipId,
            toMemberId: item.memberId,
            templateCode: 'encourage',
            bodyVi: evidenceRejectChildMessageVi(verifyRejectReason, item.title),
            flowDate: flow.flowDate,
          });
        } catch {
          /* voice best-effort */
        }
      }
      onRefreshFlow?.();
      setInboxTick((n) => n + 1);
      const next = awaitingVerifyItems.find((c) => c.id !== item.id) ?? null;
      if (next) {
        openVerifyPreview(next);
        showDiaryToast(`Đã yêu cầu «${item.title}» gửi lại · còn mục cần duyệt`);
      } else {
        setVerifyPreview(null);
        setVerifyListOpen(false);
        showDiaryToast(`Đã yêu cầu con gửi lại bằng chứng «${item.title}»`);
      }
    } catch (err) {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? String(
              (err as { response?: { data?: { message?: string } } }).response?.data
                ?.message ?? '',
            )
          : '';
      setVerifyError(msg || 'Chưa từ chối được — thử lại nhé.');
    } finally {
      setRejectingId(null);
    }
  };

  const openVerifyPreview = (item: DayFlowCommitment) => {
    setVerifyListOpen(false);
    setVerifyCheckTodays(false);
    setVerifyCheckWindow(false);
    setVerifyCheckMatch(false);
    setVerifyOverrideDuration(false);
    setVerifyRejectReason('');
    setVerifyError(null);
    setVerifyPreview(item);
  };

  const scrollToMissions = (filter: MissionFilter = 'all') => {
    setTab('tasks');
    setMissionFilter(filter);
    window.requestAnimationFrame(() => {
      document.getElementById('ph-missions')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  };

  const openStackWarmth = useMemo(() => {
    void openSeqTick;
    return buildWarmthPulse({
      role: 'parent',
      flowDate: flow.flowDate,
      memberId: parentMembershipId || 'parent',
      voiceThanks: voiceThanksReceipts,
      memories: savedMemories,
      weeklyStory,
      familyName,
    });
  }, [
    openSeqTick,
    flow.flowDate,
    parentMembershipId,
    voiceThanksReceipts,
    savedMemories,
    weeklyStory,
    familyName,
  ]);

  const openStackPending = useMemo(
    () =>
      buildPendingActions({
        role: 'parent',
        commitments: scopedCommitments,
        partnerInbox,
      }),
    [scopedCommitments, partnerInbox],
  );

  const openStackSeen = useMemo(
    () =>
      buildSeenSignals({
        role: 'parent',
        voiceThanks: voiceThanksReceipts,
        awaitingCount: awaitingVerifyItems.length,
        commitments: scopedCommitments,
      }),
    [voiceThanksReceipts, awaitingVerifyItems.length, scopedCommitments],
  );

  const openStackYarn = useMemo(() => {
    void openSeqTick;
    return buildMemoryYarn({
      role: 'parent',
      flowDate: flow.flowDate,
      memberId: parentMembershipId || 'parent',
      commitments: scopedCommitments,
      memories: savedMemories,
    });
  }, [openSeqTick, flow.flowDate, parentMembershipId, scopedCommitments, savedMemories]);

  const openStackRitualDone = useMemo(() => {
    void openSeqTick;
    return isRitualDone(parentMembershipId || 'parent', flow.flowDate);
  }, [openSeqTick, parentMembershipId, flow.flowDate]);

  const handleOpenStackCta = (ev: TodayOpenCtaEvent) => {
    if (ev.kind === 'dismiss' || ev.kind === 'dismiss_thanks') {
      dismissWarmth(parentMembershipId || 'parent', flow.flowDate);
      if (ev.kind === 'dismiss_thanks' && ev.id) {
        setDismissedVoiceThanksIds((prev) => (prev.includes(ev.id!) ? prev : [...prev, ev.id!]));
      }
      setOpenSeqTick((n) => n + 1);
      return;
    }
    if (ev.kind === 'verify_evidence' || ev.kind === 'approve_stars') {
      const hit =
        (ev.id ? scopedCommitments.find((c) => c.id === ev.id) : null) ??
        awaitingVerifyItems[0] ??
        null;
      if (hit) openVerifyPreview(hit);
      else scrollToMissions('need_help');
      return;
    }
    if (ev.kind === 'open_voice') {
      setVoiceSheetOpen(true);
      return;
    }
    if (ev.kind === 'ack_partner_voice' && ev.id) {
      void ackParentVoice(familyId, ev.id, 'read').then(() => setInboxTick((n) => n + 1));
      return;
    }
    if (ev.kind === 'open_memory') {
      setTab('diary');
      return;
    }
    if (ev.kind === 'scroll_missions') {
      scrollToMissions('need_help');
    }
  };

  const handleOpenStackRitual = async (moodCode: string, warmLineVi: string) => {
    if (!parentMembershipId || openRitualBusy) return;
    setOpenRitualBusy(true);
    try {
      await upsertMemberMood(familyId, parentMembershipId, {
        flowDate: flow.flowDate,
        moodCode,
        note: warmLineVi,
      });
      const toChild =
        effectiveChildFocus !== 'all'
          ? childOptions.find((c) => c.key === effectiveChildFocus)
          : childOptions[0];
      if (toChild?.key) {
        await sendParentVoice(familyId, {
          fromMemberId: parentMembershipId,
          toMemberId: toChild.key,
          templateCode: 'praise',
          bodyVi: warmLineVi,
          flowDate: flow.flowDate,
        });
      }
      markTodayOpenRitualDone(parentMembershipId, flow.flowDate);
      setOpenSeqTick((n) => n + 1);
      setActionToast('Đã gửi ấm cho nhà 💛');
    } catch {
      setActionToast('Chưa gửi được — thử lại nhé');
    } finally {
      setOpenRitualBusy(false);
    }
  };

  /** Rời board sang trang khác: nhớ tab đang xem để khi bấm "Quay lại" không rơi về Home. */
  const leaveBoard = (to: string) => {
    try {
      sessionStorage.setItem('famixa.parentTab', tab);
    } catch {
      /* ignore */
    }
    navigate(to);
  };

  const openSettings = () => {
    leaveBoard('/family-admin/settings');
  };

  const openAccountSettings = (hash?: string) => {
    leaveBoard(hash ? `/family-admin/settings#${hash}` : '/family-admin/settings');
  };

  /** Nhà chưa có thành viên con → không giả định "Con" trong copy. */
  const hasChildren = childOptions.length > 0;
  const showSoftCal =
    Boolean(familyId) &&
    Boolean(parentMembershipId) &&
    hasChildren &&
    !softCalDismissed &&
    !hasSoftCalAnsweredThisWeek(familyId);
  const childShort =
    (selectedChild?.name ?? focusChild?.name ?? 'Con').trim().split(/\s+/).pop() || 'Con';
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
        const noteStatus = done
          ? 'done'
          : wait
            ? 'awaiting'
            : skipped
              ? 'skipped'
              : 'pending';
        return {
          item,
          part: diaryDayPart(item),
          time: diaryTimeLabel(item),
          done,
          wait,
          skipped,
          pending: !done && !wait && !skipped,
          who: item.memberName?.trim() || (hasChildren ? childShort : ''),
          note: diaryTaskNote(
            item.title,
            item.memberName?.trim() || childShort,
            noteStatus,
            parentRole,
          ),
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
  }, [
    scopedCommitments,
    flow.flowDate,
    verifiedTick,
    childShort,
    hasChildren,
    diaryFilter,
    parentRole,
  ]);

  const diaryVisible = diaryExpanded ? diaryEntries : diaryEntries.slice(0, 5);
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
  const diarySummary = useMemo(
    () => diaryDaySummaryLine(childShort, scopedDone, Math.max(scopedTotal, 0)),
    [childShort, scopedDone, scopedTotal],
  );
  const studyEvidenceLine = useMemo(() => {
    const study = scopedCommitments.filter((c) => c.commitmentKind === 'study_focus');
    if (study.length === 0) return null;
    const done = study.filter((c) => c.status === 'done');
    const withEv = done.filter((c) => c.evidenceSatisfied);
    return {
      total: study.length,
      done: done.length,
      withEvidence: withEv.length,
      waiting: done.length - withEv.length + study.filter((c) => c.status !== 'done').length,
    };
  }, [scopedCommitments]);

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

  /**
   * Sao / quà luôn thuộc về đúng 1 con (`treasureMemberId`). Khi đang xem "Cả nhà"
   * con này có thể khác con trong Brief — nên nhãn Kho báu phải bám treasureMemberId.
   */
  const treasureChildName =
    childOptions.find((c) => c.key === treasureMemberId)?.name ?? '';
  const treasureShort =
    treasureChildName.trim().split(/\s+/).pop() || childShort;

  const renderNoChildNotice = () => (
    <section className="ph-nochild" role="status">
      <span className="ph-nochild-art" aria-hidden>
        👶
      </span>
      <div className="ph-nochild-copy">
        <strong>Nhà mình chưa thêm con</strong>
        <p>Thêm con để Famixa giao việc, ghi nhật ký và tính sao thưởng.</p>
      </div>
      <button
        type="button"
        className="ph-nochild-cta"
        onClick={() => leaveBoard('/family-admin/members')}
      >
        Thêm con
      </button>
    </section>
  );

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
        childShort: treasureShort,
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
    [childRedemptions, teamUnlocks, scopedCommitments, treasureShort, savedMemories],
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
          pct: moviePct,
          locked: false,
        },
      ];
    }
    return [];
  }, [teamUnlocks, flow.flowDate, moviePct, todayUnlock]);

  /** Only earned — hide locked goals from treasure strip. */
  const bigAchievements = useMemo(() => {
    const movieTimes = teamUnlocks.filter(
      (u) => u.status === 'confirmed' && isMovieNightUnlock(u),
    ).length;
    const readTimes = scopedCommitments.filter(
      (c) => c.status === 'done' && /đọc|sách/i.test(c.title),
    ).length;
    const rows: Array<{ id: string; icon: string; title: string; value: string }> =
      [];
    if (movieTimes > 0) {
      rows.push({
        id: 'a1',
        icon: '🎬',
        title: 'Movie Night',
        value: `${movieTimes} lần`,
      });
    }
    if (readTimes > 0) {
      rows.push({
        id: 'a2',
        icon: '📘',
        title: `Đọc sách cùng ${parentRole}`,
        value: `${readTimes} lần`,
      });
    }
    if (explorerLevel > 1) {
      rows.push({
        id: 'a3',
        icon: '🌱',
        title: 'Khu vườn',
        value: `Cấp ${explorerLevel}`,
      });
    }
    if (rewardPoints > 0) {
      rows.push({
        id: 'a4',
        icon: '⭐',
        title: 'Sao · Foxy',
        value: `${rewardPoints.toLocaleString('vi-VN')} — ${starBalanceNote(rewardPoints)}`,
      });
    }
    return rows;
  }, [teamUnlocks, scopedCommitments, explorerLevel, rewardPoints, parentRole]);

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
    setValueView('hub');
    setTab('value');
  };

  const runBriefPrimary = () => {
    const action = homeBrief.primaryAction;
    if (action.kind === 'dna_setup') {
      const el = document.getElementById('famixa-dna') || document.querySelector('.famixa-dna');
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
      leaveBoard('/onboarding');
      return;
    }
    if (action.kind === 'evening_checkin') {
      goValueAnchor('fv-3q');
      return;
    }
    if (action.kind === 'coach' && action.titleVi === 'Khoảnh khắc') {
      setTab('diary');
      return;
    }
    if (action.kind === 'attention') {
      const hit = attentionItems.find((x) => x.id === action.attentionId) ?? attentionItems[0];
      if (!hit) {
        scrollToMissions('need_help');
        return;
      }
      if (hit.kind === 'awaiting') {
        openVerifyPreview(hit.item);
        return;
      }
      if (hit.kind === 'consequence') {
        setInboxAllOpen(true);
        return;
      }
      scrollToMissions('need_help');
      return;
    }
    openCoachOrPaywall();
  };

  const runBriefReason = () => {
    const action = homeBrief.primaryAction;
    if (action.kind === 'dna_setup') {
      leaveBoard('/onboarding');
      return;
    }
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
            <p className="ph-b4-date">{formatFlowDay(flow.flowDate)}</p>
          </div>
        </div>
        <div className="ph-b4-top-right">
          <button
            type="button"
            className="ph-b4-icon-btn"
            aria-label="Việc cần xử lý"
            onClick={() => scrollToMissions('need_help')}
          >
            <IconBell size={20} />
            {attentionItems.length > 0 ? (
              <i>{Math.min(attentionItems.length, 9)}</i>
            ) : null}
          </button>
          <button
            type="button"
            className="ph-b4-icon-btn"
            aria-label="Tài khoản / Cài đặt"
            title="Tài khoản / Cài đặt"
            onClick={openSettings}
          >
            <IconSettings size={20} />
          </button>
        </div>
      </header>
      ) : null}

      {tab === 'home' ? (
        <div className="ph-b4-home">
          {!hasChildren ? renderNoChildNotice() : null}
          {hasChildren ? (
            <div className="ph-b4-focus-bar" aria-label="Đang xem thành viên nào">
              <span className="ph-b4-focus-label">
                Đang xem
                {effectiveChildFocus === 'all' && childOptions.length > 1 ? (
                  <em> · {childOptions.length} con</em>
                ) : null}
              </span>
              {renderChildPicker('module')}
            </div>
          ) : null}
          {hasChildren ? (
            <div className="ph-b4-team-line" aria-label="Tiến độ cả nhà hôm nay">
              <span aria-hidden>👨‍👩‍👧‍👦</span>
              <strong>{houseTeamSummary}</strong>
            </div>
          ) : null}

          {hasChildren ? (
            <TodayOpenStack
              role="parent"
              warmth={openStackWarmth}
              pending={openStackPending}
              seen={openStackSeen}
              yarn={openStackYarn}
              ritualDone={openStackRitualDone}
              ritualBusy={openRitualBusy}
              onCta={handleOpenStackCta}
              onRitualComplete={handleOpenStackRitual}
              onDismissYarn={() => {
                dismissYarn(parentMembershipId || 'parent', flow.flowDate);
                setOpenSeqTick((n) => n + 1);
              }}
            />
          ) : null}

          {partnerInbox.length > 0 ||
          voiceThanksReceipts.some((r) => !dismissedVoiceThanksIds.includes(r.id)) ||
          (parentMembershipId && hasChildren) ||
          weeklyStory ? (
          <section className="ph-rel-strip" aria-label="Gắn kết hôm nay">
            <p className="ph-rel-strip-eyebrow">💛 Gắn kết nhà mình · trước việc nhà</p>

            {voiceThanksReceipts.filter((r) => !dismissedVoiceThanksIds.includes(r.id)).length >
            0 ? (
              <div className="ph-partner-inbox" aria-label="Con vừa cảm ơn lời của bạn">
                {voiceThanksReceipts
                  .filter((r) => !dismissedVoiceThanksIds.includes(r.id))
                  .map((n) => (
                    <article key={n.id} className="ph-partner-inbox-card">
                      <p className="ph-partner-inbox-eyebrow">
                        <span aria-hidden>💛</span> Con vừa cảm ơn lời của bạn
                      </p>
                      <p className="ph-partner-inbox-msg">
                        {shortMemberName(n.toMemberName || 'Con')} đã cảm ơn:{' '}
                        “{n.bodyVi.trim().slice(0, 120)}
                        {n.bodyVi.trim().length > 120 ? '…' : ''}”
                      </p>
                      <div className="ph-combo-actions">
                        <button
                          type="button"
                          className="ph-nudge-btn is-primary"
                          onClick={() =>
                            setDismissedVoiceThanksIds((prev) =>
                              prev.includes(n.id) ? prev : [...prev, n.id],
                            )
                          }
                        >
                          Đã biết
                        </button>
                      </div>
                    </article>
                  ))}
              </div>
            ) : null}

            {partnerInbox.length > 0 ? (
              <div className="ph-partner-inbox" aria-label="Lời từ người cùng chăm">
                {partnerInbox.map((n) => (
                  <article key={n.id} className="ph-partner-inbox-card">
                    <p className="ph-partner-inbox-eyebrow">
                      <span aria-hidden>{parentVoiceIcon(n.templateCode)}</span> Lời từ{' '}
                      {n.fromMemberName.trim() || 'bố/mẹ'} — trả lời ngay
                    </p>
                    <p className="ph-partner-inbox-msg">{n.bodyVi}</p>
                    <div className="ph-combo-actions">
                      <button
                        type="button"
                        className="ph-nudge-btn is-primary"
                        disabled={partnerAckBusy === n.id}
                        onClick={() => void ackPartnerVoice(n.id, 'thanks')}
                      >
                        Cảm ơn
                      </button>
                      <button
                        type="button"
                        className="ph-nudge-btn"
                        disabled={partnerAckBusy === n.id}
                        onClick={() => void ackPartnerVoice(n.id, 'read')}
                      >
                        Đã xem
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            ) : null}

            {activeRelTrigger && isParentVoiceTrigger(activeRelTrigger.code) && parentMembershipId ? (
              <div
                className={`ph-rel-trigger-wrap${activeRelTrigger.isGolden ? ' is-golden' : ''}${voiceUnsent ? ' is-unsent' : ''}`}
              >
                <button
                  type="button"
                  className="ph-rel-trigger-cta"
                  onClick={() => openParentVoiceSheet(activeRelTrigger)}
                >
                  <span className="ph-rel-trigger-ico" aria-hidden>
                    {voiceUnsent ? '✨' : parentVoiceIcon(activeRelTrigger.templateCode)}
                  </span>
                  <span>
                    {voiceUnsent
                      ? `Còn 1 chạm — ${(activeRelTrigger.toMemberName || 'con').trim()} chưa nhận lời`
                      : activeRelTrigger.titleVi}
                    <em>
                      {voiceUnsent
                        ? 'Bạn đã soạn lời nhưng chưa gửi.'
                        : activeRelTrigger.bodyVi}
                    </em>
                  </span>
                  <i className="ph-sibling-nudge-chev" aria-hidden>
                    ›
                  </i>
                </button>
                <button
                  type="button"
                  className="ph-rel-trigger-dismiss"
                  onClick={dismissParentVoiceTrigger}
                >
                  Để sau
                </button>
              </div>
            ) : parentMembershipId && hasChildren ? (
              <div className="ph-rel-compose-stack">
                <button
                  type="button"
                  className="ph-rel-trigger-cta"
                  onClick={() => openParentVoiceSheet(null, 'child')}
                >
                  <span className="ph-rel-trigger-ico" aria-hidden>
                    ❤️
                  </span>
                  <span>
                    {effectiveChildFocus !== 'all'
                      ? `Gửi một lời ấm tới ${
                          childOptions.find((c) => c.key === effectiveChildFocus)?.name
                            ?.split(/\s+/)
                            .filter(Boolean)
                            .slice(-1)[0] || 'con'
                        }`
                      : 'Gửi một lời ấm tới con'}
                    <em>Bạn nói — Famixa chỉ chuyển lời. Không cần chờ mốc streak.</em>
                  </span>
                  <i className="ph-sibling-nudge-chev" aria-hidden>
                    ›
                  </i>
                </button>
                {adultOptions.length > 0 ? (
                  <button
                    type="button"
                    className="ph-rel-compose-adult"
                    onClick={() => openParentVoiceSheet(null, 'adult')}
                  >
                    Gửi lời tới người cùng chăm ›
                  </button>
                ) : null}
              </div>
            ) : null}

            {weeklyStory ? (
              <article className="ph-bond-week" aria-label="Câu chuyện tuần này">
                <p className="ph-bond-week-eyebrow">📖 Tuần này nhà mình</p>
                <p className="ph-bond-week-head">{weeklyStory.headlineVi}</p>
                <ul className="ph-bond-week-lines">
                  {weeklyStory.lines.slice(0, 3).map((line, idx) => (
                    <li key={`${line.textVi}-${idx}`}>
                      <span aria-hidden>{line.icon}</span>
                      <span>{line.textVi}</span>
                    </li>
                  ))}
                </ul>
                <div className="ph-bond-week-actions">
                  <button
                    type="button"
                    className="ph-nudge-btn"
                    onClick={() => void openParentWeekReview()}
                  >
                    Xem lại lời tuần này
                  </button>
                  {parentMembershipId && hasChildren ? (
                    <button
                      type="button"
                      className="ph-nudge-btn is-primary"
                      onClick={() => openParentVoiceSheet(null, 'child')}
                    >
                      Gửi thêm lời tới con
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="ph-nudge-btn"
                    onClick={() =>
                      void shareOrCopyNudge(
                        formatWeeklyStoryShare({
                          familyName,
                          headlineVi: weeklyStory.headlineVi,
                          lines: weeklyStory.lines,
                        }),
                      )
                        .then(() =>
                          showActionToast('Đã copy — mở Zalo dán gửi ông bà nhé'),
                        )
                        .catch(() => showActionToast('Chưa copy được — thử lại nhé'))
                    }
                  >
                    Copy gửi ông bà
                  </button>
                </div>
              </article>
            ) : null}

            {voiceToast ? (
              <p className="ph-sibling-nudge-toast" role="status">
                {voiceToast}
              </p>
            ) : null}
          </section>
          ) : null}

          <article className="ph-b4-brief" aria-label="Morning Brief">
            <div className="ph-b4-brief-main">
              <p className="ph-b4-brief-eyebrow">
                <img className="ph-b4-brief-mark" src="/brand/fami-mark-48.png" alt="" aria-hidden />
                {homeBrief.period === 'evening' ? 'Gợi ý tối' : 'Gợi ý sáng'}
                <span className="ph-b4-brief-who"> · Cả nhà</span>
              </p>
              <h2 className="ph-b4-brief-title">
                {homeBrief.primaryAction.kind === 'dna_setup' ||
                homeBrief.primaryAction.kind === 'evening_checkin'
                  ? homeBrief.moodLineVi
                  : homeBrief.period === 'evening'
                    ? 'Tối nay Famixa gợi ý một việc nhẹ.'
                    : houseTeamRemaining > 0
                      ? houseTeamRemaining === 1
                        ? 'Cả đội còn 1 việc nữa để hoàn thành ngày hôm nay.'
                        : `Cả đội còn ${houseTeamRemaining} việc nữa để hoàn thành ngày hôm nay.`
                      : houseTeamTotal > 0
                        ? 'Mission Complete — cả đội đã xong ngày hôm nay.'
                        : 'Hôm nay ưu tiên 1 việc trước.'}
              </h2>
              <div
                className={`ph-b4-brief-task is-${briefTaskTone}${
                  homeBrief.primaryAction.kind === 'attention' &&
                  homeBrief.primaryAction.attentionKind === 'awaiting'
                    ? ' is-tap'
                    : ''
                }`}
                role={
                  homeBrief.primaryAction.kind === 'attention' &&
                  homeBrief.primaryAction.attentionKind === 'awaiting'
                    ? 'button'
                    : undefined
                }
                tabIndex={
                  homeBrief.primaryAction.kind === 'attention' &&
                  homeBrief.primaryAction.attentionKind === 'awaiting'
                    ? 0
                    : undefined
                }
                onClick={() => {
                  if (
                    homeBrief.primaryAction.kind === 'attention' &&
                    homeBrief.primaryAction.attentionKind === 'awaiting'
                  ) {
                    const hit =
                      attentionItems.find((x) => x.id === homeBrief.primaryAction.attentionId) ??
                      attentionItems.find((x) => x.kind === 'awaiting');
                    if (hit?.kind === 'awaiting') openVerifyPreview(hit.item);
                  }
                }}
                onKeyDown={(e) => {
                  if (e.key !== 'Enter' && e.key !== ' ') return;
                  if (
                    homeBrief.primaryAction.kind === 'attention' &&
                    homeBrief.primaryAction.attentionKind === 'awaiting'
                  ) {
                    e.preventDefault();
                    const hit =
                      attentionItems.find((x) => x.id === homeBrief.primaryAction.attentionId) ??
                      attentionItems.find((x) => x.kind === 'awaiting');
                    if (hit?.kind === 'awaiting') openVerifyPreview(hit.item);
                  }
                }}
              >
                <span className="ph-b4-brief-check" aria-hidden>
                  {briefTaskIcon}
                </span>
                <div className="ph-b4-brief-task-body">
                  <strong>{homeBrief.primaryAction.doThisVi}</strong>
                  {homeBrief.primaryAction.statusVi ? (
                    <em>{homeBrief.primaryAction.statusVi}</em>
                  ) : null}
                </div>
              </div>
              {(() => {
                const because =
                  becauseFromSoftPrefs(softLayers) || becauseFromDna(dnaCard).becauseVi;
                const roleTip = roleMatrixBriefTip(dnaCard?.stageLabelVi);
                const line =
                  homeBrief.primaryAction.kind === 'dna_setup'
                    ? homeBrief.primaryAction.reasonVi
                    : because ||
                      homeBrief.bulletsVi.find(
                        (b) => b.includes('Vì nhà bạn') || b.includes('Famixa chưa biết'),
                      );
                if (!line && !roleTip) return null;
                return (
                  <p
                    className="ph-b4-brief-because"
                    data-playbook={homeBrief.primaryAction.playbookId || undefined}
                  >
                    {[line, roleTip].filter(Boolean).join(' · ')}
                  </p>
                );
              })()}
              <button type="button" className="ph-b4-brief-cta" onClick={runBriefPrimary}>
                <span aria-hidden>⚡</span>
                {homeBrief.primaryAction.kind === 'dna_setup'
                  ? 'Hoàn tất DNA'
                  : homeBrief.primaryAction.kind === 'evening_checkin'
                    ? 'Trả lời 3 câu'
                    : homeBrief.primaryAction.kind === 'attention' &&
                        homeBrief.primaryAction.attentionKind === 'awaiting'
                      ? 'Xem & xác nhận'
                      : 'Thực hiện ngay'}
              </button>
            </div>
          </article>

          {weekPlaybook?.parentStrategyTipVi ? (
            <button
              type="button"
              className="ph-week-tip"
              aria-label="Gợi ý đồng hành tuần này"
              onClick={() => goValueAnchor('fv-weekly')}
            >
              <span className="ph-week-tip-ico" aria-hidden>
                🌱
              </span>
              <span className="ph-week-tip-body">
                <strong>Tuần này thử một cách nhẹ</strong>
                <em>
                  {weekPlaybook.patternTitleVi
                    ? `${weekPlaybook.patternTitleVi} · `
                    : ''}
                  {weekPlaybook.parentStrategyTipVi}
                </em>
                {weekPlaybook.childVoice?.submittedAt ? (
                  <i>Con đã gửi lời tuần này — xem tip →</i>
                ) : (
                  <i>Xem pattern & tiếng nói của con →</i>
                )}
              </span>
              <span className="ph-week-tip-chev" aria-hidden>
                ›
              </span>
            </button>
          ) : null}

          {showSoftCal ? (
            <section className="ph-soft-cal" aria-label="Famixa hỏi nhẹ">
              <p className="ph-soft-cal-q">{softCalQuestion.questionVi}</p>
              <div className="ph-soft-cal-opts">
                {softCalQuestion.options.map((o) => (
                  <button
                    key={o.code}
                    type="button"
                    className="ph-soft-cal-chip"
                    disabled={softCalBusy}
                    onClick={() => void answerSoftCal(softCalQuestion, o.code)}
                  >
                    {o.labelVi}
                  </button>
                ))}
              </div>
              <button
                type="button"
                className="ph-rel-trigger-dismiss"
                disabled={softCalBusy}
                onClick={() => setSoftCalDismissed(true)}
              >
                Để tuần sau
              </button>
            </section>
          ) : null}
          {softCalToast ? (
            <p className="ph-sibling-nudge-toast" role="status">
              {softCalToast}
            </p>
          ) : null}

          {hasChildren && childOptions.length >= 2 && houseTeamRemaining >= 1 ? (
            <button
              type="button"
              className="ph-sibling-nudge-cta"
              onClick={() => void openSiblingNudgeSheet()}
            >
              <span className="ph-sibling-nudge-ico" aria-hidden>
                🤝
              </span>
              <span>
                Mời anh/chị nhắc em
                <em>Lời nhắc cố định — không gọi tên ai trên bảng nhà</em>
              </span>
              <i className="ph-sibling-nudge-chev" aria-hidden>
                ›
              </i>
            </button>
          ) : null}
          {nudgeToast ? (
            <p className="ph-sibling-nudge-toast" role="status">
              {nudgeToast}
            </p>
          ) : null}

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
                    Gợi ý Famixa: 3 câu tối giúp duy trì thói quen tốt.{' '}
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
                  <span aria-hidden>🎯</span>{' '}
                  {effectiveChildFocus === 'all'
                    ? 'Ưu tiên hôm nay'
                    : `Ưu tiên của ${selectedChild?.name ?? childShort}`}
                </h3>
              </header>
              {homeAttention.length === 0 &&
              !(homeBrief.period === 'evening' && (homeBrief.eveningOverdueCount ?? 0) > 0) ? (
                <p className="ph-b4-empty">Không việc nóng — nhà đang ổn.</p>
              ) : (
                <ul className="ph-b4-priority-list">
                  {homeBrief.period === 'evening' && (homeBrief.eveningOverdueCount ?? 0) > 0 ? (
                    <li>
                      <button
                        type="button"
                        className="ph-b4-priority-item"
                        onClick={() => scrollToMissions('need_help')}
                      >
                        <span className="ph-b4-priority-ico" aria-hidden>
                          <IconTasks size={18} />
                        </span>
                        <span>
                          <strong>
                            {homeBrief.eveningOverdueCount} việc sáng còn mở
                          </strong>
                          <em>Gộp xem trong Nhiệm vụ — tối nay ưu tiên 3 câu phản hồi</em>
                        </span>
                        <i aria-hidden />
                      </button>
                    </li>
                  ) : null}
                  {homeAttention.map((a) => {
                    if (a.kind === 'consequence') {
                      return (
                        <li key={a.id}>
                          <button
                            type="button"
                            className="ph-b4-priority-item"
                            onClick={() => setInboxAllOpen(true)}
                          >
                            <span className="ph-b4-priority-ico" aria-hidden>
                              ⚖️
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
                    const clock = a.item.windowEnd ? a.item.windowEnd.slice(0, 5) : '';
                    const who =
                      a.item.memberName?.trim() ||
                      (effectiveChildFocus !== 'all' ? selectedChild?.name : null);
                    const metaBase =
                      a.kind === 'awaiting'
                        ? a.item.commitmentKind === 'study_focus' &&
                          a.item.evidenceSatisfied === false
                          ? 'Con báo đã xong · chờ bằng chứng / xác nhận'
                          : 'Con báo đã xong · chạm để xác nhận'
                        : a.kind === 'overdue'
                          ? clock
                            ? `Quá giờ · lẽ ra xong trước ${clock}`
                            : 'Đã quá giờ — chưa xong'
                          : clock
                            ? `Cần chú ý · trước ${clock}`
                            : 'Cần chú ý';
                    const meta = who ? `${who} · ${metaBase}` : metaBase;
                    return (
                      <li key={a.id}>
                        <button
                          type="button"
                          className="ph-b4-priority-item"
                          onClick={() => {
                            if (a.kind === 'awaiting') {
                              openVerifyPreview(a.item);
                              return;
                            }
                            scrollToMissions('need_help');
                          }}
                        >
                          <span className="ph-b4-priority-ico" aria-hidden>
                            {a.kind === 'awaiting' ? '⏳' : a.kind === 'overdue' ? '⏰' : '·'}
                          </span>
                          <span>
                            <strong>{a.item.title}</strong>
                            <em>{meta}</em>
                          </span>
                          <i aria-hidden />
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}              <button
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
              onSeeAll={() => setInboxAllOpen(true)}
              onOpenMode={() => setModeSheetOpen(true)}
            />
          </div>

          <section
            className="ph-b4-progress"
            aria-label="Tiến độ cả gia đình"
          >
            <header className="ph-b4-col-head">
              <h3>
                <span aria-hidden>👨‍👩‍👧‍👦</span> Tiến độ cả gia đình
              </h3>
            </header>
            <p className="ph-b4-progress-copy">{houseTeamSummary}</p>
            <div className="ph-b4-progress-row">
              <div className="ph-b4-segments" aria-hidden>
                {Array.from({ length: progressSegments.segs }, (_, idx) => {
                  const filled = Math.round(
                    (houseTeamPercent / 100) * progressSegments.segs,
                  );
                  return (
                    <i
                      key={idx}
                      className={idx < filled ? 'is-on' : undefined}
                    />
                  );
                })}
              </div>
              <strong>{houseTeamPercent}%</strong>
            </div>
            {effectiveChildFocus !== 'all' && selectedChild ? (
              <p className="ph-b4-progress-child">
                Đang xem {selectedChild.name}:{' '}
                {familyProgressLine(scopedDone, Math.max(scopedTotal, 0))}
              </p>
            ) : null}
            <button
              type="button"
              className="ph-b4-see-all"
              onClick={() => scrollToMissions()}
            >
              Xem chi tiết ›
            </button>
          </section>

          {coopScore ? (
            <section className="ph-coop-card" aria-label="Điểm hợp tác tuần này">
              <header className="ph-b4-col-head ph-coop-head">
                <h3>
                  <span className="ph-coop-ico" aria-hidden>
                    🏆
                  </span>{' '}
                  Hợp tác tuần này
                </h3>
                <strong className="ph-coop-score">{coopScore.total}/100</strong>
              </header>
              <div className="ph-coop-bar" aria-hidden>
                <i style={{ width: `${Math.min(100, Math.max(0, coopScore.total))}%` }} />
              </div>
              <p className="ph-coop-copy">
                {coopScore.headlineVi || 'Cả nhà đang giữ nhịp cùng nhau.'}
              </p>
              {coopScore.sparkline.length > 0 ? (
                <div className="ph-coop-spark" aria-hidden>
                  {coopScore.sparkline.map((p) => (
                    <i
                      key={p.scoreDate}
                      style={{ height: `${Math.max(12, p.total)}%` }}
                      title={`${p.scoreDate}: ${p.total}`}
                    />
                  ))}
                </div>
              ) : null}
            </section>
          ) : null}

          {todayComboUnlock ? (
            <section className="ph-combo-card" aria-label="High-five anh chị">
              <header className="ph-b4-col-head">
                <h3>
                  <span aria-hidden>🙌</span> High-five đôi anh chị
                </h3>
              </header>
              <p className="ph-combo-copy">
                {todayComboUnlock.labelVi || 'Hai con cùng xong một việc — thưởng đội nhỏ.'}
              </p>
              <em className="ph-combo-meta">
                {todayComboUnlock.status === 'confirmed'
                  ? 'Đã mở thưởng đội nhỏ'
                  : todayComboUnlock.status === 'pending_confirm'
                    ? 'Chờ bố/mẹ xác nhận · không gắn bảng xếp hạng cá nhân'
                    : 'Đã để sau'}
              </em>
              {todayComboUnlock.status === 'pending_confirm' && parentMembershipId ? (
                <div className="ph-combo-actions">
                  <button
                    type="button"
                    className="ph-nudge-btn is-primary"
                    disabled={unlockBusy}
                    onClick={() => void onDecideUnlockById(todayComboUnlock.id, 'confirmed')}
                  >
                    {unlockBusy ? 'Đang lưu…' : 'Mở thưởng đội nhỏ'}
                  </button>
                  <button
                    type="button"
                    className="ph-nudge-btn"
                    disabled={unlockBusy}
                    onClick={() => void onDecideUnlockById(todayComboUnlock.id, 'deferred')}
                  >
                    Để sau
                  </button>
                </div>
              ) : null}
            </section>
          ) : null}

          {rituals.length > 0 ? (
            <section className="ph-ritual-card" aria-label="Ritual của bố mẹ tuần này">
              <header className="ph-b4-col-head ph-ritual-head">
                <h3>
                  <span className="ph-ritual-ico" aria-hidden>
                    🌿
                  </span>{' '}
                  Ritual của bố mẹ
                </h3>
                <strong className="ph-ritual-count">
                  {rituals.filter((r) => r.doneThisPeriod).length}/{rituals.length}
                </strong>
              </header>
              <ul className="ph-ritual-list">
                {rituals.map((r) => (
                  <li key={r.code}>
                    <button
                      type="button"
                      className={r.doneThisPeriod ? 'is-done' : undefined}
                      disabled={r.doneThisPeriod || ritualBusy === r.code}
                      onClick={() => void markRitualDone(r.code)}
                    >
                      <span className="ph-ritual-check" aria-hidden>
                        {r.doneThisPeriod ? '✓' : ''}
                      </span>
                      <span className="ph-ritual-text">{r.labelVi}</span>
                    </button>
                  </li>
                ))}
              </ul>
              <p className="ph-ritual-foot">Tách khỏi % đội con — nhịp riêng của bố mẹ.</p>
            </section>
          ) : null}

          {eveningCircle ? (
            <section className="ph-circle-card" aria-label="Evening Circle">
              <header className="ph-b4-col-head">
                <h3>
                  <span aria-hidden>⭐</span> Evening Circle
                </h3>
              </header>
              <p className="ph-circle-prompt">{eveningCircle.promptVi}</p>
              {eveningCircle.answers.length > 0 ? (
                <ul className="ph-circle-answers">
                  {eveningCircle.answers.map((a) => (
                    <li key={a.memoryId}>
                      <strong>{a.memberName}</strong>
                      <span>{a.answerVi}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="ph-circle-empty">Chưa ai trả lời — một câu ngắn là đủ.</p>
              )}
              {parentMembershipId && !eveningCircle.alreadyAnswered ? (
                <div className="ph-circle-compose">
                  <textarea
                    rows={2}
                    maxLength={280}
                    value={circleAnswer}
                    onChange={(e) => setCircleAnswer(e.target.value)}
                    placeholder="Câu trả lời của bạn…"
                    disabled={circleBusy}
                  />
                  <button
                    type="button"
                    className="ph-nudge-btn is-primary"
                    disabled={circleBusy || !circleAnswer.trim()}
                    onClick={() => void submitEveningCircle()}
                  >
                    {circleBusy ? 'Đang lưu…' : 'Gửi câu trả lời'}
                  </button>
                </div>
              ) : parentMembershipId && eveningCircle.alreadyAnswered ? (
                <p className="ph-circle-empty">Bạn đã trả lời tối nay — cảm ơn.</p>
              ) : null}
            </section>
          ) : null}

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
                    onClick={() => setTab('challenge')}
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
              <button type="button" onClick={() => openAccountSettings('billing')}>
                Gia hạn ›
              </button>
            </section>
          ) : (
            <BillingBanner familyId={familyId} />
          )}

          <button
            type="button"
            className="ph-b4-account"
            onClick={() => openAccountSettings()}
            aria-label="Tài khoản và cài đặt gia đình"
          >
            <span className="ph-b4-account-ico" aria-hidden>
              ⚙️
            </span>
            <span className="ph-b4-account-copy">
              <strong>Tài khoản / Cài đặt</strong>
              <em>Gia hạn · thông báo · mã PIN · rời nhà</em>
            </span>
            <span className="ph-b4-account-go" aria-hidden>
              ›
            </span>
          </button>

          <FamilyDnaCardView
            familyId={familyId}
            dna={dnaCard}
            loading={dnaLoading}
            houseScopeNote={
              childOptions.length > 1
                ? 'Cả gia đình (cho tất cả các con)'
                : dnaCard?.growthBalanceLabelVi || null
            }
            onDnaChange={(d) => setDnaCard(d)}
            onUpgrade={() => {
              setPaywallReason(
                dnaCard?.upgradeHintVi ||
                  'Nâng Peace Plan để xem việc nhà đang tập cùng con và bước nhỏ tiếp theo.',
              );
              setPaywallOpen(true);
            }}
            onSetup={() => leaveBoard('/onboarding')}
          />

          <nav className="ph-b4-explore-row" aria-label="Lối tắt tính năng">
            <button type="button" onClick={openCoachOrPaywall}>
              <i className="is-green" aria-hidden>
                <IconRobot size={18} />
              </i>
              Coach AI
            </button>
            <button type="button" onClick={() => goValueAnchor('fv-3q')}>
              <i className="is-purple" aria-hidden>
                <IconTarget size={18} />
              </i>
              3 câu tối
            </button>
            <button type="button" onClick={() => setTab('rewards')}>
              <i className="is-yellow" aria-hidden>
                <IconStar size={18} />
              </i>
              Kho báu
            </button>
            <button type="button" onClick={() => setTab('challenge')}>
              <i className="is-pink" aria-hidden>
                <IconTrophy size={18} />
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
                {hasChildren ? (
                  <>
                    Đang xem: <strong>{childFocusLabel}</strong>
                  </>
                ) : (
                  <>Chưa có con trong nhà</>
                )}
              </p>
            </div>
            <div className="ph-tasks-actions">{renderChildPicker('module')}</div>
          </header>

          {!hasChildren ? renderNoChildNotice() : null}

          <div className="ph-tasks-tabs" role="tablist" aria-label="Lọc nhiệm vụ">
            {(
              [
                { key: 'all' as const, label: 'Tất cả', count: scopedTotal, tone: 'purple' },
                {
                  key: 'need_help' as const,
                  label: parentSupportLabel(parentRole),
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
              aria-label="Xem ngày trước"
              title="Xem ngày trước"
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
              aria-label="Xem ngày sau"
              title="Xem ngày sau"
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
                {!hasChildren
                  ? 'Chưa có con trong nhà — thêm con để Famixa giao việc hằng ngày.'
                  : voicePick(`${flow.flowDate}:tasks-banner:${needHelpItems.length}`, [
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
                  <span aria-hidden>🚨</span> {parentSupportLabel(parentRole)}
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
                  .slice(0, missionFilter === 'all' ? 3 : 20)
                  .map((item) => {
                    const state = missionUxState(item, flow.flowDate, verifiedTick);
                    const kind = state === 'awaiting_check' ? 'awaiting' : 'overdue';
                    const deadline = item.windowEnd
                      ? `Trước ${item.windowEnd.slice(0, 5)}`
                      : lateLabel(item, flow.localTime);
                    const itemWho = item.memberName?.trim() || childShort;
                    return (
                      <li key={item.id} className="ph-task-card is-help">
                        <span className="ph-task-card-ico" aria-hidden>
                          {taskIcon(item.title)}
                        </span>
                        <div className="ph-task-card-body">
                          <strong>{item.title}</strong>
                          <span className="ph-task-owner">
                            <span aria-hidden>
                              {avatarEmoji(inferGenderFromName(itemWho), 'child')}
                            </span>
                            {itemWho}
                          </span>
                          <p>
                            {warmTaskSupportNote({
                              title: item.title,
                              childShort: itemWho,
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
                                alt={`Ảnh ${itemWho} gửi — ${item.title}`}
                                className="evidence-thumb is-board"
                              />
                            </a>
                          ) : null}
                          <span
                            className="ph-task-who"
                            title={itemWho}
                            aria-label={`Việc của ${itemWho}`}
                          >
                            {avatarEmoji(inferGenderFromName(itemWho), 'child')}
                          </span>
                          {kind === 'awaiting' ? (
                            <button
                              type="button"
                              className="ph-task-cta is-check"
                              disabled={busyId === item.id || verifyingId === item.id}
                              onClick={() => openVerifyPreview(item)}
                            >
                              {busyId === item.id || verifyingId === item.id
                                ? 'Đang…'
                                : 'Xem & xác nhận'}
                            </button>
                          ) : canRemindChildNow(item) ? (
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
                          ) : (
                            <span className="ph-task-cta is-muted" aria-label="Chưa tới giờ nhắc">
                              {remindChildIdleLabel(item)}
                            </span>
                          )}
                        </div>
                      </li>
                    );
                  })}
                {needHelpItems.length === 0 ? (
                  <li className="ph-empty-soft">
                    Không có việc cần {parentRole} hỗ trợ ngay.
                  </li>
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
                    .slice(0, missionFilter === 'all' ? 3 : 20)
                    .map((item) => {
                      const itemWho = item.memberName?.trim() || childShort;
                      return (
                      <li key={item.id} className="ph-task-card is-wait">
                        <span className="ph-task-card-ico" aria-hidden>
                          {taskIcon(item.title)}
                        </span>
                        <div className="ph-task-card-body">
                          <strong>{item.title}</strong>
                          <span className="ph-task-owner">
                            <span aria-hidden>
                              {avatarEmoji(inferGenderFromName(itemWho), 'child')}
                            </span>
                            {itemWho}
                          </span>
                          <p>
                            {warmTaskTip({
                              title: item.title,
                              childShort: itemWho,
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
                          <span
                            className="ph-task-who"
                            title={itemWho}
                            aria-label={`Việc của ${itemWho}`}
                          >
                            {avatarEmoji(inferGenderFromName(itemWho), 'child')}
                          </span>
                          {canRemindChildNow(item) ? (
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
                          ) : (
                            <span className="ph-task-cta is-muted" aria-label="Chưa tới giờ nhắc">
                              {remindChildIdleLabel(item)}
                            </span>
                          )}
                        </div>
                      </li>
                      );
                    })}
                  {waitingChildItems.length === 0 ? (
                    <li className="ph-empty-soft">
                      {hasChildren
                        ? 'Không còn việc chờ con làm.'
                        : 'Chưa có con trong nhà — thêm con để giao việc.'}
                    </li>
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
                  .slice(0, missionFilter === 'all' ? 4 : 30)
                  .map((item) => {
                    const clock = formatClock(item.completedAt);
                    const itemWho = item.memberName?.trim() || childShort;
                    return (
                      <li key={item.id} className="ph-task-card is-done">
                        <span className="ph-task-card-ico" aria-hidden>
                          {taskIcon(item.title)}
                        </span>
                        <div className="ph-task-card-body">
                          <strong>{item.title}</strong>
                          <span className="ph-task-owner">
                            <span aria-hidden>
                              {avatarEmoji(inferGenderFromName(itemWho), 'child')}
                            </span>
                            {itemWho}
                          </span>
                          <p>
                            {warmTaskTip({
                              title: item.title,
                              childShort: itemWho,
                              parentRole,
                              flowDate: flow.flowDate,
                              itemId: item.id,
                            })}
                          </p>
                        </div>
                        <div className="ph-task-card-side">
                          <span
                            className="ph-task-who"
                            title={itemWho}
                            aria-label={`Việc của ${itemWho}`}
                          >
                            {avatarEmoji(inferGenderFromName(itemWho), 'child')}
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
              aria-label={valueView === 'hub' ? 'Về trang chủ' : 'Về báo cáo'}
              onClick={() => {
                if (valueView === 'hub') {
                  setTab('home');
                  return;
                }
                setValueFocus(null);
                setValueView('hub');
              }}
            >
              ‹
            </button>
            <div className="ph-report-titles">
              <h1>
                {valueView === 'hub'
                  ? 'Báo cáo gia đình'
                  : FV_DETAIL_TITLES[valueView][0]}
              </h1>
              <p>
                {valueView === 'hub'
                  ? 'Sức khỏe nhà · AI đề xuất · mở từng mục khi cần'
                  : FV_DETAIL_TITLES[valueView][1]}
              </p>
            </div>
            <div className="ph-report-tools">{renderChildPicker('module')}</div>
          </header>
          {!hasChildren && valueView === 'hub' ? renderNoChildNotice() : null}
          <div className="ph-report-body">
            <FamilyValuePanel
              familyId={familyId}
              familyName={familyName}
              flow={{ ...flow, commitments: scopedCommitments }}
              glance={glance}
              nudgeToday={nudgeToday}
              coachScope={coachScope}
              momentCount={savedMemories.length + childGratitudes.length}
              onOpenPaywall={(reason) => openPaywall(reason)}
              parentMembershipId={parentMembershipId}
              eveningCheckin={eveningCheckin}
              onEveningCheckinChange={setEveningCheckin}
              dna={dnaCard}
              focusAnchorId={valueFocus}
              view={valueView}
              onViewChange={setValueView}
            />
          </div>
          <p
            className="ph-report-diary-hint"
            hidden={valueView !== 'hub'}
          >
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
                {hasChildren ? `Nhật ký của ${childShort}` : 'Nhật ký gia đình'}{' '}
                <span aria-hidden>💜</span>
              </h1>
              <p>{hasChildren ? `Đang xem: ${childFocusLabel}` : 'Chưa có con trong nhà'}</p>
            </div>
            <div className="ph-diary-tools">{renderChildPicker('module')}</div>
          </header>

          {!hasChildren ? renderNoChildNotice() : null}

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

          <div className="ph-diary-filters" aria-label="Memory lens theo thành viên">
            <button
              type="button"
              className={`ph-diary-chip${diaryMemberFilter === 'all' ? ' is-on' : ''}`}
              onClick={() => setDiaryMemberFilter('all')}
            >
              Cả nhà
            </button>
            {childOptions.map((c) => (
              <button
                key={c.key}
                type="button"
                className={`ph-diary-chip${diaryMemberFilter === c.key ? ' is-on' : ''}`}
                onClick={() => setDiaryMemberFilter(c.key)}
              >
                Của {shortMemberName(c.name)}
              </button>
            ))}
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
                      {hasChildren ? (
                        <>
                          {diarySummary.prefix}
                          {diarySummary.ratio ? (
                            <>
                              {' '}
                              <strong>{diarySummary.ratio}</strong>
                              {diarySummary.suffix ? ` ${diarySummary.suffix}` : ''}
                            </>
                          ) : null}
                        </>
                      ) : (
                        <>Chưa có con — thêm con để nhật ký bắt đầu ghi việc mỗi ngày.</>
                      )}
                    </p>
                    <em>
                      +{diaryStarsEarned} ⭐
                      {diaryStarsPending > 0 ? ` (+${diaryStarsPending} chờ duyệt)` : ''}
                    </em>
                    {studyEvidenceLine && studyEvidenceLine.done > 0 ? (
                      <em style={{ display: 'block', marginTop: 4 }}>
                        Cam kết học: {studyEvidenceLine.withEvidence}/{studyEvidenceLine.done} có
                        bằng chứng
                      </em>
                    ) : null}
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
              ) : diaryEntries.length === 0 ? (
                <p className="ph-diary-empty">
                  {hasChildren
                    ? `Hôm nay chưa có trang nhật ký — khi ${childShort} làm việc, nhật ký sẽ hiện ở đây.`
                    : 'Chưa có con trong nhà — thêm con để Famixa ghi nhật ký mỗi ngày.'}
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
                        <article
                          className={`ph-diary-card${entry.wait ? ' is-tap' : ''}`}
                          role={entry.wait ? 'button' : undefined}
                          tabIndex={entry.wait ? 0 : undefined}
                          onClick={
                            entry.wait
                              ? () => openVerifyPreview(entry.item)
                              : undefined
                          }
                          onKeyDown={
                            entry.wait
                              ? (e) => {
                                  if (e.key === 'Enter' || e.key === ' ') {
                                    e.preventDefault();
                                    openVerifyPreview(entry.item);
                                  }
                                }
                              : undefined
                          }
                        >
                          <span className={`ph-diary-ico tone-${entry.tone}`} aria-hidden>
                            {taskIcon(entry.item.title)}
                          </span>
                          <div className="ph-diary-card-body">
                            <div className="ph-diary-card-head">
                              <strong>{entry.item.title}</strong>
                              {entry.done ? (
                                <span className="ph-diary-status is-ok">Hoàn thành</span>
                              ) : entry.wait ? (
                                <span className="ph-diary-status is-wait">
                                  {entry.item.commitmentKind === 'study_focus' &&
                                  entry.item.evidenceSatisfied === false
                                    ? 'Chờ bằng chứng'
                                    : 'Chờ kiểm tra'}
                                </span>
                              ) : entry.skipped ? (
                                <span className="ph-diary-status is-skip">Bỏ qua</span>
                              ) : (
                                <span className="ph-diary-status is-pending">Chưa xong</span>
                              )}
                            </div>
                            {entry.who && childOptions.length > 1 ? (
                              <span className="ph-task-owner">
                                <span aria-hidden>
                                  {avatarEmoji(inferGenderFromName(entry.who), 'child')}
                                </span>
                                {entry.who}
                              </span>
                            ) : null}
                            <p>{entry.note}</p>
                            {entry.wait && entry.item.commitmentKind === 'study_focus' ? (
                              <p className="muted" style={{ marginTop: 4, fontSize: '0.82rem' }}>
                                {entry.item.evidenceUrl
                                  ? 'Có ảnh đính kèm — chạm để xem trước khi xác nhận.'
                                  : 'Chưa có ảnh — xem chi tiết rồi xác nhận nếu tin lời con.'}
                              </p>
                            ) : null}
                            <span className={`ph-diary-tag tone-${entry.tag.tone}`}>
                              {entry.tag.label}
                            </span>
                          </div>
                          <div
                            className="ph-diary-card-side"
                            onClick={(e) => e.stopPropagation()}
                            onKeyDown={(e) => e.stopPropagation()}
                          >
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
                                onClick={() => openVerifyPreview(entry.item)}
                              >
                                {busyId === entry.item.id || verifyingId === entry.item.id
                                  ? 'Đang…'
                                  : 'Xem & xác nhận'}
                              </button>
                            ) : null}
                          </div>
                        </article>
                      </li>
                    );
                  })}
                </ol>
              )}

              {selectedDiaryDay?.isToday && diaryEntries.length > 5 ? (
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
              {hasChildren ? (
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
                            ? `${childShort} hơi bình thường — ${parentRole} dành thêm thời gian nhé.`
                            : `${childShort} cần ${parentRole} động viên thêm hôm nay.`}
                      </p>
                    )}
                  </>
                ) : (
                  <p className="ph-diary-mood-bubble">{childShort} chưa ghi tâm trạng hôm nay</p>
                )}
              </article>
              ) : null}

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

      {tab === 'challenge' ? (
        <div className="ph-challenge-page">
          <header className="ph-challenge-top">
            <button
              type="button"
              className="ph-challenge-back"
              aria-label="Về trang chủ"
              onClick={() => setTab('home')}
            >
              ‹
            </button>
            <div className="ph-challenge-titles">
              <h1>Challenge &amp; mục tiêu</h1>
              <p>Thử thách tuần · mục tiêu bố mẹ làm gương</p>
            </div>
          </header>
          {parentMembershipId ? (
            <div className="ph-challenge-body">
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
            </div>
          ) : (
            <p className="ph-empty-soft">Cần hồ sơ bố/mẹ để mở Challenge.</p>
          )}
          <p className="ph-challenge-hint">
            Đổi sao / quà ở{' '}
            <button type="button" className="ph-text-link" onClick={() => setTab('rewards')}>
              Kho báu →
            </button>
          </p>
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
                {hasChildren
                  ? `Sao & quà của ${treasureChildName || treasureShort}`
                  : 'Chưa có con trong nhà'}
                {' · '}
                <button type="button" className="ph-text-link" onClick={() => setTab('challenge')}>
                  Challenge →
                </button>
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

          {!hasChildren ? renderNoChildNotice() : null}

          <article className="ph-treasure-hero">
            <div className="ph-treasure-chest" aria-hidden>
              🧰
            </div>
            <div className="ph-treasure-hero-copy">
              <div className="ph-treasure-stars">
                <span aria-hidden>⭐</span>
                <strong>{rewardPoints.toLocaleString('vi-VN')}</strong>
                <em>{hasChildren ? `Sao của ${treasureShort}` : 'Sao của nhà'}</em>
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
                    <b style={{ width: `${moviePct}%` }} />
                  </i>
                  <em>{moviePct}%</em>
                </div>
                <span>
                  {todayUnlock?.status === 'pending_confirm'
                    ? `Sẵn sàng mở — ${parentRole} xác nhận nhé!`
                    : todayUnlock?.status === 'confirmed'
                      ? 'Đã mở thưởng — cả nhà tận hưởng!'
                      : movieMembersLeft > 0
                        ? todayUnlock
                          ? movieMembersLeft === 1
                            ? 'Chỉ còn 1 thành viên nữa!'
                            : `Chỉ còn ${movieMembersLeft} thành viên nữa!`
                          : `Chỉ còn ${movieMembersLeft} nhiệm vụ nữa!`
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
                    Tiến độ Movie Night: {moviePct}%
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
                          ? `${treasureShort} đủ sao để đổi trên màn hình con`
                          : `${treasureShort} chưa đủ sao`
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
                <h3>
                  {hasChildren
                    ? `Huy hiệu của ${treasureShort}`
                    : 'Huy hiệu nhà mình'}
                </h3>
              </header>
              {(() => {
                const earned = (
                  [
                    {
                      id: 'b1',
                      icon: '🎁',
                      label: 'Đổi quà đầu tiên',
                      unlocked: childRedemptions.length > 0,
                    },
                    {
                      id: 'b2',
                      icon: '⭐',
                      label: '100 sao',
                      unlocked: rewardPoints >= 100,
                    },
                    {
                      id: 'b3',
                      icon: '💎',
                      label: '500 sao',
                      unlocked: rewardPoints >= 500,
                    },
                    {
                      id: 'b4',
                      icon: '🔥',
                      label: 'Chuỗi ngày tốt',
                      unlocked: (glance?.currentStreak ?? 0) >= 3,
                    },
                    {
                      id: 'b5',
                      icon: '🏆',
                      label: 'Team Champion',
                      unlocked: percent >= 100,
                    },
                  ] as const
                ).filter((b) => b.unlocked);
                if (earned.length === 0) {
                  return (
                    <p className="muted" style={{ margin: '8px 4px 0', fontSize: '0.85rem' }}>
                      Chưa có huy hiệu — làm việc / đổi quà để mở dần.
                    </p>
                  );
                }
                return (
                  <ul>
                    {earned.map((b) => (
                      <li key={b.id} className="is-on">
                        <span aria-hidden>{b.icon}</span>
                        <em>{b.label}</em>
                      </li>
                    ))}
                  </ul>
                );
              })()}
            </article>
          </div>

          <section className="ph-treasure-sec" ref={achievementsRef}>
            <header className="ph-treasure-sec-head">
              <h2>
                <span aria-hidden>🏅</span> THÀNH TỰU LỚN
              </h2>
              {bigAchievements.length > 0 ? (
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
              ) : null}
            </header>
            {bigAchievements.length === 0 ? (
              <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.85rem' }}>
                Chưa có thành tựu — khi mở Movie Night, đọc sách hoặc tích sao sẽ hiện ở đây.
              </p>
            ) : (
              <div className="ph-treasure-achieve">
                {bigAchievements.map((a) => (
                  <article key={a.id}>
                    <span aria-hidden>{a.icon}</span>
                    <strong>{a.title}</strong>
                    <em>{a.value}</em>
                  </article>
                ))}
              </div>
            )}
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

      <nav className="ph-tabbar ph-tabbar--b5" aria-label="Điều hướng bố mẹ">
        <button
          type="button"
          className={`ph-tab${tab === 'home' ? ' is-on' : ''}`}
          onClick={() => setTab('home')}
        >
          <IconHome size={22} />
          Trang chủ
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'tasks' ? ' is-on' : ''}`}
          onClick={() => setTab('tasks')}
        >
          <IconTasks size={22} />
          Nhiệm vụ
        </button>
        <button
          type="button"
          className="ph-tab ph-tab-add"
          aria-label="Thêm kỷ niệm"
          title="Thêm kỷ niệm gia đình"
          onClick={() => setAddMemoryOpen(true)}
        >
          <IconPlus size={26} />
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'value' ? ' is-on' : ''}`}
          onClick={goReportHub}
        >
          <IconReport size={22} />
          Báo cáo
        </button>
        <button
          type="button"
          className={`ph-tab${tab === 'diary' ? ' is-on' : ''}`}
          onClick={() => setTab('diary')}
        >
          <IconDiary size={22} />
          Nhật ký
        </button>
      </nav>

      {inboxAllOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setInboxAllOpen(false)}
        >
          <div
            className="sheet ph-inbox-all-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Tất cả đề xuất cần duyệt"
            onClick={(e) => e.stopPropagation()}
          >
            <DecisionInboxPanel
              variant="default"
              maxItems={30}
              familyId={familyId}
              parentMembershipId={parentMembershipId}
              refreshKey={`all-${flow.flowDate}-${inboxTick}`}
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
              onOpenMode={() => {
                setInboxAllOpen(false);
                setModeSheetOpen(true);
              }}
            />
            <button
              type="button"
              className="pill is-soft"
              onClick={() => setInboxAllOpen(false)}
            >
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      {verifyPreview ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setVerifyPreview(null)}
        >
          <div
            className="sheet ph-verify-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Xem trước rồi xác nhận"
            onClick={(e) => e.stopPropagation()}
          >
            {(() => {
              const item = verifyPreview;
              const who = item.memberName?.trim() || childShort;
              const isStudy = item.commitmentKind === 'study_focus';
              const needsEvidence =
                isStudy && item.evidenceSatisfied === false;
              const photo = withEvidenceAuth(item.evidenceUrl);
              const queue = awaitingVerifyItems;
              const idx = Math.max(
                0,
                queue.findIndex((c) => c.id === item.id),
              );
              const queuePos = queue.some((c) => c.id === item.id) ? idx + 1 : 1;
              const queueTotal = Math.max(queue.length, 1);
              return (
                <>
                  <div className="ph-verify-top">
                    <p className="ph-verify-eyebrow">Xem trước · rồi mới xác nhận</p>
                    {queue.length > 1 ? (
                      <button
                        type="button"
                        className="ph-verify-queue-btn"
                        onClick={() => setVerifyListOpen((v) => !v)}
                      >
                        {verifyListOpen
                          ? 'Ẩn danh sách'
                          : `${queuePos}/${queueTotal} · Xem các mục cần duyệt`}
                      </button>
                    ) : null}
                  </div>
                  {verifyListOpen && queue.length > 1 ? (
                    <ul className="ph-verify-queue" aria-label="Các mục cần duyệt">
                      {queue.map((c, i) => {
                        const cWho = c.memberName?.trim() || childShort;
                        const on = c.id === item.id;
                        return (
                          <li key={c.id}>
                            <button
                              type="button"
                              className={`ph-verify-queue-item${on ? ' is-on' : ''}`}
                              onClick={() => {
                                setVerifyPreview(c);
                                setVerifyListOpen(false);
                              }}
                            >
                              <strong>
                                {i + 1}. {c.title}
                              </strong>
                              <em>
                                {cWho}
                                {c.commitmentKind === 'study_focus' ? ' · 📚 Học' : ''}
                                {c.evidenceUrl ? ' · có ảnh' : ' · chưa ảnh'}
                              </em>
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  ) : null}
                  <h2>{item.title}</h2>
                  <p className="ph-verify-meta">
                    <span aria-hidden>
                      {avatarEmoji(inferGenderFromName(who), 'child')}
                    </span>{' '}
                    {who}
                    {item.windowStart || item.windowEnd
                      ? ` · ${formatWindow(item.windowStart, item.windowEnd) ?? ''}`
                      : ''}
                  </p>
                  {isStudy ? (
                    <span className="ph-verify-chip">📚 Cam kết học</span>
                  ) : null}
                  {photo ? (
                    <a
                      className="ph-verify-photo"
                      href={photo}
                      target="_blank"
                      rel="noreferrer"
                    >
                      <img src={photo} alt={`Ảnh ${who} gửi — ${item.title}`} />
                      <em>Chạm để xem ảnh lớn</em>
                    </a>
                  ) : (
                    <div className="ph-verify-empty">
                      <strong>Chưa có ảnh đính kèm</strong>
                      <p>
                        {needsEvidence
                          ? 'Con đã báo xong. Xác nhận nghĩa là bạn tin lời cam kết — sao mới được cộng.'
                          : 'Không bắt buộc ảnh với việc này. Bạn vẫn có thể xác nhận nếu đã kiểm tra ngoài đời.'}
                      </p>
                    </div>
                  )}
                  {item.evidenceGateLabelVi ? (
                    <p className="ph-verify-gate">{item.evidenceGateLabelVi}</p>
                  ) : null}
                  {needsEvidence ? (
                    <fieldset className="ph-verify-checklist">
                      <legend>Xác nhận bằng chứng (bắt buộc)</legend>
                      <label>
                        <input
                          type="checkbox"
                          checked={verifyCheckTodays}
                          onChange={(e) => setVerifyCheckTodays(e.target.checked)}
                        />
                        Đây là bài / vở / màn hình học <strong>hôm nay</strong>
                      </label>
                      <label>
                        <input
                          type="checkbox"
                          checked={verifyCheckWindow}
                          onChange={(e) => setVerifyCheckWindow(e.target.checked)}
                        />
                        Con làm <strong>trong khung cam kết</strong> (hoặc gần đúng)
                      </label>
                      <label>
                        <input
                          type="checkbox"
                          checked={verifyCheckMatch}
                          onChange={(e) => setVerifyCheckMatch(e.target.checked)}
                        />
                        Nội dung <strong>khớp</strong> việc trên cam kết
                      </label>
                      {item.studyDurationMet === false ? (
                        <label className="ph-verify-duration">
                          <input
                            type="checkbox"
                            checked={verifyOverrideDuration}
                            onChange={(e) => setVerifyOverrideDuration(e.target.checked)}
                          />
                          Chưa đủ ~{item.studyMinDurationMinutes ?? '?'} phút — tôi vẫn xác nhận
                          (ghi nhận vượt thời lượng)
                        </label>
                      ) : item.expectedDurationMinutes ? (
                        <p className="muted ph-verify-duration-ok">
                          Thời lượng tối thiểu ~{item.studyMinDurationMinutes} phút
                          {item.startedAt ? ' · đã bắt đầu' : ' · chưa bấm bắt đầu'}
                        </p>
                      ) : null}
                    </fieldset>
                  ) : null}
                  {needsEvidence ? (
                    <fieldset className="ph-verify-reject">
                      <legend>Nếu chưa đạt — chọn lý do gửi lại</legend>
                      {EVIDENCE_REJECT_REASONS.map((r) => (
                        <label key={r.code}>
                          <input
                            type="radio"
                            name={`verify-reject-${item.id}`}
                            checked={verifyRejectReason === r.code}
                            onChange={() => {
                              setVerifyRejectReason(r.code);
                              setVerifyError(null);
                            }}
                          />
                          {r.labelVi}
                        </label>
                      ))}
                    </fieldset>
                  ) : null}
                  {verifyError ? (
                    <p className="ph-verify-error" role="alert">
                      {verifyError}
                    </p>
                  ) : null}
                  <div className="ph-verify-actions">
                    <button
                      type="button"
                      className="pill is-soft"
                      onClick={() => {
                        setVerifyPreview(null);
                        setVerifyListOpen(false);
                      }}
                    >
                      Để sau
                    </button>
                    {needsEvidence ? (
                      <button
                        type="button"
                        className="btn ph-verify-reject-btn"
                        disabled={
                          !verifyRejectReason ||
                          rejectingId === item.id ||
                          verifyingId === item.id ||
                          busyId === item.id
                        }
                        onClick={() => void rejectEvidenceItem(item)}
                      >
                        {rejectingId === item.id ? 'Đang gửi…' : 'Yêu cầu gửi lại'}
                      </button>
                    ) : null}
                    <button
                      type="button"
                      className="btn btn-primary"
                      disabled={
                        busyId === item.id ||
                        verifyingId === item.id ||
                        rejectingId === item.id ||
                        (needsEvidence &&
                          (!verifyCheckTodays ||
                            !verifyCheckWindow ||
                            !verifyCheckMatch ||
                            (item.studyDurationMet === false && !verifyOverrideDuration)))
                      }
                      onClick={() => void verifyItem(item).catch(() => undefined)}
                    >
                      {busyId === item.id || verifyingId === item.id
                        ? 'Đang xác nhận…'
                        : needsEvidence
                          ? queue.length > 1
                            ? 'Xác nhận · mục tiếp'
                            : 'Xác nhận cam kết học'
                          : queue.length > 1
                            ? 'Xác nhận · mục tiếp'
                            : 'Xác nhận & duyệt sao'}
                    </button>
                  </div>
                </>
              );
            })()}
          </div>
        </div>
      ) : null}

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
            <h2>
              Lịch sử đổi quà{treasureChildName ? ` · ${treasureChildName}` : ''}
            </h2>
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
              {' · '}
              {resolvedCoach.scopeLabelVi}
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
                  <p className="muted ph-coach-based" data-playbook={tip.playbookId || undefined}>
                    Vì sao Famixa nói vậy: {tip.basedOn}
                  </p>
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

      {weekReviewOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setWeekReviewOpen(false)}
        >
          <div
            className="sheet ph-sibling-nudge-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Xem lại tuần nhà mình"
            onClick={(e) => e.stopPropagation()}
          >
            <header className="ph-nudge-head">
              <span className="ph-nudge-head-ico" aria-hidden>
                📖
              </span>
              <div>
                <h2>Tuần này nhà mình</h2>
                {weeklyStory ? (
                  <p>
                    {weeklyStory.from.slice(5)} → {weeklyStory.to.slice(5)} · đọc lại lời thật
                  </p>
                ) : (
                  <p>Đọc lại lời bố mẹ, anh chị và cảm ơn trong tuần.</p>
                )}
              </div>
              <button
                type="button"
                className="ph-nudge-close"
                aria-label="Đóng"
                onClick={() => setWeekReviewOpen(false)}
              >
                ✕
              </button>
            </header>
            {weekReviewLoading ? (
              <p className="muted">Đang tải lời tuần này…</p>
            ) : weekReviewError ? (
              <p className="banner-error" role="alert">
                {weekReviewError}
              </p>
            ) : weekReviewMoments.length === 0 ? (
              <p className="muted">
                Tuần này chưa ghi lời gắn kết — gửi một lời tới con rồi xem lại ở đây.
              </p>
            ) : (
              <div className="ph-week-review-list">
                {weekReviewMoments.map((m) => (
                  <article key={m.id} className="ph-week-review-card">
                    <p className="ph-week-review-kind">
                      <span aria-hidden>{m.icon}</span> {m.kindLabel}
                    </p>
                    <p className="ph-week-review-title">{m.titleVi}</p>
                    {m.bodyVi ? <p className="ph-week-review-body">{m.bodyVi}</p> : null}
                    <p className="ph-week-review-at">{m.at.slice(0, 16).replace('T', ' · ')}</p>
                  </article>
                ))}
              </div>
            )}
            <div className="ph-nudge-actions">
              <button
                type="button"
                className="ph-nudge-btn is-primary"
                onClick={() => setWeekReviewOpen(false)}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {voiceSheetOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => !voiceBusy && setVoiceSheetOpen(false)}
        >
          <div
            className="sheet ph-sibling-nudge-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={voiceTargetKind === 'adult' ? 'Gửi lời tới người cùng chăm' : 'Gửi lời tới con'}
            onClick={(e) => e.stopPropagation()}
          >
            <header className="ph-nudge-head">
              <span className="ph-nudge-head-ico" aria-hidden>
                {parentVoiceIcon(voiceTemplate)}
              </span>
              <div>
                <h2>
                  {voiceTargetKind === 'adult'
                    ? 'Gửi lời tới người cùng chăm'
                    : activeRelTrigger && isBirthdayWishTrigger(activeRelTrigger.code)
                      ? 'Chúc sinh nhật'
                      : 'Gửi lời tới con'}
                </h2>
                <p>Bạn là người nói — Famixa chỉ chuyển lời.</p>
              </div>
              <button
                type="button"
                className="ph-nudge-close"
                aria-label="Đóng"
                disabled={voiceBusy}
                onClick={() => setVoiceSheetOpen(false)}
              >
                ✕
              </button>
            </header>

            <div className="ph-nudge-form">
              <label className="ph-nudge-field">
                <span className="ph-nudge-label">Gửi tới</span>
                <span className="ph-nudge-select">
                  <select
                    value={voiceToId}
                    onChange={(e) => setVoiceToId(e.target.value)}
                    disabled={voiceBusy}
                  >
                    <option value="">
                      {voiceTargetKind === 'adult' ? 'Chọn người…' : 'Chọn con…'}
                    </option>
                    {voiceTargetKind === 'adult'
                      ? adultOptions.map((a) => (
                          <option key={a.id} value={a.id}>
                            {a.displayName}
                          </option>
                        ))
                      : childOptions.map((c) => (
                          <option key={c.key} value={c.key}>
                            {c.name}
                          </option>
                        ))}
                  </select>
                  <i aria-hidden>▾</i>
                </span>
              </label>

              {voiceTargetKind === 'adult' ? (
                <fieldset className="ph-sibling-nudge-templates">
                  <legend className="ph-nudge-label">Kiểu lời</legend>
                  {ADULT_VOICE_TEMPLATES.map((opt) => (
                    <label
                      key={opt.code}
                      className={`ph-nudge-option${voiceTemplate === opt.code ? ' is-on' : ''}`}
                    >
                      <input
                        type="radio"
                        name="adult-voice-template"
                        checked={voiceTemplate === opt.code}
                        disabled={voiceBusy}
                        onChange={() => {
                          setVoiceTemplate(opt.code);
                          const toName =
                            adultOptions.find((a) => a.id === voiceToId)?.displayName || 'bạn';
                          const short = shortMemberName(toName);
                          if (opt.code === 'thanks_partner') {
                            setVoiceDraft(`${short} ơi, cảm ơn hôm nay mình cùng giữ nhà nhé.`);
                          } else if (opt.code === 'help_offer') {
                            setVoiceDraft(`${short} ơi, tối nay để mình phụ một việc nhé?`);
                          } else {
                            setVoiceDraft(`${short} ơi, hôm nay mình nhớ đến nhau.`);
                          }
                        }}
                      />
                      <span>
                        <b>{opt.labelVi}</b>
                        <em>{opt.hint}</em>
                      </span>
                    </label>
                  ))}
                </fieldset>
              ) : null}

              {activeRelTrigger && isBirthdayWishTrigger(activeRelTrigger.code) ? (
                <div className="ph-soft-cal-opts" role="group" aria-label="Chọn lời chúc">
                  {BIRTHDAY_PICKER_OPTIONS.map((o) => (
                    <button
                      key={o.code}
                      type="button"
                      className={`ph-soft-cal-chip${birthdayPick === o.code ? ' is-on' : ''}`}
                      disabled={voiceBusy}
                      onClick={() => applyBirthdayPick(o.code)}
                    >
                      {o.labelVi}
                    </button>
                  ))}
                </div>
              ) : null}

              <label className="ph-nudge-field">
                <span className="ph-nudge-label">Lời của bạn</span>
                <textarea
                  className="ph-voice-textarea"
                  rows={3}
                  maxLength={380}
                  value={voiceDraft}
                  disabled={voiceBusy}
                  onChange={(e) => setVoiceDraft(e.target.value)}
                />
              </label>
            </div>

            {voiceError ? (
              <p className="banner-error" role="alert">
                {voiceError}
              </p>
            ) : null}

            <div className="ph-nudge-actions">
              <button
                type="button"
                className="ph-nudge-btn is-ghost"
                disabled={voiceBusy}
                onClick={() => setVoiceSheetOpen(false)}
              >
                Để sau
              </button>
              <button
                type="button"
                className="ph-nudge-btn is-primary"
                disabled={voiceBusy || !voiceToId || !voiceDraft.trim()}
                onClick={() => void submitParentVoice()}
              >
                {voiceBusy ? 'Đang gửi…' : 'Gửi lời'}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {siblingNudgeOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => !nudgeBusy && setSiblingNudgeOpen(false)}
        >
          <div
            className="sheet ph-sibling-nudge-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Mời anh chị nhắc em"
            onClick={(e) => e.stopPropagation()}
          >
            <header className="ph-nudge-head">
              <span className="ph-nudge-head-ico" aria-hidden>
                🤝
              </span>
              <div>
                <h2>Mời anh/chị nhắc em</h2>
                <p>Lời nhắc cố định, giọng cổ vũ — anh/chị không duyệt thay bố mẹ.</p>
              </div>
              <button
                type="button"
                className="ph-nudge-close"
                aria-label="Đóng"
                disabled={nudgeBusy}
                onClick={() => setSiblingNudgeOpen(false)}
              >
                ✕
              </button>
            </header>

            <div className="ph-nudge-form">
              <label className="ph-nudge-field">
                <span className="ph-nudge-label">Anh/chị gửi</span>
                <span className="ph-nudge-select">
                  <select
                    value={nudgeFromId}
                    onChange={(e) => setNudgeFromId(e.target.value)}
                    disabled={nudgeBusy}
                  >
                    <option value="">Chọn con đã xong việc…</option>
                    {nudgeCandidates
                      .filter((c) => c.canInvite)
                      .map((c) => (
                        <option key={c.memberId} value={c.memberId}>
                          {c.displayName}
                          {c.missionsComplete ? ' · đã xong' : ''}
                        </option>
                      ))}
                  </select>
                  <i aria-hidden>▾</i>
                </span>
                <span className="ph-nudge-hint">
                  Chỉ hiện con đã xong việc hoặc lớn hơn — tránh nhắc kiểu trách móc.
                </span>
              </label>

              <label className="ph-nudge-field">
                <span className="ph-nudge-label">Em nhận</span>
                <span className="ph-nudge-select">
                  <select
                    value={nudgeToId}
                    onChange={(e) => setNudgeToId(e.target.value)}
                    disabled={nudgeBusy}
                  >
                    <option value="">Chọn con sẽ nhận lời nhắc…</option>
                    {nudgeCandidates
                      .filter((c) => c.memberId !== nudgeFromId)
                      .map((c) => (
                        <option key={c.memberId} value={c.memberId}>
                          {c.displayName}
                        </option>
                      ))}
                  </select>
                  <i aria-hidden>▾</i>
                </span>
              </label>
            </div>

            <fieldset className="ph-sibling-nudge-templates">
              <legend className="ph-nudge-label">Chọn lời nhắc</legend>
              {NUDGE_TEMPLATE_OPTIONS.map((opt) => (
                <label
                  key={opt.code}
                  className={`ph-nudge-option${nudgeTemplate === opt.code ? ' is-on' : ''}`}
                >
                  <input
                    type="radio"
                    name="nudge-template"
                    checked={nudgeTemplate === opt.code}
                    onChange={() => setNudgeTemplate(opt.code)}
                    disabled={nudgeBusy}
                  />
                  <span className="ph-nudge-radio" aria-hidden />
                  <span className="ph-nudge-option-text">
                    <strong>{opt.title}</strong>
                    <em>{opt.hint}</em>
                  </span>
                </label>
              ))}
            </fieldset>

            <div className="ph-nudge-preview">
              <span className="ph-nudge-preview-label">Em sẽ thấy</span>
              <p>“{nudgePreview}”</p>
            </div>

            {nudgeError ? (
              <p className="banner-error" role="alert">
                {nudgeError}
              </p>
            ) : null}

            <div className="ph-nudge-actions">
              <button
                type="button"
                className="ph-nudge-btn is-ghost"
                disabled={nudgeBusy}
                onClick={() => setSiblingNudgeOpen(false)}
              >
                Huỷ
              </button>
              <button
                type="button"
                className="ph-nudge-btn is-primary"
                disabled={nudgeBusy || !nudgeFromId || !nudgeToId}
                onClick={() => void submitSiblingNudge()}
              >
                {nudgeBusy ? 'Đang gửi…' : 'Gửi lời nhắc'}
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
