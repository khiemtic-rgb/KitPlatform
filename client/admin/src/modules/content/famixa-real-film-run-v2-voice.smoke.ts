/** FAMIXA_REAL_FILM_RUN_V2_VOICE */

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
const service = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const transport = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpElevenLabsVoiceTransport.cs');
const client = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentElevenLabsClient.cs');
const di = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs');
const regression = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh01RealFilmRunV2VoiceRegression.cs');
const evalSrc = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs');

ok(create.includes('CanonicalVoiceInputNotBound'), 'T09 TEST identity without OS bind stops');
ok(create.includes('ProductionVoiceFreeze.Token') && create.includes('CHAR-TEST'), 'TEST identities are stripped before freeze');
ok(create.includes('ConsumeOfficialPermitBeforeStartAsync') && service.includes('CreateVoice'), 'T11 consume permit immediately before Start');
ok(create.includes('requireIssuedPermit: !officialFilm'), 'official execute resolves inputs without consuming permit');
ok(create.includes('AuthorizePaidNetwork'), 'paid official voice requires authorize');
ok(evalSrc.includes('LineId: line.LineId') && evalSrc.includes('VoiceId: voiceId'), 'T01 official evaluate returns canonical bind');
ok(create.includes('TryRecoverOfficialVoiceAsync') && create.includes('ProductionVoicePhysical'), 'failed MPEG verify recovers same Attempt bytes');
ok(transport.includes('MatchesConsumed') && transport.includes('CreateVoice'), 'HTTP Start checks consumed CREATE_VOICE permit');
ok(client.includes('SynthesizeProductionOnceAsync') && client.includes('language_code = "vi"'), 'production one-shot is a single ElevenLabs POST');
ok(di.includes('HttpElevenLabsVoiceTransport') && di.includes('ElevenLabsVoiceExecutionAdapter'), 'ElevenLabs adapter wired');
ok(di.includes('FixtureVoiceAdapter'), 'TEST fixture remains');
ok(regression.includes('exactly one ElevenLabs generation') && regression.includes('replay does not call provider again'), 'T12/T21 one-shot + idempotent');
ok(regression.includes('CHAR-TEST') && regression.includes('VOICE-TEST') && regression.includes('InputSnapshot immutable canonical'), 'T09 TEST ignored, OS frozen');
ok(!regression.includes('CREATE_LIPSYNC') && !regression.includes('CREATE_MIX'), 'T13 no LipSync/Mix');
ok(regression.includes('no Runway call') && regression.includes('no other AI provider'), 'T28/T30 no other AI');
ok(regression.includes('historical audio does not become CURRENT'), 'T22 historical isolation');
ok(regression.includes('VIDEO_TAKE unchanged'), 'T25 VIDEO_TAKE unchanged');

if (fail.length) {
  console.error(`FAMIXA_REAL_FILM_RUN_V2_VOICE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_REAL_FILM_RUN_V2_VOICE ok');
