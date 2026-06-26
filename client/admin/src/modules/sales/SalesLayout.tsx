import { Suspense, useEffect } from 'react';

import { Outlet, useLocation, useNavigate } from 'react-router-dom';

import { Badge, Spin, Tabs } from 'antd';

import {

  ClockCircleOutlined,

  CommentOutlined,

  FileTextOutlined,

  FormOutlined,

  MedicineBoxOutlined,

  GiftOutlined,

  RollbackOutlined,

  SafetyCertificateOutlined,

  SettingOutlined,

  ShopOutlined,

  ShoppingCartOutlined,

  TagOutlined,

} from '@ant-design/icons';

import { readPosDraftEditId } from '@/modules/sales/sales-draft-helpers';

import { useAdminChatUnread } from '@/modules/sales/useAdminChatUnread';

import { usePendingCustomerDraftCount } from '@/modules/sales/usePendingCustomerDraftCount';

import { useHasPermission } from '@/shared/auth/usePermission';

import { ensureDesktopNotificationPermission } from '@/shared/utils/desktop-notification';

import {

  moduleTabsShellStyle,

  primaryTabLabel,

  primaryTabsBarStyle,

  secondaryTabLabel,

  secondaryTabsBarStyle,

} from '@/shared/components/module-tabs.ui';



const settingsTabs = [

  {

    key: 'customers',

    label: 'Đồng ý KH',

    path: '/sales/customers',

    icon: <SafetyCertificateOutlined />,

  },

  {

    key: 'loyalty',

    label: 'Tích điểm',

    path: '/sales/loyalty',

    icon: <GiftOutlined />,

  },

  {

    key: 'vouchers',

    label: 'Voucher',

    path: '/sales/vouchers',

    icon: <TagOutlined />,

  },

  {

    key: 'store-info',

    label: 'Thông tin nhà thuốc',

    path: '/sales/settings',

    icon: <ShopOutlined />,

  },

] as const;



const mainTabs = [

  { key: 'pos', label: 'Bán hàng (POS)', path: '/sales/pos', icon: <ShoppingCartOutlined /> },

  { key: 'orders', label: 'Đơn bán', path: '/sales/orders', icon: <FileTextOutlined /> },

  {

    key: 'customer-drafts',

    label: 'Đơn hàng từ app',

    path: '/sales/customer-drafts',

    icon: <FormOutlined />,

  },

  {

    key: 'customer-reservations',

    label: 'Đặt trước app',

    path: '/sales/customer-reservations',

    icon: <MedicineBoxOutlined />,

  },

  { key: 'returns', label: 'Hàng trả lại', path: '/sales/returns', icon: <RollbackOutlined /> },

  { key: 'chat', label: 'Chat KH', path: '/sales/chat', icon: <CommentOutlined /> },

  { key: 'shift', label: 'Ca làm việc', path: '/sales/shift', icon: <ClockCircleOutlined /> },

  {

    key: 'settings',

    label: 'Cài đặt',

    path: settingsTabs[0].path,

    icon: <SettingOutlined />,

  },

];



export function SalesLayout() {

  const location = useLocation();

  const navigate = useNavigate();

  const canReadSales = useHasPermission('sales.read');

  const chatUnread = useAdminChatUnread(canReadSales);

  const pendingDrafts = usePendingCustomerDraftCount(canReadSales);



  const activeSettingsTab = settingsTabs.find((t) => location.pathname.startsWith(t.path));

  const activeMainTab =

    activeSettingsTab != null

      ? 'settings'

      : (mainTabs.find((t) => t.key !== 'settings' && location.pathname.startsWith(t.path))?.key ??

        'pos');



  useEffect(() => {

    if (location.pathname === '/sales' || location.pathname === '/sales/') {

      navigate('/sales/pos', { replace: true });

    }

  }, [location.pathname, navigate]);



  return (

    <div>

      <div style={moduleTabsShellStyle}>

        <div style={primaryTabsBarStyle}>

          <Tabs

            activeKey={activeMainTab}

            items={mainTabs.map((t) => {

              const labelNode = primaryTabLabel(t.label, t.icon);

              return {

                key: t.key,

                label:

                  t.key === 'chat' && chatUnread > 0 ? (

                    <Badge count={chatUnread} size="small" offset={[10, 0]}>

                      {labelNode}

                    </Badge>

                  ) : t.key === 'customer-drafts' && pendingDrafts > 0 ? (

                    <Badge count={pendingDrafts} size="small" offset={[10, 0]}>

                      {labelNode}

                    </Badge>

                  ) : (

                    labelNode

                  ),

              };

            })}

            onChange={(key) => {

              const tab = mainTabs.find((t) => t.key === key);

              if (!tab) return;

              if (key === 'chat') {

                void ensureDesktopNotificationPermission();

              }

              if (key === 'pos') {

                const draftId = readPosDraftEditId();

                navigate(draftId ? `${tab.path}?draftId=${draftId}` : tab.path);

                return;

              }

              navigate(tab.path);

            }}

          />

        </div>



        {activeSettingsTab ? (

          <div style={secondaryTabsBarStyle}>

            <Tabs

              activeKey={activeSettingsTab.key}

              size="small"

              items={settingsTabs.map((t) => ({

                key: t.key,

                label: secondaryTabLabel(t.label, t.icon),

              }))}

              onChange={(key) => {

                const tab = settingsTabs.find((t) => t.key === key);

                if (tab) navigate(tab.path);

              }}

            />

          </div>

        ) : null}

      </div>



      <Suspense

        fallback={

          <div style={{ padding: 48, textAlign: 'center' }}>

            <Spin tip="Đang tải..." />

          </div>

        }

      >

        <Outlet />

      </Suspense>

    </div>

  );

}


