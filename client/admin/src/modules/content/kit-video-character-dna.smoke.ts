import { MASTER_CODE_V1 } from './kit-video-master-lock';
import {
  CHARACTER_DNA_CODE,
  CHARACTER_DNA_VERSION,
  MINH_CHARACTER_DNA_ID,
  buildMinhDnaV1Spec,
  canApproveCharacterDna,
  canCreateCharacterDna,
  directorGatePass,
  dnaApproveIdempotent,
  dnaCreatesPixels,
  dnaGoldenUntouched,
  dnaShaInvalid,
  evaluateDirectorGate,
  evaluateSpecChecks,
  isDnaComplete,
  rejectDnaMutation,
} from './kit-video-character-dna';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const spec = buildMinhDnaV1Spec('abc123sha');
const locked = {
  masterExists: true,
  masterLocked: true,
  masterCode: MASTER_CODE_V1,
  shaValid: true,
  spec,
  status: 'DRAFT',
  identityPass: true,
  stressPass: true,
  p0: 0,
};

ok(MINH_CHARACTER_DNA_ID === 'CHAR-001_MINH_CHARACTER_DNA_V1', '01 document id');
ok(CHARACTER_DNA_CODE === 'CHAR-001-MINH-ERA01-DNA-V1', '01b dna code');
ok(canCreateCharacterDna(locked).ok && canCreateCharacterDna(locked).approved === false, '1 Master LOCKED → create DRAFT');
ok(!canCreateCharacterDna({ ...locked, masterLocked: false }).ok, '2 Master PENDING → no create / no APPROVED');
ok(!canCreateCharacterDna({ ...locked, shaValid: false }).ok, '3 Master SHA mismatch → reject');
ok(!canApproveCharacterDna({ ...locked, spec: { ...spec, identityInvariants: { CRITICAL: [], HIGH: [], VARIABLE: [] } } }).ok, '4 missing Identity Invariants');
ok(!canApproveCharacterDna({ ...locked, spec: { ...spec, forbiddenVariation: [] } }).ok, '5 missing Forbidden Variation');
ok(!canApproveCharacterDna({ ...locked, spec: { ...spec, allowedVariation: [] } }).ok, '6 missing Allowed Variation');
ok(canApproveCharacterDna(locked).ok && canApproveCharacterDna(locked).next === 'LOCKED', '7 Director approve → APPROVED / LOCKED');
ok(!rejectDnaMutation('LOCKED', 'UPDATE').ok && !rejectDnaMutation('LOCKED', 'CHANGE_SHA256').ok, '8 edit LOCKED → DNA_LOCKED');
ok(dnaApproveIdempotent('V1').ok && dnaApproveIdempotent('V1').createdV2 === false, '9 double approve stays V1');
ok(isDnaComplete(spec) && spec.derived_from_master === MASTER_CODE_V1, '10 spec complete + provenance');
ok(!dnaCreatesPixels('APPROVE') && dnaCreatesPixels('GENERATE'), '11 no generate');
ok(dnaGoldenUntouched('kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-004-D.jpg'), '12 Golden SH01-01 untouched');
ok(dnaShaInvalid('aaa', 'bbb'), '13 SHA drift invalidates DNA');
ok(CHARACTER_DNA_VERSION === 'V1', '14 version V1');
ok(!canCreateCharacterDna({ ...locked, lockedDna: true }).ok, '15 no create after DNA LOCKED');
ok(!canApproveCharacterDna({ ...locked, identityPass: false }).ok, '16 Identity FAIL blocks approve');
ok(!canApproveCharacterDna({ ...locked, stressPass: false }).ok, '17 Stress FAIL blocks approve');
ok(!canApproveCharacterDna({ ...locked, p0: 1 }).ok, '18 P0 > 0 blocks approve');
ok(!!spec.variationBoundaries && !!spec.regressionRules && !!spec.face && Array.isArray((spec.face as { invariant?: string[] }).invariant), '19 review groups + L-P');
const gate = evaluateDirectorGate(locked);
ok(directorGatePass(gate) && evaluateSpecChecks(spec).every((x) => x.pass), '20 director gate PASS on complete V1');
ok(!directorGatePass(evaluateDirectorGate({ ...locked, spec: { ...spec, face: {} } })), '21 Face invariant FAIL blocks gate');
ok(!directorGatePass(evaluateDirectorGate({ ...locked, spec: { ...spec, continuityRules: {} } })), '22 Continuity rules FAIL blocks gate');
ok(evaluateSpecChecks({ ...spec, hair: {} }).find((x) => x.code === 'hair_invariant')?.reason === 'thiếu Hair invariant', '23 FAIL has reason');

if (fail.length) {
  console.error('KIT VIDEO CHARACTER DNA FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO CHARACTER DNA PASS · 25 tests · director gate · derived from MASTER-V1 · no generate · no Golden');
