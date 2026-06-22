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

export function parseMoneyInput(value: string | undefined): number | undefined {
  if (!value) return undefined;
  const digits = value.replace(/[^\d]/g, '');
  if (!digits) return undefined;
  return Number(digits);
}

/** Hiển thị tiền trên danh sách — ví dụ `15.000 ₫` */
export function formatDisplayMoney(v?: number | null): string {
  if (v == null) return '—';
  return `${new Intl.NumberFormat('vi-VN').format(v)} ₫`;
}

/** Căn phải + định dạng nghìn (vi-VN) cho InputNumber tiền */
export const moneyInputClassName = 'money-input';

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
