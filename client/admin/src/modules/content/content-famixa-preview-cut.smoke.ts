import {
  existingMotionReady,
  mapPreviewCut,
  motionPreviewReady,
  shotsInInclusiveRange,
  storyPreviewReady,
} from './content-famixa-preview-cut';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '0',
    seconds: 5,
    story: '',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'cổng',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    ...partial,
  };
}

const shots = [
  shot({ id: 'SH01', story: 'Minh gọi mẹ.' }),
  shot({ id: 'SH02', story: 'Minh đưa bài.' }),
  shot({ id: 'SH03', story: 'Linh hỏi.', characters: ['CHAR-003'], characterIds: ['CHAR-003'] }),
  shot({ id: 'SH04', story: 'Câm — nhìn cổng.', characters: ['CHAR-001'], characterIds: ['CHAR-001'] }),
];

const state = {
  roles: [],
  runs: {
    SH01: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' },
    SH02: { status: 'story_locked' as const },
    SH03: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,bb', previewUrl: 'https://x/a.mp4' },
    SH04: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,cc' },
  },
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001', 'CHAR-003'],
      dialogue: [
        { id: 'L1', characterId: 'CHAR-001', text: 'Mẹ!' },
        { id: 'L2', characterId: 'CHAR-001', text: 'Mẹ xem này.' },
        { id: 'L3', characterId: 'CHAR-003', text: 'Tám?' },
        { id: 'L4', characterId: 'CHAR-001', text: 'Con được tám điểm.' },
      ],
    },
  ],
  episode: { seriesCode: 'FAMIXA', seriesTitle: 'F', episode: 'EP01', title: 'T', premise: '', moral: '', ctaRule: '', shots },
} as SeriesPilotState;

const fail: string[] = [];
const range = shotsInInclusiveRange(shots, 'SH01', 'SH03');
if (range.map((s) => s.id).join() !== 'SH01,SH02,SH03') fail.push(`range ${range.map((s) => s.id)}`);

const plan = mapPreviewCut(state, range, { hasVoiceFile: () => true });
if (plan.items[0]?.line?.id !== 'L1') fail.push('SH01 should get first Minh line');
if (plan.items[1]?.line?.id !== 'L2') fail.push('SH02 should get second Minh line');
if (plan.items[2]?.line?.id !== 'L3') fail.push('SH03 Linh should get Linh line');
if (!plan.extraLines.some((l) => l.id === 'L4')) fail.push('L4 must stay unmapped — do not dump onto last Short');
if (plan.items[2]?.lines.some((l) => l.id === 'L4')) fail.push('SH03 must not swallow leftover L4');
if (plan.estimatedSec !== 15) fail.push(`range duration is shot seconds 15, got ${plan.estimatedSec}`);
if (plan.storyMissingKf.join() !== 'SH02') fail.push(`missing KF ${plan.storyMissingKf}`);
if (storyPreviewReady(plan)) fail.push('story not ready — SH02 no KF');
if (motionPreviewReady(plan)) fail.push('full motion needs every take');
if (!existingMotionReady(plan)) fail.push('SH03 take must allow play existing');
if (plan.items[2]?.lipsynced) fail.push('SH03 must not look lipsynced without flag');
const lipPlan = mapPreviewCut(
  {
    ...state,
    runs: { ...state.runs, SH03: { ...state.runs.SH03!, lipsynced: true } },
  } as SeriesPilotState,
  range,
  { hasVoiceFile: () => true },
);
if (!lipPlan.items[2]?.lipsynced) fail.push('lipsynced short must be marked on the cut');

const long = mapPreviewCut(
  {
    ...state,
    scenes: [
      {
        id: 'SC01',
        characterIds: ['CHAR-001'],
        dialogue: [{ id: 'LONG', characterId: 'CHAR-001', text: 'A'.repeat(120) }],
      },
    ],
  } as SeriesPilotState,
  [shots[0]!],
  { hasVoiceFile: () => true },
);
if (!long.durationBlocked) fail.push('long line should block motion (voice > shot)');
if (!storyPreviewReady(long)) fail.push('story preview still allowed with duration warning');
if (motionPreviewReady(long)) fail.push('motion must block on duration');

const later = Array.from({ length: 12 }, (_, i) =>
  shot({
    id: `R${String(i + 1).padStart(2, '0')}`,
    story: `Beat ${i + 1} nhìn bài.`,
  }),
);
const laterState = {
  ...state,
  episode: { ...state.episode, shots: later },
  runs: Object.fromEntries(
    later.map((s) => [s.id, { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' }]),
  ),
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001'],
      dialogue: later.map((s, i) => ({ id: `X${i + 1}`, characterId: 'CHAR-001', text: `Câu ${i + 1}.` })),
    },
  ],
} as SeriesPilotState;
const laterPlan = mapPreviewCut(laterState, shotsInInclusiveRange(later, 'R01', 'R10'), { hasVoiceFile: () => false });
if (laterPlan.extraLines.length) fail.push(`12 lines / 12 shorts should map 1:1, extra ${laterPlan.extraLines.length}`);
if (laterPlan.items[9]?.line?.id !== 'X10') fail.push(`R10 should keep X10, got ${laterPlan.items[9]?.line?.id}`);
if (laterPlan.estimatedSec !== 50) fail.push(`10×5s = 50, got ${laterPlan.estimatedSec}`);
if (!storyPreviewReady(laterPlan)) fail.push('KF 10/10 must allow GHÉP PREVIEW even without session TTS');

const beatShots = [
  shot({
    id: 'BA',
    beatId: 'SC01-BEAT01',
    beatText: 'Minh nhìn bài. Mẹ xem này. Con được tám điểm.',
    story: 'Cận mặt Minh',
  }),
  shot({
    id: 'BB',
    beatId: 'SC01-BEAT01',
    beatText: 'Minh nhìn bài. Mẹ xem này. Con được tám điểm.',
    story: 'Nhìn điểm tám',
  }),
  shot({
    id: 'BC',
    beatId: 'SC01-BEAT01',
    beatText: 'Minh nhìn bài. Mẹ xem này. Con được tám điểm.',
    story: 'Đưa giấy cho mẹ',
  }),
];
const beatState = {
  ...state,
  episode: { ...state.episode, shots: beatShots },
  runs: {
    BA: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' },
    BB: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' },
    BC: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' },
  },
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001'],
      dialogue: [
        { id: 'D1', characterId: 'CHAR-001', text: 'Mẹ xem này.' },
        { id: 'D2', characterId: 'CHAR-001', text: 'Con được tám điểm.' },
      ],
    },
  ],
} as SeriesPilotState;
const beatPlan = mapPreviewCut(beatState, beatShots, { hasVoiceFile: () => true });
if (beatPlan.items[0]?.lines.length !== 1) fail.push(`beat first camera must not swallow all lines, got ${beatPlan.items[0]?.lines.length}`);
if (beatPlan.items[0]?.line?.id !== 'D1' || beatPlan.items[1]?.line?.id !== 'D2') {
  fail.push(`split beat should be D1/D2 across cameras, got ${beatPlan.items.map((i) => i.line?.id).join(',')}`);
}

if (fail.length) {
  console.error('PREVIEW CUT FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(`PREVIEW CUT PASS · ${plan.items.length} shot · extra ${plan.extraLines.length}`);
