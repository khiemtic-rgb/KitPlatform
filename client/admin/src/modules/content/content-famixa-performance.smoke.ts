import { lockLinePerformance, resolveLinePerformance } from './content-famixa-acting-law';
import {
  consecutiveDialogueWarning,
  emotionArcJumpWarning,
  isChildFromBible,
  patchDialoguePerformance,
  stampDialoguePerformances,
} from './content-famixa-performance';
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

const state = {
  roles: [],
  runs: {},
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceBible: { ageImpression: '11', habit: 'occasionally hesitates' } }],
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001'],
      dialogue: [
        { id: 'L1', characterId: 'CHAR-001', text: 'Mẹ... con được chín.' },
        { id: 'L2', characterId: 'CHAR-001', text: 'Thì sao?' },
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
    shots: [shot('SH01', { dialogueSegmentIds: ['L1'] }), shot('SH02', { dialogueSegmentIds: ['L2'] }), shot('SH03', { dialogueSegmentIds: ['L1'] })],
  },
} as SeriesPilotState;

const stamped = stampDialoguePerformances(state);
const d1 = stamped.scenes?.[0]?.dialogue?.[0]?.performance;
if (!d1?.locked) fail.push('Voice lock stamps performance');
const same = resolveLinePerformance({ text: 'other wording', characterId: 'CHAR-001', performance: d1 });
if (same.emotion !== d1?.emotion || same.intensity !== d1?.intensity) fail.push('F5 must keep locked voice_settings');

const patched = patchDialoguePerformance(stamped, 'L1', { emotion: 'uneasy', intensity: 3 });
const p1 = patched.scenes?.[0]?.dialogue?.[0]?.performance;
if (p1?.emotion !== 'uneasy' || p1.intensity !== 3 || !p1.locked) fail.push('operator patch locks line');

if (!isChildFromBible(state.characters?.[0])) fail.push('Minh 11 is child bible');
if (isChildFromBible({ voiceBible: { ageImpression: '40' } })) fail.push('adult bible is not child');

const talk = consecutiveDialogueWarning(stamped, stamped.episode!.shots);
if (!talk) fail.push('3 spoken shots must warn');

if (emotionArcJumpWarning({ emotionNow: 'vui / hy vọng', emotionNext: 'tổn thương' }, false) == null) {
  fail.push('arc jump without reaction must warn');
}
if (emotionArcJumpWarning({ emotionNow: 'vui', emotionNext: 'tổn thương' }, true)) {
  fail.push('reaction shot clears arc warn');
}

const keep = lockLinePerformance({
  emotion: 'hurt',
  intensity: 2,
  pace: 'slow',
  volume: 'low',
  pauseSec: 0.4,
  label: 'đau 2/5',
});
if (resolveLinePerformance({ text: 'Mẹ!', performance: keep }).emotion !== 'hurt') {
  fail.push('do not re-infer locked line');
}

if (fail.length) {
  console.error('PERFORMANCE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('PERFORMANCE PASS · lock + bible + arc + talk-warn');
