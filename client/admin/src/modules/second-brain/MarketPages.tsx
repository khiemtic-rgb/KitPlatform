import { useEffect, useState } from 'react';
import { Alert, Button, Card, Select, Space, Statistic, Table, Typography } from 'antd';
import { exchangeApi, type BackfillJob } from './exchange.api';
import { tradingApi, type TradeRow, type TradingAccount } from './trading.api';

function errorText(error: unknown): string {
  const err = error as { response?: { status?: number; data?: { message?: string } }; message?: string };
  if (!err.response) return 'API không phản hồi.';
  if (err.response.status === 404) return 'API đang chạy chưa có endpoint thị trường này. Hãy khởi động lại API kiểm thử Second Brain sau bản dựng mới.';
  return err.response.data?.message || 'Yêu cầu không thành công.';
}

const subtypes: Record<string, { value: string; label: string }[]> = {
  SPOT: [
    { value: 'spot', label: 'Spot' },
  ],
  FUTURES: [
    { value: 'linear', label: 'Bybit linear' },
    { value: 'inverse', label: 'Bybit inverse' },
    { value: 'usd_m', label: 'Binance USD-M' },
    { value: 'coin_m', label: 'Binance COIN-M' },
    { value: 'swap', label: 'OKX SWAP' },
    { value: 'expiry', label: 'OKX FUTURES có kỳ hạn' },
  ],
};

export function TradingSpotPage() {
  return <MarketBoard marketType="SPOT" title="Spot" note="Số lượng và phí giữ đơn vị sàn trả về. Spot không dùng công thức PnL của hợp đồng." />;
}

export function TradingFuturesPage() {
  return <MarketBoard marketType="FUTURES" title="Futures" note="Không tính PnL bằng chênh lệch giá nhân số lượng. Chỉ hiện realized PnL khi API khớp lệnh trả về. Linear, inverse, USD-M, COIN-M, SWAP và kỳ hạn được giữ riêng." />;
}

function MarketBoard({ marketType, title, note }: { marketType: 'SPOT' | 'FUTURES'; title: string; note: string }) {
  const [accounts, setAccounts] = useState<TradingAccount[]>([]);
  const [jobs, setJobs] = useState<BackfillJob[]>([]);
  const [rows, setRows] = useState<TradeRow[]>([]);
  const [total, setTotal] = useState(0);
  const [accountId, setAccountId] = useState<string>();
  const [subtype, setSubtype] = useState<string>();
  const [symbol, setSymbol] = useState<string>();
  const [error, setError] = useState<string | null>(null);

  async function load(nextAccount = accountId, nextSubtype = subtype, nextSymbol = symbol) {
    const [jobRows, tradePage] = await Promise.all([
      exchangeApi.backfill(marketType),
      tradingApi.trades({
        page: 1,
        pageSize: 50,
        marketType,
        tradingAccountId: nextAccount,
        productSubtype: nextSubtype,
        symbol: nextSymbol,
      }),
    ]);
    setJobs(jobRows);
    setRows(tradePage.items);
    setTotal(tradePage.total);
  }

  useEffect(() => {
    tradingApi.accounts().then(setAccounts).catch(() => undefined);
  }, []);

  useEffect(() => {
    let cancelled = false;
    const refresh = () => load(accountId, subtype, symbol)
      .catch((err) => { if (!cancelled) setError(errorText(err)); });
    refresh();
    const timer = window.setInterval(refresh, 5000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [marketType, accountId, subtype, symbol]);

  const visibleJobs = jobs.filter((job) => !subtype || job.productSubtype === subtype);
  const running = visibleJobs.some((job) => job.status === 'QUEUED' || job.status === 'RUNNING' || job.status === 'INTERRUPTED');

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Alert type="info" showIcon message={title} description={note} />
      {running && <Alert type="warning" showIcon message="Đang đồng bộ. Số liệu trên bảng là phần đã ghi, chưa phải toàn bộ lịch sử." />}
      {error && <Alert type="error" showIcon message={error} />}
      <Space wrap>
        {visibleJobs.map((job) => (
          <Card key={job.jobId} size="small" title={`${job.exchangeCode} · ${job.productSubtype}`}>
            <Statistic title={job.status} value={job.insertedCount} suffix="dòng mới" />
            <Typography.Text type="secondary">Trùng {job.duplicateCount}. Bỏ qua {job.skippedCount}. Lỗi {job.errorCount}. Trang {job.pages}.</Typography.Text>
            <div>{job.lastError || job.cutoffReason || 'Chưa có mốc cutoff.'}</div>
          </Card>
        ))}
      </Space>
      <Space wrap>
        <Select allowClear placeholder="Tài khoản" style={{ width: 260 }}
          options={accounts.map((account) => ({ value: account.tradingAccountId, label: `${account.exchangeCode} · ${account.externalAccountId}` }))}
          onChange={(value) => { setAccountId(value); load(value, subtype, symbol).catch((err) => setError(errorText(err))); }} />
        <Select allowClear placeholder="Subtype" style={{ width: 220 }}
          options={subtypes[marketType]}
          onChange={(value) => { setSubtype(value); load(accountId, value, symbol).catch((err) => setError(errorText(err))); }} />
        <Select allowClear placeholder="Symbol" style={{ width: 180 }}
          options={[...new Set(rows.map((row) => row.symbol))].map((value) => ({ value, label: value }))}
          onChange={(value) => { setSymbol(value); load(accountId, subtype, value).catch((err) => setError(errorText(err))); }} />
        <Button onClick={() => load().catch((err) => setError(errorText(err)))}>Tải lại</Button>
      </Space>
      <Table
        rowKey="tradeId"
        dataSource={rows}
        pagination={false}
        locale={{ emptyText: `Chưa có giao dịch ${title} trong bộ lọc này. ${total === 0 ? '' : ''}` }}
        columns={[
          { title: 'Thời gian', dataIndex: 'executedAt', render: (value: string) => new Date(value).toLocaleString('vi-VN') },
          { title: 'Sàn', dataIndex: 'exchangeCode' },
          { title: 'Tài khoản', dataIndex: 'externalAccountId' },
          { title: 'Subtype', dataIndex: 'productSubtype', render: (value) => value || 'Chưa phân loại' },
          { title: 'Symbol', dataIndex: 'symbol' },
          { title: 'Chiều', dataIndex: 'side' },
          { title: 'Giá', dataIndex: 'price' },
          { title: 'Số lượng', dataIndex: 'quantity' },
          { title: 'Phí', render: (_, row) => row.feeAmount == null ? 'Chưa ghi nhận' : `${row.feeAmount} ${row.feeCurrency ?? ''}` },
          { title: 'PnL sàn', dataIndex: 'realizedPnl', render: (value) => value || 'Không có' },
        ]}
      />
      <Typography.Text type="secondary">{total} dòng trong bộ lọc. Không gộp win rate Spot với Futures.</Typography.Text>
    </Space>
  );
}
