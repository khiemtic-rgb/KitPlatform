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
const router = readFileSync(new URL('../../app/router.tsx', import.meta.url), 'utf8');
const page = readFileSync(new URL('./ShiftCloseByEmployeePage.tsx', import.meta.url), 'utf8');
const shiftPage = readFileSync(new URL('../sales/SalesShiftReportPage.tsx', import.meta.url), 'utf8');
const reportView = readFileSync(new URL('./ReportViewPage.tsx', import.meta.url), 'utf8');
const codes = readFileSync(
  new URL('../../../../../src/KitPlatform.Application/Reports/ReportCodes.cs', import.meta.url),
  'utf8',
);
const controller = readFileSync(
  new URL('../../../../../src/KitPlatform.Api/Controllers/Reports/ReportsController.cs', import.meta.url),
  'utf8',
);
const repo = readFileSync(
  new URL('../../../../../src/KitPlatform.Infrastructure/Reports/ReportsRepository.cs', import.meta.url),
  'utf8',
);
const auth = readFileSync(
  new URL('../../../../../src/KitPlatform.Api/Authorization/ReportsAuthorizationExtensions.cs', import.meta.url),
  'utf8',
);

ok(catalog.includes("code: 'SALES-09'"), 'catalog SALES-09');
ok(catalog.includes("path: '/reports/sales/shift-close-by-employee'"), 'catalog path');
ok(router.includes('sales/shift-close-by-employee'), 'router page');
ok(page.includes("runReport('sales/shift-close-by-employee'"), 'page calls report API');
ok(page.includes('filterCloseRows'), 'page filters selected employee locally');
ok(page.includes('branchId') && page.includes('branchAll'), 'page has branch filter');
ok(controller.includes('Guid? branchId'), 'API accepts branchId');
ok(repo.includes('o.branch_id = @BranchId'), 'sql filters by branch');
ok(page.includes('fetchReportSalesShift'), 'page reopens close sheet via reports.read');
ok(!page.includes('/sales/shifts/'), 'page does not use sales.read GetShift');
ok(shiftPage.includes('openHistorySheet'), 'sales shift history click');
ok(shiftPage.includes('fetchSalesShift'), 'sales history uses existing GetShift');
ok(reportView.includes("definition?.code === 'SALES-03'"), 'SALES-03 rows open sheet');
ok(codes.includes('SalesShiftCloseByEmployee = "SALES-09"'), 'report code');
ok(controller.includes('[HttpGet("sales/shift-close-by-employee")]'), 'list endpoint');
ok(controller.includes('[HttpGet("sales/shifts/{id:guid}")]'), 'reports GetShift');
ok(controller.includes('ReportsPolicies.Read'), 'reports.read gate kept');
ok(repo.includes('GetSalesShiftCloseByEmployeeAsync'), 'sql group by employee × shift');
ok(repo.includes('@EmployeeId IS NULL OR o.employee_id = @EmployeeId'), 'sql always binds employee');
ok(repo.includes('["shiftId"] = r.ShiftId'), 'SALES-03 rows carry shiftId');
ok(auth.includes('reports.read'), 'auth still reports.* not sales.*');
ok(!auth.includes('sales.read'), 'reports policy not opened to sales.read');

if (failed) process.exit(1);
