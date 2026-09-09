import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { IMAGE_GENERATION_EXECUTION_ID, IMAGE_GENERATION_EXECUTION_STATUSES } from './kit-video-image-generation-execution';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ui = readFileSync(join(root, 'ContentKitVideoImageGenerationExecutionCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ImageGenerationExecutionRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ImageGenerationExecutionService.cs'),
  'utf8',
);
const provider = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiImageGenerationProvider.cs'),
  'utf8',
);

ok(IMAGE_GENERATION_EXECUTION_ID === 'PRODUCTION_IMAGE_GENERATION_EXECUTION_V1', '01 suite id');
ok(IMAGE_GENERATION_EXECUTION_STATUSES.includes('READY_FOR_DIRECTOR') && !IMAGE_GENERATION_EXECUTION_STATUSES.includes('VIDEO_READY' as never), '09 no VIDEO_READY');
ok(ui.includes('Tạo hình') && ui.includes('Bắt đầu tạo') && ui.includes('Chi tiết sản xuất'), '25 staff generate + confirm');
ok(!ui.includes('EXECUTE IMAGE GENERATION') && !ui.includes('LIPSYNC') && !ui.includes('GENERATE VIDEO'), '25 no technical execute / video');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoRetry() => false'), '18 no auto-approve/retry');
ok(service.includes('IGeminiImageGenerationProvider') && service.includes('EvaluatePreflight'), '23 execute after preflight');
ok(provider.includes('GenerateProductionStillOnceAsync') && !provider.includes('Runway'), '06 provider adapter');
ok(!service.includes('IRunway') && !provider.includes('IRunway'), '33 no Runway');

if (fail.length) {
  console.error(`PRODUCTION_IMAGE_GENERATION_EXECUTION_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_IMAGE_GENERATION_EXECUTION_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /image-generation-execution/regression)');
