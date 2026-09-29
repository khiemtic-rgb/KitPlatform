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

const catalog = readFileSync(new URL('./reports-catalog.ts', import.meta.url), 'utf8');
const codes = readFileSync(
  new URL('../../../../../src/KitPlatform.Application/Reports/ReportCodes.cs', import.meta.url),
  'utf8',
);
const repo = readFileSync(
  new URL('../../../../../src/KitPlatform.Infrastructure/Reports/ReportsRepository.cs', import.meta.url),
  'utf8',
);
const accrual = readFileSync(
  new URL('../../../../../src/KitPlatform.Infrastructure/Reports/SalesAccrualSql.cs', import.meta.url),
  'utf8',
);
const service = readFileSync(
  new URL('../../../../../src/KitPlatform.Infrastructure/Reports/ReportsService.cs', import.meta.url),
  'utf8',
);
const controller = readFileSync(
  new URL('../../../../../src/KitPlatform.Api/Controllers/Reports/ReportsController.cs', import.meta.url),
  'utf8',
);
const router = readFileSync(new URL('../../app/router.tsx', import.meta.url), 'utf8');
const page = readFileSync(new URL('./SalesRevenuePage.tsx', import.meta.url), 'utf8');
const dashboard = readFileSync(
  new URL('../../../../../src/KitPlatform.Infrastructure/Dashboard/DashboardRepository.cs', import.meta.url),
  'utf8',
);

ok(catalog.includes("code: 'SALES-10'"), 'catalog SALES-10');
ok(catalog.includes("apiPath: 'sales/receivables-movement'"), 'catalog receivables path');
ok(codes.includes('SalesReceivablesMovement = "SALES-10"'), 'report code');
ok(accrual.includes('OriginalTotalExpr'), 'original total reconstructs returns');
ok(accrual.includes("interval '15 minutes'"), 'checkout window');
ok(accrual.includes('customer_payments'), 'collection matches posted AR voucher');
ok(accrual.includes('cp.amount'), 'collection matches voucher amount');
ok(accrual.includes('AgingDaysExpr'), 'calendar aging helper');
ok(repo.includes('checkoutPaid'), 'SALES-01 checkout column');
ok(repo.includes('newDebt'), 'SALES-01 new debt');
ok(repo.includes('collectionAmount'), 'SALES-01 collections');
ok(repo.includes('refundCash'), 'SALES-01 refund cash');
ok(repo.includes('refundDebt'), 'SALES-01 refund vs AR');
ok(repo.includes("'Ngoài ca'"), 'SALES-03 loose-shift row');
ok(repo.includes('coll_shift'), 'SALES-09 collection-time shift');
ok(repo.includes('AgingDaysExpr'), 'SALES-10 calendar aging');
ok(!repo.includes('EXTRACT(DAY FROM (CURRENT_TIMESTAMP'), 'SALES-10 no interval extract');
ok(!repo.includes('AND EXISTS (\n                      SELECT 1 FROM sales_payments'), 'line reports no longer gate on any payment');
ok(service.includes('Doanh thu theo ngày bán'), 'SALES-01 catalog copy');
ok(service.includes('Thu tiền theo hình thức'), 'SALES-02 renamed');
ok(controller.includes('sales/receivables-movement'), 'API route');
ok(router.includes('sales/receivables-movement'), 'admin route');
ok(page.includes('checkoutPaid'), 'SALES-01 UI paid');
ok(page.includes('newDebt'), 'SALES-01 UI debt');
ok(dashboard.includes('TodayNewDebt'), 'dashboard new debt');
ok(dashboard.includes('TodayCollected'), 'dashboard collected');

if (failed > 0) process.exit(1);
