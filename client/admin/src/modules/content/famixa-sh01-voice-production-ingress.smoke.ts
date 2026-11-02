/** FAMIXA_SH01_VOICE_PRODUCTION_INGRESS_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const src = (rel: string) =>
  readFileSync(join(root, '../../../../../src', rel), 'utf8');

const canon = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01VoiceIngressCanon.cs');
const freeze = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/PerformanceContractService.cs');
const evalSrc = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs');
const ready = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionReadinessAuthority.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
const api = src('KitPlatform.Api/Controllers/Content/ContentProductionController.cs');

ok(canon.includes('line-SC01-CHAR-003-1'), 'T03 lineId');
ok(canon.includes('Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?'), 'T02 exact text');
ok(canon.includes('CHAR-003') && canon.includes('kOIUKJ9E0DPndqTtvJm5'), 'T04/T05 speaker + VoiceId');
ok(canon.includes('FamixaProviderIds.ElevenLabs') && canon.includes('eleven_v3'), 'T06/T07 provider/model');
ok(ready.includes('MaterializeVoiceProductionIngressAsync') && ready.includes('parentSnapshotId'), 'T09 versioned snapshot');
ok(!freeze.includes('ProductionDurationSec == source.ProductionDurationSec'), 'T09 duration short-circuit removed');
ok(evalSrc.includes('EvaluateOfficialCreateVoiceAsync') && evalSrc.includes('ProductionAiSelectionStages.Voice'), 'T17 official resolve');
ok(create.includes('OfficialFilmForbidden') && create.includes('ExecuteFrozenAsync'), 'execute still fail-closed');
ok(api.includes('voice-production-ingress'), 'ingress API');
ok(!ready.includes('SynthesizeMp3Async') && !ready.includes('ExecuteFrozenAsync'), 'T22 no provider in ingress');

if (fail.length) {
  console.error(`FAMIXA_SH01_VOICE_PRODUCTION_INGRESS_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_VOICE_PRODUCTION_INGRESS_V1 PASS FAIL=0 (no provider)');
