import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Card, Space, Table, Tag, Typography } from 'antd';
import { CheckCircleOutlined, EditOutlined, FacebookOutlined, ReloadOutlined, VideoCameraOutlined, YoutubeOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentChannels,
  fetchContentSites,
  type ContentChannelTarget,
  type ContentSiteTarget,
} from '@/shared/api/content.api';
import { startYoutubeOAuth, verifyYoutubeChannel } from '@/shared/api/content-youtube.api';
import { startTiktokOAuth, verifyTiktokChannel } from '@/shared/api/content-tiktok.api';
import { DEST_TYPE_LABEL, facebookStatusLabel, parseFacebookLink } from './content-channels';
import { useContentBrand } from './content-brand-context';
import { ContentPageHeader } from './ContentPageHeader';

type Row = {
  key: string;
  name: string;
  kind: string;
  address?: string | null;
  how: string;
  status: { color: string; text: string };
  verify?: 'youtube' | 'tiktok';
  id: string;
};

const LINK_STATUS: Record<string, { color: string; text: string }> = {
  CONNECTED: { color: 'success', text: 'Đã kết nối' },
  NEED_RECONNECT: { color: 'warning', text: 'Cần kết nối lại' },
  DISCONNECTED: { color: 'default', text: 'Chưa kết nối' },
};

function oauthStatus(configJson: string, key: 'youtube' | 'tiktok') {
  try {
    const status = (JSON.parse(configJson)?.[key]?.status as string) ?? 'DISCONNECTED';
    return LINK_STATUS[status] ?? LINK_STATUS.DISCONNECTED;
  } catch {
    return LINK_STATUS.DISCONNECTED;
  }
}

function siteRow(site: ContentSiteTarget): Row {
  const auto = site.connectorType !== 'manual' && site.connectorType !== 'buffer';
  return {
    key: `site-${site.id}`,
    id: site.id,
    name: site.name,
    kind: `Website · ${DEST_TYPE_LABEL[site.connectorType] ?? site.connectorType}`,
    address: site.baseUrl,
    how: auto ? 'Tự đăng' : 'Chép tay',
    status: !site.isActive
      ? { color: 'default', text: 'Đang tắt' }
      : !auto || site.secretConfigured
        ? { color: 'success', text: 'Sẵn sàng' }
        : { color: 'warning', text: 'Thiếu khoá' },
  };
}

function channelRow(ch: ContentChannelTarget): Row {
  const base = { key: `ch-${ch.id}`, id: ch.id, name: ch.name, address: ch.externalId };
  switch (ch.channelType) {
    case 'youtube_channel':
      return { ...base, kind: 'YouTube', how: 'Tự đăng đúng giờ', status: oauthStatus(ch.configJson, 'youtube'), verify: 'youtube' };
    case 'tiktok_channel':
      return { ...base, kind: 'TikTok', how: 'Gửi vào hộp nháp, đăng trong app', status: oauthStatus(ch.configJson, 'tiktok'), verify: 'tiktok' };
    case 'facebook_page': {
      const fb = parseFacebookLink(ch.configJson)?.status;
      let manualFlag = false;
      try {
        manualFlag = JSON.parse(ch.configJson || '{}')?.manualPosting === true;
      } catch {
        manualFlag = false;
      }
      const auto = !manualFlag && (ch.secretConfigured || (fb ?? '').toUpperCase() === 'CONNECTED');
      return {
        ...base,
        kind: 'Fanpage',
        how: auto ? 'Tự đăng' : 'Đăng tay theo lịch (Lịch đăng & Agent → Đăng tay)',
        status: auto ? facebookStatusLabel(fb) : { color: 'orange', text: 'Chưa có API, đăng tay' },
      };
    }
    default:
      return {
        ...base,
        kind: DEST_TYPE_LABEL[ch.channelType] ?? ch.channelType,
        how: 'Đăng tay (copy nội dung)',
        status: ch.isActive ? { color: 'default', text: 'Đăng tay' } : { color: 'default', text: 'Đang tắt' },
      };
  }
}

/**
 * Kênh đăng: mọi nơi đăng của thương hiệu đang chọn, trạng thái kết nối và nút kết nối.
 * Thay cho hai trang Kênh YouTube / Kênh TikTok và phần trạng thái nơi đăng trong Thương hiệu.
 */
export function ContentChannelsPage() {
  const { message } = App.useApp();
  const workspace = useContentBrand();
  const brandId = workspace.brandId;
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!brandId) {
      setRows([]);
      return;
    }
    setLoading(true);
    try {
      const [sites, channels] = await Promise.all([fetchContentSites(brandId), fetchContentChannels(brandId)]);
      setRows([...sites.map(siteRow), ...channels.map(channelRow)]);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được kênh đăng'));
    } finally {
      setLoading(false);
    }
  }, [brandId, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const connect = async (kind: 'youtube' | 'tiktok') => {
    if (!brandId) return;
    setBusy(kind);
    try {
      const { url } = kind === 'youtube' ? await startYoutubeOAuth(brandId) : await startTiktokOAuth(brandId);
      window.location.href = url;
    } catch (e) {
      message.error(apiErrorMessage(e, kind === 'youtube' ? 'Không bắt đầu được kết nối YouTube' : 'Không bắt đầu được kết nối TikTok'));
      setBusy(null);
    }
  };

  const verify = async (row: Row) => {
    setBusy(row.id);
    try {
      const r = row.verify === 'youtube' ? await verifyYoutubeChannel(row.id) : await verifyTiktokChannel(row.id);
      if (r.ok) message.success(r.message ?? 'Kết nối còn hiệu lực.');
      else message.warning(r.message ?? 'Cần kết nối lại.');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không kiểm tra được'));
    } finally {
      setBusy(null);
      await load();
    }
  };

  const connected = (r: Row) => r.status.text === LINK_STATUS.CONNECTED.text;
  // Đã có kênh kết nối tốt thì không mời "kết nối lại"; kênh lỗi có nút kết nối lại ngay trên dòng của nó.
  const hasYoutube = rows.some((r) => r.verify === 'youtube' && connected(r));
  const hasTiktok = rows.some((r) => r.verify === 'tiktok' && connected(r));
  const editLink = brandId ? `/content/brands?fbBrand=${brandId}` : '/content/brands';

  return (
    <div>
      <ContentPageHeader
        title="Kênh đăng"
        description="Nơi đăng của thương hiệu và trạng thái kết nối. Kết nối bằng tài khoản Google/TikTok/Facebook, không cần mật khẩu."
        extra={
          <>
            <Button icon={<ReloadOutlined />} onClick={() => void load()} disabled={!brandId}>
              Tải lại
            </Button>
            <Link to={editLink}>
              <Button icon={<EditOutlined />} disabled={!brandId}>
                Thêm / sửa nơi đăng
              </Button>
            </Link>
          </>
        }
      />

      {!brandId ? (
        <Alert type="info" showIcon message="Chọn một thương hiệu ở đầu trang để xem kênh đăng." />
      ) : (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Card size="small" title="Thêm kết nối">
            <Space wrap>
              {hasYoutube ? null : (
                <Button icon={<YoutubeOutlined />} loading={busy === 'youtube'} onClick={() => void connect('youtube')}>
                  Kết nối YouTube
                </Button>
              )}
              {hasTiktok ? null : (
                <Button icon={<VideoCameraOutlined />} loading={busy === 'tiktok'} onClick={() => void connect('tiktok')}>
                  Kết nối TikTok
                </Button>
              )}
              <Link to={editLink}>
                <Button icon={<FacebookOutlined />}>Kết nối Fanpage</Button>
              </Link>
              <Link to="/content/ai-text#facebook">
                <Typography.Link>Cấu hình Facebook App</Typography.Link>
              </Link>
            </Space>
          </Card>

          <Table<Row>
            rowKey="key"
            size="middle"
            loading={loading}
            dataSource={rows}
            pagination={false}
            locale={{ emptyText: 'Thương hiệu này chưa có nơi đăng. Bấm "Thêm / sửa nơi đăng".' }}
            scroll={{ x: 760 }}
            columns={[
              { title: 'Nơi đăng', dataIndex: 'name', render: (v: string, r) => (<Space direction="vertical" size={0}><span>{v}</span>{r.address ? <Typography.Text type="secondary" style={{ fontSize: 12 }}>{r.address}</Typography.Text> : null}</Space>) },
              { title: 'Loại', dataIndex: 'kind', width: 190 },
              { title: 'Cách đăng', dataIndex: 'how', width: 220 },
              { title: 'Trạng thái', width: 150, render: (_, r) => <Tag color={r.status.color}>{r.status.text}</Tag> },
              {
                title: '',
                width: 210,
                render: (_, r) =>
                  r.verify ? (
                    <Space size={6} wrap>
                      <Button size="small" icon={<CheckCircleOutlined />} loading={busy === r.id} onClick={() => void verify(r)}>
                        Kiểm tra
                      </Button>
                      {connected(r) ? null : (
                        <Button size="small" type="primary" loading={busy === r.verify} onClick={() => void connect(r.verify!)}>
                          Kết nối lại
                        </Button>
                      )}
                    </Space>
                  ) : null,
              },
            ]}
          />
          <Typography.Text type="secondary">
            Video gửi lên kênh từ bước 5 của Video Studio. Bài viết không qua Studio thì đính video trong trang chi tiết bài.
          </Typography.Text>
        </Space>
      )}
    </div>
  );
}
