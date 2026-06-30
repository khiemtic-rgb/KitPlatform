import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Tabs } from 'antd';
import { BankOutlined, FileSearchOutlined, PrinterOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons';
import {
  moduleTabsShellStyle,
  secondaryTabLabel,
  secondaryTabsBarStyle,
} from '@/shared/components/module-tabs.ui';

export function SystemLayout() {
  const { t } = useTranslation('system', { keyPrefix: 'systemLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const tabs = useMemo(
    () => [
      { key: 'branches', label: t('branches'), path: '/system/branches', icon: <BankOutlined /> },
      { key: 'users', label: t('users'), path: '/system/users', icon: <UserOutlined /> },
      { key: 'roles', label: t('roles'), path: '/system/roles', icon: <SafetyCertificateOutlined /> },
      {
        key: 'pos-settings',
        label: t('posSettings'),
        path: '/system/pos-settings',
        icon: <PrinterOutlined />,
      },
      { key: 'audit-log', label: t('auditLog'), path: '/system/audit-log', icon: <FileSearchOutlined /> },
    ],
    [t],
  );

  useEffect(() => {
    if (location.pathname === '/system' || location.pathname === '/system/') {
      navigate('/system/branches', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey =
    tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'branches';

  return (
    <div>
      <div style={moduleTabsShellStyle}>
        <div style={secondaryTabsBarStyle}>
          <Tabs
            activeKey={activeKey}
            size="small"
            items={tabs.map((tab) => ({
              key: tab.key,
              label: secondaryTabLabel(tab.label, tab.icon),
            }))}
            onChange={(key) => {
              const tab = tabs.find((item) => item.key === key);
              if (tab) navigate(tab.path);
            }}
          />
        </div>
      </div>
      <Outlet />
    </div>
  );
}
