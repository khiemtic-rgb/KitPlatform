import { readFileSync } from 'node:fs';

let failed = 0;
function ok(cond: boolean, name: string) {
  if (!cond) {
    failed += 1;
    console.error('FAIL ' + name);
  } else {
    console.log('ok ' + name);
  }
}

const rules = readFileSync(
  new URL(
    '../../../../../src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Application/Inventory/InventoryLotRules.cs',
    import.meta.url,
  ),
  'utf8',
);
const stock = readFileSync(new URL('../../shared/api/inventory.api.ts', import.meta.url), 'utf8');
const controller = readFileSync(
  new URL('../../../../../src/KitPlatform.Api/Controllers/Pharmacy/StockController.cs', import.meta.url),
  'utf8',
);
const grn = readFileSync(new URL('../procurement/GrnPoLinesEditor.tsx', import.meta.url), 'utf8');
const opening = readFileSync(new URL('./OpeningBalancePage.tsx', import.meta.url), 'utf8');
const complete = readFileSync(
  new URL(
    '../../../../../src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Procurement/ProcurementRepository.cs',
    import.meta.url,
  ),
  'utf8',
);
const transfer = readFileSync(
  new URL(
    '../../../../../src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Inventory/InventoryRepository.cs',
    import.meta.url,
  ),
  'utf8',
);

ok(rules.includes('NormalizeBatchNumber'), 'normalize lot');
ok(rules.includes('Cùng số lô'), 'intra-document same dates');
ok(rules.includes('đã có'), 'reject mismatched NSX/HSD');
ok(stock.includes('/inventory/stock/lot-identity'), 'client lot-identity');
ok(controller.includes('[HttpGet("lot-identity")]'), 'API lot-identity');
ok(controller.includes('[HttpGet("lot-conflicts")]'), 'API lot-conflicts');
ok(stock.includes('/inventory/stock/lot-conflicts'), 'client lot-conflicts');
ok(stock.includes('lot-conflicts/unify'), 'client unify lot dates');
ok(
  readFileSync(new URL('./LotConflictPage.tsx', import.meta.url), 'utf8').includes('unifyLotDates'),
  'lot conflict page can apply dates',
);
ok(
  readFileSync(new URL('../../app/router.tsx', import.meta.url), 'utf8').includes('lot-conflicts'),
  'route lot-conflicts',
);
ok(grn.includes('columns.manufacture'), 'GRN has NSX');
ok(grn.includes('lotLocked'), 'GRN locks existing lot dates');
ok(opening.includes('manufactureDate'), 'opening has NSX');
ok(complete.includes('InventoryLotRules.Resolve'), 'GRN complete resolves lot dates');
ok(transfer.includes('Sửa tồn kho trước khi điều chuyển'), 'transfer blocks dirty lot');

if (failed > 0) process.exit(1);
