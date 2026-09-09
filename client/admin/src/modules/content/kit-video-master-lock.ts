/** CHAR-001 Minh Master Reference Lock — Director PASS then LOCK. No new pixels. No auto-promote. */

import { evaluateMasterReviewGate, MASTER_REF_V1, REVIEW_ALLOWED_CANDIDATE, type MasterReviewGate } from './kit-video-master-review';

export const MINH_MASTER_LOCK_ID = 'CHAR-001_MINH_MASTER_REFERENCE_LOCK_IMPLEMENTATION_V1';
export const MASTER_CODE_V1 = 'CHAR-001-MINH-ERA01-MASTER-V1';
export const MASTER_LOCK_STATUS = 'MASTER_REFERENCE_LOCKED';
export const MASTER_SOURCE_STATUS = 'MASTER_REFERENCE_SOURCE';

export type MasterLockInput = MasterReviewGate & {
  reviewStatus?: string;
  directorPass?: boolean;
  rejected?: boolean;
  draftBlocked?: boolean;
  eligibleBlocked?: boolean;
  actor?: string;
  existingSource?: string;
  requestedSource?: string;
  canonActive?: boolean;
};

export function isDirectorActor(actor?: string) {
  const a = (actor || '').trim();
  return a.length > 0 && a.toLowerCase() !== 'anonymous';
}

export function evaluateMasterLock(g: MasterLockInput) {
  if (!isDirectorActor(g.actor)) {
    return { ok: false as const, blocked: 'MASTER_LOCK_UNAUTHORIZED: chỉ Director được khóa Master Reference.' };
  }
  const review = (g.reviewStatus || '').toUpperCase();
  if (g.rejected) return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: candidate bị REJECT.' };
  if (review === 'LOCKED') {
    return { ok: true as const, idempotent: true as const, masterCode: MASTER_CODE_V1, autoPromoted: false as const };
  }
  if (!['PASS', 'SELECTED', 'APPROVED'].includes(review)) {
    return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: Director chưa PASS.' };
  }
  if (g.draftBlocked) return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: candidate đang DRAFT.' };
  if (g.eligibleBlocked) {
    return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: candidate chưa FRONT_RUNNER/ELIGIBLE.' };
  }
  if (g.canonActive && g.existingSource && g.requestedSource && g.existingSource !== g.requestedSource) {
    return { ok: false as const, blocked: 'MASTER_LOCK_CONFLICT: Canon active với source khác. Không overwrite V1.' };
  }
  const gate = evaluateMasterReviewGate(g);
  if (!gate.ok) {
    return { ok: false as const, blocked: gate.blocked.replace('MASTER_REVIEW_BLOCKED', 'MASTER_LOCK_BLOCKED') };
  }
  if (g.directorPass === false) return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: Director chưa PASS.' };
  if (g.visionPass === false) return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: Vision chưa PASS.' };
  if ((g.candidateP0 ?? 0) > 0) return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: candidate P0 phải = 0.' };
  if (g.dnaApproved === false) return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: Visual DNA phải APPROVED.' };
  if (g.identityPass === false || (g.identityHave ?? 0) < 7) {
    return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: Identity Test phải 7/7 PASS.' };
  }
  if (g.stressPass === false || (g.stressHave ?? 0) < 10 || g.st10Pass === false) {
    return { ok: false as const, blocked: 'MASTER_LOCK_BLOCKED: Stress Test phải 10/10 PASS.' };
  }
  return {
    ok: true as const,
    masterCode: MASTER_CODE_V1,
    masterRef: MASTER_REF_V1,
    source: REVIEW_ALLOWED_CANDIDATE,
    autoPromoted: false as const,
    generate: false as const,
  };
}

export function lockIdempotent(existingSource?: string, requestedSource?: string) {
  if (!existingSource) return { ok: true as const, duplicate: false as const };
  if (existingSource === requestedSource) return { ok: true as const, duplicate: false as const, same: true as const };
  return { ok: false as const, blocked: 'MASTER_LOCK_CONFLICT: Canon active với source khác. Không overwrite V1.' };
}

export function rejectMasterMutation(action?: string) {
  const a = (action || '').toUpperCase();
  if (['UPDATE', 'DELETE', 'ARCHIVE', 'OVERWRITE', 'REGENERATE', 'CHANGE_ARTIFACT', 'CHANGE_SHA256', 'CHANGE_SOURCE'].includes(a)) {
    return { ok: false as const, blocked: 'MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST / V2.' };
  }
  return { ok: true as const };
}

export function resolveCanonPointer(characterId?: string, eraId?: string, pointer?: { masterReferenceId?: string; masterCode?: string }) {
  if ((characterId || '') !== 'CHAR-001' || (eraId || '') !== 'ERA-01') return null;
  if (!pointer?.masterReferenceId) return null;
  return { characterId: 'CHAR-001', eraId: 'ERA-01', masterReferenceId: pointer.masterReferenceId, masterCode: pointer.masterCode || MASTER_CODE_V1 };
}

export function lockCreatesPixels(action?: string) {
  return action === 'GENERATE' || action === 'REGENERATE';
}

export function autoPromoteToMaster(_identity: boolean, _stress: boolean, _dna: boolean) {
  return false;
}

export function canLockAsMaster(status?: string, gate?: MasterReviewGate) {
  return evaluateMasterLock({
    ...(gate || {}),
    reviewStatus: status,
    directorPass: ['PASS', 'SELECTED', 'APPROVED', 'LOCKED'].includes((status || '').toUpperCase()),
    actor: 'director',
    candidateCode: gate?.candidateCode || REVIEW_ALLOWED_CANDIDATE,
  });
}
