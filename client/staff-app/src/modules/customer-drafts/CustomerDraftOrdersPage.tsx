import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Input,
  Popconfirm,
  Segmented,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  PhoneOutlined,
  ReloadOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  CUSTOMER_DRAFT_ORDER_STATUS,
  CUSTOMER_DRAFT_ORDER_STATUS_COLORS,
  CUSTOMER_DRAFT_ORDER_STATUS_LABELS,
  cancelCustomerDraftOrder,
  fetchCustomerDraftOrders,
  loadCustomerDraftOrderForPos,
  type CustomerDraftOrderListItem,
} from '@/shared/api/customer-draft-orders.api';
import { fetchCustomerById } from '@/shared/api/customer.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import {
  isActionableCustomerDraftStatus,
  loadCustomerDraftCartLines,
  orderDiscountFromCustomerDraft,
} from '@/modules/sales/customer-draft-order-helpers';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

const ACTIVE_STATUSES = [
  CUSTOMER_DRAFT_ORDER_STATUS.Sent,
  CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
];

type StatusFilter = 'all' | 'sent' | 'confirmed';

function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

function hasUsablePhone(phone?: string | null): boolean {
  const d = digitsOnly(phone ?? '');
  return d.length >= 9 && d.length <= 12;
}

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return `Hôm nay ${parsed.format('HH:mm')}`;
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM HH:mm');
}

function expiryLabel(expiresAt?: string | null): { text: string; urgent: boolean } | null {
  if (!expiresAt) return null;
  const parsed = dayjs(expiresAt);
  if (!parsed.isValid()) return null;
  const now = dayjs();
  if (parsed.isBefore(now)) return { text: 'Đã hết hạn', urgent: true };
  const hours = parsed.diff(now, 'hour', true);
  if (hours <= 2) return { text: `Hết hạn ${parsed.format('HH:mm')}`, urgent: true };
  if (now.isSame(parsed, 'day')) return { text: `Hết hạn hôm nay ${parsed.format('HH:mm')}`, urgent: false };
  return { text: `Hết hạn ${parsed.format('DD/MM HH:mm')}`, urgent: false };
}

function sortDrafts(items: CustomerDraftOrderListItem[]): CustomerDraftOrderListItem[] {
  const rank: Record<number, number> = {
    [CUSTOMER_DRAFT_ORDER_STATUS.Confirmed]: 0,
    [CUSTOMER_DRAFT_ORDER_STATUS.Sent]: 1,
  };
  return [...items].sort((a, b) => {
    const ra = rank[a.status] ?? 9;
    const rb = rank[b.status] ?? 9;
    if (ra !== rb) return ra - rb;
    const ta = dayjs(a.confirmedAt ?? a.sentAt).valueOf() || 0;
    const tb = dayjs(b.confirmedAt ?? b.sentAt).valueOf() || 0;
    return tb - ta;
  });
}

export function CustomerDraftOrdersPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const { setWarehouseId, replaceCart, setCustomer, setOrderDiscount, setLoadedCustomerDraft } =
    usePosSession();
  const [items, setItems] = useState<CustomerDraftOrderListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState<StatusFilter>('all');
  const [loadingPos, setLoadingPos] = useState<string | null>(null);
  const [cancellingId, setCancellingId] = useState<string | null>(null);

  const load = useCallback(
    async (mode: 'full' | 'refresh' = 'full') => {
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      } else {
        setRefreshing(true);
      }
      try {
        const rows = await fetchCustomerDraftOrders(ACTIVE_STATUSES);
        setItems(rows.filter((row) => isActionableCustomerDraftStatus(row.status)));
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được đơn nháp khách');
        if (mode === 'full') {
          setItems([]);
          setLoadError(text);
        } else {
          message.error(text);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [message],
  );

  useEffect(() => {
    void load('full');
  }, [load]);

  const counts = useMemo(() => {
    const sent = items.filter((i) => i.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent).length;
    const confirmed = items.filter((i) => i.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed).length;
    return { sent, confirmed, total: items.length };
  }, [items]);

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    const qDigits = digitsOnly(query);
    let rows = sortDrafts(items);
    if (filter === 'sent') rows = rows.filter((i) => i.status === CUSTOMER_DRAFT_ORDER_STATUS.Sent);
    if (filter === 'confirmed') {
      rows = rows.filter((i) => i.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed);
    }
    if (q || qDigits.length >= 3) {
      rows = rows.filter((i) => {
        const phoneDigits = digitsOnly(i.customerPhone ?? '');
        return (
          i.draftNumber.toLowerCase().includes(q) ||
          i.customerName.toLowerCase().includes(q) ||
          (i.customerPhone ?? '').toLowerCase().includes(q) ||
          (qDigits.length >= 3 && phoneDigits.includes(qDigits))
        );
      });
    }
    return rows;
  }, [items, query, filter]);

  const sendToPos = async (id: string) => {
    setLoadingPos(id);
    try {
      const payload = await loadCustomerDraftOrderForPos(id);
      const lines = await loadCustomerDraftCartLines(payload);
      if (lines.length === 0) {
        message.warning('Đơn nháp không có sản phẩm hợp lệ để đưa vào POS');
        return;
      }
      setWarehouseId(payload.warehouseId);
      replaceCart(lines);
      setOrderDiscount(orderDiscountFromCustomerDraft(payload));
      const customer = await fetchCustomerById(payload.customerId);
      setCustomer({
        id: customer.id,
        customerCode: customer.customerCode,
        fullName: customer.fullName,
        phone: customer.phone,
        allowCredit: customer.allowCredit,
      });
      setLoadedCustomerDraft(payload.draftOrderId, payload.draftNumber);
      message.success(`Đã nạp đơn nháp ${payload.draftNumber} vào POS`);
      navigate('/pos');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đưa được vào POS'));
    } finally {
      setLoadingPos(null);
    }
  };

  const cancelItem = async (id: string) => {
    setCancellingId(id);
    try {
      await cancelCustomerDraftOrder(id);
      message.success('Đã hủy đơn nháp');
      await load('refresh');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không hủy được đơn nháp'));
    } finally {
      setCancellingId(null);
    }
  };

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Đơn nháp app khách"
        subtitle={
          loadError
            ? 'Không tải được danh sách'
            : counts.confirmed > 0
              ? `${counts.confirmed} khách đã xác nhận · ${counts.total} đơn`
              : counts.total > 0
                ? `${counts.sent} đang chờ khách · ${counts.total} đơn`
                : 'Không có đơn đang chờ'
        }
        backTo="/"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={refreshing || loading} />}
            aria-label="Tải lại"
            onClick={() => void load(loadError ? 'full' : 'refresh')}
          />
        }
      />
      <main className="staff-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được đơn nháp"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : (
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 10, fontSize: 12 }}>
            Dược sĩ gửi qua app · khách xác nhận → Đưa vào POS bán.
          </Typography.Text>
        )}

        <Input
          size="large"
          allowClear
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tìm mã đơn, tên, SĐT…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          disabled={Boolean(loadError) && items.length === 0}
        />

        <div className="customer-draft-toolbar">
          <Segmented
            size="middle"
            value={filter}
            onChange={(v) => setFilter(v as StatusFilter)}
            disabled={Boolean(loadError) && items.length === 0}
            options={[
              { label: `Tất cả (${counts.total})`, value: 'all' },
              { label: `Chờ khách (${counts.sent})`, value: 'sent' },
              { label: `Đã xác nhận (${counts.confirmed})`, value: 'confirmed' },
            ]}
          />
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : loadError && items.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Bấm Thử lại khi mạng ổn" />
        ) : visible.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              query.trim()
                ? 'Không tìm thấy đơn khớp'
                : filter === 'all'
                  ? 'Không có đơn nháp đang chờ. Đơn mới sẽ hiện khi dược sĩ gửi và khách xác nhận trên app.'
                  : 'Không có đơn ở trạng thái này'
            }
          />
        ) : (
          visible.map((item) => {
            const expiry = expiryLabel(item.expiresAt);
            const isConfirmed = item.status === CUSTOMER_DRAFT_ORDER_STATUS.Confirmed;
            const whenLabel = isConfirmed
              ? `Xác nhận ${formatWhen(item.confirmedAt)}`
              : `Gửi ${formatWhen(item.sentAt)}`;

            return (
              <article key={item.id} className="customer-draft-card">
                <div className="customer-draft-card__head">
                  <div className="customer-draft-card__ids">
                    <Typography.Text strong className="customer-draft-card__number">
                      {item.draftNumber}
                    </Typography.Text>
                    <Typography.Text type="secondary" className="customer-draft-card__when">
                      {whenLabel}
                    </Typography.Text>
                  </div>
                  <Tag color={CUSTOMER_DRAFT_ORDER_STATUS_COLORS[item.status] ?? 'default'}>
                    {CUSTOMER_DRAFT_ORDER_STATUS_LABELS[item.status] ?? item.status}
                  </Tag>
                </div>

                <div className="customer-draft-card__customer">
                  <Typography.Text strong>{item.customerName || 'Khách'}</Typography.Text>
                  <div className="customer-draft-card__meta">
                    {hasUsablePhone(item.customerPhone) ? (
                      <a
                        className="customer-draft-card__phone"
                        href={`tel:${digitsOnly(item.customerPhone!)}`}
                      >
                        <PhoneOutlined /> {item.customerPhone}
                      </a>
                    ) : (
                      <span className="customer-draft-card__phone-missing">Chưa có SĐT</span>
                    )}
                    <span>· {item.itemCount} SP</span>
                    {expiry ? (
                      <span className={expiry.urgent ? 'customer-draft-card__expiry is-urgent' : ''}>
                        · {expiry.text}
                      </span>
                    ) : null}
                  </div>
                </div>

                <div className="customer-draft-card__amount">{formatMoney(item.totalAmount)}</div>

                {!isConfirmed ? (
                  <Typography.Text type="secondary" className="customer-draft-card__hint">
                    Khách chưa xác nhận trên app — vẫn có thể bán tại quầy nếu khách đến.
                  </Typography.Text>
                ) : null}

                <div className="customer-draft-card__actions">
                  <Popconfirm
                    title="Hủy đơn nháp này?"
                    description="Khách sẽ không còn thấy đơn trên app."
                    onConfirm={() => void cancelItem(item.id)}
                  >
                    <Button
                      className="customer-draft-card__btn"
                      danger
                      loading={cancellingId === item.id}
                    >
                      Hủy
                    </Button>
                  </Popconfirm>
                  <Button
                    className="customer-draft-card__btn customer-draft-card__btn-pos"
                    type="primary"
                    icon={<ShoppingCartOutlined />}
                    loading={loadingPos === item.id}
                    onClick={() => void sendToPos(item.id)}
                  >
                    Đưa vào POS
                  </Button>
                </div>
              </article>
            );
          })
        )}
      </main>
    </div>
  );
}
