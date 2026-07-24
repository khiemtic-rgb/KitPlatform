import { http } from '@/shared/api/http';
import type {
  CreateCustomerPayload,
  CustomerAdminListItem,
  CustomerDetail,
  CustomerLoyaltySummary,
  CustomerPilotOtpStatus,
  LoyaltyProgramSummary,
  LoyaltyTier,
  LoyaltyTransaction,
  PagedCustomerOrdersResult,
  PagedCustomersResult,
  PagedLoyaltyTransactionsResult,
  SimilarCustomerCluster,
  SimilarCustomerClustersResult,
  SimilarCustomerMember,
  SimilarCustomerNamesResult,
  MergeCustomersPayload,
  MergeCustomersResult,
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
    customerGroupId:
      row.customerGroupId != null || row.CustomerGroupId != null
        ? String(row.customerGroupId ?? row.CustomerGroupId)
        : null,
    customerGroupName: (row.customerGroupName ?? row.CustomerGroupName) as string | null | undefined,
    groupDiscountPercent: Number(row.groupDiscountPercent ?? row.GroupDiscountPercent ?? 0),
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
    allowCredit: Boolean(row.allowCredit ?? row.AllowCredit),
    creditLimit:
      row.creditLimit != null || row.CreditLimit != null
        ? Number(row.creditLimit ?? row.CreditLimit)
        : null,
    addressLine: (row.addressLine ?? row.AddressLine) as string | undefined,
    idNumber: (row.idNumber ?? row.IdNumber) as string | undefined,
    emergencyContactName: (row.emergencyContactName ?? row.EmergencyContactName) as
      | string
      | undefined,
    emergencyContactPhone: (row.emergencyContactPhone ?? row.EmergencyContactPhone) as
      | string
      | undefined,
    clinicalNotes: (row.clinicalNotes ?? row.ClinicalNotes) as string | undefined,
    customerGroupId:
      row.customerGroupId != null || row.CustomerGroupId != null
        ? String(row.customerGroupId ?? row.CustomerGroupId)
        : null,
    customerGroupName: (row.customerGroupName ?? row.CustomerGroupName) as string | null | undefined,
    groupDiscountPercent: Number(row.groupDiscountPercent ?? row.GroupDiscountPercent ?? 0),
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

function normalizeSimilarMember(row: Record<string, unknown>): SimilarCustomerMember {
  return {
    id: String(row.id ?? row.Id),
    customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
    fullName: String(row.fullName ?? row.FullName ?? ''),
    phone: String(row.phone ?? row.Phone ?? ''),
    email: (row.email ?? row.Email) as string | undefined,
    status: Number(row.status ?? row.Status ?? 1),
    createdAt: String(row.createdAt ?? row.CreatedAt ?? ''),
    orderCount: Number(row.orderCount ?? row.OrderCount ?? 0),
  };
}

function normalizeSimilarCluster(row: Record<string, unknown>): SimilarCustomerCluster {
  const customers = ((row.customers ?? row.Customers ?? []) as Record<string, unknown>[]).map(
    normalizeSimilarMember,
  );
  return {
    clusterKey: String(row.clusterKey ?? row.ClusterKey ?? ''),
    matchKind: String(row.matchKind ?? row.MatchKind ?? 'name'),
    displayLabel: String(row.displayLabel ?? row.DisplayLabel ?? ''),
    maxSimilarity:
      row.maxSimilarity != null || row.MaxSimilarity != null
        ? Number(row.maxSimilarity ?? row.MaxSimilarity)
        : null,
    customers,
  };
}

export async function fetchSimilarCustomerClusters(
  threshold = 0.8,
): Promise<SimilarCustomerClustersResult> {
  const { data } = await http.get<Record<string, unknown>>('/customers/similar-clusters', {
    params: { threshold },
    // Name fuzzy scan can take longer on large tenants; default http timeout is 30s.
    timeout: 60_000,
  });
  const clusters = ((data.clusters ?? data.Clusters ?? []) as Record<string, unknown>[]).map(
    normalizeSimilarCluster,
  );
  return {
    clusters,
    clusterCount: Number(data.clusterCount ?? data.ClusterCount ?? clusters.length),
    customerCount: Number(
      data.customerCount ?? data.CustomerCount ?? clusters.reduce((n, c) => n + c.customers.length, 0),
    ),
    similarityThreshold: Number(data.similarityThreshold ?? data.SimilarityThreshold ?? threshold),
  };
}

export async function checkSimilarCustomerNames(
  name: string,
  excludeId?: string,
  threshold = 0.8,
): Promise<SimilarCustomerNamesResult> {
  const empty: SimilarCustomerNamesResult = { matches: [], hasExactNormalizedMatch: false };
  const trimmed = name.trim();
  if (!trimmed) return empty;

  const { data } = await http.get<Record<string, unknown>>('/customers/check-name', {
    params: {
      name: trimmed,
      excludeId: excludeId || undefined,
      threshold,
    },
  });
  const raw = (data.matches ?? data.Matches ?? []) as Record<string, unknown>[];
  return {
    matches: raw.map((row) => ({
      id: String(row.id ?? row.Id),
      customerCode: String(row.customerCode ?? row.CustomerCode ?? ''),
      fullName: String(row.fullName ?? row.FullName ?? ''),
      phone: String(row.phone ?? row.Phone ?? ''),
      similarityScore: Number(row.similarityScore ?? row.SimilarityScore ?? 0),
    })),
    hasExactNormalizedMatch: Boolean(
      data.hasExactNormalizedMatch ?? data.HasExactNormalizedMatch ?? false,
    ),
  };
}

export async function mergeCustomers(payload: MergeCustomersPayload): Promise<MergeCustomersResult> {
  const { data } = await http.post<Record<string, unknown>>('/customers/merge', {
    keeperCustomerId: payload.keeperCustomerId,
    sourceCustomerId: payload.sourceCustomerId,
    reason: payload.reason,
  });
  return {
    mergeId: String(data.mergeId ?? data.MergeId ?? ''),
    keeperCustomerId: String(data.keeperCustomerId ?? data.KeeperCustomerId ?? payload.keeperCustomerId),
    sourceCustomerId: String(data.sourceCustomerId ?? data.SourceCustomerId ?? payload.sourceCustomerId),
    sourceSoftDeleted: Boolean(data.sourceSoftDeleted ?? data.SourceSoftDeleted ?? true),
    ordersMoved: Number(data.ordersMoved ?? data.OrdersMoved ?? 0),
    paymentsMoved: Number(data.paymentsMoved ?? data.PaymentsMoved ?? 0),
    loyaltyProgramsMerged: Number(data.loyaltyProgramsMerged ?? data.LoyaltyProgramsMerged ?? 0),
    vouchersMoved: Number(data.vouchersMoved ?? data.VouchersMoved ?? 0),
    consentsMoved: Number(data.consentsMoved ?? data.ConsentsMoved ?? 0),
  };
}

export async function fetchCustomer(customerId: string): Promise<CustomerDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/customers/${customerId}`);
  return normalizeDetail(data);
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

export type CustomerImportError = { rowNumber: number; message: string };

export type CustomerImportResult = {
  created: number;
  skipped: number;
  failed: number;
  errors: CustomerImportError[];
};

const CUSTOMER_IMPORT_BATCH_SIZE = 500;
const CUSTOMER_IMPORT_TIMEOUT_MS = 120_000;

function normalizeCustomerImportResult(data: Record<string, unknown>): CustomerImportResult {
  const errors = ((data.errors ?? data.Errors ?? []) as Record<string, unknown>[]).map((row) => ({
    rowNumber: Number(row.rowNumber ?? row.RowNumber ?? 0),
    message: String(row.message ?? row.Message ?? ''),
  }));
  return {
    created: Number(data.created ?? data.Created ?? 0),
    skipped: Number(data.skipped ?? data.Skipped ?? 0),
    failed: Number(data.failed ?? data.Failed ?? 0),
    errors,
  };
}

export async function importCustomers(
  rows: Array<{
    rowNumber: number;
    customerCode: string;
    fullName: string;
    phone: string;
    email?: string;
    dateOfBirth?: string;
    gender?: number;
  }>,
  onBatchProgress?: (current: number, total: number) => void,
): Promise<CustomerImportResult> {
  if (rows.length === 0) {
    return { created: 0, skipped: 0, failed: 0, errors: [] };
  }

  const batches: (typeof rows)[] = [];
  for (let i = 0; i < rows.length; i += CUSTOMER_IMPORT_BATCH_SIZE) {
    batches.push(rows.slice(i, i + CUSTOMER_IMPORT_BATCH_SIZE));
  }

  let created = 0;
  let skipped = 0;
  let failed = 0;
  const errors: CustomerImportError[] = [];

  for (let i = 0; i < batches.length; i++) {
    onBatchProgress?.(i + 1, batches.length);
    const { data } = await http.post<Record<string, unknown>>('/customers/import', batches[i], {
      timeout: CUSTOMER_IMPORT_TIMEOUT_MS,
    });
    const batchResult = normalizeCustomerImportResult(data);
    created += batchResult.created;
    skipped += batchResult.skipped;
    failed += batchResult.failed;
    errors.push(...batchResult.errors);
  }

  return { created, skipped, failed, errors };
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
