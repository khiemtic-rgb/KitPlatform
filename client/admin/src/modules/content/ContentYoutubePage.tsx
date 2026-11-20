import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, App, Button, Card, Result, Select, Space, Spin, Table, Tag, Typography, Upload } from 'antd';
import { CheckCircleOutlined, UploadOutlined, YoutubeOutlined } from '@ant-design/icons';
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
import {
  completeYoutubeOAuth,
  startYoutubeOAuth,
  uploadTopicVideo,
  verifyYoutubeChannel,
} from '@/shared/api/content-youtube.api';

function linkStatus(configJson: string): string {
  try {
    return (JSON.parse(configJson)?.youtube?.status as string) ?? 'DISCONNECTED';
  } catch {
    return 'DISCONNECTED';
  }
}

const STATUS_TAG: Record<string, { color: string; text: string }> = {
  CONNECTED: { color: 'green', text: 'Đã kết nối' },
  NEED_RECONNECT: { color: 'red', text: 'Cần kết nối lại' },
  DISCONNECTED: { color: 'default', text: 'Chưa kết nối' },
};

export function ContentYoutubePage() {
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
      setChannels(ch.filter((c) => c.channelType === 'youtube_channel'));
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
      const { url } = await startYoutubeOAuth(brandId);
      window.location.href = url;
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không bắt đầu được kết nối YouTube'));
      setBusy(null);
    }
  };

  const verify = async (id: string) => {
    setBusy(id);
    try {
      const r = await verifyYoutubeChannel(id);
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
          <YoutubeOutlined /> Kênh YouTube
        </Typography.Title>
        <Typography.Text type="secondary">
          Kết nối một lần bằng tài khoản Google (không cần mật khẩu). Sau đó Agent đề xuất lịch đăng cho các bài đã
          có video; bài chỉ được đăng sau khi bạn bấm Duyệt trong Agent đăng bài.
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
          <Button type="primary" icon={<YoutubeOutlined />} loading={busy === 'connect'} onClick={() => void connect()}>
            {channels.length > 0 ? 'Kết nối lại YouTube' : 'Kết nối YouTube'}
          </Button>
        </Space>
        <Table<ContentChannelTarget>
          rowKey="id"
          size="small"
          pagination={false}
          dataSource={channels}
          locale={{ emptyText: 'Chưa có kênh YouTube cho thương hiệu này.' }}
          columns={[
            { title: 'Kênh', dataIndex: 'name' },
            { title: 'Channel ID', dataIndex: 'externalId' },
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
          message="Bài có video đính kèm mới được Agent đề xuất đăng lên YouTube."
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

export function ContentYoutubeCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  // Mã OAuth chỉ dùng được một lần: chặn lần chạy thứ hai của effect (React StrictMode khi dev),
  // nếu không lần hai báo "phiên hết hạn" dù lần một đã kết nối xong.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    const denied = params.get('error_description') || params.get('error');
    if (denied) {
      setError(denied);
      return;
    }
    const code = params.get('code');
    const state = params.get('state');
    if (!code || !state) {
      setError('Google không trả mã OAuth.');
      return;
    }
    void (async () => {
      try {
        await completeYoutubeOAuth(code, state);
        navigate('/content/channels', { replace: true });
      } catch (e) {
        setError(apiErrorMessage(e, 'Không hoàn tất kết nối YouTube.'));
      }
    })();
  }, [navigate, params]);

  if (error) {
    return (
      <Result
        status="warning"
        title="Chưa kết nối được YouTube"
        subTitle={error}
        extra={<Typography.Link onClick={() => navigate('/content/channels')}>Quay lại</Typography.Link>}
      />
    );
  }
  return (
    <div style={{ padding: 48, textAlign: 'center' }}>
      <Spin size="large" />
      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
        Đang hoàn tất kết nối YouTube…
      </Typography.Paragraph>
    </div>
  );
}
