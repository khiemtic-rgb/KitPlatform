import { useEffect, useMemo, useState } from 'react';
import {
  type CommitmentTemplateDto,
  type FamilyRoutineDto,
  updateCommitmentTemplate,
} from '@/shared/api/family-os.api';

type Props = {
  familyId: string;
  routine: FamilyRoutineDto;
  /** false = P0 read-only list; true = P1 edit. */
  editable?: boolean;
  onClose: () => void;
  onChanged?: () => void;
};

function fmtWindow(t?: string) {
  if (!t) return '—';
  return t.slice(0, 5);
}

export function RoutineLightEditor({
  familyId,
  routine,
  editable = false,
  onClose,
  onChanged,
}: Props) {
  const [templates, setTemplates] = useState(routine.templates);
  const [editing, setEditing] = useState<CommitmentTemplateDto | null>(null);
  const [title, setTitle] = useState('');
  const [start, setStart] = useState('16:00');
  const [end, setEnd] = useState('17:00');
  const [active, setActive] = useState(true);
  const [commitmentKind, setCommitmentKind] = useState('chore');
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

  const sorted = useMemo(
    () =>
      [...templates].sort((a, b) => {
        if (a.isActive !== b.isActive) return a.isActive ? -1 : 1;
        return a.sortOrder - b.sortOrder || (a.windowStart ?? '').localeCompare(b.windowStart ?? '');
      }),
    [templates],
  );

  const activeCount = templates.filter((t) => t.isActive).length;

  const openEdit = (t: CommitmentTemplateDto) => {
    if (!editable) return;
    setEditing(t);
    setTitle(t.title);
    setStart(t.windowStart?.slice(0, 5) || '07:00');
    setEnd(t.windowEnd?.slice(0, 5) || '08:00');
    setActive(t.isActive);
    setCommitmentKind(t.commitmentKind || 'chore');
    setError(null);
  };

  const save = async () => {
    if (!editing || !title.trim()) return;
    setBusy(true);
    setError(null);
    try {
      const updated = await updateCommitmentTemplate(familyId, routine.id, editing.id, {
        title: title.trim(),
        description: editing.description,
        memberId: editing.memberId,
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
      setToast('Đã lưu mẫu — áp dụng từ ngày mai.');
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
          {activeCount} việc đang bật
          {editable
            ? ' · chạm để sửa giờ / ẩn. Đổi mẫu áp dụng từ ngày mai — hôm nay giữ nguyên.'
            : ' · xem nhanh (chạm “Chỉnh nhẹ” để sửa).'}
        </p>

        <ul className="fa-template-list">
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
                  {t.commitmentKind === 'study_focus' ? ' · 📚 Học' : ''}
                  {t.commitmentKind === 'relation' ? ' · Quan hệ' : ''}
                </strong>
                <span>
                  {fmtWindow(t.windowStart)}–{fmtWindow(t.windowEnd)}
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
                            name={`kind-${t.id}`}
                            checked={commitmentKind === value}
                            onChange={() => setCommitmentKind(value)}
                          />
                          {label}
                        </label>
                      ))}
                    </div>
                    {commitmentKind === 'study_focus' ? (
                      <p className="ph-sheet-lead" style={{ marginTop: 8 }}>
                        Tick một mình chưa đủ để nhận sao — cần ảnh, câu hỏi nhớ bài, hoặc bố mẹ
                        xác nhận.
                      </p>
                    ) : null}
                  </fieldset>
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
                      onClick={() => void save()}
                    >
                      Lưu
                    </button>
                  </div>
                </div>
              ) : null}
            </li>
          ))}
        </ul>

        {toast ? (
          <p className="ph-action-toast" role="status">
            {toast}
          </p>
        ) : null}
      </div>
    </div>
  );
}
