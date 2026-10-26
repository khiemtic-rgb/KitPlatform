export type MoneyView = { amount: string | null; status: string; known: boolean };

export type WorkbenchTrade = {
  tradeId: string;
  status: string;
  closedAt: string | null;
  netPnl: MoneyView;
};

export type WorkbenchCandle = {
  openTime: string;
  closeTime: string;
  open: string;
  high: string;
  low: string;
  close: string;
  closed: boolean;
};

export type WorkbenchSignal = { side: string; signalTime: string };

export type CandleBar = {
  time: string;
  open: number;
  high: number;
  low: number;
  close: number;
  closed: boolean;
  buy: boolean;
  sell: boolean;
};

export type CandleChartModel = {
  bars: CandleBar[];
  unmatchedSignals: number;
  incompleteCandles: number;
};

export type EquityPoint = { label: string; equity: number; drawdown: number };

export type EquityModel = { points: EquityPoint[]; maxDrawdown: number };

export type DistributionBar = { id: string; label: string; net: number };

export type Conclusion = {
  experiment: string[];
  evidence: string[];
  unknown: string[];
  limits: string[];
  next: string[];
};

export type ComparisonTask = {
  taskId: string;
  parameterKey: string;
  status: string;
  dataSource: string;
  requestedAt: string;
  replayed: boolean;
  newFills: number;
  config?: { strategy?: string; symbol?: string; timeframe?: string } | null;
  document?: {
    overview?: { candles?: number; signals?: number; paperFills?: number };
    performance?: { closedTrades?: number; netPnl?: MoneyView; winRate?: MoneyView };
    trades?: WorkbenchTrade[];
  } | null;
};

export type ComparisonRow = {
  taskId: string;
  parameterKey: string;
  configLabel: string;
  sameConfigAsAnother: boolean;
  dataSource: string;
  status: string;
  requestedAt: string;
  candles: number | null;
  signals: number | null;
  fills: number | null;
  closedTrades: number | null;
  net: string;
  winRate: string;
  newFills: number;
  replayed: boolean;
  comparable: boolean;
};

const closedStatus = 'Closed';

export function localTime(value: string | null | undefined) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString('vi-VN');
}

export function utcTime(value: string | null | undefined) {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toISOString();
}

export function formatAmount(amount: string | null | undefined) {
  if (amount == null || amount === '') return '—';
  const value = Number(amount);
  if (!Number.isFinite(value)) return amount;
  return value.toLocaleString('vi-VN', { maximumFractionDigits: 8 });
}

export function shortKey(value: string | null | undefined) {
  if (!value) return '—';
  return value.length > 12 ? `${value.slice(0, 8)}…` : value;
}

export function sourcePresentation(source: string | undefined) {
  if (source === 'fixture') {
    return {
      title: 'Fixture kiểm thử',
      plane: 'fixture' as const,
      note: 'Đây là dữ liệu mô phỏng cố định. Không phải hiệu suất giao dịch thật và không dùng số dư Binance.',
    };
  }
  if (source === 'public_market') {
    return {
      title: 'Thị trường công khai',
      plane: 'public_market' as const,
      note: 'Nến công khai. Không đọc số dư tài khoản sàn và không gửi lệnh.',
    };
  }
  if (source === 'research_trading') {
    return {
      title: 'Research đã lưu',
      plane: 'research_trading' as const,
      note: 'Paper research đã ghi. Không phải sổ tenant và không phải số dư Binance.',
    };
  }
  return { title: source || 'Chưa có nguồn', plane: 'unknown' as const, note: 'Chưa xác định nguồn dữ liệu của lần chạy này.' };
}

export function runStatusLabel(status: string) {
  if (status === 'COMPLETED') return 'Hoàn tất';
  if (status === 'REPLAYED') return 'Replay';
  if (status === 'FAILED') return 'Lỗi';
  if (status === 'KILLED') return 'Đã dừng';
  if (status === 'NO_DATA') return 'Chưa có dữ liệu';
  return status || '—';
}

export function tradeStatusLabel(status: string) {
  if (status === 'Closed') return 'Đã đóng';
  if (status === 'Open') return 'Đang mở';
  if (status === 'Unmatched') return 'Chưa ghép';
  return status || '—';
}

export function closeReasonLabel(reason: string | null | undefined) {
  if (!reason) return '—';
  if (reason === 'sma_cross_below') return 'SMA nhanh cắt xuống';
  if (reason === 'ema_cross_below') return 'EMA nhanh cắt xuống';
  if (reason === 'rsi_reversion_exit') return 'RSI cắt lên mức thoát';
  if (reason === 'breakout_range_exit') return 'Giá phá đáy cửa sổ trước';
  return reason;
}

export function metricDisplay(view: MoneyView | undefined, closedTrades?: number, kind: 'money' | 'rate' = 'money') {
  if (closedTrades === 0) return { text: 'Chưa đủ dữ liệu', hint: 'Chưa có giao dịch đã đóng.' };
  if (!view?.known || view.amount == null) return { text: 'Chưa xác định', hint: view?.status ?? 'unavailable' };
  if (kind === 'rate') {
    const rate = Number(view.amount);
    const percent = Number.isFinite(rate) ? `${(rate * 100).toLocaleString('vi-VN', { maximumFractionDigits: 2 })}%` : view.amount;
    return { text: `${formatAmount(view.amount)} · ${percent}`, hint: 'Tỷ lệ trade đóng có Net lớn hơn 0. Hòa không tính là thắng.' };
  }
  return { text: formatAmount(view.amount), hint: view.status };
}

export function explainRunDetail(detail: string | null | undefined) {
  if (!detail) return { label: '—', raw: '' };
  const read = (name: string) => detail.match(new RegExp(`${name}=([^;]+)`))?.[1];
  const parts = [
    read('inserted') != null ? `Nến mới ${read('inserted')}` : null,
    read('duplicates') != null ? `Nến trùng ${read('duplicates')}` : null,
    read('new_fills') != null ? `Fill mới ${read('new_fills')}` : null,
    read('replayed') != null ? `Replay ${read('replayed') === 'True' ? 'có' : 'không'}` : null,
  ].filter(Boolean);
  return { label: parts.length > 0 ? parts.join(' · ') : detail, raw: detail };
}

function finite(value: string) {
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

export function candleChart(candles: WorkbenchCandle[], signals: WorkbenchSignal[]): CandleChartModel | null {
  const bars = candles.flatMap((candle) => {
    const open = finite(candle.open);
    const high = finite(candle.high);
    const low = finite(candle.low);
    const close = finite(candle.close);
    const openAt = Date.parse(candle.openTime);
    const closeAt = Date.parse(candle.closeTime);
    if (open == null || high == null || low == null || close == null || !Number.isFinite(openAt) || !Number.isFinite(closeAt)) return [];
    return [{ time: candle.openTime, openAt, closeAt, open, high, low, close, closed: candle.closed, buy: false, sell: false }];
  });
  if (bars.length === 0) return null;
  let unmatchedSignals = 0;
  for (const signal of signals) {
    const time = Date.parse(signal.signalTime);
    const bar = bars.find((item) => Number.isFinite(time) && time >= item.openAt && time <= item.closeAt);
    if (!bar) {
      unmatchedSignals += 1;
      continue;
    }
    if (signal.side === 'BUY') bar.buy = true;
    if (signal.side === 'SELL') bar.sell = true;
  }
  return {
    bars: bars.map((bar) => ({ time: bar.time, open: bar.open, high: bar.high, low: bar.low, close: bar.close, closed: bar.closed, buy: bar.buy, sell: bar.sell })),
    unmatchedSignals,
    incompleteCandles: bars.filter((bar) => !bar.closed).length,
  };
}

export function equityCurve(trades: WorkbenchTrade[]): EquityModel | null {
  const closed = trades.filter((trade) => trade.status === closedStatus);
  if (closed.length === 0 || closed.some((trade) => !trade.netPnl.known || trade.netPnl.amount == null)) return null;
  const ordered = [...closed].sort((left, right) => {
    const time = Date.parse(left.closedAt ?? '') - Date.parse(right.closedAt ?? '');
    if (time !== 0 && Number.isFinite(time)) return time;
    return left.tradeId.localeCompare(right.tradeId);
  });
  let equity = 0;
  let peak = 0;
  const points: EquityPoint[] = [{ label: 'Xuất phát', equity: 0, drawdown: 0 }];
  let maxDrawdown = 0;
  for (const trade of ordered) {
    const net = Number(trade.netPnl.amount);
    if (!Number.isFinite(net)) return null;
    equity += net;
    if (equity > peak) peak = equity;
    const drawdown = peak - equity;
    if (drawdown > maxDrawdown) maxDrawdown = drawdown;
    points.push({ label: localTime(trade.closedAt), equity, drawdown });
  }
  return { points, maxDrawdown };
}

export function pnlDistribution(trades: WorkbenchTrade[]): DistributionBar[] | null {
  const closed = trades.filter((trade) => trade.status === closedStatus);
  if (closed.length === 0 || closed.some((trade) => !trade.netPnl.known || trade.netPnl.amount == null)) return null;
  const bars = closed.map((trade) => ({ id: trade.tradeId, label: localTime(trade.closedAt), net: Number(trade.netPnl.amount) }));
  if (bars.some((bar) => !Number.isFinite(bar.net))) return null;
  return bars;
}

export function researchConclusion(input: {
  status?: string;
  dataSource?: string;
  replayed?: boolean;
  newFills?: number;
  parameterKey?: string;
  config?: { strategy?: string; symbol?: string; timeframe?: string } | null;
  closedTrades?: number;
  netKnown?: boolean;
  openPositions?: number;
}): Conclusion {
  const source = sourcePresentation(input.dataSource);
  const strategy = input.config?.strategy || 'sma-cross-v1';
  const symbol = input.config?.symbol || 'BTCUSDT';
  const timeframe = input.config?.timeframe || '1h';
  const experiment = [
    `Thí nghiệm ${strategy} trên ${symbol} ${timeframe}.`,
    `Nguồn: ${source.title}.`,
    input.parameterKey ? `Cấu hình ${input.parameterKey}.` : 'Chưa có mã cấu hình.',
    `Trạng thái lần chạy: ${runStatusLabel(input.status || '')}.`,
  ];
  const evidence: string[] = [];
  const unknown: string[] = [];
  const limits = [
    source.note,
    'Hiệu suất này không dùng số dư tài khoản Binance.',
    'Phí và slippage đã nằm trong Net PnL. Không trừ thêm lần nữa.',
    'Baseline là SMA Cross. Lần chạy này không có lệnh TP/SL. Nếu một nến chạm cả hai mức mà không biết thứ tự, kết quả phải để chưa xác định.',
  ];
  const next: string[] = [];
  const closed = input.closedTrades ?? 0;
  if (closed === 0 || input.netKnown === false) {
    unknown.push('Chưa đủ giao dịch đã đóng với Net đã biết để kết luận hiệu suất.');
  } else {
    evidence.push(`Có ${closed} giao dịch đã đóng với Net đã biết. Đây là quan sát của đúng cấu hình này, chưa đủ để gọi chiến lược thắng hay thua.`);
    if (closed < 5) unknown.push(`Mẫu ${closed} giao dịch đã đóng là quá nhỏ để kết luận.`);
  }
  if ((input.openPositions ?? 0) > 0) unknown.push('Còn vị thế mở. Unrealized PnL chưa xác định vì không có mark price.');
  if (input.replayed) {
    evidence.push(input.newFills === 0
      ? 'Replay cùng cấu hình không tạo paper fill mới.'
      : 'Lần chạy được đánh dấu replay nhưng vẫn có fill mới. Không gộp số này với lần chạy trước.');
  }
  if (input.dataSource === 'fixture') next.push('Bước tiếp theo có thể là chạy cùng chiến lược trên nguồn thị trường công khai. Đó là một thí nghiệm riêng, không cộng vào kết quả fixture.');
  else if (input.dataSource === 'public_market') next.push('Bước tiếp theo có thể là đổi một tham số trong giới hạn, ví dụ phí hoặc slippage. Cấu hình mới có mã riêng và không trộn ledger.');
  else next.push('Chọn nguồn fixture hoặc thị trường công khai trước khi chạy thí nghiệm tiếp theo.');
  if (input.replayed) next.push('Chạy lại đúng cấu hình này sẽ replay. Muốn dữ liệu mới thì đợi nến đã đóng hoặc đổi tham số.');
  return { experiment, evidence, unknown, limits, next };
}

export function sampleLines(sample: { known: boolean; inSample: { closedTrades: number; netPnl: MoneyView }; outOfSample: { closedTrades: number; netPnl: MoneyView } } | null | undefined) {
  if (!sample?.known) return ['Chưa đủ nến để tách trong mẫu và ngoài mẫu.'];
  return [
    'Tách theo thời gian: khoảng 70% nến đầu là trong mẫu, phần sau là ngoài mẫu. Không xáo trộn thời gian.',
    `Trong mẫu: ${sample.inSample.closedTrades} giao dịch đóng, Net ${metricDisplay(sample.inSample.netPnl, sample.inSample.closedTrades).text}.`,
    `Ngoài mẫu: ${sample.outOfSample.closedTrades} giao dịch đóng, Net ${metricDisplay(sample.outOfSample.netPnl, sample.outOfSample.closedTrades).text}.`,
    'Hai giai đoạn không được cộng lại. PnL dương ở một giai đoạn chưa phải lợi thế giao dịch.',
  ];
}

export function comparisonRows(tasks: ComparisonTask[]): ComparisonRow[] {
  const keys = tasks.map((task) => task.parameterKey);
  return tasks.map((task) => {
    const closed = task.document?.performance?.closedTrades;
    const comparable = task.document != null;
    return {
      taskId: task.taskId,
      parameterKey: task.parameterKey,
      configLabel: task.config ? `${task.config.strategy || '—'} · ${task.config.symbol || '—'} ${task.config.timeframe || '—'}` : '—',
      sameConfigAsAnother: keys.filter((key) => key === task.parameterKey).length > 1,
      dataSource: task.dataSource,
      status: task.status,
      requestedAt: task.requestedAt,
      candles: task.document?.overview?.candles ?? null,
      signals: task.document?.overview?.signals ?? null,
      fills: task.document?.overview?.paperFills ?? null,
      closedTrades: closed ?? null,
      net: comparable ? metricDisplay(task.document?.performance?.netPnl, closed).text : 'Chưa đủ dữ liệu',
      winRate: comparable ? metricDisplay(task.document?.performance?.winRate, closed, 'rate').text : 'Chưa đủ dữ liệu',
      newFills: task.newFills,
      replayed: task.replayed,
      comparable,
    };
  });
}
