import { parseFamixaCanon } from './content-famixa-character-memory';
import { canGenerateMasterRef, canLockCharacter } from './content-famixa-character-create';
import {
  CHAR_001_MINH_VISUAL_DNA_V1,
  dnaApproved,
  dnaReadyForMaster,
  isMinhVisualDna,
  mergeMinhVisualDnaDraft,
  minhDnaPromptOverride,
  MINH_DNA_AGE,
  MINH_DNA_ERA,
  MINH_DNA_ID,
} from './content-famixa-minh-visual-dna';

const fail: string[] = [];

if (!isMinhVisualDna(CHAR_001_MINH_VISUAL_DNA_V1)) fail.push('dna id CHAR-001');
if (CHAR_001_MINH_VISUAL_DNA_V1.status !== 'DRAFT') fail.push('DNA seed stays DRAFT');
if (CHAR_001_MINH_VISUAL_DNA_V1.era !== MINH_DNA_ERA || CHAR_001_MINH_VISUAL_DNA_V1.age !== MINH_DNA_AGE) {
  fail.push('ERA-01 age 11');
}
if (!CHAR_001_MINH_VISUAL_DNA_V1.immutableTraits.some((x) => /ERA-02|ERA-03|cùng một|identity/i.test(x))) {
  fail.push('same identity across eras');
}
if (!CHAR_001_MINH_VISUAL_DNA_V1.forbidden.some((x) => /take-01|SH01-01|Golden/i.test(x))) {
  fail.push('must not use Golden / take-01 as face lock');
}
if (!/Stylized Cinematic/i.test(CHAR_001_MINH_VISUAL_DNA_V1.identityPrinciple)) fail.push('stylized cinematic band');
if (/hot boy|photoreal|anime|cartoon/i.test(CHAR_001_MINH_VISUAL_DNA_V1.whoVisually) === false) fail.push('core rejects hot-boy/photoreal/anime');
if (CHAR_001_MINH_VISUAL_DNA_V1.recognitionTests.length < 6) fail.push('recognition tests A–F');
if (CHAR_001_MINH_VISUAL_DNA_V1.generationPriority[0] !== 'FAMIXA VISUAL STYLE SYSTEM V1') fail.push('generation priority starts at style system');
if (minhDnaPromptOverride('Give Minh blonde idol hair').ok) fail.push('prompt cannot override hair Canon');
if (minhDnaPromptOverride('Subtle reserved posture, quiet eyes').ok !== true) fail.push('on-DNA prompt allowed');
if (dnaReadyForMaster(CHAR_001_MINH_VISUAL_DNA_V1) || dnaApproved(CHAR_001_MINH_VISUAL_DNA_V1)) {
  fail.push('AI must not auto-approve DNA');
}

const seed = parseFamixaCanon({
  identity: { characterId: 'CHAR-001', name: 'Minh', currentAge: 11, currentEra: 'A11' },
  visualDna: { face: 'old sheet face', hair: 'old hair' },
  references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-001-minh-master.png' }],
  voiceDna: { voiceId: 'keep-me' },
});
const merged = parseFamixaCanon(mergeMinhVisualDnaDraft({ ...seed, identity: seed.identity, visualDna: seed.visualDna, references: seed.references, voiceDna: seed.voiceDna }));
if (merged.visualDna?.documentId !== MINH_DNA_ID) fail.push('merge attaches DNA V1');
const legacyApproved = parseFamixaCanon(
  mergeMinhVisualDnaDraft({
    identity: { characterId: 'CHAR-001', name: 'Minh' },
    visualDna: { documentId: 'FAMIXA-CHAR-001-VISUAL-DNA-V1', status: 'APPROVED', characterId: 'CHAR-001', hairLock: 'left tuft' },
  }),
);
if (legacyApproved.visualDna?.documentId !== MINH_DNA_ID) fail.push('legacy DNA must upgrade to new id');
if (legacyApproved.visualDna?.status !== 'DRAFT') fail.push('legacy APPROVED must not count as new DNA APPROVED');
if (merged.identity.currentEra !== MINH_DNA_ERA) fail.push('merge sets ERA-01');
if (merged.references?.[0]?.path !== seed.references?.[0]?.path) fail.push('merge must not rewrite master path');
if ((merged.voiceDna as { voiceId?: string } | undefined)?.voiceId !== 'keep-me') fail.push('merge must not rewrite Voice');
if (merged.visualDna?.status === 'APPROVED' || merged.visualDna?.status === 'LOCKED') fail.push('merge must not LOCK/APPROVE');

const directed = parseFamixaCanon(
  mergeMinhVisualDnaDraft({
    identity: merged.identity,
    visualDna: { ...merged.visualDna, status: 'REVIEW', hairLock: 'director tuft' },
    famixaVisualStyle: merged.famixaVisualStyle,
    references: merged.references,
    voiceDna: merged.voiceDna,
  }),
);
if (directed.visualDna?.hairLock !== 'director tuft') fail.push('merge must keep Director REVIEW edits');

const stub = parseFamixaCanon(
  mergeMinhVisualDnaDraft({
    identity: { characterId: 'CHAR-001', name: 'Minh' },
    visualDna: { documentId: MINH_DNA_ID, status: 'DRAFT', characterId: 'CHAR-001', whoVisually: '' },
  }),
);
if (!stub.visualDna?.whoVisually || !/11 tuổi|11-year/i.test(stub.visualDna.whoVisually)) {
  fail.push('DRAFT stub must fill Character Core from spec');
}
if (stub.visualDna?.status !== 'DRAFT') fail.push('fill must stay DRAFT');

const row = {
  id: '1',
  characterCode: 'CHAR-001',
  name: 'Minh',
  role: 'Con',
  universe: 'CORE',
  visual: 'frame',
  lifecycle: 'approved' as const,
  currentEra: 'ERA-01',
  version: 'V1',
  isCurrentCanon: true,
  canon: merged,
  references: merged.references ?? [],
  updatedAt: '',
};
if (canLockCharacter(row).ok) fail.push('DRAFT DNA must not LOCK');
if (canGenerateMasterRef(row).ok) fail.push('DRAFT DNA must not generate Master');

if (fail.length) {
  console.error('MINH VISUAL DNA FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('MINH VISUAL DNA PASS · ERA-01 V1 draft · no auto-LOCK · no Golden face lock');
