import {
  applySceneKfReuses,
  buildSceneKfPlan,
  continuityPlaceHint,
  firstNewKfShot,
  previewEligibleIds,
  readySceneVideoShots,
  sceneKfToGenerate,
  shotsInScene,
} from './content-famixa-batch-plan';
import type { FamixaSeriesShot, SceneContinuityLock, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    shot: partial.id,
    clock: '0',
    seconds: 5,
    story: '',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'cổng trường',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    ...partial,
  };
}

const shots = [
  shot({ id: 'SH01', story: 'Minh đứng trước cổng trường.' }),
  shot({ id: 'SH02', story: 'Minh nhìn bài, vẫn đứng tại cổng.' }),
  shot({ id: 'SH03', story: 'Minh lấy điện thoại.' }),
  shot({ id: 'SH04', story: 'Cận cảnh bài kiểm tra.', visual: 'close-up test paper', location: 'cổng trường' }),
  shot({ id: 'SH05', story: 'Minh bước vào lớp.', location: 'lớp học' }),
];

const state = {
  roles: [],
  runs: {
    SH01: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aaa', shotAction: shots[0]!.story },
    SH02: { status: 'story_locked' as const, shotAction: shots[1]!.story },
    SH03: { status: 'story_locked' as const, shotAction: shots[2]!.story },
    SH04: { status: 'story_locked' as const, shotAction: shots[3]!.story },
    SH05: { status: 'story_locked' as const, shotAction: shots[4]!.story },
  },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'FAMIXA',
    episode: 'EP01',
    title: 'TỪ 5 LÊN 8',
    premise: '',
    moral: '',
    ctaRule: '',
    shots,
  },
} as SeriesPilotState;

const fail: string[] = [];
const sc01 = shotsInScene(shots, 'SC01');
if (sc01.length !== 5) fail.push(`scene filter ${sc01.length}`);
const plan = buildSceneKfPlan(state, sc01);
if (firstNewKfShot(sc01, plan)?.id !== 'SH01') fail.push('first NEW should be SH01');
const byId = Object.fromEntries(plan.map((p) => [p.shotId, p]));
if (byId.SH01?.mode !== 'new') fail.push('SH01 should be NEW');
if (byId.SH01?.code !== 'SH01-01') fail.push(`SH01 display ${byId.SH01?.code}`);
if (byId.SH02?.code !== 'SH01-02') fail.push(`SH02 display ${byId.SH02?.code}`);
if (byId.SH02?.mode !== 'new') fail.push('SH02 same room must draw next Action, not copy KF01');
if (!/kế thừa|action mới/i.test(byId.SH02?.reason ?? '')) fail.push(`SH02 reason ${byId.SH02?.reason}`);
if (byId.SH04?.mode !== 'new') fail.push('SH04 close-up should be NEW');
if (byId.SH05?.mode !== 'new') fail.push('SH05 place shift should be NEW');
const gen = sceneKfToGenerate(sc01, plan, state);
if (gen.some((s) => s.id === 'SH01')) fail.push('SH01 already has KF — do not regenerate');
if (!gen.some((s) => s.id === 'SH02')) fail.push('SH02 must generate next still from SH01');
if (!gen.some((s) => s.id === 'SH04')) fail.push('SH04 NEW missing KF should generate');

const reused = applySceneKfReuses(state, sc01, plan);
if (reused.runs.SH02?.keyframeDataUrl) fail.push('SH02 must not copy KF01 pixels — generate next still from SH01');

const lock: SceneContinuityLock = {
  id: 'L',
  episode: 'EP01',
  scene: 'SC01',
  characters: 'Minh',
  wardrobe: 'polo',
  position: '',
  environment: 'Cổng trường buổi chiều',
  camera: '35mm',
  performance: '',
  locked: true,
};
const hint = continuityPlaceHint(lock, shots[4]!, shots[4]!.story);
if (!hint) fail.push('SH05 should warn continuity place change');

const ready = readySceneVideoShots(reused, sc01, lock);
if (ready.ready.some((s) => s.id === 'SH02')) fail.push('SH02 without own KF is not video-ready');
if (ready.blocked.length < 1) fail.push('blocked shots expected');

const hold = shot({ id: 'SH06' });
const mixed = [...shots, hold];
const mixedState = { ...state, episode: { ...state.episode!, shots: mixed } } as SeriesPilotState;
const mixedPlan = buildSceneKfPlan(mixedState, mixed);
const mixedBy = Object.fromEntries(mixedPlan.map((p) => [p.shotId, p]));
if (mixedBy.SH06?.lane !== 'hold') fail.push(`empty shot lane ${mixedBy.SH06?.lane}`);
if (mixedBy.SH06?.eligible) fail.push('HOLD must not be production eligible');
const mixedGen = sceneKfToGenerate(mixed, mixedPlan, mixedState);
if (mixedGen.some((s) => s.id === 'SH06')) fail.push('HOLD must not enter KF generate queue');
const mixedReuse = applySceneKfReuses(mixedState, mixed, mixedPlan);
if (mixedReuse.runs.SH06?.keyframeDataUrl) fail.push('HOLD must not inherit KF');
if (previewEligibleIds(mixedPlan).has('SH06')) fail.push('HOLD not in preview cut');

const chain = [
  shot({ id: 'SH01', story: 'Minh bước vào lớp.', location: 'lớp học' }),
  shot({
    id: 'SH01b',
    story: 'Minh bước vào lớp.',
    location: 'lớp học',
    voiceChainFrom: 'SH01',
    inheritFromShotId: 'SH01',
  }),
];
const chainState = { ...state, episode: { ...state.episode!, shots: chain } } as SeriesPilotState;
const chainPlan = buildSceneKfPlan(chainState, chain);
const chainRow = chainPlan.find((p) => p.shotId === 'SH01b');
if (chainRow?.lane !== 'reuse' || chainRow.sourceShotId !== 'SH01') {
  fail.push(`voice chain must reuse host KF, got ${chainRow?.lane} ${chainRow?.sourceShotId}`);
}

const twin = [shot({ id: 'A', story: 'Minh đứng im nhìn mẹ.' }), shot({ id: 'B', story: 'Minh đứng im nhìn mẹ.' })];
const twinState = { ...state, episode: { ...state.episode!, shots: twin } } as SeriesPilotState;
const twinPlan = buildSceneKfPlan(twinState, twin);
if (twinPlan[1]?.lane !== 'reuse') fail.push(`same Action must REUSE, got ${twinPlan[1]?.lane}`);

const lockedTpl = [
  shot({ id: 'T01', story: 'Minh đưa bài.' }),
  shot({ id: 'T02', story: 'Mẹ nhìn điểm, mặt nghiêm nghị không vui.' }),
];
const lockedState = {
  ...state,
  episode: { ...state.episode!, shots: lockedTpl },
  runs: {
    T01: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,lock', kfApproved: true },
    T02: { status: 'story_locked' as const },
  },
} as SeriesPilotState;
const lockedPlan = buildSceneKfPlan(lockedState, lockedTpl);
const lockedCopy = applySceneKfReuses(lockedState, lockedTpl, lockedPlan);
if (lockedCopy.runs.T02?.keyframeDataUrl) fail.push('locked SH01 must not copy pixels onto SH02');

if (fail.length) {
  console.error('BATCH PLAN FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(
  `BATCH PLAN PASS · ${plan.filter((p) => p.mode === 'new').length} NEW · ${plan.filter((p) => p.mode !== 'new').length} REUSE`,
);
