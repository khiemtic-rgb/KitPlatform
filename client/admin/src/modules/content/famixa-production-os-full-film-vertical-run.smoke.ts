/** FAMIXA_PRODUCTION_OS_FULL_FILM_VERTICAL_RUN_V1 */

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
const actions = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
const contract = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionActionContracts.cs');

ok(isProductionOsTestShot('TEST-SC01-SH01') && !isProductionOsTestShot('EP01-SC01-SH01'), 'TEST vs official');
ok(api.includes('assemblyArtifactIds') && api.includes('acceptedArtifactId'), 'API assemble uses artifact ids');

const osExec = series.slice(series.indexOf('executeProductionOsAction'), series.indexOf('const resolveLineAudio'));
ok(osExec.includes('ASSEMBLE') && osExec.includes('assemblyArtifactIds') && osExec.includes('acceptedArtifactId'),
  'OS ASSEMBLE sends accepted MIX artifactIds');
ok(!osExec.includes('lipsyncUrl') && !osExec.includes('takeUrl') && !osExec.includes('previewUrl')
  && !osExec.includes('acceptedTake') && !osExec.includes('ShotProductionFinalStore'),
  'OS ASSEMBLE has no URL/FinalStore');
ok(series.includes("productionOsLegacyRefuseNote('ASSEMBLE'"), 'legacy assemble refused');
ok(actions.includes('ProductionBindingRoles.Accepted') && actions.includes('CreateAssemblyAsync')
  && !actions.includes('lipsyncUrl') && !actions.includes('ShotProductionFinalStore'),
  'AssembleOs uses ACCEPTED artifact not URL');
ok(contract.includes('AcceptedArtifactIds') && contract.includes('No FINAL claim'),
  'Assemble contract: ACCEPTED ids, no FINAL role');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_FULL_FILM_VERTICAL_RUN_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_FULL_FILM_VERTICAL_RUN_V1 PASS FAIL=0 (no paid providers)');
