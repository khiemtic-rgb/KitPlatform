import { useCallback, useEffect, useState, type CSSProperties } from 'react';
import { Spin, message } from 'antd';
import {
  ArrowLeftOutlined,
  GiftOutlined,
  MessageOutlined,
  PhoneOutlined,
  RightOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { fetchLoyaltySummary, fetchVouchers, getApiErrorMessage } from '@/shared/api/customer-app.api';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { formatPoints } from '@/shared/utils/points';
import './PharmacyHubPage.css';

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
    <div
      className="pharmacy-hub"
      style={
        {
          '--ph-primary': branding.primaryColor,
          '--ph-secondary': branding.secondaryColor,
        } as CSSProperties
      }
    >
      <button type="button" className="pharmacy-hub-back" onClick={() => navigate('/')}>
        <ArrowLeftOutlined />
        {t('common.backHome')}
      </button>

      <section className="pharmacy-hub-hero">
        <BrandingLogo logoUrl={branding.logoUrl} size={48} style={{ background: 'rgba(255,255,255,0.95)' }} />
        <div className="pharmacy-hub-hero-copy">
          <h1 className="pharmacy-hub-hero-name">{branding.tenantName}</h1>
          <p className="pharmacy-hub-hero-tag">
            {branding.tagline || t('pharmacy.hubTagline')}
          </p>
        </div>
      </section>

      {loading ? (
        <div className="pharmacy-hub-loading">
          <Spin />
        </div>
      ) : (
        <div className="pharmacy-hub-stats">
          <div className="pharmacy-hub-stat">
            <span className="pharmacy-hub-stat-label">{t('pharmacy.points')}</span>
            <span className="pharmacy-hub-stat-value">{formatPoints(points)}</span>
            {tierName ? (
              <span className="pharmacy-hub-stat-meta">{t('pharmacy.tier', { name: tierName })}</span>
            ) : null}
          </div>
          <div className="pharmacy-hub-stat">
            <span className="pharmacy-hub-stat-label">{t('pharmacy.vouchers')}</span>
            <span className="pharmacy-hub-stat-value">{voucherCount}</span>
            <Link className="pharmacy-hub-stat-link" to="/loyalty">
              {t('pharmacy.viewOffers')}
            </Link>
          </div>
        </div>
      )}

      <div className="pharmacy-hub-actions">
        <button type="button" className="pharmacy-hub-action" onClick={() => navigate('/reservations')}>
          <span className="pharmacy-hub-action-icon" aria-hidden>
            <ShoppingCartOutlined />
          </span>
          <span className="pharmacy-hub-action-copy">
            <span className="pharmacy-hub-action-title">{t('pharmacy.reserveMed')}</span>
            <span className="pharmacy-hub-action-sub">{t('pharmacy.reserveMedSub')}</span>
          </span>
          <RightOutlined className="pharmacy-hub-action-chevron" />
        </button>

        <button type="button" className="pharmacy-hub-action" onClick={() => navigate('/chat')}>
          <span className="pharmacy-hub-action-icon pharmacy-hub-action-icon--blue" aria-hidden>
            <MessageOutlined />
          </span>
          <span className="pharmacy-hub-action-copy">
            <span className="pharmacy-hub-action-title">{t('pharmacy.chatPharmacist')}</span>
            <span className="pharmacy-hub-action-sub">{t('pharmacy.chatPharmacistSub')}</span>
          </span>
          <RightOutlined className="pharmacy-hub-action-chevron" />
        </button>

        {phone ? (
          <a className="pharmacy-hub-action" href={`tel:${phone}`}>
            <span className="pharmacy-hub-action-icon pharmacy-hub-action-icon--amber" aria-hidden>
              <PhoneOutlined />
            </span>
            <span className="pharmacy-hub-action-copy">
              <span className="pharmacy-hub-action-title">
                {t('pharmacy.callSupport', { phone: branding.supportPhone })}
              </span>
              <span className="pharmacy-hub-action-sub">{t('pharmacy.callSupportSub')}</span>
            </span>
            <RightOutlined className="pharmacy-hub-action-chevron" />
          </a>
        ) : (
          <button type="button" className="pharmacy-hub-action" disabled>
            <span className="pharmacy-hub-action-icon pharmacy-hub-action-icon--amber" aria-hidden>
              <PhoneOutlined />
            </span>
            <span className="pharmacy-hub-action-copy">
              <span className="pharmacy-hub-action-title">{t('pharmacy.noSupportPhone')}</span>
            </span>
          </button>
        )}

        <button type="button" className="pharmacy-hub-action" onClick={() => navigate('/orders')}>
          <span className="pharmacy-hub-action-icon" aria-hidden>
            <ShoppingCartOutlined />
          </span>
          <span className="pharmacy-hub-action-copy">
            <span className="pharmacy-hub-action-title">{t('pharmacy.ordersAndReorder')}</span>
            <span className="pharmacy-hub-action-sub">{t('pharmacy.ordersAndReorderSub')}</span>
          </span>
          <RightOutlined className="pharmacy-hub-action-chevron" />
        </button>

        <button type="button" className="pharmacy-hub-action" onClick={() => navigate('/loyalty')}>
          <span className="pharmacy-hub-action-icon" aria-hidden>
            <GiftOutlined />
          </span>
          <span className="pharmacy-hub-action-copy">
            <span className="pharmacy-hub-action-title">{t('pharmacy.pointsAndVouchers')}</span>
            <span className="pharmacy-hub-action-sub">{t('pharmacy.pointsAndVouchersSub')}</span>
          </span>
          <RightOutlined className="pharmacy-hub-action-chevron" />
        </button>
      </div>
    </div>
  );
}
