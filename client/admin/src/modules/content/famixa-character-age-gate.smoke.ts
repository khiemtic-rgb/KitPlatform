import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AGE_GATE_SUITE,
  AGE_GATE_V1_ID,
  ageConsistencyLabel,
  ageConsistencyShowsMismatch,
  ageConsistencyTone,
  ageExpressionRange,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const ts = read('kit-video-character-studio.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGateV1Rules.cs');
const ageRules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeConsistencyV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGateV1Regression.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const di = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(AGE_GATE_V1_ID === 'FAMIXA_CHARACTER_AGE_GATE_V1', '01 document id');
ok(AGE_GATE_SUITE === 'FAMIXA_CHARACTER_AGE_GATE_V1_REGRESSION', '02 suite id');
ok(ageExpressionRange(11).min === 10 && ageExpressionRange(27).max === 29
  && ageExpressionRange(36).min === 33 && ageExpressionRange(38).max === 41, '03 age policy');
ok(rules.includes('ICharacterAgeConsistencyEvaluator')
  && rules.includes('DeferredCharacterAgeConsistencyEvaluator')
  && rules.includes('PromptDoesNotImplyPass')
  && di.includes('ICharacterAgeConsistencyEvaluator'), '04 evaluator abstraction');
ok(rules.includes('AGE_PROFILE_NOT_READY')
  && rules.includes('INVALID_CHRONOLOGICAL_AGE')
  && rules.includes('INVALID_TARGET_AGE_RANGE')
  && rules.includes('CRP_INCOMPLETE'), '05 gate codes');
ok(ageConsistencyLabel('NOT_EVALUATED') === 'NOT_EVALUATED'
  && ageConsistencyLabel('PASS', 94) === 'PASS · 94%'
  && ageConsistencyLabel('FAIL', 71) === 'FAIL · 71%'
  && ui.includes('ageConsistencyLabel'), '06 UI status + score');
ok(ageConsistencyShowsMismatch('FAIL', false)
  && !ageConsistencyShowsMismatch('NOT_EVALUATED', false)
  && ageConsistencyTone('BLOCKED') === 'blocked'
  && ui.includes('Tuổi nhân vật') && ui.includes('Tuổi biểu hiện mục tiêu'), '07 Studio age gate copy');
ok(rules.includes('ReasonOlder') && rules.includes('Visual age appears older')
  && rules.includes('Visual age appears younger'), '08 FAIL reasons');
ok(orch.includes('StudioAgeGate') && orch.includes('ICharacterAgeConsistencyEvaluator'), '09 Studio uses Age Gate');
ok(!rules.includes('== "Minh"') && !rules.includes('== "Thảo"') && !ageRules.includes('if characterId =='),
  '10 no character-specific branch');
ok(regression.includes('PromptDoesNotImplyPass') && regression.includes('ViewsShareAgeProfile')
  && controller.includes('age-gate/regression'), '11 regression asserts gate + prompts');
ok(!thisSmokeGenerates() && rules.includes('CallsGemini() => false'), '12 no Gemini / no generation');
ok(ui.includes('Tạo lại bộ ảnh') && ts.includes('studioMayRegenerate'), '13 regenerate remains a CTA');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_AGE_GATE_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_AGE_GATE_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
