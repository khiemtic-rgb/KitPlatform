/** FAMIXA_MATERIALIZE_SH01_TIMING_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const src = (rel: string) =>
  readFileSync(join(root, '../../../../../src', rel), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');

const timing = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionDirectorTiming.cs');
const rules = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/PerformanceContractRules.cs');
const exec = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01TimingMaterializeV1Regression.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentProductionController.cs');

ok(timing.includes('5.0') && timing.includes('DIRECTOR') && timing.includes('EXPLICIT_PRODUCTION_DECISION'), 'T01-T03 director decision constants');
ok(timing.includes('FAMIXA_SHOT_TIMING_CONTRACT_V3') === false && rules.includes('IsExplicitDecision'), 'uses existing freeze path');
ok(exec.includes('LoadDirectorTimingAsync') && exec.includes('GetByScopeAsync'), 'OS facts read director snapshot');
ok(controller.includes('director-timing') && api.includes('materializeProductionDirectorTiming'), 'canonical POST path');
ok(create.includes('OfficialFilmForbidden'), 'execute guard remains');
ok(!regression.includes('svc.CreateVideoAsync') && !regression.includes('CreateVideoAsync(new'), 'T19 no CREATE_VIDEO execute');
ok(!timing.includes('providerDurationSec'), 'not provider duration');

if (fail.length) {
  console.error(`FAMIXA_MATERIALIZE_SH01_TIMING_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_MATERIALIZE_SH01_TIMING_V1 ok');
