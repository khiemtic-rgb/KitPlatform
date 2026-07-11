import { Card, Empty, List, Tag, Typography } from 'antd';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { fetchMyPharmacies } from '@/shared/api/prescriber-portal.api';
import { linkStatusColor, linkStatusLabel } from '@/shared/ui/status-labels';

export function PharmacyLinksPage() {
  const { t } = useTranslation();
  const query = useQuery({
    queryKey: ['prescriber', 'pharmacies'],
    queryFn: () => fetchMyPharmacies(true),
  });

  return (
    <div>
      <Typography.Title level={4}>{t('pharmacies.title')}</Typography.Title>
      <Card loading={query.isLoading}>
        {!query.data?.length ? (
          <Empty description={t('pharmacies.empty')} />
        ) : (
          <List
            dataSource={query.data}
            renderItem={(item) => (
              <List.Item>
                <List.Item.Meta
                  title={
                    <>
                      {item.tenantName}{' '}
                      <Tag color="blue">{item.tenantCode}</Tag>
                      <Tag color={linkStatusColor(item.linkStatus)}>
                        {linkStatusLabel(t, item.linkStatus)}
                      </Tag>
                    </>
                  }
                  description={null}
                />
              </List.Item>
            )}
          />
        )}
      </Card>
    </div>
  );
}
