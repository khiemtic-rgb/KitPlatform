import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  CALIBRATION_VIEWS,
  DIRECTOR_REVIEW_DECISIONS,
  DIRECTOR_REVIEW_EXIT_LABEL,
  DIRECTOR_REVIEW_CONTINUE_FOUNDATION,
  DIRECTOR_REVIEW_FOUNDATION_NEXT,
  DIRECTOR_REVIEW_FROM_STUDIO_LABEL,
  DIRECTOR_REVIEW_GATE_HELP,
  DIRECTOR_REVIEW_GATE_LABELS,
  DIRECTOR_REVIEW_GATES,
  DIRECTOR_REVIEW_GUIDE,
  DIRECTOR_REVIEW_GUIDE_VI,
  DIRECTOR_REVIEW_HISTORICAL_LABEL,
  DIRECTOR_REVIEW_MARK_PASS_LABEL,
  DIRECTOR_REVIEW_PASS_BLOCKED,
  DIRECTOR_REVIEW_PASS_SUBJECT,
  DIRECTOR_REVIEW_OPEN_LABEL,
  DIRECTOR_REVIEW_SAVE_LABEL,
  DIRECTOR_REVIEW_SAVE_NEXT_LABEL,
  DIRECTOR_REVIEW_UNSAVED_LEAVE,
  DIRECTOR_REVIEW_UNSAVED_MESSAGE,
  DIRECTOR_REVIEW_WORKSPACE_ID,
  directorReviewExposesMasterRevisionCta,
  directorReviewExposesRegenerateCta,
  directorReviewMarkPassEnabled,
  directorReviewDecisionVi,
  directorReviewMayPass,
  directorReviewNoteRequired,
  directorReviewOverallLabel,
  directorReviewPassEligible,
  directorReviewPackVisualPass,
  directorReviewSaveReload,
  directorReviewShowsLockCta,
  directorReviewSubjectMark,
  directorReviewTriggersGeneration,
  directorReviewNextPendingSubject,
  directorReviewWorkspaceBadge,
  directorReviewWorkspaceDirty,
  directorReviewWorkspacePath,
  directorReviewGateCompletedCount,
  directorReviewImagesReadyCount,
  directorReviewMarkPassReasons,
  directorReviewMemoryResetDetected,
  directorReviewPackScopeStatus,
  directorReviewPackUniversePass,
  directorReviewSubjectScopeStatus,
  directorReviewUiCanMarkPass,
  directorReviewDecisionPassCount,
  directorReviewHandoffCta,
  directorReviewWorkflowPhase,
  directorReviewPersistRunId,
  directorReviewSaveGateMessage,
  directorReviewDisplayJobs,
  directorReviewLoadedImageCount,
  directorReviewNavStatus,
  directorReviewSaveReady,
  emptyDirectorReviewGates,
  emptyPackUniverseGates,
  DIRECTOR_REVIEW_BACK_CALIBRATION,
  DIRECTOR_REVIEW_COMPLETE_TITLE,
  DIRECTOR_REVIEW_GO_PACK,
  DIRECTOR_REVIEW_MARK_PASS_VI,
  DIRECTOR_REVIEW_REVIEW_NEXT,
  DIRECTOR_REVIEW_START,
  DIRECTOR_REVIEW_SAVE_VI,
  DIRECTOR_REVIEW_NEXT_VI_CTA,
  DIRECTOR_REVIEW_MARK_PASS_VI_BTN,
  DIRECTOR_REVIEW_MEMORY_RESET,
  DIRECTOR_REVIEW_GATE_GROUPS,
  DIRECTOR_REVIEW_NEXT_CHARACTER,
  DIRECTOR_REVIEW_NO_REGEN,
  DIRECTOR_REVIEW_OPEN_COMPARISON,
  DIRECTOR_REVIEW_READY_TITLE,
  DIRECTOR_REVIEW_NOTE_PLACEHOLDER,
  DIRECTOR_REVIEW_PASSED_LABEL,
} from './kit-video-visual-calibration';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const card = read('ContentFamixaIdentityConditionedCalibrationDirectorReviewCard.tsx');
const workspace = read('ContentFamixaDirectorReviewWorkspace.tsx');
const panels = read('ContentFamixaDirectorReviewPanels.tsx');
const css = read('content-famixa-studio.css');
const page = read('ContentFamixaDirectorReviewPage.tsx');
const surface = workspace + panels;
const shortcut = read('ContentFamixaCharacterCalibrationShortcut.tsx');
const board = read('ContentFamixaProductionBoards.tsx');
const studio = read('ContentFamixaCharacterStudio.tsx');
const cal = read('ContentFamixaVisualCalibrationCard.tsx');
const calPage = read('ContentFamixaVisualCalibrationPage.tsx');
const router = read('../../app/router.tsx');
const api = read('../../shared/api/content.api.ts');
const controller = read('../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs');
const service = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/IdentityConditionedCalibrationDirectorReviewService.cs');
const uiReg = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/IdentityConditionedCalibrationDirectorReviewUiV1Regression.cs');
const icReg = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/IdentityConditionedCalibrationV1Regression.cs');
const foundation = read('../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/VisualFoundationFinalizationV1Regression.cs');

const subjects = Array.from({ length: 6 }, (_, i) => ({
  subjectId: `CAL-00${i + 1}`,
  views: CALIBRATION_VIEWS.map((view) => ({
    view,
    isIdentityAnchor: view === 'FRONT',
    isIdentityConditioned: view !== 'FRONT',
  })),
}));

ok(card.includes(`id={DIRECTOR_REVIEW_WORKSPACE_ID}`)
  && workspace.includes(`id={DIRECTOR_REVIEW_WORKSPACE_ID}`)
  && board.includes('<ContentFamixaIdentityConditionedCalibrationDirectorReviewCard')
  && board.indexOf('<ContentFamixaVisualCalibrationCard')
    < board.indexOf('<ContentFamixaIdentityConditionedCalibrationDirectorReviewCard')
  && board.indexOf('<ContentFamixaIdentityConditionedCalibrationDirectorReviewCard')
    < board.indexOf('<ContentFamixaCharacterStudio')
  && page.includes('ContentFamixaDirectorReviewWorkspace')
  && router.includes('visual-calibration/:packId/director-review')
  && router.includes('visual-calibration/:packId')
  && router.includes('ContentFamixaVisualCalibrationPage'),
  'review data loads / dedicated workspace route');
ok(subjects.length === 6 && workspace.includes('review?.subjects') && surface.includes('subjects.map'),
  '6 subjects');
ok(CALIBRATION_VIEWS.length === 4
  && subjects.every((s) => s.views.length === 4)
  && workspace.includes('CALIBRATION_VIEWS.map')
  && workspace.includes('selected.views')
  && workspace.includes('selectedSubjectId'),
  '4 views each / one subject at a time');
ok(subjects.every((s) => s.views[0].isIdentityAnchor)
  && workspace.includes('IDENTITY ANCHOR'),
  'FRONT anchor classification');
ok(subjects.every((s) => s.views.slice(1).every((v) => v.isIdentityConditioned))
  && workspace.includes('IDENTITY CONDITIONED')
  && workspace.includes('CONDITIONED BY FRONT'),
  'downstream identity-conditioned classification');
ok(DIRECTOR_REVIEW_GATES.length === 10
  && Object.keys(DIRECTOR_REVIEW_GATE_LABELS).length === 10
  && Object.keys(DIRECTOR_REVIEW_GATE_HELP).length === 10
  && surface.includes('DIRECTOR_REVIEW_GATES.map')
  && !card.includes('DIRECTOR_REVIEW_GATES.map'),
  '10 gate model in workspace only');

const pending = emptyDirectorReviewGates();
const allPass = { ...pending };
for (const key of DIRECTOR_REVIEW_GATES) allPass[key] = 'PASS';
const saved = { ...allPass };
ok(directorReviewSaveReload(saved, allPass)
  && workspace.includes('DIRECTOR_REVIEW_SAVE_LABEL')
  && DIRECTOR_REVIEW_SAVE_LABEL === 'Save Review'
  && DIRECTOR_REVIEW_SAVE_NEXT_LABEL === 'Save & Next Subject'
  && api.includes('saveIdentityConditionedCalibrationDirectorReview')
  && workspace.includes('fetchIdentityConditionedCalibrationDirectorReview'),
  'save review');
ok(workspace.includes('applyReview(reloaded')
  && directorReviewSaveReload(saved, { ...saved }),
  'reload review');
ok((card.includes('SUBJECTS PASS') || surface.includes('SUBJECTS PASS'))
  && surface.includes('VISUAL PASS')
  && surface.includes('PACK DECISION')
  && !directorReviewPackVisualPass({
    technicalValid: 24,
    anchors: 6,
    conditioned: 18,
    subjectPass: 0,
    fail: 0,
    reviewRequired: 0,
    allVisualPass: false,
    marked: false,
  })
  && directorReviewOverallLabel({ visualPass: false, packDecision: 'NOT_REVIEWED' })
    === 'PENDING DIRECTOR REVIEW',
  'pack status');
ok(!directorReviewTriggersGeneration()
  && !workspace.includes('generateVisualCalibrationPack')
  && !workspace.includes('generateCharacterStudio')
  && !card.includes('generateVisualCalibrationPack')
  && !service.includes('IImageGenerator')
  && !service.includes('GenerateAsync'),
  'no generation call');
ok(!service.includes('Runway')
  && !service.includes('IImageGenerator')
  && !api.includes('identity-conditioned-calibration/director-review/generate')
  && !workspace.includes('generateVisualCalibrationPack'),
  'no provider call');

ok(DIRECTOR_REVIEW_DECISIONS[0] === 'PENDING'
  && Object.values(pending).every((v) => v === 'PENDING')
  && !directorReviewMayPass('PASS', pending, 'PASS'),
  'gates and decision default PENDING');
ok(surface.includes('DIRECTOR_REVIEW_HISTORICAL_LABEL')
  && DIRECTOR_REVIEW_HISTORICAL_LABEL === 'HISTORICAL — NOT ELIGIBLE FOR CURRENT APPROVAL'
  && !panels.includes('defaultActiveKey')
  && workspace.includes('ContentFamixaDirectorHistoricalPanel')
  && !workspace.includes('historical.map'),
  'historical artifacts are not eligible');
ok(!directorReviewExposesMasterRevisionCta()
  && !workspace.includes('Bắt đầu tạo Master Revision')
  && !workspace.includes('startMasterRevision')
  && !card.includes('Bắt đầu tạo Master Revision'),
  'no Master Revision CTA on Director Review');
ok(!directorReviewExposesRegenerateCta()
  && !workspace.includes('Tạo lại bộ ảnh')
  && !workspace.includes('regenerateCharacterStudio')
  && !card.includes('Tạo lại bộ ảnh'),
  'no regenerate CTA on Director Review');
ok(!directorReviewShowsLockCta()
  && !workspace.includes('Khóa Visual Universe')
  && !workspace.includes('>Lock<')
  && !workspace.includes('[LOCK]'),
  'no lock CTA in Director Review');
ok(studio.includes('Director Review Workspace')
  && studio.includes('Bắt đầu tạo Master Revision')
  && studio.includes('Tạo lại bộ ảnh')
  && studio.includes('ContentFamixaCharacterCalibrationShortcut')
  && shortcut.includes('DIRECTOR_REVIEW_FROM_STUDIO_LABEL')
  && DIRECTOR_REVIEW_FROM_STUDIO_LABEL === 'Review Calibration'
  && !shortcut.includes('CALIBRATION_VIEWS.map'),
  'Character Studio Master Revision remains elsewhere + compact shortcut');
ok(!cal.includes('Save Review') && cal.includes('Tạo 24 ảnh Calibration')
  && cal.includes('DIRECTOR_REVIEW_OPEN_LABEL')
  && DIRECTOR_REVIEW_OPEN_LABEL === 'Open Director Review'
  && directorReviewWorkspacePath().includes('/director-review'),
  'generation stays on Visual Calibration, not Director Review');
ok(!directorReviewMarkPassEnabled({
  packDecision: 'NOT_REVIEWED',
  visualPass: false,
  technicalIntegrityValid: 24,
  subjectPass: 0,
})
  && directorReviewMarkPassEnabled({
    packDecision: 'PASS',
    visualPass: false,
    technicalIntegrityValid: 24,
    subjectPass: 6,
  })
  && surface.includes('DIRECTOR_REVIEW_MARK_PASS_LABEL')
  && DIRECTOR_REVIEW_MARK_PASS_LABEL === 'Mark Visual Calibration Pass'
  && workspace.includes('directorReviewMarkPassEnabled')
  && surface.includes('DIRECTOR_REVIEW_FOUNDATION_NEXT')
  && workspace.includes('Confirm Visual Calibration Pass')
  && workspace.includes('does NOT generate images'),
  'Visual Calibration Pass disabled until eligible');
ok(directorReviewWorkspaceDirty(
  { gates: pending, decision: 'PENDING', directorNote: '' },
  { gates: pending, decision: 'PASS', directorNote: '' },
)
  && !directorReviewWorkspaceDirty(
    { gates: pending, decision: 'PENDING', directorNote: '' },
    { gates: pending, decision: 'PENDING', directorNote: '' },
  )
  && workspace.includes('DIRECTOR_REVIEW_UNSAVED_MESSAGE')
  && workspace.includes('DIRECTOR_REVIEW_UNSAVED_LEAVE')
  && DIRECTOR_REVIEW_UNSAVED_LEAVE.includes('unsaved review changes')
  && workspace.includes('Stay')
  && workspace.includes('Leave')
  && directorReviewSubjectMark('PASS') === '✓'
  && directorReviewSubjectMark('FAIL') === '✕'
  && directorReviewSubjectMark('REVIEW_REQUIRED') === '!',
  'unsaved protection + subject marks');
ok(surface.includes('DIRECTOR_REVIEW_EXIT_LABEL')
  && DIRECTOR_REVIEW_EXIT_LABEL === 'Exit Review'
  && surface.includes('DIRECTOR_REVIEW_GUIDE')
  && DIRECTOR_REVIEW_GUIDE.includes('FRONT identity anchor')
  && workspace.includes('Next Subject')
  && workspace.includes('Previous Subject')
  && css.includes('grid-template-columns: 1fr 1fr')
  && css.includes('.fx-drws__view.is-anchor'),
  'workspace guide / exit / 2x2 comparison');
ok(directorReviewNoteRequired('FAIL')
  && directorReviewNoteRequired('REVIEW_REQUIRED')
  && !directorReviewNoteRequired('PASS')
  && !directorReviewNoteRequired('PENDING')
  && surface.includes('directorReviewNoteRequired')
  && workspace.includes('noteMissing'),
  'FAIL / REVIEW REQUIRED require note');
ok(!directorReviewPassEligible('PASS', pending)
  && directorReviewPassEligible('PASS', allPass)
  && !directorReviewPassEligible('FAIL', allPass)
  && workspace.includes('directorReviewPassEligible'),
  'PASS eligibility does not auto-score pending gates');
ok(!card.includes('CALIBRATION_VIEWS.map')
  && card.includes('DIRECTOR_REVIEW_OPEN_LABEL')
  && workspace.includes('selectedSubjectId'),
  'board card is compact entry, matrix lives in workspace');
ok(controller.includes('identity-conditioned-calibration/director-review')
  && controller.includes('identity-conditioned-calibration/director-review/ui/regression')
  && uiReg.includes('DR-UI-01')
  && uiReg.includes('DR-UI-20')
  && icReg.includes('IC-01')
  && foundation.includes('F-'),
  'API + UI regression + existing suites remain');
ok(card.includes(`id="${DIRECTOR_REVIEW_WORKSPACE_ID}"`) === false
  && card.includes('DIRECTOR_REVIEW_WORKSPACE_ID')
  && workspace.includes('DIRECTOR_REVIEW_WORKSPACE_ID'),
  'workspace id bound');
ok(directorReviewWorkspaceBadge({ visualPass: true }) === 'VISUAL CALIBRATION PASSED'
  && directorReviewWorkspaceBadge({ packDecision: 'PASS', visualPass: false }) === 'READY TO PASS'
  && directorReviewWorkspaceBadge({ fail: 1 }) === 'REVIEW BLOCKED'
  && directorReviewWorkspaceBadge({ technicalIntegrityValid: 0 }) === 'REVIEW BLOCKED'
  && directorReviewWorkspaceBadge({ subjectPass: 2 }) === 'REVIEW IN PROGRESS'
  && directorReviewDecisionVi('PENDING') === 'Chưa review'
  && directorReviewDecisionVi('PASS') === 'Đạt'
  && directorReviewDecisionVi('FAIL') === 'Không đạt'
  && directorReviewDecisionVi('REVIEW_REQUIRED') === 'Cần xem lại'
  && !workspace.includes('Chưa chấm')
  && surface.includes('SUBJECT DECISION')
  && surface.includes('DIRECTOR DECISION')
  && surface.includes('DIRECTOR GATES')
  && surface.includes('VISUAL REVIEW')
  && surface.includes('SUBJECT QUEUE')
  && surface.includes('DIRECTOR REVIEW SUMMARY')
  && workspace.includes('DIRECTOR_REVIEW_SAVE_NEXT_LABEL')
  && workspace.includes('directorReviewNextPendingSubject')
  && directorReviewNextPendingSubject([
    { subjectId: 'CAL-001', decision: 'PASS' },
    { subjectId: 'CAL-002', decision: 'PENDING' },
  ], 'CAL-001') === 'CAL-002'
  && DIRECTOR_REVIEW_GUIDE_VI.includes('Identity Anchor')
  && DIRECTOR_REVIEW_CONTINUE_FOUNDATION === 'Continue to Foundation'
  && DIRECTOR_REVIEW_PASS_SUBJECT === 'PASS SUBJECT'
  && DIRECTOR_REVIEW_PASS_BLOCKED === 'Cannot PASS this subject yet.'
  && workspace.includes('DIRECTOR_REVIEW_PASS_SUBJECT')
  && surface.includes('DIRECTOR_REVIEW_PASS_BLOCKED')
  && !workspace.includes('>Lock<')
  && !workspace.includes('Promote')
  && !workspace.includes('Bắt đầu tạo Master Revision')
  && shortcut.includes('DIRECTOR_REVIEW_OPEN_LABEL')
  && !studio.includes('DIRECTOR_REVIEW_GATES.map'),
  'V2 header / queue / status semantics');

const packPending = emptyPackUniverseGates();
const packPass = {
  VisualUniverseGate: 'PASS',
  StylizationGate: 'PASS',
  CrossCharacterGate: 'PASS',
  PhotorealismGate: 'PASS',
} as const;
const reviewIncomplete = {
  packDecision: 'NOT_REVIEWED',
  visualPass: false,
  technicalIntegrityValid: 24,
  subjectPass: 4,
  fail: 0,
  reviewRequired: 0,
  directorReviewCount: 4,
};
const reviewReady = {
  packDecision: 'PASS',
  visualPass: false,
  technicalIntegrityValid: 24,
  subjectPass: 6,
  fail: 0,
  reviewRequired: 0,
  directorReviewCount: 6,
};

ok(workspace.includes('DIRECTOR_REVIEW_WORKSPACE_ID') && page.includes('ContentFamixaDirectorReviewWorkspace'),
  'DR-UX-01 workspace loads');
ok(subjects.length === 6 && surface.includes('CHARACTER OVERVIEW') && surface.includes('subjects.map'),
  'DR-UX-02 6 characters displayed');
ok(workspace.includes('CURRENT CHARACTER') && surface.includes('is-current') && surface.includes('CURRENT'),
  'DR-UX-03 current character selected');
ok(workspace.includes('CALIBRATION_VIEWS.map') && workspace.includes('IMAGE COMPARISON'),
  'DR-UX-04 4 views displayed');
ok(workspace.includes('IDENTITY ANCHOR') && workspace.includes("view === 'FRONT'"),
  'DR-UX-05 FRONT identified as identity anchor');
ok(Object.values(pending).every((v) => v === 'PENDING') && DIRECTOR_REVIEW_DECISIONS[0] === 'PENDING'
  && surface.includes('Default: PENDING'),
  'DR-UX-06 all visual gates default PENDING');
ok(!directorReviewPassEligible('PASS', pending) && !directorReviewMayPass('PASS', pending, 'PASS'),
  'DR-UX-07 cannot PASS subject with incomplete gates');
const reviewRequiredGates = { ...allPass, IdentityGate: 'REVIEW_REQUIRED' as const };
ok(!directorReviewPassEligible('PASS', reviewRequiredGates),
  'DR-UX-08 cannot PASS subject with REVIEW_REQUIRED gate');
const failGates = { ...allPass, AgeGate: 'FAIL' as const };
ok(!directorReviewPassEligible('PASS', failGates),
  'DR-UX-09 cannot PASS subject with FAIL gate');
ok(directorReviewNoteRequired('FAIL') && DIRECTOR_REVIEW_NOTE_PLACEHOLDER.includes('Director'),
  'DR-UX-10 Director note required for FAIL');
ok(directorReviewNoteRequired('REVIEW_REQUIRED'),
  'DR-UX-11 Director note required for REVIEW_REQUIRED');
ok(workspace.includes('DIRECTOR_REVIEW_SAVE_LABEL') && workspace.includes('✓ Review saved'),
  'DR-UX-12 Save Review updates subject status');
ok(workspace.includes('DIRECTOR_REVIEW_NEXT_CHARACTER') && DIRECTOR_REVIEW_NEXT_CHARACTER.includes('Next Character')
  && workspace.includes('Next Subject'),
  'DR-UX-13 Next Character navigation works');
ok(workspace.includes('Previous Character') && workspace.includes('Previous Subject'),
  'DR-UX-14 Previous Character navigation works');
ok(surface.includes('PACK OVERVIEW') && surface.includes('Director Visual Review')
  && directorReviewPackScopeStatus(reviewIncomplete) === 'IN REVIEW',
  'DR-UX-15 Pack progress updates');
ok(!directorReviewMarkPassEnabled(reviewIncomplete)
  && directorReviewMarkPassReasons(reviewIncomplete, packPending).some((r) => r.includes('subjects not reviewed')),
  'DR-UX-16 Pack PASS remains disabled when subject is incomplete');
ok(!directorReviewUiCanMarkPass({
    ...reviewReady,
    packDecision: 'NOT_REVIEWED',
    subjectPass: 5,
    reviewRequired: 1,
  })
  && directorReviewMarkPassReasons({
    ...reviewReady,
    packDecision: 'NOT_REVIEWED',
    subjectPass: 5,
    reviewRequired: 1,
  }).some((r) => r.includes('REVIEW REQUIRED')),
  'DR-UX-17 Pack PASS remains disabled when a subject is REVIEW REQUIRED');
ok(directorReviewMarkPassEnabled(reviewReady)
  && directorReviewUiCanMarkPass(reviewReady, packPending)
  && directorReviewUiCanMarkPass(reviewReady, packPass)
  && directorReviewPackUniversePass(packPass),
  'DR-UX-18 Pack PASS enabled when 6/6 subject PASS and technical PASS');
ok(workspace.includes('Mark this Visual Calibration as PASS?')
  && workspace.includes('Confirm Visual Calibration Pass'),
  'DR-UX-19 Final confirmation appears');
ok(!directorReviewTriggersGeneration()
  && !workspace.includes('generateVisualCalibrationPack')
  && workspace.includes('does NOT generate images'),
  'DR-UX-20 Final PASS performs no generation/provider call');
ok(!workspace.includes('>Lock<') && !workspace.includes('Promote')
  && workspace.includes('does not generate images, modify Master/DNA/PRP'),
  'DR-UX-21 Final PASS performs no authority mutation');
ok(workspace.includes('modify Master/DNA/PRP')
  && !workspace.includes('startMasterRevision')
  && !service.includes('GenerateAsync'),
  'DR-UX-22 Final PASS performs no Master/DNA/PRP mutation');
ok(workspace.includes('ContentFamixaDirectorHistoricalPanel') && !workspace.includes('historical.map'),
  'DR-UX-23 Historical pixels remain excluded');
ok(workspace.includes('DIRECTOR_REVIEW_MEMORY_RESET')
  && DIRECTOR_REVIEW_MEMORY_RESET.includes('in-memory')
  && directorReviewMemoryResetDetected(
    { runId: 'run-1', reviewed: 3, visualPass: false },
    { calibrationRunId: 'run-1', subjects: [{ decision: 'PENDING' }], visualPass: false },
  )
  && !directorReviewMemoryResetDetected(
    { runId: 'run-1', reviewed: 0, visualPass: false },
    { calibrationRunId: 'run-1', subjects: [{ decision: 'PENDING' }], visualPass: false },
  )
  && DIRECTOR_REVIEW_BACK_CALIBRATION === 'Back to Visual Calibration'
  && DIRECTOR_REVIEW_GO_PACK === 'Go to Pack Review'
  && DIRECTOR_REVIEW_PASSED_LABEL === 'Visual Calibration Passed'
  && DIRECTOR_REVIEW_NO_REGEN === 'Do not regenerate automatically.'
  && DIRECTOR_REVIEW_GATE_HELP.AgeGate.includes('identical proportions')
  && directorReviewSubjectScopeStatus('PENDING', 0) === 'NOT REVIEWED'
  && directorReviewSubjectScopeStatus('PENDING', 3) === 'IN REVIEW'
  && directorReviewGateCompletedCount(pending) === 0
  && directorReviewImagesReadyCount(subjects[0].views) === 0
  && surface.includes('VISUAL UNIVERSE OVERVIEW')
  && surface.includes('SUBJECT VISUAL REVIEW'),
  'DR-UX-24 API recycle/in-memory reset is represented honestly');

ok(directorReviewWorkflowPhase({
  visualPass: false,
  packDecision: 'NOT_REVIEWED',
  technicalIntegrityValid: 24,
  subjectPass: 1,
  fail: 0,
  reviewRequired: 0,
}) === 'IN_PROGRESS'
  && surface.includes('DIRECTOR_REVIEW_REVIEW_NEXT')
  && DIRECTOR_REVIEW_REVIEW_NEXT === 'REVIEW NEXT CHARACTER',
  'DR-WF-01 1/6 PASS → Review Next Character');
ok(directorReviewWorkflowPhase({
  visualPass: false,
  packDecision: 'NOT_REVIEWED',
  technicalIntegrityValid: 24,
  subjectPass: 5,
  fail: 0,
  reviewRequired: 0,
}) === 'IN_PROGRESS'
  && directorReviewNextPendingSubject([
    { subjectId: 'CAL-001', decision: 'PASS' },
    { subjectId: 'CAL-002', decision: 'PASS' },
    { subjectId: 'CAL-003', decision: 'PASS' },
    { subjectId: 'CAL-004', decision: 'PASS' },
    { subjectId: 'CAL-005', decision: 'PASS' },
    { subjectId: 'CAL-006', decision: 'PENDING' },
  ], 'CAL-005') === 'CAL-006',
  'DR-WF-02 5/6 PASS → Review remaining character');
ok(directorReviewWorkflowPhase(reviewReady) === 'READY'
  && surface.includes('DIRECTOR_REVIEW_COMPLETE_TITLE')
  && DIRECTOR_REVIEW_COMPLETE_TITLE === 'DIRECTOR REVIEW COMPLETE',
  'DR-WF-03 6/6 PASS → Pack Ready');
ok(directorReviewMarkPassEnabled(reviewReady)
  && directorReviewUiCanMarkPass(reviewReady)
  && surface.includes('DIRECTOR_REVIEW_MARK_PASS_VI')
  && DIRECTOR_REVIEW_MARK_PASS_VI === 'Đánh dấu Visual Calibration PASS',
  'DR-WF-04 6/6 PASS + technical PASS → Mark Visual Calibration Pass enabled');
ok(!directorReviewMarkPassEnabled({
  packDecision: 'NOT_REVIEWED',
  visualPass: false,
  technicalIntegrityValid: 24,
  subjectPass: 5,
}),
  'DR-WF-05 5/6 PASS → Mark Visual Calibration Pass disabled');
ok(directorReviewWorkflowPhase({
  visualPass: false,
  packDecision: 'FAIL',
  technicalIntegrityValid: 24,
  subjectPass: 5,
  fail: 1,
  reviewRequired: 0,
}) === 'BLOCKED'
  && !directorReviewMarkPassEnabled({
    packDecision: 'FAIL',
    visualPass: false,
    technicalIntegrityValid: 24,
    subjectPass: 5,
  }),
  'DR-WF-06 one FAIL → Mark disabled');
ok(directorReviewWorkflowPhase({
  visualPass: false,
  packDecision: 'REVIEW_REQUIRED',
  technicalIntegrityValid: 24,
  subjectPass: 5,
  fail: 0,
  reviewRequired: 1,
}) === 'INCOMPLETE'
  && !directorReviewUiCanMarkPass({
    packDecision: 'REVIEW_REQUIRED',
    visualPass: false,
    technicalIntegrityValid: 24,
    subjectPass: 5,
  }),
  'DR-WF-07 one REVIEW_REQUIRED → Mark disabled');
ok(directorReviewWorkflowPhase({
  visualPass: false,
  packDecision: 'NOT_REVIEWED',
  technicalIntegrityValid: 0,
  subjectPass: 6,
  fail: 0,
  reviewRequired: 0,
}, { imagesMissing: true }) === 'TECHNICAL_BLOCK'
  && directorReviewWorkflowPhase({
    visualPass: false,
    packDecision: 'NOT_REVIEWED',
    technicalIntegrityValid: 0,
    subjectPass: 6,
    fail: 0,
    reviewRequired: 0,
  }) === 'IN_PROGRESS'
  && !directorReviewMarkPassEnabled({
    packDecision: 'PASS',
    visualPass: false,
    technicalIntegrityValid: 0,
    subjectPass: 6,
  }),
  'DR-WF-08 technical FAIL → Mark disabled');
ok(directorReviewWorkflowPhase({ ...reviewReady, visualPass: true }) === 'PASSED'
  && workspace.includes('markIdentityConditionedCalibrationVisualPass')
  && workspace.includes('visualPass'),
  'DR-WF-09 confirm Mark → VisualPass TRUE');
ok(!workspace.includes('startMasterRevision')
  && !workspace.includes('Bắt đầu tạo Master Revision')
  && surface.includes('Authority SHA'),
  'DR-WF-10 confirm Mark → authority SHA unchanged');
ok(!directorReviewTriggersGeneration() && !workspace.includes('generateVisualCalibrationPack'),
  'DR-WF-11 confirm Mark → no generation');
ok(!workspace.includes('generateVisualCalibrationPack') && !service.includes('IImageGenerator'),
  'DR-WF-12 confirm Mark → no provider call');
ok(!service.includes('GenerateAsync') && !workspace.includes('ContentGeminiClient'),
  'DR-WF-13 confirm Mark → no Gemini call');
ok(!directorReviewExposesMasterRevisionCta()
  && !workspace.includes('Tạo Revision mới')
  && surface.includes('DIRECTOR_REVIEW_MARK_PASS_LABEL'),
  'DR-WF-14 PASS state → Revision is not primary CTA');
ok(!directorReviewExposesRegenerateCta()
  && !workspace.includes('Tạo lại bộ ảnh'),
  'DR-WF-15 PASS state → Regeneration is not primary CTA');
ok(workspace.includes('DIRECTOR_REVIEW_MEMORY_HINT')
  && DIRECTOR_REVIEW_MEMORY_RESET.includes('in-memory'),
  'DR-WF-16 API recycle/in-memory behavior remains unchanged');

const handoff = read('ContentFamixaDirectorReviewHandoff.tsx');
ok(handoff.includes('DIRECTOR_REVIEW_START')
  && DIRECTOR_REVIEW_START === 'BẮT ĐẦU DUYỆT'
  && DIRECTOR_REVIEW_SAVE_VI === 'LƯU DUYỆT'
  && DIRECTOR_REVIEW_NEXT_VI_CTA.includes('NHÂN VẬT TIẾP THEO')
  && DIRECTOR_REVIEW_MARK_PASS_VI_BTN === 'ĐÁNH DẤU VISUAL CALIBRATION PASS'
  && studio.includes('Revision / Advanced Actions')
  && cal.includes('DIRECTOR_REVIEW_ADVANCED')
  && board.includes('ContentFamixaCharacterCalibrationShortcut')
  && directorReviewHandoffCta({ subjects: [] }) === DIRECTOR_REVIEW_START
  && directorReviewHandoffCta({
    subjects: Array.from({ length: 6 }, (_, i) => ({ subjectId: `CAL-00${i + 1}`, decision: 'PASS' })),
  }) === DIRECTOR_REVIEW_MARK_PASS_VI_BTN
  && directorReviewMarkPassEnabled({
    packDecision: 'NOT_REVIEWED',
    visualPass: false,
    technicalIntegrityValid: 0,
    subjectPass: 0,
    subjects: Array.from({ length: 6 }, (_, i) => ({ subjectId: `CAL-00${i + 1}`, decision: 'PASS' })),
  })
  && directorReviewDecisionPassCount([{ decision: 'PASS' }, { decision: 'PENDING' }]) === 1,
  'DR-REAL primary CTA is BẮT ĐẦU DUYỆT / LƯU DUYỆT / NEXT / MARK PASS');

const displayJobs = directorReviewDisplayJobs(subjects, 'CAL-002', 'FAMIXA-VISUAL-CALIBRATION-V1');
ok(workspace.includes('selectedSubjectId') && !workspace.includes('review?.subjects.map((subject) =>')
  && surface.includes('CHARACTER REVIEW'),
  'DRUX-01 — one active character');
ok(surface.includes('SUBJECT QUEUE') && surface.includes('directorReviewNavStatus')
  && directorReviewNavStatus('PENDING') === '○ NOT REVIEWED'
  && directorReviewNavStatus('PENDING', 2) === '◐ IN REVIEW'
  && directorReviewNavStatus('PASS') === '✓ PASSED'
  && directorReviewNavStatus('FAIL') === '✕ FAILED',
  'DRUX-02 — character navigation');
ok(workspace.includes('directorReviewDisplayJobs')
  && workspace.includes('calibrationSlotImageUrl')
  && displayJobs.some((job) => job[0] === 'CAL-002/FRONT')
  && displayJobs.some((job) => job[0] === 'CAL-002/THREE_QUARTER')
  && displayJobs.some((job) => job[0] === 'CAL-002/SIDE')
  && displayJobs.some((job) => job[0] === 'CAL-002/FULL_BODY')
  && !displayJobs.some((job) => job[0] === 'CAL-001/SIDE'),
  'DRUX-03 — 4 calibration images load');
ok(workspace.includes('IMAGE NOT AVAILABLE')
  && workspace.includes('Image unavailable')
  && workspace.includes('imageLoadDone')
  && directorReviewLoadedImageCount({ 'CAL-001/FRONT': 'blob:1' }, 'CAL-001') === 1,
  'DRUX-04 — image unavailable only on actual missing artifact');
ok(directorReviewWorkflowPhase({ technicalIntegrityValid: 0 }, { imagesMissing: true }) === 'TECHNICAL_BLOCK'
  && !directorReviewMarkPassEnabled({ packDecision: 'PASS', technicalIntegrityValid: 0, subjectPass: 6 })
  && surface.includes('DIRECTOR_REVIEW_IMAGES_BLOCKED_LINE'),
  'DRUX-05 — technical block prevents approval');
ok(Object.values(pending).every((v) => v === 'PENDING') && surface.includes('Default: PENDING'),
  'DRUX-06 — 10 criteria default PENDING');
ok(!directorReviewSaveReady(pending, 'PASS')
  && directorReviewSaveReady(allPass, 'PASS')
  && DIRECTOR_REVIEW_GATE_GROUPS.length === 3
  && DIRECTOR_REVIEW_GATES.length === 10,
  'DRUX-07 — all criteria required');
ok(!directorReviewSaveReady(allPass, 'PENDING') && directorReviewSaveReady(allPass, 'FAIL'),
  'DRUX-08 — explicit Director decision required');
ok(workspace.includes('saveIdentityConditionedCalibrationDirectorReview')
  && workspace.includes('✓ Review saved')
  && workspace.includes('directorReviewPersistRunId')
  && directorReviewPersistRunId({ calibrationRunId: 'CAL-EXEC-1' }) === 'CAL-EXEC-1'
  && directorReviewSaveGateMessage('CALIBRATION_RUN_MISMATCH').includes('Tải lại trang'),
  'DRUX-09 — Save Review persists current review');
ok(workspace.includes('DIRECTOR_REVIEW_NEXT_CHARACTER')
  && surface.includes('Review next character')
  && workspace.includes('ContentFamixaDirectorNextStep'),
  'DRUX-10 — next character navigation');
ok(surface.includes('characters approved') && surface.includes('subjectPass'),
  'DRUX-11 — 6/6 progress');
ok(!directorReviewMarkPassEnabled({
  packDecision: 'NOT_REVIEWED',
  visualPass: false,
  technicalIntegrityValid: 24,
  subjectPass: 5,
}) && workspace.includes('directorReviewMarkPassEnabled'),
  'DRUX-12 — final pass disabled before 6/6');
ok(directorReviewMarkPassEnabled(reviewReady)
  && surface.includes(DIRECTOR_REVIEW_READY_TITLE)
  && surface.includes('ĐÁNH DẤU VISUAL CALIBRATION PASS'),
  'DRUX-13 — final pass enabled at 6/6');
ok(!directorReviewTriggersGeneration()
  && !workspace.includes('generateVisualCalibrationPack')
  && workspace.includes('does NOT generate images'),
  'DRUX-14 — final pass does not generate');
ok(!workspace.includes('startMasterRevision')
  && workspace.includes('does not generate images, modify Master/DNA/PRP'),
  'DRUX-15 — final pass does not mutate authority');
ok(!service.includes('IImageGenerator') && !workspace.includes('generateVisualCalibrationPack'),
  'DRUX-16 — no provider calls');
ok(!service.includes('CREATE TABLE') && !workspace.includes('migration'),
  'DRUX-17 — no database migration');
ok(workspace.includes('calibrationSlotImageUrl')
  && !workspace.includes('generateVisualCalibrationPack')
  && DIRECTOR_REVIEW_OPEN_COMPARISON === 'Open large comparison',
  'DRUX-18 — existing artifact references remain unchanged');
ok(calPage.includes('markIdentityConditionedCalibrationVisualPass')
  && calPage.includes('Confirm Visual Calibration Pass')
  && calPage.includes('does NOT generate images')
  && cal.includes('onMark')
  && handoff.includes('onMark'),
  'Visual Calibration page records Mark Pass without leaving this screen');

if (fail.length) {
  console.error('FAIL', fail);
  process.exit(1);
}
console.log('PASS famixa-identity-conditioned-calibration-director-review.smoke.ts');
