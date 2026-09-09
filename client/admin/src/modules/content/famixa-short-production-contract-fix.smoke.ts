/** FAMIXA_SHORT_PRODUCTION_CONTRACT_FIX_V1 — voice artifact, take SoT, finalReady SoT. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from './content-famixa-series';
import { lipsyncMotionInputOf, resolveTakeUrl } from './content-famixa-final-source';
import { lipsyncSendEligible } from './content-famixa-prod-v2';
import { sameFailedInput } from './content-famixa-runway-pipe';
import { shotI2vPromptHash } from './content-famixa-prod-v2';
import {
  ACTING_BODY_WEIGHT,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { shotProgressRows } from './ContentFamixaShotProduction/ShotProductionProgress';
import {
  buildShotProductionSnapshot,
  episodeFinishReady,
} from './ContentFamixaShotProduction/ShotProductionState';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { hasResolvableVoiceArtifact, shotProductionInputOf } from './famixa-video-audio-lipsync-pipeline';
import { computeShotTiming, productionDurationOf, providerDurationOf } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const pipeline = readFileSync(join(root, 'famixa-video-audio-lipsync-pipeline.ts'), 'utf8');
const stateSrc = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionState.ts'), 'utf8');
const prodV2 = readFileSync(join(root, 'content-famixa-prod-v2.ts'), 'utf8');

ok(series.includes('lipsyncSendEligible') && series.includes('resolveTakeUrl(run)'), 'B wiring uses resolveTakeUrl');
ok(!series.includes('episodeCanFinalize('), 'C SeriesTab dropped preflight finish');
ok(series.includes('episodeFinishReady'), 'C Episode Finish uses snapshot SoT');
ok(!pipeline.includes('ttsFiles[line.id] || dur'), 'A duration is not a voice artifact');
ok(stateSrc.includes('episodeFinishReady') && stateSrc.includes('resolveTakeUrl(run)'), 'C snapshot SoT + current take');
ok(prodV2.includes('lipsyncMotionInputOf') && prodV2.includes('motionStale'), 'B eligibility helper');
ok(!series.includes('startContentSeriesTurbo') || series.includes('startContentSeriesLipsync'), 'no new provider in this smoke');

const v2Beat = {
  before: {
    action: ACTING_STANCE_DOORWAY,
    body: ACTING_BODY_WEIGHT,
    prop: 'He holds a sheet of paper with both hands.',
    gaze: 'Looks toward his mother, then lowers his gaze slightly; not at the camera.',
    room: ACTING_ROOM_STILL,
    holdSec: 0.2,
  },
  during: { speech: true, emotion: 'uneasy' as const },
  after: { holdSec: 0.22 },
};

ok(hasResolvableVoiceArtifact({ durationSec: 1.52, voiceId: 'v1', ttsFile: { url: 'blob:a', fileName: 'a.mp3' } }), 'A1 duration + blob READY');
ok(!hasResolvableVoiceArtifact({ durationSec: 1.52, voiceId: 'v1' }), 'A2 duration only NOT READY');
ok(!hasResolvableVoiceArtifact({ durationSec: 1.52, voiceId: 'v1', hasBlob: false }), 'A3 resolver fail NOT READY');

const sh01 = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.7,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001', 'CHAR-003'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  actingBeat: v2Beat,
  ...over,
});

const sh02 = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH02',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH02',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.2,
  story: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  visual: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  characters: ['CHAR-003'],
  characterIds: ['CHAR-003', 'CHAR-001'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-003-2'],
  actingBeat: {
    before: {
      action: 'Standing at the living-room doorway.',
      body: 'He takes a small half-step.',
      gaze: 'He looks toward his mother, not the camera.',
      room: ACTING_ROOM_STILL,
      holdSec: 0.2,
    },
    during: { speech: true, emotion: 'neutral' as const },
    after: { holdSec: 0.22 },
  },
  ...over,
});

const derived = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });

const run01Fail: SeriesShotRun = {
  status: 'turbo_testing',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: 'data:image/jpeg;base64,xxSH01',
  keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
  takeUrl: 'https://take/n3.mp4',
  previewUrl: undefined,
  lipsyncUrl: 'https://v3b.fal.media/old.mp4',
  lipsynced: true,
  finalSource: 'FAL',
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT',
  turboTaskId: '5c5dc70c',
  failedKfHash: 'h2b879d96:147815',
  failedPromptHash: 'hedf22d6f:421',
  shotQa: { action: true, continuity: true, voiceFace: true, lipsync: true },
  shotProduction: { motionFp: '45e47d48', assembleFp: 'ba1b5e57', assembleFileName: 'famixa-SH01-01.mp4' },
  runwayAttempts: [
    { n: 3, at: '', status: 'SUCCEEDED', taskId: '9ef1c551', promptHash: 'hf56bc88:377', source: { hash: 'h2b879d96:147815' } },
    {
      n: 4,
      at: '',
      status: 'FAILED',
      taskId: '5c5dc70c',
      promptHash: 'hedf22d6f:421',
      failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
      source: { hash: 'h2b879d96:147815' },
    },
  ],
};

const run02Ready: SeriesShotRun = {
  status: 'turbo_testing',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: 'data:image/jpeg;base64,xxSH02',
  keyframeFileName: 'kf-EP99-SC01-SH02-canon.jpg',
  takeUrl: 'https://take/sh02-n2.mp4',
  previewUrl: 'https://take/sh02-n2.mp4',
  turboStatus: 'SUCCEEDED',
  videoPipe: 'VIDEO_READY',
  runwayAttempts: [{ n: 2, at: '', status: 'SUCCEEDED', taskId: 'e1e0ebbc', promptHash: 'h75bfa5b0:286' }],
};

function stateOf(shots: FamixaSeriesShot[], runs: Record<string, SeriesShotRun>): SeriesPilotState {
  return {
    voiceLocked: true,
    scriptLocked: true,
    roles: [
      { id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'pLQJCzpzwaedKVuhI1Mq' },
      { id: 'role-CHAR-003', title: 'Mẹ', name: 'Linh', characterId: 'CHAR-003', voiceId: 'UuMSQK8FdLwaY2M8ZAnh' },
    ],
    characters: [
      { id: 'CHAR-001', name: 'Minh', voiceId: 'pLQJCzpzwaedKVuhI1Mq' },
      { id: 'CHAR-003', name: 'Linh', voiceId: 'UuMSQK8FdLwaY2M8ZAnh' },
    ],
    lines: [
      { id: 'line-SC01-CHAR-001-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con tờ này.', sceneId: 'SC01' },
      { id: 'line-SC01-CHAR-003-2', characterId: 'CHAR-003', text: 'Để đấy', sceneId: 'SC01' },
    ],
    voiceAssets: {
      'line-SC01-CHAR-001-1': { lineId: 'line-SC01-CHAR-001-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' },
      'line-SC01-CHAR-003-2': { lineId: 'line-SC01-CHAR-003-2', duration: 0.72, status: 'ready', characterId: 'CHAR-003' },
    },
    runs,
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'F',
      episode: 'EP99',
      title: 'TEST BATCH 3 SHORT',
      premise: '',
      moral: '',
      ctaRule: '',
      shots,
    },
  };
}

const tts01 = { 'line-SC01-CHAR-001-1': { url: 'blob:minh', fileName: 'minh.mp3' } };
const tts02 = { 'line-SC01-CHAR-003-2': { url: 'blob:linh', fileName: 'linh.mp3' } };

const s01 = stateOf([sh01({ timing: derived })], { 'EP99-SC01-SH01': run01Fail });
const inputNoFile = shotProductionInputOf(s01, s01.episode!.shots[0]!, {});
ok(inputNoFile.cues[0]?.status === 'MISSING' && !inputNoFile.cues[0]?.audioAssetId, 'A2 input duration-only MISSING');
const inputWithFile = shotProductionInputOf(s01, s01.episode!.shots[0]!, tts01);
ok(inputWithFile.cues[0]?.status === 'READY' && inputWithFile.cues[0]?.audioAssetId === 'line-SC01-CHAR-001-1', 'A1 input blob READY');
ok(!hasResolvableVoiceArtifact({ durationSec: 1.52, voiceId: 'v1', hasBlob: false }), 'A3 hasBlob false');

const bothTake = { takeUrl: 'https://take/a.mp4', previewUrl: 'https://take/a.mp4' };
ok(resolveTakeUrl(bothTake) === 'https://take/a.mp4' && lipsyncMotionInputOf(bothTake) === 'https://take/a.mp4', 'B1 both URLs resolve take');
ok(resolveTakeUrl({ takeUrl: 'https://take/only.mp4', previewUrl: undefined }) === 'https://take/only.mp4', 'B2 preview null + takeUrl');
ok(
  lipsyncSendEligible({ run: { takeUrl: 'https://take/only.mp4' }, spoken: true, motionStale: false }) === true,
  'B2 eligibility PASS',
);
ok(lipsyncSendEligible({ run: {}, spoken: true, motionStale: false }) === false, 'B3 no take blocked');
ok(
  lipsyncSendEligible({ run: { takeUrl: 'https://take/stale.mp4' }, spoken: true, motionStale: true }) === false,
  'B4 stale motion blocked',
);

const sh02CurrentHash = shotI2vPromptHash(stateOf([sh02()], { 'EP99-SC01-SH02': run02Ready }), sh02(), run02Ready);
const run02Current: SeriesShotRun = {
  ...run02Ready,
  runwayAttempts: [{ n: 2, at: '', status: 'SUCCEEDED', taskId: 'e1e0ebbc', promptHash: sh02CurrentHash }],
};
const s02 = stateOf([sh02()], { 'EP99-SC01-SH02': run02Current });
const snap02 = buildShotProductionSnapshot({ state: s02, shot: s02.episode!.shots[0]!, ttsFiles: tts02 });
const marks02 = Object.fromEntries(shotProgressRows(snap02).map((r) => [r.id, r.mark]));
ok(snap02.voiceReady && marks02.voice === '✓', 'D Voice READY');
ok(snap02.keyframeApproved && marks02.picture === '✓', 'D KF READY');
ok(snap02.motionReady && !snap02.motionStale && marks02.video === '✓', 'D Motion READY');
ok(!snap02.lipSyncReady && !snap02.qaReady, 'D Lip-sync blocked until QA');
ok(nextShotProductionCommand(snap02).type === 'LIPSYNC_QA_REQUIRED', 'D command QA');
ok(!snap02.mixReady && !snap02.finalReady && marks02.final === '○', 'D Final NOT READY');
ok(shotI2vPromptHash(s02, s02.episode!.shots[0]!, run02Current) === sh02CurrentHash, 'D current prompt matches stamped take');
ok(run02Ready.runwayAttempts?.[0]?.promptHash === 'h75bfa5b0:286', 'D historical n=2 His/He hash kept');
ok(providerDurationOf(s02, s02.episode!.shots[0]!) === 5, 'D provider 5');

const snap01 = buildShotProductionSnapshot({ state: s01, shot: s01.episode!.shots[0]!, ttsFiles: tts01 });
ok(snap01.motionStale && !snap01.motionReady, 'E Motion STALE');
ok(productionDurationOf(s01, s01.episode!.shots[0]!) === 5 && providerDurationOf(s01, s01.episode!.shots[0]!) === 5, 'E timing V3 5 / 5');
const sh01CurrentHash = shotI2vPromptHash(s01, s01.episode!.shots[0]!, run01Fail);
ok(
  sameFailedInput(run01Fail, 'h2b879d96:147815', 'hedf22d6f:421') === true,
  'E live V2 His/He fail hash still closes against itself',
);
ok(
  sameFailedInput(run01Fail, 'h2b879d96:147815', sh01CurrentHash) === false,
  'E current Minh-named compile is a new prompt vs live fail',
);
ok(!snap01.finalReady && !snap01.qualityPassed, 'E Final NOT READY');
ok(compileI2vPrompt(s01, s01.episode!.shots[0]!, s01.episode!.shots[0]!.story).includes('He shifts his weight slightly'), 'E V2 prompt kept');

const finishFalse = episodeFinishReady([snap01, snap02]);
ok(finishFalse.allowed === false && snap01.finalReady === false, 'C1 snapshot false → episode false');

const almost = buildShotProductionSnapshot({
  state: s02,
  shot: s02.episode!.shots[0]!,
  ttsFiles: tts02,
});
const falOnlyQa = {
  ...run02Current,
  lipsynced: true,
  lipsyncUrl: 'https://fal/lip.mp4',
  finalSource: 'FAL' as const,
  shotQa: { action: true, continuity: true, voiceFace: true },
};
const sFal = stateOf([sh02()], { 'EP99-SC01-SH02': falOnlyQa });
const snapNoQuality = buildShotProductionSnapshot({ state: sFal, shot: sFal.episode!.shots[0]!, ttsFiles: tts02 });
ok(!snapNoQuality.finalReady && !snapNoQuality.qualityPassed, 'C2 Fal URL + missing lipsyncQuality → false');
const falAvMissing = {
  ...falOnlyQa,
  shotQa: { action: true, continuity: true, voiceFace: true, lipsyncQuality: true },
};
const sAv = stateOf([sh02()], { 'EP99-SC01-SH02': falAvMissing });
const snapNoAv = buildShotProductionSnapshot({ state: sAv, shot: sAv.episode!.shots[0]!, ttsFiles: tts02 });
ok(!snapNoAv.finalReady, 'C3 Fal URL + missing finalAv → false');

const readyRun: SeriesShotRun = {
  ...falOnlyQa,
  shotQa: { action: true, continuity: true, voiceFace: true, lipsyncQuality: true, finalAv: true },
};
const sOk = stateOf([sh02()], { 'EP99-SC01-SH02': readyRun });
const fpsOk = computeInputFingerprints(sOk, sOk.episode!.shots[0]!);
sOk.runs['EP99-SC01-SH02'] = {
  ...readyRun,
  shotProduction: {
    voiceFp: fpsOk.voice,
    kfFp: fpsOk.keyframe,
    motionFp: fpsOk.motion,
    lipsyncFp: fpsOk.lipsync,
    assembleFp: fpsOk.mix,
    assembleFileName: 'famixa-SH02-01.mp4',
  },
};
const snapOk = buildShotProductionSnapshot({ state: sOk, shot: sOk.episode!.shots[0]!, ttsFiles: tts02 });
ok(snapOk.finalReady && episodeFinishReady([snapOk]).allowed, 'C4 all conditions → true');
ok(almost.finalReady === false, 'C no auto-approve SH02');

ok(!stateSrc.includes('startContentSeriesTurbo') && !stateSrc.includes('elevenlabs.io'), 'F no provider in snapshot');
ok(!prodV2.includes('fal-ai/') || prodV2.includes('lipsyncSendEligible'), 'G no media generation');

if (fail.length) {
  console.error(`FAMIXA_SHORT_PRODUCTION_CONTRACT_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SHORT_PRODUCTION_CONTRACT_FIX_V1 PASS FAIL=0 (no provider)');
console.log(`sh01 motionStale=${snap01.motionStale} final=${snap01.finalReady} liveFailClosed=${sameFailedInput(run01Fail, 'h2b879d96:147815', 'hedf22d6f:421')}`);
console.log(`sh02 voice=${snap02.voiceReady} kf=${snap02.keyframeApproved} motion=${snap02.motionReady} lip=${snap02.lipSyncReady} final=${snap02.finalReady}`);
