import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  activateFamilyMode,
  FAMILY_MODE_OPTIONS,
  fetchFamilies,
  fetchFamilyRoutines,
  resolveCalendarRoutine,
  scanAdaptiveProposals,
  type FamilyMembership,
  type FamilyRoutineDto,
  type ResolvedCalendarRoutine,
} from '@/shared/api/family-os.api';
import { getApiErrorMessage } from '@/shared/billing/capability-error';
import { useSessionStore } from '@/shared/auth/session.store';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';
import { FamilyAdminShell } from '@/modules/admin/FamilyAdminShell';
import { RoutineLightEditor } from '@/modules/admin/RoutineLightEditor';
import {
  countActiveTemplatesForFocus,
  sanitizeRoutineFocus,
  type RoutineFocusKey,
} from '@/modules/admin/routine-focus';

const FOCUS_KEY = 'famixa.routineFocus';

function shortChildName(name: string): string {
  return name.trim().split(/\s+/).filter(Boolean).pop() || name;
}

export function FamilyRoutinePage() {
  const familyId = useSessionStore((s) => s.familyId);
  const member = useSessionStore((s) => s.member);
  const [routines, setRoutines] = useState<FamilyRoutineDto[]>([]);
  const [resolved, setResolved] = useState<ResolvedCalendarRoutine | null>(null);
  const [children, setChildren] = useState<FamilyMembership[]>([]);
  const [focus, setFocus] = useState<RoutineFocusKey>('all');
  const [loadWarning, setLoadWarning] = useState<string | null>(null);
  const [selectedMode, setSelectedMode] = useState<string>('normal');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [editorRoutineId, setEditorRoutineId] = useState<string | null>(null);
  const [editorEditable, setEditorEditable] = useState(false);

  const reload = useCallback(async () => {
    if (!familyId) return;
    const warnings: string[] = [];
    const [rts, cal, families] = await Promise.all([
      fetchFamilyRoutines(familyId).catch((err) => {
        warnings.push(getApiErrorMessage(err) || 'Không tải được danh sách routine.');
        return [] as FamilyRoutineDto[];
      }),
      resolveCalendarRoutine(familyId).catch(() => null),
      fetchFamilies().catch((err) => {
        warnings.push(getApiErrorMessage(err) || 'Không tải được thành viên.');
        return [];
      }),
    ]);
    setRoutines(rts);
    setResolved(cal);
    const fam = families.find((f) => f.id === familyId) ?? families[0];
    const kids = (fam?.members ?? []).filter((m) => m.roleCode === 'child');
    setChildren(kids);
    setLoadWarning(warnings.length ? warnings.join(' ') : null);

    try {
      const raw = sessionStorage.getItem(FOCUS_KEY);
      setFocus(sanitizeRoutineFocus(raw, kids.map((c) => c.id)));
    } catch {
      setFocus('all');
    }
  }, [familyId]);

  useEffect(() => {
    void reload().catch(() => setError('Không tải được routine.'));
  }, [reload]);

  useEffect(() => {
    try {
      sessionStorage.setItem(FOCUS_KEY, focus);
    } catch {
      /* ignore */
    }
  }, [focus]);

  useEffect(() => {
    if (focus === 'all') return;
    if (children.length === 0) return;
    const next = sanitizeRoutineFocus(focus, children.map((c) => c.id));
    if (next !== focus) setFocus(next);
  }, [children, focus]);

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

  const focusChild = useMemo(
    () => (focus === 'all' ? null : children.find((c) => c.id === focus) ?? null),
    [children, focus],
  );

  const scopedActiveCount = modeRoutine
    ? countActiveTemplatesForFocus(modeRoutine.templates, focus)
    : 0;

  const subtitle = focusChild
    ? `Lịch việc của ${shortChildName(focusChild.displayName)} · chế độ vẫn chung cả nhà`
    : 'Một lịch sống cho cả nhà · chọn con để xem lịch riêng';

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
    <FamilyAdminShell title="Routine" subtitle={subtitle}>
      {toast ? (
        <p className="ph-action-toast" role="status">
          {toast}
        </p>
      ) : null}
      {error ? <p className="banner-error">{error}</p> : null}
      {loadWarning ? <p className="banner-error">{loadWarning}</p> : null}

      {children.length > 0 ? (
        <nav className="phs-child-rail fa-routine-focus" aria-label="Xem lịch của ai">
          <button
            type="button"
            className={focus === 'all' ? 'is-on' : undefined}
            onClick={() => setFocus('all')}
          >
            Cả nhà
          </button>
          {children.map((c) => {
            const short = shortChildName(c.displayName);
            const personal =
              modeRoutine?.templates.filter((t) => t.isActive && t.memberId === c.id)
                .length ?? 0;
            return (
              <button
                key={c.id}
                type="button"
                className={focus === c.id ? 'is-on' : undefined}
                onClick={() => setFocus(c.id)}
                title={c.displayName}
              >
                <span aria-hidden>
                  {avatarEmoji(inferGenderFromName(c.displayName), 'child')}
                </span>
                {short}
                {personal > 0 ? <i className="fa-focus-count">{personal}</i> : null}
              </button>
            );
          })}
        </nav>
      ) : null}

      <section className="fa-card">
        <h2>Đang dùng</h2>
        <p className="fa-hint" style={{ marginBottom: 0 }}>
          <strong>{resolved?.routineDisplayName ?? modeRoutine?.displayName ?? '—'}</strong>
          {modeRoutine
            ? ` · ${scopedActiveCount} việc${
                focusChild ? ` (${shortChildName(focusChild.displayName)} + chung)` : ''
              }`
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
        <p className="fa-hint">
          Đổi nhịp chung cả nhà theo mùa / kỳ.
          {focusChild
            ? ' Lịch việc từng con xem / chỉnh bằng chip phía trên — không đổi chế độ riêng.'
            : null}
        </p>
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
              ? `«${previewOption.label}» — ${previewOption.hint}. Đổi chế độ gắn lịch mẫu ngay; việc chỉnh tay trong «Chỉnh nhẹ» áp dụng từ ngày làm việc tiếp theo (thường là ngày mai).`
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
            {routines.map((r) => {
              const n = countActiveTemplatesForFocus(r.templates, focus);
              return (
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
                      {r.kind} · {n} việc
                      {focusChild ? ` · ${shortChildName(focusChild.displayName)}` : ''} · chỉnh
                      nhẹ →
                    </span>
                  </button>
                </li>
              );
            })}
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
          focusMemberId={focus}
          childrenMembers={children.map((c) => ({
            id: c.id,
            name: c.displayName,
          }))}
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
