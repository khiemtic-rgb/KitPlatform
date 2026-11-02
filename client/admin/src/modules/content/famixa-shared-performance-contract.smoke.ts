/** FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1 Phase A — TEST only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { lockLinePerformance } from './content-famixa-acting-law';
import {
  bindPerformanceSnapshot,
  canonicalPerformanceJson,
  freezePerformanceSnapshot,
  isValidPerformanceHash,
  PERFORMANCE_CONTRACT_DOCUMENT_ID,
  PERFORMANCE_HASH_PREFIX,
  performanceHashOf,
  projectPerformanceDraft,
  rejectLegacyHash,
  sourceRefOf,
  validatePerformanceDraft,
  type PerformanceProjectionSource,
  type PerformanceSnapshot,
} from './famixa-shared-performance-contract';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const TEST_BUILD = 'aaaaaaaa-bbbb-cccc-dddd-000000000001';
const TEST_BUILD_B = 'aaaaaaaa-bbbb-cccc-dddd-000000000002';
const store = new Map<string, PerformanceSnapshot>();

const spoken = (over: Partial<PerformanceProjectionSource> = {}): PerformanceProjectionSource => ({
  buildId: TEST_BUILD,
  shotCode: 'TEST-SC01-SH01',
  episodeCode: 'TEST',
  sceneCode: 'SC01',
  characterIds: ['CHAR-001'],
  dialogueSegmentIds: ['L1'],
  lines: [
    {
      lineId: 'L1',
      characterId: 'CHAR-001',
      text: 'Thì sao?',
      performance: lockLinePerformance({ emotion: 'tense', intensity: 3, pace: 'fast', volume: 'mid', pauseSec: 0.12, label: 'căng 3/5' }),
    },
  ],
  actingBeat: {
    before: { action: 'turns', body: 'stands', gaze: 'window', room: 'kitchen', holdSec: 0.2 },
    during: { speech: true, emotion: 'uneasy', intensity: 2, pace: 'natural' },
    after: { gaze: 'down', holdSec: 0.22 },
  },
  ...over,
});

const draftA = projectPerformanceDraft(spoken());
const draftB = projectPerformanceDraft(spoken());
ok(canonicalPerformanceJson(draftA) === canonicalPerformanceJson(draftB), 'T01 projection deterministic');
ok(draftA.shotPerformance.before?.action === 'turns' && draftA.shotPerformance.during?.emotion === 'uneasy' && draftA.lines[0]?.delivery.emotion === 'tense', 'T01 acting + locked line');
ok(!canonicalPerformanceJson(draftA).includes('holdSec') && !canonicalPerformanceJson(draftA).includes('"speech"'), 'T01 holdSec/speech omitted');

const snap1 = freezePerformanceSnapshot(draftA, 'regression', sourceRefOf(spoken(), draftA));
store.set(snap1.performanceSnapshotId, structuredClone(snap1));
ok(Boolean(snap1.performanceSnapshotId), 'T02 snapshot id');
ok(snap1.performanceHash.startsWith(PERFORMANCE_HASH_PREFIX) && snap1.performanceHash.length === PERFORMANCE_HASH_PREFIX.length + 64, 'T03 PERFORMANCE_HASH_V1');

const snap2 = freezePerformanceSnapshot(projectPerformanceDraft(spoken()), 'regression');
ok(snap1.performanceHash === snap2.performanceHash, 'T04 same draft → same hash');
ok(snap1.performanceSnapshotId !== snap2.performanceSnapshotId, 'T05 different SnapshotId');

const mutatedDraft = projectPerformanceDraft(spoken({
  actingBeat: { before: { action: 'walks away', body: 'stands', holdSec: 0.2 }, during: { speech: true, emotion: 'uneasy' }, after: { holdSec: 0.22 } },
}));
ok(mutatedDraft.shotPerformance.before?.action === 'walks away', 'T06 draft mutated');
const kept = store.get(snap1.performanceSnapshotId)!;
ok(kept.payloadJson === snap1.payloadJson && kept.performanceHash === snap1.performanceHash && kept.shotPerformance.before?.action === 'turns', 'T06 snapshot immutable');

ok(performanceHashOf(mutatedDraft) !== snap1.performanceHash, 'T07 different action → different hash');

const deliveryDraft = projectPerformanceDraft(spoken({
  lines: [{
    lineId: 'L1',
    characterId: 'CHAR-001',
    text: 'Thì sao?',
    performance: lockLinePerformance({ emotion: 'hurt', intensity: 2, pace: 'slow', volume: 'low', pauseSec: 0.4, label: 'đau 2/5' }),
  }],
}));
ok(performanceHashOf(deliveryDraft) !== snap1.performanceHash, 'T08 different line delivery → different hash');

ok(draftA.shotPerformance.during?.emotion === 'uneasy' && draftA.lines[0]?.delivery.emotion === 'tense', 'T09 L1 != L3 valid');
ok(validatePerformanceDraft(draftA).ok && validatePerformanceDraft(draftA).voiceReady, 'T09 voice-ready');

const noL1 = projectPerformanceDraft(spoken({
  actingBeat: { before: { action: 'turns', body: 'stands', holdSec: 0.2 }, during: { speech: true }, after: { holdSec: 0.22 } },
}));
ok(!noL1.shotPerformance.during?.emotion && noL1.lines[0]?.delivery.emotion === 'tense', 'T10 L1 does not inherit from first line');

const unlocked = projectPerformanceDraft(spoken({
  lines: [{ lineId: 'L1', characterId: 'CHAR-001', text: 'Thì sao?', performance: { emotion: 'tense', intensity: 3, pace: 'fast', volume: 'mid', pauseSec: 0.12, label: 'căng 3/5', locked: false } }],
}));
ok(validatePerformanceDraft(unlocked).ok && !validatePerformanceDraft(unlocked).voiceReady, 'T11 not voice-ready');
let unlockedFreeze = false;
try { freezePerformanceSnapshot(unlocked, 'regression'); } catch (e) { unlockedFreeze = e instanceof Error && e.message === 'SPOKEN_LINE_UNLOCKED'; }
ok(unlockedFreeze, 'T11 unlocked cannot freeze');

const orphan = projectPerformanceDraft(spoken({
  characterIds: ['CHAR-001'],
  lines: [{
    lineId: 'L1',
    characterId: 'CHAR-099',
    text: 'Thì sao?',
    performance: lockLinePerformance({ emotion: 'tense', intensity: 3, pace: 'fast', volume: 'mid', pauseSec: 0.12, label: 'căng 3/5' }),
  }],
}));
ok(validatePerformanceDraft(orphan).issues.some((i) => i.code === 'INVALID_CHARACTER'), 'T12 invalid characterId');

const dup = projectPerformanceDraft(spoken({
  characterIds: ['CHAR-001', 'CHAR-003'],
  primaryCharacterIds: ['CHAR-001', 'CHAR-003'],
}));
ok(validatePerformanceDraft(dup).issues.some((i) => i.code === 'DUPLICATE_PRIMARY'), 'T13 duplicate PRIMARY');

let crossShot = false;
try { bindPerformanceSnapshot(snap1, TEST_BUILD, 'TEST-SC01-SH02'); } catch (e) { crossShot = e instanceof Error && e.message === 'CROSS_SHOT_FORBIDDEN'; }
ok(crossShot, 'T14 cross-shot rejected');

let crossBuild = false;
try { bindPerformanceSnapshot(snap1, TEST_BUILD_B, snap1.shotCode); } catch (e) { crossBuild = e instanceof Error && e.message === 'CROSS_SCOPE_FORBIDDEN'; }
ok(crossBuild, 'T15 cross-build rejected');

ok(validatePerformanceDraft(draftA, { runway: 'gen4_turbo' }).issues.some((i) => i.code === 'PROVIDER_FIELD_FORBIDDEN'), 'T16 provider field rejected');
ok(validatePerformanceDraft(draftA, { voiceId: 'eleven-xyz' }).issues.some((i) => i.code === 'VOICE_ID_FORBIDDEN'), 'T17 VoiceId rejected');
ok(validatePerformanceDraft(draftA, { providerDurationSec: 5 }).issues.some((i) => i.code === 'PROVIDER_DURATION_FORBIDDEN'), 'T18 provider duration rejected');

ok(canonicalPerformanceJson(draftA) === canonicalPerformanceJson(projectPerformanceDraft(spoken())), 'T19 canon deterministic');
ok(isValidPerformanceHash(snap1.performanceHash), 'T19 hash prefix valid');

ok(rejectLegacyHash('f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17').length === 1, 'T20 ActingHash rejected');
ok(rejectLegacyHash('a1b2c3d4').length === 1 && !isValidPerformanceHash('a1b2c3d4'), 'T21 fingerprint rejected');

store.set(snap1.performanceSnapshotId, structuredClone(snap1));
const reload = store.get(snap1.performanceSnapshotId)!;
ok(reload.payloadJson === snap1.payloadJson && reload.performanceHash === snap1.performanceHash, 'T22 reload identical');

projectPerformanceDraft(spoken({
  actingBeat: { before: { action: 'turns', body: 'stands', gaze: 'camera', holdSec: 0.2 }, during: { speech: true, emotion: 'uneasy' }, after: { holdSec: 0.22 } },
}));
const afterGraph = store.get(snap1.performanceSnapshotId)!;
ok(afterGraph.payloadJson === snap1.payloadJson && !afterGraph.payloadJson.includes('camera'), 'T23 snapshot survives graph mutation');

const twoFace = projectPerformanceDraft(spoken({
  characterIds: ['CHAR-001', 'CHAR-003'],
  primaryCharacterIds: ['CHAR-001'],
  secondaryCharacterIds: ['CHAR-003'],
}));
ok(twoFace.characters.length === 2 && twoFace.characters.filter((c) => c.speechRole === 'SPEAKER').length === 1, 'T24 multi-character one speaker');
freezePerformanceSnapshot(twoFace, 'regression');

const multi = projectPerformanceDraft(spoken({
  characterIds: ['CHAR-001', 'CHAR-003'],
  primaryCharacterIds: ['CHAR-001'],
  secondaryCharacterIds: ['CHAR-003'],
  dialogueSegmentIds: ['L1', 'L2'],
  lines: [
    spoken().lines[0]!,
    {
      lineId: 'L2',
      characterId: 'CHAR-003',
      text: 'Con biết rồi.',
      performance: lockLinePerformance({ emotion: 'hurt', intensity: 2, pace: 'slow', volume: 'low', pauseSec: 0.4, label: 'đau 2/5' }),
    },
  ],
}));
ok(multi.characters.filter((c) => c.speechRole === 'SPEAKER').length === 2, 'T25 multi-speaker snapshot may exist');
freezePerformanceSnapshot(multi, 'regression');

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const video = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionActionContracts.cs');
ok(video.includes('["ApprovedKeyframe", "Story", "ActingBeat", "VisualContract", "Timing", "FrozenProviderModel", "PerformanceSnapshot"]'), 'REGRESSION CREATE_VIDEO requires PerformanceSnapshot');
ok(video.includes('["LineText", "VoiceId", "LineId", "CharacterId", "PerformanceSnapshot"]'), 'REGRESSION CREATE_VOICE requires PerformanceSnapshot');
ok(video.includes('["MotionArtifactId", "VoiceArtifactId", "FrozenProviderModel", "PerformanceSnapshot"]'), 'REGRESSION CREATE_LIPSYNC requires PerformanceSnapshot');
const cutover = read('famixa-production-os-ui-cutover.smoke.ts');
ok(cutover.includes('FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1'), 'REGRESSION UI cutover suite still present');
const contract = read('famixa-shared-performance-contract.ts');
ok(!contract.includes('gen4_turbo') && !contract.includes('eleven_v3') && !contract.includes('runway.com'), 'no provider execution in contract module');
ok(draftA.documentId === PERFORMANCE_CONTRACT_DOCUMENT_ID, 'document id');

if (fail.length) {
  console.error(`FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1_PHASE_A FAIL=${fail.length}\n${fail.join('\n')}`);
  process.exit(1);
}
console.log('FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1_PHASE_A PASS FAIL=0 (no provider)');
