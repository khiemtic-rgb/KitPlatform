/** FAMIXA_VOICE_PHYSICAL_IDENTITY_CONTRACT_V1 — T1–T17. Isolated memory store. 0 providers. 0 production writes. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  LIPSYNC_EXECUTION_INPUT_FREEZE_ID,
  freezeLipSyncExecutionInput,
  loadFrozenVoicePhysicalBytes,
  submitLipSyncFrozenExecution,
} from './famixa-lipsync-execution-input-freeze';
import {
  VOICE_PHYSICAL_IDENTITY_ID,
  VOICE_PHYSICAL_KEY_PREFIX,
  VOICE_PHYSICAL_NOT_FOUND,
  createMemoryVoicePhysicalStore,
  deleteVoicePhysicalArtifact,
  findVoicePhysicalRefsInStoreKeys,
  hasVoicePhysicalArtifact,
  loadVoicePhysicalArtifact,
  mintVoicePhysicalArtifactId,
  parseVoicePhysicalStorageKey,
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

async function main() {
  const store = createMemoryVoicePhysicalStore();
  const keyA1 = voicePhysicalStorageKey(A);
  const keyA2 = voicePhysicalStorageKey({ ...A, fingerprint: 'CHANGED', durationSec: 9.99 });
  ok(keyA1 === keyA2, 'T1 same identity → same key');
  ok(keyA1 === `${VOICE_PHYSICAL_KEY_PREFIX}:TEST_BUILD:TEST-LINE-001:TEST-VOICE-001`, 'T1 key format');
  ok(!keyA1.includes(A.fingerprint) && !keyA1.includes(String(A.durationSec)), 'T6/T7 fingerprint+duration not in key');
  const parsed = parseVoicePhysicalStorageKey(keyA1);
  ok(parsed?.buildId === 'TEST_BUILD' && parsed.artifactId === 'TEST-LINE-001' && parsed.voiceId === 'TEST-VOICE-001', 'parse canonical key');
  const minted = mintVoicePhysicalArtifactId(new Date('2026-09-17T02:00:00.000Z'));
  ok(minted.startsWith('vpa-20260917T020000Z-') && minted !== 'vpa-20260915T161911Z-e0b3093f', 'mint is new vpa, not SH01 historical');
  ok(
    findVoicePhysicalRefsInStoreKeys([keyA1], { buildId: 'TEST_BUILD', voiceId: 'TEST-VOICE-001' })?.artifactId ===
      'TEST-LINE-001',
    'find unique stored ref',
  );
  ok(
    findVoicePhysicalRefsInStoreKeys([keyA1, `${VOICE_PHYSICAL_KEY_PREFIX}:TEST_BUILD:OTHER:TEST-VOICE-001`], {
      buildId: 'TEST_BUILD',
      voiceId: 'TEST-VOICE-001',
    }) == null,
    'find refuses ambiguous refs',
  );

  const saved = await saveVoicePhysicalArtifact(A, BYTES_A, store);
  ok(saved.storageKey === keyA1, 'T2 writer uses canonical key');
  const loaded = await loadVoicePhysicalArtifact(A, store);
  ok(loaded !== VOICE_PHYSICAL_NOT_FOUND && sameVoicePhysicalBytes(loaded, BYTES_A), 'T2/T3 reader same key + exact bytes');
  ok(await hasVoicePhysicalArtifact(A, store), 'T3 has after save');

  await saveVoicePhysicalArtifact(B, BYTES_B, store);
  const loadB = await loadVoicePhysicalArtifact(B, store);
  const loadA = await loadVoicePhysicalArtifact(A, store);
  ok(
    loadA !== VOICE_PHYSICAL_NOT_FOUND &&
      loadB !== VOICE_PHYSICAL_NOT_FOUND &&
      sameVoicePhysicalBytes(loadA, BYTES_A) &&
      sameVoicePhysicalBytes(loadB, BYTES_B) &&
      !sameVoicePhysicalBytes(loadA, loadB),
    'T4 A bytes never resolve as B',
  );

  await saveVoicePhysicalArtifact(A_VOICE2, BYTES_A2, store);
  const v1 = await loadVoicePhysicalArtifact(A, store);
  const v2 = await loadVoicePhysicalArtifact(A_VOICE2, store);
  ok(
    v1 !== VOICE_PHYSICAL_NOT_FOUND &&
      v2 !== VOICE_PHYSICAL_NOT_FOUND &&
      sameVoicePhysicalBytes(v1, BYTES_A) &&
      sameVoicePhysicalBytes(v2, BYTES_A2),
    'T5 same line different voice isolated',
  );

  ok(voicePhysicalStorageKey(A) === voicePhysicalStorageKey({ ...A, fingerprint: 'NOPE' }), 'T6 fingerprint not lookup');
  ok(voicePhysicalStorageKey(A) === voicePhysicalStorageKey({ ...A, durationSec: 4.24 }), 'T7 duration not identity');
  ok(voicePhysicalStorageKey(A) !== voicePhysicalStorageKey(B), 'T7 same duration still separate');

  const missing = await loadVoicePhysicalArtifact(
    { artifactId: 'NO-SUCH', buildId: 'TEST_BUILD', lineId: 'NO-SUCH', voiceId: 'TEST-VOICE-001' },
    store,
  );
  ok(missing === VOICE_PHYSICAL_NOT_FOUND, 'T9 canonical miss is NOT_FOUND');
  const keys = await store.keys();
  ok(!keys.some((k) => k.includes('FAMIXA:tts') || k.includes('text:')), 'T9 no legacy key search');
  ok(voicePhysicalIdentityOf({ artifactId: A.artifactId }) == null, 'T8 incomplete identity does not invent latest');

  ok(sameVoicePhysicalBytes((await loadVoicePhysicalArtifact(A, store)) as Uint8Array, BYTES_A), 'T10 artifactId+identity → exact bytes');

  await deleteVoicePhysicalArtifact(A, store);
  ok(!(await hasVoicePhysicalArtifact(A, store)), 'T11 A deleted');
  ok(
    (await hasVoicePhysicalArtifact(B, store)) &&
      sameVoicePhysicalBytes((await loadVoicePhysicalArtifact(B, store)) as Uint8Array, BYTES_B),
    'T11 B untouched',
  );

  const v2Store = createMemoryVoicePhysicalStore();
  await saveVoicePhysicalArtifact(A, BYTES_A, v2Store);
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
  const frozenBytes = await loadFrozenVoicePhysicalBytes(frozen, v2Store);
  ok(sameVoicePhysicalBytes(frozenBytes, BYTES_A), 'T10 V2 frozen.voiceArtifactId → exact bytes');

  await submitLipSyncFrozenExecution({
    frozen: {
      ...frozen,
      motionArtifactId: 'mot:TEST:1',
    },
    run: {
      status: 'reviewed',
      runwayAttempts: [{ n: 1, at: '2026-01-01T00:00:00.000Z', status: 'SUCCEEDED', taskId: 't1', outputUrl: 'https://take/1.mp4' }],
    },
    shotId: 'TEST',
    persistAttempt: () => undefined,
    loadVoiceBytes: async () => new Blob([await loadFrozenVoicePhysicalBytes(frozen, v2Store)]),
    startProvider: (input) => {
      ok(input.voiceArtifactId === A.artifactId, 'V2 provider receives frozen voiceArtifactId');
      return { ok: true };
    },
  });

  const root = dirname(fileURLToPath(import.meta.url));
  const src = readFileSync(join(root, 'famixa-voice-physical-identity.ts'), 'utf8');
  const freezeSrc = readFileSync(join(root, 'famixa-lipsync-execution-input-freeze.ts'), 'utf8');
  const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
  const lipsyncFn = series.slice(series.indexOf('const startLipsync'), series.indexOf('const attachLipsync'));
  const afterFreeze = lipsyncFn.slice(lipsyncFn.indexOf('freezeLipSyncExecutionInput'));
  ok(VOICE_PHYSICAL_IDENTITY_ID === 'FAMIXA_VOICE_PHYSICAL_IDENTITY_CONTRACT_V1', 'id');
  ok(LIPSYNC_EXECUTION_INPUT_FREEZE_ID.startsWith('FAMIXA_LIPSYNC'), 'freeze id');
  ok(!/elevenlabs|ElevenLabs|previewContentSeriesTts/.test(src), 'T12/T13 helper has no ElevenLabs');
  ok(!/startContentSeriesLipsync|fal\.ai/.test(src), 'T14 helper has no Fal');
  ok(!src.includes('persistState') && !src.includes('indexedDB'), 'T15/T16 helper has no graph/IDB');
  ok(!src.includes('new Blob') && !src.includes('assembleContentSeriesCut'), 'T17 helper writes no media');
  ok(afterFreeze.includes('loadFrozenVoicePhysicalBytes') && !afterFreeze.includes('loadTtsBlob('), 'V2 send uses physical contract');
  ok(!afterFreeze.includes('lastSuccessMotionTaskId') && !afterFreeze.includes('resolveLineAudio'), 'T8 no latest/resolveLineAudio after freeze');
  ok(freezeSrc.includes('VOICE_PHYSICAL_NOT_FOUND') && freezeSrc.includes('loadVoicePhysicalArtifact'), 'V2 asks identity→bytes');

  if (fail.length) {
    console.error(`FAMIXA_VOICE_PHYSICAL_IDENTITY_CONTRACT_V1 FAIL ${fail.length}\n${fail.map((x) => `- ${x}`).join('\n')}`);
    process.exit(1);
  }
  console.log('FAMIXA_VOICE_PHYSICAL_IDENTITY_CONTRACT_V1 T1–T17 PASS');
}

void main();
