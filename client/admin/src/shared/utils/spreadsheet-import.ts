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

function isValidIsoParts(year: number, month: number, day: number): boolean {
  if (month < 1 || month > 12 || day < 1 || day > 31) return false;
  const dt = new Date(year, month - 1, day);
  return dt.getFullYear() === year && dt.getMonth() === month - 1 && dt.getDate() === day;
}

function toIsoDate(year: string, month: string, day: string): string | undefined {
  const y = year.length === 2 ? Number(`20${year}`) : Number(year);
  const m = Number(month);
  const d = Number(day);
  if (!isValidIsoParts(y, m, d)) return undefined;
  return `${String(y).padStart(4, '0')}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
}

export function parseOptionalDate(value: string): string | undefined {
  if (!value) return undefined;
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [y, m, d] = value.split('-');
    return toIsoDate(y, m, d);
  }

  const parts = value.match(/^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2,4})$/);
  if (parts) {
    const a = Number(parts[1]);
    const b = Number(parts[2]);
    const year = parts[3];

    // Excel thường xuất MM/DD/YY (vd. 12/31/30); VN thường DD/MM/YYYY.
    const candidates: Array<[string, string]> = [];
    if (a > 12) candidates.push([parts[1], parts[2]]); // DD/MM
    else if (b > 12) candidates.push([parts[2], parts[1]]); // MM/DD → day, month
    else {
      candidates.push([parts[1], parts[2]]); // ưu tiên DD/MM
      candidates.push([parts[2], parts[1]]); // thử MM/DD
    }

    for (const [day, month] of candidates) {
      const iso = toIsoDate(year, month, day);
      if (iso) return iso;
    }
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
  'manufacture_date',
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
