import { NavLink, Outlet, useLocation } from 'react-router-dom';
import {
  HeartOutlined,
  HomeOutlined,
  MedicineBoxOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import { BrandingLogo } from '@/shared/components/BrandingLogo';
import { useCustomerBranding } from '@/shared/config/BrandingProvider';
import { prefetchOverviewForPath } from '@/shared/api/overview-queries';
import { preloadRouteChunk } from '@/shared/routing/route-preload';

export function CustomerAppLayout() {
  const { t } = useTranslation();
  const location = useLocation();
  const queryClient = useQueryClient();
  const { branding } = useCustomerBranding();
  const isChat = location.pathname.startsWith('/chat');
  const isOrders = location.pathname.startsWith('/orders');
  const isHealth = location.pathname.startsWith('/health');
  const isLoyalty = location.pathname.startsWith('/loyalty');
  const isProfile = location.pathname.startsWith('/profile');
  const isReminders = location.pathname.startsWith('/reminders');
  const isFamily = location.pathname.startsWith('/family');
  const isAi = location.pathname.startsWith('/ai');
  const isMedications = location.pathname.startsWith('/medications');
  const isHome = location.pathname === '/';

  /** Consumer-first tabs: hồ sơ · thuốc · gia đình — không dẫn bằng đặt thuốc/pharmacy. */
  const tabs = [
    { to: '/', icon: <HomeOutlined />, label: t('nav.home'), kind: 'link' as const },
    { to: '/health', icon: <HeartOutlined />, label: t('nav.health'), kind: 'link' as const },
    { to: '/medications', icon: <MedicineBoxOutlined />, label: t('nav.medications'), kind: 'fab' as const },
    { to: '/family', icon: <TeamOutlined />, label: t('nav.family'), kind: 'link' as const },
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
    !isAi &&
    !isMedications;

  return (
    <div
      className={`customer-app-shell${isChat ? ' customer-app-shell--chat' : ''}${
        isHome ? ' customer-app-shell--home' : ''
      }${isOrders ? ' customer-app-shell--orders' : ''}${isHealth ? ' customer-app-shell--health' : ''}${
        isLoyalty ? ' customer-app-shell--loyalty' : ''
      }${isProfile ? ' customer-app-shell--profile' : ''}${isReminders ? ' customer-app-shell--reminders' : ''}${
        isFamily ? ' customer-app-shell--family' : ''
      }${isAi ? ' customer-app-shell--ai' : ''}${isMedications ? ' customer-app-shell--medications' : ''}`}
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
        !isAi &&
        !isMedications ? (
          <div className="customer-app-banner-wrap">
            <ApiHealthBanner />
          </div>
        ) : null}
        <Outlet />
      </main>

      <nav className="customer-app-bottom-nav" aria-label={t('nav.main')}>
        <div className="customer-app-bottom-nav-inner">
          {tabs.map((tab) => (
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
                  <span className="customer-app-bottom-nav-fab">{tab.icon}</span>
                  <span className="customer-app-bottom-nav-label">{tab.label}</span>
                </>
              ) : (
                <>
                  <span className="customer-app-bottom-nav-icon">{tab.icon}</span>
                  <span className="customer-app-bottom-nav-label">{tab.label}</span>
                </>
              )}
            </NavLink>
          ))}
        </div>
      </nav>
    </div>
  );
}
