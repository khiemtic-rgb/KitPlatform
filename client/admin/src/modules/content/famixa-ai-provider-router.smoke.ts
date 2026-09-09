/** FAMIXA_AI_PROVIDER_ROUTER_V1 — T1–T26. 0 providers. 0 HTTP. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { quoteFamixaProviderCost } from './famixa-ai-provider-cost';
import {
  FAMIXA_PROVIDER_EVIDENCE,
  FAMIXA_REJECT_CODE,
  FAMIXA_ROUTER_ID,
  FAMIXA_ROUTER_OUTCOME,
  FAMIXA_ROUTER_PRIORITY,
  evaluateFamixaProviderCandidate,
  routeFamixaProvider,
  selectFamixaProvider,
} from './famixa-ai-provider-routing-foundation';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const packInfra = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure');
const packApp = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application');
const readInfra = (name: string) => readFileSync(join(packInfra, name), 'utf8');
const readApp = (name: string) => readFileSync(join(packApp, name), 'utf8');

ok(FAMIXA_ROUTER_ID === 'FAMIXA_AI_PROVIDER_ROUTER_V1', 'suite id');

const both = { runwayConfigured: true, falConfigured: true, elevenLabsConfigured: true, geminiConfigured: true };
const none = { runwayConfigured: false, falConfigured: false, elevenLabsConfigured: false, geminiConfigured: false };
const runwayOnly = { runwayConfigured: true, falConfigured: false, elevenLabsConfigured: true, geminiConfigured: true };

const t1 = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'runway' }, { configured: both });
ok(t1.selectionMode === 'EXPLICIT' && t1.providerId === 'runway', 'T1 explicit Runway');

const t2 = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'wan' }, { configured: both });
ok(t2.selectionMode === 'EXPLICIT' && t2.providerId === 'wan', 'T2 explicit Wan');

const t3 = evaluateFamixaProviderCandidate({ capability: 'MOTION', maxCost: 30, durationSec: 5 }, 'runway', { configured: both });
ok(t3.eligible && t3.reasonCode === FAMIXA_REJECT_CODE.ELIGIBLE, 'T3 budget 30 Runway 25 eligible');

const t4 = evaluateFamixaProviderCandidate({ capability: 'MOTION', maxCost: 15, durationSec: 5 }, 'runway', { configured: both });
ok(!t4.eligible && t4.reasonCode === FAMIXA_REJECT_CODE.OVER_BUDGET, 'T4 budget 15 Runway 25 OVER_BUDGET');

const t5 = evaluateFamixaProviderCandidate({ capability: 'MOTION', maxCost: 15, durationSec: 5 }, 'wan', { configured: both });
ok(!t5.eligible && t5.reasonCode === FAMIXA_REJECT_CODE.UNKNOWN_COST && t5.reason.includes('cannot prove budget fit'), 'T5 Wan NOT proven in budget');

const t6 = routeFamixaProvider({ capability: 'MOTION', maxCost: 15, durationSec: 5 }, { configured: both });
ok(t6.outcome === FAMIXA_ROUTER_OUTCOME.NO_SAFE_CANDIDATE && !t6.decision, 'T6 NO_SAFE_CANDIDATE');

const t7 = selectFamixaProvider({ capability: 'MOTION', durationSec: 5 }, { configured: runwayOnly });
ok(t7.selectionMode === 'ROUTED' && t7.providerId === 'runway', 'T7 no budget Runway configured');

const t8 = evaluateFamixaProviderCandidate({ capability: 'MOTION' }, 'runway', { configured: none });
ok(t8.reasonCode === FAMIXA_REJECT_CODE.NOT_CONFIGURED, 'T8 no key NOT_CONFIGURED');

ok(
  evaluateFamixaProviderCandidate({ capability: 'MOTION' }, 'runway', { configured: both }).reason.includes('Quality evidence unavailable'),
  'T9 key + no health stays candidate with UNKNOWN availability note',
);

const t10 = evaluateFamixaProviderCandidate({ capability: 'MOTION' }, 'runway', {
  configured: both,
  unavailableProviderIds: ['runway'],
});
ok(t10.reasonCode === FAMIXA_REJECT_CODE.UNAVAILABLE, 'T10 unavailable reject');

const t11 = evaluateFamixaProviderCandidate({ capability: 'MOTION' }, 'gemini', { configured: both });
ok(t11.reasonCode === FAMIXA_REJECT_CODE.CAPABILITY_MISMATCH, 'T11 capability mismatch');

const t12a = selectFamixaProvider({ capability: 'MOTION' }, { configured: both });
const t12b = selectFamixaProvider({ capability: 'MOTION' }, { configured: both });
ok(t12a.providerId === 'runway' && t12b.providerId === 'runway', 'T12 deterministic runway');

const premium = selectFamixaProvider({ capability: 'MOTION', policy: 'PREMIUM' }, { configured: both });
ok(
  premium.providerId === t12a.providerId &&
    !premium.reason.includes('0.9') &&
    premium.reason.includes('quality and speed evidence unavailable'),
  'T13 PREMIUM does not fabricate quality score',
);

ok(FAMIXA_PROVIDER_EVIDENCE.speed === 'UNKNOWN' && !premium.reason.includes('latency'), 'T14 speed preference does not fabricate latency');

ok(typeof t7.reason === 'string' && t7.reason.length > 0, 'T15 SelectionDecision contains reason');
ok(typeof t7.decisionId === 'string' && t7.decisionId.length > 0, 'T16 SelectionDecision contains DecisionId');

const sot = quoteFamixaProviderCost({ providerId: t7.providerId, modelId: t7.modelId, quantity: 5 });
ok(t7.estimatedCost === sot.estimatedAmount && t7.costKind === sot.kind, 'T17 cost from Cost SoT');

const wan = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'wan', durationSec: 5 });
ok(wan.estimatedCost == null && wan.costKind === 'FAL_BILLED_ESTIMATE' && wan.estimatedCost !== 0, 'T18 Wan amount remains null');

const routerSrc = read('famixa-ai-provider-routing-foundation.ts');
ok(
  FAMIXA_ROUTER_PRIORITY.MOTION[0] === 'runway' &&
    FAMIXA_ROUTER_PRIORITY.MOTION[1] === 'wan' &&
    !routerSrc.includes('Math.random') &&
    !routerSrc.includes('crypto.getRandomValues'),
  'T19 no random provider selection',
);

const turboSrc = readInfra('ContentSeriesTurboService.cs');
const serviceSrc = readInfra('FamixaProviderRoutingFoundation.cs');
const foundationSrc = readApp('FamixaProviderRoutingFoundationV1.cs');
ok(turboSrc.includes('_selector.Select') && serviceSrc.includes('FamixaProviderSelectionFoundation.Route'), 'T20 Series start uses Router');
ok(foundationSrc.includes('FamixaProviderRouter.Route') && !turboSrc.includes('FamixaProviderResolver.Resolve('), 'T20 one canonical Select');

const kitSrc = readInfra('KitVideoMotionService.cs');
ok(kitSrc.includes('ContentRunwayClient') && !kitSrc.includes('IFamixaProviderSelectionService'), 'T21 Kit Video isolated');

ok(!routerSrc.includes('catalog.available'), 'catalog.available is not health');
ok(t6.diagnostic.primaryRejectionReason === FAMIXA_REJECT_CODE.UNKNOWN_COST, 'T6 primary reject UNKNOWN_COST');

const economy = selectFamixaProvider({ capability: 'MOTION', policy: 'ECONOMY' }, { configured: both });
ok(economy.providerId === 'runway', 'ECONOMY does not pick Wan as cheaper');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_ROUTER_V1 T1–T21 (T22–T26 run separately)');
