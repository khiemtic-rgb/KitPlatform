import { buildMovementLineQuery, movementLinesApiPath } from './movement-line-query.ts';

const loaded = { from: '2026-03-01T00:00:00.000Z', to: '2026-04-01T00:00:00.000Z' };
const row = { productId: '11111111-1111-1111-1111-111111111111', warehouseId: '22222222-2222-2222-2222-222222222222' };

const inbound = buildMovementLineQuery(loaded, row, 'in');
const outbound = buildMovementLineQuery(loaded, row, 'out');
if (!inbound || !outbound) throw new Error('expected a detail query');
if (inbound.from !== loaded.from || inbound.to !== loaded.to) throw new Error('detail period drifted from the loaded report');
if (outbound.from !== loaded.from || outbound.to !== loaded.to) throw new Error('detail period drifted from the loaded report');
if (inbound.productId !== row.productId || inbound.warehouseId !== row.warehouseId) throw new Error('detail target drifted from the row');
if (inbound.direction !== 'in' || outbound.direction !== 'out') throw new Error('direction was not preserved');
if (movementLinesApiPath !== 'inventory/movement-lines') throw new Error('detail path changed');
if ('search' in inbound || 'apiPath' in inbound) throw new Error('detail query carried an extra field');
if (buildMovementLineQuery(loaded, { productId: '', warehouseId: row.warehouseId }, 'in') !== null) {
  throw new Error('missing product should not open detail');
}

console.log('admin movement-line-query.smoke: ok');
