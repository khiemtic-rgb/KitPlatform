import { useEffect, useMemo, useState, type ReactNode } from 'react';
import type { DayFlowCommitment } from '@/shared/api/family-os.api';

export type KidPlanDayPart = 'morning' | 'afternoon' | 'evening';

type Props = {
  shortName: string;
  remaining: number;
  doneCount: number;
  total: number;
  nextMission: DayFlowCommitment | null;
  todayItems: DayFlowCommitment[];
  busyId: string | null;
  uploading: boolean;
  missionDoneError: string | null;
  famiLine: string;
  challengeSlot?: ReactNode;
  challengeHave: number;
  challengeNeed: number;
  dayPartOf: (item: DayFlowCommitment) => KidPlanDayPart;
  taskIcon: (title: string) => string;
  taskIconTone: (title: string) => string;
  clockLabel: (item: DayFlowCommitment) => string;
  durationLabel: (item: DayFlowCommitment) => string | null;
  starHint: (item: DayFlowCommitment) => string;
  studyNeedsEvidence: (item: DayFlowCommitment) => boolean;
  onStartNext: () => void;
  onOpenRewards: () => void;
  onTalkFami: () => void;
  onStartItem: (item: DayFlowCommitment) => void;
};

const PART_META: Record<
  KidPlanDayPart,
  { label: string; short: string; emoji: string; stage: string; windowHint: string }
> = {
  morning: {
    label: 'Buổi sáng',
    short: 'Sáng',
    emoji: '☀️',
    stage: 'Buổi sáng',
    windowHint: 'Trước 12:00',
  },
  afternoon: {
    label: 'Buổi chiều',
    short: 'Chiều',
    emoji: '🌤️',
    stage: 'Buổi chiều',
    windowHint: '12:00 – 17:00',
  },
  evening: {
    label: 'Buổi tối',
    short: 'Tối',
    emoji: '🌙',
    stage: 'Buổi tối',
    windowHint: 'Từ 17:00',
  },
};

type StepMark = 'done' | 'wait' | 'next' | 'todo';

function sortByWindow(a: DayFlowCommitment, b: DayFlowCommitment): number {
  const aw = a.windowStart || a.windowEnd || '99:99';
  const bw = b.windowStart || b.windowEnd || '99:99';
  return aw.localeCompare(bw);
}

function isOpenItem(item: DayFlowCommitment): boolean {
  return item.status !== 'done' && item.status !== 'skipped';
}

function isDoneItem(item: DayFlowCommitment): boolean {
  return item.status === 'done';
}

function stepMarkOf(item: DayFlowCommitment, nextId: string | null): StepMark {
  if (item.status === 'done') {
    if (item.starPosted === false || item.evidenceSatisfied === false) return 'wait';
    return 'done';
  }
  if (nextId && item.id === nextId) return 'next';
  return 'todo';
}

function stepGlyph(m: StepMark): string {
  if (m === 'done') return '✓';
  if (m === 'wait') return '…';
  return '';
}

function stepBadge(m: StepMark): { label: string; tone: string } {
  if (m === 'done') return { label: 'Đã xong', tone: 'done' };
  if (m === 'next') return { label: 'Tiếp theo', tone: 'next' };
  if (m === 'wait') return { label: 'Chờ xác nhận', tone: 'wait' };
  return { label: 'Chưa làm', tone: 'todo' };
}

function stepClock(item: DayFlowCommitment): string {
  if (item.windowStart) return item.windowStart.slice(0, 5);
  if (item.windowEnd) return item.windowEnd.slice(0, 5);
  return '';
}

export function KidPlanHub(props: Props) {
  const nextId = props.nextMission?.id ?? null;
  const nextPart = props.nextMission ? props.dayPartOf(props.nextMission) : null;

  const [openParts, setOpenParts] = useState<Record<KidPlanDayPart, boolean>>({
    morning: true,
    afternoon: true,
    evening: true,
  });
  const [doneOpen, setDoneOpen] = useState(false);

  useEffect(() => {
    if (!nextPart) return;
    setOpenParts((prev) => ({ ...prev, [nextPart]: true }));
  }, [nextPart, nextId]);

  const dayPct = Math.max(
    0,
    Math.min(100, Math.round((props.doneCount / Math.max(props.total, 1)) * 100)),
  );

  const groups = useMemo(() => {
    const bucket: Record<KidPlanDayPart, DayFlowCommitment[]> = {
      morning: [],
      afternoon: [],
      evening: [],
    };
    for (const item of [...props.todayItems].sort(sortByWindow)) {
      bucket[props.dayPartOf(item)].push(item);
    }
    return (['morning', 'afternoon', 'evening'] as const)
      .map((key) => {
        const items = bucket[key];
        const open = items.filter(isOpenItem);
        const done = items.filter(isDoneItem);
        return {
          key,
          ...PART_META[key],
          items,
          open,
          done,
          allDone: items.length > 0 && open.length === 0,
        };
      })
      .filter((g) => g.items.length > 0);
  }, [props.todayItems, props.dayPartOf]);

  const doneItems = useMemo(
    () => props.todayItems.filter(isDoneItem).sort(sortByWindow),
    [props.todayItems],
  );
  const donePreview = doneItems.slice(0, 4);
  const doneExtra = Math.max(0, doneItems.length - donePreview.length);
  const togglePart = (key: KidPlanDayPart) => {
    setOpenParts((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const challengeSlots = props.challengeNeed <= 8 ? Math.max(props.challengeNeed, 1) : 7;
  const challengeFilled =
    props.challengeNeed <= 8
      ? Math.min(challengeSlots, props.challengeHave)
      : Math.min(
          challengeSlots,
          Math.round((props.challengeHave / Math.max(props.challengeNeed, 1)) * challengeSlots),
        );
  const challengeComplete = props.challengeHave >= props.challengeNeed && props.challengeNeed > 0;

  const nextBusy =
    Boolean(props.nextMission) &&
    (props.busyId === props.nextMission?.id || props.uploading);

  return (
    <div className="kplan">
      {/* Hero sinh động — Fami · việc tiếp theo | tiến độ · quà */}
      <div className="kplan-hero">
        <article className="kplan-hero-next">
          <div className="kplan-hero-top">
            <div className="kplan-hero-fami" aria-hidden>
              <img
                src="/mascot/fami-robot.png?v=4"
                alt=""
                width={140}
                height={140}
                decoding="async"
              />
            </div>
            <div className="kplan-hero-copy">
              <em>Việc tiếp theo của con là</em>
              {props.nextMission ? (
                <>
                  <div className="kplan-hero-title">
                    <strong>{props.nextMission.title}</strong>
                    <span
                      className={`kplan-hero-ico tone-${props.taskIconTone(props.nextMission.title)}`}
                      aria-hidden
                    >
                      {props.taskIcon(props.nextMission.title)}
                    </span>
                  </div>
                  <div className="kplan-hero-pills">
                    <span className="is-time">
                      <span aria-hidden>🕒</span>{' '}
                      {props.durationLabel(props.nextMission) ??
                        props.clockLabel(props.nextMission)}
                    </span>
                    <span className="is-star">
                      <span aria-hidden>⭐</span> {props.starHint(props.nextMission)}
                    </span>
                  </div>
                </>
              ) : (
                <div className="kplan-hero-title">
                  <strong>Xong hết rồi!</strong>
                  <span className="kplan-hero-ico tone-gold" aria-hidden>
                    🏆
                  </span>
                </div>
              )}
            </div>
          </div>
          <button
            type="button"
            className="kplan-hero-cta"
            disabled={!props.nextMission || nextBusy}
            onClick={props.onStartNext}
          >
            {nextBusy ? (
              '…'
            ) : props.nextMission && props.studyNeedsEvidence(props.nextMission) ? (
              <>
                <span aria-hidden>▶</span> Gửi ảnh ngay
              </>
            ) : (
              <>
                <span aria-hidden>▶</span> Bắt đầu ngay
              </>
            )}
          </button>
        </article>

        <aside className="kplan-hero-side">
          <strong>Tiến độ hôm nay</strong>
          <div className="kplan-hero-bar-row">
            <div className="kplan-hero-bar" aria-hidden>
              <b style={{ width: `${dayPct}%` }} />
            </div>
            <em>
              {props.doneCount} / {Math.max(props.total, 0)}
            </em>
          </div>
          <p>
            {props.remaining > 0
              ? `Chỉ còn ${props.remaining} việc nữa là hoàn thành ngày!`
              : props.total > 0
                ? 'Tuyệt vời — con đã hoàn thành ngày rồi!'
                : 'Chưa có kế hoạch hôm nay.'}
          </p>
          <div className="kplan-hero-gift" aria-hidden>
            <img src="/mascot/gift-soft.png?v=4" alt="" width={96} height={96} decoding="async" />
          </div>
          <button type="button" className="kplan-hero-gift-btn" onClick={props.onOpenRewards}>
            Xem quà hôm nay 🎁
          </button>
        </aside>
      </div>

      {props.missionDoneError ? (
        <p className="kplan-error" role="alert">
          {props.missionDoneError}
        </p>
      ) : null}

      {/* Cuộc phiêu lưu hôm nay — timeline theo chặng */}
      <section className="kplan-adventure" aria-label="Cuộc phiêu lưu hôm nay">
        <header className="kplan-sec-head">
          <h2>
            <span aria-hidden>🗺️</span> Cuộc phiêu lưu hôm nay
          </h2>
        </header>

        {groups.length === 0 ? (
          <p className="kplan-empty">Chưa có việc trong kế hoạch hôm nay.</p>
        ) : (
          <div className="kplan-stages">
            {groups.map((group) => {
              const expanded = openParts[group.key];
              const pending = group.open.length;
              const doneN = group.done.length;
              return (
                <section
                  key={group.key}
                  className={`kplan-stage is-${group.key}${expanded ? ' is-open' : ''}${
                    group.allDone ? ' is-cleared' : ''
                  }${group.key === nextPart ? ' is-current' : ''}`}
                >
                  <button
                    type="button"
                    className="kplan-stage-toggle"
                    aria-expanded={expanded}
                    onClick={() => togglePart(group.key)}
                  >
                    <span className="kplan-stage-emoji" aria-hidden>
                      {group.allDone ? '✓' : group.emoji}
                    </span>
                    <span className="kplan-stage-copy">
                      <strong>{group.stage}</strong>
                      <em>
                        {group.windowHint}
                        {' · '}
                        {group.allDone
                          ? `${doneN}/${group.items.length} · Đã chinh phục`
                          : `${doneN}/${group.items.length} · còn ${pending} việc`}
                      </em>
                    </span>
                    <i aria-hidden>{expanded ? '⌃' : '⌄'}</i>
                  </button>

                  {expanded ? (
                    <ul className="kplan-stage-list">
                      {group.items.map((item) => {
                        const mark = stepMarkOf(item, nextId);
                        const badge = stepBadge(mark);
                        const clock = stepClock(item) || props.clockLabel(item);
                        const actionable = mark === 'next' || mark === 'todo';
                        const busyThis =
                          props.busyId === item.id || (props.uploading && actionable);
                        return (
                          <li key={item.id} className={`kplan-step-wrap is-${mark}`}>
                            <button
                              type="button"
                              className={`kplan-step is-${mark}${
                                actionable ? ' is-action' : ' is-idle'
                              }`}
                              disabled={busyThis && actionable}
                              onClick={() => {
                                if (actionable) props.onStartItem(item);
                              }}
                              aria-label={
                                actionable
                                  ? `${mark === 'next' ? 'Bắt đầu' : 'Làm'} ${item.title}`
                                  : mark === 'wait'
                                    ? `${item.title} — chờ xác nhận`
                                    : `${item.title} — đã xong`
                              }
                            >
                              <span className="kplan-step-dot" aria-hidden>
                                {stepGlyph(mark)}
                              </span>
                              <span
                                className={`kplan-step-ico tone-${props.taskIconTone(item.title)}`}
                                aria-hidden
                              >
                                {props.taskIcon(item.title)}
                              </span>
                              <strong className="kplan-step-title">{item.title}</strong>
                              <time className="kplan-step-time">{clock}</time>
                              <span className={`kplan-step-badge tone-${badge.tone}`}>
                                {badge.label}
                              </span>
                            </button>
                          </li>
                        );
                      })}
                    </ul>
                  ) : null}
                </section>
              );
            })}
          </div>
        )}

        {doneItems.length > 0 ? (
          <div className={`kplan-done-sum${doneOpen ? ' is-open' : ''}`}>
            <button
              type="button"
              className="kplan-done-sum-toggle"
              aria-expanded={doneOpen}
              onClick={() => setDoneOpen((v) => !v)}
            >
              <span className="kplan-done-sum-mark" aria-hidden>
                📋
              </span>
              <span className="kplan-done-sum-copy">
                <strong>Đã hoàn thành 🎉</strong>
                <em>
                  {doneItems.length === props.total && props.total > 0
                    ? `Tuyệt vời! Con đã hoàn thành cả ngày · ${doneItems.length} việc`
                    : `Tuyệt vời! Con đã hoàn thành ${doneItems.length} việc`}
                </em>
              </span>
              <span className="kplan-done-sum-icons" aria-hidden>
                {donePreview.map((item) => (
                  <span
                    key={item.id}
                    className={`kplan-done-sum-ico tone-${props.taskIconTone(item.title)}`}
                    title={item.title}
                  >
                    {props.taskIcon(item.title)}
                  </span>
                ))}
                {doneExtra > 0 ? (
                  <span className="kplan-done-sum-ico is-more">+{doneExtra}</span>
                ) : null}
              </span>
              <i aria-hidden>{doneOpen ? '⌃' : '>'}</i>
            </button>
            {doneOpen ? (
              <ul className="kplan-done-sum-list">
                {doneItems.map((item) => (
                  <li key={item.id}>
                    <span
                      className={`kplan-done-sum-ico tone-${props.taskIconTone(item.title)}`}
                      aria-hidden
                    >
                      {props.taskIcon(item.title)}
                    </span>
                    <strong>{item.title}</strong>
                    <time>{stepClock(item) || props.clockLabel(item)}</time>
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}
      </section>

      {/* Challenge — star track + quà */}
      <section className="kplan-challenge" aria-label="Thử thách tuần này">
        <div className="kplan-challenge-main">
          <header className="kplan-challenge-head">
            <span className="kplan-challenge-mark" aria-hidden>
              📋
            </span>
            <div className="kplan-challenge-copy">
              <strong>Thử thách tuần này 🏆</strong>
              <em>Kiên trì mỗi ngày – Tích lũy điểm thưởng!</em>
            </div>
            <b className="kplan-challenge-score">
              {props.challengeHave} / {props.challengeNeed}
            </b>
          </header>

          <div
            className="kplan-challenge-stars"
            aria-hidden
            style={{ ['--slots' as string]: String(challengeSlots) }}
          >
            <i className="kplan-challenge-rail" />
            {Array.from({ length: challengeSlots }, (_, i) => {
              const state =
                i < challengeFilled ? 'done' : i === challengeFilled && !challengeComplete ? 'now' : 'todo';
              return (
                <span key={i} className={`kplan-challenge-star is-${state}`}>
                  ★
                </span>
              );
            })}
          </div>
        </div>

        <aside className="kplan-challenge-prize">
          <div className="kplan-challenge-gift" aria-hidden>
            <img src="/mascot/gift-soft.png?v=4" alt="" width={88} height={88} decoding="async" />
          </div>
          <button type="button" className="kplan-challenge-btn" onClick={props.onOpenRewards}>
            Xem phần thưởng
          </button>
        </aside>

        {props.challengeSlot ? (
          <div className="kplan-challenge-slot">{props.challengeSlot}</div>
        ) : null}
      </section>

      <aside className="kplan-nudge" aria-label="Fami nhắc nhẹ">
        <div className="kplan-nudge-fami" aria-hidden>
          <img src="/mascot/fami-robot.png?v=4" alt="" width={112} height={112} decoding="async" />
        </div>
        <div className="kplan-nudge-body">
          <strong>
            Fami nhắc nhẹ <span aria-hidden>🔊</span>
          </strong>
          <p>{props.famiLine}</p>
        </div>
        <button type="button" className="kplan-nudge-cta" onClick={props.onTalkFami}>
          💬 Nhắn với Fami ›
        </button>
      </aside>
    </div>
  );
}
