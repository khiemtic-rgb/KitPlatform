import {
  canWorkV2Scene,
  clampShortSeconds,
  kfFollowIds,
  kfNeedIds,
  productionShorts,
  readyV2VideoShots,
  selectPreset,
  selectionSummary,
  shortSimpleStatus,
  videoNeedIds,
  lipsyncNeedIds,
  lipsyncInFlight,
  shouldResumeLipsync,
  lipsyncVideoUrl,
  takeVideoUrl,
  shotKeepsLipsync,
  parseFalLipsyncRef,
  estimateFalLipsyncUsd,
  estimateFalLipsyncUsdForShots,
  canRetryTurboStart,
  isTurboDailyQuota,
  inferRunwayBilled,
  shouldResumeTurboPoll,
  parseRetryAfterSec,
  nextRunwayQuietUntil,
  runwayQuietRemainMin,
} from './content-famixa-prod-v2';
import { outputAspectOf, outputFrameOf, sceneHasKeyframe, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string; shot: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    clock: '5s',
    seconds: 5,
    story: 'Minh bước ra khỏi cổng trường.',
    visual: '',
    characters: ['CHAR-001'],
    location: 'cổng trường',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    beatId: 'SC01-BEAT01',
    ...partial,
  };
}

const shots = [
  shot({ id: 'a', shot: 'SH01', story: 'Minh chạy vào nhà.' }),
  shot({ id: 'b', shot: 'SH02', story: 'Minh đưa bài kiểm tra.' }),
  shot({ id: 'c', shot: 'SH03', story: 'Mẹ nhìn điểm.' }),
  shot({ id: 'empty', shot: 'SH04', story: '' }),
];

const state = {
  roles: [],
  runs: {
    a: { status: 'story_locked' },
    b: { status: 'keyframe_ready', keyframeDataUrl: 'data:image/png;base64,aa', kfApproved: true },
    c: {
      status: 'turbo_testing',
      keyframeDataUrl: 'data:image/png;base64,aa',
      kfApproved: true,
      previewUrl: 'https://x/a.mp4',
    },
    empty: { status: 'story_locked' },
  },
  voiceLocked: true,
  shotGraphLocked: true,
  sceneMasters: {
    SC01: {
      sceneId: 'SC01',
      title: 'cổng',
      location: 'cổng trường',
      time: '',
      lighting: '',
      characters: 'Minh',
      wardrobe: '',
      props: '',
      environment: 'cổng trường',
      camera: '',
      mood: '',
      continuityRules: '',
      locked: true,
    },
  },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: '',
    episode: 'EP01',
    title: '8 điểm',
    premise: '',
    moral: '',
    ctaRule: '',
    shots,
  },
} as SeriesPilotState;

const fail: string[] = [];
const queue = productionShorts(state);
if (queue.length !== 3) fail.push(`queue must drop empty SH, got ${queue.length}`);
if (queue.some((s) => s.id === 'empty')) fail.push('empty short leaked into production');

const ten = selectPreset(queue, 10);
const sum = selectionSummary(queue, ten.fromId, ten.toId);
if (sum.count !== 3) fail.push(`select 10 of 3 must be 3, got ${sum.count}`);
if (sum.sec !== 15) fail.push(`duration 3×5s = 15, got ${sum.sec}`);

const five = selectPreset(queue, 5);
if (selectionSummary(queue, five.fromId, five.toId).count !== 3) fail.push('first 5 of 3 is 3');

if (shortSimpleStatus(state, shots[0]!) !== 'VOICE READY') fail.push('SH01 should be VOICE READY');
if (shortSimpleStatus(state, shots[1]!) !== 'KF READY') fail.push('approved SH02 should be KF READY');
if (shortSimpleStatus(state, shots[2]!) !== 'VIDEO READY') fail.push('SH03 should be VIDEO READY');
const unverified = {
  ...state,
  runs: { ...state.runs, c: { ...state.runs.c!, previewUrl: 'https://x/a.mp4', videoVerified: false as const } },
} as SeriesPilotState;
if (shortSimpleStatus(unverified, shots[2]!) === 'VIDEO READY') {
  fail.push('unverified take must not look VIDEO READY');
}
if (shortSimpleStatus(state, shots[3]!) !== 'HOLD') fail.push('empty is HOLD and not in queue');

if (kfNeedIds(state, queue).join() !== 'a') fail.push('only SH01 needs KF');
const followState = {
  ...state,
  runs: {
    ...state.runs,
    a: {
      status: 'keyframe_ready' as const,
      keyframeDataUrl: 'data:image/png;base64,aa',
      keyframeFileName: 'minh-ban-com.jpg',
    },
    b: {
      status: 'keyframe_ready' as const,
      keyframeDataUrl: 'data:image/png;base64,bb',
      keyframeFileName: 'kf-b-canon.png',
    },
  },
} as SeriesPilotState;
const follow = kfFollowIds(followState, queue);
if (follow.includes('a')) fail.push('user template must not be redrawn');
if (!follow.includes('b')) fail.push('Canon-sheet SH after template must redraw');
const approvedLock = {
  ...followState,
  runs: {
    ...followState.runs,
    a: { ...followState.runs.a!, kfApproved: true },
  },
} as SeriesPilotState;
if (kfFollowIds(approvedLock, queue)[0] !== 'b') fail.push('locked SH01 must start follow at next shot');
if (kfFollowIds(approvedLock, queue).includes('a')) fail.push('approved SH01 must stay out of KF queue');
if (videoNeedIds(state, queue).join() !== 'b') fail.push('only SH02 needs video (has KF, no take)');
if (lipsyncNeedIds(state, queue).join() !== 'c') fail.push('only SH03 take needs lipsync');
const lipDone = {
  ...state,
  runs: { ...state.runs, c: { ...state.runs.c!, lipsynced: true } },
} as SeriesPilotState;
if (lipsyncNeedIds(lipDone, queue).length) fail.push('lipsynced take must leave the Fal queue');
if (lipsyncInFlight({ lipsyncTaskId: 'lipsync_1', lipsyncStatus: 'PENDING' }) !== true) {
  fail.push('pending lipsync is in flight');
}
if (lipsyncInFlight({ lipsynced: true, lipsyncTaskId: 'lipsync_1', lipsyncStatus: 'SUCCEEDED' })) {
  fail.push('done lipsync must not stay in flight');
}
if (shouldResumeLipsync({ lipsyncTaskId: 'x', lipsyncStatus: 'FAILED', lipsyncError: 'Fal 405:' })) {
  fail.push('405 must not resume a dead Fal poll');
}
if (estimateFalLipsyncUsd(10) !== 0.12) fail.push('10s Fal lipsync 1.9 is $0.12');
if (estimateFalLipsyncUsdForShots([{ seconds: 10 }, { seconds: 10 }, { seconds: 10 }]) !== 0.36) {
  fail.push('3×10s Fal lipsync 1.9 is $0.36');
}
if (shouldResumeLipsync({ lipsyncTaskId: 'x', lipsyncStatus: 'PENDING' }) !== true) {
  fail.push('pending lipsync may resume');
}
if (lipsyncVideoUrl({ lipsynced: true, previewUrl: 'https://fal.example/a.mp4' }) !== 'https://fal.example/a.mp4') {
  fail.push('lipsynced preview is the download url');
}
if (lipsyncVideoUrl({ lipsynced: true, lipsyncUrl: 'https://fal.example/lip.mp4', previewUrl: 'https://runway.example/raw.mp4' }) !== 'https://fal.example/lip.mp4') {
  fail.push('dedicated lipsyncUrl wins');
}
if (lipsyncVideoUrl({ previewUrl: 'https://runway.example/raw.mp4' })) {
  fail.push('raw take must not look like lipsync');
}
if (takeVideoUrl({ lipsynced: true, lipsyncUrl: 'https://fal.example/lip.mp4', previewUrl: 'https://runway.example/raw.mp4' }) !== 'https://fal.example/lip.mp4') {
  fail.push('assemble must use Fal file when lipsynced');
}
if (takeVideoUrl({ previewUrl: 'https://runway.example/raw.mp4' }) !== 'https://runway.example/raw.mp4') {
  fail.push('raw take url is the assemble fallback');
}
if (!shotKeepsLipsync({ lipsyncUrl: 'https://fal.example/lip.mp4' })) {
  fail.push('lipsyncUrl without flag must still keep Fal audio');
}
if (shotKeepsLipsync({ previewUrl: 'https://runway.example/raw.mp4' })) {
  fail.push('raw take must not keep Fal audio');
}
if (parseFalLipsyncRef('https://v3b.fal.media/files/b/x/output.mp4').url !== 'https://v3b.fal.media/files/b/x/output.mp4') {
  fail.push('Fal mp4 url must attach with 0$');
}
if (parseFalLipsyncRef('abc123def456').taskId !== 'lipsync_v3_abc123def456') {
  fail.push('Usage request id must recover as v3');
}
const queuedNoTake = {
  ...state,
  runs: { ...state.runs, b: { ...state.runs.b!, turboStatus: 'PENDING' as const } },
} as SeriesPilotState;
if (shortSimpleStatus(queuedNoTake, shots[1]!) !== 'KF READY') {
  fail.push('queued without take must not look VIDEO READY');
}
const sceneLockedNoTake = { ...state, sceneLocked: true } as SeriesPilotState;
if (shortSimpleStatus(sceneLockedNoTake, shots[1]!) === 'VIDEO READY' || shortSimpleStatus(sceneLockedNoTake, shots[1]!) === 'FINAL') {
  fail.push('sceneLocked without take must not look ready');
}

const locked = { ...state, scriptLocked: true } as SeriesPilotState;
if (!canWorkV2Scene(locked)) fail.push('V2 scene must not wait for 9:16 short lock');
const draftKf = {
  ...locked,
  runs: {
    ...locked.runs,
    a: { status: 'story_locked' as const, keyframeDataUrl: 'data:image/png;base64,aa' },
  },
} as SeriesPilotState;
const v2ready = readyV2VideoShots(draftKf, queue);
if (v2ready.ready.some((s) => s.id === 'a')) fail.push('KF DRAFT must not send Runway');
if (!v2ready.ready.some((s) => s.id === 'b')) fail.push('approved SH02 must send Runway');
if (!v2ready.blocked.some((b) => b.shot.id === 'a' && /DRAFT/i.test(b.reason))) {
  fail.push('draft block reason');
}

if (clampShortSeconds(5) !== 5 || clampShortSeconds(10) !== 10) fail.push('duration only 5 or 10');
if (canRetryTurboStart('unexpected error', true)) fail.push('must not auto-retry after Runway created a task');
if (canRetryTurboStart('Runway 429 rate limit', false) !== true) fail.push('429 before create may retry');
if (canRetryTurboStart('Runway 429 daily-quota: 60: limit', false)) fail.push('daily quota must not auto-start');
if (!isTurboDailyQuota('Your daily task limit has been reached.')) fail.push('Runway daily task limit string');
if (canRetryTurboStart('Your daily task limit has been reached.', false)) fail.push('must not POST again after daily task limit');
if (inferRunwayBilled({ turboTaskId: 't1' }, 10) !== 50) fail.push('failed 10s job billed 50');
if (inferRunwayBilled({ runwayBilled: 100, turboTaskId: 't2' }, 10) !== 100) fail.push('keep cumulative billed');
if (!shouldResumeTurboPoll({ turboTaskId: 't', turboError: 'Runway giới hạn tốc độ' })) {
  fail.push('429 row must resume old task');
}
if (shouldResumeTurboPoll({ turboTaskId: 't', turboStatus: 'FAILED', turboError: 'unexpected' })) {
  fail.push('unexpected fail must not resume');
}
if (parseRetryAfterSec('Runway 429 retry-after: 45: x', 30) !== 45) fail.push('parse retry-after');
const quietAt = 1_700_000_000_000;
if (nextRunwayQuietUntil({ now: quietAt, hits: 3 }) - quietAt !== 30 * 60 * 1000) fail.push('3x 429 → 30 phút');
if (runwayQuietRemainMin(quietAt + 90_000, quietAt) !== 2) fail.push('quiet remain minutes');

if (outputAspectOf({}) !== '16:9') fail.push('unset aspect defaults 16:9 for old graphs');
if (outputAspectOf({ outputAspect: '9:16' }) !== '9:16') fail.push('chosen 9:16 must stick');
if (outputFrameOf('16:9').width !== 1280 || outputFrameOf('9:16').height !== 1280) {
  fail.push('locked pixels are 1280×720 / 720×1280');
}
if (sceneHasKeyframe({ ...state, runs: {} })) fail.push('empty runs have no KF');
if (!sceneHasKeyframe({ ...state, runs: { S01: { status: 'keyframe_ready', keyframeDataUrl: 'data:image/jpeg;x' } } })) {
  fail.push('KF pixels must count as hasKeyframe');
}

if (fail.length) {
  console.error('PROD V2 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('PROD V2 OK · queue', queue.length, '· selected', sum.count);
