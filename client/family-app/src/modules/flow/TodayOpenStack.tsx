import { useEffect, useMemo, useRef, useState } from 'react';
import {
  TINY_RITUAL_MOODS,
  TINY_RITUAL_WARM_CHIPS_KID,
  TINY_RITUAL_WARM_CHIPS_PARENT,
  type MemoryYarn,
  type PendingAction,
  type SeenSignal,
  type TodayOpenCtaKind,
  type WarmthPulse,
} from '@/modules/flow/todayOpenSequence';
import { trackTodayOpen } from '@/modules/flow/todayOpenMetrics';

export type TodayOpenCtaEvent = {
  kind: TodayOpenCtaKind;
  id?: string;
};

type Props = {
  role: 'parent' | 'child';
  warmth: WarmthPulse | null;
  pending: PendingAction[];
  seen: SeenSignal[];
  yarn?: MemoryYarn | null;
  ritualDone: boolean;
  ritualBusy?: boolean;
  onCta: (ev: TodayOpenCtaEvent) => void;
  onRitualComplete: (moodCode: string, warmLineVi: string) => void | Promise<void>;
  onDismissYarn?: () => void;
};

export function TodayOpenStack({
  role,
  warmth,
  pending,
  seen,
  yarn = null,
  ritualDone,
  ritualBusy,
  onCta,
  onRitualComplete,
  onDismissYarn,
}: Props) {
  const chips = role === 'parent' ? TINY_RITUAL_WARM_CHIPS_PARENT : TINY_RITUAL_WARM_CHIPS_KID;
  const [moodIdx, setMoodIdx] = useState(3);
  const [warmIdx, setWarmIdx] = useState(0);
  const mood = TINY_RITUAL_MOODS[moodIdx] ?? TINY_RITUAL_MOODS[3];
  const warmLine = chips[warmIdx] ?? chips[0];
  const tracked = useRef({ warmth: '', seen: '', yarn: '', ritual: false });

  const hasBody = Boolean(warmth || pending.length || seen.length || yarn || !ritualDone);
  const pendingTitle = useMemo(
    () => (role === 'parent' ? 'Việc chỉ bạn làm được' : 'Việc đang chờ bạn'),
    [role],
  );

  useEffect(() => {
    if (warmth && tracked.current.warmth !== warmth.id) {
      tracked.current.warmth = warmth.id;
      trackTodayOpen('warmth_shown', { role, id: warmth.id });
    }
  }, [warmth, role]);

  useEffect(() => {
    const key = seen.map((s) => s.id).join('|');
    if (seen.length && tracked.current.seen !== key) {
      tracked.current.seen = key;
      trackTodayOpen('seen_shown', { role, count: seen.length });
    }
  }, [seen, role]);

  useEffect(() => {
    if (yarn && tracked.current.yarn !== yarn.id) {
      tracked.current.yarn = yarn.id;
      trackTodayOpen('memory_yarn_shown', { role, id: yarn.id });
    }
  }, [yarn, role]);

  if (!hasBody) return null;

  const emit = (ev: TodayOpenCtaEvent) => {
    if (ev.kind === 'dismiss' || ev.kind === 'dismiss_thanks') {
      trackTodayOpen('warmth_dismiss', { role, id: ev.id });
    } else if (ev.kind === 'ack_parent_voice' || ev.kind === 'ack_partner_voice') {
      trackTodayOpen('ack_voice', { role, id: ev.id });
    } else if (
      ev.kind === 'verify_evidence' ||
      ev.kind === 'approve_stars' ||
      ev.kind === 'open_voice' ||
      ev.kind === 'scroll_missions'
    ) {
      trackTodayOpen('pending_tap', { role, kind: ev.kind, id: ev.id });
    } else if (ev.kind === 'open_memory') {
      trackTodayOpen('memory_yarn_open', { role, id: ev.id });
    }
    onCta(ev);
  };

  return (
    <section className="tos-stack" aria-label="Mở nhà ấm">
      {warmth ? (
        <article className="tos-warmth" aria-label="Khoảnh khắc ấm">
          <p className="tos-eyebrow">
            <span aria-hidden>{warmth.icon}</span> {warmth.eyebrowVi}
          </p>
          <h2 className="tos-title">{warmth.titleVi}</h2>
          <p className="tos-body">{warmth.bodyVi}</p>
          <div className="tos-actions">
            <button
              type="button"
              className="tos-btn is-primary"
              onClick={() => emit({ kind: warmth.cta, id: warmth.ctaId })}
            >
              {warmth.ctaLabelVi}
            </button>
            <button
              type="button"
              className="tos-btn is-ghost"
              onClick={() => emit({ kind: 'dismiss', id: warmth.id })}
            >
              Để sau
            </button>
          </div>
        </article>
      ) : null}

      {pending.length > 0 ? (
        <article className="tos-pending" aria-label={pendingTitle}>
          <p className="tos-eyebrow">
            <span aria-hidden>❤️</span> {pendingTitle}
            <em>{pending.length}</em>
          </p>
          <ul className="tos-pending-list">
            {pending.map((p) => (
              <li key={p.id}>
                <button
                  type="button"
                  className="tos-pending-item"
                  onClick={() => emit({ kind: p.cta, id: p.ctaId })}
                >
                  <span className="tos-pending-icon" aria-hidden>
                    {p.icon}
                  </span>
                  <span className="tos-pending-copy">
                    <strong>{p.titleVi}</strong>
                    <em>{p.detailVi}</em>
                  </span>
                  <span className="tos-pending-cta">{p.ctaLabelVi}</span>
                </button>
              </li>
            ))}
          </ul>
        </article>
      ) : null}

      {seen.length > 0 ? (
        <article className="tos-seen" aria-label="Được nhìn thấy">
          <p className="tos-eyebrow">
            <span aria-hidden>👀</span> Hiện diện nhà mình
          </p>
          <ul className="tos-seen-list">
            {seen.map((s) => (
              <li key={s.id}>
                <span aria-hidden>{s.icon}</span>
                <span>{s.textVi}</span>
              </li>
            ))}
          </ul>
        </article>
      ) : null}

      {yarn ? (
        <article className="tos-yarn" aria-label="Sợi nhớ">
          <p className="tos-eyebrow">
            <span aria-hidden>{yarn.icon}</span> {yarn.eyebrowVi}
          </p>
          <p className="tos-yarn-text">{yarn.textVi}</p>
          <div className="tos-actions">
            <button
              type="button"
              className="tos-btn is-primary"
              onClick={() => emit({ kind: 'open_memory', id: yarn.id })}
            >
              {yarn.ctaLabelVi}
            </button>
            {onDismissYarn ? (
              <button type="button" className="tos-btn is-ghost" onClick={onDismissYarn}>
                Để sau
              </button>
            ) : null}
          </div>
        </article>
      ) : null}

      {!ritualDone ? (
        <article className="tos-ritual" aria-label="Nghi thức 30 giây">
          <p className="tos-eyebrow">
            <span aria-hidden>🕊️</span> Nghi thức 30 giây
          </p>
          <p className="tos-ritual-lead">
            {role === 'parent'
              ? 'Chọn tâm trạng + một câu ấm — gửi tín hiệu nhà đang gần nhau.'
              : 'Cho nhà biết bạn đang thế nào + một câu ấm gửi bố mẹ.'}
          </p>
          <div className="tos-mood-row" role="group" aria-label="Tâm trạng">
            {TINY_RITUAL_MOODS.map((m, idx) => (
              <button
                key={m.code}
                type="button"
                className={'tos-mood' + (idx === moodIdx ? ' is-on' : '')}
                onClick={() => setMoodIdx(idx)}
                aria-pressed={idx === moodIdx}
                title={m.label}
              >
                <span aria-hidden>{m.emoji}</span>
              </button>
            ))}
          </div>
          <div className="tos-chip-row" role="group" aria-label="Câu ấm">
            {chips.map((c, idx) => (
              <button
                key={c}
                type="button"
                className={'tos-chip' + (idx === warmIdx ? ' is-on' : '')}
                onClick={() => setWarmIdx(idx)}
                aria-pressed={idx === warmIdx}
              >
                {c}
              </button>
            ))}
          </div>
          <button
            type="button"
            className="tos-btn is-primary tos-ritual-submit"
            disabled={ritualBusy}
            onClick={() => {
              trackTodayOpen('ritual_done', { role, mood: mood.code });
              void onRitualComplete(mood.code, warmLine);
            }}
          >
            {ritualBusy ? 'Đang gửi…' : 'Gửi ấm cho nhà'}
          </button>
        </article>
      ) : null}
    </section>
  );
}