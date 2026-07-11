import { Suspense, useCallback, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Spin } from 'antd';
import {
  ClockCircleOutlined,
  CommentOutlined,
  FileTextOutlined,
  MobileOutlined,
  RollbackOutlined,
  ShoppingCartOutlined,
} from '@ant-design/icons';
import { readPosDraftEditId } from '@/modules/sales/sales-draft-helpers';
import { useAdminChatUnread } from '@/modules/sales/useAdminChatUnread';
import { usePendingCustomerDraftCount } from '@/modules/sales/usePendingCustomerDraftCount';
import { useHasPermission } from '@/shared/auth/usePermission';
import { ensureDesktopNotificationPermission } from '@/shared/utils/desktop-notification';
import { useRegisterProductNavSubnav } from '@/shared/components/module-subnav.context';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { filterProductNavTabs } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';

const allMainTabDefs: Omit<ProductNavTab, 'label'>[] = [
  { key: 'pos', path: '/sales/pos', icon: <ShoppingCartOutlined /> },
  { key: 'orders', path: '/sales/orders', icon: <FileTextOutlined /> },
  {
    key: 'app-orders',
    path: '/sales/app-orders',
    icon: <MobileOutlined />,
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
  'app-orders': 'appOrders',
  returns: 'returns',
  chat: 'chat',
  shift: 'shift',
};

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

  const canReadSales = useHasPermission('sales.read');
  const chatUnread = useAdminChatUnread(canReadSales && mainTabs.some((tab) => tab.key === 'chat'));
  const pendingDrafts = usePendingCustomerDraftCount(canReadSales);

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
      if (tab.key === 'app-orders') {
        navigate('/sales/app-orders/drafts');
        return;
      }
      navigate(tab.path);
    },
    [navigate],
  );

  const badges = useMemo(
    () => ({
      chat: chatUnread,
      'app-orders': pendingDrafts,
    }),
    [chatUnread, pendingDrafts],
  );

  useRegisterProductNavSubnav(mainTabs, activeMainTab, navigateToTab, badges);

  return (
    <div className={isPosRoute ? 'sales-layout sales-layout--pos' : 'sales-layout'}>
      <Suspense
        fallback={
          <div style={{ padding: 48, textAlign: 'center' }}>
            <Spin tip={t('loading')} />
          </div>
        }
      >
        <div className="sales-layout__outlet">
          <Outlet />
        </div>
      </Suspense>
    </div>
  );
}
