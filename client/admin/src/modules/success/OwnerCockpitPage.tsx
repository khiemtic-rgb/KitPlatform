import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link, Navigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Col, Row, Spin, Typography, message } from 'antd';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
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
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';
import { useCanAccessOwnerCockpit, useHasPermission } from '@/shared/auth/usePermission';

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

function PeakHoursBars({
  hours,
  peakHour,
}: {
  hours: { hour: number; orderCount: number }[];
  peakHour?: number | null;
}) {
  const max = Math.max(1, ...hours.map((h) => h.orderCount));
  // Show trading hours 6–22 for readability; keep all data in peak tile.
  const visible = hours.filter((h) => h.hour >= 6 && h.hour <= 22);
  return (
    <div
      style={{
        display: 'flex',
        alignItems: 'flex-end',
        gap: 4,
        height: 72,
        marginTop: 8,
      }}
      aria-hidden
    >
      {visible.map((h) => {
        const height = Math.max(4, Math.round((h.orderCount / max) * 64));
        const isPeak = peakHour != null && h.hour === peakHour;
        return (
          <div
            key={h.hour}
            title={`${h.hour}h: ${h.orderCount}`}
            style={{
              flex: 1,
              height,
              borderRadius: 3,
              background: isPeak ? 'var(--ant-color-primary, #1677ff)' : 'rgba(22, 119, 255, 0.28)',
              minWidth: 0,
            }}
          />
        );
      })}
    </div>
  );
}

export function OwnerCockpitPage() {
  const { t } = useTranslation('success');
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const auditSlimNav = useAuditSlimNav();
  const canCockpit = useCanAccessOwnerCockpit();
  const canChecklist = useHasPermission('success.checklist');
  const [data, setData] = useState<OwnerCockpit | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    if (!canCockpit || auditSlimNav) return;
    setLoading(true);
    try {
      setData(await fetchOwnerCockpit());
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [canCockpit, auditSlimNav, t]);

  useEffect(() => {
    void load();
  }, [load]);

  if (auditSlimNav || !canCockpit) {
    return <Navigate to={canChecklist && !auditSlimNav ? '/success/shift-checklist' : '/'} replace />;
  }

  if (loading && !data) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  const sales = data?.overview.sales;
  const inv = data?.overview.inventory;
  const o2o = data?.overview.o2o;
  const extras = data?.salesExtras;
  const invX = data?.inventoryExtras;
  const cust = data?.customers;
  const peak = data?.peakHours;
  const kap = data?.latestAssessment;
  const risk = data?.riskStrip;
  const showKap = isModuleEnabled('assessment') && !!kap?.submissionId;
  const riskTone =
    (risk?.cashVarianceAlertCount ?? 0) > 0 ? 'danger' : (risk?.openShiftCountToday ?? 0) > 0 ? 'warning' : 'info';
  const peakHour = peak?.peakHour;
  const hasPeak = peakHour != null && (peak?.peakOrderCount ?? 0) > 0;

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
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.cycleCountToday')}
            value={t(`kpi.cycleStatus.${risk?.cycleCountStatusToday ?? 'not_done'}`)}
            hint={
              risk?.cycleCountAdjustmentNumber
                ? t('kpi.cycleCountDoc', { number: risk.cycleCountAdjustmentNumber })
                : t('kpi.cycleCountHint')
            }
            to="/success/loss"
            icon={<CheckCircleOutlined />}
            tone={
              risk?.cycleCountStatusToday === 'has_variance'
                ? 'danger'
                : risk?.cycleCountStatusToday === 'done'
                  ? 'default'
                  : 'warning'
            }
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
        {t('sections.peakHours')}
      </Typography.Title>
      <Row gutter={[12, 12]}>
        <Col xs={24} lg={8}>
          <Tile
            title={t('kpi.peakHour')}
            value={
              hasPeak
                ? t('kpi.peakHourValue', { hour: peakHour, hourEnd: (peakHour ?? 0) + 1 })
                : '—'
            }
            hint={
              hasPeak
                ? t('kpi.peakHourHint', {
                    count: peak?.peakOrderCount ?? 0,
                    revenue: formatDisplayMoney(peak?.peakRevenue ?? 0),
                    days: peak?.windowDays ?? 30,
                  })
                : t('kpi.peakHourEmpty')
            }
            to="/sales/shifts"
            icon={<ClockCircleOutlined />}
            tone={hasPeak ? 'info' : 'default'}
          />
        </Col>
        <Col xs={24} lg={16}>
          <div className="dashboard-tile dashboard-tile--default" style={{ display: 'block', padding: 16 }}>
            <Typography.Text type="secondary">
              {t('kpi.peakHoursCaption', { days: peak?.windowDays ?? 30 })}
            </Typography.Text>
            <PeakHoursBars hours={peak?.hours ?? []} peakHour={peakHour} />
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                marginTop: 4,
                fontSize: 11,
                color: 'rgba(0,0,0,0.45)',
              }}
            >
              <span>6h</span>
              <span>12h</span>
              <span>18h</span>
              <span>22h</span>
            </div>
          </div>
        </Col>
      </Row>

      <Typography.Title level={5} style={{ marginTop: 24 }}>
        {t('sections.inventory')}
      </Typography.Title>
      <Row gutter={[12, 12]}>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.urgentNearExpiry')}
            value={invX?.urgentNearExpirySkuCount ?? 0}
            hint={t('kpi.urgentNearExpiryHint', { days: invX?.urgentExpiryDays ?? 7 })}
            to="/inventory/stock?tab=fefo"
            icon={<WarningOutlined />}
            tone={(invX?.urgentNearExpirySkuCount ?? 0) > 0 ? 'danger' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.nearExpirySku')}
            value={invX?.nearExpirySkuCount ?? 0}
            hint={t('kpi.nearExpiryValue', {
              value: formatDisplayMoney(invX?.nearExpiryStockValue ?? 0),
              days: inv?.expiryDays ?? 30,
            })}
            to="/inventory/stock?tab=fefo"
            icon={<WarningOutlined />}
            tone={(invX?.nearExpirySkuCount ?? 0) > 0 ? 'warning' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Tile
            title={t('kpi.lowStockProducts')}
            value={inv?.lowStockProductCount ?? 0}
            to="/inventory/low-stock"
            icon={<InboxOutlined />}
            tone={(inv?.lowStockProductCount ?? 0) > 0 ? 'warning' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
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
            title={t('kpi.dormantBuyers')}
            value={cust?.dormantBuyerCount ?? 0}
            hint={t('kpi.dormantBuyersHint', { days: cust?.dormantDays ?? 30 })}
            to="/success/growth?tab=dormant"
            icon={<TeamOutlined />}
            tone={(cust?.dormantBuyerCount ?? 0) > 0 ? 'warning' : 'default'}
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('kpi.activeBuyers')}
            value={cust?.activeBuyerCount ?? 0}
            hint={t('kpi.activeBuyersHint', { days: cust?.dormantDays ?? 30 })}
            to="/customer/list"
            icon={<TeamOutlined />}
            tone="info"
          />
        </Col>
        <Col xs={24} sm={12} lg={8}>
          <Tile
            title={t('growth.tile.title')}
            value={o2o?.refillOpportunityCount ?? 0}
            hint={t('growth.tile.hint')}
            to="/success/growth"
            icon={<RiseOutlined />}
            tone={(o2o?.refillOpportunityCount ?? 0) > 0 ? 'warning' : 'info'}
          />
        </Col>
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
