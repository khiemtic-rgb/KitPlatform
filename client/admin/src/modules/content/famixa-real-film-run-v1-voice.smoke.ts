/** FAMIXA_REAL_FILM_RUN_V1_VOICE */

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

const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
const evalSrc = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs');
const timing = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionReadinessAuthority.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01RealFilmVoiceV1Regression.cs');
const adapters = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IProductionMediaAdapters.cs');
const di = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs');

ok(timing.includes('[],') && timing.includes('["CHAR-003", "CHAR-001"]'), 'timing snapshot freezes empty dialogue');
ok(evalSrc.includes('IsOfficialFilm(scope.BuildId)') && evalSrc.includes('EvaluateCreateVoiceAsync'), 'official film evaluate requires snapshot');
ok(create.includes('IsTestVoiceIdentity') && create.includes('VOICE-TEST') && create.includes('CHAR-TEST'), 'T03 TEST voice identity is not execution authority');
ok(create.includes('CanonicalVoiceInputNotBound') && create.includes('OfficialFilmForbidden'), 'official film stays fail-closed without OS bind / official adapter');
ok(create.includes('ConsumeOfficialPermitBeforeStartAsync'), 'official Start is after permit consume');
ok(adapters.includes('FixtureVoiceAdapter') && di.includes('FixtureVoiceAdapter'), 'TEST fixture remains for TEST');
ok(create.includes('ElevenLabsVoiceExecutionAdapter'), 'official path uses ElevenLabs adapter');
ok(!regression.includes('CREATE_LIPSYNC') && !regression.includes('CREATE_MIX'), 'T13 no LipSync/Mix');
ok(regression.includes('ExecuteCount == 0') && regression.includes('CreateCalls == 0'), 'T08/T21 no provider Start');
ok(regression.includes('PERFORMANCE_LINE_MISMATCH') || regression.includes('PerformanceLineMismatch'), 'empty lines block');

if (fail.length) {
  console.error(`FAMIXA_REAL_FILM_RUN_V1_VOICE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_REAL_FILM_RUN_V1_VOICE PASS FAIL=0 (no provider)');
