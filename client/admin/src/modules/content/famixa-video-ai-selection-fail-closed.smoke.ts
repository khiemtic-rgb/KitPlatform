/** FAMIXA_VIDEO_AI_SELECTION_FAIL_CLOSED_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DIRECTOR_VIDEO_MODEL_UNRESOLVED,
  directorVideoSelectionFromRow,
  formatDirectorVideoModelLabel,
  hydrateDirectorVideoSelection,
  isConfirmedDirectorVideoSelection,
  productionAiSelectionHttpStatus,
  productionScopeFromShotCode,
  providerModelFromEngine,
  revertDirectorVideoSelectionAfterPostFailure,
} from './famixa-director-video-model-display';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const picker = read('FamixaMotionEnginePicker.tsx');
const persist = series.slice(series.indexOf('const persistEngine'), series.indexOf('const persistDirectorTiming'));
const hydrate = series.slice(series.indexOf('const scope = productionScopeFromShotCode(shotId);'), series.indexOf('const persistEngine'));

ok(providerModelFromEngine('veo').providerId === 'google' && providerModelFromEngine('veo').modelId === 'veo-3.1', 'veo maps google/veo-3.1');
ok(formatDirectorVideoModelLabel(undefined, undefined) === DIRECTOR_VIDEO_MODEL_UNRESOLVED, 'empty is Chưa xác định');
ok(!isConfirmedDirectorVideoSelection({ providerId: 'google', modelId: 'veo-3.1' }), 'optimistic without selectionId is not confirmed');
ok(
  isConfirmedDirectorVideoSelection({
    providerId: 'google',
    modelId: 'veo-3.1',
    selectionId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee',
  }),
  'server row with selectionId is confirmed',
);

ok(directorVideoSelectionFromRow({ providerId: 'google', modelId: 'veo-3.1' }) === undefined, 'POST body without selectionId is invalid');
ok(
  directorVideoSelectionFromRow({
    providerId: 'google',
    modelId: 'veo-3.1',
    selectionId: 'sel-1',
  })?.selectionId === 'sel-1',
  'valid POST/GET row commits',
);

ok(
  hydrateDirectorVideoSelection({ status: 'missing', previous: { providerId: 'google', modelId: 'veo-3.1' } }) === undefined,
  'GET 404 clears optimistic local',
);
ok(
  hydrateDirectorVideoSelection({
    status: 'ok',
    row: { providerId: 'google', modelId: 'veo-3.1', selectionId: 'sel-1' },
  })?.selectionId === 'sel-1',
  'GET 200 uses server',
);
ok(
  hydrateDirectorVideoSelection({
    status: 'error',
    previous: { providerId: 'google', modelId: 'veo-3.1' },
  }) === undefined,
  'network error does not keep unconfirmed local',
);
ok(
  hydrateDirectorVideoSelection({
    status: 'error',
    previous: { providerId: 'runway', modelId: 'gen4_turbo', selectionId: 'sh01' },
  })?.selectionId === 'sh01',
  'network error keeps confirmed canonical',
);

ok(revertDirectorVideoSelectionAfterPostFailure({ providerId: 'google', modelId: 'veo-3.1' }) === undefined, 'POST fail with no prior → undefined');
ok(
  revertDirectorVideoSelectionAfterPostFailure({
    providerId: 'runway',
    modelId: 'gen4_turbo',
    selectionId: 'sh01',
  })?.providerId === 'runway',
  'POST fail reverts to prior canonical',
);
ok(productionAiSelectionHttpStatus({ response: { status: 404 } }) === 404, '404 detection');
ok(productionScopeFromShotCode('EP01-SC01-SH02').episodeCode === 'EP01', 'official SH02 scope');
ok(productionScopeFromShotCode('TEST-SC01-SH02').episodeCode === 'TEST', 'TEST SH02 isolated scope');

ok(persist.includes('upsertProductionAiSelection'), 'persist POSTs ai-selections');
ok(persist.includes('directorVideoSelectionFromRow'), 'persist commits server row only');
ok(!persist.includes('applyDirectorVideoModelSelection(m[shotId], mapped)'), 'persist does not optimistic-write mapped engine');
ok(persist.includes('revertDirectorVideoSelectionAfterPostFailure'), 'POST fail reverts');
ok(persist.includes('message.error') && persist.includes('Không lưu được Model video'), 'POST fail toast');
ok(!persist.includes('/* selection display stays local'), 'no swallow comment');
ok(
  !persist.includes('createProductionRequest')
    && !persist.includes("action: 'CREATE_VIDEO'")
    && !persist.includes('issueProductionExecutionPermit')
    && !persist.includes('materializeProductionDirectorTiming'),
  'no CREATE_VIDEO / permit / timing on persist',
);
ok(!persist.includes('4ef164d6') && !persist.includes('EP01-SC01-SH01'), 'no SH01 hardcode');

ok(hydrate.includes('hydrateDirectorVideoSelection'), 'hydrate uses fail-closed helper');
ok(hydrate.includes("status: (productionAiSelectionHttpStatus(error) === 404 ? 'missing' : 'error')"), '404 vs network');
ok(!hydrate.includes('osVideoModelByShotRef.current[shotId]?.selection;'), 'hydrate no raw local fallback');
ok(picker.includes('data-current-video-model') && picker.includes('selectionProviderId'), 'picker still shot-scoped selection');
ok(picker.includes('void _engine'), 'global engine does not paint Model video');

if (fail.length) {
  console.error(`FAMIXA_VIDEO_AI_SELECTION_FAIL_CLOSED_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VIDEO_AI_SELECTION_FAIL_CLOSED_V1 ok');
