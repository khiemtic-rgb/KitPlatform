import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Card,
  Col,
  Row,
  Space,
  Spin,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  PhoneOutlined,
  ReloadOutlined,
  RiseOutlined,
  ShoppingCartOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchDormantBuyers,
  fetchGrowthOpportunitiesToday,
  fetchGrowthWeeklyRefillReport,
  postDormantCareNow,
  postGrowthCareNow,
  type DormantBuyerItem,
  type DormantBuyers,
  type GrowthOpportunityItem,
  type GrowthOpportunitiesToday,
  type GrowthWeeklyRefillReport,
} from '@/shared/api/success.api';
import { formatDisplayMoney } from '@/shared/utils/money';
import { useCanAccessOwnerCockpit } from '@/shared/auth/usePermission';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';

type BucketKey = 'refillOverdue' | 'refillDue' | 'snoozedExpiring';
type MainTab = 'refill' | 'dormant';

function formatDate(value?: string | null) {
  if (!value) return '—';
  const d = dayjs(value);
  return d.isValid() ? d.format('DD/MM/YYYY') : String(value).slice(0, 10);
}

function zaloHref(phone: string) {
  const digits = phone.replace(/\D/g, '');
  if (!digits) return null;
  // VN mobile: keep leading 0 → 84…
  const normalized = digits.startsWith('84')
    ? digits
    : digits.startsWith('0')
      ? `84${digits.slice(1)}`
      : digits;
  return `https://zalo.me/${normalized}`;
}

export function GrowthDeskPage() {
  const { t } = useTranslation('success', { keyPrefix: 'growth' });
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const canCockpit = useCanAccessOwnerCockpit();
  const auditSlimNav = useAuditSlimNav();
  const [loading, setLoading] = useState(true);
  const [caringId, setCaringId] = useState<string | null>(null);
  const [data, setData] = useState<GrowthOpportunitiesToday | null>(null);
  const [weekly, setWeekly] = useState<GrowthWeeklyRefillReport | null>(null);
  const [dormant, setDormant] = useState<DormantBuyers | null>(null);
  const [activeBucket, setActiveBucket] = useState<BucketKey>('refillOverdue');
  const mainTab: MainTab = searchParams.get('tab') === 'dormant' ? 'dormant' : 'refill';

  const setMainTab = (tab: MainTab) => {
    const next = new URLSearchParams(searchParams);
    if (tab === 'dormant') next.set('tab', 'dormant');
    else next.delete('tab');
    setSearchParams(next, { replace: true });
  };

  const load = useCallback(async () => {
    if (!canCockpit || auditSlimNav) return;
    setLoading(true);
    try {
      const [today, report, dormantList] = await Promise.all([
        fetchGrowthOpportunitiesToday(),
        fetchGrowthWeeklyRefillReport(),
        fetchDormantBuyers({ days: 30, limit: 50 }),
      ]);
      setData(today);
      setWeekly(report);
      setDormant(dormantList);
      if (today.refillOverdue.length > 0) setActiveBucket('refillOverdue');
      else if (today.refillDue.length > 0) setActiveBucket('refillDue');
      else setActiveBucket('snoozedExpiring');
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [canCockpit, auditSlimNav, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const rows = useMemo(() => {
    if (!data) return [] as GrowthOpportunityItem[];
    if (activeBucket === 'refillDue') return data.refillDue;
    if (activeBucket === 'snoozedExpiring') return data.snoozedExpiring;
    return data.refillOverdue;
  }, [activeBucket, data]);

  const onCareNow = async (item: GrowthOpportunityItem) => {
    setCaringId(item.suggestionId);
    try {
      const result = await postGrowthCareNow(item.suggestionId);
      message.success(
        result.alreadyHadOpenDraft
          ? t('careAlreadyOpen', { draft: result.draftNumber })
          : t('careCreated', { draft: result.draftNumber }),
      );
      navigate(`/sales/app-orders/drafts?actionable=1`);
    } catch (error) {
      message.error(apiErrorMessage(error, t('careFailed')));
    } finally {
      setCaringId(null);
    }
  };

  const onDormantCare = async (item: DormantBuyerItem) => {
    setCaringId(item.customerId);
    try {
      const result = await postDormantCareNow(item.customerId);
      message.success(
        result.alreadyHadOpenDraft
          ? t('careAlreadyOpen', { draft: result.draftNumber })
          : t('careCreated', { draft: result.draftNumber }),
      );
      navigate(`/sales/app-orders/drafts?actionable=1`);
    } catch (error) {
      message.error(apiErrorMessage(error, t('dormant.careFailed')));
    } finally {
      setCaringId(null);
    }
  };

  if (auditSlimNav || !canCockpit) {
    return <Navigate to="/" replace />;
  }

  if (loading && !data) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  const bucketCards: Array<{ key: BucketKey; count: number; tone?: string }> = [
    { key: 'refillOverdue', count: data?.refillOverdue.length ?? 0, tone: 'danger' },
    { key: 'refillDue', count: data?.refillDue.length ?? 0, tone: 'warning' },
    { key: 'snoozedExpiring', count: data?.snoozedExpiring.length ?? 0 },
  ];

  return (
    <div style={{ maxWidth: 1100, margin: '0 auto', padding: '8px 0 32px' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }} wrap>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            <RiseOutlined style={{ marginRight: 8 }} />
            {t('title')}
          </Typography.Title>
          <Typography.Text type="secondary">
            {mainTab === 'dormant'
              ? t('dormant.subtitle', {
                  count: dormant?.totalCount ?? 0,
                  days: dormant?.dormantDays ?? 30,
                })
              : t('subtitle', {
                  count: data?.totalCount ?? 0,
                  date: formatDate(data?.businessDate),
                })}
          </Typography.Text>
        </div>
        <Space>
          <Link to="/success/cockpit">{t('backCockpit')}</Link>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('refresh')}
          </Button>
        </Space>
      </Space>

      <Tabs
        activeKey={mainTab}
        onChange={(key) => setMainTab(key as MainTab)}
        items={[
          {
            key: 'refill',
            label: t('tabs.refill', { count: data?.totalCount ?? 0 }),
          },
          {
            key: 'dormant',
            label: t('tabs.dormant', { count: dormant?.totalCount ?? 0 }),
          },
        ]}
        style={{ marginBottom: 8 }}
      />

      {mainTab === 'refill' ? (
        <>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16, borderRadius: 10 }}
            message={t('tip')}
          />

          {weekly ? (
            <Card size="small" style={{ marginBottom: 16, borderRadius: 12 }} title={t('weekly.title')}>
              <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12 }}>
                {t('weekly.range', {
                  start: formatDate(weekly.weekStart),
                  end: formatDate(weekly.weekEnd),
                })}
              </Typography.Text>
              <Row gutter={[12, 12]}>
                <Col xs={12} sm={6}>
                  <Typography.Text type="secondary">{t('weekly.due')}</Typography.Text>
                  <Typography.Title level={3} style={{ margin: 0 }}>
                    {weekly.dueCount}
                  </Typography.Title>
                </Col>
                <Col xs={12} sm={6}>
                  <Typography.Text type="secondary">{t('weekly.notified')}</Typography.Text>
                  <Typography.Title level={3} style={{ margin: 0 }}>
                    {weekly.notifiedCount}
                  </Typography.Title>
                </Col>
                <Col xs={12} sm={6}>
                  <Typography.Text type="secondary">{t('weekly.converted')}</Typography.Text>
                  <Typography.Title level={3} style={{ margin: 0 }}>
                    {weekly.convertedCount}
                  </Typography.Title>
                </Col>
                <Col xs={12} sm={6}>
                  <Typography.Text type="secondary">{t('weekly.revenue')}</Typography.Text>
                  <Typography.Title level={4} style={{ margin: 0 }}>
                    {formatDisplayMoney(weekly.attributedRevenue)}
                  </Typography.Title>
                </Col>
              </Row>
            </Card>
          ) : null}

          <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
            {bucketCards.map((card) => (
              <Col xs={24} sm={8} key={card.key}>
                <Card
                  size="small"
                  hoverable
                  onClick={() => setActiveBucket(card.key)}
                  style={{
                    borderRadius: 12,
                    borderColor: activeBucket === card.key ? '#0f766e' : undefined,
                    cursor: 'pointer',
                  }}
                >
                  <Typography.Text type="secondary">{t(`buckets.${card.key}`)}</Typography.Text>
                  <Typography.Title level={2} style={{ margin: '4px 0 0' }}>
                    {card.count}
                  </Typography.Title>
                </Card>
              </Col>
            ))}
          </Row>

          <Card size="small" style={{ borderRadius: 12 }} title={t(`buckets.${activeBucket}`)}>
            <Table
              rowKey="suggestionId"
              size="middle"
              pagination={{ pageSize: 20, hideOnSinglePage: true }}
              dataSource={rows}
              locale={{ emptyText: t('empty') }}
              columns={[
                {
                  title: t('columns.customer'),
                  key: 'customer',
                  render: (_, row) => (
                    <Space direction="vertical" size={0}>
                      <Link to={`/customer/list?customerId=${row.customerId}`}>
                        <UserOutlined /> {row.customerName || '—'}
                      </Link>
                      {row.customerPhone ? (
                        <Typography.Text type="secondary" copyable={{ text: row.customerPhone }}>
                          <PhoneOutlined /> {row.customerPhone}
                        </Typography.Text>
                      ) : null}
                    </Space>
                  ),
                },
                {
                  title: t('columns.order'),
                  key: 'order',
                  render: (_, row) => (
                    <Space direction="vertical" size={0}>
                      <Typography.Text strong>{row.orderLabel || row.orderNumber}</Typography.Text>
                      <Typography.Text type="secondary">#{row.orderNumber}</Typography.Text>
                    </Space>
                  ),
                },
                {
                  title: t('columns.due'),
                  key: 'due',
                  width: 140,
                  render: (_, row) => (
                    <Space direction="vertical" size={0}>
                      <span>{formatDate(row.suggestedForDate)}</span>
                      {row.daysOverdue != null && row.daysOverdue > 0 ? (
                        <Tag color="red">{t('daysOverdue', { days: row.daysOverdue })}</Tag>
                      ) : null}
                    </Space>
                  ),
                },
                {
                  title: t('columns.lastBuy'),
                  dataIndex: 'orderDate',
                  width: 120,
                  render: (v: string | null | undefined) => formatDate(v),
                },
                {
                  title: t('columns.actions'),
                  key: 'actions',
                  width: 200,
                  render: (_, row) => (
                    <Space wrap>
                      <Button
                        type="primary"
                        size="small"
                        icon={<ShoppingCartOutlined />}
                        loading={caringId === row.suggestionId}
                        onClick={() => void onCareNow(row)}
                      >
                        {t('careNow')}
                      </Button>
                      {row.customerPhone ? (
                        <Button
                          size="small"
                          href={`tel:${row.customerPhone}`}
                          icon={<PhoneOutlined />}
                        >
                          {t('call')}
                        </Button>
                      ) : null}
                    </Space>
                  ),
                },
              ]}
            />
          </Card>
        </>
      ) : (
        <>
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16, borderRadius: 10 }}
            message={t('dormant.tip')}
          />
          <Card
            size="small"
            style={{ borderRadius: 12 }}
            title={t('dormant.tableTitle', { days: dormant?.dormantDays ?? 30 })}
          >
            <Table
              rowKey="customerId"
              size="middle"
              loading={loading}
              pagination={{ pageSize: 20, hideOnSinglePage: true }}
              dataSource={dormant?.items ?? []}
              locale={{ emptyText: t('dormant.empty') }}
              columns={[
                {
                  title: t('columns.customer'),
                  key: 'customer',
                  render: (_, row) => (
                    <Space direction="vertical" size={0}>
                      <Link to={`/customer/list?customerId=${row.customerId}`}>
                        <UserOutlined /> {row.customerName || '—'}
                      </Link>
                      {row.customerPhone ? (
                        <Typography.Text type="secondary" copyable={{ text: row.customerPhone }}>
                          <PhoneOutlined /> {row.customerPhone}
                        </Typography.Text>
                      ) : (
                        <Typography.Text type="secondary">{t('dormant.noPhone')}</Typography.Text>
                      )}
                    </Space>
                  ),
                },
                {
                  title: t('dormant.columns.lastOrder'),
                  key: 'lastOrder',
                  render: (_, row) => (
                    <Space direction="vertical" size={0}>
                      <Typography.Text strong>#{row.lastOrderNumber}</Typography.Text>
                      <Typography.Text type="secondary">
                        {formatDisplayMoney(row.lastOrderTotal)}
                      </Typography.Text>
                    </Space>
                  ),
                },
                {
                  title: t('columns.lastBuy'),
                  dataIndex: 'lastOrderDate',
                  width: 120,
                  render: (v: string) => formatDate(v),
                },
                {
                  title: t('dormant.columns.daysIdle'),
                  dataIndex: 'daysSinceLastBuy',
                  width: 110,
                  render: (days: number) => (
                    <Tag color={days >= 60 ? 'red' : 'orange'}>
                      {t('dormant.daysIdle', { days })}
                    </Tag>
                  ),
                },
                {
                  title: t('columns.actions'),
                  key: 'actions',
                  width: 280,
                  render: (_, row) => {
                    const zalo = row.customerPhone ? zaloHref(row.customerPhone) : null;
                    return (
                      <Space wrap>
                        <Button
                          type="primary"
                          size="small"
                          icon={<ShoppingCartOutlined />}
                          loading={caringId === row.customerId}
                          onClick={() => void onDormantCare(row)}
                        >
                          {t('careNow')}
                        </Button>
                        <Button
                          size="small"
                          onClick={() => navigate(`/sales/pos?customerId=${row.customerId}`)}
                        >
                          {t('dormant.openPos')}
                        </Button>
                        {row.customerPhone ? (
                          <Button size="small" href={`tel:${row.customerPhone}`} icon={<PhoneOutlined />}>
                            {t('call')}
                          </Button>
                        ) : null}
                        {zalo ? (
                          <Button size="small" href={zalo} target="_blank" rel="noreferrer">
                            {t('dormant.zalo')}
                          </Button>
                        ) : null}
                      </Space>
                    );
                  },
                },
              ]}
            />
            {(dormant?.totalCount ?? 0) > (dormant?.items.length ?? 0) ? (
              <Typography.Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
                {t('dormant.showing', {
                  shown: dormant?.items.length ?? 0,
                  total: dormant?.totalCount ?? 0,
                })}
              </Typography.Paragraph>
            ) : null}
          </Card>
        </>
      )}
    </div>
  );
}
