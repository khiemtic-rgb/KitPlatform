import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import {
  SKIP_REASON_OPTIONS,
  skipReasonLabel,
  uploadCommitmentEvidence,
  getMorningNote,
  type MorningNote,
  fetchChildGratitude,
  sendChildGratitude,
  fetchRewardCatalog,
  fetchRewardRedemptions,
  fetchTeamUnlocks,
  fetchTeamNudges,
  fetchTeamNudgeFromCandidates,
  createTeamNudge,
  sendTeamNudge,
  ackTeamNudge,
  fetchRelationshipTriggers,
  fetchParentVoice,
  ackParentVoice,
  fetchEveningCircle,
  answerEveningCircle,
  fetchWeeklyStory,
  fetchFamilyMemories,
  createFamilyMemory,
  createChildRequest,
  fetchDayFlow,
  submitChildVoiceWeek,
  type RelationshipTrigger,
  type ParentVoiceMessage,
  type EveningCircle,
  type WeeklyStory,
  type FamilyMemoryEntry,
  fetchMemberMood,
  upsertMemberMood,
  redeemReward,
  fetchScreenWallet,
  fetchChildRequests,
  type RewardCatalogItem,
  type RewardRedemption,
  type TeamUnlock,
  type TeamNudge,
  type TeamNudgeCandidate,
  type TeamNudgeTemplate,
  type AccountabilityDayGlance,
  type DayFlowCommitment,
  type SkipReasonCode,
  type ScreenWallet,
  type ChildRequest,
} from '@/shared/api/family-os.api';
import { getApiErrorMessage } from '@/shared/billing/capability-error';
import { ChildScreenRequestSheet } from '@/modules/flow/ChildScreenRequestSheet';
import { ChildMissionRequestSheet } from '@/modules/flow/ChildMissionRequestSheet';
import { KidMomentSheet } from '@/modules/flow/KidMomentSheet';
import { KidHomeHub } from '@/modules/flow/KidHomeHub';
import { DailyMirrorEmptyPanel } from '@/shared/ui/DailyMirrorEmptyPanel';
import { KidPlanHub } from '@/modules/flow/KidPlanHub';
import { KidTreasureHub } from '@/modules/flow/KidTreasureHub';
import { KidDiaryHub } from '@/modules/flow/KidDiaryHub';
import { KidFamilyHub } from '@/modules/flow/KidFamilyHub';
import { type TodayOpenCtaEvent } from '@/modules/flow/TodayOpenStack';
import {
  buildMemoryYarn,
  buildPendingActions,
  buildSeenSignals,
  buildWarmthPulse,
  dismissWarmth,
  isRitualDone,
  markRitualDone,
} from '@/modules/flow/todayOpenSequence';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';
import {
  avatarEmoji,
  avatarToneClass,
  inferGenderFromName,
} from '@/shared/ui/avatarGender';
import {
  canCompleteNow,
  countdownUntilWindow,
  earlyCompleteBlockReason,
} from '@/shared/flow/commitment-timing';
import {
  formatLateDuration,
  formatLateDurationCaption,
  normalizeLateStarLabelVi,
  stripLateStarSuffixVi,
} from '@/shared/flow/late-duration';
import {
  buildFamilyMemories,
  formatMemoryDate,
  isGardenBloomMemory,
  isMovieNightUnlock,
  matchesKidMemoryFilter,
  FAMILY_MEMORY_EMPTY,
  FAMILY_MEMORY_VISIBLE,
  type FamilyMemory,
  type KidMemoryFilter,
} from '@/shared/flow/family-memories';
import { FAMILY_MOODS, moodIndexFromCode } from '@/shared/flow/family-moods';
import {
  CHILD_VOICE_DEFAULTS,
  CHILD_VOICE_HARDEST_OPTIONS,
  CHILD_VOICE_WANT_OPTIONS,
  CHILD_VOICE_WEEK_COPY,
} from '@/shared/flow/child-voice-week';
import { NUDGE_TEMPLATE_OPTIONS, isSiblingComboUnlock, nudgeMessagePreview } from '@/modules/flow/teamPlay';
import {
  isCheerSiblingTrigger,
  isThankParentTrigger,
  parentVoiceIcon,
  parentVoiceKindLabelVi,
  primaryRelationshipTrigger,
} from '@/modules/flow/memberPersonalize';
import { isParentVerified } from '@/shared/nudge/nudge-stats';
import { FamilyChallengeCard } from '@/modules/flow/FamilyChallengeCard';
import {
  capitalizeParentRole,
  diaryTaskNote,
  taskKindOf,
  type ParentRole,
} from '@/shared/voice/family-voice';
import {
  careAgeBandFromDob,
  cheerOfferCopyForBand,
  cheerPreviewAudienceVi,
  livingFoxyForBand,
  parentVoiceHomeLineForBand,
  tasksFoxyBannerVi,
} from '@/shared/care/care-age-tone';

const TRUST_CHILD_RE =
  /đánh răng|ăn sáng|ăn trưa|ăn tối|uống sữa|đi ngủ|ngủ|đi học|mặc|đồng phục|tắm|rửa mặt|rửa tay/i;
const NEED_APPROVAL_RE =
  /bài tập|học|dọn|phòng|luyện|đàn|piano|gấp|quần áo|đọc sách|viết|ôn|balo|cặp|chơi đàn/i;

type KidMissionUxState = 'open' | 'awaiting_check' | 'done' | 'upcoming' | 'skipped';

function needsParentApproval(item: DayFlowCommitment): boolean {
  if (NEED_APPROVAL_RE.test(item.title)) return true;
  if (TRUST_CHILD_RE.test(item.title)) return false;
  return Boolean(item.evidenceUrl);
}

function isStudyFocus(item: DayFlowCommitment): boolean {
  return item.commitmentKind === 'study_focus';
}

/** Soft/hard study that still needs photo / retrieval / parent verify for stars. */
function studyNeedsEvidence(item: DayFlowCommitment): boolean {
  return isStudyFocus(item) && item.evidenceSatisfied === false;
}

function kidMissionUxState(
  item: DayFlowCommitment,
  flowDate: string,
): KidMissionUxState {
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

type DayPart = 'all' | 'morning' | 'afternoon' | 'evening' | 'done';
type KidTab = 'home' | 'tasks' | 'rewards' | 'log' | 'family';
type KidHomePane = 'hub' | 'praise' | 'streak' | 'garden' | 'ask' | 'challenge';
type AchievementSheet = 'movie' | 'garden' | 'read' | null;

function requestStatusLabel(status: string): string {
  switch (status) {
    case 'pending':
      return 'Chờ bố mẹ duyệt';
    case 'approved':
      return 'Đã đồng ý';
    case 'partial':
      return 'Đồng ý một phần';
    case 'rejected':
      return 'Từ chối';
    case 'expired':
      return 'Hết hạn';
    default:
      return status;
  }
}

function requestKindLabel(req: ChildRequest): string {
  if (req.kind === 'day_mission') {
    return req.titleVi ? `Đề xuất việc «${req.titleVi}»` : 'Đề xuất việc hôm nay';
  }
  if (req.kind === 'screen_minutes' || !req.kind) {
    const mins = req.grantedMinutes ?? req.amountMinutes;
    return mins != null ? `Xin +${mins} phút màn hình` : 'Xin thêm phút màn hình';
  }
  return req.titleVi || 'Đề xuất gửi bố mẹ';
}

function EnTerm({
  en,
  vi,
  as = 'span',
}: {
  en: string;
  vi: string;
  as?: 'span' | 'strong' | 'h2' | 'em';
}) {
  const Tag = as;
  return (
    <span className="kv2-en-term">
      <Tag className="kv2-en-term-main">{en}</Tag>
      <em className="kv2-en-term-vi">{vi}</em>
    </span>
  );
}

function formatWindow(start?: string, end?: string): string | null {
  if (!start && !end) return null;
  const clean = (value?: string) => (value ? value.slice(0, 5) : '');
  if (start && end) return `${clean(start)} – ${clean(end)}`;
  return clean(start || end);
}

function hourOf(item: DayFlowCommitment): number | null {
  const raw = item.windowStart || item.windowEnd;
  if (!raw) return null;
  const h = Number(raw.slice(0, 2));
  return Number.isFinite(h) ? h : null;
}

function dayPartOf(item: DayFlowCommitment): 'morning' | 'afternoon' | 'evening' {
  const h = hourOf(item);
  if (h == null) return 'afternoon';
  if (h < 12) return 'morning';
  if (h < 17) return 'afternoon';
  return 'evening';
}

function taskIcon(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('rang')) return '🪥';
  if (t.includes('ăn sáng')) return '🍳';
  if (t.includes('ăn trưa') || t.includes('ăn tối') || t.includes('cơm')) return '🍱';
  if (t.includes('uống sữa') || t.includes('sữa')) return '🥛';
  if (t.includes('ăn') || t.includes('bữa')) return '🍳';
  if (t.includes('đồng phục') || t.includes('mặc')) return '👕';
  if (t.includes('đi học') || t.includes('đến trường')) return '🎒';
  if (t.includes('cặp') || t.includes('balo') || t.includes('chuẩn bị')) return '🎒';
  if (t.includes('bài tập') || t.includes('học')) return '📝';
  if (t.includes('đọc') || t.includes('sách') || t.includes('kể chuyện')) return '📚';
  if (t.includes('đi ngủ') || t.includes('ngủ')) return '😴';
  if (t.includes('tắm')) return '🛁';
  if (t.includes('rửa mặt') || t.includes('rửa tay')) return '🧼';
  if (t.includes('rửa')) return '🚿';
  if (t.includes('dọn') || t.includes('phòng') || t.includes('gấp')) return '🧹';
  if (t.includes('màn hình') || t.includes('tắt')) return '📱';
  if (t.includes('đàn') || t.includes('piano') || t.includes('nhạc')) return '🎹';
  if (t.includes('thể dục') || t.includes('chạy') || t.includes('bơi')) return '⚽';
  if (t.includes('giúp')) return '🤝';
  if (t.includes('tưới') || t.includes('cây')) return '🪴';
  return '🌟';
}

/** Soft pastel chip behind lively emoji icons */
function taskIconTone(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('rang') || t.includes('rửa')) return 'sky';
  if (t.includes('ăn') || t.includes('cơm') || t.includes('sữa') || t.includes('bữa')) return 'peach';
  if (t.includes('mặc') || t.includes('đồng phục')) return 'mint';
  if (t.includes('học') || t.includes('cặp') || t.includes('balo') || t.includes('bài')) return 'lilac';
  if (t.includes('đọc') || t.includes('sách')) return 'lemon';
  if (t.includes('ngủ')) return 'indigo';
  if (t.includes('dọn') || t.includes('giúp')) return 'green';
  return 'lilac';
}

function gardenPlant(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('rang')) return '🪴';
  if (t.includes('đọc') || t.includes('sách') || t.includes('kể chuyện')) return '🌳';
  if (t.includes('giúp') || t.includes('mẹ') || t.includes('bố')) return '🌸';
  if (t.includes('bài') || t.includes('học')) return '🌱';
  if (t.includes('ăn')) return '🌻';
  if (t.includes('cặp') || t.includes('balo') || t.includes('chuẩn bị')) return '🌼';
  if (t.includes('ngủ')) return '🌙';
  if (t.includes('dọn') || t.includes('tưới')) return '🌿';
  if (t.includes('tắm') || t.includes('rửa')) return '💧';
  return '🪴';
}

type GardenPlantMood = 'healthy' | 'neutral' | 'wilted';

const GARDEN_WILTED_PLANT: Record<string, string> = {
  '🌸': '🥀',
  '🌻': '🥀',
  '🌼': '🥀',
  '🌱': '🥀',
  '🪴': '🥀',
  '🌳': '🍂',
  '🌿': '🍂',
  '🌙': '🌑',
};

function gardenPlantMood(stars: number): GardenPlantMood {
  if (stars < 0) return 'wilted';
  if (stars === 0) return 'neutral';
  return 'healthy';
}

function gardenPlantForStars(
  title: string,
  stars: number,
): { plant: string; mood: GardenPlantMood } {
  const base = gardenPlant(title);
  const mood = gardenPlantMood(stars);
  if (mood === 'wilted') {
    return { plant: GARDEN_WILTED_PLANT[base] ?? '🥀', mood };
  }
  return { plant: base, mood };
}

/** Overlay badge on the pot face (mockup: lock / book / heart). */
function gardenBadge(title: string, commitmentKind?: string): string {
  if (commitmentKind === 'study_focus') return '📚';
  const t = title.toLowerCase();
  if (t.includes('đọc') || t.includes('sách')) return '📖';
  if (t.includes('giúp') || t.includes('mẹ') || t.includes('bố')) return '💖';
  if (t.includes('ăn')) return '🍳';
  if (t.includes('học') || t.includes('bài')) return '📝';
  if (t.includes('đánh răng') || t.includes('rang')) return '🪥';
  if (t.includes('ngủ')) return '😴';
  return '⭐';
}

/** Actual / pending star outcome for a completed pot — never inflate late work to full starReward. */
function gardenStarsForCommitment(c: DayFlowCommitment): number {
  if (c.starDelta != null) return c.starDelta;
  if (c.projectedStarDelta != null) return c.projectedStarDelta;
  if (c.isLateDone) return 0;
  if (c.starReward != null && c.starReward > 0) return c.starReward;
  return commitmentStars(c);
}

function praisePrideLine(
  short: string,
  c: DayFlowCommitment,
  parentRole: ParentRole,
  flowDate?: string,
): string {
  const follow = '';
  return stablePick(praiseSeed(c, flowDate), PARENT_PRAISE_ON_TIME)(
    short,
    c.title,
    follow,
    parentRole,
  );
}

function praiseEncouragementLine(
  short: string,
  c: DayFlowCommitment,
  parentRole: ParentRole,
  nextTitle?: string,
  flowDate?: string,
): string {
  const follow = lateEncourageFollow(c.title, nextTitle, 'praise');
  return stablePick(`${praiseSeed(c, flowDate)}:late`, PARENT_ENCOURAGE_LATE)(
    short,
    c.title,
    follow,
    parentRole,
  );
}

function minutesUntil(item: DayFlowCommitment, now = new Date()): string | null {
  const raw = item.windowStart || item.windowEnd;
  if (!raw) return null;
  const [h, m] = raw.slice(0, 5).split(':').map(Number);
  if (![h, m].every(Number.isFinite)) return null;
  const target = new Date(now);
  target.setHours(h, m, 0, 0);
  const diff = Math.round((target.getTime() - now.getTime()) / 60_000);
  if (item.reminderState === 'overdue') return 'Hơi trễ rồi — làm ngay nào';
  if (item.reminderState === 'due_now') return 'Đến giờ rồi!';
  if (diff <= 0) return 'Đến giờ rồi!';
  if (diff < 60) return `Còn ${diff} phút`;
  const hrs = Math.floor(diff / 60);
  const mins = diff % 60;
  return mins > 0 ? `Còn ${hrs} giờ ${mins} phút` : `Còn ${hrs} giờ`;
}

function pickNextMission(pending: DayFlowCommitment[]): DayFlowCommitment | null {
  if (pending.length === 0) return null;
  const rank = (c: DayFlowCommitment) => {
    if (c.reminderState === 'overdue') return 0;
    if (c.reminderState === 'due_now') return 1;
    return 2;
  };
  return [...pending].sort((a, b) => {
    const d = rank(a) - rank(b);
    if (d !== 0) return d;
    const aw = a.windowStart || a.windowEnd || '99:99';
    const bw = b.windowStart || b.windowEnd || '99:99';
    return aw.localeCompare(bw);
  })[0];
}

function kidSkipLabel(code?: string): string {
  switch (code) {
    case 'forgot':
      return 'Mình quên mất';
    case 'busy':
      return 'Mình đang bận việc khác';
    case 'need_help':
      return 'Mình cần bố mẹ giúp';
    case 'not_ready':
      return 'Mình chưa sẵn sàng';
    case 'sick':
      return 'Mình hơi mệt';
    case 'other':
      return 'Có chuyện khác';
    default:
      return skipReasonLabel(code) ?? 'Chưa làm được';
  }
}

function formatLongDate(d = new Date()): string {
  const map = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const yyyy = d.getFullYear();
  return `${map[d.getDay()]}, ${dd}/${mm}/${yyyy}`;
}

function shortChildName(name: string): string {
  return name.trim().split(/\s+/).pop() || name;
}

/** Stable index 0..n-1 — same seed always picks the same phrase. */
function stablePickIndex(seed: string, poolLength: number): number {
  if (poolLength <= 0) return 0;
  let h = 0;
  for (let i = 0; i < seed.length; i++) {
    h = (Math.imul(31, h) + seed.charCodeAt(i)) | 0;
  }
  return Math.abs(h) % poolLength;
}

function stablePick<T>(seed: string, pool: readonly T[]): T {
  return pool[stablePickIndex(seed, pool.length)];
}

function praiseSeed(c: DayFlowCommitment, flowDate?: string): string {
  return `${flowDate ?? ''}:${c.id}:${c.title}`;
}

function lateEncourageFollow(
  title: string,
  nextTitle?: string,
  mode: 'praise' | 'journal' = 'journal',
): string {
  if (nextTitle && nextTitle !== title) {
    return ` — tiếp theo «${nextTitle}» đúng giờ nhé!`;
  }
  if (mode === 'praise') {
    return ` — mai làm «${title}» đúng giờ hơn nhé!`;
  }
  return ` — lần sau thử đúng giờ hơn nhé!`;
}

type PraiseLine = (
  short: string,
  title: string,
  follow: string,
  parent: ParentRole,
) => string;

const PARENT_PRAISE_ON_TIME: PraiseLine[] = [
  (short, title, _f, parent) =>
    `${capitalizeParentRole(parent)} rất tự hào vì ${short} chủ động hoàn thành «${title}»! ❤️`,
  (short, title, _f, parent) =>
    `Giỏi quá ${short} ơi — «${title}» đúng giờ luôn! ${capitalizeParentRole(parent)} tự hào lắm! 💪`,
  (short, title, _f, parent) =>
    `${short} làm «${title}» thật gọn gàng — ${parent} vui lắm! ❤️`,
  (short, title) => `Hay quá! ${short} hoàn thành «${title}» đúng lúc rồi! 🌟`,
  (short, title, _f, parent) =>
    `${capitalizeParentRole(parent)} tự hào lắm vì ${short} giữ đúng giờ với «${title}»! ❤️`,
];

const PARENT_ENCOURAGE_LATE: PraiseLine[] = [
  (short, _title, follow) =>
    `Cố gắng con nhé ${short} — lần sau đúng giờ hơn nha!${follow}`,
  (short, _title, follow) => `${short} làm xong rồi đó — mai tranh thủ sớm hơn nhé!${follow}`,
  (short, _title, follow, parent) =>
    `${capitalizeParentRole(parent)} thấy ${short} đã cố — giờ giấc lần sau sẽ mượt hơn!${follow}`,
  (short, title, follow) =>
    `${short} hoàn thành «${title}» rồi — cố thêm chút nữa về giờ nhé!${follow}`,
  (short, _title, follow, parent) =>
    `${capitalizeParentRole(parent)} vẫn ủng hộ ${short}${follow} 💛`,
];

const JOURNAL_ON_TIME_GENERIC: PraiseLine[] = [
  (short, title, _f, parent) =>
    `${capitalizeParentRole(parent)} rất vui vì ${short} đã cố gắng với «${title}»!`,
  (short, title, _f, parent) =>
    `${short} làm «${title}» thật chăm chỉ — ${parent} tự hào! 💪`,
  (short, title) => `Giỏi quá ${short}! «${title}» xong đúng lúc rồi! 🌟`,
  (short, title, _f, parent) =>
    `${capitalizeParentRole(parent)} thấy ${short} rất ngoan với «${title}» hôm nay! ❤️`,
];

const BEAUTIFUL_DAY_ON_TIME: Array<(short: string, parent: ParentRole) => string> = [
  (short, parent) =>
    `Hôm nay nhà mình thật tuyệt — ${parent} ghi nhận ${short} đã cố gắng! ❤️`,
  (short, parent) =>
    `Ngày đẹp của ${short}! ${capitalizeParentRole(parent)} vui lắm vì con giữ đúng giờ! 🌟`,
  (short) => `${short} làm hôm nay thật xuất sắc — cả nhà đều tự hào! ❤️`,
];

const BEAUTIFUL_DAY_LATE_ONLY: Array<(short: string, parent: ParentRole) => string> = [
  (short, parent) =>
    `${capitalizeParentRole(parent)} thấy ${short} vẫn cố gắng hôm nay — mai mình làm đúng giờ hơn nhé! 💛`,
  (short) => `${short} vẫn hoàn thành việc hôm nay — mai mình bắt giờ sớm hơn nha! 💛`,
  (short, parent) =>
    `${capitalizeParentRole(parent)} biết ${short} đã cố — ngày mai giờ giấc sẽ mượt hơn! 💛`,
];

const PRAISE_FALLBACK: Array<(short: string, parent: ParentRole) => string> = [
  (short, parent) =>
    `${capitalizeParentRole(parent)} rất tự hào vì ${short} đang cố gắng mỗi ngày! ❤️`,
  (short, parent) =>
    `${capitalizeParentRole(parent)} luôn tin ${short} sẽ làm tốt hơn mỗi ngày! 💛`,
  (short, parent) =>
    `${short} ơi, ${parent} thấy con đang tiến bộ từng chút! 🌟`,
];

const STREAK_EMPTY_LATE_NOTES = [
  'Làm đúng giờ để mở ngày đẹp — Foxy tin con làm được! 💪',
  'Mai mình thử đúng giờ hơn — Foxy luôn ủng hộ con! 💪',
  'Giờ giấc sẽ mượt dần thôi — Foxy tin con làm được! 💪',
];

const STREAK_EMPTY_OPEN_NOTES = [
  'Xong hết việc đúng giờ hôm nay để bắt đầu chuỗi nhé! 💪',
  'Hoàn thành việc còn lại đúng giờ — chuỗi sẽ bắt đầu từ hôm nay! 💪',
  'Còn vài việc nữa — làm đúng giờ để mở chuỗi nhé! 💪',
];

const STREAK_EMPTY_FRESH_NOTES = [
  'Bắt đầu từ hôm nay — Foxy ở cạnh cổ vũ! 💪',
  'Hôm nay là ngày mới — mình cùng làm từng bước! 💪',
  'Mỗi ngày một chút — lời khen thật từ bố/mẹ ấm hơn! 💪',
];

const STREAK_ACTIVE_NOTES: Array<(short: string) => string> = [
  (short) => `Giữ vững nha ${short}! Foxy cổ vũ con! 💪`,
  (short) => `${short} đang làm rất tốt — tiếp tục nha! 🔥`,
  (short) => `Chuỗi đang đẹp lắm ${short} — báo bố/mẹ nghe nhé! 🌟`,
];

function onTimeCelebrateHeadlines(parent: ParentRole): string[] {
  return [
    'Giỏi quá!',
    'Tuyệt vời!',
    'Hay lắm!',
    `${capitalizeParentRole(parent)} tự hào lắm!`,
    'Xuất sắc!',
  ];
}

function lateCelebrateHeadlines(parent: ParentRole): string[] {
  return [
    'Xong rồi nhé!',
    'Cố gắng lắm!',
    `${capitalizeParentRole(parent)} thấy con đã cố!`,
    'Làm xong rồi — giỏi!',
  ];
}

const ON_TIME_CELEBRATE_SUBLINES: Array<(title: string) => string> = [
  (title) => `«${title}» xong — cả nhà gần đích hơn!`,
  (title) => `«${title}» xong rồi — nhớ kể bố/mẹ nghe nhé!`,
  (title) => `Foxy ghi nhận «${title}» — lời khen thật từ bố/mẹ ấm hơn.`,
];

const LATE_CELEBRATE_SUBLINES: Array<(title: string) => string> = [
  (title) => `«${title}» xong rồi — lần sau đúng giờ hơn nhé!`,
  (title) => `Con đã cố với «${title}» — Foxy đứng cạnh cổ vũ!`,
  (title) => `«${title}» hoàn thành — mai mình sớm hơn nha!`,
];

function celebrateHeadline(title: string, stars: number, parent: ParentRole): string {
  const pool = stars <= 0 ? lateCelebrateHeadlines(parent) : onTimeCelebrateHeadlines(parent);
  return stablePick(`${title}:${stars}:head`, pool);
}

function celebrateSubline(title: string, stars: number): string {
  const pool = stars <= 0 ? LATE_CELEBRATE_SUBLINES : ON_TIME_CELEBRATE_SUBLINES;
  return stablePick(`${title}:${stars}:sub`, pool)(title);
}

function dayPartLabel(item: DayFlowCommitment): string {
  const part = dayPartOf(item);
  if (part === 'morning') return 'Buổi sáng';
  if (part === 'evening') return 'Buổi tối';
  return 'Buổi chiều';
}

function minutesUntilExcited(item: DayFlowCommitment, now = new Date()): string | null {
  const raw = item.windowStart || item.windowEnd;
  if (!raw) return null;
  const [h, m] = raw.slice(0, 5).split(':').map(Number);
  if (![h, m].every(Number.isFinite)) return null;
  const target = new Date(now);
  target.setHours(h, m, 0, 0);
  const diff = Math.round((target.getTime() - now.getTime()) / 60_000);
  if (item.reminderState === 'overdue') return 'Hơi trễ rồi — làm ngay nào!';
  if (item.reminderState === 'due_now' || diff <= 0) return 'Đến giờ rồi!';
  if (diff < 60) return `Còn ${diff} phút nữa thôi!`;
  const hrs = Math.floor(diff / 60);
  const mins = diff % 60;
  return mins > 0 ? `Còn ${hrs} giờ ${mins} phút nữa thôi!` : `Còn ${hrs} giờ nữa thôi!`;
}

function weekdayLabel(dateIso: string): string {
  const d = new Date(`${dateIso}T12:00:00`);
  if (Number.isNaN(d.getTime())) return '?';
  const map = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
  return map[d.getDay()] ?? '?';
}

type WeekStreakDay = {
  key: string;
  label: string;
  on: boolean;
  isToday: boolean;
};

function normalizeDateIso(raw: string): string {
  return raw.trim().slice(0, 10);
}

function dateKeyFromDate(d: Date): string {
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

function findStreakAnchorIndex(
  cells: WeekStreakDay[],
  glanceByDate: Map<string, AccountabilityDayGlance>,
  todayIndex: number,
  todayBeautiful: boolean,
): number {
  if (todayIndex < 0) return -1;

  let anchor = -1;
  for (let i = todayIndex; i >= 0; i--) {
    const glance = glanceByDate.get(cells[i].key);
    const scored = glance?.isScored ?? false;
    const beautiful = glance?.isBeautifulDay ?? false;

    if (i === todayIndex && !scored && !beautiful) continue;
    if (!scored) continue;
    if (!beautiful) break;
    anchor = i;
  }

  if (anchor >= 0) return anchor;
  return todayBeautiful ? todayIndex : todayIndex - 1;
}

/** Mon–Sun strip for the week containing flowDate; streak dots trail from the latest beautiful day. */
function buildWeekStreakDays(
  flowDate: string,
  glanceDays: AccountabilityDayGlance[],
  streak: number,
  todayBeautiful: boolean,
): WeekStreakDay[] {
  const todayIso = normalizeDateIso(flowDate);
  const base = new Date(`${todayIso}T12:00:00`);
  if (Number.isNaN(base.getTime())) return [];

  const dow = base.getDay();
  const mondayOffset = dow === 0 ? -6 : 1 - dow;
  const glanceByDate = new Map(
    glanceDays.map((d) => [normalizeDateIso(d.date), d]),
  );
  const cells: WeekStreakDay[] = [];
  let todayIndex = -1;

  for (let i = 0; i < 7; i++) {
    const d = new Date(base);
    d.setDate(base.getDate() + mondayOffset + i);
    const key = dateKeyFromDate(d);
    const isToday = key === todayIso;
    if (isToday) todayIndex = i;

    cells.push({
      key,
      label: weekdayLabel(key),
      on: false,
      isToday,
    });
  }

  if (streak <= 0 || todayIndex < 0) return cells;

  const anchorIndex = findStreakAnchorIndex(cells, glanceByDate, todayIndex, todayBeautiful);
  if (anchorIndex < 0) return cells;

  const start = Math.max(0, anchorIndex - streak + 1);
  for (let i = start; i <= anchorIndex; i++) {
    cells[i].on = true;
  }

  return cells;
}

function todayGlanceForFlow(
  flowDate: string,
  glanceDays: AccountabilityDayGlance[],
): AccountabilityDayGlance | undefined {
  const key = normalizeDateIso(flowDate);
  return glanceDays.find((d) => normalizeDateIso(d.date) === key);
}

function streakEmptyCopy(
  todayGlance: AccountabilityDayGlance | undefined,
  flowDate?: string,
): {
  headline: string;
  note: string;
} {
  const late = todayGlance?.childLateDone ?? 0;
  const open = todayGlance?.childOpen ?? 0;
  const seed = `${flowDate ?? ''}:${late}:${open}`;
  if (late > 0) {
    return {
      headline: 'Chưa có chuỗi',
      note: stablePick(`${seed}:late`, STREAK_EMPTY_LATE_NOTES),
    };
  }
  if (open > 0) {
    return {
      headline: 'Chưa có chuỗi',
      note: stablePick(`${seed}:open`, STREAK_EMPTY_OPEN_NOTES),
    };
  }
  return {
    headline: 'Chưa có chuỗi',
    note: stablePick(`${seed}:fresh`, STREAK_EMPTY_FRESH_NOTES),
  };
}

function taskTip(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng')) return 'Giữ răng sạch sẽ mỗi ngày nhé!';
  if (t.includes('bài') || t.includes('học') || t.includes('toán')) return 'Cố gắng hoàn thành đúng giờ nhé!';
  if (t.includes('tưới') || t.includes('cây')) return 'Khu vườn cần nước của bạn!';
  if (t.includes('cặp') || t.includes('balo')) return 'Chuẩn bị đầy đủ trước khi đi ngủ.';
  if (t.includes('ngủ')) return 'Ngủ đủ giấc để mai tràn đầy năng lượng!';
  if (t.includes('đọc') || t.includes('sách')) return 'Thời gian đọc sách thú vị!';
  if (t.includes('ăn')) return 'Ăn ngon để có sức khỏe nhé!';
  if (t.includes('mặc') || t.includes('đồng phục')) return 'Mặc gọn gàng thật đẹp!';
  return 'Cố lên — Foxy tin con làm được!';
}

function commitmentStars(item: DayFlowCommitment): number {
  if (item.starReward != null && item.starReward > 0) return item.starReward;
  const t = item.title.toLowerCase();
  if (t.includes('bài') || t.includes('học') || t.includes('toán')) return 20;
  if (t.includes('ngủ') || t.includes('đánh răng')) return 15;
  return 10;
}

function commitmentDisplayDelta(item: DayFlowCommitment): number {
  if (item.status === 'done') return gardenStarsForCommitment(item);
  if (item.status === 'skipped') return 0;
  return item.projectedStarDelta ?? commitmentStars(item);
}

function starBadgeClass(delta: number): string {
  if (delta < 0) return ' is-penalty';
  if (delta === 0) return ' is-zero';
  return '';
}

function formatStarBadge(delta: number): string {
  if (delta > 0) return `+${delta}⭐`;
  if (delta === 0) return '0⭐';
  return `${delta}⭐`;
}

const LATE_ZERO_STAR_TIERS = new Set([
  'late_zero',
  'late_penalty_half',
  'late_penalty_full',
]);

function isLateZeroStarOutcome(item: DayFlowCommitment): boolean {
  if (item.isLateDone) return true;
  if (item.starTier && LATE_ZERO_STAR_TIERS.has(item.starTier)) return true;
  if (item.starLabelVi?.startsWith('Muộn') && (item.starDelta ?? 0) <= 0) return true;
  return false;
}

function kidFriendlyStarLabel(label: string): string {
  return normalizeLateStarLabelVi(label).replace(/ — 0⭐$/, ' — không được sao');
}

function lateMinutesAfterGrace(
  item: DayFlowCommitment,
  flowDate?: string,
): number | null {
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

/** One-line kid caption when a done task earned no / penalty stars for lateness. */
function lateStarCaption(item: DayFlowCommitment, flowDate?: string): string | null {
  if (item.status !== 'done') return null;
  const delta = commitmentDisplayDelta(item);
  if (!isLateZeroStarOutcome(item) || delta > 0) return null;

  if (item.starLabelVi?.startsWith('Muộn')) {
    return stripLateStarSuffixVi(item.starLabelVi);
  }

  const lateMin = lateMinutesAfterGrace(item, flowDate);
  if (lateMin != null) return formatLateDurationCaption(lateMin);

  if (item.isLateDone) {
    return 'Làm muộn';
  }

  return null;
}

function MissionStarBadge({
  item,
  className = '',
}: {
  item: DayFlowCommitment;
  className?: string;
}) {
  const delta = commitmentDisplayDelta(item);
  const hintRaw =
    item.status === 'done' ? item.starLabelVi : item.projectedStarLabelVi;
  const hint = hintRaw ? kidFriendlyStarLabel(hintRaw) : undefined;
  return (
    <span
      className={`kv2-m-stars${starBadgeClass(delta)}${className ? ` ${className}` : ''}`}
      title={hint}
    >
      {formatStarBadge(delta)}
    </span>
  );
}

function formatStarDelta(delta: number): string {
  if (delta > 0) return `+${delta}`;
  return String(delta);
}

function formatStars(n: number): string {
  return Math.round(n).toLocaleString('vi-VN');
}

function starBalanceNote(balance: number): string {
  if (balance <= 0) return 'Giữ kế hoạch hôm nay để kiếm sao nhé!';
  if (balance < 100) return 'Đang tích lũy — cố lên!';
  if (balance < 500) return 'Tiến bộ tuyệt vời!';
  return 'Quá tuyệt vời!';
}

const CATALOG_TONES = ['pink', 'lemon', 'sky', 'mint', 'lilac'] as const;

function journalDoneIsLate(item: DayFlowCommitment): boolean {
  if (item.status !== 'done') return false;
  if (item.isLateDone) return true;
  if (item.starLabelVi?.startsWith('Muộn')) return true;
  if (item.starTier && LATE_ZERO_STAR_TIERS.has(item.starTier)) return true;
  return false;
}

function journalDoneStatusLine(
  short: string,
  item: DayFlowCommitment,
  flowDate?: string,
): string {
  if (!journalDoneIsLate(item)) return `${short} đã hoàn thành!`;
  const lateMin = lateMinutesAfterGrace(item, flowDate);
  if (lateMin != null) return `${short} đã hoàn thành — muộn ${formatLateDuration(lateMin)}`;
  return `${short} đã hoàn thành — hơi chậm hơn giờ`;
}

function journalLateFollow(title: string, nextTitle?: string): string {
  return lateEncourageFollow(title, nextTitle, 'journal');
}

function journalLateNote(
  title: string,
  short: string,
  parentRole: ParentRole,
  nextTitle?: string,
  seed?: string,
): string {
  const kind = taskKindOf(title);
  const follow = journalLateFollow(title, nextTitle);
  const pick = seed ?? title;
  if (kind === 'brush') {
    const pool = [
      `${short} vẫn đánh răng xong rồi${follow} 💛`,
      `${short} đánh răng xong rồi — cố thêm chút về giờ nhé!${follow} 💛`,
    ];
    return stablePick(`${pick}:late-rang`, pool);
  }
  if (kind === 'read') {
    const pool = [
      `${short} vẫn đọc được hôm nay${follow} 📖`,
      `${short} đọc xong rồi — mai đúng giờ hơn nha!${follow} 📖`,
    ];
    return stablePick(`${pick}:late-doc`, pool);
  }
  if (kind === 'pack') {
    const pool = [
      `${short} vẫn chuẩn bị xong${follow} 💛`,
      `${short} chuẩn bị xong rồi — lần sau sớm hơn nhé!${follow} 💛`,
    ];
    return stablePick(`${pick}:late-cap`, pool);
  }
  if (kind === 'garden') {
    const pool = [
      `${short} vẫn chăm cây được${follow} 🌱`,
      `${short} tưới cây xong — mai đúng giờ hơn nha!${follow} 🌱`,
    ];
    return stablePick(`${pick}:late-cay`, pool);
  }
  if (kind === 'sleep') {
    const pool = [
      `${short} vẫn đi ngủ rồi${follow} 😴`,
      `${short} ngủ rồi — lần sau thử sớm hơn nhé!${follow} 😴`,
    ];
    return stablePick(`${pick}:late-ngu`, pool);
  }
  if (kind === 'study') {
    const pool = [
      `${short} vẫn cố gắng với bài học${follow} 💛`,
      `${short} học xong rồi — mai tranh thủ sớm hơn nhé!${follow} 💛`,
    ];
    return stablePick(`${pick}:late-hoc`, pool);
  }
  return stablePick(`${pick}:late`, PARENT_ENCOURAGE_LATE)(
    short,
    title,
    follow,
    parentRole,
  );
}

function journalNote(
  item: DayFlowCommitment,
  short: string,
  parentRole: ParentRole,
  noteStatus: 'done' | 'pending' | 'awaiting' | 'skipped',
  nextTitle?: string,
): string {
  const title = item.title;
  const kind = taskKindOf(title);
  const seed = praiseSeed(item);
  const Parent = capitalizeParentRole(parentRole);
  if (noteStatus !== 'done') {
    return diaryTaskNote(title, short, noteStatus, parentRole);
  }
  if (journalDoneIsLate(item)) {
    return journalLateNote(title, short, parentRole, nextTitle, seed);
  }
  if (kind === 'wake') {
    const pool = [
      `${short} đã dậy đúng giờ — khởi đầu ngày thật tốt! ☀️`,
      `Dậy đúng giờ rồi — ${parentRole} tự hào lắm! ☀️`,
    ];
    return stablePick(`${seed}:day`, pool);
  }
  if (kind === 'brush') {
    const pool = [
      `Tự giác hoàn thành trước giờ. ${Parent} rất tự hào! 💪`,
      `Đánh răng đúng giờ — ${parentRole} tự hào lắm! 💪`,
    ];
    return stablePick(`${seed}:rang`, pool);
  }
  if (kind === 'read') {
    const pool = [
      'Con đã đọc rất tập trung. Hôm nay con chọn sách hay quá!',
      `${Parent} thấy con đọc rất chăm — hay lắm! 📖`,
    ];
    return stablePick(`${seed}:doc`, pool);
  }
  if (kind === 'pack') {
    const pool = [
      `${short} đã chuẩn bị cặp sách. ${Parent} kiểm tra giúp nhé!`,
      `${short} chuẩn bị xong — ${parentRole} xem giúp con nhé!`,
    ];
    return stablePick(`${seed}:cap`, pool);
  }
  if (kind === 'garden') {
    const pool = [
      'Khu vườn của con đang lớn lên mỗi ngày! 🌱',
      'Cây nhà mình khỏe hơn nhờ con chăm sóc! 🌱',
    ];
    return stablePick(`${seed}:cay`, pool);
  }
  if (kind === 'sleep') {
    const pool = [
      'Ngủ sớm để mai tràn đầy năng lượng nhé con! 😴',
      'Ngủ đúng giờ — mai dậy khỏe hơn nhé! 😴',
    ];
    return stablePick(`${seed}:ngu`, pool);
  }
  if (kind === 'study') {
    const pool = [
      `${short} đã cố gắng hoàn thành bài học!`,
      `${short} học xong rồi — ${parentRole} tự hào! 💪`,
    ];
    return stablePick(`${seed}:hoc`, pool);
  }
  if (kind === 'meal') {
    const pool = [
      `${short} đã ${title.toLowerCase()} xong — ngoan lắm!`,
      `${title} xong rồi — giữ nhịp tốt nhé!`,
    ];
    return stablePick(`${seed}:meal`, pool);
  }
  return stablePick(`${seed}:on`, JOURNAL_ON_TIME_GENERIC)(short, title, '', parentRole);
}

function itemTimeLabel(item: DayFlowCommitment): string {
  const raw = item.windowStart || item.windowEnd;
  if (raw) return raw.slice(0, 5);
  return '--:--';
}

const MOODS = FAMILY_MOODS;

type Props = {
  childName: string;
  /** Cách gọi bố/mẹ theo thành viên thật trong nhà — tránh hard-code "Mẹ". */
  parentRole?: ParentRole;
  items: DayFlowCommitment[];
  busyId: string | null;
  celebrating: boolean;
  streak?: number;
  flowDate?: string;
  localTime?: string;
  todayBeautiful?: boolean;
  glanceDays?: AccountabilityDayGlance[];
  teamPercent?: number;
  teamRemaining?: number;
  teamComplete?: boolean;
  /** true when team stats come from GET /team-day (not child-only fallback). */
  teamFromApi?: boolean;
  teamMissionLine?: string;
  softLockActive?: boolean;
  softLockLabel?: string;
  familyId: string;
  childMemberId?: string;
  /** ISO date — drives teen vs younger bond copy. */
  dateOfBirth?: string | null;
  starBalance?: number;
  onStarBalanceChange?: (balance: number) => void;
  onDone: (
    item: DayFlowCommitment,
    evidenceUrl?: string,
  ) => Promise<{
    starDelta?: number;
    starLabelVi?: string;
    memberStarBalance?: number;
    starPosted?: boolean;
    evidenceSatisfied?: boolean;
    evidenceGateLabelVi?: string;
    evidenceSubmitted?: boolean;
    evidenceUrl?: string;
  } | void>;
  onReflect: (item: DayFlowCommitment, reason: SkipReasonCode) => void;
  onSelfStart?: (item: DayFlowCommitment) => void | Promise<void>;
  onHoldSwitchStart: () => void;
  onHoldSwitchCancel: () => void;
  holdProgress: number;
  holdHolding: boolean;
  onOpenParentPin: () => void;
};

export function KidFocusView({
  childName,
  parentRole = 'bố mẹ',
  items,
  busyId,
  celebrating,
  streak = 0,
  flowDate = '',
  dateOfBirth = null,
  localTime,
  todayBeautiful = false,
  glanceDays = [],
  teamPercent = 0,
  teamRemaining = 0,
  teamComplete = false,
  teamFromApi = false,
  teamMissionLine,
  softLockActive = false,
  softLockLabel,
  familyId,
  childMemberId,
  starBalance = 0,
  onStarBalanceChange,
  onDone,
  onReflect,
  onSelfStart,
  onHoldSwitchStart,
  onHoldSwitchCancel,
  holdProgress,
  holdHolding,
  onOpenParentPin,
}: Props) {
  const careBand = useMemo(() => careAgeBandFromDob(dateOfBirth), [dateOfBirth]);
  const [tab, setTab] = useState<KidTab>('home');
  const [famiSheetOpen, setFamiSheetOpen] = useState(false);
  const [homePane, setHomePane] = useState<KidHomePane>('hub');
  const [filter, setFilter] = useState<DayPart>('all');
  const [nowOpen, setNowOpen] = useState(true);
  const [waitOpen, setWaitOpen] = useState(true);
  const [soonOpen, setSoonOpen] = useState(true);
  const [treasureToast, setTreasureToast] = useState<string | null>(null);
  const [screenRequestOpen, setScreenRequestOpen] = useState(false);
  const [missionRequestOpen, setMissionRequestOpen] = useState(false);
  const [screenRequestToast, setScreenRequestToast] = useState<string | null>(null);
  const [screenWallet, setScreenWallet] = useState<ScreenWallet | null>(null);
  const [childRequests, setChildRequests] = useState<ChildRequest[]>([]);
  const [askReloadTick, setAskReloadTick] = useState(0);
  const [moodIdx, setMoodIdx] = useState(3);
  const [moodNote, setMoodNote] = useState('');
  const [moodSaving, setMoodSaving] = useState(false);
  const [moodLoaded, setMoodLoaded] = useState(false);
  const [openSeqTick, setOpenSeqTick] = useState(0);
  const [ritualBusy, setRitualBusy] = useState(false);
  const [inboxNudges, setInboxNudges] = useState<TeamNudge[]>([]);
  const [parentVoiceInbox, setParentVoiceInbox] = useState<ParentVoiceMessage[]>([]);
  const [voiceAckBusy, setVoiceAckBusy] = useState<string | null>(null);
  const [kidRelTriggers, setKidRelTriggers] = useState<RelationshipTrigger[]>([]);
  const [eveningCircle, setEveningCircle] = useState<EveningCircle | null>(null);
  const [circleAnswer, setCircleAnswer] = useState('');
  const [circleBusy, setCircleBusy] = useState(false);
  const [weeklyStory, setWeeklyStory] = useState<WeeklyStory | null>(null);
  const [nudgeAckBusy, setNudgeAckBusy] = useState<string | null>(null);
  const [nudgeCandidates, setNudgeCandidates] = useState<TeamNudgeCandidate[]>([]);
  const [cheerOpen, setCheerOpen] = useState(false);
  const [cheerToId, setCheerToId] = useState('');
  const [cheerTemplate, setCheerTemplate] = useState<TeamNudgeTemplate>('cheer_up');
  const [cheerBusy, setCheerBusy] = useState(false);
  const [cheerError, setCheerError] = useState<string | null>(null);
  const [kidVoiceHardest, setKidVoiceHardest] = useState<string>(CHILD_VOICE_DEFAULTS.hardest);
  const [kidVoiceWant, setKidVoiceWant] = useState<string>(CHILD_VOICE_DEFAULTS.want);
  const [kidVoiceWish, setKidVoiceWish] = useState('');
  const [kidVoiceBusy, setKidVoiceBusy] = useState(false);
  const [kidVoiceDone, setKidVoiceDone] = useState(false);
  const [cheerToast, setCheerToast] = useState<string | null>(null);
  const [nudgeReloadTick, setNudgeReloadTick] = useState(0);
  const [journalToast, setJournalToast] = useState<string | null>(null);
  const [kidMomentSheetOpen, setKidMomentSheetOpen] = useState(false);
  const [momentIdx, setMomentIdx] = useState(0);
  const [journalDayIdx, setJournalDayIdx] = useState(5);
  const [active, setActive] = useState<DayFlowCommitment | null>(null);
  const [askReason, setAskReason] = useState(false);
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [evidencePreview, setEvidencePreview] = useState<string | null>(null);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  const [evidenceSoftWarn, setEvidenceSoftWarn] = useState<string | null>(null);
  const [morningNote, setMorningNote] = useState<MorningNote | null>(null);
  const [uploading, setUploading] = useState(false);
  const [celebrate, setCelebrate] = useState<{
    title: string;
    stars: number;
    labelVi?: string;
  } | null>(null);
  const [foxyGlow, setFoxyGlow] = useState(false);
  const [thanksSending, setThanksSending] = useState(false);
  const [thanksSent, setThanksSent] = useState(false);
  const [thanksError, setThanksError] = useState<string | null>(null);
  const [thanksBackOffer, setThanksBackOffer] = useState<{
    toMemberId: string;
    toName: string;
  } | null>(null);
  const [thanksBackBusy, setThanksBackBusy] = useState(false);
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
  const [missionDoneError, setMissionDoneError] = useState<string | null>(null);
  const [localStars, setLocalStars] = useState(starBalance);
  const [rewardCatalog, setRewardCatalog] = useState<RewardCatalogItem[]>([]);
  const [redemptions, setRedemptions] = useState<RewardRedemption[]>([]);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [treasureLoading, setTreasureLoading] = useState(false);
  const [redeemBusyId, setRedeemBusyId] = useState<string | null>(null);
  const [treasureSheet, setTreasureSheet] = useState<'rewards' | 'badges' | null>(null);
  const [journalSheet, setJournalSheet] = useState<'memories' | 'moments' | null>(null);
  const [diaryDayPickerOpen, setDiaryDayPickerOpen] = useState(false);
  const [achievementSheet, setAchievementSheet] = useState<AchievementSheet>(null);
  const [memoryFilter, setMemoryFilter] = useState<KidMemoryFilter>('all');
  const [savedMemories, setSavedMemories] = useState<FamilyMemoryEntry[]>([]);
  const [movieRemindBusy, setMovieRemindBusy] = useState(false);
  const [journalHistoryItems, setJournalHistoryItems] = useState<DayFlowCommitment[] | null>(
    null,
  );
  const [journalHistoryLoading, setJournalHistoryLoading] = useState(false);
  const [journalHistoryError, setJournalHistoryError] = useState<string | null>(null);
  const achievementSheetOpenedAt = useRef(0);
  const bloomCaptureKey = useRef<string | null>(null);
  const actionOpenedAt = useRef(0);
  const treasureSheetOpenedAt = useRef(0);
  const journalSheetOpenedAt = useRef(0);
  const backdropPointerDown = useRef(false);
  const evidenceInputRef = useRef<HTMLInputElement>(null);
  const evidencePickTarget = useRef<DayFlowCommitment | null>(null);

  useEffect(() => {
    setLocalStars(starBalance);
  }, [starBalance]);

  useEffect(() => {
    if (!familyId || !childMemberId) {
      setInboxNudges([]);
      setNudgeCandidates([]);
      setParentVoiceInbox([]);
      setKidRelTriggers([]);
      setEveningCircle(null);
      setWeeklyStory(null);
      return;
    }
    let cancelled = false;
    void fetchTeamNudges(familyId, { flowDate, forMemberId: childMemberId })
      .then((rows) => {
        if (cancelled) return;
        setInboxNudges(
          rows.filter(
            (n) => n.toMemberId === childMemberId && n.status === 'sent',
          ),
        );
      })
      .catch(() => {
        if (!cancelled) setInboxNudges([]);
      });
    void fetchParentVoice(familyId, { forMemberId: childMemberId })
      .then((rows) => {
        if (cancelled) return;
        // Inbox: unread only. Album filter uses full history (read/thanks too).
        setParentVoiceInbox(rows);
      })
      .catch(() => {
        if (!cancelled) setParentVoiceInbox([]);
      });
    void fetchRelationshipTriggers(familyId, childMemberId, flowDate)
      .then((rows) => {
        if (!cancelled) setKidRelTriggers(rows);
      })
      .catch(() => {
        if (!cancelled) setKidRelTriggers([]);
      });
    void fetchEveningCircle(familyId, {
      forMemberId: childMemberId,
      flowDate,
    })
      .then((row) => {
        if (!cancelled) setEveningCircle(row);
      })
      .catch(() => {
        if (!cancelled) setEveningCircle(null);
      });
    void fetchWeeklyStory(familyId, flowDate, childMemberId)
      .then((row) => {
        if (!cancelled) setWeeklyStory(row);
      })
      .catch(() => {
        if (!cancelled) setWeeklyStory(null);
      });
    void fetchTeamNudgeFromCandidates(familyId, flowDate)
      .then((rows) => {
        if (!cancelled) setNudgeCandidates(rows);
      })
      .catch(() => {
        if (!cancelled) setNudgeCandidates([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, flowDate, nudgeReloadTick, teamRemaining, items.length]);

  useEffect(() => {
    if (!familyId || !childMemberId || !flowDate) {
      setMorningNote(null);
      return;
    }
    let cancelled = false;
    void getMorningNote(familyId, childMemberId, flowDate)
      .then((n) => {
        if (!cancelled) setMorningNote(n);
      })
      .catch(() => {
        if (!cancelled) setMorningNote(null);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, flowDate, items.length]);

  useEffect(() => {
    if (!cheerToast) return;
    const t = window.setTimeout(() => setCheerToast(null), 2800);
    return () => window.clearTimeout(t);
  }, [cheerToast]);

  const meNudgeCand = useMemo(
    () => nudgeCandidates.find((c) => c.memberId === childMemberId) ?? null,
    [nudgeCandidates, childMemberId],
  );
  const cheerTargets = useMemo(
    () =>
      nudgeCandidates.filter(
        (c) => c.memberId !== childMemberId && !c.missionsComplete,
      ),
    [nudgeCandidates, childMemberId],
  );
  const cheerPreview = useMemo(() => {
    const to = cheerTargets.find((c) => c.memberId === cheerToId)?.displayName;
    return nudgeMessagePreview(
      cheerTemplate,
      shortChildName(childName),
      to ? shortChildName(to) : 'em',
    );
  }, [cheerTargets, cheerToId, cheerTemplate, childName]);
  const kidPrimaryTrigger = useMemo(
    () => primaryRelationshipTrigger(kidRelTriggers),
    [kidRelTriggers],
  );
  const showCheerOffer =
    (Boolean(meNudgeCand?.canInvite) &&
      teamRemaining >= 1 &&
      cheerTargets.length > 0 &&
      !teamComplete) ||
    (kidPrimaryTrigger != null && isCheerSiblingTrigger(kidPrimaryTrigger.code));

  const cheerOfferCopy = useMemo(() => {
    const target =
      kidPrimaryTrigger?.toMemberName ||
      cheerTargets[0]?.displayName ||
      'anh/chị em';
    return cheerOfferCopyForBand(
      shortChildName(childName),
      shortChildName(target),
      teamRemaining,
      careBand,
      kidPrimaryTrigger && isCheerSiblingTrigger(kidPrimaryTrigger.code)
        ? kidPrimaryTrigger.bodyVi
        : null,
    );
  }, [kidPrimaryTrigger, cheerTargets, teamRemaining, childName, careBand]);

  const openCheerSheet = () => {
    setCheerError(null);
    setCheerTemplate(teamRemaining === 1 ? 'one_left' : 'cheer_up');
    const prefer =
      kidPrimaryTrigger?.toMemberId &&
      cheerTargets.some((c) => c.memberId === kidPrimaryTrigger.toMemberId)
        ? kidPrimaryTrigger.toMemberId
        : cheerTargets[0]?.memberId ?? '';
    setCheerToId(prefer);
    setCheerOpen(true);
  };

  const submitCheer = async () => {
    if (!childMemberId || !cheerToId) {
      setCheerError('Chọn anh/chị em để cổ vũ.');
      return;
    }
    setCheerBusy(true);
    setCheerError(null);
    try {
      const draft = await createTeamNudge(familyId, {
        fromMemberId: childMemberId,
        toMemberId: cheerToId,
        templateCode: cheerTemplate,
        flowDate: flowDate || undefined,
      });
      await sendTeamNudge(familyId, draft.id);
      setCheerOpen(false);
      setCheerToast('Đã gửi lời cổ vũ — cả đội cảm ơn con!');
      setNudgeReloadTick((n) => n + 1);
    } catch (e) {
      setCheerError(getApiErrorMessage(e) || 'Chưa gửi được — thử lại nhé.');
    } finally {
      setCheerBusy(false);
    }
  };

  const sendThanksBackTo = async (toMemberId: string, toName: string) => {
    if (!childMemberId) return false;
    const draft = await createTeamNudge(familyId, {
      fromMemberId: childMemberId,
      toMemberId,
      templateCode: 'thanks_back',
      flowDate: flowDate || undefined,
    });
    await sendTeamNudge(familyId, draft.id);
    const shortTo = toName.split(/\s+/).filter(Boolean).slice(-1)[0] || 'anh/chị';
    setCheerToast(`Đã gửi cảm ơn ${shortTo} — ${shortTo} sẽ thấy lời của ${short}!`);
    return true;
  };

  const ackInboxNudge = async (nudgeId: string, status: 'thanks' | 'seen') => {
    const source = inboxNudges.find((n) => n.id === nudgeId);
    setNudgeAckBusy(nudgeId);
    try {
      await ackTeamNudge(familyId, nudgeId, status);
      setInboxNudges((prev) => prev.filter((n) => n.id !== nudgeId));
      if (
        status === 'thanks' &&
        source &&
        source.templateCode !== 'thanks_back' &&
        source.fromMemberId
      ) {
        const toName = source.fromName.trim() || 'anh/chị';
        try {
          // One tap: ack + gửi lời cảm ơn — trước đây bước 2 dễ bỏ sót nên anh không thấy gì.
          await sendThanksBackTo(source.fromMemberId, toName);
          setThanksBackOffer(null);
        } catch (e) {
          setThanksBackOffer({ toMemberId: source.fromMemberId, toName });
          setCheerToast(
            getApiErrorMessage(e) ||
              `Chạm Gửi cảm ơn bên dưới để gửi lời tới ${shortChildName(toName) || 'anh/chị'}.`,
          );
        }
      } else if (status === 'seen' && source?.templateCode === 'thanks_back') {
        setCheerToast(
          `Đã nhận lời cảm ơn từ ${shortChildName(source.fromName) || 'em'} — ấm quá!`,
        );
      } else if (status === 'seen') {
        setCheerToast('Đã xem — giữ nhịp nhé!');
      }
      setNudgeReloadTick((n) => n + 1);
    } catch (e) {
      setCheerToast(getApiErrorMessage(e) || 'Chưa gửi được — thử lại nhé.');
    } finally {
      setNudgeAckBusy(null);
    }
  };

  const sendThanksBack = async () => {
    if (!childMemberId || !thanksBackOffer || thanksBackBusy) return;
    setThanksBackBusy(true);
    try {
      await sendThanksBackTo(thanksBackOffer.toMemberId, thanksBackOffer.toName);
      setThanksBackOffer(null);
      setNudgeReloadTick((n) => n + 1);
    } catch (e) {
      setCheerToast(getApiErrorMessage(e) || 'Chưa gửi được cảm ơn — thử lại nhé.');
    } finally {
      setThanksBackBusy(false);
    }
  };

  const ackVoiceMessage = async (messageId: string, status: 'read' | 'thanks') => {
    const source = parentVoiceInbox.find((v) => v.id === messageId);
    const from = source?.fromMemberName.trim() || 'bố/mẹ';
    setVoiceAckBusy(messageId);
    try {
      await ackParentVoice(familyId, messageId, status);
      setParentVoiceInbox((prev) =>
        prev.map((n) => (n.id === messageId ? { ...n, status } : n)),
      );
      setCheerToast(
        status === 'thanks'
          ? `Đã gửi cảm ơn ${from} — ${from} sẽ thấy phản hồi của ${short}!`
          : `Đã xem lời từ ${from}.`,
      );
      setNudgeReloadTick((n) => n + 1);
    } catch (e) {
      setCheerToast(getApiErrorMessage(e) || 'Chưa gửi được — thử lại nhé.');
    } finally {
      setVoiceAckBusy(null);
    }
  };

  const submitKidEveningCircle = async () => {
    if (!childMemberId || !circleAnswer.trim() || circleBusy) return;
    setCircleBusy(true);
    try {
      const row = await answerEveningCircle(familyId, {
        memberId: childMemberId,
        answerVi: circleAnswer.trim(),
        flowDate: flowDate || undefined,
      });
      setEveningCircle(row);
      setCircleAnswer('');
    } catch {
      // keep
    } finally {
      setCircleBusy(false);
    }
  };

  const openWeekReview = async () => {
    if (!childMemberId || !weeklyStory) return;
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
        fetchParentVoice(familyId, { forMemberId: childMemberId }),
        fetchTeamNudges(familyId, { forMemberId: childMemberId }),
        fetchFamilyMemories(familyId, {
          from,
          to,
          memberId: childMemberId,
          limit: 80,
        }).catch(() => [] as FamilyMemoryEntry[]),
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
        if (v.toMemberId !== childMemberId) continue;
        moments.push({
          id: `voice-${v.id}`,
          icon: parentVoiceIcon(v.templateCode),
          kindLabel: parentVoiceKindLabelVi(v.templateCode),
          titleVi: `Lời từ ${v.fromMemberName.trim() || 'bố/mẹ'}`,
          bodyVi: v.bodyVi,
          at: v.sentAt || v.flowDate,
        });
      }

      for (const n of nudges) {
        if (!inWeek(n.flowDate) && !inWeek(n.sentAt) && !inWeek(n.createdAt)) continue;
        if (n.status === 'draft' || n.status === 'deferred') continue;
        const isThanks = n.templateCode === 'thanks_back';
        const fromShort = shortChildName(n.fromName) || 'Anh/chị';
        const toShort = shortChildName(n.toName) || 'em';
        moments.push({
          id: `nudge-${n.id}`,
          icon: isThanks ? '💌' : '💛',
          kindLabel: isThanks ? 'Cảm ơn anh chị' : 'Cổ vũ anh chị',
          titleVi: isThanks
            ? `${fromShort} cảm ơn ${toShort}`
            : `${fromShort} cổ vũ ${toShort}`,
          bodyVi: n.messageVi,
          at: n.sentAt || n.createdAt || n.flowDate,
        });
      }

      for (const m of memories) {
        if (
          m.kind !== 'gratitude' &&
          m.kind !== 'evening_circle' &&
          m.kind !== 'streak_milestone'
        ) {
          continue;
        }
        // Avoid duplicating parent_voice / help already listed from live rows.
        moments.push({
          id: `mem-${m.id}`,
          icon: m.icon || (m.kind === 'gratitude' ? '💖' : m.kind === 'evening_circle' ? '⭐' : '🔥'),
          kindLabel:
            m.kind === 'gratitude'
              ? 'Cảm ơn bố/mẹ'
              : m.kind === 'evening_circle'
                ? 'Evening Circle'
                : 'Streak',
          titleVi: m.titleVi,
          bodyVi: m.noteVi,
          at: m.happenedAt || m.flowDate,
        });
      }

      moments.sort((a, b) => b.at.localeCompare(a.at));
      setWeekReviewMoments(moments);
    } catch (e) {
      setWeekReviewError(getApiErrorMessage(e) || 'Chưa tải được tuần này — thử lại nhé.');
      setWeekReviewMoments([]);
    } finally {
      setWeekReviewLoading(false);
    }
  };

  useEffect(() => {
    if (
      !treasureSheet &&
      !journalSheet &&
      !achievementSheet &&
      !active &&
      !weekReviewOpen &&
      !diaryDayPickerOpen
    )
      return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape') return;
      if (diaryDayPickerOpen) setDiaryDayPickerOpen(false);
      else if (weekReviewOpen) setWeekReviewOpen(false);
      else if (achievementSheet) setAchievementSheet(null);
      else if (treasureSheet) setTreasureSheet(null);
      else if (journalSheet) setJournalSheet(null);
      else setActive(null);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [
    treasureSheet,
    journalSheet,
    achievementSheet,
    active,
    weekReviewOpen,
    diaryDayPickerOpen,
  ]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    setTreasureLoading(true);
    void fetchRewardCatalog(familyId)
      .then((rows) => {
        if (!cancelled) setRewardCatalog(rows);
      })
      .catch(() => {
        if (!cancelled) setRewardCatalog([]);
      })
      .finally(() => {
        if (!cancelled) setTreasureLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId]);

  useEffect(() => {
    if (!familyId || !childMemberId) return;
    let cancelled = false;
    void fetchRewardRedemptions(familyId, childMemberId)
      .then((rows) => {
        if (!cancelled) setRedemptions(rows);
      })
      .catch(() => {
        if (!cancelled) setRedemptions([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, localStars]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void fetchFamilyMemories(familyId, { limit: 80 })
      .then((rows) => {
        if (!cancelled) setSavedMemories(rows);
      })
      .catch(() => {
        if (!cancelled) setSavedMemories([]);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, flowDate]);

  useEffect(() => {
    if (!familyId) return;
    let cancelled = false;
    void (async () => {
      try {
        if (flowDate) await fetchTeamUnlocks(familyId, flowDate, true);
        const rows = await fetchTeamUnlocks(familyId);
        if (!cancelled) setTeamUnlocks(rows);
      } catch {
        if (!cancelled) setTeamUnlocks([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [familyId, flowDate, teamComplete]);

  useEffect(() => {
    if (!familyId || !childMemberId) {
      setScreenWallet(null);
      setChildRequests([]);
      return;
    }
    let cancelled = false;
    void Promise.all([
      fetchScreenWallet(familyId).catch(() => [] as ScreenWallet[]),
      fetchChildRequests(familyId, { memberId: childMemberId }).catch(
        () => [] as ChildRequest[],
      ),
    ]).then(([wallets, requests]) => {
      if (cancelled) return;
      setScreenWallet(
        wallets.find((w) => w.memberId === childMemberId) ?? wallets[0] ?? null,
      );
      setChildRequests(
        [...requests].sort(
          (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
        ),
      );
    });
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, askReloadTick, homePane]);

  useEffect(() => {
    if (!familyId || !childMemberId || !flowDate) {
      setMoodLoaded(true);
      return;
    }
    let cancelled = false;
    setMoodLoaded(false);
    void fetchMemberMood(familyId, childMemberId, flowDate)
      .then((row) => {
        if (cancelled) return;
        if (row) {
          setMoodIdx(moodIndexFromCode(row.moodCode));
          setMoodNote(row.note ?? '');
        } else {
          setMoodIdx(3);
          setMoodNote('');
        }
      })
      .catch(() => {
        if (!cancelled) {
          setMoodIdx(3);
          setMoodNote('');
        }
      })
      .finally(() => {
        if (!cancelled) setMoodLoaded(true);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, flowDate]);

  const saveMoodEntry = async () => {
    if (!familyId || !childMemberId || !flowDate || moodSaving) return;
    setMoodSaving(true);
    try {
      await upsertMemberMood(familyId, childMemberId, {
        flowDate,
        moodCode: MOODS[moodIdx].code,
        note: moodNote.trim() || undefined,
      });
      showJournalToast(
        moodNote.trim()
          ? `Đã lưu nhật ký · ${MOODS[moodIdx].label}! 💜`
          : `Đã lưu tâm trạng «${MOODS[moodIdx].label}»! 💜`,
      );
    } catch {
      showJournalToast('Chưa lưu được — thử lại nhé.');
    } finally {
      setMoodSaving(false);
    }
  };

  const doneItems = useMemo(
    () => items.filter((c) => c.status === 'done' || c.status === 'skipped'),
    [items],
  );
  const pendingItems = useMemo(
    () => items.filter((c) => c.status !== 'done' && c.status !== 'skipped'),
    [items],
  );
  const trulyDone = useMemo(
    () => items.filter((c) => c.status === 'done'),
    [items],
  );

  const nextMission = useMemo(() => pickNextMission(pendingItems), [pendingItems]);
  const doneCount = trulyDone.length;
  const total = items.length;
  const remaining = pendingItems.length;
  const dayClosed = total > 0 && remaining === 0;
  const stars = localStars;
  const unlockPct = Math.max(
    0,
    Math.min(
      100,
      teamComplete
        ? 100
        : teamFromApi || teamRemaining > 0 || teamPercent > 0
          ? teamPercent
          : total > 0
            ? Math.round((doneCount / total) * 100)
            : 0,
    ),
  );
  const unlockLeft = teamComplete
    ? 0
    : Math.max(0, teamRemaining > 0 ? teamRemaining : remaining);

  const todayComboUnlock = useMemo(() => {
    const sameDay = teamUnlocks.filter(
      (u) => (!flowDate || u.flowDate === flowDate) && isSiblingComboUnlock(u.rewardCode),
    );
    return (
      sameDay.find((u) => u.status === 'pending_confirm' || u.status === 'confirmed') ??
      null
    );
  }, [teamUnlocks, flowDate]);

  const filteredPending = useMemo(() => {
    if (filter === 'done') return [] as DayFlowCommitment[];
    if (filter === 'all') return pendingItems;
    return pendingItems.filter((i) => dayPartOf(i) === filter);
  }, [pendingItems, filter]);

  const filteredDone = useMemo(() => {
    if (filter === 'all' || filter === 'done') return doneItems;
    return doneItems.filter((i) => dayPartOf(i) === filter);
  }, [doneItems, filter]);

  const doNowItems = useMemo(() => {
    const base = filteredPending.filter(
      (c) => c.reminderState === 'overdue' || c.reminderState === 'due_now',
    );
    // Cam kết học lên trước để con thấy khác việc nhà.
    return [...base].sort((a, b) => {
      const as = isStudyFocus(a) ? 0 : 1;
      const bs = isStudyFocus(b) ? 0 : 1;
      return as - bs;
    });
  }, [filteredPending]);
  const soonItems = useMemo(
    () =>
      filteredPending.filter(
        (c) => c.reminderState !== 'overdue' && c.reminderState !== 'due_now',
      ),
    [filteredPending],
  );
  const waitingCheckItems = useMemo(
    () =>
      filter === 'done'
        ? []
        : filteredDone.filter(
            (c) => kidMissionUxState(c, flowDate) === 'awaiting_check',
          ),
    [filteredDone, filter, flowDate],
  );

  const countAll = items.length;
  const countMorning = items.filter((i) => dayPartOf(i) === 'morning').length;
  const countAfternoon = items.filter((i) => dayPartOf(i) === 'afternoon').length;
  const countEvening = items.filter((i) => dayPartOf(i) === 'evening').length;

  const missionSegs = 7;
  const missionFilled = Math.round((unlockPct / 100) * missionSegs);

  const recentAsks = useMemo(() => childRequests.slice(0, 4), [childRequests]);
  const pendingAskCount = useMemo(
    () => childRequests.filter((r) => r.status === 'pending').length,
    [childRequests],
  );

  const garden = useMemo(
    () =>
      trulyDone.slice(0, 8).map((c) => {
        const stars = gardenStarsForCommitment(c);
        const { plant, mood } = gardenPlantForStars(c.title, stars);
        return {
          id: c.id,
          plant,
          mood,
          badge: gardenBadge(c.title, c.commitmentKind),
          stars,
          label: c.title,
        };
      }),
    [trulyDone],
  );

  const gardenBloom = useMemo(() => {
    const name = shortChildName(childName);
    const goal = 3;
    const healthy = garden.filter((g) => g.mood === 'healthy').length;
    const reached = healthy >= goal;
    const remaining = Math.max(0, goal - healthy);
    return {
      goal,
      healthy,
      reached,
      remaining,
      pct: Math.min(100, Math.round((healthy / goal) * 100)),
      label: reached
        ? `Vườn của ${name} đã nở! ${healthy} cây khỏe hôm nay 🌸`
        : healthy > 0
          ? `Còn ${remaining} cây khỏe nữa là vườn nở 🌸`
          : 'Làm việc đúng giờ để vườn nở hoa 🌸',
    };
  }, [garden, childName]);

  const gardenSlots = useMemo(() => {
    const unlocked = garden.slice(0, 4);
    const lockedLabels = ['Tưới cây', 'Trồng hoa', 'Cây bí mật'];
    const locked = Array.from(
      { length: Math.max(0, 5 - unlocked.length) },
      (_, i) => ({
        id: `lock-${i}`,
        plant: '🌿',
        badge: '🔒',
        stars: 0,
        label: lockedLabels[i] ?? 'Khóa',
        locked: true as const,
      }),
    );
    return [
      ...unlocked.map((g) => ({ ...g, locked: false as const })),
      ...locked,
    ].slice(0, 5);
  }, [garden]);

  const praiseMoments = useMemo(() => {
    const short = shortChildName(childName);
    const onTime = trulyDone.filter((x) => !x.isLateDone);
    const late = trulyDone.filter((x) => x.isLateDone);
    const picks: string[] = [];

    for (const c of onTime.slice(0, 2)) {
      picks.push(praisePrideLine(short, c, parentRole, flowDate));
    }
    for (const c of late.slice(0, 2)) {
      picks.push(praiseEncouragementLine(short, c, parentRole, nextMission?.title, flowDate));
    }

    if (todayBeautiful && onTime.length > 0) {
      picks.unshift(
        stablePick(`${flowDate}:${short}:beautiful-on`, BEAUTIFUL_DAY_ON_TIME)(short, parentRole),
      );
    } else if (todayBeautiful && late.length > 0 && onTime.length === 0) {
      picks.unshift(
        stablePick(`${flowDate}:${short}:beautiful-late`, BEAUTIFUL_DAY_LATE_ONLY)(
          short,
          parentRole,
        ),
      );
    }

    if (picks.length === 0 && doneCount > 0) {
      if (late.length > 0 && onTime.length === 0) {
        picks.push(
          praiseEncouragementLine(short, late[0], parentRole, nextMission?.title, flowDate),
        );
      } else {
        const Parent = capitalizeParentRole(parentRole);
        const movieNight = [
          `${Parent} rất vui vì ${short} đang giúp cả nhà mở Movie Night! ❤️`,
          `${short} đang giúp cả nhà gần Movie Night hơn — ${parentRole} tự hào! 🎬`,
        ];
        picks.push(stablePick(`${flowDate}:${short}:movie`, movieNight));
      }
    }
    return picks.slice(0, 4);
  }, [trulyDone, todayBeautiful, doneCount, childName, nextMission?.title, flowDate, parentRole]);

  const weekDays = useMemo(
    () => buildWeekStreakDays(flowDate, glanceDays, streak, todayBeautiful),
    [flowDate, glanceDays, streak, todayBeautiful],
  );

  const streakEmpty = useMemo(
    () => streakEmptyCopy(todayGlanceForFlow(flowDate, glanceDays), flowDate),
    [flowDate, glanceDays],
  );

  const streakActiveNote = useMemo(() => {
    const s = shortChildName(childName);
    return stablePick(`${flowDate}:${s}:streak`, STREAK_ACTIVE_NOTES)(s);
  }, [flowDate, childName]);

  useEffect(() => {
    if (!celebrating) return;
    setFoxyGlow(true);
    const t = window.setTimeout(() => setFoxyGlow(false), 1600);
    return () => window.clearTimeout(t);
  }, [celebrating]);

  useEffect(() => {
    setMissionDoneError(null);
  }, [nextMission?.id, tab]);

  const resetEvidenceDraft = () => {
    setEvidenceFile(null);
    if (evidencePreview) URL.revokeObjectURL(evidencePreview);
    setEvidencePreview(null);
    setEvidenceError(null);
  };

  const openAction = (item: DayFlowCommitment) => {
    actionOpenedAt.current = Date.now();
    setActive(item);
    setAskReason(false);
    resetEvidenceDraft();
  };

  /** Opens gallery/camera picker in the same tap (must stay in user gesture). */
  const beginEvidencePick = (item: DayFlowCommitment) => {
    if (busyId === item.id || uploading) return;
    const blocked = earlyCompleteBlockReason(item, localTime);
    if (blocked) {
      setMissionDoneError(blocked);
      return;
    }
    evidencePickTarget.current = item;
    const input = evidenceInputRef.current;
    if (!input) return;
    input.value = '';
    input.click();
  };

  const handleEvidenceInput = (file: File | undefined) => {
    const target = evidencePickTarget.current;
    evidencePickTarget.current = null;
    if (!file) return;

    if (active && (!target || target.id === active.id)) {
      pickEvidence(file);
      return;
    }
    if (!target) return;

    actionOpenedAt.current = Date.now();
    setActive(target);
    setAskReason(false);
    setEvidenceError(null);
    pickEvidence(file);
  };

  const handleBackdropPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    backdropPointerDown.current = e.target === e.currentTarget;
  };

  const handleBackdropClose = (e: React.MouseEvent<HTMLDivElement>) => {
    // Ignore ghost taps/clicks that opened the sheet (common on touch).
    if (e.target !== e.currentTarget || !backdropPointerDown.current) return;
    if (Date.now() - actionOpenedAt.current < 450) return;
    closeAction();
  };

  const quickDoneMission = async (item: DayFlowCommitment) => {
    if (busyId === item.id || uploading) return;
    const blocked = earlyCompleteBlockReason(item, localTime);
    if (blocked) {
      setMissionDoneError(blocked);
      return;
    }
    setMissionDoneError(null);
    try {
      const result = await onDone(item);
      const posted = Boolean(result?.starPosted);
      const delta = posted ? (result?.starDelta ?? 0) : 0;
      setCelebrate({
        title: item.title,
        stars: delta,
        labelVi: posted
          ? result?.starLabelVi
          : result?.evidenceGateLabelVi ??
            (item.commitmentKind === 'study_focus'
              ? result?.evidenceSubmitted || item.evidenceUrl
                ? 'Đã nộp ảnh — chờ bố mẹ xác nhận bài hôm nay'
                : 'Đã ghi — chờ bằng chứng để nhận sao'
              : result?.starLabelVi),
      });
      setFoxyGlow(true);
    } catch (err) {
      const msg =
        err && typeof err === 'object' && 'message' in err
          ? String((err as { message?: string }).message ?? '').trim()
          : '';
      setMissionDoneError(
        msg && msg !== 'commitment_done_failed'
          ? msg
          : 'Chưa lưu được — thử lại nhé.',
      );
    }
  };

  const closeAction = () => {
    setActive(null);
    setAskReason(false);
    resetEvidenceDraft();
  };

  const pickEvidence = (file: File | undefined) => {
    setEvidenceError(null);
    if (evidencePreview) URL.revokeObjectURL(evidencePreview);
    if (!file) {
      setEvidenceFile(null);
      setEvidencePreview(null);
      return;
    }
    if (!/^image\/(jpeg|png|webp)$/i.test(file.type)) {
      setEvidenceError('Chỉ chọn ảnh JPG, PNG hoặc WebP.');
      setEvidenceFile(null);
      setEvidencePreview(null);
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setEvidenceError('Ảnh tối đa 5 MB.');
      setEvidenceFile(null);
      setEvidencePreview(null);
      return;
    }
    setEvidenceFile(file);
    setEvidencePreview(URL.createObjectURL(file));
  };

  const submitDone = async () => {
    if (!active || busyId === active.id || uploading) return;
    const blocked = earlyCompleteBlockReason(active, localTime);
    if (blocked) {
      setEvidenceError(blocked);
      return;
    }
    const finishedTitle = active.title;
    setEvidenceError(null);
    setUploading(true);
    try {
      let url: string | undefined;
      if (evidenceFile) {
        const uploaded = await uploadCommitmentEvidence(
          familyId,
          evidenceFile,
          childMemberId,
        );
        url = uploaded.url;
        if (uploaded.warningMessageVi) {
          setEvidenceSoftWarn(uploaded.warningMessageVi);
        } else {
          setEvidenceSoftWarn(null);
        }
      }
      const result = await onDone(active, url);
      closeAction();
      const posted = Boolean(result?.starPosted);
      const delta = posted ? (result?.starDelta ?? 0) : 0;
      setCelebrate({
        title: finishedTitle,
        stars: delta,
        labelVi: posted
          ? result?.starLabelVi
          : result?.evidenceGateLabelVi ??
            (active.commitmentKind === 'study_focus'
              ? result?.evidenceSubmitted || active.evidenceUrl || url
                ? 'Đã nộp ảnh — chờ bố mẹ xác nhận bài hôm nay'
                : 'Đã ghi — chờ bằng chứng để nhận sao'
              : result?.starLabelVi),
      });
      setFoxyGlow(true);
    } catch {
      setEvidenceError('Chưa gửi được ảnh / lưu xong. Thử lại nhé.');
    } finally {
      setUploading(false);
    }
  };

  const dismissCelebrate = () => {
    setCelebrate(null);
    setFoxyGlow(false);
  };

  const filters: Array<{ key: DayPart; label: string; icon: string; count?: number }> = [
    { key: 'all', label: 'Tất cả', icon: '📋', count: countAll },
    { key: 'morning', label: 'Sáng', icon: '☀️', count: countMorning },
    { key: 'afternoon', label: 'Chiều', icon: '🌤️', count: countAfternoon },
    { key: 'evening', label: 'Tối', icon: '🌙', count: countEvening },
    { key: 'done', label: 'Đã xong', icon: '✅', count: doneItems.length },
  ];

  const gender = useMemo(() => inferGenderFromName(childName), [childName]);
  const avatar = avatarEmoji(gender, 'child');
  const short = shortChildName(childName);
  const foxySpeech = livingFoxyForBand(
    short,
    careBand,
    remaining,
    teamRemaining,
    teamComplete,
    Boolean(celebrate) || foxyGlow,
    nextMission?.title,
  );

  const familyXp = Math.min(500, stars * 2 + doneCount * 40);
  const familyLevel = Math.max(
    1,
    Math.min(4, Math.floor(familyXp / 100) + (stars >= 80 ? 2 : 1)),
  );
  const explorerLevel = Math.max(
    1,
    Math.min(12, familyLevel + Math.floor(stars / 40) + Math.min(4, Math.max(0, streak))),
  );
  const mysteryTarget = 2000;
  const mysteryHave = Math.min(mysteryTarget, Math.max(0, stars));
  const mysteryPct = Math.round((mysteryHave / mysteryTarget) * 100);

  const redeemCatalog = useMemo(() => {
    return rewardCatalog.map((item, idx) => {
      const tone = item.tone || CATALOG_TONES[idx % CATALOG_TONES.length];
      const isSpecial = Boolean(item.isSpecial) || item.cost == null;
      const cost = item.cost ?? null;
      const canAfford = cost != null && stars >= cost;
      const canRedeem = canAfford && !isSpecial;
      const ctaLabel = isSpecial
        ? ('Bố mẹ giữ' as const)
        : canRedeem
          ? ('Đổi ngay' as const)
          : ('Chưa đủ sao' as const);
      return {
        ...item,
        tone,
        isSpecial,
        canAfford,
        canRedeem,
        ctaLabel,
      };
    });
  }, [rewardCatalog, stars]);

  const lifetimeBloomDays = useMemo(() => {
    const days = new Set<string>();
    for (const m of savedMemories) {
      if (isGardenBloomMemory(m)) days.add(m.flowDate.slice(0, 10));
    }
    if (gardenBloom.reached && flowDate) days.add(flowDate.slice(0, 10));
    return days.size;
  }, [savedMemories, gardenBloom.reached, flowDate]);

  const lifetimeReadDays = useMemo(() => {
    const days = new Set<string>();
    for (const m of savedMemories) {
      if (/đọc|sách/i.test(m.titleVi)) days.add(m.flowDate.slice(0, 10));
    }
    if (flowDate) {
      for (const c of trulyDone) {
        if (c.status === 'done' && /đọc|sách/i.test(c.title)) {
          days.add(flowDate.slice(0, 10));
          break;
        }
      }
    }
    return days.size;
  }, [savedMemories, trulyDone, flowDate]);

  const teamChampionUnlocked = useMemo(
    () =>
      unlockPct >= 100 ||
      teamUnlocks.some(
        (u) => u.status === 'confirmed' && !isSiblingComboUnlock(u.rewardCode),
      ),
    [unlockPct, teamUnlocks],
  );

  const todayTeamRewardLabel = useMemo(() => {
    const sameDay = teamUnlocks.filter(
      (u) =>
        (!flowDate || u.flowDate === flowDate) && !isSiblingComboUnlock(u.rewardCode),
    );
    const hit =
      sameDay.find((u) => u.status === 'pending_confirm' || u.status === 'confirmed') ??
      sameDay[0];
    const label = hit?.labelVi?.trim();
    if (label && isMovieNightUnlock(hit!)) return 'Movie Night';
    if (label) return label;
    return 'Phần thưởng nhóm';
  }, [teamUnlocks, flowDate]);

  const kidBadges = useMemo(() => {
    const hasRedeem = redemptions.length > 0;
    const bloomUnlocked = lifetimeBloomDays > 0;
    return [
      {
        id: 'garden-bloom',
        icon: '🌸',
        label: 'Vườn nở',
        unlocked: bloomUnlocked,
        progress: bloomUnlocked ? 100 : gardenBloom.pct,
        hint: bloomUnlocked
          ? lifetimeBloomDays > 1
            ? `Đã nở ${lifetimeBloomDays} ngày — giữ nhịp nhé!`
            : 'Đã mở khóa nhờ đủ cây khỏe trong ngày!'
          : gardenBloom.reached
            ? 'Hôm nay vườn vừa nở!'
            : `Còn ${gardenBloom.remaining} cây khỏe nữa hôm nay`,
      },
      {
        id: 'first',
        icon: '🎁',
        label: 'Đổi quà đầu tiên',
        unlocked: hasRedeem,
        progress: hasRedeem ? 100 : 0,
        hint: hasRedeem ? 'Đã mở khóa!' : 'Đổi quà đầu tiên để nhận huy hiệu này',
      },
      {
        id: 'stars100',
        icon: '⭐',
        label: '100 sao',
        unlocked: stars >= 100,
        progress: Math.min(100, Math.round((stars / 100) * 100)),
        hint: stars >= 100 ? 'Đã mở khóa!' : `Còn ${Math.max(0, 100 - stars)}⭐ nữa`,
      },
      {
        id: 'stars500',
        icon: '💎',
        label: '500 sao',
        unlocked: stars >= 500,
        progress: Math.min(100, Math.round((stars / 500) * 100)),
        hint: stars >= 500 ? 'Đã mở khóa!' : `Còn ${Math.max(0, 500 - stars)}⭐ nữa`,
      },
      {
        id: 'streak',
        icon: '🔥',
        label: 'Chuỗi ngày tốt',
        unlocked: streak >= 3,
        progress: Math.min(100, Math.round((streak / 3) * 100)),
        hint: streak >= 3 ? 'Đã mở khóa!' : `Còn ${Math.max(0, 3 - streak)} ngày tốt nữa`,
      },
      {
        id: 'team',
        icon: '🏆',
        label: 'Team Champion',
        unlocked: teamChampionUnlocked,
        progress: teamChampionUnlocked ? 100 : Math.min(100, unlockPct),
        hint: teamChampionUnlocked
          ? 'Đã mở khóa nhờ cả nhà hoàn thành ngày!'
          : `Cả nhà hoàn thành ${Math.max(0, 100 - unlockPct)}% nữa hôm nay`,
      },
    ];
  }, [
    redemptions.length,
    stars,
    streak,
    unlockPct,
    gardenBloom,
    lifetimeBloomDays,
    teamChampionUnlocked,
  ]);

  /** Only earned achievements — locked goals stay out of the treasure strip. */
  const bigAchievements = useMemo(() => {
    const movieTimes = teamUnlocks.filter(
      (u) => u.status === 'confirmed' && isMovieNightUnlock(u),
    ).length;
    const bloomTimes = lifetimeBloomDays;
    const readTimes = lifetimeReadDays;
    const rows: Array<{
      id: string;
      icon: string;
      title: string;
      value: string;
      note: string;
    }> = [];
    if (movieTimes > 0) {
      rows.push({
        id: 'mn',
        icon: '🎬',
        title: 'Movie Night',
        value: `${movieTimes} lần`,
        note: 'Đêm xem phim cả nhà — đã mở!',
      });
    }
    if (readTimes > 0) {
      rows.push({
        id: 'read',
        icon: '📘',
        title:
          parentRole === 'mẹ'
            ? 'Đọc sách cùng mẹ'
            : parentRole === 'bố'
              ? 'Đọc sách cùng bố'
              : 'Đọc sách cùng bố mẹ',
        value: `${readTimes} lần`,
        note: 'Thói quen tuyệt vời!',
      });
    }
    if (bloomTimes > 0) {
      rows.push({
        id: 'garden',
        icon: '🌱',
        title: 'Khu vườn',
        value: `${bloomTimes} lần nở`,
        note:
          gardenBloom.healthy > 0
            ? `${gardenBloom.healthy} cây khỏe hôm nay · plot theo ngày`
            : 'Những lần vườn đã nở — xem lại trong khu vườn.',
      });
    }
    if (stars > 0) {
      rows.push({
        id: 'stars',
        icon: '⭐',
        title: 'Sao · Foxy',
        value: formatStars(stars),
        note: `${starBalanceNote(stars)} · bạn thân của ${short}`,
      });
    }
    return rows;
  }, [
    teamUnlocks,
    lifetimeBloomDays,
    lifetimeReadDays,
    short,
    stars,
    parentRole,
    gardenBloom.healthy,
  ]);

  const earnedBadges = useMemo(
    () => kidBadges.filter((b) => b.unlocked),
    [kidBadges],
  );

  const handleRedeem = async (item: (typeof redeemCatalog)[number]) => {
    if (item.isSpecial || item.cost == null) {
      showTreasureToast('Phần này bố mẹ giữ — con chưa đổi được.');
      return;
    }
    if (!item.canRedeem) {
      showTreasureToast(`Cần thêm ${item.cost - stars}⭐ nữa để đổi «${item.title}».`);
      return;
    }
    if (!childMemberId || redeemBusyId) return;
    setRedeemBusyId(item.id);
    try {
      const result = await redeemReward(familyId, childMemberId, item.id);
      setLocalStars(result.balance);
      onStarBalanceChange?.(result.balance);
      setRedemptions((prev) => [result.redemption, ...prev.filter((r) => r.id !== result.redemption.id)]);
      showTreasureToast(`Đã đổi «${item.title}» — nhờ bố mẹ xác nhận nhé! ⭐`);
    } catch (err) {
      const msg =
        err && typeof err === 'object' && 'response' in err
          ? String((err as { response?: { data?: { message?: string } } }).response?.data?.message ?? '')
          : '';
      showTreasureToast(msg || 'Chưa đổi được — thử lại nhé!');
    } finally {
      setRedeemBusyId(null);
    }
  };

  const showTreasureToast = (msg: string) => {
    setTreasureToast(msg);
    window.setTimeout(() => setTreasureToast(null), 2200);
  };

  const openTreasureSheet = (kind: 'rewards' | 'badges') => {
    treasureSheetOpenedAt.current = Date.now();
    setTreasureSheet(kind);
  };

  const openAchievementSheet = (kind: Exclude<AchievementSheet, null>) => {
    achievementSheetOpenedAt.current = Date.now();
    setAchievementSheet(kind);
  };

  const closeAchievementSheet = () => setAchievementSheet(null);

  const handleAchievementBackdropClose = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target !== e.currentTarget || !backdropPointerDown.current) return;
    if (Date.now() - achievementSheetOpenedAt.current < 450) return;
    closeAchievementSheet();
  };

  const openGardenPane = () => {
    closeAchievementSheet();
    setTab('home');
    setHomePane('garden');
  };

  const remindMovieNight = async () => {
    if (!childMemberId || movieRemindBusy) return;
    setMovieRemindBusy(true);
    try {
      await createChildRequest(familyId, {
        memberId: childMemberId,
        kind: 'movie_night',
        titleVi: 'Nhắc bố/mẹ tổ chức lại Movie Night',
        reasonNote: `${short} muốn cả nhà xem phim lại.`,
        flowDate: flowDate || undefined,
      });
      showTreasureToast(`Đã nhắc ${parentRole} tổ chức lại Movie Night!`);
      closeAchievementSheet();
      setAskReloadTick((n) => n + 1);
    } catch (err) {
      showTreasureToast(getApiErrorMessage(err) || 'Chưa gửi được nhắc — thử lại nhé.');
    } finally {
      setMovieRemindBusy(false);
    }
  };

  const closeTreasureSheet = () => setTreasureSheet(null);

  const handleTreasureBackdropClose = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target !== e.currentTarget || !backdropPointerDown.current) return;
    if (Date.now() - treasureSheetOpenedAt.current < 450) return;
    closeTreasureSheet();
  };

  const showJournalToast = (msg: string) => {
    setJournalToast(msg);
    window.setTimeout(() => setJournalToast(null), 2200);
  };

  const openJournalSheet = (kind: 'memories' | 'moments', filter: KidMemoryFilter = 'all') => {
    journalSheetOpenedAt.current = Date.now();
    setMemoryFilter(filter);
    setJournalSheet(kind);
  };

  const closeJournalSheet = () => setJournalSheet(null);

  const handleJournalBackdropClose = (e: React.MouseEvent<HTMLDivElement>) => {
    if (e.target !== e.currentTarget || !backdropPointerDown.current) return;
    if (Date.now() - journalSheetOpenedAt.current < 450) return;
    closeJournalSheet();
  };

  const journalDays = useMemo(() => {
    const labels = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
    const anchor = flowDate
      ? new Date(`${flowDate.slice(0, 10)}T12:00:00`)
      : new Date();
    return Array.from({ length: 6 }, (_, i) => {
      const d = new Date(anchor);
      d.setDate(anchor.getDate() - (5 - i));
      const isToday = i === 5;
      const dd = String(d.getDate()).padStart(2, '0');
      const mm = String(d.getMonth() + 1).padStart(2, '0');
      return {
        key: `${d.getFullYear()}-${mm}-${dd}`,
        shortLabel: isToday ? `Hôm nay ${dd}/${mm}` : `${labels[d.getDay()]} ${dd}/${mm}`,
        fullLabel: isToday
          ? `Hôm nay, ${dd}/${mm}/${d.getFullYear()}`
          : `${labels[d.getDay()]}, ${dd}/${mm}/${d.getFullYear()}`,
        isToday,
      };
    });
  }, [flowDate]);

  const selectedJournalDay =
    journalDays[Math.min(journalDayIdx, journalDays.length - 1)] ?? journalDays[journalDays.length - 1];

  const journalIsToday = selectedJournalDay.isToday;
  const journalFlowDate = selectedJournalDay.key;

  useEffect(() => {
    if (journalIsToday) {
      setJournalHistoryItems(null);
      setJournalHistoryError(null);
      setJournalHistoryLoading(false);
      return;
    }
    if (!familyId) return;
    let cancelled = false;
    setJournalHistoryLoading(true);
    setJournalHistoryError(null);
    void (async () => {
      try {
        const day = await fetchDayFlow(familyId, journalFlowDate);
        if (cancelled) return;
        if (!day) {
          setJournalHistoryItems([]);
          setJournalHistoryError(null);
          return;
        }
        const scoped = childMemberId
          ? day.commitments.filter((c) => !c.memberId || c.memberId === childMemberId)
          : day.commitments;
        setJournalHistoryItems(scoped);
      } catch {
        if (!cancelled) {
          setJournalHistoryItems([]);
          setJournalHistoryError('Chưa tải được nhật ký ngày này.');
        }
      } finally {
        if (!cancelled) setJournalHistoryLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, journalFlowDate, journalIsToday]);

  const journalSourceItems = journalIsToday ? items : journalHistoryItems ?? [];

  const journalEntries = useMemo(() => {
    const dateKey = journalIsToday ? flowDate : journalFlowDate;
    return [...journalSourceItems]
      .sort((a, b) => {
        const aw = a.windowStart || a.windowEnd || '99:99';
        const bw = b.windowStart || b.windowEnd || '99:99';
        return aw.localeCompare(bw);
      })
      .map((item) => {
        const uxState = kidMissionUxState(item, dateKey);
        const skipped = uxState === 'skipped';
        const wait = uxState === 'awaiting_check';
        const done = uxState === 'done';
        const isLate = done && journalDoneIsLate(item);
        const noteStatus = done
          ? 'done'
          : wait
            ? 'awaiting'
            : skipped
              ? 'skipped'
              : 'pending';
        return {
          item,
          part: dayPartOf(item),
          time: itemTimeLabel(item),
          done,
          wait,
          skipped,
          pending: !done && !skipped && !wait,
          isLate,
          statusLine: done ? journalDoneStatusLine(short, item, dateKey) : null,
          note: journalNote(item, short, parentRole, noteStatus, nextMission?.title),
          reward: commitmentDisplayDelta(item),
          lateCaption: isLate ? lateStarCaption(item, dateKey) : null,
        };
      });
  }, [
    journalSourceItems,
    short,
    flowDate,
    journalFlowDate,
    journalIsToday,
    nextMission?.title,
    parentRole,
  ]);

  const familyMemories = useMemo(
    () =>
      buildFamilyMemories({
        childShort: short,
        redemptions,
        teamUnlocks,
        doneItems: trulyDone,
        saved: savedMemories,
        voice: 'kid',
      }),
    [short, redemptions, teamUnlocks, trulyDone, savedMemories],
  );

  const parentVoiceAlbum = useMemo((): FamilyMemory[] => {
    return parentVoiceInbox
      .slice()
      .sort((a, b) => (b.sentAt || b.flowDate).localeCompare(a.sentAt || a.flowDate))
      .map((v) => ({
        id: `voice-album-${v.id}`,
        icon: parentVoiceIcon(v.templateCode),
        title: `Lời từ ${v.fromMemberName.trim() || 'bố/mẹ'}`,
        date: formatMemoryDate(v.sentAt || v.flowDate),
        sortAt: Date.parse(v.sentAt || `${v.flowDate}T12:00:00`) || 0,
        filterKind: 'parent_voice',
        isNew: v.status === 'sent',
      }));
  }, [parentVoiceInbox]);

  const albumMemories = useMemo(() => {
    const savedVoiceTitles = new Set(
      familyMemories
        .filter((m) => m.filterKind === 'parent_voice' || m.entry?.kind === 'parent_voice')
        .map((m) => `${m.title}|${m.date}`),
    );
    const extra = parentVoiceAlbum.filter(
      (v) => !savedVoiceTitles.has(`${v.title}|${v.date}`),
    );
    return [...familyMemories, ...extra].sort((a, b) => (b.sortAt ?? 0) - (a.sortAt ?? 0));
  }, [familyMemories, parentVoiceAlbum]);

  const filteredSheetMemories = useMemo(
    () => albumMemories.filter((m) => matchesKidMemoryFilter(m, memoryFilter)),
    [albumMemories, memoryFilter],
  );


  const kidMomentsToday = useMemo(
    () =>
      savedMemories.filter(
        (m) => m.kind === 'kid_moment' && m.memberId === childMemberId && m.flowDate === flowDate,
      ),
    [savedMemories, childMemberId, flowDate],
  );
  const kidMomentsRemainingToday = Math.max(0, 3 - kidMomentsToday.length);

  /** AI-style journal story — memorable highlights, not chore counts */
  const journalStoryParagraphs = useMemo(() => {
    const paras: string[] = [];
    const doneTitles = journalIsToday
      ? trulyDone.map((c) => c.title)
      : journalEntries.filter((e) => e.done).map((e) => e.item.title);
    const brush = doneTitles.find((t) => /đánh răng/i.test(t));
    const love = parentVoiceInbox[0];
    const badge = earnedBadges[0];
    const hasPhoto =
      kidMomentsToday.some((m) => m.photoUrl) ||
      journalEntries.some((e) => Boolean(e.item.evidenceUrl));
    const hasHighlight = Boolean(brush || love || badge || hasPhoto || doneTitles.length > 0);

    if (hasHighlight) {
      paras.push(
        journalIsToday
          ? `Hôm nay ${short} đã thật tuyệt!`
          : `${short} đã có một ngày đáng nhớ.`,
      );
    }
    if (brush) {
      paras.push(
        journalIsToday
          ? `Con đã tự giác đánh răng mà không cần nhắc.`
          : `${short} đã tự giác đánh răng trong ngày này.`,
      );
    }
    if (love) {
      const from = (love.fromMemberName || parentRole).trim();
      paras.push(`${from} cũng khen con rất nhiều! Fami giữ lời yêu thương này cho ${short}.`);
    }
    if (badge) {
      paras.push(`Con mở được huy hiệu «${badge.label}».`);
    }
    if (hasPhoto && paras.length < 4) {
      paras.push(`Còn một khoảnh khắc ảnh đẹp — đáng để xem lại sau này.`);
    }
    if (!brush && !love && !badge && doneTitles.length > 0) {
      paras.push(
        `Con đã hoàn thành «${doneTitles[0]}» — một bước nhỏ trong hành trình lớn lên.`,
      );
    }
    if (paras.length === 0) {
      paras.push(
        journalIsToday
          ? `${short} ơi — khi con làm việc tốt, nhận lời khen hoặc có ảnh đẹp, Fami sẽ kể chuyện ở đây.`
          : `Ngày này chưa có câu chuyện đáng nhớ được lưu.`,
      );
    } else if (journalIsToday) {
      paras.push(`Fami rất tự hào về ${short}! 🥰`);
    }
    return paras.slice(0, 5);
  }, [
    journalIsToday,
    trulyDone,
    journalEntries,
    parentVoiceInbox,
    earnedBadges,
    kidMomentsToday,
    short,
    parentRole,
  ]);

  /** Day timeline for diary — chronological highlights */
  const journalMilestones = useMemo(() => {
    type Row = {
      id: string;
      icon: string;
      time: string;
      title: string;
      tag: string;
      done: boolean;
      sort: string;
    };

    const tagFor = (title: string, evidence?: string | null) => {
      if (evidence) return 'Ảnh';
      if (/đánh răng|tự giác/i.test(title)) return 'Tự giác';
      if (/ăn sáng|ăn trưa|ăn tối|uống sữa/i.test(title)) return 'Ngoan';
      if (/cặp|balo|đi học|chuẩn bị/i.test(title)) return 'Sẵn sàng';
      if (/đọc/i.test(title)) return '20 phút';
      if (/ngủ|đi ngủ/i.test(title)) return 'Giỏi quá!';
      if (/giúp|dọn/i.test(title)) return 'Giỏi';
      return 'Xong';
    };

    const fromTasks = journalEntries
      .filter((e) => e.done)
      .map((e) => {
        const title = e.item.title;
        return {
          id: `tl-${e.item.id}`,
          icon: taskIcon(title),
          time: e.time || '--:--',
          title,
          tag: tagFor(title, e.item.evidenceUrl),
          done: true,
          sort: `${journalFlowDate}T${e.time || '00:00'}:00`,
        } satisfies Row;
      })
      .sort((a, b) => a.sort.localeCompare(b.sort));

    if (fromTasks.length > 0) {
      return fromTasks.slice(0, 5).map(({ sort: _s, ...rest }) => rest);
    }

    const rows: Row[] = [];
    for (const v of parentVoiceInbox.slice(0, 2)) {
      const from = (v.fromMemberName || parentRole).trim();
      rows.push({
        id: `ms-love-${v.id}`,
        icon: '💌',
        time: v.sentAt ? v.sentAt.slice(11, 16) : 'Hôm nay',
        title: `${from} gửi lời yêu thương`,
        tag: 'Thư',
        done: true,
        sort: v.sentAt || `${v.flowDate}T12:00:00`,
      });
    }
    for (const m of kidMomentsToday.slice(0, 2)) {
      rows.push({
        id: `ms-moment-${m.id}`,
        icon: '📷',
        time: (m.happenedAt || '').slice(11, 16) || 'Ảnh',
        title: m.titleVi || 'Khoảnh khắc của con',
        tag: 'Ảnh',
        done: true,
        sort: m.happenedAt || m.flowDate || '',
      });
    }
    rows.sort((a, b) => a.sort.localeCompare(b.sort));
    return rows.slice(0, 5).map(({ sort: _s, ...rest }) => rest);
  }, [
    journalEntries,
    journalFlowDate,
    parentVoiceInbox,
    parentRole,
    kidMomentsToday,
  ]);

  const memoryFilterChips = useMemo(() => {
    const chips: Array<[KidMemoryFilter, string]> = [
      ['all', 'Tất cả'],
      ['team_unlock', 'Movie Night'],
    ];
    if (parentVoiceAlbum.length > 0) chips.push(['parent_voice', 'Lời bố mẹ']);
    if (kidMomentsToday.length > 0) chips.push(['kid_moment', 'Khoảnh khắc']);
    chips.push(['beautiful_day', 'Vườn / ngày đẹp']);
    return chips;
  }, [parentVoiceAlbum.length]);

  useEffect(() => {
    if (memoryFilter === 'parent_voice' && parentVoiceAlbum.length === 0) {
      setMemoryFilter('all');
    }
  }, [memoryFilter, parentVoiceAlbum.length]);

  const confirmedMovieUnlocks = useMemo(
    () =>
      teamUnlocks
        .filter((u) => u.status === 'confirmed' && isMovieNightUnlock(u))
        .slice()
        .sort((a, b) => (b.confirmedAt || b.flowDate).localeCompare(a.confirmedAt || a.flowDate)),
    [teamUnlocks],
  );

  const gardenBloomHistory = useMemo(
    () =>
      savedMemories
        .filter(isGardenBloomMemory)
        .slice()
        .sort((a, b) => (b.happenedAt || b.flowDate).localeCompare(a.happenedAt || a.flowDate))
        .slice(0, 5),
    [savedMemories],
  );

  useEffect(() => {
    if (!gardenBloom.reached || !familyId || !childMemberId || !flowDate) return;
    const already = savedMemories.some(
      (m) => m.flowDate.slice(0, 10) === flowDate.slice(0, 10) && isGardenBloomMemory(m),
    );
    if (already) return;
    const key = `${familyId}:${childMemberId}:${flowDate}`;
    if (bloomCaptureKey.current === key) return;
    bloomCaptureKey.current = key;
    const titleVi = `Vườn của ${short} đã nở`;
    void createFamilyMemory(familyId, {
      titleVi,
      flowDate,
      memberId: childMemberId,
      kind: 'first_time',
      noteVi: 'Đủ cây khỏe trong ngày — vườn nở hoa!',
      icon: '🌸',
    })
      .then((created) => {
        setSavedMemories((prev) => {
          if (prev.some((m) => m.id === created.id)) return prev;
          if (prev.some((m) => m.flowDate.slice(0, 10) === flowDate.slice(0, 10) && isGardenBloomMemory(m)))
            return prev;
          return [created, ...prev];
        });
      })
      .catch(() => {
        bloomCaptureKey.current = null;
      });
  }, [
    gardenBloom.reached,
    familyId,
    childMemberId,
    flowDate,
    savedMemories,
    short,
  ]);

  const journalMemoriesVisible = useMemo(
    () => familyMemories.slice(0, FAMILY_MEMORY_VISIBLE),
    [familyMemories],
  );

  const journalFeatureMoments = useMemo(
    () =>
      familyMemories.slice(0, 5).map((m) => ({
        id: m.id,
        icon: m.icon,
        title: m.title,
        date: m.date,
        caption: m.pending ? 'Chờ bố mẹ xác nhận' : 'Kỷ niệm gia đình',
      })),
    [familyMemories],
  );

  const treasureMemoriesVisible = journalMemoriesVisible;

  useEffect(() => {
    setMomentIdx(0);
  }, [journalFeatureMoments.length, flowDate]);

  const todayStarsEarned = trulyDone.reduce(
    (sum, c) => sum + (c.starPosted ? (c.starDelta ?? 0) : 0),
    0,
  );

  const createdBits = useMemo(() => {
    const bits: Array<{ icon: string; text: string }> = [];
    if (gardenBloom.healthy > 0) {
      bits.push({
        icon: '🌱',
        text: `Khu vườn của ${short} có ${gardenBloom.healthy} cây khỏe hôm nay!`,
      });
    } else if (doneCount > 0) {
      bits.push({
        icon: '🌱',
        text: `Khu vườn của ${short} đã có cây — làm đúng giờ để cây khỏe hơn nhé!`,
      });
    }
    if (todayStarsEarned > 0) {
      bits.push({
        icon: '⭐',
        text: `Hôm nay con kiếm được ${formatStarDelta(todayStarsEarned)} sao!`,
      });
    }
    return bits.slice(0, 2);
  }, [gardenBloom.healthy, doneCount, short, todayStarsEarned]);

  /** One unread voice at a time; rest stay queued until the current is acked. */
  const unreadParentVoices = useMemo(() => {
    return parentVoiceInbox
      .filter((v) => v.status === 'sent')
      .slice()
      .sort((a, b) => a.sentAt.localeCompare(b.sentAt));
  }, [parentVoiceInbox]);
  const primaryParentVoice = unreadParentVoices[0] ?? null;
  const queuedParentVoiceCount = Math.max(0, unreadParentVoices.length - 1);

  const openStackWarmth = useMemo(() => {
    void openSeqTick;
    return buildWarmthPulse({
      role: 'child',
      flowDate,
      memberId: childMemberId || 'child',
      unreadParentVoice: primaryParentVoice,
      memories: savedMemories,
      weeklyStory,
    });
  }, [
    openSeqTick,
    flowDate,
    childMemberId,
    primaryParentVoice,
    savedMemories,
    weeklyStory,
  ]);

  const openStackPending = useMemo(
    () =>
      buildPendingActions({
        role: 'child',
        commitments: items,
        unreadParentVoice: primaryParentVoice,
      }),
    [items, primaryParentVoice],
  );

  const openStackSeen = useMemo(
    () =>
      buildSeenSignals({
        role: 'child',
        unreadParentVoice: primaryParentVoice,
        commitments: items,
      }),
    [primaryParentVoice, items],
  );

  const openStackYarn = useMemo(() => {
    void openSeqTick;
    return buildMemoryYarn({
      role: 'child',
      flowDate,
      memberId: childMemberId || 'child',
      commitments: items,
      memories: savedMemories,
    });
  }, [openSeqTick, flowDate, childMemberId, items, savedMemories]);

  const openStackRitualDone = useMemo(() => {
    void openSeqTick;
    return isRitualDone(childMemberId || 'child', flowDate);
  }, [openSeqTick, childMemberId, flowDate]);

  const handleOpenStackCta = (ev: TodayOpenCtaEvent) => {
    if (ev.kind === 'dismiss' || ev.kind === 'dismiss_thanks') {
      dismissWarmth(childMemberId || 'child', flowDate);
      setOpenSeqTick((n) => n + 1);
      return;
    }
    if (ev.kind === 'ack_parent_voice' && ev.id) {
      void ackVoiceMessage(ev.id, 'thanks');
      return;
    }
    if (ev.kind === 'open_memory') {
      setTab('log');
      return;
    }
    if (ev.kind === 'scroll_missions') {
      setTab('tasks');
      window.requestAnimationFrame(() => {
        document.querySelector('.kv2-missions')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      });
    }
  };

  const handleOpenStackRitual = async (moodCode: string, warmLineVi: string) => {
    if (!childMemberId || ritualBusy) return;
    setRitualBusy(true);
    try {
      await upsertMemberMood(familyId, childMemberId, {
        flowDate,
        moodCode,
        note: warmLineVi,
      });
      await sendChildGratitude(familyId, {
        fromMemberId: childMemberId,
        flowDate,
        messageVi: warmLineVi,
        praiseContext: 'Nghi thức ấm đầu ngày',
      });
      markRitualDone(childMemberId, flowDate);
      setOpenSeqTick((n) => n + 1);
      setTreasureToast('Đã gửi ấm cho nhà 💛');
    } catch {
      setTreasureToast('Chưa gửi được — thử lại nhé');
    } finally {
      setRitualBusy(false);
    }
  };

  /** One unread sibling nudge at a time (align with parent-voice queue). */
  const unreadSiblingNudges = useMemo(() => {
    return inboxNudges
      .filter((n) => n.status === 'sent')
      .slice()
      .sort((a, b) => (a.sentAt || a.createdAt).localeCompare(b.sentAt || b.createdAt));
  }, [inboxNudges]);
  const primarySiblingNudge = unreadSiblingNudges[0] ?? null;
  const queuedSiblingNudgeCount = Math.max(0, unreadSiblingNudges.length - 1);

  const foxyHomeLine = useMemo(() => {
    const unreadVoice = primaryParentVoice;
    if (unreadVoice) {
      return parentVoiceHomeLineForBand(
        unreadVoice.fromMemberName,
        short,
        careBand,
      );
    }
    if (kidPrimaryTrigger?.code === 'parent_voice_inbox') {
      return `${kidPrimaryTrigger.titleVi} — Famixa chỉ chuyển lời, không nói thay.`;
    }
    if (kidPrimaryTrigger && isCheerSiblingTrigger(kidPrimaryTrigger.code)) {
      return kidPrimaryTrigger.titleVi;
    }
    if (kidPrimaryTrigger && isThankParentTrigger(kidPrimaryTrigger.code) && !thanksSent) {
      return kidPrimaryTrigger.bodyVi;
    }
    const hot =
      doNowItems[0] ?? items.find((c) => c.status !== 'done' && c.status !== 'skipped');
    if (hot?.motivationCueVi) return hot.motivationCueVi;
    if (hot?.reminderSuppressed || hot?.interventionLevel === 'observe_only') {
      return `Foxy tin ${short} tự làm được — ít nhắc hơn hôm nay.`;
    }
    return foxySpeech;
  }, [
    primaryParentVoice,
    careBand,
    kidPrimaryTrigger,
    thanksSent,
    doNowItems,
    items,
    short,
    foxySpeech,
  ]);

  const movieStripLabel = useMemo(() => {
    if (teamMissionLine?.trim()) {
      return teamMissionLine.replace(/^[🎯🎉]\s*/, '');
    }
    if (teamComplete || unlockLeft === 0) return 'Sẵn sàng mở khóa!';
    if (unlockLeft === 1) return 'Cả đội còn 1 việc nữa để hoàn thành ngày hôm nay.';
    return `Cả đội còn ${unlockLeft} việc nữa để hoàn thành ngày hôm nay.`;
  }, [teamComplete, unlockLeft, teamMissionLine]);

  useEffect(() => {
    if (tab !== 'home') setHomePane('hub');
  }, [tab]);

  const openHomePane = (pane: KidHomePane) => setHomePane(pane);
  const backHomeHub = () => setHomePane('hub');

  const praiseLine =
    praiseMoments[0] ??
    (trulyDone.some((c) => c.isLateDone) && !trulyDone.some((c) => !c.isLateDone)
      ? praiseEncouragementLine(
          short,
          trulyDone.find((c) => c.isLateDone)!,
          parentRole,
          nextMission?.title,
          flowDate,
        )
      : stablePick(`${flowDate}:${short}:fallback`, PRAISE_FALLBACK)(short, parentRole));

  useEffect(() => {
    if (!familyId || !childMemberId || !flowDate) {
      setThanksSent(false);
      return;
    }
    let cancelled = false;
    void fetchChildGratitude(familyId, flowDate, childMemberId)
      .then((rows) => {
        if (!cancelled) setThanksSent(rows.length > 0);
      })
      .catch(() => {
        if (!cancelled) setThanksSent(false);
      });
    return () => {
      cancelled = true;
    };
  }, [familyId, childMemberId, flowDate]);

  const sendThanks = async () => {
    if (!childMemberId || thanksSending || thanksSent) return;
    setThanksSending(true);
    setThanksError(null);
    try {
      const res = await sendChildGratitude(familyId, {
        fromMemberId: childMemberId,
        flowDate: flowDate || undefined,
        praiseContext: praiseLine,
      });
      setThanksSent(true);
      {
        const Parent = capitalizeParentRole(parentRole);
        setTreasureToast(
          res.alreadySent
            ? stablePick(`${flowDate}:thanks-already`, [
                `${Parent} đã nhận lời cảm ơn hôm nay rồi! 💖`,
                `${Parent} biết con cảm ơn rồi — mai nói thêm nhé! 💖`,
              ])
            : stablePick(`${flowDate}:thanks-sent`, [
                `Đã gửi lời cảm ơn tới ${parentRole}! 💖`,
                `${Parent} nhận được lời cảm ơn của con rồi! 💖`,
              ]),
        );
      }
    } catch {
      setThanksError('Chưa gửi được — thử lại nhé.');
    } finally {
      setThanksSending(false);
    }
  };

  const sendThanksSticker = async (emoji: string) => {
    if (!childMemberId || thanksSending || thanksSent) return;
    setThanksSending(true);
    setThanksError(null);
    try {
      const res = await sendChildGratitude(familyId, {
        fromMemberId: childMemberId,
        flowDate: flowDate || undefined,
        praiseContext: `Sticker ${emoji} từ ${short}`,
      });
      setThanksSent(true);
      {
        const Parent = capitalizeParentRole(parentRole);
        setTreasureToast(
          res.alreadySent
            ? `${Parent} đã nhận lời cảm ơn hôm nay rồi! ${emoji}`
            : `Đã gửi sticker ${emoji} tới ${parentRole}!`,
        );
      }
    } catch {
      setThanksError('Chưa gửi được — thử lại nhé.');
    } finally {
      setThanksSending(false);
    }
  };


  // Keep drill/open-stack helpers live for future panes (home hub uses mock layout).
  void [
    formatLongDate,
    dayPartLabel,
    voiceAckBusy,
    eveningCircle,
    nudgeAckBusy,
    showCheerOffer,
    cheerOfferCopy,
    openCheerSheet,
    ackInboxNudge,
    sendThanksBack,
    submitKidEveningCircle,
    openWeekReview,
    todayComboUnlock,
    pendingAskCount,
    queuedParentVoiceCount,
    openStackWarmth,
    openStackPending,
    openStackSeen,
    openStackYarn,
    openStackRitualDone,
    handleOpenStackCta,
    handleOpenStackRitual,
    primarySiblingNudge,
    queuedSiblingNudgeCount,
    movieStripLabel,
    filter,
    setFilter,
    filters,
    nowOpen,
    setNowOpen,
    waitOpen,
    setWaitOpen,
    soonOpen,
    setSoonOpen,
    filteredDone,
    soonItems,
    waitingCheckItems,
    missionSegs,
    missionFilled,
    countMorning,
    countAfternoon,
    countEvening,
    countAll,
    minutesUntilExcited,
    taskTip,
    MissionStarBadge,
    onSelfStart,
    dayClosed,
    morningNote,
    todayTeamRewardLabel,
    journalFeatureMoments,
    momentIdx,
    setMomentIdx,
    kidSkipLabel,
    openHomePane,
  ];

  return (
    <section className={`kid-home kid-v2${celebrating || celebrate ? ' is-pop' : ''}`}>
      {softLockActive ? (
        <div className="kh-soft-lock" role="status">
          <div className="kh-soft-lock-inner">
            <span aria-hidden>🔒</span>
            <div>
              <strong>Tạm khóa theo thỏa thuận nhà</strong>
              <p>
                {softLockLabel
                  ? `Đang áp dụng: ${softLockLabel}. `
                  : 'Đang áp dụng thỏa thuận màn hình. '}
                Con vẫn làm việc được. Đổi người cần mã bố mẹ.
              </p>
            </div>
            <button type="button" className="pill" onClick={onOpenParentPin}>
              Mã bố mẹ
            </button>
          </div>
        </div>
      ) : null}

      <header className="kv2-top">
        <div className="kv2-identity">
          <div className={`kv2-avatar ${avatarToneClass(gender)}`} aria-hidden>
            {avatar}
          </div>
          <div className="kv2-identity-copy">
            {tab === 'tasks' ? (
              <>
                <h1 className="kv2-hello">
                  Kế hoạch <span aria-hidden>☀️</span>
                </h1>
                <p className="kv2-date">Chào {short} — ngày đẹp để tỏa sáng</p>
              </>
            ) : tab === 'rewards' ? (
              <>
                <h1 className="kv2-hello">
                  Kho báu <span aria-hidden>✨</span>
                </h1>
                <p className="kv2-date">Nỗ lực của {short} rất đáng tự hào</p>
              </>
            ) : tab === 'family' ? (
              <>
                <h1 className="kv2-hello">
                  Gia đình <span aria-hidden>▾</span>
                </h1>
                <p className="kv2-date">Cùng nhau yêu thương — nhà {short}</p>
              </>
            ) : tab === 'log' ? (
              <>
                <h1 className="kv2-hello">
                  Nhật ký <span aria-hidden>✨</span>
                </h1>
                <p className="kv2-date">Mỗi ngày một bước tiến của {short}</p>
              </>
            ) : (
              <>
                <h1 className="kv2-hello">
                  Chào {short}! <span aria-hidden>👋</span>
                </h1>
                <p className="kv2-date">Fami vui khi gặp con</p>
              </>
            )}
          </div>
        </div>

        {tab === 'tasks' ? (
          <button
            type="button"
            className="kv2-switch kv2-gift"
            aria-label="Phần thưởng"
            title="Phần thưởng"
            onClick={() => setTab('rewards')}
          >
            <span aria-hidden>🎁</span>
          </button>
        ) : tab === 'rewards' ? (
          <button
            type="button"
            className="kv2-switch kv2-gear"
            aria-label="Cài đặt bố mẹ"
            title="Bố mẹ"
            onClick={onOpenParentPin}
          >
            <span aria-hidden>⚙️</span>
          </button>
        ) : (
          <button
            type="button"
            className={`kv2-switch kv2-bell${
              unreadParentVoices.length + unreadSiblingNudges.length > 0 ? ' has-dot' : ''
            }`}
            aria-label="Đổi sang bố mẹ"
            title="Bố mẹ"
            onClick={onOpenParentPin}
          >
            <span aria-hidden>👤</span>
          </button>
        )}

        <div className="kv2-top-pills">
          <span className="kv2-pill kv2-stars" title={`Sao của ${short}: ${formatStars(stars)}`}>
            <span aria-hidden>⭐</span>
            <strong>{formatStars(stars)}</strong>
          </span>
          {tab === 'rewards' ? (
            <span className="kv2-pill kv2-level" title="Quà bí mật từ Fami">
              <span aria-hidden>🎁</span>
              <strong>Quà bí mật</strong>
            </span>
          ) : tab === 'log' ? (
            <button
              type="button"
              className="kv2-pill kv2-streak"
              title="Chọn ngày nhật ký"
              aria-haspopup="dialog"
              aria-expanded={diaryDayPickerOpen}
              onClick={() => setDiaryDayPickerOpen(true)}
            >
              <span aria-hidden>📅</span>
              <strong>
                {selectedJournalDay.isToday
                  ? 'Hôm nay'
                  : selectedJournalDay.shortLabel.replace(/^.*?(\d{2}\/\d{2}).*$/, '$1')}
              </strong>
            </button>
          ) : tab === 'tasks' ? null : (
            <span
              className="kv2-pill kv2-movie-mini"
              title={`Kế hoạch nhóm hôm nay: ${unlockPct}%`}
            >
              <span aria-hidden>👨‍👩‍👧‍👦</span>
              <i className="kv2-mini-bar" aria-hidden>
                <b style={{ width: `${unlockPct}%` }} />
              </i>
              <strong>{unlockPct}%</strong>
            </span>
          )}
        </div>
      </header>

      {tab === 'home' ? (
        <div className="kv2-home">
          {homePane !== 'hub' ? (
            <header className="kv2-home-drill-head">
              <button type="button" className="kv2-home-back" onClick={backHomeHub}>
                ‹ Quay lại
              </button>
              <strong>
                {homePane === 'praise'
                  ? 'Lời khen hôm nay'
                  : homePane === 'streak'
                    ? `Chuỗi ngày tốt của ${short}`
                    : homePane === 'garden'
                      ? `Khu vườn của ${short}`
                      : homePane === 'ask'
                        ? `Xin ${parentRole}`
                        : 'Thử thách tuần này'}
              </strong>
            </header>
          ) : null}

          {homePane === 'hub' ? (
            <>
              {childMemberId ? (
                <DailyMirrorEmptyPanel
                  familyId={familyId}
                  childShort={short}
                  childMemberId={childMemberId}
                  parentLabel={parentRole}
                  viewer="kid"
                  compact
                />
              ) : null}
            <KidHomeHub
              shortName={short}
              parentRole={parentRole}
              remaining={remaining}
              doneCount={doneCount}
              total={total}
              unlockLeft={unlockLeft}
              explorerLevel={explorerLevel}
              streak={streak}
              levelProgress={{
                have: stars % 40,
                need: 40,
              }}
              badgeUnlocked={doneCount >= 3 || streak >= 3}
              nextMission={nextMission}
              todayItems={[...trulyDone, ...pendingItems].sort((a, b) => {
                const aw = a.windowStart || a.windowEnd || '99:99';
                const bw = b.windowStart || b.windowEnd || '99:99';
                return aw.localeCompare(bw);
              })}
              busyId={busyId}
              uploading={uploading}
              missionDoneError={missionDoneError}
              primaryParentVoice={primaryParentVoice}
              thanksSent={thanksSent}
              thanksSending={thanksSending}
              foxyLine={foxyHomeLine}
              taskIcon={taskIcon}
              durationLabel={(item) => {
                if (!item.windowStart || !item.windowEnd) return null;
                const [sh, sm] = item.windowStart.slice(0, 5).split(':').map(Number);
                const [eh, em] = item.windowEnd.slice(0, 5).split(':').map(Number);
                if (![sh, sm, eh, em].every(Number.isFinite)) return null;
                const mins = eh * 60 + em - (sh * 60 + sm);
                if (mins > 0 && mins <= 180) return `${mins} phút`;
                return null;
              }}
              clockLabel={(item) =>
                formatWindow(item.windowStart, item.windowEnd) ??
                (item.windowStart ? item.windowStart.slice(0, 5) : 'Trong ngày')
              }
              starRewardOf={(item) => item.projectedStarDelta ?? item.starReward ?? 10}
              studyNeedsEvidence={studyNeedsEvidence}
              onStartNow={() => {
                if (nextMission && canCompleteNow(nextMission, localTime)) {
                  if (studyNeedsEvidence(nextMission)) {
                    beginEvidencePick(nextMission);
                    return;
                  }
                  void quickDoneMission(nextMission);
                  return;
                }
                if (nextMission) {
                  openAction(nextMission);
                  return;
                }
                setTab('tasks');
              }}
              onStartNext={() => {
                if (!nextMission) return;
                if (studyNeedsEvidence(nextMission)) {
                  beginEvidencePick(nextMission);
                  return;
                }
                if (canCompleteNow(nextMission, localTime)) {
                  void quickDoneMission(nextMission);
                  return;
                }
                openAction(nextMission);
              }}
              onOpenAllToday={() => setTab('tasks')}
              onStartItem={(item) => {
                if (studyNeedsEvidence(item)) {
                  beginEvidencePick(item);
                  return;
                }
                if (canCompleteNow(item, localTime)) {
                  void quickDoneMission(item);
                  return;
                }
                openAction(item);
              }}
              onOpenAchievements={() => openTreasureSheet('badges')}
              onSendSticker={(emoji) => void sendThanksSticker(emoji)}
              onOpenMoments={() => setTab('log')}
              onOpenSurprise={() => setTab('rewards')}
              onAckVoiceThanks={() => {
                if (primaryParentVoice) {
                  void ackVoiceMessage(primaryParentVoice.id, 'thanks');
                }
                setTab('log');
              }}
              momentPreview={(() => {
                const photo =
                  kidMomentsToday.find((m) => m.photoUrl)?.photoUrl ??
                  items.find((i) => i.evidenceUrl)?.evidenceUrl ??
                  null;
                const img = photo ? withEvidenceAuth(photo) ?? null : null;
                if (primaryParentVoice) {
                  const name = (primaryParentVoice.fromMemberName || parentRole).trim();
                  return {
                    title: `${name} vừa khen con`,
                    body: primaryParentVoice.bodyVi,
                    imageUrl: img,
                  };
                }
                const snap = kidMomentsToday[0];
                if (snap) {
                  return {
                    title: snap.titleVi || 'Khoảnh khắc của con',
                    body: snap.noteVi || 'Con vừa lưu một khoảnh khắc dễ thương!',
                    imageUrl: (snap.photoUrl ? withEvidenceAuth(snap.photoUrl) : img) ?? null,
                  };
                }
                const love = parentVoiceInbox[0];
                if (love) {
                  const name = (love.fromMemberName || parentRole).trim();
                  return {
                    title: `${name} vừa nhắn con`,
                    body: love.bodyVi,
                    imageUrl: img,
                  };
                }
                return null;
              })()}
            />
            </>
          ) : null}

          {homePane === 'challenge' && familyId && childMemberId ? (
            <div className="kv2-home-drill">
              <FamilyChallengeCard
                familyId={familyId}
                memberId={childMemberId}
                isParent={false}
                compact
              />
            </div>
          ) : null}

          {homePane === 'praise' ? (
            <div className="kv2-home-drill">
              <article className="kv2-praise">
                <p className="kv2-section-label">
                  <span aria-hidden>❤️</span> LỜI KHEN HÔM NAY
                </p>
                <div className="kv2-praise-bubble">
                  <p>{praiseLine}</p>
                </div>
                <div className="kv2-praise-foot">
                  <span className="kv2-mom" aria-hidden>
                    🦊
                  </span>
                  <span>Foxy kể</span>
                  <button
                    type="button"
                    className={`kv2-thanks${thanksSent ? ' is-sent' : ''}`}
                    disabled={thanksSending || thanksSent}
                    onClick={() => void sendThanks()}
                  >
                    <span aria-hidden>{thanksSent ? '✓' : '💖'}</span>{' '}
                    {thanksSending
                      ? 'Đang gửi…'
                      : thanksSent
                        ? 'Đã gửi'
                        : `Cảm ơn ${parentRole}!`}
                  </button>
                </div>
                {thanksError ? (
                  <p className="kv2-thanks-error" role="alert">
                    {thanksError}
                  </p>
                ) : null}
              </article>
            </div>
          ) : null}

          {homePane === 'streak' ? (
            <div className="kv2-home-drill">
              <article className="kv2-streak">
                <p className="kv2-section-label">
                  <span aria-hidden>🔥</span>{' '}
                  <EnTerm en="Streak" vi={`Chuỗi ngày tốt của ${short}`} />
                </p>
                <h3>{streak > 0 ? `${streak} ngày liên tiếp!` : streakEmpty.headline}</h3>
                <div className="kv2-streak-days">
                  {weekDays.map((d) => (
                    <div
                      key={d.key}
                      className={`kv2-day${d.on ? ' is-on' : ''}${d.isToday ? ' is-today' : ''}`}
                    >
                      <span className="kv2-day-dot" aria-hidden>
                        {d.isToday ? '⭐' : d.on ? '✓' : '·'}
                      </span>
                      <em>{d.isToday ? 'Hôm nay' : d.label}</em>
                    </div>
                  ))}
                </div>
                <p className="kv2-streak-note">
                  {streak > 0 ? streakActiveNote : streakEmpty.note}
                </p>
              </article>
            </div>
          ) : null}

          {homePane === 'garden' ? (
            <div className="kv2-home-drill">
              <section className={`kv2-garden${gardenBloom.reached ? ' is-bloom' : ''}`}>
                <header>
                  <h2>
                    KHU VƯỜN CỦA {short.toUpperCase()}
                    <span
                      className="kv2-help"
                      title="Mỗi việc xong = một cây. Đúng giờ (đủ sao) = cây khỏe. Đủ 3 cây khỏe thì vườn nở!"
                      aria-label="Gợi ý"
                    >
                      ?
                    </span>
                  </h2>
                  <button type="button" className="kv2-text-link" onClick={() => setTab('log')}>
                    Xem nhật ký
                  </button>
                </header>
                <div className={`kv2-bloom${gardenBloom.reached ? ' is-on' : ''}`}>
                  <div className="kv2-bloom-top">
                    <span className="kv2-bloom-face" aria-hidden>
                      {gardenBloom.reached ? '🌸' : '🌱'}
                    </span>
                    <p>{gardenBloom.label}</p>
                  </div>
                  <i className="kv2-bloom-bar" aria-hidden>
                    <b style={{ width: `${gardenBloom.pct}%` }} />
                  </i>
                  <em className="kv2-bloom-meta">
                    {gardenBloom.healthy}/{gardenBloom.goal} cây khỏe
                  </em>
                </div>
                {createdBits.length > 0 ? (
                  <div className="kv2-created-row" style={{ marginBottom: 12 }}>
                    {createdBits.map((b) => (
                      <article key={b.text} className="kv2-created-card">
                        <span aria-hidden>{b.icon}</span>
                        <p>{b.text}</p>
                      </article>
                    ))}
                  </div>
                ) : null}
                <div className="kv2-garden-plot">
                  {gardenSlots.map((g) => (
                    <div
                      key={g.id}
                      className={`kv2-pot${g.locked ? ' is-locked' : ''}${
                        !g.locked && g.mood === 'wilted'
                          ? ' is-wilted'
                          : !g.locked && g.mood === 'neutral'
                            ? ' is-neutral'
                            : ''
                      }`}
                      title={g.label}
                    >
                      <div className="kv2-pot-avatar" aria-hidden>
                        <span
                          className={`kv2-pot-plant${
                            !g.locked && g.mood === 'wilted'
                              ? ' is-wilted'
                              : !g.locked && g.mood === 'neutral'
                                ? ' is-neutral'
                                : ''
                          }`}
                        >
                          {g.plant}
                        </span>
                        <span className="kv2-pot-vessel">🟫</span>
                        <span className="kv2-pot-badge">{g.badge}</span>
                      </div>
                      <em className="kv2-pot-name">{g.label}</em>
                      {g.locked ? (
                        <span className="kv2-pot-meta is-lock">
                          <span aria-hidden>🔒</span> Khóa
                        </span>
                      ) : (
                        <span className={`kv2-pot-meta${starBadgeClass(g.stars)}`}>
                          {formatStarBadge(g.stars)}
                        </span>
                      )}
                    </div>
                  ))}
                </div>
                {gardenBloomHistory.length > 0 ? (
                  <div className="kv2-garden-history">
                    <p className="kv2-garden-history-head">Các lần vườn nở</p>
                    <ul>
                      {gardenBloomHistory.map((m) => (
                        <li key={m.id}>
                          <span aria-hidden>{m.icon || '🌸'}</span>
                          <strong>{m.titleVi}</strong>
                          <em>{formatMemoryDate(m.flowDate || m.happenedAt)}</em>
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : null}
              </section>
            </div>
          ) : null}

          {homePane === 'ask' ? (
            <div className="kv2-home-drill">
              <article className="kv2-praise">
                <p className="kv2-section-label">
                  <span aria-hidden>🙋</span> XIN {parentRole.toUpperCase()}
                </p>
                <p className="kv2-ask-lead">
                  Chọn một việc — {parentRole} sẽ nhận đề xuất và trả lời.
                </p>

                <div
                  className={`kv2-wallet-card${
                    screenWallet?.status === 'active' ? ' is-active' : ''
                  }`}
                >
                  <span aria-hidden>📱</span>
                  <div>
                    {screenWallet?.status === 'active' ? (
                      <>
                        <strong>Ví tuần còn {screenWallet.remainingMinutes} phút</strong>
                        <p>
                          Thỏa thuận nhà — không khóa máy. Xin thêm phút để {parentRole} duyệt.
                        </p>
                      </>
                    ) : (
                      <>
                        <strong>Phút màn hình theo thỏa thuận nhà</strong>
                        <p>
                          Famixa không khóa máy. Xin thêm phút → {parentRole} duyệt trong hộp thư.
                        </p>
                      </>
                    )}
                  </div>
                </div>

                <div className="kv2-ask-row">
                  <button
                    type="button"
                    className="kv2-screen-ask"
                    onClick={() => setMissionRequestOpen(true)}
                  >
                    Đề xuất việc hôm nay
                  </button>
                  <button
                    type="button"
                    className="kv2-screen-ask is-soft"
                    onClick={() => setScreenRequestOpen(true)}
                  >
                    Xin thêm phút màn hình
                  </button>
                </div>

                {recentAsks.length > 0 ? (
                  <ul className="kv2-ask-status" aria-label="Trạng thái đề xuất">
                    {recentAsks.map((req) => (
                      <li
                        key={req.id}
                        className={`kv2-ask-status-item is-${req.status || 'pending'}`}
                      >
                        <div>
                          <strong>{requestKindLabel(req)}</strong>
                          <em>
                            {req.status === 'approved' && req.grantedMinutes != null
                              ? `Đã cộng +${req.grantedMinutes} phút`
                              : req.status === 'partial' && req.grantedMinutes != null
                                ? `Đồng ý +${req.grantedMinutes} phút`
                                : requestStatusLabel(req.status)}
                          </em>
                        </div>
                        <span>{requestStatusLabel(req.status)}</span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="kv2-ask-empty">Chưa có đề xuất nào — gửi thử một cái nhé.</p>
                )}

                {screenRequestToast ? (
                  <p className="kv2-ask-toast" role="status">
                    {screenRequestToast}
                  </p>
                ) : null}
              </article>
            </div>
          ) : null}
        </div>
      ) : null}

            {/* Plan tab — mock Fami kế hoạch */}
      {tab === 'tasks' ? (
        <KidPlanHub
          shortName={short}
          remaining={remaining}
          doneCount={doneCount}
          total={total}
          nextMission={nextMission}
          todayItems={[...trulyDone, ...pendingItems].sort((a, b) => {
            const aw = a.windowStart || a.windowEnd || '99:99';
            const bw = b.windowStart || b.windowEnd || '99:99';
            return aw.localeCompare(bw);
          })}
          busyId={busyId}
          uploading={uploading}
          missionDoneError={missionDoneError}
          famiLine={tasksFoxyBannerVi(short, careBand, doNowItems.length, remaining)}
          challengeHave={Math.min(10, Math.max(0, streak + doneCount))}
          challengeNeed={10}
          challengeSlot={
            familyId && childMemberId ? (
              <FamilyChallengeCard
                familyId={familyId}
                memberId={childMemberId}
                isParent={false}
                compact
              />
            ) : null
          }
          dayPartOf={dayPartOf}
          taskIcon={taskIcon}
          taskIconTone={taskIconTone}
          clockLabel={(item) =>
            item.windowStart
              ? item.windowStart.slice(0, 5)
              : formatWindow(item.windowStart, item.windowEnd) ?? 'Trong ngày'
          }
          durationLabel={(item) => {
            if (!item.windowStart || !item.windowEnd) return null;
            const [sh, sm] = item.windowStart.slice(0, 5).split(':').map(Number);
            const [eh, em] = item.windowEnd.slice(0, 5).split(':').map(Number);
            if (![sh, sm, eh, em].every(Number.isFinite)) return null;
            const mins = eh * 60 + em - (sh * 60 + sm);
            if (mins > 0 && mins <= 180) return `${mins} phút`;
            return null;
          }}
          starHint={(item) => {
            const n = item.projectedStarDelta ?? item.starReward ?? 10;
            return n > 0 ? `+${n} sao` : '+sao';
          }}
          studyNeedsEvidence={studyNeedsEvidence}
          onStartNext={() => {
            if (!nextMission) return;
            if (studyNeedsEvidence(nextMission)) {
              beginEvidencePick(nextMission);
              return;
            }
            if (canCompleteNow(nextMission, localTime)) {
              void quickDoneMission(nextMission);
              return;
            }
            openAction(nextMission);
          }}
          onOpenRewards={() => setTab('rewards')}
          onTalkFami={() => setTab('home')}
          onStartItem={(item) => {
            if (studyNeedsEvidence(item)) {
              beginEvidencePick(item);
              return;
            }
            if (canCompleteNow(item, localTime)) {
              void quickDoneMission(item);
              return;
            }
            openAction(item);
          }}
        />
      ) : null}

      {tab === 'rewards' ? (
        <KidTreasureHub
          shortName={short}
          stars={stars}
          remaining={remaining}
          streak={streak}
          weekDays={weekDays}
          toast={treasureToast}
          loading={treasureLoading}
          catalog={redeemCatalog}
          redeemBusyId={redeemBusyId}
          badges={kidBadges}
          achievements={bigAchievements}
          memories={treasureMemoriesVisible.map((m) => ({
            id: m.id,
            icon: m.icon,
            title: m.title,
            date: m.date,
            imageUrl: m.photoUrl ? withEvidenceAuth(m.photoUrl) : null,
          }))}
          memoriesEmpty={FAMILY_MEMORY_EMPTY}
          hasMoreMemories={familyMemories.length > FAMILY_MEMORY_VISIBLE}
          mysteryHave={mysteryHave}
          mysteryTarget={mysteryTarget}
          mysteryPct={mysteryPct}
          surpriseReady={remaining === 0 || streak >= 2}
          formatStars={formatStars}
          wishText={kidVoiceWish}
          wishStep={kidVoiceDone ? 4 : kidVoiceWish.trim() ? 1 : 0}
          wishStepTotal={4}
          onContinue={() => setTab('tasks')}
          onRedeem={(item) =>
            void handleRedeem(item as (typeof redeemCatalog)[number])
          }
          onOpenAllRewards={() => openTreasureSheet('rewards')}
          onOpenAllBadges={() => openTreasureSheet('badges')}
          onOpenAchievement={(id) => {
            if (id === 'mn') openAchievementSheet('movie');
            else if (id === 'garden') openAchievementSheet('garden');
            else if (id === 'read') openAchievementSheet('read');
            else openTreasureSheet('rewards');
          }}
          onOpenMemories={() => openJournalSheet('memories')}
          onOpenMystery={() => {
            if (mysteryPct >= 100) {
              showTreasureToast('Fami mở hộp bí mật… có thể là sticker, thư mẹ hoặc video! 🎁');
              return;
            }
            showTreasureToast(
              `Còn ${Math.max(0, mysteryTarget - mysteryHave)} điểm khám phá nữa — không phải mua, là mở dần! ✨`,
            );
          }}
          onOpenSurprise={() => {
            if (remaining === 0 || streak >= 2) {
              showTreasureToast('Fami gửi lời chúc bất ngờ — con làm tốt lắm! 💜');
              return;
            }
            showTreasureToast('Làm nốt việc hôm nay để Fami mở quà bất ngờ nhé!');
          }}
          onWishQuick={(text) => setKidVoiceWish(text)}
          wishSlot={
            childMemberId ? (
              kidVoiceDone ? (
                <div className="kv2-t-voice is-done">
                  <p>{CHILD_VOICE_WEEK_COPY.kidDone}</p>
                </div>
              ) : (
                <div className="kv2-t-voice">
                  <label className="kv2-t-voice-field">
                    <span>{CHILD_VOICE_WEEK_COPY.hardestLabel}</span>
                    <span className="kv2-t-voice-select">
                      <select
                        value={kidVoiceHardest}
                        onChange={(e) => setKidVoiceHardest(e.target.value)}
                      >
                        {CHILD_VOICE_HARDEST_OPTIONS.map((opt) => (
                          <option key={opt.value} value={opt.value}>
                            {opt.label}
                          </option>
                        ))}
                      </select>
                      <em className="kv2-t-voice-chevron" aria-hidden>
                        ⌄
                      </em>
                    </span>
                  </label>
                  <label className="kv2-t-voice-field">
                    <span>{CHILD_VOICE_WEEK_COPY.wantLabel}</span>
                    <span className="kv2-t-voice-select">
                      <select
                        value={kidVoiceWant}
                        onChange={(e) => setKidVoiceWant(e.target.value)}
                      >
                        {CHILD_VOICE_WANT_OPTIONS.map((opt) => (
                          <option key={opt.value} value={opt.value}>
                            {opt.label}
                          </option>
                        ))}
                      </select>
                      <em className="kv2-t-voice-chevron" aria-hidden>
                        ⌄
                      </em>
                    </span>
                  </label>
                  <label className="kv2-t-voice-field">
                    <span>{CHILD_VOICE_WEEK_COPY.wishLabel}</span>
                    <input
                      value={kidVoiceWish}
                      onChange={(e) => setKidVoiceWish(e.target.value)}
                      maxLength={200}
                      placeholder={CHILD_VOICE_WEEK_COPY.wishPlaceholder}
                    />
                  </label>
                  <button
                    type="button"
                    className="kv2-t-voice-btn"
                    disabled={kidVoiceBusy}
                    onClick={() => {
                      if (!childMemberId || kidVoiceBusy) return;
                      setKidVoiceBusy(true);
                      void submitChildVoiceWeek(familyId, {
                        memberId: childMemberId,
                        hardestCode: kidVoiceHardest,
                        wantParentCode: kidVoiceWant,
                        wishVi: kidVoiceWish.trim() || undefined,
                      })
                        .then(() => setKidVoiceDone(true))
                        .catch(() =>
                          setTreasureToast(CHILD_VOICE_WEEK_COPY.sendFailed),
                        )
                        .finally(() => setKidVoiceBusy(false));
                    }}
                  >
                    {kidVoiceBusy
                      ? CHILD_VOICE_WEEK_COPY.submitting
                      : CHILD_VOICE_WEEK_COPY.submit}
                  </button>
                </div>
              )
            ) : null
          }
        />
      ) : null}

      {tab === 'log' ? (
        <KidDiaryHub
          shortName={short}
          parentRole={parentRole}
          dayTitle={selectedJournalDay.fullLabel}
          dayLabelShort={selectedJournalDay.shortLabel}
          isToday={journalIsToday}
          loading={journalHistoryLoading && !journalIsToday}
          error={journalHistoryError && !journalIsToday ? journalHistoryError : null}
          toast={journalToast}
          storyParagraphs={journalStoryParagraphs}
          storyPhotoUrl={(() => {
            const hit =
              kidMomentsToday.find((m) => m.photoUrl)?.photoUrl ??
              journalEntries.find((e) => e.item.evidenceUrl)?.item.evidenceUrl;
            return hit ? withEvidenceAuth(hit) : null;
          })()}
          days={journalDays}
          dayIdx={Math.min(journalDayIdx, journalDays.length - 1)}
          moments={[
            ...kidMomentsToday.map((m, i) => ({
              id: m.id,
              kind: 'photo' as const,
              icon: '📷',
              title: m.titleVi || 'Khoảnh khắc của con',
              time: (m.happenedAt || m.flowDate || '').slice(11, 16) || 'Hôm nay',
              imageUrl: m.photoUrl ? withEvidenceAuth(m.photoUrl) : null,
              starred: i === 0,
            })),
            ...journalEntries
              .filter((e) => e.item.evidenceUrl)
              .slice(0, 4)
              .map((e) => {
                const title = e.item.title;
                const kind = /vẽ|tranh/i.test(title)
                  ? ('draw' as const)
                  : /video|quay/i.test(title)
                    ? ('video' as const)
                    : ('photo' as const);
                return {
                  id: `ev-${e.item.id}`,
                  kind,
                  icon: taskIcon(title),
                  title,
                  time: e.time,
                  imageUrl: withEvidenceAuth(e.item.evidenceUrl!),
                  starred: /đánh răng/i.test(title),
                };
              }),
          ]}
          prides={[
            ...earnedBadges.slice(0, 2).map((b) => ({
              id: b.id,
              icon: b.icon || '🛡️',
              title: 'Huy hiệu mới',
              subtitle: b.label,
              when: journalIsToday ? 'Hôm nay' : selectedJournalDay.shortLabel,
            })),
            ...(trulyDone.some((c) => /đánh răng|tự giác/i.test(c.title))
              ? [
                  {
                    id: 'first-brush',
                    icon: '🌱',
                    title: 'Lần đầu tiên',
                    subtitle: 'Tự giác',
                    when: journalIsToday ? 'Hôm nay' : selectedJournalDay.shortLabel,
                  },
                ]
              : []),
            ...(doneCount > 0
              ? [
                  {
                    id: 'done-day',
                    icon: '🚀',
                    title: 'Hoàn thành',
                    subtitle:
                      doneCount >= 10
                        ? '10 việc trong ngày'
                        : `${doneCount} việc trong ngày`,
                    when: journalIsToday ? 'Hôm nay' : selectedJournalDay.shortLabel,
                  },
                ]
              : []),
          ].slice(0, 3)}
          loves={parentVoiceInbox.slice(0, 2).map((v) => {
            const name = (v.fromMemberName || parentRole).trim();
            const lower = name.toLowerCase();
            const tone =
              lower.includes('mẹ') || lower.includes('me')
                ? ('mom' as const)
                : lower.includes('bố') || lower.includes('bo')
                  ? ('dad' as const)
                  : ('other' as const);
            const fromLabel =
              tone === 'mom'
                ? 'Mẹ vừa khen'
                : tone === 'dad'
                  ? 'Bố vừa gửi'
                  : `${name} vừa gửi`;
            return {
              id: v.id,
              fromLabel,
              body: v.bodyVi,
              when: v.sentAt
                ? `Hôm nay ${v.sentAt.slice(11, 16)}`
                : v.flowDate || 'Gần đây',
              tone,
            };
          })}
          timeline={journalMilestones}
          memories={journalMemoriesVisible.map((m) => ({
            id: m.id,
            icon: m.icon,
            title: m.title,
            date: m.date,
            imageUrl: m.photoUrl ? withEvidenceAuth(m.photoUrl) : null,
          }))}
          memoriesEmpty={FAMILY_MEMORY_EMPTY}
          hasMoreMemories={familyMemories.length > FAMILY_MEMORY_VISIBLE}
          moods={MOODS}
          moodIdx={moodIdx}
          moodLoaded={moodLoaded}
          moodSaving={moodSaving}
          canSaveMood={Boolean(childMemberId)}
          onPickDay={setJournalDayIdx}
          onPrevDay={() => setJournalDayIdx((i) => Math.max(0, i - 1))}
          onNextDay={() =>
            setJournalDayIdx((i) => Math.min(journalDays.length - 1, i + 1))
          }
          onListenStory={() =>
            setJournalToast(
              journalIsToday
                ? 'Fami đang kể lại câu chuyện hôm nay 💚'
                : 'Fami đang kể lại ngày này 💚',
            )
          }
          onOpenMoments={() => openJournalSheet('moments')}
          onAddMoment={() => setKidMomentSheetOpen(true)}
          onOpenPrides={() => setTab('rewards')}
          onOpenLoves={() => openJournalSheet('memories', 'parent_voice')}
          onOpenTimeline={() => {
            document
              .querySelector('.kdiary-dayline')
              ?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
          }}
          onOpenMemories={() => openJournalSheet('memories')}
          onMoodPick={setMoodIdx}
          onSaveMood={() => void saveMoodEntry()}
          onPlayLove={(id) => void ackVoiceMessage(id, 'thanks')}
          footerSlot={
            softLockActive ? (
              <button type="button" className="pill" onClick={onOpenParentPin}>
                Nhập mã bố mẹ để đổi người
              </button>
            ) : (
              <button
                type="button"
                className={`pill hold-pill kv2-j-hold${holdHolding ? ' is-holding' : ''}`}
                style={{ ['--hold' as string]: holdProgress } as CSSProperties}
                onPointerDown={(e) => {
                  e.currentTarget.setPointerCapture(e.pointerId);
                  onHoldSwitchStart();
                }}
                onPointerUp={onHoldSwitchCancel}
                onPointerCancel={onHoldSwitchCancel}
                onPointerLeave={onHoldSwitchCancel}
                onContextMenu={(e) => e.preventDefault()}
              >
                <span className="hold-fill" aria-hidden />
                <span>{holdHolding ? 'Giữ tiếp…' : 'Giữ để đổi người (bố mẹ)'}</span>
              </button>
            )
          }
        />
      ) : null}

      {tab === 'family' ? (
        <KidFamilyHub
          shortName={short}
          parentRole={parentRole}
          unlockPct={unlockPct}
          unlockLeft={unlockLeft}
          doneCount={doneCount}
          total={total}
          streak={streak}
          teamComplete={teamComplete}
          primaryParentVoice={primaryParentVoice}
          weeklyStory={weeklyStory}
          doneTitles={trulyDone.map((c) => c.title)}
          prides={[
            ...earnedBadges.slice(0, 2).map((b) => ({
              id: b.id,
              icon: b.icon || '🏅',
              title: 'Huy hiệu',
              subtitle: b.label,
            })),
            ...(trulyDone.some((c) => /đánh răng|tự giác/i.test(c.title))
              ? [
                  {
                    id: 'pride-brush',
                    icon: '🌱',
                    title: 'Tự giác',
                    subtitle: 'Đánh răng / giữ nhịp',
                  },
                ]
              : []),
            ...(doneCount > 0
              ? [
                  {
                    id: 'pride-done',
                    icon: '⭐',
                    title: 'Việc tốt',
                    subtitle: `${doneCount} việc hôm nay`,
                  },
                ]
              : []),
            ...(primaryParentVoice
              ? [
                  {
                    id: 'pride-love',
                    icon: '💌',
                    title: 'Lời yêu thương',
                    subtitle: (primaryParentVoice.fromMemberName || parentRole).trim(),
                  },
                ]
              : []),
          ].slice(0, 4)}
          memories={journalMemoriesVisible.slice(0, 4).map((m) => ({
            id: m.id,
            icon: m.icon,
            title: m.title,
            date: m.date,
            imageUrl: m.photoUrl ? withEvidenceAuth(m.photoUrl) : null,
          }))}
          onDoSuggestion={() => {
            if (primaryParentVoice) {
              void ackVoiceMessage(primaryParentVoice.id, 'thanks');
              return;
            }
            setTab('tasks');
          }}
          onOpenDiary={() => setTab('log')}
          onOpenMemories={() => {
            setTab('log');
            openJournalSheet('memories');
          }}
          onOpenRewards={() => setTab('rewards')}
          onOpenPlan={() => setTab('tasks')}
          onPlayLove={() => {
            if (primaryParentVoice) void ackVoiceMessage(primaryParentVoice.id, 'read');
            setTab('log');
          }}
          footerSlot={
            softLockActive ? (
              <p className="kfam-note">
                Đang khóa theo thỏa thuận nhà
                {softLockLabel ? ` · ${softLockLabel}` : ''}.
              </p>
            ) : (
              <button
                type="button"
                className={`pill hold-pill kv2-j-hold${holdHolding ? ' is-holding' : ''}`}
                style={{ ['--hold' as string]: holdProgress } as CSSProperties}
                onPointerDown={(e) => {
                  e.currentTarget.setPointerCapture(e.pointerId);
                  onHoldSwitchStart();
                }}
                onPointerUp={onHoldSwitchCancel}
                onPointerCancel={onHoldSwitchCancel}
                onPointerLeave={onHoldSwitchCancel}
                onContextMenu={(e) => e.preventDefault()}
              >
                <span className="hold-fill" aria-hidden />
                <span>{holdHolding ? 'Giữ tiếp…' : 'Giữ để đổi người (bố mẹ)'}</span>
              </button>
            )
          }
        />
      ) : null}

      <nav className="kv2-tabbar" aria-label="Điều hướng">
        <button
          type="button"
          className={`kv2-tab${tab === 'home' ? ' is-on' : ''}`}
          onClick={() => setTab('home')}
        >
          <span aria-hidden>🏠</span>
          <em>Trang chủ</em>
        </button>
        <button
          type="button"
          className={`kv2-tab${tab === 'tasks' ? ' is-on' : ''}`}
          onClick={() => setTab('tasks')}
        >
          <span aria-hidden>📅</span>
          <em>Kế hoạch</em>
        </button>
        <button
          type="button"
          className={`kv2-fab is-fami${famiSheetOpen ? ' is-on' : ''}`}
          aria-label="Fami — trợ lý nhanh"
          title="Fami giúp con làm tiếp"
          onClick={() => setFamiSheetOpen(true)}
        >
          <span aria-hidden>🌱</span>
          <em className="kv2-fab-label">Fami</em>
        </button>
        <button
          type="button"
          className={`kv2-tab${tab === 'log' ? ' is-on' : ''}`}
          onClick={() => setTab('log')}
        >
          <span aria-hidden>📖</span>
          <em>Nhật ký</em>
        </button>
        <button
          type="button"
          className={`kv2-tab${tab === 'family' ? ' is-on' : ''}`}
          onClick={() => setTab('family')}
        >
          <span aria-hidden>👨‍👩‍👧</span>
          <em>Gia đình</em>
        </button>
      </nav>

      {famiSheetOpen ? (
        <div
          className="sheet-backdrop"
          role="presentation"
          onClick={() => setFamiSheetOpen(false)}
        >
          <div
            className="ph-sheet khub-fami-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Fami"
            onClick={(e) => e.stopPropagation()}
          >
            <header className="ph-sheet-head">
              <div>
                <h2>
                  <span aria-hidden>🌱</span> Fami giúp con
                </h2>
                <p className="khub-fami-sub">Chọn một việc nhanh — Fami đi cùng</p>
              </div>
              <button
                type="button"
                className="ph-sheet-close"
                onClick={() => setFamiSheetOpen(false)}
                aria-label="Đóng"
              >
                ×
              </button>
            </header>
            <div className="khub-fami-actions">
              <button
                type="button"
                onClick={() => {
                  setFamiSheetOpen(false);
                  setTab('home');
                  if (nextMission) openAction(nextMission);
                }}
              >
                <span aria-hidden>💬</span> Xin gợi ý việc tiếp theo
              </button>
              <button
                type="button"
                onClick={() => {
                  setFamiSheetOpen(false);
                  setTab('log');
                }}
              >
                <span aria-hidden>📖</span> Nghe Fami kể chuyện
              </button>
              <button
                type="button"
                onClick={() => {
                  setFamiSheetOpen(false);
                  setTab('rewards');
                }}
              >
                <span aria-hidden>🎁</span> Kho báu / Quà bí mật
              </button>
              <button
                type="button"
                onClick={() => {
                  setFamiSheetOpen(false);
                  setTab('family');
                }}
              >
                <span aria-hidden>👨‍👩‍👧</span> Xem Gia đình
              </button>
              <button
                type="button"
                onClick={() => {
                  setFamiSheetOpen(false);
                  if (primaryParentVoice) {
                    void ackVoiceMessage(primaryParentVoice.id, 'read');
                  } else {
                    setTab('log');
                  }
                }}
              >
                <span aria-hidden>💌</span> Đọc thư của {parentRole}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {celebrate ? (
        <div className="ka-celebrate" role="status" onClick={dismissCelebrate}>
          <div className="ka-celebrate-card" onClick={(e) => e.stopPropagation()}>
            <p className="ka-celebrate-emoji" aria-hidden>
              🎉
            </p>
            <h2>{celebrateHeadline(celebrate.title, celebrate.stars, parentRole)}</h2>
            <p className="ka-celebrate-stars">
              {celebrate.labelVi
                ? kidFriendlyStarLabel(celebrate.labelVi)
                : `${formatStarDelta(celebrate.stars)} ⭐`}
            </p>
            <p className="muted">{celebrateSubline(celebrate.title, celebrate.stars)}</p>
            <button type="button" className="btn btn-primary" onClick={dismissCelebrate}>
              {remaining > 0 ? 'Việc tiếp theo!' : 'Tuyệt quá!'}
            </button>
          </div>
        </div>
      ) : null}

      {weekReviewOpen ? (
        <div
          className="sheet-backdrop kv2-action-sheet kv2-t-sheet-backdrop"
          role="presentation"
          onClick={() => setWeekReviewOpen(false)}
        >
          <div
            className="sheet kv2-t-sheet kv2-week-review-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={`Xem lại tuần của ${short}`}
            onClick={(e) => e.stopPropagation()}
          >
            <header className="kv2-week-review-head">
              <div>
                <p className="kv2-week-review-eyebrow">💛 Gắn kết</p>
                <h2>Tuần này của {short}</h2>
                {weeklyStory ? (
                  <p className="muted">
                    {weeklyStory.from.slice(5)} → {weeklyStory.to.slice(5)} · chạm để đọc lại lời thật
                  </p>
                ) : null}
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
                Tuần này chưa ghi lời riêng cho {short} — khi bố/mẹ hoặc anh chị gửi, sẽ hiện ở đây.
              </p>
            ) : (
              <div className="kv2-week-review-list">
                {weekReviewMoments.map((m) => (
                  <article key={m.id} className="kv2-week-review-card">
                    <p className="kv2-week-review-kind">
                      <span aria-hidden>{m.icon}</span> {m.kindLabel}
                    </p>
                    <p className="kv2-week-review-title">{m.titleVi}</p>
                    {m.bodyVi ? <p className="kv2-week-review-body">{m.bodyVi}</p> : null}
                    <p className="kv2-week-review-at">
                      {m.at.slice(0, 10)}
                      {m.at.length > 10 ? ` · ${m.at.slice(11, 16)}` : ''}
                    </p>
                  </article>
                ))}
              </div>
            )}
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => setWeekReviewOpen(false)}
            >
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      {achievementSheet ? (
        <div
          className="sheet-backdrop kv2-action-sheet kv2-t-sheet-backdrop"
          role="presentation"
          onPointerDown={handleBackdropPointerDown}
          onClick={handleAchievementBackdropClose}
        >
          <div
            className="sheet kv2-t-sheet kv2-achieve-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={
              achievementSheet === 'movie'
                ? 'Movie Night'
                : achievementSheet === 'garden'
                  ? 'Khu vườn'
                  : 'Đọc sách'
            }
            onClick={(e) => e.stopPropagation()}
          >
            {achievementSheet === 'movie' ? (
              <>
                <h2>
                  <span aria-hidden>🎬</span> Movie Night
                </h2>
                <p className="muted">
                  {confirmedMovieUnlocks.length > 0
                    ? `Cả nhà đã mở ${confirmedMovieUnlocks.length} lần — xem lại bên dưới.`
                    : 'Chưa mở Movie Night — giữ kế hoạch nhóm để mở lần đầu.'}
                </p>
                <div className="kv2-j-sheet-list">
                  {confirmedMovieUnlocks.length === 0 ? (
                    <p className="muted">Chưa có lần mở nào để xem lại.</p>
                  ) : (
                    confirmedMovieUnlocks.map((u) => (
                      <article key={u.id} className="kv2-j-sheet-card">
                        <span aria-hidden>🍿</span>
                        <div>
                          <strong>{u.labelVi || 'Movie Night'}</strong>
                          <em>{formatMemoryDate(u.confirmedAt || u.flowDate)}</em>
                        </div>
                      </article>
                    ))
                  )}
                </div>
                <div className="kv2-achieve-actions">
                  {confirmedMovieUnlocks.length > 0 ? (
                    <button
                      type="button"
                      className="btn btn-primary"
                      disabled={movieRemindBusy || !childMemberId}
                      onClick={() => void remindMovieNight()}
                    >
                      {movieRemindBusy ? 'Đang gửi…' : 'Nhắc bố/mẹ tổ chức lại'}
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="pill is-soft"
                    onClick={() => {
                      closeAchievementSheet();
                      openJournalSheet('memories', 'team_unlock');
                    }}
                  >
                    Xem kỷ niệm Movie Night
                  </button>
                  <button type="button" className="pill is-soft" onClick={closeAchievementSheet}>
                    Đóng
                  </button>
                </div>
              </>
            ) : null}
            {achievementSheet === 'garden' ? (
              <>
                <h2>
                  <span aria-hidden>🌱</span> Khu vườn của {short}
                </h2>
                <p className="muted">
                  Plot hôm nay chơi theo ngày. Các lần vườn nở lưu trong kỷ niệm.
                </p>
                <div className="kv2-j-sheet-list">
                  {gardenBloomHistory.length === 0 ? (
                    <p className="muted">Chưa có lần vườn nở lưu lại — đủ 3 cây khỏe hôm nay nhé.</p>
                  ) : (
                    gardenBloomHistory.map((m) => (
                      <article key={m.id} className="kv2-j-sheet-card">
                        <span aria-hidden>{m.icon || '🌸'}</span>
                        <div>
                          <strong>{m.titleVi}</strong>
                          <em>{formatMemoryDate(m.flowDate || m.happenedAt)}</em>
                        </div>
                      </article>
                    ))
                  )}
                </div>
                <div className="kv2-achieve-actions">
                  <button type="button" className="btn btn-primary" onClick={openGardenPane}>
                    Vào khu vườn hôm nay
                  </button>
                  <button
                    type="button"
                    className="pill is-soft"
                    onClick={() => {
                      closeAchievementSheet();
                      openJournalSheet('memories', 'beautiful_day');
                    }}
                  >
                    Xem kỷ niệm vườn
                  </button>
                  <button type="button" className="pill is-soft" onClick={closeAchievementSheet}>
                    Đóng
                  </button>
                </div>
              </>
            ) : null}
            {achievementSheet === 'read' ? (
              <>
                <h2>
                  <span aria-hidden>📘</span> Đọc sách
                </h2>
                <p className="muted">
                  Xem lại các buổi đọc trong nhật ký và kỷ niệm — không tạo lịch sử giả.
                </p>
                <div className="kv2-achieve-actions">
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => {
                      closeAchievementSheet();
                      setTab('log');
                    }}
                  >
                    Mở nhật ký
                  </button>
                  <button
                    type="button"
                    className="pill is-soft"
                    onClick={() => {
                      closeAchievementSheet();
                      openJournalSheet('memories', 'all');
                    }}
                  >
                    Xem kỷ niệm
                  </button>
                  <button type="button" className="pill is-soft" onClick={closeAchievementSheet}>
                    Đóng
                  </button>
                </div>
              </>
            ) : null}
          </div>
        </div>
      ) : null}

      {diaryDayPickerOpen ? (
        <div
          className="sheet-backdrop kv2-action-sheet kv2-t-sheet-backdrop"
          role="presentation"
          onPointerDown={handleBackdropPointerDown}
          onClick={(e) => {
            if (e.target !== e.currentTarget || !backdropPointerDown.current) return;
            setDiaryDayPickerOpen(false);
          }}
        >
          <div
            className="sheet kv2-t-sheet kv2-j-sheet kv2-diary-day-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Chọn ngày nhật ký"
            onClick={(e) => e.stopPropagation()}
          >
            <h2>📅 Chọn ngày nhật ký</h2>
            <p className="kv2-diary-day-hint">Xem lại 6 ngày gần nhất của {short}</p>
            <div className="kv2-diary-day-list" role="listbox" aria-label="Ngày nhật ký">
              {[...journalDays].reverse().map((d) => {
                const idx = journalDays.findIndex((x) => x.key === d.key);
                const on = idx === Math.min(journalDayIdx, journalDays.length - 1);
                return (
                  <button
                    key={d.key}
                    type="button"
                    role="option"
                    aria-selected={on}
                    className={`kv2-diary-day-row${on ? ' is-on' : ''}${
                      d.isToday ? ' is-today' : ''
                    }`}
                    onClick={() => {
                      setJournalDayIdx(idx);
                      setDiaryDayPickerOpen(false);
                      window.requestAnimationFrame(() => {
                        document
                          .querySelector('.kdiary-dates')
                          ?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                      });
                    }}
                  >
                    <span aria-hidden>{d.isToday ? '🌟' : '📖'}</span>
                    <div>
                      <strong>{d.isToday ? 'Hôm nay' : d.shortLabel}</strong>
                      <em>{d.fullLabel}</em>
                    </div>
                    {on ? <b>Đang xem</b> : null}
                  </button>
                );
              })}
            </div>
            <button
              type="button"
              className="pill is-soft"
              onClick={() => setDiaryDayPickerOpen(false)}
            >
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      {journalSheet ? (
        <div
          className="sheet-backdrop kv2-action-sheet kv2-t-sheet-backdrop"
          role="presentation"
          onPointerDown={handleBackdropPointerDown}
          onClick={handleJournalBackdropClose}
        >
          <div
            className="sheet kv2-t-sheet kv2-j-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={
              journalSheet === 'memories' ? 'Kỷ niệm gia đình' : 'Khoảnh khắc đáng nhớ'
            }
            onClick={(e) => e.stopPropagation()}
          >
            <h2>
              {journalSheet === 'memories' ? 'Kỷ niệm đẹp của gia đình' : 'Khoảnh khắc đáng nhớ'}
            </h2>
            {journalSheet === 'memories' ? (
              <>
                <div className="kv2-mem-filters" role="tablist" aria-label="Lọc kỷ niệm">
                  {memoryFilterChips.map(([key, label]) => (
                    <button
                      key={key}
                      type="button"
                      role="tab"
                      aria-selected={memoryFilter === key}
                      className={`kv2-mem-filter${memoryFilter === key ? ' is-on' : ''}`}
                      onClick={() => setMemoryFilter(key)}
                    >
                      {label}
                    </button>
                  ))}
                </div>
                <div className="kv2-j-sheet-list">
                  {filteredSheetMemories.length === 0 ? (
                    <p className="muted">
                      {albumMemories.length === 0
                        ? FAMILY_MEMORY_EMPTY
                        : 'Không có kỷ niệm trong mục này.'}
                    </p>
                  ) : (
                    filteredSheetMemories.map((m) => (
                      <article
                        key={m.id}
                        className={`kv2-j-sheet-card${m.locked ? ' is-locked' : ''}`}
                      >
                        <span aria-hidden>{m.icon}</span>
                        <div>
                          <strong>{m.title}</strong>
                          <em>{m.date}</em>
                        </div>
                        {m.isNew ? <span className="kv2-j-new">Mới</span> : null}
                      </article>
                    ))
                  )}
                </div>
              </>
            ) : (
              <div className="kv2-j-sheet-list">
                {journalFeatureMoments.length === 0 ? (
                  <p className="muted">{FAMILY_MEMORY_EMPTY}</p>
                ) : (
                  journalFeatureMoments.map((m) => (
                    <article key={m.id} className="kv2-j-sheet-card is-moment">
                      <span className="kv2-j-sheet-art" aria-hidden>
                        {m.icon}
                      </span>
                      <div>
                        <strong>{m.title}</strong>
                        <em>{m.date}</em>
                        <p>{m.caption}</p>
                      </div>
                    </article>
                  ))
                )}
              </div>
            )}
            <button type="button" className="pill is-soft" onClick={closeJournalSheet}>
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      {treasureSheet ? (
        <div
          className="sheet-backdrop kv2-action-sheet kv2-t-sheet-backdrop"
          role="presentation"
          onPointerDown={handleBackdropPointerDown}
          onClick={handleTreasureBackdropClose}
        >
          <div
            className="sheet kv2-t-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={treasureSheet === 'rewards' ? 'Tất cả phần thưởng' : 'Tất cả huy hiệu'}
            onClick={(e) => e.stopPropagation()}
          >
            <h2>{treasureSheet === 'rewards' ? 'Tất cả phần thưởng' : 'Tất cả huy hiệu'}</h2>
            {treasureSheet === 'rewards' ? (
              <div className="kv2-t-sheet-rewards">
                {treasureLoading && redeemCatalog.length === 0 ? (
                  <p className="muted">Đang tải quà…</p>
                ) : redeemCatalog.length === 0 ? (
                  <p className="muted">Bố mẹ chưa thiết lập phần thưởng.</p>
                ) : (
                  redeemCatalog.map((item) => {
                    const busy = redeemBusyId === item.id;
                    return (
                      <article key={item.id} className={`kv2-t-sheet-reward tone-${item.tone}`}>
                        <span className="kv2-t-sheet-reward-ico" aria-hidden>
                          {item.icon}
                        </span>
                        <div className="kv2-t-sheet-reward-copy">
                          <strong>{item.title}</strong>
                          <em>
                            {item.cost == null ? (
                              '??? ⭐'
                            ) : (
                              <>
                                <span aria-hidden>⭐</span> {item.cost}
                              </>
                            )}
                          </em>
                        </div>
                        <button
                          type="button"
                          className={`kv2-t-card-cta${item.canRedeem ? ' is-active' : ' is-save'}`}
                          disabled={busy || !item.canRedeem}
                          onClick={() => void handleRedeem(item)}
                          title={
                            item.isSpecial
                              ? 'Phần này bố mẹ giữ — con chưa đổi được'
                              : undefined
                          }
                        >
                          {busy ? 'Đang đổi…' : item.ctaLabel}
                        </button>
                      </article>
                    );
                  })
                )}
              </div>
            ) : (
              <ul className="kv2-t-sheet-badges">
                {earnedBadges.length === 0 ? (
                  <li className="muted" style={{ listStyle: 'none', padding: '8px 0' }}>
                    Chưa có huy hiệu đã mở.
                  </li>
                ) : (
                  earnedBadges.map((b) => (
                    <li key={b.id} className="is-on">
                      <span className="kv2-t-sheet-badge-ico" aria-hidden>
                        {b.icon}
                      </span>
                      <div className="kv2-t-sheet-badge-copy">
                        <strong>{b.label}</strong>
                        <p>{b.hint}</p>
                      </div>
                      <em>✓</em>
                    </li>
                  ))
                )}
              </ul>
            )}
            <button type="button" className="pill is-soft" onClick={closeTreasureSheet}>
              Đóng
            </button>
          </div>
        </div>
      ) : null}

      {active ? (
        <div
          className="sheet-backdrop kv2-action-sheet"
          role="presentation"
          onPointerDown={handleBackdropPointerDown}
          onClick={handleBackdropClose}
        >
          <div
            className="sheet kh-action-sheet"
            role="dialog"
            aria-modal="true"
            aria-label={active.title}
            onClick={(e) => e.stopPropagation()}
          >
            <p className="kh-cue-mini">Cùng giúp Foxy nào</p>
            <h2>
              <span aria-hidden>{taskIcon(active.title)}</span> {active.title}
            </h2>
            <p className="muted">
              {earlyCompleteBlockReason(active, localTime) ??
                minutesUntil(active) ??
                formatWindow(active.windowStart, active.windowEnd) ??
                'Trong ngày'}
            </p>
            {earlyCompleteBlockReason(active, localTime) ? (
              <p className="muted" style={{ marginTop: 0 }}>
                {countdownUntilWindow(active, localTime) ?? 'Chờ đến giờ nhé'}
              </p>
            ) : null}
            {studyNeedsEvidence(active) ? (
              <p className="kv2-study-hint">
                📚 Ảnh chỉ là nộp bài — bố mẹ xác nhận (đúng bài hôm nay) mới được sao
              </p>
            ) : null}
            {!askReason ? (
              <>
                <button
                  type="button"
                  className={
                    studyNeedsEvidence(active)
                      ? 'btn btn-primary kid-done'
                      : 'kv2-do-photo kv2-sheet-photo'
                  }
                  disabled={
                    busyId === active.id ||
                    uploading ||
                    !canCompleteNow(active, localTime)
                  }
                  onClick={() => beginEvidencePick(active)}
                >
                  {evidenceFile
                    ? 'Đổi ảnh đã học'
                    : studyNeedsEvidence(active)
                      ? '📷 Gửi ảnh đã học'
                      : '📷 Đính kèm ảnh đã làm'}
                </button>
                {evidencePreview ? (
                  <img src={evidencePreview} alt="Ảnh đã chọn" className="evidence-thumb" />
                ) : (
                  <p className="muted" style={{ margin: 0, fontSize: 13 }}>
                    {studyNeedsEvidence(active)
                      ? active.evidencePolicy === 'hard'
                        ? 'Chế độ cứng: bắt buộc ảnh + xác nhận bố mẹ.'
                        : 'Gửi ảnh để bố mẹ kiểm «bài hôm nay». Soft: tick được nhưng sao chờ xác nhận.'
                      : 'Tuỳ chọn — chụp hoặc chọn ảnh từ thư viện để bố mẹ xem.'}
                  </p>
                )}
                {evidenceError ? <div className="banner-error">{evidenceError}</div> : null}
                {evidenceSoftWarn ? (
                  <p className="kv2-evidence-soft-warn" role="status">
                    {evidenceSoftWarn}
                  </p>
                ) : null}
                {studyNeedsEvidence(active) && active.evidencePolicy === 'hard' ? (
                  <button
                    type="button"
                    className="btn btn-primary kid-done"
                    disabled={
                      busyId === active.id ||
                      uploading ||
                      !evidenceFile ||
                      !canCompleteNow(active, localTime)
                    }
                    onClick={() => void submitDone()}
                  >
                    {uploading || busyId === active.id ? 'Đang lưu…' : 'Gửi ảnh & xong!'}
                  </button>
                ) : (
                  <button
                    type="button"
                    className={
                      studyNeedsEvidence(active)
                        ? 'kv2-do-photo kv2-sheet-photo'
                        : 'btn btn-primary kid-done'
                    }
                    disabled={
                      busyId === active.id ||
                      uploading ||
                      !canCompleteNow(active, localTime)
                    }
                    onClick={() => void submitDone()}
                  >
                    {uploading || busyId === active.id
                      ? 'Đang lưu…'
                      : studyNeedsEvidence(active)
                        ? evidenceFile
                          ? 'Gửi ảnh & xong!'
                          : 'Đã học · chờ bằng chứng'
                        : 'Mình đã làm!'}
                  </button>
                )}
                <button
                  type="button"
                  className="pill is-soft"
                  disabled={busyId === active.id || uploading}
                  onClick={() => setAskReason(true)}
                >
                  Mình chưa làm được
                </button>
              </>
            ) : (
              <>
                <h3>Chuyện gì xảy ra vậy?</h3>
                <div className="reason-grid">
                  {SKIP_REASON_OPTIONS.map((opt) => (
                    <button
                      key={opt.value}
                      type="button"
                      className="pill reason-pill"
                      disabled={busyId === active.id}
                      onClick={() => {
                        onReflect(active, opt.value);
                        setAskReason(false);
                        setActive(null);
                      }}
                    >
                      {opt.label}
                    </button>
                  ))}
                </div>
              </>
            )}
            <button type="button" className="pill is-soft" onClick={closeAction}>
              Để sau
            </button>
          </div>
        </div>
      ) : null}

      <input
        ref={evidenceInputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp,image/*"
        hidden
        aria-hidden
        tabIndex={-1}
        onChange={(e) => {
          handleEvidenceInput(e.target.files?.[0]);
          e.target.value = '';
        }}
      />

      {cheerOpen ? (
        <div
          className="sheet-backdrop kv2-action-sheet"
          role="presentation"
          onClick={() => !cheerBusy && setCheerOpen(false)}
        >
          <div
            className="sheet kh-action-sheet"
            role="dialog"
            aria-modal="true"
            aria-label="Gửi lời cổ vũ"
            onClick={(e) => e.stopPropagation()}
          >
            <header className="ph-nudge-head">
              <span className="ph-nudge-head-ico" aria-hidden>
                🤝
              </span>
              <div>
                <h2>Gửi lời cổ vũ</h2>
                <p>Lời nhắc cố định — mình cổ vũ nhau, không nhắc thay bố mẹ.</p>
              </div>
              <button
                type="button"
                className="ph-nudge-close"
                aria-label="Đóng"
                disabled={cheerBusy}
                onClick={() => setCheerOpen(false)}
              >
                ✕
              </button>
            </header>

            <label className="ph-nudge-field">
              <span className="ph-nudge-label">Gửi tới</span>
              <span className="ph-nudge-select">
                <select
                  value={cheerToId}
                  onChange={(e) => setCheerToId(e.target.value)}
                  disabled={cheerBusy}
                >
                  {cheerTargets.map((c) => (
                    <option key={c.memberId} value={c.memberId}>
                      {c.displayName}
                    </option>
                  ))}
                </select>
                <i aria-hidden>▾</i>
              </span>
            </label>

            <fieldset className="ph-sibling-nudge-templates">
              <legend className="ph-nudge-label">Chọn lời cổ vũ</legend>
              {NUDGE_TEMPLATE_OPTIONS.map((opt) => (
                <label
                  key={opt.code}
                  className={`ph-nudge-option${cheerTemplate === opt.code ? ' is-on' : ''}`}
                >
                  <input
                    type="radio"
                    name="kid-cheer-template"
                    checked={cheerTemplate === opt.code}
                    onChange={() => setCheerTemplate(opt.code)}
                    disabled={cheerBusy}
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
              <span className="ph-nudge-preview-label">
                {cheerPreviewAudienceVi(
                  careBand,
                  shortChildName(
                    nudgeCandidates.find((c) => c.memberId === cheerToId)?.displayName ||
                      'anh/chị',
                  ) || 'anh/chị',
                )}
              </span>
              <p>“{cheerPreview}”</p>
            </div>

            {cheerError ? (
              <p className="banner-error" role="alert">
                {cheerError}
              </p>
            ) : null}

            <div className="ph-nudge-actions">
              <button
                type="button"
                className="ph-nudge-btn is-ghost"
                disabled={cheerBusy}
                onClick={() => setCheerOpen(false)}
              >
                Để sau
              </button>
              <button
                type="button"
                className="ph-nudge-btn is-primary"
                disabled={cheerBusy || !cheerToId}
                onClick={() => void submitCheer()}
              >
                {cheerBusy ? 'Đang gửi…' : 'Gửi lời cổ vũ'}
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {familyId && childMemberId ? (
        <>
          <ChildScreenRequestSheet
            familyId={familyId}
            memberId={childMemberId}
            open={screenRequestOpen}
            onClose={() => setScreenRequestOpen(false)}
            onSubmitted={(msg) => {
              setScreenRequestToast(msg);
              setAskReloadTick((n) => n + 1);
            }}
          />
          
          <KidMomentSheet
            familyId={familyId}
            memberId={childMemberId}
            memberName={short}
            flowDate={flowDate}
            open={kidMomentSheetOpen}
            remainingToday={kidMomentsRemainingToday}
            onClose={() => setKidMomentSheetOpen(false)}
            onCreated={(entry) => {
              setSavedMemories((prev) => [entry, ...prev]);
              setJournalToast('Đã gửi khoảnh khắc — bố mẹ sẽ nhìn thấy.');
              window.setTimeout(() => setJournalToast(null), 3200);
            }}
          />
<ChildMissionRequestSheet
            familyId={familyId}
            memberId={childMemberId}
            open={missionRequestOpen}
            onClose={() => setMissionRequestOpen(false)}
            onSubmitted={(msg) => {
              setScreenRequestToast(msg);
              setAskReloadTick((n) => n + 1);
            }}
          />
        </>
      ) : null}
    </section>
  );
}
