import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { VIDEO_GENERATION_EXECUTION_ID, VIDEO_GENERATION_EXECUTION_STATUSES } from './kit-video-video-generation-execution';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ui = readFileSync(join(root, 'ContentKitVideoVideoGenerationExecutionCard.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VideoGenerationExecutionRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs'),
  'utf8',
);

ok(VIDEO_GENERATION_EXECUTION_ID === 'PRODUCTION_VIDEO_GENERATION_EXECUTION_V1', '01 suite id');
ok(
  VIDEO_GENERATION_EXECUTION_STATUSES.includes('READY_FOR_DIRECTOR') &&
    !VIDEO_GENERATION_EXECUTION_STATUSES.includes('GENERATED' as never) &&
    !VIDEO_GENERATION_EXECUTION_STATUSES.includes('VIDEO_READY' as never),
  '02 statuses',
);
ok(ui.includes('VIDEO GENERATION EXECUTION') && ui.includes('PRE-FLIGHT') && ui.includes('EXECUTE VIDEO'), '03 UI actions');
ok(!ui.includes('AUTO APPROVE') && !ui.includes('GENERATE AGAIN') && !ui.includes('BLIND RETRY'), '04 no retry/auto');
ok(ui.includes('READY FOR DIRECTOR') || ui.includes('READY_FOR_DIRECTOR'), '05 director pending');
ok(series.includes('ContentKitVideoVideoGenerationExecutionCard'), '06 Series wires card');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoRetry() => false'), '07 no auto');
ok(service.includes('IVideoGenerationProvider') && service.includes('ProductionGateAsync'), '08 provider + governance');
ok(!service.includes('CreateImageToVideoAsync'), '09 service does not call Runway directly');
ok(service.includes('IN_FLIGHT_NO_SECOND_CALL') && rules.includes('MustNotStartSecondProviderCall'), '10 no second provider call while REQUESTED');
ok(service.includes('ORPHAN_RECOVER') && rules.includes('IsOrphanedInFlight'), '11 orphan REQUESTED without task may recover once');

if (fail.length) {
  console.error(`PRODUCTION_VIDEO_GENERATION_EXECUTION_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_VIDEO_GENERATION_EXECUTION_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /video-generation-execution/regression)');
