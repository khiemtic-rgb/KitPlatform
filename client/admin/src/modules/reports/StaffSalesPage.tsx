import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
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
  DownloadOutlined,
  FallOutlined,
  FundOutlined,
  PieChartOutlined,
  ReloadOutlined,
  RiseOutlined,
  SearchOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
  UserOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchEmployees } from '@/shared/api/identity-admin.api';
import type { EmployeeLookup } from '@/shared/api/identity-admin.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportColumn, ReportTableResult } from '@/shared/api/reports.types';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatDisplayMoney, formatDisplayQuantity } from '@/shared/utils/money';
import { exportReportCsv } from '@/modules/reports/report-export';
import { buildConicGradient } from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  readReportFieldString,
} from '@/modules/dashboard/dashboard-revenue-range';
import './staff-sales.css';

const { RangePicker } = DatePicker;
const SLICE = ['#1677ff', '#13c2c2', '#722ed1', '#fa8c16', '#eb2f96', '#52c41a'];

type StaffRow = {
  key: string;
  employeeId: string;
  employeeName: string;
  orderCount: number;
  namedOrderCount: number;
  salesAmount: number;
  refundAmount: number;
  netAmount: number;
  aov: number;
  sharePercent: number;
};

type ProductRow = {
  key: string;
  employeeId: string;
  employeeName: string;
  productCode: string;
  productName: string;
  orderCount: number;
  qty: number;
  refundQty: number;
  netQty: number;
  salesAmount: number;
  refundAmount: number;
  netAmount: number;
};

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('month'), dayjs().endOf('day')];
}

function quarterStart() {
  const month = dayjs().month();
  return dayjs().month(Math.floor(month / 3) * 3).startOf('month');
}

function rangeFromSearch(params: URLSearchParams): [Dayjs, Dayjs] {
  const from = params.get('from');
  const to = params.get('to');
  if (from && to) {
    const start = dayjs(from);
    const end = dayjs(to);
    if (start.isValid() && end.isValid()) return [start.startOf('day'), end.endOf('day')];
  }
  return defaultRange();
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

function staffQuery(opts: { employeeId?: string; from?: Dayjs; to?: Dayjs; warehouseId?: string }) {
  const q = new URLSearchParams();
  if (opts.employeeId) q.set('employeeId', opts.employeeId);
  if (opts.from) q.set('from', opts.from.format('YYYY-MM-DD'));
  if (opts.to) q.set('to', opts.to.format('YYYY-MM-DD'));
  if (opts.warehouseId) q.set('warehouseId', opts.warehouseId);
  const qs = q.toString();
  return qs ? `?${qs}` : '';
}

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}Mđ`;
  if (Math.abs(value) >= 1_000) return `${Math.round(value / 1_000)}Nđ`;
  return formatDisplayMoney(value);
}

function axisTicks(max: number): number[] {
  const step = max <= 1_000_000 ? 250_000 : max <= 4_000_000 ? 1_000_000 : Math.ceil(max / 4 / 500_000) * 500_000;
  const top = Math.max(step, Math.ceil(max / step) * step);
  return [0, 1, 2, 3, 4].map((i) => (top / 4) * i);
}

function describeDelta(current: number, previous: number, newLabel: string) {
  if (previous === 0) return current === 0 ? null : { kind: 'new' as const, text: newLabel };
  const pct = Math.round(((current - previous) / previous) * 1000) / 10;
  if (Math.abs(pct) > 80) return { kind: 'new' as const, text: newLabel };
  return { kind: pct < 0 ? ('down' as const) : ('up' as const), text: `${pct > 0 ? '+' : ''}${pct}%` };
}

export function StaffSalesPage() {
  const { t } = useTranslation('reports', { keyPrefix: 'staffHub' });
  const canExport = useCanReportsExport();
  const location = useLocation();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const isProduct = location.pathname.includes('revenue-by-employee-product');

  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => rangeFromSearch(searchParams));
  const [warehouseId, setWarehouseId] = useState<string | undefined>(
    () => searchParams.get('warehouseId') || undefined,
  );
  const [employeeId, setEmployeeId] = useState<string | undefined>(
    () => searchParams.get('employeeId') || undefined,
  );
  const [productSearch, setProductSearch] = useState('');
  const [appliedProductSearch, setAppliedProductSearch] = useState('');
  const [tableQuery, setTableQuery] = useState('');
  const [pageSize, setPageSize] = useState(10);
  const [hiddenCols, setHiddenCols] = useState<string[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [employees, setEmployees] = useState<EmployeeLookup[]>([]);
  const [loading, setLoading] = useState(false);
  const [summary, setSummary] = useState<ReportTableResult | null>(null);
  const [prior, setPrior] = useState<ReportTableResult | null>(null);
  const [products, setProducts] = useState<ReportTableResult | null>(null);

  useEffect(() => {
    void Promise.all([fetchWarehouses(), fetchEmployees()])
      .then(([wh, emp]) => {
        setWarehouses(wh);
        setEmployees(emp);
      })
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    setEmployeeId(searchParams.get('employeeId') || undefined);
  }, [searchParams]);

  const load = useCallback(async () => {
    setLoading(true);
    const current = toIsoRange(range);
    const previous = toIsoRange(priorRange(range));
    const extra = {
      ...current,
      ...(warehouseId ? { warehouseId } : {}),
      ...(employeeId ? { employeeId } : {}),
    };
    try {
      const [sum, prev, prod] = await Promise.all([
        runReport('sales/revenue-by-employee', extra),
        runReport('sales/revenue-by-employee', { ...previous, ...(warehouseId ? { warehouseId } : {}), ...(employeeId ? { employeeId } : {}) }),
        isProduct
          ? runReport('sales/revenue-by-employee-product', {
              ...extra,
              ...(appliedProductSearch.trim() ? { search: appliedProductSearch.trim() } : {}),
            })
          : Promise.resolve(null),
      ]);
      setSummary(sum);
      setPrior(prev);
      setProducts(prod);
    } finally {
      setLoading(false);
    }
  }, [range, warehouseId, employeeId, isProduct, appliedProductSearch]);

  useEffect(() => {
    setHiddenCols([]);
    setTableQuery('');
  }, [isProduct]);

  useEffect(() => {
    void load();
  }, [load]);

  const goView = (view: 'summary' | 'product', nextEmployee = employeeId) => {
    navigate(
      `${view === 'product' ? '/reports/sales/revenue-by-employee-product' : '/reports/sales/revenue-by-employee'}${staffQuery({
        employeeId: nextEmployee,
        from: range[0],
        to: range[1],
        warehouseId,
      })}`,
    );
  };

  const syncEmployee = (id?: string) => {
    setEmployeeId(id);
    const next = new URLSearchParams(searchParams);
    if (id) next.set('employeeId', id);
    else next.delete('employeeId');
    setSearchParams(next, { replace: true });
  };

  const staffRows = useMemo<StaffRow[]>(
    () =>
      (summary?.rows ?? []).map((row, index) => {
        const orders = readReportFieldNumber(row, 'orderCount');
        const net = readReportFieldNumber(row, 'netAmount');
        return {
          key: readReportFieldString(row, 'employeeId') || `staff-${index}`,
          employeeId: readReportFieldString(row, 'employeeId'),
          employeeName: readReportFieldString(row, 'employeeName'),
          orderCount: orders,
          namedOrderCount: readReportFieldNumber(row, 'namedOrderCount'),
          salesAmount: readReportFieldNumber(row, 'salesAmount'),
          refundAmount: readReportFieldNumber(row, 'refundAmount'),
          netAmount: net,
          aov: readReportFieldNumber(row, 'aov') || (orders > 0 ? Math.round(net / orders) : 0),
          sharePercent: readReportFieldNumber(row, 'sharePercent'),
        };
      }),
    [summary],
  );

  const productRows = useMemo<ProductRow[]>(
    () =>
      (products?.rows ?? []).map((row, index) => ({
        key: `${readReportFieldString(row, 'employeeId')}-${readReportFieldString(row, 'productCode')}-${index}`,
        employeeId: readReportFieldString(row, 'employeeId'),
        employeeName: readReportFieldString(row, 'employeeName'),
        productCode: readReportFieldString(row, 'productCode'),
        productName: readReportFieldString(row, 'productName'),
        orderCount: readReportFieldNumber(row, 'orderCount'),
        qty: readReportFieldNumber(row, 'qty'),
        refundQty: readReportFieldNumber(row, 'refundQty'),
        netQty: readReportFieldNumber(row, 'netQty'),
        salesAmount: readReportFieldNumber(row, 'salesAmount'),
        refundAmount: readReportFieldNumber(row, 'refundAmount'),
        netAmount: readReportFieldNumber(row, 'netAmount'),
      })),
    [products],
  );

  const hideEmployeeCol =
    Boolean(employeeId) ||
    (productRows.length > 0 && productRows.every((row) => row.employeeId === productRows[0]?.employeeId));

  const filteredStaff = useMemo(() => {
    const q = tableQuery.trim().toLowerCase();
    if (!q) return staffRows;
    return staffRows.filter((row) => row.employeeName.toLowerCase().includes(q));
  }, [staffRows, tableQuery]);

  const filteredProducts = useMemo(() => {
    const q = tableQuery.trim().toLowerCase();
    if (!q) return productRows;
    return productRows.filter(
      (row) =>
        row.productName.toLowerCase().includes(q) ||
        row.productCode.toLowerCase().includes(q) ||
        row.employeeName.toLowerCase().includes(q),
    );
  }, [productRows, tableQuery]);

  const net = summary?.totals ? readReportFieldNumber(summary.totals, 'netAmount') : 0;
  const orders = summary?.totals ? readReportFieldNumber(summary.totals, 'orderCount') : 0;
  const named = summary?.totals ? readReportFieldNumber(summary.totals, 'namedOrderCount') : 0;
  const aov = orders > 0 ? Math.round(net / orders) : 0;
  const priorNet = prior?.totals ? readReportFieldNumber(prior.totals, 'netAmount') : 0;
  const priorOrders = prior?.totals ? readReportFieldNumber(prior.totals, 'orderCount') : 0;
  const priorAov = priorOrders > 0 ? Math.round(priorNet / priorOrders) : 0;
  const active = staffRows.length;
  const roster = employees.length || active;
  const maxNet = Math.max(...staffRows.map((row) => row.netAmount), 1);
  const ticks = axisTicks(maxNet);
  const axisMax = ticks[ticks.length - 1] || maxNet;

  const slices = staffRows.map((row, index) => ({
    label: row.employeeName,
    netAmount: row.netAmount,
    sharePercent: row.sharePercent,
    color: SLICE[index % SLICE.length],
  }));

  const staffCols = useMemo(
    () => [
      { key: 'rank', title: t('cols.rank') },
      { key: 'employeeName', title: t('cols.name') },
      { key: 'orderCount', title: t('cols.orders') },
      { key: 'namedOrderCount', title: t('cols.named') },
      { key: 'salesAmount', title: t('cols.sales') },
      { key: 'refundAmount', title: t('cols.refund') },
      { key: 'netAmount', title: t('cols.net') },
      { key: 'aov', title: t('cols.aov') },
      { key: 'sharePercent', title: t('cols.share') },
    ],
    [t],
  );

  const productCols = useMemo(
    () => [
      ...(hideEmployeeCol ? [] : [{ key: 'employeeName', title: t('cols.name') }]),
      { key: 'productCode', title: t('cols.sku') },
      { key: 'productName', title: t('cols.product') },
      { key: 'orderCount', title: t('cols.orders') },
      { key: 'qty', title: t('cols.qty') },
      { key: 'netQty', title: t('cols.netQty') },
      { key: 'netAmount', title: t('cols.net') },
    ],
    [t, hideEmployeeCol],
  );

  const columnDefs = isProduct ? productCols : staffCols;

  const staffColumns: ColumnsType<StaffRow> = staffCols
    .filter((col) => !hiddenCols.includes(col.key))
    .map((col) => ({
      title: col.title,
      dataIndex: col.key,
      key: col.key,
      align: col.key === 'employeeName' || col.key === 'rank' ? 'left' : 'right',
      render: (_v: unknown, row: StaffRow, index: number) => {
        if (col.key === 'rank') return index + 1;
        if (col.key === 'employeeName') {
          return row.employeeId ? (
            <Link
              to={`/reports/sales/revenue-by-employee-product${staffQuery({
                employeeId: row.employeeId,
                from: range[0],
                to: range[1],
                warehouseId,
              })}`}
            >
              {row.employeeName}
            </Link>
          ) : (
            row.employeeName
          );
        }
        if (col.key === 'sharePercent') {
          return (
            <span className="staff-rep__share">
              {row.sharePercent}%
              <i style={{ width: `${Math.max(8, row.sharePercent)}%` }} />
            </span>
          );
        }
        if (col.key === 'orderCount' || col.key === 'namedOrderCount') return row[col.key].toLocaleString('vi-VN');
        return formatDisplayMoney(Number(row[col.key as keyof StaffRow]));
      },
    }));

  const productColumns: ColumnsType<ProductRow> = productCols
    .filter((col) => !hiddenCols.includes(col.key))
    .map((col) => ({
      title: col.title,
      dataIndex: col.key,
      key: col.key,
      align: col.key === 'employeeName' || col.key === 'productCode' || col.key === 'productName' ? 'left' : 'right',
      render: (_v: unknown, row: ProductRow) => {
        if (col.key === 'orderCount') return row.orderCount.toLocaleString('vi-VN');
        if (col.key === 'qty' || col.key === 'netQty') return formatDisplayQuantity(Number(row[col.key as 'qty' | 'netQty']));
        if (col.key === 'netAmount') return formatDisplayMoney(row.netAmount);
        return String(row[col.key as keyof ProductRow] ?? '—');
      },
    }));

  const exportResult = useMemo<ReportTableResult | null>(() => {
    const source = isProduct ? products : summary;
    if (!source) return null;
    const columns: ReportColumn[] = columnDefs.map((col) => ({
      key: col.key,
      title: col.title,
      format: 'text',
      align: 'left',
    }));
    return { ...source, columns };
  }, [isProduct, products, summary, columnDefs]);

  const kpis = [
    {
      key: 'revenue',
      label: t('kpi.revenue'),
      value: formatDisplayMoney(net),
      delta: describeDelta(net, priorNet, t('kpi.newPeriod')),
      icon: <FundOutlined />,
    },
    {
      key: 'orders',
      label: t('kpi.orders'),
      value: orders.toLocaleString('vi-VN'),
      delta: describeDelta(orders, priorOrders, t('kpi.newPeriod')),
      icon: <ShoppingCartOutlined />,
    },
    {
      key: 'staff',
      label: t('kpi.staff'),
      value: `${active} / ${roster || '—'}`,
      hint: t('kpi.staffHint', { pct: roster ? Math.round((active / roster) * 100) : 0 }),
      icon: <TeamOutlined />,
    },
    {
      key: 'aov',
      label: t('kpi.aov'),
      value: formatDisplayMoney(aov),
      delta: describeDelta(aov, priorAov, t('kpi.newPeriod')),
      icon: <UserOutlined />,
    },
  ];

  const presets = [
    { key: 'today', label: t('period.today'), value: [dayjs().startOf('day'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'week', label: t('period.week'), value: [dayjs().startOf('week'), dayjs().endOf('day')] as [Dayjs, Dayjs] },
    { key: 'month', label: t('period.month'), value: defaultRange() },
    { key: 'quarter', label: t('period.quarter'), value: [quarterStart(), dayjs().endOf('day')] as [Dayjs, Dayjs] },
  ];

  return (
    <div className="staff-rep">
      <div className="staff-rep__head">
        <div>
          <Typography.Title level={3} className="staff-rep__title">
            {t('title')}
            <span className="staff-rep__code">{isProduct ? 'SALES-07' : 'SALES-06'}</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="staff-rep__desc">
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        <div className="staff-rep__head-tools">
          <div className="staff-rep__presets">
            {presets.map((preset) => (
              <button
                key={preset.key}
                type="button"
                className={`staff-rep__preset${sameDayRange(range, preset.value) ? ' is-active' : ''}`}
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
          {canExport && exportResult ? (
            <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(exportResult)}>
              {t('export')}
            </Button>
          ) : null}
        </div>
      </div>

      <div className="staff-rep__mode">
        <Segmented
          value={isProduct ? 'product' : 'summary'}
          onChange={(value) => goView(value === 'product' ? 'product' : 'summary')}
          options={[
            { label: t('summary'), value: 'summary' },
            { label: t('product'), value: 'product' },
          ]}
        />
      </div>

      <div className="staff-rep__toolbar">
        <label className="staff-rep__field">
          <span>{t('warehouse')}</span>
          <Select
            allowClear
            style={{ minWidth: 180 }}
            placeholder={t('warehouseAll')}
            value={warehouseId}
            onChange={setWarehouseId}
            options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
          />
        </label>
        <label className="staff-rep__field">
          <span>{t('employee')}</span>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 220 }}
            placeholder={t('employeeAll')}
            value={employeeId}
            onChange={syncEmployee}
            options={employees.map((e) => ({ value: e.id, label: `${e.employeeCode} — ${e.fullName}` }))}
          />
        </label>
        {isProduct ? (
          <label className="staff-rep__field staff-rep__field--grow">
            <span>{t('product')}</span>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={t('productPh')}
              value={productSearch}
              onChange={(e) => setProductSearch(e.target.value)}
              onPressEnter={() => {
                if (productSearch === appliedProductSearch) void load();
                else setAppliedProductSearch(productSearch);
              }}
            />
          </label>
        ) : null}
        <Button
          type="primary"
          icon={<ReloadOutlined />}
          loading={loading}
          onClick={() => {
            if (productSearch === appliedProductSearch) void load();
            else setAppliedProductSearch(productSearch);
          }}
        >
          {t('run')}
        </Button>
      </div>

      <Spin spinning={loading}>
        <div className="staff-rep__kpis">
          {kpis.map((kpi) => (
            <div key={kpi.key} className={`staff-rep__kpi staff-rep__kpi--${kpi.key}`}>
              <span className="staff-rep__kpi-icon">{kpi.icon}</span>
              <span>
                <span className="staff-rep__kpi-label">{kpi.label}</span>
                <span className="staff-rep__kpi-value">{kpi.value}</span>
                <span className="staff-rep__kpi-meta">
                  {'delta' in kpi && kpi.delta ? (
                    <span className={`staff-rep__delta staff-rep__delta--${kpi.delta.kind}`}>
                      {kpi.delta.kind === 'down' ? <FallOutlined /> : <RiseOutlined />} {kpi.delta.text}
                    </span>
                  ) : null}
                  <span className="staff-rep__hint">
                    {'hint' in kpi && kpi.hint
                      ? kpi.hint
                      : 'delta' in kpi && kpi.delta && kpi.delta.kind !== 'new'
                        ? t('kpi.vsPrior')
                        : ''}
                  </span>
                </span>
              </span>
            </div>
          ))}
        </div>

        <div className="staff-rep__charts">
          <section className="staff-rep__panel">
            <div className="staff-rep__panel-head">
              <span className="staff-rep__panel-icon">
                <FundOutlined />
              </span>
              <h3>{t('charts.bars')}</h3>
              <span className="staff-rep__legend">
                <i />
                {t('charts.legend')}
              </span>
            </div>
            {staffRows.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="staff-rep__chart">
                <div className="staff-rep__axis">
                  {[...ticks].reverse().map((tick) => (
                    <span key={tick}>{compactMoney(tick)}</span>
                  ))}
                </div>
                <div className="staff-rep__bars" style={{ gridTemplateColumns: `repeat(${staffRows.length}, minmax(72px, 1fr))` }}>
                  {staffRows.map((row) => (
                    <div key={row.key} className="staff-rep__bar-col">
                      <span className="staff-rep__bar-val">{formatDisplayMoney(row.netAmount)}</span>
                      <span className="staff-rep__bar" style={{ height: `${Math.max(8, (row.netAmount / axisMax) * 168)}px` }} />
                      <span className="staff-rep__bar-name" title={row.employeeName}>
                        {row.employeeName}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
          <section className="staff-rep__panel">
            <div className="staff-rep__panel-head">
              <span className="staff-rep__panel-icon">
                <PieChartOutlined />
              </span>
              <h3>{t('charts.share')}</h3>
            </div>
            {slices.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="staff-rep__donut-wrap">
                <div className="staff-rep__donut" style={{ background: buildConicGradient(slices) }}>
                  <div className="staff-rep__donut-hole">
                    <strong>{compactMoney(net)}</strong>
                    <span>{t('charts.total')}</span>
                  </div>
                </div>
                <ul>
                  {slices.map((slice) => (
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
        </div>

        <section className="staff-rep__panel">
          <div className="staff-rep__panel-head">
            <h3>{isProduct ? t('table.product') : t('table.summary')}</h3>
            <span className="staff-rep__muted">
              {t('table.namedHint', { count: named.toLocaleString('vi-VN') })}
            </span>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={isProduct ? t('productPh') : t('table.search')}
              value={tableQuery}
              onChange={(e) => setTableQuery(e.target.value)}
              style={{ width: 220, marginLeft: 'auto' }}
            />
            <Dropdown
              trigger={['click']}
              popupRender={() => (
                <div className="staff-rep__cols">
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
          {isProduct ? (
            <Table<ProductRow>
              rowKey="key"
              size="middle"
              columns={productColumns}
              dataSource={filteredProducts}
              pagination={{
                pageSize,
                showSizeChanger: true,
                pageSizeOptions: ['10', '20', '50'],
                showTotal: (n) => t('rows', { count: n }),
                onShowSizeChange: (_c, size) => setPageSize(size),
              }}
              locale={{ emptyText: t('empty') }}
              scroll={{ x: true }}
            />
          ) : (
            <Table<StaffRow>
              rowKey="key"
              size="middle"
              columns={staffColumns}
              dataSource={filteredStaff}
              pagination={{
                pageSize,
                showSizeChanger: true,
                pageSizeOptions: ['10', '20', '50'],
                showTotal: (n) => t('rows', { count: n }),
                onShowSizeChange: (_c, size) => setPageSize(size),
              }}
              locale={{ emptyText: t('empty') }}
              scroll={{ x: true }}
              summary={() => (
                <Table.Summary fixed>
                  <Table.Summary.Row>
                    {staffCols
                      .filter((col) => !hiddenCols.includes(col.key))
                      .map((col, index) => (
                        <Table.Summary.Cell
                          key={col.key}
                          index={index}
                          align={col.key === 'employeeName' || col.key === 'rank' ? 'left' : 'right'}
                        >
                          <Typography.Text strong>
                            {col.key === 'employeeName'
                              ? t('total')
                              : col.key === 'orderCount'
                                ? orders.toLocaleString('vi-VN')
                                : col.key === 'namedOrderCount'
                                  ? named.toLocaleString('vi-VN')
                                  : col.key === 'salesAmount'
                                    ? formatDisplayMoney(summary ? readReportFieldNumber(summary.totals ?? {}, 'salesAmount') : 0)
                                    : col.key === 'refundAmount'
                                      ? formatDisplayMoney(summary ? readReportFieldNumber(summary.totals ?? {}, 'refundAmount') : 0)
                                      : col.key === 'netAmount'
                                        ? formatDisplayMoney(net)
                                        : col.key === 'aov'
                                          ? formatDisplayMoney(aov)
                                          : col.key === 'sharePercent'
                                            ? '100%'
                                            : ''}
                          </Typography.Text>
                        </Table.Summary.Cell>
                      ))}
                  </Table.Summary.Row>
                </Table.Summary>
              )}
            />
          )}
        </section>
      </Spin>
    </div>
  );
}
