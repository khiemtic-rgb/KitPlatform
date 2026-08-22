import { useEffect, useMemo, useState } from 'react';

import { useTranslation } from 'react-i18next';

import { Layout, Menu, Dropdown, Avatar, Space, Typography, Tabs } from 'antd';

import {

  MenuFoldOutlined,

  MenuUnfoldOutlined,

  UserOutlined,

  LogoutOutlined,

} from '@ant-design/icons';

import { Outlet, useLocation, useNavigate } from 'react-router-dom';

import {
  isModuleVisibleForVertical,
  moduleRegistry,
  resolveAdminVertical,
  TEMP_HIDDEN_MODULE_KEYS,
} from '@/modules/registry';

import type { ModuleKey } from '@/modules/registry';

import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';

import { ApiHealthBanner } from '@/shared/components/ApiHealthBanner';
import {
  ModuleSubnavProvider,
  useModuleSubnavState,
} from '@/shared/components/module-subnav.context';

import { useAuthStore } from '@/shared/auth/auth.store';
import {
  useCanAccessOwnerCockpit,
  useCanAccessSuccessModule,
  useCanCatalogRead,
  useCanClinicRead,
  useCanConnectRead,
  useCanCustomerModule,
  useCanInventoryRead,
  useCanLearningRead,
  useCanProcurementRead,
  useCanReceivables,
  useCanReportsRead,
  useCanRxRead,
  useCanSalesRead,
  useCanSystemRead,
} from '@/shared/auth/usePermission';
import { useKapAdminAccess } from '@/shared/hooks/useKapAdminAccess';

import { logoutApi } from '@/shared/api/auth.api';

import { AdminLanguageSelect } from '@/shared/i18n/LanguageSelect';
import { AppBrandLogo } from '@/shared/components/AppBrandLogo';
import { resolveShellBrand } from '@/shared/config/app-brand';
import {
  CONTENT_NAV_ITEMS,
  CONTENT_NAV_SETUP,
  CONTENT_NAV_WORK,
  resolveContentNavKey,
  resolveContentNavLabel,
} from '@/modules/content/content-nav';
import { LOCAL_OS_NAV, resolveLocalOsNavKey } from '@/modules/local-os/local-os-nav';

const { Header, Sider, Content } = Layout;

function resolveActiveModuleKey(pathname: string): string {
  const contentKey = resolveContentNavKey(pathname);
  if (contentKey) return contentKey;
  const localKey = resolveLocalOsNavKey(pathname);
  if (localKey) return localKey;

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
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const platformVertical = useTenantPlatformStore((s) => s.settings?.vertical);
  const { enabled: kapEnabled, checked: kapAccessChecked } = useKapAdminAccess();
  const adminVertical = resolveAdminVertical(platformVertical);
  const shellBrand = resolveShellBrand(platformVertical);

  const canAccessSales = useCanSalesRead();
  const canAccessProcurement = useCanProcurementRead();
  const canAccessInventory = useCanInventoryRead();
  const canAccessCatalog = useCanCatalogRead();
  const canAccessReports = useCanReportsRead();
  const canAccessClinic = useCanClinicRead();
  const canAccessSystem = useCanSystemRead();
  const canAccessSuccess = useCanAccessSuccessModule();
  const canAccessOwnerCockpit = useCanAccessOwnerCockpit();
  const canAccessRx = useCanRxRead();
  const canAccessConnect = useCanConnectRead();
  const canAccessCustomer = useCanCustomerModule();
  const canAccessReceivables = useCanReceivables();
  const canAccessLearning = useCanLearningRead();
  const auditSlimNav = useAuditSlimNav();

  const modulePermissionOk = useMemo(
    (): Partial<Record<ModuleKey, boolean>> => ({
      dashboard: true,
      success: canAccessSuccess && !auditSlimNav,
      sales: canAccessSales,
      rx: canAccessRx,
      connect: canAccessConnect,
      clinic: canAccessClinic,
      familyOs: true,
      localOs: true,
      procurement: canAccessProcurement,
      inventory: canAccessInventory,
      receivables: canAccessReceivables,
      customer: canAccessCustomer,
      catalog: canAccessCatalog,
      reports: canAccessReports,
      learning: canAccessLearning && !auditSlimNav,
      kap: kapAccessChecked && kapEnabled,
      system: canAccessSystem,
    }),
    [
      canAccessSuccess,
      auditSlimNav,
      canAccessSales,
      canAccessRx,
      canAccessConnect,
      canAccessClinic,
      canAccessProcurement,
      canAccessInventory,
      canAccessReceivables,
      canAccessCustomer,
      canAccessCatalog,
      canAccessReports,
      canAccessLearning,
      kapAccessChecked,
      kapEnabled,
      canAccessSystem,
    ],
  );

  useEffect(() => {
    document.title =
      shellBrand.isFamily || shellBrand.isMarketing || shellBrand.isLocal
        ? `${shellBrand.brand} Admin`
        : 'Novixa Admin';
  }, [shellBrand.brand, shellBrand.isFamily, shellBrand.isMarketing, shellBrand.isLocal]);

  useEffect(() => {
    if (
      auditSlimNav &&
      (location.pathname.startsWith('/success') ||
        location.pathname.startsWith('/people') ||
        location.pathname.startsWith('/learning'))
    ) {
      navigate('/', { replace: true });
    }
  }, [auditSlimNav, location.pathname, navigate]);

  const activeKey = resolveActiveModuleKey(location.pathname);

  const activeModuleLabel = resolveContentNavLabel(location.pathname) ?? t(`modules.${activeKey}`);

  const menuItems = useMemo(
    () =>
      moduleRegistry
        .filter((module) => !TEMP_HIDDEN_MODULE_KEYS.includes(module.key))
        .filter((module) => {
          // Trước khi biết vertical tenant: chỉ hiện module dùng chung (dashboard, connect, system…).
          // Tránh nháy menu Nhà thuốc (Bán hàng/Kho…) trên Clinic khi còn mặc định pharmacy.
          if (!platformLoaded) {
            const scopes = module.verticals;
            if (!scopes || scopes.length === 0) return true;
            return scopes.includes('pharmacy') && scopes.includes('clinic');
          }
          return isModuleVisibleForVertical(module, adminVertical);
        })
        .flatMap((module) => {
          const platformOk =
            !module.platformModule || !platformLoaded || isModuleEnabled(module.platformModule);
          const kapOk = module.key !== 'kap' || (kapAccessChecked && kapEnabled);
          const permissionOk = modulePermissionOk[module.key] ?? true;
          const navEnabled = module.enabled && platformOk && kapOk && permissionOk;
          // Ẩn hẳn module tắt / không thuộc vertical — không hiện "(sắp có)".
          if (!navEnabled) return [];
          if (adminVertical === 'marketing' && module.key === 'dashboard') return [];
          if (adminVertical === 'marketing' && module.key === 'content') {
            return [
              ...CONTENT_NAV_WORK.map((i) => ({ key: i.key, icon: i.icon, label: i.label })),
              { type: 'divider' as const },
              ...CONTENT_NAV_SETUP.map((i) => ({ key: i.key, icon: i.icon, label: i.label })),
            ];
          }
          if (adminVertical === 'marketing' && module.key === 'localOs') {
            return [
              { type: 'divider' as const },
              ...LOCAL_OS_NAV.map((i) => ({ key: i.key, icon: i.icon, label: i.label })),
            ];
          }
          return [
            {
              key: module.key,
              icon: module.icon,
              label: t(`modules.${module.key}`),
            },
          ];
        }),
    [
      t,
      adminVertical,
      isModuleEnabled,
      platformLoaded,
      kapAccessChecked,
      kapEnabled,
      modulePermissionOk,
    ],
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



  if (location.pathname.startsWith('/local-os/duyet')) {
    return (
      <Layout className="los-phone-shell" style={{ minHeight: '100vh' }}>
        <Header className="app-header" style={{ padding: '0 16px' }}>
          <Typography.Title level={5} style={{ margin: 0 }}>
            Duyệt tin
          </Typography.Title>
          <Dropdown menu={userMenu} placement="bottomRight">
            <Space style={{ cursor: 'pointer' }}>
              <Avatar size="small" icon={<UserOutlined />} />
              <Typography.Text>{user?.username ?? 'Admin'}</Typography.Text>
            </Space>
          </Dropdown>
        </Header>
        <Content className="app-main-content">
          <Outlet />
        </Content>
      </Layout>
    );
  }

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

        style={{ background: '#1b3a6b' }}

      >

        <div
          style={{
            height: 64,
            margin: collapsed ? '16px 8px' : 16,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexDirection: 'column',
            gap: 2,
          }}
        >
          {shellBrand.isFamily || shellBrand.isMarketing || shellBrand.isLocal ? (
            <>
              <Typography.Text
                strong
                style={{ color: '#fff', fontSize: collapsed ? 12 : 18, lineHeight: 1.1 }}
              >
                {collapsed
                  ? shellBrand.isMarketing
                    ? 'MK'
                    : shellBrand.isLocal
                      ? 'TN'
                      : 'FO'
                  : shellBrand.brand}
              </Typography.Text>
              {!collapsed ? (
                <Typography.Text style={{ color: 'rgba(255,255,255,0.65)', fontSize: 11 }}>
                  {shellBrand.isMarketing ? 'KIT_MKT' : shellBrand.isLocal ? 'KIT_LOCAL' : 'Starter'}
                </Typography.Text>
              ) : null}
            </>
          ) : (
            <AppBrandLogo height={collapsed ? 36 : 48} maxWidth={collapsed ? 40 : 168} />
          )}
        </div>

        <Menu

          theme="dark"

          mode="inline"

          selectedKeys={[activeKey]}

          items={menuItems}

          onClick={({ key }) => {
            const contentItem = CONTENT_NAV_ITEMS.find((i) => i.key === key);
            if (contentItem) {
              navigate(contentItem.path);
              return;
            }
            const localItem = LOCAL_OS_NAV.find((i) => i.key === key);
            if (localItem) {
              navigate(localItem.path);
              return;
            }
            const module = moduleRegistry.find((m) => m.key === key);
            if (!module || !isModuleVisibleForVertical(module, adminVertical)) return;
            const platformOk =
              !module.platformModule || !platformLoaded || isModuleEnabled(module.platformModule);
            const kapOk = module.key !== 'kap' || (kapAccessChecked && kapEnabled);
            const permissionOk = modulePermissionOk[module.key] ?? true;
            if (!(module.enabled && platformOk && kapOk && permissionOk)) return;
            if (module.key === 'success') {
              navigate(
                canAccessOwnerCockpit
                  ? '/success/cockpit'
                  : '/success/shift-checklist',
              );
              return;
            }
            navigate(module.path);
          }}

        />

      </Sider>

      <Layout style={{ minWidth: 0, overflow: 'hidden' }}>

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

        <Content className="app-main-content">

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


