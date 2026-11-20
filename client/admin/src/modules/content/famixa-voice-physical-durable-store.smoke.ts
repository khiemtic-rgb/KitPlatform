/** FAMIXA_VOICE_PHYSICAL_ARTIFACT_DURABLE_STORE_V1 — T1–T18. Isolated file store. 0 providers. 0 production writes. */

import { existsSync, readFileSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { freezeLipSyncExecutionInput, loadFrozenVoicePhysicalBytes } from './famixa-lipsync-execution-input-freeze';
import { createFileVoicePhysicalStore } from './famixa-voice-physical-file-store';
import {
  VOICE_PHYSICAL_KEY_PREFIX,
  VOICE_PHYSICAL_NOT_FOUND,
  hasVoicePhysicalArtifact,
  loadVoicePhysicalArtifact,
  sameVoicePhysicalBytes,
  saveVoicePhysicalArtifact,
  voicePhysicalIdentityOf,
  voicePhysicalStorageKey,
} from './famixa-voice-physical-identity';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const A = {
  artifactId: 'TEST-LINE-001',
  buildId: 'TEST_BUILD',
  lineId: 'TEST-LINE-001',
  voiceId: 'TEST-VOICE-001',
  fingerprint: 'TEST-FP-001',
  durationSec: 1.23,
};
const B = {
  artifactId: 'TEST-LINE-002',
  buildId: 'TEST_BUILD',
  lineId: 'TEST-LINE-002',
  voiceId: 'TEST-VOICE-002',
  fingerprint: 'TEST-FP-002',
  durationSec: 1.23,
};
const A_VOICE2 = { ...A, voiceId: 'TEST-VOICE-002', fingerprint: 'TEST-FP-OTHER' };
const BYTES_A = new Uint8Array([9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 42]);
const BYTES_B = new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 99]);
const BYTES_A2 = new Uint8Array([11, 22, 33, 44, 55]);

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..', '..', '..', '..');
const rootDir = join(repoRoot, '.tmp', 'famixa-voice-physical-durable-v1');

async function main() {
  ok(rootDir.replace(/\\/g, '/').includes('.tmp/famixa-voice-physical-durable-v1'), 'isolated .tmp root');
  ok(!rootDir.replace(/\\/g, '/').includes('App_Data') && A.buildId === 'TEST_BUILD', 'T12 test path is not production');
  if (existsSync(rootDir)) rmSync(rootDir, { recursive: true, force: true });

  const keyA = voicePhysicalStorageKey(A);
  ok(keyA === `${VOICE_PHYSICAL_KEY_PREFIX}:TEST_BUILD:TEST-LINE-001:TEST-VOICE-001`, 'T1 deterministic canonical key');
  ok(keyA === voicePhysicalStorageKey({ ...A, fingerprint: 'CHANGED', durationSec: 9.99 }), 'T1 fingerprint+duration ignored');

  let store = createFileVoicePhysicalStore(rootDir);
  const saved = await saveVoicePhysicalArtifact(A, BYTES_A, store);
  ok(saved.storageKey === keyA, 'T2 writer uses canonical key');
  const loaded = await loadVoicePhysicalArtifact(A, store);
  ok(loaded !== VOICE_PHYSICAL_NOT_FOUND && sameVoicePhysicalBytes(loaded, BYTES_A), 'T2 save → load exact bytes');

  store = undefined as unknown as typeof store;
  const fresh = createFileVoicePhysicalStore(rootDir);
  const afterRecreate = await loadVoicePhysicalArtifact(A, fresh);
  ok(
    afterRecreate !== VOICE_PHYSICAL_NOT_FOUND && sameVoicePhysicalBytes(afterRecreate, BYTES_A),
    'T3/T11 new store instance → exact same bytes',
  );
  ok(await hasVoicePhysicalArtifact(A, fresh), 'T3 has after recreation');

  await saveVoicePhysicalArtifact(B, BYTES_B, fresh);
  const loadA = await loadVoicePhysicalArtifact(A, fresh);
  const loadB = await loadVoicePhysicalArtifact(B, fresh);
  ok(
    loadA !== VOICE_PHYSICAL_NOT_FOUND &&
      loadB !== VOICE_PHYSICAL_NOT_FOUND &&
      sameVoicePhysicalBytes(loadA, BYTES_A) &&
      sameVoicePhysicalBytes(loadB, BYTES_B) &&
      !sameVoicePhysicalBytes(loadA, loadB),
    'T4 artifact A cannot resolve artifact B',
  );
  ok(voicePhysicalStorageKey(A) !== voicePhysicalStorageKey(B), 'T5 same duration does not collide');

  await saveVoicePhysicalArtifact(A_VOICE2, BYTES_A2, fresh);
  const v1 = await loadVoicePhysicalArtifact(A, fresh);
  const v2 = await loadVoicePhysicalArtifact(A_VOICE2, fresh);
  ok(
    v1 !== VOICE_PHYSICAL_NOT_FOUND &&
      v2 !== VOICE_PHYSICAL_NOT_FOUND &&
      sameVoicePhysicalBytes(v1, BYTES_A) &&
      sameVoicePhysicalBytes(v2, BYTES_A2),
    'T6 same line different voiceId isolated',
  );

  ok(voicePhysicalStorageKey(A) === voicePhysicalStorageKey({ ...A, fingerprint: 'NOPE' }), 'T7 fingerprint is metadata');
  ok(voicePhysicalIdentityOf({ artifactId: A.artifactId }) == null, 'T8 incomplete identity does not invent latest');

  const missing = await loadVoicePhysicalArtifact(
    { artifactId: 'NO-SUCH', buildId: 'TEST_BUILD', lineId: 'NO-SUCH', voiceId: 'TEST-VOICE-001' },
    fresh,
  );
  ok(missing === VOICE_PHYSICAL_NOT_FOUND, 'T9 canonical miss returns NOT_FOUND');
  const keys = await fresh.keys();
  ok(
    keys.every((k) => k.startsWith(`${VOICE_PHYSICAL_KEY_PREFIX}:`)) &&
      !keys.some((k) => k.includes('FAMIXA:tts') || k.includes('text:') || k.includes('latest') || k.includes('accepted')),
    'T8/T9 no latest/legacy fallback keys',
  );

  const frozen = freezeLipSyncExecutionInput({
    motionArtifactId: 'mot:TEST:1',
    motionFingerprint: 'mot-fp',
    motionDurationSec: 5,
    voiceArtifactId: A.artifactId,
    voiceFingerprint: A.fingerprint,
    voiceDurationSec: A.durationSec,
    fingerprint: 'lip-fp',
    buildId: A.buildId,
    voiceId: A.voiceId,
    voiceLineIds: [A.lineId],
  });
  const third = createFileVoicePhysicalStore(rootDir);
  const frozenBytes = await loadFrozenVoicePhysicalBytes(frozen, third);
  ok(sameVoicePhysicalBytes(frozenBytes, BYTES_A), 'T10 V2 frozen voice identity resolves exact bytes');

  const idb = readFileSync(join(here, 'famixa-voice-physical-idb-store.ts'), 'utf8');
  const fileStore = readFileSync(join(here, 'famixa-voice-physical-file-store.ts'), 'utf8');
  const identity = readFileSync(join(here, 'famixa-voice-physical-identity.ts'), 'utf8');
  const series = readFileSync(join(here, 'ContentFamixaSeriesTab.tsx'), 'utf8');
  const impl = identity + fileStore + idb;
  ok(!/ElevenLabs|previewContentSeriesTts/.test(impl), 'T13 zero ElevenLabs calls');
  ok(!/startContentSeriesLipsync|fal\.ai|FalClient/.test(impl), 'T14 zero Fal calls');
  ok(!/fetch\(|axios/.test(fileStore + identity), 'T15 file/identity have zero provider calls');
  ok(!identity.includes('persistState') && !fileStore.includes('persistState') && !idb.includes('persistState'), 'T16 zero graph writes');
  ok(fileStore.includes('createFileVoicePhysicalStore') && !fileStore.includes('indexedDB'), 'T17 smoke backend is file store, not IDB');
  ok(!fileStore.includes('saveFinalBlob') && !identity.includes('saveFinalBlob') && fileStore.includes('Not production'), 'T18 zero production media writes');
  ok(A.artifactId === 'TEST-LINE-001' && A.lineId === 'TEST-LINE-001' && A.voiceId === 'TEST-VOICE-001', 'T12 isolated test identity only');
  ok(series.includes('createIdbVoicePhysicalStore') && !series.includes('createMemoryVoicePhysicalStore'), 'browser uses durable IDB backend');
  ok(!series.slice(series.indexOf('const clearSessionTts'), series.indexOf('const voiceIdsForLine')).includes('voicePhysicalStoreRef'), 'clearSessionTts does not wipe durable store');

  rmSync(rootDir, { recursive: true, force: true });

  if (fail.length) {
    console.error(`FAMIXA_VOICE_PHYSICAL_ARTIFACT_DURABLE_STORE_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
    process.exit(1);
  }
  console.log('FAMIXA_VOICE_PHYSICAL_ARTIFACT_DURABLE_STORE_V1 T1–T18 PASS');
}

void main();
