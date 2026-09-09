import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CHARACTER_REFERENCE_PACK_ID,
  OPTIONAL_REFERENCE_TYPES,
  REQUIRED_REFERENCE_TYPES,
  nextWork,
  viewLabel,
} from './kit-video-character-reference-pack';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-character-reference-pack.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoCharacterReferencePackCard.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/CharacterReferencePackRules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/CharacterReferencePackService.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

ok(CHARACTER_REFERENCE_PACK_ID === 'CHARACTER_REFERENCE_PACK_V1', '01 document id');
ok(REQUIRED_REFERENCE_TYPES.join(',') === 'FRONT,THREE_QUARTER,SIDE,FULL_BODY', '02 required views');
ok(OPTIONAL_REFERENCE_TYPES.includes('THINKING') && OPTIONAL_REFERENCE_TYPES.includes('HAPPY'), '03 optional expressions');
ok(viewLabel('THREE_QUARTER') === '3/4' && viewLabel('SIDE') === 'Nghiêng', '04 staff labels');
ok(
  nextWork({ canCreate: true, masterLocked: true, dnaLocked: true, characterName: '', authority: [] }).action ===
    'Tạo bộ tham chiếu',
  '05 create next action from empty pack',
);
ok(!ts.includes('evaluateIdentity') && !ts.includes('PackSha'), '06 TS has no governance evaluator');
ok(!api.includes("fetchCharacterReferencePack(characterId = 'CHAR-001')"), '07 API no default CHAR-001');
ok(api.includes('requireCharacterId(characterId)') && api.includes('Thiếu mã nhân vật'), '08 empty characterId blocked');
ok(ui.includes('Góc nhìn nhân vật') && ui.includes('Director Review') && ui.includes('Xem thông tin hệ thống'), '09 staff sections');
ok(ui.includes('Thông tin nhân vật') && ui.includes('Những điều không được thay đổi') && ui.includes('fx-crp__tabs'), '09b staff tabs');
ok(!ui.includes('GENERATE NOW') && !ui.includes('gemini') && !ui.includes('runway') && !ui.includes('Generation Ready'), '10 no generate CTA');
ok(rules.includes('EvaluateAuthorityGate') && rules.includes('EvaluateIdentity') && rules.includes('NEEDS_REVIEW'), '11 C# identity gate');
ok(!service.includes('Gemini') && !service.includes('Runway'), '12 service no provider');
ok(!service.includes('AutoApprove') && service.includes('EnsureDirector'), '13 no auto-approve');
ok(service.includes('OfficialMasterRow') && service.includes('ToWorkspaceDto') && rules.includes('CanCreateOfficialPack'), '13b workspace CRP, no official insert without Master row');
const crpSlice = controller.slice(
  controller.indexOf('GetCharacterReferencePack'),
  controller.indexOf('GetProductionReferencePack'),
);
ok(crpSlice.includes('character-reference-pack') && !crpSlice.includes('CHAR-001'), '14 controller no default Minh');
ok(rules.includes('CreatesPixels("GEMINI")') || rules.includes('GEMINI'), '15 gemini flagged as pixel create');
ok(rules.includes('VALIDATED') && rules.includes('REFERENCE_PACK_IDENTITY_CONFLICT') && rules.includes('EnsureRejectReason'), '16 contract statuses');
ok(rules.includes('CanUse') && rules.includes('prp_sha') && rules.includes('REFERENCE_ASSET_MISSING'), '18 PRP + canUse SoT');
ok(!ui.includes('REFERENCE_MISSING') && (ui.includes('UPLOAD ẢNH CÓ SẴN') || ui.includes('STAFF_UPLOAD_EXISTING')) && ui.includes('Thiếu:'), '19 staff language no technical missing code');
ok(!service.includes('Veo') && rules.includes('"VEO"'), '17 veo flagged, service isolated');

if (fail.length) {
  console.error(`CHARACTER_REFERENCE_PACK_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('CHARACTER_REFERENCE_PACK_V1_SMOKE PASS FAIL=0 (file/SoT scan; C# cases via /character-reference-pack/regression)');
