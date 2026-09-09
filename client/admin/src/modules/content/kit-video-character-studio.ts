export const CHARACTER_STUDIO_V1_ID = 'FAMIXA_CHARACTER_STUDIO_V1';
export const IDENTITY_LOCK_V1_ID = 'FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1';
export const IDENTITY_LOCK_SUITE = 'FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1_REGRESSION';

export const STUDIO_REJECT_REASONS = [
  { id: 'FACE_MISMATCH', label: 'Không giống khuôn mặt' },
  { id: 'HAIR_MISMATCH', label: 'Sai kiểu tóc' },
  { id: 'BODY_PROPORTION', label: 'Sai tỷ lệ cơ thể' },
  { id: 'AGE_MISMATCH', label: 'Sai tuổi' },
  { id: 'CLOTHING_MISMATCH', label: 'Sai trang phục' },
  { id: 'VIEW_INCONSISTENT', label: 'Các góc không đồng nhất' },
  { id: 'FULL_BODY_INVALID', label: 'Toàn thân không đúng' },
  { id: 'QUALITY', label: 'Chất lượng ảnh' },
  { id: 'OTHER', label: 'Khác' },
] as const;

export const STUDIO_GENDERS = [
  { id: 'male', label: 'Nam' },
  { id: 'female', label: 'Nữ' },
  { id: 'other', label: 'Khác' },
];

export function studioStatusTone(status: string) {
  const s = (status || '').toUpperCase();
  if (s === 'CHARACTER_READY' || s === 'CRP_LOCKED') return 'ready';
  if (s === 'CRP_PENDING_REVIEW' || s === 'CRP_APPROVED') return 'review';
  if (s === 'REJECTED' || s === 'FAILED') return 'bad';
  if (s.includes('GENERAT') || s.includes('REPAIR') || s.includes('CHECK')) return 'busy';
  return 'draft';
}

export function studioGenderLabel(gender?: string | null) {
  const g = (gender || '').trim().toLowerCase();
  if (g === 'male' || g === 'nam') return 'Nam';
  if (g === 'female' || g === 'nữ' || g === 'nu') return 'Nữ';
  return gender || '';
}

export function studioStatusDot(status: string) {
  const tone = studioStatusTone(status);
  if (tone === 'ready') return '🟢';
  if (tone === 'review' || tone === 'busy') return '🟡';
  if (tone === 'bad') return '🔴';
  return '⚪';
}

export function rejectReasonValid(code?: string, text?: string) {
  if (!code) return false;
  if (code === 'OTHER') return (text || '').trim().length >= 3;
  return STUDIO_REJECT_REASONS.some((r) => r.id === code);
}

export const UNIFIED_GENERATION_V1_ID = 'FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1';
export const UNIFIED_GENERATION_SUITE = 'FAMIXA_CHARACTER_STUDIO_UNIFIED_GENERATION_V1_REGRESSION';

export function profileValid(input: {
  name?: string;
  age?: number;
  gender?: string;
  styleId?: string;
  description?: string;
  role?: string;
}) {
  return Boolean(
    (input.name || '').trim()
    && (input.age ?? 0) >= 1
    && (input.age ?? 0) <= 120
    && (input.gender || '').trim()
    && (input.description || '').trim().length >= 3,
  );
}

export function studioPendingReview(status?: string) {
  const s = (status || '').toUpperCase();
  return s === 'CRP_PENDING_REVIEW' || s === 'PENDING_REVIEW';
}

export function studioPublicHeadline(row: {
  status?: string;
  statusLabel?: string;
  coverage?: number;
  requiredTotal?: number;
  officialLocked?: boolean;
  canUse?: boolean;
}) {
  const status = (row.status || '').toUpperCase();
  const cov = `${row.coverage ?? 0}/${row.requiredTotal ?? 4}`;
  if (status === 'CHARACTER_READY' || status === 'CRP_LOCKED' || row.officialLocked) {
    return `CHARACTER READY · ${cov} · LOCKED`;
  }
  if (status === 'CRP_APPROVED') return `APPROVED · ${cov}`;
  if (studioPendingReview(status)) return `PENDING REVIEW · ${cov}`;
  if (status === 'REJECTED' || status === 'CRP_REJECTED') return `Không đạt · ${cov}`;
  if ((row.statusLabel || '').includes(' · ')) return row.statusLabel || '';
  return row.statusLabel ? `${row.statusLabel} · ${cov}` : cov;
}

export function studioMayRegenerate(
  status?: string,
  officialLocked?: boolean,
  ageConsistencyStatus?: string,
  crpStale?: boolean,
  slotsNeedVision?: boolean,
) {
  if (officialLocked) return false;
  if (crpStale || slotsNeedVision) return true;
  const s = (status || '').toUpperCase();
  const age = (ageConsistencyStatus || '').toUpperCase();
  return s === 'REJECTED'
    || s === 'CRP_REJECTED'
    || s === 'FAILED'
    || s === 'GENERATION_FAILED'
    || s === 'CONSISTENCY_FAILED'
    || s === 'CRP_PENDING_REVIEW'
    || s === 'PENDING_REVIEW'
    || age === 'FAIL';
}

export function studioSlotVerdictLabel(verdict?: string | null) {
  const v = (verdict || '').toUpperCase();
  if (v === 'PASS') return 'Đạt';
  if (v === 'FAIL' || v === 'REJECTED') return 'Không đạt';
  if (v === 'MISSING') return 'Thiếu';
  if (v === 'NOT_EVALUATED' || !v) return 'Chưa chấm';
  return verdict || '';
}

export function studioSlotTone(verdict?: string | null) {
  const v = (verdict || '').toUpperCase();
  if (v === 'PASS') return 'ready';
  if (v === 'FAIL' || v === 'REJECTED' || v === 'MISSING') return 'bad';
  if (v === 'NOT_EVALUATED') return 'wait';
  return 'draft';
}

export const STUDIO_SCORE_IDENTITY_LABEL = 'Chấm với Master';

export function studioMayScoreIdentity(row?: {
  officialLocked?: boolean;
  coverage?: number;
  mayScoreIdentity?: boolean;
  slots?: { verdict?: string | null }[] | null;
} | null) {
  if (row?.mayScoreIdentity === true) return true;
  if (row?.officialLocked) return false;
  return (row?.coverage ?? 0) >= 4 && studioSlotsNeedVision(row?.slots, row?.officialLocked);
}

export function studioSlotsNeedVision(
  slots?: { verdict?: string | null }[] | null,
  officialLocked?: boolean,
) {
  if (officialLocked) return false;
  return (slots || []).some((s) => {
    const v = (s.verdict || '').toUpperCase();
    return v === 'NOT_EVALUATED' || !v;
  });
}

export function studioSlotsFailed(
  slots?: { verdict?: string | null }[] | null,
  officialLocked?: boolean,
) {
  if (officialLocked) return false;
  return (slots || []).some((s) => (s.verdict || '').toUpperCase() === 'FAIL');
}

export const AGE_CONSISTENCY_V1_ID = 'FAMIXA_CHARACTER_AGE_CONSISTENCY_V1';
export const AGE_GENERATION_INTEGRATION_V1_ID = 'FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1';
export const AGE_GENERATION_INTEGRATION_SUITE = 'FAMIXA_CHARACTER_AGE_GENERATION_INTEGRATION_CHECK_V1_REGRESSION';
export const AGE_GATE_V1_ID = 'FAMIXA_CHARACTER_AGE_GATE_V1';
export const AGE_GATE_SUITE = 'FAMIXA_CHARACTER_AGE_GATE_V1_REGRESSION';

const AGE_POLICY_BANDS = [
  { minAge: 0, maxAge: 3, tolerance: 1 },
  { minAge: 4, maxAge: 7, tolerance: 1 },
  { minAge: 8, maxAge: 12, tolerance: 1 },
  { minAge: 13, maxAge: 17, tolerance: 1 },
  { minAge: 18, maxAge: 29, tolerance: 2 },
  { minAge: 30, maxAge: 49, tolerance: 3 },
  { minAge: 50, maxAge: 69, tolerance: 4 },
  { minAge: 70, maxAge: 200, tolerance: 5 },
] as const;

export function ageExpressionRange(ageYears?: number | null) {
  const age = ageYears ?? 0;
  if (age < 1) return { min: 0, max: 0 };
  const band = AGE_POLICY_BANDS.find((b) => age >= b.minAge && age <= b.maxAge) ?? AGE_POLICY_BANDS[AGE_POLICY_BANDS.length - 1];
  return { min: Math.max(0, age - band.tolerance), max: age + band.tolerance };
}

export function ageConsistencyTone(status?: string | null, officialLocked?: boolean) {
  const s = (status || '').toUpperCase();
  if (officialLocked && s !== 'FAIL') return 'ready';
  if (s === 'PASS') return 'ready';
  if (s === 'WARNING' || s === 'PENDING_REVIEW' || s === 'NOT_EVALUATED') return 'review';
  if (s === 'FAIL') return 'bad';
  if (s === 'BLOCKED') return 'blocked';
  return 'draft';
}

export function ageConsistencyShowsMismatch(status?: string | null, officialLocked?: boolean) {
  return (status || '').toUpperCase() === 'FAIL' && !officialLocked;
}

export function ageConsistencyLabel(status?: string | null, score?: number | null) {
  const s = (status || 'NOT_EVALUATED').toUpperCase();
  if ((s === 'PASS' || s === 'FAIL') && typeof score === 'number') return `${s} · ${score}%`;
  return s;
}

export const APPEARANCE_PROFILE_V1_ID = 'FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1';
export const APPEARANCE_PROFILE_SUITE = 'FAMIXA_CHARACTER_APPEARANCE_PROFILE_V1_REGRESSION';
export const MASTER_REVISION_V1_ID = 'FAMIXA_CHARACTER_MASTER_REVISION_V1';
export const MASTER_REVISION_SUITE = 'FAMIXA_CHARACTER_MASTER_REVISION_V1_REGRESSION';
export const MASTER_REVISION_DIRECTOR_REVIEW_V1_ID = 'FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1';
export const MASTER_REVISION_DIRECTOR_REVIEW_SUITE =
  'FAMIXA_CHARACTER_MASTER_REVISION_DIRECTOR_REVIEW_V1_REGRESSION';

export const MASTER_REVISION_REJECT_REASONS = [
  { id: 'TOO_OLD', label: 'Vẫn quá già' },
  { id: 'APPEARANCE', label: 'Không đúng diện mạo' },
  { id: 'STYLE', label: 'Không đúng phong cách' },
  { id: 'IDENTITY', label: 'Không đúng nhận diện' },
] as const;

export function masterRevisionMayApprove(status?: string | null) {
  return (status || '') === 'MASTER_REVISION_PENDING_REVIEW';
}

export function masterRevisionMayReject(status?: string | null) {
  return (status || '') === 'MASTER_REVISION_PENDING_REVIEW';
}

export function masterRevisionMayLock(status?: string | null) {
  return (status || '') === 'MASTER_REVISION_APPROVED';
}

export function masterRevisionMayRequestNew(status?: string | null, officialLocked?: boolean) {
  return !officialLocked && (status || '') === 'MASTER_REVISION_REJECTED';
}

export function appearanceConsistencyTone(status?: string | null, officialLocked?: boolean) {
  return ageConsistencyTone(status, officialLocked);
}

export function appearanceConsistencyLabel(status?: string | null) {
  return (status || 'NOT_EVALUATED').toUpperCase();
}

export type DirectorGate = 'pass' | 'fail' | 'wait';

export function directorGateMark(gate: DirectorGate) {
  if (gate === 'pass') return '✓';
  if (gate === 'fail') return '✕';
  return '○';
}

export function directorGateText(gate: DirectorGate) {
  if (gate === 'pass') return 'Đạt';
  if (gate === 'fail') return 'Không đạt';
  return 'Chưa chấm';
}

export function directorGateFromStatus(status?: string | null, officialLocked?: boolean): DirectorGate {
  const s = (status || '').toUpperCase();
  if (officialLocked && s !== 'FAIL') return 'pass';
  if (s === 'PASS') return 'pass';
  if (s === 'FAIL') return 'fail';
  return 'wait';
}

export function directorFaceGate(
  slots?: { verdict?: string | null }[] | null,
  officialLocked?: boolean,
): DirectorGate {
  if (officialLocked) return 'pass';
  if (studioSlotsFailed(slots, officialLocked)) return 'fail';
  const list = slots || [];
  if (!list.length || studioSlotsNeedVision(list, officialLocked)) return 'wait';
  if (list.every((s) => (s.verdict || '').toUpperCase() === 'PASS')) return 'pass';
  return 'wait';
}

export function directorStyleGate(visualStyleGate?: string | null, projectReady?: boolean): DirectorGate {
  if (visualStyleGate === 'VALID' || projectReady) return 'pass';
  if ((visualStyleGate || '').toUpperCase() === 'PROJECT_VISUAL_STYLE_NOT_READY') return 'fail';
  return 'wait';
}

export function directorStyleConformanceGate(status?: string | null, officialLocked?: boolean): DirectorGate {
  if (officialLocked) return 'pass';
  const s = (status || '').toUpperCase();
  if (s === 'PASS') return 'pass';
  if (s === 'FAIL') return 'fail';
  return 'wait';
}

export function directorUniverseGate(gate?: string | null, officialLocked?: boolean): DirectorGate {
  if (officialLocked) return 'pass';
  const s = (gate || '').toUpperCase();
  if (s === 'PASS') return 'pass';
  if (s === 'MISMATCH' || s === 'FAIL') return 'fail';
  return 'wait';
}

export function directorStatusLabel(row: {
  status?: string;
  officialLocked?: boolean;
  crpStale?: boolean;
}) {
  const s = (row.status || '').toUpperCase();
  if (row.officialLocked || s === 'CHARACTER_READY' || s === 'CRP_LOCKED') return 'LOCKED';
  if (row.crpStale) return 'STALE';
  if (s === 'CRP_APPROVED' || s === 'MASTER_REVISION_APPROVED') return 'APPROVED';
  if (s === 'REJECTED' || s === 'CRP_REJECTED' || s === 'MASTER_REVISION_REJECTED') return 'REJECTED';
  if (s.includes('GENERAT') || s.includes('REPAIR') || s.includes('CHECK')) return 'GENERATING';
  if (s === 'MASTER_REVISION_PENDING_REVIEW' || s === 'CRP_PENDING_REVIEW' || s === 'PENDING_REVIEW') {
    return 'PENDING REVIEW';
  }
  if (s === 'MASTER_READY') return 'Ready for Review';
  if (s === 'DRAFT' || s === 'PROFILE_READY') return 'DRAFT';
  return 'READY';
}

export function directorShaLooksValid(sha?: string | null) {
  return /^[0-9a-f]{64}$/i.test((sha || '').trim());
}

export function directorIntegrityVerified(row: {
  masterSha256?: string | null;
  dnaSha256?: string | null;
  prpSha256?: string | null;
  crpSha256?: string | null;
  projectVisualStyleSha?: string | null;
}) {
  const values = [row.masterSha256, row.dnaSha256, row.prpSha256, row.crpSha256, row.projectVisualStyleSha]
    .filter((sha): sha is string => !!sha);
  return values.length > 0 && values.every((sha) => directorShaLooksValid(sha));
}

export const STUDIO_COMPLETE_TITLE = 'CHARACTER MASTER COMPLETE';
export const STUDIO_BIBLE_READY = 'CHARACTER BIBLE READY';
export const STUDIO_NEXT_STEP = 'BƯỚC TIẾP THEO';
export const STUDIO_BUILD_EPISODE = 'XÂY DỰNG EPISODE';
export const STUDIO_CONTINUE_EPISODE = '→ TIẾP TỤC XÂY DỰNG EPISODE';
export const STUDIO_CONTINUE_EPISODE_MOBILE = '→ Tiếp tục xây dựng Episode';
export const STUDIO_START_EPISODE = '→ BẮT ĐẦU XÂY DỰNG EPISODE';
export const STUDIO_MASTER_LOCKED_VI = 'MASTER ĐÃ KHÓA';
export const STUDIO_DIRECTOR_APPROVED_VI = 'ĐÃ ĐƯỢC DIRECTOR DUYỆT';
export const STUDIO_BIBLE_EXPECTED = 6;

export function studioDirectorApproved(row?: {
  status?: string | null;
  officialLocked?: boolean;
  mayLock?: boolean;
} | null) {
  if (!row) return false;
  const s = (row.status || '').toUpperCase();
  return Boolean(
    row.officialLocked
    || row.mayLock
    || s === 'CRP_APPROVED'
    || s === 'APPROVED'
    || s === 'DIRECTOR_APPROVED'
    || s === 'CHARACTER_READY'
    || s === 'CRP_LOCKED',
  );
}

export function studioMasterComplete(row?: { officialLocked?: boolean } | null) {
  return Boolean(row?.officialLocked);
}

export function studioAgeNeedsDirectorPass(row?: {
  officialLocked?: boolean;
  coverage?: number;
  ageConsistencyStatus?: string | null;
} | null) {
  if (!row || row.officialLocked) return false;
  const age = (row.ageConsistencyStatus || '').toUpperCase();
  return (row.coverage ?? 0) >= 4 && age !== 'PASS' && age !== 'FAIL';
}

export function studioAppearanceNeedsDirectorPass(row?: {
  officialLocked?: boolean;
  coverage?: number;
  ageConsistencyStatus?: string | null;
  appearanceConsistencyStatus?: string | null;
} | null) {
  if (!row || row.officialLocked) return false;
  const age = (row.ageConsistencyStatus || '').toUpperCase();
  const appearance = (row.appearanceConsistencyStatus || '').toUpperCase();
  if (age !== 'PASS') return false;
  return (row.coverage ?? 0) >= 4 && appearance !== 'PASS' && appearance !== 'FAIL';
}

export function studioLockBlockers(row?: {
  officialLocked?: boolean;
  mayLock?: boolean;
  mayApprove?: boolean;
  ageConsistencyStatus?: string | null;
  appearanceConsistencyStatus?: string | null;
} | null) {
  if (!row || row.officialLocked || row.mayLock) return [];
  const age = (row.ageConsistencyStatus || '').toUpperCase();
  const appearance = (row.appearanceConsistencyStatus || '').toUpperCase();
  const reasons: { id: string; label: string }[] = [];
  if (age !== 'PASS') reasons.push({ id: 'age', label: 'Chưa chấm tuổi' });
  if (appearance !== 'PASS') reasons.push({ id: 'appearance', label: 'Chưa chấm ngoại hình' });
  if (!row.mayApprove && age === 'PASS' && appearance === 'PASS') {
    reasons.push({ id: 'approve', label: 'Chưa duyệt bộ ảnh' });
  }
  return reasons;
}

export type StudioDirectorNextKind =
  | 'score_identity'
  | 'age_pass'
  | 'appearance_pass'
  | 'approve'
  | 'lock'
  | 'generate'
  | 'regenerate';

export type StudioDirectorNext = {
  kind: StudioDirectorNextKind;
  label: string;
  hint: string;
  enabled: boolean;
};

function studioStatusToken(row?: { status?: string | null } | null) {
  return (row?.status || '').toUpperCase();
}

export function studioCrpApproved(row?: {
  mayLock?: boolean;
  mayApprove?: boolean;
  status?: string | null;
} | null) {
  const s = studioStatusToken(row);
  return Boolean(
    row?.mayLock
    || s === 'CRP_APPROVED'
    || s === 'APPROVED'
    || s === 'DIRECTOR_APPROVED'
    || s === 'CRP_LOCKED'
    || s === 'LOCKED'
    || s === 'CHARACTER_READY',
  );
}

export function studioDirectorNext(row?: {
  officialLocked?: boolean;
  coverage?: number;
  mayGenerate?: boolean;
  mayApprove?: boolean;
  mayLock?: boolean;
  mayScoreIdentity?: boolean;
  masterSha256?: string | null;
  ageConsistencyStatus?: string | null;
  appearanceConsistencyStatus?: string | null;
  slots?: { verdict?: string | null }[] | null;
  visualStyleGate?: string | null;
  status?: string | null;
  crpStale?: boolean;
  masterRevision?: { crpStale?: boolean | null; status?: string | null } | null;
} | null, opts?: { projectStyleReady?: boolean }): StudioDirectorNext | null {
  if (!row || row.officialLocked) return null;

  const coverage = row.coverage ?? 0;
  const crpStale = Boolean(row.crpStale || row.masterRevision?.crpStale);
  const agePass = (row.ageConsistencyStatus || '').toUpperCase() === 'PASS';
  const appearancePass = (row.appearanceConsistencyStatus || '').toUpperCase() === 'PASS';

  if (studioMayScoreIdentity(row)) {
    return {
      kind: 'score_identity',
      label: STUDIO_SCORE_IDENTITY_LABEL,
      hint: 'Chấm 4 góc so với Master trước — bắt buộc trước khi chấm tuổi và ngoại hình.',
      enabled: true,
    };
  }

  if (crpStale && coverage >= 4) {
    return {
      kind: 'regenerate',
      label: 'Tạo lại bộ 4 ảnh từ Master hiện tại',
      hint: 'Master đã đổi. Bộ 4 góc đang theo Master cũ — tạo lại rồi duyệt.',
      enabled: true,
    };
  }

  if (coverage < 4 && !row.masterSha256 && row.mayGenerate) {
    return {
      kind: 'generate',
      label: 'Tạo bộ ảnh chuẩn',
      hint: 'Cần đủ 4 góc (Trước mặt, 3/4, Nghiêng, Toàn thân) trước khi Director chấm.',
      enabled: opts?.projectStyleReady !== false && row.visualStyleGate !== 'PROJECT_VISUAL_STYLE_NOT_READY',
    };
  }

  if (studioAgeNeedsDirectorPass(row)) {
    return {
      kind: 'age_pass',
      label: 'Chấm tuổi · PASS',
      hint: 'Bước 1/3: Xác nhận tuổi biểu hiện khớp với nhân vật.',
      enabled: true,
    };
  }

  if (studioAppearanceNeedsDirectorPass(row)) {
    return {
      kind: 'appearance_pass',
      label: 'Chấm ngoại hình · PASS',
      hint: 'Bước 2/3: Xác nhận ngoại hình nhất quán với Appearance Profile.',
      enabled: true,
    };
  }

  if (studioCrpApproved(row) || row.mayLock) {
    return {
      kind: 'lock',
      label: 'Khóa nhân vật',
      hint: 'Bộ ảnh đã duyệt. Khóa để dùng nhân vật này khi tạo ảnh scene.',
      enabled: true,
    };
  }

  if (coverage >= 4 && agePass && appearancePass) {
    return {
      kind: 'approve',
      label: 'Duyệt bộ ảnh',
      hint: 'Director duyệt bộ 4 góc trước khi khóa nhân vật.',
      enabled: true,
    };
  }

  if (studioMayRegenerate(row.status, row.officialLocked, row.ageConsistencyStatus, crpStale)) {
    return {
      kind: 'regenerate',
      label: 'Tạo lại bộ ảnh',
      hint: 'Bộ ảnh bị reject hoặc không đạt — tạo lại trước khi chấm.',
      enabled: true,
    };
  }

  return null;
}

export function studioNeedsLock(row?: {
  officialLocked?: boolean;
  status?: string | null;
  mayLock?: boolean;
} | null) {
  if (!row || row.officialLocked) return false;
  const s = (row.status || '').toUpperCase();
  return Boolean(
    row.mayLock
    || s === 'CRP_APPROVED'
    || s === 'APPROVED'
    || s === 'DIRECTOR_APPROVED'
    || s === 'MASTER_REVISION_APPROVED',
  );
}

export function studioLockedCount(items?: { officialLocked?: boolean }[] | null) {
  return (items ?? []).filter((row) => row.officialLocked).length;
}

export function studioBibleReady(items?: { officialLocked?: boolean }[] | null) {
  const rows = items ?? [];
  return rows.length >= STUDIO_BIBLE_EXPECTED && rows.every((row) => row.officialLocked);
}

export function directorPvsStatusLabel(status?: string | null) {
  const s = (status || '').toUpperCase();
  if (s === 'ACTIVE' || s === 'LOCKED') return s === 'LOCKED' ? 'LOCKED' : 'ACTIVE';
  if (s === 'PENDING_REVIEW') return 'PENDING REVIEW';
  if (s === 'APPROVED') return 'APPROVED';
  if (s === 'REJECTED') return 'REJECTED';
  if (s === 'DRAFT' || !s) return 'DRAFT';
  return status || 'DRAFT';
}
