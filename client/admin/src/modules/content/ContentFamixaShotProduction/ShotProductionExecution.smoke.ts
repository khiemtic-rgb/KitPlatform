/** FAMIXA_SHOT_EXECUTION_STATE_MACHINE_V1 — T1–T15. 0 providers. 0 media writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash, sameFailedInput } from '../content-famixa-runway-pipe';
import { shotI2vPromptHash } from '../content-famixa-prod-v2';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from '../content-famixa-series';
import { acceptExistingTake, playableMotionTakeOf } from './ShotProductionArtifacts';
import { computeInputFingerprints } from './ShotProductionStamp';
import { nextShotProductionCommand } from './ShotProductionOrchestrator';
import { buildShotProductionSnapshot } from './ShotProductionState';
import {
  actingBeatFingerprintOf,
  adaptMotionAttempts,
  applyMotionCallbackRecord,
  cloneExecutionFixture,
  currentPictureInputOf,
  deepStableEqual,
  deriveShotExecutionState,
  freezeMotionInput,
  motionAttemptId,
  motionCallbackRunPatch,
  picturePixelHashOf,
  validateMotionCallback,
  type ExecutionAwareAttempt,
} from './ShotProductionExecution';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const KF_A = 'data:image/jpeg;base64,PIXELAAA111';
const KF_B = 'data:image/jpeg;base64,PIXELBBB222';
const HASH_A = dataUriHash(KF_A);
const HASH_B = dataUriHash(KF_B);
ok(HASH_A !== HASH_B, 'pixel hashes differ');

function shotOf(over?: Partial<FamixaSeriesShot>): FamixaSeriesShot {
  return {
    id: 'EP99-SC01-SH04',
    scene: 'SC01',
    sceneId: 'SC01',
    shot: 'SH04',
    clock: '5s',
    seconds: 5,
    story: 'Nhân vật đứng trong phòng và nói.',
    visual: 'Nhân vật đứng trong phòng và nói.',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'PHÒNG',
    motionPrompt: '',
    motionPromptVi: 'Nhân vật đứng trong phòng và nói.',
    status: 'story_locked',
    dialogueSegmentIds: ['line-1'],
    actingBeat: {
      before: { action: 'stands still', body: 'He stays in place.', holdSec: 0.2 },
      during: { speech: true, emotion: 'calm', intensity: 1 },
      after: { holdSec: 0.2 },
    },
    ...over,
  };
}

function runOf(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'turbo_testing',
    kfApproved: true,
    keyframeDataUrl: KF_A,
    keyframeFileName: 'kf.jpg',
    kfSourceHash: HASH_A,
    pictureRevisionId: 'picture:EP99-SC01-SH04:001',
    ...over,
  };
}

function stateOf(shot: FamixaSeriesShot, run: SeriesShotRun, voiceText = 'Mẹ xem giúp con.'): SeriesPilotState {
  return {
    roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'voice-1' }],
    runs: { [shot.id]: run },
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-1' }],
    lines: [{ id: 'line-1', characterId: 'CHAR-001', text: voiceText, voiceId: 'voice-1' }],
    voiceAssets: { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'F',
      episode: 'EP99',
      title: 'EXECUTION V1',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
  };
}

const tts = { 'line-1': { url: 'blob:voice', fileName: 'a.mp3' } };

function successRow(n: number, frozen: ReturnType<typeof freezeMotionInput>, url: string): ExecutionAwareAttempt {
  return {
    n,
    at: `2026-09-08T00:00:0${n}.000Z`,
    status: 'SUCCEEDED',
    taskId: `task-ok-${n}`,
    outputUrl: url,
    promptHash: frozen.promptHash,
    attemptId: motionAttemptId('EP99-SC01-SH04', n),
    frozenInput: frozen,
    inputFingerprint: frozen.executionFingerprint,
    kf: { hash: frozen.keyframePixelHash },
    source: { hash: frozen.keyframePixelHash },
  };
}

function runningRow(n: number, frozen: ReturnType<typeof freezeMotionInput>): ExecutionAwareAttempt {
  return {
    ...successRow(n, frozen, ''),
    status: 'RUNNING',
    outputUrl: undefined,
    taskId: `task-run-${n}`,
  };
}

function failRow(n: number, frozen: ReturnType<typeof freezeMotionInput>): ExecutionAwareAttempt {
  return {
    ...successRow(n, frozen, ''),
    status: 'FAILED',
    outputUrl: undefined,
    taskId: `task-fail-${n}`,
    failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
    error: 'INTERNAL.BAD_OUTPUT.CODE01',
  };
}

function derive(shot: FamixaSeriesShot, run: SeriesShotRun, voiceText?: string) {
  const state = stateOf(shot, run, voiceText);
  return deriveShotExecutionState({ state, shot, ttsFiles: tts });
}

function snapOf(shot: FamixaSeriesShot, run: SeriesShotRun) {
  return buildShotProductionSnapshot({
    state: stateOf(shot, run),
    shot,
    ttsFiles: tts,
  });
}

const shot = shotOf();
const frozenA = freezeMotionInput(stateOf(shot, runOf()), shot);
ok(frozenA.keyframePixelHash === HASH_A, 'freeze uses kfSourceHash');
ok(picturePixelHashOf(runOf()) === HASH_A, 'picture identity hashes live still');
ok(
  picturePixelHashOf(runOf({ keyframeDataUrl: KF_B, kfSourceHash: HASH_A })) === HASH_A,
  'approved stamp wins over stale live pixels',
);
ok(
  picturePixelHashOf(runOf({ kfApproved: false, keyframeDataUrl: KF_B, kfSourceHash: HASH_A })) === HASH_B,
  'unapproved live pixels beat stale stamp',
);
ok(
  !playableMotionTakeOf(
    runOf({
      keyframeDataUrl: KF_B,
      kfSourceHash: HASH_A,
      takeUrl: 'https://take/44.mp4',
      runwayAttempts: [{ n: 44, at: '', status: 'SUCCEEDED', outputUrl: 'https://take/44.mp4', kf: { hash: HASH_A } }],
    }),
    HASH_B,
  ),
  'old hashed take is not playable on a new still',
);
ok(
  !playableMotionTakeOf(
    runOf({
      keyframeDataUrl: KF_B,
      kfSourceHash: HASH_B,
      motionNeedsRemake: true,
      takeUrl: 'https://take/44.mp4',
      runwayAttempts: [{ n: 44, at: '', status: 'SUCCEEDED', outputUrl: 'https://take/44.mp4' }],
    }),
    HASH_B,
  ),
  'unstamped last take is not playable on a known still',
);

const runT1 = runOf({
  takeUrl: 'https://take/m1.mp4',
  runwayAttempts: [successRow(1, frozenA, 'https://take/m1.mp4')],
});
const exec1 = derive(shot, runT1);
ok(exec1.currentArtifacts.motion?.validity === 'CURRENT', 'T1 motion CURRENT');
ok(exec1.currentInputs.picture.pixelHash === HASH_A, 'T1 picture pixel A');
const snap1 = snapOf(shot, runT1);
ok(nextShotProductionCommand(snap1).type !== 'CONFIRM_MOTION', 'T1 next is not CONFIRM_MOTION');

const runT2 = runOf({
  keyframeDataUrl: KF_B,
  kfSourceHash: HASH_B,
  pictureRevisionId: 'picture:EP99-SC01-SH04:002',
  takeUrl: 'https://take/m1.mp4',
  runwayAttempts: [successRow(1, frozenA, 'https://take/m1.mp4')],
});
const exec2 = derive(shot, runT2);
ok(exec2.lastSuccessArtifacts.motion?.validity === 'STALE', 'T2 motion STALE after pixel change');
ok(!exec2.currentArtifacts.motion, 'T2 current-valid motion empty');
ok(exec2.currentInputs.picture.textFingerprint === exec1.currentInputs.picture.textFingerprint, 'T2 text fingerprint same');
ok(exec2.currentInputs.picture.pixelHash === HASH_B, 'T2 pixel B');
ok(exec2.gates.pictureChanged, 'T2 pictureChanged');
ok(exec2.currentInputs.picture.revisionId === 'picture:EP99-SC01-SH04:002', 'T2 live PictureRevisionId bumped');
ok(exec2.lastSuccessArtifacts.motion?.provenance.pictureRevisionId === 'picture:EP99-SC01-SH04:001', 'T2 frozen revision stays P1');
const sameHashDiffRev = runOf({
  pictureRevisionId: 'picture:EP99-SC01-SH04:002',
  takeUrl: 'https://take/m1.mp4',
  runwayAttempts: [successRow(1, frozenA, 'https://take/m1.mp4')],
});
const execHash = derive(shot, sameHashDiffRev);
ok(!execHash.currentArtifacts.motion, 'T2 same pixelHash different PictureRevisionId is not CURRENT');
ok(execHash.gates.pictureChanged, 'T2 revision boundary sets pictureChanged');
const snap2 = snapOf(shot, runT2);
ok(nextShotProductionCommand(snap2).type === 'CONFIRM_MOTION', 'T2 next CONFIRM_MOTION');

const frozenB = freezeMotionInput(stateOf(shot, runT2), shot);
const runT3 = runOf({
  keyframeDataUrl: KF_B,
  kfSourceHash: HASH_B,
  pictureRevisionId: 'picture:EP99-SC01-SH04:002',
  takeUrl: 'https://take/m2.mp4',
  runwayAttempts: [successRow(1, frozenA, 'https://take/m1.mp4'), successRow(2, frozenB, 'https://take/m2.mp4')],
});
const exec3 = derive(shot, runT3);
ok(exec3.currentArtifacts.motion?.validity === 'CURRENT', 'T3 Motion-002 CURRENT');
ok(exec3.attempts[0]?.input?.stage === 'MOTION' && exec3.attempts[0].input.motion.keyframePixelHash === HASH_A, 'T3 Motion-001 keeps KF-A');
ok(exec3.staleArtifacts.some((a) => a.kind === 'MOTION' && a.validity === 'STALE') || exec3.attempts[0], 'T3 history kept');

const beatB = shotOf({
  actingBeat: {
    before: { action: 'turns slightly', body: 'He stays in place.', holdSec: 0.2 },
    during: { speech: true, emotion: 'worried', intensity: 2 },
    after: { holdSec: 0.4 },
  },
});
const fpsA = computeInputFingerprints(stateOf(shot, runT1), shot);
const fpsBeat = computeInputFingerprints(stateOf(beatB, runT1), beatB);
const exec4 = derive(beatB, runT1);
ok(actingBeatFingerprintOf(beatB) !== actingBeatFingerprintOf(shot), 'T4 actingBeatFingerprint changes');
ok(exec4.lastSuccessArtifacts.motion?.validity === 'STALE', 'T4 Motion stale after beat change');
ok(!exec4.currentArtifacts.motion, 'T4 current motion empty');

const runT5 = runOf({
  takeUrl: 'https://take/m1.mp4',
  lipsyncUrl: 'https://fal/lip-a.mp4',
  lipsynced: true,
  runwayAttempts: [successRow(1, frozenA, 'https://take/m1.mp4')],
  shotProduction: {
    motionFp: fpsA.motion,
    lipsyncFp: fpsA.lipsync,
    assembleFp: fpsA.mix,
    voiceFp: fpsA.voice,
    kfFp: fpsA.keyframe,
  },
});
const exec5 = derive(shot, runT5, 'Câu thoại mới hoàn toàn khác.');
ok(exec5.currentArtifacts.motion?.validity === 'CURRENT' || exec5.lastSuccessArtifacts.motion?.validity === 'CURRENT', 'T5 motion not stale from voice');
ok(exec5.lastSuccessArtifacts.lipsync?.validity === 'STALE' || exec5.lastSuccessArtifacts.lipsync?.validity === 'LEGACY_UNVERIFIED', 'T5 lip stale after voice change');

const fpsB = computeInputFingerprints(stateOf(shot, runT3), shot);
const runT6 = runOf({
  keyframeDataUrl: KF_B,
  kfSourceHash: HASH_B,
  pictureRevisionId: 'picture:EP99-SC01-SH04:002',
  takeUrl: 'https://take/m2.mp4',
  lipsyncUrl: 'https://fal/lip-old.mp4',
  lipsynced: true,
  runwayAttempts: [successRow(2, frozenB, 'https://take/m2.mp4')],
  shotProduction: {
    motionFp: fpsA.motion,
    lipsyncFp: fpsA.lipsync,
    assembleFp: fpsA.mix,
    voiceFp: fpsA.voice,
    kfFp: fpsA.keyframe,
  },
});
const exec6 = derive(shot, runT6);
ok(exec6.lastSuccessArtifacts.lipsync?.validity === 'STALE' || exec6.lastSuccessArtifacts.mix?.validity === 'STALE' || exec6.lastSuccessArtifacts.lipsync, 'T6 downstream stale after motion B');

const runT7base = runOf({
  runwayAttempts: [runningRow(1, frozenA)],
  turboTaskId: 'task-run-1',
  turboStatus: 'RUNNING',
});
const runT7b = runOf({
  keyframeDataUrl: KF_B,
  kfSourceHash: HASH_B,
  runwayAttempts: [runningRow(1, frozenA), runningRow(2, freezeMotionInput(stateOf(shot, runOf({ keyframeDataUrl: KF_B, kfSourceHash: HASH_B })), shot))],
  turboTaskId: 'task-run-2',
  turboStatus: 'RUNNING',
});
const attemptsB = adaptMotionAttempts(shot.id, runT7b.runwayAttempts);
const currentB = {
  ...freezeMotionInput(stateOf(shot, runT7b), shot),
  picture: derive(shot, runT7b).currentInputs.picture,
};
const decisionA = validateMotionCallback({
  attempts: attemptsB,
  current: currentB,
  providerTaskId: 'task-run-1',
});
ok(decisionA.promote === false && decisionA.resultClass === 'LATE_RESULT', 'T7 old callback LATE_RESULT');
const patched7 = motionCallbackRunPatch({
  run: runT7b,
  decision: decisionA,
  outputUrl: 'https://take/late-a.mp4',
  taskId: 'task-run-1',
});
ok(patched7.takeUrl !== 'https://take/late-a.mp4', 'T7 does not write takeUrl');
ok(patched7.motionNeedsRemake !== false || patched7.takeUrl == null, 'T7 does not clear remake via promote');
ok((patched7.runwayAttempts ?? []).some((a) => (a as ExecutionAwareAttempt).resultClass === 'LATE_RESULT'), 'T7 A stored as LATE_RESULT');
const inFlight = adaptMotionAttempts(shot.id, patched7.runwayAttempts ?? runT7b.runwayAttempts);
ok(inFlight.filter((a) => a.status === 'RUNNING' || a.status === 'PENDING').at(-1)?.providerTaskId === 'task-run-2', 'T7 B still in-flight');

const promptNow = shotI2vPromptHash(stateOf(shot, runOf()), shot);
const runT8 = runOf({
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  failedKfHash: HASH_A,
  failedPromptHash: promptNow,
  runwayAttempts: [failRow(1, { ...frozenA, promptHash: promptNow })],
});
ok(sameFailedInput(runT8, HASH_A, promptNow), 'T8 sameFailedInput true');
ok(derive(shot, runT8).gates.sameFailedInput, 'T8 execution gate true');

const beatPrompt = shotOf({
  actingBeat: {
    before: { action: 'walks to the table', body: 'He takes a small step.', holdSec: 0.2 },
    during: { speech: true, emotion: 'urgent', intensity: 3 },
    after: { holdSec: 0.2 },
  },
});
const promptAfterBeat = shotI2vPromptHash(stateOf(beatPrompt, runT8), beatPrompt);
ok(promptAfterBeat !== promptNow, 'T9 actingBeat changes promptHash');
ok(!sameFailedInput(runT8, HASH_A, promptAfterBeat), 'T9 circuit opens after prompt change');
const snap9 = snapOf(beatPrompt, runOf({ takeUrl: undefined, runwayAttempts: [] }));
ok(nextShotProductionCommand(snap9).type === 'CONFIRM_MOTION' || nextShotProductionCommand(snap9).type === 'ENSURE_VOICE', 'T9 can confirm motion');

const exec10 = exec2;
ok(exec10.visibleArtifacts.motion?.url === 'https://take/m1.mp4', 'T10 visible is old take');
ok(!exec10.currentArtifacts.motion, 'T10 current-valid none');

const accepted = acceptExistingTake(runT2);
ok(accepted?.acceptedTake.url === 'https://take/m1.mp4', 'T11 accept sets visible url');
const runT11 = { ...runT2, ...accepted };
const exec11 = derive(shot, runT11);
ok(exec11.acceptedArtifacts.motion?.validity === 'ACCEPTED_STALE', 'T11 ACCEPTED_STALE');
ok(exec11.currentArtifacts.motion?.validity !== 'CURRENT', 'T11 does not fake SUCCESS/CURRENT');
ok(exec11.gates.lipsyncMayUseAccepted, 'T11 lipsync may use accepted');

const runT12 = runOf({
  kfSourceHash: undefined,
  keyframeDataUrl: undefined,
  kfApproved: false,
  pictureRevisionId: undefined,
  takeUrl: undefined,
  previewUrl: 'https://legacy/only-url.mp4',
  runwayAttempts: [],
});
const exec12 = derive(shotOf({ dialogueSegmentIds: [] }), runT12);
ok(
  exec12.lastSuccessArtifacts.motion?.validity === 'LEGACY_UNVERIFIED' ||
    exec12.visibleArtifacts.motion?.validity === 'LEGACY_UNVERIFIED' ||
    exec12.gates.legacyUnverified,
  'T12 LEGACY_UNVERIFIED',
);
ok(exec12.currentArtifacts.motion?.validity !== 'CURRENT', 'T12 URL is not CURRENT');

const liveB = freezeMotionInput(stateOf(shot, runOf({ keyframeDataUrl: KF_B, kfSourceHash: HASH_B })), shot);
const oldAttempt = runningRow(1, frozenA);
const recorded = applyMotionCallbackRecord(oldAttempt, { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'INPUT_CHANGED' }, 'https://take/late.mp4');
ok(recorded.frozenInput?.keyframePixelHash === HASH_A, 'T13 frozen KF stays AAA');
ok(recorded.kf?.hash === HASH_A, 'T13 attempt kf hash not overwritten by current BBB');
ok(liveB.keyframePixelHash === HASH_B, 'T13 current is BBB');

const fixture14 = cloneExecutionFixture({ shot, run: runT1 });
const a14 = deriveShotExecutionState({ state: stateOf(fixture14.shot, fixture14.run), shot: fixture14.shot, ttsFiles: tts });
const b14 = deriveShotExecutionState({ state: stateOf(fixture14.shot, fixture14.run), shot: fixture14.shot, ttsFiles: tts });
ok(deepStableEqual(a14, b14), 'T14 derive twice deep equal');
ok(deepStableEqual(fixture14.run, runT1), 'T14 does not mutate run');

const snap15 = snapOf(shot, runT2);
ok(snap15.nextAction === nextShotProductionCommand(snap15).type, 'T15 snapshot.nextAction === command.type');
ok(snap15.nextAction !== snap15.stage, 'T15 nextAction is not stage');

const frozenWan = freezeMotionInput(stateOf(shot, runOf()), shot, { provider: 'wan' });
const liveRunway = freezeMotionInput(stateOf(shot, runOf()), shot, { provider: 'runway' });
ok(frozenWan.executionFingerprint === liveRunway.executionFingerprint, 'T16 same WHAT fingerprint across providers');
ok(frozenWan.provider !== liveRunway.provider, 'T16 HOW provider metadata differs');
ok(frozenWan.providerStamp !== liveRunway.providerStamp, 'T16 HOW stamp differs');
const runT16 = runOf({ runwayAttempts: [runningRow(1, frozenWan)] });
const decision16 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, runT16.runwayAttempts),
  current: { ...liveRunway, picture: currentPictureInputOf(stateOf(shot, runT16), shot) },
  providerTaskId: 'task-run-1',
});
ok(decision16.promote && decision16.resultClass === 'CURRENT', 'T16 same picture promotes despite provider drift');

const lateRow = { ...successRow(1, frozenA, 'https://take/wan.mp4'), resultClass: 'LATE_RESULT' as const };
const runT17 = runOf({
  takeUrl: 'https://take/old.mp4',
  motionNeedsRemake: true,
  runwayAttempts: [lateRow],
});
const decision17 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, runT17.runwayAttempts),
  current: { ...frozenA, picture: currentPictureInputOf(stateOf(shot, runT17), shot) },
  providerTaskId: 'task-ok-1',
});
ok(decision17.promote, 'T17 rebind same-picture LATE take');
const patched17 = motionCallbackRunPatch({
  run: runT17,
  decision: decision17,
  outputUrl: 'https://take/wan.mp4',
  taskId: 'task-ok-1',
});
ok(patched17.takeUrl === 'https://take/wan.mp4' && patched17.motionNeedsRemake === false, 'T17 writes current take');
const execSame = derive(shot, runT17);
ok(!execSame.gates.pictureChanged, 'T17 remake flag does not hide same-picture take');
ok(execSame.currentArtifacts.motion?.url === 'https://take/wan.mp4', 'T17 current motion is Wan file');

const runT18 = runOf({
  keyframeDataUrl: KF_B,
  kfSourceHash: HASH_B,
  pictureRevisionId: 'picture:EP99-SC01-SH04:002',
  runwayAttempts: [lateRow],
});
const decision18 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, runT18.runwayAttempts),
  current: { ...liveB, picture: currentPictureInputOf(stateOf(shot, runT18), shot) },
  providerTaskId: 'task-ok-1',
});
ok(!decision18.promote && decision18.reason === 'INPUT_CHANGED', 'T18 other-picture LATE stays unbound');
ok(
  !playableMotionTakeOf(
    runOf({
      takeUrl: 'https://take/old.mp4',
      runwayAttempts: [{ n: 40, at: '', status: 'SUCCEEDED', taskId: 'wan_1', outputUrl: 'https://fal/wan.mp4' }],
    }),
  ),
  'T19 unstamped latest is not playable when PictureRevisionId exists',
);

const runT20 = runOf({
  takeUrl: 'https://take/44.mp4',
  turboTaskId: 'task-wan-45',
  pictureRevisionAttemptN: 44,
  runwayAttempts: [
    successRow(44, frozenA, 'https://take/44.mp4'),
    {
      ...successRow(45, { ...frozenA, pictureRevisionId: undefined }, 'https://take/45.mp4'),
      taskId: 'task-wan-45',
    },
  ],
});
const patched20 = motionCallbackRunPatch({
  run: runT20,
  decision: { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'MISSING_FREEZE' },
  outputUrl: 'https://take/45.mp4',
  taskId: 'task-wan-45',
});
ok(patched20.takeUrl === 'https://take/45.mp4', 'T20 our current Wan job writes takeUrl');
ok(
  ((patched20.runwayAttempts ?? []) as ExecutionAwareAttempt[]).some(
    (row) => row.taskId === 'task-wan-45' && row.frozenInput?.pictureRevisionId === 'picture:EP99-SC01-SH04:001',
  ),
  'T20 stamps live PictureRevisionId on our job',
);
ok(patched7.takeUrl !== 'https://take/late-a.mp4', 'T20 does not heal a late other task');

const root = dirname(fileURLToPath(import.meta.url));
const src = [
  'ShotProductionExecution.ts',
  'ShotProductionState.ts',
  'ShotProductionDirector.ts',
  '../ContentFamixaSeriesTab.tsx',
].map((p) => readFileSync(join(root, p), 'utf8')).join('\n');
ok(!/if\s*\(\s*shot\.id\s*===\s*['"]SH0[123]['"]/.test(src), 'no special-case SH0x');
ok(!src.includes('startContentSeriesTurbo') || src.includes('validateMotionCallback'), 'callback validation present');
ok(!/run\.execution\.(stage|status|currentArtifact)/.test(src), 'no persisted mutable execution status');

if (fail.length) {
  console.error(`ShotProductionExecution FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('ShotProductionExecution T1-T20 PASS');
void fpsBeat;
void runT7base;
