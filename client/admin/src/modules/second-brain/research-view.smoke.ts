/** Research publication view states. Run: npx tsx src/modules/second-brain/research-view.smoke.ts */
import { classifyResearchError, researchPanelModel, type ResearchExperiment, type ResearchLoadResult } from './research-view.ts';

function fail(message: string): never {
  console.error(message);
  process.exit(1);
}

function ready(availability: string, extra: Partial<Extract<ResearchLoadResult, { kind: 'ready' }>> = {}): ResearchLoadResult {
  return {
    kind: 'ready',
    availability,
    publication: {
      availability,
      publicationId: 'pub-fixture',
      publishedAt: '2026-10-22T04:00:00+00:00',
      objects: [{ id: 'lab-result', availability: 'PRESENT_UNTIMED', publishedAt: null }],
    },
    experiments: extra.experiments ?? [],
    status: extra.status ?? null,
    report: extra.report ?? null,
  };
}

const experiment: ResearchExperiment = {
  experimentId: 'EXP-ONE',
  strategyId: 'E:\\secret\\journal.json',
  family: 'trend',
  symbol: 'SOLUSDT',
  timeframe: '15m',
  approvalStatus: 'EXPERIMENT_NOT_APPROVED',
  evidenceStatus: 'INSUFFICIENT_EVIDENCE',
  performanceClaim: 'NOT_A_PERFORMANCE_CLAIM',
  selectedByPnl: false,
  development: { win_rate_is_performance_evidence: false, journal: 'E:\\secret\\journal.json' },
  outOfSample: { profit_factor_is_performance_evidence: false },
  exchangeOrdersSent: 0,
};

const leaked = researchPanelModel(ready('INCONSISTENT', { experiments: [{ ...experiment, experimentId: 'SECRET_PNL_MARKER' }] }));
if (leaked.showBodies || leaked.experiments.length > 0 || JSON.stringify(leaked).includes('SECRET_PNL_MARKER')) {
  fail('inconsistent publication returned a body');
}
if (leaked.title !== 'INCONSISTENT') fail('inconsistent title');

for (const availability of ['MISSING', 'UNREADABLE', 'UNSUPPORTED_VERSION'] as const) {
  const model = researchPanelModel(ready(availability, {
    experiments: [experiment],
    report: { strategies: [{ netPnl: '0', sampleWarning: 'SAMPLE_TOO_SMALL' }] },
  }));
  const text = JSON.stringify(model);
  if (model.showBodies || model.experiments.length || model.strategies.length) fail(`${availability} showed a body`);
  if (text.includes('SAMPLE_TOO_SMALL') || text.includes('"0"')) fail(`${availability} leaked report numbers`);
  if (model.title !== availability) fail(`${availability} title`);
  if (availability === 'MISSING' && !model.detail.includes('chưa được nối')) fail('missing does not say the lab is unconnected');
}

const fresh = researchPanelModel(ready('FRESH', {
  experiments: [experiment],
  status: { health: 'OK', lastRunAt: '2026-10-22T03:04:02.201078+00:00', lastBarAgeMs: 242330, runnerStatus: 'SUCCEEDED', cycleOutcome: 'NO_NEW_SIGNAL' },
  report: {
    reportKind: 'PAPER_EXTENSION',
    performanceClaim: 'NOT_A_VERIFIED_PERFORMANCE_CLAIM',
    strategies: [{ experimentId: 'EXP-ONE', sampleWarning: 'SAMPLE_TOO_SMALL', netPnl: 'NOT_REPORTED_SAMPLE_TOO_SMALL', profitFactor: 'NOT_REPORTED_SAMPLE_TOO_SMALL', exchangeFill: false }],
  },
}));
const freshText = JSON.stringify(fresh);
if (!fresh.showBodies || fresh.tone !== 'info' || !fresh.detail.includes('FRESH chỉ nghĩa health nguồn là OK')) fail('fresh copy');
if (!fresh.labLine?.includes('PRESENT_UNTIMED') || fresh.publishedAt !== '2026-10-22T04:00:00+00:00') fail('lab timestamp');
if (freshText.includes('secret') || freshText.includes('journal')) fail('fresh leaked a path');
if (fresh.experiments[0]?.approvalStatus !== 'EXPERIMENT_NOT_APPROVED') fail('approval status dropped');
if (fresh.experiments[0]?.evidenceStatus !== 'INSUFFICIENT_EVIDENCE') fail('evidence status dropped');
if (fresh.experiments[0]?.performanceClaim !== 'NOT_A_PERFORMANCE_CLAIM') fail('performance claim dropped');
if (fresh.experiments[0]?.development.winRateIsPerformanceEvidence !== false) fail('evidence flag dropped');
if (fresh.experiments[0]?.strategyId !== null) fail('path strategy id was shown');
if (!fresh.strategies.some((row) => row.sampleWarning === 'SAMPLE_TOO_SMALL')) fail('sample warning dropped');
if (!fresh.healthLine?.includes('không phải hiệu suất đã xác nhận')) fail('health caption');
if (freshText.includes('tradeCount')) fail('research model includes a trading count');

const stale = researchPanelModel(ready('STALE', {
  status: { health: 'STALE', lastRunAt: '2026-10-22T03:04:02.201078+00:00', lastBarAgeMs: 242330 },
}));
if (stale.title !== 'STALE' || stale.tone !== 'warning' || !JSON.stringify(stale.statusLines).includes('2026-10-22T03:04:02.201078+00:00')) {
  fail('stale timestamps');
}

const summary = { tradeCount: 7, paperExecutionCount: 1 };
if (classifyResearchError({ response: { status: 401 } }).kind !== 'unauthorized') fail('401');
if (classifyResearchError({ response: { status: 403 } }).kind !== 'forbidden') fail('403');
if (classifyResearchError({}).kind !== 'network') fail('network');
if (researchPanelModel(classifyResearchError({ response: { status: 401 } })).title !== '401') fail('401 copy');
if (researchPanelModel(classifyResearchError({ response: { status: 403 } })).showBodies) fail('403 showed a body');
if (researchPanelModel({ kind: 'loading' }).title !== 'Đang đọc publication') fail('loading');
if (summary.tradeCount !== 7 || summary.paperExecutionCount !== 1) fail('summary mutated');

console.log('research view ok');
