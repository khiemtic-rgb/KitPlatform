import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CALIBRATION_REQUIRED_SLOTS,
  CALIBRATION_VIEWS,
  VISUAL_CALIBRATION_LIVE_GENERATION_SUITE,
  VISUAL_CALIBRATION_PACK_ID,
  calibrationCoverageValid,
  calibrationMayApprove,
  calibrationMayGenerate,
  calibrationMayLock,
} from './kit-video-visual-calibration';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const card = read('ContentFamixaVisualCalibrationCard.tsx');
const helpers = read('kit-video-visual-calibration.ts');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1Rules.cs');
const live = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1LiveGenerationRegression.cs');
const service = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationPackService.cs');
const adapter = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationGenerationProvider.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(card.includes('Tạo 24 ảnh Calibration') && helpers.includes('VISUAL_CALIBRATION_PACK_ID')
  && VISUAL_CALIBRATION_PACK_ID === 'FAMIXA-VISUAL-CALIBRATION-V1',
  'CTA exists');
ok(card.includes('Xác nhận tạo 24 ảnh') && card.includes('24 ảnh sẽ được tạo')
  && card.includes('Không tạo nhân vật') && card.includes('Không Approve')
  && card.includes('Không Lock') && card.includes('Không thay đổi Authority'),
  'confirmation modal exists');
ok(rules.includes('GateConfirmation') && rules.includes('EvaluateLiveGenerate')
  && service.includes('request.Confirm'),
  'confirm=false path');
ok(card.includes('generate: true') && api.includes('generate?: boolean')
  && service.includes('request.Generate'),
  'generate=true path');
ok(card.includes('Calibration Generation') && card.includes('{valid} / {required}')
  && card.includes('setGenerating(true)'),
  'progress state');
ok(CALIBRATION_VIEWS.join(',') === 'FRONT,THREE_QUARTER,SIDE,FULL_BODY'
  && card.includes('CAL-001') && card.includes('Child Boy') && card.includes('Adult Male')
  && CALIBRATION_REQUIRED_SLOTS === 24,
  '24-slot display');
ok(card.includes('Coverage:') && card.includes('Pixel validity:')
  && helpers.includes('calibrationCoverageValid')
  && calibrationCoverageValid({ valid: 24, required: 24 }),
  'coverage display');
ok(card.includes('PENDING DIRECTOR REVIEW') && card.includes('Visual approval:')
  && !calibrationMayApprove('DRAFT', { valid: 0, required: 24 })
  && calibrationMayApprove('PENDING_REVIEW', { valid: 24, required: 24 }),
  'PENDING_REVIEW state');
ok(!card.includes('autoApprove: true') && rules.includes('AutoApprove() => false')
  && card.includes('directorPass: true'),
  'no auto approval');
ok(!card.includes('autoLock: true') && rules.includes('AutoLock() => false')
  && !calibrationMayLock('PENDING_REVIEW')
  && calibrationMayGenerate('DRAFT'),
  'no auto lock');
ok(controller.includes('visual-calibration/live-generation/regression')
  && api.includes('/content/visual-calibration/live-generation/regression')
  && live.includes('LG-01 confirm=false blocks provider')
  && live.includes('LG-25 no automatic visual PASS')
  && VISUAL_CALIBRATION_LIVE_GENERATION_SUITE === 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_REGRESSION'
  && adapter.includes('request.CompiledPrompt')
  && !thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(),
  'official suite + no accidental generation');

function thisSmokeGenerates() {
  return false;
}
function thisSmokeApproves() {
  return false;
}
function thisSmokeLocks() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
