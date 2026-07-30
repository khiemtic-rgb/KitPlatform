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

type Mode = 'login' | 'register' | 'join';
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

function FamixaMark() {
  return (
    <svg className="fx-logo-mark" viewBox="0 0 40 40" aria-hidden>
      <circle cx="20" cy="20" r="20" fill="#1a5c45" />
      <path
        d="M20 8.5c-1.2 3.2-5.2 5.4-8.2 5.8 1.8 1.2 3.2 3.2 3.6 5.6C18.2 17.2 20 14.8 20 14.8s1.8 2.4 4.6 4.6c.4-2.4 1.8-4.4 3.6-5.6-3-.4-7-2.6-8.2-5.8z"
        fill="#7dcf8a"
      />
      <path
        d="M13.5 22.5h13v8.2c0 1.2-.9 2.1-2.1 2.1h-8.8c-1.2 0-2.1-.9-2.1-2.1v-8.2z"
        fill="#fff"
      />
      <path d="M11.8 22.8 L20 15.2l8.2 7.6" stroke="#fff" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" fill="none" />
      <rect x="18.2" y="26.2" width="3.6" height="4.4" rx="0.6" fill="#1a5c45" />
    </svg>
  );
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

function IconPhone() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <path
        d="M8.2 4.8h2.4l1.2 3.2-1.6 1a10.5 10.5 0 0 0 4.8 4.8l1-1.6 3.2 1.2v2.4c0 .9-.7 1.7-1.6 1.8-7.2.6-13.2-5.4-12.6-12.6.1-.9.9-1.6 1.8-1.6z"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
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

function IconInfo() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden>
      <circle cx="12" cy="12" r="8.5" fill="none" stroke="currentColor" strokeWidth="1.8" />
      <path d="M12 10.5v5M12 7.8h.01" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function ParentUnlockPage() {
  const navigate = useNavigate();
  const setParentSession = useSessionStore((s) => s.setParentSession);
  const setFamily = useSessionStore((s) => s.setFamily);
  const [mode, setMode] = useState<Mode>('register');
  const [loginStyle, setLoginStyle] = useState<LoginStyle>('email');
  const [tenantCode, setTenantCode] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [parentPin, setParentPin] = useState('1234');
  const [parentDisplayName, setParentDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [phone, setPhone] = useState('');
  const [referralCode, setReferralCode] = useState('');
  const [acceptedTerms, setAcceptedTerms] = useState(true);
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
  };

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
      if (mode === 'register' && !acceptedTerms) {
        throw new Error('Cần đồng ý Điều khoản sử dụng và Chính sách bảo mật.');
      }
      if (!/^\d{4}$/.test(parentPin)) {
        throw new Error('Mã bố mẹ phải gồm 4 chữ số');
      }
      if ((mode === 'register' || mode === 'join') && password.trim().length < 8) {
        throw new Error('Mật khẩu tối thiểu 8 ký tự');
      }

      if (mode === 'login') {
        if (loginStyle === 'email') {
          const mail = email.trim().toLowerCase();
          if (!mail.includes('@')) throw new Error('Nhập email KitPlatform');
          const result = await loginFamilyByEmail({ email: mail, password });
          await handleLoginResult(result);
          return;
        }
        const result = await loginFamilyParent({ tenantCode, username, password });
        await handleLoginResult(result);
        return;
      }

      const displayName = parentDisplayName.trim();
      const mail = email.trim().toLowerCase();
      const derivedUser = username.trim() || usernameFromEmail(mail);
      const houseCode = inviteCode.trim().toUpperCase();

      // Register form with mã nhà → join (giữ & nâng cấp cùng nhà)
      if (mode === 'register' && houseCode) {
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
        });
        return;
      }

      if (mode === 'register') {
        const created = await registerFamily({
          familyName: `Nhà của ${displayName}`,
          parentDisplayName: displayName,
          username: derivedUser,
          email: mail,
          password,
          parentPin,
        });
        // phone + referralCode: UI sẵn; API gắn sau khi nâng cấp
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
      <header className="fx-auth-brand">
        <div className="fx-auth-brand-row">
          <div className="fx-auth-brand-lockup">
            <FamixaMark />
            <div>
              <div className="fx-auth-wordmark">Famixa</div>
              <p className="fx-auth-tagline">Một nhà · Một kế hoạch · Một ngày</p>
            </div>
          </div>
          <img
            className="fx-auth-mascot"
            src="/unlock/fami-mascot.png"
            alt=""
            width={88}
            height={88}
            decoding="async"
          />
        </div>
      </header>

      <section className="fx-auth-card">
        {error ? <div className="banner-error">{error}</div> : null}

        {choiceToken ? (
          <div className="stack">
            <p className="muted" style={{ margin: 0 }}>
              Tài khoản Kit có nhiều workspace. Chọn nhà Famixa (hoặc đơn vị) để vào.
            </p>
            <div className="stack" style={{ gap: 8 }}>
              {workspaces.map((w) => (
                <label
                  key={w.userId}
                  className="field"
                  style={{
                    flexDirection: 'row',
                    alignItems: 'center',
                    gap: 10,
                    border:
                      selectedUserId === w.userId
                        ? '1px solid var(--brand, #1d6a6a)'
                        : '1px solid transparent',
                    borderRadius: 8,
                    padding: 8,
                    cursor: 'pointer',
                  }}
                >
                  <input
                    type="radio"
                    name="workspace"
                    checked={selectedUserId === w.userId}
                    onChange={() => setSelectedUserId(w.userId)}
                  />
                  <span>
                    <strong>{w.tenantName}</strong>
                    <span className="muted">
                      {' '}
                      · {w.tenantCode} · {productLabel(w.productCode)}
                    </span>
                  </span>
                </label>
              ))}
            </div>
            <button className="btn btn-primary" type="button" disabled={loading} onClick={onSelectWorkspace}>
              {loading ? 'Đang vào…' : 'Vào workspace đã chọn'}
            </button>
            <button className="btn" type="button" onClick={() => { setChoiceToken(null); setWorkspaces([]); }}>
              Quay lại
            </button>
          </div>
        ) : mode === 'register' ? (
          <form className="fx-auth-form" onSubmit={onSubmit}>
            <div className="fx-auth-card-head">
              <span className="fx-auth-head-icon" aria-hidden>
                <IconUser />
              </span>
              <div>
                <h1>Tạo tài khoản</h1>
                <p>Bắt đầu hành trình trưởng thành cùng gia đình bạn.</p>
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

            <label className="fx-field">
              <span>Mã nhà (nếu đã có)</span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconHome />
                </span>
                <input
                  value={inviteCode}
                  onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                  autoComplete="off"
                  placeholder="Nhập mã nhà nếu bạn đã nhận từ gia đình"
                  spellCheck={false}
                />
                <span
                  className="fx-input-action is-static"
                  title="Có mã nhà → gia nhập nhà hiện có. Để trống → tạo nhà mới."
                >
                  <IconInfo />
                </span>
              </div>
            </label>

            <div className="fx-auth-divider" role="separator">
              <span>Tùy chọn (không bắt buộc)</span>
            </div>

            <label className="fx-field">
              <span>Số điện thoại</span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconPhone />
                </span>
                <input
                  type="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  autoComplete="tel"
                  placeholder="Để Famixa hỗ trợ khi cần"
                />
              </div>
            </label>

            <label className="fx-field">
              <span>Mã giới thiệu</span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconTag />
                </span>
                <input
                  value={referralCode}
                  onChange={(e) => setReferralCode(e.target.value)}
                  autoComplete="off"
                  placeholder="Nếu có"
                  spellCheck={false}
                />
              </div>
              <small className="fx-field-hint">Mã từ bạn bè / đối tác (không phải mã nhà)</small>
            </label>

            <button className="fx-auth-submit" type="submit" disabled={loading}>
              {loading ? 'Đang xử lý…' : registerCta}
            </button>

            <label className="fx-terms">
              <input
                type="checkbox"
                checked={acceptedTerms}
                onChange={(e) => setAcceptedTerms(e.target.checked)}
              />
              <span>
                Bằng việc tạo tài khoản, bạn đồng ý với{' '}
                <a href="https://famixa.vn" target="_blank" rel="noreferrer">
                  Điều khoản sử dụng
                </a>{' '}
                và{' '}
                <a href="https://famixa.vn" target="_blank" rel="noreferrer">
                  Chính sách bảo mật
                </a>
              </span>
            </label>

            <div className="fx-auth-footer">
              <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('login')}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconUser />
                </span>
                <span>
                  Đã có tài khoản? <strong>Đăng nhập →</strong>
                </span>
              </button>
              <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('join')}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconHome />
                </span>
                <span>
                  Đã có mã nhà? <strong>Gia nhập nhà →</strong>
                </span>
              </button>
            </div>
          </form>
        ) : (
          <form className="fx-auth-form fx-auth-form--alt" onSubmit={onSubmit}>
            <div className="fx-auth-card-head">
              <span className="fx-auth-head-icon" aria-hidden>
                {mode === 'login' ? <IconUser /> : <IconHome />}
              </span>
              <div>
                <h1>{mode === 'login' ? 'Đăng nhập' : 'Gia nhập nhà'}</h1>
                <p>
                  {mode === 'login'
                    ? 'Một email Kit dùng chung Pharmacy / Famixa — nếu có nhiều nơi sẽ hỏi chọn workspace.'
                    : 'Nhập mã nhà và tạo tài khoản phụ huynh để vào cùng nhà.'}
                </p>
              </div>
            </div>

            {mode === 'login' ? (
              <>
                <div className="fx-auth-mode-tabs">
                  <button
                    type="button"
                    className={loginStyle === 'email' ? 'is-on' : undefined}
                    onClick={() => setLoginStyle('email')}
                  >
                    Email Kit
                  </button>
                  <button
                    type="button"
                    className={loginStyle === 'tenant' ? 'is-on' : undefined}
                    onClick={() => setLoginStyle('tenant')}
                  >
                    Mã gia đình
                  </button>
                </div>
                {loginStyle === 'email' ? (
                  <label className="fx-field">
                    <span>Email KitPlatform</span>
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
                ) : (
                  <>
                    <label className="fx-field">
                      <span>Mã gia đình</span>
                      <div className="fx-input">
                        <span className="fx-input-ico">
                          <IconHome />
                        </span>
                        <input
                          value={tenantCode}
                          onChange={(e) => setTenantCode(e.target.value.toUpperCase())}
                          autoComplete="organization"
                          placeholder="VD: FOS_NHAXYZ123"
                          required
                        />
                      </div>
                    </label>
                    <label className="fx-field">
                      <span>Tài khoản phụ huynh</span>
                      <div className="fx-input">
                        <span className="fx-input-ico">
                          <IconUser />
                        </span>
                        <input
                          value={username}
                          onChange={(e) => setUsername(e.target.value)}
                          autoComplete="username"
                          required
                        />
                      </div>
                    </label>
                  </>
                )}
              </>
            ) : (
              <>
                <label className="fx-field">
                  <span>
                    Mã nhà <em>*</em>
                  </span>
                  <div className="fx-input">
                    <span className="fx-input-ico">
                      <IconHome />
                    </span>
                    <input
                      value={inviteCode}
                      onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                      required
                      placeholder="Nhập mã nhà"
                      spellCheck={false}
                    />
                  </div>
                </label>
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
                      required
                      placeholder="Nhập họ và tên"
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
                      required
                      placeholder="you@email.com"
                    />
                  </div>
                </label>
              </>
            )}

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
                  autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                  required
                  placeholder={mode === 'login' ? 'Mật khẩu' : 'Tối thiểu 8 ký tự'}
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

            <label className="fx-field">
              <span>Mã bố mẹ (4 số)</span>
              <div className="fx-input">
                <span className="fx-input-ico">
                  <IconLock />
                </span>
                <input
                  inputMode="numeric"
                  pattern="\d{4}"
                  maxLength={4}
                  value={parentPin}
                  onChange={(e) => setParentPin(e.target.value.replace(/\D/g, '').slice(0, 4))}
                  autoComplete="off"
                  required
                />
              </div>
            </label>

            <button className="fx-auth-submit" type="submit" disabled={loading}>
              {loading
                ? 'Đang xử lý…'
                : mode === 'login'
                  ? 'Mở cho cả nhà →'
                  : 'Gia nhập nhà →'}
            </button>

            <div className="fx-auth-footer">
              <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('register')}>
                <span className="fx-auth-footer-ico" aria-hidden>
                  <IconHome />
                </span>
                <span>
                  Chưa có nhà? <strong>Tạo tài khoản →</strong>
                </span>
              </button>
              {mode === 'login' ? (
                <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('join')}>
                  <span className="fx-auth-footer-ico" aria-hidden>
                    <IconHome />
                  </span>
                  <span>
                    Đã có mã nhà? <strong>Gia nhập nhà →</strong>
                  </span>
                </button>
              ) : (
                <button type="button" className="fx-auth-footer-link" onClick={() => switchMode('login')}>
                  <span className="fx-auth-footer-ico" aria-hidden>
                    <IconUser />
                  </span>
                  <span>
                    Đã có tài khoản? <strong>Đăng nhập →</strong>
                  </span>
                </button>
              )}
            </div>
          </form>
        )}
      </section>
    </div>
  );
}
