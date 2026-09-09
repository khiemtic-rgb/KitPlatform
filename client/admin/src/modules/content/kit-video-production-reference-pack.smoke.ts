import { MASTER_CODE_V1 } from './kit-video-master-lock';
import { buildMinhDnaV1Spec } from './kit-video-character-dna';
import {
  MINH_PRODUCTION_PACK_ID,
  MINH_PRODUCTION_PACK_REVIEW_ID,
  PRODUCTION_PACK_CODE,
  PRODUCTION_PACK_VERSION,
  PRP_PRODUCTION_FORBIDDEN,
  attachMissingProductionRules,
  blockProductionOnShaMismatch,
  buildMinhProductionPackV1Spec,
  canApproveProductionPack,
  canCreateProductionPack,
  canCreateProductionShot,
  directorReviewPass,
  evaluateDirectorReview,
  evaluateReviewChecks,
  identityMutated,
  inheritsIdentity,
  isPackComplete,
  packApproveIdempotent,
  packCreatesPixels,
  packGoldenUntouched,
  packMutatesMasterOrDna,
  productionPackGatePass,
  prpReviewReport,
  rejectPackMutation,
  evaluateProductionPackGate,
} from './kit-video-production-reference-pack';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const dna = buildMinhDnaV1Spec('abc123sha256masterxxxxxxxxxxxxxxxxxxxx');
const spec = buildMinhProductionPackV1Spec({
  masterSha256: 'abc123sha256masterxxxxxxxxxxxxxxxxxxxx',
  dnaSha256: 'dna123sha256lockedxxxxxxxxxxxxxxxxxxxxx',
  dna,
  identityPass: true,
  stressPass: true,
  p0: 0,
});
const locked = {
  masterLocked: true,
  dnaLocked: true,
  masterShaValid: true,
  dnaShaValid: true,
  identityPass: true,
  stressPass: true,
  p0: 0,
  directorApproval: true,
  spec,
  dna,
  status: 'DRAFT',
  actor: 'director',
};

ok(MINH_PRODUCTION_PACK_ID === 'CHAR-001_MINH_PRODUCTION_REFERENCE_PACK_V1', '01 document id');
ok(PRODUCTION_PACK_CODE === 'CHAR-001-MINH-ERA01-PROD-REF-V1', '01b pack code');
ok(canCreateProductionPack(locked).ok && canCreateProductionPack(locked).status === 'DRAFT', '1 create DRAFT when gates pass');
ok(!canCreateProductionPack({ ...locked, dnaLocked: false }).ok, '2 DNA not LOCKED → no create');
ok(!canCreateProductionPack({ ...locked, masterLocked: false }).ok, '3 Master not LOCKED → no create');
ok(!canCreateProductionPack({ ...locked, masterShaValid: false }).ok, '4 Master SHA mismatch');
ok(!canCreateProductionPack({ ...locked, dnaShaValid: false }).ok, '5 DNA SHA mismatch');
ok(inheritsIdentity(spec, dna) && isPackComplete(spec), '6 inheritance + complete');
ok(!inheritsIdentity({ ...spec, faceReference: { mutated: true } }, dna), '7 forbidden identity mutation');
ok(identityMutated(spec, { ...spec, faceReference: { mutated: true } }), '7b edit identity detected');
ok(!packMutatesMasterOrDna('CREATE') && packMutatesMasterOrDna('CHANGE_MASTER') && packMutatesMasterOrDna('CHANGE_DNA'), '8 immutable Master/DNA');
ok(packApproveIdempotent('V1').ok && packApproveIdempotent('V1').createdV2 === false, '9 versioning stays V1');
ok(canApproveProductionPack(locked).ok && canApproveProductionPack(locked).next === 'LOCKED', '10 Director approve → LOCKED');
ok(!canApproveProductionPack({ ...locked, actor: '' }).ok, '11 API authorization / director actor');
ok(!canApproveProductionPack({ ...locked, identityPass: false }).ok, '12 Director gate Identity FAIL');
ok(!rejectPackMutation('LOCKED', 'UPDATE').ok && !rejectPackMutation('LOCKED', 'CHANGE_SHA256').ok, '13 LOCKED immutable');
ok(!packCreatesPixels('CREATE') && packCreatesPixels('GENERATE'), '14 no generate');
ok(packGoldenUntouched('kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-004-D.jpg'), '15 Golden untouched');
ok(spec.derived_from_master === MASTER_CODE_V1 && spec.derived_from_dna === 'CHAR-001-MINH-ERA01-DNA-V1', '16 provenance');
ok(productionPackGatePass(evaluateProductionPackGate(locked)), '17 create gate PASS');
ok(PRODUCTION_PACK_VERSION === 'V1' && !canCreateProductionPack({ ...locked, lockedPack: true }).ok, '18 no V2 from V1 button');
ok(canCreateProductionPack(locked).approved === false && canCreateProductionPack(locked).locked === false, '19 no auto APPROVE/LOCK');
ok(MINH_PRODUCTION_PACK_REVIEW_ID === 'CHAR-001_MINH_PRODUCTION_REFERENCE_PACK_REVIEW_SPEC_V1', '20 review spec id');
ok(directorReviewPass(evaluateDirectorReview(locked)) && evaluateReviewChecks(spec, dna).every((x) => x.pass), '21 director review PASS');
ok(!directorReviewPass(evaluateDirectorReview({ ...locked, spec: { ...spec, faceReference: { mutated: true } } })), '22 inheritance conflict blocks review');
ok(!canApproveProductionPack({ ...locked, spec: { ...spec, productionRules: undefined } }).ok, '23 missing production rules');
ok(PRP_PRODUCTION_FORBIDDEN.includes('identity drift') && PRP_PRODUCTION_FORBIDDEN.includes('generic AI boy'), '24 forbidden production');
ok(!canCreateProductionShot(locked).ok && !canCreateProductionShot({ ...locked, status: 'LOCKED' }).createShot, '25 production shot blocked in review');
ok(!blockProductionOnShaMismatch('aaa', 'bbb').ok && blockProductionOnShaMismatch('aaa', 'aaa').ok, '26 SHA mismatch blocks production');
ok(!rejectPackMutation('LOCKED', 'EDIT').ok && !rejectPackMutation('LOCKED', 'DELETE').ok, '27 lock immutability');
const incomplete = { ...spec };
delete incomplete.shotRules;
ok(attachMissingProductionRules(incomplete, spec.masterSha256 || '', spec.dnaSha256 || '').shotRules, '28 analyze attaches missing production rules');
ok(spec.identityConstraints && spec.shotRules && spec.forbiddenProduction, '29 A–L production sections present');
const report = prpReviewReport({
  regressionPass: true,
  directorGatePass: directorReviewPass(evaluateDirectorReview(locked)),
  status: 'DRAFT',
  masterLocked: true,
  dnaLocked: true,
  masterShaMatch: true,
  dnaShaMatch: true,
});
ok(report.REGRESSION === 'PASS' && report['DIRECTOR GATE'] === 'PASS' && report['PRP STATUS'] === 'DRAFT', '30 review report');

if (fail.length) {
  console.error('KIT VIDEO PRODUCTION REFERENCE PACK FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO PRODUCTION REFERENCE PACK PASS · 32 tests · review A–L · inherit DNA · no shot · no generate · no auto-lock · no Golden');
