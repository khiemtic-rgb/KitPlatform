import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Form, InputNumber, Modal, Select, Space, Typography, message } from 'antd';
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons';
import type { PosAllocationPreview, PosCheckoutPaymentLine } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS } from '@/shared/api/sales.types';
import { formatAllocationPreviewLine } from '@/modules/sales/pos-batch-display';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  formatDisplayMoney,
  moneyInputNumberPropsAllowZeroSuffix,
  moneyInputNumberStyle,
} from '@/shared/utils/money';
import { PosSummaryRow } from '@/modules/sales/pos-summary-ui';

const PAYMENT_METHOD_CASH = 1;

type Props = {
  open: boolean;
  loading?: boolean;
  totalAmount: number;
  subtotalGross: number;
  lineDiscountTotal: number;
  orderDiscountAmount: number;
  allocationPreview?: PosAllocationPreview | null;
  onCancel: () => void;
  onConfirm: (payments: PosCheckoutPaymentLine[]) => void | Promise<void>;
};

const moneyFieldProps = {
  ...moneyInputNumberPropsAllowZeroSuffix,
  style: { ...moneyInputNumberStyle, width: 200 },
} as const;

const paymentMethodOptions = Object.entries(SALES_PAYMENT_METHOD_LABELS).map(([value, label]) => ({
  value: Number(value),
  label,
}));

function defaultPayments(total: number): PosCheckoutPaymentLine[] {
  return [{ paymentMethod: PAYMENT_METHOD_CASH, amount: total }];
}

function sumAmounts(rows: PosCheckoutPaymentLine[]): number {
  return rows.reduce((sum, row) => sum + Number(row?.amount ?? 0), 0);
}

function isSingleCashPayment(rows: PosCheckoutPaymentLine[]): boolean {
  return rows.length === 1 && Number(rows[0]?.paymentMethod) === PAYMENT_METHOD_CASH;
}

function amountFieldLabel(autoSplit: boolean): string {
  return autoSplit ? 'Tiền khách đưa (tự chia)' : 'Tiền khách đưa';
}

function rebalanceFirstRow(rows: PosCheckoutPaymentLine[], totalAmount: number): PosCheckoutPaymentLine[] {
  if (rows.length < 2) return rows;
  const rest = rows.slice(1).reduce((sum, row) => sum + Number(row.amount ?? 0), 0);
  const firstAmount = Math.max(0, totalAmount - rest);
  return [{ ...rows[0], amount: firstAmount }, ...rows.slice(1)];
}

function normalizePaymentsForApi(
  rows: PosCheckoutPaymentLine[],
  totalAmount: number,
): PosCheckoutPaymentLine[] {
  if (isSingleCashPayment(rows)) {
    return [{ paymentMethod: PAYMENT_METHOD_CASH, amount: totalAmount }];
  }
  return rows.map((row) => ({
    paymentMethod: Number(row.paymentMethod),
    amount: Number(row.amount ?? 0),
  }));
}

function roundMoney(v: number): number {
  return Math.round(v * 100) / 100;
}

function paymentsAreValid(rows: PosCheckoutPaymentLine[], totalAmount: number): boolean {
  if (rows.length === 0) return false;
  const total = roundMoney(totalAmount);
  if (isSingleCashPayment(rows)) {
    return Number(rows[0]?.amount ?? 0) >= total - 0.009;
  }
  return Math.abs(sumAmounts(rows) - total) < 0.01;
}

export function PosCheckoutModal({
  open,
  loading,
  totalAmount,
  subtotalGross,
  lineDiscountTotal,
  orderDiscountAmount,
  allocationPreview,
  onCancel,
  onConfirm,
}: Props) {
  const isFreeOrder = totalAmount < 0.01;
  const payableTotal = roundMoney(totalAmount);
  const [payments, setPayments] = useState<PosCheckoutPaymentLine[]>(() => defaultPayments(payableTotal));
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setSubmitError(null);
    setSubmitting(false);
    setPayments(isFreeOrder ? [] : defaultPayments(payableTotal));
  }, [isFreeOrder, open, payableTotal]);

  const singleCash = !isFreeOrder && isSingleCashPayment(payments);
  const cashTendered = singleCash ? Number(payments[0]?.amount ?? 0) : 0;
  const changeDue = singleCash && cashTendered > payableTotal + 0.009 ? cashTendered - payableTotal : 0;

  const paidTotal = useMemo(() => {
    if (isFreeOrder) return 0;
    if (singleCash) return Math.min(cashTendered, payableTotal);
    return sumAmounts(payments);
  }, [cashTendered, isFreeOrder, payableTotal, payments, singleCash]);

  const paymentOk = useMemo(() => {
    if (isFreeOrder) return true;
    return paymentsAreValid(payments, payableTotal);
  }, [isFreeOrder, payableTotal, payments]);

  const busy = submitting || loading;

  const updatePayment = (index: number, patch: Partial<PosCheckoutPaymentLine>) => {
    setSubmitError(null);
    setPayments((prev) => {
      const next = prev.map((row, i) => (i === index ? { ...row, ...patch } : row));
      if (next.length >= 2 && index > 0) {
        return rebalanceFirstRow(next, payableTotal);
      }
      return next;
    });
  };

  const handleAddPayment = () => {
    setSubmitError(null);
    setPayments((prev) => {
      const allocated = singleCash ? payableTotal : sumAmounts(prev);
      const remaining = Math.max(0, payableTotal - allocated);
      if (prev.length === 1 && Math.abs(allocated - payableTotal) < 0.01) {
        return rebalanceFirstRow(
          [{ ...prev[0], amount: 0 }, { paymentMethod: 3, amount: 0 }],
          payableTotal,
        );
      }
      return rebalanceFirstRow([...prev, { paymentMethod: 3, amount: remaining }], payableTotal);
    });
  };

  const handleRemovePayment = (index: number) => {
    setSubmitError(null);
    setPayments((prev) => {
      const next = prev.filter((_, i) => i !== index);
      if (next.length === 0) return defaultPayments(payableTotal);
      if (next.length === 1) return [{ ...next[0], amount: payableTotal }];
      return rebalanceFirstRow(next, payableTotal);
    });
  };

  const handleSubmit = async () => {
    if (busy) return;
    setSubmitError(null);
    setSubmitting(true);

    try {
      if (isFreeOrder) {
        await Promise.resolve(onConfirm([]));
        return;
      }

      const rows = payments.length > 0 ? payments : defaultPayments(payableTotal);
      if (rows.length > 1 && !paymentsAreValid(rows, payableTotal)) {
        const err = 'Tổng thanh toán chưa khớp số tiền khách phải trả';
        setSubmitError(err);
        message.warning(err);
        return;
      }

      await Promise.resolve(onConfirm(normalizePaymentsForApi(rows, payableTotal)));
    } catch (error) {
      const errMsg = apiErrorMessage(error, 'Không ghi được đơn bán');
      setSubmitError(errMsg);
      message.error(errMsg);
    } finally {
      setSubmitting(false);
    }
  };

  const blockReason = useMemo(() => {
    if (isFreeOrder || paymentOk) return null;
    if (singleCash) {
      return `Khách cần đưa ít nhất ${formatDisplayMoney(payableTotal)} (hiện ${formatDisplayMoney(cashTendered)}).`;
    }
    return `Tổng phân bổ phải bằng ${formatDisplayMoney(payableTotal)} (hiện ${formatDisplayMoney(paidTotal)}).`;
  }, [cashTendered, isFreeOrder, paidTotal, payableTotal, paymentOk, singleCash]);

  const totalDiscountAmount = lineDiscountTotal + orderDiscountAmount;

  return (
    <Modal
      title="Thanh toán"
      open={open}
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel} disabled={busy}>
          Hủy
        </Button>,
        <Button key="ok" type="primary" loading={busy} onClick={() => void handleSubmit()}>
          Hoàn tất bán
        </Button>,
      ]}
      width={620}
      destroyOnClose
      maskClosable={false}
    >
      <Space direction="vertical" size={8} style={{ width: '100%', marginBottom: 16 }}>
        <PosSummaryRow label="Tổng tiền hàng" value={formatDisplayMoney(subtotalGross)} />
        {lineDiscountTotal > 0 && (
          <PosSummaryRow
            label="Chiết khấu sản phẩm"
            value={`−${formatDisplayMoney(lineDiscountTotal)}`}
            danger
          />
        )}
        {orderDiscountAmount > 0 && (
          <PosSummaryRow
            label="Chiết khấu đơn hàng"
            value={`−${formatDisplayMoney(orderDiscountAmount)}`}
            danger
          />
        )}
        {totalDiscountAmount > 0 && (
          <PosSummaryRow
            label="Tổng chiết khấu"
            value={`−${formatDisplayMoney(totalDiscountAmount)}`}
            danger
          />
        )}
        <PosSummaryRow label="Khách phải trả" value={formatDisplayMoney(totalAmount)} strong />
      </Space>

      {submitError && (
        <Alert type="error" showIcon message={submitError} style={{ marginBottom: 16 }} closable onClose={() => setSubmitError(null)} />
      )}

      {allocationPreview && allocationPreview.lines.length > 0 && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={`Phân bổ lô khi chốt (${allocationPreview.stockSourceLabel})`}
          description={
            <ul style={{ margin: '8px 0 0', paddingLeft: 18 }}>
              {allocationPreview.lines.map((line) => (
                <li key={line.productUnitId}>
                  {line.productCode} — {formatAllocationPreviewLine(line)}
                </li>
              ))}
            </ul>
          }
        />
      )}

      {isFreeOrder ? (
        <Alert
          type="success"
          showIcon
          message="Đơn 0 đ — không cần nhập thanh toán. Bấm Hoàn tất bán để ghi đơn."
        />
      ) : (
        <>
          {payments.length >= 2 && (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 12 }}
              message="Chia nhiều hình thức: nhập số tiền các dòng sau — dòng đầu tự nhận phần còn lại."
            />
          )}

          <Form layout="vertical" requiredMark={false}>
            {payments.map((row, index) => {
              const autoSplit = index === 0 && payments.length > 1;
              return (
                <Space key={index} align="start" wrap style={{ marginBottom: 8, width: '100%' }}>
                  <Form.Item label="Hình thức" required style={{ marginBottom: 0 }}>
                    <Select
                      style={{ width: 160 }}
                      value={row.paymentMethod}
                      options={paymentMethodOptions}
                      disabled={busy}
                      onChange={(value) => updatePayment(index, { paymentMethod: value })}
                    />
                  </Form.Item>
                  <Form.Item label={amountFieldLabel(autoSplit)} style={{ marginBottom: 0 }}>
                    <InputNumber
                      {...moneyFieldProps}
                      value={row.amount}
                      disabled={busy || autoSplit}
                      onChange={(value) => updatePayment(index, { amount: Number(value ?? 0) })}
                    />
                  </Form.Item>
                  {payments.length > 1 && (
                    <Button
                      type="text"
                      danger
                      icon={<MinusCircleOutlined />}
                      disabled={busy}
                      onClick={() => handleRemovePayment(index)}
                      style={{ marginTop: 30 }}
                    />
                  )}
                </Space>
              );
            })}
          </Form>

          <Button type="dashed" block icon={<PlusOutlined />} onClick={handleAddPayment} disabled={busy}>
            Thêm hình thức thanh toán
          </Button>

          <div style={{ marginTop: 16 }}>
            <Typography.Text>
              Đã phân bổ:{' '}
              <Typography.Text type={paymentOk ? 'success' : 'danger'} strong>
                {formatDisplayMoney(paidTotal)}
              </Typography.Text>
              <Typography.Text type="secondary"> / {formatDisplayMoney(payableTotal)}</Typography.Text>
            </Typography.Text>
            {changeDue > 0 && (
              <Typography.Paragraph style={{ marginTop: 8, marginBottom: 0 }}>
                Tiền thừa trả khách:{' '}
                <Typography.Text strong style={{ color: '#1677ff' }}>
                  {formatDisplayMoney(changeDue)}
                </Typography.Text>
              </Typography.Paragraph>
            )}
            {blockReason && (
              <Typography.Paragraph type="danger" style={{ marginTop: 8, marginBottom: 0 }}>
                {blockReason}
              </Typography.Paragraph>
            )}
          </div>
        </>
      )}
    </Modal>
  );
}
