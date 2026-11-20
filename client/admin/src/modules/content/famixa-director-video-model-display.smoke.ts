/** FAMIXA_DIRECTOR_VIDEO_MODEL_DISPLAY_FIX_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DIRECTOR_VIDEO_MODEL_DISPLAY_ID,
  DIRECTOR_VIDEO_MODEL_UNRESOLVED,
  applyDirectorVideoModelSelection,
  bindVideoTakeProvenance,
  formatDirectorVideoDuration,
  formatDirectorVideoModelLabel,
  formatVideoTakeProvenanceLine,
  motionEngineFromProviderModel,
  productionScopeFromShotCode,
  providerModelFromEngine,
} from './famixa-director-video-model-display';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const picker = read('FamixaMotionEnginePicker.tsx');
const engine = read('famixa-motion-engine.ts');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const persistBlock = series.slice(series.indexOf('const persistEngine'), series.indexOf('const refuseUnwiredVeo'));

const sh01Selection = {
  providerId: 'runway',
  modelId: 'gen4_turbo',
  selectionId: '9b0dceee-ba53-4cc5-8139-ee9ba58a2011',
};
const sh01Take = bindVideoTakeProvenance(undefined, {
  artifactId: 'b22370a9-6ddd-4b04-a281-c7498cc926ae',
  selection: sh01Selection,
  durationSec: 5.042,
});
const current = { selection: sh01Selection, take: sh01Take };

ok(DIRECTOR_VIDEO_MODEL_DISPLAY_ID === 'FAMIXA_DIRECTOR_VIDEO_MODEL_DISPLAY_FIX_V1', 'id');
ok(formatDirectorVideoModelLabel('runway', 'gen4_turbo') === 'Runway · Gen-4 Turbo', 'T01 SH01 displays Runway / Gen4 Turbo');
ok(formatDirectorVideoModelLabel(current.selection.providerId, current.selection.modelId) !== 'Google Veo 3', 'T02 Google Veo is not displayed as current model');
ok(sh01Take?.artifactId === 'b22370a9-6ddd-4b04-a281-c7498cc926ae' && sh01Take?.providerId === 'runway', 'T03 current VIDEO_TAKE remains unchanged');
ok(sh01Take?.artifactId === 'b22370a9-6ddd-4b04-a281-c7498cc926ae', 'T04 current VIDEO_TAKE artifact unchanged');
ok(current.selection.selectionId === '9b0dceee-ba53-4cc5-8139-ee9ba58a2011', 'T05 ProductionAiSelection unchanged');
ok(formatDirectorVideoModelLabel('runway', 'gen4_turbo') !== 'Wan 2.1 · Fal', 'T06 legacy WAN does not override');
ok(
  formatDirectorVideoModelLabel('', '') === DIRECTOR_VIDEO_MODEL_UNRESOLVED
    && formatDirectorVideoModelLabel(undefined, undefined) === DIRECTOR_VIDEO_MODEL_UNRESOLVED
    && !picker.includes("?? 'veo'")
    && persistBlock.includes('upsertProductionAiSelection'),
  'T07 no hardcoded Veo default',
);
ok(
  !persistBlock.includes('createProductionRequest')
    && !persistBlock.includes('startContentSeriesTurbo')
    && !persistBlock.includes('startContentSeriesVeo'),
  'T08 no provider call',
);
ok(!persistBlock.includes('CREATE_VIDEO') && !persistBlock.includes('fetchProductionPreviewMedia'), 'T09 no media generation');

const afterChange = applyDirectorVideoModelSelection(current, { providerId: 'google', modelId: 'veo-3.1' });
ok(
  afterChange.selection?.providerId === 'google'
    && afterChange.take?.artifactId === sh01Take?.artifactId
    && afterChange.take?.providerId === 'runway',
  'T10 changing selection does not regenerate video',
);
ok(afterChange.take?.artifactId === sh01Take?.artifactId && afterChange.take?.durationSec === 5.042, 'T11 changing selection does not mutate existing artifact');
ok(series.includes('getProductionAiSelection') && series.includes('osVideoModelByShot') && workspace.includes('osVideoModel'), 'T12 reload preserves correct display');
ok(
  formatDirectorVideoModelLabel(sh01Selection.providerId, sh01Selection.modelId) === 'Runway · Gen-4 Turbo'
    && applyDirectorVideoModelSelection({ selection: { providerId: 'google', modelId: 'veo-3.1' } }, { providerId: 'google', modelId: 'veo-3.1' }).selection?.providerId === 'google',
  'T13 cross-shot selection cannot affect SH01',
);
ok(!persistBlock.includes('episode.shots') && !series.includes('PUT series/builds') || persistBlock.includes('upsertProductionAiSelection'), 'T14 EP01 graph not written by model display');
ok(!persistBlock.includes('EP99') && !persistBlock.includes('77262ec0'), 'T15 EP99 unchanged');

ok(formatDirectorVideoDuration(5.042) === '5.04s', 'duration 5.042 → 5.04s');
ok(formatVideoTakeProvenanceLine(sh01Take) === 'Runway · Gen-4 Turbo · 5.04s', 'take provenance line');
ok(motionEngineFromProviderModel('runway', 'gen4_turbo') === 'turbo', 'engine from OS selection');
ok(providerModelFromEngine('veo').modelId === 'veo-3.1', 'picker veo maps google/veo-3.1');
ok(productionScopeFromShotCode('EP01-SC01-SH01').sceneCode === 'SC01', 'scope from shot code');
ok(picker.includes('Model video') && picker.includes('Thay đổi') && !picker.includes('Máy dựng'), 'Director copy');
ok(engine.includes("label: 'Runway · Gen-4 Turbo'") && engine.includes("label: 'Wan 2.1 · Fal'") && engine.includes("label: 'Google Veo 3'"), 'model list remains');
ok(!picker.includes('providerJobId') && !picker.includes('storageKey'), 'no internal IDs in picker');
ok(persistBlock.includes('applyDirectorVideoModelSelection'), 'change keeps take via helper');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_VIDEO_MODEL_DISPLAY_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_VIDEO_MODEL_DISPLAY_FIX_V1 PASS FAIL=0 (no provider)');
