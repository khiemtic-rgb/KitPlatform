import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  activateFamilyMode,
  addAdHocCommitment,
  addFamilyMember,
  ensureDayFlow,
  FAMILY_MODE_OPTIONS,
  fetchFamilies,
  fetchFamilyRoutines,
  fetchFamilySubscription,
  resolveCalendarRoutine,
  scanAdaptiveProposals,
  type DayFlow,
  type FamilyMembership,
  type FamilyRoutineDto,
  type FamilySubscription,
  type ResolvedCalendarRoutine,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { RoutineLightEditor } from '@/modules/admin/RoutineLightEditor';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { isCapabilityPaywallError, getApiErrorMessage } from '@/shared/billing/capability-error';

const ROLE_LABEL: Record<string, string> = {
  guardian: 'Bố/Mẹ',
  caregiver: 'Người chăm sóc',
  child: 'Con',
  viewer: 'Xem',
};

export function FamilyAdminPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const member = useSessionStore((s) => s.member);
  const familyName = useSessionStore((s) => s.familyName);

  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [routines, setRoutines] = useState<FamilyRoutineDto[]>([]);
  const [flow, setFlow] = useState<DayFlow | null>(null);
  const [resolved, setResolved] = useState<ResolvedCalendarRoutine | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editorRoutineId, setEditorRoutineId] = useState<string | null>(null);
  const [editorEditable, setEditorEditable] = useState(false);

  const [newName, setNewName] = useState('');
  const [newRole, setNewRole] = useState('child');
  const [missionChildId, setMissionChildId] = useState('');
  const [missionTitle, setMissionTitle] = useState('');
  const [missionStart, setMissionStart] = useState('16:00');
  const [missionEnd, setMissionEnd] = useState('17:00');
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);

  /** Device unlock is parent-gated; block only when a child profile is active. */
  const childMode = member?.roleCode === 'child';

  const children = useMemo(
    () => members.filter((m) => m.roleCode === 'child'),
    [members],
  );

  const childAtLimit = useMemo(() => {
    const max = subscription?.maxChildren;
    if (max == null) return false;
    return children.length >= max;
  }, [subscription?.maxChildren, children.length]);

  const activeRoutine = useMemo(() => {
    const id = editorRoutineId ?? resolved?.routineId;
    if (!id) return null;
    return routines.find((r) => r.id === id) ?? null;
  }, [editorRoutineId, resolved?.routineId, routines]);

  const modeRoutine = useMemo(() => {
    if (!resolved?.routineId) return null;
    return routines.find((r) => r.id === resolved.routineId) ?? null;
  }, [resolved, routines]);

  const reload = useCallback(async () => {
    if (!familyId) return;
    const families = await fetchFamilies();
    const fam = families.find((f) => f.id === familyId) ?? families[0];
    setMembers(fam?.members ?? []);
    const [rts, day, cal, sub] = await Promise.all([
      fetchFamilyRoutines(familyId).catch(() => [] as FamilyRoutineDto[]),
      ensureDayFlow(familyId).catch(() => null),
      resolveCalendarRoutine(familyId).catch(() => null),
      fetchFamilySubscription(familyId).catch(() => null),
    ]);
    setRoutines(rts);
    setFlow(day);
    setResolved(cal);
    setSubscription(sub);
    if (!missionChildId && fam?.members) {
      const kid = fam.members.find((m) => m.roleCode === 'child');
      if (kid) setMissionChildId(kid.id);
    }
  }, [familyId, missionChildId]);

  useEffect(() => {
    if (!familyId) {
      navigate('/unlock', { replace: true });
      return;
    }
    if (childMode) {
      navigate('/today', { replace: true });
      return;
    }
    void reload().catch(() => setError('Không tải được dữ liệu gia đình.'));
  }, [familyId, childMode, navigate, reload]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(t);
  }, [toast]);

  const onAddMember = async () => {
    if (!familyId || !newName.trim()) return;
    if (newRole === 'child' && childAtLimit) {
      setError(
        subscription?.upgradeHintVi ||
          `Gói hiện tại tối đa ${subscription?.maxChildren ?? 1} trẻ. Nâng gói để thêm con.`,
      );
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await addFamilyMember(familyId, {
        displayName: newName.trim(),
        roleCode: newRole,
      });
      setNewName('');
      setToast('Đã thêm thành viên.');
      await reload();
    } catch (err: unknown) {
      setError(
        isCapabilityPaywallError(err)
          ? getApiErrorMessage(err)
          : (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
            'Chưa thêm được thành viên.',
      );
    } finally {
      setBusy(false);
    }
  };

  const onAddMission = async () => {
    if (!familyId || !missionTitle.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await addAdHocCommitment(familyId, {
        memberId: missionChildId || undefined,
        title: missionTitle.trim(),
        windowStart: missionStart || undefined,
        windowEnd: missionEnd || undefined,
        flowDate: flow?.flowDate,
      });
      setMissionTitle('');
      setToast('Đã thêm việc vào hôm nay.');
      await reload();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Chưa thêm được việc.',
      );
    } finally {
      setBusy(false);
    }
  };

  const onMode = async (mode: string) => {
    if (!familyId) return;
    setBusy(true);
    setError(null);
    try {
      const result = await activateFamilyMode(familyId, {
        mode,
        activatedByMemberId: member?.id,
        confirmNow: true,
      });
      setToast(result.messageVi);
      await reload();
      // P2: scan adaptive proposals (incl. routine_trim) into Decision Inbox.
      void scanAdaptiveProposals(familyId).catch(() => undefined);
      if (result.primaryRoutineId) {
        setEditorRoutineId(result.primaryRoutineId);
        setEditorEditable(false);
      }
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Chưa đổi được chế độ.',
      );
    } finally {
      setBusy(false);
    }
  };

  const openRoutine = (routineId: string, editable: boolean) => {
    setEditorRoutineId(routineId);
    setEditorEditable(editable);
  };

  if (!familyId || childMode) return null;

  return (
    <section className="fa-page">
      <header className="fa-top">
        <button type="button" className="fa-back" onClick={() => navigate('/who')}>
          ←
        </button>
        <div>
          <h1>Quản trị gia đình</h1>
          <p>{familyName ?? 'Famixa'} · ngay trên điện thoại</p>
        </div>
        <Link to="/today" className="fa-today-link">
          Hôm nay
        </Link>
      </header>

      {toast ? (
        <p className="ph-action-toast" role="status">
          {toast}
        </p>
      ) : null}
      {error ? <p className="banner-error">{error}</p> : null}

      <section className="fa-card">
        <h2>Thành viên</h2>
        <ul className="fa-member-list">
          {members.length === 0 ? (
            <li className="fa-empty-row">
              <strong>Chưa có thành viên</strong>
              <span>Thêm bố/mẹ hoặc con bên dưới</span>
            </li>
          ) : (
            members.map((m) => (
              <li key={m.id}>
                <strong>{m.displayName}</strong>
                <span>{ROLE_LABEL[m.roleCode] ?? m.roleCode}</span>
              </li>
            ))
          )}
        </ul>
        <div className="fa-add-row">
          <input
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="Tên thành viên mới"
          />
          <select value={newRole} onChange={(e) => setNewRole(e.target.value)}>
            <option value="child">Con</option>
            <option value="guardian">Bố/Mẹ</option>
            <option value="caregiver">Người chăm sóc</option>
          </select>
          <button
            type="button"
            className="btn btn-primary"
            disabled={busy || (newRole === 'child' && childAtLimit)}
            onClick={() => void onAddMember()}
          >
            Thêm
          </button>
        </div>
        {newRole === 'child' && childAtLimit ? (
          <p className="fa-hint">
            Gói {subscription?.displayNameVi || 'hiện tại'} tối đa {subscription?.maxChildren}{' '}
            trẻ.
            {familyId ? (
              <>
                {' '}
                <Link
                  to={buildCheckoutPath({
                    productCode: 'family_os',
                    subjectType: 'family',
                    subjectId: familyId,
                    planCode:
                      subscription?.recommendedUpgradePlanCode || 'family_pro_month',
                    returnPath: '/family-admin',
                  })}
                >
                  Nâng gói
                </Link>
              </>
            ) : null}
          </p>
        ) : null}
      </section>

      <section className="fa-card">
        <h2>Thêm việc hôm nay</h2>
        <p className="fa-hint">
          Lịch con thay đổi từng ngày — thêm tại đây hoặc để con đề xuất, bạn chỉ 👍.
        </p>
        <select
          value={missionChildId}
          onChange={(e) => setMissionChildId(e.target.value)}
          disabled={children.length === 0}
        >
          {children.length === 0 ? (
            <option value="">Thêm con trước khi gán việc</option>
          ) : (
            children.map((c) => (
              <option key={c.id} value={c.id}>
                {c.displayName}
              </option>
            ))
          )}
        </select>
        <input
          value={missionTitle}
          onChange={(e) => setMissionTitle(e.target.value)}
          placeholder="Vd: Học thêm Toán / Đi sinh nhật"
        />
        <div className="fa-time-row">
          <input
            type="time"
            value={missionStart}
            onChange={(e) => setMissionStart(e.target.value)}
          />
          <input type="time" value={missionEnd} onChange={(e) => setMissionEnd(e.target.value)} />
        </div>
        <button
          type="button"
          className="btn btn-primary"
          disabled={busy || !missionTitle.trim() || children.length === 0}
          onClick={() => void onAddMission()}
        >
          Thêm vào hôm nay
        </button>
        {flow ? (
          <p className="fa-hint">
            Hôm nay ({flow.flowDate}): {flow.doneCount}/{flow.totalCommitments} việc.
          </p>
        ) : null}
      </section>

      <section className="fa-card">
        <h2>Chế độ gia đình</h2>
        <p className="fa-hint">1 chạm đổi Routine theo hè / thi / du lịch — không vào admin máy tính.</p>
        <div className="fa-mode-grid">
          {FAMILY_MODE_OPTIONS.map((m) => (
            <button
              key={m.value}
              type="button"
              disabled={busy}
              onClick={() => void onMode(m.value)}
            >
              <strong>{m.label}</strong>
              <span>{m.hint}</span>
            </button>
          ))}
        </div>
        {modeRoutine || resolved ? (
          <div className="fa-mode-active" role="status">
            <p>
              Đang dùng:{' '}
              <strong>{resolved?.routineDisplayName ?? modeRoutine?.displayName ?? '—'}</strong>
              {modeRoutine
                ? ` · ${modeRoutine.templates.filter((t) => t.isActive).length} việc`
                : null}
              {resolved?.periodDisplayName ? (
                <span className="fa-mode-period"> · {resolved.periodDisplayName}</span>
              ) : null}
            </p>
            {modeRoutine ? (
              <div className="fa-mode-actions">
                <button
                  type="button"
                  className="pill is-soft"
                  onClick={() => openRoutine(modeRoutine.id, false)}
                >
                  Xem việc
                </button>
                <button
                  type="button"
                  className="pill"
                  onClick={() => openRoutine(modeRoutine.id, true)}
                >
                  Chỉnh nhẹ
                </button>
              </div>
            ) : null}
          </div>
        ) : null}
      </section>

      <section className="fa-card">
        <h2>Routine hiện có</h2>
        {routines.length === 0 ? (
          <p className="fa-hint">Chưa có routine — chạy AI Onboarding để sinh starter.</p>
        ) : (
          <ul className="fa-routine-list">
            {routines.map((r) => (
              <li key={r.id}>
                <button
                  type="button"
                  className="fa-routine-open"
                  onClick={() => openRoutine(r.id, true)}
                >
                  <strong>{r.displayName}</strong>
                  <span>
                    {r.kind} · {r.templates.filter((t) => t.isActive).length} việc · chỉnh nhẹ →
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <button
          type="button"
          className="pill is-soft"
          onClick={() => navigate('/onboarding')}
        >
          Chạy lại AI Setup Wizard
        </button>
      </section>

      {activeRoutine && editorRoutineId ? (
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
    </section>
  );
}
