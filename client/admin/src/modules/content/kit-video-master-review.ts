/** CHAR-001 Minh Master Review — last human gate before SELECT / APPROVE / LOCK. No new pixels. */

import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import { STRESS_ALLOWED_CANDIDATE, STRESS_CASES } from './kit-video-identity-stress';
import { REQUIRED_VARIANTS } from './kit-video-identity-test';

export const MINH_MASTER_REVIEW_ID = 'CHAR-001_MINH_MASTER_REVIEW_SPEC_V1';
export const MASTER_REF_V1 = 'CHAR-001_MASTER_REFERENCE_V1';
export const REVIEW_ALLOWED_CANDIDATE = STRESS_ALLOWED_CANDIDATE;
export const REVIEW_PARENT = 'MINH-E01-CANDIDATE-004';
export const REVIEW_VARIATION = '004-D';

export const REVIEW_IDENTITY_LABELS: Record<string, string> = {
  FRONT: 'FRONT',
  THREE_QUARTER_LEFT: '3/4 LEFT',
  THREE_QUARTER_RIGHT: '3/4 RIGHT',
  PROFILE: 'PROFILE',
  NEUTRAL: 'NEUTRAL',
  SAD: 'SAD',
  LIGHT_SMILE: 'LIGHT SMILE',
};

export type MasterReviewGate = {
  readable?: boolean;
  hashValid?: boolean;
  visionPass?: boolean;
  productionStill?: boolean;
  candidateP0?: number;
  eligible?: boolean;
  dnaApproved?: boolean;
  identityPass?: boolean;
  identityHave?: number;
  identityP0?: number;
  stressPass?: boolean;
  stressHave?: number;
  stressP0?: number;
  st10Pass?: boolean;
  lockedMaster?: boolean;
  candidateCode?: string;
};

export function isAllowedReviewCandidate(code?: string) {
  return (code || '').toUpperCase() === REVIEW_ALLOWED_CANDIDATE;
}

export function evaluateMasterReviewGate(g: MasterReviewGate) {
  if (!isAllowedReviewCandidate(g.candidateCode)) {
    return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: chỉ MINH-E01-CANDIDATE-004-D.' };
  }
  if (g.lockedMaster) return { ok: false as const, blocked: 'MASTER_LOCKED: V1 không overwrite. Dùng MASTER_CHANGE_REQUEST.' };
  if (g.readable === false) return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: artifact chưa đọc được.' };
  if (g.hashValid === false) return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: HASH_VALID thất bại.' };
  if (g.visionPass === false) return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: Vision chưa PASS.' };
  if (g.productionStill === false) return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: imageType phải PRODUCTION_STILL.' };
  if ((g.candidateP0 ?? 0) > 0) return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: candidate P0 phải = 0.' };
  if (g.eligible === false) {
    return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: candidate bị loại (INELIGIBLE / REJECTED). Identity + Stress mới là cổng Review.' };
  }
  if (g.dnaApproved === false) return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: Visual DNA phải APPROVED.' };
  if (g.identityPass === false || (g.identityHave ?? 0) < REQUIRED_VARIANTS.length || (g.identityP0 ?? 0) > 0) {
    return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: Identity Test phải 7/7 PASS, P0 = 0.' };
  }
  if (g.stressPass === false || (g.stressHave ?? 0) < STRESS_CASES.length || (g.stressP0 ?? 0) > 0 || g.st10Pass === false) {
    return { ok: false as const, blocked: 'MASTER_REVIEW_BLOCKED: Stress Test phải 10/10 PASS, P0 = 0, ST-10 PASS.' };
  }
  return { ok: true as const, autoSelected: false as const, canon: false as const, generate: false as const };
}

export function canDirectorPassReview(status?: string, gate?: MasterReviewGate) {
  const g = evaluateMasterReviewGate(gate || {});
  if (!g.ok) return g;
  if (status && !['PENDING', 'CONDITIONAL'].includes(status)) {
    return { ok: false as const, blocked: 'MASTER_REVIEW: Director PASS chỉ từ PENDING / CONDITIONAL.' };
  }
  return { ok: true as const, next: 'MASTER_REFERENCE_LOCKED' as const, selected: false as const, locked: true as const, autoSelected: false as const };
}

export function canSelectAfterReview(status?: string, gate?: MasterReviewGate) {
  const g = evaluateMasterReviewGate(gate || {});
  if (!g.ok) return g;
  if (status !== 'PASS') return { ok: false as const, blocked: 'MASTER_REVIEW: Chọn Master chỉ sau Director PASS.' };
  return { ok: true as const, masterRef: MASTER_REF_V1, copiedBytes: false as const, autoSelected: false as const };
}

export function canApproveAfterSelect(status?: string, gate?: MasterReviewGate) {
  const g = evaluateMasterReviewGate(gate || {});
  if (!g.ok) return g;
  if (status !== 'SELECTED') return { ok: false as const, blocked: 'MASTER_REVIEW: APPROVE chỉ sau khi đã chọn Master Reference.' };
  return { ok: true as const, locked: false as const };
}

export function canLockAfterApprove(status?: string, gate?: MasterReviewGate) {
  const g = evaluateMasterReviewGate(gate || {});
  if (!g.ok) return g;
  if (!['PASS', 'SELECTED', 'APPROVED'].includes(status || '')) {
    return { ok: false as const, blocked: 'MASTER_REVIEW: LOCK chỉ sau Director PASS.' };
  }
  return { ok: true as const, canon: true as const, version: 'V1' as const, autoSelected: false as const };
}

export function reviewCreatesPixels(action?: string) {
  return action === 'GENERATE' || action === 'REGENERATE';
}

export function noRunwayInMasterReview(blob: string) {
  return !/runway|i2v|lipsync|take-01|SH01-01|phase.?07|film assembly/i.test(blob);
}

export function goldenUntouched(path?: string) {
  return !isLegacyOrGoldenPath(path || '');
}

/** Live REGRESSION line for Director Decision — contract only, not a generate/Runway action. */
export function reviewRegressionStatus(gate?: MasterReviewGate, artifactPath?: string) {
  const g = evaluateMasterReviewGate(gate || {});
  const golden = goldenUntouched(artifactPath || 'kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-004-D.jpg');
  const ok = g.ok && golden && !reviewCreatesPixels('PASS') && noRunwayInMasterReview(MINH_MASTER_REVIEW_ID);
  return {
    ok,
    label: ok ? 'PASS' : 'FAIL',
    golden,
    generate: false as const,
    runway: false as const,
  };
}
