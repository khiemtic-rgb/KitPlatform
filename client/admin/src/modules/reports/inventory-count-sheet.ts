export type InventoryCountColumn = {
  key: string;
  title: string;
  format?: string;
};

export type InventoryCountCell = string | number | { formula: string };

const COUNT_COLUMNS: InventoryCountColumn[] = [
  { key: 'actualQty', title: 'Thực tế' },
  { key: 'varianceQty', title: 'Chênh lệch' },
  { key: 'note', title: 'Ghi chú' },
];

const NUMERIC_FORMATS = new Set(['qty', 'money', 'integer']);

export function buildInventoryCountSheet(input: {
  columns: InventoryCountColumn[];
  rows: Record<string, unknown>[];
}): InventoryCountCell[][] {
  const columns = [...input.columns, ...COUNT_COLUMNS];
  const header = columns.map((column) => column.title);
  const closingIndex = input.columns.findIndex((column) => column.key === 'closingQty');
  const actualIndex = input.columns.length;
  const varianceIndex = actualIndex + 1;
  const body = input.rows.map((row, index) => {
    const excelRow = index + 2;
    const cells: InventoryCountCell[] = input.columns.map((column) => sheetValue(row[column.key], column.format));
    cells.push('');
    cells.push(
      closingIndex >= 0
        ? {
            formula: `IF(${excelColumn(actualIndex)}${excelRow}="","",${excelColumn(actualIndex)}${excelRow}-${excelColumn(closingIndex)}${excelRow})`,
          }
        : '',
    );
    cells.push('');
    return cells;
  });

  if (body.length === 0) return [header];

  const firstDataRow = 2;
  const lastDataRow = body.length + 1;
  const total: InventoryCountCell[] = columns.map((column, index) => {
    if (column.key === 'productName') return 'Tổng cộng';
    if (index === varianceIndex || index === actualIndex || NUMERIC_FORMATS.has(input.columns[index]?.format ?? '')) {
      return { formula: `SUM(${excelColumn(index)}${firstDataRow}:${excelColumn(index)}${lastDataRow})` };
    }
    return '';
  });
  return [header, ...body, total];
}

function sheetValue(value: unknown, format?: string): string | number {
  if (value == null || value === '') return '';
  if (format && NUMERIC_FORMATS.has(format)) {
    const number = Number(value);
    return Number.isFinite(number) ? number : '';
  }
  return String(value);
}

function excelColumn(index: number): string {
  let n = index + 1;
  let label = '';
  while (n > 0) {
    const remainder = (n - 1) % 26;
    label = String.fromCharCode(65 + remainder) + label;
    n = Math.floor((n - 1) / 26);
  }
  return label;
}
