import type { ReactNode } from 'react';
import type {
  FamilyDnaCard,
  FamilyBehaviorTwin,
  WeekPlaybook,
} from '@/shared/api/family-os.api';
import type { ParentPulse } from '@/shared/value/parent-pulse';
import type { FvView } from '@/modules/flow/FamilyValuePanel';
import { withEvidenceAuth } from '@/shared/upload/evidence-url';

export type FamilyMomentCard = {
  id: string;
  title: string;
  subtitle?: string;
  icon?: string;
  photoUrl?: string;
};

type ExploreCard = {
  id: string;
  icon: string;
  title: string;
  hint: string;
  view: Exclude<FvView, 'hub'>;
  tone: 'violet' | 'teal' | 'amber' | 'rose';
};

type TipLite = {
  doThis: string;
  insight: string;
};

type Props = {
  familyName: string;
  parentHelloLabel: string;
  childPicker: ReactNode;
  noChildNotice?: ReactNode;
  hasChildren: boolean;
  attentionCount: number;
  pulse: ParentPulse;
  scopedDone: number;
  scopedTotal: number;
  momentsCount: number;
  coachTips: TipLite[];
  twin: FamilyBehaviorTwin | null;
  dna: FamilyDnaCard | null;
  weekPlaybook: WeekPlaybook | null;
  coachInsightHeadline?: string | null;
  moments: FamilyMomentCard[];
  onOpenAttention: () => void;
  onOpenProfile: () => void;
  onOpenWeeklyPlan: () => void;
  onOpenCoach: () => void;
  onOpenMoments: () => void;
  onOpenExplore: (view: Exclude<FvView, 'hub'>) => void;
};

const EXPLORE: ExploreCard[] = [
  {
    id: 'q3',
    icon: '💛',
    title: '3Q — AI hiểu gia đình',
    hint: '3 câu hỏi tối nay',
    view: 'q3',
    tone: 'amber',
  },
  {
    id: 'rop',
    icon: '🔬',
    title: 'Insight chuyên sâu',
    hint: 'Báo cáo tăng trưởng',
    view: 'rop',
    tone: 'teal',
  },
  {
    id: 'wins',
    icon: '🏆',
    title: 'Ghi nhận đáng tự hào',
    hint: 'Thành tựu nhà',
    view: 'recognition',
    tone: 'amber',
  },
  {
    id: 'timeline',
    icon: '🎯',
    title: 'Mục tiêu dài hạn',
    hint: 'Hành trình trưởng thành',
    view: 'timeline',
    tone: 'violet',
  },
];

const FALLBACK_AXES = [
  { label: 'Trách nhiệm', score: 68 },
  { label: 'Yêu thương', score: 62 },
  { label: 'Giao tiếp', score: 48 },
  { label: 'Kỷ luật', score: 58 },
  { label: 'Học hỏi', score: 66 },
];

function radarAxes(twin: FamilyBehaviorTwin | null) {
  const dims = twin?.children?.[0]?.dimensions;
  if (!dims || dims.length === 0) return FALLBACK_AXES;
  return dims.slice(0, 5).map((d) => ({
    label: d.labelVi || d.code,
    score: Math.max(0, Math.min(100, Math.round(d.score))),
  }));
}

function radarPoints(scores: number[], cx: number, cy: number, r: number) {
  const n = scores.length;
  return scores
    .map((s, i) => {
      const a = (-Math.PI / 2) + (i * 2 * Math.PI) / n;
      const rr = (s / 100) * r;
      return `${cx + Math.cos(a) * rr},${cy + Math.sin(a) * rr}`;
    })
    .join(' ');
}

function ringPoints(n: number, cx: number, cy: number, r: number) {
  return Array.from({ length: n }, (_, i) => {
    const a = (-Math.PI / 2) + (i * 2 * Math.PI) / n;
    return `${cx + Math.cos(a) * r},${cy + Math.sin(a) * r}`;
  }).join(' ');
}

function qualityMinutes(done: number, moments: number) {
  return Math.max(0, Math.min(45, done * 3 + (moments > 0 ? 6 : 0)));
}

function growthInsight(
  axes: Array<{ label: string; score: number }>,
  pulse: ParentPulse,
  coachHeadline?: string | null,
) {
  if (coachHeadline?.trim()) return coachHeadline.trim();
  const sorted = [...axes].sort((a, b) => b.score - a.score);
  const best = sorted[0];
  const weak = sorted[sorted.length - 1];
  if (best && weak && best.label !== weak.label) {
    return `Tuần này gia đình mình đang tiến bộ ở ${best.label} 💪 nhưng cần cải thiện ${weak.label} ❤️`;
  }
  return pulse.insightVi || pulse.dayMoodVi || pulse.headlineVi;
}

export function ParentFamilyPanel(props: Props) {
  const axes = radarAxes(props.twin);
  const score = Math.max(0, Math.min(100, Math.round(props.pulse.familyScore)));
  const deltaHint =
    props.pulse.nudgeTrend === 'down'
      ? '+ điểm nhịp nhà'
      : props.pulse.nudgeTrend === 'up'
        ? 'Cần sát cánh thêm'
        : 'Tuần này';
  const insight = growthInsight(axes, props.pulse, props.coachInsightHeadline);
  const responsibility =
    axes.find((a) => /trách|respon/i.test(a.label))?.score ??
    Math.round((props.twin?.familyPeaceIndex ?? score) || score);
  const mins = qualityMinutes(props.scopedDone, props.momentsCount);
  const ratio = `${props.scopedDone}/${Math.max(props.scopedTotal, 0)}`;
  const dnaMatch = Math.max(
    55,
    Math.min(92, Math.round((props.twin?.familyPeaceIndex ?? score) || 72)),
  );
  const dnaLabel =
    props.dna?.growthBalanceLabelVi ||
    props.dna?.stageLabelVi ||
    'Người đồng hành';
  const learnings = [
    props.dna?.coachTipVi,
    props.weekPlaybook?.parentStrategyTipVi,
    props.twin?.retirementAdviceVi,
    props.coachTips[0]?.insight,
  ]
    .map((s) => (s || '').trim())
    .filter(Boolean)
    .slice(0, 3) as string[];

  const tips: TipLite[] =
    props.coachTips.length > 0
      ? props.coachTips.slice(0, 3)
      : [
          {
            doThis: '10 phút trò chuyện cùng con',
            insight: 'Hỏi con hôm nay có điều gì vui?',
          },
          {
            doThis: 'Khen ngợi 1 điều mỗi thành viên',
            insight: 'Một lời khen chân thành = một trái tim hạnh phúc',
          },
        ];

  const familyTitle = props.familyName?.trim() || 'Gia đình mình';

  return (
    <div className="pf-root" id="pf-family">
      <header className="pf-top">
        <div className="pf-titles">
          <button type="button" className="pf-family-switch" onClick={props.onOpenProfile}>
            <strong>
              {familyTitle} <span aria-hidden>▾</span>
            </strong>
            <em>Trung tâm trưởng thành</em>
          </button>
        </div>
        <div className="pf-top-actions">
          <button
            type="button"
            className="pf-ico-btn pf-bell"
            aria-label="Việc cần chú ý"
            onClick={props.onOpenAttention}
          >
            🔔
            {props.attentionCount > 0 ? (
              <i>{Math.min(props.attentionCount, 9)}</i>
            ) : null}
          </button>
          {props.childPicker}
        </div>
      </header>

      {!props.hasChildren ? props.noChildNotice : null}

      <article className="pf-hero">
        <div className="pf-hero-main">
          <div className="pf-hero-copy">
            <p className="pf-hero-kicker">
              Điểm sức khỏe nhà <span aria-hidden>ⓘ</span>
            </p>
            <p className="pf-hero-score">
              <strong>{score}</strong>
              <em>/ 100</em>
            </p>
            <span className="pf-hero-delta">
              Tuần này <b>↗ {deltaHint}</b>
            </span>
            <p className="pf-hero-insight">{insight}</p>
            <button type="button" className="pf-hero-cta" onClick={props.onOpenWeeklyPlan}>
              Xem kế hoạch tuần <span aria-hidden>›</span>
            </button>
          </div>
          <div className="pf-hero-art" aria-hidden>
            <span className="pf-hero-bubble is-a">👨</span>
            <span className="pf-hero-bubble is-b">👩</span>
            <span className="pf-hero-bubble is-c">👧</span>
            <span className="pf-hero-bubble is-d">💚</span>
          </div>
        </div>
        <div className="pf-hero-metrics">
          <div>
            <strong className="is-ok">{ratio}</strong>
            <em>Việc đã hoàn thành</em>
          </div>
          <div>
            <strong className="is-pink">{mins} phút</strong>
            <em>Thời gian chất lượng</em>
          </div>
          <div>
            <strong className="is-amber">{props.momentsCount}</strong>
            <em>Khoảnh khắc đẹp</em>
          </div>
          <div>
            <strong className="is-teal">{responsibility}%</strong>
            <em>Trách nhiệm</em>
          </div>
        </div>
      </article>

      <section className="pf-sec">
        <header className="pf-sec-head">
          <h2>
            <span aria-hidden>✨</span> AI đề xuất hôm nay
          </h2>
          <button type="button" className="pf-link" onClick={props.onOpenCoach}>
            Xem tất cả ›
          </button>
        </header>
        <p className="pf-sec-lead">
          AI muốn cả nhà cùng thực hiện {Math.min(tips.length, 2)} việc nhỏ để ngày hôm nay thêm
          tuyệt vời nhé! 💚
        </p>
        <div className="pf-hscroll" role="list">
          {tips.map((tip, idx) => (
            <article key={`${tip.doThis}-${idx}`} className="pf-suggest" role="listitem">
              <span className="pf-suggest-ico" aria-hidden>
                {idx === 0 ? '💚' : '🌱'}
              </span>
              <strong>{tip.doThis}</strong>
              <p>{tip.insight}</p>
              <footer>
                <em>{idx === 0 ? '10 phút' : '5 phút'}</em>
                <button type="button" className="pf-mini" onClick={props.onOpenCoach}>
                  Làm ngay ›
                </button>
              </footer>
            </article>
          ))}
        </div>
      </section>

      <section className="pf-sec">
        <header className="pf-sec-head">
          <h2>
            <span aria-hidden>📊</span> Gia đình đang phát triển
          </h2>
          <button type="button" className="pf-link" onClick={() => props.onOpenExplore('weekly')}>
            Xem tất cả ›
          </button>
        </header>
        <div className="pf-growth">
          <article className="pf-radar-card">
            <svg viewBox="0 0 160 160" className="pf-radar" aria-hidden>
              {[0.35, 0.6, 0.85, 1].map((t) => (
                <polygon
                  key={t}
                  points={ringPoints(axes.length, 80, 80, 58 * t)}
                  className="pf-radar-ring"
                />
              ))}
              <polygon
                points={radarPoints(
                  axes.map((a) => a.score),
                  80,
                  80,
                  58,
                )}
                className="pf-radar-fill"
              />
            </svg>
            <ul className="pf-radar-legend">
              {axes.map((a) => (
                <li key={a.label}>
                  <span>{a.label}</span>
                  <b>{a.score}%</b>
                </li>
              ))}
            </ul>
          </article>
          <div className="pf-growth-side">
            <article className="pf-mini-card">
              <em>Xu hướng điểm nhà</em>
              <strong>
                {score}/100
              </strong>
              <span className="pf-trend-up">↗ {deltaHint}</span>
              <div className="pf-spark" aria-hidden>
                <i style={{ height: '28%' }} />
                <i style={{ height: '42%' }} />
                <i style={{ height: '36%' }} />
                <i style={{ height: '58%' }} />
                <i style={{ height: '72%' }} />
                <i style={{ height: '64%' }} />
                <i style={{ height: '88%' }} />
              </div>
            </article>
            <article className="pf-mini-card is-dna">
              <em>DNA gia đình</em>
              <strong>
                Gia đình mình giống «{dnaLabel}»
              </strong>
              <div className="pf-dna-ring" aria-hidden>
                <b>{dnaMatch}%</b>
              </div>
            </article>
          </div>
          <article className="pf-learn">
            <div className="pf-learn-art" aria-hidden>
              🤖
            </div>
            <div>
              <strong>AI học được tuần này</strong>
              <ul>
                {(learnings.length > 0
                  ? learnings
                  : [
                      `${props.parentHelloLabel} đang giữ nhịp nhắc nhẹ hiệu quả`,
                      'Cả nhà gắn kết hơn khi có mục tiêu rõ ràng',
                      'Buổi tối là khung giờ kết nối tốt nhất',
                    ]
                ).map((line) => (
                  <li key={line}>
                    <span aria-hidden>✓</span>
                    {line}
                  </li>
                ))}
              </ul>
            </div>
          </article>
        </div>
      </section>

      <section className="pf-sec">
        <header className="pf-sec-head">
          <h2>
            <span aria-hidden>❤️</span> Nhìn lại những khoảnh khắc
          </h2>
          <button type="button" className="pf-link" onClick={props.onOpenMoments}>
            Xem tất cả ›
          </button>
        </header>
        <div className="pf-hscroll" role="list">
          <button
            type="button"
            className="pf-mem is-replay"
            onClick={() => props.onOpenExplore('replay')}
          >
            <span className="pf-mem-badge">Khoảnh khắc tháng</span>
            <strong>Video tổng kết tháng</strong>
            <em>Xem lại ›</em>
          </button>
          <button
            type="button"
            className="pf-mem is-letter"
            onClick={() => props.onOpenExplore('letter')}
          >
            <span className="pf-mem-badge">Thư yêu thương</span>
            <strong>AI vừa viết thư cho {props.parentHelloLabel}</strong>
            <em>Đọc thư ›</em>
          </button>
          {props.moments.length > 0 ? (
            props.moments.slice(0, 4).map((m) => {
              const src = m.photoUrl ? withEvidenceAuth(m.photoUrl) : undefined;
              return (
                <article key={m.id} className="pf-mem is-photo" role="listitem">
                  <div className="pf-mem-art" aria-hidden>
                    {src ? <img src={src} alt="" /> : <span>{m.icon || '✨'}</span>}
                  </div>
                  <strong>{m.title}</strong>
                  {m.subtitle ? <em>{m.subtitle}</em> : null}
                </article>
              );
            })
          ) : (
            <button
              type="button"
              className="pf-mem is-photo"
              onClick={props.onOpenMoments}
            >
              <strong>Khoảnh khắc nổi bật</strong>
              <em>Mở nhật ký để xem ›</em>
            </button>
          )}
          <button
            type="button"
            className="pf-mem is-timeline"
            onClick={() => props.onOpenExplore('timeline')}
          >
            <span className="pf-mem-badge">Timeline trưởng thành</span>
            <strong>Các mốc phát triển của con</strong>
            <em>Xem hành trình ›</em>
          </button>
        </div>
      </section>

      <section className="pf-sec">
        <header className="pf-sec-head">
          <h2>
            <span aria-hidden>💡</span> Khám phá &amp; Hiểu gia đình
          </h2>
          <button type="button" className="pf-link" onClick={() => props.onOpenExplore('weekly')}>
            Xem tất cả ›
          </button>
        </header>
        <div className="pf-hscroll" role="list">
          {EXPLORE.map((c) => (
            <button
              key={c.id}
              type="button"
              className={`pf-explore is-${c.tone}`}
              onClick={() => props.onOpenExplore(c.view)}
            >
              <span aria-hidden>{c.icon}</span>
              <strong>{c.title}</strong>
              <em>{c.hint}</em>
            </button>
          ))}
        </div>
      </section>
    </div>
  );
}
