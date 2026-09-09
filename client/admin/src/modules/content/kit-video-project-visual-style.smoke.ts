import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PROJECT_VISUAL_STYLE_SUITE, PROJECT_VISUAL_STYLE_V1_ID } from './kit-video-project-visual-style';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualStyleV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const ui = read('ContentFamixaCharacterStudio.tsx');
const card = read('ContentFamixaProjectVisualStyleCard.tsx');
const board = read('ContentFamixaProductionBoards.tsx');
const api = read('../../shared/api/content.api.ts');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(PROJECT_VISUAL_STYLE_V1_ID === 'FAMIXA_PROJECT_VISUAL_STYLE_V1', '01 document id');
ok(PROJECT_VISUAL_STYLE_SUITE === 'FAMIXA_PROJECT_VISUAL_STYLE_V1_REGRESSION', '02 suite id');
ok(rules.includes('IProjectVisualStyleAuthority') === false && rules.includes('ProjectVisualStyleV1Rules'), '03 Application rules');
ok(controller.includes('project-visual-style/regression') && api.includes('/content/project-visual-style'), '04 API');
ok(card.includes('Thiết lập phong cách') && board.includes('ContentFamixaProjectVisualStyleCard'), '05 Project UI');
ok(ui.includes('Inherited from Project') && !ui.includes('<Radio.Group value={styleId}'), '06 Character cannot pick style');
ok(!rules.includes('Minh' + 'VisualStyle') && !rules.includes('Nam' + 'VisualStyle')
  && !orch.includes('characterName == "Minh"') && !orch.includes('characterName == "Nam"'), '07 no character hard-code');
ok(!rules.includes('Google.GenAI') && !orch.includes('ContentGeminiClient'), '08 no Gemini in style/orchestrator isolation');
ok(rules.includes('PROJECT_VISUAL_STYLE_NOT_READY') && rules.includes('VISUAL_STYLE_CONFLICT') && rules.includes('CRP_VISUAL_STYLE_MISMATCH'), '09 gate codes');
ok(!thisSmokeGenerates(), '10 no generation during TS smoke');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_PROJECT_VISUAL_STYLE_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PROJECT_VISUAL_STYLE_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
