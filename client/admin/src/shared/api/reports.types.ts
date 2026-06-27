export type ReportColumnFormat = 'text' | 'money' | 'qty' | 'date' | 'integer';

export interface ReportColumn {
  key: string;
  title: string;
  format: ReportColumnFormat;
  align: 'left' | 'right';
}

export interface ReportTableResult {
  reportCode: string;
  title: string;
  generatedAtUtc: string;
  filterLabels: Record<string, string>;
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  totals?: Record<string, unknown> | null;
}

export interface ReportCatalogItem {
  code: string;
  name: string;
  category: string;
  description: string;
  requiresSales: boolean;
  requiresProcurement: boolean;
  requiresInventory: boolean;
}

export interface ReportQueryParams {
  from?: string;
  to?: string;
  groupBy?: string;
  warehouseId?: string;
  supplierId?: string;
  search?: string;
  expiryDays?: number;
}
