import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { App, Button, Card, Form, Input, Typography, Space } from 'antd';
import { KeyOutlined, LockOutlined, ShopOutlined, UserOutlined } from '@ant-design/icons';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { loginApi } from '@/shared/api/auth.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import {
  APP_BRAND,
  loadStoredTenantCode,
  saveStoredTenantCode,
} from '@/shared/config/app-brand';

type LoginFormValues = {
  tenantCode: string;
  username: string;
  password: string;
};

export function LoginPage() {
  const { t } = useTranslation('auth', { keyPrefix: 'login' });
  const { t: tc } = useTranslation('common', { keyPrefix: 'appLayout' });
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm<LoginFormValues>();
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((s) => s.setSession);

  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const fillDemo = () => {
    form.setFieldsValue({
      tenantCode: 'DEMO_PHARMACY',
      username: 'admin',
      password: 'Admin@123',
    });
  };

  const onFinish = async (values: LoginFormValues) => {
    const tenantCode = values.tenantCode.trim().toUpperCase();
    if (!tenantCode) {
      message.warning(t('messages.tenantRequired'));
      return;
    }

    setLoading(true);
    try {
      saveStoredTenantCode(tenantCode);
      const data = await loginApi({
        tenantCode,
        username: values.username.trim(),
        password: values.password,
      });
      if (!data?.accessToken) {
        message.error(t('messages.invalidResponse'));
        return;
      }
      setSession(data);
      message.success(t('messages.success'));
      navigate(from, { replace: true });
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.failed')));
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
        background: 'linear-gradient(135deg, #0f766e 0%, #134e4a 50%, #115e59 100%)',
        padding: 24,
      }}
    >
      <Card style={{ width: 440, boxShadow: '0 8px 32px rgba(0,0,0,0.2)' }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ textAlign: 'center' }}>
            <Typography.Title level={3} style={{ marginBottom: 4 }}>
              {APP_BRAND}
            </Typography.Title>
            <Typography.Text type="secondary">{tc('productName')}</Typography.Text>
          </div>

          <Form
            form={form}
            layout="vertical"
            onFinish={onFinish}
            onFinishFailed={onFinishFailed}
            requiredMark={false}
            initialValues={{
              tenantCode: loadStoredTenantCode(),
              username: 'admin',
            }}
            autoComplete="on"
          >
            <Form.Item
              name="tenantCode"
              label={t('tenantCode')}
              rules={[{ required: true, message: t('tenantCodeRequired') }]}
              tooltip={t('tenantCodeTooltip')}
            >
              <Input
                prefix={<ShopOutlined />}
                placeholder="NT_A"
                size="large"
                style={{ textTransform: 'uppercase' }}
                autoComplete="organization"
              />
            </Form.Item>
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
                  DEMO_PHARMACY / admin
                </Button>
              </Typography.Paragraph>
            ) : null}
          </Space>
        </Space>
      </Card>
    </div>
  );
}
