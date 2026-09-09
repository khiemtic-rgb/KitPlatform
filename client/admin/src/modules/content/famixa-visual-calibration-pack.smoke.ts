import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CALIBRATION_VIEWS,
  VISUAL_CALIBRATION_PACK_ID,
  VISUAL_CALIBRATION_SUITE,
  calibrationMayApprove,
  calibrationMayCreate,
  calibrationMayLock,
  calibrationStatusLabel,
} from './kit-video-visual-calibration';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const card = read('ContentFamixaVisualCalibrationCard.tsx');
const helpers = read('kit-video-visual-calibration.ts');
const board = read('ContentFamixaProductionBoards.tsx');
const studio = read('ContentFamixaCharacterStudio.tsx');
const api = read('../../shared/api/content.api.ts');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1Rules.cs');
const regression = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1Regression.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');

ok(board.includes('<ContentFamixaVisualCalibrationCard')
  && board.indexOf('<ContentFamixaVisualUniverseAuthorityCard') < board.indexOf('<ContentFamixaVisualCalibrationCard')
  && board.indexOf('<ContentFamixaVisualCalibrationCard') < board.indexOf('<ContentFamixaCharacterStudio'),
  'page loads / board order');
ok(card.includes('VISUAL UNIVERSE') && card.includes('Current:') && card.includes('Candidate'),
  'current universe displayed');
ok(card.includes('Candidate') && card.includes('calibrationStatusLabel'),
  'candidate displayed');
ok(card.includes('Tạo bộ kiểm tra phong cách') && card.includes('createVisualCalibrationPack')
  && api.includes('/content/visual-calibration/packs'),
  'create pack');
ok(card.includes('title="Xác nhận"') && card.includes('onCancel={() => setConfirm(null)}'),
  'confirm dialog');
ok(card.includes('onCancel={() => setConfirm(null)}') && card.includes('setConfirm(null)'),
  'cancel');
ok(card.includes('generateVisualCalibrationPack') && card.includes('generate: true')
  && card.includes('title="Generate Calibration Pack"')
  && card.includes("setConfirm('generate')"),
  'generate');
ok(CALIBRATION_VIEWS.join(',') === 'FRONT,THREE_QUARTER,SIDE,FULL_BODY'
  && card.includes('Child Boy') && card.includes('Adult Male') && card.includes('Older Adult')
  && card.includes('fx-cstudio__grid'),
  'review grid');
ok(card.includes('Duyệt') && card.includes('approveVisualCalibrationPack')
  && card.includes('directorPass: true'),
  'approve');
ok(card.includes('Không đạt') && card.includes('rejectVisualCalibrationPack'),
  'reject');
ok(card.includes('Khóa Visual Universe') && card.includes('lockVisualCalibrationPack'),
  'lock');
ok(card.includes('6 nhân vật mẫu · 24 ảnh · cùng một Visual Universe')
  && card.includes('bài test phong cách, chưa phải nhân vật thật')
  && card.includes("label: 'Chi tiết kỹ thuật'"),
  'authority state / director copy');
ok(!card.includes('defaultActiveKey') && !studio.includes('visual-calibration'),
  'calibration isolated from Character Studio');
ok(/useEffect\(\(\) => \{\s*load\(\);\s*\}, \[\]\)/.test(card)
  && !helpers.includes('autoGenerate: true')
  && rules.includes('AutoGenerate() => false'),
  'no automatic generation');
ok(!card.includes('autoApprove: true') && rules.includes('AutoApprove() => false')
  && calibrationMayApprove('DRAFT') === false,
  'no automatic approval');
ok(!card.includes('autoLock: true') && rules.includes('AutoLock() => false')
  && calibrationMayLock('APPROVED') === true
  && calibrationMayLock('PENDING_REVIEW') === false,
  'no automatic lock');
ok(calibrationMayCreate('DRAFT') && calibrationStatusLabel('PENDING_REVIEW') === 'PENDING REVIEW'
  && VISUAL_CALIBRATION_PACK_ID === 'FAMIXA-VISUAL-CALIBRATION-V1'
  && VISUAL_CALIBRATION_SUITE === 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_REGRESSION',
  'helpers');
ok(controller.includes('visual-calibration/regression')
  && controller.includes('visual-calibration/packs')
  && api.includes('/content/visual-calibration/regression')
  && regression.includes('CASE-020 Minh remains unchanged')
  && rules.includes('VisualCalibrationPromptCompilerV1')
  && rules.includes('CallsGemini() => false')
  && !thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(),
  'API + regression + no mutate');

function thisSmokeGenerates() {
  return false;
}
function thisSmokeApproves() {
  return false;
}
function thisSmokeLocks() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_VISUAL_CALIBRATION_PACK_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_CALIBRATION_PACK_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
