import { evaluateVisionQa, type KitVideoVisionObservation } from './kit-video-vision';
import { famixaGoldenContract, inferImageType, integrityI2vReady } from './kit-video-pixel';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const contract = famixaGoldenContract();
ok(contract.productionImage?.imageType === 'PRODUCTION_STILL', 'production image contract');
ok(contract.productionImage?.noCharacterSheet && contract.productionImage?.noLogo, 'sheet and logo forbidden');

ok(inferImageType('PRODUCTION_STILL') === 'PRODUCTION_STILL', 'still type');
ok(inferImageType('PRODUCTION_STILL', 'this is a character sheet with wardrobe grid') === 'CHARACTER_SHEET', 'sheet inferred from raw');

const hashA = 'aaa111';
const hashB = 'bbb222';
const approved = integrityI2vReady({
  attemptStatus: 'APPROVED',
  qaStatus: 'PASS',
  p0Fail: [],
  liveHash: hashA,
  storedHash: hashA,
  qaHash: hashA,
  approvedHash: hashA,
  imageType: 'PRODUCTION_STILL',
  artifactOk: true,
});
ok(approved.ready && approved.runwaySubmitted === false, 'matching hashes I2V ready');

ok(
  !integrityI2vReady({
    attemptStatus: 'APPROVED',
    qaStatus: 'PASS',
    liveHash: hashB,
    storedHash: hashA,
    qaHash: hashA,
    approvedHash: hashA,
    imageType: 'PRODUCTION_STILL',
    artifactOk: true,
  }).ready,
  'replace artifact after approve BLOCK',
);

ok(
  !integrityI2vReady({
    attemptStatus: 'APPROVED',
    qaStatus: 'PASS',
    liveHash: hashB,
    storedHash: hashB,
    qaHash: hashA,
    approvedHash: hashA,
    imageType: 'PRODUCTION_STILL',
    artifactOk: true,
  }).ready,
  'artifact modified after vision BLOCK',
);

ok(
  !integrityI2vReady({
    attemptStatus: 'APPROVED',
    qaStatus: undefined,
    liveHash: hashA,
    storedHash: hashA,
    qaHash: '',
    approvedHash: hashA,
    imageType: 'PRODUCTION_STILL',
    artifactOk: true,
  }).ready,
  'QA deleted BLOCK',
);

const sheet = integrityI2vReady({
  attemptStatus: 'APPROVED',
  qaStatus: 'PASS',
  liveHash: hashA,
  storedHash: hashA,
  qaHash: hashA,
  approvedHash: hashA,
  imageType: 'CHARACTER_SHEET',
  artifactOk: true,
});
ok(!sheet.ready && sheet.blocked.some((b) => /CHARACTER_SHEET/.test(b)), 'sheet never I2V ready');

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
const declared = evaluateVisionQa(contract, clean);
ok(declared.status === 'PASS', 'phase 04 declared path still PASS without imageType');
ok(evaluateVisionQa(contract, { ...clean, imageType: 'CHARACTER_SHEET' }).status === 'FAIL', 'declared sheet P0 FAIL');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 05.1 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PHASE 05.1 PASS · integrity + image type + hash gate');
