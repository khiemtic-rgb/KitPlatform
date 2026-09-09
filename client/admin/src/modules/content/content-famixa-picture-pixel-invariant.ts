/** Picture new-pixel invariant. Revision is a version number, not pixel identity. */

import { dataUriHash } from './content-famixa-runway-pipe';
import { detachAvAfterPictureChange } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import { nextPictureRevisionId } from './ContentFamixaShotProduction/ShotProductionExecution';

export const PICTURE_DUPLICATE_COPY = 'Ảnh mới không khác ảnh hiện tại. Vui lòng tạo lại.';

export type PictureGenerationKind = 'NEW_PENDING_PICTURE' | 'DUPLICATE_PIXEL' | 'GENERATION_FAILED';
export type PictureApproveKind = 'APPROVE_NEW' | 'DUPLICATE_PIXEL' | 'GENERATION_FAILED';

export type PictureIdentityRun = {
  kfApproved?: boolean;
  kfForceNew?: boolean;
  kfRetryOk?: boolean;
  kfSourceHash?: string;
  pictureRevisionId?: string;
  keyframeDataUrl?: string;
  keyframeFileName?: string;
  takeUrl?: string;
  previewUrl?: string;
  lipsyncUrl?: string;
  lipsynced?: boolean;
  motionNeedsRemake?: boolean;
  acceptedTake?: unknown;
  videoApproved?: boolean;
  finalSource?: unknown;
  runwayAttempts?: { n?: number }[];
  pictureRevisionAttemptN?: number;
};

export function picturePixelHashOfData(dataUrl?: string) {
  return dataUriHash(dataUrl);
}

/** Last approved picture identity. Filename is never used. */
export function currentApprovedPictureHash(run?: PictureIdentityRun) {
  const stamp = (run?.kfSourceHash || '').trim();
  if (!stamp) return '';
  if ((run?.pictureRevisionId || '').trim() || run?.kfApproved) return stamp;
  return '';
}

export function classifyGeneratedPicture(generatedHash: string, approvedHash: string): PictureGenerationKind {
  const next = (generatedHash || '').trim();
  if (!next) return 'GENERATION_FAILED';
  const cur = (approvedHash || '').trim();
  if (cur && next === cur) return 'DUPLICATE_PIXEL';
  return 'NEW_PENDING_PICTURE';
}

export function classifyPictureApprove(incomingHash: string, approvedHash: string): PictureApproveKind {
  const incoming = (incomingHash || '').trim();
  if (!incoming) return 'GENERATION_FAILED';
  const cur = (approvedHash || '').trim();
  if (cur && incoming === cur) return 'DUPLICATE_PIXEL';
  return 'APPROVE_NEW';
}

export function applyPictureGenerationGuard<T extends PictureIdentityRun>(
  run: T,
  generatedDataUrl?: string,
): { kind: PictureGenerationKind; run: T } {
  if (!(generatedDataUrl || '').startsWith('data:image')) {
    return { kind: 'GENERATION_FAILED', run };
  }
  const generatedHash = picturePixelHashOfData(generatedDataUrl);
  const approvedHash = currentApprovedPictureHash(run);
  const kind = classifyGeneratedPicture(generatedHash, approvedHash);
  if (kind !== 'NEW_PENDING_PICTURE') return { kind, run };
  return {
    kind,
    run: {
      ...run,
      keyframeDataUrl: generatedDataUrl,
      kfApproved: false,
      kfForceNew: false,
      kfSourceHash: approvedHash || generatedHash,
      ...detachAvAfterPictureChange(run),
    } as T,
  };
}

export function applyPictureApproveGuard<T extends PictureIdentityRun>(
  run: T,
  shotId: string,
  incomingDataUrl?: string,
): { kind: PictureApproveKind; run: T } {
  const incoming = (incomingDataUrl || '').startsWith('data:image') ? incomingDataUrl : run.keyframeDataUrl;
  const incomingHash = picturePixelHashOfData(incoming);
  const approvedHash = currentApprovedPictureHash(run);
  const kind = classifyPictureApprove(incomingHash, approvedHash);
  if (kind !== 'APPROVE_NEW') return { kind, run };
  return {
    kind,
    run: {
      ...run,
      kfApproved: true,
      kfRetryOk: true,
      kfSourceHash: incomingHash,
      pictureRevisionId: nextPictureRevisionId(shotId, run.pictureRevisionId),
      ...detachAvAfterPictureChange(run),
    } as T,
  };
}
