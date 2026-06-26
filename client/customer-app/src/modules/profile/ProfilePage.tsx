import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Descriptions, Space, Spin, Switch, Tag, Typography, message } from 'antd';
import { BellOutlined, EnvironmentOutlined, GiftOutlined, LogoutOutlined } from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
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
  CONSENT_CHANNEL_LABELS,
  CONSENT_PURPOSE_LABELS,
  CUSTOMER_APP_CARE_REMINDER_CONSENTS,
  CUSTOMER_APP_CHAT_CONSENT,
  type CustomerConsent,
  type PushSubscriptionStatus,
} from '@/shared/api/customer-app.types';
import { useAuthStore } from '@/shared/auth/auth.store';
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { isPushSupported, requestNotificationPermission, subscribePush, unsubscribePush } from '@/shared/push/push-client';
import { useCustomerNotificationCount } from '@/shared/hooks/useCustomerNotificationCount';

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
  return (
    <div
      role="button"
      tabIndex={0}
      aria-pressed={checked}
      aria-busy={saving}
      aria-label={`${label} — ${checked ? 'đã bật' : 'chưa bật'}`}
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
            {checked ? 'Đã bật' : 'Chưa bật'}
          </Tag>
        </Space>
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
          {description}
        </Typography.Text>
      </div>
      <Switch
        checked={checked}
        loading={saving}
        checkedChildren="Bật"
        unCheckedChildren="Tắt"
        tabIndex={-1}
        style={{ flexShrink: 0, pointerEvents: 'none' }}
      />
    </div>
  );
}

export function ProfilePage() {
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
        const msg = getApiErrorMessage(consentsResult.reason, 'Không tải được đồng ý');
        setConsentLoadError(msg);
      }

      if (eligibleResult.status === 'fulfilled') {
        setCareReminderEligible(eligibleResult.value);
      }

      if (pushResult.status === 'fulfilled') {
        setPushStatus(pushResult.value);
      } else {
        setPushStatus(null);
      }
    } finally {
      setConsentLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadConsents();
  }, [loadConsents]);

  useRetryWhenApiOnline(() => loadConsents());

  const consentSummary = useMemo(() => {
    const granted = consentRows.filter((row) => row.granted);
    if (granted.length === 0) return 'Chưa đồng ý kênh nào';
    return granted
      .map((row) => `${CONSENT_CHANNEL_LABELS[row.channel]} — ${CONSENT_PURPOSE_LABELS[row.purpose]}`)
      .join(', ');
  }, [consentRows]);

  const onDisablePush = async (showToast = true) => {
    setPushLoading(true);
    setPushError(null);
    try {
      const endpoint = await unsubscribePush();
      if (endpoint) {
        await unregisterPushSubscription(endpoint);
      }
      setPushStatus(await fetchPushStatus());
      if (showToast) message.success('Đã tắt thông báo push');
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không tắt được thông báo push'));
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

      message.success(granted ? 'Đã bật' : 'Đã tắt');
    } catch (error) {
      setConsentRows(previousItems);
      message.error(getApiErrorMessage(error, 'Không lưu được đồng ý'));
    } finally {
      setSavingConsentKey(null);
    }
  };

  const onEnablePush = async () => {
    if (!browserPushSupported) {
      message.warning('Trình duyệt không hỗ trợ thông báo push.');
      return;
    }

    setPushLoading(true);
    setPushError(null);
    try {
      // Xin quyền ngay khi user bấm — tránh mất "user gesture" sau await (Safari/mobile).
      await requestNotificationPermission();

      const status = pushStatus ?? (await fetchPushStatus());
      if (!status.publicKey) {
        throw new Error('API chưa cấu hình VAPID key (CustomerAppPush).');
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
      message.success('Đã bật thông báo push trên thiết bị này');
    } catch (error) {
      const msg = getApiErrorMessage(error, 'Không bật được thông báo push');
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
      message.success(granted ? 'Đã bật chat dược sĩ' : 'Đã tắt chat dược sĩ');
    } catch (error) {
      setChatConsentGranted(previous);
      message.error(getApiErrorMessage(error, 'Không lưu được đồng ý chat'));
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
      clearSession();
      message.success('Đã đăng xuất');
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
          <Spin tip="Đang chờ API — tự tải lại khi kết nối trở lại" />
        </div>
      ) : null}

      {!(online === false && consentLoadError) && !careReminderEligible ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Cần đồng ý ít nhất SMS hoặc App push cho nhắc chăm sóc để bật lịch nhắc uống thuốc."
          action={
            <Link to="/reminders" style={{ whiteSpace: 'nowrap' }}>
              Xem nhắc thuốc
            </Link>
          }
        />
      ) : !(online === false && consentLoadError) ? (
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          Đã đủ điều kiện gửi nhắc: {consentSummary}
        </Typography.Paragraph>
      ) : null}

      {!(online === false && consentLoadError) ? (
        <>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 12, fontSize: 12 }}>
        Mỗi dòng hiển thị nhãn <strong>Đã bật</strong> / <strong>Chưa bật</strong> — chạm cả dòng để bật hoặc tắt.
      </Typography.Paragraph>

      <Space direction="vertical" style={{ width: '100%' }} size={12}>
        {consentRows.map((row) => (
          <ConsentToggleRow
            key={row.key}
            label={CONSENT_CHANNEL_LABELS[row.channel]}
            description={CONSENT_PURPOSE_LABELS[row.purpose]}
            checked={row.granted}
            saving={savingConsentKey === row.key}
            onToggle={() => void onConsentToggle(row.key, !row.granted)}
          />
        ))}

        <Typography.Text type="secondary" style={{ display: 'block', marginTop: 4, fontSize: 12 }}>
          Chat dược sĩ
        </Typography.Text>

        <ConsentToggleRow
          label={CONSENT_CHANNEL_LABELS[CUSTOMER_APP_CHAT_CONSENT.channel]}
          description={CONSENT_PURPOSE_LABELS[CUSTOMER_APP_CHAT_CONSENT.purpose]}
          checked={chatConsentGranted}
          saving={savingConsentKey === CHAT_CONSENT_KEY}
          onToggle={() => void onChatConsentToggle()}
        />

        {!chatConsentGranted ? (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
            Bật đồng ý này để gửi tin nhắn tại mục <Link to="/chat">Chat</Link>.
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
        Tài khoản
      </Typography.Title>

      <Card style={{ borderRadius: 12, marginBottom: 16 }}>
        <Descriptions column={1} size="small">
          <Descriptions.Item label="Họ tên">{profile?.fullName ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Số điện thoại">{profile?.phone ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="Nhà thuốc">{profile?.tenantCode ?? '—'}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Card style={{ borderRadius: 12, marginBottom: 16 }}>
        <Button
          type="default"
          block
          size="large"
          icon={<GiftOutlined />}
          onClick={() => navigate('/loyalty')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          Điểm thưởng & voucher
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<EnvironmentOutlined />}
          onClick={() => navigate('/addresses')}
          style={{ textAlign: 'left', justifyContent: 'flex-start', marginBottom: 12 }}
        >
          Địa chỉ giao hàng
        </Button>
        <Button
          type="default"
          block
          size="large"
          icon={<BellOutlined />}
          onClick={() => navigate('/notifications')}
          style={{ textAlign: 'left', justifyContent: 'flex-start' }}
        >
          Thông báo{notificationCount > 0 ? ` (${notificationCount} mới)` : ''}
        </Button>
      </Card>

      <Card title="Thông báo push (PWA)" style={{ borderRadius: 12, marginBottom: 16 }}>
        {consentLoading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : !browserPushSupported ? (
          <Alert type="info" showIcon message="Trình duyệt hiện tại chưa hỗ trợ Web Push." />
        ) : pushStatus === null ? (
          <Alert
            type="warning"
            showIcon
            message="Không tải được trạng thái push"
            description="Kiểm tra API đang chạy (port 5290) rồi bấm Thử lại."
            action={
              <Button size="small" onClick={() => void loadConsents()}>
                Thử lại
              </Button>
            }
          />
        ) : !pushStatus.supported ? (
          <Alert
            type="warning"
            showIcon
            message="API chưa bật push — cấu hình CustomerAppPush:PublicKey/PrivateKey rồi restart API."
          />
        ) : (
          <>
            {pushError ? (
              <Alert type="error" showIcon message={pushError} style={{ marginBottom: 12 }} closable onClose={() => setPushError(null)} />
            ) : null}
            <Space wrap style={{ marginBottom: 12 }}>
              <Typography.Text type="secondary">Trạng thái thiết bị:</Typography.Text>
              <Tag color={pushStatus.subscribed ? 'success' : 'default'}>
                {pushStatus.subscribed ? 'Đã bật push' : 'Chưa bật push'}
              </Tag>
            </Space>
            <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
              {pushStatus.subscribed
                ? `Đã đăng ký ${pushStatus.subscriptionCount} thiết bị nhận nhắc uống thuốc qua push.`
                : 'Bật push để nhận nhắc uống thuốc (dev: mở http://localhost:5174, cho phép thông báo khi trình duyệt hỏi).'}
            </Typography.Paragraph>
            <Button
              type={pushStatus.subscribed ? 'default' : 'primary'}
              icon={<BellOutlined />}
              loading={pushLoading}
              block
              onClick={() => void (pushStatus.subscribed ? onDisablePush() : onEnablePush())}
            >
              {pushStatus.subscribed ? 'Tắt thông báo push' : 'Bật thông báo push'}
            </Button>
          </>
        )}
      </Card>

      <Card title="Đồng ý nhận thông báo (CDP)" style={{ borderRadius: 12, marginBottom: 16 }}>
        {consentBody}
      </Card>

      <Button danger block icon={<LogoutOutlined />} size="large" onClick={() => void onLogout()}>
        Đăng xuất
      </Button>
    </div>
  );
}
