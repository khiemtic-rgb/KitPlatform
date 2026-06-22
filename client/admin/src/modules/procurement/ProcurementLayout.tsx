import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';

const tabs = [
  { key: 'orders', label: 'Đơn đặt hàng', path: '/procurement/purchase-orders' },
  { key: 'receipts', label: 'Phiếu nhập hàng', path: '/procurement/goods-receipts' },
  { key: 'suppliers', label: 'Nhà cung cấp', path: '/procurement/suppliers' },
  { key: 'payments', label: 'Thanh toán NCC', path: '/procurement/supplier-payments' },
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
