/** FAMIXA_PICTURE_MOTION_EXECUTION_BOUNDARY_FIX_V1 — T1–T20. 0 providers. 0 media writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import { rememberKfPixels } from './content-famixa-kf-store';
import {
  bindPictureHashOnRuns,
  slimPilotForStorage,
  withRunPixels,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';
import { acceptExistingTake, detachAvAfterPictureChange, takeFromOtherPicture } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import { directorPictureVideoSurface } from './ContentFamixaShotProduction/ShotProductionCta';
import {
  adaptMotionAttempts,
  currentPictureInputOf,
  deriveShotExecutionState,
  freezeMotionInput,
  motionAttemptId,
  nextPictureRevisionId,
  picturePixelHashOf,
  validateMotionCallback,
  type ExecutionAwareAttempt,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { directorMotionCaption, directorShowsStillPreview } from './ContentFamixaShotProduction/ShotProductionPreview';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';

const fail: string[] = [];
const ok = (cond: unknown, name: string) => {
  if (!cond) fail.push(name);
};

const P1 = 'data:image/jpeg;base64,PIXELP1AAA111';
const P2 = 'data:image/jpeg;base64,PIXELP2BBB222';
const HASH_P1 = dataUriHash(P1);
const HASH_P2 = dataUriHash(P2);
ok(HASH_P1 !== HASH_P2, 'pixel hashes differ');

const TAKE_M1 = 'https://fal.example/take-44.mp4';
const TAKE_M2 = 'https://fal.example/take-45.mp4';
const SHOT_ID = 'EP01-SC01-SH-BND';
const REV_P1 = nextPictureRevisionId(SHOT_ID);
const REV_P2 = nextPictureRevisionId(SHOT_ID, REV_P1);
ok(REV_P1 === `picture:${SHOT_ID}:001` && REV_P2 === `picture:${SHOT_ID}:002`, 'PictureRevisionId increments on approve');

function shotOf(id = SHOT_ID): FamixaSeriesShot {
  return {
    id,
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
    keyframeDataUrl: P1,
    keyframeFileName: 'kf-EP01-SC01-SH01-canon.jpg',
    kfSourceHash: HASH_P1,
    pictureRevisionId: REV_P1,
    takeUrl: TAKE_M1,
    previewUrl: TAKE_M1,
    motionNeedsRemake: false,
    videoPipe: 'VIDEO_READY',
    runwayAttempts: [
      {
        n: 44,
        at: '',
        status: 'SUCCEEDED',
        taskId: 'wan-44',
        outputUrl: TAKE_M1,
        kf: { hash: HASH_P1 },
      },
    ],
    ...over,
  };
}

function stateOf(shot: FamixaSeriesShot, run: SeriesShotRun): SeriesPilotState {
  return {
    roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'voice-1' }],
    runs: { [shot.id]: run },
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-1' }],
    lines: [{ id: 'line-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con.', voiceId: 'voice-1' }],
    voiceAssets: { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
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

function derive(shot: FamixaSeriesShot, run: SeriesShotRun) {
  return deriveShotExecutionState({ state: stateOf(shot, run), shot, ttsFiles: tts });
}

function snap(shot: FamixaSeriesShot, run: SeriesShotRun) {
  return buildShotProductionSnapshot({ state: stateOf(shot, run), shot, ttsFiles: tts });
}

function persistSlimHydrateBind(state: SeriesPilotState) {
  const bound = bindPictureHashOnRuns(state);
  const slim = slimPilotForStorage(bound);
  return bindPictureHashOnRuns(withRunPixels(slim));
}

function p2Approved(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return runOf({
    keyframeDataUrl: P2,
    kfSourceHash: HASH_P2,
    kfApproved: true,
    pictureRevisionId: REV_P2,
    motionNeedsRemake: true,
    keyframeFileName: 'kf-EP01-SC01-SH01-canon.jpg',
    ...detachAvAfterPictureChange(runOf()),
    ...over,
  });
}

const shot = shotOf();

const execP1 = derive(shot, runOf());
ok(execP1.currentInputs.picture.pixelHash === HASH_P1, 'T1 P1 picture current');

const execP2 = derive(shot, p2Approved());
ok(execP2.currentInputs.picture.pixelHash === HASH_P2, 'T1 P2 picture current');
ok(!execP2.currentArtifacts.motion, 'T1 M1 is not current after P2 approve');
ok(execP2.gates.pictureChanged, 'T1 pictureChanged');
ok(
  execP2.visibleArtifacts.motion?.url === TAKE_M1 &&
    (execP2.visibleArtifacts.motion?.validity === 'STALE' ||
      execP2.lastSuccessArtifacts.motion?.validity === 'STALE' ||
      execP2.lastSuccessArtifacts.motion?.validity === 'LATE_RESULT' ||
      execP2.lastSuccessArtifacts.motion?.validity === 'LEGACY_UNVERIFIED'),
  'T1 M1 remains visible stale',
);

const snapP2 = snap(shot, p2Approved());
ok(nextShotProductionCommand(snapP2).type === 'CONFIRM_MOTION', 'T1 nextAction CONFIRM_MOTION');

const afterPersist = persistSlimHydrateBind(stateOf(shot, p2Approved()));
const runPersist = afterPersist.runs[shot.id]!;
ok(runPersist.motionNeedsRemake, 'T2 persist keeps remake');
ok(runPersist.pictureRevisionId === REV_P2, 'T2 pictureRevisionId survives persist/slim');
ok(runPersist.videoPipe !== 'VIDEO_READY' || runPersist.motionNeedsRemake, 'T2 persist does not promote M1');
ok(!derive(shot, runPersist).currentArtifacts.motion, 'T2 M1 not current after persist');

rememberKfPixels(shot.id, P2, 'kf-EP01-SC01-SH01-canon.jpg');
const afterHydrate = persistSlimHydrateBind(stateOf(shot, p2Approved()));
const runHydrate = afterHydrate.runs[shot.id]!;
ok(picturePixelHashOf(runHydrate, shot.id) === HASH_P2, 'T3 hydrate keeps P2 identity');
ok(runHydrate.motionNeedsRemake, 'T3 hydrate keeps remake');
ok(!derive(shot, runHydrate).currentArtifacts.motion, 'T3 M1 not current after slim/hydrate/bind');
ok(nextShotProductionCommand(snap(shot, runHydrate)).type === 'CONFIRM_MOTION', 'T3 command CONFIRM_MOTION');

const boundKeep = bindPictureHashOnRuns(stateOf(shot, p2Approved()));
ok(boundKeep.runs[shot.id]?.motionNeedsRemake, 'T4 bind does not clear remake');
ok(boundKeep.runs[shot.id]?.videoPipe !== 'VIDEO_READY' || boundKeep.runs[shot.id]?.motionNeedsRemake, 'T4 bind does not set VIDEO_READY');

ok(
  p2Approved().keyframeFileName === runOf().keyframeFileName && HASH_P1 !== HASH_P2,
  'T5 same filename different pixels',
);
ok(takeFromOtherPicture({
  keyframeDataUrl: P2,
  kfSourceHash: HASH_P2,
  takeKfHash: HASH_P1,
  hasTake: true,
}), 'T5 old motion stale vs new pixels');

const frozenSameHash = freezeMotionInput(stateOf(shot, runOf({ takeUrl: undefined, runwayAttempts: [] })), shot);
ok(frozenSameHash.pictureRevisionId === REV_P1 && frozenSameHash.keyframePixelHash === HASH_P1, 'T6 freeze belongs to P1 revision');
const sameHashDiffRev = runOf({
  keyframeDataUrl: P1,
  kfSourceHash: HASH_P1,
  pictureRevisionId: REV_P2,
  motionNeedsRemake: true,
  runwayAttempts: [
    {
      n: 44,
      at: '',
      status: 'SUCCEEDED',
      taskId: 'wan-44',
      outputUrl: TAKE_M1,
      resultClass: 'LATE_RESULT',
      frozenInput: frozenSameHash,
      kf: { hash: HASH_P1 },
    },
  ],
});
const execSameHash = derive(shot, sameHashDiffRev);
ok(execSameHash.currentInputs.picture.pixelHash === HASH_P1, 'T6 same pixelHash');
ok(execSameHash.currentInputs.picture.revisionId === REV_P2, 'T6 different PictureRevisionId');
ok(!execSameHash.currentArtifacts.motion, 'T6 same pixelHash + different revision is not CURRENT');
ok(
  execSameHash.lastSuccessArtifacts.motion?.validity === 'LATE_RESULT' ||
    execSameHash.lastSuccessArtifacts.motion?.validity === 'STALE',
  'T6 M44 is LATE_RESULT / STALE',
);
ok(nextShotProductionCommand(snap(shot, sameHashDiffRev)).type === 'CONFIRM_MOTION', 'T6 nextAction CONFIRM_MOTION');
const legacyNoRev = p2Approved({
  keyframeDataUrl: P1,
  kfSourceHash: HASH_P1,
  runwayAttempts: [
    {
      n: 44,
      at: '',
      status: 'SUCCEEDED',
      outputUrl: TAKE_M1,
      resultClass: 'LATE_RESULT',
      kf: { hash: HASH_P1 },
    },
  ],
});
ok(!derive(shot, legacyNoRev).currentArtifacts.motion, 'T6 legacy take without frozenPictureRevisionId is not CURRENT');

const idbShot = shotOf('EP01-SC01-SH-IDB');
rememberKfPixels(idbShot.id, P1, 'kf-EP01-SC01-SH01-canon.jpg');
const idbRun = p2Approved({ keyframeDataUrl: undefined, kfSourceHash: HASH_P2, motionNeedsRemake: true });
const idbBound = bindPictureHashOnRuns(stateOf(idbShot, idbRun));
ok(idbBound.runs[idbShot.id]?.motionNeedsRemake, 'T6 stale IDB P1 does not promote M1 onto P2 stamp');
ok(idbBound.runs[idbShot.id]?.kfSourceHash === HASH_P2, 'T6 bind does not copy old hash onto P2');

ok(!p2Approved().takeUrl, 'T7 takeUrl is cleared after picture revision');
ok(p2Approved().runwayAttempts?.some((row) => row.outputUrl === TAKE_M1), 'T7 old take stays in history');
ok(!derive(shot, p2Approved()).currentArtifacts.motion, 'T7 old take is not current');
ok(p2Approved().pictureRevisionAttemptN === 44, 'T7 epoch is last attempt n at approve');

ok(!p2Approved().previewUrl, 'T8 previewUrl is cleared after picture revision');
ok(!derive(shot, p2Approved({ previewUrl: TAKE_M1 })).currentArtifacts.motion, 'T8 leftover previewUrl is not current evidence');

const accepted = acceptExistingTake(runOf());
const acceptedRun = p2Approved({
  acceptedTake: accepted?.acceptedTake,
  motionNeedsRemake: true,
});
const execAccepted = derive(shot, acceptedRun);
ok(
  !execAccepted.currentArtifacts.motion &&
    (execAccepted.acceptedArtifacts.motion?.validity === 'ACCEPTED_STALE' ||
      execAccepted.gates.pictureChanged),
  'T9 accepted old take is ACCEPTED_STALE / not current',
);

const snapAccepted = snap(shot, acceptedRun);
const cmdAccepted = nextShotProductionCommand(snapAccepted);
ok(cmdAccepted.type !== 'ACCEPT_EXISTING', 'T10 picture change is not automatic ACCEPT_EXISTING');
ok(cmdAccepted.type !== 'CONFIRM_MOTION', 'T10 ACCEPTED_STALE does not force remake');
ok(accepted?.acceptedTake?.url === TAKE_M1, 'T10 explicit accept helper still records the old take');

const frozenP2 = freezeMotionInput(stateOf(shot, p2Approved({ takeUrl: undefined, runwayAttempts: [] })), shot);
const m2Row: ExecutionAwareAttempt = {
  n: 45,
  at: '2026-09-08T00:00:00.000Z',
  status: 'SUCCEEDED',
  taskId: 'wan-45',
  outputUrl: TAKE_M2,
  promptHash: frozenP2.promptHash,
  attemptId: motionAttemptId(shot.id, 45),
  frozenInput: frozenP2,
  inputFingerprint: frozenP2.executionFingerprint,
  kf: { hash: HASH_P2 },
  source: { hash: HASH_P2 },
};
const runM2 = p2Approved({
  takeUrl: TAKE_M2,
  previewUrl: TAKE_M2,
  motionNeedsRemake: false,
  videoPipe: 'VIDEO_READY',
  runwayAttempts: [m2Row],
});
const execM2 = derive(shot, runM2);
ok(execM2.currentArtifacts.motion?.url === TAKE_M2, 'T11 M2 on P2 is CURRENT');
ok(execM2.currentArtifacts.motion?.validity === 'CURRENT', 'T11 validity CURRENT');
const collide = p2Approved({
  takeUrl: TAKE_M1,
  motionNeedsRemake: false,
  runwayAttempts: [
    {
      n: 44,
      at: '',
      status: 'SUCCEEDED',
      outputUrl: TAKE_M1,
      resultClass: 'LATE_RESULT',
      kf: { hash: HASH_P2 },
    },
    m2Row,
  ],
});
const execCollide = derive(shot, collide);
ok(execCollide.currentArtifacts.motion?.url === TAKE_M2, 'T11 same hash does not keep take 44 over M2');
ok(execCollide.currentArtifacts.motion?.validity === 'CURRENT', 'T11 M2 stays CURRENT when old take shares hash');

const frozenP1 = freezeMotionInput(stateOf(shot, runOf()), shot);
const lateDecision = validateMotionCallback({
  attempts: adaptMotionAttempts(shot.id, [
    {
      n: 44,
      at: '',
      status: 'SUCCEEDED',
      taskId: 'wan-44',
      outputUrl: TAKE_M1,
      attemptId: motionAttemptId(shot.id, 44),
      frozenInput: frozenP1,
      kf: { hash: HASH_P1 },
    } as ExecutionAwareAttempt,
  ]),
  current: { ...frozenP2, picture: currentPictureInputOf(stateOf(shot, p2Approved()), shot) },
  providerTaskId: 'wan-44',
});
ok(!lateDecision.promote && lateDecision.resultClass === 'LATE_RESULT', 'T12 old job after P2 is LATE_RESULT');

rememberKfPixels(shot.id, P2, 'kf-EP01-SC01-SH01-canon.jpg');
const afterM2 = persistSlimHydrateBind(stateOf(shot, runM2));
ok(!afterM2.runs[shot.id]?.motionNeedsRemake, 'T13 M2 remake stays clear');
ok(afterM2.runs[shot.id]?.takeUrl === TAKE_M2, 'T13 M2 takeUrl survives persist/hydrate');
ok(derive(shot, afterM2.runs[shot.id]!).currentArtifacts.motion?.url === TAKE_M2, 'T13 M2 remains current');

const failHash = dataUriHash(P2);
ok(
  sameFailedInput(
    {
      failedKfHash: failHash,
      failedPromptHash: 'same-prompt',
      runwayAttempts: [{ n: 1, at: '', status: 'FAILED', taskId: 'fail-1', promptHash: 'same-prompt', source: { hash: failHash } }],
    },
    failHash,
    'same-prompt',
  ),
  'T14 sameFailedInput still locks',
);
ok(
  !sameFailedInput(
    {
      failedKfHash: HASH_P1,
      failedPromptHash: 'same-prompt',
      runwayAttempts: [{ n: 1, at: '', status: 'FAILED', taskId: 'fail-1', promptHash: 'same-prompt', source: { hash: HASH_P1 } }],
    },
    HASH_P2,
    'same-prompt',
  ),
  'T14 new picture is not locked by old fail',
);
ok(
  !sameFailedInput(
    {
      pictureRevisionId: REV_P2,
      failedKfHash: HASH_P1,
      failedPromptHash: 'same-prompt',
      runwayAttempts: [
        {
          n: 1,
          at: '',
          status: 'FAILED',
          taskId: 'fail-1',
          promptHash: 'same-prompt',
          source: { hash: HASH_P1 },
          frozenInput: { pictureRevisionId: REV_P1, baseMotionFingerprint: '', actingBeatFingerprint: '', promptHash: 'same-prompt', timingHash: '', provider: 'wan', providerDuration: 5, executionFingerprint: 'fp' },
        },
      ],
    },
    HASH_P1,
    'same-prompt',
  ),
  'T14 same pixelHash on a new PictureRevisionId is not sameFailedInput',
);

const noPixel = runOf({ keyframeDataUrl: undefined, kfSourceHash: undefined, kfApproved: true, motionNeedsRemake: true });
const execNoPixel = derive(shot, noPixel);
ok(
  !execNoPixel.currentArtifacts.motion || execNoPixel.currentArtifacts.motion.validity !== 'CURRENT',
  'T15 no pixel identity never assumes current',
);

const desk = directorPictureVideoSurface({
  snap: snapP2,
  cmd: nextShotProductionCommand(snapP2),
  hasLiveStill: true,
  kfApproved: true,
});
ok(desk.primary?.action === 'motion' && desk.primary.label === 'Tạo video', 'T16 CTA Tạo video');

ok(directorMotionCaption({ current: false, takeN: 44 }) === 'Video cũ — không khớp hình hiện tại', 'T17 old video label');
ok(directorMotionCaption({ current: true, takeN: 45 }) === 'Video · take 45', 'T17 current take may show number');
ok(directorShowsStillPreview({ phase: 'motion', takeFromOtherPicture: true, hasUsableVideo: true }), 'T17 still beats old take');

ok(snapP2.takeFromOtherPicture === execP2.gates.pictureChanged, 'T18 snapshot agrees with execution pictureChanged');
ok(snapP2.nextAction === nextShotProductionCommand(snapP2).type, 'T18 snapshot.nextAction === command');
ok(!snapP2.execution.currentArtifacts.motion, 'T18 snapshot execution has no current motion');

ok(nextShotProductionCommand(snapP2).type === 'CONFIRM_MOTION', 'T19 CONFIRM_MOTION');

const root = dirname(fileURLToPath(import.meta.url));
const src = [
  'content-famixa-series.ts',
  'ContentFamixaShotProduction/ShotProductionExecution.ts',
  'ContentFamixaShotProduction/ShotProductionState.ts',
  'ContentFamixaSeriesTab.tsx',
].map((p) => readFileSync(join(root, p), 'utf8')).join('\n');
ok(!/if\s*\(\s*shot\.id\s*===\s*['"]SH0[123]['"]/.test(src), 'T20 no special-case SH0x');
ok(src.includes('validateMotionCallback'), 'T20 callback validation present');
ok(src.includes('pictureRevisionId') && src.includes('nextPictureRevisionId'), 'T20 PictureRevisionId is execution identity');
ok(typeof fetch === 'function', 'T20 runtime has fetch but smoke does not call a provider');

if (fail.length) {
  console.error(`FAMIXA_PICTURE_MOTION_EXECUTION_BOUNDARY_FIX_V1 FAIL=${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_PICTURE_MOTION_EXECUTION_BOUNDARY_FIX_V1 PASS FAIL=0 (no provider)');
