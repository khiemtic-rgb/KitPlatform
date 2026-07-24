import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilies, loginFamilyParent } from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';

export function ParentUnlockPage() {
  const navigate = useNavigate();
  const setParentSession = useSessionStore((s) => s.setParentSession);
  const setFamily = useSessionStore((s) => s.setFamily);
  const [tenantCode, setTenantCode] = useState('DEMO_FAMILY');
  const [username, setUsername] = useState('admin');
  const [password, setPassword] = useState('Admin@123');
  const [parentPin, setParentPin] = useState('1234');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      if (!/^\d{4}$/.test(parentPin)) {
        throw new Error('Mã bố mẹ phải gồm 4 chữ số');
      }
      const session = await loginFamilyParent({ tenantCode, username, password });
      setParentSession({
        accessToken: session.accessToken,
        refreshToken: session.refreshToken,
        tenantCode: tenantCode.trim().toUpperCase(),
        parentPin,
      });
      const families = await fetchFamilies();
      const family = families[0];
      if (!family) throw new Error('Chưa có gia đình. Chạy seed DEMO_FAMILY trước.');
      setFamily({ familyId: family.id, familyName: family.displayName });
      navigate('/who', { replace: true });
    } catch (err) {
      let message = 'Không mở được FamilyOS';
      if (err && typeof err === 'object' && 'response' in err) {
        const ax = err as { response?: { status?: number; data?: { message?: string } } };
        const apiMsg = ax.response?.data?.message?.trim();
        if (ax.response?.status === 401) {
          message =
            apiMsg ||
            'Sai mã gia đình / tài khoản / mật khẩu. Trên production cần tenant FamilyOS (vd. DEMO_FAMILY).';
        } else if (!ax.response) {
          message = 'Không kết nối được API — kiểm tra mạng hoặc proxy /api.';
        } else {
          message = apiMsg || `Lỗi API (${ax.response.status})`;
        }
      } else if (err instanceof Error) {
        message = err.message;
      }
      setError(message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <header className="stack" style={{ gap: 4 }}>
        <div className="brand-mark">FamilyOS</div>
        <div className="brand-sub">Một nhà · Một kế hoạch · Một ngày</div>
      </header>

      <section className="card stack">
        <p className="lead">Mở khóa cho cả nhà</p>
        <p className="muted" style={{ margin: 0 }}>
          Bố/Mẹ làm một lần. Sau đó các bé chỉ chạm tên của mình — không cần mật khẩu.
        </p>

        {error ? <div className="banner-error">{error}</div> : null}

        <form className="stack" onSubmit={onSubmit}>
          <div className="field">
            <label htmlFor="tenant">Mã gia đình</label>
            <input
              id="tenant"
              value={tenantCode}
              onChange={(e) => setTenantCode(e.target.value.toUpperCase())}
              autoComplete="organization"
              inputMode="text"
            />
          </div>
          <div className="field">
            <label htmlFor="user">Tài khoản phụ huynh</label>
            <input
              id="user"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
            />
          </div>
          <div className="field">
            <label htmlFor="pass">Mật khẩu</label>
            <input
              id="pass"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
          </div>
          <div className="field">
            <label htmlFor="pin">Mã bố mẹ (4 số)</label>
            <input
              id="pin"
              inputMode="numeric"
              pattern="\d{4}"
              maxLength={4}
              value={parentPin}
              onChange={(e) => setParentPin(e.target.value.replace(/\D/g, '').slice(0, 4))}
              autoComplete="off"
            />
          </div>
          <p className="muted" style={{ margin: 0, fontSize: '0.9rem' }}>
            Bé giữ nút hoặc bố mẹ nhập mã này để đổi người — demo mặc định <strong>1234</strong>.
          </p>
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {loading ? 'Đang mở…' : 'Mở cho cả nhà'}
          </button>
        </form>
      </section>
    </>
  );
}
