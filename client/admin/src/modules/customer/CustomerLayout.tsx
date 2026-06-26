import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import { UnorderedListOutlined } from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';

const tabs = [
  { key: 'list', label: 'Danh sách', path: '/customer/list', icon: <UnorderedListOutlined /> },
];

export function CustomerLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (location.pathname === '/customer' || location.pathname === '/customer/') {
      navigate('/customer/list', { replace: true });
    }
  }, [location.pathname, navigate]);

  const onDetailRoute =
    /^\/customer\/[^/]+/.test(location.pathname) && !location.pathname.startsWith('/customer/list');

  return (
    <div>
      {!onDetailRoute ? (
        <div style={moduleTabsShellStyle}>
          <div style={secondaryTabsBarStyle}>
            <Tabs
              activeKey="list"
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
      ) : null}
      <Outlet />
    </div>
  );
}
