/** FAMIXA_AI_PROVIDER_LATE_JOB_CURRENT_LOCK_V1 — T1–T15. 0 providers. */

import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import { acceptExistingTake } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  adaptMotionAttempts,
  currentMotionCandidateAttempt,
  currentPictureInputOf,
  freezeMotionInput,
  isMotionAttemptSuperseded,
  motionAttemptId,
  motionCallbackRunPatch,
  validateMotionCallback,
  type ExecutionAwareAttempt,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const KF = 'data:image/jpeg;base64,LOCKPIXAAA';
const KF_B = 'data:image/jpeg;base64,LOCKPIXBBB';
const HASH = dataUriHash(KF);
const HASH_B = dataUriHash(KF_B);

const shot: FamixaSeriesShot = {
  id: 'EP01-SC01-SH01',
  shot: 'SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  story: 'Minh bước vào nhà',
  seconds: 5,
  characters: ['CHAR-003'],
  characterIds: ['CHAR-003'],
  dialogueSegmentIds: ['line-1'],
  actingBeat: { before: { action: 'stand', holdSec: 0.2 }, during: { speech: true, emotion: 'neutral' }, after: { holdSec: 0.22 } },
};

const run = (extra?: Partial<SeriesShotRun>): SeriesShotRun => ({
  kfApproved: true,
  kfSourceHash: HASH,
  keyframeDataUrl: KF,
  pictureRevisionId: 'picture:EP01-SC01-SH01:006',
  ...extra,
});

const stateOf = (r: SeriesShotRun, s: FamixaSeriesShot = shot): SeriesPilotState => ({
  schemaVersion: 4,
  roles: [{ id: 'role-CHAR-003', title: 'Mẹ', name: 'Linh', characterId: 'CHAR-003', voiceId: 'kOIUKJ9E0DPndqTtvJm5' }],
  episode: { episode: 'EP01', title: 'Tập 01', shots: [s] },
  lines: [{ id: 'line-1', text: 'Về rồi hả con?', characterId: 'CHAR-003', voiceId: 'kOIUKJ9E0DPndqTtvJm5' }],
  characters: [{ id: 'CHAR-003', name: 'Linh', voiceId: 'kOIUKJ9E0DPndqTtvJm5' }],
  voiceAssets: { 'line-1': { lineId: 'line-1', duration: 1.5, status: 'ready', characterId: 'CHAR-003' } },
  runs: { [s.id]: r },
});

function frozenOf(r: SeriesShotRun, s: FamixaSeriesShot = shot, provider: 'runway' | 'wan' = 'runway') {
  return freezeMotionInput(stateOf(r, s), s, { provider });
}

function currentOf(r: SeriesShotRun, s: FamixaSeriesShot = shot, provider: 'runway' | 'wan' = 'runway') {
  return { ...frozenOf(r, s, provider), picture: currentPictureInputOf(stateOf(r, s), s) };
}

function row(n: number, frozen: ReturnType<typeof freezeMotionInput>, status: 'RUNNING' | 'SUCCEEDED', url?: string): ExecutionAwareAttempt {
  return {
    n,
    at: `2026-09-09T00:00:${String(n).padStart(2, '0')}.000Z`,
    status,
    taskId: `task-${n}`,
    outputUrl: url,
    attemptId: motionAttemptId(shot.id, n),
    frozenInput: frozen,
    inputFingerprint: frozen.executionFingerprint,
    kf: { hash: frozen.keyframePixelHash },
    source: { hash: frozen.keyframePixelHash },
  };
}

const base = run();
const frozen = frozenOf(base);

const one = run({ runwayAttempts: [row(1, frozen, 'RUNNING')] });
const t1 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, one.runwayAttempts),
  current: currentOf(one),
  providerTaskId: 'task-1',
});
ok(t1.promote && t1.resultClass === 'CURRENT', 'T1 single job success → CURRENT');

const aDone = run({
  takeUrl: 'https://take/a.mp4',
  previewUrl: 'https://take/a.mp4',
  turboTaskId: 'task-10',
  runwayAttempts: [row(10, frozen, 'SUCCEEDED', 'https://take/a.mp4')],
});
const t2 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, aDone.runwayAttempts),
  current: currentOf(aDone),
  providerTaskId: 'task-10',
});
ok(t2.promote && t2.resultClass === 'CURRENT', 'T2 A SUCCESS → CURRENT');

const bothSuccess = run({
  takeUrl: 'https://take/b.mp4',
  previewUrl: 'https://take/b.mp4',
  turboTaskId: 'task-11',
  shotProduction: { motionFp: 'stamp-b' },
  runwayAttempts: [
    row(10, frozen, 'SUCCEEDED', 'https://take/a.mp4'),
    row(11, frozen, 'SUCCEEDED', 'https://take/b.mp4'),
  ],
});
const adaptedBoth = adaptMotionAttempts(shot.id, bothSuccess.runwayAttempts);
ok(currentMotionCandidateAttempt(adaptedBoth)?.n === 11, 'T3 candidate is 11');
const t3 = validateMotionCallback({
  attempts: adaptedBoth,
  current: currentOf(bothSuccess),
  providerTaskId: 'task-11',
});
ok(t3.promote && t3.resultClass === 'CURRENT', 'T3 B SUCCESS → B CURRENT');

const t4 = validateMotionCallback({
  attempts: adaptedBoth,
  current: currentOf(bothSuccess),
  providerTaskId: 'task-10',
});
ok(!t4.promote && t4.resultClass === 'LATE_RESULT' && t4.reason === 'SUPERSEDED', 'T4 A late SUCCESS → not CURRENT');
const patch4 = motionCallbackRunPatch({
  run: bothSuccess,
  decision: t4,
  outputUrl: 'https://take/a.mp4',
  taskId: 'task-10',
});
ok(patch4.takeUrl == null && patch4.previewUrl == null, 'T4 A does not overwrite takeUrl');

const aStillRunning = run({
  takeUrl: 'https://take/b.mp4',
  previewUrl: 'https://take/b.mp4',
  turboTaskId: 'task-11',
  shotProduction: { motionFp: 'stamp-b' },
  runwayAttempts: [
    row(10, frozen, 'RUNNING'),
    row(11, frozen, 'SUCCEEDED', 'https://take/b.mp4'),
  ],
});
const t5 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, aStillRunning.runwayAttempts),
  current: currentOf(aStillRunning),
  providerTaskId: 'task-10',
});
ok(!t5.promote && t5.reason === 'SUPERSEDED', 'T5 A late after B SUCCESS → SUPERSEDED');
const patch5 = motionCallbackRunPatch({
  run: aStillRunning,
  decision: t5,
  outputUrl: 'https://take/a-late.mp4',
  taskId: 'task-10',
});
ok(patch5.takeUrl == null && patch5.previewUrl == null, 'T5 B remains CURRENT on graph fields');
ok(patch5.shotProduction == null, 'T15 old attempt does not rewrite production stamp');
ok((patch5.runwayAttempts ?? []).some((r) => (r as ExecutionAwareAttempt).resultClass === 'LATE_RESULT'), 'T5 A stored LATE_RESULT');

const otherPic = run({
  keyframeDataUrl: KF_B,
  kfSourceHash: HASH_B,
  pictureRevisionId: 'picture:EP01-SC01-SH01:007',
  runwayAttempts: [row(10, frozen, 'RUNNING'), row(11, frozenOf(run({ keyframeDataUrl: KF_B, kfSourceHash: HASH_B, pictureRevisionId: 'picture:EP01-SC01-SH01:007' })), 'RUNNING')],
});
const t6 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, otherPic.runwayAttempts),
  current: currentOf(otherPic),
  providerTaskId: 'task-10',
});
ok(!t6.promote && t6.resultClass === 'LATE_RESULT', 'T6 different Picture → LATE_RESULT');

const beatShot: FamixaSeriesShot = {
  ...shot,
  actingBeat: { before: { action: 'walk in', holdSec: 0.4 }, during: { speech: true, emotion: 'urgent' }, after: { holdSec: 0.3 } },
};
const whatNow = currentOf(base, beatShot);
ok(whatNow.executionFingerprint !== frozen.executionFingerprint, 'T7 WHAT differs');
const t7 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, [row(10, frozen, 'RUNNING')]),
  current: whatNow,
  providerTaskId: 'task-10',
});
ok(!t7.promote && t7.resultClass === 'LATE_RESULT', 'T7 different WHAT → late');

const frozenWan = frozenOf(base, shot, 'wan');
ok(frozen.executionFingerprint === frozenWan.executionFingerprint, 'T8 WHAT same across providers');
const switchRun = run({
  takeUrl: 'https://take/wan.mp4',
  previewUrl: 'https://take/wan.mp4',
  turboTaskId: 'task-11',
  runwayAttempts: [
    { ...row(10, frozen, 'RUNNING'), model: 'gen4_turbo' },
    { ...row(11, frozenWan, 'SUCCEEDED', 'https://take/wan.mp4'), model: 'wan-2.1' },
  ],
});
const t8 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, switchRun.runwayAttempts),
  current: currentOf(switchRun, shot, 'wan'),
  providerTaskId: 'task-10',
});
ok(!t8.promote && t8.reason === 'SUPERSEDED', 'T8 older Runway after newer Wan → SUPERSEDED');
ok(!isMotionAttemptSuperseded(adaptMotionAttempts(shot.id, [row(11, frozenWan, 'SUCCEEDED', 'https://take/wan.mp4')]), adaptMotionAttempts(shot.id, [row(11, frozenWan, 'SUCCEEDED', 'https://take/wan.mp4')])[0]!), 'T8 newer itself not superseded');

const accepted = acceptExistingTake({
  takeUrl: 'https://take/keep.mp4',
  previewUrl: 'https://take/keep.mp4',
  kfSourceHash: HASH,
  runwayAttempts: [{ n: 9, status: 'SUCCEEDED', outputUrl: 'https://take/keep.mp4' }],
});
ok(Boolean(accepted?.acceptedTake?.url), 'T9 ACCEPT_EXISTING helper');
const acceptedRun = run({
  ...accepted,
  takeUrl: 'https://take/b.mp4',
  acceptedTake: accepted?.acceptedTake,
  runwayAttempts: [
    row(10, frozen, 'RUNNING'),
    row(11, frozen, 'SUCCEEDED', 'https://take/b.mp4'),
  ],
});
const t9 = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, acceptedRun.runwayAttempts),
  current: currentOf(acceptedRun),
  providerTaskId: 'task-10',
});
const patch9 = motionCallbackRunPatch({
  run: acceptedRun,
  decision: t9,
  outputUrl: 'https://take/a-late.mp4',
  taskId: 'task-10',
});
ok(!t9.promote && patch9.acceptedTake == null && patch9.takeUrl == null, 'T9 late callback does not replace accepted/visible');

ok(
  !sameFailedInput({ failedKfHash: 'other', failedPromptHash: 'p', runwayAttempts: [] }, HASH, 'p'),
  'T10 sameFailedInput still hash-based',
);

ok(t4.attempt?.attemptId === motionAttemptId(shot.id, 10), 'T11 callback identity kept');
ok(t5.attempt?.providerTaskId === 'task-10', 'T11 task identity kept');

const t12 = validateMotionCallback({
  attempts: adaptedBoth,
  current: currentOf(bothSuccess),
  providerTaskId: 'task-11',
});
ok(t12.promote && t12.resultClass === 'CURRENT', 'T12 idempotent CURRENT callback safe');

ok(patch5.takeUrl == null, 'T13 old attempt cannot overwrite takeUrl');
ok(patch5.previewUrl == null, 'T14 old attempt cannot overwrite previewUrl');
ok(patch5.shotProduction == null && aStillRunning.shotProduction?.motionFp === 'stamp-b', 'T15 stamp stays on current run');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_LATE_JOB_CURRENT_LOCK_V1 T1–T15');
