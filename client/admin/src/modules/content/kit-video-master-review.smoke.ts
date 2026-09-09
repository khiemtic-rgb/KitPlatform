import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import { REQUIRED_VARIANTS } from './kit-video-identity-test';
import { STRESS_CASES } from './kit-video-identity-stress';
import {
  canApproveAfterSelect,
  canDirectorPassReview,
  canLockAfterApprove,
  canSelectAfterReview,
  evaluateMasterReviewGate,
  goldenUntouched,
  MASTER_REF_V1,
  MINH_MASTER_REVIEW_ID,
  noRunwayInMasterReview,
  REVIEW_ALLOWED_CANDIDATE,
  REVIEW_PARENT,
  REVIEW_VARIATION,
  reviewCreatesPixels,
  reviewRegressionStatus,
} from './kit-video-master-review';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const gate = {
  candidateCode: REVIEW_ALLOWED_CANDIDATE,
  readable: true,
  hashValid: true,
  visionPass: true,
  productionStill: true,
  candidateP0: 0,
  eligible: true,
  dnaApproved: true,
  identityPass: true,
  identityHave: 7,
  identityP0: 0,
  stressPass: true,
  stressHave: 10,
  stressP0: 0,
  st10Pass: true,
};

ok(MINH_MASTER_REVIEW_ID === 'CHAR-001_MINH_MASTER_REVIEW_SPEC_V1', '01 spec id');
ok(MASTER_REF_V1 === 'CHAR-001_MASTER_REFERENCE_V1', '01b master ref code');
ok(REVIEW_PARENT === 'MINH-E01-CANDIDATE-004' && REVIEW_VARIATION === '004-D', '01c parent / variation');
ok(REQUIRED_VARIANTS.length === 7 && STRESS_CASES.length === 10, '01d 7 + 10');

ok(!evaluateMasterReviewGate({ ...gate, candidateCode: 'MINH-E01-CANDIDATE-004' }).ok, '02 only 004-D');
ok(!evaluateMasterReviewGate({ ...gate, identityHave: 6, identityPass: false }).ok, '03 identity 7/7 required');
ok(!evaluateMasterReviewGate({ ...gate, stressHave: 9, stressPass: false }).ok, '04 stress 10/10 required');
ok(!evaluateMasterReviewGate({ ...gate, st10Pass: false }).ok, '05 ST-10 must PASS');
ok(!evaluateMasterReviewGate({ ...gate, dnaApproved: false }).ok, '06 DNA APPROVED');
ok(!evaluateMasterReviewGate({ ...gate, visionPass: false }).ok, '07 Vision PASS');
ok(!evaluateMasterReviewGate({ ...gate, productionStill: false }).ok, '08 PRODUCTION_STILL');
ok(!evaluateMasterReviewGate({ ...gate, hashValid: false }).ok, '09 HASH_VALID');
ok(evaluateMasterReviewGate(gate).ok && evaluateMasterReviewGate(gate).autoSelected === false, '10 004-D opens review');
ok(evaluateMasterReviewGate({ ...gate, eligible: undefined }).ok, '10b no selection.lifecycle still opens');
ok(!evaluateMasterReviewGate({ ...gate, eligible: false }).ok, '10c explicit INELIGIBLE blocks');

ok(!canDirectorPassReview('PASS', gate).ok, '11 no second PASS from PASS');
ok(canDirectorPassReview('PENDING', gate).ok && canDirectorPassReview('PENDING', gate).next === 'MASTER_REFERENCE_LOCKED', '12 Director PASS locks V1');
ok(!canSelectAfterReview('PENDING', gate).ok, '13 select blocked before PASS');
ok(canSelectAfterReview('PASS', gate).ok && canSelectAfterReview('PASS', gate).copiedBytes === false, '14 select after PASS, no copy');
ok(!canApproveAfterSelect('PASS', gate).ok, '15 approve blocked before select');
ok(canApproveAfterSelect('SELECTED', gate).ok, '16 approve after select');
ok(!canLockAfterApprove('PENDING', gate).ok, '17 lock blocked before Director PASS');
ok(canLockAfterApprove('PASS', gate).ok && canLockAfterApprove('APPROVED', gate).ok, '18 lock after Director PASS');

ok(!reviewCreatesPixels('SELECT') && reviewCreatesPixels('GENERATE'), '19 no generate in review');
ok(noRunwayInMasterReview(MINH_MASTER_REVIEW_ID + ' director pass select approve lock') && !isLegacyOrGoldenPath('004-D.jpg'), '20 no Runway');
ok(goldenUntouched('kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-004-D.jpg'), '21 Golden SH01-01 untouched');
ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '22 Phase 01–06 engine id unchanged');
ok(reviewRegressionStatus(gate).ok && reviewRegressionStatus(gate).label === 'PASS' && reviewRegressionStatus(gate).generate === false, '23 REGRESSION PASS on Director Decision');
ok(!reviewRegressionStatus({ ...gate, stressPass: false, stressHave: 9 }).ok, '24 REGRESSION FAIL when Stress incomplete');

if (fail.length) {
  console.error('KIT VIDEO MASTER REVIEW FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO MASTER REVIEW PASS · 26 tests · 004-D · REGRESSION line · no generate · no Canon auto · no Runway');
