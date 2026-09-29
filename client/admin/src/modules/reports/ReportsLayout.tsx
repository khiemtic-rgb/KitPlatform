import { useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import {
  BarChartOutlined,
  InboxOutlined,
  ShoppingOutlined,
  ShopOutlined,
  TeamOutlined,
  UnorderedListOutlined,
  UserOutlined,
} from '@ant-design/icons';
import { useRegisterProductNavSubnav } from '@/shared/components/module-subnav.context';
import type { ProductNavTab } from '@/shared/product/product-phases';
import { useProductNavGuard } from '@/shared/product/useProductNavGuard';
import { useCanReportsRead } from '@/shared/auth/usePermission';
import { useAuthStore } from '@/shared/auth/auth.store';
import { findReportByPath } from '@/modules/reports/reports-catalog';
import { pushRecentReport } from '@/modules/reports/report-recent';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import './reports-hub.css';

export function ReportsLayout() {
  const { t } = useTranslation('reports', { keyPrefix: 'hub.tabs' });
  const location = useLocation();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const canReports = useCanReportsRead();
  const view = params.get('view');
  const username = useAuthStore((s) => s.user?.username ?? '');
  const auditSlimNav = useAuditSlimNav();
  const connectEnabled = useTenantPlatformStore((s) => s.loaded && s.isModuleEnabled('novixa_connect'));

  useEffect(() => {
    const report = findReportByPath(location.pathname, { auditSlimNav, connectEnabled });
    if (!report) return;
    pushRecentReport({
      path: report.path,
      code: report.code,
      name: report.name,
      group:
        report.code === 'SALES-08' || report.path.includes('/customers')
          ? 'customers'
          : report.path.includes('revenue-by-employee')
            ? 'staff'
            : report.category === 'inventory'
              ? 'inventory'
              : report.category === 'procurement'
                ? 'procurement'
                : report.category === 'sales'
                  ? 'sales'
                  : 'other',
      openedBy: username || '—',
      openedAt: new Date().toISOString(),
      from: params.get('from') || undefined,
      to: params.get('to') || undefined,
    });
  }, [location.pathname, auditSlimNav, connectEnabled, username, params]);

  const allTabs: ProductNavTab[] = useMemo(
    () => [
      { key: 'overview', label: t('overview'), path: '/reports', icon: <BarChartOutlined /> },
      { key: 'sales', label: t('sales'), path: '/reports/sales/revenue-by-period', icon: <ShopOutlined /> },
      { key: 'inventory', label: t('inventory'), path: '/reports/inventory/stock-snapshot', icon: <InboxOutlined /> },
      { key: 'customers', label: t('customers'), path: '/reports/customers', icon: <UserOutlined /> },
      { key: 'staff', label: t('staff'), path: '/reports/sales/revenue-by-employee', icon: <TeamOutlined /> },
      { key: 'procurement', label: t('procurement'), path: '/reports/procurement/grn-value', icon: <ShoppingOutlined /> },
      { key: 'list', label: t('list'), path: '/reports/catalog', icon: <UnorderedListOutlined /> },
    ],
    [t],
  );

  useProductNavGuard(allTabs, '/reports');

  useEffect(() => {
    if (location.pathname === '/reports/') {
      navigate('/reports', { replace: true });
    }
  }, [location.pathname, navigate]);

  const activeKey = (() => {
    const path = location.pathname;
    if (path.startsWith('/reports/sales/revenue-by-employee')) return 'staff';
    if (path.startsWith('/reports/sales')) return 'sales';
    if (path.startsWith('/reports/inventory')) return 'inventory';
    if (path.startsWith('/reports/procurement')) return 'procurement';
    if (path.startsWith('/reports/customers')) return 'customers';
    if (path.startsWith('/reports/catalog') || (path === '/reports' && view === 'list')) return 'list';
    if (path === '/reports' && view === 'customers') return 'customers';
    return 'overview';
  })();

  useRegisterProductNavSubnav(allTabs, activeKey, (tab) => navigate(tab.path));

  if (!canReports) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
