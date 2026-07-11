import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Col, Row, Space, Spin, Tag, Typography, message } from 'antd';
import {
  MessageOutlined,
  PhoneOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { fetchLoyaltySummary, fetchVouchers, getApiErrorMessage } from '@/shared/api/customer-app.api';
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { formatPoints } from '@/shared/utils/points';

export function PharmacyHubPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { branding } = useCustomerBranding();
  const [loading, setLoading] = useState(true);
  const [points, setPoints] = useState(0);
  const [tierName, setTierName] = useState<string | null>(null);
  const [voucherCount, setVoucherCount] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [loyalty, vouchers] = await Promise.all([fetchLoyaltySummary(), fetchVouchers(true)]);
      const program = loyalty.programs[0];
      setPoints(program?.pointsBalance ?? 0);
      setTierName(program?.currentTier?.tierName ?? null);
      setVoucherCount(vouchers.items.filter((v) => !v.isUsed).length);
    } catch (error) {
      message.error(getApiErrorMessage(error, t('pharmacy.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const phone = branding.supportPhone?.replace(/\s/g, '') ?? '';

  return (
    <div>
      <BackToHomeButton />
      <Card
        style={{
          borderRadius: 16,
          marginBottom: 16,
          background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
          border: 'none',
        }}
        styles={{ body: { padding: 20, color: '#fff' } }}
      >
        <Space align="start" style={{ width: '100%', justifyContent: 'space-between' }}>
          <Space align="start" size={12}>
            <BrandingLogo logoUrl={branding.logoUrl} size={48} style={{ background: 'rgba(255,255,255,0.95)' }} />
            <div>
              <Typography.Title level={4} style={{ color: '#fff', margin: 0 }}>
                {branding.tenantName}
              </Typography.Title>
              {branding.tagline ? (
                <Typography.Text style={{ color: 'rgba(255,255,255,0.9)', fontSize: 13 }}>
                  {branding.tagline}
                </Typography.Text>
              ) : null}
            </div>
          </Space>
        </Space>
      </Card>

      {loading ? (
        <div style={{ textAlign: 'center', padding: 32 }}>
          <Spin />
        </div>
      ) : (
        <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
          <Col span={12}>
            <Card size="small" style={{ borderRadius: 12 }}>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('pharmacy.points')}
              </Typography.Text>
              <div>
                <Typography.Text strong style={{ fontSize: 20, color: branding.primaryColor }}>
                  {formatPoints(points)}
                </Typography.Text>
              </div>
              {tierName ? <Tag color="gold">{t('pharmacy.tier', { name: tierName })}</Tag> : null}
            </Card>
          </Col>
          <Col span={12}>
            <Card size="small" style={{ borderRadius: 12 }}>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('pharmacy.vouchers')}
              </Typography.Text>
              <div>
                <Typography.Text strong style={{ fontSize: 20 }}>
                  {voucherCount}
                </Typography.Text>
              </div>
              <Link to="/loyalty">{t('pharmacy.viewOffers')}</Link>
            </Card>
          </Col>
        </Row>
      )}

      <Space direction="vertical" style={{ width: '100%' }} size={10}>
        <Button block size="large" icon={<ShoppingCartOutlined />} onClick={() => navigate('/reservations')}>
          {t('pharmacy.reserveMed')}
        </Button>
        <Button block size="large" icon={<MessageOutlined />} onClick={() => navigate('/chat')}>
          {t('pharmacy.chatPharmacist')}
        </Button>
        {phone ? (
          <Button block size="large" icon={<PhoneOutlined />} href={`tel:${phone}`}>
            {t('pharmacy.callSupport', { phone: branding.supportPhone })}
          </Button>
        ) : (
          <Button block size="large" icon={<PhoneOutlined />} disabled>
            {t('pharmacy.noSupportPhone')}
          </Button>
        )}
        <Button block onClick={() => navigate('/orders')}>
          {t('pharmacy.ordersAndReorder')}
        </Button>
        <Button block onClick={() => navigate('/loyalty')}>
          {t('pharmacy.pointsAndVouchers')}
        </Button>
      </Space>
    </div>
  );
}
