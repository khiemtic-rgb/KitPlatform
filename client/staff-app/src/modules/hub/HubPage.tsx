import { useCallback, useEffect, useState } from 'react';
import { App, Alert, Badge, Button, Space, Typography } from 'antd';
import {
  BarChartOutlined,
  DollarOutlined,
  FileSearchOutlined,
  FormOutlined,
  InboxOutlined,
  LogoutOutlined,
  MessageOutlined,
  PrinterOutlined,
  RollbackOutlined,
  ShoppingCartOutlined,
  SwapOutlined,
  TeamOutlined,
  AuditOutlined,
  ImportOutlined,
  SolutionOutlined,
} from '@ant-design/icons';
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

type MenuItemProps = {
  icon: React.ReactNode;
  label: string;
  hint?: string;
  badge?: number;
  onClick: () => void;
  compact?: boolean;
};

function HubMenuItem({ icon, label, hint, badge, onClick, compact }: MenuItemProps) {
  return (
    <button type="button" className="hub-menu-item" onClick={onClick}>
      <Space style={{ width: '100%', justifyContent: compact ? 'flex-start' : 'space-between' }} direction={compact ? 'vertical' : 'horizontal'} size={compact ? 8 : 'middle'}>
        <span className="hub-menu-icon">{icon}</span>
        <div style={{ textAlign: 'left', flex: 1 }}>
          <Typography.Text strong>{label}</Typography.Text>
          {hint && !compact ? (
            <div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {hint}
              </Typography.Text>
            </div>
          ) : null}
        </div>
        {!compact ? (
          badge != null && badge > 0 ? <Badge count={badge} /> : <span className="hub-menu-arrow">→</span>
        ) : badge != null && badge > 0 ? (
          <Badge count={badge} style={{ marginTop: 4 }} />
        ) : null}
      </Space>
    </button>
  );
}

export function HubPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clearSession = useAuthStore((s) => s.clearSession);
  const [unread, setUnread] = useState(0);
  const [reservationCount, setReservationCount] = useState(0);
  const [customerDraftCount, setCustomerDraftCount] = useState(0);
  const [remoteBuild, setRemoteBuild] = useState<string | null>(null);
  const localBuild = getLocalAppBuild();
  const canSales = useCanSalesRead();
  const canInventory = useCanInventoryRead();
  const canProcurement = useCanProcurementRead();

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

  useEffect(() => {
    void loadBadges();
    const timer = window.setInterval(() => void loadBadges(), 30_000);
    return () => window.clearInterval(timer);
  }, [loadBadges]);

  const logout = async () => {
    try {
      if (refreshToken) await logoutApi(refreshToken);
    } finally {
      clearSession();
      navigate('/login', { replace: true });
    }
  };

  return (
    <div className="staff-shell">
      <header className="staff-header">
        <AppBrandLogo height={32} maxWidth={120} />
        <Typography.Text type="secondary" style={{ display: 'block', fontSize: 13 }}>
          {user?.tenantCode ?? '—'} · {user?.username ?? '—'}
          {localBuild ? ` · v${localBuild}` : ''}
        </Typography.Text>
      </header>

      <main className="staff-body hub-body">
        {isBuildStale(remoteBuild) ? (
          <Alert
            type="warning"
            showIcon
            message="Đang có bản app mới"
            description="Bấm Cập nhật ngay để tải menu Chuyển kho, Đơn nháp và các tính năng mới."
            action={
              <Button size="small" type="primary" onClick={() => void enforceLatestAppBuild()}>
                Cập nhật ngay
              </Button>
            }
            style={{ marginBottom: 12 }}
          />
        ) : null}

        <button type="button" className="hub-primary-card" onClick={() => navigate('/pos')}>
          <Space align="start">
            <span className="hub-menu-icon">
              <ShoppingCartOutlined />
            </span>
            <div>
              <Typography.Text strong style={{ color: '#fff', fontSize: 18 }}>
                Bán hàng
              </Typography.Text>
              <div>
                <Typography.Text style={{ color: 'rgba(255,255,255,0.85)', fontSize: 13 }}>
                  POS · tìm SP · in bill
                </Typography.Text>
              </div>
            </div>
          </Space>
        </button>

        <Typography.Text className="hub-section-label">Khách hàng</Typography.Text>
        {canSales ? (
          <>
        <div className="hub-grid">
          <HubMenuItem
            compact
            icon={<TeamOutlined />}
            label="Khách + OTP"
            onClick={() => navigate('/customers')}
          />
          <HubMenuItem
            compact
            icon={<MessageOutlined />}
            label="Chat"
            badge={unread}
            onClick={() => navigate('/chat')}
          />
        </div>
        <div className="hub-list-item">
          <HubMenuItem
            icon={<InboxOutlined />}
            label="Giữ hàng"
            hint="Đơn app khách · đưa vào POS"
            badge={reservationCount}
            onClick={() => navigate('/reservations')}
          />
        </div>
        <div className="hub-list-item">
          <HubMenuItem
            icon={<SolutionOutlined />}
            label="Đơn nháp app khách"
            hint="Dược sĩ gửi · khách xác nhận → POS"
            badge={customerDraftCount}
            onClick={() => navigate('/customer-drafts')}
          />
        </div>
        <div className="hub-list-item">
          <HubMenuItem
            icon={<FormOutlined />}
            label="Đơn nháp"
            hint="Lưu tạm tại quầy · mở lại POS"
            onClick={() => navigate('/drafts')}
          />
        </div>
          </>
        ) : null}

        <Typography.Text className="hub-section-label">Quầy</Typography.Text>
        <div className="hub-grid">
          {canInventory ? (
            <>
              <HubMenuItem
                compact
                icon={<FileSearchOutlined />}
                label="Tra tồn"
                hint="Xem tồn mọi kho"
                onClick={() => navigate('/stock')}
              />
              <HubMenuItem
                compact
                icon={<SwapOutlined />}
                label="Chuyển kho"
                hint="Quầy lấy hàng nhau"
                onClick={() => navigate('/transfers')}
              />
              <HubMenuItem
                compact
                icon={<AuditOutlined />}
                label="Kiểm kê"
                hint="Đếm tồn · quét mã"
                onClick={() => navigate('/stocktake')}
              />
            </>
          ) : null}
          {canProcurement ? (
            <HubMenuItem
              compact
              icon={<ImportOutlined />}
              label="Nhập hàng"
              hint="Phiếu nhập · lô/HSD · cập nhật tồn"
              onClick={() => navigate('/goods-receipt')}
            />
          ) : null}
          {canSales ? (
            <>
              <HubMenuItem
                compact
                icon={<PrinterOutlined />}
                label="Đơn & in lại"
                onClick={() => navigate('/orders')}
              />
              <HubMenuItem
                compact
                icon={<DollarOutlined />}
                label="Thu công nợ"
                onClick={() => navigate('/collect')}
              />
              <HubMenuItem
                compact
                icon={<RollbackOutlined />}
                label="Trả hàng"
                onClick={() => navigate('/returns')}
              />
            </>
          ) : null}
        </div>

        <Typography.Text className="hub-section-label">Ca làm việc</Typography.Text>
        <div className="hub-list-item">
          <HubMenuItem
            icon={<BarChartOutlined />}
            label="Hôm nay"
            hint="Doanh thu ca · mở / đóng ca"
            onClick={() => navigate('/today')}
          />
        </div>

        <div className="hub-footnote">
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Báo cáo chi tiết · cấu hình nâng cao → admin trên máy tính
          </Typography.Text>
          <Typography.Text type="secondary" style={{ display: 'block', fontSize: 11, marginTop: 4 }}>
            Phiên bản app: {import.meta.env.VITE_APP_BUILD ?? 'dev'}
          </Typography.Text>
        </div>

        <Button
          block
          size="large"
          className="hub-logout-btn"
          icon={<LogoutOutlined />}
          onClick={() => void logout()}
        >
          Đăng xuất
        </Button>
      </main>
    </div>
  );
}
