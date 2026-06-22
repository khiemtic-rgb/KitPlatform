import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import { readPosDraftEditId } from '@/modules/sales/sales-draft-helpers';

const tabs = [
  { key: 'pos', label: 'Bán hàng (POS)', path: '/sales/pos' },
  { key: 'orders', label: 'Đơn bán', path: '/sales/orders' },
  { key: 'returns', label: 'Phiếu trả', path: '/sales/returns' },
  { key: 'customers', label: 'Đồng ý KH', path: '/sales/customers' },
  { key: 'shift', label: 'Ca làm việc', path: '/sales/shift' },
];

export function SalesLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (location.pathname === '/sales' || location.pathname === '/sales/') {
      navigate('/sales/pos', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((t) => location.pathname.startsWith(t.path))?.key ?? 'pos';

  return (
    <div>
      <Tabs
        activeKey={activeKey}
        items={tabs.map((t) => ({ key: t.key, label: t.label }))}
        onChange={(key) => {
          const tab = tabs.find((t) => t.key === key);
          if (!tab) return;
          if (key === 'pos') {
            const draftId = readPosDraftEditId();
            navigate(draftId ? `${tab.path}?draftId=${draftId}` : tab.path);
            return;
          }
          navigate(tab.path);
        }}
        style={{ marginBottom: 16 }}
      />
      <Outlet />
    </div>
  );
}
