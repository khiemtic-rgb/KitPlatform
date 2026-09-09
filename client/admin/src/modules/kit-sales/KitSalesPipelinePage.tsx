import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Col, Row, Space, Typography, message } from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesPipelineSummary,
  type KitSalesPipelineSummary,
} from '@/shared/api/kit-sales.api';
import { KIT_SALES_STATUSES } from './kit-sales.constants';

export function KitSalesPipelinePage() {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [summary, setSummary] = useState<KitSalesPipelineSummary | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      setSummary(await fetchKitSalesPipelineSummary());
    } catch (error) {
      message.error(apiErrorMessage(error, t('create.failed')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const buckets = useMemo(() => {
    const counts = Object.fromEntries((summary?.byStatus ?? []).map((b) => [b.status, b.count]));
    return KIT_SALES_STATUSES.map((status) => ({ status, count: counts[status] ?? 0 }));
  }, [summary]);

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Space>
        <Typography.Title level={3} style={{ marginBottom: 0 }}>
          {t('pipeline.title')}
        </Typography.Title>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('pipeline.reload')}
        </Button>
      </Space>
      <Typography.Text type="secondary">
        {t('pipeline.total', { count: summary?.totalLeads ?? 0 })}
      </Typography.Text>
      <Row gutter={[12, 12]}>
        {buckets.map((bucket) => (
          <Col key={bucket.status} xs={12} sm={8} md={6} xl={4}>
            <Card
              size="small"
              hoverable
              loading={loading && !summary}
              onClick={() => navigate('/kit-sales/leads')}
            >
              <Typography.Text type="secondary">
                {t(`status.${bucket.status}`, { defaultValue: bucket.status })}
              </Typography.Text>
              <div style={{ fontSize: 24, fontWeight: 600 }}>{bucket.count}</div>
            </Card>
          </Col>
        ))}
      </Row>
    </Space>
  );
}
