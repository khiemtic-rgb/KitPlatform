import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, App, Button, Card, Input, Modal, Select, Space, Typography } from 'antd';
import {
  fetchContentBrands,
  fetchContentSeriesBuild,
  fetchFilmTypes,
  fetchSeriesBuildsForBrand,
  putContentSeriesBuild,
  type ContentSeriesBuildSummary,
  type FilmTypeRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { suggestNextEpisodeCode } from './content-famixa-series-desk';
import { useContentBrand } from './content-brand-context';
import { StudioPerformancePanel } from './StudioPerformancePanel';
import { ContentImportedVideosCard } from './ContentImportedVideosCard';
import { createPipelineChoice, seriesCodeForPipeline, videoWorkflowDescription, videoWorkflowLabel } from './video-pipeline';

type StudioFilm = ContentSeriesBuildSummary & { line: string };
type StudioBrand = { code: string; name: string };

function spokenLine(graph: Record<string, unknown> | undefined): string {
  const lines = graph?.lines;
  if (Array.isArray(lines)) {
    for (const row of lines) {
      if (row && typeof row === 'object' && 'text' in row && typeof row.text === 'string' && row.text.trim()) {
        return row.text.trim();
      }
    }
  }
  const episode = graph?.episode;
  if (episode && typeof episode === 'object' && 'shots' in episode && Array.isArray(episode.shots)) {
    const shot = episode.shots.find((row) => row && typeof row === 'object' && 'story' in row);
    if (shot && typeof shot === 'object' && 'story' in shot && typeof shot.story === 'string') return shot.story.trim();
  }
  return '';
}

function statusLabel(status: string) {
  if (status === 'draft') return 'Nháp';
  return status;
}

export function ContentStudioPage() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const workspace = useContentBrand();
  const [brand, setBrand] = useState<StudioBrand | null>(null);
  const [films, setFilms] = useState<StudioFilm[]>([]);
  const [ready, setReady] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pipelines, setPipelines] = useState<FilmTypeRow[]>([]);
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState('');
  const [pipelineId, setPipelineId] = useState<string>();
  const [creating, setCreating] = useState(false);
  const choice = createPipelineChoice(pipelines);

  useEffect(() => {
    if (!workspace.brandId) {
      setBrand(null);
      setFilms([]);
      setPipelines([]);
      setLoadError(null);
      setReady(true);
      return;
    }
    let cancelled = false;
    setReady(false);
    setLoadError(null);
    void fetchContentBrands(true)
      .then(async (brands) => {
        const selected = brands.find((row) => row.id === workspace.brandId);
        if (!selected?.code) {
          if (!cancelled) {
            setBrand(null);
            setFilms([]);
            setPipelines([]);
            setLoadError('Không thấy mã thương hiệu để mở phim.');
          }
          return;
        }
        if (!cancelled) setBrand({ code: selected.code, name: selected.name });
        const brandId = workspace.brandId;
        if (!brandId) return;
        const [rows, pipelineRows] = await Promise.all([
          fetchSeriesBuildsForBrand(selected.code, brandId),
          fetchFilmTypes(brandId).catch(() => [] as FilmTypeRow[]),
        ]);
        if (!cancelled) setPipelines(pipelineRows);
        const detailed = await Promise.all(
          rows.map(async (row) => {
            try {
              const full = await fetchContentSeriesBuild(row.id);
              return { ...row, line: spokenLine(full.graph) };
            } catch {
              return { ...row, line: '' };
            }
          }),
        );
        if (!cancelled) setFilms(detailed);
      })
      .catch((error) => {
        if (!cancelled) {
          setFilms([]);
          setLoadError(apiErrorMessage(error, 'Không tải được danh sách phim.'));
        }
      })
      .finally(() => {
        if (!cancelled) setReady(true);
      });
    return () => {
      cancelled = true;
    };
  }, [workspace.brandId]);

  const createFilm = async () => {
    const name = title.trim();
    const pipeline = choice.active.find((row) => row.id === (choice.selected?.id || pipelineId));
    if (!brand || !workspace.brandId || !name || !pipeline) return;
    const seriesCode = seriesCodeForPipeline(pipeline.code);
    if (!seriesCode) {
      message.error('Mã pipeline không dùng được cho phim.');
      return;
    }
    setCreating(true);
    try {
      const episode = suggestNextEpisodeCode(films);
      const created = await putContentSeriesBuild({
        id: crypto.randomUUID(),
        seriesCode,
        brandId: workspace.brandId,
        filmTypeId: pipeline.id,
        graph: {
          schemaVersion: 9,
          roles: [],
          runs: {},
          lines: [],
          characters: [],
          scenes: [],
          shorts: [],
          stills: [],
          episode: {
            seriesCode,
            seriesTitle: (pipeline.seriesLabel || '').trim() || pipeline.name,
            episode,
            title: name,
            premise: (pipeline.purpose || '').trim(),
            shots: [],
          },
        },
      });
      setOpen(false);
      setTitle('');
      navigate(`/content/videos?build=${created.id}&brand=${workspace.brandId}`);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tạo được phim.'));
    } finally {
      setCreating(false);
    }
  };

  if (workspace.scope.mode !== 'one' || !workspace.brandId) {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        <Typography.Title level={4} style={{ margin: 0 }}>Video Studio</Typography.Title>
        <StudioPerformancePanel />
        <Alert type="info" showIcon message="Chọn một thương hiệu." />
      </Space>
    );
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          Video Studio
        </Typography.Title>
        <Button
          type="primary"
          disabled={!brand || choice.active.length === 0}
          onClick={() => {
            setPipelineId(choice.selected?.id);
            setOpen(true);
          }}
        >
          Tạo phim
        </Button>
      </div>
      <StudioPerformancePanel forBrand={brand?.code ?? ''} />
      <ContentImportedVideosCard brandId={workspace.brandId} />
      {ready && !loadError && choice.active.length === 0 ? (
        <Alert
          type="warning"
          showIcon
          message="Chưa có Video Pipeline"
          description={
            <span>
              Brand này chưa có pipeline đang bật. Hãy tạo pipeline trong{' '}
              <Link to={`/content/settings/film-types?brand=${workspace.brandId}`}>cài đặt Brand</Link>
              . Không mở workflow của brand khác.
            </span>
          }
        />
      ) : null}
      {!ready ? <Typography.Text type="secondary">Đang mở phim…</Typography.Text> : null}
      {loadError ? <Alert type="warning" showIcon message={loadError} /> : null}
      {ready && !loadError && films.length === 0 && choice.active.length > 0 ? (
        <Alert type="info" showIcon message="Chưa có phim. Bấm Tạo phim để bắt đầu." />
      ) : null}
      {films.map((row) => (
        <Card
          key={row.id}
          size="small"
          hoverable
          onClick={() => navigate(`/content/videos?build=${row.id}&brand=${workspace.brandId}`)}
        >
          <Space direction="vertical" size={4}>
            <Typography.Text strong>
              {row.episodeCode || 'Tập'} · {row.title || row.seriesCode}
            </Typography.Text>
            {row.line ? <Typography.Paragraph style={{ margin: 0 }}>{row.line}</Typography.Paragraph> : null}
            <Typography.Text type="secondary">
              {row.pipelineName || 'Chưa gắn pipeline'} · {statusLabel(row.status)} · {row.shotCount} shot
            </Typography.Text>
          </Space>
        </Card>
      ))}
      <Modal
        title="Tạo phim"
        open={open}
        okText="Tạo"
        cancelText="Hủy"
        confirmLoading={creating}
        okButtonProps={{ disabled: !title.trim() || !(choice.selected?.id || pipelineId) }}
        onCancel={() => {
          if (creating) return;
          setOpen(false);
        }}
        onOk={() => void createFilm()}
      >
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          {choice.active.length > 1 ? (
            <Select
              style={{ width: '100%' }}
              placeholder="Chọn Video Pipeline"
              value={pipelineId}
              onChange={setPipelineId}
              options={choice.active.map((row) => ({
                value: row.id,
                label: videoWorkflowLabel(row.workflowKey)
                  ? `${row.name} · ${videoWorkflowLabel(row.workflowKey)}`
                  : row.name,
              }))}
            />
          ) : (
            <Typography.Text type="secondary">
              Pipeline: {choice.selected?.name}
              {videoWorkflowLabel(choice.selected?.workflowKey)
                ? ` · ${videoWorkflowLabel(choice.selected?.workflowKey)}`
                : ''}
            </Typography.Text>
          )}
          {videoWorkflowDescription(
            choice.active.find((row) => row.id === (choice.selected?.id || pipelineId))?.workflowKey,
          ) ? (
            <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
              {videoWorkflowDescription(
                choice.active.find((row) => row.id === (choice.selected?.id || pipelineId))?.workflowKey,
              )}
            </Typography.Paragraph>
          ) : null}
          <Input
            autoFocus
            placeholder="Tên phim"
            value={title}
            onChange={(event) => setTitle(event.target.value)}
            onPressEnter={() => {
              if (title.trim() && (choice.selected?.id || pipelineId)) void createFilm();
            }}
          />
        </Space>
      </Modal>
    </Space>
  );
}
