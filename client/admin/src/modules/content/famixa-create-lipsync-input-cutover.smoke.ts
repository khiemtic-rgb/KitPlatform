/** FAMIXA_PRODUCTION_OS_CREATE_LIPSYNC_INPUT_CUTOVER_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isProductionOsTestShot, resolveCurrentVoiceArtifactId } from './production-os-action-authority';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const api = read('../../shared/api/content.api.ts');
const tts = read('content-famixa-tts-store.ts');
const idb = read('famixa-voice-physical-idb-store.ts');

ok(isProductionOsTestShot('TEST-SC01-SH01') && !isProductionOsTestShot('EP01-SC01-SH01'), 'TEST vs official');
ok(api.includes('videoArtifactId') && api.includes('voiceArtifactId'), 'GET actions/state expose VIDEO+VOICE ids');
ok(api.includes('getProductionCurrentVoice'), 'GET /voice remains');

const osExec = series.slice(series.indexOf('executeProductionOsAction'), series.indexOf('const resolveLineAudio'));
ok(osExec.includes('motionArtifactId') && osExec.includes('voiceArtifactId'), 'OS POST sends artifactIds');
ok(osExec.includes('CREATE_LIPSYNC') && osExec.includes('lineId'), 'OS lipsync sends lineId');
ok(!osExec.includes('loadTtsBlobAny') && !osExec.includes('acceptedTake') && !osExec.includes('takeUrl'), 'T18/T44 OS execute has no IDB/URL');
ok(osExec.includes('getProductionCurrentVoice') || osExec.includes('resolveCurrentVoiceArtifactId'), 'OS voice from Production OS');
ok(resolveCurrentVoiceArtifactId({ nodes: [{ node: 'VOICE', status: 'CURRENT', artifactId: 'v1' }], actions: [] }) === 'v1', 'helper is OS node');

ok(series.includes('LEGACY / CACHE / RECOVERY') && series.includes('resolveLineAudio'), 'legacy IDB classified');
ok(tts.includes('CACHE / RECOVERY') && idb.includes('CACHE / RECOVERY'), 'IDB stores remain cache');
ok(series.includes("productionOsLegacyRefuseNote('CREATE_LIPSYNC'"), 'legacy POST /series/lipsync refused');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_CREATE_LIPSYNC_INPUT_CUTOVER_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_CREATE_LIPSYNC_INPUT_CUTOVER_V1 PASS FAIL=0 (no Fal)');
