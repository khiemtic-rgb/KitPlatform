/** FAMIXA_FAL_LIPSYNC_PLAYBACK_V1 — local path only. 0 Fal POST. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { canStampFalLipsyncFinal, deskAuthVideoSrc, deskFalPlayableForShot, falLipsyncPlaybackUrl, isFalLipsyncTaskId, isFalPlaybackSrc, isPlayableFalOutputUrl, lipsyncRunFileMissing, playableFalLipsyncSrc, rewriteFalLipsyncPointers } from './famixa-fal-lipsync-playback';
import { isLocalApiOrigin } from '../../shared/api/api-network';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const task = 'lipsync_v3_01a09617-d14e-77d3-abd2-31400caa566c';
ok(isFalLipsyncTaskId(task), 'lipsync_v3 is a Fal task');
ok(!isFalLipsyncTaskId('veo_abc'), 'Veo is not Fal');
const play = falLipsyncPlaybackUrl(task);
ok(play?.includes('/content/series/fal-video') && play.includes('taskId='), 'short HTTP path');
ok(isFalPlaybackSrc(play), 'local path is playback src');
ok(!isFalPlaybackSrc('https://v3b.fal.media/files/x.mp4'), 'CDN is not local playback');
ok(!playableFalLipsyncSrc({ lipsyncUrl: play }), 'bare fal-video is not playable');
ok(
  playableFalLipsyncSrc({ blobUrl: 'blob:http://localhost/x', lipsyncUrl: play }) === 'blob:http://localhost/x',
  'auth blob is playable',
);
ok(
  playableFalLipsyncSrc({ lipsyncUrl: 'https://v3b.fal.media/files/x.mp4' }) === 'https://v3b.fal.media/files/x.mp4',
  'CDN remains playable',
);
ok(isPlayableFalOutputUrl('https://v3b.fal.media/files/x.mp4'), 'T4 CDN is playable output');
ok(!isPlayableFalOutputUrl(play), 'T3 bare pointer is not playable output');
ok(!canStampFalLipsyncFinal(play), 'T3 cannot stamp lipsynced on broken pointer');
ok(canStampFalLipsyncFinal(play, true), 'T1 verified local pointer can stamp');
ok(canStampFalLipsyncFinal('https://v3b.fal.media/files/x.mp4'), 'T2 persist FAIL keeps CDN stampable');
ok(
  playableFalLipsyncSrc({ lipsyncUrl: play, localFileReady: true }) === play,
  'T1 verified local path is canonical playable',
);

ok(
  rewriteFalLipsyncPointers({ lipsyncTaskId: task, lipsyncUrl: play }).lipsyncUrl === play,
  'rewrite keeps existing local path',
);
ok(
  rewriteFalLipsyncPointers({ lipsyncTaskId: task, lipsyncUrl: undefined }).lipsyncUrl == null,
  'rewrite does not mint a local pointer',
);
ok(
  rewriteFalLipsyncPointers({
    lipsyncTaskId: task,
    lipsyncUrl: 'https://v3b.fal.media/files/x.mp4',
  }).lipsyncUrl === 'https://v3b.fal.media/files/x.mp4',
  'rewrite does not drop live CDN before persist',
);

const root = dirname(fileURLToPath(import.meta.url));
const falCs = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentFalClient.cs'),
  'utf8',
);
ok(falCs.includes('FamixaFalLipsyncPollV1.PeekPaths') && falCs.includes('FollowPaths') && falCs.includes('QueueApp'), 'peek follows /response payload');
ok(!falCs.includes('%2Fv3'), 'peek does not encode slash');
ok(falCs.includes('PersistFalOutputAsync') && falCs.includes('TryReadStoredFal'), 'poll persists Fal mp4');
ok(falCs.includes('CanonicalPlayableUrl') && falCs.includes('TryReadNamed'), 'persist verifies file before local pointer');
ok(falCs.includes('FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection'), 'v3 speaker pin is integer pixels');
ok(!/coordinates = new\[\] \{ px, py \}/.test(falCs), 'v3 does not send float coordinates');
ok(
  falCs.includes('active_speaker_detection') &&
    falCs.includes('bounding_boxes') &&
    falCs.includes('speakerFrameCount') &&
    falCs.includes('auto_detect = false') &&
    falCs.includes('auto_detect = true') &&
    !falCs.includes('v3 = true') &&
    !falCs.includes('coordinates = pin'),
  'pin uses take-frame bounding_boxes; no-pin uses auto_detect; never v3 flag or point pin',
);
const turboCs = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'),
  'utf8',
);
ok(turboCs.includes('FrameCountFromTake'), 'turbo counts boxes from the uploaded take, not the 5s shot clock');

const desk = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionWorkspace.tsx'), 'utf8');
const card = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionCard.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
ok(desk.includes('fetchContentSeriesFalVideo') && desk.includes('falPlaySrc'), 'desk loads Fal file with auth');
ok(desk.includes('deskFalPlayableForShot') && desk.includes('falPlayShotId') && desk.includes('falFileMissing'), 'SH02 does not keep SH01 Fal blob');
ok(
  !deskFalPlayableForShot({
    shotId: 'EP01-SC01-SH02',
    taskId: 'lipsync_v3_sh02',
    fetchedShotId: 'EP01-SC01-SH01',
    fetchedTaskId: 'lipsync_v3_sh01',
    blobUrl: 'blob:http://localhost/sh01',
  }),
  'leftover SH01 blob is not playable on SH02',
);
ok(
  deskFalPlayableForShot({
    shotId: 'EP01-SC01-SH01',
    taskId: task,
    fetchedShotId: 'EP01-SC01-SH01',
    fetchedTaskId: task,
    blobUrl: 'blob:http://localhost/sh01',
  }) === 'blob:http://localhost/sh01',
  'same-shot Fal blob stays playable',
);
ok(desk.includes('deskAuthVideoSrc') && desk.includes('fetchContentSeriesFalVideo(taskId)'), 'desk hero only uses auth Fal blob');
ok(!desk.includes('isFalPlaybackSrc(raw)'), 'desk fetches Fal by taskId even when graph still has fal.media');
ok(deskAuthVideoSrc('blob:http://localhost/lip') === 'blob:http://localhost/lip', 'desk blob is hero src');
ok(!deskAuthVideoSrc('/api/content/series/fal-video?taskId=lipsync_x'), 'desk rejects bare fal-video');
ok(!deskAuthVideoSrc('https://v3b.fal.media/files/x.mp4'), 'desk rejects expired Fal CDN');
ok(desk.includes('lipsyncActionOpts') && desk.includes('{ resume: true }'), 'desk lipsync resume never opens paid Confirm');
ok(desk.includes('falNoOutput') && desk.includes('DIRECTOR_LIPSYNC_NEW_LABEL') && desk.includes('{ forceNew: true }'), 'dead Fal job opens Khớp môi mới');
ok(desk.includes('forceNewLipsync') && card.includes('forceNewLipsync') && card.includes('DIRECTOR_LIPSYNC_NEW_LABEL'), 'card keeps Khớp môi mới on dead Fal job');
ok(
  series.includes('send?.forceNew') &&
    series.includes('speaker target') &&
    series.includes('Hủy = dừng'),
  'dead job Confirm freezes speaker target before Fal',
);
ok(
  series.includes('canStampFalLipsyncFinal') &&
    series.includes('proveLocalFalFile') &&
    series.includes('localFileReady'),
  'poll/assemble require playable Fal source',
);
ok(
  lipsyncRunFileMissing({
    lipsynced: true,
    lipsyncTaskId: task,
    lipsyncUrl: play,
  }),
  'claimed Fal + bare pointer is missing file',
);
ok(
  !lipsyncRunFileMissing({ lipsyncUrl: 'https://v3b.fal.media/files/x.mp4' }),
  'CDN mp4 is not missing',
);

const net = readFileSync(join(root, '../../shared/api/api-network.ts'), 'utf8');
ok(net.includes('isLocalApiOrigin') && net.includes("host === 'localhost'"), 'localhost is not VPS');
ok(net.includes('Ensure-ApiDev') && !/isProductionApi\(\): boolean \{\s*return Boolean\(resolveApiOrigin\(\)\)/.test(net), 'VITE localhost is local');

ok(isLocalApiOrigin('http://localhost:5290'), 'localhost origin is local');
ok(isLocalApiOrigin('http://127.0.0.1:5290'), 'loopback is local');
ok(!isLocalApiOrigin('https://api.novixa.vn'), 'novixa is not local');

if (fail.length) {
  console.error(`famixa-fal-lipsync-playback.smoke failed:\n- ${fail.join('\n- ')}`);
  process.exit(1);
}
console.log('famixa-fal-lipsync-playback.smoke ok');
