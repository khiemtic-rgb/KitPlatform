import {
  appendSceneShot,
  bindPictureHashOnRuns,
  emptyPilot,
  insertSceneShot,
  insertShortClip,
  mergeClipLists,
  mergeRemotePilot,
  parseFamixaPack,
  replaceStoryFromParse,
  withRunPixels,
  padSceneShots,
  removeSceneShots,
  sceneInsertAnchor,
  shotRunOf,
} from './content-famixa-series';
import { kfPixelsOf, rememberKfPixels } from './content-famixa-kf-store';
import { stillOf } from './content-famixa-shot-catalog';
import { VISUAL_MODE_3D, VISUAL_MODE_PHOTOREAL } from './kit-video-visual-mode';
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
const restored = withRunPixels(keep);
if (!restored.runs.S04?.keyframeDataUrl) fail.push('withRunPixels must restore KF from mem');

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
const localWan = {
  ...base,
  runs: {
    'SH-WAN-01': {
      status: 'turbo_testing' as const,
      takeUrl: 'https://old.example/take.mp4',
      kfSourceHash: 'still-now',
      runwayAttempts: [{ n: 40, at: '', status: 'SUCCEEDED', taskId: 'wan_abc', outputUrl: 'https://fal.example/wan.mp4', kf: { hash: 'still-now' } }],
    },
  },
} as SeriesPilotState;
const remOldTake = {
  ...base,
  runs: {
    'SH-WAN-01': {
      status: 'turbo_testing' as const,
      takeUrl: 'https://old.example/take.mp4',
      kfSourceHash: 'still-now',
      runwayAttempts: [{ n: 2, at: '', status: 'SUCCEEDED', taskId: 'old', outputUrl: 'https://old.example/take.mp4' }],
    },
  },
} as SeriesPilotState;
const keptWan = mergeRemotePilot(remOldTake, localWan);
if (!keptWan.runs['SH-WAN-01']?.runwayAttempts?.some((a) => a.outputUrl === 'https://fal.example/wan.mp4')) {
  fail.push('mergeRemote must keep local Wan attempt when server graph is older');
}
if (keptWan.runs['SH-WAN-01']?.takeUrl !== 'https://fal.example/wan.mp4') {
  fail.push('mergeRemote must play local Wan take over stale server takeUrl');
}
const sh01Live = {
  ...base,
  runs: {
    'EP01-SC01-SH01': {
      status: 'turbo_testing' as const,
      kfApproved: true,
      kfSourceHash: 'hcfbd2a78:140859',
      takeUrl: 'https://v3b.fal.media/files/b/old/SfHrc.mp4',
      motionNeedsRemake: true,
      turboStatus: 'SUCCEEDED' as const,
      videoPipe: 'VIDEO_DOWNLOADING' as const,
      runwayAttempts: [
        {
          n: 38,
          status: 'SUCCEEDED',
          taskId: 'wan_old',
          outputUrl: 'https://v3b.fal.media/files/b/old/SfHrc.mp4',
          kf: { hash: 'h9c99120b:160267' },
        },
        {
          n: 44,
          status: 'SUCCEEDED',
          resultClass: 'LATE_RESULT' as const,
          taskId: 'wan_01a08088-04a0-7e81-bc31-c0a786a4c5c2',
          outputUrl: 'https://v3b.fal.media/files/b/0aa99696/RYvS1wcKK4hUZ11j0s7Vz_VSlXMNoU.mp4',
          kf: { hash: 'hcfbd2a78:140859' },
        },
      ],
    },
  },
} as SeriesPilotState;
const boundSh01 = bindPictureHashOnRuns(sh01Live);
if (!boundSh01.runs['EP01-SC01-SH01']?.motionNeedsRemake) {
  fail.push('bind must not clear remake or promote take 44 to CURRENT');
}
const sh01P2 = {
  ...base,
  runs: {
    'EP01-SC01-SH01': {
      ...sh01Live.runs['EP01-SC01-SH01'],
      pictureRevisionId: 'picture:EP01-SC01-SH01:002',
      pictureRevisionAttemptN: 44,
      turboTaskId: 'wan_01a08088-04a0-7e81-bc31-c0a786a4c5c2',
    },
  },
} as SeriesPilotState;
const boundSh01P2 = bindPictureHashOnRuns(sh01P2);
if (boundSh01P2.runs['EP01-SC01-SH01']?.takeUrl === 'https://v3b.fal.media/files/b/0aa99696/RYvS1wcKK4hUZ11j0s7Vz_VSlXMNoU.mp4') {
  fail.push('bind must not reattach take 44 after picture revision epoch');
}
if (boundSh01.runs['EP01-SC01-SH01']?.videoPipe === 'VIDEO_READY' && !sh01Live.runs['EP01-SC01-SH01']?.motionNeedsRemake) {
  fail.push('bind must not set VIDEO_READY from historical take');
}
const newStill = 'data:image/jpeg;base64,PIXELNEUSTILL999';
const rebound = bindPictureHashOnRuns({
  ...sh01Live,
  runs: {
    'EP01-SC01-SH01': {
      ...sh01Live.runs['EP01-SC01-SH01']!,
      keyframeDataUrl: newStill,
      kfApproved: true,
      kfSourceHash: 'hcfbd2a78:140859',
      motionNeedsRemake: true,
    },
  },
} as SeriesPilotState);
const reboundRun = rebound.runs['EP01-SC01-SH01'];
if (reboundRun?.kfSourceHash === 'hcfbd2a78:140859') {
  fail.push('bind must restamp hash from live new still pixels');
}
if (!reboundRun?.motionNeedsRemake) {
  fail.push('bind must not reattach take 44 after a new still');
}
if (reboundRun?.takeUrl === 'https://v3b.fal.media/files/b/0aa99696/RYvS1wcKK4hUZ11j0s7Vz_VSlXMNoU.mp4' && !reboundRun.motionNeedsRemake) {
  fail.push('new still must not play old Wan as current');
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

rememberKfPixels('EP01-SC01-SH01', 'data:image/png;base64,ghost');
const fresh = {
  ...emptyPilot(),
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP01',
    title: 'Phía Sau Điểm Số',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [scShot('EP01-SC01-SH01', 'SH01', 'Minh bước vào nhà')],
  },
} as SeriesPilotState;
if (shotRunOf(fresh, fresh.episode!.shots[0]!).keyframeDataUrl) {
  fail.push('new story shot must not inherit leftover KF pixels');
}

const photoreal = {
  status: 'keyframe_ready' as const,
  keyframeDataUrl: 'data:image/png;base64,school',
  visualMode: VISUAL_MODE_PHOTOREAL,
  shotAction: 'Minh nhìn giấy 8/10 ngoài sân trường',
};
if (stillOf(photoreal, 'Minh bước vào nhà, mẹ lau bàn')) fail.push('photoreal leftover must not display');
if (stillOf({ ...photoreal, visualMode: VISUAL_MODE_3D, shotAction: 'Minh bước vào nhà, mẹ lau bàn' }, 'Minh bước vào nhà, mẹ lau bàn')) {
  fail.push('draft 3D without kfBoundAction must not display');
}
if (
  !stillOf(
    {
      ...photoreal,
      visualMode: VISUAL_MODE_3D,
      kfBoundAction: 'Minh bước vào nhà, mẹ lau bàn',
    },
    'Minh bước vào nhà, mẹ lau bàn',
  )
) {
  fail.push('bound 3D still must display');
}
if (
  stillOf(
    { status: 'approved' as const, keyframeDataUrl: 'data:image/png;base64,ok', visualMode: VISUAL_MODE_3D },
    'Minh bước vào nhà',
  )
) {
  /* approved unstamped EP take may show */
} else {
  fail.push('approved still without bind may display');
}

const dirty = {
  ...emptyPilot(),
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP01',
    title: 'Tập 01',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [scShot('EP01-SC01-SH01', 'SH01', 'cũ')],
  },
  runs: {
    'EP01-SC01-SH01': {
      status: 'keyframe_ready' as const,
      keyframeDataUrl: 'data:image/png;base64,old',
      visualMode: VISUAL_MODE_PHOTOREAL,
    },
  },
} as SeriesPilotState;
const parsed = parseFamixaPack(`VIDEO TITLE: Phía Sau Điểm Số
EPISODE: 01
SC01 —
Minh bước vào nhà.
Mẹ: Về rồi hả con?`);
if (parsed.error) fail.push(`reparse ${parsed.error}`);
else {
  const replaced = replaceStoryFromParse(dirty, parsed, dirty.packDraft || '');
  if (replaced.runs['EP01-SC01-SH01']?.keyframeDataUrl) fail.push('replace story must strip old KF');
}

if (fail.length) {
  console.error('SHORT KF KEEP FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('SHORT KF KEEP PASS');
