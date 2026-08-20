import {
  addStoryThread,
  approveEpisodeNarrative,
  inheritStoryMemory,
  needsInheritanceReview,
  openThreads,
} from './content-famixa-story-memory';
import type { FamixaStoryMemory } from './content-famixa-story-memory';

const ep01: FamixaStoryMemory = {
  seriesArc: { premise: 'Parent pressure on Minh', currentBeat: 'CONFLICT' },
  season: 'Season 01',
  seasonArc: { currentBeat: 'CONFLICT' },
  episodeNarrative: {
    episode: 'EP01',
    whatHappened: 'Parental pressure causes Minh to hide something.',
    whatChanged: 'Minh secrecy up; trust toward parents down.',
    characterChanges: 'Minh unresolved emotional conflict.',
    relationshipChanges: 'Minh trusts Nam reduced.',
    unresolvedConflicts: 'Parents do not realize their mistake.',
    secretsHidden: 'Minh hides his test result.',
    approved: false,
  },
  ledger: [],
  characterStates: [
    {
      characterId: 'CHAR-001',
      secrets: 'increased',
      internalConflict: 'unresolved',
      trust: { 'CHAR-002': 'reduced', 'CHAR-003': 'reduced' },
    },
    { characterId: 'CHAR-002', belief: 'pressure remains', arcPosition: 'before realization' },
    { characterId: 'CHAR-003', belief: 'pressure remains', arcPosition: 'before realization' },
  ],
  relationships: [
    { id: 'CHAR-001|CHAR-002', a: 'CHAR-001', b: 'CHAR-002', trust: 'reduced' },
    { id: 'CHAR-001|CHAR-003', a: 'CHAR-001', b: 'CHAR-003', trust: 'reduced' },
    { id: 'CHAR-002|CHAR-003', a: 'CHAR-002', b: 'CHAR-003' },
  ],
  threads: [],
  inheritReviewed: true,
};

const withThread = addStoryThread(ep01, {
  name: 'Minh is hiding something',
  createdEpisode: 'EP01',
  cause: 'parental pressure',
  consequence: 'loss of trust',
});
const locked = approveEpisodeNarrative(withThread, { seriesCode: 'FAMIXA', seriesTitle: '', episode: 'EP01', title: '', premise: '', moral: '', ctaRule: '', shots: [] });
const ep02 = inheritStoryMemory(locked, 'EP02', 'Next');

const fail: string[] = [];
if (openThreads(ep02).length !== 1) fail.push('EP02 lost OPEN thread');
if (ep02.threads[0]?.status !== 'OPEN') fail.push('KIT closed the thread');
if (ep02.threads[0]?.name !== 'Minh is hiding something') fail.push('thread renamed');
if (ep02.characterStates.find((s) => s.characterId === 'CHAR-001')?.trust?.['CHAR-002'] !== 'reduced') {
  fail.push('Minh trust toward Nam reset');
}
if (ep02.characterStates.find((s) => s.characterId === 'CHAR-002')?.arcPosition !== 'before realization') {
  fail.push('Nam auto-realized');
}
if (ep02.inheritReviewed) fail.push('EP02 skipped inheritance review');
if (!needsInheritanceReview({ episode: { seriesCode: 'FAMIXA', seriesTitle: '', episode: 'EP02', title: '', premise: '', moral: '', ctaRule: '', shots: [] }, storyMemory: ep02 })) {
  fail.push('needsInheritanceReview false');
}
if (locked.ledger.length !== 1 || !locked.ledger[0]?.approved) fail.push('EP01 ledger missing');

if (fail.length) {
  console.error('STORY MEMORY FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('STORY MEMORY PASS · EP01 OPEN thread inherited by EP02 · no auto-resolve');
