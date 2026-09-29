import {
  classifyStockAnomaly,
  impliedUnitCost,
  stockAnomalyNeedsCostFix,
  stockAnomalyNeedsQtyFix,
} from './stock-anomaly';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

assert(classifyStockAnomaly(40_003, 933_389_999).includes('value'), 'XH leftover 900tr must stay on board');
assert(!classifyStockAnomaly(40_003, 933_389_999).includes('qty'), '40k is under qty tag');
assert(!classifyStockAnomaly(40_003, 933_389_999).includes('cost'), '23k unit cost is under 10tr');
assert(stockAnomalyNeedsQtyFix(40_003, ['value']), '40k + value still offers count');
assert(stockAnomalyNeedsCostFix(['value']), 'value offers revalue');

assert(classifyStockAnomaly(327, 915_597_711).includes('value'), '327 x dirty cost still value');
assert(!stockAnomalyNeedsQtyFix(327, ['value']), '327 does not force count');

assert(classifyStockAnomaly(87, 80_000_000).length === 0, '80tr stays off board');
assert(classifyStockAnomaly(100_000, 1).includes('qty'), '100k qty still flags');
assert(impliedUnitCost(10, 120_000_000) === 12_000_000, 'implied cost');
assert(classifyStockAnomaly(10, 120_000_000).includes('cost'), '12tr unit cost flags');

console.log('stock-anomaly.smoke: ok');
