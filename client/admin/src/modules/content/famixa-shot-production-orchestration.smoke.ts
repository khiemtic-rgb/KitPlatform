import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { invalidateAfterEdit, retryScope, sameFingerprintBlindRetry } from './ContentFamixaShotProduction/ShotProductionActions';
import { oneShotAssembleBody } from './ContentFamixaShotProduction/ShotProductionAssemble';
import { ORCH_GATE } from './ContentFamixaShotProduction/ShotProductionErrors';
import { SHOT_PRODUCTION_ORCHESTRATION_ID } from './ContentFamixaShotProduction/ShotProductionIntent';
import {
  nextShotProductionCommand,
  produceShot,
  type ShotProductionAdapters,
} from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { directorPrimaryCta, shotQaMissing } from './ContentFamixaShotProduction/ShotProductionCta';
import { DIRECTOR_COPY, directorRecoveryOf } from './ContentFamixaShotProduction/ShotProductionDirector';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { applyProductionStamps, computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { ensureShotVoiceAssets } from './ContentFamixaShotProduction/ShotProductionVoice';
import { videoProductionPreflight } from './famixa-video-audio-lipsync-pipeline';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const shotOf = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'SH-03',
  scene: 'SC01',
  shot: '03',
  clock: '',
  seconds: 5,
  story: 'Minh nhìn bố rồi cúi đầu',
  visual: 'phòng khách',
  characters: ['Minh'],
  location: 'nhà',
  motionPrompt: 'slow tilt',
  motionPromptVi: 'nghiêng chậm',
  status: 'story_locked',
  sceneId: 'SC01',
  characterIds: ['CHAR-001'],
  dialogueSegmentIds: ['LINE-003'],
  ...over,
});

const line = (text = 'Con xin lỗi bố.', voice = true) => ({
  id: 'LINE-003',
  characterId: 'CHAR-001',
  text,
  sceneId: 'SC01',
  voiceId: voice ? 'voice-minh' : undefined,
});

function baseState(opts?: {
  shot?: FamixaSeriesShot;
  run?: SeriesShotRun;
  text?: string;
  voiceId?: boolean;
  duration?: number;
  silent?: boolean;
}): SeriesPilotState {
  const shot = opts?.silent
    ? shotOf({ dialogueSegmentIds: [] })
    : opts?.shot ?? shotOf();
  return {
    roles: [],
    runs: { [shot.id]: opts?.run ?? { status: 'story_locked' } },
    lines: opts?.silent ? [] : [line(opts?.text, opts?.voiceId !== false)],
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: opts?.voiceId === false ? undefined : 'voice-minh' }],
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
    voiceAssets:
      opts?.duration && opts.duration > 0
        ? { 'LINE-003': { lineId: 'LINE-003', duration: opts.duration, status: 'ready' } }
        : undefined,
  };
}

function snap(state: SeriesPilotState, extras?: Parameters<typeof buildShotProductionSnapshot>[0]) {
  const shot = state.episode!.shots[0]!;
  return buildShotProductionSnapshot({
    state,
    shot,
    ttsFiles: extras?.ttsFiles ?? (state.voiceAssets ? { 'LINE-003': { url: 'blob:1', fileName: 'a.mp3' } } : {}),
    ...extras,
    state: extras?.state ?? state,
    shot: extras?.shot ?? shot,
  });
}

function stampReady(state: SeriesPilotState, which: Array<'voice' | 'kf' | 'motion' | 'lipsync' | 'mix'>) {
  const shot = state.episode!.shots[0]!;
  const fps = computeInputFingerprints(state, shot);
  const run = state.runs[shot.id] ?? { status: 'story_locked' as const };
  const shotProduction = { ...(run.shotProduction ?? {}) };
  if (which.includes('voice')) shotProduction.voiceFp = fps.voice;
  if (which.includes('kf')) shotProduction.kfFp = fps.keyframe;
  if (which.includes('motion')) shotProduction.motionFp = fps.motion;
  if (which.includes('lipsync')) shotProduction.lipsyncFp = fps.lipsync;
  if (which.includes('mix')) shotProduction.assembleFp = fps.mix;
  state.runs[shot.id] = { ...run, shotProduction };
  return state;
}

const spokenReadyRun = (): SeriesShotRun => ({
  status: 'approved',
  keyframeDataUrl: 'data:image/jpeg;base64,xx',
  keyframeFileName: 'kf.jpg',
  kfApproved: true,
  previewUrl: 'https://take/mute.mp4',
  takeUrl: 'https://take/mute.mp4',
  videoApproved: true,
  lipsynced: true,
  lipsyncUrl: 'https://fal/out.mp4',
  finalSource: 'FAL',
  shotQa: { action: true, continuity: true, voiceFace: true, lipsyncQuality: true, finalAv: true },
});

ok(SHOT_PRODUCTION_ORCHESTRATION_ID === 'FAMIXA_SHOT_PRODUCTION_ORCHESTRATION_V1', '01 id');

const silentDraft = snap(baseState({ silent: true }));
ok(silentDraft.isSilent && nextShotProductionCommand(silentDraft).type === 'ENSURE_PICTURE', '02 silent');

const spokenDraft = snap(baseState({ duration: 0 }));
ok(!spokenDraft.voiceReady && nextShotProductionCommand(spokenDraft).type === 'ENSURE_VOICE', '03 spoken');

const noVoiceId = snap(baseState({ voiceId: false }));
ok(nextShotProductionCommand(noVoiceId).code === ORCH_GATE.VOICE_NOT_ASSIGNED, '04 missing voiceId');

const voiceMissing = snap(baseState({ duration: 0 }));
ok(voiceMissing.stage === 'VOICE_REQUIRED' && voiceMissing.voiceDurationSec === 0, '05 voice not ready');

const voiceOnly = stampReady(baseState({ duration: 2.8 }), ['voice']);
const voiceReadySnap = snap(voiceOnly);
ok(voiceReadySnap.voiceReady && voiceReadySnap.voiceDurationSec === 2.8 && nextShotProductionCommand(voiceReadySnap).type === 'ENSURE_PICTURE', '06 voice ready');

const kfMissing = voiceReadySnap;
ok(!kfMissing.keyframeReady && nextShotProductionCommand(kfMissing).type === 'ENSURE_PICTURE', '07 KF missing');

const kfStubOnly = snap(
  stampReady(
    baseState({
      duration: 2.8,
      run: { status: 'keyframe_ready', keyframeFileName: 'kf.jpg', kfApproved: false },
    }),
    ['voice'],
  ),
);
ok(!kfStubOnly.keyframeReady && nextShotProductionCommand(kfStubOnly).type === 'ENSURE_PICTURE', '07b fileName without pixels is Tạo hình');

const kfWaitState = stampReady(
  baseState({
    duration: 2.8,
    run: { status: 'keyframe_ready', keyframeDataUrl: 'data:image/jpeg;base64,xx', keyframeFileName: 'kf.jpg', kfApproved: false },
  }),
  ['voice', 'kf'],
);
const kfWait = snap(kfWaitState);
ok(nextShotProductionCommand(kfWait).type === 'WAIT_PICTURE_APPROVAL', '08 KF waiting approval');

const kfOkState = stampReady(
  baseState({
    duration: 2.8,
    run: { status: 'keyframe_ready', keyframeDataUrl: 'data:image/jpeg;base64,xx', keyframeFileName: 'kf.jpg', kfApproved: true },
  }),
  ['voice', 'kf'],
);
const kfOk = snap(kfOkState);
ok(kfOk.keyframeApproved && nextShotProductionCommand(kfOk).type === 'CONFIRM_MOTION', '09 KF approved');
ok(nextShotProductionCommand(kfOk).paid && nextShotProductionCommand(kfOk).kind === 'CONFIRM_REQUIRED', '10 motion confirmation');

const motionBusy = snap(kfOkState, { motionBusy: true, state: kfOkState, shot: kfOkState.episode!.shots[0]!, ttsFiles: { 'LINE-003': { url: 'b', fileName: 'a.mp3' } } });
ok(nextShotProductionCommand(motionBusy).type === 'WAIT_MOTION', '11 motion processing');

const motionReadyState = stampReady(
  baseState({
    duration: 2.8,
    run: {
      status: 'reviewed',
      keyframeDataUrl: 'data:image/jpeg;base64,xx',
      keyframeFileName: 'kf.jpg',
      kfApproved: true,
      previewUrl: 'https://take/mute.mp4',
      takeUrl: 'https://take/mute.mp4',
      videoApproved: true,
    },
  }),
  ['voice', 'kf', 'motion'],
);
const motionReady = snap(motionReadyState);
ok(motionReady.motionReady && nextShotProductionCommand(motionReady).type === 'LIPSYNC_QA_REQUIRED', '12 motion ready → QA');

ok(nextShotProductionCommand(motionReady).code === ORCH_GATE.LIPSYNC_QA_REQUIRED, '13 QA missing');

const qaState = structuredClone(motionReadyState);
qaState.runs['SH-03'] = { ...qaState.runs['SH-03']!, shotQa: { action: true, continuity: true, voiceFace: true } };
const qaSnap = snap(qaState);
ok(qaSnap.qaReady && nextShotProductionCommand(qaSnap).type === 'CONFIRM_LIPSYNC', '14 lipsync confirmation');

const lipBusy = snap(qaState, { lipsyncBusy: true, state: qaState, shot: qaState.episode!.shots[0]!, ttsFiles: { 'LINE-003': { url: 'b', fileName: 'a.mp3' } } });
ok(nextShotProductionCommand(lipBusy).type === 'WAIT_LIPSYNC', '15 lipsync processing');

const lipReadyState = stampReady(
  baseState({ duration: 2.8, run: spokenReadyRun() }),
  ['voice', 'kf', 'motion', 'lipsync'],
);
const lipReady = snap(lipReadyState);
ok(lipReady.lipSyncReady && !lipReady.mixReady && nextShotProductionCommand(lipReady).type === 'ENSURE_MIX', '16 mix required ≠ hasLip');

const mixBusy = snap(lipReadyState, { mixBusy: true, state: lipReadyState, shot: lipReadyState.episode!.shots[0]!, ttsFiles: { 'LINE-003': { url: 'b', fileName: 'a.mp3' } } });
ok(nextShotProductionCommand(mixBusy).type === 'WAIT_MIX', '17 mix processing');

const finalState = stampReady(baseState({ duration: 2.8, run: spokenReadyRun() }), ['voice', 'kf', 'motion', 'lipsync', 'mix']);
const finalSnap = snap(finalState);
ok(finalSnap.mixReady && finalSnap.finalReady && nextShotProductionCommand(finalSnap).type === 'READY_FINAL', '18 final ready');
const missingBlob = snap(finalState, { artifactPresent: false });
ok(missingBlob.mixReady && missingBlob.finalArtifactReady === false, '18i stamp without local file');
ok(nextShotProductionCommand(missingBlob).type === 'ENSURE_MIX', '18j Hoàn thiện remux when file gone');
ok(directorPrimaryCta(missingBlob).label === 'Hoàn thiện Shot', '18k director sees Hoàn thiện');
ok(directorRecoveryOf(missingBlob).message.includes('không còn trên máy'), '18l missing-file copy');

const mixReadyNoQualityState = stampReady(
  baseState({
    duration: 2.8,
    run: { ...spokenReadyRun(), shotQa: { action: true, continuity: true, voiceFace: true } },
  }),
  ['voice', 'kf', 'motion', 'lipsync', 'mix'],
);
const mixReadyNoQuality = snap(mixReadyNoQualityState);
ok(mixReadyNoQuality.lipSyncReady && mixReadyNoQuality.mixReady && !mixReadyNoQuality.qualityPassed, '18b lipsync+mix without quality ticks');
ok(nextShotProductionCommand(mixReadyNoQuality).type === 'LIPSYNC_QA_REQUIRED', '18c Kiểm tra stays until quality ticks');
ok(
  shotQaMissing(mixReadyNoQualityState.runs['SH-03']?.shotQa, true, { afterLipsync: true, afterMix: true }).join() ===
    'Lip-sync đạt,Final A/V đạt',
  '18d hidden ticks were the dead Kiểm tra',
);
ok(directorRecoveryOf(mixReadyNoQuality).status === 'QUALITY_QA', '18e director stays on quality QA');
ok(DIRECTOR_COPY.QUALITY_QA.includes('lồng tiếng'), '18f quality QA copy');
const lipNoSession = snap(lipReadyState, { ttsFiles: {} });
ok(lipNoSession.voiceReady && lipNoSession.lipSyncReady, '18g lipsync file counts as voice after session miss');
ok(nextShotProductionCommand(lipNoSession).type !== 'ENSURE_VOICE', '18h do not ask Tạo thoại after lipsync');

const multiShot = shotOf({ characterIds: ['CHAR-001', 'CHAR-002'] });
const multiState: SeriesPilotState = {
  ...baseState(),
  lines: [
    line(),
    { id: 'LINE-004', characterId: 'CHAR-002', text: 'Bố đây.', sceneId: 'SC01', voiceId: 'voice-bo' },
  ],
  characters: [
    { id: 'CHAR-001', name: 'Minh', voiceId: 'voice-minh' },
    { id: 'CHAR-002', name: 'Nam', voiceId: 'voice-nam' },
  ],
  episode: { ...baseState().episode!, shots: [{ ...multiShot, dialogueSegmentIds: ['LINE-003', 'LINE-004'] }] },
  runs: { 'SH-03': { status: 'story_locked' } },
};
ok(nextShotProductionCommand(snap(multiState)).code === ORCH_GATE.MULTI_SPEAKER_LIPSYNC_UNSUPPORTED, '19 multi-speaker blocked');

ok(invalidateAfterEdit('dialogue').voice && invalidateAfterEdit('dialogue').lipsync && invalidateAfterEdit('dialogue').mix && !invalidateAfterEdit('dialogue').picture, '20 dialogue invalidates voice/lipsync/mix');
ok(invalidateAfterEdit('voiceId').voice && invalidateAfterEdit('voiceId').lipsync && !invalidateAfterEdit('voiceId').picture, '21 voiceId invalidates voice/lipsync');
ok(invalidateAfterEdit('action').picture && invalidateAfterEdit('action').motion && invalidateAfterEdit('action').lipsync && !invalidateAfterEdit('action').voice, '22 action invalidates KF/motion/lipsync');
ok(invalidateAfterEdit('acting').voice && invalidateAfterEdit('acting').motion && invalidateAfterEdit('acting').visualReview, '23 emotion/acting invalidates relevant + visual review');

const staleVoiceState = structuredClone(finalState);
staleVoiceState.lines = [line('Con xin lỗi mẹ.')];
const staleVoice = snap(staleVoiceState);
ok(staleVoice.voiceStale && !staleVoice.voiceReady && !staleVoice.lipSyncReady && !staleVoice.finalReady, '24 stale voice cannot drive lipsync/final');

const staleKfState = structuredClone(finalState);
staleKfState.episode!.shots[0] = { ...staleKfState.episode!.shots[0]!, story: 'Minh chạy ra cửa' };
const staleKf = snap(staleKfState);
ok(staleKf.keyframeStale && !staleKf.keyframeApproved && !staleKf.motionReady && !staleKf.finalReady, '25 stale KF cannot drive I2V/final');

ok(!retryScope('motion').voice && !retryScope('motion').keyframe && retryScope('motion').i2v, '26 runway retry motion only');
ok(!retryScope('lipsync').voice && !retryScope('lipsync').keyframe && !retryScope('lipsync').i2v && retryScope('lipsync').lipsync, '27 fal retry lipsync only');
ok(!retryScope('mix').voice && !retryScope('mix').i2v && !retryScope('mix').lipsync && retryScope('mix').mix, '28 mix retry mix only');
ok(sameFingerprintBlindRetry({ failed: true, lastFailHash: 'aaa', nextHash: 'aaa' }), '29 same failed fingerprint blocked');

ok(lipReady.preflight.finalReady && !lipReady.finalReady, '30 final cannot be ready without assemble stamp');

const overlay = snap(lipReadyState);
ok(videoProductionPreflight({ ...overlay.input, overlayPreview: true }).finalReady === false, '31 TTS overlay not final');

const reloaded = snap(finalState);
ok(reloaded.mixReady && nextShotProductionCommand(reloaded).type === 'READY_FINAL', '32 reload keeps mix from assembleFp not useState');

const calls: string[] = [];
const adapters: ShotProductionAdapters = {
  ensureVoice: async () => {
    calls.push('voice');
  },
  ensurePicture: async () => {
    calls.push('picture');
  },
  ensureMotion: async () => {
    calls.push('motion');
  },
  ensureLipsync: async () => {
    calls.push('lipsync');
  },
  ensureMix: async () => {
    calls.push('mix');
  },
};

async function runAsync() {
  const produced = await produceShot(spokenDraft, adapters, () => spokenDraft);
  ok(produced.ran.includes('ENSURE_VOICE') && !calls.includes('motion'), 'async-voice-then-stop-paid');
  const paid = await produceShot(kfOk, adapters, () => kfOk);
  ok(paid.command.type === 'CONFIRM_MOTION' && !calls.includes('motion'), 'async-confirm-required');
  const afterConfirm = await produceShot(kfOk, adapters, () => kfOk, { confirmedMotion: true });
  ok(calls.includes('motion') && afterConfirm.command.type === 'CONFIRM_MOTION', 'async-confirm-then-motion');

  const readyVoice: string[] = [];
  await ensureShotVoiceAssets({
    lines: [
      { id: 'LINE-003', voiceId: 'voice-minh', text: 'xin chào', name: 'Minh' },
      { id: 'LINE-READY', voiceId: 'voice-minh', text: 'đã có', name: 'Minh' },
    ],
    alreadyReady: (id) => id === 'LINE-READY',
    loadCueAudio: async (row) => {
      readyVoice.push(row.id);
      return 'blob:1';
    },
  });
  ok(readyVoice.join(',') === 'LINE-003', 'reuse-ready-voice');

  const generated: string[] = [];
  const hydrated: string[] = [];
  await ensureShotVoiceAssets({
    lines: [{ id: 'LINE-IDB', voiceId: 'voice-linh', text: 'Để đấy', name: 'Linh' }],
    alreadyReady: () => false,
    hydrateCueAudio: async (row) => {
      hydrated.push(row.id);
      return 'blob:idb';
    },
    loadCueAudio: async (row) => {
      generated.push(row.id);
      return 'blob:tts';
    },
  });
  ok(hydrated.join(',') === 'LINE-IDB' && generated.length === 0, 'hydrate-skips-generate');

  let invented = false;
  try {
    await ensureShotVoiceAssets({
      lines: [{ id: 'X', text: 'hi', name: 'Minh' }],
      alreadyReady: () => false,
      loadCueAudio: async () => {
        invented = true;
        return 'x';
      },
    });
  } catch (e) {
    ok(e instanceof Error && e.message === ORCH_GATE.VOICE_NOT_ASSIGNED && !invented, 'no-invent-voice');
  }

  const edited: SeriesPilotState = {
    ...voiceOnly,
    lines: [line('Câu mới.')],
  };
  const stamped = applyProductionStamps(voiceOnly, edited);
  ok(stamped.runs['SH-03']?.shotProduction?.voiceFp === voiceOnly.runs['SH-03']?.shotProduction?.voiceFp, 'edit-does-not-restamp');
}

const body = oneShotAssembleBody({
  shotCode: 'SH03',
  seconds: 5,
  videoUrl: 'https://take/mute.mp4',
  spoken: true,
  lipsynced: true,
  voices: [{ lineId: 'LINE-003', startSec: 0, audioBase64: 'AAA' }],
});
ok(body.clips[0]?.useVideoAudio && body.clips[0]?.voices.length === 0, 'mix-keeps-fal-audio');

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const card = read('ContentFamixaShotProduction/ShotProductionCard.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const qa = read('ContentFamixaShotProduction/ShotProductionQa.tsx');
const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');

ok(series.includes('applyProductionStamps') && series.includes('readLive'), 'wired-authoritative-refresh');
ok(series.includes('ContentFamixaBuildVoiceBoard') && series.includes('ContentFamixaBuildImageBoard'), 'old-tabs-remain');
ok(series.includes('saveFinalBlob') && series.includes('Không mở take/lipsync như Final'), 'canonical-watch');
ok(qa.includes('Hành động phù hợp') && qa.includes('Giọng / khuôn mặt phù hợp'), 'qa-visible');
ok(qa.includes('Môi khớp thoại chưa?') && qa.includes('Hình và tiếng ổn chưa?'), 'qa-director-quality');
ok(qa.includes('afterLipsync') && ws.includes('if (!snap.lipSyncReady)') && ws.includes('void onEnsureMix(shotId)'), 'qa-does-not-relipsync-when-ready');
ok(!orch.includes('generateContentSeriesStill') && !orch.includes('startContentSeriesTurbo'), 'no-provider-in-orchestrator');
ok(!card.includes('MUTE_TAKE') && !card.includes('Fal $') && !card.includes('PVS'), 'staff-card-clean');

void runAsync()
  .then(() => {
    if (fail.length) {
      console.error(`FAMIXA_SHOT_PRODUCTION_ORCHESTRATION_V1_HARDENING FAIL=${fail.length}`);
      for (const name of fail) console.error(`  - ${name}`);
      process.exit(1);
    }
    console.log('FAMIXA_SHOT_PRODUCTION_ORCHESTRATION_V1_HARDENING PASS FAIL=0 (no provider)');
  })
  .catch((err) => {
    console.error(err);
    process.exit(1);
  });
