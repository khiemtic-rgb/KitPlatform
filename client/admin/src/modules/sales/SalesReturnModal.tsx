import { useEffect, useMemo } from 'react';
import { Form, Input, InputNumber, Modal, Select, Space, Typography } from 'antd';
import type { PosCheckoutPaymentLine, SalesOrderDetail } from '@/shared/api/sales.types';
import { SALES_PAYMENT_METHOD_LABELS } from '@/shared/api/sales.types';
import { previewReturnRefund } from '@/modules/sales/sales-return-pricing';
import { PosSummaryMoney, PosSummaryRow } from '@/modules/sales/pos-summary-ui';
import { formatDisplayMoney } from '@/shared/utils/money';

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
      title={order ? `Trả hàng — ${order.orderNumber}` : 'Trả hàng'}
      open={open}
      onCancel={onCancel}
      onOk={() => void handleOk()}
      confirmLoading={loading}
      okText="Ghi nhận trả & hoàn tiền"
      okButtonProps={{ disabled: !canSubmit }}
      width={620}
      destroyOnClose
      maskClosable={false}
    >
      {order && (
        <>
          <Space direction="vertical" size={8} style={{ width: '100%', marginBottom: 16 }}>
            <PosSummaryRow label="Khách phải trả (đơn)" value={formatDisplayMoney(order.totalAmount)} />
            <PosSummaryRow
              label="Tiền hoàn lần này"
              value={formatDisplayMoney(preview.totalRefund)}
              danger={preview.totalRefund > 0}
              strong
            />
          </Space>

          <Form form={form} layout="vertical">
            <Form.Item name="reason" label="Lý do">
              <Input.TextArea rows={2} maxLength={300} />
            </Form.Item>
            {returnableLines.map((line) => {
              const max = line.quantity - (line.returnedQuantity ?? 0);
              return (
                <Form.Item
                  key={line.id}
                  name={['quantities', line.id]}
                  label={`${line.productName} (còn trả tối đa ${max.toLocaleString('vi-VN')})`}
                  initialValue={0}
                >
                  <InputNumber min={0} max={max} style={{ width: '100%' }} />
                </Form.Item>
              );
            })}

            <Form.Item
              name="paymentMethod"
              label="Hình thức hoàn tiền"
              rules={[{ required: true, message: 'Chọn hình thức hoàn' }]}
            >
              <Select
                disabled={!hasReturnQty}
                options={Object.entries(SALES_PAYMENT_METHOD_LABELS).map(([value, label]) => ({
                  value: Number(value),
                  label,
                }))}
              />
            </Form.Item>
            <Form.Item label="Tiền trả khách">
              <PosSummaryMoney
                value={formatDisplayMoney(preview.totalRefund)}
                danger={preview.totalRefund > 0}
                strong
              />
            </Form.Item>
            {hasReturnQty && preview.totalRefund > 0 && (
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Hoàn qua {SALES_PAYMENT_METHOD_LABELS[paymentMethod] ?? '—'} — ghi vào báo cáo ca.
              </Typography.Text>
            )}
          </Form>
        </>
      )}
    </Modal>
  );
}
