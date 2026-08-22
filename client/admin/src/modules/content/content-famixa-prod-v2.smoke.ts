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
} from './content-famixa-prod-v2';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

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
if (videoNeedIds(state, queue).join() !== 'b') fail.push('only SH02 needs video (has KF, no take)');

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

if (fail.length) {
  console.error('PROD V2 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('PROD V2 OK · queue', queue.length, '· selected', sum.count);
