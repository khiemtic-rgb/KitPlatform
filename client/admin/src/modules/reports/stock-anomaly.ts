/** Ngưỡng “tồn bất thường” — bắt lô/SKU kiểu data bẩn (thừa số 0, nhầm đơn vị). */
export const STOCK_QTY_ANOMALY = 100_000;
export const STOCK_UNIT_COST_ANOMALY = 10_000_000;
export const STOCK_VALUE_ANOMALY = 1_000_000_000;

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
