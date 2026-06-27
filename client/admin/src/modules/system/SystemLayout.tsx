import { useEffect } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import { BankOutlined, FileSearchOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';

const tabs = [
  { key: 'users', label: 'Nhân viên', path: '/system/users', icon: <UserOutlined /> },
  { key: 'roles', label: 'Vai trò', path: '/system/roles', icon: <SafetyCertificateOutlined /> },
  { key: 'branches', label: 'Chi nhánh', path: '/system/branches', icon: <BankOutlined /> },
  { key: 'audit-log', label: 'Nhật ký', path: '/system/audit-log', icon: <FileSearchOutlined /> },
];

export function SystemLayout() {
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    if (location.pathname === '/system' || location.pathname === '/system/') {
      navigate('/system/users', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    tabs.find((t) => location.pathname.startsWith(t.path))?.key ?? 'users';

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
