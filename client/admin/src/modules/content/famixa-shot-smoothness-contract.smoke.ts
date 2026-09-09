/** FAMIXA_SHOT_SMOOTHNESS_CONTRACT_V1 — coverage / editorial / last-frame / mix flags. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';
import { compileI2vPrompt } from './content-famixa-series';
import { oneShotAssembleBody } from './ContentFamixaShotProduction/ShotProductionAssemble';
import { providerDurationOf } from './famixa-shot-production-timing';
import {
  SMOOTHNESS_DOCUMENT_ID,
  applyEditorialPreset,
  deriveCoverage,
  hasDirectedMotion,
  lastFrameEligible,
} from './famixa-shot-smoothness-contract';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const contractSrc = readFileSync(join(root, 'famixa-shot-smoothness-contract.ts'), 'utf8');
const cardSrc = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionSmoothnessCard.tsx'), 'utf8');
const assembleSrc = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesAssembleService.cs'),
  'utf8',
);
const turboSrc = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'),
  'utf8',
);
const runwaySrc = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentRunwayClient.cs'),
  'utf8',
);

const shot = (partial: Partial<FamixaSeriesShot> & { id: string; story: string }): FamixaSeriesShot => ({
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH',
  clock: '5s',
  seconds: 10,
  visual: partial.story,
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001'],
  location: 'PHÒNG KHÁCH - TỐI',
  motionPrompt: '',
  motionPromptVi: partial.story,
  status: 'story_locked',
  dialogueSegmentIds: [],
  ...partial,
});

const sh01 = shot({
  id: 'EP99-SC01-SH01',
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characterIds: ['CHAR-001'],
});
const sh02 = shot({
  id: 'EP99-SC01-SH02',
  story: 'Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.',
  characters: ['CHAR-003'],
  characterIds: ['CHAR-003'],
});
const sh03 = shot({
  id: 'EP99-SC01-SH03',
  story: 'Minh bước tới bàn ăn, đặt tờ giấy xuống, đứng im.',
});

const state: SeriesPilotState = {
  roles: [],
  runs: {
    [sh01.id]: {
      status: 'approved',
      takeUrl: 'https://example.test/sh01.mp4',
      runwayAttempts: [{ n: 1, status: 'SUCCEEDED', duration: 5, outputUrl: 'https://example.test/sh01.mp4' }],
    } as SeriesPilotState['runs'][string],
    [sh02.id]: { status: 'approved', transitionType: 'CONTINUOUS' } as SeriesPilotState['runs'][string],
    [sh03.id]: { status: 'approved', transitionType: 'CONTINUOUS' } as SeriesPilotState['runs'][string],
  },
  episode: { id: 'EP99', code: 'EP99', title: 'TEST', shots: [sh01, sh02, sh03] } as SeriesPilotState['episode'],
};

ok(SMOOTHNESS_DOCUMENT_ID === 'FAMIXA_SHOT_SMOOTHNESS_CONTRACT_V1', 'doc id');
ok(deriveCoverage(sh01).size === 'MS' && deriveCoverage(sh01).cameraPath === 'HOLD', 'SH01 medium hold');
ok(deriveCoverage(sh02).size === 'MCU', 'SH02 MCU sit/phone');
ok(deriveCoverage(sh03).cameraPath === 'TRACK' && hasDirectedMotion(sh03), 'SH03 track + directed');
ok(deriveCoverage(sh02, { previous: sh01, transition: 'CONTINUOUS' }).bridge === 'CUT', 'subject change → CUT');
ok(!lastFrameEligible({ coverage: { size: 'MS', cameraPath: 'HOLD', bridge: 'CUT' }, previousTakeUrl: 'https://x' }), 'CUT not last-frame');
ok(
  lastFrameEligible({
    coverage: { size: 'MS', cameraPath: 'HOLD', bridge: 'LAST_FRAME' },
    previousTakeUrl: 'https://x',
  }),
  'LAST_FRAME + url eligible',
);

const floor = applyEditorialPreset(state, sh01, 'DIALOGUE_FLOOR');
ok(floor.source === 'DIRECTOR', 'dialogue floor is Director');
const full = applyEditorialPreset(state, sh01, 'FULL_TAKE');
ok(full.source === 'DEFAULT_TAKE' && full.productionDurationSec === 5, 'full take = performance 5');
ok(providerDurationOf(state, sh01) === 5, 'stale seconds=10 does not win over SUCCESS 5s');

const holdPrompt = compileI2vPrompt(state, sh01, sh01.story);
ok(/Blink and breathe/i.test(holdPrompt) && /Camera remains steady/i.test(holdPrompt), 'hold keeps blink');
const walkPrompt = compileI2vPrompt(state, sh03, sh03.story);
ok(!/Blink and breathe/i.test(walkPrompt) && /continuous move|bước|place|walk/i.test(walkPrompt), 'directed drops blink');

const body = oneShotAssembleBody({
  shotCode: 'SH01-01',
  seconds: 5,
  spoken: true,
  lipsynced: true,
  voices: [],
});
ok(body.mix.grade === true && body.mix.interpolate === false && body.mix.music === true, 'Mix grade on, interpolate off, bed on');

ok(cardSrc.includes('Sát thoại') && cardSrc.includes('Grade 2.5D'), 'UI chips');
ok(!cardSrc.includes('photoreal') || cardSrc.includes('Không photoreal'), 'no photoreal push');
ok(assembleSrc.includes('minterpolate') && assembleSrc.includes('colorbalance'), 'P2/P3 ffmpeg');
ok(turboSrc.includes('LastFrameFromUrl') && turboSrc.includes('ExtractLastFrameDataUriAsync'), 'P1 last-frame extract');
ok(runwaySrc.includes('position = "last"'), 'Runway last image payload');
ok(!contractSrc.includes('startContentSeriesTurbo') && !cardSrc.includes('fetch('), 'no provider in contract/UI');

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('famixa-shot-smoothness-contract.smoke ok');
