/** FAMIXA Execution Provenance V1 — evidence only. Not CURRENT/APPROVED authority. 0 HTTP. */

import type { FamixaProviderCapability } from './famixa-ai-provider-orchestration';
import type { FamixaProviderSelectionDecision } from './famixa-ai-provider-routing-foundation';

export const FAMIXA_EXECUTION_PROVENANCE_ID = 'FAMIXA_AI_PROVIDER_EXECUTION_PROVENANCE_V1';

export type FamixaProvenanceClass = 'VERIFIED' | 'PARTIAL' | 'LEGACY_UNVERIFIED';

export type FamixaSelectionSnapshot = {
  decisionId: string;
  providerId: string;
  modelId?: string;
  selectionMode: string;
  reason?: string;
  estimatedCost?: number | null;
  costKind?: string;
};

export type FamixaExecutionProvenance = {
  attemptId: string;
  capability: FamixaProviderCapability;
  decisionId?: string | null;
  shotId?: string;
  providerId?: string;
  modelId?: string;
  selectionMode?: string;
  selectionReason?: string;
  pictureRevisionId?: string;
  picturePixelHash?: string;
  motionContentFingerprint?: string;
  actingBeatFingerprint?: string;
  timingHash?: string;
  providerRequestId?: string | null;
  providerTaskId?: string | null;
  artifactId?: string;
  createdAt?: string;
  class: FamixaProvenanceClass;
};

export type FamixaExecutionLineage = {
  provenance: FamixaExecutionProvenance;
  decision?: FamixaSelectionSnapshot;
  whatFingerprint?: string;
  howProviderId?: string;
  howModelId?: string;
};

export function snapshotFamixaSelection(decision: FamixaProviderSelectionDecision): FamixaSelectionSnapshot {
  return {
    decisionId: decision.decisionId,
    providerId: decision.providerId,
    modelId: decision.modelId,
    selectionMode: decision.selectionMode,
    reason: decision.reason,
    estimatedCost: decision.estimatedCost,
    costKind: decision.costKind,
  };
}

export function vendorRequestId(raw?: string | null) {
  const t = (raw || '').trim();
  return t.length ? t : null;
}

export function classifyFamixaProvenance(opts: {
  attemptId?: string | null;
  decisionId?: string | null;
  providerId?: string | null;
  providerRequestId?: string | null;
}): FamixaProvenanceClass {
  const attempt = Boolean((opts.attemptId || '').trim());
  const decision = Boolean((opts.decisionId || '').trim());
  const provider = Boolean((opts.providerId || '').trim());
  if (attempt && decision && provider) return 'VERIFIED';
  if (attempt && provider) return 'PARTIAL';
  if ((opts.providerRequestId || '').trim() && provider) return 'PARTIAL';
  return 'LEGACY_UNVERIFIED';
}

export function bindFamixaExecutionProvenance(opts: {
  attemptId: string;
  capability: FamixaProviderCapability;
  decision?: FamixaProviderSelectionDecision | FamixaSelectionSnapshot | null;
  shotId?: string;
  pictureRevisionId?: string;
  picturePixelHash?: string;
  motionContentFingerprint?: string;
  actingBeatFingerprint?: string;
  timingHash?: string;
  providerRequestId?: string | null;
  providerTaskId?: string | null;
  artifactId?: string;
  createdAt?: string;
}): FamixaExecutionProvenance {
  const requestId = vendorRequestId(opts.providerRequestId);
  const taskId = vendorRequestId(opts.providerTaskId) ?? requestId;
  const decisionId = opts.decision?.decisionId;
  const providerId = opts.decision?.providerId;
  return {
    attemptId: opts.attemptId,
    capability: opts.capability,
    decisionId: decisionId || null,
    shotId: opts.shotId,
    providerId,
    modelId: opts.decision?.modelId,
    selectionMode: opts.decision?.selectionMode,
    selectionReason: 'reason' in (opts.decision || {}) ? (opts.decision as FamixaProviderSelectionDecision).reason : (opts.decision as FamixaSelectionSnapshot | undefined)?.reason,
    pictureRevisionId: opts.pictureRevisionId,
    picturePixelHash: opts.picturePixelHash,
    motionContentFingerprint: opts.motionContentFingerprint,
    actingBeatFingerprint: opts.actingBeatFingerprint,
    timingHash: opts.timingHash,
    providerRequestId: requestId,
    providerTaskId: taskId,
    artifactId: opts.artifactId,
    createdAt: opts.createdAt,
    class: classifyFamixaProvenance({
      attemptId: opts.attemptId,
      decisionId,
      providerId,
      providerRequestId: requestId,
    }),
  };
}

/** HOW from snapshot when present. Prefix infer only if selectionSnapshot.providerId is missing. */
export function motionHowProviderId(
  snapshot?: { providerId?: string } | null,
  model?: string | null,
): string | undefined {
  const fromSnap = (snapshot?.providerId || '').trim();
  if (fromSnap) return fromSnap;
  const m = (model || '').trim();
  if (!m) return undefined;
  return m.startsWith('wan') ? 'wan' : 'runway';
}

export type MotionReverseFrozen = {
  pictureRevisionId?: string;
  keyframePixelHash?: string;
  executionFingerprint?: string;
  actingBeatFingerprint?: string;
  timingHash?: string;
  promptHash?: string;
};

export type MotionReverseAttempt = {
  attemptId: string;
  selectionSnapshot?: FamixaSelectionSnapshot;
  decisionId?: string;
  provider?: string;
  providerRequestId?: string | null;
  providerTaskId?: string | null;
  frozenInput?: MotionReverseFrozen;
  input?: { stage?: string; motion?: MotionReverseFrozen };
};

export type MotionReverseArtifact = {
  artifactId: string;
  attemptId?: string;
};

/** Runtime reverse: Artifact.attemptId → Attempt.selectionSnapshot + frozenInput. No invented request id. */
export function reconstructMotionArtifactLineage(
  artifact: MotionReverseArtifact,
  attempts: readonly MotionReverseAttempt[],
): FamixaExecutionLineage {
  const attemptId = (artifact.attemptId || '').trim();
  const attempt = attemptId ? attempts.find((row) => row.attemptId === attemptId) : undefined;
  const snap = attempt?.selectionSnapshot;
  const frozen =
    attempt?.input?.stage === 'MOTION' && attempt.input.motion
      ? attempt.input.motion
      : attempt?.frozenInput;
  const providerId = (snap?.providerId || '').trim() || undefined;
  const requestId = vendorRequestId(attempt?.providerRequestId);
  const provenance = bindFamixaExecutionProvenance({
    attemptId: attempt?.attemptId || attemptId,
    capability: 'MOTION',
    decision: snap,
    shotId: undefined,
    pictureRevisionId: frozen?.pictureRevisionId,
    picturePixelHash: frozen?.keyframePixelHash,
    motionContentFingerprint: frozen?.executionFingerprint,
    actingBeatFingerprint: frozen?.actingBeatFingerprint,
    timingHash: frozen?.timingHash,
    providerRequestId: requestId,
    providerTaskId: vendorRequestId(attempt?.providerTaskId),
    artifactId: artifact.artifactId,
  });
  if (!snap) {
    return reconstructFamixaExecutionLineage({
      ...provenance,
      decisionId: null,
      providerId: undefined,
      modelId: undefined,
      class: classifyFamixaProvenance({
        attemptId: attempt?.attemptId || attemptId,
        decisionId: null,
        providerId: null,
        providerRequestId: requestId,
      }),
    });
  }
  return reconstructFamixaExecutionLineage({
    ...provenance,
    providerId,
    modelId: snap.modelId,
  });
}

export function reconstructFromSelectionSnapshot(opts: {
  artifactId: string;
  attemptId?: string;
  capability: FamixaProviderCapability;
  snapshot?: FamixaSelectionSnapshot | null;
  pictureRevisionId?: string;
  picturePixelHash?: string;
  providerRequestId?: string | null;
}): FamixaExecutionLineage {
  const snap = opts.snapshot?.decisionId && opts.snapshot.providerId ? opts.snapshot : undefined;
  const requestId = vendorRequestId(opts.providerRequestId);
  const provenance = bindFamixaExecutionProvenance({
    attemptId: opts.attemptId || '',
    capability: opts.capability,
    decision: snap,
    pictureRevisionId: opts.pictureRevisionId,
    picturePixelHash: opts.picturePixelHash,
    providerRequestId: requestId,
    artifactId: opts.artifactId,
  });
  if (!snap) {
    return reconstructFamixaExecutionLineage({
      ...provenance,
      decisionId: null,
      providerId: undefined,
      modelId: undefined,
      class: classifyFamixaProvenance({
        attemptId: opts.attemptId,
        decisionId: null,
        providerId: null,
        providerRequestId: requestId,
      }),
    });
  }
  return reconstructFamixaExecutionLineage(provenance);
}

export function reconstructFamixaExecutionLineage(provenance: FamixaExecutionProvenance): FamixaExecutionLineage {
  const decision =
    provenance.decisionId && provenance.providerId
      ? {
          decisionId: provenance.decisionId,
          providerId: provenance.providerId,
          modelId: provenance.modelId,
          selectionMode: provenance.selectionMode || 'LEGACY',
          reason: provenance.selectionReason,
        }
      : undefined;
  return {
    provenance,
    decision,
    whatFingerprint: provenance.motionContentFingerprint,
    howProviderId: provenance.providerId,
    howModelId: provenance.modelId,
  };
}

export function sameProvenanceWhat(a: FamixaExecutionProvenance, b: FamixaExecutionProvenance) {
  return Boolean(
    a.motionContentFingerprint &&
      a.motionContentFingerprint === b.motionContentFingerprint &&
      (a.pictureRevisionId || '') === (b.pictureRevisionId || ''),
  );
}

export function sameProvenanceHow(a: FamixaExecutionProvenance, b: FamixaExecutionProvenance) {
  return (a.providerId || '').toLowerCase() === (b.providerId || '').toLowerCase() && (a.modelId || '') === (b.modelId || '');
}
