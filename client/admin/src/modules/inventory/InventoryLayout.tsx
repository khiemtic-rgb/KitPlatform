import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  AuditOutlined,
  CheckSquareOutlined,
  DatabaseOutlined,
  ExportOutlined,
  HomeOutlined,
  ImportOutlined,
  SwapOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import { useRegisterSimpleModuleSubnav } from '@/shared/components/module-subnav.context';

export function InventoryLayout() {
  const { t } = useTranslation('inventory', { keyPrefix: 'inventoryLayout.tabs' });
  const location = useLocation();
  const navigate = useNavigate();

  const tabs = useMemo(
    () => [
      { key: 'stock', label: t('stock'), path: '/inventory/stock', icon: <DatabaseOutlined /> },
      { key: 'low-stock', label: t('lowStock'), path: '/inventory/low-stock', icon: <WarningOutlined /> },
      { key: 'adjustments', label: t('adjustments'), path: '/inventory/adjustments', icon: <AuditOutlined /> },
      { key: 'transfers', label: t('transfers'), path: '/inventory/transfers', icon: <SwapOutlined /> },
      {
        key: 'opening',
        label: t('opening'),
        path: '/inventory/opening-balance',
        icon: <ImportOutlined />,
      },
      { key: 'warehouses', label: t('warehouses'), path: '/inventory/warehouses', icon: <HomeOutlined /> },
      {
        key: 'gpp-checklist',
        label: t('gppChecklist'),
        path: '/inventory/gpp-checklist',
        icon: <CheckSquareOutlined />,
      },
      { key: 'qd540', label: t('qd540Export'), path: '/inventory/qd540-export', icon: <ExportOutlined /> },
    ],
    [t],
  );

  useEffect(() => {
    if (location.pathname === '/inventory' || location.pathname === '/inventory/') {
      navigate('/inventory/stock', { replace: true });
    }
  }, [location.pathname, navigate]);

  // Prefer longer path matches first (e.g. low-stock before stock prefix edge-cases).
  const activeKey =
    [...tabs].sort((a, b) => b.path.length - a.path.length).find((tab) => location.pathname.startsWith(tab.path))
      ?.key ?? 'stock';

  useRegisterSimpleModuleSubnav(tabs, activeKey, navigate);

  return <Outlet />;
}
