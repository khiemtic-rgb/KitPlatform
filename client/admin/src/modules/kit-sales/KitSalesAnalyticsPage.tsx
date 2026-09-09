import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Col, Row, Space, Typography, message } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchKitSalesJourneyDue,
  fetchKitSalesLeads,
  fetchKitSalesPipelineSummary,
  type KitSalesJourneyPlan,
  type KitSalesLead,
  type KitSalesPipelineSummary,
} from '@/shared/api/kit-sales.api';
import { KIT_SALES_PIPELINE_STATUSES } from './kit-sales.constants';
import { classifyKitSalesToday } from './kit-sales.helpers';

export function KitSalesAnalyticsPage() {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [summary, setSummary] = useState<KitSalesPipelineSummary | null>(null);
  const [leads, setLeads] = useState<KitSalesLead[]>([]);
  const [due, setDue] = useState<KitSalesJourneyPlan[]>([]);

  useEffect(() => {
    void Promise.all([
      fetchKitSalesPipelineSummary(),
      fetchKitSalesLeads({ limit: 200 }),
      fetchKitSalesJourneyDue(20).catch(() => [] as KitSalesJourneyPlan[]),
    ])
      .then(([pipeline, rows, journeys]) => {
        setSummary(pipeline);
        setLeads(rows);
        setDue(journeys);
      })
      .catch((error) => message.error(apiErrorMessage(error, t('create.failed'))));
  }, [t]);

  const today = useMemo(() => classifyKitSalesToday(leads, due), [due, leads]);
  const buckets = useMemo(() => {
    const counts = Object.fromEntries((summary?.byStatus ?? []).map((b) => [b.status, b.count]));
    return KIT_SALES_PIPELINE_STATUSES.map((status) => ({ status, count: counts[status] ?? 0 }));
  }, [summary]);
  const demoCount = buckets.find((row) => row.status === 'demo')?.count ?? 0;

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {t('analytics.title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('analytics.blurb')}
        </Typography.Paragraph>
      </div>

      <Typography.Text>
        {t('workspace.strip', {
          due: today.due.length,
          followup: today.followup.length,
          phc: today.phc.length,
          demo: demoCount,
        })}
      </Typography.Text>

      <Card size="small" title={t('analytics.funnel')}>
        <Row gutter={[12, 12]}>
          {buckets.map((bucket) => (
            <Col key={bucket.status} xs={12} sm={8} md={6}>
              <Typography.Text type="secondary">
                {t(`status.${bucket.status}`, { defaultValue: bucket.status })}
              </Typography.Text>
              <div style={{ fontSize: 22, fontWeight: 600 }}>{bucket.count}</div>
            </Col>
          ))}
        </Row>
      </Card>

      <Button onClick={() => navigate('/kit-sales/intelligence')}>{t('analytics.openIntel')}</Button>
    </Space>
  );
}
