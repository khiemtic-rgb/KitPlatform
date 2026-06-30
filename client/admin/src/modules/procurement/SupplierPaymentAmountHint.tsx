import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation('procurement', { keyPrefix: 'supplierPayments.amountHint' });

  return (
    <Space direction="vertical" size="small" style={{ width: '100%', marginBottom: 16 }}>
      <Alert type="info" showIcon message={t('title')} description={t('description')} />
      {loading ? (
        <Typography.Text type="secondary">{t('loading')}</Typography.Text>
      ) : hints &&
        (hints.payableOutstanding != null ||
          hints.grnPreTax != null ||
          hints.poPostTax != null) ? (
        <Space direction="vertical" size={4} style={{ width: '100%' }}>
          {hints.payableOutstanding != null && (
            <Space wrap>
              <Typography.Text>
                {t('outstanding')}
                {hints.grnNumber ? ` — ${hints.grnNumber}` : ''}:{' '}
                <Typography.Text strong>{formatDisplayMoney(hints.payableOutstanding)}</Typography.Text>
              </Typography.Text>
              <Button type="link" size="small" onClick={() => onFillAmount(hints.payableOutstanding!)}>
                {t('fillOutstanding')}
              </Button>
            </Space>
          )}
          {hints.grnPreTax != null && hints.payableOutstanding == null && (
            <Space wrap>
              <Typography.Text>
                {t('grnPreTax')}
                {hints.grnNumber ? ` — ${hints.grnNumber}` : ''}:{' '}
                <Typography.Text strong>{formatDisplayMoney(hints.grnPreTax)}</Typography.Text>
              </Typography.Text>
              <Button type="link" size="small" onClick={() => onFillAmount(hints.grnPreTax!)}>
                {t('fillGrn')}
              </Button>
            </Space>
          )}
          {hints.poPostTax != null && (
            <Space wrap>
              <Typography.Text type="secondary">
                {t('poPostTax')}
                {hints.poNumber ? ` — ${hints.poNumber}` : ''}: {formatDisplayMoney(hints.poPostTax)}
              </Typography.Text>
              <Button type="link" size="small" onClick={() => onFillAmount(hints.poPostTax!)}>
                {t('fillPo')}
              </Button>
            </Space>
          )}
        </Space>
      ) : null}
    </Space>
  );
}
