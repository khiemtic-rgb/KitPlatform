import type {
  AdjustmentDetailDto,
  AdjustmentItemDto,
  AdjustmentListItemDto,
  BranchLookupDto,
  OpeningBalanceBatchListItemDto,
  OpeningBalanceLineRequest,
  OpeningBalanceResultDto,
  PagedStockBatchesResult,
  PagedStockProductsResult,
  Req,
  StockBatchListItemDto,
  StockProductSummaryDto,
  TransferDetailDto,
  TransferItemDto,
  TransferListItemDto,
  WarehouseDto,
} from '@/shared/api/generated';

export type StockBatch = Req<
  StockBatchListItemDto,
  | 'id'
  | 'warehouseId'
  | 'warehouseCode'
  | 'warehouseName'
  | 'productId'
  | 'productCode'
  | 'productName'
  | 'batchNumber'
  | 'unitCost'
  | 'quantityAvailable'
  | 'quantityReceived'
  | 'status'
> & Pick<StockBatchListItemDto, 'saleUnitName'>;

export type PagedStockBatches = Omit<Req<PagedStockBatchesResult, 'total' | 'page' | 'pageSize'>, 'items'> & {
  items: StockBatch[];
};

export type StockProductSummary = Req<
  StockProductSummaryDto,
  'productId' | 'productCode' | 'productName' | 'totalQuantity' | 'warehouseCount' | 'batchCount'
> & Pick<StockProductSummaryDto, 'saleUnitName'>;

export type PagedStockProducts = Omit<Req<PagedStockProductsResult, 'total' | 'page' | 'pageSize'>, 'items'> & {
  items: StockProductSummary[];
};

export type Warehouse = Req<
  WarehouseDto,
  'id' | 'branchId' | 'branchName' | 'warehouseCode' | 'warehouseName' | 'warehouseType' | 'isDefault' | 'status'
>;

export type BranchLookup = Req<BranchLookupDto, 'id' | 'branchCode' | 'branchName'>;

export type OpeningBalanceLine = Req<
  OpeningBalanceLineRequest,
  'productId' | 'batchNumber' | 'unitCost' | 'quantity'
>;

export type OpeningBalanceResult = Req<OpeningBalanceResultDto, 'warehouseId' | 'linesProcessed'> & {
  batchIds: string[];
};

export type OpeningBalanceBatch = Req<
  OpeningBalanceBatchListItemDto,
  | 'batchId'
  | 'warehouseId'
  | 'warehouseName'
  | 'productId'
  | 'productCode'
  | 'productName'
  | 'batchNumber'
  | 'unitCost'
  | 'quantityAvailable'
  | 'openingQuantity'
  | 'firstOpeningDate'
  | 'canVoid'
> & Pick<OpeningBalanceBatchListItemDto, 'saleUnitName'>;

export type TransferListItem = Req<
  TransferListItemDto,
  | 'id'
  | 'transferNumber'
  | 'fromWarehouseId'
  | 'fromWarehouseName'
  | 'toWarehouseId'
  | 'toWarehouseName'
  | 'status'
  | 'transferDate'
  | 'itemCount'
>;

export type TransferItem = Req<
  TransferItemDto,
  'id' | 'batchId' | 'productId' | 'productCode' | 'productName' | 'batchNumber' | 'quantity'
>;

export type TransferDetail = Omit<
  Req<
    TransferDetailDto,
    | 'id'
    | 'transferNumber'
    | 'fromWarehouseId'
    | 'fromWarehouseName'
    | 'toWarehouseId'
    | 'toWarehouseName'
    | 'status'
    | 'transferDate'
  >,
  'items'
> & {
  items: TransferItem[];
  itemCount?: number;
};

export type AdjustmentListItem = Req<
  AdjustmentListItemDto,
  'id' | 'adjustmentNumber' | 'warehouseId' | 'warehouseName' | 'status' | 'adjustmentDate' | 'itemCount'
>;

export type AdjustmentItem = Req<
  AdjustmentItemDto,
  | 'id'
  | 'batchId'
  | 'productId'
  | 'productCode'
  | 'productName'
  | 'batchNumber'
  | 'systemQuantity'
  | 'actualQuantity'
  | 'differenceQuantity'
>;

export type AdjustmentDetail = Omit<
  Req<
    AdjustmentDetailDto,
    'id' | 'adjustmentNumber' | 'warehouseId' | 'warehouseName' | 'status' | 'adjustmentDate'
  >,
  'items'
> & {
  items: AdjustmentItem[];
  itemCount?: number;
};

export type AdjustmentCountEntry = {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  batchId?: string;
  batchNumber?: string;
  quantity: number;
  counterUserId?: string;
  counterUserName?: string;
  zone?: string;
  scannedBarcode?: string;
  note?: string;
  createdAt: string;
};

export type AdjustmentCountPreviewLine = {
  productId: string;
  productCode: string;
  productName: string;
  batchId?: string;
  batchNumber?: string;
  countedQuantity: number;
  systemQuantity: number;
  differenceQuantity: number;
  entryCount: number;
};

export type AdjustmentCountPreview = {
  byBatch: AdjustmentCountPreviewLine[];
  byProduct: AdjustmentCountPreviewLine[];
};

export type InventoryBarcodeResolve = {
  productId: string;
  productCode: string;
  productName: string;
  saleUnitName?: string;
  suggestedBatchId?: string;
  suggestedBatchNumber?: string;
};

export type LowStockProduct = {
  productId: string;
  productCode: string;
  productName: string;
  saleUnitName?: string;
  warehouseId: string;
  warehouseName: string;
  branchId?: string;
  branchName?: string;
  totalQuantity: number;
  minStockQty: number;
  batchCount: number;
};

export type OpeningBalanceImportError = { rowNumber: number; message: string };

export type OpeningBalanceImportResult = {
  linesProcessed: number;
  batchIds: string[];
  errors: OpeningBalanceImportError[];
};

export type CategoryLowStockSetting = {
  id: string;
  categoryCode: string;
  categoryName: string;
  minStockQty?: number;
  productCount: number;
};

export type WarehouseLowStockSetting = {
  id: string;
  warehouseCode: string;
  warehouseName: string;
  branchId?: string;
  branchName?: string;
  minStockQty?: number;
  isDefault: boolean;
};

export type LowStockSettings = {
  defaultMinStockQty?: number;
  systemFallbackQty: number;
  categories: CategoryLowStockSetting[];
  warehouses: WarehouseLowStockSetting[];
};

export const WAREHOUSE_TYPE_LABELS: Record<number, string> = {
  1: 'Kho chính',
  2: 'Kho bán lẻ',
  3: 'Kho thuốc kê đơn',
  4: 'Kho lạnh',
  5: 'Kho trả hàng',
};

export const TRANSFER_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ hoàn tất',
  2: 'Đang chuyển',
  3: 'Hoàn tất',
  4: 'Đã hủy',
};

export const ADJUSTMENT_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ duyệt',
  2: 'Đang kiểm',
  3: 'Đã duyệt',
  4: 'Đã hủy',
};

export const STATUS_LABELS: Record<number, string> = {
  1: 'Hoạt động',
  2: 'Ngừng',
};
