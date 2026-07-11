import { Suspense, useCallback, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Spin } from 'antd';
import {
  AccountBookOutlined,
  CreditCardOutlined,
  DollarOutlined,
  WalletOutlined,
} from '@ant-design/icons';
import { useRegisterProductNavSubnav } from '@/shared/components/module-subnav.context';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { filterProductNavTabs } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

const allMainTabDefs: Omit<ProductNavTab, 'label'>[] = [
  {
    key: 'customer-receivables',
    path: '/receivables/customers',
    icon: <DollarOutlined />,
    feature: 'sales.receivables',
  },
  {
    key: 'customer-payments',
    path: '/receivables/customer-payments',
    icon: <CreditCardOutlined />,
    feature: 'sales.customerPayments',
  },
  {
    key: 'supplier-payables',
    path: '/receivables/suppliers',
    icon: <AccountBookOutlined />,
    feature: 'procurement.payables',
  },
  {
    key: 'supplier-payments',
    path: '/receivables/supplier-payments',
    icon: <WalletOutlined />,
    feature: 'procurement.payments',
  },
];

const tabLabelKeys: Record<string, string> = {
  'customer-receivables': 'customerReceivables',
  'customer-payments': 'customerPayments',
  'supplier-payables': 'supplierPayables',
  'supplier-payments': 'supplierPayments',
};

export function ReceivablesLayout() {
  const { t } = useTranslation('receivables', { keyPrefix: 'receivablesLayout' });
  const location = useLocation();
  const navigate = useNavigate();

  const allMainTabs = useMemo<ProductNavTab[]>(
    () =>
      allMainTabDefs.map((tab) => ({
        ...tab,
        label: t(`tabs.${tabLabelKeys[tab.key] ?? tab.key}`),
      })),
    [t],
  );

  const mainTabs = useMemo(() => filterProductNavTabs(allMainTabs), [allMainTabs]);

  useProductNavGuard(allMainTabs, mainTabs[0]?.path ?? '/receivables/customers');

  const activeMainTab =
    mainTabs.find((tab) => location.pathname.startsWith(tab.path))?.key
    ?? mainTabs[0]?.key
    ?? 'customer-receivables';

  useEffect(() => {
    if (location.pathname === '/receivables' || location.pathname === '/receivables/') {
      navigate(mainTabs[0]?.path ?? '/receivables/customers', { replace: true });
    }
  }, [location.pathname, navigate, mainTabs]);

  const navigateToTab = useCallback(
    (tab: ProductNavTab) => {
      navigate(tab.path);
    },
    [navigate],
  );

  useRegisterProductNavSubnav(mainTabs, activeMainTab, navigateToTab);

  return (
    <Suspense
      fallback={
        <div style={{ padding: 48, textAlign: 'center' }}>
          <Spin tip={t('loading')} />
        </div>
      }
    >
      <Outlet />
    </Suspense>
  );
}
