import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Alert, Button, Calendar, Card, Segmented, Select, Space, Spin, Tag, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentBrands,
  fetchContentCalendar,
  type ContentArticleEpisode,
  type ContentArticleSeries,
  type ContentBrand,
  type ContentCalendarItem,
} from '@/shared/api/content.api';
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

export function ContentCalendarPage() {
  const [params] = useSearchParams();
  const lineageIndex = useTopicLineageIndex();
  const [view, setView] = useState<'month' | 'week'>(() =>
    params.get('view') === 'month' ? 'month' : 'week',
  );
  const [cursor, setCursor] = useState(() => {
    const day = params.get('day');
    return day && dayjs(day).isValid() ? dayjs(day) : dayjs();
  });
  const [brandId, setBrandId] = useState<string | undefined>();
  const [channel, setChannel] = useState<string | undefined>();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [items, setItems] = useState<ContentCalendarItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
    
    return (
      <div className="content-calendar-schedule">
        <span style={{ color: '#64748b' }}>
          {item.channel ? channelLabel(item.channel) : 'Kênh không xác định'}
        </span>
        <span style={{ color: '#64748b' }}>·</span>
        <span style={{ color: '#64748b', fontVariantNumeric: 'tabular-nums' }}>{time}</span>
        <span style={{ color: '#64748b' }}>·</span>
        <Tag color={STATUS_COLOR[item.status] ?? 'default'} style={{ margin: 0, fontSize: 11 }}>
          {item.status}
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
          <Typography.Title level={4} style={{ margin: 0 }}>
            Lịch nội dung
          </Typography.Title>
          <Typography.Text type="secondary">
            Mặc định xem tuần — tháng chỉ khi cần nhìn cả kỳ. Màu = brand, không phải trạng thái.
          </Typography.Text>
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
            placeholder="Mọi brand"
            style={{ minWidth: 180 }}
            value={brandId}
            onChange={(v) => setBrandId(v)}
            options={brands.map((b) => ({ value: b.id, label: b.name }))}
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
          <Link to="/content/ops">
            <Button>Hôm nay (hàng đợi)</Button>
          </Link>
        </Space>
      </div>

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
                            {it.brandCode} · {itemType}
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
