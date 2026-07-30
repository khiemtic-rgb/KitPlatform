import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  addFamilyMember,
  createFamilyInvite,
  fetchFamilies,
  fetchFamilySubscription,
  formatFamilyInviteShare,
  updateFamilyMember,
  type FamilyInvite,
  type FamilyMembership,
  type FamilySubscription,
} from '@/shared/api/family-os.api';
import { buildCheckoutPath } from '@/shared/api/payment.api';
import { getApiErrorMessage, isCapabilityPaywallError } from '@/shared/billing/capability-error';
import { shareOrCopyNudge } from '@/shared/nudge/nudge';
import { useSessionStore } from '@/shared/auth/session.store';
import { avatarEmoji, inferGenderFromName } from '@/shared/ui/avatarGender';
import { FamilyAdminShell, ROLE_LABEL } from '@/modules/admin/FamilyAdminShell';

function memberEmoji(m: FamilyMembership): string {
  return avatarEmoji(inferGenderFromName(m.displayName), m.roleCode);
}

function roleSelectClass(roleCode: string): string {
  if (roleCode === 'child') return 'is-child';
  if (roleCode === 'guardian' || roleCode === 'caregiver') return 'is-parent';
  return 'is-other';
}

export function FamilyMembersPage() {
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const selfId = useSessionStore((s) => s.member?.id);
  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [subscription, setSubscription] = useState<FamilySubscription | null>(null);
  const [invite, setInvite] = useState<FamilyInvite | null>(null);
  const [inviteBusy, setInviteBusy] = useState(false);
  const [newName, setNewName] = useState('');
  const [newRole, setNewRole] = useState('child');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [toast, setToast] = useState<string | null>(null);
  const [menuId, setMenuId] = useState<string | null>(null);

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

  const loadInvite = useCallback(async () => {
    if (!familyId) return;
    setInviteBusy(true);
    try {
      setInvite(
        await createFamilyInvite(familyId, {
          roleCode: 'guardian',
          maxUses: 3,
          validDays: 7,
        }),
      );
    } catch {
      setInvite(null);
    } finally {
      setInviteBusy(false);
    }
  }, [familyId]);

  useEffect(() => {
    void reload().catch(() => setError('Không tải được thành viên.'));
    void loadInvite();
  }, [reload, loadInvite]);

  useEffect(() => {
    if (!toast) return;
    const t = window.setTimeout(() => setToast(null), 2800);
    return () => window.clearTimeout(t);
  }, [toast]);

  useEffect(() => {
    if (!menuId) return;
    const close = () => setMenuId(null);
    window.addEventListener('click', close);
    return () => window.removeEventListener('click', close);
  }, [menuId]);

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
    setMenuId(null);
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

  const onCopyCode = async () => {
    if (!invite) return;
    try {
      await shareOrCopyNudge(invite.code);
      setToast('Đã sao chép mã nhà.');
    } catch {
      setToast('Chưa copy được — chọn mã và copy tay.');
    }
  };

  const onShareCode = async () => {
    if (!invite) return;
    try {
      const text = formatFamilyInviteShare({
        code: invite.code,
        familyName,
        expiresAt: invite.expiresAt,
      });
      const how = await shareOrCopyNudge(text, { preferShare: true });
      setToast(how === 'shared' ? 'Đã mở chia sẻ.' : 'Đã sao chép nội dung mời.');
    } catch {
      setToast('Chưa chia sẻ được — thử Sao chép.');
    }
  };

  return (
    <FamilyAdminShell
      title="Thành viên"
      subtitle="Thêm, đổi vai trò và mời vào nhà"
      backTo="/family-admin/settings"
    >
      {toast ? (
        <p className="ph-action-toast" role="status">
          {toast}
        </p>
      ) : null}
      {error ? <p className="banner-error">{error}</p> : null}

      <section className="fa-card fa-mem-card">
        <header className="fa-mem-card-head">
          <span className="fa-mem-ico" aria-hidden>
            👥
          </span>
          <div>
            <h2>Danh sách thành viên</h2>
            <p>Quản lý các thành viên trong gia đình</p>
          </div>
        </header>

        <ul className="fa-mem-list">
          {members.length === 0 ? (
            <li className="fa-empty-row">
              <strong>Chưa có thành viên</strong>
              <span>Thêm bố/mẹ hoặc con bên dưới</span>
            </li>
          ) : (
            members.map((m) => {
              const isSelf = m.id === selfId;
              return (
                <li key={m.id} className="fa-mem-row">
                  <span className="fa-mem-avatar" aria-hidden>
                    {memberEmoji(m)}
                  </span>
                  <div className="fa-mem-body">
                    <strong>
                      {m.displayName}
                      {isSelf ? <i className="fa-mem-you">Bạn</i> : null}
                    </strong>
                    <em>Vai trò: {ROLE_LABEL[m.roleCode] ?? m.roleCode}</em>
                  </div>
                  <label className={`fa-mem-role ${roleSelectClass(m.roleCode)}`}>
                    <span aria-hidden>
                      {m.roleCode === 'child' ? '🧒' : m.roleCode === 'viewer' ? '👁' : '👑'}
                    </span>
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
                  </label>
                  <div className="fa-mem-more">
                    <button
                      type="button"
                      className="fa-mem-dots"
                      aria-label={`Tuỳ chọn ${m.displayName}`}
                      disabled={busy || isSelf}
                      onClick={(e) => {
                        e.stopPropagation();
                        setMenuId((id) => (id === m.id ? null : m.id));
                      }}
                    >
                      ⋯
                    </button>
                    {menuId === m.id ? (
                      <div className="fa-mem-menu" role="menu">
                        <button
                          type="button"
                          role="menuitem"
                          disabled={busy}
                          onClick={(e) => {
                            e.stopPropagation();
                            void onDeactivate(m);
                          }}
                        >
                          Ẩn khỏi nhà
                        </button>
                      </div>
                    ) : null}
                  </div>
                </li>
              );
            })
          )}
        </ul>
      </section>

      <section className="fa-card fa-mem-card">
        <header className="fa-mem-card-head">
          <span className="fa-mem-ico" aria-hidden>
            ➕
          </span>
          <div>
            <h2>Thêm thành viên</h2>
            <p>Mời thêm thành viên vào gia đình</p>
          </div>
        </header>
        <div className="fa-mem-add">
          <input
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="Nhập tên thành viên"
            aria-label="Tên thành viên mới"
            onKeyDown={(e) => {
              if (e.key === 'Enter') void onAdd();
            }}
          />
          <label className={`fa-mem-role ${roleSelectClass(newRole)}`}>
            <span aria-hidden>{newRole === 'child' ? '🧒' : '👑'}</span>
            <select
              value={newRole}
              onChange={(e) => setNewRole(e.target.value)}
              aria-label="Vai trò thành viên mới"
            >
              <option value="child">Con</option>
              <option value="guardian">Bố/Mẹ</option>
              <option value="caregiver">Người chăm sóc</option>
            </select>
          </label>
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

      <section className="fa-card fa-mem-card">
        <header className="fa-mem-card-head">
          <span className="fa-mem-ico" aria-hidden>
            🔑
          </span>
          <div>
            <h2>Mời bằng mã nhà</h2>
            <p>Người khác dùng mã này để gia nhập nhà của bạn</p>
          </div>
        </header>

        <div className="fa-mem-code-box">
          <span className="fa-mem-code-ico" aria-hidden>
            🏡
          </span>
          <div>
            <em>Mã nhà của bạn</em>
            <strong aria-live="polite">
              {inviteBusy && !invite ? '…' : invite?.code ?? 'Chưa tạo được mã'}
            </strong>
          </div>
          <button
            type="button"
            className="pill"
            disabled={!invite || inviteBusy}
            onClick={() => void onCopyCode()}
          >
            Sao chép
          </button>
        </div>

        <button
          type="button"
          className="fa-mem-share"
          disabled={!invite || inviteBusy}
          onClick={() => void onShareCode()}
        >
          Chia sẻ mã nhà
        </button>

        <div className="fa-mem-safe">
          <div>
            <strong>Mã nhà giúp gia đình kết nối an toàn</strong>
            <p>Chỉ những người có mã này mới có thể tham gia vào nhà của bạn.</p>
          </div>
          <span aria-hidden>🛡️</span>
        </div>
      </section>
    </FamilyAdminShell>
  );
}
