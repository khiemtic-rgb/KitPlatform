export const INVENTORY_ANOMALY_REPORT_PATH = '/reports/inventory/stock-snapshot';

export type StockFixTarget = {
  productId?: string;
  productCode?: string;
  warehouseId?: string;
  warehouseName?: string;
  from?: 'anomaly';
};

export function inventoryAnomalyReturnPath(input: StockFixTarget): string {
  const qs = new URLSearchParams();
  if (input.productId) qs.set('productId', input.productId);
  if (input.productCode) qs.set('q', input.productCode);
  if (input.warehouseId) qs.set('warehouseId', input.warehouseId);
  return `${INVENTORY_ANOMALY_REPORT_PATH}?${qs.toString()}`;
}

function applyProduct(qs: URLSearchParams, input: StockFixTarget) {
  if (input.productId) qs.set('productId', input.productId);
  if (input.productCode) qs.set('q', input.productCode);
  if (input.warehouseId) qs.set('warehouseId', input.warehouseId);
  if (input.from === 'anomaly') qs.set('from', 'anomaly');
}

export function inventoryStockFixPath(input: StockFixTarget & { productCode: string }): string {
  const qs = new URLSearchParams({ tab: 'fefo' });
  applyProduct(qs, { ...input, from: input.from ?? 'anomaly' });
  return `/inventory/stock?${qs.toString()}`;
}

export function inventoryRevaluePath(input: StockFixTarget & { productCode: string }): string {
  const qs = new URLSearchParams({ tab: 'fefo', revalue: '1' });
  applyProduct(qs, { ...input, from: input.from ?? 'anomaly' });
  return `/inventory/stock?${qs.toString()}`;
}

export function inventoryAdjustFixPath(input: StockFixTarget): string {
  const qs = new URLSearchParams({ create: '1' });
  applyProduct(qs, input);
  if (input.warehouseName) qs.set('warehouse', input.warehouseName);
  return `/inventory/adjustments?${qs.toString()}`;
}
