export interface CustomerAdminListItem {
  id: string;
  customerCode: string;
  fullName: string;
  phone: string;
  hasAppAccount?: boolean;
  allowCredit?: boolean;
  creditLimit?: number | null;
  currentOutstanding?: number;
  customerGroupName?: string | null;
  groupDiscountPercent?: number;
  pharmacyRelation?: string;
  appLastLoginAt?: string | null;
}

export interface CustomerDetail extends CustomerAdminListItem {
  email?: string | null;
  status?: number;
  addressLine?: string | null;
}

export interface UpdateCustomerCreditPayload {
  allowCredit: boolean;
  creditLimit?: number | null;
}

export interface CreateCustomerPayload {
  fullName: string;
  phone: string;
}

export interface CustomerPilotOtpStatus {
  enabled: boolean;
  code: string | null;
  expiresAt: string | null;
  createdAt: string | null;
}

export interface IssueCounterPilotOtpResult {
  customerId: string;
  phone: string;
  pilotCode?: string | null;
  expiresAt?: string | null;
  message: string;
}

export interface PagedCustomersResult {
  items: CustomerAdminListItem[];
  total: number;
}
