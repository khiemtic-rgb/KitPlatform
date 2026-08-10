import { useMemo, type ReactNode } from 'react';
import type { DayFlowCommitment } from '@/shared/api/family-os.api';
import type { FamilyMemory } from '@/shared/flow/family-memories';
import {
  FAMILY_MEMORY_EMPTY,
  memoryRelativeAgoLabel,
} from '@/shared/flow/family-memories';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';
import { SoftEvidenceImg } from '@/shared/ui/SoftEvidenceImg';
import { isKidMomentAudio } from '@/modules/flow/kidMomentAck';

export type DiaryLens =
  | 'all'
  | 'moments'
  | 'words'
  | 'achievements'
  | 'events'
  | 'memories';

export type DiaryDayChip = {
  key: string;
  shortLabel: string;
  fullLabel: string;
  isToday: boolean;
};

export type DiaryTimelineEntry = {
  item: DayFlowCommitment;
  time: string;
  done: boolean;
  wait: boolean;
  skipped: boolean;
  who: string;
  note: string;
  starLabel: string;
  lateCaption: string | null;
  tone: string;
  tag: { label: string; tone: string };
};

export type DiaryMomentCard = {
  id: string;
  icon: string;
  title: string;
  date: string;
  caption: string;
  memory: FamilyMemory;
};

type Props = {
  toast?: string | null;
  childPicker: ReactNode;
  noChildNotice?: ReactNode;
  hasChildren: boolean;
  childShort: string;
  attentionCount?: number;
  onOpenAttention?: () => void;
  scopedDone: number;
  scopedTotal: number;
  storyBullets?: string[];
  qualityMinutes?: number;
  momentsCount: number;
  diaryDays: DiaryDayChip[];
  diaryDayIdx: number;
  onSelectDay: (idx: number) => void;
  datesRef?: (el: HTMLDivElement | null) => void;
  lens: DiaryLens;
  onLens: (lens: DiaryLens) => void;
  entries: DiaryTimelineEntry[];
  entriesExpanded: boolean;
  onToggleExpand: () => void;
  moments: DiaryMomentCard[];
  memories: FamilyMemory[];
  memoryHeartBusy: string | null;
  busyId: string | null;
  verifyingId: string | null;
  coachInsight?: string | null;
  onOpenVerify: (item: DayFlowCommitment) => void;
  onHeartMemory: (mem: FamilyMemory) => void;
  onOpenMemories: () => void;
  onOpenStoryDetail: () => void;
  onOpenReport: () => void;
  taskIcon: (title: string) => string;
  withEvidenceAuth: (url: string | null | undefined) => string | undefined;
  /** M0 Daily Digital Mirror empty / checklist */
  mirrorPanel?: ReactNode;
};

const LENSES: Array<{ id: DiaryLens; icon: string; label: string; tone: string }> = [
  { id: 'all', icon: '▦', label: 'Tất cả', tone: 'teal' },
  { id: 'moments', icon: '📷', label: 'Khoảnh khắc', tone: 'violet' },
  { id: 'words', icon: '🎤', label: 'Lời nói', tone: 'lilac' },
  { id: 'achievements', icon: '🏆', label: 'Thành tựu', tone: 'amber' },
  { id: 'events', icon: '💕', label: 'Sự kiện', tone: 'pink' },
  { id: 'memories', icon: '🖼️', label: 'Kỷ niệm', tone: 'rose' },
];

function momentKindIcon(mem: FamilyMemory): string {
  const kind = mem.entry?.kind || mem.filterKind || '';
  const audio = isKidMomentAudio({
    icon: mem.entry?.icon || mem.icon,
    photoUrl: mem.photoUrl || mem.entry?.photoUrl,
  });
  if (kind === 'parent_voice' || audio) return '🎤';
  if (kind === 'kid_moment') return '📷';
  if (kind === 'team_unlock' || kind === 'beautiful_day') return '🏆';
  if (kind === 'movie_night') return '💕';
  return mem.icon || '✨';
}

function MemoryCardArt(props: {
  photoUrl?: string;
  icon: string;
  withEvidenceAuth: (url?: string | null) => string | undefined;
  audio?: boolean;
  emojiClassName?: string;
}) {
  if (props.audio && !props.photoUrl) {
    return <span className="pd-wave">♪ ▄ ▅ ▆ ▅ ▄</span>;
  }
  return (
    <SoftEvidenceImg
      url={props.photoUrl}
      fallback={props.icon}
      className={props.emojiClassName}
      auth={props.withEvidenceAuth}
    />
  );
}

export function ParentDiaryPanel(props: Props) {
  const remaining = Math.max(0, props.scopedTotal - props.scopedDone);
  const ratio = `${props.scopedDone}/${Math.max(props.scopedTotal, 0)}`;
  const qualityMins =
    props.qualityMinutes ??
    Math.max(0, Math.min(45, props.scopedDone * 3 + (props.momentsCount > 0 ? 6 : 0)));

  const bullets = useMemo(() => {
    if (props.storyBullets && props.storyBullets.length > 0) return props.storyBullets;
    const out: string[] = [];
    if (props.scopedDone > 0) {
      out.push(`⭐ Nhà mình đã xong ${props.scopedDone} việc trong kế hoạch hôm nay`);
    }
    if (props.momentsCount > 0) {
      out.push(`❤️ Có ${props.momentsCount} khoảnh khắc đáng nhớ được giữ lại`);
    }
    if (remaining === 0 && props.scopedTotal > 0) {
      out.push(`☀️ Một ngày giữ nhịp trọn vẹn — thật đáng kể!`);
    } else if (remaining > 0) {
      out.push(`☀️ Còn ${remaining} việc nhẹ để khép lại ngày thật đẹp`);
    }
    if (out.length === 0) {
      out.push('✨ Hôm nay vẫn là một trang trắng đẹp — bắt đầu bằng một lời ấm');
    }
    return out.slice(0, 3);
  }, [
    props.storyBullets,
    props.scopedDone,
    props.momentsCount,
    remaining,
    props.scopedTotal,
  ]);

  const showTimeline = props.lens !== 'memories' && props.lens !== 'words';
  const showMoments =
    props.lens === 'all' ||
    props.lens === 'moments' ||
    props.lens === 'words' ||
    props.lens === 'achievements';
  const showMemories =
    props.lens === 'all' || props.lens === 'memories' || props.lens === 'events';
  const showStory = props.lens === 'all' || props.lens === 'achievements';

  const visibleEntries =
    props.entriesExpanded || props.entries.length <= 6
      ? props.entries
      : props.entries.slice(0, 6);

  const momentCards = useMemo(() => {
    const isAudio = (m: DiaryMomentCard) =>
      isKidMomentAudio({
        icon: m.memory.entry?.icon || m.memory.icon,
        photoUrl: m.memory.photoUrl || m.memory.entry?.photoUrl,
      });
    if (props.lens === 'words') {
      return props.moments.filter(
        (m) =>
          m.memory.entry?.kind === 'parent_voice' ||
          isAudio(m) ||
          /giọng|lời|voice|khen/i.test(`${m.title} ${m.caption}`),
      );
    }
    if (props.lens === 'achievements') {
      return props.moments.filter(
        (m) =>
          m.memory.entry?.kind === 'team_unlock' ||
          m.memory.entry?.kind === 'beautiful_day' ||
          /thành|mở khóa|sao|🏆/i.test(`${m.title} ${m.icon}`),
      );
    }
    return props.moments;
  }, [props.moments, props.lens]);

  return (
    <div className="pd-root" id="pd-diary">
      {props.toast ? (
        <div className="pd-toast" role="status">
          {props.toast}
        </div>
      ) : null}

      <header className="pd-top">
        <div className="pd-titles">
          <h1>
            Nhật ký của nhà mình <span aria-hidden>💜</span>
          </h1>
          <p>Mỗi ngày là một câu chuyện đẹp</p>
        </div>
        <div className="pd-top-actions">
          <button
            type="button"
            className="pd-ico-btn"
            aria-label="Chọn ngày nhật ký"
            onClick={() => {
              const idx = props.diaryDays.findIndex((d) => d.isToday);
              if (idx >= 0) props.onSelectDay(idx);
            }}
          >
            📅
          </button>
          {props.onOpenAttention ? (
            <button
              type="button"
              className="pd-ico-btn pd-bell"
              aria-label="Việc cần ưu tiên"
              onClick={props.onOpenAttention}
            >
              🔔
              {(props.attentionCount ?? 0) > 0 ? (
                <i>{Math.min(props.attentionCount ?? 0, 9)}</i>
              ) : null}
            </button>
          ) : null}
          {props.childPicker}
        </div>
      </header>

      {!props.hasChildren ? props.noChildNotice : null}

      {props.hasChildren && props.mirrorPanel ? props.mirrorPanel : null}

      <div
        ref={props.datesRef}
        className="pd-dates"
        role="tablist"
        aria-label="Chọn ngày"
      >
        {props.diaryDays.map((d, i) => (
          <button
            key={d.key}
            type="button"
            role="tab"
            aria-selected={i === props.diaryDayIdx}
            aria-disabled={!d.isToday}
            title={
              d.isToday ? d.fullLabel : 'Nhật ký các ngày khác sẽ mở khi có lịch sử lưu'
            }
            className={`pd-date${i === props.diaryDayIdx ? ' is-on' : ''}${
              d.isToday ? '' : ' is-muted'
            }`}
            disabled={!d.isToday}
            onClick={() => {
              if (!d.isToday) return;
              props.onSelectDay(i);
            }}
          >
            {d.shortLabel}
          </button>
        ))}
        <button
          type="button"
          className="pd-date is-pick"
          onClick={() => {
            const idx = props.diaryDays.findIndex((d) => d.isToday);
            if (idx >= 0) props.onSelectDay(idx);
          }}
        >
          <span aria-hidden>📅</span> Chọn ngày
        </button>
      </div>

      <nav className="pd-lenses" aria-label="Lọc nhật ký">
        {LENSES.map((f) => (
          <button
            key={f.id}
            type="button"
            className={`pd-lens is-${f.tone}${props.lens === f.id ? ' is-on' : ''}`}
            onClick={() => props.onLens(f.id)}
          >
            <span aria-hidden>{f.icon}</span>
            {f.label}
          </button>
        ))}
      </nav>

      {showStory ? (
        <article className="pd-story">
          <header className="pd-story-head">
            <span aria-hidden>🤖</span>
            <strong>Câu chuyện hôm nay</strong>
          </header>
          <div className="pd-story-body">
            <div className="pd-story-copy">
              <h2>
                {props.scopedTotal > 0 && remaining === 0
                  ? 'Hôm nay là một ngày đáng nhớ! 💚'
                  : props.scopedDone > 0
                    ? 'Hôm nay nhà mình đang viết một câu chuyện ấm. 💚'
                    : 'Hôm nay vẫn có thể thành một câu chuyện đẹp. 💜'}
              </h2>
              <ul>
                {bullets.map((b) => (
                  <li key={b}>{b}</li>
                ))}
              </ul>
            </div>
            <div className="pd-story-art" aria-hidden>
              <span className="pd-story-bubble is-a">👨</span>
              <span className="pd-story-bubble is-b">👩</span>
              <span className="pd-story-bubble is-c">👧</span>
              <span className="pd-story-bubble is-d">📖</span>
            </div>
          </div>
          <div className="pd-story-stats">
            <div>
              <strong className="is-ok">
                <i aria-hidden>✓</i> {ratio}
              </strong>
              <em>Việc đã xong</em>
            </div>
            <div>
              <strong className="is-pink">
                <i aria-hidden>♡</i> {qualityMins} phút
              </strong>
              <em>Thời gian chất lượng</em>
            </div>
            <div>
              <strong className="is-amber">
                <i aria-hidden>★</i> {props.momentsCount}
              </strong>
              <em>Khoảnh khắc đẹp</em>
            </div>
          </div>
          <button type="button" className="pd-story-cta" onClick={props.onOpenStoryDetail}>
            Xem câu chuyện chi tiết <span aria-hidden>›</span>
          </button>
        </article>
      ) : null}

      {showMoments ? (
        <section className="pd-sec">
          <header className="pd-sec-head">
            <h2>
              {props.lens === 'words'
                ? 'Lời nói ấm hôm nay'
                : 'Khoảnh khắc đáng nhớ hôm nay'}
            </h2>
            {props.moments.length > 0 ? (
              <button type="button" className="pd-link" onClick={props.onOpenMemories}>
                Xem tất cả ›
              </button>
            ) : null}
          </header>
          {momentCards.length === 0 ? (
            <p className="pd-empty">{FAMILY_MEMORY_EMPTY}</p>
          ) : (
            <div className="pd-hscroll" role="list">
              {momentCards.map((m) => {
                const audio = isKidMomentAudio({
                  icon: m.memory.entry?.icon || m.memory.icon,
                  photoUrl: m.memory.photoUrl || m.memory.entry?.photoUrl,
                });
                return (
                  <article key={m.id} className="pd-moment" role="listitem">
                    <span className="pd-moment-kind" aria-hidden>
                      {momentKindIcon(m.memory)}
                    </span>
                    <button
                      type="button"
                      className={`pd-moment-heart${m.memory.entry?.isFavorite ? ' is-on' : ''}`}
                      aria-label={
                        m.memory.entry?.isFavorite ? 'Bỏ thích' : 'Lưu / thích kỷ niệm'
                      }
                      disabled={props.memoryHeartBusy === m.id}
                      onClick={() => props.onHeartMemory(m.memory)}
                    >
                      {m.memory.entry?.isFavorite ? '❤️' : '🤍'}
                    </button>
                    <div className={`pd-moment-art${audio ? ' is-audio' : ''}`} aria-hidden>
                      <MemoryCardArt
                        photoUrl={m.memory.photoUrl}
                        icon={m.icon}
                        withEvidenceAuth={props.withEvidenceAuth}
                        audio={audio}
                        emojiClassName="pd-moment-emoji"
                      />
                    </div>
                    <strong>{m.title}</strong>
                    <em>{m.caption}</em>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      ) : null}

      {showTimeline ? (
        <section className="pd-sec" id="pd-timeline">
          <header className="pd-sec-head">
            <h2>Nhật ký hôm nay</h2>
            <em className="pd-sec-count">{props.entries.length} mục</em>
          </header>
          {props.entries.length === 0 ? (
            <p className="pd-empty">
              {props.hasChildren
                ? `Hôm nay chưa có trang nhật ký — khi ${props.childShort} làm việc, nhật ký sẽ hiện ở đây.`
                : 'Chưa có con trong nhà — thêm con để Famixa ghi nhật ký mỗi ngày.'}
            </p>
          ) : (
            <ol className="pd-timeline">
              {visibleEntries.map((entry) => (
                <li key={entry.item.id} className="pd-node">
                  <time>{entry.time}</time>
                  <span className="pd-rail" aria-hidden />
                  <article
                    className={`pd-card${entry.wait ? ' is-tap' : ''}`}
                    role={entry.wait ? 'button' : undefined}
                    tabIndex={entry.wait ? 0 : undefined}
                    onClick={
                      entry.wait ? () => props.onOpenVerify(entry.item) : undefined
                    }
                    onKeyDown={
                      entry.wait
                        ? (e) => {
                            if (e.key === 'Enter' || e.key === ' ') {
                              e.preventDefault();
                              props.onOpenVerify(entry.item);
                            }
                          }
                        : undefined
                    }
                  >
                    <span className={`pd-ico tone-${entry.tone}`} aria-hidden>
                      {props.taskIcon(entry.item.title)}
                    </span>
                    <div className="pd-card-body">
                      <strong>{entry.item.title}</strong>
                      <p>{entry.note}</p>
                      {entry.wait ? (
                        <button
                          type="button"
                          className="pd-mini-cta"
                          disabled={
                            props.busyId === entry.item.id ||
                            props.verifyingId === entry.item.id
                          }
                          onClick={(e) => {
                            e.stopPropagation();
                            props.onOpenVerify(entry.item);
                          }}
                        >
                          {props.busyId === entry.item.id ||
                          props.verifyingId === entry.item.id
                            ? 'Đang…'
                            : 'Xem & xác nhận'}
                        </button>
                      ) : entry.done ? (
                        <span className="pd-star-line">{entry.starLabel}</span>
                      ) : null}
                    </div>
                    <span
                      className="pd-who"
                      title={entry.who}
                      aria-label={entry.who ? `Của ${entry.who}` : undefined}
                    >
                      {avatarEmoji(inferGenderFromName(entry.who || 'Con'), 'child')}
                    </span>
                  </article>
                </li>
              ))}
            </ol>
          )}
          {props.entries.length > 6 ? (
            <button type="button" className="pd-more" onClick={props.onToggleExpand}>
              {props.entriesExpanded ? 'Thu gọn' : 'Xem thêm'} <span aria-hidden>▾</span>
            </button>
          ) : null}
        </section>
      ) : null}

      <aside className="pd-ai">
        <div className="pd-ai-art" aria-hidden>
          🤖
        </div>
        <div className="pd-ai-copy">
          <strong>AI ghi lại &amp; kể chuyện</strong>
          <p>
            {props.coachInsight?.trim() ||
              (props.scopedDone > 0
                ? `Tuần này nhà mình đang giữ nhịp tốt — ${ratio} việc hôm nay đã xong. Famixa có thể gom thành báo cáo ấm cho cả nhà.`
                : 'Famixa sẵn sàng ghi lại nhịp nhà và kể lại thành câu chuyện cuối ngày.')}
          </p>
          <button type="button" className="pd-ai-cta" onClick={props.onOpenReport}>
            Xem chi tiết báo cáo
          </button>
        </div>
      </aside>

      {showMemories ? (
        <section className="pd-sec">
          <header className="pd-sec-head">
            <h2>Kỷ niệm</h2>
            {props.memories.length > 0 ? (
              <button type="button" className="pd-link" onClick={props.onOpenMemories}>
                Xem tất cả ›
              </button>
            ) : null}
          </header>
          {props.memories.length === 0 ? (
            <p className="pd-empty">{FAMILY_MEMORY_EMPTY}</p>
          ) : (
            <div className="pd-hscroll" role="list">
              {props.memories.slice(0, 8).map((m) => (
                  <article
                    key={m.id}
                    className={`pd-memory${m.locked ? ' is-locked' : ''}`}
                    role="listitem"
                  >
                    <span className="pd-memory-ago">{memoryRelativeAgoLabel(m.sortAt)}</span>
                    <div className="pd-memory-art" aria-hidden>
                      <MemoryCardArt
                        photoUrl={m.photoUrl}
                        icon={m.icon}
                        withEvidenceAuth={props.withEvidenceAuth}
                      />
                    </div>
                    <strong>{m.title}</strong>
                    <button
                      type="button"
                      className={`pd-moment-heart is-mem${
                        m.entry?.isFavorite ? ' is-on' : ''
                      }`}
                      aria-label={m.entry?.isFavorite ? 'Bỏ thích' : 'Lưu / thích kỷ niệm'}
                      disabled={props.memoryHeartBusy === m.id}
                      onClick={() => props.onHeartMemory(m)}
                    >
                      {m.entry?.isFavorite ? '❤️' : '🤍'}
                    </button>
                  </article>
                ))}
            </div>
          )}
        </section>
      ) : null}
    </div>
  );
}
