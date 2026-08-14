import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Input,
  Segmented,
  Spin,
  Tag,
  Typography,
} from 'antd';
import {
  PrinterOutlined,
  ReloadOutlined,
  RollbackOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  fetchReceiptSettings,
  fetchSalesOrderById,
  fetchSalesOrders,
} from '@/shared/api/sales.api';
import {
  STAFF_PAYMENT_METHOD_OPTIONS,
  type SalesOrderDetailFull,
  type SalesOrderListItem,
} from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { buildReceiptHtml, printReceiptDocument } from '@/modules/sales/receipt-print';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type DateFilter = 'today' | 'yesterday' | '7d';
type StatusFilter = 'sold' | 'cancelled' | 'all';

const PAGE_SIZE = 40;

const SALE_STATUS_LABEL: Record<number, string> = {
  1: 'Nháp',
  2: 'Hoàn tất',
  3: 'Đã hủy',
  4: 'Hoàn tiền',
};

const SALE_STATUS_TAG: Record<number, string> = {
  1: 'default',
  2: 'success',
  3: 'error',
  4: 'warning',
};

function dateRange(filter: DateFilter): { from: string; to: string } {
  if (filter === 'today') {
    const start = dayjs().startOf('day');
    return { from: start.toISOString(), to: start.add(1, 'day').toISOString() };
  }
  if (filter === 'yesterday') {
    const start = dayjs().subtract(1, 'day').startOf('day');
    return { from: start.toISOString(), to: start.add(1, 'day').toISOString() };
  }
  const start = dayjs().subtract(6, 'day').startOf('day');
  return { from: start.toISOString(), to: dayjs().startOf('day').add(1, 'day').toISOString() };
}

function formatWhen(value?: string | null): string {
  if (!value) return '—';
  const parsed = dayjs(value);
  if (!parsed.isValid()) return '—';
  const now = dayjs();
  if (now.isSame(parsed, 'day')) return parsed.format('HH:mm');
  if (now.subtract(1, 'day').isSame(parsed, 'day')) return `Hôm qua ${parsed.format('HH:mm')}`;
  return parsed.format('DD/MM HH:mm');
}

function paymentLabel(method: number): string {
  return STAFF_PAYMENT_METHOD_OPTIONS.find((o) => o.value === method)?.label ?? `PT ${method}`;
}

function toReceiptOrder(order: SalesOrderDetailFull) {
  return {
    id: order.id,
    orderNumber: order.orderNumber,
    orderDate: order.orderDate,
    totalAmount: order.totalAmount,
    amountPaid: order.amountPaid,
    customerName: order.customerName,
    items: order.items.map((i) => ({
      productCode: i.productCode,
      productName: i.productName,
      unitName: i.unitName,
      quantity: i.quantity,
      unitPrice: i.unitPrice,
      lineTotal: i.lineTotal,
      batchNumber: i.batchNumber,
    })),
    payments: order.payments,
  };
}

function matchesStatus(item: SalesOrderListItem, filter: StatusFilter): boolean {
  if (filter === 'all') return item.status !== 1;
  if (filter === 'cancelled') return item.status === 3;
  return item.status === 2 || item.status === 4;
}

function canReprintStatus(status?: number): boolean {
  return status === 2 || status === 4;
}

export function OrdersPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [dateFilter, setDateFilter] = useState<DateFilter>('today');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('sold');
  const [search, setSearch] = useState('');
  const [searchApplied, setSearchApplied] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [items, setItems] = useState<SalesOrderListItem[]>([]);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const [order, setOrder] = useState<SalesOrderDetailFull | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [printingId, setPrintingId] = useState<string | null>(null);
  const [showBill, setShowBill] = useState(false);
  const [storeName, setStoreName] = useState('Nhà thuốc');

  useEffect(() => {
    void fetchReceiptSettings().then((s) => setStoreName(s.name));
  }, []);

  useEffect(() => {
    const timer = window.setTimeout(() => setSearchApplied(search.trim()), 280);
    return () => window.clearTimeout(timer);
  }, [search]);

  const fetchPage = useCallback(
    async (pageNum: number) => {
      const range = dateRange(dateFilter);
      const result = await fetchSalesOrders({
        page: pageNum,
        pageSize: PAGE_SIZE,
        search: searchApplied || undefined,
        status: statusFilter === 'cancelled' ? 3 : undefined,
        ...range,
      });
      const rows = result.items
        .filter((row) => matchesStatus(row, statusFilter))
        .sort((a, b) => dayjs(b.orderDate).valueOf() - dayjs(a.orderDate).valueOf());
      const more =
        result.items.length >= PAGE_SIZE || pageNum * PAGE_SIZE < (result.total ?? 0);
      return { rows, more, apiTotal: result.total };
    },
    [dateFilter, searchApplied, statusFilter],
  );

  const load = useCallback(
    async (mode: 'full' | 'refresh' = 'full') => {
      if (mode === 'full') {
        setLoading(true);
        setLoadError(null);
      } else {
        setRefreshing(true);
      }
      try {
        const { rows, more } = await fetchPage(1);
        setItems(rows);
        setPage(1);
        setHasMore(more);
        setLoadError(null);
      } catch (error) {
        const text = apiErrorMessage(error, 'Không tải được danh sách đơn bán');
        if (mode === 'full') {
          setItems([]);
          setHasMore(false);
          setLoadError(text);
        } else {
          message.error(text);
        }
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [fetchPage, message],
  );

  useEffect(() => {
    void load('full');
  }, [load]);

  const loadMore = async () => {
    if (!hasMore || loadingMore) return;
    setLoadingMore(true);
    try {
      const next = page + 1;
      const { rows, more } = await fetchPage(next);
      setItems((prev) => {
        const seen = new Set(prev.map((r) => r.id));
        return [...prev, ...rows.filter((r) => !seen.has(r.id))];
      });
      setPage(next);
      setHasMore(more);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải thêm được'));
    } finally {
      setLoadingMore(false);
    }
  };

  const listTotalAmount = useMemo(
    () => items.reduce((sum, row) => sum + (row.totalAmount || 0), 0),
    [items],
  );

  const receiptHtml = useMemo(() => {
    if (!order) return '';
    return buildReceiptHtml(toReceiptOrder(order), { name: storeName });
  }, [order, storeName]);

  const openOrder = async (id: string) => {
    setDetailLoading(true);
    setShowBill(false);
    try {
      const detail = await fetchSalesOrderById(id);
      setOrder(detail);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn'));
    } finally {
      setDetailLoading(false);
    }
  };

  const printOrder = async (detail: SalesOrderDetailFull) => {
    if (!canReprintStatus(detail.status)) {
      message.warning('Chỉ in lại đơn đã bán / hoàn tiền');
      return;
    }
    setPrintingId(detail.id);
    try {
      const html = buildReceiptHtml(toReceiptOrder(detail), { name: storeName });
      printReceiptDocument(html);
    } catch {
      message.error('Không in được bill');
    } finally {
      setPrintingId(null);
    }
  };

  const quickPrint = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setPrintingId(id);
    try {
      const detail = await fetchSalesOrderById(id);
      if (!canReprintStatus(detail.status)) {
        message.warning('Chỉ in lại đơn đã bán');
        return;
      }
      printReceiptDocument(buildReceiptHtml(toReceiptOrder(detail), { name: storeName }));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không in được bill'));
    } finally {
      setPrintingId(null);
    }
  };

  const dateLabel =
    dateFilter === 'today' ? 'hôm nay' : dateFilter === 'yesterday' ? 'hôm qua' : '7 ngày';

  const subtitle = loadError
    ? 'Không tải được danh sách'
    : order
      ? order.orderNumber
      : `${items.length}${hasMore ? '+' : ''} đơn · ${dateLabel}`;

  if (order) {
    const canReprint = canReprintStatus(order.status);
    const outstanding = order.outstanding ?? Math.max(0, order.totalAmount - (order.amountPaid ?? 0));
    return (
      <div className="staff-shell">
        <StaffPageHeader
          title="Chi tiết đơn"
          subtitle={subtitle}
          backTo="/orders"
          onBack={() => {
            setOrder(null);
            setShowBill(false);
          }}
        />
        <main className="staff-body orders-detail-body">
          <section className="orders-hero">
            <div className="orders-hero__top">
              <div>
                <Typography.Text strong className="orders-hero__number">
                  {order.orderNumber}
                </Typography.Text>
                <div className="orders-hero__meta">
                  {formatWhen(order.orderDate)}
                  {order.warehouseName ? ` · ${order.warehouseName}` : ''}
                </div>
              </div>
              <Tag color={SALE_STATUS_TAG[order.status ?? 2] ?? 'default'}>
                {SALE_STATUS_LABEL[order.status ?? 2] ?? `TT ${order.status}`}
              </Tag>
            </div>

            <div className="orders-hero__customer">
              <span className="orders-hero__customer-name">{order.customerName?.trim() || 'Khách lẻ'}</span>
            </div>

            <div className="orders-hero__totals">
              <div>
                <span>Tổng đơn</span>
                <strong>{formatMoney(order.totalAmount)}</strong>
              </div>
              <div>
                <span>Đã thu</span>
                <strong className="is-paid">{formatMoney(order.amountPaid ?? 0)}</strong>
              </div>
              {outstanding > 0.0001 ? (
                <div>
                  <span>Còn nợ</span>
                  <strong className="is-debt">{formatMoney(outstanding)}</strong>
                </div>
              ) : null}
            </div>

            {order.payments?.length ? (
              <div className="orders-hero__pays">
                {order.payments.map((p, idx) => (
                  <span key={`${p.paymentMethod}-${idx}`} className="orders-pay-chip">
                    {paymentLabel(p.paymentMethod)} · {formatMoney(p.amount)}
                  </span>
                ))}
              </div>
            ) : null}
          </section>

          <Typography.Text className="orders-section-label">
            Sản phẩm ({order.items.length})
          </Typography.Text>
          <div className="orders-lines">
            {order.items.map((line, idx) => (
              <article key={line.id ?? `${line.productCode}-${idx}`} className="orders-line">
                <div className="orders-line__main">
                  <Typography.Text strong className="orders-line__name">
                    {line.productName}
                  </Typography.Text>
                  <div className="orders-line__meta">
                    {line.productCode ? `${line.productCode} · ` : ''}
                    {line.quantity} {line.unitName}
                    {line.batchNumber ? ` · Lô ${line.batchNumber}` : ''}
                  </div>
                </div>
                <div className="orders-line__right">
                  <span className="orders-line__total">{formatMoney(line.lineTotal)}</span>
                  <span className="orders-line__unit">
                    {formatMoney(line.unitPrice)}/{line.unitName}
                  </span>
                </div>
              </article>
            ))}
          </div>

          {canReprint && showBill ? (
            <div className="orders-bill-preview receipt-print-area">
              <iframe title="receipt-reprint" srcDoc={receiptHtml} />
            </div>
          ) : null}
        </main>

        <footer className="staff-footer no-print orders-footer">
          {canReprint ? (
            <div className="orders-footer__actions">
              <Button size="large" onClick={() => setShowBill((v) => !v)}>
                {showBill ? 'Ẩn bill' : 'Xem bill'}
              </Button>
              <Button
                type="primary"
                size="large"
                icon={<PrinterOutlined />}
                loading={printingId === order.id}
                onClick={() => void printOrder(order)}
              >
                In lại
              </Button>
            </div>
          ) : null}
          <div className="orders-footer__secondary">
            {order.status === 2 ? (
              <Button
                block
                size="large"
                icon={<RollbackOutlined />}
                onClick={() => navigate('/returns')}
              >
                Trả hàng
              </Button>
            ) : null}
            <Button
              block
              size="large"
              icon={<ShoppingCartOutlined />}
              onClick={() => navigate('/pos')}
            >
              Sang POS
            </Button>
          </div>
        </footer>
      </div>
    );
  }

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Đơn bán"
        subtitle={subtitle}
        backTo="/"
        right={
          <Button
            type="text"
            className="chat-header-refresh"
            icon={<ReloadOutlined spin={refreshing || loading || detailLoading} />}
            aria-label="Tải lại"
            onClick={() => void load(loadError ? 'full' : 'refresh')}
          />
        }
      />
      <main className="staff-body orders-body">
        {loadError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được đơn bán"
            description={loadError}
            action={
              <Button size="small" type="primary" loading={loading} onClick={() => void load('full')}>
                Thử lại
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : (
          <div className="orders-summary">
            <div>
              <span className="orders-summary__label">Đang xem</span>
              <strong>
                {items.length}
                {hasMore ? '+' : ''}
              </strong>
            </div>
            <div>
              <span className="orders-summary__label">Tổng (trang này)</span>
              <strong className="orders-summary__money">{formatMoney(listTotalAmount)}</strong>
            </div>
          </div>
        )}

        <Input
          allowClear
          size="large"
          className="orders-search"
          prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
          placeholder="Tìm số HĐ, tên khách, SĐT…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />

        <Segmented
          block
          className="orders-date-filter"
          value={dateFilter}
          onChange={(v) => setDateFilter(v as DateFilter)}
          options={[
            { label: 'Hôm nay', value: 'today' },
            { label: 'Hôm qua', value: 'yesterday' },
            { label: '7 ngày', value: '7d' },
          ]}
        />

        <Segmented
          block
          className="orders-status-filter"
          value={statusFilter}
          onChange={(v) => setStatusFilter(v as StatusFilter)}
          options={[
            { label: 'Đã bán', value: 'sold' },
            { label: 'Đã hủy', value: 'cancelled' },
            { label: 'Tất cả', value: 'all' },
          ]}
        />

        {detailLoading ? (
          <div className="orders-loading">
            <Spin tip="Đang mở đơn…" />
          </div>
        ) : null}

        {loading ? (
          <div className="orders-loading">
            <Spin />
          </div>
        ) : items.length === 0 ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              searchApplied
                ? 'Không tìm thấy đơn phù hợp'
                : dateFilter === 'today'
                  ? 'Chưa có đơn bán hôm nay'
                  : 'Chưa có đơn trong khoảng đã chọn'
            }
          />
        ) : (
          <>
            <div className="orders-list orders-list--compact">
              {items.map((row) => {
                const reprintable = canReprintStatus(row.status);
                return (
                  <button
                    key={row.id}
                    type="button"
                    className="orders-row"
                    onClick={() => void openOrder(row.id)}
                  >
                    <div className="orders-row__left">
                      <div className="orders-row__top">
                        <span className="orders-row__number">{row.orderNumber}</span>
                        <span className="orders-row__when">{formatWhen(row.orderDate)}</span>
                        {row.status !== 2 ? (
                          <Tag
                            color={SALE_STATUS_TAG[row.status] ?? 'default'}
                            className="orders-row__tag"
                          >
                            {SALE_STATUS_LABEL[row.status] ?? row.status}
                          </Tag>
                        ) : null}
                      </div>
                      <div className="orders-row__customer">
                        {row.customerName?.trim() || 'Khách lẻ'}
                        {row.itemCount != null && row.itemCount > 0 ? ` · ${row.itemCount} SP` : ''}
                      </div>
                    </div>
                    <div className="orders-row__right">
                      <span className="orders-row__amount">{formatMoney(row.totalAmount)}</span>
                      {reprintable ? (
                        <span
                          role="button"
                          tabIndex={0}
                          className="orders-row__print"
                          aria-label="In lại"
                          onClick={(e) => void quickPrint(row.id, e)}
                          onKeyDown={(e) => {
                            if (e.key === 'Enter' || e.key === ' ') {
                              e.preventDefault();
                              void quickPrint(row.id, e as unknown as React.MouseEvent);
                            }
                          }}
                        >
                          {printingId === row.id ? <Spin size="small" /> : <PrinterOutlined />}
                        </span>
                      ) : (
                        <span className="orders-row__chevron">›</span>
                      )}
                    </div>
                  </button>
                );
              })}
            </div>

            {hasMore ? (
              <Button
                block
                size="large"
                className="orders-load-more"
                loading={loadingMore}
                onClick={() => void loadMore()}
              >
                Tải thêm đơn
              </Button>
            ) : items.length >= PAGE_SIZE ? (
              <Typography.Text type="secondary" className="orders-end-hint">
                Đã hết trong khoảng đã chọn
              </Typography.Text>
            ) : null}
          </>
        )}
      </main>
    </div>
  );
}
