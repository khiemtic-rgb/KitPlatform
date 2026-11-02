/** FAMIXA_FAL_V3_TARGETED_SPEAKER_INPUT_FIX_V1 — T1–T15. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  buildLipSyncProviderHowRequest,
  freezeLipSyncExecutionInput,
} from './famixa-lipsync-execution-input-freeze';
import {
  LIPSYNC_SPEAKER_MARK_FRAME_NUMBER,
  assertFrozenSpeakerTargetUnchanged,
  assertLipSyncSpeakerTargetReady,
  canMutateLiveSpeakerTarget,
  lipsyncSpeakerPickOf,
  shouldOmitRejectedSpeakerPin,
} from './famixa-lipsync-speaker-target';
import {
  falV3ActiveSpeakerDetectionOf,
  mapFrozenSpeakerToFalV3How,
  sourceVideoPixelCoordinates,
  whatHasFalSpecificSpeakerField,
} from './famixa-fal-v3-lipsync-speaker-how';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const chars = [
  { id: 'CHAR-003', name: 'Linh' },
  { id: 'CHAR-001', name: 'Minh' },
];

const linh = lipsyncSpeakerPickOf('right', chars);
const minh = lipsyncSpeakerPickOf('left', chars);
ok(linh.characterId === 'CHAR-003', 'T1 click Linh → CHAR-003');
ok(minh.characterId === 'CHAR-001', 'T2 click Minh → CHAR-001');

const frozen = freezeLipSyncExecutionInput({
  motionArtifactId: 'mot:EP01-SC01-SH01:55',
  motionFingerprint: 'f64e90d',
  motionDurationSec: 4,
  voiceArtifactId: 'vpa-20260915T161911Z-e0b3093f',
  voiceFingerprint: '17dbe460',
  voiceDurationSec: 4.24,
  fingerprint: 'lip-target-fix',
  providerId: 'fal',
  modelId: 'v3',
  lineId: 'line-SC01-CHAR-003-1',
  motionTaskId: 'veo_fixture_take_55',
  motionUrl: 'https://take/55.mp4',
  speakerCharacterId: linh.characterId,
  speakerFaceRegion: { x: linh.x, y: linh.y, width: linh.width, height: linh.height },
  speakerFrameNumber: linh.frameNumber,
});
ok(frozen.speakerCharacterId === 'CHAR-003', 'T3 Linh target survives freeze');

let threw = false;
try {
  assertFrozenSpeakerTargetUnchanged(frozen, 'CHAR-001');
} catch {
  threw = true;
}
ok(threw, 'T4 speaker target immutable after freeze');
ok(!canMutateLiveSpeakerTarget(frozen), 'T4 UI cannot change provider target after freeze');
ok(canMutateLiveSpeakerTarget(undefined), 'T4 live pick allowed before freeze');
ok(
  canMutateLiveSpeakerTarget(frozen, {
    lipsyncError:
      'Fal từ chối options.active_speaker_detection: invalid active speaker detection configuration. We could not use the selected speaker face for this video. Please select the face again or try auto',
  }),
  'T4 remake can retarget after Fal face reject',
);

const [px, py] = sourceVideoPixelCoordinates(0.66, 0.38, 1280, 720);
ok(px === 845 && py === 274, 'T5 normalized UI → 1280x720 pixels');
ok(frozen.speakerFaceRegion?.x === linh.x && frozen.speakerFaceRegion?.y === linh.y, 'T5 freeze keeps normalized coords');
const [linhPx, linhPy] = sourceVideoPixelCoordinates(linh.x, linh.y, 1280, 720);
ok(linh.x === 0.63 && linh.y === 0.25, 'preset Linh is face, not chest');
ok(linhPx === 806 && linhPy === 180, 'preset Linh pin is on the face band');

ok(frozen.speakerFrameNumber === LIPSYNC_SPEAKER_MARK_FRAME_NUMBER, 'T6 frame_number from deterministic mark frame');
ok(LIPSYNC_SPEAKER_MARK_FRAME_NUMBER === 0, 'T6 existing marking frame is 0');

const falHow = mapFrozenSpeakerToFalV3How({
  videoUrl: frozen.motionUrl,
  takeTaskId: frozen.motionTaskId,
  syncMode: 'silence',
  speakerCharacterId: frozen.speakerCharacterId,
  speakerFaceRegion: frozen.speakerFaceRegion,
  speakerFrameNumber: frozen.speakerFrameNumber,
});
const asd = falHow.options?.active_speaker_detection;
ok(Boolean(asd), 'T7 Fal adapter emits active_speaker_detection');
ok(asd?.frame_number === 0, 'T6/T7 frame_number present');
ok(asd?.coordinates[0] === linhPx && asd?.coordinates[1] === linhPy, 'T7 coordinates are source pixels');
ok(Number.isInteger(asd?.coordinates[0]) && Number.isInteger(asd?.coordinates[1]), 'T7 coordinates are integers');

const what = {
  speakerCharacterId: frozen.speakerCharacterId,
  speakerFaceRegion: frozen.speakerFaceRegion,
};
ok(!whatHasFalSpecificSpeakerField(what), 'T8 no Fal-specific fields leak into WHAT');

const how = buildLipSyncProviderHowRequest({
  frozen,
  input: {
    motionArtifactId: frozen.motionArtifactId,
    voiceArtifactId: frozen.voiceArtifactId,
    videoUrl: frozen.motionUrl,
    takeTaskId: frozen.motionTaskId,
    voiceBytes: new Uint8Array([1]),
  },
  clipId: 'EP01-SC01-SH01',
  syncMode: 'silence',
  model: 'v3',
});
ok(how.voiceDurationSec === 4.24, 'T9 voice remains 4.24s');
ok(how.performanceDurationSec === 4, 'T11 Motion remains 4.00s');
ok(how.motionArtifactId === 'mot:EP01-SC01-SH01:55', 'T12 Motion artifact remains Take 55');
ok(how.voiceArtifactId === 'vpa-20260915T161911Z-e0b3093f', 'T13 Voice artifact remains vpa');

const turbo = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'),
  'utf8',
);
const audioPrep = readFileSync(
  join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/FamixaFalLipsyncAudioPrepV1.cs'),
  'utf8',
);
ok(turbo.includes('FamixaFalLipsyncAudioPrepV1.KeepFullVoiceBytes'), 'T10 server keeps full single voice');
ok(audioPrep.includes('voiceCount == 1'), 'T10 no atrim=0:4 for single LipSync voice');

ok(!turbo.includes('01a0a327'), 'T14 historical LipSync untouched in turbo');
const impl = [
  readFileSync(join(root, 'famixa-lipsync-speaker-target.ts'), 'utf8'),
  readFileSync(join(root, 'famixa-fal-v3-lipsync-speaker-how.ts'), 'utf8'),
  readFileSync(join(root, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8'),
].join('\n');
ok(!impl.includes('01a0a327'), 'T14 historical LipSync task untouched');
ok(!impl.includes('startContentSeriesLipsync'), 'T15 no client Fal submit in speaker modules');
ok(!impl.includes('queue.fal.run') && !impl.includes('elevenlabs'), 'T15 zero provider calls');

assertLipSyncSpeakerTargetReady({
  multiFace: true,
  speakerCharacterId: 'CHAR-003',
  speakerFaceRegion: frozen.speakerFaceRegion,
  requiredCharacterId: 'CHAR-003',
});
let closed = false;
try {
  assertLipSyncSpeakerTargetReady({ multiFace: true, requiredCharacterId: 'CHAR-003' });
} catch {
  closed = true;
}
ok(closed, 'T7 fail-closed when multi-face has no target');
ok(
  JSON.stringify(falV3ActiveSpeakerDetectionOf({ x: 0.66, y: 0.38, width: 1280, height: 720, frameNumber: 0 })) ===
    JSON.stringify({ frame_number: 0, coordinates: [845, 274] }),
  'T7 exact ASD object',
);
const rejectErr =
  'invalid active speaker detection configuration. We could not use the selected speaker face for this video. Please select the face again or try auto';
ok(
  shouldOmitRejectedSpeakerPin({
    remake: true,
    error: rejectErr,
    frozenRegion: { x: 0.66, y: 0.38 },
    liveMark: { x: 0.66, y: 0.38 },
  }),
  'chest pin after reject is not resent',
);
ok(
  !shouldOmitRejectedSpeakerPin({
    remake: true,
    error: rejectErr,
    frozenRegion: { x: 0.66, y: 0.38 },
    liveMark: { x: 0.63, y: 0.25 },
  }),
  'face-band pin is sent so the silent mouth stays still',
);
ok(!shouldOmitRejectedSpeakerPin({ remake: true, error: '' }), 'omit only after Fal face reject');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS T1–T15 FAMIXA_FAL_V3_TARGETED_SPEAKER_INPUT_FIX_V1');
