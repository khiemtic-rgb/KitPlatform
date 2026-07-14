import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Col, Row, Spin, Typography, message } from 'antd';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  FundOutlined,
  InboxOutlined,
  ReloadOutlined,
  RightOutlined,
  RiseOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchOwnerCockpit, type OwnerCockpit } from '@/shared/api/success.api';
import { formatDisplayMoney } from '@/shared/utils/money';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';

type TileProps = {
  title: string;
  value: string | number;
  hint?: string;
  to: string;
  icon: ReactNode;
  tone?: 'default' | 'danger' | 'warning' | 'info';
};

function Tile({ title, value, hint, to, icon, tone = 'default' }: TileProps) {
  return (
    <Link to={to} className={`dashboard-tile dashboard-tile--${tone}`}>
      <div className="dashboard-tile__icon">{icon}</div>
      <div className="dashboard-tile__body">
        <span className="dashboard-tile__label">{title}</span>
        <span className="dashboard-tile__value">{value}</span>
        {hint ? <span className="dashboard-tile__hint">{hint}</span> : null}
      </div>
      <RightOutlined className="dashboard-tile__arrow" />
    </Link>
  );
}

export function OwnerCockpitPage() {
  const { t } = useTranslation('success');
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const [data, setData] = useState<OwnerCockpit | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fetchOwnerCockpit());
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading && !data) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  const sales = data?.overview.sales;
  const inv = data?.overview.inventory;
  const extras = data?.salesExtras;
  const invX = data?.inventoryExtras;
  const cust = data?.customers;
  const kap = data?.latestAssessment;
  const risk = data?.riskStrip;
  const showKap = isModuleEnabled('assessment') && !!kap?.submissionId;
  const riskTone =
    (risk?.cashVarianceAlertCount ?? 0) > 0 ? 'danger' : (risk?.openShiftCountToday ?? 0) > 0 ? 'warning' : 'info';

  return (
    <div className="dashboard-page">
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            <FundOutlined style={{ marginRight: 8 }} />
            {t('title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('subtitle')}</Typography.Text>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {t('refresh')}
        </Button>
      </div>

      <Alert type="info" showIcon style={{ marginTop: 16 }} message={t('tip')} />

      <div style={{ marginTop: 16, display: 'flex', flexWrap: 'wrap', gap: 12 }}>
        <Link to="/success/shift-checklist" className="dashboard-action">
          <CheckCircleOutlined />
          <span>{t('kpi.shiftChecklist')}</span>
          <RightOutlined />
        </Link>
        <Link to="/success/loss" className="dashboard-action">
          <WarningOutlined />
          <span>{t('kpi.lossRisk')}</span>
          <RightOutlined />
        </Link>
      </div>

      <Typography.Title level={5} style={{ marginTop: 24 }}>
        {t('sections.risk')}
      </Typography.Title>
      <Row gutter={[12, 12]}>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.cashVarianceAlerts')}
            value={risk?.cashVarianceAlertCount ?? 0}
            hint={
              risk?.topAlertShiftNumber
                ? t('kpi.topAlertShift', { shift: risk.topAlertShiftNumber })
                : t('kpi.thresholdHint', { value: formatDisplayMoney(risk?.cashVarianceThreshold ?? 0) })
            }
            to="/success/loss"
            icon={<WarningOutlined />}
            tone={riskTone}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.maxCashVariance')}
            value={formatDisplayMoney(risk?.maxAbsCashVarianceToday ?? 0)}
            hint={t('kpi.closedShiftsToday', { count: risk?.closedShiftCountToday ?? 0 })}
            to="/success/loss"
            icon={<RiseOutlined />}
            tone={(risk?.cashVarianceAlertCount ?? 0) > 0 ? 'danger' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.openShiftsToday')}
            value={risk?.openShiftCountToday ?? 0}
            hint={t('kpi.openShiftsHint')}
            to="/sales/shifts"
            icon={<CalendarOutlined />}
            tone={(risk?.openShiftCountToday ?? 0) > 0 ? 'warning' : 'default'}
          />
        </Col>
      </Row>

      <Typography.Title level={5} style={{ marginTop: 24 }}>
        {t('sections.sales')}
      </Typography.Title>
      <Row gutter={[12, 12]}>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.todayRevenue')}
            value={formatDisplayMoney(sales?.todayNetTotal ?? 0)}
            to="/reports"
            icon={<RiseOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.weekRevenue')}
            value={formatDisplayMoney(sales?.weekNetTotal ?? 0)}
            hint={t('kpi.weekOrders', { count: extras?.weekOrderCount ?? 0 })}
            to="/reports"
            icon={<CalendarOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.monthRevenue')}
            value={formatDisplayMoney(extras?.monthNetTotal ?? 0)}
            hint={t('kpi.monthOrders', { count: extras?.monthOrderCount ?? 0 })}
            to="/reports"
            icon={<FundOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.todayOrders')}
            value={sales?.todayOrderCount ?? 0}
            to="/sales/pos"
            icon={<ShoppingCartOutlined />}
          />
        </Col>
      </Row>

      <Typography.Title level={5} style={{ marginTop: 24 }}>
        {t('sections.inventory')}
      </Typography.Title>
      <Row gutter={[12, 12]}>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.nearExpirySku')}
            value={invX?.nearExpirySkuCount ?? 0}
            hint={t('kpi.nearExpiryValue', {
              value: formatDisplayMoney(invX?.nearExpiryStockValue ?? 0),
              days: inv?.expiryDays ?? 30,
            })}
            to="/inventory/stock?tab=fefo"
            icon={<WarningOutlined />}
            tone={(invX?.nearExpirySkuCount ?? 0) > 0 ? 'danger' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.lowStockProducts')}
            value={inv?.lowStockProductCount ?? 0}
            to="/inventory/low-stock"
            icon={<InboxOutlined />}
            tone={(inv?.lowStockProductCount ?? 0) > 0 ? 'warning' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.lowStockBatches')}
            value={inv?.lowStockBatchCount ?? 0}
            to="/inventory/stock?tab=fefo"
            icon={<InboxOutlined />}
            tone={(inv?.lowStockBatchCount ?? 0) > 0 ? 'warning' : 'default'}
          />
        </Col>
      </Row>

      <Typography.Title level={5} style={{ marginTop: 24 }}>
        {t('sections.customers')}
      </Typography.Title>
      <Row gutter={[12, 12]}>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.newCustomers')}
            value={cust?.newCustomers7d ?? 0}
            hint={t('kpi.last7Days')}
            to="/customer/list"
            icon={<TeamOutlined />}
            tone="info"
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.returningCustomers')}
            value={cust?.returningCustomers7d ?? 0}
            hint={t('kpi.last7Days')}
            to="/customer/list"
            icon={<TeamOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.totalCustomers')}
            value={data?.overview.catalog.customerCount ?? 0}
            to="/customer/list"
            icon={<TeamOutlined />}
          />
        </Col>
      </Row>

      {showKap ? (
        <>
          <Typography.Title level={5} style={{ marginTop: 24 }}>
            {t('sections.assessment')}
          </Typography.Title>
          <Row gutter={[12, 12]}>
            <Col xs={24} sm={12} lg={8}>
              <Tile
                title={t('kpi.latestKapScore')}
                value={
                  kap?.overallScore != null ? Number(kap.overallScore).toFixed(2) : t('kpi.noScore')
                }
                hint={kap?.completedAt ? String(kap.completedAt).slice(0, 10) : kap?.status}
                to="/kap/leads"
                icon={<RiseOutlined />}
                tone="info"
              />
            </Col>
          </Row>
        </>
      ) : null}

      <div style={{ marginTop: 24 }}>
        <Link to="/">{t('backDashboard')}</Link>
      </div>
    </div>
  );
}
