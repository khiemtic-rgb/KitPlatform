import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Alert, Button, Spin, Switch, Tag, message } from 'antd';
import {
  BellOutlined,
  CameraOutlined,
  DollarOutlined,
  EnvironmentOutlined,
  GiftOutlined,
  HeartOutlined,
  LockOutlined,
  LogoutOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  MobileOutlined,
  RightOutlined,
  RobotOutlined,
  SafetyCertificateOutlined,
  SettingOutlined,
  ShopOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  fetchCareReminderEligible,
  fetchConsents,
  fetchPushStatus,
  getApiErrorMessage,
  logoutApi,
  registerPushSubscription,
  unregisterPushSubscription,
  upsertConsents,
} from '@/shared/api/customer-app.api';
import {
  CUSTOMER_APP_CARE_REMINDER_CONSENTS,
  CUSTOMER_APP_CHAT_CONSENT,
  type CustomerConsent,
  type PushSubscriptionStatus,
} from '@/shared/api/customer-app.types';
import { useAuthStore } from '@/shared/auth/auth.store';
import { clearCustomerCachedData } from '@/shared/api/customer-session-cleanup';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { isPushSupported, requestNotificationPermission, subscribePush, unsubscribePush } from '@/shared/push/push-client';
import { useCustomerNotificationCount } from '@/shared/hooks/useCustomerNotificationCount';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import './ProfilePage.css';

const APP_PUSH_CHANNEL = 4;
const CARE_REMINDER_PURPOSE = 2;
const CHAT_CONSENT_KEY = `${CUSTOMER_APP_CHAT_CONSENT.channel}:${CUSTOMER_APP_CHAT_CONSENT.purpose}`;

type ConsentRow = {
  key: string;
  channel: number;
  purpose: number;
  granted: boolean;
};

type MenuTone = 'rose' | 'amber' | 'violet' | 'sky' | 'teal' | 'indigo' | 'slate' | 'green';

function mergeCareReminderConsents(consents: CustomerConsent[]): ConsentRow[] {
  const byKey = new Map(consents.map((c) => [`${c.channel}:${c.purpose}`, c]));
  return CUSTOMER_APP_CARE_REMINDER_CONSENTS.map(({ channel, purpose }) => {
    const existing = byKey.get(`${channel}:${purpose}`);
    return {
      key: `${channel}:${purpose}`,
      channel,
      purpose,
      granted: existing?.granted ?? false,
    };
  });
}

function consentIcon(channel: number): ReactNode {
  if (channel === 1) return <MessageOutlined />;
  if (channel === 4) return <BellOutlined />;
  if (channel === 5) return <MobileOutlined />;
  return <BellOutlined />;
}

function ConsentToggleRow({
  label,
  description,
  checked,
  saving,
  icon,
  onToggle,
}: {
  label: string;
  description: string;
  checked: boolean;
  saving: boolean;
  icon: ReactNode;
  onToggle: () => void;
}) {
  const { t } = useTranslation();

  return (
    <div
      role="button"
      tabIndex={0}
      aria-pressed={checked}
      aria-busy={saving}
      aria-label={t('profile.toggleAria', {
        label,
        state: checked ? t('common.enabled') : t('common.disabled'),
      })}
      className={`profile-hub-consent-row${checked ? ' profile-hub-consent-row--on' : ''}${
        saving ? ' profile-hub-consent-row--busy' : ''
      }`}
      onClick={() => {
        if (saving) return;
        onToggle();
      }}
      onKeyDown={(event) => {
        if (saving) return;
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onToggle();
        }
      }}
    >
      <span className="profile-hub-consent-icon">{icon}</span>
      <div className="profile-hub-consent-copy">
        <div className="profile-hub-consent-top">
          <span className="profile-hub-consent-label">{label}</span>
          <Tag color={checked ? 'success' : 'default'} style={{ margin: 0 }}>
            {checked ? t('common.enabled') : t('common.disabled')}
          </Tag>
        </div>
        <div className="profile-hub-consent-desc">{description}</div>
      </div>
      <Switch checked={checked} loading={saving} tabIndex={-1} style={{ pointerEvents: 'none' }} />
    </div>
  );
}

export function ProfilePage() {
  const { t } = useTranslation();
  const { branding } = useCustomerBranding();
  const { consentChannel, consentPurpose } = useCustomerLabels();
  const profile = useAuthStore((s) => s.profile);
  const { online } = useApiHealth();
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clearSession = useAuthStore((s) => s.clearSession);
  const navigate = useNavigate();
  const [consentRows, setConsentRows] = useState<ConsentRow[]>(() => mergeCareReminderConsents([]));
  const [consentLoading, setConsentLoading] = useState(true);
  const [consentLoadError, setConsentLoadError] = useState<string | null>(null);
  const [savingConsentKey, setSavingConsentKey] = useState<string | null>(null);
  const [careReminderEligible, setCareReminderEligible] = useState(false);
  const [chatConsentGranted, setChatConsentGranted] = useState(false);
  const [pushStatus, setPushStatus] = useState<PushSubscriptionStatus | null>(null);
  const [pushLoading, setPushLoading] = useState(false);
  const [pushError, setPushError] = useState<string | null>(null);
  const notificationCount = useCustomerNotificationCount();

  const browserPushSupported = isPushSupported();
  const appPushConsentGranted = useMemo(
    () =>
      consentRows.some(
        (row) => row.channel === APP_PUSH_CHANNEL && row.purpose === CARE_REMINDER_PURPOSE && row.granted,
      ),
    [consentRows],
  );

  const loadConsents = useCallback(async () => {
    setConsentLoading(true);
    setConsentLoadError(null);
    try {
      const [consentsResult, eligibleResult, pushResult] = await Promise.allSettled([
        fetchConsents(),
        fetchCareReminderEligible(),
        fetchPushStatus(),
      ]);

      if (consentsResult.status === 'fulfilled') {
        setConsentRows(mergeCareReminderConsents(consentsResult.value));
        setChatConsentGranted(
          consentsResult.value.some(
            (c) =>
              c.channel === CUSTOMER_APP_CHAT_CONSENT.channel &&
              c.purpose === CUSTOMER_APP_CHAT_CONSENT.purpose &&
              c.granted,
          ),
        );
      } else {
        const msg = getApiErrorMessage(consentsResult.reason, t('profile.loadConsentsFailed'));
        setConsentLoadError(msg);
      }

      if (eligibleResult.status === 'fulfilled') {
        setCareReminderEligible(eligibleResult.value);
      }

      if (pushResult.status === 'fulfilled') {
        setPushStatus(pushResult.value);
        setPushError(null);
      } else {
        setPushStatus(null);
        setPushError(getApiErrorMessage(pushResult.reason, t('profile.pushLoadFailed')));
      }
    } finally {
      setConsentLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadConsents();
  }, [loadConsents]);

  useRetryWhenApiOnline(() => loadConsents());

  const consentSummary = useMemo(() => {
    const granted = consentRows.filter((row) => row.granted);
    if (granted.length === 0) return t('profile.consentNone');
    return granted
      .map((row) => `${consentChannel(row.channel)} — ${consentPurpose(row.purpose)}`)
      .join(', ');
  }, [consentRows, consentChannel, consentPurpose, t]);

  const onDisablePush = async (showToast = true) => {
    setPushLoading(true);
    setPushError(null);
    try {
      const endpoint = await unsubscribePush();
      if (endpoint) {
        await unregisterPushSubscription(endpoint);
      }
      setPushStatus(await fetchPushStatus());
      if (showToast) message.success(t('profile.pushDisabledSuccess'));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('profile.pushDisableFailed')));
    } finally {
      setPushLoading(false);
    }
  };

  const onConsentToggle = async (key: string, granted: boolean) => {
    const row = consentRows.find((item) => item.key === key);
    if (!row || savingConsentKey === key) return;

    setSavingConsentKey(key);
    const previousItems = consentRows;
    setConsentRows((items) => items.map((item) => (item.key === key ? { ...item, granted } : item)));

    try {
      const saved = await upsertConsents([{ channel: row.channel, purpose: row.purpose, granted }]);
      setConsentRows(mergeCareReminderConsents(saved));
      setCareReminderEligible(await fetchCareReminderEligible());

      if (row.channel === APP_PUSH_CHANNEL && !granted && pushStatus?.subscribed) {
        void onDisablePush(false);
      }

      message.success(granted ? t('profile.turnedOn') : t('profile.turnedOff'));
    } catch (error) {
      setConsentRows(previousItems);
      message.error(getApiErrorMessage(error, t('profile.consentSaveFailed')));
    } finally {
      setSavingConsentKey(null);
    }
  };

  const onEnablePush = async () => {
    if (!browserPushSupported) {
      message.warning(t('profile.pushBrowserUnsupported'));
      return;
    }

    setPushLoading(true);
    setPushError(null);
    try {
      await requestNotificationPermission();

      const status = pushStatus ?? (await fetchPushStatus());
      if (!status.publicKey) {
        throw new Error(t('profile.vapidNotConfigured'));
      }

      if (!appPushConsentGranted) {
        const saved = await upsertConsents([
          { channel: APP_PUSH_CHANNEL, purpose: CARE_REMINDER_PURPOSE, granted: true },
        ]);
        setConsentRows(mergeCareReminderConsents(saved));
        setCareReminderEligible(await fetchCareReminderEligible());
      }

      const subscription = await subscribePush(status.publicKey);
      await registerPushSubscription(subscription);
      setPushStatus(await fetchPushStatus());
      message.success(t('profile.pushEnabledSuccess'));
    } catch (error) {
      const msg = getApiErrorMessage(error, t('profile.pushEnableFailed'));
      setPushError(msg);
      message.error(msg);
    } finally {
      setPushLoading(false);
    }
  };

  const onChatConsentToggle = async () => {
    if (savingConsentKey === CHAT_CONSENT_KEY) return;

    const granted = !chatConsentGranted;
    setSavingConsentKey(CHAT_CONSENT_KEY);
    const previous = chatConsentGranted;
    setChatConsentGranted(granted);
    try {
      const saved = await upsertConsents([
        {
          channel: CUSTOMER_APP_CHAT_CONSENT.channel,
          purpose: CUSTOMER_APP_CHAT_CONSENT.purpose,
          granted,
        },
      ]);
      setChatConsentGranted(
        saved.some(
          (c) =>
            c.channel === CUSTOMER_APP_CHAT_CONSENT.channel &&
            c.purpose === CUSTOMER_APP_CHAT_CONSENT.purpose &&
            c.granted,
        ),
      );
      message.success(granted ? t('profile.chatConsentOn') : t('profile.chatConsentOff'));
    } catch (error) {
      setChatConsentGranted(previous);
      message.error(getApiErrorMessage(error, t('profile.chatConsentSaveFailed')));
    } finally {
      setSavingConsentKey(null);
    }
  };

  const onLogout = async () => {
    try {
      if (refreshToken) {
        await logoutApi(refreshToken);
      }
    } catch {
      // vẫn xóa session local
    } finally {
      clearCustomerCachedData();
      clearSession();
      message.success(t('profile.logoutSuccess'));
      navigate('/login', { replace: true });
    }
  };

  const scrollToSettings = () => {
    document.getElementById('profile-hub-settings')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const menuItems: Array<{
    key: string;
    title: string;
    sub: string;
    icon: ReactNode;
    tone: MenuTone;
    onClick: () => void;
    wide?: boolean;
  }> = [
    {
      key: 'health',
      title: t('profile.healthWallet'),
      sub: t('profile.healthWalletSub'),
      icon: <HeartOutlined />,
      tone: 'rose',
      onClick: () => navigate('/health'),
    },
    {
      key: 'loyalty',
      title: t('profile.loyalty'),
      sub: t('profile.loyaltySub'),
      icon: <GiftOutlined />,
      tone: 'amber',
      onClick: () => navigate('/loyalty'),
    },
    {
      key: 'family',
      title: t('profile.family'),
      sub: t('profile.familySub'),
      icon: <TeamOutlined />,
      tone: 'violet',
      onClick: () => navigate('/family'),
    },
    {
      key: 'meds',
      title: t('profile.medications'),
      sub: t('profile.medicationsSub'),
      icon: <MedicineBoxOutlined />,
      tone: 'sky',
      onClick: () => navigate('/medications'),
    },
    {
      key: 'pharmacy',
      title: t('profile.myPharmacy'),
      sub: t('profile.myPharmacySub'),
      icon: <ShopOutlined />,
      tone: 'teal',
      onClick: () => navigate('/pharmacy'),
    },
    {
      key: 'ai',
      title: t('profile.aiCopilot'),
      sub: t('profile.aiCopilotSub'),
      icon: <RobotOutlined />,
      tone: 'indigo',
      onClick: () => navigate('/ai'),
    },
    {
      key: 'settings',
      title: t('profile.settings'),
      sub: t('profile.settingsSub'),
      icon: <SettingOutlined />,
      tone: 'slate',
      onClick: scrollToSettings,
    },
    {
      key: 'addresses',
      title: t('profile.addresses'),
      sub: t('profile.addressesSub'),
      icon: <EnvironmentOutlined />,
      tone: 'green',
      onClick: () => navigate('/addresses'),
    },
    {
      key: 'receivables',
      title: t('profile.receivables'),
      sub: t('profile.receivablesSub'),
      icon: <DollarOutlined />,
      tone: 'amber',
      onClick: () => navigate('/receivables'),
      wide: true,
    },
    {
      key: 'notifications',
      title:
        notificationCount > 0
          ? t('profile.notificationsNew', { count: notificationCount })
          : t('profile.notifications'),
      sub: t('profile.notificationsSub'),
      icon: <SafetyCertificateOutlined />,
      tone: 'teal',
      onClick: () => navigate('/notifications'),
      wide: true,
    },
  ];

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  return (
    <div className="profile-hub">
      <header className="profile-hub-header" style={headerStyle}>
        <div className="profile-hub-header-top">
          <div className="profile-hub-brand">
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div>
              <div className="profile-hub-brand-title">{branding.appName}</div>
              <div className="profile-hub-tagline">{branding.tagline || t('profile.hubTagline')}</div>
            </div>
          </div>
          <div className="profile-hub-shield" aria-hidden>
            <UserOutlined />
            <span className="profile-hub-shield-lock">
              <LockOutlined />
            </span>
          </div>
        </div>
      </header>

      <div className="profile-hub-sheet">
        <h1 className="profile-hub-section-title">{t('profile.title')}</h1>

        <section className="profile-hub-account-card">
          <div className="profile-hub-account-top">
            <div className="profile-hub-avatar" aria-hidden>
              <UserOutlined />
              <span className="profile-hub-avatar-cam">
                <CameraOutlined />
              </span>
            </div>
          </div>

          <div className="profile-hub-info-grid">
            <div className="profile-hub-info-cell">
              <span className="profile-hub-info-label">{t('profile.fullName')}</span>
              <span className="profile-hub-info-value">{profile?.fullName || '—'}</span>
            </div>
            <div className="profile-hub-info-cell">
              <span className="profile-hub-info-label">{t('profile.phone')}</span>
              <span className="profile-hub-info-value">{profile?.phone || '—'}</span>
            </div>
            <div className="profile-hub-info-cell">
              <span className="profile-hub-info-label">{t('profile.pharmacy')}</span>
              <span className="profile-hub-info-value">{profile?.tenantCode || '—'}</span>
            </div>
          </div>
        </section>

        <div className="profile-hub-menu-grid">
          {menuItems.map((item) => (
            <button
              key={item.key}
              type="button"
              className={`profile-hub-menu-item${item.wide ? ' profile-hub-menu-item--wide' : ''}`}
              onClick={item.onClick}
            >
              <span className={`profile-hub-menu-icon profile-hub-menu-icon--${item.tone}`}>{item.icon}</span>
              <span className="profile-hub-menu-copy">
                <span className="profile-hub-menu-title">{item.title}</span>
                <span className="profile-hub-menu-sub">{item.sub}</span>
              </span>
              <RightOutlined className="profile-hub-menu-chevron" />
            </button>
          ))}
        </div>

        <section className="profile-hub-card" id="profile-hub-settings">
          <h2 className="profile-hub-card-title">{t('profile.pushCardTitle')}</h2>
          {consentLoading ? (
            <div className="profile-hub-loading">
              <Spin />
            </div>
          ) : !browserPushSupported ? (
            <Alert type="info" showIcon message={t('profile.pushUnsupportedBrowser')} />
          ) : pushStatus === null ? (
            <Alert
              type="warning"
              showIcon
              message={t('profile.pushLoadFailed')}
              description={pushError ?? t('profile.pushLoadFailedDesc')}
              action={
                <Button size="small" onClick={() => void loadConsents()}>
                  {t('common.retry')}
                </Button>
              }
            />
          ) : !pushStatus.supported ? (
            <Alert type="warning" showIcon message={t('profile.pushApiDisabled')} />
          ) : (
            <>
              {pushError ? (
                <Alert
                  type="error"
                  showIcon
                  message={pushError}
                  style={{ marginBottom: 12 }}
                  closable
                  onClose={() => setPushError(null)}
                />
              ) : null}
              <div className="profile-hub-card-status">
                <span>{t('profile.pushCurrentStatus')}</span>
                <Tag color={pushStatus.subscribed ? 'success' : 'default'}>
                  {pushStatus.subscribed ? t('profile.pushSubscribed') : t('profile.pushNotSubscribed')}
                </Tag>
              </div>
              <p className="profile-hub-card-desc">{t('profile.pushCardHint')}</p>
              <button
                type="button"
                className={`profile-hub-push-btn${pushStatus.subscribed ? '' : ' profile-hub-push-btn--primary'}`}
                disabled={pushLoading}
                onClick={() => void (pushStatus.subscribed ? onDisablePush() : onEnablePush())}
              >
                <BellOutlined />
                {pushStatus.subscribed ? t('profile.pushDisable') : t('profile.pushEnable')}
              </button>
            </>
          )}
        </section>

        <section className="profile-hub-card">
          <h2 className="profile-hub-card-title">{t('profile.consentCardTitle')}</h2>
          {consentLoading ? (
            <div className="profile-hub-loading">
              <Spin />
            </div>
          ) : (
            <>
              {consentLoadError && !shouldHidePageErrorForOfflineApi(consentLoadError, online) ? (
                <Alert
                  type="error"
                  showIcon
                  style={{ marginBottom: 12 }}
                  message={consentLoadError}
                  action={
                    <Button size="small" onClick={() => void loadConsents()}>
                      {t('common.retry')}
                    </Button>
                  }
                />
              ) : null}

              {online === false && consentLoadError ? (
                <div className="profile-hub-loading">
                  <Spin tip={t('common.waitingApi')} />
                </div>
              ) : null}

              {!(online === false && consentLoadError) && !careReminderEligible ? (
                <Alert
                  type="warning"
                  showIcon
                  style={{ marginBottom: 12 }}
                  message={t('profile.consentWarning')}
                  action={
                    <Link to="/reminders" style={{ whiteSpace: 'nowrap' }}>
                      {t('profile.consentViewReminders')}
                    </Link>
                  }
                />
              ) : !(online === false && consentLoadError) ? (
                <p className="profile-hub-consent-hint">
                  {t('profile.consentEligible', { summary: consentSummary })}
                </p>
              ) : null}

              {!(online === false && consentLoadError) ? (
                <>
                  <p className="profile-hub-consent-hint">{t('profile.consentHint')}</p>
                  <div className="profile-hub-consent-list">
                    {consentRows.map((row) => (
                      <ConsentToggleRow
                        key={row.key}
                        icon={consentIcon(row.channel)}
                        label={consentChannel(row.channel)}
                        description={consentPurpose(row.purpose)}
                        checked={row.granted}
                        saving={savingConsentKey === row.key}
                        onToggle={() => void onConsentToggle(row.key, !row.granted)}
                      />
                    ))}

                    <ConsentToggleRow
                      icon={<MobileOutlined />}
                      label={consentChannel(CUSTOMER_APP_CHAT_CONSENT.channel)}
                      description={consentPurpose(CUSTOMER_APP_CHAT_CONSENT.purpose)}
                      checked={chatConsentGranted}
                      saving={savingConsentKey === CHAT_CONSENT_KEY}
                      onToggle={() => void onChatConsentToggle()}
                    />
                  </div>
                  {!chatConsentGranted ? (
                    <p className="profile-hub-consent-hint" style={{ marginTop: 10, marginBottom: 0 }}>
                      {t('profile.chatConsentHint')} <Link to="/chat">{t('nav.chat')}</Link>.
                    </p>
                  ) : null}
                </>
              ) : null}
            </>
          )}
        </section>

        <button type="button" className="profile-hub-logout" onClick={() => void onLogout()}>
          <LogoutOutlined />
          {t('profile.logout')}
        </button>
      </div>
    </div>
  );
}
