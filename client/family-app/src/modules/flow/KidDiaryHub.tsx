import { useMemo, useRef, type ReactNode } from 'react';
import { SoftEvidenceImg } from '@/shared/ui/SoftEvidenceImg';

const STORY_HL_RE =
  /(tự giác đánh răng|đánh răng|huy hiệu|lời yêu thương|đọc sách|giúp mẹ|giúp bố|không cần nhắc)/gi;

function highlightStory(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  let last = 0;
  let m: RegExpExecArray | null;
  const re = new RegExp(STORY_HL_RE.source, STORY_HL_RE.flags);
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) nodes.push(text.slice(last, m.index));
    nodes.push(
      <b key={`hl-${m.index}`} className="kdiary-tale-hl">
        {m[0]}
      </b>,
    );
    last = m.index + m[0].length;
  }
  if (last < text.length) nodes.push(text.slice(last));
  return nodes;
}

export type DiaryMood = {
  code: string;
  emoji: string;
  label: string;
};

export type DiaryMoment = {
  id: string;
  kind: 'photo' | 'video' | 'voice' | 'draw' | 'other';
  icon: string;
  title: string;
  time: string;
  imageUrl?: string | null;
  /** Voice / video duration label, e.g. "0:18" */
  duration?: string | null;
  starred?: boolean;
};

export type DiaryPride = {
  id: string;
  icon: string;
  title: string;
  subtitle: string;
  when: string;
};

export type DiaryLove = {
  id: string;
  fromLabel: string;
  body: string;
  when: string;
  tone: 'mom' | 'dad' | 'other';
};

export type DiaryTimelineItem = {
  id: string;
  icon: string;
  time: string;
  title: string;
  tag: string;
  done: boolean;
};

export type DiaryMemory = {
  id: string;
  icon: string;
  title: string;
  date: string;
  imageUrl?: string | null;
};

export type DiaryDayChip = {
  key: string;
  shortLabel: string;
  fullLabel: string;
  isToday: boolean;
};

type Props = {
  shortName: string;
  parentRole: string;
  dayTitle: string;
  dayLabelShort: string;
  isToday: boolean;
  loading: boolean;
  error: string | null;
  toast: string | null;
  /** Short AI / Fami journal paragraphs for the day */
  storyParagraphs: string[];
  storyPhotoUrl?: string | null;
  days: DiaryDayChip[];
  dayIdx: number;
  moments: DiaryMoment[];
  prides: DiaryPride[];
  loves: DiaryLove[];
  timeline: DiaryTimelineItem[];
  memories: DiaryMemory[];
  memoriesEmpty: string;
  hasMoreMemories: boolean;
  moods: readonly DiaryMood[];
  moodIdx: number;
  moodLoaded: boolean;
  moodSaving: boolean;
  canSaveMood: boolean;
  footerSlot?: ReactNode;
  onPickDay: (idx: number) => void;
  onPrevDay: () => void;
  onNextDay: () => void;
  onListenStory: () => void;
  onOpenMoments: () => void;
  onAddMoment: () => void;
  onOpenPrides: () => void;
  onOpenLoves: () => void;
  onOpenTimeline: () => void;
  onOpenMemories: () => void;
  onMoodPick: (idx: number) => void;
  onSaveMood: () => void;
  onPlayLove?: (id: string) => void;
};

const KIND_META: Record<
  DiaryMoment['kind'],
  { label: string; tone: string; badgeIcon: string }
> = {
  photo: { label: 'Ảnh', tone: 'amber', badgeIcon: '📷' },
  video: { label: 'Video', tone: 'violet', badgeIcon: '🎬' },
  voice: { label: 'Voice', tone: 'teal', badgeIcon: '🎙' },
  draw: { label: 'Vẽ tranh', tone: 'green', badgeIcon: '🌸' },
  other: { label: 'Moment', tone: 'lilac', badgeIcon: '✨' },
};

/** Diary UI labels — keep API codes from FAMILY_MOODS */
const DIARY_MOOD_UI: Record<string, { label: string; emoji: string }> = {
  love: { label: 'Tuyệt vời', emoji: '🤩' },
  happy: { label: 'Vui vẻ', emoji: '😄' },
  ok: { label: 'Ổn thôi', emoji: '😐' },
  mad: { label: 'Khó khăn', emoji: '😟' },
  sad: { label: 'Buồn quá', emoji: '😢' },
};

export function KidDiaryHub(props: Props) {
  const canPrev = props.dayIdx > 0;
  const canNext = props.dayIdx < props.days.length - 1;
  const momentsTrackRef = useRef<HTMLDivElement | null>(null);

  const moodOrder = useMemo(() => {
    const prefer = ['love', 'happy', 'ok', 'mad', 'sad'];
    const indexed = props.moods.map((m, idx) => ({ m, idx }));
    return [...indexed].sort(
      (a, b) => prefer.indexOf(a.m.code) - prefer.indexOf(b.m.code),
    );
  }, [props.moods]);

  const heroMoment = props.moments.find((m) => m.imageUrl) ?? props.moments[0] ?? null;

  const taleText = useMemo(() => {
    const lines =
      props.storyParagraphs.length > 0
        ? props.storyParagraphs
        : [
            props.isToday
              ? `${props.shortName} ơi — hôm nay chưa có khoảnh khắc đặc biệt. Làm việc tốt hoặc nhận lời khen, Fami sẽ viết nhật ký cho con nhé.`
              : `Ngày này Fami chưa ghi được câu chuyện đáng nhớ nào.`,
          ];
    const joined = lines.join(' ').replace(/\s+/g, ' ').trim();
    if (joined.length <= 320) return joined;
    const cut = joined.slice(0, 300);
    const soft = cut.lastIndexOf('. ');
    return soft > 120 ? cut.slice(0, soft + 1) : `${cut.trim()}…`;
  }, [props.storyParagraphs, props.isToday, props.shortName]);

  const polaroidUrl = heroMoment?.imageUrl || props.storyPhotoUrl || null;

  const momentCards = props.moments.slice(0, 8);

  const scrollMoments = (dir: -1 | 1) => {
    const el = momentsTrackRef.current;
    if (!el) return;
    el.scrollBy({ left: dir * Math.min(220, el.clientWidth * 0.7), behavior: 'smooth' });
  };

  return (
    <div className="kdiary">
      {props.toast ? (
        <div className="kdiary-toast" role="status">
          {props.toast}
        </div>
      ) : null}

      <div className="kdiary-dates">
        <button
          type="button"
          className="kdiary-nav"
          aria-label="Ngày trước"
          disabled={!canPrev}
          onClick={props.onPrevDay}
        >
          ‹
        </button>
        <div className="kdiary-date-scroll" role="tablist" aria-label="Chọn ngày">
          {props.days.map((d, i) => (
            <button
              key={d.key}
              type="button"
              role="tab"
              aria-selected={i === props.dayIdx}
              title={d.fullLabel}
              className={`kdiary-chip${i === props.dayIdx ? ' is-on' : ''}${
                d.isToday ? '' : ' is-past'
              }`}
              onClick={() => props.onPickDay(i)}
            >
              {d.shortLabel}
            </button>
          ))}
        </div>
        <button
          type="button"
          className="kdiary-nav"
          aria-label="Ngày sau"
          disabled={!canNext}
          onClick={props.onNextDay}
        >
          ›
        </button>
      </div>

      {!props.isToday ? (
        <p className="kdiary-readonly">Xem lại một ngày trong hành trình lớn lên của con.</p>
      ) : null}

      {/* 1. HERO — Fami kể chuyện + polaroid */}
      <article className="kdiary-tale" aria-label="Câu chuyện ngày hôm nay">
        <div className="kdiary-tale-fami" aria-hidden>
          <i className="kdiary-tale-heart is-a">💗</i>
          <i className="kdiary-tale-heart is-b">💕</i>
          <i className="kdiary-tale-heart is-c">💗</i>
          <img
            src="/mascot/fami-robot.png?v=5"
            alt=""
            width={120}
            height={120}
            decoding="async"
          />
        </div>

        <div className="kdiary-tale-bubble">
          <header className="kdiary-tale-head">
            <strong>Fami kể về ngày hôm nay</strong>
            <button
              type="button"
              className="kdiary-tale-speak"
              aria-label="Nghe câu chuyện"
              onClick={props.onListenStory}
            >
              🔊
            </button>
          </header>
          <p className="kdiary-tale-body">{highlightStory(taleText)}</p>
          <button type="button" className="kdiary-tale-listen" onClick={props.onListenStory}>
            <span aria-hidden>▶</span> Nghe lại câu chuyện
          </button>
        </div>

        {polaroidUrl ? (
          <button
            type="button"
            className="kdiary-tale-polaroid"
            onClick={props.onOpenMoments}
            aria-label="Xem khoảnh khắc"
          >
            <SoftEvidenceImg
              url={polaroidUrl}
              fallback={heroMoment?.icon || '📷'}
              auth={(u) => u?.trim() || undefined}
            />
            <i className="kdiary-tale-pin" aria-hidden>
              ♥
            </i>
          </button>
        ) : (
          <button
            type="button"
            className="kdiary-tale-polaroid is-empty"
            onClick={props.onAddMoment}
            aria-label="Thêm khoảnh khắc"
          >
            <span aria-hidden>{heroMoment?.icon || '📷'}</span>
            <em>Thêm ảnh</em>
            <i className="kdiary-tale-pin" aria-hidden>
              ♥
            </i>
          </button>
        )}
      </article>

      {/* 2. KHOẢNH KHẮC ĐÁNG NHỚ — carousel theo mẫu */}
      <section className="kdiary-moments">
        <header className="kdiary-sec-head">
          <h2>
            <span aria-hidden>⭐</span> Khoảnh khắc đáng nhớ
          </h2>
          <button type="button" className="kdiary-link is-purple" onClick={props.onOpenMoments}>
            Xem tất cả ›
          </button>
        </header>

        <div className="kdiary-moments-wrap">
          <button
            type="button"
            className="kdiary-moments-nav is-prev"
            aria-label="Khoảnh khắc trước"
            onClick={() => scrollMoments(-1)}
          >
            <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden>
              <path
                d="M14.5 5.5L8 12l6.5 6.5"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.4"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
          <div
            ref={momentsTrackRef}
            className="kdiary-moments-track"
            role="list"
            aria-label="Khoảnh khắc đáng nhớ"
          >
            {momentCards.map((m, i) => {
              const meta = KIND_META[m.kind];
              const starred =
                m.starred ||
                i === 0 ||
                /đánh răng|tự giác/i.test(m.title);
              return (
                <article
                  key={m.id}
                  className={`kdiary-mcard tone-${meta.tone}`}
                  role="listitem"
                >
                  <div className={`kdiary-mcard-media is-${m.kind}`} aria-hidden>
                    <span className="kdiary-mcard-badge">
                      <i>{meta.badgeIcon}</i>
                      {meta.label}
                    </span>
                    {m.kind === 'voice' ? (
                      <div className="kdiary-mcard-voice">
                        <div className="kdiary-mcard-voice-top">
                          <b>{props.shortName.slice(0, 1).toUpperCase()}</b>
                          <span className="kdiary-mcard-wave" />
                        </div>
                        <div className="kdiary-mcard-voice-bar">
                          <em>▶</em>
                          <i>{m.duration || '0:18'}</i>
                        </div>
                      </div>
                    ) : (
                      <SoftEvidenceImg
                        url={m.imageUrl}
                        fallback={m.icon || meta.badgeIcon}
                        fallbackClassName="kdiary-mcard-fallback"
                        auth={(u) => u?.trim() || undefined}
                      />
                    )}
                    {m.kind === 'video' ? (
                      <i className="kdiary-mcard-play" aria-hidden>
                        ▶
                      </i>
                    ) : null}
                  </div>
                  <div className="kdiary-mcard-foot">
                    <strong>
                      {m.title}
                      {starred ? <i aria-hidden>⭐</i> : null}
                    </strong>
                    {m.kind !== 'voice' ? (
                      <em>
                        {m.kind === 'video' ? '🕒 ' : ''}
                        {m.time}
                      </em>
                    ) : (
                      <em>Voice · {m.duration || m.time}</em>
                    )}
                  </div>
                </article>
              );
            })}
            <button
              type="button"
              className="kdiary-mcard is-add"
              onClick={props.onAddMoment}
              aria-label="Thêm khoảnh khắc mới"
            >
              <span className="kdiary-mcard-add-ico" aria-hidden>
                📷
              </span>
              <strong>Thêm khoảnh khắc mới</strong>
            </button>
          </div>
          <button
            type="button"
            className="kdiary-moments-nav is-next"
            aria-label="Khoảnh khắc sau"
            onClick={() => scrollMoments(1)}
          >
            <svg viewBox="0 0 24 24" width="16" height="16" aria-hidden>
              <path
                d="M9.5 5.5L16 12l-6.5 6.5"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.4"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
        </div>
      </section>

      {/* 3 + 4. Tự hào + Lời yêu thương — theo mẫu */}
      <div className="kdiary-mid">
        <section className="kdiary-panel is-pride">
          <header className="kdiary-panel-head">
            <h2>
              <span aria-hidden>🏆</span> Những điều đáng tự hào
            </h2>
            <button type="button" className="kdiary-link is-purple" onClick={props.onOpenPrides}>
              Xem tất cả ›
            </button>
          </header>
          {props.prides.length === 0 ? (
            <p className="kdiary-empty">
              Huy hiệu, lần đầu tiên, việc hoàn thành… Fami sẽ gắn ở đây.
            </p>
          ) : (
            <div className="kdiary-pride-rail" role="list">
              {props.prides.map((p) => (
                <article key={p.id} className="kdiary-pride-card" role="listitem">
                  <span className="kdiary-pride-ico" aria-hidden>
                    {p.icon}
                  </span>
                  <em>{p.title}</em>
                  <strong>{p.subtitle}</strong>
                  <b>{p.when}</b>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="kdiary-panel is-love">
          <header className="kdiary-panel-head">
            <h2>
              <span aria-hidden>💗</span> Lời yêu thương
            </h2>
            <button type="button" className="kdiary-link is-rose" onClick={props.onOpenLoves}>
              Xem tất cả ›
            </button>
          </header>
          {props.loves.length === 0 ? (
            <p className="kdiary-empty">
              Khi {props.parentRole} gửi thư hoặc lời khen — Fami giữ mãi cho {props.shortName}.
            </p>
          ) : (
            <div className="kdiary-love-stack">
              {props.loves.map((l) => {
                const initial =
                  (l.fromLabel.replace(/^[^A-Za-zÀ-ỹ]+/, '').trim().charAt(0) ||
                    (l.tone === 'mom' ? 'M' : l.tone === 'dad' ? 'B' : '💙')
                  ).toUpperCase();
                return (
                  <article key={l.id} className={`kdiary-love-row is-${l.tone}`}>
                    <div className="kdiary-love-avatar" aria-hidden>
                      {initial}
                    </div>
                    <div className="kdiary-love-copy">
                      <strong>
                        <i aria-hidden>{l.tone === 'dad' ? '💙' : '💗'}</i>
                        {l.fromLabel}
                      </strong>
                      <p>«{l.body}»</p>
                      <em>{l.when}</em>
                    </div>
                    <button
                      type="button"
                      className="kdiary-love-play"
                      aria-label="Nghe lời yêu thương"
                      onClick={() => props.onPlayLove?.(l.id)}
                    >
                      <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden>
                        <path d="M8 5.5v13l11-6.5L8 5.5z" fill="currentColor" />
                      </svg>
                    </button>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </div>

      {/* 5. Reflection — theo mẫu Fami cảm xúc */}
      <section className="kdiary-feel">
        <header className="kdiary-feel-head">
          <h2>
            <span aria-hidden>😊</span>
            {props.isToday
              ? 'Con cảm thấy thế nào về ngày hôm nay?'
              : 'Ngày đó con cảm thấy thế nào?'}
          </h2>
          <em>
            <span aria-hidden>💡</span> Gợi ý từ Fami
          </em>
        </header>

        {props.isToday ? (
          <>
            <div className="kdiary-feel-body">
              <div className="kdiary-feel-picks" aria-label="Chọn cảm xúc">
                {moodOrder.map(({ m, idx }) => {
                  const ui = DIARY_MOOD_UI[m.code] ?? m;
                  return (
                    <button
                      key={m.code}
                      type="button"
                      className={`kdiary-feel-card${idx === props.moodIdx ? ' is-on' : ''}`}
                      aria-pressed={idx === props.moodIdx}
                      onClick={() => props.onMoodPick(idx)}
                    >
                      <span aria-hidden>{ui.emoji}</span>
                      <em>{ui.label}</em>
                    </button>
                  );
                })}
              </div>
              <aside className="kdiary-feel-fami" aria-hidden>
                <div className="kdiary-feel-bubble">
                  <p>
                    Fami luôn ở đây
                    <br />
                    để lắng nghe
                    <br />
                    cảm xúc của con! 💚
                  </p>
                </div>
                <img
                  src="/mascot/fami-robot.png?v=5"
                  alt=""
                  width={96}
                  height={96}
                  decoding="async"
                />
              </aside>
            </div>
            <button
              type="button"
              className="kdiary-feel-save"
              disabled={!props.canSaveMood || props.moodSaving || !props.moodLoaded}
              onClick={props.onSaveMood}
            >
              {props.moodSaving ? 'Đang lưu…' : 'Lưu cảm xúc ngày hôm nay'}
            </button>
          </>
        ) : (
          <p className="kdiary-empty">Chọn «Hôm nay» để ghi cảm xúc.</p>
        )}
      </section>

      {/* Dòng thời gian hôm nay */}
      <section className="kdiary-dayline">
        <header className="kdiary-dayline-head">
          <h2>
            <span aria-hidden>🔗</span>{' '}
            {props.isToday ? 'Dòng thời gian hôm nay' : 'Dòng thời gian ngày này'}
          </h2>
          <button type="button" className="kdiary-link is-purple" onClick={props.onOpenTimeline}>
            Xem tất cả ›
          </button>
        </header>
        {props.loading ? (
          <p className="kdiary-empty">Đang mở nhật ký ngày này…</p>
        ) : props.error ? (
          <p className="kdiary-empty">{props.error}</p>
        ) : props.timeline.length === 0 ? (
          <p className="kdiary-empty">
            {props.isToday
              ? 'Hoàn thành việc tốt — Fami sẽ xếp vào dòng thời gian của con.'
              : 'Ngày này chưa có mốc trên dòng thời gian.'}
          </p>
        ) : (
          <div className="kdiary-dayline-track" role="list">
            <i className="kdiary-dayline-rail" aria-hidden />
            {props.timeline.map((t) => (
              <div
                key={t.id}
                className={`kdiary-dayline-node${t.done ? ' is-done' : ''}`}
                role="listitem"
              >
                <span className="kdiary-dayline-ico" aria-hidden>
                  {t.icon}
                </span>
                <em>{t.time}</em>
                <strong>{t.title}</strong>
                <b>{t.tag}</b>
              </div>
            ))}
            <span className="kdiary-dayline-end" aria-hidden>
              ›
            </span>
          </div>
        )}
      </section>

      {/* Album — Kỷ niệm đẹp của gia đình (theo mẫu) */}
      <section className="kdiary-album">
        <i className="kdiary-album-deco is-a" aria-hidden>
          ♡
        </i>
        <i className="kdiary-album-deco is-b" aria-hidden>
          ♥
        </i>
        <i className="kdiary-album-deco is-c" aria-hidden>
          ♡
        </i>
        <header className="kdiary-album-head">
          <h2>
            <span aria-hidden>💗</span> Kỷ niệm đẹp của gia đình
          </h2>
          <button type="button" className="kdiary-link is-purple" onClick={props.onOpenMemories}>
            Xem tất cả ›
          </button>
        </header>
        <div className="kdiary-album-track" role="list">
          {props.memories.length === 0 ? (
            <p className="kdiary-empty">{props.memoriesEmpty}</p>
          ) : (
            props.memories.map((m) => (
              <article key={m.id} className="kdiary-album-card" role="listitem">
                <div className="kdiary-album-media" aria-hidden>
                  <SoftEvidenceImg
                    url={m.imageUrl}
                    fallback={m.icon || '📸'}
                    fallbackClassName="kdiary-album-fallback"
                    auth={(u) => u?.trim() || undefined}
                  />
                </div>
                <div className="kdiary-album-cap">
                  <strong>{m.title}</strong>
                  <em>{m.date}</em>
                </div>
              </article>
            ))
          )}
          <button
            type="button"
            className="kdiary-album-card is-add"
            onClick={props.onAddMoment}
            aria-label="Thêm kỷ niệm của con"
          >
            <span className="kdiary-album-add-ico" aria-hidden>
              🖼️
            </span>
            <strong>
              Thêm kỷ niệm
              <br />
              của con
            </strong>
          </button>
        </div>
      </section>

      {props.footerSlot ? <div className="kdiary-footer">{props.footerSlot}</div> : null}
    </div>
  );
}
