import type { ReactNode } from 'react';
import type {
  DayFlowCommitment,
  FamilyMemoryEntry,
  FamilyMemberMood,
  FamilyRitual,
  ChildGratitude,
  TeamUnlock,
} from '@/shared/api/family-os.api';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';
import { isKidMomentAudio } from '@/modules/flow/kidMomentAck';
import { shortMemberName } from '@/modules/flow/relationshipGraph';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';

export type ParentHomeAttention =
  | {
      kind: 'awaiting' | 'overdue' | 'due_soon';
      id: string;
      item: DayFlowCommitment;
    }
  | {
      kind: 'consequence';
      id: string;
      event: { labelVi: string; memberName?: string | null };
    };

type Props = {
  parentHelloLabel: string;
  period: 'morning' | 'evening' | string;
  moodLineVi?: string;
  houseTeamPercent: number;
  houseTeamRemaining: number;
  houseTeamTotal: number;
  houseTeamSummary: string;
  attentionItems: ParentHomeAttention[];
  unreadKidMoments: FamilyMemoryEntry[];
  savedMemories: FamilyMemoryEntry[];
  childGratitudes: ChildGratitude[];
  familyMoods: FamilyMemberMood[];
  rituals: FamilyRitual[];
  todayUnlock: TeamUnlock | null;
  coachInsight?: string | null;
  coachDoThis?: string | null;
  /** Label for current child focus (Cả nhà / short name). */
  focusLabel?: string;
  /** True when parent focuses whole house — diversify AI cards across kids. */
  focusAll?: boolean;
  /** Per-child plan progress — used in “Cả nhà” metrics card. */
  childProgress?: Array<{
    id: string;
    name: string;
    done: number;
    total: number;
    percent: number;
  }>;
  onOpenPlan: () => void;
  onOpenTasks: () => void;
  onOpenVoice: (opts?: {
    toMemberId?: string;
    intent?: 'encourage' | 'praise' | 'soft' | 'moment';
    taskTitle?: string;
  }) => void;
  onOpenKidMoment: (id: string) => void;
  onOpenVerify: (item: DayFlowCommitment) => void;
  onOpenDiary: () => void;
  onOpenChallenge: () => void;
  onOpenRewards: () => void;
  onOpenValue: () => void;
  onOpenCoach: () => void;
};

type AiBadge = 'heart' | 'smile' | 'check' | 'eye' | 'clip' | 'wave';

type AiCard = {
  id: string;
  tone: 'rose' | 'amber' | 'teal';
  avatar: string;
  badge: AiBadge;
  title: string;
  body: string;
  cta: string;
  onClick: () => void;
};

function needYouLabel(parentHelloLabel: string): string {
  const n = parentHelloLabel.trim().toLowerCase();
  if (n === 'mẹ' || n.endsWith(' mẹ')) return 'AI cần mẹ ơi';
  if (n === 'bố' || n === 'ba' || n.endsWith(' bố') || n.endsWith(' ba')) return 'AI cần bố ơi';
  return 'AI cần bố mẹ ơi';
}

function parentRoleWord(parentHelloLabel: string): string {
  const n = parentHelloLabel.trim().toLowerCase();
  if (n === 'mẹ' || n.endsWith(' mẹ')) return 'mẹ';
  if (n === 'bố' || n === 'ba' || n.endsWith(' bố') || n.endsWith(' ba')) return 'bố';
  return 'bố mẹ';
}

function memberKey(id?: string | null, name?: string | null): string {
  if (id) return id;
  const n = name?.trim();
  return n || 'house';
}

function childFace(name: string): string {
  return avatarEmoji(inferGenderFromName(name), 'child');
}

function badgeGlyph(badge: AiBadge): string {
  switch (badge) {
    case 'heart':
      return '♥';
    case 'smile':
      return '☺';
    case 'check':
      return '✓';
    case 'eye':
      return '👁';
    case 'clip':
      return '✓';
    case 'wave':
      return '✦';
  }
}

function buildAiCards(input: {
  attentionItems: ParentHomeAttention[];
  unreadKidMoments: FamilyMemoryEntry[];
  houseTeamRemaining: number;
  focusAll: boolean;
  parentRole: string;
  onOpenVoice: (opts?: {
    toMemberId?: string;
    intent?: 'encourage' | 'praise' | 'soft' | 'moment';
    taskTitle?: string;
  }) => void;
  onOpenKidMoment: (id: string) => void;
  onOpenVerify: (item: DayFlowCommitment) => void;
  onOpenTasks: () => void;
}): AiCard[] {
  const cards: AiCard[] = [];
  const usedMembers = new Set<string>();
  const parent = input.parentRole;
  const max = 3;

  const takeMember = (key: string) => {
    if (!input.focusAll) return true;
    if (usedMembers.has(key)) return false;
    usedMembers.add(key);
    return true;
  };

  for (const m of input.unreadKidMoments) {
    if (cards.length >= max) break;
    const who = shortMemberName(m.memberName || 'Con');
    const key = memberKey(m.memberId, m.memberName);
    if (!takeMember(key)) continue;
    const audio = isKidMomentAudio(m);
    cards.push({
      id: `moment-${m.id}`,
      tone: 'rose',
      avatar: childFace(m.memberName || who),
      badge: 'eye',
      title: `${who} vừa gửi ${audio ? 'giọng nói' : 'khoảnh khắc'}`,
      body: `Một lời nhìn thấy từ ${parent} cũng đủ làm ${who} ấm lên.`,
      cta: 'Nhìn thấy',
      onClick: () => input.onOpenKidMoment(m.id),
    });
  }

  const awaiting = input.attentionItems.filter((a) => a.kind === 'awaiting');
  const due = input.attentionItems.filter(
    (a) => a.kind === 'overdue' || a.kind === 'due_soon',
  );
  const cons = input.attentionItems.filter((a) => a.kind === 'consequence');

  for (const a of awaiting) {
    if (cards.length >= max) break;
    if (a.kind !== 'awaiting') continue;
    const who = shortMemberName(a.item.memberName || 'Con');
    const key = memberKey(a.item.memberId, a.item.memberName);
    if (!takeMember(key)) continue;
    const needsEvidence =
      a.item.commitmentKind === 'study_focus' && a.item.evidenceSatisfied === false;
    cards.push({
      id: `attn-${a.id}`,
      tone: needsEvidence ? 'teal' : 'rose',
      avatar: childFace(a.item.memberName || who),
      badge: needsEvidence ? 'check' : 'heart',
      title: needsEvidence
        ? `${who} chờ ${parent} xác nhận`
        : `${who} đang chờ một lời khen từ ${parent}`,
      body: needsEvidence
        ? `${who} đã gửi bằng chứng cho «${a.item.title}».`
        : `${who} vừa xong «${a.item.title}» — một lời khen sẽ rất ý nghĩa.`,
      cta: needsEvidence ? 'Xác nhận' : 'Khen ngay',
      onClick: () =>
        needsEvidence
          ? input.onOpenVerify(a.item)
          : input.onOpenVoice({
              toMemberId: a.item.memberId,
              intent: 'praise',
              taskTitle: a.item.title,
            }),
    });
  }

  for (const a of due) {
    if (cards.length >= max) break;
    if (a.kind === 'consequence') continue;
    const who = shortMemberName(a.item.memberName || 'Con');
    const key = memberKey(a.item.memberId, a.item.memberName);
    if (!takeMember(key)) continue;
    cards.push({
      id: `due-${a.id}`,
      tone: 'amber',
      avatar: childFace(a.item.memberName || who),
      badge: 'smile',
      title: `${who} hơi mất động lực`,
      body: `AI gợi ý ${parent} dành 5 phút trò chuyện cùng ${who} về «${a.item.title}».`,
      cta: 'Trò chuyện',
      onClick: () =>
        input.onOpenVoice({
          toMemberId: a.item.memberId,
          intent: 'encourage',
          taskTitle: a.item.title,
        }),
    });
  }

  for (const a of cons) {
    if (cards.length >= max) break;
    if (a.kind !== 'consequence') continue;
    const who = shortMemberName(a.event.memberName || 'Con');
    const key = memberKey(null, a.event.memberName);
    if (!takeMember(key)) continue;
    cards.push({
      id: `cons-${a.id}`,
      tone: 'amber',
      avatar: childFace(a.event.memberName || who),
      badge: 'wave',
      title: `${who} cần ${parent} quyết định nhẹ`,
      body: a.event.labelVi,
      cta: 'Xem ngay',
      onClick: () => input.onOpenTasks(),
    });
  }

  if (input.houseTeamRemaining > 0 && cards.length < max) {
    cards.push({
      id: 'plan-left',
      tone: 'teal',
      avatar: '📋',
      badge: 'clip',
      title:
        input.houseTeamRemaining === 1
          ? '1 việc trong kế hoạch chưa hoàn thành'
          : `${input.houseTeamRemaining} việc trong kế hoạch chưa hoàn thành`,
      body: `${parent.charAt(0).toUpperCase()}${parent.slice(1)} xem qua và để Famixa nhắc nhẹ nhé.`,
      cta: 'Xem ngay',
      onClick: () => input.onOpenTasks(),
    });
  }

  if (cards.length === 0) {
    cards.push({
      id: 'soft-voice',
      tone: 'rose',
      avatar: '💛',
      badge: 'heart',
      title: 'Gửi một lời ấm hôm nay',
      body: 'Không việc nóng — một lời khen ngắn vẫn làm nhà ấm hơn.',
      cta: 'Gửi lời ấm',
      onClick: () => input.onOpenVoice({ intent: 'soft' }),
    });
  }

  return cards.slice(0, max);
}

function softenHomeCoachInsight(input: {
  raw: string;
  priorityTitle?: string;
  who?: string;
  remaining?: number;
}): string {
  const raw = input.raw.trim();
  const task = input.priorityTitle?.trim();
  const who = input.who?.trim();
  const whoBit = who ? ` với ${who}` : '';

  if (!raw) {
    return task
      ? `Hôm nay chỉ cần một bước nhẹ${whoBit}: «${task}».`
      : 'Tuần này nhà mình đang kết nối tốt. Giữ một nhịp nhỏ là đủ.';
  }

  if (/quá giờ|chưa xong|overdue/i.test(raw)) {
    return task
      ? `Nhà đang cần một nhịp nhẹ${whoBit} — ưu tiên «${task}», không cần làm hết ngay.`
      : 'Nhà đang hơi chùng — chọn một việc nhỏ thay vì đua cả list.';
  }

  if (/^\s*cả nhà có \d+ việc/i.test(raw) && task) {
    return `Nhà đang cần một nhịp nhẹ${whoBit} — ưu tiên «${task}», không cần làm hết ngay.`;
  }

  if (input.remaining != null && input.remaining > 0 && task && raw.length > 120) {
    return `Ưu tiên nhẹ «${task}»${whoBit} — còn ${input.remaining} việc, làm từng bước thôi.`;
  }

  return raw;
}

function buildHomeCoachChip(input: {
  attentionItems: ParentHomeAttention[];
  unreadKidMoments: FamilyMemoryEntry[];
  parentRole: string;
  onOpenVoice: Props['onOpenVoice'];
  onOpenKidMoment: Props['onOpenKidMoment'];
  onOpenVerify: Props['onOpenVerify'];
  onOpenTasks: Props['onOpenTasks'];
}): { label: string; onClick: () => void } | null {
  const moment = input.unreadKidMoments[0];
  if (moment) {
    const who = shortMemberName(moment.memberName || 'Con');
    return {
      label: `Nhìn thấy · ${who}`,
      onClick: () => input.onOpenKidMoment(moment.id),
    };
  }

  for (const a of input.attentionItems) {
    if (a.kind === 'awaiting') {
      const who = shortMemberName(a.item.memberName || 'Con');
      const needsEvidence =
        a.item.commitmentKind === 'study_focus' && a.item.evidenceSatisfied === false;
      if (needsEvidence) {
        return {
          label: `Xác nhận · ${who}`,
          onClick: () => input.onOpenVerify(a.item),
        };
      }
      return {
        label: `Khen ngay · ${who}`,
        onClick: () =>
          input.onOpenVoice({
            toMemberId: a.item.memberId,
            intent: 'praise',
            taskTitle: a.item.title,
          }),
      };
    }
  }

  for (const a of input.attentionItems) {
    if (a.kind === 'overdue' || a.kind === 'due_soon') {
      const who = shortMemberName(a.item.memberName || 'Con');
      return {
        label: `Gửi lời ấm · ${who}`,
        onClick: () =>
          input.onOpenVoice({
            toMemberId: a.item.memberId,
            intent: 'encourage',
            taskTitle: a.item.title,
          }),
      };
    }
  }

  for (const a of input.attentionItems) {
    if (a.kind === 'consequence') {
      return {
        label: 'Xem quyết định nhẹ',
        onClick: () => input.onOpenTasks(),
      };
    }
  }

  const role =
    input.parentRole === 'mẹ' || input.parentRole === 'bố' ? input.parentRole : 'nhà';
  return {
    label: `Gửi một lời ấm từ ${role}`,
    onClick: () => input.onOpenVoice({ intent: 'soft' }),
  };
}

export function ParentHomeStoryBody(props: Props) {
  const parentRole = parentRoleWord(props.parentHelloLabel);
  const focusAll = Boolean(props.focusAll ?? props.focusLabel === 'Cả nhà');
  const aiCards = buildAiCards({
    attentionItems: props.attentionItems,
    unreadKidMoments: props.unreadKidMoments,
    houseTeamRemaining: props.houseTeamRemaining,
    focusAll,
    parentRole,
    onOpenVoice: props.onOpenVoice,
    onOpenKidMoment: props.onOpenKidMoment,
    onOpenVerify: props.onOpenVerify,
    onOpenTasks: props.onOpenTasks,
  });

  const doneCount = Math.max(props.houseTeamTotal - props.houseTeamRemaining, 0);
  const statusTone =
    props.houseTeamPercent >= 70 ? 'good' : props.houseTeamPercent >= 40 ? 'ok' : 'care';
  const statusWord =
    statusTone === 'good' ? 'Tốt' : statusTone === 'ok' ? 'Ổn' : 'Cần chăm';
  const moodPositive = props.familyMoods.length > 0;
  const nowLabel = new Date().toLocaleTimeString('vi-VN', {
    hour: '2-digit',
    minute: '2-digit',
  });
  const needLabel = needYouLabel(props.parentHelloLabel);
  const helloRole = props.parentHelloLabel.trim() || 'bố mẹ';
  const parentTitle =
    parentRole === 'mẹ' ? 'Mẹ' : parentRole === 'bố' ? 'Bố' : 'Bố mẹ';
  const allCount = Math.max(props.attentionItems.length, aiCards.length);

  const priorityAttn = props.attentionItems.find(
    (a) => a.kind === 'awaiting' || a.kind === 'overdue' || a.kind === 'due_soon',
  );
  const priorityTitle =
    priorityAttn && priorityAttn.kind !== 'consequence'
      ? priorityAttn.item.title
      : undefined;
  const priorityWho =
    priorityAttn && priorityAttn.kind !== 'consequence'
      ? shortMemberName(priorityAttn.item.memberName || 'Con')
      : props.unreadKidMoments[0]
        ? shortMemberName(props.unreadKidMoments[0].memberName || 'Con')
        : undefined;
  const homeCoachInsight = softenHomeCoachInsight({
    raw: (props.coachInsight || props.moodLineVi || props.coachDoThis || '').trim(),
    priorityTitle,
    who: priorityWho,
    remaining: props.houseTeamRemaining,
  });
  const homeCoachChip = buildHomeCoachChip({
    attentionItems: props.attentionItems,
    unreadKidMoments: props.unreadKidMoments,
    parentRole,
    onOpenVoice: props.onOpenVoice,
    onOpenKidMoment: props.onOpenKidMoment,
    onOpenVerify: props.onOpenVerify,
    onOpenTasks: props.onOpenTasks,
  });

  const momentCards: ReactNode[] = [];
  for (const m of props.unreadKidMoments.slice(0, 3)) {
    const audio = isKidMomentAudio(m);
    const src = m.photoUrl ? withEvidenceAuth(m.photoUrl) : undefined;
    momentCards.push(
      <button
        key={`u-${m.id}`}
        type="button"
        className="phs-moment-card is-new"
        onClick={() => props.onOpenKidMoment(m.id)}
      >
        <span className="phs-moment-label">Mới · {audio ? 'Giọng nói' : 'Ảnh'}</span>
        {audio ? (
          <div className="phs-moment-audio" aria-hidden>
            ▶ ▁▃▅▇▅▃▁
          </div>
        ) : src ? (
          <img src={src} alt="" />
        ) : (
          <span className="phs-moment-ph" aria-hidden>
            📷
          </span>
        )}
        <strong className="phs-moment-title">{m.noteVi || m.titleVi}</strong>
        <em className="phs-moment-meta">{shortMemberName(m.memberName || 'Con')}</em>
      </button>,
    );
  }
  for (const g of props.childGratitudes.slice(0, 2)) {
    momentCards.push(
      <button
        key={`g-${g.id}`}
        type="button"
        className="phs-moment-card is-praise"
        onClick={props.onOpenDiary}
      >
        <span className="phs-moment-label">Lời cảm ơn</span>
        <span className="phs-moment-ph is-quote" aria-hidden>
          “
        </span>
        <strong className="phs-moment-title">{g.messageVi}</strong>
        <em className="phs-moment-meta">{g.fromMemberName || 'Con'}</em>
      </button>,
    );
  }
  for (const m of props.savedMemories
    .filter((x) => x.kind !== 'kid_moment' || !props.unreadKidMoments.some((u) => u.id === x.id))
    .slice(0, 4)) {
    const audio = isKidMomentAudio(m);
    const src = m.photoUrl && !audio ? withEvidenceAuth(m.photoUrl) : undefined;
    momentCards.push(
      <button
        key={m.id}
        type="button"
        className="phs-moment-card"
        onClick={props.onOpenDiary}
      >
        <span className="phs-moment-label">
          {audio ? 'Giọng nói' : m.kind === 'parent_voice' ? 'Lời khen' : 'Kỷ niệm'}
        </span>
        {src ? (
          <img src={src} alt="" />
        ) : (
          <span className="phs-moment-ph" aria-hidden>
            {m.icon || '✨'}
          </span>
        )}
        <strong className="phs-moment-title">{m.titleVi}</strong>
        <em className="phs-moment-meta">{m.noteVi || m.memberName || ''}</em>
      </button>,
    );
  }

  return (
    <div className="phs-stack">
      <section className="phs-hero" aria-label="Chào buổi ngày">
        <div className="phs-hero-copy">
          <p className="phs-hero-hello">
            {props.period === 'evening'
              ? `Chào buổi tối, ${helloRole.toLowerCase()}`
              : `Chào buổi sáng, ${helloRole.toLowerCase()}`}
          </p>
          <h2 className="phs-hero-title">
            {props.period === 'evening' ? (
              <>
                Tối nay nhà mình có thể <span>nhìn thấy nhau</span> chỉ với một lời ấm.
              </>
            ) : (
              <>
                Hôm nay là ngày tốt để <span>yêu thương</span> và{' '}
                <span>kết nối cả nhà</span>.
              </>
            )}
          </h2>
          <div className="phs-hero-status">
            <div className="phs-hero-status-text">
              <p>
                Gia đình đang ở trạng thái{' '}
                <b className={`phs-status-pill is-${statusTone}`}>{statusWord}</b>
              </p>
              <span>
                {props.focusLabel && props.focusLabel !== 'Cả nhà' ? (
                  <>
                    <strong className="phs-focus-inline">{props.focusLabel}</strong>
                    {' · '}
                  </>
                ) : null}
                {props.houseTeamRemaining > 0
                  ? `Còn ${props.houseTeamRemaining} việc trong kế hoạch hôm nay.`
                  : props.houseTeamSummary}
              </span>
            </div>
          </div>
          <button type="button" className="phs-hero-cta" onClick={props.onOpenPlan}>
            Xem kế hoạch hôm nay
          </button>
        </div>
        <div className="phs-hero-art" aria-hidden>
          <img src="/brand/fami-mark-48.png" alt="" className="phs-hero-mark" />
        </div>
      </section>

      <section className="phs-ai" aria-label={needLabel}>
        <header className="phs-sec-head phs-ai-head">
          <div>
            <h3>{needLabel}</h3>
            <p>
              {aiCards.length > 0
                ? `${parentTitle} chỉ cần dành vài phút cho ${Math.min(aiCards.length, 3)} việc này nhé ✨`
                : 'Không việc nóng — vẫn có thể gửi một lời ấm.'}
            </p>
          </div>
          {allCount > 0 ? (
            <button type="button" className="phs-link-pill" onClick={props.onOpenTasks}>
              Xem tất cả ({Math.max(props.attentionItems.length, allCount)})
            </button>
          ) : null}
        </header>
        <div className="phs-hscroll phs-ai-row" role="list">
          {aiCards.map((c) => (
            <article key={c.id} className={`phs-ai-card is-${c.tone}`} role="listitem">
              <div className={`phs-ai-icon is-${c.badge}`} aria-hidden>
                <span className="phs-ai-avatar">{c.avatar}</span>
                <i className="phs-ai-badge">{badgeGlyph(c.badge)}</i>
              </div>
              <strong className="phs-ai-title">{c.title}</strong>
              <p className="phs-ai-body">{c.body}</p>
              <button type="button" className="phs-ai-cta" onClick={c.onClick}>
                {c.cta} <span aria-hidden>›</span>
              </button>
            </article>
          ))}
        </div>
      </section>

      <section className="phs-metrics" aria-label="Gia đình hôm nay">
        <header className="phs-sec-head">
          <div>
            <h3>Gia đình hôm nay</h3>
            <p>Một nhìn nhanh nhịp nhà — bấm để vào chi tiết.</p>
          </div>
        </header>
        <div className="phs-hscroll phs-metrics-grid" role="list">
          <button
            type="button"
            className={`phs-metric${
              focusAll && (props.childProgress?.length ?? 0) > 1 ? ' is-wide' : ''
            }`}
            role="listitem"
            onClick={props.onOpenTasks}
          >
            <div className="phs-metric-visual" aria-hidden>
              <div
                className="phs-ring"
                style={{
                  ['--p' as string]: `${Math.min(100, Math.max(0, props.houseTeamPercent))}%`,
                }}
              >
                <strong>{props.houseTeamPercent}%</strong>
              </div>
            </div>
            <span className="phs-metric-label">Tiến độ kế hoạch</span>
            {focusAll && (props.childProgress?.length ?? 0) > 1 ? (
              <div className="phs-metric-kids">
                {(props.childProgress ?? []).slice(0, 3).map((row) => (
                  <span key={row.id} className="phs-metric-kid-row">
                    <em>{row.name}</em>
                    <i aria-hidden>
                      <b style={{ width: `${Math.min(100, row.percent)}%` }} />
                    </i>
                    <strong>{row.percent}%</strong>
                  </span>
                ))}
                {(props.childProgress?.length ?? 0) > 3 ? (
                  <span className="phs-metric-kid-more">
                    +{(props.childProgress?.length ?? 0) - 3} con nữa
                  </span>
                ) : null}
              </div>
            ) : (
              <em className="phs-metric-desc">
                {doneCount}/{Math.max(props.houseTeamTotal, 0)} việc đã xong
              </em>
            )}
            <span className="phs-metric-go">Chi tiết</span>
          </button>
          <button
            type="button"
            className="phs-metric"
            role="listitem"
            onClick={props.onOpenRewards}
          >
            <span className="phs-metric-visual is-mood" aria-hidden>
              <span className="phs-metric-glyph">😀</span>
            </span>
            <span className="phs-metric-label">Mood gia đình</span>
            <em className="phs-metric-desc">
              {moodPositive
                ? 'Tích cực — cả nhà đang có năng lượng tốt'
                : 'Chưa ghi mood hôm nay'}
            </em>
            <span className="phs-metric-go">Chi tiết</span>
          </button>
          <button
            type="button"
            className="phs-metric"
            role="listitem"
            onClick={props.onOpenChallenge}
          >
            <span className="phs-metric-visual is-trophy" aria-hidden>
              <span className="phs-metric-glyph">🏆</span>
            </span>
            <span className="phs-metric-label">Challenge</span>
            <em className="phs-metric-desc">
              {props.todayUnlock
                ? props.todayUnlock.labelVi || 'Thử thách đang mở'
                : 'Chọn hoạt động gia đình cuối tuần'}
            </em>
            <span className="phs-metric-go">Chi tiết</span>
          </button>
          <button type="button" className="phs-metric" role="listitem" onClick={props.onOpenValue}>
            <span className="phs-metric-visual is-routine" aria-hidden>
              <span className="phs-metric-routine" />
            </span>
            <span className="phs-metric-label">Routine</span>
            <em className="phs-metric-desc">
              {props.rituals[0]
                ? `${props.rituals[0].labelVi}${
                    props.rituals[0].doneThisPeriod ? ' · đã xong' : ' · đang chờ'
                  }`
                : 'Ăn tối cùng nhau · đang chờ'}
            </em>
            <span className="phs-metric-go">Chi tiết</span>
          </button>
        </div>
      </section>

      <section className="phs-moments" aria-label="Khoảnh khắc đáng nhớ">
        <header className="phs-sec-head">
          <div>
            <h3>Khoảnh khắc</h3>
            <p>Ảnh, giọng nói và lời cảm ơn trong nhà.</p>
          </div>
          <button type="button" className="phs-link" onClick={props.onOpenDiary}>
            Xem tất cả
          </button>
        </header>
        <div className="phs-hscroll" role="list">
          {momentCards.length > 0 ? (
            momentCards
          ) : (
            <div className="phs-empty-card">
              <p>Chưa có khoảnh khắc — khi con gửi ảnh hoặc giọng nói sẽ hiện ở đây.</p>
              <button type="button" className="phs-link" onClick={props.onOpenDiary}>
                Mở Nhật ký
              </button>
            </div>
          )}
        </div>
      </section>

      <section className="phs-coach" aria-label="AI Coach">
        <div className="phs-coach-mark" aria-hidden>
          <img src="/brand/fami-mark-48.png" alt="" />
        </div>
        <div className="phs-coach-copy">
          <p className="phs-coach-eyebrow">
            AI Coach
            <span>Góc nhìn cho gia đình</span>
          </p>
          <p className="phs-coach-body">{homeCoachInsight}</p>
          {homeCoachChip ? (
            <button
              type="button"
              className="phs-coach-chip"
              onClick={homeCoachChip.onClick}
            >
              <span aria-hidden>✦</span>
              {homeCoachChip.label}
            </button>
          ) : null}
          <div className="phs-coach-foot">
            <button type="button" className="phs-coach-cta" onClick={props.onOpenCoach}>
              Xem gợi ý
            </button>
            <time className="phs-coach-time">Cập nhật {nowLabel}</time>
          </div>
        </div>
      </section>
    </div>
  );
}
