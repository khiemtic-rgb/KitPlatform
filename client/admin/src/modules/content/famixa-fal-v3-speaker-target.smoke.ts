/** FAMIXA_FAL_V3_SPEAKER_TARGET_FORENSIC_V1 — T1–T13. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  buildLipSyncProviderHowRequest,
  freezeLipSyncExecutionInput,
} from './famixa-lipsync-execution-input-freeze';
import {
  assertFrozenSpeakerTargetUnchanged,
  lipsyncSpeakerCharacterIdFromSide,
  lipsyncSpeakerPickOf,
} from './famixa-lipsync-speaker-target';
import { mapFrozenSpeakerToFalV3How } from './famixa-fal-v3-lipsync-speaker-how';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const chars = [
  { id: 'CHAR-003', name: 'Linh' },
  { id: 'CHAR-001', name: 'Minh' },
];

const linhPick = lipsyncSpeakerPickOf('right', chars);
const minhPick = lipsyncSpeakerPickOf('left', chars);
ok(linhPick.characterId === 'CHAR-003', 'T1 UI Linh selection → CHAR-003');
ok(minhPick.characterId === 'CHAR-001', 'T2 UI Minh selection → CHAR-001');
ok(lipsyncSpeakerCharacterIdFromSide('right', chars) === 'CHAR-003', 'T1 side right is Linh');
ok(lipsyncSpeakerCharacterIdFromSide('left', chars) === 'CHAR-001', 'T2 side left is Minh');

const frozen = freezeLipSyncExecutionInput({
  motionArtifactId: 'mot:EP01-SC01-SH01:55',
  motionFingerprint: 'f64e90d',
  motionDurationSec: 4,
  voiceArtifactId: 'vpa-20260915T161911Z-e0b3093f',
  voiceFingerprint: '17dbe460',
  voiceDurationSec: 4.24,
  fingerprint: 'lip-speaker',
  providerId: 'fal',
  modelId: 'v3',
  lineId: 'line-SC01-CHAR-003-1',
  motionTaskId: 'veo_fixture_take_55',
  motionUrl: 'https://take/55.mp4',
  speakerCharacterId: linhPick.characterId,
  speakerFaceRegion: { x: linhPick.x, y: linhPick.y, width: linhPick.width, height: linhPick.height },
});
ok(frozen.speakerCharacterId === 'CHAR-003', 'T3 speaker target survives freeze');

let threw = false;
try {
  assertFrozenSpeakerTargetUnchanged(frozen, 'CHAR-001');
} catch {
  threw = true;
}
ok(threw, 'T4 speaker target cannot change after freeze');
assertFrozenSpeakerTargetUnchanged(frozen, 'CHAR-003');

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
  speaker: minhPick,
});
ok(how.speakerCharacterId === 'CHAR-003', 'T5 provider receives target CHAR-003');
ok(how.voiceDurationSec === 4.24, 'T8 Voice duration 4.24 preserved on HOW');
ok(how.performanceDurationSec === 4, 'T10 Motion performance duration remains 4.00');

const falHow = mapFrozenSpeakerToFalV3How({
  videoUrl: how.videoUrl,
  takeTaskId: how.takeTaskId,
  syncMode: how.syncMode,
  speakerCharacterId: how.speakerCharacterId,
  speakerFaceRegion: frozen.speakerFaceRegion,
  speakerFrameNumber: 0,
});
ok(Boolean(falHow.options?.active_speaker_detection), 'T6/T7 adapter emits ASD from frozen Linh');
ok(!falHow.sync_mode || falHow.sync_mode === 'silence', 'T6 sync_mode is documented');

const liveMutated = { ...frozen, speakerCharacterId: 'CHAR-001' };
ok(frozen.speakerCharacterId === 'CHAR-003' && liveMutated.speakerCharacterId === 'CHAR-001', 'T4 live copy is not freeze');
const howAfterLive = buildLipSyncProviderHowRequest({
  frozen,
  input: {
    motionArtifactId: frozen.motionArtifactId,
    voiceArtifactId: frozen.voiceArtifactId,
    videoUrl: frozen.motionUrl,
    takeTaskId: frozen.motionTaskId,
    voiceBytes: new Uint8Array([1]),
  },
  clipId: 'EP01-SC01-SH01',
  model: 'v3',
});
ok(howAfterLive.speakerCharacterId === 'CHAR-003', 'T4 HOW ignores later live CHAR-001');

const turbo = readFileSync(join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs'), 'utf8');
const audioPrep = readFileSync(join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/FamixaFalLipsyncAudioPrepV1.cs'), 'utf8');
ok(turbo.includes('FamixaFalLipsyncAudioPrepV1.KeepFullVoiceBytes'), 'T8/T9 server keeps full single voice');
ok(audioPrep.includes('KeepFullVoiceBytes(int voiceCount) => voiceCount == 1'), 'T9 single voice not trimmed to motion');
ok(turbo.includes('MergeLipsyncVoices'), 'merge remains for multi');
ok(how.voiceDurationSec !== how.performanceDurationSec, 'T8/T10 voice 4.24 ≠ motion 4.00');

ok(frozen.motionArtifactId === 'mot:EP01-SC01-SH01:55', 'T11 motion identity unchanged');
ok(frozen.voiceArtifactId === 'vpa-20260915T161911Z-e0b3093f', 'T11 voice identity unchanged');
ok(frozen.motionDurationSec === 4 && frozen.voiceDurationSec === 4.24, 'T11 frozen durations unchanged');

const impl = [
  readFileSync(join(root, 'famixa-lipsync-speaker-target.ts'), 'utf8'),
  readFileSync(join(root, 'famixa-fal-v3-lipsync-speaker-how.ts'), 'utf8'),
  readFileSync(join(root, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8'),
].join('\n');
ok(!impl.includes('01a0a327'), 'T12 historical LipSync task untouched');
ok(!impl.includes('startContentSeriesLipsync'), 'T13 no client Fal submit in speaker modules');
ok(!impl.includes('queue.fal.run'), 'T13 no Fal queue URL');
ok(!impl.includes('elevenlabs') && !impl.includes('ElevenLabs'), 'T13 no ElevenLabs');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS T1–T13 FAMIXA_FAL_V3_SPEAKER_TARGET_FORENSIC_V1');
