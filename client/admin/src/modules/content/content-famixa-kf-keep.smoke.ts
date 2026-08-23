import {
  appendSceneShot,
  insertSceneShot,
  insertShortClip,
  mergeClipLists,
  mergeRemotePilot,
  padSceneShots,
  removeSceneShots,
  sceneInsertAnchor,
} from './content-famixa-series';
import { kfPixelsOf } from './content-famixa-kf-store';
import type { FamixaSeriesShot, FamixaShortClip, SeriesPilotState } from './content-famixa-series';

function short(id: string, hook = id): FamixaShortClip {
  return {
    id,
    hook,
    visual: '',
    seconds: 7,
    motionPrompt: 'run',
    motionPromptVi: '',
  };
}

const fail: string[] = [];

const base = {
  roles: [],
  runs: {
    S04: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,aa' },
    S05: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,bb' },
  },
  shorts: [short('S04'), short('S05')],
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP01',
    title: 'T',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [],
  },
} as SeriesPilotState;

const a = insertShortClip(base, { beforeId: 'S04' });
if (a.short.id !== 'S01') fail.push(`first insert id ${a.short.id}`);
if (a.state.shorts.map((s) => s.id).join() !== 'S01,S04,S05') fail.push(`order ${a.state.shorts.map((s) => s.id)}`);
if (!a.state.runs.S04?.keyframeDataUrl) fail.push('S04 KF must stay after insert');

const b = insertShortClip(a.state, { beforeId: 'S04' });
if (b.state.shorts.map((s) => s.id).join() !== 'S01,S02,S04,S05') fail.push(`second ${b.state.shorts.map((s) => s.id)}`);

const merged = mergeClipLists([short('S01'), short('S02')], base.shorts);
if (merged.map((s) => s.id).join() !== 'S01,S02,S04,S05') fail.push(`merge ${merged.map((s) => s.id)}`);

const remote = { ...base, runs: { S04: { status: 'keyframe_ready' as const } } } as SeriesPilotState;
const keep = mergeRemotePilot(remote, base);
if (!kfPixelsOf('S04')) fail.push('mergeRemote must keep local KF');
if (!kfPixelsOf('S05')) fail.push('mergeRemote must keep local-only run KF');
if (keep.runs.S04?.keyframeDataUrl) fail.push('mergeRemote must not put KF dataUrl on graph');

const localLip = {
  ...base,
  runs: {
    ...base.runs,
    S04: {
      status: 'turbo_testing' as const,
      previewUrl: 'https://runway.example/raw.mp4',
      lipsynced: true,
      lipsyncUrl: 'https://fal.example/lip.mp4',
    },
  },
} as SeriesPilotState;
const remNoLip = {
  ...base,
  runs: { S04: { status: 'turbo_testing' as const, previewUrl: 'https://runway.example/raw.mp4' } },
} as SeriesPilotState;
const keptLip = mergeRemotePilot(remNoLip, localLip);
if (!keptLip.runs.S04?.lipsynced || keptLip.runs.S04.lipsyncUrl !== 'https://fal.example/lip.mp4') {
  fail.push('mergeRemote must keep local Fal lipsync when server graph lacks it');
}

function scShot(id: string, shot: string, story = 'có chuyện'): FamixaSeriesShot {
  return {
    id,
    scene: 'SC01',
    sceneId: 'SC01',
    shot,
    clock: '5s',
    seconds: 5,
    story,
    visual: '',
    characters: [],
    characterIds: [],
    location: '',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
  };
}

const scene = {
  ...base,
  episode: {
    ...base.episode!,
    shots: [scShot('SC01-SH03', 'SH03'), scShot('SC01-SH04', 'SH04')],
  },
} as SeriesPilotState;
const i1 = insertSceneShot(scene, { beforeId: sceneInsertAnchor(scene.episode!.shots)!.id });
if (i1.shot.shot !== 'SH01') fail.push(`insert first free ${i1.shot.shot}`);
if (i1.state.episode!.shots.map((s) => s.shot).join() !== 'SH01,SH03,SH04') {
  fail.push(`insert order ${i1.state.episode!.shots.map((s) => s.shot)}`);
}
const i2 = insertSceneShot(i1.state, { beforeId: sceneInsertAnchor(i1.state.episode!.shots)!.id });
if (i2.state.episode!.shots.map((s) => s.shot).join() !== 'SH01,SH02,SH03,SH04') {
  fail.push(`insert 2 ${i2.state.episode!.shots.map((s) => s.shot)}`);
}
const ap = appendSceneShot(scene, 'SC01');
if (ap.shot.shot !== 'SH05') fail.push(`append after max ${ap.shot.shot}`);
if (ap.state.episode!.shots.map((s) => s.shot).join() !== 'SH03,SH04,SH05') {
  fail.push(`append order ${ap.state.episode!.shots.map((s) => s.shot)}`);
}
const gone = removeSceneShots(i2.state, ['SC01-SH04']);
if (gone.episode!.shots.some((s) => s.id === 'SC01-SH04')) fail.push('remove SH04');
if (!gone.episode!.shots.some((s) => s.shot === 'SH01')) fail.push('remove must keep SH01');

const padded = padSceneShots(scene, 'SC01', 6);
if (padded.added.length !== 4) fail.push(`pad added ${padded.added.length}`);
if (padded.state.episode!.shots.filter((s) => s.scene === 'SC01' || s.sceneId === 'SC01').length !== 6) {
  fail.push(`pad count ${padded.state.episode!.shots.length}`);
}
if (padded.state.episode!.shots[0]?.id !== 'SC01-SH03') fail.push('pad must keep first existing shot');

if (fail.length) {
  console.error('SHORT KF KEEP FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('SHORT KF KEEP PASS');
