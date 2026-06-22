import { useState } from 'react';
import { Button, Card, Form, Input, Typography, message, Space } from 'antd';
import { LockOutlined, UserOutlined } from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { loginApi } from '@/shared/api/auth.api';
import { useAuthStore } from '@/shared/auth/auth.store';

export function LoginPage() {
  const [loading, setLoading] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((s) => s.setSession);

  const from = (location.state as { from?: string } | null)?.from ?? '/';

  const onFinish = async (values: { username: string; password: string }) => {
    setLoading(true);
    try {
      const data = await loginApi({
        username: values.username.trim(),
        password: values.password,
      });
      setSession(data);
      message.success('Đăng nhập thành công');
      navigate(from, { replace: true });
    } catch {
      message.error('Sai tên đăng nhập hoặc mật khẩu');
    } finally {
      setLoading(false);
    }
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
      <Card style={{ width: 420, boxShadow: '0 8px 32px rgba(0,0,0,0.2)' }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ textAlign: 'center' }}>
            <Typography.Title level={3} style={{ marginBottom: 4 }}>
              PharmaCore
            </Typography.Title>
            <Typography.Text type="secondary">Đăng nhập hệ thống ERP nhà thuốc</Typography.Text>
          </div>

          <Form layout="vertical" onFinish={onFinish} requiredMark={false}>
            <Form.Item
              name="username"
              label="Tên đăng nhập"
              rules={[{ required: true, message: 'Nhập tên đăng nhập' }]}
            >
              <Input prefix={<UserOutlined />} placeholder="admin" size="large" autoComplete="username" />
            </Form.Item>
            <Form.Item
              name="password"
              label="Mật khẩu"
              rules={[{ required: true, message: 'Nhập mật khẩu' }]}
            >
              <Input.Password prefix={<LockOutlined />} placeholder="••••••••" size="large" autoComplete="current-password" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block size="large" loading={loading}>
              Đăng nhập
            </Button>
          </Form>

          <Typography.Paragraph type="secondary" style={{ marginBottom: 0, textAlign: 'center', fontSize: 12 }}>
            Demo: admin / Admin@123
          </Typography.Paragraph>
        </Space>
      </Card>
    </div>
  );
}
