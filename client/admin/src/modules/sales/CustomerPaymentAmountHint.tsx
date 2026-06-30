import { Alert, Button, Space } from 'antd';
import { useTranslation } from 'react-i18next';
import { formatDisplayMoney } from '@/shared/utils/money';

interface CustomerPaymentAmountHintProps {
  orderNumber?: string;
  outstanding?: number;
  onFillAmount: (amount: number) => void;
}

export function CustomerPaymentAmountHint({
  orderNumber,
  outstanding,
  onFillAmount,
}: CustomerPaymentAmountHintProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'customerPayments.form.hint' });

  if (outstanding == null || outstanding <= 0.009) {
    return (
      <Alert type="warning" showIcon style={{ marginBottom: 16 }} message={t('noOutstanding')} />
    );
  }

  return (
    <Space direction="vertical" size="small" style={{ width: '100%', marginBottom: 16 }}>
      <Alert
        type="info"
        showIcon
        message={
          orderNumber
            ? t('orderOutstanding', {
                orderNumber,
                outstanding: formatDisplayMoney(outstanding),
              })
            : t('genericOutstanding', { outstanding: formatDisplayMoney(outstanding) })
        }
        description={t('description')}
      />
      <Button type="link" size="small" style={{ padding: 0 }} onClick={() => onFillAmount(outstanding)}>
        {t('fillOutstanding')}
      </Button>
    </Space>
  );
}
