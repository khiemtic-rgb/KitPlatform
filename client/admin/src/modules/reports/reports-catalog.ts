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
  favorite?: boolean;
}

type ReportDefinitionMeta = Omit<ReportDefinition, 'name' | 'description'>;

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
    code: 'PROC-01',
    category: 'procurement',
    path: '/reports/procurement/grn-value',
    apiPath: 'procurement/grn-value',
    supportsDateRange: true,
    supportsGroupBy: ['supplier', 'month', 'day'],
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

export function getReportDefinitions(): ReportDefinition[] {
  return REPORT_DEFINITIONS_META.map(localizeReport);
}

export function getReportCategoryLabel(category: ReportCategory): string {
  return reportsT()(`categories.${category}`);
}

export function findReportByPath(pathname: string): ReportDefinition | undefined {
  return getReportDefinitions().find((r) => pathname.startsWith(r.path));
}

export function reportsForCategory(category: ReportCategory): ReportDefinition[] {
  return getReportDefinitions().filter((r) => r.category === category);
}
