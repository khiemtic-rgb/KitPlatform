import { Link } from 'react-router-dom';
import { Button } from 'antd';
import type { IdentityConditionedCalibrationPackReview } from '@/shared/api/content.api';
import {
  DIRECTOR_REVIEW_AWAITING,
  DIRECTOR_REVIEW_DONE_VI,
  DIRECTOR_REVIEW_FROM_STUDIO_LABEL,
  DIRECTOR_REVIEW_IMAGES_READY,
  DIRECTOR_REVIEW_MARK_PASS_LABEL,
  DIRECTOR_REVIEW_MARK_PASS_VI_BTN,
  DIRECTOR_REVIEW_NEXT_VI_CTA,
  DIRECTOR_REVIEW_OPEN_LABEL,
  DIRECTOR_REVIEW_REVIEW_NEXT,
  DIRECTOR_REVIEW_START,
  directorReviewDecisionPassCount,
  directorReviewHandoffCta,
  directorReviewMarkPassEnabled,
  directorReviewSubjectChipVi,
  directorReviewSubjectsReviewed,
  directorReviewWorkflowPhase,
  directorReviewWorkspacePath,
  visualCalibrationWorkspacePath,
} from './kit-video-visual-calibration';

export function ContentFamixaDirectorReviewHandoff({
  review,
  compact,
  busy,
  onMark,
}: {
  review?: IdentityConditionedCalibrationPackReview;
  compact?: boolean;
  busy?: boolean;
  onMark?: () => void;
}) {
  const phase = directorReviewWorkflowPhase(review);
  const href = directorReviewWorkspacePath(review?.packId);
  const technical = review?.technicalIntegrityValid ?? 0;
  const images = review?.viewsFound ?? technical;
  const packPass = review?.subjectPass ?? 0;
  const visualPass = directorReviewDecisionPassCount(review?.subjects);
  const reviewed = directorReviewSubjectsReviewed(review?.subjects);
  const readyImages = (review?.technicalIntegrityValid ?? 0) >= 24 || (review?.viewsFound ?? 0) >= 24;
  const label = directorReviewHandoffCta(review);
  const canMark = directorReviewMarkPassEnabled(review);

  return (
    <aside className={`fx-dr-handoff${compact ? ' is-compact' : ''}`} aria-label="Director Review handoff">
      <p className="fx-drws__kicker">DIRECTOR REVIEW</p>
      {readyImages ? (
        <>
          <p className="fx-dr-handoff__flow">
            {DIRECTOR_REVIEW_IMAGES_READY}
            {' → '}
            {DIRECTOR_REVIEW_AWAITING}
          </p>
          <p>
            4 / 4 images created
            {' · '}
            Technical integrity: {technical >= 24 ? 'PASS' : 'PENDING'}
            {' · '}
            Director decision: {reviewed > 0 ? `${reviewed} / 6 recorded` : 'PENDING'}
          </p>
        </>
      ) : (
        <p>Visual Calibration · 6 Characters · 24 Images</p>
      )}
      <p>
        Director {visualPass} / 6 PASS
        {' · '}
        Pack {packPass} / 6 PASS
        {' · '}
        Images {images} / 24
      </p>
      {canMark ? (
        <p>
          6 nhân vật đã được Director duyệt. Bước tiếp theo trên màn này:
          {' '}
          ĐÁNH DẤU VISUAL CALIBRATION PASS.
        </p>
      ) : visualPass >= 6 && phase !== 'PASSED' ? (
        <p>
          6 nhân vật đã được Director duyệt. Nút bên dưới mở lại màn duyệt — chưa phải bước mới.
        </p>
      ) : null}
      <div className="fx-dr-handoff__chips">
        {(review?.subjects ?? []).map((subject) => (
          <Link key={subject.subjectId} to={directorReviewWorkspacePath(review?.packId, subject.subjectId)}>
            {subject.label} {directorReviewSubjectChipVi(subject.decision)}
          </Link>
        ))}
      </div>
      {phase === 'PASSED' ? (
        <p>{DIRECTOR_REVIEW_DONE_VI}</p>
      ) : canMark && onMark ? (
        <Button type="primary" size="large" loading={busy} onClick={onMark}>
          {DIRECTOR_REVIEW_MARK_PASS_VI_BTN}
        </Button>
      ) : canMark ? (
        <Link to={visualCalibrationWorkspacePath(review?.packId)}>
          <Button type="primary" size="large">{DIRECTOR_REVIEW_MARK_PASS_VI_BTN}</Button>
        </Link>
      ) : (
        <Link to={href}>
          <Button type="primary" size="large">{label}</Button>
        </Link>
      )}
      <p className="fx-desk__note">
        {DIRECTOR_REVIEW_FROM_STUDIO_LABEL}
        {' · '}
        {DIRECTOR_REVIEW_OPEN_LABEL}
        {' · '}
        {DIRECTOR_REVIEW_REVIEW_NEXT}
        {' · '}
        {DIRECTOR_REVIEW_MARK_PASS_LABEL}
        {' · '}
        {DIRECTOR_REVIEW_START}
        {' · '}
        {DIRECTOR_REVIEW_NEXT_VI_CTA}
        {' · '}
        {DIRECTOR_REVIEW_MARK_PASS_VI_BTN}
      </p>
    </aside>
  );
}
