import { useEffect, useState } from 'react';
import {
  fetchIdentityConditionedCalibrationDirectorReview,
  type IdentityConditionedCalibrationPackReview,
} from '@/shared/api/content.api';
import { ContentFamixaDirectorReviewHandoff } from './ContentFamixaDirectorReviewHandoff';
import { Link } from 'react-router-dom';
import { Button } from 'antd';
import {
  DIRECTOR_REVIEW_FROM_STUDIO_LABEL,
  DIRECTOR_REVIEW_OPEN_LABEL,
  visualCalibrationWorkspacePath,
} from './kit-video-visual-calibration';

export function ContentFamixaCharacterCalibrationShortcut() {
  const [review, setReview] = useState<IdentityConditionedCalibrationPackReview>();

  useEffect(() => {
    void fetchIdentityConditionedCalibrationDirectorReview()
      .then(setReview)
      .catch(() => undefined);
  }, []);

  return (
    <div className="fx-cal-shortcut">
      <ContentFamixaDirectorReviewHandoff review={review} />
      <Link to={visualCalibrationWorkspacePath(review?.packId)}>
        <Button>Mở Visual Calibration</Button>
      </Link>
      <span className="fx-drws__sr">
        {DIRECTOR_REVIEW_FROM_STUDIO_LABEL}
        {' '}
        {DIRECTOR_REVIEW_OPEN_LABEL}
      </span>
    </div>
  );
}
