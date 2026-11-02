/** FAMIXA_PRODUCTION_OS_OFFICIAL_VIDEO_AUTHORITY_BRIDGE_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  prepareOfficialLipsyncIngress,
  prepareOfficialVoiceIngress,
  VIDEO_TAKE_CURRENT_INPUT,
  VOICE_PRODUCTION_OS_NOT_READY,
} from './production-os-action-authority';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const api = read('../../shared/api/content.api.ts');
const authority = read('production-os-action-authority.ts');

ok(api.includes('getAuthoritativeProductionVideo') && api.includes('authoritative-video'), 'HTTP client GET authoritative-video');
ok(series.includes('getAuthoritativeProductionVideo') && series.includes('osAuthoritativeByShot'), 'hydrate reads ACCEPTED:VIDEO_TAKE');
ok(!series.includes('osState.videoArtifactId || osState.currentArtifactId'), 'hydrate does not use KEYFRAME current as video');
ok(workspace.includes('data-os-video-complete') && workspace.includes('VIDEO: ✓ Đã hoàn thành'), 'UI complete from OS accepted');
ok(workspace.includes('osAcceptedVideo') && workspace.includes('osPreviewAuthority'), 'accepted take is preview authority');
ok(workspace.includes('onPrepareOsVoice') && workspace.includes('data-os-prepare-voice'), 'Nạp thoại OS preflight');
ok(series.includes('prepareOfficialVoiceIngress') && series.includes('VOICE_PRODUCTION_OS_NOT_READY'), 'CTA uses OS ingress');
ok(!series.slice(series.indexOf('const prepareOfficialVoice'), series.indexOf('const ensureShotVoice')).includes('ENSURE_VOICE'), 'CTA does not ENSURE_VOICE');
ok(series.includes("searchParams.get('build')") && series.includes("searchParams.get('shot')"), 'deep-link build+shot');

const accepted = prepareOfficialVoiceIngress({
  buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
  shotCode: 'EP01-SC01-SH02',
  take: {
    empty: false,
    artifactId: 'b44e530b-5b59-422f-9b80-e058a3a639b5',
    role: 'VIDEO_TAKE',
    acceptanceStatus: 'ACCEPTED',
  },
});
ok(accepted.ok && accepted.execute === true && accepted.artifactId === 'b44e530b-5b59-422f-9b80-e058a3a639b5', 'ingress sees SH02 VIDEO_TAKE');
ok(!accepted.reason, 'SH02 accepted take may execute CREATE_VOICE');
ok(
  prepareOfficialVoiceIngress({
    buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
    shotCode: 'EP01-SC01-SH02',
    take: { empty: true },
  }).execute === false,
  'missing take does not execute',
);
const recovered = prepareOfficialVoiceIngress({
  buildId: '2615b2da-da9e-486b-9372-8927784df40f',
  shotCode: 'EP01-SC01-SH01',
  take: {
    empty: false,
    artifactId: '167bad5e-35b1-443e-9a1d-c8aed9837aec',
    role: 'VIDEO_TAKE',
    acceptanceStatus: 'CURRENT',
  },
});
ok(recovered.ok && recovered.execute === false && recovered.reason === VIDEO_TAKE_CURRENT_INPUT, 'tenant CURRENT is video input');
ok(recovered.artifactId === '167bad5e-35b1-443e-9a1d-c8aed9837aec', 'tenant CURRENT keeps artifact');
ok(
  prepareOfficialLipsyncIngress({
    buildId: '2615b2da-da9e-486b-9372-8927784df40f',
    shotCode: 'EP01-SC01-SH02',
    video: {
      empty: false,
      artifactId: '99540a03-40d3-42ee-ace9-59cd45759a58',
      role: 'VIDEO_TAKE',
      acceptanceStatus: 'CURRENT',
    },
  }).execute === false,
  'tenant CURRENT does not execute lipsync',
);
ok(series.includes('explicitBuildRef') && series.includes("searchParams.get('build')"), 'workspace selects build from query');
ok(series.includes('fetchContentSeriesBuild'), 'selected build loads series build API');
ok(workspace.includes('data-os-video-current') && workspace.includes('VIDEO: CURRENT'), 'CURRENT video is visible');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_OFFICIAL_VIDEO_AUTHORITY_BRIDGE_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_OFFICIAL_VIDEO_AUTHORITY_BRIDGE_V1 PASS FAIL=0 (no provider)');
