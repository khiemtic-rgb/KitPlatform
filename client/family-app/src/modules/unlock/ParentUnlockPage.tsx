import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  acceptFamilyInvite,
  fetchFamilies,
  loginFamilyByEmail,
  loginFamilyParent,
  registerFamily,
  selectFamilyWorkspace,
  type AuthWorkspace,
  type FamilyLoginResult,
} from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';

type Mode = 'login' | 'register';
type LoginStyle = 'email' | 'tenant';

function apiError(err: unknown, fallback: string): string {
  if (err && typeof err === 'object' && 'response' in err) {
    const ax = err as { response?: { status?: number; data?: { message?: string } } };
    const apiMsg = ax.response?.data?.message?.trim();
    if (ax.response?.status === 401) {
      return apiMsg || 'Sai email / mã gia đình / tài khoản / mật khẩu.';
    }
    if (!ax.response) return 'Không kết nối được API — kiểm tra mạng hoặc proxy /api.';
    return apiMsg || `Lỗi API (${ax.response.status})`;
  }
  if (err instanceof Error) return err.message;
  return fallback;
}

function productLabel(code: string): string {
  switch (code) {
    case 'pharmacy':
      return 'Pharmacy';
    case 'clinic':
      return 'Clinic';
    case 'family_os':
      return 'Family OS';
    default:
      return code;
  }
}

function usernameFromEmail(email: string): string {
  const local = email.split('@')[0] ?? '';
  const cleaned = local.replace(/[^a-zA-Z0-9_]/g, '').slice(0, 24);
  return cleaned || 'parent';
}

function IconUser() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <circle cx="12" cy="8" r="3.5" fill="none" stroke="currentColor" strokeWidth="1.8" />
      <path d="M5 19c1.8-3.2 4.2-4.8 7-4.8s5.2 1.6 7 4.8" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function IconMail() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <rect x="3.5" y="5.5" width="17" height="13" rx="2" fill="none" stroke="currentColor" strokeWidth="1.8" />
      <path d="M4.5 7.5 12 13l7.5-5.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function IconLock() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <rect x="5" y="10.5" width="14" height="9.5" rx="2" fill="none" stroke="currentColor" strokeWidth="1.8" />
      <path d="M8 10.5V8a4 4 0 0 1 8 0v2.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  );
}

function IconHome() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <path d="M4 11.5 12 5l8 6.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M6.5 10.5V19h11v-8.5" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
    </svg>
  );
}

function IconTag() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <path d="M3.5 12.5V5.8A2.3 2.3 0 0 1 5.8 3.5h6.7l8 8-6.7 6.7-8-8z" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      <circle cx="8.2" cy="8.2" r="1.2" fill="currentColor" />
    </svg>
  );
}

function IconEye({ off }: { off?: boolean }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      {off ? (
        <>
          <path d="M3 3l18 18" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
          <path d="M10.5 6.4A10.6 10.6 0 0 1 12 6.2c5 0 8.8 4.2 9.7 5.8-.4.7-1.4 2.2-3.2 3.6M7.2 7.6C4.8 9.1 3.5 11.2 3.3 12c.9 1.6 4.7 5.8 9.7 5.8 1.2 0 2.3-.2 3.3-.6" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </>
      ) : (
        <>
          <path d="M2.8 12S6.5 6.2 12 6.2 21.2 12 21.2 12 17.5 17.8 12 17.8 2.8 12 2.8 12z" fill="none" stroke="currentColor" strokeWidth="1.8" />
          <circle cx="12" cy="12" r="2.6" fill="none" stroke="currentColor" strokeWidth="1.8" />
        </>
      )}
    </svg>
  );
}

const LAST_EMAIL_KEY = 'famixa.lastEmail';

function readLastEmail(): string {
  try {
    return (localStorage.getItem(LAST_EMAIL_KEY) || '').trim();
  } catch {
    return '';
  }
}

function rememberEmail(email: string) {
  try {
    const mail = email.trim().toLowerCase();
    if (mail.includes('@')) localStorage.setItem(LAST_EMAIL_KEY, mail);
  } catch {
    /* ignore */
  }
}

export function ParentUnlockPage() {
  const navigate = useNavigate();
  const setParentSession = useSessionStore((s) => s.setParentSession);
  const setFamily = useSessionStore((s) => s.setFamily);
  const lastEmail = readLastEmail();
  const [mode, setMode] = useState<Mode>('login');
  const [loginStyle, setLoginStyle] = useState<LoginStyle>('email');
  const [showAdvancedLogin, setShowAdvancedLogin] = useState(false);
  const [showInvite, setShowInvite] = useState(false);
  const [tenantCode, setTenantCode] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [parentPin] = useState('1234');
  const [parentDisplayName, setParentDisplayName] = useState('');
  const [email, setEmail] = useState(lastEmail);
  const [inviteCode, setInviteCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [choiceToken, setChoiceToken] = useState<string | null>(null);
  const [workspaces, setWorkspaces] = useState<AuthWorkspace[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);

  const switchMode = (next: Mode) => {
    setMode(next);
    setError(null);
    setChoiceToken(null);
    setWorkspaces([]);
    if (next === 'register') setLoginStyle('email');
  };

  const openInviteOnRegister = () => {
    switchMode('register');
    setShowInvite(true);
  };

  const finish = async (input: {
    accessToken: string;
    refreshToken: string | null;
    tenantCode: string;
    parentPin: string;
    familyId?: string;
    familyName?: string;
    emailHint?: string;
  }) => {
    if (input.emailHint) rememberEmail(input.emailHint);
    else if (email.trim()) rememberEmail(email);

    setParentSession({
      accessToken: input.accessToken,
      refreshToken: input.refreshToken,
      tenantCode: input.tenantCode.trim().toUpperCase(),
      parentPin: input.parentPin,
      demoMode: false,
    });
    if (input.familyId && input.familyName) {
      setFamily({ familyId: input.familyId, familyName: input.familyName });
    } else {
      const families = await fetchFamilies();
      if (families.length === 0) {
        throw new Error(
          'Workspace này chưa có gia đình Family OS. Chọn nhà Famixa khác hoặc tạo nhà mới bằng cùng email Kit.',
        );
      }
      const family =
        families.length === 1
          ? families[0]
          : families.find((f) => f.displayName) ?? families[0];
      setFamily({ familyId: family.id, familyName: family.displayName });
    }
    navigate('/who', { replace: true });
  };

  const handleLoginResult = async (result: FamilyLoginResult) => {
    if (result.kind === 'choice') {
      setChoiceToken(result.selectionToken);
      setWorkspaces(result.workspaces);
      setSelectedUserId(
        result.workspaces.find((w) => w.isDefault)?.userId ?? result.workspaces[0]?.userId ?? null,
      );
      return;
    }
    await finish({
      accessToken: result.accessToken,
      refreshToken: result.refreshToken,
      tenantCode: result.tenantCode || tenantCode,
      parentPin,
      emailHint: email,
    });
  };

  const onSelectWorkspace = async () => {
    if (!choiceToken || !selectedUserId) {
      setError('Chọn một nhà / workspace để tiếp tục.');
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const session = await selectFamilyWorkspace({
        selectionToken: choiceToken,
        userId: selectedUserId,
      });
      setChoiceToken(null);
      setWorkspaces([]);
      await finish({
        accessToken: session.accessToken,
        refreshToken: session.refreshToken,
        tenantCode: session.tenantCode,
        parentPin,
        emailHint: email,
      });
    } catch (err) {
      setError(apiError(err, 'Không chọn được workspace'));
    } finally {
      setLoading(false);
    }
  };

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      if (!/^\d{4}$/.test(parentPin)) {
        throw new Error('Mã bố mẹ phải gồm 4 chữ số');
      }
      if (mode === 'register' && password.trim().length < 8) {
        throw new Error('Mật khẩu tối thiểu 8 ký tự');
      }

      if (mode === 'login') {
        if (loginStyle === 'email') {
          const mail = email.trim().toLowerCase();
          if (!mail.includes('@')) throw new Error('Nhập email của bạn');
          const result = await loginFamilyByEmail({ email: mail, password });
          await handleLoginResult(result);
          return;
        }
        const result = await loginFamilyParent({ tenantCode, username, password });
        await handleLoginResult(result);
        return;
      }

      const displayName = parentDisplayName.trim();
      if (!displayName) throw new Error('Nhập họ và tên bố/mẹ');
      const mail = email.trim().toLowerCase();
      if (!mail.includes('@')) throw new Error('Nhập email hợp lệ');
      const derivedUser = username.trim() || usernameFromEmail(mail);
      const houseCode = inviteCode.trim().toUpperCase();

      if (houseCode) {
        const joined = await acceptFamilyInvite({
          code: houseCode,
          parentDisplayName: displayName,
          username: derivedUser,
          email: mail,
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
          emailHint: mail,
        });
        return;
      }

      const created = await registerFamily({
        familyName: `Nhà của ${displayName}`,
        parentDisplayName: displayName,
        username: derivedUser,
        email: mail,
        password,
        parentPin,
      });
      await finish({
        accessToken: created.accessToken,
        refreshToken: created.refreshToken,
        tenantCode: created.tenantCode,
        parentPin,
        familyId: created.familyId,
        familyName: created.familyName,
        emailHint: mail,
      });
    } catch (err) {
      setError(apiError(err, 'Không mở được FamilyOS'));
    } finally {
      setLoading(false);
    }
  };

  const registerCta =
    inviteCode.trim().length > 0 ? 'Gia nhập nhà & tiếp tục →' : 'Tạo nhà & tiếp tục →';

  return (
    <div className="fx-auth">
      <div className="fx-auth-atmosphere" aria-hidden>
        <span className="fx-auth-leaf is-a" />
        <span className="fx-auth-leaf is-b" />
        <span className="fx-auth-leaf is-c" />
      </div>

      <header className="fx-auth-hero">
        <div className="fx-auth-brand-lockup">
          <img
            className="fx-auth-mascot"
            src="/brand/fami-mascot-mark.png"
            alt=""
            width={64}
            height={64}
            decoding="async"
          />
          <span className="fx-auth-wordmark">Famixa</span>
        </div>
        <p className="fx-auth-promise">Mỗi ngày nhà mình gần nhau hơn một chút</p>
        <p className="fx-auth-tagline">Một nhà · Một kế hoạch · Một ngày</p>
      </header>

      <section className="fx-auth-card">
        {error ? <div className="banner-error">{error}</div> : null}

        {choiceToken && workspaces.length > 0 ? (
          <div className="fx-auth-form">
            <div className="fx-auth-card-head">
              <div>
                <h1>Chọn nhà</h1>
                <p>Email này thuộc nhiều workspace — chọn nhà Famixa để tiếp tục.</p>
              </div>
            </div>
            <ul className="fx-workspace-list">
              {workspaces.map((w) => (
                <li key={w.userId}>
                  <button
                    type="button"
                    className={selectedUserId === w.userId ? 'is-on' : undefined}
                    onClick={() => setSelectedUserId(w.userId)}
                  >
                    <strong>{w.tenantName || w.tenantCode}</strong>
                    <em>
                      {w.tenantCode}
                      {w.productCode ? ` · ${productLabel(w.productCode)}` : ''}
                    </em>
                  </button>
                </li>
              ))}
            </ul>
            <button
              type="button"
              className="fx-auth-submit"
              disabled={loading || !selectedUserId}
              onClick={() => void onSelectWorkspace()}
            >
              {loading ? 'Đang vào…' : 'Tiếp tục →'}
            </button>
            <button type="button" className="fx-auth-text-btn" onClick={() => switchMode(mode)}>
              Quay lại
            </button>
          </div>
        ) : mode === 'register' ? (
          <form className="fx-auth-form" onSubmit={onSubmit}>
            <div className="fx-auth-card-head">
              <div>
                <h1>Tạo nhà Famixa</h1>
                <p>Ba bước ngắn — vào dùng ngay.</p>
              </div>
            </div>

            <label className="fx-field">
              <span>
                Họ và tên bố/mẹ <em>*</em>
              </span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconUser />
                </span>
                <input
                  value={parentDisplayName}
                  onChange={(e) => setParentDisplayName(e.target.value)}
                  autoComplete="name"
                  placeholder="Nhập họ và tên"
                  required
                />
              </div>
            </label>

            <label className="fx-field">
              <span>
                Email <em>*</em>
              </span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconMail />
                </span>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  autoComplete="email"
                  placeholder="you@email.com"
                  required
                />
              </div>
            </label>

            <label className="fx-field">
              <span>
                Mật khẩu <em>*</em>
              </span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconLock />
                </span>
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="new-password"
                  placeholder="Tối thiểu 8 ký tự"
                  minLength={8}
                  required
                />
                <button
                  type="button"
                  className="fx-input-action"
                  aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  onClick={() => setShowPassword((v) => !v)}
                >
                  <IconEye off={showPassword} />
                </button>
              </div>
            </label>

            <div className="fx-auth-optional">
              <button
                type="button"
                className={`fx-auth-optional-toggle${showInvite ? ' is-open' : ''}`}
                aria-expanded={showInvite}
                onClick={() => setShowInvite((v) => !v)}
              >
                <span>Có mã mời từ nhà sẵn có?</span>
                <em>{showInvite ? 'Thu gọn' : 'Nhập mã'}</em>
              </button>
              {showInvite ? (
                <label className="fx-field">
                  <span>Mã nhà / mã mời</span>
                  <div className="fx-input">
                    <span className="fx-input-ico">
                      <IconHome />
                    </span>
                    <input
                      value={inviteCode}
                      onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                      autoComplete="off"
                      placeholder="Để trống nếu tạo nhà mới"
                      spellCheck={false}
                    />
                  </div>
                  <small className="fx-field-hint">
                    Có mã → vào nhà hiện có. Để trống → tạo nhà mới.
                  </small>
                </label>
              ) : null}
            </div>

            <button className="fx-auth-submit" type="submit" disabled={loading}>
              {loading ? 'Đang xử lý…' : registerCta}
            </button>

            <p className="fx-auth-legal">
              Bằng việc tiếp tục, bạn đồng ý với{' '}
              <a href="https://famixa.vn" target="_blank" rel="noreferrer">
                Điều khoản
              </a>{' '}
              và{' '}
              <a href="https://famixa.vn" target="_blank" rel="noreferrer">
                Bảo mật
              </a>
              .
            </p>

            <div className="fx-auth-footer is-single">
              <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('login')}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconUser />
                </span>
                <span>
                  Đã có tài khoản? <strong>Đăng nhập →</strong>
                </span>
              </button>
            </div>
          </form>
        ) : (
          <form className="fx-auth-form" onSubmit={onSubmit}>
            <div className="fx-auth-card-head">
              <div>
                <h1>Đăng nhập</h1>
                <p>
                  {loginStyle === 'tenant'
                    ? 'Mã gia đình · tài khoản · mật khẩu.'
                    : 'Chào bố mẹ — vào nhà chỉ với email.'}
                </p>
              </div>
            </div>

            {loginStyle === 'email' ? (
              <label className="fx-field">
                <span>
                  Email <em>*</em>
                </span>
                <div className="fx-input">
                  <span className="fx-input-ico">
                    <IconMail />
                  </span>
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    autoComplete="email"
                    placeholder="ban@email.com"
                    required
                  />
                </div>
              </label>
            ) : null}

            {loginStyle === 'tenant' ? (
              <>
                <label className="fx-field">
                  <span>
                    Mã gia đình <em>*</em>
                  </span>
                  <div className="fx-input">
                    <span className="fx-input-ico">
                      <IconHome />
                    </span>
                    <input
                      value={tenantCode}
                      onChange={(e) => setTenantCode(e.target.value.toUpperCase())}
                      autoComplete="organization"
                      placeholder="VD: DEMO_FAMILY"
                      required
                    />
                  </div>
                </label>
                <label className="fx-field">
                  <span>
                    Tài khoản phụ huynh <em>*</em>
                  </span>
                  <div className="fx-input">
                    <span className="fx-input-ico">
                      <IconUser />
                    </span>
                    <input
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      autoComplete="username"
                      placeholder="VD: admin"
                      required
                    />
                  </div>
                </label>
              </>
            ) : null}

            <label className="fx-field">
              <span>
                Mật khẩu <em>*</em>
              </span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconLock />
                </span>
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                  required
                  placeholder="Mật khẩu"
                />
                <button
                  type="button"
                  className="fx-input-action"
                  aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  onClick={() => setShowPassword((v) => !v)}
                >
                  <IconEye off={showPassword} />
                </button>
              </div>
            </label>

            <div className="fx-auth-optional">
              <button
                type="button"
                className={`fx-auth-optional-toggle${showAdvancedLogin ? ' is-open' : ''}`}
                aria-expanded={showAdvancedLogin}
                onClick={() => {
                  setShowAdvancedLogin((v) => {
                    const next = !v;
                    setLoginStyle(next ? 'tenant' : 'email');
                    return next;
                  });
                }}
              >
                <span>
                  {showAdvancedLogin ? 'Đăng nhập bằng email' : 'Cách khác (mã gia đình)'}
                </span>
                <em>{showAdvancedLogin ? 'Dùng email' : 'Mở'}</em>
              </button>
            </div>

            <button className="fx-auth-submit" type="submit" disabled={loading}>
              {loading ? 'Đang xử lý…' : 'Vào nhà →'}
            </button>

            <div className="fx-auth-footer">
              <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('register')}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconHome />
                </span>
                <span>
                  Chưa có nhà? <strong>Tạo nhà →</strong>
                </span>
              </button>
              <button type="button" className="fx-auth-footer-link" onClick={openInviteOnRegister}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconTag />
                </span>
                <span>
                  Có mã mời? <strong>Gia nhập →</strong>
                </span>
              </button>
              <button type="button" className="fx-auth-footer-link" onClick={() => navigate('/demo')}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconEye />
                </span>
                <span>
                  Xem nhà demo <strong>(chỉ xem) →</strong>
                </span>
              </button>
            </div>
          </form>
        )}
      </section>
    </div>
  );
}
