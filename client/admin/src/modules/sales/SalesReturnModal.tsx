import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Form, Input, InputNumber, Modal, Select, Space, Typography } from 'antd';
import type { PosCheckoutPaymentLine, SalesOrderDetail } from '@/shared/api/sales.types';
import { useSalesEnums } from '@/shared/i18n/use-sales-enums';
import { previewReturnRefund } from '@/modules/sales/sales-return-pricing';
import { resolveOrderPaymentSummary } from '@/modules/sales/sales-order-payment-summary';
import { PosSummaryMoney, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { formatDisplayMoney } from '@/shared/utils/money';

function splitReturnRefund(totalRefund: number, outstanding: number, amountPaid: number) {
  const debtReduced = Math.min(totalRefund, Math.max(0, outstanding));
  const cashRefund = Math.min(totalRefund - debtReduced, Math.max(0, amountPaid));
  return { debtReduced, cashRefund };
}

type Props = {
  open: boolean;
  loading?: boolean;
  order: SalesOrderDetail | null;
  onCancel: () => void;
  onConfirm: (payload: {
    reason?: string;
    items: { salesOrderItemId: string; quantity: number }[];
    payments: PosCheckoutPaymentLine[];
  }) => void;
};

export function SalesReturnModal({ open, loading, order, onCancel, onConfirm }: Props) {
  const { t } = useTranslation('sales', { keyPrefix: 'returns.modal' });
  const { paymentMethodLabel, paymentMethodOptions } = useSalesEnums();
  const [form] = Form.useForm<{
    reason?: string;
    quantities: Record<string, number>;
    paymentMethod: number;
  }>();

  const returnableLines = useMemo(
    () =>
      order?.items.filter(
        (line) => line.batchId && line.quantity - (line.returnedQuantity ?? 0) > 0.0001,
      ) ?? [],
    [order],
  );

  useEffect(() => {
    if (!open || !order) return;
    const initial: Record<string, number> = {};
    for (const line of returnableLines) {
      initial[line.id] = 0;
    }
    form.setFieldsValue({ reason: '', quantities: initial, paymentMethod: 1 });
  }, [form, open, order, returnableLines]);

  const quantities = Form.useWatch('quantities', form) as Record<string, number> | undefined;
  const paymentMethod = Number(Form.useWatch('paymentMethod', form) ?? 1);

  const preview = useMemo(() => {
    if (!order) return { totalRefund: 0, lines: [] };
    return previewReturnRefund(order, quantities ?? {});
  }, [order, quantities]);

  const paymentSummary = useMemo(
    () => (order ? resolveOrderPaymentSummary(order) : null),
    [order],
  );

  const returnSplit = useMemo(() => {
    if (!paymentSummary) return { debtReduced: 0, cashRefund: 0 };
    return splitReturnRefund(
      preview.totalRefund,
      paymentSummary.outstanding,
      paymentSummary.amountPaid,
    );
  }, [paymentSummary, preview.totalRefund]);

  const hasReturnQty = preview.lines.length > 0;
  const canSubmit = hasReturnQty;

  const handleOk = async () => {
    if (!order || !canSubmit) return;
    const values = await form.validateFields();
    onConfirm({
      reason: values.reason,
      items: preview.lines.map((line) => ({
        salesOrderItemId: line.itemId,
        quantity: line.quantity,
      })),
      payments: [{ paymentMethod: values.paymentMethod, amount: preview.totalRefund }],
    });
  };

  return (
    <Modal
      title={order ? t('title', { orderNumber: order.orderNumber }) : t('titleDefault')}
      open={open}
      onCancel={onCancel}
      onOk={() => void handleOk()}
      confirmLoading={loading}
      okText={t('confirm')}
      okButtonProps={{ disabled: !canSubmit }}
      width={620}
      destroyOnClose
      maskClosable={false}
    >
      {order && (
        <>
          <Space direction="vertical" size={8} style={{ width: '100%', marginBottom: 16 }}>
            <PosSummaryRow label={t('customerDue')} value={formatDisplayMoney(order.totalAmount)} />
            <PosSummaryRow
              label={t('refundThisTime')}
              value={formatDisplayMoney(preview.totalRefund)}
              danger={preview.totalRefund > 0}
              strong
            />
            {preview.totalRefund > 0.009 && returnSplit.debtReduced > 0.009 ? (
              <PosSummaryRow
                label={t('debtReduced')}
                value={formatDisplayMoney(returnSplit.debtReduced)}
              />
            ) : null}
            {preview.totalRefund > 0.009 && returnSplit.cashRefund > 0.009 ? (
              <PosSummaryRow
                label={t('cashRefund')}
                value={formatDisplayMoney(returnSplit.cashRefund)}
                danger
              />
            ) : null}
            {preview.totalRefund > 0.009 &&
            returnSplit.debtReduced > 0.009 &&
            returnSplit.cashRefund <= 0.009 ? (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('creditOnlyNote')}
              </Typography.Text>
            ) : null}
          </Space>

          <Form form={form} layout="vertical">
            <Form.Item name="reason" label={t('reason')}>
              <Input.TextArea rows={2} maxLength={300} />
            </Form.Item>
            {returnableLines.map((line) => {
              const max = line.quantity - (line.returnedQuantity ?? 0);
              return (
                <Form.Item
                  key={line.id}
                  name={['quantities', line.id]}
                  label={t('lineQty', {
                    productName: line.productName,
                    max: max.toLocaleString(),
                  })}
                  initialValue={0}
                >
                  <InputNumber min={0} max={max} style={{ width: '100%' }} />
                </Form.Item>
              );
            })}

            <Form.Item
              name="paymentMethod"
              label={t('paymentMethod')}
              rules={[{ required: true, message: t('paymentMethodRequired') }]}
            >
              <Select disabled={!hasReturnQty} options={paymentMethodOptions} />
            </Form.Item>
            <Form.Item label={t('customerRefund')}>
              <PosSummaryMoney
                value={formatDisplayMoney(returnSplit.cashRefund)}
                danger={returnSplit.cashRefund > 0}
                strong
              />
            </Form.Item>
            {hasReturnQty && returnSplit.cashRefund > 0.009 && (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('refundViaShift', { method: paymentMethodLabel(paymentMethod) })}
              </Typography.Text>
            )}
          </Form>
        </>
      )}
    </Modal>
  );
}
