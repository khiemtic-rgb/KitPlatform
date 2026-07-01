import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Card,
  Col,
  Row,
  Space,
  Spin,
  Statistic,
  Tag,
  Typography,
  message,
} from 'antd';
import {
  AccountBookOutlined,
  CalendarOutlined,
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

type AlertItem = {
  key: string;
  label: string;
  count: number;
  to: string;
  tone: 'danger' | 'warning' | 'info';
};

type KpiCardProps = {
  title: string;
  value: string | number;
  prefix?: ReactNode;
  hint?: string;
  to?: string;
  linkLabel?: string;
  valueStyle?: React.CSSProperties;
  className?: string;
};

function KpiCard({ title, value, prefix, hint, to, linkLabel, valueStyle, className }: KpiCardProps) {
  return (
    <Card size="small" hoverable={Boolean(to)} className={className}>
      <Statistic title={title} value={value} prefix={prefix} valueStyle={valueStyle} />
      {hint ? (
        <Typography.Text type="secondary" className="dashboard-kpi-card__hint">
          {hint}
        </Typography.Text>
      ) : null}
      {to ? (
        <Link to={to} className="dashboard-kpi-card__link">
          {linkLabel}
        </Link>
      ) : null}
    </Card>
  );
}

function HeroMetric({
  title,
  value,
  hint,
  to,
  linkLabel,
  accent,
}: {
  title: string;
  value: string | number;
  hint?: string;
  to?: string;
  linkLabel?: string;
  accent?: 'primary' | 'default';
}) {
  return (
    <Card className={`dashboard-hero-metric dashboard-hero-metric--${accent ?? 'default'}`}>
      <Typography.Text type="secondary" className="dashboard-hero-metric__title">
        {title}
      </Typography.Text>
      <Typography.Title level={3} className="dashboard-hero-metric__value">
        {value}
      </Typography.Title>
      {hint ? (
        <Typography.Text type="secondary" className="dashboard-hero-metric__hint">
          {hint}
        </Typography.Text>
      ) : null}
      {to && linkLabel ? (
        <Link to={to} className="dashboard-hero-metric__link">
          {linkLabel} <RightOutlined />
        </Link>
      ) : null}
    </Card>
  );
}

export function DashboardPage() {
  const { t } = useTranslation('dashboard');
  const { t: tc } = useTranslation('common');
  const user = useAuthStore((s) => s.user);
  const [overview, setOverview] = useState<DashboardOverview | null>(null);
  const [loading, setLoading] = useState(true);

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
  const showO2oKpis = showReservations || showChat;
  const viewDetails = t('viewDetails');

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
        to: '/sales/customer-drafts?actionable=1',
        tone: 'warning',
      });
    }
    if (canSales && showReservations && (o2o?.reservationsAwaitingCount ?? 0) > 0) {
      items.push({
        key: 'reservations',
        label: t('kpis.reservationsAwaiting.title'),
        count: o2o!.reservationsAwaitingCount,
        to: '/sales/customer-reservations?awaiting=1',
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
    const items: Array<{ key: string; label: string; to: string; icon: ReactNode }> = [];
    if (canSales) {
      items.push({
        key: 'pos',
        label: t('quickActions.pos'),
        to: '/sales/pos',
        icon: <ShopOutlined />,
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
        to: '/sales/customer-drafts?actionable=1',
        icon: <TeamOutlined />,
      });
    }
    return items;
  }, [canInventory, canProcurement, canReceivables, canSales, t]);

  return (
    <div className="dashboard-page">
      <div className="dashboard-page__header">
        <div>
          <Typography.Title level={4} className="dashboard-page__greeting">
            {t('greeting', { name: user?.username ?? 'Admin' })}
          </Typography.Title>
          <Space wrap size={[8, 4]}>
            <Tag color="blue">{user?.tenantCode ?? 'DEMO_PHARMACY'}</Tag>
            {user?.roles.map((role) => (
              <Tag key={role} color="green">
                {role}
              </Tag>
            ))}
          </Space>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
          {tc('actions.reload')}
        </Button>
      </div>

      {quickActions.length > 0 ? (
        <Card size="small" className="dashboard-page__quick-actions" title={t('quickActions.title')}>
          <Space wrap size={[8, 8]}>
            {quickActions.map((action) => (
              <Link key={action.key} to={action.to} className="dashboard-quick-action">
                {action.icon}
                <span>{action.label}</span>
              </Link>
            ))}
          </Space>
        </Card>
      ) : null}

      {canSales ? (
        <Spin spinning={loading && !overview}>
          <Row gutter={[16, 16]} className="dashboard-page__hero-row">
            <Col xs={24} md={8}>
              <HeroMetric
                accent="primary"
                title={t('kpis.todayRevenue.title')}
                value={formatDisplayMoney(sales?.todayNetTotal)}
                hint={t('kpis.todayOrders.hint')}
                to="/sales/shift"
                linkLabel={viewDetails}
              />
            </Col>
            <Col xs={24} md={8}>
              <HeroMetric
                title={t('hero.weekRevenue')}
                value={formatDisplayMoney(sales?.weekNetTotal)}
                hint={t('hero.weekRevenueHint')}
                to="/reports/sales/revenue-by-period"
                linkLabel={viewDetails}
              />
            </Col>
            <Col xs={24} md={8}>
              <HeroMetric
                title={t('kpis.todayOrders.title')}
                value={sales?.todayOrderCount ?? '—'}
                hint={t('kpis.customers.countHint', { count: catalog?.customerCount ?? 0 })}
                to="/sales/orders"
                linkLabel={viewDetails}
              />
            </Col>
          </Row>
        </Spin>
      ) : null}

      <Card
        size="small"
        className="dashboard-page__alerts"
        title={
          <Space>
            <WarningOutlined />
            {t('alerts.title')}
            {alerts.length > 0 ? <Tag color="orange">{alerts.length}</Tag> : null}
          </Space>
        }
      >
        {loading && !overview ? (
          <Spin size="small" />
        ) : alerts.length === 0 ? (
          <Typography.Text type="secondary">{t('alerts.allClear')}</Typography.Text>
        ) : (
          <div className="dashboard-alert-list">
            {alerts.map((alert) => (
              <Link
                key={alert.key}
                to={alert.to}
                className={`dashboard-alert-item dashboard-alert-item--${alert.tone}`}
              >
                <span className="dashboard-alert-item__label">{alert.label}</span>
                <Tag className="dashboard-alert-item__count">{alert.count}</Tag>
                <RightOutlined className="dashboard-alert-item__arrow" />
              </Link>
            ))}
          </div>
        )}
      </Card>

      {canSales ? (
        <section className="dashboard-page__section">
          <Typography.Title level={5}>{t('sections.sales')}</Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[12, 12]}>
              <Col xs={12} sm={8} lg={6}>
                <KpiCard
                  className="dashboard-kpi-card--compact"
                  title={t('kpis.customers.title')}
                  value={catalog?.customerCount ?? '—'}
                  prefix={<TeamOutlined />}
                  to="/customer/list"
                  linkLabel={viewDetails}
                />
              </Col>
              <Col xs={12} sm={8} lg={6}>
                <KpiCard
                  className="dashboard-kpi-card--compact"
                  title={t('kpis.draftOrdersAwaiting.title')}
                  value={o2o?.draftOrdersAwaitingCount ?? '—'}
                  prefix={<ShoppingCartOutlined />}
                  to="/sales/customer-drafts?actionable=1"
                  linkLabel={viewDetails}
                  valueStyle={
                    (o2o?.draftOrdersAwaitingCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                  }
                />
              </Col>
            </Row>
          </Spin>
        </section>
      ) : null}

      {(canInventory || canProcurement || canCatalog) ? (
        <section className="dashboard-page__section">
          <Typography.Title level={5}>{t('sections.inventoryProcurement')}</Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[12, 12]}>
              {canCatalog ? (
                <Col xs={12} sm={8} lg={6}>
                  <KpiCard
                    className="dashboard-kpi-card--compact"
                    title={t('kpis.products.title')}
                    value={catalog?.productCount ?? '—'}
                    prefix={<MedicineBoxOutlined />}
                    to="/catalog/products"
                    linkLabel={viewDetails}
                  />
                </Col>
              ) : null}
              {canInventory ? (
                <>
                  <Col xs={12} sm={8} lg={6}>
                    <KpiCard
                      className="dashboard-kpi-card--compact"
                      title={t('kpis.activeBatches.title')}
                      value={inventory?.activeBatchCount ?? '—'}
                      prefix={<InboxOutlined />}
                      to="/inventory/stock?tab=fefo"
                      linkLabel={viewDetails}
                    />
                  </Col>
                  <Col xs={12} sm={8} lg={6}>
                    <KpiCard
                      className="dashboard-kpi-card--compact"
                      title={t('kpis.nearExpiry.title', { days: inventory?.expiryDays ?? 30 })}
                      value={inventory?.nearExpiryBatchCount ?? '—'}
                      prefix={<WarningOutlined />}
                      to="/inventory/stock?tab=fefo"
                      linkLabel={viewDetails}
                      valueStyle={
                        (inventory?.nearExpiryBatchCount ?? 0) > 0 ? { color: '#cf1322' } : undefined
                      }
                    />
                  </Col>
                  <Col xs={12} sm={8} lg={6}>
                    <KpiCard
                      className="dashboard-kpi-card--compact"
                      title={t('kpis.lowStockProducts.title')}
                      value={inventory?.lowStockProductCount ?? '—'}
                      prefix={<WarningOutlined />}
                      hint={t('kpis.lowStockProducts.hint')}
                      to="/inventory/low-stock"
                      linkLabel={viewDetails}
                      valueStyle={
                        (inventory?.lowStockProductCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                      }
                    />
                  </Col>
                  <Col xs={12} sm={8} lg={6}>
                    <KpiCard
                      className="dashboard-kpi-card--compact"
                      title={t('kpis.lowStockBatches.title')}
                      value={inventory?.lowStockBatchCount ?? '—'}
                      prefix={<InboxOutlined />}
                      hint={t('kpis.lowStockBatches.hint')}
                      to="/inventory/stock?tab=fefo"
                      linkLabel={viewDetails}
                      valueStyle={
                        (inventory?.lowStockBatchCount ?? 0) > 0 ? { color: '#cf1322' } : undefined
                      }
                    />
                  </Col>
                </>
              ) : null}
              {canProcurement ? (
                <Col xs={12} sm={8} lg={6}>
                  <KpiCard
                    className="dashboard-kpi-card--compact"
                    title={t('kpis.pendingPoReceipt.title')}
                    value={procurement?.pendingReceiptCount ?? '—'}
                    prefix={<CalendarOutlined />}
                    to="/procurement/purchase-orders?pendingReceipt=1"
                    linkLabel={viewDetails}
                    valueStyle={
                      (procurement?.pendingReceiptCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                    }
                  />
                </Col>
              ) : null}
            </Row>
          </Spin>
        </section>
      ) : null}

      {canSales && showO2oKpis ? (
        <section className="dashboard-page__section">
          <Typography.Title level={5}>{t('sections.customerApp')}</Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[12, 12]}>
              {showReservations ? (
                <Col xs={12} sm={8} lg={6}>
                  <KpiCard
                    className="dashboard-kpi-card--compact"
                    title={t('kpis.reservationsAwaiting.title')}
                    value={o2o?.reservationsAwaitingCount ?? '—'}
                    prefix={<CalendarOutlined />}
                    hint={t('kpis.reservationsAwaiting.hint')}
                    to="/sales/customer-reservations?awaiting=1"
                    linkLabel={viewDetails}
                    valueStyle={
                      (o2o?.reservationsAwaitingCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                    }
                  />
                </Col>
              ) : null}
              {showChat ? (
                <Col xs={12} sm={8} lg={6}>
                  <KpiCard
                    className="dashboard-kpi-card--compact"
                    title={t('kpis.chatUnread.title')}
                    value={o2o?.chatUnreadCount ?? '—'}
                    prefix={<MessageOutlined />}
                    to="/sales/chat"
                    linkLabel={viewDetails}
                    valueStyle={(o2o?.chatUnreadCount ?? 0) > 0 ? { color: '#1677ff' } : undefined}
                  />
                </Col>
              ) : null}
            </Row>
          </Spin>
        </section>
      ) : null}
    </div>
  );
}
