import { useState } from 'react';
import { Alert, Button, Card, Form, Input } from 'antd';
import { useNavigate } from 'react-router-dom';
import { loginApi } from '@/shared/api/auth.api';
import { useAuthStore } from '@/shared/auth/auth.store';

export function LoginPage() {
  const navigate = useNavigate();
  const setSession = useAuthStore((state) => state.setSession);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  return (
    <Card title="Second Brain" style={{ maxWidth: 420, margin: '48px auto' }}>
      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
      <Form
        layout="vertical"
        initialValues={{ tenantCode: 'SECOND_BRAIN' }}
        onFinish={async (values: { username: string; password: string; tenantCode?: string }) => {
          setBusy(true);
          setError(null);
          try {
            const result = await loginApi({
              username: values.username,
              password: values.password,
              tenantCode: values.tenantCode,
            });
            if (result.kind !== 'session') {
              setError('Host này không chọn workspace.');
              return;
            }
            setSession(result.session);
            navigate('/second-brain/session', { replace: true });
          } catch {
            setError('Đăng nhập không thành công.');
          } finally {
            setBusy(false);
          }
        }}
      >
        <Form.Item name="tenantCode" label="Tenant" rules={[{ required: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="username" label="Tài khoản" rules={[{ required: true }]}>
          <Input autoComplete="username" />
        </Form.Item>
        <Form.Item name="password" label="Mật khẩu" rules={[{ required: true }]}>
          <Input.Password autoComplete="current-password" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={busy} block>
          Đăng nhập
        </Button>
      </Form>
    </Card>
  );
}
