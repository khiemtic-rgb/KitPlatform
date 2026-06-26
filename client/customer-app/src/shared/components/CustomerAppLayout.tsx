import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { Badge } from 'antd';
import {
  HomeOutlined,
  MedicineBoxOutlined,
  MessageOutlined,
  ShoppingOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import { useCustomerChatUnread } from '@/shared/hooks/useCustomerChatUnread';
import { useCustomerDraftOrderAlerts } from '@/shared/hooks/useCustomerDraftOrderAlerts';

const tabs = [
  { to: '/', icon: <HomeOutlined />, label: 'Trang chủ' },
  { to: '/orders', icon: <ShoppingOutlined />, label: 'Đơn hàng' },
  { to: '/reminders', icon: <MedicineBoxOutlined />, label: 'Nhắc thuốc' },
  { to: '/chat', icon: <MessageOutlined />, label: 'Chat' },
  { to: '/profile', icon: <UserOutlined />, label: 'Tài khoản' },
] as const;

export function CustomerAppLayout() {
  const location = useLocation();
  const chatUnread = useCustomerChatUnread();
  const draftOrderAlerts = useCustomerDraftOrderAlerts();

  return (
    <div style={{ minHeight: '100vh', background: '#f0fdfa' }}>
      <header
        style={{
          position: 'sticky',
          top: 0,
          zIndex: 10,
          background: 'linear-gradient(135deg, #0f766e, #115e59)',
          color: '#fff',
          padding: '14px 16px',
          boxShadow: '0 2px 8px rgba(15,118,110,0.25)',
        }}
      >
        <div style={{ maxWidth: 480, margin: '0 auto', fontWeight: 600, fontSize: 17 }}>
          PharmaCore Khách hàng
        </div>
      </header>

      <main className="customer-app-content">
        <div style={{ maxWidth: 480, margin: '0 auto', padding: '0 12px' }}>
          <ApiHealthBanner />
        </div>
        <Outlet />
      </main>

      <nav className="customer-app-bottom-nav" aria-label="Điều hướng chính">
        <div className="customer-app-bottom-nav-inner">
          {tabs.map((tab) => {
            const active =
              tab.to === '/'
                ? location.pathname === '/'
                : location.pathname.startsWith(tab.to);
            const showDraftBadge = tab.to === '/orders' && draftOrderAlerts > 0 && !active;
            const showChatBadge = tab.to === '/chat' && chatUnread > 0 && !active;

            return (
              <NavLink
                key={tab.to}
                to={tab.to}
                end={tab.to === '/'}
                className={({ isActive }) =>
                  `customer-app-bottom-nav-item${isActive ? ' customer-app-bottom-nav-item--active' : ''}`
                }
              >
                {showDraftBadge ? (
                  <Badge
                    count={draftOrderAlerts > 99 ? '99+' : draftOrderAlerts}
                    size="small"
                    offset={[-2, 2]}
                  >
                    <span className="customer-app-bottom-nav-icon">{tab.icon}</span>
                  </Badge>
                ) : showChatBadge ? (
                  <Badge count={chatUnread > 99 ? '99+' : chatUnread} size="small" offset={[-2, 2]}>
                    <span className="customer-app-bottom-nav-icon">{tab.icon}</span>
                  </Badge>
                ) : (
                  <span className="customer-app-bottom-nav-icon">{tab.icon}</span>
                )}
                <span className="customer-app-bottom-nav-label">{tab.label}</span>
              </NavLink>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
