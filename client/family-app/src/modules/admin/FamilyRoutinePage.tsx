import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  activateFamilyMode,
  FAMILY_MODE_OPTIONS,
  fetchFamilyRoutines,
  resolveCalendarRoutine,
  scanAdaptiveProposals,
  type FamilyRoutineDto,
  type ResolvedCalendarRoutine,
} from '@/shared/api/family-os.api';
import { getApiErrorMessage } from '@/shared/billing/capability-error';
import { useSessionStore } from '@/shared/auth/session.store';
import { FamilyAdminShell } from '@/modules/admin/FamilyAdminShell';
import { RoutineLightEditor } from '@/modules/admin/RoutineLightEditor';

export function FamilyRoutinePage() {
  const familyId = useSessionStore((s) => s.familyId);
  const member = useSessionStore((s) => s.member);
  const [routines, setRoutines] = useState<FamilyRoutineDto[]>([]);
  const [resolved, setResolved] = useState<ResolvedCalendarRoutine | null>(null);
  const [selectedMode, setSelectedMode] = useState<string>('normal');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [editorRoutineId, setEditorRoutineId] = useState<string | null>(null);
  const [editorEditable, setEditorEditable] = useState(false);

  const reload = useCallback(async () => {
    if (!familyId) return;
    const [rts, cal] = await Promise.all([
      fetchFamilyRoutines(familyId).catch(() => [] as FamilyRoutineDto[]),
      resolveCalendarRoutine(familyId).catch(() => null),
    ]);
    setRoutines(rts);
    setResolved(cal);
  }, [familyId]);

  useEffect(() => {
    void reload().catch(() => setError('Không tải được routine.'));
  }, [reload]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(t);
  }, [toast]);

  const modeRoutine = useMemo(() => {
    if (!resolved?.routineId) return null;
    return routines.find((r) => r.id === resolved.routineId) ?? null;
  }, [resolved, routines]);

  const activeRoutine = useMemo(() => {
    if (!editorRoutineId) return null;
    return routines.find((r) => r.id === editorRoutineId) ?? null;
  }, [editorRoutineId, routines]);

  const previewOption = FAMILY_MODE_OPTIONS.find((m) => m.value === selectedMode);

  const onSaveMode = async () => {
    if (!familyId) return;
    setBusy(true);
    setError(null);
    try {
      const result = await activateFamilyMode(familyId, {
        mode: selectedMode,
        activatedByMemberId: member?.id,
        confirmNow: true,
      });
      setToast(result.messageVi);
      await reload();
      void scanAdaptiveProposals(familyId).catch(() => undefined);
      if (result.primaryRoutineId) {
        setEditorRoutineId(result.primaryRoutineId);
        setEditorEditable(false);
      }
    } catch (err: unknown) {
      setError(getApiErrorMessage(err) || 'Chưa đổi được chế độ.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <FamilyAdminShell title="Routine" subtitle="Một lịch sống cho cả nhà">
      {toast ? (
        <p className="ph-action-toast" role="status">
          {toast}
        </p>
      ) : null}
      {error ? <p className="banner-error">{error}</p> : null}

      <section className="fa-card">
        <h2>Đang dùng</h2>
        <p className="fa-hint" style={{ marginBottom: 0 }}>
          <strong>{resolved?.routineDisplayName ?? modeRoutine?.displayName ?? '—'}</strong>
          {modeRoutine
            ? ` · ${modeRoutine.templates.filter((t) => t.isActive).length} việc`
            : null}
          {resolved?.periodDisplayName ? ` · ${resolved.periodDisplayName}` : null}
        </p>
        {modeRoutine ? (
          <div className="fa-mode-actions" style={{ marginTop: 10 }}>
            <button
              type="button"
              className="pill is-soft"
              onClick={() => {
                setEditorRoutineId(modeRoutine.id);
                setEditorEditable(false);
              }}
            >
              Xem việc
            </button>
            <button
              type="button"
              className="pill"
              onClick={() => {
                setEditorRoutineId(modeRoutine.id);
                setEditorEditable(true);
              }}
            >
              Chỉnh nhẹ
            </button>
          </div>
        ) : null}
      </section>

      <section className="fa-card">
        <h2>Chọn chế độ</h2>
        <p className="fa-hint">Một mục tiêu: đổi nhịp nhà theo mùa / kỳ.</p>
        <div className="fa-mode-radios" role="radiogroup" aria-label="Chế độ routine">
          {FAMILY_MODE_OPTIONS.map((m) => (
            <label
              key={m.value}
              className={`fa-mode-radio${selectedMode === m.value ? ' is-on' : ''}`}
            >
              <input
                type="radio"
                name="family-mode"
                value={m.value}
                checked={selectedMode === m.value}
                onChange={() => setSelectedMode(m.value)}
              />
              <span>
                <strong>{m.label}</strong>
                <em>{m.hint}</em>
              </span>
            </label>
          ))}
        </div>

        <div className="fa-routine-preview">
          <strong>Preview</strong>
          <p>
            {previewOption
              ? `«${previewOption.label}» — ${previewOption.hint}. Lịch việc sẽ gắn theo chế độ này từ hôm nay.`
              : 'Chọn một chế độ để xem mô tả.'}
          </p>
        </div>

        <button
          type="button"
          className="btn btn-primary"
          disabled={busy}
          onClick={() => void onSaveMode()}
        >
          {busy ? 'Đang lưu…' : 'Lưu chế độ'}
        </button>
      </section>

      {routines.length > 0 ? (
        <section className="fa-card">
          <h2>Routine hiện có</h2>
          <ul className="fa-routine-list">
            {routines.map((r) => (
              <li key={r.id}>
                <button
                  type="button"
                  className="fa-routine-open"
                  onClick={() => {
                    setEditorRoutineId(r.id);
                    setEditorEditable(true);
                  }}
                >
                  <strong>{r.displayName}</strong>
                  <span>
                    {r.kind} · {r.templates.filter((t) => t.isActive).length} việc · chỉnh nhẹ →
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      ) : (
        <section className="fa-card">
          <p className="fa-hint" style={{ margin: 0 }}>
            Chưa có routine — mở AI Setup trên hub để sinh lịch starter.
          </p>
        </section>
      )}

      {activeRoutine && editorRoutineId && familyId ? (
        <RoutineLightEditor
          familyId={familyId}
          routine={activeRoutine}
          editable={editorEditable}
          onClose={() => {
            setEditorRoutineId(null);
            setEditorEditable(false);
          }}
          onChanged={() => void reload()}
        />
      ) : null}
    </FamilyAdminShell>
  );
}
