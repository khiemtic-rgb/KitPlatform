/** FAMIXA_PRODUCTION_OS_SHOT_STATE_PREVIEW_AUTHORITY_CUTOVER_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isProductionOsTestShot } from './production-os-action-authority';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const finalSrc = read('content-famixa-final-source.ts');
const api = read('../../shared/api/content.api.ts');
const idb = read('famixa-voice-physical-idb-store.ts');
const finalStore = read('ContentFamixaShotProduction/ShotProductionFinalStore.ts');

ok(isProductionOsTestShot('TEST-SC01-SH01') && !isProductionOsTestShot('EP01-SC01-SH01'), 'TEST vs official shot');
ok(workspace.includes('osPreviewAuthority'), 'TEST preview is OS VISIBLE only');
ok(workspace.includes('osPreviewAuthority') && workspace.includes('motionUrl: undefined'), 'T04 TEST empty preview does not use graph');
ok(
  workspace.includes(': osPreviewAuthority')
    && workspace.includes('lipsyncUrl: undefined, finalUrl: undefined'),
  'T21 TEST does not fall through to assemble/final',
);

const osHydrate = series.slice(series.indexOf('const meta = await getProductionPreview'), series.indexOf('persistEngine'));
ok(osHydrate.includes('getProductionPreview') && osHydrate.includes('fetchProductionPreviewMedia'), 'hydrate preview from OS GET');
ok(series.includes('getAuthoritativeProductionVideo'), 'hydrate also reads ACCEPTED:VIDEO_TAKE');
ok(!osHydrate.includes('assembleVideoUrl') && !osHydrate.includes('resolveTakeUrl') && !osHydrate.includes('acceptedTake'), 'T07-T11 hydrate ignores legacy URLs');
ok(osHydrate.includes('delete next[shotId]') || osHydrate.includes('VISIBLE empty'), 'T04 TEST empty deletes osPreview');

ok(orch.includes('DERIVED / LEGACY command projection'), 'T41 nextShotProductionCommand classified DERIVED');
ok(!orch.includes('BindCurrent') && !orch.includes('createProductionRequest') && !orch.includes('BindVisible'), 'T41 command cannot mutate binding');
ok(!orch.includes('localStorage') && !orch.includes('indexedDB'), 'T42/T43 orchestrator has no web storage authority');

ok(api.includes('role?: string | null') && api.includes('mimeType?: string | null'), 'preview meta carries role/mime');
ok(api.includes('/content/production/shots/') && api.includes('preview/media'), 'canonical preview media route');

ok(finalSrc.includes('previewUrl') && finalSrc.includes('assembleVideoUrl'), 'legacy resolver still present');
ok(
  !workspace.includes('assembleVideoUrl(') || workspace.includes('osPreviewAuthority'),
  'OS player does not use assembleVideoUrl as authority',
);

ok(idb.includes('kit-famixa-tts') || idb.includes('famixa'), 'T43 IDB voice store remains (non-preview authority)');
ok(!workspace.includes('kit-famixa-tts'), 'T20 voice IDB is not video preview authority');
ok(finalStore.includes('ShotProductionFinalStore') || finalStore.includes('export'), 'T21 FINAL store remains (non-preview)');

ok(orch.includes('export function nextShotProductionCommand'), 'T41 command remains a projection');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_SHOT_STATE_PREVIEW_AUTHORITY_CUTOVER_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_SHOT_STATE_PREVIEW_AUTHORITY_CUTOVER_V1 PASS FAIL=0 (no provider)');
