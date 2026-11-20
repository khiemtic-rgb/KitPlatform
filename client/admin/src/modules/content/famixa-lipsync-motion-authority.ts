/** LipSync/Mix duration authority. LATE_RESULT is not a downstream clock. 0 providers. */

import type { RunwayAttempt } from './content-famixa-runway-pipe';
import type { SeriesShotRun } from './content-famixa-series';

export const LIPSYNC_MOTION_AUTHORITY_ID = 'FAMIXA_LIPSYNC_LATE_RESULT_RECONCILIATION_V1';

function roundCentisecond(n: number) {
  return Math.round(Math.max(0, n) * 100) / 100;
}

export function parseMotionArtifactN(artifactId?: string, shotId?: string): number | undefined {
  const raw = (artifactId || '').trim();
  const id = (shotId || '').trim();
  if (!raw || !id) return undefined;
  const prefix = `mot:${id}:`;
  if (!raw.startsWith(prefix)) return undefined;
  const n = Number(raw.slice(prefix.length));
  return Number.isFinite(n) && n > 0 ? n : undefined;
}

export function isLateMotionAttempt(row?: Pick<RunwayAttempt, 'resultClass' | 'status'>): boolean {
  const rc = (row?.resultClass || '').toUpperCase();
  const st = (row?.status || '').toUpperCase();
  return rc === 'LATE_RESULT' || st === 'LATE_RESULT';
}

export function isNonLateMotionSuccess(row?: Pick<RunwayAttempt, 'status' | 'resultClass' | 'outputUrl'> & { url?: string }): boolean {
  if (!row || isLateMotionAttempt(row)) return false;
  const st = (row.status || '').toUpperCase();
  if (st !== 'SUCCEEDED' && st !== 'SUCCESS' && st !== 'READY') return false;
  return Boolean(((row as { outputUrl?: string }).outputUrl || (row as { url?: string }).url || '').trim());
}

export function currentNonLateMotionAttempt(attempts?: RunwayAttempt[]): RunwayAttempt | undefined {
  return [...(attempts ?? [])]
    .filter(isNonLateMotionSuccess)
    .sort((a, b) => (b.n || 0) - (a.n || 0))[0];
}

export function lipsyncBoundMotionN(run?: SeriesShotRun, shotId?: string): number | undefined {
  return parseMotionArtifactN(run?.lipsyncFrozenInput?.motionArtifactId, shotId);
}

export function lipsyncBoundMotionAttempt(run?: SeriesShotRun, shotId?: string): RunwayAttempt | undefined {
  const n = lipsyncBoundMotionN(run, shotId);
  if (!n) return undefined;
  return (run?.runwayAttempts ?? []).find((row) => row.n === n);
}

/** Bound motion is still the LipSync source. A later LATE_RESULT does not steal it. */
export function lipsyncBoundMotionStillAuthority(run?: SeriesShotRun, shotId?: string): boolean {
  const bound = lipsyncBoundMotionAttempt(run, shotId);
  if (!bound || isLateMotionAttempt(bound)) return false;
  const st = (bound.status || '').toUpperCase();
  if (st !== 'SUCCEEDED' && st !== 'SUCCESS' && st !== 'READY') return false;
  const current = currentNonLateMotionAttempt(run?.runwayAttempts);
  if (!current) return true;
  return (current.n || 0) === (bound.n || 0);
}

export function lipsyncBoundDurationSec(run?: SeriesShotRun, shotId?: string): number {
  if (!lipsyncBoundMotionStillAuthority(run, shotId)) return 0;
  const row = lipsyncBoundMotionAttempt(run, shotId);
  const dur = Number(row?.duration) || Number(row?.exactRequest?.duration);
  return dur > 0 ? roundCentisecond(dur) : 0;
}

function attemptPlayableDurationSec(row?: RunwayAttempt): number {
  const dur = Number(row?.duration) || Number((row as { exactRequest?: { duration?: number } })?.exactRequest?.duration);
  return dur > 0 ? roundCentisecond(dur) : 0;
}

/** Bound take is shorter than spoken floor — the 4s file cannot hold the line. */
export function boundMotionClipsSpeech(run?: SeriesShotRun, shotId?: string, floorSec = 0): boolean {
  const bound = lipsyncBoundDurationSec(run, shotId);
  const floor = Number(floorSec) || 0;
  return bound > 0 && floor > bound + 0.021;
}

function isSuccessAttempt(row?: RunwayAttempt) {
  const st = (row?.status || '').toUpperCase();
  return st === 'SUCCEEDED' || st === 'SUCCESS' || st === 'READY';
}

function attemptHasPlayablePointer(row?: RunwayAttempt) {
  return Boolean(
    ((row as { outputUrl?: string } | undefined)?.outputUrl || row?.taskId || '').trim(),
  );
}

function isCoveringPlayableAttempt(row?: RunwayAttempt) {
  return Boolean(row && attemptHasPlayablePointer(row) && (isSuccessAttempt(row) || isLateMotionAttempt(row)));
}

/** Real 6s file, even when the graph stamped the 4s take as duration 5. */
export function playbackSixSecondTake(run?: SeriesShotRun): RunwayAttempt | undefined {
  return [...(run?.runwayAttempts ?? [])]
    .filter((row) => isCoveringPlayableAttempt(row) && attemptPlayableDurationSec(row) >= 5.9)
    .sort((a, b) => attemptPlayableDurationSec(b) - attemptPlayableDurationSec(a) || (b.n || 0) - (a.n || 0))[0];
}

function laterPlayableThanBound(run?: SeriesShotRun, shotId?: string): RunwayAttempt | undefined {
  const boundN = lipsyncBoundMotionN(run, shotId) || 0;
  return [...(run?.runwayAttempts ?? [])]
    .filter((row) => (row.n || 0) > boundN && isCoveringPlayableAttempt(row))
    .sort((a, b) => attemptPlayableDurationSec(b) - attemptPlayableDurationSec(a) || (b.n || 0) - (a.n || 0))[0];
}

/** Desk asked for a covering take: 6s file, later LATE take, or acceptedTake newer than the 4s bind. */
export function speechCoveringMotionAttempt(run?: SeriesShotRun, shotId?: string, floorSec = 0): RunwayAttempt | undefined {
  const six = playbackSixSecondTake(run);
  if (six) return six;
  const later = laterPlayableThanBound(run, shotId);
  if (later) return later;
  const boundN = lipsyncBoundMotionN(run, shotId) || 0;
  const acceptedN = Number(run?.acceptedTake?.n) || 0;
  if (acceptedN > boundN) {
    const hit = (run?.runwayAttempts ?? []).find((row) => row.n === acceptedN && isCoveringPlayableAttempt(row));
    if (hit) return hit;
    const url = (run?.acceptedTake?.url || '').trim();
    const taskId = (run?.turboTaskId || '').trim();
    if (url || taskId) {
      return {
        n: acceptedN,
        status: 'SUCCEEDED',
        outputUrl: url || undefined,
        taskId: taskId || undefined,
      } as RunwayAttempt;
    }
  }
  const floor = Number(floorSec) || 0;
  if (floor > 0) {
    const byFloor = [...(run?.runwayAttempts ?? [])]
      .filter((row) => {
        if (!isCoveringPlayableAttempt(row)) return false;
        if ((row.n || 0) === boundN && attemptPlayableDurationSec(row) + 0.021 < 5.9) return false;
        return attemptPlayableDurationSec(row) + 0.021 >= floor;
      })
      .sort((a, b) => attemptPlayableDurationSec(b) - attemptPlayableDurationSec(a) || (b.n || 0) - (a.n || 0))[0];
    if (byFloor) return byFloor;
  }
  return undefined;
}

export function lipsyncBoundMotionFingerprint(run?: SeriesShotRun, shotId?: string): string | undefined {
  if (!lipsyncBoundMotionStillAuthority(run, shotId)) return undefined;
  const fp = (run?.lipsyncFrozenInput?.motionFingerprint || '').trim();
  return fp || undefined;
}

export function lipsyncBoundArtifactId(run?: SeriesShotRun, shotId?: string): string | undefined {
  if (!lipsyncBoundMotionStillAuthority(run, shotId)) return undefined;
  return (run?.lipsyncFrozenInput?.motionArtifactId || '').trim() || undefined;
}

export function takeUrlBelongsToLateResult(run?: SeriesShotRun): boolean {
  const url = (run?.takeUrl || run?.previewUrl || '').trim();
  if (!url) return false;
  const hit = (run?.runwayAttempts ?? []).find((row) => (row.outputUrl || '').trim() === url);
  return Boolean(hit && isLateMotionAttempt(hit));
}

export const LIPSYNC_BOUND_SOURCE_AUTHORITY_ID = 'FAMIXA_SH01_LIPSYNC_BOUND_SOURCE_AUTHORITY_V1';

export type ProductionVideoSourceKind = 'lipsync' | 'bound_motion' | 'motion';

export type CurrentProductionVideoSource = {
  kind: ProductionVideoSourceKind;
  url?: string;
  motionUrl?: string;
  motionN?: number;
  motionArtifactId?: string;
  motionTaskId?: string;
  lipsyncTaskId?: string;
  usesBoundMotion: boolean;
  usesSpeechCoveringTake?: boolean;
};

function trimUrl(v?: string) {
  return (v || '').trim() || undefined;
}

/** Freeze exists but a newer non-late motion is CURRENT — old LipSync bind is released. */
export function lipsyncBindReleasedByNewerMotion(run?: SeriesShotRun, shotId?: string): boolean {
  const n = lipsyncBoundMotionN(run, shotId);
  if (!n) return false;
  return !lipsyncBoundMotionStillAuthority(run, shotId);
}

/** Proven LipSync output stays current unless a newer CURRENT motion released the bind. */
export function currentSuccessfulLipsyncPlayable(opts: {
  run?: SeriesShotRun;
  shotId?: string;
  lipsyncPlayableUrl?: string;
}): boolean {
  const lip = trimUrl(opts.lipsyncPlayableUrl);
  if (!lip) return false;
  const claimed = Boolean(opts.run?.lipsynced || trimUrl(opts.run?.lipsyncTaskId));
  if (!claimed) return false;
  return !lipsyncBindReleasedByNewerMotion(opts.run, opts.shotId);
}

/**
 * Desk preview only. Current successful LipSync beats stale Final / bound Motion.
 * Does not write graph, Final, acceptedTake, or Motion.
 */
export function resolveCurrentLipsyncPreviewBinding(opts: {
  run?: SeriesShotRun;
  shotId?: string;
  lipsyncPlayableUrl?: string;
  falFileMissing?: boolean;
  takeUrl?: string;
  finalUrl?: string;
  speechFloorSec?: number;
}): { lipsyncUrl?: string; takeUrl?: string; finalUrl?: string; holdLipsync: boolean } {
  const released = lipsyncBindReleasedByNewerMotion(opts.run, opts.shotId);
  const claimed = Boolean(opts.run?.lipsynced && trimUrl(opts.run?.lipsyncTaskId));
  const hold = claimed && !opts.falFileMissing && !released;
  const playable = trimUrl(opts.lipsyncPlayableUrl);
  if (hold && !playable) {
    return { lipsyncUrl: undefined, takeUrl: undefined, finalUrl: undefined, holdLipsync: true };
  }
  const source = resolveCurrentProductionVideoSource({
    run: opts.run,
    shotId: opts.shotId,
    lipsyncPlayableUrl: opts.lipsyncPlayableUrl,
    fallbackMotionUrl: opts.takeUrl,
    speechFloorSec: opts.speechFloorSec,
  });
  if (source.usesSpeechCoveringTake && source.kind !== 'lipsync') {
    return {
      lipsyncUrl: undefined,
      takeUrl: trimUrl(opts.takeUrl) || trimUrl(source.url),
      finalUrl: undefined,
      holdLipsync: false,
    };
  }
  const lipsyncUrl =
    source.kind === 'lipsync' ? trimUrl(source.url) : hold ? playable : undefined;
  if (lipsyncUrl || hold) {
    return { lipsyncUrl, takeUrl: undefined, finalUrl: undefined, holdLipsync: true };
  }
  return {
    lipsyncUrl: undefined,
    takeUrl: trimUrl(opts.takeUrl),
    finalUrl: trimUrl(opts.finalUrl),
    holdLipsync: false,
  };
}

/** LipSync CURRENT bind is the only motion authority. LATE acceptedTake/lastSuccess cannot steal it. */
export function resolveCurrentProductionVideoSource(opts: {
  run?: SeriesShotRun;
  shotId?: string;
  lipsyncPlayableUrl?: string;
  fallbackMotionUrl?: string;
  fallbackMotionN?: number;
  fallbackMotionTaskId?: string;
  speechFloorSec?: number;
}): CurrentProductionVideoSource {
  const run = opts.run;
  const shotId = opts.shotId;
  const released = lipsyncBindReleasedByNewerMotion(run, shotId);
  const lip = !released ? trimUrl(opts.lipsyncPlayableUrl) : undefined;
  const claimed = Boolean(run?.lipsynced || trimUrl(run?.lipsyncTaskId));
  const bound = lipsyncBoundMotionStillAuthority(run, shotId);
  const row = bound ? lipsyncBoundMotionAttempt(run, shotId) : undefined;
  const boundUrl = trimUrl(row?.outputUrl);
  const boundId = bound ? lipsyncBoundArtifactId(run, shotId) : undefined;
  /** Current Fal SUCCESS (6s A+V) beats a mute covering take. Cover only when LipSync is not playable. */
  if (lip && claimed && !released) {
    return {
      kind: 'lipsync',
      url: lip,
      motionUrl: boundUrl || trimUrl(opts.fallbackMotionUrl),
      motionN: row?.n || opts.fallbackMotionN,
      motionArtifactId: boundId,
      motionTaskId: trimUrl(row?.taskId) || trimUrl(opts.fallbackMotionTaskId),
      lipsyncTaskId: trimUrl(run?.lipsyncTaskId),
      usesBoundMotion: bound,
      usesSpeechCoveringTake: false,
    };
  }
  const cover =
    opts.speechFloorSec != null ? speechCoveringMotionAttempt(run, shotId, opts.speechFloorSec) : undefined;
  if (cover) {
    const url = trimUrl((cover as { outputUrl?: string }).outputUrl);
    return {
      kind: 'motion',
      url,
      motionUrl: url,
      motionN: cover.n,
      motionArtifactId: shotId && cover.n ? `mot:${shotId}:${cover.n}` : undefined,
      motionTaskId: trimUrl(cover.taskId),
      lipsyncTaskId: undefined,
      usesBoundMotion: false,
      usesSpeechCoveringTake: true,
    };
  }
  if (bound && (boundUrl || row?.n)) {
    return {
      kind: lip ? 'lipsync' : 'bound_motion',
      url: lip || boundUrl,
      motionUrl: boundUrl,
      motionN: row?.n,
      motionArtifactId: boundId,
      motionTaskId: trimUrl(row?.taskId),
      lipsyncTaskId: trimUrl(run?.lipsyncTaskId),
      usesBoundMotion: true,
    };
  }
  return {
    kind: 'motion',
    url: trimUrl(opts.fallbackMotionUrl),
    motionUrl: trimUrl(opts.fallbackMotionUrl),
    motionN: opts.fallbackMotionN,
    motionArtifactId: undefined,
    motionTaskId: trimUrl(opts.fallbackMotionTaskId),
    lipsyncTaskId: trimUrl(run?.lipsyncTaskId),
    usesBoundMotion: false,
  };
}
