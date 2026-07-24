import { useEffect, useMemo, useRef, useState, type CSSProperties } from 'react';
import {
  SKIP_REASON_OPTIONS,
  skipReasonLabel,
  uploadCommitmentEvidence,
  fetchChildGratitude,
  sendChildGratitude,
  fetchRewardCatalog,
  fetchRewardRedemptions,
  fetchTeamUnlocks,
  fetchMemberMood,
  upsertMemberMood,
  redeemReward,
  type RewardCatalogItem,
  type RewardRedemption,
  type TeamUnlock,
  type AccountabilityDayGlance,
  type DayFlowCommitment,
  type SkipReasonCode,
} from '@/shared/api/family-os.api';
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
  FAMILY_MEMORY_EMPTY,
  FAMILY_MEMORY_VISIBLE,
} from '@/shared/flow/family-memories';
import { FAMILY_MOODS, moodIndexFromCode } from '@/shared/flow/family-moods';
import { isParentVerified } from '@/shared/nudge/nudge-stats';

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

function kidMissionUxState(
  item: DayFlowCommitment,
  flowDate: string,
): KidMissionUxState {
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

type DayPart = 'all' | 'morning' | 'afternoon' | 'evening' | 'done';
type KidTab = 'home' | 'tasks' | 'rewards' | 'log';

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
function gardenBadge(title: string): string {
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

function praisePrideLine(short: string, c: DayFlowCommitment, flowDate?: string): string {
  const follow = '';
  return stablePick(praiseSeed(c, flowDate), MOM_PRAISE_ON_TIME)(short, c.title, follow);
}

function praiseEncouragementLine(
  short: string,
  c: DayFlowCommitment,
  nextTitle?: string,
  flowDate?: string,
): string {
  const follow = lateEncourageFollow(c.title, nextTitle, 'praise');
  return stablePick(`${praiseSeed(c, flowDate)}:late`, MOM_ENCOURAGE_LATE)(short, c.title, follow);
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

function livingFoxy(
  childName: string,
  remaining: number,
  teamRemaining: number,
  teamComplete: boolean,
  justCelebrated: boolean,
  nextTitle?: string,
): string {
  const short = childName.split(/\s+/).pop() || childName;
  if (justCelebrated) {
    const cheers = [
      `Wowww!! ${short} giỏi quá! Foxy tự hào lắm!`,
      `Yeah!! ${short} làm tốt lắm — Foxy vui quá!`,
      `${short} giỏi quá! Foxy tự hào lắm! 🦊`,
    ];
    return stablePick(`${short}:celebrate`, cheers);
  }
  if (teamComplete) return `🎉 Cả nhà mở được Movie Night rồi! Cảm ơn ${short}!`;
  if (remaining === 0 && teamRemaining > 0) {
    return `${short} xong phần mình rồi! Cả đội còn ${teamRemaining} việc — Foxy chờ Movie Night!`;
  }
  if (remaining === 0) return `${short} ơi, hôm nay con đã giúp Foxy hết sức rồi!`;
  if (remaining === 1) return `${short} ơi. Con chỉ còn 1 việc nữa. Mình cùng cố nhé!`;
  if (remaining === 2) return `${short} ơi. Con chỉ còn 2 việc nữa. Mình cùng cố nhé!`;
  if (nextTitle) return `${short} ơi, tiếp theo mình làm «${nextTitle}» giúp cả nhà nhé!`;
  return `${short} ơi, hôm nay chúng ta giúp cả nhà mở Movie Night nào!`;
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

type MomLine = (short: string, title: string, follow: string) => string;

const MOM_PRAISE_ON_TIME: MomLine[] = [
  (short, title) => `Mẹ rất tự hào vì ${short} chủ động hoàn thành «${title}»! ❤️`,
  (short, title) => `Giỏi quá ${short} ơi — «${title}» đúng giờ luôn! Mẹ tự hào lắm! 💪`,
  (short, title) => `${short} làm «${title}» thật gọn gàng — mẹ vui lắm! ❤️`,
  (short, title) => `Hay quá! ${short} hoàn thành «${title}» đúng lúc rồi! 🌟`,
  (short, title) => `Mẹ tự hào lắm vì ${short} giữ đúng giờ với «${title}»! ❤️`,
];

const MOM_ENCOURAGE_LATE: MomLine[] = [
  (short, _title, follow) =>
    `Cố gắng con nhé ${short} — lần sau đúng giờ hơn nha!${follow}`,
  (short, _title, follow) => `${short} làm xong rồi đó — mai tranh thủ sớm hơn nhé!${follow}`,
  (short, _title, follow) =>
    `Mẹ thấy ${short} đã cố — giờ giấc lần sau sẽ mượt hơn!${follow}`,
  (short, title, follow) => `${short} hoàn thành «${title}» rồi — cố thêm chút nữa về giờ nhé!${follow}`,
  (short, _title, follow) => `Mẹ vẫn ủng hộ ${short}${follow} 💛`,
];

const JOURNAL_ON_TIME_GENERIC: MomLine[] = [
  (short, title) => `Mẹ rất vui vì ${short} đã cố gắng với «${title}»!`,
  (short, title) => `${short} làm «${title}» thật chăm chỉ — mẹ tự hào! 💪`,
  (short, title) => `Giỏi quá ${short}! «${title}» xong đúng lúc rồi! 🌟`,
  (short, title) => `Mẹ thấy ${short} rất ngoan với «${title}» hôm nay! ❤️`,
];

const BEAUTIFUL_DAY_ON_TIME: Array<(short: string) => string> = [
  (short) => `Hôm nay nhà mình thật tuyệt — mẹ ghi nhận ${short} đã cố gắng! ❤️`,
  (short) => `Ngày đẹp của ${short}! Mẹ vui lắm vì con giữ đúng giờ! 🌟`,
  (short) => `${short} làm hôm nay thật xuất sắc — cả nhà đều tự hào! ❤️`,
];

const BEAUTIFUL_DAY_LATE_ONLY: Array<(short: string) => string> = [
  (short) => `Mẹ thấy ${short} vẫn cố gắng hôm nay — mai mình làm đúng giờ hơn nhé! 💛`,
  (short) => `${short} vẫn hoàn thành việc hôm nay — mai mình bắt giờ sớm hơn nha! 💛`,
  (short) => `Mẹ biết ${short} đã cố — ngày mai giờ giấc sẽ mượt hơn! 💛`,
];

const PRAISE_FALLBACK: Array<(short: string) => string> = [
  (short) => `Mẹ rất tự hào vì ${short} đang cố gắng mỗi ngày! ❤️`,
  (short) => `Mẹ luôn tin ${short} sẽ làm tốt hơn mỗi ngày! 💛`,
  (short) => `${short} ơi, mẹ thấy con đang tiến bộ từng chút! 🌟`,
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
  'Bắt đầu từ hôm nay — Foxy luôn ủng hộ con! 💪',
  'Hôm nay là ngày mới — Foxy tin con làm được! 💪',
  'Mỗi ngày một chút — Foxy luôn bên con! 💪',
];

const STREAK_ACTIVE_NOTES: Array<(short: string) => string> = [
  (short) => `Giữ vững nha ${short}! Foxy luôn ủng hộ con! 💪`,
  (short) => `${short} đang làm rất tốt — tiếp tục nha! 🔥`,
  (short) => `Chuỗi đang đẹp lắm ${short} — Foxy tự hào! 🌟`,
];

const ALL_DONE_CHEER: Array<(short: string) => string> = [
  (short) => `Giỏi quá ${short} ơi — Foxy tự hào lắm! Nghỉ vui nhé 💪`,
  (short) => `${short} xuất sắc! Foxy ôm con cái — nghỉ ngơi nhé! 🦊`,
  (short) => `Tuyệt vời ${short}! Hôm nay con giúp Foxy hết sức rồi! 🌟`,
];

const ON_TIME_CELEBRATE_HEADLINES = [
  'Giỏi quá!',
  'Tuyệt vời!',
  'Hay lắm!',
  'Mẹ tự hào lắm!',
  'Xuất sắc!',
];

const LATE_CELEBRATE_HEADLINES = [
  'Xong rồi nhé!',
  'Cố gắng lắm!',
  'Mẹ thấy con đã cố!',
  'Làm xong rồi — giỏi!',
];

const ON_TIME_CELEBRATE_SUBLINES: Array<(title: string) => string> = [
  (title) => `«${title}» đã giúp Foxy thêm một bước!`,
  (title) => `«${title}» xong — Foxy vui lắm!`,
  (title) => `Foxy ghi nhận «${title}» của con!`,
];

const LATE_CELEBRATE_SUBLINES: Array<(title: string) => string> = [
  (title) => `«${title}» xong rồi — lần sau đúng giờ hơn nhé!`,
  (title) => `Foxy thấy con đã cố với «${title}»!`,
  (title) => `«${title}» hoàn thành — mai mình sớm hơn nha!`,
];

function celebrateHeadline(title: string, stars: number): string {
  const pool = stars <= 0 ? LATE_CELEBRATE_HEADLINES : ON_TIME_CELEBRATE_HEADLINES;
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

function clockOf(item: DayFlowCommitment): string {
  const raw = item.windowStart || item.windowEnd;
  return raw ? raw.slice(0, 5) : '--:--';
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
  if (balance <= 0) return 'Hoàn thành nhiệm vụ để kiếm sao nhé!';
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
  nextTitle?: string,
  seed?: string,
): string {
  const t = title.toLowerCase();
  const follow = journalLateFollow(title, nextTitle);
  const pick = seed ?? title;
  if (t.includes('đánh răng')) {
    const pool = [
      `${short} vẫn đánh răng xong rồi${follow} 💛`,
      `${short} đánh răng xong rồi — cố thêm chút về giờ nhé!${follow} 💛`,
    ];
    return stablePick(`${pick}:late-rang`, pool);
  }
  if (t.includes('đọc') || t.includes('sách')) {
    const pool = [
      `${short} vẫn đọc được hôm nay${follow} 📖`,
      `${short} đọc xong rồi — mai đúng giờ hơn nha!${follow} 📖`,
    ];
    return stablePick(`${pick}:late-doc`, pool);
  }
  if (t.includes('cặp') || t.includes('balo') || t.includes('chuẩn bị')) {
    const pool = [
      `${short} vẫn chuẩn bị xong${follow} 💛`,
      `${short} chuẩn bị xong rồi — lần sau sớm hơn nhé!${follow} 💛`,
    ];
    return stablePick(`${pick}:late-cap`, pool);
  }
  if (t.includes('tưới') || t.includes('cây')) {
    const pool = [
      `${short} vẫn chăm cây được${follow} 🌱`,
      `${short} tưới cây xong — mai đúng giờ hơn nha!${follow} 🌱`,
    ];
    return stablePick(`${pick}:late-cay`, pool);
  }
  if (t.includes('ngủ')) {
    const pool = [
      `${short} vẫn đi ngủ rồi${follow} 😴`,
      `${short} ngủ rồi — lần sau thử sớm hơn nhé!${follow} 😴`,
    ];
    return stablePick(`${pick}:late-ngu`, pool);
  }
  if (t.includes('bài') || t.includes('học')) {
    const pool = [
      `${short} vẫn cố gắng với bài học${follow} 💛`,
      `${short} học xong rồi — mai tranh thủ sớm hơn nhé!${follow} 💛`,
    ];
    return stablePick(`${pick}:late-hoc`, pool);
  }
  return stablePick(`${pick}:late`, MOM_ENCOURAGE_LATE)(short, title, follow);
}

function journalNote(item: DayFlowCommitment, short: string, nextTitle?: string): string {
  const title = item.title;
  const t = title.toLowerCase();
  const seed = praiseSeed(item);
  if (item.status === 'skipped') {
    const pool = [
      `${short} chưa làm được lần này — lần sau cố thêm nhé!`,
      `${short} chưa kịp lần này — mai thử lại nhé!`,
    ];
    return stablePick(`${seed}:skip`, pool);
  }
  if (item.status === 'done' && journalDoneIsLate(item)) {
    return journalLateNote(title, short, nextTitle, seed);
  }
  if (t.includes('đánh răng')) {
    const pool = [
      'Tự giác hoàn thành trước giờ. Mẹ rất tự hào! 💪',
      'Đánh răng đúng giờ — mẹ tự hào lắm! 💪',
    ];
    return stablePick(`${seed}:rang`, pool);
  }
  if (t.includes('đọc') || t.includes('sách')) {
    const pool = [
      'Con đã đọc rất tập trung. Hôm nay con chọn sách hay quá!',
      'Mẹ thấy con đọc rất chăm — hay lắm! 📖',
    ];
    return stablePick(`${seed}:doc`, pool);
  }
  if (t.includes('cặp') || t.includes('balo') || t.includes('chuẩn bị')) {
    const pool = [
      `${short} đã chụp ảnh cặp sách. Mẹ kiểm tra giúp nhé!`,
      `${short} chuẩn bị xong — mẹ xem giúp con nhé!`,
    ];
    return stablePick(`${seed}:cap`, pool);
  }
  if (t.includes('tưới') || t.includes('cây')) {
    const pool = [
      'Khu vườn của con đang lớn lên mỗi ngày! 🌱',
      'Cây nhà mình khỏe hơn nhờ con chăm sóc! 🌱',
    ];
    return stablePick(`${seed}:cay`, pool);
  }
  if (t.includes('ngủ')) {
    const pool = [
      'Ngủ sớm để mai tràn đầy năng lượng nhé con! 😴',
      'Ngủ đúng giờ — mai dậy khỏe hơn nhé! 😴',
    ];
    return stablePick(`${seed}:ngu`, pool);
  }
  if (t.includes('bài') || t.includes('học')) {
    const pool = [
      `${short} đã cố gắng hoàn thành bài học!`,
      `${short} học xong rồi — mẹ tự hào! 💪`,
    ];
    return stablePick(`${seed}:hoc`, pool);
  }
  return stablePick(`${seed}:on`, JOURNAL_ON_TIME_GENERIC)(short, title, '');
}

function itemTimeLabel(item: DayFlowCommitment): string {
  const raw = item.windowStart || item.windowEnd;
  if (raw) return raw.slice(0, 5);
  return '--:--';
}

const MOODS = FAMILY_MOODS;

type Props = {
  childName: string;
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
  starBalance?: number;
  onStarBalanceChange?: (balance: number) => void;
  onDone: (
    item: DayFlowCommitment,
    evidenceUrl?: string,
  ) => Promise<{ starDelta?: number; starLabelVi?: string; memberStarBalance?: number } | void>;
  onReflect: (item: DayFlowCommitment, reason: SkipReasonCode) => void;
  onHoldSwitchStart: () => void;
  onHoldSwitchCancel: () => void;
  holdProgress: number;
  holdHolding: boolean;
  onOpenParentPin: () => void;
};

export function KidFocusView({
  childName,
  items,
  busyId,
  celebrating,
  streak = 0,
  flowDate = '',
  localTime,
  todayBeautiful = false,
  glanceDays = [],
  teamPercent = 0,
  teamRemaining = 0,
  teamComplete = false,
  teamFromApi = false,
  softLockActive = false,
  softLockLabel,
  familyId,
  childMemberId,
  starBalance = 0,
  onStarBalanceChange,
  onDone,
  onReflect,
  onHoldSwitchStart,
  onHoldSwitchCancel,
  holdProgress,
  holdHolding,
  onOpenParentPin,
}: Props) {
  const [tab, setTab] = useState<KidTab>('home');
  const [filter, setFilter] = useState<DayPart>('all');
  const [nowOpen, setNowOpen] = useState(true);
  const [waitOpen, setWaitOpen] = useState(true);
  const [soonOpen, setSoonOpen] = useState(true);
  const [treasureToast, setTreasureToast] = useState<string | null>(null);
  const [moodIdx, setMoodIdx] = useState(3);
  const [moodNote, setMoodNote] = useState('');
  const [moodSaving, setMoodSaving] = useState(false);
  const [moodLoaded, setMoodLoaded] = useState(false);
  const [journalToast, setJournalToast] = useState<string | null>(null);
  const [momentIdx, setMomentIdx] = useState(0);
  const [journalDayIdx, setJournalDayIdx] = useState(5);
  const [active, setActive] = useState<DayFlowCommitment | null>(null);
  const [askReason, setAskReason] = useState(false);
  const [evidenceFile, setEvidenceFile] = useState<File | null>(null);
  const [evidencePreview, setEvidencePreview] = useState<string | null>(null);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [celebrate, setCelebrate] = useState<{
    title: string;
    stars: number;
    labelVi?: string;
  } | null>(null);
  const [foxyGlow, setFoxyGlow] = useState(false);
  const [foxyIdx, setFoxyIdx] = useState(0);
  const [thanksSending, setThanksSending] = useState(false);
  const [thanksSent, setThanksSent] = useState(false);
  const [thanksError, setThanksError] = useState<string | null>(null);
  const [missionDoneError, setMissionDoneError] = useState<string | null>(null);
  const [localStars, setLocalStars] = useState(starBalance);
  const [rewardCatalog, setRewardCatalog] = useState<RewardCatalogItem[]>([]);
  const [redemptions, setRedemptions] = useState<RewardRedemption[]>([]);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [treasureLoading, setTreasureLoading] = useState(false);
  const [redeemBusyId, setRedeemBusyId] = useState<string | null>(null);
  const [treasureSheet, setTreasureSheet] = useState<'rewards' | 'badges' | null>(null);
  const [journalSheet, setJournalSheet] = useState<'memories' | 'moments' | null>(null);
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

  const filteredPending = useMemo(() => {
    if (filter === 'done') return [] as DayFlowCommitment[];
    if (filter === 'all') return pendingItems;
    return pendingItems.filter((i) => dayPartOf(i) === filter);
  }, [pendingItems, filter]);

  const filteredDone = useMemo(() => {
    if (filter === 'all' || filter === 'done') return doneItems;
    return doneItems.filter((i) => dayPartOf(i) === filter);
  }, [doneItems, filter]);

  const doNowItems = useMemo(
    () =>
      filteredPending.filter(
        (c) => c.reminderState === 'overdue' || c.reminderState === 'due_now',
      ),
    [filteredPending],
  );
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

  const challengeTarget = 3;
  const challengeDone = Math.min(challengeTarget, doneCount);
  const challengePct = Math.round((challengeDone / challengeTarget) * 100);
  const missionSegs = 7;
  const missionFilled = Math.round((unlockPct / 100) * missionSegs);

  const garden = useMemo(
    () =>
      trulyDone.slice(0, 8).map((c) => {
        const stars = gardenStarsForCommitment(c);
        const { plant, mood } = gardenPlantForStars(c.title, stars);
        return {
          id: c.id,
          plant,
          mood,
          badge: gardenBadge(c.title),
          stars,
          label: c.title,
        };
      }),
    [trulyDone],
  );

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
      picks.push(praisePrideLine(short, c, flowDate));
    }
    for (const c of late.slice(0, 2)) {
      picks.push(praiseEncouragementLine(short, c, nextMission?.title, flowDate));
    }

    if (todayBeautiful && onTime.length > 0) {
      picks.unshift(stablePick(`${flowDate}:${short}:beautiful-on`, BEAUTIFUL_DAY_ON_TIME)(short));
    } else if (todayBeautiful && late.length > 0 && onTime.length === 0) {
      picks.unshift(stablePick(`${flowDate}:${short}:beautiful-late`, BEAUTIFUL_DAY_LATE_ONLY)(short));
    }

    if (picks.length === 0 && doneCount > 0) {
      if (late.length > 0 && onTime.length === 0) {
        picks.push(praiseEncouragementLine(short, late[0], nextMission?.title, flowDate));
      } else {
        const movieNight = [
          `Mẹ rất vui vì ${short} đang giúp cả nhà mở Movie Night! ❤️`,
          `${short} đang giúp cả nhà gần Movie Night hơn — mẹ tự hào! 🎬`,
        ];
        picks.push(stablePick(`${flowDate}:${short}:movie`, movieNight));
      }
    }
    return picks.slice(0, 4);
  }, [trulyDone, todayBeautiful, doneCount, childName, nextMission?.title, flowDate]);

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

  const allDoneCheer = useMemo(() => {
    const s = shortChildName(childName);
    return stablePick(`${flowDate}:${s}:all-done`, ALL_DONE_CHEER)(s);
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
      const delta = result?.starDelta ?? 0;
      setCelebrate({
        title: item.title,
        stars: delta,
        labelVi: result?.starLabelVi,
      });
      setFoxyGlow(true);
    } catch {
      setMissionDoneError('Chưa lưu được — thử lại nhé.');
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
        url = await uploadCommitmentEvidence(familyId, evidenceFile);
      }
      const result = await onDone(active, url);
      closeAction();
      const delta = result?.starDelta ?? 0;
      setCelebrate({
        title: finishedTitle,
        stars: delta,
        labelVi: result?.starLabelVi,
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
  const foxySpeech = livingFoxy(
    childName,
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
  const xpNeed = 500;
  const xpHave = familyXp >= xpNeed ? xpNeed : familyXp;
  const segments = 10;
  const filledSegs = Math.round((unlockPct / 100) * segments);

  const redeemCatalog = useMemo(() => {
    return rewardCatalog.map((item, idx) => {
      const tone = item.tone || CATALOG_TONES[idx % CATALOG_TONES.length];
      const isSpecial = Boolean(item.isSpecial) || item.cost == null;
      const cost = item.cost ?? null;
      const canAfford = cost != null && stars >= cost;
      const canRedeem = canAfford && !isSpecial;
      const ctaLabel = isSpecial
        ? ('Để dành' as const)
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

  const kidBadges = useMemo(() => {
    const hasRedeem = redemptions.length > 0;
    return [
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
        unlocked: unlockPct >= 100,
        progress: Math.min(100, unlockPct),
        hint:
          unlockPct >= 100
            ? 'Đã mở khóa!'
            : `Cả nhà hoàn thành ${Math.max(0, 100 - unlockPct)}% nữa`,
      },
    ];
  }, [redemptions.length, stars, streak, teamComplete, unlockPct]);

  const bigAchievements = useMemo(() => {
    const movieTimes = teamUnlocks.filter((u) => u.status === 'confirmed').length;
    const readTimes = trulyDone.filter(
      (c) => c.status === 'done' && /đọc|sách/i.test(c.title),
    ).length;
    return [
      {
        id: 'mn',
        icon: '🎬',
        title: 'Movie Night',
        value: movieTimes > 0 ? `${movieTimes} lần` : '—',
        note: movieTimes > 0 ? 'Cả nhà cùng vui!' : 'Hoàn thành nhiệm vụ nhóm nhé!',
      },
      {
        id: 'read',
        icon: '📘',
        title: 'Đọc sách cùng mẹ',
        value: readTimes > 0 ? `${readTimes} lần` : '—',
        note: readTimes > 0 ? 'Thói quen tuyệt vời!' : 'Thử đọc sách hôm nay nhé!',
      },
      {
        id: 'garden',
        icon: '🌱',
        title: 'Khu vườn',
        value: `Cấp ${explorerLevel}`,
        note: doneCount > 0 ? 'Cây đã lớn rất nhanh!' : 'Bắt đầu từ nhiệm vụ đầu tiên!',
      },
      {
        id: 'foxy',
        icon: '🦊',
        title: 'Foxy',
        value: `${formatStars(stars)} — ${starBalanceNote(stars)}`,
        note: `Bạn thân của ${short}!`,
      },
      {
        id: 'stars',
        icon: '⭐',
        title: 'Sao đã tích lũy',
        value: formatStars(stars),
        note: starBalanceNote(stars),
      },
    ];
  }, [teamUnlocks, trulyDone, explorerLevel, doneCount, short, stars]);

  const handleRedeem = async (item: (typeof redeemCatalog)[number]) => {
    if (item.isSpecial || item.cost == null) {
      showTreasureToast('Để dành — bố mẹ sẽ chọn bất ngờ cho con!');
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

  const openJournalSheet = (kind: 'memories' | 'moments') => {
    journalSheetOpenedAt.current = Date.now();
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
    const today = new Date();
    return Array.from({ length: 6 }, (_, i) => {
      const d = new Date(today);
      d.setDate(today.getDate() - (5 - i));
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
  }, []);

  const selectedJournalDay =
    journalDays[Math.min(journalDayIdx, journalDays.length - 1)] ?? journalDays[journalDays.length - 1];

  const journalEntries = useMemo(() => {
    return [...items]
      .sort((a, b) => {
        const aw = a.windowStart || a.windowEnd || '99:99';
        const bw = b.windowStart || b.windowEnd || '99:99';
        return aw.localeCompare(bw);
      })
      .map((item) => {
        const uxState = kidMissionUxState(item, flowDate);
        const skipped = uxState === 'skipped';
        const wait = uxState === 'awaiting_check';
        const done = uxState === 'done';
        const isLate = done && journalDoneIsLate(item);
        return {
          item,
          part: dayPartOf(item),
          time: itemTimeLabel(item),
          done,
          wait,
          skipped,
          pending: !done && !skipped && !wait,
          isLate,
          statusLine: done ? journalDoneStatusLine(short, item, flowDate) : null,
          note: journalNote(item, short, nextMission?.title),
          reward: commitmentDisplayDelta(item),
          lateCaption: isLate ? lateStarCaption(item, flowDate) : null,
        };
      });
  }, [items, short, flowDate, nextMission?.title]);

  const familyMemories = useMemo(
    () =>
      buildFamilyMemories({
        childShort: short,
        redemptions,
        teamUnlocks,
        doneItems: trulyDone,
        voice: 'kid',
      }),
    [short, redemptions, teamUnlocks, trulyDone],
  );

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

  const upcoming = useMemo(
    () =>
      pendingItems
        .filter((c) => !nextMission || c.id !== nextMission.id)
        .slice(0, 3),
    [pendingItems, nextMission],
  );

  const createdBits = useMemo(() => {
    const bits: Array<{ icon: string; text: string }> = [];
    if (doneCount > 0) {
      bits.push({
        icon: '🌱',
        text: `Khu vườn của ${short} có ${Math.min(doneCount, 8)} cây hôm nay!`,
      });
    }
    if (todayStarsEarned > 0) {
      bits.push({
        icon: '⭐',
        text: `Hôm nay con kiếm được ${formatStarDelta(todayStarsEarned)} sao!`,
      });
    }
    bits.push({
      icon: '🎬',
      text:
        teamComplete || unlockPct >= 100
          ? 'Movie Night sẵn sàng — nhờ bố mẹ xác nhận!'
          : `Movie Night đã lên đến ${unlockPct}%!`,
    });
    return bits.slice(0, 3);
  }, [doneCount, short, unlockPct, todayStarsEarned, teamComplete]);

  const praiseLine =
    praiseMoments[0] ??
    (trulyDone.some((c) => c.isLateDone) && !trulyDone.some((c) => !c.isLateDone)
      ? praiseEncouragementLine(
          short,
          trulyDone.find((c) => c.isLateDone)!,
          nextMission?.title,
          flowDate,
        )
      : stablePick(`${flowDate}:${short}:fallback`, PRAISE_FALLBACK)(short));

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
      setTreasureToast(
        res.alreadySent
          ? stablePick(`${flowDate}:thanks-already`, [
              'Mẹ đã nhận lời cảm ơn hôm nay rồi! 💖',
              'Mẹ biết con cảm ơn rồi — mai nói thêm nhé! 💖',
            ])
          : stablePick(`${flowDate}:thanks-sent`, [
              'Đã gửi lời cảm ơn tới mẹ! 💖',
              'Mẹ nhận được lời cảm ơn của con rồi! 💖',
            ]),
      );
    } catch {
      setThanksError('Chưa gửi được — thử lại nhé.');
    } finally {
      setThanksSending(false);
    }
  };

  const foxyLines = useMemo(
    () => [
      foxySpeech,
      `Tuyệt vời! ${shortChildName(childName)} đang giúp cả nhà tiến gần hơn đến Movie Night đó! ❤️`,
      remaining > 0
        ? `Còn ${remaining} việc nữa — Foxy tin ${shortChildName(childName)} làm được!`
        : `Hôm nay ${shortChildName(childName)} đã xong phần của mình rồi!`,
      teamComplete
        ? `Movie Night mở được rồi — nhờ bố mẹ xác nhận nhé!`
        : `Mỗi việc xong = nhà mình gần Movie Night thêm một chút!`,
    ],
    [foxySpeech, childName, remaining, teamComplete],
  );

  useEffect(() => {
    setFoxyIdx(0);
  }, [foxySpeech, celebrating]);

  return (
    <section className={`kid-home kid-v2${celebrating || celebrate ? ' is-pop' : ''}`}>
      {softLockActive ? (
        <div className="kh-soft-lock" role="status">
          <div className="kh-soft-lock-inner">
            <span aria-hidden>🔒</span>
            <div>
              <strong>Soft-lock · thỏa thuận nhà</strong>
              <p>
                {softLockLabel
                  ? `Đang áp dụng: ${softLockLabel}. `
                  : 'Đang áp dụng thỏa thuận màn hình. '}
                Con vẫn làm Mission được. Đổi người cần mã bố mẹ.
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
          <div>
            {tab === 'tasks' ? (
              <>
                <h1 className="kv2-hello">
                  Nhiệm vụ của {short} <span aria-hidden>✨</span>
                </h1>
                <p className="kv2-date">Cùng cố gắng nhé! 💪</p>
              </>
            ) : tab === 'rewards' ? (
              <>
                <h1 className="kv2-hello">
                  Kho báu của {short} <span aria-hidden>✨</span>
                </h1>
                <p className="kv2-date">Mỗi việc tốt, một kho báu lớn! 🧡</p>
              </>
            ) : tab === 'log' ? (
              <>
                <h1 className="kv2-hello">
                  Nhật ký của {short} <span aria-hidden>✨</span>
                </h1>
                <p className="kv2-date">Những khoảnh khắc tuyệt vời mỗi ngày ❤️</p>
              </>
            ) : (
              <>
                <h1 className="kv2-hello">
                  Chào {short}! <span aria-hidden>👋</span>
                </h1>
                <p className="kv2-date">{formatLongDate()}</p>
              </>
            )}
          </div>
        </div>
        <div className="kv2-top-pills">
          <span className="kv2-pill kv2-stars" title="Sao">
            <span aria-hidden>⭐</span>
            <strong>{formatStars(stars)}</strong>
            {tab === 'tasks' || tab === 'rewards' || tab === 'log' ? (
              <em className="kv2-stars-label">Sao của {short}</em>
            ) : null}
          </span>
          {tab === 'rewards' ? (
            <span className="kv2-pill kv2-level" title="Cấp độ">
              <span aria-hidden>🏔️</span>
              <strong>Level {explorerLevel}</strong>
              <em className="kv2-stars-label">Explorer</em>
            </span>
          ) : tab === 'log' ? (
            <span className="kv2-pill kv2-streak" title="Chuỗi ngày">
              <span aria-hidden>📅</span>
              <strong>{streak}</strong>
              <em className="kv2-stars-label">Ngày liên tiếp</em>
            </span>
          ) : (
            <span className="kv2-pill kv2-movie-mini" title="Movie Night">
              <span aria-hidden>❤️</span>
              <em>Movie Night</em>
              <i className="kv2-mini-bar" aria-hidden>
                <b style={{ width: `${unlockPct}%` }} />
              </i>
              <strong>{unlockPct}%</strong>
            </span>
          )}
          {tab === 'tasks' ? (
            <button
              type="button"
              className="kv2-gift"
              aria-label="Phần thưởng"
              title="Phần thưởng"
              onClick={() => setTab('rewards')}
            >
              <span aria-hidden>🎁</span>
            </button>
          ) : tab === 'rewards' ? (
            <button
              type="button"
              className="kv2-gear"
              aria-label="Cài đặt bố mẹ"
              title="Bố mẹ"
              onClick={onOpenParentPin}
            >
              <span aria-hidden>⚙️</span>
            </button>
          ) : (
            <button
              type="button"
              className={`kv2-bell${tab === 'log' ? ' has-dot' : ''}`}
              aria-label="Đổi sang bố mẹ"
              title="Bố mẹ"
              onClick={onOpenParentPin}
            >
              <span aria-hidden>🔔</span>
            </button>
          )}
        </div>
      </header>

      {tab === 'home' ? (
        <div className="kv2-home">
          <div className="kv2-hero-row">
            <article className={`kv2-movie${teamComplete ? ' is-ready' : ''}`}>
              <div className="kv2-movie-copy">
                <p className="kv2-movie-eyebrow">
                  Cùng cả nhà mở <strong>MOVIE NIGHT</strong>
                </p>
                <div className="kv2-segs" aria-hidden>
                  {Array.from({ length: segments }, (_, i) => (
                    <span key={i} className={i < filledSegs ? 'is-on' : undefined} />
                  ))}
                </div>
                <div className="kv2-movie-meta">
                  <strong>{unlockPct}%</strong>
                  <span>
                    {teamComplete || unlockLeft === 0
                      ? 'Sẵn sàng mở khóa!'
                      : unlockLeft === 1
                        ? 'Chỉ còn 1 việc nữa thôi!'
                        : `Chỉ còn ${unlockLeft} việc nữa thôi!`}
                  </span>
                </div>
              </div>
              <div className="kv2-movie-art" aria-hidden>
                <span className="kv2-popcorn">🍿</span>
                <span className="kv2-play">▶</span>
              </div>
            </article>

            <article className="kv2-house">
              <div className="kv2-house-scene" aria-hidden>
                <span className="kv2-house-emoji">🏡</span>
                <span className="kv2-house-sun">☀️</span>
                <span className="kv2-house-cloud">☁️</span>
              </div>
              <div className="kv2-house-card">
                <div className="kv2-house-head">
                  <span aria-hidden>❤️</span>
                  <strong>Gia đình mình</strong>
                  <em>Cấp {Math.min(4, familyLevel)}</em>
                </div>
                <div className="kv2-xp">
                  <div className="kv2-xp-bar">
                    <span style={{ width: `${Math.round((xpHave / xpNeed) * 100)}%` }} />
                  </div>
                  <p>
                    {xpHave} / {xpNeed} XP
                  </p>
                </div>
              </div>
            </article>
          </div>

          <div className="kv2-focus-row">
            <article className="kv2-next">
              <p className="kv2-section-label">
                <span aria-hidden>✨</span> NHIỆM VỤ TIẾP THEO
              </p>
              {nextMission ? (
                <>
                  <div className="kv2-next-body">
                    <div className={`kv2-next-art tone-${taskIconTone(nextMission.title)}`} aria-hidden>
                      <span className="kv2-emoji-bounce">{taskIcon(nextMission.title)}</span>
                    </div>
                    <div>
                      <h2>{nextMission.title}</h2>
                      <p className="kv2-next-part">{dayPartLabel(nextMission)}</p>
                      <p className="kv2-next-timer">
                        <span aria-hidden>⏰</span>{' '}
                        {earlyCompleteBlockReason(nextMission, localTime) ??
                          minutesUntilExcited(nextMission) ??
                          formatWindow(nextMission.windowStart, nextMission.windowEnd) ??
                          'Trong ngày'}
                      </p>
                      {earlyCompleteBlockReason(nextMission, localTime) ? (
                        <p className="kv2-early-wait muted">
                          {countdownUntilWindow(nextMission, localTime) ??
                            'Chờ đến giờ nhé'}
                        </p>
                      ) : null}
                      <MissionStarBadge item={nextMission} className="kv2-next-stars" />
                    </div>
                  </div>
                  <div className="kv2-next-actions">
                    <button
                      type="button"
                      className="kv2-do"
                      disabled={
                        busyId === nextMission.id ||
                        uploading ||
                        !canCompleteNow(nextMission, localTime)
                      }
                      onClick={() => void quickDoneMission(nextMission)}
                    >
                      <span aria-hidden>✓</span>{' '}
                      {busyId === nextMission.id ? 'Đang gửi…' : 'Mình đã làm'}
                    </button>
                    <button
                      type="button"
                      className="kv2-do-photo"
                      disabled={
                        busyId === nextMission.id ||
                        uploading ||
                        !canCompleteNow(nextMission, localTime)
                      }
                      onClick={() => beginEvidencePick(nextMission)}
                    >
                      <span aria-hidden>📷</span> Đính kèm ảnh đã làm
                    </button>
                  </div>
                  {missionDoneError ? (
                    <p className="kv2-do-error" role="alert">
                      {missionDoneError}
                    </p>
                  ) : null}
                </>
              ) : (
                <div className="kv2-next-done">
                  <div className="kv2-next-art tone-gold kv2-done-pulse" aria-hidden>
                    <span className="kv2-emoji-bounce">🏆</span>
                  </div>
                  <div>
                    <h2>Xong phần của con rồi!</h2>
                    <p>
                      {teamComplete
                        ? 'Cả nhà mở được Movie Night — nhờ bố mẹ xác nhận nhé!'
                        : teamRemaining > 0
                          ? `Cả đội còn ${teamRemaining} việc — con đã giúp Foxy rất nhiều.`
                          : 'Foxy ôm con cái! Nghỉ ngơi vui vẻ nhé.'}
                    </p>
                  </div>
                </div>
              )}
            </article>

            <article className={`kv2-foxy${foxyGlow ? ' is-glow' : ''}`}>
              <p className="kv2-section-label">FOXY NÓI VỚI {short.toUpperCase()}</p>
              <div className="kv2-foxy-row">
                <div className="kv2-foxy-bubble">
                  <p>{foxyLines[foxyIdx % foxyLines.length]}</p>
                </div>
                <div className="kv2-foxy-mascot" aria-hidden>
                  <span className="kv2-fox-face">🦊</span>
                  <span className="kv2-fox-hood">F</span>
                </div>
              </div>
              <div className="kv2-dots" role="tablist" aria-label="Tin nhắn Foxy">
                {foxyLines.map((_, i) => (
                  <button
                    key={i}
                    type="button"
                    className={i === foxyIdx % foxyLines.length ? 'is-on' : undefined}
                    aria-label={`Tin ${i + 1}`}
                    onClick={() => setFoxyIdx(i)}
                  />
                ))}
              </div>
            </article>
          </div>

          <section className="kv2-created">
            <h2>CON VỪA TẠO RA</h2>
            <div className="kv2-created-row">
              {createdBits.map((b) => (
                <article key={b.text} className="kv2-created-card">
                  <span aria-hidden>{b.icon}</span>
                  <p>{b.text}</p>
                </article>
              ))}
            </div>
          </section>

          <div className="kv2-social-row">
            <article className="kv2-praise">
              <p className="kv2-section-label">
                <span aria-hidden>❤️</span> LỜI KHEN HÔM NAY
              </p>
              <div className="kv2-praise-bubble">
                <p>{praiseLine}</p>
              </div>
              <div className="kv2-praise-foot">
                <span className="kv2-mom" aria-hidden>
                  👩
                </span>
                <span>Lời khen hôm nay</span>
                <button
                  type="button"
                  className={`kv2-thanks${thanksSent ? ' is-sent' : ''}`}
                  disabled={thanksSending || thanksSent}
                  onClick={() => void sendThanks()}
                >
                  <span aria-hidden>{thanksSent ? '✓' : '💖'}</span>{' '}
                  {thanksSending ? 'Đang gửi…' : thanksSent ? 'Đã gửi' : 'Cảm ơn mẹ!'}
                </button>
              </div>
              {thanksError ? (
                <p className="kv2-thanks-error" role="alert">
                  {thanksError}
                </p>
              ) : null}
            </article>

            <article className="kv2-streak">
              <p className="kv2-section-label">
                <span aria-hidden>🔥</span> STREAK CỦA {short.toUpperCase()}
              </p>
              <h3>
                {streak > 0 ? `${streak} ngày liên tiếp!` : streakEmpty.headline}
              </h3>
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

          <div className="kv2-bottom-row">
            <section className="kv2-garden">
              <header>
                <h2>
                  KHU VƯỜN CỦA {short.toUpperCase()}
                  <span className="kv2-help" title="Mỗi việc xong = một cây mới" aria-label="Gợi ý">
                    ?
                  </span>
                </h2>
                <button type="button" className="kv2-text-link" onClick={() => setTab('log')}>
                  Xem vườn
                </button>
              </header>
              <div className="kv2-garden-plot">
                {gardenSlots.map((g) => (
                  <div
                    key={g.id}
                    className={`kv2-pot${g.locked ? ' is-locked' : ''}`}
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
            </section>

            <section className="kv2-upcoming">
              <h2>NHIỆM VỤ SẮP TỚI</h2>
              {upcoming.length === 0 ? (
                <div className={`kv2-all-done${dayClosed || remaining === 0 ? ' is-cheer' : ''}`}>
                  <div className="kv2-all-done-icon" aria-hidden>
                    <span className="kv2-emoji-bounce">{dayClosed || remaining === 0 ? '🏆' : '✨'}</span>
                    <span className="kv2-all-done-spark" aria-hidden>
                      ⭐
                    </span>
                  </div>
                  <strong>
                    {dayClosed || remaining === 0 ? 'Xong hết rồi!' : 'Không còn việc sắp tới'}
                  </strong>
                  <p>
                    {dayClosed || remaining === 0
                      ? allDoneCheer
                      : 'Làm việc tiếp theo ở trên, rồi quay lại đây nhé!'}
                  </p>
                </div>
              ) : (
                <ul>
                  {upcoming.map((c) => (
                    <li key={c.id}>
                      <span className={`kv2-task-ico tone-${taskIconTone(c.title)}`} aria-hidden>
                        {taskIcon(c.title)}
                      </span>
                      <strong>{c.title}</strong>
                      <MissionStarBadge item={c} />
                      <em className="kv2-time-pill">{clockOf(c)}</em>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>
        </div>
      ) : null}

      {/* Full checklist only on Tasks tab — mockup Nhiệm vụ */}
      {tab === 'tasks' ? (
        <div className="kv2-missions">
          <div className="kv2-m-filters" role="tablist" aria-label="Lọc nhiệm vụ">
            {filters.map((f) => (
              <button
                key={f.key}
                type="button"
                role="tab"
                aria-selected={filter === f.key}
                className={`kv2-m-filter${filter === f.key ? ' is-on' : ''}${
                  f.key === 'done' ? ' is-done' : ''
                }`}
                onClick={() => setFilter(f.key)}
              >
                <span aria-hidden>{f.icon}</span>
                <em>{f.label}</em>
                {f.count != null ? <b>{f.count}</b> : null}
              </button>
            ))}
          </div>

          <article className="kv2-m-banner">
            <div className="kv2-m-foxy" aria-hidden>
              🦊
            </div>
            <div className="kv2-m-bubble">
              <p>
                {short} đang làm rất tốt! Hoàn thành nhiệm vụ để cùng cả nhà mở Movie Night
                nhé! 🎬
              </p>
            </div>
            <div className="kv2-m-goal">
              <span className="kv2-m-goal-pop" aria-hidden>
                🍿
              </span>
              <div className="kv2-m-goal-copy">
                <span className="kv2-m-goal-eyebrow">Đang tiến gần đến mục tiêu!</span>
                <strong className="kv2-m-goal-pct">{unlockPct}%</strong>
                <div className="kv2-m-segs" aria-hidden>
                  {Array.from({ length: missionSegs }, (_, i) => (
                    <span key={i} className={i < missionFilled ? 'is-on' : undefined} />
                  ))}
                </div>
                <em>
                  {teamComplete || unlockLeft === 0
                    ? 'Sẵn sàng mở khóa!'
                    : unlockLeft === 1
                      ? 'Chỉ còn 1 việc nữa thôi!'
                      : `Chỉ còn ${unlockLeft} việc nữa thôi!`}
                </em>
              </div>
            </div>
          </article>

          {filter === 'done' ? (
            <section className="kv2-m-sec">
              <button
                type="button"
                className="kv2-m-sec-head"
                onClick={() => setSoonOpen((v) => !v)}
              >
                <span>
                  <span aria-hidden>✅</span> ĐÃ XONG ({filteredDone.length})
                </span>
                <span aria-hidden>{soonOpen ? '▴' : '▾'}</span>
              </button>
              {soonOpen ? (
                <ul className="kv2-m-list">
                  {filteredDone.map((item) => {
                    const skipped = item.status === 'skipped';
                    const lateNote = skipped ? null : lateStarCaption(item, flowDate);
                    return (
                      <li key={item.id} className="kv2-m-row is-done">
                        <span
                          className={`kv2-m-ico tone-${taskIconTone(item.title)}`}
                          aria-hidden
                        >
                          {taskIcon(item.title)}
                        </span>
                        <div className="kv2-m-row-body">
                          <strong>{item.title}</strong>
                          {lateNote ? (
                            <span className="kv2-m-time muted">{lateNote}</span>
                          ) : (
                            <span>
                              {skipped
                                ? kidSkipLabel(item.skipReason)
                                : formatWindow(item.windowStart, item.windowEnd) ?? 'Đã xong'}
                            </span>
                          )}
                        </div>
                        {!skipped ? (
                          <MissionStarBadge item={item} />
                        ) : (
                          <span className="kv2-m-badge is-miss">Bỏ qua</span>
                        )}
                      </li>
                    );
                  })}
                  {filteredDone.length === 0 ? (
                    <li className="kv2-m-empty">Chưa có việc xong hôm nay — làm tiếp nhé!</li>
                  ) : null}
                </ul>
              ) : null}
            </section>
          ) : (
            <>
              <section className="kv2-m-sec">
                <button
                  type="button"
                  className="kv2-m-sec-head"
                  onClick={() => setNowOpen((v) => !v)}
                >
                  <span>
                    <span aria-hidden>⏰</span> VIỆC CẦN LÀM NGAY
                    {doNowItems.length ? ` (${doNowItems.length})` : ''}
                  </span>
                  <span aria-hidden>{nowOpen ? '▴' : '▾'}</span>
                </button>
                {nowOpen ? (
                  <div className="kv2-m-now">
                    {doNowItems.length === 0 ? (
                      <p className="kv2-m-empty soft">
                        {dayClosed
                          ? 'Xong hết rồi — Foxy ôm bạn cái! 🦊'
                          : 'Không có việc gấp — xem phần sắp tới nhé!'}
                      </p>
                    ) : (
                      doNowItems.map((item) => (
                        <article key={item.id} className="kv2-m-featured">
                          <div className="kv2-m-featured-top">
                            <span
                              className={`kv2-m-featured-ico tone-${taskIconTone(item.title)}`}
                              aria-hidden
                            >
                              {taskIcon(item.title)}
                            </span>
                            <div className="kv2-m-featured-copy">
                              <strong>{item.title}</strong>
                              <p>{taskTip(item.title)}</p>
                              <span className="kv2-m-urgency">
                                <span aria-hidden>⏱</span>
                                {minutesUntilExcited(item) ??
                                  minutesUntil(item) ??
                                  formatWindow(item.windowStart, item.windowEnd) ??
                                  'Làm ngay nhé!'}
                              </span>
                            </div>
                            <MissionStarBadge item={item} className="is-featured" />
                          </div>
                          <div className="kv2-m-featured-actions">
                            <button
                              type="button"
                              className="kv2-m-cta"
                              disabled={
                                busyId === item.id ||
                                !canCompleteNow(item, localTime)
                              }
                              onClick={() => void quickDoneMission(item)}
                            >
                              {busyId === item.id ? 'Đang gửi…' : 'Mình đã làm'}
                            </button>
                            <button
                              type="button"
                              className="kv2-m-photo-btn"
                              disabled={
                                busyId === item.id ||
                                uploading ||
                                !canCompleteNow(item, localTime)
                              }
                              onClick={() => beginEvidencePick(item)}
                            >
                              <span aria-hidden>📷</span> Đính kèm ảnh đã làm
                            </button>
                          </div>
                        </article>
                      ))
                    )}
                    {missionDoneError ? (
                      <p className="kv2-do-error" role="alert">
                        {missionDoneError}
                      </p>
                    ) : null}
                  </div>
                ) : null}
              </section>

              <section className="kv2-m-sec">
                <button
                  type="button"
                  className="kv2-m-sec-head"
                  onClick={() => setWaitOpen((v) => !v)}
                >
                  <span>
                    <span aria-hidden>⌛</span> CHỜ MẸ KIỂM TRA
                    {waitingCheckItems.length ? ` (${waitingCheckItems.length})` : ''}
                  </span>
                  <span aria-hidden>{waitOpen ? '▴' : '▾'}</span>
                </button>
                {waitOpen ? (
                  <ul className="kv2-m-list">
                    {waitingCheckItems.map((item) => {
                      const lateNote = lateStarCaption(item, flowDate);
                      return (
                        <li key={item.id} className="kv2-m-row">
                          <span
                            className={`kv2-m-ico tone-${taskIconTone(item.title)}`}
                            aria-hidden
                          >
                            {taskIcon(item.title)}
                          </span>
                          <div className="kv2-m-row-body">
                            <strong>{item.title}</strong>
                            {lateNote ? (
                              <span className="kv2-m-time muted">{lateNote}</span>
                            ) : null}
                          </div>
                          <MissionStarBadge item={item} />
                          <span className="kv2-m-badge is-wait">Chờ kiểm tra</span>
                        </li>
                      );
                    })}
                    {waitingCheckItems.length === 0 ? (
                      <li className="kv2-m-empty">Chưa có việc chờ kiểm tra.</li>
                    ) : null}
                  </ul>
                ) : null}
              </section>

              <section className="kv2-m-sec">
                <button
                  type="button"
                  className="kv2-m-sec-head"
                  onClick={() => setSoonOpen((v) => !v)}
                >
                  <span>
                    <span aria-hidden>📅</span> VIỆC SẮP TỚI
                    {soonItems.length ? ` (${soonItems.length})` : ''}
                  </span>
                  <span aria-hidden>{soonOpen ? '▴' : '▾'}</span>
                </button>
                {soonOpen ? (
                  <ul className="kv2-m-list">
                    {soonItems.map((item) => (
                      <li key={item.id} className="kv2-m-row is-soon">
                        <span
                          className={`kv2-m-ico tone-${taskIconTone(item.title)}`}
                          aria-hidden
                        >
                          {taskIcon(item.title)}
                        </span>
                        <div className="kv2-m-row-body">
                          <strong>{item.title}</strong>
                          <span className="kv2-m-time">
                            {earlyCompleteBlockReason(item, localTime) ??
                              minutesUntilExcited(item) ??
                              (item.windowStart
                                ? item.windowStart.slice(0, 5)
                                : formatWindow(item.windowStart, item.windowEnd) ??
                                  'Trong ngày')}
                          </span>
                          {earlyCompleteBlockReason(item, localTime) ? (
                            <span className="kv2-m-time muted">
                              {countdownUntilWindow(item, localTime) ?? 'Chờ đến giờ nhé'}
                            </span>
                          ) : null}
                        </div>
                        <MissionStarBadge item={item} />
                        <div className="kv2-m-row-actions">
                          <button
                            type="button"
                            className="kv2-m-mini-photo"
                            disabled={
                              busyId === item.id ||
                              uploading ||
                              !canCompleteNow(item, localTime)
                            }
                            onClick={() => beginEvidencePick(item)}
                            aria-label={`Đính kèm ảnh ${item.title} đã làm`}
                          >
                            📷
                          </button>
                          <button
                            type="button"
                            className="kv2-m-mini-do"
                            disabled={
                              busyId === item.id ||
                              uploading ||
                              !canCompleteNow(item, localTime)
                            }
                            onClick={() => void quickDoneMission(item)}
                            aria-label={`Làm ${item.title}`}
                          >
                            {busyId === item.id ? '…' : 'Làm'}
                          </button>
                        </div>
                      </li>
                    ))}
                    {soonItems.length === 0 ? (
                      <li className="kv2-m-empty">
                        {dayClosed
                          ? 'Không còn việc sắp tới — tuyệt quá!'
                          : 'Chưa có việc sắp tới trong bộ lọc này.'}
                      </li>
                    ) : null}
                  </ul>
                ) : null}
              </section>
            </>
          )}

          <aside className="kv2-m-challenge">
            <span className="kv2-m-challenge-bulb" aria-hidden>
              💡
            </span>
            <div className="kv2-m-challenge-copy">
              <strong>Tiến độ hôm nay</strong>
              <p>
                {dayClosed
                  ? 'Con đã xong phần việc hôm nay — tuyệt vời!'
                  : `Hoàn thành thêm việc để cả nhà gần Movie Night hơn (${unlockPct}%)`}
              </p>
              <div className="kv2-m-challenge-bar" aria-hidden>
                <b style={{ width: `${challengePct}%` }} />
              </div>
            </div>
            <div className="kv2-m-challenge-side">
              <em>
                {challengeDone} / {challengeTarget}
              </em>
              <span aria-hidden>🎬</span>
            </div>
          </aside>
        </div>
      ) : null}

      {tab === 'rewards' ? (
        <div className="kv2-treasure">
          {treasureToast ? (
            <div className="kv2-t-toast" role="status">
              {treasureToast}
            </div>
          ) : null}

          <article className={`kv2-t-family${teamComplete ? ' is-ready' : ''}`}>
            <span className="kv2-t-family-badge">
              <span aria-hidden>★</span> PHẦN THƯỞNG CẢ GIA ĐÌNH
            </span>
            <div className="kv2-t-family-body">
              <div className="kv2-t-family-art" aria-hidden>
                <span className="kv2-t-popcorn">🍿</span>
                <span className="kv2-t-play">▶</span>
              </div>
              <div className="kv2-t-family-copy">
                <h2>Movie Night</h2>
                <div className="kv2-t-family-bar">
                  <i aria-hidden>
                    <b style={{ width: `${unlockPct}%` }} />
                  </i>
                  <strong>{unlockPct}%</strong>
                </div>
                <p>
                  {teamComplete || unlockLeft === 0
                    ? 'Sẵn sàng mở khóa — nhờ bố mẹ xác nhận!'
                    : unlockLeft === 1
                      ? 'Chỉ còn 1 nhiệm vụ nữa!'
                      : `Chỉ còn ${unlockLeft} nhiệm vụ nữa!`}
                </p>
                <button
                  type="button"
                  className="kv2-t-detail"
                  onClick={() => setTab('home')}
                >
                  Xem chi tiết →
                </button>
              </div>
              <div className="kv2-t-family-chest" aria-hidden>
                <span>🧰</span>
                <aside className="kv2-t-sticky">
                  <span>❤️</span>
                  <p>Cả nhà cùng hoàn thành để mở khóa phần thưởng nhé!</p>
                </aside>
              </div>
            </div>
          </article>

          <section className="kv2-t-sec">
            <div className="kv2-t-sec-head">
              <div>
                <h3>
                  <span aria-hidden>⭐</span> ĐỔI THƯỞNG BẰNG SAO
                </h3>
                <p>Bố mẹ đã thiết lập các phần thưởng cho con</p>
              </div>
              <button
                type="button"
                className="kv2-t-link"
                onClick={() => openTreasureSheet('rewards')}
              >
                Xem tất cả →
              </button>
            </div>
            <div className="kv2-t-redeem">
              {treasureLoading && redeemCatalog.length === 0 ? (
                <p className="muted" style={{ padding: '8px 4px' }}>Đang tải quà…</p>
              ) : null}
              {redeemCatalog.map((item) => {
                const busy = redeemBusyId === item.id;
                return (
                  <article key={item.id} className={`kv2-t-card tone-${item.tone}`}>
                    <span className="kv2-t-card-ico" aria-hidden>
                      {item.icon}
                    </span>
                    <strong>{item.title}</strong>
                    <em>
                      {item.cost == null ? '??? ⭐' : (
                        <>
                          <span aria-hidden>⭐</span> {item.cost}
                        </>
                      )}
                    </em>
                    <button
                      type="button"
                      className={`kv2-t-card-cta${item.canRedeem ? ' is-active' : ' is-save'}`}
                      disabled={busy || (!item.canRedeem && !item.isSpecial)}
                      onClick={() => void handleRedeem(item)}
                    >
                      {busy ? 'Đang đổi…' : item.ctaLabel}
                    </button>
                  </article>
                );
              })}
            </div>
          </section>

          <div className="kv2-t-mid">
            <article className="kv2-t-mystery">
              <h3>KHO BÁU BÍ MẬT</h3>
              <div className="kv2-t-mystery-body">
                <span className="kv2-t-mystery-ico" aria-hidden>
                  📦
                </span>
                <div>
                  <strong>Mystery Box</strong>
                  <p>Mở khi đạt {formatStars(mysteryTarget)} ⭐</p>
                  <div className="kv2-t-mystery-bar" aria-hidden>
                    <b style={{ width: `${mysteryPct}%` }} />
                  </div>
                  <em>
                    {formatStars(mysteryHave)} / {formatStars(mysteryTarget)}
                  </em>
                </div>
              </div>
            </article>

            <article className="kv2-t-badges">
              <div className="kv2-t-sec-head is-compact">
                <h3>HUY HIỆU CỦA {short.toUpperCase()}</h3>
                <button
                  type="button"
                  className="kv2-t-link"
                  onClick={() => openTreasureSheet('badges')}
                >
                  Xem tất cả →
                </button>
              </div>
              <ul className="kv2-t-badge-row">
                {kidBadges.map((b) => (
                  <li
                    key={b.id}
                    className={b.unlocked ? 'is-on' : 'is-off'}
                    title={b.label}
                  >
                    <span aria-hidden>{b.icon}</span>
                    <em>{b.label}</em>
                  </li>
                ))}
              </ul>
            </article>
          </div>

          <section className="kv2-t-sec">
            <div className="kv2-t-sec-head">
              <h3>
                <span aria-hidden>👑</span> THÀNH TỰU LỚN
              </h3>
            </div>
            <div className="kv2-t-achieve">
              {bigAchievements.map((a) => (
                <article key={a.id} className="kv2-t-achieve-card">
                  <span aria-hidden>{a.icon}</span>
                  <strong>{a.title}</strong>
                  <b>{a.value}</b>
                  <em>{a.note}</em>
                </article>
              ))}
            </div>
          </section>

          <section className="kv2-t-sec">
            <div className="kv2-t-sec-head">
              <h3>
                <span aria-hidden>❤️</span> KỶ NIỆM ĐÁNG NHỚ
              </h3>
            </div>
            <div className="kv2-t-memories">
              {treasureMemoriesVisible.length === 0 ? (
                <p className="muted" style={{ padding: '12px 4px', margin: 0 }}>
                  {FAMILY_MEMORY_EMPTY}
                </p>
              ) : (
                <ol className="kv2-t-timeline">
                  {treasureMemoriesVisible.map((m) => (
                    <li key={m.id}>
                      <span className="kv2-t-dot" aria-hidden />
                      <span className="kv2-t-mem-ico" aria-hidden>
                        {m.icon}
                      </span>
                      <strong>{m.title}</strong>
                      <em>{m.date}</em>
                    </li>
                  ))}
                </ol>
              )}
              {familyMemories.length > FAMILY_MEMORY_VISIBLE ? (
                <button
                  type="button"
                  className="kv2-t-link"
                  style={{ marginTop: 8 }}
                  onClick={() => openJournalSheet('memories')}
                >
                  Xem tất cả →
                </button>
              ) : null}
            </div>
          </section>
        </div>
      ) : null}

      {tab === 'log' ? (
        <div className="kv2-journal">
          {journalToast ? (
            <div className="kv2-t-toast" role="status">
              {journalToast}
            </div>
          ) : null}

          <div className="kv2-j-dates">
            <button
              type="button"
              className="kv2-j-nav"
              aria-label="Ngày trước"
              disabled={journalDayIdx <= 0}
              onClick={() => setJournalDayIdx((v) => Math.max(0, v - 1))}
            >
              ‹
            </button>
            <div className="kv2-j-date-scroll" role="tablist" aria-label="Chọn ngày">
              {journalDays.map((d, i) => (
                <button
                  key={d.key}
                  type="button"
                  role="tab"
                  aria-selected={i === journalDayIdx}
                  className={`kv2-j-chip${i === journalDayIdx ? ' is-on' : ''}`}
                  onClick={() => setJournalDayIdx(i)}
                >
                  {d.shortLabel}
                </button>
              ))}
            </div>
          </div>

          <div className="kv2-j-layout">
            <div className="kv2-j-main">
              <h2 className="kv2-j-day-title">{selectedJournalDay.fullLabel}</h2>

              {selectedJournalDay.isToday ? (
                <ol className="kv2-j-timeline">
                  {journalEntries.length === 0 ? (
                    <li className="kv2-j-empty">
                      Hôm nay chưa có trang nhật ký — làm việc đầu tiên rồi quay lại nhé!
                    </li>
                  ) : (
                    journalEntries.map((entry, idx) => {
                      const prevPart = idx > 0 ? journalEntries[idx - 1].part : null;
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
                          className={`kv2-j-node${entry.done ? ' is-done' : ''}${
                            entry.wait ? ' is-wait' : ''
                          }${entry.skipped ? ' is-skip' : ''}${entry.pending ? ' is-pending' : ''}`}
                        >
                          <div className="kv2-j-rail" aria-hidden>
                            {showPart ? (
                              <span className="kv2-j-part">
                                {partIcon} {partText}
                              </span>
                            ) : (
                              <span className="kv2-j-dot" />
                            )}
                            <em className="kv2-j-time">{entry.time}</em>
                          </div>
                          <article className="kv2-j-card">
                            <span
                              className={`kv2-j-ico tone-${taskIconTone(entry.item.title)}`}
                              aria-hidden
                            >
                              {taskIcon(entry.item.title)}
                            </span>
                            <div className="kv2-j-card-body">
                              <strong>{entry.item.title}</strong>
                              {entry.done ? (
                                <span
                                  className={`kv2-j-status is-ok${entry.isLate ? ' is-late' : ''}`}
                                >
                                  {entry.statusLine}
                                </span>
                              ) : entry.wait ? (
                                <span className="kv2-j-status is-wait">
                                  Đang chờ mẹ kiểm tra
                                </span>
                              ) : entry.skipped ? (
                                <span className="kv2-j-status is-skip">
                                  {kidSkipLabel(entry.item.skipReason)}
                                </span>
                              ) : (
                                <span className="kv2-j-status is-pending">Chưa hoàn thành</span>
                              )}
                              <p>{entry.note}</p>
                              {entry.lateCaption ? (
                                <em className="kv2-j-late-cap">{entry.lateCaption}</em>
                              ) : null}
                            </div>
                            <div className="kv2-j-card-side">
                              {entry.item.evidenceUrl ? (
                                <a
                                  className="kv2-j-photo"
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
                                <div className="kv2-j-photo is-placeholder" aria-hidden>
                                  {taskIcon(entry.item.title)}
                                </div>
                              )}
                              {entry.done ? (
                                <span
                                  className={`kv2-j-star-badge${starBadgeClass(entry.reward)}`}
                                >
                                  {formatStarBadge(entry.reward)}
                                </span>
                              ) : entry.wait ? (
                                <span className="kv2-j-star-badge is-wait">
                                  Chờ kiểm tra
                                </span>
                              ) : entry.pending ? (
                                <button
                                  type="button"
                                  className="kv2-j-do-btn"
                                  onClick={() => openAction(entry.item)}
                                >
                                  Làm ngay
                                </button>
                              ) : null}
                            </div>
                          </article>
                        </li>
                      );
                    })
                  )}
                </ol>
              ) : (
                <p className="kv2-j-empty soft">
                  Nhật ký các ngày trước sẽ sớm có — hôm nay hãy làm thật vui nhé!
                </p>
              )}

              <section className="kv2-j-mem-sec">
                <div className="kv2-t-sec-head">
                  <h3>Kỷ niệm đẹp của gia đình</h3>
                  {familyMemories.length > FAMILY_MEMORY_VISIBLE ? (
                    <button
                      type="button"
                      className="kv2-t-link"
                      onClick={() => openJournalSheet('memories')}
                    >
                      Xem tất cả →
                    </button>
                  ) : null}
                </div>
                <div className="kv2-j-mem-row">
                  {journalMemoriesVisible.length === 0 ? (
                    <p className="kv2-j-empty soft">{FAMILY_MEMORY_EMPTY}</p>
                  ) : (
                    journalMemoriesVisible.map((m) => (
                      <article
                        key={m.id}
                        className={`kv2-j-mem-card${m.locked ? ' is-locked' : ''}`}
                      >
                        {m.isNew ? <span className="kv2-j-new">Mới</span> : null}
                        <span aria-hidden>{m.icon}</span>
                        <strong>{m.title}</strong>
                        <em>{m.date}</em>
                      </article>
                    ))
                  )}
                </div>
              </section>
            </div>

            <aside className="kv2-j-side">
              <article className="kv2-j-summary">
                <span className="kv2-j-trophy" aria-hidden>
                  🏆
                </span>
                <div>
                  <p>
                    Tuyệt vời! {short} đã hoàn thành{' '}
                    <strong>
                      {doneCount} / {Math.max(total, 1)}
                    </strong>{' '}
                    việc
                  </p>
                  <div className="kv2-j-summary-meta">
                    <span>{formatStarDelta(todayStarsEarned)} ⭐</span>
                    <em>Hôm nay</em>
                  </div>
                </div>
              </article>

              <article className="kv2-j-mood">
                <h3>Tâm trạng của {short}</h3>
                <p className="kv2-j-mood-ask">Con cảm thấy thế nào hôm nay?</p>
                <div className="kv2-j-mood-pick" aria-label="Chọn tâm trạng">
                  <span className="kv2-j-mood-current">
                    {MOODS[moodIdx].emoji} {MOODS[moodIdx].label}
                  </span>
                  <div className="kv2-j-mood-row">
                    {MOODS.map((m, i) => (
                      <button
                        key={m.code}
                        type="button"
                        className={i === moodIdx ? 'is-on' : undefined}
                        aria-label={m.label}
                        aria-pressed={i === moodIdx}
                        onClick={() => setMoodIdx(i)}
                      >
                        {m.emoji}
                      </button>
                    ))}
                  </div>
                </div>
                <label className="kv2-j-mood-note">
                  <span className="sr-only">Ghi chú nhật ký</span>
                  <textarea
                    rows={3}
                    placeholder="Con muốn kể thêm gì không?"
                    value={moodNote}
                    onChange={(e) => setMoodNote(e.target.value)}
                  />
                </label>
                <button
                  type="button"
                  className="kv2-j-save"
                  disabled={!moodLoaded || moodSaving || !childMemberId}
                  onClick={() => void saveMoodEntry()}
                >
                  {moodSaving ? 'Đang lưu…' : 'Lưu nhật ký'}
                </button>
              </article>

              <article className="kv2-j-moment">
                <div className="kv2-t-sec-head is-compact">
                  <h3>Khoảnh khắc đáng nhớ</h3>
                  <button
                    type="button"
                    className="kv2-t-link"
                    onClick={() => {
                      if (journalFeatureMoments.length > 0) openJournalSheet('moments');
                    }}
                    disabled={journalFeatureMoments.length === 0}
                  >
                    Xem tất cả →
                  </button>
                </div>
                {journalFeatureMoments.length === 0 ? (
                  <p className="kv2-j-empty soft">{FAMILY_MEMORY_EMPTY}</p>
                ) : (
                  <>
                <div className="kv2-j-moment-card">
                  <span className="kv2-j-moment-heart" aria-hidden>
                    ❤️
                  </span>
                  <div className="kv2-j-moment-art" aria-hidden>
                    {journalFeatureMoments[momentIdx]?.icon}
                  </div>
                  <strong>{journalFeatureMoments[momentIdx]?.title}</strong>
                  <em>{journalFeatureMoments[momentIdx]?.date}</em>
                  <p>{journalFeatureMoments[momentIdx]?.caption}</p>
                </div>
                <div className="kv2-j-dots" role="tablist" aria-label="Chuyển khoảnh khắc">
                  {journalFeatureMoments.map((m, i) => (
                    <button
                      key={m.id}
                      type="button"
                      role="tab"
                      aria-selected={i === momentIdx}
                      className={i === momentIdx ? 'is-on' : undefined}
                      onClick={() => setMomentIdx(i)}
                    />
                  ))}
                </div>
                  </>
                )}
              </article>

              {softLockActive ? (
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
              )}
            </aside>
          </div>
        </div>
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
          <span aria-hidden>📋</span>
          <em>Nhiệm vụ</em>
        </button>
        <button
          type="button"
          className="kv2-fab"
          aria-label={nextMission ? `Làm ${nextMission.title}` : 'Kho báu'}
          disabled={Boolean(nextMission && !canCompleteNow(nextMission, localTime))}
          onClick={() => {
            if (nextMission && canCompleteNow(nextMission, localTime)) {
              void quickDoneMission(nextMission);
            } else if (!nextMission) setTab('rewards');
          }}
        >
          <span aria-hidden>⭐</span>
        </button>
        <button
          type="button"
          className={`kv2-tab${tab === 'rewards' ? ' is-on' : ''}`}
          onClick={() => setTab('rewards')}
        >
          <span aria-hidden>🧰</span>
          <em>Kho báu</em>
        </button>
        <button
          type="button"
          className={`kv2-tab${tab === 'log' ? ' is-on' : ''}`}
          onClick={() => setTab('log')}
        >
          <span aria-hidden>📖</span>
          <em>Nhật ký</em>
        </button>
      </nav>

      {celebrate ? (
        <div className="ka-celebrate" role="status" onClick={dismissCelebrate}>
          <div className="ka-celebrate-card" onClick={(e) => e.stopPropagation()}>
            <p className="ka-celebrate-emoji" aria-hidden>
              🎉
            </p>
            <h2>{celebrateHeadline(celebrate.title, celebrate.stars)}</h2>
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
              <div className="kv2-j-sheet-list">
                {familyMemories.length === 0 ? (
                  <p className="muted">{FAMILY_MEMORY_EMPTY}</p>
                ) : (
                  familyMemories.map((m) => (
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
                          disabled={busy || (!item.canRedeem && !item.isSpecial)}
                          onClick={() => void handleRedeem(item)}
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
                {kidBadges.map((b) => (
                  <li key={b.id} className={b.unlocked ? 'is-on' : 'is-off'}>
                    <span className="kv2-t-sheet-badge-ico" aria-hidden>
                      {b.icon}
                    </span>
                    <div className="kv2-t-sheet-badge-copy">
                      <strong>{b.label}</strong>
                      <p>{b.hint}</p>
                      {!b.unlocked ? (
                        <div className="kv2-t-sheet-badge-bar" aria-hidden>
                          <b style={{ width: `${b.progress}%` }} />
                        </div>
                      ) : null}
                    </div>
                    <em>{b.unlocked ? '✓' : `${b.progress}%`}</em>
                  </li>
                ))}
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
            {!askReason ? (
              <>
                <button
                  type="button"
                  className="kv2-do-photo kv2-sheet-photo"
                  disabled={
                    busyId === active.id ||
                    uploading ||
                    !canCompleteNow(active, localTime)
                  }
                  onClick={() => beginEvidencePick(active)}
                >
                  {evidenceFile ? 'Đổi ảnh đã làm' : '📷 Đính kèm ảnh đã làm'}
                </button>
                {evidencePreview ? (
                  <img src={evidencePreview} alt="Ảnh đã chọn" className="evidence-thumb" />
                ) : (
                  <p className="muted" style={{ margin: 0, fontSize: 13 }}>
                    Tuỳ chọn — chụp hoặc chọn ảnh từ thư viện để bố mẹ xem.
                  </p>
                )}
                {evidenceError ? <div className="banner-error">{evidenceError}</div> : null}
                <button
                  type="button"
                  className="btn btn-primary kid-done"
                  disabled={
                    busyId === active.id ||
                    uploading ||
                    !canCompleteNow(active, localTime)
                  }
                  onClick={() => void submitDone()}
                >
                  {uploading || busyId === active.id ? 'Đang lưu…' : 'Mình đã làm!'}
                </button>
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
    </section>
  );
}
