/** FAMIXA_SHOT_TIMING_CONTRACT_V3 — T1–T14 + synthetic. 0 providers. 0 persist. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { applyEditDurations } from './content-famixa-scene-first';
import type { FamixaSeriesShot, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import { normalizeLipsyncSyncMode } from './content-famixa-prod-v2';
import { oneShotAssembleBody } from './ContentFamixaShotProduction/ShotProductionAssemble';
import { isStale, lipsyncInputFingerprint, mixInputFingerprint } from './ContentFamixaShotProduction/ShotProductionFingerprint';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import {
  DOCUMENT_ID,
  TIMING_BLOCK,
  assembleDurationOf,
  dialogueFloorOf,
  speechCutPlayableSec,
  editorialDurationOf,
  falInputDurationOf,
  performanceDurationOf,
  productionDurationOf,
  timingDurationBlockReason,
} from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const timingSrc = readFileSync(join(root, 'famixa-shot-production-timing.ts'), 'utf8');
const sceneFirstSrc = readFileSync(join(root, 'content-famixa-scene-first.ts'), 'utf8');
const turboSrc = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'),
  'utf8',
);
const tabSrc = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const stampSrc = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionStamp.ts'), 'utf8');

const shot = (partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot => ({
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH',
  clock: '5s',
  seconds: 5,
  story: 'Minh bước tới bàn ăn, đặt tờ giấy xuống, đứng im.',
  visual: 'Minh bước tới bàn ăn, đặt tờ giấy xuống, đứng im.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001'],
  location: 'PHÒNG KHÁCH - TỐI',
  motionPrompt: '',
  motionPromptVi: 'Minh bước tới bàn ăn, đặt tờ giấy xuống, đứng im.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-1'],
  actingBeat: {
    before: { holdSec: 0.2, action: 'Standing.' },
    during: { speech: true },
    after: { holdSec: 0.22 },
  },
  ...partial,
});

const run = (over: Partial<SeriesShotRun> = {}): SeriesShotRun => ({
  status: 'turbo_testing',
  takeUrl: 'https://take/mute.mp4',
  runwayAttempts: [{ n: 2, at: '', status: 'SUCCEEDED', duration: 5, outputUrl: 'https://take/mute.mp4' }],
  ...over,
});

const stateOf = (row: FamixaSeriesShot, voiceSec = 0.64, runOver?: SeriesShotRun): SeriesPilotState => ({
  roles: [{ id: 'role-1', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'v1' }],
  lines: [{ id: 'line-1', characterId: 'CHAR-001', text: 'Dạ mẹ.', voiceId: 'v1' }],
  voiceAssets: { 'line-1': { lineId: 'line-1', duration: voiceSec, status: 'ready', characterId: 'CHAR-001' } },
  runs: { [row.id]: runOver ?? run() },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP99',
    title: 'TIMING V3',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [row],
  },
});

ok(DOCUMENT_ID === 'FAMIXA_SHOT_TIMING_CONTRACT_V3', 'document id V3');

const t1 = shot({ id: 'SYN-T1' });
const s1 = stateOf(t1, 0.64);
ok(performanceDurationOf(s1, t1) === 5, 'T1 performance 5');
ok(productionDurationOf(s1, t1) === 5, 'T1 production 5');
ok(falInputDurationOf(s1, t1) === 5, 'T1 Fal input 5');
ok(assembleDurationOf(s1, t1) === 5, 'T1 assemble 5');
ok(dialogueFloorOf(s1, t1) === 1.06, 'T1 floor 1.06');
ok(speechCutPlayableSec(s1, t1, 5) === 1.06, 'T1 speech-cut trims to floor');
ok(!timingDurationBlockReason(s1, t1), 'T1 no block');

const t2 = shot({ id: 'SYN-T2' });
const s2 = stateOf(t2, 2);
ok(performanceDurationOf(s2, t2) === 5 && productionDurationOf(s2, t2) === 5 && falInputDurationOf(s2, t2) === 5, 'T2 voice 2 keep 5');

const silent = shot({ id: 'SYN-T3', dialogueSegmentIds: [] });
const s3: SeriesPilotState = { ...stateOf(silent, 0), voiceAssets: {}, lines: [] };
ok(performanceDurationOf(s3, silent) === 5 && productionDurationOf(s3, silent) === 5, 'T3 silent production 5');
ok(speechCutPlayableSec(s3, silent, 5) === 5, 'T3 silent speech-cut keeps full take');

const t4 = shot({
  id: 'SYN-T4',
  actingBeat: { before: { holdSec: 0.2 }, during: { speech: true }, after: { holdSec: 2 } },
});
const s4 = stateOf(t4, 0.64);
ok(dialogueFloorOf(s4, t4) === 2.84, 'T4 floor 2.84');
ok(productionDurationOf(s4, t4) === 5 && falInputDurationOf(s4, t4) === 5, 'T4 production/Fal 5');

const t5 = shot({
  id: 'SYN-T5',
  timing: {
    leadInSec: 0.2,
    tailSec: 0.22,
    reactionSec: 0,
    interCueGapSec: 0.1,
    voiceDurationSec: 1.52,
    productionDurationSec: 3.4,
    providerDurationSec: 5,
    speechStartSec: 0.2,
    speechEndSec: 1.72,
    performanceDurationSec: 5,
    source: 'DIRECTOR',
  },
});
const s5 = stateOf(t5, 1.52);
ok(falInputDurationOf(s5, t5) === 5, 'T5 Fal input 5');
ok(assembleDurationOf(s5, t5) === 3.4 && editorialDurationOf(s5, t5) === 3.4, 'T5 assemble 3.4');
ok(
  oneShotAssembleBody({
    shotCode: 'SH-T5',
    seconds: assembleDurationOf(s5, t5),
    spoken: true,
    lipsynced: true,
    voices: [],
  }).clips[0]?.seconds === 3.4,
  'T5 assemble body 3.4',
);
ok(!timingDurationBlockReason(s5, t5), 'T5 3.4 valid editorial');

const t6 = shot({
  id: 'SYN-T6',
  timing: {
    leadInSec: 0.2,
    tailSec: 0.22,
    reactionSec: 0,
    interCueGapSec: 0.1,
    voiceDurationSec: 0.64,
    productionDurationSec: 1.06,
    providerDurationSec: 5,
    speechStartSec: 0.2,
    speechEndSec: 0.84,
    performanceDurationSec: 5,
    source: 'DIRECTOR',
  },
});
const s6 = stateOf(t6, 0.64);
ok(dialogueFloorOf(s6, t6) === 1.06, 'T6 floor 1.06');
ok(editorialDurationOf(s6, t6) === 1.06 && productionDurationOf(s6, t6) === 1.06, 'T6 director 1.06 accepted');
ok(falInputDurationOf(s6, t6) === 5, 'T6 Fal still 5');
ok(productionDurationOf(s1, t1) !== 1.06, 'T6 1.06 is not default');

const t7 = shot({
  id: 'SYN-T7',
  timing: {
    leadInSec: 0.2,
    tailSec: 0.22,
    reactionSec: 0,
    interCueGapSec: 0.1,
    voiceDurationSec: 1.52,
    productionDurationSec: 1,
    providerDurationSec: 5,
    speechStartSec: 0.2,
    speechEndSec: 1.72,
    performanceDurationSec: 5,
    source: 'DIRECTOR',
  },
});
const s7 = stateOf(t7, 1.52);
ok(timingDurationBlockReason(s7, t7) === TIMING_BLOCK.PRODUCTION_SHORTER_THAN_DIALOGUE, 'T7 production < dialogueEnd BLOCK');

const t8 = shot({
  id: 'SYN-T8',
  timing: {
    leadInSec: 0.2,
    tailSec: 0.22,
    reactionSec: 0,
    interCueGapSec: 0.1,
    voiceDurationSec: 0.64,
    productionDurationSec: 6,
    providerDurationSec: 5,
    speechStartSec: 0.2,
    speechEndSec: 0.84,
    performanceDurationSec: 5,
    source: 'DIRECTOR',
  },
});
const s8 = stateOf(t8, 0.64);
ok(timingDurationBlockReason(s8, t8) === TIMING_BLOCK.PRODUCTION_LONGER_THAN_PERFORMANCE, 'T8 production > take BLOCK');

const t9 = shot({
  id: 'SYN-T9',
  actingBeat: { before: { holdSec: 0.2 }, during: { speech: true }, after: { holdSec: 2 } },
});
const s9 = stateOf(t9, 0.64);
const edited9 = applyEditDurations(s9, [t9]);
const written9 = edited9.episode?.shots[0];
ok(written9?.timing?.productionDurationSec === 5, 'T9 applyEditDurations production stays 5');
ok(written9?.editSeconds === 5, 'T9 editSeconds 5');
ok(written9?.actingBeat?.after.holdSec === 2, 'T9 after.holdSec 2 kept');
ok(written9?.timing?.source === 'DEFAULT_TAKE', 'T9 source DEFAULT_TAKE');
ok(dialogueFloorOf(edited9, written9!) === 2.84, 'T9 floor still 2.84');

const legacyVoiceStamp = shot({
  id: 'SYN-T10',
  timing: {
    leadInSec: 0.2,
    tailSec: 0.22,
    reactionSec: 0,
    interCueGapSec: 0.1,
    voiceDurationSec: 0.64,
    productionDurationSec: 1.06,
    providerDurationSec: 5,
    speechStartSec: 0.2,
    speechEndSec: 0.84,
  },
});
const s10 = stateOf(legacyVoiceStamp, 0.64);
ok(productionDurationOf(s10, legacyVoiceStamp) === 5, 'T10 legacy voice-clock stamp ignored');
ok(!timingSrc.includes('if (timing && isSh01TimingV2Shot'), 'T10 no SH01 apply in shotTimingOf');
ok(!sceneFirstSrc.includes('isSh01TimingV2Shot'), 'T10 applyEditDurations no shot-id');

ok(!/if \(request\.ProductionDurationSec is > 0\)\s+takeBytes = TrimVideoToDuration/.test(turboSrc), 'T11 no pre-Fal production trim');
ok(turboSrc.includes('full performance take'), 'T11 Fal comment full take');
ok(tabSrc.includes('performanceDurationSec: performanceDurationOf'), 'T11 client sends performance');
ok(normalizeLipsyncSyncMode(undefined) === 'silence', 'T11 default sync silence');

const fpsA = computeInputFingerprints(s1, t1);
const fpsEditorial = computeInputFingerprints(s5, t5);
ok(isStale(fpsA.mix, fpsEditorial.mix), 'T12 editorial change stales mix');
ok(fpsA.lipsync !== '' && fpsEditorial.lipsync !== '', 'T12 lipsync fps exist');
const voiceChanged = stateOf(t1, 0.3);
ok(productionDurationOf(voiceChanged, t1) === 5, 'T12 voice change does not shrink production');
ok(
  mixInputFingerprint({ lipsyncFp: 'a', voiceFp: 'b', motionFp: 'c', productionDurationSec: 5 }) !==
    mixInputFingerprint({ lipsyncFp: 'a', voiceFp: 'b', motionFp: 'c', productionDurationSec: 3.4 }),
  'T12 mix includes editorial',
);
ok(
  lipsyncInputFingerprint({ voiceFp: 'a', motionFp: 'b', performanceDurationSec: 5 }) ===
    lipsyncInputFingerprint({ voiceFp: 'a', motionFp: 'b', productionDurationSec: 5 }),
  'T12 lipsync hashes performance',
);
ok(stampSrc.includes('performanceDurationOf') && stampSrc.includes('editorialDurationOf'), 'T12 stamp uses both clocks');

const oldLip = lipsyncInputFingerprint({ voiceFp: 'v', motionFp: 'm', performanceDurationSec: 1.06 });
const newLip = lipsyncInputFingerprint({ voiceFp: 'v', motionFp: 'm', performanceDurationSec: 5 });
ok(isStale(oldLip, newLip), 'T13 old 1.06 lipsync stale vs full take');

const t14 = shot({
  id: 'EP99-SC01-SH99',
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  actingBeat: {
    before: { holdSec: 0.2, action: 'Standing at the living-room doorway.' },
    during: { speech: true, emotion: 'uneasy' },
    after: { holdSec: 0.22 },
  },
});
const s14 = stateOf(t14, 1.52);
ok(productionDurationOf(s14, t14) === 5, 'T14 SH01-shaped other id default 5');
ok(productionDurationOf(s14, t14) !== 3.4, 'T14 no special-case 3.4');
ok(!timingSrc.includes("shot.id === SH01_TIMING_V2_SHOT_ID") || timingSrc.includes('Duration authority must not'), 'T14 id helper is compat only');

const syn = shot({
  id: 'SYN-ADD',
  actingBeat: { before: { holdSec: 0.2 }, during: { speech: true }, after: { holdSec: 2 } },
});
const synState = stateOf(syn, 0.6);
ok(performanceDurationOf(synState, syn) === 5, 'SYN performance 5');
ok(dialogueFloorOf(synState, syn) === 2.8, 'SYN floor 2.80 (0.20+0.60+2.00)');
ok(productionDurationOf(synState, syn) === 5, 'SYN production default 5');
ok(falInputDurationOf(synState, syn) === 5, 'SYN Fal 5');
ok(assembleDurationOf(synState, syn) === 5, 'SYN assemble 5');
ok(productionDurationOf(synState, syn) !== 2.82 && productionDurationOf(synState, syn) !== 1.06, 'SYN not 2.82/1s');

const fixtureIds = ['EP99-SC01-SH01', 'EP99-SC01-SH02', 'EP99-SC01-SH03'];
for (const id of fixtureIds) {
  const row = shot({ id });
  const st = stateOf(row, 0.64);
  ok(productionDurationOf(st, row) === 5 && falInputDurationOf(st, row) === 5, `${id} fixture default 5`);
}

ok(!timingSrc.includes('startContentSeriesTurbo') && !timingSrc.includes('elevenlabs'), 'no provider in timing');
ok(!timingSrc.includes('persistState'), 'no persist in timing');

if (fail.length) {
  console.error(`FAMIXA_SHOT_TIMING_CONTRACT_V3 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SHOT_TIMING_CONTRACT_V3 PASS T1–T14 + SYN FAIL=0 (no provider)');
