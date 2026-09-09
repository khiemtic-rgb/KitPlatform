/** KIT Video Engine V1 — Checkpoint 1. Engine ≠ Project. No Famixa-only branches. */

export const KIT_VIDEO_ENGINE = 'KIT-VIDEO-ENGINE-V1';

export const PRODUCTION_STATES = [
  'DRAFT',
  'SCRIPT_APPROVED',
  'SHOT_PLANNED',
  'SCENE_MASTER_READY',
  'KEYFRAME_GENERATION',
  'KEYFRAME_REVIEW',
  'KEYFRAME_APPROVED',
  'VIDEO_GENERATION',
  'VIDEO_READY',
  'LIPSYNC',
  'LIPSYNC_READY',
  'EDITING',
  'FINAL_QA',
  'FINAL',
] as const;

export type KitVideoProductionState = (typeof PRODUCTION_STATES)[number];

export const SHOT_STATES = [
  'DRAFT',
  'HOLD',
  'READY',
  'KF_GENERATING',
  'KF_REVIEW',
  'KF_APPROVED',
  'I2V_READY',
  'I2V_GENERATING',
  'VIDEO_READY',
  'LIPSYNC_READY',
  'FINAL_SELECTED',
  'FAILED',
] as const;

export type KitVideoShotState = (typeof SHOT_STATES)[number];

const PRODUCTION_NEXT: Record<KitVideoProductionState, KitVideoProductionState[]> = {
  DRAFT: ['SCRIPT_APPROVED'],
  SCRIPT_APPROVED: ['SHOT_PLANNED'],
  SHOT_PLANNED: ['SCENE_MASTER_READY'],
  SCENE_MASTER_READY: ['KEYFRAME_GENERATION'],
  KEYFRAME_GENERATION: ['KEYFRAME_REVIEW'],
  KEYFRAME_REVIEW: ['KEYFRAME_APPROVED', 'KEYFRAME_GENERATION'],
  KEYFRAME_APPROVED: ['VIDEO_GENERATION'],
  VIDEO_GENERATION: ['VIDEO_READY', 'KEYFRAME_APPROVED'],
  VIDEO_READY: ['LIPSYNC'],
  LIPSYNC: ['LIPSYNC_READY', 'VIDEO_READY'],
  LIPSYNC_READY: ['EDITING'],
  EDITING: ['FINAL_QA'],
  FINAL_QA: ['FINAL', 'EDITING'],
  FINAL: [],
};

const SHOT_NEXT: Record<KitVideoShotState, KitVideoShotState[]> = {
  DRAFT: ['READY', 'HOLD'],
  HOLD: ['READY', 'DRAFT'],
  READY: ['KF_GENERATING', 'HOLD'],
  KF_GENERATING: ['KF_REVIEW', 'FAILED'],
  KF_REVIEW: ['KF_APPROVED', 'KF_GENERATING', 'FAILED'],
  KF_APPROVED: ['I2V_READY'],
  I2V_READY: ['I2V_GENERATING'],
  I2V_GENERATING: ['VIDEO_READY', 'FAILED'],
  VIDEO_READY: ['LIPSYNC_READY', 'FINAL_SELECTED'],
  LIPSYNC_READY: ['FINAL_SELECTED'],
  FINAL_SELECTED: [],
  FAILED: ['READY', 'KF_REVIEW', 'I2V_READY'],
};

export function canTransitionProduction(from: string, to: string) {
  if (from === to) return { ok: true as const };
  const allowed = PRODUCTION_NEXT[from as KitVideoProductionState];
  if (!allowed) return { ok: false as const, blocked: `Unknown production state ${from}.` };
  if (!allowed.includes(to as KitVideoProductionState)) {
    return { ok: false as const, blocked: `Illegal production jump ${from} → ${to}.` };
  }
  return { ok: true as const };
}

export function canTransitionShot(from: string, to: string) {
  if (from === to) return { ok: true as const };
  const allowed = SHOT_NEXT[from as KitVideoShotState];
  if (!allowed) return { ok: false as const, blocked: `Unknown shot state ${from}.` };
  if (!allowed.includes(to as KitVideoShotState)) {
    return { ok: false as const, blocked: `Illegal shot jump ${from} → ${to}.` };
  }
  return { ok: true as const };
}

export const RUN_STATUSES = [
  'READY',
  'RUNNING',
  'SUCCEEDED',
  'FAILED',
  'BLOCKED',
  'CANCELLED',
  'PARTIALLY_COMPLETE',
] as const;

export type KitVideoRunStatus = (typeof RUN_STATUSES)[number];

/** One failed shot never fails the production. */
export function productionAfterShotFailure(productionState: string, shots: { state: string; failed?: boolean }[]) {
  const failed = shots.filter((s) => s.state === 'FAILED' || s.failed).length;
  const runStatus: KitVideoRunStatus =
    failed > 0 && failed < shots.length
      ? 'PARTIALLY_COMPLETE'
      : failed === shots.length && shots.length > 0
        ? 'FAILED'
        : 'READY';
  return {
    productionState,
    runStatus,
    episodeFailed: false,
    episode: runStatus,
    isolated: shots.filter((s) => s.state === 'FAILED' || s.failed).map((_, i) => i),
    othersContinue: failed < shots.length,
  };
}

export function holdIfNoAction(hasAction: boolean): KitVideoShotState {
  return hasAction ? 'READY' : 'HOLD';
}

export function canSubmitProvider(shotState: string) {
  const s = (shotState || '').toUpperCase();
  return s !== 'HOLD' && s !== 'DRAFT' && s !== '';
}

export function creditGate(status: string, confirmed: boolean) {
  const s = (status || '').toUpperCase();
  if (s === 'PRECHECK' || !s) return confirmed ? 'READY' : 'PRECHECK';
  if (s === 'READY') return confirmed ? 'SUBMITTED' : 'CONFIRM_REQUIRED';
  if (s === 'CONFIRM_REQUIRED') return confirmed ? 'SUBMITTED' : 'CONFIRM_REQUIRED';
  return s;
}

export function classifyRetry(failureCode?: string | null, error?: string | null) {
  const code = (failureCode || '').toUpperCase();
  const err = (error || '').toUpperCase();
  if (code.includes('BAD_OUTPUT') || err.includes('BAD_OUTPUT')) return 'REPAIR_REQUIRED' as const;
  if (code.includes('INVALID') || err.includes('INVALID INPUT') || err.includes('INVALID_INPUT'))
    return 'NON_RETRYABLE' as const;
  if (err.includes('TIMEOUT') || err.includes('NETWORK') || code.includes('TIMEOUT') || code.includes('UNAVAILABLE'))
    return 'RETRYABLE' as const;
  return 'UNKNOWN' as const;
}

export type KitVideoMediaSnapshot = {
  providerStatus?: string | null;
  outputUrl?: string | null;
  httpStatus?: number | null;
  fileExists?: boolean | null;
  readable?: boolean | null;
  containerOk?: boolean | null;
  probeOk?: boolean | null;
  probeError?: string | null;
  durationSec?: number | null;
  width?: number | null;
  height?: number | null;
};

export function evaluateMedia(snap: KitVideoMediaSnapshot) {
  const provider = (snap.providerStatus || '').toUpperCase();
  const url = (snap.outputUrl || '').trim();
  const reasons: string[] = [];
  if ((snap.httpStatus === 200 || snap.httpStatus === 201) && provider !== 'SUCCEEDED') {
    reasons.push('HTTP 200 không phải SUCCESS.');
  }
  if (provider !== 'SUCCEEDED') reasons.push(`Provider status ${provider} ≠ SUCCEEDED.`);
  if (!url) reasons.push('SUCCEEDED nhưng không có Output URL.');
  if (snap.fileExists === false) reasons.push('File không tồn tại.');
  if (snap.readable === false) reasons.push('File không đọc được.');
  if (snap.probeOk === false) reasons.push(snap.probeError || 'Download/probe lỗi.');
  if (snap.containerOk === false) reasons.push('Media container không hợp lệ.');
  if (snap.durationSec != null && snap.durationSec <= 0) reasons.push('Duration không hợp lệ.');
  if ((snap.width != null && snap.width < 16) || (snap.height != null && snap.height < 16)) {
    reasons.push('Resolution không hợp lệ.');
  }
  const ok =
    provider === 'SUCCEEDED' &&
    url.length > 0 &&
    snap.fileExists !== false &&
    snap.readable !== false &&
    snap.probeOk !== false &&
    snap.containerOk !== false &&
    !(snap.durationSec != null && snap.durationSec <= 0) &&
    !(snap.width != null && snap.width < 16) &&
    !(snap.height != null && snap.height < 16);
  return { ok, shotOutcome: ok ? 'VIDEO_READY' : 'FAILED', reasons };
}

export type KitVideoJobRecord = {
  jobId: string;
  idempotencyKey: string;
  status: string;
  attempts: { attemptNo: number; status: string }[];
  providerTasks: { providerTaskId: string; providerStatus: string; failureCode?: string }[];
  persisted: true;
};

/** Same IdempotencyKey never creates a second paid job. */
export function applyIdempotency(
  existing: KitVideoJobRecord | undefined,
  key: string,
  confirmed: boolean,
): { job: KitVideoJobRecord; created: boolean; attemptAdded: boolean } {
  if (existing && existing.idempotencyKey === key) {
    return { job: existing, created: false, attemptAdded: false };
  }
  return {
    job: {
      jobId: `job-${key}`,
      idempotencyKey: key,
      status: confirmed ? 'SUBMITTED' : 'CONFIRM_REQUIRED',
      attempts: confirmed ? [{ attemptNo: 1, status: 'RUNNING' }] : [],
      providerTasks: [],
      persisted: true,
    },
    created: true,
    attemptAdded: confirmed,
  };
}

export function retryCreatesNewAttempt(job: KitVideoJobRecord): KitVideoJobRecord {
  const next = job.attempts.length === 0 ? 1 : Math.max(...job.attempts.map((a) => a.attemptNo)) + 1;
  return {
    ...job,
    status: 'SUBMITTED',
    attempts: [...job.attempts, { attemptNo: next, status: 'RUNNING' }],
  };
}

export function jobSurvivesBrowserClose(job: KitVideoJobRecord) {
  return job.persisted === true && !!job.jobId;
}

export function formatShotBoard(shot: {
  shotCode: string;
  state: string;
  lastProvider?: string | null;
  lastFailureCode?: string | null;
}) {
  const label =
    shot.state === 'I2V_GENERATING'
      ? 'VIDEO GENERATING'
      : shot.state === 'VIDEO_READY'
        ? 'VIDEO READY'
        : shot.state;
  const lines = [shot.shotCode, label];
  if (shot.state === 'FAILED' && shot.lastFailureCode) {
    lines.push(`${shot.lastProvider || 'Provider'}: ${shot.lastFailureCode}`);
  }
  return lines.filter((l) => l !== 'HTTP 200 = Success').join('\n');
}

export type KitVideoProject = {
  id: string;
  projectCode: string;
  name: string;
  brandCode: string;
  status: string;
  visual: Record<string, unknown>;
  rules: Record<string, unknown>;
  universes: KitVideoUniverse[];
};

export type KitVideoUniverse = {
  id: string;
  universeCode: string;
  name: string;
  status: string;
  world: Record<string, unknown>;
};

export type KitVideoProduction = {
  id: string;
  projectCode: string;
  universeCode: string;
  productionCode: string;
  title: string;
  state: KitVideoProductionState | string;
  runStatus?: KitVideoRunStatus | string;
  seriesBuildId?: string | null;
  shots: {
    shotCode: string;
    state: string;
    failed: boolean;
    lastProvider?: string | null;
    lastFailureCode?: string | null;
  }[];
  updatedAt: string;
};
