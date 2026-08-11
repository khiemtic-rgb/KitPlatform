import { useCallback, useEffect, useState, type CSSProperties } from 'react';
import { Spin, message } from 'antd';
import {
  ArrowLeftOutlined,
  BellOutlined,
  CameraOutlined,
  FileTextOutlined,
  GiftOutlined,
  HistoryOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  PhoneOutlined,
  RightOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  confirmPharmacyLink,
  fetchDraftOrders,
  fetchLoyaltySummary,
  getApiErrorMessage,
} from '@/shared/api/customer-app.api';
import { CUSTOMER_DRAFT_ORDER_STATUS } from '@/shared/api/customer-app.types';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { PharmacyLinkSoftBanner } from '@/shared/components/PharmacyLinkGate';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { usePharmacyLink } from '@/shared/config/PharmacyLinkProvider';
import { formatPoints } from '@/shared/utils/points';
import './PharmacyHubPage.css';

export function PharmacyHubPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const claimCode = searchParams.get('claim')?.trim() || searchParams.get('code')?.trim() || '';
  const { branding } = useCustomerBranding();
  const { linked, requireLink, partnerName } = usePharmacyLink();
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated());
  const setProfile = useAuthStore((s) => s.setProfile);
  const [loading, setLoading] = useState(true);
  const [points, setPoints] = useState(0);
  const [pendingOrders, setPendingOrders] = useState(0);

  const load = useCallback(async () => {
    if (!linked || !useAuthStore.getState().isAuthenticated()) {
      setLoading(false);
      setPoints(0);
      setPendingOrders(0);
      return;
    }
    setLoading(true);
    try {
      const [loyalty, drafts] = await Promise.all([fetchLoyaltySummary(), fetchDraftOrders()]);
      const program = loyalty.programs[0];
      setPoints(program?.pointsBalance ?? 0);
      setPendingOrders(
        drafts.filter(
          (d) =>
            d.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
            d.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
        ).length,
      );
    } catch (error) {
      message.error(getApiErrorMessage(error, t('pharmacy.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [linked, t]);

  useEffect(() => {
    void load();
  }, [load]);

  // QR deep-link: auto-confirm pharmacy membership once claim code is present.
  useEffect(() => {
    if (!isAuthenticated || !claimCode) return;
    let cancelled = false;
    void (async () => {
      try {
        const profile = await confirmPharmacyLink('qr_scan');
        if (!cancelled) {
          setProfile(profile);
          message.success(t('pharmacyLink.claimSuccess', { defaultValue: 'Đã liên kết nhà thuốc' }));
        }
      } catch (error) {
        if (!cancelled) {
          message.error(
            getApiErrorMessage(error, t('pharmacyLink.claimFailed', { defaultValue: 'Không liên kết được nhà thuốc' })),
          );
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [claimCode, isAuthenticated, setProfile, t]);

  const phone = branding.supportPhone?.replace(/\s/g, '') ?? '';

  const claimPharmacyLink = () => {
    if (isAuthenticated) {
      void confirmPharmacyLink('qr_scan')
        .then((profile) => {
          setProfile(profile);
          message.success(t('pharmacyLink.claimSuccess', { defaultValue: 'Đã liên kết nhà thuốc' }));
        })
        .catch((error) => {
          message.error(
            getApiErrorMessage(error, t('pharmacyLink.claimFailed', { defaultValue: 'Không liên kết được nhà thuốc' })),
          );
        });
    }
    navigate(`/health?add=prescription&code=${encodeURIComponent(claimCode)}`);
  };

  const goPartner = (to: string, intent: string) => {
    if (!requireLink(intent)) return;
    navigate(to);
  };

  const primaryActions = [
    {
      key: 'add',
      needsLink: false,
      intent: '',
      to: '/health?add=prescription',
      icon: <CameraOutlined />,
      tone: 'pharmacy-hub-action-icon--blue',
      title: t('pharmacy.addPrescription'),
      sub: t('pharmacy.addPrescriptionSub'),
    },
    {
      key: 'schedule',
      needsLink: false,
      intent: '',
      to: '/medications',
      icon: <MedicineBoxOutlined />,
      tone: 'pharmacy-hub-action-icon--amber',
      title: t('pharmacy.medSchedule'),
      sub: t('pharmacy.medScheduleSub'),
    },
    {
      key: 'reminders',
      needsLink: false,
      intent: '',
      to: '/reminders',
      icon: <BellOutlined />,
      tone: '',
      title: t('pharmacy.reminders'),
      sub: t('pharmacy.remindersSub'),
    },
    {
      key: 'timeline',
      needsLink: false,
      intent: '',
      to: '/timeline',
      icon: <HistoryOutlined />,
      tone: '',
      title: t('pharmacy.careTimeline'),
      sub: t('pharmacy.careTimelineSub'),
    },
  ] as const;

  const secondaryActions = [
    {
      key: 'orders',
      needsLink: true,
      intent: t('pharmacyLink.intentOrders'),
      to: '/orders',
      icon: <FileTextOutlined />,
      title: t('pharmacy.myPrescriptions'),
      sub:
        pendingOrders > 0
          ? t('pharmacy.myPrescriptionsPending', { count: pendingOrders })
          : t('pharmacy.myPrescriptionsSub'),
    },
    {
      key: 'chat',
      needsLink: true,
      intent: t('pharmacyLink.intentChat'),
      to: '/chat',
      icon: <MessageOutlined />,
      title: t('pharmacy.chatPharmacist'),
      sub: t('pharmacy.chatPharmacistSub'),
    },
    {
      key: 'reserve',
      needsLink: true,
      intent: t('pharmacyLink.intentReserve'),
      to: '/reservations',
      icon: <ShoppingCartOutlined />,
      title: t('pharmacy.reserveMed'),
      sub: t('pharmacy.reserveMedSub'),
    },
    {
      key: 'loyalty',
      needsLink: true,
      intent: t('pharmacyLink.intentLoyalty'),
      to: '/loyalty',
      icon: <GiftOutlined />,
      title: t('pharmacy.pointsAndVouchers'),
      sub: t('pharmacy.pointsAndVouchersSub'),
    },
  ] as const;

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
          <p className="pharmacy-hub-hero-kicker">{t('pharmacy.hubKicker')}</p>
          <h1 className="pharmacy-hub-hero-name">{t('pharmacy.hubTitle')}</h1>
          <p className="pharmacy-hub-hero-tag">{branding.tagline || t('pharmacy.hubTagline')}</p>
        </div>
      </section>

      <PharmacyLinkSoftBanner />

      {claimCode ? (
        <div className="pharmacy-hub-claim" role="status">
          <strong>{t('pharmacy.claimTitle')}</strong>
          <span>{t('pharmacy.claimBody', { code: claimCode })}</span>
          <button type="button" className="pharmacy-hub-claim-cta" onClick={claimPharmacyLink}>
            {t('pharmacy.claimCta')}
          </button>
        </div>
      ) : null}

      {loading ? (
        <div className="pharmacy-hub-loading">
          <Spin />
        </div>
      ) : (
        <div className="pharmacy-hub-stats">
          <button
            type="button"
            className={`pharmacy-hub-stat${!linked ? ' pharmacy-hub-stat--gated' : ''}`}
            onClick={() => goPartner('/orders', t('pharmacyLink.intentOrders'))}
          >
            <span className="pharmacy-hub-stat-label">{t('pharmacy.openOrders')}</span>
            <span className="pharmacy-hub-stat-value">{linked ? pendingOrders : '—'}</span>
            <span className="pharmacy-hub-stat-meta">{t('pharmacy.openOrdersMeta')}</span>
          </button>
          <button
            type="button"
            className={`pharmacy-hub-stat${!linked ? ' pharmacy-hub-stat--gated' : ''}`}
            onClick={() => goPartner('/loyalty', t('pharmacyLink.intentLoyalty'))}
          >
            <span className="pharmacy-hub-stat-label">{t('pharmacy.points')}</span>
            <span className="pharmacy-hub-stat-value">{linked ? formatPoints(points) : '—'}</span>
            <span className="pharmacy-hub-stat-link">{t('pharmacy.viewOffers')}</span>
          </button>
        </div>
      )}

      <h2 className="pharmacy-hub-section-title">{t('pharmacy.careActions')}</h2>
      <div className="pharmacy-hub-actions">
        {primaryActions.map((action) => (
          <button
            key={action.key}
            type="button"
            className={`pharmacy-hub-action${action.needsLink && !linked ? ' pharmacy-hub-action--gated' : ''}`}
            onClick={() => {
              if (action.needsLink) {
                goPartner(action.to, action.intent);
                return;
              }
              navigate(action.to);
            }}
          >
            <span className={`pharmacy-hub-action-icon ${action.tone}`.trim()} aria-hidden>
              {action.icon}
            </span>
            <span className="pharmacy-hub-action-copy">
              <span className="pharmacy-hub-action-title">{action.title}</span>
              <span className="pharmacy-hub-action-sub">{action.sub}</span>
            </span>
            <RightOutlined className="pharmacy-hub-action-chevron" />
          </button>
        ))}
      </div>

      <h2 className="pharmacy-hub-section-title">{t('pharmacy.partnerActions')}</h2>
      <div className="pharmacy-hub-actions pharmacy-hub-actions--secondary">
        {secondaryActions.map((action) => (
          <button
            key={action.key}
            type="button"
            className={`pharmacy-hub-action pharmacy-hub-action--compact${
              action.needsLink && !linked ? ' pharmacy-hub-action--gated' : ''
            }`}
            onClick={() => {
              if (action.needsLink) {
                goPartner(action.to, action.intent);
                return;
              }
              navigate(action.to);
            }}
          >
            <span className="pharmacy-hub-action-icon" aria-hidden>
              {action.icon}
            </span>
            <span className="pharmacy-hub-action-copy">
              <span className="pharmacy-hub-action-title">{action.title}</span>
              <span className="pharmacy-hub-action-sub">{action.sub}</span>
            </span>
            <RightOutlined className="pharmacy-hub-action-chevron" />
          </button>
        ))}

        {phone ? (
          <a
            className={`pharmacy-hub-action pharmacy-hub-action--compact${!linked ? ' pharmacy-hub-action--gated' : ''}`}
            href={linked ? `tel:${phone}` : undefined}
            onClick={(e) => {
              if (!requireLink(t('pharmacyLink.intentCall'))) {
                e.preventDefault();
              }
            }}
          >
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
        ) : null}
      </div>

      <p className="pharmacy-hub-partner-note">
        {linked
          ? t('pharmacy.partnerNote', {
              name: partnerName || branding.tenantName || t('branding.defaultTenantName'),
            })
          : t('pharmacy.partnerNoteUnlinked')}
      </p>
    </div>
  );
}
