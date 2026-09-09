import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CALIBRATION_REQUIRED_SLOTS,
  CALIBRATION_VIEWS,
  VISUAL_CALIBRATION_LIVE_SUITE,
  VISUAL_CALIBRATION_PACK_ID,
  calibrationCoverageValid,
  calibrationMayApprove,
  calibrationMayGenerate,
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
const live = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1LiveGenerationReadinessRegression.cs');
const service = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationPackService.cs');
const adapter = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationGenerationProvider.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(card.includes('Visual Calibration') && helpers.includes('VISUAL_CALIBRATION_PACK_ID')
  && VISUAL_CALIBRATION_PACK_ID === 'FAMIXA-VISUAL-CALIBRATION-V1',
  'pack loading');
ok(CALIBRATION_VIEWS.join(',') === 'FRONT,THREE_QUARTER,SIDE,FULL_BODY'
  && card.includes('fx-cstudio__grid')
  && card.includes('Child Boy') && card.includes('Adult Male')
  && CALIBRATION_REQUIRED_SLOTS === 24,
  '24 slot rendering');
ok(card.includes('Coverage ') && helpers.includes('calibrationCoverageValid')
  && !calibrationCoverageValid({ valid: 0, required: 24 })
  && calibrationCoverageValid({ valid: 24, required: 24 }),
  'coverage');
ok(card.includes('VUA {pack?.visualUniverseSha')
  && card.includes('PVS {pack?.projectVisualStyleSha')
  && card.includes('CDL {pack?.characterDesignLanguageSha')
  && rules.includes('CompilePlan'),
  'authority SHA');
ok(card.includes('title="Xác nhận"') && card.includes("setConfirm('generate')")
  && card.includes('title="Generate Calibration Pack"')
  && card.includes('generate: true'),
  'confirmation');
ok(/useEffect\(\(\) => \{\s*load\(\);\s*\}, \[\]\)/.test(card)
  && rules.includes('AutoGenerate() => false')
  && calibrationMayGenerate('DRAFT')
  && !calibrationMayApprove('DRAFT', { valid: 0, required: 24 })
  && calibrationMayApprove('PENDING_REVIEW', { valid: 24, required: 24 })
  && !calibrationMayApprove('PENDING_REVIEW', { valid: 23, required: 24 })
  && service.includes('IVisualCalibrationGenerationProvider')
  && adapter.includes('request.CompiledPrompt')
  && !adapter.includes('CompilePack')
  && controller.includes('visual-calibration/live-generation-readiness/regression')
  && api.includes('/content/visual-calibration/live-generation-readiness/regression')
  && live.includes('R-25 no provider call during regression')
  && VISUAL_CALIBRATION_LIVE_SUITE === 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_READINESS_REGRESSION'
  && !thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(),
  'no accidental generation');

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
  console.error(`FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_READINESS_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_READINESS_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
