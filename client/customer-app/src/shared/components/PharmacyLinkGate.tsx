import { useEffect, type ReactNode } from 'react';
import { ShopOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { usePharmacyLink } from '@/shared/config/PharmacyLinkProvider';
import './PharmacyLinkSheet.css';

/** Route wrapper: commerce pages cần partner link. */
export function PharmacyLinkRequired({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { linked, openLinkSheet, linkNow, tenantCode, paused } = usePharmacyLink();

  useEffect(() => {
    if (!linked) {
      openLinkSheet(t('pharmacyLink.intentServices'));
    }
  }, [linked, openLinkSheet, t]);

  if (linked) return <>{children}</>;

  return (
    <div className="pls-gate-page">
      <div className="pls-icon" style={{ margin: '0 auto' }} aria-hidden>
        <ShopOutlined />
      </div>
      <h1>{t('pharmacyLink.title')}</h1>
      <p>{paused ? t('pharmacyLink.bodyPaused') : t('pharmacyLink.body')}</p>
      <div className="pls-gate-actions">
        <button
          type="button"
          className="pls-btn pls-btn--primary"
          onClick={() => {
            if (paused) {
              linkNow(tenantCode);
              return;
            }
            // Prospect / chưa member: bắt buộc QR quầy — không one-click invite.
            navigate(tenantCode ? `/rx?tenant=${encodeURIComponent(tenantCode)}` : '/rx');
          }}
        >
          {paused ? t('pharmacyLink.resume') : t('pharmacyLink.scanQr')}
        </button>
        <button type="button" className="pls-btn pls-btn--ghost" onClick={() => navigate('/')}>
          {t('common.backHome')}
        </button>
      </div>
    </div>
  );
}

/** Banner nhẹ trên màn care vẫn mở khi chưa link. */
export function PharmacyLinkSoftBanner() {
  const { t } = useTranslation();
  const { linked, openLinkSheet, paused, partnerName } = usePharmacyLink();
  if (linked) return null;

  return (
    <div className="pls-banner" role="status">
      <div>
        <strong>{t('pharmacyLink.bannerTitle')}</strong>
        <span>
          {paused
            ? t('pharmacyLink.bannerPaused', { name: partnerName || '—' })
            : t('pharmacyLink.bannerBody')}
        </span>
        <button
          type="button"
          className="pls-banner-cta"
          onClick={() => openLinkSheet(t('pharmacyLink.intentSync'))}
        >
          {paused ? t('pharmacyLink.resume') : t('pharmacyLink.bannerCta')}
        </button>
      </div>
    </div>
  );
}
