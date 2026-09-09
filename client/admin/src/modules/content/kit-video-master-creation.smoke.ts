import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import {
  artifactFailed,
  buildMasterContract,
  compileMasterCreationPrompt,
  directorDecision,
  evaluateCreationQa,
  identityScore,
  imageTypeFail,
  MAX_AUTO_ATTEMPTS,
  MAX_CANDIDATE_BATCH,
  MINH_MASTER_CREATION_ID,
  noRunwayInCreation,
  precheckGenerate,
  productionAcceptsMaster,
  rankMasterCandidates,
} from './kit-video-master-creation';
import {
  doNotBlindRetry,
  ensureMasterImmutable,
  nextMasterVersion,
} from './kit-video-master-reference';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(MINH_MASTER_CREATION_ID === 'CHAR-001_MINH_MASTER_REFERENCE_CREATION_V1', 'creation spec id');
ok(MAX_CANDIDATE_BATCH === 4 && MAX_AUTO_ATTEMPTS === 3, 'batch / attempt caps');

const contract = buildMasterContract({ candidateId: 'MINH-E01-CANDIDATE-001', attemptNumber: 1 });
const compiled = compileMasterCreationPrompt(contract);
ok(compiled.ok && compiled.prompt.includes('[STYLE]') && compiled.prompt.includes('[CHARACTER DNA]'), 'compiler sections');
ok(compiled.ok && !compiled.prompt.includes('{') && !/dialogue/i.test(compiled.prompt.split('[FORBIDDEN]')[0] || ''), 'no JSON dump / no dialogue in instruction');
ok(!!compiled.fingerprint, '01 generate contract has fingerprint');

ok(artifactFailed({ exists: true, readable: true, mime: 'image/jpeg', width: 768, height: 1024 }).ok, '01 artifact valid');
ok(artifactFailed({ exists: false, readable: false }).status === 'ARTIFACT_FAILED', '02 corrupt / missing artifact');
ok(imageTypeFail('CHARACTER_SHEET').status === 'IMAGE_TYPE_FAIL', '03 character sheet IMAGE_TYPE_FAIL');

const age = evaluateCreationQa({ artifactExists: true, sha256: 'a', persistedSha256: 'a', identityMatch: true, styleMatch: true, age: 16, expectedAge: 11 });
ok(!age.ok && age.p0.includes('AGE_FAIL'), '04 wrong age');

const face = evaluateCreationQa({ artifactExists: true, sha256: 'a', persistedSha256: 'a', faceMatch: false, identityMatch: true, styleMatch: true, age: 11, expectedAge: 11 });
ok(!face.ok && (face.p0.includes('FACE_FAIL') || face.p0.includes('IDENTITY_FAIL')), '05 wrong face');

const hair = evaluateCreationQa({ artifactExists: true, sha256: 'a', persistedSha256: 'a', identityMatch: true, hairMatch: false, styleMatch: true, age: 11, expectedAge: 11 });
ok(!hair.ok && hair.p0.includes('HAIR_FAIL'), '06 wrong hair');

const body = evaluateCreationQa({ artifactExists: true, sha256: 'a', persistedSha256: 'a', identityMatch: true, styleMatch: true, childProportion: false, age: 11, expectedAge: 11 });
ok(!body.ok && body.p0.includes('BODY_FAIL'), '07 wrong body');

const style = evaluateCreationQa({ artifactExists: true, sha256: 'a', persistedSha256: 'a', identityMatch: true, styleMatch: false, age: 11, expectedAge: 11 });
ok(!style.ok && style.p0.some((x) => /STYLE/.test(x)), '08 wrong style');

const angle = evaluateCreationQa({
  artifactExists: true,
  sha256: 'a',
  persistedSha256: 'a',
  identityMatch: true,
  styleMatch: true,
  age: 11,
  expectedAge: 11,
  angles: [{ role: 'THREE_Q_RIGHT', identityMatch: false }],
});
ok(!angle.ok && angle.p1.some((x) => /ANGLE/.test(x)), '09 front/3-4 drift');

const expr = evaluateCreationQa({
  artifactExists: true,
  sha256: 'a',
  persistedSha256: 'a',
  identityMatch: true,
  styleMatch: true,
  age: 11,
  expectedAge: 11,
  expressions: [{ role: 'SAD', identityMatch: false }],
});
ok(!expr.ok && expr.p1.some((x) => /EXPRESSION/.test(x)), '10 expression drift');

ok(!doNotBlindRetry({ fingerprint: 'fp', prompt: 'p' }, { fingerprint: 'fp', prompt: 'p' }).ok, '11 same fingerprint blind retry');
ok(!precheckGenerate({ confirmed: true, batchCount: 0, attemptsOnFingerprint: 0, sameFingerprint: true }).ok, '11 precheck diagnosis required');

const rejected = directorDecision('REJECT');
ok(!rejected.masterCreated && rejected.status === 'REJECTED', '12 director reject no master');

const approved = directorDecision('APPROVE');
ok(approved.masterCreated && approved.status === 'APPROVED' && approved.locked === false, '13 director approve is not lock');

const hashLock = evaluateCreationQa({ artifactExists: true, sha256: 'bbb', persistedSha256: 'aaa', identityMatch: true, styleMatch: true, age: 11, expectedAge: 11 });
ok(hashLock.status === 'BLOCK' && hashLock.p0.includes('HASH_MISMATCH'), '14 hash mismatch no lock');

try {
  ensureMasterImmutable({ status: 'LOCKED', version: 'V1' }, { version: 'V1' });
  fail.push('15 locked overwrite');
} catch (e) {
  ok(/MASTER_LOCKED/.test(String(e)), '15 locked V1 no overwrite');
}

const v1 = { version: 'V1', hash: 'aaa' };
const v2 = { version: nextMasterVersion(v1.version), hash: 'ccc' };
ok(v2.version === 'V2' && v1.version === 'V1' && v1.hash === 'aaa', '16 V2 keeps V1');

ok(!productionAcceptsMaster('DRAFT') && !productionAcceptsMaster('APPROVED') && productionAcceptsMaster('LOCKED'), '17 production only LOCKED');
ok(noRunwayInCreation(compiled.prompt + MINH_MASTER_CREATION_ID), '18 no Runway in creation');
ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '19 engine id unchanged');

const prettyFail = rankMasterCandidates([
  { id: 'pretty', p0: ['IDENTITY_FAIL'], score: 98 },
  { id: 'plain', p0: [], score: 71 },
]);
ok(prettyFail.winner === 'plain' && prettyFail.rejectedHighScore.includes('pretty') && prettyFail.usedScoreAsOverride === false, 'score does not override P0');
ok(identityScore({ face: 80, hair: 80, body: 80, age: 80, style: 80, expression: 80, artifact: 80 }) > 70, 'rank helper');
ok(!precheckGenerate({ confirmed: false, batchCount: 0, attemptsOnFingerprint: 0 }).ok, 'credit gate');
ok(!precheckGenerate({ confirmed: true, batchCount: 4, attemptsOnFingerprint: 0 }).ok, 'batch 4');
ok(!precheckGenerate({ confirmed: true, batchCount: 0, attemptsOnFingerprint: 3 }).ok, 'max attempts');

if (fail.length) {
  console.error('KIT VIDEO MASTER CREATION FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO MASTER CREATION PASS · tests 01–19 · no auto-LOCK · no Runway');
