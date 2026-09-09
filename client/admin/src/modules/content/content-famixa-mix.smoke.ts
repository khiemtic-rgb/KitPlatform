import { buildAssembleTimeline } from './content-famixa-assemble';
import { mapPreviewCut } from './content-famixa-preview-cut';
import {
  assembleMixPayload,
  compileMixCueSheet,
  formatMixConfirm,
  MIX_MUSIC_ID,
  MIX_ROOM_ID,
  normalizeMixPrefs,
} from './content-famixa-mix';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '5s',
    seconds: 5,
    story: '',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'bàn ăn',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    ...partial,
  };
}

const shots = [
  shot({ id: 'SH01', story: 'Linh ngồi vuốt điện thoại.' }),
  shot({ id: 'SH02', story: 'Minh bước vào cửa.' }),
  shot({
    id: 'SH03',
    story: 'Liếc giấy bài kiểm trên bàn.',
    characterIds: [],
    characters: [],
  }),
  shot({ id: 'SH04', story: 'Minh đông cứng, nụ cười gượng, không ngẩng.' }),
];

const state = {
  roles: [],
  runs: {
    SH01: { status: 'approved', previewUrl: 'https://r.example/a.mp4', shotAction: 'vuốt điện thoại' },
    SH02: { status: 'approved', previewUrl: 'https://r.example/b.mp4', shotAction: 'bước vào' },
    SH03: {
      status: 'approved',
      previewUrl: 'https://r.example/c.mp4',
      visualSpec: { framing: 'INSERT', shotAction: 'liếc giấy', shotId: 'SH03' },
    },
    SH04: { status: 'approved', keyframeDataUrl: 'data:image/png;base64,aa', shotAction: 'đông cứng' },
  },
  episode: { episode: 'EP01', title: 'tap', shots },
} as unknown as SeriesPilotState;

const plan = mapPreviewCut(state, shots, { hasVoiceFile: () => false });
const tl = buildAssembleTimeline(plan, { fit: 'take' });

const fail: string[] = [];

const def = normalizeMixPrefs(undefined);
if (!def.room || !def.foley || !def.loudnorm || def.music) fail.push('default = phòng + Foley + −14, nhạc trống');

const sheet = compileMixCueSheet(tl, shots, state);
if (!sheet.room || sheet.roomId !== MIX_ROOM_ID) fail.push('room.night.dining');
if (sheet.music || sheet.musicId) fail.push('music stays off until ticked');
if (!sheet.loudnorm) fail.push('loudnorm on');
const ids = sheet.sfx.map((s) => s.assetId);
if (!ids.includes('phone-tap')) fail.push(`phone Foley: ${ids.join(',')}`);
if (!ids.includes('footstep') && !ids.includes('door')) fail.push(`enter Foley: ${ids.join(',')}`);
if (!ids.includes('paper')) fail.push(`INSERT paper: ${ids.join(',')}`);
if (!ids.includes('breath')) fail.push(`silent CU breath: ${ids.join(',')}`);
if (sheet.sfx.filter((s) => s.assetId === 'paper').some((s) => s.startSec < (tl.clips[2]?.startSec ?? 0))) {
  fail.push('paper must sit on INSERT clip');
}

const dry = compileMixCueSheet(tl, shots, state, { room: false, foley: false, music: false, loudnorm: false });
if (dry.room || dry.music || dry.loudnorm || dry.sfx.length) fail.push('all-off sheet must be empty');

const bed = compileMixCueSheet(tl, shots, state, { music: true });
if (!bed.music || bed.musicId !== MIX_MUSIC_ID) fail.push('Phase 3 bed id');
const copy = formatMixConfirm(bed);
if (!/Phòng: room\.night\.dining/i.test(copy) || !/duck/i.test(copy) || !/−14|−14/.test(copy)) {
  fail.push(`confirm mix: ${copy}`);
}
if (!/Nhạc: trống/.test(formatMixConfirm(sheet))) fail.push('default confirm says nhạc trống');

const payload = assembleMixPayload(sheet);
if (payload.music || payload.musicId) fail.push('payload omits bed when off');
if (!payload.room || payload.roomId !== MIX_ROOM_ID) fail.push('payload room id');

if (fail.length) {
  console.error('MIX FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(`MIX PASS · ${sheet.sfx.length} Foley · room ${sheet.roomId} · music off`);
