export const PO_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
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

/** PO có thể sửa SL / thêm dòng (Nháp, Đơn mới, Nhận một phần). */
export function canEditPurchaseOrder(status: number): boolean {
  return status === 1 || status === 2 || status === 3;
}

export const GRN_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
  2: 'Hoàn tất',
  3: 'Đã hủy',
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
  1: 'Nháp',
  2: 'Đã ghi sổ',
  3: 'Đã hủy',
};

export interface SupplierPaymentListFilters {
  search?: string;
  supplierId?: string;
  status?: number;
  dateFrom?: string;
  dateTo?: string;
}

export interface LastPurchasePriceHint {
  unitPrice?: number;
  priceDate?: string;
  source?: string;
  documentNumber?: string;
}

export interface Supplier {
  id: string;
  supplierCode: string;
  supplierName: string;
  taxCode?: string;
  contactName?: string;
  phone?: string;
  email?: string;
  address?: string;
  paymentTerms: number;
  status: number;
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
  deletedAt?: string;
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
  lineTotal: number;
}

export interface PurchaseOrderDetail extends PurchaseOrderListItem {
  expectedDate?: string;
  subtotal: number;
  taxAmount: number;
  notes?: string;
  items: PurchaseOrderItem[];
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
  deletedAt?: string;
}

export interface GoodsReceiptItem {
  id: string;
  purchaseOrderItemId?: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  batchNumber: string;
  manufactureDate?: string;
  expiryDate: string;
  quantity: number;
  unitCost: number;
  lineTotal: number;
}

export interface GoodsReceiptDetail extends GoodsReceiptListItem {
  notes?: string;
  items: GoodsReceiptItem[];
}

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
}

export interface SupplierPaymentListItem {
  id: string;
  paymentNumber: string;
  supplierId: string;
  supplierName: string;
  amount: number;
  paymentMethod: number;
  status: number;
  paymentDate: string;
  postedAt?: string;
  purchaseOrderId?: string;
  poNumber?: string;
  goodsReceiptId?: string;
  grnNumber?: string;
  notes?: string;
}
