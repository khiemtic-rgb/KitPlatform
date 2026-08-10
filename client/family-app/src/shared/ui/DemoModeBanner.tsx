import { Link } from 'react-router-dom';
import { useSessionStore } from '@/shared/auth/session.store';

/** Sticky hint when browsing the GTM demo house. */
export function DemoModeBanner() {
  const demoMode = useSessionStore((s) => s.demoMode);
  const memberRole = useSessionStore((s) => s.member?.roleCode);
  const clear = useSessionStore((s) => s.clear);
  const show = demoMode || memberRole?.toLowerCase() === 'viewer';
  if (!show) return null;

  return (
    <div
      className="demo-mode-banner"
      role="status"
      style={{
        background: 'linear-gradient(90deg, #0f3d2e, #165c45)',
        color: '#f3faf6',
        padding: '0.55rem 0.85rem',
        fontSize: '0.875rem',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: 12,
        zIndex: 40,
      }}
    >
      <span>
        Nhà demo — chỉ xem. Muốn dùng thật cho nhà mình?{' '}
        <Link to="/unlock" onClick={() => clear()} style={{ color: '#b8f0d4', fontWeight: 600 }}>
          Tạo nhà
        </Link>
      </span>
    </div>
  );
}
