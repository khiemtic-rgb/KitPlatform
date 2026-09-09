/** FAMIXA Shot Execution State Machine V1 — derived canonical state. Not a persisted status store. */

import { dataUriHash, patchRunwayAttempt, sameFailedInput, type RunwayAttempt } from '../content-famixa-runway-pipe';
import { editorialSourceOf, editorialSourceReady, type EditorialCut } from '../content-famixa-editorial-cut';
import { resolveTakeUrl, stampMuteTake } from '../content-famixa-final-source';
import { lipsyncSendEligible, shotI2vPromptHash } from '../content-famixa-prod-v2';
import { linesForShot } from '../content-famixa-dialogue-map';
import { kfPixelsOf } from '../content-famixa-kf-store';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from '../content-famixa-series';
import { editorialDurationOf, performanceDurationOf, providerDurationOf } from '../famixa-shot-production-timing';
import { productionHash } from './ShotProductionFingerprint';
import { computeInputFingerprints, voiceDurationSecOf } from './ShotProductionStamp';
import { shotProductionIntentOf } from './ShotProductionIntent';
import {
  acceptedTakeOf,
  lastSuccessTakeOf,
  motionAttemptStatusOf,
  playableMotionTakeOf,
  samePictureSuccessTakeOf,
  visibleTakeOf,
  type AcceptedTake,
} from './ShotProductionArtifacts';
import type { ShotProductionCommand } from './ShotProductionOrchestrator';
import {
  motionHowProviderId,
  reconstructMotionArtifactLineage,
  type FamixaSelectionSnapshot,
} from '../famixa-ai-provider-execution-provenance';

export const SHOT_EXECUTION_ID = 'FAMIXA_SHOT_EXECUTION_STATE_MACHINE_V1';

export type ExecutionStage =
  | 'DESIGN'
  | 'PICTURE'
  | 'MOTION'
  | 'VOICE'
  | 'LIPSYNC'
  | 'QA'
  | 'MIX'
  | 'FINAL';

export type ArtifactKind = 'PICTURE' | 'MOTION' | 'VOICE' | 'LIPSYNC' | 'MIX' | 'FINAL';

export type ArtifactValidity =
  | 'CURRENT'
  | 'STALE'
  | 'ACCEPTED_STALE'
  | 'LEGACY_UNVERIFIED'
  | 'FAILED'
  | 'LATE_RESULT'
  | 'PENDING';

export type ExecutionAttemptStatus = 'PENDING' | 'RUNNING' | 'SUCCESS' | 'FAILED' | 'CANCELLED' | 'LATE_RESULT';

export type FrozenMotionInput = {
  keyframeArtifactId?: string;
  keyframePixelHash?: string;
  pictureRevisionId?: string;
  baseMotionFingerprint: string;
  actingBeatFingerprint: string;
  promptHash: string;
  visualContractHash?: string;
  timingHash: string;
  provider: 'runway' | 'wan';
  providerDuration: 5 | 10;
  /** HOW metadata. Not hashed into executionFingerprint. */
  providerStamp?: string;
  executionFingerprint: string;
};

export type FrozenVoiceInput = {
  fingerprint: string;
  lineIds: string[];
  voiceIds: string[];
};

export type FrozenLipSyncInput = {
  motionArtifactId?: string;
  motionFingerprint?: string;
  voiceFingerprint: string;
  fingerprint: string;
  usedValidity?: ArtifactValidity;
};

export type FrozenMixInput = {
  videoArtifactId?: string;
  voiceFingerprint: string;
  editorialFingerprint: string;
  fingerprint: string;
};

export type PictureInput = {
  shotId: string;
  keyframeArtifactId?: string;
  pixelHash?: string;
  revisionId?: string;
  textFingerprint: string;
  approved: boolean;
  pendingApproval: boolean;
};

export type MotionInput = {
  picture: PictureInput;
  actingBeatFingerprint: string;
  visualContractHash?: string;
  timingHash: string;
  baseMotionFingerprint: string;
  promptHash: string;
  provider: 'runway' | 'wan';
  providerDuration: 5 | 10;
  providerStamp?: string;
  executionFingerprint: string;
};

export type VoiceInput = {
  fingerprint: string;
  lineIds: string[];
  voiceIds: string[];
};

export type LipSyncInput = {
  motionArtifactId?: string;
  voiceFingerprint: string;
  fingerprint: string;
};

export type MixInput = {
  videoArtifactId?: string;
  voiceFingerprint: string;
  editorialFingerprint: string;
  fingerprint: string;
};

export type EditorialInput = {
  mode?: 'FULL_TAKE' | 'SPEECH_CUT';
  fingerprint: string;
};

export type CurrentInputs = {
  picture: PictureInput;
  motion: MotionInput;
  voice: VoiceInput;
  lipsync: LipSyncInput;
  mix: MixInput;
  editorial: EditorialInput;
};

export type ArtifactProvenance = {
  keyframeArtifactId?: string;
  pixelHash?: string;
  pictureRevisionId?: string;
  actingBeatFingerprint?: string;
  visualContractHash?: string;
  timingHash?: string;
  promptHash?: string;
  voiceFingerprint?: string;
  motionArtifactId?: string;
  lipsyncArtifactId?: string;
  mixArtifactId?: string;
  complete: boolean;
};

export type ExecutionArtifact = {
  artifactId: string;
  kind: ArtifactKind;
  attemptId?: string;
  url?: string;
  validity: ArtifactValidity;
  inputFingerprint?: string;
  provenance: ArtifactProvenance;
};

export type FrozenStageInput =
  | { stage: 'MOTION'; motion: FrozenMotionInput }
  | { stage: 'VOICE'; voice: FrozenVoiceInput }
  | { stage: 'LIPSYNC'; lipsync: FrozenLipSyncInput }
  | { stage: 'MIX'; mix: FrozenMixInput };

export type ExecutionAttempt = {
  attemptId: string;
  shotId: string;
  stage: ExecutionStage;
  n: number;
  status: ExecutionAttemptStatus;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  provider?: 'runway' | 'wan' | 'fal' | 'elevenlabs' | 'ffmpeg';
  providerTaskId?: string;
  providerRequestId?: string;
  decisionId?: string;
  selectionSnapshot?: FamixaSelectionSnapshot;
  input?: FrozenStageInput;
  inputFingerprint?: string;
  outputArtifactId?: string;
  error?: string;
  resultClass?: 'CURRENT' | 'LATE_RESULT';
  url?: string;
};

export type ShotExecutionState = {
  shotId: string;
  currentStage: ExecutionStage;
  currentInputs: CurrentInputs;
  currentArtifacts: {
    picture?: ExecutionArtifact;
    motion?: ExecutionArtifact;
    voice?: ExecutionArtifact;
    lipsync?: ExecutionArtifact;
    mix?: ExecutionArtifact;
    final?: ExecutionArtifact;
  };
  lastSuccessArtifacts: {
    motion?: ExecutionArtifact;
    lipsync?: ExecutionArtifact;
    mix?: ExecutionArtifact;
  };
  visibleArtifacts: {
    motion?: ExecutionArtifact;
    lipsync?: ExecutionArtifact;
  };
  acceptedArtifacts: {
    motion?: ExecutionArtifact;
  };
  staleArtifacts: ExecutionArtifact[];
  attempts: ExecutionAttempt[];
  gates: {
    sameFailedInput: boolean;
    acceptExisting: boolean;
    lipsyncMayUseAccepted: boolean;
    editorialSourceReady: boolean;
    legacyUnverified: boolean;
    pictureChanged: boolean;
  };
  nextAction?: ShotProductionCommand;
  reason: string;
  inFlightAttemptId?: string;
};

export type CallbackDecision =
  | { promote: true; resultClass: 'CURRENT'; attempt: ExecutionAttempt }
  | { promote: false; resultClass: 'LATE_RESULT'; keepHistory: true; attempt?: ExecutionAttempt; reason: string };

export type ExecutionAwareAttempt = RunwayAttempt & {
  attemptId?: string;
  resultClass?: 'CURRENT' | 'LATE_RESULT';
  frozenInput?: FrozenMotionInput;
  inputFingerprint?: string;
};

function stableJson(value: unknown): string {
  if (value == null) return '';
  if (typeof value !== 'object') return String(value);
  if (Array.isArray(value)) return `[${value.map(stableJson).join(',')}]`;
  const rec = value as Record<string, unknown>;
  return `{${Object.keys(rec)
    .sort()
    .map((k) => `${k}:${stableJson(rec[k])}`)
    .join(',')}}`;
}

export function pictureArtifactId(shotId: string, pixelHash?: string) {
  const hash = (pixelHash || '').trim();
  return hash ? `pic:${shotId}:${hash}` : undefined;
}

export function motionAttemptId(shotId: string, n: number) {
  return `mot:${shotId}:${n}`;
}

export function actingBeatFingerprintOf(shot: FamixaSeriesShot) {
  return productionHash(['actingBeat', stableJson(shot.actingBeat ?? null)]);
}

export function timingHashOf(state: SeriesPilotState, shot: FamixaSeriesShot) {
  return productionHash([
    'timingV3',
    providerDurationOf(state, shot),
    editorialDurationOf(state, shot),
    performanceDurationOf(state, shot),
  ]);
}

export function editorialFingerprintOf(cut?: EditorialCut) {
  return productionHash([
    'editorial',
    cut?.mode,
    cut?.room === false ? '0' : '1',
    cut?.foley === false ? '0' : '1',
    cut?.music === false ? '0' : '1',
    cut?.loudnorm === false ? '0' : '1',
    cut?.grade === false ? '0' : '1',
  ]);
}

export type MotionWhatFingerprintParts = {
  baseMotionFingerprint: string;
  actingBeatFingerprint: string;
  promptHash: string;
  pixelHash?: string;
  providerDuration: 5 | 10;
  timingHash: string;
};

/** WHAT only. Provider is HOW and must not stale this hash. */
export function motionContentFingerprintOf(parts: MotionWhatFingerprintParts) {
  return productionHash([
    'motionExec',
    parts.baseMotionFingerprint,
    parts.actingBeatFingerprint,
    parts.promptHash,
    parts.pixelHash,
    parts.providerDuration,
    parts.timingHash,
  ]);
}

export function motionProviderStampOf(provider: string, providerDuration: 5 | 10) {
  return productionHash(['motionHow', (provider || '').trim(), providerDuration]);
}

/** Historical hash that mixed HOW into WHAT. Kept for graph compat only. */
export function motionLegacyExecutionFingerprintOf(
  parts: MotionWhatFingerprintParts & { provider: string },
) {
  return productionHash([
    'motionExec',
    parts.baseMotionFingerprint,
    parts.actingBeatFingerprint,
    parts.promptHash,
    parts.pixelHash,
    parts.provider,
    parts.providerDuration,
    parts.timingHash,
  ]);
}

export function motionExecutionFingerprintOf(
  parts: MotionWhatFingerprintParts & { provider?: string },
) {
  return motionContentFingerprintOf(parts);
}

export function sameMotionContentFingerprint(
  stored: string | undefined,
  current: string | undefined,
  parts?: MotionWhatFingerprintParts,
) {
  const have = (stored || '').trim();
  const now = (current || '').trim();
  if (!have || !now) return false;
  if (have === now) return true;
  if (!parts) return false;
  if (have === motionContentFingerprintOf(parts)) return true;
  if (have === motionLegacyExecutionFingerprintOf({ ...parts, provider: 'runway' })) return true;
  if (have === motionLegacyExecutionFingerprintOf({ ...parts, provider: 'wan' })) return true;
  return false;
}

function motionWhatPartsOf(
  input: FrozenMotionInput | MotionInput,
): MotionWhatFingerprintParts {
  const pixelHash =
    'picture' in input ? input.picture.pixelHash : input.keyframePixelHash;
  return {
    baseMotionFingerprint: input.baseMotionFingerprint,
    actingBeatFingerprint: input.actingBeatFingerprint,
    promptHash: input.promptHash,
    pixelHash,
    providerDuration: input.providerDuration,
    timingHash: input.timingHash,
  };
}

export function picturePixelHashOf(run?: SeriesShotRun, shotId?: string) {
  const raw = (run?.keyframeDataUrl || '').trim();
  const stamp = (run?.kfSourceHash || '').trim();
  const approved = Boolean(run?.kfApproved || run?.status === 'approved');
  if (raw.startsWith('data:image')) {
    const live = dataUriHash(raw);
    if (!approved || !stamp || live === stamp) return live;
  }
  if (shotId) {
    const mem = kfPixelsOf(shotId);
    if (mem?.startsWith('data:image')) {
      const memHash = dataUriHash(mem);
      if (!stamp || memHash === stamp) return memHash;
    }
  }
  return stamp;
}

/** `picture:{shotId}:{nnn}` — minted only on Duyệt hình. Not a filename. */
export function nextPictureRevisionId(shotId: string, prev?: string) {
  const id = (shotId || '').trim() || 'shot';
  const escaped = id.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const m = new RegExp(`^picture:${escaped}:(\\d+)$`).exec((prev || '').trim());
  const n = m ? Number(m[1]) + 1 : 1;
  return `picture:${id}:${String(n).padStart(3, '0')}`;
}

export function samePictureRevision(frozenId?: string, liveId?: string) {
  const a = (frozenId || '').trim();
  const b = (liveId || '').trim();
  return Boolean(a && b && a === b);
}

export function currentPictureInputOf(state: SeriesPilotState, shot: FamixaSeriesShot): PictureInput {
  const run = shotRunOf(state, shot);
  const fps = computeInputFingerprints(state, shot);
  const pixelHash = picturePixelHashOf(run, shot.id) || undefined;
  const hasPixels = Boolean(run.keyframeDataUrl?.startsWith('data:image') || pixelHash);
  const approved = Boolean((run.kfApproved || run.status === 'approved') && (hasPixels || Boolean((run.kfSourceHash || '').trim())));
  const revisionId = approved ? (run.pictureRevisionId || '').trim() || undefined : undefined;
  return {
    shotId: shot.id,
    keyframeArtifactId: pictureArtifactId(shot.id, pixelHash),
    pixelHash,
    revisionId,
    textFingerprint: fps.keyframe,
    approved,
    pendingApproval: Boolean(hasPixels && !run.kfApproved && run.status !== 'approved'),
  };
}

export function freezeMotionInput(state: SeriesPilotState, shot: FamixaSeriesShot, opts?: { provider?: 'runway' | 'wan' }): FrozenMotionInput {
  const fps = computeInputFingerprints(state, shot);
  const picture = currentPictureInputOf(state, shot);
  const run = shotRunOf(state, shot);
  const provider = opts?.provider ?? ((run.model || '').startsWith('wan') ? 'wan' : 'runway');
  const providerDuration = providerDurationOf(state, shot);
  const actingBeatFingerprint = actingBeatFingerprintOf(shot);
  const promptHash = shotI2vPromptHash(state, shot, run);
  const timingHash = timingHashOf(state, shot);
  return {
    keyframeArtifactId: picture.keyframeArtifactId,
    keyframePixelHash: picture.pixelHash,
    pictureRevisionId: picture.revisionId,
    baseMotionFingerprint: fps.motion,
    actingBeatFingerprint,
    promptHash,
    timingHash,
    provider,
    providerDuration,
    providerStamp: motionProviderStampOf(provider, providerDuration),
    executionFingerprint: motionExecutionFingerprintOf({
      baseMotionFingerprint: fps.motion,
      actingBeatFingerprint,
      promptHash,
      pixelHash: picture.pixelHash,
      providerDuration,
      timingHash,
    }),
  };
}

export function freezeVoiceInput(state: SeriesPilotState, shot: FamixaSeriesShot): FrozenVoiceInput {
  const fps = computeInputFingerprints(state, shot);
  const lines = linesForShot(state, shot);
  return {
    fingerprint: fps.voice,
    lineIds: lines.map((l) => l.id),
    voiceIds: lines.map((l) => (l.voiceId || '').trim()).filter(Boolean),
  };
}

export function freezeLipSyncInput(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  opts?: { motionArtifactId?: string; usedValidity?: ArtifactValidity },
): FrozenLipSyncInput {
  const fps = computeInputFingerprints(state, shot);
  return {
    motionArtifactId: opts?.motionArtifactId,
    motionFingerprint: fps.motion,
    voiceFingerprint: fps.voice,
    fingerprint: fps.lipsync,
    usedValidity: opts?.usedValidity,
  };
}

export function freezeMixInput(state: SeriesPilotState, shot: FamixaSeriesShot, opts?: { videoArtifactId?: string }): FrozenMixInput {
  const fps = computeInputFingerprints(state, shot);
  return {
    videoArtifactId: opts?.videoArtifactId,
    voiceFingerprint: fps.voice,
    editorialFingerprint: editorialFingerprintOf(state.editorialCut),
    fingerprint: fps.mix,
  };
}

function frozenFromAttempt(shotId: string, row: ExecutionAwareAttempt): FrozenMotionInput | undefined {
  if (row.frozenInput?.executionFingerprint) return row.frozenInput;
  const pixelHash = (row.kf?.hash || row.source?.hash || row.exactRequest?.kfHash || '').trim() || undefined;
  const promptHash = (row.promptHash || row.exactRequest?.promptHash || '').trim();
  const duration = (row.duration === 10 ? 10 : 5) as 5 | 10;
  const how = motionHowProviderId(row.selectionSnapshot, row.model);
  const provider: 'runway' | 'wan' = how === 'wan' ? 'wan' : 'runway';
  if (!pixelHash && !promptHash && !row.fingerprint) return undefined;
  const actingBeatFingerprint = (row.frozenInput?.actingBeatFingerprint || '').trim();
  const baseMotionFingerprint = (row.frozenInput?.baseMotionFingerprint || row.fingerprint || '').trim();
  const timingHash = (row.frozenInput?.timingHash || '').trim();
  const executionFingerprint =
    (row.inputFingerprint || row.frozenInput?.executionFingerprint || '').trim() ||
    (pixelHash && promptHash
      ? motionExecutionFingerprintOf({
          baseMotionFingerprint,
          actingBeatFingerprint,
          promptHash,
          pixelHash,
          provider,
          providerDuration: duration,
          timingHash,
        })
      : '');
  if (!executionFingerprint && !pixelHash) return undefined;
  return {
    keyframeArtifactId: pixelHash ? pictureArtifactId(shotId, pixelHash) : undefined,
    keyframePixelHash: pixelHash,
    pictureRevisionId: (row.frozenInput?.pictureRevisionId || '').trim() || undefined,
    baseMotionFingerprint,
    actingBeatFingerprint,
    promptHash,
    timingHash,
    provider,
    providerDuration: duration,
    providerStamp: row.frozenInput?.providerStamp || motionProviderStampOf(provider, duration),
    executionFingerprint,
  };
}

export function adaptMotionAttempts(shotId: string, attempts?: RunwayAttempt[]): ExecutionAttempt[] {
  return (attempts ?? []).map((raw) => {
    const row = raw as ExecutionAwareAttempt;
    const st = motionAttemptStatusOf(row);
    const frozen = frozenFromAttempt(shotId, row);
    let status: ExecutionAttemptStatus = 'PENDING';
    if (st === 'SUCCESS') status = row.resultClass === 'LATE_RESULT' ? 'LATE_RESULT' : 'SUCCESS';
    else if (st === 'FAILED') status = 'FAILED';
    else if (st === 'PENDING') status = row.taskId ? 'RUNNING' : 'PENDING';
    else if (row.resultClass === 'LATE_RESULT') status = 'LATE_RESULT';
    const n = row.n || 0;
    return {
      attemptId: row.attemptId || motionAttemptId(shotId, n),
      shotId,
      stage: 'MOTION',
      n,
      status,
      createdAt: row.at || '',
      startedAt: row.at,
      completedAt: st === 'SUCCESS' || st === 'FAILED' ? row.at : undefined,
      provider: motionHowProviderId(row.selectionSnapshot, row.model) === 'wan' ? 'wan' : 'runway',
      providerTaskId: row.taskId,
      providerRequestId: row.taskId,
      decisionId: row.selectionSnapshot?.decisionId,
      selectionSnapshot: row.selectionSnapshot,
      input: frozen ? { stage: 'MOTION', motion: frozen } : undefined,
      inputFingerprint: row.inputFingerprint || frozen?.executionFingerprint,
      outputArtifactId: st === 'SUCCESS' ? `mot:${shotId}:${n}` : undefined,
      error: row.error || row.failureCode,
      resultClass: row.resultClass,
      url: (row.outputUrl || '').trim() || undefined,
    };
  });
}

function provenanceFromFrozen(shotId: string, frozen?: FrozenMotionInput): ArtifactProvenance {
  const pixelHash = (frozen?.keyframePixelHash || '').trim() || undefined;
  const promptHash = (frozen?.promptHash || '').trim() || undefined;
  const acting = (frozen?.actingBeatFingerprint || '').trim() || undefined;
  const pictureRevisionId = (frozen?.pictureRevisionId || '').trim() || undefined;
  return {
    keyframeArtifactId: frozen?.keyframeArtifactId || (pixelHash ? pictureArtifactId(shotId, pixelHash) : undefined),
    pixelHash,
    pictureRevisionId,
    actingBeatFingerprint: acting,
    visualContractHash: frozen?.visualContractHash,
    timingHash: frozen?.timingHash,
    promptHash,
    complete: Boolean(pixelHash && pictureRevisionId && (frozen?.executionFingerprint || promptHash)),
  };
}

function classifyMotionArtifact(opts: {
  shotId: string;
  url?: string;
  attempt?: ExecutionAttempt;
  current: MotionInput;
  accepted?: AcceptedTake;
  resultClass?: 'CURRENT' | 'LATE_RESULT';
}): ExecutionArtifact {
  const url = (opts.url || opts.attempt?.url || '').trim() || undefined;
  const frozen = opts.attempt?.input?.stage === 'MOTION' ? opts.attempt.input.motion : undefined;
  const provenance = provenanceFromFrozen(opts.shotId, frozen);
  const artifactId = opts.attempt?.outputArtifactId || (url ? `mot:${opts.shotId}:legacy` : `mot:${opts.shotId}:unknown`);
  if (opts.attempt?.status === 'FAILED') {
    return { artifactId, kind: 'MOTION', attemptId: opts.attempt.attemptId, url, validity: 'FAILED', inputFingerprint: frozen?.executionFingerprint, provenance };
  }
  if (opts.attempt?.status === 'PENDING' || opts.attempt?.status === 'RUNNING') {
    return { artifactId, kind: 'MOTION', attemptId: opts.attempt.attemptId, url, validity: 'PENDING', inputFingerprint: frozen?.executionFingerprint, provenance };
  }
  if (opts.resultClass === 'LATE_RESULT' || opts.attempt?.resultClass === 'LATE_RESULT' || opts.attempt?.status === 'LATE_RESULT') {
    const takePixel = (frozen?.keyframePixelHash || '').trim();
    const livePixel = (opts.current.picture.pixelHash || '').trim();
    if (
      url &&
      samePictureRevision(frozen?.pictureRevisionId, opts.current.picture.revisionId) &&
      takePixel &&
      livePixel &&
      takePixel === livePixel
    ) {
      return { artifactId, kind: 'MOTION', attemptId: opts.attempt?.attemptId, url, validity: 'CURRENT', inputFingerprint: frozen?.executionFingerprint, provenance };
    }
    return { artifactId, kind: 'MOTION', attemptId: opts.attempt?.attemptId, url, validity: 'LATE_RESULT', inputFingerprint: frozen?.executionFingerprint, provenance };
  }
  const acceptedUrl = (opts.accepted?.url || '').trim();
  const isAccepted = Boolean(url && acceptedUrl && url === acceptedUrl);
  if (!samePictureRevision(frozen?.pictureRevisionId, opts.current.picture.revisionId)) {
    const missingFreeze = !(frozen?.pictureRevisionId || '').trim();
    return {
      artifactId,
      kind: 'MOTION',
      attemptId: opts.attempt?.attemptId,
      url,
      validity: isAccepted ? 'ACCEPTED_STALE' : missingFreeze || !opts.current.picture.revisionId ? 'LEGACY_UNVERIFIED' : 'STALE',
      inputFingerprint: frozen?.executionFingerprint,
      provenance,
    };
  }
  if (!provenance.complete) {
    if (opts.current.picture.pixelHash && url) {
      const takePixel = (opts.accepted?.kfHash && isAccepted ? opts.accepted.kfHash : frozen?.keyframePixelHash || '').trim();
      if (takePixel && takePixel !== opts.current.picture.pixelHash) {
        return { artifactId, kind: 'MOTION', attemptId: opts.attempt?.attemptId, url, validity: isAccepted ? 'ACCEPTED_STALE' : 'STALE', inputFingerprint: frozen?.executionFingerprint, provenance };
      }
      if (!takePixel && opts.current.picture.pixelHash && (opts.current.picture.approved || Boolean(opts.current.picture.pixelHash))) {
        return {
          artifactId,
          kind: 'MOTION',
          attemptId: opts.attempt?.attemptId,
          url,
          validity: isAccepted ? 'ACCEPTED_STALE' : 'LEGACY_UNVERIFIED',
          inputFingerprint: frozen?.executionFingerprint,
          provenance,
        };
      }
    }
    return { artifactId, kind: 'MOTION', attemptId: opts.attempt?.attemptId, url, validity: isAccepted ? 'ACCEPTED_STALE' : 'LEGACY_UNVERIFIED', inputFingerprint: frozen?.executionFingerprint, provenance };
  }
  const match =
    sameMotionContentFingerprint(
      frozen!.executionFingerprint,
      opts.current.executionFingerprint,
      motionWhatPartsOf(opts.current),
    ) && (frozen!.keyframePixelHash || '') === (opts.current.picture.pixelHash || '');
  if (match) {
    return { artifactId, kind: 'MOTION', attemptId: opts.attempt?.attemptId, url, validity: 'CURRENT', inputFingerprint: frozen!.executionFingerprint, provenance };
  }
  return {
    artifactId,
    kind: 'MOTION',
    attemptId: opts.attempt?.attemptId,
    url,
    validity: isAccepted ? 'ACCEPTED_STALE' : 'STALE',
    inputFingerprint: frozen?.executionFingerprint,
    provenance,
  };
}

function classifyPicture(picture: PictureInput): ExecutionArtifact | undefined {
  if (!picture.pixelHash && !picture.approved && !picture.pendingApproval) return undefined;
  const complete = Boolean(picture.pixelHash);
  return {
    artifactId: picture.keyframeArtifactId || `pic:${picture.shotId}:unknown`,
    kind: 'PICTURE',
    validity: complete && (picture.approved || picture.pendingApproval) ? 'CURRENT' : complete ? 'CURRENT' : 'LEGACY_UNVERIFIED',
    inputFingerprint: picture.pixelHash || picture.textFingerprint,
    provenance: {
      keyframeArtifactId: picture.keyframeArtifactId,
      pixelHash: picture.pixelHash,
      pictureRevisionId: picture.revisionId,
      complete,
    },
  };
}

function classifyVoice(voice: VoiceInput, ready: boolean): ExecutionArtifact | undefined {
  if (!ready && !voice.fingerprint) return undefined;
  return {
    artifactId: `voi:${voice.lineIds.join(',') || 'none'}:${voice.fingerprint}`,
    kind: 'VOICE',
    validity: ready ? 'CURRENT' : 'STALE',
    inputFingerprint: voice.fingerprint,
    provenance: { voiceFingerprint: voice.fingerprint, complete: Boolean(voice.fingerprint) },
  };
}

function classifyDownstream(opts: {
  kind: 'LIPSYNC' | 'MIX' | 'FINAL';
  artifactId: string;
  url?: string;
  frozenFp?: string;
  currentFp: string;
  upstreamOk: boolean;
  provenance: ArtifactProvenance;
}): ExecutionArtifact {
  const complete = opts.provenance.complete;
  if (!opts.url && !opts.frozenFp) {
    return { artifactId: opts.artifactId, kind: opts.kind, validity: 'LEGACY_UNVERIFIED', provenance: opts.provenance };
  }
  if (!complete) {
    return { artifactId: opts.artifactId, kind: opts.kind, url: opts.url, validity: 'LEGACY_UNVERIFIED', inputFingerprint: opts.frozenFp, provenance: opts.provenance };
  }
  if (!opts.upstreamOk || opts.frozenFp !== opts.currentFp) {
    return { artifactId: opts.artifactId, kind: opts.kind, url: opts.url, validity: 'STALE', inputFingerprint: opts.frozenFp, provenance: opts.provenance };
  }
  return { artifactId: opts.artifactId, kind: opts.kind, url: opts.url, validity: 'CURRENT', inputFingerprint: opts.frozenFp, provenance: opts.provenance };
}

export function currentInFlightMotionAttemptId(attempts: ExecutionAttempt[]) {
  const live = [...attempts].reverse().find((a) => a.status === 'RUNNING' || a.status === 'PENDING');
  return live?.attemptId;
}

export function motionAttemptOrderOf(attempt: { n?: number }) {
  return attempt.n || 0;
}

export function isCompleteMotionSuccess(attempt: ExecutionAttempt) {
  return (
    attempt.status === 'SUCCESS' &&
    attempt.resultClass !== 'LATE_RESULT' &&
    Boolean((attempt.url || '').trim())
  );
}

/** Newest SUCCESS with output that is not LATE_RESULT. Attempt n is authority, not callback time. */
export function currentMotionCandidateAttempt(attempts: ExecutionAttempt[]) {
  return [...attempts]
    .filter(isCompleteMotionSuccess)
    .sort((a, b) => motionAttemptOrderOf(b) - motionAttemptOrderOf(a))[0];
}

export function newerValidMotionSuccess(attempts: ExecutionAttempt[], attempt: ExecutionAttempt) {
  const order = motionAttemptOrderOf(attempt);
  return [...attempts]
    .filter((row) => motionAttemptOrderOf(row) > order && isCompleteMotionSuccess(row))
    .sort((a, b) => motionAttemptOrderOf(b) - motionAttemptOrderOf(a))[0];
}

export function isMotionAttemptSuperseded(attempts: ExecutionAttempt[], attempt: ExecutionAttempt) {
  return Boolean(newerValidMotionSuccess(attempts, attempt));
}

export function findMotionAttempt(attempts: ExecutionAttempt[], opts: { attemptId?: string; providerTaskId?: string }) {
  const id = (opts.attemptId || '').trim();
  const task = (opts.providerTaskId || '').trim();
  if (id) {
    const hit = attempts.find((a) => a.attemptId === id);
    if (hit) return hit;
  }
  if (task) return attempts.find((a) => (a.providerTaskId || '').trim() === task);
  return undefined;
}

export function validateMotionCallback(opts: {
  attempts: ExecutionAttempt[];
  current: MotionInput;
  attemptId?: string;
  providerTaskId?: string;
  inFlightAttemptId?: string;
}): CallbackDecision {
  const attempt = findMotionAttempt(opts.attempts, opts);
  if (!attempt) {
    return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, reason: 'UNKNOWN_ATTEMPT' };
  }
  const frozen = attempt.input?.stage === 'MOTION' ? attempt.input.motion.executionFingerprint : attempt.inputFingerprint;
  const live = opts.current.executionFingerprint;
  const frozenMotion = attempt.input?.stage === 'MOTION' ? attempt.input.motion : undefined;
  const frozenPixel = (frozenMotion?.keyframePixelHash || '').trim();
  const livePixel = (opts.current.picture.pixelHash || '').trim() || undefined;
  const frozenRev = (frozenMotion?.pictureRevisionId || '').trim();
  const liveRev = (opts.current.picture.revisionId || '').trim();
  const sameRevision = samePictureRevision(frozenRev, liveRev);
  const samePicture = Boolean(sameRevision && frozenPixel && livePixel && frozenPixel === livePixel);
  const pixelMismatch = Boolean(frozenPixel && livePixel && frozenPixel !== livePixel);
  const revisionMismatch = Boolean(frozenRev && liveRev && frozenRev !== liveRev);
  const revisionUnproven = Boolean(liveRev && !frozenRev);
  const inFlight = opts.inFlightAttemptId || currentInFlightMotionAttemptId(opts.attempts);
  const inFlightMismatch = Boolean(inFlight && attempt.attemptId !== inFlight);
  if (pixelMismatch || inFlightMismatch || revisionMismatch || revisionUnproven) {
    return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, attempt, reason: 'INPUT_CHANGED' };
  }
  if (isMotionAttemptSuperseded(opts.attempts, attempt)) {
    return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, attempt, reason: 'SUPERSEDED' };
  }
  if (attempt.status === 'SUCCESS' || attempt.status === 'FAILED' || attempt.status === 'LATE_RESULT') {
    if (attempt.status !== 'FAILED' && samePicture && (attempt.url || '').trim()) {
      const whatChanged = Boolean(
        frozen &&
          live &&
          !sameMotionContentFingerprint(frozen, live, motionWhatPartsOf(opts.current)),
      );
      if (whatChanged) {
        return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, attempt, reason: 'INPUT_CHANGED' };
      }
      return { promote: true, resultClass: 'CURRENT', attempt };
    }
    return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, attempt, reason: 'ALREADY_FINAL' };
  }
  if (samePicture) {
    const whatChanged = Boolean(
      frozen &&
        live &&
        !sameMotionContentFingerprint(frozen, live, motionWhatPartsOf(opts.current)),
    );
    if (whatChanged) {
      return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, attempt, reason: 'INPUT_CHANGED' };
    }
    return { promote: true, resultClass: 'CURRENT', attempt };
  }
  const late = Boolean(
    frozen &&
      live &&
      !sameMotionContentFingerprint(frozen, live, motionWhatPartsOf(opts.current)),
  );
  if (late || !frozen) {
    return { promote: false, resultClass: 'LATE_RESULT', keepHistory: true, attempt, reason: late ? 'INPUT_CHANGED' : 'MISSING_FREEZE' };
  }
  return { promote: true, resultClass: 'CURRENT', attempt };
}

export function applyMotionCallbackRecord(
  row: ExecutionAwareAttempt,
  decision: CallbackDecision,
  outputUrl?: string,
  opts?: { failed?: boolean; pictureRevisionId?: string },
): ExecutionAwareAttempt {
  const stampRev = (opts?.pictureRevisionId || '').trim();
  const frozen = row.frozenInput
    ? {
        ...row.frozenInput,
        pictureRevisionId: row.frozenInput.pictureRevisionId || stampRev || undefined,
      }
    : row.frozenInput;
  return {
    ...row,
    status: opts?.failed ? 'FAILED' : row.status || 'SUCCEEDED',
    outputUrl: outputUrl ?? row.outputUrl,
    resultClass: decision.resultClass,
    frozenInput: frozen,
    inputFingerprint: row.inputFingerprint || frozen?.executionFingerprint,
    kf: frozen?.keyframePixelHash ? { ...row.kf, hash: frozen.keyframePixelHash } : row.kf,
    source: frozen?.keyframePixelHash ? { ...row.source, hash: frozen.keyframePixelHash } : row.source,
  };
}

/** Patch only the matched attempt. Late results never rewrite current take / accepted / remake. */
export function motionCallbackRunPatch(opts: {
  run: SeriesShotRun;
  decision: CallbackDecision;
  outputUrl?: string;
  taskId?: string;
  failed?: boolean;
  videoBytes?: number;
  videoMime?: string;
  silentTake?: boolean;
}): Partial<SeriesShotRun> {
  const taskId = (opts.taskId || opts.run.turboTaskId || '').trim();
  const list = [...(opts.run.runwayAttempts ?? [])] as ExecutionAwareAttempt[];
  const idx = list.findIndex((row) => (row.attemptId && row.attemptId === opts.decision.attempt?.attemptId) || (taskId && row.taskId === taskId));
  const hit = idx >= 0 ? list[idx] : undefined;
  if (hit) {
    list[idx] = applyMotionCallbackRecord(hit, opts.decision, opts.outputUrl, {
      failed: opts.failed,
      pictureRevisionId: opts.decision.promote ? opts.run.pictureRevisionId : undefined,
    });
  }
  const attempts = hit
    ? list
    : (patchRunwayAttempt(opts.run.runwayAttempts, taskId || undefined, {
        status: opts.failed ? 'FAILED' : 'SUCCEEDED',
        outputUrl: opts.outputUrl,
        resultClass: opts.decision.resultClass,
      }) as ExecutionAwareAttempt[]);
  if (!opts.decision.promote) {
    const liveRev = (opts.run.pictureRevisionId || '').trim();
    const take = (opts.run.takeUrl || '').trim();
    const newUrl = (opts.outputUrl || '').trim();
    const turbo = (opts.run.turboTaskId || '').trim();
    const row = (idx >= 0 ? attempts[idx] : attempts.find((item) => (item.taskId || '').trim() === taskId)) as
      | ExecutionAwareAttempt
      | undefined;
    const frozenRev = (row?.frozenInput?.pictureRevisionId || '').trim();
    const hash = (row?.kf?.hash || row?.frozenInput?.keyframePixelHash || '').trim();
    const liveHash = (opts.run.kfSourceHash || '').trim();
    const epoch = opts.run.pictureRevisionAttemptN || 0;
    const adapted = adaptMotionAttempts(opts.decision.attempt?.shotId || '', attempts);
    const adaptedHit = adapted.find((item) => (item.providerTaskId || '').trim() === taskId || item.attemptId === opts.decision.attempt?.attemptId);
    const superseded = Boolean(adaptedHit && isMotionAttemptSuperseded(adapted, adaptedHit));
    if (
      !opts.failed &&
      !superseded &&
      liveRev &&
      taskId &&
      turbo &&
      taskId === turbo &&
      newUrl &&
      newUrl !== take &&
      (row?.n || 0) > epoch &&
      !frozenRev &&
      hash &&
      liveHash &&
      hash === liveHash
    ) {
      const stamped = attempts.map((item) =>
        item === row || (item.taskId || '').trim() === taskId
          ? applyMotionCallbackRecord(item, { promote: true, resultClass: 'CURRENT', attempt: opts.decision.attempt }, newUrl, {
              pictureRevisionId: liveRev,
            })
          : item,
      );
      return {
        status: 'turbo_testing',
        ...stampMuteTake(newUrl, opts.silentTake),
        turboStatus: 'SUCCEEDED',
        turboError: undefined,
        videoPipe: 'VIDEO_READY',
        motionNeedsRemake: false,
        videoVerified: true,
        videoBytes: opts.videoBytes,
        videoMime: opts.videoMime,
        runwayAttempts: stamped,
      };
    }
    return { runwayAttempts: attempts };
  }
  if (opts.failed) {
    return {
      turboStatus: 'FAILED',
      videoPipe: 'RUNWAY_FAILED',
      videoVerified: false,
      runwayAttempts: attempts,
    };
  }
  return {
    status: 'turbo_testing',
    ...stampMuteTake(opts.outputUrl || '', opts.silentTake),
    turboStatus: 'SUCCEEDED',
    turboError: undefined,
    videoPipe: 'VIDEO_READY',
    motionNeedsRemake: false,
    videoVerified: true,
    videoBytes: opts.videoBytes,
    videoMime: opts.videoMime,
    runwayAttempts: attempts,
  };
}

export function hasProductionTakeLineage(run?: SeriesShotRun) {
  if ((run?.acceptedTake?.url || '').trim()) return true;
  if ((run?.takeUrl || '').trim()) return true;
  if ((run?.lipsyncUrl || '').trim()) return true;
  return (run?.runwayAttempts ?? []).some((row) => {
    const st = (row.status || '').toUpperCase();
    return Boolean((row.outputUrl || '').trim()) && (st === 'SUCCEEDED' || st === 'SUCCESS' || st === 'READY');
  });
}

function stageOfReason(opts: {
  picture: PictureInput;
  motion?: ExecutionArtifact;
  voice?: ExecutionArtifact;
  lipsync?: ExecutionArtifact;
  mix?: ExecutionArtifact;
  spoken: boolean;
}): ExecutionStage {
  if (!opts.picture.approved && !opts.picture.pendingApproval) return 'PICTURE';
  if (opts.picture.pendingApproval) return 'PICTURE';
  if (opts.spoken && opts.voice?.validity !== 'CURRENT') return 'VOICE';
  if (opts.motion?.validity !== 'CURRENT') return 'MOTION';
  if (opts.spoken && opts.lipsync?.validity !== 'CURRENT') return 'LIPSYNC';
  if (opts.mix?.validity !== 'CURRENT') return 'MIX';
  return 'FINAL';
}

export function deriveShotExecutionState(opts: {
  state: SeriesPilotState;
  shot: FamixaSeriesShot;
  ttsFiles?: Record<string, { url: string; fileName: string }>;
  hasVoiceFile?: (lineId: string) => boolean;
  nextAction?: ShotProductionCommand;
}): ShotExecutionState {
  const { state, shot } = opts;
  const run = shotRunOf(state, shot);
  const intent = shotProductionIntentOf(state, shot);
  const spoken = intent.productionMode === 'SPOKEN';
  const fps = computeInputFingerprints(state, shot);
  const picture = currentPictureInputOf(state, shot);
  const frozenMotion = freezeMotionInput(state, shot);
  const motionInput: MotionInput = { ...frozenMotion, picture };
  const voiceIn: VoiceInput = freezeVoiceInput(state, shot);
  const editorial: EditorialInput = {
    mode: state.editorialCut?.mode,
    fingerprint: editorialFingerprintOf(state.editorialCut),
  };
  const attempts = adaptMotionAttempts(shot.id, run.runwayAttempts);
  const inFlightAttemptId = currentInFlightMotionAttemptId(attempts);
  const accepted = run.acceptedTake;
  const lastSuccessAny = lastSuccessTakeOf(run.runwayAttempts);
  const samePictureTake = samePictureSuccessTakeOf(run, picture.pixelHash, picture.revisionId);
  const playableTake = playableMotionTakeOf(run, picture.pixelHash, picture.revisionId);
  const candidate = currentMotionCandidateAttempt(attempts);
  const lastSuccess = candidate
    ? {
        n: candidate.n,
        status: 'SUCCESS' as const,
        url: candidate.url,
        kfHash: candidate.input?.stage === 'MOTION' ? candidate.input.motion.keyframePixelHash : undefined,
      }
    : samePictureTake || playableTake || lastSuccessAny;
  const visible = candidate
    ? lastSuccess
    : playableTake ?? samePictureTake ?? visibleTakeOf(run);
  const lastAttempt = candidate ?? (lastSuccess ? attempts.find((a) => a.n === lastSuccess.n) : undefined);
  const visibleAttempt = candidate ?? (visible ? attempts.find((a) => a.n === visible.n || a.url === visible.url) : undefined);

  const motionFromAttempt = lastAttempt
    ? classifyMotionArtifact({
        shotId: shot.id,
        url: lastSuccess?.url,
        attempt: lastAttempt,
        current: motionInput,
        accepted,
        resultClass: lastAttempt.resultClass,
      })
    : visible?.url
      ? classifyMotionArtifact({
          shotId: shot.id,
          url: visible.url,
          current: motionInput,
          accepted,
        })
      : undefined;

  const pictureArt = classifyPicture(picture);
  const lines = linesForShot(state, shot);
  const voiceReady = !spoken
    ? true
    : lines.every((line) => {
        const dur = Number(state.voiceAssets?.[line.id]?.duration || 0);
        return dur > 0.2 || Boolean(opts.ttsFiles?.[line.id]?.url) || Boolean(opts.hasVoiceFile?.(line.id));
      });
  const voiceArt = spoken ? classifyVoice(voiceIn, voiceReady) : { artifactId: `voi:${shot.id}:silent`, kind: 'VOICE' as const, validity: 'CURRENT' as const, provenance: { complete: true } };

  const motionCurrent = motionFromAttempt?.validity === 'CURRENT' ? motionFromAttempt : undefined;
  const motionAllowedForLip =
    motionFromAttempt?.validity === 'CURRENT' ||
    (motionFromAttempt?.validity === 'ACCEPTED_STALE' &&
      lipsyncSendEligible({
        run,
        spoken,
        remake: false,
        motionStale: motionFromAttempt.validity !== 'CURRENT',
        accepted: Boolean(accepted?.url),
      }));

  const lipUrl = (run.lipsyncUrl || '').trim() || undefined;
  const lipsyncIn: LipSyncInput = {
    motionArtifactId: motionCurrent?.artifactId || (motionAllowedForLip ? motionFromAttempt?.artifactId : undefined),
    voiceFingerprint: voiceIn.fingerprint,
    fingerprint: fps.lipsync,
  };
  const lipStamp = run.shotProduction?.lipsyncFp;
  const lipArt = lipUrl
    ? classifyDownstream({
        kind: 'LIPSYNC',
        artifactId: `lip:${shot.id}:${run.lipsyncTaskId || 'legacy'}`,
        url: lipUrl,
        frozenFp: lipStamp,
        currentFp: fps.lipsync,
        upstreamOk: Boolean(motionAllowedForLip && (!spoken || voiceArt.validity === 'CURRENT')),
        provenance: {
          motionArtifactId: lipsyncIn.motionArtifactId,
          voiceFingerprint: voiceIn.fingerprint,
          complete: Boolean(lipStamp && lipsyncIn.motionArtifactId),
        },
      })
    : undefined;

  const mixIn: MixInput = {
    videoArtifactId: lipArt?.validity === 'CURRENT' ? lipArt.artifactId : motionAllowedForLip ? motionFromAttempt?.artifactId : undefined,
    voiceFingerprint: voiceIn.fingerprint,
    editorialFingerprint: editorial.fingerprint,
    fingerprint: fps.mix,
  };
  const mixStamp = run.shotProduction?.assembleFp;
  const mixArt = mixStamp
    ? classifyDownstream({
        kind: 'MIX',
        artifactId: `mix:${shot.id}:${mixStamp}`,
        frozenFp: mixStamp,
        currentFp: fps.mix,
        upstreamOk: Boolean((lipArt?.validity === 'CURRENT' || (!spoken && motionCurrent)) && editorial.fingerprint),
        provenance: {
          motionArtifactId: motionFromAttempt?.artifactId,
          lipsyncArtifactId: lipArt?.artifactId,
          complete: Boolean(mixStamp),
        },
      })
    : undefined;
  const finalArt =
    mixArt?.validity === 'CURRENT'
      ? { ...mixArt, kind: 'FINAL' as const, artifactId: `final:${shot.id}:${mixStamp}` }
      : mixArt
        ? { ...mixArt, kind: 'FINAL' as const, artifactId: `final:${shot.id}:${mixStamp || 'stale'}`, validity: 'STALE' as const }
        : undefined;

  const visibleMotion = visible?.url
    ? classifyMotionArtifact({
        shotId: shot.id,
        url: visible.url,
        attempt: visibleAttempt,
        current: motionInput,
        accepted,
        resultClass: visibleAttempt?.resultClass,
      })
    : undefined;

  const stale: ExecutionArtifact[] = [];
  for (const row of [motionFromAttempt, visibleMotion, lipArt, mixArt, finalArt]) {
    if (row && (row.validity === 'STALE' || row.validity === 'LATE_RESULT' || row.validity === 'ACCEPTED_STALE' || row.validity === 'LEGACY_UNVERIFIED')) {
      if (!stale.some((s) => s.artifactId === row.artifactId && s.kind === row.kind)) stale.push(row);
    }
  }

  const pictureIdentityKnown = Boolean(picture.pixelHash);
  const takePixel =
    (visible?.kfHash ||
      lastSuccess?.kfHash ||
      (lastAttempt?.input?.stage === 'MOTION' ? lastAttempt.input.motion.keyframePixelHash : '') ||
      '') || '';
  const takeRevision =
    (lastAttempt?.input?.stage === 'MOTION' ? lastAttempt.input.motion.pictureRevisionId : '') ||
    (visibleAttempt?.input?.stage === 'MOTION' ? visibleAttempt.input.motion.pictureRevisionId : '') ||
    '';
  const liveRevision = (picture.revisionId || '').trim();
  const revisionMismatch = Boolean(liveRevision && takeRevision && liveRevision !== takeRevision);
  const revisionUnproven = Boolean(picture.approved && liveRevision && !takeRevision && Boolean(visible?.url || lastSuccess?.url));
  const pixelMismatch = Boolean(picture.pixelHash && takePixel && takePixel !== picture.pixelHash);
  const samePictureTakeReady = Boolean(
    samePictureRevision(takeRevision, liveRevision) && picture.pixelHash && takePixel && takePixel === picture.pixelHash,
  );
  const remakeFlag = Boolean(run.motionNeedsRemake) && !samePictureTakeReady;
  const unstampedBoundTake = Boolean(
    picture.approved &&
      pictureIdentityKnown &&
      Boolean((run.kfSourceHash || '').trim()) &&
      Boolean(visible?.url || lastSuccess?.url) &&
      !takePixel,
  );
  const pictureChanged = Boolean(
    remakeFlag ||
      revisionMismatch ||
      revisionUnproven ||
      (picture.approved && pixelMismatch) ||
      unstampedBoundTake ||
      (run.kfApproved === false && Boolean(visible?.url || lastSuccess?.url) && !samePictureTakeReady),
  );

  const sameFail = sameFailedInput(run, picture.pixelHash || dataUriHash(run.keyframeDataUrl), motionInput.promptHash);
  const acceptExisting = Boolean(visible?.url && motionFromAttempt && motionFromAttempt.validity !== 'CURRENT' && !accepted?.url);
  const lipsyncMayUseAccepted = lipsyncSendEligible({
    run,
    spoken,
    remake: false,
    motionStale: motionFromAttempt?.validity !== 'CURRENT',
    accepted: Boolean(accepted?.url),
  });

  const currentStage = stageOfReason({
    picture,
    motion: motionFromAttempt,
    voice: voiceArt,
    lipsync: lipArt,
    mix: mixArt,
    spoken,
  });

  let reason = 'Shot đang ở input hiện tại.';
  if (pictureChanged) reason = 'Video hiện tại được tạo từ ảnh/diễn xuất cũ.';
  else if (motionFromAttempt?.validity === 'LEGACY_UNVERIFIED') reason = 'Video có URL nhưng thiếu provenance.';
  else if (sameFail) reason = 'Lần gửi trước đã FAIL trên cùng input.';
  else if (lipArt?.validity === 'STALE') reason = 'Lồng tiếng không còn khớp voice/motion hiện tại.';
  else if (mixArt?.validity === 'STALE') reason = 'Bản mix không còn khớp nguồn hiện tại.';

  return {
    shotId: shot.id,
    currentStage,
    currentInputs: {
      picture,
      motion: motionInput,
      voice: voiceIn,
      lipsync: lipsyncIn,
      mix: mixIn,
      editorial,
    },
    currentArtifacts: {
      picture: pictureArt?.validity === 'CURRENT' ? pictureArt : undefined,
      motion: motionCurrent,
      voice: voiceArt.validity === 'CURRENT' ? voiceArt : undefined,
      lipsync: lipArt?.validity === 'CURRENT' ? lipArt : undefined,
      mix: mixArt?.validity === 'CURRENT' ? mixArt : undefined,
      final: finalArt?.validity === 'CURRENT' ? finalArt : undefined,
    },
    lastSuccessArtifacts: {
      motion: motionFromAttempt,
      lipsync: lipArt,
      mix: mixArt,
    },
    visibleArtifacts: {
      motion: visibleMotion,
      lipsync: lipArt,
    },
    acceptedArtifacts: {
      motion:
        accepted?.url && visibleMotion && (visibleMotion.url || '') === (accepted.url || '')
          ? { ...visibleMotion, validity: visibleMotion.validity === 'CURRENT' ? 'CURRENT' : 'ACCEPTED_STALE' }
          : undefined,
    },
    staleArtifacts: stale,
    attempts,
    gates: {
      sameFailedInput: sameFail,
      acceptExisting,
      lipsyncMayUseAccepted,
      editorialSourceReady: editorialSourceReady(run),
      legacyUnverified: Boolean(
        motionFromAttempt?.validity === 'LEGACY_UNVERIFIED' || lipArt?.validity === 'LEGACY_UNVERIFIED',
      ),
      pictureChanged,
    },
    nextAction: opts.nextAction,
    reason,
    inFlightAttemptId,
  };
}

/** Snapshot projection only. Does not invent current-valid from URL. */
export function executionMotionProjection(exec: ShotExecutionState, opts?: { keyframeApproved?: boolean; motionStaleCompat?: boolean }) {
  const current = exec.currentArtifacts.motion?.validity === 'CURRENT';
  const legacy = exec.lastSuccessArtifacts.motion?.validity === 'LEGACY_UNVERIFIED';
  const approved = opts?.keyframeApproved !== false && exec.currentInputs.picture.approved;
  const takeFromOtherPicture = exec.gates.pictureChanged;
  const motionReady = Boolean(current && approved);
  const motionReadyCompat = motionReady || Boolean(legacy && approved && !takeFromOtherPicture && !opts?.motionStaleCompat);
  return {
    takeFromOtherPicture,
    motionReady: motionReadyCompat,
    motionCurrent: current,
    motionLegacy: legacy,
    motionStale: Boolean(
      exec.lastSuccessArtifacts.motion &&
        (exec.lastSuccessArtifacts.motion.validity === 'STALE' ||
          exec.lastSuccessArtifacts.motion.validity === 'LATE_RESULT' ||
          exec.gates.pictureChanged),
    ),
  };
}

export function executionDiagnosticLines(exec: ShotExecutionState) {
  const motion = exec.lastSuccessArtifacts.motion;
  const currentMot = exec.currentArtifacts.motion;
  const lineage = motion ? reconstructMotionArtifactLineage(motion, exec.attempts) : undefined;
  return [
    `SHOT ${exec.shotId}`,
    `CURRENT STAGE ${exec.currentStage}`,
    `CURRENT INPUT KF ${exec.currentInputs.picture.pixelHash || '—'} rev ${exec.currentInputs.picture.revisionId || '—'} beat ${exec.currentInputs.motion.actingBeatFingerprint || '—'}`,
    `CURRENT MOTION ${currentMot?.artifactId || 'none'}`,
    `LAST SUCCESS ${motion?.artifactId || '—'} ${motion?.validity || ''}`,
    `VISIBLE ${exec.visibleArtifacts.motion?.artifactId || '—'} ${exec.visibleArtifacts.motion?.validity || ''}`,
    `ACCEPTED ${exec.acceptedArtifacts.motion?.artifactId || '—'} ${exec.acceptedArtifacts.motion?.validity || ''}`,
    `STALE ${exec.staleArtifacts.map((s) => `${s.kind}:${s.validity}`).join(',') || '—'}`,
    `LEGACY ${exec.gates.legacyUnverified ? 'true' : 'false'}`,
    `LAST ATTEMPT ${exec.attempts.at(-1)?.attemptId || '—'} ${exec.attempts.at(-1)?.status || ''}`,
    `IN-FLIGHT ${exec.inFlightAttemptId || '—'}`,
    `LATE ${exec.attempts.some((a) => a.resultClass === 'LATE_RESULT' || a.status === 'LATE_RESULT') ? 'yes' : 'no'}`,
    `NEXT ${exec.nextAction?.type || '—'}`,
    `REASON ${exec.reason}`,
    `LINEAGE motion←KF ${motion?.provenance.pixelHash || '—'} beat ${motion?.provenance.actingBeatFingerprint || '—'}`,
    `PROVENANCE ${lineage?.provenance.class || 'LEGACY_UNVERIFIED'} decision ${lineage?.decision?.decisionId || '—'} how ${lineage?.howProviderId || '—'} what ${lineage?.whatFingerprint || '—'}`,
    `SAME_FAILED_INPUT ${exec.gates.sameFailedInput ? 'true' : 'false'}`,
  ];
}

export function deepStableEqual(a: unknown, b: unknown): boolean {
  return stableJson(a) === stableJson(b);
}

export function cloneExecutionFixture<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

/** Display only. Production identity never uses this. */
export function previewUrlIsDisplayOnly() {
  return true;
}

export function editorialVisibleSource(run: Parameters<typeof editorialSourceOf>[0]) {
  return editorialSourceOf(run);
}

export function editorialCurrentValidSource(exec: ShotExecutionState, run: Parameters<typeof editorialSourceOf>[0]) {
  if (exec.currentArtifacts.lipsync?.url) return exec.currentArtifacts.lipsync.url;
  if (exec.currentArtifacts.motion?.url) return exec.currentArtifacts.motion.url;
  const visible = editorialSourceOf(run);
  if (visible && exec.currentArtifacts.lipsync?.validity === 'CURRENT') return visible;
  return '';
}

export { resolveTakeUrl };
