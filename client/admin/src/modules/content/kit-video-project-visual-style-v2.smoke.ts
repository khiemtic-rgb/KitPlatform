import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  PROJECT_VISUAL_STYLE_V1_ID,
  PROJECT_VISUAL_STYLE_V2_ID,
  PROJECT_VISUAL_STYLE_V2_SUITE,
  visualStyleRevisionMayApprove,
  visualStyleRevisionMayLock,
  visualStyleRevisionMayRequest,
} from './kit-video-project-visual-style';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualStyleV2Rules.cs');
const v1 = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualStyleV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const auth = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProjectVisualStyleAuthority.cs');
const card = read('ContentFamixaProjectVisualStyleCard.tsx');
const api = read('../../shared/api/content.api.ts');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(PROJECT_VISUAL_STYLE_V2_ID === 'FAMIXA_PROJECT_VISUAL_STYLE_V2'
  && PROJECT_VISUAL_STYLE_V2_SUITE === 'FAMIXA_PROJECT_VISUAL_STYLE_V2_REGRESSION'
  && PROJECT_VISUAL_STYLE_V1_ID === 'FAMIXA_PROJECT_VISUAL_STYLE_V1', '01 document ids');
ok(rules.includes('StylePromptBlock') && rules.includes('NOT_PHOTOREALISTIC')
  && rules.includes('CanonicalStyleDescription') && rules.includes('StylizationLevel = "STRONG"')
  && rules.includes('AutoApprove() => false') && rules.includes('OverwritesV1() => false'), '02 V2 style + no auto');
ok(!rules.includes('if (characterId') && !rules.includes('if (characterName')
  && !v1.includes('FAMIXA 3D Stylized Realism: a premium'), '03 no character branch; V1 prompt unchanged');
ok(controller.includes('character-studio/project-visual-style/v2/regression')
  && controller.includes('project-visual-style/{projectId}/revision/approve')
  && api.includes('/content/character-studio/project-visual-style/v2'), '04 APIs');
ok(card.includes('Tạo Visual Style Revision') && card.includes('Khóa Visual Style')
  && card.includes('CURRENT AUTHORITY') && card.includes('Authority cấp Project')
  && !card.includes('auto approve'), '05 UI review CTAs');
ok(visualStyleRevisionMayRequest('') && visualStyleRevisionMayApprove('PENDING_REVIEW')
  && visualStyleRevisionMayLock('APPROVED') && !visualStyleRevisionMayLock('PENDING_REVIEW'), '06 gates');
ok(orch.includes('CompileAuthorityPrompt') && auth.includes('RunRevisionRegression')
  && !orch.includes('ContentGeminiClient') && !rules.includes('Google.GenAI'), '07 compile + no Gemini');
ok(!thisSmokeGenerates() && !api.includes('revision/execute') && !card.includes('Generate Character'), '08 smoke does not generate');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ProjectVisualStyleV2Regression.cs');
ok(regression.includes('01 same project') && regression.includes('23 four CRP views')
  && regression.includes('30 Nam 38') && regression.includes('31 age 11')
  && regression.includes('StyleForCharacter'), '09 24-case + semantic compile');
ok(auth.includes('GetV2Async') && controller.includes('GetProjectVisualStyleV2'), '10 GET V2 definition');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_PROJECT_VISUAL_STYLE_V2_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PROJECT_VISUAL_STYLE_V2_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
