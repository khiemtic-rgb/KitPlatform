/** FAMIXA_MOTION_CONTRACT_V2 — body/room on existing actingBeat. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, slimPilotForStorage, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';
import { i2vPromptIsEnglish } from './content-famixa-i2v-en';
import { promptHashOf, sameFailedInput } from './content-famixa-runway-pipe';
import { compileRunwayPromptV1, promptViolatesRunwayI2vLaw } from './content-runway-prompt-v1';
import {
  ACTING_ACTION_CHIPS,
  ACTING_BODY_CHIPS,
  ACTING_BODY_WEIGHT,
  ACTING_ROOM_CHIPS,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
  patchActingBeat,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import { computeShotTiming, productionDurationOf } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const card = read('ContentFamixaShotProduction/ShotProductionActingBeatCard.tsx');
const helper = read('ContentFamixaShotProduction/ShotProductionActingBeat.ts');
const compiler = read('content-runway-prompt-v1.ts');
const seriesMap = read('content-famixa-series.ts');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const stamp = read('ContentFamixaShotProduction/ShotProductionStamp.ts');

const timing = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
ok(timing.productionDurationSec === 1.94, '15 timing remains 1.94');

const shot = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.94,
  timing,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001', 'CHAR-003'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  ...over,
});

const weight = ACTING_BODY_CHIPS.find((c) => c.label === 'Hơi chuyển trọng tâm')!;
const roomQuiet = ACTING_ROOM_CHIPS.find((c) => c.label === 'Phòng phía sau yên')!;
const doorway = ACTING_ACTION_CHIPS.find((c) => c.label === 'Đứng ở cửa')!;
ok(weight.value === ACTING_BODY_WEIGHT, 'SH01 body chip English');
ok(roomQuiet.value === ACTING_ROOM_STILL, 'SH01 room chip English');
ok(doorway.value === ACTING_STANCE_DOORWAY && !/hesitat|weight|shift/i.test(doorway.value), 'stance is not body');

const sh01 = shot();
let beat = patchActingBeat(sh01, true, { emotion: 'uneasy', action: doorway.value });
beat = patchActingBeat({ ...sh01, actingBeat: beat }, true, { body: weight.value });
beat = patchActingBeat({ ...sh01, actingBeat: beat }, true, {
  gaze: 'Looks toward his mother, then lowers his gaze slightly; not at the camera.',
  room: roomQuiet.value,
});
ok(Boolean(beat.before.body), '01 V2 actingBeat accepts body');
ok(Boolean(beat.before.room), '02 V2 actingBeat accepts room');
ok(beat.before.body === 'He shifts his weight slightly and hesitates.', '03 body English persisted');
ok(beat.before.room === 'The living room behind him stays still; only he moves.', '03b room English persisted');
ok(beat.before.action === 'Standing at the living-room doorway.', 'stance doorway');
ok(beat.before.prop === 'He holds a sheet of paper with both hands.', 'prop kept');
ok(beat.before.gaze?.includes('lowers his gaze'), 'gaze kept');
ok(beat.before.holdSec === 0.2 && beat.after.holdSec === 0.22, 'holdSec unchanged');
ok(beat.during.speech === true && beat.during.emotion === 'uneasy', 'speech + uneasy');
ok(beat.during.intensity == null && beat.during.pace == null, 'no intensity/pace');

ok(card.includes('Hành động cơ thể') && card.includes('Không gian phía sau'), 'UI body/room');
ok(helper.includes('Hơi chuyển trọng tâm') && helper.includes('Phòng phía sau yên') && card.includes('ACTING_BODY_CHIPS'), 'UI SH01 chips');
ok(!card.includes(ACTING_BODY_WEIGHT) && helper.includes(ACTING_BODY_WEIGHT), '04 VI labels, English in helper');

const kfUrl = 'data:image/jpeg;base64,xxSH01KF';
const v2Shot = { ...sh01, actingBeat: beat, seconds: 5, editSeconds: 1.94, timing };
const state: SeriesPilotState = {
  roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
  runs: {
    'EP99-SC01-SH01': {
      status: 'approved',
      kfApproved: true,
      keyframeDataUrl: kfUrl,
      keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
      failedKfHash: 'h2b879d96:147815',
      failedPromptHash: 'hf56bc88:377',
    },
  },
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'v1' }],
  lines: [{ id: 'line-SC01-CHAR-001-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con tờ này.', sceneId: 'SC01' }],
  voiceAssets: { 'line-SC01-CHAR-001-1': { lineId: 'line-SC01-CHAR-001-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP99',
    title: 'TEST BATCH 3 SHORT',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [v2Shot],
  },
};

const persisted = slimPilotForStorage(state);
ok(persisted.episode?.shots[0]?.actingBeat?.before.body === ACTING_BODY_WEIGHT, '03c body survives slimPilotForStorage');
ok(persisted.episode?.shots[0]?.actingBeat?.before.room === ACTING_ROOM_STILL, '03d room survives slimPilotForStorage');

const compiled = compileI2vPrompt(state, v2Shot, v2Shot.story);
ok(/He shifts his weight slightly and hesitates/i.test(compiled), '05 body in compiled prompt');
ok(/The living room behind him stays still; only he moves/i.test(compiled), '06 room in compiled prompt');
ok(!/Subtle body movement, quiet tension/i.test(compiled), '07 generic body absent when body set');
ok(/Camera remains steady/i.test(compiled), '09 camera remains steady');
ok(/Blink and breathe/i.test(compiled), '10 Blink and breathe unchanged');
ok(!/do not |don't |no extra people/i.test(compiled), '11 no negative I2V language');
ok(!promptViolatesRunwayI2vLaw(compiled), '11b I2V law pass');
ok(!/Visual Authority|DNA|Master|wardrobe|3D_STYLIZED|polo|age 11|FAMIXA/i.test(compiled), '12 no Visual Authority');
ok(!/Hơi chuyển|Phòng phía sau|Không di chuyển|mong manh|Đứng ở cửa/.test(compiled), '04b no Vietnamese in prompt');
ok(/Standing at the living-room doorway/i.test(compiled), '04c hold body keeps stance, not walk-in');

const walkHoldShot: FamixaSeriesShot = {
  ...v2Shot,
  story: 'Minh bước vào nhà, đứng đối diện mẹ đang lau bàn.',
  motionPromptVi: 'Minh vừa vào nhà, đứng đối diện mẹ đang lau bàn.',
  actingBeat: {
    ...beat,
    before: {
      ...beat.before,
      action: 'Minh vừa vào nhà, đứng đối diện mẹ đang lau bàn. Mặt Minh hướng Linh.',
      body: 'He stays in place.',
    },
  },
};
const walkHold = compileI2vPrompt(state, walkHoldShot, walkHoldShot.story);
ok(/stays in place/i.test(walkHold), '17 hold body stays in compiled prompt');
ok(/Blink and breathe/i.test(walkHold) && !/continuous move/i.test(walkHold), '17b hold is not directed walk');
ok(!/vào nhà|bước vào|walk(?:s|ing)? in|enters?\b/i.test(walkHold), '17c hold drops enter locomotion');

const viHoldShot: FamixaSeriesShot = {
  ...walkHoldShot,
  actingBeat: {
    ...walkHoldShot.actingBeat!,
    before: {
      ...walkHoldShot.actingBeat!.before,
      action: 'Minh vừa vào nhà, đứng đối diện mẹ đang lau bàn. Mặt Minh hướng Linh.',
      body: 'Minh bước vào rồi đứng lại. Linh đứng ở bàn, không ra cửa.',
      gaze: 'Minh ngẩng nhìn Linh',
    },
  },
};
const viHold = compileI2vPrompt(state, viHoldShot, viHoldShot.story);
ok(/stays in place|adjusts his posture slightly/i.test(viHold), '18 VI hold body compiles English hold');
ok(/Blink and breathe/i.test(viHold) && !/continuous move/i.test(viHold), '18b VI hold is not directed walk');
ok(!/vào nhà|bước vào|walk(?:s|ing)? in|enters?\b/i.test(viHold), '18c VI hold drops enter locomotion');
ok(promptHashOf(viHold) !== promptHashOf(compiled), '18d VI hold opens a new prompt hash');

const unsavedWalk: FamixaSeriesShot = {
  ...walkHoldShot,
  actingBeat: {
    ...walkHoldShot.actingBeat!,
    before: {
      ...walkHoldShot.actingBeat!.before,
      action: 'Minh vừa vào nhà, đứng đối diện mẹ đang lau bàn.',
      body: '',
    },
  },
};
const unsavedHold = compileI2vPrompt(state, unsavedWalk, unsavedWalk.story);
ok(/stays in place/i.test(unsavedHold) && !/continuous move/i.test(unsavedHold), '19 acting walk-in without walk-body is hold');
ok(!/vào nhà|bước vào|walk(?:s|ing)? in|enters?\b/i.test(unsavedHold), '19b unsaved walk-in is not sent to Runway');
ok(i2vPromptIsEnglish(viHold) && i2vPromptIsEnglish(unsavedHold), '20 live VI acting fields do not reach Runway');
ok(!/ngẩng|bàn ăn|bỏ chữ|đối diện/i.test(viHold), '20b no Vietnamese gaze/room dump');
ok(
  sameFailedInput(
    {
      failedKfHash: 'h2b879d96:147815',
      failedPromptHash: promptHashOf(compiled),
      runwayAttempts: [
        {
          n: 4,
          at: '',
          status: 'FAILED',
          failureCode: 'INTERNAL.BAD_OUTPUT',
          promptHash: promptHashOf(compiled),
          taskId: 'walk-hold',
          source: { hash: 'h2b879d96:147815' },
        },
      ],
    },
    'h2b879d96:147815',
    promptHashOf(walkHold),
  ) === false,
  '17d sameFailedInput opens when hold drops walk-in',
);

const v1 = compileRunwayPromptV1({ action: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.' });
ok(/Subtle body movement/i.test(v1.text), '08 V1 fallback when body absent');

const newHash = promptHashOf(compiled);
ok(newHash !== 'hf56bc88:377', '13 prompt hash differs from SH01 n=3');
ok(
  sameFailedInput(
    {
      failedKfHash: 'h2b879d96:147815',
      failedPromptHash: 'hf56bc88:377',
      runwayAttempts: [
        {
          n: 3,
          at: '',
          status: 'FAILED',
          failureCode: 'INTERNAL.BAD_OUTPUT',
          promptHash: 'hf56bc88:377',
          taskId: '9ef1c551',
          source: { hash: 'h2b879d96:147815' },
        },
      ],
    },
    'h2b879d96:147815',
    newHash,
  ) === false,
  '13b sameFailedInput opens on new hash',
);

ok(state.runs['EP99-SC01-SH01']?.keyframeDataUrl === kfUrl, '14 KF unchanged');
ok(v2Shot.seconds === 5 && productionDurationOf(state, v2Shot) === 5, '15b seconds 5 / V3 production 5');

ok(!helper.includes('startContentSeriesTurbo') && !card.includes('fal-ai/') && !compiler.includes('eleven'), '16 no provider calls');
ok(!orch.includes('before.body') && !stamp.includes('before.room'), 'orch / fingerprints untouched');
ok(seriesMap.includes('holdBodyLine') && seriesMap.includes('runwayEnglishBit(beat.before.room'), 'compileI2vPrompt maps V2');

if (fail.length) {
  console.error(`FAMIXA_MOTION_CONTRACT_V2 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_MOTION_CONTRACT_V2 PASS FAIL=0 (no provider)');
console.log(`compiled=${compiled}`);
console.log(`newHash=${newHash}`);
