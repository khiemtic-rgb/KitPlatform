/** AUTHORITY-STABILIZATION-01E-GAP-CLOSURE — legacy direct routes do not submit a provider job. 0 providers. */

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

const between = (text: string, start: string, end: string) => {
  const from = text.indexOf(start);
  const to = text.indexOf(end, from + start.length);
  return from >= 0 && to > from ? text.slice(from, to) : '';
};

const turbo = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs');
const vge = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const actions = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
const progress = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionProgressService.cs');
const authority = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionAuthorityV1.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionAuthorityV1Regression.cs');
const cutover = src('Packs/Content/KitPlatform.Packs.Content.Application/SeriesGraphMediaWriteCutover.cs');
const integrity = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentController.cs');
const series = read('ContentFamixaSeriesTab.tsx');
const accept = read('famixa-accept-existing-authority.ts');
const view = read('kit-video-production-progress.ts');

const closed = 'LegacyProviderSubmitClosed';
const start = between(turbo, 'public async Task<ContentSeriesTurboTaskDto> StartAsync', 'public async Task<ContentSeriesTurboTaskDto> GetAsync');
const poll = between(turbo, 'public async Task<ContentSeriesTurboTaskDto> GetAsync', 'public async Task<ContentSeriesTurboTaskDto> StartLipsyncAsync');
const lipsync = between(turbo, 'public async Task<ContentSeriesTurboTaskDto> StartLipsyncAsync', 'private static bool IsAllowedLipsyncVideoRef');
const execute = between(vge, 'public async Task<VideoGenerationExecutionDto> ExecuteAsync', 'public async Task<VideoGenerationExecutionDto> ApproveAsync');
const vgeGet = between(vge, 'public async Task<VideoGenerationExecutionDto> GetAsync', 'public async Task<VideoGenerationExecutionDto> ExecuteAsync');
const attach = between(series, 'const attachLipsync', 'const hydrateCutVoice');

ok(start.includes(closed) && start.indexOf(closed) < start.indexOf('.StartAsync('), 'T1/T4 POST turbo throws before motion StartAsync');
ok(lipsync.includes(closed) && lipsync.indexOf(closed) < lipsync.indexOf('_fal.UploadAsync') && lipsync.indexOf(closed) < lipsync.indexOf('.StartAsync('), 'T2/T5 POST lipsync throws before Fal upload and LipSync StartAsync');
ok(execute.includes(closed) && execute.indexOf(closed) < execute.indexOf('ResumeAsync') && execute.indexOf(closed) < execute.indexOf('GenerateAsync'), 'T3/T6/T19 VGE execute throws before resume or generate');
ok(poll.includes('.GetAsync(') && !poll.includes('.StartAsync(') && !poll.includes('UploadAsync'), 'T7/T20 turbo GET polls and does not submit');
ok(series.includes('EDITORIAL. Pastes a Fal file') && !attach.includes('BindNamedAsync') && !attach.includes('createProductionRequest'), 'T8/T10 attachLipsync stays editorial');
ok(vgeGet.includes('GetLatestAsync') && !vgeGet.includes('GenerateAsync') && !vgeGet.includes('ResumeAsync'), 'T9/T20 VGE GET does not submit');
ok(create.includes('FreezeInputAsync') && create.includes('StartFrozenAsync') && create.includes('CreateVoiceAsync') && actions.includes('CreateLipsyncAsync') && actions.includes('AcceptOsAsync'), 'T11/T12/T13/T14 official OS actions remain');
ok(progress.includes('ApplyOfficialOsProgress') && !progress.includes('VideoGenerationExecutionRepository') && !view.includes('video-generation-execution'), 'T15 progress stays OS-only');
ok(create.includes('ReadVerifiedPhysicalBytes') && integrity.includes('ReadVerifiedPhysicalBytes'), 'T16 preview and voice stay on the SHA gate');
ok(accept.includes('acceptedTake ≠ official accept authority') && !create.includes('graph_json') && !create.includes('acceptedTake'), 'T17/T18 acceptedTake and graph media stay non-authority');
ok(cutover.includes('acceptedTake'), 'T24 01A media list remains');
ok(authority.includes('LegacyDirectMaySubmitProviderJob') && regression.includes('does not submit a provider job'), 'T28 01E regression records the submit cutoff');
ok(!authority.includes('CREATE TABLE') && !authority.includes('interface IMotion') && !regression.includes('class GenericExecution'), 'T21/T22/T23 no schema, adapter contract, or generic engine');
ok(controller.includes('catch (InvalidOperationException ex)') && controller.includes('return BadRequest(new { message = ex.Message })'), 'turbo and lipsync refusal uses the existing BadRequest message');
ok(vge.includes('GetLatestAsync'), 'VGE history reader remains');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_EXECUTION_CUTOFF_01E FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_EXECUTION_CUTOFF_01E PASS FAIL=0 (no provider)');
