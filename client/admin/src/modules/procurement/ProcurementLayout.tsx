import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import {
  AccountBookOutlined,
  ContainerOutlined,
  CreditCardOutlined,
  FileTextOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';

const tabs = [
  {
    key: 'orders',
    label: 'Đơn đặt hàng',
    path: '/procurement/purchase-orders',
    icon: <FileTextOutlined />,
  },
  {
    key: 'receipts',
    label: 'Phiếu nhập hàng',
    path: '/procurement/goods-receipts',
    icon: <ContainerOutlined />,
  },
  { key: 'suppliers', label: 'Nhà cung cấp', path: '/procurement/suppliers', icon: <TeamOutlined /> },
  {
    key: 'payables',
    label: 'Công nợ NCC',
    path: '/procurement/supplier-payables',
    icon: <AccountBookOutlined />,
  },
  {
    key: 'payments',
    label: 'Thanh toán NCC',
    path: '/procurement/supplier-payments',
    icon: <CreditCardOutlined />,
  },
];

export function ProcurementLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (location.pathname === '/procurement' || location.pathname === '/procurement/') {
      navigate('/procurement/purchase-orders', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((t) => location.pathname.startsWith(t.path))?.key ?? 'orders';

  return (
    <div>
      <div style={moduleTabsShellStyle}>
        <div style={secondaryTabsBarStyle}>
          <Tabs
            activeKey={activeKey}
            size="small"
            items={tabs.map((t) => ({
              key: t.key,
              label: secondaryTabLabel(t.label, t.icon),
            }))}
            onChange={(key) => {
              const tab = tabs.find((t) => t.key === key);
              if (tab) navigate(tab.path);
            }}
          />
        </div>
      </div>
      <Outlet />
    </div>
  );
}
