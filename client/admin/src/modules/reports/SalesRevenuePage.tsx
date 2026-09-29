import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Button,
  Checkbox,
  DatePicker,
  Dropdown,
  Empty,
  Input,
  Segmented,
  Select,
  Spin,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  BarChartOutlined,
  DownloadOutlined,
  FallOutlined,
  FundOutlined,
  PieChartOutlined,
  ReloadOutlined,
  RiseOutlined,
  SearchOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
  UndoOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportColumn, ReportTableResult } from '@/shared/api/reports.types';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatDisplayMoney } from '@/shared/utils/money';
import { exportReportCsv, formatReportCell } from '@/modules/reports/report-export';
import {
  buildCategoryChartSlices,
  buildConicGradient,
} from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  readReportFieldString,
} from '@/modules/dashboard/dashboard-revenue-range';
import './sales-revenue.css';

const { RangePicker } = DatePicker;

type GroupBy = 'day' | 'week' | 'month';
type Tone = 'revenue' | 'paid' | 'debt' | 'collection' | 'orders' | 'refund';

type DetailRow = {
  key: string;
  periodLabel: string;
  salesAmount: number;
  checkoutPaid: number;
  newDebt: number;
  collectionAmount: number;
  cashAmount: number;
  transferAmount: number;
  cardAmount: number;
  ewalletAmount: number;
  refundAmount: number;
  refundCash: number;
  refundDebt: number;
  netAmount: number;
  orderCount: number;
  aov: number;
  changePct: number | null;
  sharePct: number;
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

function priorRange(range: [Dayjs, Dayjs]): [Dayjs, Dayjs] {
  const days = range[1].startOf('day').diff(range[0].startOf('day'), 'day') + 1;
  const prevTo = range[0].subtract(1, 'day').endOf('day');
  return [prevTo.subtract(days - 1, 'day').startOf('day'), prevTo];
}

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)} tr`;
  return formatDisplayMoney(value);
}

function describeDelta(
  current: number,
  previous: number,
  newLabel: string,
): { kind: 'up' | 'down' | 'new'; text: string } | null {
  if (previous === 0) return current === 0 ? null : { kind: 'new', text: newLabel };
  const pct = Math.round(((current - previous) / previous) * 1000) / 10;
  if (Math.abs(pct) > 999) return { kind: current >= previous ? 'up' : 'down', text: newLabel };
  return { kind: pct < 0 ? 'down' : 'up', text: `${pct > 0 ? '+' : ''}${pct}%` };
}

export function SalesRevenuePage() {
  const { t } = useTranslation('reports', { keyPrefix: 'revenue' });
  const { t: tg } = useTranslation('reports', { keyPrefix: 'groupBy' });
  const canExport = useCanReportsExport();

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [groupBy, setGroupBy] = useState<GroupBy>('day');
  const [warehouseId, setWarehouseId] = useState<string>();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [query, setQuery] = useState('');
  const [hiddenCols, setHiddenCols] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [period, setPeriod] = useState<ReportTableResult | null>(null);
  const [prior, setPrior] = useState<ReportTableResult | null>(null);
  const [categoryRows, setCategoryRows] = useState<Record<string, unknown>[]>([]);

  useEffect(() => {
    void fetchWarehouses()
      .then(setWarehouses)
      .catch(() => undefined);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    const current = toIsoRange(range);
    const previous = toIsoRange(priorRange(range));
    const extra = warehouseId ? { warehouseId } : {};
    try {
      const [periodRes, priorRes, categoryRes] = await Promise.all([
        runReport('sales/revenue-by-period', { ...current, groupBy, ...extra }),
        runReport('sales/revenue-by-period', { ...previous, groupBy, ...extra }),
        runReport('sales/revenue-by-category', { ...current, ...extra }),
      ]);
      setPeriod(periodRes);
      setPrior(priorRes);
      setCategoryRows(categoryRes.rows);
    } finally {
      setLoading(false);
    }
  }, [range, groupBy, warehouseId]);

  useEffect(() => {
    void load();
  }, [load]);

  const net = period?.totals ? readReportFieldNumber(period.totals, 'netAmount') : 0;
  const sales = period?.totals ? readReportFieldNumber(period.totals, 'salesAmount') : 0;
  const checkoutPaid = period?.totals ? readReportFieldNumber(period.totals, 'checkoutPaid') : 0;
  const newDebt = period?.totals ? readReportFieldNumber(period.totals, 'newDebt') : 0;
  const collectionAmount = period?.totals ? readReportFieldNumber(period.totals, 'collectionAmount') : 0;
  const refund = period?.totals ? readReportFieldNumber(period.totals, 'refundAmount') : 0;
  const orders = period?.totals ? readReportFieldNumber(period.totals, 'orderCount') : 0;
  const aov = orders > 0 ? Math.round(sales / orders) : 0;
  const priorSales = prior?.totals ? readReportFieldNumber(prior.totals, 'salesAmount') : 0;
  const priorPaid = prior?.totals ? readReportFieldNumber(prior.totals, 'checkoutPaid') : 0;
  const priorDebt = prior?.totals ? readReportFieldNumber(prior.totals, 'newDebt') : 0;
  const priorCollection = prior?.totals ? readReportFieldNumber(prior.totals, 'collectionAmount') : 0;
  const priorOrders = prior?.totals ? readReportFieldNumber(prior.totals, 'orderCount') : 0;
  const priorRefund = prior?.totals ? readReportFieldNumber(prior.totals, 'refundAmount') : 0;

  const points = useMemo(
    () =>
      (period?.rows ?? []).map((row) => ({
        label: readReportFieldString(row, 'periodLabel'),
        netAmount: readReportFieldNumber(row, 'salesAmount'),
        orderCount: readReportFieldNumber(row, 'orderCount'),
      })),
    [period],
  );

  const slices = useMemo(
    () => buildCategoryChartSlices(categoryRows, t('other'), 7),
    [categoryRows, t],
  );

  const detailRows = useMemo<DetailRow[]>(() => {
    const rows = period?.rows ?? [];
    return rows.map((row, index) => {
      const salesAmount = readReportFieldNumber(row, 'salesAmount');
      const netAmount = readReportFieldNumber(row, 'netAmount');
      const orderCount = readReportFieldNumber(row, 'orderCount');
      const prevSales = index > 0 ? readReportFieldNumber(rows[index - 1], 'salesAmount') : null;
      const changePct =
        prevSales != null && prevSales !== 0
          ? Math.round(((salesAmount - prevSales) / prevSales) * 1000) / 10
          : null;
      return {
        key: `${readReportFieldString(row, 'periodLabel')}-${index}`,
        periodLabel: readReportFieldString(row, 'periodLabel'),
        salesAmount,
        checkoutPaid: readReportFieldNumber(row, 'checkoutPaid'),
        newDebt: readReportFieldNumber(row, 'newDebt'),
        collectionAmount: readReportFieldNumber(row, 'collectionAmount'),
        cashAmount: readReportFieldNumber(row, 'cashAmount'),
        transferAmount: readReportFieldNumber(row, 'transferAmount'),
        cardAmount: readReportFieldNumber(row, 'cardAmount'),
        ewalletAmount: readReportFieldNumber(row, 'ewalletAmount'),
        refundAmount: readReportFieldNumber(row, 'refundAmount'),
        refundCash: readReportFieldNumber(row, 'refundCash'),
        refundDebt: readReportFieldNumber(row, 'refundDebt'),
        netAmount,
        orderCount,
        aov: orderCount > 0 ? Math.round(salesAmount / orderCount) : 0,
        changePct,
        sharePct: sales > 0 ? Math.round((salesAmount / sales) * 1000) / 10 : 0,
      };
    });
  }, [period, sales]);

  const filteredRows = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return detailRows;
    return detailRows.filter((row) => row.periodLabel.toLowerCase().includes(q));
  }, [detailRows, query]);

  const columnDefs = useMemo(
    () => [
      { key: 'periodLabel', title: t(`cols.periodBy.${groupBy}`), align: 'left' as const },
      { key: 'orderCount', title: t('cols.orders'), align: 'right' as const },
      { key: 'salesAmount', title: t('cols.sales'), align: 'right' as const },
      { key: 'checkoutPaid', title: t('cols.checkout'), align: 'right' as const },
      { key: 'newDebt', title: t('cols.debt'), align: 'right' as const },
      { key: 'collectionAmount', title: t('cols.collection'), align: 'right' as const },
      { key: 'cashAmount', title: t('cols.cash'), align: 'right' as const },
      { key: 'transferAmount', title: t('cols.transfer'), align: 'right' as const },
      { key: 'cardAmount', title: t('cols.card'), align: 'right' as const },
      { key: 'ewalletAmount', title: t('cols.ewallet'), align: 'right' as const },
      { key: 'refundAmount', title: t('cols.refund'), align: 'right' as const },
      { key: 'refundCash', title: t('cols.refundCash'), align: 'right' as const },
      { key: 'refundDebt', title: t('cols.refundDebt'), align: 'right' as const },
      { key: 'netAmount', title: t('cols.net'), align: 'right' as const },
      { key: 'aov', title: t('cols.aov'), align: 'right' as const },
      { key: 'changePct', title: t('cols.change'), align: 'right' as const },
      { key: 'sharePct', title: t('cols.share'), align: 'right' as const },
    ],
    [t, groupBy],
  );

  const columns: ColumnsType<DetailRow> = useMemo(
    () =>
      columnDefs
        .filter((col) => !hiddenCols.includes(col.key))
        .map((col) => ({
          title: col.title,
          dataIndex: col.key,
          key: col.key,
          align: col.align,
          render: (value: unknown, row: DetailRow) => {
            if (col.key === 'changePct') {
              if (row.changePct == null) return '—';
              const up = row.changePct >= 0;
              return (
                <span className={up ? 'sales-rev__up' : 'sales-rev__down'}>
                  {up ? <RiseOutlined /> : <FallOutlined />} {up ? '+' : ''}
                  {row.changePct}%
                </span>
              );
            }
            if (col.key === 'sharePct') return `${row.sharePct}%`;
            if (col.key === 'orderCount') return row.orderCount.toLocaleString('vi-VN');
            if (col.key === 'periodLabel') return row.periodLabel;
            return formatDisplayMoney(Number(value));
          },
        })),
    [columnDefs, hiddenCols],
  );

  const exportResult = useMemo<ReportTableResult | null>(() => {
    if (!period) return null;
    const columns: ReportColumn[] = columnDefs.map((col) => ({
      key: col.key,
      title: col.title,
      format: col.key === 'periodLabel' ? 'text' : col.key === 'orderCount' ? 'integer' : 'text',
      align: col.align,
    }));
    return {
      ...period,
      columns,
      rows: detailRows.map((row) => ({
        ...row,
        changePct: row.changePct == null ? '—' : `${row.changePct}%`,
        sharePct: `${row.sharePct}%`,
        salesAmount: formatReportCell(row.salesAmount, 'money'),
        checkoutPaid: formatReportCell(row.checkoutPaid, 'money'),
        newDebt: formatReportCell(row.newDebt, 'money'),
        collectionAmount: formatReportCell(row.collectionAmount, 'money'),
        cashAmount: formatReportCell(row.cashAmount, 'money'),
        transferAmount: formatReportCell(row.transferAmount, 'money'),
        cardAmount: formatReportCell(row.cardAmount, 'money'),
        ewalletAmount: formatReportCell(row.ewalletAmount, 'money'),
        refundAmount: formatReportCell(row.refundAmount, 'money'),
        refundCash: formatReportCell(row.refundCash, 'money'),
        refundDebt: formatReportCell(row.refundDebt, 'money'),
        netAmount: formatReportCell(row.netAmount, 'money'),
        aov: formatReportCell(row.aov, 'money'),
      })),
      totals: {
        periodLabel: t('total'),
        salesAmount: formatReportCell(sales, 'money'),
        checkoutPaid: formatReportCell(checkoutPaid, 'money'),
        newDebt: formatReportCell(newDebt, 'money'),
        collectionAmount: formatReportCell(collectionAmount, 'money'),
        refundAmount: formatReportCell(refund, 'money'),
        refundCash: formatReportCell(readReportFieldNumber(period.totals ?? {}, 'refundCash'), 'money'),
        refundDebt: formatReportCell(readReportFieldNumber(period.totals ?? {}, 'refundDebt'), 'money'),
        netAmount: formatReportCell(net, 'money'),
        orderCount: orders,
        aov: formatReportCell(aov, 'money'),
        changePct: '—',
        sharePct: '100%',
      },
    };
  }, [period, columnDefs, detailRows, t, sales, checkoutPaid, newDebt, collectionAmount, refund, net, orders, aov]);

  const kpis = [
    {
      key: 'revenue' as Tone,
      label: t('kpi.revenue'),
      value: formatDisplayMoney(sales),
      delta: describeDelta(sales, priorSales, t('kpi.newPeriod')),
      icon: <FundOutlined />,
    },
    {
      key: 'paid' as Tone,
      label: t('kpi.paid'),
      value: formatDisplayMoney(checkoutPaid),
      delta: describeDelta(checkoutPaid, priorPaid, t('kpi.newPeriod')),
      icon: <BarChartOutlined />,
    },
    {
      key: 'debt' as Tone,
      label: t('kpi.debt'),
      value: formatDisplayMoney(newDebt),
      delta: describeDelta(newDebt, priorDebt, t('kpi.newPeriod')),
      icon: <ShoppingCartOutlined />,
    },
    {
      key: 'collection' as Tone,
      label: t('kpi.collection'),
      value: formatDisplayMoney(collectionAmount),
      delta: describeDelta(collectionAmount, priorCollection, t('kpi.newPeriod')),
      icon: <UndoOutlined />,
    },
    {
      key: 'orders' as Tone,
      label: t('kpi.orders'),
      value: orders.toLocaleString('vi-VN'),
      delta: describeDelta(orders, priorOrders, t('kpi.newPeriod')),
      icon: <ShoppingCartOutlined />,
    },
    {
      key: 'refund' as Tone,
      label: t('kpi.refund'),
      value: formatDisplayMoney(refund),
      delta: describeDelta(refund, priorRefund, t('kpi.newPeriod')),
      icon: <UndoOutlined />,
    },
  ];

  return (
    <div className="sales-rev">
      <div className="sales-rev__head">
        <div>
          <Typography.Title level={3} className="sales-rev__title">
            {t('title')}
            <span className="sales-rev__code">SALES-01</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="sales-rev__desc">
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        {canExport && exportResult ? (
          <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(exportResult)}>
            {t('export')}
          </Button>
        ) : null}
      </div>

      <div className="sales-rev__toolbar">
        <label className="sales-rev__field">
          <span>{t('period')}</span>
          <RangePicker
            value={range}
            allowClear={false}
            format="DD/MM/YYYY"
            onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
          />
        </label>
        <label className="sales-rev__field">
          <span>{t('groupBy')}</span>
          <Segmented<GroupBy>
            value={groupBy}
            onChange={setGroupBy}
            options={[
              { label: tg('day'), value: 'day' },
              { label: tg('week'), value: 'week' },
              { label: tg('month'), value: 'month' },
            ]}
          />
        </label>
        <label className="sales-rev__field sales-rev__field--grow">
          <span>{t('warehouse')}</span>
          <Select
            allowClear
            style={{ width: '100%', minWidth: 180 }}
            placeholder={t('warehouseAll')}
            value={warehouseId}
            onChange={setWarehouseId}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          />
        </label>
        <Button type="primary" icon={<ReloadOutlined />} loading={loading} onClick={() => void load()}>
          {t('run')}
        </Button>
      </div>

      <Spin spinning={loading}>
        <div className="sales-rev__kpis">
          {kpis.map((kpi) => (
            <div key={kpi.key} className={`sales-rev__kpi sales-rev__kpi--${kpi.key}`}>
              <span className="sales-rev__kpi-icon">{kpi.icon}</span>
              <span>
                <span className="sales-rev__kpi-label">{kpi.label}</span>
                <span className="sales-rev__kpi-value">{kpi.value}</span>
                <span className="sales-rev__kpi-meta">
                  {kpi.delta ? (
                    <span className={`sales-rev__delta sales-rev__delta--${kpi.delta.kind}`}>
                      {kpi.delta.kind === 'down' ? <FallOutlined /> : <RiseOutlined />} {kpi.delta.text}
                    </span>
                  ) : null}
                  <span className="sales-rev__kpi-hint">{t('kpi.vsPrior')}</span>
                </span>
              </span>
            </div>
          ))}
        </div>

        <div className="sales-rev__charts">
          <section className="sales-rev__panel">
            <div className="sales-rev__panel-head">
              <span className="sales-rev__panel-icon">
                <BarChartOutlined />
              </span>
              <h3>{t(`charts.${groupBy}`)}</h3>
              <div className="sales-rev__legend">
                <span>
                  <i style={{ background: '#9ec5ff' }} />
                  {t('charts.revenue')}
                </span>
                <span>
                  <i style={{ background: '#52c41a' }} />
                  {t('charts.orders')}
                </span>
              </div>
            </div>
            {points.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <ComboChart points={points} />
            )}
          </section>
          <section className="sales-rev__panel">
            <div className="sales-rev__panel-head">
              <span className="sales-rev__panel-icon">
                <PieChartOutlined />
              </span>
              <h3>{t('charts.category')}</h3>
              <Link to="/reports/sales/revenue-by-category" className="sales-rev__more">
                {t('byCategory')}
              </Link>
            </div>
            {slices.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="sales-rev__donut-wrap">
                <div className="sales-rev__donut" style={{ background: buildConicGradient(slices) }}>
                  <div className="sales-rev__donut-hole">
                    <strong>{compactMoney(sales)}</strong>
                    <span>{t('charts.total')}</span>
                  </div>
                </div>
                <ul className="sales-rev__cat-list">
                  {slices.map((slice) => (
                    <li key={slice.label}>
                      <span>
                        <i style={{ background: slice.color }} />
                        {slice.label}
                      </span>
                      <b>
                        {slice.sharePercent}% · {compactMoney(slice.netAmount)}
                      </b>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
        </div>

        <section className="sales-rev__panel sales-rev__table-panel">
          <div className="sales-rev__panel-head">
            <span className="sales-rev__panel-icon">
              <FundOutlined />
            </span>
            <h3>{t(`table.${groupBy}`)}</h3>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={t('search')}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              style={{ width: 220, marginLeft: 'auto' }}
            />
            <Dropdown
              trigger={['click']}
              popupRender={() => (
                <div className="sales-rev__cols">
                  <Checkbox.Group
                    value={columnDefs.filter((col) => !hiddenCols.includes(col.key)).map((col) => col.key)}
                    onChange={(keys) => {
                      const visible = new Set(keys.map(String));
                      setHiddenCols(columnDefs.filter((col) => !visible.has(col.key)).map((col) => col.key));
                    }}
                  >
                    {columnDefs.map((col) => (
                      <div key={col.key}>
                        <Checkbox value={col.key}>{col.title}</Checkbox>
                      </div>
                    ))}
                  </Checkbox.Group>
                </div>
              )}
            >
              <Button icon={<SettingOutlined />}>{t('columns')}</Button>
            </Dropdown>
          </div>
          <Table<DetailRow>
            rowKey="key"
            size="middle"
            columns={columns}
            dataSource={filteredRows}
            pagination={{ pageSize: 14, showSizeChanger: false, showTotal: (n) => t('rows', { count: n }) }}
            locale={{ emptyText: t('empty') }}
            scroll={{ x: true }}
            summary={() =>
              filteredRows.length ? (
                <Table.Summary fixed>
                  <Table.Summary.Row>
                    {columnDefs
                      .filter((col) => !hiddenCols.includes(col.key))
                      .map((col, index) => (
                        <Table.Summary.Cell key={col.key} index={index} align={col.align}>
                          <Typography.Text strong>
                            {col.key === 'periodLabel'
                              ? t('total')
                              : col.key === 'salesAmount'
                                ? formatDisplayMoney(sales)
                                : col.key === 'refundAmount'
                                  ? formatDisplayMoney(refund)
                                  : col.key === 'netAmount'
                                    ? formatDisplayMoney(net)
                                    : col.key === 'orderCount'
                                      ? orders.toLocaleString('vi-VN')
                                      : col.key === 'aov'
                                        ? formatDisplayMoney(aov)
                                        : col.key === 'sharePct'
                                          ? '100%'
                                          : '—'}
                          </Typography.Text>
                        </Table.Summary.Cell>
                      ))}
                  </Table.Summary.Row>
                </Table.Summary>
              ) : null
            }
          />
        </section>
      </Spin>
    </div>
  );
}

function ComboChart({
  points,
}: {
  points: { label: string; netAmount: number; orderCount: number }[];
}) {
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
    <div className="sales-rev__combo">
      <div
        className="sales-rev__combo-bars"
        style={{ gridTemplateColumns: `repeat(${points.length}, minmax(6px, 1fr))` }}
      >
        {points.map((p, i) => (
          <div
            key={`${p.label}-${i}`}
            className="sales-rev__combo-bar"
            style={{ height: `${Math.max(3, (p.netAmount / maxNet) * 100)}%` }}
            title={`${p.label}: ${formatDisplayMoney(p.netAmount)} / ${p.orderCount}`}
          />
        ))}
      </div>
      <svg className="sales-rev__combo-svg" viewBox="0 0 100 100" preserveAspectRatio="none">
        <polyline fill="none" stroke="#52c41a" strokeWidth="1.8" points={line} vectorEffect="non-scaling-stroke" />
      </svg>
      <div
        className="sales-rev__combo-labels"
        style={{ gridTemplateColumns: `repeat(${points.length}, minmax(6px, 1fr))` }}
      >
        {points.map((p, i) => (
          <span key={`${p.label}-l-${i}`}>{i % step === 0 ? p.label.slice(0, 5) : ''}</span>
        ))}
      </div>
    </div>
  );
}
