import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Card, Space, Steps, Tag, Typography } from 'antd';
import { fetchContentBrands, putContentSeriesBuild } from '@/shared/api/content.api';
import { emptyPilot, slimPilotForServer, type SeriesPilotState } from './content-famixa-series';
import { ContentNovixaStages } from './ContentNovixaStages';
import { NovixaExportStep } from './NovixaExportStep';
import { NovixaScriptStep } from './NovixaScriptStep';
import { NovixaShortsPanel } from './NovixaShortsPanel';
import {
  describeClientError,
  FILM_SERIES_DESK_STEPS,
  novixaBrandProfile,
  novixaUnits,
  spokenLine,
  type NovixaBrandProfile,
} from './film-production-orchestrator';
import { filmScriptSource, novixaFilmSummary, type FilmStepKey } from './novixa-film-progress';
import { scriptScenes, type NovixaShortEdits, type NovixaShortPlan } from './novixa-shorts';

const STEP_ORDER: FilmStepKey[] = ['script', 'pictures', 'video', 'shorts', 'publish'];

function stateFromBuild(graph: Record<string, unknown> | null | undefined, buildId: string, seriesCode: string): SeriesPilotState {
  const base = emptyPilot();
  const raw = graph && typeof graph === 'object' ? graph : {};
  const episodeRaw = raw.episode && typeof raw.episode === 'object' ? raw.episode as SeriesPilotState['episode'] : undefined;
  const episode = episodeRaw
    ? { ...episodeRaw, seriesCode: episodeRaw.seriesCode || seriesCode }
    : base.episode
      ? { ...base.episode, seriesCode }
      : undefined;
  return {
    ...base,
    ...(raw as Partial<SeriesPilotState>),
    buildId,
    episode,
  };
}

/**
 * Trang phim luồng Novixa (ảnh từng câu + giọng ElevenLabs + video), theo 5 bước:
 * Kịch bản → Hình → Giọng & video → Video ngắn → Xuất & đăng. Mỗi bước một việc chính.
 */
export function ContentNovixaLocalStudio({
  buildId,
  seriesCode,
  episodeCode,
  title,
  steps,
  graph,
  brandId,
}: {
  buildId: string;
  seriesCode: string;
  episodeCode: string;
  title?: string | null;
  steps?: string[] | null;
  graph?: Record<string, unknown> | null;
  brandId: string;
}) {
  const [state, setState] = useState(() => stateFromBuild(graph, buildId, seriesCode));
  const [saveError, setSaveError] = useState<string | null>(null);
  const [brand, setBrand] = useState<NovixaBrandProfile | null>(null);
  const [brandCode, setBrandCode] = useState('');
  const summary = novixaFilmSummary(state);
  const [step, setStep] = useState<FilmStepKey>(() => novixaFilmSummary(stateFromBuild(graph, buildId, seriesCode)).next.step);
  const topRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!brandId) {
      setBrand(null);
      return undefined;
    }
    let cancelled = false;
    void fetchContentBrands().then((rows) => {
      if (cancelled) return;
      const row = rows.find((item) => item.id === brandId);
      setBrand(novixaBrandProfile(row));
      setBrandCode(row?.code || '');
    }).catch(() => {
      if (!cancelled) setBrand(null);
    });
    return () => {
      cancelled = true;
    };
  }, [brandId]);
  const filmSteps = steps?.length ? steps : [...FILM_SERIES_DESK_STEPS];

  const commit = (next: SeriesPilotState) => {
    const saved = { ...next, buildId };
    setState(saved);
    setSaveError(null);
    void putContentSeriesBuild({
      id: buildId,
      seriesCode,
      graph: slimPilotForServer(saved) as unknown as Record<string, unknown>,
    }).catch((error) => {
      const detail = describeClientError(error);
      setSaveError(`Không lưu được tiến độ phim.${detail ? ` (${detail})` : ''}`);
    });
  };

  const go = (key: FilmStepKey) => {
    setStep(key);
    topRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const film = state.filmOrchestration;
  const scriptText = film?.script || state.scriptDraft || state.packDraft || '';
  const scriptLines = novixaUnits(scriptText).map((unit) => unit.text);
  const scenes = scriptScenes(scriptText.split(/\r?\n/), spokenLine);
  const tallStills = (film?.stillOrientation ?? 'tall') === 'tall' ? film?.stills : film?.shelf?.orientation === 'tall' ? film.shelf.stills : undefined;
  const filmTitle = (title || '').trim() || filmScriptSource(state as Parameters<typeof filmScriptSource>[0])?.title || episodeCode;
  const locked = summary.scriptLocked && !summary.scriptChanged;
  const index = STEP_ORDER.indexOf(step);
  const source = filmScriptSource(state as Parameters<typeof filmScriptSource>[0]);

  const savePlans = (plans: NovixaShortPlan[]) => {
    if (!state.filmOrchestration) return;
    commit({ ...state, filmOrchestration: { ...state.filmOrchestration, shortPlans: plans } });
  };

  const saveEdits = (edits: NovixaShortEdits) => {
    if (!state.filmOrchestration) return;
    commit({ ...state, filmOrchestration: { ...state.filmOrchestration, shortEdits: edits } });
  };

  const lockedNotice = !locked && step !== 'script' ? (
    <Alert
      type="warning"
      showIcon
      message={summary.scriptChanged ? 'Kịch bản đã sửa sau khi khóa.' : 'Kịch bản chưa khóa.'}
      action={<Button size="small" onClick={() => go('script')}>Về bước 1</Button>}
    />
  ) : null;

  return (
    <div data-stored-brand={brandId} ref={topRef} style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
        <Link to={`/content/studio?brand=${brandId}`}>← Video Studio</Link>
        <Space wrap align="center">
          {source?.code || episodeCode ? <Tag color="#0b1b3a" style={{ color: '#f3d38a', fontWeight: 700 }}>{source?.code || episodeCode}</Tag> : null}
          <Typography.Title level={3} style={{ margin: 0 }}>{filmTitle}</Typography.Title>
          {source ? <Tag color={source.approved ? 'green' : 'gold'}>{source.approved ? 'Kịch bản đã duyệt' : 'Kịch bản chờ duyệt'}</Tag> : null}
          <Typography.Text type="secondary">
            {brand?.name || ''}
            {brand?.voice?.voiceName ? ` · giọng ${brand.voice.voiceName}` : ''}
            {summary.lines ? ` · ${summary.scenes ? `${summary.scenes} cảnh · ` : ''}${summary.lines} câu` : ''}
          </Typography.Text>
        </Space>
      </div>

      <Card size="small">
        <Steps
          type="navigation"
          size="small"
          current={index}
          onChange={(value) => go(STEP_ORDER[value])}
          items={summary.steps.map((item) => ({
            title: item.label,
            description: item.sub,
            status: item.status === 'done' ? 'finish' : item.status === 'doing' ? 'process' : 'wait',
          }))}
        />
        <div style={{ marginTop: 8, display: 'flex', flexWrap: 'wrap', gap: 8, alignItems: 'center' }}>
          <Typography.Text type="secondary">Việc tiếp theo:</Typography.Text>
          <Typography.Text strong>{summary.next.text}</Typography.Text>
          {summary.next.step !== step ? <Button size="small" type="link" onClick={() => go(summary.next.step)}>Đi tới</Button> : null}
        </div>
      </Card>

      {saveError ? <Alert type="error" showIcon message={saveError} /> : null}
      {lockedNotice}

      {step === 'script' ? (
        <Card>
          <NovixaScriptStep
            state={state}
            steps={filmSteps}
            seriesCode={seriesCode}
            episodeCode={episodeCode}
            brandCode={brandCode}
            brand={brand}
            onCommit={commit}
          />
        </Card>
      ) : null}

      {step === 'pictures' || step === 'video' ? (
        <Card>
          {locked ? (
            <ContentNovixaStages
              buildId={buildId}
              state={state}
              steps={filmSteps}
              onCommit={commit}
              brand={brand}
              section={step === 'pictures' ? 'pictures' : 'video'}
              scenes={scenes}
            />
          ) : (
            <Typography.Text type="secondary">Khóa kịch bản ở bước 1 trước khi tạo ảnh và dựng video.</Typography.Text>
          )}
        </Card>
      ) : null}

      {step === 'shorts' ? (
        summary.needsWide ? (
          <NovixaShortsPanel
            lines={scriptLines}
            scenes={scenes}
            plans={film?.shortPlans ?? []}
            onPlansChange={savePlans}
            edits={film?.shortEdits ?? {}}
            onEditsChange={saveEdits}
            onShortsMade={(count) => {
              if (state.filmOrchestration) commit({ ...state, filmOrchestration: { ...state.filmOrchestration, shortsDone: count } });
            }}
            buildId={buildId}
            videoLines={tallStills?.length ? tallStills.map((item) => item.text) : scriptLines}
            videoReady={summary.tall.video}
            filmTitle={filmTitle}
          />
        ) : (
          <Card>
            <Typography.Title level={4} style={{ margin: 0 }}>4. Video ngắn</Typography.Title>
            <Typography.Text type="secondary">Phim này đã là video ngắn (dưới 12 câu), không cần cắt thêm. Sang bước 5.</Typography.Text>
          </Card>
        )
      ) : null}

      {step === 'publish' ? (
        <Card>
          <NovixaExportStep
            buildId={buildId}
            brandId={brandId}
            brandCode={brandCode}
            scriptId={source?.id}
            filmTitle={filmTitle}
            scriptLines={scriptLines}
            state={state}
            needsWide={summary.needsWide}
            onCommit={commit}
          />
        </Card>
      ) : null}

      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
        <Button disabled={index <= 0} onClick={() => go(STEP_ORDER[Math.max(0, index - 1)])}>← Bước trước</Button>
        <Button disabled={index >= STEP_ORDER.length - 1} onClick={() => go(STEP_ORDER[Math.min(STEP_ORDER.length - 1, index + 1)])}>Bước sau →</Button>
      </div>
    </div>
  );
}
