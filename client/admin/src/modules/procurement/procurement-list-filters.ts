import type {
  GoodsReceiptListFilters,
  GoodsReceiptListItem,
  PurchaseOrderListFilters,
  PurchaseOrderListItem,
} from '@/shared/api/procurement.types';

function includesSearch(value: string, query: string): boolean {
  return value.toLowerCase().includes(query);
}

export function filterPurchaseOrdersClient(
  items: PurchaseOrderListItem[],
  filters: PurchaseOrderListFilters,
  search: string,
): PurchaseOrderListItem[] {
  let result = items;
  const q = search.trim().toLowerCase();

  if (q) {
    result = result.filter(
      (row) =>
        includesSearch(row.poNumber, q) ||
        includesSearch(row.supplierName, q),
    );
  }

  if (filters.status != null) {
    result = result.filter((row) => row.status === filters.status);
  }

  if (filters.supplierId) {
    result = result.filter((row) => row.supplierId === filters.supplierId);
  }

  if (filters.warehouseId) {
    result = result.filter((row) => row.warehouseId === filters.warehouseId);
  }

  if (filters.dateFrom) {
    result = result.filter((row) => row.orderDate.slice(0, 10) >= filters.dateFrom!);
  }

  if (filters.dateTo) {
    result = result.filter((row) => row.orderDate.slice(0, 10) <= filters.dateTo!);
  }

  if (filters.pendingReceiptOnly) {
    result = result.filter((row) => row.status === 2 || row.status === 3);
  }

  return result;
}

export function filterGoodsReceiptsClient(
  items: GoodsReceiptListItem[],
  filters: GoodsReceiptListFilters,
  search: string,
): GoodsReceiptListItem[] {
  let result = items;
  const q = search.trim().toLowerCase();

  if (q) {
    result = result.filter(
      (row) =>
        includesSearch(row.grnNumber, q) ||
        includesSearch(row.supplierName, q) ||
        includesSearch(row.poNumber ?? '', q),
    );
  }

  if (filters.status != null) {
    result = result.filter((row) => row.status === filters.status);
  }

  if (filters.supplierId) {
    result = result.filter((row) => row.supplierId === filters.supplierId);
  }

  if (filters.warehouseId) {
    result = result.filter((row) => row.warehouseId === filters.warehouseId);
  }

  if (filters.dateFrom) {
    result = result.filter((row) => row.receiptDate.slice(0, 10) >= filters.dateFrom!);
  }

  if (filters.dateTo) {
    result = result.filter((row) => row.receiptDate.slice(0, 10) <= filters.dateTo!);
  }

  if (filters.purchaseOrderId) {
    result = result.filter((row) => row.purchaseOrderId === filters.purchaseOrderId);
  }

  return result;
}

export function filterSuppliersById<T extends { id: string }>(items: T[], supplierId?: string): T[] {
  if (!supplierId) return items;
  return items.filter((row) => row.id === supplierId);
}
