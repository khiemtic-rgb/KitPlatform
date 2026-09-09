import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_REFERENCE_GENERATION_ID,
  STAFF_ACCEPT,
  STAFF_CREATE_FULL_BODY,
  STAFF_CREATED,
  STAFF_DETAILS,
  STAFF_REJECT,
  STAFF_START,
  STAFF_UPLOAD_EXISTING,
  STAFF_WAITING,
  angleLabel,
  canOfferGenerate,
  confirmCopy,
  generateCta,
  STAFF_REGENERATE_FULL_BODY,
} from './kit-video-character-reference-generation';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-reference-generation.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoCharacterReferenceGenerationCard.tsx'), 'utf8');
const packUi = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const staff = `${ts}\n${ui}\n${packUi}`;
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferenceGenerationRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterReferenceGenerationService.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

ok(CHARACTER_REFERENCE_GENERATION_ID === 'FAMIXA_CHARACTER_REFERENCE_GENERATION_V1', '01 document id');
ok(generateCta('FULL_BODY') === STAFF_CREATE_FULL_BODY && angleLabel('FULL_BODY') === 'Toàn thân', '02 missing FULL_BODY CTA');
ok(staff.includes(STAFF_CREATE_FULL_BODY) && staff.includes(STAFF_UPLOAD_EXISTING) && staff.includes(STAFF_REGENERATE_FULL_BODY), '03 CTA TẠO ẢNH / TẠO LẠI + upload secondary');
ok(confirmCopy({ character: 'Minh', angle: 'Toàn thân', provider: 'Gemini' }).start === STAFF_START, '04 confirmation');
ok(staff.includes('Nhà cung cấp hình ảnh') && staff.includes('Gemini') && !ui.includes('autoSelect'), '05 provider selection');
ok(staff.includes(STAFF_START) && ui.includes('confirm: true'), '06 generate confirmation');
ok(staff.includes(STAFF_WAITING) && staff.includes(STAFF_CREATED) && ui.includes('READY_FOR_DIRECTOR'), '07 READY_FOR_DIRECTOR');
ok(staff.includes(STAFF_ACCEPT) && staff.includes(STAFF_REJECT), '08 ACCEPT / REJECT');
ok(rules.includes('AutoApprove() => false') && rules.includes('AutoLock() => false') && ui.includes('auto_approve') === false, '09 no auto approve');
ok(rules.includes('AcceptLocks() => false') && !service.includes('LockAsync'), '10 no auto lock');
ok(!service.includes('FirstRealProduction') && !ui.includes('SHOT-001') && !ui.includes('Tạo hình'), '11 no shot generation');
ok(!service.includes('IVideoGeneration') && !service.includes('Runway') && !ui.includes('Veo'), '12 no video generation');
ok(staff.includes(STAFF_DETAILS) && ui.includes('Collapse') && !ui.includes('Intent SHA'), '13 technical details hidden by default');
ok(canOfferGenerate({ missing: true, provider: 'GEMINI' }) && !canOfferGenerate({ missing: true, provider: '' }), '14 provider not auto-selected');
ok(controller.includes('reference-generation/execute') && controller.includes('reference-generation/prepare'), '15 endpoints');
ok(!ts.includes('content.api') && !rules.includes('ContentGeminiClient'), '16 TS map + Application no Gemini client');
ok(service.includes('IImageGenerationProvider') && service.includes('IGeminiImageGenerationProvider'), '17 provider port');
ok(service.includes('first_real_production = false'), '18 no First Real Production');
ok(service.includes('FindSlotOnDisk') && service.includes('AttachLatestAcceptedAsync') && service.includes('if (slots[type] is JsonObject)'), '19 newest candidate wins + register by executionId');
ok(ui.includes('Kiểm tra bộ ảnh') && ui.includes('Đưa ảnh này vào khung') && ui.includes('onClose()'), '20 validate closes and re-attaches');

if (fail.length) {
  console.error(`FAMIXA_CHARACTER_REFERENCE_GENERATION_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_CHARACTER_REFERENCE_GENERATION_V1_SMOKE PASS FAIL=0 (file/SoT scan; C# via /character-reference-generation/regression)');
