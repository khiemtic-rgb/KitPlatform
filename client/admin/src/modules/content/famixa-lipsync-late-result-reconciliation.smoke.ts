/** FAMIXA_LIPSYNC_LATE_RESULT_RECONCILIATION_V1 — T1–T9. 0 providers. 0 graph writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash } from './content-famixa-runway-pipe';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import {
  LIPSYNC_MOTION_AUTHORITY_ID,
  lipsyncBoundDurationSec,
  lipsyncBoundMotionStillAuthority,
} from './famixa-lipsync-motion-authority';
import { performanceDurationOf } from './famixa-shot-production-timing';
import { DIRECTOR_LIPSYNC_LABEL, directorPictureVideoSurface } from './ContentFamixaShotProduction/ShotProductionCta';
import {
  deriveShotExecutionState,
  freezeLipSyncInput,
  freezeMixInput,
  freezeMotionInput,
  motionAttemptId,
  motionCallbackRunPatch,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { applyProductionStamps, computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const KF = 'data:image/jpeg;base64,PIXELRECONCILE55';
const HASH = dataUriHash(KF);
const SHOT_ID = 'EP01-SC01-SH09';
const LIP_URL = 'https://v3b.fal.media/files/b/recon/lip.mp4';

function shotOf(): FamixaSeriesShot {
  return {
    id: SHOT_ID,
    scene: 'SC01',
    sceneId: 'SC01',
    shot: 'SH09',
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
      before: { action: 'stands still', holdSec: 0.2 },
      during: { speech: true, emotion: 'calm', intensity: 1 },
      after: { holdSec: 0.2 },
    },
  };
}

function baseRun(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'reviewed',
    kfApproved: true,
    videoApproved: true,
    keyframeDataUrl: KF,
    keyframeFileName: 'kf.jpg',
    kfSourceHash: HASH,
    pictureRevisionId: `picture:${SHOT_ID}:001`,
    lipsyncTaskId: 'lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f',
    lipsyncStatus: 'SUCCEEDED',
    lipsynced: true,
    lipsyncUrl: LIP_URL,
    finalSource: 'FAL',
    shotQa: { action: true, continuity: true, voiceFace: true, lipsync: true, lipsyncQuality: true, finalAv: true },
    ...over,
  };
}

function stateOf(run: SeriesShotRun): SeriesPilotState {
  const shot = shotOf();
  return {
    roles: [{ id: 'role-1', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'voice-1' }],
    runs: { [shot.id]: run },
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-1' }],
    lines: [{ id: 'line-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con.', voiceId: 'voice-1' }],
    voiceAssets: { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'F',
      episode: 'EP01',
      title: 'RECONCILE',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
  };
}

const tts = { 'line-1': { url: 'blob:voice', fileName: 'a.mp3' } };

function attempt55(frozen: ReturnType<typeof freezeMotionInput>) {
  return {
    n: 55,
    at: '2026-09-14T00:00:00.000Z',
    status: 'SUCCEEDED' as const,
    resultClass: 'CURRENT' as const,
    duration: 5,
    taskId: 'veo_take55',
    outputUrl: 'https://take/55.mp4',
    attemptId: motionAttemptId(SHOT_ID, 55),
    frozenInput: frozen,
    inputFingerprint: frozen.executionFingerprint,
    promptHash: frozen.promptHash,
    kf: { hash: frozen.keyframePixelHash },
    source: { hash: frozen.keyframePixelHash },
  };
}

function attemptLate56() {
  return {
    n: 56,
    at: '2026-09-14T01:00:00.000Z',
    status: 'SUCCEEDED' as const,
    resultClass: 'LATE_RESULT' as const,
    duration: 6,
    taskId: 'veo_take56',
    outputUrl: 'https://take/56.mp4',
    attemptId: motionAttemptId(SHOT_ID, 56),
    promptHash: 'late',
    kf: { hash: HASH },
    source: { hash: HASH },
  };
}

function withBoundLip(run: SeriesShotRun, state: SeriesPilotState) {
  const shot = shotOf();
  const fps = computeInputFingerprints(state, shot);
  const lipsyncFrozenInput = freezeLipSyncInput(state, shot, { motionArtifactId: motionAttemptId(SHOT_ID, 55) });
  const mixFrozenInput = freezeMixInput(state, shot, { videoArtifactId: LIP_URL });
  return {
    ...run,
    takeUrl: 'https://take/55.mp4',
    previewUrl: 'https://take/55.mp4',
    lipsyncFrozenInput,
    mixFrozenInput,
    shotProduction: {
      voiceFp: fps.voice,
      kfFp: fps.keyframe,
      motionFp: fps.motion,
      lipsyncFp: fps.lipsync,
      assembleFp: fps.mix,
      assembleFileName: 'famixa-SH09-01.mp4',
    },
  };
}

ok(LIPSYNC_MOTION_AUTHORITY_ID === 'FAMIXA_LIPSYNC_LATE_RESULT_RECONCILIATION_V1', 'id');

const shot = shotOf();
const seed = stateOf(baseRun({ takeUrl: 'https://take/55.mp4', runwayAttempts: [] }));
const frozen = freezeMotionInput(seed, shot);
const boundRun = withBoundLip(
  baseRun({
    takeUrl: 'https://take/55.mp4',
    previewUrl: 'https://take/55.mp4',
    runwayAttempts: [attempt55(frozen)],
  }),
  stateOf(baseRun({ takeUrl: 'https://take/55.mp4', runwayAttempts: [attempt55(frozen)] })),
);
const frozenLip = boundRun.lipsyncFrozenInput;
const approved = { ...boundRun, videoApproved: true };
const lateRun: SeriesShotRun = {
  ...approved,
  takeUrl: 'https://take/56.mp4',
  previewUrl: 'https://take/56.mp4',
  turboTaskId: 'veo_take56',
  acceptedTake: { n: 56, url: 'https://take/56.mp4', kfHash: HASH, acceptedAt: '2026-09-14T01:00:00.000Z' },
  runwayAttempts: [attempt55(frozen), attemptLate56()],
};
const before = stateOf(approved);
const after = stateOf(lateRun);
const fpsBefore = computeInputFingerprints(before, shot);
const fpsAfter = computeInputFingerprints(after, shot);
const execAfter = deriveShotExecutionState({ state: after, shot, ttsFiles: tts });

ok(lipsyncBoundMotionStillAuthority(lateRun, SHOT_ID), 'T1 bound 55 still authority');
ok(execAfter.lastSuccessArtifacts.lipsync?.validity === 'CURRENT', 'T1 LipSync remains CURRENT');
ok(fpsAfter.lipsync === fpsBefore.lipsync, 'T1 lipsync fps unchanged');

ok(execAfter.lastSuccessArtifacts.mix?.validity === 'CURRENT', 'T2 Mix remains CURRENT');
ok(fpsAfter.mix === fpsBefore.mix, 'T2 mix fps unchanged');

ok(performanceDurationOf(after, shot) === 5, 'T3 duration authority stays 5');
ok(lipsyncBoundDurationSec(lateRun, SHOT_ID) === 5, 'T3 bound duration 5');
ok(performanceDurationOf(after, shot) !== 6, 'T3 late 6s is not the clock');

ok(
  JSON.stringify(lateRun.lipsyncFrozenInput) === JSON.stringify(frozenLip),
  'T4 lipsyncFrozenInput unchanged',
);

ok(lateRun.videoApproved === true, 'T5 videoApproved unchanged');
ok(lateRun.acceptedTake?.n === 56 && lateRun.acceptedTake?.url === 'https://take/56.mp4', 'T6 acceptedTake unchanged');

const patched = motionCallbackRunPatch({
  run: approved,
  decision: { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'SUPERSEDED' },
  outputUrl: 'https://take/56.mp4',
  taskId: 'veo_take56',
});
ok(!('videoApproved' in patched), 'T5 late patch does not write videoApproved');
ok(!('acceptedTake' in patched), 'T6 late patch does not write acceptedTake');
ok(!('lipsyncFrozenInput' in patched), 'T4 late patch does not write lipsyncFrozenInput');

const prevStamp = applyProductionStamps(before, after);
ok(prevStamp.runs[SHOT_ID]?.shotProduction?.motionFp === approved.shotProduction?.motionFp, 'late takeUrl does not restamp motionFp');

const snap = buildShotProductionSnapshot({ state: after, shot, ttsFiles: tts, artifactPresent: true });
const cmd = nextShotProductionCommand(snap);
const desk = directorPictureVideoSurface({
  snap,
  cmd,
  hasLiveStill: true,
  kfApproved: true,
});
ok(cmd.type !== 'CONFIRM_LIPSYNC', 'T7 command is not CONFIRM_LIPSYNC');
ok(desk.primary?.label !== DIRECTOR_LIPSYNC_LABEL, 'T7 Khớp môi is not primary');
ok(snap.lipSyncReady === true, 'T7 lipSyncReady');

const changed: SeriesShotRun = {
  ...lateRun,
  runwayAttempts: [
    attempt55(frozen),
    attemptLate56(),
    {
      ...attempt55(frozen),
      n: 57,
      duration: 8,
      resultClass: 'CURRENT',
      taskId: 'veo_take57',
      outputUrl: 'https://take/57.mp4',
      attemptId: motionAttemptId(SHOT_ID, 57),
    },
  ],
};
const staleState = stateOf(changed);
const fpsStale = computeInputFingerprints(staleState, shot);
const execStale = deriveShotExecutionState({ state: staleState, shot, ttsFiles: tts });
ok(!lipsyncBoundMotionStillAuthority(changed, SHOT_ID), 'T8 new current take releases bind');
ok(fpsStale.lipsync !== fpsAfter.lipsync, 'T8 lipsync fps moves');
ok(execStale.lastSuccessArtifacts.lipsync?.validity === 'STALE', 'T8 LipSync STALE when source changes');

const root = dirname(fileURLToPath(import.meta.url));
const src = [
  'famixa-lipsync-motion-authority.ts',
  'famixa-shot-production-timing.ts',
  'ContentFamixaShotProduction/ShotProductionStamp.ts',
]
  .map((p) => readFileSync(join(root, p), 'utf8'))
  .join('\n');
ok(!/startContentSeries(Turbo|Lipsync)/.test(src), 'T9 no start turbo/lipsync');
ok(!/fal\.ai\/queue/.test(src), 'T9 no Fal queue URL');
ok(!/generativelanguage\.googleapis/.test(src), 'T9 no Veo URL');
ok(!/\bfetch\s*\(/.test(src), 'T9 no fetch');

if (fail.length) {
  console.error(`FAMIXA_LIPSYNC_LATE_RESULT_RECONCILIATION_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_LIPSYNC_LATE_RESULT_RECONCILIATION_V1 T1–T9 PASS');
