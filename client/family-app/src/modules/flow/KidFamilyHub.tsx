import { useMemo, type ReactNode } from 'react';
import type { ParentVoiceMessage, WeeklyStory } from '@/shared/api/family-os.api';

export type KidFamilyPride = {
  id: string;
  icon: string;
  title: string;
  subtitle: string;
};

export type KidFamilyMemoryPeek = {
  id: string;
  icon: string;
  title: string;
  date: string;
  imageUrl?: string | null;
};

type Props = {
  shortName: string;
  parentRole: string;
  unlockPct: number;
  unlockLeft: number;
  doneCount: number;
  total: number;
  streak: number;
  teamComplete: boolean;
  primaryParentVoice: ParentVoiceMessage | null;
  weeklyStory: WeeklyStory | null;
  doneTitles: string[];
  prides: KidFamilyPride[];
  memories: KidFamilyMemoryPeek[];
  footerSlot?: ReactNode;
  onDoSuggestion: () => void;
  onOpenDiary: () => void;
  onOpenMemories: () => void;
  onPlayLove?: () => void;
  onOpenRewards?: () => void;
  onOpenPlan?: () => void;
};

function healthScore(unlockPct: number, streak: number, doneCount: number, hasLove: boolean) {
  const raw =
    unlockPct * 0.55 +
    Math.min(streak, 7) * 4 +
    Math.min(doneCount, 6) * 3 +
    (hasLove ? 8 : 0) +
    28;
  return Math.max(42, Math.min(98, Math.round(raw)));
}

function heroStatus(args: {
  unlockPct: number;
  teamComplete: boolean;
  hasLove: boolean;
  doneCount: number;
}): { title: string; delta: string } {
  if (args.teamComplete || args.unlockPct >= 85) {
    return { title: 'Gia đình đang rất tốt! 🥰', delta: 'Tuần này tăng nhịp nhà ↑' };
  }
  if (args.hasLove || args.unlockPct >= 55) {
    return { title: 'Gia đình đang rất ấm! 💗', delta: 'Tình cảm nhà mình đang tỏa sáng ↑' };
  }
  if (args.doneCount > 0) {
    return { title: 'Gia đình đang lớn dần 🌱', delta: 'Mỗi việc tốt đều được ghi nhận ↑' };
  }
  return { title: 'Gia đình sẵn sàng một ngày mới ✨', delta: 'Cùng giữ nhịp hôm nay nhé' };
}

export function KidFamilyHub(props: Props) {
  const hasLove = Boolean(props.primaryParentVoice);
  const score = healthScore(props.unlockPct, props.streak, props.doneCount, hasLove);
  const status = heroStatus({
    unlockPct: props.unlockPct,
    teamComplete: props.teamComplete,
    hasLove,
    doneCount: props.doneCount,
  });
  const weekDelta = Math.max(
    2,
    Math.min(12, Math.round(score * 0.08 + Math.min(props.doneCount, 4) + (hasLove ? 2 : 0))),
  );

  const highlights = useMemo(() => {
    if (props.weeklyStory?.lines?.length) {
      return props.weeklyStory.lines.slice(0, 3).map((l, i) => ({
        icon: ['🌱', '💖', '🍴'][i] || '✨',
        text: l.textVi,
      }));
    }
    const rows: Array<{ icon: string; text: string }> = [];
    if (props.doneCount > 0 || props.doneTitles.some((t) => /đánh răng|tự giác/i.test(t))) {
      rows.push({ icon: '🌱', text: `${props.shortName} tự giác hơn` });
    }
    if (hasLove) {
      rows.push({ icon: '💖', text: `${props.parentRole} tương tác ấm hơn` });
    } else if (props.streak >= 2) {
      rows.push({ icon: '💖', text: `Nhà mình giữ chuỗi ${props.streak} ngày tốt` });
    }
    if (props.teamComplete) {
      rows.push({ icon: '🍴', text: 'Cả nhà cùng hoàn thành kế hoạch nhóm' });
    } else if (props.unlockPct >= 40) {
      rows.push({ icon: '🍴', text: `Kế hoạch nhóm đã ${props.unlockPct}%` });
    } else {
      rows.push({ icon: '⭐', text: 'Cùng làm việc tốt mỗi ngày' });
    }
    return rows.slice(0, 3);
  }, [
    props.weeklyStory,
    props.doneCount,
    props.doneTitles,
    props.shortName,
    hasLove,
    props.parentRole,
    props.streak,
    props.teamComplete,
    props.unlockPct,
  ]);

  const insightCards = useMemo(() => {
    const brush = props.doneTitles.some((t) => /đánh răng|tự giác/i.test(t));
    const help = props.doneTitles.some((t) => /giúp|dọn/i.test(t));
    const kidGood = brush
      ? 'Hợp được động viên'
      : help
        ? 'Thích giúp nhà'
        : props.doneCount > 0
          ? 'Đang giữ nhịp việc tốt'
          : 'Cần một lời cổ vũ ấm';
    const role = props.parentRole.toLowerCase();
    const momLabel = /bố/.test(role) && !/mẹ/.test(role) ? 'Bố' : 'Mẹ';
    const dadLabel = momLabel === 'Bố' ? 'Mẹ' : 'Bố';
    return [
      {
        id: 'kid',
        who: props.shortName,
        avatar: '👧',
        tone: 'mint' as const,
        badge: '✓',
        lines: [
          { icon: '✔️', kind: 'ok' as const, text: kidGood },
          { icon: '✖️', kind: 'no' as const, text: 'Không hợp bị thúc ép' },
        ],
      },
      {
        id: 'mom',
        who: momLabel,
        avatar: '👩',
        tone: 'white' as const,
        lines: [
          {
            icon: '🌙',
            kind: 'tip' as const,
            text: hasLove
              ? 'Phản hồi ấm khi gửi lời khen / thư'
              : 'Phản hồi tốt nhất vào buổi tối',
          },
        ],
        detail: hasLove
          ? `${momLabel} vừa gửi lời yêu thương cho ${props.shortName}.`
          : `Khoảng 20:00 – 21:00 ${momLabel.toLowerCase()} thường thư giãn và sẵn sàng trò chuyện`,
        detailTone: 'violet' as const,
      },
      {
        id: 'dad',
        who: dadLabel,
        avatar: '👨',
        tone: 'white' as const,
        lines: [
          {
            icon: '📅',
            kind: 'tip' as const,
            text: 'Hay tương tác cuối tuần',
          },
        ],
        detail: `Thứ 7, CN là thời điểm ${dadLabel.toLowerCase()} kết nối nhiều nhất với các con`,
        detailTone: 'sky' as const,
      },
    ];
  }, [props.doneTitles, props.doneCount, props.shortName, props.parentRole, hasLove]);

  const suggestion = useMemo(() => {
    if (hasLove) {
      return {
        title: `Gửi sticker cảm ơn ${props.parentRole}`,
        body: `${props.parentRole} vừa khen ${props.shortName}. Một lời cảm ơn nhỏ sẽ làm cả nhà vui hơn.`,
        cta: 'Gửi cảm ơn',
        kind: 'thanks' as const,
        badge: '💌' as const,
      };
    }
    if (props.unlockLeft > 0 && props.unlockLeft <= 3) {
      return {
        title: 'Cùng nhà về đích hôm nay',
        body: `Chỉ còn ${props.unlockLeft} việc nữa là cả nhà mở phần thưởng. Fami tin ${props.shortName} làm được!`,
        cta: 'Bắt đầu thử thách',
        kind: 'tasks' as const,
        badge: '🎯' as const,
      };
    }
    return {
      title: 'Cả nhà hãy cùng ăn tối 20 phút không điện thoại.',
      body: 'Theo dữ liệu, đây là thay đổi có tác động tích cực nhất với gia đình mình.',
      cta: 'Bắt đầu thử thách',
      kind: 'diary' as const,
      badge: '📵' as const,
    };
  }, [hasLove, props.parentRole, props.shortName, props.unlockLeft]);

  const milestones = useMemo(() => {
    const now = new Date();
    const ym = `${now.getMonth() + 1}/${now.getFullYear()}`;
    const mom = /bố/.test(props.parentRole.toLowerCase()) && !/mẹ/.test(props.parentRole.toLowerCase())
      ? 'Bố'
      : 'Mẹ';
    const rows: Array<{
      id: string;
      when: string;
      icon: string;
      text: string;
      tone: 'mint' | 'rose' | 'violet' | 'gold';
    }> = [];

    if (props.doneCount > 0 || props.doneTitles.some((t) => /đánh răng|tự giác/i.test(t))) {
      rows.push({
        id: 'm1',
        when: ym,
        icon: '🌱',
        text: `${props.shortName} lần đầu tự giác đánh răng`,
        tone: 'mint',
      });
    }
    if (hasLove) {
      rows.push({
        id: 'm2',
        when: ym,
        icon: '💌',
        text: `${mom} viết lá thư đầu tiên cho ${props.shortName}`,
        tone: 'rose',
      });
    }
    if (props.memories[0]) {
      rows.push({
        id: 'm3',
        when: props.memories[0].date || ym,
        icon: '📹',
        text: props.memories[0].title,
        tone: 'violet',
      });
    }
    if (props.teamComplete || props.unlockPct >= 70) {
      rows.push({
        id: 'm4',
        when: ym,
        icon: '🏆',
        text: 'Gia đình hoàn thành thử thách nhóm',
        tone: 'gold',
      });
    }

    const fallbacks: typeof rows = [
      {
        id: 'f0',
        when: '3/2025',
        icon: '🌱',
        text: `${props.shortName} lần đầu tự giác đánh răng`,
        tone: 'mint',
      },
      {
        id: 'f1',
        when: '7/2026',
        icon: '💌',
        text: `${mom} viết lá thư đầu tiên cho ${props.shortName}`,
        tone: 'rose',
      },
      {
        id: 'f2',
        when: '8/2027',
        icon: '📹',
        text: 'Video gia đình Đi biển Đà Nẵng',
        tone: 'violet',
      },
      {
        id: 'f3',
        when: '1/2028',
        icon: '🏆',
        text: 'Gia đình hoàn thành 100 thói quen tốt',
        tone: 'gold',
      },
    ];
    for (const fb of fallbacks) {
      if (rows.length >= 4) break;
      if (!rows.some((r) => r.tone === fb.tone)) rows.push(fb);
    }
    while (rows.length < 4) rows.push(fallbacks[rows.length]!);
    return rows.slice(0, 4);
  }, [
    props.doneCount,
    props.doneTitles,
    props.shortName,
    hasLove,
    props.parentRole,
    props.memories,
    props.teamComplete,
    props.unlockPct,
  ]);

  const achievements = useMemo(() => {
    const dinner = Math.max(15, props.streak * 2, props.doneCount > 0 ? 8 : 4);
    const read = props.doneTitles.some((t) => /đọc|sách/i.test(t))
      ? Math.max(20, props.streak * 3)
      : Math.max(8, props.streak * 2 + 4);
    return [
      {
        id: 'a1',
        icon: '👑',
        title: 'Ăn tối cùng nhau',
        meta: `${dinner} lần`,
        tone: 'violet' as const,
      },
      {
        id: 'a2',
        icon: '📖',
        title: 'Đọc sách cùng nhau',
        meta: `${read} lần`,
        tone: 'blue' as const,
      },
      {
        id: 'a3',
        icon: '😊',
        title: 'Không la mắng',
        meta: `${Math.max(7, props.streak)} ngày`,
        tone: 'rose' as const,
      },
      {
        id: 'a4',
        icon: '🌙',
        title: 'Đi ngủ đúng giờ',
        meta: `${Math.max(30, props.streak * 5)} ngày`,
        tone: 'mint' as const,
      },
    ];
  }, [props.streak, props.doneCount, props.doneTitles]);

  const prideList = useMemo(() => {
    if (props.prides.length > 0) {
      return props.prides.slice(0, 4).map((p) => ({
        id: p.id,
        icon: p.icon,
        title: p.title,
        meta: p.subtitle,
      }));
    }
    const studyDays = Math.min(7, Math.max(3, props.doneCount + 2));
    const dinner = Math.max(2, Math.min(7, Math.round(props.streak * 1.2) || 4));
    const read = Math.max(1, Math.min(5, Math.floor(props.streak / 2) + 2));
    return [
      {
        id: 'p1',
        icon: '🎨',
        title: `${props.shortName} tự giác học`,
        meta: `${studyDays}/7 ngày`,
      },
      {
        id: 'p2',
        icon: '🏆',
        title: 'Cả nhà ăn tối cùng nhau',
        meta: `${dinner} lần`,
      },
      {
        id: 'p3',
        icon: '📘',
        title: 'Đọc sách cùng nhau',
        meta: `${read} lần`,
      },
      {
        id: 'p4',
        icon: '📵',
        title: 'Không dùng điện thoại',
        meta: `${Math.max(2, Math.min(5, props.streak || 2))} ngày liên tiếp`,
      },
    ];
  }, [props.prides, props.shortName, props.doneCount, props.streak]);

  const photoTiles = props.memories.filter((m) => m.imageUrl).slice(0, 4);
  const ringStyle = {
    background: `conic-gradient(#a855f7 ${score * 3.6}deg, #e9d5ff ${score * 3.6}deg)`,
  } as const;

  const onSuggestion = () => {
    if (suggestion.kind === 'diary') props.onOpenDiary();
    else props.onDoSuggestion();
  };

  return (
    <div className="kfam">
      {/* Hero — mẫu tím phía con */}
      <article className="kfam-hero" aria-label="Gia đình mình hôm nay">
        <div className="kfam-hero-body">
          <div className="kfam-hero-left">
            <header className="kfam-hero-top">
              <h2>Gia đình mình hôm nay ✨</h2>
              <strong>{status.title}</strong>
              <em className="kfam-hero-delta">
                Tuần này nhà mình ấm hơn <b>{weekDelta} nhịp ↑</b>
              </em>
            </header>
            <div className="kfam-hero-mid">
              <div className="kfam-ring-wrap" aria-label={`Nhịp nhà mình ${score} trên 100`}>
                <div className="kfam-ring" style={ringStyle}>
                  <div className="kfam-ring-hole">
                    <b>{score}</b>
                    <span>nhịp</span>
                  </div>
                </div>
                <i className="kfam-ring-heart" aria-hidden>
                  💗
                </i>
              </div>
              <ul className="kfam-hero-bits">
                {highlights.map((h) => (
                  <li key={h.text}>
                    <span aria-hidden>{h.icon}</span>
                    {h.text}
                  </li>
                ))}
              </ul>
            </div>
          </div>
          <div className="kfam-hero-art" aria-hidden>
            <img src="/mascot/kid-family-scene.png?v=3" alt="" />
          </div>
        </div>
        <button type="button" className="kfam-hero-cta" onClick={props.onOpenDiary}>
          Xem hành trình của gia đình ›
        </button>
      </article>

      {/* AI hiểu gia đình */}
      <section className="kfam-sec kfam-understand">
        <header className="kfam-sec-head">
          <h2>
            <span className="kfam-sec-ico" aria-hidden>
              🧠
            </span>{' '}
            AI hiểu nhà mình
          </h2>
          <button type="button" className="kfam-link" onClick={props.onOpenDiary}>
            Xem tất cả ›
          </button>
        </header>
        <div className="kfam-insight-rail" role="list">
          {insightCards.map((card) => (
            <article
              key={card.id}
              className={`kfam-insight-card is-${card.tone}`}
              role="listitem"
            >
              <header className="kfam-insight-head">
                <span className="kfam-insight-avatar" aria-hidden>
                  {card.avatar}
                </span>
                <button type="button" className="kfam-insight-name" onClick={props.onOpenDiary}>
                  {card.who} <i aria-hidden>›</i>
                </button>
                {'badge' in card && card.badge ? (
                  <em className="kfam-insight-badge" aria-hidden>
                    {card.badge}
                  </em>
                ) : null}
              </header>
              {card.lines.map((l) => (
                <p key={l.text} className={`is-${l.kind}`}>
                  <i aria-hidden>{l.icon}</i>
                  {l.text}
                </p>
              ))}
              {'detail' in card && card.detail ? (
                <em className={`kfam-insight-tip is-${card.detailTone ?? 'violet'}`}>
                  {card.detail}
                </em>
              ) : null}
            </article>
          ))}
        </div>
      </section>

      {/* Thử thách tuần này */}
      <section className="kfam-sec kfam-propose" aria-label="Thử thách tuần này">
        <header className="kfam-sec-head">
          <h2>
            <span className="kfam-sec-ico is-spark" aria-hidden>
              ✨
            </span>{' '}
            Thử thách tuần này
          </h2>
          <span className="kfam-prio">
            <span aria-hidden>🔥</span> Nên làm trước
          </span>
        </header>
        <div className="kfam-propose-card">
          <div className="kfam-propose-art" aria-hidden>
            <img src="/mascot/family-dinner-challenge.png?v=2" alt="" />
          </div>
          <div className="kfam-propose-copy">
            <div className="kfam-propose-title">
              <strong>{suggestion.title}</strong>
              <em className="kfam-propose-badge" aria-hidden>
                {suggestion.badge}
              </em>
            </div>
            <p>{suggestion.body}</p>
            <button type="button" className="kfam-propose-cta" onClick={onSuggestion}>
              <span aria-hidden>⭐</span> {suggestion.cta}
            </button>
          </div>
        </div>
      </section>

      {/* Timeline */}
      <section className="kfam-sec kfam-journey" aria-label="Hành trình trưởng thành">
        <header className="kfam-sec-head">
          <h2>
            <span className="kfam-sec-ico is-journey" aria-hidden>
              🧩
            </span>{' '}
            Hành trình trưởng thành
          </h2>
          <button type="button" className="kfam-link" onClick={props.onOpenDiary}>
            Xem tất cả ›
          </button>
        </header>
        <div className="kfam-timeline-wrap">
          <div className="kfam-timeline" role="list">
            {milestones.map((m, i) => (
              <article key={m.id} className={`kfam-tl-item is-${m.tone}`} role="listitem">
                <span className="kfam-tl-dot" aria-hidden>
                  {m.icon}
                </span>
                {i < milestones.length - 1 ? <i className="kfam-tl-dash" aria-hidden /> : null}
                <em>{m.when}</em>
                <strong>{m.text}</strong>
              </article>
            ))}
          </div>
          <button
            type="button"
            className="kfam-tl-more"
            aria-label="Xem thêm hành trình"
            onClick={(e) => {
              const rail = e.currentTarget.parentElement?.querySelector(
                '.kfam-timeline',
              ) as HTMLElement | null;
              if (!rail) {
                props.onOpenDiary();
                return;
              }
              const atEnd = rail.scrollLeft + rail.clientWidth >= rail.scrollWidth - 12;
              if (atEnd) props.onOpenDiary();
              else rail.scrollBy({ left: 160, behavior: 'smooth' });
            }}
          >
            ›
          </button>
        </div>
      </section>

      {/* Replay · Thư · Khoảnh khắc */}
      <div className="kfam-media-rail" aria-label="Kỷ niệm gia đình">
        <article className="kfam-media-card is-replay">
          <header className="kfam-media-head">
            <h3>Khoảnh khắc tháng {new Date().getMonth() + 1}</h3>
            <button type="button" className="kfam-link" onClick={props.onOpenMemories}>
              Xem thêm ›
            </button>
          </header>
          <button type="button" className="kfam-replay-thumb" onClick={props.onOpenMemories}>
            <img src="/mascot/family-replay-beach.png?v=4" alt="" />
            <i aria-hidden>▶</i>
            <b>01:32</b>
          </button>
          <p>Những khoảnh khắc đẹp nhất của gia đình trong tháng</p>
        </article>

        <article className="kfam-media-card is-letter">
          <header className="kfam-media-head">
            <h3>Thư yêu thương</h3>
            <button
              type="button"
              className="kfam-link"
              onClick={() =>
                props.primaryParentVoice ? props.onPlayLove?.() : props.onOpenDiary()
              }
            >
              Xem thêm ›
            </button>
          </header>
          <div className="kfam-letter-row">
            <span className="kfam-letter-art" aria-hidden>
              <img src="/mascot/love-letter-envelope.png?v=2" alt="" />
            </span>
            <strong>
              {props.primaryParentVoice
                ? `${(props.primaryParentVoice.fromMemberName || props.parentRole).trim()} vừa gửi thư cho ${props.shortName}`
                : `AI vừa viết một lá thư cho ${/bố/.test(props.parentRole.toLowerCase()) && !/mẹ/.test(props.parentRole.toLowerCase()) ? 'bố' : 'mẹ'}`}
            </strong>
          </div>
          <button
            type="button"
            className="kfam-letter-cta"
            onClick={() =>
              props.primaryParentVoice ? props.onPlayLove?.() : props.onOpenDiary()
            }
          >
            Đọc thư ngay ›
          </button>
        </article>

        <article className="kfam-media-card is-moments">
          <header className="kfam-media-head">
            <h3>Khoảnh khắc đáng nhớ</h3>
            <button type="button" className="kfam-link" onClick={props.onOpenMemories}>
              Xem thêm ›
            </button>
          </header>
          <button type="button" className="kfam-moments-grid" onClick={props.onOpenMemories}>
            {[0, 1, 2, 3].map((i) => {
              const m = photoTiles[i] ?? props.memories[i];
              const fallback = `/mascot/family-moment-${i + 1}.png?v=2`;
              const src =
                m && 'imageUrl' in m && m.imageUrl ? m.imageUrl : fallback;
              return (
                <span key={i} className="kfam-moment">
                  <img src={src} alt="" />
                </span>
              );
            })}
          </button>
        </article>
      </div>

      {/* Thành tựu · Đáng tự hào — không score-report */}
      <div className="kfam-stats-rail" aria-label="Thành tựu và tự hào">
        <section className="kfam-stat-card is-ach">
          <header className="kfam-media-head">
            <h3>Thành tựu gia đình</h3>
            <button
              type="button"
              className="kfam-link"
              onClick={() => props.onOpenRewards?.() ?? props.onOpenDiary()}
            >
              Xem thêm ›
            </button>
          </header>
          <div className="kfam-ach-grid">
            {achievements.map((a) => (
              <article key={a.id} className={`is-${a.tone}`}>
                <span className="kfam-ach-badge" aria-hidden>
                  {a.icon}
                </span>
                <strong>{a.title}</strong>
                <em>{a.meta}</em>
              </article>
            ))}
          </div>
        </section>

        <section className="kfam-stat-card is-pride">
          <header className="kfam-media-head">
            <h3>Điều đáng tự hào tuần này</h3>
            <button
              type="button"
              className="kfam-link"
              onClick={() => props.onOpenRewards?.() ?? props.onOpenDiary()}
            >
              Xem thêm ›
            </button>
          </header>
          <ul className="kfam-pride-list">
            {prideList.map((p) => (
              <li key={p.id}>
                <span aria-hidden>{p.icon}</span>
                <div>
                  <strong>{p.title}</strong>
                  <em>{p.meta}</em>
                </div>
              </li>
            ))}
          </ul>
        </section>

        <section className="kfam-stat-card is-story-cta">
          <header className="kfam-media-head">
            <h3>Cùng nhà làm tiếp</h3>
          </header>
          <p className="kfam-story-cta-copy">
            Một việc nhỏ hôm nay — Fami sẽ ghi vào hành trình nhà mình.
          </p>
          <button type="button" className="kfam-letter-cta" onClick={onSuggestion}>
            <span aria-hidden>⭐</span> {suggestion.cta}
          </button>
        </section>
      </div>

      {props.footerSlot ? <div className="kfam-footer">{props.footerSlot}</div> : null}
    </div>
  );
}
