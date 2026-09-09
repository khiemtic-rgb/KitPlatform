import { compileCharacterPromptSubset, parseFamixaCanon } from './content-famixa-character-memory';
import {
  briefDoesNotLockVisual,
  CHAR_001_MINH_BRIEF,
  isMinhBrief,
  mergeMinhBriefIntoCanon,
  MINH_BRIEF_ID,
  MINH_BRIEF_NEXT,
  MINH_CHARACTER_STATEMENT,
  MINH_EMOTIONAL_DNA,
  MINH_PLANNED_FAMILY,
} from './content-famixa-character-brief';

const fail: string[] = [];

if (!isMinhBrief(CHAR_001_MINH_BRIEF)) fail.push('brief id CHAR-001');
if (CHAR_001_MINH_BRIEF.status !== 'BRIEF' || CHAR_001_MINH_BRIEF.readyFor !== 'VISUAL_DESIGN') {
  fail.push('status must stay BRIEF — not locked Canon');
}
if (!briefDoesNotLockVisual(CHAR_001_MINH_BRIEF)) fail.push('Brief must not lock face/hair/voice/master');
if (CHAR_001_MINH_BRIEF.nextStep !== MINH_BRIEF_NEXT) fail.push('next step is Visual DNA Spec');
if (!CHAR_001_MINH_BRIEF.statement.includes('wanting to be trusted')) fail.push('statement trust');
if (CHAR_001_MINH_BRIEF.oneLine !== MINH_EMOTIONAL_DNA) fail.push('one-line emotional DNA');
if (CHAR_001_MINH_BRIEF.notA.some((x) => /always pitiful|gifted/i.test(x)) === false) {
  fail.push('not weak / not gifted constraints');
}

const seed = parseFamixaCanon({
  identity: { characterId: 'CHAR-001', name: 'Minh', role: 'Con', currentAge: 11, currentEra: 'A11' },
  visualDna: { face: 'round youthful face', hair: 'short black slightly messy hair', build: 'slim child body' },
  voiceDna: { voiceId: '' },
  wardrobe: [{ id: 'OUTFIT-HOME-01', set: 'HOME', label: 'Áo nhà tối' }],
  references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-001-minh-master.png' }],
});
const merged = parseFamixaCanon(mergeMinhBriefIntoCanon({ ...seed, identity: seed.identity }));
if (merged.brief?.documentId !== MINH_BRIEF_ID) fail.push('merge attaches brief');
if (merged.identity.role !== 'Main Child') fail.push('role Main Child');
if (merged.visualDna?.face !== seed.visualDna?.face) fail.push('merge must not rewrite face');
if (merged.references?.[0]?.path !== seed.references?.[0]?.path) fail.push('merge must not rewrite master path');
if ((merged.voiceDna?.voiceId ?? '') !== '') fail.push('merge must not invent Voice ID');
if (merged.relationships?.some((r) => r.to === 'CHAR-004' && r.type === 'sister')) {
  fail.push('An must not become sister');
}
if (merged.relationships?.some((r) => r.to === 'CHAR-003' && r.type === 'mother') !== true) {
  fail.push('mother stays CHAR-003');
}
if (MINH_PLANNED_FAMILY.some((f) => f.characterCode)) fail.push('Ông/Bà/Em gái must have no Character ID');
if (merged.evolution?.eras.some((e) => e.era === 'A16') !== true) fail.push('A16 is CHAR-001 era');
if (merged.personalityDna?.includes('curious') !== true) fail.push('core curious');

const subset = compileCharacterPromptSubset(merged, 'OUTFIT-HOME-01');
if (subset.includes(MINH_EMOTIONAL_DNA) || subset.includes(MINH_CHARACTER_STATEMENT.slice(0, 40))) {
  fail.push('Brief / emotional DNA must not enter image prompt subset');
}
if (/đáng thương|always sad|cry|pitiful/i.test(subset)) fail.push('subset must not play Minh as pitiful');
if (!/CHAR-001|Minh|A11/i.test(subset)) fail.push('subset still carries identity');

if (fail.length) {
  console.error('MINH BRIEF FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('MINH BRIEF PASS · FAMIXA-CHAR-001-BRIEF-V1 · not locked · no invent family IDs');
