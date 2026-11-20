import { useEffect, useState } from 'react';
import {
  Alert, App, Button, Card, Checkbox, Descriptions, Form, Input, Select, Space, Table, Tag, Typography,
} from 'antd';
import { useAuthStore } from '@/shared/auth/auth.store';
import { exchangeApi, type BackfillJob, type ExchangeAccount, type ExchangeConnection } from './exchange.api';

function errorText(error: unknown): string {
  const err = error as { response?: { status?: number; data?: { message?: string } }; message?: string };
  const status = err.response?.status;
  const message = err.response?.data?.message;
  if (status === 401) return 'Phiên đăng nhập đã hết hạn.';
  if (status === 403) return message || 'Không có quyền quản lý kết nối sàn.';
  if (status === 429) return message || 'Sàn đang giới hạn tần suất.';
  if (status === 503) return message || 'Chưa cấu hình khóa mã hóa. Không lưu credential.';
  if (!err.response) return 'API không phản hồi. Không có dữ liệu tài khoản.';
  return message || 'Yêu cầu không thành công.';
}

function Missing({ value }: { value?: string | number | null }) {
  if (value === 0) return <>0</>;
  return value ? <>{value}</> : <Typography.Text type="secondary">Chưa được sàn cung cấp</Typography.Text>;
}

function freshnessLabel(value: string): string {
  if (value === 'FRESH') return 'Mới';
  if (value === 'STALE') return 'Cũ';
  if (value === 'DISCONNECTED') return 'Đã ngắt kết nối — snapshot được giữ';
  return 'Chưa có snapshot';
}

function statusTag(connection: ExchangeConnection) {
  if (connection.status === 'DISCONNECTED') return <Tag>Đã ngắt</Tag>;
  if (connection.lastProbeStatus === 'REJECTED') return <Tag color="red">Credential bị từ chối</Tag>;
  if (connection.lastProbeStatus === 'BALANCE_UNAVAILABLE') return <Tag color="gold">Đã kết nối, thiếu số dư</Tag>;
  return <Tag color="green">Đã kết nối</Tag>;
}

export function TradingExchangePage() {
  const { modal } = App.useApp();
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const permissions = useAuthStore((s) => s.user?.permissions ?? []);
  const canConnect = roles.includes('ADMIN') || permissions.includes('personal_intelligence.exchange.connect');
  const canRead = canConnect || permissions.includes('personal_intelligence.exchange.read');
  const [form] = Form.useForm();
  const exchangeCode = Form.useWatch('exchangeCode', form);
  const [connections, setConnections] = useState<ExchangeConnection[]>([]);
  const [jobs, setJobs] = useState<BackfillJob[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [tested, setTested] = useState<string | null>(null);

  async function load() {
    const [rows, backfill] = await Promise.all([exchangeApi.list(), exchangeApi.backfill()]);
    setConnections(rows);
    setJobs(backfill);
  }

  useEffect(() => {
    if (!canRead) return;
    load().catch((err) => setError(errorText(err)));
    const timer = window.setInterval(() => {
      load().catch(() => undefined);
    }, 5000);
    return () => window.clearInterval(timer);
  }, [canRead]);

  if (!canRead) {
    return <Alert type="error" showIcon message="Bạn không có quyền xem kết nối sàn." />;
  }

  async function testConnection() {
    const values = await form.validateFields();
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const probe = await exchangeApi.test(values);
      setTested(JSON.stringify(values));
      setNotice(probe.message);
    } catch (err) {
      setTested(null);
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function saveConnection() {
    const values = await form.validateFields();
    if (tested !== JSON.stringify(values)) {
      setError('Hãy kiểm tra kết nối với đúng key này trước khi lưu.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await exchangeApi.save(values);
      form.setFieldValue('apiSecret', '');
      setTested(null);
      form.setFieldValue('passphrase', '');
      setNotice('Đã lưu credential đã mã hóa. Secret và passphrase không được hiển thị lại. Lịch sử đang được xếp hàng, chưa tải xong.');
      await load();
    } catch (err) {
      setError(errorText(err));
    } finally {
      setBusy(false);
    }
  }

  async function refresh(id: string) {
    setBusy(true);
    setError(null);
    try {
      await exchangeApi.refresh(id);
      setNotice('Đã làm mới tài khoản từ sàn.');
      await load();
    } catch (err) {
      setError(errorText(err));
      await load().catch(() => undefined);
    } finally {
      setBusy(false);
    }
  }

  function disconnect(id: string) {
    modal.confirm({
      title: 'Ngắt kết nối sàn?',
      content: 'Credential sẽ bị xóa. Snapshot tài khoản và sổ giao dịch được giữ lại.',
      okText: 'Ngắt kết nối',
      cancelText: 'Hủy',
      onOk: async () => {
        await exchangeApi.disconnect(id);
        setNotice('Đã ngắt kết nối. Snapshot cũ vẫn còn.');
        await load();
      },
    });
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        message="Chỉ đọc Bybit, Binance và OKX. Không đặt lệnh, không chuyển tiền, không rút tiền."
        description="Bybit chỉ lưu khi readOnly=1 và permissions không có Trade, Order, Transfer hoặc Withdraw. Binance từ chối rút và chuyển. Enable Futures hoặc giao dịch Spot khiến luồng đó bị chặn, không được bật để lấy lịch sử. OKX chỉ xác minh khi perm đúng read_only. Thiếu perm thì phải tự xác nhận và lịch sử không chạy. Sau khi lưu, secret và passphrase không hiện lại. Kết nối thành công chưa có nghĩa là lịch sử đã tải xong."
      />
      {error && <Alert type="error" showIcon message={error} />}
      {notice && <Alert type="success" showIcon message={notice} />}
      <Card title="Kết nối sàn">
        <Form
          form={form}
          layout="vertical"
          initialValues={{ exchangeCode: 'BYBIT', environment: 'TESTNET' }}
          onValuesChange={() => setTested(null)}
          style={{ maxWidth: 640 }}
        >
          <Form.Item name="exchangeCode" label="Sàn" rules={[{ required: true }]}>
            <Select options={[{ value: 'BYBIT', label: 'Bybit' }, { value: 'BINANCE', label: 'Binance' }, { value: 'OKX', label: 'OKX' }]} />
          </Form.Item>
          <Form.Item name="environment" label="Môi trường" rules={[{ required: true }]}>
            <Select options={[{ value: 'TESTNET', label: 'Testnet' }, { value: 'MAINNET', label: 'Mainnet' }]} />
          </Form.Item>
          <Form.Item name="apiKey" label="API Key" rules={[{ required: true, min: 8 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item name="apiSecret" label="API Secret" rules={[{ required: true, min: 8 }]}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          {exchangeCode === 'OKX' && (
            <Form.Item name="passphrase" label="Passphrase" rules={[{ required: true, min: 1 }]}>
              <Input.Password autoComplete="new-password" />
            </Form.Item>
          )}
          {exchangeCode !== 'BYBIT' && (
            <Form.Item name="acknowledgeUnverifiedPermissions" valuePropName="checked">
              <Checkbox>
                Tôi xác nhận key không có quyền đặt lệnh, chuyển hoặc rút. Nếu sàn không trả quyền rõ ràng, lịch sử futures không được chạy. Không bật Enable Futures hay Trade để lấy lịch sử.
              </Checkbox>
            </Form.Item>
          )}
          <Space>
            <Button onClick={testConnection} loading={busy} disabled={!canConnect}>Kiểm tra kết nối</Button>
            <Button type="primary" onClick={saveConnection} loading={busy} disabled={!canConnect || !tested}>Lưu kết nối</Button>
          </Space>
          {!canConnect && <Typography.Paragraph type="secondary">Bạn chỉ có quyền xem. Không thể lưu hoặc ngắt kết nối.</Typography.Paragraph>}
        </Form>
      </Card>
      <Card title="Kết nối đã lưu">
        <Table
          rowKey="connectionId"
          dataSource={connections}
          pagination={false}
          locale={{ emptyText: 'Chưa có kết nối sàn.' }}
          columns={[
            { title: 'Sàn', dataIndex: 'exchangeCode' },
            { title: 'Môi trường', dataIndex: 'environment' },
            { title: 'API key', dataIndex: 'apiKeyMask' },
            { title: 'Trạng thái', render: (_, row) => statusTag(row) },
            {
              title: 'Quyền chỉ đọc',
              render: (_, row) => row.readOnlyVerified
                ? <Tag color="green">readOnly=1, không có Withdraw</Tag>
                : <Tag>Chưa có bằng chứng chỉ đọc</Tag>,
            },
            { title: 'Lần kiểm tra', dataIndex: 'lastCheckedAt', render: (value) => value || 'Chưa kiểm tra' },
            { title: 'Lỗi', dataIndex: 'lastError', render: (value) => value || 'Không' },
            {
              title: '',
              render: (_, row) => (
                <Space>
                  <Button size="small" disabled={!canConnect || row.status !== 'CONNECTED'} onClick={() => refresh(row.connectionId)}>Làm mới</Button>
                  <Button size="small" danger disabled={!canConnect || row.status !== 'CONNECTED'} onClick={() => disconnect(row.connectionId)}>Ngắt</Button>
                </Space>
              ),
            },
          ]}
        />
      </Card>
      <Card title="Đồng bộ lịch sử">
        <Typography.Paragraph type="secondary">
          Mỗi dòng là một luồng riêng: sàn, tài khoản, Spot hoặc Futures, và subtype. Lỗi của luồng này không đổi trạng thái luồng kia. PARTIAL nghĩa là đã đi hết phần API trả được, không phải toàn bộ đời tài khoản.
        </Typography.Paragraph>
        <Table
          rowKey="jobId"
          dataSource={jobs}
          pagination={false}
          locale={{ emptyText: 'Chưa có tác vụ đồng bộ.' }}
          columns={[
            { title: 'Sàn', dataIndex: 'exchangeCode' },
            { title: 'Thị trường', render: (_, row) => `${row.marketType} · ${row.productSubtype}` },
            { title: 'Trạng thái', dataIndex: 'status' },
            { title: 'Mới', dataIndex: 'insertedCount' },
            { title: 'Trùng', dataIndex: 'duplicateCount' },
            { title: 'Bỏ qua', dataIndex: 'skippedCount' },
            { title: 'Lỗi', dataIndex: 'errorCount' },
            { title: 'Trang', dataIndex: 'pages' },
            { title: 'Cutoff', dataIndex: 'cutoffAt', render: (value) => value || 'Chưa chạm mốc API' },
            { title: 'Lý do', dataIndex: 'cutoffReason', render: (_, row) => row.lastError || row.cutoffReason || 'Không' },
            {
              title: '',
              render: (_, row) => (
                <Space>
                  {row.status === 'PAUSED' && <Button size="small" disabled={!canConnect} onClick={() => exchangeApi.resumeBackfill(row.jobId).then(load).catch((err) => setError(errorText(err)))}>Tiếp tục</Button>}
                  {(row.status === 'FAILED' || row.status === 'INTERRUPTED' || row.status === 'PARTIAL') && <Button size="small" disabled={!canConnect} onClick={() => exchangeApi.retryBackfill(row.jobId).then(load).catch((err) => setError(errorText(err)))}>Thử lại</Button>}
                  {(row.status === 'QUEUED' || row.status === 'RUNNING') && <Button size="small" disabled={!canConnect} onClick={() => exchangeApi.pauseBackfill(row.jobId).then(load).catch((err) => setError(errorText(err)))}>Tạm dừng</Button>}
                  <Button size="small" disabled={!canConnect} onClick={() => exchangeApi.requestBackfill(row.connectionId, 'INCREMENTAL', row.productSubtype).then(load).catch((err) => setError(errorText(err)))}>Tăng dần</Button>
                  <Button size="small" disabled={!canConnect || row.status === 'QUEUED' || row.status === 'RUNNING' || row.status === 'PAUSED' || row.status === 'INTERRUPTED'} onClick={() => exchangeApi.requestBackfill(row.connectionId, 'FULL', row.productSubtype).then(load).catch((err) => setError(errorText(err)))}>Đồng bộ lại</Button>
                </Space>
              ),
            },
          ]}
        />
      </Card>
    </Space>
  );
}

export function TradingExchangeAccountPage() {
  const roles = useAuthStore((s) => s.user?.roles ?? []);
  const permissions = useAuthStore((s) => s.user?.permissions ?? []);
  const canConnect = roles.includes('ADMIN') || permissions.includes('personal_intelligence.exchange.connect');
  const canRead = canConnect || permissions.includes('personal_intelligence.exchange.read');
  const [connections, setConnections] = useState<ExchangeConnection[]>([]);
  const [selected, setSelected] = useState<string | null>(null);
  const [account, setAccount] = useState<ExchangeAccount | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  async function open(id: string) {
    setSelected(id);
    setAccount(await exchangeApi.account(id));
  }

  useEffect(() => {
    if (!canRead) {
      setLoading(false);
      return;
    }
    exchangeApi.list()
      .then(async (rows) => {
        setConnections(rows);
        if (rows[0]) await open(rows[0].connectionId);
      })
      .catch((err) => setError(errorText(err)))
      .finally(() => setLoading(false));
  }, [canRead]);

  async function refresh() {
    if (!selected) return;
    setError(null);
    try {
      setAccount(await exchangeApi.refresh(selected));
    } catch (err) {
      setError(errorText(err));
      try {
        setAccount(await exchangeApi.account(selected));
      } catch {
        // The failed refresh already explains why a new snapshot was not stored.
      }
    }
  }
  if (!canRead) return <Alert type="error" showIcon message="Bạn không có quyền xem tài khoản sàn." />;
  if (loading) return <Card loading />;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        message="Số liệu dưới đây là phản hồi đọc từ sàn. Màn hình này không tính PnL, lợi nhuận hay số dư suy đoán."
      />
      {error && <Alert type="error" showIcon message={error} />}
      <Card
        title="Tài khoản sàn"
        extra={
          <Space>
            <Select
              style={{ minWidth: 280 }}
              placeholder="Chọn kết nối"
              value={selected ?? undefined}
              options={connections.map((row) => ({
                value: row.connectionId,
                label: `${row.exchangeCode} ${row.environment} ${row.apiKeyMask}`,
              }))}
              onChange={(id) => open(id).catch((err) => setError(errorText(err)))}
            />
            <Button disabled={!canConnect || account?.connection.status !== 'CONNECTED'} onClick={() => refresh()}>
              Làm mới
            </Button>
          </Space>
        }
      >
        {!account && <Typography.Text type="secondary">Chưa có kết nối để hiển thị.</Typography.Text>}
        {account && (
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            {account.connection.status === 'DISCONNECTED' && (
              <Alert type="warning" showIcon message="Kết nối đã bị ngắt. Snapshot bên dưới là dữ liệu cũ, không phải số dư hiện tại." />
            )}
            {account.connection.lastProbeStatus === 'REJECTED' && (
              <Alert type="error" showIcon message={account.connection.lastError || 'Credential bị từ chối hoặc đã bị thu hồi.'} />
            )}
            {account.balanceError && <Alert type="warning" showIcon message={account.balanceError} />}
            <Descriptions column={1} bordered size="small">
              <Descriptions.Item label="Sàn">{account.connection.exchangeCode} · {account.connection.environment}</Descriptions.Item>
              <Descriptions.Item label="Tài khoản sàn"><Missing value={account.connection.externalUserId} /></Descriptions.Item>
              <Descriptions.Item label="API key">{account.connection.apiKeyMask}</Descriptions.Item>
              <Descriptions.Item label="Loại tài khoản"><Missing value={account.accountType} /></Descriptions.Item>
              <Descriptions.Item label="Margin mode"><Missing value={account.marginMode} /></Descriptions.Item>
              <Descriptions.Item label="unifiedMarginStatus"><Missing value={account.unifiedMarginStatus} /></Descriptions.Item>
              <Descriptions.Item label="Equity (totalEquity)"><Missing value={account.totalEquity} /></Descriptions.Item>
              <Descriptions.Item label="Wallet (totalWalletBalance)"><Missing value={account.totalWalletBalance} /></Descriptions.Item>
              <Descriptions.Item label="Available (totalAvailableBalance)"><Missing value={account.totalAvailableBalance} /></Descriptions.Item>
              <Descriptions.Item label="Thời điểm lấy dữ liệu">{account.capturedAt || 'Chưa có snapshot'}</Descriptions.Item>
              <Descriptions.Item label="Độ mới">{freshnessLabel(account.freshness)}</Descriptions.Item>
              <Descriptions.Item label="Nguồn">{account.sourceEndpoints || 'Chưa có'}</Descriptions.Item>
            </Descriptions>
            <Table
              rowKey="coin"
              dataSource={account.assets}
              pagination={false}
              locale={{ emptyText: 'Sàn chưa trả danh mục coin.' }}
              columns={[
                { title: 'Coin', dataIndex: 'coin' },
                { title: 'Wallet balance', dataIndex: 'walletBalance', render: (value) => <Missing value={value} /> },
                { title: 'Equity', dataIndex: 'equity', render: (value) => <Missing value={value} /> },
                { title: 'USD value', dataIndex: 'usdValue', render: (value) => <Missing value={value} /> },
                { title: 'Available to withdraw', dataIndex: 'availableToWithdraw', render: (value) => <Missing value={value} /> },
              ]}
            />
          </Space>
        )}
      </Card>
    </Space>
  );
}
