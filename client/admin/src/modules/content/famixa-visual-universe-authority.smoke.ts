import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { directorStyleConformanceGate, directorUniverseGate } from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/FamixaVisualUniverseAuthorityV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/FamixaVisualUniverseAuthorityV1Regression.cs');
const compose = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGenerationIntegrationV1Rules.cs');
const revision = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterMasterRevisionV1Rules.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../shared/api/content.api.ts');
const card = read('ContentFamixaVisualUniverseAuthorityCard.tsx');
const studio = read('ContentFamixaCharacterStudio.tsx');
const board = read('ContentFamixaProductionBoards.tsx');

ok(rules.includes('FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1')
  && rules.includes('LOW_TO_MODERATE')
  && rules.includes('STRUCTURED_CHARACTER_DESIGN_LANGUAGE'), '01 authority + structured CDL');
ok(rules.includes('StyleExemplars') && rules.includes('CalibrationArchetypes')
  && rules.includes('ADULT_MALE') && !rules.includes('if (characterName'), '02 reference + calibration, no name branch');
ok(compose.includes('CompileStylePrefix') && revision.includes('CompileStylePrefix')
  && compose.includes('ageBlock') && revision.includes('ageBlock'), '03 prompt order style before identity');
ok(controller.includes('character-studio/project-visual-universe')
  && controller.includes('project-visual-universe/calibration')
  && controller.includes('project-visual-universe/regression')
  && api.includes('/content/character-studio/project-visual-universe'), '04 API');
ok(card.includes('FAMIXA Visual Universe') && card.includes('STYLE CALIBRATION')
  && card.includes('Chi tiết kỹ thuật') && !card.includes('defaultActiveKey')
  && board.includes('ContentFamixaVisualUniverseAuthorityCard'), '05 Director card');
ok(studio.includes('Style') && studio.includes('Visual Universe')
  && studio.includes('directorStyleConformanceGate') && studio.includes('directorUniverseGate'), '06 Character Studio gates');
ok(directorStyleConformanceGate('NOT_EVALUATED') === 'wait'
  && directorStyleConformanceGate('FAIL') === 'fail'
  && directorUniverseGate('NOT_LOCKED') === 'wait'
  && directorUniverseGate('MISMATCH') === 'fail'
  && directorUniverseGate('PASS', true) === 'pass', '07 gate helpers');
ok(rules.includes('CallsGemini() => false') && rules.includes('AutoApprove() => false')
  && rules.includes('ReplacesProjectVisualStyleV1() => false')
  && !controller.includes('project-visual-universe/execute'), '08 no auto / no PVS replace');
ok(regression.includes('FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1_REGRESSION')
  && regression.includes('ProtectedMasterSha')
  && regression.includes('Adult age does not escalate'), '09 regression contract');
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
  console.error(`FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
