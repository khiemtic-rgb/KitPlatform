export const GRN_STATUS = {
  Pending: 1,
  Completed: 2,
  Cancelled: 3,
} as const;

export const GRN_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ nhập kho',
  2: 'Hoàn tất',
  3: 'Đã hủy',
};

export const GRN_STATUS_TAG: Record<number, string> = {
  1: 'blue',
  2: 'green',
  3: 'red',
};

export interface Supplier {
  id: string;
  supplierCode: string;
  supplierName: string;
  paymentTerms: number;
  status: number;
  isPlaceholder: boolean;
}

export interface ProcurementVatTreatment {
  id: string;
  treatmentCode: string;
  treatmentName: string;
  ratePercent: number;
  isNotSubject: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface GoodsReceiptListItem {
  id: string;
  grnNumber: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  purchaseOrderId?: string;
  poNumber?: string;
  status: number;
  receiptDate: string;
  itemCount: number;
  totalAmount?: number;
}

export interface GoodsReceiptItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  batchNumber: string;
  expiryDate: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
}

export interface GoodsReceiptDetail extends GoodsReceiptListItem {
  notes?: string;
  totalAmount: number;
  items: GoodsReceiptItem[];
}

export interface PurchaseOrderListItem {
  id: string;
  poNumber: string;
  supplierId: string;
  supplierName: string;
  warehouseId: string;
  warehouseName: string;
  status: number;
  orderDate: string;
  totalAmount: number;
  itemCount: number;
}

export interface PurchaseOrderItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  orderedQty: number;
  receivedQty: number;
  unitPrice: number;
}

export interface PurchaseOrderDetail extends PurchaseOrderListItem {
  vatTreatmentId: string;
  items: PurchaseOrderItem[];
}

export interface GoodsReceiptListFilters {
  search?: string;
  supplierId?: string;
  warehouseId?: string;
  status?: number;
  page?: number;
  pageSize?: number;
}

export interface PagedListResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
