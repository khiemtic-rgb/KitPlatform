/** FAMIXA_MOTION_REMAKE_COST_ESTIMATE_FIX_V1 — remake modal estimate only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';
import { computeShotTiming, providerDurationOf } from './famixa-shot-production-timing';
import { famixaMotionGenerateCost } from './famixa-ai-provider-cost';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const remakeStart = series.indexOf('if (opts?.remake');
const remakeBlock = series.slice(remakeStart, series.indexOf('if (onlyIds?.length === 1)', remakeStart));

ok(remakeBlock.includes('famixaMotionBatchCost') && remakeBlock.includes('providerDurationOf(stateRef.current, s)'), '01 modal estimate uses providerDurationOf via Cost SoT');
ok(!remakeBlock.includes('generateCost(engine, s.seconds)'), '01b remake modal does not use shot.seconds');
ok(remakeBlock.includes('seconds: providerDurationOf(stateRef.current, shot)'), '02 confirm payload duration stays providerDurationOf');
ok(remakeBlock.includes("cancelText: 'Hủy — 0 cr'"), '03 cancel remains 0 cr');
ok(series.includes('famixaMotionGenerateCost') && series.includes('quoteFamixaProviderCost'), '05 generateCost reads Cost SoT');
ok(!series.includes('credits: sec * 5'), '05b SeriesTab no longer duplicates Runway formula');
ok(!remakeBlock.includes('startContentSeriesTurbo') && !remakeBlock.includes('fal-ai/'), '04 no provider in remake modal');

function generateCostCredits(seconds: number) {
  return famixaMotionGenerateCost('turbo', seconds).credits ?? 0;
}

const timing = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
const sh01: FamixaSeriesShot = {
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '10s',
  seconds: 10,
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
};

const state: SeriesPilotState = {
  roles: [],
  runs: {},
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

ok(sh01.seconds === 10, 'SH01 leftover seconds=10');
ok(timing.productionDurationSec === 1.94 && timing.providerDurationSec === 5, 'production 1.94 / provider 5');
ok(generateCostCredits(timing.providerDurationSec) === 25, '01d Cost SoT of Timing V3 provider 5s is 25 cr');
ok(generateCostCredits(sh01.seconds) === 50, 'old leftover seconds would show 50');
ok(generateCostCredits(5) === 25 && generateCostCredits(10) === 50, '05b 5s=25 / 10s=50 unchanged');
ok(typeof providerDurationOf(state, sh01) === 'number', '01c providerDurationOf still exported');

if (fail.length) {
  console.error(`FAMIXA_MOTION_REMAKE_COST_ESTIMATE_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_MOTION_REMAKE_COST_ESTIMATE_FIX_V1 PASS FAIL=0 (no provider)');
console.log(`shot.seconds=${sh01.seconds} provider=${providerDurationOf(state, sh01)} modalCr=${generateCostCredits(providerDurationOf(state, sh01))}`);
