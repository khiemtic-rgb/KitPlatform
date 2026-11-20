/** AUTHORITY-STABILIZATION-01D — production progress is OS bindings. 0 providers. */

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

const rules = src('Packs/Content/KitPlatform.Packs.Content.Application/ProductionProgressRules.cs');
const service = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionProgressService.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/ProductionProgressAuthorityV1Regression.cs');
const series = read('ContentFamixaSeriesTab.tsx');
const view = read('kit-video-production-progress.ts');
const vge = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs');
const cutover = src('Packs/Content/KitPlatform.Packs.Content.Application/SeriesGraphMediaWriteCutover.cs');
const integrity = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs');
const accept = read('famixa-accept-existing-authority.ts');

ok(rules.includes('fact.ScriptLocked'), 'T1 script stage still reads graph lock');
ok(service.includes('ApplyOfficialOsProgress') && service.includes('ListActiveBindingsAsync'), 'T6 service reads OS bindings');
ok(!service.includes('VideoGenerationExecutionRepository') && !service.includes('_vge'), 'T3/T15 progress service does not read VGE');
ok(rules.includes('An empty list clears production flags'), 'T7/T8 empty OS does not keep a prior production flag');
ok(regression.includes('verification does not approve'), 'T9 verification stays distinct from approval');
ok(regression.includes('mix binding is finish'), 'T10 binding finish is distinct from script');
ok(integrity.includes('ReadVerifiedPhysicalBytes'), 'T11 01B SHA gate remains');
ok(accept.includes('acceptedTake ≠ official accept authority'), 'T12 01C acceptedTake stays non-authority');
ok(cutover.includes('acceptedTake'), 'T13 01A still lists acceptedTake as media');
ok(vge.includes('GetLatestAsync'), 'T14 VGE execution service still reads its own history');
ok(!view.includes('video-generation-execution') && !view.includes('GetLatestAsync'), 'T15 progress view has no VGE client');
ok(!series.includes('Math.max(workflow?.imageProgress') && !series.includes('Math.max(workflow?.videoProgress'), 'T2/T5 series track does not raise OS counts with graph URLs');
ok(!rules.includes('class GenericProgress') && !service.includes('CREATE TABLE'), 'T16 no new progress table or generic service');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_PROGRESS_AUTHORITY_01D FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_PROGRESS_AUTHORITY_01D PASS FAIL=0 (no provider)');
