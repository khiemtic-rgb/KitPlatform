import type {
  DayFlowCommitment,
  FamilyMemoryEntry,
  ParentVoiceMessage,
  WeeklyStory,
} from '@/shared/api/family-os.api';
import { pickMemoryWinVi } from '@/shared/value/home-family-feed';
import { shortMemberName } from '@/modules/flow/relationshipGraph';
import { FAMILY_MOODS } from '@/shared/flow/family-moods';

export type TodayOpenCtaKind =
  | 'verify_evidence'
  | 'approve_stars'
  | 'open_voice'
  | 'ack_parent_voice'
  | 'ack_partner_voice'
  | 'scroll_missions'
  | 'open_memory'
  | 'dismiss'
  | 'dismiss_thanks'
  | 'open_kid_moment'
  | 'ack_kid_moment';

export type WarmthPulse = {
  id: string;
  icon: string;
  eyebrowVi: string;
  titleVi: string;
  bodyVi: string;
  ctaLabelVi: string;
  cta: TodayOpenCtaKind;
  ctaId?: string;
};

export type PendingAction = {
  id: string;
  icon: string;
  titleVi: string;
  detailVi: string;
  ctaLabelVi: string;
  cta: TodayOpenCtaKind;
  ctaId?: string;
  priority: number;
};

export type SeenSignal = {
  id: string;
  icon: string;
  textVi: string;
};

export type MemoryYarn = {
  id: string;
  icon: string;
  eyebrowVi: string;
  textVi: string;
  ctaLabelVi: string;
  relatedMemberName?: string;
};

export const TINY_RITUAL_MOODS = FAMILY_MOODS;

export const TINY_RITUAL_WARM_CHIPS_PARENT = [
  'Mẹ/bố thấy con hôm nay rồi — tự hào lắm.',
  'Cả nhà đang bên nhau, từng chút một.',
  'Con cứ làm nhẹ nhàng; nhà luôn đón con.',
] as const;

export const TINY_RITUAL_WARM_CHIPS_KID = [
  'Con cảm ơn bố mẹ đã luôn quan tâm.',
  'Hôm nay con sẽ cố gắng vì nhà mình.',
  'Con muốn bố mẹ biết: con ổn và yêu nhà.',
] as const;

function storageKey(
  kind: 'warmth' | 'ritual' | 'yarn',
  memberId: string,
  flowDate: string,
): string {
  return `famixa.open.${kind}.${memberId || 'anon'}.${flowDate}`;
}

export function isWarmthDismissed(memberId: string, flowDate: string): boolean {
  try {
    return localStorage.getItem(storageKey('warmth', memberId, flowDate)) === '1';
  } catch {
    return false;
  }
}

export function dismissWarmth(memberId: string, flowDate: string): void {
  try {
    localStorage.setItem(storageKey('warmth', memberId, flowDate), '1');
  } catch {
    /* ignore */
  }
}

export function isRitualDone(memberId: string, flowDate: string): boolean {
  try {
    return localStorage.getItem(storageKey('ritual', memberId, flowDate)) === '1';
  } catch {
    return false;
  }
}

export function markRitualDone(memberId: string, flowDate: string): void {
  try {
    localStorage.setItem(storageKey('ritual', memberId, flowDate), '1');
  } catch {
    /* ignore */
  }
}

export function isYarnDismissed(memberId: string, flowDate: string): boolean {
  try {
    return localStorage.getItem(storageKey('yarn', memberId, flowDate)) === '1';
  } catch {
    return false;
  }
}

export function dismissYarn(memberId: string, flowDate: string): void {
  try {
    localStorage.setItem(storageKey('yarn', memberId, flowDate), '1');
  } catch {
    /* ignore */
  }
}

/** Relative time in Vietnamese — Soft Presence copy. */
export function formatRelativeVi(iso: string | undefined, now = new Date()): string | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  if (!Number.isFinite(t)) return null;
  const mins = Math.round((now.getTime() - t) / 60000);
  if (mins < 0) return 'vừa xong';
  if (mins < 1) return 'vừa xong';
  if (mins < 60) return `${mins} phút trước`;
  const hours = Math.round(mins / 60);
  if (hours < 24) return `${hours} giờ trước`;
  const days = Math.round(hours / 24);
  if (days === 1) return 'hôm qua';
  if (days < 14) return `${days} ngày trước`;
  return null;
}

function daysBetween(a: string, b: string): number | null {
  const ta = Date.parse(a.slice(0, 10) + 'T12:00:00');
  const tb = Date.parse(b.slice(0, 10) + 'T12:00:00');
  if (!Number.isFinite(ta) || !Number.isFinite(tb)) return null;
  return Math.round((tb - ta) / 86400000);
}

function isAwaitingParentCheck(c: DayFlowCommitment): boolean {
  const st = (c.status || '').toLowerCase();
  if (st === 'awaiting_check' || st === 'waiting_check') return true;
  if ((c.evidenceSubmitted || Boolean(c.evidenceUrl)) && !c.evidenceSatisfied) return true;
  if (
    c.commitmentKind === 'study_focus' &&
    st === 'done' &&
    c.evidenceSatisfied === false &&
    !c.starPosted
  ) {
    return true;
  }
  if (st === 'done' && !c.starPosted && (c.starDelta ?? c.projectedStarDelta ?? 0) > 0) return true;
  return false;
}

/**
 * One warm story for open — real data only, no LLM.
 * Priority: child thanks → memory win → weekly story → soft fallback.
 */
export function buildWarmthPulse(input: {
  role: 'parent' | 'child';
  flowDate: string;
  memberId: string;
  voiceThanks?: ParentVoiceMessage[];
  unreadParentVoice?: ParentVoiceMessage | null;
  memories?: FamilyMemoryEntry[];
  weeklyStory?: WeeklyStory | null;
  familyName?: string;
  unreadKidMoments?: FamilyMemoryEntry[];
}): WarmthPulse | null {
  if (isWarmthDismissed(input.memberId, input.flowDate)) return null;

  if (input.role === 'parent') {
    const moment = (input.unreadKidMoments ?? [])[0];
    if (moment) {
      const who = shortMemberName(moment.memberName || 'Con');
      const audio = (moment.icon === '🎤' || moment.icon === 'mic') ||
        /\.(webm|m4a|mp3|ogg|aac|wav)(\?|$)/i.test(moment.photoUrl || '');
      return {
        id: `warm-moment-${moment.id}`,
        icon: audio ? '🎤' : '📷',
        eyebrowVi: 'Nhà cần bạn',
        titleVi: `${who} vừa gửi một khoảnh khắc`,
        bodyVi: moment.noteVi?.trim()
          ? `"${moment.noteVi.trim().slice(0, 120)}${moment.noteVi.length > 120 ? '…' : ''}"`
          : audio
            ? 'Một câu nói nhỏ — bố/mẹ nhìn thấy là đủ ấm.'
            : 'Một ảnh nhỏ — không phải bài tập, chỉ muốn nhà thấy.',
        ctaLabelVi: 'Nhìn thấy con',
        cta: 'open_kid_moment',
        ctaId: moment.id,
      };
    }

    const thanks = (input.voiceThanks ?? []).find((v) => !v.ackAt);
    if (thanks) {
      const child = shortMemberName(thanks.toMemberName || 'Con');
      return {
        id: `warm-thanks-${thanks.id}`,
        icon: '💛',
        eyebrowVi: 'Nhà mình đang ấm',
        titleVi: `${child} vừa cảm ơn lời của bạn`,
        bodyVi: `“${thanks.bodyVi.trim().slice(0, 140)}${thanks.bodyVi.length > 140 ? '…' : ''}”`,
        ctaLabelVi: 'Đã thấy',
        cta: 'dismiss_thanks',
        ctaId: thanks.id,
      };
    }
  }

  if (input.role === 'child' && input.unreadParentVoice) {
    const v = input.unreadParentVoice;
    const from = shortMemberName(v.fromMemberName || 'Bố mẹ');
    return {
      id: `warm-voice-${v.id}`,
      icon: '🧡',
      eyebrowVi: 'Có lời ấm đang chờ',
      titleVi: `${from} gửi lời cho bạn`,
      bodyVi: `“${v.bodyVi.trim().slice(0, 140)}${v.bodyVi.length > 140 ? '…' : ''}”`,
      ctaLabelVi: 'Đọc & trả lời',
      cta: 'ack_parent_voice',
      ctaId: v.id,
    };
  }

  const memoryWin = pickMemoryWinVi(input.memories ?? [], input.flowDate);
  if (memoryWin) {
    return {
      id: `warm-mem-${input.flowDate}`,
      icon: '✨',
      eyebrowVi: 'Khoảnh khắc đáng nhớ',
      titleVi: memoryWin,
      bodyVi: 'Famixa giữ lại để cả nhà nhớ — mở nhật ký khi muốn xem thêm.',
      ctaLabelVi: 'Xem nhật ký',
      cta: 'open_memory',
    };
  }

  const line = input.weeklyStory?.lines?.[0];
  if (line?.textVi) {
    return {
      id: `warm-week-${input.weeklyStory!.from}`,
      icon: line.icon || '📖',
      eyebrowVi: input.weeklyStory!.headlineVi || 'Câu chuyện tuần',
      titleVi: line.textVi,
      bodyVi: 'Một sợi nhớ nhỏ — nhà mình đang gắn kết từng ngày.',
      ctaLabelVi: input.role === 'parent' ? 'Gửi lời ấm' : 'Giữ kế hoạch',
      cta: input.role === 'parent' ? 'open_voice' : 'scroll_missions',
    };
  }

  const house = (input.familyName || 'Nhà mình').trim() || 'Nhà mình';
  return {
    id: `warm-fallback-${input.flowDate}`,
    icon: '🌱',
    eyebrowVi: 'Famixa chào cả nhà',
    titleVi:
      input.role === 'parent'
        ? `Hôm nay ${house} có thể ấm hơn chỉ với một lời nhìn thấy con.`
        : `Hôm nay nhà đang chờ thấy bạn — từng việc nhỏ cũng là yêu thương.`,
    bodyVi:
      input.role === 'parent'
        ? 'Không cần làm hết mọi thứ. Một lời khen đúng lúc đã đủ để vòng yêu thương chạy.'
        : 'Bố mẹ sẽ thấy khi bạn cố gắng. Làm nhẹ nhàng, rồi về nhà trong app nhé.',
    ctaLabelVi: input.role === 'parent' ? 'Xem việc chờ mình' : 'Xem việc hôm nay',
    cta: input.role === 'parent' ? 'verify_evidence' : 'scroll_missions',
  };
}

/** Role-gated actions only this person can unlock. */
export function buildPendingActions(input: {
  role: 'parent' | 'child';
  commitments: DayFlowCommitment[];
  partnerInbox?: ParentVoiceMessage[];
  unreadParentVoice?: ParentVoiceMessage | null;
  unreadKidMoments?: FamilyMemoryEntry[];
  max?: number;
}): PendingAction[] {
  const max = input.max ?? 3;
  const out: PendingAction[] = [];

  if (input.role === 'parent') {
    for (const m of input.unreadKidMoments ?? []) {
      const who = shortMemberName(m.memberName || 'Con');
      out.push({
        id: `pend-moment-${m.id}`,
        icon: '💙',
        titleVi: `Nhà cần bạn nhìn khoảnh khắc của ${who}`,
        detailVi: m.noteVi?.trim() || m.titleVi,
        ctaLabelVi: 'Nhìn thấy',
        cta: 'open_kid_moment',
        ctaId: m.id,
        priority: 0,
      });
    }

    for (const c of input.commitments) {
      if (!isAwaitingParentCheck(c)) continue;
      const who = shortMemberName(c.memberName || 'Con');
      const needsVerify = Boolean(c.evidenceSubmitted) && !c.evidenceSatisfied;
      out.push({
        id: `pend-ev-${c.id}`,
        icon: needsVerify ? '📷' : '⭐',
        titleVi: needsVerify
          ? `Chỉ bố/mẹ xác nhận được · ${who}`
          : `Chỉ bố/mẹ duyệt sao · ${who}`,
        detailVi: c.title,
        ctaLabelVi: needsVerify ? 'Xác nhận bằng chứng' : 'Duyệt sao',
        cta: needsVerify ? 'verify_evidence' : 'approve_stars',
        ctaId: c.id,
        priority: needsVerify ? 0 : 1,
      });
    }
    for (const n of input.partnerInbox ?? []) {
      out.push({
        id: `pend-partner-${n.id}`,
        icon: '💬',
        titleVi: `Lời từ ${shortMemberName(n.fromMemberName || 'bố/mẹ')} — cần bạn đọc`,
        detailVi: n.bodyVi.slice(0, 100),
        ctaLabelVi: 'Đã đọc',
        cta: 'ack_partner_voice',
        ctaId: n.id,
        priority: 2,
      });
    }
    if (out.length === 0) {
      out.push({
        id: 'pend-voice-soft',
        icon: '🧡',
        titleVi: 'Chỉ bạn gửi được lời ấm tới con',
        detailVi: 'Một câu “mẹ/bố thấy rồi” đủ để con mở lại vòng động lực.',
        ctaLabelVi: 'Gửi lời ấm',
        cta: 'open_voice',
        priority: 9,
      });
    }
  } else {
    if (input.unreadParentVoice) {
      const v = input.unreadParentVoice;
      out.push({
        id: `pend-voice-${v.id}`,
        icon: '💛',
        titleVi: 'Có lời bố/mẹ — chỉ bạn trả lời được',
        detailVi: v.bodyVi.slice(0, 100),
        ctaLabelVi: 'Trả lời lời ấm',
        cta: 'ack_parent_voice',
        ctaId: v.id,
        priority: 0,
      });
    }
    const open = input.commitments.filter((c) => {
      const st = (c.status || '').toLowerCase();
      return st === 'pending' || st === 'in_progress' || st === 'due' || st === 'overdue';
    });
    for (const c of open.slice(0, 2)) {
      const study = c.commitmentKind === 'study_focus';
      out.push({
        id: `pend-task-${c.id}`,
        icon: study ? '📚' : '✅',
        titleVi: study ? 'Cam kết học đang chờ bạn' : 'Việc nhà đang chờ bạn',
        detailVi: c.title,
        ctaLabelVi: study ? 'Nộp bằng chứng' : 'Làm việc',
        cta: 'scroll_missions',
        ctaId: c.id,
        priority: study ? 1 : 2,
      });
    }
  }

  return out.sort((a, b) => a.priority - b.priority).slice(0, max);
}

/** Soft presence — “được nhìn thấy” with live-ish timestamps. */
export function buildSeenSignals(input: {
  role: 'parent' | 'child';
  voiceThanks?: ParentVoiceMessage[];
  partnerInbox?: ParentVoiceMessage[];
  unreadParentVoice?: ParentVoiceMessage | null;
  awaitingCount?: number;
  commitments?: DayFlowCommitment[];
  kidMoments?: FamilyMemoryEntry[];
  now?: Date;
}): SeenSignal[] {
  const out: SeenSignal[] = [];
  const now = input.now ?? new Date();
  const commitments = input.commitments ?? [];

  if (input.role === 'parent') {
    const km = (input.kidMoments ?? [])
      .slice()
      .sort((a, b) => String(b.happenedAt).localeCompare(String(a.happenedAt)))[0];
    if (km) {
      const rel = formatRelativeVi(km.happenedAt, now);
      const who = shortMemberName(km.memberName || 'Con');
      out.push({
        id: `seen-moment-${km.id}`,
        icon: '📷',
        textVi: rel
          ? `${who} gửi khoảnh khắc ${rel} — nhà cần bạn nhìn thấy`
          : `${who} vừa gửi khoảnh khắc — nhà cần bạn nhìn thấy`,
      });
    }

    const waiting = commitments
      .filter((c) => (c.evidenceSubmitted || Boolean(c.evidenceUrl)) && !c.evidenceSatisfied)
      .map((c) => ({
        c,
        at: c.completedAt || c.startedAt || '',
      }))
      .filter((x) => x.at)
      .sort((a, b) => b.at.localeCompare(a.at));
    if (waiting[0]) {
      const rel = formatRelativeVi(waiting[0].at, now);
      const who = shortMemberName(waiting[0].c.memberName || 'Con');
      out.push({
        id: `seen-soft-ev-${waiting[0].c.id}`,
        icon: '📡',
        textVi: rel
          ? `${who} vừa nộp bằng chứng · ${rel}`
          : `${who} vừa nộp bằng chứng — nhà đang chờ bạn nhìn thấy`,
      });
    }

    const verified = commitments
      .filter((c) => Boolean(c.evidenceSatisfiedAt))
      .sort((a, b) =>
        String(b.evidenceSatisfiedAt).localeCompare(String(a.evidenceSatisfiedAt)),
      );
    if (verified[0]?.evidenceSatisfiedAt) {
      const rel = formatRelativeVi(verified[0].evidenceSatisfiedAt, now);
      const who = shortMemberName(verified[0].memberName || 'Con');
      if (rel) {
        out.push({
          id: `seen-soft-ok-${verified[0].id}`,
          icon: '✅',
          textVi: `Bạn đã xác nhận cho ${who} · ${rel}`,
        });
      }
    }

    for (const t of (input.voiceThanks ?? []).slice(0, 2)) {
      const rel = formatRelativeVi(t.ackAt || t.sentAt, now);
      out.push({
        id: `seen-thanks-${t.id}`,
        icon: '👀',
        textVi: rel
          ? `${shortMemberName(t.toMemberName || 'Con')} đã thấy lời của bạn · ${rel}`
          : `${shortMemberName(t.toMemberName || 'Con')} đã thấy lời của bạn`,
      });
    }

    if ((input.awaitingCount ?? 0) > 0 && !waiting[0]) {
      out.push({
        id: 'seen-await',
        icon: '🕊️',
        textVi: `${input.awaitingCount} việc con đã nộp — đang chờ bạn nhìn thấy`,
      });
    }
  } else {
    if (input.unreadParentVoice) {
      const v = input.unreadParentVoice;
      const rel = formatRelativeVi(v.sentAt, now);
      out.push({
        id: `seen-voice-${v.id}`,
        icon: '👀',
        textVi: rel
          ? `${shortMemberName(v.fromMemberName || 'Bố mẹ')} đang nhìn về phía bạn · ${rel}`
          : `${shortMemberName(v.fromMemberName || 'Bố mẹ')} đang nhìn về phía bạn`,
      });
    }

    const satisfied = commitments
      .filter((c) => Boolean(c.evidenceSatisfiedAt))
      .sort((a, b) =>
        String(b.evidenceSatisfiedAt).localeCompare(String(a.evidenceSatisfiedAt)),
      );
    if (satisfied[0]?.evidenceSatisfiedAt) {
      const rel = formatRelativeVi(satisfied[0].evidenceSatisfiedAt, now);
      if (rel) {
        out.push({
          id: `seen-parent-ok-${satisfied[0].id}`,
          icon: '💛',
          textVi: `Bố/mẹ đã xác nhận bằng chứng · ${rel}`,
        });
      }
    }

    const streak = commitments
      .filter((c) => (c.habitStreakDays ?? 0) >= 2)
      .sort((a, b) => (b.habitStreakDays ?? 0) - (a.habitStreakDays ?? 0))[0];
    if (streak) {
      out.push({
        id: `seen-streak-${streak.id}`,
        icon: '🔥',
        textVi: `Nhà thấy streak ${streak.habitStreakDays} ngày — bạn đang được nhìn thấy`,
      });
    }

    if (out.length === 0) {
      out.push({
        id: 'seen-soft',
        icon: '🏠',
        textVi: 'Nhà mình vẫn ở đây — kể cả khi bạn chưa làm hết việc',
      });
    }
  }

  return out.slice(0, 3);
}

/**
 * Memory Yarn — one contextual “sợi nhớ” when evidence/streak/verify is in play.
 * Uses past memories only (not today) so it feels like remembering, not listing today.
 */
export function buildMemoryYarn(input: {
  role: 'parent' | 'child';
  flowDate: string;
  memberId: string;
  commitments: DayFlowCommitment[];
  memories: FamilyMemoryEntry[];
}): MemoryYarn | null {
  if (isYarnDismissed(input.memberId, input.flowDate)) return null;

  const commitments = input.commitments;
  const past = (input.memories ?? [])
    .filter((m) => m.flowDate && m.flowDate < input.flowDate && m.titleVi?.trim())
    .slice()
    .sort((a, b) => b.flowDate.localeCompare(a.flowDate) || b.happenedAt.localeCompare(a.happenedAt));

  if (past.length === 0) return null;

  let triggerMemberId: string | undefined;
  let triggerReason: 'evidence' | 'verify' | 'streak' | 'study' | null = null;

  if (input.role === 'parent') {
    const waiting = commitments.find(
      (c) => (c.evidenceSubmitted || Boolean(c.evidenceUrl)) && !c.evidenceSatisfied,
    );
    if (waiting) {
      triggerMemberId = waiting.memberId;
      triggerReason = 'evidence';
    } else {
      const verified = commitments.find((c) => Boolean(c.evidenceSatisfiedAt));
      if (verified) {
        triggerMemberId = verified.memberId;
        triggerReason = 'verify';
      } else {
        const streak = commitments.find((c) => (c.habitStreakDays ?? 0) >= 2);
        if (streak) {
          triggerMemberId = streak.memberId;
          triggerReason = 'streak';
        }
      }
    }
  } else {
    const submitted = commitments.find(
      (c) =>
        (c.evidenceSubmitted || Boolean(c.evidenceUrl)) &&
        (c.commitmentKind === 'study_focus' || Boolean(c.evidenceUrl)),
    );
    const studyOpen = commitments.find((c) => {
      const st = (c.status || '').toLowerCase();
      return (
        c.commitmentKind === 'study_focus' &&
        (st === 'pending' || st === 'in_progress' || st === 'due' || st === 'overdue')
      );
    });
    const streak = commitments.find((c) => (c.habitStreakDays ?? 0) >= 2);
    if (submitted) {
      triggerMemberId = submitted.memberId || input.memberId;
      triggerReason = 'evidence';
    } else if (streak) {
      triggerMemberId = streak.memberId || input.memberId;
      triggerReason = 'streak';
    } else if (studyOpen) {
      triggerMemberId = studyOpen.memberId || input.memberId;
      triggerReason = 'study';
    }
  }

  if (!triggerReason) return null;

  const preferKinds = new Set(['streak_milestone', 'first_time', 'beautiful_day', 'celebration']);
  const forMember = triggerMemberId
    ? past.filter((m) => !m.memberId || m.memberId === triggerMemberId)
    : past;
  const pool = forMember.length ? forMember : past;
  const hit =
    pool.find((m) => preferKinds.has(m.kind)) ||
    pool.find((m) => /học|toán|tiếng|bài|cam kết/i.test(m.titleVi + (m.noteVi || ''))) ||
    pool[0];
  if (!hit) return null;

  const gap = daysBetween(hit.flowDate, input.flowDate);
  const when =
    gap == null
      ? 'Trước đây'
      : gap <= 1
        ? 'Hôm qua'
        : gap < 7
          ? `${gap} ngày trước`
          : gap < 14
            ? 'Tuần trước'
            : `${Math.round(gap / 7)} tuần trước`;

  const who = shortMemberName(hit.memberName || (input.role === 'parent' ? 'Con' : 'Bạn'));
  const eyebrow =
    triggerReason === 'evidence' || triggerReason === 'study'
      ? 'Sợi nhớ đúng lúc'
      : triggerReason === 'verify'
        ? 'Nhà nhớ khoảnh khắc này'
        : 'Sợi nhớ streak';

  return {
    id: `yarn-${hit.id}`,
    icon: hit.icon || '🧵',
    eyebrowVi: eyebrow,
    textVi: `${when} ${who.toLowerCase() === 'bạn' ? 'bạn' : who} cũng đã từng: ${hit.titleVi}`,
    ctaLabelVi: 'Xem nhật ký',
    relatedMemberName: hit.memberName,
  };
}