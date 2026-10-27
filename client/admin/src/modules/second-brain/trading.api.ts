import { http } from '@/shared/api/http';

export type TradingPage<T> = { items: T[]; total: number; page: number; pageSize: number };

export type TradingSummary = {
  tradeCount: number;
  fixtureTradeCount: number;
  otherSourceTradeCount: number;
  decisionCount: number;
  paperExecutionCount: number;
  riskVetoCount: number;
  latestSource: string | null;
  latestRecordedAt: string | null;
  onlyFixtureData: boolean;
  missingData: string[];
  importTradeCount: number;
  exchangeTradeCount: number;
};

export type TradingAccount = {
  tradingAccountId: string;
  personId: string;
  personName: string;
  externalAccountId: string;
  exchangeCode: string;
  exchangeName: string;
  status: string;
};

export type TradeRow = {
  tradeId: string;
  executedAt: string;
  exchangeCode: string;
  externalAccountId: string;
  symbol: string;
  side: string;
  price: number;
  quantity: number;
  feeAmount: number | null;
  feeCurrency: string | null;
  source: string;
  sourceType: string;
  isObserved: boolean;
  externalTradeId: string;
  category?: string | null;
  marketType?: string | null;
  productSubtype?: string | null;
  realizedPnl?: string | null;
};

export type DecisionRow = {
  decisionId: string;
  thesis: string;
  confidence: number;
  epistemicKind: string;
  status: string;
  riskAllowed: boolean | null;
  paperExecuted: boolean | null;
  operationKey: string | null;
};

export type DecisionDetail = {
  decision: DecisionRow;
  contextSummary: string | null;
  contextEpistemicKind: string | null;
  contextSource: string | null;
  evidence: { sourceRef: string; epistemicKind: string }[];
  intendedAction: string | null;
  actualAction: string | null;
  actionStatus: string | null;
  outcomeSummary: string | null;
  reflectionNote: string | null;
  experienceStatement: string | null;
  experienceStatus: string | null;
  paperExecutionCount?: number;
  policyStatus: string | null;
  policyRule: string | null;
  unknowns: string[];
};

export type OperationRow = {
  operationId: string;
  operationKey: string;
  decisionId: string | null;
  createdAt: string;
  riskAllowed: boolean | null;
  paperExecuted: boolean | null;
  executionMode: string | null;
  autonomy: string | null;
  evidenceBasis: string | null;
  unknowns: string[];
};

export type PaperRow = {
  executionId: string;
  decisionId: string | null;
  mode: string;
  symbol: string | null;
  side: string | null;
  quantity: number | null;
  price: number | null;
  orderStatus: string;
  executedAt: string;
};

export type ImportPreview = {
  acceptedCount: number;
  duplicateCount: number;
  duplicateExternalIds: string[];
  rows: {
    externalTradeId: string;
    symbol: string;
    side: string;
    quantity: number;
    price: number;
    executedAt: string;
    feeAmount: number | null;
    feeCurrency: string | null;
  }[];
};

export type LoopResult = {
  loopId: string;
  decisionId: string;
  policyStatus: string;
  riskAllowed: boolean;
  paperExecuted: boolean;
  executionMode: string | null;
  autonomy: string;
  evidenceBasis: string;
  reconstructedNetQuantity: number;
  storedTradeCount: number;
  eventTypes: string[];
  unknowns: string[];
  symbol: string;
  side: string;
  quantity: number;
  price: number;
  riskReasons?: string[] | null;
};

const base = '/personal-intelligence/trading';

export const tradingApi = {
  summary: () => http.get<TradingSummary>(`${base}/summary`).then((r) => r.data),
  accounts: () => http.get<TradingAccount[]>(`${base}/accounts`).then((r) => r.data),
  trades: (params: Record<string, string | number | undefined>) =>
    http.get<TradingPage<TradeRow>>(`${base}/trades`, { params }).then((r) => r.data),
  trade: (id: string) => http.get<TradeRow>(`${base}/trades/${id}`).then((r) => r.data),
  decisions: (params: Record<string, string | number | undefined>) =>
    http.get<TradingPage<DecisionRow>>(`${base}/decisions`, { params }).then((r) => r.data),
  decision: (id: string) => http.get<DecisionDetail>(`${base}/decisions/${id}`).then((r) => r.data),
  operations: (page = 1) =>
    http.get<TradingPage<OperationRow>>(`${base}/operations`, { params: { page, pageSize: 20 } }).then((r) => r.data),
  operation: (key: string) => http.get<OperationRow>(`${base}/operations/${encodeURIComponent(key)}`).then((r) => r.data),
  paper: (page = 1) =>
    http.get<TradingPage<PaperRow>>(`${base}/paper-executions`, { params: { page, pageSize: 20 } }).then((r) => r.data),
  previewImport: (tradingAccountId: string, csv: string, source: string) =>
    http.post<ImportPreview>(`${base}/imports/preview`, { tradingAccountId, csv, source }).then((r) => r.data),
  commitImport: (tradingAccountId: string, csv: string, source: string) =>
    http.post<{ insertedCount: number; duplicateCount: number }>(`${base}/imports`, { tradingAccountId, csv, source }).then((r) => r.data),
  runLoop: (body: Record<string, unknown>) => http.post<LoopResult>(`${base}/loops`, body).then((r) => r.data),
  approvePaper: (decisionId: string) =>
    http.post<{ decisionId: string; executionId: string; mode: string; alreadyRecorded: boolean }>(
      `${base}/decisions/${decisionId}/paper`,
    ).then((r) => r.data),
  rejectPaper: (decisionId: string) =>
    http.post<{ decisionId: string; status: string; executionCount: number; alreadyRejected: boolean }>(
      `${base}/decisions/${decisionId}/reject`,
    ).then((r) => r.data),
};
