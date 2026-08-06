import { useEffect, useMemo, useState, type ReactNode } from 'react';
import type {
  DayFlowCommitment,
  FamilyRitual,
  TeamUnlock,
} from '@/shared/api/family-os.api';
import { QuickNudgeButton } from '@/shared/ui/QuickNudgeButton';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';
import { canRemindChildNow, remindChildIdleLabel } from '@/shared/reminders/remind-window';
import type { PlanCalendarItem } from '@/modules/flow/planGroups';
import { unlockStatusLabelVi } from '@/modules/flow/planGroups';
import { shortPersonName } from '@/shared/voice/family-voice';

type PlanGroup = 'today' | 'routine' | 'challenge' | 'calendar';
export type PlanMissionFilter = 'all' | 'need_help' | 'waiting_child' | 'done';

type Props = {
  flowDate: string;
  localTime?: string;
  familyId: string;
  parentHelloLabel: string;
  parentRole: string;
  childFocusLabel: string;
  /** When viewing whole family, surface member name under each task title. */
  showMemberOnRow?: boolean;
  hasChildren: boolean;
  childPicker: ReactNode;
  noChildNotice: ReactNode;
  attentionCount?: number;
  onOpenAttention?: () => void;
  scopedDone: number;
  scopedTotal: number;
  needHelpItems: DayFlowCommitment[];
  waitingChildItems: DayFlowCommitment[];
  doneTodayItems: DayFlowCommitment[];
  rituals: FamilyRitual[];
  todayUnlock: TeamUnlock | null;
  challengeRows?: TeamUnlock[];
  calendarItems?: PlanCalendarItem[];
  calendarSummary?: string;
  busyId: string | null;
  verifyingId: string | null;
  ritualBusy: string | null;
  missionFilter?: PlanMissionFilter;
  onOpenVerify: (item: DayFlowCommitment) => void;
  onParentNudged: (count?: number) => void;
  onToast: (msg: string) => void;
  onOpenChallenge: () => void;
  onOpenDiary: () => void;
  onMarkRitual: (code: string) => void;
  onOpenModeSheet: () => void;
  taskIcon: (title: string) => string;
  warmTip: (item: DayFlowCommitment, who: string) => string;
  warmSupport: (
    item: DayFlowCommitment,
    who: string,
    kind: 'awaiting' | 'overdue',
  ) => string;
  needsParentCheck: (item: DayFlowCommitment) => boolean;
};

function minutesUntilEnd(item: DayFlowCommitment, localTime?: string): number | null {
  if (!item.windowEnd || !localTime) return null;
  const [eh, em] = item.windowEnd.slice(0, 5).split(':').map(Number);
  const [nh, nm] = localTime.slice(0, 5).split(':').map(Number);
  if (![eh, em, nh, nm].every(Number.isFinite)) return null;
  return eh * 60 + em - (nh * 60 + nm);
}

function statusPill(
  item: DayFlowCommitment,
  localTime?: string,
): { label: string; tone: 'danger' | 'warn' | 'ok' | 'muted' } {
  if (item.reminderState === 'overdue') return { label: 'Quá hạn', tone: 'danger' };
  const mins = minutesUntilEnd(item, localTime);
  if (mins != null && mins <= 0) return { label: 'Quá hạn', tone: 'danger' };
  if (mins != null && mins <= 60) return { label: 'Còn 1 giờ', tone: 'warn' };
  if (mins != null && mins <= 120) {
    return { label: `Còn ${Math.max(1, Math.ceil(mins / 60))} giờ`, tone: 'warn' };
  }
  if (item.reminderState === 'due_now') return { label: 'Đến giờ', tone: 'warn' };
  if (item.windowStart) return { label: item.windowStart.slice(0, 5), tone: 'muted' };
  return { label: 'Sắp tới', tone: 'muted' };
}

function timeLabel(item: DayFlowCommitment): string {
  if (item.windowStart) return item.windowStart.slice(0, 5);
  if (item.windowEnd) return item.windowEnd.slice(0, 5);
  return '—';
}

function ritualCadenceLabel(cadence: string): string {
  const c = cadence.trim().toLowerCase();
  if (c === 'daily' || c === 'day') return 'Mỗi ngày';
  if (c === 'weekly' || c === 'week') return 'Mỗi tuần';
  return cadence.trim() || 'Định kỳ';
}

export function ParentPlanPanel(props: Props) {
  const [group, setGroup] = useState<PlanGroup>('today');
  const [openPanel, setOpenPanel] = useState<PlanGroup | null>(null);
  const [priorityExpanded, setPriorityExpanded] = useState(false);
  const [nextExpanded, setNextExpanded] = useState(false);
  const [doneExpanded, setDoneExpanded] = useState(false);

  useEffect(() => {
    const filter = props.missionFilter ?? 'all';
    if (filter === 'need_help') setPriorityExpanded(true);
    if (filter === 'waiting_child') setNextExpanded(true);
    if (filter === 'done') setDoneExpanded(true);
  }, [props.missionFilter]);

  const remaining = Math.max(0, props.scopedTotal - props.scopedDone);
  const priorityCount = props.needHelpItems.length;
  const nextCount = props.waitingChildItems.length;
  const doneCount = props.doneTodayItems.length;
  const ritualsDone = props.rituals.filter((r) => r.doneThisPeriod).length;

  const greeting = useMemo(() => {
    const hour = Number((props.localTime || '12:00').slice(0, 2));
    const role = (props.parentHelloLabel || 'bố mẹ').trim();
    if (hour >= 17) return `Chào buổi tối, ${role} ơi!`;
    if (hour >= 12) return `Chào buổi chiều, ${role} ơi!`;
    return `Chào buổi sáng, ${role} ơi!`;
  }, [props.localTime, props.parentHelloLabel]);

  const goGroup = (id: PlanGroup) => {
    setGroup(id);
    if (id === 'today') {
      setOpenPanel(null);
      return;
    }
    setOpenPanel(id);
    window.requestAnimationFrame(() => {
      document.getElementById(`pp-acc-${id}`)?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    });
  };

  const renderRow = (
    item: DayFlowCommitment,
    mode: 'priority' | 'next',
  ) => {
    const who = item.memberName?.trim() || props.childFocusLabel;
    const whoShort = shortPersonName(who);
    const showWho = Boolean(props.showMemberOnRow && item.memberName?.trim());
    const emoji = avatarEmoji(inferGenderFromName(who), 'child');
    const pill = statusPill(item, props.localTime);
    const awaiting =
      mode === 'priority' && props.needsParentCheck(item) && Boolean(item.evidenceUrl);
    const kind: 'awaiting' | 'overdue' = awaiting ? 'awaiting' : 'overdue';
    return (
      <li key={item.id} className={`pp-row is-${mode}${showWho ? ' has-who' : ''}`}>
        <time className="pp-row-time">{timeLabel(item)}</time>
        <span className="pp-row-ico" aria-hidden>
          {props.taskIcon(item.title)}
        </span>
        <div className="pp-row-body">
          <strong>{item.title}</strong>
          {showWho ? (
            <span className="pp-row-who-line" aria-label={`Việc của ${who}`}>
              <i aria-hidden>{emoji}</i>
              <em>{whoShort}</em>
            </span>
          ) : null}
          <p>
            {mode === 'priority'
              ? props.warmSupport(item, who, kind)
              : props.warmTip(item, who)}
          </p>
        </div>
        {!showWho ? (
          <span className="pp-row-who" title={who} aria-label={`Việc của ${who}`}>
            {emoji}
          </span>
        ) : null}
        <div className="pp-row-side">
          <span className={`pp-pill is-${pill.tone}`}>{pill.label}</span>
          {awaiting ? (
            <button
              type="button"
              className="pp-row-cta"
              disabled={props.busyId === item.id || props.verifyingId === item.id}
              onClick={() => props.onOpenVerify(item)}
            >
              {props.busyId === item.id || props.verifyingId === item.id
                ? 'Đang…'
                : 'Xác nhận'}
            </button>
          ) : canRemindChildNow(item) ? (
            <QuickNudgeButton
              items={item}
              familyId={props.familyId}
              flowDate={props.flowDate}
              label="Nhắc"
              className="pp-row-cta is-soft"
              onNudged={(count) => {
                props.onParentNudged(count);
                props.onToast('Đã chuẩn bị tin nhắc — gửi Zalo/Messenger cho con');
              }}
            />
          ) : (
            <span className="pp-row-idle">{remindChildIdleLabel(item)}</span>
          )}
        </div>
      </li>
    );
  };

  const priorityShow = priorityExpanded ? props.needHelpItems : props.needHelpItems.slice(0, 3);
  const nextShow = nextExpanded ? props.waitingChildItems : props.waitingChildItems.slice(0, 4);
  const doneShow = doneExpanded ? props.doneTodayItems : props.doneTodayItems.slice(0, 6);
  const doneExtra = Math.max(0, doneCount - doneShow.length);

  return (
    <div className="pp-root" id="pp-plan">
      <header className="pp-top">
        <h1>Kế hoạch</h1>
        <div className="pp-top-actions">
          <button
            type="button"
            className="pp-ico-btn"
            aria-label="Mở nhật ký theo ngày"
            onClick={props.onOpenDiary}
          >
            📅
          </button>
          {props.onOpenAttention ? (
            <button
              type="button"
              className="pp-ico-btn pp-bell"
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

      <nav className="pp-rail" aria-label="Nhóm trong kế hoạch">
        {(
          [
            { id: 'today' as const, icon: '⭐', label: 'Việc hôm nay' },
            { id: 'routine' as const, icon: '🔁', label: 'Routine' },
            { id: 'challenge' as const, icon: '🎯', label: 'Challenge' },
            { id: 'calendar' as const, icon: '📆', label: 'Lịch gia đình' },
          ] as const
        ).map((g) => (
          <button
            key={g.id}
            type="button"
            className={`pp-rail-item${group === g.id ? ' is-on' : ''}`}
            onClick={() => goGroup(g.id)}
          >
            <span aria-hidden>{g.icon}</span>
            {g.label}
          </button>
        ))}
      </nav>

      <article className="pp-hero">
        <div className="pp-hero-copy">
          <p className="pp-hero-hi">☀️ {greeting} ☀️</p>
          <p className="pp-hero-sum">
            {remaining > 0 ? (
              <>
                Hôm nay gia đình còn <strong>{remaining} việc nữa</strong> để kết thúc một ngày
                thật đẹp 💚
              </>
            ) : props.scopedTotal > 0 ? (
              <>Hôm nay nhà mình đã giữ nhịp xong — tuyệt vời! 💚</>
            ) : (
              <>Chưa có việc trong kế hoạch hôm nay.</>
            )}
          </p>
          <div className="pp-hero-stats">
            <div>
              <strong className="is-ok">
                {props.scopedDone}/{Math.max(props.scopedTotal, 0)}
              </strong>
              <em>Đã hoàn thành</em>
            </div>
            <div>
              <strong className="is-warn">{priorityCount}</strong>
              <em>Cần ưu tiên</em>
            </div>
            <div>
              <strong>{remaining}</strong>
              <em>Còn lại</em>
            </div>
          </div>
        </div>
        <div className="pp-hero-art" aria-hidden>
          <span className="pp-hero-bubble is-a">👨</span>
          <span className="pp-hero-bubble is-b">👩</span>
          <span className="pp-hero-bubble is-c">👧</span>
          <span className="pp-hero-bubble is-d">👦</span>
        </div>
      </article>

      <section className="pp-sec" id="pp-priority">
        <header className="pp-sec-head">
          <h2>
            <span aria-hidden>🔥</span> Cần làm ngay
            <b>({priorityCount})</b>
          </h2>
          {priorityCount > 3 ? (
            <button
              type="button"
              className="pp-link"
              onClick={() => setPriorityExpanded((v) => !v)}
            >
              {priorityExpanded ? 'Thu gọn' : 'Xem tất cả'} ›
            </button>
          ) : null}
        </header>
        <ul className="pp-list">
          {priorityShow.map((item) => renderRow(item, 'priority'))}
          {priorityCount === 0 ? (
            <li className="pp-empty">Không có việc cần ưu tiên ngay.</li>
          ) : null}
        </ul>
      </section>

      <section className="pp-sec" id="pp-next">
        <header className="pp-sec-head">
          <h2>
            <span aria-hidden>⏱️</span> Tiếp theo
            <b>({nextCount})</b>
          </h2>
          {nextCount > 4 ? (
            <button type="button" className="pp-link" onClick={() => setNextExpanded((v) => !v)}>
              {nextExpanded ? 'Thu gọn' : 'Xem tất cả'} ›
            </button>
          ) : null}
        </header>
        <ul className="pp-list">
          {nextShow.map((item) => renderRow(item, 'next'))}
          {nextCount === 0 ? <li className="pp-empty">Chưa có việc tiếp theo.</li> : null}
        </ul>
      </section>

      <section className="pp-sec" id="pp-done">
        <header className="pp-sec-head">
          <h2>
            <span aria-hidden>✅</span> Đã hoàn thành
            <b>({doneCount})</b>
          </h2>
          {doneCount > 0 ? (
            <button type="button" className="pp-link" onClick={() => setDoneExpanded((v) => !v)}>
              {doneExpanded ? 'Thu gọn' : 'Xem lại'} ›
            </button>
          ) : null}
        </header>
        {doneCount === 0 ? (
          <p className="pp-empty soft">Hôm nay chưa có việc hoàn thành.</p>
        ) : (
          <div className="pp-done-row" role="list">
            {doneShow.map((item) => (
              <span
                key={item.id}
                className="pp-done-chip"
                role="listitem"
                title={item.title}
              >
                <i aria-hidden>{props.taskIcon(item.title)}</i>
                <em aria-hidden>✓</em>
              </span>
            ))}
            {doneExtra > 0 ? <span className="pp-done-more">+{doneExtra}</span> : null}
          </div>
        )}
        {doneExpanded ? (
          <ul className="pp-list is-done">
            {props.doneTodayItems.map((item) => {
              const who = item.memberName?.trim() || props.childFocusLabel;
              const whoShort = shortPersonName(who);
              const showWho = Boolean(props.showMemberOnRow && item.memberName?.trim());
              const emoji = avatarEmoji(inferGenderFromName(who), 'child');
              return (
                <li key={`done-${item.id}`} className="pp-row is-done">
                  <span className="pp-row-ico" aria-hidden>
                    {props.taskIcon(item.title)}
                  </span>
                  <div className="pp-row-body">
                    <strong>{item.title}</strong>
                    {showWho ? (
                      <span className="pp-row-who-line" aria-label={`Việc của ${who}`}>
                        <i aria-hidden>{emoji}</i>
                        <em>{whoShort}</em>
                      </span>
                    ) : (
                      <p>{who}</p>
                    )}
                  </div>
                  <span className="pp-pill is-ok">Xong</span>
                </li>
              );
            })}
          </ul>
        ) : null}
      </section>

      <div className="pp-acc">
        <details
          id="pp-acc-routine"
          className="pp-acc-item"
          open={openPanel === 'routine'}
          onToggle={(e) => {
            if ((e.target as HTMLDetailsElement).open) setOpenPanel('routine');
            else if (openPanel === 'routine') setOpenPanel(null);
          }}
        >
          <summary>
            <div>
              <strong>Routine hôm nay</strong>
              <em>
                {props.rituals.length > 0
                  ? `${ritualsDone}/${props.rituals.length} đã hoàn thành`
                  : 'Chưa có routine'}
              </em>
            </div>
            <span className="pp-acc-meta">
              <em className="pp-acc-go">Xem chi tiết</em>
              <i aria-hidden>▾</i>
            </span>
          </summary>
          <ul className="pp-soft-list">
            {props.rituals.length === 0 ? (
              <li className="pp-empty">Mở chế độ nhà để Famixa chỉnh nhịp routine.</li>
            ) : (
              props.rituals.map((r) => (
                <li key={r.code}>
                  <div>
                    <strong>{r.labelVi}</strong>
                    <em>
                      {ritualCadenceLabel(r.cadence)}
                      {r.doneThisPeriod ? ' · đã xong' : ' · đang chờ'}
                    </em>
                  </div>
                  {r.doneThisPeriod ? (
                    <span className="pp-check">✓</span>
                  ) : (
                    <button
                      type="button"
                      className="pp-row-cta is-soft"
                      disabled={props.ritualBusy === r.code}
                      onClick={() => props.onMarkRitual(r.code)}
                    >
                      {props.ritualBusy === r.code ? 'Đang…' : 'Check-in'}
                    </button>
                  )}
                </li>
              ))
            )}
          </ul>
          <button type="button" className="pp-link block" onClick={props.onOpenModeSheet}>
            Đổi chế độ nhà →
          </button>
        </details>

        <details
          id="pp-acc-challenge"
          className="pp-acc-item"
          open={openPanel === 'challenge'}
          onToggle={(e) => {
            if ((e.target as HTMLDetailsElement).open) setOpenPanel('challenge');
            else if (openPanel === 'challenge') setOpenPanel(null);
          }}
        >
          <summary>
            <div>
              <strong>Challenge đang tham gia</strong>
              <em>
                {(props.challengeRows?.length ?? 0) > 0
                  ? `${props.challengeRows!.length} challenge đang theo dõi`
                  : props.todayUnlock
                    ? `${props.todayUnlock.labelVi || 'Challenge'} · ${props.todayUnlock.teamPercent}%`
                    : 'Chưa có challenge đang chạy'}
              </em>
            </div>
            <span className="pp-acc-meta">
              <em className="pp-acc-go">Xem chi tiết</em>
              <i aria-hidden>▾</i>
            </span>
          </summary>
          <div className="pp-challenge">
            {props.todayUnlock ? (
              <>
                <strong>{props.todayUnlock.labelVi || 'Challenge'}</strong>
                <p>
                  {props.todayUnlock.teamDone}/{Math.max(props.todayUnlock.teamTotal, 1)} thành viên
                </p>
                <div className="pp-bar" aria-hidden>
                  <b style={{ width: `${Math.min(100, props.todayUnlock.teamPercent)}%` }} />
                </div>
              </>
            ) : null}
            {(props.challengeRows ?? []).length > 0 ? (
              <ul className="pp-soft-list">
                {(props.challengeRows ?? []).slice(0, 4).map((u) => (
                  <li key={`${u.rewardCode}-${u.flowDate}`}>
                    <div>
                      <strong>{u.labelVi || u.rewardCode}</strong>
                      <em>
                        {unlockStatusLabelVi(u.status)} · {u.teamDone}/
                        {Math.max(u.teamTotal, 1)}
                      </em>
                    </div>
                    <span className="pp-check">{u.teamPercent}%</span>
                  </li>
                ))}
              </ul>
            ) : !props.todayUnlock ? (
              <p>Chọn hoạt động gia đình để giữ nhịp kết nối.</p>
            ) : null}
            <button type="button" className="pp-row-cta" onClick={props.onOpenChallenge}>
              Mở Challenge →
            </button>
          </div>
        </details>

        <details
          id="pp-acc-calendar"
          className="pp-acc-item"
          open={openPanel === 'calendar'}
          onToggle={(e) => {
            if ((e.target as HTMLDetailsElement).open) setOpenPanel('calendar');
            else if (openPanel === 'calendar') setOpenPanel(null);
          }}
        >
          <summary>
            <div>
              <strong>Lịch gia đình</strong>
              <em>
                {(props.calendarItems?.length ?? 0) > 0
                  ? `${props.calendarItems!.length} sự kiện sắp tới`
                  : props.calendarSummary || 'Sự kiện & nhịp đời nhà'}
              </em>
            </div>
            <span className="pp-acc-meta">
              <em className="pp-acc-go">Xem chi tiết</em>
              <i aria-hidden>▾</i>
            </span>
          </summary>
          <div className="pp-challenge">
            {(props.calendarItems ?? []).length > 0 ? (
              <ul className="pp-soft-list">
                {(props.calendarItems ?? []).slice(0, 6).map((item) => (
                  <li key={item.id}>
                    <div>
                      <strong>{item.titleVi}</strong>
                      <em>
                        {item.whenVi}
                        {item.metaVi ? ` · ${item.metaVi}` : ''}
                      </em>
                    </div>
                    <span aria-hidden>
                      {item.kind === 'birthday'
                        ? '🎂'
                        : item.kind === 'period'
                          ? '🌿'
                          : item.kind === 'study'
                            ? '📚'
                            : '📌'}
                    </span>
                  </li>
                ))}
              </ul>
            ) : (
              <p>
                Chưa có mốc lịch — thêm ngày sinh hoặc bật chế độ nhà (hè / du lịch).
              </p>
            )}
            <button type="button" className="pp-row-cta is-soft" onClick={props.onOpenDiary}>
              Mở Nhật ký theo ngày →
            </button>
            <button type="button" className="pp-link block" onClick={props.onOpenModeSheet}>
              Đổi nhịp lịch nhà →
            </button>
          </div>
        </details>
      </div>
    </div>
  );
}
