import type { ParentPulse } from '@/shared/value/parent-pulse';
import type { ParentCoachTip } from '@/shared/value/resolve-parenting-coach';

export type HomeBriefPeriod = 'morning' | 'evening';

export type HomeBriefActionKind = 'attention' | 'coach' | 'evening_checkin';

export type HomeBriefAttentionKind = 'awaiting' | 'overdue' | 'consequence';

export type HomeBriefAction = {
  kind: HomeBriefActionKind;
  titleVi: string;
  reasonVi: string;
  doThisVi: string;
  /** When kind=attention — drive CTA behavior. */
  attentionKind?: HomeBriefAttentionKind;
  attentionId?: string;
};

export type HomeBrief = {
  period: HomeBriefPeriod;
  eyebrowVi: string;
  moodLineVi: string;
  bulletsVi: string[];
  primaryAction: HomeBriefAction;
  eveningCheckinHintVi?: string;
  /** Evening: count of morning overdue tasks collapsed in priority list. */
  eveningOverdueCount?: number;
};

export type HomeBriefAttentionLite = {
  kind: HomeBriefAttentionKind;
  id: string;
  titleVi: string;
  detailVi?: string;
};

function localHour(localTime?: string | null, fallback = new Date()): number {
  if (localTime && /^\d{1,2}:\d{2}/.test(localTime)) {
    return Number(localTime.split(':')[0]) || fallback.getHours();
  }
  return fallback.getHours();
}

/**
 * P0.5 Home Brief — morning: Attention first; evening: 3Q / khoảnh khắc trước việc sáng còn mở.
 */
export function buildHomeBrief(input: {
  pulse: ParentPulse;
  coach: ParentCoachTip;
  attentionCount: number;
  overdueCount?: number;
  topAttention?: HomeBriefAttentionLite | null;
  localTime?: string | null;
  eveningCheckinDone?: boolean;
  /** Today memory win line, e.g. "Lần đầu: tự học". */
  memoryWinVi?: string | null;
}): HomeBrief {
  const hour = localHour(input.localTime);
  const period: HomeBriefPeriod = hour >= 17 ? 'evening' : 'morning';
  const { pulse, coach } = input;
  const overdueCount = input.overdueCount ?? 0;

  let bullets = [pulse.nudgeLineVi, pulse.autonomyLineVi, pulse.peaceLineVi]
    .map((b) => b.trim())
    .filter(Boolean)
    .slice(0, 3);

  let moodLineVi =
    period === 'morning'
      ? pulse.dayMoodVi || pulse.headlineVi
      : pulse.insightVi?.trim() ||
        pulse.dayMoodVi ||
        'Hôm nay nhà đã có nhịp — Famixa ghi nhận giúp bạn.';

  let primaryAction: HomeBriefAction;

  if (period === 'evening' && !input.eveningCheckinDone) {
    // Evening priority: 3Q trước việc sáng còn mở.
    if (overdueCount > 0) {
      moodLineVi =
        overdueCount === 1
          ? 'Còn 1 việc ban ngày mở — tối nay ưu tiên 3 câu phản hồi.'
          : `Còn ${overdueCount} việc ban ngày mở — tối nay ưu tiên 3 câu phản hồi.`;
    }
    primaryAction = {
      kind: 'evening_checkin',
      titleVi: '3 câu tối',
      doThisVi: 'Trả lời 3 câu phản hồi nhanh',
      reasonVi: 'Giúp Famixa học nhịp nhà — chỉ mất khoảng 20 giây.',
    };
  } else if (period === 'evening' && input.memoryWinVi?.trim()) {
    const win = input.memoryWinVi.trim();
    moodLineVi = `Hôm nay có khoảnh khắc đáng nhớ — ${win}`;
    primaryAction = {
      kind: 'coach',
      titleVi: 'Khoảnh khắc',
      doThisVi: 'Xem / lưu khoảnh khắc gia đình',
      reasonVi: win,
    };
  } else if (input.attentionCount > 0 && input.topAttention) {
    const a = input.topAttention;
    const verb =
      a.kind === 'awaiting'
        ? 'Xác nhận'
        : a.kind === 'overdue'
          ? 'Xử lý'
          : 'Quyết định';
    moodLineVi =
      input.attentionCount === 1
        ? `Có 1 việc cần bạn ${verb.toLowerCase()} trước.`
        : `Có ${input.attentionCount} việc cần bạn xử lý trước.`;
    primaryAction = {
      kind: 'attention',
      attentionKind: a.kind,
      attentionId: a.id,
      titleVi: verb,
      doThisVi: a.titleVi,
      reasonVi:
        a.detailVi ||
        'Famixa xếp việc nóng lên trước tip Coach — xong rồi Brief sẽ gợi ý nhẹ tay hơn.',
    };
  } else {
    primaryAction = {
      kind: 'coach',
      titleVi:
        period === 'morning'
          ? coach.titleVi || 'Gợi ý sáng'
          : coach.titleVi || 'Gợi ý tối',
      reasonVi: coach.insight,
      doThisVi: coach.doThis,
    };
  }

  const win = input.memoryWinVi?.trim();
  if (win && primaryAction.kind !== 'attention' && primaryAction.kind !== 'evening_checkin') {
    bullets = [win, ...bullets.filter((b) => b !== win)].slice(0, 3);
  } else if (win && primaryAction.kind === 'evening_checkin') {
    bullets = [win, ...bullets.filter((b) => b !== win)].slice(0, 3);
  }

  let eveningCheckinHintVi: string | undefined;
  if (
    period === 'evening' &&
    !input.eveningCheckinDone &&
    primaryAction.kind !== 'evening_checkin'
  ) {
    eveningCheckinHintVi =
      'Tối nay: 3 câu phản hồi nhanh — giúp Famixa học nhịp nhà.';
  }

  return {
    period,
    eyebrowVi: period === 'morning' ? 'Famixa · Brief sáng' : 'Famixa · Brief tối',
    moodLineVi,
    bulletsVi: bullets,
    primaryAction,
    eveningCheckinHintVi,
    eveningOverdueCount: period === 'evening' ? overdueCount : undefined,
  };
}
