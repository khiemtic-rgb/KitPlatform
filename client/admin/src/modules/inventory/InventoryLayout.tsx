import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import {
  AuditOutlined,
  BankOutlined,
  DatabaseOutlined,
  ImportOutlined,
  SwapOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';

const tabs = [
  { key: 'stock', label: 'Tồn kho', path: '/inventory/stock', icon: <DatabaseOutlined /> },
  { key: 'low-stock', label: 'Tồn thấp', path: '/inventory/low-stock', icon: <WarningOutlined /> },
  { key: 'warehouses', label: 'Kho', path: '/inventory/warehouses', icon: <BankOutlined /> },
  {
    key: 'opening',
    label: 'Nhập tồn đầu kỳ',
    path: '/inventory/opening-balance',
    icon: <ImportOutlined />,
  },
  { key: 'transfers', label: 'Điều chuyển', path: '/inventory/transfers', icon: <SwapOutlined /> },
  { key: 'adjustments', label: 'Kiểm kê', path: '/inventory/adjustments', icon: <AuditOutlined /> },
];

export function InventoryLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (location.pathname === '/inventory' || location.pathname === '/inventory/') {
      navigate('/inventory/stock', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((t) => location.pathname.startsWith(t.path))?.key ?? 'stock';

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
