import { useTranslation } from 'react-i18next';
import { Card } from 'antd';
import { CustomerAppLinkQrPanel } from '@/modules/sales/CustomerAppLinkQrPanel';

export function CustomerAppLinkQrCard() {
  const { t } = useTranslation('sales', { keyPrefix: 'receiptSettings.customerAppLinkCard' });

  return (
    <Card title={t('title')}>
      <CustomerAppLinkQrPanel />
    </Card>
  );
}
