/** FAMIXA_PRODUCTION_OS_OFFICIAL_PREVIEW_INTEGRITY_V1 — 01B official preview OS-only + SHA. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { looksLikeProductionShotCode } from './production-os-action-authority';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const here = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(here, name), 'utf8');
const src = (rel: string) => readFileSync(join(here, '../../../../../src', rel), 'utf8');

const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const integrity = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const finalStore = read('ContentFamixaShotProduction/ShotProductionFinalStore.ts');
const idb = read('famixa-voice-physical-idb-store.ts');

ok(looksLikeProductionShotCode('EP01-SC01-SH01') && looksLikeProductionShotCode('TEST-SC01-SH01'), 'official + TEST shot codes');
ok(workspace.includes('looksLikeProductionShotCode(current.id) || osAcceptedVideo'), 'T4 official preview authority is OS-only');
ok(workspace.includes('osPreviewAuthority') && workspace.includes('motionUrl: undefined'), 'T4 empty official preview does not use graph');
ok(
  workspace.includes(': osPreviewAuthority') &&
    workspace.includes('lipsyncUrl: undefined, finalUrl: undefined'),
  'T5/T6 official empty does not use FinalStore / lipsync / acceptedTake',
);
ok(workspace.includes("osPreviewAuthority") && workspace.includes("(osVoiceUrl || '').trim() || undefined"), 'T7 official voice is OS URL only');
ok(!workspace.includes('kit-famixa-tts'), 'NEGATIVE IDB blob != official preview authority');
ok(finalStore.includes('ShotProductionFinalStore') || finalStore.includes('export'), 'FinalStore remains non-official');
ok(idb.includes('kit-famixa-tts') || idb.includes('famixa'), 'IDB remains non-official cache');
ok(integrity.includes('ReadVerifiedPhysicalBytes') && integrity.includes('Sha256Hex'), 'physical bytes hashed');
ok(create.includes('OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes'), 'ReadPreview/Voice use SHA gate');
ok(!integrity.includes('takeUrl') && !integrity.includes('previewUrl') && !integrity.includes('acceptedTake'), 'NEGATIVE graph URL != official preview authority');
ok(!integrity.includes('ByteSha256 ='), 'NEGATIVE mismatch cannot mutate ByteSha256');
ok(!create.includes('executeVideoGenerationExecution'), 'NEGATIVE VGE URL != official preview authority');
ok(series.includes('fetchProductionPreviewMedia') && series.includes('delete next[shotId]'), 'SHA/media fail clears official preview');
ok(!series.slice(series.indexOf('const meta = await getProductionPreview'), series.indexOf('persistEngine')).includes('acceptedTake'), 'hydrate ignores acceptedTake');
ok(!series.slice(series.indexOf('const meta = await getProductionPreview'), series.indexOf('persistEngine')).includes('takeUrl'), 'hydrate ignores takeUrl');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_OFFICIAL_PREVIEW_INTEGRITY_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_OFFICIAL_PREVIEW_INTEGRITY_V1 PASS FAIL=0 (no provider)');
