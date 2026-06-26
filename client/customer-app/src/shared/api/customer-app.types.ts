export interface CustomerProfile {
  accountId: string;
  customerId: string;
  tenantId: string;
  tenantCode: string;
  fullName: string;
  phone: string;
}

export interface CustomerLoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  profile: CustomerProfile;
}

export interface LoyaltyTier {
  tierCode: string;
  tierName: string;
  minPoints: number;
  discountPercent: number;
}

export interface LoyaltyProgramSummary {
  programId: string;
  programCode: string;
  programName: string;
  pointsBalance: number;
  lifetimePoints: number;
  currentTier: LoyaltyTier | null;
  nextTier: LoyaltyTier | null;
}

export interface LoyaltySummary {
  programs: LoyaltyProgramSummary[];
}

export interface LoyaltyTransaction {
  id: string;
  programId: string;
  programCode: string;
  transactionType: number;
  points: number;
  salesOrderId: string | null;
  notes: string | null;
  createdAt: string;
}

export interface PagedLoyaltyTransactions {
  items: LoyaltyTransaction[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomerVoucher {
  customerVoucherId: string;
  voucherId: string;
  voucherCode: string;
  voucherName: string;
  discountType: number;
  discountValue: number;
  minOrderAmount: number;
  validFrom: string;
  validTo: string;
  issuedAt: string;
  usedAt: string | null;
  isUsed: boolean;
  isExpired: boolean;
}

export interface CustomerVoucherList {
  items: CustomerVoucher[];
}

export interface MedicationReminder {
  id: string;
  productId: string;
  productCode: string;
  productName: string;
  dosageNote: string | null;
  remindTime: string;
  daysOfWeek: number[];
  nextRemindAt: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MedicationReminderList {
  items: MedicationReminder[];
}

export interface CreateMedicationReminderRequest {
  productId: string;
  dosageNote?: string;
  remindTime: string;
  daysOfWeek?: number[];
}

export interface UpdateMedicationReminderRequest {
  productId?: string;
  dosageNote?: string;
  remindTime?: string;
  daysOfWeek?: number[];
  isActive?: boolean;
}

export interface CustomerProductSearchItem {
  id: string;
  productCode: string;
  productName: string;
  genericName: string | null;
  saleUnitName: string | null;
}

export interface CustomerProductSearchResult {
  items: CustomerProductSearchItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomerConsent {
  id: string;
  customerId: string;
  channel: number;
  purpose: number;
  granted: boolean;
  grantedAt?: string;
  revokedAt?: string;
  source: number;
  notes?: string;
}

export interface PushSubscriptionStatus {
  supported: boolean;
  subscribed: boolean;
  subscriptionCount: number;
  publicKey: string | null;
}

export const CONSENT_CHANNEL_LABELS: Record<number, string> = {
  1: 'SMS',
  2: 'Zalo',
  3: 'Email',
  4: 'App push',
  5: 'Trong app',
};

export const CONSENT_PURPOSE_LABELS: Record<number, string> = {
  1: 'Marketing',
  2: 'Nhắc chăm sóc',
  3: 'Nghiên cứu',
  4: 'Hỗ trợ AI dược sĩ',
};

/** Các kênh khách có thể bật trong app cho nhắc uống thuốc. */
export const CUSTOMER_APP_CARE_REMINDER_CONSENTS = [
  { channel: 1, purpose: 2 },
  { channel: 4, purpose: 2 },
] as const;

/** Đồng ý chat hai chiều với dược sĩ trong app. */
export const CUSTOMER_APP_CHAT_CONSENT = { channel: 5, purpose: 4 } as const;

export interface CustomerChatMessage {
  id: string;
  senderType: number;
  senderName: string | null;
  body: string;
  createdAt: string;
  readAt: string | null;
}

export interface CustomerChatThread {
  threadId: string;
  unreadCount: number;
  lastMessageAt: string | null;
  lastMessagePreview: string | null;
}

export interface CustomerChatMessageList {
  items: CustomerChatMessage[];
  hasMore: boolean;
}

export interface CustomerDraftOrderLine {
  id: string;
  lineNumber: number;
  productCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  lineAmount: number;
  dosageNote?: string | null;
}

export interface CustomerDraftOrder {
  id: string;
  draftNumber: string;
  status: number;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  notes?: string | null;
  sentAt?: string | null;
  confirmedAt?: string | null;
  completedAt?: string | null;
  expiresAt?: string | null;
  salesOrderNumber?: string | null;
  items: CustomerDraftOrderLine[];
}

export interface CustomerDraftOrderListItem {
  id: string;
  draftNumber: string;
  status: number;
  totalAmount: number;
  itemCount: number;
  sentAt?: string | null;
  confirmedAt?: string | null;
  expiresAt?: string | null;
}

export const CUSTOMER_DRAFT_ORDER_STATUS = {
  Sent: 2,
  Confirmed: 3,
  Completed: 4,
  Cancelled: 5,
  Expired: 6,
} as const;

export const CUSTOMER_DRAFT_ORDER_STATUS_LABELS: Record<number, string> = {
  2: 'Chờ xác nhận',
  3: 'Đã xác nhận',
  4: 'Đã mua',
  5: 'Đã hủy',
  6: 'Hết hạn',
};

export interface CustomerPurchaseListItem {
  id: string;
  orderNumber: string;
  status: number;
  orderDate: string;
  totalAmount: number;
  itemCount: number;
  totalRefunded: number;
}

export interface CustomerPurchaseLine {
  id: string;
  productName: string;
  unitName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  returnedQuantity: number;
}

export interface CustomerPurchasePayment {
  paymentMethod: number;
  amount: number;
}

export interface CustomerPurchaseDetail {
  id: string;
  orderNumber: string;
  status: number;
  orderDate: string;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  totalRefunded: number;
  notes?: string | null;
  loyaltyPointsEarned?: number | null;
  loyaltyPointsRedeemed: number;
  loyaltyDiscountAmount: number;
  voucherDiscountAmount: number;
  voucherCode?: string | null;
  items: CustomerPurchaseLine[];
  payments: CustomerPurchasePayment[];
}

export const CUSTOMER_PURCHASE_STATUS = {
  Completed: 2,
  Refunded: 4,
} as const;

export const CUSTOMER_PURCHASE_STATUS_LABELS: Record<number, string> = {
  2: 'Hoàn tất',
  4: 'Hoàn tiền',
};

export const CUSTOMER_PAYMENT_METHOD_LABELS: Record<number, string> = {
  1: 'Tiền mặt',
  2: 'Thẻ',
  3: 'Chuyển khoản',
  4: 'Ví điện tử',
};

export interface CustomerAddress {
  id: string;
  label: string;
  recipientName?: string | null;
  phone?: string | null;
  addressLine: string;
  ward?: string | null;
  district?: string | null;
  province?: string | null;
  isDefault: boolean;
}

export const LOYALTY_TX_LABELS: Record<number, string> = {
  1: 'Tích điểm',
  2: 'Đổi điểm',
  3: 'Hết hạn',
  4: 'Điều chỉnh',
};

export const DAY_LABELS: Record<number, string> = {
  1: 'T2',
  2: 'T3',
  3: 'T4',
  4: 'T5',
  5: 'T6',
  6: 'T7',
  7: 'CN',
};

export const DEFAULT_TENANT_CODE = 'DEMO_PHARMACY';
