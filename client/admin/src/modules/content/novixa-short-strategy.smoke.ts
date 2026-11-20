import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { videoProductionPreflight, type DialogueCue } from './famixa-video-audio-lipsync-pipeline';
import {
  backendAcceptedStrategy,
  isConfirmedStaticVoiceOver,
  strategyClaim,
  strategyEngaged,
} from './novixa-short-strategy';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(!strategyEngaged({}) && Object.keys(strategyClaim({})).length === 0, 'blank shot stays off the strategy claim');
ok(!isConfirmedStaticVoiceOver({ pictureStrategy: 'STATIC', speechDelivery: 'VOICE_OVER' }), 'unconfirmed static voice-over is not accepted locally');
ok(
  isConfirmedStaticVoiceOver({
    pictureStrategy: 'STATIC',
    speechDelivery: 'VOICE_OVER',
    strategyConfirmed: true,
  }),
  'confirmed static voice-over matches the server predicate',
);

const sent = strategyClaim({
  pictureStrategy: 'STATIC',
  speechDelivery: 'VOICE_OVER',
  strategyConfirmed: true,
  characterIds: ['CHAR-001', 'CHAR-002'],
});
ok(sent.pictureStrategy === 'STATIC' && sent.speechDelivery === 'VOICE_OVER' && sent.strategyConfirmed === true, 'claim carries the three fields');
ok(sent.directSpeakerCount === 0, 'voice-over does not invent an on-camera speaker count');

ok(
  backendAcceptedStrategy(undefined, true) === false,
  'missing catalog is not confirmation',
);
ok(
  backendAcceptedStrategy(
    {
      buildId: 'b',
      shotCode: 'NV-01',
      currentState: 'EMPTY',
      actions: [{ action: 'CREATE_VIDEO', available: false, blockReason: 'STRATEGY_CONFIRMATION_REQUIRED' }],
    },
    true,
  ) === false,
  'confirmation block keeps the badge off',
);
ok(
  backendAcceptedStrategy(
    {
      buildId: 'b',
      shotCode: 'NV-01',
      currentState: 'EMPTY',
      actions: [{ action: 'CREATE_VIDEO', available: false, blockReason: 'STATIC_FORBIDS_PAID_VIDEO' }],
    },
    true,
  ) === true,
  'a paid-video block after confirm means the server accepted the strategy',
);
ok(
  backendAcceptedStrategy(
    {
      buildId: 'b',
      shotCode: 'NV-01',
      currentState: 'EMPTY',
      actions: [{ action: 'CREATE_VIDEO', available: true, blockReason: null }],
    },
    false,
  ) === false,
  'an unsent confirmation cannot light the badge',
);

const cue: DialogueCue = {
  dialogueId: 'd',
  shotId: 'NV-01',
  speakerCharacterId: 'CHAR-001',
  text: 'Đóng cửa rồi.',
  voiceId: 'voice',
  startSec: 0,
  endSec: 2,
  audioAssetId: 'aud',
  audioDurationSec: 2,
  status: 'READY',
};
const still = videoProductionPreflight({
  shotId: 'NV-01',
  hasCanonicalKf: true,
  cues: [cue],
  hasMuteTake: false,
  hasLipSync: false,
  mixReady: true,
  directorApproved: true,
  voiceOverStill: true,
});
ok(
  still.finalReady &&
    still.finalSource === 'VOICE_OVER_STILL' &&
    still.finalSource !== 'I2V_ALLOWED' &&
    still.finalSource !== 'TTS_OVERLAY' &&
    still.videoDurationSec === still.audioDurationSec &&
    still.audioDurationSec === 2 &&
    !still.blockers.includes('LIPSYNC_REQUIRED') &&
    !still.blockers.includes('PREVIEW_ONLY'),
  'static voice-over final source is the still hold',
);
const legacySpoken = videoProductionPreflight({
  shotId: 'EP01-SC01-SH02',
  hasCanonicalKf: true,
  cues: [cue],
  hasMuteTake: true,
  hasLipSync: false,
  mixReady: true,
});
ok(legacySpoken.finalSource === 'RAW_I2V' && !legacySpoken.finalReady, 'undeclared spoken shot still requires lip-sync');
const confirmedPlan = videoProductionPreflight({
  shotId: 'NV-01',
  hasCanonicalKf: true,
  cues: [cue],
  hasMuteTake: false,
  hasLipSync: false,
  mixReady: true,
  pictureStrategy: 'STATIC',
  speechDelivery: 'VOICE_OVER',
  strategyConfirmed: true,
});
ok(confirmedPlan.finalSource === 'VOICE_OVER_STILL' && confirmedPlan.finalReady, 'confirmed plan maps to the still final');
const unconfirmedPlan = videoProductionPreflight({
  shotId: 'NV-01',
  hasCanonicalKf: true,
  cues: [cue],
  hasMuteTake: true,
  hasLipSync: false,
  mixReady: true,
  pictureStrategy: 'STATIC',
  speechDelivery: 'VOICE_OVER',
  strategyConfirmed: false,
});
ok(unconfirmedPlan.finalSource === 'RAW_I2V' && !unconfirmedPlan.finalReady, 'unconfirmed plan stays on the Famixa contract');
const movingPlan = videoProductionPreflight({
  shotId: 'NV-01',
  hasCanonicalKf: true,
  cues: [cue],
  hasMuteTake: true,
  hasLipSync: false,
  mixReady: true,
  pictureStrategy: 'IMAGE_TO_VIDEO',
  speechDelivery: 'LIP_SYNC',
  strategyConfirmed: true,
});
ok(movingPlan.finalSource === 'RAW_I2V' && !movingPlan.finalReady, 'confirmed image-to-video does not become a still');
const overlay = videoProductionPreflight({
  shotId: 'NV-01',
  hasCanonicalKf: true,
  cues: [cue],
  hasMuteTake: false,
  hasLipSync: false,
  mixReady: true,
  directorApproved: true,
  overlayPreview: true,
  voiceOverStill: true,
});
ok(
  !overlay.finalReady &&
    overlay.finalSource === 'NONE' &&
    overlay.finalSource !== 'TTS_OVERLAY' &&
    overlay.blockers.includes('PREVIEW_ONLY') &&
    overlay.blockers.includes('NOT_ELIGIBLE_FOR_FINAL'),
  'overlay cannot become the still final',
);

const root = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const desk = readFileSync(join(root, 'NovixaShortStrategyDesk.tsx'), 'utf8');
ok(series.includes('NovixaShortStrategyDesk'), 'shot plan mounts the strategy desk');
ok(series.includes('strategyClaim('), 'production claims include the strategy fields');
ok(desk.includes('suggestNovixaShortStrategy'), 'suggestion is requested from the server');
ok(!desk.includes('strategyConfirmed: true') || desk.includes('event.target.checked'), 'the desk does not hard-code confirmation');
ok(series.includes('isConfirmedStaticVoiceOver'), 'mix click uses the confirmed still predicate');

if (fail.length) {
  console.error(`NOVIXA_SHORT_DESK FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('NOVIXA_SHORT_DESK PASS FAIL=0 (no provider)');
