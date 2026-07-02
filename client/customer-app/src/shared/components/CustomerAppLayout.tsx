import { NavLink, Outlet, useLocation } from 'react-router-dom';

import { Badge } from 'antd';

import {

  HomeOutlined,

  MedicineBoxOutlined,

  MessageOutlined,

  ShoppingOutlined,

  UserOutlined,

} from '@ant-design/icons';

import { useTranslation } from 'react-i18next';

import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';

import { BrandingLogo } from '@/shared/components/BrandingLogo';

import { useCustomerBranding } from '@/shared/config/BrandingProvider';

import { useCustomerChatUnread } from '@/shared/hooks/useCustomerChatUnread';

import { useCustomerDraftOrderAlerts } from '@/shared/hooks/useCustomerDraftOrderAlerts';



export function CustomerAppLayout() {

  const { t } = useTranslation();

  const location = useLocation();

  const { branding } = useCustomerBranding();

  const chatUnread = useCustomerChatUnread();

  const draftOrderAlerts = useCustomerDraftOrderAlerts();



  const tabs = [

    { to: '/', icon: <HomeOutlined />, label: t('nav.home') },

    { to: '/orders', icon: <ShoppingOutlined />, label: t('nav.orders') },

    { to: '/reminders', icon: <MedicineBoxOutlined />, label: t('nav.reminders') },

    { to: '/chat', icon: <MessageOutlined />, label: t('nav.chat') },

    { to: '/profile', icon: <UserOutlined />, label: t('nav.account') },

  ] as const;



  const headerGradient = `linear-gradient(135deg, ${branding.primaryColor}, ${branding.secondaryColor})`;



  return (

    <div style={{ minHeight: '100vh', background: '#f0fdfa' }}>

      <header

        style={{

          position: 'sticky',

          top: 0,

          zIndex: 10,

          background: headerGradient,

          color: '#fff',

          padding: '14px 16px',

          boxShadow: '0 2px 8px rgba(15,118,110,0.25)',

        }}

      >

        <div style={{ maxWidth: 480, margin: '0 auto' }}>

          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>

            <BrandingLogo logoUrl={branding.logoUrl} />

            <div style={{ minWidth: 0 }}>

              <div style={{ fontWeight: 600, fontSize: 17 }}>{branding.appName}</div>

              {branding.tagline ? (

                <div style={{ fontSize: 12, opacity: 0.9, marginTop: 2 }}>{branding.tagline}</div>

              ) : null}

            </div>

          </div>

        </div>

      </header>



      <main

        className={

          location.pathname.startsWith('/chat')

            ? 'customer-app-content customer-app-content--chat'

            : 'customer-app-content'

        }

      >

        {!location.pathname.startsWith('/chat') ? (

          <div style={{ maxWidth: 480, margin: '0 auto', padding: '0 12px' }}>

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

