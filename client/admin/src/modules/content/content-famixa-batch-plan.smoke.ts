import {
  applySceneKfReuses,
  buildSceneKfPlan,
  continuityPlaceHint,
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
const byId = Object.fromEntries(plan.map((p) => [p.shotId, p]));
if (byId.SH01?.mode !== 'new') fail.push('SH01 should be NEW');
if (byId.SH02?.mode === 'new') fail.push('SH02 should reuse');
if (byId.SH04?.mode !== 'new') fail.push('SH04 close-up should be NEW');
if (byId.SH05?.mode !== 'new') fail.push('SH05 place shift should be NEW');
const gen = sceneKfToGenerate(sc01, plan, state);
if (gen.some((s) => s.id === 'SH01')) fail.push('SH01 already has KF — do not regenerate');
if (!gen.some((s) => s.id === 'SH04')) fail.push('SH04 NEW missing KF should generate');

const reused = applySceneKfReuses(state, sc01, plan);
if (!reused.runs.SH02?.keyframeDataUrl) fail.push('SH02 should copy KF01');
if (reused.runs.SH02?.keyframeInheritedFrom !== 'SH01') fail.push('SH02 inherit from SH01');

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
if (ready.ready.some((s) => s.id === 'SH02')) fail.push('SH02 reused KF still story_locked — not video-ready');
if (ready.blocked.length < 1) fail.push('blocked shots expected');

if (fail.length) {
  console.error('BATCH PLAN FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(
  `BATCH PLAN PASS · ${plan.filter((p) => p.mode === 'new').length} NEW · ${plan.filter((p) => p.mode !== 'new').length} REUSE`,
);
