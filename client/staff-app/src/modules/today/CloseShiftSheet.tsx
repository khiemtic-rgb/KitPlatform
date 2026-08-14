import { useState } from 'react';
import { App, Alert, Form, Input, InputNumber, Modal, Typography } from 'antd';
import type { SalesShiftDetail } from '@/shared/api/sales.types';
import { closeSalesShift } from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { formatMoney } from '@/shared/utils/money';

type Props = {
  open: boolean;
  shift: SalesShiftDetail | null;
  onClose: () => void;
  onClosed: () => void;
};

export function CloseShiftSheet({ open, shift, onClose, onClosed }: Props) {
  const { message } = App.useApp();
  const [form] = Form.useForm<{ closingCash: number; closeNotes?: string }>();
  const [loading, setLoading] = useState(false);
  const [closingCashWatch, setClosingCashWatch] = useState<number | null>(null);

  const expectedCash = shift?.summary?.expectedCash ?? 0;
  const variance =
    closingCashWatch != null ? Number(closingCashWatch) - expectedCash : null;

  const submit = async () => {
    if (!shift) return;
    try {
      const values = await form.validateFields();
      setLoading(true);
      await closeSalesShift(shift.id, {
        closingCash: Number(values.closingCash),
        closeNotes: values.closeNotes?.trim() || undefined,
      });
      message.success(`Đã đóng ca ${shift.shiftNumber}`);
      form.resetFields();
      setClosingCashWatch(null);
      onClosed();
      onClose();
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return;
      message.error(apiErrorMessage(error, 'Không đóng được ca'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      open={open}
      title={shift ? `Đóng ca ${shift.shiftNumber}` : 'Đóng ca'}
      okText="Đóng ca"
      okButtonProps={{ danger: true, loading }}
      cancelText="Hủy"
      onCancel={onClose}
      onOk={() => void submit()}
      destroyOnClose
      afterOpenChange={(visible) => {
        if (visible && shift) {
          form.setFieldsValue({ closingCash: expectedCash, closeNotes: '' });
          setClosingCashWatch(expectedCash);
        }
      }}
    >
      {shift?.summary ? (
        <>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 12 }}
            message={`Dự kiến trong két: ${formatMoney(expectedCash)}`}
            description={`Doanh thu thuần ca ${formatMoney(shift.summary.netTotal)} · đầu ca ${formatMoney(shift.summary.openingCash)}`}
          />
          {variance != null && Math.abs(variance) > 0.009 ? (
            <Alert
              type={variance < 0 ? 'warning' : 'success'}
              showIcon
              style={{ marginBottom: 12 }}
              message={
                variance < 0
                  ? `Thiếu ${formatMoney(Math.abs(variance))} so với dự kiến`
                  : `Thừa ${formatMoney(variance)} so với dự kiến`
              }
              description="Ghi chú giải trình nếu có chênh lệch trước khi đóng."
            />
          ) : null}
        </>
      ) : null}
      <Form
        form={form}
        layout="vertical"
        onValuesChange={(_, all) => setClosingCashWatch(Number(all.closingCash ?? 0))}
      >
        <Form.Item
          name="closingCash"
          label="Tiền mặt đếm thực tế"
          rules={[{ required: true, message: 'Nhập số tiền đếm được' }]}
          extra="Đếm tiền trong két rồi nhập số thực tế"
        >
          <InputNumber
            size="large"
            style={{ width: '100%' }}
            min={0}
            formatter={(v) => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.')}
            parser={(v) => Number(String(v ?? '').replace(/\./g, '')) as 0}
          />
        </Form.Item>
        <Form.Item name="closeNotes" label="Ghi chú đóng ca">
          <Input.TextArea rows={2} placeholder="Chênh lệch, bàn giao, sự cố…" />
        </Form.Item>
      </Form>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Sau khi đóng ca không bán tiếp trên ca này được — cần mở ca mới.
      </Typography.Text>
    </Modal>
  );
}
