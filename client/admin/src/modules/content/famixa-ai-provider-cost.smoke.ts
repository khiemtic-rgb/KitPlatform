/** FAMIXA_AI_PROVIDER_COST_SINGLE_SOURCE_OF_TRUTH_V1 — T1–T24. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { acceptExistingTake } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import { isMotionAttemptSuperseded } from './ContentFamixaShotProduction/ShotProductionExecution';
import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import { CREDIT_SUM_KIND, RUNWAY_SPENT_SUM_KIND } from './content-famixa-series';
import {
  estimateFalLipsyncUsd,
  runwayCostView,
  runwayEstimatedCredits,
} from './content-famixa-prod-v2';
import {
  FAMIXA_COST_SOT_ID,
  famixaMotionGenerateCost,
  quoteFamixaProviderCost,
} from './famixa-ai-provider-cost';
import {
  estimateFamixaProviderCost,
  FAMIXA_PROVIDER_CATALOG,
  resolveFamixaProviderTask,
} from './famixa-ai-provider-orchestration';
import { applyPictureGenerationGuard } from './content-famixa-picture-pixel-invariant';
import { editorialDurationOf, performanceDurationOf, providerDurationOf } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');

ok(FAMIXA_COST_SOT_ID === 'FAMIXA_AI_PROVIDER_COST_SINGLE_SOURCE_OF_TRUTH_V1', 'SoT id');

const t1 = quoteFamixaProviderCost({ providerId: 'runway', modelId: 'gen4_turbo', quantity: 5 });
ok(t1.estimatedAmount === 25 && t1.costKind === 'ESTIMATE' && t1.kind === 'ESTIMATE', 'T1 Runway 5 sec → 25 cr ESTIMATE');

const t2 = quoteFamixaProviderCost({ providerId: 'runway', modelId: 'gen4_turbo', quantity: 10 });
ok(t2.estimatedAmount === 50 && t2.costKind === 'ESTIMATE', 'T2 Runway 10 sec → 50 cr ESTIMATE');

const t3 = quoteFamixaProviderCost({ providerId: 'wan', modelId: 'wan-2.1', quantity: 5 });
ok(t3.kind === 'FAL_BILLED_ESTIMATE' && t3.billingBasis === 'FAL_BILLED_ESTIMATE', 'T3 Wan FAL_BILLED_ESTIMATE');
ok(t3.estimatedAmount !== 0 && t3.estimatedAmount == null, 'T3 Wan amount is not 0');
ok(famixaMotionGenerateCost('wan', 5).credits !== 0, 'T3 generateCost Wan is not 0');

const t4short = quoteFamixaProviderCost({ providerId: 'fal', modelId: '1.9', quantity: 2 });
ok(t4short.billedQuantity === 5 && t4short.estimatedAmount === 0.06, 'T4 Fal 1.9 below min still bills 5 sec');
const t4 = quoteFamixaProviderCost({ providerId: 'fal', modelId: '1.9', quantity: 5 });
ok(t4.billedQuantity === 5 && t4.estimatedAmount === 0.06, 'T4 Fal 1.9 5 sec minimum');

const t5 = quoteFamixaProviderCost({ providerId: 'fal', modelId: '1.9', quantity: 10 });
ok(t5.estimatedAmount === 0.12, 'T5 Fal 1.9 10 sec');
ok(estimateFalLipsyncUsd(10, '1.9') === 0.12, 'T5 wrapper matches SoT');

const t6 = quoteFamixaProviderCost({ providerId: 'fal', modelId: 'v3', quantity: 10 });
ok(t6.estimatedAmount === 1.33 && t6.costKind === 'ESTIMATE', 'T6 Fal v3 estimate');

const t7 = quoteFamixaProviderCost({ providerId: 'fal', modelId: 'ls', quantity: 40 });
ok(t7.estimatedAmount === 0.2, 'T7 LatentSync <=40 → $0.20');

const t8 = quoteFamixaProviderCost({ providerId: 'gemini' });
ok(t8.costKind === 'UNKNOWN' && t8.kind === 'UNKNOWN' && t8.estimatedAmount == null, 'T8 Gemini UNKNOWN');

const t9 = quoteFamixaProviderCost({ providerId: 'elevenlabs' });
ok(t9.costKind === 'UNKNOWN' && t9.estimatedAmount == null, 'T9 ElevenLabs UNKNOWN');

const t10 = quoteFamixaProviderCost({
  providerId: 'runway',
  modelId: 'gen4_turbo',
  quantity: 5,
  editorialDurationSec: 2,
});
ok(t10.estimatedAmount === 25 && t10.billedQuantity === 5, 'T10 editorial 2 sec does not cut Runway 25 cr');

const t11 = quoteFamixaProviderCost({ providerId: 'fal', modelId: '1.9', quantity: 5 });
ok(t11.billedQuantity === 5 && t11.minimumBillableQuantity === 5, 'T11 LipSync performance 5 sec uses min billing');

const series = read('ContentFamixaSeriesTab.tsx');
const studio = read('ContentFamixaStudioView.tsx');
ok(series.includes("from './famixa-ai-provider-cost'") && series.includes('famixaMotionGenerateCost'), 'T12 SeriesTab reads Cost SoT');
ok(studio.includes('engine === \'wan\'') && !studio.includes('sec * 5'), 'T12 StudioView does not compute provider cost');
ok(read('ContentFamixaProdV2.tsx').includes('FAMIXA_COST_COPY'), 'T12 ProdV2 copy from SoT');

ok(series.includes('function generateCost') && series.includes('return famixaMotionGenerateCost(engine, seconds)'), 'T13 generateCost is SoT wrapper');
ok(!series.includes("credits: 0, label: 'Fal"), 'T13 Wan 0-credit formula removed');

const inferred = runwayCostView({ turboTaskId: 't', previewUrl: 'https://x/a.mp4', videoVerified: true } as never, 5);
ok(inferred.phase === 'INFERRED_ESTIMATE' && inferred.phase !== 'ACTUAL', 'T14 runwayCostView does not claim ACTUAL');
ok(String(inferred.label).includes('Inferred estimate'), 'T14 label is inferred');
ok(runwayEstimatedCredits(5) === 25, 'T14 estimated still 25 from SoT');

ok(CREDIT_SUM_KIND === 'KIT_CREDIT_ESTIMATE', 'T15 creditSum classified KIT estimate');
ok(RUNWAY_SPENT_SUM_KIND === 'INFERRED_ESTIMATE', 'T15 runwaySpentSum is inferred');
ok(!read('content-famixa-series.ts').includes("VENDOR_ACTUAL"), 'T15 no vendor actual claim');

const t16 = quoteFamixaProviderCost({ providerId: 'unknown-vendor', modelId: 'nope' });
ok(t16.costKind === 'UNKNOWN' && t16.kind === 'UNKNOWN', 'T16 unknown provider → UNKNOWN');
ok(estimateFamixaProviderCost('unknown-vendor').kind === 'UNKNOWN', 'T16 wrapper UNKNOWN');

ok(FAMIXA_PROVIDER_CATALOG.map((p) => p.providerId).join(',') === 'gemini,runway,wan,elevenlabs,fal', 'T17 registry catalog unchanged');
ok(estimateFamixaProviderCost('wan').kind === 'FAL_BILLED_ESTIMATE', 'T17 Wan Phase 2 kind kept');

ok(resolveFamixaProviderTask('wan_abc', undefined, 'MOTION').providerId === 'wan', 'T18 lifecycle task identity kept');
ok(typeof isMotionAttemptSuperseded === 'function', 'T19 late-job lock export kept');

ok(typeof acceptExistingTake === 'function', 'T20 Artifact V2 helper kept');
ok(typeof providerDurationOf === 'function' && typeof editorialDurationOf === 'function' && typeof performanceDurationOf === 'function', 'T21 Timing V3 exports kept');
ok(typeof applyPictureGenerationGuard === 'function', 'T22 Picture invariant export kept');

const accept = acceptExistingTake({
  takeUrl: 'https://take/keep.mp4',
  previewUrl: 'https://take/keep.mp4',
  kfSourceHash: 'h1',
  runwayAttempts: [{ n: 9, status: 'SUCCEEDED', outputUrl: 'https://take/keep.mp4' }],
});
ok(Boolean(accept?.acceptedTake?.url), 'T23 ACCEPT_EXISTING unchanged');
ok(!sameFailedInput({ failedKfHash: 'other', failedPromptHash: 'p', runwayAttempts: [] }, dataUriHash('data:image/jpeg;base64,AA'), 'p'), 'T24 sameFailedInput unchanged');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_COST_SINGLE_SOURCE_OF_TRUTH_V1 T1–T24');
