import { useCallback, useEffect, useState } from 'react';
import { App, Alert, Button, Card, Form, Input, Space, Typography } from 'antd';
import { LockOutlined, ShopOutlined, UserOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { loginApi } from '@/shared/api/auth.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useAuthStore } from '@/shared/auth/auth.store';
import { APP_BRAND, DEFAULT_TENANT_CODE, isTenantCodeLocked, loadStoredTenantCode, saveStoredTenantCode } from '@/shared/config/app-brand';
import { AppBrandLogo } from '@/shared/components/AppBrandLogo';
import { apiPath, resolveApiOrigin } from '@/shared/api/api-base';

type FormValues = { tenantCode: string; username: string; password: string };

async function probeApiHealth(timeoutMs = 8000): Promise<boolean> {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), timeoutMs);
  try {
    const res = await fetch(apiPath('/api/health'), {
      method: 'GET',
      signal: controller.signal,
      cache: 'no-store',
    });
    return res.ok;
  } catch {
    return false;
  } finally {
    window.clearTimeout(timer);
  }
}

export function LoginPage() {
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [apiOnline, setApiOnline] = useState<boolean | null>(null);
  const [checkingApi, setCheckingApi] = useState(false);
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const tenantLocked = isTenantCodeLocked();

  const checkApi = useCallback(async () => {
    setCheckingApi(true);
    try {
      // Retry briefly — deploy/restart can make the first probe fail.
      for (let attempt = 0; attempt < 3; attempt += 1) {
        if (await probeApiHealth()) {
          setApiOnline(true);
          return;
        }
        await new Promise((r) => window.setTimeout(r, 700 * (attempt + 1)));
      }
      setApiOnline(false);
    } finally {
      setCheckingApi(false);
    }
  }, []);

  useEffect(() => {
    void checkApi();
    const timer = window.setInterval(() => {
      void probeApiHealth(5000).then((ok) => {
        if (ok) setApiOnline(true);
      });
    }, 20_000);
    return () => window.clearInterval(timer);
  }, [checkApi]);

  const onFinish = async (values: FormValues) => {
    const tenantCode = (tenantLocked ? DEFAULT_TENANT_CODE : values.tenantCode).trim().toUpperCase();
    if (!tenantCode) {
      message.warning('Nhập mã nhà thuốc');
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
      setSession(data);
      setApiOnline(true);
      message.success('Đăng nhập thành công');
      navigate('/', { replace: true });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Sai mã nhà thuốc, tên đăng nhập hoặc mật khẩu'));
      // If login failed due to network, refresh banner state.
      void checkApi();
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="staff-shell" style={{ justifyContent: 'center', padding: 20 }}>
      <Card style={{ borderRadius: 16, width: '100%' }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ textAlign: 'center' }}>
            <div style={{ display: 'flex', justifyContent: 'center', marginBottom: 12 }}>
              <AppBrandLogo height={64} maxWidth={180} />
            </div>
            <Typography.Title level={3} style={{ marginBottom: 4, color: '#0f766e' }}>
              {APP_BRAND}
            </Typography.Title>
            <Typography.Text type="secondary">Quầy bán hàng</Typography.Text>
          </div>
          {apiOnline === false ? (
            <Alert
              type="warning"
              showIcon
              message="Chưa kết nối được máy chủ"
              description={
                <Space direction="vertical" size={8} style={{ width: '100%' }}>
                  <span>
                    {resolveApiOrigin()
                      ? `Không gọi được ${resolveApiOrigin()}. Kiểm tra Wi‑Fi/4G rồi bấm Thử lại — vẫn đăng nhập được nếu mạng ổn định.`
                      : 'Chạy run-dev.bat ở thư mục KitPlatform (API port 5290), rồi tải lại trang.'}
                  </span>
                  <Button size="small" loading={checkingApi} onClick={() => void checkApi()}>
                    Thử lại kết nối
                  </Button>
                </Space>
              }
            />
          ) : null}
          <Form
            layout="vertical"
            onFinish={onFinish}
            initialValues={{
              tenantCode: loadStoredTenantCode() || (import.meta.env.DEV ? 'DEMO_PHARMACY' : ''),
              username: 'admin',
            }}
          >
            {!tenantLocked ? (
              <Form.Item name="tenantCode" label="Mã nhà thuốc" rules={[{ required: true }]}>
                <Input prefix={<ShopOutlined />} placeholder="NT_XUANHOA" style={{ textTransform: 'uppercase' }} />
              </Form.Item>
            ) : null}
            <Form.Item name="username" label="Tên đăng nhập" rules={[{ required: true }]}>
              <Input prefix={<UserOutlined />} autoComplete="username" />
            </Form.Item>
            <Form.Item name="password" label="Mật khẩu" rules={[{ required: true }]}>
              <Input.Password prefix={<LockOutlined />} autoComplete="current-password" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block size="large" loading={loading}>
              Đăng nhập
            </Button>
          </Form>
        </Space>
      </Card>
    </div>
  );
}
