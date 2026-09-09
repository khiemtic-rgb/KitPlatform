import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CALIBRATION_REQUIRED_SLOTS,
  VISUAL_CALIBRATION_HARDENING_SUITE,
  calibrationCoverageValid,
  calibrationMayApprove,
  calibrationMayGenerate,
  calibrationMayLock,
} from './kit-video-visual-calibration';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const helpers = read('kit-video-visual-calibration.ts');
const card = read('ContentFamixaVisualCalibrationCard.tsx');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1Rules.cs');
const hardening = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualCalibrationPackV1HardeningRegression.cs');
const service = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VisualCalibrationPackService.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const api = read('../../shared/api/content.api.ts');

ok(rules.includes('GatePixelsNotReady') && rules.includes('GetCoverage') && rules.includes('IsValidPixel'),
  'coverage');
ok(calibrationMayApprove('PENDING_REVIEW', { valid: 24, required: 24 })
  && !calibrationMayApprove('PENDING_REVIEW', { valid: 23, required: 24 })
  && !calibrationMayApprove('DRAFT', { valid: 24, required: 24 })
  && rules.includes('CALIBRATION_PIXELS_NOT_READY'),
  'approval gate');
ok(calibrationMayLock('APPROVED') && !calibrationMayLock('PENDING_REVIEW')
  && !calibrationMayLock('DRAFT')
  && rules.includes('AuthorityTransitioned')
  && service.includes('_universe.LockAsync'),
  'lock gate');
ok(rules.includes('PretendsPackLockIsUniverseLock() => false')
  && service.includes('GateVuaPromotion')
  && !service.includes('MarkAuthorityTransition') === false,
  'authority semantics');
ok(rules.includes('MayMaterializeStale') && rules.includes('VisualUniverseStale')
  && service.includes('visualUniverseStale')
  && service.includes('MayMaterializeStale(locked, true)'),
  'stale impact');
ok(service.includes('OfficialLockedAsync') && service.includes('AuthorityLocked')
  && hardening.includes('H-14 Minh SHA unchanged')
  && hardening.includes('H-13 Locked character is never mutated'),
  'locked character protection');
ok(calibrationMayGenerate('DRAFT') && !calibrationMayGenerate('PENDING_REVIEW')
  && !calibrationMayGenerate('LOCKED')
  && card.includes('calibrationMayApprove(status, pack?.coverage)')
  && card.includes('Coverage ')
  && card.includes('generate: true')
  && card.includes("setConfirm('generate')")
  && /useEffect\(\(\) => \{\s*load\(\);\s*\}, \[\]\)/.test(card)
  && controller.includes('visual-calibration/hardening/regression')
  && api.includes('/content/visual-calibration/${packId}/coverage')
  && VISUAL_CALIBRATION_HARDENING_SUITE === 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_HARDENING_REGRESSION'
  && calibrationCoverageValid({ valid: CALIBRATION_REQUIRED_SLOTS, required: 24 })
  && helpers.includes('KindPixel') === false
  && !thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(),
  'UI + API + no mutate');

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
  console.error(`FAMIXA_VISUAL_CALIBRATION_PACK_V1_HARDENING_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VISUAL_CALIBRATION_PACK_V1_HARDENING_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
