import type {
  GoodsReceiptDetailDto,
  GoodsReceiptItemDto,
  GoodsReceiptListItemDto,
  LastPurchasePriceHintDto,
  PurchaseOrderDetailDto,
  PurchaseOrderItemDto,
  PurchaseOrderListItemDto,
  Req,
  SupplierDto,
  SupplierPaymentListItemDto,
} from '@/shared/api/generated';

export const PO_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ duyệt',
  2: 'Đơn mới',
  3: 'Nhận một phần',
  4: 'Đã nhận đủ',
  5: 'Đóng',
  6: 'Đã hủy',
};

/** Màu Tag trạng thái PO trên danh sách */
export const PO_STATUS_TAG: Record<number, string> = {
  1: 'default',
  2: 'blue',
  3: 'orange',
  4: 'green',
  5: 'purple',
  6: 'red',
};

/** PO có thể sửa SL / thêm dòng (Chờ duyệt, Đơn mới, Nhận một phần). */
export function canEditPurchaseOrder(status: number): boolean {
  return status === 1 || status === 2 || status === 3;
}

export const GRN_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ nhập kho',
  2: 'Hoàn tất',
  3: 'Đã hủy',
};

/** Màu Tag trạng thái phiếu nhập trên danh sách */
export const GRN_STATUS_TAG: Record<number, string> = {
  1: 'blue',
  2: 'green',
  3: 'red',
};

export const SUPPLIER_STATUS_LABELS: Record<number, string> = {
  1: 'Hoạt động',
  2: 'Ngừng',
};

export const PAYMENT_METHOD_LABELS: Record<number, string> = {
  1: 'Tiền mặt',
  2: 'Chuyển khoản',
  3: 'Khác',
};

export const SUPPLIER_PAYMENT_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ ghi sổ',
  2: 'Đã ghi sổ',
  3: 'Đã hủy',
};

/** Màu Tag trạng thái phiếu thanh toán NCC */
export const SUPPLIER_PAYMENT_STATUS_TAG: Record<number, string> = {
  1: 'blue',
  2: 'green',
  3: 'red',
};

export interface SupplierPaymentListFilters {
  search?: string;
  supplierId?: string;
  status?: number;
  dateFrom?: string;
  dateTo?: string;
}

export type SupplierPayablesAging = {
  current: number;
  days31To60: number;
  days61To90: number;
  over90: number;
};

export type SupplierPayablesRow = {
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  paymentTerms: number;
  totalPayable: number;
  unappliedCredit: number;
  aging: SupplierPayablesAging;
  openDocumentCount: number;
};

export type SupplierPayablesDetailLine = {
  goodsReceiptId: string;
  grnNumber: string;
  receiptDate: string;
  grnTotal: number;
  paidAmount: number;
  outstanding: number;
  daysOutstanding: number;
};

export type SupplierPayablesDetail = {
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  paymentTerms: number;
  totalPayable: number;
  unappliedCredit: number;
  aging: SupplierPayablesAging;
  lines: SupplierPayablesDetailLine[];
};

export type ProcurementVatTreatment = {
  id: string;
  treatmentCode: string;
  treatmentName: string;
  ratePercent: number;
  isNotSubject: boolean;
  sortOrder: number;
  isActive: boolean;
  canDelete: boolean;
};

export type LastPurchasePriceHint = Pick<
  LastPurchasePriceHintDto,
  'unitPrice' | 'priceDate' | 'source' | 'documentNumber'
>;

export type Supplier = Req<
  SupplierDto,
  'id' | 'supplierCode' | 'supplierName' | 'paymentTerms' | 'status' | 'isPlaceholder'
> & {
  wholesaleFacilityCode?: string;
};

export type PurchaseOrderListItem = Req<
  PurchaseOrderListItemDto,
  | 'id'
  | 'poNumber'
  | 'supplierId'
  | 'supplierName'
  | 'warehouseId'
  | 'warehouseName'
  | 'status'
  | 'orderDate'
  | 'totalAmount'
  | 'itemCount'
>;

export type PurchaseOrderItem = Req<
  PurchaseOrderItemDto,
  | 'id'
  | 'productId'
  | 'productCode'
  | 'productName'
  | 'productUnitId'
  | 'unitName'
  | 'orderedQty'
  | 'receivedQty'
  | 'unitPrice'
  | 'lineTotal'
>;

export type PurchaseOrderDetail = Omit<
  Req<
    PurchaseOrderDetailDto,
    | 'id'
    | 'poNumber'
    | 'supplierId'
    | 'supplierName'
    | 'warehouseId'
    | 'warehouseName'
    | 'status'
    | 'orderDate'
    | 'subtotal'
    | 'taxAmount'
    | 'totalAmount'
  >,
  'items'
> & {
  items: PurchaseOrderItem[];
  itemCount?: number;
  vatTreatmentId: string;
  vatTreatmentCode: string;
  vatTreatmentName: string;
  vatIsNotSubject: boolean;
  taxRatePercent?: number;
};

export type GoodsReceiptListItem = Req<
  GoodsReceiptListItemDto,
  | 'id'
  | 'grnNumber'
  | 'supplierId'
  | 'supplierName'
  | 'warehouseId'
  | 'warehouseName'
  | 'status'
  | 'receiptDate'
  | 'itemCount'
> & {
  purchaseOrderId?: string;
  poNumber?: string;
};

export type GoodsReceiptItem = Req<
  GoodsReceiptItemDto,
  | 'id'
  | 'productId'
  | 'productCode'
  | 'productName'
  | 'productUnitId'
  | 'unitName'
  | 'batchNumber'
  | 'expiryDate'
  | 'quantity'
  | 'unitCost'
  | 'lineTotal'
> & {
  purchaseOrderItemId?: string;
  manufactureDate?: string;
  discountType?: number;
  discountValue?: number;
  discountAmount?: number;
  inventoryUnitCost?: number;
};

export type GoodsReceiptDetail = Omit<
  Req<
    GoodsReceiptDetailDto,
    | 'id'
    | 'grnNumber'
    | 'supplierId'
    | 'supplierName'
    | 'warehouseId'
    | 'warehouseName'
    | 'status'
    | 'receiptDate'
    | 'subtotalGross'
    | 'lineDiscountTotal'
    | 'merchandiseNet'
    | 'orderDiscountValue'
    | 'orderDiscountAmount'
    | 'taxRatePercent'
    | 'taxAmount'
    | 'totalAmount'
  >,
  'items'
> & {
  items: GoodsReceiptItem[];
  itemCount?: number;
  purchaseOrderId?: string;
  poNumber?: string;
  notes?: string;
  supplierInvoiceNumber?: string;
  orderDiscountType?: number;
  vatTreatmentId?: string;
  vatTreatmentCode?: string;
  vatTreatmentName?: string;
  vatIsNotSubject?: boolean;
};

export interface PurchaseOrderListFilters {
  search?: string;
  supplierId?: string;
  warehouseId?: string;
  status?: number;
  dateFrom?: string;
  dateTo?: string;
  productId?: string;
  pendingReceiptOnly?: boolean;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface GoodsReceiptListFilters {
  search?: string;
  supplierId?: string;
  warehouseId?: string;
  status?: number;
  dateFrom?: string;
  dateTo?: string;
  purchaseOrderId?: string;
  productId?: string;
  includeArchived?: boolean;
  page?: number;
  pageSize?: number;
}

export interface PagedListResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export type SupplierPaymentListItem = Req<
  SupplierPaymentListItemDto,
  | 'id'
  | 'paymentNumber'
  | 'supplierId'
  | 'supplierName'
  | 'amount'
  | 'paymentMethod'
  | 'status'
  | 'paymentDate'
>;
