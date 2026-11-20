/** Director Production Timing — Production OS authority only. */

export const DIRECTOR_PRODUCTION_TIMING_ID = 'FAMIXA_DIRECTOR_TIMING_UI_V1';
export const DIRECTOR_PRODUCTION_TIMING_UNDECIDED = 'Chưa quyết định';
export const DIRECTOR_PRODUCTION_TIMING_OPTIONS = [5, 10] as const;

export type DirectorProductionDurationSec = (typeof DIRECTOR_PRODUCTION_TIMING_OPTIONS)[number];

export type DirectorSpeechInterval = {
  speechStartSec: number;
  speechEndSec: number;
};

export type DirectorProductionTiming = {
  durationSec?: number | null;
  timingSource?: string | null;
  decisionType?: string | null;
  timingStatus?: string | null;
  performanceSnapshotId?: string | null;
  speechStartSec?: number | null;
  speechEndSec?: number | null;
};

/**
 * A speech interval exists only when the director supplied both ends.
 * Picture duration, a computed lead-in, and a one-sided value are not that interval.
 */
export function explicitDirectorSpeechInterval(row?: {
  speechStartSec?: number | null;
  speechEndSec?: number | null;
  timingSource?: string | null;
  timingStatus?: string | null;
} | null): DirectorSpeechInterval | null {
  if (!row) return null;
  const source = (row.timingSource || '').trim();
  const status = (row.timingStatus || '').trim();
  const director = source === 'DIRECTOR' || (source.length === 0 && status === 'AUTHORITATIVE');
  if (!director) return null;
  const start = row.speechStartSec;
  const end = row.speechEndSec;
  if (typeof start !== 'number' || typeof end !== 'number') return null;
  if (!Number.isFinite(start) || !Number.isFinite(end) || start < 0 || !(end > start)) return null;
  return { speechStartSec: start, speechEndSec: end };
}

export function isAuthoritativeDirectorTiming(row?: DirectorProductionTiming | null): boolean {
  const sec = Number(row?.durationSec);
  if (!Number.isFinite(sec) || sec <= 0) return false;
  const source = (row?.timingSource || '').trim();
  const decision = (row?.decisionType || '').trim();
  if (source && decision) {
    return source === 'DIRECTOR' && decision === 'EXPLICIT_PRODUCTION_DECISION';
  }
  return (row?.timingStatus || '').trim() === 'AUTHORITATIVE';
}

export function formatDirectorProductionDuration(row?: DirectorProductionTiming | null): string {
  if (!isAuthoritativeDirectorTiming(row)) return DIRECTOR_PRODUCTION_TIMING_UNDECIDED;
  const sec = Number(row?.durationSec);
  if (sec === 5) return '5 giây';
  if (sec === 10) return '10 giây';
  return `${sec} giây`;
}

export function directorTimingFromPreview(preview?: {
  durationSec?: number | null;
  timingStatus?: string | null;
  performanceSnapshotId?: string | null;
  speechStartSec?: number | null;
  speechEndSec?: number | null;
} | null): DirectorProductionTiming {
  const authoritative = (preview?.timingStatus || '').trim() === 'AUTHORITATIVE';
  return {
    durationSec: preview?.durationSec,
    timingStatus: preview?.timingStatus,
    timingSource: authoritative ? 'DIRECTOR' : undefined,
    decisionType: authoritative ? 'EXPLICIT_PRODUCTION_DECISION' : undefined,
    performanceSnapshotId: preview?.performanceSnapshotId,
    speechStartSec: authoritative ? preview?.speechStartSec ?? null : null,
    speechEndSec: authoritative ? preview?.speechEndSec ?? null : null,
  };
}

export function directorTimingFromSnapshot(row?: {
  productionDurationSec?: number | null;
  timingSource?: string | null;
  decisionType?: string | null;
  performanceSnapshotId?: string;
  speechStartSec?: number | null;
  speechEndSec?: number | null;
} | null): DirectorProductionTiming {
  return {
    durationSec: row?.productionDurationSec,
    timingSource: row?.timingSource,
    decisionType: row?.decisionType,
    timingStatus:
      (row?.timingSource || '') === 'DIRECTOR' && (row?.decisionType || '') === 'EXPLICIT_PRODUCTION_DECISION'
        ? 'AUTHORITATIVE'
        : undefined,
    performanceSnapshotId: row?.performanceSnapshotId,
    speechStartSec: row?.speechStartSec ?? null,
    speechEndSec: row?.speechEndSec ?? null,
  };
}
