import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import { GiftOutlined, TagOutlined, UnorderedListOutlined } from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

export function CustomerLayout() {
  const { t } = useTranslation('customer', { keyPrefix: 'customerLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const allTabs: ProductNavTab[] = useMemo(
    () => [
      { key: 'list', label: t('list'), path: '/customer/list', icon: <UnorderedListOutlined /> },
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

  return (
    <div>
      {!onDetailRoute ? (
        <div style={moduleTabsShellStyle}>
          <div style={secondaryTabsBarStyle}>
            <Tabs
              activeKey={activeKey}
              size="small"
              items={tabs.map((tab) => ({
                key: tab.key,
                label: secondaryTabLabel(tab.label, tab.icon),
              }))}
              onChange={(key) => {
                const tab = tabs.find((item) => item.key === key);
                if (tab) navigate(tab.path);
              }}
            />
          </div>
        </div>
      ) : null}
      <Outlet />
    </div>
  );
}
