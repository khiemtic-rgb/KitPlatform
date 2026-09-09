import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_AUTHORITY_INITIALIZATION_V1_ID,
  STAFF_APPROVE_MASTER,
  STAFF_CANCEL,
  STAFF_CREATE_DNA,
  STAFF_CREATE_MASTER,
  STAFF_CREATE_PRP,
  STAFF_DETAILS,
  STAFF_LOCK_MASTER,
  STAFF_NEED_MASTER,
  STAFF_READY,
  STAFF_REJECT,
  STAFF_START,
  STAFF_WAIT_AUTHORITY,
  canOfferMaster,
  masterConfirmCopy,
} from './kit-video-character-authority-initialization';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-authority-initialization.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentFamixaCharacterAuthorityCard.tsx'), 'utf8');
const lib = readFileSync(join(root, 'ContentFamixaCharacterLibrary.tsx'), 'utf8');
const packUi = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAuthorityInitializationV1Rules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterAuthorityInitializationV1Service.cs'),
  'utf8',
);
const adapter = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterAuthorityGenerationProvider.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);
const app = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterAuthorityInitializationV1Rules.cs'),
  'utf8',
);

const staff = `${ts}\n${ui}\n${lib}\n${packUi}`;
ok(CHARACTER_AUTHORITY_INITIALIZATION_V1_ID === 'FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1', '01 document id');
ok(staff.includes('Character Authority') && staff.includes(STAFF_CREATE_MASTER) && staff.includes(STAFF_CREATE_DNA) && staff.includes(STAFF_CREATE_PRP), '02 stages');
ok(staff.includes(STAFF_NEED_MASTER) && ui.includes('READY_FOR_DIRECTOR') && staff.includes(STAFF_APPROVE_MASTER), '03 Master state + review');
ok(staff.includes('Chờ Master') && staff.includes(STAFF_CREATE_DNA), '04 DNA after Master');
ok(staff.includes('Chờ DNA') && staff.includes(STAFF_CREATE_PRP), '05 PRP after DNA');
ok(staff.includes(STAFF_REJECT) && staff.includes(STAFF_LOCK_MASTER), '06 Director review + lock');
ok(staff.includes(STAFF_READY) || staff.includes('sẵn sàng tạo bộ ảnh tham chiếu'), '07 readiness');
ok(staff.includes('Gemini') && staff.includes('Runway') && staff.includes('Veo') && ui.includes('chưa hỗ trợ'), '08 provider selection');
ok(masterConfirmCopy({ name: 'Nam', eraId: 'ERA-01', provider: 'Gemini' }).start === STAFF_START
  && ui.includes('confirm: true'), '09 confirmation');
ok(!canOfferMaster({ authorityLocked: false, masterStatus: 'MISSING', provider: '' })
  && canOfferMaster({ authorityLocked: false, masterStatus: 'MISSING', provider: 'GEMINI' }),
  '10 no auto-select provider');
ok(!ui.includes('auto_approve') && rules.includes('AutoApprove() => false'), '11 no auto approve');
ok(!ui.includes('auto_lock') && rules.includes('AutoLock() => false') && !service.includes('LockMasterAsync') === false, '12 lock is explicit');
ok(!service.includes('FirstRealProduction') && !ui.includes('Tạo hình') && !ui.includes('SHOT-001'), '13 no production generation');
ok(!service.includes('IVideoGeneration') && !ui.includes('GENERATE VIDEO'), '14 no video generation');
ok(staff.includes(STAFF_DETAILS) && !ui.includes('IntentSha') && !ui.includes('providerRequestId'), '15 no technical copy on primary UI');
ok(staff.includes(STAFF_WAIT_AUTHORITY) && lib.includes('ContentFamixaCharacterAuthorityCard'), '16 library workflow');
ok(controller.includes('authority-initialization/master/execute')
  && controller.includes('character-authority-initialization-v1/regression'), '17 endpoints');
ok(!app.includes('ContentGeminiClient') && service.includes('ICharacterAuthorityGenerationProvider')
  && adapter.includes('ContentGeminiClient'), '18 Application → provider → Gemini adapter');
ok(rules.includes('RejectHistoricalStill') && rules.includes('AutoRetry() => false'), '19 no fallback / no retry');
ok(staff.includes(STAFF_CANCEL) && staff.includes(STAFF_START), '20 Hủy / Bắt đầu tạo');

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1_SMOKE PASS FAIL=0 (file/SoT scan; C# via /character-authority-initialization-v1/regression)');
