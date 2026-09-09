import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_REFERENCE_AUTO_GENERATION_V2_ID,
  STAFF_APPROVE,
  STAFF_CANCEL,
  STAFF_CREATE_SET,
  STAFF_DETAILS,
  STAFF_PENDING,
  STAFF_REJECT,
  STAFF_START,
  canOfferSet,
  pendingSetLabel,
  setConfirmCopy,
} from './kit-video-character-reference-generation-v2';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-reference-generation-v2.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoCharacterReferenceSetCard.tsx'), 'utf8');
const packUi = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferenceAutoGenerationV2Rules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterReferenceAutoGenerationV2Service.cs'),
  'utf8',
);
const adapter = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/GeminiCharacterReferenceGenerationProvider.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

const staff = `${ts}\n${ui}\n${packUi}`;
ok(CHARACTER_REFERENCE_AUTO_GENERATION_V2_ID === 'FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2', '01 document id');
ok(staff.includes(STAFF_CREATE_SET) && packUi.includes('ContentKitVideoCharacterReferenceSetCard'), '02 set button on pack card');
ok(setConfirmCopy({ name: 'Nam', characterId: 'CHAR-002', eraId: 'ERA-01', provider: 'Gemini' }).start === STAFF_START
  && ui.includes('confirm: true'), '03 confirmation');
ok(staff.includes('Gemini') && staff.includes('Runway') && staff.includes('Veo') && ui.includes('chưa hỗ trợ'), '04 provider selection');
ok(!canOfferSet({ authorityReady: true, provider: '', locked: false, alreadyComplete: false })
  && canOfferSet({ authorityReady: true, provider: 'GEMINI', locked: false, alreadyComplete: false }),
  '05 provider not auto-selected');
ok(staff.includes(STAFF_CANCEL) && staff.includes(STAFF_START), '06 Hủy / Bắt đầu tạo');
ok(pendingSetLabel(4).includes(STAFF_PENDING) && staff.includes(STAFF_APPROVE) && staff.includes(STAFF_REJECT), '07 4 slots pending review');
ok(!ui.includes('Sẵn sàng production') && !ui.includes('READY FOR PRODUCTION'), '08 no production CTA after generation');
ok(staff.includes(STAFF_DETAILS) && !ui.includes('IntentSha') && !ui.includes('providerRequestId'), '09 no technical copy on primary UI');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false') && !ui.includes('auto_approve'), '10 no auto approve');
ok(rules.includes('AutoLock() => false') && !service.includes('LockAsync'), '11 no auto lock');
ok(!service.includes('FirstRealProduction') && !ui.includes('SHOT-001') && !ui.includes('Tạo hình'), '12 no production generation');
ok(!service.includes('IVideoGeneration') && !ui.includes('GENERATE VIDEO'), '13 no video');
ok(controller.includes('reference-generation-set/execute') && controller.includes('character-reference-auto-generation-v2/regression'), '14 endpoints');
ok(!ts.includes('content.api') && !rules.includes('ContentGeminiClient'), '15 TS map + Application no Gemini client');
ok(service.includes('ICharacterReferenceGenerationProvider') && adapter.includes('ContentGeminiClient'), '16 Application → provider → Gemini adapter');
ok(rules.includes('RejectHistoricalStill') && rules.includes('7ed003d7') === false
  && service.includes('RejectHistoricalStill'), '17 historical still rejected');

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2_SMOKE PASS FAIL=0 (file/SoT scan; C# via /character-reference-auto-generation-v2/regression)');
