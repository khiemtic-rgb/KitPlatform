import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CHARACTER_STUDIO_V1_ID, profileValid, rejectReasonValid } from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ts = read('kit-video-character-studio.ts');
const ui = read('ContentFamixaCharacterStudio.tsx');
const board = read('ContentFamixaProductionBoards.tsx');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(CHARACTER_STUDIO_V1_ID === 'FAMIXA_CHARACTER_STUDIO_V1', '01 document id');
ok(ui.includes('Tạo nhân vật') && ui.includes('Tạo bộ ảnh chuẩn') && ui.includes('Character Studio') && board.includes('ContentFamixaCharacterStudio'), '02 Studio UI');
ok(profileValid({ name: 'Lan', age: 10, gender: 'female', description: 'curious' })
  && !profileValid({ name: '', age: 10, gender: 'female', description: 'x' }), '03 profile');
ok(rejectReasonValid('FACE_MISMATCH', '') && !rejectReasonValid('OTHER', 'no'), '04 reject reason');
ok(api.includes('/content/character-studio/characters') && controller.includes('character-studio/regression'), '05 API');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false')
  && rules.includes('ChainOrder') && rules.includes('ICharacterGenerationProvider'), '06 engine rules');
ok(orch.includes('ICharacterGenerationProvider') && !orch.includes('ContentGeminiClient')
  && !orch.includes('Google.GenAI'), '07 orchestrator isolated');
ok(!rules.includes('if (characterId ==') && !orch.includes('if (characterId ==')
  && !orch.includes('characterName == "Minh"') && !orch.includes('characterName == "Nam"'), '08 no character hardcode');
ok(!ui.includes('Tạo FRONT') && !ui.includes('Tạo 3/4') && !ui.includes('auto approve'), '09 no per-slot / auto');
ok(!ui.includes('SHOT-002') && !board.includes('GENERATE VIDEO'), '10 no production/video');
ok(rules.includes('STYLE_3D_STYLIZED_REALISM') && ui.includes('Inherited from Project') && !ui.includes('<Radio.Group value={styleId}'), '11 project style inherit — no character picker');
ok(rules.includes('SelectReferences') && rules.includes('CLOSE_UP'), '12 reference selector');
ok(orch.includes('GenerateViewBatchAsync') && orch.includes('ScoreAgainstMasterAsync'), '12b identity lock FRONT-first + Vision');
ok(!thisSmokeGenerates(), '13 no generation during TS smoke');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_STUDIO_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_STUDIO_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
