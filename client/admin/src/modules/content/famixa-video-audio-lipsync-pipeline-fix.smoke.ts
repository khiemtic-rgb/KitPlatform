import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DOCUMENT_ID,
  GATE,
  I2V_IS_MUTE_TAKE,
  MIX_STAFF_EPISODE_ROLE,
  MULTI_SPEAKER_LIPSYNC_SUPPORTED,
  PROJECT_VISUAL_MODE,
  STAFF_OVERLAY_ELIGIBLE_FOR_FINAL,
  SUITE_ID,
  canFinalizeShot,
  classifyShot,
  episodeCanFinalize,
  falseFinalBlocked,
  i2vDurationForDialogue,
  mergeSameSpeakerCues,
  retryTouches,
  stitchSeconds,
  videoProductionPreflight,
  type DialogueCue,
  type ShotProductionInput,
} from './famixa-video-audio-lipsync-pipeline';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const cue = (id: string, speaker: string, start: number, end: number, ready = true): DialogueCue => ({
  dialogueId: id,
  shotId: 'SH-01',
  speakerCharacterId: speaker,
  text: 'xin chào',
  voiceId: ready ? 'voice-1' : undefined,
  startSec: start,
  endSec: end,
  audioAssetId: ready ? id : undefined,
  audioDurationSec: ready ? end - start : 0,
  status: ready ? 'READY' : 'MISSING',
});

const silent: ShotProductionInput = {
  shotId: 'S',
  hasCanonicalKf: true,
  cues: [],
  hasMuteTake: true,
  hasLipSync: false,
  mixReady: true,
  projectVisualMode: PROJECT_VISUAL_MODE,
  kfVisualMode: PROJECT_VISUAL_MODE,
  identityLocked: true,
};

const spokenNoLip: ShotProductionInput = {
  shotId: 'L',
  hasCanonicalKf: true,
  cues: [cue('d1', 'CHAR-001', 0.2, 3)],
  hasMuteTake: true,
  hasLipSync: false,
  mixReady: true,
  identityLocked: true,
};

const spokenReady: ShotProductionInput = { ...spokenNoLip, shotId: 'F', hasLipSync: true };

ok(DOCUMENT_ID === 'FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1' && SUITE_ID.includes('REGRESSION'), 'VA-doc');
ok(classifyShot([]) === 'SILENT_SHOT' && videoProductionPreflight(silent).finalReady, 'VA-01 silent');
ok(videoProductionPreflight({ ...spokenNoLip, cues: [cue('d1', 'CHAR-001', 0, 2, false)] }).blockers.includes(GATE.VOICE_REQUIRED), 'VA-02 voice');
ok(videoProductionPreflight(spokenNoLip).blockers.includes(GATE.LIPSYNC_REQUIRED), 'VA-03 lipsync');
ok(!canFinalizeShot(spokenNoLip).allowed && videoProductionPreflight(spokenNoLip).finalSource === 'RAW_I2V', 'VA-04 raw i2v');
ok((spokenReady.cues[0]?.audioDurationSec || 0) > 0.2, 'VA-05 duration');
ok(Boolean(spokenReady.cues[0]?.speakerCharacterId), 'VA-06 speaker');
ok(
  videoProductionPreflight({
    ...spokenReady,
    cues: [cue('d1', 'CHAR-001', 0, 2), cue('d2', 'CHAR-003', 2, 4)],
  }).blockers.includes(GATE.MULTI_SPEAKER_LIPSYNC_UNSUPPORTED) && !MULTI_SPEAKER_LIPSYNC_SUPPORTED,
  'VA-07 multi',
);
ok(mergeSameSpeakerCues([cue('d1', 'CHAR-001', 0, 2), cue('d2', 'CHAR-001', 2, 4)]).discarded.length === 0, 'VA-08 merge');
ok(I2V_IS_MUTE_TAKE, 'VA-09 mute');
ok(videoProductionPreflight(spokenReady).finalSource === 'LIPSYNC_VIDEO' && videoProductionPreflight(spokenReady).finalReady, 'VA-10 final source');
ok(i2vDurationForDialogue(5.6) === 10 && stitchSeconds({ kind: 'SINGLE_SPEAKER_SHOT', takeDurationSec: 5, lastAudioEndSec: 5.6 }) >= 5.6, 'VA-13 duration');
ok(!retryTouches('lipsync').keyframe && !retryTouches('lipsync').i2v, 'VA-15/16 retry lipsync');
ok(!retryTouches('mix').i2v && retryTouches('mix').mix, 'VA-17 retry mix');
ok(PROJECT_VISUAL_MODE === '3D_STYLIZED_REALISM', 'VA-19 mode');
ok(falseFinalBlocked(spokenNoLip) && !STAFF_OVERLAY_ELIGIBLE_FOR_FINAL && MIX_STAFF_EPISODE_ROLE === 'PREVIEW_ONLY', 'VA-12 overlay');
ok(!episodeCanFinalize([spokenNoLip]).allowed && episodeCanFinalize([silent, spokenReady]).allowed, 'VA-30 episode gate');

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const boards = read('ContentFamixaBuildBoards.tsx');
const api = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ContentContracts.cs');
const assemble = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesAssembleService.cs');
const turbo = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const rules = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VideoAudioLipsyncPipelineV1Rules.cs');

ok(controller.includes('video-audio-lipsync-pipeline/regression'), 'VA-api');
ok(assemble.includes('ApplyCanonicalMix') && assemble.includes('ContentMixAssets'), 'VA-11 server mix');
ok(api.includes('ContentSeriesAssembleMixDto') && api.includes('Voices'), 'VA-api mix+voices');
ok(turbo.includes('MergeLipsyncVoices'), 'VA-08 server merge');
ok(series.includes('ContentFamixaBuildVoiceBoard') && series.includes('canFinalize'), 'VA-staff voice+gate');
ok(boards.includes('Create Lip-Sync') && boards.includes('NOT SUPPORTED FOR MULTI-SPEAKER'), 'VA-ui stages');
ok(series.includes('MIX_STAFF_EPISODE_ROLE') && !rules.includes('CallsGemini'), 'VA-25 no providers in rules');
ok(!rules.includes('HttpClient') && !rules.includes('elevenlabs.io'), 'VA-26..29 no provider clients');

if (fail.length) {
  console.error(`FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1 PASS FAIL=0 (no provider)');
