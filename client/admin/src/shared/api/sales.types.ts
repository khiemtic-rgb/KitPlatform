export interface CustomerListItem {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  email?: string;
}

export interface PosBatchHint {
  batchId: string;
  batchNumber: string;
  expiryDate?: string;
  quantityAvailable: number;
  isSuggested: boolean;
}

export interface PosProductLookup {
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  conversionFactor: number;
  unitPrice: number;
  stockAvailable: number;
  batchHints?: PosBatchHint[];
  stockSourceLabel?: string;
}

export interface PosBatchAllocationPreview {
  batchId: string;
  batchNumber: string;
  expiryDate?: string;
  quantity: number;
  bookQuantityAvailable: number;
}

export interface PosAllocationPreviewLine {
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  requestedQuantity: number;
  allocations: PosBatchAllocationPreview[];
}

export interface PosAllocationPreview {
  lines: PosAllocationPreviewLine[];
  stockSourceLabel: string;
}

export interface SalesOrderListItem {
  id: string;
  orderNumber: string;
  warehouseId: string;
  warehouseName: string;
  customerId?: string;
  customerName?: string;
  status: number;
  orderDate: string;
  totalAmount: number;
  itemCount: number;
  totalRefunded?: number;
  salesShiftId?: string;
  shiftNumber?: string;
}

export interface SalesPaymentLine {
  id?: string;
  paymentMethod: number;
  amount: number;
  paidAt?: string;
}

export interface SalesOrderItem {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  batchId?: string;
  batchNumber?: string;
  expiryDate?: string;
  quantity: number;
  unitPrice: number;
  discountAmount?: number;
  discountType?: number;
  discountValue?: number;
  lineTotal: number;
  returnedQuantity?: number;
}

export interface SalesReturnItem {
  id: string;
  salesOrderItemId: string;
  productCode: string;
  productName: string;
  batchNumber: string;
  quantity: number;
  refundAmount: number;
}

export interface SalesReturnListItem {
  id: string;
  returnNumber: string;
  salesOrderId: string;
  orderNumber: string;
  returnDate: string;
  status: number;
  totalRefund: number;
  salesShiftId?: string;
  shiftNumber?: string;
}

export interface SalesReturnDetail {
  id: string;
  returnNumber: string;
  salesOrderId: string;
  orderNumber: string;
  returnDate: string;
  status: number;
  reason?: string;
  totalRefund: number;
  items: SalesReturnItem[];
  payments?: SalesPaymentLine[];
  salesShiftId?: string;
  shiftNumber?: string;
}

export interface ShiftLotComplianceAlert {
  productId: string;
  productCode: string;
  productName: string;
  soldBatchNumber: string;
  soldExpiryDate?: string;
  earlierBatchNumber: string;
  earlierExpiryDate?: string;
  earlierBookQuantity: number;
  stockSourceLabel: string;
}

export interface SalesShiftPaymentSummary {
  paymentMethod: number;
  salesAmount: number;
  refundAmount: number;
  netAmount: number;
}

export interface SalesShiftSummary {
  from: string;
  to: string;
  totalSales: number;
  totalRefunds: number;
  netTotal: number;
  byMethod: SalesShiftPaymentSummary[];
  openingCash?: number;
  cashSales?: number;
  cashRefunds?: number;
  expectedCash?: number;
  closingCash?: number;
  cashVariance?: number;
}

export interface SalesShiftListItem {
  id: string;
  shiftNumber: string;
  warehouseId: string;
  warehouseName: string;
  openedByUserName: string;
  openedAt: string;
  closedAt?: string;
  openingCash: number;
  closingCash?: number;
  cashVariance?: number;
  status: number;
}

export interface SalesShiftDetail {
  id: string;
  shiftNumber: string;
  warehouseId: string;
  warehouseName: string;
  openedByUserName: string;
  closedByUserName?: string;
  openedAt: string;
  closedAt?: string;
  openingCash: number;
  closingCash?: number;
  expectedCash?: number;
  cashVariance?: number;
  status: number;
  closeNotes?: string;
  summary: SalesShiftSummary;
  lotAlerts?: ShiftLotComplianceAlert[];
}

export const SALES_SHIFT_STATUSES = {
  Open: 1,
  Closed: 2,
} as const;

export interface SalesOrderDetail {
  id: string;
  orderNumber: string;
  warehouseId: string;
  warehouseName: string;
  customerId?: string;
  customerName?: string;
  status: number;
  orderDate: string;
  subtotal: number;
  discountAmount: number;
  lineDiscountTotal?: number;
  orderDiscountType?: number;
  orderDiscountValue?: number;
  totalAmount: number;
  totalRefunded?: number;
  notes?: string;
  salesShiftId?: string;
  shiftNumber?: string;
  items: SalesOrderItem[];
  payments?: SalesPaymentLine[];
  refundPayments?: SalesPaymentLine[];
}

export const SALES_DISCOUNT_TYPES = {
  Percent: 1,
  Fixed: 2,
} as const;

export type SalesDiscountType = (typeof SALES_DISCOUNT_TYPES)[keyof typeof SALES_DISCOUNT_TYPES];

export const SALES_PAYMENT_METHOD_LABELS: Record<number, string> = {
  1: 'Tiền mặt',
  2: 'Thẻ',
  3: 'Chuyển khoản',
  4: 'Ví điện tử',
};

export interface PosCheckoutPaymentLine {
  paymentMethod: number;
  amount: number;
}

export interface CartLine {
  key: string;
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  stockAvailable?: number;
  batchHints?: PosBatchHint[];
  stockSourceLabel?: string;
  allocationPreview?: string;
  discountType?: SalesDiscountType;
  discountValue?: number;
}

export const SALE_STATUS_LABELS: Record<number, string> = {
  1: 'Nháp',
  2: 'Hoàn tất',
  3: 'Đã hủy',
  4: 'Hoàn tiền',
};

export const SALES_RETURN_STATUS_LABELS: Record<number, string> = {
  2: 'Hoàn tất',
};
