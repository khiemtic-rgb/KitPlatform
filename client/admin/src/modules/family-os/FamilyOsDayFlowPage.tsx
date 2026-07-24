import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button, Modal, Select, message } from 'antd';
import {
  AppstoreOutlined,
  CheckOutlined,
  HeartOutlined,
  QuestionCircleOutlined,
  ReloadOutlined,
  RightOutlined,
  UndoOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  SKIP_REASON_OPTIONS,
  decideConsequenceEvent,
  ensureDayFlow,
  fetchAccountabilityGlance,
  fetchCoachInsight,
  fetchConsequenceEvents,
  fetchFamilies,
  fetchTeamUnlocks,
  skipReasonLabel,
  updateCommitmentProgress,
  type AccountabilityGlance,
  type ConsequenceEvent,
  type DayFlow,
  type DayFlowCommitment,
  type FamilyCoachInsight,
  type FamilySummary,
  type TeamUnlock,
} from '@/shared/api/family-os.api';
import './family-os-dayflow.css';

const WEEKDAYS_VI = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];

function formatWindow(start?: string, end?: string): string {
  const clean = (v?: string) => (v ? v.slice(0, 5) : '');
  if (start && end) return `${clean(start)} – ${clean(end)}`;
  return clean(start || end) || '—';
}

function formatFlowDay(flowDate: string): string {
  const d = new Date(`${flowDate}T12:00:00`);
  if (Number.isNaN(d.getTime())) return flowDate;
  const weekday = WEEKDAYS_VI[d.getDay()] ?? '';
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  return `${weekday}, ${dd}/${mm}`;
}

function formatCompletedAt(value?: string): string | null {
  if (!value) return null;
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return null;
  return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
}

function formatDayChip(dateIso: string): string {
  return String(dateIso).slice(5);
}

function taskIcon(title: string): string {
  const t = title.toLowerCase();
  if (t.includes('dậy') || t.includes('thức')) return '☀️';
  if (t.includes('đánh răng') || t.includes('rang')) return '🪥';
  if (t.includes('ăn') || t.includes('cơm') || t.includes('bữa')) return '🥣';
  if (t.includes('đồng phục') || t.includes('mặc')) return '👕';
  if (t.includes('cặp') || t.includes('balo')) return '🎒';
  if (t.includes('bài tập') || t.includes('học')) return '📘';
  if (t.includes('đọc') || t.includes('sách')) return '📖';
  if (t.includes('tắm') || t.includes('rửa')) return '🚿';
  if (t.includes('ngủ')) return '🌙';
  if (t.includes('dọn') || t.includes('phòng')) return '🧹';
  return '⭐';
}

function statusMeta(item: DayFlowCommitment): { label: string; tone: string } {
  if (item.status === 'done') {
    return item.isLateDone
      ? { label: 'Xong muộn', tone: 'late' }
      : { label: 'Đã xong', tone: 'done' };
  }
  if (item.status === 'skipped') return { label: 'Bỏ qua', tone: 'skip' };
  if (item.reminderState === 'overdue') return { label: 'Quá giờ', tone: 'overdue' };
  if (item.reminderState === 'due_now') return { label: 'Đến giờ', tone: 'due' };
  if (item.reminderState === 'upcoming') return { label: 'Sắp tới', tone: 'soon' };
  return { label: 'Đang chờ', tone: 'wait' };
}

type MemberGroup = {
  key: string;
  name: string;
  items: DayFlowCommitment[];
  doneOnly: number;
  total: number;
};

function groupByMember(commitments: DayFlowCommitment[]): MemberGroup[] {
  const map = new Map<string, MemberGroup>();
  for (const c of commitments) {
    const key = c.memberId ?? c.memberName ?? '__house__';
    const name = c.memberName?.trim() || 'Cả nhà';
    let g = map.get(key);
    if (!g) {
      g = { key, name, items: [], doneOnly: 0, total: 0 };
      map.set(key, g);
    }
    g.items.push(c);
    g.total += 1;
    if (c.status === 'done') g.doneOnly += 1;
  }
  return [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

/** Always surface every child — even with 0 Mission (Team Play). */
function buildChildGroups(
  children: { id: string; displayName: string }[],
  commitments: DayFlowCommitment[],
): MemberGroup[] {
  const fromFlow = groupByMember(commitments);
  const byKey = new Map(fromFlow.map((g) => [g.key, g]));
  const result: MemberGroup[] = children.map((ch) => {
    const hit = byKey.get(ch.id);
    if (hit) {
      byKey.delete(ch.id);
      return { ...hit, name: ch.displayName || hit.name };
    }
    return {
      key: ch.id,
      name: ch.displayName,
      items: [],
      doneOnly: 0,
      total: 0,
    };
  });
  // House / unassigned leftovers
  for (const g of byKey.values()) {
    if (g.key !== '__house__') result.push(g);
  }
  return result.sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

function memberEmoji(name: string): string {
  const n = name.toLowerCase();
  if (n.includes('nhi') || n.includes('linh') || n === 'mẹ') return '👧';
  if (n.includes('huy') || n.includes('đức') || n === 'bố') return '👦';
  return '🧒';
}

export function FamilyOsDayFlowPage() {
  const [loading, setLoading] = useState(true);
  const [family, setFamily] = useState<FamilySummary | null>(null);
  const [flow, setFlow] = useState<DayFlow | null>(null);
  const [events, setEvents] = useState<ConsequenceEvent[]>([]);
  const [glance, setGlance] = useState<AccountabilityGlance | null>(null);
  const [coach, setCoach] = useState<FamilyCoachInsight | null>(null);
  const [teamUnlocks, setTeamUnlocks] = useState<TeamUnlock[]>([]);
  const [skipTarget, setSkipTarget] = useState<DayFlowCommitment | null>(null);
  const [skipReason, setSkipReason] = useState<string>('forgot');
  const [saving, setSaving] = useState(false);
  const [memberFilter, setMemberFilter] = useState<string>('all');
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');

  const familyId = family?.id ?? null;
  const guardians = (family?.members ?? []).filter((m) => m.roleCode !== 'child');
  const childMembers = useMemo(
    () =>
      (family?.members ?? [])
        .filter((m) => m.roleCode === 'child' && m.status !== 'archived')
        .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)),
    [family],
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const families = await fetchFamilies();
      const first = families[0] ?? null;
      setFamily(first);
      if (!first) {
        setFlow(null);
        setEvents([]);
        setGlance(null);
        setCoach(null);
        setTeamUnlocks([]);
        return;
      }
      const day = await ensureDayFlow(first.id);
      setFlow(day);
      const [ev, gl, insight, unlockRows] = await Promise.all([
        fetchConsequenceEvents(first.id, { flowDate: day.flowDate }),
        fetchAccountabilityGlance(first.id),
        fetchCoachInsight(first.id, day.flowDate),
        fetchTeamUnlocks(first.id, day.flowDate, true).then(() =>
          fetchTeamUnlocks(first.id),
        ),
      ]);
      setEvents(ev);
      setGlance(gl);
      setCoach(insight);
      setTeamUnlocks(unlockRows);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được Hôm nay'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!familyId) return;
    const timer = window.setInterval(() => {
      void (async () => {
        try {
          const day = await ensureDayFlow(familyId);
          setFlow(day);
          const [ev, gl, insight, unlockRows] = await Promise.all([
            fetchConsequenceEvents(familyId, { flowDate: day.flowDate }),
            fetchAccountabilityGlance(familyId),
            fetchCoachInsight(familyId, day.flowDate),
            fetchTeamUnlocks(familyId, day.flowDate, true).then(() =>
              fetchTeamUnlocks(familyId),
            ),
          ]);
          setEvents(ev);
          setGlance(gl);
          setCoach(insight);
          setTeamUnlocks(unlockRows);
        } catch {
          /* ignore poll errors */
        }
      })();
    }, 60_000);
    return () => window.clearInterval(timer);
  }, [familyId]);

  const refreshFlow = async () => {
    if (!familyId) return;
    const day = await ensureDayFlow(familyId);
    setFlow(day);
    const [ev, gl, insight, unlockRows] = await Promise.all([
      fetchConsequenceEvents(familyId, { flowDate: day.flowDate }),
      fetchAccountabilityGlance(familyId),
      fetchCoachInsight(familyId, day.flowDate),
      fetchTeamUnlocks(familyId, day.flowDate, true).then(() => fetchTeamUnlocks(familyId)),
    ]);
    setEvents(ev);
    setGlance(gl);
    setCoach(insight);
    setTeamUnlocks(unlockRows);
  };

  const markDone = async (commitmentId: string) => {
    if (!familyId) return;
    try {
      await updateCommitmentProgress(familyId, commitmentId, 'done');
      await refreshFlow();
      message.success('Đã đánh dấu xong');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không cập nhật được'));
    }
  };

  const markPending = async (commitmentId: string) => {
    if (!familyId) return;
    try {
      await updateCommitmentProgress(familyId, commitmentId, 'pending');
      await refreshFlow();
      message.success('Đã mở lại cam kết');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không cập nhật được'));
    }
  };

  const confirmSkip = async () => {
    if (!familyId || !skipTarget) return;
    setSaving(true);
    try {
      await updateCommitmentProgress(familyId, skipTarget.id, 'skipped', skipReason);
      setSkipTarget(null);
      await refreshFlow();
      message.success('Đã ghi lý do — kiểm tra xác nhận phụ huynh nếu có thỏa thuận khớp');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không ghi nhận được'));
    } finally {
      setSaving(false);
    }
  };

  const decideEvent = async (event: ConsequenceEvent, status: 'applied' | 'waived') => {
    if (!familyId) return;
    const decidedBy = guardians[0]?.id;
    if (!decidedBy) {
      message.error('Cần ít nhất một phụ huynh để xác nhận');
      return;
    }
    try {
      await decideConsequenceEvent(familyId, event.id, {
        status,
        decidedBy,
        decisionNote: status === 'applied' ? 'Áp dụng trên Admin' : 'Bỏ qua trên Admin',
      });
      message.success(status === 'applied' ? 'Đã áp dụng hậu quả' : 'Đã bỏ qua');
      await refreshFlow();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không quyết định được'));
    }
  };

  const pendingByCommitment = useMemo(() => {
    const map = new Map<string, ConsequenceEvent[]>();
    for (const e of events.filter((x) => x.status === 'pending_confirm')) {
      const list = map.get(e.commitmentId) ?? [];
      list.push(e);
      map.set(e.commitmentId, list);
    }
    return map;
  }, [events]);

  const groups = useMemo(
    () =>
      flow
        ? buildChildGroups(
            childMembers.map((m) => ({ id: m.id, displayName: m.displayName })),
            flow.commitments,
          )
        : buildChildGroups(
            childMembers.map((m) => ({ id: m.id, displayName: m.displayName })),
            [],
          ),
    [flow, childMembers],
  );
  const visibleGroups = useMemo(
    () => (memberFilter === 'all' ? groups : groups.filter((g) => g.key === memberFilter)),
    [groups, memberFilter],
  );

  const doneCount = flow?.doneCount ?? 0;
  const total = flow?.totalCommitments ?? 0;
  const skippedCount = flow
    ? flow.commitments.filter((c) => c.status === 'skipped').length
    : 0;
  const remaining = flow?.pendingCount ?? 0;
  const doneRatio = total > 0 ? Math.round((doneCount / total) * 100) : 0;
  const dayEnd =
    flow?.commitments
      .map((c) => c.windowEnd)
      .filter(Boolean)
      .sort()
      .at(-1)
      ?.slice(0, 5) ?? null;

  const focusChild =
    groups.find((g) => g.key === coach?.focusMemberId && g.total > 0) ??
    groups.find((g) => g.total > 0) ??
    groups[0] ??
    null;

  // Keep "Tất cả" when there are 2+ children — don't collapse to one kid.
  useEffect(() => {
    if (childMembers.length <= 1 && memberFilter === 'all' && focusChild) {
      setMemberFilter(focusChild.key);
    }
  }, [childMembers.length, focusChild, memberFilter]);

  const todayUnlock = useMemo(
    () =>
      teamUnlocks.find((u) => u.flowDate === flow?.flowDate) ?? teamUnlocks[0] ?? null,
    [teamUnlocks, flow?.flowDate],
  );

  const renderTaskCard = (item: DayFlowCommitment) => {
    const pending = pendingByCommitment.get(item.id) ?? [];
    const completed = formatCompletedAt(item.completedAt);
    const open = item.status !== 'done' && item.status !== 'skipped';
    const status = statusMeta(item);
    return (
      <article key={item.id} className={`fd-task tone-${status.tone}${viewMode === 'list' ? ' is-list' : ''}`}>
        <span className={`fd-badge tone-${status.tone}`}>{status.label}</span>
        <div className="fd-task-icon" aria-hidden>
          {taskIcon(item.title)}
        </div>
        <div className="fd-task-body">
          <h3>{item.title}</h3>
          <p>{formatWindow(item.windowStart, item.windowEnd)}</p>
          {completed ? <p className="fd-done-at">xong lúc {completed}</p> : null}
          {item.status === 'skipped' && item.skipReason ? (
            <p className="fd-skip-reason">
              Lý do: {skipReasonLabel(item.skipReason) ?? item.skipReason}
            </p>
          ) : null}
          {item.starDelta != null && item.status === 'done' ? (
            <p className="fd-star-delta">
              {item.starDelta >= 0 ? '+' : ''}
              {item.starDelta}⭐
              {item.starLabelVi ? ` · ${item.starLabelVi}` : ''}
            </p>
          ) : item.projectedStarDelta != null && open ? (
            <p className="fd-star-delta is-projected">
              Dự kiến: {item.projectedStarDelta >= 0 ? '+' : ''}
              {item.projectedStarDelta}⭐
              {item.projectedStarLabelVi ? ` · ${item.projectedStarLabelVi}` : ''}
            </p>
          ) : null}
        </div>

        {pending.length > 0 ? (
          <div className="fd-pending">
            <span>Chờ phụ huynh · {pending[0].labelVi}</span>
            <div className="fd-pending-actions">
              <button type="button" onClick={() => void decideEvent(pending[0], 'applied')}>
                Áp dụng
              </button>
              <button type="button" onClick={() => void decideEvent(pending[0], 'waived')}>
                Bỏ qua
              </button>
              <button type="button" onClick={() => void markPending(item.id)}>
                Gia hạn
              </button>
            </div>
          </div>
        ) : null}

        <div className="fd-task-actions">
          {open ? (
            <>
              <button
                type="button"
                className="fd-btn primary"
                onClick={() => void markDone(item.id)}
              >
                <CheckOutlined /> Xong
              </button>
              <button
                type="button"
                className="fd-btn"
                onClick={() => {
                  setSkipReason('forgot');
                  setSkipTarget(item);
                }}
              >
                <QuestionCircleOutlined /> Chưa làm
              </button>
            </>
          ) : (
            <button type="button" className="fd-btn" onClick={() => void markPending(item.id)}>
              <UndoOutlined /> Mở lại
            </button>
          )}
        </div>
      </article>
    );
  };

  return (
    <div className={`fd-page${loading ? ' is-loading' : ''}`}>
      <header className="fd-header">
        <div>
          <h1>
            Hôm nay{flow ? ` · ${formatFlowDay(flow.flowDate)}` : ''}{' '}
            <span aria-hidden>☀️</span>
          </h1>
          <p>
            {flow
              ? `${flow.routineName}${dayEnd ? ` · Ngày kết thúc lúc ${dayEnd}` : ''}`
              : 'Nhịp sống trong ngày của cả gia đình'}
          </p>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          Làm mới
        </Button>
      </header>

      {coach ? (
        <section className="fd-coach">
          <div className="fd-coach-mascot" aria-hidden>
            🦊
          </div>
          <div className="fd-coach-main">
            <h2>
              Coach · Hôm nay {coach.doneCount}/{coach.totalCount} hoàn thành
              {coach.skippedCount > 0 ? ` · ${coach.skippedCount} bỏ qua` : ''}
            </h2>
            <p>
              {coach.attention ||
                coach.headline ||
                coach.strength ||
                'Coach đang quan sát nhịp nhà hôm nay.'}
            </p>
            {coach.proposal ? <p className="fd-coach-proposal">{coach.proposal}</p> : null}
            {coach.ctaPath ? (
              <Link to={coach.ctaPath} className="fd-coach-link">
                {coach.ctaLabel || 'Chỉnh trong Routine'} →
              </Link>
            ) : (
              <Link to="/family-os/routines" className="fd-coach-link">
                Chỉnh trong Routine →
              </Link>
            )}
          </div>
          <aside className="fd-coach-tip">
            <div className="fd-coach-tip-head">
              <span aria-hidden>💡</span>
              <strong>Gợi ý từ Coach</strong>
            </div>
            <p>
              {coach.strength ||
                coach.pattern ||
                'Ghi lý do khi bỏ qua giúp Coach hiểu và hỗ trợ nhà tốt hơn.'}
            </p>
          </aside>
        </section>
      ) : null}

      {todayUnlock ? (
        <section className="fd-unlock">
          <div>
            <span className="fd-unlock-label">Team Unlock</span>
            <strong>{todayUnlock.labelVi}</strong>
            <p>
              {todayUnlock.teamDone}/{todayUnlock.teamTotal} Mission · {todayUnlock.teamPercent}%
              {todayUnlock.status === 'confirmed'
                ? ' · Đã xác nhận'
                : todayUnlock.status === 'pending_confirm'
                  ? ' · Chờ xác nhận trên app Mẹ'
                  : ''}
            </p>
          </div>
        </section>
      ) : null}

      <div className="fd-stats">
        <section className="fd-progress-card">
          <h3>🏡 Tiến độ cả đội hôm nay</h3>
          <div className="fd-progress-row">
            <div
              className="fd-donut"
              style={{ ['--pct' as string]: `${doneRatio}` }}
              aria-label={`${doneRatio}%`}
            >
              <strong>{doneRatio}%</strong>
            </div>
            <div className="fd-progress-meta">
              <div className="fd-progress-label">
                <strong>
                  {doneCount} / {total}
                </strong>{' '}
                Mission cả nhà
              </div>
              <div className="fd-bar">
                <span style={{ width: `${doneRatio}%` }} />
              </div>
              {skippedCount > 0 ? (
                <p className="fd-muted">{skippedCount} việc ghi lý do / bỏ qua</p>
              ) : null}
            </div>
          </div>
          <div className="fd-progress-foot">
            <div className="fd-star-note">
              <span aria-hidden>⭐</span>
              {remaining > 0
                ? `🎯 Cả đội còn ${remaining} Mission nữa để hoàn thành ngày hôm nay.`
                : total > 0
                  ? '🎉 Mission Complete! Cả đội đã xong.'
                  : 'Chưa có Mission hôm nay.'}
            </div>
            {remaining > 0 ? (
              <span className="fd-remain-pill">Còn {remaining} Mission</span>
            ) : null}
          </div>
        </section>

        <section className="fd-streak-card">
          <h3>
            <span aria-hidden>🔥</span> Streak · Ngày đẹp
          </h3>
          <div className="fd-streak-row">
            <div>
              <div className="fd-streak-hero">
                <strong>{glance?.currentStreak ?? 0}</strong>
                <span>ngày liên tiếp</span>
              </div>
              <div className="fd-day-strip">
                {(glance?.days ?? []).slice(-7).map((d) => (
                  <span
                    key={d.date}
                    className={`fd-day-chip${d.date === glance?.today ? ' is-today' : ''}${
                      d.isBeautifulDay ? ' is-good' : ''
                    }`}
                    title={d.date}
                  >
                    {formatDayChip(d.date)}
                  </span>
                ))}
              </div>
            </div>
            <div className="fd-streak-art" aria-hidden>
              🏔️
              <span>🚩</span>
            </div>
          </div>
        </section>
      </div>

      {groups.length > 0 ? (
        <section className="fd-board">
          <div className="fd-board-head">
            <div className="fd-member-tabs">
              {childMembers.length > 1 ? (
                <button
                  type="button"
                  className={`fd-member-tab${memberFilter === 'all' ? ' is-on' : ''}`}
                  onClick={() => setMemberFilter('all')}
                >
                  Cả đội
                </button>
              ) : null}
              {groups.map((g) => {
                const pct = g.total > 0 ? Math.round((g.doneOnly / g.total) * 100) : 0;
                return (
                  <button
                    key={g.key}
                    type="button"
                    className={`fd-member-tab${memberFilter === g.key ? ' is-on' : ''}`}
                    onClick={() => setMemberFilter(g.key)}
                  >
                    <span className="fd-member-av" aria-hidden>
                      {memberEmoji(g.name)}
                    </span>
                    <span>
                      <strong>{g.name}</strong>
                      <em>
                        {g.total > 0 ? `${g.doneOnly}/${g.total} · ${pct}%` : 'Chưa có Mission'}
                      </em>
                    </span>
                  </button>
                );
              })}
            </div>
            <div className="fd-view-toggle" role="group" aria-label="Chế độ xem">
              <span>Chế độ xem</span>
              <button
                type="button"
                className={viewMode === 'grid' ? 'is-on' : ''}
                aria-label="Lưới"
                onClick={() => setViewMode('grid')}
              >
                <AppstoreOutlined />
              </button>
              <button
                type="button"
                className={viewMode === 'list' ? 'is-on' : ''}
                aria-label="Danh sách"
                onClick={() => setViewMode('list')}
              >
                <UnorderedListOutlined />
              </button>
            </div>
          </div>

          {visibleGroups.map((group) => {
            const openLeft = group.items.filter(
              (c) => c.status !== 'done' && c.status !== 'skipped',
            ).length;
            return (
              <div key={group.key} className="fd-group">
                {memberFilter === 'all' && childMembers.length > 1 ? (
                  <div className="fd-group-title">
                    <span className="fd-member-av" aria-hidden>
                      {memberEmoji(group.name)}
                    </span>
                    <strong>{group.name}</strong>
                    <em>
                      {group.total > 0 ? `${group.doneOnly}/${group.total}` : '0 Mission'}
                    </em>
                  </div>
                ) : null}
                {group.total === 0 ? (
                  <div className="fd-empty-child">
                    <p>
                      <strong>{group.name}</strong> chưa có Mission trong nhịp hôm nay.
                    </p>
                    <Link to="/family-os/routines">Gắn việc trong Nhịp sống →</Link>
                  </div>
                ) : (
                  <div className={`fd-task-grid${viewMode === 'list' ? ' is-list' : ''}`}>
                    {group.items.map(renderTaskCard)}
                    {openLeft > 0 ? (
                      <article className="fd-cheer">
                        <span aria-hidden>🏆</span>
                        <p>
                          Cố lên {group.name}! Phần của mình còn {openLeft} Mission — cả đội đang chờ
                          trên thanh tiến độ nhà!
                        </p>
                      </article>
                    ) : null}
                  </div>
                )}
              </div>
            );
          })}
        </section>
      ) : null}

      {!loading && (!flow || flow.commitments.length === 0) ? (
        <section className="fd-empty">Chưa có cam kết hôm nay.</section>
      ) : null}

      <footer className="fd-note">
        <div className="fd-note-left">
          <HeartOutlined />
          <strong>Ghi chú nhanh</strong>
        </div>
        <p>
          Việc bị bỏ qua sẽ không ảnh hưởng đến streak. Đừng quên ghi lý do để Coach hiểu và hỗ trợ
          bạn tốt hơn nhé!
        </p>
        <RightOutlined />
      </footer>

      <Modal
        title="Chưa làm — ghi lý do"
        open={!!skipTarget}
        onCancel={() => setSkipTarget(null)}
        onOk={() => void confirmSkip()}
        confirmLoading={saving}
        okText="Ghi nhận"
      >
        <p className="fd-modal-hint">
          Ghi lý do giúp cả nhà hiểu chuyện gì xảy ra. Nếu khớp thỏa thuận đã đồng ý, hệ thống gợi ý
          hậu quả — phụ huynh xác nhận trước khi áp.
        </p>
        <strong>{skipTarget?.title}</strong>
        <div style={{ marginTop: 12 }}>
          <Select
            style={{ width: '100%' }}
            value={skipReason}
            onChange={setSkipReason}
            options={SKIP_REASON_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
          />
        </div>
      </Modal>
    </div>
  );
}
