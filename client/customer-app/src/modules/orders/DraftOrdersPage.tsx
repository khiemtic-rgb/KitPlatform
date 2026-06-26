import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Card, Empty, List, Popconfirm, Space, Spin, Tabs, Tag, Typography, message } from 'antd';
import dayjs from 'dayjs';
import {
  confirmDraftOrder,
  fetchDraftOrder,
  fetchDraftOrders,
  getApiErrorMessage,
  hideDraftOrder,
  cancelDraftOrder,
} from '@/shared/api/customer-app.api';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  CUSTOMER_DRAFT_ORDER_STATUS_LABELS,
  type CustomerDraftOrder,
  type CustomerDraftOrderListItem,
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

function isPurchasedOrder(status: number): boolean {
  return status === CUSTOMER_DRAFT_ORDER_STATUS.Completed;
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
  const [orders, setOrders] = useState<CustomerDraftOrderListItem[]>([]);
  const [activeTab, setActiveTab] = useState<'placed' | 'purchased'>('placed');
  const [selectedId, setSelectedId] = useState<string>();
  const [detail, setDetail] = useState<CustomerDraftOrder | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [hiding, setHiding] = useState(false);
  const [newDraftBanner, setNewDraftBanner] = useState<CustomerDraftOrderListItem[]>([]);

  const placedOrders = useMemo(() => orders.filter((o) => isPlacedOrder(o.status)), [orders]);
  const purchasedOrders = useMemo(() => orders.filter((o) => isPurchasedOrder(o.status)), [orders]);
  const visibleOrders = useMemo(
    () => (activeTab === 'placed' ? placedOrders : purchasedOrders),
    [activeTab, placedOrders, purchasedOrders],
  );

  const loadList = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setInitialLoading(true);
    setLoadError(null);
    try {
      const items = await fetchDraftOrders();
      setOrders(items);
      const unseenSent = filterUnseenSentDrafts(
        items.filter((o) => o.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent),
      );
      if (unseenSent.length > 0) {
        setNewDraftBanner(unseenSent);
      }
      const placed = items.filter((o) => isPlacedOrder(o.status));
      const purchased = items.filter((o) => isPurchasedOrder(o.status));
      setActiveTab((tab) => {
        const pool = tab === 'placed' ? placed : purchased;
        if (pool.length === 0) return placed.length > 0 ? 'placed' : 'purchased';
        return tab;
      });
      setSelectedId((current) => {
        if (current && items.some((o) => o.id === current)) return current;
        const pool = placed.length > 0 ? placed : purchased;
        return pool[0]?.id;
      });
    } catch (error) {
      setOrders([]);
      setLoadError(getApiErrorMessage(error, 'Không tải được đơn hàng'));
    } finally {
      setInitialLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void loadList(false);
  }, [loadList]);

  useRetryWhenApiOnline(() => loadList(true));

  useEffect(() => {
    return subscribeDraftOrderAlerts((drafts) => {
      setNewDraftBanner((current) => {
        const merged = new Map(current.map((d) => [d.id, d]));
        drafts.forEach((d) => merged.set(d.id, d));
        return [...merged.values()];
      });
      void loadList(true);
    });
  }, [loadList]);

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
    if (visibleOrders.length === 0) {
      setSelectedId(undefined);
      setDetail(null);
      return;
    }
    setSelectedId((current) => {
      if (current && visibleOrders.some((o) => o.id === current)) return current;
      return visibleOrders[0].id;
    });
  }, [activeTab, visibleOrders]);

  useEffect(() => {
    if (!selectedId) {
      setDetail(null);
      return;
    }
    let cancelled = false;
    void fetchDraftOrder(selectedId)
      .then((data) => {
        if (!cancelled) setDetail(data);
      })
      .catch((error) => {
        if (!cancelled) {
          setDetail(null);
          message.error(getApiErrorMessage(error, 'Không tải được chi tiết đơn'));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [selectedId]);

  const onConfirm = async () => {
    if (!selectedId) return;
    setConfirming(true);
    try {
      const updated = await confirmDraftOrder(selectedId);
      setDetail(updated);
      message.success('Đã xác nhận đơn hàng — khi lấy thuốc dược sĩ sẽ xác nhận lại tại quầy');
      await loadList(true);
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
      await loadList(true);
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
      await loadList(true);
    } catch (error) {
      message.error(getApiErrorMessage(error, 'Không ẩn được đơn hàng'));
    } finally {
      setHiding(false);
    }
  };

  const waitingForApi = online === false && !!loadError;

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

      {loadError && !shouldHidePageErrorForOfflineApi(loadError, online) ? (
        <Alert
          type="error"
          showIcon
          message="Không tải được đơn hàng"
          description={loadError}
          action={
            <Button size="small" onClick={() => void loadList(true)}>
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

      {!waitingForApi && !loadError && orders.length === 0 ? (
        <Card size="small" style={{ borderRadius: 12 }}>
          <Typography.Paragraph style={{ marginBottom: 8 }}>Chưa có đơn hàng nào.</Typography.Paragraph>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Khi dược sĩ gửi đơn thuốc qua app, bạn sẽ thấy tại đây.
          </Typography.Text>
        </Card>
      ) : !waitingForApi && !loadError ? (
        <Spin spinning={refreshing}>
          <Tabs
            activeKey={activeTab}
            onChange={(key) => setActiveTab(key as 'placed' | 'purchased')}
            items={[
              {
                key: 'placed',
                label: `Đơn hàng đặt${placedOrders.length > 0 ? ` (${placedOrders.length})` : ''}`,
                children: (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <OrderListCards items={placedOrders} selectedId={selectedId} onSelect={setSelectedId} />
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
                  </Space>
                ),
              },
              {
                key: 'purchased',
                label: `Đơn hàng đã mua${purchasedOrders.length > 0 ? ` (${purchasedOrders.length})` : ''}`,
                children: (
                  <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                    <OrderListCards items={purchasedOrders} selectedId={selectedId} onSelect={setSelectedId} />
                    {detail && activeTab === 'purchased' && isPurchasedOrder(detail.status) ? (
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
