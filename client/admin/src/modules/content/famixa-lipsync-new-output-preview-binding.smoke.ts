/** FAMIXA_SH01_LIPSYNC_NEW_OUTPUT_PREVIEW_BINDING_V1 — desk preview only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { editorialSourceOf } from './content-famixa-editorial-cut';
import { playableFalLipsyncSrc } from './famixa-fal-lipsync-playback';
import {
  currentSuccessfulLipsyncPlayable,
  lipsyncBindReleasedByNewerMotion,
  resolveCurrentLipsyncPreviewBinding,
  resolveCurrentProductionVideoSource,
} from './famixa-lipsync-motion-authority';
import { resolveShotPreviewKind } from './ContentFamixaShotProduction/ShotProductionPreview';
import type { SeriesShotRun } from './content-famixa-series';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const SHOT = 'EP01-SC01-SH01';
const HIST = 'lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f';
const LIP_PTR = `/api/content/series/fal-video?taskId=${HIST}`;
const LIP_BLOB = 'blob:http://localhost/lip-6s';
const TAKE_4S = 'blob:http://localhost/veo-4s';
const FINAL_4S = 'blob:http://localhost/final-4s';
const MOTION_57 = 'https://take/57.mp4';

function runOf(over: Partial<SeriesShotRun> = {}): SeriesShotRun {
  return {
    status: 'reviewed',
    kfApproved: true,
    videoApproved: true,
    lipsyncTaskId: HIST,
    lipsyncStatus: 'SUCCEEDED',
    lipsynced: true,
    lipsyncUrl: LIP_PTR,
    finalSource: 'FAL',
    takeUrl: TAKE_4S,
    previewUrl: TAKE_4S,
    ...over,
  };
}

ok(
  !playableFalLipsyncSrc({ lipsyncUrl: LIP_PTR }),
  'T1 bare fal-video pointer is not a <video> src',
);
ok(
  playableFalLipsyncSrc({ blobUrl: LIP_BLOB, lipsyncUrl: LIP_PTR }) === LIP_BLOB,
  'T1 auth blob of current LipSync is playable',
);

const incomplete = runOf({ lipsyncFrozenInput: undefined, runwayAttempts: [] });
const srcIncomplete = resolveCurrentProductionVideoSource({
  run: incomplete,
  shotId: SHOT,
  lipsyncPlayableUrl: LIP_BLOB,
  fallbackMotionUrl: TAKE_4S,
});
ok(srcIncomplete.kind === 'lipsync' && srcIncomplete.url === LIP_BLOB, 'T2 incomplete freeze still binds playable LipSync');
ok(srcIncomplete.url !== TAKE_4S, 'T2 Motion 4s does not steal current LipSync');

const boundFreeze = runOf({
  lipsyncFrozenInput: {
    motionArtifactId: `mot:${SHOT}:55`,
    voiceFingerprint: 'voice',
    fingerprint: 'lip',
  },
  runwayAttempts: [
    {
      n: 55,
      status: 'SUCCEEDED',
      resultClass: 'CURRENT',
      outputUrl: TAKE_4S,
      taskId: 'veo_take55',
    },
    {
      n: 56,
      status: 'SUCCEEDED',
      resultClass: 'LATE_RESULT',
      outputUrl: 'https://take/56.mp4',
      taskId: 'veo_take56',
    },
  ],
});
ok(currentSuccessfulLipsyncPlayable({ run: boundFreeze, shotId: SHOT, lipsyncPlayableUrl: LIP_BLOB }), 'T3 bound 55 + blob is current LipSync');
ok(!lipsyncBindReleasedByNewerMotion(boundFreeze, SHOT), 'T3 LATE 56 does not release LipSync bind');

const bind = resolveCurrentLipsyncPreviewBinding({
  run: boundFreeze,
  shotId: SHOT,
  lipsyncPlayableUrl: LIP_BLOB,
  takeUrl: TAKE_4S,
  finalUrl: FINAL_4S,
});
ok(bind.holdLipsync && bind.lipsyncUrl === LIP_BLOB, 'T4 current LipSync wins preview');
ok(!bind.takeUrl && !bind.finalUrl, 'T4 stale Final 4s and Motion 4s stay off <video>');
const prev = resolveShotPreviewKind({
  finalUrl: bind.finalUrl,
  lipsyncUrl: bind.lipsyncUrl,
  takeUrl: bind.takeUrl,
});
ok(prev.kind === 'lipsync' && prev.src === LIP_BLOB, 'T4 <video> src is new LipSync blob');

const loading = resolveCurrentLipsyncPreviewBinding({
  run: boundFreeze,
  shotId: SHOT,
  lipsyncPlayableUrl: undefined,
  falFileMissing: false,
  takeUrl: TAKE_4S,
  finalUrl: FINAL_4S,
});
ok(loading.holdLipsync && !loading.takeUrl && !loading.finalUrl, 'T5 while current LipSync hydrates, do not flash 4s Motion/Final');
const loadingCover = resolveCurrentLipsyncPreviewBinding({
  run: boundFreeze,
  shotId: SHOT,
  lipsyncPlayableUrl: undefined,
  falFileMissing: false,
  takeUrl: TAKE_4S,
  finalUrl: FINAL_4S,
  speechFloorSec: 5.21,
});
ok(loadingCover.holdLipsync && !loadingCover.takeUrl, 'T5b speech covering must not steal hero while Fal blob hydrates');

const missing = resolveCurrentLipsyncPreviewBinding({
  run: boundFreeze,
  shotId: SHOT,
  lipsyncPlayableUrl: undefined,
  falFileMissing: true,
  takeUrl: TAKE_4S,
  finalUrl: FINAL_4S,
});
ok(!missing.holdLipsync && missing.takeUrl === TAKE_4S, 'T6 missing Fal file may fall back to Motion');

const released = runOf({
  lipsyncFrozenInput: {
    motionArtifactId: `mot:${SHOT}:55`,
    voiceFingerprint: 'voice',
    fingerprint: 'lip',
  },
  runwayAttempts: [
    { n: 55, status: 'SUCCEEDED', resultClass: 'CURRENT', outputUrl: TAKE_4S, taskId: 'veo_take55' },
    { n: 57, status: 'SUCCEEDED', resultClass: 'CURRENT', outputUrl: MOTION_57, taskId: 'veo_take57' },
  ],
});
ok(lipsyncBindReleasedByNewerMotion(released, SHOT), 'T7 newer CURRENT motion releases historical LipSync bind');
const stalePrev = resolveCurrentLipsyncPreviewBinding({
  run: released,
  shotId: SHOT,
  lipsyncPlayableUrl: LIP_BLOB,
  takeUrl: MOTION_57,
  finalUrl: FINAL_4S,
});
ok(!stalePrev.holdLipsync && stalePrev.takeUrl === MOTION_57 && stalePrev.lipsyncUrl !== LIP_BLOB, 'T7 historical task cannot stay on preview');

ok(
  editorialSourceOf({
    lipsynced: true,
    lipsyncTaskId: HIST,
    lipsyncUrl: LIP_PTR,
    takeUrl: 'https://take/4s.mp4',
  }) === LIP_PTR,
  'T8 editorial current LipSync pointer beats take URL',
);
ok(
  editorialSourceOf({ takeUrl: 'https://take/4s.mp4' }) === 'https://take/4s.mp4',
  'T8 take remains when no LipSync',
);

const root = dirname(fileURLToPath(import.meta.url));
const desk = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionWorkspace.tsx'), 'utf8');
ok(desk.includes('resolveCurrentLipsyncPreviewBinding'), 'T9 desk uses current LipSync preview bind');
ok(desk.includes('deskFalPlayableForShot') && desk.includes('falPlaySrc'), 'T9 desk still hydrates Fal blob per shot');
ok(!/fal\.ai\/sync-lipsync|elevenlabs|generativelanguage|runwayml/i.test(desk), 'T10 no provider submit on desk bind');

if (fail.length) {
  console.error(`famixa-lipsync-new-output-preview-binding.smoke failed:\n- ${fail.join('\n- ')}`);
  process.exit(1);
}
console.log('famixa-lipsync-new-output-preview-binding.smoke ok');
