import dayjs from 'dayjs';
import { formatDisplayDate } from '@/shared/utils/date';

export function formatThermalMoney(v: number): string {
  return `${new Intl.NumberFormat('vi-VN').format(Math.round(v))}đ`;
}

export function formatReceiptDate(iso?: string): string {
  if (!iso) return '—';
  const d = dayjs(iso);
  if (d.isValid()) return d.format('DD/MM/YYYY');
  return formatDisplayDate(iso);
}

export function formatReceiptSaleTime(iso?: string): string {
  if (!iso) return '—';
  const d = dayjs(iso);
  return d.isValid() ? d.format('HH:mm') : '—';
}

export function formatReceiptDateTime(iso?: string): string {
  if (!iso) return '—';
  const d = dayjs(iso);
  if (d.isValid()) return d.format('DD/MM/YYYY HH:mm');
  return formatDisplayDate(iso);
}

export function dashedLine(char = '─'): string {
  return `<div class="rule">${char.repeat(32)}</div>`;
}

export function rowBetween(left: string, right: string, className = ''): string {
  return `<div class="row ${className}"><span class="row-left">${left}</span><span class="row-right">${right}</span></div>`;
}

export const THERMAL_RECEIPT_STYLES = `
    * { box-sizing: border-box; margin: 0; padding: 0; }
    @page {
      size: 80mm auto;
      margin: 2mm 3mm;
    }
    html, body {
      width: 80mm;
      max-width: 80mm;
      margin: 0 auto;
      padding: 0;
      background: #fff;
      color: #000;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    body {
      font-family: 'Courier New', Courier, 'Liberation Mono', monospace;
      font-size: 12px;
      line-height: 1.35;
      padding: 4mm 3mm 6mm;
    }
    .receipt { width: 100%; }
    .center { text-align: center; }
    .store-name {
      font-size: 15px;
      font-weight: 700;
      letter-spacing: 0.02em;
      margin-bottom: 2px;
    }
    .store-sub {
      font-size: 11px;
      margin-bottom: 2px;
    }
    .store-contact {
      font-size: 11px;
      margin-bottom: 6px;
      word-break: break-word;
    }
    .title {
      font-size: 13px;
      font-weight: 700;
      margin: 4px 0 6px;
      letter-spacing: 0.04em;
    }
    .meta {
      font-size: 11px;
      margin-bottom: 2px;
      word-break: break-word;
    }
    .rule {
      text-align: center;
      overflow: hidden;
      white-space: nowrap;
      margin: 6px 0;
      font-size: 10px;
      letter-spacing: -0.05em;
      color: #000;
    }
    .item { margin-bottom: 6px; }
    .item-name {
      font-weight: 600;
      word-break: break-word;
      margin-bottom: 1px;
    }
    .item-sub {
      font-size: 11px;
      color: #333;
      word-break: break-word;
    }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 4px;
      align-items: flex-start;
    }
    .row-left {
      flex: 1 1 auto;
      min-width: 0;
      word-break: break-word;
    }
    .row-right {
      flex: 0 0 auto;
      text-align: right;
      white-space: nowrap;
    }
    .row.sub { font-size: 11px; }
    .row.total {
      font-size: 14px;
      font-weight: 700;
      margin-top: 2px;
    }
    .row.total .row-left,
    .row.total .row-right {
      font-weight: 700;
    }
    .footer {
      margin-top: 8px;
      font-size: 11px;
      text-align: center;
      line-height: 1.45;
    }
    .note {
      font-size: 10px;
      margin-top: 4px;
      text-align: center;
      font-style: italic;
    }
    @media print {
      body { padding: 0; }
      .no-print { display: none; }
    }
    @media (max-width: 220px) {
      body { font-size: 11px; }
      .store-name { font-size: 13px; }
    }
`;

export function buildThermalReceiptDocument(title: string, bodyHtml: string): string {
  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${title}</title>
  <style>${THERMAL_RECEIPT_STYLES}</style>
</head>
<body>
  <div class="receipt">${bodyHtml}</div>
  <script>window.onload = () => { window.print(); }</script>
</body>
</html>`;
}

export function openThermalPrintWindow(html: string): boolean {
  const win = window.open('', '_blank', 'width=360,height=720');
  if (!win) return false;
  win.document.write(html);
  win.document.close();
  return true;
}
