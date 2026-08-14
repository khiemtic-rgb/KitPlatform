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
  Select,
  Spin,
  Typography,
} from 'antd';
import {
  PhoneOutlined,
  ReloadOutlined,
  SearchOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import {
  createAndPostCustomerPayment,
  fetchCustomerReceivablesDetail,
  fetchReceivablesSummary,
  type CustomerReceivablesRow,
  type CustomerReceivablesSummary,
} from '@/shared/api/receivables.api';
import { fetchCustomerList } from '@/shared/api/customer.api';
import type { CustomerAdminListItem } from '@/shared/api/customer.types';
import {
  SALES_PAYMENT_BANK,
  SALES_PAYMENT_CARD,
  SALES_PAYMENT_CASH,
  SALES_PAYMENT_EWALLET,
} from '@/shared/api/sales.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';
import { useCanSalesWrite } from '@/shared/auth/usePermission';
import { usePosSession } from '@/modules/pos/pos-session.store';
import { StaffPageHeader } from '@/shared/layout/StaffPageHeader';

type PayMethod = typeof SALES_PAYMENT_CASH | typeof SALES_PAYMENT_BANK | typeof SALES_PAYMENT_CARD | typeof SALES_PAYMENT_EWALLET;

const PAY_OPTIONS: { label: string; value: PayMethod }[] = [
  { label: 'Tiền mặt', value: SALES_PAYMENT_CASH },
  { label: 'CK', value: SALES_PAYMENT_BANK },
  { label: 'Thẻ', value: SALES_PAYMENT_CARD },
  { label: 'Ví', value: SALES_PAYMENT_EWALLET },
];

function payMethodLabel(method: PayMethod): string {
  return PAY_OPTIONS.find((o) => o.value === method)?.label ?? 'Thanh toán';
}

function formatOrderDate(value?: string): string {
  if (!value) return '';
  const parsed = dayjs(value);
  return parsed.isValid() ? parsed.format('DD/MM') : '';
}

export function CollectPaymentPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanSalesWrite();
  const warehouseId = usePosSession((s) => s.warehouseId);

  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<CustomerAdminListItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [debtors, setDebtors] = useState<CustomerReceivablesRow[]>([]);
  const [loadingDebtors, setLoadingDebtors] = useState(true);
  const [debtorsError, setDebtorsError] = useState<string | null>(null);

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [receivable, setReceivable] = useState<CustomerReceivablesSummary | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [amount, setAmount] = useState(0);
  const [method, setMethod] = useState<PayMethod>(SALES_PAYMENT_CASH);
  const [notes, setNotes] = useState('');
  const [salesOrderId, setSalesOrderId] = useState<string | undefined>();
  const [saving, setSaving] = useState(false);

  const loadDebtors = useCallback(async () => {
    setLoadingDebtors(true);
    setDebtorsError(null);
    try {
      const rows = await fetchReceivablesSummary(warehouseId ?? undefined);
      setDebtors(rows.slice(0, 30));
    } catch (error) {
      setDebtors([]);
      setDebtorsError(apiErrorMessage(error, 'Không tải được danh sách công nợ'));
    } finally {
      setLoadingDebtors(false);
    }
  }, [warehouseId]);

  useEffect(() => {
    if (!selectedId) void loadDebtors();
  }, [loadDebtors, selectedId]);

  useEffect(() => {
    if (query.trim().length < 2) {
      setHits([]);
      return;
    }
    const timer = window.setTimeout(() => {
      void (async () => {
        setSearching(true);
        try {
          setHits((await fetchCustomerList(query.trim())).items);
        } catch (error) {
          setHits([]);
          message.error(apiErrorMessage(error, 'Không tìm được khách'));
        } finally {
          setSearching(false);
        }
      })();
    }, 280);
    return () => window.clearTimeout(timer);
  }, [message, query]);

  const loadDetail = useCallback(
    async (
      customerId: string,
      hint?: { customerCode: string; fullName: string; phone?: string },
    ) => {
      setSelectedId(customerId);
      setLoadingDetail(true);
      setDetailError(null);
      setReceivable(null);
      setNotes('');
      setSalesOrderId(undefined);
      setMethod(SALES_PAYMENT_CASH);
      try {
        const detail = await fetchCustomerReceivablesDetail(customerId, hint);
        setReceivable(detail);
        setAmount(detail.totalReceivable > 0 ? detail.totalReceivable : 0);
      } catch (error) {
        setDetailError(apiErrorMessage(error, 'Không tải được công nợ'));
        setSelectedId(null);
      } finally {
        setLoadingDetail(false);
      }
    },
    [],
  );

  const pickCustomer = (customer: CustomerAdminListItem) => {
    setQuery('');
    setHits([]);
    void loadDetail(customer.id, {
      customerCode: customer.customerCode,
      fullName: customer.fullName,
      phone: customer.phone,
    });
  };

  const pickDebtor = (row: CustomerReceivablesRow) => {
    setQuery('');
    setHits([]);
    void loadDetail(row.customerId, {
      customerCode: row.customerCode,
      fullName: row.customerName,
      phone: row.customerPhone ?? undefined,
    });
  };

  const clearCustomer = () => {
    setSelectedId(null);
    setReceivable(null);
    setDetailError(null);
    setAmount(0);
    setNotes('');
    setSalesOrderId(undefined);
  };

  const remaining = useMemo(() => {
    if (!receivable) return 0;
    return Math.max(0, receivable.totalReceivable - amount);
  }, [amount, receivable]);

  const canSubmit =
    canWrite &&
    Boolean(selectedId) &&
    Boolean(receivable) &&
    (receivable?.totalReceivable ?? 0) > 0 &&
    amount > 0 &&
    amount <= (receivable?.totalReceivable ?? 0) + 0.01;

  const submit = async () => {
    if (!selectedId || !receivable || !canSubmit) return;
    if (amount > receivable.totalReceivable + 0.01) {
      message.warning('Số tiền vượt công nợ');
      return;
    }
    setSaving(true);
    try {
      const result = await createAndPostCustomerPayment({
        customerId: selectedId,
        amount,
        paymentMethod: method,
        salesOrderId,
        notes: notes.trim() || undefined,
        customerName: receivable.customerName,
        customerCode: receivable.customerCode,
      });
      message.success(`Đã thu ${formatMoney(amount)} · ${result.paymentNumber}`);
      navigate('/collect/receipt', { replace: true, state: { payment: result } });
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không thu được tiền'));
    } finally {
      setSaving(false);
    }
  };

  const showSearch = !selectedId && !loadingDetail;

  return (
    <div className="staff-shell">
      <StaffPageHeader
        title="Thu công nợ"
        subtitle={
          receivable
            ? `${receivable.customerName} · nợ ${formatMoney(receivable.totalReceivable)}`
            : debtors.length > 0
              ? `${debtors.length} khách còn nợ`
              : 'Thu tiền khách trả nợ tại quầy'
        }
        backTo="/"
        right={
          showSearch ? (
            <Button
              type="text"
              className="chat-header-refresh"
              icon={<ReloadOutlined spin={loadingDebtors} />}
              aria-label="Tải lại"
              onClick={() => void loadDebtors()}
            />
          ) : undefined
        }
      />
      <main className={`staff-body collect-body${canSubmit ? ' collect-body--actions' : ''}`}>
        {!canWrite ? (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message="Chỉ xem"
            description="Cần quyền bán hàng (sales.write) để ghi sổ phiếu thu."
          />
        ) : null}

        {showSearch ? (
          <>
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message="Điều kiện thu nợ"
              description="Chọn khách còn công nợ → nhập số tiền & hình thức → xác nhận. Phiếu thu được ghi sổ ngay, không hoàn tác dễ dàng."
            />

            <Input
              size="large"
              className="collect-search"
              prefix={<SearchOutlined style={{ color: '#94a3b8' }} />}
              placeholder="SĐT, tên hoặc mã khách…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              allowClear
              autoFocus
            />

            {searching ? (
              <div style={{ textAlign: 'center', padding: 16 }}>
                <Spin />
              </div>
            ) : null}

            {query.trim().length >= 2 && !searching && hits.length === 0 ? (
              <Empty
                image={Empty.PRESENTED_IMAGE_SIMPLE}
                description="Không tìm thấy khách — thử SĐT hoặc tên khác"
              />
            ) : null}

            {hits.length > 0 ? (
              <div className="collect-section">
                <Typography.Text type="secondary" className="collect-section__label">
                  Kết quả tìm ({hits.length})
                </Typography.Text>
                {hits.map((c) => (
                  <button
                    key={c.id}
                    type="button"
                    className="collect-hit"
                    onClick={() => pickCustomer(c)}
                  >
                    <div className="collect-hit__main">
                      <Typography.Text strong>{c.fullName}</Typography.Text>
                      <div className="collect-hit__meta">
                        {c.phone || '—'} · {c.customerCode}
                        {c.allowCredit ? ' · Được ghi nợ' : ''}
                      </div>
                    </div>
                    {(c.currentOutstanding ?? 0) > 0.009 ? (
                      <div className="collect-hit__debt">{formatMoney(c.currentOutstanding ?? 0)}</div>
                    ) : (
                      <UserOutlined style={{ color: '#94a3b8' }} />
                    )}
                  </button>
                ))}
              </div>
            ) : null}

            {query.trim().length < 2 ? (
              <div className="collect-section">
                <Typography.Text type="secondary" className="collect-section__label">
                  Khách còn nợ (ưu tiên thu)
                </Typography.Text>
                {debtorsError ? (
                  <Alert
                    type="error"
                    showIcon
                    message="Không tải danh sách công nợ"
                    description={debtorsError}
                    action={
                      <Button size="small" type="primary" onClick={() => void loadDebtors()}>
                        Thử lại
                      </Button>
                    }
                  />
                ) : loadingDebtors ? (
                  <div style={{ textAlign: 'center', padding: 16 }}>
                    <Spin />
                  </div>
                ) : debtors.length === 0 ? (
                  <Empty
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                    description="Không có khách còn công nợ. Tìm theo SĐT/tên nếu cần."
                  />
                ) : (
                  debtors.map((row) => (
                    <button
                      key={row.customerId}
                      type="button"
                      className="collect-hit"
                      onClick={() => pickDebtor(row)}
                    >
                      <div className="collect-hit__main">
                        <Typography.Text strong>{row.customerName}</Typography.Text>
                        <div className="collect-hit__meta">
                          {row.customerPhone || '—'} · {row.customerCode}
                          {row.openDocumentCount > 0 ? ` · ${row.openDocumentCount} đơn` : ''}
                        </div>
                      </div>
                      <div className="collect-hit__debt">{formatMoney(row.totalReceivable)}</div>
                    </button>
                  ))
                )}
              </div>
            ) : null}
          </>
        ) : null}

        {loadingDetail ? (
          <div style={{ textAlign: 'center', padding: 24 }}>
            <Spin />
          </div>
        ) : null}

        {detailError ? (
          <Alert
            type="error"
            showIcon
            message="Không tải được công nợ"
            description={detailError}
            action={
              <Button size="small" onClick={clearCustomer}>
                Chọn lại
              </Button>
            }
          />
        ) : null}

        {receivable && !loadingDetail ? (
          <>
            <section className="collect-hero">
              <div className="collect-hero__top">
                <div>
                  <Typography.Text strong className="collect-hero__name">
                    {receivable.customerName}
                  </Typography.Text>
                  <div className="collect-hero__meta">
                    {receivable.customerCode}
                    {receivable.customerPhone ? ` · ${receivable.customerPhone}` : ''}
                  </div>
                </div>
                {receivable.customerPhone ? (
                  <Button
                    type="default"
                    icon={<PhoneOutlined />}
                    href={`tel:${receivable.customerPhone}`}
                    aria-label="Gọi khách"
                  />
                ) : null}
              </div>
              <div className="collect-hero__balance">
                <span>Công nợ hiện tại</span>
                <strong>{formatMoney(receivable.totalReceivable)}</strong>
              </div>
              {(receivable.unappliedCredit ?? 0) > 0.009 ? (
                <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 6 }}>
                  Có tín dụng chưa áp: {formatMoney(receivable.unappliedCredit ?? 0)}
                </Typography.Text>
              ) : null}
              <Button type="link" className="collect-hero__switch" onClick={clearCustomer}>
                ← Chọn khách khác
              </Button>
            </section>

            {receivable.totalReceivable <= 0 ? (
              <Alert type="success" showIcon message="Khách không còn công nợ" description="Không cần thu thêm." />
            ) : (
              <>
                <Typography.Text type="secondary" className="collect-field-label">
                  Số tiền thu
                </Typography.Text>
                <InputNumber
                  size="large"
                  className="collect-amount"
                  style={{ width: '100%' }}
                  min={0}
                  max={receivable.totalReceivable}
                  value={amount}
                  onChange={(v) => setAmount(Number(v ?? 0))}
                  formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.')}
                  parser={(v) => Number((v ?? '').replace(/\./g, ''))}
                />
                <div className="collect-presets">
                  <Button size="middle" onClick={() => setAmount(receivable.totalReceivable)}>
                    Thu hết
                  </Button>
                  <Button
                    size="middle"
                    onClick={() => setAmount(Math.round(receivable.totalReceivable / 2))}
                  >
                    50%
                  </Button>
                  {[50_000, 100_000, 200_000, 500_000]
                    .filter((n) => n < receivable.totalReceivable)
                    .map((n) => (
                      <Button key={n} size="middle" onClick={() => setAmount(n)}>
                        {formatMoney(n)}
                      </Button>
                    ))}
                </div>
                <div className="collect-remaining">
                  Còn lại sau thu: <strong>{formatMoney(remaining)}</strong>
                </div>

                <Typography.Text type="secondary" className="collect-field-label">
                  Hình thức
                </Typography.Text>
                <Segmented
                  block
                  className="collect-method"
                  value={method}
                  onChange={(v) => setMethod(v as PayMethod)}
                  options={PAY_OPTIONS}
                />

                {receivable.lines.length > 0 ? (
                  <>
                    <Typography.Text type="secondary" className="collect-field-label">
                      Áp vào đơn (tuỳ chọn)
                    </Typography.Text>
                    <Select
                      size="large"
                      allowClear
                      style={{ width: '100%', marginBottom: 12 }}
                      placeholder="Phân bổ tự động theo FIFO nếu bỏ trống"
                      value={salesOrderId}
                      onChange={(v) => setSalesOrderId(v)}
                      options={receivable.lines.map((line) => ({
                        value: line.salesOrderId,
                        label: `${line.orderNumber} · nợ ${formatMoney(line.outstanding)}${
                          line.daysOutstanding != null ? ` · ${line.daysOutstanding} ngày` : ''
                        }`,
                      }))}
                    />
                  </>
                ) : null}

                <Typography.Text type="secondary" className="collect-field-label">
                  Ghi chú
                </Typography.Text>
                <Input.TextArea
                  rows={2}
                  placeholder="VD: khách trả một phần, CK lúc 14:00…"
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  style={{ marginBottom: 14 }}
                />

                {receivable.lines.length > 0 ? (
                  <div className="collect-section">
                    <Typography.Text type="secondary" className="collect-section__label">
                      Công nợ theo đơn ({receivable.lines.length})
                    </Typography.Text>
                    {receivable.lines.map((line) => (
                      <div key={line.salesOrderId} className="collect-order-row">
                        <div>
                          <Typography.Text strong>{line.orderNumber}</Typography.Text>
                          <div className="collect-hit__meta">
                            {formatOrderDate(line.orderDate)}
                            {line.daysOutstanding != null ? ` · ${line.daysOutstanding} ngày` : ''}
                          </div>
                        </div>
                        <div className="collect-hit__debt">{formatMoney(line.outstanding)}</div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </>
            )}
          </>
        ) : null}
      </main>

      {canSubmit && receivable ? (
        <footer className="staff-footer collect-footer">
          <Typography.Text type="secondary" className="collect-footer__hint">
            {payMethodLabel(method)} · ghi sổ ngay · còn lại {formatMoney(remaining)}
          </Typography.Text>
          <Popconfirm
            title="Xác nhận thu công nợ?"
            description={`${receivable.customerName} · ${formatMoney(amount)} · ${payMethodLabel(method)}`}
            okText="Thu tiền"
            cancelText="Đóng"
            onConfirm={() => void submit()}
          >
            <Button type="primary" block size="large" loading={saving}>
              Xác nhận thu {formatMoney(amount)}
            </Button>
          </Popconfirm>
        </footer>
      ) : null}
    </div>
  );
}
