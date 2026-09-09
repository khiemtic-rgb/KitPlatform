import {
  assetPreflight,
  canUseInProduction,
  canonChangeImpact,
  compileImagePrompt,
  eraAge,
  evaluateKeyframeQa,
  evaluateReferenceQa,
  holdLockedCanon,
  inheritWardrobe,
  KIT_VIDEO_ASSET,
  mutateApprovedSnapshot,
  nextAssetVersion,
  resolveShotPackage,
  sameCharacterIdentity,
  type KitVideoAsset,
  type KitVideoPreviousState,
  type KitVideoSceneMaster,
  type KitVideoShotSpec,
} from './kit-video-asset';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(KIT_VIDEO_ASSET === 'KIT-VIDEO-ASSET-V1', 'engine asset id');

const minh: KitVideoAsset = {
  assetCode: 'CHAR-001',
  assetKind: 'CHARACTER',
  name: 'Minh',
  lifecycle: 'APPROVED',
  version: 'V1',
  era: 'ERA-01',
  canon: {
    identity: { name: 'Minh', gender: 'male', age: 11, era: 'ERA-01' },
    face: { shape: 'round youthful' },
    hair: { style: 'short black slightly messy' },
    body: { proportion: 'child' },
    wardrobe: { defaultId: 'WARDROBE-001' },
  },
  references: [{ kind: 'FRONT', path: '/canon/minh-front.png', isPrimary: true, qaStatus: 'PASS' }],
  eras: [
    { era: 'ERA-01', age: 11, status: 'APPROVED' },
    { era: 'ERA-02', age: 16, status: 'DRAFT' },
    { era: 'ERA-03', age: 23, status: 'DRAFT' },
  ],
};
const linh: KitVideoAsset = {
  assetCode: 'CHAR-003',
  assetKind: 'CHARACTER',
  name: 'Linh',
  lifecycle: 'APPROVED',
  version: 'V1',
  era: 'ERA-01',
  canon: { identity: { name: 'Linh', age: 38, era: 'ERA-01' }, wardrobe: { defaultId: 'WARDROBE-002' } },
  references: [{ kind: 'FRONT', path: '/canon/linh-front.png', isPrimary: true }],
};
const loc: KitVideoAsset = {
  assetCode: 'LOC-001',
  assetKind: 'LOCATION',
  name: 'Minh home living room',
  lifecycle: 'APPROVED',
  version: 'V1',
  era: '',
  canon: {
    layout: 'sofa facing TV, doorway left, window right',
    furniture: ['sofa', 'TV cabinet', 'low table'],
    lighting: 'late afternoon indoor',
  },
  references: [{ kind: 'WIDE', path: '/canon/loc-wide.png', isPrimary: true }],
};
const paper: KitVideoAsset = {
  assetCode: 'PROP-001',
  assetKind: 'PROP',
  name: 'School test paper',
  lifecycle: 'APPROVED',
  version: 'V1',
  era: '',
  canon: { identity: { name: 'School test paper' } },
  references: [],
};
const uniform: KitVideoAsset = {
  assetCode: 'WARDROBE-001',
  assetKind: 'WARDROBE',
  name: 'School uniform',
  lifecycle: 'APPROVED',
  version: 'V1',
  era: '',
  canon: { identity: { name: 'School uniform' } },
  references: [],
};
const catalog = [minh, linh, loc, paper, uniform];

const scene: KitVideoSceneMaster = {
  sceneCode: 'SC01',
  status: 'LOCKED',
  locationCode: 'LOC-001',
  lighting: 'late afternoon indoor',
  presence: [
    { characterCode: 'CHAR-001', position: 'living-room doorway', wardrobe: 'WARDROBE-001', emotion: 'excited' },
    { characterCode: 'CHAR-003', position: 'sofa', wardrobe: 'WARDROBE-002' },
  ],
};

const spec = (shot: string, extra: Partial<KitVideoShotSpec> = {}): KitVideoShotSpec => ({
  shotCode: shot,
  sceneCode: 'SC01',
  era: 'ERA-01',
  requiredCharacters: ['CHAR-001'],
  requiredLocation: 'LOC-001',
  requiredWardrobe: { 'CHAR-001': 'WARDROBE-001' },
  action: 'Minh stands in doorway',
  i2vImageSource: 'APPROVED_KEYFRAME',
  ...extra,
});

const prev: KitVideoPreviousState = {
  shotCode: 'SH01-01',
  approved: true,
  characters: { 'CHAR-001': { wardrobe: 'WARDROBE-001', position: 'doorway', emotion: 'excited' } },
  location: 'LOC-001',
  lighting: 'late afternoon indoor',
  props: [],
};

// TEST 01 — Minh 5 shot cùng identity
const shots = ['SH01-01', 'SH01-02', 'SH01-03', 'SH01-04', 'SH01-05'].map((code) =>
  resolveShotPackage({ catalog, spec: spec(code), sceneMaster: scene, previous: code === 'SH01-01' ? null : prev }),
);
ok(
  shots.every((p) => p.characters[0] && sameCharacterIdentity(p.characters[0], minh) && p.characters[0].canon.identity?.age === 11),
  '01 identity consistent',
);

// TEST 02 — 11 tuổi không thành 16
ok(eraAge(minh, 'ERA-01') === 11, '02 era-01 is 11');
ok(eraAge(minh, 'ERA-02') === 16, '02 era-02 exists as same identity');
const drift = resolveShotPackage({
  catalog,
  spec: spec('SH01-02', { era: 'ERA-02' }),
  sceneMaster: scene,
  previous: prev,
});
ok(drift.blocked.some((b) => b.startsWith('ERA_NOT_READY')), '02 era-02 draft blocked');

// TEST 03 — school uniform không tự đổi
ok(inheritWardrobe(prev, 'CHAR-001') === 'WARDROBE-001', '03 inherit uniform');
ok(inheritWardrobe(prev, 'CHAR-001', undefined) !== 'WARDROBE-002', '03 no silent wardrobe change');

// TEST 04 — living room furniture từ canon
const room = resolveShotPackage({ catalog, spec: spec('SH01-01'), sceneMaster: scene });
ok(room.locations[0]?.canon.furniture?.includes('sofa'), '04 furniture from canon');
ok(room.locations[0]?.canon.layout?.includes('doorway left'), '04 layout not random');

// TEST 05 — Minh + Mẹ = 2
ok(
  evaluateKeyframeQa({
    requiredCharacters: ['CHAR-001', 'CHAR-003'],
    detectedCharacters: ['CHAR-001', 'CHAR-003'],
  }).ok,
  '05 two people pass',
);

// TEST 06 — thiếu test paper
ok(
  !evaluateKeyframeQa({
    requiredCharacters: ['CHAR-001', 'CHAR-003'],
    detectedCharacters: ['CHAR-001', 'CHAR-003'],
    requiredProps: ['PROP-001'],
    detectedProps: [],
  }).ok,
  '06 missing paper fail',
);

// TEST 07 — chỉ có Minh
const onlyMinh = evaluateKeyframeQa({
  requiredCharacters: ['CHAR-001', 'CHAR-003'],
  detectedCharacters: ['CHAR-001'],
});
ok(!onlyMinh.ok && !onlyMinh.allowI2v, '07 missing mother fail + no I2V');

// TEST 08 — Ông nội missing BLOCK, không tự generate
const ong = resolveShotPackage({
  catalog,
  spec: spec('SH01-06', { requiredCharacters: ['CHAR-001', 'Ông nội'] }),
  sceneMaster: scene,
  previous: prev,
});
ok(ong.missing.some((m) => m.name === 'Ông nội'), '08 missing ông');
ok(ong.blocked.some((b) => b.includes('MISSING')), '08 blocked');
ok(!ong.characters.some((c) => c.name === 'Ông nội'), '08 no invented character');
ok(!assetPreflight(ong, spec('SH01-06', { requiredCharacters: ['CHAR-001', 'Ông nội'] })).ok, '08 preflight block');

// TEST 09 — Locked V1 không overwrite
ok(holdLockedCanon('LOCKED'), '09 locked');
ok(!holdLockedCanon('APPROVED'), '09 approved can edit');
ok(nextAssetVersion('V1') === 'V2', '09 new version V2');
ok(canUseInProduction('APPROVED') && canUseInProduction('LOCKED'), '09 production gate');

// TEST 10 — regen SH01-03 không đụng SH01-01/02
const snaps: Record<string, KitVideoPreviousState> = {
  'SH01-01': { ...prev, shotCode: 'SH01-01', approved: true },
  'SH01-02': { ...prev, shotCode: 'SH01-02', approved: true },
  'SH01-03': { ...prev, shotCode: 'SH01-03', approved: true },
};
const after = mutateApprovedSnapshot(snaps, 'SH01-03');
ok(after['SH01-01'].approved && after['SH01-02'].approved, '10 prior snapshots stay');
ok(after['SH01-03'].approved === false, '10 only target unapproved');

const qaRef = evaluateReferenceQa({
  readable: true,
  width: 1024,
  height: 1024,
  faceVisible: true,
  aspectOk: true,
});
ok(qaRef.ok, 'ref qa pass');
ok(!evaluateReferenceQa({ readable: true, faceVisible: false }).ok, 'ref face fail blocks');

const compiled = compileImagePrompt(room, spec('SH01-01', { dialogue: 'Mẹ ơi điểm 10!', speaker: 'CHAR-001', emotion: 'excited' }));
ok(compiled.ok && !compiled.hasDialogue, 'prompt has no dialogue');
ok(compiled.i2vSource === 'APPROVED_KEYFRAME', 'i2v keyframe only');
ok(
  !compileImagePrompt(room, spec('SH01-01', { i2vImageSource: 'CHARACTER_CROP' })).ok,
  'no character crop to I2V',
);

ok(canonChangeImpact(27).warn && canonChangeImpact(27).message.includes('27 Shot'), 'impact warn');

const cropFail = evaluateKeyframeQa({
  requiredCharacters: ['CHAR-001', 'CHAR-003'],
  detectedCharacters: ['CHAR-001', 'CHAR-003'],
  requiredProps: ['PROP-001'],
  detectedProps: ['PROP-001'],
  actionNeeds: ['CHAR-001', 'CHAR-003', 'PROP-001', 'interaction'],
  visible: { halfFace: true },
});
ok(!cropFail.ok, 'composition half-face fail');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 02 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PHASE 02 PASS · tests 01–10 · generic asset');
