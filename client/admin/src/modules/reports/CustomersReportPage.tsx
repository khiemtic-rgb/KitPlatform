import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, DatePicker, Empty, Input, Select, Spin, Table, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  CalendarOutlined,
  DownloadOutlined,
  FilterOutlined,
  FundOutlined,
  PieChartOutlined,
  RiseOutlined,
  SearchOutlined,
  ShoppingCartOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchDashboardOverview } from '@/shared/api/dashboard.api';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportTableResult } from '@/shared/api/reports.types';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatDisplayDateTime } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';
import { exportReportCsv } from '@/modules/reports/report-export';
import { buildConicGradient } from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  readReportFieldString,
} from '@/modules/dashboard/dashboard-revenue-range';
import './customers-report.css';

const { RangePicker } = DatePicker;

type Tone = 'revenue' | 'orders' | 'buyers' | 'first' | 'attach' | 'book';
type CustomerRow = {
  key: string;
  customerId: string;
  customerCode: string;
  customerName: string;
  orderCount: number;
  netAmount: number;
  lastOrderAt: string;
  isReturning: boolean;
  sharePercent: number;
};

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('month'), dayjs().endOf('day')];
}

function toIsoRange(range: [Dayjs, Dayjs]) {
  return {
    from: range[0].startOf('day').toISOString(),
    to: range[1].add(1, 'day').startOf('day').toISOString(),
  };
}

function sameDayRange(a: [Dayjs, Dayjs], b: [Dayjs, Dayjs]) {
  return a[0].isSame(b[0], 'day') && a[1].isSame(b[1], 'day');
}

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)} tr`;
  return formatDisplayMoney(value);
}

function isReturningRow(row: Record<string, unknown>) {
  return row.isReturning === true || row.IsReturning === true;
}

export function CustomersReportPage() {
  const { t } = useTranslation('reports', { keyPrefix: 'customerHub' });
  const canExport = useCanReportsExport();

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ReportTableResult | null>(null);
  const [bookCount, setBookCount] = useState(0);

  useEffect(() => {
    void fetchWarehouses()
      .then(setWarehouses)
      .catch(() => undefined);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    const current = toIsoRange(range);
    const extra = warehouseId ? { warehouseId } : {};
    try {
      const [report, overview] = await Promise.all([
        runReport('sales/revenue-by-customer', { ...current, ...extra }),
        fetchDashboardOverview().catch(() => null),
      ]);
      setResult(report);
      setBookCount(overview?.catalog.customerCount ?? 0);
    } finally {
      setLoading(false);
    }
  }, [range, warehouseId]);

  useEffect(() => {
    void load();
  }, [load]);

  const rows = useMemo<CustomerRow[]>(
    () =>
      (result?.rows ?? []).map((row, index) => ({
        key: readReportFieldString(row, 'customerId') || `row-${index}`,
        customerId: readReportFieldString(row, 'customerId'),
        customerCode: readReportFieldString(row, 'customerCode'),
        customerName: readReportFieldString(row, 'customerName'),
        orderCount: readReportFieldNumber(row, 'orderCount'),
        netAmount: readReportFieldNumber(row, 'netAmount'),
        lastOrderAt: String(row.lastOrderAt ?? row.LastOrderAt ?? ''),
        isReturning: isReturningRow(row),
        sharePercent: readReportFieldNumber(row, 'sharePercent'),
      })),
    [result],
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return rows;
    return rows.filter(
      (row) =>
        row.customerCode.toLowerCase().includes(q) || row.customerName.toLowerCase().includes(q),
    );
  }, [rows, query]);

  const net = result?.totals ? readReportFieldNumber(result.totals, 'netAmount') : 0;
  const namedOrders = result?.totals ? readReportFieldNumber(result.totals, 'orderCount') : 0;
  const walkIn = result?.totals ? readReportFieldNumber(result.totals, 'walkInOrderCount') : 0;
  const allOrders = result?.totals ? readReportFieldNumber(result.totals, 'allOrderCount') : namedOrders + walkIn;
  const firstCount = rows.filter((row) => !row.isReturning).length;
  const attach = allOrders > 0 ? Math.round((namedOrders / allOrders) * 1000) / 10 : 0;
  const aov = namedOrders > 0 ? Math.round(net / namedOrders) : 0;
  const weekend = result?.totals ? readReportFieldNumber(result.totals, 'weekendOrderCount') : 0;
  const weekendShare = allOrders > 0 ? Math.round((weekend / allOrders) * 1000) / 10 : 0;
  const peakHour = result?.totals ? readReportFieldNumber(result.totals, 'peakHour') : 0;
  const peakHourOrders = result?.totals ? readReportFieldNumber(result.totals, 'peakHourOrders') : 0;

  const firstOrders = rows.filter((r) => !r.isReturning).reduce((s, r) => s + r.orderCount, 0);
  const returningOrders = rows.filter((r) => r.isReturning).reduce((s, r) => s + r.orderCount, 0);
  const mixTotal = firstOrders + returningOrders + walkIn;
  const mix = [
    { label: t('mix.first'), value: firstOrders, color: '#1677ff' },
    { label: t('mix.returning'), value: returningOrders, color: '#13c2c2' },
    { label: t('mix.walkIn'), value: walkIn, color: '#d9d9d9' },
  ]
    .filter((item) => item.value > 0)
    .map((item) => ({
      ...item,
      sharePercent: mixTotal > 0 ? Math.round((item.value / mixTotal) * 1000) / 10 : 0,
    }));

  const newByDay = useMemo(() => {
    const map = new Map<string, number>();
    for (const raw of result?.rows ?? []) {
      if (isReturningRow(raw)) continue;
      const stamp = String(raw.firstInPeriodAt ?? raw.FirstInPeriodAt ?? '');
      const day = dayjs(stamp);
      if (!day.isValid()) continue;
      const key = day.format('DD/MM');
      map.set(key, (map.get(key) ?? 0) + 1);
    }
    return [...map.entries()].map(([label, count]) => ({ label, count }));
  }, [result]);
  const maxNew = Math.max(...newByDay.map((p) => p.count), 1);

  const repeatCount = rows.filter((row) => row.orderCount >= 2).length;
  const valueCut = [...rows].sort((a, b) => b.netAmount - a.netAmount).slice(0, Math.max(1, Math.ceil(rows.length * 0.2)));
  const funnel = [
    { key: 'book', label: t('charts.funnelBook'), value: bookCount },
    { key: 'buy', label: t('charts.funnelBuy'), value: rows.length },
    { key: 'repeat', label: t('charts.funnelRepeat'), value: repeatCount },
    { key: 'value', label: t('charts.funnelValue'), value: rows.length ? valueCut.length : 0 },
  ];
  const funnelMax = Math.max(...funnel.map((item) => item.value), 1);

  const presets = [
    { key: 'today', label: t('periodToday'), value: [dayjs().startOf('day'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'week', label: t('periodWeek'), value: [dayjs().startOf('week'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'month', label: t('periodMonth'), value: defaultRange() },
  ];

  const columns: ColumnsType<CustomerRow> = [
    {
      title: t('table.rank'),
      width: 48,
      render: (_v, _r, index) => index + 1,
    },
    { title: t('table.code'), dataIndex: 'customerCode' },
    {
      title: t('table.name'),
      dataIndex: 'customerName',
      render: (name: string, row) =>
        row.customerId ? <Link to={`/customer/${row.customerId}`}>{name}</Link> : name,
    },
    { title: t('table.orders'), dataIndex: 'orderCount', align: 'right', render: (n: number) => n.toLocaleString('vi-VN') },
    { title: t('table.revenue'), dataIndex: 'netAmount', align: 'right', render: (n: number) => formatDisplayMoney(n) },
    { title: t('table.last'), dataIndex: 'lastOrderAt', render: (v: string) => formatDisplayDateTime(v) },
    {
      title: t('table.segment'),
      render: (_v, row) => (row.isReturning ? t('mix.returning') : t('mix.first')),
    },
    {
      title: t('table.status'),
      render: () => <span className="cust-rep__ok">{t('table.bought')}</span>,
    },
  ];

  const kpis = [
    { key: 'revenue' as Tone, label: t('kpi.revenue'), value: formatDisplayMoney(net), icon: <FundOutlined /> },
    { key: 'orders' as Tone, label: t('kpi.orders'), value: namedOrders.toLocaleString('vi-VN'), icon: <ShoppingCartOutlined /> },
    { key: 'buyers' as Tone, label: t('kpi.buyers'), value: rows.length.toLocaleString('vi-VN'), icon: <UserOutlined /> },
    { key: 'first' as Tone, label: t('kpi.first'), value: firstCount.toLocaleString('vi-VN'), icon: <UserAddOutlined /> },
    { key: 'attach' as Tone, label: t('kpi.attach'), value: `${attach}%`, icon: <FilterOutlined /> },
    { key: 'book' as Tone, label: t('kpi.book'), value: bookCount.toLocaleString('vi-VN'), hint: t('bookHint'), icon: <UserOutlined /> },
  ];

  return (
    <div className="cust-rep">
      <div className="cust-rep__head">
        <div>
          <Typography.Title level={3} className="cust-rep__title">
            {t('title')}
            <span className="cust-rep__code">SALES-08</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="cust-rep__desc">
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        {canExport && result ? (
          <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(result)}>
            {t('export')}
          </Button>
        ) : null}
      </div>

      <div className="cust-rep__toolbar">
        <div className="cust-rep__presets">
          {presets.map((preset) => (
            <button
              key={preset.key}
              type="button"
              className={`cust-rep__preset${sameDayRange(range, preset.value) ? ' is-active' : ''}`}
              onClick={() => setRange(preset.value)}
            >
              {preset.label}
            </button>
          ))}
        </div>
        <div className="cust-rep__period">
          <CalendarOutlined />
          <RangePicker
            value={range}
            allowClear={false}
            variant="borderless"
            format="DD/MM/YYYY"
            onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
          />
        </div>
        <Select
          allowClear
          style={{ minWidth: 180 }}
          placeholder={t('warehouseAll')}
          value={warehouseId}
          onChange={setWarehouseId}
          options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
        />
      </div>

      <Spin spinning={loading}>
        <div className="cust-rep__kpis">
          {kpis.map((kpi) => (
            <div key={kpi.key} className={`cust-rep__kpi cust-rep__kpi--${kpi.key}`}>
              <span className="cust-rep__kpi-icon">{kpi.icon}</span>
              <span>
                <span className="cust-rep__kpi-label">{kpi.label}</span>
                <span className="cust-rep__kpi-value">{kpi.value}</span>
                <span className="cust-rep__kpi-hint">{kpi.hint ?? t('asOf')}</span>
              </span>
            </div>
          ))}
        </div>

        <div className="cust-rep__charts">
          <section className="cust-rep__panel">
            <div className="cust-rep__panel-head">
              <span className="cust-rep__panel-icon">
                <PieChartOutlined />
              </span>
              <h3>{t('charts.mix')}</h3>
              <span className="cust-rep__muted">{t('charts.mixHint')}</span>
            </div>
            {mix.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="cust-rep__donut-wrap">
                <div
                  className="cust-rep__donut"
                  style={{
                    background: buildConicGradient(
                      mix.map((item) => ({
                        label: item.label,
                        netAmount: item.value,
                        sharePercent: item.sharePercent,
                        color: item.color,
                      })),
                    ),
                  }}
                >
                  <div className="cust-rep__donut-hole">
                    <strong>{allOrders.toLocaleString('vi-VN')}</strong>
                    <span>đơn</span>
                  </div>
                </div>
                <ul>
                  {mix.map((item) => (
                    <li key={item.label}>
                      <span>
                        <i style={{ background: item.color }} />
                        {item.label}
                      </span>
                      <b>
                        {item.sharePercent}% · {item.value.toLocaleString('vi-VN')}
                      </b>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
          <section className="cust-rep__panel">
            <div className="cust-rep__panel-head">
              <span className="cust-rep__panel-icon">
                <CalendarOutlined />
              </span>
              <h3>{t('charts.new')}</h3>
            </div>
            {newByDay.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="cust-rep__combo">
                <div
                  className="cust-rep__combo-bars"
                  style={{ gridTemplateColumns: `repeat(${newByDay.length}, minmax(8px, 1fr))` }}
                >
                  {newByDay.map((p) => (
                    <div
                      key={p.label}
                      className="cust-rep__combo-bar"
                      style={{ height: `${Math.max(6, (p.count / maxNew) * 100)}%` }}
                      title={`${p.label}: ${p.count}`}
                    />
                  ))}
                </div>
                <div
                  className="cust-rep__combo-labels"
                  style={{ gridTemplateColumns: `repeat(${newByDay.length}, minmax(8px, 1fr))` }}
                >
                  {newByDay.map((p, i) => (
                    <span key={p.label}>{i % Math.ceil(newByDay.length / 8) === 0 ? p.label : ''}</span>
                  ))}
                </div>
              </div>
            )}
          </section>
          <section className="cust-rep__panel">
            <div className="cust-rep__panel-head">
              <span className="cust-rep__panel-icon">
                <RiseOutlined />
              </span>
              <h3>{t('charts.funnel')}</h3>
            </div>
            {funnel.map((item) => (
              <div key={item.key} className="cust-rep__funnel">
                <span>{item.label}</span>
                <span className="cust-rep__funnel-track">
                  <span style={{ width: `${Math.max(8, (item.value / funnelMax) * 100)}%` }} />
                </span>
                <b>{item.value.toLocaleString('vi-VN')}</b>
              </div>
            ))}
          </section>
        </div>

        <div className="cust-rep__body">
          <section className="cust-rep__panel">
            <div className="cust-rep__panel-head">
              <span className="cust-rep__panel-icon">
                <UserOutlined />
              </span>
              <h3>{t('table.title')}</h3>
              <span className="cust-rep__muted">{t('table.count', { count: filtered.length })}</span>
              <Input
                allowClear
                prefix={<SearchOutlined />}
                placeholder={t('table.search')}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                style={{ width: 220, marginLeft: 'auto' }}
              />
            </div>
            <Table<CustomerRow>
              rowKey="key"
              size="middle"
              columns={columns}
              dataSource={filtered}
              pagination={{ pageSize: 10, showSizeChanger: false }}
              locale={{ emptyText: t('empty') }}
              scroll={{ x: true }}
            />
          </section>
          <aside className="cust-rep__side">
            <section className="cust-rep__panel">
              <div className="cust-rep__panel-head">
                <h3>{t('top.title')}</h3>
                <span className="cust-rep__muted">{t('top.limit')}</span>
              </div>
              {rows.length === 0 ? (
                <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
              ) : (
                rows.slice(0, 10).map((row) => (
                  <div key={row.key} className="cust-rep__top">
                    <span className="cust-rep__top-name" title={row.customerName}>
                      {row.customerId ? (
                        <Link to={`/customer/${row.customerId}`}>{row.customerName}</Link>
                      ) : (
                        row.customerName
                      )}
                    </span>
                    <span className="cust-rep__top-track">
                      <span style={{ width: `${Math.max(6, (row.netAmount / Math.max(rows[0]?.netAmount, 1)) * 100)}%` }} />
                    </span>
                    <b>{compactMoney(row.netAmount)}</b>
                  </div>
                ))
              )}
            </section>
            <section className="cust-rep__panel">
              <div className="cust-rep__panel-head">
                <h3>{t('insight.title')}</h3>
              </div>
              <div className="cust-rep__insight">
                <span>{t('insight.peak')}</span>
                <b>
                  {peakHour || peakHour === 0
                    ? `${String(peakHour).padStart(2, '0')}:00 · ${peakHourOrders.toLocaleString('vi-VN')}`
                    : '—'}
                </b>
              </div>
              <div className="cust-rep__insight">
                <span>{t('insight.weekend')}</span>
                <b>{weekendShare}%</b>
              </div>
              <div className="cust-rep__insight">
                <span>{t('insight.aov')}</span>
                <b>{formatDisplayMoney(aov)}</b>
              </div>
              <div className="cust-rep__insight">
                <span>{t('insight.attach')}</span>
                <b>{attach}%</b>
              </div>
            </section>
          </aside>
        </div>
      </Spin>
    </div>
  );
}
