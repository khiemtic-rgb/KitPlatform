import { reportsT } from '@/shared/i18n';

export type ReportCategory = 'sales' | 'procurement' | 'inventory';

export type ReportGroupByOption = 'day' | 'week' | 'month' | 'supplier';

export interface ReportDefinition {
  code: string;
  name: string;
  description: string;
  category: ReportCategory;
  path: string;
  /** API path segment under /reports */
  apiPath: string;
  supportsDateRange?: boolean;
  supportsGroupBy?: ReportGroupByOption[];
  supportsWarehouse?: boolean;
  supportsSupplier?: boolean;
  supportsSearch?: boolean;
  supportsExpiryDays?: boolean;
  supportsEmployee?: boolean;
  supportsBranch?: boolean;
  favorite?: boolean;
  /** Connect PK report — hide without Connect module or during DEMO audit_slim_nav. */
  requiresConnect?: boolean;
  hideWhenAuditSlim?: boolean;
  /** Keep route + API, hide from tabs / home (e.g. staff product view). */
  hideFromNav?: boolean;
}

type ReportDefinitionMeta = Omit<ReportDefinition, 'name' | 'description'>;

export type ReportCatalogOptions = {
  auditSlimNav?: boolean;
  connectEnabled?: boolean;
  includeHidden?: boolean;
};

const REPORT_DEFINITIONS_META: ReportDefinitionMeta[] = [
  {
    code: 'SALES-01',
    category: 'sales',
    path: '/reports/sales/revenue-by-period',
    apiPath: 'sales/revenue-by-period',
    supportsDateRange: true,
    supportsGroupBy: ['day', 'week', 'month'],
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'SALES-02',
    category: 'sales',
    path: '/reports/sales/revenue-by-payment-method',
    apiPath: 'sales/revenue-by-payment-method',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'SALES-03',
    category: 'sales',
    path: '/reports/sales/shifts',
    apiPath: 'sales/shifts',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'SALES-04',
    category: 'sales',
    path: '/reports/sales/revenue-by-category',
    apiPath: 'sales/revenue-by-category',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
  },
  {
    code: 'SALES-06',
    category: 'sales',
    path: '/reports/sales/revenue-by-employee',
    apiPath: 'sales/revenue-by-employee',
    supportsDateRange: true,
    supportsWarehouse: true,
    supportsEmployee: true,
    favorite: true,
  },
  {
    code: 'SALES-07',
    category: 'sales',
    path: '/reports/sales/revenue-by-employee-product',
    apiPath: 'sales/revenue-by-employee-product',
    supportsDateRange: true,
    supportsWarehouse: true,
    supportsEmployee: true,
    supportsSearch: true,
    hideFromNav: true,
  },
  {
    code: 'SALES-08',
    category: 'sales',
    path: '/reports/customers',
    apiPath: 'sales/revenue-by-customer',
    supportsDateRange: true,
    supportsWarehouse: true,
    supportsSearch: true,
    hideFromNav: true,
  },
  {
    code: 'SALES-09',
    category: 'sales',
    path: '/reports/sales/shift-close-by-employee',
    apiPath: 'sales/shift-close-by-employee',
    supportsDateRange: true,
    supportsWarehouse: true,
    supportsEmployee: true,
    supportsBranch: true,
    favorite: true,
  },
  {
    code: 'SALES-10',
    category: 'sales',
    path: '/reports/sales/receivables-movement',
    apiPath: 'sales/receivables-movement',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
  },
  // SALES-05: Connect PK — visible only when Connect enabled and not DEMO audit slim
  {
    code: 'SALES-05',
    category: 'sales',
    path: '/reports/sales/revenue-by-clinic-doctor',
    apiPath: 'sales/revenue-by-clinic-doctor',
    supportsDateRange: true,
    supportsWarehouse: true,
    favorite: true,
    requiresConnect: true,
    hideWhenAuditSlim: true,
  },
  {
    code: 'PROC-01',
    category: 'procurement',
    path: '/reports/procurement/grn-value',
    apiPath: 'procurement/grn-value',
    supportsDateRange: true,
    supportsGroupBy: ['supplier', 'month', 'week', 'day'],
    supportsWarehouse: true,
    supportsSupplier: true,
    favorite: true,
  },
  {
    code: 'PROC-03',
    category: 'procurement',
    path: '/reports/procurement/payables-snapshot',
    apiPath: 'procurement/payables-snapshot',
    favorite: true,
  },
  {
    code: 'INV-01',
    category: 'inventory',
    path: '/reports/inventory/stock-snapshot',
    apiPath: 'inventory/stock-snapshot',
    supportsWarehouse: true,
    supportsSearch: true,
    favorite: true,
  },
  {
    code: 'INV-02',
    category: 'inventory',
    path: '/reports/inventory/near-expiry',
    apiPath: 'inventory/near-expiry',
    supportsWarehouse: true,
    supportsExpiryDays: true,
    favorite: true,
  },
  {
    code: 'INV-03',
    category: 'inventory',
    path: '/reports/inventory/movement-summary',
    apiPath: 'inventory/movement-summary',
    supportsDateRange: true,
    supportsWarehouse: true,
    supportsSearch: true,
    favorite: true,
  },
];

function localizeReport(meta: ReportDefinitionMeta): ReportDefinition {
  const t = reportsT();
  return {
    ...meta,
    name: t(`reports.${meta.code}.name`),
    description: t(`reports.${meta.code}.description`),
  };
}

export function getReportDefinitions(options?: ReportCatalogOptions): ReportDefinition[] {
  const auditSlimNav = options?.auditSlimNav === true;
  const connectEnabled = options?.connectEnabled === true;
  const includeHidden = options?.includeHidden === true;
  return REPORT_DEFINITIONS_META
    .filter((meta) => {
      if (meta.requiresConnect && !connectEnabled) return false;
      if (auditSlimNav && meta.hideWhenAuditSlim) return false;
      if (meta.hideFromNav && !includeHidden) return false;
      return true;
    })
    .map(localizeReport);
}

export function getReportCategoryLabel(category: ReportCategory): string {
  return reportsT()(`categories.${category}`);
}

export function findReportByPath(
  pathname: string,
  options?: ReportCatalogOptions,
): ReportDefinition | undefined {
  const matches = getReportDefinitions({ ...options, includeHidden: true }).filter(
    (r) => pathname === r.path || pathname.startsWith(`${r.path}/`),
  );
  return matches.sort((a, b) => b.path.length - a.path.length)[0];
}

export function reportsForCategory(
  category: ReportCategory,
  options?: ReportCatalogOptions,
): ReportDefinition[] {
  return getReportDefinitions(options).filter((r) => r.category === category);
}
