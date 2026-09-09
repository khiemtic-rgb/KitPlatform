import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Modal } from 'antd';
import {
  fetchIdentityConditionedCalibrationDirectorReview,
  markIdentityConditionedCalibrationVisualPass,
  type IdentityConditionedCalibrationPackReview,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { ContentFamixaDirectorReviewHandoff } from './ContentFamixaDirectorReviewHandoff';
import { ContentFamixaVisualCalibrationCard } from './ContentFamixaVisualCalibrationCard';
import {
  DIRECTOR_REVIEW_CONTINUE_FOUNDATION,
  DIRECTOR_REVIEW_MARK_PASS_LABEL,
  DIRECTOR_REVIEW_MARK_PASS_VI_BTN,
  DIRECTOR_REVIEW_MARK_PASS_WARNING,
  DIRECTOR_REVIEW_START,
  VISUAL_CALIBRATION_PACK_ID,
  directorReviewHandoffCta,
  directorReviewMarkPassEnabled,
  directorReviewPersistRunId,
  directorReviewWorkspacePath,
  visualCalibrationWorkspacePath,
} from './kit-video-visual-calibration';
import './content-famixa-studio.css';

export function ContentFamixaVisualCalibrationPage() {
  const { packId } = useParams();
  const id = packId || VISUAL_CALIBRATION_PACK_ID;
  const [review, setReview] = useState<IdentityConditionedCalibrationPackReview>();
  const [busy, setBusy] = useState(false);
  const [confirmMark, setConfirmMark] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    void fetchIdentityConditionedCalibrationDirectorReview()
      .then(setReview)
      .catch(() => undefined);
  }, []);

  const canMark = directorReviewMarkPassEnabled(review);
  const runId = directorReviewPersistRunId(review, { packId: id, generationExecutionId: review?.calibrationRunId });
  const cta = directorReviewHandoffCta(review);

  const confirmVisualPass = () => {
    if (!runId || !canMark) return;
    setBusy(true);
    setConfirmMark(false);
    void markIdentityConditionedCalibrationVisualPass(runId)
      .then((workspace) => {
        if (workspace.gateCode && !workspace.visualPass) throw new Error(workspace.gateCode);
        setReview(workspace);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Chưa thể đánh VISUAL PASS.')))
      .finally(() => setBusy(false));
  };

  return (
    <div className="fx-drws-page" id="famixa-visual-calibration">
      <header className="fx-drws__bar">
        <div>
          <p className="fx-drws__brand">FAMIXA VISUAL CALIBRATION</p>
          <p className="fx-drws__kicker">VISUAL CALIBRATION</p>
          <h1>Visual Calibration</h1>
          <p>6 characters · 24 images</p>
        </div>
        <div className="fx-drws__head-actions">
          {review?.visualPass ? (
            <Link to="/content/videos">
              <Button type="primary" size="large">{DIRECTOR_REVIEW_CONTINUE_FOUNDATION}</Button>
            </Link>
          ) : canMark ? (
            <Button type="primary" size="large" loading={busy} onClick={() => setConfirmMark(true)}>
              {DIRECTOR_REVIEW_MARK_PASS_VI_BTN}
            </Button>
          ) : (
            <Link to={directorReviewWorkspacePath(id)}>
              <Button type="primary" size="large">{cta}</Button>
            </Link>
          )}
          <span className="fx-drws__sr">{DIRECTOR_REVIEW_START}</span>
        </div>
      </header>
      {error ? <p className="fx-drws__error">{error}</p> : null}
      <ContentFamixaDirectorReviewHandoff
        review={review}
        busy={busy}
        onMark={() => setConfirmMark(true)}
      />
      <ContentFamixaVisualCalibrationCard markBusy={busy} onMark={() => setConfirmMark(true)} />
      <p className="fx-drws__sr">{visualCalibrationWorkspacePath(id)}</p>
      <Modal
        title="Confirm Visual Calibration Pass"
        open={confirmMark}
        onCancel={() => setConfirmMark(false)}
        footer={[
          <Button key="cancel" onClick={() => setConfirmMark(false)}>Cancel</Button>,
          <Button key="ok" type="primary" loading={busy} disabled={!canMark} onClick={confirmVisualPass}>
            Confirm Visual Calibration Pass
          </Button>,
        ]}
      >
        <p>Mark this Visual Calibration as PASS?</p>
        <p>This action records the Director decision only. It does NOT generate images.</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_WARNING}</p>
        <p>{DIRECTOR_REVIEW_MARK_PASS_LABEL}</p>
      </Modal>
    </div>
  );
}
