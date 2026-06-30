import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, Card, Col, Row, Space, Spin, Statistic, Tag, Typography, message } from 'antd';
import {
  CalendarOutlined,
  InboxOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  ReloadOutlined,
  ShoppingCartOutlined,
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

interface KpiCardProps {
  title: string;
  value: string | number;
  prefix?: ReactNode;
  hint?: string;
  to?: string;
  linkLabel?: string;
  valueStyle?: React.CSSProperties;
}

function KpiCard({ title, value, prefix, hint, to, linkLabel, valueStyle }: KpiCardProps) {
  return (
    <Card size="small" hoverable={Boolean(to)}>
      <Statistic title={title} value={value} prefix={prefix} valueStyle={valueStyle} />
      {hint && (
        <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginTop: 8 }}>
          {hint}
        </Typography.Text>
      )}
      {to && (
        <Link to={to} style={{ fontSize: 12, marginTop: 8, display: 'inline-block' }}>
          {linkLabel}
        </Link>
      )}
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

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
        <div>
          <Typography.Title level={4} style={{ marginBottom: 4 }}>
            {t('greeting', { name: user?.username ?? 'Admin' })}
          </Typography.Title>
          <Space wrap>
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
      </Space>

      {canSales && (
        <>
          <Typography.Title level={5} style={{ margin: 0 }}>
            {t('sections.sales')}
          </Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[16, 16]}>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title={t('kpis.todayRevenue.title')}
                  value={formatDisplayMoney(sales?.todayNetTotal)}
                  prefix={<ShopOutlined />}
                  hint={t('kpis.todayRevenue.hint', {
                    amount: formatDisplayMoney(sales?.weekNetTotal),
                  })}
                  to="/sales/shift"
                  linkLabel={viewDetails}
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title={t('kpis.todayOrders.title')}
                  value={sales?.todayOrderCount ?? '—'}
                  prefix={<ShoppingCartOutlined />}
                  hint={t('kpis.todayOrders.hint')}
                  to="/sales/orders"
                  linkLabel={viewDetails}
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title={t('kpis.customers.title')}
                  value={catalog?.customerCount ?? '—'}
                  prefix={<TeamOutlined />}
                  hint={t('kpis.customers.hint')}
                  to="/customer/list"
                  linkLabel={viewDetails}
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title={t('kpis.draftOrdersAwaiting.title')}
                  value={o2o?.draftOrdersAwaitingCount ?? '—'}
                  prefix={<TeamOutlined />}
                  hint={t('kpis.draftOrdersAwaiting.hint')}
                  to="/sales/customer-drafts?actionable=1"
                  linkLabel={viewDetails}
                  valueStyle={
                    (o2o?.draftOrdersAwaitingCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                  }
                />
              </Col>
            </Row>
          </Spin>
        </>
      )}

      {(canInventory || canProcurement || canCatalog) && (
        <>
          <Typography.Title level={5} style={{ margin: 0 }}>
            {t('sections.inventoryProcurement')}
          </Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[16, 16]}>
              {canCatalog && (
                <Col xs={24} sm={12} lg={6}>
                  <KpiCard
                    title={t('kpis.products.title')}
                    value={catalog?.productCount ?? '—'}
                    prefix={<MedicineBoxOutlined />}
                    to="/catalog/products"
                    linkLabel={viewDetails}
                  />
                </Col>
              )}
              {canInventory && (
                <>
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
                      title={t('kpis.activeBatches.title')}
                      value={inventory?.activeBatchCount ?? '—'}
                      prefix={<InboxOutlined />}
                      to="/inventory/stock?tab=fefo"
                      linkLabel={viewDetails}
                    />
                  </Col>
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
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
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
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
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
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
              )}
              {canProcurement && (
                <Col xs={24} sm={12} lg={6}>
                  <KpiCard
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
              )}
            </Row>
          </Spin>
        </>
      )}

      {canSales && showO2oKpis && (
        <>
          <Typography.Title level={5} style={{ margin: 0 }}>
            {t('sections.customerApp')}
          </Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[16, 16]}>
              {showReservations && (
                <Col xs={24} sm={12} lg={8}>
                  <KpiCard
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
              )}
              {showChat && (
                <Col xs={24} sm={12} lg={8}>
                  <KpiCard
                    title={t('kpis.chatUnread.title')}
                    value={o2o?.chatUnreadCount ?? '—'}
                    prefix={<MessageOutlined />}
                    to="/sales/chat"
                    linkLabel={viewDetails}
                    valueStyle={(o2o?.chatUnreadCount ?? 0) > 0 ? { color: '#1677ff' } : undefined}
                  />
                </Col>
              )}
            </Row>
          </Spin>
        </>
      )}
    </Space>
  );
}
