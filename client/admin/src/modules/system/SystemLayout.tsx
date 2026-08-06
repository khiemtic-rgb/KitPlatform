import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  BankOutlined,
  CloudOutlined,
  CreditCardOutlined,
  FileSearchOutlined,
  GlobalOutlined,
  MobileOutlined,
  PrinterOutlined,
  SafetyCertificateOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { resolveAdminVertical } from '@/modules/registry';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';

export function SystemLayout() {
  const { t } = useTranslation('system', { keyPrefix: 'systemLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();
  const platformVertical = useTenantPlatformStore((s) => s.settings?.vertical);
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const auditSlimNav = useAuditSlimNav();
  const adminVertical = resolveAdminVertical(platformVertical);

  const tabs = useMemo(() => {
    const branchLabel = adminVertical === 'family' ? 'Gia đình' : t('branches');
    const packLabel = adminVertical === 'family' ? 'Gói FamilyOS' : t('platformPack');
    const customerAppOk = !platformLoaded || isModuleEnabled('customer_app');
    const all = [
      { key: 'branches', label: branchLabel, path: '/system/branches', icon: <BankOutlined /> },
      { key: 'users', label: t('users'), path: '/system/users', icon: <UserOutlined /> },
      { key: 'roles', label: t('roles'), path: '/system/roles', icon: <SafetyCertificateOutlined /> },
      {
        key: 'platform-pack',
        label: packLabel,
        path: '/system/platform-pack',
        icon: <CloudOutlined />,
        hideWhenAuditSlim: true as const,
      },
      {
        key: 'billing',
        label: t('billing'),
        path: '/system/billing',
        icon: <CreditCardOutlined />,
        familyOnly: true as const,
      },
      {
        key: 'trial-signups',
        label: 'Đăng ký dùng thử',
        path: '/system/trial-signups',
        icon: <UserAddOutlined />,
        familyOnly: true as const,
      },
      {
        key: 'pos-settings',
        label: t('posSettings'),
        path: '/system/pos-settings',
        icon: <PrinterOutlined />,
        pharmacyOnly: true as const,
      },
      {
        key: 'customer-app-settings',
        label: t('customerAppSettings'),
        path: '/system/customer-app-settings',
        icon: <MobileOutlined />,
        pharmacyOnly: true as const,
        requiresCustomerApp: true as const,
      },
      {
        key: 'storefront-settings',
        label: t('storefrontSettings'),
        path: '/system/storefront-settings',
        icon: <GlobalOutlined />,
        pharmacyOnly: true as const,
      },
      { key: 'audit-log', label: t('auditLog'), path: '/system/audit-log', icon: <FileSearchOutlined /> },
    ];

    return all
      .filter((tab) => {
        if ('pharmacyOnly' in tab && tab.pharmacyOnly && adminVertical !== 'pharmacy') return false;
        if ('familyOnly' in tab && tab.familyOnly && adminVertical !== 'family') return false;
        if ('hideWhenAuditSlim' in tab && tab.hideWhenAuditSlim && auditSlimNav) return false;
        if ('requiresCustomerApp' in tab && tab.requiresCustomerApp && !customerAppOk) return false;
        return true;
      })
      .map((tab) => {
        const {
          pharmacyOnly: _p,
          familyOnly: _f,
          hideWhenAuditSlim: _h,
          requiresCustomerApp: _c,
          ...rest
        } = tab as typeof tab & {
          pharmacyOnly?: boolean;
          familyOnly?: boolean;
          hideWhenAuditSlim?: boolean;
          requiresCustomerApp?: boolean;
        };
        return rest;
      });
  }, [t, adminVertical, auditSlimNav, isModuleEnabled, platformLoaded]);

  useEffect(() => {
    if (location.pathname === '/system' || location.pathname === '/system/') {
      navigate('/system/branches', { replace: true });
      return;
    }

    const onPharmacyOnlyTab =
      location.pathname.startsWith('/system/pos-settings') ||
      location.pathname.startsWith('/system/customer-app-settings') ||
      location.pathname.startsWith('/system/storefront-settings');
    if (adminVertical !== 'pharmacy' && onPharmacyOnlyTab) {
      navigate('/system/platform-pack', { replace: true });
      return;
    }

    if (adminVertical !== 'family' && location.pathname.startsWith('/system/billing')) {
      navigate('/system/branches', { replace: true });
      return;
    }

    if (auditSlimNav && location.pathname.startsWith('/system/platform-pack')) {
      navigate('/system/branches', { replace: true });
      return;
    }

    const customerAppOk = !platformLoaded || isModuleEnabled('customer_app');
    if (!customerAppOk && location.pathname.startsWith('/system/customer-app-settings')) {
      navigate('/system/branches', { replace: true });
    }
  }, [location.pathname, navigate, adminVertical, auditSlimNav, isModuleEnabled, platformLoaded]);

  const activeKey =
    tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'branches';

  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  return <Outlet />;
}
