/** FAMIXA_SH01_ACTING_BEAT_PROMPT_FRESHNESS_V1 — snapshot freshness only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from './content-famixa-series';
import { promptHashOf } from './content-famixa-runway-pipe';
import { shotI2vPromptHash } from './content-famixa-prod-v2';
import {
  ACTING_BODY_WEIGHT,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import { directorPrimaryCta, studioMotionSendOpts } from './ContentFamixaShotProduction/ShotProductionCta';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import {
  buildShotProductionSnapshot,
  lastTakePromptHashOf,
  motionPromptFreshnessStale,
} from './ContentFamixaShotProduction/ShotProductionState';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { computeShotTiming } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const stateSrc = read('ContentFamixaShotProduction/ShotProductionState.ts');
const fpSrc = read('ContentFamixaShotProduction/ShotProductionFingerprint.ts');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const series = read('ContentFamixaSeriesTab.tsx');

ok(stateSrc.includes('shotI2vPromptHash') && stateSrc.includes('motionPromptFreshnessStale'), 'uses existing I2V hash');
ok(!fpSrc.includes('shotI2vPromptHash') && !fpSrc.includes('actingBeat'), 'motionInputFingerprint untouched');
ok(!orch.includes('shotI2vPromptHash') && !orch.includes('motionPromptFreshnessStale'), 'orchestrator untouched');
ok(series.includes('startShotMotion') && series.includes('forceNew: true'), 'remake path kept');

const timing = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
const v1Beat = {
  before: {
    action: 'Standing at the living-room doorway, hesitates briefly.',
    prop: 'He holds a sheet of paper with both hands.',
    gaze: 'Looks toward his mother, then lowers his gaze slightly; not at the camera.',
    holdSec: 0.2,
  },
  during: { speech: true, emotion: 'uneasy' as const },
  after: { holdSec: 0.22 },
};
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

const shot = (beat: typeof v1Beat): FamixaSeriesShot => ({
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
  actingBeat: beat,
});

const takeUrl = 'https://take/n3.mp4';
const kfUrl = 'data:image/jpeg;base64,xxSH01KF';
const takeRun = (over: Partial<SeriesShotRun> = {}): SeriesShotRun => ({
  status: 'approved',
  kfApproved: true,
  videoApproved: true,
  keyframeDataUrl: kfUrl,
  keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
  takeUrl,
  previewUrl: takeUrl,
  failedKfHash: 'h2b879d96:147815',
  failedPromptHash: 'hf56bc88:377',
  runwayAttempts: [
    {
      n: 3,
      at: '',
      status: 'SUCCEEDED',
      promptHash: 'hf56bc88:377',
      taskId: '9ef1c551',
      source: { hash: 'h2b879d96:147815' },
    },
  ],
  ...over,
});

const stateOf = (row: FamixaSeriesShot, run: SeriesShotRun): SeriesPilotState => ({
  roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
  runs: { 'EP99-SC01-SH01': run },
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'v1' }],
  lines: [
    {
      id: 'line-SC01-CHAR-001-1',
      characterId: 'CHAR-001',
      text: 'Mẹ xem giúp con tờ này.',
      sceneId: 'SC01',
      performance: { emotion: 'uneasy', intensity: 2, label: 'mong manh 2' },
    },
  ],
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

const v1 = shot(v1Beat);
const v2 = shot(v2Beat);
const baseRun = takeRun();
const s1 = stateOf(v1, baseRun);
const s2 = stateOf(v2, baseRun);
const fps1 = computeInputFingerprints(s1, v1);
const fps2 = computeInputFingerprints(s2, v2);
const v1Hash = promptHashOf(compileI2vPrompt(s1, v1, v1.story));
const matching = takeRun({
  runwayAttempts: [
    {
      n: 3,
      at: '',
      status: 'SUCCEEDED',
      promptHash: v1Hash,
      taskId: '9ef1c551',
      source: { hash: 'h2b879d96:147815' },
    },
  ],
  shotProduction: { motionFp: fps1.motion, kfFp: fps1.keyframe, voiceFp: fps1.voice },
});
const tts = { 'line-SC01-CHAR-001-1': { url: 'blob:minh', fileName: 'minh.mp3' } };
const snap1 = buildShotProductionSnapshot({ state: stateOf(v1, matching), shot: v1, ttsFiles: tts });
const snap2 = buildShotProductionSnapshot({ state: stateOf(v2, matching), shot: v2, ttsFiles: tts });

ok(Boolean(fps1.motion) && fps1.motion === fps2.motion, 'motionFp unchanged by actingBeat (timing already in fp)');
ok(lastTakePromptHashOf(baseRun) === 'hf56bc88:377', 'historical n=3 His/He hash kept on live take');
ok(/Minh's performance is uneasy and contained|His performance is uneasy and contained/i.test(compileI2vPrompt(s1, v1, v1.story)), 'V1 compile is Minh-aware');
ok(/Minh's performance is uneasy and contained|His performance is uneasy and contained/i.test(compileI2vPrompt(s2, v2, v2.story)), 'V2 compile is Minh-aware');
ok(shotI2vPromptHash(s2, v2, matching) !== v1Hash, 'V2 current hash differs from matching V1 take');
ok(lastTakePromptHashOf(matching) === v1Hash, 'latest take promptHash');
ok(snap1.motionStale === false && snap1.motionReady === true, '08 matching prompt remains READY');
ok(snap2.motionStale === true && snap2.motionReady === false, '01 V2 actingBeat makes motion STALE');
ok(snap2.keyframeApproved === true && snap2.stamp?.kfFp === fps1.keyframe, '03 KF remains approved');
ok(snap2.input.hasMuteTake === true && matching.takeUrl === takeUrl, '04 take remains stored');
ok(nextShotProductionCommand(snap2).type === 'ACCEPT_EXISTING', 'ACCEPT_EXISTING after V2 stale take');
ok(directorPrimaryCta(snap2).label === 'Dùng video này' && directorPrimaryCta(snap2).action === 'accept-existing', '02 CTA Dùng video này');
ok(!studioMotionSendOpts(snap2).remake, '06 remake is not the primary stale CTA');
ok(studioMotionSendOpts(snap1).remake !== true, 'matching prompt does not remake');

const noHashRun = takeRun({ runwayAttempts: [{ n: 3, at: '', status: 'SUCCEEDED', taskId: '9ef1c551' }], shotProduction: matching.shotProduction });
const snapNoHash = buildShotProductionSnapshot({ state: stateOf(v2, noHashRun), shot: v2, ttsFiles: tts });
ok(lastTakePromptHashOf(noHashRun) === '', '09 no historical promptHash');
ok(snapNoHash.motionStale === false && snapNoHash.motionReady === true, '09 absent promptHash does not auto-stale');
ok(!motionPromptFreshnessStale({ hasMuteTake: true, lastTakePromptHash: '', currentPromptHash: 'hedf22d6f:421' }), '09b helper empty last');

ok(!stateSrc.includes('startContentSeriesTurbo') && !stateSrc.includes('fal-ai/') && !stateSrc.includes('eleven'), '05 no provider in snapshot');
ok(snap2.fps.motion === snap1.fps.motion, 'fingerprint helper still equal');

if (fail.length) {
  console.error(`FAMIXA_SH01_ACTING_BEAT_PROMPT_FRESHNESS_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_ACTING_BEAT_PROMPT_FRESHNESS_V1 PASS FAIL=0 (no provider)');
console.log(`storedMotionFp=${snap2.stamp?.motionFp}`);
console.log(`currentMotionFp=${snap2.motionFingerprint}`);
console.log(`lastTake=${lastTakePromptHashOf(matching)}`);
console.log(`currentPrompt=${shotI2vPromptHash(s2, v2, matching)}`);
console.log(`motionStale=${snap2.motionStale} motionReady=${snap2.motionReady}`);
console.log(`cmd=${nextShotProductionCommand(snap2).type}`);
console.log(`cta=${directorPrimaryCta(snap2).label}`);
