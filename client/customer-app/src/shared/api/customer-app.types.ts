export interface CustomerProfile {
  accountId: string;
  customerId: string;
  tenantId: string;
  tenantCode: string;
  fullName: string;
  phone: string;
  preferredLocale?: string | null;
  /** Server: prospect | member | revoked */
  pharmacyRelation?: string | null;
  acquisitionSource?: string | null;
  avatarUrl?: string | null;
}

export interface CustomerLoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  profile: CustomerProfile;
}

export interface CustomerOtpSentResponse {
  expiresInSeconds: number;
  cooldownSeconds: number;
  message: string;
  pilotCode?: string | null;
  status?: 'otp_sent' | 'pending_approval' | 'counter_pin_required' | string;
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
  familyMemberId: string | null;
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
  familyMemberId?: string | null;
  dosageNote?: string;
  remindTime: string;
  daysOfWeek?: number[];
}

export interface UpdateMedicationReminderRequest {
  productId?: string;
  familyMemberId?: string | null;
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
  amountPaid: number;
  outstanding: number;
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
  amountPaid: number;
  outstanding: number;
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

export interface CustomerReceivableLine {
  salesOrderId: string;
  orderNumber: string;
  orderDate: string;
  orderTotal: number;
  amountPaid: number;
  outstanding: number;
}

export interface CustomerReceivablesSummary {
  totalReceivable: number;
  openOrderCount: number;
  lines: CustomerReceivableLine[];
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

export const CUSTOMER_RESERVATION_STATUS = {
  Pending: 1,
  Confirmed: 2,
  Ready: 3,
  Collected: 4,
  Cancelled: 5,
  Rejected: 6,
} as const;

export const CUSTOMER_RESERVATION_STATUS_LABELS: Record<number, string> = {
  1: 'Chờ xác nhận',
  2: 'Đã xác nhận',
  3: 'Sẵn sàng lấy thuốc',
  4: 'Đã lấy thuốc',
  5: 'Đã hủy',
  6: 'Từ chối',
};

export const CUSTOMER_RESERVATION_FULFILLMENT = {
  Pickup: 1,
  Delivery: 2,
} as const;

export const CUSTOMER_RESERVATION_FULFILLMENT_LABELS: Record<number, string> = {
  1: 'Đến quầy lấy',
  2: 'Giao tận nơi',
};

export interface CustomerReservationLine {
  id: string;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  customerNote?: string | null;
}

export interface CustomerReservationListItem {
  id: string;
  reservationNumber: string;
  status: number;
  fulfillmentType: number;
  itemCount: number;
  submittedAt: string;
  readyAt?: string | null;
}

export interface CustomerReservationDetail {
  id: string;
  reservationNumber: string;
  status: number;
  fulfillmentType: number;
  addressId?: string | null;
  addressSummary?: string | null;
  notes?: string | null;
  staffNotes?: string | null;
  submittedAt: string;
  confirmedAt?: string | null;
  readyAt?: string | null;
  collectedAt?: string | null;
  salesOrderId?: string | null;
  salesOrderNumber?: string | null;
  items: CustomerReservationLine[];
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

export interface RepurchaseSuggestion {
  id: string;
  salesOrderId: string;
  orderNumber: string;
  orderLabel: string;
  status: string;
  orderDate: string;
  reminderDaysSupply: number | null;
  suggestedForDate: string | null;
  snoozedUntil: string | null;
  drinkRemindersCreatedAt: string | null;
  convertedAt: string | null;
  convertedReservationId: string | null;
  convertedSalesOrderId: string | null;
}

export interface ReorderRepurchaseResult {
  suggestion: RepurchaseSuggestion;
  reservationId: string;
  reservationNumber: string;
}

export interface FamilyMember {
  id: string;
  linkedCustomerId: string | null;
  fullName: string;
  phone: string | null;
  dateOfBirth: string | null;
  gender: number | null;
  relationship: string;
  notes: string | null;
  status: number;
  notifyCaregiver: boolean;
}

export interface HealthRecordAttachment {
  fileName: string;
  mimeType: string;
  url?: string;
  dataUrl?: string;
}

export interface HealthRecord {
  id: string;
  familyMemberId: string | null;
  recordType: string;
  title: string;
  summary: string | null;
  providerName: string | null;
  recordedAt: string;
  attachmentsJson: string;
  metadataJson: string;
  attachments: HealthRecordAttachment[];
}

export interface ServerNotification {
  id: string;
  category: string;
  title: string;
  body: string;
  href: string | null;
  readAt: string | null;
  createdAt: string;
  isRead: boolean;
}

export interface CareReminder {
  id: string;
  familyMemberId: string | null;
  healthRecordId: string | null;
  reminderType: string;
  title: string;
  note: string | null;
  remindAt: string;
  isDone: boolean;
}

export const FAMILY_RELATIONSHIP_LABELS: Record<string, string> = {
  parent: 'Cha/mẹ',
  child: 'Con',
  spouse: 'Vợ/chồng',
  sibling: 'Anh/chị/em',
  other: 'Khác',
};

export const HEALTH_RECORD_TYPE_LABELS: Record<string, string> = {
  visit: 'Khám bệnh',
  prescription: 'Đơn thuốc',
  lab: 'Xét nghiệm',
  allergy: 'Dị ứng',
  diagnosis: 'Chẩn đoán',
  note: 'Ghi chú',
  bmi: 'BMI',
  blood_pressure: 'Huyết áp',
  blood_glucose: 'Đường huyết',
  vaccination: 'Tiêm chủng',
  other: 'Khác',
};

export const VITAL_RECORD_TYPES = ['bmi', 'blood_pressure', 'blood_glucose'] as const;

export const CARE_REMINDER_TYPE_LABELS: Record<string, string> = {
  visit: 'Tái khám',
  lab: 'Xét nghiệm',
  medication: 'Thuốc / điều trị',
  exercise: 'Vận động',
  nutrition: 'Dinh dưỡng',
  other: 'Khác',
};

export const NOTIFICATION_CATEGORY_LABELS: Record<string, string> = {
  order: 'Đơn hàng',
  medication: 'Nhắc thuốc',
  care: 'Tái khám / chăm sóc',
  family: 'Gia đình',
  system: 'Hệ thống',
  general: 'Chung',
};

export interface ActiveMedicationTimelineEvent {
  eventType: string;
  occurredAt: string;
  label: string;
}

export interface ActiveMedication {
  productId: string;
  productCode: string;
  productName: string;
  dosageNote: string | null;
  familyMemberId: string | null;
  medicationReminderId: string | null;
  remindTime: string | null;
  daysRemaining: number | null;
  supplyEndDate: string | null;
  lastOrderNumber: string | null;
  lastOrderDate: string | null;
  repurchaseSuggestionCount: number;
  timeline: ActiveMedicationTimelineEvent[];
}

export interface MedicationAdherenceSummary {
  dueCount: number;
  takenToday: number;
  skippedToday: number;
  scheduledToday: number;
  missedStreakDays: number;
  showMissedAlert: boolean;
}

export interface AiHealthAskResponse {
  answer: string;
  confidence: string;
  suggestChat: boolean;
  disclaimer: string;
  asksRemainingToday?: number | null;
}
