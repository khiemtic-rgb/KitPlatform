export function formatStockCap(stockAvailable: number, unitName: string): string {
  return `${stockAvailable.toLocaleString('vi-VN')} ${unitName}`;
}

export function capQuantityToStock(stockAvailable: number, quantity: number): number {
  if (stockAvailable <= 0) return 0;
  return Math.min(quantity, stockAvailable);
}

export function outOfStockWarningText(unitName: string): string {
  return `Sản phẩm đã hết tồn tại kho này (ĐVT: ${unitName}).`;
}

export function stockCapWarningText(stockAvailable: number, unitName: string): string {
  if (stockAvailable <= 0) {
    return outOfStockWarningText(unitName);
  }
  const cap = formatStockCap(stockAvailable, unitName);
  return `Số lượng hàng còn trong kho chỉ có ${cap}. Bạn chỉ bán tối đa ${cap}.`;
}
