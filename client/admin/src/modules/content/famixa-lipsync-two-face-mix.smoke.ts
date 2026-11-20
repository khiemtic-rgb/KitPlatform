/** Two-face must still Confirm Fal. Mute overlay is not the fix. 0 Fal POST. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { clearTwoFaceLipsyncMixOnly, leftoverTwoFaceMixOnly } from './famixa-lipsync-two-face-mix';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

ok(leftoverTwoFaceMixOnly({ lipsyncMixOnly: true }), 'T1 leftover mute flag is detected');
ok(leftoverTwoFaceMixOnly({ lipsyncStatus: 'MIX_ONLY' }), 'T1 MIX_ONLY status is leftover');
ok(!leftoverTwoFaceMixOnly({ lipsyncStatus: 'FAILED' }), 'T1 Fal fail is not mute-close');
ok(clearTwoFaceLipsyncMixOnly().lipsyncMixOnly === false, 'T2 reopen clears mute-close');

const root = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const orch = readFileSync(
  join(root, 'ContentFamixaShotProduction/ShotProductionOrchestrator.ts'),
  'utf8',
);
const turbo = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'),
  'utf8',
);
const falCs = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentFalClient.cs'),
  'utf8',
);
ok(!series.includes('twoFaceClose'), 'T3 startLipsync does not skip two-face Fal');
ok(series.includes('clearTwoFaceLipsyncMixOnly') || series.includes('lipsyncMixOnly: false'), 'T3 opening SH01 reopens Fal');
ok(!orch.includes('if (snap.lipsyncMixOnly)'), 'T4 orchestrator does not skip lipsync on two-face');
ok(turbo.includes('FrameCountFromTake'), 'T5 boxes follow take frames, not mute overlay');
ok(falCs.includes('bounding_boxes') && falCs.includes('auto_detect = false'), 'T5 two-face still sends Linh box');

if (fail.length) {
  console.error(`famixa-lipsync-two-face-mix.smoke failed:\n- ${fail.join('\n- ')}`);
  process.exit(1);
}
console.log('famixa-lipsync-two-face-mix.smoke ok');
