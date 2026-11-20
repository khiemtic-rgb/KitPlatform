/** AUTHORITY-STABILIZATION-01E — direct Turbo/VGE/Desk LipSync are not official execution authority. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const here = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(here, name), 'utf8');
const src = (rel: string) => readFileSync(join(here, '../../../../../src', rel), 'utf8');

const turbo = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs');
const vge = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const engine = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs');
const progress = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionProgressService.cs');
const rules = src('Packs/Content/KitPlatform.Packs.Content.Application/ProductionProgressRules.cs');
const authority = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionAuthorityV1.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionAuthorityV1Regression.cs');
const cutover = src('Packs/Content/KitPlatform.Packs.Content.Application/SeriesGraphMediaWriteCutover.cs');
const integrity = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentController.cs');
const series = read('ContentFamixaSeriesTab.tsx');
const accept = read('famixa-accept-existing-authority.ts');
const view = read('kit-video-production-progress.ts');

const between = (text: string, start: string, end: string) => {
  const from = text.indexOf(start);
  const to = text.indexOf(end, from + start.length);
  return from >= 0 && to > from ? text.slice(from, to) : '';
};

const sendTurbo = between(series, 'const sendTurbo = async', 'const startShotMotion');
const startLipsync = between(series, 'const startLipsync = async', 'const attachLipsync');
const retry = between(create, 'async Task<CreateVideoSliceResult> RetryCreateVideoAsync', 'async Task<CreateVideoSliceResult> RunAttemptLifecycleAsync');
const late = between(engine, 'public async Task<ProductionAttempt> ApplyLateResultAsync', 'public async Task<ProductionTask> NewTaskForChangedInputAsync');

ok(turbo.includes('LEGACY_EXECUTION') && !turbo.includes('BindNamedAsync') && !turbo.includes('IProductionWorkStore'), 'T1/T11 turbo direct path does not create an official binding');
ok(vge.includes('LEGACY_EXECUTION') && !vge.includes('BindNamedAsync') && !vge.includes('IProductionWorkStore'), 'T2/T10/T18 VGE direct path does not create an official binding and remains readable');
ok(startLipsync.includes("productionOsLegacyRefuseNote('CREATE_LIPSYNC', 'POST /series/lipsync')") && startLipsync.indexOf('productionOsLegacyRefuseNote') < startLipsync.indexOf('startContentSeriesLipsync'), 'T3/T12 desk lipsync returns before the provider call');
ok(late.includes('LateResultIgnored') && !late.includes('BindNamedAsync'), 'T4/T20 a late provider result does not create a binding');
ok(authority.includes('ProviderCompletionEstablishesOfficialBinding() => false'), 'T5 provider output is not official artifact authority');
ok(create.includes('StartFrozenAsync') && create.includes('FreezeInputAsync'), 'T6 official OS execution still reaches the frozen provider adapter');
ok(retry.includes('AssertFrozenMatch') && !retry.includes('FreezeInputAsync'), 'T7/T8/T19 retry uses the frozen snapshot and does not refreeze');
ok(!create.includes('graph_json') && !create.includes('acceptedTake') && !create.includes('VideoGenerationExecution'), 'T9/T16/T17 OS create-video does not read graph, acceptedTake, or VGE');
ok(progress.includes('ApplyOfficialOsProgress') && !progress.includes('VideoGenerationExecutionRepository') && !view.includes('video-generation-execution'), 'T13 official progress stays OS-only');
ok(create.includes('ReadPreviewMediaAsync') && create.includes('ReadVerifiedPhysicalBytes'), 'T14 official preview stays on the SHA gate');
ok(create.includes('ReadCurrentVoiceMediaAsync') && integrity.includes('ReadVerifiedPhysicalBytes'), 'T15 official voice stays on the SHA gate');
ok(accept.includes('acceptedTake ≠ official accept authority'), 'T16 01C acceptedTake stays non-authority');
ok(cutover.includes('acceptedTake'), 'T22 01A media list remains');
ok(vge.includes('GetLatestAsync'), 'T18 VGE history read remains');
ok(rules.includes('An empty list clears production flags'), 'T25 01D empty OS list still clears production flags');
ok(!authority.includes('CREATE TABLE') && !regression.includes('class GenericExecution'), 'T21 no new table or generic execution engine');
ok(controller.includes('POST content/series/turbo') || controller.includes('[HttpPost("series/turbo")]'), 'turbo route remains classified, not deleted');
ok(sendTurbo.includes("productionOsLegacyRefuseNote('CREATE_VIDEO', 'POST /series/turbo')") && sendTurbo.indexOf('return false') < sendTurbo.indexOf('startContentSeriesTurbo'), 'desk turbo returns before the provider call');
ok(series.includes('EDITORIAL. Pastes a Fal file'), 'attach Fal file stays editorial');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_EXECUTION_AUTHORITY_01E FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_EXECUTION_AUTHORITY_01E PASS FAIL=0 (no provider)');
