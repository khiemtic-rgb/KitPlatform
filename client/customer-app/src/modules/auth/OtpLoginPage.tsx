import { useState } from 'react';
import { Button, Card, Form, Input, Space, Steps, Typography, message } from 'antd';
import { MobileOutlined, SafetyOutlined } from '@ant-design/icons';
import { useNavigate } from 'react-router-dom';
import { getApiErrorMessage, requestOtp, verifyOtp } from '@/shared/api/customer-app.api';
import { DEFAULT_TENANT_CODE } from '@/shared/api/customer-app.types';
import { useAuthStore } from '@/shared/auth/auth.store';

export function OtpLoginPage() {
  const [step, setStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [phone, setPhone] = useState(import.meta.env.DEV ? '0909123456' : '');
  const [tenantCode, setTenantCode] = useState(DEFAULT_TENANT_CODE);
  const setSession = useAuthStore((s) => s.setSession);
  const navigate = useNavigate();

  const onRequestOtp = async () => {
    const normalized = phone.trim();
    if (normalized.length < 9) {
      message.warning('Nhập số điện thoại hợp lệ');
      return;
    }
    setLoading(true);
    try {
      const res = await requestOtp(normalized, tenantCode.trim());
      message.success(res.message || 'Đã gửi mã OTP');
      setStep(1);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không gửi được OTP'));
    } finally {
      setLoading(false);
    }
  };

  const onVerifyOtp = async (values: { code: string }) => {
    setLoading(true);
    try {
      const data = await verifyOtp(phone.trim(), values.code.trim(), tenantCode.trim());
      setSession(data);
      message.success(`Xin chào ${data.profile.fullName}!`);
      navigate('/', { replace: true });
    } catch {
      message.error('Mã OTP không đúng hoặc đã hết hạn');
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
        padding: 20,
        background: 'linear-gradient(160deg, #ccfbf1 0%, #f0fdfa 45%, #ecfeff 100%)',
      }}
    >
      <Card style={{ width: '100%', maxWidth: 400, borderRadius: 16 }}>
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <div style={{ textAlign: 'center' }}>
            <Typography.Title level={3} style={{ marginBottom: 4, color: '#0f766e' }}>
              App khách hàng
            </Typography.Title>
            <Typography.Text type="secondary">Đăng nhập bằng OTP qua số điện thoại</Typography.Text>
          </div>

          <Steps
            size="small"
            current={step}
            items={[{ title: 'SĐT' }, { title: 'OTP' }]}
          />

          {step === 0 ? (
            <Form layout="vertical" onFinish={onRequestOtp}>
              <Form.Item label="Số điện thoại" required>
                <Input
                  prefix={<MobileOutlined />}
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="0909123456"
                  size="large"
                />
              </Form.Item>
              <Form.Item label="Mã nhà thuốc">
                <Input
                  value={tenantCode}
                  onChange={(e) => setTenantCode(e.target.value)}
                  placeholder={DEFAULT_TENANT_CODE}
                  size="large"
                />
              </Form.Item>
              <Button type="primary" htmlType="submit" block size="large" loading={loading}>
                Gửi mã OTP
              </Button>
            </Form>
          ) : (
            <Form layout="vertical" onFinish={onVerifyOtp}>
              <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
                Mã gửi tới <strong>{phone}</strong>.
                {import.meta.env.DEV ? (
                  <> Dev: dùng <code>000000</code></>
                ) : null}
              </Typography.Paragraph>
              <Form.Item
                name="code"
                label="Mã OTP"
                rules={[{ required: true, message: 'Nhập mã OTP' }]}
              >
                <Input
                  prefix={<SafetyOutlined />}
                  placeholder="000000"
                  size="large"
                  maxLength={6}
                  inputMode="numeric"
                />
              </Form.Item>
              <Button type="primary" htmlType="submit" block size="large" loading={loading}>
                Xác nhận
              </Button>
              <Button type="link" block onClick={() => setStep(0)} style={{ marginTop: 8 }}>
                Đổi số điện thoại
              </Button>
            </Form>
          )}

          {import.meta.env.DEV ? (
            <Typography.Paragraph type="secondary" style={{ margin: 0, fontSize: 12, textAlign: 'center' }}>
              Demo: 0909123456 · {DEFAULT_TENANT_CODE}
            </Typography.Paragraph>
          ) : null}
        </Space>
      </Card>
    </div>
  );
}
