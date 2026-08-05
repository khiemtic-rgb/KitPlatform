import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

export type TreasureCatalogItem = {
  id: string;
  title: string;
  icon: string;
  tone: string;
  cost: number | null;
  isSpecial: boolean;
  canRedeem: boolean;
  ctaLabel: string;
};

export type TreasureBadge = {
  id: string;
  icon: string;
  label: string;
  unlocked: boolean;
  progress: number;
  hint: string;
};

export type TreasureAchievement = {
  id: string;
  icon: string;
  title: string;
  value: string;
  note: string;
};

export type TreasureMemory = {
  id: string;
  icon: string;
  title: string;
  date: string;
  imageUrl?: string | null;
  isVideo?: boolean;
};

export type TreasureStreakDay = {
  key: string;
  label: string;
  on: boolean;
  isToday: boolean;
};

export type TreasureCollection = {
  id: string;
  emoji: string;
  title: string;
  have: number;
  need: number;
  tone: 'gold' | 'green' | 'pink' | 'blue' | 'mystery';
  locked?: boolean;
  badgeIds?: string[];
};

type Props = {
  shortName: string;
  stars: number;
  remaining: number;
  streak: number;
  weekDays: TreasureStreakDay[];
  toast: string | null;
  loading: boolean;
  catalog: TreasureCatalogItem[];
  redeemBusyId: string | null;
  badges: TreasureBadge[];
  achievements: TreasureAchievement[];
  memories: TreasureMemory[];
  memoriesEmpty: string;
  hasMoreMemories: boolean;
  mysteryHave: number;
  mysteryTarget: number;
  mysteryPct: number;
  surpriseReady?: boolean;
  collections?: TreasureCollection[];
  wishSlot?: ReactNode;
  wishText?: string;
  wishStep?: number;
  wishStepTotal?: number;
  formatStars: (n: number) => string;
  onContinue: () => void;
  onRedeem: (item: TreasureCatalogItem) => void;
  onOpenAllRewards: () => void;
  onOpenAllBadges: () => void;
  onOpenAchievement: (id: string) => void;
  onOpenMemories: () => void;
  onOpenMystery: () => void;
  onOpenSurprise: () => void;
  onWishQuick?: (text: string) => void;
};

const WISH_STICKERS = [
  { emoji: '🦁', label: 'Đi sở thú', text: 'Con ước được đi sở thú cùng bố mẹ' },
  { emoji: '📚', label: 'Đọc sách cùng', text: 'Con ước bố/mẹ đọc sách cùng con' },
  { emoji: '🍦', label: 'Ăn kem', text: 'Con ước được ăn kem cùng nhà' },
  { emoji: '🎮', label: 'Chơi cùng', text: 'Con ước bố/mẹ chơi cùng con thêm' },
  { emoji: '💌', label: 'Ôm & khen', text: 'Con muốn được ôm và lời khen từ bố mẹ' },
  { emoji: '🌙', label: 'Kể chuyện', text: 'Con ước được nghe bố/mẹ kể chuyện trước khi ngủ' },
];

function categoryTag(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('sách') || t.includes('đọc')) return 'Sách';
  if (t.includes('xe') || t.includes('lego') || t.includes('đồ chơi') || t.includes('gấu'))
    return 'Đồ chơi';
  if (t.includes('tai nghe') || t.includes('balo') || t.includes('phụ kiện')) return 'Phụ kiện';
  if (t.includes('phim') || t.includes('movie') || t.includes('game')) return 'Giải trí';
  if (t.includes('kem') || t.includes('bánh') || t.includes('kẹo') || t.includes('chocolate'))
    return 'Đồ ngọt';
  return 'Quà';
}

/** Minh họa theo đúng phần thưởng — ưu tiên tên món, rồi icon catalog. */
function resolveRewardEmoji(title: string, icon: string): string {
  const t = title.toLowerCase().trim();
  const i = icon.trim();

  if (/kem|ice\s*cream|sundae|🍦|🍨|🍧/.test(t) || /🍦|🍨|🍧/.test(i)) return '🍦';
  if (/bánh|cookie|donut|🍩|🍪/.test(t) || /🍩|🍪|🧁/.test(i)) return '🧁';
  if (/kẹo|chocolate|🍬|🍫/.test(t) || /🍬|🍫/.test(i)) return '🍬';
  if (/sách|đọc|truyện|book|📚/.test(t) || /📚|📖/.test(i)) return '📚';
  if (/lego|xếp hình|🧱/.test(t) || /🧱/.test(i)) return '🧱';
  if (/xe\b|điều khiển|ô tô|🚗|🏎️/.test(t) || /🚗|🏎️|🚙/.test(i)) return '🚗';
  if (/gấu|teddy|đồ chơi|toy|🧸/.test(t) || /🧸|🪀/.test(i)) return '🧸';
  if (/game|chơi game|nintendo|playstation|🎮/.test(t) || /🎮/.test(i)) return '🎮';
  if (/phim|movie|cinema|🍿|🎬/.test(t) || /🍿|🎬/.test(i)) return '🍿';
  if (/tai nghe|headphone|🎧/.test(t) || /🎧/.test(i)) return '🎧';
  if (/balo|ba lô|backpack|🎒/.test(t) || /🎒/.test(i)) return '🎒';
  if (/pizza|🍔|🍟|đồ ăn/.test(t) || /🍕|🍔|🍟/.test(i)) return '🍕';
  if (/sữa|trà sữa|🧋|🥤/.test(t) || /🧋|🥤/.test(i)) return '🧋';
  if (/xe đạp|🚲/.test(t) || /🚲/.test(i)) return '🚲';
  if (/bóng|⚽|🏀/.test(t) || /⚽|🏀/.test(i)) return '⚽';
  if (/park|công viên|picnic|🌳/.test(t)) return '🌳';
  if (/nhạc|piano|guitar|🎵|🎸/.test(t) || /🎵|🎸|🎹/.test(i)) return '🎵';

  // Icon catalog cụ thể (không phải hộp quà chung) → giữ
  if (i && i !== '🎁' && i !== '?' && !/^🎁+$/.test(i)) return i;

  if (/bí mật|bất ngờ|mystery|hộp quà/.test(t)) return '🎁';
  if (/quà|thưởng|phần thưởng/.test(t)) return '🎁';
  return i || '🎁';
}

function rewardVisual(
  icon: string,
  title: string,
): { kind: 'img' | 'emoji'; src?: string; emoji: string } {
  const emoji = resolveRewardEmoji(title, icon);
  const t = title.toLowerCase().trim();
  // Ảnh hộp quà 3D chỉ khi đúng là quà/hộp bí mật — không đè lên kem/sách/đồ chơi…
  const useGiftArt =
    emoji === '🎁' &&
    (/^quà\b|phần thưởng|bí mật|bất ngờ|mystery|hộp quà/.test(t) || t.length === 0);
  if (useGiftArt) {
    return { kind: 'img', src: '/mascot/gift-soft.png?v=6', emoji: '🎁' };
  }
  return { kind: 'emoji', emoji };
}

function deriveCollections(badges: TreasureBadge[]): TreasureCollection[] {
  const measure = (
    ids: string[],
    emoji: string,
    title: string,
    tone: TreasureCollection['tone'],
  ): TreasureCollection => {
    const set = badges.filter((b) => ids.includes(b.id));
    const avg = set.length ? set.reduce((s, b) => s + b.progress, 0) / set.length : 0;
    const need = 30;
    return {
      id: title,
      emoji,
      title,
      have: Math.round((avg / 100) * need),
      need,
      tone,
      locked: false,
      badgeIds: ids,
    };
  };
  return [
    measure(['streak', 'garden-bloom'], '⭐', 'Chăm chỉ', 'gold'),
    measure(['stars100', 'stars500'], '📚', 'Ham học', 'green'),
    measure(['first', 'team'], '💗', 'Tốt bụng', 'pink'),
    measure(['team', 'garden-bloom'], '👟', 'Thể thao', 'blue'),
    {
      id: 'mystery',
      emoji: '🔒',
      title: 'Bí ẩn',
      have: 0,
      need: 30,
      tone: 'mystery',
      locked: true,
    },
  ];
}

export function KidTreasureHub(props: Props) {
  const [wishOpen, setWishOpen] = useState(false);
  const [wishDraw, setWishDraw] = useState(false);
  const [zoomBadge, setZoomBadge] = useState<TreasureBadge | null>(null);
  const [pickedWish, setPickedWish] = useState<string | null>(null);
  const [memPage, setMemPage] = useState(0);
  const albumRef = useRef<HTMLDivElement | null>(null);

  const wishTotal = props.wishStepTotal ?? 4;
  const wishStep = Math.min(wishTotal, Math.max(0, props.wishStep ?? (props.wishText ? 1 : 0)));
  const wishPct = wishTotal > 0 ? Math.min(100, Math.round((wishStep / wishTotal) * 100)) : 0;
  const displayWish =
    pickedWish?.trim() ||
    props.wishText?.trim() ||
    `Con ước được đi sở thú cùng gia đình`;
  const memCount = props.memories.length;

  useEffect(() => {
    const el = albumRef.current;
    if (!el) return;
    const onScroll = () => {
      const card = el.querySelector('.ktre-mem') as HTMLElement | null;
      if (!card) return;
      const styles = getComputedStyle(el);
      const gap = Number.parseFloat(styles.columnGap || styles.gap || '10') || 10;
      const step = Math.max(1, card.offsetWidth + gap);
      setMemPage(Math.min(memCount - 1, Math.max(0, Math.round(el.scrollLeft / step))));
    };
    el.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
    return () => el.removeEventListener('scroll', onScroll);
  }, [memCount]);

  const scrollAlbum = (dir: 1 | -1) => {
    const el = albumRef.current;
    if (!el) return;
    const cards = Array.from(el.querySelectorAll<HTMLElement>('.ktre-mem'));
    if (cards.length === 0) return;
    const styles = getComputedStyle(el);
    const gap = Number.parseFloat(styles.columnGap || styles.gap || '10') || 10;
    const step = Math.max(1, cards[0]!.offsetWidth + gap);
    const current = Math.round(el.scrollLeft / step);
    const next = Math.max(0, Math.min(cards.length - 1, current + dir));
    const targetLeft = cards[next]!.offsetLeft - cards[0]!.offsetLeft;
    el.scrollTo({ left: targetLeft, behavior: 'smooth' });
    setMemPage(next);
  };

  const primary = useMemo(() => {
    const priced = props.catalog.filter((c) => c.cost != null && !c.isSpecial);
    if (priced.length === 0) return props.catalog[0] ?? null;
    return [...priced].sort((a, b) => {
      const ar = Math.max(0, (a.cost ?? 0) - props.stars);
      const br = Math.max(0, (b.cost ?? 0) - props.stars);
      return ar - br;
    })[0];
  }, [props.catalog, props.stars]);

  const others = useMemo(() => {
    if (!primary) return props.catalog;
    return props.catalog.filter((c) => c.id !== primary.id);
  }, [props.catalog, primary]);

  const collections = props.collections ?? deriveCollections(props.badges);
  const unlockedBadges = props.badges.filter((b) => b.unlocked).length;
  const badgeTotal = Math.max(props.badges.length, 1);

  const primaryLeft =
    primary?.cost != null ? Math.max(0, primary.cost - props.stars) : null;
  const primaryPct =
    primary?.cost != null && primary.cost > 0
      ? Math.min(100, Math.round((props.stars / primary.cost) * 100))
      : props.mysteryPct;

  const heroLead = (() => {
    if (props.remaining > 0) {
      return (
        <>
          {props.shortName} ơi, chỉ còn <strong>{props.remaining} việc nữa</strong> thôi! Con sẽ
          mở được món quà đầu tiên 🎉
        </>
      );
    }
    if (primary && primaryLeft != null && primaryLeft > 0) {
      return (
        <>
          {props.shortName} chỉ còn <strong>{primaryLeft}⭐</strong> nữa là mở được{' '}
          <strong>{primary.title}</strong>!
        </>
      );
    }
    if (primary && primaryLeft === 0) {
      return (
        <>
          Tuyệt! <strong>{props.shortName}</strong> đủ sao đổi <strong>{primary.title}</strong> rồi
          🎁
        </>
      );
    }
    return (
      <>
        {props.shortName} giữ nhịp đẹp lắm — kho báu đang chờ mở ✨
      </>
    );
  })();

  const mysteryReady = props.mysteryPct >= 100 || props.stars >= props.mysteryTarget;
  const surpriseReady =
    props.surpriseReady ?? (props.remaining === 0 || props.streak >= 2);

  const heroAch = props.achievements[0] ?? null;
  const sideAchs = props.achievements.slice(1, 3);

  const onPickWish = (text: string) => {
    setPickedWish(text);
    props.onWishQuick?.(text);
    setWishOpen(true);
  };

  const primaryVisual = primary ? rewardVisual(primary.icon, primary.title) : null;

  return (
    <div className="ktre">
      {props.toast ? (
        <div className="ktre-toast" role="status">
          {props.toast}
        </div>
      ) : null}

      {/* 1. Hero — theo mẫu: Fami+bubble trái · chuỗi+đảo phải · CTA full */}
      <article className="ktre-hero">
        <div className="ktre-hero-top">
          <div className="ktre-hero-say">
            <div className="ktre-fami" aria-hidden>
              <img
                src="/mascot/fami-robot.png?v=5"
                alt=""
                width={128}
                height={128}
                decoding="async"
              />
            </div>
            <div className="ktre-bubble">
              <em>Fami nói:</em>
              <p>{heroLead}</p>
            </div>
          </div>

          <aside className="ktre-hero-stage">
            <div className="ktre-streak">
              <em>Chuỗi ngày tuyệt vời</em>
              <strong>
                <span aria-hidden>🔥</span> {props.streak} ngày
              </strong>
              <div className="ktre-week" aria-label={`Chuỗi ${props.streak} ngày`}>
                {props.weekDays.map((d, i) => (
                  <span key={d.key} className={d.on ? 'is-on' : undefined} title={d.label}>
                    {d.on ? '✓' : i + 1}
                  </span>
                ))}
              </div>
            </div>

            <div className="ktre-island" aria-hidden>
              <img
                className="ktre-island-art"
                src="/mascot/treasure-island.png?v=4"
                alt=""
                width={180}
                height={180}
                decoding="async"
              />
              <span className="ktre-island-stars">{props.formatStars(props.stars)}⭐</span>
            </div>
          </aside>
        </div>

        <button type="button" className="ktre-cta" onClick={props.onContinue}>
          Tiếp tục hành trình ›
        </button>
      </article>

      {/* 2–3. Mục tiêu + kho báu khác — 2 cột theo mẫu */}
      <div className="ktre-mid">
        <section className="ktre-sec ktre-sec-goal">
          <header className="ktre-sec-head">
            <h2>
              <span aria-hidden>🎯</span> Mục tiêu hiện tại
            </h2>
          </header>
          {props.loading && !primary ? (
            <p className="ktre-empty">Đang tải quà…</p>
          ) : primary ? (
            <article className={`ktre-goal tone-${primary.tone}`}>
              <div className="ktre-goal-art" aria-hidden>
                <div className="ktre-goal-orb">
                  {primaryVisual?.kind === 'img' ? (
                    <img src={primaryVisual.src} alt="" width={88} height={88} decoding="async" />
                  ) : (
                    <span>{primaryVisual?.emoji ?? primary.icon}</span>
                  )}
                </div>
                <i className="ktre-spark is-a">✨</i>
                <i className="ktre-spark is-b">⭐</i>
                <em>{categoryTag(primary.title)}</em>
              </div>
              <div className="ktre-goal-body">
                <strong className="ktre-goal-name">{primary.title}</strong>
                <span className="ktre-goal-cost">
                  {primary.cost == null ? (
                    'Phần bố mẹ giữ'
                  ) : (
                    <>
                      {primary.cost} Sao <span aria-hidden>⭐</span>
                    </>
                  )}
                </span>
                <p className="ktre-goal-cheer">
                  {primaryLeft == null
                    ? 'Nhờ bố mẹ giữ giúp'
                    : primaryLeft === 0
                      ? 'Đủ sao rồi — đổi ngay!'
                      : `Con còn ${primaryLeft} ⭐ nữa nhé!`}
                </p>
                <div className="ktre-goal-progress">
                  <div className="ktre-bar" aria-hidden>
                    <b style={{ width: `${primaryPct}%` }} />
                  </div>
                  <span>
                    {props.formatStars(Math.min(props.stars, primary.cost ?? props.stars))} /{' '}
                    {primary.cost == null ? '???' : props.formatStars(primary.cost)}
                  </span>
                </div>
                <button
                  type="button"
                  className="ktre-goal-cta"
                  disabled={
                    props.redeemBusyId === primary.id ||
                    (!primary.canRedeem && !primary.isSpecial)
                  }
                  onClick={() => props.onRedeem(primary)}
                >
                  {props.redeemBusyId === primary.id
                    ? 'Đang đổi…'
                    : primaryLeft === 0
                      ? 'Đổi ngay'
                      : 'Tiếp tục'}
                </button>
              </div>
            </article>
          ) : (
            <p className="ktre-empty">Bố mẹ chưa gắn kho báu — hỏi bố mẹ nhé!</p>
          )}
        </section>

        <section className="ktre-sec ktre-sec-others">
          <header className="ktre-sec-head">
            <h2>
              <span aria-hidden>🧰</span> Những kho báu khác
            </h2>
            <button type="button" className="ktre-link" onClick={props.onOpenAllRewards}>
              Xem tất cả
            </button>
          </header>
          {others.length === 0 ? (
            <p className="ktre-empty">Chưa có kho báu khác.</p>
          ) : (
            <div className="ktre-hscroll">
              {others.map((item) => {
                const vis = rewardVisual(item.icon, item.title);
                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`ktre-mini tone-${item.tone}`}
                    disabled={props.redeemBusyId === item.id}
                    onClick={() => props.onRedeem(item)}
                  >
                    <span className="ktre-mini-art" aria-hidden>
                      <span className="ktre-mini-orb">
                        {vis.kind === 'img' ? (
                          <img src={vis.src} alt="" width={56} height={56} decoding="async" />
                        ) : (
                          <span>{vis.emoji}</span>
                        )}
                      </span>
                    </span>
                    <strong>{item.title}</strong>
                    <em>{item.cost == null ? '??? Sao' : `${item.cost} Sao`}</em>
                  </button>
                );
              })}
            </div>
          )}
        </section>
      </div>

      {/* 4. Bí mật + Surprise — theo mẫu sinh động */}
      <div className="ktre-promo">
        <article className={`ktre-promo-card is-mystery${mysteryReady ? ' is-ready' : ''}`}>
          <header className="ktre-promo-head">
            <span className="ktre-promo-badge is-q" aria-hidden>
              ?
            </span>
            <em>Kho báu bí mật</em>
          </header>
          <div className="ktre-promo-body is-mystery-layout">
            <div className="ktre-promo-art" aria-hidden>
              <img src="/mascot/mystery-chest.png?v=2" alt="" width={120} height={120} decoding="async" />
            </div>
            <div className="ktre-promo-copy">
              <strong>Hộp quà bí mật</strong>
              <p>Hộp quà bí mật đang chờ {props.shortName}!</p>
              <button type="button" className="ktre-promo-cta is-split" onClick={props.onOpenMystery}>
                <span>{mysteryReady ? 'Mở ngay' : 'Sắp mở'}</span>
                <em aria-hidden>⭐ 50 ⭐</em>
              </button>
            </div>
          </div>
        </article>

        <article className={`ktre-promo-card is-surprise${surpriseReady ? ' is-ready' : ''}`}>
          <header className="ktre-promo-head">
            <span className="ktre-promo-badge is-gift" aria-hidden>
              🎁
            </span>
            <em>Quà bí mật</em>
          </header>
          <div className="ktre-promo-body is-surprise-layout">
            <div className="ktre-promo-copy">
              <p>Fami có một món quà đang chờ {props.shortName} nè!</p>
              <button type="button" className="ktre-promo-cta" onClick={props.onOpenSurprise}>
                Xem ngay
              </button>
            </div>
            <div className="ktre-promo-art" aria-hidden>
              <img
                src="/mascot/fami-surprise-gift.png?v=2"
                alt=""
                width={120}
                height={120}
                decoding="async"
              />
            </div>
          </div>
        </article>
      </div>

      {/* 5–6. Bộ sưu tập huy hiệu + Thành tựu — theo mẫu */}
      <div className="ktre-glory">
        <section className="ktre-sec ktre-sec-badges">
          <header className="ktre-sec-head">
            <h2>
              <span className="ktre-gem" aria-hidden>
                💎
              </span>{' '}
              Bộ sưu tập huy hiệu
            </h2>
            <button type="button" className="ktre-link" onClick={props.onOpenAllBadges}>
              Xem tất cả
            </button>
          </header>

          <div className="ktre-hex-row" role="list">
            {collections.map((c) => {
              const locked = Boolean(c.locked);
              return (
                <button
                  key={c.id}
                  type="button"
                  role="listitem"
                  className={`ktre-hex tone-${c.tone}${locked ? ' is-lock' : ''}`}
                  aria-label={locked ? 'Huy hiệu bí ẩn' : c.title}
                  onClick={() => {
                    if (locked) {
                      setZoomBadge({
                        id: 'mystery',
                        icon: '🔒',
                        label: 'Huy hiệu bí ẩn',
                        unlocked: false,
                        progress: 0,
                        hint: 'Làm thêm việc tốt — Fami sẽ mở khoá sau!',
                      });
                      return;
                    }
                    const ids = c.badgeIds ?? [];
                    const hit =
                      props.badges.find((b) => ids.includes(b.id) && b.unlocked) ??
                      props.badges.find((b) => ids.includes(b.id)) ??
                      null;
                    if (hit) setZoomBadge(hit);
                    else props.onOpenAllBadges();
                  }}
                >
                  <span className="ktre-hex-medal" aria-hidden>
                    {locked ? '🔒' : c.emoji}
                  </span>
                  <strong>{c.title}</strong>
                  <em>{locked ? '???' : `${c.have}/${c.need}`}</em>
                </button>
              );
            })}
          </div>

          <div className="ktre-badge-foot">
            <div className="ktre-badge-prog">
              <span aria-hidden>⭐</span>
              <div className="ktre-bar" aria-hidden>
                <b
                  style={{
                    width: `${Math.min(100, Math.round((unlockedBadges / badgeTotal) * 100))}%`,
                  }}
                />
              </div>
            </div>
            <em>
              {unlockedBadges} / {badgeTotal} huy hiệu đã mở khóa
            </em>
          </div>
        </section>

        <section className="ktre-sec ktre-sec-ach">
          <header className="ktre-sec-head">
            <h2>
              <span aria-hidden>🎁</span> Thành tựu đáng tự hào
            </h2>
            {props.achievements.length > 0 ? (
              <button
                type="button"
                className="ktre-link"
                onClick={() => props.onOpenAchievement(props.achievements[0]!.id)}
              >
                Xem tất cả
              </button>
            ) : null}
          </header>

          {!heroAch ? (
            <p className="ktre-empty">
              Khi con tự giác hoàn thành việc đầu tiên, Fami sẽ treo kỷ niệm ở đây.
            </p>
          ) : (
            <div className="ktre-ach-grid">
              <button
                type="button"
                className="ktre-ach is-hero"
                onClick={() => props.onOpenAchievement(heroAch.id)}
              >
                <span className="ktre-ach-scene" aria-hidden>
                  <i>{heroAch.icon || '🌱'}</i>
                </span>
                <div className="ktre-ach-hero-copy">
                  <strong>{heroAch.title}</strong>
                  <em>
                    {/\d{1,2}\/\d{1,2}/.test(heroAch.note)
                      ? `Ngày đạt được: ${heroAch.note}`
                      : heroAch.note || heroAch.value}
                  </em>
                </div>
              </button>
              <div className="ktre-ach-side">
                {sideAchs.length === 0 ? (
                  <div className="ktre-ach is-soft">
                    <span aria-hidden>🌱</span>
                    <div>
                      <strong>Khoảnh khắc tiếp theo</strong>
                      <em>Giữ nhịp thêm vài ngày nữa nhé!</em>
                    </div>
                  </div>
                ) : (
                  sideAchs.map((a) => (
                    <button
                      key={a.id}
                      type="button"
                      className="ktre-ach"
                      onClick={() => props.onOpenAchievement(a.id)}
                    >
                      <span aria-hidden>{a.icon}</span>
                      <div>
                        <strong>{a.title}</strong>
                        <em>{a.note || a.value}</em>
                      </div>
                    </button>
                  ))
                )}
              </div>
            </div>
          )}
        </section>
      </div>

      {/* 8–9. Wish Box + Kỷ niệm — hai panel cân đối theo mẫu */}
      <div className="ktre-foot">
        <section className="ktre-sec ktre-sec-wish ktre-panel">
          <header className="ktre-sec-head">
            <h2>
              <span className="ktre-wish-mark" aria-hidden>
                🐷
              </span>
              Hộp ước nguyện
            </h2>
            <button
              type="button"
              className="ktre-link"
              onClick={() => {
                setWishDraw(false);
                setWishOpen(true);
              }}
            >
              Xem tất cả
            </button>
          </header>

          <div className="ktre-panel-body">
            <article className="ktre-wish-card">
              <button type="button" className="ktre-wish-heart" aria-label="Yêu thích ước mơ">
                ♥
              </button>
              <div className="ktre-wish-env" aria-hidden>
                <span>💌</span>
              </div>
              <div className="ktre-wish-body">
                <p>{displayWish}</p>
                <em>
                  {wishStep}/{wishTotal} bước đã hoàn thành
                </em>
                <div className="ktre-wish-bar" aria-hidden>
                  <b style={{ width: `${wishPct}%` }} />
                </div>
              </div>
            </article>
          </div>

          <div className="ktre-wish-acts" aria-label="Gửi ước mơ">
            <button
              type="button"
              className="tone-write"
              onClick={() => {
                setWishDraw(false);
                setWishOpen(true);
              }}
            >
              <i aria-hidden>✏️</i>
              <em>Viết ước mơ</em>
            </button>
            <button
              type="button"
              className="tone-draw"
              onClick={() => {
                setWishOpen(false);
                setWishDraw((v) => !v);
              }}
            >
              <i aria-hidden>🖌️</i>
              <em>Vẽ ước mơ</em>
            </button>
            <button
              type="button"
              className="tone-voice"
              onClick={() => {
                setWishDraw(false);
                setWishOpen(true);
              }}
            >
              <i aria-hidden>🎙️</i>
              <em>Gửi voice</em>
            </button>
            <button
              type="button"
              className="tone-view"
              onClick={() => {
                setWishDraw(false);
                setWishOpen(true);
              }}
            >
              <i aria-hidden>💝</i>
              <em>Xem ước mơ</em>
            </button>
          </div>

          {wishDraw ? (
            <div className="ktre-wish-stickers" aria-label="Chọn sticker ước muốn">
              {WISH_STICKERS.map((s) => (
                <button
                  key={s.label}
                  type="button"
                  className={pickedWish === s.text ? 'is-on' : undefined}
                  onClick={() => {
                    onPickWish(s.text);
                    setWishDraw(false);
                  }}
                >
                  <span aria-hidden>{s.emoji}</span>
                  <em>{s.label}</em>
                </button>
              ))}
            </div>
          ) : null}

          {wishOpen && props.wishSlot ? (
            <div className="ktre-wish-slot">{props.wishSlot}</div>
          ) : null}
        </section>

        <section className="ktre-sec ktre-sec-mem ktre-panel">
          <header className="ktre-sec-head">
            <h2>
              <span className="ktre-mem-mark" aria-hidden>
                🧰
              </span>
              Kỷ niệm đáng nhớ
            </h2>
            {props.hasMoreMemories || props.memories.length > 0 ? (
              <button type="button" className="ktre-link" onClick={props.onOpenMemories}>
                Xem tất cả
              </button>
            ) : null}
          </header>

          {props.memories.length === 0 ? (
            <p className="ktre-empty ktre-panel-body">{props.memoriesEmpty}</p>
          ) : (
            <div className="ktre-album-wrap">
              {memCount > 1 ? (
                <button
                  type="button"
                  className="ktre-album-nav is-prev"
                  aria-label="Kỷ niệm trước"
                  disabled={memPage <= 0}
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    scrollAlbum(-1);
                  }}
                >
                  ‹
                </button>
              ) : null}
              <div className="ktre-album" ref={albumRef}>
                {props.memories.map((m, i) => {
                  const video = m.isVideo ?? i === 1;
                  return (
                    <button
                      key={m.id}
                      type="button"
                      className="ktre-mem"
                      onClick={props.onOpenMemories}
                    >
                      <div className="ktre-mem-frame">
                        {m.imageUrl ? (
                          <img src={m.imageUrl} alt="" decoding="async" />
                        ) : (
                          <span aria-hidden>{m.icon}</span>
                        )}
                        {video ? (
                          <i className="ktre-mem-play" aria-hidden>
                            ▶
                          </i>
                        ) : null}
                      </div>
                      <div className="ktre-mem-meta">
                        <strong>{m.title}</strong>
                        <em>{m.date}</em>
                      </div>
                    </button>
                  );
                })}
              </div>
              {memCount > 1 ? (
                <button
                  type="button"
                  className="ktre-album-nav is-next"
                  aria-label="Xem kỷ niệm tiếp"
                  disabled={memPage >= memCount - 1}
                  onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    scrollAlbum(1);
                  }}
                >
                  ›
                </button>
              ) : null}
              {memCount > 1 ? (
                <div className="ktre-album-dots" aria-hidden>
                  {props.memories.slice(0, 6).map((m, i) => (
                    <i key={m.id} className={i === memPage ? 'is-on' : undefined} />
                  ))}
                </div>
              ) : null}
            </div>
          )}
        </section>
      </div>

      {zoomBadge ? (
        <div
          className="ktre-zoom"
          role="dialog"
          aria-modal="true"
          aria-label={zoomBadge.label}
          onClick={() => setZoomBadge(null)}
        >
          <div className="ktre-zoom-card" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              className="ktre-zoom-close"
              aria-label="Đóng"
              onClick={() => setZoomBadge(null)}
            >
              ×
            </button>
            <span className={`ktre-zoom-medal${zoomBadge.unlocked ? ' is-on' : ''}`} aria-hidden>
              {zoomBadge.unlocked ? zoomBadge.icon : '🔒'}
            </span>
            <strong>{zoomBadge.unlocked ? zoomBadge.label : 'Huy hiệu bí ẩn'}</strong>
            <p>{zoomBadge.hint}</p>
            <div className="ktre-bar" aria-hidden>
              <b style={{ width: `${Math.min(100, zoomBadge.progress)}%` }} />
            </div>
            <em>{Math.round(zoomBadge.progress)}% hành trình</em>
            <button type="button" className="ktre-cta" onClick={props.onOpenAllBadges}>
              Xem bộ sưu tập ›
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
