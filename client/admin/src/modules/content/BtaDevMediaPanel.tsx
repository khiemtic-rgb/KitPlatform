import { useCallback, useEffect, useState } from 'react';
import { Alert, Button, Card, Checkbox, Col, Descriptions, Row, Segmented, Space, Table, Tag, Typography } from 'antd';
import { http } from '@/shared/api/http';
import { apiErrorMessage } from '@/shared/api/api-error';

/**
 * Tab "BTA" trong AI dựng video. Chỉ hiện khi Vite chạy Development.
 * API cũng từ chối nếu không phải Development, vì ảnh và mp3 ghi vào repo trên máy chủ kênh.
 * Khóa: khóa sản xuất Kit MKT (khóa Famixa) của tenant đang đăng nhập. Màn này không nhận khóa.
 */

type Lot = 'A' | 'B' | 'C';

/** Mỗi tập: file lệnh ảnh, file lời đọc v4 và các lô ảnh. Ảnh ghi vào thư mục của file lệnh (hinh-ai/01, hinh-ai/02…). */
const EPISODES = {
  '01': {
    title: "Tập 01 · Vietnam's Zodiac Cat",
    image: 'docs/marketing/bta/hinh-ai/01/lenh-tao-anh.json',
    voice: 'docs/marketing/bta/kich-ban/01-vietnam-zodiac-cat.giong-v4.txt',
    lots: ['A', 'B', 'C'] as Lot[],
  },
  '02': {
    title: 'Tập 02 · Lunar New Year, the Vietnamese Way',
    image: 'docs/marketing/bta/hinh-ai/02/lenh-tao-anh.json',
    voice: 'docs/marketing/bta/kich-ban/02-tet-vietnamese-way.giong-v4.txt',
    lots: ['A', 'B'] as Lot[],
  },
  '03': {
    title: "Tập 03 · Vietnam's Latin Alphabet",
    image: 'docs/marketing/bta/hinh-ai/03/lenh-tao-anh.json',
    voice: 'docs/marketing/bta/kich-ban/03-latin-alphabet.giong-v4.txt',
    lots: ['A', 'B'] as Lot[],
  },
} as const;
type EpisodeCode = keyof typeof EPISODES;
type Episode = (typeof EPISODES)[EpisodeCode];

export function btaMediaTabEnabled() {
  return import.meta.env.DEV;
}

export function BtaDevMediaPanel() {
  const [code, setCode] = useState<EpisodeCode>('03');
  if (!btaMediaTabEnabled()) return null;
  const episode = EPISODES[code];
  return (
    <div>
      <Space style={{ marginBottom: 12 }} wrap>
        <Typography.Text strong>Tập</Typography.Text>
        <Segmented
          value={code}
          onChange={(value) => setCode(value as EpisodeCode)}
          options={(Object.keys(EPISODES) as EpisodeCode[]).map((x) => ({ value: x, label: EPISODES[x].title }))}
        />
      </Space>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={`BTA ${episode.title} · tạo ảnh và giọng qua API`}
        description={
          <>
            Đăng nhập tenant <Typography.Text strong>FAMIXA_PRODUCTION</Typography.Text>: BTA dùng chung khóa Runway và
            ElevenLabs sản xuất của tenant này (cùng khóa Famixa đang chạy). Không dán khóa ở đây. File ghi vào <Typography.Text code>docs/marketing/bta</Typography.Text> trên máy chủ
            kênh. Mỗi lần gửi tốn credit đều phải tích xác nhận; trần credit lấy từ
            <Typography.Text code>tran_credit</Typography.Text> trong file lệnh.
          </>
        }
      />
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <ImageBatch key={`img-${code}`} episode={episode} />
        </Col>
        <Col xs={24} xl={12}>
          <VoiceBatch key={`voice-${code}`} episode={episode} />
        </Col>
      </Row>
    </div>
  );
}

/* ---------------- Ảnh ---------------- */

type ImageItem = {
  file: string;
  promptText: string;
  ratio: string;
  model: string;
  seed: number | null;
  creditUocTinh: number;
  daCo: boolean;
  daGui: boolean;
};

type ImagePlan = {
  lo: string;
  tongCredit: number;
  creditDaDung: number;
  creditConLai: number;
  anh: ImageItem[];
};

type RunState = { tone: 'processing' | 'success' | 'error' | 'default'; text: string };

function ImageBatch({ episode }: { episode: Episode }) {
  const imageLenh = episode.image;
  const [lo, setLo] = useState<Lot>(episode.lots[0]);
  const [plan, setPlan] = useState<ImagePlan | null>(null);
  const [planError, setPlanError] = useState('');
  const [ok, setOk] = useState(false);
  const [loading, setLoading] = useState(false);
  const [running, setRunning] = useState(false);
  const [runs, setRuns] = useState<Record<string, RunState>>({});

  const load = useCallback(async (batch: Lot) => {
    setLoading(true);
    setPlanError('');
    setOk(false);
    try {
      const { data } = await http.post<ImagePlan>('/content/media/bta-images/plan', { lenh: imageLenh, lo: batch });
      setPlan(data);
    } catch (e) {
      setPlan(null);
      setPlanError(apiErrorMessage(e, 'Không đọc được lệnh tạo ảnh'));
    } finally {
      setLoading(false);
    }
  }, [imageLenh]);

  useEffect(() => {
    void load(lo);
  }, [lo, load]);

  function mark(file: string, state: RunState) {
    setRuns((prev) => ({ ...prev, [file]: state }));
  }

  async function run() {
    if (!plan || !ok) return;
    setRunning(true);
    try {
      for (const item of plan.anh) {
        if (item.daCo || item.daGui) continue;
        try {
          mark(item.file, { tone: 'processing', text: 'Đang gửi' });
          const created = await http.post<{ taskId: string; creditUocTinh: number }>(
            '/content/media/text-to-image',
            { promptText: item.promptText, ratio: item.ratio, model: item.model, seed: item.seed, file: item.file, lenh: imageLenh },
            { timeout: 120000 },
          );
          mark(item.file, { tone: 'processing', text: 'Runway đang vẽ' });
          const task = await pollTask(created.data.taskId);
          const loi = [task.failureCode, task.failure].filter(Boolean).join(' ').trim();
          const saved = await http.post<{ ketQua: string; loi?: string | null }>(
            '/content/media/bta-images/save',
            {
              file: item.file,
              taskId: created.data.taskId,
              outputUrl: task.outputUrl,
              creditUocTinh: item.creditUocTinh,
              model: item.model,
              ratio: item.ratio,
              ketQua: task.status,
              loi: loi || null,
              lenh: imageLenh,
            },
            { timeout: 120000 },
          );
          const good = saved.data.ketQua === 'SUCCEEDED';
          mark(item.file, {
            tone: good ? 'success' : 'error',
            text: good ? 'Đã lưu' : `${saved.data.ketQua}${saved.data.loi ? ': ' + saved.data.loi : ''}`,
          });
        } catch (e) {
          mark(item.file, { tone: 'error', text: apiErrorMessage(e, 'Lỗi') });
        }
      }
    } finally {
      setRunning(false);
      void load(lo);
    }
  }

  const overCap = plan ? plan.tongCredit > plan.creditConLai : false;
  const cap = plan ? plan.creditDaDung + plan.creditConLai : 0;

  return (
    <Card
      size="small"
      title="1. Ảnh minh hoạ · Runway gen4_image"
      extra={
        <Segmented
          value={lo}
          disabled={running}
          onChange={(value) => {
            setLo(value as Lot);
            setRuns({});
          }}
          options={episode.lots.map((x) => ({ value: x, label: `Lô ${x}` }))}
        />
      }
    >
      {planError ? <Alert type="error" showIcon style={{ marginBottom: 12 }} message={planError} /> : null}
      {plan ? (
        <Descriptions size="small" column={3} style={{ marginBottom: 12 }}>
          <Descriptions.Item label="Lô này">{plan.tongCredit} credit</Descriptions.Item>
          <Descriptions.Item label="Đã dùng">
            {plan.creditDaDung}/{cap}
          </Descriptions.Item>
          <Descriptions.Item label="Còn">{plan.creditConLai}</Descriptions.Item>
        </Descriptions>
      ) : null}
      <Table<ImageItem>
        size="small"
        rowKey="file"
        loading={loading}
        pagination={false}
        dataSource={plan?.anh ?? []}
        columns={[
          {
            title: 'File',
            dataIndex: 'file',
            render: (file: string, item) => (
              <Typography.Text title={item.promptText} style={{ fontSize: 12 }}>
                {file}
              </Typography.Text>
            ),
          },
          { title: 'Credit', dataIndex: 'creditUocTinh', width: 64, align: 'right' },
          {
            title: 'Trạng thái',
            width: 180,
            render: (_, item) => {
              const live = runs[item.file];
              if (live) return <Tag color={live.tone}>{live.text}</Tag>;
              if (item.daCo) return <Tag color="success">Đã có file</Tag>;
              if (item.daGui) return <Tag>Đã gửi trước đó</Tag>;
              return <Tag color="blue">Sẽ gửi</Tag>;
            },
          },
        ]}
      />
      {plan ? (
        <Space direction="vertical" style={{ width: '100%', marginTop: 12 }}>
          {overCap ? (
            <Alert type="warning" showIcon message={`Lô này vượt trần ${cap} credit, không gửi được.`} />
          ) : null}
          <Checkbox checked={ok} disabled={running || plan.tongCredit <= 0} onChange={(e) => setOk(e.target.checked)}>
            Tôi duyệt gửi lô {plan.lo}, ước tính {plan.tongCredit} credit. Ảnh lỗi không tự chạy lại.
          </Checkbox>
          <Button
            type="primary"
            htmlType="button"
            disabled={!ok || plan.tongCredit <= 0 || overCap}
            loading={running}
            onClick={() => void run()}
          >
            {plan.tongCredit > 0 ? `Tạo ${plan.anh.filter((x) => !x.daCo && !x.daGui).length} ảnh lô ${plan.lo}` : 'Lô này đã xong'}
          </Button>
        </Space>
      ) : null}
    </Card>
  );
}

type TaskBody = { status: string; outputUrl?: string | null; failureCode?: string | null; failure?: string | null };

async function pollTask(taskId: string): Promise<TaskBody> {
  for (let i = 0; i < 120; i++) {
    const { data } = await http.get<TaskBody>(`/content/media/tasks/${encodeURIComponent(taskId)}`, { timeout: 60000 });
    if (data.status === 'SUCCEEDED' || data.status === 'FAILED' || data.status === 'CANCELLED') return data;
    await new Promise((resolve) => setTimeout(resolve, 5000));
  }
  return { status: 'TIMEOUT', failure: 'quá 10 phút' };
}

/* ---------------- Giọng ---------------- */

type VoicePreview = { text: string; soKyTu: number; creditUocTinh: number; ghiChu: string };
type PartPlan = { soKyTu: number; creditUocTinh: number; ghiChu: string; doanDau: string };
type Take = {
  fileMp3: string;
  fileTiming: string;
  modelId: string;
  voiceId?: string;
  requestId?: string | null;
  creditUocTinh: number;
};

function TakeResult({ take }: { take: Take }) {
  return (
    <Descriptions size="small" column={1} bordered style={{ marginTop: 8 }}>
      <Descriptions.Item label="Model">{take.modelId}</Descriptions.Item>
      <Descriptions.Item label="MP3">
        <Typography.Text code copyable>
          {take.fileMp3}
        </Typography.Text>
      </Descriptions.Item>
      <Descriptions.Item label="Mốc thời gian">
        <Typography.Text code copyable>
          {take.fileTiming}
        </Typography.Text>
      </Descriptions.Item>
      {take.requestId ? <Descriptions.Item label="Request">{take.requestId}</Descriptions.Item> : null}
      <Descriptions.Item label="Ước tính">{take.creditUocTinh} ký tự</Descriptions.Item>
    </Descriptions>
  );
}

function VoiceBatch({ episode }: { episode: Episode }) {
  const voiceLenh = episode.voice;
  const [probe, setProbe] = useState<VoicePreview | null>(null);
  const [probeOk, setProbeOk] = useState(false);
  const [probeBusy, setProbeBusy] = useState(false);
  const [probeTake, setProbeTake] = useState<Take | null>(null);
  const [probeError, setProbeError] = useState('');

  const [phan, setPhan] = useState<1 | 2 | 3>(1);
  const [part, setPart] = useState<PartPlan | null>(null);
  const [partOk, setPartOk] = useState(false);
  const [partBusy, setPartBusy] = useState(false);
  const [partTake, setPartTake] = useState<Take | null>(null);
  const [partError, setPartError] = useState('');

  useEffect(() => {
    http
      .get<VoicePreview>('/content/media/bta-voice/cau-thu')
      .then(({ data }) => setProbe(data))
      .catch((e) => setProbeError(apiErrorMessage(e, 'Không đọc được câu thử')));
  }, []);

  useEffect(() => {
    setPart(null);
    setPartOk(false);
    setPartTake(null);
    setPartError('');
    http
      .post<PartPlan>('/content/media/bta-voice/plan', { lenh: voiceLenh, phan })
      .then(({ data }) => setPart(data))
      .catch((e) => setPartError(apiErrorMessage(e, 'Không đọc được phần')));
  }, [phan, voiceLenh]);

  async function recordProbe() {
    if (!probeOk) return;
    setProbeBusy(true);
    setProbeError('');
    try {
      const { data } = await http.post<Take>('/content/media/bta-voice/cau-thu', { xacNhan: true }, { timeout: 180000 });
      setProbeTake(data);
      setProbeOk(false);
    } catch (e) {
      setProbeError(apiErrorMessage(e, 'Không thu được câu thử'));
    } finally {
      setProbeBusy(false);
    }
  }

  async function recordPart() {
    if (!partOk) return;
    setPartBusy(true);
    setPartError('');
    try {
      const { data } = await http.post<Take>(
        '/content/media/bta-voice/phan',
        { lenh: voiceLenh, phan, xacNhan: `THU-PHAN-${phan}` },
        { timeout: 300000 },
      );
      setPartTake(data);
      setPartOk(false);
    } catch (e) {
      setPartError(apiErrorMessage(e, 'Không thu được phần'));
    } finally {
      setPartBusy(false);
    }
  }

  const busy = probeBusy || partBusy;

  return (
    <Card size="small" title="2. Giọng đọc · ElevenLabs v4 (BTA_Narrator)">
      <Typography.Text strong>Bước 1 · Câu thử</Typography.Text>
      <Typography.Paragraph type="secondary" style={{ margin: '4px 0 8px' }}>
        Thu một câu ngắn để kiểm tra khóa, model và giọng trước khi thu cả phần.
      </Typography.Paragraph>
      {probe ? (
        <Typography.Paragraph style={{ marginBottom: 8 }}>
          <Typography.Text italic>“{probe.text}”</Typography.Text>{' '}
          <Typography.Text type="secondary">
            · {probe.soKyTu} ký tự ({probe.ghiChu})
          </Typography.Text>
        </Typography.Paragraph>
      ) : null}
      <Space wrap>
        <Checkbox checked={probeOk} disabled={!probe || busy} onChange={(e) => setProbeOk(e.target.checked)}>
          Tôi duyệt thu câu thử này
        </Checkbox>
        <Button type="primary" htmlType="button" disabled={!probeOk} loading={probeBusy} onClick={() => void recordProbe()}>
          Thu câu thử
        </Button>
      </Space>
      {probeError ? <Alert type="error" showIcon style={{ marginTop: 8 }} message={probeError} /> : null}
      {probeTake ? <TakeResult take={probeTake} /> : null}

      <div style={{ borderTop: '1px solid rgba(0,0,0,0.06)', margin: '16px 0 12px' }} />

      <Space style={{ width: '100%', justifyContent: 'space-between' }} wrap>
        <Typography.Text strong>Bước 2 · Thu cả phần</Typography.Text>
        <Segmented
          value={phan}
          disabled={busy}
          onChange={(value) => setPhan(value as 1 | 2 | 3)}
          options={[
            { value: 1, label: 'Phần 1' },
            { value: 2, label: 'Phần 2' },
            { value: 3, label: 'Phần 3' },
          ]}
        />
      </Space>
      {!probeTake ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginTop: 8 }}
          message="Nên thu và nghe câu thử trước. Một phần dài khoảng 1–2 nghìn ký tự."
        />
      ) : null}
      {part ? (
        <>
          <Typography.Paragraph type="secondary" style={{ margin: '8px 0 4px' }}>
            {part.soKyTu} ký tự · {part.ghiChu}
          </Typography.Paragraph>
          <Typography.Paragraph
            style={{ background: 'rgba(0,0,0,0.02)', padding: 8, borderRadius: 6, fontSize: 12, marginBottom: 8 }}
          >
            {part.doanDau}…
          </Typography.Paragraph>
          <Space wrap>
            <Checkbox checked={partOk} disabled={busy} onChange={(e) => setPartOk(e.target.checked)}>
              Tôi duyệt thu phần {phan}, khoảng {part.creditUocTinh} ký tự
            </Checkbox>
            <Button danger htmlType="button" disabled={!partOk} loading={partBusy} onClick={() => void recordPart()}>
              Thu phần {phan}
            </Button>
          </Space>
        </>
      ) : null}
      {partError ? <Alert type="error" showIcon style={{ marginTop: 8 }} message={partError} /> : null}
      {partTake ? <TakeResult take={partTake} /> : null}
    </Card>
  );
}
