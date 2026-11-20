import { lazy, Suspense, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Alert, Spin } from 'antd';
import { fetchContentBrands, fetchContentSeriesBuild, type ContentSeriesBuild } from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { ContentNovixaLocalStudio } from './ContentNovixaLocalStudio';
import { ContentPipelineBoard } from './ContentPipelineBoard';
import { opensNovixaMotion, resolveVideoWorkspace, videoSeriesLine } from './video-pipeline';

const ContentFamixaSeriesTab = lazy(() =>
  import('./ContentFamixaSeriesTab').then((m) => ({ default: m.ContentFamixaSeriesTab })),
);

export function ContentVideoPipelineHost() {
  const [params] = useSearchParams();
  const buildId = (params.get('build') || '').trim();
  const [row, setRow] = useState<ContentSeriesBuild | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [brandCode, setBrandCode] = useState<string | null>(null);
  const [brandReady, setBrandReady] = useState(false);

  useEffect(() => {
    if (!buildId) {
      setRow(null);
      setError('Phim không có mã.');
      return;
    }
    let cancelled = false;
    setError(null);
    setRow(null);
    void fetchContentSeriesBuild(buildId)
      .then((item) => {
        if (!cancelled) setRow(item);
      })
      .catch((reason) => {
        if (!cancelled) setError(apiErrorMessage(reason, 'Không mở được phim.'));
      });
    return () => {
      cancelled = true;
    };
  }, [buildId]);

  useEffect(() => {
    if (!row) {
      setBrandCode(null);
      setBrandReady(false);
      return;
    }
    const brandId = (row.brandId || '').trim();
    if (!brandId) {
      setBrandCode(null);
      setBrandReady(true);
      return;
    }
    let cancelled = false;
    setBrandReady(false);
    void fetchContentBrands(true)
      .then((brands) => {
        if (cancelled) return;
        setBrandCode(brands.find((brand) => brand.id === brandId)?.code || '');
      })
      .catch(() => {
        if (!cancelled) setBrandCode('');
      })
      .finally(() => {
        if (!cancelled) setBrandReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [row]);

  if (!buildId || error) {
    return <Alert type="error" showIcon message={error || 'Phim không có mã.'} />;
  }
  if (!row || !brandReady) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin />
      </div>
    );
  }

  const workspace = resolveVideoWorkspace(row);
  const seriesLine = videoSeriesLine(row);
  const workflowKey =
    (row.workflowKey || '').trim() ||
    (workspace === 'short_factory' ? 'factory_short' : workspace === 'guided' ? 'guided_production' : '');
  if (
    opensNovixaMotion({
      filmTypeId: row.filmTypeId,
      workflowKey,
      brandCode,
      seriesCode: row.seriesCode,
    })
  ) {
    return (
      <ContentNovixaLocalStudio
        buildId={row.id}
        seriesCode={row.seriesCode}
        episodeCode={row.episodeCode}
        title={row.title}
        steps={row.steps}
        graph={row.graph}
        brandId={row.brandId || ''}
      />
    );
  }
  if (workspace === 'series_continuity' || workspace === 'short_factory' || workspace === 'guided') {
    return (
      <Suspense fallback={<Spin />}>
        <ContentFamixaSeriesTab
          catalogCode={row.seriesCode}
          seriesLine={seriesLine}
          pipelineSteps={row.steps}
          workflowKey={workflowKey || undefined}
          brandCode={brandCode}
        />
      </Suspense>
    );
  }
  return (
    <ContentPipelineBoard
      workspace={workspace}
      pipelineName={row.pipelineName}
      purpose={row.purpose}
      seriesLine={seriesLine}
      aspect={row.aspect}
      steps={row.steps}
      filmTitle={row.title}
      settingsBrandId={row.brandId}
      buildId={row.id}
      seriesCode={row.seriesCode}
      episodeCode={row.episodeCode}
      graph={row.graph}
    />
  );
}
