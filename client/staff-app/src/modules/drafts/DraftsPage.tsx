import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Input,
  Popconfirm,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  ReloadOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  cancelDraftSale,
  fetchDraftSalesOrders,
  fetchSalesOrder,
} from '@/shared/api/sales.api';
import { fetchCustomerById } from '@/shared/api/customer.api';
import type { SalesOrderListItem } from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import {
  loadDraftCartLines,
  orderDiscountFromDetail,
  persistPosDraftEdit,
} from '@/modules/sales/sales-draft-helpers';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return `Hôm nay ${parsed.format('HH:mm')}`;
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM HH:mm');
}

function sortDrafts(items: SalesOrderListItem[]): SalesOrderListItem[] {
  return [...items].sort((a, b) => {
    const ta = dayjs(a.orderDate).valueOf() || 0;
    const tb = dayjs(b.orderDate).valueOf() || 0;
    return tb - ta;
  });
}

export function DraftsPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const loadDraftIntoSession = usePosSession((s) => s.loadDraftIntoSession);
  const [items, setItems] = useState<SalesOrderListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
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
        setItems(await fetchDraftSalesOrders());
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được đơn nháp');
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

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    let rows = sortDrafts(items);
    if (q) {
      rows = rows.filter(
        (i) =>
          i.orderNumber.toLowerCase().includes(q) ||
          (i.customerName ?? '').toLowerCase().includes(q) ||
          (i.warehouseName ?? '').toLowerCase().includes(q) ||
          (i.shiftNumber ?? '').toLowerCase().includes(q),
      );
    }
    return rows;
  }, [items, query]);

  const openInPos = async (id: string) => {
    setLoadingPos(id);
    try {
      const order = await fetchSalesOrder(id);
      if (order.status !== 1) {
        message.warning('Đơn không còn ở trạng thái nháp');
        await load('refresh');
        return;
      }
      if (!order.warehouseId) {
        message.error('Đơn nháp thiếu kho — mở lại từ POS không được.');
        return;
      }
      const lines = await loadDraftCartLines(order);
      if (lines.length === 0) {
        message.warning('Đơn nháp không có sản phẩm hợp lệ');
        return;
      }
      let customer = null;
      if (order.customerId) {
        try {
          const row = await fetchCustomerById(order.customerId);
          customer = {
            id: row.id,
            customerCode: row.customerCode,
            fullName: row.fullName,
            phone: row.phone,
            allowCredit: row.allowCredit,
          };
        } catch {
          customer = order.customerName
            ? {
                id: order.customerId,
                customerCode: '',
                fullName: order.customerName,
                phone: '',
              }
            : null;
        }
      }
      loadDraftIntoSession({
        warehouseId: order.warehouseId,
        cart: lines,
        customer,
        orderDiscount: orderDiscountFromDetail(order),
        draftId: order.id,
        draftNumber: order.orderNumber,
      });
      persistPosDraftEdit(order.id);
      message.success(`Đã mở nháp ${order.orderNumber}`);
      navigate('/pos');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không mở được đơn nháp'));
    } finally {
      setLoadingPos(null);
    }
  };

  const cancelItem = async (id: string) => {
    setCancellingId(id);
    try {
      await cancelDraftSale(id);
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
        title="Đơn nháp"
        subtitle={
          loadError
            ? 'Không tải được danh sách'
            : items.length > 0
              ? `${items.length} đơn tạm lưu tại quầy`
              : 'Không có đơn đang mở'
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
            Nháp quầy (Lưu tạm) · mở lại để sửa hoặc thanh toán. Khác với đơn nháp app khách.
          </Typography.Text>
        )}

        <Input
          size="large"
          allowClear
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tìm mã đơn, khách, kho, ca…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          disabled={Boolean(loadError) && items.length === 0}
          style={{ marginBottom: 12 }}
        />

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
                : 'Không có đơn nháp. Trên POS bấm Lưu tạm để tạo.'
            }
          />
        ) : (
          visible.map((item) => (
            <article key={item.id} className="counter-draft-card">
              <div className="counter-draft-card__head">
                <div className="counter-draft-card__ids">
                  <Typography.Text strong className="counter-draft-card__number">
                    {item.orderNumber}
                  </Typography.Text>
                  <Typography.Text type="secondary" className="counter-draft-card__when">
                    {formatWhen(item.orderDate)}
                  </Typography.Text>
                </div>
                <Tag color="gold">Nháp</Tag>
              </div>

              <div className="counter-draft-card__customer">
                <Typography.Text strong>{item.customerName || 'Khách lẻ'}</Typography.Text>
                <div className="counter-draft-card__meta">
                  <span>{item.itemCount ?? 0} SP</span>
                  {item.warehouseName ? <span>· {item.warehouseName}</span> : null}
                  {item.shiftNumber ? <span>· Ca {item.shiftNumber}</span> : null}
                </div>
              </div>

              <div className="counter-draft-card__amount">{formatMoney(item.totalAmount)}</div>

              <div className="counter-draft-card__actions">
                <Popconfirm
                  title="Hủy đơn nháp này?"
                  description="Không thể hoàn tác. Khách/giỏ sẽ mất trên nháp này."
                  onConfirm={() => void cancelItem(item.id)}
                >
                  <Button
                    className="counter-draft-card__btn"
                    danger
                    loading={cancellingId === item.id}
                  >
                    Hủy
                  </Button>
                </Popconfirm>
                <Button
                  className="counter-draft-card__btn counter-draft-card__btn-pos"
                  type="primary"
                  icon={<ShoppingCartOutlined />}
                  loading={loadingPos === item.id}
                  onClick={() => void openInPos(item.id)}
                >
                  Mở trong POS
                </Button>
              </div>
            </article>
          ))
        )}
      </main>
    </div>
  );
}
