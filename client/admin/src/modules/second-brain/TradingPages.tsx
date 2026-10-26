import { useEffect, useState } from 'react';
import {
  Alert, App, Button, Card, Descriptions, Drawer, Empty, Form, Input, InputNumber, Select, Space, Statistic, Table, Tag, Typography,
} from 'antd';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/auth.store';
import { operatorNotices } from './operator-notices';
import {
  tradingApi,
  type DecisionDetail, type DecisionRow, type ImportPreview, type LoopResult, type OperationRow,
  type PaperRow, type TradeRow, type TradingAccount, type TradingSummary,
} from './trading.api';

function errorText(error: unknown): string {
  const err = error as { response?: { status?: number; data?: { message?: string } }; message?: string };
  const status = err.response?.status;
  const message = err.response?.data?.message;
  if (status === 401) return 'Phiên đăng nhập đã hết hạn.';
  if (status === 403) return message || 'Không có quyền hoặc module chưa được bật.';
  if (status === 409) return 'Nội dung đã đổi so với lần ghi trước. Hãy bắt đầu nhận định mới.';
  if (!err.response) return 'API không phản hồi.';
  return message || 'Yêu cầu không thành công.';
}

function sourceLabel(source?: string | null): string {
  const value = source?.trim().toUpperCase();
  if (!value) return 'Chưa ghi nhận';
  if (value === 'FIXTURE') return 'Dữ liệu kiểm thử';
  if (value === 'IMPORT') return 'CSV do người dùng nhập';
  if (value === 'EXCHANGE') return 'Đồng bộ từ sàn';
  if (value === 'MIXED') return 'Nhiều nguồn';
  return source ?? 'Chưa ghi nhận';
}

function kindLabel(kind?: string | null): string {
  if (kind === 'HYPOTHESIS') return 'Nhận định';
  if (kind === 'OBSERVATION') return 'Dữ liệu quan sát';
  if (kind === 'INFERENCE') return 'Suy luận';
  if (kind === 'POLICY') return 'Quy tắc đề xuất';
  return kind || 'Chưa ghi nhận';
}

function SourceTag({ source }: { source?: string | null }) {
  const label = sourceLabel(source);
  const value = source?.trim().toUpperCase();
  const color = value === 'FIXTURE' ? 'gold' : value === 'EXCHANGE' ? 'green' : value === 'IMPORT' ? 'blue' : undefined;
  return <Tag color={color}>{label}</Tag>;
}

function Missing({ value }: { value?: string | null }) {
  return value ? <>{value}</> : <Typography.Text type="secondary">Chưa ghi nhận</Typography.Text>;
}

export function TradingOverviewPage() {
  const [data, setData] = useState<TradingSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    tradingApi.summary().then(setData).catch((err) => setError(errorText(err))).finally(() => setLoading(false));
  }, []);
  const notices = data ? operatorNotices(data) : [];
  const counts = data ?? {
    tradeCount: 0, fixtureTradeCount: 0, importTradeCount: 0, exchangeTradeCount: 0,
    decisionCount: 0, paperExecutionCount: 0, riskVetoCount: 0, latestSource: null, latestRecordedAt: null, missingData: [],
  };
  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {loading ? <Card loading /> : error ? <Alert type="error" showIcon message={error} /> : (
      <>
      <Typography.Text strong>Sổ giao dịch Personal Intelligence</Typography.Text>
      {data?.onlyFixtureData && (
        <Alert type="warning" showIcon message="Chỉ có dữ liệu kiểm thử. Có thể đồng bộ lịch sử khớp lệnh từ Phiên làm việc, hoặc nhập CSV." />
      )}
      {counts.tradeCount === 0 ? <Empty description="Chưa có giao dịch nào được lưu." /> : null}
      <Space wrap>
        <Card><Statistic title="Giao dịch đã ghi" value={counts.tradeCount} /></Card>
        <Card><Statistic title="Dữ liệu kiểm thử" value={counts.fixtureTradeCount} /></Card>
        <Card><Statistic title="CSV do người dùng nhập" value={counts.importTradeCount ?? 0} /></Card>
        <Card><Statistic title="Đồng bộ từ sàn" value={counts.exchangeTradeCount ?? 0} /></Card>
        <Card><Statistic title="Nhận định" value={counts.decisionCount} /></Card>
        <Card><Statistic title="Giả lập" value={counts.paperExecutionCount} /></Card>
        <Card><Statistic title="Risk từ chối" value={counts.riskVetoCount} /></Card>
      </Space>
      <Card title="Nhập gần nhất">
        <Space>
          <SourceTag source={counts.latestSource} />
          <span>{counts.latestRecordedAt ? new Date(counts.latestRecordedAt).toLocaleString('vi-VN') : 'Chưa ghi nhận'}</span>
        </Space>
      </Card>
      {(counts.missingData ?? []).length > 0 && (
        <Alert type="info" showIcon message="Dữ liệu còn thiếu" description={counts.missingData.join(' ')} />
      )}
      <Card title="Việc tiếp theo">
        <Space direction="vertical" style={{ width: '100%' }}>
          {notices.map((item) => (
            <Alert
              key={item.id}
              type={item.priority === 'high' ? 'warning' : 'info'}
              showIcon
              message={item.action}
              description={`${item.reason}${item.blocker ? ` ${item.blocker}` : ''}`}
              action={<Link to={item.href}>Mở</Link>}
            />
          ))}
        </Space>
      </Card>
      </>
      )}
    </Space>
  );
}

export function TradingLedgerPage() {
  const [rows, setRows] = useState<TradeRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [accounts, setAccounts] = useState<TradingAccount[]>([]);
  const [filters, setFilters] = useState<Record<string, string | undefined>>({});
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [open, setOpen] = useState<TradeRow | null>(null);

  const load = (nextPage: number, next = filters) => {
    setLoading(true);
    tradingApi.trades({ page: nextPage, pageSize: 20, ...next })
      .then((result) => { setRows(result.items); setTotal(result.total); })
      .catch((err) => setError(errorText(err)))
      .finally(() => setLoading(false));
  };
  useEffect(() => {
    tradingApi.accounts().then(setAccounts).catch(() => undefined);
    load(1);
  }, []);

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      {error && <Alert type="error" showIcon message={error} />}
      <Space wrap>
        <Select allowClear placeholder="Tài khoản" style={{ width: 220 }}
          options={accounts.map((a) => ({ value: a.tradingAccountId, label: `${a.exchangeCode} · ${a.externalAccountId}` }))}
          onChange={(value) => setFilters((f) => ({ ...f, tradingAccountId: value }))} />
        <Input placeholder="Symbol" style={{ width: 140 }} onChange={(e) => setFilters((f) => ({ ...f, symbol: e.target.value || undefined }))} />
        <Select allowClear placeholder="Chiều" style={{ width: 120 }}
          options={[{ value: 'BUY', label: 'BUY' }, { value: 'SELL', label: 'SELL' }]}
          onChange={(value) => setFilters((f) => ({ ...f, side: value }))} />
        <Select allowClear placeholder="Thị trường" style={{ width: 140 }}
          options={[{ value: 'SPOT', label: 'Spot' }, { value: 'FUTURES', label: 'Futures' }]}
          onChange={(value) => setFilters((f) => ({ ...f, marketType: value }))} />
        <Select allowClear placeholder="Nguồn" style={{ width: 140 }}
          options={[
            { value: 'FIXTURE', label: 'Dữ liệu kiểm thử' },
            { value: 'IMPORT', label: 'CSV do người dùng nhập' },
            { value: 'EXCHANGE', label: 'Đồng bộ từ sàn' },
          ]}
          onChange={(value) => setFilters((f) => ({ ...f, source: value }))} />
        <Button onClick={() => { setPage(1); load(1); }}>Lọc</Button>
      </Space>
      <Table rowKey="tradeId" loading={loading} dataSource={rows} pagination={{ current: page, total, pageSize: 20, onChange: (p) => { setPage(p); load(p); } }}
        locale={{ emptyText: 'Chưa có giao dịch khớp bộ lọc.' }}
        onRow={(row) => ({ onClick: () => setOpen(row), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Thời gian', dataIndex: 'executedAt', render: (v: string) => new Date(v).toLocaleString('vi-VN') },
          { title: 'Sàn', dataIndex: 'exchangeCode' },
          { title: 'Tài khoản', dataIndex: 'externalAccountId' },
          { title: 'Thị trường', render: (_, row) => row.marketType ? `${row.marketType}${row.productSubtype ? ` · ${row.productSubtype}` : ''}` : 'Chưa phân loại' },
          { title: 'Symbol', dataIndex: 'symbol' },
          { title: 'Chiều', dataIndex: 'side' },
          { title: 'Giá', dataIndex: 'price' },
          { title: 'Số lượng', dataIndex: 'quantity' },
          { title: 'Phí', render: (_, row) => row.feeAmount == null ? 'Chưa ghi nhận' : `${row.feeAmount} ${row.feeCurrency ?? ''}` },
          { title: 'Nguồn', dataIndex: 'source', render: (v: string) => <SourceTag source={v} /> },
        ]} />
      <Drawer title="Chi tiết giao dịch quan sát" open={!!open} onClose={() => setOpen(null)} width={480}>
        {open && (
          <Descriptions column={1}>
            <Descriptions.Item label="Mã ngoài">{open.externalTradeId}</Descriptions.Item>
            <Descriptions.Item label="Nguồn"><SourceTag source={open.source} /></Descriptions.Item>
            <Descriptions.Item label="Cách ghi">{open.sourceType === 'IMPORT' ? 'Nhập từ tệp CSV' : open.sourceType === 'EXCHANGE' ? 'Đồng bộ từ API sàn' : open.sourceType}</Descriptions.Item>
            <Descriptions.Item label="Loại sản phẩm">{open.marketType ? `${open.marketType} · ${open.productSubtype || open.category || 'không có subtype'}` : (open.category || 'Không có trên dòng này')}</Descriptions.Item>
            <Descriptions.Item label="PnL sàn">{open.realizedPnl || 'Sàn không trả realized PnL trên dòng này. Không tính bằng chênh lệch giá.'}</Descriptions.Item>
            <Descriptions.Item label="Quan sát">{open.isObserved ? 'Có — đây không phải một quyết định' : 'Không'}</Descriptions.Item>
            <Descriptions.Item label="Phí">{open.feeAmount == null ? 'Chưa ghi nhận' : `${open.feeAmount} ${open.feeCurrency ?? ''}`}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>
    </Space>
  );
}

export function TradingImportPage() {
  const navigate = useNavigate();
  const [accounts, setAccounts] = useState<TradingAccount[]>([]);
  const [accountId, setAccountId] = useState<string>();
  const [source, setSource] = useState<string>();
  const [csv, setCsv] = useState('');
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => { tradingApi.accounts().then(setAccounts).catch((err) => setError(errorText(err))); }, []);

  const readFile = async (file: File) => {
    if (file.size > 1_000_000) { setError('Tệp vượt quá 1 MB.'); return; }
    setCsv(await file.text());
    setPreview(null);
  };

  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      {error && <Alert type="error" showIcon message={error} />}
      {message && <Alert type="success" showIcon message={message} />}
      <Alert type="info" showIcon message="Định dạng hỗ trợ: external_trade_id,symbol,side,quantity,price,executed_at,fee_amount,fee_currency. Import không tạo Decision." />
      <Space wrap>
        <Select placeholder="Tài khoản" style={{ width: 260 }} value={accountId}
          options={accounts.map((a) => ({ value: a.tradingAccountId, label: `${a.exchangeCode} · ${a.externalAccountId}` }))}
          onChange={setAccountId} />
        <Select placeholder="Chọn ý nghĩa dữ liệu" value={source} style={{ width: 240 }} onChange={setSource}
          options={[{ value: 'IMPORT', label: 'CSV do người dùng nhập' }, { value: 'FIXTURE', label: 'Dữ liệu kiểm thử' }]} />
        <input aria-label="Chọn tệp CSV" type="file" accept=".csv,text/csv" onChange={(e) => { const file = e.target.files?.[0]; if (file) void readFile(file); }} />
      </Space>
      <Input.TextArea rows={8} value={csv} onChange={(e) => { setCsv(e.target.value); setPreview(null); }} placeholder="Dán CSV hoặc chọn tệp" />
      <Space>
        <Button disabled={!accountId || !csv || !source || busy} onClick={() => {
          setBusy(true); setError(null);
          tradingApi.previewImport(accountId!, csv, source!).then(setPreview).catch((err) => setError(errorText(err))).finally(() => setBusy(false));
        }}>Xem trước</Button>
        <Button type="primary" disabled={!preview || !accountId || busy} onClick={() => {
          setBusy(true); setError(null);
          tradingApi.commitImport(accountId!, csv, source!).then((result) => {
            setMessage(`Hợp lệ ${preview?.acceptedCount ?? 0}. Trùng ${result.duplicateCount}. Đã ghi ${result.insertedCount}. Import không tạo nhận định.`);
            navigate(`/second-brain/session?account=${accountId}`);
          }).catch((err) => setError(errorText(err))).finally(() => setBusy(false));
        }}>Xác nhận import</Button>
      </Space>
      {preview && (
        <Card title={`Hợp lệ ${preview.acceptedCount} · Trùng ${preview.duplicateCount}`}>
          <Table size="small" rowKey="externalTradeId" dataSource={preview.rows} pagination={false}
            columns={[
              { title: 'Mã', dataIndex: 'externalTradeId' },
              { title: 'Symbol', dataIndex: 'symbol' },
              { title: 'Chiều', dataIndex: 'side' },
              { title: 'Giá', dataIndex: 'price' },
              { title: 'Số lượng', dataIndex: 'quantity' },
              { title: 'Phí', render: (_, row) => row.feeAmount ?? 'Chưa ghi nhận' },
            ]} />
          {preview.duplicateExternalIds.length > 0 && <Typography.Paragraph>Trùng: {preview.duplicateExternalIds.join(', ')}</Typography.Paragraph>}
        </Card>
      )}
    </Space>
  );
}

export function TradingJournalPage() {
  const [params] = useSearchParams();
  const focusId = params.get('decision');
  const [rows, setRows] = useState<DecisionRow[]>([]);
  const [search, setSearch] = useState('');
  const [detail, setDetail] = useState<DecisionDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const load = (q = search) => {
    setLoading(true);
    tradingApi.decisions({ page: 1, pageSize: 30, search: q || undefined })
      .then((r) => setRows(r.items)).catch((err) => setError(errorText(err))).finally(() => setLoading(false));
  };
  useEffect(() => { load(''); }, []);
  useEffect(() => {
    if (!focusId) return;
    tradingApi.decision(focusId).then((item) => {
      setDetail(item);
      setRows((current) => current.some((row) => row.decisionId === focusId) ? current : [item.decision, ...current]);
    }).catch((err) => setError(errorText(err)));
  }, [focusId]);
  const ordered = focusId ? [...rows.filter((row) => row.decisionId === focusId), ...rows.filter((row) => row.decisionId !== focusId)] : rows;
  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      {error && <Alert type="error" showIcon message={error} />}
      {focusId && <Alert type="info" showIcon message="Đang mở nhận định vừa ghi của phiên này." />}
      <Space>
        <Input.Search placeholder="Tìm luận điểm" allowClear onSearch={(value) => { setSearch(value); load(value); }} />
      </Space>
      <Table rowKey="decisionId" loading={loading} dataSource={ordered} locale={{ emptyText: 'Chưa có nhận định.' }}
        onRow={(row) => ({ onClick: () => tradingApi.decision(row.decisionId).then(setDetail).catch((err) => setError(errorText(err))), style: { cursor: 'pointer' } })}
        columns={[
          { title: 'Nhận định', dataIndex: 'thesis' },
          { title: 'Loại', dataIndex: 'epistemicKind', render: (v: string) => <Tag>{kindLabel(v)}</Tag> },
          { title: 'Tin cậy', dataIndex: 'confidence' },
          { title: 'Risk', dataIndex: 'riskAllowed', render: (v: boolean | null) => v == null ? 'Chưa ghi nhận' : v ? 'Cho phép' : 'Từ chối' },
          { title: 'Paper', dataIndex: 'paperExecuted', render: (v: boolean | null) => v ? 'Đã ghi' : 'Chưa thực hiện' },
        ]} />
      <Drawer title="Chi tiết quyết định" width={560} open={!!detail} onClose={() => setDetail(null)}>
        {detail && (
          <Descriptions column={1}>
            <Descriptions.Item label="Nhận định">{detail.decision.thesis}</Descriptions.Item>
            <Descriptions.Item label="Loại"><Tag>{kindLabel(detail.decision.epistemicKind)}</Tag></Descriptions.Item>
            <Descriptions.Item label="Bối cảnh"><Missing value={detail.contextSummary} /> {detail.contextEpistemicKind && <Tag>{kindLabel(detail.contextEpistemicKind)}</Tag>}</Descriptions.Item>
            <Descriptions.Item label="Nguồn dữ liệu"><Missing value={sourceLabel(detail.contextSource)} /></Descriptions.Item>
            <Descriptions.Item label="Dữ liệu quan sát">{detail.evidence.length === 0 ? 'Chưa ghi nhận' : detail.evidence.map((e) => e.sourceRef).join(', ')}</Descriptions.Item>
            <Descriptions.Item label="Hành động dự kiến"><Missing value={detail.intendedAction} /></Descriptions.Item>
            <Descriptions.Item label="Hành động thực tế"><Missing value={detail.actualAction} /></Descriptions.Item>
            <Descriptions.Item label="Kết quả"><Missing value={detail.outcomeSummary} /></Descriptions.Item>
            <Descriptions.Item label="Phản tư"><Missing value={detail.reflectionNote} /></Descriptions.Item>
            <Descriptions.Item label="Kinh nghiệm"><Missing value={detail.experienceStatement} /></Descriptions.Item>
            <Descriptions.Item label="Quy tắc đề xuất"><Missing value={detail.policyRule} /></Descriptions.Item>
            <Descriptions.Item label="Chưa biết">{detail.unknowns.length === 0 ? 'Chưa ghi nhận' : detail.unknowns.join(' ')}</Descriptions.Item>
          </Descriptions>
        )}
      </Drawer>
    </Space>
  );
}

export function TradingAgentPage() {
  const navigate = useNavigate();
  const { modal } = App.useApp();
  const canApprove = (useAuthStore((s) => s.user?.roles ?? []).includes('ADMIN'))
    || (useAuthStore((s) => s.user?.permissions ?? []).includes('personal_intelligence.trading.approve'));
  const [accounts, setAccounts] = useState<TradingAccount[]>([]);
  const [accountId, setAccountId] = useState<string>();
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID().replace(/-/g, ''));
  const [result, setResult] = useState<LoopResult | null>(null);
  const [history, setHistory] = useState<OperationRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [form] = Form.useForm();
  useEffect(() => {
    tradingApi.accounts().then(setAccounts).catch((err) => setError(errorText(err)));
    tradingApi.operations().then((page) => setHistory(page.items)).catch(() => undefined);
  }, []);

  const submit = async (retry: boolean) => {
    const values = await form.validateFields();
    const account = accounts.find((item) => item.tradingAccountId === accountId);
    if (!account) { setError('Chọn tài khoản.'); return; }
    const key = retry ? operationKey : crypto.randomUUID().replace(/-/g, '');
    if (!retry) setOperationKey(key);
    setBusy(true); setError(null);
    try {
      const body = {
        personId: account.personId,
        tradingAccountId: account.tradingAccountId,
        fixtureCsv: values.csv,
        thesis: values.thesis,
        expectedOutcome: values.expectedOutcome || null,
        invalidation: values.invalidation || null,
        symbol: values.symbol,
        side: values.side,
        quantity: values.quantity,
        price: values.price,
        leverage: values.leverage,
        confidence: values.confidence,
        maxNotional: values.maxNotional,
        maxLeverage: values.maxLeverage,
        minConfidence: values.minConfidence,
        reflectionNote: values.reflectionNote,
        policyRule: values.policyRule,
        operationKey: key,
        live: false,
      };
      const next = await tradingApi.runLoop(body);
      setResult(next);
      const page = await tradingApi.operations();
      setHistory(page.items);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Space direction="vertical" style={{ width: '100%' }} size={16}>
      {error && <Alert type="error" showIcon message={error} />}
      <Alert type="info" showIcon message="Biểu mẫu này ghi nhận định do người dùng nhập. Nó không xếp hạng chiến lược và không có kết quả out-of-sample. Phiên làm việc là lối vào chính." />
      <Form form={form} layout="vertical" initialValues={{ side: 'BUY', leverage: 1, confidence: 0.6, maxNotional: 1000, maxLeverage: 2, minConfidence: 0.55, quantity: 0.1, price: 100 }}>
        <Select placeholder="Tài khoản" style={{ width: 280, marginBottom: 12 }} value={accountId}
          options={accounts.map((a) => ({ value: a.tradingAccountId, label: `${a.exchangeCode} · ${a.externalAccountId}` }))}
          onChange={setAccountId} />
        <Form.Item name="thesis" label="Luận điểm" rules={[{ required: true }]}><Input /></Form.Item>
        <Space wrap>
          <Form.Item name="symbol" label="Symbol" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="side" label="Chiều"><Select options={[{ value: 'BUY' }, { value: 'SELL' }]} /></Form.Item>
          <Form.Item name="quantity" label="Số lượng"><InputNumber min={0} /></Form.Item>
          <Form.Item name="price" label="Giá"><InputNumber min={0} /></Form.Item>
          <Form.Item name="confidence" label="Tin cậy"><InputNumber min={0} max={1} step={0.05} /></Form.Item>
          <Form.Item name="maxNotional" label="Trần notional"><InputNumber min={0} /></Form.Item>
          <Form.Item name="maxLeverage" label="Trần đòn bẩy"><InputNumber min={0} /></Form.Item>
          <Form.Item name="minConfidence" label="Tin cậy tối thiểu"><InputNumber min={0} max={1} step={0.05} /></Form.Item>
        </Space>
        <Form.Item name="expectedOutcome" label="Kết quả kỳ vọng"><Input /></Form.Item>
        <Form.Item name="invalidation" label="Điều kiện vô hiệu"><Input /></Form.Item>
        <Form.Item name="reflectionNote" label="Ghi chú reflection" rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="policyRule" label="Đề xuất policy" rules={[{ required: true }]}><Input /></Form.Item>
        <Form.Item name="csv" label="CSV quan sát" rules={[{ required: true }]}>
          <Input.TextArea rows={6} />
        </Form.Item>
        <Space>
          <Button type="primary" loading={busy} disabled={busy} onClick={() => void submit(false)}>Phân tích mới</Button>
          <Button loading={busy} disabled={busy} onClick={() => void submit(true)}>Gửi lại nếu mạng lỗi</Button>
        </Space>
      </Form>
      {result && (
        <Card title="Kết quả đối chiếu">
          <Descriptions column={1}>
            <Descriptions.Item label="Cơ sở">Dữ liệu đã cung cấp, không phải thị trường live</Descriptions.Item>
            <Descriptions.Item label="Rủi ro">{result.riskAllowed ? 'Cho phép' : `Từ chối. ${(result.riskReasons ?? []).join(' ')}`}</Descriptions.Item>
            <Descriptions.Item label="Giả lập">{result.paperExecuted ? 'Đã ghi, không gửi lệnh lên sàn' : 'Chưa phê duyệt'}</Descriptions.Item>
            <Descriptions.Item label="Điều chưa biết">{result.unknowns.join(' ') || 'Chưa ghi nhận'}</Descriptions.Item>
          </Descriptions>
          <Space>
            <Button onClick={() => navigate(`/second-brain/journal?decision=${result.decisionId}`)}>Mở nhật ký</Button>
            {canApprove && result.riskAllowed && !result.paperExecuted && (
              <Button type="primary" onClick={() => modal.confirm({
                title: 'Ghi giả lập?',
                content: 'Đây là mô phỏng. Không gửi lệnh lên sàn.',
                okText: 'Phê duyệt giả lập',
                cancelText: 'Huỷ',
                onOk: () => tradingApi.approvePaper(result.decisionId).then(() => tradingApi.operation(operationKey)).then(() => setResult({ ...result, paperExecuted: true, executionMode: 'PAPER' })),
              })}>Phê duyệt giả lập</Button>
            )}
          </Space>
        </Card>
      )}
      <Card title="Lịch sử phân tích">
        <Table rowKey="operationId" dataSource={history} pagination={false} locale={{ emptyText: 'Chưa có phân tích.' }}
          columns={[
            { title: 'Rủi ro', dataIndex: 'riskAllowed', render: (v: boolean | null) => v == null ? 'Chưa ghi nhận' : v ? 'Cho phép' : 'Từ chối' },
            { title: 'Cơ sở', dataIndex: 'evidenceBasis', render: (v: string | null) => v === 'Fixture' ? 'Dữ liệu đã lưu' : (v ?? 'Chưa ghi nhận') },
            { title: 'Giả lập', dataIndex: 'paperExecuted', render: (v: boolean | null) => v ? 'Đã ghi' : 'Chưa' },
          ]} />
      </Card>
    </Space>
  );
}

export function TradingRiskPage() {
  const [rows, setRows] = useState<PaperRow[]>([]);
  const [ops, setOps] = useState<OperationRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    Promise.all([tradingApi.paper(), tradingApi.operations()])
      .then(([paper, operations]) => { setRows(paper.items); setOps(operations.items); })
      .catch((err) => setError(errorText(err)));
  }, []);
  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Alert type="info" showIcon message="Không có lệnh live. Paper là bản ghi mô phỏng sau khi risk cho phép và có phê duyệt riêng. Số dòng ở đây không phải bằng chứng đủ để chọn một phương án." />
      {error && <Alert type="error" showIcon message={error} />}
      <Card title="Đánh giá rủi ro đã lưu">
        <Table rowKey="operationId" dataSource={ops} pagination={false} locale={{ emptyText: 'Chưa có đánh giá.' }}
          columns={[
            { title: 'Rủi ro', dataIndex: 'riskAllowed', render: (v: boolean | null) => v == null ? 'Chưa ghi nhận' : v ? 'Cho phép' : 'Từ chối' },
            { title: 'Điều chưa biết', dataIndex: 'unknowns', render: (v: string[]) => v.length ? v.join(' ') : 'Chưa ghi nhận' },
          ]} />
      </Card>
      <Card title="Paper execution">
        <Table rowKey="executionId" dataSource={rows} pagination={false} locale={{ emptyText: 'Chưa có paper execution.' }}
          columns={[
            { title: 'Thời điểm', dataIndex: 'executedAt', render: (v: string) => new Date(v).toLocaleString('vi-VN') },
            { title: 'Mode', dataIndex: 'mode' },
            { title: 'Symbol', dataIndex: 'symbol' },
            { title: 'Chiều', dataIndex: 'side' },
            { title: 'Số lượng', dataIndex: 'quantity' },
            { title: 'Giá', dataIndex: 'price' },
            { title: 'Trạng thái lệnh', dataIndex: 'orderStatus' },
          ]} />
      </Card>
    </Space>
  );
}
