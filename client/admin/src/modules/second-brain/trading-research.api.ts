import { http } from '@/shared/api/http';

export type ResearchDeskConfig = {
  symbol: string;
  timeframe: string;
  strategy: string;
  quantity: number;
  confidence: number;
  slippageBps: number;
  feeRate: number;
  maxNotional: number;
  maxLeverage: number;
  minConfidence: number;
  sessionNotional: number;
  dataSource: 'fixture' | 'public_market';
  strategyParams?: string;
};

export type PnlView = { amount: string | null; status: string; known: boolean };

export type TradeLedgerRow = {
  tradeId: string;
  status: string;
  direction: string | null;
  symbol: string | null;
  timeframe: string | null;
  strategy: string | null;
  openedAt: string | null;
  closedAt: string | null;
  openPrice: string | null;
  closePrice: string | null;
  quantity: string | null;
  grossPnl: PnlView;
  fees: PnlView;
  slippage: PnlView;
  netPnl: PnlView;
  closeReason: string | null;
  fillKeys: string[];
};

export type PositionLedgerRow = {
  symbol: string;
  direction: string;
  quantity: string;
  entryPrice: string;
  unrealizedPnl: PnlView;
  fillKeys: string[];
};

export type ResearchDocument = {
  generatedAt: string;
  source: string;
  overview: {
    status: string;
    lastRunAt: string | null;
    candles: number;
    signals: number;
    paperFills: number;
    replays: number;
    fees: string;
    slippage: string;
    cashFromFills: string;
    realizedPnl: PnlView;
  };
  candles: Array<{ symbol: string; interval: string; openTime: string; closeTime: string; open: string; high: string; low: string; close: string; closed: boolean }>;
  signals: Array<{ strategy: string; symbol: string; side: string; signalTime: string; status: string; riskAllowed: boolean | null; riskReason: string }>;
  fills: Array<{ side: string; quantity: string; tapePrice: string; executionPrice: string; fee: string; slippageQuote: string; netCash: string }>;
  runs: Array<{ recordedAt: string; status: string; inserted: number | null; duplicates: number | null; newFills: number | null; replayed: boolean | null; detail: string }>;
  trades: TradeLedgerRow[];
  positions: PositionLedgerRow[];
  performance: {
    closedTrades: number;
    winRate: PnlView;
    profitFactor: PnlView;
    expectancy: PnlView;
    netPnl: PnlView;
    maxDrawdown: PnlView;
  };
  sample?: {
    known: boolean;
    splitAt: string | null;
    note: string;
    inSample: { closedTrades: number; netPnl: PnlView; averageHoldHours: string | null };
    outOfSample: { closedTrades: number; netPnl: PnlView; averageHoldHours: string | null };
  } | null;
};

export type ResearchTask = {
  taskId: string;
  parameterKey: string;
  status: string;
  dataSource: string;
  narrativeKind: string;
  requestedAt: string;
  startedAt: string;
  finishedAt: string;
  error: string | null;
  replayed: boolean;
  newFills: number;
  config: ResearchDeskConfig;
  reportText: string;
  document: ResearchDocument | null;
};

const base = '/personal-intelligence/trading-research';

export const tradingResearchApi = {
  desk: () => http.get<{ executeEnabled: boolean; dataPlane: string; engineVersion?: string; tasks: ResearchTask[] }>(`${base}/desk`).then((response) => response.data),
  run: (body: Partial<ResearchDeskConfig> & { live?: boolean }) => http.post<ResearchTask>(`${base}/tasks`, body).then((response) => response.data),
  batch: (body: Partial<ResearchDeskConfig>) => http.post<{ tasks: ResearchTask[] }>(`${base}/batch`, body).then((response) => response.data),
};
