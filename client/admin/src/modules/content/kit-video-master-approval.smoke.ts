import { MASTER_REF_V1, REVIEW_ALLOWED_CANDIDATE, canDirectorPassReview, evaluateMasterReviewGate, reviewRegressionStatus } from './kit-video-master-review';
import { evaluateMasterLock, lockIdempotent, rejectMasterMutation } from './kit-video-master-lock';

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
  reviewStatus: 'PENDING',
  directorPass: true,
  actor: 'director',
};

ok(evaluateMasterReviewGate(gate).ok && canDirectorPassReview('PENDING', gate).locked === true, '1 004-D Director PASS → lock V1');
ok(MASTER_REF_V1 === 'CHAR-001_MASTER_REFERENCE_V1', '1b master code');
ok(!evaluateMasterReviewGate({ ...gate, stressHave: 9, stressPass: false }).ok, '2 Stress 9/10 reject');
ok(!evaluateMasterReviewGate({ ...gate, candidateP0: 1 }).ok, '3 P0 > 0 reject');
ok(!evaluateMasterReviewGate({ ...gate, dnaApproved: false }).ok, '4 DNA not APPROVED reject');
ok(lockIdempotent(REVIEW_ALLOWED_CANDIDATE, REVIEW_ALLOWED_CANDIDATE).ok, '5 double click same Master');
ok(!rejectMasterMutation('UPDATE').ok && !rejectMasterMutation('CHANGE_SHA256').ok, '6 mutate locked reject');
ok(!evaluateMasterLock({ ...gate, reviewStatus: 'PENDING', directorPass: false, actor: '' }).ok, '7 unauthorized');
ok(canDirectorPassReview('PENDING', gate).autoSelected === false, '8 no auto-select');
ok(reviewRegressionStatus(gate).ok && reviewRegressionStatus(gate).label === 'PASS', '9 REGRESSION line PASS');

if (fail.length) {
  console.error('KIT VIDEO MASTER APPROVAL FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO MASTER APPROVAL PASS · 9 tests · Director PASS = LOCK V1 · REGRESSION line · no generate');
