/** FAMIXA_DIRECTOR_SHOT_ENTRY_UX_V1 — Video drawer → existing Lane A studio. No provider. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const boards = read('ContentFamixaBuildBoards.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const overview = read('ContentFamixaProductionOverview.tsx');

const produceStart = series.indexOf('const openShotStudio');
const produceBlock = series.slice(produceStart, produceStart + 280);
const focusBlock = series.slice(series.indexOf('const focusStudioShot'), produceStart);

ok(boards.includes('Sản xuất Shot') && boards.includes('onProduceShot'), '01 Video drawer has Sản xuất Shot');
ok(/mode === 'video' && onProduceShot/.test(boards), '01b button only on Video scene drawer');
const imageBoard = boards.slice(
  boards.indexOf('export function ContentFamixaBuildImageBoard'),
  boards.indexOf('export function ContentFamixaBuildVideoBoard'),
);
ok(!imageBoard.includes('onProduceShot'), '01c images drawer has no Sản xuất Shot');
ok(series.includes('onProduceShot={openShotStudio}'), '02 SeriesTab wires Video drawer to openShotStudio');
ok(produceBlock.includes("setProdTab('overview')") && produceBlock.includes("setProdView('overview')"), '03 lands on Tổng quan');
ok(
  produceBlock.includes('focusStudioShot') &&
    focusBlock.includes('setStudioShotId') &&
    focusBlock.includes('setStudioFocusTick'),
  '04 reuses existing focus / shotId',
);
ok(!produceBlock.includes('openScene') && !produceBlock.includes("setProdView('shot')"), '05 no Lane B openScene');
ok(!produceBlock.includes('ContentKitVideoDirectorProductionWorkspace'), '05b no Lane B workspace');
ok(!produceBlock.includes('startContentSeriesTurbo') && !produceBlock.includes('startLipsync') && !produceBlock.includes('generateSceneKf'), '06 no provider call');
ok(!produceBlock.includes('navigate(') && !produceBlock.includes('useNavigate'), '07 no new route');
ok(overview.includes('onFocusShot') && series.includes('onFocusShot={focusStudioShot}') && series.includes('ContentFamixaShotProductionPanel'), '08 Tổng quan still focuses existing studio');
ok(ws.includes('selectedShotId') && ws.includes('focusTick') && ws.includes('ShotProductionPreview'), '09 existing ShotProductionWorkspace unchanged contract');
ok(!orch.includes('openShotStudio') && !orch.includes('Sản xuất Shot'), '10 orchestrator untouched');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_SHOT_ENTRY_UX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_SHOT_ENTRY_UX_V1 PASS FAIL=0 (no provider)');
