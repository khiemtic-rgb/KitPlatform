import {
  assembleVideoUrl,
  finalSourceBlockReason,
  mergeKeepFinalSource,
  resolveFinalSource,
  resolveTakeUrl,
  stampFalFinal,
  stampFinalSource,
  stampMuteTake,
} from './content-famixa-final-source';
import { assembleNeedTtsOverlay, assembleConfirmCopy, buildAssembleTimeline } from './content-famixa-assemble';
import { mapPreviewCut } from './content-famixa-preview-cut';
import { lipsyncNeedIds, lipsyncQaReady } from './content-famixa-prod-v2';
import { multiSpeakerBlock } from './content-famixa-dialogue-map';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(id: string, extra?: Partial<FamixaSeriesShot>): FamixaSeriesShot {
  return {
    id,
    scene: 'SC01',
    sceneId: 'SC01',
    shot: id,
    clock: '5s',
    seconds: 5,
    story: 'Minh nói.',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: '',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    dialogueSegmentIds: extra?.dialogueSegmentIds ?? ['L1'],
    ...extra,
  };
}

const fail: string[] = [];

if (resolveFinalSource({ lipsynced: true, lipsyncUrl: 'https://fal.example/lip.mp4' }) !== 'FAL') {
  fail.push('lipsync must resolve FAL');
}
if (resolveFinalSource({ previewUrl: 'https://runway.example/a.mp4' }, false) !== 'RUNWAY_TTS') {
  fail.push('spoken raw take is preview temp');
}
if (resolveFinalSource({ previewUrl: 'https://runway.example/a.mp4' }, true) !== 'RUNWAY') {
  fail.push('silent take is RUNWAY');
}
if (resolveFinalSource({}) !== 'NONE') fail.push('empty is NONE');
if (assembleVideoUrl({ lipsyncUrl: 'https://fal.example/lip.mp4', previewUrl: 'https://runway.example/raw.mp4', lipsynced: true }) !== 'https://fal.example/lip.mp4') {
  fail.push('assemble must use Fal file');
}
if (assembleVideoUrl({ takeUrl: 'https://runway.example/take.mp4' }) !== 'https://runway.example/take.mp4') {
  fail.push('assemble must use takeUrl when previewUrl is gone');
}

const kept = mergeKeepFinalSource({ previewUrl: 'https://runway.example/raw.mp4' }, {
  lipsynced: true,
  lipsyncUrl: 'https://fal.example/lip.mp4',
  finalSource: 'FAL',
});
if (kept.finalSource !== 'FAL' || kept.lipsyncUrl !== 'https://fal.example/lip.mp4') {
  fail.push('merge must keep local Fal');
}

const shots = [
  shot('SH01', { dialogueSegmentIds: ['L1'] }),
  shot('SH06', { dialogueSegmentIds: ['L6'] }),
  shot('SH07', { dialogueSegmentIds: [] }),
  shot('SH09', { dialogueSegmentIds: ['L1', 'L9'] }),
];
const state = {
  roles: [],
  runs: {
    SH01: { status: 'turbo_testing', previewUrl: 'https://x/1.mp4', keyframeDataUrl: 'data:image/png;base64,aa' },
    SH06: {
      status: 'turbo_testing',
      previewUrl: 'https://runway.example/6.mp4',
      lipsyncUrl: 'https://fal.example/6.mp4',
      lipsynced: true,
      finalSource: 'FAL',
      keyframeDataUrl: 'data:image/png;base64,bb',
    },
    SH07: { status: 'turbo_testing', previewUrl: 'https://x/7.mp4', keyframeDataUrl: 'data:image/png;base64,cc' },
    SH09: { status: 'turbo_testing', previewUrl: 'https://x/9.mp4', keyframeDataUrl: 'data:image/png;base64,dd' },
  },
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001', 'CHAR-003'],
      dialogue: [
        { id: 'L1', characterId: 'CHAR-001', text: 'Mẹ!' },
        { id: 'L6', characterId: 'CHAR-001', text: 'Không.' },
        { id: 'L9', characterId: 'CHAR-003', text: 'Bạn An được mấy?' },
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

const plan = mapPreviewCut(state, shots, { hasVoiceFile: () => true });
if (plan.items[1]?.finalSource !== 'FAL') fail.push('Test 5: SH06 must be FAL');
if (assembleNeedTtsOverlay(plan).some((i) => i.shotId === 'SH06')) fail.push('Test 5: FAL must not overlay TTS');
const tl = buildAssembleTimeline(plan, { hasVoiceFile: () => true, voiceSecOf: () => 2, fit: 'speech' });
if (!tl.clips[1]?.useVideoAudio) fail.push('Test 5: FAL clip keeps video audio');
if (tl.clips[0]?.useVideoAudio) fail.push('Test 5: raw spoken overlays TTS');

const need = lipsyncNeedIds(state, shots);
if (need.includes('SH06')) fail.push('Test 5: FAL leaves Fal queue');
if (need.includes('SH07')) fail.push('Test 6: silent shot must not call Fal');
if (!need.includes('SH01')) fail.push('spoken raw take still needs Fal');

const block = finalSourceBlockReason(plan.items.map((i) => ({ code: i.code, silent: i.silent, finalSource: i.finalSource, lipsynced: i.lipsynced })));
if (!block) fail.push('Test 7: mixed FAL + TTS must block Final');
const copy = assembleConfirmCopy(plan);
if (!/FAL/i.test(copy.detail) || !/chưa khớp môi|mix TTS|RUNWAY_TTS|Preview/i.test(copy.detail)) {
  fail.push(`Test 7 confirm must list sources: ${copy.detail}`);
}

const allFal = stampFinalSource({ lipsynced: true, lipsyncUrl: 'https://fal.example/a.mp4' });
if (allFal.finalSource !== 'FAL') fail.push('stamp FAL');

const mute = stampMuteTake('https://runway.example/raw.mp4', false);
if (mute.takeUrl !== 'https://runway.example/raw.mp4' || mute.finalSource !== 'RUNWAY_TTS') {
  fail.push('TAKE stamp is mute Runway');
}
const falKeep = stampFalFinal(
  { takeUrl: 'https://runway.example/raw.mp4', previewUrl: 'https://runway.example/raw.mp4' },
  'https://fal.example/lip.mp4',
);
if (falKeep.takeUrl !== 'https://runway.example/raw.mp4') fail.push('Fal must keep TAKE');
if (falKeep.previewUrl) fail.push('Fal must not overwrite previewUrl');
if (falKeep.lipsyncUrl !== 'https://fal.example/lip.mp4' || falKeep.finalSource !== 'FAL') {
  fail.push('Fal stamps FINAL only');
}
if (resolveTakeUrl({ ...mute, ...falKeep }) !== 'https://runway.example/raw.mp4') {
  fail.push('resolve TAKE after Fal');
}
if (need.includes('SH09')) fail.push('two speakers must not enter Fal queue');
if (!multiSpeakerBlock([{ characterId: 'CHAR-001', name: 'Minh' }, { characterId: 'CHAR-003', name: 'Linh' }])) {
  fail.push('two speakers block Fal');
}
if (multiSpeakerBlock([{ characterId: 'CHAR-001', name: 'Minh' }, { characterId: 'CHAR-001', name: 'Minh' }])) {
  fail.push('same speaker is not multi');
}
if (lipsyncQaReady({})) fail.push('QA empty is not ready');
if (lipsyncQaReady({ shotQa: { action: true, continuity: true } })) fail.push('spoken QA needs VOICE/FACE');
if (!lipsyncQaReady({ shotQa: { action: true, continuity: true, voiceFace: true } })) fail.push('ACTION+CONTINUITY+VOICE/FACE ready');
if (!lipsyncQaReady({ shotQa: { action: true, continuity: true } }, false)) fail.push('silent QA skips VOICE/FACE');

if (fail.length) {
  console.error('FINAL SOURCE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('FINAL SOURCE PASS · Test 5/6/7');
