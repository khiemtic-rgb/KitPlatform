/** FAMIXA_REAL_FILM_READINESS_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const src = (rel: string) =>
  readFileSync(join(root, '../../../../../src', rel), 'utf8');

const gate = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/CreateVideoActionContractGate.cs');
const exec = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs');
const ids = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkIds.cs');
const permit = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionPermit.cs');
const selection = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionAiSelection.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01RealFilmReadinessV1Regression.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentProductionController.cs');
const api = read('../../shared/api/content.api.ts');
const paid = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionPaidNetworkPermit.cs');

ok(ids.includes('TIMING_PRODUCTION_DECISION_REQUIRED'), 'T06 timing reason exists');
ok(ids.includes('MISSING_PRODUCTION_AI_SELECTION'), 'T15 selection reason exists');
ok(selection.includes('runway') === false && selection.includes('EXPLICIT'), 'T09 selection is explicit, not silent runway map');
ok(exec.includes('GetActiveAsync') && exec.includes('ProductionAiSelectionStages.Video'), 'T09 evaluate reads OS selection');
ok(exec.includes('GetIssuedAsync') && !exec.includes('TryConsumeAsync'), 'T12 evaluate does not consume');
ok(exec.includes('TimingProductionDecisionRequired'), 'T14 official film missing timing');
ok(permit.includes('ISSUED') && permit.includes('CONSUMED'), 'T11 permit states');
ok(controller.includes('ai-selections') && controller.includes('execution-permits'), 'API selection + permit');
ok(controller.includes('create-video-input-preview'), 'T30 input preview GET');
ok(api.includes('upsertProductionAiSelection') && api.includes('issueProductionExecutionPermit'), 'client readiness APIs');
ok(create.includes('OfficialFilmForbidden'), 'T16 execute guard remains');
ok(!create.includes('ALLOW_OFFICIAL_FILM'), 'no global official-film bypass');
ok(paid.includes('ProductionTestSandboxIds.ScopeId'), 'TEST paid permit unchanged');
ok(!exec.includes('wan →') && !exec.includes('wan-2.1'), 'T10 no wan remap in evaluate');
ok(!regression.includes('svc.CreateVideoAsync') && !regression.includes('CreateVideoAsync(new'), 'T24 readiness suite does not execute CREATE_VIDEO');
ok(gate.includes('claim?.TakeUrl') && gate.includes('claim?.LegacyGraphReady'), 'T29 gate ignores legacy graph');
ok(regression.includes('TIMING_PRODUCTION_DECISION_REQUIRED'), 'T13 live expected block named');

if (fail.length) {
  console.error(`FAMIXA_REAL_FILM_READINESS_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_REAL_FILM_READINESS_V1 ok');
