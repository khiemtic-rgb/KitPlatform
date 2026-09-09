import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import {
  diagnoseCollapsedStressQa,
  canOpenStressTest,
  canPromoteAfterStress,
  compileStressPrompt,
  directorCanApprove,
  directorStressDecision,
  evaluateStressCase,
  fingerprintStress,
  MINH_IDENTITY_STRESS_ID,
  noRunwayInStress,
  reuseOrRepair,
  rollupStress,
  sourceStillValid,
  STRESS_ALLOWED_CANDIDATE,
  STRESS_CASES,
  STRESS_KIND,
  stressArtifactKindGuard,
} from './kit-video-identity-stress';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(MINH_IDENTITY_STRESS_ID === 'CHAR-001_MINH_IDENTITY_STRESS_TEST_IMPLEMENTATION_V1', 'spec id');
ok(STRESS_CASES.length === 10 && STRESS_CASES[9].key === 'MULTI_PERSON', '10 cases');

const gate = {
  characterId: 'CHAR-001',
  eraId: 'ERA-01',
  candidateCode: STRESS_ALLOWED_CANDIDATE,
  dnaStatus: 'APPROVED',
  identityEligible: true,
  identityTestComplete: true,
  artifactOk: true,
  artifactReadable: true,
  sha256: 'aaa111',
  liveSha256: 'aaa111',
  visionPass: true,
};

ok(!canOpenStressTest({ ...gate, candidateCode: 'MINH-E01-CANDIDATE-004' }).ok, '01 source candidate invalid');
ok(!canOpenStressTest({ ...gate, liveSha256: 'bbb222' }).ok, '02 source SHA mismatch');
ok(!canOpenStressTest({ ...gate, artifactOk: false }).ok, '03 artifact missing');
ok(!canOpenStressTest({ ...gate, artifactReadable: false }).ok, '04 artifact corrupted');
ok(!sourceStillValid({ storedSha: 'aaa', liveSha: 'bbb' }).ok, '02b invalidated on source change');

ok(evaluateStressCase({ sheet: true, scores: { identityScore: 99 } }).p0.includes('IMAGE_TYPE_FAIL'), '05 character sheet');
ok(evaluateStressCase({ collage: true, scores: { identityScore: 99 } }).p0.includes('IMAGE_TYPE_FAIL'), '06 collage');
ok(evaluateStressCase({ missingSubject: true, scores: { identityScore: 99 } }).p0.includes('MISSING_SUBJECT'), '07 missing Minh');
ok(evaluateStressCase({ differentPerson: true, scores: { identityScore: 99 } }).p0.includes('IDENTITY_MISMATCH'), '08 wrong identity');
ok(evaluateStressCase({ duplicateMinh: true, scores: { identityScore: 99 } }).p0.includes('DUPLICATE_MINH'), '09 duplicate Minh');
ok(evaluateStressCase({ faceDeformed: true, scores: { identityScore: 99 } }).p0.includes('FACE_DEFORMATION'), '10 face deformation');
ok(!evaluateStressCase({ conditionSatisfied: false, scores: { identityScore: 94 } }).ok, '11 wrong test condition');
ok(!evaluateStressCase({ visionFail: true, scores: { identityScore: 94 } }).ok, '12 Vision FAIL');
ok(!evaluateStressCase({ faceSame: false, scores: { identityScore: 99 } }).ok && !evaluateStressCase({ faceSame: false, scores: { identityScore: 99 } }).usedScoreAsOverride, '13 P0 FAIL beats score');
ok(!evaluateStressCase({ scores: { identityScore: 70 } }).ok, '14 score below threshold');

const fp = fingerprintStress({ candidateId: '004-D', caseCode: 'ST-01', sourceFingerprint: 'src1' });
ok(fp.includes('ST-01') && fp.includes('src1'), '15 fingerprint key');
ok(reuseOrRepair({ fingerprint: fp, existingFingerprint: fp, existingValid: true }).blindRetry, '15 duplicate fingerprint blocked');
ok(reuseOrRepair({ fingerprint: fp, existingFingerprint: fp, existingValid: true }).action === 'DO_NOT_BLIND_RETRY', '16 blind retry blocked');
ok(!reuseOrRepair({ fingerprint: fp, existingFingerprint: fp, existingValid: false, regenerate: true }).blindRetry === false, '16b regen without diagnosis blocked');
ok(reuseOrRepair({ fingerprint: 'new', existingFingerprint: fp, existingValid: true, regenerate: true, diagnosis: 'keep face' }).action === 'NEW_ATTEMPT', '16c failed case regen with diagnosis');

ok(!directorCanApprove('FAIL').ok, '17 Director cannot approve failed test');
ok(directorCanApprove('PASS').readyForMasterReview && !directorCanApprove('PASS').locked, '17b Director PASS ≠ lock');

const ten = STRESS_CASES.map((c) => ({ code: c.code, sha256: 'x', status: 'PASS', p0: [], scores: { identityScore: 94 } }));
const all = rollupStress(ten);
ok(all.status === 'PASS' && all.allowMasterReview && !all.locked && !all.productionStill, '18 Stress PASS cannot auto-lock Master');
ok(!stressArtifactKindGuard('PRODUCTION_STILL').ok, '19 Stress artifact cannot become Production Still');
ok(stressArtifactKindGuard(STRESS_KIND).ok, '19b kind IDENTITY_STRESS');

ok(isLegacyOrGoldenPath('SH01-01') && noRunwayInStress(MINH_IDENTITY_STRESS_ID + ' analyze director'), '20 Golden SH01-01 unchanged');
ok(noRunwayInStress('identity stress run analyze director') && KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '21 Runway called = FALSE');

ok(!canPromoteAfterStress({ identityComplete: true, stressStatus: 'PASS', directorPass: false, artifactOk: true, dnaApproved: true }).ok, 'director required');
ok(canPromoteAfterStress({ identityComplete: true, stressStatus: 'PASS', directorPass: true, artifactOk: true, dnaApproved: true }).readyForMasterReview, 'READY_FOR_MASTER_REVIEW');
ok(!directorStressDecision('PASS', 'FAIL').ok, 'approve fail blocked');
ok(compileStressPrompt({ caseCode: 'ST-09', candidateCode: STRESS_ALLOWED_CANDIDATE, sourceSha256: 'aaa111' }).prompt.includes('study desk'), 'ST-09 scene');
ok(compileStressPrompt({ caseCode: 'ST-10', candidateCode: STRESS_ALLOWED_CANDIDATE, sourceSha256: 'aaa111' }).prompt.includes('two_people'), 'ST-10 multi');
ok(canOpenStressTest(gate).ok, '004-D eligible opens');
ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '22 Phase 01–06 engine id unchanged');

const collapsed = diagnoseCollapsedStressQa({
  p0: ['IDENTITY_MISMATCH', 'FACE_DEFORMATION', 'AGE_DRIFT', 'HAIR_IDENTITY_BREAK', 'FACIAL_PROPORTION_BREAK', 'DISTINCTIVE_FEATURE_LOSS'],
  identityScore: 40,
  vision: 'FAIL',
});
ok(!!collapsed && collapsed.collapsedCascade === true && collapsed.scoreMode === 'HARDCODED_40', '23 ST-10 cascade diagnostic');
ok(diagnoseCollapsedStressQa({ p0: ['IDENTITY_MISMATCH'], identityScore: 55, vision: 'FAIL' }) == null, '24 real single P0 is not cascade');
ok(!evaluateStressCase({ differentPerson: true, scores: { identityScore: 40 } }).p0.includes('AGE_DRIFT'), '25 evaluator must not invent AGE_DRIFT from identity fail');

if (fail.length) {
  console.error('KIT VIDEO IDENTITY STRESS FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO IDENTITY STRESS PASS · 25 tests · 10 cases · 004-D · no Canon · no LOCK · no Runway');
