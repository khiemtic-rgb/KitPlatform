export function formatMoneyInput(value: number | string | undefined): string {
  if (value === undefined || value === null || value === '') return '';
  const n =
    typeof value === 'number'
      ? Math.round(value)
      : parseMoneyInput(typeof value === 'string' ? value : String(value));
  if (n == null || Number.isNaN(n)) return '';
  return n.toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
}

/** Ô nhập tiền có hậu tố đ — ví dụ `15.000 đ` */
export function formatMoneyInputWithSuffix(value: number | string | undefined): string {
  const formatted = formatMoneyInput(value);
  return formatted ? `${formatted} đ` : '';
}

/** Ô nhập điểm có hậu tố — ví dụ `2.000 điểm` */
export function formatPointsInputWithSuffix(value: number | string | undefined): string {
  const formatted = formatMoneyInput(value);
  return formatted ? `${formatted} điểm` : '';
}

/** Ô nhập phần trăm có hậu tố — ví dụ `5 %` */
export function formatPercentInputWithSuffix(value: number | string | undefined): string {
  if (value === undefined || value === null || value === '') return '';
  const n =
    typeof value === 'number'
      ? value
      : parseMoneyInput(typeof value === 'string' ? value : String(value));
  if (n == null || Number.isNaN(n)) return '';
  return `${n} %`;
}

export function parsePercentInput(value: string | undefined): number | undefined {
  if (!value) return undefined;
  const normalized = value.replace('%', '').trim();
  const digits = normalized.replace(/[^\d.,]/g, '').replace(',', '.');
  if (!digits) return undefined;
  const n = Number(digits);
  return Number.isNaN(n) ? undefined : n;
}

export function parseMoneyInput(value: string | undefined): number | undefined {
  if (!value) return undefined;
  const digits = value.replace(/[^\d]/g, '');
  if (!digits) return undefined;
  return Number(digits);
}

/** Hiển thị số lượng — ví dụ `10.000` hoặc `1.234,5` */
export function formatQuantityInput(value: number | string | undefined): string {
  if (value === undefined || value === null || value === '') return '';
  const n =
    typeof value === 'number'
      ? value
      : parseQuantityInput(typeof value === 'string' ? value : String(value));
  if (n == null || Number.isNaN(n)) return '';
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 }).format(n);
}

export function parseQuantityInput(value: string | undefined): number | undefined {
  if (!value) return undefined;
  const trimmed = value.trim().replace(/\s/g, '');
  const decimalComma = trimmed.match(/^([\d.]*),(\d{1,3})$/);
  if (decimalComma) {
    const intPart = decimalComma[1].replace(/\./g, '');
    const n = Number(`${intPart}.${decimalComma[2]}`);
    return Number.isNaN(n) ? undefined : n;
  }
  const normalized = trimmed.replace(/\./g, '').replace(',', '.');
  if (!normalized) return undefined;
  const n = Number(normalized);
  return Number.isNaN(n) ? undefined : n;
}

/** Hiển thị số lượng trên bảng */
export function formatDisplayQuantity(v?: number | null): string {
  if (v == null) return '—';
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 }).format(v);
}

/** Ô số lượng read-only trên bảng mua hàng — căn phải, chữ số thẳng cột */
export const tableQuantityCellClassName = 'procurement-qty-cell';

/** Hiển thị tiền trên danh sách — ví dụ `15.000 ₫` */
export function formatDisplayMoney(v?: number | null): string {
  if (v == null) return '—';
  return `${new Intl.NumberFormat('vi-VN').format(v)} ₫`;
}

/** Căn phải + định dạng nghìn (vi-VN) cho InputNumber tiền */
export const moneyInputClassName = 'money-input';

export const quantityInputNumberProps = {
  min: 0.001,
  step: 1,
  precision: 3,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatQuantityInput(value),
  parser: (value: string | undefined) => parseQuantityInput(value) ?? 0,
} as const;

export const moneyInputNumberStyle = { width: '100%' } as const;

export const moneyInputNumberProps = {
  min: 1,
  precision: 0,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatMoneyInput(value),
  parser: (value: string | undefined) => parseMoneyInput(value) ?? 0,
} as const;

/** Số tiền cho phép 0 (đơn giá mua, giá vốn…) */
export const moneyInputNumberPropsAllowZero = {
  min: 0,
  precision: 0,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatMoneyInput(value),
  parser: (value: string | undefined) => parseMoneyInput(value) ?? 0,
} as const;

/** Số tiền cho phép 0, hiển thị hậu tố đ (chiết khấu giá trị…) */
export const moneyInputNumberPropsAllowZeroSuffix = {
  min: 0,
  precision: 0,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatMoneyInputWithSuffix(value),
  parser: (value: string | undefined) => parseMoneyInput(value) ?? 0,
} as const;

/** Điểm thưởng cho phép 0, hiển thị hậu tố điểm */
export const pointsInputNumberPropsAllowZeroSuffix = {
  min: 0,
  precision: 0,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatPointsInputWithSuffix(value),
  parser: (value: string | undefined) => parseMoneyInput(value) ?? 0,
} as const;

/** Phần trăm 0–100, hiển thị hậu tố % */
export const percentInputNumberProps = {
  min: 0,
  max: 100,
  precision: 0,
  controls: false,
  className: moneyInputClassName,
  formatter: (value: number | string | undefined) => formatPercentInputWithSuffix(value),
  parser: (value: string | undefined) => parsePercentInput(value) ?? 0,
} as const;
