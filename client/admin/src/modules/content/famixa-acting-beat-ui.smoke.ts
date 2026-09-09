/** FAMIXA_ACTING_BEAT_UI_V1 — Director actingBeat editor. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, slimPilotForStorage, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';
import { promptHashOf, sameFailedInput } from './content-famixa-runway-pipe';
import {
  ACTING_ACTION_CHIPS,
  ACTING_EMOTION_VI,
  ACTING_GAZE_CHIPS,
  ACTING_PAPER_PROP_EN,
  ACTING_PAPER_PROP_VI,
  derivedPaperProp,
  patchActingBeat,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { applyTimingToShot, computeShotTiming } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');

const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const card = read('ContentFamixaShotProduction/ShotProductionActingBeatCard.tsx');
const helper = read('ContentFamixaShotProduction/ShotProductionActingBeat.ts');
const series = read('ContentFamixaSeriesTab.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const stamp = read('ContentFamixaShotProduction/ShotProductionStamp.ts');
const actions = read('ContentFamixaShotProduction/ShotProductionActions.ts');
const pipe = read('content-famixa-runway-pipe.ts');
const compiler = read('content-runway-prompt-v1.ts');
const law = read('content-famixa-acting-law.ts');
const studio = read('ContentFamixaStudioView.tsx');
const boards = read('ContentFamixaBuildBoards.tsx');
const laneB = read('ContentKitVideoDirectorProductionWorkspace.tsx');

ok(ws.includes('ShotProductionActingBeatCard') && card.includes('Diễn xuất') && card.includes('sẽ diễn thế nào?'), '01 actingBeat UI in Director Studio');
ok(card.includes('Cảm xúc') && card.includes('ACTING_EMOTIONS'), '01b emotion control');
ok(card.includes('Trước khi nói') && card.includes('Ánh mắt') && card.includes('Đạo cụ') && card.includes('Thoại'), '01c beat sections');
ok(!card.includes('holdSec') && !card.includes('intensity') && !card.includes('after.action') && !helper.includes('compileRunwayPromptV1'), '01d hidden fields');

const timing = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
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
  characterIds: ['CHAR-001'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  ...over,
});

const sh01 = shot();
const spoken = true;
const afterEmotion = patchActingBeat(sh01, spoken, { emotion: 'uneasy' });
ok(afterEmotion.during.emotion === 'uneasy', '02 emotion writes during.emotion');

const hesitate = ACTING_ACTION_CHIPS.find((c) => c.label === 'Hơi do dự')!;
const doorway = ACTING_ACTION_CHIPS.find((c) => c.label === 'Đứng ở cửa')!;
ok(doorway.value === 'Standing at the living-room doorway.', '03a doorway chip English');
ok(hesitate.value === doorway.value, '03b Hơi do dự is stance only, not body');
const afterAction = patchActingBeat(sh01, spoken, { action: hesitate.value });
ok(afterAction.before.action === 'Standing at the living-room doorway.', '03 action chip writes stance before.action');

const mom = ACTING_GAZE_CHIPS.find((c) => c.label === 'Nhìn mẹ')!;
const lower = ACTING_GAZE_CHIPS.find((c) => c.label === 'Hạ mắt rồi nhìn mẹ')!;
const nocam = ACTING_GAZE_CHIPS.find((c) => c.label === 'Không nhìn camera')!;
ok(mom.value === 'He looks toward his mother, not the camera.', '04a mom gaze English');
ok(nocam.value === 'not at the camera', '04b no-camera English');
const afterGaze = patchActingBeat(sh01, spoken, { gaze: lower.value });
ok(afterGaze.before.gaze === 'Looks toward his mother, then lowers his gaze slightly; not at the camera.', '04 gaze chip writes before.gaze');

ok(derivedPaperProp(sh01) === ACTING_PAPER_PROP_EN, '05 derived paper English');
ok(ACTING_PAPER_PROP_VI === 'Cầm tờ giấy bằng hai tay', '05b prop UI Vietnamese');
ok(afterAction.before.prop === ACTING_PAPER_PROP_EN, '05c patch keeps derived paper');

ok(afterEmotion.during.speech === true && afterAction.during.speech === true && afterGaze.during.speech === true, '06 speech true for spoken SH01');

ok(ACTING_EMOTION_VI.uneasy === 'mong manh' && law.includes("uneasy: 'mong manh'"), '07 VI emotion label mapped');
ok(!/[Đđ]ứng|[Hh]ơi|[Nn]hìn|[Hh]ạ/.test(hesitate.value + lower.value), '07b chips store English not Vietnamese');
ok(!card.includes(hesitate.value) || helper.includes(hesitate.value), '07c English values live in helper');

const targetBeat = patchActingBeat(shot({ actingBeat: afterEmotion }), spoken, {
  action: hesitate.value,
  gaze: lower.value,
});
ok(targetBeat.during.emotion === 'uneasy', '07d SH01 emotion uneasy');
ok(targetBeat.before.action === hesitate.value && targetBeat.before.gaze === lower.value, '07e SH01 action+gaze English');

const kfUrl = 'data:image/jpeg;base64,xxSH01KF';
const baseState = (row: FamixaSeriesShot): SeriesPilotState => ({
  roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
  runs: {
    'EP99-SC01-SH01': {
      status: 'approved',
      kfApproved: true,
      keyframeDataUrl: kfUrl,
      keyframeFileName: 'kf.jpg',
      takeUrl: 'https://take/old.mp4',
      previewUrl: 'https://take/old.mp4',
      turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
      failedKfHash: 'h2b879d96:147815',
      failedPromptHash: 'hd86e2dc8:346',
      runwayAttempts: [
        {
          n: 1,
          at: '2026-09-04T15:52:00.000Z',
          taskId: '106cedc3-f65c-4781-ad55-d2cc188bf17e',
          status: 'FAILED',
          failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
          promptHash: 'hd86e2dc8:346',
          source: { hash: 'h2b879d96:147815' },
        },
      ],
    },
  },
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'v1' }],
  lines: [{ id: 'line-SC01-CHAR-001-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con tờ này.', voiceId: 'v1' }],
  voiceAssets: { 'line-SC01-CHAR-001-1': { lineId: 'line-SC01-CHAR-001-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP99',
    title: 'TEST BATCH 3 SHORT',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [row],
  },
});

const persisted = slimPilotForStorage(
  baseState({
    ...sh01,
    actingBeat: targetBeat,
  }),
);
ok(persisted.episode?.shots[0]?.actingBeat?.before.action === hesitate.value, '08 actingBeat survives slimPilotForStorage');
ok(series.includes('onActingBeat') && series.includes('persistState') && /shots:\s*live\.episode\.shots\.map/.test(series), '08b persist via existing episode.shots patch');
ok(!series.includes('new store') && !helper.includes('fetch(') && !card.includes('/api/'), '08c no new store/API');

const beforeShot = shot();
const beforeState = baseState(beforeShot);
const beforePrompt = compileI2vPrompt(beforeState, beforeShot, beforeShot.story);
const afterShot = { ...sh01, actingBeat: targetBeat };
const afterState = baseState(afterShot);
const afterPrompt = compileI2vPrompt(afterState, afterShot, afterShot.story);
ok(beforePrompt !== afterPrompt, '09 compileI2vPrompt changes after actingBeat');
ok(/Standing at the living-room doorway/i.test(afterPrompt), '09b stance reaches compiled prompt');
ok(/lowers his gaze slightly/i.test(afterPrompt), '09c new gaze in compiled prompt');
ok(/Minh's performance is uneasy and contained|His performance is uneasy and contained/i.test(afterPrompt), '09d emotion reaches Runway English');
ok(!/mong manh|Hơi do dự|Hạ mắt|Đứng ở cửa/.test(afterPrompt), '09e no Vietnamese in Runway prompt');

const failedPrompt =
  'Standing at the living-room doorway. At the doorway, body turned toward the room. He holds a sheet of paper with both hands. He looks toward his mother, not the camera. His performance is natural, contained emotion. He begins speaking after a short beat. Subtle body movement, natural, contained emotion. Blink and breathe. Camera remains steady.';
const failedRun = beforeState.runs['EP99-SC01-SH01']!;
const failedHash = promptHashOf(failedPrompt);
const closed = sameFailedInput(
  { ...failedRun, failedPromptHash: failedHash, runwayAttempts: [{ ...failedRun.runwayAttempts![0]!, promptHash: failedHash }] },
  failedRun.failedKfHash,
  failedHash,
);
const opened = sameFailedInput(
  { ...failedRun, failedPromptHash: failedHash, runwayAttempts: [{ ...failedRun.runwayAttempts![0]!, promptHash: failedHash }] },
  failedRun.failedKfHash,
  promptHashOf(afterPrompt),
);
ok(closed === true, '10 same prompt still closed');
ok(opened === false, '10 changing actingBeat opens failedPromptHash circuit');
ok(promptHashOf(beforePrompt) !== promptHashOf(afterPrompt), '10b hashes differ naturally');

ok(afterState.runs['EP99-SC01-SH01']?.keyframeDataUrl === kfUrl, '11 KF unchanged');
ok(afterShot.seconds === 5 && afterShot.actingBeat?.before.holdSec === 0.2, '12 provider 5 / holdSec unchanged');
ok(targetBeat.before.holdSec === 0.2 && targetBeat.after.holdSec === 0.22, '12b holdSec preserved');

const locked = applyTimingToShot({ ...afterShot, actingBeat: targetBeat }, timing, { emotion: 'burst', intensity: 4 });
ok(locked.actingBeat?.during.emotion === 'uneasy' && locked.actingBeat?.before.action === hesitate.value, '12c Voice lock keeps Director beat');

ok(/Camera remains steady/i.test(afterPrompt), '13 camera remains steady');
ok(!/Camera eases|Camera holds, then/i.test(afterPrompt), '13b no retry camera rotate');

ok(orch.includes('export function nextShotProductionCommand') && orch.includes('CONFIRM_MOTION'), '14 orchestrator architecture present');
ok(!orch.includes('actingBeat') && !orch.includes('ShotProductionActingBeat'), '14b orchestrator untouched');
ok(!stamp.includes('actingBeat') && actions.includes('sameFailedInput'), '14c fingerprints / sameFailedInput not rewritten');
ok(pipe.includes('export function promptHashOf') && pipe.includes('export function sameFailedInput'), '14d pipe hashes untouched');
ok(series.includes('startShotMotion') && series.includes('forceNew: true'), '14e remake path unchanged');

const ui = `${ws}\n${card}\n${helper}`;
ok(
  !ui.includes('startContentSeriesTurbo') &&
    !ui.includes('generateContentSeriesStill') &&
    !ui.includes('eleven') &&
    !ui.includes('fal.run') &&
    !ui.includes('runwayml'),
  '15 no provider calls',
);
ok(!card.includes('generateSceneKf') && !card.includes('startSceneTurbo') && !card.includes('startLipsync'), '16 no media regeneration');

ok(series.includes('<ContentFamixaStudioView') && studio.includes('export function ContentFamixaStudioView'), '17 old Studio remains intact');
ok(boards.includes('ContentFamixaBuildVideoBoard') && series.includes('ContentFamixaBuildVoiceBoard'), '17b old boards mount');
ok(laneB.includes('ContentKitVideoDirectorProductionWorkspace') && !ws.includes('ContentKitVideoDirectorProductionWorkspace'), '18 Lane B intact / not wired');
ok(!compiler.includes('ShotProductionActingBeat') && compiler.includes('Blink and breathe.'), '18b compileRunwayPromptV1 + blink untouched');

const fps1 = computeInputFingerprints(beforeState, beforeShot);
const fps2 = computeInputFingerprints(afterState, afterShot);
ok(Boolean(fps1.motion && fps2.motion), '18c fingerprints still compute');
ok(typeof nextShotProductionCommand === 'function', '18d nextShotProductionCommand importable');

if (fail.length) {
  console.error(`FAMIXA_ACTING_BEAT_UI_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_ACTING_BEAT_UI_V1 PASS FAIL=0 (no provider)');
