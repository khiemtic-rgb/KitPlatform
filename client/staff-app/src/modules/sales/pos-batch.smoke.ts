import { formatCartLotButton, showsBatchLabelField, showsBatchPicker } from './pos-batch';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

assert(showsBatchLabelField('suggest'), 'suggest shows lot field');
assert(showsBatchPicker('suggest', [{ batchId: '1', batchNumber: 'A', quantityAvailable: 1, isSuggested: true }]));
assert(!showsBatchPicker('suggest', []));

const label = formatCartLotButton({
  batchLabel: 'TON-DAU-SAPO',
  batchHints: [
    {
      batchId: 'b1',
      batchNumber: 'TON-DAU-SAPO',
      expiryDate: '2030-12-31',
      quantityAvailable: 12,
      isSuggested: true,
    },
  ],
});
assert(label.includes('TON-DAU-SAPO'), `lot missing: ${label}`);
assert(label.includes('12/2030'), `hsd missing: ${label}`);
assert(label.includes('FEFO'), `fefo missing: ${label}`);

console.log('pos-batch.smoke: ok');
