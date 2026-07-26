import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  checkinParentGoal,
  createParentGoal,
  deleteParentGoal,
  fetchParentGoals,
  fetchSharedParentProgress,
  updateParentGoal,
  type ParentGoal,
  type SharedParentProgress,
} from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  memberId: string;
  viewerName: string;
};

type Suggestion = { emoji: string; title: string; target: number };

const SUGGESTIONS: Suggestion[] = [
  { emoji: '📖', title: 'Đọc sách 20 phút', target: 5 },
  { emoji: '🍚', title: 'Ăn tối không dùng điện thoại', target: 7 },
  { emoji: '🚶', title: 'Đi bộ / vận động 30 phút', target: 4 },
  { emoji: '🌙', title: 'Cất điện thoại sau 21h', target: 5 },
  { emoji: '🧘', title: 'Thở / thiền 10 phút', target: 5 },
  { emoji: '💬', title: 'Trò chuyện cùng con 15 phút', target: 7 },
];

function goalEmoji(g: { emoji?: string }): string {
  return g.emoji && g.emoji.trim() ? g.emoji : '🎯';
}

export function ParentGoalsPanel({ familyId, memberId, viewerName }: Props) {
  const [goals, setGoals] = useState<ParentGoal[]>([]);
  const [shared, setShared] = useState<SharedParentProgress[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [customTitle, setCustomTitle] = useState('');
  const [addOpen, setAddOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [mine, sharedRows] = await Promise.all([
        fetchParentGoals(familyId, memberId),
        fetchSharedParentProgress(familyId).catch(() => [] as SharedParentProgress[]),
      ]);
      setGoals(mine);
      setShared(sharedRows);
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Không tải được mục tiêu.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [familyId, memberId]);

  useEffect(() => {
    void load();
  }, [load]);

  const activeGoals = useMemo(() => goals.filter((g) => g.isActive), [goals]);
  const firstName = viewerName.trim().split(/\s+/).pop() || viewerName;

  const refreshShared = useCallback(async () => {
    const rows = await fetchSharedParentProgress(familyId).catch(() => shared);
    setShared(rows);
  }, [familyId, shared]);

  const addGoal = async (title: string, emoji?: string, target?: number) => {
    const trimmed = title.trim();
    if (!trimmed) return;
    setAdding(true);
    try {
      const created = await createParentGoal(familyId, {
        memberId,
        title: trimmed,
        emoji,
        targetDaysPerWeek: target,
      });
      setGoals((prev) => [...prev, created]);
      setCustomTitle('');
      setAddOpen(false);
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Không thêm được mục tiêu.',
      );
    } finally {
      setAdding(false);
    }
  };

  const toggleToday = async (goal: ParentGoal) => {
    setBusyId(goal.id);
    try {
      const next = goal.todayStatus === 'done' ? 'clear' : 'done';
      const updated = await checkinParentGoal(familyId, goal.id, next);
      setGoals((prev) => prev.map((g) => (g.id === goal.id ? updated : g)));
      if (goal.shareWithFamily) void refreshShared();
    } catch {
      setError('Không lưu được check-in.');
    } finally {
      setBusyId(null);
    }
  };

  const toggleShare = async (goal: ParentGoal) => {
    setBusyId(goal.id);
    try {
      const updated = await updateParentGoal(familyId, goal.id, {
        shareWithFamily: !goal.shareWithFamily,
      });
      setGoals((prev) => prev.map((g) => (g.id === goal.id ? updated : g)));
      void refreshShared();
    } catch {
      setError('Không đổi được chế độ chia sẻ.');
    } finally {
      setBusyId(null);
    }
  };

  const removeGoal = async (goal: ParentGoal) => {
    setBusyId(goal.id);
    try {
      await deleteParentGoal(familyId, goal.id);
      setGoals((prev) => prev.filter((g) => g.id !== goal.id));
      void refreshShared();
    } catch {
      setError('Không xóa được mục tiêu.');
    } finally {
      setBusyId(null);
    }
  };

  const usedTitles = new Set(activeGoals.map((g) => g.title.toLowerCase()));
  const openSuggestions = SUGGESTIONS.filter((s) => !usedTitles.has(s.title.toLowerCase()));

  const sharedByMember = useMemo(() => {
    const map = new Map<string, { name: string; items: SharedParentProgress[] }>();
    for (const row of shared) {
      const entry = map.get(row.memberId) ?? { name: row.memberName, items: [] };
      entry.items.push(row);
      map.set(row.memberId, entry);
    }
    return [...map.values()];
  }, [shared]);

  return (
    <section className="pg-panel">
      <header className="pg-head">
        <div>
          <h3 className="pg-title">Mục tiêu của {firstName}</h3>
          <p className="pg-sub muted">
            Bố mẹ cùng làm gương. Riêng tư mặc định — chỉ chia sẻ khi bạn bật.
          </p>
        </div>
      </header>

      {error ? <p className="pg-error">{error}</p> : null}

      {loading ? (
        <p className="muted">Đang tải…</p>
      ) : (
        <>
          {activeGoals.length === 0 ? (
            <p className="muted pg-empty">
              Chưa có mục tiêu nào. Chọn một gợi ý bên dưới để bắt đầu.
            </p>
          ) : (
            <ul className="pg-list">
              {activeGoals.map((goal) => {
                const done = goal.todayStatus === 'done';
                return (
                  <li key={goal.id} className={`pg-item${done ? ' is-done' : ''}`}>
                    <button
                      type="button"
                      className={`pg-check${done ? ' is-done' : ''}`}
                      disabled={busyId === goal.id}
                      aria-label={done ? 'Bỏ đánh dấu hôm nay' : 'Đã làm hôm nay'}
                      onClick={() => void toggleToday(goal)}
                    >
                      {done ? '✓' : ''}
                    </button>
                    <div className="pg-body">
                      <div className="pg-line">
                        <span className="pg-emoji">{goalEmoji(goal)}</span>
                        <span className="pg-name">{goal.title}</span>
                      </div>
                      <div className="pg-meta muted">
                        <span>
                          Tuần này {goal.weekDoneCount}/{goal.targetDaysPerWeek}
                        </span>
                        {goal.currentStreak > 0 ? (
                          <span className="pg-streak">🔥 {goal.currentStreak} ngày</span>
                        ) : null}
                      </div>
                    </div>
                    <div className="pg-actions">
                      <button
                        type="button"
                        className={`pill pg-share${goal.shareWithFamily ? '' : ' is-soft'}`}
                        disabled={busyId === goal.id}
                        onClick={() => void toggleShare(goal)}
                        title={
                          goal.shareWithFamily
                            ? 'Đang chia sẻ với cả nhà — chạm để ẩn'
                            : 'Đang riêng tư — chạm để chia sẻ với cả nhà'
                        }
                      >
                        {goal.shareWithFamily ? '🤝 Chia sẻ' : '🔒 Riêng tư'}
                      </button>
                      <button
                        type="button"
                        className="pg-remove"
                        disabled={busyId === goal.id}
                        aria-label="Xóa mục tiêu"
                        onClick={() => void removeGoal(goal)}
                      >
                        ×
                      </button>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}

          <div className="pg-add">
            {openSuggestions.length > 0 ? (
              <div className="pg-suggest">
                {openSuggestions.map((s) => (
                  <button
                    key={s.title}
                    type="button"
                    className="pill pg-suggest-chip"
                    disabled={adding}
                    onClick={() => void addGoal(s.title, s.emoji, s.target)}
                  >
                    {s.emoji} {s.title}
                  </button>
                ))}
              </div>
            ) : null}

            {addOpen ? (
              <form
                className="pg-custom"
                onSubmit={(e) => {
                  e.preventDefault();
                  void addGoal(customTitle);
                }}
              >
                <input
                  className="pg-input"
                  value={customTitle}
                  autoFocus
                  maxLength={200}
                  placeholder="Mục tiêu của tôi…"
                  onChange={(e) => setCustomTitle(e.target.value)}
                />
                <button type="submit" className="btn btn-primary" disabled={adding || !customTitle.trim()}>
                  Thêm
                </button>
                <button type="button" className="btn btn-ghost" onClick={() => setAddOpen(false)}>
                  Hủy
                </button>
              </form>
            ) : (
              <button type="button" className="btn btn-ghost pg-custom-open" onClick={() => setAddOpen(true)}>
                + Mục tiêu khác
              </button>
            )}
          </div>

          {sharedByMember.length > 0 ? (
            <div className="pg-shared">
              <h4 className="pg-shared-title">Cả nhà hôm nay</h4>
              <ul className="pg-shared-list">
                {sharedByMember.map((m) => (
                  <li key={m.name} className="pg-shared-member">
                    <span className="pg-shared-name">{m.name}</span>
                    <span className="pg-shared-items">
                      {m.items.map((it) => (
                        <span
                          key={it.goalId}
                          className={`pg-shared-chip${it.todayDone ? ' is-done' : ''}`}
                        >
                          {it.todayDone ? '✓ ' : ''}
                          {goalEmoji(it)} {it.title}
                        </span>
                      ))}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </>
      )}
    </section>
  );
}
