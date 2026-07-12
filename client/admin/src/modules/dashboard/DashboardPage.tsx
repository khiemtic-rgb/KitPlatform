import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Col, Row, Segmented, Spin, Tag, Typography, Alert, message } from 'antd';
import {
  AccountBookOutlined,
  CalendarOutlined,
  CheckCircleOutlined,
  InboxOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  ReloadOutlined,
  RightOutlined,
  ShoppingCartOutlined,
  ShoppingOutlined,
  ShopOutlined,
  TeamOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useAuthStore } from '@/shared/auth/auth.store';
import { fetchDashboardOverview } from '@/shared/api/dashboard.api';
import type { DashboardOverview } from '@/shared/api/dashboard.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { formatDisplayMoney } from '@/shared/utils/money';
import { isProductFeatureEnabled } from '@/shared/product/product-phases';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { resolveAdminVertical } from '@/modules/registry';
import { ClinicOverviewPage } from '@/modules/clinic/ClinicOverviewPage';
import { DashboardRevenueChart } from '@/modules/dashboard/DashboardRevenueChart';
import { DashboardCategoryChart } from '@/modules/dashboard/DashboardCategoryChart';
import type { RevenuePeriodDays } from '@/modules/dashboard/dashboard-revenue-range';

type AlertItem = {
  key: string;
  label: string;
  count: number;
  to: string;
  tone: 'danger' | 'warning' | 'info';
};

type MetricTileProps = {
  title: string;
  value: string | number;
  icon: ReactNode;
  to: string;
  tone?: 'default' | 'danger' | 'warning' | 'info';
  hint?: string;
};

function MetricTile({ title, value, icon, to, tone = 'default', hint }: MetricTileProps) {
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

export function DashboardPage() {
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const vertical = resolveAdminVertical(useTenantPlatformStore((s) => s.settings?.vertical));

  if (!platformLoaded) {
    return (
      <div className="dashboard-page" style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  if (vertical === 'clinic') {
    return <ClinicOverviewPage />;
  }

  return <PharmacyDashboardPage />;
}

function PharmacyDashboardPage() {
  const { t } = useTranslation('dashboard');
  const { t: tc } = useTranslation('common');
  const user = useAuthStore((s) => s.user);
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [revenuePeriodDays, setRevenuePeriodDays] = useState<RevenuePeriodDays>(7);

  const canSales = useHasPermission('sales.read') || useHasPermission('sales.write');
  const canCatalog = useHasPermission('catalog.read') || useHasPermission('catalog.write');
  const canInventory = useHasPermission('inventory.read') || useHasPermission('inventory.write');
  const canProcurement = useHasPermission('procurement.read') || useHasPermission('procurement.write');
  const canReceivables = useHasPermission('sales.read');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setOverview(await fetchDashboardOverview());
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const sales = overview?.sales;
  const catalog = overview?.catalog;
  const inventory = overview?.inventory;
  const procurement = overview?.procurement;
  const o2o = overview?.o2o;
  const showReservations = isProductFeatureEnabled('sales.customerReservations');
  const showChat = isProductFeatureEnabled('sales.chat');

  const revenuePeriodOptions = useMemo(
    () => [
      { label: t('revenueChart.period7'), value: 7 as RevenuePeriodDays },
      { label: t('revenueChart.period14'), value: 14 as RevenuePeriodDays },
      { label: t('revenueChart.period30'), value: 30 as RevenuePeriodDays },
    ],
    [t],
  );

  const alerts = useMemo(() => {
    const items: AlertItem[] = [];
    if (canInventory && (inventory?.nearExpiryBatchCount ?? 0) > 0) {
      items.push({
        key: 'near-expiry',
        label: t('kpis.nearExpiry.title', { days: inventory?.expiryDays ?? 30 }),
        count: inventory!.nearExpiryBatchCount,
        to: '/inventory/stock?tab=fefo',
        tone: 'danger',
      });
    }
    if (canInventory && (inventory?.lowStockProductCount ?? 0) > 0) {
      items.push({
        key: 'low-stock-products',
        label: t('kpis.lowStockProducts.title'),
        count: inventory!.lowStockProductCount,
        to: '/inventory/low-stock',
        tone: 'warning',
      });
    }
    if (canInventory && (inventory?.lowStockBatchCount ?? 0) > 0) {
      items.push({
        key: 'low-stock-batches',
        label: t('kpis.lowStockBatches.title'),
        count: inventory!.lowStockBatchCount,
        to: '/inventory/stock?tab=fefo',
        tone: 'warning',
      });
    }
    if (canProcurement && (procurement?.pendingReceiptCount ?? 0) > 0) {
      items.push({
        key: 'pending-po',
        label: t('kpis.pendingPoReceipt.title'),
        count: procurement!.pendingReceiptCount,
        to: '/procurement/purchase-orders?pendingReceipt=1',
        tone: 'warning',
      });
    }
    if (canSales && (o2o?.draftOrdersAwaitingCount ?? 0) > 0) {
      items.push({
        key: 'app-drafts',
        label: t('kpis.draftOrdersAwaiting.title'),
        count: o2o!.draftOrdersAwaitingCount,
        to: '/sales/app-orders/drafts?actionable=1',
        tone: 'warning',
      });
    }
    if (canSales && showReservations && (o2o?.reservationsAwaitingCount ?? 0) > 0) {
      items.push({
        key: 'reservations',
        label: t('kpis.reservationsAwaiting.title'),
        count: o2o!.reservationsAwaitingCount,
        to: '/sales/app-orders/reservations?awaiting=1',
        tone: 'warning',
      });
    }
    if (canSales && showChat && (o2o?.chatUnreadCount ?? 0) > 0) {
      items.push({
        key: 'chat',
        label: t('kpis.chatUnread.title'),
        count: o2o!.chatUnreadCount,
        to: '/sales/chat',
        tone: 'info',
      });
    }
    return items;
  }, [canInventory, canProcurement, canSales, inventory, o2o, procurement, showChat, showReservations, t]);

  const quickActions = useMemo(() => {
    const items: Array<{ key: string; label: string; to: string; icon: ReactNode; primary?: boolean }> = [];
    if (canSales) {
      items.push({
        key: 'pos',
        label: t('quickActions.pos'),
        to: '/sales/pos',
        icon: <ShopOutlined />,
        primary: true,
      });
    }
    if (canProcurement) {
      items.push({
        key: 'procurement',
        label: t('quickActions.procurement'),
        to: '/procurement/purchase-orders',
        icon: <ShoppingOutlined />,
      });
    }
    if (canReceivables && isProductFeatureEnabled('sales.receivables')) {
      items.push({
        key: 'receivables',
        label: t('quickActions.receivables'),
        to: '/receivables/customers',
        icon: <AccountBookOutlined />,
      });
    }
    if (canInventory) {
      items.push({
        key: 'low-stock',
        label: t('quickActions.lowStock'),
        to: '/inventory/low-stock',
        icon: <InboxOutlined />,
      });
    }
    if (canSales) {
      items.push({
        key: 'app-drafts',
        label: t('quickActions.appDrafts'),
        to: '/sales/app-orders/drafts?actionable=1',
        icon: <TeamOutlined />,
      });
    }
    return items;
  }, [canInventory, canProcurement, canReceivables, canSales, t]);

  const secondaryTiles = useMemo(() => {
    const tiles: MetricTileProps[] = [];
    if (canSales) {
      tiles.push({
        title: t('kpis.customers.title'),
        value: catalog?.customerCount ?? '—',
        icon: <TeamOutlined />,
        to: '/customer/list',
        hint: t('kpis.customers.hint'),
      });
      tiles.push({
        title: t('kpis.draftOrdersAwaiting.title'),
        value: o2o?.draftOrdersAwaitingCount ?? '—',
        icon: <ShoppingCartOutlined />,
        to: '/sales/app-orders/drafts?actionable=1',
        tone: (o2o?.draftOrdersAwaitingCount ?? 0) > 0 ? 'warning' : 'default',
      });
    }
    if (canCatalog) {
      tiles.push({
        title: t('kpis.products.title'),
        value: catalog?.productCount ?? '—',
        icon: <MedicineBoxOutlined />,
        to: '/catalog/products',
      });
    }
    if (canInventory) {
      tiles.push({
        title: t('kpis.activeBatches.title'),
        value: inventory?.activeBatchCount ?? '—',
        icon: <InboxOutlined />,
        to: '/inventory/stock?tab=fefo',
      });
      tiles.push({
        title: t('kpis.nearExpiry.title', { days: inventory?.expiryDays ?? 30 }),
        value: inventory?.nearExpiryBatchCount ?? '—',
        icon: <WarningOutlined />,
        to: '/inventory/stock?tab=fefo',
        tone: (inventory?.nearExpiryBatchCount ?? 0) > 0 ? 'danger' : 'default',
      });
      tiles.push({
        title: t('kpis.lowStockProducts.title'),
        value: inventory?.lowStockProductCount ?? '—',
        icon: <WarningOutlined />,
        to: '/inventory/low-stock',
        tone: (inventory?.lowStockProductCount ?? 0) > 0 ? 'warning' : 'default',
        hint: t('kpis.lowStockProducts.hint'),
      });
    }
    if (canProcurement) {
      tiles.push({
        title: t('kpis.pendingPoReceipt.title'),
        value: procurement?.pendingReceiptCount ?? '—',
        icon: <CalendarOutlined />,
        to: '/procurement/purchase-orders?pendingReceipt=1',
        tone: (procurement?.pendingReceiptCount ?? 0) > 0 ? 'warning' : 'default',
      });
    }
    if (canSales && showReservations) {
      tiles.push({
        title: t('kpis.reservationsAwaiting.title'),
        value: o2o?.reservationsAwaitingCount ?? '—',
        icon: <CalendarOutlined />,
        to: '/sales/app-orders/reservations?awaiting=1',
        tone: (o2o?.reservationsAwaitingCount ?? 0) > 0 ? 'warning' : 'default',
      });
    }
    if (canSales && showChat) {
      tiles.push({
        title: t('kpis.chatUnread.title'),
        value: o2o?.chatUnreadCount ?? '—',
        icon: <MessageOutlined />,
        to: '/sales/chat',
        tone: (o2o?.chatUnreadCount ?? 0) > 0 ? 'info' : 'default',
      });
    }
    return tiles;
  }, [
    canCatalog,
    canInventory,
    canProcurement,
    canSales,
    catalog,
    inventory,
    o2o,
    procurement,
    showChat,
    showReservations,
    t,
  ]);

  return (
    <div className="dashboard-page">
      <div className="dashboard-shell">
        <header className="dashboard-top">
          <div>
            <Typography.Title level={4} className="dashboard-top__title">
              {t('greeting', { name: user?.username ?? 'Admin' })}
            </Typography.Title>
            <div className="dashboard-top__meta">
              <Tag bordered={false} className="dashboard-top__tenant">
                {user?.tenantCode ?? 'DEMO_PHARMACY'}
              </Tag>
              {user?.roles.map((role) => (
                <Tag key={role} bordered={false} className="dashboard-top__role">
                  {role}
                </Tag>
              ))}
            </div>
          </div>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {tc('actions.reload')}
          </Button>
        </header>

        <Alert
          type="info"
          showIcon
          message={t('workspaceHint.title')}
          description={t('workspaceHint.description')}
          style={{ marginBottom: 16 }}
        />

        {quickActions.length > 0 ? (
          <div className="dashboard-actions">
            {quickActions.map((action) => (
              <Link
                key={action.key}
                to={action.to}
                className={action.primary ? 'dashboard-action dashboard-action--primary' : 'dashboard-action'}
              >
                <span className="dashboard-action__icon">{action.icon}</span>
                <span>{action.label}</span>
              </Link>
            ))}
          </div>
        ) : null}

        <Spin spinning={loading && !overview}>
          {canSales ? (
            <div className="dashboard-hero">
              <Link to="/sales/shift" className="dashboard-hero__stat dashboard-hero__stat--featured">
                <span className="dashboard-hero__label">{t('kpis.todayRevenue.title')}</span>
                <span className="dashboard-hero__value">{formatDisplayMoney(sales?.todayNetTotal)}</span>
                <span className="dashboard-hero__hint">{t('kpis.todayOrders.hint')}</span>
              </Link>
              <Link to="/reports/sales/revenue-by-period" className="dashboard-hero__stat">
                <span className="dashboard-hero__label">{t('hero.weekRevenue')}</span>
                <span className="dashboard-hero__value">{formatDisplayMoney(sales?.weekNetTotal)}</span>
                <span className="dashboard-hero__hint">{t('hero.weekRevenueHint')}</span>
              </Link>
              <Link to="/sales/orders" className="dashboard-hero__stat">
                <span className="dashboard-hero__label">{t('kpis.todayOrders.title')}</span>
                <span className="dashboard-hero__value">{sales?.todayOrderCount ?? '—'}</span>
                <span className="dashboard-hero__hint">
                  {t('kpis.customers.countHint', { count: catalog?.customerCount ?? 0 })}
                </span>
              </Link>
            </div>
          ) : null}

          {canSales ? (
            <section className="dashboard-analytics">
              <div className="dashboard-analytics__toolbar">
                <Typography.Text className="dashboard-analytics__title">{t('analytics.title')}</Typography.Text>
                <Segmented
                  size="small"
                  value={revenuePeriodDays}
                  options={revenuePeriodOptions}
                  onChange={(value) => setRevenuePeriodDays(value as RevenuePeriodDays)}
                />
              </div>
              <Row gutter={[16, 16]} className="dashboard-analytics__grid">
                <Col xs={24} xl={14}>
                  <DashboardRevenueChart enabled periodDays={revenuePeriodDays} />
                </Col>
                <Col xs={24} xl={10}>
                  <DashboardCategoryChart enabled periodDays={revenuePeriodDays} />
                </Col>
              </Row>
            </section>
          ) : null}

          {alerts.length > 0 ? (
            <div className="dashboard-alerts">
              <div className="dashboard-alerts__head">
                <WarningOutlined />
                <span>{t('alerts.title')}</span>
                <Tag color="orange">{alerts.length}</Tag>
              </div>
              <div className="dashboard-alerts__list">
                {alerts.map((alert) => (
                  <Link
                    key={alert.key}
                    to={alert.to}
                    className={`dashboard-alert-chip dashboard-alert-chip--${alert.tone}`}
                  >
                    <span>{alert.label}</span>
                    <strong>{alert.count}</strong>
                  </Link>
                ))}
              </div>
            </div>
          ) : (
            <div className="dashboard-alerts dashboard-alerts--clear">
              <CheckCircleOutlined />
              <span>{t('alerts.allClear')}</span>
            </div>
          )}

          {secondaryTiles.length > 0 ? (
            <section className="dashboard-grid-section">
              <Typography.Text type="secondary" className="dashboard-grid-section__title">
                {t('sections.details')}
              </Typography.Text>
              <Row gutter={[12, 12]}>
                {secondaryTiles.map((tile) => (
                  <Col key={tile.title} xs={24} sm={12} lg={8} xl={6}>
                    <MetricTile {...tile} />
                  </Col>
                ))}
              </Row>
            </section>
          ) : null}
        </Spin>
      </div>
    </div>
  );
}
