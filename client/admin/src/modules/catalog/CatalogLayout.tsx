import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';

const tabs = [
  { key: 'products', label: 'Sản phẩm', path: '/catalog/products' },
  { key: 'categories', label: 'Danh mục SP', path: '/catalog/categories' },
  { key: 'brands', label: 'Thương hiệu', path: '/catalog/brands' },
  { key: 'ingredients', label: 'Hoạt chất', path: '/catalog/ingredients' },
];

export function CatalogLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (location.pathname === '/catalog' || location.pathname === '/catalog/') {
      navigate('/catalog/products', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((t) => location.pathname.startsWith(t.path))?.key ?? 'products';

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
