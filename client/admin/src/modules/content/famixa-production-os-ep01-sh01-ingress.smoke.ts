/** FAMIXA_PRODUCTION_OS_EP01_SH01_PRODUCTION_INGRESS_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const svc = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const gate = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/CreateVideoActionContractGate.cs');
const canon = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01IngressCanon.cs');
const ingress = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01ProductionIngressV1Regression.cs');
const api = read('../../shared/api/content.api.ts');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentProductionController.cs');

ok(canon.includes('4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f'), 'T01 EP01 build');
ok(canon.includes('EP01-SC01-SH01'), 'T02 SH01');
ok(canon.includes('Minh bước vào'), 'T03 Story source');
ok(canon.includes('Linh looks toward Minh'), 'T06 ActingBeat source');
ok(canon.includes('CHAR-003') && canon.includes('35mm'), 'T09 VisualContract source');
ok(!canon.includes('CREATE_MASTER') && !canon.includes('Visual Universe Authority'), 'T11 no VA rewrite');
ok(canon.includes('picture:EP01-SC01-SH01:007'), 'T18 pictureRevisionId');
ok(canon.includes('hf82234a0:125347'), 'T15 kfSourceHash');
ok(canon.includes('7a158fa6bcfa7e914852ac617abfcc045fd07f2842ce51bb425cf02659e783e9'), 'T17 physical SHA');
ok(svc.includes('IngressProvenanceJson') && svc.includes('existing-pixels'), 'T19 CREATE_IMAGE existing-pixels');
ok(svc.includes('import') && !svc.includes('wan-2.1'), 'T27 no silent wan map in RegisterKeyframe');
ok(!svc.includes('graph_json') && !svc.includes('kfApproved'), 'T34 CREATE_IMAGE does not read graph');
ok(gate.includes('claim?.KfApproved') && gate.includes('claim?.TakeUrl') && gate.includes('claim?.LegacyGraphReady'), 'T34 gate ignores graph flags');
ok(controller.includes('ProductionActions.CreateImage') && controller.includes('existing-pixels'), 'T19 POST /keyframes import');
ok(api.includes('ingressProvenanceJson') && api.includes('pictureRevisionId'), 'client registerProductionKeyframe provenance');
ok(ingress.includes('MISSING_TIMING') && ingress.includes('OfficialFilmForbidden'), 'T32 remaining blockers named');
ok(!ingress.includes('svc.CreateVideoAsync') && !ingress.includes('CreateVideoAsync(new'), 'T30 ingress does not execute CREATE_VIDEO');
ok(canon.includes('LEGACY_INGRESS'), 'T05 sourceType');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_EP01_SH01_PRODUCTION_INGRESS_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_EP01_SH01_PRODUCTION_INGRESS_V1 ok');
