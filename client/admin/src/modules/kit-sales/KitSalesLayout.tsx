import { useEffect, useMemo } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  BarChartOutlined,
  FundOutlined,
  RocketOutlined,
  SettingOutlined,
  SolutionOutlined,
  ThunderboltOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';
import { useAuthStore } from '@/shared/auth/auth.store';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import { ADMIN_MODULE_PLATFORM_CODES } from '@/shared/platform/platform-feature-map';

export function KitSalesLayout() {
  const { t } = useTranslation('kitSales');
  const location = useLocation();
  const navigate = useNavigate();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const isAdmin = roles.includes('ADMIN');
  const platformLoaded = useTenantPlatformStore((s) => s.loaded);
  const isModuleEnabled = useTenantPlatformStore((s) => s.isModuleEnabled);
  const moduleCode = ADMIN_MODULE_PLATFORM_CODES.kitSales ?? 'kit_sales';
  const moduleOk = !platformLoaded || isModuleEnabled(moduleCode);

  const tabs = useMemo(
    () => [
      { key: 'workspace', label: t('nav.workspace'), path: '/kit-sales', icon: <ThunderboltOutlined /> },
      { key: 'leads', label: t('nav.leads'), path: '/kit-sales/leads', icon: <UnorderedListOutlined /> },
      { key: 'pipeline', label: t('nav.pipeline'), path: '/kit-sales/pipeline', icon: <FundOutlined /> },
      { key: 'campaigns', label: t('nav.campaigns'), path: '/kit-sales/campaigns', icon: <RocketOutlined /> },
      {
        key: 'intelligence',
        label: t('nav.intelligence'),
        path: '/kit-sales/intelligence',
        icon: <SolutionOutlined />,
      },
      { key: 'analytics', label: t('nav.analytics'), path: '/kit-sales/analytics', icon: <BarChartOutlined /> },
      { key: 'settings', label: t('nav.settings'), path: '/kit-sales/settings', icon: <SettingOutlined /> },
    ],
    [t],
  );

  useEffect(() => {
    if (!isAdmin) {
      navigate('/', { replace: true });
      return;
    }
    if (platformLoaded && !moduleOk) {
      if (isModuleEnabled('kit_content')) navigate('/content/ops', { replace: true });
      else navigate('/', { replace: true });
    }
  }, [isAdmin, isModuleEnabled, moduleOk, navigate, platformLoaded]);

  const activeKey =
    tabs
      .filter((tab) => tab.key !== 'workspace')
      .find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'workspace';
  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  if (!isAdmin || (platformLoaded && !moduleOk)) return null;

  return <Outlet />;
}
