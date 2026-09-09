/** FAMIXA_SH01_ACTING_BEAT_REMAKE_GATE_FIX_V1 — remake eligibility only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { kfIsApproved } from './content-famixa-batch-plan';
import { shotI2vPromptHash } from './content-famixa-prod-v2';
import { dataUriHash, lastGenerationFail, promptHashOf, sameFailedInput } from './content-famixa-runway-pipe';
import {
  compileI2vPrompt,
  i2vActionOf,
  shotHasValidAction,
  shotRunOf,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';
import { resolveTakeUrl } from './content-famixa-final-source';
import { studioMotionSendOpts } from './ContentFamixaShotProduction/ShotProductionCta';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const remakeStart = series.indexOf('if (opts?.remake');
const remakeBlock = series.slice(remakeStart, series.indexOf('if (onlyIds?.length === 1)', remakeStart));

const KF = 'data:image/jpeg;base64,xxSH01KF';
const LIVE_FAILED_PROMPT =
  'Standing at the living-room doorway. At the doorway, body turned toward the room. He holds a sheet of paper with both hands. He looks toward his mother, not the camera. His performance is natural, contained emotion. He begins speaking after a short beat. Subtle body movement, natural, contained emotion. Blink and breathe. Camera remains steady.';

const actingBeat = {
  after: { holdSec: 0.22 },
  before: {
    gaze: 'Looks toward his mother, then lowers his gaze slightly; not at the camera.',
    prop: 'He holds a sheet of paper with both hands.',
    action: 'Standing at the living-room doorway, hesitates briefly.',
    holdSec: 0.2,
  },
  during: { speech: true, emotion: 'uneasy' },
};

const shotOf = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '10s',
  seconds: 5,
  editSeconds: 1.7,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001', 'CHAR-003'],
  characterIds: ['CHAR-001', 'CHAR-003'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  actingBeat,
  ...over,
});

function remakeEligible(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const run = shotRunOf(state, shot);
  if (!run.keyframeDataUrl || !kfIsApproved(run) || run.prodSkip || !shotHasValidAction(shot, run)) return false;
  const promptHash = shotI2vPromptHash(state, shot, run);
  if (sameFailedInput(run, dataUriHash(run.keyframeDataUrl), promptHash)) return false;
  if (resolveTakeUrl(run)) return true;
  return Boolean(lastGenerationFail(run));
}

function stateOf(shot: FamixaSeriesShot, run: SeriesShotRun, extra?: FamixaSeriesShot): SeriesPilotState {
  const shots = extra ? [shot, extra] : [shot];
  return {
    roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
    runs: Object.fromEntries(shots.map((s) => [s.id, s.id === shot.id ? run : { status: 'story_locked' as const }])),
    characters: [
      { id: 'CHAR-001', name: 'Minh', voiceId: 'v1' },
      { id: 'CHAR-003', name: 'Linh', voiceId: 'v2' },
    ],
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
      shots,
    },
  };
}

const failedAttempt = (promptHash: string, kfHash: string) => ({
  n: 2,
  at: '2026-09-04T15:52:00.000Z',
  taskId: '106cedc3-f65c-4781-ad55-d2cc188bf17e',
  status: 'FAILED',
  failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
  promptHash,
  source: { hash: kfHash },
  kf: { hash: kfHash },
});

const sh01 = shotOf();
const liveState = stateOf(sh01, {
  status: 'turbo_testing',
  kfApproved: true,
  keyframeDataUrl: KF,
  keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
  previewUrl: undefined,
  takeUrl: 'https://take/old.mp4',
  lipsyncUrl: 'https://fal/lip.mp4',
  lipsynced: true,
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  turboStatus: 'FAILED',
  turboTaskId: '106cedc3-f65c-4781-ad55-d2cc188bf17e',
  failedKfHash: 'h2b879d96:147815',
  failedPromptHash: 'hd86e2dc8:346',
  runwayAttempts: [failedAttempt('hd86e2dc8:346', 'h2b879d96:147815')],
});
const liveAction = i2vActionOf(liveState, sh01, liveState.runs[sh01.id]);
const livePrompt = compileI2vPrompt(liveState, sh01, liveAction);
const liveHash = promptHashOf(livePrompt);
ok(/Minh's performance is uneasy and contained|His performance is uneasy and contained/i.test(livePrompt), 'live current prompt is Minh-aware');
ok(liveHash !== 'hd86e2dc8:346', 'live currentPromptHash differs from failed hd86e2dc8:346');
ok(promptHashOf(LIVE_FAILED_PROMPT) === 'hd86e2dc8:346', 'live failedPromptHash hd86e2dc8:346');
ok(
  sameFailedInput(liveState.runs[sh01.id], 'h2b879d96:147815', liveHash) === false,
  'live sameFailedInput OPEN',
);

const sameKf = dataUriHash(KF);
const openRun: SeriesShotRun = {
  status: 'turbo_testing',
  kfApproved: true,
  keyframeDataUrl: KF,
  keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
  previewUrl: undefined,
  takeUrl: 'https://take/old.mp4',
  lipsyncUrl: 'https://fal/lip.mp4',
  turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
  turboStatus: 'FAILED',
  turboTaskId: '106cedc3-f65c-4781-ad55-d2cc188bf17e',
  failedKfHash: sameKf,
  failedPromptHash: 'hd86e2dc8:346',
  runwayAttempts: [failedAttempt('hd86e2dc8:346', sameKf)],
};
const openState = stateOf(sh01, openRun);
ok(sameFailedInput(openRun, dataUriHash(KF), shotI2vPromptHash(openState, sh01, openRun)) === false, '01 circuit OPEN');
ok(remakeEligible(openState, sh01) === true, '01 SH01 new prompt + null previewUrl ALLOW_ONE_NEW_JOB');
ok(!openRun.previewUrl && Boolean(openRun.takeUrl), '01b previewUrl null / takeUrl exists');

const closedHash = shotI2vPromptHash(openState, sh01, openRun);
const closedRun: SeriesShotRun = {
  ...openRun,
  failedPromptHash: closedHash,
  runwayAttempts: [failedAttempt(closedHash, sameKf)],
};
const closedState = stateOf(sh01, closedRun);
ok(sameFailedInput(closedRun, dataUriHash(KF), shotI2vPromptHash(closedState, sh01, closedRun)) === true, '02 circuit CLOSED');
ok(remakeEligible(closedState, sh01) === false, '02 same failed prompt BLOCK');

const staleRun: SeriesShotRun = {
  status: 'approved',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: KF,
  previewUrl: 'https://take/ready.mp4',
  takeUrl: 'https://take/ready.mp4',
};
const staleState = stateOf(sh01, staleRun);
ok(remakeEligible(staleState, sh01) === true, '03 successful previewUrl remake ALLOW');

const firstRun: SeriesShotRun = {
  status: 'reviewed',
  kfApproved: true,
  keyframeDataUrl: KF,
};
const firstState = stateOf(sh01, firstRun);
const firstSnap = buildShotProductionSnapshot({
  state: firstState,
  shot: sh01,
  ttsFiles: { 'line-SC01-CHAR-001-1': { url: 'blob:minh', fileName: 'minh.mp3' } },
});
ok(remakeEligible(firstState, sh01) === false, '04 first-time remake list excludes no-take');
ok(nextShotProductionCommand(firstSnap).type === 'CONFIRM_MOTION' && !studioMotionSendOpts(firstSnap).remake, '04b first-time still non-remake');

const other = shotOf({ id: 'EP99-SC01-SH02', shot: 'SH02', story: 'Mẹ nhìn tờ giấy.' });
const two = stateOf(sh01, openRun, other);
const onlySh01 = [sh01, other].filter((s) => ['EP99-SC01-SH01'].includes(s.id) && remakeEligible(two, s));
ok(onlySh01.length === 1 && onlySh01[0]?.id === 'EP99-SC01-SH01', '05 onlyIds SH01 does not submit SH02');
ok(remakeBlock.includes('onlyIds.includes(s.id)'), '05b remake keeps onlyIds filter');

ok(remakeBlock.includes('forceNew: true'), '06 remake send keeps forceNew');
ok(!remakeBlock.includes('generateSceneKf') && !remakeBlock.includes('ensureShotVoice'), '07 no KF / voice regen');
ok(
  remakeBlock.includes('sameFailedInput') &&
    remakeBlock.includes('lastGenerationFail') &&
    remakeBlock.includes('if (resolveTakeUrl(run)) return true'),
  'gate uses resolveTakeUrl + current-input checks',
);
ok(!remakeBlock.includes('failedKfHash: undefined') && !remakeBlock.includes('failedPromptHash: undefined'), 'no hash wipe');
ok(
  !remakeBlock.includes('startContentSeriesTurbo') &&
    !remakeBlock.includes('fal-ai/') &&
    !remakeBlock.includes('eleven'),
  '09 no provider in remake gate',
);
ok(series.includes('startShotMotion') && series.includes('forceNew: true'), '10 Studio remake wiring kept');
ok(
  series.includes("action: 'He stays in place.'") &&
    series.includes('holdRetry <= maxCameraShift') &&
    series.includes('usedPrompt(holdFallback)'),
  '12 hold remake cycles unused camera lines before Tạo hình mới',
);
ok(series.includes('ignoreCircuit: remakeTake'), '11 remake Confirm does not precheck-block sameFailedInput');

if (fail.length) {
  console.error(`FAMIXA_SH01_ACTING_BEAT_REMAKE_GATE_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_ACTING_BEAT_REMAKE_GATE_FIX_V1 PASS FAIL=0 (no provider)');
