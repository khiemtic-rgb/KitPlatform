import { promptUsedOnCurrentKf } from '../content-runway-adapter';
import { dataUriHash, lastGenerationFail, sameFailedInput } from '../content-famixa-runway-pipe';
import { CAMERA_RETRY_POOL } from '../content-runway-prompt-v1';
import { lipsyncQaReady, shotI2vPromptHash } from '../content-famixa-prod-v2';
import { resolveTakeUrl } from '../content-famixa-final-source';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from '../content-famixa-series';
import { kfPixelsOf } from '../content-famixa-kf-store';
import {
  dialogueExceedsI2vCap,
  i2vDurationForDialogue,
  shotProductionInputOf,
  videoProductionPreflight,
  voiceReadyOf,
  type PreflightResult,
  type ShotProductionInput,
} from '../famixa-video-audio-lipsync-pipeline';
import {
  acceptedTakeOf,
  currentAttemptOf,
  deriveMotionState,
  lastFailOf,
  lastSuccessfulTakePromptHashOf,
  lastSuccessTakeOf,
  playableMotionTakeOf,
  samePictureSuccessTakeOf,
  takeFromOtherPicture,
  visibleTakeOf,
  type MotionAttemptArtifact,
  type MotionStateId,
} from './ShotProductionArtifacts';
import { ORCH_GATE } from './ShotProductionErrors';
import { isStale, type ProductionInputFps } from './ShotProductionFingerprint';
import { shotProductionIntentOf, type ShotProductionIntent } from './ShotProductionIntent';
import { computeInputFingerprints, voiceDurationSecOf, type ShotProductionStamp } from './ShotProductionStamp';
import { providerDurationOf, timingDurationBlockReason } from '../famixa-shot-production-timing';
import { nextShotProductionCommand } from './ShotProductionOrchestrator';
import {
  deriveShotExecutionState,
  executionMotionProjection,
  picturePixelHashOf,
  type ShotExecutionState,
} from './ShotProductionExecution';

export type ShotProductionStage =
  | 'DRAFT'
  | 'VOICE_REQUIRED'
  | 'VOICE_READY'
  | 'VISUAL_REQUIRED'
  | 'VISUAL_REVIEW'
  | 'VIDEO_REQUIRED'
  | 'VIDEO_CONFIRM'
  | 'VIDEO_PROCESSING'
  | 'VIDEO_REVIEW'
  | 'VIDEO_READY'
  | 'VIDEO_ERROR'
  | 'LIPSYNC_REQUIRED'
  | 'LIPSYNC_QA_REQUIRED'
  | 'LIPSYNC_CONFIRM'
  | 'LIPSYNC_PROCESSING'
  | 'LIPSYNC_READY'
  | 'LIPSYNC_ERROR'
  | 'MIX_REQUIRED'
  | 'MIX_PROCESSING'
  | 'MIX_ERROR'
  | 'FINAL_READY'
  | 'BLOCKED'
  | 'ERROR'
  | 'NEEDS_VISUAL_REVIEW';

export type ShotProductionSnapshot = {
  shotId: string;
  intent: ShotProductionIntent;
  input: ShotProductionInput;
  preflight: PreflightResult;
  stage: ShotProductionStage;
  isSilent: boolean;
  speaker?: string;
  voiceReady: boolean;
  voiceDurationSec: number;
  voiceFingerprint: string;
  voiceStale: boolean;
  keyframeReady: boolean;
  keyframeApproved: boolean;
  keyframeFingerprint: string;
  keyframeStale: boolean;
  picturePendingApproval: boolean;
  pictureUnusable?: boolean;
  kfGenerated: boolean;
  motionReady: boolean;
  motionUsable: boolean;
  motionFingerprint: string;
  motionStale: boolean;
  takeFromOtherPicture: boolean;
  motionBusy: boolean;
  motionFailed: boolean;
  motionAccepted: boolean;
  motionAcceptedStale: boolean;
  motionState: MotionStateId;
  retryLocked: boolean;
  camerasExhausted: boolean;
  currentAttempt?: MotionAttemptArtifact;
  lastSuccessTake?: MotionAttemptArtifact;
  lastFail?: MotionAttemptArtifact;
  visibleTake?: MotionAttemptArtifact;
  acceptedTake?: MotionAttemptArtifact;
  videoApproved: boolean;
  lipSyncRequired: boolean;
  lipSyncReady: boolean;
  lipSyncFingerprint: string;
  lipSyncStale: boolean;
  lipsyncBusy: boolean;
  lipsyncFailed: boolean;
  qaReady: boolean;
  qualityPassed: boolean;
  mixReady: boolean;
  mixStale: boolean;
  mixBusy: boolean;
  mixFailed: boolean;
  finalReady: boolean;
  finalArtifactReady: boolean;
  durationMismatch: boolean;
  i2vSeconds: 5 | 10;
  blockingReason?: string;
  nextAction: string;
  fps: ProductionInputFps;
  stamp?: ShotProductionStamp;
  lastFailHash?: string;
  /** Derived canonical state. Snapshot flags are a compatibility projection of this. */
  execution?: ShotExecutionState;
};

export function lipsyncQaReadyOf(state: SeriesPilotState, shot: FamixaSeriesShot, spoken: boolean) {
  return lipsyncQaReady(shotRunOf(state, shot), spoken);
}

/** Prompt hash of the last SUCCESS take. A later FAIL must not replace it. */
export function lastTakePromptHashOf(run?: { runwayAttempts?: { promptHash?: string; status?: string; outputUrl?: string }[] }) {
  return lastSuccessfulTakePromptHashOf(run);
}

/** Real submitted FAIL jobs on this KF. Local i2vRetry clicks do not count. */
export function failedJobsOnThisKf(
  run?: {
    failedKfHash?: string;
    runwayAttempts?: {
      status?: string;
      failureCode?: string;
      taskId?: string;
      source?: { hash?: string };
      kf?: { hash?: string };
    }[];
  },
  kfHash?: string,
) {
  if (!kfHash) return 0;
  return (run?.runwayAttempts ?? []).filter((a) => {
    if (!a.taskId?.trim()) return false;
    const failed = /FAIL|CANCEL/i.test(a.status || '') || /INTERNAL|BAD_OUTPUT/i.test(a.failureCode || '');
    if (!failed) return false;
    const stamped = a.source?.hash || a.kf?.hash;
    if (stamped) return stamped === kfHash;
    return run?.failedKfHash === kfHash;
  }).length;
}

/** Runway INTERNAL on this picture only. Older KF fails must not block a new still. */
export function runwayFailedOnCurrentKf(
  run?: Parameters<typeof failedJobsOnThisKf>[0] & { turboError?: string; failedKfHash?: string },
  kfHash?: string,
) {
  const hash = (kfHash || '').trim();
  if (!hash) return false;
  if (failedJobsOnThisKf(run, hash) >= 1) return true;
  const last = lastGenerationFail(run as Parameters<typeof lastGenerationFail>[0]);
  const lastHash = (last?.source?.hash || last?.kf?.hash || '').trim();
  if (lastHash) return lastHash === hash;
  if (/INTERNAL|BAD_OUTPUT/i.test(run?.turboError || '')) {
    return (run?.failedKfHash || '').trim() === hash;
  }
  return false;
}

/** Prompt-used-by-take freshness. Does not change motionInputFingerprint. */
export function motionPromptFreshnessStale(opts: {
  hasMuteTake: boolean;
  lastTakePromptHash?: string;
  currentPromptHash?: string;
}) {
  const last = (opts.lastTakePromptHash || '').trim();
  return Boolean(opts.hasMuteTake && last && last !== (opts.currentPromptHash || '').trim());
}

export function buildShotProductionSnapshot(opts: {
  state: SeriesPilotState;
  shot: FamixaSeriesShot;
  ttsFiles: Record<string, { url: string; fileName: string }>;
  hasVoiceFile?: (lineId: string) => boolean;
  pictureRejected?: boolean;
  pictureUnusable?: boolean;
  motionBusy?: boolean;
  lipsyncBusy?: boolean;
  mixBusy?: boolean;
  mixFailed?: boolean;
  artifactPresent?: boolean;
}): ShotProductionSnapshot {
  const intent = shotProductionIntentOf(opts.state, opts.shot);
  const input = shotProductionInputOf(opts.state, opts.shot, opts.ttsFiles, opts.hasVoiceFile);
  const preflight = videoProductionPreflight(input);
  const run = shotRunOf(opts.state, opts.shot);
  const fps = computeInputFingerprints(opts.state, opts.shot);
  const stamp = run.shotProduction;
  const spoken = intent.productionMode === 'SPOKEN';
  const silent = intent.productionMode === 'SILENT';
  const voiceDurationSec = spoken ? voiceDurationSecOf(opts.state, opts.shot) : 0;
  const voiceStale = spoken && isStale(stamp?.voiceFp, fps.voice);
  const keyframeStale = isStale(stamp?.kfFp, fps.keyframe);
  const currentPromptHash = shotI2vPromptHash(opts.state, opts.shot, run);
  const currentAttempt = currentAttemptOf(run.runwayAttempts);
  const lastSuccessTakeAny = lastSuccessTakeOf(run.runwayAttempts);
  const livePictureHash = picturePixelHashOf(run, opts.shot.id);
  const livePictureRevision = (run.pictureRevisionId || '').trim() || undefined;
  const samePictureTake = samePictureSuccessTakeOf(run, livePictureHash, livePictureRevision);
  const playableTake = playableMotionTakeOf(run, livePictureHash, livePictureRevision);
  const lastSuccessTake = samePictureTake || playableTake || lastSuccessTakeAny;
  const lastFail = lastFailOf(run.runwayAttempts);
  const acceptedTake = acceptedTakeOf(run);
  const visibleTake = playableTake ?? samePictureTake ?? visibleTakeOf(run);
  const lastTakePromptHash = lastTakePromptHashOf(run);
  const freshnessHash = (visibleTake?.promptHash || lastTakePromptHash).trim();
  const stampMotionStale =
    isStale(stamp?.motionFp, fps.motion) ||
    motionPromptFreshnessStale({
      hasMuteTake: Boolean(visibleTake?.url || input.hasMuteTake),
      lastTakePromptHash: freshnessHash,
      currentPromptHash,
    });
  const lipSyncStale = spoken && isStale(stamp?.lipsyncFp, fps.lipsync);
  const mixStale = isStale(stamp?.assembleFp, fps.mix);
  const sessionVoiceReady = silent || (voiceReadyOf(input.cues) && !voiceStale);
  const hasLipFile = spoken && Boolean(input.hasLipSync);
  const voiceReady = sessionVoiceReady || (hasLipFile && !lipSyncStale && !voiceStale);
  const keyframeReady = Boolean(
    run.keyframeDataUrl?.startsWith('data:image') || kfPixelsOf(opts.shot.id)?.startsWith('data:image'),
  );
  const keyframeApproved = Boolean(input.hasCanonicalKf) && !keyframeStale;
  const exec = deriveShotExecutionState({
    state: opts.state,
    shot: opts.shot,
    ttsFiles: opts.ttsFiles,
    hasVoiceFile: opts.hasVoiceFile,
  });
  const motionProj = executionMotionProjection(exec, {
    keyframeApproved,
    motionStaleCompat: stampMotionStale,
  });
  const motionStale = Boolean(stampMotionStale || motionProj.motionStale);
  const motionReady = motionProj.motionReady;
  const motionAccepted = Boolean(acceptedTake?.url);
  const motionUsable = motionReady || (motionAccepted && Boolean(visibleTake?.url || resolveTakeUrl(run)));
  const motionAcceptedStale = motionAccepted && motionStale;
  const pictureChanged =
    motionProj.takeFromOtherPicture ||
    takeFromOtherPicture({
      keyframeDataUrl: run.keyframeDataUrl,
      kfSourceHash: run.kfSourceHash,
      takeKfHash: visibleTake?.kfHash || lastSuccessTake?.kfHash,
      motionNeedsRemake: run.motionNeedsRemake,
      kfApproved: run.kfApproved,
      hasTake: Boolean(visibleTake?.url || resolveTakeUrl(run) || run.lipsyncUrl),
      bindUnstampedTake: Boolean(run.kfSourceHash),
      liveRevisionId: exec.currentInputs.picture.revisionId,
      takeRevisionId:
        (exec.lastSuccessArtifacts.motion?.provenance.pictureRevisionId ||
          exec.visibleArtifacts.motion?.provenance.pictureRevisionId ||
          '') || undefined,
    });
  const kfHash = dataUriHash(run.keyframeDataUrl);
  const failKf = run.failedKfHash || lastGenerationFail(run)?.source?.hash;
  const failOnThisKf = Boolean(failKf && kfHash && failKf === kfHash);
  const retryLocked =
    sameFailedInput(run, kfHash, currentPromptHash) ||
    promptUsedOnCurrentKf(run, currentPromptHash, kfHash);
  const camerasExhausted =
    failOnThisKf &&
    retryLocked &&
    failedJobsOnThisKf(run, kfHash) >= CAMERA_RETRY_POOL;
  const currentFailed = currentAttempt?.status === 'FAILED' || (Boolean(run.turboError) && currentAttempt?.status !== 'SUCCESS');
  const motionState = deriveMotionState({
    hasVisibleTake: Boolean(visibleTake?.url),
    accepted: motionAccepted,
    stale: motionStale,
    currentFailed,
    retryLocked,
    motionReady,
  });
  const lipSyncRequired = spoken;
  const hasCurrentTake = Boolean(resolveTakeUrl(run) || visibleTake?.url);
  const lipSyncReady = spoken
    ? Boolean(input.hasLipSync) && !lipSyncStale && voiceReady && motionUsable && hasCurrentTake
    : true;
  const qaReady = silent || lipsyncQaReady(run, true);
  const qualityPassed = silent || Boolean(run.shotQa?.lipsyncQuality && run.shotQa?.finalAv);
  const assembleOk = Boolean(stamp?.assembleFp) && stamp?.assembleFp === fps.mix && !mixStale;
  const mixReady = assembleOk;
  const finalArtifactReady = mixReady && (opts.artifactPresent !== false ? Boolean(stamp?.assembleFp) : false);
  const timingBlock = timingDurationBlockReason(opts.state, opts.shot);
  const durationMismatch =
    Boolean(timingBlock) ||
    (spoken &&
      (voiceDurationSec > 0 && (dialogueExceedsI2vCap(voiceDurationSec) || preflight.warnings.includes('DIALOGUE_EXCEEDS_I2V_CAP'))));
  const characterChanged = Boolean(stamp?.characterIds?.length && stamp.characterIds.join(',') !== fps.character);
  const pictureRejected = Boolean(opts.pictureRejected ?? (run.kfApproved === false && Boolean(run.notes) && keyframeReady));
  const pictureUnusable = Boolean(opts.pictureUnusable);
  const picturePendingApproval = Boolean(keyframeReady && !run.kfApproved && run.status !== 'approved' && !pictureUnusable);
  const motionFailed = currentFailed && !opts.motionBusy && !motionReady;
  const lipsyncFailed = Boolean(run.lipsyncError) && !opts.lipsyncBusy && !lipSyncReady;
  const lastFailHash = run.failedKfHash && run.failedPromptHash ? `${run.failedKfHash}:${run.failedPromptHash}` : undefined;

  let blockingReason: string | undefined;
  if (intent.productionMode === 'MULTI_SPEAKER_BLOCKED') blockingReason = ORCH_GATE.MULTI_SPEAKER_LIPSYNC_UNSUPPORTED;
  else if (characterChanged) blockingReason = ORCH_GATE.CHARACTER_AUTHORITY_REQUIRED;
  else if (spoken && input.cues.some((c) => !(c.voiceId || '').trim())) blockingReason = ORCH_GATE.VOICE_NOT_ASSIGNED;
  else if (timingBlock) blockingReason = timingBlock;
  else if (durationMismatch) blockingReason = ORCH_GATE.DURATION_MISMATCH;
  else if (pictureRejected) blockingReason = ORCH_GATE.NEEDS_VISUAL_REVIEW;

  const finalReady =
    !blockingReason &&
    keyframeApproved &&
    motionUsable &&
    (!spoken || (voiceReady && lipSyncReady && qaReady && qualityPassed)) &&
    mixReady &&
    !input.overlayPreview;

  const stage = deriveShotProductionStage({
    intent,
    spoken,
    blockingReason,
    pictureRejected,
    picturePendingApproval,
    keyframeReady,
    keyframeApproved,
    voiceReady,
    motionReady,
    motionUsable,
    motionAccepted,
    motionBusy: Boolean(opts.motionBusy),
    motionFailed,
    videoApproved: Boolean(run.videoApproved || run.status === 'approved'),
    qaReady,
    lipSyncReady,
    lipsyncBusy: Boolean(opts.lipsyncBusy),
    lipsyncFailed,
    mixReady,
    mixBusy: Boolean(opts.mixBusy),
    mixFailed: Boolean(opts.mixFailed),
    finalReady,
  });

  const snap: ShotProductionSnapshot = {
    shotId: opts.shot.id,
    intent,
    input,
    preflight,
    stage,
    isSilent: silent,
    speaker: intent.characterIds[0],
    voiceReady,
    voiceDurationSec,
    voiceFingerprint: fps.voice,
    voiceStale,
    keyframeReady,
    keyframeApproved,
    keyframeFingerprint: fps.keyframe,
    keyframeStale,
    picturePendingApproval,
    pictureUnusable,
    kfGenerated: keyframeReady,
    motionReady,
    motionUsable,
    motionFingerprint: fps.motion,
    motionStale,
    takeFromOtherPicture: pictureChanged,
    motionBusy: Boolean(opts.motionBusy),
    motionFailed,
    motionAccepted,
    motionAcceptedStale,
    motionState,
    retryLocked,
    camerasExhausted,
    currentAttempt,
    lastSuccessTake,
    lastFail,
    visibleTake,
    acceptedTake,
    videoApproved: Boolean(run.videoApproved || run.status === 'approved'),
    lipSyncRequired,
    lipSyncReady,
    lipSyncFingerprint: fps.lipsync,
    lipSyncStale,
    lipsyncBusy: Boolean(opts.lipsyncBusy),
    lipsyncFailed,
    qaReady,
    qualityPassed,
    mixReady,
    mixStale,
    mixBusy: Boolean(opts.mixBusy),
    mixFailed: Boolean(opts.mixFailed),
    finalReady,
    finalArtifactReady,
    durationMismatch,
    i2vSeconds: providerDurationOf(opts.state, opts.shot),
    blockingReason,
    nextAction: stage,
    fps,
    stamp,
    lastFailHash,
    execution: exec,
  };
  const next = nextShotProductionCommand(snap);
  snap.nextAction = next.type;
  snap.execution = { ...exec, nextAction: next };
  return snap;
}

export function deriveShotProductionStage(opts: {
  intent: ShotProductionIntent;
  spoken: boolean;
  blockingReason?: string;
  pictureRejected?: boolean;
  picturePendingApproval?: boolean;
  keyframeReady?: boolean;
  keyframeApproved?: boolean;
  voiceReady?: boolean;
  motionReady?: boolean;
  motionUsable?: boolean;
  motionAccepted?: boolean;
  motionBusy?: boolean;
  motionFailed?: boolean;
  videoApproved?: boolean;
  qaReady?: boolean;
  lipSyncReady?: boolean;
  lipsyncBusy?: boolean;
  lipsyncFailed?: boolean;
  mixReady?: boolean;
  mixBusy?: boolean;
  mixFailed?: boolean;
  finalReady?: boolean;
}): ShotProductionStage {
  if (opts.intent.productionMode === 'MULTI_SPEAKER_BLOCKED' || opts.blockingReason === ORCH_GATE.CHARACTER_AUTHORITY_REQUIRED) {
    return 'BLOCKED';
  }
  if (opts.pictureRejected) return 'NEEDS_VISUAL_REVIEW';
  if (opts.spoken && !opts.voiceReady) return 'VOICE_REQUIRED';
  if (!opts.keyframeReady) return 'VISUAL_REQUIRED';
  if (opts.picturePendingApproval || !opts.keyframeApproved) return 'VISUAL_REVIEW';
  if (opts.motionBusy) return 'VIDEO_PROCESSING';
  if (opts.motionFailed && !opts.motionAccepted && !opts.motionUsable) return 'VIDEO_ERROR';
  if (!opts.motionReady && !opts.motionUsable) return opts.blockingReason === ORCH_GATE.DURATION_MISMATCH ? 'BLOCKED' : 'VIDEO_CONFIRM';
  if (!opts.videoApproved && !opts.motionAccepted) return 'VIDEO_REVIEW';
  if (opts.spoken && !opts.qaReady) return 'LIPSYNC_QA_REQUIRED';
  if (opts.spoken && opts.lipsyncBusy) return 'LIPSYNC_PROCESSING';
  if (opts.spoken && opts.lipsyncFailed) return 'LIPSYNC_ERROR';
  if (opts.spoken && !opts.lipSyncReady) return 'LIPSYNC_CONFIRM';
  if (opts.mixBusy) return 'MIX_PROCESSING';
  if (opts.mixFailed) return 'MIX_ERROR';
  if (opts.finalReady) return 'FINAL_READY';
  if (!opts.mixReady) return 'MIX_REQUIRED';
  if (opts.lipSyncReady) return 'LIPSYNC_READY';
  if (opts.motionReady || opts.motionUsable) return 'VIDEO_READY';
  if (opts.voiceReady) return 'VOICE_READY';
  return 'DRAFT';
}

/** Director + Episode Finish SoT. Do not use videoProductionPreflight.finalReady. */
export function episodeFinishReady(snaps: ShotProductionSnapshot[]) {
  if (!snaps.length) return { allowed: false, blockers: ['NO_SHOTS'], ready: 0, total: 0 };
  const notReady = snaps.filter((s) => !s.finalReady);
  return {
    allowed: notReady.length === 0,
    blockers: notReady.map((s) => `${s.shotId}:NOT_FINAL_READY`),
    ready: snaps.length - notReady.length,
    total: snaps.length,
  };
}

export function snapshotOfShot(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  ttsFiles: Record<string, { url: string; fileName: string }>,
  extras?: {
    motionBusy?: boolean;
    lipsyncBusy?: boolean;
    mixBusy?: boolean;
    mixFailed?: boolean;
    pictureRejected?: boolean;
    pictureUnusable?: boolean;
    artifactPresent?: boolean;
    hasVoiceFile?: (lineId: string) => boolean;
  },
) {
  return buildShotProductionSnapshot({
    state,
    shot,
    ttsFiles,
    hasVoiceFile: extras?.hasVoiceFile,
    pictureRejected: extras?.pictureRejected,
    pictureUnusable: extras?.pictureUnusable,
    motionBusy: extras?.motionBusy,
    lipsyncBusy: extras?.lipsyncBusy,
    mixBusy: extras?.mixBusy,
    mixFailed: extras?.mixFailed,
    artifactPresent: extras?.artifactPresent,
  });
}

export function durationDecision(voiceEndSec: number) {
  const mismatch = dialogueExceedsI2vCap(voiceEndSec);
  return {
    i2vSec: i2vDurationForDialogue(voiceEndSec) as 5 | 10,
    mismatch,
    code: mismatch ? ORCH_GATE.DURATION_MISMATCH : undefined,
  };
}
