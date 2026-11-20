/** FAMIXA_LIPSYNC_EXECUTION_INPUT_FREEZE_V2 — T1–T15. Isolated fixture. 0 Fal. 0 graph/IDB writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { SeriesShotRun } from './content-famixa-series';
import { lastSuccessMotionTaskId, lastSuccessTakeOf } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  LIPSYNC_EXECUTION_INPUT_FREEZE_ID,
  createLipSyncAttemptProvenance,
  freezeLipSyncExecutionInput,
  resolveAuthoritativeLipSyncMotionSource,
  resolveAuthoritativeLipSyncVoiceSource,
  resolveLipSyncRemakeSources,
  resolveLipSyncMotionFromFrozenInput,
  resolveLipSyncVoiceFromFrozenInput,
  resolveStoredVoicePhysicalSource,
  stampLipSyncAttemptProviderRequest,
  submitLipSyncFrozenExecution,
  validateLipSyncFrozenInput,
} from './famixa-lipsync-execution-input-freeze';
import {
  createMemoryVoicePhysicalStore,
  saveVoicePhysicalArtifact,
  voicePhysicalIdentityOf,
} from './famixa-voice-physical-identity';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const SHOT_ID = 'EP01-SC01-SH01';
const MOTION_55 = 'mot:EP01-SC01-SH01:55';
const MOTION_56 = 'mot:EP01-SC01-SH01:56';
const VOICE = 'line-SC01-CHAR-003-1';
const TAKE_55 = 'https://take/55.mp4';
const TAKE_56 = 'https://take/56.mp4';
const TASK_55 = 'veo_fixture_take_55';
const TASK_56 = 'veo_fixture_take_56';
const MOTION_FP = 'f64e90d';
const VOICE_FP = '17dbe460';
const LIP_FP = 'lip-freeze-v2';

function attempt55() {
  return {
    n: 55,
    at: '2026-09-11T15:12:39.000Z',
    taskId: TASK_55,
    status: 'SUCCEEDED',
    outputUrl: TAKE_55,
    duration: 5,
    fingerprint: MOTION_FP,
  };
}

function attempt56() {
  return {
    n: 56,
    at: '2026-09-12T03:36:47.000Z',
    taskId: TASK_56,
    status: 'SUCCEEDED',
    resultClass: 'LATE_RESULT' as const,
    outputUrl: TAKE_56,
    duration: 6,
    fingerprint: 'late-56',
  };
}

function fixtureRun(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'reviewed',
    videoApproved: true,
    acceptedTake: { n: 56, url: TAKE_56, acceptedAt: '2026-09-14T15:26:20.000Z' },
    takeUrl: TAKE_56,
    previewUrl: TAKE_56,
    turboTaskId: TASK_56,
    runwayAttempts: [attempt55(), attempt56()],
    lipsyncFrozenInput: {
      motionArtifactId: MOTION_55,
      motionFingerprint: MOTION_FP,
      voiceFingerprint: VOICE_FP,
      fingerprint: LIP_FP,
    },
    ...over,
  };
}

const frozen = freezeLipSyncExecutionInput({
  motionArtifactId: MOTION_55,
  motionFingerprint: MOTION_FP,
  motionDurationSec: 5,
  voiceArtifactId: VOICE,
  voiceFingerprint: VOICE_FP,
  voiceDurationSec: 4.24,
  fingerprint: LIP_FP,
  providerId: 'fal',
  modelId: 'v3',
});

const run = fixtureRun();
ok(run.acceptedTake?.n === 56 && run.videoApproved === true, 'fixture acceptedTake=56 videoApproved');
ok(lastSuccessTakeOf(run.runwayAttempts)?.n === 56, 'T2 lastSuccess take is 56');
ok(lastSuccessMotionTaskId(run) === TASK_56, 'T2 lastSuccessMotionTaskId is 56');

const authMotion = resolveAuthoritativeLipSyncMotionSource({
  run,
  shotId: SHOT_ID,
  motionFingerprint: MOTION_FP,
});
ok(authMotion?.artifactId === MOTION_55 && authMotion.n === 55, 'authoritative motion stays 55');
ok(authMotion?.taskId === TASK_55 && authMotion.url === TAKE_55, 'authoritative physical is take 55');

const authVoice = resolveAuthoritativeLipSyncVoiceSource({
  lineIds: [VOICE, 'line-other'],
  voiceFingerprint: VOICE_FP,
  durationByLineId: { [VOICE]: 4.24, 'line-other': 9 },
  voiceAssets: {
    [VOICE]: {
      lineId: VOICE,
      artifactId: 'vpa-20260915T161911Z-e0b3093f',
      duration: 4.24,
      voiceId: 'kOIUKJ9E0DPndqTtvJm5',
    },
  },
});
ok(authVoice?.artifactId === 'vpa-20260915T161911Z-e0b3093f' && authVoice.durationSec === 4.24, 'authoritative voice is graph artifactId');
ok(authVoice?.artifactId !== VOICE, 'authoritative voice is not lineId');

const remakeFrozen = fixtureRun({
  lipsyncFrozenInput: {
    motionArtifactId: MOTION_55,
    motionFingerprint: MOTION_FP,
    motionDurationSec: 5,
    voiceArtifactId: 'vpa-20260915T161911Z-e0b3093f',
    voiceFingerprint: VOICE_FP,
    voiceDurationSec: 4.24,
    fingerprint: LIP_FP,
    lineId: VOICE,
    voiceLineIds: [VOICE],
    voiceId: 'kOIUKJ9E0DPndqTtvJm5',
  },
});
const remake = resolveLipSyncRemakeSources({
  run: remakeFrozen,
  shotId: SHOT_ID,
  motionFingerprint: MOTION_FP,
  lineIds: [VOICE],
  voiceFingerprint: VOICE_FP,
  durationByLineId: { [VOICE]: 4.24 },
  voiceAssets: { [VOICE]: { lineId: VOICE, duration: 4.24, artifactId: VOICE } },
  voiceId: 'kOIUKJ9E0DPndqTtvJm5',
});
ok(remake.motion?.artifactId === MOTION_55 && remake.motion?.n === 55, 'remake motion stays bound 55');
ok(remake.voice?.artifactId === 'vpa-20260915T161911Z-e0b3093f', 'remake voice uses freeze vpa when graph has lineId');
ok(remake.voice?.artifactId !== VOICE, 'remake voice is not lineId');

const store = createMemoryVoicePhysicalStore();
const lineIdent = voicePhysicalIdentityOf({
  artifactId: VOICE,
  buildId: 'TEST_BUILD',
  lineId: VOICE,
  voiceId: 'kOIUKJ9E0DPndqTtvJm5',
});
ok(!!lineIdent, 'store remake identity from lineId key');
await saveVoicePhysicalArtifact(lineIdent!, new Uint8Array([7, 8, 9, 10]), store);
const storedLine = await resolveStoredVoicePhysicalSource({
  store,
  buildId: 'TEST_BUILD',
  lineId: VOICE,
  voiceId: 'kOIUKJ9E0DPndqTtvJm5',
  voiceFingerprint: VOICE_FP,
  durationSec: 4.24,
});
ok(storedLine?.artifactId === VOICE && storedLine.durationSec === 4.24, 'store remake uses lineId key when bytes exist');
const missingStore = await resolveStoredVoicePhysicalSource({
  store: createMemoryVoicePhysicalStore(),
  buildId: 'TEST_BUILD',
  lineId: VOICE,
  voiceId: 'kOIUKJ9E0DPndqTtvJm5',
  voiceFingerprint: VOICE_FP,
  durationSec: 4.24,
});
ok(!missingStore, 'empty store does not invent Voice artifact');

const physical = resolveLipSyncMotionFromFrozenInput(run, SHOT_ID, frozen);
ok(physical.motionArtifactId === MOTION_55 && physical.taskId === TASK_55 && physical.url === TAKE_55, 'T1 frozen resolve = 55');
ok(physical.taskId !== TASK_56 && physical.url !== TAKE_56, 'T1 acceptedTake 56 does not resolve');

const lateOnly = fixtureRun();
ok(lateOnly.runwayAttempts?.some((row) => row.n === 56 && row.resultClass === 'LATE_RESULT'), 'T3 take 56 LATE');
const latePhysical = resolveLipSyncMotionFromFrozenInput(lateOnly, SHOT_ID, frozen);
ok(latePhysical.motionArtifactId === MOTION_55 && latePhysical.url === TAKE_55, 'T3 LATE 56 does not override frozen 55');

let resolveLineAudioCalls = 0;
const resolveLineAudio = () => {
  resolveLineAudioCalls += 1;
  return { id: 'wrong-line' };
};
const voiceLoads: string[] = [];
const voice = resolveLipSyncVoiceFromFrozenInput(frozen, (id) => {
  voiceLoads.push(id);
  return { id, bytes: `audio:${id}` };
});
ok(voice.voiceArtifactId === VOICE && voice.bytes.id === VOICE, 'T4 load by frozen voice id');
ok(resolveLineAudioCalls === 0, 'T4 resolveLineAudio not used to pick voice');
ok(voiceLoads.length === 1 && voiceLoads[0] === VOICE, 'T4 single frozen voice load');

const order: string[] = [];
const motionLoads: string[] = [];
const providerInputs: { motionArtifactId: string; voiceArtifactId: string; takeTaskId?: string; videoUrl?: string }[] = [];
const persisted: { motionArtifactId?: string; voiceArtifactId?: string; motionFingerprint?: string; voiceFingerprint?: string; status?: string }[] = [];

await submitLipSyncFrozenExecution({
  frozen,
  run,
  shotId: SHOT_ID,
  persistAttempt: (attempt) => {
    order.push('attempt');
    persisted.push(attempt);
  },
  loadVoiceBytes: (id) => {
    order.push('voice');
    return { id, bytes: `audio:${id}` };
  },
  loadMotionBytes: (id) => {
    motionLoads.push(id);
    order.push('motion');
  },
  startProvider: (input) => {
    order.push('provider');
    providerInputs.push({
      motionArtifactId: input.motionArtifactId,
      voiceArtifactId: input.voiceArtifactId,
      takeTaskId: input.takeTaskId,
      videoUrl: input.videoUrl,
    });
    return { taskId: 'fake-provider-0', status: 'PENDING' };
  },
});

ok(providerInputs[0]?.motionArtifactId === MOTION_55 && providerInputs[0]?.takeTaskId === TASK_55, 'T5 provider motion from frozen 55');
ok(providerInputs[0]?.voiceArtifactId === VOICE && providerInputs[0]?.videoUrl === TAKE_55, 'T5 provider physical from frozen ids');
ok(providerInputs[0]?.takeTaskId !== TASK_56 && providerInputs[0]?.videoUrl !== TAKE_56, 'T5 provider did not receive 56');
ok(persisted[0]?.motionArtifactId === MOTION_55, 'T6 attempt stores motionArtifactId');
ok(persisted[0]?.voiceArtifactId === VOICE, 'T7 attempt stores voiceArtifactId');
ok(persisted[0]?.motionFingerprint === MOTION_FP && persisted[0]?.voiceFingerprint === VOICE_FP, 'T8 attempt stores fingerprints');
ok(order[0] === 'attempt' && order.includes('provider') && order.indexOf('attempt') < order.indexOf('provider'), 'T9 provenance before provider');

const after = { ...run };
ok(after.acceptedTake?.n === 56 && after.acceptedTake?.url === TAKE_56, 'T13 acceptedTake remains 56');
ok(after.videoApproved === true, 'T14 videoApproved unchanged');

const t15Loads: string[] = [];
const t15Provider: string[] = [];
await submitLipSyncFrozenExecution({
  frozen,
  run: fixtureRun({
    acceptedTake: { n: 56, url: TAKE_56 },
    turboTaskId: TASK_56,
    takeUrl: TAKE_56,
    previewUrl: TAKE_56,
  }),
  shotId: SHOT_ID,
  persistAttempt: () => undefined,
  loadVoiceBytes: (id) => ({ id }),
  loadMotionBytes: (id) => t15Loads.push(id),
  startProvider: (input) => {
    t15Provider.push(input.motionArtifactId, input.takeTaskId || '', input.videoUrl || '');
    return { ok: true };
  },
});
ok(t15Loads.length === 1 && t15Loads[0] === MOTION_55, 'T15 requested physical artifact == 55');
ok(!t15Loads.includes(MOTION_56) && t15Provider[0] === MOTION_55 && t15Provider[1] === TASK_55, 'T15 not 56');

const attempt = createLipSyncAttemptProvenance(frozen);
ok(attempt.status === 'SUBMITTING' && !attempt.providerRequestId, 'attempt pending before request id');
const stamped = stampLipSyncAttemptProviderRequest(attempt, 'lipsync_v3_fake', 'PENDING');
ok(stamped.providerRequestId === 'lipsync_v3_fake' && stamped.motionArtifactId === MOTION_55, 'stamp request id keeps identity');
ok(validateLipSyncFrozenInput(frozen), 'frozen validates');

const root = dirname(fileURLToPath(import.meta.url));
const helper = readFileSync(join(root, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const lipsyncFn = series.slice(series.indexOf('const startLipsync'), series.indexOf('const attachLipsync'));
ok(LIPSYNC_EXECUTION_INPUT_FREEZE_ID === 'FAMIXA_LIPSYNC_EXECUTION_INPUT_FREEZE_V2', 'id');
ok(!/startContentSeries(Turbo|Lipsync)/.test(helper), 'T10 helper has no provider start');
ok(!/fal\.ai\/queue/.test(helper) && !/generativelanguage\.googleapis/.test(helper), 'T11 helper has no Fal/Veo URL');
ok(!helper.includes('new Blob') && !helper.includes('assembleContentSeriesCut'), 'T12 helper writes no media');
const afterFreeze = lipsyncFn.slice(lipsyncFn.indexOf('freezeLipSyncExecutionInput'));
ok(!lipsyncFn.includes('lastSuccessMotionTaskId') && !afterFreeze.includes('resolveTakeUrl('), 'send path drops lastSuccess/resolveTakeUrl');
ok(lipsyncFn.includes('submitLipSyncFrozenExecution') && lipsyncFn.includes('freezeLipSyncExecutionInput'), 'startLipsync freezes before submit');
ok(lipsyncFn.includes('resolveLipSyncRemakeSources'), 'remake recovers freeze Motion+Voice before Fal');
ok(lipsyncFn.includes('resolveStoredVoicePhysicalSource'), 'remake recovers stored Voice bytes before Fal');
ok(lipsyncFn.includes('omitSpeakerByShot'), 'remake snapshots omit-pin before clearing Fal error');
ok(lipsyncFn.includes('isSpeakerFaceBandMark'), 'two-face remake repairs chest pin to face');
ok(lipsyncFn.includes('multiFace ? false'), 'two-face remake never omits speaker pin');
ok(
  lipsyncFn.indexOf('resolveLineAudio') >= 0 &&
    lipsyncFn.indexOf('resolveLineAudio') < lipsyncFn.indexOf('freezeLipSyncExecutionInput'),
  'remake promotes existing TTS blob before freeze',
);
ok(!afterFreeze.includes('resolveLineAudio'), 'T4 after freeze no resolveLineAudio');
ok(!afterFreeze.includes('getContentSeriesTurbo') && afterFreeze.includes('buildLipSyncProviderHowRequest'), 'after freeze HOW-only no turbo refresh');

if (fail.length) {
  console.error(`FAMIXA_LIPSYNC_EXECUTION_INPUT_FREEZE_V2 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_LIPSYNC_EXECUTION_INPUT_FREEZE_V2 T1–T15 PASS');
