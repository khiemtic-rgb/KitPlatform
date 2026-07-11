import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  DashboardOutlined,
  LinkOutlined,
  MedicineBoxOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useRegisterProductNavSubnav } from '@/shared/components/module-subnav.context';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

export function RxLayout() {
  const { t } = useTranslation('rx', { keyPrefix: 'rxLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const allTabs: ProductNavTab[] = useMemo(
    () => [
      {
        key: 'overview',
        label: t('overview'),
        path: '/rx/overview',
        icon: <DashboardOutlined />,
      },
      {
        key: 'prescriptions',
        label: t('prescriptions'),
        path: '/rx/prescriptions',
        icon: <MedicineBoxOutlined />,
      },
      {
        key: 'prescribers',
        label: t('prescribers'),
        path: '/rx/prescribers',
        icon: <UserOutlined />,
      },
      {
        key: 'prescriber-links',
        label: t('prescriberLinks'),
        path: '/rx/prescriber-links',
        icon: <LinkOutlined />,
      },
    ],
    [t],
  );

  const tabs = useProductNavGuard(allTabs, '/rx/overview');

  useEffect(() => {
    if (location.pathname === '/rx' || location.pathname === '/rx/') {
      navigate('/rx/overview', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'overview';

  useRegisterProductNavSubnav(tabs, activeKey, (tab) => navigate(tab.path));

  return <Outlet />;
}
