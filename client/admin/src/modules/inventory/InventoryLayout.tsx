import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';

const tabs = [
  { key: 'stock', label: 'Tồn kho', path: '/inventory/stock' },
  { key: 'warehouses', label: 'Kho', path: '/inventory/warehouses' },
  { key: 'opening', label: 'Nhập tồn đầu kỳ', path: '/inventory/opening-balance' },
  { key: 'transfers', label: 'Điều chuyển', path: '/inventory/transfers' },
  { key: 'adjustments', label: 'Kiểm kê', path: '/inventory/adjustments' },
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
      <Tabs
        activeKey={activeKey}
        items={tabs.map((t) => ({ key: t.key, label: t.label }))}
        onChange={(key) => {
          const tab = tabs.find((t) => t.key === key);
          if (tab) navigate(tab.path);
        }}
        style={{ marginBottom: 16 }}
      />
      <Outlet />
    </div>
  );
}
