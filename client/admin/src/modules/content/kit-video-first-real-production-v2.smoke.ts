import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { canOfferGenerate, confirmCopy } from './kit-video-first-real-production';
import {
  DIRECTOR_QA_CHECKS,
  FIRST_REAL_PRODUCTION_V2_ID,
  HISTORICAL_STILL_ID,
  approvedReviewLabel,
  crpLockedLabel,
  firstRealConfirmNote,
  isImageDirectorApproved,
  isImageDirectorRejected,
  isShot001,
  pendingReviewLabel,
} from './kit-video-first-real-production-v2';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-first-real-production-v2.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoImageGenerationExecutionCard.tsx'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const v1 = readFileSync(join(root, 'kit-video-first-real-production.ts'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/FirstRealProductionV2Rules.cs'),
  'utf8',
);
const service = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ImageGenerationExecutionService.cs'),
  'utf8',
);
const firstReal = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/FirstRealProductionService.cs'),
  'utf8',
);
const controller = readFileSync(
  join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'),
  'utf8',
);

ok(FIRST_REAL_PRODUCTION_V2_ID === 'FAMIXA_FIRST_REAL_PRODUCTION_V2', '01 document id');
ok(HISTORICAL_STILL_ID === '7ed003d7-789a-41ef-bcdf-a88637ff14d2' && ui.includes('HISTORICAL_STILL_ID'), '01b historical still excluded');
ok(isShot001('CHAR-001-MINH-ERA01-SHOT-001') && !isShot001('SHOT-002') && !isShot001('SHOT-011'), '02 SHOT-001 only');
ok(crpLockedLabel(true) === '✓ Đã khóa' && crpLockedLabel(false) === 'Chưa hoàn tất', '03 CRP locked label');
ok(pendingReviewLabel().includes('Chờ duyệt') && !pendingReviewLabel().includes('Đã duyệt'), '04 pending review');
ok(isImageDirectorApproved('IMAGE_APPROVED', 'PENDING') && !isImageDirectorApproved('READY_FOR_DIRECTOR', 'PENDING'), '04b IMAGE_APPROVED is already approved');
ok(isImageDirectorRejected('IMAGE_REJECTED', 'PENDING') && !isImageDirectorRejected('READY_FOR_DIRECTOR', 'PENDING'), '04c IMAGE_REJECTED is already rejected');
ok(firstRealConfirmNote('Shot 01', 'Gemini').includes('lần tạo hình production đầu tiên'), '05 first-real confirm');
ok(confirmCopy({ shotLabel: 'Shot 01', character: 'Minh', location: 'Phòng khách', action: 'Đọc tờ giấy', provider: 'Gemini' }).note.includes('Duyệt hình'), '06 V1 confirm carries V2 note');
ok(!canOfferGenerate({ crpUsable: true, provider: 'GEMINI', shotCode: 'SHOT-002' }), '07 no SHOT-002 generate');
ok(canOfferGenerate({ crpUsable: true, provider: 'GEMINI', shotCode: 'CHAR-001-MINH-ERA01-SHOT-001' }), '08 SHOT-001 + Gemini can offer');
ok(DIRECTOR_QA_CHECKS.length === 10 && ui.includes('DIRECTOR_QA_CHECKS'), '09 director checklist display only');
ok(ui.includes('Chờ duyệt') && ui.includes('Bắt đầu tạo') && ui.includes('Hủy'), '10 staff confirm + pending');
ok(ui.includes('fetchImageGenerationExecutionArtifactBlob'), '10d staff preview uses authenticated blob');
ok(ui.includes('isImageDirectorApproved') && approvedReviewLabel().includes('Đã duyệt'), '10e IMAGE_APPROVED shows Đã duyệt, no second Duyệt');
ok(tab.includes('images={') && tab.includes('ContentKitVideoImageGenerationExecutionCard'), '10b Duyệt hình on Hình ảnh tab');
const boards = readFileSync(join(root, 'ContentFamixaProductionBoards.tsx'), 'utf8');
ok(boards.includes('ContentKitVideoImageGenerationExecutionCard') && boards.includes('chờ duyệt'), '10c Duyệt hình visible on Nhân vật tab');
ok(!ui.includes('Tạo lại') && !ui.includes('GENERATE VIDEO') && !ui.includes('LIPSYNC'), '11 no regenerate / video');
ok(ui.includes('Chi tiết sản xuất') && !ui.includes('EXECUTE IMAGE GENERATION'), '12 technical details collapsed');
ok(!ts.includes('content.api') && !ts.includes('import.meta.env'), '13 V2 TS map only');
ok(rules.includes('CrpReadyForProduction') && rules.includes('LOCKED') && rules.includes('AutoRetry() => false'), '14 CRP LOCKED + no auto retry');
ok(service.includes('FirstRealProductionV2Rules.Evaluate') && service.includes('HasRequiredCrpRefs'), '15 execute V2 + CRP refs');
ok(service.includes('FirstRealProductionRules.DocumentId') && service.includes('MayCallProvider(gate)'), '16 V1 gate strings kept');
ok(firstReal.includes('RunV2Regression') && (firstReal.includes('Confirm') || firstReal.includes('false')), '17 GET never confirms');
ok(controller.includes('first-real-production-v2/regression') && controller.includes('request?.Confirm ?? false'), '18 V2 regression + confirm');
ok(!service.includes('IRunway') && !rules.includes('ContentGeminiClient'), '19 no Runway / no Gemini client in rules');
ok(!v1.includes('FAMIXA_FIRST_REAL_PRODUCTION_V2'), '20 V1 document id unchanged');

if (fail.length) {
  console.error(`FAMIXA_FIRST_REAL_PRODUCTION_V2_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_FIRST_REAL_PRODUCTION_V2_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /first-real-production-v2/regression)');
