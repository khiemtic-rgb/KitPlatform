import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Card, Tag } from 'antd';
import {
  fetchIdentityConditionedCalibrationDirectorReview,
  type IdentityConditionedCalibrationPackReview,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  DIRECTOR_REVIEW_MARK_PASS_LABEL,
  DIRECTOR_REVIEW_MARK_PASS_VI,
  DIRECTOR_REVIEW_OPEN_LABEL,
  DIRECTOR_REVIEW_REVIEW_NEXT,
  DIRECTOR_REVIEW_WORKSPACE_ID,
  directorReviewOverallLabel,
  directorReviewWorkflowPhase,
  directorReviewWorkspacePath,
} from './kit-video-visual-calibration';

function statusColor(value?: string) {
  if (value === 'PASS' || value === 'VISUAL CALIBRATION PASS') return 'green';
  if (value === 'FAIL' || value === 'DIRECTOR REVIEW BLOCKED') return 'red';
  if (value === 'REVIEW_REQUIRED' || value === 'READY TO MARK PASS') return 'orange';
  return 'blue';
}

export function ContentFamixaIdentityConditionedCalibrationDirectorReviewCard() {
  const [review, setReview] = useState<IdentityConditionedCalibrationPackReview>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    void fetchIdentityConditionedCalibrationDirectorReview()
      .then((workspace) => {
        setReview(workspace);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được Director Review.')));
  }, []);

  const overall = directorReviewOverallLabel(review);
  const phase = directorReviewWorkflowPhase(review);
  const href = directorReviewWorkspacePath(review?.packId, review?.subjects[0]?.subjectId);
  const cta = phase === 'READY'
    ? DIRECTOR_REVIEW_MARK_PASS_LABEL
    : phase === 'IN_PROGRESS'
      ? DIRECTOR_REVIEW_REVIEW_NEXT
      : DIRECTOR_REVIEW_OPEN_LABEL;

  return (
    <Card
      id={DIRECTOR_REVIEW_WORKSPACE_ID}
      className="fx-look__card fx-pvs fx-pvs--director fx-cal-dir"
      size="small"
      title="Director Review · Identity-Conditioned Calibration"
    >
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <p className="fx-desk__note">
        24 ảnh đã tạo được duyệt ở workspace riêng — một subject một lần. Không tạo Master Revision. Không regenerate.
      </p>
      <div className="fx-cal__review-summary">
        <p>CALIBRATION RUN <b>{review?.calibrationRunId || '—'}</b></p>
        <p>Images <b>{review?.viewsFound ?? 0} / {review?.viewsExpected ?? 24}</b></p>
        <p>Director Review <b>{review?.subjectPass ?? 0} / {review?.subjectExpected ?? 6}</b></p>
        <p>SUBJECTS PASS <b>{review?.subjectPass ?? 0} / {review?.subjectExpected ?? 6}</b></p>
        <p>VISUAL PASS <b>{review?.visualPass ? 'TRUE' : 'FALSE'}</b></p>
        <p>PACK DECISION <b>{review?.packDecision || 'NOT_REVIEWED'}</b></p>
        <p>Foundation <b>{review?.foundationComplete ? 'COMPLETE' : 'NOT READY'}</b></p>
        <p>{review?.subjectPass ?? 0} / 6 CHARACTERS APPROVED</p>
      </div>
      <p>
        <Tag color={statusColor(overall)}>{overall}</Tag>
      </p>
      <Link to={href}>
        <Button type="primary">{cta}</Button>
      </Link>
      {phase === 'READY' ? <p className="fx-desk__note">{DIRECTOR_REVIEW_MARK_PASS_VI}</p> : null}
      <p className="fx-desk__note">{DIRECTOR_REVIEW_OPEN_LABEL}</p>
    </Card>
  );
}
