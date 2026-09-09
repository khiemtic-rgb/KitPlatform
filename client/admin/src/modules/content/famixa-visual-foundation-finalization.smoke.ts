import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualFoundationFinalizationV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualFoundationFinalizationV1Regression.cs');
const provider = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationGenerationProvider.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(controller.includes('visual-foundation/finalization')
  && controller.includes('visual-foundation/finalization/regression')
  && !controller.includes('visual-foundation/finalization/approve')
  && !controller.includes('visual-foundation/finalization/lock'), '01 API is read-only');
ok(rules.includes('TECHNICAL_PASS')
  && rules.includes('VISUAL_PASS')
  && rules.includes('IDENTITY_PASS')
  && rules.includes('TechnicalPassIsVisualPass() => false'), '02 pass layers are separate');
ok(rules.includes('CalibrationUsesIdentityRefs() => true')
  && rules.includes('CalibrationGeneratesIndependently() => false')
  && provider.includes('IdentityConditionedCalibrationV1Rules.ProviderMayCall')
  && !provider.includes('[],'), '03 calibration uses FRONT identity refs');
ok(rules.includes('ViewRefs')
  && rules.includes('MASTER')
  && rules.includes('FRONT'), '04 production identity ref policy reused');
ok(rules.includes('CameraInvariantPixelReady() => false')
  && rules.includes('CameraMayRedesignIdentity() => false'), '05 camera invariant is compile-only until identity pixels');
ok(rules.includes('ExtraNegativeHasWatermark')
  && rules.includes('watermark'), '06 watermark gate exists');
ok(rules.includes('CallsGemini() => false')
  && rules.includes('MutatesAuthority() => false')
  && rules.includes('ProtectedMasterSha'), '07 no Gemini / no Minh mutate');
ok(rules.includes('AUTHORIZE_IDENTITY_CONDITIONED_LIVE_GENERATION')
  && !rules.includes('new PVS')
  && regression.includes('F-16'), '08 next step is identity-conditioned live generation');
ok(regression.includes('F-01')
  && regression.includes('F-18')
  && rules.includes('FAMIXA_VISUAL_FOUNDATION_FINALIZATION_V1_REGRESSION'), '09 regression cases');

if (fail.length) {
  console.error('FAIL', fail);
  process.exit(1);
}
console.log('PASS famixa-visual-foundation-finalization.smoke.ts');
