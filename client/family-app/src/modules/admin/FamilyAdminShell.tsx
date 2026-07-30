import { useEffect, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { useSessionStore } from '@/shared/auth/session.store';
import { useGoBack } from '@/shared/nav/use-go-back';

export const ROLE_LABEL: Record<string, string> = {
  guardian: 'Bố/Mẹ',
  caregiver: 'Người chăm sóc',
  child: 'Con',
  viewer: 'Xem',
};

type ShellProps = {
  title: string;
  subtitle?: string;
  backTo?: string;
  children: ReactNode;
  trailing?: ReactNode;
};

/** Shared chrome for Quản trị gia đình + sub-screens. */
export function FamilyAdminShell({
  title,
  subtitle,
  backTo = '/family-admin',
  children,
  trailing,
}: ShellProps) {
  const navigate = useNavigate();
  const goBack = useGoBack(backTo);
  const familyId = useSessionStore((s) => s.familyId);
  const member = useSessionStore((s) => s.member);
  const childMode = member?.roleCode === 'child';

  useEffect(() => {
    if (!familyId) {
      navigate('/unlock', { replace: true });
      return;
    }
    if (childMode) {
      navigate('/today', { replace: true });
    }
  }, [familyId, childMode, navigate]);

  if (!familyId || childMode) return null;

  return (
    <section className="fa-page">
      <header className="fa-top">
        <button
          type="button"
          className="fa-back"
          aria-label="Quay lại"
          onClick={goBack}
        >
          ←
        </button>
        <div className="fa-top-copy">
          <h1>{title}</h1>
          {subtitle ? <p>{subtitle}</p> : null}
        </div>
        {trailing ?? (
          <button type="button" className="fa-today-link" onClick={() => navigate('/today')}>
            Hôm nay
          </button>
        )}
      </header>
      {children}
    </section>
  );
}
