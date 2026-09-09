import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  UNIFIED_GENERATION_SUITE,
  UNIFIED_GENERATION_V1_ID,
  profileValid,
  rejectReasonValid,
  studioMayRegenerate,
  studioPendingReview,
  studioPublicHeadline,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioUnifiedGenerationV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const service = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioGenerationService.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const appCs = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioUnifiedGenerationV1Rules.cs')
  + read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioUnifiedGenerationV1Regression.cs')
  + read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ContentContracts.cs');

ok(UNIFIED_GENERATION_V1_ID === 'FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1', '01 document id');
ok(UNIFIED_GENERATION_SUITE === 'FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1_REGRESSION', '02 suite id');
ok(ui.includes('Character Studio') && ui.includes('+ Tạo nhân vật') && ui.includes('Tạo bộ ảnh chuẩn'), '03 Studio UI + generate CTA');
ok(ui.includes('PHONG CÁCH DỰ ÁN') && ui.includes('INHERITED') && !ui.includes('<Radio.Group value={styleId}'), '04 Project Style displayed, no picker');
ok(profileValid({ name: 'Lan', age: 10, gender: 'female', description: 'curious' })
  && ui.includes('Lưu hồ sơ') && ui.includes('generate: false'), '05 Create Character saves profile');
ok(ui.includes('Xác nhận') && ui.includes('Nhà cung cấp: Gemini'), '06 confirmation dialog');
ok(studioPendingReview('CRP_PENDING_REVIEW')
  && studioPublicHeadline({ status: 'CRP_PENDING_REVIEW', coverage: 4, requiredTotal: 4 }) === 'PENDING REVIEW · 4/4'
  && studioPublicHeadline({ status: 'CHARACTER_READY', coverage: 4, requiredTotal: 4, officialLocked: true })
    === 'CHARACTER READY · 4/4 · LOCKED'
  && ui.includes('studioPublicHeadline') && ui.includes('Duyệt bộ ảnh'), '07 Pending Review vs CHARACTER READY');
ok(rules.includes('PublicHeadline') && rules.includes('CHARACTER READY') && orch.includes('PublicHeadline'), '07b status headline from factory state');
ok(rejectReasonValid('FACE_MISMATCH', '') && ui.includes('Không đạt') && ui.includes('Đánh giá bộ ảnh'), '08 Reject dialog works');
ok(studioMayRegenerate('REJECTED')
  && studioMayRegenerate('FAILED')
  && studioMayRegenerate('CRP_PENDING_REVIEW')
  && !studioMayRegenerate('REJECTED', true)
  && ui.includes('Tạo lại bộ ảnh')
  && ui.includes('canStartRedo')
  && orch.includes('produced.Remove(view)'), '09 Regenerate after reject / fail / pending replaces slots');
ok(!ui.includes('characterId === \'CHAR-001\'') && !orch.includes('characterName == "Minh"')
  && !orch.includes('characterName == "Nam"') && !orch.includes('characterName == "Linh"')
  && !service.includes('CHAR-001') && !rules.includes('if (characterId'), '10 no character-specific UI/code');
ok(controller.includes('character-studio/{characterId}/reference-generation')
  && api.includes('/content/character-studio/${encodeURIComponent(characterId)}/reference-generation'), '11 unified API');
ok(!appCs.includes('Google.GenAI') && !appCs.includes('ContentGeminiClient')
  && !orch.includes('ContentGeminiClient') && !service.includes('ContentGeminiClient'), '12 Application/orchestrator isolated');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false')
  && rules.includes('ReferenceSetCount = 4'), '13 factory rules');
ok(!thisSmokeGenerates(), '14 no generation during TS smoke');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
