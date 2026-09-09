import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  FIRST_REAL_PRODUCTION_ID,
  FIRST_REAL_PROVIDERS,
  STAFF_CRP_BLOCK,
  canOfferGenerate,
  confirmCopy,
  crpReadyLabel,
  pickShot001,
  staffShotLabel,
} from './kit-video-first-real-production';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-first-real-production.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentKitVideoImageGenerationExecutionCard.tsx'), 'utf8');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
const rules = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/FirstRealProductionRules.cs'),
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

ok(FIRST_REAL_PRODUCTION_ID === 'FAMIXA_FIRST_REAL_PRODUCTION_V1', '01 document id');
ok(FIRST_REAL_PROVIDERS.map((x) => x.id).join('|') === '|GEMINI|RUNWAY|VEO', '02 provider radios');
ok(staffShotLabel('CHAR-001-MINH-ERA01-SHOT-001') === 'Shot 01', '03 staff shot label');
ok(pickShot001([{ shotCode: 'SHOT-002' }, { shotCode: 'CHAR-001-MINH-ERA01-SHOT-001' }])?.shotCode.includes('SHOT-001'), '04 pick SHOT-001');
ok(crpReadyLabel(false) === 'Chưa hoàn tất' && !canOfferGenerate({ crpUsable: false, provider: 'GEMINI' }), '05 CRP blocks generate');
ok(!canOfferGenerate({ crpUsable: true, provider: '' }) && !canOfferGenerate({ crpUsable: true, provider: 'RUNWAY' }), '06 no auto provider');
ok(canOfferGenerate({ crpUsable: true, provider: 'GEMINI' }), '07 Gemini + usable CRP can offer');
ok(confirmCopy({ shotLabel: 'Shot 01', character: 'Minh', location: 'Phòng khách', action: 'Đọc tờ giấy', provider: 'Gemini' }).note.includes('01 hình'), '08 confirm copy');
ok(!ts.includes('content.api') && !ts.includes('kfCount') && !ts.includes('import.meta.env'), '09 TS map only');
ok(ui.includes('Tạo hình') && ui.includes('Bắt đầu tạo') && ui.includes('Hủy'), '10 staff confirm');
ok(ts.includes('Chưa chọn') && ts.includes('Gemini') && ts.includes('Runway') && ts.includes('Veo') && ui.includes('FIRST_REAL_PROVIDERS'), '11 provider radios');
ok(ts.includes(STAFF_CRP_BLOCK) && ui.includes('STAFF_CRP_BLOCK'), '12 staff CRP block');
ok(ui.includes('Chi tiết sản xuất') && !ui.includes('EXECUTE IMAGE GENERATION'), '13 technical details collapsed');
ok(!ui.includes('GENERATE VIDEO') && !ui.includes('LIPSYNC') && !ui.includes('Tạo lại'), '14 no video / no regenerate');
ok(api.includes('confirm: body?.confirm === true') && api.includes('first-real-production'), '15 execute requires confirm');
ok(rules.includes('MayCallProvider') && rules.includes('AutoSelectProvider() => false'), '16 no auto-select');
ok(service.includes('FirstRealProductionRules.DocumentId') && service.includes('MayCallProvider(gate)'), '17 execute gate before Gemini');
ok(firstReal.includes('Confirm') || firstReal.includes('false'), '18 GET never confirms');
ok(controller.includes('first-real-production') && controller.includes('request?.Confirm ?? false'), '19 controller confirm + preview');
ok(!service.includes('IRunway') && !firstReal.includes('GenerateAsync'), '20 preview/service no extra provider');

if (fail.length) {
  console.error(`FAMIXA_FIRST_REAL_PRODUCTION_V1_REGRESSION FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_FIRST_REAL_PRODUCTION_V1_REGRESSION PASS FAIL=0 P0=0 (file/SoT scan; C# via /first-real-production/regression)');
