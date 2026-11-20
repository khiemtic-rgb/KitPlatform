import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, App, Button, Card, Input, Result, Select, Space, Spin, Table, Tag, Typography, Upload } from 'antd';
import { CheckCircleOutlined, UploadOutlined } from '@ant-design/icons';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentBrands,
  fetchContentChannels,
  fetchContentTopics,
  type ContentBrand,
  type ContentChannelTarget,
  type ContentTopic,
} from '@/shared/api/content.api';
import { uploadTopicVideo } from '@/shared/api/content-youtube.api';
import { completeTiktokOAuth, startTiktokOAuth, verifyTiktokChannel } from '@/shared/api/content-tiktok.api';

function linkStatus(configJson: string): string {
  try {
    return (JSON.parse(configJson)?.tiktok?.status as string) ?? 'DISCONNECTED';
  } catch {
    return 'DISCONNECTED';
  }
}

const STATUS_TAG: Record<string, { color: string; text: string }> = {
  CONNECTED: { color: 'green', text: 'Đã kết nối' },
  NEED_RECONNECT: { color: 'red', text: 'Cần kết nối lại' },
  DISCONNECTED: { color: 'default', text: 'Chưa kết nối' },
};

export function ContentTiktokPage() {
  const { message } = App.useApp();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandId, setBrandId] = useState<string | undefined>();
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const list = await fetchContentBrands(true);
        setBrands(list);
        setBrandId((cur) => cur ?? list[0]?.id);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được thương hiệu'));
      }
    })();
  }, [message]);

  const load = useCallback(async () => {
    if (!brandId) return;
    try {
      const [ch, tp] = await Promise.all([fetchContentChannels(brandId), fetchContentTopics({ brandId })]);
      setChannels(ch.filter((c) => c.channelType === 'tiktok_channel'));
      setTopics(tp);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được dữ liệu'));
    }
  }, [brandId, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const connect = async () => {
    if (!brandId) return;
    setBusy('connect');
    try {
      const { url } = await startTiktokOAuth(brandId);
      window.location.href = url;
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không bắt đầu được kết nối TikTok'));
      setBusy(null);
    }
  };

  const verify = async (id: string) => {
    setBusy(id);
    try {
      const r = await verifyTiktokChannel(id);
      if (r.ok) message.success(r.message ?? 'Kết nối còn hiệu lực.');
      else message.warning(r.message ?? 'Cần kết nối lại.');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không kiểm tra được'));
    } finally {
      setBusy(null);
      await load();
    }
  };

  return (
    <div>
      <Space direction="vertical" size={4} style={{ marginBottom: 16 }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          Kênh TikTok
        </Typography.Title>
        <Typography.Text type="secondary">
          Kết nối một lần bằng tài khoản TikTok (không cần mật khẩu). Sau khi bạn bấm Duyệt trong Agent đăng bài,
          video được gửi vào hộp nháp TikTok của bạn; bạn mở TikTok để chỉnh caption và bấm đăng.
        </Typography.Text>
      </Space>

      <Card style={{ marginBottom: 16 }} title="Kết nối">
        <Space wrap style={{ marginBottom: 12 }}>
          <Select
            style={{ minWidth: 240 }}
            placeholder="Thương hiệu"
            value={brandId}
            onChange={setBrandId}
            options={brands.map((b) => ({ value: b.id, label: `${b.name} (${b.code})` }))}
          />
          <Button type="primary" loading={busy === 'connect'} onClick={() => void connect()}>
            {channels.length > 0 ? 'Kết nối lại TikTok' : 'Kết nối TikTok'}
          </Button>
        </Space>
        <Table<ContentChannelTarget>
          rowKey="id"
          size="small"
          pagination={false}
          dataSource={channels}
          locale={{ emptyText: 'Chưa có tài khoản TikTok cho thương hiệu này.' }}
          columns={[
            { title: 'Tài khoản', dataIndex: 'name' },
            { title: 'Open ID', dataIndex: 'externalId', ellipsis: true },
            {
              title: 'Trạng thái',
              render: (_: unknown, r) => {
                const t = STATUS_TAG[linkStatus(r.configJson)] ?? STATUS_TAG.DISCONNECTED;
                return <Tag color={t.color}>{t.text}</Tag>;
              },
            },
            {
              title: '',
              render: (_: unknown, r) => (
                <Button size="small" icon={<CheckCircleOutlined />} loading={busy === r.id} onClick={() => void verify(r.id)}>
                  Kiểm tra
                </Button>
              ),
            },
          ]}
        />
      </Card>

      <Card title="Video cho bài" extra={<Typography.Text type="secondary">MP4 / MOV / WEBM, tối đa 512 MB</Typography.Text>}>
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message="Bài có video đính kèm mới được Agent đề xuất gửi lên TikTok (vào hộp nháp)."
        />
        <Table<ContentTopic>
          rowKey="id"
          size="small"
          dataSource={topics}
          pagination={{ pageSize: 15, hideOnSinglePage: true }}
          columns={[
            { title: 'Bài', dataIndex: 'title', ellipsis: true },
            { title: 'Trạng thái', dataIndex: 'status', width: 120 },
            {
              title: 'Video',
              width: 160,
              render: (_: unknown, r) => (
                <Upload
                  accept=".mp4,.mov,.webm,.m4v"
                  showUploadList={false}
                  customRequest={async ({ file, onSuccess, onError }) => {
                    try {
                      const v = await uploadTopicVideo(r.id, file as File);
                      message.success(`Đã đính video: ${v.fileName}`);
                      onSuccess?.(v);
                    } catch (e) {
                      message.error(apiErrorMessage(e, 'Không tải được video'));
                      onError?.(e as Error);
                    }
                  }}
                >
                  <Button size="small" icon={<UploadOutlined />}>
                    Tải video lên
                  </Button>
                </Upload>
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
}

/**
 * Lấy code + state từ địa chỉ TikTok trả về (trang kittech.vn/tiktok/callback/?code=…&state=…),
 * hoặc từ chuỗi chỉ có phần ?code=…&state=…. Trả null nếu thiếu một trong hai.
 */
function parseTiktokReturn(text: string): { code: string; state: string } | null {
  const raw = text.trim();
  if (!raw) return null;
  const q = raw.includes('?') ? raw.slice(raw.indexOf('?') + 1) : raw;
  const p = new URLSearchParams(q.split('#')[0]);
  const code = p.get('code');
  const state = p.get('state');
  return code && state ? { code, state } : null;
}

export function ContentTiktokCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [missingCode, setMissingCode] = useState(false);
  const [pasted, setPasted] = useState('');
  const [completing, setCompleting] = useState(false);
  const started = useRef(false);

  const complete = async (code: string, state: string) => {
    setCompleting(true);
    try {
      await completeTiktokOAuth(code, state);
      navigate('/content/channels', { replace: true });
    } catch (e) {
      setError(apiErrorMessage(e, 'Không hoàn tất kết nối TikTok.'));
      setMissingCode(false);
    } finally {
      setCompleting(false);
    }
  };

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    const denied = params.get('error_description') || params.get('error');
    if (denied) {
      setError(denied);
      return;
    }
    // Đọc cả window.location: một số lớp bao trang có thể đổi query (ví dụ thêm ?brand=) trước khi trang này đọc.
    const found = parseTiktokReturn(params.toString()) ?? parseTiktokReturn(window.location.search);
    if (!found) {
      // Redirect URI của TikTok là trang kittech.vn, không tự chuyển mã về máy này: cho dán địa chỉ trang đó vào.
      setMissingCode(true);
      return;
    }
    void complete(found.code, found.state);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [navigate, params]);

  if (missingCode) {
    const parsed = parseTiktokReturn(pasted);
    return (
      <Card style={{ maxWidth: 720, margin: '48px auto' }} title="Hoàn tất kết nối TikTok">
        <Typography.Paragraph>
          TikTok trả mã về trang <Typography.Text code>kittech.vn/tiktok/callback/</Typography.Text>. Mở lại tab đó (hoặc
          Ctrl+H, tìm dòng kittech.vn/tiktok/callback/?code=… mới nhất, chuột phải → Copy link), rồi dán cả địa chỉ vào đây.
          Mã chỉ dùng được vài phút và một lần.
        </Typography.Paragraph>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            allowClear
            placeholder="https://kittech.vn/tiktok/callback/?code=…&state=…"
            value={pasted}
            onChange={(e) => setPasted(e.target.value)}
            onPressEnter={() => parsed && void complete(parsed.code, parsed.state)}
          />
          <Button type="primary" disabled={!parsed} loading={completing} onClick={() => parsed && void complete(parsed.code, parsed.state)}>
            Hoàn tất kết nối
          </Button>
        </Space.Compact>
        {pasted && !parsed ? (
          <Typography.Text type="danger" style={{ display: 'block', marginTop: 8 }}>
            Địa chỉ này không có đủ code và state. Lấy lại địa chỉ đầy đủ từ lịch sử trình duyệt, hoặc bấm Kết nối TikTok lại từ đầu.
          </Typography.Text>
        ) : null}
        <div style={{ marginTop: 16 }}>
          <Typography.Link onClick={() => navigate('/content/channels')}>Quay lại Kênh đăng</Typography.Link>
        </div>
      </Card>
    );
  }

  if (error) {
    return (
      <Result
        status="warning"
        title="Chưa kết nối được TikTok"
        subTitle={error}
        extra={<Typography.Link onClick={() => navigate('/content/channels')}>Quay lại</Typography.Link>}
      />
    );
  }
  return (
    <div style={{ padding: 48, textAlign: 'center' }}>
      <Spin size="large" />
      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
        Đang hoàn tất kết nối TikTok…
      </Typography.Paragraph>
    </div>
  );
}
