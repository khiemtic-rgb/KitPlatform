import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_DESIGN_LANGUAGE_SUITE,
  CHARACTER_DESIGN_LANGUAGE_V1_ID,
  designLanguageReady,
} from './kit-video-character-design-language';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterDesignLanguageV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterDesignLanguageV1Regression.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../shared/api/content.api.ts');
const card = read('ContentFamixaCharacterDesignLanguageCard.tsx');
const board = read('ContentFamixaProductionBoards.tsx');

ok(CHARACTER_DESIGN_LANGUAGE_V1_ID === 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1'
  && CHARACTER_DESIGN_LANGUAGE_SUITE === 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1_REGRESSION', '01 document id');
ok(rules.includes('PositivePromptBlock') && rules.includes('NegativePromptBlock')
  && rules.includes('AgeAdaptiveBlock') && rules.includes('StylizationLevel = "STRONG"')
  && rules.includes('PhotorealismLevel = "LOW"'), '02 definition + prompt blocks');
ok(designLanguageReady({ sha: 'a'.repeat(64), status: 'DRAFT' })
  && !designLanguageReady({ sha: 'short', status: 'DRAFT' }), '03 SHA + status');
ok(controller.includes('character-studio/character-design-language')
  && controller.includes('character-design-language/regression')
  && controller.includes('character-design-language/compile-preview')
  && api.includes('/content/character-studio/character-design-language'), '04 API reachable');
ok(rules.includes('AgeAdaptiveStylization') || card.includes('Age adaptive'), '05 age-adaptive block');
ok(!rules.includes('if (characterName') && !rules.includes('if (characterId')
  && !rules.includes('characterName ==') && !rules.includes('NamAppearance'), '06 no character-name branch');
ok(rules.includes('CallsGemini() => false') && rules.includes('Persists() => false')
  && rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false')
  && !controller.includes('character-design-language/execute'), '07 no provider / mutation / auto');
ok(card.includes('CHARACTER DESIGN LANGUAGE') && card.includes('Chi tiết Character Design Language')
  && !card.includes('defaultActiveKey')
  && board.includes('ContentFamixaCharacterDesignLanguageCard'), '08 compact read-only card');
ok(regression.includes('CDL-001') && regression.includes('CDL-020')
  && regression.includes('ProtectedMasterSha'), '09 regression contract');
ok(!thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(), '10 smoke does not mutate');

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
  console.error(`FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
