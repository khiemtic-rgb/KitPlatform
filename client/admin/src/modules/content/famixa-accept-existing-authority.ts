/**
 * AUTHORITY-STABILIZATION-01C
 * acceptedTake ≠ official accept authority
 *
 * Accept Existing may keep an in-memory compatibility object.
 * It does not create Production OS ACCEPTED and does not authorize official preview.
 * Official accept is Production OS ACCEPT with a VIDEO_TAKE or MIX ArtifactId, or REFUSE.
 */

export const ACCEPT_EXISTING_AUTHORITY_NEGATIVE = 'acceptedTake ≠ official accept authority';

export const OFFICIAL_ACCEPT_UNAVAILABLE = 'ACCEPT · UNAVAILABLE · không có VIDEO_TAKE / MIX chính thức';

/** Accept Existing click. Compatibility notice only. Does not POST and does not stamp acceptedTake. */
export const ACCEPT_EXISTING_NON_AUTHORITY_NOTE =
  'ACCEPT_EXISTING · NON-AUTHORITY · không tạo ProductionArtifact / ProductionBinding';

/** Explicit non-authority. Present URLs and artifact ids do not change this record. */
export const ACCEPT_EXISTING_AUTHORITY_EFFECT = {
  classification: 'LEGACY_COMPATIBILITY' as const,
  createsProductionArtifact: false as const,
  createsProductionBinding: false as const,
  refreezesInput: false as const,
  officialPreview: false as const,
  promotesGraphUrl: false as const,
  promotesIdbOrFinalStore: false as const,
};

export function acceptExistingAuthorityEffect(_input?: {
  acceptedTakeUrl?: string | null;
  takeUrl?: string | null;
  finalStoreUrl?: string | null;
  idbKey?: string | null;
  videoTakeArtifactId?: string | null;
  mixArtifactId?: string | null;
}) {
  return ACCEPT_EXISTING_AUTHORITY_EFFECT;
}

const ARTIFACT_GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Production OS artifact ids only. Graph URLs and acceptedTake.url never qualify. */
export function officialArtifactId(value?: string | null): string | undefined {
  const id = (value || '').trim();
  if (!ARTIFACT_GUID.test(id)) return undefined;
  return id;
}

export type OfficialAcceptDecision =
  | {
      outcome: 'OS_ACCEPT';
      action: 'ACCEPT';
      artifactId: string;
      role: 'VIDEO_TAKE' | 'MIX';
      createsOsAcceptedFromAcceptedTake: false;
    }
  | {
      outcome: 'REFUSE';
      reason: typeof OFFICIAL_ACCEPT_UNAVAILABLE;
      createsOsAcceptedFromAcceptedTake: false;
    };

/**
 * Classifies an OS ArtifactId. Accept Existing does not call this and does not POST.
 * A graph URL never qualifies. MIX wins over VIDEO_TAKE when both are real ids.
 */
export function decideOfficialAccept(input: {
  videoTakeArtifactId?: string | null;
  mixArtifactId?: string | null;
  acceptedTakeUrl?: string | null;
  takeUrl?: string | null;
}): OfficialAcceptDecision {
  void input.acceptedTakeUrl;
  void input.takeUrl;
  const mix = officialArtifactId(input.mixArtifactId);
  if (mix) {
    return {
      outcome: 'OS_ACCEPT',
      action: 'ACCEPT',
      artifactId: mix,
      role: 'MIX',
      createsOsAcceptedFromAcceptedTake: false,
    };
  }
  const video = officialArtifactId(input.videoTakeArtifactId);
  if (video) {
    return {
      outcome: 'OS_ACCEPT',
      action: 'ACCEPT',
      artifactId: video,
      role: 'VIDEO_TAKE',
      createsOsAcceptedFromAcceptedTake: false,
    };
  }
  return {
    outcome: 'REFUSE',
    reason: OFFICIAL_ACCEPT_UNAVAILABLE,
    createsOsAcceptedFromAcceptedTake: false,
  };
}

/** VIDEO_TAKE identity from OS state only. Never a graph URL. */
export function officialVideoTakeArtifactId(input: {
  authoritativeRole?: string | null;
  authoritativeArtifactId?: string | null;
  authoritativeEmpty?: boolean;
  videoArtifactId?: string | null;
  motionArtifactId?: string | null;
}): string | undefined {
  const fromAuthoritative =
    !input.authoritativeEmpty && (input.authoritativeRole || '').trim() === 'VIDEO_TAKE'
      ? officialArtifactId(input.authoritativeArtifactId)
      : undefined;
  return fromAuthoritative || officialArtifactId(input.videoArtifactId) || officialArtifactId(input.motionArtifactId);
}
