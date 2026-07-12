export interface CustomerAdminListItem {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  email?: string;
  status: number;
  createdAt: string;
}

export interface PagedCustomersResult {
  items: CustomerAdminListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomerDetail {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  email?: string;
  dateOfBirth?: string;
  gender?: number;
  status: number;
  createdAt: string;
  hasAppAccount: boolean;
  appVerified?: boolean;
  appLastLoginAt?: string;
  allowCredit?: boolean;
  creditLimit?: number | null;
  addressLine?: string;
  idNumber?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  clinicalNotes?: string;
}

export interface CustomerPilotOtpStatus {
  enabled: boolean;
  code: string | null;
  expiresAt: string | null;
  createdAt: string | null;
}

export interface CustomerOrderListItem {
  id: string;
  orderNumber: string;
  status: number;
  orderDate: string;
  totalAmount: number;
  itemCount: number;
}

export interface PagedCustomerOrdersResult {
  items: CustomerOrderListItem[];
  total: number;
  page: number;
  pageSize: number;
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
  currentTier?: LoyaltyTier;
  nextTier?: LoyaltyTier;
}

export interface CustomerLoyaltySummary {
  programs: LoyaltyProgramSummary[];
}

export interface LoyaltyTransaction {
  id: string;
  programId: string;
  programCode: string;
  transactionType: number;
  points: number;
  salesOrderId?: string;
  notes?: string;
  createdAt: string;
}

export interface PagedLoyaltyTransactionsResult {
  items: LoyaltyTransaction[];
  total: number;
  page: number;
  pageSize: number;
}

export const CUSTOMER_STATUS_LABELS: Record<number, string> = {
  1: 'Hoạt động',
  0: 'Ngưng',
};

export const CUSTOMER_GENDER_LABELS: Record<number, string> = {
  1: 'Nam',
  2: 'Nữ',
};

export const LOYALTY_TX_LABELS: Record<number, string> = {
  1: 'Tích điểm',
  2: 'Đổi điểm',
  3: 'Hết hạn',
  4: 'Điều chỉnh',
};

export interface CreateCustomerPayload {
  fullName: string;
  phone: string;
  customerCode?: string;
  email?: string;
  dateOfBirth?: string;
  gender?: number;
  addressLine?: string;
  idNumber?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  clinicalNotes?: string;
}

export interface UpdateCustomerPayload extends CreateCustomerPayload {
  customerCode: string;
  status: number;
  allowCredit?: boolean;
  creditLimit?: number | null;
}

export const CUSTOMER_GENDER_OPTIONS = [
  { value: 1, label: 'Nam' },
  { value: 2, label: 'Nữ' },
];

export const CUSTOMER_STATUS_OPTIONS = [
  { value: 1, label: 'Hoạt động' },
  { value: 0, label: 'Ngưng' },
];
