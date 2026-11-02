/** FAMIXA_SH01_FINAL_ARTIFACT_READINESS_CTA_FIX_V1 — T1–T11. 0 providers. 0 graph writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash } from './content-famixa-runway-pipe';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import {
  lipsyncBoundMotionStillAuthority,
} from './famixa-lipsync-motion-authority';
import {
  DIRECTOR_LIPSYNC_LABEL,
  DIRECTOR_LIPSYNC_NEW_LABEL,
  deskCanOfferLipsync,
  deskExposeLipsyncCta,
  deskExposeLipsyncRemakeCta,
  deskLipsyncOutputCurrent,
  directorPictureVideoSurface,
  directorPrimaryCta,
} from './ContentFamixaShotProduction/ShotProductionCta';
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

const KF = 'data:image/jpeg;base64,PIXELREADYCTA55';
const HASH = dataUriHash(KF);
const SHOT_ID = 'EP01-SC01-SH01';
const LIP_URL = '/api/content/series/fal-video?taskId=lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f';

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
      title: 'READYCTA',
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
  return {
    ...run,
    takeUrl: 'https://take/55.mp4',
    previewUrl: 'https://take/55.mp4',
    lipsyncFrozenInput: freezeLipSyncInput(state, shot, { motionArtifactId: motionAttemptId(SHOT_ID, 55) }),
    mixFrozenInput: freezeMixInput(state, shot, { videoArtifactId: LIP_URL }),
    shotQa: {
      action: true,
      continuity: true,
      motion: true,
      voiceFace: true,
      lipsync: true,
      lipsyncQuality: true,
      finalAv: true,
      decision: 'ACCEPT',
      reviewedPlaybackId: 'assembly:famixa-SH01-01.mp4',
    },
    shotProduction: {
      voiceFp: fps.voice,
      kfFp: fps.keyframe,
      motionFp: fps.motion,
      lipsyncFp: fps.lipsync,
      assembleFp: fps.mix,
      assembleFileName: 'famixa-SH01-01.mp4',
    },
  };
}

function deskOf(snap: ReturnType<typeof buildShotProductionSnapshot>) {
  return directorPictureVideoSurface({
    snap,
    cmd: nextShotProductionCommand(snap),
    hasLiveStill: true,
    kfApproved: true,
  });
}

function lipsyncIsNextProductionAction(desk: ReturnType<typeof deskOf>) {
  return (
    (desk.primary?.action === 'lipsync' && desk.primary?.label === DIRECTOR_LIPSYNC_LABEL) ||
    (desk.secondary?.action === 'lipsync' && desk.secondary?.label === DIRECTOR_LIPSYNC_LABEL)
  );
}

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
const approved = { ...boundRun, videoApproved: true };
const lateRun: SeriesShotRun = {
  ...approved,
  takeUrl: 'https://take/56.mp4',
  previewUrl: 'https://take/56.mp4',
  turboTaskId: 'veo_take56',
  acceptedTake: { n: 56, url: 'https://take/56.mp4', kfHash: HASH, acceptedAt: '2026-09-14T01:00:00.000Z' },
  runwayAttempts: [attempt55(frozen), attemptLate56()],
};
const after = stateOf(lateRun);

const proven = buildShotProductionSnapshot({ state: after, shot, ttsFiles: tts, artifactPresent: true });
ok(proven.mixReady && Boolean(proven.stamp?.assembleFp), 'T1 mix + assembleFp');
ok(proven.finalArtifactReady === true, 'T1 finalArtifactReady=true when artifact proven');
ok(nextShotProductionCommand(proven).type === 'READY_FINAL', 'T1 READY_FINAL');
ok(directorPrimaryCta(proven).label === 'Xem video', 'T1 primary Xem video');

const missing = buildShotProductionSnapshot({ state: after, shot, ttsFiles: tts, artifactPresent: false });
ok(missing.mixReady && Boolean(missing.stamp?.assembleFp), 'T2 mix + assembleFp still true');
ok(missing.finalArtifactReady === false, 'T2 finalArtifactReady=false when artifact missing');
ok(nextShotProductionCommand(missing).type === 'ENSURE_MIX', 'T2 ENSURE_MIX');
ok(directorPrimaryCta(missing).label === 'Hoàn thiện Shot', 'T2 primary Hoàn thiện Shot');

const unknown = buildShotProductionSnapshot({ state: after, shot, ttsFiles: tts });
ok(unknown.mixReady && Boolean(unknown.stamp?.assembleFp), 'T3 mix + assembleFp still true');
ok(unknown.finalArtifactReady === false, 'T3 undefined is not ready');
ok(nextShotProductionCommand(unknown).type === 'ENSURE_MIX', 'T3 ENSURE_MIX');
ok(directorPrimaryCta(unknown).label === 'Hoàn thiện Shot', 'T3 primary Hoàn thiện Shot');
ok(nextShotProductionCommand(unknown).type !== 'READY_FINAL', 'T4 UNKNOWN is not READY_FINAL');
ok(unknown.finalArtifactReady !== true, 'T4 UNKNOWN finalArtifactReady is not true');

ok(unknown.execution.currentArtifacts.lipsync?.validity === 'CURRENT', 'T5 LipSync CURRENT');
ok(deskLipsyncOutputCurrent(unknown) === true, 'T5 playable LipSync current');
ok(deskCanOfferLipsync(unknown) === true, 'T5 capability Khớp môi remains');
ok(deskExposeLipsyncCta(unknown) === false, 'T5 CURRENT hides Director Khớp môi CTA');
ok(deskExposeLipsyncRemakeCta(unknown) === true, 'T5 CURRENT still offers Khớp môi mới remake');
ok(nextShotProductionCommand(unknown).type !== 'CONFIRM_LIPSYNC', 'T5 command is not CONFIRM_LIPSYNC');
ok(!lipsyncIsNextProductionAction(deskOf(unknown)), 'T5 Khớp môi is not next production action');
ok(deskOf(unknown).primary?.label === 'Hoàn thiện Shot', 'T5 primary remains Hoàn thiện Shot');
ok(deskOf(unknown).tertiary?.label === DIRECTOR_LIPSYNC_NEW_LABEL, 'T5 ENSURE_MIX remake is tertiary');
ok(!lipsyncIsNextProductionAction(deskOf(proven)), 'T5 proven READY_FINAL Khớp môi is not next step');
ok(deskOf(proven).primary?.label === 'Xem video', 'T5 proven primary stays Xem video');
ok(deskOf(proven).secondary?.label === DIRECTOR_LIPSYNC_NEW_LABEL, 'T5 proven READY_FINAL shows Khớp môi mới');

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
const staleSnap = buildShotProductionSnapshot({ state: staleState, shot, ttsFiles: tts, artifactPresent: true });
ok(staleSnap.lipSyncStale === true, 'T6 LipSync STALE');
ok(staleSnap.execution.lastSuccessArtifacts.lipsync?.validity === 'STALE', 'T6 lastSuccess LipSync STALE');
ok(deskExposeLipsyncCta(staleSnap) === true, 'T6 STALE still exposes Khớp môi');
ok(nextShotProductionCommand(staleSnap).type === 'CONFIRM_LIPSYNC', 'T6 command CONFIRM_LIPSYNC');
ok(deskOf(staleSnap).primary?.label === DIRECTOR_LIPSYNC_LABEL, 'T6 primary Khớp môi');

const execAfter = deriveShotExecutionState({ state: after, shot, ttsFiles: tts });
ok(lipsyncBoundMotionStillAuthority(lateRun, SHOT_ID), 'T7 bound 55 still authority');
ok(execAfter.lastSuccessArtifacts.lipsync?.validity === 'CURRENT', 'T7 LipSync remains CURRENT');
ok(execAfter.lastSuccessArtifacts.mix?.validity === 'CURRENT', 'T7 Mix remains CURRENT');
const patched = motionCallbackRunPatch({
  run: approved,
  decision: { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'SUPERSEDED' },
  outputUrl: 'https://take/56.mp4',
  taskId: 'veo_take56',
});
ok(!('lipsyncFrozenInput' in patched), 'T7 late patch does not write lipsyncFrozenInput');
const prevStamp = applyProductionStamps(stateOf(approved), after);
ok(prevStamp.runs[SHOT_ID]?.shotProduction?.motionFp === approved.shotProduction?.motionFp, 'T7 late takeUrl does not restamp motionFp');

ok(lateRun.acceptedTake?.n === 56 && lateRun.acceptedTake?.url === 'https://take/56.mp4', 'T8 acceptedTake unchanged');
ok(!('acceptedTake' in patched), 'T8 late patch does not write acceptedTake');
ok(unknown.acceptedTake?.n === 56, 'T8 snapshot keeps acceptedTake 56');

ok(lateRun.videoApproved === true, 'T9 videoApproved unchanged');
ok(!('videoApproved' in patched), 'T9 late patch does not write videoApproved');
ok(unknown.videoApproved === true, 'T9 snapshot keeps videoApproved');

const root = dirname(fileURLToPath(import.meta.url));
const src = [
  'ContentFamixaShotProduction/ShotProductionState.ts',
  'ContentFamixaShotProduction/ShotProductionCta.ts',
]
  .map((p) => readFileSync(join(root, p), 'utf8'))
  .join('\n');
ok(!/runwayml\.com/.test(src), 'T10 no Runway host change');
ok(!/generativelanguage\.googleapis/.test(src), 'T10 no Veo URL');
ok(!/wan\.video|dashscope/.test(src), 'T10 no Wan host change');
ok(src.includes('artifactPresent === true'), 'T10 readiness requires proven presence');
ok(src.includes('deskExposeLipsyncCta'), 'T10 LipSync CURRENT hides Director CTA');
ok(!/startContentSeries(Turbo|Lipsync)/.test(src), 'T11 no start turbo/lipsync');
ok(!/fal\.ai\/queue/.test(src), 'T11 no Fal queue URL');
ok(!/\bfetch\s*\(/.test(src), 'T11 no fetch');

if (fail.length) {
  console.error(`FAMIXA_SH01_FINAL_ARTIFACT_READINESS_CTA_FIX_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_FINAL_ARTIFACT_READINESS_CTA_FIX_V1 T1–T11 PASS');
