import type { TFunction } from 'i18next';

export function formatStockCap(stockAvailable: number, unitName: string): string {
  return `${stockAvailable.toLocaleString()} ${unitName}`;
}

export function capQuantityToStock(stockAvailable: number, quantity: number): number {
  if (stockAvailable <= 0) return 0;
  return Math.min(quantity, stockAvailable);
}

export function outOfStockWarningText(unitName: string, t?: TFunction<'sales'>): string {
  if (t) return t('pos.stock.outOfStock', { unit: unitName });
  return `Sản phẩm đã hết tồn tại kho này (ĐVT: ${unitName}).`;
}

export function stockCapWarningText(
  stockAvailable: number,
  unitName: string,
  t?: TFunction<'sales'>,
): string {
  if (stockAvailable <= 0) {
    return outOfStockWarningText(unitName, t);
  }
  const cap = formatStockCap(stockAvailable, unitName);
  if (t) return t('pos.stock.capped', { cap });
  return `Số lượng hàng còn trong kho chỉ có ${cap}. Bạn chỉ bán tối đa ${cap}.`;
}
