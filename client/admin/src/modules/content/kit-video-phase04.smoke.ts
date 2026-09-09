import {
  applyDirectorDecision,
  buildI2vReadyPackage,
  canRetry,
  compileVisualContract,
  compileVisualPrompt,
  KIT_VIDEO_VISION,
  MAX_AUTO_ATTEMPTS,
  recordAttempt,
  referencePackForNextShot,
  directorFeedbackToRevision,
  evaluateVisionQa,
  type KitVideoVisionObservation,
} from './kit-video-vision';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(KIT_VIDEO_VISION === 'KIT-VIDEO-VISION-V1', 'engine vision id');
ok(MAX_AUTO_ATTEMPTS === 3, 'max auto attempts');

const contract = compileVisualContract({
  shotCode: 'SH01-02',
  action: 'Minh đưa bài kiểm tra cho mẹ.',
  characters: [
    { code: 'CHAR-001', name: 'Minh', version: 'V1', era: 'ERA-01', reference: '/canon/minh-front.png' },
    { code: 'CHAR-003', name: 'Linh', version: 'V1', era: 'ERA-01', reference: '/canon/linh-front.png' },
  ],
  props: [{ code: 'PROP-001', name: 'Test paper', state: 'held_by_minh' }],
  location: { code: 'LOC-001', name: 'Living room', time: 'Evening', lighting: 'Warm indoor lighting' },
  wardrobe: { 'CHAR-001': 'WARDROBE-001' },
});

ok(contract.items.some((i) => i.id === 'CHAR-001' && i.level === 'MANDATORY' && i.priority === 'P0'), 'contract minh mandatory');
ok(contract.items.some((i) => i.id === 'CHAR-003' && i.level === 'MANDATORY'), 'contract mother mandatory');
ok(contract.items.some((i) => i.id === 'PROP-001' && i.level === 'MANDATORY'), 'contract paper mandatory');
ok(contract.items.some((i) => i.id === 'WINDOW' && i.level === 'OPTIONAL'), 'window optional');
ok(contract.items.some((i) => i.id === 'EXTRA_PERSON' && i.level === 'FORBIDDEN'), 'extra forbidden');
ok(contract.actionVisibility.includes('Interaction'), 'action visibility');

const clean: KitVideoVisionObservation = {
  characters: ['CHAR-001', 'CHAR-003'],
  props: [{ code: 'PROP-001', state: 'held_by_minh' }],
  location: 'LOC-001',
  wardrobe: { 'CHAR-001': 'WARDROBE-001' },
  actionVisible: true,
  croppedOut: [],
  missingHeads: [],
  missingHandsRelevant: false,
  occludedFaces: [],
  identityMatch: { 'CHAR-001': true, 'CHAR-003': true },
  lightingDelta: 'none',
  integrity: { readable: true, width: 1280, height: 720, aspect: '16:9', unexpectedText: false, watermark: false, artifact: false },
};

// TEST 01
const pass2 = evaluateVisionQa(contract, clean);
ok(pass2.status === 'PASS' && pass2.allowI2v, '01 two characters PASS');
ok(pass2.scores.action === 100 && pass2.p0Fail.length === 0, '01 scores do not hide P0');

// TEST 02
const extra = evaluateVisionQa(contract, { ...clean, characters: ['CHAR-001', 'CHAR-003', 'CHAR-002'] });
ok(extra.status === 'FAIL' && extra.p0Fail.some((r) => r.includes('Expected 2') && r.includes('Detected 3')), '02 extra FAIL');

// TEST 03
const missingMother = evaluateVisionQa(contract, { ...clean, characters: ['CHAR-001'] });
ok(missingMother.status === 'FAIL' && missingMother.p0Fail.some((r) => r.includes('CHAR-003')), '03 missing mother FAIL');

// TEST 04
const noPaper = evaluateVisionQa(contract, { ...clean, props: [] });
ok(noPaper.status === 'FAIL' && noPaper.p0Fail.some((r) => r.includes('PROP-001')), '04 missing paper FAIL');

// TEST 05
const onTable = evaluateVisionQa(contract, { ...clean, props: [{ code: 'PROP-001', state: 'on_table' }] });
ok(onTable.status === 'FAIL' && onTable.p0Fail.some((r) => r.includes('on_table')), '05 held vs table FAIL');

// TEST 06
const homeClothes = evaluateVisionQa(contract, { ...clean, wardrobe: { 'CHAR-001': 'WARDROBE-002' } });
ok(homeClothes.status === 'FAIL' && homeClothes.p0Fail.some((r) => r.includes('WARDROBE')), '06 wardrobe FAIL');

// TEST 07
const bedroom = evaluateVisionQa(contract, { ...clean, location: 'Bedroom' });
ok(bedroom.status === 'FAIL' && bedroom.p0Fail.some((r) => /Location|Bedroom/i.test(r)), '07 location FAIL');

// TEST 08
const cropped = evaluateVisionQa(contract, { ...clean, croppedOut: ['CHAR-003'] });
ok(cropped.status === 'FAIL' && cropped.p0Fail.some((r) => r.includes('cropped')), '08 crop FAIL');

// TEST 09
const otherFace = evaluateVisionQa(contract, { ...clean, identityMatch: { 'CHAR-001': false, 'CHAR-003': true } });
ok(otherFace.status === 'FAIL' && otherFace.p0Fail.some((r) => r.includes('face identity')), '09 face FAIL');
ok(otherFace.scores.character > 50 && otherFace.status === 'FAIL', '09 high score still FAIL on P0');

// TEST 10
const approvedPass = recordAttempt([], 'SH01-02', 'fp-pass', pass2);
const rejected = applyDirectorDecision({ ...approvedPass, status: 'QA_PASS', qa: pass2 }, 'REJECT');
ok(rejected.status === 'REJECTED', '10 rejected');
ok(!buildI2vReadyPackage(rejected).ready, '10 reject no I2V');

// TEST 11
const failAtt = recordAttempt([], 'SH01-02', 'fp-fail', extra);
ok(failAtt.qa && !failAtt.qa.canBeReference, '11 fail not reference');
ok(referencePackForNextShot([failAtt]).length === 0, '11 fail excluded from refs');

// TEST 12
const a1 = recordAttempt([], 'SH01-03', 'fp-a', pass2);
const a2 = recordAttempt([a1], 'SH01-03', 'fp-b', pass2);
ok(a1.attemptNo === 1 && a2.attemptNo === 2, '12 new attempt');
ok(a1.attemptId !== a2.attemptId, '12 no overwrite');

// TEST 13
let blind = false;
try {
  recordAttempt([failAtt], 'SH01-02', failAtt.fingerprint, extra);
} catch {
  blind = true;
}
ok(blind, '13 blind retry blocked');
ok(!canRetry({ fingerprint: 'same' }, 'same', false).ok, '13 same fingerprint blocked');
ok(canRetry({ fingerprint: 'old' }, 'new', true).ok, '13 strategy change allowed');

// TEST 14
const dialogue = 'Mẹ ơi, con được 9 điểm!';
const req = compileVisualPrompt(contract, { projectStyle: 'Famixa still' });
ok(!req.hasDialogue && !req.prompt.includes(dialogue), '14 no dialogue');
ok(req.sections.SUBJECT && req.sections.ACTION && req.sections.NEGATIVE, '14 prompt sections');
ok(!req.prompt.includes('{') && !req.prompt.includes('pack_content'), '14 no json dump');
ok(req.i2vImageSource === 'APPROVED_KEYFRAME', '14 i2v source keyframe');
let dumped = false;
try {
  compileVisualPrompt(contract, { dialogue, projectStyle: dialogue });
} catch {
  dumped = true;
}
ok(dumped, '14 compiler rejects dialogue in prompt');

// TEST 15
const approved = applyDirectorDecision({ ...a1, status: 'QA_PASS', qa: pass2 }, 'APPROVE');
const pack = buildI2vReadyPackage(approved);
ok(pack.ready && pack.source === 'APPROVED_KEYFRAME' && pack.runwaySubmitted === false, '15 I2V package no Runway');
ok(referencePackForNextShot([approved, failAtt]).every((a) => a.status === 'APPROVED'), '15 only approved refs');

const revision = directorFeedbackToRevision('Không thấy bài kiểm tra');
ok(/PROP-001|test paper|hands/i.test(revision), 'director feedback compiled');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 04 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PHASE 04 PASS · tests 01–15 · visual contract + vision QA');
