import { buildInventoryCountSheet } from './inventory-count-sheet.ts';

const grid = buildInventoryCountSheet({
  columns: [
    { key: 'productCode', title: 'Mã SP', format: 'text' },
    { key: 'productName', title: 'Tên SP', format: 'text' },
    { key: 'warehouseName', title: 'Kho', format: 'text' },
    { key: 'openingQty', title: 'Tồn đầu', format: 'qty' },
    { key: 'inQty', title: 'Nhập', format: 'qty' },
    { key: 'outQty', title: 'Xuất', format: 'qty' },
    { key: 'adjustmentInQty', title: 'Kiểm kê tăng', format: 'qty' },
    { key: 'adjustmentOutQty', title: 'Kiểm kê giảm', format: 'qty' },
    { key: 'closingQty', title: 'Tồn cuối', format: 'qty' },
  ],
  rows: [
    {
      productCode: 'P1',
      productName: 'Paracetamol',
      warehouseName: 'Kho A',
      openingQty: 10,
      inQty: 4,
      outQty: 1,
      adjustmentInQty: 2,
      adjustmentOutQty: 1,
      closingQty: 13,
    },
  ],
});

const headers = grid[0];
if (headers.at(-3) !== 'Thực tế' || headers.at(-2) !== 'Chênh lệch' || headers.at(-1) !== 'Ghi chú') {
  throw new Error('count columns missing');
}
const data = grid[1];
if (data[8] !== 13) throw new Error('closing quantity was not kept as a number');
if (data[9] !== '') throw new Error('actual count must start blank');
if (data[11] !== '') throw new Error('note must start blank');
const variance = data[10];
if (typeof variance !== 'object' || variance.formula !== 'IF(J2="","",J2-I2)') {
  throw new Error(`variance formula drifted: ${JSON.stringify(variance)}`);
}
const total = grid[2];
if (total[1] !== 'Tổng cộng') throw new Error('total label missing');
const totalVariance = total[10];
if (typeof totalVariance !== 'object' || totalVariance.formula !== 'SUM(K2:K2)') {
  throw new Error('variance total is not a sum of the count rows');
}

console.log('admin inventory-count-sheet.smoke: ok');
