/** FAMIXA_AI_PROVIDER_RUNTIME_MAXCOST_V1 — T1–T20. 0 providers. 0 HTTP. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { quoteFamixaProviderCost } from './famixa-ai-provider-cost';
import {
  FAMIXA_REJECT_CODE,
  FAMIXA_ROUTER_OUTCOME,
  evaluateFamixaProviderCandidate,
  routeFamixaProvider,
  selectFamixaProvider,
  seriesStartToSelectionRequirements,
  validateFamixaRuntimeMaxCost,
} from './famixa-ai-provider-routing-foundation';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const packApp = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application');
const packInfra = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure');
const readApp = (name: string) => readFileSync(join(packApp, name), 'utf8');
const readInfra = (name: string) => readFileSync(join(packInfra, name), 'utf8');

const both = { runwayConfigured: true, falConfigured: true, elevenLabsConfigured: true, geminiConfigured: true };

ok(validateFamixaRuntimeMaxCost(undefined) == null && validateFamixaRuntimeMaxCost(null) == null, 'T1 omitted valid');
const r1 = routeFamixaProvider(seriesStartToSelectionRequirements({ seconds: 5, engine: 'turbo' }), { configured: both });
ok(r1.outcome === FAMIXA_ROUTER_OUTCOME.ROUTED && r1.decision?.providerId === 'runway', 'T1 omitted → no budget filter');

const req30 = seriesStartToSelectionRequirements({ maxCost: 30, currency: 'credit', seconds: 5, engine: 'turbo' });
ok(req30.maxCost === 30 && req30.currency === 'credit', 'T8 MaxCost+Currency survive request → requirements');
ok(
  evaluateFamixaProviderCandidate(req30, 'runway', { configured: both }).eligible,
  'T2 MaxCost 30 Runway 25 eligible',
);

const req15 = seriesStartToSelectionRequirements({ maxCost: 15, seconds: 5, engine: 'turbo' });
ok(req15.maxCost === 15, 'T9 MaxCost survives into Router input');
ok(
  evaluateFamixaProviderCandidate(req15, 'runway', { configured: both }).reasonCode === FAMIXA_REJECT_CODE.OVER_BUDGET,
  'T3 MaxCost 15 OVER_BUDGET',
);

const t4 = routeFamixaProvider(req15, { configured: both });
ok(t4.outcome === FAMIXA_ROUTER_OUTCOME.NO_SAFE_CANDIDATE, 'T4 Runway 25 + Wan unknown → NO_SAFE_CANDIDATE');

const t5 = routeFamixaProvider(seriesStartToSelectionRequirements({ maxCost: 30, seconds: 5 }), { configured: both });
ok(t5.decision?.providerId === 'runway', 'T5 MaxCost 30 Runway eligible');

ok(
  evaluateFamixaProviderCandidate(seriesStartToSelectionRequirements({ maxCost: 0, seconds: 5 }), 'runway', {
    configured: both,
  }).reasonCode === FAMIXA_REJECT_CODE.OVER_BUDGET,
  'T6 MaxCost 0 → OVER_BUDGET',
);

ok(validateFamixaRuntimeMaxCost(-1) === 'MAXCOST_INVALID', 'T7 negative invalid');
let threw = false;
try {
  seriesStartToSelectionRequirements({ maxCost: -5, seconds: 5 });
} catch (e) {
  threw = String((e as Error).message).startsWith('MAXCOST_INVALID');
}
ok(threw, 'T7 negative request rejected');

const explicit = selectFamixaProvider({
  capability: 'MOTION',
  explicitProviderId: 'runway',
  maxCost: 15,
  durationSec: 5,
});
ok(explicit.selectionMode === 'EXPLICIT' && explicit.providerId === 'runway', 'T10 explicit + MaxCost 15 no switch');

const seriesTab = read('ContentFamixaSeriesTab.tsx');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const turbo = readInfra('ContentSeriesTurboService.cs');
const startFn = api.slice(api.indexOf('export async function startContentSeriesTurbo'), api.indexOf('export async function rewriteContentSeriesKfNote'));
const startCall = seriesTab.slice(seriesTab.indexOf('const started = await startContentSeriesTurbo'), seriesTab.indexOf('const started = await startContentSeriesTurbo') + 900);
ok(
  !startCall.includes('fetchContentBudget') &&
    !startCall.includes('globalCeilingUsd') &&
    !startFn.includes('fetchContentBudget') &&
    !startFn.includes('globalCeilingUsd'),
  'T11 monthly budget does not become MaxCost',
);
ok(!startCall.includes('localStorage') && !startFn.includes('localStorage'), 'T12 no localStorage budget');

const wan = quoteFamixaProviderCost({ providerId: 'wan', modelId: 'wan-2.1', quantity: 5 });
ok(wan.estimatedAmount == null && wan.kind === 'FAL_BILLED_ESTIMATE', 'T13 Wan amount remains null');

ok(typeof explicit.decisionId === 'string' && typeof explicit.reason === 'string', 'T14 SelectionDecision kept');

ok(turbo.includes('FamixaRuntimeMaxCost.MotionRequirements') && api.includes('maxCost?: number | null'), 'start wiring');
ok(readApp('ContentContracts.cs').includes('decimal? MaxCost') && readApp('ContentContracts.cs').includes('string? Currency'), 'contract MaxCost+Currency');
ok(seriesTab.includes('maxCost: opts.maxCost') && seriesTab.includes('currency: opts.currency'), 'Director pass-through');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_RUNTIME_MAXCOST_V1 T1–T14 (T15–T20 run separately)');
