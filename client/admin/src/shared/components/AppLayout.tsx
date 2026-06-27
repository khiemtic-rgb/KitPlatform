import { useState, type CSSProperties } from 'react';
import { Layout, Menu, Dropdown, Avatar, Space, Typography, theme } from 'antd';
import {
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  UserOutlined,
  LogoutOutlined,
} from '@ant-design/icons';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { buildMenuItems, HEADER_MODULE_KEYS, moduleRegistry } from '@/modules/registry';
import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import { MODULE_PRIMARY_BG, primaryTabLabel } from '@/shared/components/module-tabs.ui';
import { useAuthStore } from '@/shared/auth/auth.store';
import { logoutApi } from '@/shared/api/auth.api';

const { Header, Sider, Content } = Layout;

const enabledModules = moduleRegistry.filter(
  (m) => m.enabled && HEADER_MODULE_KEYS.includes(m.key),
);

export function AppLayout() {
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
        label: 'Đăng xuất',
        onClick: handleLogout,
      },
    ],
  };

  const moduleNav = (
    <nav
      aria-label="Module chính"
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
            {primaryTabLabel(m.label)}
          </button>
        );
      })}
    </nav>
  );

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
          items={buildMenuItems()}
          onClick={({ key }) => {
            const module = moduleRegistry.find((m) => m.key === key);
            if (module?.enabled) {
              navigate(module.path);
            }
          }}
        />
      </Sider>
      <Layout>
        <Header
          style={{
            padding: '0 16px',
            background: MODULE_PRIMARY_BG,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            height: 'auto',
            lineHeight: 'normal',
          }}
        >
          <Space align="center" style={{ flex: 1, minWidth: 0 }}>
            <span
              style={{ cursor: 'pointer', fontSize: 18, flexShrink: 0 }}
              onClick={() => setCollapsed((c) => !c)}
              role="button"
              tabIndex={0}
              onKeyDown={(e) => e.key === 'Enter' && setCollapsed((c) => !c)}
            >
              {collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            </span>
            <Typography.Text type="secondary" style={{ flexShrink: 0 }}>
              ERP Nhà thuốc
            </Typography.Text>
            {moduleNav}
          </Space>
          <Dropdown menu={userMenu} placement="bottomRight">
            <Space style={{ cursor: 'pointer', flexShrink: 0, marginLeft: 12 }}>
              <Avatar size="small" icon={<UserOutlined />} />
              <Typography.Text>{user?.username ?? 'Admin'}</Typography.Text>
            </Space>
          </Dropdown>
        </Header>
        <ApiHealthBanner />
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
