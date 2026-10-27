import { useEffect, useMemo, useState } from 'react';
import {
  Alert, App, Button, Card, Collapse, Descriptions, Input, InputNumber, Radio, Select, Space, Table, Tag, Typography,
} from 'antd';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuthStore } from '@/shared/auth/auth.store';
import { exchangeApi, executionCategories, type ExchangeAccount, type ExchangeConnection, type ExchangeSyncResult } from './exchange.api';
import {
  tradingApi,
  type ImportPreview, type LoopResult, type TradeRow, type TradingAccount, type TradingSummary,
} from './trading.api';

function errorText(error: unknown): string {
  const err = error as { response?: { status?: number; data?: { message?: string } }; message?: string };
  const status = err.response?.status;
  const message = err.response?.data?.message;
  if (status === 401) return 'Phiên đăng nhập đã hết hạn.';
  if (status === 403) return message || 'Không có quyền hoặc module chưa được bật.';
  if (status === 409) return 'Phiên này đã được ghi với nội dung khác. Hãy bắt đầu nhận định mới.';
  if (!err.response) return 'API không phản hồi.';
  return message || 'Yêu cầu không thành công.';
}

function sourceLabel(source?: string | null): string {
  const value = source?.trim().toUpperCase();
  if (value === 'FIXTURE') return 'Dữ liệu kiểm thử';
  if (value === 'IMPORT') return 'CSV do người dùng nhập';
  if (value === 'EXCHANGE') return 'Đồng bộ từ sàn';
  if (value === 'MIXED') return 'Nhiều nguồn';
  return source || 'Chưa ghi nhận';
}

function ledgerText(fixture: number, csv: number, exchange: number): string {
  const parts = [
    fixture > 0 ? 'kiểm thử' : '',
    csv > 0 ? 'CSV' : '',
    exchange > 0 ? 'sàn' : '',
  ].filter(Boolean);
  if (parts.length === 0) return 'Chưa có giao dịch';
  if (parts.length > 1) return 'Sổ đang có nhiều nguồn';
  if (fixture > 0) return 'Dữ liệu kiểm thử';
  if (csv > 0) return 'CSV do người dùng nhập';
  return 'Đồng bộ từ sàn';
}

function SyncReport({ result, pending }: { result: ExchangeSyncResult; pending?: boolean }) {
  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Typography.Text>
        {result.category} · {pending ? 'Sẽ ghi' : 'Đã ghi'} {result.insertedCount}. Trùng {result.duplicateCount}. Không ghi {result.unmappedCount}. Đã lấy {result.fetchedCount} execution trên {result.pages} trang, mỗi trang tối đa {result.pageLimit}.
      </Typography.Text>
      <Typography.Text>
        Khoảng yêu cầu {when(result.requestedStart)} → {when(result.requestedEnd)}.
        {' '}Sàn trả {result.earliestExecAt ? `${when(result.earliestExecAt)} → ${when(result.latestExecAt)}` : 'không có execution trong phần đã lấy'}.
      </Typography.Text>
      {result.truncated && (
        <Alert
          type="warning"
          showIcon
          message="Chưa lấy hết khoảng thời gian đã chọn."
          description={result.resumeBefore ? `Đặt mốc kết thúc tại ${when(result.resumeBefore)} để lấy phần cũ hơn. Dòng đã lưu không bị ghi đè.` : 'Hãy thu hẹp khoảng thời gian rồi đồng bộ lại.'}
        />
      )}
      {result.missingFields.length > 0 && (
        <Typography.Text>Trường thiếu trên dòng ghi được: {result.missingFields.join(', ')}.</Typography.Text>
      )}
      {result.issues.slice(0, 8).map((issue) => (
        <Typography.Text key={`${issue.execId ?? 'none'}-${issue.reason}`} type="secondary">
          {issue.execId || 'Không có execId'} · {issue.execType || 'không rõ loại'} · {issue.reason}
        </Typography.Text>
      ))}
      {result.notes.map((note) => <Typography.Text key={note} type="secondary">{note}</Typography.Text>)}
    </Space>
  );
}

function riskSentence(reason: string): string {
  if (reason.includes('Notional')) return 'Quy mô lệnh vượt trần đã đặt.';
  if (reason.includes('Leverage')) return 'Đòn bẩy vượt trần đã đặt.';
  if (reason.includes('Confidence')) return 'Mức tin cậy thấp hơn ngưỡng đã đặt.';
  if (reason.includes('Live')) return 'Không gửi lệnh thật.';
  if (reason.includes('Quantity')) return 'Thiếu giá hoặc số lượng.';
  if (reason.includes('Symbol')) return 'Thiếu mã giao dịch.';
  if (reason.includes('Side')) return 'Chiều giao dịch không hợp lệ.';
  return reason;
}

function when(value?: string | null): string {
  return value ? new Date(value).toLocaleString('vi-VN') : 'Chưa ghi nhận';
}

export function TradingSessionPage() {
  const navigate = useNavigate();
  const { modal } = App.useApp();
  const [params] = useSearchParams();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const permissions = useAuthStore((s) => s.user?.permissions ?? []);
  const canApprove = roles.includes('ADMIN') || permissions.includes('personal_intelligence.trading.approve');
  const canConnect = roles.includes('ADMIN') || permissions.includes('personal_intelligence.exchange.connect');
  const canReadExchange = canConnect || permissions.includes('personal_intelligence.exchange.read');

  const [summary, setSummary] = useState<TradingSummary | null>(null);
  const [accounts, setAccounts] = useState<TradingAccount[]>([]);
  const [accountId, setAccountId] = useState<string>();
  const [fixtureCount, setFixtureCount] = useState(0);
  const [historyCount, setHistoryCount] = useState(0);
  const [exchangeCount, setExchangeCount] = useState(0);
  const [tradeSource, setTradeSource] = useState<string>();
  const [syncCategory, setSyncCategory] = useState('linear');
  const [syncFrom, setSyncFrom] = useState(() => new Date(Date.now() - 6 * 86400000).toISOString().slice(0, 10));
  const [syncTo, setSyncTo] = useState(() => new Date().toISOString().slice(0, 10));
  const [syncPreview, setSyncPreview] = useState<ExchangeSyncResult | null>(null);
  const [syncResult, setSyncResult] = useState<ExchangeSyncResult | null>(null);
  const [latestSync, setLatestSync] = useState<ExchangeSyncResult | null>(null);
  const [connections, setConnections] = useState<ExchangeConnection[]>([]);
  const [accountView, setAccountView] = useState<ExchangeAccount | null>(null);
  const [exchangeNote, setExchangeNote] = useState<string | null>(null);
  const [trades, setTrades] = useState<TradeRow[]>([]);
  const [tradeTotal, setTradeTotal] = useState(0);
  const [symbol, setSymbol] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [picked, setPicked] = useState<TradeRow[]>([]);
  const [showImport, setShowImport] = useState(false);
  const [provenance, setProvenance] = useState<'IMPORT' | 'FIXTURE' | ''>('');
  const [csv, setCsv] = useState('');
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [importMessage, setImportMessage] = useState<string | null>(null);
  const [thesis, setThesis] = useState('');
  const [invalidation, setInvalidation] = useState('');
  const [confidence, setConfidence] = useState<number>(0.6);
  const [maxNotional, setMaxNotional] = useState<number>(1000);
  const [maxLeverage, setMaxLeverage] = useState<number>(2);
  const [minConfidence, setMinConfidence] = useState<number>(0.55);
  const [operationKey, setOperationKey] = useState(() => crypto.randomUUID().replace(/-/g, ''));
  const [result, setResult] = useState<LoopResult | null>(null);
  const [checked, setChecked] = useState<{ notional: number; confidence: number; maxNotional: number; maxLeverage: number; minConfidence: number } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const account = accounts.find((item) => item.tradingAccountId === accountId);
  const liveConnection = connections.find((item) => item.status !== 'DISCONNECTED') ?? null;

  const loadTrades = (id = accountId, source = tradeSource, fromDay = from, toDay = to) => {
    if (!id) return Promise.resolve();
    return tradingApi.trades({
      page: 1,
      pageSize: 30,
      tradingAccountId: id,
      source: source || undefined,
      symbol: symbol || undefined,
      from: fromDay ? new Date(fromDay).toISOString() : undefined,
      to: toDay ? new Date(new Date(toDay).getTime() + 86400000).toISOString() : undefined,
    }).then((page) => {
      setTrades(page.items);
      setTradeTotal(page.total);
    });
  };

  const loadScope = (id: string) => Promise.all([
    tradingApi.trades({ page: 1, pageSize: 1, tradingAccountId: id, source: 'FIXTURE' }),
    tradingApi.trades({ page: 1, pageSize: 1, tradingAccountId: id, source: 'IMPORT' }),
    tradingApi.trades({ page: 1, pageSize: 1, tradingAccountId: id, source: 'EXCHANGE' }),
  ]).then(([fixture, history, exchange]) => {
    setFixtureCount(fixture.total);
    setHistoryCount(history.total);
    setExchangeCount(exchange.total);
  });

  const loadExchange = () => {
    if (!canReadExchange) {
      setExchangeNote('Tài khoản này không xem được kết nối sàn.');
      return Promise.resolve();
    }
    return exchangeApi.list().then(async (list) => {
      setConnections(list);
      const live = list.find((item) => item.status !== 'DISCONNECTED');
      if (!live) {
        setAccountView(null);
        return;
      }
      setAccountView(await exchangeApi.account(live.connectionId));
      setLatestSync(await exchangeApi.latestExecutions(live.connectionId));
    }).catch((err) => setExchangeNote(errorText(err)));
  };

  useEffect(() => {
    tradingApi.summary().then(setSummary).catch((err) => setError(errorText(err)));
    tradingApi.accounts().then((rows) => {
      setAccounts(rows);
      const requested = params.get('account');
      const initial = rows.find((row) => row.tradingAccountId === requested)?.tradingAccountId ?? rows[0]?.tradingAccountId;
      if (initial) setAccountId(initial);
    }).catch((err) => setError(errorText(err)));
    void loadExchange();
  }, []);

  useEffect(() => {
    if (!accountId) return;
    setPicked([]);
    setResult(null);
    void loadScope(accountId);
    void loadTrades(accountId);
  }, [accountId]);

  const totalTrades = fixtureCount + historyCount + exchangeCount;
  const ledgerLabel = ledgerText(fixtureCount, historyCount, exchangeCount);

  const latest = useMemo(
    () => [...picked].sort((a, b) => +new Date(b.executedAt) - +new Date(a.executedAt))[0],
    [picked],
  );
  const mixedSymbols = new Set(picked.map((row) => row.symbol)).size > 1;
  const missingFee = trades.filter((row) => row.feeAmount == null).length;

  let next = { text: 'Chọn một sổ để bắt đầu.', action: 'Chọn sổ', target: 'session-ledger' };
  if (!liveConnection && canConnect) next = { text: 'Nối Bybit chỉ đọc để xem số dư và đồng bộ lịch sử khớp lệnh.', action: 'Mở kết nối', target: 'session-connect' };
  else if (totalTrades === 0 && liveConnection) next = { text: 'Đồng bộ lịch sử khớp lệnh Bybit. CSV chỉ dùng khi dữ liệu không có trên sàn.', action: 'Tới đồng bộ', target: 'session-sync' };
  else if (totalTrades === 0 && accountId) next = { text: 'Sổ này chưa có giao dịch. Hãy nhập và chọn rõ đây là CSV của bạn hay dữ liệu kiểm thử.', action: 'Nhập dữ liệu', target: 'session-import' };
  else if (picked.length === 0 && tradeTotal > 0) next = { text: 'Chọn giao dịch đã lưu để làm dữ liệu quan sát.', action: 'Chọn giao dịch', target: 'session-trades' };
  else if (picked.length > 0 && !result) next = { text: 'Xác nhận nhận định cho các giao dịch đã chọn.', action: 'Tới nhận định', target: 'session-thesis' };
  else if (result?.riskAllowed && !result.paperExecuted && canApprove) next = { text: 'Rủi ro cho phép. Giả lập là bước riêng và không gửi lệnh lên sàn.', action: 'Tới giả lập', target: 'session-paper' };
  else if (result) next = { text: 'Mở đúng nhận định vừa ghi trong nhật ký.', action: 'Mở nhật ký', target: 'session-paper' };

  const submit = async (reuseKey: boolean) => {
    if (!account || !latest) { setError('Chọn giao dịch đã lưu trước.'); return; }
    if (mixedSymbols) { setError('Chọn các giao dịch cùng một mã.'); return; }
    if (!thesis.trim() || !invalidation.trim()) { setError('Cần nhận định và điều khiến nhận định sai.'); return; }
    const key = reuseKey ? operationKey : crypto.randomUUID().replace(/-/g, '');
    if (!reuseKey) setOperationKey(key);
    setChecked({
      notional: latest.quantity * latest.price,
      confidence,
      maxNotional,
      maxLeverage,
      minConfidence,
    });
    setBusy(true);
    setError(null);
    try {
      const nextResult = await tradingApi.runLoop({
        personId: account.personId,
        tradingAccountId: account.tradingAccountId,
        fixtureCsv: '',
        thesis: thesis.trim(),
        invalidation: invalidation.trim(),
        expectedOutcome: null,
        symbol: latest.symbol,
        side: latest.side,
        quantity: latest.quantity,
        price: latest.price,
        leverage: 1,
        confidence,
        maxNotional,
        maxLeverage,
        minConfidence,
        reflectionNote: '',
        policyRule: '',
        operationKey: key,
        live: false,
        tradeIds: picked.map((row) => row.tradeId),
      });
      setResult(nextResult);
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  const previewImport = () => {
    if (!accountId || !csv || !provenance) return;
    setBusy(true);
    setError(null);
    tradingApi.previewImport(accountId, csv, provenance)
      .then(setPreview)
      .catch((err) => setError(errorText(err)))
      .finally(() => setBusy(false));
  };

  const commitImport = () => {
    if (!accountId || !csv || !provenance || !preview) return;
    setBusy(true);
    setError(null);
    tradingApi.commitImport(accountId, csv, provenance).then((committed) => {
      setImportMessage(`Hợp lệ ${preview.acceptedCount}. Trùng ${preview.duplicateCount}. Lỗi 0. Đã ghi ${committed.insertedCount}. Import không tạo nhận định.`);
      setCsv('');
      setPreview(null);
      setShowImport(false);
      return Promise.all([loadScope(accountId), loadTrades(accountId), tradingApi.summary().then(setSummary)]);
    }).catch((err) => setError(errorText(err))).finally(() => setBusy(false));
  };

  const syncBody = () => ({
    category: syncCategory,
    start: new Date(`${syncFrom}T00:00:00`).toISOString(),
    end: new Date(`${syncTo}T23:59:59`).toISOString(),
  });

  const previewSync = () => {
    if (!liveConnection) return;
    setBusy(true);
    setError(null);
    exchangeApi.previewExecutions(liveConnection.connectionId, syncBody())
      .then((result) => { setSyncPreview(result); setSyncResult(null); })
      .catch((err) => setError(errorText(err)))
      .finally(() => setBusy(false));
  };

  const commitSync = async () => {
    if (!liveConnection || !syncPreview) return;
    setBusy(true);
    setError(null);
    try {
      const result = await exchangeApi.syncExecutions(liveConnection.connectionId, syncBody());
      setSyncResult(result);
      setLatestSync(result);
      setSyncPreview(null);
      setShowImport(false);
      if (result.tradingAccountId) {
        setAccounts(await tradingApi.accounts());
        setFrom(syncFrom);
        setTo(syncTo);
        setTradeSource('EXCHANGE');
        setPicked([]);
        setAccountId(result.tradingAccountId);
        await loadScope(result.tradingAccountId);
        await loadTrades(result.tradingAccountId, 'EXCHANGE', syncFrom, syncTo);
        document.getElementById('session-trades')?.scrollIntoView({ behavior: 'smooth' });
      }
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  };

  const approve = () => {
    if (!result) return;
    modal.confirm({
      title: 'Ghi giả lập?',
      content: 'Đây là mô phỏng trên dữ liệu đã lưu. Không gửi lệnh lên sàn.',
      okText: 'Phê duyệt giả lập',
      cancelText: 'Để sau',
      onOk: () => tradingApi.approvePaper(result.decisionId).then(() => {
        setResult({ ...result, paperExecuted: true, executionMode: 'PAPER' });
      }),
    });
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {error && <Alert type="error" showIcon message={error} />}
      <Card>
        <Space direction="vertical" style={{ width: '100%' }}>
          <Typography.Text strong>Phiên làm việc</Typography.Text>
          <Typography.Text>
            Kết nối sàn: {liveConnection ? `${liveConnection.exchangeCode} ${liveConnection.environment}` : 'Chưa có'}
            {' · '}cập nhật {when(accountView?.capturedAt ?? liveConnection?.lastCheckedAt)}
          </Typography.Text>
          <Typography.Text>
            Sổ: {account ? `${account.exchangeCode} · ${account.externalAccountId}` : 'Chưa chọn'} · {ledgerLabel}
            {' · '}{totalTrades} giao dịch
            {missingFee > 0 ? ` · ${missingFee} dòng trên trang này chưa có phí` : ''}
            {summary?.missingData.length ? ` · ${summary.missingData.map((item) => item.includes('FIXTURE') ? 'Toàn bộ giao dịch đang là dữ liệu kiểm thử.' : item).join(' ')}` : ''}
          </Typography.Text>
          <Space wrap>
            <Typography.Text>Bước tiếp theo: {next.text}</Typography.Text>
            <Button type="primary" onClick={() => document.getElementById(next.target)?.scrollIntoView({ behavior: 'smooth' })}>{next.action}</Button>
          </Space>
        </Space>
      </Card>

      <Card id="session-connect" title="1. Đọc tài khoản sàn">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Alert type="info" showIcon message="Kết nối Bybit, Binance hoặc OKX chỉ để đọc. Lần kết nối thành công sẽ xếp hàng đồng bộ lịch sử theo từng Spot và Futures. Kết nối xong chưa có nghĩa là lịch sử đã tải xong. Không đặt lệnh, không chuyển tiền, không rút tiền. Đồng bộ không tạo nhận định." />
          {exchangeNote && <Alert type="warning" showIcon message={exchangeNote} />}
          {!liveConnection && (
            <Space>
              <Typography.Text>Chưa có kết nối đang dùng. Chỉ đọc, không đặt lệnh.</Typography.Text>
              {canConnect
                ? <Link to="/second-brain/exchange"><Button type="primary">Mở cấu hình kết nối</Button></Link>
                : <Typography.Text type="secondary">Tài khoản này không được thêm kết nối.</Typography.Text>}
            </Space>
          )}
          {liveConnection && (
            <Space direction="vertical">
              <Space wrap>
                <Tag color={liveConnection.lastProbeStatus === 'REJECTED' ? 'red' : 'green'}>
                  {liveConnection.readOnlyVerified ? 'Chỉ đọc đã xác minh' : 'Đã kết nối'}
                </Tag>
                <span>Kiểm tra tài khoản: {when(liveConnection.lastCheckedAt)}</span>
                <span>Lịch sử khớp lệnh: {latestSync?.createdAt ? when(latestSync.createdAt) : 'Chưa đồng bộ'}</span>
                {liveConnection.lastError && <Typography.Text type="danger">{liveConnection.lastError}</Typography.Text>}
              </Space>
              {accountView && (
                <Descriptions column={1} size="small">
                  <Descriptions.Item label="Số dư ví">{accountView.totalWalletBalance ?? 'Chưa được sàn cung cấp'}</Descriptions.Item>
                  <Descriptions.Item label="Equity">{accountView.totalEquity ?? 'Chưa được sàn cung cấp'}</Descriptions.Item>
                  <Descriptions.Item label="Snapshot">{when(accountView.capturedAt)} · {accountView.freshness === 'STALE' ? 'Đã cũ — nên làm mới' : accountView.freshness === 'FRESH' ? 'Mới' : 'Chưa có snapshot mới'}</Descriptions.Item>
                </Descriptions>
              )}
              <Space>
                {canConnect && <Button onClick={() => { setBusy(true); exchangeApi.refresh(liveConnection.connectionId).then(setAccountView).catch((err) => setError(errorText(err))).finally(() => setBusy(false)); }}>Làm mới số dư</Button>}
                <Link to="/second-brain/account">Xem tài khoản</Link>
              </Space>
              <Space id="session-sync" direction="vertical" style={{ width: '100%' }}>
                <Typography.Text strong>Đồng bộ lịch sử khớp lệnh</Typography.Text>
                <Typography.Text type="secondary">
                  Form này là mẫu có giới hạn 31 ngày và 10 trang cho Bybit. Lịch sử dài hơn chạy bằng tác vụ nền trên Spot và Futures, có checkpoint và không dừng ở 10 trang. Xem trước chưa ghi sổ.
                </Typography.Text>
                <Space>
                  <Link to="/second-brain/spot">Mở Spot</Link>
                  <Link to="/second-brain/futures">Mở Futures</Link>
                </Space>
                <Space wrap>
                  <Select
                    aria-label="Loại sản phẩm"
                    style={{ width: 240 }}
                    value={syncCategory}
                    options={executionCategories}
                    onChange={(value) => { setSyncCategory(value); setSyncPreview(null); }}
                  />
                  <Input type="date" aria-label="Đồng bộ từ ngày" value={syncFrom} onChange={(event) => { setSyncFrom(event.target.value); setSyncPreview(null); }} />
                  <Input type="date" aria-label="Đồng bộ đến ngày" value={syncTo} onChange={(event) => { setSyncTo(event.target.value); setSyncPreview(null); }} />
                </Space>
                {canConnect ? (
                  <Space>
                    <Button disabled={!syncFrom || !syncTo || busy} onClick={previewSync}>Xem trước</Button>
                    <Button type="primary" disabled={!syncPreview || busy} onClick={() => void commitSync()}>Đồng bộ vào sổ</Button>
                  </Space>
                ) : <Typography.Text type="secondary">Tài khoản này xem được kết nối nhưng không được đồng bộ.</Typography.Text>}
                {syncPreview && <SyncReport result={syncPreview} pending />}
                {syncResult && <SyncReport result={syncResult} />}
                {!syncResult && latestSync?.truncated && <Alert type="warning" showIcon message="Lần đồng bộ gần nhất chưa lấy hết khoảng đã chọn." />}
              </Space>
            </Space>
          )}
        </Space>
      </Card>

      <Card id="session-ledger" title="2. Chọn sự thật của sổ">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Select
            placeholder="Chọn sổ"
            style={{ width: 360 }}
            value={accountId}
            options={accounts.map((item) => ({ value: item.tradingAccountId, label: `${item.exchangeCode} · ${item.externalAccountId} · ${item.personName}` }))}
            onChange={setAccountId}
          />
          <Typography.Text>Phân loại hiện có: {ledgerLabel}. Kiểm thử {fixtureCount}. CSV {historyCount}. Đồng bộ từ sàn {exchangeCount}.</Typography.Text>
          {(totalTrades === 0 && !liveConnection) || showImport ? (
            <Space id="session-import" direction="vertical" style={{ width: '100%' }}>
              <Alert type="warning" showIcon message="Chọn ý nghĩa dữ liệu trước khi ghi. Không có lựa chọn mặc định. Import không tạo nhận định." />
              <Radio.Group value={provenance} onChange={(event) => { setProvenance(event.target.value); setPreview(null); }}>
                <Radio value="IMPORT">CSV do người dùng nhập</Radio>
                <Radio value="FIXTURE">Dữ liệu kiểm thử</Radio>
              </Radio.Group>
              <Input.TextArea rows={5} value={csv} placeholder="external_trade_id,symbol,side,quantity,price,executed_at,fee_amount,fee_currency" onChange={(event) => { setCsv(event.target.value); setPreview(null); }} />
              <Space>
                <Button disabled={!accountId || !csv || !provenance || busy} onClick={previewImport}>Xem trước</Button>
                <Button type="primary" disabled={!preview || busy} onClick={commitImport}>Ghi vào sổ</Button>
              </Space>
              {preview && <Typography.Text>Hợp lệ {preview.acceptedCount}. Trùng {preview.duplicateCount}. Lỗi {error ? 1 : 0}.</Typography.Text>}
            </Space>
          ) : (
            <Button onClick={() => setShowImport(true)}>Nhập CSV vào sổ này</Button>
          )}
          {importMessage && <Alert type="success" showIcon message={importMessage} />}
        </Space>
      </Card>

      <Card id="session-trades" title="3. Chọn dữ liệu quan sát">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Typography.Text>Các dòng dưới đây là giao dịch đã lưu, chưa phải nhận định. Không cần dán lại CSV.</Typography.Text>
          <Space wrap>
            <Select
              allowClear
              placeholder="Nguồn"
              aria-label="Nguồn giao dịch"
              style={{ width: 200 }}
              value={tradeSource}
              options={[
                { value: 'FIXTURE', label: 'Dữ liệu kiểm thử' },
                { value: 'IMPORT', label: 'CSV do người dùng nhập' },
                { value: 'EXCHANGE', label: 'Đồng bộ từ sàn' },
              ]}
              onChange={(value) => setTradeSource(value)}
            />
            <Input placeholder="Mã" value={symbol} onChange={(event) => setSymbol(event.target.value)} style={{ width: 140 }} />
            <Input type="date" aria-label="Từ ngày" value={from} onChange={(event) => setFrom(event.target.value)} />
            <Input type="date" aria-label="Đến ngày" value={to} onChange={(event) => setTo(event.target.value)} />
            <Button onClick={() => void loadTrades().catch((err) => setError(errorText(err)))}>Lọc</Button>
            <Link to="/second-brain/ledger">Mở sổ đầy đủ</Link>
          </Space>
          <Table
            rowKey="tradeId"
            size="small"
            dataSource={trades}
            pagination={false}
            locale={{ emptyText: 'Chưa có giao dịch trong phạm vi này.' }}
            rowSelection={{
              selectedRowKeys: picked.map((row) => row.tradeId),
              onChange: (_keys, rows) => {
                const visible = new Set(trades.map((row) => row.tradeId));
                setPicked([...picked.filter((row) => !visible.has(row.tradeId)), ...(rows as TradeRow[])]);
              },
            }}
            columns={[
              { title: 'Thời gian', dataIndex: 'executedAt', render: (value: string) => when(value) },
              { title: 'Mã', dataIndex: 'symbol' },
              { title: 'Chiều', dataIndex: 'side' },
              { title: 'Giá', dataIndex: 'price' },
              { title: 'Số lượng', dataIndex: 'quantity' },
              { title: 'Phí', render: (_: unknown, row: TradeRow) => row.feeAmount == null ? 'Chưa có' : `${row.feeAmount} ${row.feeCurrency ?? ''}` },
              { title: 'Nguồn', dataIndex: 'source', render: (value: string, row: TradeRow) => <Tag color={value === 'EXCHANGE' ? 'green' : value === 'FIXTURE' ? 'gold' : 'blue'}>{sourceLabel(value)}{row.category ? ` · ${row.category}` : ''}</Tag> },
            ]}
          />
          <Typography.Text type="secondary">Đang hiện {trades.length} / {tradeTotal} giao dịch của sổ này.</Typography.Text>
          {picked.length > 0 && (
            <Alert
              type="info"
              showIcon
              message={`Đã chọn ${picked.length} giao dịch vì bạn đánh dấu chúng trong sổ ${account?.externalAccountId ?? ''}.`}
              description={mixedSymbols
                ? 'Các giao dịch không cùng mã. Hãy chọn lại một mã trước khi xác nhận nhận định.'
                : `Nhận định sẽ dùng lệnh gần nhất: ${latest?.side} ${latest?.quantity} ${latest?.symbol} @ ${latest?.price}. Đây vẫn là dữ liệu quan sát.`}
            />
          )}
        </Space>
      </Card>

      <Card id="session-thesis" title="4. Xác nhận nhận định">
        <Space direction="vertical" style={{ width: '100%' }}>
          <Input placeholder="Bạn dự định làm gì, hoặc muốn kiểm chứng điều gì?" value={thesis} onChange={(event) => setThesis(event.target.value)} />
          <Input placeholder="Điều gì khiến nhận định này sai?" value={invalidation} onChange={(event) => setInvalidation(event.target.value)} />
          <Space>
            <span>Mức tin cậy</span>
            <InputNumber min={0} max={1} step={0.05} value={confidence} onChange={(value) => setConfidence(Number(value ?? 0))} />
          </Space>
          <Collapse items={[{
            key: 'limits',
            label: 'Trần rủi ro',
            children: (
              <Space wrap>
                <span>Trần quy mô</span><InputNumber min={0} value={maxNotional} onChange={(value) => setMaxNotional(Number(value ?? 0))} />
                <span>Trần đòn bẩy</span><InputNumber min={0} value={maxLeverage} onChange={(value) => setMaxLeverage(Number(value ?? 0))} />
                <span>Ngưỡng tin cậy</span><InputNumber min={0} max={1} step={0.05} value={minConfidence} onChange={(value) => setMinConfidence(Number(value ?? 0))} />
              </Space>
            ),
          }]} />
          <Space>
            <Button type="primary" loading={busy} disabled={picked.length === 0 || mixedSymbols} onClick={() => void submit(false)}>Xác nhận nhận định</Button>
            {error === 'API không phản hồi.' && <Button loading={busy} onClick={() => void submit(true)}>Gửi lại</Button>}
            <Button onClick={() => { setResult(null); setOperationKey(crypto.randomUUID().replace(/-/g, '')); }}>Nhận định mới</Button>
          </Space>
        </Space>
      </Card>

      {result && (
        <Card id="session-result" title="5. Bằng chứng và rủi ro">
          <Descriptions column={1}>
            <Descriptions.Item label="Dữ liệu đã quan sát">
              {picked.map((row) => `${row.symbol} ${row.side} ${row.quantity} @ ${row.price}`).join('; ') || 'Chưa có'}
              . Không có dữ liệu thị trường live trong lần đối chiếu này.
            </Descriptions.Item>
            <Descriptions.Item label="Nhận định của bạn">{thesis}</Descriptions.Item>
            <Descriptions.Item label="Rủi ro">
              {result.riskAllowed
                ? `Cho phép. Đã kiểm tra quy mô ${checked?.notional ?? ''} so với trần ${checked?.maxNotional ?? ''}, đòn bẩy 1 so với trần ${checked?.maxLeverage ?? ''}, và mức tin cậy ${checked?.confidence ?? ''} so với ngưỡng ${checked?.minConfidence ?? ''}.`
                : `Từ chối. ${(result.riskReasons ?? []).map(riskSentence).join(' ') || 'Chưa có lý do chi tiết.'}`}
            </Descriptions.Item>
            <Descriptions.Item label="Điều chưa biết">{result.unknowns.length ? result.unknowns.join(' ') : 'Chưa ghi nhận'}</Descriptions.Item>
            <Descriptions.Item label="Kết quả hiện có">{result.paperExecuted ? 'Đã ghi giả lập. Không gửi lệnh lên sàn.' : 'Chưa ghi giả lập.'}</Descriptions.Item>
          </Descriptions>
        </Card>
      )}

      {result && (
        <Card id="session-paper" title="6. Giả lập và nhật ký của phiên này">
          <Space direction="vertical">
            {!result.riskAllowed && <Alert type="warning" showIcon message="Rủi ro từ chối nên không có bước giả lập." />}
            {result.riskAllowed && !result.paperExecuted && canApprove && (
              <Button type="primary" onClick={approve}>Phê duyệt giả lập</Button>
            )}
            {result.riskAllowed && !result.paperExecuted && !canApprove && (
              <Alert type="info" showIcon message="Tài khoản này được chạy phân tích nhưng chưa được phê duyệt giả lập." />
            )}
            {result.paperExecuted && <Alert type="success" showIcon message="Đã ghi giả lập. Không có lệnh nào được gửi lên sàn." />}
            <Button onClick={() => navigate(`/second-brain/journal?decision=${result.decisionId}`)}>Mở nhận định vừa ghi</Button>
            <Typography.Text type="secondary">Phản tư và quy tắc không bắt buộc. Nếu chưa ghi, nhật ký sẽ nói rõ chưa ghi nhận.</Typography.Text>
          </Space>
        </Card>
      )}
    </Space>
  );
}
