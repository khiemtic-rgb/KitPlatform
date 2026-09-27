import { filterStockReviewRows, isStockLineConfirmed, stockReviewKey } from './stock-review';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

const confirmed = new Set([stockReviewKey('p1', 'w1')]);

assert(isStockLineConfirmed('p1', 'w1', confirmed), 'confirmed key matches');
assert(!isStockLineConfirmed('p2', 'w1', confirmed), 'other SKU stays pending');

const rows = [
  { productId: 'p1', warehouseId: 'w1', totalQty: 12, stockValue: 3_000_000 },
  { productId: 'p2', warehouseId: 'w1', totalQty: 5, stockValue: 2_000_000 },
  { productId: 'p3', warehouseId: 'w2', totalQty: 0, stockValue: 0 },
];

const pending = filterStockReviewRows(rows, confirmed, 'pending');
assert(pending.length === 1 && pending[0].productId === 'p2', 'few-million line stays until owner confirms');
assert(filterStockReviewRows(rows, confirmed, 'confirmed').length === 1, 'confirmed tab');
assert(filterStockReviewRows(rows, new Set(), 'pending').length === 2, 'zero stock omitted; rest pending');

console.log('stock-review.smoke: ok');
