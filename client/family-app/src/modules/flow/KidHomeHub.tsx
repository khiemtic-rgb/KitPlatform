import { useEffect, useMemo, useState } from 'react';
import type { DayFlowCommitment, ParentVoiceMessage } from '@/shared/api/family-os.api';

type Props = {
  shortName: string;
  parentRole: string;
  remaining: number;
  doneCount: number;
  total: number;
  unlockLeft: number;
  explorerLevel: number;
  streak: number;
  levelProgress: { have: number; need: number };
  badgeUnlocked: boolean;
  nextMission: DayFlowCommitment | null;
  todayItems: DayFlowCommitment[];
  busyId: string | null;
  uploading: boolean;
  missionDoneError: string | null;
  primaryParentVoice: ParentVoiceMessage | null;
  thanksSent: boolean;
  thanksSending: boolean;
  foxyLine: string;
  taskIcon: (title: string) => string;
  durationLabel: (item: DayFlowCommitment) => string | null;
  clockLabel: (item: DayFlowCommitment) => string;
  starRewardOf: (item: DayFlowCommitment) => number;
  studyNeedsEvidence: (item: DayFlowCommitment) => boolean;
  onStartNow: () => void;
  onStartNext: () => void;
  onOpenAllToday: () => void;
  onStartItem: (item: DayFlowCommitment) => void;
  onOpenAchievements: () => void;
  onSendSticker: (emoji: string) => void;
  onAckVoiceThanks: () => void;
  onOpenMoments: () => void;
  onOpenSurprise: () => void;
  momentPreview: {
    title: string;
    body: string;
    imageUrl: string | null;
  } | null;
};

const STICKERS = ['❤️', '👍', '🥰', '🎉'] as const;

type RowMark = 'done' | 'next' | 'todo' | 'wait';

function markOf(item: DayFlowCommitment, nextId: string | null): RowMark {
  if (item.status === 'done') {
    if (item.starPosted === false || item.evidenceSatisfied === false) return 'wait';
    return 'done';
  }
  if (item.status === 'skipped') return 'todo';
  if (nextId && item.id === nextId) return 'next';
  return 'todo';
}

function markGlyph(m: RowMark): string {
  if (m === 'done') return '✓';
  if (m === 'next') return '';
  if (m === 'wait') return '…';
  return '';
}

function statusBadge(m: RowMark): { label: string; tone: string } {
  if (m === 'done') return { label: 'Đã xong 😊', tone: 'done' };
  if (m === 'next') return { label: 'Tiếp theo', tone: 'next' };
  if (m === 'wait') return { label: 'Chờ xác nhận', tone: 'wait' };
  return { label: 'Chưa làm', tone: 'todo' };
}

function itemClock(item: DayFlowCommitment): string {
  if (item.windowStart) return item.windowStart.slice(0, 5);
  if (item.windowEnd) return item.windowEnd.slice(0, 5);
  return '';
}

function nextCheerLine(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('rang')) return 'Giữ nụ cười thật tươi nhé!';
  if (t.includes('rửa mặt') || t.includes('rửa tay')) return 'Sạch sẽ là siêu năng lực nhỏ!';
  if (t.includes('tắm')) return 'Tắm xong là sảng khoái liền!';
  if (t.includes('ăn sáng')) return 'Nạp năng lượng cho một ngày vui!';
  if (t.includes('ăn') || t.includes('cơm') || t.includes('bữa')) return 'Ăn ngon để khỏe và chăm chỉ nhé!';
  if (t.includes('sữa')) return 'Uống sữa để cao lớn nào!';
  if (t.includes('bài tập') || t.includes('học')) return 'Từ từ thôi — Fami tin con làm được!';
  if (t.includes('đọc') || t.includes('sách')) return 'Mỗi trang sách là một cuộc phiêu lưu!';
  if (t.includes('ngủ')) return 'Ngủ ngon để mơ đẹp với Fami!';
  if (t.includes('dọn') || t.includes('gấp') || t.includes('phòng')) return 'Phòng gọn là con chủ động rồi đó!';
  if (t.includes('đi học') || t.includes('cặp') || t.includes('balo')) return 'Chuẩn bị xong là sẵn sàng chinh phục!';
  if (t.includes('thể dục') || t.includes('chạy') || t.includes('bơi')) return 'Vận động một chút cho thật khỏe!';
  if (t.includes('giúp')) return 'Giúp nhà là hành động siêu cool!';
  return 'Cùng Fami làm xong việc này nhé!';
}

function nextIconTone(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('đánh răng') || t.includes('rang') || t.includes('rửa') || t.includes('tắm')) return 'sky';
  if (t.includes('ăn') || t.includes('cơm') || t.includes('sữa') || t.includes('bữa')) return 'peach';
  if (t.includes('học') || t.includes('bài') || t.includes('đọc') || t.includes('sách')) return 'lemon';
  if (t.includes('ngủ')) return 'indigo';
  if (t.includes('dọn') || t.includes('giúp') || t.includes('tưới')) return 'mint';
  return 'mint';
}

export function KidHomeHub(props: Props) {
  const nextId = props.nextMission?.id ?? null;
  const pct = Math.max(
    0,
    Math.min(100, Math.round((props.levelProgress.have / Math.max(props.levelProgress.need, 1)) * 100)),
  );
  const giftLeft = props.remaining > 0 ? props.remaining : Math.max(0, props.unlockLeft);
  const [dayTip, setDayTip] = useState<string | null>(null);

  useEffect(() => {
    if (!dayTip) return;
    const t = window.setTimeout(() => setDayTip(null), 2200);
    return () => window.clearTimeout(t);
  }, [dayTip]);

  const heroLine =
    props.remaining > 0 ? (
      <>
        Hôm nay chúng mình cùng cố gắng nhé!
        <br />
        Chỉ còn <strong>{props.remaining} việc</strong> nữa là hoàn thành một ngày tuyệt vời! ☀️
      </>
    ) : props.total > 0 ? (
      <>Hôm nay {props.shortName} đã giữ nhịp xong rồi — Fami tự hào lắm! ☀️</>
    ) : (
      props.foxyLine
    );

  /** One “Fami đang chờ” reason only — why kids open the app. */
  const waitMode = useMemo(() => {
    if (props.primaryParentVoice) return 'praise' as const;
    if (props.doneCount > 0 && !props.thanksSent) return 'sticker' as const;
    return 'idle' as const;
  }, [props.primaryParentVoice, props.doneCount, props.thanksSent]);

  const list = props.todayItems.slice(0, 5);
  const showSurprise = props.remaining === 0 || props.unlockLeft === 0 || props.doneCount >= 2;
  const nextStars = props.nextMission ? props.starRewardOf(props.nextMission) : 0;
  const rowBusy = Boolean(props.busyId || props.uploading);
  const streakGoal = 5;
  const streakLit = Math.min(streakGoal, Math.max(0, props.streak));
  const levelPct = pct;
  const nextLevel = Math.min(12, props.explorerLevel + 1);

  const onTodayRow = (item: DayFlowCommitment, m: RowMark) => {
    if (m === 'done') {
      setDayTip('Việc này con đã xong rồi! 🌟');
      return;
    }
    if (m === 'wait') {
      setDayTip('Đã nộp — chờ bố mẹ xác nhận sao nhé!');
      return;
    }
    if (rowBusy) return;
    props.onStartItem(item);
  };

  return (
    <div className="khub">
      {/* Hero — Fami | bubble | level+quà | CTA */}
      <article className="khub-hero">
        <div className="khub-hero-main">
          <div className="khub-welcome" aria-hidden>
            <img
              className="khub-welcome-img"
              src="/mascot/fami-robot.png?v=4"
              alt=""
              width={180}
              height={180}
              decoding="async"
            />
          </div>

          <div className="khub-hero-mid">
            <p className="khub-bubble">{heroLine}</p>
          </div>

          <div className="khub-hero-side">
            <div className="khub-level">
              <strong>Level {props.explorerLevel} ⭐</strong>
              <div className="khub-bar" aria-hidden>
                <b style={{ width: `${pct}%` }} />
              </div>
              <em>
                {props.levelProgress.have} / {props.levelProgress.need}
              </em>
            </div>
            <button type="button" className="khub-gift-chip" onClick={props.onOpenSurprise}>
              <span className="khub-gift-art" aria-hidden>
                <img src="/mascot/gift-soft.png?v=4" alt="" width={72} height={72} decoding="async" />
              </span>
              <span className="khub-gift-copy">
                {giftLeft > 0 ? (
                  <>
                    Chỉ còn <strong>{giftLeft} việc</strong> nữa để mở{' '}
                    <strong>quà hôm nay</strong> 🎁
                  </>
                ) : (
                  <>Quà hôm nay đã sẵn sàng — chạm để mở! 🎁</>
                )}
              </span>
            </button>
          </div>
        </div>

        <button type="button" className="khub-cta" onClick={props.onStartNow}>
          Bắt đầu ngay 🚀
        </button>
      </article>

      {/* Việc tiếp theo — nhất */}
      <section className="khub-next" aria-label="Việc tiếp theo">
        <div className="khub-next-head">
          <span className="khub-next-badge">
            <span aria-hidden>⭐</span> Việc tiếp theo
          </span>
          {props.nextMission && nextStars > 0 ? (
            <span className="khub-next-stars" title="Sao thưởng khi hoàn thành đúng">
              <span aria-hidden>⭐</span> +{nextStars} sao
            </span>
          ) : null}
        </div>

        {props.nextMission ? (
          <div className="khub-next-row">
            <span
              className={`khub-next-ico tone-${nextIconTone(props.nextMission.title)}`}
              aria-hidden
            >
              {props.taskIcon(props.nextMission.title)}
            </span>
            <div className="khub-next-body">
              <strong>{props.nextMission.title}</strong>
              <p className="khub-next-cheer">{nextCheerLine(props.nextMission.title)}</p>
              <span className="khub-next-time">
                <span aria-hidden>🕒</span>{' '}
                {props.durationLabel(props.nextMission) ??
                  props.clockLabel(props.nextMission)}
              </span>
            </div>
            <button
              type="button"
              className="khub-start"
              disabled={props.busyId === props.nextMission.id || props.uploading}
              onClick={props.onStartNext}
            >
              {props.busyId === props.nextMission.id
                ? '…'
                : props.studyNeedsEvidence(props.nextMission)
                  ? '📷 Ảnh'
                  : (
                      <>
                        <span aria-hidden>▶</span> Bắt đầu
                      </>
                    )}
            </button>
          </div>
        ) : (
          <div className="khub-next-row is-done">
            <span className="khub-next-ico tone-lemon" aria-hidden>
              🏆
            </span>
            <div className="khub-next-body">
              <strong>Xong hết phần của con rồi!</strong>
              <p className="khub-next-cheer">
                Fami đang chờ lời cảm ơn hoặc lời khen từ nhà.
              </p>
            </div>
          </div>
        )}
        {props.missionDoneError ? (
          <p className="khub-error" role="alert">
            {props.missionDoneError}
          </p>
        ) : null}
      </section>

      {/* Hôm nay — vertical list */}
      <section className="khub-day" aria-label="Tiến trình hôm nay">
        <div className="khub-day-head">
          <h2 className="khub-day-title">
            <span aria-hidden>🌞</span> Hôm nay
          </h2>
          <button type="button" className="khub-day-all" onClick={props.onOpenAllToday}>
            Xem tất cả &gt;
          </button>
        </div>
        {list.length === 0 ? (
          <p className="khub-empty">Chưa có việc trong ngày.</p>
        ) : (
          <ul className="khub-list">
            {list.map((item) => {
              const m = markOf(item, nextId);
              const badge = statusBadge(m);
              const clock = itemClock(item);
              const stars =
                m === 'done' ? (item.starDelta ?? 0) : props.starRewardOf(item);
              const actionable = m === 'next' || m === 'todo';
              const busyThis = props.busyId === item.id || (rowBusy && actionable);
              return (
                <li key={item.id} className={`khub-li-wrap is-${m}`}>
                  <button
                    type="button"
                    className={`khub-li is-${m}${actionable ? ' is-action' : ' is-idle'}`}
                    disabled={busyThis && actionable}
                    onClick={() => onTodayRow(item, m)}
                    aria-label={
                      actionable
                        ? `${m === 'next' ? 'Bắt đầu' : 'Làm'} ${item.title}`
                        : m === 'wait'
                          ? `${item.title} — chờ xác nhận`
                          : `${item.title} — đã xong`
                    }
                  >
                    <span className="khub-dot" aria-hidden>
                      {markGlyph(m)}
                    </span>
                    <span
                      className={`khub-li-ico tone-${nextIconTone(item.title)}`}
                      aria-hidden
                    >
                      {props.taskIcon(item.title)}
                    </span>
                    <div className="khub-li-copy">
                      <strong>{item.title}</strong>
                      <em>
                        {clock ||
                          props.durationLabel(item) ||
                          props.clockLabel(item)}
                        {stars > 0 && m !== 'done' ? (
                          <span className="khub-li-stars"> · +{stars}⭐</span>
                        ) : null}
                        {m === 'done' && stars > 0 ? (
                          <span className="khub-li-stars is-earned"> · +{stars}⭐</span>
                        ) : null}
                      </em>
                    </div>
                    <span className={`khub-badge tone-${badge.tone}`}>{badge.label}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        )}
        {dayTip ? (
          <p className="khub-day-tip" role="status">
            {dayTip}
          </p>
        ) : null}
      </section>

      {/* Fami đang chờ — khích lệ + sticker */}
      <aside className={`khub-wait is-${waitMode}`} aria-label="Fami đang chờ con">
        <div className="khub-wait-fami" aria-hidden>
          <img
            className="khub-wait-fami-img"
            src="/mascot/fami-robot.png?v=4"
            alt=""
            width={96}
            height={96}
            decoding="async"
          />
          <span className="khub-wait-heart">❤️</span>
          <span className="khub-wait-spark a">✨</span>
          <span className="khub-wait-spark b">💫</span>
        </div>

        <div className="khub-wait-main">
          <strong className="khub-wait-title">Fami đang chờ con nè!</strong>

          {waitMode === 'praise' && props.primaryParentVoice ? (
            <>
              <p className="khub-wait-line">
                {(props.primaryParentVoice.fromMemberName || props.parentRole).trim()} vừa gửi
                lời khen ấm áp 💚
              </p>
              <p className="khub-wait-ask">Mở ra nghe ngay nhé!</p>
              <button type="button" className="khub-wait-cta" onClick={props.onAckVoiceThanks}>
                👂 Đọc ngay
              </button>
            </>
          ) : waitMode === 'sticker' ? (
            <>
              <p className="khub-wait-line">Con vừa làm rất tốt 💚</p>
              <p className="khub-wait-ask">
                Con muốn gửi {props.parentRole} một sticker không?
              </p>
              <div className="khub-sticker-row" role="group" aria-label="Chọn sticker">
                {STICKERS.map((s, i) => (
                  <button
                    key={s}
                    type="button"
                    className="khub-sticker"
                    style={{ animationDelay: `${i * 0.12}s` }}
                    disabled={props.thanksSending}
                    onClick={() => props.onSendSticker(s)}
                    aria-label={`Gửi sticker ${s}`}
                  >
                    {s}
                  </button>
                ))}
              </div>
              {props.thanksSending ? (
                <p className="khub-wait-sending" role="status">
                  Đang gửi tới {props.parentRole}…
                </p>
              ) : null}
            </>
          ) : (
            <>
              <p className="khub-wait-line">Fami luôn ở cạnh con đó!</p>
              <p className="khub-wait-ask">
                Làm việc tiếp theo — rồi mình kể cho {props.parentRole} nghe nhé.
              </p>
              <button type="button" className="khub-wait-cta is-go" onClick={props.onStartNow}>
                ▶ Cùng làm nào
              </button>
            </>
          )}
        </div>
      </aside>

      {/* Thành tích của con */}
      <section className="khub-ach" aria-label="Thành tích của con">
        <div className="khub-ach-head">
          <h2 className="khub-ach-title">
            <span aria-hidden>🏆</span> Thành tích của con
          </h2>
          <button type="button" className="khub-ach-all" onClick={props.onOpenAchievements}>
            Xem tất cả &gt;
          </button>
        </div>

        <div className="khub-ach-grid">
          <article className="khub-ach-card tone-fire">
            <span className="khub-ach-ico" aria-hidden>
              🔥
            </span>
            <p className="khub-ach-label">
              {props.streak > 0
                ? `${props.streak} ngày liên tiếp hoàn thành việc`
                : 'Bắt đầu chuỗi ngày tự giác nhé!'}
            </p>
            <div className="khub-ach-flames" aria-hidden>
              {Array.from({ length: streakGoal }, (_, i) => (
                <span key={i} className={i < streakLit ? 'is-on' : 'is-off'}>
                  🔥
                </span>
              ))}
            </div>
          </article>

          <article className="khub-ach-card tone-level">
            <span className="khub-ach-shield" aria-hidden>
              {props.explorerLevel}
            </span>
            <p className="khub-ach-label">
              <strong>Level {props.explorerLevel}</strong>
              <em>Tiến gần Level {nextLevel}</em>
            </p>
            <div className="khub-ach-bar" aria-hidden>
              <b style={{ width: `${levelPct}%` }} />
            </div>
          </article>

          <article className={`khub-ach-card tone-badge${props.badgeUnlocked ? ' is-won' : ''}`}>
            <span className="khub-ach-ico" aria-hidden>
              🏅
            </span>
            <p className="khub-ach-label">
              <strong>
                Huy hiệu <em className="khub-ach-name">Chăm chỉ</em>
              </strong>
              <em className={props.badgeUnlocked ? 'is-ok' : 'is-soon'}>
                {props.badgeUnlocked ? 'Đã đạt được' : 'Sắp mở khóa'}
              </em>
            </p>
          </article>
        </div>
      </section>

      {/* Khoảnh khắc hôm nay */}
      <section className="khub-moment" aria-label="Khoảnh khắc hôm nay">
        <div className="khub-moment-head">
          <h2 className="khub-moment-title">
            <span aria-hidden>❤️</span> Khoảnh khắc hôm nay
          </h2>
          <button type="button" className="khub-moment-all" onClick={props.onOpenMoments}>
            Xem tất cả &gt;
          </button>
        </div>

        {props.momentPreview ? (
          <article className="khub-moment-card">
            <div className="khub-moment-thumb" aria-hidden>
              {props.momentPreview.imageUrl ? (
                <img src={props.momentPreview.imageUrl} alt="" decoding="async" />
              ) : (
                <span className="khub-moment-thumb-fallback">👨‍👩‍👧</span>
              )}
            </div>
            <div className="khub-moment-copy">
              <strong>{props.momentPreview.title}</strong>
              <p>«{props.momentPreview.body}»</p>
            </div>
            <button
              type="button"
              className="khub-moment-cta"
              onClick={() => {
                if (props.primaryParentVoice) props.onAckVoiceThanks();
                else props.onOpenMoments();
              }}
            >
              <span aria-hidden>❤️</span> Xem ngay
            </button>
          </article>
        ) : (
          <article className="khub-moment-card is-empty">
            <div className="khub-moment-thumb" aria-hidden>
              <span className="khub-moment-thumb-fallback">💌</span>
            </div>
            <div className="khub-moment-copy">
              <strong>Fami đang giữ chỗ ấm áp</strong>
              <p>Khi {props.parentRole} khen con, khoảnh khắc sẽ hiện ở đây!</p>
            </div>
            <button type="button" className="khub-moment-cta" onClick={props.onOpenMoments}>
              <span aria-hidden>📷</span> Nhật ký
            </button>
          </article>
        )}
      </section>

      {/* Điều bất ngờ — không phải ngày nào cũng có */}
      {showSurprise ? (
        <button type="button" className="khub-surprise" onClick={props.onOpenSurprise}>
          <span aria-hidden>🎁</span>
          <span>
            <strong>Fami có một điều bất ngờ</strong>
            <em>Mở ra xem thử nhé</em>
          </span>
          <i>Mở</i>
        </button>
      ) : null}
    </div>
  );
}
