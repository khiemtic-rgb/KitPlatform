import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Alert, Button, Calendar, Card, List, Segmented, Select, Space, Tag, Typography } from 'antd';
import type { Dayjs } from 'dayjs';
import dayjs from 'dayjs';
import isoWeek from 'dayjs/plugin/isoWeek';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentBrands,
  fetchContentCalendar,
  type ContentBrand,
  type ContentCalendarItem,
} from '@/shared/api/content.api';
import { brandColor, channelLabel } from './content-brand';

dayjs.extend(isoWeek);

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

  const renderItem = (it: ContentCalendarItem) => (
    <div className="content-week-item" style={{ marginBottom: 6 }}>
      <span className="content-brand-dot" style={{ background: brandColor(it.brandCode) }} />
      <span className="content-week-item__text">
        <Tag color={STATUS_COLOR[it.status] ?? 'default'} style={{ marginInlineEnd: 4 }}>
          {it.status}
        </Tag>
        <strong>{it.brandCode}</strong>
        {it.channel ? ` · ${channelLabel(it.channel)}` : ''}
        {' · '}
        {it.packageId ? (
          <Link to="/content/packages">{it.title}</Link>
        ) : it.topicId ? (
          <Link to="/content/topics">{it.title}</Link>
        ) : (
          it.title
        )}
      </span>
    </div>
  );

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
                <ul style={{ listStyle: 'none', margin: 0, padding: 0, fontSize: 12 }}>
                  {list.map((it) => (
                    <li key={`${it.kind}-${it.topicId}-${it.publishJobId}-${it.at}`} className="content-week-item">
                      <span className="content-brand-dot" style={{ background: brandColor(it.brandCode) }} />
                      <span className="content-week-item__text">
                        {it.brandCode}
                        {it.channel ? ` · ${channelLabel(it.channel)}` : ''} · {it.title}
                      </span>
                    </li>
                  ))}
                  {cellItems(date).length > 3 ? (
                    <li style={{ color: '#64748b' }}>+{cellItems(date).length - 3}</li>
                  ) : null}
                </ul>
              </div>
            );
          }}
        />
      ) : (
        <div className="content-week-grid content-week-grid--page">
          {weekDays.map((d) => (
            <Card
              key={d.format('YYYY-MM-DD')}
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
              <List
                size="small"
                loading={loading}
                dataSource={cellItems(d)}
                locale={{ emptyText: '—' }}
                renderItem={(it) => <List.Item style={{ padding: '4px 0', display: 'block' }}>{renderItem(it)}</List.Item>}
              />
            </Card>
          ))}
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
