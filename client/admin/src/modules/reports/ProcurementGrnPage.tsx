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
  Tag,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  AppstoreOutlined,
  BarChartOutlined,
  DownloadOutlined,
  FileTextOutlined,
  InboxOutlined,
  PieChartOutlined,
  ReloadOutlined,
  SearchOutlined,
  SettingOutlined,
  ShoppingCartOutlined,
  TeamOutlined,
} from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchSuppliers } from '@/shared/api/procurement.api';
import { GRN_STATUS_LABELS, GRN_STATUS_TAG, type Supplier } from '@/shared/api/procurement.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportColumn, ReportTableResult } from '@/shared/api/reports.types';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney, formatDisplayQuantity } from '@/shared/utils/money';
import { exportReportCsv } from '@/modules/reports/report-export';
import { buildConicGradient, type CategoryChartSlice } from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  readReportFieldString,
} from '@/modules/dashboard/dashboard-revenue-range';
import './procurement-grn.css';

const { RangePicker } = DatePicker;
const SLICE = ['#1677ff', '#13c2c2', '#722ed1', '#fa8c16', '#eb2f96', '#8c8c8c'];

type GroupBy = 'day' | 'week' | 'month';
type Tone = 'value' | 'grn' | 'qty' | 'suppliers';

type PeriodRow = {
  key: string;
  label: string;
  grnCount: number;
  totalQty: number;
  preTaxAmount: number;
};

type DocumentRow = {
  key: string;
  grnId: string;
  periodLabel: string;
  grnNumber: string;
  receiptDate: string;
  supplierName: string;
  warehouseName: string;
  totalQty: number;
  preTaxAmount: number;
  taxAmount: number;
  totalAmount: number;
  status: number;
  statusLabel: string;
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

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}Mđ`;
  return formatDisplayMoney(value);
}

function axisTicks(max: number): number[] {
  const step = max <= 1_000_000 ? 250_000 : max <= 4_000_000 ? 1_000_000 : Math.ceil(max / 4 / 500_000) * 500_000;
  const top = Math.max(step, Math.ceil(max / step) * step);
  return [0, 1, 2, 3, 4].map((i) => (top / 4) * i);
}

function buildSupplierSlices(rows: Record<string, unknown>[], otherLabel: string): CategoryChartSlice[] {
  const parsed = rows
    .map((row) => ({
      label: readReportFieldString(row, 'supplierName') || otherLabel,
      netAmount: readReportFieldNumber(row, 'preTaxAmount'),
    }))
    .filter((item) => item.netAmount > 0)
    .sort((a, b) => b.netAmount - a.netAmount);
  const total = parsed.reduce((sum, item) => sum + item.netAmount, 0);
  if (total <= 0) return [];
  const top = parsed.slice(0, 5);
  const other = parsed.slice(5).reduce((sum, item) => sum + item.netAmount, 0);
  const slices = top.map((item, index) => ({
    label: item.label,
    netAmount: item.netAmount,
    sharePercent: Math.round((item.netAmount / total) * 1000) / 10,
    color: SLICE[index % SLICE.length],
  }));
  if (other > 0) {
    slices.push({
      label: otherLabel,
      netAmount: other,
      sharePercent: Math.round((other / total) * 1000) / 10,
      color: SLICE[5],
    });
  }
  return slices;
}

export function ProcurementGrnPage() {
  const { t } = useTranslation('reports', { keyPrefix: 'procurementHub' });
  const { t: tg } = useTranslation('reports', { keyPrefix: 'groupBy' });
  const canExport = useCanReportsExport();

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [groupBy, setGroupBy] = useState<GroupBy>('day');
  const [warehouseId, setWarehouseId] = useState<string>();
  const [supplierId, setSupplierId] = useState<string>();
  const [tableQuery, setTableQuery] = useState('');
  const [hiddenCols, setHiddenCols] = useState<string[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(false);
  const [period, setPeriod] = useState<ReportTableResult | null>(null);
  const [bySupplier, setBySupplier] = useState<ReportTableResult | null>(null);
  const [documents, setDocuments] = useState<ReportTableResult | null>(null);

  useEffect(() => {
    void Promise.all([fetchWarehouses(), fetchSuppliers(true)])
      .then(([wh, sup]) => {
        setWarehouses(wh);
        setSuppliers(sup);
      })
      .catch(() => undefined);
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    const current = toIsoRange(range);
    const extra = {
      ...current,
      ...(warehouseId ? { warehouseId } : {}),
      ...(supplierId ? { supplierId } : {}),
    };
    try {
      const [periodRes, supplierRes, docsRes] = await Promise.all([
        runReport('procurement/grn-value', { ...extra, groupBy }),
        runReport('procurement/grn-value', { ...extra, groupBy: 'supplier' }),
        runReport('procurement/grn-documents', { ...extra, groupBy }).catch(() => null),
      ]);
      setPeriod(periodRes);
      setBySupplier(supplierRes);
      setDocuments(docsRes);
    } finally {
      setLoading(false);
    }
  }, [range, groupBy, warehouseId, supplierId]);

  useEffect(() => {
    void load();
  }, [load]);

  const resetFilters = () => {
    setRange(defaultRange());
    setGroupBy('day');
    setWarehouseId(undefined);
    setSupplierId(undefined);
    setTableQuery('');
    setHiddenCols([]);
  };

  const periodRows = useMemo<PeriodRow[]>(
    () =>
      (period?.rows ?? []).map((row, index) => ({
        key: `${readReportFieldString(row, 'periodLabel')}-${index}`,
        label: readReportFieldString(row, 'periodLabel'),
        grnCount: readReportFieldNumber(row, 'grnCount'),
        totalQty: readReportFieldNumber(row, 'totalQty'),
        preTaxAmount: readReportFieldNumber(row, 'preTaxAmount'),
      })),
    [period],
  );

  const documentRows = useMemo<DocumentRow[]>(
    () =>
      (documents?.rows ?? []).map((row, index) => ({
        key: readReportFieldString(row, 'grnId') || `grn-${index}`,
        grnId: readReportFieldString(row, 'grnId'),
        periodLabel: readReportFieldString(row, 'periodLabel'),
        grnNumber: readReportFieldString(row, 'grnNumber'),
        receiptDate: readReportFieldString(row, 'receiptDate'),
        supplierName: readReportFieldString(row, 'supplierName'),
        warehouseName: readReportFieldString(row, 'warehouseName'),
        totalQty: readReportFieldNumber(row, 'totalQty'),
        preTaxAmount: readReportFieldNumber(row, 'preTaxAmount'),
        taxAmount: readReportFieldNumber(row, 'taxAmount'),
        totalAmount: readReportFieldNumber(row, 'totalAmount'),
        status: readReportFieldNumber(row, 'status'),
        statusLabel: readReportFieldString(row, 'statusLabel') || GRN_STATUS_LABELS[readReportFieldNumber(row, 'status')] || '—',
      })),
    [documents],
  );

  const filteredDocuments = useMemo(() => {
    const q = tableQuery.trim().toLowerCase();
    if (!q) return documentRows;
    return documentRows.filter(
      (row) =>
        row.grnNumber.toLowerCase().includes(q) ||
        row.supplierName.toLowerCase().includes(q) ||
        row.warehouseName.toLowerCase().includes(q),
    );
  }, [documentRows, tableQuery]);

  const filteredPeriods = useMemo(() => {
    const q = tableQuery.trim().toLowerCase();
    if (!q) return periodRows;
    return periodRows.filter((row) => row.label.toLowerCase().includes(q));
  }, [periodRows, tableQuery]);

  const useDocuments = true;
  const value = period?.totals ? readReportFieldNumber(period.totals, 'preTaxAmount') : 0;
  const grnCount = period?.totals ? readReportFieldNumber(period.totals, 'grnCount') : 0;
  const qty = period?.totals ? readReportFieldNumber(period.totals, 'totalQty') : 0;
  const supplierCount = (bySupplier?.rows ?? []).length;
  const maxValue = Math.max(...periodRows.map((row) => row.preTaxAmount), 1);
  const ticks = axisTicks(maxValue);
  const axisMax = ticks[ticks.length - 1] || maxValue;
  const slices = useMemo(
    () => buildSupplierSlices(bySupplier?.rows ?? [], t('other')),
    [bySupplier, t],
  );

  const documentCols = useMemo(
    () => [
      { key: 'periodLabel', title: t('cols.period'), align: 'left' as const },
      { key: 'grnNumber', title: t('cols.grn'), align: 'left' as const },
      { key: 'receiptDate', title: t('cols.date'), align: 'left' as const },
      { key: 'supplierName', title: t('cols.supplier'), align: 'left' as const },
      { key: 'warehouseName', title: t('cols.warehouse'), align: 'left' as const },
      { key: 'totalQty', title: t('cols.qty'), align: 'right' as const },
      { key: 'preTaxAmount', title: t('cols.preTax'), align: 'right' as const },
      { key: 'taxAmount', title: t('cols.tax'), align: 'right' as const },
      { key: 'totalAmount', title: t('cols.total'), align: 'right' as const },
      { key: 'statusLabel', title: t('cols.status'), align: 'left' as const },
      { key: 'action', title: t('cols.action'), align: 'right' as const },
    ],
    [t],
  );

  const periodCols = useMemo(
    () => [
      { key: 'label', title: t('cols.period'), align: 'left' as const },
      { key: 'grnCount', title: t('cols.grnCount'), align: 'right' as const },
      { key: 'totalQty', title: t('cols.qty'), align: 'right' as const },
      { key: 'preTaxAmount', title: t('cols.preTax'), align: 'right' as const },
    ],
    [t],
  );

  const columnDefs = useDocuments ? documentCols : periodCols;

  const documentColumns: ColumnsType<DocumentRow> = documentCols
    .filter((col) => !hiddenCols.includes(col.key))
    .map((col) => ({
      title: col.title,
      dataIndex: col.key,
      key: col.key,
      align: col.align,
      render: (_v: unknown, row: DocumentRow) => {
        if (col.key === 'receiptDate') return formatDisplayDate(row.receiptDate);
        if (col.key === 'totalQty') return formatDisplayQuantity(row.totalQty);
        if (col.key === 'preTaxAmount' || col.key === 'taxAmount' || col.key === 'totalAmount') {
          return formatDisplayMoney(Number(row[col.key]));
        }
        if (col.key === 'statusLabel') {
          return <Tag color={GRN_STATUS_TAG[row.status] ?? 'default'}>{row.statusLabel}</Tag>;
        }
        if (col.key === 'action') {
          return (
            <Link to="/procurement/goods-receipts">{t('open')}</Link>
          );
        }
        return String(row[col.key as keyof DocumentRow] ?? '—');
      },
    }));

  const periodColumns: ColumnsType<PeriodRow> = periodCols
    .filter((col) => !hiddenCols.includes(col.key))
    .map((col) => ({
      title: col.title,
      dataIndex: col.key,
      key: col.key,
      align: col.align,
      render: (_v: unknown, row: PeriodRow) => {
        if (col.key === 'grnCount') return row.grnCount.toLocaleString('vi-VN');
        if (col.key === 'totalQty') return formatDisplayQuantity(row.totalQty);
        if (col.key === 'preTaxAmount') return formatDisplayMoney(row.preTaxAmount);
        return row.label;
      },
    }));

  const exportResult = useMemo<ReportTableResult | null>(() => {
    const source = documents ?? period;
    if (!source) return null;
    const columns: ReportColumn[] = columnDefs
      .filter((col) => col.key !== 'action')
      .map((col) => ({
        key: col.key,
        title: col.title,
        format: 'text',
        align: col.align,
      }));
    return { ...source, columns };
  }, [useDocuments, documents, period, columnDefs]);

  const kpis = [
    { key: 'value' as Tone, label: t('kpi.value'), value: formatDisplayMoney(value), hint: t('kpi.preTaxHint', { count: grnCount }), icon: <ShoppingCartOutlined /> },
    { key: 'grn' as Tone, label: t('kpi.grn'), value: grnCount.toLocaleString('vi-VN'), hint: t('kpi.grnHint'), icon: <FileTextOutlined /> },
    { key: 'qty' as Tone, label: t('kpi.qty'), value: formatDisplayQuantity(qty), hint: t('kpi.qtyHint'), icon: <InboxOutlined /> },
    { key: 'suppliers' as Tone, label: t('kpi.suppliers'), value: supplierCount.toLocaleString('vi-VN'), hint: t('kpi.suppliersHint'), icon: <TeamOutlined /> },
  ];

  return (
    <div className="proc-grn">
      <div className="proc-grn__head">
        <div>
          <Typography.Title level={3} className="proc-grn__title">
            {t('title')}
            <span className="proc-grn__code">PROC-01</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="proc-grn__desc">
            {t('subtitle')}
          </Typography.Paragraph>
        </div>
        {canExport && exportResult ? (
          <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(exportResult)}>
            {t('export')}
          </Button>
        ) : null}
      </div>

      <div className="proc-grn__toolbar">
        <label className="proc-grn__field">
          <span>{t('period')}</span>
          <RangePicker
            value={range}
            allowClear={false}
            format="DD/MM/YYYY"
            onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
          />
        </label>
        <label className="proc-grn__field">
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
        <label className="proc-grn__field">
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
        <label className="proc-grn__field">
          <span>{t('supplier')}</span>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 200 }}
            placeholder={t('supplierAll')}
            value={supplierId}
            onChange={setSupplierId}
            options={suppliers.map((s) => ({ value: s.id, label: `${s.supplierCode} — ${s.supplierName}` }))}
          />
        </label>
        <Button type="primary" icon={<ReloadOutlined />} loading={loading} onClick={() => void load()}>
          {t('run')}
        </Button>
        <Button onClick={resetFilters}>{t('reset')}</Button>
      </div>

      <Spin spinning={loading}>
        <div className="proc-grn__kpis">
          {kpis.map((kpi) => (
            <div key={kpi.key} className={`proc-grn__kpi proc-grn__kpi--${kpi.key}`}>
              <span className="proc-grn__kpi-icon">{kpi.icon}</span>
              <span>
                <span className="proc-grn__kpi-label">{kpi.label}</span>
                <span className="proc-grn__kpi-value">{kpi.value}</span>
                <span className="proc-grn__hint">{kpi.hint}</span>
              </span>
            </div>
          ))}
        </div>

        <div className="proc-grn__charts">
          <section className="proc-grn__panel">
            <div className="proc-grn__panel-head">
              <span className="proc-grn__panel-icon">
                <BarChartOutlined />
              </span>
              <h3>{t('charts.time')}</h3>
              <span className="proc-grn__legend">
                <i />
                {t('charts.bar')}
              </span>
            </div>
            {periodRows.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('emptyChart')} />
            ) : (
              <div className="proc-grn__chart">
                <div className="proc-grn__axis">
                  {[...ticks].reverse().map((tick) => (
                    <span key={tick}>{compactMoney(tick)}</span>
                  ))}
                </div>
                <div className="proc-grn__bars" style={{ gridTemplateColumns: `repeat(${periodRows.length}, minmax(18px, 1fr))` }}>
                  {periodRows.map((row) => (
                    <div key={row.key} className="proc-grn__bar-col" title={`${row.label}: ${formatDisplayMoney(row.preTaxAmount)}`}>
                      <span className="proc-grn__bar" style={{ height: `${Math.max(4, (row.preTaxAmount / axisMax) * 168)}px` }} />
                      <span className="proc-grn__bar-name">{row.label.slice(0, 5)}</span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
          <section className="proc-grn__panel">
            <div className="proc-grn__panel-head">
              <span className="proc-grn__panel-icon">
                <PieChartOutlined />
              </span>
              <h3>{t('charts.supplier')}</h3>
              <span className="proc-grn__muted">{t('charts.top5')}</span>
            </div>
            {slices.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('emptyDonut')} />
            ) : (
              <div className="proc-grn__donut-wrap">
                <div className="proc-grn__donut" style={{ background: buildConicGradient(slices) }}>
                  <div className="proc-grn__donut-hole">
                    <strong>{compactMoney(value)}</strong>
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

        <section className="proc-grn__panel">
          <div className="proc-grn__panel-head">
            <span className="proc-grn__panel-icon">
              <AppstoreOutlined />
            </span>
            <h3>{t('table.title')}</h3>
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder={t('table.search')}
              value={tableQuery}
              onChange={(e) => setTableQuery(e.target.value)}
              style={{ width: 240, marginLeft: 'auto' }}
            />
            <Dropdown
              trigger={['click']}
              popupRender={() => (
                <div className="proc-grn__cols">
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
            {canExport && exportResult ? (
              <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(exportResult)}>
                {t('export')}
              </Button>
            ) : null}
          </div>
          {useDocuments ? (
            <Table<DocumentRow>
              rowKey="key"
              size="middle"
              columns={documentColumns}
              dataSource={filteredDocuments}
              pagination={{
                pageSize: 10,
                showSizeChanger: true,
                pageSizeOptions: ['10', '20', '50'],
                showTotal: (n) => t('rows', { count: n }),
              }}
              locale={{ emptyText: t('empty') }}
              scroll={{ x: true }}
            />
          ) : (
            <Table<PeriodRow>
              rowKey="key"
              size="middle"
              columns={periodColumns}
              dataSource={filteredPeriods}
              pagination={{ pageSize: 10, showSizeChanger: true, pageSizeOptions: ['10', '20', '50'] }}
              locale={{ emptyText: t('empty') }}
              scroll={{ x: true }}
            />
          )}
        </section>
      </Spin>
    </div>
  );
}
