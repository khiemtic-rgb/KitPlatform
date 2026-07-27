import { useCallback, useEffect, useState } from 'react';
import { Spin, message } from 'antd';
import { ArrowLeftOutlined, CloseOutlined, RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  fetchReceivableOrder,
  fetchReceivablesSummary,
  getApiErrorMessage,
} from '@/shared/api/customer-app.api';
import type {
  CustomerPurchaseDetail,
  CustomerReceivableLine,
  CustomerReceivablesSummary,
} from '@/shared/api/customer-app.types';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { formatMoney } from '@/shared/i18n/format-money';
import './ReceivablesPage.css';

function ReceivableOrderDetail({ detail }: { detail: CustomerPurchaseDetail }) {
  const { t } = useTranslation();
  const { paymentMethod } = useCustomerLabels();

  return (
    <>
      <div className="recv-tip">
        <div className="recv-tip-title">{t('receivables.viewOnlyTitle')}</div>
        <div className="recv-tip-sub">{t('receivables.viewOnlyDesc')}</div>
      </div>

      <div className="recv-detail-summary">
        <span className="recv-summary-label">{t('receivables.outstandingThisOrder')}</span>
        <span className="recv-summary-value">{formatMoney(detail.outstanding)}</span>
        <span className="recv-summary-meta">
          {t('receivables.paidAndTotal', {
            paid: formatMoney(detail.amountPaid),
            total: formatMoney(detail.totalAmount),
          })}
        </span>
      </div>

      <div className="recv-detail-lines">
        {detail.items.map((line) => (
          <div key={line.id} className="recv-detail-line">
            <div className="recv-detail-line-title">{line.productName}</div>
            <div className="recv-detail-line-sub">
              {line.quantity} {line.unitName} · {formatMoney(line.lineTotal)}
            </div>
          </div>
        ))}
      </div>

      {detail.payments.length > 0 ? (
        <p className="recv-payments">
          {t('receivables.collected')}:{' '}
          {detail.payments
            .map((p) => `${paymentMethod(p.paymentMethod)}: ${formatMoney(p.amount)}`)
            .join(' · ')}
        </p>
      ) : null}
    </>
  );
}

export function ReceivablesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { online } = useApiHealth();
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [summary, setSummary] = useState<CustomerReceivablesSummary | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<CustomerPurchaseDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [drawerOpen, setDrawerOpen] = useState(false);

  const loadSummary = useCallback(async () => {
    setLoadError(null);
    setLoading(true);
    try {
      setSummary(await fetchReceivablesSummary());
    } catch (error) {
      setLoadError(getApiErrorMessage(error, t('receivables.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadSummary();
  }, [loadSummary]);

  useRetryWhenApiOnline(() => loadSummary());

  const openDetail = async (line: CustomerReceivableLine) => {
    setSelectedId(line.salesOrderId);
    setDrawerOpen(true);
    setDetail(null);
    setDetailLoading(true);
    try {
      setDetail(await fetchReceivableOrder(line.salesOrderId));
    } catch (error) {
      message.error(getApiErrorMessage(error, t('receivables.detailLoadFailed')));
      setDrawerOpen(false);
    } finally {
      setDetailLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="recv-page recv-loading">
        <Spin />
      </div>
    );
  }

  return (
    <div className="recv-page">
      <button type="button" className="recv-back" onClick={() => navigate('/')}>
        <ArrowLeftOutlined />
        {t('common.backHome')}
      </button>

      <h1 className="recv-title">{t('receivables.title')}</h1>
      <p className="recv-intro">{t('receivables.intro')}</p>

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <div className="recv-error">
          {loadError}
          <button type="button" onClick={() => void loadSummary()}>
            {t('common.retry')}
          </button>
        </div>
      ) : null}

      {summary && summary.totalReceivable <= 0.009 ? (
        <div className="recv-empty">{t('receivables.empty')}</div>
      ) : summary ? (
        <>
          <div className="recv-summary">
            <span className="recv-summary-label">{t('receivables.totalOutstanding')}</span>
            <span className="recv-summary-value">{formatMoney(summary.totalReceivable)}</span>
            <span className="recv-summary-meta">
              {t('receivables.openOrdersSummary', { count: summary.openOrderCount })}
            </span>
          </div>

          <div className="recv-list">
            {summary.lines.map((line) => (
              <button
                key={line.salesOrderId}
                type="button"
                className={`recv-card${line.salesOrderId === selectedId ? ' recv-card--active' : ''}`}
                onClick={() => void openDetail(line)}
              >
                <div className="recv-card-main">
                  <div className="recv-card-top">
                    <h2 className="recv-card-title">{line.orderNumber}</h2>
                    <span className="recv-badge">
                      {t('receivables.owed', { amount: formatMoney(line.outstanding) })}
                    </span>
                  </div>
                  <p className="recv-card-sub">
                    {t('receivables.orderDateTotal', {
                      date: dayjs(line.orderDate).format('DD/MM/YYYY'),
                      total: formatMoney(line.orderTotal),
                    })}
                    {line.amountPaid > 0.009
                      ? t('receivables.paidPartial', { amount: formatMoney(line.amountPaid) })
                      : ''}
                  </p>
                </div>
                <RightOutlined className="recv-card-chevron" />
              </button>
            ))}
          </div>
        </>
      ) : null}

      {drawerOpen ? (
        <div
          className="recv-drawer-backdrop"
          role="presentation"
          onClick={() => setDrawerOpen(false)}
        >
          <div
            className="recv-drawer"
            role="dialog"
            aria-modal="true"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="recv-drawer-head">
              <h2 className="recv-drawer-title">
                {detail ? detail.orderNumber : t('receivables.drawerDetail')}
              </h2>
              <button
                type="button"
                className="recv-drawer-close"
                aria-label={t('common.close')}
                onClick={() => setDrawerOpen(false)}
              >
                <CloseOutlined />
              </button>
            </div>
            {detailLoading ? (
              <div className="recv-loading">
                <Spin />
              </div>
            ) : detail ? (
              <ReceivableOrderDetail detail={detail} />
            ) : null}
          </div>
        </div>
      ) : null}
    </div>
  );
}
