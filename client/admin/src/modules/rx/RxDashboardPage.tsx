import { useCallback, useEffect, useState } from 'react';
import { Button, Card, Col, Row, Space, Statistic, Table, Tag, Typography, message } from 'antd';
import { LinkOutlined, ReloadOutlined } from '@ant-design/icons';
import { Link, useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchTenantRxDashboard, type RxTenantDashboard } from '@/shared/api/rx.api';
import { useTranslation } from 'react-i18next';

export function RxDashboardPage() {
  const { t } = useTranslation('rx', { keyPrefix: 'dashboard' });
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [data, setData] = useState<RxTenantDashboard | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fetchTenantRxDashboard());
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const portalPct =
    data && data.portalSignedThisMonth + data.staffSignedThisMonth > 0
      ? Math.round(
          (100 * data.portalSignedThisMonth) /
            (data.portalSignedThisMonth + data.staffSignedThisMonth),
        )
      : null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('subtitle')}</Typography.Text>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('refresh')}
        </Button>
      </div>

      <Row gutter={[12, 12]}>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic title={t('signedThisMonth')} value={data?.signedThisMonth ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic title={t('pendingDispense')} value={data?.pendingDispense ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic
              title={t('overdue')}
              value={data?.overduePendingDispense ?? 0}
              valueStyle={
                (data?.overduePendingDispense ?? 0) > 0 ? { color: '#cf1322' } : undefined
              }
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic title={t('pendingLinks')} value={data?.pendingLinkApprovals ?? 0} />
          </Card>
        </Col>
      </Row>

      <Row gutter={[12, 12]}>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic title={t('activeLinks')} value={data?.activePrescriberLinks ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic title={t('pendingVerify')} value={data?.pendingVerification ?? 0} />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic
              title={t('avgSla')}
              value={data?.avgHoursToDispense != null ? Number(data.avgHoursToDispense.toFixed(1)) : '—'}
              suffix={data?.avgHoursToDispense != null ? t('hours') : undefined}
            />
          </Card>
        </Col>
        <Col xs={12} md={6}>
          <Card loading={loading && !data}>
            <Statistic
              title={t('portalShare')}
              value={portalPct != null ? portalPct : '—'}
              suffix={portalPct != null ? '%' : undefined}
            />
          </Card>
        </Col>
      </Row>

      {(data?.pendingLinkApprovals ?? 0) > 0 ? (
        <Card size="small">
          <Space>
            <Tag color="processing">{t('pendingLinks')}</Tag>
            <Typography.Text>{t('pendingLinksHint')}</Typography.Text>
            <Link to="/rx/prescriber-links">
              <Button type="link" icon={<LinkOutlined />} style={{ paddingInline: 0 }}>
                {t('openLinks')}
              </Button>
            </Link>
          </Space>
        </Card>
      ) : null}

      <Row gutter={[12, 12]}>
        <Col xs={24} lg={12}>
          <Card title={t('topPrescribers')} loading={loading && !data}>
            <Table
              size="small"
              pagination={false}
              rowKey={(row) => row.prescriberId ?? row.prescriberName}
              dataSource={data?.topPrescribers ?? []}
              locale={{ emptyText: t('emptyTop') }}
              columns={[
                {
                  title: t('colDoctor'),
                  render: (_, row) => (
                    <div>
                      <div>{row.prescriberName}</div>
                      {row.phone ? (
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {row.phone}
                        </Typography.Text>
                      ) : null}
                    </div>
                  ),
                },
                { title: t('colMonth'), dataIndex: 'signedThisMonth', width: 90 },
                { title: t('colTotal'), dataIndex: 'signedTotal', width: 90 },
              ]}
            />
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card title={t('pendingQueue')} loading={loading && !data}>
            <Table
              size="small"
              pagination={false}
              rowKey="id"
              dataSource={data?.recentPendingDispense ?? []}
              locale={{ emptyText: t('emptyPending') }}
              onRow={(row) => ({
                onClick: () => navigate(`/rx/prescriptions?rx=${row.id}`),
                style: { cursor: 'pointer' },
              })}
              columns={[
                {
                  title: t('colRx'),
                  dataIndex: 'prescriptionCode',
                  render: (code, row) => (
                    <div>
                      <Typography.Link>{code}</Typography.Link>
                      <div style={{ fontSize: 12, color: '#64748b' }}>
                        {row.prescriberName ?? '—'} · {row.patientName ?? '—'}
                      </div>
                    </div>
                  ),
                },
                {
                  title: t('colWait'),
                  width: 100,
                  render: (_, row) => {
                    const hours = row.hoursWaiting;
                    const overdue = hours != null && hours >= 24;
                    return (
                      <Tag color={overdue ? 'red' : 'blue'}>
                        {hours != null ? `${hours.toFixed(0)}h` : '—'}
                      </Tag>
                    );
                  },
                },
                {
                  title: t('colSigned'),
                  width: 120,
                  render: (_, row) =>
                    row.signedAt ? dayjs(row.signedAt).format('DD/MM HH:mm') : '—',
                },
              ]}
            />
          </Card>
        </Col>
      </Row>
    </div>
  );
}
