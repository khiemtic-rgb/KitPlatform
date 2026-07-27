import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Spin, Tag, message } from 'antd';
import {
  ArrowLeftOutlined,
  BarChartOutlined,
  ClockCircleOutlined,
  GiftOutlined,
  ReloadOutlined,
  RightOutlined,
  SafetyCertificateOutlined,
  StarFilled,
  StarOutlined,
  TagOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  fetchLoyaltySummary,
  fetchLoyaltyTransactions,
  fetchVouchers,
  getApiErrorMessage,
} from '@/shared/api/customer-app.api';
import type {
  CustomerVoucher,
  LoyaltyProgramSummary,
  LoyaltyTransaction,
} from '@/shared/api/customer-app.types';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { formatMoney } from '@/shared/i18n/format-money';
import { formatPoints } from '@/shared/utils/points';
import './LoyaltyPage.css';

type LoyaltyTab = 'overview' | 'history' | 'vouchers';

export function LoyaltyPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { branding } = useCustomerBranding();
  const { loyaltyTx } = useCustomerLabels();
  const [tab, setTab] = useState<LoyaltyTab>('overview');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [program, setProgram] = useState<LoyaltyProgramSummary | null>(null);
  const [transactions, setTransactions] = useState<LoyaltyTransaction[]>([]);
  const [vouchers, setVouchers] = useState<CustomerVoucher[]>([]);

  const loadData = useCallback(
    async (showSpinner = true) => {
      if (showSpinner) setLoading(true);
      else setRefreshing(true);
      try {
        const [summary, tx, voucherList] = await Promise.all([
          fetchLoyaltySummary(),
          fetchLoyaltyTransactions(1, 20),
          fetchVouchers(true),
        ]);
        setProgram(summary.programs[0] ?? null);
        setTransactions(tx.items);
        setVouchers(voucherList.items);
      } catch (error) {
        message.error(getApiErrorMessage(error, t('loyalty.loadFailed')));
      } finally {
        if (showSpinner) setLoading(false);
        else setRefreshing(false);
      }
    },
    [t],
  );

  useEffect(() => {
    void loadData();
  }, [loadData]);

  const tierProgress =
    program?.nextTier && program.currentTier
      ? Math.min(
          100,
          Math.max(
            0,
            Math.round(
              ((program.pointsBalance - program.currentTier.minPoints) /
                Math.max(1, program.nextTier.minPoints - program.currentTier.minPoints)) *
                100,
            ),
          ),
        )
      : program
        ? 100
        : 0;

  const tabs: Array<{ key: LoyaltyTab; label: string; icon: ReactNode }> = [
    { key: 'overview', label: t('loyalty.tabOverview'), icon: <BarChartOutlined /> },
    { key: 'history', label: t('loyalty.tabHistory'), icon: <ClockCircleOutlined /> },
    { key: 'vouchers', label: t('loyalty.tabVouchers'), icon: <TagOutlined /> },
  ];

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  return (
    <div className="loyalty-hub">
      <header className="loyalty-hub-header" style={headerStyle}>
        <div className="loyalty-hub-header-deco" aria-hidden>
          <span className="loyalty-hub-header-coin loyalty-hub-header-coin--1">
            <StarFilled />
          </span>
          <span className="loyalty-hub-header-coin loyalty-hub-header-coin--2">
            <StarFilled />
          </span>
          <span className="loyalty-hub-header-gift" />
        </div>
        <div className="loyalty-hub-header-inner">
          <div className="loyalty-hub-brand">
            <button
              type="button"
              className="loyalty-hub-back"
              aria-label={t('common.back')}
              onClick={() => (window.history.length > 1 ? navigate(-1) : navigate('/'))}
            >
              <ArrowLeftOutlined />
            </button>
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div>
              <div className="loyalty-hub-brand-title">{branding.appName}</div>
              <div className="loyalty-hub-tagline">{branding.tagline || t('loyalty.hubTagline')}</div>
            </div>
          </div>
          <button
            type="button"
            className="loyalty-hub-refresh"
            disabled={loading || refreshing}
            onClick={() => void loadData(false)}
          >
            <ReloadOutlined spin={refreshing} />
            {t('loyalty.refresh')}
          </button>
        </div>
      </header>

      <div className="loyalty-hub-sheet">
        <div className="loyalty-hub-tabs" role="tablist">
          {tabs.map((item) => (
            <button
              key={item.key}
              type="button"
              role="tab"
              aria-selected={tab === item.key}
              className={`loyalty-hub-tab${tab === item.key ? ' loyalty-hub-tab--active' : ''}`}
              onClick={() => setTab(item.key)}
            >
              {item.icon}
              {item.label}
            </button>
          ))}
        </div>

        {loading ? (
          <div className="loyalty-hub-loading">
            <Spin />
          </div>
        ) : null}

        {!loading && tab === 'overview' ? (
          <>
            {program ? (
              <section className="loyalty-hub-card">
                <h1 className="loyalty-hub-card-title">{program.programName}</h1>
                <div className="loyalty-hub-points-row">
                  <div className="loyalty-hub-star">
                    <StarFilled />
                  </div>
                  <div>
                    <div className="loyalty-hub-points-value">
                      <span className="loyalty-hub-points-num">{formatPoints(program.pointsBalance)}</span>
                      <span className="loyalty-hub-points-unit">{t('loyalty.pointsUnit')}</span>
                    </div>
                    <div className="loyalty-hub-lifetime">
                      {t('loyalty.lifetimePoints', { value: formatPoints(program.lifetimePoints) })}
                    </div>
                  </div>
                </div>

                {program.currentTier ? (
                  <div className="loyalty-hub-tier-row">
                    <span className="loyalty-hub-tier-badge">
                      <StarOutlined />
                      {t('loyalty.tier', { name: program.currentTier.tierName })}
                    </span>
                    <span className="loyalty-hub-tier-divider" aria-hidden />
                    {program.nextTier ? (
                      <span className="loyalty-hub-tier-hint">
                        {t('loyalty.pointsToNextPrefix')}{' '}
                        <strong>
                          {formatPoints(Math.max(0, program.nextTier.minPoints - program.pointsBalance))}
                        </strong>{' '}
                        {t('loyalty.pointsToNextSuffix', { tier: program.nextTier.tierName })}
                      </span>
                    ) : (
                      <span className="loyalty-hub-tier-hint">{t('loyalty.topTier')}</span>
                    )}
                  </div>
                ) : null}

                {program.nextTier ? (
                  <div className="loyalty-hub-progress">
                    <div className="loyalty-hub-progress-bar" aria-hidden>
                      <div className="loyalty-hub-progress-fill" style={{ width: `${tierProgress}%` }} />
                    </div>
                    <div className="loyalty-hub-progress-labels">
                      <span>{formatPoints(program.pointsBalance)}</span>
                      <span>{formatPoints(program.nextTier.minPoints)}</span>
                    </div>
                  </div>
                ) : null}
              </section>
            ) : (
              <div className="loyalty-hub-empty">{t('loyalty.noProgram')}</div>
            )}

            <div className="loyalty-hub-features">
              <button type="button" className="loyalty-hub-feature" onClick={() => setTab('vouchers')}>
                <GiftOutlined />
                <span className="loyalty-hub-feature-title">{t('loyalty.featureRedeem')}</span>
                <span className="loyalty-hub-feature-sub">{t('loyalty.featureRedeemSub')}</span>
              </button>
              <div className="loyalty-hub-feature" role="presentation">
                <SafetyCertificateOutlined />
                <span className="loyalty-hub-feature-title">{t('loyalty.featureSafe')}</span>
                <span className="loyalty-hub-feature-sub">{t('loyalty.featureSafeSub')}</span>
              </div>
              <button type="button" className="loyalty-hub-feature" onClick={() => setTab('vouchers')}>
                <StarOutlined />
                <span className="loyalty-hub-feature-title">{t('loyalty.featurePerk')}</span>
                <span className="loyalty-hub-feature-sub">{t('loyalty.featurePerkSub')}</span>
              </button>
            </div>

            <section className="loyalty-hub-promo">
              <div className="loyalty-hub-promo-copy">
                <div className="loyalty-hub-promo-title">{t('loyalty.promoTitle')}</div>
                <div className="loyalty-hub-promo-sub">{t('loyalty.promoSub')}</div>
                <button type="button" className="loyalty-hub-promo-btn" onClick={() => setTab('vouchers')}>
                  {t('loyalty.promoCta')}
                  <RightOutlined />
                </button>
              </div>
                <div className="loyalty-hub-promo-art" aria-hidden>
                  <span className="loyalty-hub-coin loyalty-hub-coin--a">
                    <StarFilled />
                  </span>
                  <div className="loyalty-hub-gift" />
                  <span className="loyalty-hub-ticket" />
                  <span className="loyalty-hub-coin loyalty-hub-coin--b">
                    <StarFilled />
                  </span>
                </div>
            </section>
          </>
        ) : null}

        {!loading && tab === 'history' ? (
          transactions.length === 0 ? (
            <div className="loyalty-hub-empty">{t('loyalty.emptyHistory')}</div>
          ) : (
            <div className="loyalty-hub-list">
              {transactions.map((item) => (
                <div key={item.id} className="loyalty-hub-item">
                  <div className="loyalty-hub-item-top">
                    <span className="loyalty-hub-item-title">
                      {loyaltyTx(item.transactionType) ?? t('loyalty.transaction')}
                    </span>
                    <span
                      className={`loyalty-hub-item-points${
                        item.points >= 0 ? ' loyalty-hub-item-points--plus' : ' loyalty-hub-item-points--minus'
                      }`}
                    >
                      {item.points > 0 ? '+' : ''}
                      {formatPoints(item.points)}
                    </span>
                  </div>
                  <div className="loyalty-hub-item-meta">{item.notes ?? item.programCode}</div>
                  <div className="loyalty-hub-item-meta">{dayjs(item.createdAt).format('DD/MM/YYYY HH:mm')}</div>
                </div>
              ))}
            </div>
          )
        ) : null}

        {!loading && tab === 'vouchers' ? (
          <>
            <p className="loyalty-hub-item-meta" style={{ marginBottom: 12 }}>
              {t('loyalty.voucherIntro')}
            </p>
            {vouchers.length === 0 ? (
              <div className="loyalty-hub-empty">{t('loyalty.emptyVouchers')}</div>
            ) : (
              <div className="loyalty-hub-list">
                {vouchers.map((item) => (
                  <div key={item.customerVoucherId} className="loyalty-hub-item">
                    <div className="loyalty-hub-item-top">
                      <span className="loyalty-hub-item-title">{item.voucherName}</span>
                      {item.isUsed ? (
                        <Tag>{t('loyalty.used')}</Tag>
                      ) : item.isExpired ? (
                        <Tag color="red">{t('loyalty.expired')}</Tag>
                      ) : (
                        <Tag color="green">{t('loyalty.available')}</Tag>
                      )}
                    </div>
                    <div className="loyalty-hub-item-meta">
                      {t('loyalty.code')}: <strong>{item.voucherCode}</strong>
                    </div>
                    <div className="loyalty-hub-item-meta">
                      {t('loyalty.discountLabel')}{' '}
                      {item.discountType === 1 ? `${item.discountValue}%` : formatMoney(item.discountValue)}
                      {item.minOrderAmount > 0
                        ? ` · ${t('loyalty.minOrder', { amount: formatMoney(item.minOrderAmount) })}`
                        : ''}
                    </div>
                    <div className="loyalty-hub-item-meta">
                      {t('loyalty.validTo')}: {dayjs(item.validTo).format('DD/MM/YYYY')}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </>
        ) : null}
      </div>
    </div>
  );
}
