/** FAMIXA_DIRECTOR_CTA_EXECUTION_UNIFY_V1 — T1–T6. 0 providers. 0 media writes. */

import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import { shotI2vPromptHash } from './content-famixa-prod-v2';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { acceptExistingTake } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  directorExecutionNote,
  directorPictureVideoSurface,
  studioMotionSendOpts,
} from './ContentFamixaShotProduction/ShotProductionCta';
import {
  deriveShotExecutionState,
  executionDiagnosticLines,
  freezeMotionInput,
  motionAttemptId,
  type ExecutionAwareAttempt,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { buildShotProductionSnapshot, type ShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';

const fail: string[] = [];
const ok = (cond: unknown, name: string) => {
  if (!cond) fail.push(name);
};

const SHOT_ID = 'EP01-SC01-SH01';
const PIX5 = 'data:image/jpeg;base64,PIXEL005AAA';
const PIX6 = 'data:image/jpeg;base64,PIXEL006BBB';
const HASH5 = dataUriHash(PIX5);
const HASH6 = dataUriHash(PIX6);
const REV5 = `picture:${SHOT_ID}:005`;
const REV6 = `picture:${SHOT_ID}:006`;
ok(HASH5 !== HASH6, 'pixel hashes differ');

function shotOf(): FamixaSeriesShot {
  return {
    id: SHOT_ID,
    scene: 'SC01',
    sceneId: 'SC01',
    shot: 'SH01',
    clock: '5s',
    seconds: 5,
    story: 'Nhân vật đứng trong phòng và nói.',
    visual: 'Nhân vật đứng trong phòng và nói.',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'nhà',
    status: 'story_locked',
    dialogueSegmentIds: ['line-1'],
    actingBeat: {
      before: { action: 'stands still', body: 'He stays in place.', holdSec: 0.2 },
      during: { speech: true, emotion: 'calm', intensity: 1 },
      after: { holdSec: 0.2 },
    },
  };
}

function runOf(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'turbo_testing',
    kfApproved: true,
    keyframeDataUrl: PIX5,
    keyframeFileName: 'kf-EP01-SC01-SH01-canon.jpg',
    kfSourceHash: HASH5,
    pictureRevisionId: REV5,
    motionNeedsRemake: false,
    ...over,
  };
}

function stateOf(shot: FamixaSeriesShot, run: SeriesShotRun, opts?: { voiceReady?: boolean }): SeriesPilotState {
  return {
    roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'voice-1' }],
    runs: { [shot.id]: run },
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-1' }],
    lines: [{ id: 'line-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con.', voiceId: 'voice-1' }],
    voiceAssets: opts?.voiceReady === false ? {} : { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'Famixa',
      episode: 'EP01',
      title: 'EP01',
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
    attemptId: motionAttemptId(SHOT_ID, n),
    frozenInput: frozen,
    inputFingerprint: frozen.executionFingerprint,
    kf: { hash: frozen.keyframePixelHash },
    source: { hash: frozen.keyframePixelHash },
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

function snapOf(run: SeriesShotRun, opts?: { voiceReady?: boolean }): ShotProductionSnapshot {
  const shot = shotOf();
  return buildShotProductionSnapshot({
    state: stateOf(shot, run, opts),
    shot,
    ttsFiles: opts?.voiceReady === false ? {} : tts,
  });
}

function deskOf(snap: ShotProductionSnapshot) {
  const cmd = nextShotProductionCommand(snap);
  return {
    cmd,
    exec: snap.execution,
    desk: directorPictureVideoSurface({
      snap,
      cmd,
      hasLiveStill: true,
      kfApproved: true,
    }),
  };
}

const shot = shotOf();
const frozen5 = freezeMotionInput(stateOf(shot, runOf()), shot);
const frozen5NoVoice = freezeMotionInput(stateOf(shot, runOf(), { voiceReady: false }), shot);

const runT1 = runOf({
  takeUrl: 'https://take/m5.mp4',
  runwayAttempts: [successRow(52, frozen5NoVoice, 'https://take/m5.mp4')],
});
const snapT1 = snapOf(runT1, { voiceReady: false });
const t1 = deskOf(snapT1);
ok(snapT1.execution.currentInputs.picture.revisionId === REV5, 'T1 picture :005');
ok(snapT1.execution.currentArtifacts.motion?.validity === 'CURRENT', 'T1 motion :005 CURRENT');
ok(t1.cmd.type === 'ENSURE_VOICE', 'T1 nextAction ENSURE_VOICE');
ok(t1.desk.primary?.label !== 'Tạo video' && t1.desk.primary?.action !== 'motion', 'T1 Director CTA != Tạo video');
ok(t1.desk.note === 'Video hiện tại đã khớp hình. Tiếp tục phần thoại.', 'T1 note follows CURRENT + ENSURE_VOICE');

const runT2 = runOf({
  keyframeDataUrl: PIX6,
  kfSourceHash: HASH6,
  pictureRevisionId: REV6,
  motionNeedsRemake: true,
  takeUrl: 'https://take/m5.mp4',
  runwayAttempts: [successRow(52, frozen5, 'https://take/m5.mp4')],
});
const snapT2 = snapOf(runT2);
const t2 = deskOf(snapT2);
ok(snapT2.execution.currentInputs.picture.revisionId === REV6, 'T2 picture :006');
ok(
  snapT2.execution.currentArtifacts.motion?.validity !== 'CURRENT' &&
    (snapT2.execution.lastSuccessArtifacts.motion?.validity === 'STALE' ||
      snapT2.execution.visibleArtifacts.motion?.validity === 'STALE'),
  'T2 motion :005 STALE',
);
ok(t2.cmd.type === 'CONFIRM_MOTION', 'T2 nextAction CONFIRM_MOTION');
ok(t2.desk.primary?.label === 'Tạo video' && t2.desk.primary?.action === 'motion', 'T2 Director CTA = Tạo video');
ok(t2.desk.note === 'Hình đã duyệt. Video hiện tại không còn khớp hình.', 'T2 note follows STALE + CONFIRM_MOTION');

const accepted = acceptExistingTake(runT2);
ok(accepted?.acceptedTake.url === 'https://take/m5.mp4', 'T3 accept keeps visible take');
const runT3 = { ...runT2, ...accepted };
const snapT3 = snapOf(runT3);
const t3 = deskOf(snapT3);
ok(snapT3.execution.acceptedArtifacts.motion?.validity === 'ACCEPTED_STALE', 'T3 ACCEPTED_STALE');
ok(snapT3.execution.currentArtifacts.motion?.validity !== 'CURRENT', 'T3 does not fake CURRENT');
ok(t3.cmd.type !== 'CONFIRM_MOTION', 'T3 nextAction is not remake');
ok(t3.desk.primary?.label !== 'Tạo video' && t3.desk.primary?.action !== 'motion', 'T3 Director does not force remake');
ok(
  t3.cmd.type === 'ENSURE_VOICE' ||
    t3.cmd.type === 'WAIT_VOICE' ||
    t3.cmd.type === 'WAIT_VIDEO_REVIEW' ||
    t3.cmd.type === 'LIPSYNC_QA_REQUIRED' ||
    t3.cmd.type === 'CONFIRM_LIPSYNC' ||
    t3.cmd.type === 'ENSURE_MIX' ||
    t3.cmd.type === 'READY_FINAL',
  'T3 CTA follows nextAction (voice/lipsync/mix)',
);

const runT4 = runOf({
  takeUrl: 'https://take/m5.mp4',
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  runwayAttempts: [successRow(52, frozen5NoVoice, 'https://take/m5.mp4'), failRow(53, frozen5NoVoice)],
});
const snapT4 = snapOf(runT4, { voiceReady: false });
const t4 = deskOf(snapT4);
ok(snapT4.execution.attempts.at(-1)?.status === 'FAILED', 'T4 last attempt FAILED');
ok(snapT4.execution.currentArtifacts.motion?.validity !== 'FAILED', 'T4 failed attempt is not current');
ok(snapT4.execution.currentArtifacts.motion?.validity === 'CURRENT', 'T4 lastSuccess remains current when revision matches');
ok(t4.cmd.type === nextShotProductionCommand(snapT4).type, 'T4 CTA follows canonical recovery command');
ok(t4.desk.primary?.label !== 'Tạo video', 'T4 Director does not treat failed attempt as remake CTA');

const promptNow = shotI2vPromptHash(stateOf(shot, runOf()), shot);
const runT5 = runOf({
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  failedKfHash: HASH5,
  failedPromptHash: promptNow,
  takeUrl: undefined,
  runwayAttempts: [failRow(1, { ...frozen5, promptHash: promptNow })],
});
ok(sameFailedInput(runT5, HASH5, promptNow), 'T5 sameFailedInput true');
const snapT5 = snapOf(runT5);
ok(snapT5.execution.gates.sameFailedInput && snapT5.retryLocked, 'T5 execution circuit closed');
const bypass = { ...snapT5, takeFromOtherPicture: true };
const t5 = deskOf(bypass);
ok(t5.cmd.type !== 'CONFIRM_MOTION', 'T5 takeFromOtherPicture does not become CONFIRM_MOTION');
ok(t5.desk.primary?.label !== 'Tạo video' && t5.desk.primary?.action !== 'motion', 'T5 Director does not bypass circuit');
ok(!studioMotionSendOpts(bypass).remake, 'T5 remake send does not bypass circuit');

function sameExecutionStory(snap: ShotProductionSnapshot) {
  const cmd = nextShotProductionCommand(snap);
  const desk = directorPictureVideoSurface({ snap, cmd, hasLiveStill: true, kfApproved: true });
  const note = directorExecutionNote({ snap, cmd });
  const lines = executionDiagnosticLines({ ...snap.execution, nextAction: cmd });
  const currentId = snap.execution.currentArtifacts.motion?.artifactId || 'none';
  return {
    cmd,
    desk,
    note,
    lines,
    currentId,
    inspectorCurrent: lines.some((line) => line === `CURRENT MOTION ${currentId}`),
    inspectorNext: lines.some((line) => line === `NEXT ${cmd.type}`),
  };
}

const s1 = sameExecutionStory(snapT1);
const s2 = sameExecutionStory(snapT2);
ok(s1.desk.note === s1.note && s1.inspectorCurrent && s1.inspectorNext, 'T6 T1 note + Inspector share execution');
ok(s2.desk.note === s2.note && s2.inspectorCurrent && s2.inspectorNext, 'T6 T2 note + Inspector share execution');
ok(s1.note.includes('khớp hình') && s1.cmd.type === 'ENSURE_VOICE', 'T6 CURRENT + ENSURE_VOICE described once');
ok(s2.note.includes('không còn khớp hình') && s2.cmd.type === 'CONFIRM_MOTION', 'T6 STALE + CONFIRM_MOTION described once');

const t1ForcedOther = deskOf({ ...snapT1, takeFromOtherPicture: true });
ok(t1ForcedOther.cmd.type === 'ENSURE_VOICE', 'T1 overlay takeFromOtherPicture cannot change voice-first command');
ok(t1ForcedOther.desk.primary?.label !== 'Tạo video', 'T1 overlay cannot override Director CTA');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_CTA_EXECUTION_UNIFY_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_CTA_EXECUTION_UNIFY_V1 PASS FAIL=0 (no provider)');
void deriveShotExecutionState;
