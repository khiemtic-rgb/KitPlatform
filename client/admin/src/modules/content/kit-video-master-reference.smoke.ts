import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import { distinguishFrameKind } from './kit-video-visual-system';
import {
  canLockMaster,
  canPromoteToProductionStill,
  canUseAsMasterSource,
  CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1,
  compileMasterCandidatePrompt,
  diagnoseRepair,
  doNotBlindRetry,
  ensureMasterImmutable,
  eraRemainsChar001,
  evaluateMasterQa,
  isProviderIndependent,
  KIT_VIDEO_MASTER_REFERENCE,
  MINH_MASTER_SPEC_ID,
  nextCandidateCode,
  nextMasterVersion,
  persistenceFailMeansRevalidate,
  productionStillMayReferenceMaster,
  refuseGenerate,
  registerCandidate,
} from './kit-video-master-reference';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const spec = CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1;
ok(spec.documentId === MINH_MASTER_SPEC_ID && spec.status === 'DRAFT', 'spec draft');
ok(spec.assetKind === 'CHARACTER' && spec.assetCode === 'CHAR-001' && spec.era === 'ERA-01', 'generic asset CHAR-001 ERA-01');
ok(KIT_VIDEO_MASTER_REFERENCE === 'KIT-VIDEO-MASTER-REFERENCE-V1', 'generic engine id');
ok(!spec.generateEnabled && spec.notI2vSource && spec.notProductionStill, 'no auto generate / not I2V');

const c1 = registerCandidate([], {
  generationAttempt: 1,
  artifactPath: 'App_Data/kit-video-master/CHAR-001/ERA-01/candidate-001.jpg',
  sha256: 'aaa111',
  visualStyleVersion: 'V1',
  characterId: 'CHAR-001',
  eraId: 'ERA-01',
  role: 'IDENTITY',
});
ok(c1.ok && c1.candidate.characterId === 'CHAR-001', '01 candidate has character ID');
ok(c1.ok && c1.candidate.eraId === 'ERA-01', '02 candidate has era');
ok(c1.ok && !!c1.candidate.sha256, '03 candidate has artifact hash');
ok(c1.ok && c1.candidate.candidateId === 'MINH-E01-CANDIDATE-001', 'candidate code');

const dup = registerCandidate(c1.ok ? c1.candidates : [], { ...c1.candidate });
ok(!dup.ok && /overwrite/i.test(dup.blocked), '12 locked/existing candidate no overwrite');

const missing = evaluateMasterQa({ artifactExists: false, expectedCharacterId: 'CHAR-001' });
ok(!missing.ok && missing.status === 'BLOCK' && missing.p0.includes('ARTIFACT_MISSING'), '04 artifact missing BLOCK');

const hashChange = evaluateMasterQa({
  artifactExists: true,
  sha256: 'bbb222',
  persistedSha256: 'aaa111',
  identityMatch: true,
  styleMatch: true,
  age: 11,
  expectedAge: 11,
});
ok(!hashChange.ok && hashChange.status === 'BLOCK' && hashChange.p0.includes('HASH_MISMATCH'), '05 hash change BLOCK');

const sheet = canPromoteToProductionStill('CHARACTER_SHEET');
const sheetKind = distinguishFrameKind('CHARACTER_SHEET');
ok(!sheet.ok && !sheetKind.i2vAllowed, '06 character sheet cannot become Production Still');

const age = evaluateMasterQa({
  artifactExists: true,
  sha256: 'aaa111',
  persistedSha256: 'aaa111',
  identityMatch: true,
  styleMatch: true,
  age: 16,
  expectedAge: 11,
});
ok(!age.ok && age.p0.includes('AGE_FAIL'), '07 wrong age FAIL');

const identity = evaluateMasterQa({
  artifactExists: true,
  sha256: 'aaa111',
  persistedSha256: 'aaa111',
  identityMatch: false,
  characterId: 'generic-ai-boy',
  expectedCharacterId: 'CHAR-001',
  styleMatch: true,
  age: 11,
  expectedAge: 11,
});
ok(!identity.ok && identity.p0.includes('IDENTITY_FAIL'), '08 wrong identity FAIL');

const style = evaluateMasterQa({
  artifactExists: true,
  sha256: 'aaa111',
  persistedSha256: 'aaa111',
  identityMatch: true,
  styleMatch: false,
  photoreal: true,
  age: 11,
  expectedAge: 11,
});
ok(!style.ok && style.p0.some((x) => /STYLE/.test(x)), '09 wrong visual style FAIL');

const prettyWrong = evaluateMasterQa({
  artifactExists: true,
  sha256: 'aaa111',
  persistedSha256: 'aaa111',
  identityMatch: false,
  styleMatch: true,
  beautyScore: 99,
  age: 11,
  expectedAge: 11,
});
ok(!prettyWrong.ok && prettyWrong.usedBeautyScore === false && prettyWrong.p0.includes('IDENTITY_FAIL'), 'beauty score does not pass identity');

const angles = evaluateMasterQa({
  artifactExists: true,
  sha256: 'aaa111',
  persistedSha256: 'aaa111',
  identityMatch: true,
  styleMatch: true,
  age: 11,
  expectedAge: 11,
  angles: [
    { role: 'FRONT', identityMatch: true },
    { role: 'SIDE', identityMatch: false },
  ],
});
ok(!angles.ok && angles.p1.some((x) => /ANGLE_INCONSISTENT:SIDE/.test(x)), '10 multi-angle inconsistency FAIL');

const expr = evaluateMasterQa({
  artifactExists: true,
  sha256: 'aaa111',
  persistedSha256: 'aaa111',
  identityMatch: true,
  styleMatch: true,
  age: 11,
  expectedAge: 11,
  expressions: [
    { role: 'NEUTRAL', identityMatch: true },
    { role: 'SAD', identityMatch: false },
  ],
});
ok(!expr.ok && expr.p1.some((x) => /EXPRESSION_INCONSISTENT:SAD/.test(x)), '11 expression inconsistency FAIL');

try {
  ensureMasterImmutable({ status: 'LOCKED', version: 'V1' }, { version: 'V1' });
  fail.push('12 locked V1 must throw');
} catch (e) {
  ok(/MASTER_LOCKED/.test(String(e)), '12 locked reference no overwrite');
}

const v1 = { version: 'V1', sha256: 'aaa111', path: 'v1.jpg' };
const v2 = { version: nextMasterVersion(v1.version), sha256: 'ccc333', path: 'v2.jpg' };
ok(v2.version === 'V2' && v1.version === 'V1' && v1.sha256 === 'aaa111', '13 V2 does not change V1');

const still = productionStillMayReferenceMaster({ status: 'LOCKED', path: 'master-front.jpg' }, { path: 'sh01-01.jpg' });
ok(still.ok && still.stillIsNotMaster && still.source === 'LOCKED_MASTER_REFERENCE', '14 production still can reference Master');

ok(isProviderIndependent() && refuseGenerate().blocked.includes('GEMINI_NOT_CALLED'), '15 Master does not depend on Gemini');
ok(refuseGenerate(true).blocked.includes('must not become Canon'), '15 controlled test is not Canon');

ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '16 engine id unchanged');

ok(!canUseAsMasterSource('/content/famixa/canon/CHAR-001-FRONT.png').ok, 'legacy sheet is not Master');
ok(!canUseAsMasterSource('take-01.mp4').ok, 'Golden take-01 is not Master');
ok(!canLockMaster({ status: 'DRAFT', hashes: {}, directorApproved: false }).ok, 'no Lock without gates');
ok(diagnoseRepair('HAIR_SILHOUETTE').region === 'HAIR', 'repair hair only');
ok(!doNotBlindRetry({ fingerprint: 'fp1', prompt: 'p' }, { fingerprint: 'fp1', prompt: 'p' }).ok, 'blind retry blocked');
ok(persistenceFailMeansRevalidate('database hash ui'), 'persist fail revalidate');
ok(nextCandidateCode(['MINH-E01-CANDIDATE-001']) === 'MINH-E01-CANDIDATE-002', 'candidate increment');
ok(eraRemainsChar001('CHAR-001', 'ERA-02').ok && !eraRemainsChar001('CHAR-005', 'ERA-02').ok, 'Minh stays CHAR-001');
ok(!compileMasterCandidatePrompt({ role: 'FRONT', dnaApproved: false, styleReady: true }).ok, 'DNA DRAFT blocks compile');
ok(compileMasterCandidatePrompt({ role: 'FRONT', dnaApproved: true, styleReady: true }).ok, 'compile prepared without provider');

if (fail.length) {
  console.error('KIT VIDEO MASTER REFERENCE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO MASTER REFERENCE PASS · tests 01–16 · CHAR-001 ERA-01 DRAFT · no Gemini · no auto-LOCK');
