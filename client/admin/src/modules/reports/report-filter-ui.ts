import type { Supplier } from '@/shared/api/procurement.types';
import type { Warehouse } from '@/shared/api/inventory.types';
import { reportsT } from '@/shared/i18n';
import type { ReportDefinition } from '@/modules/reports/reports-catalog';

const API_FILTER_KEY_MAP: Record<string, 'period' | 'warehouse' | 'supplier' | 'productSearch'> = {
  'Thời điểm': 'period',
  Kho: 'warehouse',
  NCC: 'supplier',
  'Nhà cung cấp': 'supplier',
  'Tìm kiếm': 'productSearch',
};

function localizeFilterKey(key: string): string {
  const canonical = API_FILTER_KEY_MAP[key];
  if (canonical) return reportsT()(`filters.${canonical}`);
  return key;
}

export function filterHintsForReport(definition: ReportDefinition): string[] {
  const t = reportsT();
  const hints: string[] = [];
  if (definition.supportsDateRange) hints.push(t('filters.period'));
  if (definition.supportsGroupBy?.length) hints.push(t('filters.groupBy'));
  if (definition.supportsWarehouse) hints.push(t('filters.warehouse'));
  if (definition.supportsSupplier) hints.push(t('filters.supplier'));
  if (definition.supportsSearch) hints.push(t('filters.productSearch'));
  if (definition.supportsExpiryDays) hints.push(t('filters.expiryDays'));
  return hints;
}

function resolveFilterValue(
  key: string,
  value: string,
  warehouses: Warehouse[],
  suppliers: Supplier[],
  productSearchLabel?: string,
): string {
  const canonical = API_FILTER_KEY_MAP[key];
  if (canonical === 'warehouse' || key === 'Kho') {
    return warehouses.find((w) => w.id === value)?.warehouseName ?? value;
  }
  if (canonical === 'supplier' || key === 'NCC') {
    const supplier = suppliers.find((s) => s.id === value);
    return supplier ? `${supplier.supplierCode} — ${supplier.supplierName}` : value;
  }
  if (canonical === 'productSearch' || key === 'Tìm kiếm') {
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
  const t = reportsT();
  const entries = Object.entries(filterLabels).map(([key, value]) => ({
    key: localizeFilterKey(key),
    value: resolveFilterValue(key, value, warehouses, suppliers, productSearchLabel),
  }));

  const warehouseLabel = t('filters.warehouse');
  const supplierLabel = t('filters.supplier');

  if (definition.supportsWarehouse && !filterLabels.Kho) {
    const insertAt = entries.findIndex((e) => e.key === t('filters.period')) + 1 || entries.length;
    entries.splice(insertAt === 0 ? entries.length : insertAt, 0, {
      key: warehouseLabel,
      value: t('filters.warehouseAll'),
    });
  }

  if (definition.supportsSupplier && !filterLabels.NCC) {
    const khoIndex = entries.findIndex((e) => e.key === warehouseLabel);
    const insertAt = khoIndex >= 0 ? khoIndex + 1 : entries.length;
    entries.splice(insertAt, 0, {
      key: supplierLabel,
      value: t('filters.supplierAll'),
    });
  }

  return entries;
}
