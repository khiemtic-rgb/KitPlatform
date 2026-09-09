import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import {
  canOpenIdentityTest,
  compileIdentityTestPrompt,
  directorIdentityDecision,
  dnaAllowsIdentityTest,
  evaluateIdentityArtifact,
  fingerprintIdentityTest,
  identityTestCompleteness,
  identityTestCreatesCanon,
  identityTestKindGuard,
  MINH_IDENTITY_TEST_ID,
  noRunwayInIdentityTest,
  PRIORITY_CANDIDATES,
  REQUIRED_VARIANTS,
  reuseOrRetry,
  rollupIdentityTest,
} from './kit-video-identity-test';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(MINH_IDENTITY_TEST_ID === 'CHAR-001_MINH_IDENTITY_TEST_SPEC_V1', 'spec id');
ok(REQUIRED_VARIANTS.length === 7, '7 required variants');
ok(PRIORITY_CANDIDATES.includes('MINH-E01-CANDIDATE-004'), '01 priority 004');

ok(
  canOpenIdentityTest({ dnaStatus: 'APPROVED', candidateCode: 'MINH-E01-CANDIDATE-004', frontRunner: true }).ok,
  '01 Candidate 004 eligible → test created',
);
ok(!canOpenIdentityTest({ dnaStatus: 'REVIEW', candidateCode: 'MINH-E01-CANDIDATE-004', frontRunner: true }).ok, '02 DNA not APPROVED → BLOCKED');
ok(!dnaAllowsIdentityTest('DRAFT') && !dnaAllowsIdentityTest('INCOMPLETE'), '02 DNA gate');
ok(!canOpenIdentityTest({ dnaStatus: 'APPROVED', candidateCode: 'MINH-E01-CANDIDATE-001', frontRunner: true }).ok, '001 not in this round');

const base = {
  artifactExists: true,
  readable: true,
  sha256: 'aaa111',
  liveSha256: 'aaa111',
  qaSha256: 'aaa111',
  imageType: 'IDENTITY_TEST',
  characterCount: 1,
  minhPresent: true,
  faceVisible: true,
  identitySame: true,
  ageLook: '11' as const,
  hairSame: true,
  requestedVariant: 'FRONT',
  viewpoint: 'FRONT',
  emotionOk: true,
  score: 88,
};

for (const variant of REQUIRED_VARIANTS) {
  const compiled = compileIdentityTestPrompt({ variant, candidateCode: 'MINH-E01-CANDIDATE-004', sourceSha256: 'aaa111' });
  ok(compiled.ok && compiled.prompt.includes('[IMAGE CONTRACT]') && !compiled.prompt.includes('{'), `${variant} prompt`);
  const art = evaluateIdentityArtifact({ ...base, requestedVariant: variant, viewpoint: variant === 'SAD' || variant === 'LIGHT_SMILE' || variant === 'NEUTRAL' ? 'FRONT' : variant });
  ok(art.ok && art.kind === 'IDENTITY_TEST', `0${3 + REQUIRED_VARIANTS.indexOf(variant)} ${variant} generated → artifact valid`);
}

ok(identityTestCompleteness([]).status === 'INCOMPLETE', '10 missing artifact → INCOMPLETE');
ok(!evaluateIdentityArtifact({ ...base, sha256: 'aaa', liveSha256: 'bbb' }).ok, '11 hash mismatch → BLOCKED');
ok(!evaluateIdentityArtifact({ ...base, minhPresent: false }).ok, '12 Vision FAIL → not PASS');
ok(evaluateIdentityArtifact({ ...base, characterCount: 2 }).p0.includes('P0-03_CHARACTER_COUNT'), '13 extra character → P0');
ok(evaluateIdentityArtifact({ ...base, croppedFace: true }).diagnosis.includes('FACE_CROP'), '14 character crop → P0');
ok(evaluateIdentityArtifact({ ...base, sheet: true }).p0.includes('IMAGE_TYPE_FAIL'), '15 character sheet → P0');
ok(evaluateIdentityArtifact({ ...base, requestedVariant: 'PROFILE', viewpoint: 'FRONT' }).diagnosis.includes('VIEWPOINT_FAILURE'), '16 wrong viewpoint');
ok(evaluateIdentityArtifact({ ...base, identitySame: false }).diagnosis.includes('IDENTITY_DRIFT'), '17 identity drift');
ok(evaluateIdentityArtifact({ ...base, ageLook: 'adult' }).diagnosis.includes('AGE_DRIFT'), '18 age drift');
ok(evaluateIdentityArtifact({ ...base, requestedVariant: 'SAD', emotionOk: true, identitySame: true }).ok, '19 emotion change identity kept → PASS');
ok(!evaluateIdentityArtifact({ ...base, minhPresent: false, score: 99 }).ok && evaluateIdentityArtifact({ ...base, minhPresent: false, score: 99 }).usedScoreAsOverride === false, '20 high score + P0 → FAIL');

const fp = fingerprintIdentityTest({
  candidateId: 'c4',
  testType: 'VIEW',
  testVariant: 'FRONT',
  dnaVersion: 'V1',
  sourceSha256: 'aaa111',
});
ok(reuseOrRetry({ fingerprint: fp, existingFingerprint: fp, existingSha: 'aaa111', existingValid: true }).action === 'REUSE_EXISTING_ARTIFACT', '21 same fingerprint reuse');
ok(reuseOrRetry({ fingerprint: fp, existingFingerprint: fp, existingValid: false }).blindRetry, '21 DO_NOT_BLIND_RETRY when invalid');
ok(reuseOrRetry({ fingerprint: fp, regenerate: true }).action === 'NEW_ATTEMPT', '22 regenerate → new attempt');

const pass = directorIdentityDecision({ decision: 'PASS' });
const failDec = directorIdentityDecision({ decision: 'FAIL' });
const promote = directorIdentityDecision({ decision: 'PROMOTE_TO_MASTER_REVIEW' });
ok(pass.ok && !pass.masterCreated && !pass.locked, '23 Director PASS → audit, no Master');
ok(failDec.ok && !failDec.canon, '24 Director FAIL → audit');
ok(promote.promoteToMasterReview && !promote.selectMaster && !promote.masterCreated, '25 PROMOTE ≠ Master');
ok(!identityTestCreatesCanon([{ type: 'IDENTITY_TEST_CREATED' }, { type: 'DIRECTOR_PASS' }]), '26 Identity Test does not create Canon');
ok(!identityTestKindGuard('MASTER_REFERENCE').ok && identityTestKindGuard('IDENTITY_TEST').ok, '26 kind guard');
ok(!canOpenIdentityTest({ dnaStatus: 'APPROVED', candidateCode: 'MINH-E01-CANDIDATE-004', frontRunner: true }).locked, '27 no LOCK');
ok(isLegacyOrGoldenPath('take-01.mp4') && evaluateIdentityArtifact({ ...base, goldenPath: 'SH01-01' }).p0.includes('P0_GOLDEN_NOT_MASTER'), '28 Golden unchanged / not Master');
ok(noRunwayInIdentityTest(MINH_IDENTITY_TEST_ID + ' generate analyze compare director') && KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '29 Runway called = FALSE');

const complete = identityTestCompleteness(REQUIRED_VARIANTS.map((v) => ({ variant: v, sha256: 'x', fingerprint: 'f', vision: 'PASS' })));
ok(complete.complete && complete.status === 'IDENTITY_TEST_COMPLETE', '19 completeness 7/7');
ok(rollupIdentityTest({ artifacts: REQUIRED_VARIANTS.map((v) => ({ variant: v, sha256: 'x', fingerprint: 'f', vision: 'FAIL', p0: ['IDENTITY_DRIFT'] })), dnaApproved: true }).status === 'FAIL', '12 rollup FAIL');

if (fail.length) {
  console.error('KIT VIDEO IDENTITY TEST FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO IDENTITY TEST PASS · tests 01–29 · no Canon · no LOCK · no Runway');
