import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Descriptions, Space, Spin, Switch, Tag, Typography, message } from 'antd';
import { BellOutlined, DollarOutlined, EnvironmentOutlined, ExperimentOutlined, GiftOutlined, HeartOutlined, LogoutOutlined, RobotOutlined, ShopOutlined, TeamOutlined } from '@ant-design/icons';
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
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { isPushSupported, requestNotificationPermission, subscribePush, unsubscribePush } from '@/shared/push/push-client';
import { useCustomerNotificationCount } from '@/shared/hooks/useCustomerNotificationCount';
import { useCustomerLocale } from '@/shared/i18n/LocaleProvider';
import { LanguageSelect } from '@/shared/i18n/LanguageSelect';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';

const APP_PUSH_CHANNEL = 4;
const CARE_REMINDER_PURPOSE = 2;
const CHAT_CONSENT_KEY = `${CUSTOMER_APP_CHAT_CONSENT.channel}:${CUSTOMER_APP_CHAT_CONSENT.purpose}`;

type ConsentRow = {
  key: string;
  channel: number;
  purpose: number;
  granted: boolean;
};

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

function ConsentToggleRow({
  label,
  description,
  checked,
  saving,
  onToggle,
}: {
  label: string;
  description: string;
  checked: boolean;
  saving: boolean;
  onToggle: () => void;
}) {
  const { t } = useTranslation();

  return (
    <div
      role="button"
      tabIndex={0}
      aria-pressed={checked}
      aria-busy={saving}
      aria-label={t('profile.toggleAria', { label, state: checked ? t('common.enabled') : t('common.disabled') })}
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
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: 12,
        padding: '10px 12px',
        borderRadius: 10,
        border: `1px solid ${checked ? '#5eead4' : '#e2e8f0'}`,
        background: checked ? '#f0fdfa' : '#f8fafc',
        cursor: saving ? 'wait' : 'pointer',
        touchAction: 'manipulation',
        userSelect: 'none',
        opacity: saving ? 0.72 : 1,
      }}
    >
      <div style={{ flex: 1, minWidth: 0, pointerEvents: 'none' }}>
        <Space wrap size={6} style={{ marginBottom: 2 }}>
          <Typography.Text strong>{label}</Typography.Text>
          <Tag color={checked ? 'success' : 'default'} style={{ margin: 0 }}>
            {checked ? t('common.enabled') : t('common.disabled')}
          </Tag>
        </Space>
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
          {description}
        </Typography.Text>
      </div>
      <Switch
        checked={checked}
        loading={saving}
        checkedChildren={t('common.on')}
        unCheckedChildren={t('common.off')}
        tabIndex={-1}
        style={{ flexShrink: 0, pointerEvents: 'none' }}
      />
    </div>
  );
}

export function ProfilePage() {
  const { t } = useTranslation();
  const { consentChannel, consentPurpose } = useCustomerLabels();
  const { locale, supportedLocales, setLocale, saving: savingLocale } = useCustomerLocale();
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
    () => consentRows.some((row) => row.channel === APP_PUSH_CHANNEL && row.purpose === CARE_REMINDER_PURPOSE && row.granted),
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
      // Xin quyền ngay khi user bấm — tránh mất "user gesture" sau await (Safari/mobile).
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

  const consentBody = consentLoading ? (
    <div style={{ textAlign: 'center', padding: 24 }}>
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
              Thử lại
            </Button>
          }
        />
      ) : null}

      {online === false && consentLoadError ? (
        <div style={{ textAlign: 'center', padding: 16, marginBottom: 12 }}>
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
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          {t('profile.consentEligible', { summary: consentSummary })}
        </Typography.Paragraph>
      ) : null}

      {!(online === false && consentLoadError) ? (
        <>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
        {t('profile.consentHint')}
      </Typography.Paragraph>

      <Space direction="vertical" style={{ width: '100%' }} size={12}>
        {consentRows.map((row) => (
          <ConsentToggleRow
            key={row.key}
            label={consentChannel(row.channel)}
            description={consentPurpose(row.purpose)}
            checked={row.granted}
            saving={savingConsentKey === row.key}
            onToggle={() => void onConsentToggle(row.key, !row.granted)}
          />
        ))}

        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4, fontSize: 12 }}>
          {t('profile.chatConsentSection')}
        </Typography.Text>

        <ConsentToggleRow
          label={consentChannel(CUSTOMER_APP_CHAT_CONSENT.channel)}
          description={consentPurpose(CUSTOMER_APP_CHAT_CONSENT.purpose)}
          checked={chatConsentGranted}
          saving={savingConsentKey === CHAT_CONSENT_KEY}
          onToggle={() => void onChatConsentToggle()}
        />

        {!chatConsentGranted ? (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
            {t('profile.chatConsentHint')} <Link to="/chat">{t('nav.chat')}</Link>.
          </Typography.Paragraph>
        ) : null}
      </Space>
        </>
      ) : null}
    </>
  );

  return (
    <div>
      <BackToHomeButton />
      <Typography.Title level={5} style={{ marginTop: 0 }}>
        {t('profile.title')}
      </Typography.Title>

      <Card style={{ borderRadius: 12, marginBottom: 16 }} title={t('profile.language')}>
        <LanguageSelect
          value={locale}
          options={supportedLocales}
          loading={savingLocale}
          onChange={(value) => {
            void setLocale(value).then((saved) => {
              if (saved) message.success(t('profile.languageSaved'));
            });
          }}
        />
      </Card>

      <Card style={{ borderRadius: 12, marginBottom: 16 }}>
        <Descriptions column={1} size="small">
          <Descriptions.Item label={t('profile.fullName')}>{profile?.fullName ?? '—'}</Descriptions.Item>
          <Descriptions.Item label={t('profile.phone')}>{profile?.phone ?? '—'}</Descriptions.Item>
          <Descriptions.Item label={t('profile.pharmacy')}>{profile?.tenantCode ?? '—'}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Card style={{ borderRadius: 12, marginBottom: 16 }}>
        <Button
          type="default"
          block
          size="large"
          icon={<HeartOutlined />}
          onClick={() => navigate('/health')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.healthWallet')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<GiftOutlined />}
          onClick={() => navigate('/loyalty')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.loyalty')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<TeamOutlined />}
          onClick={() => navigate('/family')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.family')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<ExperimentOutlined />}
          onClick={() => navigate('/medications')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.medications')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<ShopOutlined />}
          onClick={() => navigate('/pharmacy')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.myPharmacy')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<RobotOutlined />}
          onClick={() => navigate('/ai')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.aiCopilot')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<DollarOutlined />}
          onClick={() => navigate('/receivables')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.receivables')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<EnvironmentOutlined />}
          onClick={() => navigate('/addresses')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          {t('profile.addresses')}
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<BellOutlined />}
          onClick={() => navigate('/notifications')}
          style={{ textAlign: 'left', justifyContent: 'flex-start' }}
        >
          {notificationCount > 0
            ? t('profile.notificationsNew', { count: notificationCount })
            : t('profile.notifications')}
        </Button>
      </Card>

      <Card title={t('profile.pushCardTitle')} style={{ borderRadius: 12, marginBottom: 16 }}>
        {consentLoading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
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
          <Alert
            type="warning"
            showIcon
            message={t('profile.pushApiDisabled')}
          />
        ) : (
          <>
            {pushError ? (
              <Alert type="error" showIcon message={pushError} style={{ marginBottom: 12 }} closable onClose={() => setPushError(null)} />
            ) : null}
            <Space wrap style={{ marginBottom: 12 }}>
              <Typography.Text type="secondary">{t('profile.pushDeviceStatus')}</Typography.Text>
              <Tag color={pushStatus.subscribed ? 'success' : 'default'}>
                {pushStatus.subscribed ? t('profile.pushSubscribed') : t('profile.pushNotSubscribed')}
              </Tag>
            </Space>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
              {pushStatus.subscribed
                ? t('profile.pushSubscribedDesc', { count: pushStatus.subscriptionCount })
                : t('profile.pushNotSubscribedDesc')}
            </Typography.Paragraph>
            <Button
              type={pushStatus.subscribed ? 'default' : 'primary'}
              icon={<BellOutlined />}
              loading={pushLoading}
              block
              onClick={() => void (pushStatus.subscribed ? onDisablePush() : onEnablePush())}
            >
              {pushStatus.subscribed ? t('profile.pushDisable') : t('profile.pushEnable')}
            </Button>
          </>
        )}
      </Card>

      <Card title={t('profile.consentCardTitle')} style={{ borderRadius: 12, marginBottom: 16 }}>
        {consentBody}
      </Card>

      <Button danger block icon={<LogoutOutlined />} size="large" onClick={() => void onLogout()}>
        {t('profile.logout')}
      </Button>
    </div>
  );
}
