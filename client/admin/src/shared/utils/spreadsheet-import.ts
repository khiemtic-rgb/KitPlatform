import * as XLSX from 'xlsx';

export type SpreadsheetRow = Record<string, string>;

function normalizeHeader(value: unknown): string {
  return String(value ?? '')
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\s+/g, '_');
}

function cellText(value: unknown): string {
  if (value == null) return '';
  if (value instanceof Date) return value.toISOString().slice(0, 10);
  return String(value).trim();
}

export async function parseSpreadsheetFile(file: File): Promise<SpreadsheetRow[]> {
  const buffer = await file.arrayBuffer();
  const workbook = XLSX.read(buffer, { type: 'array', cellDates: true });
  const sheetName = workbook.SheetNames[0];
  if (!sheetName) return [];

  const sheet = workbook.Sheets[sheetName];
  const matrix = XLSX.utils.sheet_to_json<(string | number | Date | null)[]>(sheet, {
    header: 1,
    defval: '',
    raw: false,
  });
  if (matrix.length < 2) return [];

  const headers = (matrix[0] ?? []).map(normalizeHeader);
  const rows: SpreadsheetRow[] = [];

  for (let i = 1; i < matrix.length; i++) {
    const line = matrix[i] ?? [];
    const row: SpreadsheetRow = {};
    let hasValue = false;
    headers.forEach((header, col) => {
      if (!header) return;
      const text = cellText(line[col]);
      if (text) hasValue = true;
      row[header] = text;
    });
    if (hasValue) rows.push(row);
  }

  return rows;
}

export function pickRowValue(row: SpreadsheetRow, ...keys: string[]): string {
  for (const key of keys) {
    const normalized = normalizeHeader(key);
    const value = row[normalized];
    if (value) return value;
  }
  return '';
}

export function parseDecimal(value: string): number | undefined {
  if (!value) return undefined;
  const cleaned = value.replace(/[^\d.,-]/g, '').replace(/\./g, '').replace(',', '.');
  const num = Number(cleaned);
  return Number.isFinite(num) ? num : undefined;
}

export function parseOptionalDate(value: string): string | undefined {
  if (!value) return undefined;
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
  const dmy = value.match(/^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2,4})$/);
  if (dmy) {
    const day = dmy[1].padStart(2, '0');
    const month = dmy[2].padStart(2, '0');
    let year = dmy[3];
    if (year.length === 2) year = `20${year}`;
    return `${year}-${month}-${day}`;
  }
  const parsed = Date.parse(value);
  if (!Number.isNaN(parsed)) return new Date(parsed).toISOString().slice(0, 10);
  return undefined;
}

export const PRODUCT_IMPORT_TEMPLATE_HEADERS = [
  'product_code',
  'product_name',
  'generic_name',
  'barcode',
  'sale_unit_name',
  'retail_price',
  'min_stock_qty',
  'category_code',
  'brand_code',
  'drug_type',
];

export const OPENING_BALANCE_TEMPLATE_HEADERS = [
  'product_key',
  'batch_number',
  'expiry_date',
  'quantity',
  'unit_cost',
];

export function downloadCsvTemplate(filename: string, headers: string[]): void {
  const csv = `${headers.join(',')}\n`;
  const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
