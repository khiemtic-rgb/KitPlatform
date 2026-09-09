/** Motion attempt lifecycle. Derived from attempts[] + optional acceptedTake. Not a third SoT. */

import type { RunwayAttempt } from '../content-famixa-runway-pipe';
import { dataUriHash, lastGenerationFail, sameFailedInput } from '../content-famixa-runway-pipe';
import { resolveTakeUrl } from '../content-famixa-final-source';

export type MotionAttemptStatus = 'SUCCESS' | 'FAILED' | 'PENDING' | 'UNKNOWN';

export type MotionAttemptArtifact = {
  n: number;
  status: MotionAttemptStatus;
  promptHash?: string;
  kfHash?: string;
  pictureRevisionId?: string;
  motionInputFingerprint?: string;
  url?: string;
  createdAt?: string;
  error?: string;
};

export type AcceptedTake = {
  n?: number;
  url: string;
  promptHash?: string;
  kfHash?: string;
  acceptedAt?: string;
};

export type MotionLifecycleRun = {
  acceptedTake?: AcceptedTake;
  runwayAttempts?: RunwayAttempt[];
  takeUrl?: string;
  previewUrl?: string;
  lipsyncUrl?: string;
  lipsynced?: boolean;
  takeHistory?: { url: string; taskId?: string }[];
  failedKfHash?: string;
  failedPromptHash?: string;
  keyframeDataUrl?: string;
  kfSourceHash?: string;
  motionNeedsRemake?: boolean;
  pictureRevisionId?: string;
  pictureRevisionAttemptN?: number;
  turboStatus?: string;
  turboError?: string;
};

export function lastMotionAttemptN(run?: { runwayAttempts?: { n?: number }[] }) {
  let max = 0;
  for (const row of run?.runwayAttempts ?? []) {
    if ((row.n || 0) > max) max = row.n || 0;
  }
  return max;
}

export function motionAttemptAfterPicture(n: number | undefined, epoch?: number) {
  return (n || 0) > (epoch || 0);
}

export type MotionLifecycle = {
  currentAttempt?: MotionAttemptArtifact;
  lastSuccessTake?: MotionAttemptArtifact;
  lastFail?: MotionAttemptArtifact;
  visibleTake?: MotionAttemptArtifact;
  acceptedTake?: MotionAttemptArtifact;
};

export function motionAttemptStatusOf(attempt?: Pick<RunwayAttempt, 'status' | 'failureCode' | 'error'>): MotionAttemptStatus {
  const st = (attempt?.status || '').toUpperCase();
  if (st === 'SUCCEEDED' || st === 'SUCCESS' || st === 'READY') return 'SUCCESS';
  if (st === 'FAILED' || st === 'CANCELLED') return 'FAILED';
  if (/INTERNAL|BAD_OUTPUT/i.test(`${attempt?.failureCode || ''} ${attempt?.error || ''}`)) return 'FAILED';
  if (st === 'PENDING' || st === 'RUNNING' || st === 'PROCESSING' || st === 'SUBMITTED' || st === 'QUEUED') return 'PENDING';
  return 'UNKNOWN';
}

export function motionArtifactOf(attempt: RunwayAttempt): MotionAttemptArtifact {
  const frozen = (attempt as { frozenInput?: { keyframePixelHash?: string; pictureRevisionId?: string } }).frozenInput;
  const frozenHash = frozen?.keyframePixelHash;
  return {
    n: attempt.n,
    status: motionAttemptStatusOf(attempt),
    promptHash: (attempt.promptHash || attempt.exactRequest?.promptHash || '').trim() || undefined,
    kfHash: (attempt.kf?.hash || attempt.source?.hash || attempt.exactRequest?.kfHash || frozenHash || '').trim() || undefined,
    pictureRevisionId: (frozen?.pictureRevisionId || '').trim() || undefined,
    motionInputFingerprint: (attempt.fingerprint || '').trim() || undefined,
    url: (attempt.outputUrl || '').trim() || undefined,
    createdAt: attempt.at,
    error: attempt.error || attempt.failureCode,
  };
}

export function isResolvableSuccessTake(take?: MotionAttemptArtifact) {
  return Boolean(take && take.status === 'SUCCESS' && take.url?.trim());
}

export function currentAttemptOf(attempts?: RunwayAttempt[]) {
  const last = attempts?.at(-1);
  return last ? motionArtifactOf(last) : undefined;
}

/** Last SUCCESS motion task id — never the current FAILED attempt. Prefer accepted take. */
export function lastSuccessMotionTaskId(run?: {
  runwayAttempts?: RunwayAttempt[];
  takeHistory?: { taskId?: string }[];
  turboTaskId?: string;
  acceptedTake?: AcceptedTake;
}) {
  const list = run?.runwayAttempts ?? [];
  const acceptedN = run?.acceptedTake?.n;
  if (acceptedN != null) {
    const accepted = list.find(
      (row) => row.n === acceptedN && motionAttemptStatusOf(row) === 'SUCCESS' && row.taskId?.trim(),
    );
    if (accepted?.taskId?.trim()) return accepted.taskId.trim();
  }
  for (let i = list.length - 1; i >= 0; i--) {
    const row = list[i];
    if (motionAttemptStatusOf(row) === 'SUCCESS' && row?.taskId?.trim()) return row.taskId.trim();
  }
  const failId = (run?.turboTaskId || '').trim();
  const hist = (run?.takeHistory ?? []).map((h) => (h.taskId || '').trim()).filter(Boolean);
  return hist.find((id) => id !== failId) || hist[0] || '';
}

export function lastSuccessTakeOf(attempts?: RunwayAttempt[]) {
  const list = attempts ?? [];
  for (let i = list.length - 1; i >= 0; i--) {
    const take = motionArtifactOf(list[i]!);
    if (isResolvableSuccessTake(take)) return take;
  }
  return undefined;
}

/** Latest SUCCESS take frozen on this still. Hash match is not enough across Picture revisions. */
export function samePictureSuccessTakeOf(
  run?: MotionLifecycleRun,
  livePixelHash?: string,
  liveRevisionId?: string,
) {
  const live =
    (livePixelHash || '').trim() || dataUriHash(run?.keyframeDataUrl) || (run?.kfSourceHash || '').trim();
  if (!live) return undefined;
  const liveRev = (liveRevisionId || (run as { pictureRevisionId?: string } | undefined)?.pictureRevisionId || '').trim();
  const list = run?.runwayAttempts ?? [];
  for (let i = list.length - 1; i >= 0; i--) {
    const take = motionArtifactOf(list[i]!);
    if (!isResolvableSuccessTake(take)) continue;
    if ((take.kfHash || '').trim() !== live) continue;
    const takeRev = (take.pictureRevisionId || '').trim();
    if (liveRev && takeRev && takeRev !== liveRev) continue;
    if (liveRev && !takeRev) continue;
    return take;
  }
  return undefined;
}

/** Desk player. Same-picture SUCCESS only. Unstamped last take is not current on a known still. */
export function playableMotionTakeOf(run?: MotionLifecycleRun, livePixelHash?: string, liveRevisionId?: string) {
  const same = samePictureSuccessTakeOf(run, livePixelHash, liveRevisionId);
  if (same?.url) return same;
  const liveRev = (liveRevisionId || run?.pictureRevisionId || '').trim();
  if (liveRev) return undefined;
  const last = lastSuccessTakeOf(run?.runwayAttempts);
  if (!last?.url) return undefined;
  const live =
    (livePixelHash || '').trim() || dataUriHash(run?.keyframeDataUrl) || (run?.kfSourceHash || '').trim();
  const takeHash = (last.kfHash || '').trim();
  if (takeHash && live && takeHash !== live) return undefined;
  if (!takeHash && live && run?.motionNeedsRemake) return undefined;
  return last;
}

export function mergeMotionAttempts(remote?: RunwayAttempt[], local?: RunwayAttempt[]) {
  const map = new Map<string, RunwayAttempt>();
  const keyOf = (row: RunwayAttempt, i: number) =>
    ((row.taskId || (row as { attemptId?: string }).attemptId || `n:${row.n ?? i}`) as string).trim();
  let i = 0;
  for (const row of [...(remote ?? []), ...(local ?? [])]) {
    const k = keyOf(row, i++);
    const prev = map.get(k);
    if (!prev) {
      map.set(k, row);
      continue;
    }
    const prevUrl = (prev.outputUrl || '').trim();
    const nextUrl = (row.outputUrl || '').trim();
    map.set(k, {
      ...prev,
      ...row,
      outputUrl: nextUrl || prevUrl,
      kf: row.kf || prev.kf,
      source: row.source || prev.source,
      exactRequest: row.exactRequest || prev.exactRequest,
    });
  }
  return [...map.values()].sort((a, b) => (a.n || 0) - (b.n || 0) || String(a.at || '').localeCompare(String(b.at || '')));
}

export function lastFailOf(attempts?: RunwayAttempt[]) {
  const list = attempts ?? [];
  for (let i = list.length - 1; i >= 0; i--) {
    const take = motionArtifactOf(list[i]!);
    if (take.status === 'FAILED') return take;
  }
  return undefined;
}

export function acceptedTakeOf(run?: { acceptedTake?: AcceptedTake }): MotionAttemptArtifact | undefined {
  const url = (run?.acceptedTake?.url || '').trim();
  if (!url) return undefined;
  return {
    n: run?.acceptedTake?.n ?? 0,
    status: 'SUCCESS',
    promptHash: run?.acceptedTake?.promptHash,
    kfHash: run?.acceptedTake?.kfHash,
    url,
    createdAt: run?.acceptedTake?.acceptedAt,
  };
}

function graphTakeArtifact(run?: MotionLifecycleRun): MotionAttemptArtifact | undefined {
  const url = resolveTakeUrl(run);
  if (!url) return undefined;
  const last = lastSuccessTakeOf(run?.runwayAttempts);
  if (last) return { ...last, url: last.url || url };
  return { n: 0, status: 'SUCCESS', url };
}

/** visibleTake = acceptedTake ?? lastSuccessTake. A new FAIL must not hide the last SUCCESS. */
export function visibleTakeOf(run?: MotionLifecycleRun): MotionAttemptArtifact | undefined {
  return acceptedTakeOf(run) ?? lastSuccessTakeOf(run?.runwayAttempts) ?? graphTakeArtifact(run);
}

export function motionLifecycleOf(run?: MotionLifecycleRun): MotionLifecycle {
  return {
    currentAttempt: currentAttemptOf(run?.runwayAttempts),
    lastSuccessTake: lastSuccessTakeOf(run?.runwayAttempts) ?? graphTakeArtifact(run),
    lastFail: lastFailOf(run?.runwayAttempts),
    visibleTake: visibleTakeOf(run),
    acceptedTake: acceptedTakeOf(run),
  };
}

/** Prompt hash of the last SUCCESS take — never a FAILED attempt. */
export function lastSuccessfulTakePromptHashOf(run?: { runwayAttempts?: RunwayAttempt[] }) {
  const list = run?.runwayAttempts ?? [];
  for (let i = list.length - 1; i >= 0; i--) {
    const take = motionArtifactOf(list[i]!);
    if (take.status !== 'SUCCESS') continue;
    const hash = (take.promptHash || '').trim();
    if (hash) return hash;
  }
  return '';
}

export function takeFromOtherPicture(opts: {
  keyframeDataUrl?: string;
  kfSourceHash?: string;
  takeKfHash?: string;
  motionNeedsRemake?: boolean;
  kfApproved?: boolean;
  hasTake?: boolean;
  bindUnstampedTake?: boolean;
  liveRevisionId?: string;
  takeRevisionId?: string;
}) {
  const liveRev = (opts.liveRevisionId || '').trim();
  const takeRev = (opts.takeRevisionId || '').trim();
  if (liveRev && takeRev && liveRev !== takeRev) return true;
  if (liveRev && opts.hasTake && !takeRev) return true;
  const now = dataUriHash(opts.keyframeDataUrl) || (opts.kfSourceHash || '').trim();
  const take = (opts.takeKfHash || '').trim();
  if (now && take && take === now && (!liveRev || !takeRev || liveRev === takeRev)) return false;
  if (opts.motionNeedsRemake) return true;
  if (opts.kfApproved === false && opts.hasTake) return true;
  if (now && take && take !== now) return true;
  if (now && opts.hasTake && !take && (Boolean((opts.kfSourceHash || '').trim()) || opts.bindUnstampedTake)) return true;
  return false;
}

/** New still cannot keep Fal / accepted take as the playing clip. History stays in runwayAttempts. */
export function detachAvAfterPictureChange(run?: MotionLifecycleRun & { previewUrl?: string }) {
  const hadAv = Boolean(
    resolveTakeUrl(run) || run?.previewUrl || run?.takeUrl || run?.lipsyncUrl || run?.lipsynced,
  );
  return {
    lipsyncUrl: undefined,
    lipsynced: false as const,
    lipsyncStatus: undefined,
    lipsyncTaskId: undefined,
    lipsyncSelectionSnapshot: undefined,
    finalSource: undefined,
    acceptedTake: undefined,
    videoApproved: false as const,
    takeUrl: undefined,
    previewUrl: undefined,
    pictureRevisionAttemptN: lastMotionAttemptN(run),
    motionNeedsRemake: hadAv,
  };
}

export function acceptExistingTake(run?: MotionLifecycleRun): { acceptedTake: AcceptedTake } | undefined {
  const visible = visibleTakeOf(run);
  const url = (visible?.url || '').trim();
  if (!url) return undefined;
  return {
    acceptedTake: {
      n: visible?.n,
      url,
      promptHash: visible?.promptHash,
      kfHash: visible?.kfHash,
      acceptedAt: new Date().toISOString(),
    },
  };
}

export type MotionStateId =
  | 'MOTION_READY'
  | 'MOTION_STALE'
  | 'MOTION_FAILED'
  | 'RETRY_AVAILABLE'
  | 'RETRY_LOCKED'
  | 'ACCEPT_EXISTING'
  | 'ACCEPTED'
  | 'ACCEPTED_STALE';

export function deriveMotionState(opts: {
  hasVisibleTake: boolean;
  accepted: boolean;
  stale: boolean;
  currentFailed: boolean;
  retryLocked: boolean;
  motionReady: boolean;
}): MotionStateId {
  if (opts.accepted) return opts.stale ? 'ACCEPTED_STALE' : 'ACCEPTED';
  if (opts.hasVisibleTake && (opts.currentFailed || opts.stale)) return 'ACCEPT_EXISTING';
  if (opts.motionReady) return opts.stale ? 'MOTION_STALE' : 'MOTION_READY';
  if (opts.currentFailed && opts.retryLocked) return 'RETRY_LOCKED';
  if (opts.currentFailed) return 'RETRY_AVAILABLE';
  if (opts.stale) return 'MOTION_STALE';
  return 'MOTION_FAILED';
}

export function motionRemakeEligible(opts: {
  run?: MotionLifecycleRun & { prodSkip?: boolean; keyframeDataUrl?: string };
  kfApproved: boolean;
  hasValidAction: boolean;
  kfHash?: string;
  promptHash?: string;
}) {
  const run = opts.run;
  if (!run?.keyframeDataUrl || !opts.kfApproved || run.prodSkip || !opts.hasValidAction) return false;
  if (sameFailedInput(run, opts.kfHash, opts.promptHash)) return false;
  if (resolveTakeUrl(run)) return true;
  return Boolean(lastGenerationFail(run));
}
