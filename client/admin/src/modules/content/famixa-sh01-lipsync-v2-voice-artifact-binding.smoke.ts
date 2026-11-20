/** FAMIXA_SH01_LIPSYNC_V2_VOICE_ARTIFACT_BINDING_FIX_V1 — T1–T10. Isolated fixture. 0 providers. 0 production writes. */

import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { SeriesShotRun } from './content-famixa-series';
import { lastSuccessMotionTaskId } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  freezeLipSyncExecutionInput,
  loadFrozenVoicePhysicalBytes,
  resolveAuthoritativeLipSyncMotionSource,
  resolveAuthoritativeLipSyncVoiceSource,
  resolveLipSyncMotionFromFrozenInput,
  submitLipSyncFrozenExecution,
  voicePhysicalArtifactIdFromGraph,
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
const VOICE = 'kOIUKJ9E0DPndqTtvJm5';
const ARTIFACT = 'vpa-20260915T161911Z-e0b3093f';
const STORAGE = `FAMIXA:voice:${BUILD}:${ARTIFACT}:${VOICE}`;
const MOTION_55 = 'mot:EP01-SC01-SH01:55';
const TASK_55 = 'veo_fixture_take_55';
const TASK_56 = 'veo_fixture_take_56';
const TAKE_55 = 'https://take/55.mp4';
const TAKE_56 = 'https://take/56.mp4';
const BYTES = new Uint8Array([73, 68, 51, 4, 2, 4, 8, 16, 32, 64, 128, 7, 9, 11]);

function fixtureRun(): SeriesShotRun {
  return {
    status: 'reviewed',
    videoApproved: true,
    acceptedTake: { n: 56, url: TAKE_56, acceptedAt: '2026-09-14T15:26:20.000Z' },
    takeUrl: TAKE_56,
    previewUrl: TAKE_56,
    turboTaskId: TASK_56,
    runwayAttempts: [
      {
        n: 55,
        at: '2026-09-11T15:12:39.000Z',
        taskId: TASK_55,
        status: 'SUCCEEDED',
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

const voiceAssets = {
  [LINE]: {
    lineId: LINE,
    artifactId: ARTIFACT,
    storageKey: STORAGE,
    voiceId: VOICE,
    duration: 4.24,
    physicalArtifactResolvable: true,
  },
};

async function main() {
  const fromGraph = voicePhysicalArtifactIdFromGraph(LINE, voiceAssets);
  ok(fromGraph === ARTIFACT, 'T1 graph artifactId');
  ok(voicePhysicalArtifactIdFromGraph(LINE, { [LINE]: { artifactId: LINE } }) == null, 'T1 refuses lineId-as-artifactId');
  ok(voicePhysicalArtifactIdFromGraph(LINE, {}) == null, 'T1 missing asset does not invent lineId');

  const voiceSrc = resolveAuthoritativeLipSyncVoiceSource({
    lineIds: [LINE],
    voiceFingerprint: '17dbe460',
    voiceAssets,
    voiceId: VOICE,
  });
  ok(voiceSrc?.artifactId === ARTIFACT, 'T1 voiceAssets.artifactId → source.artifactId');
  ok(voiceSrc?.lineId === LINE && voiceSrc.voiceId === VOICE, 'T1 keeps lineId + voiceId');
  ok(voiceSrc?.storageKey === STORAGE, 'T1 keeps storageKey');

  const frozen = freezeLipSyncExecutionInput({
    motionArtifactId: MOTION_55,
    motionFingerprint: 'f64e90d',
    motionDurationSec: 5,
    voiceArtifactId: voiceSrc!.artifactId,
    voiceFingerprint: '17dbe460',
    voiceDurationSec: 4.24,
    fingerprint: '4d7522f7',
    voiceLineIds: voiceSrc!.lineIds,
    buildId: BUILD,
    voiceId: voiceSrc!.voiceId,
  });
  ok(frozen.voiceArtifactId === ARTIFACT, 'T3 frozen.voiceArtifactId == vpa-20260915T161911Z-e0b3093f');
  ok(frozen.voiceArtifactId !== LINE, 'T2 frozen.voiceArtifactId != lineId');
  ok(frozen.voiceId === VOICE && frozen.buildId === BUILD, 'frozen voiceId + buildId');
  ok(voicePhysicalStorageKey({ artifactId: frozen.voiceArtifactId, buildId: BUILD, lineId: LINE, voiceId: VOICE }) === STORAGE, 'canonical key');

  const store = createMemoryVoicePhysicalStore();
  await saveVoicePhysicalArtifact(
    { artifactId: ARTIFACT, buildId: BUILD, lineId: LINE, voiceId: VOICE },
    BYTES,
    store,
  );
  const loaded = await loadVoicePhysicalArtifact(
    { artifactId: frozen.voiceArtifactId, buildId: frozen.buildId!, lineId: LINE, voiceId: frozen.voiceId! },
    store,
  );
  ok(loaded !== VOICE_PHYSICAL_NOT_FOUND && sameVoicePhysicalBytes(loaded, BYTES), 'T4 store load by frozen identity');
  const frozenBytes = await loadFrozenVoicePhysicalBytes(frozen, store);
  ok(sameVoicePhysicalBytes(frozenBytes, BYTES), 'T4 frozen.voiceArtifactId resolves canonical store');
  ok(createHash('sha256').update(frozenBytes).digest('hex').length === 64, 'T4 resolved bytes are hashable');

  const wrongLine = await loadVoicePhysicalArtifact(
    { artifactId: LINE, buildId: BUILD, lineId: LINE, voiceId: VOICE },
    store,
  );
  ok(wrongLine === VOICE_PHYSICAL_NOT_FOUND, 'T5 lineId-as-artifactId cannot resolve');
  const otherLine = await loadVoicePhysicalArtifact(
    { artifactId: ARTIFACT, buildId: BUILD, lineId: 'line-SC01-CHAR-001-2', voiceId: VOICE },
    store,
  );
  ok(otherLine !== VOICE_PHYSICAL_NOT_FOUND, 'T5 lineId is not the storage key — isolation is by artifactId');
  const missingOther = await loadVoicePhysicalArtifact(
    { artifactId: 'line-SC01-CHAR-001-2', buildId: BUILD, lineId: 'line-SC01-CHAR-001-2', voiceId: VOICE },
    store,
  );
  ok(missingOther === VOICE_PHYSICAL_NOT_FOUND, 'T5 wrong line artifact cannot resolve this Voice');

  const wrongVoice = await loadVoicePhysicalArtifact(
    { artifactId: ARTIFACT, buildId: BUILD, lineId: LINE, voiceId: 'WRONG-VOICE' },
    store,
  );
  ok(wrongVoice === VOICE_PHYSICAL_NOT_FOUND, 'T6 wrong voiceId cannot resolve');

  const run = fixtureRun();
  ok(run.acceptedTake?.n === 56, 'fixture acceptedTake=56');
  ok(lastSuccessMotionTaskId(run) === TASK_56, 'T8 lastSuccessMotionTaskId=56');
  const motionSrc = resolveAuthoritativeLipSyncMotionSource({
    run,
    shotId: SHOT,
    motionFingerprint: 'f64e90d',
  });
  ok(motionSrc?.artifactId === MOTION_55 && motionSrc.n === 55 && motionSrc.taskId === TASK_55, 'T7/T8 bound motion stays 55');
  const physical = resolveLipSyncMotionFromFrozenInput(run, SHOT, frozen);
  ok(physical.motionArtifactId === MOTION_55 && physical.taskId === TASK_55 && physical.url === TAKE_55, 'T7 frozen motion is 55');
  ok(physical.taskId !== TASK_56 && physical.url !== TAKE_56, 'T7 acceptedTake 56 does not replace Motion 55');

  const order: string[] = [];
  const provider: { motionArtifactId: string; voiceArtifactId: string; takeTaskId?: string; videoUrl?: string }[] = [];
  await submitLipSyncFrozenExecution({
    frozen,
    run,
    shotId: SHOT,
    persistAttempt: () => {
      order.push('attempt');
    },
    loadVoiceBytes: async () => {
      order.push('voice');
      return new Blob([await loadFrozenVoicePhysicalBytes(frozen, store)]);
    },
    startProvider: (input) => {
      order.push('provider');
      provider.push({
        motionArtifactId: input.motionArtifactId,
        voiceArtifactId: input.voiceArtifactId,
        takeTaskId: input.takeTaskId,
        videoUrl: input.videoUrl,
      });
      return { ok: true, dryRun: true };
    },
  });
  ok(order[0] === 'attempt' && order.indexOf('attempt') < order.indexOf('provider'), 'T9 freeze/attempt before provider');
  ok(provider[0]?.voiceArtifactId === ARTIFACT && provider[0]?.motionArtifactId === MOTION_55, 'T9 provider receives vpa + mot 55');
  ok(provider[0]?.takeTaskId === TASK_55 && provider[0]?.videoUrl === TAKE_55, 'T8 provider not 56');

  const root = dirname(fileURLToPath(import.meta.url));
  const helper = readFileSync(join(root, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8');
  const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
  const lipsyncFn = series.slice(series.indexOf('const startLipsync'), series.indexOf('const attachLipsync'));
  ok(lipsyncFn.includes('voiceAssets: stateRef.current.voiceAssets'), 'startLipsync reads graph voiceAssets');
  ok(lipsyncFn.includes('voiceArtifactId: voiceSrc.artifactId'), 'startLipsync freezes source artifactId');
  ok(!helper.includes('artifactId = lineIds[0]'), 'T1 resolver no longer uses lineIds[0] as artifactId');
  ok(!/startContentSeries(Turbo|Lipsync)|elevenlabs|fal\.ai/.test(helper), 'T10 helper has zero provider calls');

  if (fail.length) {
    console.error(
      `FAMIXA_SH01_LIPSYNC_V2_VOICE_ARTIFACT_BINDING_FIX_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`,
    );
    process.exit(1);
  }
  console.log('FAMIXA_SH01_LIPSYNC_V2_VOICE_ARTIFACT_BINDING_FIX_V1 T1–T10 PASS');
}

void main();
