import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  fetchFamilies,
  loginFamilyParent,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';

/** Overridable at build time for pilot (defaults match local / GTM seed). */
const DEMO_TENANT = import.meta.env.VITE_FAMIXA_DEMO_TENANT || 'DEMO_FAMILY';
const DEMO_USER = import.meta.env.VITE_FAMIXA_DEMO_USER || 'demo';
const DEMO_PASS = import.meta.env.VITE_FAMIXA_DEMO_PASSWORD || 'Admin@123';

/**
 * One-tap GTM entry: viewer login → pick persona → browse read-only.
 */
export function DemoEnterPage() {
  const navigate = useNavigate();
  const setParentSession = useSessionStore((s) => s.setParentSession);
  const setFamily = useSessionStore((s) => s.setFamily);
  const clear = useSessionStore((s) => s.clear);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function enter() {
      setBusy(true);
      setError(null);
      try {
        clear();
        const session = await loginFamilyParent({
          tenantCode: DEMO_TENANT,
          username: DEMO_USER,
          password: DEMO_PASS,
        });
        if (cancelled) return;
        if (session.kind !== 'session') {
          throw new Error('Tài khoản demo cần chọn workspace — dùng DEMO_FAMILY/demo trong đăng nhập nâng cao.');
        }
        setParentSession({
          accessToken: session.accessToken,
          refreshToken: session.refreshToken,
          tenantCode: session.tenantCode || DEMO_TENANT,
          demoMode: true,
        });
        const families = await fetchFamilies();
        const house = families[0];
        if (!house) throw new Error('Nhà demo chưa có dữ liệu — chạy seed-family-os-local.ps1');
        setFamily({ familyId: house.id, familyName: house.displayName });
        navigate('/who', { replace: true });
      } catch (err) {
        if (cancelled) return;
        const msg =
          err && typeof err === 'object' && 'response' in err
            ? ((err as { response?: { data?: { message?: string } } }).response?.data?.message ??
              'Không vào được nhà demo')
            : err instanceof Error
              ? err.message
              : 'Không vào được nhà demo';
        setError(msg);
        setBusy(false);
      }
    }

    void enter();
    return () => {
      cancelled = true;
    };
  }, [clear, navigate, setFamily, setParentSession]);

  return (
    <div className="unlock-screen demo-enter">
      <div className="unlock-card" style={{ maxWidth: 420, margin: '0 auto', padding: '2rem 1.25rem' }}>
        <p className="eyebrow" style={{ letterSpacing: '0.08em', opacity: 0.7, marginBottom: 8 }}>
          FAMIXA
        </p>
        <h1 style={{ fontSize: '1.6rem', margin: '0 0 0.5rem' }}>Nhà demo</h1>
        <p style={{ margin: '0 0 1.25rem', lineHeight: 1.45, opacity: 0.85 }}>
          2 bé · mùa hè + lịch đi học sẵn. Chỉ xem — không sửa được nhà này.
        </p>
        {busy && !error ? <p>Đang mở nhà demo…</p> : null}
        {error ? (
          <div>
            <p role="alert" style={{ color: '#b42318', marginBottom: 12 }}>
              {error}
            </p>
            <button type="button" className="btn primary" onClick={() => window.location.reload()}>
              Thử lại
            </button>
            <button
              type="button"
              className="btn"
              style={{ marginLeft: 8 }}
              onClick={() => navigate('/unlock')}
            >
              Đăng nhập thường
            </button>
          </div>
        ) : null}
      </div>
    </div>
  );
}
