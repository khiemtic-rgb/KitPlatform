import { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, App, Button, Input, Space, Tag, Typography } from 'antd';
import type { SeriesPilotState } from './content-famixa-series';
import {
  analyzeFilmScript,
  applyConfirmedBreakdown,
  filmScriptHash,
  isShortOnlyLine,
  novixaUnits,
  spokenLine,
  startFilmProduction,
  type NovixaBrandProfile,
} from './film-production-orchestrator';
import { findDisclaimer, scriptScenes, shortOnlyLines } from './novixa-shorts';
import { lintScript, lintSummary, voiceOverhead } from './novixa-script-lint';
import { ScriptLibraryCard } from './ScriptLibraryCard';
import { filmScriptSource } from './novixa-film-progress';

const SYLLABLES_PER_SECOND = 3.3;

/** Bước 1: nạp hoặc dán kịch bản, kiểm tra, rồi khóa. Hình, giọng và video dựa trên bản đã khóa. */
export function NovixaScriptStep({
  state,
  steps,
  seriesCode,
  episodeCode,
  brandCode,
  brand,
  onCommit,
}: {
  state: SeriesPilotState;
  steps: string[];
  seriesCode: string;
  episodeCode: string;
  brandCode: string;
  brand: NovixaBrandProfile | null;
  onCommit: (next: SeriesPilotState) => void;
}) {
  const { modal, message } = App.useApp();
  const film = state.filmOrchestration;
  const [draft, setDraft] = useState(state.scriptDraft || film?.script || state.packDraft || '');
  const timer = useRef<number | undefined>(undefined);
  const lintAck = useRef(false);
  const stateRef = useRef(state);
  stateRef.current = state;
  const [error, setError] = useState('');
  // Phim mới chưa có phần điều phối: giữ nguồn kịch bản tới lúc khóa.
  const [pendingSource, setPendingSource] = useState<{ id: string; code: string; title: string; approved: boolean } | null>(null);

  const saveDraft = (value: string, extra?: Partial<SeriesPilotState>) => {
    window.clearTimeout(timer.current);
    const current = stateRef.current;
    if (value === (current.scriptDraft || '') && !extra) return;
    onCommit({ ...current, ...extra, scriptDraft: value });
  };

  useEffect(() => () => window.clearTimeout(timer.current), []);

  const lines = useMemo(() => novixaUnits(draft).map((unit) => unit.text), [draft]);
  const scenes = useMemo(() => scriptScenes(draft.split(/\r?\n/), spokenLine), [draft]);
  const shortOnly = useMemo(() => shortOnlyLines(draft.split(/\r?\n/), spokenLine, isShortOnlyLine), [draft]);
  const overhead = useMemo(() => voiceOverhead(lines, shortOnly), [lines, shortOnly]);
  const lint = useMemo(() => lintScript(lines, scenes, shortOnly), [lines, scenes, shortOnly]);
  const lintCount = lintSummary(lint);
  const syllables = lines.reduce((sum, line) => sum + line.split(/\s+/).filter(Boolean).length, 0);
  const minutes = syllables / SYLLABLES_PER_SECOND / 60;
  const disclaimer = findDisclaimer(lines);
  const avoidHits = (brand?.avoid ?? [])
    .map((term) => term.trim())
    .filter((term) => term.length > 1 && draft.toLowerCase().includes(term.toLowerCase()));
  const locked = film?.analysis === 'confirmed' && Boolean(film.script);
  const changed = locked && filmScriptHash(draft) !== (film?.scriptHash || filmScriptHash(film?.script || ''));
  const source = filmScriptSource(state as Parameters<typeof filmScriptSource>[0]) ?? pendingSource ?? undefined;

  const lock = () => {
    setError('');
    window.clearTimeout(timer.current);
    const proposal = analyzeFilmScript(draft, episodeCode);
    if (!proposal.ok) {
      setError(proposal.error || 'Không đọc được kịch bản.');
      return;
    }
    if (lintCount.errors > 0 && !lintAck.current) {
      modal.confirm({
        title: `Kịch bản còn ${lintCount.errors} lỗi vi phạm luật kênh`,
        zIndex: 4000,
        content: 'Nên sửa các lỗi (chữ đỏ ở mục Kiểm tra luật kênh) trước khi khóa. Khóa bây giờ thì hình, giọng và video sẽ dựng từ bản còn lỗi.',
        okText: 'Vẫn khóa',
        okButtonProps: { danger: true },
        cancelText: 'Quay lại sửa',
        onOk: () => {
          lintAck.current = true;
          lock();
          lintAck.current = false;
        },
      });
      return;
    }
    const finish = (replaceProduced: boolean) => {
      const current = stateRef.current;
      const result = applyConfirmedBreakdown({ ...current, scriptDraft: draft }, proposal, { confirmed: true, replaceProduced, seriesCode });
      if (!result.ok) {
        setError('Không khóa được kịch bản.');
        return;
      }
      // Giữ ảnh, giọng, kế hoạch video ngắn và thành phẩm đã có; chỉ thay phần kịch bản.
      const kept = current.filmOrchestration;
      const merged: SeriesPilotState = {
        ...result.state,
        scriptDraft: draft,
        filmOrchestration: result.state.filmOrchestration
          ? {
              ...(kept ?? {}),
              ...result.state.filmOrchestration,
              motion: result.state.filmOrchestration.motion ?? kept?.motion,
              scriptSource: kept?.scriptSource ?? pendingSource ?? filmScriptSource(current as Parameters<typeof filmScriptSource>[0]),
            }
          : result.state.filmOrchestration,
      };
      const planned = startFilmProduction(merged, steps, 'novixa_local');
      onCommit(planned.changed ? planned.state : merged);
      message.success('Đã khóa kịch bản. Sang bước Hình.');
    };
    const probe = applyConfirmedBreakdown({ ...state, scriptDraft: draft }, proposal, { confirmed: true, seriesCode });
    if (!probe.ok && probe.reason === 'has_production') {
      modal.confirm({
        title: 'Thay cấu trúc kịch bản?',
        zIndex: 4000,
        content: 'Một số câu cũ đã có dữ liệu dựng. Khóa bản mới sẽ thay cấu trúc; ảnh và video đã tạo vẫn giữ trong hồ sơ phim.',
        okText: 'Khóa bản mới',
        cancelText: 'Để sau',
        onOk: () => finish(true),
      });
      return;
    }
    finish(false);
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', gap: 12, alignItems: 'center' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>1. Kịch bản</Typography.Title>
          <Typography.Text type="secondary">
            {!locked
              ? 'Nạp từ thư viện hoặc dán kịch bản, kiểm tra rồi khóa.'
              : changed
                ? 'Kịch bản đã sửa sau khi khóa. Khóa lại để hình, giọng và video dùng bản mới.'
                : 'Đã khóa. Hình, giọng và video dựa trên bản này. Sửa trong ô bên dưới thì cần khóa lại.'}
          </Typography.Text>
        </div>
        <Space wrap>
          {locked && !changed ? <Tag color="green">Đã khóa</Tag> : null}
          <Button type="primary" disabled={!lines.length || (locked && !changed)} onClick={lock}>
            {changed ? 'Khóa lại kịch bản' : 'Khóa kịch bản'}
          </Button>
        </Space>
      </div>

      {brandCode ? (
        <ScriptLibraryCard
          brandCode={brandCode}
          currentScript={draft}
          hasPictures={Boolean(film?.stills?.length || film?.shelf?.stills?.length)}
          onLoad={(text, item) => {
            setDraft(text);
            const picked = { id: item.id, code: item.code, title: item.title, approved: item.approved };
            setPendingSource(picked);
            saveDraft(text, {
              filmOrchestration: film
                ? { ...film, scriptSource: picked }
                : film,
            });
          }}
        />
      ) : null}

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16 }}>
        <div style={{ flex: '999 1 520px', minWidth: 0 }}>
          <Input.TextArea
            value={draft}
            autoSize={{ minRows: 14, maxRows: 30 }}
            placeholder={'Cảnh 1. Mở đầu\nMỗi câu lời đọc một dòng.\n\nCảnh 2. …'}
            onChange={(event) => {
              const value = event.target.value;
              setDraft(value);
              window.clearTimeout(timer.current);
              timer.current = window.setTimeout(() => saveDraft(value), 500);
            }}
            onBlur={() => saveDraft(draft)}
          />
        </div>
        <aside style={{ flex: '1 1 280px', background: '#f8f9fb', borderRadius: 10, padding: 16, display: 'flex', flexDirection: 'column', gap: 8 }}>
          <Typography.Text strong>Kiểm tra</Typography.Text>
          <Typography.Text>
            {scenes.length ? `✓ ${scenes.length} cảnh` : '• Chưa có dòng "Cảnh N. Tiêu đề" (cần để cắt video ngắn theo cảnh)'}
            {' · '}
            {lines.length} câu · khoảng {minutes < 1 ? `${Math.round(minutes * 60)} giây` : `${minutes.toFixed(1).replace('.', ',')} phút`}
          </Typography.Text>
          <Typography.Text type={avoidHits.length ? 'danger' : undefined}>
            {avoidHits.length ? `✗ Có cụm cấm: ${avoidHits.join(', ')}` : '✓ Không có cụm cấm của thương hiệu'}
          </Typography.Text>
          <Typography.Text type={disclaimer ? undefined : 'warning'}>
            {disclaimer
              ? `✓ Có câu miễn trừ (câu ${disclaimer.from + 1}${disclaimer.to > disclaimer.from ? `–${disclaimer.to + 1}` : ''})`
              : '• Chưa có câu miễn trừ ("…không phải lời khuyên đầu tư")'}
          </Typography.Text>
          <Typography.Text type={source && !source.approved ? 'warning' : undefined}>
            {source
              ? source.approved
                ? `✓ ${source.code} đã được duyệt`
                : `• ${source.code} đang chờ duyệt: chưa nên dựng giọng và video`
              : '• Kịch bản dán tay: tự kiểm tra số liệu có nguồn và ngày'}
          </Typography.Text>
        </aside>
      </div>
      <details open={lintCount.errors > 0}>
        <summary style={{ cursor: 'pointer' }}>
          <Typography.Text strong>Kiểm tra luật kênh</Typography.Text>{' '}
          <Tag color={lintCount.errors ? 'red' : 'green'}>{lintCount.errors ? `${lintCount.errors} lỗi` : 'Không có lỗi'}</Tag>
          {lintCount.warns ? <Tag color="gold">{lintCount.warns} cảnh báo</Tag> : null}
        </summary>
        <Space direction="vertical" size={4} style={{ width: '100%', marginTop: 8 }}>
          <Typography.Text type="secondary">
            Kênh không kèo, không gợi ý mã, không quảng cáo sàn, số liệu có nguồn và ngày. Bộ kiểm tra chỉ đọc chữ, có thể bắt nhầm hoặc sót; người duyệt vẫn là người quyết định.
          </Typography.Text>
          {shortOnly.length ? (
            <Typography.Text>
              Kịch bản viết theo đoạn: {scenes.filter((scene) => scene.n > 0).length} cảnh = {scenes.filter((scene) => scene.n > 0).length} video ngắn, {shortOnly.length} câu chỉ dành cho video ngắn (đọc giọng nhưng video dài bỏ đi),
              thêm khoảng {overhead.seconds} giây giọng đọc (+{overhead.percent}% so với video dài).
            </Typography.Text>
          ) : (
            <Typography.Text type="secondary">
              Muốn mỗi cảnh thành một video ngắn tự đứng được: viết mỗi cảnh theo kiểu hook, phần thân, câu chốt, và đánh dấu câu chỉ dành cho video ngắn bằng "[ngắn]" ở đầu dòng.
            </Typography.Text>
          )}
          {lint.length === 0 ? <Typography.Text>✓ Không thấy vấn đề.</Typography.Text> : null}
          {lint.map((issue, k) => (
            <Typography.Text key={`${issue.code}-${issue.line}-${k}`} type={issue.level === 'error' ? 'danger' : 'warning'}>
              {issue.level === 'error' ? '✗' : '•'}{' '}
              {issue.line >= 0 ? `Câu ${issue.line + 1}${issue.scene ? ` (cảnh ${issue.scene})` : ''}: ` : ''}
              {issue.message}
            </Typography.Text>
          ))}
        </Space>
      </details>
      {error ? <Alert type="error" showIcon message={error} /> : null}
    </Space>
  );
}
