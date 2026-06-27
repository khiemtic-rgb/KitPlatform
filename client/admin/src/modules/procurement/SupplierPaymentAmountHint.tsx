import { Alert, Button, Space, Typography } from 'antd';
import { formatDisplayMoney } from '@/shared/utils/money';
import type { SupplierPaymentAmountHints } from '@/modules/procurement/supplier-payment-amount-hints';

interface SupplierPaymentAmountHintProps {
  hints: SupplierPaymentAmountHints | null;
  loading?: boolean;
  onFillAmount: (amount: number) => void;
}

export function SupplierPaymentAmountHint({
  hints,
  loading,
  onFillAmount,
}: SupplierPaymentAmountHintProps) {
  return (
    <Space direction="vertical" size="small" style={{ width: '100%', marginBottom: 16 }}>
      <Alert
        type="info"
        showIcon
        message="Công nợ hệ thống theo GRN trước thuế GTGT"
        description="Số «Còn lại» trên công nợ và gợi ý «trước thuế» khớp sổ phải trả. «Tổng PO sau thuế» chỉ tham chiếu khi thanh toán theo hóa đơn VAT — có thể khác công nợ."
      />
      {loading ? (
        <Typography.Text type="secondary">Đang tính gợi ý số tiền…</Typography.Text>
      ) : hints &&
        (hints.payableOutstanding != null ||
          hints.grnPreTax != null ||
          hints.poPostTax != null) ? (
        <Space direction="vertical" size={4} style={{ width: '100%' }}>
          {hints.payableOutstanding != null && (
            <Space wrap>
              <Typography.Text>
                Số còn lại (công nợ, trước thuế)
                {hints.grnNumber ? ` — ${hints.grnNumber}` : ''}:{' '}
                <Typography.Text strong>{formatDisplayMoney(hints.payableOutstanding)}</Typography.Text>
              </Typography.Text>
              <Button type="link" size="small" onClick={() => onFillAmount(hints.payableOutstanding!)}>
                Điền số còn lại
              </Button>
            </Space>
          )}
          {hints.grnPreTax != null &&
            hints.payableOutstanding == null && (
              <Space wrap>
                <Typography.Text>
                  Giá trị GRN (trước thuế)
                  {hints.grnNumber ? ` — ${hints.grnNumber}` : ''}:{' '}
                  <Typography.Text strong>{formatDisplayMoney(hints.grnPreTax)}</Typography.Text>
                </Typography.Text>
                <Button type="link" size="small" onClick={() => onFillAmount(hints.grnPreTax!)}>
                  Điền theo GRN
                </Button>
              </Space>
            )}
          {hints.poPostTax != null && (
            <Space wrap>
              <Typography.Text type="secondary">
                Tổng PO sau thuế (tham chiếu)
                {hints.poNumber ? ` — ${hints.poNumber}` : ''}: {formatDisplayMoney(hints.poPostTax)}
              </Typography.Text>
              <Button type="link" size="small" onClick={() => onFillAmount(hints.poPostTax!)}>
                Điền tổng PO
              </Button>
            </Space>
          )}
        </Space>
      ) : null}
    </Space>
  );
}
