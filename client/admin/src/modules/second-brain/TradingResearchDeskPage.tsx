import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Card, Collapse, Descriptions, Form, InputNumber, Select, Space, Table, Tabs, Tag, Typography } from 'antd';
import { useAuthStore } from '@/shared/auth/auth.store';
import { CandleChart, DistributionChart, EquityChart } from './ResearchWorkbenchCharts';
import { tradingResearchApi, type ResearchDocument, type ResearchTask } from './trading-research.api';
import {
  candleChart,
  closeReasonLabel,
  comparisonRows,
  equityCurve,
  explainRunDetail,
  formatAmount,
  localTime,
  metricDisplay,
  pnlDistribution,
  researchConclusion,
  runStatusLabel,
  sampleLines,
  shortKey,
  sourcePresentation,
  tradeStatusLabel,
  utcTime,
} from './research-workbench';

function TimeText({ value }: { value: string | null | undefined }) {
  return <span title={utcTime(value)}>{localTime(value)}</span>;
}

function money(value: ResearchDocument['overview']['realizedPnl'] | undefined, closedTrades?: number) {
  return metricDisplay(value, closedTrades).text;
}

const presets = [
  { value: 'sma-cross-v1|fast=5,slow=20', label: 'SMA Cross 5/20' },
  { value: 'sma-cross-v1|fast=8,slow=21', label: 'SMA Cross 8/21' },
  { value: 'ema-cross-v1|fast=12,slow=26', label: 'EMA Cross 12/26' },
  { value: 'rsi-reversion-v1|entry=30,exit=50,period=14', label: 'RSI hồi quy 14' },
  { value: 'breakout-v1|lookback=20', label: 'Breakout 20' },
  { value: 'breakout-v1|lookback=10', label: 'Breakout 10' },
];

const initialConfig = {
  symbol: 'BTCUSDT',
  timeframe: '1h',
  preset: presets[0].value,
  dataSource: 'fixture' as const,
  quantity: 0.001,
  confidence: 0.8,
  slippageBps: 10,
  feeRate: 0.001,
  maxNotional: 1000,
  maxLeverage: 1,
  minConfidence: 0.5,
  sessionNotional: 5000,
};

export function TradingResearchDeskPage() {
  const canRun = (useAuthStore((s) => s.user?.roles ?? []).includes('ADMIN'))
    || (useAuthStore((s) => s.user?.permissions ?? []).includes('personal_intelligence.trading.run'));
  const [tasks, setTasks] = useState<ResearchTask[]>([]);
  const [current, setCurrent] = useState<ResearchTask | null>(null);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [executeEnabled, setExecuteEnabled] = useState<boolean | null>(null);
  const [engineVersion, setEngineVersion] = useState('research-engine-v2');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [tab, setTab] = useState('performance');
  const [form] = Form.useForm();
  const dataSource = Form.useWatch('dataSource', form) as string | undefined;

  const load = () => {
    setLoading(true);
    tradingResearchApi.desk()
      .then((desk) => {
        setExecuteEnabled(desk.executeEnabled);
        setEngineVersion(desk.engineVersion || 'research-engine-v2');
        setTasks(desk.tasks);
        setCurrent((existing) => existing ?? desk.tasks[0] ?? null);
        setError(null);
      })
      .catch((err: { response?: { status?: number } }) => {
        const status = err.response?.status;
        if (status === 401) setError('Phiên đăng nhập không hợp lệ với research desk. Hãy đăng nhập lại.');
        else if (status === 403) setError('Tài khoản không có quyền chạy nghiên cứu.');
        else if (status === 503) setError('Research desk chưa được cấu hình xác thực. Không chạy nghiên cứu.');
        else setError('Không kết nối được Trading Research desk trong Second Brain.');
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, []);

  const runValues = (values: Record<string, unknown>) => {
    const preset = String(values.preset || presets[0].value);
    const split = preset.indexOf('|');
    return {
      ...values,
      strategy: split < 0 ? preset : preset.slice(0, split),
      strategyParams: split < 0 ? '' : preset.slice(split + 1),
    };
  };

  const submit = async () => {
    const values = await form.validateFields();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const task = await tradingResearchApi.run(runValues(values));
      setCurrent(task);
      setTasks((rows) => [task, ...rows.filter((row) => row.taskId !== task.taskId)]);
      setTab('performance');
      setNotice(task.replayed ? 'Replay xong. Không tạo paper fill mới.' : 'Đã chạy nghiên cứu và ghi nhận kết quả.');
    } catch (err) {
      const message = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(message || 'Nhiệm vụ research không chạy được.');
    } finally {
      setBusy(false);
    }
  };

  const runPlan = async () => {
    const values = await form.validateFields();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const result = await tradingResearchApi.batch(runValues(values));
      const rows = result.tasks ?? [];
      setTasks((existing) => [...rows].reverse().reduce((all, task) => [task, ...all.filter((row) => row.taskId !== task.taskId)], existing));
      setCurrent(rows[rows.length - 1] ?? rows[0] ?? null);
      setTab('history');
      setNotice(`Đã chạy bộ ${rows.length} chiến lược mặc định. Mỗi cấu hình có mã riêng.`);
    } catch (err) {
      const message = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(message || 'Bộ thí nghiệm không chạy được.');
    } finally {
      setBusy(false);
    }
  };

  const document = current?.document;
  const source = sourcePresentation(current?.dataSource || document?.source || dataSource);
  const closedTrades = document?.performance?.closedTrades;
  const candles = useMemo(() => document ? candleChart(document.candles, document.signals) : null, [document]);
  const equity = useMemo(() => document ? equityCurve(document.trades ?? []) : null, [document]);
  const distribution = useMemo(() => document ? pnlDistribution(document.trades ?? []) : null, [document]);
  const conclusion = researchConclusion({
    status: current?.status,
    dataSource: current?.dataSource || document?.source,
    replayed: current?.replayed,
    newFills: current?.newFills,
    parameterKey: current?.parameterKey,
    config: current?.config,
    closedTrades,
    netKnown: document?.performance?.netPnl.known,
    openPositions: document?.positions?.length ?? 0,
  });
  const compared = comparisonRows(tasks.filter((task) => selectedIds.includes(task.taskId)));
  const formSource = sourcePresentation(dataSource || 'fixture');

  return (
    <Space direction="vertical" size={16} style={{ width: '100%', maxWidth: '100%' }}>
      <div id="research-config">
        <Typography.Title level={3} style={{ margin: 0 }}>Trading Agent</Typography.Title>
        <Space wrap style={{ marginTop: 8 }}>
          <Tag>Paper research</Tag>
          <Tag>{current?.config?.symbol || 'BTCUSDT'}</Tag>
          <Tag>{current?.config?.timeframe || '1h'}</Tag>
          <Tag>{current?.config?.strategy || 'sma-cross-v1'}</Tag>
          <Tag>{engineVersion}</Tag>
          <Tag color={source.plane === 'fixture' ? 'gold' : 'blue'}>{source.title}</Tag>
          {current && <Tag color={current.replayed ? 'gold' : current.status === 'FAILED' ? 'red' : 'blue'}>{runStatusLabel(current.status)}</Tag>}
        </Space>
        <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
          Workbench nghiên cứu chiến lược. Không gửi lệnh lên sàn. {source.note}
        </Typography.Paragraph>
      </div>
      {loading && <Alert type="info" showIcon message="Đang tải nhiệm vụ research." />}
      {error && <Alert type="error" showIcon message={error} />}
      {notice && <Alert type="success" showIcon message={notice} />}
      {busy && <Alert type="info" showIcon message="Đang chạy nghiên cứu." />}
      {executeEnabled === false && <Alert type="warning" showIcon message="Host này chưa bật thực thi research. Xem lịch sử nếu đã có." />}

      <Card title="Cấu hình nghiên cứu">
        <Form form={form} layout="vertical" initialValues={initialConfig} onFinish={() => void submit()}>
          <Typography.Text strong>Thị trường</Typography.Text>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, marginTop: 8 }}>
            <Form.Item name="symbol" label="Symbol"><Select disabled options={[{ value: 'BTCUSDT', label: 'BTCUSDT' }]} /></Form.Item>
            <Form.Item name="timeframe" label="Timeframe"><Select disabled options={[{ value: '1h', label: '1 giờ' }]} /></Form.Item>
            <Form.Item name="dataSource" label="Nguồn nến">
              <Select options={[{ value: 'fixture', label: 'Fixture kiểm thử' }, { value: 'public_market', label: 'Thị trường công khai' }]} />
            </Form.Item>
          </div>
          <Alert type={formSource.plane === 'fixture' ? 'warning' : 'info'} showIcon message={formSource.note} style={{ marginBottom: 12 }} />
          <Typography.Text strong>Chiến lược</Typography.Text>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12, marginTop: 8 }}>
            <Form.Item name="preset" label="Chiến lược">
              <Select options={presets} />
            </Form.Item>
          </div>
          <Typography.Paragraph type="secondary">Mỗi preset có quy tắc vào và ra trên nến đã đóng. Lệnh khớp ở giá mở của nến kế tiếp. Chưa có TP/SL.</Typography.Paragraph>
          <Collapse items={[{
            key: 'execution',
            label: 'Mô phỏng thực thi và giới hạn rủi ro',
            children: (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 12 }}>
                <Form.Item name="quantity" label="Khối lượng" rules={[{ required: true }]}><InputNumber min={0.00000001} max={0.01} step={0.001} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="feeRate" label="Phí"><InputNumber min={0} max={0.01} step={0.0001} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="slippageBps" label="Slippage bps"><InputNumber min={0} max={50} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="confidence" label="Độ tin cậy"><InputNumber min={0.5} max={1} step={0.05} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="minConfidence" label="Độ tin cậy tối thiểu"><InputNumber min={0.5} max={1} step={0.05} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="maxNotional" label="Trần notional"><InputNumber min={1} max={1000} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="sessionNotional" label="Trần phiên"><InputNumber min={1} max={5000} style={{ width: '100%' }} /></Form.Item>
                <Form.Item name="maxLeverage" label="Trần đòn bẩy"><InputNumber disabled style={{ width: '100%' }} /></Form.Item>
              </div>
            ),
          }]} />
          <Typography.Paragraph type="secondary">Đòn bẩy tối đa cố định bằng 1. Paper research, không gửi lệnh thật.</Typography.Paragraph>
          <Space wrap>
            <Button type="primary" htmlType="submit" loading={busy} disabled={!canRun || executeEnabled === false}>Chạy nghiên cứu</Button>
            <Button onClick={() => void runPlan()} loading={busy} disabled={!canRun || executeEnabled === false}>Chạy bộ 4 chiến lược</Button>
          </Space>
        </Form>
      </Card>

      {!current && !loading && <Alert message="Chưa có nhiệm vụ research." />}
      {current && (
        <Card id="research-summary" title="Tóm tắt lần chạy">
          <Descriptions column={{ xs: 1, sm: 2, md: 3 }}>
            <Descriptions.Item label="Trạng thái">{runStatusLabel(current.status)}</Descriptions.Item>
            <Descriptions.Item label="Nguồn">{source.title}</Descriptions.Item>
            <Descriptions.Item label="Replay">{current.replayed ? 'Có, không tạo dữ liệu mới' : 'Không'}</Descriptions.Item>
            <Descriptions.Item label="Bắt đầu"><TimeText value={current.startedAt} /></Descriptions.Item>
            <Descriptions.Item label="Kết thúc"><TimeText value={current.finishedAt} /></Descriptions.Item>
            <Descriptions.Item label="Fill mới">{current.newFills}</Descriptions.Item>
            <Descriptions.Item label="Cấu hình">
              <Typography.Text copyable={current.parameterKey ? { text: current.parameterKey } : false}>{shortKey(current.parameterKey)}</Typography.Text>
            </Descriptions.Item>
          </Descriptions>
          {document && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 12, marginTop: 12 }}>
              <Metric label="Nến" value={String(document.overview.candles)} />
              <Metric label="Tín hiệu" value={String(document.overview.signals)} />
              <Metric label="Paper fills" value={String(document.overview.paperFills)} />
              <Metric label="Giao dịch đóng" value={document.performance ? String(document.performance.closedTrades) : 'Chưa đủ dữ liệu'} />
              <Metric label="Net PnL" value={money(document.performance?.netPnl, closedTrades)} />
              <Metric label="Win rate" value={metricDisplay(document.performance?.winRate, closedTrades, 'rate').text} />
              <Metric label="Profit factor" value={money(document.performance?.profitFactor, closedTrades)} />
              <Metric label="Expectancy" value={money(document.performance?.expectancy, closedTrades)} />
              <Metric label="Max drawdown" value={money(document.performance?.maxDrawdown, closedTrades)} />
            </div>
          )}
          {!document && <Alert style={{ marginTop: 12 }} type="warning" showIcon message="Lần chạy này chưa có báo cáo để đọc." />}
        </Card>
      )}

      <Tabs
        activeKey={tab}
        onChange={setTab}
        items={[
          {
            key: 'performance',
            label: 'Hiệu suất',
            children: document ? (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Typography.Paragraph type="secondary">
                  Chỉ tính từ trade research đã đóng. Không dùng số dư Binance, không tính unrealized, và không trừ phí thêm lần nữa. Chỉ số chưa biết hiện là Chưa xác định, không phải 0.
                  {source.plane === 'fixture' ? ' Số liệu fixture không phải hiệu suất giao dịch thật.' : ''}
                </Typography.Paragraph>
                <Descriptions column={{ xs: 1, sm: 2 }}>
                  <Descriptions.Item label="Phí đã ghi">{formatAmount(document.overview.fees)}</Descriptions.Item>
                  <Descriptions.Item label="Slippage đã ghi">{formatAmount(document.overview.slippage)}</Descriptions.Item>
                  <Descriptions.Item label="Realized PnL phiên">{money(document.overview.realizedPnl)}</Descriptions.Item>
                </Descriptions>
                <Card size="small" title="Trong mẫu và Ngoài mẫu">
                  {sampleLines(document.sample).map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                </Card>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 12 }}>
                  <Card size="small" title="PnL lũy kế đã chốt">
                    {equity ? <EquityChart model={equity} /> : <Typography.Text type="secondary">Chưa đủ dữ liệu để vẽ. Cần mọi giao dịch đã đóng có Net đã biết.</Typography.Text>}
                    <Typography.Paragraph type="secondary">Đường xanh là Net lũy kế, xuất phát từ 0. Đường vàng là drawdown trên đường đó.</Typography.Paragraph>
                  </Card>
                  <Card size="small" title="Phân bố lãi lỗ">
                    {distribution ? <DistributionChart bars={distribution} /> : <Typography.Text type="secondary">Chưa đủ dữ liệu. Chưa có giao dịch đã đóng với Net đã biết.</Typography.Text>}
                    {distribution && distribution.length < 5 && <Typography.Paragraph type="secondary">Mẫu {distribution.length} giao dịch. Chưa đủ để kết luận chiến lược.</Typography.Paragraph>}
                  </Card>
                </div>
              </Space>
            ) : <Alert message="Chưa có báo cáo hiệu suất." />,
          },
          {
            key: 'market',
            label: 'Thị trường và tín hiệu',
            children: document ? (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Card size="small" title="Biểu đồ nến">
                  {candles ? <CandleChart model={candles} /> : <Typography.Text type="secondary">Chưa có nến đủ số để vẽ. Chạy nghiên cứu với nguồn đã chọn.</Typography.Text>}
                  <Typography.Paragraph type="secondary">
                    Nến xanh đóng cửa không thấp hơn mở cửa. Chấm xanh là BUY, chấm vàng là SELL, chỉ khi thời điểm tín hiệu nằm trong nến.
                    {candles && candles.unmatchedSignals > 0 ? ` ${candles.unmatchedSignals} tín hiệu không gắn được vào nến.` : ''}
                    {candles && candles.incompleteCandles > 0 ? ` ${candles.incompleteCandles} nến chưa hoàn tất.` : ''}
                    {' '}Không suy đoán TP/SL.
                  </Typography.Paragraph>
                </Card>
                <Table
                  scroll={{ x: 720 }}
                  rowKey={(row) => row.openTime}
                  dataSource={document.candles}
                  pagination={{ pageSize: 8 }}
                  locale={{ emptyText: 'Chưa có nến.' }}
                  columns={[
                    { title: 'Symbol', dataIndex: 'symbol' },
                    { title: 'Khung', dataIndex: 'interval' },
                    { title: 'Mở', dataIndex: 'openTime', render: (value: string) => <TimeText value={value} /> },
                    { title: 'Đóng', dataIndex: 'closeTime', render: (value: string) => <TimeText value={value} /> },
                    { title: 'Trạng thái', dataIndex: 'closed', render: (closed: boolean) => closed ? 'Đã đóng' : 'Chưa hoàn tất' },
                    { title: 'OHLC', render: (_: unknown, row) => `${formatAmount(row.open)} / ${formatAmount(row.high)} / ${formatAmount(row.low)} / ${formatAmount(row.close)}` },
                  ]}
                />
                <Table
                  scroll={{ x: 720 }}
                  rowKey={(row) => row.signalTime + row.side}
                  dataSource={document.signals}
                  pagination={false}
                  locale={{ emptyText: 'Chưa có tín hiệu.' }}
                  columns={[
                    { title: 'Chiến lược', dataIndex: 'strategy' },
                    { title: 'Thời điểm', dataIndex: 'signalTime', render: (value: string) => <TimeText value={value} /> },
                    { title: 'Loại', dataIndex: 'side' },
                    { title: 'Trạng thái', dataIndex: 'status' },
                    { title: 'Risk', dataIndex: 'riskAllowed', render: (value: boolean | null) => value == null ? '—' : value ? 'Cho phép' : 'Từ chối' },
                    { title: 'Lý do', dataIndex: 'riskReason', render: (value: string) => value || '—' },
                  ]}
                />
              </Space>
            ) : <Alert message="Chưa có dữ liệu thị trường." />,
          },
          {
            key: 'paper',
            label: 'Paper trading',
            children: document ? (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Typography.Paragraph type="secondary">Paper fill là một lần khớp mô phỏng. Trade Ledger chỉ đóng khi đủ cặp. Một fill chưa phải một giao dịch đã đóng.</Typography.Paragraph>
                <Table
                  scroll={{ x: 720 }}
                  rowKey={(_row, index) => String(index)}
                  dataSource={document.fills}
                  pagination={false}
                  locale={{ emptyText: 'Chưa có paper fill.' }}
                  columns={[
                    { title: 'Side', dataIndex: 'side' },
                    { title: 'Khối lượng', dataIndex: 'quantity', render: (value: string) => formatAmount(value) },
                    { title: 'Giá tape', dataIndex: 'tapePrice', render: (value: string) => formatAmount(value) },
                    { title: 'Giá khớp', dataIndex: 'executionPrice', render: (value: string) => formatAmount(value) },
                    { title: 'Phí', dataIndex: 'fee', render: (value: string) => formatAmount(value) },
                    { title: 'Slippage', dataIndex: 'slippageQuote', render: (value: string) => formatAmount(value) },
                    { title: 'Cash flow', dataIndex: 'netCash', render: (value: string) => formatAmount(value) },
                  ]}
                />
                <Card title="Trade Ledger" size="small">
                  <Typography.Paragraph type="secondary">
                    Sổ suy từ paper fill của research. Không đọc sổ tenant hay sàn. Cấu hình {shortKey(current.parameterKey)}.
                    {' '}{current.config ? `${current.config.strategy} · ${current.config.symbol} ${current.config.timeframe}` : ''}
                  </Typography.Paragraph>
                  {(document.positions ?? []).length === 0
                    ? <Alert type="info" showIcon message="Không có vị thế mở. Unrealized PnL chưa xác định vì không có mark price." />
                    : (document.positions ?? []).map((position) => (
                      <Alert key={position.symbol} type="info" showIcon message={`Vị thế ${position.direction} ${position.symbol}: khối lượng ${formatAmount(position.quantity)}, giá vào ${formatAmount(position.entryPrice)}. Unrealized PnL chưa xác định (${position.unrealizedPnl.status}).`} />
                    ))}
                  <Table
                    scroll={{ x: 960 }}
                    rowKey="tradeId"
                    dataSource={document.trades ?? []}
                    pagination={false}
                    locale={{ emptyText: 'Chưa có giao dịch đã ghép.' }}
                    columns={[
                      { title: 'Trade', dataIndex: 'tradeId', ellipsis: true },
                      { title: 'Trạng thái', dataIndex: 'status', render: tradeStatusLabel },
                      { title: 'Hướng', dataIndex: 'direction', render: (value: string | null) => value || '—' },
                      { title: 'Symbol', dataIndex: 'symbol', render: (value: string | null) => value || '—' },
                      { title: 'Khung', dataIndex: 'timeframe', render: (value: string | null) => value || '—' },
                      { title: 'Mở', dataIndex: 'openedAt', render: (value: string | null) => <TimeText value={value} /> },
                      { title: 'Đóng', dataIndex: 'closedAt', render: (value: string | null) => <TimeText value={value} /> },
                      { title: 'Giá mở', dataIndex: 'openPrice', render: (value: string | null) => formatAmount(value) },
                      { title: 'Giá đóng', dataIndex: 'closePrice', render: (value: string | null) => formatAmount(value) },
                      { title: 'Khối lượng', dataIndex: 'quantity', render: (value: string | null) => formatAmount(value) },
                      { title: 'Gross', dataIndex: 'grossPnl', render: (value) => money(value) },
                      { title: 'Phí', dataIndex: 'fees', render: (value) => money(value) },
                      { title: 'Slippage', dataIndex: 'slippage', render: (value) => money(value) },
                      { title: 'Net', dataIndex: 'netPnl', render: (value) => money(value) },
                      { title: 'Lý do đóng', dataIndex: 'closeReason', render: closeReasonLabel },
                      { title: 'Fills', dataIndex: 'fillKeys', render: (value: string[]) => value.join(', ') },
                    ]}
                  />
                </Card>
              </Space>
            ) : <Alert message="Chưa có paper fill." />,
          },
          {
            key: 'history',
            label: 'Lịch sử thí nghiệm',
            children: (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Typography.Paragraph type="secondary">Mỗi nhiệm vụ giữ ledger theo mã cấu hình. Không cộng kết quả của hai mã khác nhau. Chọn tối đa 3 dòng để đặt cạnh nhau.</Typography.Paragraph>
                <Table
                  scroll={{ x: 760 }}
                  rowKey="taskId"
                  dataSource={tasks}
                  pagination={false}
                  locale={{ emptyText: 'Chưa có nhiệm vụ.' }}
                  rowSelection={{
                    selectedRowKeys: selectedIds,
                    onChange: (keys) => setSelectedIds(keys.map(String).slice(0, 3)),
                  }}
                  onRow={(row) => ({ onClick: () => { setCurrent(row); globalThis.document.getElementById('research-summary')?.scrollIntoView({ block: 'start' }); } })}
                  columns={[
                    { title: 'Thời điểm', dataIndex: 'requestedAt', render: (value: string) => <TimeText value={value} /> },
                    { title: 'Trạng thái', dataIndex: 'status', render: runStatusLabel },
                    { title: 'Nguồn', dataIndex: 'dataSource', render: (value: string) => sourcePresentation(value).title },
                    { title: 'Cấu hình', render: (_: unknown, row: ResearchTask) => row.config ? `${row.config.strategy} · ${row.config.symbol} ${row.config.timeframe}` : '—' },
                    { title: 'Mã', dataIndex: 'parameterKey', render: (value: string) => <Typography.Text copyable={{ text: value }}>{shortKey(value)}</Typography.Text> },
                    { title: 'Fill mới', dataIndex: 'newFills' },
                    { title: 'Replay', dataIndex: 'replayed', render: (value: boolean) => value ? 'Có' : 'Không' },
                    { title: 'Lỗi', dataIndex: 'error', render: (value: string | null) => value || '—' },
                  ]}
                />
                <Card size="small" title="So sánh các lần đã chọn">
                  {compared.length === 0 && <Typography.Text type="secondary">Chọn từ một đến ba lần chạy. Nếu một lần chưa có báo cáo, dòng đó ghi chưa đủ dữ liệu.</Typography.Text>}
                  {compared.some((row) => row.sameConfigAsAnother) && <Alert type="info" showIcon message="Có ít nhất hai dòng cùng mã cấu hình. Đó là replay hoặc lần chạy lại, không phải hai thí nghiệm độc lập." />}
                  {new Set(compared.map((row) => row.dataSource)).size > 1 && <Alert type="warning" showIcon message="Các lần đang so sánh không cùng nguồn dữ liệu. Không cộng Net của chúng." />}
                  {compared.length > 0 && (
                    <Table
                      scroll={{ x: 860 }}
                      rowKey="taskId"
                      pagination={false}
                      dataSource={compared}
                      columns={[
                        { title: 'Thời điểm', dataIndex: 'requestedAt', render: (value: string) => <TimeText value={value} /> },
                        { title: 'Nguồn', dataIndex: 'dataSource', render: (value: string) => sourcePresentation(value).title },
                        { title: 'Mã', dataIndex: 'parameterKey', render: (value: string) => shortKey(value) },
                        { title: 'Nến', dataIndex: 'candles', render: (value: number | null) => value ?? 'Chưa đủ dữ liệu' },
                        { title: 'Tín hiệu', dataIndex: 'signals', render: (value: number | null) => value ?? 'Chưa đủ dữ liệu' },
                        { title: 'Fill', dataIndex: 'fills', render: (value: number | null) => value ?? 'Chưa đủ dữ liệu' },
                        { title: 'Đóng', dataIndex: 'closedTrades', render: (value: number | null) => value ?? 'Chưa đủ dữ liệu' },
                        { title: 'Net', dataIndex: 'net' },
                        { title: 'Win rate', dataIndex: 'winRate' },
                        { title: 'Fill mới', dataIndex: 'newFills' },
                      ]}
                    />
                  )}
                </Card>
                {document && (
                  <Card size="small" title="Các lượt ghi trong thí nghiệm đang xem">
                    <Table
                      scroll={{ x: 720 }}
                      rowKey={(row) => row.recordedAt + row.detail}
                      dataSource={document.runs}
                      pagination={false}
                      locale={{ emptyText: 'Chưa có lượt chạy.' }}
                      columns={[
                        { title: 'Thời điểm', dataIndex: 'recordedAt', render: (value: string) => <TimeText value={value} /> },
                        { title: 'Trạng thái', dataIndex: 'status', render: runStatusLabel },
                        { title: 'Tóm tắt', dataIndex: 'detail', render: (value: string) => explainRunDetail(value).label },
                        { title: 'Chi tiết kỹ thuật', dataIndex: 'detail', render: (value: string) => <Typography.Text type="secondary">{explainRunDetail(value).raw || '—'}</Typography.Text> },
                      ]}
                    />
                  </Card>
                )}
              </Space>
            ),
          },
          {
            key: 'conclusion',
            label: 'Kết luận',
            children: !current ? <Alert message="Chưa có thí nghiệm để kết luận." /> : (
              <Space direction="vertical" size={12} style={{ width: '100%' }}>
                <Card size="small" title="Đã thực hiện">
                  {conclusion.experiment.map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                </Card>
                <Card size="small" title="Bằng chứng">
                  {conclusion.evidence.length === 0
                    ? <Typography.Text type="secondary">Chưa có bằng chứng hiệu suất.</Typography.Text>
                    : conclusion.evidence.map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                </Card>
                <Card size="small" title="Chưa kết luận được">
                  {conclusion.unknown.map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                </Card>
                <Card size="small" title="Giới hạn dữ liệu">
                  {conclusion.limits.map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                </Card>
                <Card size="small" title="Hướng thử tiếp">
                  {conclusion.next.map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                  {sampleLines(document?.sample).map((line) => <Typography.Paragraph key={line} style={{ marginBottom: 4 }}>{line}</Typography.Paragraph>)}
                </Card>
                {current?.reportText && (
                  <Collapse items={[{ key: 'report', label: 'Giải thích từ lần chạy', children: <Typography.Paragraph style={{ marginBottom: 0 }}>{current.reportText}</Typography.Paragraph> }]} />
                )}
              </Space>
            ),
          },
        ]}
      />
    </Space>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ border: '1px solid #f0f0f0', borderRadius: 8, padding: 12 }}>
      <Typography.Text type="secondary">{label}</Typography.Text>
      <div style={{ fontSize: 16, marginTop: 4 }}>{value}</div>
    </div>
  );
}
