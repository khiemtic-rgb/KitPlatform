import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  candleChart,
  comparisonRows,
  equityCurve,
  metricDisplay,
  pnlDistribution,
  researchConclusion,
  sampleLines,
  type WorkbenchTrade,
} from './research-workbench.ts';

const root = dirname(fileURLToPath(import.meta.url));
const page = readFileSync(join(root, 'TradingResearchDeskPage.tsx'), 'utf8');
const required = [
  'Cấu hình nghiên cứu',
  'Tóm tắt lần chạy',
  'Hiệu suất',
  'Thị trường và tín hiệu',
  'Paper trading',
  'Lịch sử thí nghiệm',
  'Kết luận',
  'Chưa đủ dữ liệu',
  'Paper research',
  'Không suy đoán TP/SL',
  'Chạy bộ 4 chiến lược',
  'Ngoài mẫu',
];
const missing = required.filter((text) => !page.includes(text));
if (missing.length > 0) {
  console.error(missing.join(','));
  process.exit(1);
}
if (page.includes("from './trading.api'") || page.includes('pack_personal_intelligence')) {
  console.error('workbench mixes tenant data');
  process.exit(1);
}

const closed = (id: string, net: string, closedAt: string): WorkbenchTrade => ({
  tradeId: id,
  status: 'Closed',
  closedAt,
  netPnl: { amount: net, status: 'realized', known: true },
});

const empty = metricDisplay({ amount: null, status: 'unavailable', known: false }, 0);
if (empty.text !== 'Chưa đủ dữ liệu' || empty.text === '0') {
  console.error('missing performance became a number');
  process.exit(1);
}

const unknown = metricDisplay({ amount: null, status: 'unavailable', known: false }, 2);
if (unknown.text !== 'Chưa xác định' || unknown.text === '0') {
  console.error('unknown performance became a number');
  process.exit(1);
}

const trades = [
  closed('b', '-1', '2026-01-03T00:00:00Z'),
  closed('a', '2', '2026-01-02T00:00:00Z'),
];
const curve = equityCurve(trades);
if (!curve || curve.points[1].equity !== 2 || curve.points[2].equity !== 1 || curve.maxDrawdown !== 1) {
  console.error('equity curve is not cumulative net');
  process.exit(1);
}
if (equityCurve([{ ...closed('open', '1', '2026-01-02T00:00:00Z'), status: 'Open' }]) != null) {
  console.error('open trade produced an equity curve');
  process.exit(1);
}
if (equityCurve([closed('a', '1', '2026-01-02T00:00:00Z'), { tradeId: 'b', status: 'Closed', closedAt: '2026-01-03T00:00:00Z', netPnl: { amount: null, status: 'unavailable', known: false } }]) != null) {
  console.error('unknown net still produced an equity curve');
  process.exit(1);
}
const bars = pnlDistribution(trades);
if (!bars || bars.reduce((sum, bar) => sum + bar.net, 0) !== 1) {
  console.error('distribution changed net');
  process.exit(1);
}

const chart = candleChart(
  [{ openTime: '2026-01-01T00:00:00Z', closeTime: '2026-01-01T01:00:00Z', open: '1', high: '2', low: '0.5', close: '1.5', closed: true }],
  [{ side: 'BUY', signalTime: '2026-01-01T00:10:00Z' }, { side: 'SELL', signalTime: '2026-01-02T00:00:00Z' }],
);
if (!chart || !chart.bars[0].buy || chart.bars[0].sell || chart.unmatchedSignals !== 1) {
  console.error('signal was attached without a candle');
  process.exit(1);
}
if (candleChart([{ openTime: 'bad', closeTime: 'bad', open: 'x', high: '1', low: '1', close: '1', closed: true }], []) != null) {
  console.error('invalid candle was drawn');
  process.exit(1);
}

const conclusion = researchConclusion({
  status: 'COMPLETED',
  dataSource: 'fixture',
  replayed: true,
  newFills: 0,
  parameterKey: 'abc123',
  config: { strategy: 'sma-cross-v1', symbol: 'BTCUSDT', timeframe: '1h' },
  closedTrades: 1,
  netKnown: true,
  openPositions: 1,
});
const text = [...conclusion.experiment, ...conclusion.evidence, ...conclusion.unknown, ...conclusion.limits, ...conclusion.next].join(' ');
if (/chiến lược (tốt|xấu)/.test(text) || !text.includes('Không phải hiệu suất giao dịch thật') || !text.includes('Không trừ thêm') || !text.includes('chưa xác định')) {
  console.error('conclusion overclaims or drops a limit');
  process.exit(1);
}

const rows = comparisonRows([
  { taskId: '1', parameterKey: 'aaa', status: 'COMPLETED', dataSource: 'fixture', requestedAt: '2026-01-02T00:00:00Z', replayed: false, newFills: 2, document: { overview: { candles: 30, signals: 2, paperFills: 2 }, performance: { closedTrades: 1, netPnl: { amount: '1', status: 'realized', known: true }, winRate: { amount: '1', status: 'realized', known: true } } } },
  { taskId: '2', parameterKey: 'bbb', status: 'COMPLETED', dataSource: 'public_market', requestedAt: '2026-01-03T00:00:00Z', replayed: false, newFills: 1, document: { overview: { candles: 48, signals: 1, paperFills: 1 }, performance: { closedTrades: 0, netPnl: { amount: null, status: 'unavailable', known: false }, winRate: { amount: null, status: 'unavailable', known: false } } } },
]);
if (rows.length !== 2 || rows[0].parameterKey === rows[1].parameterKey || rows[1].net !== 'Chưa đủ dữ liệu' || 'total' in rows[0]) {
  console.error('comparison merged configs or invented a total');
  process.exit(1);
}
const lines = sampleLines({
  known: true,
  inSample: { closedTrades: 1, netPnl: { amount: '1', status: 'realized', known: true } },
  outOfSample: { closedTrades: 0, netPnl: { amount: null, status: 'unavailable', known: false } },
});
if (!lines.some((line) => line.includes('Không xáo trộn')) || !lines.some((line) => line.includes('Ngoài mẫu')) || lines.join(' ').includes('lợi thế giao dịch đã có')) {
  console.error('sample split overclaims');
  process.exit(1);
}

console.log('research workbench ok');
