import { useEffect, useMemo, useState } from 'react';
import { Button, Collapse, Modal, Space } from 'antd';
import {
  fetchIdentityConditionedCalibrationDirectorReview,
  fetchVisualCalibrationPack,
  markIdentityConditionedCalibrationVisualPass,
  saveIdentityConditionedCalibrationDirectorReview,
  type IdentityConditionedCalibrationPackReview,
  type IdentityConditionedCalibrationSubjectReview,
  type VisualCalibrationPack,
} from '@/shared/api/content.api';
import { ContentFamixaVisualModeBadge } from './ContentFamixaVisualModeBadge';
import { http } from '@/shared/api/http';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  CALIBRATION_VIEWS,
  DIRECTOR_REVIEW_GATES,
  DIRECTOR_REVIEW_FAIL_REASONS,
  DIRECTOR_REVIEW_ADVANCED,
  DIRECTOR_REVIEW_ADVANCED_ENGINEERING,
  DIRECTOR_REVIEW_OPEN_COMPARISON,
  DIRECTOR_REVIEW_COMPLETE_TITLE,
  DIRECTOR_REVIEW_DONE_VI,
  DIRECTOR_REVIEW_GO_PACK,
  DIRECTOR_REVIEW_MARK_PASS_VI_BTN,
  DIRECTOR_REVIEW_NEXT_VI_CTA,
  DIRECTOR_REVIEW_SAVE_VI,
  DIRECTOR_REVIEW_KEEP_FAIL,
  DIRECTOR_REVIEW_MARK_PASS_LABEL,
  DIRECTOR_REVIEW_MARK_PASS_WARNING,
  DIRECTOR_REVIEW_MEMORY_HINT,
  DIRECTOR_REVIEW_MEMORY_RESET,
  DIRECTOR_REVIEW_NEXT_CHARACTER,
  DIRECTOR_REVIEW_PACK_SUMMARY_LABEL,
  DIRECTOR_REVIEW_PACK_UNIVERSE_KEY,
  DIRECTOR_REVIEW_PASS_SUBJECT,
  DIRECTOR_REVIEW_PROGRESS_KEY,
  DIRECTOR_REVIEW_SAVE_LABEL,
  DIRECTOR_REVIEW_SAVE_NEXT_LABEL,
  DIRECTOR_REVIEW_UNSAVED_LEAVE,
  DIRECTOR_REVIEW_UNSAVED_MESSAGE,
  DIRECTOR_REVIEW_WORKSPACE_ID,
  VISUAL_CALIBRATION_PACK_ID,
  calibrationSlotImageUrl,
  calibrationViewCompositeLabel,
  calibrationViewRoleLabel,
  directorReviewDecisionVi,
  directorReviewGateCompletedCount,
  directorReviewImagesReadyCount,
  directorReviewMarkPassBlockedReason,
  directorReviewMarkPassEnabled,
  directorReviewMarkPassReasons,
  directorReviewMemoryResetDetected,
  directorReviewDisplayJobs,
  directorReviewLoadedImageCount,
  directorReviewPackSlotPresent,
  directorReviewNextDecisionSubject,
  directorReviewNextPendingSubject,
  directorReviewNoteRequired,
  directorReviewPassEligible,
  directorReviewSubjectRoleLine,
  directorReviewSubjectScopeStatus,
  directorReviewSubjectsReviewed,
  directorReviewWorkflowPhase,
  directorReviewUiCanMarkPass,
  directorReviewPrimaryAction,
  directorReviewSaveReady,
  directorReviewPersistRunId,
  directorReviewSaveGateMessage,
  directorReviewSubjectChipVi,
  directorReviewViewBadge,
  directorReviewViewsComplete,
  directorReviewWorkspaceDirty,
  visualCalibrationWorkspacePath,
  emptyDirectorReviewGates,
  emptyPackUniverseGates,
  type DirectorReviewDecision,
  type DirectorReviewGates,
  type DirectorReviewPackUniverseGates,
} from './kit-video-visual-calibration';
import {
  ContentFamixaDirectorDecisionPanel,
  ContentFamixaDirectorGatePanel,
  ContentFamixaDirectorHistoricalPanel,
  ContentFamixaDirectorImageStatus,
  ContentFamixaDirectorNextStep,
  ContentFamixaDirectorPackOverview,
  ContentFamixaDirectorPackSummaryView,
  ContentFamixaDirectorReviewHeader,
  ContentFamixaDirectorWorkflowBanner,
  ContentFamixaDirectorStickyStrip,
  ContentFamixaDirectorSubjectNavigator,
  ContentFamixaDirectorTechnicalDetails,
  ContentFamixaDirectorUniverseOverview,
} from './ContentFamixaDirectorReviewPanels';

async function authImage(url: string) {
  const { data } = await http.get<Blob>(url.replace(/^\/api/, ''), { responseType: 'blob' });
  return URL.createObjectURL(data);
}

function gatesFromSubject(subject?: IdentityConditionedCalibrationSubjectReview) {
  if (!subject) return emptyDirectorReviewGates();
  return {
    IdentityGate: subject.gates.identityGate,
    AgeGate: subject.gates.ageGate,
    AppearanceGate: subject.gates.appearanceGate,
    FaceConsistencyGate: subject.gates.faceConsistencyGate,
    ViewConsistencyGate: subject.gates.viewConsistencyGate,
    WardrobeConsistencyGate: subject.gates.wardrobeConsistencyGate,
    VisualUniverseGate: subject.gates.visualUniverseGate,
    StylizationGate: subject.gates.stylizationGate,
    CrossCharacterGate: subject.gates.crossCharacterGate,
    PhotorealismGate: subject.gates.photorealismGate,
  } as DirectorReviewGates;
}

type Draft = {
  gates: DirectorReviewGates;
  decision: DirectorReviewDecision;
  directorNote: string;
  failureReason: (typeof DIRECTOR_REVIEW_FAIL_REASONS)[number];
};

function draftFromSubject(subject?: IdentityConditionedCalibrationSubjectReview): Draft {
  const reason = subject?.failureReason
    && DIRECTOR_REVIEW_FAIL_REASONS.includes(subject.failureReason as (typeof DIRECTOR_REVIEW_FAIL_REASONS)[number])
    ? subject.failureReason as (typeof DIRECTOR_REVIEW_FAIL_REASONS)[number]
    : DIRECTOR_REVIEW_FAIL_REASONS[0];
  return {
    gates: gatesFromSubject(subject),
    decision: ((subject?.decision as DirectorReviewDecision) || 'PENDING'),
    directorNote: subject?.directorNote || '',
    failureReason: reason,
  };
}

function payloadOf(draft: Draft) {
  return {
    identityGate: draft.gates.IdentityGate,
    ageGate: draft.gates.AgeGate,
    appearanceGate: draft.gates.AppearanceGate,
    faceConsistencyGate: draft.gates.FaceConsistencyGate,
    viewConsistencyGate: draft.gates.ViewConsistencyGate,
    wardrobeConsistencyGate: draft.gates.WardrobeConsistencyGate,
    visualUniverseGate: draft.gates.VisualUniverseGate,
    stylizationGate: draft.gates.StylizationGate,
    crossCharacterGate: draft.gates.CrossCharacterGate,
    photorealismGate: draft.gates.PhotorealismGate,
    decision: draft.decision,
    directorNote: draft.directorNote,
    failureReason: draft.decision === 'FAIL' ? draft.failureReason : undefined,
    markVisualPass: false,
  };
}

export function ContentFamixaDirectorReviewWorkspace({
  packId = VISUAL_CALIBRATION_PACK_ID,
  initialSubjectId,
}: {
  packId?: string;
  initialSubjectId?: string;
}) {
  const [review, setReview] = useState<IdentityConditionedCalibrationPackReview>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [savedFlash, setSavedFlash] = useState(false);
  const [images, setImages] = useState<Record<string, string>>({});
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>();
  const [draft, setDraft] = useState<Draft>(draftFromSubject());
  const [savedDraft, setSavedDraft] = useState<Draft>(draftFromSubject());
  const [lightbox, setLightbox] = useState<string>();
  const [zoom, setZoom] = useState(false);
  const [pendingSubjectId, setPendingSubjectId] = useState<string>();
  const [confirmMark, setConfirmMark] = useState(false);
  const [confirmFail, setConfirmFail] = useState(false);
  const [pane, setPane] = useState<'review' | 'summary' | 'complete'>('review');
  const [packGates, setPackGates] = useState<DirectorReviewPackUniverseGates>(emptyPackUniverseGates());
  const [memoryReset, setMemoryReset] = useState(false);
  const [savedSubjectLabel, setSavedSubjectLabel] = useState<string>();
  const [pack, setPack] = useState<VisualCalibrationPack>();
  const [imageLoadDone, setImageLoadDone] = useState(false);
  const [compareOpen, setCompareOpen] = useState(false);

  const dirty = directorReviewWorkspaceDirty(draft, savedDraft);

  const applyReview = (workspace: IdentityConditionedCalibrationPackReview, keepSubject?: string) => {
    setReview(workspace);
    const nextId = keepSubject
      && workspace.subjects.some((s) => s.subjectId === keepSubject)
      ? keepSubject
      : workspace.subjects[0]?.subjectId;
    setSelectedSubjectId(nextId);
    const subject = workspace.subjects.find((s) => s.subjectId === nextId);
    const nextDraft = draftFromSubject(subject);
    setDraft(nextDraft);
    setSavedDraft(nextDraft);
    const key = `${DIRECTOR_REVIEW_PACK_UNIVERSE_KEY}-${workspace.calibrationRunId || 'none'}`;
    try {
      const raw = sessionStorage.getItem(key);
      setPackGates(raw ? { ...emptyPackUniverseGates(), ...JSON.parse(raw) } : emptyPackUniverseGates());
    } catch {
      setPackGates(emptyPackUniverseGates());
    }
    try {
      const prevRaw = sessionStorage.getItem(DIRECTOR_REVIEW_PROGRESS_KEY);
      const prev = prevRaw ? JSON.parse(prevRaw) as { runId?: string | null; reviewed?: number; visualPass?: boolean } : null;
      setMemoryReset(directorReviewMemoryResetDetected(prev, workspace));
      sessionStorage.setItem(DIRECTOR_REVIEW_PROGRESS_KEY, JSON.stringify({
        runId: workspace.calibrationRunId,
        reviewed: directorReviewSubjectsReviewed(workspace.subjects),
        visualPass: workspace.visualPass,
      }));
    } catch {
      setMemoryReset(false);
    }
  };

  useEffect(() => {
    setBusy(true);
    setLoading(true);
    void Promise.all([
      fetchIdentityConditionedCalibrationDirectorReview(),
      fetchVisualCalibrationPack(packId).catch(() => undefined),
    ])
      .then(([workspace, visualPack]) => {
        if (visualPack) setPack(visualPack);
        applyReview(workspace, initialSubjectId);
        const phase = directorReviewWorkflowPhase(workspace);
        if (phase === 'READY' || phase === 'PASSED') setPane('complete');
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được Director Review.')))
      .finally(() => {
        setBusy(false);
        setLoading(false);
      });
  }, []);

  useEffect(() => {
    if (!dirty) return;
    const onLeave = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = DIRECTOR_REVIEW_UNSAVED_MESSAGE;
    };
    window.addEventListener('beforeunload', onLeave);
    return () => window.removeEventListener('beforeunload', onLeave);
  }, [dirty]);

  const selected = review?.subjects.find((s) => s.subjectId === selectedSubjectId);
  const subjects = useMemo(() => review?.subjects ?? [], [review]);

  useEffect(() => {
    let dead = false;
    const made: string[] = [];
    const jobs = directorReviewDisplayJobs(review?.subjects, selected?.subjectId, review?.packId || packId);
    void calibrationSlotImageUrl;
    setImageLoadDone(false);
    void Promise.all(
      jobs.map(([key, url]) =>
        authImage(url)
          .then((blob) => {
            made.push(blob);
            return [key, blob] as const;
          })
          .catch(() => undefined),
      ),
    ).then((pairs) => {
      if (dead) {
        made.forEach((u) => URL.revokeObjectURL(u));
        return;
      }
      const next: Record<string, string> = {};
      for (const pair of pairs) if (pair) next[pair[0]] = pair[1];
      setImages(next);
      setImageLoadDone(true);
    });
    return () => {
      dead = true;
      made.forEach((u) => URL.revokeObjectURL(u));
    };
  }, [review?.calibrationRunId, selected?.subjectId, review?.packId, packId]);

  const isolatedCanPass = directorReviewPassEligible(selected?.technicalStatus || 'FAIL', draft.gates)
    && directorReviewViewsComplete(selected?.views);
  const loadedImages = directorReviewLoadedImageCount(images, selected?.subjectId);
  const imagesComplete = loadedImages === CALIBRATION_VIEWS.length;
  const allGatesPass = DIRECTOR_REVIEW_GATES.every((key) => draft.gates[key] === 'PASS');
  const canPass = isolatedCanPass || (allGatesPass && imagesComplete);
  const runId = directorReviewPersistRunId(review, pack);
  const markEnabled = directorReviewMarkPassEnabled(review);
  const uiCanMark = directorReviewUiCanMarkPass(review, packGates);
  const markBlocked = directorReviewMarkPassBlockedReason(review);
  const markReasons = directorReviewMarkPassReasons(review, packGates);
  const imagesMissing = imageLoadDone && loadedImages < CALIBRATION_VIEWS.length;
  const noteMissing = directorReviewNoteRequired(draft.decision) && !draft.directorNote.trim();
  const saveReady = directorReviewSaveReady(draft.gates, draft.decision)
    && !noteMissing
    && (draft.decision !== 'PASS' || imagesComplete);
  const selectedIndex = subjects.findIndex((s) => s.subjectId === selectedSubjectId);
  const prevSubject = selectedIndex > 0 ? subjects[selectedIndex - 1] : undefined;
  const nextSubject = selectedIndex >= 0 && selectedIndex < subjects.length - 1
    ? subjects[selectedIndex + 1]
    : undefined;
  const readOnly = Boolean(review?.visualPass);
  const imagesReady = imagesComplete
    ? CALIBRATION_VIEWS.length
    : Math.max(loadedImages, directorReviewImagesReadyCount(selected?.views));
  const gatesCompleted = directorReviewGateCompletedCount(draft.gates);
  const subjectStatus = directorReviewSubjectScopeStatus(draft.decision, gatesCompleted);
  const nextPendingId = directorReviewNextPendingSubject(subjects, selectedSubjectId);
  const nextPending = subjects.find((s) => s.subjectId === nextPendingId) || nextSubject;
  const phase = directorReviewWorkflowPhase(review, { imagesMissing });
  const primaryAction = directorReviewPrimaryAction(phase, {
    savedPass: savedFlash && draft.decision === 'PASS',
    hasNext: Boolean(nextPending),
  });

  const commitSubject = (subjectId: string) => {
    const subject = review?.subjects.find((s) => s.subjectId === subjectId);
    setSelectedSubjectId(subjectId);
    const nextDraft = draftFromSubject(subject);
    setDraft(nextDraft);
    setSavedDraft(nextDraft);
    setPendingSubjectId(undefined);
    setSavedFlash(false);
    setImageLoadDone(false);
    setCompareOpen(false);
    setLightbox(undefined);
  };

  const requestSubject = (subjectId: string) => {
    if (subjectId === selectedSubjectId) return;
    if (dirty) {
      setPendingSubjectId(subjectId);
      return;
    }
    commitSubject(subjectId);
  };

  const persist = (
    nextDraft: Draft = draft,
    then?: (reloaded: IdentityConditionedCalibrationPackReview) => void,
  ) => {
    if (!runId || !selectedSubjectId) {
      setError(!selectedSubjectId
        ? 'Chưa chọn nhân vật để lưu.'
        : 'Chưa có phiên review để lưu. Tải lại trang rồi nhấn Lưu duyệt.');
      return;
    }
    const noteGap = directorReviewNoteRequired(nextDraft.decision) && !nextDraft.directorNote.trim();
    if (noteGap) {
      setError('Director Note is required for FAIL / REVIEW REQUIRED.');
      return;
    }
    if (nextDraft.decision === 'PASS' && !DIRECTOR_REVIEW_GATES.every((key) => nextDraft.gates[key] === 'PASS')) {
      setError('PASS cần cả 10 visual gates = PASS.');
      return;
    }
    if (nextDraft.decision === 'PASS' && loadedImages < CALIBRATION_VIEWS.length) {
      setError('PASS requires all 4 current views, including FRONT identity anchor.');
      return;
    }
    if (!directorReviewSaveReady(nextDraft.gates, nextDraft.decision)) {
      setError('Chọn đủ 10 tiêu chí và Director decision trước khi lưu.');
      return;
    }
    setDraft(nextDraft);
    setBusy(true);
    void saveIdentityConditionedCalibrationDirectorReview(runId, selectedSubjectId, payloadOf(nextDraft))
      .then((workspace) => {
        const gateMessage = directorReviewSaveGateMessage(workspace.gateCode);
        if (gateMessage) throw new Error(gateMessage);
        return fetchIdentityConditionedCalibrationDirectorReview(workspace.calibrationRunId || runId);
      })
      .then((reloaded) => {
        applyReview(reloaded, selectedSubjectId);
        setSavedFlash(true);
        setSavedSubjectLabel(selected?.label);
        const saved = reloaded.subjects.find((s) => s.subjectId === selectedSubjectId);
        const phase = directorReviewWorkflowPhase(reloaded);
        if (phase === 'READY' || phase === 'PASSED') setPane('complete');
        if (nextDraft.decision === 'PASS' && (saved?.decision || '').toUpperCase() !== 'PASS') {
          setError('Đã lưu tiêu chí, nhưng PASS chưa được ghi nhận. Tải lại trang rồi thử lại.');
        } else {
          setError(undefined);
        }
        window.setTimeout(() => setSavedFlash(false), 2400);
        then?.(reloaded);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không lưu được Director Review.')))
      .finally(() => setBusy(false));
  };

  const saveReview = (nextDraft: Draft = draft) => persist(nextDraft);

  const saveAndNext = () => {
    persist(draft, (reloaded) => {
      const nextId = directorReviewNextPendingSubject(reloaded.subjects, selectedSubjectId);
      if (directorReviewWorkflowPhase(reloaded) === 'READY' || directorReviewWorkflowPhase(reloaded) === 'PASSED') {
        setPane('complete');
        return;
      }
      if (nextId) {
        setPane('review');
        commitSubject(nextId);
        return;
      }
      setPane('complete');
    });
  };

  const openSummary = () => {
    if (dirty) {
      setPendingSubjectId('__summary__');
      return;
    }
    setPane('summary');
  };

  const persistPackGates = (next: DirectorReviewPackUniverseGates) => {
    setPackGates(next);
    try {
      sessionStorage.setItem(
        `${DIRECTOR_REVIEW_PACK_UNIVERSE_KEY}-${runId || 'none'}`,
        JSON.stringify(next),
      );
    } catch {
      /* in-memory only */
    }
  };

  const confirmVisualPass = () => {
    if (!runId || !markEnabled) return;
    setBusy(true);
    setConfirmMark(false);
    void markIdentityConditionedCalibrationVisualPass(runId)
      .then((workspace) => {
        applyReview(workspace, selectedSubjectId);
        setPane('complete');
        if (workspace.gateCode) throw new Error(workspace.gateCode);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa thể đánh VISUAL PASS.')))
      .finally(() => setBusy(false));
  };

  const lightboxIndex = lightbox ? CALIBRATION_VIEWS.indexOf(lightbox as (typeof CALIBRATION_VIEWS)[number]) : -1;
  const stepView = (delta: number) => {
    if (lightboxIndex < 0) return;
    setLightbox(CALIBRATION_VIEWS[(lightboxIndex + delta + CALIBRATION_VIEWS.length) % CALIBRATION_VIEWS.length]);
    setZoom(false);
  };

  useEffect(() => {
    if (!lightbox) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setLightbox(undefined);
      if (e.key === 'ArrowLeft') stepView(-1);
      if (e.key === 'ArrowRight') stepView(1);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [lightbox, lightboxIndex]);

  if (loading) {
    return (
      <section id={DIRECTOR_REVIEW_WORKSPACE_ID} className="fx-drws">
        <p className="fx-drws__guide">Loading calibration review...</p>
      </section>
    );
  }

  return (
    <section id={DIRECTOR_REVIEW_WORKSPACE_ID} className="fx-drws">
      <ContentFamixaDirectorReviewHeader
        review={review}
        onExit={visualCalibrationWorkspacePath(review?.packId || packId)}
        onOpenSummary={openSummary}
      />
      <ContentFamixaVisualModeBadge compact />
      <p className="fx-drws__memory">{DIRECTOR_REVIEW_MEMORY_HINT}</p>
      {memoryReset ? <p className="fx-drws__memory is-reset">{DIRECTOR_REVIEW_MEMORY_RESET}</p> : null}
      {error ? <p className="fx-drws__error">{error}</p> : null}
      <ContentFamixaDirectorImageStatus
        loaded={loadedImages}
        loadDone={imageLoadDone}
      />
      <ContentFamixaDirectorWorkflowBanner
        review={review}
        busy={busy}
        onReviewNext={() => {
          const id = directorReviewNextPendingSubject(subjects, selectedSubjectId);
          setPane('review');
          if (id) requestSubject(id);
        }}
        onReviewFailed={() => {
          const id = directorReviewNextDecisionSubject(subjects, selectedSubjectId, ['FAIL']);
          setPane('review');
          if (id) requestSubject(id);
        }}
        onReviewRequired={() => {
          const id = directorReviewNextDecisionSubject(subjects, selectedSubjectId, ['REVIEW_REQUIRED']);
          setPane('review');
          if (id) requestSubject(id);
        }}
        onMark={() => setConfirmMark(true)}
      />

      {pane === 'complete' ? (
        <ContentFamixaDirectorPackSummaryView
          review={review}
          busy={busy}
          packGates={packGates}
          onSelect={(id) => {
            setPane('review');
            requestSubject(id);
          }}
          onMark={() => setConfirmMark(true)}
          onBack={() => setPane('review')}
        />
      ) : pane === 'summary' ? (
        <ContentFamixaDirectorPackSummaryView
          review={review}
          busy={busy}
          packGates={packGates}
          onSelect={(id) => {
            setPane('review');
            requestSubject(id);
          }}
          onMark={() => setConfirmMark(true)}
          onBack={() => setPane('review')}
        />
      ) : (
      <div className="fx-drws__layout">
        <ContentFamixaDirectorSubjectNavigator
          subjects={subjects}
          selectedSubjectId={selectedSubjectId}
          thumbs={images}
          onSelect={requestSubject}
        />
        <Collapse
          ghost
          className="fx-director-tech"
          items={[{
            key: 'universe',
            label: `${DIRECTOR_REVIEW_ADVANCED_ENGINEERING} ▾ · ${DIRECTOR_REVIEW_ADVANCED}`,
            children: (
              <>
                <ContentFamixaDirectorPackOverview review={review} />
                <ContentFamixaDirectorUniverseOverview
                  subjects={subjects}
                  thumbs={images}
                  packGates={packGates}
                  readOnly={readOnly}
                  onChange={(gate, value) => persistPackGates({ ...packGates, [gate]: value })}
                  onOpen={requestSubject}
                />
              </>
            ),
          }]}
        />
        <div className="fx-drws__main">
          {selected ? (
            <>
              {selected ? (
                <ContentFamixaDirectorStickyStrip
                  label={selected.label}
                  index={(selectedIndex >= 0 ? selectedIndex : 0) + 1}
                  total={subjects.length || 6}
                  imagesReady={imagesReady}
                  gatesCompleted={gatesCompleted}
                  decision={draft.decision === 'PENDING' ? 'PENDING' : draft.decision}
                  busy={busy}
                  disabled={readOnly}
                  onSave={() => saveReview()}
                />
              ) : null}
              <header className="fx-drws__subject-head">
                <div>
                  <p className="fx-drws__kicker">CHARACTER REVIEW</p>
                  <p className="fx-drws__kicker">
                    CURRENT CHARACTER
                    {' · '}
                    Character {(selectedIndex >= 0 ? selectedIndex : 0) + 1} / {subjects.length || 6}
                  </p>
                  <p className="fx-drws__kicker">
                    SUBJECT {String((selectedIndex >= 0 ? selectedIndex : 0) + 1).padStart(2, '0')} / {String(subjects.length || 6).padStart(2, '0')}
                  </p>
                  <h2>{selected.label} REVIEW</h2>
                  <p>
                    {directorReviewSubjectRoleLine(selected)}
                    {' · '}
                    {selected.subjectId}
                    {' · '}
                    {selected.subjectType}
                    {' · Age target '}
                    {selected.targetAppearanceAgeMin}–{selected.targetAppearanceAgeMax}
                  </p>
                  <p className="fx-drws__checklist">
                    {imagesReady} / 4 images available
                    {' · '}
                    {gatesCompleted} / 10 visual gates completed
                    {' · '}
                    Decision: {draft.decision}
                    {' · '}
                    Subject Review: {subjectStatus}
                    {' · '}
                    {directorReviewSubjectChipVi(draft.decision)}
                  </p>
                </div>
                <div className="fx-drws__split-status">
                  <p>Status: {subjectStatus === 'NOT REVIEWED' ? 'PENDING REVIEW' : subjectStatus}</p>
                  <p>Identity anchor {images[`${selected.subjectId}/FRONT`] ? 'READY' : 'PENDING'}</p>
                  <p>Subject Review: {subjectStatus}</p>
                  <p>Decision {directorReviewDecisionVi(draft.decision)}</p>
                </div>
              </header>

              <p className="fx-drws__compare">IMAGE COMPARISON · Compare against FRONT Identity Anchor</p>
              <p className="fx-drws__kicker">CALIBRATION VIEWS</p>
              <div className="fx-drws__compare-actions">
                <Button onClick={() => setCompareOpen(true)}>{DIRECTOR_REVIEW_OPEN_COMPARISON}</Button>
              </div>
              <div className="fx-drws__views">
                {CALIBRATION_VIEWS.map((view) => {
                  const slot = selected.views.find((v) => v.view === view);
                  const src = images[`${selected.subjectId}/${view}`];
                  const packSlot = directorReviewPackSlotPresent(pack, selected.subjectId, view);
                  const missing = !src && imageLoadDone;
                  const frontMissing = view === 'FRONT' && missing;
                  const badge = src ? 'PASS' : directorReviewViewBadge(slot);
                  return (
                    <button
                      key={view}
                      type="button"
                      className={`fx-drws__view${view === 'FRONT' ? ' is-anchor' : ''}`}
                      onClick={() => setLightbox(view)}
                    >
                      {src ? (
                        <img src={src} alt={`${selected.label} ${view}`} />
                      ) : (
                        <div className="fx-clib__ph">
                          {missing ? 'IMAGE NOT AVAILABLE' : ''}
                          {missing ? <span className="fx-drws__sr">Image unavailable</span> : null}
                        </div>
                      )}
                      <strong>{calibrationViewCompositeLabel(view)}</strong>
                      <em>{view}</em>
                      <p className={view === 'FRONT' ? 'fx-cal__anchor' : 'fx-cal__conditioned'}>
                        {view === 'FRONT' ? 'IDENTITY ANCHOR' : 'IDENTITY CONDITIONED'}
                      </p>
                      {view !== 'FRONT' ? (
                        <p className="fx-cal__conditioned">CONDITIONED BY FRONT</p>
                      ) : (
                        <p className="fx-cal__anchor">IDENTITY ANCHOR</p>
                      )}
                      <span className={`fx-drws__badge ${src ? 'is-pass' : missing ? 'is-fail' : 'is-pending'}`}>
                        {src ? '✓ Loaded' : missing ? '⚠ Load problem' : badge}
                      </span>
                      {frontMissing ? <p className="fx-drws__missing">IDENTITY ANCHOR FAILED</p> : null}
                      {missing ? (
                        <p className="fx-drws__missing">
                          IMAGE NOT AVAILABLE
                          {packSlot ? ' · Artifact integrity/load failure' : ''}
                        </p>
                      ) : null}
                    </button>
                  );
                })}
              </div>

              <ContentFamixaDirectorGatePanel
                gates={draft.gates}
                technicalStatus={selected.technicalStatus}
                readOnly={readOnly}
                onChange={(gate, value) => setDraft((prev) => ({
                  ...prev,
                  gates: { ...prev.gates, [gate]: value },
                }))}
              />
              <ContentFamixaDirectorDecisionPanel
                decision={draft.decision}
                canPass={canPass}
                directorNote={draft.directorNote}
                failureReason={draft.failureReason}
                noteMissing={noteMissing}
                viewsComplete={imagesComplete || directorReviewViewsComplete(selected.views)}
                technicalStatus={selected.technicalStatus}
                gates={draft.gates}
                readOnly={readOnly}
                onDecision={(value) => setDraft((prev) => ({ ...prev, decision: value }))}
                onNote={(value) => setDraft((prev) => ({ ...prev, directorNote: value }))}
                onFailureReason={(value) => setDraft((prev) => ({ ...prev, failureReason: value }))}
              />

              {savedFlash ? (
                <p className="fx-drws__saved">
                  ✓ Review saved
                  {savedSubjectLabel ? ` · Updated: ${savedSubjectLabel} · ${draft.decision}` : ''}
                  {draft.decision === 'PASS' && nextPending
                    ? ` · ${6 - (review?.subjectPass ?? 0)} / 6 characters remaining.`
                    : ''}
                  {draft.decision === 'PASS' && !nextPending ? ` · ${DIRECTOR_REVIEW_COMPLETE_TITLE}` : ''}
                </p>
              ) : null}
              {savedFlash ? (
                <ContentFamixaDirectorNextStep
                  decision={draft.decision}
                  label={savedSubjectLabel || selected.label}
                  approved={review?.subjectPass ?? 0}
                  nextLabel={nextPending?.label}
                  note={draft.directorNote}
                  onNext={() => nextPending && requestSubject(nextPending.subjectId)}
                  onAgain={() => {
                    setSavedFlash(false);
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                  }}
                />
              ) : null}
              {savedFlash && nextPending && draft.decision === 'PASS' ? (
                <p className="fx-drws__next-hint">
                  ✓ {savedSubjectLabel} approved. Next: {nextPending.label}
                </p>
              ) : null}
              {error ? <p className="fx-drws__error">{error}</p> : null}
              <div className="fx-drws__savebar">
                {primaryAction === 'MARK_PASS' ? (
                  <Button type="primary" size="large" loading={busy} onClick={() => setConfirmMark(true)}>
                    {DIRECTOR_REVIEW_MARK_PASS_VI_BTN}
                  </Button>
                ) : primaryAction === 'NEXT' ? (
                  <Button type="primary" size="large" onClick={() => nextPending && requestSubject(nextPending.subjectId)}>
                    {DIRECTOR_REVIEW_NEXT_VI_CTA}
                  </Button>
                ) : primaryAction === 'FOUNDATION' ? (
                  <p>{DIRECTOR_REVIEW_DONE_VI}</p>
                ) : (
                  <Button type="primary" size="large" loading={busy} disabled={readOnly} onClick={() => saveReview()}>
                    {DIRECTOR_REVIEW_SAVE_VI}
                  </Button>
                )}
                <div className="fx-drws__savebar-more">
                <span className="fx-drws__sr">{DIRECTOR_REVIEW_SAVE_LABEL}</span>
                <Button
                  disabled={!prevSubject || readOnly}
                  onClick={() => prevSubject && requestSubject(prevSubject.subjectId)}
                >
                  ← Previous Character
                </Button>
                <Button
                  disabled={!prevSubject || readOnly}
                  onClick={() => prevSubject && requestSubject(prevSubject.subjectId)}
                >
                  ← Previous Subject
                </Button>
                <Button loading={busy} disabled={!saveReady || readOnly} onClick={() => saveReview()}>
                  {DIRECTOR_REVIEW_SAVE_LABEL}
                </Button>
                {nextPending ? (
                  <Button
                    disabled={readOnly}
                    onClick={() => requestSubject(nextPending.subjectId)}
                  >
                    {DIRECTOR_REVIEW_NEXT_CHARACTER}
                  </Button>
                ) : (
                  <Button onClick={() => setPane('complete')}>
                    {DIRECTOR_REVIEW_GO_PACK}
                  </Button>
                )}
                {directorReviewNextPendingSubject(subjects, selectedSubjectId) ? (
                  <Button loading={busy} disabled={!saveReady || readOnly} onClick={saveAndNext}>
                    {DIRECTOR_REVIEW_SAVE_NEXT_LABEL}
                  </Button>
                ) : (
                  <Button onClick={openSummary}>
                    {DIRECTOR_REVIEW_PACK_SUMMARY_LABEL}
                  </Button>
                )}
                {canPass ? (
                  <span className="fx-desk__note">{DIRECTOR_REVIEW_PASS_SUBJECT}</span>
                ) : null}
                <Button disabled={!nextSubject || readOnly} onClick={() => nextSubject && requestSubject(nextSubject.subjectId)}>
                  Next Subject →
                </Button>
                </div>
              </div>
              {!uiCanMark ? (
                <div className="fx-drws__locked">
                  <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL} [DISABLED]</p>
                  <p>Reasons:</p>
                  <ul>
                    {(markReasons.length ? markReasons : [markBlocked]).map((reason) => (
                      <li key={reason}>• {reason}</li>
                    ))}
                  </ul>
                </div>
              ) : null}
              <ContentFamixaDirectorTechnicalDetails review={review} subject={selected} />
              <ContentFamixaDirectorHistoricalPanel count={review?.historical?.length ?? 0} />
            </>
          ) : (
            <p className="fx-desk__note">Chọn một subject để review.</p>
          )}
        </div>
      </div>
      )}

      <Modal
        title="Large comparison"
        open={compareOpen}
        footer={null}
        width={1180}
        onCancel={() => setCompareOpen(false)}
      >
        {selected ? (
          <div className="fx-drws__compare-modal">
            <p>IMAGE COMPARISON · {selected.label}</p>
            <div className="fx-drws__views is-large">
              {CALIBRATION_VIEWS.map((view) => {
                const src = images[`${selected.subjectId}/${view}`];
                return (
                  <figure key={view} className={`fx-drws__view${view === 'FRONT' ? ' is-anchor' : ''}`}>
                    {src ? <img src={src} alt={`${selected.label} ${view}`} /> : <div className="fx-clib__ph">IMAGE NOT AVAILABLE</div>}
                    <figcaption>
                      {calibrationViewCompositeLabel(view)}
                      {' · '}
                      {view === 'FRONT' ? 'IDENTITY ANCHOR' : 'IDENTITY CONDITIONED'}
                    </figcaption>
                  </figure>
                );
              })}
            </div>
            <Button onClick={() => setCompareOpen(false)}>Close</Button>
          </div>
        ) : null}
      </Modal>

      <Modal
        title="Image viewer"
        open={Boolean(lightbox)}
        footer={null}
        width={960}
        onCancel={() => setLightbox(undefined)}
      >
        {lightbox && selected ? (
          <div className={`fx-drws__lightbox${zoom ? ' is-zoom' : ''}`}>
            <div className="fx-drws__lightbox-row">
              {lightbox !== 'FRONT' && images[`${selected.subjectId}/FRONT`] ? (
                <aside>
                  <p>FRONT · IDENTITY ANCHOR</p>
                  <img src={images[`${selected.subjectId}/FRONT`]} alt="FRONT identity anchor" />
                </aside>
              ) : null}
              {images[`${selected.subjectId}/${lightbox}`] ? (
                <img src={images[`${selected.subjectId}/${lightbox}`]} alt={`${selected.label} ${lightbox}`} />
              ) : (
                <p>Image unavailable</p>
              )}
            </div>
            <p>
              <b>{selected.label}</b>
              {' · '}
              {calibrationViewCompositeLabel(lightbox)}
              {' · '}
              {calibrationViewRoleLabel(lightbox)}
              {' · '}
              {lightbox === 'FRONT' ? 'IDENTITY ANCHOR' : 'IDENTITY CONDITIONED'}
            </p>
            <Space wrap>
              <Button onClick={() => stepView(-1)}>Previous</Button>
              <Button onClick={() => stepView(1)}>Next</Button>
              <Button onClick={() => setZoom((v) => !v)}>{zoom ? 'Fit to screen' : 'Zoom'}</Button>
              <Button onClick={() => setLightbox(undefined)}>Close</Button>
            </Space>
          </div>
        ) : null}
      </Modal>

      <Modal
        title={DIRECTOR_REVIEW_UNSAVED_MESSAGE}
        open={Boolean(pendingSubjectId)}
        onCancel={() => setPendingSubjectId(undefined)}
        footer={[
          <Button key="stay" type="primary" onClick={() => setPendingSubjectId(undefined)}>Stay</Button>,
          <Button
            key="leave"
            onClick={() => {
              if (pendingSubjectId === '__summary__') {
                setPendingSubjectId(undefined);
                setPane('summary');
                return;
              }
              if (pendingSubjectId) commitSubject(pendingSubjectId);
            }}
          >
            Leave
          </Button>,
        ]}
      >
        <p>{DIRECTOR_REVIEW_UNSAVED_LEAVE}</p>
      </Modal>

      <Modal
        title="Subject FAIL"
        open={confirmFail}
        onCancel={() => setConfirmFail(false)}
        footer={[
          <Button
            key="keep"
            type="primary"
            danger
            loading={busy}
            onClick={() => {
              setConfirmFail(false);
              saveReview({ ...draft, decision: 'FAIL' });
            }}
          >
            {DIRECTOR_REVIEW_KEEP_FAIL}
          </Button>,
        ]}
      >
        <p>Subject này đã FAIL.</p>
        <p>Review không tạo lại ảnh. Tạo lại thuộc Visual Calibration Generation — không chạy từ Director Review.</p>
      </Modal>

      <Modal
        title="Confirm Visual Calibration Pass"
        open={confirmMark}
        onCancel={() => setConfirmMark(false)}
        footer={[
          <Button key="cancel" onClick={() => setConfirmMark(false)}>Cancel</Button>,
          <Button key="ok" type="primary" loading={busy} disabled={!markEnabled} onClick={confirmVisualPass}>
            Confirm Visual Calibration Pass
          </Button>,
        ]}
      >
        <p>Mark this Visual Calibration as PASS?</p>
        <p>You are confirming that the current 24-image calibration pack is visually acceptable and can be used as the FAMIXA visual foundation.</p>
        <p>This action records the Director decision only. It does not regenerate images or mutate identity authority.</p>
        <p>This records the Director's approval of the current calibration set.</p>
        <p>It does not generate images, modify Master/DNA/PRP, lock character identity, or change authority.</p>
        <p>6/6 subjects passed. 24/24 images passed.</p>
        <p>This action will record the Director decision.</p>
        <p>This action does NOT generate images, lock characters, change Master, change DNA / PRP / CRP, promote VUA, or modify historical artifacts.</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_WARNING}</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL} remains a decision-recording action only.</p>
      </Modal>
    </section>
  );
}
