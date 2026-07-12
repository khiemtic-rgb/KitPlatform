import { BankOutlined, CalendarOutlined, MedicineBoxOutlined, ShopOutlined } from '@ant-design/icons';
import { Button, Card, Space, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import type { CustomerConnectInbox, CustomerConnectInboxItem } from '@/shared/api/customer-app.api';

function kindIcon(kind: string) {
  if (kind === 'rx_ready') return <ShopOutlined />;
  if (kind.startsWith('booking_')) return <CalendarOutlined />;
  if (kind.startsWith('referral_')) return <BankOutlined />;
  return <MedicineBoxOutlined />;
}

function formatWhen(iso?: string, locale?: string) {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return d.toLocaleString(locale === 'en-US' ? 'en-US' : 'vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

type Props = {
  inbox: CustomerConnectInbox | null;
  loading?: boolean;
};

export function ConnectCarePanel({ inbox, loading }: Props) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();

  if (!inbox?.connectEnabled) return null;
  if (!loading && inbox.items.length === 0) return null;

  return (
    <Card
      size="small"
      style={{ borderRadius: 12, border: '1px solid #bae6fd' }}
      styles={{ body: { padding: '12px 14px' } }}
    >
      <Typography.Text strong style={{ display: 'block', marginBottom: 10 }}>
        {t('home.connectTitle')}
      </Typography.Text>
      {loading ? (
        <Typography.Text type="secondary">…</Typography.Text>
      ) : (
        <Space direction="vertical" size={10} style={{ width: '100%' }}>
          {inbox.items.slice(0, 5).map((item) => (
            <ConnectCareItem
              key={`${item.kind}-${item.sourceId}`}
              item={item}
              locale={i18n.language}
              onPickup={() => navigate('/pharmacy')}
            />
          ))}
        </Space>
      )}
    </Card>
  );
}

function ConnectCareItem({
  item,
  locale,
  onPickup,
}: {
  item: CustomerConnectInboxItem;
  locale: string;
  onPickup: () => void;
}) {
  const { t } = useTranslation();
  const titleKey = `home.connectKinds.${item.kind}` as const;
  const when = formatWhen(item.scheduledAt, locale);

  return (
    <div
      style={{
        display: 'flex',
        gap: 10,
        alignItems: 'flex-start',
        padding: '8px 0',
        borderTop: '1px solid rgba(0,0,0,0.06)',
      }}
    >
      <span style={{ color: '#0369a1', marginTop: 2 }}>{kindIcon(item.kind)}</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <Typography.Text strong style={{ display: 'block', fontSize: 13 }}>
          {t(titleKey)}
        </Typography.Text>
        {item.clinicName ? (
          <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
            {item.clinicName}
          </Typography.Text>
        ) : null}
        {item.detail ? (
          <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
            {item.detail}
          </Typography.Text>
        ) : null}
        {when ? (
          <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
            {t('home.connectScheduled', { when })}
          </Typography.Text>
        ) : null}
        {item.ctaKey === 'pickup' ? (
          <Button type="link" size="small" style={{ paddingInline: 0, height: 'auto' }} onClick={onPickup}>
            {t('home.connectCtaPickup')}
          </Button>
        ) : (
          <Typography.Text type="secondary" style={{ fontSize: 11 }}>
            {t('home.connectCtaInfo')}
          </Typography.Text>
        )}
      </div>
    </div>
  );
}
