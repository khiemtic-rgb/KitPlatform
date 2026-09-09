import { Typography } from 'antd';
import { useTranslation } from 'react-i18next';

export function KitSalesCampaignsPage() {
  const { t } = useTranslation('kitSales');
  return (
    <div>
      <Typography.Title level={3}>{t('campaigns.title')}</Typography.Title>
      <Typography.Paragraph type="secondary">{t('campaigns.blurb')}</Typography.Paragraph>
    </div>
  );
}
