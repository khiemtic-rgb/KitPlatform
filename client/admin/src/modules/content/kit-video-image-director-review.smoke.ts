import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { IMAGE_DIRECTOR_REVIEW_ID, IMAGE_DIRECTOR_REVIEW_STATUSES } from './kit-video-image-director-review';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ui = readFileSync(join(root, 'ContentKitVideoImageDirectorReviewCard.tsx'), 'utf8');
const workspace = readFileSync(join(root, 'ContentKitVideoDirectorProductionWorkspace.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ImageGenerationDirectorReviewRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ImageGenerationDirectorReviewService.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

ok(IMAGE_DIRECTOR_REVIEW_ID === 'PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1', '01 suite id');
ok(
  IMAGE_DIRECTOR_REVIEW_STATUSES.includes('PENDING') &&
    IMAGE_DIRECTOR_REVIEW_STATUSES.includes('APPROVED') &&
    !IMAGE_DIRECTOR_REVIEW_STATUSES.includes('GENERATED' as never) &&
    !IMAGE_DIRECTOR_REVIEW_STATUSES.includes('VIDEO_READY' as never),
  '02 review statuses only',
);
ok(ui.includes('PRODUCTION IMAGE — DIRECTOR REVIEW') && ui.includes('APPROVE IMAGE') && ui.includes('REJECT IMAGE'), '03 review UI');
ok(ui.includes('fetchImageGenerationExecutionArtifactBlob') && ui.includes('IMAGE PREVIEW'), '04 real artifact blob');
ok(ui.includes('DIRECTOR APPROVAL:') && !ui.includes('DIRECTOR GATE: PASS'), '05 no fake Director PASS');
ok(ui.includes('IMAGE READY — DIRECTOR REVIEW REQUIRED'), '06 ready badge');
ok(!ui.includes('EXECUTE IMAGE GENERATION') && !ui.includes('RUNWAY') && !ui.includes('GENERATE VIDEO'), '07 no generate/runway');
ok(workspace.includes('✓ DUYỆT ẢNH') && workspace.includes('aria-label="APPROVE IMAGE"'), '08 workspace director entry');
ok(series.includes('ContentKitVideoImageDirectorReviewCard'), '09 Series wires review card');
ok(rules.includes('AutoApprove() => false') && rules.includes('AllowsGemini() => false') && rules.includes('AllowsRunway() => false'), '10 no auto/provider');
ok(service.includes('IGeminiImageGenerationProvider') === false && service.includes('IRunway') === false, '11 service no provider');
ok(controller.includes('image-director-review/approve') && controller.includes('generate = false'), '12 APIs keep generate=false');
ok(ui.includes('id="production-image-director-review"'), '13 scroll target');

if (fail.length) {
  console.error(`PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /image-director-review/regression)');
