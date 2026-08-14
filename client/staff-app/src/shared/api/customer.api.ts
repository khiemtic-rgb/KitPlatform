import { http } from '@/shared/api/http';
import type {
  CreateCustomerPayload,
  CustomerAdminListItem,
  CustomerDetail,
  CustomerPilotOtpStatus,
  IssueCounterPilotOtpResult,
  PagedCustomersResult,
  UpdateCustomerCreditPayload,
} from '@/shared/api/customer.types';

function mapCustomerRow(row: Record<string, unknown>): CustomerAdminListItem {
  return {
    id: String(row.id ?? row.Id),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: String(row.phone ?? row.Phone ?? ''),
    hasAppAccount: Boolean(row.hasAppAccount ?? row.HasAppAccount),
    allowCredit: Boolean(row.allowCredit ?? row.AllowCredit),
    creditLimit:
      row.creditLimit != null || row.CreditLimit != null
        ? Number(row.creditLimit ?? row.CreditLimit)
        : null,
    currentOutstanding: Number(row.currentOutstanding ?? row.CurrentOutstanding ?? 0),
    customerGroupName: (row.customerGroupName ?? row.CustomerGroupName) as string | null | undefined,
    groupDiscountPercent: Number(row.groupDiscountPercent ?? row.GroupDiscountPercent ?? 0),
    pharmacyRelation: String(row.pharmacyRelation ?? row.PharmacyRelation ?? 'member'),
    appLastLoginAt: (row.appLastLoginAt ?? row.AppLastLoginAt) as string | null | undefined,
  };
}

function mapCustomerDetail(data: Record<string, unknown>): CustomerDetail {
  return {
    ...mapCustomerRow(data),
    email: (data.email ?? data.Email) as string | null | undefined,
    status: Number(data.status ?? data.Status ?? 1),
    addressLine: (data.addressLine ?? data.AddressLine) as string | null | undefined,
  };
}

export async function fetchCustomerList(search?: string, pageSize = 30): Promise<PagedCustomersResult> {
  const { data } = await http.get<Record<string, unknown>>('/customers', {
    params: { search, page: 1, pageSize },
  });
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(mapCustomerRow);
  return {
    items,
    total: Number(data.total ?? data.Total ?? items.length),
  };
}

export async function fetchCustomerDetail(customerId: string): Promise<CustomerDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/customers/${customerId}`);
  return mapCustomerDetail(data);
}

export async function fetchCustomerById(customerId: string): Promise<CustomerDetail> {
  return fetchCustomerDetail(customerId);
}

/** Cập nhật ghi nợ — cần sales.write; giữ nguyên các trường CRM khác. */
export async function updateCustomerCreditSettings(
  customerId: string,
  payload: UpdateCustomerCreditPayload,
): Promise<CustomerDetail> {
  const current = await fetchCustomerDetail(customerId);
  const { data } = await http.put<Record<string, unknown>>(`/customers/${customerId}`, {
    fullName: current.fullName,
    phone: current.phone,
    customerCode: current.customerCode,
    email: current.email ?? null,
    status: current.status ?? 1,
    allowCredit: payload.allowCredit,
    creditLimit: payload.allowCredit ? payload.creditLimit ?? null : null,
  });
  return mapCustomerDetail(data);
}

/** Cập nhật SĐT tại quầy — cần sales.write. */
export async function updateCustomerPhone(
  customerId: string,
  phone: string,
): Promise<CustomerDetail> {
  const current = await fetchCustomerDetail(customerId);
  const { data } = await http.put<Record<string, unknown>>(`/customers/${customerId}`, {
    fullName: current.fullName,
    phone: phone.trim(),
    customerCode: current.customerCode,
    email: current.email ?? null,
    status: current.status ?? 1,
    allowCredit: Boolean(current.allowCredit),
    creditLimit: current.allowCredit ? current.creditLimit ?? null : null,
  });
  return mapCustomerDetail(data);
}

export async function createCustomer(payload: CreateCustomerPayload): Promise<CustomerDetail> {
  const { data } = await http.post<Record<string, unknown>>('/customers', payload);
  return mapCustomerDetail(data);
}

export async function fetchCustomerPilotOtp(customerId: string): Promise<CustomerPilotOtpStatus> {
  const { data } = await http.get<Record<string, unknown>>(`/customers/${customerId}/pilot-otp`);
  return {
    enabled: Boolean(data.enabled ?? data.Enabled),
    code: (data.code ?? data.Code) != null ? String(data.code ?? data.Code) : null,
    expiresAt: (data.expiresAt ?? data.ExpiresAt) as string | null,
    createdAt: (data.createdAt ?? data.CreatedAt) as string | null,
  };
}

export async function issueCounterPilotOtp(payload: {
  phone: string;
  fullName?: string;
}): Promise<IssueCounterPilotOtpResult> {
  const { data } = await http.post<Record<string, unknown>>('/customers/issue-counter-otp', payload);
  return {
    customerId: String(data.customerId ?? data.CustomerId ?? ''),
    phone: String(data.phone ?? data.Phone ?? ''),
    pilotCode:
      data.pilotCode != null || data.PilotCode != null || data.code != null || data.Code != null
        ? String(data.pilotCode ?? data.PilotCode ?? data.code ?? data.Code)
        : null,
    expiresAt: (data.expiresAt ?? data.ExpiresAt) as string | null,
    message: String(data.message ?? data.Message ?? 'Đã tạo mã OTP tại quầy.'),
  };
}
