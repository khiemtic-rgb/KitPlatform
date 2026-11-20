import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Space, Typography } from 'antd';
import { putContentSeriesBuild } from '@/shared/api/content.api';
import { emptyPilot, slimPilotForServer, type SeriesPilotState } from './content-famixa-series';
import { ContentFilmProductionDesk } from './ContentFilmProductionDesk';
import { videoWorkflowDescription, videoWorkflowLabel } from './video-pipeline';

function readGraph(graph?: Record<string, unknown> | null): SeriesPilotState {
  if (!graph || Array.isArray(graph)) return emptyPilot();
  const row = graph as SeriesPilotState;
  return { ...emptyPilot(), ...row, runs: row.runs ?? {}, roles: row.roles ?? [] };
}

export function ContentPipelineBoard({
  workspace,
  pipelineName,
  purpose,
  seriesLine,
  aspect,
  steps,
  filmTitle,
  settingsBrandId,
  buildId,
  seriesCode,
  episodeCode,
  graph,
}: {
  workspace: 'short_factory' | 'guided' | 'unconfigured';
  pipelineName?: string | null;
  purpose?: string | null;
  seriesLine?: string | null;
  aspect?: string | null;
  steps?: string[] | null;
  filmTitle?: string | null;
  settingsBrandId?: string | null;
  buildId?: string | null;
  seriesCode?: string | null;
  episodeCode?: string | null;
  graph?: Record<string, unknown> | null;
}) {
  const [pilot, setPilot] = useState<SeriesPilotState>(() => readGraph(graph));
  const [saveError, setSaveError] = useState<string | null>(null);
  if (workspace === 'unconfigured') {
    const settings = settingsBrandId
      ? `/content/settings/film-types?brand=${settingsBrandId}`
      : '/content/settings/film-types';
    return (
      <Alert
        type="warning"
        showIcon
        message="Phim này chưa gắn Video Pipeline."
        description={
          <span>
            Không mở form dựng của pipeline khác. Hãy cấu hình pipeline trong{' '}
            <Link to={settings}>cài đặt Brand</Link>, rồi tạo phim mới.
          </span>
        }
      />
    );
  }

  const workflowKey = workspace === 'short_factory' ? 'factory_short' : 'guided_production';
  const title = videoWorkflowLabel(workflowKey);
  const workflowDescription = videoWorkflowDescription(workflowKey);
  const commit = (next: SeriesPilotState) => {
    setPilot(next);
    if (!buildId || !(seriesCode || '').trim()) return;
    const slim = slimPilotForServer(next) as unknown as Record<string, unknown>;
    void putContentSeriesBuild({ id: buildId, seriesCode: seriesCode || undefined, graph: slim }).catch(() => {
      setSaveError('Không lưu được tiến độ phim.');
    });
  };

  return (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
      <div>
        <Typography.Text type="secondary">{title}</Typography.Text>
        {workflowDescription ? (
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {workflowDescription}
          </Typography.Paragraph>
        ) : null}
        <Typography.Title level={4} style={{ margin: 0 }}>
          {filmTitle || pipelineName || 'Pipeline'}
        </Typography.Title>
        {filmTitle && pipelineName ? <Typography.Text type="secondary">{pipelineName}</Typography.Text> : null}
        {seriesLine ? <Typography.Paragraph style={{ marginBottom: 0 }}>{seriesLine}</Typography.Paragraph> : null}
        {purpose ? <Typography.Paragraph type="secondary">{purpose}</Typography.Paragraph> : null}
        {aspect ? <Typography.Text type="secondary">Khung hình {aspect}</Typography.Text> : null}
      </div>
      {saveError ? <Alert type="error" showIcon message={saveError} /> : null}
      <ContentFilmProductionDesk
        key={buildId || 'film'}
        state={pilot}
        steps={steps ?? []}
        episodeCode={episodeCode || 'EP01'}
        seriesCode={seriesCode || undefined}
        onCommit={commit}
      />
      <Link to={settingsBrandId ? `/content/studio?brand=${settingsBrandId}` : '/content/studio'}>← Video Studio</Link>
    </Space>
  );
}
