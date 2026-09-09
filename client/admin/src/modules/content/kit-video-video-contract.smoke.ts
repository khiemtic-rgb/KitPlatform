import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PRODUCTION_VIDEO_CONTRACT_ID, PRODUCTION_VIDEO_CONTRACT_STATUSES } from './kit-video-video-contract';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ui = readFileSync(join(root, 'ContentKitVideoProductionVideoContractCard.tsx'), 'utf8');
const workspace = readFileSync(join(root, 'ContentKitVideoDirectorProductionWorkspace.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProductionVideoContractRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionVideoContractService.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

ok(PRODUCTION_VIDEO_CONTRACT_ID === 'PRODUCTION_VIDEO_CONTRACT_V1', '01 suite id');
ok(
  PRODUCTION_VIDEO_CONTRACT_STATUSES.includes('VALIDATED') &&
    PRODUCTION_VIDEO_CONTRACT_STATUSES.includes('DIRECTOR_APPROVED') &&
    !PRODUCTION_VIDEO_CONTRACT_STATUSES.includes('GENERATED' as never) &&
    !PRODUCTION_VIDEO_CONTRACT_STATUSES.includes('VIDEO_READY' as never),
  '02 contract statuses only',
);
ok(ui.includes('PRODUCTION VIDEO CONTRACT') && ui.includes('DIRECTOR APPROVE') && ui.includes('REJECT'), '03 review UI');
ok(ui.includes('APPROVED STILL') && ui.includes('VISUAL ANCHOR') && ui.includes('fetchImageGenerationExecutionArtifactBlob'), '04 still preview');
ok(ui.includes('VIDEO CONTRACT BLOCKED') && ui.includes('Approved production still required'), '05 blocked copy');
ok(!ui.includes('DIRECTOR GATE: PASS') && !ui.includes('GENERATE VIDEO') && !ui.includes('RUNWAY') && !ui.includes('GEMINI'), '06 no generate/provider');
ok(workspace.includes('CREATE VIDEO CONTRACT') && workspace.includes('Approved production still'), '07 workspace video contract entry');
ok(series.includes('ContentKitVideoProductionVideoContractCard'), '08 Series wires card');
ok(rules.includes('AutoApprove() => false') && rules.includes('AllowsGemini() => false') && rules.includes('AllowsRunway() => false'), '09 no auto/provider');
ok(service.includes('IGemini') === false && service.includes('IRunway') === false && service.includes('generate = false'), '10 service no provider');
ok(controller.includes('video-contract/{contractId:guid}/approve') && controller.includes('generation = false'), '11 APIs keep generation=false');
ok(ui.includes('id="production-video-contract"'), '12 scroll target');

if (fail.length) {
  console.error(`PRODUCTION_VIDEO_CONTRACT_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_VIDEO_CONTRACT_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /video-contract/regression)');
