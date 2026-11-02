import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, App, Button, Card, Input, Modal, Radio, Select, Space, Tabs, Tag, Typography } from 'antd';
import {
  fetchContentBrands,
  fetchContentSeriesBuild,
  fetchFilmTypes,
  fetchScriptLibrary,
  fetchScriptLibraryItem,
  fetchSeriesBuildsForBrand,
  putContentSeriesBuild,
  type ContentScriptLibraryItem,
  type ContentSeriesBuildSummary,
  type FilmTypeRow,
} from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { suggestNextEpisodeCode } from './content-famixa-series-desk';
import { useContentBrand } from './content-brand-context';
import { ContentPageHeader } from './ContentPageHeader';
import { ContentStorageCleanup } from './ContentStorageCleanup';

const STUDIO_DESC = 'Dựng phim từ kịch bản: hình, giọng, video, cắt video ngắn, rồi duyệt để đưa vào lịch đăng.';
import { StudioPerformancePanel } from './StudioPerformancePanel';
import { ContentImportedVideosCard } from './ContentImportedVideosCard';
import { filmScriptSource, novixaFilmSummary, type NovixaFilmSummary } from './novixa-film-progress';
import {
  NOVIXA_FLOW_BRANDS,
  createPipelineChoice,
  seriesCodeForPipeline,
  videoWorkflowDescription,
  videoWorkflowLabel,
} from './video-pipeline';

type StudioFilm = ContentSeriesBuildSummary & {
  line: string;
  graph?: Record<string, unknown> | null;
};
type StudioBrand = { code: string; name: string };

function spokenLine(graph: Record<string, unknown> | undefined | null): string {
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

const STEP_TONE: Record<string, { bg: string; border: string; fg: string }> = {
  done: { bg: '#effbe9', border: '#b8e3a6', fg: '#276b1f' },
  doing: { bg: '#eaf1fd', border: '#1658c4', fg: '#1658c4' },
  todo: { bg: '#f6f7f9', border: '#e3e6eb', fg: '#4a5565' },
};

function FilmProgressCard({ film, summary, onOpen }: { film: StudioFilm; summary: NovixaFilmSummary; onOpen: () => void }) {
  const source = filmScriptSource(film.graph as Parameters<typeof filmScriptSource>[0]);
  return (
    <Card size="small">
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 20, alignItems: 'center' }}>
        <div style={{ flex: '999 1 560px', minWidth: 0, display: 'flex', flexDirection: 'column', gap: 10 }}>
          <Space wrap align="center">
            <Tag color="#0b1b3a" style={{ color: '#f3d38a', fontWeight: 700 }}>{source?.code || film.episodeCode || 'Phim'}</Tag>
            <Typography.Text strong style={{ fontSize: 16 }}>{film.title || source?.title || film.seriesCode}</Typography.Text>
            {source ? <Tag color={source.approved ? 'green' : 'gold'}>{source.approved ? 'Kịch bản đã duyệt' : 'Kịch bản chờ duyệt'}</Tag> : <Tag>Kịch bản dán tay</Tag>}
          </Space>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {summary.lines ? `${summary.scenes ? `${summary.scenes} cảnh · ` : ''}${summary.lines} câu` : 'Chưa có kịch bản'}
            {summary.needsWide ? ' · bản dài (ngang + dọc + video ngắn)' : summary.lines ? ' · video ngắn (dọc)' : ''}
          </Typography.Text>
          <ol aria-label="Tiến độ" style={{ listStyle: 'none', margin: 0, padding: 0, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 8 }}>
            {summary.steps.map((step) => {
              const tone = STEP_TONE[step.status];
              return (
                <li key={step.key} style={{ borderRadius: 8, padding: '8px 10px', background: tone.bg, border: `1px solid ${tone.border}` }}>
                  <div style={{ fontSize: 12, fontWeight: 600, color: tone.fg }}>{step.status === 'done' ? '✓ ' : ''}{step.n}. {step.label}</div>
                  <div style={{ fontSize: 12, color: '#3a4553' }}>{step.sub}</div>
                </li>
              );
            })}
          </ol>
        </div>
        <div style={{ flex: '1 1 200px', display: 'flex', flexDirection: 'column', gap: 6, alignItems: 'flex-start' }}>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>Việc tiếp theo</Typography.Text>
          <Typography.Text strong>{summary.next.text}</Typography.Text>
          <Button type="primary" onClick={onOpen}>Mở phim</Button>
        </div>
      </div>
    </Card>
  );
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
  const [tab, setTab] = useState('films');
  const [library, setLibrary] = useState<ContentScriptLibraryItem[]>([]);
  const [libraryNote, setLibraryNote] = useState('');
  const [scriptPick, setScriptPick] = useState<string>('');
  const choice = createPipelineChoice(pipelines);
  const novixaFlow = NOVIXA_FLOW_BRANDS.includes((brand?.code || '').trim().toLowerCase());

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
              return { ...row, line: spokenLine(full.graph), graph: full.graph };
            } catch {
              return { ...row, line: '', graph: null };
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

  const openCreate = () => {
    setPipelineId(choice.selected?.id);
    setScriptPick('');
    setTitle('');
    setOpen(true);
    if (!novixaFlow || !brand?.code) return;
    void fetchScriptLibrary(brand.code)
      .then((data) => {
        setLibrary(data.items);
        setLibraryNote(data.message || '');
        const first = data.items.find((item) => item.approved) ?? data.items[0];
        if (first) {
          setScriptPick(first.id);
          setTitle(`${first.code} – ${first.title}`);
        }
      })
      .catch((error) => setLibraryNote(apiErrorMessage(error, 'Không đọc được thư viện kịch bản.')));
  };

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
      let scriptDraft = '';
      let source: { id: string; code: string; title: string; approved: boolean } | undefined;
      if (novixaFlow && scriptPick) {
        const item = await fetchScriptLibraryItem(brand.code, scriptPick);
        scriptDraft = (item.body || '').trim();
        source = { id: item.id, code: item.code, title: item.title, approved: item.approved };
      }
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
          ...(scriptDraft ? { scriptDraft, novixaScriptSource: source } : {}),
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
        <ContentPageHeader title="Video Studio" description={STUDIO_DESC} />
        <StudioPerformancePanel />
        <Alert type="info" showIcon message="Chọn một thương hiệu." />
      </Space>
    );
  }

  const openFilm = (row: StudioFilm) => navigate(`/content/videos?build=${row.id}&brand=${workspace.brandId}`);
  const selectedPipeline = choice.active.find((row) => row.id === (choice.selected?.id || pipelineId));

  const filmsTab = (
    <Space direction="vertical" size={12} style={{ width: '100%' }}>
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
      {novixaFlow ? (
        <Typography.Text type="secondary">
          Mỗi phim đi qua 5 bước: Kịch bản → Hình → Giọng &amp; video → Video ngắn → Xuất &amp; đăng. Thẻ phim cho biết đang ở bước nào và việc tiếp theo.
        </Typography.Text>
      ) : null}
      {!ready ? <Typography.Text type="secondary">Đang mở phim…</Typography.Text> : null}
      {loadError ? <Alert type="warning" showIcon message={loadError} /> : null}
      {ready && !loadError && films.length === 0 && choice.active.length > 0 ? (
        <Alert type="info" showIcon message="Chưa có phim. Bấm Tạo phim để bắt đầu." />
      ) : null}
      {films.map((row) =>
        novixaFlow ? (
          <FilmProgressCard key={row.id} film={row} summary={novixaFilmSummary(row.graph as Parameters<typeof novixaFilmSummary>[0])} onOpen={() => openFilm(row)} />
        ) : (
          <Card key={row.id} size="small" hoverable onClick={() => openFilm(row)}>
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
        ),
      )}
    </Space>
  );

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <ContentPageHeader
        title="Video Studio"
        description={STUDIO_DESC}
        extra={
          <Space>
            <ContentStorageCleanup />
            <Button type="primary" disabled={!brand || choice.active.length === 0} onClick={openCreate}>
              + Tạo phim
            </Button>
          </Space>
        }
      />
      <StudioPerformancePanel forBrand={brand?.code ?? ''} />
      <Tabs
        activeKey={novixaFlow ? 'films' : tab}
        onChange={setTab}
        items={[
          { key: 'films', label: `Phim (${films.length})`, children: filmsTab },
          // Kênh dựng trong Studio (Người Giữ Vốn…) đưa video vào lịch ở bước 5 của phim; tab nhập MP4 ngoài không dùng nên ẩn.
          ...(novixaFlow
            ? []
            : [{ key: 'imports', label: 'Video thành phẩm', children: <ContentImportedVideosCard brandId={workspace.brandId} /> }]),
        ]}
      />
      <Modal
        title={novixaFlow ? 'Tạo phim từ kịch bản' : 'Tạo phim'}
        open={open}
        okText="Tạo phim"
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
          {novixaFlow ? (
            <Space direction="vertical" size={6} style={{ width: '100%' }}>
              <Typography.Text strong>Kịch bản</Typography.Text>
              <Radio.Group
                value={scriptPick}
                onChange={(event) => {
                  const id = event.target.value as string;
                  setScriptPick(id);
                  const item = library.find((row) => row.id === id);
                  if (item) setTitle(`${item.code} – ${item.title}`);
                }}
                style={{ display: 'flex', flexDirection: 'column', gap: 8 }}
              >
                {library.map((item) => (
                  <Radio key={item.id} value={item.id}>
                    <Space wrap size={6}>
                      <strong>{item.code} · {item.title}</strong>
                      <Tag color={item.approved ? 'green' : 'gold'}>{item.status}</Tag>
                      <Typography.Text type="secondary">{item.scenes} cảnh{item.updated ? ` · cập nhật ${item.updated}` : ''}</Typography.Text>
                    </Space>
                  </Radio>
                ))}
                <Radio value="">Dán kịch bản tay sau khi tạo</Radio>
              </Radio.Group>
              {libraryNote ? <Typography.Text type="secondary">{libraryNote}</Typography.Text> : null}
              {library.find((row) => row.id === scriptPick && !row.approved) ? (
                <Alert type="warning" showIcon message="Kịch bản chưa duyệt: tạo phim để xem thử được, nhưng chưa nên dựng giọng và video." />
              ) : null}
            </Space>
          ) : null}
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
          ) : null}
          <Space direction="vertical" size={4} style={{ width: '100%' }}>
            <Typography.Text strong>Tên phim</Typography.Text>
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
          {selectedPipeline ? (
            <Typography.Text type="secondary">
              Luồng dựng: {selectedPipeline.name}
              {choice.active.length === 1 ? ' (tự chọn vì thương hiệu chỉ có một)' : ''}
              {!novixaFlow && videoWorkflowDescription(selectedPipeline.workflowKey) ? `. ${videoWorkflowDescription(selectedPipeline.workflowKey)}` : ''}
            </Typography.Text>
          ) : null}
        </Space>
      </Modal>
    </Space>
  );
}
