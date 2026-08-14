import { useCallback, useEffect, useMemo, useState } from 'react';
import { App, Alert, Badge, Button, Drawer, Typography } from 'antd';
import {
  AppstoreOutlined,
  AuditOutlined,
  BarChartOutlined,
  BellOutlined,
  DollarOutlined,
  FileSearchOutlined,
  FormOutlined,
  HomeOutlined,
  ImportOutlined,
  InboxOutlined,
  LogoutOutlined,
  MenuOutlined,
  MessageOutlined,
  MoreOutlined,
  OrderedListOutlined,
  PrinterOutlined,
  RollbackOutlined,
  ScanOutlined,
  ShoppingCartOutlined,
  SolutionOutlined,
  SwapOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { useNavigate } from 'react-router-dom';
import { fetchChatThreads, sumUnreadThreads } from '@/shared/api/chat.api';
import { countActiveReservations, fetchReservations, RESERVATION_STATUS } from '@/shared/api/reservations.api';
import { logoutApi } from '@/shared/api/auth.api';
import { useAuthStore } from '@/shared/auth/auth.store';
import { AppBrandLogo } from '@/shared/components/AppBrandLogo';
import {
  enforceLatestAppBuild,
  fetchRemoteAppBuild,
  getLocalAppBuild,
  isBuildStale,
} from '@/shared/pwa/app-version';
import {
  useCanInventoryRead,
  useCanProcurementRead,
  useCanSalesRead,
} from '@/shared/auth/usePermission';
import { fetchCustomerDraftOrders, CUSTOMER_DRAFT_ORDER_STATUS } from '@/shared/api/customer-draft-orders.api';
import {
  fetchOpenShift,
  fetchShiftSummary,
  fetchWarehouses,
} from '@/shared/api/sales.api';
import type { SalesShiftDetail } from '@/shared/api/sales.types';
import { formatMoney } from '@/shared/utils/money';
import { usePosSession } from '@/modules/pos/pos-session.store';

type TileProps = {
  icon: React.ReactNode;
  label: string;
  hint?: string;
  badge?: number;
  onClick: () => void;
};

type ListRowProps = {
  icon: React.ReactNode;
  label: string;
  hint?: string;
  badge?: number;
  onClick: () => void;
  tone?: 'teal' | 'green' | 'blue' | 'orange' | 'purple' | 'amber' | 'rose' | 'slate';
};

type GridTileProps = {
  icon: React.ReactNode;
  label: string;
  onClick: () => void;
  tone?: ListRowProps['tone'];
};

const TONE_CLASS: Record<NonNullable<ListRowProps['tone']>, string> = {
  teal: 'tone-teal',
  green: 'tone-green',
  blue: 'tone-blue',
  orange: 'tone-orange',
  purple: 'tone-purple',
  amber: 'tone-amber',
  rose: 'tone-rose',
  slate: 'tone-slate',
};

function HubTile({ icon, label, hint, badge, onClick }: TileProps) {
  return (
    <button type="button" className="hub-tile" onClick={onClick}>
      <span className="hub-icon-well tone-teal">{icon}</span>
      <span className="hub-tile-label">{label}</span>
      {hint ? <span className="hub-tile-hint">{hint}</span> : null}
      {badge != null && badge > 0 ? <Badge count={badge} className="hub-tile-badge" /> : null}
    </button>
  );
}

function HubListRow({ icon, label, hint, badge, onClick, tone = 'teal' }: ListRowProps) {
  return (
    <button type="button" className="hub-list-row" onClick={onClick}>
      <span className={`hub-icon-well ${TONE_CLASS[tone]}`}>{icon}</span>
      <span className="hub-list-copy">
        <span className="hub-list-label">{label}</span>
        {hint ? <span className="hub-list-hint">{hint}</span> : null}
      </span>
      {badge != null && badge > 0 ? <Badge count={badge} /> : <span className="hub-chevron">›</span>}
    </button>
  );
}

function HubGridTile({ icon, label, onClick, tone = 'teal' }: GridTileProps) {
  return (
    <button type="button" className="hub-grid-tile" onClick={onClick}>
      <span className={`hub-icon-well ${TONE_CLASS[tone]}`}>{icon}</span>
      <span className="hub-grid-label">{label}</span>
    </button>
  );
}

export function HubPage() {
  const navigate = useNavigate();
  const { message } = App.useApp();
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clearSession = useAuthStore((s) => s.clearSession);
  const posWarehouseId = usePosSession((s) => s.warehouseId);

  const [unread, setUnread] = useState(0);
  const [reservationCount, setReservationCount] = useState(0);
  const [customerDraftCount, setCustomerDraftCount] = useState(0);
  const [remoteBuild, setRemoteBuild] = useState<string | null>(null);
  const [openShift, setOpenShift] = useState<SalesShiftDetail | null>(null);
  const [todayNet, setTodayNet] = useState<number | null>(null);
  const [moreOpen, setMoreOpen] = useState(false);
  const [counterExpanded, setCounterExpanded] = useState(false);

  const localBuild = getLocalAppBuild();
  const canSales = useCanSalesRead();
  const canInventory = useCanInventoryRead();
  const canProcurement = useCanProcurementRead();

  const pharmacyName = user?.tenantCode ? `Nhà thuốc ${user.tenantCode}` : 'Nhà thuốc';

  useEffect(() => {
    void (async () => {
      const remote = await fetchRemoteAppBuild();
      setRemoteBuild(remote);
      if (isBuildStale(remote)) {
        await enforceLatestAppBuild();
      }
    })();
  }, []);

  const loadBadges = useCallback(async () => {
    try {
      const [threads, reservations, customerDrafts] = await Promise.all([
        fetchChatThreads(),
        fetchReservations([
          RESERVATION_STATUS.Pending,
          RESERVATION_STATUS.Confirmed,
          RESERVATION_STATUS.Ready,
        ]),
        fetchCustomerDraftOrders([
          CUSTOMER_DRAFT_ORDER_STATUS.Sent,
          CUSTOMER_DRAFT_ORDER_STATUS.Confirmed,
        ]).catch(() => []),
      ]);
      setUnread(sumUnreadThreads(threads));
      setReservationCount(countActiveReservations(reservations));
      setCustomerDraftCount(customerDrafts.length);
    } catch {
      setUnread(0);
      setReservationCount(0);
      setCustomerDraftCount(0);
    }
  }, []);

  const loadShiftCard = useCallback(async () => {
    try {
      const warehouses = await fetchWarehouses();
      const warehouseId = posWarehouseId ?? warehouses[0]?.id;
      const start = dayjs().startOf('day').toISOString();
      const end = dayjs().endOf('day').toISOString();
      const [shift, daySummary] = await Promise.all([
        warehouseId ? fetchOpenShift(warehouseId) : Promise.resolve(null),
        fetchShiftSummary(start, end).catch(() => null),
      ]);
      setOpenShift(shift);
      setTodayNet(daySummary?.netTotal ?? shift?.summary?.netTotal ?? null);
    } catch {
      setOpenShift(null);
      setTodayNet(null);
    }
  }, [posWarehouseId]);

  useEffect(() => {
    void loadBadges();
    void loadShiftCard();
    const timer = window.setInterval(() => {
      void loadBadges();
      void loadShiftCard();
    }, 30_000);
    return () => window.clearInterval(timer);
  }, [loadBadges, loadShiftCard]);

  const logout = async () => {
    try {
      if (refreshToken) await logoutApi(refreshToken);
    } finally {
      clearSession();
      navigate('/login', { replace: true });
    }
  };

  const counterItems = useMemo(() => {
    const items: Array<{
      key: string;
      icon: React.ReactNode;
      label: string;
      tone: NonNullable<ListRowProps['tone']>;
      onClick: () => void;
    }> = [];

    if (canInventory) {
      items.push(
        {
          key: 'stock',
          icon: <FileSearchOutlined />,
          label: 'Tra tồn',
          tone: 'blue',
          onClick: () => navigate('/stock'),
        },
        {
          key: 'transfers',
          icon: <SwapOutlined />,
          label: 'Chuyển kho',
          tone: 'purple',
          onClick: () => navigate('/transfers'),
        },
        {
          key: 'stocktake',
          icon: <AuditOutlined />,
          label: 'Kiểm kê',
          tone: 'orange',
          onClick: () => navigate('/stocktake'),
        },
      );
    }
    if (canProcurement) {
      items.push({
        key: 'grn',
        icon: <ImportOutlined />,
        label: 'Nhập hàng',
        tone: 'green',
        onClick: () => navigate('/goods-receipt'),
      });
    }
    if (canSales) {
      items.push(
        {
          key: 'orders',
          icon: <PrinterOutlined />,
          label: 'In đơn & in lại',
          tone: 'blue',
          onClick: () => navigate('/orders'),
        },
        {
          key: 'collect',
          icon: <DollarOutlined />,
          label: 'Thu công nợ',
          tone: 'amber',
          onClick: () => navigate('/collect'),
        },
        {
          key: 'returns',
          icon: <RollbackOutlined />,
          label: 'Trả hàng',
          tone: 'rose',
          onClick: () => navigate('/returns'),
        },
      );
    }
    return items;
  }, [canInventory, canProcurement, canSales, navigate]);

  const visibleCounter = counterExpanded ? counterItems : counterItems.slice(0, 7);
  const hasMoreCounter = counterItems.length > 7;

  return (
    <div className="staff-shell hub-shell">
      <header className="hub-topbar">
        <button
          type="button"
          className="hub-icon-btn"
          aria-label="Menu"
          onClick={() => setMoreOpen(true)}
        >
          <MenuOutlined />
        </button>

        <div className="hub-topbar-brand">
          <AppBrandLogo height={28} maxWidth={72} />
          <div className="hub-topbar-titles">
            <Typography.Text className="hub-pharmacy-name" ellipsis>
              {pharmacyName}
            </Typography.Text>
            <Typography.Text type="secondary" className="hub-pharmacy-sub">
              {user?.username ?? '—'}
              {localBuild ? ` · v${localBuild}` : ''}
            </Typography.Text>
          </div>
        </div>

        <button
          type="button"
          className="hub-icon-btn"
          aria-label="Thông báo"
          onClick={() => navigate('/chat')}
        >
          <Badge count={unread} size="small" offset={[-2, 2]}>
            <BellOutlined />
          </Badge>
        </button>
      </header>

      <main className="staff-body hub-body">
        {isBuildStale(remoteBuild) ? (
          <Alert
            type="warning"
            showIcon
            message="Đang có bản app mới"
            description="Bấm Cập nhật ngay để tải menu và tính năng mới."
            action={
              <Button size="small" type="primary" onClick={() => void enforceLatestAppBuild()}>
                Cập nhật ngay
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : null}

        {canSales ? (
          <div className="hub-primary-card">
            <button type="button" className="hub-primary-main" onClick={() => navigate('/pos')}>
              <span className="hub-primary-icon">
                <ShoppingCartOutlined />
              </span>
              <span className="hub-primary-copy">
                <span className="hub-primary-title">BÁN HÀNG</span>
                <span className="hub-primary-hint">POS · Tìm SP · Quét mã · In bill</span>
              </span>
            </button>
            <button
              type="button"
              className="hub-primary-scan"
              aria-label="Quét mã bán hàng"
              onClick={() => navigate('/pos')}
            >
              <ScanOutlined />
            </button>
            <span className="hub-primary-chevron" aria-hidden>
              ›
            </span>
          </div>
        ) : null}

        {canSales ? (
          <>
            <Typography.Text className="hub-section-label">Khách hàng</Typography.Text>
            <div className="hub-tile-row">
              <HubTile
                icon={<TeamOutlined />}
                label="Khách + OTP"
                hint="Thêm & tìm khách"
                onClick={() => navigate('/customers')}
              />
              <HubTile
                icon={<MessageOutlined />}
                label="Chat"
                hint="Chat với khách"
                badge={unread}
                onClick={() => navigate('/chat')}
              />
            </div>

            <div className="hub-list-card">
              <HubListRow
                icon={<InboxOutlined />}
                label="Giữ hàng"
                hint="Đơn app khách · đưa vào POS"
                badge={reservationCount}
                tone="green"
                onClick={() => navigate('/reservations')}
              />
              <HubListRow
                icon={<SolutionOutlined />}
                label="Đơn nháp app khách"
                hint="Dược sĩ gửi · khách xác nhận → POS"
                badge={customerDraftCount}
                tone="teal"
                onClick={() => navigate('/customer-drafts')}
              />
              <HubListRow
                icon={<FormOutlined />}
                label="Đơn nháp"
                hint="Lưu tạm tại quầy · mở lại POS"
                tone="teal"
                onClick={() => navigate('/drafts')}
              />
            </div>
          </>
        ) : null}

        {(canInventory || canProcurement || canSales) && counterItems.length > 0 ? (
          <>
            <Typography.Text className="hub-section-label">Quầy</Typography.Text>
            <div className="hub-counter-grid">
              {visibleCounter.map((item) => (
                <HubGridTile
                  key={item.key}
                  icon={item.icon}
                  label={item.label}
                  tone={item.tone}
                  onClick={item.onClick}
                />
              ))}
              {hasMoreCounter ? (
                <HubGridTile
                  icon={counterExpanded ? <AppstoreOutlined /> : <AppstoreOutlined />}
                  label={counterExpanded ? 'Thu gọn' : 'Xem thêm'}
                  tone="slate"
                  onClick={() => setCounterExpanded((v) => !v)}
                />
              ) : null}
            </div>
          </>
        ) : null}

        <Typography.Text className="hub-section-label">Ca làm việc</Typography.Text>
        <div className="hub-shift-card">
          <button type="button" className="hub-shift-main" onClick={() => navigate('/today')}>
            <span className="hub-icon-well tone-green">
              <BarChartOutlined />
            </span>
            <span className="hub-shift-copy">
              <span className="hub-list-label">Hôm nay</span>
              <span className="hub-list-hint">Doanh thu ca · mở / đóng ca</span>
            </span>
            <span className="hub-shift-amount">
              {todayNet != null ? formatMoney(todayNet) : '—'}
            </span>
            <span className="hub-chevron">›</span>
          </button>
          <div className="hub-shift-meta">
            {openShift ? (
              <>
                <span className="hub-shift-pill">
                  <span className="hub-shift-dot" />
                  Ca đang mở
                </span>
                <span className="hub-shift-meta-text">
                  {openShift.openedAt ? dayjs(openShift.openedAt).format('HH:mm') : '—'}
                  {openShift.shiftNumber ? ` · ${openShift.shiftNumber}` : ''}
                </span>
                <button type="button" className="hub-shift-close" onClick={() => navigate('/today')}>
                  Chốt ca
                </button>
              </>
            ) : (
              <span className="hub-shift-meta-text hub-shift-meta-warn">Chưa mở ca · mở tại POS</span>
            )}
          </div>
        </div>
      </main>

      <nav className="hub-tabbar" aria-label="Điều hướng chính">
        <button type="button" className="hub-tab is-active" onClick={() => window.scrollTo({ top: 0 })}>
          <HomeOutlined />
          <span>Trang chủ</span>
        </button>
        <button
          type="button"
          className="hub-tab"
          onClick={() => (canSales ? navigate('/orders') : message.warning('Không có quyền xem đơn'))}
        >
          <OrderedListOutlined />
          <span>Đơn hàng</span>
        </button>
        <button
          type="button"
          className="hub-tab hub-tab-scan"
          aria-label="Quét mã"
          onClick={() => (canSales ? navigate('/pos') : message.warning('Không có quyền bán hàng'))}
        >
          <span className="hub-scan-fab">
            <ScanOutlined />
          </span>
          <span>Quét mã</span>
        </button>
        <button type="button" className="hub-tab" onClick={() => navigate('/chat')}>
          <Badge count={unread} size="small" offset={[4, -2]}>
            <BellOutlined />
          </Badge>
          <span>Thông báo</span>
        </button>
        <button type="button" className="hub-tab" onClick={() => setMoreOpen(true)}>
          <MoreOutlined />
          <span>Thêm</span>
        </button>
      </nav>

      <Drawer
        title="Thêm"
        placement="bottom"
        height="auto"
        open={moreOpen}
        onClose={() => setMoreOpen(false)}
        className="hub-more-drawer"
      >
        <div className="hub-more-body">
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 12, fontSize: 13 }}>
            Báo cáo chi tiết · cấu hình nâng cao → admin trên máy tính
          </Typography.Text>
          <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 16, fontSize: 12 }}>
            Phiên bản app: {import.meta.env.VITE_APP_BUILD ?? localBuild ?? 'dev'}
          </Typography.Text>
          <Button
            block
            size="large"
            danger
            icon={<LogoutOutlined />}
            onClick={() => void logout()}
          >
            Đăng xuất
          </Button>
        </div>
      </Drawer>
    </div>
  );
}
