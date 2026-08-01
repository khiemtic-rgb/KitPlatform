export type TenantBatchModeValue = 'off' | 'suggest' | 'label_optional' | 'label_required';

export const SALES_PAYMENT_CASH = 1;
export const SALES_PAYMENT_CARD = 2;
export const SALES_PAYMENT_BANK = 3;
export const SALES_PAYMENT_EWALLET = 4;
export const SALES_PAYMENT_CREDIT = 5;

export const SALES_DISCOUNT_TYPES = {
  Percent: 1,
  Fixed: 2,
} as const;

export type SalesDiscountType = (typeof SALES_DISCOUNT_TYPES)[keyof typeof SALES_DISCOUNT_TYPES];

export const STAFF_PAYMENT_METHOD_OPTIONS = [
  { value: SALES_PAYMENT_CASH, label: 'Tiền mặt' },
  { value: SALES_PAYMENT_CARD, label: 'Thẻ' },
  { value: SALES_PAYMENT_BANK, label: 'Chuyển khoản' },
  { value: SALES_PAYMENT_EWALLET, label: 'Ví điện tử' },
  { value: SALES_PAYMENT_CREDIT, label: 'Ghi nợ' },
];

export interface PosCheckoutPaymentLine {
  paymentMethod: number;
  amount: number;
}

export interface PosCustomerVoucher {
  customerVoucherId: string;
  voucherId: string;
  voucherCode: string;
  voucherName: string;
  discountType: number;
  discountValue: number;
  minOrderAmount: number;
  discountAmount: number;
}

export interface PosCustomerLoyalty {
  loyaltyEnabled: boolean;
  pointsBalance: number;
  amountPerPoint: number;
  pointsPerAmount: number;
  maxRedeemPercent: number;
  maxRedeemDiscountAmount: number;
  maxRedeemPoints: number;
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

export interface PosBatchHint {
  batchId: string;
  batchNumber: string;
  expiryDate?: string;
  quantityAvailable: number;
  isSuggested: boolean;
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
  dispensingClass?: string;
  stockAvailable?: number;
  batchHints?: PosBatchHint[];
  batchLabel?: string;
  prescriptionLineId?: string;
  discountType?: SalesDiscountType;
  discountValue?: number;
}

export interface PosProductLookup {
  productId: string;
  productCode: string;
  productName: string;
  productUnitId: string;
  unitName: string;
  unitPrice: number;
  stockAvailable: number;
  dispensingClass?: string;
  batchHints?: PosBatchHint[];
}

export type TenantRxSettings = {
  enforcementMode: 'off' | 'strict' | 'warn';
  posBlockedAudit: boolean;
};

export interface PosProductSearchItem {
  productCode: string;
  productName: string;
  lookupCode: string;
  unitName: string;
  unitPrice: number;
  stockAvailable: number;
}

export interface CustomerListItem {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  allowCredit?: boolean;
}

export interface SalesOrderItem {
  id?: string;
  productId?: string;
  productUnitId?: string;
  productCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  batchNumber?: string;
  batchId?: string;
  returnedQuantity?: number;
  discountType?: SalesDiscountType;
  discountValue?: number;
}

export interface SalesOrderListItem {
  id: string;
  orderNumber: string;
  customerName?: string;
  orderDate: string;
  totalAmount: number;
  status: number;
}

export interface SalesOrderDetailFull extends SalesOrderDetail {
  discountAmount: number;
  items: SalesOrderItem[];
}

export interface SalesReturnDetail {
  id: string;
  returnNumber: string;
  orderNumber: string;
  totalRefund: number;
}

export interface SalesOrderDetail {
  id: string;
  orderNumber: string;
  orderDate: string;
  totalAmount: number;
  amountPaid: number;
  status?: number;
  warehouseId?: string;
  customerId?: string;
  customerName?: string;
  orderDiscountType?: number;
  orderDiscountValue?: number;
  items: SalesOrderItem[];
  payments: { paymentMethod: number; amount: number }[];
}

export interface SalesShiftSummary {
  from: string;
  to: string;
  totalSales: number;
  totalRefunds: number;
  netTotal: number;
  byMethod: {
    paymentMethod: number;
    salesAmount: number;
    refundAmount: number;
    netAmount: number;
  }[];
  openingCash: number;
  cashSales: number;
  cashRefunds: number;
  expectedCash: number;
}

export interface SalesShiftDetail {
  id: string;
  shiftNumber: string;
  warehouseId: string;
  warehouseName?: string;
  status: number;
  openedAt?: string;
  summary?: SalesShiftSummary;
  lotAlerts?: ShiftLotComplianceAlert[];
}

export interface ReceiptStoreSettings {
  name: string;
  phone?: string;
  address?: string;
  tagline?: string;
}

export interface Warehouse {
  id: string;
  warehouseCode: string;
  warehouseName: string;
  branchName?: string;
}

export interface CreateSalePayload {
  warehouseId: string;
  customerId?: string;
  saveAsDraft: boolean;
  orderDiscountType?: number;
  orderDiscountValue?: number;
  loyaltyDiscountAmount?: number;
  customerVoucherId?: string;
  orderReminderLabel?: string | null;
  orderReminderDaysSupply?: number;
  items: {
    productId: string;
    productUnitId: string;
    quantity: number;
    batchNumber?: string;
    prescriptionLineId?: string;
    discountType?: number;
    discountValue?: number;
  }[];
  payments?: { paymentMethod: number; amount: number }[];
  prescriptionId?: string;
}
