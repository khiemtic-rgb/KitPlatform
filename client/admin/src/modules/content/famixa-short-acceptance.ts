import type { SeriesShotRun } from './content-famixa-series';

export type OsPlaybackIdentity = {
  artifactId?: string | null;
  contentHash?: string | null;
  role?: string | null;
  durationSec?: number | null;
};

/** Preview identity already returned by the shot preview. Duration stays on that artifact. */
export function osPreviewIdentityFromMeta(meta: {
  artifactId?: string | null;
  byteSha256?: string | null;
  role?: string | null;
  durationSec?: number | null;
}): OsPlaybackIdentity {
  return {
    artifactId: meta.artifactId,
    contentHash: meta.byteSha256,
    role: meta.role,
    durationSec: meta.durationSec,
  };
}

/**
 * An OS shot is reviewed only against the resolved OS artifact.
 * A graph URL is not a substitute when that artifact has not resolved.
 */
export function reviewTargetForDecision(
  run: Parameters<typeof playbackReviewTarget>[0],
  os: OsPlaybackIdentity | null | undefined,
  osShot: boolean,
) {
  if (osShot) {
    const id = (os?.artifactId || '').trim();
    return id ? `os:${id}` : '';
  }
  return playbackReviewTarget(run, os);
}

/**
 * Checklist ticks name the file they were marked against.
 * They do not write ACCEPT or REJECT.
 */
export function bindShotChecklist(
  next: NonNullable<SeriesShotRun['shotQa']>,
  target: string,
  hash: string,
  complete: boolean,
): NonNullable<SeriesShotRun['shotQa']> {
  const playback = (target || '').trim();
  const contentHash = (hash || '').trim();
  if (!complete || !playback || (playback.startsWith('os:') && !contentHash)) {
    return {
      ...next,
      reviewedPlaybackId: undefined,
      reviewedContentHash: undefined,
      decision: undefined,
    };
  }
  return {
    ...next,
    reviewedPlaybackId: playback,
    reviewedContentHash: playback.startsWith('os:') ? contentHash : undefined,
    decision: undefined,
  };
}

export const REVIEW_TARGET_MISSING = 'REVIEW_TARGET_MISSING';
export const REVIEW_HASH_MISSING = 'REVIEW_HASH_MISSING';

/**
 * ACCEPT or REJECT is a separate human action for one playback.
 * Completed ticks are not this action.
 */
export function explicitArtifactDecision(input: {
  decision: 'ACCEPT' | 'REJECT';
  target: string;
  hash: string;
  checklist: NonNullable<SeriesShotRun['shotQa']>;
}):
  | { ok: true; qa: NonNullable<SeriesShotRun['shotQa']> }
  | { ok: false; blocker: string; detail: string } {
  const target = (input.target || '').trim();
  const hash = (input.hash || '').trim();
  if (!target) {
    return {
      ok: false,
      blocker: REVIEW_TARGET_MISSING,
      detail: 'Chưa có đúng tệp đang phát để ghi quyết định.',
    };
  }
  if (target.startsWith('os:') && !hash) {
    return {
      ok: false,
      blocker: REVIEW_HASH_MISSING,
      detail: 'Chưa có mã băm của đúng tệp đang phát.',
    };
  }
  return {
    ok: true,
    qa: {
      ...input.checklist,
      reviewedPlaybackId: target,
      reviewedContentHash: target.startsWith('os:') ? hash : undefined,
      decision: input.decision,
    },
  };
}

/** Identity of the file the desk can actually point at. An OS artifact wins over a graph URL. */
export function playbackReviewTarget(
  run?: Pick<SeriesShotRun, 'lipsyncUrl' | 'takeUrl' | 'shotProduction'>,
  os?: OsPlaybackIdentity | null,
) {
  const artifactId = (os?.artifactId || '').trim();
  if (artifactId) return `os:${artifactId}`;
  const file = run?.shotProduction?.assembleFileName?.trim();
  if (file) return `assembly:${file}`;
  const lip = run?.lipsyncUrl?.trim();
  if (lip) return `lipsync:${lip}`;
  const take = run?.takeUrl?.trim();
  if (take) return `take:${take}`;
  return '';
}

export async function sha256Hex(bytes: ArrayBuffer) {
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, '0')).join('');
}

/** Playback bytes must match the hash of the artifact the preview named. */
export async function previewBytesMatch(blob: Blob, expectedSha?: string | null) {
  const expected = (expectedSha || '').trim().toLowerCase();
  if (!expected) return false;
  const live = await sha256Hex(await blob.arrayBuffer());
  return live === expected;
}

/**
 * Spoken quality passes only when the director accepted this playback id.
 * A stored shotQa tick without that id is still an unbound checklist.
 */
export function qualityReviewAccepted(run?: SeriesShotRun, os?: OsPlaybackIdentity | null) {
  if (run?.shotQa?.decision !== 'ACCEPT') return false;
  if (!run.shotQa.action || !run.shotQa.continuity || !run.shotQa.motion || !run.shotQa.voiceFace) return false;
  if (!run.shotQa.lipsyncQuality || !run.shotQa.finalAv) return false;
  const target = playbackReviewTarget(run, os);
  if (!target || run.shotQa.reviewedPlaybackId !== target) return false;
  if (target.startsWith('os:')) {
    const hash = (os?.contentHash || '').trim().toLowerCase();
    const reviewed = (run.shotQa.reviewedContentHash || '').trim().toLowerCase();
    return hash.length > 0 && reviewed === hash;
  }
  return true;
}
