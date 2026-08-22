import { applyDialogueMap, coverageOf, proposeDialogueMap } from './content-famixa-dialogue-map';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '5s',
    seconds: 5,
    story: 'Minh nhìn bài.',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'bếp',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    ...partial,
  };
}

const spoken = [
  shot({ id: 'SH01', story: 'Minh gọi mẹ. Mẹ xem này.' }),
  shot({ id: 'SH02', story: 'Câm — đứng nhìn.', characters: ['CHAR-001'], characterIds: ['CHAR-001'] }),
  shot({ id: 'SH03', story: 'Linh hỏi.', characters: ['CHAR-003'], characterIds: ['CHAR-003'] }),
  shot({ id: 'SH04', story: 'Minh trả lời tám.' }),
  shot({ id: 'SH05', story: 'Câm — Nam bước vào.', characters: ['CHAR-002'], characterIds: ['CHAR-002'] }),
];

const state = {
  roles: [],
  runs: Object.fromEntries(
    spoken.map((s) => [s.id, { status: 'turbo_testing' as const, keyframeDataUrl: 'x', previewUrl: 'https://x/a.mp4' }]),
  ),
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
      dialogue: [
        { id: 'D1', characterId: 'CHAR-001', text: 'Mẹ xem này.' },
        { id: 'D2', characterId: 'CHAR-003', text: 'Tám điểm sao?' },
        { id: 'D3', characterId: 'CHAR-001', text: 'Con được tám điểm.' },
        { id: 'D4', characterId: 'CHAR-001', text: 'Câu chưa gắn short nào.' },
      ],
    },
  ],
  episode: { seriesCode: 'F', seriesTitle: 'F', episode: 'EP01', title: 'T', premise: '', moral: '', ctaRule: '', shots: spoken },
} as SeriesPilotState;

const fail: string[] = [];
const { byShot, extraIds } = proposeDialogueMap(state);
if ((byShot.get('SH02') ?? []).length) fail.push('silent SH02 must be NONE');
if ((byShot.get('SH05') ?? []).length) fail.push('silent SH05 must be NONE');
if (!byShot.get('SH01')?.includes('D1')) fail.push('D1 fits SH01 action');
if (extraIds.includes('D4') !== true) fail.push('unmapped D4 must stay extra');
if ((byShot.get('SH01') ?? []).includes('D4')) fail.push('must not dump leftover onto SH01');

const locked = applyDialogueMap(state);
if (!Array.isArray(locked.episode?.shots.find((s) => s.id === 'SH02')?.dialogueSegmentIds)) {
  fail.push('lock writes NONE as empty array');
}

const voices = new Set(['D1', 'D2', 'D3']);
const cov = coverageOf(locked, spoken, { hasVoiceFile: (id) => voices.has(id) });
if (!cov.assembleBlocked) fail.push('unmapped D4 must block assemble');
if (cov.spoken !== 3) fail.push(`spoken ${cov.spoken}`);
if (cov.silent !== 2) fail.push(`silent ${cov.silent}`);
if (cov.voiceReady !== 3) fail.push(`voice ready ${cov.voiceReady}`);

const shortVoice = coverageOf(locked, spoken, { hasVoiceFile: (id) => id === 'D1' || id === 'D2' });
if (shortVoice.voiceMissing.length !== 1) fail.push('one missing voice must block');
if (!shortVoice.assembleBlocked) fail.push('18/20 style missing voice blocks');

const longState = {
  ...state,
  scenes: [
    {
      id: 'SC01',
      characterIds: ['CHAR-001'],
      dialogue: [
        { id: 'L1', characterId: 'CHAR-001', text: 'Một câu khá dài để chiếm khoảng bốn giây thoại khi ước lượng.' },
        { id: 'L2', characterId: 'CHAR-001', text: 'Câu thứ hai cũng dài tương tự để vượt mười giây khi cộng lại với câu trước.' },
        { id: 'L3', characterId: 'CHAR-001', text: 'Câu thứ ba tiếp nhịp, không bịa beat mới, chỉ nối Short cùng hành động.' },
      ],
    },
  ],
  episode: {
    ...state.episode!,
    shots: [
      shot({
        id: 'SH01',
        story: 'Một câu khá dài để chiếm khoảng bốn giây thoại khi ước lượng. Câu thứ hai cũng dài tương tự để vượt mười giây khi cộng lại với câu trước. Câu thứ ba tiếp nhịp, không bịa beat mới, chỉ nối Short cùng hành động.',
      }),
    ],
  },
  runs: { SH01: { status: 'story_locked' as const } },
} as SeriesPilotState;
const chained = applyDialogueMap(longState);
const chainShots = chained.episode?.shots ?? [];
if (chainShots.length < 2) fail.push(`overflow must insert continuation, got ${chainShots.length}`);
if (chainShots[0]?.seconds !== 10) fail.push('spoken host must be 10s');
if (!chainShots.slice(1).every((s) => s.voiceChainFrom === 'SH01' && s.story === chainShots[0]?.story)) {
  fail.push('continuation must copy Action/KF host, not invent beat');
}
const allIds = chainShots.flatMap((s) => s.dialogueSegmentIds ?? []);
if (!['L1', 'L2', 'L3'].every((id) => allIds.includes(id))) fail.push('must keep every spoken line');
if (allIds.length !== 3) fail.push('must not duplicate lines');
const again = applyDialogueMap(chained);
if ((again.episode?.shots.length ?? 0) !== chainShots.length) fail.push('chain must be idempotent');

if (fail.length) {
  console.error('DIALOGUE MAP FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log(`DIALOGUE MAP PASS · spoken ${cov.spoken} · silent ${cov.silent} · extra ${cov.extraUnmapped.length}`);
