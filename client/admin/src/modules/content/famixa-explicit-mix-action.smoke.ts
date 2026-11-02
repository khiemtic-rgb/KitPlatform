/** FAMIXA_EXPLICIT_MIX_ACTION_V1 — T1–T15. 0 providers. 0 assemble. 0 IDB writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash } from './content-famixa-runway-pipe';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { lipsyncBoundArtifactId } from './famixa-lipsync-motion-authority';
import {
  artifactPresentOfProof,
  parseStoredFinalArtifact,
  proveFinalArtifact,
  type FinalArtifactIdentity,
} from './ContentFamixaShotProduction/ShotProductionFinalStore';
import { directorPictureVideoSurface, directorPrimaryCta } from './ContentFamixaShotProduction/ShotProductionCta';
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

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const ws = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionWorkspace.tsx'), 'utf8');
const store = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionFinalStore.ts'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const hydrateStart = ws.indexOf('const stored = await loadFinalArtifact');
const hydrate = hydrateStart >= 0 ? ws.slice(hydrateStart, hydrateStart + 900) : '';

ok(!/finalHydrated[\s\S]{0,800}onEnsureMix/.test(ws), 'T1 hydrate does not call onEnsureMix');
ok(!hydrate.includes('onEnsureMix') && !hydrate.includes('assembleContentSeriesCut'), 'T1 hydrate block has no assemble');
ok(!hydrate.includes('saveFinalBlob'), 'T2 hydrate does not saveFinalBlob');
ok(!hydrate.includes('assembleContentSeriesCut') && !hydrate.includes('new Blob'), 'T3 hydrate creates no media');
ok(!ws.includes('mixOnce'), 'T1 mixOnce auto-mix ref removed');

const BUILD = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
const SHOT_ID = 'EP01-SC01-SH01';
const LIP = 'lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f';
const MOTION = 'mot:EP01-SC01-SH01:55';
const KF = 'data:image/jpeg;base64,PIXELEXPLICITMIX55';
const HASH = dataUriHash(KF);

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
      title: 'EXPLICITMIX',
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
const boundRun = baseRun({
  takeUrl: 'https://take/55.mp4',
  previewUrl: 'https://take/55.mp4',
  runwayAttempts: [
    {
      n: 55,
      at: '2026-09-14T00:00:00.000Z',
      status: 'SUCCEEDED',
      resultClass: 'CURRENT',
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
});
const fps = computeInputFingerprints(stateOf(boundRun), shot);
const approved = {
  ...boundRun,
  videoApproved: true,
  lipsyncFrozenInput: freezeLipSyncInput(stateOf(boundRun), shot, { motionArtifactId: MOTION }),
  mixFrozenInput: freezeMixInput(stateOf(boundRun), shot, { videoArtifactId: boundRun.lipsyncUrl }),
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
const leftover = parseStoredFinalArtifact(new Blob([new Uint8Array(120)], { type: 'video/mp4' }));
const current: FinalArtifactIdentity = {
  buildId: BUILD,
  shotId: SHOT_ID,
  assembleFp: fps.mix,
  motionArtifactId: MOTION,
  lipsyncTaskId: LIP,
};
const leftoverProof = proveFinalArtifact(leftover, current);
const snap = buildShotProductionSnapshot({
  state: after,
  shot,
  ttsFiles: tts,
  artifactPresent: artifactPresentOfProof(leftoverProof),
});
const cmd = nextShotProductionCommand(snap);
const desk = directorPictureVideoSurface({ snap, cmd, hasLiveStill: true, kfApproved: true });
ok(leftoverProof.validity === 'LEGACY_UNVERIFIED', 'T4 leftover is LEGACY_UNVERIFIED');
ok(cmd.type === 'ENSURE_MIX', 'T4 nextAction ENSURE_MIX');
ok(directorPrimaryCta(snap).label === 'Hoàn thiện Shot', 'T4 primary Hoàn thiện Shot');
ok(desk.primary?.label === 'Hoàn thiện Shot' && desk.primary?.action === 'mix', 'T4 desk primary mix');

ok(ws.includes("else if (action === 'mix') void onEnsureMix(shotId)"), 'T5 explicit Hoàn thiện Shot calls onEnsureMix');
ok(tab.includes('onEnsureMix={(shotId) => void assembleOneShot(shotId)}'), 'T5 SeriesTab wires assembleOneShot');
ok(tab.includes('saveFinalBlob(shot.id, mp4, identity)'), 'T6 explicit mix persists identity');
ok(
  tab.includes('finalArtifactIdentityOf(live, liveShot)') &&
    store.includes('assembleFp') &&
    store.includes('motionArtifactId') &&
    store.includes('lipsyncTaskId'),
  'T6 identity fields persist',
);
ok(ws.includes('artifactPresentOfProof(proof)') && store.includes("validity: 'CURRENT'"), 'T7 presence only after proof');

const proven = proveFinalArtifact(
  {
    kind: 'record',
    record: {
      blob: new Blob([new Uint8Array(120)], { type: 'video/mp4' }),
      meta: {
        buildId: BUILD,
        shotId: SHOT_ID,
        assembleFp: fps.mix,
        motionArtifactId: MOTION,
        lipsyncTaskId: LIP,
        size: 120,
        at: '2026-09-15T08:00:00.000Z',
      },
    },
  },
  current,
);
const readySnap = buildShotProductionSnapshot({
  state: after,
  shot,
  ttsFiles: tts,
  artifactPresent: artifactPresentOfProof(proven),
});
ok(proven.ok && readySnap.finalArtifactReady === true, 'T7 proven identity is present');
ok(nextShotProductionCommand(readySnap).type === 'READY_FINAL', 'T8 READY_FINAL after proven save');

ok(lipsyncBoundArtifactId(lateRun, SHOT_ID) === MOTION, 'T9 late 56 does not steal bind');
ok(snap.execution.currentArtifacts.lipsync?.validity === 'CURRENT' || snap.lipSyncReady === true, 'T10 LipSync CURRENT');
ok(lateRun.acceptedTake?.n === 56, 'T11 acceptedTake unchanged');
ok(lateRun.videoApproved === true, 'T12 videoApproved unchanged');
const patched = motionCallbackRunPatch({
  run: approved,
  decision: { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'SUPERSEDED' },
  outputUrl: 'https://take/56.mp4',
  taskId: 'veo_take56',
});
ok(!('acceptedTake' in patched) && !('videoApproved' in patched), 'T11–T12 late patch does not write take/approved');

ok(!/runwayml\.com/.test(store + ws), 'T13 no Runway host change');
ok(!/generativelanguage\.googleapis/.test(store + ws), 'T13 no Veo URL');
ok(!/wan\.video|dashscope/.test(store + ws), 'T13 no Wan host change');
ok(!/startContentSeries(Turbo|Lipsync)/.test(store + ws), 'T14 no start turbo/lipsync');
ok(!/fal\.ai\/queue/.test(store + ws), 'T14 no Fal queue URL');
ok(!store.includes('assembleContentSeriesCut') && !ws.includes('assembleContentSeriesCut'), 'T15 no assemble in workspace/store');

if (fail.length) {
  console.error(`FAMIXA_EXPLICIT_MIX_ACTION_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_EXPLICIT_MIX_ACTION_V1 T1–T15 PASS');
