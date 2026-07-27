import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { Badge } from 'antd';
import {
  HeartOutlined,
  HomeOutlined,
  MessageOutlined,
  PlusOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { prefetchOverviewForPath } from '@/shared/api/overview-queries';
import { useCustomerChatUnread } from '@/shared/hooks/useCustomerChatUnread';
import { useCustomerDraftOrderAlerts } from '@/shared/hooks/useCustomerDraftOrderAlerts';
import { preloadRouteChunk } from '@/shared/routing/route-preload';

export function CustomerAppLayout() {
  const { t } = useTranslation();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { branding } = useCustomerBranding();
  const chatUnread = useCustomerChatUnread();
  const draftOrderAlerts = useCustomerDraftOrderAlerts();
  const isChat = location.pathname.startsWith('/chat');
  const isOrders = location.pathname.startsWith('/orders');
  const isHealth = location.pathname.startsWith('/health');
  const isLoyalty = location.pathname.startsWith('/loyalty');
  const isProfile = location.pathname.startsWith('/profile');
  const isReminders = location.pathname.startsWith('/reminders');
  const isFamily = location.pathname.startsWith('/family');
  const isAi = location.pathname.startsWith('/ai');
  const isHome = location.pathname === '/';

  const tabs = [
    { to: '/', icon: <HomeOutlined />, label: t('nav.home'), kind: 'link' as const },
    { to: '/health', icon: <HeartOutlined />, label: t('nav.health'), kind: 'link' as const },
    { to: '/orders', icon: <PlusOutlined />, label: t('nav.orderMeds'), kind: 'fab' as const },
    { to: '/chat', icon: <MessageOutlined />, label: t('nav.chat'), kind: 'link' as const },
    { to: '/profile', icon: <UserOutlined />, label: t('nav.account'), kind: 'link' as const },
  ];

  const headerGradient = `linear-gradient(90deg, ${branding.primaryColor}, ${branding.secondaryColor})`;

  const warmTab = (path: string) => {
    if (path === '/') return;
    preloadRouteChunk(path);
    void prefetchOverviewForPath(queryClient, path);
  };

  const hideChromeHeader =
    !isHome &&
    !isChat &&
    !isOrders &&
    !isHealth &&
    !isLoyalty &&
    !isProfile &&
    !isReminders &&
    !isFamily &&
    !isAi;

  return (
    <div
      className={`customer-app-shell${isChat ? ' customer-app-shell--chat' : ''}${
        isHome ? ' customer-app-shell--home' : ''
      }${isOrders ? ' customer-app-shell--orders' : ''}${isHealth ? ' customer-app-shell--health' : ''}${
        isLoyalty ? ' customer-app-shell--loyalty' : ''
      }${isProfile ? ' customer-app-shell--profile' : ''}${isReminders ? ' customer-app-shell--reminders' : ''}${
        isFamily ? ' customer-app-shell--family' : ''
      }${isAi ? ' customer-app-shell--ai' : ''}`}
    >
      {hideChromeHeader ? (
        <header className="customer-app-header" style={{ background: headerGradient }}>
          <div className="customer-app-header-inner">
            <div className="customer-app-header-brand">
              <BrandingLogo logoUrl={branding.logoUrl} size={36} />
              <div className="customer-app-header-text">
                <div className="customer-app-header-title">{branding.appName}</div>
                {branding.tagline ? (
                  <div className="customer-app-header-tagline">{branding.tagline}</div>
                ) : null}
              </div>
            </div>
          </div>
        </header>
      ) : null}

      <main
        className={
          isChat ? 'customer-app-content customer-app-content--chat' : 'customer-app-content'
        }
      >
        {!isChat &&
        !isOrders &&
        !isHealth &&
        !isLoyalty &&
        !isProfile &&
        !isReminders &&
        !isFamily &&
        !isAi ? (
          <div className="customer-app-banner-wrap">
            <ApiHealthBanner />
          </div>
        ) : null}
        <Outlet />
      </main>

      <nav className="customer-app-bottom-nav" aria-label={t('nav.main')}>
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
                  `customer-app-bottom-nav-item${isActive ? ' customer-app-bottom-nav-item--active' : ''}${
                    tab.kind === 'fab' ? ' customer-app-bottom-nav-item--fab' : ''
                  }`
                }
                onTouchStart={() => warmTab(tab.to)}
                onMouseEnter={() => warmTab(tab.to)}
                onFocus={() => warmTab(tab.to)}
              >
                {tab.kind === 'fab' ? (
                  <>
                    <span className="customer-app-bottom-nav-fab">
                      {showDraftBadge ? (
                        <Badge
                          count={draftOrderAlerts > 99 ? '99+' : draftOrderAlerts}
                          size="small"
                          offset={[-2, 2]}
                        >
                          {tab.icon}
                        </Badge>
                      ) : (
                        tab.icon
                      )}
                    </span>
                    <span className="customer-app-bottom-nav-label">{tab.label}</span>
                  </>
                ) : showChatBadge ? (
                  <>
                    <Badge count={chatUnread > 99 ? '99+' : chatUnread} size="small" offset={[-2, 2]}>
                      <span className="customer-app-bottom-nav-icon">{tab.icon}</span>
                    </Badge>
                    <span className="customer-app-bottom-nav-label">{tab.label}</span>
                  </>
                ) : (
                  <>
                    <span className="customer-app-bottom-nav-icon">{tab.icon}</span>
                    <span className="customer-app-bottom-nav-label">{tab.label}</span>
                  </>
                )}
              </NavLink>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
