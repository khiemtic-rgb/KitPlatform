import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import { REVIEW_ALLOWED_CANDIDATE } from './kit-video-master-review';
import {
  autoPromoteToMaster,
  canLockAsMaster,
  evaluateMasterLock,
  lockCreatesPixels,
  lockIdempotent,
  MASTER_CODE_V1,
  MINH_MASTER_LOCK_ID,
  rejectMasterMutation,
  resolveCanonPointer,
} from './kit-video-master-lock';

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
  reviewStatus: 'PASS',
  directorPass: true,
  actor: 'director',
};

ok(MINH_MASTER_LOCK_ID === 'CHAR-001_MINH_MASTER_REFERENCE_LOCK_IMPLEMENTATION_V1', '01 spec id');
ok(MASTER_CODE_V1 === 'CHAR-001-MINH-ERA01-MASTER-V1', '01b master code independent of candidate');
ok(evaluateMasterLock(gate).ok && evaluateMasterLock(gate).autoPromoted === false, '1 Director PASS + gates → lock');
ok(canLockAsMaster('PASS', gate).ok, '1b lock after Director PASS');
ok(lockIdempotent(REVIEW_ALLOWED_CANDIDATE, REVIEW_ALLOWED_CANDIDATE).ok && !lockIdempotent(REVIEW_ALLOWED_CANDIDATE, REVIEW_ALLOWED_CANDIDATE).duplicate, '2 lock twice same source no duplicate');
ok(!evaluateMasterLock({ ...gate, visionPass: false }).ok, '3 Vision FAIL reject');
ok(!evaluateMasterLock({ ...gate, candidateP0: 1 }).ok, '4 P0 > 0 reject');
ok(!evaluateMasterLock({ ...gate, dnaApproved: false }).ok, '5 DNA not APPROVED reject');
ok(!evaluateMasterLock({ ...gate, identityHave: 6, identityPass: false }).ok, '6 Identity < 7/7 reject');
ok(!evaluateMasterLock({ ...gate, stressHave: 9, stressPass: false }).ok, '7 Stress < 10/10 reject');
ok(!evaluateMasterLock({ ...gate, reviewStatus: 'PENDING', directorPass: false }).ok, '8 Director chưa PASS reject');
ok(!evaluateMasterLock({ ...gate, rejected: true }).ok, '9 REJECT reject');
ok(!evaluateMasterLock({ ...gate, eligible: false }).ok, '10 NOT_ELIGIBLE reject');
ok(!evaluateMasterLock({ ...gate, reviewStatus: 'PENDING', directorPass: false, draftBlocked: true }).ok, '11 DRAFT reject');
ok(!evaluateMasterLock({ ...gate, reviewStatus: 'PENDING', directorPass: false, eligibleBlocked: true }).ok, '12 not FRONT_RUNNER/ELIGIBLE reject');
ok(!evaluateMasterLock({ ...gate, canonActive: true, existingSource: 'OTHER', requestedSource: REVIEW_ALLOWED_CANDIDATE }).ok, '13 Canon active conflict');
ok(!evaluateMasterLock({ ...gate, actor: '' }).ok && !evaluateMasterLock({ ...gate, actor: 'anonymous' }).ok, '14 unauthorized');
ok(!rejectMasterMutation('UPDATE').ok, '15 update after lock reject');
ok(!rejectMasterMutation('DELETE').ok, '16 delete after lock reject');
ok(!rejectMasterMutation('CHANGE_ARTIFACT').ok && !rejectMasterMutation('CHANGE_SHA256').ok, '17 change artifact/SHA reject');
ok(lockIdempotent(REVIEW_ALLOWED_CANDIDATE, REVIEW_ALLOWED_CANDIDATE).ok, '18 duplicate V1 idempotent');
ok(!autoPromoteToMaster(true, true, true) && !lockCreatesPixels('LOCK') && lockCreatesPixels('GENERATE'), '19 no auto-promote / no generate');
ok(
  resolveCanonPointer('CHAR-001', 'ERA-01', { masterReferenceId: 'm1', masterCode: MASTER_CODE_V1 })?.masterReferenceId === 'm1'
    && !/004-D/.test(JSON.stringify(resolveCanonPointer('CHAR-001', 'ERA-01', { masterReferenceId: 'm1' }))),
  '20 resolve via Canon Pointer not 004-D',
);
ok(!isLegacyOrGoldenPath('kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-004-D.jpg'), '21 Golden SH01-01 untouched');
ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '22 engine id unchanged');

if (fail.length) {
  console.error('KIT VIDEO MASTER LOCK FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO MASTER LOCK PASS · 22 tests · 004-D → V1 · no generate · no auto-promote · no Golden');
