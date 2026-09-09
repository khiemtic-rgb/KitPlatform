/** FAMIXA_SHOT_PRODUCTION_CONTRACT_FIX_V1 — no provider calls. */

import { applyDialogueMap } from './content-famixa-dialogue-map';
import { applyEditDurations } from './content-famixa-scene-first';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';
import { compileI2vPrompt } from './content-famixa-series';
import { compileRunwayPromptV1, promptViolatesRunwayI2vLaw } from './content-runway-prompt-v1';
import { normalizeLipsyncSyncMode } from './content-famixa-prod-v2';
import { oneShotAssembleBody } from './ContentFamixaShotProduction/ShotProductionAssemble';
import { isStale, mixInputFingerprint } from './ContentFamixaShotProduction/ShotProductionFingerprint';
import { computeInputFingerprints } from './ContentFamixaShotProduction/ShotProductionStamp';
import { nextShotProductionCommand } from './ContentFamixaShotProduction/ShotProductionOrchestrator';
import { buildShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  computeShotTiming,
  mapProviderDuration,
  productionDurationOf,
  providerDurationOf,
  shotTimingOf,
} from './famixa-shot-production-timing';

const fail: string[] = [];
function ok(cond: unknown, label: string) {
  if (!cond) fail.push(label);
}

const timing = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
ok(timing.productionDurationSec === 1.94, '01 SH01 production 1.94');
ok(timing.providerDurationSec === 5, '02 SH01 provider 5');
ok(timing.speechStartSec === 0.2, '01b speech start 0.20');
ok(timing.speechEndSec === 1.72, '01c speech end 1.72');
ok(mapProviderDuration(1.94) === 5, '02b map 1.94 → 5');
ok(mapProviderDuration(5.5) === 5, '02c map 5.50 → 5');
ok(mapProviderDuration(5.51) === 10, '02d map 5.51 → 10');
ok(mapProviderDuration(10.06) === 'BLOCK', '02e map >10.05 BLOCK');

const shot = (partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot => ({
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '10s',
  seconds: 10,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001'],
  location: 'PHÒNG KHÁCH - TỐI',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  ...partial,
});

const sh01 = shot({ id: 'EP99-SC01-SH01', timing, editSeconds: 1.94, seconds: 5, clock: '5s' });
ok(sh01.editSeconds === 1.94, '03 editSeconds = production');
ok(sh01.seconds === 5, '03b seconds = provider only');

const state: SeriesPilotState = {
  roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'pLQJCzpzwaedKVuhI1Mq' }],
  runs: {
    'EP99-SC01-SH01': {
      status: 'approved',
      kfApproved: true,
      videoApproved: true,
      lipsynced: true,
      lipsyncUrl: 'https://fal/lip.mp4',
      takeUrl: 'https://take/mute.mp4',
      previewUrl: 'https://take/mute.mp4',
      keyframeDataUrl: 'data:image/jpeg;base64,xx',
      keyframeFileName: 'kf.jpg',
      shotQa: { action: true, continuity: true, voiceFace: true },
    },
  },
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'pLQJCzpzwaedKVuhI1Mq' }],
  lines: [{ id: 'line-SC01-CHAR-001-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con tờ này.', voiceId: 'pLQJCzpzwaedKVuhI1Mq' }],
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

ok(productionDurationOf(state, sh01) === 5, '03c V3 production default = performance 5');
ok(providerDurationOf(state, sh01) === 5, '03d providerDurationOf 5');
ok(shotTimingOf(state, sh01)?.speechEndSec === 1.72, '03e speech window');
ok(
  oneShotAssembleBody({
    shotCode: 'SH01-01',
    seconds: productionDurationOf(state, sh01),
    spoken: true,
    lipsynced: true,
    voices: [],
  }).clips[0]?.seconds === 5,
  '03f assemble Final 5',
);

const prompt = compileI2vPrompt(state, sh01, sh01.story);
ok(/doorway|Standing at/i.test(prompt), '06 action/blocking');
ok(/paper|both hands/i.test(prompt), '06 prop');
ok(/mother|not the (camera|lens)/i.test(prompt), '06 gaze');
ok(/uneasy|contained|Subtle body movement/i.test(prompt), '06 acting');
ok(/Camera remains steady/i.test(prompt), '06 camera');
ok(/short beat|Blink and breathe/i.test(prompt), '06 timing');
ok(!/3D|stylized|wardrobe|DNA|Master|Visual Universe|polo|age 11|FAMIXA/i.test(prompt), '07 no Visual Authority');
ok(!promptViolatesRunwayI2vLaw(prompt), '07b prompt passes I2V law');

ok(normalizeLipsyncSyncMode(undefined) === 'silence', '08 default silence');
ok(normalizeLipsyncSyncMode() === 'silence', '09 not remap');
ok(normalizeLipsyncSyncMode('remap') === 'remap', '09b remap only if explicit');

const fps = computeInputFingerprints(state, sh01);
ok(Boolean(fps.mix && fps.lipsync && fps.motion), '10 fingerprints exist');
const fps2 = computeInputFingerprints(
  { ...state, episode: { ...state.episode!, shots: [{ ...sh01, timing: { ...timing, productionDurationSec: 2.5, source: 'DIRECTOR' } }] } },
  { ...sh01, timing: { ...timing, productionDurationSec: 2.5, source: 'DIRECTOR' } },
);
ok(isStale(fps.mix, fps2.mix), '11 changing production duration stales mix');
ok(mixInputFingerprint({ lipsyncFp: 'a', voiceFp: 'b', motionFp: 'c', productionDurationSec: 1.94 }) !==
  mixInputFingerprint({ lipsyncFp: 'a', voiceFp: 'b', motionFp: 'c', productionDurationSec: 10 }), '11b mix includes production');

const legacy = shot({ id: 'LEGACY-SH', seconds: 10, clock: '10s', dialogueSegmentIds: ['line-SC01-CHAR-001-1'] });
const legacyState: SeriesPilotState = {
  ...state,
  voiceAssets: undefined,
  episode: { ...state.episode!, shots: [legacy] },
};
const mapped = applyDialogueMap(legacyState);
ok(mapped.episode?.shots[0]?.seconds === 10, '12 applyDialogueMap does not rewrite legacy seconds');
ok(!mapped.episode?.shots[0]?.timing, '12b no silent timing write');
const edited = applyEditDurations(legacyState, [legacy]);
ok(edited.episode?.shots[0]?.seconds === 10 && !edited.episode?.shots[0]?.timing, '12c applyEditDurations skips unmeasured voice');

const multi = structuredClone(state);
multi.lines = [
  state.lines![0]!,
  { id: 'line-2', characterId: 'CHAR-002', text: 'Để đấy.', voiceId: 'v2' },
];
multi.characters = [
  { id: 'CHAR-001', name: 'Minh', voiceId: 'pLQJCzpzwaedKVuhI1Mq' },
  { id: 'CHAR-002', name: 'Nam', voiceId: 'v2' },
];
multi.episode = {
  ...state.episode!,
  shots: [{ ...sh01, characterIds: ['CHAR-001', 'CHAR-002'], dialogueSegmentIds: ['line-SC01-CHAR-001-1', 'line-2'] }],
};
const multiSnap = buildShotProductionSnapshot({
  state: multi,
  shot: multi.episode!.shots[0]!,
  ttsFiles: {},
});
ok(nextShotProductionCommand(multiSnap).code === 'MULTI_SPEAKER_LIPSYNC_UNSUPPORTED', '13 multi-speaker blocked');

const timed = applyEditDurations(state, [sh01]);
const timedShot = timed.episode?.shots[0];
ok(timedShot?.timing?.productionDurationSec === 5, '18 applyEditDurations V3 production 5');
ok(timedShot?.seconds === 5 && timedShot.editSeconds === 5, '18b seconds provider / edit performance');
ok(timedShot?.actingBeat?.before.holdSec === 0.2, '08 actingBeat before 0.20');
ok(timedShot?.actingBeat?.after.holdSec === 0.22, '08 actingBeat after 0.22');
ok(timedShot?.actingBeat?.during.speech === true, '08 actingBeat during speech');

const root = dirname(fileURLToPath(import.meta.url));
const assembleCs = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesAssembleService.cs'),
  'utf8',
);
ok(!/tpad=stop_mode=clone/.test(assembleCs), '04 no tpad clone');
ok(/apad=pad_dur=0\.05/.test(assembleCs), '05 apad mux only');
ok(/ngắn hơn production/.test(assembleCs), '05b short take fails assemble');

const v1 = compileRunwayPromptV1({
  action: sh01.story,
  motion: {
    action: 'Standing at the living-room doorway.',
    prop: 'He holds a sheet of paper with both hands.',
    gaze: 'He looks toward his mother, not the camera.',
    acting: 'His performance is uneasy and contained.',
    timing: 'He begins speaking after a short beat.',
    camera: 'Camera remains steady.',
  },
});
ok(/Standing at the living-room doorway/.test(v1.text), '18 motion action');
ok(/sheet of paper/.test(v1.text), '18 prop');
ok(/mother/.test(v1.text), '18 gaze');

if (fail.length) {
  console.error('SHOT PRODUCTION CONTRACT FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('SHOT PRODUCTION CONTRACT PASS · production 5 · provider 5 · Final 5');
