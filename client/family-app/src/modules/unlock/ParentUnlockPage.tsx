import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  acceptFamilyInvite,
  fetchFamilies,
  loginFamilyParent,
  registerFamily,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';

type Mode = 'login' | 'register' | 'join';

function apiError(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const ax = err as { response?: { status?: number; data?: { message?: string } } };
    const apiMsg = ax.response?.data?.message?.trim();
    if (ax.response?.status === 401) {
      return apiMsg || 'Sai mã gia đình / tài khoản / mật khẩu.';
    }
    if (!ax.response) return 'Không kết nối được API — kiểm tra mạng hoặc proxy /api.';
    return apiMsg || `Lỗi API (${ax.response.status})`;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

export function ParentUnlockPage() {
  const navigate = useNavigate();
  const setParentSession = useSessionStore((s) => s.setParentSession);
  const setFamily = useSessionStore((s) => s.setFamily);
  const [mode, setMode] = useState<Mode>('login');
  const [tenantCode, setTenantCode] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [parentPin, setParentPin] = useState('1234');
  const [familyName, setFamilyName] = useState('');
  const [parentDisplayName, setParentDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [child1Name, setChild1Name] = useState('');
  const [child2Name, setChild2Name] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const finish = async (input: {
    accessToken: string;
    refreshToken: string | null;
    tenantCode: string;
    parentPin: string;
    familyId?: string;
    familyName?: string;
  }) => {
    setParentSession({
      accessToken: input.accessToken,
      refreshToken: input.refreshToken,
      tenantCode: input.tenantCode.trim().toUpperCase(),
      parentPin: input.parentPin,
    });
    if (input.familyId && input.familyName) {
      setFamily({ familyId: input.familyId, familyName: input.familyName });
    } else {
      const families = await fetchFamilies();
      if (families.length === 0) {
        throw new Error('Tài khoản chưa gắn gia đình. Đăng ký nhà mới hoặc dùng mã mời.');
      }
      const family =
        families.length === 1
          ? families[0]
          : families.find((f) => f.displayName) ?? families[0];
      setFamily({ familyId: family.id, familyName: family.displayName });
    }
    navigate('/who', { replace: true });
  };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      if (!/^\d{4}$/.test(parentPin)) {
        throw new Error('Mã bố mẹ phải gồm 4 chữ số');
      }

      if (mode === 'login') {
        const session = await loginFamilyParent({ tenantCode, username, password });
        await finish({
          accessToken: session.accessToken,
          refreshToken: session.refreshToken,
          tenantCode,
          parentPin,
        });
        return;
      }

      if (mode === 'register') {
        const created = await registerFamily({
          familyName,
          parentDisplayName,
          username,
          email,
          password,
          parentPin,
          child1Name: child1Name || undefined,
          child2Name: child2Name || undefined,
        });
        await finish({
          accessToken: created.accessToken,
          refreshToken: created.refreshToken,
          tenantCode: created.tenantCode,
          parentPin,
          familyId: created.familyId,
          familyName: created.familyName,
        });
        return;
      }

      const joined = await acceptFamilyInvite({
        code: inviteCode,
        parentDisplayName,
        username,
        email,
        password,
        parentPin,
      });
      await finish({
        accessToken: joined.accessToken,
        refreshToken: joined.refreshToken,
        tenantCode: joined.tenantCode,
        parentPin,
        familyId: joined.familyId,
        familyName: joined.familyName,
      });
    } catch (err) {
      setError(apiError(err, 'Không mở được FamilyOS'));
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

        <div className="stack" style={{ gap: 8, flexDirection: 'row', flexWrap: 'wrap' }}>
          {(
            [
              ['login', 'Đăng nhập'],
              ['register', 'Tạo nhà mới'],
              ['join', 'Tham gia bằng mã'],
            ] as const
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              className={`btn${mode === id ? ' btn-primary' : ''}`}
              onClick={() => {
                setMode(id);
                setError(null);
              }}
            >
              {label}
            </button>
          ))}
        </div>

        {error ? <div className="banner-error">{error}</div> : null}

        <form className="stack" onSubmit={onSubmit}>
          {mode === 'login' ? (
            <>
              <div className="field">
                <label htmlFor="tenant">Mã gia đình</label>
                <input
                  id="tenant"
                  value={tenantCode}
                  onChange={(e) => setTenantCode(e.target.value.toUpperCase())}
                  autoComplete="organization"
                  placeholder="VD: FOS_NHAXYZ123"
                  required
                />
              </div>
            </>
          ) : null}

          {mode === 'register' ? (
            <>
              <div className="field">
                <label htmlFor="familyName">Tên nhà</label>
                <input
                  id="familyName"
                  value={familyName}
                  onChange={(e) => setFamilyName(e.target.value)}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="parentName">Tên phụ huynh</label>
                <input
                  id="parentName"
                  value={parentDisplayName}
                  onChange={(e) => setParentDisplayName(e.target.value)}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="email">Email</label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="child1">Con 1 (tuỳ chọn)</label>
                <input id="child1" value={child1Name} onChange={(e) => setChild1Name(e.target.value)} />
              </div>
              <div className="field">
                <label htmlFor="child2">Con 2 (tuỳ chọn)</label>
                <input id="child2" value={child2Name} onChange={(e) => setChild2Name(e.target.value)} />
              </div>
            </>
          ) : null}

          {mode === 'join' ? (
            <>
              <div className="field">
                <label htmlFor="invite">Mã mời</label>
                <input
                  id="invite"
                  value={inviteCode}
                  onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="parentNameJoin">Tên phụ huynh</label>
                <input
                  id="parentNameJoin"
                  value={parentDisplayName}
                  onChange={(e) => setParentDisplayName(e.target.value)}
                  required
                />
              </div>
              <div className="field">
                <label htmlFor="emailJoin">Email</label>
                <input
                  id="emailJoin"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>
            </>
          ) : null}

          <div className="field">
            <label htmlFor="user">Tài khoản phụ huynh</label>
            <input
              id="user"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoComplete="username"
              required
            />
          </div>
          <div className="field">
            <label htmlFor="pass">Mật khẩu</label>
            <input
              id="pass"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
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
              required
            />
          </div>
          <p className="muted" style={{ margin: 0, fontSize: '0.9rem' }}>
            {mode === 'register'
              ? 'Sau khi tạo nhà sẽ nhận mã gia đình (tenant) để đăng nhập lại / mời bố mẹ kia.'
              : 'Bé giữ nút hoặc bố mẹ nhập mã này để đổi người.'}
          </p>
          <button className="btn btn-primary" type="submit" disabled={loading}>
            {loading
              ? 'Đang xử lý…'
              : mode === 'login'
                ? 'Mở cho cả nhà'
                : mode === 'register'
                  ? 'Tạo nhà & dùng thử 30 ngày'
                  : 'Tham gia nhà'}
          </button>
        </form>
      </section>
    </>
  );
}
