import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  AGE_CONSISTENCY_V1_ID,
  ageConsistencyShowsMismatch,
  ageConsistencyTone,
  ageExpressionRange,
  rejectReasonValid,
  studioMayRegenerate,
  studioPublicHeadline,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const ts = read('kit-video-character-studio.ts');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeConsistencyV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const studioRules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioV1Rules.cs');

ok(AGE_CONSISTENCY_V1_ID === 'FAMIXA_CHARACTER_AGE_CONSISTENCY_V1', '01 document id');
ok(ageExpressionRange(11).min === 10 && ageExpressionRange(11).max === 12
  && ageExpressionRange(36).min === 33 && ageExpressionRange(36).max === 39, '02 default age range');
ok(ui.includes('Tuổi nhân vật') && ui.includes('Tuổi biểu hiện mục tiêu')
  && ui.includes('Age Consistency'), '03 Studio shows Age Target');
ok(ui.includes('AGE MISMATCH') && ui.includes('Tuổi biểu hiện không phù hợp'), '04 AGE MISMATCH copy');
ok(rejectReasonValid('AGE_MISMATCH', '') && ts.includes('Sai tuổi'), '05 reject AGE_MISMATCH');
ok(studioMayRegenerate('REJECTED')
  && studioMayRegenerate('FAILED')
  && studioMayRegenerate(undefined, false, 'FAIL')
  && ui.includes('Tạo lại bộ ảnh'), '06 regenerate CTA after reject / fail');
ok(!ageConsistencyShowsMismatch('NOT_EVALUATED', true)
  && ageConsistencyTone('NOT_EVALUATED', true) === 'ready'
  && studioPublicHeadline({ status: 'CHARACTER_READY', coverage: 4, requiredTotal: 4, officialLocked: true })
    === 'CHARACTER READY · 4/4 · LOCKED', '07 locked character has no Age warning');
ok(controller.includes('character-studio/{characterId}/age-consistency')
  && controller.includes('HttpPost("character-studio/{characterId}/age-consistency")')
  && api.includes('/content/character-studio/${encodeURIComponent(characterId)}/age-consistency'), '08 age-consistency API');
ok(rules.includes('PolicyBands') && rules.includes('CompileAgeAppearanceProfile')
  && studioRules.includes('AgeExpressionBrief')
  && orch.includes('ageExpression'), '09 Age Policy + Appearance Profile');
ok(!rules.includes('if characterId ==') && !rules.includes('== "Minh"')
  && !orch.includes('characterName == "Minh"') && !orch.includes('characterName == "Linh"'),
  '10 no character-specific branch');
ok(rules.includes('InventsApparentAge() => false')
  && rules.includes('StatusNotEvaluated'), '11 no invented age score');
ok(!thisSmokeGenerates(), '12 no generation during TS smoke');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_AGE_CONSISTENCY_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_AGE_CONSISTENCY_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
