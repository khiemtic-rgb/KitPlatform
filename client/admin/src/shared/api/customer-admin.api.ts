import { http } from '@/shared/api/http';
import type {
  CreateCustomerPayload,
  CustomerAdminListItem,
  CustomerDetail,
  CustomerLoyaltySummary,
  LoyaltyProgramSummary,
  LoyaltyTier,
  LoyaltyTransaction,
  PagedCustomerOrdersResult,
  PagedCustomersResult,
  PagedLoyaltyTransactionsResult,
  UpdateCustomerPayload,
} from '@/shared/api/customer-admin.types';

function normalizeListItem(row: Record<string, unknown>): CustomerAdminListItem {
  return {
    id: String(row.id ?? row.Id),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: String(row.phone ?? row.Phone ?? ''),
    email: (row.email ?? row.Email) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

function normalizeDetail(row: Record<string, unknown>): CustomerDetail {
  return {
    id: String(row.id ?? row.Id),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: String(row.phone ?? row.Phone ?? ''),
    email: (row.email ?? row.Email) as string | undefined,
    dateOfBirth: (row.dateOfBirth ?? row.DateOfBirth) as string | undefined,
    gender: row.gender != null || row.Gender != null ? Number(row.gender ?? row.Gender) : undefined,
    status: Number(row.status ?? row.Status ?? 1),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    hasAppAccount: Boolean(row.hasAppAccount ?? row.HasAppAccount),
    appVerified:
      row.appVerified != null || row.AppVerified != null
        ? Boolean(row.appVerified ?? row.AppVerified)
        : undefined,
    appLastLoginAt: (row.appLastLoginAt ?? row.AppLastLoginAt) as string | undefined,
  };
}

function normalizeTier(row: Record<string, unknown>): LoyaltyTier {
  return {
    tierCode: String(row.tierCode ?? row.TierCode ?? ''),
    tierName: String(row.tierName ?? row.TierName ?? ''),
    minPoints: Number(row.minPoints ?? row.MinPoints ?? 0),
    discountPercent: Number(row.discountPercent ?? row.DiscountPercent ?? 0),
  };
}

function normalizeProgram(row: Record<string, unknown>): LoyaltyProgramSummary {
  const currentTier = row.currentTier ?? row.CurrentTier;
  const nextTier = row.nextTier ?? row.NextTier;
  return {
    programId: String(row.programId ?? row.ProgramId),
    programCode: String(row.programCode ?? row.ProgramCode ?? ''),
    programName: String(row.programName ?? row.ProgramName ?? ''),
    pointsBalance: Number(row.pointsBalance ?? row.PointsBalance ?? 0),
    lifetimePoints: Number(row.lifetimePoints ?? row.LifetimePoints ?? 0),
    currentTier: currentTier ? normalizeTier(currentTier as Record<string, unknown>) : undefined,
    nextTier: nextTier ? normalizeTier(nextTier as Record<string, unknown>) : undefined,
  };
}

function normalizeTransaction(row: Record<string, unknown>): LoyaltyTransaction {
  return {
    id: String(row.id ?? row.Id),
    programId: String(row.programId ?? row.ProgramId),
    programCode: String(row.programCode ?? row.ProgramCode ?? ''),
    transactionType: Number(row.transactionType ?? row.TransactionType ?? 1),
    points: Number(row.points ?? row.Points ?? 0),
    salesOrderId: (row.salesOrderId ?? row.SalesOrderId) as string | undefined,
    notes: (row.notes ?? row.Notes) as string | undefined,
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
  };
}

export async function fetchCustomers(params?: {
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedCustomersResult> {
  const { data } = await http.get<Record<string, unknown>>('/customers', { params });
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(normalizeListItem);
  return {
    items,
    total: Number(data.total ?? data.Total ?? items.length),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? 20),
  };
}

export async function fetchCustomer(customerId: string): Promise<CustomerDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/customers/${customerId}`);
  return normalizeDetail(data);
}

export async function fetchNextCustomerCode(): Promise<string> {
  const { data } = await http.get<Record<string, unknown>>('/customers/next-code');
  return String(data.customerCode ?? data.CustomerCode ?? '');
}

export async function createCustomer(payload: CreateCustomerPayload): Promise<CustomerDetail> {
  const { data } = await http.post<Record<string, unknown>>('/customers', payload);
  return normalizeDetail(data);
}

export async function updateCustomer(
  customerId: string,
  payload: UpdateCustomerPayload,
): Promise<CustomerDetail> {
  const { data } = await http.put<Record<string, unknown>>(`/customers/${customerId}`, payload);
  return normalizeDetail(data);
}

export async function fetchCustomerOrders(
  customerId: string,
  page = 1,
  pageSize = 20,
): Promise<PagedCustomerOrdersResult> {
  const { data } = await http.get<Record<string, unknown>>(`/customers/${customerId}/orders`, {
    params: { page, pageSize },
  });
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map((row) => ({
    id: String(row.id ?? row.Id),
    orderNumber: String(row.orderNumber ?? row.OrderNumber ?? ''),
    status: Number(row.status ?? row.Status ?? 1),
    orderDate: String(row.orderDate ?? row.OrderDate ?? ''),
    totalAmount: Number(row.totalAmount ?? row.TotalAmount ?? 0),
    itemCount: Number(row.itemCount ?? row.ItemCount ?? 0),
  }));
  return {
    items,
    total: Number(data.total ?? data.Total ?? items.length),
    page: Number(data.page ?? data.Page ?? page),
    pageSize: Number(data.pageSize ?? data.PageSize ?? pageSize),
  };
}

export async function fetchCustomerLoyaltySummary(customerId: string): Promise<CustomerLoyaltySummary> {
  const { data } = await http.get<Record<string, unknown>>(`/customers/${customerId}/loyalty/summary`);
  const programs = ((data.programs ?? data.Programs ?? []) as Record<string, unknown>[]).map(
    normalizeProgram,
  );
  return { programs };
}

export async function fetchCustomerLoyaltyTransactions(
  customerId: string,
  page = 1,
  pageSize = 20,
  programId?: string,
): Promise<PagedLoyaltyTransactionsResult> {
  const { data } = await http.get<Record<string, unknown>>(
    `/customers/${customerId}/loyalty/transactions`,
    { params: { page, pageSize, programId } },
  );
  const items = ((data.items ?? data.Items ?? []) as Record<string, unknown>[]).map(normalizeTransaction);
  return {
    items,
    total: Number(data.total ?? data.Total ?? items.length),
    page: Number(data.page ?? data.Page ?? page),
    pageSize: Number(data.pageSize ?? data.PageSize ?? pageSize),
  };
}
