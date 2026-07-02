import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Col,
  Progress,
  Row,
  Segmented,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
  message,
} from 'antd';
import { ReloadOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import {
  fetchCustomerEngagementDrillDown,
  fetchCustomerEngagementOverview,
  type CustomerEngagementDrillDownItem,
  type CustomerEngagementFunnelStep,
  type CustomerEngagementOverview,
} from '@/shared/api/customer-engagement.api';
import { apiErrorMessage } from '@/shared/api/api-error';

const PERIOD_OPTIONS = [
  { label: '7d', value: 7 },
  { label: '30d', value: 30 },
  { label: '90d', value: 90 },
];

function pct(value: number) {
  return `${Math.round(value * 1000) / 10}%`;
}

function deltaTag(delta: number) {
  if (delta > 0.005) return <Tag color="green">+{pct(delta)}</Tag>;
  if (delta < -0.005) return <Tag color="red">{pct(delta)}</Tag>;
  return <Tag>—</Tag>;
}

function FunnelStepCard({
  step,
  cohortSize,
  selected,
  onSelect,
}: {
  step: CustomerEngagementFunnelStep;
  cohortSize: number;
  selected: boolean;
  onSelect: () => void;
}) {
  const { t } = useTranslation('customer', { keyPrefix: 'engagement' });
  const barPercent = cohortSize > 0 ? Math.round((step.count / cohortSize) * 100) : 0;

  return (
    <Card
      size="small"
      hoverable
      onClick={onSelect}
      style={{
        borderRadius: 12,
        borderColor: selected ? '#0f766e' : undefined,
        cursor: 'pointer',
      }}
    >
      <Space direction="vertical" style={{ width: '100%' }} size={6}>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
          <Typography.Text strong>{t(`steps.${step.key}`, { defaultValue: step.label })}</Typography.Text>
          {deltaTag(step.deltaVsPriorPeriod)}
        </div>
        <Typography.Title level={3} style={{ margin: 0 }}>
          {step.count}
          <Typography.Text type="secondary" style={{ fontSize: 14, marginLeft: 8 }}>
            {pct(step.rateFromCohort)} {t('ofCohort')}
          </Typography.Text>
        </Typography.Title>
        <Progress percent={barPercent} showInfo={false} strokeColor="#0f766e" />
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('fromPrevious', { rate: pct(step.rateFromPrevious) })}
        </Typography.Text>
      </Space>
    </Card>
  );
}

export function CustomerEngagementPage() {
  const { t } = useTranslation('customer', { keyPrefix: 'engagement' });
  const [periodDays, setPeriodDays] = useState(30);
  const [loading, setLoading] = useState(true);
  const [overview, setOverview] = useState<CustomerEngagementOverview | null>(null);
  const [selectedStep, setSelectedStep] = useState<string | null>(null);
  const [drillLoading, setDrillLoading] = useState(false);
  const [drillItems, setDrillItems] = useState<CustomerEngagementDrillDownItem[]>([]);
  const [drillTotal, setDrillTotal] = useState(0);
  const [drillPage, setDrillPage] = useState(1);

  const loadOverview = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchCustomerEngagementOverview(periodDays);
      setOverview(data);
      setSelectedStep((prev) => prev ?? data.funnel[0]?.key ?? null);
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [periodDays, t]);

  const loadDrillDown = useCallback(
    async (step: string, page = 1) => {
      setDrillLoading(true);
      try {
        const data = await fetchCustomerEngagementDrillDown({ step, periodDays, page, pageSize: 20 });
        setDrillItems(data.items);
        setDrillTotal(data.total);
        setDrillPage(data.page);
      } catch (error) {
        message.error(apiErrorMessage(error, t('drillFailed')));
      } finally {
        setDrillLoading(false);
      }
    },
    [periodDays, t],
  );

  useEffect(() => {
    void loadOverview();
  }, [loadOverview]);

  useEffect(() => {
    if (selectedStep) {
      void loadDrillDown(selectedStep, 1);
    }
  }, [selectedStep, loadDrillDown]);

  const columns = [
    {
      title: t('columns.name'),
      dataIndex: 'fullName',
      key: 'fullName',
      render: (name: string, row: CustomerEngagementDrillDownItem) => (
        <Link to={`/customer/${row.customerId}`}>{name || '—'}</Link>
      ),
    },
    { title: t('columns.phone'), dataIndex: 'phone', key: 'phone' },
    {
      title: t('columns.lastLogin'),
      dataIndex: 'lastLoginAt',
      key: 'lastLoginAt',
      render: (value: string | null) => (value ? dayjs(value).format('DD/MM/YYYY HH:mm') : '—'),
    },
    {
      title: t('columns.firstLogin'),
      dataIndex: 'firstLoginAt',
      key: 'firstLoginAt',
      render: (value: string | null) => (value ? dayjs(value).format('DD/MM/YYYY') : '—'),
    },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12 }}>
        <div>
          <Typography.Title level={4} style={{ marginBottom: 4 }}>
            {t('title')}
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        <Space>
          <Segmented
            options={PERIOD_OPTIONS.map((opt) => ({ label: opt.label, value: opt.value }))}
            value={periodDays}
            onChange={(value) => setPeriodDays(Number(value))}
          />
          <Button icon={<ReloadOutlined />} onClick={() => void loadOverview()}>
            {t('refresh')}
          </Button>
        </Space>
      </div>

      {loading ? (
        <div style={{ textAlign: 'center', padding: 48 }}>
          <Spin size="large" />
        </div>
      ) : overview ? (
        <>
          {overview.alerts.length > 0 ? (
            <Space direction="vertical" style={{ width: '100%' }}>
              {overview.alerts.map((alert) => (
                <Alert key={alert.key} type="warning" showIcon message={alert.message} />
              ))}
            </Space>
          ) : null}

          <Row gutter={[12, 12]}>
            <Col xs={24} md={8}>
              <Card size="small" style={{ borderRadius: 12, height: '100%' }}>
                <Typography.Text type="secondary">{t('cohort')}</Typography.Text>
                <Typography.Title level={3} style={{ margin: '4px 0 0' }}>
                  {overview.cohortSize}
                </Typography.Title>
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card size="small" style={{ borderRadius: 12, height: '100%' }}>
                <Typography.Text type="secondary">{t('retention30d')}</Typography.Text>
                <Typography.Title level={3} style={{ margin: '4px 0 0' }}>
                  {pct(overview.retention30d.rate)}
                </Typography.Title>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {t('retentionDetail', {
                    retained: overview.retention30d.retainedCount,
                    eligible: overview.retention30d.eligibleCount,
                  })}
                </Typography.Text>
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card size="small" style={{ borderRadius: 12, height: '100%' }}>
                <Typography.Text type="secondary">{t('period')}</Typography.Text>
                <Typography.Title level={3} style={{ margin: '4px 0 0' }}>
                  {overview.periodDays} {t('days')}
                </Typography.Title>
              </Card>
            </Col>
          </Row>

          <Row gutter={[12, 12]}>
            {overview.funnel.map((step) => (
              <Col xs={24} sm={12} lg={8} xl={24 / Math.max(overview.funnel.length, 1)} key={step.key}>
                <FunnelStepCard
                  step={step}
                  cohortSize={overview.cohortSize}
                  selected={selectedStep === step.key}
                  onSelect={() => setSelectedStep(step.key)}
                />
              </Col>
            ))}
          </Row>

          <Card
            title={t('drillTitle', {
              step: t(`steps.${selectedStep ?? ''}`, { defaultValue: selectedStep ?? '' }),
            })}
            style={{ borderRadius: 12 }}
          >
            <Table
              rowKey="accountId"
              loading={drillLoading}
              columns={columns}
              dataSource={drillItems}
              pagination={{
                current: drillPage,
                total: drillTotal,
                pageSize: 20,
                onChange: (page) => {
                  if (selectedStep) void loadDrillDown(selectedStep, page);
                },
              }}
            />
          </Card>
        </>
      ) : null}
    </Space>
  );
}
