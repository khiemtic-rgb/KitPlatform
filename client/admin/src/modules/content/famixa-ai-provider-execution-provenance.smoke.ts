/** FAMIXA_AI_PROVIDER_EXECUTION_PROVENANCE_V1 — T1–T30. 0 providers. 0 HTTP. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  adaptMotionAttempts,
  isMotionAttemptSuperseded,
  motionAttemptId,
  motionContentFingerprintOf,
  type ExecutionAttempt,
} from './ContentFamixaShotProduction/ShotProductionExecution';
import { selectFamixaProvider } from './famixa-ai-provider-routing-foundation';
import {
  FAMIXA_EXECUTION_PROVENANCE_ID,
  bindFamixaExecutionProvenance,
  classifyFamixaProvenance,
  motionHowProviderId,
  reconstructFamixaExecutionLineage,
  reconstructFromSelectionSnapshot,
  reconstructMotionArtifactLineage,
  sameProvenanceHow,
  sameProvenanceWhat,
  snapshotFamixaSelection,
  vendorRequestId,
} from './famixa-ai-provider-execution-provenance';

const fail: string[] = [];
const ok = (cond: unknown, label: string) => {
  if (!cond) fail.push(label);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const packInfra = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure');
const packApp = join(root, '../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application');

ok(FAMIXA_EXECUTION_PROVENANCE_ID === 'FAMIXA_AI_PROVIDER_EXECUTION_PROVENANCE_V1', 'suite id');

const decision = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'runway', durationSec: 5 });
const snap = snapshotFamixaSelection(decision);
ok(snap.decisionId === decision.decisionId && snap.providerId === 'runway', 'T1 SelectionDecision snapshot');

const attemptId = motionAttemptId('EP01-SC01-SH01', 1);
const prov = bindFamixaExecutionProvenance({
  attemptId,
  capability: 'MOTION',
  decision,
  shotId: 'EP01-SC01-SH01',
  pictureRevisionId: 'picture:EP01-SC01-SH01:006',
  picturePixelHash: 'pix-a',
  motionContentFingerprint: 'what-a',
  actingBeatFingerprint: 'act-a',
  timingHash: 'time-a',
  providerRequestId: 'runway-task-1',
  artifactId: 'mot:EP01-SC01-SH01:1',
});
ok(prov.decisionId === decision.decisionId, 'T2 DecisionId survives Start bind');
ok(prov.providerId === 'runway', 'T3 ProviderId survives');
ok(prov.modelId === decision.modelId, 'T4 ModelId survives');
ok(prov.attemptId === attemptId, 'T5 AttemptId preserved');
ok(prov.providerRequestId === 'runway-task-1', 'T6 fake provider request persisted');
ok(vendorRequestId('') == null && vendorRequestId(undefined) == null, 'T7/T19 no invented request id');
ok(prov.providerTaskId === 'runway-task-1' && prov.attemptId !== prov.providerRequestId, 'T8 task id ≠ local attempt');

const lineage = reconstructFamixaExecutionLineage(prov);
ok(
  lineage.decision?.decisionId === decision.decisionId &&
    lineage.howProviderId === 'runway' &&
    lineage.whatFingerprint === 'what-a' &&
    lineage.provenance.artifactId === 'mot:EP01-SC01-SH01:1',
  'T9–T11 Artifact→Attempt→Decision→Provider→WHAT',
);

const wan = selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'wan', durationSec: 5 });
const wanProv = bindFamixaExecutionProvenance({
  attemptId: motionAttemptId('EP01-SC01-SH01', 2),
  capability: 'MOTION',
  decision: wan,
  pictureRevisionId: 'picture:EP01-SC01-SH01:006',
  motionContentFingerprint: 'what-a',
  providerRequestId: 'wan_abc',
});
ok(
  sameProvenanceWhat(prov, wanProv) &&
    !sameProvenanceHow(prov, wanProv) &&
    wanProv.attemptId !== prov.attemptId &&
    wanProv.decisionId !== prov.decisionId,
  'T12/T16 switch same WHAT new HOW/attempt/decision',
);

const lateA: ExecutionAttempt = {
  attemptId: motionAttemptId('EP01-SC01-SH01', 1),
  shotId: 'EP01-SC01-SH01',
  stage: 'MOTION',
  n: 1,
  status: 'SUCCESS',
  createdAt: '',
  url: 'https://a.mp4',
  providerRequestId: 'runway-task-1',
};
const currentB: ExecutionAttempt = {
  attemptId: motionAttemptId('EP01-SC01-SH01', 2),
  shotId: 'EP01-SC01-SH01',
  stage: 'MOTION',
  n: 2,
  status: 'SUCCESS',
  createdAt: '',
  url: 'https://b.mp4',
  providerRequestId: 'wan_abc',
};
ok(isMotionAttemptSuperseded([lateA, currentB], lateA), 'T13 Late A superseded by attempt n, not request id');

const retry = bindFamixaExecutionProvenance({
  attemptId: motionAttemptId('EP01-SC01-SH01', 3),
  capability: 'MOTION',
  decision: selectFamixaProvider({ capability: 'MOTION', explicitProviderId: 'runway', durationSec: 5 }),
  pictureRevisionId: 'picture:EP01-SC01-SH01:006',
  motionContentFingerprint: 'what-a',
  providerRequestId: 'runway-task-2',
});
ok(retry.attemptId !== attemptId && sameProvenanceWhat(prov, retry), 'T14–T15 retry new attempt same WHAT');

const regen = bindFamixaExecutionProvenance({
  attemptId: motionAttemptId('EP01-SC01-SH01', 4),
  capability: 'MOTION',
  decision,
  pictureRevisionId: 'picture:EP01-SC01-SH01:007',
  motionContentFingerprint: 'what-b',
});
ok(!sameProvenanceWhat(prov, regen), 'T17 regenerate new WHAT');

ok(classifyFamixaProvenance({ attemptId: 'mot:old:1' }) === 'LEGACY_UNVERIFIED', 'T18 LEGACY_UNVERIFIED');
ok(decision.estimatedCost === 25 && decision.costKind === 'ESTIMATE', 'T20 cost provenance from decision');
ok(wan.estimatedCost == null && wan.costKind === 'FAL_BILLED_ESTIMATE', 'T21 Wan cost null');

const lip = bindFamixaExecutionProvenance({
  attemptId: 'lip:SH01:1',
  capability: 'LIPSYNC',
  decision: selectFamixaProvider({ capability: 'LIPSYNC' }),
  providerRequestId: 'lipsync_fake',
});
ok(lip.providerId === 'fal' && lip.providerRequestId === 'lipsync_fake', 'T22 LipSync provenance');

const voice = bindFamixaExecutionProvenance({
  attemptId: 'voi:SH01:1',
  capability: 'VOICE',
  decision: selectFamixaProvider({ capability: 'VOICE' }),
});
ok(voice.providerId === 'elevenlabs' && voice.providerRequestId == null, 'T23 Voice no invented request id');

const pic = bindFamixaExecutionProvenance({
  attemptId: 'pic:SH01:1',
  capability: 'PICTURE',
  decision: selectFamixaProvider({ capability: 'PICTURE' }),
  pictureRevisionId: 'picture:SH01:006',
  picturePixelHash: 'pix-a',
});
ok(pic.providerId === 'gemini' && pic.picturePixelHash === 'pix-a', 'T24 Picture provenance');

const turbo = readFileSync(join(packInfra, 'ContentSeriesTurboService.cs'), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
ok(turbo.includes('FamixaExecutionProvenanceRules.StampTurbo'), 'Start stamps Decision onto turbo DTO');
ok(series.includes('selectionSnapshot: started.decisionId'), 'Series start persists DecisionId on attempt');
ok(series.includes('lipsyncSelectionSnapshot: started.decisionId'), 'LipSync start persists DecisionId on run');
ok(read('famixa-ai-provider-execution-provenance.ts').includes('Not CURRENT/APPROVED authority'), 'provenance is not authority');
ok(readFileSync(join(packApp, 'FamixaExecutionProvenanceV1.cs'), 'utf8').includes('evidence, not lifecycle authority'), 'C# evidence-only');
ok(read('famixa-ai-provider-execution-provenance.ts').includes("from './famixa-ai-provider-orchestration'"), 'FamixaProviderCapability imported from orchestration');

const whatParts = {
  baseMotionFingerprint: 'base',
  actingBeatFingerprint: 'act-a',
  promptHash: 'p1',
  pixelHash: 'pix-a',
  providerDuration: 5 as const,
  timingHash: 'time-a',
};
const whatHash = motionContentFingerprintOf(whatParts);
ok(!whatHash.includes('runway') && !whatHash.includes('wan'), 'WHAT hash does not contain provider id');
ok(
  motionHowProviderId({ providerId: 'wan' }, 'gen4_turbo') === 'wan',
  'snapshot providerId wins over model prefix',
);

const adapted = adaptMotionAttempts('EP01-SC01-SH01', [
  {
    n: 1,
    at: '2026-09-09T00:00:00.000Z',
    status: 'SUCCEEDED',
    taskId: 'runway-task-1',
    outputUrl: 'https://take/a.mp4',
    attemptId,
    model: 'gen4_turbo',
    selectionSnapshot: snap,
    frozenInput: {
      pictureRevisionId: 'picture:EP01-SC01-SH01:006',
      keyframePixelHash: 'pix-a',
      baseMotionFingerprint: 'base',
      actingBeatFingerprint: 'act-a',
      promptHash: 'p1',
      timingHash: 'time-a',
      provider: 'runway',
      providerDuration: 5,
      executionFingerprint: whatHash,
    },
  },
]);
ok(adapted[0]?.selectionSnapshot?.decisionId === snap.decisionId, 'adapt copies selectionSnapshot');
ok(adapted[0]?.provider === 'runway', 'adapt HOW from snapshot not prefix');

const artifact = {
  artifactId: adapted[0]!.outputArtifactId || 'mot:EP01-SC01-SH01:1',
  attemptId: adapted[0]!.attemptId,
};
const reverse = reconstructMotionArtifactLineage(artifact, adapted);
ok(
  reverse.decision?.decisionId === snap.decisionId &&
    reverse.howProviderId === 'runway' &&
    reverse.howModelId === snap.modelId &&
    reverse.whatFingerprint === whatHash &&
    reverse.provenance.pictureRevisionId === 'picture:EP01-SC01-SH01:006' &&
    reverse.provenance.picturePixelHash === 'pix-a' &&
    reverse.provenance.class === 'VERIFIED',
  'runtime reverse Artifact→Attempt→Decision→Provider→WHAT→Picture',
);

const legacyArt = { artifactId: 'mot:EP01-SC01-SH01:legacy' };
const legacyLineage = reconstructMotionArtifactLineage(legacyArt, [
  { attemptId: 'mot:EP01-SC01-SH01:9', provider: 'runway', providerRequestId: 'old-task' },
]);
ok(
  legacyLineage.provenance.class === 'LEGACY_UNVERIFIED' &&
    !legacyLineage.decision &&
    !legacyLineage.howProviderId &&
    legacyLineage.provenance.providerRequestId == null,
  'legacy artifact is not backfilled',
);

ok(series.includes('pictureSelectionSnapshot: res.decisionId'), 'Picture Start persists DecisionId on run');
ok(series.includes('selectionSnapshot: preview.selectionSnapshot'), 'Voice Start persists DecisionId on voice asset');
ok(readFileSync(join(packInfra, 'ContentSeriesPilotService.cs'), 'utf8').includes('ContentSeriesTtsPreviewDto'), 'Voice Start returns preview DTO');

const picSnap = snapshotFamixaSelection(selectFamixaProvider({ capability: 'PICTURE' }));
const picLineage = reconstructFromSelectionSnapshot({
  artifactId: 'pic:EP01-SC01-SH01:pix-a',
  attemptId: 'pic:EP01-SC01-SH01:1',
  capability: 'PICTURE',
  snapshot: picSnap,
  pictureRevisionId: 'picture:EP01-SC01-SH01:006',
  picturePixelHash: 'pix-a',
});
ok(
  picLineage.decision?.decisionId === picSnap.decisionId &&
    picLineage.howProviderId === 'gemini' &&
    picLineage.provenance.picturePixelHash === 'pix-a',
  'Picture reverse Decision→Provider→pixel',
);
ok(
  reconstructFromSelectionSnapshot({
    artifactId: 'pic:old',
    capability: 'PICTURE',
    picturePixelHash: 'pix-old',
  }).provenance.class === 'LEGACY_UNVERIFIED',
  'Picture legacy not backfilled',
);

const voiceSnap = snapshotFamixaSelection(selectFamixaProvider({ capability: 'VOICE' }));
const voiceLineage = reconstructFromSelectionSnapshot({
  artifactId: 'voi:line-1:fp',
  attemptId: 'voi:line-1:1',
  capability: 'VOICE',
  snapshot: voiceSnap,
});
ok(voiceLineage.howProviderId === 'elevenlabs' && voiceLineage.provenance.providerRequestId == null, 'Voice reverse Decision→Provider, request id null');

const lipSnap = snapshotFamixaSelection(selectFamixaProvider({ capability: 'LIPSYNC' }));
const lipLineage = reconstructFromSelectionSnapshot({
  artifactId: 'lip:EP01-SC01-SH01:lipsync_1',
  attemptId: 'lip:EP01-SC01-SH01:1',
  capability: 'LIPSYNC',
  snapshot: lipSnap,
  providerRequestId: 'lipsync_1',
});
ok(lipLineage.howProviderId === 'fal' && lipLineage.provenance.providerRequestId === 'lipsync_1', 'LipSync reverse Decision→Provider→request');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_AI_PROVIDER_EXECUTION_PROVENANCE_V1 T1–T24 (T25–T30 run separately)');
