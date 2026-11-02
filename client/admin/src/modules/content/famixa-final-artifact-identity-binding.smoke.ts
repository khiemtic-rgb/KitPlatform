/** FAMIXA_FINAL_ARTIFACT_IDENTITY_BINDING_V1 — T1–T15. 0 providers. 0 IDB writes. 0 assemble. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash } from './content-famixa-runway-pipe';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { lipsyncBoundArtifactId } from './famixa-lipsync-motion-authority';
import {
  FINAL_ARTIFACT_IDENTITY_ID,
  artifactPresentOfProof,
  finalArtifactIdentityOf,
  parseStoredFinalArtifact,
  proveFinalArtifact,
  type FinalArtifactIdentity,
  type FinalArtifactRecord,
} from './ContentFamixaShotProduction/ShotProductionFinalStore';
import {
  freezeLipSyncInput,
  freezeMixInput,
  freezeMotionInput,
  motionAttemptId,
  motionCallbackRunPatch,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { DIRECTOR_LIPSYNC_LABEL, directorPictureVideoSurface, directorPrimaryCta } from './ContentFamixaShotProduction/ShotProductionCta';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const BUILD = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
const SHOT_ID = 'EP01-SC01-SH01';
const MIX = 'f7cc4097';
const MOTION = 'mot:EP01-SC01-SH01:55';
const LIP = 'lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f';
const KF = 'data:image/jpeg;base64,PIXELFINALID55';
const HASH = dataUriHash(KF);

const current: FinalArtifactIdentity = {
  buildId: BUILD,
  shotId: SHOT_ID,
  assembleFp: MIX,
  motionArtifactId: MOTION,
  lipsyncTaskId: LIP,
};

const blob = new Blob([new Uint8Array(120)], { type: 'video/mp4' });

function record(over: Partial<FinalArtifactRecord['meta']> = {}): FinalArtifactRecord {
  return {
    blob,
    meta: {
      buildId: BUILD,
      shotId: SHOT_ID,
      assembleFp: MIX,
      motionArtifactId: MOTION,
      lipsyncTaskId: LIP,
      size: blob.size,
      at: '2026-09-15T08:00:00.000Z',
      ...over,
    },
  };
}

ok(FINAL_ARTIFACT_IDENTITY_ID === 'FAMIXA_FINAL_ARTIFACT_IDENTITY_BINDING_V1', 'id');

const t1 = proveFinalArtifact({ kind: 'record', record: record() }, current);
ok(t1.ok && t1.validity === 'CURRENT' && artifactPresentOfProof(t1) === true, 'T1 matching assembleFp is ready');

const t2 = proveFinalArtifact({ kind: 'record', record: record({ assembleFp: 'deadbeef' }) }, current);
ok(!t2.ok && t2.validity === 'MISMATCH' && artifactPresentOfProof(t2) === false, 'T2 assembleFp mismatch is not ready');

const t3 = proveFinalArtifact(parseStoredFinalArtifact(blob), current);
ok(t3.validity === 'LEGACY_UNVERIFIED' && artifactPresentOfProof(t3) === false, 'T3 raw Blob is LEGACY_UNVERIFIED');

const t4 = proveFinalArtifact({ kind: 'record', record: record({ buildId: 'other-build' }) }, current);
ok(!t4.ok && t4.validity === 'MISMATCH', 'T4 other buildId is not ready');

const t5 = proveFinalArtifact({ kind: 'record', record: record({ shotId: 'EP01-SC01-SH02' }) }, current);
ok(!t5.ok && t5.validity === 'MISMATCH', 'T5 other shotId is not ready');

const t6 = proveFinalArtifact({ kind: 'record', record: record({ motionArtifactId: 'mot:EP01-SC01-SH01:56' }) }, current);
ok(!t6.ok && t6.validity === 'MISMATCH', 'T6 other motionArtifactId is not ready');

const t7 = proveFinalArtifact(
  { kind: 'record', record: record({ lipsyncTaskId: 'lipsync_v3_other' }) },
  current,
);
ok(!t7.ok && t7.validity === 'MISMATCH', 'T7 other lipsyncTaskId is not ready');

const t8a = proveFinalArtifact(parseStoredFinalArtifact({ blob, meta: { shotId: SHOT_ID } }), current);
ok(t8a.validity === 'LEGACY_UNVERIFIED' && artifactPresentOfProof(t8a) === false, 'T8 missing metadata is not ready');
const t8b = proveFinalArtifact({ kind: 'incomplete', blob }, current);
ok(!t8b.ok && t8b.validity === 'LEGACY_UNVERIFIED', 'T8 incomplete wrapper is not ready');

const t9 = proveFinalArtifact({ kind: 'record', record: record() }, current);
ok(t9.ok && t9.validity === 'CURRENT' && blob.size >= 80, 'T9 current metadata + readable blob is ready');

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
    lipsyncTaskId: LIP,
    lipsyncStatus: 'SUCCEEDED',
    lipsynced: true,
    lipsyncUrl: `/api/content/series/fal-video?taskId=${LIP}`,
    finalSource: 'FAL',
    shotQa: { action: true, continuity: true, voiceFace: true, lipsync: true, lipsyncQuality: true, finalAv: true },
    ...over,
  };
}

function stateOf(run: SeriesShotRun): SeriesPilotState {
  const shot = shotOf();
  return {
    buildId: BUILD,
    roles: [{ id: 'role-1', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'voice-1' }],
    runs: { [shot.id]: run },
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-1' }],
    lines: [{ id: 'line-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con.', voiceId: 'voice-1' }],
    voiceAssets: { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'F',
      episode: 'EP01',
      title: 'FINALID',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
  };
}

const tts = { 'line-1': { url: 'blob:voice', fileName: 'a.mp3' } };
const shot = shotOf();
const seed = stateOf(baseRun({ takeUrl: 'https://take/55.mp4', runwayAttempts: [] }));
const frozen = freezeMotionInput(seed, shot);
const bound = {
  ...baseRun({
    takeUrl: 'https://take/55.mp4',
    previewUrl: 'https://take/55.mp4',
    runwayAttempts: [
      {
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
      },
    ],
  }),
};
const fps = computeInputFingerprints(stateOf(bound), shot);
const approved = {
  ...bound,
  videoApproved: true,
  lipsyncFrozenInput: freezeLipSyncInput(stateOf(bound), shot, { motionArtifactId: MOTION }),
  mixFrozenInput: freezeMixInput(stateOf(bound), shot, { videoArtifactId: bound.lipsyncUrl }),
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
const lateRun: SeriesShotRun = {
  ...approved,
  takeUrl: 'https://take/56.mp4',
  previewUrl: 'https://take/56.mp4',
  turboTaskId: 'veo_take56',
  acceptedTake: { n: 56, url: 'https://take/56.mp4', kfHash: HASH, acceptedAt: '2026-09-14T01:00:00.000Z' },
  runwayAttempts: [
    approved.runwayAttempts![0]!,
    {
      n: 56,
      at: '2026-09-14T01:00:00.000Z',
      status: 'SUCCEEDED',
      resultClass: 'LATE_RESULT',
      duration: 6,
      taskId: 'veo_take56',
      outputUrl: 'https://take/56.mp4',
      attemptId: motionAttemptId(SHOT_ID, 56),
      promptHash: 'late',
      kf: { hash: HASH },
      source: { hash: HASH },
    },
  ],
};
const after = stateOf(lateRun);
const identityAfter = finalArtifactIdentityOf(after, shot);
ok(lipsyncBoundArtifactId(lateRun, SHOT_ID) === MOTION, 'T10 bound motion stays 55');
ok(identityAfter.motionArtifactId === MOTION, 'T10 Final identity motion stays 55');
ok(identityAfter.motionArtifactId !== 'mot:EP01-SC01-SH01:56', 'T10 late 56 is not Final identity');
ok(lateRun.acceptedTake?.n === 56, 'T11 acceptedTake unchanged');
ok(lateRun.videoApproved === true, 'T12 videoApproved unchanged');
const patched = motionCallbackRunPatch({
  run: approved,
  decision: { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'SUPERSEDED' },
  outputUrl: 'https://take/56.mp4',
  taskId: 'veo_take56',
});
ok(!('acceptedTake' in patched), 'T11 late patch does not write acceptedTake');
ok(!('videoApproved' in patched), 'T12 late patch does not write videoApproved');

const legacySnap = buildShotProductionSnapshot({
  state: after,
  shot,
  ttsFiles: tts,
  artifactPresent: artifactPresentOfProof(t3),
});
ok(legacySnap.finalArtifactReady === false, 'T3 snapshot leftover is not final ready');
ok(nextShotProductionCommand(legacySnap).type === 'ENSURE_MIX', 'T3 leftover command ENSURE_MIX');
ok(directorPrimaryCta(legacySnap).label === 'Hoàn thiện Shot', 'T3 leftover primary Hoàn thiện Shot');
ok(
  directorPictureVideoSurface({
    snap: legacySnap,
    cmd: nextShotProductionCommand(legacySnap),
    hasLiveStill: true,
    kfApproved: true,
  }).primary?.label !== DIRECTOR_LIPSYNC_LABEL,
  'T3 leftover does not make Khớp môi primary',
);

const provenSnap = buildShotProductionSnapshot({
  state: after,
  shot,
  ttsFiles: tts,
  artifactPresent: artifactPresentOfProof(t1),
});
ok(provenSnap.finalArtifactReady === true, 'T1 snapshot proven is ready');
ok(nextShotProductionCommand(provenSnap).type === 'READY_FINAL', 'T1 proven READY_FINAL');

const root = dirname(fileURLToPath(import.meta.url));
const store = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionFinalStore.ts'), 'utf8');
const ws = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionWorkspace.tsx'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
ok(!/runwayml\.com/.test(store), 'T13 no Runway host change');
ok(!/generativelanguage\.googleapis/.test(store), 'T13 no Veo URL');
ok(!/wan\.video|dashscope/.test(store), 'T13 no Wan host change');
ok(ws.includes('proveFinalArtifact') && store.includes('finalArtifactIdentityOf'), 'T13 identity proof wired');
ok(!/startContentSeries(Turbo|Lipsync)/.test(store), 'T14 no start turbo/lipsync');
ok(!/fal\.ai\/queue/.test(store), 'T14 no Fal queue URL');
ok(!/assembleContentSeriesCut/.test(store), 'T15 store does not assemble');
ok(!store.includes('indexedDB.deleteDatabase') && !ws.includes('indexedDB.deleteDatabase'), 'T15 no IDB delete');
ok(tab.includes('saveFinalBlob(shot.id, mp4, identity)'), 'T15 next assemble saves identity');

if (fail.length) {
  console.error(`FAMIXA_FINAL_ARTIFACT_IDENTITY_BINDING_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_FINAL_ARTIFACT_IDENTITY_BINDING_V1 T1–T15 PASS');
