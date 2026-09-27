export type StockReviewTab = 'pending' | 'confirmed';

export function stockReviewKey(productId: string, warehouseId: string): string {
  return `${productId}::${warehouseId}`;
}

export function isStockLineConfirmed(
  productId: string,
  warehouseId: string,
  confirmed: ReadonlySet<string>,
): boolean {
  if (!productId || !warehouseId) return false;
  return confirmed.has(stockReviewKey(productId, warehouseId));
}

/** Worklist stays until the owner marks the line clean — thresholds do not hide it. */
export function filterStockReviewRows<T extends { productId: string; warehouseId: string; totalQty: number }>(
  rows: readonly T[],
  confirmed: ReadonlySet<string>,
  tab: StockReviewTab,
): T[] {
  return rows.filter((row) => {
    if (!(row.totalQty > 0)) return false;
    const clean = isStockLineConfirmed(row.productId, row.warehouseId, confirmed);
    return tab === 'confirmed' ? clean : !clean;
  });
}
