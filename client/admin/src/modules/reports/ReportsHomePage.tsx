import { type ReactNode, useCallback, useEffect, useMemo, useState } from 'react';
import { Link, Navigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Button, DatePicker, Empty, Spin, Typography } from 'antd';
import {
  AppstoreOutlined,
  ArrowRightOutlined,
  BarChartOutlined,
  CalendarOutlined,
  DownloadOutlined,
  FallOutlined,
  InboxOutlined,
  PieChartOutlined,
  RiseOutlined,
  ShoppingCartOutlined,
  ShopOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  UserAddOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchDashboardOverview } from '@/shared/api/dashboard.api';
import { runReport } from '@/shared/api/reports.api';
import type { ReportTableResult } from '@/shared/api/reports.types';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatDisplayMoney } from '@/shared/utils/money';
import { exportReportCsv } from '@/modules/reports/report-export';
import {
  buildCategoryChartSlices,
  buildConicGradient,
} from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  readReportFieldString,
} from '@/modules/dashboard/dashboard-revenue-range';
import './reports-hub.css';

const { RangePicker } = DatePicker;

type HubPoint = { label: string; netAmount: number; orderCount: number };
type Tone = 'revenue' | 'orders' | 'customers' | 'products' | 'staff' | 'stock';

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('month'), dayjs().endOf('day')];
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
  const prevFrom = prevTo.subtract(days - 1, 'day').startOf('day');
  return [prevFrom, prevTo];
}

function sameDayRange(a: [Dayjs, Dayjs], b: [Dayjs, Dayjs]) {
  return a[0].isSame(b[0], 'day') && a[1].isSame(b[1], 'day');
}

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)} tr`;
  return formatDisplayMoney(value);
}

function pointsFromPeriodRows(rows: Record<string, unknown>[]): HubPoint[] {
  return rows.map((row) => {
    const label = readReportFieldString(row, 'periodLabel');
    const short = label.length >= 5 ? label.slice(0, 5) : label;
    return {
      label: short,
      netAmount: readReportFieldNumber(row, 'netAmount'),
      orderCount: readReportFieldNumber(row, 'orderCount'),
    };
  });
}

function topProductsFromEmployeeRows(rows: Record<string, unknown>[], limit = 10) {
  const map = new Map<string, { name: string; qty: number; net: number }>();
  for (const row of rows) {
    const code = readReportFieldString(row, 'productCode') || readReportFieldString(row, 'productName');
    if (!code) continue;
    const current = map.get(code) ?? {
      name: readReportFieldString(row, 'productName') || code,
      qty: 0,
      net: 0,
    };
    current.qty += readReportFieldNumber(row, 'netQty') || readReportFieldNumber(row, 'qty');
    current.net += readReportFieldNumber(row, 'netAmount');
    map.set(code, current);
  }
  return [...map.entries()]
    .map(([code, value]) => ({ code, ...value }))
    .sort((a, b) => b.net - a.net)
    .slice(0, limit);
}

function initials(name: string) {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 1).toUpperCase();
  return `${parts[parts.length - 2][0] ?? ''}${parts[parts.length - 1][0] ?? ''}`.toUpperCase();
}

function PanelHead({
  icon,
  title,
  to,
  more,
}: {
  icon: ReactNode;
  title: string;
  to?: string;
  more?: string;
}) {
  return (
    <div className="reports-hub__panel-head">
      <span className="reports-hub__panel-icon">{icon}</span>
      <h3>{title}</h3>
      {to && more ? (
        <Link to={to} className="reports-hub__more">
          {more}
        </Link>
      ) : null}
    </div>
  );
}

export function ReportsHomePage() {
  const { t } = useTranslation('reports', { keyPrefix: 'hub' });
  const { t: th } = useTranslation('reports', { keyPrefix: 'home' });
  const canExport = useCanReportsExport();
  const [searchParams] = useSearchParams();
  const view = searchParams.get('view');

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [loading, setLoading] = useState(false);
  const [period, setPeriod] = useState<ReportTableResult | null>(null);
  const [prior, setPrior] = useState<ReportTableResult | null>(null);
  const [categoryRows, setCategoryRows] = useState<Record<string, unknown>[]>([]);
  const [staffRows, setStaffRows] = useState<Record<string, unknown>[]>([]);
  const [staffTotals, setStaffTotals] = useState<Record<string, unknown> | null>(null);
  const [productRows, setProductRows] = useState<Record<string, unknown>[]>([]);
  const [customerCount, setCustomerCount] = useState(0);
  const [buyerCount, setBuyerCount] = useState(0);
  const [batchCount, setBatchCount] = useState(0);

  const load = useCallback(async () => {
    setLoading(true);
    const current = toIsoRange(range);
    const previous = toIsoRange(priorRange(range));
    try {
      const [periodRes, priorRes, categoryRes, staffRes, productRes, customerRes, overview] = await Promise.all([
        runReport('sales/revenue-by-period', { ...current, groupBy: 'day' }),
        runReport('sales/revenue-by-period', { ...previous, groupBy: 'day' }),
        runReport('sales/revenue-by-category', current),
        runReport('sales/revenue-by-employee', current),
        runReport('sales/revenue-by-employee-product', current).catch(() => null),
        runReport('sales/revenue-by-customer', current).catch(() => null),
        fetchDashboardOverview().catch(() => null),
      ]);
      setPeriod(periodRes);
      setPrior(priorRes);
      setCategoryRows(categoryRes.rows);
      setStaffRows(staffRes.rows);
      setStaffTotals(staffRes.totals ?? null);
      setProductRows(productRes?.rows ?? []);
      setBuyerCount(customerRes?.rows.length ?? 0);
      setCustomerCount(overview?.catalog.customerCount ?? 0);
      setBatchCount(overview?.inventory.activeBatchCount ?? 0);
    } finally {
      setLoading(false);
    }
  }, [range]);

  useEffect(() => {
    void load();
  }, [load]);

  const points = useMemo(() => pointsFromPeriodRows(period?.rows ?? []), [period]);
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
  const slices = useMemo(
    () => buildCategoryChartSlices(categoryRows, t('other'), 7),
    [categoryRows, t],
  );
  const products = useMemo(() => topProductsFromEmployeeRows(productRows), [productRows]);
  const staff = useMemo(
    () =>
      [...staffRows]
        .map((row) => ({
          id: readReportFieldString(row, 'employeeId'),
          name: readReportFieldString(row, 'employeeName'),
          net: readReportFieldNumber(row, 'netAmount'),
          share: readReportFieldNumber(row, 'sharePercent'),
        }))
        .sort((a, b) => b.net - a.net)
        .slice(0, 6),
    [staffRows],
  );
  const maxStaffNet = Math.max(...staff.map((row) => row.net), 1);
  const presets = [
    { key: 'today', label: t('period.today'), value: [dayjs().startOf('day'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'week', label: t('period.week'), value: [dayjs().startOf('week'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'month', label: t('period.month'), value: [dayjs().startOf('month'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
  ];

  const kpis: {
    key: Tone;
    label: string;
    value: string;
    delta?: { kind: 'up' | 'down' | 'new'; text: string } | null;
    hint: string;
    to: string;
    icon: ReactNode;
  }[] = [
    {
      key: 'revenue',
      label: t('kpi.revenue'),
      value: formatDisplayMoney(net),
      delta: describeDelta(net, priorNet, t),
      hint: t('kpi.vsPrior'),
      to: '/reports/sales/revenue-by-period',
      icon: <ShopOutlined />,
    },
    {
      key: 'orders',
      label: t('kpi.orders'),
      value: orders.toLocaleString('vi-VN'),
      delta: describeDelta(orders, priorOrders, t),
      hint: t('kpi.vsPrior'),
      to: '/reports/sales/revenue-by-period',
      icon: <ShoppingCartOutlined />,
    },
    {
      key: 'customers',
      label: t('kpi.customers'),
      value: buyerCount.toLocaleString('vi-VN'),
      hint: t('kpi.customersHint', { count: namedOrders.toLocaleString('vi-VN') }),
      to: '/reports/customers',
      icon: <UserOutlined />,
    },
    {
      key: 'products',
      label: t('kpi.products'),
      value: soldSkuCount.toLocaleString('vi-VN'),
      hint: t('kpi.soldHint'),
      to: '/reports/inventory/stock-snapshot',
      icon: <AppstoreOutlined />,
    },
    {
      key: 'staff',
      label: t('kpi.staff'),
      value: String(staffRows.length),
      hint: t('kpi.staffHint'),
      to: '/reports/sales/revenue-by-employee',
      icon: <TeamOutlined />,
    },
    {
      key: 'stock',
      label: t('kpi.stock'),
      value: batchCount.toLocaleString('vi-VN'),
      hint: t('kpi.batches'),
      to: '/reports/inventory/stock-snapshot',
      icon: <InboxOutlined />,
    },
  ];

  return (
    <div className="reports-hub">
      <div className="reports-hub__head">
        <div className="reports-hub__brand">
          <span className="reports-hub__brand-icon">
            <BarChartOutlined />
          </span>
          <div>
            <Typography.Title level={3} className="reports-hub__title">
              {th('title')}
            </Typography.Title>
            <Typography.Paragraph type="secondary" className="reports-hub__desc">
              {t('subtitle')}
            </Typography.Paragraph>
          </div>
        </div>
        <div className="reports-hub__tools">
          <div className="reports-hub__presets">
            {presets.map((preset) => (
              <button
                key={preset.key}
                type="button"
                className={`reports-hub__preset${sameDayRange(range, preset.value) ? ' is-active' : ''}`}
                onClick={() => setRange(preset.value)}
              >
                {preset.label}
              </button>
            ))}
          </div>
          <div className="reports-hub__period">
            <CalendarOutlined />
            <RangePicker
              value={range}
              format="DD/MM/YYYY"
              allowClear={false}
              variant="borderless"
              onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
            />
          </div>
          {canExport && period ? (
            <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(period)}>
              {t('export')}
            </Button>
          ) : null}
        </div>
      </div>

      <Spin spinning={loading}>
        <div className="reports-hub__kpis">
          {kpis.map((kpi) => (
            <Link key={kpi.key} to={kpi.to} className={`reports-hub__kpi reports-hub__kpi--${kpi.key}`}>
              <span className="reports-hub__kpi-icon">{kpi.icon}</span>
              <span className="reports-hub__kpi-body">
                <span className="reports-hub__kpi-label">{kpi.label}</span>
                <span className="reports-hub__kpi-value">{kpi.value}</span>
                <span className="reports-hub__kpi-meta">
                  {kpi.delta ? (
                    <span className={`reports-hub__kpi-delta reports-hub__kpi-delta--${kpi.delta.kind}`}>
                      {kpi.delta.kind === 'down' ? <FallOutlined /> : <RiseOutlined />} {kpi.delta.text}
                    </span>
                  ) : null}
                  <span className="reports-hub__kpi-hint">{kpi.hint}</span>
                </span>
              </span>
            </Link>
          ))}
        </div>

        {view === 'customers' ? (
          <Navigate to="/reports/customers" replace />
        ) : view === 'list' ? (
          <Navigate to="/reports/catalog" replace />
        ) : (
          <div className="reports-hub__body">
            <div className="reports-hub__main">
              <div className="reports-hub__charts">
                <section className="reports-hub__panel">
                  <PanelHead
                    icon={<BarChartOutlined />}
                    title={t('charts.daily')}
                    to="/reports/sales/revenue-by-period"
                    more={t('viewAll')}
                  />
                  <div className="reports-hub__legend">
                    <span>
                      <i style={{ background: '#9ec5ff' }} />
                      {t('charts.revenue')}
                    </span>
                    <span>
                      <i style={{ background: '#1677ff' }} />
                      {t('charts.orders')}
                    </span>
                  </div>
                  {points.length === 0 ? (
                    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
                  ) : (
                    <ComboChart points={points} />
                  )}
                </section>
                <section className="reports-hub__panel">
                  <PanelHead
                    icon={<PieChartOutlined />}
                    title={t('charts.category')}
                    to="/reports/sales/revenue-by-category"
                    more={t('viewAll')}
                  />
                  {slices.length === 0 ? (
                    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
                  ) : (
                    <div className="reports-hub__donut-wrap">
                      <div className="reports-hub__donut" style={{ background: buildConicGradient(slices) }}>
                        <div className="reports-hub__donut-hole">
                          <span className="reports-hub__donut-total">{compactMoney(net)}</span>
                          <span className="reports-hub__donut-caption">{t('charts.total')}</span>
                        </div>
                      </div>
                      <ul className="reports-hub__cat-list">
                        {slices.map((slice) => (
                          <li key={slice.label}>
                            <span>
                              <span className="reports-hub__dot" style={{ background: slice.color }} />
                              {slice.label}
                            </span>
                            <span className="num">
                              {slice.sharePercent}% · {compactMoney(slice.netAmount)}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </section>
              </div>

              <div className="reports-hub__tables">
                <section className="reports-hub__panel">
                  <PanelHead
                    icon={<AppstoreOutlined />}
                    title={t('tables.topProducts')}
                    to="/reports/sales/revenue-by-employee-product"
                    more={t('viewAll')}
                  />
                  {products.length === 0 ? (
                    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
                  ) : (
                    <table className="reports-hub__table">
                      <thead>
                        <tr>
                          <th>{t('tables.product')}</th>
                          <th className="num">{t('tables.qty')}</th>
                          <th className="num">{t('tables.revenue')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {products.map((row, index) => (
                          <tr key={row.code}>
                            <td>
                              <span className="reports-hub__product">
                                <span
                                  className={`reports-hub__rank${index < 3 ? ` reports-hub__rank--${index + 1}` : ''}`}
                                >
                                  {index + 1}
                                </span>
                                <span className="reports-hub__product-name" title={row.name}>
                                  {row.name}
                                </span>
                              </span>
                            </td>
                            <td className="num">{row.qty.toLocaleString('vi-VN')}</td>
                            <td className="num">{formatDisplayMoney(row.net)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </section>
                <section className="reports-hub__panel">
                  <PanelHead
                    icon={<TeamOutlined />}
                    title={t('tables.staff')}
                    to="/reports/sales/revenue-by-employee"
                    more={t('viewAll')}
                  />
                  {staff.length === 0 ? (
                    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
                  ) : (
                    staff.map((row) => {
                      const href = row.id
                        ? `/reports/sales/revenue-by-employee-product?employeeId=${encodeURIComponent(row.id)}`
                        : '/reports/sales/revenue-by-employee';
                      return (
                        <Link key={row.name} to={href} className="reports-hub__hbar">
                          <span className="reports-hub__avatar">{initials(row.name)}</span>
                          <span>
                            <span className="reports-hub__hbar-name" title={row.name}>
                              {row.name}
                            </span>
                            <span className="reports-hub__hbar-share">
                              {row.share.toLocaleString('vi-VN')}%
                            </span>
                            <span className="reports-hub__hbar-track">
                              <span
                                className="reports-hub__hbar-fill"
                                style={{ width: `${Math.max(4, (row.net / maxStaffNet) * 100)}%` }}
                              />
                            </span>
                          </span>
                          <span className="reports-hub__hbar-val">{compactMoney(row.net)}</span>
                        </Link>
                      );
                    })
                  )}
                </section>
                <section className="reports-hub__panel">
                  <PanelHead
                    icon={<UserAddOutlined />}
                    title={t('tables.customers')}
                    to="/reports/customers"
                    more={t('viewAll')}
                  />
                  <CustomerAttach orders={orders} namedOrders={namedOrders} customerCount={customerCount} />
                </section>
              </div>
            </div>

            <aside className="reports-hub__side">
              <section className="reports-hub__panel">
                <PanelHead icon={<ThunderboltOutlined />} title={t('help.title')} />
                <ul className="reports-hub__help">
                  <li>
                    <span className="reports-hub__help-icon">
                      <RiseOutlined />
                    </span>
                    <span>{t('help.one')}</span>
                  </li>
                  <li>
                    <span className="reports-hub__help-icon">
                      <TeamOutlined />
                    </span>
                    <span>{t('help.two')}</span>
                  </li>
                  <li>
                    <span className="reports-hub__help-icon">
                      <UserAddOutlined />
                    </span>
                    <span>{t('help.three')}</span>
                  </li>
                </ul>
              </section>
              <section className="reports-hub__panel">
                <PanelHead icon={<CalendarOutlined />} title={t('quick.title')} />
                <div className="reports-hub__quick">
                  <Link to="/reports/sales/revenue-by-employee-product">
                    <span className="reports-hub__quick-icon">
                      <AppstoreOutlined />
                    </span>
                    <span className="reports-hub__quick-label">{t('quick.products')}</span>
                    <ArrowRightOutlined />
                  </Link>
                  <Link to="/reports/sales/revenue-by-employee">
                    <span className="reports-hub__quick-icon">
                      <TeamOutlined />
                    </span>
                    <span className="reports-hub__quick-label">{t('quick.staff')}</span>
                    <ArrowRightOutlined />
                  </Link>
                  <Link to="/reports/customers">
                    <span className="reports-hub__quick-icon">
                      <UserAddOutlined />
                    </span>
                    <span className="reports-hub__quick-label">{t('quick.customers')}</span>
                    <ArrowRightOutlined />
                  </Link>
                  <Link to="/reports/inventory/near-expiry">
                    <span className="reports-hub__quick-icon">
                      <InboxOutlined />
                    </span>
                    <span className="reports-hub__quick-label">{t('quick.expiry')}</span>
                    <ArrowRightOutlined />
                  </Link>
                  <Link to="/reports/catalog">
                    <span className="reports-hub__quick-icon">
                      <BarChartOutlined />
                    </span>
                    <span className="reports-hub__quick-label">{t('quick.all')}</span>
                    <ArrowRightOutlined />
                  </Link>
                </div>
              </section>
            </aside>
          </div>
        )}
      </Spin>
    </div>
  );
}

function describeDelta(
  current: number,
  previous: number,
  t: (key: string) => string,
): { kind: 'up' | 'down' | 'new'; text: string } | null {
  if (previous === 0) {
    return current === 0 ? null : { kind: 'new', text: t('kpi.newPeriod') };
  }
  const pct = Math.round(((current - previous) / previous) * 1000) / 10;
  if (Math.abs(pct) > 80) {
    return { kind: current >= previous ? 'up' : 'down', text: t('kpi.newPeriod') };
  }
  return {
    kind: pct < 0 ? 'down' : 'up',
    text: `${pct > 0 ? '+' : ''}${pct}%`,
  };
}

function ComboChart({ points }: { points: HubPoint[] }) {
  const maxNet = Math.max(...points.map((p) => p.netAmount), 1);
  const maxOrd = Math.max(...points.map((p) => p.orderCount), 1);
  const step = points.length > 16 ? Math.ceil(points.length / 8) : 1;
  const line = points
    .map((p, i) => {
      const x = points.length === 1 ? 50 : (i / (points.length - 1)) * 100;
      const y = 96 - (p.orderCount / maxOrd) * 86;
      return `${x},${y}`;
    })
    .join(' ');

  return (
    <div className="reports-hub__combo">
      <div
        className="reports-hub__combo-bars"
        style={{ gridTemplateColumns: `repeat(${points.length}, minmax(4px, 1fr))` }}
      >
        {points.map((p, i) => (
          <div
            key={`${p.label}-${i}`}
            className="reports-hub__combo-bar"
            style={{ height: `${Math.max(3, (p.netAmount / maxNet) * 100)}%` }}
            title={`${p.label}: ${formatDisplayMoney(p.netAmount)} / ${p.orderCount}`}
          />
        ))}
      </div>
      <svg className="reports-hub__combo-svg" viewBox="0 0 100 100" preserveAspectRatio="none">
        <polyline fill="none" stroke="#1677ff" strokeWidth="1.6" points={line} vectorEffect="non-scaling-stroke" />
      </svg>
      <div
        className="reports-hub__combo-labels"
        style={{ gridTemplateColumns: `repeat(${points.length}, minmax(4px, 1fr))` }}
      >
        {points.map((p, i) => (
          <span key={`${p.label}-l-${i}`} className="reports-hub__combo-label">
            {i % step === 0 ? p.label : ''}
          </span>
        ))}
      </div>
    </div>
  );
}

function CustomerAttach({
  orders,
  namedOrders,
  customerCount,
}: {
  orders: number;
  namedOrders: number;
  customerCount: number;
}) {
  const { t } = useTranslation('reports', { keyPrefix: 'hub.tables' });
  const walkIn = Math.max(0, orders - namedOrders);
  const rate = orders > 0 ? Math.round((namedOrders / orders) * 1000) / 10 : 0;
  const namedPct = orders > 0 ? (namedOrders / orders) * 100 : 0;
  return (
    <div>
      <div className="reports-hub__attach-rate">
        <strong>{rate}%</strong>
        <span>{t('attachRate')}</span>
      </div>
      <div className="reports-hub__split" aria-hidden>
        <span className="reports-hub__split-named" style={{ width: `${namedPct}%` }} />
        <span className="reports-hub__split-walk" style={{ width: `${Math.max(0, 100 - namedPct)}%` }} />
      </div>
      <div className="reports-hub__stat-grid">
        <div className="reports-hub__stat">
          <span className="reports-hub__stat-label">
            <UserAddOutlined /> {t('namedOrders')}
          </span>
          <b>{namedOrders.toLocaleString('vi-VN')}</b>
        </div>
        <div className="reports-hub__stat">
          <span className="reports-hub__stat-label">
            <UserOutlined /> {t('walkIn')}
          </span>
          <b>{walkIn.toLocaleString('vi-VN')}</b>
        </div>
        <div className="reports-hub__stat">
          <span className="reports-hub__stat-label">
            <TeamOutlined /> {t('bookCustomers')}
          </span>
          <b>{customerCount.toLocaleString('vi-VN')}</b>
        </div>
      </div>
    </div>
  );
}

