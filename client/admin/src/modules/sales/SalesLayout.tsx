import { memo, Suspense, useCallback, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Badge, Spin } from 'antd';
import {
  ClockCircleOutlined,
  CommentOutlined,
  DollarOutlined,
  FileTextOutlined,
  FormOutlined,
  MedicineBoxOutlined,
  RollbackOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { readPosDraftEditId } from '@/modules/sales/sales-draft-helpers';
import { useAdminChatUnread } from '@/modules/sales/useAdminChatUnread';
import { usePendingCustomerDraftCount } from '@/modules/sales/usePendingCustomerDraftCount';
import { useHasPermission } from '@/shared/auth/usePermission';
import { ensureDesktopNotificationPermission } from '@/shared/utils/desktop-notification';
import { primaryTabLabel } from '@/shared/components/module-tabs.ui';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { filterProductNavTabs } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

const allMainTabDefs: Omit<ProductNavTab, 'label'>[] = [
  { key: 'pos', path: '/sales/pos', icon: <ShoppingCartOutlined /> },
  { key: 'orders', path: '/sales/orders', icon: <FileTextOutlined /> },
  {
    key: 'customer-receivables',
    path: '/sales/customer-receivables',
    icon: <DollarOutlined />,
    feature: 'sales.receivables',
  },
  {
    key: 'customer-payments',
    path: '/sales/customer-payments',
    icon: <DollarOutlined />,
    feature: 'sales.customerPayments',
  },
  {
    key: 'customer-drafts',
    path: '/sales/customer-drafts',
    icon: <FormOutlined />,
  },
  {
    key: 'customer-reservations',
    path: '/sales/customer-reservations',
    icon: <MedicineBoxOutlined />,
    feature: 'sales.customerReservations',
  },
  { key: 'returns', path: '/sales/returns', icon: <RollbackOutlined /> },
  {
    key: 'chat',
    path: '/sales/chat',
    icon: <CommentOutlined />,
    feature: 'sales.chat',
  },
  { key: 'shift', path: '/sales/shift', icon: <ClockCircleOutlined /> },
];

const tabLabelKeys: Record<string, string> = {
  pos: 'pos',
  orders: 'orders',
  'customer-receivables': 'customerReceivables',
  'customer-payments': 'customerPayments',
  'customer-drafts': 'customerDrafts',
  'customer-reservations': 'customerReservations',
  returns: 'returns',
  chat: 'chat',
  shift: 'shift',
};

type SalesSubnavProps = {
  tabs: ProductNavTab[];
  activeKey: string;
  onNavigate: (tab: ProductNavTab) => void;
};

const SalesSubnav = memo(function SalesSubnav({ tabs, activeKey, onNavigate }: SalesSubnavProps) {
  const { t } = useTranslation('sales', { keyPrefix: 'salesLayout' });
  const canReadSales = useHasPermission('sales.read');
  const chatUnread = useAdminChatUnread(canReadSales && tabs.some((tab) => tab.key === 'chat'));
  const pendingDrafts = usePendingCustomerDraftCount(canReadSales);

  return (
    <nav className="pos-sales-subnav" aria-label={t('navAriaLabel')}>
      {tabs.map((tab) => {
        const active = activeKey === tab.key;
        const labelNode = primaryTabLabel(tab.label, tab.icon);
        return (
          <button
            key={tab.key}
            type="button"
            className={
              active
                ? 'pos-sales-subnav__item pos-sales-subnav__item--active'
                : 'pos-sales-subnav__item'
            }
            onClick={() => onNavigate(tab)}
          >
            {tab.key === 'chat' && chatUnread > 0 ? (
              <Badge count={chatUnread} size="small" offset={[8, 0]}>
                {labelNode}
              </Badge>
            ) : tab.key === 'customer-drafts' && pendingDrafts > 0 ? (
              <Badge count={pendingDrafts} size="small" offset={[8, 0]}>
                {labelNode}
              </Badge>
            ) : (
              labelNode
            )}
          </button>
        );
      })}
    </nav>
  );
});

export function SalesLayout() {
  const { t } = useTranslation('sales', { keyPrefix: 'salesLayout' });
  const location = useLocation();
  const navigate = useNavigate();
  const isPosRoute = location.pathname.startsWith('/sales/pos');

  const allMainTabs = useMemo<ProductNavTab[]>(
    () =>
      allMainTabDefs.map((tab) => ({
        ...tab,
        label: t(`tabs.${tabLabelKeys[tab.key] ?? tab.key}`),
      })),
    [t],
  );

  const mainTabs = useMemo(() => filterProductNavTabs(allMainTabs), [allMainTabs]);

  useProductNavGuard(allMainTabs, '/sales/pos');

  const activeMainTab =
    mainTabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'pos';

  useEffect(() => {
    if (location.pathname === '/sales' || location.pathname === '/sales/') {
      navigate('/sales/pos', { replace: true });
    }
  }, [location.pathname, navigate]);

  const navigateToTab = useCallback(
    (tab: ProductNavTab) => {
      if (tab.key === 'chat') {
        void ensureDesktopNotificationPermission();
      }
      if (tab.key === 'pos') {
        const draftId = readPosDraftEditId();
        navigate(draftId ? `${tab.path}?draftId=${draftId}` : tab.path);
        return;
      }
      navigate(tab.path);
    },
    [navigate],
  );

  return (
    <div className={isPosRoute ? 'sales-layout--pos' : 'sales-layout'}>
      <SalesSubnav tabs={mainTabs} activeKey={activeMainTab} onNavigate={navigateToTab} />

      <Suspense
        fallback={
          <div style={{ padding: 48, textAlign: 'center' }}>
            <Spin tip={t('loading')} />
          </div>
        }
      >
        <div
          className={
            isPosRoute ? 'sales-layout__outlet sales-layout__outlet--pos' : 'sales-layout__outlet'
          }
        >
          <Outlet />
        </div>
      </Suspense>
    </div>
  );
}
