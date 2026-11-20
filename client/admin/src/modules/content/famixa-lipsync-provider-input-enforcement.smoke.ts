/** FAMIXA_LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_V1 — T1–T13. 0 providers. */

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { SeriesShotRun } from './content-famixa-series';
import { lastSuccessMotionTaskId } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_ID,
  buildLipSyncProviderHowRequest,
  freezeLipSyncExecutionInput,
  loadFrozenVoicePhysicalBytes,
  resolveLipSyncMotionFromFrozenInput,
  sha256HexOfBytes,
  stampLipSyncProviderPhysicalProvenance,
  submitLipSyncFrozenExecution,
  voicePhysicalArtifactIdFromGraph,
} from './famixa-lipsync-execution-input-freeze';
import {
  VOICE_PHYSICAL_KEY_PREFIX,
  VOICE_PHYSICAL_NOT_FOUND,
  createMemoryVoicePhysicalStore,
  saveVoicePhysicalArtifact,
  voicePhysicalIdentityOf,
  voicePhysicalStorageKey,
} from './famixa-voice-physical-identity';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const SHOT = 'EP01-SC01-SH01';
const BUILD = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
const MOTION_55 = 'mot:EP01-SC01-SH01:55';
const LINE = 'line-SC01-CHAR-003-1';
const ARTIFACT = 'vpa-20260915T161911Z-e0b3093f';
const VOICE_ID = 'kOIUKJ9E0DPndqTtvJm5';
const TAKE_55 = 'https://take/55.mp4';
const TAKE_56 = 'https://take/56.mp4';
const TASK_55 = 'veo_fixture_take_55';
const TASK_56 = 'veo_fixture_take_56';
const EXPECT_SHA = 'E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C';
const VOICE_BYTES = new Uint8Array([0xe0, 0xb3, 0x09, 0x3f, 0x11, 0x22, 0x33]);

function attempt(n: number, late?: boolean) {
  return {
    n,
    status: 'SUCCEEDED' as const,
    resultClass: late ? ('LATE_RESULT' as const) : ('CURRENT' as const),
    taskId: n === 55 ? TASK_55 : TASK_56,
    outputUrl: n === 55 ? TAKE_55 : TAKE_56,
    duration: n === 55 ? 4 : 6,
  };
}

function runOf(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'reviewed',
    videoApproved: true,
    acceptedTake: { n: 56, url: TAKE_56 },
    takeUrl: TAKE_56,
    previewUrl: TAKE_56,
    turboTaskId: TASK_56,
    runwayAttempts: [attempt(55), attempt(56, true)],
    ...over,
  };
}

function frozenOf() {
  return freezeLipSyncExecutionInput({
    motionArtifactId: MOTION_55,
    motionFingerprint: 'f64e90d',
    motionDurationSec: 4,
    voiceArtifactId: ARTIFACT,
    voiceFingerprint: '17dbe460',
    voiceDurationSec: 4.24,
    fingerprint: 'lip-enforcement',
    providerId: 'fal',
    modelId: 'v3',
    buildId: BUILD,
    voiceId: VOICE_ID,
    lineId: LINE,
    voiceLineIds: [LINE],
    motionTaskId: TASK_55,
    motionUrl: TAKE_55,
  });
}

ok(LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_ID === 'FAMIXA_LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_V1', 'id');

const frozen = frozenOf();
const mutated = runOf({
  acceptedTake: { n: 56, url: TAKE_56 },
  turboTaskId: TASK_56,
  takeUrl: TAKE_56,
  previewUrl: TAKE_56,
  runwayAttempts: [
    attempt(55),
    { ...attempt(56, true), status: 'SUCCEEDED', resultClass: 'CURRENT' },
  ],
});
ok(mutated.acceptedTake?.n === 56, 'fixture acceptedTake=56');
ok(lastSuccessMotionTaskId(mutated) === TASK_56, 'T3 lastSuccessMotionTaskId=56');

const t1 = resolveLipSyncMotionFromFrozenInput(runOf(), SHOT, frozen);
ok(t1.motionArtifactId === MOTION_55 && t1.taskId === TASK_55 && t1.url === TAKE_55, 'T1 provider motion = 55');

const t2 = resolveLipSyncMotionFromFrozenInput(mutated, SHOT, frozen);
ok(t2.taskId === TASK_55 && t2.url === TAKE_55 && t2.taskId !== TASK_56, 'T2 acceptedTake 56 ignored');

const t3 = resolveLipSyncMotionFromFrozenInput(
  runOf({ turboTaskId: TASK_56, takeUrl: TAKE_56 }),
  SHOT,
  frozen,
);
ok(t3.taskId === TASK_55, 'T3 lastSuccess 56 ignored');

const t4 = resolveLipSyncMotionFromFrozenInput(
  runOf({ takeUrl: TAKE_56, previewUrl: TAKE_56, turboTaskId: TASK_56 }),
  SHOT,
  frozen,
);
ok(t4.url === TAKE_55 && t4.url !== TAKE_56, 'T4 current take 56 ignored');

const store = createMemoryVoicePhysicalStore();
const ident = voicePhysicalIdentityOf({
  artifactId: ARTIFACT,
  buildId: BUILD,
  lineId: LINE,
  voiceId: VOICE_ID,
});
if (!ident) throw new Error('identity');
await saveVoicePhysicalArtifact(ident, VOICE_BYTES, store);
ok(voicePhysicalStorageKey(ident) === `${VOICE_PHYSICAL_KEY_PREFIX}:${BUILD}:${ARTIFACT}:${VOICE_ID}`, 'T6 canonical FAMIXA:voice key');

const mutatedAssets = {
  [LINE]: { lineId: LINE, artifactId: 'vpa-OTHER', duration: 9, voiceId: 'other' },
};
ok(voicePhysicalArtifactIdFromGraph(LINE, mutatedAssets) === 'vpa-OTHER', 'graph can change after freeze');
const voiceBytes = await loadFrozenVoicePhysicalBytes(frozen, store);
ok([...voiceBytes].join(',') === [...VOICE_BYTES].join(','), 'T5/T9 frozen voice bytes unchanged after graph change');
ok(voicePhysicalArtifactIdFromGraph(LINE, { [LINE]: { artifactId: LINE } }) == null, 'T8 lineId cannot become artifactId');
ok(!voicePhysicalStorageKey(ident).startsWith('FAMIXA:tts:'), 'T7 key is not FAMIXA:tts');
try {
  assertVoicePhysicalKeyRefusesTts();
  ok(true, 'T7 legacy FAMIXA:tts cannot be selected');
} catch {
  ok(false, 'T7 legacy FAMIXA:tts cannot be selected');
}

function assertVoicePhysicalKeyRefusesTts() {
  const keys = [voicePhysicalStorageKey(ident)];
  if (keys.some((k) => k.startsWith('FAMIXA:tts:'))) throw new Error('tts');
}

const persisted: { motionArtifactId?: string; voiceArtifactId?: string; voicePhysicalFingerprint?: string; motionTaskId?: string }[] = [];
const provider: { motionArtifactId: string; voiceArtifactId: string; takeTaskId?: string; videoUrl?: string }[] = [];
await submitLipSyncFrozenExecution({
  frozen,
  run: mutated,
  shotId: SHOT,
  persistAttempt: (attempt) => persisted.push(attempt),
  loadVoiceBytes: async () => voiceBytes,
  startProvider: (input) => {
    provider.push({
      motionArtifactId: input.motionArtifactId,
      voiceArtifactId: input.voiceArtifactId,
      takeTaskId: input.takeTaskId,
      videoUrl: input.videoUrl,
    });
    const how = buildLipSyncProviderHowRequest({
      frozen,
      input,
      clipId: SHOT,
      model: 'v3',
    });
    ok(how.takeTaskId === TASK_55 && how.videoUrl === TAKE_55, 'T10 HOW motion is Take 55');
    ok(how.performanceDurationSec === 4 && how.voiceArtifactId === ARTIFACT, 'T10 HOW voice/duration frozen');
    return { dryRun: true };
  },
});
ok(provider[0]?.motionArtifactId === MOTION_55 && provider[0]?.takeTaskId === TASK_55, 'T1/T10 provider Motion 55');
ok(provider[0]?.voiceArtifactId === ARTIFACT && provider[0]?.videoUrl === TAKE_55, 'T9/T10 exact frozen artifacts');
ok(provider[0]?.takeTaskId !== TASK_56 && provider[0]?.videoUrl !== TAKE_56, 'T2–T4 provider never 56');
const lastAttempt = persisted[persisted.length - 1];
ok(lastAttempt?.motionArtifactId === MOTION_55 && lastAttempt?.voiceArtifactId === ARTIFACT, 'T11 provenance frozen ids');
ok(lastAttempt?.motionTaskId === TASK_55, 'T11 provenance motionTaskId 55');
ok(Boolean(lastAttempt?.voicePhysicalFingerprint), 'T11 provenance voice byte hash');
const sha = await sha256HexOfBytes(voiceBytes);
ok(lastAttempt?.voicePhysicalFingerprint === sha, 'T11 voicePhysicalFingerprint is SHA of sent bytes');
ok(sha !== EXPECT_SHA, 'fixture bytes hashed; live E0B3093F is identity target not this smoke file');

const stamped = stampLipSyncProviderPhysicalProvenance(
  {
    motionArtifactId: MOTION_55,
    motionFingerprint: 'f64e90d',
    motionDurationSec: 4,
    voiceArtifactId: ARTIFACT,
    voiceFingerprint: '17dbe460',
    voiceDurationSec: 4.24,
    status: 'SUBMITTING',
  },
  {
    motionTaskId: TASK_55,
    voicePhysicalFingerprint: EXPECT_SHA,
    voiceId: VOICE_ID,
    buildId: BUILD,
    lineId: LINE,
  },
);
ok(stamped.voicePhysicalFingerprint === EXPECT_SHA && stamped.voiceId === VOICE_ID, 'T11 stamp holds SH01 voice identity');

const missing = voicePhysicalIdentityOf({ artifactId: ARTIFACT, buildId: BUILD, lineId: LINE, voiceId: 'wrong' });
ok(missing, 'wrong voiceId still forms identity');
const missingBytes = await (async () => {
  try {
    await loadFrozenVoicePhysicalBytes({ ...frozen, voiceId: 'wrong' }, store);
    return 'hit';
  } catch (e) {
    return e instanceof Error ? e.message : 'err';
  }
})();
ok(missingBytes === VOICE_PHYSICAL_NOT_FOUND, 'T6 wrong voiceId cannot load frozen bytes');

const root = dirname(fileURLToPath(import.meta.url));
const helper = readFileSync(join(root, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const lipsyncFn = series.slice(series.indexOf('const startLipsync'), series.indexOf('const attachLipsync'));
const afterFreeze = lipsyncFn.slice(lipsyncFn.indexOf('freezeLipSyncExecutionInput'));
ok(afterFreeze.includes('buildLipSyncProviderHowRequest'), 'T12 HOW builder is the provider boundary');
ok(!afterFreeze.includes('getContentSeriesTurbo'), 'T12 no turbo refresh after freeze');
ok(!afterFreeze.includes('resolveTakeUrl'), 'T12 no resolveTakeUrl after freeze');
ok(!afterFreeze.includes('lastSuccessMotionTaskId'), 'T12 no lastSuccess after freeze');
ok(!afterFreeze.includes('performanceDurationOf'), 'T12 no performanceDurationOf after freeze');
ok(!afterFreeze.includes('resolveLineAudio'), 'T12 no resolveLineAudio after freeze');
ok(!afterFreeze.includes('voiceAssets['), 'T12 no voiceAssets re-pick after freeze');
ok(!afterFreeze.includes('FAMIXA:tts:'), 'T7 no legacy tts key on send path');
ok(!helper.includes('resolveTakeUrl') && !helper.includes('lastSuccessMotionTaskId'), 'T12 helper has no mutable selectors');
ok(!/startContentSeries(Turbo|Lipsync)|fal\.ai|elevenlabs/.test(helper), 'T13 helper has zero provider calls');
ok(!series.slice(series.indexOf('const startLipsync'), series.indexOf('const attachLipsync')).includes('01a0a327'), 'historical task not rewritten');

ok(createHash('sha256').update('x').digest('hex').length === 64, 'hash util present');

if (fail.length) {
  console.error(`FAMIXA_LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_V1 T1–T13 PASS');
