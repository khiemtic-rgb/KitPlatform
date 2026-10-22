import { http } from '@/shared/api/http';

const base = '/personal-intelligence/trading/exchanges';

export type ExchangeAsset = {
  coin: string;
  walletBalance: string | null;
  equity: string | null;
  usdValue: string | null;
  availableToWithdraw: string | null;
};

export type ExchangeConnection = {
  connectionId: string;
  exchangeCode: string;
  environment: string;
  status: string;
  apiKeyMask: string;
  readOnlyVerified: boolean;
  permissionNames: string[];
  externalUserId: string | null;
  marginMode: string | null;
  unifiedMarginStatus: number | null;
  uta: number | null;
  lastCheckedAt: string | null;
  lastError: string | null;
  lastProbeStatus: string;
  disconnectedAt: string | null;
};

export type ExchangeAccount = {
  connection: ExchangeConnection;
  snapshotId: string | null;
  capturedAt: string | null;
  freshness: string;
  sourceEndpoints: string | null;
  accountType: string | null;
  marginMode: string | null;
  unifiedMarginStatus: number | null;
  totalEquity: string | null;
  totalWalletBalance: string | null;
  totalAvailableBalance: string | null;
  balanceError: string | null;
  assets: ExchangeAsset[];
};

export type ExchangeProbe = {
  accepted: boolean;
  message: string;
  readOnlyVerified: boolean;
  permissionNames: string[];
  externalUserId: string | null;
  marginMode: string | null;
  accountType: string | null;
  totalEquity: string | null;
  balanceError: string | null;
  sourceEndpoints: string;
  checkedAt: string;
};

export type ExchangeCredentialInput = {
  exchangeCode: string;
  environment: string;
  apiKey: string;
  apiSecret: string;
  passphrase?: string;
  acknowledgeUnverifiedPermissions?: boolean;
};

export type BackfillJob = {
  jobId: string;
  connectionId: string;
  exchangeCode: string;
  environment: string;
  marketType: string;
  productSubtype: string;
  status: string;
  mode: string;
  windowStart: string | null;
  windowEnd: string | null;
  oldestExecAt: string | null;
  newestExecAt: string | null;
  cutoffAt: string | null;
  cutoffReason: string | null;
  pages: number;
  batches: number;
  fetchedCount: number;
  insertedCount: number;
  duplicateCount: number;
  skippedCount: number;
  errorCount: number;
  lastError: string | null;
  updatedAt: string | null;
  finishedAt: string | null;
};

export type ExchangeSyncIssue = {
  execId: string | null;
  execType: string | null;
  reason: string;
  missingFields: string[];
};

export type ExchangeSyncResult = {
  committed: boolean;
  syncId: string | null;
  tradingAccountId: string | null;
  exchangeCode: string;
  environment: string;
  category: string;
  source: string;
  requestedStart: string;
  requestedEnd: string;
  earliestExecAt: string | null;
  latestExecAt: string | null;
  resumeBefore: string | null;
  createdAt: string | null;
  pages: number;
  pageLimit: number;
  fetchedCount: number;
  insertedCount: number;
  duplicateCount: number;
  unmappedCount: number;
  truncated: boolean;
  notes: string[];
  missingFields: string[];
  issues: ExchangeSyncIssue[];
  tradeIds: string[];
};

export const executionCategories = [
  { value: 'linear', label: 'Hợp đồng USDT (linear)' },
  { value: 'inverse', label: 'Hợp đồng coin (inverse)' },
  { value: 'spot', label: 'Giao ngay (spot)' },
  { value: 'option', label: 'Quyền chọn (option)' },
];

export const exchangeApi = {
  list: () => http.get<ExchangeConnection[]>(base).then((r) => r.data),
  account: (id: string) => http.get<ExchangeAccount>(`${base}/${id}/account`).then((r) => r.data),
  test: (body: ExchangeCredentialInput) => http.post<ExchangeProbe>(`${base}/test`, body).then((r) => r.data),
  save: (body: ExchangeCredentialInput) => http.post<ExchangeAccount>(base, body).then((r) => r.data),
  refresh: (id: string) => http.post<ExchangeAccount>(`${base}/${id}/refresh`).then((r) => r.data),
  disconnect: (id: string) => http.delete(`${base}/${id}`).then((r) => r.data),
  latestExecutions: (id: string) =>
    http.get<ExchangeSyncResult | null>(`${base}/${id}/executions/latest`).then((r) => r.data),
  previewExecutions: (id: string, body: { category: string; start: string; end: string }) =>
    http.post<ExchangeSyncResult>(`${base}/${id}/executions/preview`, body).then((r) => r.data),
  syncExecutions: (id: string, body: { category: string; start: string; end: string }) =>
    http.post<ExchangeSyncResult>(`${base}/${id}/executions`, body).then((r) => r.data),
  backfill: (marketType?: string) =>
    http.get<BackfillJob[]>(`${base}/backfill`, { params: marketType ? { marketType } : {} }).then((r) => r.data),
  requestBackfill: (id: string, mode: 'FULL' | 'INCREMENTAL', productSubtype?: string) =>
    http.post<BackfillJob[]>(`${base}/${id}/backfill`, { mode, productSubtype }).then((r) => r.data),
  pauseBackfill: (jobId: string) => http.post<BackfillJob>(`${base}/backfill/${jobId}/pause`).then((r) => r.data),
  resumeBackfill: (jobId: string) => http.post<BackfillJob>(`${base}/backfill/${jobId}/resume`).then((r) => r.data),
  retryBackfill: (jobId: string) => http.post<BackfillJob>(`${base}/backfill/${jobId}/retry`).then((r) => r.data),
};
