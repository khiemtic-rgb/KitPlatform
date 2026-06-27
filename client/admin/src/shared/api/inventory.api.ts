import { isAxiosError } from 'axios';
import { http } from '@/shared/api/http';
import type {
  AdjustmentDetail,
  AdjustmentCountEntry,
  AdjustmentCountPreviewLine,
  AdjustmentCountPreview,
  InventoryBarcodeResolve,
  AdjustmentListItem,
  BranchLookup,
  OpeningBalanceBatch,
  OpeningBalanceLine,
  OpeningBalanceResult,
  OpeningBalanceImportResult,
  LowStockProduct,
  LowStockSettings,
  CategoryLowStockSetting,
  WarehouseLowStockSetting,
  PagedStockBatches,
  PagedStockProducts,
  StockBatch,
  StockProductSummary,
  TransferDetail,
  TransferListItem,
  Warehouse,
} from '@/shared/api/inventory.types';

function normalizePaged<T>(data: Record<string, unknown>, mapItem: (row: Record<string, unknown>) => T): {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
} {
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    items: rawItems.map(mapItem),
    total: Number(data.total ?? data.Total ?? 0),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? 20),
  };
}

function normalizeWarehouse(row: Record<string, unknown>): Warehouse {
  return {
    id: String(row.id ?? row.Id),
    branchId: String(row.branchId ?? row.BranchId),
    branchName: String(row.branchName ?? row.BranchName ?? ''),
    warehouseCode: String(row.warehouseCode ?? row.WarehouseCode ?? ''),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    warehouseType: Number(row.warehouseType ?? row.WarehouseType ?? 1),
    isDefault: Boolean(row.isDefault ?? row.IsDefault),
    address: (row.address ?? row.Address) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
  };
}

function normalizeStockProductSummary(row: Record<string, unknown>): StockProductSummary {
  return {
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    saleUnitName: (row.saleUnitName ?? row.SaleUnitName) as string | undefined,
    totalQuantity: Number(row.totalQuantity ?? row.TotalQuantity ?? 0),
    warehouseCount: Number(row.warehouseCount ?? row.WarehouseCount ?? 0),
    batchCount: Number(row.batchCount ?? row.BatchCount ?? 0),
  };
}

function normalizeStockBatch(row: Record<string, unknown>): StockBatch {
  return {
    id: String(row.id ?? row.Id),
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    warehouseCode: String(row.warehouseCode ?? row.WarehouseCode ?? ''),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    saleUnitName: (row.saleUnitName ?? row.SaleUnitName) as string | undefined,
    batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
    expiryDate: (row.expiryDate ?? row.ExpiryDate) as string | undefined,
    unitCost: Number(row.unitCost ?? row.UnitCost ?? 0),
    quantityAvailable: Number(row.quantityAvailable ?? row.QuantityAvailable ?? 0),
    quantityReceived: Number(row.quantityReceived ?? row.QuantityReceived ?? 0),
    status: Number(row.status ?? row.Status ?? 1),
  };
}

function normalizeTransferListItem(row: Record<string, unknown>): TransferListItem {
  return {
    id: String(row.id ?? row.Id),
    transferNumber: String(row.transferNumber ?? row.TransferNumber ?? ''),
    fromWarehouseId: String(row.fromWarehouseId ?? row.FromWarehouseId),
    fromWarehouseName: String(row.fromWarehouseName ?? row.FromWarehouseName ?? ''),
    toWarehouseId: String(row.toWarehouseId ?? row.ToWarehouseId),
    toWarehouseName: String(row.toWarehouseName ?? row.ToWarehouseName ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    transferDate: String(row.transferDate ?? row.TransferDate ?? ''),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
  };
}

function normalizeTransferDetail(data: Record<string, unknown>): TransferDetail {
  const base = normalizeTransferListItem(data);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    ...base,
    notes: (data.notes ?? data.Notes) as string | undefined,
    items: rawItems.map((row) => ({
      id: String(row.id ?? row.Id),
      batchId: String(row.batchId ?? row.BatchId),
      productId: String(row.productId ?? row.ProductId),
      productCode: String(row.productCode ?? row.ProductCode ?? ''),
      productName: String(row.productName ?? row.ProductName ?? ''),
      batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
      quantity: Number(row.quantity ?? row.Quantity ?? 0),
    })),
  };
}

function normalizeAdjustmentListItem(row: Record<string, unknown>): AdjustmentListItem {
  return {
    id: String(row.id ?? row.Id),
    adjustmentNumber: String(row.adjustmentNumber ?? row.AdjustmentNumber ?? ''),
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    adjustmentDate: String(row.adjustmentDate ?? row.AdjustmentDate ?? ''),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
  };
}

function normalizeAdjustmentDetail(data: Record<string, unknown>): AdjustmentDetail {
  const base = normalizeAdjustmentListItem(data);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    ...base,
    reason: (data.reason ?? data.Reason) as string | undefined,
    items: rawItems.map((row) => ({
      id: String(row.id ?? row.Id),
      batchId: String(row.batchId ?? row.BatchId),
      productId: String(row.productId ?? row.ProductId),
      productCode: String(row.productCode ?? row.ProductCode ?? ''),
      productName: String(row.productName ?? row.ProductName ?? ''),
      batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
      systemQuantity: Number(row.systemQuantity ?? row.SystemQuantity ?? 0),
      actualQuantity: Number(row.actualQuantity ?? row.ActualQuantity ?? 0),
      differenceQuantity: Number(row.differenceQuantity ?? row.DifferenceQuantity ?? 0),
      note: (row.note ?? row.Note) as string | undefined,
    })),
  };
}

export async function fetchBranchLookups(): Promise<BranchLookup[]> {
  const { data } = await http.get<BranchLookup[]>('/inventory/warehouses/branches');
  return data.map((row) => ({
    id: String((row as unknown as Record<string, unknown>).id ?? (row as unknown as Record<string, unknown>).Id),
    branchCode: String((row as unknown as Record<string, unknown>).branchCode ?? (row as unknown as Record<string, unknown>).BranchCode ?? ''),
    branchName: String((row as unknown as Record<string, unknown>).branchName ?? (row as unknown as Record<string, unknown>).BranchName ?? ''),
  }));
}

export async function fetchWarehouses(): Promise<Warehouse[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/inventory/warehouses');
  return data.map((row) => normalizeWarehouse(row));
}

export async function createWarehouse(payload: {
  branchId: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseType: number;
  isDefault?: boolean;
  address?: string;
}): Promise<Warehouse> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/warehouses', payload);
  return normalizeWarehouse(data);
}

export async function updateWarehouse(
  id: string,
  payload: {
    warehouseName: string;
    warehouseType: number;
    isDefault?: boolean;
    address?: string;
    status?: number;
  },
): Promise<Warehouse> {
  const { data } = await http.put<Record<string, unknown>>(`/inventory/warehouses/${id}`, payload);
  return normalizeWarehouse(data);
}

export async function deleteWarehouse(id: string): Promise<void> {
  await http.delete(`/inventory/warehouses/${id}`);
}

export async function fetchStockBatches(params: {
  warehouseId?: string;
  productId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedStockBatches> {
  const { data } = await http.get<Record<string, unknown>>('/inventory/stock/batches', { params });
  return normalizePaged(data, normalizeStockBatch);
}

export async function fetchStockProducts(params: {
  warehouseId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedStockProducts> {
  const { data } = await http.get<Record<string, unknown>>('/inventory/stock/products', { params });
  return normalizePaged(data, normalizeStockProductSummary);
}

export async function createOpeningBalance(payload: {
  warehouseId: string;
  notes?: string;
  lines: OpeningBalanceLine[];
}): Promise<OpeningBalanceResult> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/opening-balance', payload);
  return {
    warehouseId: String(data.warehouseId ?? data.WarehouseId),
    linesProcessed: Number(data.linesProcessed ?? data.LinesProcessed ?? 0),
    batchIds: ((data.batchIds ?? data.BatchIds ?? []) as unknown[]).map(String),
  };
}

function normalizeOpeningBalanceBatch(row: Record<string, unknown>): OpeningBalanceBatch {
  return {
    batchId: String(row.batchId ?? row.BatchId),
    warehouseId: String(row.warehouseId ?? row.WarehouseId),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    saleUnitName: (row.saleUnitName ?? row.SaleUnitName) as string | undefined,
    batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
    expiryDate: (row.expiryDate ?? row.ExpiryDate) as string | undefined,
    unitCost: Number(row.unitCost ?? row.UnitCost ?? 0),
    quantityAvailable: Number(row.quantityAvailable ?? row.QuantityAvailable ?? 0),
    openingQuantity: Number(row.openingQuantity ?? row.OpeningQuantity ?? 0),
    firstOpeningDate: String(row.firstOpeningDate ?? row.FirstOpeningDate ?? ''),
    canVoid: Boolean(row.canVoid ?? row.CanVoid),
    voidBlockReason: (row.voidBlockReason ?? row.VoidBlockReason) as string | undefined,
  };
}

export async function fetchOpeningBalanceBatches(warehouseId?: string): Promise<OpeningBalanceBatch[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/inventory/opening-balance/batches', {
    params: warehouseId ? { warehouseId } : undefined,
  });
  return data.map((row) => normalizeOpeningBalanceBatch(row));
}

export async function voidOpeningBalanceBatch(batchId: string): Promise<void> {
  await http.delete(`/inventory/opening-balance/batches/${batchId}`);
}

export async function fetchTransfers(): Promise<TransferListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/inventory/transfers');
  return data.map((row) => normalizeTransferListItem(row));
}

export async function fetchTransfer(id: string): Promise<TransferDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/inventory/transfers/${id}`);
  return normalizeTransferDetail(data);
}

export async function createTransfer(payload: {
  fromWarehouseId: string;
  toWarehouseId: string;
  notes?: string;
  items: { batchId: string; quantity: number }[];
}): Promise<TransferDetail> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/transfers', payload);
  return normalizeTransferDetail(data);
}

export async function completeTransfer(id: string): Promise<TransferDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/inventory/transfers/${id}/complete`);
  return normalizeTransferDetail(data);
}

export async function fetchAdjustments(): Promise<AdjustmentListItem[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/inventory/adjustments');
  return data.map((row) => normalizeAdjustmentListItem(row));
}

export async function fetchAdjustment(id: string): Promise<AdjustmentDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/inventory/adjustments/${id}`);
  return normalizeAdjustmentDetail(data);
}

export async function createAdjustment(payload: {
  warehouseId: string;
  reason?: string;
  items: { batchId: string; actualQuantity: number; note?: string }[];
}): Promise<AdjustmentDetail> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/adjustments', payload);
  return normalizeAdjustmentDetail(data);
}

export async function approveAdjustment(id: string): Promise<AdjustmentDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/inventory/adjustments/${id}/approve`);
  return normalizeAdjustmentDetail(data);
}

function normalizeCountEntry(row: Record<string, unknown>): AdjustmentCountEntry {
  return {
    id: String(row.id ?? row.Id),
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    batchId: (row.batchId ?? row.BatchId) as string | undefined,
    batchNumber: (row.batchNumber ?? row.BatchNumber) as string | undefined,
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    counterUserId: (row.counterUserId ?? row.CounterUserId) as string | undefined,
    counterUserName: (row.counterUserName ?? row.CounterUserName) as string | undefined,
    zone: (row.zone ?? row.Zone) as string | undefined,
    scannedBarcode: (row.scannedBarcode ?? row.ScannedBarcode) as string | undefined,
    note: (row.note ?? row.Note) as string | undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizeCountPreviewLine(row: Record<string, unknown>): AdjustmentCountPreviewLine {
  return {
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    batchId: (row.batchId ?? row.BatchId) as string | undefined,
    batchNumber: (row.batchNumber ?? row.BatchNumber) as string | undefined,
    countedQuantity: Number(row.countedQuantity ?? row.CountedQuantity ?? 0),
    systemQuantity: Number(row.systemQuantity ?? row.SystemQuantity ?? 0),
    differenceQuantity: Number(row.differenceQuantity ?? row.DifferenceQuantity ?? 0),
    entryCount: Number(row.entryCount ?? row.EntryCount ?? 0),
  };
}

export async function createCountingSession(payload: {
  warehouseId: string;
  reason?: string;
}): Promise<AdjustmentDetail> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/adjustments/counting-sessions', payload);
  return normalizeAdjustmentDetail(data);
}

export async function fetchActiveCountingSession(warehouseId: string): Promise<AdjustmentListItem | null> {
  try {
    const { data } = await http.get<Record<string, unknown>>('/inventory/adjustments/active-counting', {
      params: { warehouseId },
    });
    return normalizeAdjustmentListItem(data);
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 404) return null;
    throw error;
  }
}

export async function fetchCountPreview(adjustmentId: string): Promise<AdjustmentCountPreview> {
  const { data } = await http.get<Record<string, unknown>>(`/inventory/adjustments/${adjustmentId}/count-preview`);
  const byBatchRaw = (data.byBatch ?? data.ByBatch ?? []) as Record<string, unknown>[];
  const byProductRaw = (data.byProduct ?? data.ByProduct ?? []) as Record<string, unknown>[];
  return {
    byBatch: byBatchRaw.map((row) => normalizeCountPreviewLine(row)),
    byProduct: byProductRaw.map((row) => normalizeCountPreviewLine(row)),
  };
}

export async function fetchCountEntries(adjustmentId: string): Promise<AdjustmentCountEntry[]> {
  const { data } = await http.get<Record<string, unknown>[]>(`/inventory/adjustments/${adjustmentId}/count-entries`);
  return data.map((row) => normalizeCountEntry(row));
}

export async function addCountEntries(
  adjustmentId: string,
  entries: {
    productId?: string;
    batchId: string;
    quantity: number;
    scannedBarcode?: string;
    zone?: string;
    note?: string;
  }[],
): Promise<AdjustmentCountEntry[]> {
  const { data } = await http.post<Record<string, unknown>[]>(
    `/inventory/adjustments/${adjustmentId}/count-entries`,
    { entries },
  );
  return data.map((row) => normalizeCountEntry(row));
}

export async function deleteCountEntry(adjustmentId: string, entryId: string): Promise<void> {
  await http.delete(`/inventory/adjustments/${adjustmentId}/count-entries/${entryId}`);
}

function normalizeBarcodeResolve(data: Record<string, unknown>): InventoryBarcodeResolve {
  return {
    productId: String(data.productId ?? data.ProductId),
    productCode: String(data.productCode ?? data.ProductCode ?? ''),
    productName: String(data.productName ?? data.ProductName ?? ''),
    saleUnitName: (data.saleUnitName ?? data.SaleUnitName) as string | undefined,
    suggestedBatchId: (data.suggestedBatchId ?? data.SuggestedBatchId) as string | undefined,
    suggestedBatchNumber: (data.suggestedBatchNumber ?? data.SuggestedBatchNumber) as string | undefined,
  };
}

export async function resolveInventoryBarcode(
  warehouseId: string,
  barcode: string,
): Promise<InventoryBarcodeResolve | null> {
  try {
    const { data } = await http.get<Record<string, unknown>>('/inventory/adjustments/resolve-barcode', {
      params: { warehouseId, barcode },
    });
    return normalizeBarcodeResolve(data);
  } catch {
    return null;
  }
}

function normalizeLowStockProduct(row: Record<string, unknown>): LowStockProduct {
  return {
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    saleUnitName: (row.saleUnitName ?? row.SaleUnitName) as string | undefined,
    warehouseId: String(row.warehouseId ?? row.WarehouseId ?? ''),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    branchId: (row.branchId ?? row.BranchId) as string | undefined,
    branchName: (row.branchName ?? row.BranchName) as string | undefined,
    totalQuantity: Number(row.totalQuantity ?? row.TotalQuantity ?? 0),
    minStockQty: Number(row.minStockQty ?? row.MinStockQty ?? 10),
    batchCount: Number(row.batchCount ?? row.BatchCount ?? 0),
  };
}

export async function fetchLowStockProducts(params?: {
  warehouseId?: string;
  defaultThreshold?: number;
}): Promise<LowStockProduct[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/inventory/stock/low-stock', { params });
  return data.map((row) => normalizeLowStockProduct(row));
}

export async function importOpeningBalance(payload: {
  warehouseId: string;
  notes?: string;
  rows: Array<{
    rowNumber: number;
    productKey: string;
    batchNumber: string;
    expiryDate?: string;
    quantity: number;
    unitCost: number;
  }>;
}): Promise<OpeningBalanceImportResult> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/opening-balance/import', payload);
  const errors = ((data.errors ?? data.Errors ?? []) as Record<string, unknown>[]).map((row) => ({
    rowNumber: Number(row.rowNumber ?? row.RowNumber ?? 0),
    message: String(row.message ?? row.Message ?? ''),
  }));
  return {
    linesProcessed: Number(data.linesProcessed ?? data.LinesProcessed ?? 0),
    batchIds: ((data.batchIds ?? data.BatchIds ?? []) as unknown[]).map(String),
    errors,
  };
}

function normalizeCategoryLowStock(row: Record<string, unknown>): CategoryLowStockSetting {
  return {
    id: String(row.id ?? row.Id),
    categoryCode: String(row.categoryCode ?? row.CategoryCode ?? ''),
    categoryName: String(row.categoryName ?? row.CategoryName ?? ''),
    minStockQty: (row.minStockQty ?? row.MinStockQty) as number | undefined,
    productCount: Number(row.productCount ?? row.ProductCount ?? 0),
  };
}

function normalizeWarehouseLowStock(row: Record<string, unknown>): WarehouseLowStockSetting {
  return {
    id: String(row.id ?? row.Id),
    warehouseCode: String(row.warehouseCode ?? row.WarehouseCode ?? ''),
    warehouseName: String(row.warehouseName ?? row.WarehouseName ?? ''),
    branchId: (row.branchId ?? row.BranchId) as string | undefined,
    branchName: (row.branchName ?? row.BranchName) as string | undefined,
    minStockQty: (row.minStockQty ?? row.MinStockQty) as number | undefined,
    isDefault: Boolean(row.isDefault ?? row.IsDefault),
  };
}

export async function fetchLowStockSettings(): Promise<LowStockSettings> {
  const { data } = await http.get<Record<string, unknown>>('/inventory/low-stock/settings');
  const categories = ((data.categories ?? data.Categories ?? []) as Record<string, unknown>[]).map(
    normalizeCategoryLowStock,
  );
  const warehouses = ((data.warehouses ?? data.Warehouses ?? []) as Record<string, unknown>[]).map(
    normalizeWarehouseLowStock,
  );
  return {
    defaultMinStockQty: (data.defaultMinStockQty ?? data.DefaultMinStockQty) as number | undefined,
    systemFallbackQty: Number(data.systemFallbackQty ?? data.SystemFallbackQty ?? 10),
    categories,
    warehouses,
  };
}

export async function updateLowStockDefault(defaultMinStockQty?: number): Promise<number | undefined> {
  const { data } = await http.put<Record<string, unknown>>('/inventory/low-stock/settings/default', {
    defaultMinStockQty: defaultMinStockQty ?? null,
  });
  return (data.defaultMinStockQty ?? data.DefaultMinStockQty) as number | undefined;
}

export async function updateCategoryLowStockSetting(
  categoryId: string,
  minStockQty?: number,
): Promise<CategoryLowStockSetting> {
  const { data } = await http.put<Record<string, unknown>>(
    `/inventory/low-stock/settings/categories/${categoryId}`,
    { minStockQty: minStockQty ?? null },
  );
  return normalizeCategoryLowStock(data);
}

export async function updateWarehouseLowStockSetting(
  warehouseId: string,
  minStockQty?: number,
): Promise<WarehouseLowStockSetting> {
  const { data } = await http.put<Record<string, unknown>>(
    `/inventory/low-stock/settings/warehouses/${warehouseId}`,
    { minStockQty: minStockQty ?? null },
  );
  return normalizeWarehouseLowStock(data);
}

export async function applyLowStockToAll(onlyUnset = true, minStockQty?: number): Promise<number> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/low-stock/settings/apply-all', {
    minStockQty: minStockQty ?? null,
    onlyUnset,
  });
  return Number(data.updatedCount ?? data.UpdatedCount ?? 0);
}

export async function applyLowStockToCategory(
  categoryId: string,
  onlyUnset = true,
  minStockQty?: number,
): Promise<number> {
  const { data } = await http.post<Record<string, unknown>>(
    `/inventory/low-stock/settings/apply-category/${categoryId}`,
    { minStockQty: minStockQty ?? null, onlyUnset },
  );
  return Number(data.updatedCount ?? data.UpdatedCount ?? 0);
}
