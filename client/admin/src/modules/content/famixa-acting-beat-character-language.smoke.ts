/** FAMIXA_ACTING_BEAT_CHARACTER_LANGUAGE_FIX_V1 — speaker-aware wrappers. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from './content-famixa-series';
import { promptHashOf } from './content-famixa-runway-pipe';
import {
  ACTING_BODY_WEIGHT,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
  ACTING_STANCE_SIT,
  actingRoomStillForSpeaker,
  patchActingBeat,
  shotActingSpeakerOf,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import {
  buildShotProductionSnapshot,
  lastTakePromptHashOf,
  motionPromptFreshnessStale,
} from './ContentFamixaShotProduction/ShotProductionState';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { actingSpeakerFromDialogue } from './famixa-acting-beat-language';
import { computeShotTiming } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const series = read('content-famixa-series.ts');
const language = read('famixa-acting-beat-language.ts');
const runway = read('content-runway-prompt-v1.ts');
const helper = read('ContentFamixaShotProduction/ShotProductionActingBeat.ts');
const card = read('ContentFamixaShotProduction/ShotProductionActingBeatCard.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');

ok(series.includes('actingSpeakerFromDialogue') && series.includes('actingPerformanceLine'), 'compileI2vPrompt uses speaker language');
ok(language.includes('actingSpeakerFromDialogue') && !language.includes('characterIds'), 'speaker from dialogue, not roster');
ok(!series.includes("=== 'EP99-SC01-SH02'") && !language.includes("=== 'CHAR-003'"), 'no SH02 / CHAR-003 special case');
ok(runway.includes("The performance is ${acting}") && !runway.includes("motion.timing || 'Begins speaking"), 'Runway compiler has no default speech line');

const timing01 = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
const timing02 = computeShotTiming({ voiceDurations: [0.8], leadInSec: 0.2, tailSec: 0.22 });

const v2Beat = {
  before: {
    action: ACTING_STANCE_DOORWAY,
    body: ACTING_BODY_WEIGHT,
    prop: 'He holds a sheet of paper with both hands.',
    gaze: 'Looks toward his mother, then lowers his gaze slightly; not at the camera.',
    room: ACTING_ROOM_STILL,
    holdSec: 0.2,
  },
  during: { speech: true, emotion: 'uneasy' as const },
  after: { holdSec: 0.22 },
};

const invalidSh02Beat = {
  before: {
    action: ACTING_STANCE_DOORWAY,
    body: 'He takes a small half-step.',
    gaze: 'He looks toward his mother, not the camera.',
    room: ACTING_ROOM_STILL,
    holdSec: 0.2,
  },
  during: { speech: true, emotion: 'neutral' as const },
  after: { holdSec: 0.22 },
};

const sh01 = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.94,
  timing: timing01,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001', 'CHAR-003'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  actingBeat: v2Beat,
  ...over,
});

const sh02 = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH02',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH02',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.22,
  timing: timing02,
  story: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  visual: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  characters: ['CHAR-003'],
  characterIds: ['CHAR-001', 'CHAR-003'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-003-2'],
  actingBeat: invalidSh02Beat,
  ...over,
});

const roster = {
  roles: [
    { id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' },
    { id: 'role-CHAR-003', title: 'Mẹ', name: 'Linh', characterId: 'CHAR-003', voiceId: 'v3' },
  ],
  characters: [
    { id: 'CHAR-001', name: 'Minh', voiceId: 'v1' },
    { id: 'CHAR-003', name: 'Linh', voiceId: 'v3' },
  ],
};

const savedSh02Beat = patchActingBeat(
  {
    ...sh02(),
    actingBeat: patchActingBeat(sh02(), true, {
      emotion: 'neutral',
      action: ACTING_STANCE_SIT,
      body: 'Remains seated and makes only a small natural movement.',
      gaze: 'Keeps looking at the phone and does not look at the camera.',
      room: actingRoomStillForSpeaker('Linh'),
    }),
  },
  true,
  {},
);

const take02 = 'https://take/sh02-n3.mp4';
const run02: SeriesShotRun = {
  status: 'approved',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: 'data:image/jpeg;base64,xxSH02KF',
  keyframeFileName: 'kf-EP99-SC01-SH02-canon.jpg',
  takeUrl: take02,
  previewUrl: take02,
  runwayAttempts: [
    {
      n: 3,
      at: '',
      status: 'SUCCEEDED',
      promptHash: 'h75bfa5b0:286',
      taskId: '2f024b26',
      source: { hash: 'hsh02kf' },
    },
  ],
};

const stateOf = (shots: FamixaSeriesShot[], runs: Record<string, SeriesShotRun>): SeriesPilotState => ({
  roles: roster.roles,
  runs,
  characters: roster.characters,
  lines: [
    { id: 'line-SC01-CHAR-001-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con tờ này.', sceneId: 'SC01' },
    { id: 'line-SC01-CHAR-003-2', characterId: 'CHAR-003', text: 'Để đấy', sceneId: 'SC01' },
  ],
  voiceAssets: {
    'line-SC01-CHAR-001-1': { lineId: 'line-SC01-CHAR-001-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' },
    'line-SC01-CHAR-003-2': { lineId: 'line-SC01-CHAR-003-2', duration: 0.8, status: 'ready', characterId: 'CHAR-003' },
  },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP99',
    title: 'TEST BATCH 3 SHORT',
    premise: '',
    moral: '',
    ctaRule: '',
    shots,
  },
});

const liveSh02 = sh02();
const fixtureSh02 = { ...liveSh02, actingBeat: savedSh02Beat };
const row01 = sh01();
const state = stateOf([row01, liveSh02], { 'EP99-SC01-SH02': run02 });
const fixtureState = stateOf([row01, fixtureSh02], { 'EP99-SC01-SH02': run02 });

const speaker02 = actingSpeakerFromDialogue(state, liveSh02);
const uiSpeaker = shotActingSpeakerOf(state, liveSh02, [{ characterId: 'CHAR-003', text: 'Để đấy' }]);
ok(speaker02.characterId === 'CHAR-003' && speaker02.name === 'Linh', 'A SH02 speaker CHAR-003 Linh');
ok(uiSpeaker.characterId === 'CHAR-003' && uiSpeaker.name === 'Linh', 'A2 UI speaker matches dialogue');
ok(actingSpeakerFromDialogue(state, { ...liveSh02, characterIds: ['CHAR-001', 'CHAR-003'] }).name === 'Linh', 'A3 roster order does not steal speaker');

const compiled02 = compileI2vPrompt(fixtureState, fixtureSh02, fixtureSh02.story);
ok(!/His performance/i.test(compiled02), 'B no His performance on SH02');
ok(!/He begins speaking/i.test(compiled02), 'B2 no He begins speaking on SH02');
ok(!/\bthe boy\b/i.test(compiled02), 'B3 no the boy');
ok(/Linh's performance is neutral and contained/i.test(compiled02), 'B4 Linh performance wrapper');
ok(!/begins speaking/i.test(compiled02), 'B5 mute I2V has no speech timing');
ok(/Sitting on the sofa/i.test(compiled02), 'B6 sitting');
ok(/phone/i.test(compiled02) && !/Standing at the living-room doorway/i.test(compiled02), 'B7 phone, not doorway');
ok(/small natural movement/i.test(compiled02), 'B8 small movement');
ok(/only Linh makes subtle movement/i.test(compiled02), 'B9 room names Linh');

const compiled01 = compileI2vPrompt(state, row01, row01.story);
ok(/Standing at the living-room doorway/i.test(compiled01), 'C SH01 doorway');
ok(/sheet of paper/i.test(compiled01), 'C2 SH01 paper');
ok(/lowers his gaze slightly/i.test(compiled01), 'C3 SH01 Director gaze kept');
ok(/He shifts his weight slightly and hesitates/i.test(compiled01), 'C4 SH01 Director body kept');
ok(/The living room behind him stays still; only he moves/i.test(compiled01), 'C5 SH01 Director room kept');
ok(/uneasy and contained/i.test(compiled01), 'C6 SH01 uneasy');
ok(/Minh's performance is uneasy and contained/i.test(compiled01) || /His performance is uneasy and contained/i.test(compiled01), 'C7 SH01 wrapper uses Minh metadata or name');
ok(!/begins speaking/i.test(compiled01), 'C8 mute I2V has no speech timing');
ok(!/Linh's performance/i.test(compiled01), 'C9 SH01 is not Linh');

ok(liveSh02.actingBeat?.before.action === ACTING_STANCE_DOORWAY, 'D live SH02 beat not mutated');
ok(liveSh02.actingBeat?.before.body === 'He takes a small half-step.', 'D2 historical body kept');
ok(state.episode?.shots.find((s) => s.id === 'EP99-SC01-SH02')?.actingBeat?.before.action === ACTING_STANCE_DOORWAY, 'D3 graph fixture row unchanged');

const fps02 = computeInputFingerprints(state, liveSh02);
const stamped02: SeriesShotRun = {
  ...run02,
  shotProduction: { motionFp: fps02.motion, kfFp: fps02.keyframe, voiceFp: fps02.voice },
};
const tts02 = { 'line-SC01-CHAR-003-2': { url: 'blob:linh', fileName: 'linh.mp3' } };
const snap = buildShotProductionSnapshot({
  state: stateOf([fixtureSh02], { 'EP99-SC01-SH02': stamped02 }),
  shot: fixtureSh02,
  ttsFiles: tts02,
});
ok(lastTakePromptHashOf(stamped02) === 'h75bfa5b0:286', 'E n=3 promptHash kept');
ok(stamped02.takeUrl === take02 && stamped02.runwayAttempts?.[0]?.n === 3, 'E2 n=3 take kept');
ok(motionPromptFreshnessStale({
  hasMuteTake: true,
  lastTakePromptHash: 'h75bfa5b0:286',
  currentPromptHash: promptHashOf(compiled02),
}), 'E3 freshness STALE');
ok(snap.motionStale === true && snap.input.hasMuteTake === true, 'E4 snapshot STALE + take stored');

ok(!language.includes('fetch(') && !language.includes('startContentSeriesTurbo') && !language.includes('fal-ai/') && !language.includes('eleven'), 'F no provider in language helper');
ok(!card.includes('startSceneTurbo') && !helper.includes('fetch(') && !orch.includes('actingBeat'), 'F2 no media / orch untouched');

if (fail.length) {
  console.error(`FAMIXA_ACTING_BEAT_CHARACTER_LANGUAGE_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_ACTING_BEAT_CHARACTER_LANGUAGE_FIX_V1 PASS FAIL=0 (no provider)');
console.log(`speaker=${speaker02.characterId} ${speaker02.name}`);
console.log(`sh02Prompt=${compiled02}`);
console.log(`sh02Hash=${promptHashOf(compiled02)}`);
console.log(`sh01Prompt=${compiled01}`);
console.log(`sh01Hash=${promptHashOf(compiled01)}`);
console.log(`motionStale=${snap.motionStale}`);
