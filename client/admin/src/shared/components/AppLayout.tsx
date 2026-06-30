import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import { Layout, Menu, Dropdown, Avatar, Space, Typography, Button, theme } from 'antd';
import {
  AppstoreOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
  LogoutOutlined,
} from '@ant-design/icons';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { HEADER_MODULE_KEYS, moduleRegistry } from '@/modules/registry';
import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import { MODULE_PRIMARY_BG, primaryTabLabel } from '@/shared/components/module-tabs.ui';
import { useAuthStore } from '@/shared/auth/auth.store';
import { logoutApi } from '@/shared/api/auth.api';

const { Header, Sider, Content } = Layout;

const enabledModules = moduleRegistry.filter(
  (m) => m.enabled && HEADER_MODULE_KEYS.includes(m.key),
);

export function AppLayout() {
  const { t } = useTranslation('common');
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { token } = theme.useToken();
  const user = useAuthStore((s) => s.user);
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const clearSession = useAuthStore((s) => s.clearSession);

  const activeKey =
    moduleRegistry.find((m) => {
      if (!m.enabled) return false;
      if (m.key === 'catalog') return location.pathname.startsWith('/catalog');
      if (m.path === '/') return location.pathname === '/';
      return location.pathname.startsWith(m.path);
    })?.key ?? 'dashboard';

  const isPosRoute = location.pathname.startsWith('/sales/pos');

  useEffect(() => {
    if (isPosRoute) {
      setCollapsed(true);
    }
  }, [isPosRoute]);

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

  const moduleNav = (
    <nav
      aria-label={t('appLayout.mainNavAria')}
      style={{
        display: 'flex',
        flex: 1,
        minWidth: 0,
        alignItems: 'stretch',
        overflowX: 'auto',
        overflowY: 'hidden',
        scrollbarWidth: 'thin',
      }}
    >
      {enabledModules.map((m) => {
        const selected = activeKey === m.key;
        const itemStyle: CSSProperties = {
          display: 'inline-flex',
          alignItems: 'center',
          gap: 8,
          padding: '0 14px',
          height: 46,
          border: 'none',
          background: 'transparent',
          cursor: 'pointer',
          whiteSpace: 'nowrap',
          flexShrink: 0,
          borderBottom: `2px solid ${selected ? token.colorPrimary : 'transparent'}`,
          color: selected ? token.colorPrimary : token.colorText,
        };
        return (
          <button
            key={m.key}
            type="button"
            style={itemStyle}
            onClick={() => navigate(m.path)}
          >
            {m.icon}
            {primaryTabLabel(t(`modules.${m.key}`))}
          </button>
        );
      })}
    </nav>
  );

  return (
    <Layout
      className={isPosRoute ? 'app-layout--pos' : undefined}
      style={{
        minHeight: '100vh',
        ...(isPosRoute ? { height: '100vh', overflow: 'hidden' } : {}),
      }}
    >
      <Sider
        collapsible={!isPosRoute}
        collapsed={isPosRoute || collapsed}
        onCollapse={isPosRoute ? undefined : setCollapsed}
        breakpoint="lg"
        collapsedWidth={isPosRoute ? 0 : 64}
        trigger={isPosRoute ? null : undefined}
        theme="dark"
        width={240}
        style={isPosRoute ? { overflow: 'hidden' } : undefined}
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
      <Layout className={isPosRoute ? 'app-layout__main--pos' : undefined}>
        <Header
          className={isPosRoute ? 'app-layout__header--pos' : undefined}
          style={{
            padding: isPosRoute ? '0 12px' : '0 16px',
            background: MODULE_PRIMARY_BG,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            height: isPosRoute ? 44 : 'auto',
            lineHeight: 'normal',
            flexShrink: 0,
          }}
        >
          <Space align="center" style={{ flex: 1, minWidth: 0 }}>
            {!isPosRoute ? (
              <span
                style={{ cursor: 'pointer', fontSize: 18, flexShrink: 0 }}
                onClick={() => setCollapsed((c) => !c)}
                role="button"
                tabIndex={0}
                onKeyDown={(e) => e.key === 'Enter' && setCollapsed((c) => !c)}
              >
                {collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
              </span>
            ) : (
              <Typography.Text strong style={{ flexShrink: 0 }}>
                POS
              </Typography.Text>
            )}
            <Typography.Text type="secondary" style={{ flexShrink: 0 }}>
              {t('appLayout.productName')}
            </Typography.Text>
            {!isPosRoute ? (
              moduleNav
            ) : (
              <Dropdown
                menu={{
                  items: enabledModules.map((m) => ({
                    key: m.key,
                    icon: m.icon,
                    label: t(`modules.${m.key}`),
                    onClick: () => navigate(m.path),
                  })),
                }}
              >
                <Button type="text" size="small" icon={<AppstoreOutlined />}>
                  {t('appLayout.switchModule')}
                </Button>
              </Dropdown>
            )}
          </Space>
          <Dropdown menu={userMenu} placement="bottomRight">
            <Space style={{ cursor: 'pointer', flexShrink: 0, marginLeft: 12 }}>
              <Avatar size="small" icon={<UserOutlined />} />
              <Typography.Text>{user?.username ?? 'Admin'}</Typography.Text>
            </Space>
          </Dropdown>
        </Header>
        <ApiHealthBanner />
        <Content
          className={isPosRoute ? 'app-content--pos' : undefined}
          style={{
            margin: isPosRoute ? 0 : 24,
            padding: isPosRoute ? 0 : undefined,
            background: isPosRoute ? '#fff' : undefined,
            minHeight: isPosRoute ? 0 : undefined,
            flex: isPosRoute ? 1 : undefined,
            display: isPosRoute ? 'flex' : undefined,
            flexDirection: isPosRoute ? 'column' : undefined,
            overflow: isPosRoute ? 'hidden' : undefined,
          }}
        >
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
