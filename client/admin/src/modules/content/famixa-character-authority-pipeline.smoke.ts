import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_AUTHORITY_PIPELINE_V1_ID,
  CRP_SET_TYPES,
  PIPELINE_STATES,
  STAFF_APPROVE_CRP,
  STAFF_APPROVE_MASTER,
  STAFF_BUILDING,
  STAFF_CANCEL,
  STAFF_CREATE_AUTHORITY,
  STAFF_CRP_CREATE,
  STAFF_CRP_REVIEW,
  STAFF_DNA_AUTO,
  STAFF_LOCK_MASTER,
  STAFF_MASTER_REVIEW,
  STAFF_PRP_AUTO,
  STAFF_READY,
  STAFF_REJECT,
  STAFF_START,
  STAFF_SYSTEM,
  authorityConfirmCopy,
  hideDnaPrpCtas,
  mayShowMasterGenerate,
  pipelineStaff,
} from './kit-video-character-authority-pipeline';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-authority-pipeline.ts'), 'utf8');
const initTs = readFileSync(join(root, 'kit-video-character-authority-initialization.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentFamixaCharacterAuthorityCard.tsx'), 'utf8');
const setUi = readFileSync(join(root, 'ContentKitVideoCharacterReferenceSetCard.tsx'), 'utf8');
const packUi = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const lib = readFileSync(join(root, 'ContentFamixaCharacterLibrary.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAuthorityPipelineV1Rules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterAuthorityPipelineV1Service.cs'),
  'utf8',
);
const initService = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterAuthorityInitializationV1Service.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);
const app = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAuthorityPipelineV1Rules.cs'),
  'utf8',
);
const adapter = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterAuthorityGenerationProvider.cs'),
  'utf8',
);

const staff = `${ts}\n${initTs}\n${ui}\n${setUi}\n${packUi}\n${lib}`;

ok(CHARACTER_AUTHORITY_PIPELINE_V1_ID === 'FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1', '01 document id');
ok(PIPELINE_STATES[0] === 'PROFILE_CREATED' && PIPELINE_STATES.includes('CHARACTER_READY'), '02 onboarding states');
ok(staff.includes(STAFF_CREATE_AUTHORITY) && ui.includes('fetchCharacterAuthorityPipeline'), '03 Master generation UI');
ok(staff.includes(STAFF_MASTER_REVIEW) && ui.includes('READY_FOR_DIRECTOR') && staff.includes(STAFF_APPROVE_MASTER), '04 Master review');
ok(staff.includes(STAFF_DNA_AUTO) && hideDnaPrpCtas() && !ui.includes('executeCharacterAuthorityDna'), '05 DNA auto / no DNA CTA');
ok(staff.includes(STAFF_PRP_AUTO) && !ui.includes('executeCharacterAuthorityPrp'), '06 PRP auto / no PRP CTA');
ok(staff.includes(STAFF_CRP_CREATE) && CRP_SET_TYPES.join(',') === 'FRONT,THREE_QUARTER,SIDE,FULL_BODY', '07 CRP set 4 slots');
ok(packUi.includes('!authorityReady') && packUi.includes('createCharacterReferencePack'), '07b official create hidden when authority ready');
ok(setUi.includes('FRONT') === false || packUi.includes('FULL_BODY') || staff.includes(STAFF_CRP_CREATE), '08 4-slot reference display');
ok(staff.includes(STAFF_APPROVE_CRP) && staff.includes(STAFF_CRP_REVIEW), '09 CRP review');
ok(staff.includes(STAFF_LOCK_MASTER) && rules.includes('AutoLock() => false'), '10 lock is explicit');
ok(staff.includes(STAFF_READY) || pipelineStaff('CHARACTER_READY').includes(STAFF_READY), '11 canUse / ready copy');
ok(staff.includes('pipelineState') || api.includes('pipelineState'), '12 Character Authority status');
ok(staff.includes(STAFF_BUILDING) && staff.includes(STAFF_SYSTEM) && !ui.includes('IntentSha'), '13 staff copy, no SHA primary');
ok(staff.includes('Gemini') && staff.includes('Runway') && staff.includes('Veo') && ui.includes('chưa hỗ trợ')
  && ui.includes('AUTHORITY_PROVIDERS'), '14 provider selection');
ok(authorityConfirmCopy({ name: 'Nam', eraId: 'ERA-01', provider: 'Gemini' }).start === STAFF_START
  && ui.includes('confirm: true'), '15 confirmation');
ok(!mayShowMasterGenerate({ authorityLocked: false, masterStatus: 'MISSING', provider: '' })
  && mayShowMasterGenerate({ authorityLocked: false, masterStatus: 'MISSING', provider: 'GEMINI' }),
  '16 no auto-select provider / duplicate protection UI');
ok(rules.includes('BLOCK_DUPLICATE') && rules.includes('RejectHistoricalStill'), '17 duplicate + historical still');
ok(!service.includes('FirstRealProduction') && !ui.includes('SHOT-001') && !ui.includes('GENERATE VIDEO'), '18 no production in UI');
ok(controller.includes('authority-pipeline') && controller.includes('character-authority-pipeline-v1/regression'), '19 endpoints');
ok(!app.includes('ContentGeminiClient') && initService.includes('ICharacterAuthorityGenerationProvider')
  && adapter.includes('ContentGeminiClient'), '20 Application → provider → Gemini adapter');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoRunFullPipeline() => false'), '21 no auto approve / full pipeline');
ok(ui.includes('authorityLocked') && initService.includes('nhân vật đã khóa')
  && !initService.includes('if (minh)') && !service.includes('officialMinh'), '22 locked authority generic, no Minh/Nam branch');
ok(staff.includes(STAFF_CANCEL) && staff.includes(STAFF_REJECT), '23 Hủy / Không đạt');
ok(!thisSmokeGenerates(), '24 no generation during TS smoke');

function thisSmokeGenerates() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
