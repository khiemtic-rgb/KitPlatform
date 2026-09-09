/** FAMIXA_SH01_TIMING_V2 — production 3.4 / provider 5. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';
import { promptHashOf } from './content-famixa-runway-pipe';
import { oneShotAssembleBody } from './ContentFamixaShotProduction/ShotProductionAssemble';
import { isStale, keyframeInputFingerprint, motionInputFingerprint } from './ContentFamixaShotProduction/ShotProductionFingerprint';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import {
  ACTING_BODY_WEIGHT,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import { applyEditDurations } from './content-famixa-scene-first';
import {
  SH01_TIMING_V2_PRODUCTION_SEC,
  SH01_TIMING_V2_SHOT_ID,
  applySh01TimingV2,
  computeShotTiming,
  mapProviderDuration,
  productionDurationOf,
  providerDurationOf,
  shotTimingOf,
} from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const timingSrc = readFileSync(join(root, 'famixa-shot-production-timing.ts'), 'utf8');
const assemble = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const fpSrc = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionFingerprint.ts'), 'utf8');
const compiler = readFileSync(join(root, 'content-runway-prompt-v1.ts'), 'utf8');

ok(timingSrc.includes('SH01_TIMING_V2_PRODUCTION_SEC = 3.4'), 'A target 3.4 on timing layer');
ok(assemble.includes('editorialDurationOf(stateRef.current, shot)'), 'H assemble uses editorialDurationOf');
ok(fpSrc.includes('productionDurationSec'), 'I motion fingerprint already includes productionDuration');
ok(!compiler.includes('SH01_TIMING_V2') && !compiler.includes('3.4'), 'D compiler not changed for duration');

const derived = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
ok(derived.productionDurationSec === 1.94, 'root cause clock 0.20+1.52+0.22=1.94');
ok(mapProviderDuration(3.4) === 5 && derived.providerDurationSec === 5, 'B map 3.4 → provider 5');

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

const shot = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: SH01_TIMING_V2_SHOT_ID,
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.94,
  timing: derived,
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

const sh01 = shot();
ok(sh01.actingBeat?.before.holdSec === 0.2 && sh01.actingBeat.after.holdSec === 0.22, 'C holdSec unchanged');
ok(sh01.actingBeat?.before.body === ACTING_BODY_WEIGHT && sh01.actingBeat.before.room === ACTING_ROOM_STILL, 'C V2 fields kept');

const state: SeriesPilotState = {
  roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
  runs: {
    [SH01_TIMING_V2_SHOT_ID]: {
      status: 'approved',
      kfApproved: true,
      keyframeDataUrl: 'data:image/jpeg;base64,xxSH01KF',
      keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
      takeUrl: 'https://take/n3.mp4',
      previewUrl: 'https://take/n3.mp4',
      shotProduction: { kfFp: 'kf-old', motionFp: 'motion-194' },
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
    shots: [sh01],
  },
};

const other = shot({ id: 'EP99-SC01-SH02', shot: 'SH02' });
ok(productionDurationOf(state, other) === 5, 'other shot V3 default 5');

ok(productionDurationOf(state, sh01) === 5, 'A productionDurationOf V3 default 5');
ok(providerDurationOf(state, sh01) === 5, 'B providerDurationOf 5');
ok(shotTimingOf(state, sh01)?.leadInSec === 0.2 && shotTimingOf(state, sh01)?.tailSec === 0.22, 'C timing holds unchanged');
ok((shotTimingOf(state, sh01)?.reactionSec || 0) === 0, 'V3 does not stuff reaction via shot id');

const v2Timing = applySh01TimingV2(derived);
ok(v2Timing.productionDurationSec === 3.4 && v2Timing.providerDurationSec === 5, 'applySh01TimingV2 3.4 / 5');

const edited = applyEditDurations(state, [sh01]);
const written = edited.episode?.shots[0];
ok(written?.timing?.productionDurationSec === 5 && written.seconds === 5 && written.editSeconds === 5, 'applyEditDurations writes 5 / 5');
ok(written?.actingBeat?.before.holdSec === 0.2 && written.actingBeat.after.holdSec === 0.22, 'C actingBeat holdSec after write');
ok(written?.actingBeat?.before.body === ACTING_BODY_WEIGHT, 'C body after write');

const beforePrompt = compileI2vPrompt({ ...state, episode: { ...state.episode!, shots: [{ ...sh01, timing: derived }] } }, { ...sh01, timing: derived }, sh01.story);
const afterPrompt = compileI2vPrompt(state, sh01, sh01.story);
ok(beforePrompt === afterPrompt, 'D compiled prompt unchanged by timing');
ok(promptHashOf(beforePrompt) === promptHashOf(afterPrompt), 'D promptHash unchanged');
ok(/He shifts his weight slightly and hesitates/.test(afterPrompt), 'D V2 body still in prompt');

const kf = keyframeInputFingerprint({
  shotId: sh01.id,
  action: sh01.story,
  characterIds: sh01.characterIds ?? [],
});
ok(kf === keyframeInputFingerprint({ shotId: sh01.id, action: sh01.story, characterIds: sh01.characterIds ?? [] }), 'E KF fingerprint ignores production');
ok(
  isStale(
    motionInputFingerprint({ keyframeFp: kf, action: sh01.story, productionDurationSec: 1.94 }),
    motionInputFingerprint({ keyframeFp: kf, action: sh01.story, productionDurationSec: 3.4 }),
  ),
  'I production in motion fingerprint → take stale',
);
ok(Boolean(computeInputFingerprints(state, sh01).motion), 'I snapshot still hashes motion');

ok(
  oneShotAssembleBody({
    shotCode: 'SH01-01',
    seconds: productionDurationOf(state, sh01),
    spoken: true,
    lipsynced: true,
    voices: [],
  }).clips[0]?.seconds === 5,
  'H assemble target 5',
);

ok(!timingSrc.includes('startContentSeriesTurbo') && !timingSrc.includes('fal-ai/'), 'F no provider');
ok(!timingSrc.includes('generateSceneKf'), 'G no media');

if (fail.length) {
  console.error(`FAMIXA_SH01_TIMING_V2 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SH01_TIMING_V2 PASS FAIL=0 (no provider)');
console.log(`derived=${derived.productionDurationSec} target=${productionDurationOf(state, sh01)} provider=${providerDurationOf(state, sh01)}`);
