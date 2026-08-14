import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  App,
  Alert,
  Button,
  Empty,
  Input,
  InputNumber,
  Popconfirm,
  Segmented,
  Spin,
  Typography,
} from 'antd';
import { ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  createSaleReturn,
  fetchOpenShift,
  fetchSalesOrderById,
  searchSalesOrders,
} from '@/shared/api/sales.api';
import type { SalesOrderDetailFull, SalesOrderListItem, SalesShiftDetail } from '@/shared/api/sales.types';
import {
  SALES_PAYMENT_BANK,
  SALES_PAYMENT_CASH,
} from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import {
  blockedReturnReason,
  previewReturnRefund,
  resolveReturnPaymentSummary,
  returnableQuantity,
  splitReturnRefund,
} from '@/modules/returns/sales-return-pricing';
import { useCanSalesWrite } from '@/shared/auth/usePermission';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type SearchMode = 'document' | 'customer';
type RefundMethod = typeof SALES_PAYMENT_CASH | typeof SALES_PAYMENT_BANK;

const ORDER_STATUS_LABEL: Record<number, string> = {
  2: 'Đã bán',
  4: 'Đã trả một phần',
};

export function ReturnsPage() {
  const { message } = App.useApp();
  const canWrite = useCanSalesWrite();
  const warehouseId = usePosSession((s) => s.warehouseId);

  const [searchMode, setSearchMode] = useState<SearchMode>('customer');
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<SalesOrderListItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);

  const [shift, setShift] = useState<SalesShiftDetail | null>(null);
  const [shiftLoading, setShiftLoading] = useState(true);

  const [order, setOrder] = useState<SalesOrderDetailFull | null>(null);
  const [loadingOrder, setLoadingOrder] = useState(false);
  const [quantities, setQuantities] = useState<Record<string, number>>({});
  const [reason, setReason] = useState('');
  const [refundMethod, setRefundMethod] = useState<RefundMethod>(SALES_PAYMENT_CASH);
  const [saving, setSaving] = useState(false);

  const refreshShift = useCallback(async () => {
    if (!warehouseId) {
      setShift(null);
      setShiftLoading(false);
      return;
    }
    setShiftLoading(true);
    try {
      setShift(await fetchOpenShift(warehouseId));
    } catch {
      setShift(null);
    } finally {
      setShiftLoading(false);
    }
  }, [warehouseId]);

  useEffect(() => {
    void refreshShift();
  }, [refreshShift]);

  useEffect(() => {
    if (query.trim().length < 2) {
      setHits([]);
      setSearchError(null);
      return;
    }
    const timer = window.setTimeout(() => {
      void (async () => {
        setSearching(true);
        setSearchError(null);
        try {
          setHits(await searchSalesOrders(query.trim(), searchMode));
        } catch (error) {
          setHits([]);
          setSearchError(apiErrorMessage(error, 'Không tìm được đơn'));
        } finally {
          setSearching(false);
        }
      })();
    }, 300);
    return () => window.clearTimeout(timer);
  }, [query, searchMode]);

  const loadOrder = async (id: string) => {
    setLoadingOrder(true);
    try {
      const detail = await fetchSalesOrderById(id);
      if (detail.status !== 2 && detail.status !== 4) {
        message.warning('Chỉ trả hàng trên đơn đã hoàn tất / đã trả một phần');
        return;
      }
      setOrder(detail);
      const initial: Record<string, number> = {};
      for (const line of detail.items) {
        if (line.id) initial[line.id] = 0;
      }
      setQuantities(initial);
      setReason('');
      setRefundMethod(SALES_PAYMENT_CASH);
      setQuery('');
      setHits([]);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được đơn'));
    } finally {
      setLoadingOrder(false);
    }
  };

  const clearOrder = () => {
    setOrder(null);
    setQuantities({});
    setReason('');
  };

  const preview = useMemo(
    () => (order ? previewReturnRefund(order, quantities) : { totalRefund: 0, lines: [] }),
    [order, quantities],
  );

  const paymentSummary = useMemo(
    () => (order ? resolveReturnPaymentSummary(order) : { amountPaid: 0, outstanding: 0 }),
    [order],
  );

  const refundSplit = useMemo(
    () =>
      splitReturnRefund(preview.totalRefund, paymentSummary.outstanding, paymentSummary.amountPaid),
    [preview.totalRefund, paymentSummary],
  );

  const returnableLines = useMemo(
    () => order?.items.filter((line) => returnableQuantity(line) > 0) ?? [],
    [order],
  );

  const blockedLines = useMemo(
    () =>
      order?.items.filter((line) => {
        const reasonText = blockedReturnReason(line);
        return reasonText != null && (line.returnedQuantity ?? 0) < line.quantity;
      }) ?? [],
    [order],
  );

  const warehouseMismatch =
    Boolean(order?.warehouseId) &&
    Boolean(warehouseId) &&
    order!.warehouseId !== warehouseId;

  const shiftReady = Boolean(warehouseId && shift);
  const canSubmit =
    canWrite &&
    shiftReady &&
    !warehouseMismatch &&
    preview.lines.length > 0 &&
    !saving;

  const setLineQty = (lineId: string, value: number, max: number) => {
    const next = Math.max(0, Math.min(max, value));
    setQuantities((prev) => ({ ...prev, [lineId]: next }));
  };

  const returnAll = () => {
    if (!order) return;
    const next: Record<string, number> = {};
    for (const line of returnableLines) {
      if (line.id) next[line.id] = returnableQuantity(line);
    }
    setQuantities((prev) => ({ ...prev, ...next }));
  };

  const clearQty = () => {
    if (!order) return;
    const next: Record<string, number> = {};
    for (const line of order.items) {
      if (line.id) next[line.id] = 0;
    }
    setQuantities(next);
  };

  const submit = async () => {
    if (!order || !canSubmit) return;
    setSaving(true);
    try {
      const result = await createSaleReturn(order.id, {
        reason: reason.trim() || undefined,
        items: preview.lines.map((line) => ({
          salesOrderItemId: line.itemId,
          quantity: line.quantity,
        })),
        payments: [{ paymentMethod: refundMethod, amount: preview.totalRefund }],
      });
      message.success(`Đã trả · ${result.returnNumber} · hoàn ${formatMoney(result.totalRefund)}`);
      clearOrder();
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không trả được hàng'));
    } finally {
      setSaving(false);
    }
  };

  const searchPlaceholder =
    searchMode === 'customer' ? 'SĐT hoặc tên khách…' : 'Số hóa đơn (VD: HD-…)';

  const showSearch = !order && !loadingOrder;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Trả hàng"
        subtitle={
          order
            ? `${order.orderNumber} · ${formatMoney(order.totalAmount)}`
            : shiftReady
              ? `Ca ${shift?.shiftNumber ?? ''} · sẵn sàng trả`
              : 'Cần mở ca bán hàng'
        }
        backTo="/"
        right={
          showSearch ? (
            <Button
              type="text"
              className="chat-header-refresh"
              icon={<ReloadOutlined spin={shiftLoading} />}
              aria-label="Tải lại ca"
              onClick={() => void refreshShift()}
            />
          ) : undefined
        }
      />
      <main className={`staff-body returns-body${order ? ' returns-body--actions' : ''}`}>
        {!canWrite ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chỉ xem"
            description="Cần quyền bán hàng (sales.write) để xác nhận trả hàng."
          />
        ) : null}

        {!warehouseId ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chưa chọn kho POS"
            description="Vào Bán hàng / mở ca để chọn kho trước khi trả."
          />
        ) : shiftLoading ? (
          <div style={{ textAlign: 'center', padding: 12 }}>
            <Spin size="small" />
          </div>
        ) : !shift ? (
          <Alert
            type="error"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chưa mở ca"
            description="Cần mở ca cùng kho bán hàng trước khi trả. Tồn sẽ nhập lại kho khi xác nhận."
            action={
              <Button size="small" type="primary" onClick={() => void refreshShift()}>
                Kiểm tra lại
              </Button>
            }
          />
        ) : (
          <Alert
            type="success"
            showIcon
            style={{ marginBottom: 12 }}
            message={`Ca ${shift.shiftNumber} đang mở`}
            description={`${shift.warehouseName ?? 'Kho hiện tại'} · chỉ trả dòng có lô · khách không cầm bill vẫn tìm được đơn.`}
          />
        )}

        {loadingOrder ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : null}

        {showSearch ? (
          <>
            <Segmented
              block
              className="returns-mode"
              value={searchMode}
              onChange={(v) => {
                setSearchMode(v as SearchMode);
                setQuery('');
                setHits([]);
                setSearchError(null);
              }}
              options={[
                { label: 'Khách hàng', value: 'customer' },
                { label: 'Số HĐ', value: 'document' },
              ]}
            />
            <Input
              size="large"
              className="returns-search"
              prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
              placeholder={searchPlaceholder}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              allowClear
              autoFocus
            />
            <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 12 }}>
              {searchMode === 'customer'
                ? 'Gõ SĐT/tên → chọn đơn đã bán gần nhất.'
                : 'Nhập số hóa đơn nếu khách có bill.'}
            </Typography.Text>

            {searchError ? (
              <Alert type="error" showIcon message={searchError} style={{ marginBottom: 12 }} />
            ) : null}

            {searching ? (
              <div style={{ textAlign: 'center', padding: 16 }}>
                <Spin />
              </div>
            ) : null}

            {!searching && query.trim().length >= 2 && hits.length === 0 && !searchError ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="Không có đơn đã bán / trả một phần khớp"
              />
            ) : null}

            {hits.map((hit) => (
              <button
                key={hit.id}
                type="button"
                className="returns-hit"
                onClick={() => void loadOrder(hit.id)}
                disabled={!shiftReady || !canWrite}
              >
                <div className="returns-hit__head">
                  <Typography.Text strong>{hit.orderNumber}</Typography.Text>
                  <span className="returns-hit__status">
                    {ORDER_STATUS_LABEL[hit.status] ?? ''}
                  </span>
                </div>
                <div className="returns-hit__meta">
                  {hit.customerName ?? 'Khách lẻ'}
                  {hit.warehouseName ? ` · ${hit.warehouseName}` : ''}
                </div>
                <div className="returns-hit__meta">
                  {dayjs(hit.orderDate).format('DD/MM/YYYY HH:mm')}
                  {hit.itemCount != null ? ` · ${hit.itemCount} dòng` : ''}
                </div>
                <div className="returns-hit__amount">{formatMoney(hit.totalAmount)}</div>
              </button>
            ))}

            {query.trim().length < 2 ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="Nhập ít nhất 2 ký tự để tìm đơn trả hàng"
              />
            ) : null}
          </>
        ) : null}

        {order && !loadingOrder ? (
          <>
            <section className="returns-hero">
              <div className="returns-hero__row">
                <Typography.Text strong className="returns-hero__number">
                  {order.orderNumber}
                </Typography.Text>
                <span className="returns-hit__status">
                  {ORDER_STATUS_LABEL[order.status ?? 0] ?? ''}
                </span>
              </div>
              <div className="returns-hit__meta">
                {order.customerName ?? 'Khách lẻ'}
                {' · '}
                {dayjs(order.orderDate).format('DD/MM/YYYY HH:mm')}
              </div>
              <div className="returns-hit__meta">
                {order.warehouseName ?? 'Kho đơn'}
                {' · '}
                Tổng {formatMoney(order.totalAmount)}
                {paymentSummary.outstanding > 0.009
                  ? ` · còn nợ ${formatMoney(paymentSummary.outstanding)}`
                  : ''}
              </div>
              <Button type="link" className="returns-hero__switch" onClick={clearOrder}>
                ← Tìm đơn khác
              </Button>
            </section>

            {warehouseMismatch ? (
              <Alert
                type="error"
                showIcon
                style={{ marginBottom: 12 }}
                message="Sai kho"
                description={`Đơn thuộc ${order.warehouseName ?? 'kho khác'}. Đổi kho POS / mở ca đúng kho rồi thử lại.`}
              />
            ) : null}

            {returnableLines.length === 0 ? (
              <Alert
                type="warning"
                showIcon
                style={{ marginBottom: 12 }}
                message="Không còn dòng trả được"
                description="Đã trả hết hoặc các dòng thiếu lô kho (xử lý trên máy tính)."
              />
            ) : (
              <>
                <div className="returns-line-toolbar">
                  <Typography.Text strong>
                    Chọn SL trả ({returnableLines.length} dòng)
                  </Typography.Text>
                  <div className="returns-line-toolbar__actions">
                    <Button size="small" onClick={returnAll}>
                      Trả hết
                    </Button>
                    <Button size="small" onClick={clearQty}>
                      Xóa SL
                    </Button>
                  </div>
                </div>

                {returnableLines.map((line) => {
                  const max = returnableQuantity(line);
                  const qty = quantities[line.id ?? ''] ?? 0;
                  const linePreview = preview.lines.find((row) => row.itemId === line.id);
                  return (
                    <article key={line.id} className="returns-line-card">
                      <Typography.Text strong>{line.productName}</Typography.Text>
                      <div className="returns-hit__meta">
                        {line.productCode}
                        {line.batchNumber ? ` · Lô ${line.batchNumber}` : ''}
                        {' · '}
                        {line.unitName}
                      </div>
                      <div className="returns-hit__meta">
                        Đã bán {line.quantity}
                        {(line.returnedQuantity ?? 0) > 0
                          ? ` · đã trả ${line.returnedQuantity}`
                          : ''}
                        {' · còn trả '}
                        {max}
                      </div>
                      <div className="returns-line-card__qty">
                        <Button
                          size="middle"
                          disabled={qty <= 0}
                          onClick={() => setLineQty(line.id!, qty - 1, max)}
                        >
                          −
                        </Button>
                        <InputNumber
                          size="large"
                          min={0}
                          max={max}
                          value={qty}
                          onChange={(v) => setLineQty(line.id!, Number(v ?? 0), max)}
                          style={{ flex: 1 }}
                        />
                        <Button
                          size="middle"
                          disabled={qty >= max}
                          onClick={() => setLineQty(line.id!, qty + 1, max)}
                        >
                          +
                        </Button>
                        <Button size="middle" type="link" onClick={() => setLineQty(line.id!, max, max)}>
                          Max
                        </Button>
                      </div>
                      {linePreview && linePreview.refundAmount > 0 ? (
                        <div className="returns-line-card__refund">
                          Hoàn dòng ≈ {formatMoney(linePreview.refundAmount)}
                        </div>
                      ) : null}
                    </article>
                  );
                })}
              </>
            )}

            {blockedLines.length > 0 ? (
              <div className="returns-blocked">
                <Typography.Text type="secondary" className="returns-section-label">
                  Không trả được trên điện thoại ({blockedLines.length})
                </Typography.Text>
                {blockedLines.map((line) => (
                  <div key={line.id ?? line.productCode} className="returns-blocked__row">
                    <span>
                      {line.productCode} · {line.productName}
                    </span>
                    <span>{blockedReturnReason(line)}</span>
                  </div>
                ))}
              </div>
            ) : null}

            <Typography.Text type="secondary" className="returns-section-label">
              Hình thức hoàn tiền phần đã thu
            </Typography.Text>
            <Segmented
              block
              className="returns-method"
              value={refundMethod}
              onChange={(v) => setRefundMethod(v as RefundMethod)}
              options={[
                { label: 'Tiền mặt', value: SALES_PAYMENT_CASH },
                { label: 'Chuyển khoản', value: SALES_PAYMENT_BANK },
              ]}
            />

            <Typography.Text type="secondary" className="returns-section-label">
              Lý do trả (tuỳ chọn)
            </Typography.Text>
            <Input.TextArea
              rows={2}
              placeholder="VD: khách đổi ý, hàng lỗi…"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              style={{ marginBottom: 12 }}
            />

            <section className="returns-summary">
              <div className="returns-summary__row">
                <span>Tổng hoàn</span>
                <strong>{formatMoney(preview.totalRefund)}</strong>
              </div>
              {refundSplit.debtReduced > 0.009 ? (
                <div className="returns-summary__row returns-summary__row--muted">
                  <span>Giảm công nợ</span>
                  <span>{formatMoney(refundSplit.debtReduced)}</span>
                </div>
              ) : null}
              {refundSplit.cashRefund > 0.009 ? (
                <div className="returns-summary__row returns-summary__row--muted">
                  <span>
                    Hoàn {refundMethod === SALES_PAYMENT_BANK ? 'CK' : 'tiền mặt'}
                  </span>
                  <span>{formatMoney(refundSplit.cashRefund)}</span>
                </div>
              ) : null}
              <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8, fontSize: 12 }}>
                Xác nhận sẽ nhập lại tồn theo lô đã bán và ghi sổ hoàn tiền.
              </Typography.Text>
            </section>
          </>
        ) : null}
      </main>

      {order ? (
        <footer className="staff-footer returns-footer">
          <Typography.Text type="secondary" className="returns-footer__hint">
            {!shiftReady
              ? 'Mở ca trước khi xác nhận'
              : warehouseMismatch
                ? 'Đổi đúng kho đơn mới trả được'
                : preview.lines.length === 0
                  ? 'Nhập SL trả trên ít nhất 1 dòng'
                  : `${preview.lines.length} dòng · hoàn ${formatMoney(preview.totalRefund)}`}
          </Typography.Text>
          <Popconfirm
            title="Xác nhận trả hàng?"
            description={`${order.orderNumber} · hoàn ${formatMoney(preview.totalRefund)}. Tồn nhập lại kho.`}
            okText="Trả hàng"
            cancelText="Đóng"
            disabled={!canSubmit}
            onConfirm={() => void submit()}
          >
            <Button type="primary" block size="large" loading={saving} disabled={!canSubmit}>
              Xác nhận trả hàng
            </Button>
          </Popconfirm>
        </footer>
      ) : null}
    </div>
  );
}
