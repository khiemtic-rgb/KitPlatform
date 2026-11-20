/** FAMIXA_PRODUCTION_OS_CREATE_VOICE_CUTOVER_V1 */

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
const authority = read('production-os-action-authority.ts');
const idb = read('famixa-voice-physical-idb-store.ts');
const tts = read('content-famixa-tts-store.ts');
const identity = read('famixa-voice-physical-identity.ts');

ok(isProductionOsTestShot('TEST-SC01-SH01') && !isProductionOsTestShot('EP01-SC01-SH01'), 'TEST vs official shot');
ok(authority.includes('resolveCurrentVoiceArtifactId') && authority.includes('Never kit-famixa-tts'), 'OS voice resolver helper');
ok(resolveCurrentVoiceArtifactId({ nodes: [{ node: 'VOICE', status: 'CURRENT', artifactId: 'art-a' }], actions: [] }) === 'art-a', 'T19 helper returns VOICE node');
ok(resolveCurrentVoiceArtifactId({ nodes: [{ node: 'VOICE', status: 'MISSING' }], actions: [] }) === undefined, 'T32 helper empty without artifact');

ok(api.includes('getProductionCurrentVoice') && api.includes('/voice'), 'GET /voice client');
ok(api.includes('ProductionVoiceArtifactMeta'), 'voice artifact meta');

const osExec = series.slice(series.indexOf('executeProductionOsAction'), series.indexOf('const resolveLineAudio'));
ok(osExec.includes('getProductionCurrentVoice'), 'OS CREATE_LIPSYNC calls GET /voice');
ok(osExec.includes('resolveCurrentVoiceArtifactId'), 'OS path uses OS voice helper');
ok(!osExec.includes('loadTtsBlobAny') && !osExec.includes('resolveLineAudio'), 'T22 OS execute does not use IDB');
ok(osExec.includes('voiceArtifactId: action === \'CREATE_LIPSYNC\' ? voiceArtId'), 'lipsync sends VoiceArtifactId');

ok(series.includes('LEGACY / CACHE / RECOVERY') && series.includes('resolveLineAudio'), 'official resolveLineAudio classified');
ok(idb.includes('CACHE / RECOVERY') && idb.includes('kit-famixa-tts'), 'T34 IDB remains as cache');
ok(tts.includes('CACHE / RECOVERY') && tts.includes('NOT Production OS'), 'T35 TTS store not authority');
ok(identity.includes('artifactId') && identity.includes('buildId'), 'legacy VoicePhysical identity remains');

ok(!authority.includes('class CurrentVoice') && !authority.includes('VoiceOrchestrator'), 'T41/T42 no second voice machine');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_CREATE_VOICE_CUTOVER_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_CREATE_VOICE_CUTOVER_V1 PASS FAIL=0 (no provider)');
