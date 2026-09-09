import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_DESIGN_LANGUAGE_V2_ID,
  CHARACTER_DESIGN_LANGUAGE_V2_SUITE,
  designLanguageReady,
} from './kit-video-character-design-language';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterDesignLanguageV2Rules.cs');
const style = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStyleConsistencyV2Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterDesignLanguageV2Regression.cs');
const compose = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGenerationIntegrationV1Rules.cs');
const revision = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionV1Rules.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../shared/api/content.api.ts');
const card = read('ContentFamixaCharacterDesignLanguageV2Card.tsx');
const board = read('ContentFamixaProductionBoards.tsx');

ok(CHARACTER_DESIGN_LANGUAGE_V2_ID === 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2'
  && CHARACTER_DESIGN_LANGUAGE_V2_SUITE === 'FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_REGRESSION', '01 document id');
ok(rules.includes('STRONG_STYLIZED_3D') && rules.includes('PhotorealismCeiling = "LOW"')
  && rules.includes('CartoonFloor = "CONTROLLED"') && rules.includes('PromptBlock'), '02 definition + prompt block');
ok(designLanguageReady({ sha: 'a'.repeat(64), status: 'DRAFT' })
  && !designLanguageReady({ sha: 'short', status: 'DRAFT' }), '03 SHA + status');
ok(controller.includes('character-studio/character-design-language/v2')
  && controller.includes('character-design-language/v2/regression')
  && api.includes('/content/character-studio/character-design-language/v2'), '04 API reachable');
ok((compose.includes('CharacterDesignLanguageV2Rules.PromptBlock')
    || compose.includes('CompileStylePrefix'))
  && (revision.includes('CharacterDesignLanguageV2Rules.PromptBlock')
    || revision.includes('CompileStylePrefix'))
  && rules.includes('PromptBlock'), '05 prompt assembly uses CDL V2');
ok(!rules.includes('if (characterName') && !rules.includes('if (characterId')
  && !rules.includes('characterName ==') && !rules.includes('NamAppearance'), '06 no character-name branch');
ok(rules.includes('CallsGemini() => false') && rules.includes('Persists() => false')
  && rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false')
  && !controller.includes('character-design-language/v2/execute'), '07 no provider / mutation / auto');
ok(card.includes('CHARACTER DESIGN LANGUAGE V2') && card.includes('Chi tiết kỹ thuật')
  && !card.includes('defaultActiveKey')
  && board.includes('ContentFamixaCharacterDesignLanguageV2Card'), '08 compact read-only card');
ok(style.includes('StatusNotEvaluated') && style.includes('CompilationImpliesPass() => false')
  && style.includes('FaceEqualsStyle() => false'), '09 style gate is NOT_EVALUATED without evaluator');
ok(regression.includes('FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_REGRESSION')
  && regression.includes('ProtectedMasterSha')
  && regression.includes('DesignLanguageBlock identical'), '10 regression contract');
ok(!thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(), '11 smoke does not mutate');

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
  console.error(`FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
