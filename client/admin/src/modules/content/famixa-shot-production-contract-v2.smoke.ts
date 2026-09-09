/** FAMIXA_SHOT_PRODUCTION_CONTRACT_V2 — deterministic, 0 providers, 0 media writes, 0 graph mutation. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveTakeUrl } from './content-famixa-final-source';
import { lipsyncSendEligible, shotI2vPromptHash } from './content-famixa-prod-v2';
import { dataUriHash, sameFailedInput } from './content-famixa-runway-pipe';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { hasResolvableVoiceArtifact } from './famixa-video-audio-lipsync-pipeline';
import {
  acceptExistingTake,
  DIRECTOR_COPY,
  directorRecoveryOf,
  directorTextIsSafe,
  lastTakePromptHashOf,
  lastSuccessMotionTaskId,
  motionLifecycleOf,
  motionRemakeEligible,
  nextShotProductionCommand,
} from './ContentFamixaShotProduction';
import { directorPrimaryCta } from './ContentFamixaShotProduction/ShotProductionCta';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const src = [
  'ContentFamixaShotProduction/ShotProductionState.ts',
  'ContentFamixaShotProduction/ShotProductionOrchestrator.ts',
  'ContentFamixaShotProduction/ShotProductionArtifacts.ts',
  'ContentFamixaShotProduction/ShotProductionDirector.ts',
  'ContentFamixaShotProduction/ShotProductionCard.tsx',
  'content-famixa-prod-v2.ts',
].map(read).join('\n');

ok(!/shotId\s*===\s*['"]EP99-SC01-SH0[123]['"]/.test(src), 'no special-case shot id');
ok(!/if\s*\(\s*shot\.id\s*===\s*['"]SH0[123]['"]/.test(src), 'no special-case SH0x id');
ok(!src.includes('startContentSeriesTurbo') && !src.includes('fal-ai/') && !src.includes('elevenlabs'), 'no provider calls in contract path');

const KF = 'data:image/jpeg;base64,contractV2kf';
const KF_HASH = dataUriHash(KF);
const TAKE_2 = 'https://take/n2.mp4';
const PROMPT_A = 'hAAAA0001:111';
const PROMPT_B = 'hBBBB0002:222';

function shotOf(id: string, over?: Partial<FamixaSeriesShot>): FamixaSeriesShot {
  return {
    id,
    scene: 'SC01',
    sceneId: 'SC01',
    shot: id.slice(-3),
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
    ...over,
  };
}

function successAttempt(n: number, promptHash: string, url: string) {
  return {
    n,
    at: `2026-09-05T00:00:0${n}.000Z`,
    status: 'SUCCEEDED' as const,
    promptHash,
    outputUrl: url,
    taskId: `task-ok-${n}`,
    source: { hash: KF_HASH },
    kf: { hash: KF_HASH },
  };
}

function failAttempt(n: number, promptHash: string) {
  return {
    n,
    at: `2026-09-05T00:00:0${n}.000Z`,
    status: 'FAILED' as const,
    promptHash,
    failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
    error: 'INTERNAL.BAD_OUTPUT.CODE01',
    taskId: `task-fail-${n}`,
    source: { hash: KF_HASH },
    kf: { hash: KF_HASH },
  };
}

function runOf(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'turbo_testing',
    kfApproved: true,
    keyframeDataUrl: KF,
    keyframeFileName: 'kf.jpg',
    takeUrl: TAKE_2,
    previewUrl: undefined,
    ...over,
  };
}

function stateOf(shot: FamixaSeriesShot, run: SeriesShotRun, voiceReady = true): SeriesPilotState {
  return {
    roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'voice-1' }],
    runs: { [shot.id]: run },
    characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'voice-1' }],
    lines: [{ id: 'line-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con.', voiceId: 'voice-1' }],
    voiceAssets: voiceReady
      ? { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } }
      : { 'line-1': { lineId: 'line-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
    episode: {
      seriesCode: 'FAMIXA',
      seriesTitle: 'F',
      episode: 'EP99',
      title: 'CONTRACT V2',
      premise: '',
      moral: '',
      ctaRule: '',
      shots: [shot],
    },
  };
}

const tts = { 'line-1': { url: 'blob:voice', fileName: 'a.mp3' } };

function snapOf(shot: FamixaSeriesShot, run: SeriesShotRun, files = tts) {
  return buildShotProductionSnapshot({
    state: stateOf(shot, run),
    shot,
    ttsFiles: files,
    hasVoiceFile: (id) => Boolean(files[id as 'line-1']?.url),
  });
}

const fixtureShot = shotOf('EP99-SC01-FX01');
const failAfterSuccess = runOf({
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  failedKfHash: KF_HASH,
  failedPromptHash: PROMPT_B,
  runwayAttempts: [successAttempt(2, PROMPT_A, TAKE_2), failAttempt(3, PROMPT_B)],
});

const life = motionLifecycleOf(failAfterSuccess);
ok(life.currentAttempt?.n === 3 && life.currentAttempt.status === 'FAILED', 'T1 currentAttempt = n3 FAIL');
ok(life.lastSuccessTake?.n === 2 && life.lastSuccessTake.url === TAKE_2, 'T1 lastSuccessTake = n2');
ok(life.lastFail?.n === 3, 'T1 lastFail = n3');
ok(life.visibleTake?.n === 2 && life.visibleTake.url === TAKE_2, 'T1 visibleTake = n2');
ok(!life.acceptedTake, 'T1 acceptedTake = null');
ok(lastTakePromptHashOf(failAfterSuccess) === PROMPT_A, 'T1 lastTakePromptHash = A');
ok(lastSuccessMotionTaskId(failAfterSuccess) === 'task-ok-2', 'T1 lastSuccessMotionTaskId ignores FAIL n3');
ok(
  lastSuccessMotionTaskId({
    ...failAfterSuccess,
    turboTaskId: 'task-fail-3',
    acceptedTake: { n: 2, url: TAKE_2 },
    takeHistory: [{ url: TAKE_2, taskId: 'task-fail-3' }],
  }) === 'task-ok-2',
  'T1 lastSuccessMotionTaskId prefers accepted SUCCESS',
);
ok(resolveTakeUrl(failAfterSuccess) === TAKE_2, 'T1 resolveTakeUrl keeps SUCCESS take');

const snap1 = snapOf(fixtureShot, failAfterSuccess);
ok(snap1.currentAttempt?.n === 3, 'T1 snap current n3');
ok(snap1.lastSuccessTake?.n === 2, 'T1 snap lastSuccess n2');
ok(snap1.visibleTake?.url === TAKE_2, 'T1 snap visible n2');
ok(nextShotProductionCommand(snap1).type === 'ACCEPT_EXISTING', 'T1 next = ACCEPT_EXISTING');
ok(directorPrimaryCta(snap1).label === 'Dùng video này', 'T1 CTA Dùng video này');
ok(directorRecoveryOf(snap1).actions.some((a) => a.type === 'ACCEPT_EXISTING'), 'T1 recovery has ACCEPT_EXISTING');

const sameInputRun = runOf({
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  failedKfHash: KF_HASH,
  failedPromptHash: PROMPT_B,
  runwayAttempts: [successAttempt(2, PROMPT_A, TAKE_2), failAttempt(3, PROMPT_B)],
});
ok(sameFailedInput(sameInputRun, KF_HASH, PROMPT_B) === true, 'T2 sameFailedInput true');
ok(sameFailedInput(sameInputRun, KF_HASH, PROMPT_A) === false, 'T2 different prompt opens circuit');
const rec2 = directorRecoveryOf(snap1);
ok(rec2.actions.some((a) => a.type === 'ACCEPT_EXISTING') && rec2.actions.some((a) => a.type === 'EDIT_INPUT'), 'T2 CTA accept or edit');
ok(!rec2.actions.some((a) => a.type === 'RETRY_MOTION' || a.type === 'CONFIRM_MOTION'), 'T2 no blind retry');
ok(rec2.message.includes('Không thể thử lại') || rec2.message.includes('Video trước đó'), 'T2 human message');

const acceptedPatch = acceptExistingTake(failAfterSuccess);
ok(acceptedPatch?.acceptedTake.url === TAKE_2, 'T3 accept points at old take');
ok(failAfterSuccess.runwayAttempts?.length === 2, 'T3 accept does not drop attempts');
ok(failAfterSuccess.failedPromptHash === PROMPT_B, 'T3 accept does not reset fail stamp');
ok(sameFailedInput({ ...failAfterSuccess, ...acceptedPatch }, KF_HASH, PROMPT_B) === true, 'T3 sameFailedInput stays');

const acceptedRun: SeriesShotRun = { ...failAfterSuccess, ...acceptedPatch };
const snap3 = snapOf(fixtureShot, acceptedRun);
ok(snap3.acceptedTake?.url === TAKE_2, 'T3 acceptedTake = old take');
ok(snap3.motionState === 'ACCEPTED' || snap3.motionState === 'ACCEPTED_STALE', 'T3 motion ACCEPTED/ACCEPTED_STALE');
const qaRun: SeriesShotRun = {
  ...acceptedRun,
  videoApproved: true,
  shotQa: { action: true, continuity: true, voiceFace: true },
};
const snap3b = snapOf(fixtureShot, qaRun);
ok(
  lipsyncSendEligible({
    run: qaRun,
    spoken: true,
    motionStale: snap3b.motionStale,
    accepted: true,
  }),
  'T3 lipsync eligible after accept when voice+QA pass',
);

ok(resolveTakeUrl(qaRun) === TAKE_2, 'T4 resolveTakeUrl returns old SUCCESS');
ok(
  lipsyncSendEligible({ run: qaRun, spoken: true, motionStale: true, accepted: true }),
  'T4 motionStale alone does not block when accepted',
);
ok(
  !lipsyncSendEligible({ run: failAfterSuccess, spoken: true, motionStale: true, accepted: false }),
  'T4 stale still blocks when not accepted',
);

const previewless = runOf({
  previewUrl: undefined,
  takeUrl: TAKE_2,
  runwayAttempts: [successAttempt(2, PROMPT_A, TAKE_2)],
});
ok(Boolean(visibleTakeOfSafe(previewless)), 'T5 visibleTake exists without previewUrl');
ok(
  motionRemakeEligible({
    run: previewless,
    kfApproved: true,
    hasValidAction: true,
    kfHash: KF_HASH,
    promptHash: 'other',
  }),
  'T5 remake recognizes takeUrl',
);

ok(lastTakePromptHashOf(failAfterSuccess) === PROMPT_A, 'T6 lastTakePromptHashOf = A');
ok(lastTakePromptHashOf(failAfterSuccess) !== PROMPT_B, 'T6 lastTakePromptHashOf is not B');

ok(!hasResolvableVoiceArtifact({ durationSec: 1.52, voiceId: 'voice-1' }), 'T7 duration only is NOT READY');
ok(hasResolvableVoiceArtifact({ durationSec: 1.52, voiceId: 'voice-1', ttsFile: { url: 'blob:a', fileName: 'a.mp3' } }), 'T8 blob/url is READY');
const noVoiceSnap = buildShotProductionSnapshot({
  state: stateOf(fixtureShot, failAfterSuccess),
  shot: fixtureShot,
  ttsFiles: {},
});
ok(!noVoiceSnap.voiceReady, 'T7 snapshot voice NOT READY without artifact');
ok(
  directorRecoveryOf(noVoiceSnap).actions.some((a) => a.label === 'Nạp thoại' || a.label === 'Tạo thoại'),
  'T7 CTA Nạp/Tạo thoại',
);

ok(snap1.visibleTake?.url === TAKE_2 && snap1.currentAttempt?.status === 'FAILED', 'T9 old take remains visible');
ok(snap1.currentAttempt?.status !== 'SUCCESS', 'T9 no fake success');
ok(nextShotProductionCommand(snap1).type !== 'CONFIRM_MOTION', 'T9 no provider retry command');

const freshTake = runOf({
  previewUrl: TAKE_2,
  takeUrl: TAKE_2,
  videoApproved: true,
  lipsynced: true,
  lipsyncUrl: 'https://fal/lip.mp4',
  finalSource: 'FAL',
  shotQa: { action: true, continuity: true, voiceFace: true, lipsyncQuality: true, finalAv: true },
  runwayAttempts: [successAttempt(1, PROMPT_A, TAKE_2)],
  acceptedTake: { n: 1, url: TAKE_2, promptHash: PROMPT_A, kfHash: KF_HASH },
});
const freshState = stateOf(fixtureShot, freshTake);
const fps = computeInputFingerprints(freshState, fixtureShot);
const finalRun: SeriesShotRun = {
  ...freshTake,
  shotProduction: {
    voiceFp: fps.voice,
    kfFp: fps.keyframe,
    motionFp: fps.motion,
    lipsyncFp: fps.lipsync,
    assembleFp: fps.mix,
  },
};
const snap10 = snapOf(fixtureShot, finalRun);
ok(snap10.voiceReady && snap10.motionUsable && snap10.lipSyncReady && snap10.mixReady && snap10.finalReady, 'T10 finalReady only with valid artifacts');
ok(snap10.preflight.finalReady !== undefined, 'T10 preflight still exists for VA');
ok(snap10.finalReady === true, 'T10 Director finalReady uses snapshot SoT');

function visibleTakeOfSafe(run: SeriesShotRun) {
  return motionLifecycleOf(run).visibleTake;
}

const recA = directorRecoveryOf(snap1);
ok(recA.message.includes('Video trước đó'), 'UX A keep-old-take copy');
ok(recA.actions.some((a) => a.label === 'Dùng video này'), 'UX A/2 Dùng video này');
ok(directorTextIsSafe(recA.message) && recA.actions.every((a) => directorTextIsSafe(a.label)), 'UX no tech leak');

const lockedNoTake = {
  ...snapOf(
    fixtureShot,
    runOf({
      takeUrl: undefined,
      previewUrl: undefined,
      turboStatus: 'FAILED',
      turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
      failedKfHash: KF_HASH,
      failedPromptHash: PROMPT_B,
      runwayAttempts: [failAttempt(1, PROMPT_B)],
    }),
  ),
  retryLocked: true,
};
ok(directorRecoveryOf(lockedNoTake).message.includes('Chưa tạo được video') || directorRecoveryOf(lockedNoTake).message.includes('Không thể thử lại'), 'UX D/B no-take readable');
ok(directorRecoveryOf(lockedNoTake).actions.some((a) => a.label === 'Tạo video'), 'UX D/B Tạo video when locked without take');
ok(directorRecoveryOf(lockedNoTake).actions.some((a) => a.label === 'Tạo hình mới'), 'UX D/B Tạo hình mới when locked');

const firstGen = snapOf(fixtureShot, runOf({ takeUrl: undefined, previewUrl: undefined, runwayAttempts: [] }));
ok(directorPrimaryCta(firstGen).label === 'Tạo video' || nextShotProductionCommand(firstGen).type === 'CONFIRM_MOTION', 'UX 4 Tạo video');

const acceptedRec = directorRecoveryOf(snap3b);
ok(
  acceptedRec.actions.some((a) => a.label === 'Lồng tiếng' || a.label === 'Kiểm tra video') ||
    ['CONFIRM_LIPSYNC', 'LIPSYNC_QA_REQUIRED'].includes(nextShotProductionCommand(snap3b).type),
  'UX 5 accepted continues to QA/lipsync',
);

ok(!DIRECTOR_COPY.SUCCESS_THEN_FAIL.includes('hash'), 'UX copy has no hash');

const sh01 = shotOf('EP99-SC01-SH01');
const sh02 = shotOf('EP99-SC01-SH02', { dialogueSegmentIds: ['line-1'] });
const sh03 = shotOf('EP99-SC01-SH03');
const sh01Run = runOf({
  takeUrl: 'https://take/sh01.mp4',
  runwayAttempts: [successAttempt(3, PROMPT_A, 'https://take/sh01.mp4'), failAttempt(4, 'hedf22d6f:421')],
  failedPromptHash: 'hedf22d6f:421',
  failedKfHash: KF_HASH,
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
});
const sh02Run = runOf({
  takeUrl: 'https://take/sh02.mp4',
  runwayAttempts: [successAttempt(3, 'h75bfa5b0:286', 'https://take/sh02.mp4'), failAttempt(4, 'hcd889087:356')],
  failedPromptHash: 'hcd889087:356',
  failedKfHash: KF_HASH,
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
});
const sh03Run = runOf({
  takeUrl: 'https://take/sh03.mp4',
  runwayAttempts: [successAttempt(2, 'ha41fc287:94', 'https://take/sh03.mp4'), failAttempt(3, 'h83b82fdd:278')],
  failedPromptHash: 'h83b82fdd:278',
  failedKfHash: KF_HASH,
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
});
for (const [label, shot, run] of [
  ['SH01', sh01, sh01Run],
  ['SH02', sh02, sh02Run],
  ['SH03', sh03, sh03Run],
] as const) {
  const lifeN = motionLifecycleOf(run);
  const snapN = snapOf(shot, run);
  ok(lifeN.visibleTake?.url && lifeN.currentAttempt?.status === 'FAILED', `${label} visible SUCCESS kept after FAIL`);
  ok(nextShotProductionCommand(snapN).type === 'ACCEPT_EXISTING', `${label} next ACCEPT_EXISTING`);
  ok(!src.includes(`if (shot.id === '${shot.id}')`), `${label} not special-cased`);
}

const sh04 = shotOf('EP99-SC01-SH04');
const sh04Fail = runOf({
  takeUrl: 'https://take/sh04-n2.mp4',
  runwayAttempts: [successAttempt(2, PROMPT_A, 'https://take/sh04-n2.mp4'), failAttempt(3, PROMPT_B)],
  failedPromptHash: PROMPT_B,
  failedKfHash: KF_HASH,
  turboStatus: 'FAILED',
  turboError: 'provider failed',
});
const sh04Life = motionLifecycleOf(sh04Fail);
ok(sh04Life.visibleTake?.url === 'https://take/sh04-n2.mp4', 'SH04 visible old take');
const sh04Accept = { ...sh04Fail, ...acceptExistingTake(sh04Fail) };
const sh04Qa: SeriesShotRun = {
  ...sh04Accept,
  videoApproved: true,
  shotQa: { action: true, continuity: true, voiceFace: true },
};
const sh04Snap = snapOf(sh04, sh04Qa);
ok(sh04Snap.acceptedTake?.url === 'https://take/sh04-n2.mp4', 'SH04 ACCEPT_EXISTING');
ok(
  lipsyncSendEligible({
    run: sh04Qa,
    spoken: true,
    motionStale: sh04Snap.motionStale,
    accepted: true,
  }),
  'SH04 lipsync eligible after accept',
);
ok(resolveTakeUrl(sh04Qa) === 'https://take/sh04-n2.mp4', 'SH04 resolveTakeUrl old SUCCESS');

const card = read('ContentFamixaShotProduction/ShotProductionCard.tsx');
ok(card.includes('Dùng video này') || card.includes('ACCEPT_EXISTING'), 'UX card can show accept');
ok(!card.includes('sameFailedInput') && !card.includes('INTERNAL.BAD_OUTPUT'), 'UX card hides tech codes');

const series = read('ContentFamixaSeriesTab.tsx');
const lipsyncFn = series.slice(series.indexOf('const startLipsync'), series.indexOf('const attachLipsync'));
ok(lipsyncFn.includes('lastSuccessMotionTaskId') && lipsyncFn.includes('takeTaskId'), 'lipsync sends SUCCESS task id');
ok(lipsyncFn.includes('getContentSeriesTurbo(takeTaskId)'), 'lipsync refreshes take URL · 0 cr');
ok(!lipsyncFn.includes('startContentSeriesTurbo'), 'lipsync does not create I2V');
ok(lipsyncFn.includes('Hỏi lại task cũ') && lipsyncFn.includes('Không tạo video chuyển động mới'), 'lipsync expired-link copy');

const turboRoot = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs');
const turbo = readFileSync(turboRoot, 'utf8');
const fetchStart = turbo.indexOf('private async Task<byte[]> FetchTakeForLipsyncAsync');
const fetchTake = fetchStart >= 0 ? turbo.slice(fetchStart, fetchStart + 1200) : '';
ok(fetchTake.includes('RecoverAsync') && fetchTake.includes('takeTaskId'), 'API refreshes expired take from old task');
ok(!fetchTake.includes('CreateImageToVideo'), 'API lipsync refresh does not create I2V');

if (fail.length) {
  console.error(`FAMIXA_SHOT_PRODUCTION_CONTRACT_V2 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SHOT_PRODUCTION_CONTRACT_V2 PASS FAIL=0 (no provider, no media, no graph write)');
