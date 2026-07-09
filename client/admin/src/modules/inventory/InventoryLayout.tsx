import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AuditOutlined,
  BankOutlined,
  CheckSquareOutlined,
  DatabaseOutlined,
  ImportOutlined,
  SwapOutlined,
  ExportOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';

export function InventoryLayout() {
  const { t } = useTranslation('inventory', { keyPrefix: 'inventoryLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const tabs = useMemo(
    () => [
      {
        key: 'opening',
        label: t('opening'),
        path: '/inventory/opening-balance',
        icon: <ImportOutlined />,
      },
      { key: 'stock', label: t('stock'), path: '/inventory/stock', icon: <DatabaseOutlined /> },
      { key: 'low-stock', label: t('lowStock'), path: '/inventory/low-stock', icon: <WarningOutlined /> },
      { key: 'gpp-checklist', label: t('gppChecklist'), path: '/inventory/gpp-checklist', icon: <CheckSquareOutlined /> },
      { key: 'transfers', label: t('transfers'), path: '/inventory/transfers', icon: <SwapOutlined /> },
      { key: 'adjustments', label: t('adjustments'), path: '/inventory/adjustments', icon: <AuditOutlined /> },
      { key: 'warehouses', label: t('warehouses'), path: '/inventory/warehouses', icon: <BankOutlined /> },
      { key: 'qd540', label: t('qd540Export'), path: '/inventory/qd540-export', icon: <ExportOutlined /> },
    ],
    [t],
  );

  useEffect(() => {
    if (location.pathname === '/inventory' || location.pathname === '/inventory/') {
      navigate('/inventory/opening-balance', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = tabs.find((tab) => location.pathname.startsWith(tab.path))?.key ?? 'opening';

  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  return <Outlet />;
}
