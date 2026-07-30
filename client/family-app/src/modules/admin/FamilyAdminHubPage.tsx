import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  fetchFamilies,
  fetchFamilyRoutines,
  resolveCalendarRoutine,
  type FamilyMembership,
  type ResolvedCalendarRoutine,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { isOnboardingDone } from '@/shared/onboarding/onboarding';
import { FamilyAdminShell, ROLE_LABEL } from '@/modules/admin/FamilyAdminShell';

type HubCardProps = {
  icon: string;
  title: string;
  subtitle: string;
  status?: string;
  done?: boolean;
  progress?: number | null;
  onClick: () => void;
};

function HubCard({ icon, title, subtitle, status, done, progress, onClick }: HubCardProps) {
  return (
    <button type="button" className="fa-hub-card" onClick={onClick}>
      <span className="fa-hub-ico" aria-hidden>
        {icon}
      </span>
      <span className="fa-hub-body">
        <strong>{title}</strong>
        <span className="fa-hub-sub">{subtitle}</span>
        {status ? <span className="fa-hub-status">{status}</span> : null}
        {progress != null && progress < 100 ? (
          <span className="fa-hub-bar" aria-hidden>
            <span style={{ width: `${Math.max(8, Math.min(100, progress))}%` }} />
          </span>
        ) : null}
        {done ? <span className="fa-hub-done">Đã hoàn thành ✓</span> : null}
      </span>
      <span className="fa-hub-chevron" aria-hidden>
        ›
      </span>
    </button>
  );
}

export function FamilyAdminHubPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [resolved, setResolved] = useState<ResolvedCalendarRoutine | null>(null);
  const [routineCount, setRoutineCount] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    if (!familyId) return;
    const families = await fetchFamilies();
    const fam = families.find((f) => f.id === familyId) ?? families[0];
    setMembers(fam?.members ?? []);
    const [rts, cal] = await Promise.all([
      fetchFamilyRoutines(familyId).catch(() => []),
      resolveCalendarRoutine(familyId).catch(() => null),
    ]);
    setRoutineCount(rts.length);
    setResolved(cal);
  }, [familyId]);

  useEffect(() => {
    void reload().catch(() => setError('Không tải được tổng quan gia đình.'));
  }, [reload]);

  const memberNames = useMemo(() => {
    if (members.length === 0) return 'Thêm bố/mẹ hoặc con';
    return members
      .slice(0, 3)
      .map((m) => m.displayName)
      .join(' · ')
      .concat(members.length > 3 ? ` +${members.length - 3}` : '');
  }, [members]);

  const memberRoles = useMemo(() => {
    if (members.length === 0) return undefined;
    return members
      .slice(0, 3)
      .map((m) => ROLE_LABEL[m.roleCode] ?? m.roleCode)
      .join(' · ');
  }, [members]);

  const aiDone = familyId ? isOnboardingDone(familyId) : false;
  const routineLabel =
    resolved?.routineDisplayName ??
    (routineCount > 0 ? 'Đã có routine' : 'Chưa có routine');
  const routineSub = resolved?.periodDisplayName
    ? resolved.periodDisplayName
    : routineCount > 0
      ? 'Chạm để đổi chế độ / chỉnh'
      : 'Thiết lập lịch ngày cho nhà';

  return (
    <FamilyAdminShell
      title="Quản trị gia đình"
      subtitle={familyName ?? 'Nhà mình'}
      backTo="/today"
    >
      {error ? <p className="banner-error">{error}</p> : null}

      <div className="fa-hub-list">
        <HubCard
          icon="👨‍👩‍👧"
          title="Thành viên"
          subtitle={`${members.length} thành viên`}
          status={[memberNames, memberRoles].filter(Boolean).join(' · ')}
          onClick={() => navigate('/family-admin/members')}
        />
        <HubCard
          icon="🔄"
          title="Routine"
          subtitle={routineLabel}
          status={routineSub}
          onClick={() => navigate('/family-admin/routine')}
        />
        <HubCard
          icon="🏠"
          title="Mã nhà"
          subtitle="Mời bố/mẹ / người thân"
          status="Sao chép · Chia sẻ · QR"
          onClick={() => navigate('/family-admin/invite')}
        />
        <HubCard
          icon="🤖"
          title="AI Setup"
          subtitle={aiDone ? 'Đã hoàn thành' : 'Thiết lập hồ sơ nhà'}
          done={aiDone}
          progress={aiDone ? null : 40}
          status={aiDone ? undefined : 'Chưa xong — tiếp tục wizard'}
          onClick={() => navigate('/onboarding')}
        />
        <HubCard
          icon="⚙️"
          title="Tài khoản / Cài đặt"
          subtitle="Gói · nhắc việc · PIN · đăng xuất"
          onClick={() => navigate('/family-admin/settings')}
        />
      </div>
    </FamilyAdminShell>
  );
}

/** @deprecated use FamilyAdminHubPage — kept name for existing imports */
export { FamilyAdminHubPage as FamilyAdminPage };
