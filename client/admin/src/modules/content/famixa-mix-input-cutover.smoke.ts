/** FAMIXA_PRODUCTION_OS_MIX_INPUT_CUTOVER_V1 */

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
const series = read('ContentFamixaSeriesTab.tsx');
const api = read('../../shared/api/content.api.ts');

ok(isProductionOsTestShot('TEST-SC01-SH01') && !isProductionOsTestShot('EP01-SC01-SH01'), 'TEST vs official');
ok(api.includes('lipsyncArtifactId'), 'GET actions/state expose LipSync id');

const osExec = series.slice(series.indexOf('executeProductionOsAction'), series.indexOf('const resolveLineAudio'));
ok(osExec.includes('MIX_VIDEO') && osExec.includes('lipsyncArtifactId'), 'OS MIX sends LipSync artifactId');
ok(!osExec.includes('lipsyncUrl') && !osExec.includes('loadTtsBlobAny') && !osExec.includes('acceptedTake'), 'OS MIX has no URL/IDB');
ok(osExec.includes("action === 'MIX_VIDEO' && !lipsyncId"), 'MIX refuses without LipSync id');
ok(series.includes("productionOsLegacyRefuseNote('MIX_VIDEO'"), 'legacy assemble refused');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_MIX_INPUT_CUTOVER_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_MIX_INPUT_CUTOVER_V1 PASS FAIL=0 (no Fal)');
