import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Alert, Button, Calendar, Card, DatePicker, Segmented, Select, Space, Spin, Tag, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchPlatformPosts,
  markPlatformOutside,
  matchPlatformPost,
  syncPlatformPosts,
  type PlatformPost,
  type PlatformSyncReport,
} from '@/shared/api/content-agent.api';
import {
  fetchContentBrands,
  fetchContentCalendar,
  fetchContentChannels,
  fetchContentTopics,
  type ContentArticleEpisode,
  type ContentArticleSeries,
  type ContentBrand,
  type ContentCalendarItem,
  type ContentChannelTarget,
  type ContentTopic,
} from '@/shared/api/content.api';
import { useContentBrand } from './content-brand-context';
import { brandColor, channelLabel } from './content-brand';
import { episodeSequenceLabel, useTopicLineageIndex } from './content-lineage';

dayjs.extend(isoWeek);

// V2-D.1: Calendar Distribution Control Center
// Hierarchy: Brand → Series → Episode → Schedule

type ScheduleItem = ContentCalendarItem;

type EpisodeSchedule = {
  episode: ContentArticleEpisode;
  schedules: ScheduleItem[];
};

type SeriesGroup = {
  series: ContentArticleSeries;
  episodes: EpisodeSchedule[];
};

type SecondaryGroups = {
  standalone: ScheduleItem[];
  unlinked: ScheduleItem[];
  package: ScheduleItem[];
  other: ScheduleItem[];
};

type BrandDaySchedule = {
  brand: { id: string; code: string; name: string };
  seriesGroups: SeriesGroup[];
  secondary: SecondaryGroups;
};

function statusText(item: ScheduleItem) {
  if (!item.sourceLabel) return item.status;
  if (item.status === 'scheduled') return 'Hẹn giờ';
  if (item.status === 'published') return 'Đã đăng';
  return item.status;
}

function platformTone(label?: string | null) {
  if (label === 'YouTube Studio') return { color: 'red', background: '#fef2f2' };
  if (label === 'Business Suite') return { color: 'purple', background: '#f5f3ff' };
  return null;
}

const STATUS_COLOR: Record<string, string> = {
  Draft: 'default',
  Generating: 'processing',
  Review: 'blue',
  Approved: 'cyan',
  Scheduled: 'geekblue',
  Published: 'green',
  BudgetBlocked: 'red',
  Rejected: 'orange',
  Queued: 'processing',
  Running: 'processing',
  Succeeded: 'green',
  Failed: 'red',
};

function rangeFor(view: 'month' | 'week', cursor: Dayjs) {
  if (view === 'week') {
    const from = cursor.startOf('isoWeek');
    return { from: from.toISOString(), to: from.add(7, 'day').toISOString(), cursor: from };
  }
  const from = cursor.startOf('month');
  return { from: from.toISOString(), to: from.add(1, 'month').toISOString(), cursor: from };
}

export function ContentCalendarPage({ embedded = false }: { embedded?: boolean } = {}) {
  const [params] = useSearchParams();
  const lineageIndex = useTopicLineageIndex();
  const [view, setView] = useState<'month' | 'week'>(() =>
    params.get('view') === 'month' ? 'month' : 'week',
  );
  const [cursor, setCursor] = useState(() => {
    const day = params.get('day');
    return day && dayjs(day).isValid() ? dayjs(day) : dayjs();
  });
  const workspace = useContentBrand();
  const brandId = workspace.brandId;
  const [channel, setChannel] = useState<string | undefined>();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [items, setItems] = useState<ContentCalendarItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [syncing, setSyncing] = useState(false);
  const [syncNote, setSyncNote] = useState<string | null>(null);
  const [syncError, setSyncError] = useState<string | null>(null);
  const [unmatched, setUnmatched] = useState<PlatformPost[]>([]);
  const [topics, setTopics] = useState<ContentTopic[]>([]);
  const [channelTargets, setChannelTargets] = useState<ContentChannelTarget[]>([]);
  const [picks, setPicks] = useState<Record<string, string>>({});
  const [matchingId, setMatchingId] = useState<string | null>(null);
  const [markTopicId, setMarkTopicId] = useState<string>();
  const [markChannelId, setMarkChannelId] = useState<string>();
  const [markKind, setMarkKind] = useState<string>('reel');
  const [markAt, setMarkAt] = useState<Dayjs | null>(null);
  const [marking, setMarking] = useState(false);

  const window = useMemo(() => rangeFor(view, cursor), [view, cursor]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [cal, brandList] = await Promise.all([
        fetchContentCalendar({ from: window.from, to: window.to, brandId }),
        fetchContentBrands(true),
      ]);
      setItems(cal);
      setBrands(brandList);
      setError(null);
    } catch (e) {
      setError(apiErrorMessage(e, 'Không tải được lịch'));
    } finally {
      setLoading(false);
    }
  }, [window.from, window.to, brandId]);

  useEffect(() => {
    void load();
  }, [load]);

  const loadUnmatched = useCallback(async () => {
    if (!brandId) {
      setUnmatched([]);
      setChannelTargets([]);
      return;
    }
    try {
      const [posts, topicList, channelList] = await Promise.all([
        fetchPlatformPosts(brandId, true),
        fetchContentTopics({ brandId }),
        fetchContentChannels(brandId),
      ]);
      setUnmatched(posts);
      setTopics(topicList);
      setChannelTargets(channelList.filter((c) => c.isActive && (c.channelType === 'facebook_page' || c.channelType === 'youtube_channel')));
    } catch (e) {
      const msg = apiErrorMessage(e, '');
      if (msg.includes('442')) setSyncError(msg);
      setUnmatched([]);
      setChannelTargets([]);
    }
  }, [brandId]);

  useEffect(() => {
    void loadUnmatched();
  }, [loadUnmatched]);

  const syncNow = async () => {
    if (!brandId) return;
    setSyncing(true);
    setSyncError(null);
    try {
      const report: PlatformSyncReport = await syncPlatformPosts(brandId);
      const lines = report.byChannel.map(
        (c) => `${c.channelName}: ${c.upserted} bài, ${c.matched} đã ghép, ${c.unmatched} chưa ghép`,
      );
      setSyncNote(
        report.migrationRequired
          ? report.warnings.join(' ')
          : [`${report.matched} đã ghép, ${report.unmatched} chưa ghép.`, ...lines, ...report.warnings].join(' '),
      );
      if (report.migrationRequired) setSyncError(report.warnings[0] ?? 'Cần migration 442.');
      await Promise.all([load(), loadUnmatched()]);
    } catch (e) {
      setSyncError(apiErrorMessage(e, 'Không đồng bộ được lịch nền tảng'));
    } finally {
      setSyncing(false);
    }
  };

  const assignTopic = async (post: PlatformPost) => {
    const topicId = picks[post.id];
    if (!topicId) return;
    setMatchingId(post.id);
    try {
      await matchPlatformPost(post.id, topicId);
      await Promise.all([load(), loadUnmatched()]);
    } catch (e) {
      setSyncError(apiErrorMessage(e, 'Không ghép được bài'));
    } finally {
      setMatchingId(null);
    }
  };

  const markOutside = async () => {
    if (!markTopicId || !markChannelId || !markAt) return;
    setMarking(true);
    setSyncError(null);
    try {
      await markPlatformOutside({
        topicId: markTopicId,
        channelId: markChannelId,
        publishAt: markAt.toISOString(),
        kind: markKind,
      });
      setSyncNote('Đã ghi lịch hẹn ngoài hệ thống. Bài này không được đề xuất lại trên kênh đó.');
      await Promise.all([load(), loadUnmatched()]);
    } catch (e) {
      setSyncError(apiErrorMessage(e, 'Không ghi được lịch hẹn'));
    } finally {
      setMarking(false);
    }
  };

  const channels = useMemo(() => {
    const set = new Set<string>();
    for (const it of items) {
      if (it.channel) set.add(it.channel);
    }
    return [...set];
  }, [items]);

  const visible = useMemo(
    () => (channel ? items.filter((it) => it.channel === channel) : items),
    [items, channel],
  );

  const byDay = useMemo(() => {
    const map = new Map<string, ContentCalendarItem[]>();
    for (const item of visible) {
      const key = dayjs(item.at).format('YYYY-MM-DD');
      const list = map.get(key) ?? [];
      list.push(item);
      map.set(key, list);
    }
    return map;
  }, [visible]);

  const weekDays = useMemo(() => {
    const start = dayjs(window.from);
    return Array.from({ length: 7 }, (_, i) => start.add(i, 'day'));
  }, [window.from]);

  const cellItems = (date: Dayjs) => byDay.get(date.format('YYYY-MM-DD')) ?? [];

  // V2-D.1: Group items by Brand → Series → Episode
  const groupedByDay = useMemo(() => {
    const result = new Map<string, BrandDaySchedule[]>();
    
    for (const [dateKey, items] of byDay.entries()) {
      const brandMap = new Map<string, BrandDaySchedule>();
      
      for (const item of items) {
        // Get or create brand group
        let brandSchedule = brandMap.get(item.brandId);
        if (!brandSchedule) {
          brandSchedule = {
            brand: { id: item.brandId, code: item.brandCode, name: item.brandName },
            seriesGroups: [],
            secondary: { standalone: [], unlinked: [], package: [], other: [] },
          };
          brandMap.set(item.brandId, brandSchedule);
        }
        
        // Determine if item is Series Content
        const lineage = item.topicId && lineageIndex ? lineageIndex.get(item.topicId) : null;
        
        if (lineage) {
          // Series Content
          const { series, episode } = lineage;
          let seriesGroup = brandSchedule.seriesGroups.find((s) => s.series.id === series.id);
          if (!seriesGroup) {
            seriesGroup = { series, episodes: [] };
            brandSchedule.seriesGroups.push(seriesGroup);
          }
          
          let episodeSchedule = seriesGroup.episodes.find((e) => e.episode.id === episode.id);
          if (!episodeSchedule) {
            episodeSchedule = { episode, schedules: [] };
            seriesGroup.episodes.push(episodeSchedule);
          }
          
          episodeSchedule.schedules.push(item);
        } else {
          // Classify non-series content
          if (item.topicId && !lineageIndex) {
            // Lineage index still loading - defer to standalone
            brandSchedule.secondary.standalone.push(item);
          } else if (item.topicId) {
            // Topic exists but no lineage → unlinked
            brandSchedule.secondary.unlinked.push(item);
          } else if (item.packageId) {
            // Package/Brand Angle
            brandSchedule.secondary.package.push(item);
          } else {
            // Other (publish job, etc.)
            brandSchedule.secondary.other.push(item);
          }
        }
      }
      
      // Sort series by name, episodes by episodeNo, schedules by time
      for (const brandSchedule of brandMap.values()) {
        brandSchedule.seriesGroups.sort((a, b) => a.series.name.localeCompare(b.series.name));
        for (const seriesGroup of brandSchedule.seriesGroups) {
          seriesGroup.episodes.sort((a, b) => a.episode.episodeNo - b.episode.episodeNo);
          for (const episodeSchedule of seriesGroup.episodes) {
            episodeSchedule.schedules.sort((a, b) => dayjs(a.at).valueOf() - dayjs(b.at).valueOf());
          }
        }
        // Sort secondary groups
        brandSchedule.secondary.standalone.sort((a, b) => dayjs(a.at).valueOf() - dayjs(b.at).valueOf());
        brandSchedule.secondary.unlinked.sort((a, b) => dayjs(a.at).valueOf() - dayjs(b.at).valueOf());
        brandSchedule.secondary.package.sort((a, b) => dayjs(a.at).valueOf() - dayjs(b.at).valueOf());
        brandSchedule.secondary.other.sort((a, b) => dayjs(a.at).valueOf() - dayjs(b.at).valueOf());
      }
      
      result.set(dateKey, Array.from(brandMap.values()));
    }
    
    return result;
  }, [byDay, lineageIndex]);

  // Render a single schedule item
  const renderScheduleItem = (item: ScheduleItem) => {
    const time = dayjs(item.at).format('HH:mm');
    const lineage = item.topicId && lineageIndex ? lineageIndex.get(item.topicId) : null;
    const tone = platformTone(item.sourceLabel);

    return (
      <div
        className="content-calendar-schedule"
        style={tone ? { background: tone.background, borderRadius: 4, padding: '2px 4px' } : undefined}
      >
        {item.sourceLabel ? (
          <Tag color={tone?.color} style={{ margin: 0, fontSize: 11 }}>
            {item.sourceLabel}
          </Tag>
        ) : null}
        <span style={{ color: '#64748b' }}>
          {item.channel ? channelLabel(item.channel) : 'Kênh không xác định'}
        </span>
        <span style={{ color: '#64748b' }}>·</span>
        <span style={{ color: '#64748b', fontVariantNumeric: 'tabular-nums' }}>{time}</span>
        <span style={{ color: '#64748b' }}>·</span>
        <Tag color={tone ? tone.color : (STATUS_COLOR[item.status] ?? 'default')} style={{ margin: 0, fontSize: 11 }}>
          {statusText(item)}
        </Tag>
        <span style={{ color: '#64748b' }}>·</span>
        {lineage ? (
          <Link
            to={`/content/article-series/${lineage.series.id}/episodes/${lineage.episode.id}`}
            style={{ fontSize: 12 }}
          >
            Xem Content
          </Link>
        ) : item.topicId ? (
          <Link to="/content/topics" style={{ fontSize: 12 }}>
            Xem Content
          </Link>
        ) : item.packageId ? (
          <Link to={`/content/packages?package=${item.packageId}`} style={{ fontSize: 12 }}>
            Xem Brand Angle
          </Link>
        ) : (
          <span style={{ fontSize: 12, color: '#94a3b8' }}>—</span>
        )}
      </div>
    );
  };

  // Render secondary groups
  const renderSecondaryGroup = (label: string, items: ScheduleItem[]) => {
    if (items.length === 0) return null;
    return (
      <div className="content-calendar-secondary">
        <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', marginBottom: 4 }}>
          {label}
        </Typography.Text>
        {items.map((item, idx) => (
          <div key={`${item.topicId || item.packageId || item.publishJobId}-${item.at}-${idx}`} style={{ marginBottom: 4 }}>
            <div style={{ fontSize: 12, color: '#475569', marginBottom: 2 }}>
              {item.title}
            </div>
            {renderScheduleItem(item)}
          </div>
        ))}
      </div>
    );
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', marginBottom: 16 }}>
        <div>
          {embedded ? null : (
            <Typography.Title level={4} style={{ margin: 0 }}>
              Lịch đăng
            </Typography.Title>
          )}
          <Typography.Text type="secondary">Màu theo thương hiệu, không theo trạng thái.</Typography.Text>
        </div>
        <Space wrap>
          <Segmented
            value={view}
            onChange={(v) => setView(v as 'month' | 'week')}
            options={[
              { label: 'Tuần', value: 'week' },
              { label: 'Tháng', value: 'month' },
            ]}
          />
          <Select
            allowClear
            placeholder="Mọi kênh"
            style={{ minWidth: 140 }}
            value={channel}
            onChange={(v) => setChannel(v)}
            options={channels.map((c) => ({ value: c, label: channelLabel(c) }))}
          />
          <Button onClick={() => setCursor(dayjs())}>Hôm nay</Button>
          <Button type="primary" loading={syncing} disabled={!brandId} onClick={() => void syncNow()}>
            Đồng bộ ngay
          </Button>
        </Space>
      </div>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 12 }}
        message="TikTok: đánh dấu tay. API không cho đọc lịch đã đăng. Fanpage hẹn trong Business Suite đôi khi Graph không trả — dùng nút bên dưới."
      />
      {brandId ? (
        <Card size="small" title="Đánh dấu đã hẹn ngoài hệ thống" style={{ marginBottom: 16 }}>
          <Space wrap>
            <Select
              showSearch
              placeholder="Bài viết"
              style={{ minWidth: 260 }}
              value={markTopicId}
              onChange={setMarkTopicId}
              optionFilterProp="label"
              options={topics.map((t) => ({ value: t.id, label: t.title }))}
            />
            <Select
              placeholder="Kênh"
              style={{ minWidth: 200 }}
              value={markChannelId}
              onChange={(value) => {
                setMarkChannelId(value);
                const target = channelTargets.find((c) => c.id === value);
                setMarkKind(target?.channelType === 'youtube_channel' ? 'short' : 'reel');
              }}
              options={channelTargets.map((c) => ({
                value: c.id,
                label: `${c.name} · ${c.channelType === 'youtube_channel' ? 'YouTube' : 'Fanpage'}`,
              }))}
            />
            <Select
              style={{ width: 120 }}
              value={markKind}
              onChange={setMarkKind}
              options={[
                { value: 'reel', label: 'Reel' },
                { value: 'post', label: 'Bài viết' },
                { value: 'video', label: 'Video dài' },
                { value: 'short', label: 'Short' },
              ]}
            />
            <DatePicker
              showTime={{ format: 'HH:mm' }}
              format="DD/MM/YYYY HH:mm"
              value={markAt}
              onChange={setMarkAt}
              placeholder="Giờ hẹn"
            />
            <Button
              loading={marking}
              disabled={!markTopicId || !markChannelId || !markAt}
              onClick={() => void markOutside()}
            >
              Đánh dấu đã hẹn ngoài hệ thống
            </Button>
          </Space>
        </Card>
      ) : null}
      {syncNote ? <Alert type="success" showIcon message={syncNote} style={{ marginBottom: 12 }} /> : null}
      {syncError ? <Alert type="warning" showIcon message={syncError} style={{ marginBottom: 12 }} /> : null}
      {brandId && unmatched.length > 0 ? (
        <Card size="small" title="Chưa ghép" style={{ marginBottom: 16 }}>
          <Space direction="vertical" style={{ width: '100%' }} size={8}>
            {unmatched.map((post) => (
              <div key={post.id} style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                <Tag color={platformTone(post.sourceLabel)?.color}>{post.sourceLabel}</Tag>
                <Typography.Text style={{ minWidth: 180 }}>
                  {post.title || post.caption || post.externalId}
                </Typography.Text>
                <Typography.Text type="secondary">
                  {post.publishAt ? dayjs(post.publishAt).format('DD/MM HH:mm') : 'Chưa có giờ'}
                </Typography.Text>
                <Select
                  showSearch
                  placeholder="Chọn bài viết"
                  style={{ minWidth: 240 }}
                  value={picks[post.id]}
                  onChange={(value) => setPicks((prev) => ({ ...prev, [post.id]: value }))}
                  optionFilterProp="label"
                  options={topics.map((t) => ({ value: t.id, label: t.title }))}
                />
                <Button
                  size="small"
                  loading={matchingId === post.id}
                  disabled={!picks[post.id]}
                  onClick={() => void assignTopic(post)}
                >
                  Ghép
                </Button>
              </div>
            ))}
          </Space>
        </Card>
      ) : null}

      {error ? <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} /> : null}

      {loading && !items.length ? (
        <div style={{ textAlign: 'center', padding: 48 }}>
          <Spin tip="Đang tải lịch..." />
        </div>
      ) : null}

      {!lineageIndex && items.length > 0 ? (
        <Alert
          type="info"
          showIcon
          message="Đang tải story context..."
          description="Lineage đang được xây dựng để hiển thị Series/Episode. Calendar sẽ cập nhật khi xong."
          style={{ marginBottom: 16 }}
        />
      ) : null}

      {view === 'month' ? (
        <Calendar
          value={cursor}
          onSelect={(d) => {
            setCursor(d);
            setView('week');
          }}
          onPanelChange={(d) => setCursor(d)}
          cellRender={(date, info) => {
            if (info.type !== 'date') return info.originNode;
            const list = cellItems(date).slice(0, 3);
            if (list.length === 0) return info.originNode;
            return (
              <div>
                {info.originNode}
                <ul style={{ listStyle: 'none', margin: 0, padding: 0, fontSize: 11 }}>
                  {list.map((it) => {
                    const lineage = it.topicId && lineageIndex ? lineageIndex.get(it.topicId) : null;
                    const itemType = lineage
                      ? `${lineage.series.code}-E${lineage.episode.episodeNo.toString().padStart(2, '0')}`
                      : it.topicId && !lineageIndex
                      ? '...'
                      : it.packageId
                      ? 'Brand Angle'
                      : it.topicId
                      ? 'Chưa liên kết'
                      : it.publishJobId
                      ? 'Khác'
                      : 'Độc lập';
                    
                    const linkTo = lineage
                      ? `/content/article-series/${lineage.series.id}/episodes/${lineage.episode.id}`
                      : it.packageId
                      ? `/content/packages?package=${it.packageId}`
                      : it.topicId
                      ? '/content/topics'
                      : undefined;
                    
                    const title = it.title.length > 30 ? `${it.title.substring(0, 30)}...` : it.title;
                    
                    return (
                      <li key={`${it.kind}-${it.topicId}-${it.publishJobId}-${it.at}`} className="content-calendar-month-item">
                        <span className="content-brand-dot" style={{ background: brandColor(it.brandCode) }} />
                        <div className="content-calendar-month-item__content">
                          <div style={{ fontWeight: 500, marginBottom: 1 }}>
                            {it.brandCode} · {it.sourceLabel ?? itemType}
                          </div>
                          {linkTo ? (
                            <Link to={linkTo} style={{ fontSize: 11, color: '#475569' }}>
                              {title}
                            </Link>
                          ) : (
                            <span style={{ fontSize: 11, color: '#64748b' }}>{title}</span>
                          )}
                        </div>
                      </li>
                    );
                  })}
                  {cellItems(date).length > 3 ? (
                    <li style={{ color: '#64748b', paddingLeft: 14 }}>+{cellItems(date).length - 3}</li>
                  ) : null}
                </ul>
              </div>
            );
          }}
        />
      ) : (
        <div className="content-week-grid content-week-grid--page">
          {weekDays.map((d) => {
            const dateKey = d.format('YYYY-MM-DD');
            const brandSchedules = groupedByDay.get(dateKey) ?? [];
            const hasLineage = lineageIndex !== null;
            
            return (
              <Card
                key={dateKey}
                size="small"
                title={d.format('dd DD/MM')}
                extra={
                  d.isSame(dayjs(), 'day') ? (
                    <Tag color="blue" style={{ margin: 0 }}>
                      Hôm nay
                    </Tag>
                  ) : null
                }
              >
                {!hasLineage && cellItems(d).length > 0 ? (
                  <div style={{ marginBottom: 12, padding: 8, background: '#f1f5f9', borderRadius: 4 }}>
                    <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                      Đang tải Series context...
                    </Typography.Text>
                  </div>
                ) : null}
                
                {brandSchedules.length === 0 ? (
                  <Typography.Text type="secondary">—</Typography.Text>
                ) : (
                  <div>
                    {brandSchedules.map((brandSchedule) => (
                      <div 
                        key={brandSchedule.brand.id} 
                        className="content-calendar-brand"
                        style={{ borderLeftColor: brandColor(brandSchedule.brand.code) }}
                      >
                        <div className="content-calendar-brand__header">
                          <span className="content-brand-dot" style={{ background: brandColor(brandSchedule.brand.code) }} />
                          <Typography.Text strong style={{ fontSize: 13 }}>
                            {brandSchedule.brand.name}
                          </Typography.Text>
                        </div>
                        
                        {/* Series Groups */}
                        {brandSchedule.seriesGroups.map((seriesGroup) => (
                          <div key={seriesGroup.series.id} className="content-calendar-series">
                            <div className="content-calendar-series__header">
                              <Link to={`/content/article-series/${seriesGroup.series.id}`} style={{ fontSize: 13, fontWeight: 500 }}>
                                {seriesGroup.series.name}
                              </Link>
                              <Tag style={{ fontSize: 10, lineHeight: '16px', padding: '0 4px', marginLeft: 6 }}>
                                {seriesGroup.series.code}
                              </Tag>
                            </div>
                            
                            {/* Episodes */}
                            {seriesGroup.episodes.map((episodeSchedule) => (
                              <div key={episodeSchedule.episode.id} className="content-calendar-episode">
                                <div className="content-calendar-episode__header">
                                  <Link
                                    to={`/content/article-series/${seriesGroup.series.id}/episodes/${episodeSchedule.episode.id}`}
                                    style={{ fontSize: 12, color: '#475569' }}
                                  >
                                    {episodeSequenceLabel(episodeSchedule.episode.episodeNo)} · {episodeSchedule.episode.title}
                                  </Link>
                                </div>
                                <div className="content-calendar-episode__schedules">
                                  {episodeSchedule.schedules.map((schedule, idx) => (
                                    <div key={`${schedule.topicId}-${schedule.at}-${idx}`}>
                                      {renderScheduleItem(schedule)}
                                    </div>
                                  ))}
                                </div>
                              </div>
                            ))}
                          </div>
                        ))}
                        
                        {/* Secondary Content */}
                        {renderSecondaryGroup('Nội dung độc lập', brandSchedule.secondary.standalone)}
                        {renderSecondaryGroup('Nội dung chưa liên kết', brandSchedule.secondary.unlinked)}
                        {renderSecondaryGroup('Brand Angle', brandSchedule.secondary.package)}
                        {renderSecondaryGroup('Khác', brandSchedule.secondary.other)}
                      </div>
                    ))}
                  </div>
                )}
              </Card>
            );
          })}
        </div>
      )}

      <div className="content-cal-legend">
        {brands.map((b) => (
          <span key={b.id}>
            <span className="content-brand-dot" style={{ background: brandColor(b.code) }} />
            {b.name}
          </span>
        ))}
        <span style={{ color: '#64748b' }}>Nháp · Chờ duyệt · Lịch · Đã đăng · Lỗi</span>
      </div>
    </div>
  );
}
