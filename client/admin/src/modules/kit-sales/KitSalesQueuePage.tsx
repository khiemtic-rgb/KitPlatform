import { useEffect, useState } from 'react';
import { Alert, Table, Typography } from 'antd';
import { useTranslation } from 'react-i18next';
import { listKitSalesReviews, type KitSalesReview } from '@/shared/api/kit-sales.api';

export function KitSalesQueuePage() {
  const { t } = useTranslation('kitSales');
  const [rows, setRows] = useState<KitSalesReview[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    listKitSalesReviews('pending')
      .then((items) => {
        if (alive) setRows(items);
      })
      .catch((reason: unknown) => {
        if (alive) setError(reason instanceof Error ? reason.message : t('queue.loadError'));
      });
    return () => {
      alive = false;
    };
  }, [t]);

  return (
    <div>
      <Typography.Title level={4}>{t('queue.title')}</Typography.Title>
      {error ? <Alert type="error" message={error} /> : null}
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: t('queue.empty') }}
        columns={[
          { title: t('queue.type'), dataIndex: 'taskType' },
          { title: t('queue.status'), dataIndex: 'status' },
          { title: t('queue.created'), dataIndex: 'createdAt' },
        ]}
      />
    </div>
  );
}
