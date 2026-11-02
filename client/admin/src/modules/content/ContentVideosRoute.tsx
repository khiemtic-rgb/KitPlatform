import { useSearchParams } from 'react-router-dom';
import { ContentVideoPipelineHost } from './ContentVideoPipelineHost';
import { ContentVideosPage } from './ContentVideosPage';

export function ContentVideosRoute() {
  const [params] = useSearchParams();
  const buildId = (params.get('build') || '').trim();
  if (buildId) return <ContentVideoPipelineHost />;
  return <ContentVideosPage />;
}
