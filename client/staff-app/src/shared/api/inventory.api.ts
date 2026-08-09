import { http } from '@/shared/api/http';
import { isAxiosError } from 'axios';
import type {
  AdjustmentCountEntry,
  AdjustmentListFilters,
  AdjustmentListItem,
  InventoryBarcodeResolve,
  PagedAdjustments,
  PagedStockBatches,
  PagedStockProducts,
  StockBatch,
  StockProductSummary,
  TransferDetail,
  TransferListItem,
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

function normalizeStockProductSummary(row: Record<string, unknown>): StockProductSummary {
  return {
    productId: String(row.productId ?? row.ProductId),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    saleUnitName: (row.saleUnitName ?? row.SaleUnitName) as string | undefined,
    totalQuantity: Number(row.totalQuantity ?? row.TotalQuantity ?? 0),
  };
}

function normalizeStockBatch(row: Record<string, unknown>): StockBatch {
  return {
    id: String(row.id ?? row.Id),
    batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
    expiryDate: (row.expiryDate ?? row.ExpiryDate) as string | undefined,
    quantityAvailable: Number(row.quantityAvailable ?? row.QuantityAvailable ?? 0),
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
    hasShortage: Boolean(row.hasShortage ?? row.HasShortage ?? false),
  };
}

function normalizeTransferDetail(data: Record<string, unknown>): TransferDetail {
  const base = normalizeTransferListItem(data);
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    ...base,
    notes: (data.notes ?? data.Notes) as string | undefined,
    receiveNotes: (data.receiveNotes ?? data.ReceiveNotes) as string | undefined,
    shippedAt: (data.shippedAt ?? data.ShippedAt) as string | undefined,
    receivedAt: (data.receivedAt ?? data.ReceivedAt) as string | undefined,
    items: rawItems.map((row) => ({
      id: String(row.id ?? row.Id),
      batchId: String(row.batchId ?? row.BatchId),
      productId: String(row.productId ?? row.ProductId),
      productCode: String(row.productCode ?? row.ProductCode ?? ''),
      productName: String(row.productName ?? row.ProductName ?? ''),
      batchNumber: String(row.batchNumber ?? row.BatchNumber ?? ''),
      quantity: Number(row.quantity ?? row.Quantity ?? 0),
      receivedQuantity:
        row.receivedQuantity == null && row.ReceivedQuantity == null
          ? undefined
          : Number(row.receivedQuantity ?? row.ReceivedQuantity),
    })),
  };
}

export async function fetchStockBatches(params: {
  warehouseId: string;
  productId: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedStockBatches> {
  const { data } = await http.get<Record<string, unknown>>('/inventory/stock/batches', { params });
  return normalizePaged(data, normalizeStockBatch);
}

export async function fetchStockProducts(params: {
  warehouseId: string;
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedStockProducts> {
  const { data } = await http.get<Record<string, unknown>>('/inventory/stock/products', { params });
  return normalizePaged(data, normalizeStockProductSummary);
}

export async function fetchTransfers(filters?: {
  search?: string;
  status?: number;
  fromWarehouseId?: string;
  toWarehouseId?: string;
  dateFrom?: string;
  dateTo?: string;
  hasShortage?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<{ items: TransferListItem[]; total: number; page: number; pageSize: number }> {
  const params: Record<string, string | number | boolean> = {};
  if (filters) {
    for (const [key, value] of Object.entries(filters)) {
      if (value === undefined || value === '' || value === false) continue;
      params[key] = value as string | number | boolean;
    }
  }
  const { data } = await http.get<Record<string, unknown>>('/inventory/transfers', {
    params: Object.keys(params).length > 0 ? params : undefined,
  });
  const rawItems = (data.items ?? data.Items ?? []) as Record<string, unknown>[];
  return {
    items: rawItems.map((row) => normalizeTransferListItem(row)),
    total: Number(data.total ?? data.Total ?? 0),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? rawItems.length),
  };
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

export async function shipTransfer(id: string): Promise<TransferDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/inventory/transfers/${id}/ship`);
  return normalizeTransferDetail(data);
}

export async function receiveTransfer(
  id: string,
  payload?: {
    notes?: string;
    items?: { transferItemId: string; receivedQuantity: number }[];
  },
): Promise<TransferDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/inventory/transfers/${id}/receive`, payload ?? {});
  return normalizeTransferDetail(data);
}

export async function cancelTransfer(id: string): Promise<TransferDetail> {
  const { data } = await http.post<Record<string, unknown>>(`/inventory/transfers/${id}/cancel`);
  return normalizeTransferDetail(data);
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

function normalizeCountEntry(row: Record<string, unknown>): AdjustmentCountEntry {
  return {
    id: String(row.id ?? row.Id),
    productId: (row.productId ?? row.ProductId) as string | undefined,
    productCode: (row.productCode ?? row.ProductCode) as string | undefined,
    productName: (row.productName ?? row.ProductName) as string | undefined,
    batchId: (row.batchId ?? row.BatchId) as string | undefined,
    batchNumber: (row.batchNumber ?? row.BatchNumber) as string | undefined,
    quantity: Number(row.quantity ?? row.Quantity ?? 0),
    zone: (row.zone ?? row.Zone) as string | undefined,
  };
}

export async function fetchAdjustments(filters?: AdjustmentListFilters): Promise<PagedAdjustments> {
  const params: Record<string, string | number> = {};
  if (filters) {
    for (const [key, value] of Object.entries(filters)) {
      if (value === undefined || value === '') continue;
      params[key] = value as string | number;
    }
  }
  const { data } = await http.get<Record<string, unknown>>('/inventory/adjustments', {
    params: Object.keys(params).length > 0 ? params : undefined,
  });
  return normalizePaged(data, normalizeAdjustmentListItem);
}

export async function fetchAdjustment(id: string): Promise<AdjustmentListItem> {
  const { data } = await http.get<Record<string, unknown>>(`/inventory/adjustments/${id}`);
  return normalizeAdjustmentListItem(data);
}

export async function createCountingSession(payload: {
  warehouseId: string;
  reason?: string;
}): Promise<AdjustmentListItem> {
  const { data } = await http.post<Record<string, unknown>>('/inventory/adjustments/counting-sessions', payload);
  return normalizeAdjustmentListItem(data);
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

export async function fetchCountEntries(adjustmentId: string): Promise<AdjustmentCountEntry[]> {
  const { data } = await http.get<Record<string, unknown>[]>(`/inventory/adjustments/${adjustmentId}/count-entries`);
  return data.map((row) => normalizeCountEntry(row));
}

export async function addCountEntries(
  adjustmentId: string,
  entries: { batchId: string; quantity: number; zone?: string; scannedBarcode?: string }[],
): Promise<AdjustmentCountEntry[]> {
  const { data } = await http.post<Record<string, unknown>[]>(
    `/inventory/adjustments/${adjustmentId}/count-entries`,
    { entries },
  );
  return data.map((row) => normalizeCountEntry(row));
}

export async function approveAdjustment(adjustmentId: string): Promise<void> {
  await http.post(`/inventory/adjustments/${adjustmentId}/approve`);
}

export async function resolveInventoryBarcode(
  warehouseId: string,
  barcode: string,
): Promise<InventoryBarcodeResolve> {
  const { data } = await http.get<Record<string, unknown>>('/inventory/adjustments/resolve-barcode', {
    params: { warehouseId, barcode },
  });
  return {
    productId: String(data.productId ?? data.ProductId),
    productCode: String(data.productCode ?? data.ProductCode ?? ''),
    productName: String(data.productName ?? data.ProductName ?? ''),
    saleUnitName: (data.saleUnitName ?? data.SaleUnitName) as string | undefined,
    suggestedBatchId: (data.suggestedBatchId ?? data.SuggestedBatchId) as string | undefined,
    suggestedBatchNumber: (data.suggestedBatchNumber ?? data.SuggestedBatchNumber) as string | undefined,
  };
}
