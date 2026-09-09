/** FAMIXA_AI_PROVIDER_ROUTING_FOUNDATION_V1 — T1–T24. 0 providers. 0 HTTP. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  FAMIXA_ROUTING_FOUNDATION_ID,
  classifyFamixaProviderAvailability,
  isKnownNumericCostWithinBudget,
  selectFamixaProvider,
} from './famixa-ai-provider-routing-foundation';
import { resolveFamixaProvider } from './famixa-ai-provider-orchestration';

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

ok(FAMIXA_ROUTING_FOUNDATION_ID === 'FAMIXA_AI_PROVIDER_ROUTING_FOUNDATION_V1', 'suite id');

const t1 = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'runway' });
ok(t1.selectionMode === 'EXPLICIT' && t1.providerId === 'runway', 'T1 explicit Runway');

const t2 = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'wan' });
ok(t2.selectionMode === 'EXPLICIT' && t2.providerId === 'wan', 'T2 explicit Wan');

const t3 = selectFamixaProvider({ capability: 'MOTION', engine: 'turbo' });
ok(t3.selectionMode === 'ROUTED' && t3.providerId === 'runway', 'T3 engine turbo → ROUTED runway');

const t4 = selectFamixaProvider({ capability: 'MOTION', engine: 'wan' });
ok(t4.selectionMode === 'ROUTED' && t4.providerId === 'wan', 'T4 engine wan → ROUTED wan (eligible, no budget)');

const t5 = selectFamixaProvider({ capability: 'MOTION' });
ok(t5.selectionMode === 'ROUTED' && t5.policy === 'STANDARD' && t5.providerId === 'runway', 'T5 no explicit → ROUTED stable runway');

const t6 = selectFamixaProvider({ capability: 'PICTURE' });
ok(t6.providerId === 'gemini', 'T6 Picture → Gemini');

const t7 = selectFamixaProvider({ capability: 'VOICE' });
ok(t7.providerId === 'elevenlabs', 'T7 Voice → ElevenLabs');

const t8 = selectFamixaProvider({ capability: 'LIPSYNC' });
ok(t8.providerId === 'fal', 'T8 LipSync → Fal');

const t9 = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'runway', durationSec: 5 });
ok(t9.estimatedCost === 25 && t9.costKind === 'ESTIMATE', 'T9 Runway numeric ESTIMATE');

const t10 = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'wan', durationSec: 5 });
ok(t10.estimatedCost == null && t10.costKind === 'FAL_BILLED_ESTIMATE', 'T10 Wan null FAL_BILLED_ESTIMATE');
ok(t10.estimatedCost !== 0, 'T10 Wan amount is not 0');

ok(isKnownNumericCostWithinBudget(25, 30) === 'WITHIN_BUDGET', 'T11 known <= maxCost');
ok(isKnownNumericCostWithinBudget(50, 25) === 'OVER_BUDGET', 'T12 known > maxCost');
ok(isKnownNumericCostWithinBudget(null, 30) === 'UNKNOWN', 'T13 unknown cost');
ok(isKnownNumericCostWithinBudget(undefined, 30) === 'UNKNOWN', 'T13 undefined is UNKNOWN not zero');

const t14 = classifyFamixaProviderAvailability('runway', {
  runwayConfigured: false,
  falConfigured: false,
  elevenLabsConfigured: false,
  geminiConfigured: false,
});
ok(t14.state === 'NOT_CONFIGURED' && t14.isAvailable === false, 'T14 no key → NOT_CONFIGURED');

const t15 = classifyFamixaProviderAvailability('runway', {
  runwayConfigured: true,
  falConfigured: true,
  elevenLabsConfigured: true,
  geminiConfigured: true,
});
ok(
  t15.state === 'UNKNOWN' && t15.configuration === 'CONFIGURED' && t15.isAvailable === false && t15.state !== 'AVAILABLE',
  'T15 key + no health → UNKNOWN not AVAILABLE',
);

ok(typeof t1.reason === 'string' && t1.reason.length > 0, 'T16 SelectionDecision has reason');
ok(typeof t1.decisionId === 'string' && t1.decisionId.length > 0, 'T17 SelectionDecision has DecisionId');

const turboSrc = readInfra('ContentSeriesTurboService.cs');
ok(turboSrc.includes('_selector.Select') && turboSrc.includes('FamixaProviderSelectionRequirements'), 'T18 TurboService Start uses Selection Service');
ok(!turboSrc.includes('FamixaProviderResolver.Resolve('), 'T18 TurboService no longer Resolve() outside Foundation');
ok(turboSrc.includes('decision.ProviderId'), 'T18 execution receives SelectionDecision');
const seriesTab = read('ContentFamixaSeriesTab.tsx');
ok(seriesTab.includes('selectFamixaProvider') && seriesTab.includes("capability: 'MOTION'"), 'T18 SeriesTab start stamp uses Foundation');

const kitSrc = readInfra('KitVideoMotionService.cs');
const kitRules = readApp('KitVideoMotionRules.cs');
ok(kitSrc.includes('ContentRunwayClient') && !kitSrc.includes('IFamixaProviderSelectionService'), 'T19 Kit Video isolated from Series selector');
ok(kitRules.includes('gen4_turbo') && !kitRules.includes('FamixaProviderSelectionFoundation'), 'T19 Kit Video MotionRules stays Runway pin');

ok(resolveFamixaProvider('MOTION', { engine: 'turbo' }).providerId === 'runway', 'compat Resolve wraps Foundation turbo');
ok(resolveFamixaProvider('MOTION', { engine: 'wan' }).providerId === 'wan', 'compat Resolve wraps Foundation wan');

const over = selectFamixaProvider({
  capability: 'MOTION',
  explicitProviderId: 'runway',
  maxCost: 1,
  durationSec: 10,
});
ok(over.selectionMode === 'EXPLICIT' && over.providerId === 'runway', 'explicit over budget does not switch');

const orch = read('famixa-ai-provider-orchestration.ts');
ok(orch.includes('selectFamixaProvider') && orch.includes('Compatibility wrapper'), 'resolveFamixaProvider wraps Foundation');

const catalog = read('famixa-ai-provider-orchestration.ts');
ok(catalog.includes("available: true"), 'catalog.available still exists as catalog flag');
ok(!read('famixa-ai-provider-routing-foundation.ts').includes('catalog.available'), 'Foundation does not treat catalog.available as health');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_ROUTING_FOUNDATION_V1 T1–T19 (T20–T24 run separately)');
