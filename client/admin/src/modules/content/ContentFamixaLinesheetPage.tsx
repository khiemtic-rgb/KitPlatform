import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Collapse,
  Form,
  Input,
  Modal,
  Progress,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  buildLinesheetTimeline,
  cancelLinesheetRun,
  createLinesheetProduction,
  draftLinesheetShots,
  generateLinesheetTts,
  previewLinesheetTts,
  fetchLinesheetBlob,
  getLinesheetStatus,
  importLinesheetScript,
  linesheetPaths,
  listLinesheetProductions,
  listLinesheetRuns,
  putLinesheetSettings,
  putLinesheetShots,
  queueLinesheetRender,
  uploadLinesheetFiles,
  type LinesheetProduction,
  type LinesheetQueueRender,
  type LinesheetRun,
  type LinesheetStatus,
  type LinesheetTtsLine,
  type LinesheetTtsPreview,
} from '@/shared/api/famixa-linesheet.api';
import { ContentPageHeader } from './ContentPageHeader';
import { KeyframeStep, LipsyncStep, MotionStep, OutroStep, QaStep, StepCard } from './LinesheetAiSteps';

const KIND_LABEL: Record<string, string> = {
  line_audio: 'Giọng',
  cue_music: 'Nhạc',
  cue_sfx: 'Hiệu ứng',
  layer_bg: 'Nền',
  layer_mid: 'Tiền cảnh',
  character: 'Nhân vật',
};

const STATUS_TAG: Record<LinesheetRun['status'], { color: string; label: string }> = {
  QUEUED: { color: 'default', label: 'Chờ dựng' },
  RUNNING: { color: 'processing', label: 'Đang dựng' },
  SUCCEEDED: { color: 'success', label: 'Xong' },
  FAILED: { color: 'error', label: 'Lỗi' },
  CANCELLED: { color: 'warning', label: 'Đã hủy' },
};

const SIZES: Record<LinesheetQueueRender['format'], { label: string; w: number; h: number }[]> = {
  '16x9': [
    { label: 'Xem thử 1280×720', w: 1280, h: 720 },
    { label: 'Nhanh 640×360', w: 640, h: 360 },
    { label: 'Đầy đủ 1920×1080', w: 1920, h: 1080 },
  ],
  '9x16': [
    { label: 'Xem thử 540×960', w: 540, h: 960 },
    { label: 'Đầy đủ 1080×1920', w: 1080, h: 1920 },
  ],
};

export function fmtMs(ms: number | null | undefined) {
  if (ms == null) return '—';
  const s = Math.round(ms / 100) / 10;
  const m = Math.floor(s / 60);
  return `${m}:${(s - m * 60).toFixed(1).padStart(4, '0')}`;
}

/** "1:23.5" hoặc "83.5" → ms. Trống → undefined. */
export function parseTime(v: string): number | undefined | null {
  const t = v.trim();
  if (!t) return undefined;
  const m = /^(?:(\d+):)?(\d+(?:\.\d+)?)$/.exec(t);
  if (!m) return null;
  return Math.round(((m[1] ? Number(m[1]) * 60 : 0) + Number(m[2])) * 1000);
}

function saveBlob(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 30_000);
}

/** Nút chọn file (một file), gọi onPick rồi xóa lựa chọn để chọn lại cùng file vẫn chạy. */
function FileButton({ accept, onPick, children, disabled, loading, type }: {
  accept: string;
  onPick: (f: File) => void;
  children: ReactNode;
  disabled?: boolean;
  loading?: boolean;
  type?: 'primary' | 'default';
}) {
  const ref = useRef<HTMLInputElement>(null);
  return (
    <>
      <input
        ref={ref}
        type="file"
        accept={accept}
        style={{ display: 'none' }}
        onChange={(e) => {
          const f = e.target.files?.[0];
          e.target.value = '';
          if (f) onPick(f);
        }}
      />
      <Button type={type} disabled={disabled} loading={loading} onClick={() => ref.current?.click()}>
        {children}
      </Button>
    </>
  );
}

function MissingList({ label, items }: { label: string; items: string[] }) {
  if (items.length === 0) return null;
  return (
    <Collapse
      size="small"
      style={{ marginTop: 8 }}
      items={[{ key: label, label: `${label}: thiếu ${items.length}`, children: <Typography.Text code>{items.slice(0, 300).join(', ')}</Typography.Text> }]}
    />
  );
}

/**
 * M1 "Dựng tập từ bảng câu": kịch bản CSV + giọng + ảnh tách lớp → video nháp (FFmpeg trên máy chủ, không gọi AI trả phí).
 * Ảnh trong gói mẫu là ảnh giữ chỗ; thay bằng ảnh thật thì chỉ shot dùng ảnh đó được dựng lại.
 */
export function ContentFamixaLinesheetPage() {
  const { message, modal } = App.useApp();
  const [productions, setProductions] = useState<LinesheetProduction[]>([]);
  const [selected, setSelected] = useState<string | undefined>();
  const [status, setStatus] = useState<LinesheetStatus | null>(null);
  const [runs, setRuns] = useState<LinesheetRun[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [uploadPct, setUploadPct] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [allowMissing, setAllowMissing] = useState(false);
  const [preview, setPreview] = useState<{ url: string; title: string } | null>(null);
  const [tts, setTts] = useState<LinesheetTtsPreview | null>(null);
  const [createForm] = Form.useForm<{ universeCode: string; episodeCode: string; title?: string }>();

  const [format, setFormat] = useState<LinesheetQueueRender['format']>('16x9');
  const [sizeIdx, setSizeIdx] = useState(0);
  const [rangeFrom, setRangeFrom] = useState('');
  const [rangeTo, setRangeTo] = useState('');
  const [textLang, setTextLang] = useState<'vi' | 'en'>('vi');
  const [burnSubs, setBurnSubs] = useState<'none' | 'vi' | 'en'>('none');

  const loadProductions = useCallback(async () => {
    try {
      const list = await listLinesheetProductions();
      setProductions(list);
      setSelected((cur) => cur ?? list[0]?.id);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được danh sách tập'));
    }
  }, [message]);

  const refresh = useCallback(async (id = selected) => {
    if (!id) return;
    try {
      const [s, r] = await Promise.all([getLinesheetStatus(id), listLinesheetRuns(id)]);
      setStatus(s);
      setRuns(r);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được trạng thái'));
    }
  }, [selected, message]);

  useEffect(() => {
    void loadProductions();
  }, [loadProductions]);

  useEffect(() => {
    setStatus(null);
    setRuns([]);
    setTts(null);
    void refresh(selected);
  }, [selected, refresh]);

  const active = runs.some((r) => r.status === 'QUEUED' || r.status === 'RUNNING');
  useEffect(() => {
    if (!active || !selected) return;
    const t = setInterval(() => {
      void listLinesheetRuns(selected).then(setRuns).catch(() => undefined);
    }, 3000);
    return () => clearInterval(t);
  }, [active, selected]);

  useEffect(() => () => {
    if (preview) URL.revokeObjectURL(preview.url);
  }, [preview]);

  /** Chạy một thao tác, báo lỗi (kể cả danh sách lỗi theo hàng), rồi tải lại trạng thái. */
  const act = async (key: string, fn: () => Promise<string | void>) => {
    setBusy(key);
    try {
      const ok = await fn();
      if (ok) message.success(ok);
      await refresh();
    } catch (e) {
      const data = (e as { response?: { data?: { errors?: { rowId: string; message: string; code: string }[] } } }).response?.data;
      if (data?.errors?.length) {
        modal.error({
          title: 'Dữ liệu chưa hợp lệ',
          width: 640,
          content: (
            <div style={{ maxHeight: 360, overflow: 'auto' }}>
              {data.errors.slice(0, 100).map((x, i) => (
                <div key={i}>
                  <Typography.Text code>{x.rowId || '—'}</Typography.Text> {x.message} <Typography.Text type="secondary">({x.code})</Typography.Text>
                </div>
              ))}
            </div>
          ),
        });
      } else {
        message.error(apiErrorMessage(e, 'Không thực hiện được'));
      }
    } finally {
      setBusy(null);
    }
  };

  const download = (path: string) =>
    act('download', async () => {
      const { blob, name } = await fetchLinesheetBlob(path);
      saveBlob(blob, name);
    });

  const id = selected;
  const s = status;
  const scriptDone = !!s?.scriptVersion;
  const filesDone = !!s && s.missingAudio.length === 0 && s.missingCues.length === 0 && s.missingLayers.length === 0 && scriptDone;
  const timelineDone = s?.timelineTotalMs != null;
  const shotsDone = s?.shotCount != null;
  const size = SIZES[format][Math.min(sizeIdx, SIZES[format].length - 1)] ?? SIZES[format][0]!;

  const queue = () => {
    const from = parseTime(rangeFrom);
    const to = parseTime(rangeTo);
    if (from === null || to === null) return message.warning('Khoảng thời gian dạng 0:45 hoặc 45.');
    return act('queue', async () => {
      const run = await queueLinesheetRender(id!, { format, width: size.w, height: size.h, rangeStartMs: from, rangeEndMs: to, textLang, burnSubs });
      return `Đã xếp hàng: ${run.stepCount - 2} shot, ${run.framesExpected ?? 0} khung.`;
    });
  };

  const lineAudio = useRef<HTMLAudioElement | null>(null);
  const playLine = async (rowId: string) => {
    if (!id) return;
    try {
      const { blob } = await fetchLinesheetBlob(linesheetPaths.lineAudio(id, rowId));
      lineAudio.current?.pause();
      const a = new Audio(URL.createObjectURL(blob));
      lineAudio.current = a;
      a.onended = () => URL.revokeObjectURL(a.src);
      await a.play();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không nghe được câu này'));
    }
  };

  const openPreview = (run: LinesheetRun) =>
    act('preview', async () => {
      const { blob } = await fetchLinesheetBlob(linesheetPaths.runFile(run.id, 'video'));
      setPreview({ url: URL.createObjectURL(blob), title: `${run.format} ${run.width}×${run.height} · ${fmtMs(run.rangeStartMs)}–${fmtMs(run.rangeEndMs)}` });
    });

  const columns = useMemo(
    () => [
      {
        title: 'Lần dựng',
        key: 'what',
        render: (_: unknown, r: LinesheetRun) => (
          <Space direction="vertical" size={0}>
            <Typography.Text>{r.format} · {r.width}×{r.height} · {r.fps} fps</Typography.Text>
            <Typography.Text type="secondary">
              {fmtMs(r.rangeStartMs)}–{fmtMs(r.rangeEndMs)} · chữ {r.textLang}{r.burnSubs !== 'none' ? ` · đốt phụ đề ${r.burnSubs}` : ''}
            </Typography.Text>
          </Space>
        ),
      },
      {
        title: 'Trạng thái',
        key: 'status',
        width: 260,
        render: (_: unknown, r: LinesheetRun) => (
          <Space direction="vertical" size={2} style={{ width: '100%' }}>
            <Tag color={STATUS_TAG[r.status].color}>{STATUS_TAG[r.status].label}</Tag>
            {r.status === 'RUNNING' || r.status === 'QUEUED' ? (
              <Progress percent={Math.round((r.nextStep / Math.max(r.stepCount, 1)) * 100)} size="small" />
            ) : null}
            {r.error ? <Typography.Text type="danger" style={{ fontSize: 12 }}>{r.error}</Typography.Text> : null}
          </Space>
        ),
      },
      {
        title: 'Khung',
        key: 'frames',
        width: 130,
        render: (_: unknown, r: LinesheetRun) => (
          <span>
            {r.framesActual ?? '—'} / {r.framesExpected ?? '—'}
            {r.shotLog.length ? (
              <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
                {r.shotLog.filter((x) => x.cached).length}/{r.shotLog.length} shot từ cache
              </Typography.Text>
            ) : null}
          </span>
        ),
      },
      {
        title: '',
        key: 'actions',
        width: 280,
        render: (_: unknown, r: LinesheetRun) => (
          <Space wrap>
            {r.hasOutput ? (
              <>
                <Button size="small" type="primary" onClick={() => void openPreview(r)}>Xem</Button>
                <Button size="small" onClick={() => void download(linesheetPaths.runFile(r.id, 'video'))}>Tải MP4</Button>
                <Button size="small" onClick={() => void download(linesheetPaths.runFile(r.id, 'srt-vi'))}>SRT vi</Button>
                <Button size="small" onClick={() => void download(linesheetPaths.runFile(r.id, 'srt-en'))}>SRT en</Button>
              </>
            ) : null}
            {r.status === 'QUEUED' || r.status === 'RUNNING' ? (
              <Button size="small" danger onClick={() => void act('cancel', async () => { await cancelLinesheetRun(r.id); return 'Đã hủy.'; })}>Hủy</Button>
            ) : null}
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [runs],
  );

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <ContentPageHeader
        title="Dựng từ bảng câu"
        description="Kịch bản CSV + giọng + ảnh → video có phụ đề Việt/Anh. Bước 4, 7, 8, 9 gọi AI trả phí: luôn Xem trước, và chỉ chạy khi đã xác nhận đúng số tiền."
        extra={
          <Space>
            <Link to="/content/studio">← Video Studio</Link>
            <Select
              style={{ minWidth: 260 }}
              placeholder="Chọn tập"
              value={selected}
              onChange={setSelected}
              options={productions.map((p) => ({ value: p.id, label: `${p.universeCode} · ${p.episodeCode}${p.title ? ` · ${p.title}` : ''}` }))}
            />
            <Button onClick={() => setCreateOpen(true)}>+ Tập mới</Button>
          </Space>
        }
      />

      {!id ? <Alert type="info" showIcon message="Chưa có tập nào. Bấm “+ Tập mới”, ví dụ LECH_TONG · EP01." /> : null}

      {id && s ? (
        <>
          <StepCard
            n={1}
            title="Kịch bản (CSV bảng câu)"
            done={scriptDone}
            extra={
              <FileButton accept=".csv,text/csv" loading={busy === 'script'} onPick={(f) => void act('script', async () => `Đã nhập phiên bản ${(await importLinesheetScript(id, f)).version}.`)}>
                Nhập CSV
              </FileButton>
            }
          >
            {scriptDone ? (
              <Typography.Text>Phiên bản {s.scriptVersion} · {s.rowCount} hàng · {s.lineCount} câu thoại</Typography.Text>
            ) : (
              <Typography.Text type="secondary">
                Cột: id, scene, type (line|pause|music|sfx), speaker, vi, en, direction, duration_ms, pause_after_ms, cue, screen_vi, screen_en.
                Lỗi được báo theo từng hàng; một hàng sai thì không nhập gì.
              </Typography.Text>
            )}
          </StepCard>

          <StepCard
            n={2}
            title="Bối cảnh và nhân vật (JSON)"
            done={s.settingsReady}
            extra={
              <FileButton
                accept=".json,application/json"
                loading={busy === 'settings'}
                onPick={(f) =>
                  void act('settings', async () => {
                    let json: unknown;
                    try {
                      json = JSON.parse(await f.text());
                    } catch {
                      throw new Error('File không phải JSON hợp lệ.');
                    }
                    await putLinesheetSettings(id, json);
                    return 'Đã lưu cấu hình bối cảnh.';
                  })
                }
              >
                Nhập settings.json
              </FileButton>
            }
          >
            {s.settingsErrors.length ? (
              <Alert type="warning" showIcon message="Cấu hình chưa khớp kịch bản" description={s.settingsErrors.slice(0, 8).join(' · ')} />
            ) : (
              <Typography.Text type="secondary">scene_to_setting, row_setting_rules, settings (cast), characters (height), chat_self.</Typography.Text>
            )}
          </StepCard>

          <StepCard
            n={3}
            title="Giọng, nhạc, ảnh (một file .zip)"
            done={filesDone}
            extra={
              <FileButton
                accept=".zip,application/zip"
                loading={busy === 'files'}
                disabled={!scriptDone}
                onPick={(f) =>
                  void act('files', async () => {
                    setUploadPct(0);
                    try {
                      const r = await uploadLinesheetFiles(id, f, setUploadPct);
                      return `Nhận ${r.accepted} file${r.skipped.length ? `, bỏ qua ${r.skipped.length}` : ''}.`;
                    } finally {
                      setUploadPct(null);
                    }
                  })
                }
              >
                Tải zip
              </FileButton>
            }
          >
            <Typography.Text type="secondary" style={{ display: 'block' }}>
              audio/&lt;id câu&gt;.mp3|wav · music/&lt;cue&gt;.* · sfx/&lt;cue&gt;.* · settings/&lt;bối cảnh&gt;/bg.png, mid.png · characters/&lt;ID&gt;.png.
              Giọng được cắt khoảng lặng thừa và đo thời lượng khi nhận. Tải lại cùng tên thì thay file cũ.
            </Typography.Text>
            {uploadPct != null ? <Progress percent={uploadPct} size="small" /> : null}
            <Space wrap style={{ marginTop: 8 }}>
              {Object.entries(s.fileCounts).map(([k, v]) => (
                <Tag key={k}>{KIND_LABEL[k] ?? k}: {v}</Tag>
              ))}
            </Space>
            <MissingList label="Giọng" items={s.missingAudio} />
            <MissingList label="Nhạc/hiệu ứng (sẽ là khoảng lặng)" items={s.missingCues} />
            <MissingList label="Ảnh" items={s.missingLayers} />
          </StepCard>

          <StepCard
            n={4}
            title="Giọng ElevenLabs (tùy chọn, tốn phí)"
            done={!!tts && tts.pendingCount === 0 && tts.lineCount > 0}
            extra={
              <Space>
                <Button disabled={!scriptDone} loading={busy === 'tts-preview'} onClick={() => void act('tts-preview', async () => { setTts(await previewLinesheetTts(id)); })}>
                  Xem trước
                </Button>
                <Button
                  type="primary"
                  disabled={!tts || tts.pendingCount === 0}
                  loading={busy === 'tts'}
                  onClick={() =>
                    tts &&
                    modal.confirm({
                      title: 'Tạo giọng bằng ElevenLabs?',
                      content: `Sẽ gửi ${tts.pendingCount} câu, ${tts.pendingChars.toLocaleString('vi-VN')} ký tự (mô hình ${tts.model}). Lượt này trừ vào hạn mức gói ElevenLabs của anh.`,
                      okText: 'Tạo giọng',
                      onOk: () =>
                        act('tts', async () => {
                          const r = await generateLinesheetTts(id, { onlyPending: true, confirmChars: tts.pendingChars });
                          setTts(await previewLinesheetTts(id));
                          return `Đã tạo ${r.generated} câu${r.failed.length ? `, lỗi ${r.failed.length}: ${r.failed.slice(0, 2).join(' · ')}` : ''}. Bấm Tính lại dòng thời gian.`;
                        }),
                    })
                  }
                >
                  Tạo giọng{tts ? ` (${tts.pendingCount} câu)` : ''}
                </Button>
              </Space>
            }
          >
            {tts ? (
              <>
                <Typography.Text>
                  {tts.lineCount} câu có giọng · {tts.totalChars.toLocaleString('vi-VN')} ký tự · cần tạo {tts.pendingCount} câu ({tts.pendingChars.toLocaleString('vi-VN')} ký tự)
                </Typography.Text>
                {tts.missingVoices.length ? (
                  <Alert style={{ marginTop: 8 }} type="warning" showIcon message={`Chưa có voice_id cho: ${tts.missingVoices.join(', ')} — các câu này giữ giọng đã tải lên.`} />
                ) : null}
                <Collapse
                  size="small"
                  style={{ marginTop: 8 }}
                  items={[{
                    key: 'lines',
                    label: 'Từng câu (chữ gửi đi kèm thẻ cảm xúc)',
                    children: (
                      <Table<LinesheetTtsLine>
                        rowKey="rowId"
                        size="small"
                        pagination={{ pageSize: 20 }}
                        dataSource={tts.lines}
                        columns={[
                          { title: 'Câu', dataIndex: 'rowId', width: 100 },
                          { title: 'Người nói', dataIndex: 'speaker', width: 100 },
                          { title: 'Gửi đi', dataIndex: 'text' },
                          { title: '', key: 'st', width: 90, render: (_: unknown, l: LinesheetTtsLine) => (l.pending ? <Tag color="orange">Cần tạo</Tag> : <Tag color="green">Đã có</Tag>) },
                          { title: 'Bản', key: 'take', width: 56, render: (_: unknown, l: LinesheetTtsLine) => l.take },
                          {
                            title: '',
                            key: 'redo',
                            width: 150,
                            render: (_: unknown, l: LinesheetTtsLine) => (
                              <Space size={4}>
                                <Button size="small" disabled={l.pending} onClick={() => void playLine(l.rowId)}>
                                  Nghe
                                </Button>
                                <Button
                                  size="small"
                                  title="Tạo một bản đọc mới cho câu này (seed khác), dùng khi câu bị lơ lớ hoặc sai cảm xúc"
                                  onClick={() =>
                                    void act('tts', async () => {
                                      await generateLinesheetTts(id, { rowIds: [l.rowId], onlyPending: false, confirmChars: l.chars, newTake: true });
                                      setTts(await previewLinesheetTts(id));
                                      void playLine(l.rowId);
                                      return `Đã tạo bản ${l.take + 1} cho ${l.rowId}. Nghe thử; ổn thì bấm Tính lại dòng thời gian.`;
                                    })
                                  }
                                >
                                  Bản mới
                                </Button>
                              </Space>
                            ),
                          },
                        ]}
                      />
                    ),
                  }]}
                />
              </>
            ) : (
              <Typography.Text type="secondary">
                Giọng từng nhân vật lấy từ mục <Typography.Text code>voices</Typography.Text> trong settings.json; thẻ cảm xúc tự lấy từ cột direction (ghi đè bằng cột tts_tags). Khóa ElevenLabs dùng khóa đã lưu trong cài đặt AI của Content. Bấm Xem trước trước khi tạo.
              </Typography.Text>
            )}
          </StepCard>

          <StepCard
            n={5}
            title="Dòng thời gian và phụ đề"
            done={timelineDone}
            extra={
              <Space>
                <Checkbox checked={allowMissing} onChange={(e) => setAllowMissing(e.target.checked)}>Cho phép thiếu giọng (ước lượng)</Checkbox>
                <Button
                  type="primary"
                  disabled={!scriptDone}
                  loading={busy === 'timeline'}
                  onClick={() => void act('timeline', async () => { await buildLinesheetTimeline(id, allowMissing); return 'Đã tính dòng thời gian.'; })}
                >
                  Tính
                </Button>
              </Space>
            }
          >
            {timelineDone ? (
              <Space wrap>
                <Typography.Text>Tổng {fmtMs(s.timelineTotalMs)}</Typography.Text>
                {s.timelineEstimatedCount ? <Tag color="warning">{s.timelineEstimatedCount} câu ước lượng</Tag> : null}
                <Button size="small" onClick={() => void download(linesheetPaths.timelineCsv(id))}>timeline.csv</Button>
                <Button size="small" onClick={() => void download(linesheetPaths.subtitles(id, 'vi'))}>SRT vi</Button>
                <Button size="small" onClick={() => void download(linesheetPaths.subtitles(id, 'en'))}>SRT en</Button>
              </Space>
            ) : (
              <Typography.Text type="secondary">Tính lại mỗi khi nhập kịch bản mới hoặc đổi giọng.</Typography.Text>
            )}
          </StepCard>

          <StepCard
            n={6}
            title="Bảng shot"
            done={shotsDone}
            extra={
              <Space>
                <Button
                  disabled={!timelineDone || !s.settingsReady}
                  loading={busy === 'draft'}
                  onClick={() => {
                    const run = () => void act('draft', async () => `Đã tạo ${(await draftLinesheetShots(id, true)).shots} shot nháp.`);
                    if (s.shotsEdited) {
                      modal.confirm({ title: 'Bảng shot đã được sửa tay', content: 'Tạo bảng nháp mới sẽ thay bảng đã sửa. Tải bảng cũ về trước nếu cần.', okText: 'Tạo lại', onOk: run });
                    } else run();
                  }}
                >
                  Tạo bảng nháp
                </Button>
                <Button disabled={!shotsDone} onClick={() => void download(linesheetPaths.shotsCsv(id))}>Tải CSV</Button>
                <FileButton accept=".csv,text/csv" disabled={!scriptDone} loading={busy === 'shots'} onPick={(f) => void act('shots', async () => `Đã lưu ${(await putLinesheetShots(id, f)).shots} shot đã sửa.`)}>
                  Nhập CSV đã sửa
                </FileButton>
              </Space>
            }
          >
            {shotsDone ? (
              <Typography.Text>{s.shotCount} shot{s.shotsEdited ? ' · đã sửa tay' : ' · bản nháp tự động'}</Typography.Text>
            ) : (
              <Typography.Text type="secondary">Bảng nháp tự chia shot theo bối cảnh, khoảng lặng dài và độ dài tối đa 7 giây; sửa preset máy, nhân vật, focus trong Excel rồi nhập lại.</Typography.Text>
            )}
          </StepCard>

          <KeyframeStep n={7} id={id} onChanged={() => void refresh()} />
          <MotionStep n={8} id={id} disabled={shotsDone ? null : 'Cần bảng shot (bước 6) trước.'} onChanged={() => void refresh()} />
          <LipsyncStep
            n={9}
            id={id}
            disabled={!shotsDone || !timelineDone ? 'Cần dòng thời gian và bảng shot đã chốt trước.' : null}
            onChanged={() => void refresh()}
          />
          <OutroStep n={10} id={id} onChanged={() => void refresh()} />

          <StepCard n={11} title="Dựng video" done={runs.some((r) => r.status === 'SUCCEEDED')}>
            <Space wrap align="center">
              <Radio.Group
                value={format}
                onChange={(e) => {
                  setFormat(e.target.value);
                  setSizeIdx(0);
                  setBurnSubs(e.target.value === '9x16' ? 'vi' : 'none');
                }}
                optionType="button"
                options={[{ value: '16x9', label: '16:9 YouTube' }, { value: '9x16', label: '9:16 Shorts' }]}
              />
              <Select style={{ width: 190 }} value={sizeIdx} onChange={setSizeIdx} options={SIZES[format].map((x, i) => ({ value: i, label: x.label }))} />
              <Input style={{ width: 90 }} placeholder="từ 0:00" value={rangeFrom} onChange={(e) => setRangeFrom(e.target.value)} />
              <Input style={{ width: 90 }} placeholder="đến hết" value={rangeTo} onChange={(e) => setRangeTo(e.target.value)} />
              <Select style={{ width: 120 }} value={textLang} onChange={setTextLang} options={[{ value: 'vi', label: 'Chữ màn hình vi' }, { value: 'en', label: 'Chữ màn hình en' }]} />
              <Select
                style={{ width: 150 }}
                value={burnSubs}
                onChange={setBurnSubs}
                options={[{ value: 'none', label: 'Phụ đề rời (2 track)' }, { value: 'vi', label: 'Đốt phụ đề vi' }, { value: 'en', label: 'Đốt phụ đề en' }]}
              />
              <Button type="primary" disabled={!shotsDone || !timelineDone} loading={busy === 'queue'} onClick={() => void queue()}>
                Dựng
              </Button>
            </Space>
            <Typography.Paragraph type="secondary" style={{ margin: '8px 0 0' }}>
              Mỗi shot được lưu cache: đổi một ảnh hay một câu chỉ dựng lại shot liên quan. Ảnh trong gói mẫu là ảnh giữ chỗ để kiểm nhịp và khung; thay ảnh thật khi có.
            </Typography.Paragraph>
          </StepCard>

          <QaStep n={12} id={id} status={s} />

          <Card size="small" title="Các lần dựng" extra={<Button size="small" onClick={() => void refresh()}>Làm mới</Button>}>
            <Table rowKey="id" size="small" pagination={false} dataSource={runs} columns={columns} locale={{ emptyText: 'Chưa dựng lần nào.' }} />
          </Card>
        </>
      ) : null}

      <Modal
        open={createOpen}
        title="Tập mới"
        okText="Tạo"
        onCancel={() => setCreateOpen(false)}
        confirmLoading={busy === 'create'}
        onOk={() =>
          void createForm.validateFields().then((v) =>
            act('create', async () => {
              const p = await createLinesheetProduction(v);
              setCreateOpen(false);
              createForm.resetFields();
              await loadProductions();
              setSelected(p.id);
              return `Đã tạo ${p.universeCode} · ${p.episodeCode}.`;
            }),
          )
        }
      >
        <Form form={createForm} layout="vertical" initialValues={{ universeCode: 'LECH_TONG', episodeCode: 'EP01' }}>
          <Form.Item name="universeCode" label="Mã series" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]{1,64}$/, message: 'Chữ, số, _ hoặc -' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="episodeCode" label="Mã tập" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]{1,32}$/, message: 'Chữ, số, _ hoặc -' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="title" label="Tên tập">
            <Input placeholder="Đêm tuyệt nhất" />
          </Form.Item>
        </Form>
      </Modal>

      <Modal open={!!preview} title={preview?.title} footer={null} width={960} destroyOnClose onCancel={() => setPreview(null)}>
        {preview ? <video src={preview.url} controls autoPlay style={{ width: '100%', maxHeight: '70vh', background: '#000' }} /> : null}
      </Modal>
    </Space>
  );
}
