import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, App, Button, Card, Empty, Input, Popconfirm, Space, Spin, Tag, Typography } from 'antd';
import { CheckOutlined, CopyOutlined, DownloadOutlined, LinkOutlined } from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentAssetObjectUrl,
  fetchContentChannels,
  fetchContentTopicDetail,
  type ContentChannelTarget,
  type ContentTopicDetail,
} from '@/shared/api/content.api';
import {
  fetchAgentProposals,
  isManualProposal,
  markAgentProposalPosted,
  type AgentProposal,
} from '@/shared/api/content-agent.api';
import { channelOpenUrl } from './content-manual-dest';
import { splitFacebookPost } from './facebook-post';

const SOON_MS = 30 * 60_000;

function lateText(ms: number) {
  const min = Math.round(ms / 60_000);
  return min < 60 ? `${min} phút` : `${Math.round(min / 60)} giờ`;
}

function when(iso: string) {
  return dayjs(iso).format('ddd DD/MM · HH:mm');
}

/**
 * Lịch đăng → Đăng tay: các lượt Fanpage (chưa có API Meta) đã duyệt, xếp theo giờ.
 * Đến giờ: copy nội dung, tải video/ảnh, mở Fanpage, đăng, bấm "Đã đăng".
 */
export function ManualPostingTab({ brandId }: { brandId?: string }) {
  const { message } = App.useApp();
  const [rows, setRows] = useState<AgentProposal[]>([]);
  const [channels, setChannels] = useState<ContentChannelTarget[]>([]);
  const [loading, setLoading] = useState(false);
  const [details, setDetails] = useState<Record<string, ContentTopicDetail>>({});
  const [busy, setBusy] = useState('');
  const [links, setLinks] = useState<Record<string, string>>({});
  const [now, setNow] = useState(() => Date.now());

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, ch] = await Promise.all([
        fetchAgentProposals({ brand: brandId }),
        brandId ? fetchContentChannels(brandId) : Promise.resolve([] as ContentChannelTarget[]),
      ]);
      setRows(list.filter(isManualProposal));
      setChannels(ch);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được lịch đăng tay'));
    } finally {
      setLoading(false);
    }
  }, [brandId, message]);

  useEffect(() => {
    void load();
  }, [load]);

  // Cập nhật "đến giờ" mỗi phút.
  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), 60_000);
    return () => window.clearInterval(t);
  }, []);

  const groups = useMemo(() => {
    const todayEnd = dayjs().endOf('day').valueOf();
    const weekAgo = now - 7 * 86_400_000;
    const pending = rows.filter((r) => r.status === 'Approved').sort((a, b) => a.publishAt.localeCompare(b.publishAt));
    return {
      due: pending.filter((r) => new Date(r.publishAt).getTime() <= now + SOON_MS),
      today: pending.filter((r) => {
        const t = new Date(r.publishAt).getTime();
        return t > now + SOON_MS && t <= todayEnd;
      }),
      later: pending.filter((r) => new Date(r.publishAt).getTime() > todayEnd),
      posted: rows
        .filter((r) => r.status === 'Posted' && new Date(r.decidedAt ?? r.publishAt).getTime() >= weekAgo)
        .sort((a, b) => b.publishAt.localeCompare(a.publishAt)),
    };
  }, [now, rows]);

  const detailOf = async (topicId: string) => {
    if (details[topicId]) return details[topicId];
    const d = await fetchContentTopicDetail(topicId);
    setDetails((cur) => ({ ...cur, [topicId]: d }));
    return d;
  };

  const postText = (d: ContentTopicDetail) => {
    const v =
      d.variants.find((x) => x.kind === 'fb_page' && x.bodyMarkdown.trim()) ??
      d.variants.find((x) => (x.kind === 'fb_short' || x.kind === 'social_caption') && x.bodyMarkdown.trim());
    return splitFacebookPost(v?.bodyMarkdown ?? '');
  };

  const copy = async (r: AgentProposal, part: 'post' | 'comment') => {
    setBusy(`${part}-${r.id}`);
    try {
      const d = await detailOf(r.topicId);
      const { post, comment } = postText(d);
      const text = part === 'post' ? post : comment;
      if (!text) {
        message.warning(part === 'post' ? 'Bài chưa có nội dung Facebook. Mở bài để viết.' : 'Bài không có bình luận đầu.');
        return;
      }
      await navigator.clipboard.writeText(text);
      message.success(part === 'post' ? 'Đã copy nội dung bài. Dán vào ô viết bài trên Fanpage.' : 'Đã copy bình luận đầu. Đăng xong thì dán làm bình luận và ghim.');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không copy được'));
    } finally {
      setBusy('');
    }
  };

  const download = async (r: AgentProposal) => {
    setBusy(`dl-${r.id}`);
    try {
      const d = await detailOf(r.topicId);
      const asset = d.assets.find((a) => a.kind === 'video') ?? d.assets.find((a) => a.isSelected) ?? d.assets[0];
      if (!asset) {
        message.warning('Bài này không có video hay ảnh. Đăng chữ thôi.');
        return;
      }
      const url = await fetchContentAssetObjectUrl(asset.id);
      const a = document.createElement('a');
      a.href = url;
      a.download = asset.fileName || `${r.topicId}.mp4`;
      a.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được tệp'));
    } finally {
      setBusy('');
    }
  };

  const pageUrl = (r: AgentProposal) => {
    const ch = channels.find((c) => c.id === r.channelId);
    return ch ? channelOpenUrl(ch) : null;
  };

  const markPosted = async (r: AgentProposal) => {
    setBusy(`done-${r.id}`);
    try {
      await markAgentProposalPosted(r.id, links[r.id]?.trim() || undefined);
      message.success('Đã ghi nhận: đã đăng.');
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không ghi nhận được'));
    } finally {
      setBusy('');
    }
  };

  const item = (r: AgentProposal, tone: 'due' | 'normal' | 'done') => {
    const t = new Date(r.publishAt).getTime();
    const late = tone === 'due' && t < now;
    const url = pageUrl(r);
    return (
      <Card
        key={r.id}
        size="small"
        style={{ marginBottom: 8, borderLeft: `4px solid ${tone === 'due' ? (late ? '#ff4d4f' : '#fa8c16') : tone === 'done' ? '#13c2c2' : '#d9d9d9'}` }}
      >
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'flex-start' }}>
          <div style={{ minWidth: 150 }}>
            <Typography.Text strong style={{ fontSize: 16 }}>{when(r.publishAt)}</Typography.Text>
            <div>
              {tone === 'done' ? (
                <Tag color="cyan">Đã đăng</Tag>
              ) : late ? (
                <Tag color="red">Quá giờ {lateText(now - t)}</Tag>
              ) : tone === 'due' ? (
                <Tag color="orange">Đến giờ đăng</Tag>
              ) : null}
            </div>
          </div>
          <div style={{ flex: '1 1 260px', minWidth: 0 }}>
            <Link to={`/content/topics/${r.topicId}`}>{r.title}</Link>
            <div>
              <Typography.Text type="secondary">{r.targetLabel}</Typography.Text>
            </div>
            {tone === 'done' && r.lastError?.startsWith('Link bài: ') ? (
              <Typography.Link href={r.lastError.slice(10)} target="_blank" rel="noreferrer">
                Xem bài đã đăng
              </Typography.Link>
            ) : null}
          </div>
          {tone === 'done' ? null : (
            <Space wrap>
              <Button type="primary" icon={<CopyOutlined />} loading={busy === `post-${r.id}`} onClick={() => void copy(r, 'post')}>
                1. Copy nội dung
              </Button>
              <Button icon={<DownloadOutlined />} loading={busy === `dl-${r.id}`} onClick={() => void download(r)}>
                2. Tải video/ảnh
              </Button>
              <Button icon={<LinkOutlined />} disabled={!url} href={url ?? undefined} target="_blank" rel="noreferrer">
                3. Mở Fanpage
              </Button>
              <Button loading={busy === `comment-${r.id}`} onClick={() => void copy(r, 'comment')}>
                Copy bình luận đầu
              </Button>
              <Popconfirm
                title="Đã đăng bài này lên Fanpage?"
                description={
                  <Input
                    size="small"
                    placeholder="Link bài vừa đăng (không bắt buộc)"
                    value={links[r.id] ?? ''}
                    onChange={(e) => setLinks((cur) => ({ ...cur, [r.id]: e.target.value }))}
                    style={{ width: 280 }}
                  />
                }
                okText="Đã đăng"
                cancelText="Chưa"
                onConfirm={() => void markPosted(r)}
              >
                <Button icon={<CheckOutlined />} loading={busy === `done-${r.id}`}>
                  4. Đã đăng
                </Button>
              </Popconfirm>
            </Space>
          )}
        </div>
      </Card>
    );
  };

  if (loading && !rows.length) {
    return (
      <div style={{ textAlign: 'center', padding: 32 }}>
        <Spin />
      </div>
    );
  }

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Alert
        type="info"
        showIcon
        message="Fanpage chưa có API Meta nên đăng tay theo giờ đã xếp."
        description={
          <ol style={{ margin: 0, paddingLeft: 18 }}>
            <li>Đến giờ: copy nội dung, tải video (hoặc ảnh), mở Fanpage.</li>
            <li>
              Trên Facebook: video dọc đăng dạng <b>Reels</b>, video ngang đăng dạng <b>Video</b>; tải tệp lên trực tiếp, không
              dán link YouTube/TikTok. Dán nội dung, không sửa dòng đầu (đó là câu móc).
            </li>
            <li>Đăng xong: dán bình luận đầu (nếu có) và ghim; trả lời bình luận trong giờ đầu.</li>
            <li>Bấm "Đã đăng" (có thể dán link bài) để lịch trừ đi lượt này.</li>
          </ol>
        }
      />
      <div>
        <Typography.Title level={5}>Cần đăng ngay ({groups.due.length})</Typography.Title>
        {groups.due.length ? groups.due.map((r) => item(r, 'due')) : <Typography.Text type="secondary">Không có lượt nào đến giờ.</Typography.Text>}
      </div>
      <div>
        <Typography.Title level={5}>Còn lại hôm nay ({groups.today.length})</Typography.Title>
        {groups.today.length ? groups.today.map((r) => item(r, 'normal')) : <Typography.Text type="secondary">Không còn lượt nào hôm nay.</Typography.Text>}
      </div>
      <div>
        <Typography.Title level={5}>Sắp tới ({groups.later.length})</Typography.Title>
        {groups.later.length ? groups.later.map((r) => item(r, 'normal')) : <Typography.Text type="secondary">Chưa có lượt nào.</Typography.Text>}
      </div>
      <div>
        <Typography.Title level={5}>Đã đăng 7 ngày qua ({groups.posted.length})</Typography.Title>
        {groups.posted.length ? groups.posted.map((r) => item(r, 'done')) : <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có" />}
      </div>
    </Space>
  );
}
