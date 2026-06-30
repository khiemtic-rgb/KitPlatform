import type { ReportColumn, ReportTableResult } from '@/shared/api/reports.types';
import { reportsT } from '@/shared/i18n';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney, formatDisplayQuantity } from '@/shared/utils/money';

export function formatReportCell(value: unknown, format: ReportColumn['format']): string {
  if (value == null || value === '') return '—';
  switch (format) {
    case 'money':
      return formatDisplayMoney(Number(value));
    case 'qty':
      return formatDisplayQuantity(Number(value));
    case 'integer':
      return Number(value).toLocaleString('vi-VN');
    case 'date':
      return formatDisplayDate(String(value));
    default:
      return String(value);
  }
}

export function exportReportCsv(result: ReportTableResult): void {
  const headers = result.columns.map((c) => c.title);
  const body = result.rows.map((row) =>
    result.columns.map((col) => formatReportCell(row[col.key], col.format)),
  );
  if (result.totals) {
    body.push(result.columns.map((col) => formatReportCell(result.totals?.[col.key], col.format)));
  }
  const escape = (cell: string) => `"${cell.replace(/"/g, '""')}"`;
  const csv = [headers, ...body].map((line) => line.map(escape).join(',')).join('\n');
  const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${result.reportCode}-${new Date().toISOString().slice(0, 10)}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

export function printReportElement(elementId: string, title?: string): void {
  const node = document.getElementById(elementId);
  if (!node) return;
  const t = reportsT();
  const printTitle = title ?? t('export.printTitle');
  const printedAt = t('export.printedAt');
  const win = window.open('', '_blank', 'noopener,noreferrer,width=900,height=700');
  if (!win) return;
  win.document.write(`<!DOCTYPE html>
    <html><head><meta charset="utf-8"><title>${printTitle}</title>
    <style>
      @page { size: A4 portrait; margin: 12mm 14mm; }
      body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11px; padding: 0; color: #111; }
      h1 { font-size: 16px; margin: 0 0 8px; text-align: center; }
      .meta { color: #555; margin-bottom: 12px; text-align: center; }
      table { width: 100%; border-collapse: collapse; }
      th, td { border: 1px solid #333; padding: 5px 6px; }
      th { background: #f0f0f0; }
      td.num { text-align: right; font-variant-numeric: tabular-nums; }
      tfoot td { font-weight: bold; background: #fafafa; }
    </style></head><body>
    <h1>${printTitle}</h1>
    <div class="meta">${printedAt} ${new Date().toLocaleString('vi-VN')}</div>
    ${node.innerHTML}</body></html>
  `);
  win.document.close();
  win.focus();
  win.print();
}
