export type ResearchPublication = {
  availability: string;
  schemaVersion?: string | null;
  publicationId?: string | null;
  publishedAt?: string | null;
  publisher?: string | null;
  objects?: { id?: string; availability?: string; publishedAt?: string | null }[];
  readError?: { code?: string; message?: string } | null;
};

export type ResearchExperiment = {
  experimentId: string;
  strategyId?: string | null;
  strategyVersion?: string | null;
  family?: string | null;
  symbol?: string | null;
  timeframe?: string | null;
  dataSource?: string | null;
  snapshotSha256?: string | null;
  approvalStatus?: string | null;
  evidenceStatus?: string | null;
  performanceClaim?: string | null;
  riskLimitsApproval?: string | null;
  selectedByPnl?: boolean | null;
  parametersChosenOnOutOfSample?: boolean | null;
  development?: unknown;
  outOfSample?: unknown;
  liveOrders?: boolean | null;
  exchangeOrdersSent?: number | null;
};

export type ResearchPaperStatus = {
  availability?: string;
  runnerStatus?: string | null;
  cycleOutcome?: string | null;
  lastRunAt?: string | null;
  lastProcessedOpenTime?: number | null;
  lastError?: string | null;
  health?: string | null;
  lastBarAgeMs?: number | null;
  liveOrders?: boolean | null;
  exchangeOrdersSent?: number | null;
  effectiveLiveMode?: string | null;
  riskLimitsApproval?: string | null;
  extensionCount?: number | null;
  extensionSha256?: string | null;
};

export type ResearchPaperStrategy = {
  experimentId?: string | null;
  sampleWarning?: string | null;
  inSampleOutOfSample?: string | null;
  exchangeFill?: boolean | null;
  netPnl?: string | null;
  profitFactor?: string | null;
  closedTrades?: number | null;
};

export type ResearchPaperReport = {
  availability?: string;
  reportKind?: string | null;
  symbol?: string | null;
  timeframe?: string | null;
  extensionBarCount?: number | null;
  extensionStartUtc?: string | null;
  extensionEndUtc?: string | null;
  strategies?: ResearchPaperStrategy[];
  performanceClaim?: string | null;
  liveOrders?: boolean | null;
  exchangeOrdersSent?: number | null;
  effectiveLiveMode?: string | null;
};

export type ResearchLoadResult =
  | { kind: 'loading' }
  | { kind: 'unauthorized' }
  | { kind: 'forbidden' }
  | { kind: 'network' }
  | { kind: 'http'; status: number }
  | {
      kind: 'ready';
      availability: string;
      publication: ResearchPublication | null;
      experiments: ResearchExperiment[];
      status: ResearchPaperStatus | null;
      report: ResearchPaperReport | null;
    };

export type EvidenceFlags = {
  winRateIsPerformanceEvidence: boolean | null;
  profitFactorIsPerformanceEvidence: boolean | null;
};

export type ResearchExperimentView = {
  experimentId: string;
  strategyId: string | null;
  strategyVersion: string | null;
  family: string | null;
  symbol: string | null;
  timeframe: string | null;
  dataSource: string | null;
  snapshotSha256: string | null;
  approvalStatus: string | null;
  evidenceStatus: string | null;
  performanceClaim: string | null;
  riskLimitsApproval: string | null;
  selectedByPnl: boolean | null;
  parametersChosenOnOutOfSample: boolean | null;
  development: EvidenceFlags;
  outOfSample: EvidenceFlags;
  liveOrders: boolean | null;
  exchangeOrdersSent: number | null;
};

export type ResearchStrategyView = {
  experimentId: string | null;
  sampleWarning: string | null;
  inSampleOutOfSample: string | null;
  exchangeFill: boolean | null;
  netPnl: string | null;
  profitFactor: string | null;
  closedTrades: number | null;
};

export type ResearchPanelModel = {
  tone: 'info' | 'warning' | 'error';
  title: string;
  detail: string;
  showBodies: boolean;
  labLine: string | null;
  publicationId: string | null;
  publishedAt: string | null;
  experiments: ResearchExperimentView[];
  statusLines: { label: string; value: string }[];
  reportLines: { label: string; value: string }[];
  strategies: ResearchStrategyView[];
  healthLine: string | null;
};

const SEPARATE = 'Các experiment research không phải nguồn của sổ giao dịch.';

export function classifyResearchError(error: unknown): ResearchLoadResult {
  const status = (error as { response?: { status?: number } } | null)?.response?.status;
  if (status === 401) return { kind: 'unauthorized' };
  if (status === 403) return { kind: 'forbidden' };
  if (typeof status === 'number') return { kind: 'http', status };
  return { kind: 'network' };
}

export function researchPanelModel(result: ResearchLoadResult): ResearchPanelModel {
  if (result.kind === 'loading') return blank('info', 'Đang đọc publication', 'Đang đọc publication.', false);
  if (result.kind === 'unauthorized') return blank('error', '401', '401. Phiên đăng nhập đã hết hạn.', false);
  if (result.kind === 'forbidden') return blank('error', '403', '403. Không có quyền đọc research hoặc module chưa được bật.', false);
  if (result.kind === 'network') return blank('error', 'Không tới được API', 'API research không phản hồi.', false);
  if (result.kind === 'http') return blank('error', `HTTP ${result.status}`, `API research trả HTTP ${result.status}. Không có dữ liệu publication.`, false);

  const availability = result.availability;
  const showBodies = availability === 'FRESH' || availability === 'STALE';
  const copy = availabilityCopy(availability);
  if (!showBodies) return { ...blank(copy.tone, copy.title, copy.detail, false), publicationId: safeText(result.publication?.publicationId) };
  const lab = result.publication?.objects?.find((item) => item.id === 'lab-result');
  return {
    tone: copy.tone,
    title: copy.title,
    detail: `${copy.detail} ${SEPARATE}`,
    showBodies: true,
    labLine: lab?.availability === 'PRESENT_UNTIMED'
      ? 'Lab: PRESENT_UNTIMED. Nguồn lab không có timestamp. Không dùng giờ file.'
      : null,
    publicationId: safeText(result.publication?.publicationId),
    publishedAt: safeText(result.publication?.publishedAt),
    experiments: result.experiments.map(sanitizeExperiment),
    statusLines: statusLines(result.status),
    reportLines: reportLines(result.report),
    strategies: (result.report?.strategies ?? []).map(sanitizeStrategy),
    healthLine: healthLine(result.status?.health ?? null),
  };
}

export function sanitizeExperiment(row: ResearchExperiment): ResearchExperimentView {
  return {
    experimentId: safeText(row.experimentId) ?? 'Không hiển thị',
    strategyId: safeText(row.strategyId),
    strategyVersion: safeText(row.strategyVersion),
    family: safeText(row.family),
    symbol: safeText(row.symbol),
    timeframe: safeText(row.timeframe),
    dataSource: safeText(row.dataSource),
    snapshotSha256: safeHash(row.snapshotSha256),
    approvalStatus: safeText(row.approvalStatus),
    evidenceStatus: safeText(row.evidenceStatus),
    performanceClaim: safeText(row.performanceClaim),
    riskLimitsApproval: safeText(row.riskLimitsApproval),
    selectedByPnl: row.selectedByPnl ?? null,
    parametersChosenOnOutOfSample: row.parametersChosenOnOutOfSample ?? null,
    development: evidenceFlags(row.development),
    outOfSample: evidenceFlags(row.outOfSample),
    liveOrders: row.liveOrders ?? null,
    exchangeOrdersSent: typeof row.exchangeOrdersSent === 'number' ? row.exchangeOrdersSent : null,
  };
}

function availabilityCopy(availability: string): { tone: ResearchPanelModel['tone']; title: string; detail: string } {
  if (availability === 'FRESH') {
    return { tone: 'info', title: 'FRESH', detail: 'Publication đọc được. FRESH chỉ nghĩa health nguồn là OK.' };
  }
  if (availability === 'STALE') {
    return { tone: 'warning', title: 'STALE', detail: 'Health nguồn là STALE. Các mốc nguồn vẫn được hiển thị.' };
  }
  if (availability === 'MISSING') {
    return { tone: 'error', title: 'MISSING', detail: 'MISSING. Research Lab chưa được nối. Không có publication để đọc.' };
  }
  if (availability === 'UNREADABLE') {
    return { tone: 'error', title: 'UNREADABLE', detail: 'UNREADABLE. Publication không đọc được. Không hiển thị experiment hay báo cáo.' };
  }
  if (availability === 'INCONSISTENT') {
    return { tone: 'error', title: 'INCONSISTENT', detail: 'INCONSISTENT. Hash không khớp. Không hiển thị nội dung publication.' };
  }
  if (availability === 'UNSUPPORTED_VERSION') {
    return { tone: 'error', title: 'UNSUPPORTED_VERSION', detail: 'UNSUPPORTED_VERSION. Schema không được hỗ trợ. Không hiển thị nội dung publication.' };
  }
  return { tone: 'error', title: 'Không rõ', detail: 'Publication trả trạng thái không có trong hợp đồng. Không hiển thị nội dung.' };
}

function blank(tone: ResearchPanelModel['tone'], title: string, detail: string, showBodies: boolean): ResearchPanelModel {
  return {
    tone, title, detail, showBodies,
    labLine: null,
    publicationId: null,
    publishedAt: null,
    experiments: [],
    statusLines: [],
    reportLines: [],
    strategies: [],
    healthLine: null,
  };
}

function statusLines(status: ResearchPaperStatus | null): { label: string; value: string }[] {
  if (!status) return [];
  return [
    ['Runner', safeText(status.runnerStatus)],
    ['Chu kỳ', safeText(status.cycleOutcome)],
    ['Lần chạy nguồn', safeText(status.lastRunAt)],
    ['Tuổi nến nguồn (ms)', numberText(status.lastBarAgeMs)],
    ['Health nguồn', safeText(status.health)],
    ['Live', boolText(status.liveOrders)],
    ['Lệnh gửi sàn theo nguồn', numberText(status.exchangeOrdersSent)],
    ['Chế độ live', safeText(status.effectiveLiveMode)],
    ['Phê duyệt giới hạn risk', safeText(status.riskLimitsApproval)],
    ['Số extension', numberText(status.extensionCount)],
    ['Lỗi nguồn', safeText(status.lastError)],
  ].map(([label, value]) => ({ label: label!, value: value ?? 'Chưa ghi nhận' }));
}

function reportLines(report: ResearchPaperReport | null): { label: string; value: string }[] {
  if (!report) return [];
  return [
    ['Loại báo cáo', safeText(report.reportKind)],
    ['Symbol', safeText(report.symbol)],
    ['Khung thời gian', safeText(report.timeframe)],
    ['Số nến extension', numberText(report.extensionBarCount)],
    ['Đầu cửa sổ', safeText(report.extensionStartUtc)],
    ['Cuối cửa sổ', safeText(report.extensionEndUtc)],
    ['Tuyên bố hiệu suất', safeText(report.performanceClaim)],
    ['Live', boolText(report.liveOrders)],
    ['Chế độ live', safeText(report.effectiveLiveMode)],
  ].map(([label, value]) => ({ label: label!, value: value ?? 'Chưa ghi nhận' }));
}

function sanitizeStrategy(row: ResearchPaperStrategy): ResearchStrategyView {
  return {
    experimentId: safeText(row.experimentId),
    sampleWarning: safeText(row.sampleWarning),
    inSampleOutOfSample: safeText(row.inSampleOutOfSample),
    exchangeFill: row.exchangeFill ?? null,
    netPnl: safeText(row.netPnl),
    profitFactor: safeText(row.profitFactor),
    closedTrades: typeof row.closedTrades === 'number' ? row.closedTrades : null,
  };
}

function healthLine(health: string | null): string {
  if (health === 'OK') return 'Health nguồn: OK. Đây là độ mới của nguồn, không phải hiệu suất đã xác nhận.';
  if (health === 'STALE') return 'Health nguồn: STALE.';
  if (!health) return 'Health nguồn: chưa ghi nhận.';
  return `Health nguồn: ${safeText(health) ?? 'không hiển thị'}.`;
}

function evidenceFlags(value: unknown): EvidenceFlags {
  const row = value && typeof value === 'object' ? value as Record<string, unknown> : {};
  return {
    winRateIsPerformanceEvidence: typeof row.win_rate_is_performance_evidence === 'boolean' ? row.win_rate_is_performance_evidence : null,
    profitFactorIsPerformanceEvidence: typeof row.profit_factor_is_performance_evidence === 'boolean' ? row.profit_factor_is_performance_evidence : null,
  };
}

function numberText(value: number | null | undefined): string | null {
  return typeof value === 'number' ? String(value) : null;
}

function boolText(value: boolean | null | undefined): string | null {
  if (value === true) return 'true';
  if (value === false) return 'false';
  return null;
}

export function safeText(value: string | null | undefined): string | null {
  if (value == null || value === '') return null;
  const lower = value.toLowerCase();
  if (
    value.includes('\\')
    || value.includes('/')
    || value.includes('..')
    || lower.includes('api_key')
    || lower.includes('frozen_lab_result')
    || lower.includes('paper_ops_state_dir')
  ) return null;
  return value;
}

function safeHash(value: string | null | undefined): string | null {
  const text = safeText(value);
  if (!text || !/^[a-f0-9]+$/i.test(text)) return null;
  return text;
}
