/** FAMIXA_SH01_VOICE_GRAPH_REBIND_V1 — restore existing physical Voice ref. 0 providers. */

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { FamixaVoiceAsset, SeriesPilotState, SeriesShotRun } from './content-famixa-series';
import {
  mergeRemotePilot,
  mergeVoiceAssets,
  rebindVoicePhysicalGraphRef,
  slimPilotForStorage,
  voiceAssetPhysicalArtifactId,
} from './content-famixa-series';
import {
  freezeLipSyncExecutionInput,
  loadFrozenVoicePhysicalBytes,
  resolveAuthoritativeLipSyncMotionSource,
  resolveAuthoritativeLipSyncVoiceSource,
} from './famixa-lipsync-execution-input-freeze';
import {
  VOICE_PHYSICAL_NOT_FOUND,
  createMemoryVoicePhysicalStore,
  loadVoicePhysicalArtifact,
  sameVoicePhysicalBytes,
  saveVoicePhysicalArtifact,
  voicePhysicalStorageKey,
} from './famixa-voice-physical-identity';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const BUILD = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
const SHOT = 'EP01-SC01-SH01';
const LINE = 'line-SC01-CHAR-003-1';
const OTHER = 'line-SC01-CHAR-001-2';
const VOICE = 'kOIUKJ9E0DPndqTtvJm5';
const ARTIFACT = 'vpa-20260915T161911Z-e0b3093f';
const STORAGE = `FAMIXA:voice:${BUILD}:${ARTIFACT}:${VOICE}`;
const MOTION_55 = 'mot:EP01-SC01-SH01:55';
const TASK_55 = 'veo_fixture_take_55';
const TASK_56 = 'veo_fixture_take_56';
const TAKE_55 = 'https://take/55.mp4';
const TAKE_56 = 'https://take/56.mp4';
const BYTES = new Uint8Array([73, 68, 51, 4, 2, 4, 8, 16, 32, 64, 128, 7, 9, 11]);
const SHA = createHash('sha256').update(BYTES).digest('hex').toUpperCase();

const durationOnly: FamixaVoiceAsset = {
  lineId: LINE,
  status: 'ready',
  duration: 4.24,
  characterId: 'CHAR-003',
};
const otherLine: FamixaVoiceAsset = {
  lineId: OTHER,
  status: 'ready',
  duration: 2.48,
  characterId: 'CHAR-001',
};

function fixtureRun(): SeriesShotRun {
  return {
    status: 'reviewed',
    videoApproved: true,
    acceptedTake: { n: 56, url: TAKE_56, acceptedAt: '2026-09-14T15:26:20.000Z' },
    takeUrl: TAKE_56,
    previewUrl: TAKE_56,
    turboTaskId: TASK_56,
    lastSuccessMotionTaskId: TASK_56,
    runwayAttempts: [
      {
        n: 55,
        at: '2026-09-11T15:12:39.000Z',
        taskId: TASK_55,
        status: 'SUCCEEDED',
        resultClass: 'CURRENT',
        outputUrl: TAKE_55,
        duration: 5,
        fingerprint: 'f64e90d',
      },
      {
        n: 56,
        at: '2026-09-12T03:36:47.000Z',
        taskId: TASK_56,
        status: 'SUCCEEDED',
        resultClass: 'LATE_RESULT',
        outputUrl: TAKE_56,
        duration: 6,
        fingerprint: 'late-56',
      },
    ],
    lipsyncFrozenInput: {
      motionArtifactId: MOTION_55,
      motionFingerprint: 'f64e90d',
      voiceFingerprint: '17dbe460',
      fingerprint: '4d7522f7',
    },
  };
}

function episodeOf(): SeriesPilotState['episode'] {
  return {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP01',
    title: 'Tập 01',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [{ id: SHOT, scene: 'SC01', code: 'SH01', title: 'SH01', action: '', status: 'story_locked' }],
  };
}

function stateOf(voiceAssets: Record<string, FamixaVoiceAsset>): SeriesPilotState {
  return {
    buildId: BUILD,
    roles: [],
    runs: { [SHOT]: fixtureRun() },
    shorts: [],
    stills: [],
    episode: episodeOf(),
    schemaVersion: 1,
    characters: [{ id: 'CHAR-003', name: 'Linh', voiceId: VOICE }],
    scenes: [],
    lines: [],
    parseWarnings: [],
    parseVersion: 0,
    storyVersion: 0,
    storyReviewed: true,
    voiceAssets,
  };
}

const store = createMemoryVoicePhysicalStore();
await saveVoicePhysicalArtifact({ artifactId: ARTIFACT, buildId: BUILD, lineId: LINE, voiceId: VOICE }, BYTES, store);
const loaded = await loadVoicePhysicalArtifact(
  { artifactId: ARTIFACT, buildId: BUILD, lineId: LINE, voiceId: VOICE },
  store,
);
ok(loaded !== VOICE_PHYSICAL_NOT_FOUND, 'T1 physical artifact exists');
ok(loaded !== VOICE_PHYSICAL_NOT_FOUND && loaded.length === BYTES.length, 'T2 exact byte length');
ok(loaded !== VOICE_PHYSICAL_NOT_FOUND && sameVoicePhysicalBytes(loaded, BYTES), 'T2 exact bytes');
ok(
  loaded !== VOICE_PHYSICAL_NOT_FOUND &&
    createHash('sha256').update(loaded).digest('hex').toUpperCase() === SHA,
  'T2 exact SHA256',
);

const before = stateOf({ [LINE]: durationOnly, [OTHER]: otherLine });
ok(!voiceAssetPhysicalArtifactId(LINE, before.voiceAssets?.[LINE]), 'preflight duration-only has no physical id');
const after = rebindVoicePhysicalGraphRef(before, LINE, {
  artifactId: ARTIFACT,
  storageKey: STORAGE,
  voiceId: VOICE,
});
ok(after.voiceAssets?.[LINE]?.artifactId === ARTIFACT, 'T3 graph artifactId restored');
ok(after.voiceAssets?.[LINE]?.storageKey === STORAGE, 'T4 graph storageKey restored');
ok(after.voiceAssets?.[LINE]?.physicalArtifactResolvable === true, 'T5 physicalArtifactResolvable true');
ok(after.voiceAssets?.[LINE]?.lineId === LINE, 'preserve lineId');
ok(after.voiceAssets?.[LINE]?.status === 'ready', 'preserve status');
ok(after.voiceAssets?.[LINE]?.duration === 4.24, 'preserve duration');
ok(after.voiceAssets?.[LINE]?.characterId === 'CHAR-003', 'preserve characterId');
ok(after.voiceAssets?.[OTHER]?.artifactId == null, 'other line unchanged');
ok(after.voiceAssets?.[OTHER]?.duration === 2.48, 'other line duration unchanged');
ok(rebindVoicePhysicalGraphRef(before, LINE, { artifactId: LINE, storageKey: STORAGE, voiceId: VOICE }) === before, 'refuse lineId as artifactId');

const src = resolveAuthoritativeLipSyncVoiceSource({
  lineIds: [LINE],
  voiceFingerprint: '17dbe460',
  durationByLineId: { [LINE]: 4.24 },
  voiceAssets: after.voiceAssets,
  voiceId: VOICE,
});
ok(src?.artifactId === ARTIFACT, 'T6 resolver returns real artifactId');
ok(src?.artifactId !== LINE, 'T6 never lineId');
ok(src?.voiceId === VOICE, 'T6 voiceId');

const frozen = freezeLipSyncExecutionInput({
  motionArtifactId: MOTION_55,
  motionFingerprint: 'f64e90d',
  motionDurationSec: 5,
  voiceArtifactId: src!.artifactId,
  voiceFingerprint: '17dbe460',
  voiceDurationSec: 4.24,
  fingerprint: '4d7522f7',
  voiceLineIds: [LINE],
  voiceDurationByLineId: { [LINE]: 4.24 },
  providerId: 'fal',
  modelId: 'v3',
  buildId: BUILD,
  voiceId: src!.voiceId,
});
const frozenBytes = await loadFrozenVoicePhysicalBytes(frozen, store);
ok(sameVoicePhysicalBytes(frozenBytes, BYTES), 'T7 frozen Voice resolution returns real artifact');
ok(voicePhysicalStorageKey({ artifactId: frozen.voiceArtifactId, buildId: BUILD, lineId: LINE, voiceId: VOICE }) === STORAGE, 'T7 canonical key');

const wrongLine = await loadVoicePhysicalArtifact(
  { artifactId: OTHER, buildId: BUILD, lineId: OTHER, voiceId: VOICE },
  store,
);
ok(wrongLine === VOICE_PHYSICAL_NOT_FOUND, 'T8 wrong line cannot resolve');
const wrongVoice = await loadVoicePhysicalArtifact(
  { artifactId: ARTIFACT, buildId: BUILD, lineId: LINE, voiceId: 'WRONG-VOICE' },
  store,
);
ok(wrongVoice === VOICE_PHYSICAL_NOT_FOUND, 'T9 wrong voiceId cannot resolve');

const motion = resolveAuthoritativeLipSyncMotionSource({
  run: after.runs[SHOT],
  shotId: SHOT,
  motionFingerprint: 'f64e90d',
});
ok(motion?.artifactId === MOTION_55 && motion.n === 55, 'T10 Motion remains Take 55');
ok(after.runs[SHOT]?.acceptedTake?.n === 56, 'T15 acceptedTake 56 untouched');
ok(after.runs[SHOT]?.videoApproved === true, 'T14 videoApproved untouched');

const remDuration = stateOf({ [LINE]: durationOnly, [OTHER]: otherLine });
const locBound = after;
const merged = mergeRemotePilot(remDuration, locBound);
ok(merged.voiceAssets?.[LINE]?.artifactId === ARTIFACT, 'hydrate merge keeps local artifactId');
ok(merged.voiceAssets?.[LINE]?.storageKey === STORAGE, 'hydrate merge keeps local storageKey');
ok(merged.voiceAssets?.[LINE]?.physicalArtifactResolvable === true, 'hydrate merge keeps resolvable');
ok(merged.voiceAssets?.[OTHER]?.duration === 2.48, 'hydrate merge keeps other line');
ok(merged.runs[SHOT]?.acceptedTake?.n === 56, 'hydrate merge does not change acceptedTake');
ok(merged.runs[SHOT]?.videoApproved === true, 'hydrate merge does not change videoApproved');

const slim = slimPilotForStorage(after);
ok(slim.voiceAssets?.[LINE]?.artifactId === ARTIFACT, 'slimPilotForStorage keeps artifactId');
ok(slim.voiceAssets?.[LINE]?.storageKey === STORAGE, 'slimPilotForStorage keeps storageKey');

const dropped = mergeVoiceAssets({ [LINE]: durationOnly }, { [LINE]: after.voiceAssets![LINE]! });
ok(dropped?.[LINE]?.artifactId === ARTIFACT, 'mergeVoiceAssets restores from local');
ok(voiceAssetPhysicalArtifactId(LINE, { ...durationOnly, artifactId: LINE }) == null, 'lineId-as-artifactId refused');

const here = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(here, 'content-famixa-series.ts'), 'utf8');
const freeze = readFileSync(join(here, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8');
ok(series.includes('voiceAssets: sameEp ? mergeVoiceAssets(graph.voiceAssets, local.voiceAssets)'), 'T11 mergeRemotePilot keeps physical Voice');
ok(!series.includes('elevenlabs') && !freeze.includes('startContentSeriesLipsync'), 'T11 zero provider calls in this helper');
ok(!series.includes('vpa-') || series.includes(ARTIFACT) === false, 'T12 helper does not mint SH01 artifactId');
ok(after.runs[SHOT]?.lipsyncFrozenInput?.motionArtifactId === MOTION_55, 'T13 no Final/Motion rewrite');
ok(before.runs[SHOT]?.videoApproved === after.runs[SHOT]?.videoApproved, 'T14 no videoApproved changes');
ok(before.runs[SHOT]?.acceptedTake?.n === after.runs[SHOT]?.acceptedTake?.n, 'T15 no acceptedTake changes');

if (fail.length) {
  console.error('FAMIXA_SH01_VOICE_GRAPH_REBIND_V1 FAIL');
  for (const row of fail) console.error(' -', row);
  process.exit(1);
}
console.log('FAMIXA_SH01_VOICE_GRAPH_REBIND_V1 PASS');
