export const movementLinesApiPath = 'inventory/movement-lines';

export type MovementLineQuery = {
  from?: string;
  to?: string;
  productId: string;
  warehouseId: string;
  direction: 'in' | 'out';
};

export function buildMovementLineQuery(
  loaded: { from?: string; to?: string },
  row: { productId?: unknown; warehouseId?: unknown },
  direction: 'in' | 'out',
): MovementLineQuery | null {
  const productId = typeof row.productId === 'string' ? row.productId : row.productId ? String(row.productId) : '';
  const warehouseId =
    typeof row.warehouseId === 'string' ? row.warehouseId : row.warehouseId ? String(row.warehouseId) : '';
  if (!productId || !warehouseId) return null;
  return {
    from: loaded.from,
    to: loaded.to,
    productId,
    warehouseId,
    direction,
  };
}
