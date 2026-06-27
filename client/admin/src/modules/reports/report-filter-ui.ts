import type { Supplier } from '@/shared/api/procurement.types';
import type { Warehouse } from '@/shared/api/inventory.types';
import type { ReportDefinition } from '@/modules/reports/reports-catalog';

export function filterHintsForReport(definition: ReportDefinition): string[] {
  const hints: string[] = [];
  if (definition.supportsDateRange) hints.push('Kỳ');
  if (definition.supportsGroupBy?.length) hints.push('Nhóm theo');
  if (definition.supportsWarehouse) hints.push('Kho');
  if (definition.supportsSupplier) hints.push('Nhà cung cấp');
  if (definition.supportsSearch) hints.push('Tên sản phẩm');
  if (definition.supportsExpiryDays) hints.push('Số ngày HSD');
  return hints;
}

function filterLabelKey(key: string): string {
  if (key === 'Tìm kiếm') return 'Tên sản phẩm';
  if (key === 'NCC') return 'Nhà cung cấp';
  return key;
}

function resolveFilterValue(
  key: string,
  value: string,
  warehouses: Warehouse[],
  suppliers: Supplier[],
  productSearchLabel?: string,
): string {
  if (key === 'Kho') {
    return warehouses.find((w) => w.id === value)?.warehouseName ?? value;
  }
  if (key === 'NCC') {
    const supplier = suppliers.find((s) => s.id === value);
    return supplier ? `${supplier.supplierCode} — ${supplier.supplierName}` : value;
  }
  if (key === 'Tìm kiếm') {
    return productSearchLabel || value;
  }
  return value;
}

export function buildReportFilterDisplayEntries(
  definition: ReportDefinition,
  filterLabels: Record<string, string>,
  warehouses: Warehouse[],
  suppliers: Supplier[],
  productSearchLabel?: string,
): Array<{ key: string; value: string }> {
  const entries = Object.entries(filterLabels).map(([key, value]) => ({
    key: filterLabelKey(key),
    value: resolveFilterValue(key, value, warehouses, suppliers, productSearchLabel),
  }));

  if (definition.supportsWarehouse && !filterLabels.Kho) {
    const insertAt = entries.findIndex((e) => e.key === 'Thời điểm') + 1 || entries.length;
    entries.splice(insertAt === 0 ? entries.length : insertAt, 0, { key: 'Kho', value: 'Tất cả kho' });
  }

  if (definition.supportsSupplier && !filterLabels.NCC) {
    const khoIndex = entries.findIndex((e) => e.key === 'Kho');
    const insertAt = khoIndex >= 0 ? khoIndex + 1 : entries.length;
    entries.splice(insertAt, 0, { key: 'Nhà cung cấp', value: 'Tất cả NCC' });
  }

  return entries;
}
