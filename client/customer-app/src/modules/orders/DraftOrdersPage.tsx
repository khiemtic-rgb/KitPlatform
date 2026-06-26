import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Card, Empty, List, Popconfirm, Space, Spin, Tabs, Tag, Typography, message } from 'antd';
import { RightOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  confirmDraftOrder,
  cancelReservation,
  fetchDraftOrder,
  fetchDraftOrders,
  fetchPurchase,
  fetchPurchases,
  fetchReservation,
  fetchReservations,
  getApiErrorMessage,
  hideDraftOrder,
  cancelDraftOrder,
} from '@/shared/api/customer-app.api';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  CUSTOMER_DRAFT_ORDER_STATUS_LABELS,
  CUSTOMER_PAYMENT_METHOD_LABELS,
  CUSTOMER_PURCHASE_STATUS,
  CUSTOMER_PURCHASE_STATUS_LABELS,
  CUSTOMER_RESERVATION_FULFILLMENT_LABELS,
  CUSTOMER_RESERVATION_STATUS,
  CUSTOMER_RESERVATION_STATUS_LABELS,
  type CustomerDraftOrder,
  type CustomerDraftOrderListItem,
  type CustomerPurchaseDetail,
  type CustomerPurchaseListItem,
  type CustomerReservationDetail,
  type CustomerReservationListItem,
} from '@/shared/api/customer-app.types';
import { BackToHomeButton } from '@/shared/components/BackToHomeButton';
import { shouldHidePageErrorForOfflineApi } from '@/shared/components/ApiHealthBanner';
import { useApiHealth, useRetryWhenApiOnline } from '@/shared/api/useApiHealth';
import { subscribeDraftOrderAlerts } from '@/shared/hooks/draft-order-alert-bus';
import {
  filterUnseenSentDrafts,
  markSentDraftsSeen,
} from '@/shared/hooks/draft-order-seen';

function formatMoney(value: number) {
  return value.toLocaleString('vi-VN') + 'đ';
}

function isPlacedOrder(status: number): boolean {
  return (
    status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Cancelled ||
    status === CUSTOMER_DRAFT_ORDER_STATUS.Expired
  );
}

function purchaseStatusLabel(item: CustomerPurchaseListItem): { label: string; color: string } {
  if (
    item.status === CUSTOMER_PURCHASE_STATUS.Completed &&
    item.totalRefunded > 0.0001
  ) {
    return { label: 'Trả một phần', color: 'orange' };
  }
  return {
    label: CUSTOMER_PURCHASE_STATUS_LABELS[item.status] ?? String(item.status),
    color: item.status === CUSTOMER_PURCHASE_STATUS.Refunded ? 'warning' : 'success',
  };
}

function statusTagColor(status: number): string {
  if (status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed) return 'green';
  if (status === CUSTOMER_DRAFT_ORDER_STATUS.Completed) return 'success';
  if (status === CUSTOMER_DRAFT_ORDER_STATUS.Cancelled) return 'error';
  if (status === CUSTOMER_DRAFT_ORDER_STATUS.Expired) return 'warning';
  return 'blue';
}

function OrderListCards({
  items,
  selectedId,
  onSelect,
}: {
  items: CustomerDraftOrderListItem[];
  selectedId?: string;
  onSelect: (id: string) => void;
}) {
  if (items.length === 0) {
    return <Empty description="Chưa có đơn nào" image={Empty.PRESENTED_IMAGE_SIMPLE} />;
  }

  return (
    <List
      dataSource={items}
      renderItem={(item) => (
        <Card
          size="small"
          style={{
            marginBottom: 8,
            borderRadius: 12,
            borderColor: item.id === selectedId ? '#0f766e' : undefined,
            cursor: 'pointer',
          }}
          onClick={() => onSelect(item.id)}
        >
          <Space direction="vertical" size={2} style={{ width: '100%' }}>
            <Space wrap>
              <Typography.Text strong>{item.draftNumber}</Typography.Text>
              <Tag color={statusTagColor(item.status)}>
                {CUSTOMER_DRAFT_ORDER_STATUS_LABELS[item.status] ?? item.status}
              </Tag>
            </Space>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {item.itemCount} sản phẩm · {formatMoney(item.totalAmount)}
            </Typography.Text>
          </Space>
        </Card>
      )}
    />
  );
}

function PurchaseListCards({
  items,
  selectedId,
  onSelect,
}: {
  items: CustomerPurchaseListItem[];
  selectedId?: string;
  onSelect: (id: string) => void;
}) {
  if (items.length === 0) {
    return (
      <Empty
        description="Chưa có đơn mua tại quầy"
        image={Empty.PRESENTED_IMAGE_SIMPLE}
      />
    );
  }

  return (
    <List
      dataSource={items}
      renderItem={(item) => {
        const status = purchaseStatusLabel(item);
        return (
          <Card
            size="small"
            style={{
              marginBottom: 8,
              borderRadius: 12,
              borderColor: item.id === selectedId ? '#0f766e' : undefined,
              cursor: 'pointer',
            }}
            onClick={() => onSelect(item.id)}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Space direction="vertical" size={2} style={{ flex: 1, minWidth: 0 }}>
                <Space wrap>
                  <Typography.Text strong>{item.orderNumber}</Typography.Text>
                  <Tag color={status.color}>{status.label}</Tag>
                </Space>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {dayjs(item.orderDate).format('DD/MM/YYYY HH:mm')} · {item.itemCount} sản phẩm ·{' '}
                  {formatMoney(item.totalAmount)}
                </Typography.Text>
                {item.id === selectedId ? (
                  <Typography.Text style={{ fontSize: 12, color: '#0f766e' }}>
                    Đang xem — chi tiết phía trên
                  </Typography.Text>
                ) : null}
              </Space>
              <RightOutlined
                style={{
                  color: item.id === selectedId ? '#0f766e' : '#94a3b8',
                  fontSize: 14,
                  flexShrink: 0,
                }}
              />
            </div>
          </Card>
        );
      }}
    />
  );
}

function PurchaseDetailPanel({ detail }: { detail: CustomerPurchaseDetail }) {
  const status = purchaseStatusLabel({
    id: detail.id,
    orderNumber: detail.orderNumber,
    status: detail.status,
    orderDate: detail.orderDate,
    totalAmount: detail.totalAmount,
    itemCount: detail.items.length,
    totalRefunded: detail.totalRefunded,
  });

  return (
    <Card size="small" title={`Hóa đơn ${detail.orderNumber}`} style={{ borderRadius: 12 }}>
      <Space wrap style={{ marginBottom: 12 }}>
        <Tag color={status.color}>{status.label}</Tag>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {dayjs(detail.orderDate).format('DD/MM/YYYY HH:mm')}
        </Typography.Text>
      </Space>

      <List
        size="small"
        dataSource={detail.items}
        renderItem={(line) => (
          <List.Item>
            <List.Item.Meta
              title={line.productName}
              description={
                <Space direction="vertical" size={0}>
                  <span>
                    {line.quantity} {line.unitName} · {formatMoney(line.lineTotal)}
                  </span>
                  {line.returnedQuantity > 0 ? (
                    <Typography.Text type="warning" style={{ fontSize: 12 }}>
                      Đã trả {line.returnedQuantity} {line.unitName}
                    </Typography.Text>
                  ) : null}
                </Space>
              }
            />
          </List.Item>
        )}
      />

      <Space direction="vertical" style={{ width: '100%', marginTop: 8 }}>
        <Typography.Text>
          Tổng thanh toán: <strong>{formatMoney(detail.totalAmount)}</strong>
        </Typography.Text>
        {detail.discountAmount > 0 ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Giảm giá: {formatMoney(detail.discountAmount)}
          </Typography.Text>
        ) : null}
        {detail.loyaltyDiscountAmount > 0 ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Giảm điểm: {formatMoney(detail.loyaltyDiscountAmount)}
          </Typography.Text>
        ) : null}
        {detail.voucherDiscountAmount > 0 && detail.voucherCode ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Voucher {detail.voucherCode}: -{formatMoney(detail.voucherDiscountAmount)}
          </Typography.Text>
        ) : null}
        {detail.loyaltyPointsEarned ? (
          <Typography.Text type="success" style={{ fontSize: 12 }}>
            +{detail.loyaltyPointsEarned} điểm thưởng
          </Typography.Text>
        ) : null}
        {detail.payments.length > 0 ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Thanh toán:{' '}
            {detail.payments
              .map(
                (p) =>
                  `${CUSTOMER_PAYMENT_METHOD_LABELS[p.paymentMethod] ?? p.paymentMethod}: ${formatMoney(p.amount)}`,
              )
              .join(' · ')}
          </Typography.Text>
        ) : null}
        {detail.notes ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Ghi chú: {detail.notes}
          </Typography.Text>
        ) : null}
      </Space>
    </Card>
  );
}

function reservationStatusColor(status: number): string {
  if (status === CUSTOMER_RESERVATION_STATUS.Ready) return 'green';
  if (status === CUSTOMER_RESERVATION_STATUS.Confirmed) return 'blue';
  if (status === CUSTOMER_RESERVATION_STATUS.Collected) return 'success';
  if (status === CUSTOMER_RESERVATION_STATUS.Cancelled || status === CUSTOMER_RESERVATION_STATUS.Rejected)
    return 'error';
  return 'gold';
}

function ReservationListCards({
  items,
  selectedId,
  onSelect,
}: {
  items: CustomerReservationListItem[];
  selectedId?: string;
  onSelect: (id: string) => void;
}) {
  if (items.length === 0) {
    return (
      <Empty
        description="Chưa có yêu cầu đặt trước"
        image={Empty.PRESENTED_IMAGE_SIMPLE}
      />
    );
  }

  return (
    <List
      dataSource={items}
      renderItem={(item) => (
        <Card
          size="small"
          style={{
            marginBottom: 8,
            borderRadius: 12,
            borderColor: item.id === selectedId ? '#0f766e' : undefined,
            cursor: 'pointer',
          }}
          onClick={() => onSelect(item.id)}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Space direction="vertical" size={2} style={{ flex: 1, minWidth: 0 }}>
              <Space wrap>
                <Typography.Text strong>{item.reservationNumber}</Typography.Text>
                <Tag color={reservationStatusColor(item.status)}>
                  {CUSTOMER_RESERVATION_STATUS_LABELS[item.status] ?? item.status}
                </Tag>
              </Space>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {dayjs(item.submittedAt).format('DD/MM/YYYY HH:mm')} · {item.itemCount} sản phẩm ·{' '}
                {CUSTOMER_RESERVATION_FULFILLMENT_LABELS[item.fulfillmentType] ?? item.fulfillmentType}
              </Typography.Text>
            </Space>
            <RightOutlined
              style={{
                color: item.id === selectedId ? '#0f766e' : '#94a3b8',
                fontSize: 14,
                flexShrink: 0,
              }}
            />
          </div>
        </Card>
      )}
    />
  );
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
  return (
    <Card size="small" title={`Yêu cầu ${detail.reservationNumber}`} style={{ borderRadius: 12 }}>
      <Space wrap style={{ marginBottom: 12 }}>
        <Tag color={reservationStatusColor(detail.status)}>
          {CUSTOMER_RESERVATION_STATUS_LABELS[detail.status] ?? detail.status}
        </Tag>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {CUSTOMER_RESERVATION_FULFILLMENT_LABELS[detail.fulfillmentType]}
          {detail.addressSummary ? ` · ${detail.addressSummary}` : ''}
        </Typography.Text>
      </Space>

      {detail.salesOrderNumber ? (
        <Typography.Text type="success" style={{ fontSize: 13, display: 'block' }}>
          Hóa đơn {detail.salesOrderNumber} — xem tab Đơn hàng đã mua
        </Typography.Text>
      ) : detail.status === CUSTOMER_RESERVATION_STATUS.Collected ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 8 }}
          message="Chưa có hóa đơn bán"
          description="Nhà thuốc cần bán qua quầy để ghi nhận thanh toán và tồn kho."
        />
      ) : null}

      <List
        size="small"
        dataSource={detail.items}
        renderItem={(line) => (
          <List.Item>
            <List.Item.Meta
              title={line.productName}
              description={
                <span>
                  {line.quantity} {line.unitName}
                  {line.customerNote ? ` · ${line.customerNote}` : ''}
                </span>
              }
            />
          </List.Item>
        )}
      />

      {detail.notes ? (
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 8 }}>
          Ghi chú: {detail.notes}
        </Typography.Text>
      ) : null}
      {detail.staffNotes ? (
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 4 }}>
          Nhà thuốc: {detail.staffNotes}
        </Typography.Text>
      ) : null}

      {detail.status === CUSTOMER_RESERVATION_STATUS.Pending ? (
        <Popconfirm title="Hủy yêu cầu đặt trước?" onConfirm={onCancel}>
          <Button danger block loading={cancelling} style={{ marginTop: 12 }}>
            Hủy yêu cầu
          </Button>
        </Popconfirm>
      ) : null}
    </Card>
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
  return (
    <Card size="small" title={`Chi tiết ${detail.draftNumber}`} style={{ borderRadius: 12 }}>
      {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message="Bạn có thể xác nhận đơn hàng (tuỳ chọn). Khi đến quầy, dược sĩ sẽ hỏi lại trước khi bán."
        />
      ) : null}

      <List
        size="small"
        dataSource={detail.items}
        renderItem={(line) => (
          <List.Item>
            <List.Item.Meta
              title={line.productName}
              description={
                <Space direction="vertical" size={0}>
                  <span>
                    {line.quantity} {line.unitName} · {formatMoney(line.lineAmount)}
                  </span>
                  {line.dosageNote ? (
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {line.dosageNote}
                    </Typography.Text>
                  ) : null}
                </Space>
              }
            />
          </List.Item>
        )}
      />

      <Space direction="vertical" style={{ width: '100%', marginTop: 8 }}>
        <Typography.Text>
          Tổng tiền: <strong>{formatMoney(detail.totalAmount)}</strong>
        </Typography.Text>
        {detail.expiresAt && detail.status !== CUSTOMER_DRAFT_ORDER_STATUS.Completed ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Hết hạn: {dayjs(detail.expiresAt).format('DD/MM/YYYY HH:mm')}
          </Typography.Text>
        ) : null}
        {detail.completedAt ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Mua lúc: {dayjs(detail.completedAt).format('DD/MM/YYYY HH:mm')}
          </Typography.Text>
        ) : null}
        {detail.notes ? (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Ghi chú: {detail.notes}
          </Typography.Text>
        ) : null}
        {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ? (
          <Button type="primary" loading={confirming} onClick={onConfirm}>
            Xác nhận đơn hàng (tuỳ chọn)
          </Button>
        ) : null}
        {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent ||
        detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed ? (
          <Popconfirm
            title="Hủy đơn hàng?"
            description="Nhà thuốc sẽ được thông báo. Bạn có thể đặt lại sau nếu cần."
            okText="Hủy đơn"
            cancelText="Không"
            onConfirm={onCancel}
          >
            <Button danger loading={cancelling} disabled={confirming || hiding}>
              Hủy đơn hàng
            </Button>
          </Popconfirm>
        ) : null}
        {detail.status === CUSTOMER_DRAFT_ORDER_STATUS.Completed && detail.salesOrderNumber ? (
          <Typography.Text type="success">Đã mua tại quầy — {detail.salesOrderNumber}</Typography.Text>
        ) : null}
        <Popconfirm
          title="Ẩn đơn hàng khỏi app?"
          description="Bạn sẽ không thấy đơn này trên app nữa. Nhà thuốc vẫn giữ lịch sử đơn."
          okText="Ẩn khỏi app"
          cancelText="Huỷ"
          onConfirm={onHide}
        >
          <Button danger loading={hiding} disabled={confirming}>
            Ẩn khỏi app
          </Button>
        </Popconfirm>
      </Space>
    </Card>
  );
}

export function DraftOrdersPage() {
  const { online } = useApiHealth();
  const [initialLoading, setInitialLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [purchaseLoadError, setPurchaseLoadError] = useState<string | null>(null);
  const [reservationLoadError, setReservationLoadError] = useState<string | null>(null);
  const [orders, setOrders] = useState<CustomerDraftOrderListItem[]>([]);
  const [purchases, setPurchases] = useState<CustomerPurchaseListItem[]>([]);
  const [reservations, setReservations] = useState<CustomerReservationListItem[]>([]);
  const [activeTab, setActiveTab] = useState<'placed' | 'purchased' | 'reservations'>('placed');
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

  const loadAll = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setInitialLoading(true);
    setLoadError(null);
    setPurchaseLoadError(null);
    setReservationLoadError(null);
    try {
      const [draftResult, purchaseResult, reservationResult] = await Promise.allSettled([
        fetchDraftOrders(),
        fetchPurchases(),
        fetchReservations(),
      ]);

      if (draftResult.status === 'fulfilled') {
        const items = draftResult.value;
        setOrders(items);
        const unseenSent = filterUnseenSentDrafts(
          items.filter((o) => o.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent),
        );
        if (unseenSent.length > 0) {
          setNewDraftBanner(unseenSent);
        }
        const placed = items.filter((o) => isPlacedOrder(o.status));
        setActiveTab((tab) => {
          if (tab === 'placed' && placed.length === 0 && purchaseResult.status === 'fulfilled' && purchaseResult.value.length > 0) {
            return 'purchased';
          }
          return tab;
        });
        setSelectedId((current) => {
          if (current && items.some((o) => o.id === current)) return current;
          const placed = items.filter((o) => isPlacedOrder(o.status));
          return placed[0]?.id;
        });
      } else {
        setOrders([]);
        setLoadError(getApiErrorMessage(draftResult.reason, 'Không tải được đơn hàng'));
      }

      if (purchaseResult.status === 'fulfilled') {
        const items = purchaseResult.value;
        setPurchases(items);
        setSelectedPurchaseId((current) => {
          if (current && items.some((o) => o.id === current)) return current;
          return items[0]?.id;
        });
      } else {
        setPurchases([]);
        setPurchaseLoadError(getApiErrorMessage(purchaseResult.reason, 'Không tải được lịch sử mua'));
      }

      if (reservationResult.status === 'fulfilled') {
        const items = reservationResult.value;
        setReservations(items);
        setSelectedReservationId((current) => {
          if (current && items.some((o) => o.id === current)) return current;
          return items[0]?.id;
        });
      } else {
        setReservations([]);
        setReservationLoadError(getApiErrorMessage(reservationResult.reason, 'Không tải được đặt trước'));
      }
    } finally {
      setInitialLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void loadAll(false);
  }, [loadAll]);

  useRetryWhenApiOnline(() => loadAll(true));

  useEffect(() => {
    return subscribeDraftOrderAlerts((drafts) => {
      setNewDraftBanner((current) => {
        const merged = new Map(current.map((d) => [d.id, d]));
        drafts.forEach((d) => merged.set(d.id, d));
        return [...merged.values()];
      });
      void loadAll(true);
    });
  }, [loadAll]);

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
    if (activeTab !== 'placed' || placedOrders.length === 0) {
      if (activeTab === 'placed') {
        setSelectedId(undefined);
        setDetail(null);
      }
      return;
    }
    setSelectedId((current) => {
      if (current && placedOrders.some((o) => o.id === current)) return current;
      return placedOrders[0].id;
    });
  }, [activeTab, placedOrders]);

  useEffect(() => {
    if (activeTab !== 'purchased' || purchases.length === 0) {
      if (activeTab === 'purchased') {
        setSelectedPurchaseId(undefined);
        setPurchaseDetail(null);
      }
      return;
    }
    setSelectedPurchaseId((current) => {
      if (current && purchases.some((o) => o.id === current)) return current;
      return purchases[0].id;
    });
  }, [activeTab, purchases]);

  useEffect(() => {
    if (activeTab !== 'placed' || !selectedId) {
      if (activeTab === 'placed') setDetail(null);
      return;
    }
    let cancelled = false;
    setDetailLoading(true);
    void fetchDraftOrder(selectedId)
      .then((data) => {
        if (!cancelled) setDetail(data);
      })
      .catch((error) => {
        if (!cancelled) {
          setDetail(null);
          message.error(getApiErrorMessage(error, 'Không tải được chi tiết đơn'));
        }
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeTab, selectedId]);

  useEffect(() => {
    if (activeTab !== 'purchased' || !selectedPurchaseId) {
      if (activeTab === 'purchased') setPurchaseDetail(null);
      return;
    }
    let cancelled = false;
    setPurchaseDetailLoading(true);
    void fetchPurchase(selectedPurchaseId)
      .then((data) => {
        if (!cancelled) setPurchaseDetail(data);
      })
      .catch((error) => {
        if (!cancelled) {
          setPurchaseDetail(null);
          message.error(getApiErrorMessage(error, 'Không tải được chi tiết hóa đơn'));
        }
      })
      .finally(() => {
        if (!cancelled) setPurchaseDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeTab, selectedPurchaseId]);

  useEffect(() => {
    if (activeTab !== 'reservations' || reservations.length === 0) {
      if (activeTab === 'reservations') {
        setSelectedReservationId(undefined);
        setReservationDetail(null);
      }
      return;
    }
    setSelectedReservationId((current) => {
      if (current && reservations.some((o) => o.id === current)) return current;
      return reservations[0].id;
    });
  }, [activeTab, reservations]);

  useEffect(() => {
    if (activeTab !== 'reservations' || !selectedReservationId) {
      if (activeTab === 'reservations') setReservationDetail(null);
      return;
    }
    let cancelled = false;
    setReservationDetailLoading(true);
    void fetchReservation(selectedReservationId)
      .then((data) => {
        if (!cancelled) setReservationDetail(data);
      })
      .catch((error) => {
        if (!cancelled) {
          setReservationDetail(null);
          message.error(getApiErrorMessage(error, 'Không tải được chi tiết đặt trước'));
        }
      })
      .finally(() => {
        if (!cancelled) setReservationDetailLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeTab, selectedReservationId]);

  useEffect(() => {
    if (activeTab !== 'purchased' || !purchaseDetail) return;
    purchaseDetailRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }, [activeTab, selectedPurchaseId, purchaseDetail]);

  const onConfirm = async () => {
    if (!selectedId) return;
    setConfirming(true);
    try {
      const updated = await confirmDraftOrder(selectedId);
      setDetail(updated);
      message.success('Đã xác nhận đơn hàng — khi lấy thuốc dược sĩ sẽ xác nhận lại tại quầy');
      await loadAll(true);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không xác nhận được đơn hàng'));
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
      message.success('Đã hủy đơn hàng — nhà thuốc đã được thông báo');
      await loadAll(true);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không hủy được đơn hàng'));
    } finally {
      setCancelling(false);
    }
  };

  const onHide = async () => {
    if (!selectedId) return;
    setHiding(true);
    try {
      await hideDraftOrder(selectedId);
      message.success('Đã ẩn đơn hàng khỏi app');
      setDetail(null);
      await loadAll(true);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không ẩn được đơn hàng'));
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
      message.success('Đã hủy yêu cầu');
      await loadAll(true);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không hủy được yêu cầu'));
    } finally {
      setCancellingReservation(false);
    }
  };

  const waitingForApi = online === false && !!loadError && !!purchaseLoadError && !!reservationLoadError;
  const pageError =
    activeTab === 'placed'
      ? loadError
      : activeTab === 'purchased'
        ? purchaseLoadError ?? loadError
        : reservationLoadError ?? loadError;

  if (initialLoading) {
    return (
      <div style={{ textAlign: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <BackToHomeButton />
      <Typography.Title level={5} style={{ margin: 0 }}>
        Đơn hàng
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
        Dược sĩ soạn đơn — bạn xác nhận (tuỳ chọn) rồi đến quầy thanh toán.
      </Typography.Paragraph>

      <Card
        size="small"
        style={{ borderRadius: 12, background: '#fffbeb', borderColor: '#fcd34d' }}
        styles={{ body: { padding: '10px 14px' } }}
      >
        <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap>
          <Typography.Text style={{ fontSize: 13 }}>Cần thuốc chưa có sẵn? Gửi yêu cầu đặt trước.</Typography.Text>
          <Link to="/reservations">
            <Button type="primary" size="small">
              Đặt thuốc trước
            </Button>
          </Link>
        </Space>
      </Card>

      {newDraftBanner.length > 0 ? (
        <Alert
          type="info"
          showIcon
          message="Đơn thuốc mới"
          description={
            newDraftBanner.length === 1
              ? `${newDraftBanner[0].draftNumber} — tổng tạm tính ${formatMoney(newDraftBanner[0].totalAmount)}.`
              : `Bạn có ${newDraftBanner.length} đơn mới chờ xem.`
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
          type="error"
          showIcon
          message={activeTab === 'placed' ? 'Không tải được đơn hàng' : activeTab === 'purchased' ? 'Không tải được lịch sử mua' : 'Không tải được đặt trước'}
          description={pageError}
          action={
            <Button size="small" onClick={() => void loadAll(true)}>
              Thử lại
            </Button>
          }
        />
      ) : null}

      {waitingForApi ? (
        <div style={{ textAlign: 'center', padding: 32 }}>
          <Spin tip="Đang chờ API — tự tải lại khi kết nối trở lại" />
        </div>
      ) : null}

      {!waitingForApi && !pageError ? (
        <Spin spinning={refreshing}>
          <Tabs
            activeKey={activeTab}
            onChange={(key) => setActiveTab(key as 'placed' | 'purchased' | 'reservations')}
            items={[
              {
                key: 'placed',
                label: `Đơn hàng đặt${placedOrders.length > 0 ? ` (${placedOrders.length})` : ''}`,
                children: (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    {selectedId && detailLoading ? (
                      <Card size="small" style={{ borderRadius: 12, textAlign: 'center', padding: 16 }}>
                        <Spin tip="Đang tải chi tiết đơn…" />
                      </Card>
                    ) : null}
                    {detail && activeTab === 'placed' && isPlacedOrder(detail.status) ? (
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
                    <OrderListCards items={placedOrders} selectedId={selectedId} onSelect={setSelectedId} />
                  </Space>
                ),
              },
              {
                key: 'reservations',
                label: `Đặt trước${reservations.length > 0 ? ` (${reservations.length})` : ''}`,
                children: (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                      Yêu cầu thuốc chưa có sẵn — không phải hóa đơn bán. Hóa đơn thanh toán tại quầy nằm ở tab Đã mua.
                    </Typography.Paragraph>
                    {selectedReservationId && reservationDetailLoading ? (
                      <Card size="small" style={{ borderRadius: 12, textAlign: 'center', padding: 16 }}>
                        <Spin tip="Đang tải chi tiết…" />
                      </Card>
                    ) : null}
                    {reservationDetail && activeTab === 'reservations' ? (
                      <ReservationDetailPanel
                        detail={reservationDetail}
                        cancelling={cancellingReservation}
                        onCancel={() => void onCancelReservation()}
                      />
                    ) : null}
                    <ReservationListCards
                      items={reservations}
                      selectedId={selectedReservationId}
                      onSelect={setSelectedReservationId}
                    />
                  </Space>
                ),
              },
              {
                key: 'purchased',
                label: `Đơn hàng đã mua${purchases.length > 0 ? ` (${purchases.length})` : ''}`,
                children: (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <Typography.Paragraph type="secondary" style={{ marginBottom: 0, fontSize: 13 }}>
                      Chạm một hóa đơn để xem chi tiết sản phẩm và thanh toán.
                    </Typography.Paragraph>
                    {selectedPurchaseId && purchaseDetailLoading ? (
                      <Card size="small" style={{ borderRadius: 12, textAlign: 'center', padding: 16 }}>
                        <Spin tip="Đang tải chi tiết hóa đơn…" />
                      </Card>
                    ) : null}
                    {purchaseDetail && activeTab === 'purchased' ? (
                      <div ref={purchaseDetailRef}>
                        <PurchaseDetailPanel detail={purchaseDetail} />
                      </div>
                    ) : null}
                    <PurchaseListCards
                      items={purchases}
                      selectedId={selectedPurchaseId}
                      onSelect={setSelectedPurchaseId}
                    />
                  </Space>
                ),
              },
            ]}
          />
        </Spin>
      ) : null}

      <Link to="/">← Về trang chủ</Link>
    </Space>
  );
}
