import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Checkbox, Collapse, Input, Radio, Select, Space, Tag, Typography } from 'antd';
import { fetchNovixaShort, fetchNovixaShorts, renderNovixaStockMotion } from '@/shared/api/content.api';
import {
  cutKey,
  findDisclaimer,
  newShortPlan,
  resolveAutoCut,
  resolveShortPlan,
  splitIntoShorts,
  EMPTY_EDIT,
  optimizeShorts,
  SHORT_TARGET_SECONDS,
  type ShortTarget,
  type NovixaShortEdit,
  type NovixaShortEdits,
  type NovixaShortPlan,
  type ScriptScene,
  type ShortPart,
} from './novixa-shorts';
import { describeClientError } from './film-production-orchestrator';
import { filmName } from './novixa-publish-draft';

type ShortFile = { index: number; title?: string | null; from: number; to: number; seconds: number; parts?: ShortPart[] | null };

function partsLabel(parts: ShortPart[] | null | undefined, from: number, to: number) {
  const rows = parts?.length ? parts : [{ from, to }];
  return rows.map((part) => (part.from === part.to ? `${part.from + 1}` : `${part.from + 1}–${part.to + 1}`)).join(' + ');
}

/**
 * Video ngắn cắt từ video dài đã dựng (không tạo giọng mới, không dựng lại hình).
 * Theo cảnh: người dựng chọn cảnh, câu thêm và câu miễn trừ; đoạn không liền nhau được ghép.
 * Gợi ý tự động: tách theo độ dài và từ nối, giữ lại để tham khảo.
 * Luôn cắt từ bản dọc 9:16.
 */
export function NovixaShortsPanel({
  lines,
  scenes = [],
  plans = [],
  onPlansChange,
  edits = {},
  onEditsChange,
  onShortsMade,
  buildId,
  videoLines,
  videoReady,
  filmTitle = '',
}: {
  lines: string[];
  scenes?: ScriptScene[];
  plans?: NovixaShortPlan[];
  onPlansChange?: (plans: NovixaShortPlan[]) => void;
  /** Chỉnh sửa của người dựng cho các bản cắt tự gợi ý, theo khóa bản cắt. */
  edits?: NovixaShortEdits;
  onEditsChange?: (edits: NovixaShortEdits) => void;
  /** Báo số video ngắn vừa cắt xong theo cảnh (để tiến độ phim cập nhật). */
  onShortsMade?: (count: number) => void;
  buildId?: string;
  videoLines?: string[];
  videoReady?: boolean;
  /** Tên phim, in trên cảnh kết của video ngắn. */
  filmTitle?: string;
}) {
  const [target, setTarget] = useState<ShortTarget>('balanced');
  const [hookFirst, setHookFirst] = useState(true);
  const sceneStartsKey = scenes.map((scene) => scene.from).join(',');
  // Phương án tối ưu: tự chia, tự chọn hook, bỏ câu hứa lãi, gắn câu miễn trừ khi cần. Không ra được thì dùng cách chia theo độ dài.
  const optimized = useMemo(
    () => optimizeShorts(lines, target, hookFirst, scenes.map((scene) => scene.from)),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [lines, target, hookFirst, sceneStartsKey],
  );
  const cuts = useMemo(() => splitIntoShorts(lines), [lines]);
  const [rows, setRows] = useState<NovixaShortPlan[]>(plans);
  const plansKey = JSON.stringify(plans);
  useEffect(() => {
    setRows(plans);
    // Đồng bộ khi phim lưu xong hoặc mở lại.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [plansKey]);
  const [autoEdits, setAutoEdits] = useState<NovixaShortEdits>(edits);
  const editsKey = JSON.stringify(edits);
  useEffect(() => {
    setAutoEdits(edits);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editsKey]);
  const resolved = useMemo(() => rows.map((plan) => resolveShortPlan(plan, scenes, lines)), [rows, scenes, lines]);
  const [copied, setCopied] = useState('');
  const [hookCard, setHookCard] = useState(true);
  const [endCard, setEndCard] = useState(true);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [made, setMade] = useState<{ folder: string; shorts: ShortFile[] } | null>(null);
  const [saving, setSaving] = useState(0);
  const [watch, setWatch] = useState<{ index: number; url: string } | null>(null);
  const [loadingWatch, setLoadingWatch] = useState(0);
  const sameLines = !videoLines?.length || videoLines.join('\n') === lines.join('\n');

  useEffect(() => {
    if (!buildId) return undefined;
    let cancelled = false;
    void fetchNovixaShorts(buildId).then((found) => {
      if (cancelled || !found?.shorts?.length || !found.folder) return;
      setMade((current) => current ?? { folder: found.folder!.split(/[\\/]/).pop() || '', shorts: found.shorts! });
    });
    return () => {
      cancelled = true;
    };
  }, [buildId]);

  useEffect(() => () => {
    if (watch) URL.revokeObjectURL(watch.url);
  }, [watch]);

  const commit = (next: NovixaShortPlan[]) => {
    setRows(next);
    onPlansChange?.(next);
  };
  const edit = (id: string, patch: Partial<NovixaShortPlan>, save = true) => {
    const next = rows.map((row) => (row.id === id ? { ...row, ...patch } : row));
    if (save) commit(next);
    else setRows(next);
  };

  const view = async (item: ShortFile) => {
    if (!buildId || !made) return;
    if (watch?.index === item.index) {
      setWatch(null);
      return;
    }
    setLoadingWatch(item.index);
    try {
      const blob = await fetchNovixaShort(buildId, made.folder, item.index);
      setWatch({ index: item.index, url: URL.createObjectURL(blob) });
    } catch {
      setError('Không mở được video ngắn.');
    } finally {
      setLoadingWatch(0);
    }
  };

  const exportCuts = async (kind: 'scene' | 'auto', list: { from: number; to: number; title: string; hook?: string; parts?: ShortPart[] }[]) => {
    if (!buildId || !videoLines?.length || !list.length) return;
    setBusy(kind);
    setError('');
    try {
      const result = await renderNovixaStockMotion({
        buildId,
        lines: videoLines,
        phase: 'shorts',
        allowVoice: false,
        orientation: 'tall',
        cuts: list,
        hookCard: kind === 'auto' && hookCard,
        endCardTitle: kind === 'auto' && endCard && filmTitle ? filmName(filmTitle) : undefined,
      });
      const dropped = list.some((cut) => (cut.parts?.length ?? 0) > 1) && Boolean(result.shorts?.some((item) => !item.parts?.length));
      if (result.status === 'ready' && dropped) {
        // Máy chủ cũ bỏ qua `parts` và cắt liền từ đầu đến cuối: không dùng các file này.
        setError('Máy chủ API chưa nhận cách ghép đoạn (cần khởi động lại API). Các video vừa cắt chưa đúng, đừng dùng.');
      } else if (result.status === 'ready' && result.folder && result.shorts?.length) {
        setMade({ folder: result.folder.split(/[\\/]/).pop() || '', shorts: result.shorts });
        if (kind === 'scene') onShortsMade?.(result.shorts.length);
      } else {
        setError(result.message || 'Không cắt được video ngắn.');
      }
    } catch (caught) {
      const detail = describeClientError(caught);
      setError(`Không cắt được video ngắn. Kiểm tra máy chủ rồi thử lại.${detail ? ` (${detail})` : ''}`);
    } finally {
      setBusy('');
    }
  };

  const baseRows = optimized.length ? optimized.map((row) => ({ cut: row.cut, base: row.edit })) : cuts.map((cut) => ({ cut, base: EMPTY_EDIT }));
  const autoRows = baseRows.map(({ cut, base }, index) => {
    const key = cutKey(cut);
    const edit = autoEdits[key] ?? base;
    const shown = { ...cut, id: `S${String(index + 1).padStart(2, '0')}` };
    return { cut: shown, key, base, edit, result: resolveAutoCut(shown, edit, lines, hookFirst) };
  });
  const [regenerated, setRegenerated] = useState(0);
  const regenerate = () => {
    setAutoEdits({});
    onEditsChange?.({});
    setRegenerated((n) => n + 1);
  };
  const exportable = autoRows.filter((row) => row.result.exportable);
  const setEdit = (key: string, base: NovixaShortEdit, patch: Partial<NovixaShortEdit>, save = true) => {
    const next = { ...autoEdits, [key]: { ...(autoEdits[key] ?? base), ...patch } };
    setAutoEdits(next);
    if (save) onEditsChange?.(next);
  };
  const exportAuto = () => {
    const list = exportable.map(({ result }) => {
      const parts = result.parts;
      return { from: parts[0].from, to: parts[parts.length - 1].to, title: result.cardText, hook: result.cardText, parts };
    });
    void exportCuts('auto', list);
  };

  const exportScenes = () => {
    const list = rows.map((plan, k) => {
      const parts = resolved[k].parts;
      return { from: parts[0].from, to: parts[parts.length - 1].to, title: plan.title.trim() || plan.id, parts };
    });
    void exportCuts('scene', list);
  };

  const save = async (item: ShortFile) => {
    if (!buildId || !made) return;
    setSaving(item.index);
    try {
      const blob = await fetchNovixaShort(buildId, made.folder, item.index);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `video-ngan-${String(item.index).padStart(2, '0')}.mp4`;
      link.click();
      URL.revokeObjectURL(url);
    } catch {
      setError('Không tải được video ngắn.');
    } finally {
      setSaving(0);
    }
  };

  if (!cuts.length && !optimized.length) return null;

  const copy = async (id: string, text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(id);
    } catch {
      setCopied('');
    }
  };

  const sceneOptions = scenes
    .filter((scene) => scene.n > 0)
    .map((scene) => ({
      value: scene.n,
      label: `Cảnh ${scene.n}${scene.title ? ` · ${scene.title}` : ''} (câu ${scene.from + 1}–${scene.to + 1})`,
    }));
  const planBlocked = !rows.length || resolved.some((row) => row.errors.length > 0);

  return (
    <Card size="small" title="4. Video ngắn">
      <Space direction="vertical" style={{ width: '100%' }} size={12}>
        <Typography.Text type="secondary">
          Cắt từ video dài bản dọc 9:16 đã dựng. Không tạo giọng mới, không dựng lại hình. Đoạn không liền nhau được ghép lại.
        </Typography.Text>
        {buildId && !videoReady ? (
          <Alert type="warning" showIcon message="Tạo video dài bản dọc trước. Sau đó bấm xuất để cắt thành các video ngắn." />
        ) : null}
        {!sameLines ? (
          <Alert type="warning" showIcon message="Kịch bản đã đổi so với bộ ảnh của video. Tạo lại ảnh và video trước khi cắt theo cảnh." />
        ) : null}

        <Typography.Title level={5} style={{ margin: 0 }}>Theo cảnh</Typography.Title>
        {sceneOptions.length ? (
          <Space direction="vertical" style={{ width: '100%' }} size={12}>
            {rows.map((plan, k) => {
              const result = resolved[k];
              return (
                <Card key={plan.id} size="small" type="inner">
                  <Space direction="vertical" style={{ width: '100%' }} size={8}>
                    <Space wrap style={{ width: '100%' }}>
                      <strong>{plan.id}</strong>
                      <Input
                        style={{ minWidth: 260 }}
                        value={plan.title}
                        placeholder="Tên video ngắn"
                        onChange={(event) => edit(plan.id, { title: event.target.value }, false)}
                        onBlur={() => commit(rows)}
                      />
                      <Button size="small" danger onClick={() => commit(rows.filter((row) => row.id !== plan.id))}>
                        Bỏ
                      </Button>
                    </Space>
                    <Select
                      mode="multiple"
                      style={{ width: '100%' }}
                      placeholder="Chọn cảnh"
                      value={plan.scenes}
                      options={sceneOptions}
                      onChange={(value: number[]) => edit(plan.id, { scenes: value })}
                    />
                    <Space wrap>
                      <Input
                        style={{ width: 260 }}
                        value={plan.extra}
                        placeholder="Câu thêm, ví dụ 38 hoặc 36-37"
                        onChange={(event) => edit(plan.id, { extra: event.target.value }, false)}
                        onBlur={() => commit(rows)}
                      />
                      <Checkbox checked={plan.disclaimer} onChange={(event) => edit(plan.id, { disclaimer: event.target.checked })}>
                        Gắn câu miễn trừ ở cuối
                      </Checkbox>
                    </Space>
                    {result.parts.length ? (
                      <Typography.Text type="secondary">
                        câu {partsLabel(result.parts, 0, 0)} · ~{result.seconds}s{result.seconds > 60 ? ' (dài hơn 60 giây)' : ''}
                      </Typography.Text>
                    ) : null}
                    {result.errors.map((item) => <Alert key={item} type="error" showIcon message={item} />)}
                    {result.riskFlags.map((item) => <Alert key={item} type="warning" showIcon message={item} />)}
                    {result.lines.length ? (
                      <Typography.Paragraph type="secondary" style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }} ellipsis={{ rows: 3, expandable: true }}>
                        {result.lines.join('\n')}
                      </Typography.Paragraph>
                    ) : null}
                  </Space>
                </Card>
              );
            })}
            <Space wrap>
              <Button onClick={() => commit([...rows, newShortPlan(rows)])}>Thêm video ngắn</Button>
              {buildId ? (
                <Button
                  type="primary"
                  loading={busy === 'scene'}
                  disabled={!videoReady || !sameLines || planBlocked || Boolean(busy)}
                  onClick={exportScenes}
                >
                  Xuất {rows.length} video ngắn theo cảnh
                </Button>
              ) : null}
            </Space>
          </Space>
        ) : (
          <Alert
            type="info"
            showIcon
            message='Kịch bản chưa có dòng "Cảnh N. Tiêu đề".'
            description="Thêm một dòng tiêu đề trước mỗi cảnh (dòng này không được đọc) để chọn video ngắn theo cảnh. Tạm thời có thể dùng gợi ý tự động bên dưới."
          />
        )}

        {error ? <Alert type="error" showIcon message={error} /> : null}
        {made ? (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Typography.Text strong>Đã cắt</Typography.Text>
            {made.shorts.map((item) => (
              <div key={item.index}>
                <Space wrap>
                  <strong>Video ngắn {item.index}</strong>
                  {item.title ? <span>{item.title}</span> : null}
                  <span>câu {partsLabel(item.parts, item.from, item.to)}</span>
                  <span>{item.seconds}s</span>
                  <Button size="small" type={watch?.index === item.index ? 'primary' : 'default'} loading={loadingWatch === item.index} onClick={() => void view(item)}>
                    {watch?.index === item.index ? 'Ẩn' : 'Xem'}
                  </Button>
                  <Button size="small" loading={saving === item.index} onClick={() => void save(item)}>Tải về</Button>
                </Space>
                {watch?.index === item.index ? (
                  <video src={watch.url} controls autoPlay playsInline style={{ display: 'block', marginTop: 8, maxHeight: 520, maxWidth: '100%', borderRadius: 8, background: '#000' }} />
                ) : null}
              </div>
            ))}
          </Space>
        ) : null}

        <Collapse
          defaultActiveKey={['auto']}
          items={[
            {
              key: 'auto',
              label: `Phương án tối ưu tự động (${autoRows.length}) · chấm từ nội dung chữ, không phải dự đoán lượt xem`,
              children: (
                <Space direction="vertical" style={{ width: '100%' }}>
                  <Typography.Text type="secondary">
                    Tự chia kịch bản thành các video ngắn đạt ngay: chọn khoảng câu 25–58 giây, ưu tiên cắt đúng ranh giới cảnh, chọn câu hook tốt nhất, bỏ câu hứa lợi nhuận, gắn câu miễn trừ khi có nói về vào/thoát lệnh. Không đọc lại giọng. Mở từng đoạn nếu muốn chỉnh tay. “Tạo lại” sẽ xóa chỉnh tay và tính lại.
                  </Typography.Text>
                  <Space wrap>
                    <Select
                      value={target}
                      style={{ width: 230 }}
                      onChange={(value: ShortTarget) => { setTarget(value); setAutoEdits({}); onEditsChange?.({}); }}
                      options={[
                        { value: 'short', label: `Ưu tiên ngắn (~${SHORT_TARGET_SECONDS.short}s)` },
                        { value: 'balanced', label: `Cân bằng (~${SHORT_TARGET_SECONDS.balanced}s)` },
                        { value: 'long', label: `Ưu tiên dài (~${SHORT_TARGET_SECONDS.long}s)` },
                      ]}
                    />
                    <Button type="primary" ghost onClick={regenerate}>Tạo lại phương án tối ưu</Button>
                    {regenerated ? <Typography.Text type="secondary">Đã tạo lại: {autoRows.length} video ngắn.</Typography.Text> : null}
                  </Space>
                  <Checkbox checked={hookFirst} onChange={(event) => setHookFirst(event.target.checked)}>
                    Hook lên đầu
                  </Checkbox>
                  <Checkbox checked={hookCard} onChange={(event) => setHookCard(event.target.checked)}>
                    Thêm cảnh chữ hook 2,6 giây ở đầu
                  </Checkbox>
                  <Checkbox checked={endCard} disabled={!filmTitle} onChange={(event) => setEndCard(event.target.checked)}>
                    Thêm cảnh kết 2 giây “Xem bản đầy đủ” (dẫn sang video dài)
                  </Checkbox>
                  {buildId ? (
                    <Button
                      loading={busy === 'auto'}
                      disabled={!videoReady || !exportable.length || Boolean(busy)}
                      onClick={exportAuto}
                    >
                      Xuất {exportable.length}/{autoRows.length} video ngắn đã đạt
                    </Button>
                  ) : null}
                  <Collapse
                    items={autoRows.map(({ cut, key, base, edit, result }) => {
                      const hasDisclaimer = Boolean(findDisclaimer(lines));
                      const edited = autoEdits[key] !== undefined;
                      return {
                        key: cut.id,
                        label: (
                          <Space wrap>
                            <strong>{cut.id}</strong>
                            <span>câu {cut.startIndex + 1}–{cut.endIndex + 1}</span>
                            <span>~{result.seconds}s</span>
                            <Tag color={result.verdict.color}>{result.verdict.label}</Tag>
                            <Tag>Hook {result.scores.hook}</Tag>
                            <Tag>Hợp lý {result.scores.coherence}</Tag>
                            <Tag>Hot {result.scores.hot}</Tag>
                            {edited ? <Tag color="blue">Đã sửa</Tag> : null}
                            {result.exportable ? null : <Tag color="default">Chưa xuất được</Tag>}
                          </Space>
                        ),
                        children: (
                          <Space direction="vertical" style={{ width: '100%' }} size={10}>
                            {result.riskFlags.map((flag) => <Alert key={flag} type="error" showIcon message={flag} />)}
                            {result.notes.map((note) => <Typography.Text key={note} type="secondary">• {note}</Typography.Text>)}
                            <div>
                              <Typography.Text strong>Chữ trên cảnh hook (im lặng, 2,6 giây)</Typography.Text>
                              <Input.TextArea
                                autoSize={{ minRows: 1, maxRows: 4 }}
                                maxLength={140}
                                showCount
                                value={edit.cardText ?? ''}
                                placeholder={result.hookText}
                                onChange={(event) => setEdit(key, base, { cardText: event.target.value }, false)}
                                onBlur={() => onEditsChange?.(autoEdits)}
                              />
                              <Typography.Text type="secondary">Để trống thì dùng đúng câu hook. Chỉ đổi chữ trên hình, giọng nói không đổi.</Typography.Text>
                            </div>
                            <div>
                              <Typography.Text strong>Câu trong đoạn (chọn hook, bỏ câu thừa)</Typography.Text>
                              <Radio.Group
                                style={{ display: 'block', width: '100%' }}
                                value={result.hookIndex}
                                onChange={(event) => setEdit(key, base, { hookLine: event.target.value as number })}
                              >
                                {Array.from({ length: cut.endIndex - cut.startIndex + 1 }, (_, k) => cut.startIndex + k).map((i) => {
                                  const dropped = edit.drop.includes(i);
                                  return (
                                    <div key={i} style={{ display: 'flex', gap: 8, alignItems: 'flex-start', padding: '2px 0', opacity: dropped ? 0.45 : 1 }}>
                                      <Radio value={i} disabled={dropped} />
                                      <Checkbox
                                        checked={!dropped}
                                        onChange={(event) => setEdit(key, base, { drop: event.target.checked ? edit.drop.filter((n) => n !== i) : [...edit.drop, i] })}
                                      />
                                      <Typography.Text type="secondary" style={{ minWidth: 28 }}>{i + 1}.</Typography.Text>
                                      <Typography.Text delete={dropped} style={{ flex: 1, minWidth: 0, wordBreak: 'break-word' }}>
                                        {lines[i] ?? '(không có chữ)'}
                                      </Typography.Text>
                                    </div>
                                  );
                                })}
                              </Radio.Group>
                              <Typography.Text type="secondary">Nút tròn: câu hook. Ô vuông: giữ câu trong video ngắn.</Typography.Text>
                            </div>
                            <Space wrap>
                              <Checkbox
                                checked={edit.disclaimer}
                                disabled={!hasDisclaimer}
                                onChange={(event) => setEdit(key, base, { disclaimer: event.target.checked })}
                              >
                                Gắn câu miễn trừ ở cuối{hasDisclaimer ? '' : ' (kịch bản chưa có câu này)'}
                              </Checkbox>
                              <Checkbox
                                checked={edit.approved}
                                disabled={result.riskFlags.length > 0}
                                onChange={(event) => setEdit(key, base, { approved: event.target.checked })}
                              >
                                Tôi đã xem, cho xuất dù điểm thấp
                              </Checkbox>
                              <Button size="small" disabled={!edited} onClick={() => {
                                const next = { ...autoEdits };
                                delete next[key];
                                setAutoEdits(next);
                                onEditsChange?.(next);
                              }}>
                                Khôi phục gợi ý gốc
                              </Button>
                            </Space>
                            <Typography.Text type="secondary">
                              Thứ tự đọc: câu {result.order.map((i) => i + 1).join(', ')} · ~{result.seconds}s
                            </Typography.Text>
                            <Typography.Paragraph style={{ whiteSpace: 'pre-wrap', marginBottom: 0 }}>{result.lines.join('\n')}</Typography.Paragraph>
                            <Button size="small" onClick={() => void copy(cut.id, result.lines.join('\n'))}>
                              {copied === cut.id ? 'Đã chép' : 'Chép kịch bản'}
                            </Button>
                          </Space>
                        ),
                      };
                    })}
                  />
                </Space>
              ),
            },
          ]}
        />
      </Space>
    </Card>
  );
}
