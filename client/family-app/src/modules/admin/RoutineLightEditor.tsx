import { useEffect, useMemo, useState } from 'react';
import {
  addCommitmentTemplate,
  type CommitmentTemplateDto,
  type FamilyRoutineDto,
  updateCommitmentTemplate,
} from '@/shared/api/family-os.api';
import { templateVisibleForFocus } from '@/modules/admin/routine-focus';

type ChildOpt = { id: string; name: string };

type Props = {
  familyId: string;
  routine: FamilyRoutineDto;
  /** false = read-only list; true = edit. */
  editable?: boolean;
  /** `all` or child membership id — filters list; new tasks default to this. */
  focusMemberId?: 'all' | string;
  childrenMembers?: ChildOpt[];
  onClose: () => void;
  onChanged?: () => void;
};

function fmtWindow(t?: string) {
  if (!t) return '—';
  return t.slice(0, 5);
}

function shortName(name: string): string {
  return name.trim().split(/\s+/).filter(Boolean).pop() || name;
}

function whoLabel(memberId: string | undefined, children: ChildOpt[]): string {
  if (!memberId) return 'Cả nhà';
  const hit = children.find((c) => c.id === memberId);
  return hit ? shortName(hit.name) : 'Con';
}

export function RoutineLightEditor({
  familyId,
  routine,
  editable = false,
  focusMemberId = 'all',
  childrenMembers = [],
  onClose,
  onChanged,
}: Props) {
  const [templates, setTemplates] = useState(routine.templates);
  const [editing, setEditing] = useState<CommitmentTemplateDto | null>(null);
  const [adding, setAdding] = useState(false);
  const [title, setTitle] = useState('');
  const [start, setStart] = useState('16:00');
  const [end, setEnd] = useState('17:00');
  const [active, setActive] = useState(true);
  const [commitmentKind, setCommitmentKind] = useState('chore');
  const [assigneeId, setAssigneeId] = useState<string>('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  useEffect(() => {
    setTemplates(routine.templates);
  }, [routine]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 2600);
    return () => window.clearTimeout(t);
  }, [toast]);

  const visible = useMemo(
    () => templates.filter((t) => templateVisibleForFocus(t, focusMemberId)),
    [templates, focusMemberId],
  );

  const sorted = useMemo(
    () =>
      [...visible].sort((a, b) => {
        if (a.isActive !== b.isActive) return a.isActive ? -1 : 1;
        const aShared = a.memberId ? 1 : 0;
        const bShared = b.memberId ? 1 : 0;
        if (aShared !== bShared) return aShared - bShared;
        return (
          a.sortOrder - b.sortOrder ||
          (a.windowStart ?? '').localeCompare(b.windowStart ?? '')
        );
      }),
    [visible],
  );

  const activeCount = visible.filter((t) => t.isActive).length;
  const focusLabel =
    focusMemberId === 'all'
      ? 'cả nhà'
      : shortName(
          childrenMembers.find((c) => c.id === focusMemberId)?.name ?? 'con',
        );

  const defaultAssignee = () =>
    focusMemberId === 'all' ? '' : focusMemberId;

  const openEdit = (t: CommitmentTemplateDto) => {
    if (!editable) return;
    setAdding(false);
    setEditing(t);
    setTitle(t.title);
    setStart(t.windowStart?.slice(0, 5) || '07:00');
    setEnd(t.windowEnd?.slice(0, 5) || '08:00');
    setActive(t.isActive);
    setCommitmentKind(t.commitmentKind || 'chore');
    setAssigneeId(t.memberId ?? '');
    setError(null);
  };

  const openAdd = () => {
    if (!editable) return;
    setEditing(null);
    setAdding(true);
    setTitle('');
    setStart('16:00');
    setEnd('17:00');
    setActive(true);
    setCommitmentKind('chore');
    setAssigneeId(defaultAssignee());
    setError(null);
  };

  const saveEdit = async () => {
    if (!editing || !title.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const updated = await updateCommitmentTemplate(familyId, routine.id, editing.id, {
        title: title.trim(),
        description: editing.description,
        memberId: assigneeId || undefined,
        windowStart: start || undefined,
        windowEnd: end || undefined,
        sortOrder: editing.sortOrder,
        isActive: active,
        priority: editing.priority,
        starReward: editing.starReward,
        commitmentKind,
      });
      setTemplates((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      setEditing(null);
      setToast('Đã lưu mẫu — áp dụng từ ngày làm việc tiếp theo (thường ngày mai).');
      onChanged?.();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Chưa lưu được.',
      );
    } finally {
      setBusy(false);
    }
  };

  const saveAdd = async () => {
    if (!title.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const created = await addCommitmentTemplate(familyId, routine.id, {
        title: title.trim(),
        memberId: assigneeId || undefined,
        windowStart: start || undefined,
        windowEnd: end || undefined,
        sortOrder: templates.length,
        commitmentKind,
      });
      let row = created;
      if (!active) {
        row = await updateCommitmentTemplate(familyId, routine.id, created.id, {
          title: created.title,
          description: created.description,
          memberId: assigneeId || undefined,
          windowStart: start || created.windowStart,
          windowEnd: end || created.windowEnd,
          sortOrder: created.sortOrder,
          isActive: false,
          priority: created.priority,
          starReward: created.starReward,
          commitmentKind,
        });
      }
      setTemplates((prev) => [...prev, row]);
      setAdding(false);
      setToast('Đã thêm việc — áp dụng từ ngày làm việc tiếp theo (thường ngày mai).');
      onChanged?.();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Chưa thêm được.',
      );
    } finally {
      setBusy(false);
    }
  };

  const renderAssigneeField = (idPrefix: string) => (
    <label className="fa-field">
      Ai làm
      <select
        value={assigneeId}
        onChange={(e) => setAssigneeId(e.target.value)}
        aria-label="Ai làm việc này"
        id={`${idPrefix}-assignee`}
      >
        <option value="">Cả nhà (chung)</option>
        {childrenMembers.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>
    </label>
  );

  const renderKindField = (name: string) => (
    <fieldset className="fa-field">
      <legend>Loại cam kết</legend>
      <div className="fa-time-row" style={{ flexWrap: 'wrap', gap: 8 }}>
        {(
          [
            ['chore', 'Việc nhà'],
            ['study_focus', 'Học / tập trung'],
            ['relation', 'Quan hệ'],
          ] as const
        ).map(([value, label]) => (
          <label key={value} className="fa-check">
            <input
              type="radio"
              name={name}
              checked={commitmentKind === value}
              onChange={() => setCommitmentKind(value)}
            />
            {label}
          </label>
        ))}
      </div>
      {commitmentKind === 'study_focus' ? (
        <p className="ph-sheet-lead" style={{ marginTop: 8 }}>
          Tick một mình chưa đủ để nhận sao — cần ảnh, câu hỏi nhớ bài, hoặc bố mẹ xác nhận.
        </p>
      ) : null}
    </fieldset>
  );

  return (
    <div className="ph-sheet-backdrop" role="presentation" onClick={onClose}>
      <div
        className="ph-sheet fa-routine-sheet"
        role="dialog"
        aria-modal="true"
        aria-label={`Routine ${routine.displayName}`}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ph-sheet-head">
          <h2>{routine.displayName}</h2>
          <button type="button" className="ph-sheet-close" onClick={onClose} aria-label="Đóng">
            ×
          </button>
        </header>
        <p className="ph-sheet-lead">
          {activeCount} việc · đang xem <strong>{focusLabel}</strong>
          {editable
            ? ' · sửa giờ / ai làm / ẩn. Đổi mẫu áp dụng từ ngày làm việc tiếp theo.'
            : ' · xem nhanh (chạm “Chỉnh nhẹ” để sửa).'}
        </p>

        <ul className="fa-template-list">
          {sorted.length === 0 ? (
            <li className="fa-template-empty">
              {focusMemberId === 'all'
                ? 'Chưa có việc trong routine này.'
                : `Chưa có việc riêng của ${focusLabel} (và chưa có việc chung).`}
              {editable ? ' Thêm việc bên dưới.' : null}
            </li>
          ) : null}
          {sorted.map((t) => (
            <li key={t.id} className={t.isActive ? undefined : 'is-off'}>
              <button
                type="button"
                className={`fa-template-row${editing?.id === t.id ? ' is-editing' : ''}`}
                disabled={!editable}
                onClick={() => openEdit(t)}
              >
                <strong>
                  {t.title}
                  {t.commitmentKind === 'study_focus' ? ' · Học' : ''}
                  {t.commitmentKind === 'relation' ? ' · Quan hệ' : ''}
                </strong>
                <span>
                  {fmtWindow(t.windowStart)}–{fmtWindow(t.windowEnd)}
                  {' · '}
                  <em className={t.memberId ? 'is-child' : 'is-shared'}>
                    {whoLabel(t.memberId, childrenMembers)}
                  </em>
                  {!t.isActive ? ' · đang ẩn' : ''}
                </span>
              </button>
              {editing?.id === t.id ? (
                <div className="fa-template-edit">
                  <h3>Chỉnh nhẹ · {t.title}</h3>
                  <label className="fa-field">
                    Tên việc
                    <input value={title} onChange={(e) => setTitle(e.target.value)} autoFocus />
                  </label>
                  {renderAssigneeField(`edit-${t.id}`)}
                  {renderKindField(`kind-${t.id}`)}
                  <div className="fa-time-row">
                    <label className="fa-field">
                      Từ
                      <input type="time" value={start} onChange={(e) => setStart(e.target.value)} />
                    </label>
                    <label className="fa-field">
                      Đến
                      <input type="time" value={end} onChange={(e) => setEnd(e.target.value)} />
                    </label>
                  </div>
                  <label className="fa-check">
                    <input
                      type="checkbox"
                      checked={active}
                      onChange={(e) => setActive(e.target.checked)}
                    />
                    Hiện trong Routine (tắt = ẩn từ ngày mai)
                  </label>
                  {error ? <p className="ph-sheet-error">{error}</p> : null}
                  <div className="fa-edit-actions">
                    <button
                      type="button"
                      className="btn btn-ghost"
                      disabled={busy}
                      onClick={() => setEditing(null)}
                    >
                      Huỷ
                    </button>
                    <button
                      type="button"
                      className="btn btn-primary"
                      disabled={busy}
                      onClick={() => void saveEdit()}
                    >
                      Lưu
                    </button>
                  </div>
                </div>
              ) : null}
            </li>
          ))}
        </ul>

        {editable ? (
          <div className="fa-template-add-wrap">
            {!adding ? (
              <button type="button" className="btn btn-ghost fa-template-add-btn" onClick={openAdd}>
                + Thêm việc
                {focusMemberId !== 'all' ? ` cho ${focusLabel}` : ''}
              </button>
            ) : (
              <div className="fa-template-edit is-add">
                <h3>
                  Thêm việc
                  {focusMemberId !== 'all' ? ` · ${focusLabel}` : ''}
                </h3>
                <label className="fa-field">
                  Tên việc
                  <input
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="VD: Họm vở / luyện piano"
                    autoFocus
                  />
                </label>
                {renderAssigneeField('add')}
                {renderKindField('kind-add')}
                <div className="fa-time-row">
                  <label className="fa-field">
                    Từ
                    <input type="time" value={start} onChange={(e) => setStart(e.target.value)} />
                  </label>
                  <label className="fa-field">
                    Đến
                    <input type="time" value={end} onChange={(e) => setEnd(e.target.value)} />
                  </label>
                </div>
                <label className="fa-check">
                  <input
                    type="checkbox"
                    checked={active}
                    onChange={(e) => setActive(e.target.checked)}
                  />
                  Hiện trong Routine
                </label>
                {error ? <p className="ph-sheet-error">{error}</p> : null}
                <div className="fa-edit-actions">
                  <button
                    type="button"
                    className="btn btn-ghost"
                    disabled={busy}
                    onClick={() => setAdding(false)}
                  >
                    Huỷ
                  </button>
                  <button
                    type="button"
                    className="btn btn-primary"
                    disabled={busy || !title.trim()}
                    onClick={() => void saveAdd()}
                  >
                    Thêm
                  </button>
                </div>
              </div>
            )}
          </div>
        ) : null}

        {toast ? (
          <p className="ph-action-toast" role="status">
            {toast}
          </p>
        ) : null}
      </div>
    </div>
  );
}
