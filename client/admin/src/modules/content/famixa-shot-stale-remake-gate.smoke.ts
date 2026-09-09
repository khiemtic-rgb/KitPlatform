/** FAMIXA_SH01_STALE_REMAKE_GATE_V1 — Studio CTA remake wiring only. No providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { readyV2VideoShots } from './content-famixa-prod-v2';
import { directorPrimaryCta, studioLipsyncSendOpts, studioMotionSendOpts } from './ContentFamixaShotProduction/ShotProductionCta';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { buildShotProductionSnapshot, runwayFailedOnCurrentKf } from './ContentFamixaShotProduction/ShotProductionState';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');

const shot: FamixaSeriesShot = {
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '10s',
  seconds: 10,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
};

const line = {
  id: 'line-SC01-CHAR-001-1',
  characterId: 'CHAR-001',
  text: 'Mẹ xem giúp con tờ này.',
  voiceId: 'voice-minh',
  sceneId: 'SC01',
};

function stateOf(run: SeriesShotRun): SeriesPilotState {
  return {
    roles: [],
    runs: { [shot.id]: run },
    lines: [line],
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-minh' }],
    voiceAssets: { [line.id]: { lineId: line.id, duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'Famixa',
      episode: 'EP99',
      title: 'EP99',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
  };
}

const takeRun: SeriesShotRun = {
  status: 'approved',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: 'data:image/jpeg;base64,xx',
  keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  lipsyncUrl: 'https://fal/lip.mp4',
  lipsynced: true,
  shotQa: { action: true, continuity: true, voiceFace: true },
};

const tts = { [line.id]: { url: 'blob:voice', fileName: 'a.mp3' } };

function snapOf(run: SeriesShotRun) {
  const state = stateOf(run);
  return buildShotProductionSnapshot({ state, shot, ttsFiles: tts });
}

const fps = computeInputFingerprints(stateOf(takeRun), shot);
const motionStaleRun: SeriesShotRun = {
  ...takeRun,
  shotProduction: {
    voiceFp: fps.voice,
    kfFp: fps.keyframe,
    motionFp: 'stale-motion',
    lipsyncFp: fps.lipsync,
    assembleFp: fps.mix,
  },
};
const motionStaleSnap = snapOf(motionStaleRun);
ok(motionStaleSnap.motionStale === true && motionStaleSnap.input.hasMuteTake === true, '01 existing take + motionStale');
ok(nextShotProductionCommand(motionStaleSnap).type === 'ACCEPT_EXISTING', '01b stale visible take asks ACCEPT_EXISTING');
ok(directorPrimaryCta(motionStaleSnap).action === 'accept-existing', '01c Studio CTA is accept-existing');
ok(!studioMotionSendOpts(motionStaleSnap).remake, '02 remake is not the primary ACCEPT path');

const batch = readyV2VideoShots(stateOf(motionStaleRun), [shot]);
ok(batch.ready.length === 0 && batch.blocked.length === 0, '03 previewUrl still skips batch I2V');
ok(Boolean(motionStaleSnap.visibleTake?.url), '03b stale remake still sees the existing take');

const firstMotionRun: SeriesShotRun = {
  status: 'reviewed',
  kfApproved: true,
  keyframeDataUrl: 'data:image/jpeg;base64,xx',
  keyframeFileName: 'kf.jpg',
};
const firstMotionSnap = snapOf(firstMotionRun);
ok(nextShotProductionCommand(firstMotionSnap).type === 'CONFIRM_MOTION', '03c first motion still CONFIRM_MOTION');
ok(!studioMotionSendOpts(firstMotionSnap).remake, '03d first motion does not remake');

const lipStaleRun: SeriesShotRun = {
  ...takeRun,
  shotProduction: {
    voiceFp: fps.voice,
    kfFp: fps.keyframe,
    motionFp: fps.motion,
    lipsyncFp: 'stale-lipsync',
    assembleFp: fps.mix,
  },
};
const lipStaleSnap = snapOf(lipStaleRun);
ok(lipStaleSnap.lipSyncStale === true && lipStaleSnap.input.hasLipSync === true && !lipStaleSnap.motionStale, '04 lipsync stale only');
ok(nextShotProductionCommand(lipStaleSnap).type === 'CONFIRM_LIPSYNC', '04b command CONFIRM_LIPSYNC');
ok(studioLipsyncSendOpts(lipStaleSnap).remake === true, '04c Studio lipsync CTA remake=true');
ok(!studioMotionSendOpts(lipStaleSnap).remake, '04d lipsync stale does not remake motion');

const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const card = read('ContentFamixaShotProduction/ShotProductionCard.tsx');
const motionBlock = series.slice(series.indexOf('const startShotMotion'), series.indexOf('const rememberLineAudio'));
const lipsyncBlock = series.slice(series.indexOf('onEnsureLipsync={(shotId) => {'), series.indexOf('onEnsureMix={(shotId)'));
const videoBoard = series.slice(series.indexOf('<ContentFamixaBuildVideoBoard'), series.indexOf('</ContentFamixaBuildVideoBoard>'));
const overviewBind = series.slice(series.indexOf('onCreateSceneVideo='), series.indexOf('onCreateSceneVideo=') + 80);

ok(motionBlock.includes('forceNew: true') && motionBlock.includes('sendTurbo'), '02b Studio motion Confirms a remake job');
ok(
  motionBlock.includes('beforeTask') &&
    motionBlock.includes('newTask') &&
    motionBlock.includes('Chưa gửi job mới') &&
    !motionBlock.includes('Không gửi được:'),
  '02c Confirm does not replay stale INTERNAL.BAD_OUTPUT',
);
ok(
  motionBlock.includes('runwayFailedOnCurrentKf') &&
    motionBlock.includes('CONFIRM WAN') &&
    motionBlock.includes('Không gửi Runway'),
  '02d stop Runway after a real fail on this KF',
);
ok(
  !runwayFailedOnCurrentKf(
    {
      runwayAttempts: [
        { status: 'FAILED', taskId: 'old', kf: { hash: 'h-old' }, failureCode: 'INTERNAL.BAD_OUTPUT.CODE01' },
      ],
    },
    'h-new',
  ),
  '02e old-picture Runway FAIL does not poison new still',
);
ok(
  runwayFailedOnCurrentKf(
    {
      runwayAttempts: [
        { status: 'FAILED', taskId: 'now', kf: { hash: 'h-new' }, failureCode: 'INTERNAL.BAD_OUTPUT.CODE01' },
      ],
    },
    'h-new',
  ),
  '02f current-picture Runway FAIL still routes Wan',
);
ok(lipsyncBlock.includes('studioLipsyncSendOpts') && lipsyncBlock.includes('startLipsync([shotId], opts)'), '04e Studio lipsync wires remake opts');
ok(!motionBlock.includes('ensureShotVoice') && !lipsyncBlock.includes('ensureShotVoice'), '05 Voice is not regenerated');
ok(!motionBlock.includes('generateSceneKf') && !lipsyncBlock.includes('generateSceneKf'), '06 Keyframe is not regenerated');
ok(!card.includes('startContentSeriesTurbo') && !card.includes('/api/content/series/turbo') && !card.includes('fal-ai/'), '07 helpers do not call providers');
ok(series.includes('if (opts?.remake') && series.includes('if (resolveTakeUrl(run)) return true'), '03e remake path uses resolveTakeUrl');
ok(series.includes('lipsyncSendEligible') && read('content-famixa-prod-v2.ts').includes('!opts.remake'), '04f lipsync remake reuses existing skip');
ok(read('content-famixa-prod-v2.ts').includes('resolveTakeUrl(run) && !run.motionNeedsRemake'), '08 batch readyV2VideoShots skips existing take unless remake');
ok(videoBoard.includes('startSceneTurbo([shotId])') && !videoBoard.includes('studioMotionSendOpts'), '08b Video board still default I2V');
ok(overviewBind.includes('onCreateSceneVideo={startSceneTurbo}'), '09 Overview workflow unchanged');
ok(!card.includes('ContentKitVideoDirectorProductionWorkspace') && !motionBlock.includes('ContentKitVideoDirectorProductionWorkspace'), '10 Lane B unchanged');
ok(series.includes('onEnsureVoice={(shotId) => ensureShotVoice(shotId)}'), '05b Voice remains its own CTA');
const voiceFn = series.slice(series.indexOf('const ensureShotVoice'), series.indexOf('const assembleOneShot'));
ok(voiceFn.includes('hydrateSessionTts') && voiceFn.includes('resolveLineAudio'), '05c Tạo thoại hydrates IDB first');
ok(
  voiceFn.includes('Không tạo TTS mới')
    && voiceFn.includes('modal.confirm')
    && voiceFn.includes('loadCueAudio')
    && voiceFn.includes('okText: \'Tạo thoại\''),
  '05d Tạo thoại TTS only after Confirm',
);
ok(workspace.includes('hasVoiceFile') && !workspace.includes("cmd.type === 'ENSURE_VOICE' || cmd.type === 'ENSURE_PICTURE'"), '05e Director snap reads session blob; no voice retry loop');
ok(
  series.includes("prodTab === 'overview'") &&
    series.includes('directorBench') &&
    series.includes('studioShotId') &&
    series.includes('haveBlob && haveUrl'),
  '05f director bench hydrates session TTS including shot lines',
);
ok(series.includes('onEnsurePicture={(shotId) =>'), '06c Picture remains its own CTA');

if (fail.length) {
  console.error(`FAMIXA_SH01_STALE_REMAKE_GATE_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_STALE_REMAKE_GATE_V1 PASS FAIL=0 (no provider)');
