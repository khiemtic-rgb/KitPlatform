import { compileNarrativeStillPrompt } from './content-famixa-kf-pipeline';
import { deriveVisualSpec } from './content-famixa-visual-spec';
import {
  CHARACTER_PROMPT_COMPILER,
  characterAuditOf,
  characterProductionGuard,
  compileCharacterPromptSubset,
  FAMIXA_VISUAL_STYLE,
  parseFamixaCanon,
  resolveFamixaCharacters,
  type FamixaCharacterRecord,
} from './content-famixa-character-memory';

const fail: string[] = [];

const minhCanon = parseFamixaCanon({
  identity: { characterId: 'CHAR-001', name: 'Minh', role: 'Con', currentAge: 11, currentEra: 'A11' },
  visualDna: { face: 'round youthful face', hair: 'short black slightly messy hair', build: 'slim child body' },
  personalityDna: ['sensitive'],
  wardrobe: [{ id: 'OUTFIT-HOME-01', set: 'HOME', label: 'Áo nhà tối' }],
  famixaVisualStyle: { summary: FAMIXA_VISUAL_STYLE.summary },
  references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-001-minh-master.png' }],
});
if (minhCanon.identity.characterId !== 'CHAR-001') fail.push('TEST 01 parse Minh');
if (!minhCanon.references?.some((r) => r.kind === 'FRONT')) fail.push('TEST 01 Minh FRONT');

const minh: FamixaCharacterRecord = {
  id: '1',
  characterCode: 'CHAR-001',
  name: 'Minh',
  role: 'Con',
  universe: 'CORE',
  visual: 'frame',
  lifecycle: 'locked',
  currentEra: 'A11',
  version: 'V1',
  isCurrentCanon: true,
  canon: minhCanon,
  references: minhCanon.references ?? [],
  updatedAt: '',
};
const linh: FamixaCharacterRecord = {
  ...minh,
  id: '3',
  characterCode: 'CHAR-003',
  name: 'Linh',
  role: 'Mẹ',
  canon: parseFamixaCanon({
    identity: { characterId: 'CHAR-003', name: 'Linh', currentEra: 'A11' },
    visualDna: { face: 'adult Vietnamese mother face' },
    wardrobe: [{ id: 'OUTFIT-HOME-01', set: 'HOME' }],
    references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-003-linh-master.png' }],
  }),
  references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-003-linh-master.png' }],
};

const ep01 = resolveFamixaCharacters([minh, linh], ['CHAR-001']);
const ep99 = resolveFamixaCharacters([minh, linh], ['CHAR-001']);
if (ep01[0]?.characterCode !== 'CHAR-001' || ep99[0]?.version !== ep01[0]?.version) {
  fail.push('TEST 03 same CHAR-001 current canon across builds');
}

const locked = characterProductionGuard({ registry: [minh], characterIds: ['CHAR-001'] });
if (!locked.ok) fail.push(`TEST 07 locked must pass guard: ${locked.blocked.join('; ')}`);
if (locked.resolved[0]?.lifecycle !== 'locked') fail.push('TEST 07 lifecycle stays locked');

const approvedOnly: FamixaCharacterRecord = { ...minh, lifecycle: 'approved' };
const approvedBlock = characterProductionGuard({ registry: [approvedOnly], characterIds: ['CHAR-001'] });
if (approvedBlock.ok || !approvedBlock.blocked.some((b) => /LOCK/i.test(b))) {
  fail.push('TEST 07b approved frame must BLOCK production until LOCK');
}

const unknown = characterProductionGuard({ registry: [minh], characterIds: ['CHAR-008'] });
if (unknown.ok || !unknown.blocked.some((b) => /REQUEST CREATION/i.test(b))) {
  fail.push('TEST 08 unknown CHAR must BLOCK');
}

const noFront: FamixaCharacterRecord = {
  ...minh,
  references: [],
  canon: { ...minhCanon, references: [] },
};
const missing = characterProductionGuard({ registry: [noFront], characterIds: ['CHAR-001'] });
if (missing.ok || !missing.blocked.some((b) => /FRONT/i.test(b))) fail.push('TEST 09 missing FRONT must BLOCK');

const subset = compileCharacterPromptSubset(minhCanon);
if (!/CHAR-001|Minh|A11|OUTFIT-HOME-01/i.test(subset)) fail.push('subset must carry identity + era + outfit');
if (/buồn|hurt|looking down|đứng trong phòng/i.test(subset)) fail.push('TEST 04/05 subset must not carry emotion/action');
if (!subset.includes(FAMIXA_VISUAL_STYLE.summary.slice(0, 24))) fail.push('subset must include Famixa visual style');

const spec = deriveVisualSpec({
  shotId: 'SH01-01',
  action: 'Minh đứng ở cửa cầm tờ giấy.',
  spoken: 'Mẹ xem giúp con tờ này.',
  names: ['Minh'],
  ids: ['CHAR-001'],
  speakers: ['Minh'],
  location: 'Phòng khách',
});
const prompt = compileNarrativeStillPrompt({ spec, aspect: '16:9', characterSubset: subset });
if (!/CHAR-001|Minh/i.test(prompt)) fail.push('compiler must keep identity');
if ((prompt.match(/sensitive|needs recognition/g) ?? []).length > 0 && /PRIORITY 2/.test(prompt) === false) {
  /* personality may appear only if dumped — subset must not include it */
}
if (/sensitive|needs recognition/.test(subset)) fail.push('TEST 04 personality must not enter subset');

const audit = characterAuditOf(locked);
if (audit.compilerVersion !== CHARACTER_PROMPT_COMPILER) fail.push('audit compiler version');
if (!audit.characterCodes.includes('CHAR-001')) fail.push('audit CHAR-001');
if (audit.era !== 'A11' || audit.version !== 'V1') fail.push('audit era/version');

if (fail.length) {
  console.error('CHARACTER GUARD FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('CHARACTER GUARD PASS · TEST 01/03/07/08/09 + subset 04/05');
