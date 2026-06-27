import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import {
  BarChartOutlined,
  InboxOutlined,
  ShoppingOutlined,
  ShopOutlined,
} from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';
import { ReportCategoryNav } from '@/modules/reports/ReportCategoryNav';

const allTabs: ProductNavTab[] = [
  {
    key: 'home',
    label: 'Trung tâm báo cáo',
    path: '/reports',
    icon: <BarChartOutlined />,
  },
  {
    key: 'sales',
    label: 'Bán hàng',
    path: '/reports/sales/revenue-by-period',
    icon: <ShopOutlined />,
  },
  {
    key: 'procurement',
    label: 'Mua hàng',
    path: '/reports/procurement/grn-value',
    icon: <ShoppingOutlined />,
  },
  {
    key: 'inventory',
    label: 'Kho hàng',
    path: '/reports/inventory/stock-snapshot',
    icon: <InboxOutlined />,
  },
];

export function ReportsLayout() {
  const location = useLocation();
  const navigate = useNavigate();
  useProductNavGuard(allTabs, '/reports');

  useEffect(() => {
    if (location.pathname === '/reports/') {
      navigate('/reports', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    location.pathname.startsWith('/reports/sales')
      ? 'sales'
      : location.pathname.startsWith('/reports/procurement')
        ? 'procurement'
        : location.pathname.startsWith('/reports/inventory')
          ? 'inventory'
          : 'home';

  const visibleTabs = allTabs;

  return (
    <div>
      <div style={moduleTabsShellStyle}>
        <div style={secondaryTabsBarStyle}>
          <Tabs
            activeKey={activeKey}
            size="small"
            items={visibleTabs.map((t) => ({
              key: t.key,
              label: secondaryTabLabel(t.label, t.icon),
            }))}
            onChange={(key) => {
              const tab = visibleTabs.find((t) => t.key === key);
              if (tab) navigate(tab.path);
            }}
          />
        </div>
        <ReportCategoryNav />
      </div>
      <Outlet />
    </div>
  );
}
