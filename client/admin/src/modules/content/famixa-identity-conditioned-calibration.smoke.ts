import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/IdentityConditionedCalibrationV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/IdentityConditionedCalibrationV1Regression.cs');
const packRules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1Rules.cs');
const provider = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationGenerationProvider.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(controller.includes('identity-conditioned-calibration/regression')
  && controller.includes('identity-conditioned-calibration')
  && !controller.includes('identity-conditioned-calibration/generate')
  && !controller.includes('identity-conditioned-calibration/approve'), '01 API route exists and is read-only');
ok(rules.includes('CalibrationIdentityContract')
  && rules.includes('IdentityAnchorSlot')
  && rules.includes('IdentityAnchorSha256')
  && rules.includes('ReferenceRole')
  && rules.includes('IdentityAnchorView = "FRONT"'), '02 contract + IdentityAnchor fields exist');
ok(rules.includes('GenerationOrder')
  && rules.includes('FrontFirst')
  && packRules.includes('BindLiveAnchor')
  && packRules.includes('ProviderMayCall'), '03 FRONT-first order exists');
ok(rules.includes('ViewRefs')
  && rules.includes('THREE_QUARTER')
  && rules.includes('SIDE')
  && rules.includes('FULL_BODY')
  && rules.includes('ReferenceRoleAnchor'), '04 reference chain exists');
ok(packRules.includes('IdentityAnchorSha256')
  && packRules.includes('ReferenceRole')
  && rules.includes('IsValidIdentityAnchor')
  && rules.includes('IsIdentityConditioned'), '05 IdentityAnchor fields exist on artifacts');
ok(rules.includes('DownstreamRequiresFront() => true')
  && rules.includes('IndependentDownstreamGenerationAllowed() => false')
  && provider.includes('GateReference')
  && !provider.includes('References = []'), '06 downstream reference requirement exists');
ok(rules.includes('confirm=false')
  || (packRules.includes('if (!confirm)')
    && packRules.includes('GateConfirmation')), '07 dry-run semantics exist');
ok(provider.includes('ProviderMayCall')
  && provider.includes('request.CompiledPrompt')
  && !provider.includes('IVisualUniverseAuthority')
  && !provider.includes('IUnifiedVisualCompiler'), '08 no direct provider bypass');
ok(rules.includes('LegacyFallback() => false')
  && rules.includes('IndependentDownstreamGenerationAllowed() => false')
  && packRules.includes('continue;'), '09 no legacy fallback');
ok(rules.includes('MutatesCharacters() => false')
  && rules.includes('MutatesAuthority() => false')
  && !rules.includes('update Master')
  && regression.includes('IC-33'), '10 no character mutation');
ok(regression.includes('IC-01')
  && regression.includes('IC-43')
  && rules.includes('FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_V1_REGRESSION'), '11 regression cases');
ok(!rules.includes('ICalibrationVisualCompiler')
  && !rules.includes('CalibrationStyleCompiler'), '12 no second style compiler');

if (fail.length) {
  console.error('FAIL', fail);
  process.exit(1);
}
console.log('PASS famixa-identity-conditioned-calibration.smoke.ts');
