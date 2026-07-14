import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  GiftOutlined,
  LineChartOutlined,
  TeamOutlined,
  TagOutlined,
  UnorderedListOutlined,
} from '@ant-design/icons';
import { useRegisterProductNavSubnav } from '@/shared/components/module-subnav.context';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

export function CustomerLayout() {
  const { t } = useTranslation('customer', { keyPrefix: 'customerLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const allTabs: ProductNavTab[] = useMemo(
    () => [
      { key: 'list', label: t('list'), path: '/customer/list', icon: <UnorderedListOutlined /> },
      { key: 'groups', label: t('groups'), path: '/customer/groups', icon: <TeamOutlined /> },
      {
        key: 'engagement',
        label: t('engagement'),
        path: '/customer/engagement',
        icon: <LineChartOutlined />,
        feature: 'customer.engagement',
      },
      { key: 'loyalty', label: t('loyalty'), path: '/customer/loyalty', icon: <GiftOutlined /> },
      {
        key: 'vouchers',
        label: t('vouchers'),
        path: '/customer/vouchers',
        icon: <TagOutlined />,
        feature: 'sales.vouchers',
      },
    ],
    [t],
  );

  const tabs = useProductNavGuard(allTabs, '/customer/list');

  useEffect(() => {
    if (location.pathname === '/customer' || location.pathname === '/customer/') {
      navigate('/customer/list', { replace: true });
    }
  }, [location.pathname, navigate]);

  const onDetailRoute =
    /^\/customer\/[^/]+/.test(location.pathname) &&
    !tabs.some((tab) => location.pathname.startsWith(tab.path));

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'list';

  useRegisterProductNavSubnav(onDetailRoute ? null : tabs, activeKey, (tab) => navigate(tab.path));

  return <Outlet />;
}
