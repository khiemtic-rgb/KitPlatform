import { useQuery } from '@tanstack/react-query';
import { Button, Card, Input, Space, Typography, message } from 'antd';
import { fetchMe } from '@/shared/api/partner-portal.api';
import { useAuthStore } from '@/shared/auth/auth.store';

export function ReferralPage() {
  const cached = useAuthStore((s) => s.partner);
  const { data = cached } = useQuery({ queryKey: ['partner-me'], queryFn: fetchMe });

  if (!data) return null;

  return (
    <div>
      <Typography.Title level={3}>Link & QR giới thiệu</Typography.Title>
      <Typography.Paragraph type="secondary">
        Gửi link hoặc QR này cho nhà thuốc. Mọi khảo sát từ link sẽ gắn về tài khoản <strong>{data.code}</strong>.
      </Typography.Paragraph>
      <Card>
        <Space direction="vertical" style={{ width: '100%' }} size="middle">
          <div>
            <Typography.Text type="secondary">Link khảo sát</Typography.Text>
            <Input.TextArea value={data.referralUrl} autoSize readOnly style={{ marginTop: 8 }} />
            <Button
              type="primary"
              style={{ marginTop: 8, background: '#0f766e' }}
              onClick={async () => {
                await navigator.clipboard.writeText(data.referralUrl);
                message.success('Đã sao chép link');
              }}
            >
              Sao chép link
            </Button>
          </div>
          <div>
            <Typography.Text type="secondary">QR Code</Typography.Text>
            <div style={{ marginTop: 12 }}>
              <img src={data.qrUrl} alt="QR referral" width={240} height={240} style={{ borderRadius: 8, background: '#fff' }} />
            </div>
          </div>
        </Space>
      </Card>
    </div>
  );
}
