import { useEffect, useMemo, useState, type FormEvent, type MouseEvent } from 'react';
import {
  CheckOutlined,
  EnvironmentOutlined,
  PhoneOutlined,
  SafetyOutlined,
  SendOutlined,
  ShopOutlined,
} from '@ant-design/icons';
import { message } from 'antd';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { getApiErrorMessage, requestOtp, verifyOtp } from '@/shared/api/customer-app.api';
import {
  APP_BRAND,
  DEFAULT_TENANT_CODE,
  isTenantCodeLocked,
  loadStoredTenantCode,
  saveStoredTenantCode,
} from '@/shared/config/app-brand';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { applyTenantFromUrl } from '@/shared/config/tenant-link';
import { useAuthStore } from '@/shared/auth/auth.store';
import './OtpLoginPage.css';

function formatCountdown(totalSeconds: number) {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

function formatPhoneDisplay(raw: string) {
  const digits = raw.replace(/\D/g, '').slice(0, 11);
  if (digits.length <= 4) return digits;
  if (digits.length <= 7) return `${digits.slice(0, 4)} ${digits.slice(4)}`;
  return `${digits.slice(0, 4)} ${digits.slice(4, 7)} ${digits.slice(7)}`;
}

function digitsOnly(value: string) {
  return value.replace(/\D/g, '');
}

/** Normalize VN mobile for zalo.me / tel links. */
function normalizePartnerPhone(raw: string) {
  let digits = raw.replace(/\D/g, '');
  if (digits.startsWith('84') && digits.length >= 11) {
    digits = `0${digits.slice(2)}`;
  }
  return digits;
}

const LEGAL_TERMS_URL = 'https://novixa.vn/vi/dieu-khoan-su-dung/';
const LEGAL_PRIVACY_URL = 'https://novixa.vn/vi/chinh-sach-bao-mat/';

function openLegalLink(event: MouseEvent<HTMLAnchorElement>) {
  // Keep checkbox state when user taps legal links inside the consent label.
  event.stopPropagation();
}

function ShieldLogo() {
  return (
    <svg viewBox="0 0 32 32" aria-hidden>
      <path
        fill="currentColor"
        d="M16 2.5c4.2 2.2 7.4 3 11 3.2v10.3c0 6.2-4.1 11.4-11 13.5-6.9-2.1-11-7.3-11-13.5V5.7c3.6-.2 6.8-1 11-3.2z"
        opacity="0.22"
      />
      <path
        fill="currentColor"
        d="M16 4.2c3.6 1.8 6.4 2.5 9.5 2.7v9.1c0 5.1-3.3 9.4-9.5 11.3-6.2-1.9-9.5-6.2-9.5-11.3V6.9c3.1-.2 5.9-.9 9.5-2.7z"
      />
      <path
        fill="#fff"
        d="M14.7 10.2h2.6v4.1h4.1v2.6h-4.1v4.1h-2.6v-4.1h-4.1v-2.6h4.1z"
      />
    </svg>
  );
}

function DotGrid() {
  return (
    <div className="otp-login-dots" aria-hidden>
      {Array.from({ length: 16 }, (_, i) => (
        <span key={i} />
      ))}
    </div>
  );
}

export function OtpLoginPage() {
  const { t } = useTranslation();
  const { branding, refresh } = useCustomerBranding();
  const [searchParams] = useSearchParams();
  const search = searchParams.toString();
  const initialTenant = useMemo(() => applyTenantFromUrl(search ? `?${search}` : ''), [search]);
  const [step, setStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [phone, setPhone] = useState(import.meta.env.DEV ? '0909123456' : '');
  const [tenantCode, setTenantCode] = useState(initialTenant.code || DEFAULT_TENANT_CODE);
  const [channel, setChannel] = useState<'counter' | 'remote'>('counter');
  const [counterPin, setCounterPin] = useState('');
  const [inviteCode, setInviteCode] = useState('');
  const [pilotCode, setPilotCode] = useState<string | null>(null);
  const [expiresInSeconds, setExpiresInSeconds] = useState(0);
  const [otpCode, setOtpCode] = useState('');
  const [agreed, setAgreed] = useState(true);
  const [pendingApproval, setPendingApproval] = useState(false);
  const tenantLocked = initialTenant.locked || isTenantCodeLocked();
  const setSession = useAuthStore((s) => s.setSession);
  const navigate = useNavigate();

  const pharmacyName = branding.tenantName?.trim() || tenantCode || t('branding.defaultTenantName');
  const locationLabel = t('auth.locationFallback').trim();

  useEffect(() => {
    if (initialTenant.locked) {
      setTenantCode(initialTenant.code);
    }
  }, [initialTenant.code, initialTenant.locked]);

  useEffect(() => {
    const code = (tenantLocked ? DEFAULT_TENANT_CODE || tenantCode : tenantCode).trim().toUpperCase();
    if (!code) return;
    saveStoredTenantCode(code);
    void refresh();
  }, [refresh, tenantCode, tenantLocked]);

  useEffect(() => {
    if (step !== 1) return;

    const timer = window.setInterval(() => {
      setExpiresInSeconds((current) => (current > 0 ? current - 1 : 0));
    }, 1000);

    return () => window.clearInterval(timer);
  }, [step]);

  const resolvedTenant = () =>
    (tenantLocked ? DEFAULT_TENANT_CODE || tenantCode : tenantCode).trim().toUpperCase();

  const resetOtpStep = () => {
    setStep(0);
    setPilotCode(null);
    setExpiresInSeconds(0);
    setOtpCode('');
    setPendingApproval(false);
  };

  const onRequestOtp = async (event: FormEvent) => {
    event.preventDefault();
    const normalized = digitsOnly(phone);
    if (normalized.length < 9) {
      message.warning(t('auth.invalidPhone'));
      return;
    }
    if (!agreed) {
      message.warning(t('auth.consentRequired'));
      return;
    }
    if (channel === 'counter' && !counterPin.trim()) {
      message.warning(t('auth.counterPinRequired'));
      return;
    }
    setLoading(true);
    try {
      const code = resolvedTenant();
      if (!code) {
        message.warning(t('auth.tenantRequired'));
        return;
      }
      saveStoredTenantCode(code);
      const res = await requestOtp(normalized, code, {
        channel,
        counterPin: channel === 'counter' ? counterPin.trim() : undefined,
        inviteCode: channel === 'remote' ? inviteCode.trim() || undefined : undefined,
      });
      message.success(res.message || t('auth.otpSent'));
      const status = (res.status || 'otp_sent').toLowerCase();
      if (status === 'pending_approval') {
        setPendingApproval(true);
        setPilotCode(null);
        setOtpCode('');
        setExpiresInSeconds(0);
        setStep(1);
        return;
      }
      setPendingApproval(false);
      setPilotCode(res.pilotCode?.trim() || null);
      setExpiresInSeconds(res.expiresInSeconds);
      setOtpCode(res.pilotCode?.trim() || '');
      setStep(1);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('auth.otpSendFailed')));
    } finally {
      setLoading(false);
    }
  };

  const onVerifyOtp = async (event: FormEvent) => {
    event.preventDefault();
    const code = otpCode.trim();
    if (!code) {
      message.warning(t('auth.otpRequired'));
      return;
    }
    setLoading(true);
    try {
      const data = await verifyOtp(digitsOnly(phone), code, resolvedTenant());
      setSession(data);
      message.success(t('auth.welcome', { name: data.profile.fullName }));
      navigate('/', { replace: true });
    } catch {
      message.error(t('auth.otpInvalid'));
    } finally {
      setLoading(false);
    }
  };

  const contactPharmacy = () => {
    const phoneNumber = branding.supportPhone
      ? normalizePartnerPhone(branding.supportPhone)
      : '';
    if (phoneNumber) {
      window.open(`https://zalo.me/${phoneNumber}`, '_blank', 'noopener,noreferrer');
      return;
    }
    message.info(t('auth.contactPharmacyHint'));
  };

  const continueAsGuest = () => {
    navigate('/', { replace: true });
  };

  return (
    <div className="otp-login">
      <div className="otp-login-card">
        <DotGrid />

        <header className="otp-login-brand">
          <div className="otp-login-logo">
            <ShieldLogo />
          </div>
          <h1 className="otp-login-brand-name">{APP_BRAND}</h1>
          <p className="otp-login-brand-tag">{t('auth.subtitle')}</p>
          <div className="otp-login-brand-rule" />
        </header>

        <nav className="otp-login-steps" aria-label={t('auth.stepsLabel')}>
          <div className={`otp-login-step${step === 0 ? ' is-active' : ''}${step > 0 ? ' is-done' : ''}`}>
            <span className="otp-login-step-num">1</span>
            <span className="otp-login-step-label">{t('auth.stepPhone')}</span>
          </div>
          <div className={`otp-login-step-line${step > 0 ? ' is-active' : ''}`} />
          <div className={`otp-login-step${step === 1 ? ' is-active' : ''}`}>
            <span className="otp-login-step-num">2</span>
            <span className="otp-login-step-label">{t('auth.stepOtp')}</span>
          </div>
        </nav>

        {(tenantLocked || branding.tenantName) && (
          <div className="otp-login-connect">
            <div className="otp-login-connect-left">
              <span className="otp-login-connect-icon" aria-hidden>
                <ShopOutlined />
              </span>
              <div className="otp-login-connect-copy">
                <p className="otp-login-connect-kicker">{t('auth.connectingWith')}</p>
                <p className="otp-login-connect-name">{pharmacyName}</p>
              </div>
            </div>
            {locationLabel ? (
              <span className="otp-login-connect-loc">
                <EnvironmentOutlined />
                {locationLabel}
              </span>
            ) : null}
          </div>
        )}

        {step === 0 ? (
          <form onSubmit={(e) => void onRequestOtp(e)}>
            <div className="otp-login-field">
              <label className="otp-login-label" htmlFor="otp-phone">
                {t('auth.phoneLabel')}
                <em>*</em>
              </label>
              <div className="otp-login-input">
                <PhoneOutlined className="otp-login-input-icon" />
                <input
                  id="otp-phone"
                  value={formatPhoneDisplay(phone)}
                  onChange={(e) => setPhone(digitsOnly(e.target.value))}
                  placeholder="0909 123 456"
                  type="tel"
                  inputMode="tel"
                  autoComplete="tel"
                />
              </div>
            </div>

            {!tenantLocked ? (
              <div className="otp-login-field">
                <label className="otp-login-label" htmlFor="otp-tenant">
                  {t('auth.tenantLabel')}
                  <em>*</em>
                </label>
                <div className="otp-login-input">
                  <ShopOutlined className="otp-login-input-icon" />
                  <input
                    id="otp-tenant"
                    value={tenantCode}
                    onChange={(e) => setTenantCode(e.target.value.toUpperCase())}
                    placeholder={DEFAULT_TENANT_CODE || loadStoredTenantCode() || 'NT_A'}
                    autoCapitalize="characters"
                    style={{ textTransform: 'uppercase' }}
                  />
                </div>
              </div>
            ) : null}

            <div className="otp-login-field">
              <span className="otp-login-label">{t('auth.channelLabel')}</span>
              <div className="otp-login-channel" role="group" aria-label={t('auth.channelLabel')}>
                <button
                  type="button"
                  className={`otp-login-channel-btn${channel === 'counter' ? ' is-active' : ''}`}
                  onClick={() => setChannel('counter')}
                >
                  {t('auth.channelCounter')}
                </button>
                <button
                  type="button"
                  className={`otp-login-channel-btn${channel === 'remote' ? ' is-active' : ''}`}
                  onClick={() => setChannel('remote')}
                >
                  {t('auth.channelRemote')}
                </button>
              </div>
            </div>

            {channel === 'counter' ? (
              <div className="otp-login-field">
                <label className="otp-login-label" htmlFor="otp-counter-pin">
                  {t('auth.counterPinLabel')}
                  <em>*</em>
                </label>
                <div className="otp-login-input">
                  <SafetyOutlined className="otp-login-input-icon" />
                  <input
                    id="otp-counter-pin"
                    value={counterPin}
                    onChange={(e) => setCounterPin(e.target.value)}
                    placeholder={t('auth.counterPinPlaceholder')}
                    inputMode="numeric"
                    autoComplete="one-time-code"
                  />
                </div>
              </div>
            ) : (
              <div className="otp-login-field">
                <label className="otp-login-label" htmlFor="otp-invite">
                  {t('auth.inviteCodeLabel')}
                </label>
                <div className="otp-login-input">
                  <ShopOutlined className="otp-login-input-icon" />
                  <input
                    id="otp-invite"
                    value={inviteCode}
                    onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                    placeholder={t('auth.inviteCodePlaceholder')}
                    autoCapitalize="characters"
                    style={{ textTransform: 'uppercase' }}
                  />
                </div>
              </div>
            )}

            <label className={`otp-login-consent${agreed ? ' is-checked' : ''}`}>
              <input
                type="checkbox"
                checked={agreed}
                onChange={(e) => setAgreed(e.target.checked)}
              />
              <span className="otp-login-check" aria-hidden>
                {agreed ? <CheckOutlined /> : null}
              </span>
              <span className="otp-login-consent-text">
                {t('auth.consentBefore')}{' '}
                <a
                  href={LEGAL_TERMS_URL}
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={openLegalLink}
                >
                  {t('auth.termsOfUse')}
                </a>{' '}
                {t('auth.consentAnd')}{' '}
                <a
                  href={LEGAL_PRIVACY_URL}
                  target="_blank"
                  rel="noopener noreferrer"
                  onClick={openLegalLink}
                >
                  {t('auth.privacyPolicy')}
                </a>
              </span>
            </label>

            <button type="submit" className="otp-login-submit" disabled={loading || !agreed}>
              <SendOutlined />
              {t('auth.sendOtp')}
            </button>
          </form>
        ) : pendingApproval ? (
          <div className="otp-login-pending">
            <p className="otp-login-pilot-label">{t('auth.pendingTitle')}</p>
            <p className="otp-login-otp-note">{t('auth.pendingBody')}</p>
            <button
              type="button"
              className="otp-login-submit"
              onClick={() => {
                setPendingApproval(false);
                setOtpCode('');
              }}
            >
              <SafetyOutlined />
              {t('auth.pendingContinue')}
            </button>
            <button type="button" className="otp-login-back" onClick={resetOtpStep}>
              {t('auth.changePhone')}
            </button>
          </div>
        ) : (
          <form onSubmit={(e) => void onVerifyOtp(e)}>
            <p className="otp-login-otp-note">
              {t('auth.otpSentTo')} <strong>{formatPhoneDisplay(phone)}</strong>.
              {import.meta.env.DEV && !pilotCode ? (
                <>
                  {' '}
                  {t('auth.devOtpHint')} <code>000000</code>
                </>
              ) : null}
            </p>

            {pilotCode ? (
              <div className="otp-login-pilot">
                <p className="otp-login-pilot-label">{t('auth.pilotCodeTitle')}</p>
                <p className="otp-login-pilot-code">{pilotCode}</p>
                <p className="otp-login-pilot-meta">
                  {expiresInSeconds > 0
                    ? t('auth.pilotCodeExpires', { seconds: formatCountdown(expiresInSeconds) })
                    : t('auth.pilotCodeExpired')}
                </p>
                <p className="otp-login-pilot-meta">{t('auth.pilotCodeHint')}</p>
              </div>
            ) : null}

            <div className="otp-login-field">
              <label className="otp-login-label" htmlFor="otp-code">
                {t('auth.otpLabel')}
                <em>*</em>
              </label>
              <div className="otp-login-input">
                <SafetyOutlined className="otp-login-input-icon" />
                <input
                  id="otp-code"
                  value={otpCode}
                  onChange={(e) => setOtpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  placeholder="000000"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                />
              </div>
            </div>

            <button type="submit" className="otp-login-submit" disabled={loading}>
              <SafetyOutlined />
              {t('auth.confirm')}
            </button>
            <button type="button" className="otp-login-back" onClick={resetOtpStep}>
              {t('auth.changePhone')}
            </button>
          </form>
        )}

        {step === 0 ? (
          <div className="otp-login-footers">
            <p className="otp-login-footer">
              {t('auth.noAccount')}{' '}
              <button type="button" onClick={contactPharmacy}>
                {t('auth.contactPharmacy')}
              </button>
            </p>
            <p className="otp-login-footer otp-login-footer--guest">
              <button type="button" onClick={continueAsGuest}>
                {t('auth.tryWithoutLogin')}
              </button>
            </p>
          </div>
        ) : null}

        {import.meta.env.DEV ? (
          <p className="otp-login-hint">
            Demo: 0909 123 456 · {DEFAULT_TENANT_CODE || tenantCode}
          </p>
        ) : null}
      </div>
    </div>
  );
}
