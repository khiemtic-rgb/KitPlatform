import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Popconfirm, Spin, message } from 'antd';
import {
  ClockCircleOutlined,
  FileTextOutlined,
  RightOutlined,
  SafetyCertificateOutlined,
  ShoppingCartOutlined,
  ShoppingOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  confirmDraftOrder,
  cancelReservation,
  fetchDraftOrder,
  fetchPurchase,
  fetchReservation,
  getApiErrorMessage,
  hideDraftOrder,
  cancelDraftOrder,
} from '@/shared/api/customer-app.api';
import { useOrdersOverviewQuery } from '@/shared/api/overview-queries';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  CUSTOMER_PURCHASE_STATUS,
  CUSTOMER_RESERVATION_STATUS,
  type CustomerDraftOrder,
  type CustomerDraftOrderListItem,
  type CustomerPurchaseDetail,
  type CustomerPurchaseListItem,
  type CustomerReservationDetail,
  type CustomerReservationListItem,
} from '@/shared/api/customer-app.types';
import { useCustomerLabels } from '@/shared/i18n/useCustomerLabels';
import { ListCardSkeleton } from '@/shared/components/ListCardSkeleton';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { subscribeDraftOrderAlerts } from '@/shared/hooks/draft-order-alert-bus';
import {
  filterUnseenSentDrafts,
  markSentDraftsSeen,
} from '@/shared/hooks/draft-order-seen';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { formatMoney } from '@/shared/i18n/format-money';
import './OrdersHub.css';

type HubTab = 'placed' | 'reservations' | 'purchased';
type DetailMode = null | { kind: 'draft' | 'purchase' | 'reservation'; id: string };

function formatOrderDate(iso: string) {
  return dayjs(iso).format('DD/MM/YYYY');
}

function isPlacedOrder(status: number): boolean {
  return (
    status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Cancelled ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Expired
  );
}

function purchaseStatusLabel(
  item: CustomerPurchaseListItem,
  labels: { purchaseStatus: (n: number) => string; partialRefund: string },
): { label: string; tone: string } {
  if (
    item.status === CUSTOMER_PURCHASE_STATUS.Completed &&
    item.totalRefunded > 0.0001
  ) {
    return { label: labels.partialRefund, tone: 'orange' };
  }
  return {
    label: labels.purchaseStatus(item.status) ?? String(item.status),
    tone: item.status === CUSTOMER_PURCHASE_STATUS.Refunded ? 'warning' : 'success',
  };
}

function PurchaseDetailPanel({ detail }: { detail: CustomerPurchaseDetail }) {
  const { t } = useTranslation();
  const { purchaseStatus, paymentMethod } = useCustomerLabels();
  const status = purchaseStatusLabel(
    {
      id: detail.id,
      orderNumber: detail.orderNumber,
      status: detail.status,
      orderDate: detail.orderDate,
      totalAmount: detail.totalAmount,
      amountPaid: detail.amountPaid,
      outstanding: detail.outstanding,
      itemCount: detail.items.length,
      totalRefunded: detail.totalRefunded,
    },
    { purchaseStatus, partialRefund: t('orders.partialRefund') },
  );

  return (
    <article className="orders-hub-panel">
      <h2 className="orders-hub-panel-title">
        {t('ordersDetail.invoiceTitle', { number: detail.orderNumber })}
      </h2>
      <div className="orders-hub-panel-meta">
        <span className={`orders-hub-status orders-hub-status--${status.tone}`}>{status.label}</span>
        <span className="orders-hub-panel-time">{dayjs(detail.orderDate).format('DD/MM/YYYY HH:mm')}</span>
      </div>

      <div className="orders-hub-lines">
        {detail.items.map((line) => (
          <div key={line.id} className="orders-hub-line">
            <div className="orders-hub-line-title">{line.productName}</div>
            <div className="orders-hub-line-sub">
              {line.quantity} {line.unitName} · {formatMoney(line.lineTotal)}
            </div>
            {line.returnedQuantity > 0 ? (
              <div className="orders-hub-line-warn">
                {t('ordersDetail.returnedQty', { qty: line.returnedQuantity, unit: line.unitName })}
              </div>
            ) : null}
          </div>
        ))}
      </div>

      <div className="orders-hub-totals">
        <div>
          {t('ordersDetail.totalPayment')}: <strong>{formatMoney(detail.totalAmount)}</strong>
        </div>
        {detail.discountAmount > 0 ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.discount')}: {formatMoney(detail.discountAmount)}
          </div>
        ) : null}
        {detail.loyaltyDiscountAmount > 0 ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.loyaltyDiscount')}: {formatMoney(detail.loyaltyDiscountAmount)}
          </div>
        ) : null}
        {detail.voucherDiscountAmount > 0 && detail.voucherCode ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.voucherDiscount', { code: detail.voucherCode })}:{' '}
            -{formatMoney(detail.voucherDiscountAmount)}
          </div>
        ) : null}
        {detail.loyaltyPointsEarned ? (
          <div className="orders-hub-totals-ok">
            {t('ordersDetail.loyaltyPointsEarned', { points: detail.loyaltyPointsEarned })}
          </div>
        ) : null}
        {detail.payments.length > 0 ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.payment')}:{' '}
            {detail.payments
              .map((p) => `${paymentMethod(p.paymentMethod)}: ${formatMoney(p.amount)}`)
              .join(' · ')}
          </div>
        ) : null}
        {detail.outstanding > 0.009 ? (
          <>
            <div>
              {t('ordersDetail.paid')}: <strong>{formatMoney(detail.amountPaid)}</strong>
            </div>
            <div className="orders-hub-totals-danger">
              {t('ordersDetail.outstanding')}: <strong>{formatMoney(detail.outstanding)}</strong>
            </div>
            <div className="orders-hub-totals-muted">{t('ordersDetail.payAtCounter')}</div>
          </>
        ) : null}
        {detail.notes ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.notes')}: {detail.notes}
          </div>
        ) : null}
      </div>

      <div className="orders-hub-panel-actions">
        <Link className="orders-hub-btn orders-hub-btn--primary" to="/reservations">
          {t('ordersDetail.reorder')}
        </Link>
      </div>
    </article>
  );
}

function reservationStatusTone(status: number): string {
  if (status === CUSTOMER_RESERVATION_STATUS.Ready) return 'green';
  if (status === CUSTOMER_RESERVATION_STATUS.Confirmed) return 'blue';
  if (status === CUSTOMER_RESERVATION_STATUS.Collected) return 'success';
  if (status === CUSTOMER_RESERVATION_STATUS.Cancelled || status === CUSTOMER_RESERVATION_STATUS.Rejected)
    return 'error';
  return 'gold';
}

function ReservationDetailPanel({
  detail,
  cancelling,
  onCancel,
}: {
  detail: CustomerReservationDetail;
  cancelling: boolean;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const { reservationStatus, reservationFulfillment } = useCustomerLabels();

  return (
    <article className="orders-hub-panel">
      <h2 className="orders-hub-panel-title">
        {t('ordersDetail.requestTitle', { number: detail.reservationNumber })}
      </h2>
      <div className="orders-hub-panel-meta">
        <span className={`orders-hub-status orders-hub-status--${reservationStatusTone(detail.status)}`}>
          {reservationStatus(detail.status)}
        </span>
        <span className="orders-hub-panel-time">
          {reservationFulfillment(detail.fulfillmentType)}
          {detail.addressSummary ? ` · ${detail.addressSummary}` : ''}
        </span>
      </div>

      {detail.salesOrderNumber ? (
        <div className="orders-hub-totals-ok" style={{ marginBottom: 10 }}>
          {t('ordersDetail.invoiceLinkPurchased', { number: detail.salesOrderNumber })}
        </div>
      ) : detail.status === CUSTOMER_RESERVATION_STATUS.Collected ? (
        <div className="orders-hub-alert orders-hub-alert--warn">
          <strong>{t('ordersDetail.noInvoiceTitle')}</strong>
          <div>{t('ordersDetail.noInvoiceDesc')}</div>
        </div>
      ) : null}

      <div className="orders-hub-lines">
        {detail.items.map((line) => (
          <div key={line.id} className="orders-hub-line">
            <div className="orders-hub-line-title">{line.productName}</div>
            <div className="orders-hub-line-sub">
              {line.quantity} {line.unitName}
              {line.customerNote ? ` · ${line.customerNote}` : ''}
            </div>
          </div>
        ))}
      </div>

      {detail.notes ? (
        <div className="orders-hub-totals-muted" style={{ marginTop: 10 }}>
          {t('ordersDetail.notes')}: {detail.notes}
        </div>
      ) : null}
      {detail.staffNotes ? (
        <div className="orders-hub-totals-muted" style={{ marginTop: 4 }}>
          {t('ordersDetail.pharmacy')}: {detail.staffNotes}
        </div>
      ) : null}

      {detail.status === CUSTOMER_RESERVATION_STATUS.Pending ? (
        <div className="orders-hub-panel-actions">
          <Popconfirm title={t('ordersDetail.cancelReservationConfirm')} onConfirm={onCancel}>
            <button type="button" className="orders-hub-btn orders-hub-btn--danger" disabled={cancelling}>
              {t('ordersDetail.cancelRequest')}
            </button>
          </Popconfirm>
        </div>
      ) : null}
    </article>
  );
}

function OrderDetailPanel({
  detail,
  confirming,
  cancelling,
  hiding,
  onConfirm,
  onCancel,
  onHide,
}: {
  detail: CustomerDraftOrder;
  confirming: boolean;
  cancelling: boolean;
  hiding: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  onHide: () => void;
}) {
  const { t } = useTranslation();

  return (
    <article className="orders-hub-panel">
      <h2 className="orders-hub-panel-title">
        {t('ordersDetail.detailTitle', { number: detail.draftNumber })}
      </h2>

      {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ? (
        <div className="orders-hub-alert">{t('ordersDetail.confirmInfo')}</div>
      ) : null}

      <div className="orders-hub-lines">
        {detail.items.map((line) => (
          <div key={line.id} className="orders-hub-line">
            <div className="orders-hub-line-title">{line.productName}</div>
            <div className="orders-hub-line-sub">
              {line.quantity} {line.unitName} · {formatMoney(line.lineAmount)}
            </div>
            {line.dosageNote ? <div className="orders-hub-line-sub">{line.dosageNote}</div> : null}
          </div>
        ))}
      </div>

      <div className="orders-hub-totals">
        <div>
          {t('ordersDetail.totalAmount')}: <strong>{formatMoney(detail.totalAmount)}</strong>
        </div>
        {detail.expiresAt && detail.status !== CUSTOMER_DRAFT_ORDER_STATUS.Completed ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.expiresAt')}: {dayjs(detail.expiresAt).format('DD/MM/YYYY HH:mm')}
          </div>
        ) : null}
        {detail.completedAt ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.purchasedAt')}: {dayjs(detail.completedAt).format('DD/MM/YYYY HH:mm')}
          </div>
        ) : null}
        {detail.notes ? (
          <div className="orders-hub-totals-muted">
            {t('ordersDetail.notes')}: {detail.notes}
          </div>
        ) : null}
        {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Completed && detail.salesOrderNumber ? (
          <div className="orders-hub-totals-ok">
            {t('ordersDetail.purchasedAtCounter', { number: detail.salesOrderNumber })}
          </div>
        ) : null}
      </div>

      <div className="orders-hub-panel-actions">
        {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ? (
          <button
            type="button"
            className="orders-hub-btn orders-hub-btn--primary"
            disabled={confirming}
            onClick={onConfirm}
          >
            {t('ordersDetail.confirmOrder')}
          </button>
        ) : null}
        {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
        detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed ? (
          <Popconfirm
            title={t('ordersDetail.cancelOrderTitle')}
            description={t('ordersDetail.cancelOrderDesc')}
            okText={t('ordersDetail.cancelOrderOk')}
            cancelText={t('ordersDetail.cancelOrderNo')}
            onConfirm={onCancel}
          >
            <button
              type="button"
              className="orders-hub-btn orders-hub-btn--danger"
              disabled={cancelling || confirming || hiding}
            >
              {t('ordersDetail.cancelOrder')}
            </button>
          </Popconfirm>
        ) : null}
        <Popconfirm
          title={t('ordersDetail.hideTitle')}
          description={t('ordersDetail.hideDesc')}
          okText={t('ordersDetail.hideOk')}
          cancelText={t('common.cancel')}
          onConfirm={onHide}
        >
          <button
            type="button"
            className="orders-hub-btn orders-hub-btn--danger"
            disabled={hiding || confirming}
          >
            {t('ordersDetail.hideFromApp')}
          </button>
        </Popconfirm>
      </div>
    </article>
  );
}

export function DraftOrdersPage() {
  const { t } = useTranslation();
  const { branding } = useCustomerBranding();
  const { online } = useApiHealth();
  const { data, isLoading, isFetching, error, refetch } = useOrdersOverviewQuery();
  const { draftOrderStatus, purchaseStatus, reservationStatus } = useCustomerLabels();
  const initialLoading = isLoading && !data;
  const refreshing = isFetching && !initialLoading;
  const [loadError, setLoadError] = useState<string | null>(null);
  const [orders, setOrders] = useState<CustomerDraftOrderListItem[]>([]);
  const [purchases, setPurchases] = useState<CustomerPurchaseListItem[]>([]);
  const [reservations, setReservations] = useState<CustomerReservationListItem[]>([]);
  const [hubTab, setHubTab] = useState<HubTab>('placed');
  const [detailMode, setDetailMode] = useState<DetailMode>(null);
  const [selectedId, setSelectedId] = useState<string>();
  const [selectedPurchaseId, setSelectedPurchaseId] = useState<string>();
  const [selectedReservationId, setSelectedReservationId] = useState<string>();
  const [detail, setDetail] = useState<CustomerDraftOrder | null>(null);
  const [purchaseDetail, setPurchaseDetail] = useState<CustomerPurchaseDetail | null>(null);
  const [reservationDetail, setReservationDetail] = useState<CustomerReservationDetail | null>(null);
  const [purchaseDetailLoading, setPurchaseDetailLoading] = useState(false);
  const [reservationDetailLoading, setReservationDetailLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [cancellingReservation, setCancellingReservation] = useState(false);
  const [hiding, setHiding] = useState(false);
  const [newDraftBanner, setNewDraftBanner] = useState<CustomerDraftOrderListItem[]>([]);
  const purchaseDetailRef = useRef<HTMLDivElement>(null);

  const placedOrders = useMemo(() => orders.filter((o) => isPlacedOrder(o.status)), [orders]);

  const applyOrdersData = useCallback(
    (
      draftItems: CustomerDraftOrderListItem[],
      purchaseItems: CustomerPurchaseListItem[],
      reservationItems: CustomerReservationListItem[],
    ) => {
      setOrders(draftItems);
      setPurchases(purchaseItems);
      setReservations(reservationItems);

      const unseenSent = filterUnseenSentDrafts(
        draftItems.filter((o) => o.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent),
      );
      if (unseenSent.length > 0) {
        setNewDraftBanner(unseenSent);
      }

      setSelectedId((current) =>
        current && draftItems.some((o) => o.id === current) ? current : undefined,
      );
      setSelectedPurchaseId((current) =>
        current && purchaseItems.some((o) => o.id === current) ? current : undefined,
      );
      setSelectedReservationId((current) =>
        current && reservationItems.some((o) => o.id === current) ? current : undefined,
      );
    },
    [],
  );

  useEffect(() => {
    if (!data) return;
    applyOrdersData(data.draftOrders, data.purchases, data.reservations);
  }, [applyOrdersData, data]);

  useEffect(() => {
    if (!error) return;
    setLoadError(getApiErrorMessage(error, t('ordersDetail.loadDraftFailed')));
  }, [error, t]);

  useRetryWhenApiOnline(() => void refetch());

  useEffect(() => {
    return subscribeDraftOrderAlerts((drafts) => {
      setNewDraftBanner((current) => {
        const merged = new Map(current.map((d) => [d.id, d]));
        drafts.forEach((d) => merged.set(d.id, d));
        return [...merged.values()];
      });
      void refetch();
    });
  }, [refetch]);

  const ordersRef = useRef(orders);
  ordersRef.current = orders;

  useEffect(() => {
    return () => {
      const sentIds = ordersRef.current
        .filter((o) => o.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent)
        .map((o) => o.id);
      markSentDraftsSeen(sentIds);
    };
  }, []);

  useEffect(() => {
    if (detailMode?.kind !== 'draft' || !selectedId) {
      if (detailMode?.kind !== 'draft') setDetail(null);
      return;
    }
    let cancelled = false;
    setDetailLoading(true);
    void fetchDraftOrder(selectedId)
      .then((row) => {
        if (!cancelled) setDetail(row);
      })
      .catch((err) => {
        if (!cancelled) {
          setDetail(null);
          message.error(getApiErrorMessage(err, t('ordersDetail.detailLoadFailed')));
        }
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [detailMode, selectedId, t]);

  useEffect(() => {
    if (detailMode?.kind !== 'purchase' || !selectedPurchaseId) {
      if (detailMode?.kind !== 'purchase') setPurchaseDetail(null);
      return;
    }
    let cancelled = false;
    setPurchaseDetailLoading(true);
    void fetchPurchase(selectedPurchaseId)
      .then((row) => {
        if (!cancelled) setPurchaseDetail(row);
      })
      .catch((err) => {
        if (!cancelled) {
          setPurchaseDetail(null);
          message.error(getApiErrorMessage(err, t('ordersDetail.invoiceDetailLoadFailed')));
        }
      })
      .finally(() => {
        if (!cancelled) setPurchaseDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [detailMode, selectedPurchaseId, t]);

  useEffect(() => {
    if (detailMode?.kind !== 'reservation' || !selectedReservationId) {
      if (detailMode?.kind !== 'reservation') setReservationDetail(null);
      return;
    }
    let cancelled = false;
    setReservationDetailLoading(true);
    void fetchReservation(selectedReservationId)
      .then((row) => {
        if (!cancelled) setReservationDetail(row);
      })
      .catch((err) => {
        if (!cancelled) {
          setReservationDetail(null);
          message.error(getApiErrorMessage(err, t('ordersDetail.reservationDetailLoadFailed')));
        }
      })
      .finally(() => {
        if (!cancelled) setReservationDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [detailMode, selectedReservationId, t]);

  useEffect(() => {
    if (detailMode?.kind !== 'purchase' || !purchaseDetail) return;
    purchaseDetailRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }, [detailMode, selectedPurchaseId, purchaseDetail]);

  const onConfirm = async () => {
    if (!selectedId) return;
    setConfirming(true);
    try {
      const updated = await confirmDraftOrder(selectedId);
      setDetail(updated);
      message.success(t('ordersDetail.confirmSuccess'));
      await refetch();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('ordersDetail.confirmFailed')));
    } finally {
      setConfirming(false);
    }
  };

  const onCancel = async () => {
    if (!selectedId) return;
    setCancelling(true);
    try {
      const updated = await cancelDraftOrder(selectedId);
      setDetail(updated);
      message.success(t('ordersDetail.cancelSuccess'));
      await refetch();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('ordersDetail.cancelFailed')));
    } finally {
      setCancelling(false);
    }
  };

  const onHide = async () => {
    if (!selectedId) return;
    setHiding(true);
    try {
      await hideDraftOrder(selectedId);
      message.success(t('ordersDetail.hideSuccess'));
      setDetail(null);
      setDetailMode(null);
      await refetch();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('ordersDetail.hideFailed')));
    } finally {
      setHiding(false);
    }
  };

  const onCancelReservation = async () => {
    if (!selectedReservationId) return;
    setCancellingReservation(true);
    try {
      const updated = await cancelReservation(selectedReservationId);
      setReservationDetail(updated);
      message.success(t('ordersDetail.cancelReservationSuccess'));
      await refetch();
    } catch (error) {
      message.error(getApiErrorMessage(error, t('ordersDetail.cancelReservationFailed')));
    } finally {
      setCancellingReservation(false);
    }
  };


  const waitingForApi = online === false && !!loadError;
  const pageError = loadError;

  const statusTone = (kind: 'draft' | 'purchase' | 'reservation', status: number) => {
    if (kind === 'draft') {
      if (status === CUSTOMER_DRAFT_ORDER_STATUS.Cancelled) return 'error' as const;
      if (status === CUSTOMER_DRAFT_ORDER_STATUS.Expired) return 'warn' as const;
      if (status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed || status === CUSTOMER_DRAFT_ORDER_STATUS.Completed)
        return 'done' as const;
      return 'processing' as const;
    }
    if (kind === 'purchase') {
      return status === CUSTOMER_PURCHASE_STATUS.Refunded ? ('warn' as const) : ('done' as const);
    }
    if (status === CUSTOMER_RESERVATION_STATUS.Cancelled || status === CUSTOMER_RESERVATION_STATUS.Rejected)
      return 'error' as const;
    if (status === CUSTOMER_RESERVATION_STATUS.Ready || status === CUSTOMER_RESERVATION_STATUS.Collected)
      return 'done' as const;
    return 'processing' as const;
  };

  const listCards = useMemo(() => {
    if (hubTab === 'placed') {
      return placedOrders.map((item) => ({
        key: item.id,
        title: item.draftNumber,
        sub: t('orders.hubOrderPreview', { count: item.itemCount, amount: formatMoney(item.totalAmount) }),
        time: formatOrderDate(item.sentAt || item.confirmedAt || item.expiresAt || new Date().toISOString()),
        statusLabel: draftOrderStatus(item.status),
        statusTone: statusTone('draft', item.status),
        icon: 'rx' as const,
        active: detailMode?.kind === 'draft' && detailMode.id === item.id,
        onClick: () => {
          setSelectedId(item.id);
          setDetailMode({ kind: 'draft', id: item.id });
        },
      }));
    }
    if (hubTab === 'reservations') {
      return reservations.map((item) => ({
        key: item.id,
        title: item.reservationNumber,
        sub: t('orders.hubReservationPreview', { count: item.itemCount }),
        time: formatOrderDate(item.submittedAt),
        statusLabel: reservationStatus(item.status),
        statusTone: statusTone('reservation', item.status),
        icon: 'clock' as const,
        active: detailMode?.kind === 'reservation' && detailMode.id === item.id,
        onClick: () => {
          setSelectedReservationId(item.id);
          setDetailMode({ kind: 'reservation', id: item.id });
        },
      }));
    }
    return purchases.map((item) => {
      const status = purchaseStatusLabel(item, {
        purchaseStatus,
        partialRefund: t('orders.partialRefund'),
      });
      return {
        key: item.id,
        title: item.orderNumber,
        sub: t('orders.hubOrderPreview', { count: item.itemCount, amount: formatMoney(item.totalAmount) }),
        time: formatOrderDate(item.orderDate),
        statusLabel: status.label,
        statusTone: status.tone === 'warning' || status.tone === 'orange' ? ('warn' as const) : ('done' as const),
        icon: 'bag' as const,
        active: detailMode?.kind === 'purchase' && detailMode.id === item.id,
        onClick: () => {
          setSelectedPurchaseId(item.id);
          setDetailMode({ kind: 'purchase', id: item.id });
        },
      };
    });
  }, [
    detailMode,
    draftOrderStatus,
    hubTab,
    placedOrders,
    purchaseStatus,
    purchases,
    reservationStatus,
    reservations,
    t,
  ]);

  const hubTabs: Array<{ key: HubTab; label: string; icon: ReactNode }> = [
    {
      key: 'placed',
      label: `${t('orders.tabPlaced')}${placedOrders.length ? ` (${placedOrders.length})` : ''}`,
      icon: <FileTextOutlined />,
    },
    {
      key: 'reservations',
      label: `${t('orders.tabReservations')}${reservations.length ? ` (${reservations.length})` : ''}`,
      icon: <ClockCircleOutlined />,
    },
    {
      key: 'purchased',
      label: `${t('orders.tabPurchased')}${purchases.length ? ` (${purchases.length})` : ''}`,
      icon: <ShoppingOutlined />,
    },
  ];

  const headerStyle = {
    background: `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`,
  };

  const closeDetail = () => {
    setDetailMode(null);
    setDetail(null);
    setPurchaseDetail(null);
    setReservationDetail(null);
  };

  return (
    <div className="orders-hub">
      <header className="orders-hub-header" style={headerStyle}>
        <div className="orders-hub-header-inner">
          <div className="orders-hub-brand">
            <BrandingLogo logoUrl={branding.logoUrl} />
            <div className="orders-hub-brand-text">
              <div className="orders-hub-brand-title">{branding.appName}</div>
              <div className="orders-hub-tagline">{branding.tagline || t('orders.hubTagline')}</div>
            </div>
          </div>
        </div>
      </header>

      <div className="orders-hub-sheet">
        <h1 className="orders-hub-title">{t('orders.title')}</h1>
        <p className="orders-hub-intro">{t('orders.intro')}</p>

        <div className="orders-hub-reserve">
          <div className="orders-hub-reserve-icon">
            <ShoppingCartOutlined />
          </div>
          <div className="orders-hub-reserve-copy">
            <div className="orders-hub-reserve-title">{t('orders.reserveCardTitle')}</div>
            <div className="orders-hub-reserve-sub">{t('orders.reserveCardSub')}</div>
          </div>
          <Link className="orders-hub-reserve-btn" to="/reservations">
            {t('orders.reserveCardCta')}
            <RightOutlined />
          </Link>
        </div>

        {newDraftBanner.length > 0 ? (
          <Alert
            style={{ marginTop: 12, borderRadius: 12 }}
            type="info"
            showIcon
            message={t('ordersDetail.newDraftTitle')}
            description={
              newDraftBanner.length === 1
                ? t('ordersDetail.newDraftSingle', {
                    number: newDraftBanner[0].draftNumber,
                    amount: formatMoney(newDraftBanner[0].totalAmount),
                  })
                : t('ordersDetail.newDraftMultiple', { count: newDraftBanner.length })
            }
            closable
            onClose={() => {
              markSentDraftsSeen(newDraftBanner.map((d) => d.id));
              setNewDraftBanner([]);
            }}
          />
        ) : null}

        {pageError && !shouldHidePageErrorForOfflineApi(pageError, online) ? (
          <Alert
            style={{ marginTop: 12, borderRadius: 12 }}
            type="error"
            showIcon
            message={t('ordersDetail.loadDraftFailed')}
            description={pageError}
            action={
              <Button size="small" onClick={() => void refetch()}>
                {t('common.retry')}
              </Button>
            }
          />
        ) : null}

        <div className="orders-hub-tabs" role="tablist">
          {hubTabs.map((tab) => (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={hubTab === tab.key}
              className={`orders-hub-tab${hubTab === tab.key ? ' orders-hub-tab--active' : ''}`}
              onClick={() => {
                setHubTab(tab.key);
                closeDetail();
              }}
            >
              {tab.icon}
              {tab.label}
            </button>
          ))}
        </div>

        {detailMode ? (
          <div className="orders-hub-detail">
            <button type="button" className="orders-hub-detail-back" onClick={closeDetail}>
              ← {t('common.back')}
            </button>
            {detailMode.kind === 'draft' && detailLoading ? (
              <div className="orders-hub-panel-loading">
                <Spin tip={t('ordersDetail.loadingDetail')} />
              </div>
            ) : null}
            {detailMode.kind === 'draft' && detail && isPlacedOrder(detail.status) ? (
              <OrderDetailPanel
                detail={detail}
                confirming={confirming}
                cancelling={cancelling}
                hiding={hiding}
                onConfirm={() => void onConfirm()}
                onCancel={() => void onCancel()}
                onHide={() => void onHide()}
              />
            ) : null}
            {detailMode.kind === 'purchase' && purchaseDetailLoading ? (
              <div className="orders-hub-panel-loading">
                <Spin tip={t('ordersDetail.loadingInvoiceDetail')} />
              </div>
            ) : null}
            {detailMode.kind === 'purchase' && purchaseDetail ? (
              <div ref={purchaseDetailRef}>
                <PurchaseDetailPanel detail={purchaseDetail} />
              </div>
            ) : null}
            {detailMode.kind === 'reservation' && reservationDetailLoading ? (
              <div className="orders-hub-panel-loading">
                <Spin tip={t('ordersDetail.loadingReservationDetail')} />
              </div>
            ) : null}
            {detailMode.kind === 'reservation' && reservationDetail ? (
              <ReservationDetailPanel
                detail={reservationDetail}
                cancelling={cancellingReservation}
                onCancel={() => void onCancelReservation()}
              />
            ) : null}
          </div>
        ) : null}

        {waitingForApi ? (
          <div style={{ textAlign: 'center', padding: 32 }}>
            <Spin tip={t('common.waitingApi')} />
          </div>
        ) : null}

        {!waitingForApi && !detailMode ? (
          <Spin spinning={refreshing && !initialLoading}>
            {initialLoading ? (
              <ListCardSkeleton rows={5} />
            ) : listCards.length === 0 ? (
              <div className="orders-hub-empty">{t('orders.hubEmpty')}</div>
            ) : (
              <div className="orders-hub-list">
                {listCards.map((item) => (
                  <button
                    key={item.key}
                    type="button"
                    className={`orders-hub-card${item.active ? ' orders-hub-card--active' : ''}`}
                    onClick={item.onClick}
                  >
                    <span
                      className={`orders-hub-card-icon${
                        item.icon === 'bag'
                          ? ' orders-hub-card-icon--bag'
                          : item.icon === 'clock'
                            ? ' orders-hub-card-icon--clock'
                            : ''
                      }`}
                    >
                      {item.icon === 'bag' ? (
                        <ShoppingOutlined />
                      ) : item.icon === 'clock' ? (
                        <ClockCircleOutlined />
                      ) : (
                        <FileTextOutlined />
                      )}
                    </span>
                    <div className="orders-hub-card-main">
                      <div className="orders-hub-card-top">
                        <span className="orders-hub-card-title">{item.title}</span>
                        <span className={`orders-hub-status orders-hub-status--${item.statusTone}`}>
                          {item.statusLabel}
                        </span>
                      </div>
                      <div className="orders-hub-card-sub">{item.sub}</div>
                    </div>
                    <div className="orders-hub-card-meta">
                      <span className="orders-hub-time">{item.time}</span>
                      <RightOutlined className="orders-hub-chevron" />
                    </div>
                  </button>
                ))}
              </div>
            )}
          </Spin>
        ) : null}

        {!detailMode ? (
          <div className="orders-hub-privacy">
            <div className="orders-hub-privacy-icon">
              <SafetyCertificateOutlined />
            </div>
            <div>
              <div className="orders-hub-privacy-title">{t('orders.privacyTitle')}</div>
              <div className="orders-hub-privacy-sub">{t('orders.privacySub')}</div>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}
