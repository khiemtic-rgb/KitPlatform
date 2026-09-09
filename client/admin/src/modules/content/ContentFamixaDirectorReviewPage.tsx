import { useParams, useSearchParams } from 'react-router-dom';
import { ContentFamixaDirectorReviewWorkspace } from './ContentFamixaDirectorReviewWorkspace';
import { VISUAL_CALIBRATION_PACK_ID } from './kit-video-visual-calibration';
import './content-famixa-studio.css';

export function ContentFamixaDirectorReviewPage() {
  const { packId } = useParams();
  const [params] = useSearchParams();
  return (
    <div className="fx-drws-page">
      <ContentFamixaDirectorReviewWorkspace
        packId={packId || VISUAL_CALIBRATION_PACK_ID}
        initialSubjectId={params.get('subject') || undefined}
      />
    </div>
  );
}
