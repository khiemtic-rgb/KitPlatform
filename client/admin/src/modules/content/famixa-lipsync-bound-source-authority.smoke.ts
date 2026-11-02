/** FAMIXA_SH01_LIPSYNC_BOUND_SOURCE_AUTHORITY_V1 — T1–T18. 0 providers. 0 assemble. 0 graph writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { dataUriHash } from './content-famixa-runway-pipe';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import {
  LIPSYNC_BOUND_SOURCE_AUTHORITY_ID,
  boundMotionClipsSpeech,
  lipsyncBoundArtifactId,
  lipsyncBoundMotionStillAuthority,
  resolveCurrentProductionVideoSource,
  speechCoveringMotionAttempt,
} from './famixa-lipsync-motion-authority';
import { lastSuccessTakeOf } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  directorProductionCaption,
  resolveShotPreviewKind,
} from './ContentFamixaShotProduction/ShotProductionPreview';
import {
  parseStoredFinalArtifact,
  playableCurrentFinalBlob,
  proveFinalArtifact,
  type FinalArtifactIdentity,
} from './ContentFamixaShotProduction/ShotProductionFinalStore';
import {
  freezeLipSyncInput,
  freezeMixInput,
  freezeMotionInput,
  motionAttemptId,
  motionCallbackRunPatch,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const BUILD = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
const SHOT_ID = 'EP01-SC01-SH01';
const MIX = 'f7cc4097';
const MOTION = 'mot:EP01-SC01-SH01:55';
const LIP = 'lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f';
const LIP_URL = 'https://v3b.fal.media/files/b/bound/lip.mp4';
const TAKE_55 = 'https://take/55.mp4';
const TAKE_56 = 'https://take/56.mp4';
const TAKE_57 = 'https://take/57.mp4';
const KF = 'data:image/jpeg;base64,PIXELBOUNDSRC55';
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
    lipsyncUrl: LIP_URL,
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
      title: 'BOUND',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
  };
}

function attempt55(frozen: ReturnType<typeof freezeMotionInput>) {
  return {
    n: 55,
    at: '2026-09-14T00:00:00.000Z',
    status: 'SUCCEEDED' as const,
    resultClass: 'CURRENT' as const,
    duration: 5,
    taskId: 'veo_take55',
    outputUrl: TAKE_55,
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
    outputUrl: TAKE_56,
    attemptId: motionAttemptId(SHOT_ID, 56),
    promptHash: 'late',
    kf: { hash: HASH },
    source: { hash: HASH },
  };
}

const shot = shotOf();
const seed = stateOf(baseRun({ takeUrl: TAKE_55, runwayAttempts: [] }));
const frozen = freezeMotionInput(seed, shot);
const fps = computeInputFingerprints(
  stateOf(baseRun({ takeUrl: TAKE_55, runwayAttempts: [attempt55(frozen)] })),
  shot,
);
const boundRun = {
  ...baseRun({
    takeUrl: TAKE_55,
    previewUrl: TAKE_55,
    runwayAttempts: [attempt55(frozen)],
    lipsyncFrozenInput: freezeLipSyncInput(stateOf(baseRun({ takeUrl: TAKE_55, runwayAttempts: [attempt55(frozen)] })), shot, {
      motionArtifactId: MOTION,
    }),
    mixFrozenInput: freezeMixInput(stateOf(baseRun({ takeUrl: TAKE_55, runwayAttempts: [attempt55(frozen)] })), shot, {
      videoArtifactId: LIP_URL,
    }),
    shotProduction: {
      voiceFp: fps.voice,
      kfFp: fps.keyframe,
      motionFp: fps.motion,
      lipsyncFp: fps.lipsync,
      assembleFp: MIX,
    },
  }),
};
const lateRun: SeriesShotRun = {
  ...boundRun,
  takeUrl: TAKE_56,
  previewUrl: TAKE_56,
  turboTaskId: 'veo_take56',
  acceptedTake: { n: 56, url: TAKE_56, kfHash: HASH, acceptedAt: '2026-09-14T01:00:00.000Z' },
  runwayAttempts: [attempt55(frozen), attemptLate56()],
};

ok(LIPSYNC_BOUND_SOURCE_AUTHORITY_ID === 'FAMIXA_SH01_LIPSYNC_BOUND_SOURCE_AUTHORITY_V1', 'id');

const t1 = resolveCurrentProductionVideoSource({ run: boundRun, shotId: SHOT_ID });
ok(t1.usesBoundMotion && t1.motionN === 55 && t1.motionUrl === TAKE_55, 'T1 LipSync CURRENT → bound motion 55');
ok(t1.motionArtifactId === MOTION && t1.motionArtifactId === lipsyncBoundArtifactId(boundRun, SHOT_ID), 'T1 artifact 55');

const t2 = resolveCurrentProductionVideoSource({
  run: lateRun,
  shotId: SHOT_ID,
  fallbackMotionUrl: lateRun.acceptedTake?.url,
  fallbackMotionN: lateRun.acceptedTake?.n,
});
ok(t2.usesBoundMotion && t2.motionN === 55 && t2.url === TAKE_55, 'T2 acceptedTake 56 does not steal source');
ok(t2.motionUrl !== TAKE_56 && t2.motionN !== 56, 'T2 source is not 56');

const last = lastSuccessTakeOf(lateRun.runwayAttempts);
ok(last?.n === 56 && last.url === TAKE_56, 'T3 lastSuccess is 56');
const t3 = resolveCurrentProductionVideoSource({
  run: lateRun,
  shotId: SHOT_ID,
  fallbackMotionUrl: last?.url,
  fallbackMotionN: last?.n,
});
ok(t3.motionN === 55 && t3.motionUrl === TAKE_55, 'T3 lastSuccess 56 does not steal source');

const preview56blocked = resolveShotPreviewKind({ takeUrl: t3.url, lipsyncUrl: undefined });
ok(preview56blocked.src === TAKE_55 && preview56blocked.src !== TAKE_56, 'T4 preview src is 55');
ok(t3.motionN !== 56 && !t3.url?.includes('/56'), 'T4 production source cannot resolve 56');

ok(
  directorProductionCaption({ kind: preview56blocked.kind, takeN: t3.motionN }) === 'Video · take 55',
  'T5 caption follows bound motion 55',
);
const withLip = resolveCurrentProductionVideoSource({
  run: lateRun,
  shotId: SHOT_ID,
  lipsyncPlayableUrl: LIP_URL,
  fallbackMotionUrl: TAKE_56,
});
const lipPrev = resolveShotPreviewKind({ lipsyncUrl: withLip.url, takeUrl: withLip.motionUrl });
ok(withLip.kind === 'lipsync' && lipPrev.kind === 'lipsync' && lipPrev.src === LIP_URL, 'T5/T6 preview LipSync when playable');
ok(directorProductionCaption({ kind: lipPrev.kind, takeN: withLip.motionN }) === 'Preview Lip-sync', 'T5 LipSync caption');

ok(withLip.url === LIP_URL && withLip.motionUrl === TAKE_55, 'T6 assemble/preview LipSync output when playable');

const noFal = resolveCurrentProductionVideoSource({
  run: lateRun,
  shotId: SHOT_ID,
  fallbackMotionUrl: TAKE_56,
});
ok(noFal.kind === 'bound_motion' && noFal.url === TAKE_55, 'T7 Fal unavailable → bound motion 55');

ok(noFal.url !== TAKE_56 && noFal.motionUrl !== lateRun.acceptedTake?.url, 'T8 no fallback to acceptedTake 56');

ok(!boundMotionClipsSpeech(lateRun, SHOT_ID, 4.24), 'T8b 4.24s speech still fits bound 5s fixture');
ok(boundMotionClipsSpeech(lateRun, SHOT_ID, 5.21), 'T8c spoken floor above bound 5s clips');
const cover56 = speechCoveringMotionAttempt(lateRun, SHOT_ID, 5.21);
ok(cover56?.n === 56 && cover56.outputUrl === TAKE_56, 'T8d covering take is 6s file 56');
const coverDesk = speechCoveringMotionAttempt(lateRun, SHOT_ID, 0);
ok(coverDesk?.n === 56, 'T8d desk still picks 6s take when floor stamp is 0');
const tSpeech = resolveCurrentProductionVideoSource({
  run: lateRun,
  shotId: SHOT_ID,
  lipsyncPlayableUrl: LIP_URL,
  fallbackMotionUrl: TAKE_55,
  speechFloorSec: 5.21,
});
ok(tSpeech.kind === 'lipsync' && tSpeech.url === LIP_URL && !tSpeech.usesSpeechCoveringTake, 'T8e current Fal 6s stays preview when speech covering exists');
const tCoverMute = resolveCurrentProductionVideoSource({
  run: lateRun,
  shotId: SHOT_ID,
  fallbackMotionUrl: TAKE_55,
  speechFloorSec: 5.21,
});
ok(tCoverMute.usesSpeechCoveringTake && tCoverMute.url === TAKE_56 && tCoverMute.motionN === 56, 'T8e mute covering take only when LipSync is not playable');
const stamped5: SeriesShotRun = {
  ...lateRun,
  runwayAttempts: [attempt55(frozen), { ...attemptLate56(), duration: 5 }],
};
ok(speechCoveringMotionAttempt(stamped5, SHOT_ID, 4.24)?.n === 56, 'T8f later take wins even when graph stamps 5s');
const lateStatus: SeriesShotRun = {
  ...lateRun,
  runwayAttempts: [attempt55(frozen), { ...attemptLate56(), status: 'LATE_RESULT', duration: 0 }],
};
ok(speechCoveringMotionAttempt(lateStatus, SHOT_ID, 0)?.n === 56, 'T8g LATE_RESULT status still covers the 4s bind');

const staleRun: SeriesShotRun = {
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
      outputUrl: TAKE_57,
      attemptId: motionAttemptId(SHOT_ID, 57),
    },
  ],
};
ok(!lipsyncBoundMotionStillAuthority(staleRun, SHOT_ID), 'T9 LipSync bind released');
const t9 = resolveCurrentProductionVideoSource({
  run: staleRun,
  shotId: SHOT_ID,
  fallbackMotionUrl: TAKE_57,
  fallbackMotionN: 57,
});
ok(!t9.usesBoundMotion && t9.url === TAKE_57 && t9.motionN === 57, 'T9 STALE uses existing motion resolution');
const t9lip = resolveCurrentProductionVideoSource({
  run: staleRun,
  shotId: SHOT_ID,
  lipsyncPlayableUrl: LIP_URL,
  fallbackMotionUrl: TAKE_57,
  fallbackMotionN: 57,
});
ok(t9lip.kind === 'motion' && t9lip.url === TAKE_57 && t9lip.url !== LIP_URL, 'T9 released bind ignores historical LipSync URL');

const current: FinalArtifactIdentity = {
  buildId: BUILD,
  shotId: SHOT_ID,
  assembleFp: MIX,
  motionArtifactId: MOTION,
  lipsyncTaskId: LIP,
};
const leftover = parseStoredFinalArtifact(new Blob([new Uint8Array(120)], { type: 'video/mp4' }));
ok(leftover.kind === 'legacy', 'T10 leftover is legacy');
ok(!playableCurrentFinalBlob(leftover, current), 'T10 raw Blob is not playable CURRENT Final');
ok(proveFinalArtifact(leftover, current).validity === 'LEGACY_UNVERIFIED', 'T10 proof LEGACY_UNVERIFIED');

const provenBlob = new Blob([new Uint8Array(120)], { type: 'video/mp4' });
const proven = playableCurrentFinalBlob(
  {
    kind: 'record',
    record: {
      blob: provenBlob,
      meta: {
        buildId: BUILD,
        shotId: SHOT_ID,
        assembleFp: MIX,
        motionArtifactId: MOTION,
        lipsyncTaskId: LIP,
        size: 120,
        at: '2026-09-15T08:00:00.000Z',
      },
    },
  },
  current,
);
ok(Boolean(proven) && proveFinalArtifact(
  {
    kind: 'record',
    record: {
      blob: provenBlob,
      meta: {
        buildId: BUILD,
        shotId: SHOT_ID,
        assembleFp: MIX,
        motionArtifactId: MOTION,
        lipsyncTaskId: LIP,
        size: 120,
        at: '2026-09-15T08:00:00.000Z',
      },
    },
  },
  current,
).validity === 'CURRENT', 'T11 matching assembleFp is CURRENT');

ok(lateRun.runwayAttempts?.some((row) => row.n === 56 && row.resultClass === 'LATE_RESULT'), 'T12 late Take 56 unchanged');
ok(lateRun.acceptedTake?.n === 56 && lateRun.acceptedTake.url === TAKE_56, 'T13 acceptedTake unchanged');
ok(lateRun.videoApproved === true, 'T14 videoApproved unchanged');
const patched = motionCallbackRunPatch({
  run: boundRun,
  decision: { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'SUPERSEDED' },
  outputUrl: TAKE_56,
  taskId: 'veo_take56',
});
ok(!('acceptedTake' in patched) && !('videoApproved' in patched) && !('lipsyncFrozenInput' in patched), 'T12–T14 late patch does not write take/approved/bind');

const root = dirname(fileURLToPath(import.meta.url));
const auth = readFileSync(join(root, 'famixa-lipsync-motion-authority.ts'), 'utf8');
const ws = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionWorkspace.tsx'), 'utf8');
const preview = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionPreview.tsx'), 'utf8');
const store = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionFinalStore.ts'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const assemble = tab.slice(tab.indexOf('const assembleOneShot'), tab.indexOf('const approveShotPicture'));
const watch = tab.slice(tab.indexOf('const watchShot'), tab.indexOf('const approveShotVideo'));

ok(ws.includes('resolveCurrentProductionVideoSource') && assemble.includes('resolveCurrentProductionVideoSource'), 'wired helper');
ok(ws.includes('directorProductionCaption') && preview.includes('directorProductionCaption'), 'T5 caption helper wired');
ok(ws.includes('takeN: productionSource.motionN'), 'T5 caption uses productionSource.motionN');
ok(!/directorProductionCaption\(\{[\s\S]*?matchingTake/.test(ws), 'T5 caption does not use matchingTake');
ok(watch.includes('playableCurrentFinalBlob') && !watch.includes('loadFinalBlob(shotId)'), 'T10 watch proves Final');
ok(!/startContentSeries(Turbo|Lipsync)/.test(auth + store), 'T15 no start turbo/lipsync in helper/store');
ok(!/fal\.ai\/queue/.test(auth + ws + store) && !assemble.includes('startContentSeriesLipsync'), 'T16 no Fal job');
ok(!auth.includes('assembleContentSeriesCut') && !auth.includes('new Blob'), 'T17 helper creates no media');
ok(!auth.includes('persistState') && !auth.includes('patchRun'), 'T18 helper does not mutate graph');

if (fail.length) {
  console.error(`FAMIXA_SH01_LIPSYNC_BOUND_SOURCE_AUTHORITY_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_LIPSYNC_BOUND_SOURCE_AUTHORITY_V1 T1–T18 PASS');
