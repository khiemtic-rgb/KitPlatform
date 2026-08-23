import {
  buildAssembleTimeline,
  formatSrt,
  looksLikeVideoUrl,
  existingTakesReady,
  planWithExistingTakes,
  rangeTakesReady,
  takeDownloadName,
  lipsyncDownloadName,
  assembleConfirmCopy,
  assembleNeedTtsOverlay,
} from './content-famixa-assemble';
import { mapPreviewCut } from './content-famixa-preview-cut';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '5s',
    seconds: 5,
    story: 'Minh gọi mẹ.',
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
];

const state = {
  roles: [],
  runs: {
    SH01: { status: 'turbo_testing' as const, keyframeDataUrl: 'data:image/png;base64,aa', previewUrl: 'https://x/a.mp4' },
    SH02: { status: 'turbo_testing' as const, keyframeDataUrl: 'data:image/png;base64,bb', previewUrl: 'https://x/b.mp4' },
  },
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001'],
      dialogue: [
        { id: 'L1', characterId: 'CHAR-001', text: 'Mẹ!' },
        { id: 'L2', characterId: 'CHAR-001', text: 'Mẹ xem này.' },
      ],
    },
  ],
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP01',
    title: 'T',
    premise: '',
    moral: '',
    ctaRule: '',
    shots,
  },
} as SeriesPilotState;

const fail: string[] = [];
const plan = mapPreviewCut(state, shots, { hasVoiceFile: () => true, voiceSecOf: () => 2 });
if (!rangeTakesReady(plan)) fail.push('both takes present');
const tl = buildAssembleTimeline(plan, { hasVoiceFile: () => true, voiceSecOf: () => 2, fit: 'speech' });
if (tl.clips.length !== 2) fail.push(`clips ${tl.clips.length}`);
if (tl.cues.length !== 2) fail.push(`cues ${tl.cues.length}`);
if (tl.lanes.length !== 5) fail.push(`lanes ${tl.lanes.length}`);
if (tl.lanes.find((l) => l.id === 'sfx')?.spans[0]?.label !== 'room tone') fail.push('sfx room tone');
if (tl.totalSec < 4 || tl.totalSec > 6.5) fail.push(`speech-fit total ${tl.totalSec}`);
if (tl.cues[0]!.startSec < 0.15 || tl.cues[0]!.startSec > 0.3) fail.push(`preroll ${tl.cues[0]!.startSec}`);
if (tl.missingVideo.length) fail.push(`missing video ${tl.missingVideo}`);
const takeFit = buildAssembleTimeline(plan, { hasVoiceFile: () => true, voiceSecOf: () => 1, fit: 'take' });
if (takeFit.totalSec !== 10) fail.push(`take-fit should keep 5s+5s, got ${takeFit.totalSec}`);
const srt = formatSrt(tl.cues);
if (!srt.includes('Mẹ!')) fail.push('srt missing line');
if (!srt.includes('00:00:00,200')) fail.push(`srt start ${srt.slice(0, 40)}`);
if (takeDownloadName(shots[0]!, shots) !== 'FAMIXA_SH01-01.mp4') fail.push(`name ${takeDownloadName(shots[0]!, shots)}`);
if (lipsyncDownloadName(shots[0]!, shots) !== 'FAMIXA_SH01-01-lipsync.mp4') {
  fail.push(`lipsync name ${lipsyncDownloadName(shots[0]!, shots)}`);
}

const noVid = mapPreviewCut(
  { ...state, runs: { SH01: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' } } } as SeriesPilotState,
  [shots[0]!],
  { hasVoiceFile: () => true },
);
if (rangeTakesReady(noVid)) fail.push('missing take must block assemble');
if (existingTakesReady(noVid)) fail.push('no-take plan is not existing-ready');
if (!existingTakesReady(plan)) fail.push('plan with takes must be existing-ready');
const mixed = mapPreviewCut(
  {
    ...state,
    runs: {
      SH01: { status: 'turbo_testing' as const, keyframeDataUrl: 'data:image/png;base64,aa', previewUrl: 'https://x/a.mp4' },
      SH02: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,bb' },
    },
  } as SeriesPilotState,
  shots,
  { hasVoiceFile: () => true },
);
if (rangeTakesReady(mixed)) fail.push('partial takes must not be full-range ready');
if (!existingTakesReady(mixed)) fail.push('one existing take is enough to test-mux');
if (planWithExistingTakes(mixed).items.map((i) => i.shotId).join() !== 'SH01') {
  fail.push('test mux keeps only existing takes');
}

const lipState = {
  ...state,
  runs: {
    ...state.runs,
    SH01: { ...state.runs.SH01!, lipsynced: true, lipsyncUrl: 'https://fal.example/lip.mp4' },
  },
} as SeriesPilotState;
const lipOnly = mapPreviewCut(lipState, shots, { hasVoiceFile: () => false });
const lipReady = planWithExistingTakes(lipOnly);
if (!lipReady.items[0]?.lipsynced) fail.push('SH01-01 lipsync must stay on the cut');
if (assembleNeedTtsOverlay(lipReady).map((i) => i.shotId).join() !== 'SH02') {
  fail.push('lipsynced SH01 must not demand TTS overlay');
}
const lipCopy = assembleConfirmCopy(lipReady);
if (!lipCopy.detail.includes('SH01-01') || !/khớp môi/i.test(lipCopy.detail)) {
  fail.push(`confirm must keep lipsync audio: ${lipCopy.detail}`);
}
if (/Ghép MP4 \+ thoại/.test(lipCopy.okText) && !/FAL|Preview/.test(lipCopy.okText)) {
  fail.push('mixed cut must not look like TTS-only mux');
}
if (!/miệng không theo lời|Preview tạm/i.test(lipCopy.detail)) fail.push('mixed cut must warn overlay has no lips');
const lipTl = buildAssembleTimeline(lipReady, { hasVoiceFile: () => true, voiceSecOf: () => 2, fit: 'speech' });
if (!lipTl.clips[0]?.useVideoAudio) fail.push('lipsynced clip must keep video audio');
if (lipTl.clips[1]?.useVideoAudio) fail.push('raw take must overlay TTS');

if (!looksLikeVideoUrl('https://dncdn.runwayml.com/generations/abc')) fail.push('runway url without suffix');
if (!looksLikeVideoUrl('https://cdn.example.com/a.mp4?x=1')) fail.push('mp4 query');
if (looksLikeVideoUrl('https://example.com/page')) fail.push('html must not be video');

if (fail.length) {
  console.error('ASSEMBLE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(`ASSEMBLE PASS · ${tl.clips.length} clip · ${tl.cues.length} cue · ${tl.totalSec}s`);
