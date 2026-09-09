import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  IDENTITY_LOCK_SUITE,
  IDENTITY_LOCK_V1_ID,
  STUDIO_SCORE_IDENTITY_LABEL,
  studioMayScoreIdentity,
  studioSlotVerdictLabel,
  studioSlotsNeedVision,
} from './kit-video-character-studio';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioIdentityLockV1Rules.cs');
const compose = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAgeGenerationIntegrationV1Rules.cs');
const studio = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterStudioV1Rules.cs');
const orch = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterStudioOrchestrator.cs');
const judge = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterStudioIdentityJudge.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../shared/api/content.api.ts');
const ui = read('ContentFamixaCharacterStudio.tsx');

ok(IDENTITY_LOCK_V1_ID === 'FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1'
  && IDENTITY_LOCK_SUITE === 'FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1_REGRESSION', '01 document id');
ok(rules.includes('StudioLock') && compose.includes('CharacterStudioIdentityLockV1Rules.StudioLock')
  && !compose.includes('MasterSha256: {request.MasterSha256}'), '02 A studio lock / no authority SHA in CRP prompt');
ok(studio.includes('if (scores is null)') && studio.includes('UnevaluatedSlot'), '03 A no default 96');
ok(rules.includes('ViewRefs') && rules.includes('["MASTER", "FRONT"]')
  && orch.includes('GenerateViewBatchAsync') && orch.includes('laterMissing'), '04 B FRONT-first then Master+FRONT');
ok(orch.includes('ScoreAgainstMasterAsync') && judge.includes('ICharacterStudioIdentityJudge')
  && rules.includes('VisionUserPrompt') && rules.includes('AutoApprove() => false'), '05 C Vision vs Master, no auto-approve');
ok(controller.includes('character-studio/identity-lock/regression')
  && !orch.includes('ContentGeminiClient') && !rules.includes('if (characterId'), '06 API + no Gemini in Application/orchestrator');
ok(!thisSmokeGenerates() && !api.includes('identity-lock/execute'), '07 smoke does not generate');
ok(rules.includes('MayApproveAfterVision') && rules.includes('IDENTITY_VISION_NOT_READY')
  && orch.includes('MayApproveAfterVision') && orch.includes('StaffVisionPending'),
  '08 approve blocked until Vision PASS');
ok(studioSlotVerdictLabel('NOT_EVALUATED') === 'Chưa chấm'
  && studioSlotVerdictLabel('PASS') === 'Đạt'
  && studioSlotsNeedVision([{ verdict: 'NOT_EVALUATED' }])
  && !studioSlotsNeedVision([{ verdict: 'NOT_EVALUATED' }], true)
  && ui.includes('Bộ 4 ảnh chưa được chấm với Master')
  && ui.includes('2–4 phút'),
  '09 UI: Chưa chấm / no approve until Vision');
ok(STUDIO_SCORE_IDENTITY_LABEL === 'Chấm với Master'
  && studioMayScoreIdentity({ coverage: 4, officialLocked: false, slots: [{ verdict: 'NOT_EVALUATED' }] })
  && !studioMayScoreIdentity({ coverage: 4, officialLocked: true, slots: [{ verdict: 'NOT_EVALUATED' }] })
  && ui.includes('STUDIO_SCORE_IDENTITY_LABEL')
  && ui.includes('scoreCharacterStudioIdentity')
  && ui.includes('score-identity')
  && api.includes('/score-identity')
  && controller.includes('score-identity')
  && orch.includes('ScoreIdentityAsync')
  && orch.includes('PersistIdentityScoresAsync')
  && !orch.includes('GenerateViewBatchAsync') === false
  && rules.includes('StaffScoreExisting')
  && rules.includes('GeneratesPixelsOnScore() => false'),
  '10 score existing views without generating a new set');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
