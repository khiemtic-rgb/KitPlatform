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

function heroStatus(args: {
  unlockPct: number;
  teamComplete: boolean;
  hasLove: boolean;
  doneCount: number;
  streak: number;
}): { title: string; delta: string } {
  if (args.teamComplete || args.unlockPct >= 85) {
    return {
      title: 'Gia đình đang rất vui! 🥰',
      delta:
        args.doneCount > 0
          ? `Hôm nay nhà mình đã xong ${args.doneCount} việc`
          : 'Cả nhà đang tiến gần phần thưởng',
    };
  }
  if (args.hasLove || args.unlockPct >= 55) {
    return {
      title: 'Gia đình đang rất ấm! 💗',
      delta: args.hasLove
        ? 'Có lời yêu thương mới từ bố mẹ'
        : `Kế hoạch nhóm đã ${args.unlockPct}%`,
    };
  }
  if (args.doneCount > 0) {
    return {
      title: 'Gia đình đang lớn dần 🌱',
      delta: `Hôm nay con đã làm ${args.doneCount} việc tốt`,
    };
  }
  if (args.streak > 0) {
    return {
      title: 'Gia đình sẵn sàng một ngày mới ✨',
      delta: `Chuỗi ${args.streak} ngày tốt đang chờ tiếp tục`,
    };
  }
  return {
    title: 'Gia đình sẵn sàng một ngày mới ✨',
    delta: 'Cùng bắt đầu một việc nhỏ nhé',
  };
}

export function KidFamilyHub(props: Props) {
  const hasLove = Boolean(props.primaryParentVoice);
  const status = heroStatus({
    unlockPct: props.unlockPct,
    teamComplete: props.teamComplete,
    hasLove,
    doneCount: props.doneCount,
    streak: props.streak,
  });

  const ringPct = props.teamComplete
    ? 100
    : Math.max(
        0,
        Math.min(
          100,
          props.unlockPct > 0
            ? props.unlockPct
            : props.total > 0
              ? Math.round((props.doneCount / props.total) * 100)
              : 0,
        ),
      );

  const highlights = useMemo(() => {
    if (props.weeklyStory?.lines?.length) {
      return props.weeklyStory.lines.slice(0, 3).map((l, i) => ({
        icon: ['🌱', '💖', '⭐'][i] || '✨',
        text: l.textVi,
      }));
    }
    const rows: Array<{ icon: string; text: string }> = [];
    if (props.doneCount > 0) {
      rows.push({
        icon: '🌱',
        text:
          props.doneTitles.some((t) => /đánh răng|tự giác/i.test(t))
            ? `${props.shortName} tự giác hơn hôm nay`
            : `${props.shortName} đã làm ${props.doneCount} việc tốt`,
      });
    }
    if (hasLove) {
      rows.push({
        icon: '💖',
        text: `${props.parentRole} vừa gửi lời yêu thương`,
      });
    } else if (props.streak >= 2) {
      rows.push({
        icon: '🔥',
        text: `Nhà mình giữ chuỗi ${props.streak} ngày tốt`,
      });
    }
    if (props.teamComplete) {
      rows.push({ icon: '🏆', text: 'Cả nhà cùng hoàn thành kế hoạch nhóm' });
    } else if (props.unlockPct >= 40) {
      rows.push({ icon: '🎯', text: `Kế hoạch nhóm đã ${props.unlockPct}%` });
    }
    if (rows.length === 0) {
      rows.push({ icon: '⭐', text: 'Cùng làm một việc tốt cho nhà mình' });
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

  const famiNotes = useMemo(() => {
    const notes: Array<{ id: string; icon: string; text: string }> = [];
    if (hasLove) {
      notes.push({
        id: 'love',
        icon: '💌',
        text: `${props.parentRole} vừa gửi lời ấm — con có thể đọc ngay.`,
      });
    }
    if (props.doneCount > 0) {
      notes.push({
        id: 'done',
        icon: '⭐',
        text: `Hôm nay ${props.shortName} đã làm ${props.doneCount} việc tốt.`,
      });
    }
    if (props.streak >= 2) {
      notes.push({
        id: 'streak',
        icon: '🔥',
        text: `Chuỗi ${props.streak} ngày tốt — Fami tự hào lắm!`,
      });
    }
    if (props.unlockLeft > 0 && props.unlockLeft <= 3) {
      notes.push({
        id: 'team',
        icon: '🎁',
        text: `Chỉ còn ${props.unlockLeft} việc nữa là cả nhà mở phần thưởng.`,
      });
    }
    if (notes.length === 0) {
      notes.push({
        id: 'start',
        icon: '💚',
        text: `Fami ở đây cùng ${props.shortName} — làm một việc nhỏ là được ghi nhận.`,
      });
    }
    return notes.slice(0, 3);
  }, [
    hasLove,
    props.parentRole,
    props.doneCount,
    props.shortName,
    props.streak,
    props.unlockLeft,
  ]);

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
      title: 'Làm một việc tốt cùng nhà',
      body: `Chọn việc tiếp theo — Fami sẽ ghi vào nhật ký nhà mình cho ${props.shortName}.`,
      cta: 'Xem kế hoạch',
      kind: 'tasks' as const,
      badge: '🌱' as const,
    };
  }, [hasLove, props.parentRole, props.shortName, props.unlockLeft]);

  const milestones = useMemo(() => {
    const now = new Date();
    const ym = `${now.getMonth() + 1}/${now.getFullYear()}`;
    const rows: Array<{
      id: string;
      when: string;
      icon: string;
      text: string;
      tone: 'mint' | 'rose' | 'violet' | 'gold';
    }> = [];

    if (props.doneTitles.some((t) => /đánh răng|tự giác/i.test(t))) {
      rows.push({
        id: 'm-brush',
        when: ym,
        icon: '🌱',
        text: `${props.shortName} tự giác đánh răng hôm nay`,
        tone: 'mint',
      });
    } else if (props.doneCount > 0) {
      rows.push({
        id: 'm-done',
        when: ym,
        icon: '⭐',
        text: `${props.shortName} hoàn thành ${props.doneCount} việc hôm nay`,
        tone: 'mint',
      });
    }
    if (hasLove && props.primaryParentVoice) {
      const from = (props.primaryParentVoice.fromMemberName || props.parentRole).trim();
      rows.push({
        id: 'm-love',
        when: ym,
        icon: '💌',
        text: `${from} gửi lời yêu thương cho ${props.shortName}`,
        tone: 'rose',
      });
    }
    for (const mem of props.memories.slice(0, 2)) {
      rows.push({
        id: `m-mem-${mem.id}`,
        when: mem.date || ym,
        icon: mem.icon || '✨',
        text: mem.title,
        tone: 'violet',
      });
    }
    if (props.teamComplete || props.unlockPct >= 100) {
      rows.push({
        id: 'm-team',
        when: ym,
        icon: '🏆',
        text: 'Gia đình hoàn thành thử thách nhóm hôm nay',
        tone: 'gold',
      });
    }
    return rows.slice(0, 4);
  }, [
    props.doneTitles,
    props.doneCount,
    props.shortName,
    hasLove,
    props.primaryParentVoice,
    props.parentRole,
    props.memories,
    props.teamComplete,
    props.unlockPct,
  ]);

  const prideList = props.prides.slice(0, 4).map((p) => ({
    id: p.id,
    icon: p.icon,
    title: p.title,
    meta: p.subtitle,
  }));

  const photoTiles = props.memories.filter((m) => m.imageUrl).slice(0, 4);
  const ringStyle = {
    background: `conic-gradient(#a855f7 ${ringPct * 3.6}deg, #e9d5ff ${ringPct * 3.6}deg)`,
  } as const;

  const onSuggestion = () => {
    if (suggestion.kind === 'thanks') props.onDoSuggestion();
    else props.onOpenPlan?.() ?? props.onDoSuggestion();
  };

  const openLoveOrDiary = () => {
    if (props.primaryParentVoice) props.onPlayLove?.();
    else props.onOpenDiary();
  };

  return (
    <div className="kfam">
      <article className="kfam-hero" aria-label="Gia đình mình hôm nay">
        <div className="kfam-hero-body">
          <div className="kfam-hero-left">
            <header className="kfam-hero-top">
              <h2>Gia đình mình hôm nay ✨</h2>
              <strong>{status.title}</strong>
              <em className="kfam-hero-delta">{status.delta}</em>
            </header>
            <div className="kfam-hero-mid">
              <div
                className="kfam-ring-wrap"
                aria-label={`Tiến độ nhà mình ${ringPct} phần trăm`}
              >
                <div className="kfam-ring" style={ringStyle}>
                  <div className="kfam-ring-hole">
                    <b>{ringPct}</b>
                    <span>%</span>
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
          Xem nhật ký gia đình ›
        </button>
      </article>

      {/* Fami kể — chỉ từ tín hiệu thật */}
      <section className="kfam-sec kfam-understand">
        <header className="kfam-sec-head">
          <h2>
            <span className="kfam-sec-ico" aria-hidden>
              💚
            </span>{' '}
            Fami kể về nhà mình
          </h2>
          <button type="button" className="kfam-link" onClick={props.onOpenDiary}>
            Xem nhật ký ›
          </button>
        </header>
        <div className="kfam-insight-rail" role="list">
          {famiNotes.map((note) => (
            <article key={note.id} className="kfam-insight-card is-mint" role="listitem">
              <header className="kfam-insight-head">
                <span className="kfam-insight-avatar" aria-hidden>
                  {note.icon}
                </span>
                <span className="kfam-insight-name is-static">Fami</span>
              </header>
              <p className="is-ok">
                <i aria-hidden>💬</i>
                {note.text}
              </p>
            </article>
          ))}
        </div>
      </section>

      <section className="kfam-sec kfam-propose" aria-label="Việc nên làm tiếp">
        <header className="kfam-sec-head">
          <h2>
            <span className="kfam-sec-ico is-spark" aria-hidden>
              ✨
            </span>{' '}
            Fami gợi ý tiếp
          </h2>
          <span className="kfam-prio">
            <span aria-hidden>💚</span> Dành cho con
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

      <section className="kfam-sec kfam-journey" aria-label="Hành trình nhà mình">
        <header className="kfam-sec-head">
          <h2>
            <span className="kfam-sec-ico is-journey" aria-hidden>
              🧩
            </span>{' '}
            Hành trình nhà mình
          </h2>
          <button type="button" className="kfam-link" onClick={props.onOpenDiary}>
            Xem tất cả ›
          </button>
        </header>
        {milestones.length === 0 ? (
          <p className="kfam-empty">
            Chưa có cột mốc hôm nay — làm một việc tốt hoặc nhận lời khen, Fami sẽ ghi ở đây.
          </p>
        ) : (
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
              aria-label="Xem tất cả trên nhật ký"
              onClick={props.onOpenDiary}
            >
              ›
            </button>
          </div>
        )}
      </section>

      <div className="kfam-media-rail" aria-label="Kỷ niệm gia đình">
        <article className="kfam-media-card is-replay">
          <header className="kfam-media-head">
            <h3>Kỷ niệm gần đây</h3>
            <button type="button" className="kfam-link" onClick={props.onOpenMemories}>
              Xem thêm ›
            </button>
          </header>
          {photoTiles[0] ? (
            <button type="button" className="kfam-replay-thumb" onClick={props.onOpenMemories}>
              <img src={photoTiles[0].imageUrl!} alt="" />
              <i aria-hidden>📷</i>
            </button>
          ) : (
            <button type="button" className="kfam-replay-thumb is-empty" onClick={props.onOpenMemories}>
              <span aria-hidden>✨</span>
              <em>Chưa có ảnh kỷ niệm</em>
            </button>
          )}
          <p>
            {photoTiles[0]
              ? photoTiles[0].title
              : 'Khi nhà mình lưu ảnh đẹp — Fami giữ ở đây.'}
          </p>
        </article>

        <article className="kfam-media-card is-letter">
          <header className="kfam-media-head">
            <h3>Thư yêu thương</h3>
            <button type="button" className="kfam-link" onClick={openLoveOrDiary}>
              {hasLove ? 'Đọc ›' : 'Xem ›'}
            </button>
          </header>
          <div className="kfam-letter-row">
            <span className="kfam-letter-art" aria-hidden>
              <img src="/mascot/love-letter-envelope.png?v=2" alt="" />
            </span>
            <strong>
              {props.primaryParentVoice
                ? `${(props.primaryParentVoice.fromMemberName || props.parentRole).trim()} vừa gửi thư cho ${props.shortName}`
                : `Chưa có thư — khi ${props.parentRole} gửi, đọc ở đây nhé`}
            </strong>
          </div>
          <button type="button" className="kfam-letter-cta" onClick={openLoveOrDiary}>
            {hasLove ? 'Đọc thư ngay ›' : 'Mở nhật ký ›'}
          </button>
        </article>

        <article className="kfam-media-card is-moments">
          <header className="kfam-media-head">
            <h3>Khoảnh khắc đáng nhớ</h3>
            <button type="button" className="kfam-link" onClick={props.onOpenMemories}>
              Xem thêm ›
            </button>
          </header>
          {photoTiles.length > 0 ? (
            <button type="button" className="kfam-moments-grid" onClick={props.onOpenMemories}>
              {photoTiles.map((m) => (
                <span key={m.id} className="kfam-moment">
                  <img src={m.imageUrl!} alt="" />
                </span>
              ))}
            </button>
          ) : (
            <button
              type="button"
              className="kfam-moments-grid is-empty"
              onClick={props.onOpenMemories}
            >
              <span className="kfam-moment is-empty">📷</span>
              <span className="kfam-moment is-empty">✨</span>
              <em>Chưa có khoảnh khắc ảnh</em>
            </button>
          )}
        </article>
      </div>

      <div className="kfam-stats-rail" aria-label="Điều đáng tự hào">
        <section className="kfam-stat-card is-pride">
          <header className="kfam-media-head">
            <h3>Điều đáng tự hào hôm nay</h3>
            <button
              type="button"
              className="kfam-link"
              onClick={() => props.onOpenRewards?.() ?? props.onOpenDiary()}
            >
              Xem kho báu ›
            </button>
          </header>
          {prideList.length === 0 ? (
            <p className="kfam-empty">
              Huy hiệu, việc hoàn thành hoặc lời khen sẽ hiện ở đây khi có thật.
            </p>
          ) : (
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
          )}
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
