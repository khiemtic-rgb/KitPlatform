import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, App, Button, Card, Input, InputNumber, Popconfirm, Select, Space, Table, Tag, Tooltip, Typography } from 'antd';
import { CheckOutlined, CloseOutlined, CopyOutlined, LinkOutlined, ReloadOutlined, RobotOutlined, SendOutlined, StopOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import { fetchContentBrands, type ContentBrand } from '@/shared/api/content.api';
import {
  approveAgentProposal,
  cancelAgentProposal,
  fetchAgentScheduleSettings,
  isManualProposal,
  fetchAgentProposals,
  generateAgentProposals,
  rejectAgentProposal,
  retryAgentProposal,
  type AgentGenerateResult,
  type AgentProposal,
  type AgentProposalStatus,
} from '@/shared/api/content-agent.api';
import { useContentBrand } from './content-brand-context';

const STATUS_TEXT: Record<AgentProposalStatus, string> = {
  Proposed: 'Chờ duyệt',
  Approved: 'Đã duyệt',
  Rejected: 'Đã từ chối',
  Cancelled: 'Đã huỷ',
  Posted: 'Đã đăng tay',
  Failed: 'Lỗi',
};

const STATUS_COLOR: Record<AgentProposalStatus, string> = {
  Proposed: 'gold',
  Approved: 'green',
  Rejected: 'default',
  Cancelled: 'default',
  Posted: 'cyan',
  Failed: 'red',
};

const STATUS_FILTER = [
  { value: 'open', label: 'Chờ duyệt + chờ đăng' },
  { value: 'Proposed', label: 'Chỉ chờ duyệt' },
  { value: 'Approved', label: 'Chỉ đã duyệt' },
  { value: 'done', label: 'Đã đăng' },
  { value: 'failed', label: 'Lỗi, cần xử lý' },
  { value: 'Rejected', label: 'Đã từ chối' },
  { value: 'Cancelled', label: 'Đã huỷ' },
  { value: 'all', label: 'Tất cả' },
];

/** Job đăng chạy lâu hơn mức này coi như kẹt (khớp StuckRunningAfter ở máy chủ). */
const STUCK_MS = 45 * 60_000;

type JobState = 'done' | 'failed' | 'running' | 'stuck' | 'queued';

/** Kết quả thật của lượt đăng, đọc từ job đăng; lượt đăng tay đã đánh dấu tính là đã đăng. */
function jobState(r: AgentProposal, now: Date): JobState | null {
  if (r.status === 'Posted') return 'done';
  if (r.status !== 'Approved' || !r.publishJobId) return null;
  switch (r.jobStatus) {
    case 'Succeeded':
      return 'done';
    case 'Failed':
      return 'failed';
    case 'Running': {
      const at = r.jobUpdatedAt ? new Date(r.jobUpdatedAt).getTime() : 0;
      return now.getTime() - at > STUCK_MS ? 'stuck' : 'running';
    }
    case 'Queued':
      return 'queued';
    default:
      return null;
  }
}

/** Quá giờ mà chưa có gì xảy ra: đề xuất chưa duyệt, lượt đăng tay chưa đăng, hoặc job còn chờ. */
function isOverdue(r: AgentProposal, now: Date) {
  if (new Date(r.publishAt).getTime() >= now.getTime()) return false;
  if (r.status === 'Proposed') return true;
  if (r.status !== 'Approved') return false;
  if (isManualProposal(r)) return true;
  return jobState(r, now) === 'queued';
}

const JOB_TAG: Record<JobState, { text: string; color: string }> = {
  done: { text: 'Đã đăng', color: 'green' },
  failed: { text: 'Đăng lỗi', color: 'red' },
  running: { text: 'Đang đăng', color: 'processing' },
  stuck: { text: 'Bị kẹt', color: 'orange' },
  queued: { text: 'Chờ đăng', color: 'blue' },
};

const ALL = 'all';

const PLATFORM: Record<string, { label: string; color: string; manual?: boolean }> = {
  youtube_channel: { label: 'YouTube', color: 'red' },
  tiktok_channel: { label: 'TikTok', color: 'magenta' },
  facebook_page: { label: 'Fanpage', color: 'blue', manual: true },
  local_os: { label: 'Website', color: 'green' },
  wordpress_rest: { label: 'Website', color: 'green' },
  astro_git: { label: 'Website', color: 'green' },
};

function platformOf(r: AgentProposal) {
  return PLATFORM[r.connectorType] ?? { label: r.siteId ? 'Website' : 'Khác', color: 'default', manual: true };
}

function isManualRow(r: AgentProposal) {
  return platformOf(r).manual === true || isManualProposal(r);
}

const PLATFORM_FILTER = [
  { value: ALL, label: 'Mọi nơi đăng' },
  { value: 'YouTube', label: 'YouTube' },
  { value: 'TikTok', label: 'TikTok' },
  { value: 'Fanpage', label: 'Fanpage' },
  { value: 'Website', label: 'Website' },
];

const TIME_FILTER = [
  { value: ALL, label: 'Mọi thời gian' },
  { value: 'overdue', label: 'Quá giờ' },
  { value: 'today', label: 'Hôm nay' },
  { value: 'tomorrow', label: 'Ngày mai' },
  { value: '7d', label: '7 ngày tới' },
];

const MODE_FILTER = [
  { value: ALL, label: 'Mọi cách đăng' },
  { value: 'manual', label: 'Đăng tay' },
  { value: 'auto', label: 'Tự động' },
];

function vnDayKey(d: Date) {
  return new Date(d.getTime() + 7 * 3600_000).toISOString().slice(0, 10);
}

/** Nhãn thời gian so với bây giờ: dòng quá giờ chưa xử lý, hôm nay, ngày mai. */
function dayTag(r: AgentProposal, now: Date): { text: string; color: string } | null {
  if (r.status !== 'Proposed' && r.status !== 'Approved') return null;
  const state = jobState(r, now);
  if (state && state !== 'queued') return null;
  const at = new Date(r.publishAt);
  if (isOverdue(r, now)) return { text: 'Quá giờ', color: 'red' };
  if (at.getTime() < now.getTime()) return null;
  const k = vnDayKey(at);
  if (k === vnDayKey(now)) return { text: 'Hôm nay', color: 'blue' };
  if (k === vnDayKey(new Date(now.getTime() + 86_400_000))) return { text: 'Ngày mai', color: 'cyan' };
  return null;
}

function vnTime(iso: string) {
  return new Date(iso).toLocaleString('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

/** Tab "Agent đăng bài" của Lịch đăng: đề xuất của Agent cho bài chữ, và các lượt đăng đã duyệt. */
export function ContentAgentPage({ embedded = false }: { embedded?: boolean } = {}) {
  const { message } = App.useApp();
  const workspace = useContentBrand();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const brandId = workspace.brandId ?? ALL;
  const [days, setDays] = useState(7);
  const [maxPerDay, setMaxPerDay] = useState(1);
  const [filter, setFilter] = useState('open');
  const [rows, setRows] = useState<AgentProposal[]>([]);
  const [loading, setLoading] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [last, setLast] = useState<AgentGenerateResult | null>(null);
  const [fBrand, setFBrand] = useState(ALL);
  const [fPlatform, setFPlatform] = useState(ALL);
  const [fTime, setFTime] = useState(ALL);
  const [fMode, setFMode] = useState(ALL);
  const [fText, setFText] = useState('');

  useEffect(() => {
    void (async () => {
      try {
        const list = await fetchContentBrands(true);
        setBrands(list);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được danh sách thương hiệu'));
      }
    })();
  }, [message]);

  // Số ngày và số bài/ngày lấy từ "Cài đặt lịch Agent" của thương hiệu.
  useEffect(() => {
    if (brandId === ALL) return;
    let cancelled = false;
    void fetchAgentScheduleSettings(brandId)
      .then((settings) => {
        if (cancelled) return;
        setDays(settings.days);
        setMaxPerDay(settings.maxPerDayPerChannel);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [brandId]);

  const load = useCallback(async () => {
    if (!brandId) return;
    setLoading(true);
    try {
      const clientSide = filter === 'open' || filter === 'all' || filter === 'done' || filter === 'failed';
      const status = clientSide ? undefined : filter;
      const all = await fetchAgentProposals({ brand: brandId === ALL ? undefined : brandId, status });
      const t = new Date();
      const pick = (r: AgentProposal) => {
        const s = jobState(r, t);
        if (filter === 'open') return (r.status === 'Proposed' || r.status === 'Approved') && s !== 'done';
        if (filter === 'done') return s === 'done';
        if (filter === 'failed') return r.status === 'Failed' || s === 'failed' || s === 'stuck';
        return true;
      };
      setRows(all.filter(pick));
      setSelected([]);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được đề xuất'));
    } finally {
      setLoading(false);
    }
  }, [brandId, filter, message]);

  useEffect(() => {
    void load();
  }, [load]);

  // Khi còn lượt đang đăng hoặc vừa bấm Đăng lại, tự tải lại để thấy kết quả mà không phải bấm.
  const waiting = useMemo(() => {
    const t = new Date();
    return rows.some((r) => {
      const s = jobState(r, t);
      return s === 'running' || (s === 'queued' && new Date(r.publishAt).getTime() <= t.getTime() + 60_000);
    });
  }, [rows]);
  useEffect(() => {
    if (!waiting) return;
    const timer = window.setInterval(() => void load(), 15_000);
    return () => window.clearInterval(timer);
  }, [waiting, load]);

  const generate = async () => {
    if (!brandId || brandId === ALL) return;
    setGenerating(true);
    try {
      const res = await generateAgentProposals(brandId, days, maxPerDay);
      setLast(res);
      message.success(
        res.created > 0
          ? `Đã tạo ${res.created} đề xuất mới. Chưa có gì được đăng.`
          : 'Không có đề xuất mới. Xem lý do bên dưới.',
      );
      await load();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tạo được đề xuất'));
    } finally {
      setGenerating(false);
    }
  };

  const act = async (id: string, fn: () => Promise<unknown>, ok: string) => {
    setBusyId(id);
    try {
      await fn();
      message.success(ok);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không thực hiện được'));
    } finally {
      setBusyId(null);
      await load();
    }
  };

  const approveSelected = async () => {
    const ids = viewRows.filter((r) => selected.includes(r.id) && r.status === 'Proposed').map((r) => r.id);
    let okCount = 0;
    for (const id of ids) {
      try {
        await approveAgentProposal(id);
        okCount += 1;
      } catch (e) {
        message.error(apiErrorMessage(e, 'Một đề xuất không duyệt được'));
      }
    }
    if (okCount > 0) message.success(`Đã duyệt ${okCount} đề xuất.`);
    await load();
  };

  const now = new Date();
  const viewRows = useMemo(() => {
    const t = new Date();
    const todayKey = vnDayKey(t);
    const tomorrowKey = vnDayKey(new Date(t.getTime() + 86_400_000));
    const weekEnd = t.getTime() + 7 * 86_400_000;
    const q = fText.trim().toLowerCase();
    return rows
      .filter((r) => {
        if (brandId === ALL && fBrand !== ALL && r.brandId !== fBrand) return false;
        if (fPlatform !== ALL && platformOf(r).label !== fPlatform) return false;
        if (fMode === 'manual' && !isManualRow(r)) return false;
        if (fMode === 'auto' && isManualRow(r)) return false;
        const at = new Date(r.publishAt);
        if (fTime === 'overdue' && !isOverdue(r, t)) return false;
        if (fTime === 'today' && vnDayKey(at) !== todayKey) return false;
        if (fTime === 'tomorrow' && vnDayKey(at) !== tomorrowKey) return false;
        if (fTime === '7d' && !(at.getTime() >= t.getTime() && at.getTime() <= weekEnd)) return false;
        if (q && !r.title.toLowerCase().includes(q)) return false;
        return true;
      })
      .sort((a, b) => a.publishAt.localeCompare(b.publishAt));
  }, [rows, brandId, fBrand, fPlatform, fTime, fMode, fText]);

  const filtering = fBrand !== ALL || fPlatform !== ALL || fTime !== ALL || fMode !== ALL || fText.trim() !== '';

  const pendingSelected = useMemo(
    () => viewRows.filter((r) => selected.includes(r.id) && r.status === 'Proposed').length,
    [viewRows, selected],
  );

  return (
    <div>
      {embedded ? null : (
        <Space direction="vertical" size={4} style={{ marginBottom: 16 }}>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <RobotOutlined /> Agent đăng bài
          </Typography.Title>
        </Space>
      )}
      <Typography.Paragraph type="secondary">
        Khi bật Viết tự động, Agent dựng khung từ YouTube và Fanpage có lượt xem rồi nhờ AI viết. Website đăng theo khung giờ đã cài. Bài viết Fanpage đăng lúc 15:00, lệch giờ thước phim. Video và YouTube vẫn chờ duyệt.
        Video duyệt ở bước 5 của Video Studio đã được xếp và duyệt sẵn, hiện trong danh sách dưới.
      </Typography.Paragraph>

      <Card size="small" title="Agent đề xuất lịch cho bài chữ" style={{ marginBottom: 16 }}>
        <Space wrap>
          <span>
            Số ngày <InputNumber min={1} max={14} value={days} onChange={(v) => setDays(v ?? 7)} />
          </span>
          <span>
            Tối đa bài/ngày/kênh <InputNumber min={1} max={4} value={maxPerDay} onChange={(v) => setMaxPerDay(v ?? 1)} />
          </span>
          <Button type="primary" icon={<RobotOutlined />} loading={generating} disabled={!brandId || brandId === ALL} title={brandId === ALL ? 'Chọn một thương hiệu ở đầu trang để tạo đề xuất' : undefined} onClick={() => void generate()}>
            Tạo đề xuất
          </Button>
        </Space>
      </Card>

      {last && (
        <Space direction="vertical" style={{ width: '100%', marginBottom: 16 }}>
          {last.warnings.map((w) => (
            <Alert key={w} type="info" showIcon message={w} />
          ))}
          {last.skipped.length > 0 && (
            <Alert
              type="warning"
              showIcon
              message={`${last.skipped.length} bài chưa được đề xuất`}
              description={
                <ul style={{ margin: 0, paddingLeft: 18 }}>
                  {last.skipped.map((s) => (
                    <li key={s.topicId}>
                      <strong>{s.title}</strong>: {s.reason}
                    </li>
                  ))}
                </ul>
              }
            />
          )}
        </Space>
      )}

      <Card
        title="Đề xuất và lượt đăng đã duyệt"
        extra={
          <Space>
            <Select style={{ width: 210 }} value={filter} onChange={setFilter} options={STATUS_FILTER} />
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
              Tải lại
            </Button>
            <Popconfirm
              title={`Duyệt ${pendingSelected} đề xuất đã chọn?`}
              description="Mỗi mục sẽ tạo một job đăng đúng giờ đề xuất."
              okText="Duyệt"
              cancelText="Không"
              disabled={pendingSelected === 0}
              onConfirm={() => void approveSelected()}
            >
              <Button type="primary" icon={<CheckOutlined />} disabled={pendingSelected === 0}>
                Duyệt đã chọn ({pendingSelected})
              </Button>
            </Popconfirm>
          </Space>
        }
      >
        <Space wrap style={{ marginBottom: 12 }}>
          {brandId === ALL ? (
            <Select
              style={{ width: 170 }}
              value={fBrand}
              onChange={setFBrand}
              options={[{ value: ALL, label: 'Mọi thương hiệu' }, ...brands.map((b) => ({ value: b.id, label: b.name }))]}
            />
          ) : null}
          <Select style={{ width: 150 }} value={fPlatform} onChange={setFPlatform} options={PLATFORM_FILTER} />
          <Select style={{ width: 150 }} value={fTime} onChange={setFTime} options={TIME_FILTER} />
          <Select style={{ width: 150 }} value={fMode} onChange={setFMode} options={MODE_FILTER} />
          <Input.Search
            allowClear
            style={{ width: 220 }}
            placeholder="Tìm theo tiêu đề"
            value={fText}
            onChange={(e) => setFText(e.target.value)}
          />
          <Typography.Text type="secondary">
            Hiện {viewRows.length}/{rows.length} lượt
          </Typography.Text>
          {filtering ? (
            <Button
              size="small"
              onClick={() => {
                setFBrand(ALL);
                setFPlatform(ALL);
                setFTime(ALL);
                setFMode(ALL);
                setFText('');
              }}
            >
              Xoá lọc
            </Button>
          ) : null}
        </Space>
        <Table<AgentProposal>
          rowKey="id"
          loading={loading}
          dataSource={viewRows}
          pagination={{ pageSize: 20, hideOnSinglePage: true }}
          scroll={{ x: 1300 }}
          onRow={(r) => {
            const s = jobState(r, now);
            return isOverdue(r, now) || s === 'failed' || s === 'stuck' ? { style: { background: '#fff1f0' } } : {};
          }}
          rowSelection={{
            selectedRowKeys: selected,
            onChange: (keys) => setSelected(keys as string[]),
            getCheckboxProps: (r) => ({ disabled: r.status !== 'Proposed' }),
          }}
          locale={{ emptyText: 'Chưa có đề xuất. Bấm "Tạo đề xuất".' }}
          columns={[
            {
              title: 'Giờ đăng (VN)',
              dataIndex: 'publishAt',
              width: 160,
              render: (v: string, r) => {
                const tag = dayTag(r, now);
                return (
                  <Space direction="vertical" size={2}>
                    <span>{vnTime(v)}</span>
                    {tag ? <Tag color={tag.color} style={{ marginInlineEnd: 0 }}>{tag.text}</Tag> : null}
                  </Space>
                );
              },
            },
            {
              title: 'Thương hiệu',
              dataIndex: 'brandId',
              width: 130,
              render: (v: string) => brands.find((b) => b.id === v)?.name ?? '',
            },
            { title: 'Bài', dataIndex: 'title', width: 280, render: (v: string) => <Tooltip title={v} placement="topLeft"><span style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>{v}</span></Tooltip> },
            {
              title: 'Nơi đăng',
              dataIndex: 'targetLabel',
              width: 170,
              render: (v: string, r) => {
                const pf = platformOf(r);
                return (
                  <Space direction="vertical" size={2}>
                    <Tag color={pf.color} style={{ marginInlineEnd: 0 }}>
                      {pf.label}
                    </Tag>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {v}
                    </Typography.Text>
                  </Space>
                );
              },
            },
            {
              title: 'Trạng thái',
              dataIndex: 'status',
              width: 170,
              render: (v: AgentProposalStatus, r) => {
                const s = jobState(r, now);
                const tag = s && v === 'Approved' ? JOB_TAG[s] : null;
                const doneText = r.connectorType === 'tiktok_channel' ? 'Đã vào hộp nháp TikTok' : JOB_TAG.done.text;
                const note =
                  s === 'failed' || s === 'stuck'
                    ? r.jobError ?? (s === 'stuck' ? 'Máy tắt khi đang đăng. Kiểm tra kênh rồi bấm Đăng lại.' : null)
                    : r.lastError && v !== 'Approved'
                      ? r.lastError
                      : null;
                return (
                  <Space direction="vertical" size={0}>
                    <Space size={4} wrap>
                      {tag ? (
                        <Tag color={tag.color}>{s === 'done' ? doneText : tag.text}</Tag>
                      ) : (
                        <Tag color={STATUS_COLOR[v]}>{STATUS_TEXT[v] ?? v}</Tag>
                      )}
                      {isManualProposal(r) ? <Tag color="orange">Đăng tay</Tag> : null}
                    </Space>
                    {s === 'done' && r.jobUrl ? (
                      <Typography.Link href={r.jobUrl} target="_blank" rel="noreferrer" style={{ fontSize: 12 }}>
                        <LinkOutlined /> Mở bài
                      </Typography.Link>
                    ) : null}
                    {r.connectorType === 'tiktok_channel' && r.jobCaption ? (
                      <Tooltip title={r.jobCaption} placement="topLeft">
                        <Typography.Link
                          style={{ fontSize: 12 }}
                          onClick={() => {
                            void navigator.clipboard
                              .writeText(r.jobCaption ?? '')
                              .then(() => message.success('Đã chép chú thích. Mở bản nháp trên TikTok và dán vào ô mô tả.'))
                              .catch(() => message.error('Không chép được, hãy rê chuột để xem và chép tay.'));
                          }}
                        >
                          <CopyOutlined /> Chép chú thích TikTok
                        </Typography.Link>
                      </Tooltip>
                    ) : null}
                    {note ? (
                      <Tooltip title={note} placement="topLeft">
                        <Typography.Text
                          type={s === 'failed' || s === 'stuck' ? 'danger' : 'secondary'}
                          style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden', fontSize: 12 }}
                        >
                          {note}
                        </Typography.Text>
                      </Tooltip>
                    ) : null}
                  </Space>
                );
              },
            },
            {
              title: 'Lý do',
              dataIndex: 'reason',
              width: 280,
              render: (v: string) => (
                <Tooltip title={v} placement="topLeft">
                  <Typography.Text
                    type="secondary"
                    style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden', fontSize: 12 }}
                  >
                    {v}
                  </Typography.Text>
                </Tooltip>
              ),
            },
            {
              title: '',
              key: 'actions',
              fixed: 'right' as const,
              width: 200,
              render: (_: unknown, r) => (
                <Space>
                  {r.status === 'Proposed' && (
                    <>
                      <Popconfirm
                        title="Duyệt đề xuất này?"
                        description={`Sẽ đăng lên ${r.targetLabel} lúc ${vnTime(r.publishAt)}.`}
                        okText="Duyệt"
                        cancelText="Không"
                        onConfirm={() =>
                          void act(
                            r.id,
                            () => approveAgentProposal(r.id),
                            r.connectorType === 'facebook_page'
                              ? 'Đã duyệt. Fanpage chưa có API thì lượt này nằm ở tab Đăng tay.'
                              : 'Đã duyệt, job đăng đã được tạo.',
                          )
                        }
                      >
                        <Button size="small" type="primary" icon={<CheckOutlined />} loading={busyId === r.id}>
                          Duyệt
                        </Button>
                      </Popconfirm>
                      <Button
                        size="small"
                        icon={<CloseOutlined />}
                        disabled={busyId === r.id}
                        onClick={() => void act(r.id, () => rejectAgentProposal(r.id), 'Đã từ chối.')}
                      >
                        Từ chối
                      </Button>
                    </>
                  )}
                  {(() => {
                    const s = jobState(r, now);
                    if (s === 'failed' || s === 'stuck') {
                      return (
                        <Popconfirm
                          title="Đăng lại lượt này?"
                          description="Nếu máy tắt giữa lúc đăng, bài có thể đã lên. Xem kênh trước để tránh trùng bài."
                          okText="Đăng lại"
                          cancelText="Không"
                          onConfirm={() => void act(r.id, () => retryAgentProposal(r.id), 'Đã đưa vào hàng đăng. Kết quả hiện sau khoảng 15 giây.')}
                        >
                          <Button size="small" type="primary" icon={<SendOutlined />} loading={busyId === r.id}>
                            Đăng lại
                          </Button>
                        </Popconfirm>
                      );
                    }
                    if (s === 'queued' && isOverdue(r, now)) {
                      return (
                        <Button
                          size="small"
                          type="primary"
                          icon={<SendOutlined />}
                          loading={busyId === r.id}
                          onClick={() => void act(r.id, () => retryAgentProposal(r.id), 'Đã đưa vào hàng đăng. Kết quả hiện sau khoảng 15 giây.')}
                        >
                          Đăng ngay
                        </Button>
                      );
                    }
                    return null;
                  })()}
                  {r.status === 'Approved' && (isManualProposal(r) || r.jobStatus === 'Queued' || r.jobStatus === 'Failed') && (
                    <Popconfirm
                      title="Huỷ lượt đăng này?"
                      description="Lượt sẽ không được đăng nữa. Huỷ được khi job chưa chạy hoặc đã lỗi."
                      okText="Huỷ job"
                      cancelText="Không"
                      onConfirm={() => void act(r.id, () => cancelAgentProposal(r.id), 'Đã huỷ job đăng.')}
                    >
                      <Button size="small" danger icon={<StopOutlined />} loading={busyId === r.id}>
                        Huỷ
                      </Button>
                    </Popconfirm>
                  )}
                </Space>
              ),
            },
          ]}
        />
      </Card>
    </div>
  );
}
