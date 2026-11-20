/** FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_VOICE_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  OFFICIAL_SH02_LINE_ID,
  OFFICIAL_SH02_VOICE_ID,
  prepareOfficialVoiceIngress,
  VOICE_PRODUCTION_OS_NOT_READY,
} from './production-os-action-authority';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const src = (rel: string) => readFileSync(join(root, '../../../../../src', rel), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const api = read('../../shared/api/content.api.ts');
const ready = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionReadinessAuthority.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
const canon = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh02VoiceIngressCanon.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentProductionController.cs');

ok(canon.includes('line-SC01-CHAR-001-2'), 'SH02 lineId');
ok(canon.includes('ywBZEqUhld86Jeajq94o'), 'SH02 CHAR-001 voiceId');
ok(!canon.includes('kOIUKJ9E0DPndqTtvJm5') || canon.includes('IsSh01VoiceId'), 'does not copy SH01 voice as identity');
ok(ready.includes('MaterializeSh02VoiceProductionIngressAsync'), 'SH02 ingress method');
ok(create.includes('ProductionEp01Sh02VoiceIngressCanon.VideoParentArtifactId'), 'CREATE_VOICE binds VIDEO_TAKE parent');
ok(create.includes('b44e530b-5b59-422f-9b80-e058a3a639b5') || canon.includes('b44e530b-5b59-422f-9b80-e058a3a639b5'), 'locked parent id');
ok(controller.includes('MaterializeSh02VoiceProductionIngressAsync'), 'API dispatches SH02 ingress');
ok(api.includes('materializeVoiceProductionIngress') && api.includes('voice-production-ingress'), 'client ingress API');
ok(api.includes('fetchProductionVoiceMedia'), 'client voice media');
ok(series.includes('materializeVoiceProductionIngress') && series.includes("action: 'CREATE_VOICE'"), 'CTA executes CREATE_VOICE');
ok(series.includes('authorizePaidNetwork: true'), 'official paid authorize');
ok(!series.slice(series.indexOf('const prepareOfficialVoice'), series.indexOf('const ensureShotVoice')).includes('ENSURE_VOICE'), 'no ENSURE_VOICE');
ok(workspace.includes('data-os-voice-complete') && workspace.includes('VOICE: ✓ Đã có thoại'), 'Director VOICE complete');
ok(workspace.includes('osVoiceUrl'), 'player uses OS voice URL');
ok(OFFICIAL_SH02_LINE_ID === 'line-SC01-CHAR-001-2', 'client lineId matches canon');
ok(OFFICIAL_SH02_VOICE_ID === 'ywBZEqUhld86Jeajq94o', 'client voiceId matches canon');

const readySh02 = prepareOfficialVoiceIngress({
  buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
  shotCode: 'EP01-SC01-SH02',
  take: {
    empty: false,
    artifactId: 'b44e530b-5b59-422f-9b80-e058a3a639b5',
    role: 'VIDEO_TAKE',
    acceptanceStatus: 'ACCEPTED',
  },
});
ok(readySh02.execute === true && readySh02.voiceId === OFFICIAL_SH02_VOICE_ID, 'SH02 execute when parent accepted');
ok(
  prepareOfficialVoiceIngress({
    buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
    shotCode: 'EP01-SC01-SH01',
    take: {
      empty: false,
      artifactId: 'b44e530b-5b59-422f-9b80-e058a3a639b5',
      role: 'VIDEO_TAKE',
      acceptanceStatus: 'ACCEPTED',
    },
  }).execute === false,
  'SH01 stays preflight-only',
);
ok(
  prepareOfficialVoiceIngress({
    buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
    shotCode: 'EP01-SC01-SH02',
    take: { empty: true },
  }).reason === VOICE_PRODUCTION_OS_NOT_READY,
  'missing take fail-closed',
);

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_VOICE_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_VOICE_V1 PASS FAIL=0 (no provider)');
