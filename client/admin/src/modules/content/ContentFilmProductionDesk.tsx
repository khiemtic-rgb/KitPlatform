import { useEffect, useRef, useState } from 'react';
import { Alert, App, Button, Card, Input, Space, Typography } from 'antd';
import type { SeriesPilotState } from './content-famixa-series';
import {
  analyzeFilmScript,
  applyConfirmedBreakdown,
  continueFilmProduction,
  filmProductionSnapshot,
  novixaVideoCurrent,
  novixaVideoGate,
  startFilmProduction,
  type FilmExecutor,
  type FilmScriptProposal,
} from './film-production-orchestrator';
import { VIDEO_PIPELINE_STEPS } from './video-pipeline';

const STEP_LABEL = new Map(VIDEO_PIPELINE_STEPS.map((step) => [step.value, step.label]));
const PAID = new Set(['voice', 'image', 'video']);

function stepLabel(step: string) {
  return STEP_LABEL.get(step as (typeof VIDEO_PIPELINE_STEPS)[number]['value']) || step;
}

export function ContentFilmProductionDesk({
  state,
  steps,
  episodeCode,
  seriesCode,
  onCommit,
  onNovixaProduce,
  executor = 'none',
  produceLabel,
  produceNote,
  incomingScript,
}: {
  state: SeriesPilotState;
  steps: string[];
  episodeCode: string;
  seriesCode?: string;
  onCommit: (next: SeriesPilotState) => void;
  onNovixaProduce?: (state: SeriesPilotState) => Promise<SeriesPilotState | void>;
  executor?: FilmExecutor;
  produceLabel?: string;
  produceNote?: string;
  /** Kịch bản nạp từ thư viện: đổi `nonce` mỗi lần nạp. */
  incomingScript?: { text: string; nonce: number } | null;
}) {
  const [script, setScript] = useState(state.scriptDraft || state.filmOrchestration?.script || state.packDraft || '');
  const scriptRef = useRef(script);
  const saveTimer = useRef<number | undefined>(undefined);
  const stateRef = useRef(state);
  const onCommitRef = useRef(onCommit);
  stateRef.current = state;
  onCommitRef.current = onCommit;
  const { modal, message } = App.useApp();
  const [producing, setProducing] = useState(false);
  const [proposal, setProposal] = useState<FilmScriptProposal | null>(null);
  const progress = filmProductionSnapshot(state, steps);
  const confirmed = progress.analysis === 'confirmed';
  const paidSteps = steps.filter((step) => PAID.has(step));

  const rememberScript = (value: string) => {
    scriptRef.current = value;
    const current = stateRef.current;
    if (value === (current.scriptDraft || '') && value === (current.filmOrchestration?.script || value)) return;
    onCommitRef.current({
      ...current,
      scriptDraft: value,
      filmOrchestration: current.filmOrchestration
        ? { ...current.filmOrchestration, script: value }
        : current.filmOrchestration,
    });
  };

  useEffect(() => () => {
    window.clearTimeout(saveTimer.current);
    rememberScript(scriptRef.current);
  }, []);

  const queueScriptSave = (value: string) => {
    scriptRef.current = value;
    setScript(value);
    window.clearTimeout(saveTimer.current);
    saveTimer.current = window.setTimeout(() => rememberScript(value), 400);
  };

  useEffect(() => {
    if (!incomingScript) return;
    window.clearTimeout(saveTimer.current);
    scriptRef.current = incomingScript.text;
    setScript(incomingScript.text);
    setProposal(null);
    rememberScript(incomingScript.text);
    // Chỉ chạy khi có lần nạp mới.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [incomingScript?.nonce]);

  const commitBreakdown = (replaceProduced: boolean) => {
    if (!proposal) return;
    const result = applyConfirmedBreakdown(state, proposal, {
      confirmed: true,
      replaceProduced,
      seriesCode,
    });
    if (!result.ok) return;
    setProposal(null);
    const planned = startFilmProduction({ ...result.state, scriptDraft: result.state.filmOrchestration?.script }, steps, executor);
    onCommit(planned.changed ? planned.state : { ...result.state, scriptDraft: result.state.filmOrchestration?.script });
  };

  const confirmBreakdown = () => {
    if (!proposal?.ok) return;
    const result = applyConfirmedBreakdown(state, proposal, { confirmed: true, seriesCode });
    if (!result.ok && result.reason === 'has_production') {
      modal.confirm({
        title: 'Shot đã có dữ liệu sản xuất',
        zIndex: 4000,
        content: `Các shot ${result.shotIds.join(', ')} đã có hình hoặc video. Xác nhận sẽ đưa chúng ra khỏi danh sách chiếu. Asset đã tạo được giữ trong hồ sơ phim.`,
        okText: 'Xác nhận thay cấu trúc',
        cancelText: 'Giữ nguyên',
        onOk: () => commitBreakdown(true),
      });
      return;
    }
    if (result.ok) {
      setProposal(null);
      const planned = startFilmProduction({ ...result.state, scriptDraft: result.state.filmOrchestration?.script }, steps, executor);
      onCommit(planned.changed ? planned.state : { ...result.state, scriptDraft: result.state.filmOrchestration?.script });
    }
  };

  const produce = () => {
    const scope = `${progress.shotCount} shot`;
    const cost = produceNote
      ? ` ${produceNote}`
      : executor === 'novixa_local'
      ? ' Hình vẽ từ từng câu. Giọng dùng ElevenLabs eleven_v3. Không gọi Gemini.'
      : paidSteps.length
      ? ` Pipeline có bước ${paidSteps.map(stepLabel).join(', ')}. Các bước này chưa có executor cấp phim nên không gọi provider, không ước tính thêm chi phí, và không được đánh dấu hoàn tất.`
      : ' Không có bước tạo ảnh hoặc video trong pipeline này.';
    message.info({ content: 'Xác nhận để bắt đầu dựng video.', key: 'novixa-produce' });
    modal.confirm({
      title: 'Sản xuất toàn bộ phim',
      zIndex: 4000,
      content: `Phạm vi: ${scope}. Hệ thống ghi nhận các bước đã xác nhận theo đúng pipeline của phim.${cost}`,
      okText: 'Bắt đầu điều phối',
      cancelText: 'Để sau',
      onOk: async () => {
        setProducing(true);
        message.loading({ content: 'Đang dựng video từ kịch bản.', key: 'novixa-produce', duration: 0 });
        try {
          if (executor === 'novixa_local' && onNovixaProduce) {
            const produced = await onNovixaProduce(state);
            if (produced) onCommit(produced);
            const motion = produced ? produced.filmOrchestration?.motion : undefined;
            const note = motion?.message || 'Đã ghi nhận lệnh dựng.';
            if (motion?.status === 'ready') message.success({ content: note, key: 'novixa-produce' });
            else message.warning({ content: note, key: 'novixa-produce', duration: 8 });
            return;
          }
          const started = startFilmProduction(state, steps, executor);
          if (started.changed) onCommit(started.state);
          message.success({ content: 'Đã ghi nhận các bước của phim.', key: 'novixa-produce' });
        } finally {
          setProducing(false);
        }
      },
    });
  };

  return (
    <Card size="small" title="Điều phối sản xuất phim" style={{ marginBottom: 16 }}>
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Nhập toàn bộ kịch bản. Phân tích chỉ đọc cấu trúc cảnh, lời thoại và hành động. Không tạo ảnh hoặc video.
        </Typography.Paragraph>
        <Input.TextArea
          value={script}
          rows={8}
          placeholder="Dán kịch bản. Dùng CẢNH 01, hoặc tên nhân vật kèm dấu hai chấm."
          onChange={(event) => queueScriptSave(event.target.value)}
          onBlur={() => rememberScript(scriptRef.current)}
        />
        {executor === 'novixa_local' && state.filmOrchestration?.motion?.message ? (
          <Alert type={state.filmOrchestration.motion.status === 'ready' ? 'success' : 'warning'} showIcon message={state.filmOrchestration.motion.message} />
        ) : null}
        <Space wrap>
          <Button
            onClick={() => setProposal(analyzeFilmScript(script, episodeCode))}
            disabled={!script.trim()}
          >
            Phân tích kịch bản
          </Button>
          <Button onClick={confirmBreakdown} disabled={!proposal?.ok}>
            Xác nhận phân cảnh
          </Button>
          <Button type="primary" loading={producing} onClick={produce} disabled={!confirmed || progress.shotCount === 0 || steps.length === 0}>
            {produceLabel || 'Sản xuất toàn bộ phim'}
          </Button>
          <Button
            onClick={() => {
              const live = stateRef.current;
              const next = continueFilmProduction(live, steps, executor);
              if (next.changed) onCommit(next.state);
              if (executor !== 'novixa_local') {
                message.info(next.changed ? 'Đã cập nhật các bước còn lại.' : 'Không có bước mới để chạy.');
                return;
              }
              const block = novixaVideoGate(live);
              if (block) message.warning(block);
              else if (novixaVideoCurrent(live)) message.success('Video đã có. Không tạo lượt giọng mới.');
              else message.info('Ảnh hoặc giọng đã đổi. Bấm Tạo video để dựng lại.');
            }}
            disabled={!confirmed || state.filmOrchestration?.tasks.length === 0}
          >
            Tiếp tục sản xuất
          </Button>
          <Button disabled={!progress.reviewExportReady}>Duyệt và xuất phim</Button>
        </Space>
        <Typography.Text>
          Phân tích: {confirmed ? 'Đã xác nhận' : proposal ? 'Chờ xác nhận' : 'Chưa phân tích'}
          {' · '}
          {progress.sceneCount} cảnh · {progress.shotCount} shot
          {' · '}
          Chưa xử lý {progress.pending} · Đang xử lý {progress.running} · Hoàn tất {progress.done} · Lỗi {progress.failed}
          {' · '}
          Hình {progress.imageDone}/{progress.shotCount} · Video {progress.videoDone}/{progress.shotCount}
        </Typography.Text>
        {steps.length === 0 ? (
          <Alert type="info" showIcon message="Pipeline chưa khai báo bước sản xuất." />
        ) : (
          <ol style={{ margin: 0, paddingLeft: 18 }}>
            {progress.steps.map((step) => (
              <li key={step.step}>
                {stepLabel(step.step)} —{' '}
                {step.status === 'done'
                  ? 'Đã xong'
                  : step.status === 'blocked'
                    ? 'Chưa đủ điều kiện'
                    : step.status === 'failed'
                      ? 'Lỗi'
                      : step.status === 'unavailable'
                        ? 'Chưa khả dụng'
                        : 'Chưa chạy'}
                {step.detail && step.status !== 'done' ? ` (${step.detail})` : ''}
              </li>
            ))}
          </ol>
        )}
        {!progress.reviewExportReady ? (
          <Typography.Text type="secondary">
            Duyệt và xuất phim chỉ mở khi mọi shot đã xong hai bước này. Bước chưa có executor không được tính là hoàn tất.
          </Typography.Text>
        ) : null}
        {proposal && !proposal.ok ? <Alert type="warning" showIcon message={proposal.error || 'Chưa phân tích được kịch bản.'} /> : null}
        {proposal?.warnings.map((warning) => (
          <Alert key={warning} type="warning" showIcon message={warning} />
        ))}
        {proposal?.ok ? (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            <Typography.Text strong>
              Xem trước {proposal.scenes.length} cảnh, {proposal.shots.length} shot. Sửa trước khi xác nhận.
            </Typography.Text>
            {proposal.scenes.map((scene) => (
              <div key={scene.id}>
                <Input
                  value={scene.title}
                  addonBefore={scene.id}
                  onChange={(event) => {
                    const title = event.target.value;
                    setProposal({
                      ...proposal,
                      scenes: proposal.scenes.map((row) => (row.id === scene.id ? { ...row, title } : row)),
                      shots: proposal.shots.map((shot) => (shot.sceneId === scene.id ? { ...shot, sceneTitle: title } : shot)),
                    });
                  }}
                />
                {proposal.shots
                  .filter((shot) => shot.sceneId === scene.id)
                  .map((shot) => (
                    <Input.TextArea
                      key={shot.id}
                      style={{ marginTop: 8 }}
                      value={shot.story}
                      autoSize
                      onChange={(event) => {
                        const story = event.target.value;
                        setProposal({
                          ...proposal,
                          shots: proposal.shots.map((row) => (row.id === shot.id ? { ...row, story } : row)),
                        });
                      }}
                    />
                  ))}
              </div>
            ))}
          </Space>
        ) : null}
      </Space>
    </Card>
  );
}
