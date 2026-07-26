import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Card, Form, Input, Modal, Radio, Space, Typography } from 'antd';
import { KeyOutlined, LockOutlined, MailOutlined, ShopOutlined, UserOutlined } from '@ant-design/icons';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { loginApi, selectWorkspaceApi } from '@/shared/api/auth.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { AuthWorkspace, LoginResponse } from '@/shared/api/types';
import { useAuthStore } from '@/shared/auth/auth.store';
import {
  APP_BRAND,
  DEFAULT_TENANT_CODE,
  isTenantCodeLocked,
  loadStoredTenantCode,
  saveStoredTenantCode,
} from '@/shared/config/app-brand';
import { AdminLanguageSelect } from '@/shared/i18n/LanguageSelect';
import { AppBrandLogo } from '@/shared/components/AppBrandLogo';

type LoginMode = 'tenant' | 'email';

type LoginFormValues = {
  tenantCode: string;
  username: string;
  email: string;
  password: string;
};

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

export function LoginPage() {
  const { t } = useTranslation('auth', { keyPrefix: 'login' });
  const { t: tc } = useTranslation('common', { keyPrefix: 'appLayout' });
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<LoginMode>('email');
  const [form] = Form.useForm<LoginFormValues>();
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((s) => s.setSession);

  const [choiceToken, setChoiceToken] = useState<string | null>(null);
  const [workspaces, setWorkspaces] = useState<AuthWorkspace[]>([]);
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null);

  const from = (location.state as { from?: string } | null)?.from ?? '/';
  const tenantLocked = isTenantCodeLocked();

  const applySession = (data: LoginResponse) => {
    if (!data?.accessToken) {
      message.error(t('messages.invalidResponse'));
      return;
    }
    saveStoredTenantCode(data.user.tenantCode);
    setSession(data);
    message.success(t('messages.success'));
    navigate(from, { replace: true });
  };

  const fillDemo = () => {
    setMode('tenant');
    form.setFieldsValue({
      tenantCode: 'DEMO_PHARMACY',
      username: 'admin',
      password: 'Admin@123',
    });
  };

  const fillFamilyDemo = () => {
    setMode('tenant');
    form.setFieldsValue({
      tenantCode: 'DEMO_FAMILY',
      username: 'admin',
      password: 'Admin@123',
    });
  };

  const onFinish = async (values: LoginFormValues) => {
    setLoading(true);
    try {
      if (mode === 'email') {
        const email = values.email.trim().toLowerCase();
        if (!email.includes('@')) {
          message.warning('Nhập email KitPlatform hợp lệ.');
          return;
        }
        const result = await loginApi({
          username: email,
          password: values.password,
        });
        if (result.kind === 'choice') {
          setChoiceToken(result.choice.selectionToken);
          setWorkspaces(result.choice.workspaces);
          setSelectedUserId(
            result.choice.workspaces.find((w) => w.isDefault)?.userId ??
              result.choice.workspaces[0]?.userId ??
              null,
          );
          return;
        }
        applySession(result.session);
        return;
      }

      const tenantCode = (tenantLocked ? DEFAULT_TENANT_CODE : values.tenantCode).trim().toUpperCase();
      if (!tenantCode) {
        message.warning(t('messages.tenantRequired'));
        return;
      }

      saveStoredTenantCode(tenantCode);
      const result = await loginApi({
        tenantCode,
        username: values.username.trim(),
        password: values.password,
      });
      if (result.kind === 'choice') {
        setChoiceToken(result.choice.selectionToken);
        setWorkspaces(result.choice.workspaces);
        setSelectedUserId(
          result.choice.workspaces.find((w) => w.isDefault)?.userId ??
            result.choice.workspaces[0]?.userId ??
            null,
        );
        return;
      }
      applySession(result.session);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.failed')));
    } finally {
      setLoading(false);
    }
  };

  const onSelectWorkspace = async () => {
    if (!choiceToken || !selectedUserId) {
      message.warning('Chọn một workspace để tiếp tục.');
      return;
    }
    setLoading(true);
    try {
      const session = await selectWorkspaceApi({
        selectionToken: choiceToken,
        userId: selectedUserId,
      });
      setChoiceToken(null);
      setWorkspaces([]);
      applySession(session);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không chọn được workspace.'));
    } finally {
      setLoading(false);
    }
  };

  const onFinishFailed = () => {
    message.warning(t('messages.formIncomplete'));
  };

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #1b3a6b 0%, #2563eb 55%, #15803d 100%)',
        padding: 24,
      }}
    >
      <Card style={{ width: 440, boxShadow: '0 8px 32px rgba(0,0,0,0.2)' }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <AdminLanguageSelect />
          </div>
          <div style={{ textAlign: 'center' }}>
            <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 12 }}>
              <AppBrandLogo height={72} maxWidth={200} />
            </div>
            <Typography.Title level={3} style={{ marginBottom: 4 }}>
              {APP_BRAND}
            </Typography.Title>
            <Typography.Text type="secondary">{tc('productName')}</Typography.Text>
          </div>

          {!tenantLocked ? (
            <Radio.Group
              value={mode}
              onChange={(e) => setMode(e.target.value as LoginMode)}
              optionType="button"
              buttonStyle="solid"
              style={{ width: '100%', display: 'flex' }}
            >
              <Radio.Button value="email" style={{ flex: 1, textAlign: 'center' }}>
                Email Kit
              </Radio.Button>
              <Radio.Button value="tenant" style={{ flex: 1, textAlign: 'center' }}>
                Mã cửa hàng
              </Radio.Button>
            </Radio.Group>
          ) : null}

          <Form
            form={form}
            layout="vertical"
            onFinish={onFinish}
            onFinishFailed={onFinishFailed}
            requiredMark={false}
            initialValues={{
              tenantCode: loadStoredTenantCode(),
              username: 'admin',
              email: '',
            }}
            autoComplete="on"
          >
            {mode === 'tenant' && !tenantLocked ? (
              <Form.Item
                name="tenantCode"
                label={t('tenantCode')}
                rules={[{ required: true, message: t('tenantCodeRequired') }]}
                tooltip={t('tenantCodeTooltip')}
              >
                <Input
                  prefix={<ShopOutlined />}
                  placeholder="DEMO_FAMILY / DEMO_PHARMACY"
                  size="large"
                  style={{ textTransform: 'uppercase' }}
                  autoComplete="organization"
                />
              </Form.Item>
            ) : null}

            {mode === 'email' ? (
              <Form.Item
                name="email"
                label="Email KitPlatform"
                rules={[
                  { required: true, message: 'Nhập email' },
                  { type: 'email', message: 'Email không hợp lệ' },
                ]}
              >
                <Input
                  prefix={<MailOutlined />}
                  placeholder="ban@email.com"
                  size="large"
                  autoComplete="email"
                  name="email"
                />
              </Form.Item>
            ) : (
              <Form.Item
                name="username"
                label={t('username')}
                rules={[{ required: true, message: t('usernameRequired') }]}
              >
                <Input
                  prefix={<UserOutlined />}
                  placeholder="admin"
                  size="large"
                  autoComplete="username"
                  name="username"
                />
              </Form.Item>
            )}

            <Form.Item
              name="password"
              label={t('password')}
              rules={[{ required: true, message: t('passwordRequired') }]}
            >
              <Input.Password
                prefix={<LockOutlined />}
                placeholder="••••••••"
                size="large"
                autoComplete="current-password"
                name="password"
              />
            </Form.Item>
            <Button type="primary" htmlType="submit" block size="large" loading={loading}>
              {t('submit')}
            </Button>
          </Form>

          <Space direction="vertical" size={4} style={{ width: '100%', textAlign: 'center' }}>
            <Link to="/setup">
              <Button type="link" icon={<KeyOutlined />} style={{ padding: 0 }}>
                {t('setupLink')}
              </Button>
            </Link>
            {import.meta.env.DEV ? (
              <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 12 }}>
                {t('devDemo')}{' '}
                <Button type="link" size="small" style={{ padding: 0, height: 'auto', fontSize: 12 }} onClick={fillDemo}>
                  DEMO_PHARMACY
                </Button>
                {' · '}
                <Button
                  type="link"
                  size="small"
                  style={{ padding: 0, height: 'auto', fontSize: 12 }}
                  onClick={fillFamilyDemo}
                >
                  DEMO_FAMILY
                </Button>
              </Typography.Paragraph>
            ) : null}
          </Space>
        </Space>
      </Card>

      <Modal
        title="Chọn workspace"
        open={Boolean(choiceToken)}
        onCancel={() => {
          setChoiceToken(null);
          setWorkspaces([]);
        }}
        onOk={onSelectWorkspace}
        okText="Vào workspace"
        confirmLoading={loading}
        destroyOnClose
      >
        <Typography.Paragraph type="secondary">
          Tài khoản Kit này có nhiều đơn vị. Chọn nơi bạn muốn làm việc.
        </Typography.Paragraph>
        <Radio.Group
          value={selectedUserId}
          onChange={(e) => setSelectedUserId(e.target.value)}
          style={{ width: '100%' }}
        >
          <Space direction="vertical" style={{ width: '100%' }}>
            {workspaces.map((w) => (
              <Radio key={w.userId} value={w.userId} style={{ width: '100%' }}>
                <div>
                  <strong>{w.tenantName}</strong>{' '}
                  <Typography.Text type="secondary">
                    ({w.tenantCode} · {productLabel(w.productCode)})
                  </Typography.Text>
                </div>
              </Radio>
            ))}
          </Space>
        </Radio.Group>
      </Modal>
    </div>
  );
}
