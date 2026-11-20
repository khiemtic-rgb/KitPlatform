import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Alert, App, Button, Card, Checkbox, Form, Input, InputNumber, Modal, Progress, Select, Space, Switch, Table, Tag, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchLinesheetBlob,
  generateLinesheetKeyframes,
  generateLinesheetLipsync,
  generateLinesheetMotion,
  getLinesheetSettings,
  linesheetPaths,
  listLinesheetRuns,
  previewLinesheetKeyframes,
  previewLinesheetLipsync,
  previewLinesheetMotion,
  putLinesheetSettings,
  type LinesheetRun,
  type LinesheetStatus,
} from '@/shared/api/famixa-linesheet.api';

/**
 * Các bước tốn phí của M1 (thêm 2026-11-06): ảnh cảnh, chuyển động (Runway), ghép môi (fal), phần mở/kết, soát trước khi đăng.
 * Quy tắc chung: Xem trước không tốn tiền; Chạy chia thành từng lượt nhỏ, mỗi lượt gửi đúng số tiền của lượt đó
 * (máy chủ từ chối nếu lệch), nên dừng giữa chừng chỉ mất tiền những lượt đã xong.
 */

export function StepCard({ n, title, done, children, extra }: { n: number; title: string; done: boolean; children?: ReactNode; extra?: ReactNode }) {
  return (
    <Card
      size="small"
      title={
        <Space>
          <Tag color={done ? 'success' : 'default'}>{done ? '✓' : n}</Tag>
          <span>{title}</span>
        </Space>
      }
      extra={extra}
    >
      {children}
    </Card>
  );
}

type Unit = 'credit' | 'usd';
const fmtCost = (v: number, unit: Unit) => (unit === 'usd' ? `${v.toFixed(2)} USD` : `${v.toLocaleString('vi-VN')} credit`);
const sumCost = (xs: number[], unit: Unit) => {
  const t = xs.reduce((a, b) => a + b, 0);
  return unit === 'usd' ? Math.round(t * 100 + 1e-9) / 100 : t;
};

type PaidItem = { key: string; cost: number; has: boolean; cells: Record<string, ReactNode>; prompt?: string };
type PaidColumn = { key: string; title: string; width?: number };
type Progressing = { done: number; total: number; used: number; failed: string[]; stopping: boolean };

/** Bảng chọn mục + xác nhận tiền + chạy từng lượt + xem kết quả. Dùng chung cho ảnh cảnh, chuyển động, ghép môi. */
function PaidStep(props: {
  n: number;
  title: string;
  unit: Unit;
  provider: string;
  batch: number;
  disabled?: string | null;
  columns: PaidColumn[];
  load: () => Promise<{ items: PaidItem[]; summary: ReactNode; notes?: string[] }>;
  run: (keys: string[], cost: number, force: boolean, prompts: Record<string, string>) => Promise<{ generated: number; used: number; failed: string[] }>;
  view?: (key: string) => { path: string; kind: 'image' | 'video' };
  editablePrompt?: boolean;
  toolbar?: ReactNode;
  onChanged: () => void;
  reloadSignal?: number;
}) {
  const { message, modal } = App.useApp();
  const [items, setItems] = useState<PaidItem[] | null>(null);
  const [summary, setSummary] = useState<ReactNode>(null);
  const [notes, setNotes] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [sel, setSel] = useState<string[]>([]);
  const [force, setForce] = useState(false);
  const [prompts, setPrompts] = useState<Record<string, string>>({});
  const [prog, setProg] = useState<Progressing | null>(null);
  const stopRef = useRef(false);
  const [viewing, setViewing] = useState<{ url: string; kind: 'image' | 'video'; title: string } | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const r = await props.load();
      setItems(r.items);
      setSummary(r.summary);
      setNotes(r.notes ?? []);
      setSel(r.items.filter((i) => !i.has).map((i) => i.key));
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không xem trước được'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (props.reloadSignal && items) void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [props.reloadSignal]);

  useEffect(() => () => {
    if (viewing) URL.revokeObjectURL(viewing.url);
  }, [viewing]);

  const byKey = useMemo(() => new Map((items ?? []).map((i) => [i.key, i])), [items]);
  const todo = sel.map((k) => byKey.get(k)).filter((i): i is PaidItem => !!i && (force || !i.has));
  const todoCost = sumCost(todo.map((i) => i.cost), props.unit);
  const skipped = sel.length - todo.length;

  const start = () => {
    if (!todo.length) return;
    const batches: PaidItem[][] = [];
    for (let i = 0; i < todo.length; i += props.batch) batches.push(todo.slice(i, i + props.batch));
    modal.confirm({
      title: `${props.title}: chạy ${todo.length} mục?`,
      width: 520,
      content: (
        <Space direction="vertical">
          <Typography.Text>
            Tốn <b>{fmtCost(todoCost, props.unit)}</b> trên {props.provider}, chia {batches.length} lượt.
          </Typography.Text>
          {skipped ? <Typography.Text type="secondary">{skipped} mục đã có kết quả sẽ được bỏ qua (bật “Làm lại” để chạy lại).</Typography.Text> : null}
          <Typography.Text type="secondary">Bấm Dừng thì lượt đang chạy vẫn hoàn tất, các lượt sau không chạy và không tốn tiền.</Typography.Text>
        </Space>
      ),
      okText: `Chạy (${fmtCost(todoCost, props.unit)})`,
      cancelText: 'Hủy',
      onOk: () => void runBatches(batches),
    });
  };

  const runBatches = async (batches: PaidItem[][]) => {
    stopRef.current = false;
    const p: Progressing = { done: 0, total: todo.length, used: 0, failed: [], stopping: false };
    setProg({ ...p });
    for (const b of batches) {
      if (stopRef.current) break;
      const cost = sumCost(b.map((i) => i.cost), props.unit);
      const pr: Record<string, string> = {};
      for (const i of b) if (prompts[i.key]?.trim()) pr[i.key] = prompts[i.key]!.trim();
      try {
        const r = await props.run(b.map((i) => i.key), cost, force, pr);
        p.used = sumCost([p.used, r.used], props.unit);
        p.failed.push(...r.failed);
      } catch (e) {
        p.failed.push(`${b.map((i) => i.key).join(', ')}: ${apiErrorMessage(e, 'lỗi')}`);
      }
      p.done += b.length;
      setProg({ ...p, stopping: stopRef.current });
    }
    setProg({ ...p, stopping: false });
    message[p.failed.length ? 'warning' : 'success'](
      `Đã chạy ${p.done}/${p.total} mục, dùng ${fmtCost(p.used, props.unit)}${p.failed.length ? `, lỗi ${p.failed.length}` : ''}.`,
    );
    await load();
    props.onChanged();
  };

  const active = !!prog && prog.done < prog.total;

  const openView = async (key: string) => {
    if (!props.view) return;
    const v = props.view(key);
    try {
      const { blob } = await fetchLinesheetBlob(v.path);
      setViewing({ url: URL.createObjectURL(blob), kind: v.kind, title: key });
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không mở được kết quả'));
    }
  };

  const doneCount = items?.filter((i) => i.has).length ?? 0;
  return (
    <StepCard
      n={props.n}
      title={props.title}
      done={!!items && items.length > 0 && doneCount === items.length}
      extra={
        <Space>
          {props.toolbar}
          <Button disabled={!!props.disabled || active} loading={loading} onClick={() => void load()}>
            Xem trước
          </Button>
          <Button type="primary" disabled={!items || !todo.length || active} onClick={start}>
            Chạy{items ? ` (${fmtCost(todoCost, props.unit)})` : ''}
          </Button>
        </Space>
      }
    >
      {props.disabled ? <Typography.Text type="secondary">{props.disabled}</Typography.Text> : null}
      {!props.disabled && !items ? <Typography.Text type="secondary">Bấm Xem trước để thấy mục nào cần chạy và tốn bao nhiêu. Xem trước không tốn tiền.</Typography.Text> : null}
      {items ? (
        <Space direction="vertical" style={{ width: '100%' }}>
          <Space wrap>
            {summary}
            <Tag color="success">Đã có {doneCount}/{items.length}</Tag>
            <Checkbox checked={force} onChange={(e) => setForce(e.target.checked)}>Làm lại mục đã có</Checkbox>
            <Button size="small" onClick={() => setSel(items.filter((i) => !i.has).map((i) => i.key))}>Chọn mục chưa có</Button>
            <Button size="small" onClick={() => setSel([])}>Bỏ chọn</Button>
          </Space>
          {notes.length ? <Alert type="info" showIcon message={notes.slice(0, 6).join(' · ')} /> : null}
          {prog ? (
            <Space style={{ width: '100%' }} align="center">
              <Progress style={{ minWidth: 260 }} percent={Math.round((prog.done / Math.max(prog.total, 1)) * 100)} size="small" status={active ? 'active' : prog.failed.length ? 'exception' : 'success'} />
              <Typography.Text type="secondary">
                {prog.done}/{prog.total} · đã dùng {fmtCost(prog.used, props.unit)}
              </Typography.Text>
              {active ? (
                <Button size="small" danger disabled={prog.stopping} onClick={() => { stopRef.current = true; setProg({ ...prog, stopping: true }); }}>
                  {prog.stopping ? 'Đang dừng…' : 'Dừng'}
                </Button>
              ) : null}
            </Space>
          ) : null}
          {prog?.failed.length ? <Alert type="error" showIcon message="Mục lỗi" description={prog.failed.slice(0, 10).join(' · ')} /> : null}
          <Table<PaidItem>
            rowKey="key"
            size="small"
            pagination={{ pageSize: 15, showSizeChanger: false }}
            dataSource={items}
            rowSelection={{ selectedRowKeys: sel, onChange: (k) => setSel(k as string[]), getCheckboxProps: () => ({ disabled: active }) }}
            columns={[
              { title: 'Mã', key: 'key', width: 200, render: (_: unknown, i: PaidItem) => <Typography.Text code style={{ whiteSpace: 'nowrap' }}>{i.key}</Typography.Text> },
              ...props.columns.map((c) => ({ title: c.title, key: c.key, width: c.width, render: (_: unknown, i: PaidItem) => i.cells[c.key] })),
              ...(props.editablePrompt
                ? [{
                    title: 'Câu lệnh (để trống = mặc định)',
                    key: 'prompt',
                    render: (_: unknown, i: PaidItem) => (
                      <Input.TextArea
                        autoSize={{ minRows: 1, maxRows: 4 }}
                        placeholder={i.prompt}
                        value={prompts[i.key] ?? ''}
                        onChange={(e) => setPrompts((p) => ({ ...p, [i.key]: e.target.value }))}
                      />
                    ),
                  }]
                : []),
              { title: 'Phí', key: 'cost', width: 100, render: (_: unknown, i: PaidItem) => fmtCost(i.cost, props.unit) },
              {
                title: '',
                key: 'st',
                width: 140,
                render: (_: unknown, i: PaidItem) => (
                  <Space size={4}>
                    {i.has ? <Tag color="green">Đã có</Tag> : <Tag color="orange">Chưa có</Tag>}
                    {i.has && props.view ? <Button size="small" onClick={() => void openView(i.key)}>Xem</Button> : null}
                  </Space>
                ),
              },
            ]}
          />
        </Space>
      ) : null}
      <Modal open={!!viewing} title={viewing?.title} footer={null} width={960} destroyOnClose onCancel={() => setViewing(null)}>
        {viewing?.kind === 'image' ? <img src={viewing.url} alt={viewing.title} style={{ width: '100%' }} /> : null}
        {viewing?.kind === 'video' ? <video src={viewing.url} controls autoPlay loop style={{ width: '100%', maxHeight: '70vh', background: '#000' }} /> : null}
      </Modal>
    </StepCard>
  );
}

type Settings = Record<string, unknown>;

export function KeyframeStep({ n, id, onChanged }: { n: number; id: string; onChanged: () => void }) {
  return (
    <PaidStep
      n={n}
      title="Ảnh cảnh (Runway, tốn credit)"
      unit="credit"
      provider="Runway"
      batch={1}
      columns={[
        { key: 'model', title: 'Mô hình', width: 120 },
        { key: 'refs', title: 'Ảnh mẫu gửi kèm', width: 200 },
        { key: 'prompt', title: 'Mô tả' },
      ]}
      load={async () => {
        const p = await previewLinesheetKeyframes(id);
        return {
          summary: <Typography.Text>{p.count} ảnh trong keyframe_specs · cần tạo {p.pendingCount} ({fmtCost(p.pendingCredits, 'credit')})</Typography.Text>,
          notes: ['Mỗi lượt một ảnh, không chạy hai lượt cùng lúc', 'Sau khi đổi ảnh, đo lại faces trên khung chuyển động'],
          items: p.keyframes.map((k) => ({
            key: k.name,
            cost: k.credits,
            has: k.has,
            cells: {
              model: `${k.model} · ${k.ratio}`,
              refs: k.refs.length ? k.refs.join(', ') : '—',
              prompt: <Typography.Paragraph ellipsis={{ rows: 2, expandable: true }} style={{ margin: 0 }}>{k.prompt}</Typography.Paragraph>,
            },
          })),
        };
      }}
      run={async (names, cost, force) => {
        const r = await generateLinesheetKeyframes(id, { names, confirmCredits: cost, force });
        return { generated: r.generated, used: r.creditsUsed, failed: r.failed };
      }}
      view={(name) => ({ path: linesheetPaths.keyframeImage(id, name), kind: 'image' })}
      onChanged={onChanged}
    />
  );
}

export function MotionStep({ n, id, disabled, onChanged }: { n: number; id: string; disabled?: string | null; onChanged: () => void }) {
  const { message } = App.useApp();
  const [model, setModel] = useState<string | null>(null);
  const [reload, setReload] = useState(0);
  const [switching, setSwitching] = useState(false);

  const switchModel = async (m: string) => {
    setSwitching(true);
    try {
      const s = (await getLinesheetSettings(id)) as Settings;
      await putLinesheetSettings(id, { ...s, motion_model: m });
      setModel(m);
      setReload((x) => x + 1);
      message.success(`Đã đổi mô hình chuyển động sang ${m}. Phí trong bảng đã tính lại.`);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không đổi được mô hình'));
    } finally {
      setSwitching(false);
    }
  };

  return (
    <PaidStep
      n={n}
      title="Chuyển động (Runway, tốn credit)"
      unit="credit"
      provider="Runway"
      batch={2}
      disabled={disabled}
      reloadSignal={reload}
      editablePrompt
      toolbar={
        model ? (
          <Select
            size="middle"
            style={{ width: 230 }}
            value={model}
            loading={switching}
            onChange={(m) => void switchModel(m)}
            options={[
              { value: 'gen4_turbo', label: 'gen4_turbo · 5 credit/giây' },
              { value: 'gen4.5', label: 'gen4.5 · 12 credit/giây' },
            ]}
          />
        ) : null
      }
      columns={[
        { key: 'scene', title: 'Cảnh · bối cảnh', width: 180 },
        { key: 'len', title: 'Dài', width: 90 },
      ]}
      load={async () => {
        const p = await previewLinesheetMotion(id);
        setModel(p.model);
        return {
          summary: <Typography.Text>{p.shotCount} shot · mô hình {p.model} · cần tạo {p.pendingCount} ({fmtCost(p.pendingCredits, 'credit')})</Typography.Text>,
          notes: [
            'Cảnh rộng nhiều khoảng trống (sân trường, công viên, bệnh viện): dùng gen4.5, turbo hay thêm người lạ',
            'Shot ngồi nghe thoại có thể dùng chung chuyển động qua motion_share trong settings để khỏi tốn credit',
          ],
          items: p.shots.map((x) => ({
            key: x.shotId,
            cost: x.credits,
            has: x.has,
            prompt: x.prompt,
            cells: { scene: `${x.scene} · ${x.setting}`, len: `${(x.durationMs / 1000).toFixed(1)}s → ${x.clipSeconds}s` },
          })),
        };
      }}
      run={async (shotIds, cost, force, prompts) => {
        const r = await generateLinesheetMotion(id, { shotIds, confirmCredits: cost, force, prompts: Object.keys(prompts).length ? prompts : undefined });
        return { generated: r.generated, used: r.creditsUsed, failed: r.failed };
      }}
      view={(shotId) => ({ path: linesheetPaths.motionClip(id, shotId), kind: 'video' })}
      onChanged={onChanged}
    />
  );
}

export function LipsyncStep({ n, id, disabled, onChanged }: { n: number; id: string; disabled?: string | null; onChanged: () => void }) {
  return (
    <PaidStep
      n={n}
      title="Ghép môi (fal, tốn USD)"
      unit="usd"
      provider="fal"
      batch={4}
      disabled={disabled}
      columns={[
        { key: 'shot', title: 'Shot', width: 110 },
        { key: 'speaker', title: 'Người nói', width: 110 },
        { key: 'sec', title: 'Giây', width: 70 },
      ]}
      load={async () => {
        const p = await previewLinesheetLipsync(id);
        return {
          summary: <Typography.Text>{p.lineCount} câu quay mặt · mô hình {p.model} · cần ghép {p.pendingCount} ({fmtCost(p.pendingUsd, 'usd')})</Typography.Text>,
          notes: [...p.notes, 'Kéo dài shot sẽ làm mất ghép môi của shot đó; ghép môi sau khi chốt bảng shot'],
          items: p.lines.map((l) => ({
            key: l.rowId,
            cost: l.usd,
            has: l.has,
            cells: { shot: l.shotId, speaker: l.speaker, sec: l.seconds.toFixed(1) },
          })),
        };
      }}
      run={async (rowIds, cost, force) => {
        const r = await generateLinesheetLipsync(id, { rowIds, confirmUsd: cost, force });
        return { generated: r.generated, used: r.usdUsed, failed: r.failed };
      }}
      onChanged={onChanged}
    />
  );
}

type OutroForm = {
  title_logo?: string;
  enabled: boolean;
  end_title?: string;
  series_line?: string;
  follow_line?: string;
  next_label?: string;
  next_title?: string;
  next_note?: string;
  logo?: string;
  sting_cue?: string;
  music_cue?: string;
  music_start_sec?: number;
  title_seconds?: number;
  card_seconds?: number;
};

/** Thẻ tên phim (logo trên tiêu đề) và phần kết tập: sửa thẳng trong settings, không tốn tiền. */
export function OutroStep({ n, id, onChanged }: { n: number; id: string; onChanged: () => void }) {
  const { message } = App.useApp();
  const [form] = Form.useForm<OutroForm>();
  const [loaded, setLoaded] = useState(false);
  const [saving, setSaving] = useState(false);
  const [enabled, setEnabled] = useState(false);

  const load = async () => {
    try {
      const s = (await getLinesheetSettings(id)) as Settings;
      const o = (s.outro ?? {}) as Partial<OutroForm>;
      form.setFieldsValue({ title_logo: (s.title_logo as string) ?? '', enabled: !!o.enabled, ...o });
      setEnabled(!!o.enabled);
      setLoaded(true);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không đọc được settings'));
    }
  };

  useEffect(() => {
    setLoaded(false);
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const save = async () => {
    const v = await form.validateFields();
    setSaving(true);
    try {
      const s = (await getLinesheetSettings(id)) as Settings;
      const { title_logo, ...outro } = v;
      const prev = (s.outro ?? {}) as Settings;
      await putLinesheetSettings(id, { ...s, title_logo: title_logo?.trim() || null, outro: { ...prev, ...outro } });
      setEnabled(v.enabled);
      message.success('Đã lưu phần mở và kết. Dựng lại cả tập để thấy thay đổi.');
      onChanged();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không lưu được'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <StepCard
      n={n}
      title="Thẻ tên phim và phần kết tập"
      done={loaded && enabled}
      extra={<Button type="primary" disabled={!loaded} loading={saving} onClick={() => void save()}>Lưu</Button>}
    >
      <Form form={form} layout="vertical" disabled={!loaded}>
        <Space wrap align="start">
          <Form.Item name="title_logo" label="Logo trên thẻ tên phim (tên ảnh refs/)" style={{ width: 280 }}>
            <Input placeholder="famixa_logo_nen_toi" />
          </Form.Item>
          <Form.Item name="enabled" label="Có phần kết tập" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Space>
        <Space wrap align="start">
          <Form.Item name="end_title" label="Dòng hết tập" style={{ width: 200 }}><Input placeholder="HẾT TẬP 1" /></Form.Item>
          <Form.Item name="series_line" label="Dòng tên phim" style={{ width: 320 }}><Input placeholder="LỆCH TÔNG · Chuyện năm mười tám tuổi" /></Form.Item>
          <Form.Item name="follow_line" label="Lời mời theo dõi" style={{ width: 300 }}><Input placeholder="Theo dõi để không lỡ tập mới" /></Form.Item>
        </Space>
        <Space wrap align="start">
          <Form.Item name="next_label" label="Nhãn tập sau" style={{ width: 140 }}><Input placeholder="TẬP 2" /></Form.Item>
          <Form.Item name="next_title" label="Tên tập sau" style={{ width: 240 }}><Input placeholder="Tiền tài liệu" /></Form.Item>
          <Form.Item name="next_note" label="Ghi chú" style={{ width: 180 }}><Input placeholder="Sắp ra mắt" /></Form.Item>
          <Form.Item name="logo" label="Logo phần kết (refs/)" style={{ width: 220 }}><Input placeholder="để trống = logo thẻ tên" /></Form.Item>
        </Space>
        <Space wrap align="start">
          <Form.Item name="sting_cue" label="Tiếng chốt (cue)" style={{ width: 160 }}><Input placeholder="end_sting" /></Form.Item>
          <Form.Item name="music_cue" label="Nhạc nền (cue)" style={{ width: 220 }}><Input placeholder="song_den_duong_verse" /></Form.Item>
          <Form.Item name="music_start_sec" label="Nhạc bắt đầu từ giây"><InputNumber min={0} step={0.5} /></Form.Item>
          <Form.Item name="title_seconds" label="Giây chữ hết tập"><InputNumber min={1} max={10} /></Form.Item>
          <Form.Item name="card_seconds" label="Giây thẻ cuối"><InputNumber min={5} max={20} /></Form.Item>
        </Space>
        <Typography.Paragraph type="secondary" style={{ margin: 0 }}>
          Thẻ cuối 5–20 giây để gắn màn hình kết thúc của YouTube. Phần kết chỉ thêm khi dựng cả tập (không đặt khoảng thời gian).
        </Typography.Paragraph>
      </Form>
    </StepCard>
  );
}

const QA_MANUAL = [
  'Mặt nhân vật giống nhau giữa các cảnh (tóc, áo, kính)',
  'Cảnh rộng không có người lạ, không có người nhân đôi',
  'Đồ vật liền mạch (hộp đàn, hộp cơm, phong bì) giữa các shot',
  'Môi khớp lời ở các câu quay mặt; không có miệng mấp máy khi im',
  'Mặt không quá tối, nhất là cảnh đêm',
  'Chữ trên màn hình đọc kịp, không bị che',
  'Thẻ tên phim có logo; phần kết đúng tên tập sau',
  'Nghe cả tập bằng tai nghe: không vấp, không rè, nhạc không lấn lời',
];

type QaCheck = { label: string; ok: boolean; detail?: string };

/** Soát trước khi đăng: phần máy kiểm được + danh sách xem bằng mắt (đánh dấu lưu trên trình duyệt này). */
export function QaStep({ n, id, status }: { n: number; id: string; status: LinesheetStatus }) {
  const { message } = App.useApp();
  const storeKey = `famixa-qa:${id}`;
  const [manual, setManual] = useState<boolean[]>(() => {
    try {
      const v = JSON.parse(localStorage.getItem(storeKey) ?? '[]') as boolean[];
      return QA_MANUAL.map((_, i) => !!v[i]);
    } catch {
      return QA_MANUAL.map(() => false);
    }
  });
  const [auto, setAuto] = useState<QaCheck[] | null>(null);
  const [checking, setChecking] = useState(false);

  useEffect(() => {
    try {
      const v = JSON.parse(localStorage.getItem(storeKey) ?? '[]') as boolean[];
      setManual(QA_MANUAL.map((_, i) => !!v[i]));
    } catch {
      setManual(QA_MANUAL.map(() => false));
    }
    setAuto(null);
  }, [storeKey]);

  const tick = (i: number, v: boolean) => {
    const next = manual.map((x, j) => (j === i ? v : x));
    setManual(next);
    try {
      localStorage.setItem(storeKey, JSON.stringify(next));
    } catch {
      /* trình duyệt chặn lưu: vẫn dùng được trong phiên */
    }
  };

  const check = async () => {
    setChecking(true);
    const out: QaCheck[] = [];
    const s = status;
    out.push({ label: 'Đủ giọng, nhạc, ảnh', ok: !s.missingAudio.length && !s.missingCues.length && !s.missingLayers.length, detail: `thiếu ${s.missingAudio.length} giọng, ${s.missingCues.length} nhạc, ${s.missingLayers.length} ảnh` });
    out.push({ label: 'Không còn câu ước lượng thời lượng', ok: s.timelineTotalMs != null && s.timelineEstimatedCount === 0, detail: s.timelineTotalMs == null ? 'chưa tính dòng thời gian' : `${s.timelineEstimatedCount} câu ước lượng` });
    out.push({ label: 'Cấu hình khớp kịch bản', ok: s.settingsReady && !s.settingsErrors.length, detail: s.settingsErrors.slice(0, 2).join(' · ') || undefined });
    const tries: [string, () => Promise<QaCheck>][] = [
      ['Ảnh cảnh', async () => { const p = await previewLinesheetKeyframes(id); return { label: 'Đủ ảnh cảnh', ok: p.pendingCount === 0, detail: `${p.pendingCount}/${p.count} chưa có` }; }],
      ['Chuyển động', async () => { const p = await previewLinesheetMotion(id); return { label: 'Đủ chuyển động', ok: p.pendingCount === 0, detail: `${p.pendingCount}/${p.shotCount} shot chưa có (${fmtCost(p.pendingCredits, 'credit')})` }; }],
      ['Ghép môi', async () => { const p = await previewLinesheetLipsync(id); return { label: 'Đủ ghép môi', ok: p.pendingCount === 0, detail: `${p.pendingCount}/${p.lineCount} câu chưa có (${fmtCost(p.pendingUsd, 'usd')})` }; }],
      ['Phần kết', async () => { const st = (await getLinesheetSettings(id)) as Settings; const o = (st.outro ?? {}) as { enabled?: boolean }; return { label: 'Có thẻ tên phim và phần kết', ok: !!st.title_logo && !!o.enabled, detail: `${st.title_logo ? 'có' : 'chưa có'} logo, phần kết ${o.enabled ? 'bật' : 'tắt'}` }; }],
      ['Bản dựng', async () => {
        const runs: LinesheetRun[] = await listLinesheetRuns(id);
        const full = runs.find((r) => r.status === 'SUCCEEDED' && r.rangeStartMs === 0 && r.width >= 1920 && (s.timelineTotalMs == null || r.rangeEndMs >= s.timelineTotalMs));
        return { label: 'Có bản dựng cả tập 1920×1080', ok: !!full, detail: full ? `xong ${new Date(full.completedAt ?? full.createdAt).toLocaleString('vi-VN')}` : 'chưa có' };
      }],
    ];
    for (const [name, fn] of tries) {
      try {
        out.push(await fn());
      } catch (e) {
        out.push({ label: name, ok: false, detail: apiErrorMessage(e, 'không kiểm được') });
      }
    }
    setAuto(out);
    setChecking(false);
    if (out.every((x) => x.ok)) message.success('Phần máy kiểm đã đạt. Còn danh sách xem bằng mắt.');
  };

  const manualDone = manual.filter(Boolean).length;
  const allOk = !!auto && auto.every((x) => x.ok) && manualDone === QA_MANUAL.length;
  return (
    <StepCard n={n} title="Soát trước khi đăng" done={allOk} extra={<Button loading={checking} onClick={() => void check()}>Kiểm tra</Button>}>
      <Space direction="vertical" style={{ width: '100%' }}>
        {auto ? (
          <Space direction="vertical" size={2}>
            {auto.map((c) => (
              <Space key={c.label}>
                <Tag color={c.ok ? 'success' : 'error'}>{c.ok ? 'Đạt' : 'Chưa'}</Tag>
                <Typography.Text>{c.label}</Typography.Text>
                {c.detail ? <Typography.Text type="secondary">{c.detail}</Typography.Text> : null}
              </Space>
            ))}
          </Space>
        ) : (
          <Typography.Text type="secondary">Bấm Kiểm tra để máy soát file, dòng thời gian, ảnh cảnh, chuyển động, ghép môi, phần kết và bản dựng cuối. Không tốn tiền.</Typography.Text>
        )}
        <Typography.Text strong>Xem bằng mắt ({manualDone}/{QA_MANUAL.length})</Typography.Text>
        {QA_MANUAL.map((t, i) => (
          <Checkbox key={t} checked={manual[i]} onChange={(e) => tick(i, e.target.checked)}>{t}</Checkbox>
        ))}
      </Space>
    </StepCard>
  );
}
