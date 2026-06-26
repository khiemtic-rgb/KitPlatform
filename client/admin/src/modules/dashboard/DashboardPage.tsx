import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
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
          {linkLabel ?? 'Xem chi tiết →'}
        </Link>
      )}
    </Card>
  );
}

export function DashboardPage() {
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
      message.error(apiErrorMessage(error, 'Không tải được tổng quan'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const sales = overview?.sales;
  const catalog = overview?.catalog;
  const inventory = overview?.inventory;
  const procurement = overview?.procurement;
  const o2o = overview?.o2o;

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
        <div>
          <Typography.Title level={4} style={{ marginBottom: 4 }}>
            Xin chào, {user?.username ?? 'Admin'}
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
          Tải lại
        </Button>
      </Space>

      {canSales && (
        <>
          <Typography.Title level={5} style={{ margin: 0 }}>
            Bán hàng
          </Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[16, 16]}>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title="Doanh thu hôm nay"
                  value={formatDisplayMoney(sales?.todayNetTotal)}
                  prefix={<ShopOutlined />}
                  hint={`7 ngày (VN): ${formatDisplayMoney(sales?.weekNetTotal)}`}
                  to="/sales/shift"
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title="Đơn bán hôm nay"
                  value={sales?.todayOrderCount ?? '—'}
                  prefix={<ShoppingCartOutlined />}
                  hint="Theo ngày VN (UTC+7)"
                  to="/sales/orders"
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title="Khách hàng"
                  value={catalog?.customerCount ?? '—'}
                  prefix={<TeamOutlined />}
                  hint="Hồ sơ CRM"
                  to="/customer/list"
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title="Đơn app chờ xử lý"
                  value={o2o?.draftOrdersAwaitingCount ?? '—'}
                  prefix={<TeamOutlined />}
                  hint="Đã gửi / khách xác nhận"
                  to="/sales/customer-drafts?actionable=1"
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
            Kho & mua hàng
          </Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[16, 16]}>
              {canCatalog && (
                <Col xs={24} sm={12} lg={6}>
                  <KpiCard
                    title="Sản phẩm"
                    value={catalog?.productCount ?? '—'}
                    prefix={<MedicineBoxOutlined />}
                    to="/catalog/products"
                  />
                </Col>
              )}
              {canInventory && (
                <>
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
                      title="Lô đang tồn"
                      value={inventory?.activeBatchCount ?? '—'}
                      prefix={<InboxOutlined />}
                      to="/inventory/stock?tab=fefo"
                    />
                  </Col>
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
                      title={`Sắp hết HSD (${inventory?.expiryDays ?? 30} ngày)`}
                      value={inventory?.nearExpiryBatchCount ?? '—'}
                      prefix={<WarningOutlined />}
                      to="/inventory/stock?tab=fefo"
                      valueStyle={
                        (inventory?.nearExpiryBatchCount ?? 0) > 0 ? { color: '#cf1322' } : undefined
                      }
                    />
                  </Col>
                  <Col xs={24} sm={12} lg={6}>
                    <KpiCard
                      title="Tồn thấp (≤10)"
                      value={inventory?.lowStockBatchCount ?? '—'}
                      prefix={<InboxOutlined />}
                      hint="Lô còn hàng nhưng SL thấp"
                      to="/inventory/stock?tab=fefo"
                      valueStyle={
                        (inventory?.lowStockBatchCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                      }
                    />
                  </Col>
                </>
              )}
              {canProcurement && (
                <Col xs={24} sm={12} lg={6}>
                  <KpiCard
                    title="PO chờ nhận hàng"
                    value={procurement?.pendingReceiptCount ?? '—'}
                    prefix={<CalendarOutlined />}
                    to="/procurement/purchase-orders?pendingReceipt=1"
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

      {canSales && (
        <>
          <Typography.Title level={5} style={{ margin: 0 }}>
            App khách
          </Typography.Title>
          <Spin spinning={loading && !overview}>
            <Row gutter={[16, 16]}>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title="Đặt trước chờ xử lý"
                  value={o2o?.reservationsAwaitingCount ?? '—'}
                  prefix={<CalendarOutlined />}
                  hint="Chờ duyệt / sẵn sàng"
                  to="/sales/customer-reservations?awaiting=1"
                  valueStyle={
                    (o2o?.reservationsAwaitingCount ?? 0) > 0 ? { color: '#d48806' } : undefined
                  }
                />
              </Col>
              <Col xs={24} sm={12} lg={8}>
                <KpiCard
                  title="Chat chưa đọc"
                  value={o2o?.chatUnreadCount ?? '—'}
                  prefix={<MessageOutlined />}
                  to="/sales/chat"
                  valueStyle={(o2o?.chatUnreadCount ?? 0) > 0 ? { color: '#1677ff' } : undefined}
                />
              </Col>
            </Row>
          </Spin>
        </>
      )}
    </Space>
  );
}
