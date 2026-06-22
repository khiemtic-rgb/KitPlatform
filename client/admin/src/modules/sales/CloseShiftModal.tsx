import { Form, Input, InputNumber, Modal, Typography } from 'antd';
import type { SalesShiftDetail } from '@/shared/api/sales.types';
import { ShiftSummaryPanel } from '@/modules/sales/shift-summary-ui';
import { formatDisplayMoney, moneyInputNumberPropsAllowZeroSuffix, moneyInputNumberStyle } from '@/shared/utils/money';

type Props = {
  open: boolean;
  loading?: boolean;
  shift: SalesShiftDetail | null;
  onCancel: () => void;
  onConfirm: (closingCash: number, closeNotes?: string) => void;
};

export function CloseShiftModal({ open, loading, shift, onCancel, onConfirm }: Props) {
  const [form] = Form.useForm<{ closingCash: number; closeNotes?: string }>();
  const expectedCash = shift?.summary.expectedCash ?? shift?.expectedCash ?? 0;

  return (
    <Modal
      title={shift ? `Đóng ca — ${shift.shiftNumber}` : 'Đóng ca'}
      open={open}
      width={640}
      confirmLoading={loading}
      okText="Đóng ca"
      okButtonProps={{ danger: true }}
      onCancel={() => {
        form.resetFields();
        onCancel();
      }}
      onOk={async () => {
        const values = await form.validateFields();
        onConfirm(values.closingCash, values.closeNotes);
      }}
    >
      {shift && (
        <>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
            Đếm tiền mặt thực tế trong két và nhập số đếm được. Hệ thống so với tiền mặt dự kiến{' '}
            <strong>{formatDisplayMoney(expectedCash)}</strong>.
          </Typography.Paragraph>
          <ShiftSummaryPanel summary={shift.summary} showCashReconciliation />
          <Form
            form={form}
            layout="vertical"
            style={{ marginTop: 16 }}
            initialValues={{ closingCash: expectedCash }}
          >
            <Form.Item
              name="closingCash"
              label="Tiền mặt đếm cuối ca"
              rules={[{ required: true, message: 'Nhập tiền đếm được' }]}
            >
              <InputNumber
                {...moneyInputNumberPropsAllowZeroSuffix}
                style={{ ...moneyInputNumberStyle, width: '100%' }}
              />
            </Form.Item>
            <Form.Item name="closeNotes" label="Ghi chú (tuỳ chọn)">
              <Input.TextArea rows={2} placeholder="Giải thích chênh lệch nếu có..." />
            </Form.Item>
          </Form>
        </>
      )}
    </Modal>
  );
}
