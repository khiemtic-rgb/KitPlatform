import { useMemo, useState } from 'react';

import { useTranslation } from 'react-i18next';

import { Layout, Menu, Dropdown, Avatar, Space, Typography, Tabs } from 'antd';

import {

  MenuFoldOutlined,

  MenuUnfoldOutlined,

  UserOutlined,

  LogoutOutlined,

} from '@ant-design/icons';

import { Outlet, useLocation, useNavigate } from 'react-router-dom';

import { moduleRegistry } from '@/modules/registry';

import type { ModuleKey } from '@/modules/registry';

import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import {
  ModuleSubnavProvider,
  useModuleSubnavState,
} from '@/shared/components/module-subnav.context';

import { useAuthStore } from '@/shared/auth/auth.store';

import { logoutApi } from '@/shared/api/auth.api';

import { AdminLanguageSelect } from '@/shared/i18n/LanguageSelect';



const { Header, Sider, Content } = Layout;



function resolveActiveModuleKey(pathname: string): ModuleKey {

  if (pathname === '/') return 'dashboard';

  for (const module of moduleRegistry) {

    if (!module.enabled || module.key === 'dashboard') continue;

    const base = `/${module.path.split('/').filter(Boolean)[0]}`;

    if (pathname.startsWith(base)) return module.key;

  }

  return 'dashboard';

}



function AppLayoutShell() {

  const { t } = useTranslation('common');

  const [collapsed, setCollapsed] = useState(false);

  const navigate = useNavigate();

  const location = useLocation();
  const user = useAuthStore((s) => s.user);

  const refreshToken = useAuthStore((s) => s.refreshToken);

  const clearSession = useAuthStore((s) => s.clearSession);

  const subnav = useModuleSubnavState();



  const activeKey = resolveActiveModuleKey(location.pathname);

  const activeModuleLabel = t(`modules.${activeKey}`);



  const menuItems = useMemo(

    () =>

      moduleRegistry.map((module) => ({

        key: module.key,

        icon: module.icon,

        label: module.enabled

          ? t(`modules.${module.key}`)

          : t('modules.comingSoon', { name: t(`modules.${module.key}`) }),

        disabled: !module.enabled,

      })),

    [t],

  );



  const handleLogout = async () => {

    try {

      if (refreshToken) {

        await logoutApi(refreshToken);

      }

    } catch {

      // ignore — still clear local session

    } finally {

      clearSession();

      navigate('/login', { replace: true });

    }

  };



  const userMenu = {

    items: [

      {

        key: 'logout',

        icon: <LogoutOutlined />,

        label: t('appLayout.logout'),

        onClick: handleLogout,

      },

    ],

  };



  return (

    <Layout style={{ minHeight: '100vh' }}>

      <Sider

        collapsible

        collapsed={collapsed}

        onCollapse={setCollapsed}

        breakpoint="lg"

        collapsedWidth={64}

        theme="dark"

        width={240}

      >

        <div

          style={{

            height: 64,

            margin: 16,

            display: 'flex',

            alignItems: 'center',

            justifyContent: collapsed ? 'center' : 'flex-start',

            color: '#fff',

            fontWeight: 700,

            fontSize: collapsed ? 14 : 18,

            letterSpacing: 0.5,

          }}

        >

          {collapsed ? 'PC' : 'PharmaCore'}

        </div>

        <Menu

          theme="dark"

          mode="inline"

          selectedKeys={[activeKey]}

          items={menuItems}

          onClick={({ key }) => {

            const module = moduleRegistry.find((m) => m.key === key);

            if (module?.enabled) {

              navigate(module.path);

            }

          }}

        />

      </Sider>

      <Layout>

        <Header className="app-header">

          <div className="app-header__left">

            <span

              className="app-header__toggle"

              onClick={() => setCollapsed((c) => !c)}

              role="button"

              tabIndex={0}

              onKeyDown={(e) => e.key === 'Enter' && setCollapsed((c) => !c)}

            >

              {collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}

            </span>

            <Typography.Title level={5} className="app-header__module-title">

              {activeModuleLabel}

            </Typography.Title>

            {subnav ? (

              <>

                <span className="app-header__module-sep" aria-hidden>

                  |

                </span>

                <Tabs

                  className="app-header-module-tabs"

                  activeKey={subnav.activeKey}

                  size="small"

                  items={subnav.tabs}

                  onChange={subnav.onChange}

                />

              </>

            ) : null}

          </div>

          <Space size={12} style={{ flexShrink: 0, marginLeft: 12 }}>
            <AdminLanguageSelect />
            <Dropdown menu={userMenu} placement="bottomRight">
              <Space style={{ cursor: 'pointer' }}>
                <Avatar size="small" icon={<UserOutlined />} />
                <Typography.Text>{user?.username ?? 'Admin'}</Typography.Text>
              </Space>
            </Dropdown>
          </Space>

        </Header>

        <ApiHealthBanner />

        <Content style={{ margin: 24 }}>

          <Outlet />

        </Content>

      </Layout>

    </Layout>

  );

}



export function AppLayout() {

  return (

    <ModuleSubnavProvider>

      <AppLayoutShell />

    </ModuleSubnavProvider>

  );

}


