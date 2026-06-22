export interface PagedStockBatches {
  items: StockBatch[];
  total: number;
  page: number;
  pageSize: number;
}

export interface StockProductSummary {
  productId: string;
  productCode: string;
  productName: string;
  totalQuantity: number;
  warehouseCount: number;
  batchCount: number;
}

export interface PagedStockProducts {
  items: StockProductSummary[];
  total: number;
  page: number;
  pageSize: number;
}

export interface StockBatch {
  id: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  productId: string;
  productCode: string;
  productName: string;
  batchNumber: string;
  expiryDate?: string;
  unitCost: number;
  quantityAvailable: number;
  quantityReceived: number;
  status: number;
}

export interface Warehouse {
  id: string;
  branchId: string;
  branchName: string;
  warehouseCode: string;
  warehouseName: string;
  warehouseType: number;
  isDefault: boolean;
  address?: string;
  status: number;
}

export interface BranchLookup {
  id: string;
  branchCode: string;
  branchName: string;
}

export interface OpeningBalanceLine {
  productId: string;
  batchNumber: string;
  expiryDate?: string;
  manufactureDate?: string;
  unitCost: number;
  quantity: number;
}

export interface OpeningBalanceResult {
  warehouseId: string;
  linesProcessed: number;
  batchIds: string[];
}

export interface OpeningBalanceBatch {
  batchId: string;
  warehouseId: string;
  warehouseName: string;
  productId: string;
  productCode: string;
  productName: string;
  batchNumber: string;
  expiryDate?: string;
  unitCost: number;
  quantityAvailable: number;
  openingQuantity: number;
  firstOpeningDate: string;
  canVoid: boolean;
  voidBlockReason?: string;
}

export interface TransferListItem {
  id: string;
  transferNumber: string;
  fromWarehouseId: string;
  fromWarehouseName: string;
  toWarehouseId: string;
  toWarehouseName: string;
  status: number;
  transferDate: string;
  itemCount: number;
}

export interface TransferItem {
  id: string;
  batchId: string;
  productId: string;
  productCode: string;
  productName: string;
  batchNumber: string;
  quantity: number;
}

export interface TransferDetail extends TransferListItem {
  notes?: string;
  items: TransferItem[];
}

export interface AdjustmentListItem {
  id: string;
  adjustmentNumber: string;
  warehouseId: string;
  warehouseName: string;
  status: number;
  adjustmentDate: string;
  itemCount: number;
}

export interface AdjustmentItem {
  id: string;
  batchId: string;
  productId: string;
  productCode: string;
  productName: string;
  batchNumber: string;
  systemQuantity: number;
  actualQuantity: number;
  differenceQuantity: number;
  note?: string;
}

export interface AdjustmentDetail extends AdjustmentListItem {
  reason?: string;
  items: AdjustmentItem[];
}

export const WAREHOUSE_TYPE_LABELS: Record<number, string> = {
  1: 'Kho chính',
  2: 'Kho bán lẻ',
  3: 'Kho thuốc kê đơn',
  4: 'Kho lạnh',
  5: 'Kho trả hàng',
};

export const TRANSFER_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
  2: 'Chờ xử lý',
  3: 'Hoàn tất',
  4: 'Đã hủy',
};

export const ADJUSTMENT_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
  2: 'Đang kiểm',
  3: 'Đã duyệt',
  4: 'Đã hủy',
};

export const STATUS_LABELS: Record<number, string> = {
  1: 'Hoạt động',
  2: 'Ngừng',
};
