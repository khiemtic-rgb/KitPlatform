import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Tabs, Typography } from 'antd';
import { useLocation, useNavigate } from 'react-router-dom';
import { secondaryTabsBarStyle } from '@/shared/components/module-tabs.ui';
import { reportsForCategory, type ReportCategory } from '@/modules/reports/reports-catalog';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';

export function categoryFromReportPath(pathname: string): ReportCategory | null {
  if (pathname.startsWith('/reports/sales')) return 'sales';
  if (pathname.startsWith('/reports/procurement')) return 'procurement';
  if (pathname.startsWith('/reports/inventory')) return 'inventory';
  return null;
}

export function ReportCategoryNav() {
  const { i18n } = useTranslation();
  const location = useLocation();
  const navigate = useNavigate();
  const auditSlimNav = useAuditSlimNav();
  const category = categoryFromReportPath(location.pathname);
  const reports = useMemo(
    () => (category ? reportsForCategory(category, { auditSlimNav }) : []),
    [category, i18n.language, auditSlimNav],
  );

  if (!category) return null;

  const activeReport = reports.find((r) => location.pathname.startsWith(r.path));

  return (
    <div style={secondaryTabsBarStyle}>
      <Tabs
        activeKey={activeReport?.path ?? reports[0]?.path}
        size="small"
        items={reports.map((r) => ({
          key: r.path,
          label: (
            <span>
              <Typography.Text code style={{ fontSize: 11, marginRight: 6 }}>
                {r.code}
              </Typography.Text>
              {r.name}
            </span>
          ),
        }))}
        onChange={(path) => navigate(path)}
      />
    </div>
  );
}
