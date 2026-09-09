/** FAMIXA_ACTING_BEAT_SHOT_CONTEXT_FIX_V1 — speaker + contextual chips. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from './content-famixa-series';
import { promptHashOf, sameFailedInput } from './content-famixa-runway-pipe';
import { shotI2vPromptHash } from './content-famixa-prod-v2';
import {
  ACTING_ACTION_CHIPS,
  ACTING_BODY_CHIPS,
  ACTING_BODY_WEIGHT,
  ACTING_GAZE_CHIPS,
  ACTING_ROOM_CHIPS,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
  ACTING_STANCE_SIT,
  INVALID_CONTEXTUAL_BEAT,
  actingBeatContextStatus,
  actingChipsForShot,
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
import { applyTimingToShot, computeShotTiming, deriveActingBeat } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const helper = read('ContentFamixaShotProduction/ShotProductionActingBeat.ts');
const card = read('ContentFamixaShotProduction/ShotProductionActingBeatCard.tsx');
const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const timingSrc = read('famixa-shot-production-timing.ts');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const compiler = read('content-runway-prompt-v1.ts');

ok(ws.includes('shotActingSpeakerOf') && !ws.includes('characterIds?.includes'), 'speaker from dialogue, not roster find');
ok(card.includes('actingChipsForShot') && card.includes('Tự viết hành động'), 'contextual chips + custom beat');
ok(card.includes('disabled={!dirty}'), 'save enabled only when dirty');
ok(!helper.includes("=== 'EP99-SC01-SH02'") && !timingSrc.includes("=== 'EP99-SC01-SH02'") && !card.includes("=== 'CHAR-003'"), 'no SH02 / CHAR-003 special case');

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
  characterIds: ['CHAR-003', 'CHAR-001'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-003-2'],
  actingBeat: invalidSh02Beat,
  ...over,
});

const rosterFirstMinh = {
  characters: [
    { id: 'CHAR-001', name: 'Minh', voiceId: 'v1' },
    { id: 'CHAR-003', name: 'Linh', voiceId: 'v3' },
  ],
  roles: [
    { id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' },
    { id: 'role-CHAR-003', title: 'Mẹ', name: 'Linh', characterId: 'CHAR-003', voiceId: 'v3' },
  ],
};

const speaker02 = shotActingSpeakerOf(
  { ...rosterFirstMinh, characters: [{ id: 'CHAR-001', name: 'Minh' }, { id: 'CHAR-003', name: 'Linh' }] },
  sh02({ characterIds: ['CHAR-001', 'CHAR-003'] }),
  [{ id: 'line-SC01-CHAR-003-2', characterId: 'CHAR-003', text: 'Để đấy' }],
);
ok(speaker02.characterId === 'CHAR-003' && speaker02.name === 'Linh', '01 SH02 speaker CHAR-003 Linh');
ok(speaker02.name !== 'Minh', '01b speaker is not roster-first Minh');

const derived02 = deriveActingBeat(sh02({ actingBeat: undefined }), timing02, { emotion: 'neutral' });
ok(!/doorway|Standing at the living-room/i.test(derived02.before.action || ''), '02 deriveActingBeat has no doorway');
ok(!/mother|half-step/i.test(`${derived02.before.body || ''} ${derived02.before.gaze || ''}`), '02b derive has no mom / half-step');
ok(/Sitting on the sofa/i.test(derived02.before.action || ''), '02c derive sitting from visual');
ok(/phone/i.test(`${derived02.before.prop || ''} ${derived02.before.gaze || ''}`), '02d derive phone from visual');

const kept = applyTimingToShot(sh02(), timing02, { emotion: 'burst' });
ok(kept.actingBeat?.before.action === ACTING_STANCE_DOORWAY, '02e applyTimingToShot does not overwrite stored beat');
ok(actingBeatContextStatus(sh02()) === INVALID_CONTEXTUAL_BEAT, '02f stored SH02 beat is INVALID_CONTEXTUAL_BEAT');

const labelsOf = (chips: { label: string; value: string }[]) => chips.map((c) => `${c.label}\n${c.value}`).join('\n');
const sh02Actions = actingChipsForShot(sh02(), ACTING_ACTION_CHIPS);
const sh02Bodies = actingChipsForShot(sh02(), ACTING_BODY_CHIPS);
const sh02Gazes = actingChipsForShot(sh02(), ACTING_GAZE_CHIPS);
const sh02Rooms = actingChipsForShot(sh02(), ACTING_ROOM_CHIPS);
const sh02Catalog = labelsOf([...sh02Actions, ...sh02Bodies, ...sh02Gazes, ...sh02Rooms]);
ok(!/Đứng ở cửa|Hơi do dự|Nhìn mẹ|Hạ mắt rồi nhìn mẹ|Bước nhẹ một bước/.test(sh02Catalog), '03 SH02 hides SH01 doorway/mom/step chips');
ok(!/Standing at the living-room doorway|toward his mother|half-step/i.test(sh02Catalog), '03b SH02 catalog values are not Minh doorway');
ok(sh02Actions.some((c) => c.value === ACTING_STANCE_SIT), '03c SH02 exposes sitting chip');
ok(sh02Gazes.some((c) => /phone/i.test(c.value)), '03d SH02 exposes phone gaze');
ok(sh02Bodies.some((c) => /seated|small natural movement/i.test(c.value)), '03e SH02 exposes seated motion');

const sh01Actions = actingChipsForShot(sh01(), ACTING_ACTION_CHIPS);
ok(sh01Actions.some((c) => c.value === ACTING_STANCE_DOORWAY), '03f SH01 still exposes doorway chips');
ok(actingChipsForShot(sh01(), ACTING_GAZE_CHIPS).some((c) => c.label === 'Nhìn mẹ'), '03g SH01 still exposes mom gaze');

const kf01 = 'data:image/jpeg;base64,xxSH01KF';
const kf02 = 'data:image/jpeg;base64,xxSH02KF';
const take02 = 'https://take/sh02-n3.mp4';

const run01: SeriesShotRun = {
  status: 'turbo_testing',
  kfApproved: true,
  keyframeDataUrl: kf01,
  keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
  failedKfHash: 'h2b879d96:147815',
  failedPromptHash: 'hedf22d6f:421',
  runwayAttempts: [
    {
      n: 4,
      at: '',
      status: 'FAILED',
      failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
      promptHash: 'hedf22d6f:421',
      taskId: '5c5dc70c',
      source: { hash: 'h2b879d96:147815' },
    },
  ],
};

const run02: SeriesShotRun = {
  status: 'approved',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: kf02,
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
  roles: rosterFirstMinh.roles,
  runs,
  characters: rosterFirstMinh.characters,
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

const row01 = sh01();
const row02 = sh02();
const beforeState = stateOf([row01, row02], { 'EP99-SC01-SH01': run01, 'EP99-SC01-SH02': run02 });
const beforePrompt = compileI2vPrompt(beforeState, row02, row02.story);
const beforeHash = promptHashOf(beforePrompt);

const savedBeat = patchActingBeat(
  {
    ...row02,
    actingBeat: patchActingBeat(row02, true, {
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
ok(/Sitting on the sofa/i.test(savedBeat.before.action || ''), '04 saved action sitting');
ok(/phone/i.test(`${savedBeat.before.prop || ''} ${savedBeat.before.gaze || ''}`), '04b saved phone');
ok(/Linh/i.test(savedBeat.before.room || ''), '04c saved room names Linh');
ok(actingBeatContextStatus({ ...row02, actingBeat: savedBeat }, savedBeat) === 'OK', '04d saved beat fits SH02 visual');

const afterShot = { ...row02, actingBeat: savedBeat };
const afterState = stateOf([row01, afterShot], { 'EP99-SC01-SH01': run01, 'EP99-SC01-SH02': run02 });
const afterPrompt = compileI2vPrompt(afterState, afterShot, afterShot.story);
const afterHash = promptHashOf(afterPrompt);
ok(afterHash !== beforeHash, '04e promptHash changes after Save');
ok(afterHash !== 'h75bfa5b0:286', '04f current hash is not n=3 h75bfa5b0:286');
ok(/Sitting on the sofa/i.test(afterPrompt) && !/Standing at the living-room doorway/i.test(afterPrompt), '04g compiled sitting, not doorway');

const tts02 = { 'line-SC01-CHAR-003-2': { url: 'blob:linh', fileName: 'linh.mp3' } };
const fps02 = computeInputFingerprints(beforeState, row02);
const stamped02: SeriesShotRun = {
  ...run02,
  shotProduction: { motionFp: fps02.motion, kfFp: fps02.keyframe, voiceFp: fps02.voice },
};
const snapBefore = buildShotProductionSnapshot({ state: stateOf([row02], { 'EP99-SC01-SH02': stamped02 }), shot: row02, ttsFiles: tts02 });
const snapAfter = buildShotProductionSnapshot({
  state: stateOf([afterShot], { 'EP99-SC01-SH02': stamped02 }),
  shot: afterShot,
  ttsFiles: tts02,
});
ok(lastTakePromptHashOf(stamped02) === 'h75bfa5b0:286', '05 last take remains n=3 hash');
ok(stamped02.takeUrl === take02 && stamped02.runwayAttempts?.[0]?.n === 3, '05b historical n=3 take kept');
ok(motionPromptFreshnessStale({ hasMuteTake: true, lastTakePromptHash: 'h75bfa5b0:286', currentPromptHash: afterHash }), '05c freshness helper STALE');
ok(snapAfter.motionStale === true && snapAfter.motionReady === false, '05d SH02 Motion n=3 is STALE after Save');
ok(snapAfter.input.hasMuteTake === true, '05e old take stays stored');

ok(row01.actingBeat?.before.action === ACTING_STANCE_DOORWAY, '06 SH01 V2 action kept');
ok(row01.actingBeat?.before.body === ACTING_BODY_WEIGHT, '06b SH01 V2 body kept');
ok(row01.actingBeat?.before.gaze?.includes('lowers his gaze'), '06c SH01 V2 gaze kept');
ok(row01.actingBeat?.before.room === ACTING_ROOM_STILL, '06d SH01 V2 room kept');
ok(row01.actingBeat?.during.emotion === 'uneasy', '06e SH01 V2 emotion kept');

const sh01Prompt = compileI2vPrompt(beforeState, row01, row01.story);
const sh01Hash = promptHashOf(sh01Prompt);
ok(/Standing at the living-room doorway/i.test(sh01Prompt) && /uneasy and contained/i.test(sh01Prompt), '07 SH01 semantic kept');
ok(/Minh's performance is uneasy and contained|His performance is uneasy and contained/i.test(sh01Prompt), '07b SH01 wrapper is Minh-aware');
ok(Boolean(shotI2vPromptHash(beforeState, row01, run01)), '07c SH01 still compiles');
const fps01a = computeInputFingerprints(beforeState, row01);
const fps01b = computeInputFingerprints(afterState, row01);
ok(fps01a.motion === fps01b.motion && Boolean(fps01a.motion), '07d SH01 motion fingerprint unchanged');
ok(Boolean(snapBefore.fps.motion) && snapBefore.fps.motion === snapAfter.fps.motion, '07e SH02 motionFp unchanged; stale is prompt freshness');

const src = `${helper}\n${card}\n${ws}`;
ok(!src.includes('startContentSeriesTurbo') && !src.includes('fal-ai/') && !src.includes('eleven') && !src.includes('runwayml'), '08 no provider calls');
ok(!card.includes('startSceneTurbo') && !card.includes('generateSceneKf') && !helper.includes('fetch('), '08b no media generation');
ok(!orch.includes('actingBeat') && compiler.includes('Blink and breathe.'), '08c orch / Runway compiler untouched');

if (fail.length) {
  console.error(`FAMIXA_ACTING_BEAT_SHOT_CONTEXT_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_ACTING_BEAT_SHOT_CONTEXT_FIX_V1 PASS FAIL=0 (no provider)');
console.log(`speaker=${speaker02.characterId} ${speaker02.name}`);
console.log(`sh02BeforeHash=${beforeHash}`);
console.log(`sh02AfterHash=${afterHash}`);
console.log(`sh02MotionStale=${snapAfter.motionStale}`);
console.log(`sh01Hash=${sh01Hash}`);
console.log(`sameFailedInput=${sameFailedInput(run01, 'h2b879d96:147815', sh01Hash)}`);
