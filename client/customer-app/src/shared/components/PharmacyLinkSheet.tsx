import { ShopOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { usePharmacyLinkOptional } from '@/shared/config/PharmacyLinkProvider';
import { DEFAULT_TENANT_CODE, loadStoredTenantCode } from '@/shared/config/app-brand';
import './PharmacyLinkSheet.css';

export function PharmacyLinkSheet() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const link = usePharmacyLinkOptional();
  if (!link) return null;

  const { sheetOpen, closeLinkSheet, linkNow, sheetIntent, paused, tenantCode, partnerName } =
    link;

  if (!sheetOpen) return null;

  const suggested =
    tenantCode || loadStoredTenantCode() || DEFAULT_TENANT_CODE || '';

  const onLinkExisting = () => {
    // Chỉ resume member đã pause; không tự xác nhận prospect.
    if (paused && suggested) {
      linkNow(suggested);
      return;
    }
    onScanQr();
  };

  const onScanQr = () => {
    closeLinkSheet();
    navigate(suggested ? `/rx?tenant=${encodeURIComponent(suggested)}` : '/rx');
  };

  const onEnterCode = () => {
    closeLinkSheet();
    navigate('/login');
  };

  return (
    <div className="pls-root" role="presentation" onClick={closeLinkSheet}>
      <div
        className="pls-sheet"
        role="dialog"
        aria-modal="true"
        aria-labelledby="pls-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="pls-handle" aria-hidden />
        <div className="pls-icon" aria-hidden>
          <ShopOutlined />
        </div>
        <h2 id="pls-title" className="pls-title">
          {t('pharmacyLink.title')}
        </h2>
        <p className="pls-body">
          {sheetIntent
            ? t('pharmacyLink.bodyWithIntent', { intent: sheetIntent })
            : paused
              ? t('pharmacyLink.bodyPaused')
              : t('pharmacyLink.body')}
        </p>
        {(partnerName || suggested) && (
          <p className="pls-partner">
            {t('pharmacyLink.knownPartner', {
              name: partnerName || suggested,
            })}
          </p>
        )}

        <div className="pls-actions">
          {paused && suggested ? (
            <button type="button" className="pls-btn pls-btn--primary" onClick={onLinkExisting}>
              {t('pharmacyLink.resume')}
            </button>
          ) : null}
          <button type="button" className="pls-btn pls-btn--primary" onClick={onScanQr}>
            {t('pharmacyLink.scanQr')}
          </button>
          {!suggested ? (
            <button type="button" className="pls-btn pls-btn--ghost" onClick={onEnterCode}>
              {t('pharmacyLink.enterCode')}
            </button>
          ) : null}
          <button type="button" className="pls-btn pls-btn--ghost" onClick={closeLinkSheet}>
            {t('pharmacyLink.later')}
          </button>
        </div>
      </div>
    </div>
  );
}
