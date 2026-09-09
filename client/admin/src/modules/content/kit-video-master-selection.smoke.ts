import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import {
  appendSelectionEvent,
  bindMasterToCandidate,
  bindVariation,
  compareCandidates,
  directorSelect,
  ensureCanVary,
  ensureMasterImmutable,
  evaluateSelection,
  markFrontRunner,
  masterHashValid,
  MINH_MASTER_SELECTION_ID,
  MINH_MASTER_SELECTION_REFINEMENT_ID,
  nextMasterVersion,
  nextVariationCode,
  noGeminiInSelection,
  resolveLockedMaster,
  selectionAuditComplete,
} from './kit-video-master-selection';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(MINH_MASTER_SELECTION_ID === 'CHAR-001_MINH_MASTER_REFERENCE_SELECTION_V1', 'selection spec id');

const valid = {
  projectCode: 'FAMIXA',
  characterId: 'CHAR-001',
  eraId: 'ERA-01',
  candidateId: 'MINH-E01-CANDIDATE-002',
  artifactPath: 'App_Data/kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-002.jpg',
  sha256: 'aaa111',
  liveSha256: 'aaa111',
  artifactExists: true,
  readable: true,
  width: 768,
  height: 1024,
  imageType: 'PRODUCTION_STILL',
  qaSha256: 'aaa111',
  lifecycle: 'ELIGIBLE',
  characterCount: 1,
  faceVisible: true,
  hairVisible: true,
  eyesVisible: true,
  visionPass: true,
  qaStatus: 'PASS',
  faceMatch: 88,
  dnaMatch: 86,
  ageScore: 84,
  hairMatch: 80,
  eyeMatch: 82,
  distinctiveness: 78,
  stability: 80,
  ageLook: '11' as const,
  childProportion: true,
  dnaApproved: true,
};

const pass = evaluateSelection(valid);
ok(pass.eligible && pass.lifecycle === 'ELIGIBLE', '01 valid → ELIGIBLE');

const badFile = evaluateSelection({ ...valid, artifactExists: false, readable: false });
ok(!badFile.eligible && (badFile.lifecycle === 'FAILED' || badFile.lifecycle === 'NOT_ELIGIBLE'), '02 invalid → INELIGIBLE');

const sheet = evaluateSelection({ ...valid, imageType: 'CHARACTER_SHEET' });
ok(!sheet.eligible && sheet.p0.some((x) => /IMAGE_TYPE/.test(x)), '03 character sheet BLOCK');

const crowd = evaluateSelection({ ...valid, characterCount: 2 });
ok(!crowd.eligible && crowd.p0.some((x) => /CHARACTER_COUNT/.test(x)), '04 multi-character BLOCK');

const noFace = evaluateSelection({ ...valid, faceVisible: false });
ok(!noFace.eligible && noFace.p0.some((x) => /IDENTITY/.test(x)), '05 missing face BLOCK');

ok(pass.p0.length === 0 && pass.visualDnaId === 'CHAR-001_MINH_VISUAL_DNA_V1', '06 / 07 vision + DNA comparison PASS');

const weak = evaluateSelection({
  ...valid,
  candidateId: 'MINH-E01-CANDIDATE-001',
  faceMatch: 60,
  dnaMatch: 58,
  distinctiveness: 40,
  genericAiFace: true,
});
const cmp = compareCandidates([
  { ...valid, candidateId: 'MINH-E01-CANDIDATE-002' },
  { ...valid, candidateId: 'MINH-E01-CANDIDATE-003', faceMatch: 92, dnaMatch: 90, distinctiveness: 88 },
  { ...valid, candidateId: 'MINH-E01-CANDIDATE-001', faceMatch: 60, dnaMatch: 58, genericAiFace: true },
]);
ok(cmp.compared === 3 && cmp.recommendedCandidateId === 'MINH-E01-CANDIDATE-003', '08 compare + 09 ranking');
ok(cmp.autoSelected === false && cmp.usedScoreAsOverride === false, '09 rank helper');

const prettyFail = evaluateSelection({
  ...valid,
  faceVisible: false,
  faceMatch: 96,
  dnaMatch: 96,
  hairMatch: 96,
  eyeMatch: 96,
  distinctiveness: 96,
  stability: 96,
});
ok(!prettyFail.eligible && prettyFail.score.total >= 90 && prettyFail.usedScoreAsOverride === false, '10 high score + P0 FAIL → BLOCK');

ok(cmp.ranks[0]?.recommendation === 'RECOMMENDED' && cmp.autoSelected === false, '11 AI recommendation ≠ automatic selection');

const selected = directorSelect({
  decision: 'SELECT_AS_MASTER',
  result: pass,
  dnaApproved: true,
  lifecycle: 'ELIGIBLE',
  imageType: 'PRODUCTION_STILL',
});
ok(selected.ok && selected.masterCreated && selected.generated === false, '12 director select → MASTER');
ok(
  !directorSelect({
    decision: 'SELECT_AS_MASTER',
    result: prettyFail,
    dnaApproved: true,
    lifecycle: 'ELIGIBLE',
    imageType: 'PRODUCTION_STILL',
  }).ok,
  '12 P0 cannot select',
);
ok(!directorSelect({ decision: 'REJECT', result: pass, dnaApproved: true }).masterCreated, '12 reject no master');

const bound = bindMasterToCandidate({
  candidateId: 'MINH-E01-CANDIDATE-004',
  artifactPath: valid.artifactPath,
  sha256: 'bbb222',
});
ok(bound.sourceCandidate === 'MINH-E01-CANDIDATE-004' && bound.sourceArtifact === valid.artifactPath && !bound.generated && !bound.copiedBytes, '13 master keeps artifact');
ok(bound.sha256 === 'bbb222', '14 SHA256 stored');

try {
  ensureMasterImmutable({ status: 'LOCKED', version: 'V1' }, { version: 'V1' });
  fail.push('15 locked overwrite');
} catch (e) {
  ok(/MASTER_LOCKED/.test(String(e)), '15 LOCKED immutable');
}
try {
  ensureMasterImmutable({ status: 'LOCKED', version: 'V1' }, { version: 'V1', overwrite: true });
  fail.push('16 locked V1 overwrite');
} catch (e) {
  ok(/MASTER_LOCKED/.test(String(e)), '16 locked V1 no overwrite');
}

const v2 = nextMasterVersion('V1');
ok(v2 === 'V2', '17 V2 is new version');

ok(isLegacyOrGoldenPath('take-01.mp4') && !resolveLockedMaster({ status: 'LOCKED', path: 'take-01.mp4', sha256: 'x', fileSha256: 'x' }).ok, '18 golden video not Master');

const resolved = resolveLockedMaster({
  status: 'LOCKED',
  sha256: 'bbb222',
  fileSha256: 'bbb222',
  path: valid.artifactPath,
});
ok(resolved.ok && resolved.source === 'LOCKED_MASTER_REFERENCE', '19 resolve production');
ok(!resolveLockedMaster({ status: 'APPROVED', sha256: 'bbb222', fileSha256: 'bbb222', path: valid.artifactPath }).ok, '19 draft/approved not production');
ok(!masterHashValid('aaa', 'bbb').ok, '14/19 hash mismatch invalid');

const events = [
  { type: 'CANDIDATE_CREATED' },
  { type: 'CANDIDATE_ANALYZED' },
  { type: 'CANDIDATE_REJECTED' },
  { type: 'CANDIDATE_SELECTED', actor: 'director', candidateId: 'MINH-E01-CANDIDATE-002', sha256: 'aaa111' },
  { type: 'MASTER_CREATED' },
  { type: 'MASTER_APPROVED' },
  { type: 'MASTER_LOCKED' },
].reduce((acc, ev) => appendSelectionEvent(acc, ev), [] as { type: string }[]);
ok(selectionAuditComplete(events), '20 audit log complete');

ok(noGeminiInSelection(MINH_MASTER_SELECTION_ID + ' analyze compare select lock'), 'no generate in selection spec');
ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', 'engine id unchanged');
ok(weak.recommendation !== 'RECOMMENDED' || weak.score.total >= 80, 'weak not forced recommended');

ok(
  !directorSelect({
    decision: 'SELECT_AS_MASTER',
    result: pass,
    dnaApproved: true,
    lifecycle: 'DRAFT',
    imageType: 'PRODUCTION_STILL',
  }).ok,
  '21 DRAFT cannot SELECT',
);
ok(
  !directorSelect({
    decision: 'SELECT_AS_MASTER',
    result: pass,
    dnaApproved: true,
    lifecycle: 'ELIGIBLE',
    imageType: 'CHARACTER_CANDIDATE',
  }).ok,
  '21 CHARACTER_CANDIDATE cannot SELECT',
);

const runner = markFrontRunner({ lifecycle: 'REVIEW', canonEligible: true, frontRunnerCount: 1 });
ok(runner.ok && runner.lifecycle === 'FRONT_RUNNER' && !runner.canon && !runner.production && !runner.masterCreated, '22 FRONT_RUNNER shortlist');
ok(!markFrontRunner({ lifecycle: 'REJECTED', canonEligible: true }).ok, '22 REJECTED cannot be FRONT_RUNNER');
ok(!markFrontRunner({ controlledTest: true, canonEligible: false }).ok, '22 controlled test cannot be FRONT_RUNNER');

const vA = nextVariationCode('MINH-E01-CANDIDATE-004', []);
const vB = nextVariationCode('MINH-E01-CANDIDATE-004', [vA.code]);
const vC = nextVariationCode('MINH-E01-CANDIDATE-004', [vA.code, vB.code]);
const vD = nextVariationCode('MINH-E01-CANDIDATE-004', [vA.code, vB.code, vC.code]);
const vE = nextVariationCode('MINH-E01-CANDIDATE-004', [vA.code, vB.code, vC.code, vD.code]);
ok(
  vA.code === 'MINH-E01-CANDIDATE-004-A' &&
    vB.code === 'MINH-E01-CANDIDATE-004-B' &&
    vC.code === 'MINH-E01-CANDIDATE-004-C' &&
    vD.code === 'MINH-E01-CANDIDATE-004-D' &&
    vE.code === 'MINH-E01-CANDIDATE-004-E',
  '23 variation A–E',
);
ok(!nextVariationCode('MINH-E01-CANDIDATE-004', [vA.code, vB.code, vC.code, vD.code, vE.code]).ok, '23 variation cap 5');
const family = bindVariation({ id: 'parent-004', code: 'MINH-E01-CANDIDATE-004' }, vA.code);
ok(family.parentCandidateId === 'parent-004' && family.sourceCandidateId === 'parent-004' && !family.overwriteParent, '23 parent/source ids');
ok(!family.autoSelected && !family.autoApproved && !family.autoLocked, '24 variation not auto SELECT/APPROVE/LOCK');
ok(ensureCanVary({ parentLifecycle: 'FRONT_RUNNER', existingVariations: 0 }).ok, '24 can vary from FRONT_RUNNER');
ok(!ensureCanVary({ parentLifecycle: 'REJECTED' }).ok, '24 cannot vary REJECTED');

const hashDrift = evaluateSelection({ ...valid, qaSha256: 'zzz999' });
ok(!hashDrift.eligible && hashDrift.p0.some((x) => /HASH/.test(x)), '26 QA hash must match artifact');
ok(MINH_MASTER_SELECTION_REFINEMENT_ID === 'CHAR-001_MASTER_REFERENCE_SELECTION_REFINEMENT_V1', '28 refinement id');

if (fail.length) {
  console.error('KIT VIDEO MASTER SELECTION FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO MASTER SELECTION PASS · tests 01–28 · gate + FRONT_RUNNER + variation · no auto-LOCK · no Gemini generate');
