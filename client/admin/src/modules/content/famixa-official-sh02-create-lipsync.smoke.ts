/** FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_LIPSYNC_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  OFFICIAL_SH02_LINE_ID,
  OFFICIAL_SH02_SPEAKER_ID,
  OFFICIAL_SH02_VOICE_ARTIFACT_ID,
  OFFICIAL_SH02_VIDEO_TAKE_ID,
  prepareOfficialLipsyncIngress,
  LIPSYNC_PRODUCTION_OS_NOT_READY,
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
const canon = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionEp01Sh02LipsyncCanon.cs');
const evaluate = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs');
const controller = src('KitPlatform.Api/Controllers/Content/ContentProductionController.cs');

ok(canon.includes('0fde86c2-250f-4303-bda0-4913a6dbd1e4'), 'VOICE parent id');
ok(canon.includes('b44e530b-5b59-422f-9b80-e058a3a639b5'), 'VIDEO parent id');
ok(canon.includes('CHAR-001') && canon.includes('line-SC01-CHAR-001-2'), 'SH02 speaker + line');
ok(canon.includes('0.28') && canon.includes('0.38'), 'CK23 T3 pin remains historical');
ok(canon.includes('450') && canon.includes('720') && canon.includes('400'), 'CK23B shot-specific XYXY box');
ok(canon.includes('cut_off'), 'CK23B sync_mode cut_off');
ok(canon.includes('32bec770-9706-4aad-bc21-8db3d4f11d4c'), 'CK23 artifact remains immutable id');
ok(evaluate.includes('EvaluateOfficialSh02CreateLipsyncAsync'), 'evaluate uses official SH02 bind');
ok(evaluate.includes('GetAuthoritativeVideoTakeAsync'), 'evaluate uses ACCEPTED:VIDEO_TAKE');
ok(create.includes('ProductionEp01Sh02LipsyncCanon.MatchesScope'), 'CREATE_LIPSYNC official SH02 path');
ok(create.includes('GetAuthoritativeVideoTakeAsync'), 'CREATE_LIPSYNC uses video authority');
ok(ready.includes('MaterializeSh02LipsyncProductionIngressAsync'), 'SH02 lipsync ingress');
ok(controller.includes('lipsync-production-ingress'), 'API lipsync ingress');
ok(api.includes('materializeLipsyncProductionIngress'), 'client lipsync ingress API');
ok(series.includes('prepareOfficialLipsync') && series.includes("action: 'CREATE_LIPSYNC'"), 'CTA executes CREATE_LIPSYNC');
ok(series.includes('authorizePaidNetwork: true'), 'official paid authorize');
ok(!series.slice(series.indexOf('const prepareOfficialLipsync'), series.indexOf('const ensureShotVoice')).includes('ENSURE_LIPSYNC'), 'no ENSURE_LIPSYNC');
ok(workspace.includes('data-os-lipsync-complete') && workspace.includes('LIPSYNC: ✓ Đã khớp môi'), 'Director LIPSYNC complete');
ok(OFFICIAL_SH02_LINE_ID === 'line-SC01-CHAR-001-2', 'client lineId matches canon');
ok(OFFICIAL_SH02_SPEAKER_ID === 'CHAR-001', 'client speaker matches canon');
ok(OFFICIAL_SH02_VOICE_ARTIFACT_ID === '0fde86c2-250f-4303-bda0-4913a6dbd1e4', 'client voice artifact');
ok(OFFICIAL_SH02_VIDEO_TAKE_ID === 'b44e530b-5b59-422f-9b80-e058a3a639b5', 'client video artifact');

const readySh02 = prepareOfficialLipsyncIngress({
  buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
  shotCode: 'EP01-SC01-SH02',
  video: {
    empty: false,
    artifactId: OFFICIAL_SH02_VIDEO_TAKE_ID,
    role: 'VIDEO_TAKE',
    acceptanceStatus: 'ACCEPTED',
  },
  voice: {
    empty: false,
    artifactId: OFFICIAL_SH02_VOICE_ARTIFACT_ID,
    role: 'VOICE',
  },
});
ok(readySh02.execute === true && readySh02.lineId === OFFICIAL_SH02_LINE_ID, 'SH02 execute when parents accepted');
ok(
  prepareOfficialLipsyncIngress({
    buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
    shotCode: 'EP01-SC01-SH01',
    video: {
      empty: false,
      artifactId: OFFICIAL_SH02_VIDEO_TAKE_ID,
      role: 'VIDEO_TAKE',
      acceptanceStatus: 'ACCEPTED',
    },
    voice: { empty: false, artifactId: OFFICIAL_SH02_VOICE_ARTIFACT_ID, role: 'VOICE' },
  }).execute === false,
  'SH01 stays preflight-only',
);
ok(
  prepareOfficialLipsyncIngress({
    buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
    shotCode: 'EP01-SC01-SH02',
    video: { empty: true },
    voice: { empty: false, artifactId: OFFICIAL_SH02_VOICE_ARTIFACT_ID, role: 'VOICE' },
  }).reason === LIPSYNC_PRODUCTION_OS_NOT_READY,
  'missing take fail-closed',
);

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_LIPSYNC_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_LIPSYNC_V1 PASS FAIL=0 (no provider)');
