import { type ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, DatePicker, Dropdown, Empty, Input, Select, Spin, Table, Tag, Typography } from 'antd';
import {
  AppstoreOutlined,
  BarChartOutlined,
  CalendarOutlined,
  DownOutlined,
  FileTextOutlined,
  InboxOutlined,
  PieChartOutlined,
  PlusOutlined,
  RightOutlined,
  SearchOutlined,
  ShopOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchDashboardOverview } from '@/shared/api/dashboard.api';
import { runReport } from '@/shared/api/reports.api';
import type { ReportTableResult } from '@/shared/api/reports.types';
import { formatDisplayDate, formatDisplayDateTime } from '@/shared/utils/date';
import { formatDisplayMoney } from '@/shared/utils/money';
import { buildConicGradient, type CategoryChartSlice } from '@/modules/dashboard/dashboard-category-chart';
import { readReportFieldNumber, readReportFieldString } from '@/modules/dashboard/dashboard-revenue-range';
import { getReportDefinitions, type ReportDefinition } from '@/modules/reports/reports-catalog';
import { readRecentReports } from '@/modules/reports/report-recent';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import './reports-catalog-page.css';

const { RangePicker } = DatePicker;
const SLICE = ['#1677ff', '#13c2c2', '#722ed1', '#fa8c16', '#eb2f96', '#8c8c8c'];
const FAVORITE_CODES = ['SALES-01', 'INV-01', 'SALES-04', 'SALES-08', 'SALES-06', 'PROC-01'];

type GroupKey = 'sales' | 'inventory' | 'procurement' | 'customers' | 'staff' | 'other';
type GroupBy = 'day' | 'week' | 'month';
type Tone = 'revenue' | 'orders' | 'customers' | 'products' | 'staff' | 'stock';

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('month'), dayjs().endOf('day')];
}

function quarterStart() {
  const month = dayjs().month();
  return dayjs().month(Math.floor(month / 3) * 3).startOf('month');
}

function toIsoRange(range: [Dayjs, Dayjs]) {
  return {
    from: range[0].startOf('day').toISOString(),
    to: range[1].add(1, 'day').startOf('day').toISOString(),
  };
}

function priorRange(range: [Dayjs, Dayjs]): [Dayjs, Dayjs] {
  const days = range[1].startOf('day').diff(range[0].startOf('day'), 'day') + 1;
  const prevTo = range[0].subtract(1, 'day').endOf('day');
  return [prevTo.subtract(days - 1, 'day').startOf('day'), prevTo];
}

function sameDayRange(a: [Dayjs, Dayjs], b: [Dayjs, Dayjs]) {
  return a[0].isSame(b[0], 'day') && a[1].isSame(b[1], 'day');
}

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)} tr`;
  return formatDisplayMoney(value);
}

function describeDelta(current: number, previous: number, newLabel: string) {
  if (previous === 0) return current === 0 ? null : { kind: 'new' as const, text: newLabel };
  const pct = Math.round(((current - previous) / previous) * 1000) / 10;
  if (Math.abs(pct) > 80) return { kind: 'new' as const, text: newLabel };
  return { kind: pct < 0 ? ('down' as const) : ('up' as const), text: `${pct > 0 ? '+' : ''}${pct}%` };
}

function reportGroup(item: ReportDefinition): GroupKey {
  if (item.code === 'SALES-08' || item.path.includes('/customers')) return 'customers';
  if (item.path.includes('revenue-by-employee')) return 'staff';
  if (item.category === 'inventory') return 'inventory';
  if (item.category === 'procurement') return 'procurement';
  if (item.category === 'sales') return 'sales';
  return 'other';
}

function groupColor(key: GroupKey) {
  return (
    {
      sales: 'blue',
      inventory: 'cyan',
      procurement: 'gold',
      customers: 'purple',
      staff: 'magenta',
      other: 'default',
    } as const
  )[key];
}

function LineChart({
  points,
}: {
  points: { label: string; net: number }[];
}) {
  const [hover, setHover] = useState<number | null>(null);
  const maxNet = Math.max(...points.map((p) => p.net), 1);
  const coords = points.map((p, i) => {
    const x = points.length === 1 ? 50 : (i / (points.length - 1)) * 100;
    const y = 36 - (p.net / maxNet) * 30;
    return { ...p, x, y };
  });
  const line = coords.map((c) => `${c.x},${c.y}`).join(' ');
  const area = `0,40 ${line} 100,40`;
  const active = hover != null ? coords[hover] : coords[coords.length - 1];

  return (
    <div className="rep-cat__line">
      <div className="rep-cat__line-y">
        <span>{compactMoney(maxNet)}</span>
        <span>{compactMoney(maxNet / 2)}</span>
        <span>0</span>
      </div>
      <div className="rep-cat__line-plot">
        {active ? (
          <div className="rep-cat__tip">
            <div>{active.label}</div>
            <strong>{formatDisplayMoney(active.net)}</strong>
          </div>
        ) : null}
        <svg
          viewBox="0 0 100 40"
          preserveAspectRatio="none"
          onMouseLeave={() => setHover(null)}
          onMouseMove={(e) => {
            const rect = e.currentTarget.getBoundingClientRect();
            const x = ((e.clientX - rect.left) / rect.width) * 100;
            let nearest = 0;
            let best = Infinity;
            coords.forEach((c, i) => {
              const d = Math.abs(c.x - x);
              if (d < best) {
                best = d;
                nearest = i;
              }
            });
            setHover(nearest);
          }}
        >
          <polyline points="0,10 100,10" className="rep-cat__grid" />
          <polyline points="0,25 100,25" className="rep-cat__grid" />
          <polygon points={area} fill="rgba(22,119,255,0.08)" />
          <polyline
            fill="none"
            stroke="#1677ff"
            strokeWidth="1.6"
            points={line}
            vectorEffect="non-scaling-stroke"
          />
          {active ? (
            <circle cx={active.x} cy={active.y} r="1.6" fill="#1677ff" />
          ) : null}
        </svg>
        <div className="rep-cat__line-labels" style={{ gridTemplateColumns: `repeat(${points.length}, minmax(0, 1fr))` }}>
          {points.map((p, i) => (
            <span key={`${p.label}-${i}`}>{i % Math.ceil(points.length / 8) === 0 ? p.label.slice(0, 5) : ''}</span>
          ))}
        </div>
      </div>
    </div>
  );
}

export function ReportsCatalogPage() {
  const { t } = useTranslation('reports', { keyPrefix: 'catalogHub' });
  const navigate = useNavigate();
  const auditSlimNav = useAuditSlimNav();
  const connectEnabled = useTenantPlatformStore((s) => s.loaded && s.isModuleEnabled('novixa_connect'));

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [groupBy, setGroupBy] = useState<GroupBy>('day');
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [period, setPeriod] = useState<ReportTableResult | null>(null);
  const [prior, setPrior] = useState<ReportTableResult | null>(null);
  const [staffRows, setStaffRows] = useState<Record<string, unknown>[]>([]);
  const [staffTotals, setStaffTotals] = useState<Record<string, unknown> | null>(null);
  const [productRows, setProductRows] = useState<Record<string, unknown>[]>([]);
  const [buyerCount, setBuyerCount] = useState(0);
  const [batchCount, setBatchCount] = useState(0);
  const [recentTick, setRecentTick] = useState(0);

  const catalog = useMemo(
    () => getReportDefinitions({ auditSlimNav, connectEnabled, includeHidden: true }),
    [auditSlimNav, connectEnabled],
  );

  const load = useCallback(async () => {
    setLoading(true);
    const current = toIsoRange(range);
    const previous = toIsoRange(priorRange(range));
    try {
      const [periodRes, priorRes, staffRes, productRes, customerRes, overview] = await Promise.all([
        runReport('sales/revenue-by-period', { ...current, groupBy }),
        runReport('sales/revenue-by-period', { ...previous, groupBy }),
        runReport('sales/revenue-by-employee', current),
        runReport('sales/revenue-by-employee-product', current).catch(() => null),
        runReport('sales/revenue-by-customer', current).catch(() => null),
        fetchDashboardOverview().catch(() => null),
      ]);
      setPeriod(periodRes);
      setPrior(priorRes);
      setStaffRows(staffRes.rows);
      setStaffTotals(staffRes.totals ?? null);
      setProductRows(productRes?.rows ?? []);
      setBuyerCount(customerRes?.rows.length ?? 0);
      setBatchCount(overview?.inventory.activeBatchCount ?? 0);
    } finally {
      setLoading(false);
    }
  }, [range, groupBy]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setRecentTick((n) => n + 1);
  }, []);

  const groups = useMemo(
    () =>
      (
        [
          { key: 'sales' as const, icon: <ShopOutlined />, to: '/reports/sales/revenue-by-period' },
          { key: 'inventory' as const, icon: <InboxOutlined />, to: '/reports/inventory/stock-snapshot' },
          { key: 'procurement' as const, icon: <ShoppingCartOutlined />, to: '/reports/procurement/grn-value' },
          { key: 'customers' as const, icon: <UserOutlined />, to: '/reports/customers' },
          { key: 'staff' as const, icon: <TeamOutlined />, to: '/reports/sales/revenue-by-employee' },
          { key: 'other' as const, icon: <FileTextOutlined />, to: '/reports/procurement/payables-snapshot' },
        ] as const
      ).map((group) => ({
        ...group,
        items: catalog.filter((item) => reportGroup(item) === group.key),
      })),
    [catalog],
  );

  const q = query.trim().toLowerCase();
  const filteredGroups = useMemo(
    () =>
      groups.filter(
        (group) =>
          group.items.length > 0 &&
          (!q ||
            t(`groups.${group.key}`).toLowerCase().includes(q) ||
            group.items.some((item) => `${item.name} ${item.code}`.toLowerCase().includes(q))),
      ),
    [groups, q, t],
  );

  const mix = useMemo<CategoryChartSlice[]>(() => {
    const filled = groups.filter((group) => group.items.length > 0);
    const total = filled.reduce((sum, group) => sum + group.items.length, 0);
    if (total === 0) return [];
    return filled.map((group, index) => ({
      label: t(`groups.${group.key}`),
      netAmount: group.items.length,
      sharePercent: Math.round((group.items.length / total) * 1000) / 10,
      color: SLICE[index % SLICE.length],
    }));
  }, [groups, t]);

  const favorites = FAVORITE_CODES.map((code) => catalog.find((item) => item.code === code)).filter(
    (item): item is ReportDefinition => Boolean(item),
  );
  const recent = useMemo(() => readRecentReports(), [recentTick]);
  const filteredRecent = useMemo(
    () =>
      recent.filter(
        (row) =>
          !q ||
          row.name.toLowerCase().includes(q) ||
          t(`groups.${row.group}`).toLowerCase().includes(q),
      ),
    [recent, q, t],
  );

  const net = period?.totals ? readReportFieldNumber(period.totals, 'netAmount') : 0;
  const orders = period?.totals ? readReportFieldNumber(period.totals, 'orderCount') : 0;
  const priorNet = prior?.totals ? readReportFieldNumber(prior.totals, 'netAmount') : 0;
  const priorOrders = prior?.totals ? readReportFieldNumber(prior.totals, 'orderCount') : 0;
  const namedOrders = staffTotals ? readReportFieldNumber(staffTotals, 'namedOrderCount') : 0;
  const soldSkuCount = useMemo(() => {
    const codes = new Set(
      productRows
        .map((row) => readReportFieldString(row, 'productCode') || readReportFieldString(row, 'productName'))
        .filter(Boolean),
    );
    return codes.size;
  }, [productRows]);
  const points = useMemo(
    () =>
      (period?.rows ?? []).map((row) => ({
        label: readReportFieldString(row, 'periodLabel'),
        net: readReportFieldNumber(row, 'netAmount'),
      })),
    [period],
  );

  const presets = [
    { key: 'today', label: t('period.today'), value: [dayjs().startOf('day'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'week', label: t('period.week'), value: [dayjs().startOf('week'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'month', label: t('period.month'), value: defaultRange() },
    { key: 'quarter', label: t('period.quarter'), value: [quarterStart(), dayjs().endOf('day')] as [Dayjs, Dayjs] },
  ];

  const kpis: { key: Tone; label: string; value: string; hint: string; to: string; icon: ReactNode; delta?: ReturnType<typeof describeDelta> }[] = [
    { key: 'revenue', label: t('kpi.revenue'), value: formatDisplayMoney(net), hint: t('kpi.vsPrior'), to: '/reports/sales/revenue-by-period', icon: <BarChartOutlined />, delta: describeDelta(net, priorNet, t('kpi.newPeriod')) },
    { key: 'orders', label: t('kpi.orders'), value: orders.toLocaleString('vi-VN'), hint: t('kpi.vsPrior'), to: '/reports/sales/revenue-by-period', icon: <ShoppingCartOutlined />, delta: describeDelta(orders, priorOrders, t('kpi.newPeriod')) },
    { key: 'customers', label: t('kpi.customers'), value: buyerCount.toLocaleString('vi-VN'), hint: t('kpi.namedHint', { count: namedOrders.toLocaleString('vi-VN') }), to: '/reports/customers', icon: <UserOutlined /> },
    { key: 'products', label: t('kpi.products'), value: soldSkuCount.toLocaleString('vi-VN'), hint: t('kpi.soldHint'), to: '/reports/inventory/stock-snapshot', icon: <AppstoreOutlined /> },
    { key: 'staff', label: t('kpi.staff'), value: String(staffRows.length), hint: t('kpi.staffHint'), to: '/reports/sales/revenue-by-employee', icon: <TeamOutlined /> },
    { key: 'stock', label: t('kpi.stock'), value: batchCount.toLocaleString('vi-VN'), hint: t('kpi.batches'), to: '/reports/inventory/stock-snapshot', icon: <InboxOutlined /> },
  ];

  const createItems = groups
    .filter((group) => group.items.length > 0)
    .flatMap((group) => [
      { type: 'group' as const, key: `g-${group.key}`, label: t(`groups.${group.key}`) },
      ...group.items.map((item) => ({ key: item.path, label: item.name })),
    ]);

  const withRange = (path: string) => {
    const qs = new URLSearchParams({ from: range[0].toISOString(), to: range[1].endOf('day').toISOString() });
    return `${path}?${qs.toString()}`;
  };
  const openReport = (path: string) => {
    navigate(withRange(path));
  };

  return (
    <div className="rep-cat">
      <div className="rep-cat__head">
        <div>
          <Typography.Title level={3} className="rep-cat__title">
            {t('title')}
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="rep-cat__desc">
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        <div className="rep-cat__tools">
          <div className="rep-cat__presets">
            {presets.map((preset) => (
              <button
                key={preset.key}
                type="button"
                className={`rep-cat__preset${sameDayRange(range, preset.value) ? ' is-active' : ''}`}
                onClick={() => setRange(preset.value)}
              >
                {preset.label}
              </button>
            ))}
          </div>
          <RangePicker
            value={range}
            allowClear={false}
            format="DD/MM/YYYY"
            onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
          />
          <Dropdown
            trigger={['click']}
            menu={{
              items: createItems,
              onClick: ({ key }) => openReport(key),
            }}
          >
            <Button type="primary" icon={<PlusOutlined />}>
              {t('create')} <DownOutlined />
            </Button>
          </Dropdown>
        </div>
      </div>

      <Spin spinning={loading}>
        <div className="rep-cat__kpis">
          {kpis.map((kpi) => (
            <Link key={kpi.key} to={withRange(kpi.to)} className={`rep-cat__kpi rep-cat__kpi--${kpi.key}`}>
              <span className="rep-cat__kpi-icon">{kpi.icon}</span>
              <span>
                <span className="rep-cat__kpi-label">{kpi.label}</span>
                <span className="rep-cat__kpi-value">{kpi.value}</span>
                <span className="rep-cat__kpi-meta">
                  {kpi.delta && kpi.delta.kind !== 'new' ? (
                    <span className={`rep-cat__delta rep-cat__delta--${kpi.delta.kind}`}>{kpi.delta.text}</span>
                  ) : kpi.delta ? (
                    <span className="rep-cat__delta rep-cat__delta--new">{kpi.delta.text}</span>
                  ) : null}
                  <span className="rep-cat__hint">{kpi.hint}</span>
                </span>
              </span>
            </Link>
          ))}
        </div>

        <div className="rep-cat__charts">
          <section className="rep-cat__panel">
            <div className="rep-cat__panel-head">
              <span className="rep-cat__panel-icon">
                <BarChartOutlined />
              </span>
              <h3>{t('charts.time')}</h3>
              <Select
                size="small"
                value={groupBy}
                onChange={setGroupBy}
                style={{ marginLeft: 'auto', width: 128 }}
                options={[
                  { value: 'day', label: t('charts.byDay') },
                  { value: 'week', label: t('charts.byWeek') },
                  { value: 'month', label: t('charts.byMonth') },
                ]}
              />
            </div>
            {points.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <LineChart points={points} />
            )}
          </section>
          <section className="rep-cat__panel">
            <div className="rep-cat__panel-head">
              <span className="rep-cat__panel-icon">
                <PieChartOutlined />
              </span>
              <h3>{t('charts.mix')}</h3>
            </div>
            {mix.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="rep-cat__donut-wrap">
                <div className="rep-cat__donut" style={{ background: buildConicGradient(mix) }}>
                  <div className="rep-cat__donut-hole">
                    <strong>{catalog.length}</strong>
                    <span>{t('charts.types')}</span>
                  </div>
                </div>
                <ul>
                  {mix.map((slice) => (
                    <li key={slice.label}>
                      <span>
                        <i style={{ background: slice.color }} />
                        {slice.label}
                      </span>
                      <b>{slice.sharePercent}%</b>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
          <section className="rep-cat__panel">
            <div className="rep-cat__panel-head">
              <h3>{t('charts.used')}</h3>
            </div>
            <ol className="rep-cat__used">
              {favorites.map((item, index) => (
                <li key={item.code}>
                  <span className="rep-cat__used-rank">{index + 1}</span>
                  <Link to={withRange(item.path)}>{item.name}</Link>
                </li>
              ))}
            </ol>
          </section>
        </div>

        <section className="rep-cat__panel">
          <div className="rep-cat__panel-head">
            <div>
              <h3>{t('pick.title')}</h3>
              <p className="rep-cat__muted">{t('pick.subtitle')}</p>
            </div>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={t('pick.search')}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              style={{ width: 240, marginLeft: 'auto' }}
            />
          </div>
          <div className="rep-cat__cards">
            {filteredGroups.map((group) => (
              <Link key={group.key} to={withRange(group.to)} className={`rep-cat__card rep-cat__card--${group.key}`}>
                <span className="rep-cat__card-icon">{group.icon}</span>
                <span>
                  <strong>{t(`groups.${group.key}`)}</strong>
                  <span className="rep-cat__muted">{t(`groups.${group.key}Hint`)}</span>
                </span>
                <span className="rep-cat__card-count">
                  {t('pick.count', { count: group.items.length })}
                  <RightOutlined />
                </span>
              </Link>
            ))}
          </div>
        </section>

        <section className="rep-cat__panel">
          <div className="rep-cat__panel-head">
            <span className="rep-cat__panel-icon">
              <CalendarOutlined />
            </span>
            <h3>{t('recent.title')}</h3>
          </div>
          <Table
            rowKey="path"
            size="middle"
            dataSource={filteredRecent}
            pagination={false}
            locale={{ emptyText: t('recent.empty') }}
            columns={[
              { title: t('recent.name'), dataIndex: 'name' },
              {
                title: t('recent.group'),
                dataIndex: 'group',
                render: (group: string) => <Tag color={groupColor(group as GroupKey)}>{t(`groups.${group}`)}</Tag>,
              },
              {
                title: t('recent.period'),
                render: (_: unknown, row: (typeof filteredRecent)[number]) =>
                  row.from && row.to ? `${formatDisplayDate(row.from)} – ${formatDisplayDate(row.to)}` : '—',
              },
              { title: t('recent.by'), dataIndex: 'openedBy' },
              {
                title: t('recent.at'),
                dataIndex: 'openedAt',
                render: (value: string) => formatDisplayDateTime(value),
              },
              {
                title: t('recent.action'),
                align: 'right',
                render: (_: unknown, row: (typeof filteredRecent)[number]) => (
                  <Link to={row.from && row.to ? `${row.path}?from=${encodeURIComponent(row.from)}&to=${encodeURIComponent(row.to)}` : row.path}>
                    {t('recent.open')}
                  </Link>
                ),
              },
            ]}
          />
        </section>
      </Spin>
    </div>
  );
}
