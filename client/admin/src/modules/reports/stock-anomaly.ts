/** Tag gợi ý trên danh sách rà — không ẩn dòng. Dòng chỉ ra khi chủ bấm Đã chuẩn. */
export const STOCK_QTY_ANOMALY = 100_000;
export const STOCK_UNIT_COST_ANOMALY = 10_000_000;
/** Dòng vài trăm triệu vẫn làm tổng nhà thuốc thành chục tỷ sau khi đã kéo giá/SL xuống dưới 1 tỷ. */
export const STOCK_VALUE_ANOMALY = 100_000_000;
/** Khi giá trị đã đỏ mà SL vẫn lớn: mở luôn “Sửa số lượng”, không chỉ sửa giá. */
export const STOCK_QTY_FIX_HINT = 10_000;

export type StockAnomalyReason = 'qty' | 'cost' | 'value';

export function impliedUnitCost(qty: number, value: number): number {
  if (!(qty > 0)) return 0;
  return value / qty;
}

export function classifyStockAnomaly(qty: number, value: number): StockAnomalyReason[] {
  const reasons: StockAnomalyReason[] = [];
  if (qty >= STOCK_QTY_ANOMALY) reasons.push('qty');
  if (impliedUnitCost(qty, value) >= STOCK_UNIT_COST_ANOMALY) reasons.push('cost');
  if (value >= STOCK_VALUE_ANOMALY) reasons.push('value');
  return reasons;
}

export function stockAnomalyNeedsQtyFix(qty: number, reasons: readonly StockAnomalyReason[]): boolean {
  if (reasons.includes('qty')) return true;
  return reasons.includes('value') && qty >= STOCK_QTY_FIX_HINT;
}

export function stockAnomalyNeedsCostFix(reasons: readonly StockAnomalyReason[]): boolean {
  return reasons.includes('cost') || reasons.includes('value');
}
