import { http } from '@/shared/api/http';
import type { ReportCatalogItem, ReportQueryParams, ReportTableResult } from '@/shared/api/reports.types';

function normalizeColumn(row: Record<string, unknown>) {
  return {
    key: String(row.key ?? row.Key ?? ''),
    title: String(row.title ?? row.Title ?? ''),
    format: String(row.format ?? row.Format ?? 'text') as ReportTableResult['columns'][number]['format'],
    align: (String(row.align ?? row.Align ?? 'left') === 'right' ? 'right' : 'left') as 'left' | 'right',
  };
}

function normalizeReport(data: Record<string, unknown>): ReportTableResult {
  const columnsRaw = (data.columns ?? data.Columns ?? []) as Record<string, unknown>[];
  const rowsRaw = (data.rows ?? data.Rows ?? []) as Record<string, unknown>[];
  const totalsRaw = (data.totals ?? data.Totals) as Record<string, unknown> | null | undefined;
  const filterRaw = (data.filterLabels ?? data.FilterLabels ?? {}) as Record<string, string>;

  return {
    reportCode: String(data.reportCode ?? data.ReportCode ?? ''),
    title: String(data.title ?? data.Title ?? ''),
    generatedAtUtc: String(data.generatedAtUtc ?? data.GeneratedAtUtc ?? ''),
    filterLabels: filterRaw,
    columns: columnsRaw.map(normalizeColumn),
    rows: rowsRaw.map((row) => {
      const out: Record<string, unknown> = {};
      for (const [k, v] of Object.entries(row)) out[k] = v;
      return out;
    }),
    totals: totalsRaw ?? null,
  };
}

export async function fetchReportCatalog(): Promise<ReportCatalogItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/reports/catalog');
  return data.map((row) => ({
    code: String(row.code ?? row.Code ?? ''),
    name: String(row.name ?? row.Name ?? ''),
    category: String(row.category ?? row.Category ?? ''),
    description: String(row.description ?? row.Description ?? ''),
    requiresSales: Boolean(row.requiresSales ?? row.RequiresSales),
    requiresProcurement: Boolean(row.requiresProcurement ?? row.RequiresProcurement),
    requiresInventory: Boolean(row.requiresInventory ?? row.RequiresInventory),
  }));
}

export async function runReport(apiPath: string, params: ReportQueryParams): Promise<ReportTableResult> {
  const { data } = await http.get<Record<string, unknown>>(`/reports/${apiPath}`, { params });
  return normalizeReport(data);
}
