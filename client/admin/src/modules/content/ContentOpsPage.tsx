import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert, Button, Card, Col, Row, Space, Spin, Tag, Typography } from 'antd';
import {
  CalendarOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  FileTextOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentOps,
  fetchContentPackages,
  fetchContentTopics,
  fetchContentEpisodesNeedContent,
  type ContentCalendarItem,
  type ContentOpsSnapshot,
  type ContentPackage,
  type ContentTopic,
  type EpisodeNeedContent,
} from '@/shared/api/content.api';
import { channelLabel } from './content-brand';
import {
  episodeSequenceLabel,
  packageForTopic,
  qualityGateLabel,
  useTopicLineageIndex,
  type TopicSeriesLineage,
} from './content-lineage';
import { PublishJobStatusTag, StatusTag, connectorLabel } from './ContentTopicsPage';
import { fetchAgentProposals, type AgentProposal } from '@/shared/api/content-agent.api';
import { useContentBrand } from './content-brand-context';
import { ContentPageHeader } from './ContentPageHeader';

function LineageLine({
  lineageIndex,
  topicId,
  brandName,
}: {
  lineageIndex: Map<string, TopicSeriesLineage> | null;
  topicId?: string | null;
  brandName?: string | null;
}) {
  const textStyle = { fontSize: 12 };
  if (topicId && !lineageIndex) {
    return (
      <div style={{ marginTop: 4 }}>
        <Typography.Text type="secondary" style={textStyle}>
          {brandName ? `${brandName} · ` : ''}Đang tải nguồn bài…
        </Typography.Text>
      </div>
    );
  }
  const lineage = topicId ? lineageIndex?.get(topicId) : undefined;
  if (lineage) {
    const { series, episode } = lineage;
    return (
      <div style={{ marginTop: 4 }}>
        <Typography.Text type="secondary" style={textStyle}>
          {series.brandName} · {series.code} ·{' '}
          <Link to={`/content/article-series/${series.id}/episodes/${episode.id}`}>
            {episodeSequenceLabel(episode.episodeNo)} · {episode.title}
          </Link>
        </Typography.Text>
      </div>
    );
  }
  const parts = [brandName, topicId ? 'Bài độc lập' : null].filter(Boolean);
  if (parts.length === 0) return null;
  return (
    <div style={{ marginTop: 4 }}>
      <Typography.Text type="secondary" style={textStyle}>
        {parts.join(' · ')}
      </Typography.Text>
    </div>
  );
}

const KIND_LABEL: Record<string, string> = {
  generate_topic: 'Viết bài',
  generate_package: 'Generate nơi đăng',
  publish_topic: 'Xuất bản',
  video_mvp: 'Video MVP',
  video_render: 'Render video',
  brand_adapt: 'Brand Fit',
};

const CONNECTOR_LABEL: Record<string, string> = {
  facebook_page: 'Facebook Page',
  facebook_group: 'Facebook Group',
  site: 'Website',
  wordpress: 'WordPress',
};

export function ContentOpsPage() {
  const [data, setData] = useState<ContentOpsSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const lineageIndex = useTopicLineageIndex();
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [episodesNeedContent, setEpisodesNeedContent] = useState<EpisodeNeedContent[]>([]);
  const [packages, setPackages] = useState<ContentPackage[]>([]);
  const [proposals, setProposals] = useState<AgentProposal[]>([]);
  const workspace = useContentBrand();
  const brandId = workspace.brandId;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      void fetchAgentProposals({ brand: brandId })
        .then(setProposals)
        .catch(() => setProposals([]));
      const [opsData, reviewData, approvedData, episodesData, packageRows] = await Promise.all([
        fetchContentOps(),
        fetchContentTopics({ status: 'Review' }),
        fetchContentTopics({ status: 'Approved' }),
        fetchContentEpisodesNeedContent(),
        fetchContentPackages(),
      ]);
      setData(opsData);
      setTopics([...reviewData, ...approvedData]);
      setEpisodesNeedContent(episodesData);
      setPackages(packageRows);
      setError(null);
    } catch (e) {
      setError(apiErrorMessage(e, 'Không tải được bảng điều khiển'));
    } finally {
      setLoading(false);
    }
  }, [brandId]);

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

  const nextEpisodes = useMemo(() => {
    const bySeries = new Map<string, EpisodeNeedContent[]>();
    for (const ep of episodesNeedContent) {
      const list = bySeries.get(ep.seriesId);
      if (list) list.push(ep);
      else bySeries.set(ep.seriesId, [ep]);
    }
    return [...bySeries.values()].map((list) => {
      const sorted = [...list].sort((a, b) => a.episodeNo - b.episodeNo);
      return { episode: sorted[0], laterCount: sorted.length - 1 };
    });
  }, [episodesNeedContent]);

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

  // Chọn một thương hiệu ở đầu trang thì chỉ hiện bài của thương hiệu đó.
  const scopedTopics = brandId ? topics.filter((t) => t.brandId === brandId) : topics;
  // Queue: Chờ duyệt (Review)
  const reviewTopics = scopedTopics.filter((t) => t.status === 'Review');
  
  // Queue: Đã duyệt (Approved)
  const approvedTopics = scopedTopics.filter((t) => t.status === 'Approved');

  // Lịch đăng của Agent: đề xuất chờ duyệt và lượt đã duyệt trong 7 ngày tới (gồm video từ Video Studio).
  // Hôm nay đã có ở thẻ "Lịch hôm nay", nên thẻ này bắt đầu từ ngày mai.
  const fromMs = dayjs().add(1, 'day').startOf('day').valueOf();
  const pendingProposals = proposals.filter((p) => p.status === 'Proposed');
  const todayEndMs = dayjs().endOf('day').valueOf();
  const manualToday = proposals.filter(
    (p) => p.status === 'Approved' && !p.publishJobId && new Date(p.publishAt).getTime() <= todayEndMs,
  );
  const upcoming = proposals
    .filter((p) => p.status === 'Approved' && new Date(p.publishAt).getTime() >= fromMs && new Date(p.publishAt).getTime() < fromMs + 7 * 86_400_000)
    .sort((a, b) => a.publishAt.localeCompare(b.publishAt));
  const calendarLink = (tab?: string) => {
    const q = new URLSearchParams();
    if (tab) q.set('tab', tab);
    if (brandId) q.set('brand', brandId);
    const qs = q.toString();
    return qs ? `/content/calendar?${qs}` : '/content/calendar';
  };
  
  // Queue: Lịch hôm nay (Today's schedule)
  const today = dayjs().format('YYYY-MM-DD');
  const todayItems = (byDay.get(today) ?? []).filter((it) => it.status === 'Scheduled' || it.status === 'Queued');
  
  // Queue: Có vấn đề (Issues)
  const failedPublishJobs = data?.failedPublishJobs ?? [];
  const shownErrors = (data?.recentErrors ?? []).slice(0, 3);
  const issues = [...failedPublishJobs, ...shownErrors];

  return (
    <div className="content-desk">
      <ContentPageHeader
        title="Tổng quan"
        description="Việc cần làm hôm nay: viết, duyệt, lên lịch, xử lý lỗi."
        extra={
          <>
            <Link to={calendarLink()}>
              <Button icon={<CalendarOutlined />}>Lịch đăng</Button>
            </Link>
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
              Tải lại
            </Button>
          </>
        }
      />

      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} /> : null}

      {/* Summary Counts */}
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={12} sm={8} md={4}>
          <Card size="small" style={{ textAlign: 'center' }}>
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              Cần viết
            </Typography.Text>
            <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#722ed1' }}>
              {nextEpisodes.length}
            </Typography.Title>
          </Card>
        </Col>
        <Col xs={12} sm={8} md={4}>
          <Card size="small" style={{ textAlign: 'center' }}>
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              Chờ duyệt
            </Typography.Text>
            <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#1677ff' }}>
              {reviewTopics.length}
            </Typography.Title>
          </Card>
        </Col>
        <Col xs={12} sm={8} md={4}>
          <Card size="small" style={{ textAlign: 'center' }}>
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              Đã duyệt
            </Typography.Text>
            <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#52c41a' }}>
              {approvedTopics.length}
            </Typography.Title>
          </Card>
        </Col>
        <Col xs={12} sm={8} md={4}>
          <Card size="small" style={{ textAlign: 'center' }}>
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              Lịch hôm nay
            </Typography.Text>
            <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#13c2c2' }}>
              {todayItems.length}
            </Typography.Title>
          </Card>
        </Col>
        <Col xs={12} sm={8} md={4}>
          <Card size="small" style={{ textAlign: 'center' }}>
            <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12 }}>
              Có vấn đề
            </Typography.Text>
            <Typography.Title level={3} style={{ margin: '4px 0 0', color: '#ff4d4f' }}>
              {issues.length}
            </Typography.Title>
          </Card>
        </Col>
      </Row>

      {/* Queue 1: CẦN VIẾT */}
      <Card 
        size="small" 
        title={<><FileTextOutlined style={{ marginRight: 8, color: '#722ed1' }} />Cần viết ({nextEpisodes.length})</>}
        style={{ marginBottom: 16 }}
      >
        {nextEpisodes.length === 0 ? (
          <Typography.Text type="secondary">Không có Episode nào đang chờ sản xuất.</Typography.Text>
        ) : (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            {nextEpisodes.slice(0, 10).map(({ episode, laterCount }) => (
              <div key={episode.episodeId} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                <div style={{ flex: 1 }}>
                  <Typography.Text strong>{episode.brandName}</Typography.Text>
                  <div style={{ marginTop: 4 }}>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {episode.seriesName} · E{String(episode.episodeNo).padStart(2, '0')}
                    </Typography.Text>
                  </div>
                  <div style={{ marginTop: 4 }}>
                    <Typography.Text>{episode.title}</Typography.Text>
                  </div>
                  {episode.objective && (
                    <div style={{ marginTop: 4 }}>
                      <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                        {episode.objective}
                      </Typography.Text>
                    </div>
                  )}
                  {laterCount > 0 && (
                    <div style={{ marginTop: 4 }}>
                      <Link to={`/content/article-series/${episode.seriesId}`} style={{ fontSize: 12 }}>
                        +{laterCount} Episode sau
                      </Link>
                    </div>
                  )}
                </div>
                <Space direction="vertical" size={4} style={{ alignItems: 'flex-end' }}>
                  <Tag color="purple">Chưa có Content</Tag>
                  <Link to={`/content/article-series/${episode.seriesId}/episodes/${episode.episodeId}`}>
                    <Button size="small" type="primary">Tạo nội dung</Button>
                  </Link>
                </Space>
              </div>
            ))}
            {nextEpisodes.length > 10 && (
              <Typography.Text type="secondary">
                ... và {nextEpisodes.length - 10} Series khác
              </Typography.Text>
            )}
          </Space>
        )}
      </Card>

      {/* Queue 2: CHỜ DUYỆT */}
      <Card
        size="small"
        title={<><CheckCircleOutlined style={{ marginRight: 8, color: '#1677ff' }} />Chờ duyệt ({reviewTopics.length})</>}
        style={{ marginBottom: 16 }}
      >
        {reviewTopics.length === 0 ? (
          <Typography.Text type="secondary">Không có bài chờ duyệt</Typography.Text>
        ) : (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            {reviewTopics.slice(0, 10).map((topic) => {
              const gate = packageForTopic(packages, topic.id)?.qualityGate;
              return (
                <div key={topic.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                  <div style={{ flex: 1 }}>
                    <Link to={`/content/topics/${topic.id}`} style={{ fontWeight: 600 }}>{topic.title}</Link>
                    <LineageLine lineageIndex={lineageIndex} topicId={topic.id} brandName={topic.brandName} />
                    <div style={{ marginTop: 4 }}>
                      <Typography.Text type={!gate ? 'secondary' : gate.passed ? 'success' : 'warning'} style={{ fontSize: 12 }}>
                        Quality Gate: {qualityGateLabel(gate)}
                      </Typography.Text>
                    </div>
                  </div>
                  <Space direction="vertical" size={4} style={{ alignItems: 'flex-end' }}>
                    <StatusTag status={topic.status} />
                    <Link to={`/content/topics/${topic.id}`}>
                      <Button size="small" type="primary">Duyệt</Button>
                    </Link>
                  </Space>
                </div>
              );
            })}
            {reviewTopics.length > 10 && (
              <Link to="/content/topics">Xem tất cả {reviewTopics.length} bài</Link>
            )}
          </Space>
        )}
      </Card>

      {/* Queue 3: ĐÃ DUYỆT */}
      <Card
        size="small"
        title={<><CheckCircleOutlined style={{ marginRight: 8, color: '#52c41a' }} />Đã duyệt ({approvedTopics.length})</>}
        style={{ marginBottom: 16 }}
      >
        {approvedTopics.length === 0 ? (
          <Typography.Text type="secondary">Không có bài đã duyệt chưa lên lịch</Typography.Text>
        ) : (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            {approvedTopics.slice(0, 10).map((topic) => (
                <div key={topic.id} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                  <div style={{ flex: 1 }}>
                    <Link to={`/content/topics/${topic.id}`} style={{ fontWeight: 600 }}>{topic.title}</Link>
                    <LineageLine lineageIndex={lineageIndex} topicId={topic.id} brandName={topic.brandName} />
                  </div>
                  <Space>
                    <StatusTag status={topic.status} />
                    <Link to={`/content/topics/${topic.id}`}>
                      <Button size="small">Xem</Button>
                    </Link>
                  </Space>
                </div>
            ))}
            {approvedTopics.length > 10 && (
              <Link to="/content/topics">Xem tất cả {approvedTopics.length} bài</Link>
            )}
          </Space>
        )}
      </Card>

      <Row gutter={[16, 16]}>
        {/* Queue 4: LỊCH HÔM NAY */}
        <Col xs={24} md={12}>
          <Card
            size="small"
            title={<><CalendarOutlined style={{ marginRight: 8, color: '#13c2c2' }} />Lịch hôm nay ({todayItems.length})</>}
            extra={<Link to="/content/calendar">Xem lịch</Link>}
          >
            {todayItems.length === 0 ? (
              <Typography.Text type="secondary">Không có lịch hôm nay</Typography.Text>
            ) : (
              <Space direction="vertical" size={8} style={{ width: '100%' }}>
                {todayItems.map((item) => {
                  const time = dayjs(item.at).format('HH:mm');
                  return (
                    <div key={`${item.kind}-${item.topicId}-${item.at}`} style={{ padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
                        <Typography.Text strong style={{ minWidth: 40 }}>{time}</Typography.Text>
                        <div style={{ flex: 1 }}>
                          <div>
                            {item.channel ? <Tag>{channelLabel(item.channel)}</Tag> : null}
                            {item.topicId ? (
                              <Link to={`/content/topics/${item.topicId}`} style={{ fontSize: 12 }}>{item.title}</Link>
                            ) : (
                              <Typography.Text style={{ fontSize: 12 }}>{item.title}</Typography.Text>
                            )}
                          </div>
                          <LineageLine lineageIndex={lineageIndex} topicId={item.topicId} brandName={item.brandName} />
                          <div style={{ marginTop: 4 }}>
                            {item.status === 'Queued' ? (
                              <PublishJobStatusTag status={item.status} />
                            ) : (
                              <StatusTag status={item.status} />
                            )}
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </Space>
            )}
          </Card>
        </Col>

        {/* Queue 5: CÓ VẤN ĐỀ */}
        <Col xs={24} md={12}>
          <Card
            size="small"
            title={<><ExclamationCircleOutlined style={{ marginRight: 8, color: '#ff4d4f' }} />Có vấn đề ({issues.length})</>}
          >
            {issues.length === 0 ? (
              <Typography.Text type="secondary">Không có vấn đề</Typography.Text>
            ) : (
              <Space direction="vertical" size={8} style={{ width: '100%' }}>
                {failedPublishJobs.map((job) => (
                    <div key={job.jobId} style={{ padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                      <Space size={4}>
                        <PublishJobStatusTag status="Failed" />
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          Xuất bản · {CONNECTOR_LABEL[job.connectorType] ?? connectorLabel(job.connectorType).title}
                        </Typography.Text>
                      </Space>
                      <div style={{ marginTop: 4 }}>
                        <Link to={`/content/topics/${job.topicId}`}>{job.topicTitle}</Link>
                      </div>
                      <LineageLine lineageIndex={lineageIndex} topicId={job.topicId} />
                      {job.lastError ? (
                        <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                          {job.lastError}
                        </Typography.Text>
                      ) : null}
                    </div>
                ))}
                {shownErrors.map((job) => (
                  <div key={job.id} style={{ padding: '8px 0', borderBottom: '1px solid #f0f0f0' }}>
                    <Space size={4}>
                      <PublishJobStatusTag status="Failed" />
                      {job.topicId ? (
                        <Link to={`/content/topics/${job.topicId}`}>{KIND_LABEL[job.kind] ?? job.kind}</Link>
                      ) : (
                        <Typography.Text>{KIND_LABEL[job.kind] ?? job.kind}</Typography.Text>
                      )}
                    </Space>
                    <LineageLine lineageIndex={lineageIndex} topicId={job.topicId} brandName={job.brandName} />
                    <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginTop: 4 }}>
                      {job.errorMessage || job.title || 'Job thất bại'}
                    </Typography.Text>
                  </div>
                ))}
              </Space>
            )}
          </Card>
        </Col>
      </Row>

      {/* Lịch đăng của Agent */}
      <Card
        size="small"
        title={<><CalendarOutlined style={{ marginRight: 8, color: '#13c2c2' }} />Sắp đăng 7 ngày tới, từ ngày mai ({upcoming.length})</>}
        extra={
          <Space size={12}>
            {manualToday.length ? (
              <Link to={calendarLink('dangtay')}>
                <Tag color="orange" style={{ marginInlineEnd: 0 }}>{manualToday.length} lượt đăng tay hôm nay</Tag>
              </Link>
            ) : null}
            {pendingProposals.length ? (
              <Link to={calendarLink('duyet')}>{pendingProposals.length} đề xuất chờ duyệt</Link>
            ) : null}
            <Link to={calendarLink()}>Xem lịch</Link>
          </Space>
        }
        style={{ marginBottom: 16 }}
      >
        {upcoming.length === 0 ? (
          <Typography.Text type="secondary">Chưa có lượt đăng nào đã duyệt trong 7 ngày tới.</Typography.Text>
        ) : (
          <Space direction="vertical" size={6} style={{ width: '100%' }}>
            {upcoming.slice(0, 8).map((p) => (
              <div key={p.id} style={{ display: 'flex', gap: 8, alignItems: 'baseline', flexWrap: 'wrap' }}>
                <Typography.Text type="secondary" style={{ minWidth: 120 }}>
                  {dayjs(p.publishAt).format('ddd DD/MM HH:mm')}
                </Typography.Text>
                <Tag>{p.targetLabel}</Tag>
                {!p.publishJobId ? <Tag color="orange">Đăng tay</Tag> : null}
                <Link to={`/content/topics/${p.topicId}`}>{p.title}</Link>
              </div>
            ))}
            {upcoming.length > 8 ? <Link to={calendarLink('duyet')}>Xem thêm {upcoming.length - 8} lượt</Link> : null}
          </Space>
        )}
      </Card>

      {/* System Alerts */}
      {s && !s.facebookAppConfigured ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Chưa gắn Facebook App — không Kết nối lại được Fanpage"
          description="Model AI → Facebook: dán App ID + App Secret (app Novixa Healthcare Platform), Redirect URI trùng Meta, rồi Lưu + Test."
          action={
            <Link to="/content/ai-text#facebook">
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
    </div>
  );
}
