import { Form, Input, InputNumber, Modal, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import type { SalesShiftDetail } from '@/shared/api/sales.types';
import { ShiftSummaryPanel } from '@/modules/sales/shift-summary-ui';
import {
  formatDisplayMoney,
  moneyInputNumberPropsAllowZeroSuffix,
  moneyInputNumberStyle,
} from '@/shared/utils/money';

type Props = {
  open: boolean;
  loading?: boolean;
  shift: SalesShiftDetail | null;
  onCancel: () => void;
  onConfirm: (closingCash: number, closeNotes?: string) => void;
};

export function CloseShiftModal({ open, loading, shift, onCancel, onConfirm }: Props) {
  const { t } = useTranslation('sales', { keyPrefix: 'shiftReport.closeModal' });
  const [form] = Form.useForm<{ closingCash: number; closeNotes?: string }>();
  const expectedCash = shift?.summary.expectedCash ?? shift?.expectedCash ?? 0;

  return (
    <Modal
      title={shift ? t('title', { shiftNumber: shift.shiftNumber }) : t('titleDefault')}
      open={open}
      width={640}
      confirmLoading={loading}
      okText={t('confirm')}
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
            {t('intro', { expected: formatDisplayMoney(expectedCash) })}
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
              label={t('closingCash')}
              rules={[{ required: true, message: t('closingCashRequired') }]}
            >
              <InputNumber
                {...moneyInputNumberPropsAllowZeroSuffix}
                style={{ ...moneyInputNumberStyle, width: '100%' }}
              />
            </Form.Item>
            <Form.Item name="closeNotes" label={t('notes')}>
              <Input.TextArea rows={2} placeholder={t('notesPlaceholder')} />
            </Form.Item>
          </Form>
        </>
      )}
    </Modal>
  );
}
