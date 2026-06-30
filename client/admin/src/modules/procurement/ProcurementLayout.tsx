import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import {
  AccountBookOutlined,
  ContainerOutlined,
  CreditCardOutlined,
  FileTextOutlined,
  PercentageOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

export function ProcurementLayout() {
  const { t } = useTranslation('procurement', { keyPrefix: 'procurementLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const allTabs: ProductNavTab[] = useMemo(
    () => [
      { key: 'suppliers', label: t('suppliers'), path: '/procurement/suppliers', icon: <TeamOutlined /> },
      {
        key: 'orders',
        label: t('orders'),
        path: '/procurement/purchase-orders',
        icon: <FileTextOutlined />,
      },
      {
        key: 'receipts',
        label: t('receipts'),
        path: '/procurement/goods-receipts',
        icon: <ContainerOutlined />,
      },
      {
        key: 'vat-settings',
        label: t('vatSettings'),
        path: '/procurement/vat-treatments',
        icon: <PercentageOutlined />,
        feature: 'procurement.vatAdmin',
      },
      {
        key: 'payables',
        label: t('payables'),
        path: '/procurement/supplier-payables',
        icon: <AccountBookOutlined />,
        feature: 'procurement.payables',
      },
      {
        key: 'payments',
        label: t('payments'),
        path: '/procurement/supplier-payments',
        icon: <CreditCardOutlined />,
        feature: 'procurement.payments',
      },
    ],
    [t],
  );

  const tabs = useProductNavGuard(allTabs, '/procurement/suppliers');

  useEffect(() => {
    if (location.pathname === '/procurement' || location.pathname === '/procurement/') {
      navigate('/procurement/suppliers', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'suppliers';

  return (
    <div>
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
      <Outlet />
    </div>
  );
}
