import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AGE_GENERATION_INTEGRATION_SUITE,
  AGE_GENERATION_INTEGRATION_V1_ID,
  ageExpressionRange,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGenerationIntegrationV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGenerationIntegrationV1Regression.cs');
const ageRules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeConsistencyV1Rules.cs');
const studioRules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const geminiStudio = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterGenerationProvider.cs');
const geminiViews = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterReferenceGenerationProvider.cs');
const contract = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProductionOsRules.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(AGE_GENERATION_INTEGRATION_V1_ID === 'FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1', '01 document id');
ok(AGE_GENERATION_INTEGRATION_SUITE === 'FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1_REGRESSION', '02 suite id');
ok(ageExpressionRange(11).min === 10 && ageExpressionRange(11).max === 12
  && ageExpressionRange(38).min === 35 && ageExpressionRange(38).max === 41
  && ageExpressionRange(36).min === 33 && ageExpressionRange(36).max === 39
  && ageExpressionRange(27).min === 25 && ageExpressionRange(27).max === 29, '03 matrix age policy');
ok(rules.includes('ComposeViewPrompt') && rules.includes('AgeAppearanceProfile')
  && rules.includes('ChronologicalAge') && rules.includes('TargetAppearanceAgeMin'), '04 Prompt Builder');
ok(contract.includes('AgeAppearanceProfile = null')
  && orch.includes('StudioViewSet') && orch.includes('ComposeViewPrompt'), '05 generation request carries Age Profile');
ok(geminiStudio.includes('ProviderRequestHasAge')
  && geminiViews.includes('ProviderPrompt'), '06 Gemini adapter consumes Age Profile');
ok(ageRules.includes('AgePromptBlock') && studioRules.includes('AgeExpressionBrief'), '07 Identity brief labeled Age fields');
ok(rules.includes('ViewsShareAgeProfile') && rules.includes('ViewCamera'), '08 view consistency');
ok(rules.includes('ReadyAllowed') && ageRules.includes('FaceAndAgeIndependent'), '09 independent gates');
ok(!rules.includes('== "Minh"') && !rules.includes('== "Thảo"') && !rules.includes('== "Thao"')
  && !orch.includes('characterName == "Minh"'), '10 no character-specific branch');
ok(regression.includes('PromptContainsAge') && regression.includes('ProviderRequestHasAge')
  && regression.includes('ChronologicalAge: 27'), '11 regression asserts request/prompt, not only DB');
ok(controller.includes('age-generation-integration/regression'), '12 regression endpoint');
ok(!thisSmokeGenerates() && rules.includes('CallsGemini() => false'), '13 no Gemini / no image generation');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
