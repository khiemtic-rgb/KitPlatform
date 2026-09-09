export const VISUAL_CALIBRATION_PACK_ID = 'FAMIXA-VISUAL-CALIBRATION-V1';
export const VISUAL_CALIBRATION_SUITE = 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_REGRESSION';
export const VISUAL_CALIBRATION_HARDENING_SUITE = 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_HARDENING_REGRESSION';
export const VISUAL_CALIBRATION_LIVE_SUITE = 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_READINESS_REGRESSION';
export const VISUAL_CALIBRATION_LIVE_GENERATION_SUITE = 'FAMIXA_VISUAL_CALIBRATION_PACK_V1_LIVE_GENERATION_REGRESSION';
export const CALIBRATION_VIEWS = ['FRONT', 'THREE_QUARTER', 'SIDE', 'FULL_BODY'] as const;
export const CALIBRATION_REQUIRED_SLOTS = 24;
export const CALIBRATION_DIRECTOR_FAIL_REASONS = [
  'Sai visual style',
  'Quá giống người thật',
  'Sai tỷ lệ thiết kế',
  'Sai eye design',
  'Sai facial construction',
  'Sai hair language',
  'Sai material rendering',
  'Sai lighting',
  'Sai color treatment',
  'Không đồng nhất giữa các archetype',
  'Không đồng nhất giữa các góc',
  'Khác',
] as const;

export function calibrationViewLabel(view: string) {
  if (view === 'THREE_QUARTER') return '3/4';
  if (view === 'FULL_BODY') return 'Full Body';
  if (view === 'FRONT') return 'Front';
  if (view === 'SIDE') return 'Side';
  return view;
}

export function calibrationSlotImageUrl(packId: string, subjectId: string, view: string) {
  return `/api/content/visual-calibration/${packId}/slots/${encodeURIComponent(subjectId)}/${encodeURIComponent(view)}/image`;
}

export function calibrationSlotStatus(
  artifact?: { generationStatus?: string | null; path?: string | null; sha256?: string | null } | null,
  packStatus?: string | null,
) {
  const s = (artifact?.generationStatus || '').toUpperCase();
  if (s) return s;
  if (artifact?.path && artifact?.sha256) return 'SUCCESS';
  if ((packStatus || '').toUpperCase() === 'GENERATING') return 'GENERATING';
  return 'PENDING';
}

export function calibrationStatusLabel(status?: string | null) {
  const s = (status || '').toUpperCase();
  if (s === 'LOCKED') return 'LOCKED';
  if (s === 'APPROVED') return 'APPROVED';
  if (s === 'PENDING_REVIEW') return 'PENDING REVIEW';
  if (s === 'REJECTED') return 'REJECTED';
  if (s === 'GENERATING') return 'GENERATING';
  return status || 'DRAFT';
}

export function calibrationCoverageValid(coverage?: { valid?: number; required?: number } | null) {
  return (coverage?.valid ?? 0) === CALIBRATION_REQUIRED_SLOTS
    && (coverage?.required ?? CALIBRATION_REQUIRED_SLOTS) === CALIBRATION_REQUIRED_SLOTS;
}

export function calibrationMayCreate(status?: string | null) {
  const s = (status || '').toUpperCase();
  return !s || s === 'DRAFT' || s === 'REJECTED';
}

export function calibrationMayGenerate(
  status?: string | null,
  coverage?: { valid?: number } | null,
) {
  const s = (status || '').toUpperCase();
  if (s === 'APPROVED' || s === 'LOCKED') return false;
  if (s === 'PENDING_REVIEW')
    return coverage != null && (coverage.valid ?? 0) < CALIBRATION_REQUIRED_SLOTS;
  return s === 'DRAFT' || s === 'REJECTED' || s === 'GENERATING' || !s;
}

export function calibrationMayApprove(status?: string | null, coverage?: { valid?: number; required?: number } | null) {
  return (status || '').toUpperCase() === 'PENDING_REVIEW' && calibrationCoverageValid(coverage);
}

export function calibrationMayLock(status?: string | null) {
  return (status || '').toUpperCase() === 'APPROVED';
}

export function calibrationAuthorityTransitioned(pack?: { authorityTransitioned?: boolean; currentAuthority?: boolean } | null) {
  return pack?.authorityTransitioned === true;
}

export const DIRECTOR_REVIEW_DECISIONS = ['PENDING', 'PASS', 'FAIL', 'REVIEW_REQUIRED'] as const;
export const DIRECTOR_REVIEW_GATES = [
  'IdentityGate',
  'AgeGate',
  'AppearanceGate',
  'FaceConsistencyGate',
  'ViewConsistencyGate',
  'WardrobeConsistencyGate',
  'VisualUniverseGate',
  'StylizationGate',
  'CrossCharacterGate',
  'PhotorealismGate',
] as const;
export const DIRECTOR_REVIEW_FAIL_REASONS = [
  'IDENTITY_DRIFT',
  'AGE_MISMATCH',
  'APPEARANCE_DRIFT',
  'FACE_INCONSISTENCY',
  'VIEW_MISMATCH',
  'WARDROBE_INCONSISTENCY',
  'VISUAL_STYLE_DRIFT',
  'CROSS_CHARACTER_COLLISION',
  'PHOTOREALISM_LEAKAGE',
  'TECHNICAL_ARTIFACT_INVALID',
  'IDENTITY_ANCHOR_INVALID',
  'REFERENCE_BINDING_INVALID',
  'OTHER',
] as const;

export type DirectorReviewDecision = (typeof DIRECTOR_REVIEW_DECISIONS)[number];
export type DirectorReviewGates = Record<(typeof DIRECTOR_REVIEW_GATES)[number], DirectorReviewDecision>;

export function emptyDirectorReviewGates(): DirectorReviewGates {
  return {
    IdentityGate: 'PENDING',
    AgeGate: 'PENDING',
    AppearanceGate: 'PENDING',
    FaceConsistencyGate: 'PENDING',
    ViewConsistencyGate: 'PENDING',
    WardrobeConsistencyGate: 'PENDING',
    VisualUniverseGate: 'PENDING',
    StylizationGate: 'PENDING',
    CrossCharacterGate: 'PENDING',
    PhotorealismGate: 'PENDING',
  };
}

export function directorReviewMayPass(
  technical: string,
  gates: DirectorReviewGates,
  decision: string,
) {
  if (technical !== 'PASS' || decision !== 'PASS') return false;
  return DIRECTOR_REVIEW_GATES.every((key) => gates[key] === 'PASS');
}

export function directorReviewPassEligible(technical: string, gates: DirectorReviewGates) {
  return technical === 'PASS' && DIRECTOR_REVIEW_GATES.every((key) => gates[key] === 'PASS');
}

export function directorReviewViewsComplete(
  views?: { view?: string; artifactPath?: string | null }[] | null,
) {
  if (!views?.length) return false;
  return CALIBRATION_VIEWS.every((view) =>
    views.some((row) => row.view === view && Boolean(row.artifactPath)));
}

export function directorReviewPackVisualPass(input: {
  technicalValid: number;
  anchors: number;
  conditioned: number;
  subjectPass: number;
  fail: number;
  reviewRequired: number;
  allVisualPass: boolean;
  marked: boolean;
}) {
  return input.marked
    && input.technicalValid === 24
    && input.anchors === 6
    && input.conditioned === 18
    && input.subjectPass === 6
    && input.fail === 0
    && input.reviewRequired === 0
    && input.allVisualPass;
}

export function directorReviewTriggersGeneration() {
  return false;
}

export const DIRECTOR_REVIEW_GATE_LABELS: Record<(typeof DIRECTOR_REVIEW_GATES)[number], string> = {
  IdentityGate: 'Identity',
  AgeGate: 'Age',
  AppearanceGate: 'Appearance',
  FaceConsistencyGate: 'Face',
  ViewConsistencyGate: 'View',
  WardrobeConsistencyGate: 'Wardrobe',
  VisualUniverseGate: 'Visual Universe',
  StylizationGate: 'Stylization',
  CrossCharacterGate: 'Cross-character',
  PhotorealismGate: 'Photorealism',
};

export const DIRECTOR_REVIEW_WORKSPACE_ID = 'identity-conditioned-calibration-director-review';
export const DIRECTOR_REVIEW_SAVE_LABEL = 'Save Review';
export const DIRECTOR_REVIEW_SAVE_NEXT_LABEL = 'Save & Next Subject';
export const DIRECTOR_REVIEW_PACK_SUMMARY_LABEL = 'Review Pack Summary →';
export const DIRECTOR_REVIEW_MARK_PASS_LABEL = 'Mark Visual Calibration Pass';
export const DIRECTOR_REVIEW_OPEN_LABEL = 'Open Director Review';
export const DIRECTOR_REVIEW_FROM_STUDIO_LABEL = 'Review Calibration';
export const DIRECTOR_REVIEW_START = 'BẮT ĐẦU DUYỆT';
export const DIRECTOR_REVIEW_SAVE_VI = 'LƯU DUYỆT';
export const DIRECTOR_REVIEW_NEXT_VI_CTA = 'NHÂN VẬT TIẾP THEO →';
export const DIRECTOR_REVIEW_MARK_PASS_VI_BTN = 'ĐÁNH DẤU VISUAL CALIBRATION PASS';
export const DIRECTOR_REVIEW_IMAGES_READY = 'ẢNH ĐÃ TẠO';
export const DIRECTOR_REVIEW_AWAITING = 'CHỜ DIRECTOR DUYỆT';
export const DIRECTOR_REVIEW_DONE_VI = 'Visual Calibration đã hoàn tất. Không cần thao tác thêm trong Director Review.';
export const DIRECTOR_REVIEW_ADVANCED = 'Revision / Advanced Actions';
export const DIRECTOR_REVIEW_ADVANCED_ENGINEERING = 'Advanced / Engineering actions';
export const DIRECTOR_REVIEW_OPEN_COMPARISON = 'Open large comparison';
export const DIRECTOR_REVIEW_IMAGES_READY_LINE = 'Technical check passed — images ready for Director review';
export const DIRECTOR_REVIEW_IMAGES_BLOCKED_LINE = 'Technical check required — Director review is blocked';
export const DIRECTOR_REVIEW_READY_TITLE = 'VISUAL CALIBRATION READY';
export const DIRECTOR_REVIEW_NOTE_PLACEHOLDER =
  'Nhập nhận xét của Director...';
export const DIRECTOR_REVIEW_BACK_CALIBRATION = 'Back to Visual Calibration';
export const DIRECTOR_REVIEW_MEMORY_HINT =
  'Director Review persistence is currently in-memory. API recycle resets saved reviews.';
export const DIRECTOR_REVIEW_MEMORY_RESET =
  'Director Review session was reset because review persistence is currently in-memory.';
export const DIRECTOR_REVIEW_NEXT_CHARACTER = 'Review Next Character →';
export const DIRECTOR_REVIEW_GO_PACK = 'Go to Pack Review';
export const DIRECTOR_REVIEW_PASSED_LABEL = 'Visual Calibration Passed';
export const DIRECTOR_REVIEW_PASSED_VI = 'Visual Calibration đã PASS';
export const DIRECTOR_REVIEW_MARK_PASS_VI = 'Đánh dấu Visual Calibration PASS';
export const DIRECTOR_REVIEW_REVIEW_NEXT = 'REVIEW NEXT CHARACTER';
export const DIRECTOR_REVIEW_REVIEW_NEXT_VI = 'Duyệt tiếp';
export const DIRECTOR_REVIEW_COMPLETE_TITLE = 'DIRECTOR REVIEW COMPLETE';
export const DIRECTOR_REVIEW_COMPLETE_VI = 'Duyệt hoàn tất';
export const DIRECTOR_REVIEW_REVIEW_FAILED = 'REVIEW FAILED CHARACTER';
export const DIRECTOR_REVIEW_REVIEW_FAILED_VI = 'Xem nhân vật không đạt';
export const DIRECTOR_REVIEW_REVIEW_REQUIRED_CTA = 'REVIEW REQUIRED CHARACTER';
export const DIRECTOR_REVIEW_REVIEW_REQUIRED_VI = 'Xem nhân vật cần xem lại';
export const DIRECTOR_REVIEW_CONTINUE_FOUNDATION_VI = 'Tiếp tục Visual Foundation';
export const DIRECTOR_REVIEW_FOUNDATION_READY = 'VISUAL FOUNDATION READY';
export const DIRECTOR_REVIEW_FOUNDATION_DONE = 'No further action is required in Director Review.';
export const DIRECTOR_REVIEW_NO_REGEN = 'Do not regenerate automatically.';
export const DIRECTOR_REVIEW_PACK_UNIVERSE_KEY = 'famixa-dr-pack-universe';
export const DIRECTOR_REVIEW_PROGRESS_KEY = 'famixa-dr-progress';
export const DIRECTOR_REVIEW_FAIL_HINT =
  'Ảnh không đạt. Việc tạo lại ảnh thuộc workflow Calibration Generation và không được thực hiện tự động trong bước Review.';
export const DIRECTOR_REVIEW_UNSAVED_MESSAGE = 'Bạn có thay đổi chưa lưu.';
export const DIRECTOR_REVIEW_UNSAVED_LEAVE = 'You have unsaved review changes. Leave without saving?';
export const DIRECTOR_REVIEW_FOUNDATION_NEXT = 'NEXT PHASE: FOUNDATION FINALIZATION';
export const DIRECTOR_REVIEW_CONTINUE_FOUNDATION = 'Continue to Foundation';
export const DIRECTOR_REVIEW_HISTORICAL_LABEL = 'HISTORICAL — NOT ELIGIBLE FOR CURRENT APPROVAL';
export const DIRECTOR_REVIEW_EXIT_LABEL = 'Exit Review';
export const DIRECTOR_REVIEW_GUIDE =
  'Compare the FRONT identity anchor with all conditioned views. Only mark PASS when the character remains visually consistent.';
export const DIRECTOR_REVIEW_GUIDE_VI =
  'Đối chiếu ảnh TRƯỚC MẶT (Identity Anchor) với 3/4, Nghiêng và Toàn thân. Chỉ chọn PASS khi nhân vật giữ được nhận diện nhất quán.';
export const DIRECTOR_REVIEW_MARK_PASS_WARNING =
  'Records Director approval only. Does not generate, lock, promote, or mutate character authority.';
export const DIRECTOR_REVIEW_PASS_BLOCKED = 'Cannot PASS this subject yet.';
export const DIRECTOR_REVIEW_PASS_SUBJECT = 'PASS SUBJECT';
export const DIRECTOR_REVIEW_FAIL_SUBJECT = 'FAIL SUBJECT';
export const DIRECTOR_REVIEW_KEEP_FAIL = 'Giữ FAIL';
export const DIRECTOR_REVIEW_GATE_HELP: Record<(typeof DIRECTOR_REVIEW_GATES)[number], string> = {
  IdentityGate: 'Does this character still look like the same person as the FRONT anchor?',
  AgeGate: 'Does the apparent age remain consistent across all views? Do not require children and adults to have identical proportions.',
  AppearanceGate: 'Does hair, skin, and overall look stay the same person?',
  FaceConsistencyGate: 'Does the face stay consistent with the FRONT identity anchor?',
  ViewConsistencyGate: 'Is each view the correct camera angle?',
  WardrobeConsistencyGate: 'Does wardrobe stay consistent across the four views?',
  VisualUniverseGate: 'Do these images belong to the same visual world?',
  StylizationGate: 'Is the illustration style consistent with FAMIXA?',
  CrossCharacterGate: 'Could this character be confused with another character?',
  PhotorealismGate: 'Does the image stay illustrated, not photoreal?',
};

export function directorReviewNoteRequired(decision?: string | null) {
  const d = (decision || '').toUpperCase();
  return d === 'FAIL' || d === 'REVIEW_REQUIRED';
}

export function directorReviewSubjectsReviewed(subjects?: { decision?: string | null }[] | null) {
  return (subjects ?? []).filter((s) => {
    const d = (s.decision || 'PENDING').toUpperCase();
    return d === 'PASS' || d === 'FAIL' || d === 'REVIEW_REQUIRED';
  }).length;
}

export function visualCalibrationWorkspacePath(packId = VISUAL_CALIBRATION_PACK_ID) {
  return `/content/visual-calibration/${encodeURIComponent(packId)}`;
}

export function directorReviewWorkspacePath(packId = VISUAL_CALIBRATION_PACK_ID, subjectId?: string) {
  const base = `${visualCalibrationWorkspacePath(packId)}/director-review`;
  return subjectId ? `${base}?subject=${encodeURIComponent(subjectId)}` : base;
}

export function calibrationViewDirectorLabel(view: string) {
  if (view === 'FRONT') return 'Trước mặt';
  if (view === 'THREE_QUARTER') return '3/4';
  if (view === 'SIDE') return 'Nghiêng';
  if (view === 'FULL_BODY') return 'Toàn thân';
  return view;
}

export function calibrationViewCompositeLabel(view: string) {
  return `${view} · ${calibrationViewDirectorLabel(view)}`;
}

export function directorReviewSubjectRoleLine(subject?: {
  subjectType?: string | null;
  chronologicalAge?: number | null;
  targetAppearanceAgeMin?: number | null;
  targetAppearanceAgeMax?: number | null;
} | null) {
  const type = (subject?.subjectType || '').trim();
  if (subject?.chronologicalAge) return type ? `${type}` : `Age ${subject.chronologicalAge}`;
  return type;
}

export function calibrationViewRoleLabel(view: string) {
  return view === 'FRONT' ? 'IDENTITY ANCHOR' : 'CONDITIONED BY FRONT';
}

export function directorReviewSubjectMark(decision?: string | null, reviewStatus?: string | null) {
  const d = (decision || reviewStatus || 'PENDING').toUpperCase();
  if (d === 'PASS') return '✓';
  if (d === 'FAIL') return '✕';
  if (d === 'REVIEW_REQUIRED') return '!';
  if (d === 'IN_REVIEW') return '●';
  return '○';
}

export function directorReviewWorkspaceDirty(
  current: { gates: DirectorReviewGates; decision: string; directorNote: string },
  saved: { gates: DirectorReviewGates; decision: string; directorNote: string },
) {
  return current.decision !== saved.decision
    || current.directorNote !== saved.directorNote
    || !DIRECTOR_REVIEW_GATES.every((key) => current.gates[key] === saved.gates[key]);
}

export function directorReviewOverallLabel(review?: {
  visualPass?: boolean;
  packDecision?: string | null;
  fail?: number;
  subjectPass?: number;
  directorReviewCount?: number;
} | null) {
  if (review?.visualPass) return 'VISUAL CALIBRATION PASS';
  if (review?.packDecision === 'PASS' && review.visualPass !== true) return 'READY TO MARK PASS';
  if ((review?.fail ?? 0) > 0) return 'DIRECTOR REVIEW BLOCKED';
  if ((review?.subjectPass ?? 0) > 0 || (review?.directorReviewCount ?? 0) > 0) return 'DIRECTOR REVIEW IN PROGRESS';
  return 'PENDING DIRECTOR REVIEW';
}

export function directorReviewShowsLockCta() {
  return false;
}

export function directorReviewExposesMasterRevisionCta() {
  return false;
}

export function directorReviewExposesRegenerateCta() {
  return false;
}

export function directorReviewMarkPassEnabled(review?: {
  packDecision?: string | null;
  visualPass?: boolean;
  technicalIntegrityValid?: number;
  subjectPass?: number;
  fail?: number;
  reviewRequired?: number;
  subjects?: { decision?: string | null }[] | null;
} | null) {
  if (!review || review.visualPass) return false;
  if ((review.fail ?? 0) > 0 || (review.reviewRequired ?? 0) > 0) return false;
  if (directorReviewDecisionPassCount(review.subjects) === 6) return true;
  return review.packDecision === 'PASS'
    && (review.technicalIntegrityValid ?? 0) === 24
    && (review.subjectPass ?? 0) === 6;
}

export function directorReviewMarkPassBlockedReason(review?: {
  packDecision?: string | null;
  visualPass?: boolean;
  technicalIntegrityValid?: number;
  subjectPass?: number;
  fail?: number;
  reviewRequired?: number;
  subjects?: { decision?: string | null }[] | null;
} | null) {
  if (!review) return 'Chưa tải được Director Review.';
  if (review.visualPass) return 'Visual Calibration Pass đã được ghi nhận.';
  if (directorReviewDecisionPassCount(review.subjects) === 6) return '';
  if ((review.technicalIntegrityValid ?? 0) < 24) return 'Technical integrity chưa 24/24.';
  if ((review.subjectPass ?? 0) < 6) return `Chưa đủ Subject PASS (${review.subjectPass ?? 0}/6).`;
  if ((review.fail ?? 0) > 0) return 'Còn FAIL. Ghi Decision = FAIL, không regenerate.';
  if ((review.reviewRequired ?? 0) > 0) return 'Còn REVIEW_REQUIRED.';
  if (review.packDecision !== 'PASS') return 'Pack chưa đủ điều kiện Visual Calibration PASS.';
  return '';
}

export function directorReviewSaveReload(
  saved: DirectorReviewGates,
  loaded: DirectorReviewGates,
) {
  return DIRECTOR_REVIEW_GATES.every((key) => saved[key] === loaded[key]);
}

/** Presentation-only. Does not change eligibility or persistence. */
export function directorReviewDecisionVi(status?: string | null) {
  const d = (status || 'PENDING').toUpperCase();
  if (d === 'PASS') return 'Đạt';
  if (d === 'FAIL') return 'Không đạt';
  if (d === 'REVIEW_REQUIRED') return 'Cần xem lại';
  return 'Chưa review';
}

/** Presentation-only mapping of directorReviewOverallLabel. */
export function directorReviewWorkspaceBadge(review?: {
  visualPass?: boolean;
  packDecision?: string | null;
  fail?: number;
  subjectPass?: number;
  directorReviewCount?: number;
  technicalIntegrityValid?: number;
} | null) {
  const overall = directorReviewOverallLabel(review);
  if (overall === 'VISUAL CALIBRATION PASS') return 'VISUAL CALIBRATION PASSED';
  if (overall === 'READY TO MARK PASS') return 'READY TO PASS';
  if (overall === 'DIRECTOR REVIEW BLOCKED') return 'REVIEW BLOCKED';
  if ((review?.technicalIntegrityValid ?? 24) < 24) return 'REVIEW BLOCKED';
  return 'REVIEW IN PROGRESS';
}

export function directorReviewWorkspaceBadgeTone(badge?: string | null) {
  if (badge === 'VISUAL CALIBRATION PASSED' || badge === 'READY TO PASS') return 'pass';
  if (badge === 'REVIEW BLOCKED') return 'fail';
  return 'progress';
}

export function directorReviewGatePassedCount(gates?: DirectorReviewGates | null) {
  if (!gates) return 0;
  return DIRECTOR_REVIEW_GATES.filter((key) => gates[key] === 'PASS').length;
}

export function directorReviewImagesReadyCount(
  views?: { view?: string; artifactPath?: string | null }[] | null,
) {
  if (!views?.length) return 0;
  return CALIBRATION_VIEWS.filter((view) =>
    views.some((row) => row.view === view && Boolean(row.artifactPath))).length;
}

export function directorReviewViewBadge(view?: {
  artifactPath?: string | null;
  technicalStatus?: string | null;
} | null) {
  if (!view?.artifactPath) return 'PENDING';
  const t = (view.technicalStatus || '').toUpperCase();
  if (t === 'PASS' || t === 'FAIL') return t;
  return 'PENDING';
}

export function directorReviewPassChecklist(input: {
  viewsComplete: boolean;
  technicalPass: boolean;
  gates: DirectorReviewGates;
}) {
  return [
    { id: 'images', label: '4/4 images', ok: input.viewsComplete },
    { id: 'technical', label: 'Technical validation', ok: input.technicalPass },
    ...DIRECTOR_REVIEW_GATES.map((key) => ({
      id: key,
      label: DIRECTOR_REVIEW_GATE_LABELS[key],
      ok: input.gates[key] === 'PASS',
    })),
  ];
}

export function directorReviewPendingCount(subjects?: { decision?: string | null }[] | null) {
  return (subjects ?? []).filter((s) => {
    const d = (s.decision || 'PENDING').toUpperCase();
    return d === 'PENDING' || d === 'NOT_REVIEWED';
  }).length;
}

export function directorReviewNextPendingSubject(
  subjects?: { subjectId: string; decision?: string | null }[] | null,
  currentId?: string,
) {
  const list = subjects ?? [];
  const pending = (row: { subjectId: string; decision?: string | null }) => {
    const d = (row.decision || 'PENDING').toUpperCase();
    return d === 'PENDING' || d === 'NOT_REVIEWED';
  };
  const idx = list.findIndex((s) => s.subjectId === currentId);
  const after = list.slice(idx + 1).find(pending);
  if (after) return after.subjectId;
  return list.find((s) => pending(s) && s.subjectId !== currentId)?.subjectId;
}

export const DIRECTOR_REVIEW_PACK_UNIVERSE_GATES = [
  'VisualUniverseGate',
  'StylizationGate',
  'CrossCharacterGate',
  'PhotorealismGate',
] as const;

export type DirectorReviewPackUniverseGates =
  Record<(typeof DIRECTOR_REVIEW_PACK_UNIVERSE_GATES)[number], DirectorReviewDecision>;

export function emptyPackUniverseGates(): DirectorReviewPackUniverseGates {
  return {
    VisualUniverseGate: 'PENDING',
    StylizationGate: 'PENDING',
    CrossCharacterGate: 'PENDING',
    PhotorealismGate: 'PENDING',
  };
}

export function directorReviewPackUniversePass(gates?: DirectorReviewPackUniverseGates | null) {
  if (!gates) return false;
  return DIRECTOR_REVIEW_PACK_UNIVERSE_GATES.every((key) => gates[key] === 'PASS');
}

export function directorReviewGateCompletedCount(gates?: DirectorReviewGates | null) {
  if (!gates) return 0;
  return DIRECTOR_REVIEW_GATES.filter((key) => gates[key] !== 'PENDING').length;
}

export function directorReviewSubjectScopeStatus(decision?: string | null, completedGates = 0) {
  const d = (decision || 'PENDING').toUpperCase();
  if (d === 'PASS') return 'PASS';
  if (d === 'FAIL') return 'FAIL';
  if (d === 'REVIEW_REQUIRED') return 'REVIEW REQUIRED';
  if (completedGates > 0) return 'IN REVIEW';
  return 'NOT REVIEWED';
}

export function directorReviewPackScopeStatus(review?: {
  visualPass?: boolean;
  packDecision?: string | null;
  fail?: number;
  reviewRequired?: number;
  subjectPass?: number;
  directorReviewCount?: number;
} | null) {
  if (review?.visualPass) return 'PASS';
  if ((review?.fail ?? 0) > 0) return 'BLOCKED';
  if (review?.packDecision === 'PASS') return 'READY';
  if ((review?.subjectPass ?? 0) > 0 || (review?.directorReviewCount ?? 0) > 0) return 'IN REVIEW';
  return 'PENDING';
}

export function directorReviewMarkPassReasons(
  review?: {
    packDecision?: string | null;
    visualPass?: boolean;
    technicalIntegrityValid?: number;
    subjectPass?: number;
    fail?: number;
    reviewRequired?: number;
    directorReviewCount?: number;
  } | null,
  _packGates?: DirectorReviewPackUniverseGates | null,
) {
  const reasons: string[] = [];
  if (!review) return ['Director Review is not loaded.'];
  if (review.visualPass) return [];
  if ((review.technicalIntegrityValid ?? 0) < 24) reasons.push('Technical integrity is not 24/24.');
  const reviewed = review.directorReviewCount ?? review.subjectPass ?? 0;
  if ((review.subjectPass ?? 0) < 6) {
    const leftover = 6 - (review.subjectPass ?? 0);
    reasons.push(leftover === 6 ? '6 subjects not reviewed' : `${leftover} subjects not reviewed`);
  }
  if ((review.fail ?? 0) > 0) reasons.push('A subject is FAIL.');
  if ((review.reviewRequired ?? 0) > 0) reasons.push('A subject is REVIEW REQUIRED.');
  if (reviewed === 0 && reasons.length === 0) reasons.push('Pack review is pending.');
  return [...new Set(reasons)];
}

export function directorReviewUiCanMarkPass(
  review?: Parameters<typeof directorReviewMarkPassEnabled>[0],
  _packGates?: DirectorReviewPackUniverseGates | null,
) {
  return directorReviewMarkPassEnabled(review);
}

export type DirectorReviewWorkflowPhase =
  | 'PASSED'
  | 'TECHNICAL_BLOCK'
  | 'BLOCKED'
  | 'INCOMPLETE'
  | 'READY'
  | 'IN_PROGRESS';

export const DIRECTOR_REVIEW_GATE_GROUPS: {
  id: string;
  title: string;
  gates: readonly (typeof DIRECTOR_REVIEW_GATES)[number][];
}[] = [
  {
    id: 'IDENTITY',
    title: 'IDENTITY',
    gates: ['IdentityGate', 'AgeGate', 'AppearanceGate', 'FaceConsistencyGate'],
  },
  {
    id: 'CONSISTENCY',
    title: 'VISUAL CONSISTENCY',
    gates: ['ViewConsistencyGate', 'WardrobeConsistencyGate', 'VisualUniverseGate'],
  },
  {
    id: 'STYLE',
    title: 'STYLE',
    gates: ['StylizationGate', 'CrossCharacterGate', 'PhotorealismGate'],
  },
];

export function directorReviewPersistRunId(
  review?: { calibrationRunId?: string | null; packId?: string | null } | null,
  pack?: { generationExecutionId?: string | null; calibrationRunId?: string | null; packId?: string | null } | null,
) {
  return review?.calibrationRunId
    || pack?.generationExecutionId
    || pack?.calibrationRunId
    || (review?.packId || pack?.packId ? `CAL-REVIEW-${review?.packId || pack?.packId}` : '');
}

export function directorReviewSaveGateMessage(code?: string | null) {
  const c = (code || '').toUpperCase();
  if (c === 'CALIBRATION_RUN_MISMATCH')
    return 'Không lưu được vì phiên review không khớp. Tải lại trang rồi nhấn Lưu duyệt lại.';
  if (!c) return '';
  return `Không lưu được Director Review (${code}).`;
}

export function directorReviewSaveReady(
  gates?: DirectorReviewGates | null,
  decision?: string | null,
) {
  if (!gates || !decision || decision === 'PENDING') return false;
  return DIRECTOR_REVIEW_GATES.every((key) => gates[key] !== 'PENDING');
}

export function directorReviewDisplayJobs(
  subjects: { subjectId: string }[] | undefined,
  selectedId: string | undefined,
  packId: string,
) {
  return (subjects ?? []).flatMap((subject) =>
    CALIBRATION_VIEWS
      .filter((view) => subject.subjectId === selectedId || view === 'FRONT')
      .map((view) => [`${subject.subjectId}/${view}`, calibrationSlotImageUrl(packId, subject.subjectId, view)] as const));
}

export function directorReviewWorkflowPhase(review?: {
  visualPass?: boolean;
  packDecision?: string | null;
  technicalIntegrityValid?: number;
  subjectPass?: number;
  fail?: number;
  reviewRequired?: number;
} | null, opts?: { imagesMissing?: boolean }): DirectorReviewWorkflowPhase {
  if (review?.visualPass) return 'PASSED';
  if (opts?.imagesMissing) return 'TECHNICAL_BLOCK';
  if ((review?.fail ?? 0) > 0) return 'BLOCKED';
  if ((review?.reviewRequired ?? 0) > 0) return 'INCOMPLETE';
  if (directorReviewMarkPassEnabled(review)) return 'READY';
  return 'IN_PROGRESS';
}

export function directorReviewLoadedImageCount(
  images?: Record<string, string> | null,
  subjectId?: string,
) {
  if (!subjectId || !images) return 0;
  return CALIBRATION_VIEWS.filter((view) => Boolean(images[`${subjectId}/${view}`])).length;
}

export function directorReviewPackSlotPresent(
  pack: {
    subjects?: {
      calibrationSubjectId?: string;
      artifacts?: { viewType?: string | null; path?: string | null }[];
    }[];
  } | null | undefined,
  subjectId?: string,
  view?: string,
) {
  if (!subjectId || !view) return false;
  return (pack?.subjects ?? []).some((subject) =>
    (subject.calibrationSubjectId || '').toUpperCase() === subjectId.toUpperCase()
    && (subject.artifacts ?? []).some((art) =>
      (art.viewType || '').toUpperCase() === view.toUpperCase() && Boolean(art.path)));
}

export function directorReviewDecisionPassCount(
  subjects?: { decision?: string | null }[] | null,
) {
  return (subjects ?? []).filter((s) => (s.decision || '').toUpperCase() === 'PASS').length;
}

export function directorReviewHandoffCta(review?: {
  visualPass?: boolean;
  packDecision?: string | null;
  technicalIntegrityValid?: number;
  subjectPass?: number;
  fail?: number;
  reviewRequired?: number;
  subjects?: { decision?: string | null }[] | null;
} | null) {
  const reviewed = directorReviewSubjectsReviewed(review?.subjects);
  const visualPass = directorReviewDecisionPassCount(review?.subjects);
  if (review?.visualPass) return DIRECTOR_REVIEW_OPEN_LABEL;
  if (directorReviewMarkPassEnabled(review)) return DIRECTOR_REVIEW_MARK_PASS_VI_BTN;
  if (visualPass >= 6 && reviewed >= 6) return 'MỞ LẠI PHẦN DUYỆT';
  if (reviewed > 0) return DIRECTOR_REVIEW_NEXT_VI_CTA;
  return DIRECTOR_REVIEW_START;
}

export function directorReviewNavStatus(decision?: string | null, completedGates = 0) {
  const d = (decision || 'PENDING').toUpperCase();
  if (d === 'PASS') return '✓ PASSED';
  if (d === 'FAIL') return '✕ FAILED';
  if (d === 'REVIEW_REQUIRED' || completedGates > 0) return '◐ IN REVIEW';
  return '○ NOT REVIEWED';
}

export function directorReviewNextDecisionSubject(
  subjects?: { subjectId: string; decision?: string | null }[] | null,
  currentId?: string,
  decisions: string[] = ['PENDING', 'NOT_REVIEWED'],
) {
  const list = subjects ?? [];
  const match = (row: { subjectId: string; decision?: string | null }) =>
    decisions.includes((row.decision || 'PENDING').toUpperCase());
  const idx = list.findIndex((s) => s.subjectId === currentId);
  const after = list.slice(idx + 1).find(match);
  if (after) return after.subjectId;
  return list.find((s) => match(s) && s.subjectId !== currentId)?.subjectId;
}

export function directorReviewTechnicalBlockReasons(review?: {
  technicalIntegrityValid?: number;
  frontAnchorsValid?: number;
  identityConditionedValid?: number;
  gateCode?: string | null;
} | null) {
  const reasons: string[] = [];
  if ((review?.technicalIntegrityValid ?? 0) < 24) {
    reasons.push(`Technical integrity ${review?.technicalIntegrityValid ?? 0} / 24.`);
  }
  if ((review?.frontAnchorsValid ?? 0) < 6) {
    reasons.push(`Identity anchors ${review?.frontAnchorsValid ?? 0} / 6.`);
  }
  if ((review?.identityConditionedValid ?? 0) < 18) {
    reasons.push(`Conditioned views ${review?.identityConditionedValid ?? 0} / 18.`);
  }
  if (review?.gateCode) reasons.push(review.gateCode);
  if (!reasons.length) reasons.push('Technical integrity is not PASS.');
  return reasons;
}

export function directorReviewSubjectStatusVi(status?: string | null) {
  const d = (status || 'NOT REVIEWED').toUpperCase();
  if (d === 'PASS') return 'Đã duyệt';
  if (d === 'FAIL') return 'Không đạt';
  if (d === 'REVIEW REQUIRED' || d === 'REVIEW_REQUIRED') return 'Cần xem lại';
  if (d === 'IN REVIEW') return 'Đang duyệt';
  return 'Đang chờ duyệt';
}

export function directorReviewSubjectCue(decision?: string | null) {
  const d = (decision || 'PENDING').toUpperCase();
  if (d === 'PASS') return '✓ Director Approved';
  if (d === 'FAIL') return '✕ Director Rejected';
  if (d === 'REVIEW_REQUIRED') return '! Review Required';
  return '○ Awaiting Review';
}

export function directorReviewSubjectChipVi(decision?: string | null) {
  const d = (decision || 'PENDING').toUpperCase();
  if (d === 'PASS') return '✓ Đã PASS';
  if (d === 'FAIL') return '✕ FAIL';
  if (d === 'REVIEW_REQUIRED') return '! Cần xem lại';
  return '○ Chưa duyệt';
}

export function directorReviewPrimaryAction(
  phase: DirectorReviewWorkflowPhase,
  opts?: { savedPass?: boolean; hasNext?: boolean },
) {
  if (phase === 'PASSED') return 'FOUNDATION';
  if (phase === 'READY') return 'MARK_PASS';
  if (opts?.savedPass && opts.hasNext) return 'NEXT';
  if (phase === 'IN_PROGRESS' || phase === 'BLOCKED' || phase === 'INCOMPLETE') return 'SAVE';
  return 'START';
}

export function directorReviewMemoryResetDetected(
  previous?: { runId?: string | null; reviewed?: number; visualPass?: boolean } | null,
  current?: {
    calibrationRunId?: string | null;
    subjects?: { decision?: string | null }[] | null;
    visualPass?: boolean;
  } | null,
) {
  if (!previous || !current) return false;
  const reviewed = directorReviewSubjectsReviewed(current.subjects);
  return (previous.reviewed ?? 0) > 0 && reviewed === 0 && current.visualPass !== true;
}
