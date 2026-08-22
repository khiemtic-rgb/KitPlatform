import { parseEpisodeStory } from './content-famixa-story-parse';
import { groupShotsByBeat, pruneEmptyShots, shotHasValidAction } from './content-famixa-series';
import type { SeriesPilotState } from './content-famixa-series';

const six =
  `VIDEO TITLE: TỪ 5 LÊN 8\nEPISODE 01\nCHAR-001 Minh\n\n` +
  `SC01 — TỪ 5 LÊN 8\n\n` +
  `Minh bước ra khỏi cổng trường.\n` +
  `Minh nhìn bài kiểm tra.\n` +
  `Minh nhận ra mình được 8 điểm.\n` +
  `Minh mở điện thoại.\n` +
  `Minh gõ tin nhắn cho mẹ rồi xóa.\n` +
  `Minh cất điện thoại và nói "Về khoe mới được."\n`;

const split =
  `VIDEO TITLE: Cổng\nEPISODE 01\nCHAR-001 Minh\n\n` +
  `SC01 — CỔNG\n\n` +
  `Minh bước ra khỏi cổng trường, nhìn bài kiểm tra, cận cảnh điểm 8.\n`;

const fail: string[] = [];
const doc = parseEpisodeStory(six);
if (!doc) fail.push('six-beat script failed to parse');
else {
  if (doc.shots.length !== 6) fail.push(`expected 6 shots from 6 beats, got ${doc.shots.length}: ${doc.shots.map((s) => s.shot).join(',')}`);
  if (new Set(doc.shots.map((s) => s.beatId)).size !== 6) fail.push('each beat should be one shot');
  if (doc.shots.some((s) => !shotHasValidAction(s))) fail.push('empty production shot');
  if (doc.shots.some((s) => s.inheritFromShotId)) fail.push('auto inheritFromShotId');
}

const cut = parseEpisodeStory(split);
if (!cut) fail.push('split-beat script failed to parse');
else {
  const beats = groupShotsByBeat(cut.shots);
  if (beats.length !== 1) fail.push(`one action beat should stay one beat, got ${beats.length}`);
  if (cut.shots.length < 2 || cut.shots.length > 3) {
    fail.push(`camera split of one beat should be 2–3 shots, got ${cut.shots.length}`);
  }
  if (new Set(cut.shots.map((s) => s.beatId)).size !== 1) fail.push('split shots must share beatId');
  if (cut.shots.some((s) => !shotHasValidAction(s))) fail.push('split invented empty SH');
}

const padded: SeriesPilotState = {
  roles: [],
  stills: [],
  shorts: [],
  runs: {},
  schemaVersion: 9,
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: '',
    episode: 'EP01',
    title: '',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [
      {
        id: 'EP01-SC01-SH01',
        scene: 'SC01',
        sceneId: 'SC01',
        shot: 'SH01',
        clock: '5s',
        seconds: 5,
        story: 'Minh bước ra khỏi cổng trường.',
        visual: '',
        characters: [],
        location: '',
        motionPrompt: '',
        motionPromptVi: '',
        status: 'story_locked',
        beatId: 'SC01-BEAT01',
        beatText: 'Minh bước ra khỏi cổng trường.',
      },
      {
        id: 'EP01-SC01-SH07',
        scene: 'SC01',
        sceneId: 'SC01',
        shot: 'SH07',
        clock: '5s',
        seconds: 5,
        story: '',
        visual: '',
        characters: [],
        location: '',
        motionPrompt: '',
        motionPromptVi: '',
        status: 'story_locked',
      },
    ],
  },
};
const locked = pruneEmptyShots(padded);
if (locked.episode?.shots.length !== 1) fail.push('prune must drop empty SH07');
if (locked.shotGraphLocked !== true) fail.push('prune must lock shot graph');

if (fail.length) {
  console.error('SHOT GRAPH FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('SHOT GRAPH OK', doc?.shots.length, 'from six beats;', cut?.shots.length, 'from one split beat');
