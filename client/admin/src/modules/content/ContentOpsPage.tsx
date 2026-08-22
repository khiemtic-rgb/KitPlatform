import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Alert, App, Button, Card, Col, Progress, Row, Spin, Table, Tag, Typography } from 'antd';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  ClockCircleOutlined,
  ExclamationCircleOutlined,
  FileTextOutlined,
  PlusOutlined,
  ReloadOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentOps,
  runContentPublishJob,
  type ContentCalendarItem,
  type ContentOpsFailedPublish,
  type ContentOpsSnapshot,
  type ContentPackage,
  type ContentWorkJob,
} from '@/shared/api/content.api';
import { brandColor, channelLabel, fitMark } from './content-brand';

dayjs.extend(isoWeek);

const KIND_LABEL: Record<string, string> = {
  generate_topic: 'Viết bài',
  generate_package: 'Generate nơi đăng',
  publish_topic: 'Xuất bản',
  video_mvp: 'Video MVP',
  video_render: 'Render video',
  brand_adapt: 'Brand Fit',
};

function money(n: number) {
  return `$${n.toLocaleString('en-US', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;
}

function weekDays() {
  const start = dayjs().startOf('isoWeek');
  return Array.from({ length: 7 }, (_, i) => start.add(i, 'day'));
}

export function ContentOpsPage() {
  const navigate = useNavigate();
  const [data, setData] = useState<ContentOpsSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await fetchContentOps());
      setError(null);
    } catch (e) {
      setError(apiErrorMessage(e, 'Không tải được bảng điều khiển'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    const t = window.setInterval(() => void load(), 15_000);
    return () => window.clearInterval(t);
  }, [load]);

  const byDay = useMemo(() => {
    const map = new Map<string, ContentCalendarItem[]>();
    for (const item of data?.weekItems ?? []) {
      const key = dayjs(item.at).format('YYYY-MM-DD');
      const list = map.get(key) ?? [];
      list.push(item);
      map.set(key, list);
    }
    return map;
  }, [data?.weekItems]);

  if (loading && !data) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', padding: 48 }}>
        <Spin size="large" />
      </div>
    );
  }

  const s = data;
  const spendPct =
    s && s.monthCeilingUsd > 0 ? Math.min(100, Math.round((s.monthSpendUsd / s.monthCeilingUsd) * 100)) : 0;

  const tiles = s
    ? [
        {
          title: 'Ý tưởng gốc',
          value: s.coreIdeaCount,
          hint: `${s.coreDraftCount} nháp · ${s.coreUnscoredCount} chưa chấm Fit`,
          to: '/content/pool',
          icon: <FileTextOutlined />,
          tone: s.coreUnscoredCount > 0 ? 'info' : 'default',
        },
        {
          title: 'Góc brand',
          value: s.adaptationCount,
          hint: 'Không phải 1 idea × mọi brand',
          to: '/content/packages',
          icon: <ThunderboltOutlined />,
          tone: 'default',
        },
        {
          title: 'Chờ duyệt bài',
          value: s.reviewCount,
          hint: s.generatingCount > 0 ? `${s.generatingCount} đang tạo` : 'Generate xong mới duyệt',
          to: '/content/packages',
          icon: <CheckCircleOutlined />,
          tone: s.reviewCount > 0 ? 'info' : 'default',
        },
        {
          title: 'Lịch tuần này',
          value: s.scheduledThisWeek,
          hint: `${s.scheduledCount} đang scheduled`,
          to: '/content/calendar?view=week',
          icon: <ClockCircleOutlined />,
          tone: 'default',
        },
        {
          title: 'Đã đăng tuần',
          value: s.publishedThisWeek,
          hint: `${s.publishedTodayCount} hôm nay`,
          to: '/content/packages',
          icon: <CalendarOutlined />,
          tone: 'default',
        },
        {
          title: 'Lỗi cần xử lý',
          value: s.errorCount,
          hint: 'Job / xuất bản / video',
          to: '/content/packages',
          icon: <ExclamationCircleOutlined />,
          tone: s.errorCount > 0 ? 'danger' : 'default',
        },
      ]
    : [];

  return (
    <div className="content-desk">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, marginBottom: 16 }}>
        <div>
          <Typography.Title level={3} style={{ margin: 0 }}>
            Hôm nay
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ margin: '6px 0 0' }}>
            Idea Pool → Góc brand → duyệt → đăng Fanpage/web. Group chỉ copy tay.
          </Typography.Paragraph>
        </div>
        <div style={{ display: 'flex', gap: 8, flexShrink: 0, flexWrap: 'wrap' }}>
          <Link to="/content/pool">
            <Button type="primary" icon={<PlusOutlined />}>
              Idea Pool
            </Button>
          </Link>
          <Link to="/content/packages">
            <Button icon={<ThunderboltOutlined />}>Góc brand</Button>
          </Link>
          <Link to="/content/calendar?view=week">
            <Button icon={<CalendarOutlined />}>Lịch tuần</Button>
          </Link>
          <Link to="/local-os/listings">
            <Button>Thái Nguyên Life</Button>
          </Link>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            Tải lại
          </Button>
        </div>
      </div>

      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} /> : null}
      {s && !s.facebookAppConfigured ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Chưa gắn Facebook App — không Kết nối lại được Fanpage"
          description="Model AI → Facebook: dán App ID + App Secret (app Novixa Healthcare Platform), Redirect URI trùng Meta, rồi Lưu + Test."
          action={
            <Link to="/content/ai#facebook">
              <Button size="small" type="primary">
                Mở Facebook App
              </Button>
            </Link>
          }
        />
      ) : null}
      {s && (s.budgetBlockedCount > 0 || spendPct >= 90) ? (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          message={
            s.budgetBlockedCount > 0
              ? `${s.budgetBlockedCount} bài bị chặn vì trần ngân sách`
              : `Đã dùng ${spendPct}% trần AI tháng này`
          }
          action={
            <Link to="/content/budget">
              <Button size="small">Chi phí AI</Button>
            </Link>
          }
        />
      ) : null}

      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        {tiles.map((t) => (
          <Col xs={12} sm={8} md={4} key={t.title}>
            <Link to={t.to} className={`dashboard-tile dashboard-tile--${t.tone}`} style={{ height: '100%' }}>
              <div className="dashboard-tile__icon">{t.icon}</div>
              <div className="dashboard-tile__body">
                <span className="dashboard-tile__label">{t.title}</span>
                <span className="dashboard-tile__value">{t.value}</span>
                {t.hint ? <span className="dashboard-tile__hint">{t.hint}</span> : null}
              </div>
            </Link>
          </Col>
        ))}
      </Row>

      {s ? (
        <Card size="small" style={{ marginBottom: 16 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap', alignItems: 'center' }}>
            <div>
              <Typography.Text strong>AI Usage tháng này</Typography.Text>
              <Typography.Text type="secondary" style={{ marginLeft: 8 }}>
                {money(s.monthSpendUsd)} / {money(s.monthCeilingUsd)}
              </Typography.Text>
            </div>
            <Progress
              percent={spendPct}
              size="small"
              style={{ minWidth: 220, flex: 1, maxWidth: 360, margin: 0 }}
              status={spendPct >= 90 ? 'exception' : undefined}
            />
            <Link to="/content/budget">Giới hạn chi phí</Link>
          </div>
        </Card>
      ) : null}

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} lg={10}>
          <QueueCard snapshot={s} onReload={() => void load()} />
        </Col>
        <Col xs={24} lg={14}>
          <IdeaRail snapshot={s} />
        </Col>
      </Row>

      <Card
        size="small"
        title="Tuần này"
        extra={<Link to="/content/calendar?view=week">Mở lịch tuần</Link>}
        style={{ marginBottom: 16 }}
      >
        <div className="content-week-grid">
          {weekDays().map((d) => {
            const key = d.format('YYYY-MM-DD');
            const items = (byDay.get(key) ?? []).slice(0, 3);
            const extra = (byDay.get(key) ?? []).length - items.length;
            const today = d.isSame(dayjs(), 'day');
            return (
              <button
                key={key}
                type="button"
                className={`content-week-col${today ? ' content-week-col--today' : ''}`}
                onClick={() => navigate(`/content/calendar?view=week&day=${key}`)}
              >
                <div className="content-week-col__head">
                  <span>{d.format('dd')}</span>
                  <strong>{d.format('DD/MM')}</strong>
                </div>
                {items.length === 0 ? (
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    —
                  </Typography.Text>
                ) : (
                  items.map((it) => <WeekLine key={`${it.kind}-${it.topicId}-${it.publishJobId}-${it.at}`} item={it} />)
                )}
                {extra > 0 ? <span className="content-week-more">+{extra}</span> : null}
              </button>
            );
          })}
        </div>
      </Card>

      <Card title="Thương hiệu" size="small">
        <Table
          size="small"
          rowKey="brandId"
          pagination={false}
          dataSource={s?.brands ?? []}
          locale={{ emptyText: 'Chưa có thương hiệu — vào Thương hiệu & nơi đăng để thêm.' }}
          columns={[
            {
              title: 'Brand',
              dataIndex: 'brandName',
              render: (n: string, r) => (
                <span>
                  <span className="content-brand-dot" style={{ background: brandColor(r.brandCode) }} />
                  {n} <Typography.Text type="secondary">({r.brandCode})</Typography.Text>
                </span>
              ),
            },
            { title: 'Chờ duyệt', dataIndex: 'reviewCount', width: 100 },
            { title: 'Lịch', dataIndex: 'scheduledCount', width: 80 },
            { title: 'Đăng tháng', dataIndex: 'publishedMonthCount', width: 110 },
            { title: 'AI $', dataIndex: 'spendUsd', width: 90, render: (n: number) => money(n) },
          ]}
        />
      </Card>
    </div>
  );
}

const CONNECTOR_LABEL: Record<string, string> = {
  facebook_page: 'Fanpage',
  astro_git: 'Astro',
  wordpress_rest: 'WordPress',
  manual: 'Xuất tay',
};

function QueueCard({ snapshot, onReload }: { snapshot: ContentOpsSnapshot | null; onReload: () => void }) {
  const { message } = App.useApp();
  const [retryId, setRetryId] = useState<string | null>(null);
  const errors = snapshot?.recentErrors ?? [];
  const jobs = snapshot?.activeJobs ?? [];
  const failedPublish = snapshot?.failedPublishJobs ?? [];

  const retry = async (job: ContentOpsFailedPublish) => {
    setRetryId(job.jobId);
    try {
      const updated = await runContentPublishJob(job.jobId);
      if (updated.status === 'Succeeded') message.success(`Đã đăng lại ${CONNECTOR_LABEL[job.connectorType] ?? job.connectorType}`);
      else message.error(updated.lastError || 'Chạy lại thất bại');
      onReload();
    } catch (e) {
      message.error(apiErrorMessage(e, 'Chạy lại thất bại'));
    } finally {
      setRetryId(null);
    }
  };

  return (
    <Card size="small" title="Hàng đợi — làm ngay" style={{ height: '100%' }}>
      {!snapshot || (snapshot.errorCount === 0 && snapshot.reviewCount === 0 && snapshot.coreUnscoredCount === 0 && jobs.length === 0 && failedPublish.length === 0 && snapshot.budgetBlockedCount === 0) ? (
        <Typography.Text type="secondary">Không có việc gấp. Vào Idea Pool nghĩ ý tưởng tuần.</Typography.Text>
      ) : null}

      {snapshot && snapshot.coreUnscoredCount > 0 ? (
        <div className="content-queue-row">
          <Tag color="blue">Fit</Tag>
          <Link to="/content/pool">{snapshot.coreUnscoredCount} ý tưởng chưa chấm Brand Fit</Link>
        </div>
      ) : null}
      {snapshot && snapshot.reviewCount > 0 ? (
        <div className="content-queue-row">
          <Tag color="cyan">Duyệt</Tag>
          <Link to="/content/packages">{snapshot.reviewCount} bài chờ duyệt — góc đã viết</Link>
        </div>
      ) : null}
      {failedPublish.map((job) => (
        <div key={job.jobId} className="content-queue-row" style={{ alignItems: 'flex-start' }}>
          <Tag color="red">Xuất bản</Tag>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Link to={`/content/topics?topic=${job.topicId}`}>{job.topicTitle}</Link>
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              {CONNECTOR_LABEL[job.connectorType] ?? job.connectorType}
              {job.lastError ? ` · ${job.lastError}` : ''}
            </Typography.Text>
          </div>
          <Button
            size="small"
            type="link"
            loading={retryId === job.jobId}
            onClick={() => void retry(job)}
          >
            Chạy lại
          </Button>
        </div>
      ))}
      {errors.map((job) => (
        <div key={job.id} className="content-queue-row">
          <Tag color="red">Job</Tag>
          <div>
            <div>
              {KIND_LABEL[job.kind] ?? job.kind}
              {job.brandName ? ` · ${job.brandName}` : ''}
            </div>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {job.errorMessage || job.title || 'Job thất bại'}
            </Typography.Text>
          </div>
        </div>
      ))}
      {jobs.length > 0 ? (
        <Table<ContentWorkJob>
          size="small"
          rowKey="id"
          pagination={false}
          style={{ marginTop: 8 }}
          dataSource={jobs}
          columns={[
            { title: 'Job nền', dataIndex: 'kind', width: 120, render: (k: string) => KIND_LABEL[k] ?? k },
            { title: 'Nội dung', dataIndex: 'title', ellipsis: true },
            {
              title: '',
              dataIndex: 'status',
              width: 90,
              render: (st: string) => (
                <Tag color={st === 'Running' ? 'processing' : 'default'}>{st === 'Running' ? 'Chạy' : 'Chờ'}</Tag>
              ),
            },
          ]}
        />
      ) : null}
    </Card>
  );
}

function IdeaRail({ snapshot }: { snapshot: ContentOpsSnapshot | null }) {
  const ideas = snapshot?.coreIdeas ?? [];
  const brands = snapshot?.brands ?? [];
  return (
    <Card
      size="small"
      title="Ý tưởng tuần"
      extra={<Link to="/content/pool">Mở Idea Pool</Link>}
      style={{ height: '100%' }}
    >
      {ideas.length === 0 ? (
        <Typography.Text type="secondary">
          Chưa có Core Idea. Dán 10 ý tưởng trong Pool — AI chấm Fit từng brand, không ép đủ 6 bài.
        </Typography.Text>
      ) : (
        <div className="content-idea-rail">
          {ideas.map((idea) => (
            <IdeaCard key={idea.id} idea={idea} brands={brands} />
          ))}
        </div>
      )}
    </Card>
  );
}

function IdeaCard({
  idea,
  brands,
}: {
  idea: ContentPackage;
  brands: ContentOpsSnapshot['brands'];
}) {
  return (
    <Link to="/content/pool" className="content-idea-card">
      <Typography.Text strong>{idea.title}</Typography.Text>
      {idea.coreIdea?.insight ? (
        <Typography.Paragraph type="secondary" ellipsis={{ rows: 2 }} style={{ margin: '4px 0 8px', fontSize: 12 }}>
          {idea.coreIdea.insight}
        </Typography.Paragraph>
      ) : (
        <div style={{ height: 8 }} />
      )}
      <div className="content-idea-chips">
        {brands.map((b) => {
          const fit = (idea.brandFits ?? []).find((f) => f.brandId === b.brandId);
          return (
            <span
              key={b.brandId}
              className="content-brand-chip"
              style={{ borderColor: brandColor(b.brandCode), color: brandColor(b.brandCode) }}
              title={fit?.reason || fit?.angle || b.brandName}
            >
              {b.brandCode} {fitMark(fit?.verdict)}
            </span>
          );
        })}
      </div>
      {(idea.adaptationCount ?? 0) > 0 ? (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {idea.adaptationCount} góc đã tạo
        </Typography.Text>
      ) : (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          Chưa tạo góc — chấm Fit rồi tick
        </Typography.Text>
      )}
    </Link>
  );
}

function WeekLine({ item }: { item: ContentCalendarItem }) {
  return (
    <div className="content-week-item">
      <span className="content-brand-dot" style={{ background: brandColor(item.brandCode) }} />
      <span className="content-week-item__text">
        {item.brandCode}
        {item.channel ? ` · ${channelLabel(item.channel)}` : ''} · {item.title}
      </span>
    </div>
  );
}
