import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  addFamilyMember,
  fetchFamilies,
  fetchFamilySubscription,
  updateFamilyMember,
  type FamilyMembership,
  type FamilySubscription,
} from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { getApiErrorMessage, isCapabilityPaywallError } from '@/shared/billing/capability-error';
import { useSessionStore } from '@/shared/auth/session.store';
import { FamilyAdminShell, ROLE_LABEL } from '@/modules/admin/FamilyAdminShell';

export function FamilyMembersPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [newName, setNewName] = useState('');
  const [newRole, setNewRole] = useState('child');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);

  const children = useMemo(
    () => members.filter((m) => m.roleCode === 'child'),
    [members],
  );
  const childAtLimit = useMemo(() => {
    const max = subscription?.maxChildren;
    if (max == null) return false;
    return children.length >= max;
  }, [subscription?.maxChildren, children.length]);

  const reload = useCallback(async () => {
    if (!familyId) return;
    const families = await fetchFamilies();
    const fam = families.find((f) => f.id === familyId) ?? families[0];
    setMembers(fam?.members ?? []);
    setSubscription(await fetchFamilySubscription(familyId).catch(() => null));
  }, [familyId]);

  useEffect(() => {
    void reload().catch(() => setError('Không tải được thành viên.'));
  }, [reload]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(t);
  }, [toast]);

  const onAdd = async () => {
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
          : getApiErrorMessage(err) || 'Chưa thêm được thành viên.',
      );
    } finally {
      setBusy(false);
    }
  };

  const onRoleChange = async (m: FamilyMembership, roleCode: string) => {
    if (!familyId || roleCode === m.roleCode) return;
    setBusy(true);
    setError(null);
    try {
      await updateFamilyMember(familyId, m.id, { roleCode });
      setToast(`Đã đổi vai trò «${m.displayName}».`);
      await reload();
    } catch (err: unknown) {
      setError(getApiErrorMessage(err) || 'Chưa đổi được vai trò.');
    } finally {
      setBusy(false);
    }
  };

  const onDeactivate = async (m: FamilyMembership) => {
    if (!familyId) return;
    if (!window.confirm(`Ẩn thành viên «${m.displayName}» khỏi nhà?`)) return;
    setBusy(true);
    setError(null);
    try {
      await updateFamilyMember(familyId, m.id, { status: 'inactive' });
      setToast('Đã ẩn thành viên.');
      await reload();
    } catch (err: unknown) {
      setError(getApiErrorMessage(err) || 'Chưa ẩn được thành viên.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <FamilyAdminShell title="Thành viên" subtitle="Thêm · đổi vai · mời vào nhà">
      {toast ? (
        <p className="ph-action-toast" role="status">
          {toast}
        </p>
      ) : null}
      {error ? <p className="banner-error">{error}</p> : null}

      <section className="fa-card">
        <h2>Danh sách</h2>
        <ul className="fa-member-list">
          {members.length === 0 ? (
            <li className="fa-empty-row">
              <strong>Chưa có thành viên</strong>
              <span>Thêm bố/mẹ hoặc con bên dưới</span>
            </li>
          ) : (
            members.map((m) => (
              <li key={m.id} className="fa-member-row">
                <div>
                  <strong>{m.displayName}</strong>
                  <span>{ROLE_LABEL[m.roleCode] ?? m.roleCode}</span>
                </div>
                <div className="fa-member-actions">
                  <select
                    value={m.roleCode}
                    disabled={busy}
                    aria-label={`Vai trò ${m.displayName}`}
                    onChange={(e) => void onRoleChange(m, e.target.value)}
                  >
                    <option value="child">Con</option>
                    <option value="guardian">Bố/Mẹ</option>
                    <option value="caregiver">Người chăm sóc</option>
                    <option value="viewer">Xem</option>
                  </select>
                  <button type="button" className="pill is-soft" disabled={busy} onClick={() => void onDeactivate(m)}>
                    Ẩn
                  </button>
                </div>
              </li>
            ))
          )}
        </ul>
      </section>

      <section className="fa-card">
        <h2>Thêm thành viên</h2>
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
            disabled={busy || !newName.trim() || (newRole === 'child' && childAtLimit)}
            onClick={() => void onAdd()}
          >
            Thêm
          </button>
        </div>
        {newRole === 'child' && childAtLimit && familyId ? (
          <p className="fa-hint">
            Gói {subscription?.displayNameVi || 'hiện tại'} tối đa {subscription?.maxChildren} trẻ.{' '}
            <Link
              to={buildCheckoutPath({
                productCode: 'family_os',
                subjectType: 'family',
                subjectId: familyId,
                planCode: subscription?.recommendedUpgradePlanCode || 'family_pro_month',
                returnPath: '/family-admin/members',
              })}
            >
              Nâng gói
            </Link>
          </p>
        ) : null}
      </section>

      <section className="fa-card fa-card-action">
        <h2>Mời bằng mã nhà</h2>
        <p className="fa-hint">Bố/mẹ kia hoặc ông bà vào app → Gia nhập nhà bằng mã.</p>
        <button type="button" className="btn btn-primary" onClick={() => navigate('/family-admin/invite')}>
          Mở mã nhà →
        </button>
      </section>
    </FamilyAdminShell>
  );
}
