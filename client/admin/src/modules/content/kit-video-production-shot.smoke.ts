import { MASTER_CODE_V1 } from './kit-video-master-lock';
import { buildMinhDnaV1Spec } from './kit-video-character-dna';
import {
  MINH_PRODUCTION_SHOT_ID,
  PRODUCTION_SHOT_PREFIX,
  PRODUCTION_SHOT_VERSION,
  SHOT_FORBIDDEN,
  assessShotRisk,
  buildMinhProductionShotV1Spec,
  canApproveProductionShot,
  canCreateProductionShot,
  evaluateDirectorShotReview,
  evaluateShotSourceGate,
  identityMutated,
  inheritsShotIdentity,
  isShotComplete,
  markShotIdentityCheck,
  rejectShotMutation,
  shotCode,
  shotCreatesPixels,
  shotGatePass,
  shotGoldenUntouched,
  shotMutatesSources,
  shotReviewReport,
} from './kit-video-production-shot';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const dna = buildMinhDnaV1Spec('abc123sha256masterxxxxxxxxxxxxxxxxxxxx');
const spec = buildMinhProductionShotV1Spec({
  seq: 1,
  masterSha256: 'abc123sha256masterxxxxxxxxxxxxxxxxxxxx',
  dnaSha256: 'dna123sha256lockedxxxxxxxxxxxxxxxxxxxxx',
  prpSha256: 'prp123sha256lockedxxxxxxxxxxxxxxxxxxxxx',
  dna,
});
const locked = {
  masterLocked: true,
  dnaLocked: true,
  prpLocked: true,
  masterShaValid: true,
  dnaShaValid: true,
  prpShaValid: true,
  identityPass: true,
  stressPass: true,
  spec,
  dna,
  status: 'DRAFT',
  actor: 'director',
};
const checked = markShotIdentityCheck(spec, true, 'LOW');

ok(MINH_PRODUCTION_SHOT_ID === 'CHAR-001_MINH_PRODUCTION_SHOT_SPEC_V1', '01 document id');
ok(shotCode(1) === `${PRODUCTION_SHOT_PREFIX}-001`, '01b shot code');
ok(canCreateProductionShot(locked).ok && canCreateProductionShot(locked).status === 'DRAFT', '1 create DRAFT when sources locked');
ok(!canCreateProductionShot({ ...locked, prpLocked: false }).ok, '2 PRP not LOCKED → no create');
ok(!canCreateProductionShot({ ...locked, masterShaValid: false }).ok, '3 Master SHA mismatch');
ok(inheritsShotIdentity(spec, dna) && isShotComplete(spec), '4 inheritance + complete');
ok(!inheritsShotIdentity({ ...spec, identity: { face: { mutated: true } } }, dna), '5 forbidden identity mutation');
ok(identityMutated(spec, { ...spec, identity: { ...spec.identity, face: { mutated: true } } }), '5b edit identity detected');
ok(!shotMutatesSources('CREATE') && shotMutatesSources('CHANGE_PRP'), '6 immutable Master/DNA/PRP');
ok(!canApproveProductionShot(locked).ok, '7 cannot skip Identity Check');
ok(
  canApproveProductionShot({ ...locked, spec: checked, status: 'DIRECTOR_REVIEW' }).ok &&
    canApproveProductionShot({ ...locked, spec: checked, status: 'DIRECTOR_REVIEW' }).next === 'LOCKED',
  '8 Director approve after Identity Check → LOCKED',
);
ok(!canApproveProductionShot({ ...locked, spec: checked, status: 'DIRECTOR_REVIEW', actor: '' }).ok, '9 director actor required');
ok(!rejectShotMutation('LOCKED', 'UPDATE').ok && !rejectShotMutation('LOCKED', 'CHANGE_SHA256').ok, '10 LOCKED immutable');
ok(!shotCreatesPixels('CREATE') && shotCreatesPixels('GEMINI') && shotCreatesPixels('RUNWAY'), '11 no generate');
ok(shotGoldenUntouched('kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-004-D.jpg'), '12 Golden untouched');
ok(spec.source && (spec.source as { master?: string }).master === MASTER_CODE_V1, '13 provenance');
ok(shotGatePass(evaluateShotSourceGate(locked)), '14 source gate PASS');
ok(PRODUCTION_SHOT_VERSION === 'V1' && canCreateProductionShot(locked).approved === false, '15 no auto APPROVE');
ok(assessShotRisk(spec) === 'LOW', '16 default risk LOW');
ok(assessShotRisk({ ...spec, interaction: { ...spec.interaction, peopleCount: 2, minhDistinct: true, noBlend: true, noSwap: true } }) === 'HIGH', '17 multi-person HIGH');
ok(SHOT_FORBIDDEN.includes('identity drift') && SHOT_FORBIDDEN.includes('generic AI child'), '18 forbidden list');
ok(!canApproveProductionShot({ ...locked, spec: markShotIdentityCheck(spec, false, 'HIGH'), status: 'IDENTITY_CHECK' }).ok, '19 HIGH FAIL blocks approve');
ok(evaluateDirectorShotReview({ ...locked, spec: checked }).every((x) => x.pass), '20 director review PASS after check');
const report = shotReviewReport({
  regressionPass: true,
  directorGatePass: true,
  status: 'DRAFT',
  masterLocked: true,
  dnaLocked: true,
  prpLocked: true,
  allShaMatch: true,
});
ok(report.REGRESSION === 'PASS' && report.PRP === 'LOCKED' && report['ALL SHA'] === 'MATCH', '21 review report');

if (fail.length) {
  console.error('KIT VIDEO PRODUCTION SHOT FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO PRODUCTION SHOT PASS · 23 tests · inherit DNA · no generate · no auto-lock · no Golden');
