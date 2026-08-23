import {
  FAMIXA_CANON_VERSION,
  frameCanonIds,
  isOffFrameCanon,
  resolveCanonSpeaker,
  seedFamixaCanon,
} from './content-famixa-char-canon';
import { applyFamixaCanonToPilot, ensurePilotGraph, type SeriesPilotState } from './content-famixa-series';
import { parseEpisodeStory } from './content-famixa-story-parse';

const fail: string[] = [];

if (resolveCanonSpeaker('Tone') || resolveCanonSpeaker('Thời lượng')) {
  fail.push('meta heading must not become a CHAR');
}
if (resolveCanonSpeaker('An')?.id !== 'CHAR-004' || !isOffFrameCanon('CHAR-004', 'An')) {
  fail.push('An is CHAR-004 off-frame');
}
if (!resolveCanonSpeaker('Minh') || resolveCanonSpeaker('Minh')?.visual !== 'frame') {
  fail.push('Minh must stay in frame');
}
if (seedFamixaCanon([]).some((c) => c.id === 'CHAR-004')) {
  fail.push('empty seed must not add An');
}
if (seedFamixaCanon([{ id: 'CHAR-004', name: 'An' }]).find((c) => c.id === 'CHAR-004')?.offFrame !== true) {
  fail.push('named An stays mention-only');
}
if (frameCanonIds(['CHAR-001', 'CHAR-003', 'CHAR-004', 'CHAR-005']).join() !== 'CHAR-001,CHAR-003') {
  fail.push('frame ids must drop An and extras');
}

const dirty: SeriesPilotState = {
  roles: [
    { id: 'role-tone', title: 'Tone', name: 'Tone', characterId: 'CHAR-TONE' },
    { id: 'role-an', title: 'Bạn', name: 'An', characterId: 'CHAR-004' },
    { id: 'role-minh', title: 'Con', name: 'Minh', characterId: 'CHAR-001' },
  ],
  runs: {},
  characters: [
    { id: 'CHAR-001', name: 'Minh' },
    { id: 'CHAR-003', name: 'Linh' },
    { id: 'CHAR-004', name: 'An' },
    { id: 'CHAR-005', name: 'Hoàng' },
  ],
  episode: {
    seriesCode: 'FAMIXA',
    episode: 'EP01',
    title: 'Test',
    shots: [
      {
        id: 'EP01-SC01-SH01',
        scene: 'SC01',
        shot: 'SH01',
        clock: '5s',
        seconds: 5,
        story: 'Minh đưa bài cho mẹ. Bạn An được nhắc.',
        visual: '',
        characters: ['CHAR-001', 'CHAR-003', 'CHAR-004'],
        characterIds: ['CHAR-001', 'CHAR-003', 'CHAR-004'],
        location: 'Phòng ăn',
        motionPrompt: '',
        motionPromptVi: '',
        status: 'story_locked',
      },
    ],
  },
};

const healed = applyFamixaCanonToPilot(dirty);
if (healed.canonVersion !== FAMIXA_CANON_VERSION) fail.push('canonVersion must stamp');
if (healed.roles.some((r) => /tone|an/i.test(`${r.name} ${r.characterId}`))) {
  fail.push(`healed roles still have extras: ${healed.roles.map((r) => r.name).join()}`);
}
if (!healed.roles.some((r) => r.characterId === 'CHAR-001')) fail.push('Minh role must stay');
const shotIds = healed.episode?.shots?.[0]?.characterIds ?? [];
if (shotIds.includes('CHAR-004') || shotIds.includes('CHAR-005')) {
  fail.push(`shot still has off-frame body: ${shotIds.join()}`);
}

const loaded = ensurePilotGraph(dirty);
if (loaded.canonVersion !== FAMIXA_CANON_VERSION) fail.push('ensurePilotGraph must apply canon');
if ((loaded.episode?.shots?.[0]?.characterIds ?? []).includes('CHAR-004')) {
  fail.push('load must strip An from shot');
}

const nextPack = parseEpisodeStory(`SC01 — BỮA CƠM
Tone: Tàn nhẫn, thực dụng.
CHAR-004 — An | Bạn
CHAR-005 — Hoàng | Bạn
Minh chạy ùa từ cửa vào.
MINH
Mẹ! Bạn An được chín điểm!
LINH
Bạn An chín rưỡi.
`);
if (!nextPack) fail.push('next pack failed to parse');
else {
  if (nextPack.characters.some((c) => c.id === 'CHAR-005' || /hoàng|tone/i.test(c.name))) {
    fail.push(`new pack invented extra CHAR: ${nextPack.characters.map((c) => c.id + c.name).join()}`);
  }
  if (nextPack.shots.some((s) => (s.characterIds ?? []).includes('CHAR-004'))) {
    fail.push('new pack must not put An on shot characterIds');
  }
  if (nextPack.characters.some((c) => c.id === 'CHAR-004') && !nextPack.characters.find((c) => c.id === 'CHAR-004')?.offFrame) {
    fail.push('An if present must be offFrame');
  }
  const visual = nextPack.characters.filter((c) => !c.offFrame && c.id !== 'CHAR-VO').map((c) => c.id);
  if (visual.includes('CHAR-004')) fail.push('An must not be a visual role');
}

if (fail.length) {
  console.error('CHAR CANON FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(`CHAR CANON PASS · v${FAMIXA_CANON_VERSION} · heal + next pack`);
