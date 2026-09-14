import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Button,
  Checkbox,
  Dropdown,
  Empty,
  Input,
  Select,
  Spin,
  Table,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import {
  AlertOutlined,
  AppstoreOutlined,
  DownloadOutlined,
  FilterOutlined,
  InboxOutlined,
  PieChartOutlined,
  ReloadOutlined,
  SearchOutlined,
  SettingOutlined,
  WarningOutlined,
} from '@ant-design/icons';
import dayjs from 'dayjs';
import { fetchCategoryLookups } from '@/shared/api/catalog.api';
import type { LookupItem } from '@/shared/api/catalog.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportColumn, ReportTableResult } from '@/shared/api/reports.types';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { formatDisplayDateTime } from '@/shared/utils/date';
import { formatDisplayMoney, formatDisplayQuantity } from '@/shared/utils/money';
import { exportReportCsv } from '@/modules/reports/report-export';
import {
  buildCategoryChartSlices,
  buildConicGradient,
} from '@/modules/dashboard/dashboard-category-chart';
import {
  readReportFieldNumber,
  readReportFieldString,
} from '@/modules/dashboard/dashboard-revenue-range';
import './inventory-stock.css';

type StockStatus = 'ok' | 'soon' | 'expired';
type AdvancedFilter = 'all' | 'near' | 'expired' | 'stale';
type Tone = 'value' | 'qty' | 'sku' | 'alert';

type StockRow = {
  key: string;
  productCode: string;
  productName: string;
  categoryLabel: string;
  warehouseName: string;
  unitName: string;
  updatedAt: string;
  totalQty: number;
  stockValue: number;
  status: StockStatus;
  stale: boolean;
};

function compactMoney(value: number): string {
  if (Math.abs(value) >= 1_000_000_000) return `${(value / 1_000_000_000).toFixed(1)} tỷ`;
  if (Math.abs(value) >= 1_000_000) return `${(value / 1_000_000).toFixed(1)} tr`;
  return formatDisplayMoney(value);
}

function parseDay(value: unknown) {
  const raw = String(value ?? '');
  const parsed = dayjs(raw);
  return parsed.isValid() ? parsed.startOf('day') : null;
}

function rowKey(code: string, warehouse: string) {
  return `${code}::${warehouse}`;
}

export function InventoryStockPage() {
  const { t } = useTranslation('reports', { keyPrefix: 'inventoryHub' });
  const canExport = useCanReportsExport();

  const [warehouseId, setWarehouseId] = useState<string>();
  const [categoryId, setCategoryId] = useState<string>();
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [advanced, setAdvanced] = useState<AdvancedFilter>('all');
  const [tableQuery, setTableQuery] = useState('');
  const [hiddenCols, setHiddenCols] = useState<string[]>([]);
  const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [categories, setCategories] = useState<LookupItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [stock, setStock] = useState<ReportTableResult | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [expiryRows, setExpiryRows] = useState<Record<string, unknown>[]>([]);
  const [staleKeys, setStaleKeys] = useState<Set<string>>(new Set());

  useEffect(() => {
    void Promise.all([
      fetchWarehouses().catch(() => [] as Warehouse[]),
      fetchCategoryLookups().catch(() => [] as LookupItem[]),
    ]).then(([wh, cats]) => {
      setWarehouses(wh);
      setCategories(cats);
    });
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    const extra: Record<string, string> = {};
    if (warehouseId) extra.warehouseId = warehouseId;
    if (categoryId) extra.categoryId = categoryId;
    if (search) extra.search = search;
    const from = dayjs().subtract(59, 'day').startOf('day').toISOString();
    const to = dayjs().add(1, 'day').startOf('day').toISOString();
    try {
      setLoadError(null);
      const [stockRes, expiryRes, movementRes] = await Promise.all([
        runReport('inventory/stock-snapshot', extra).catch(async () => {
          await new Promise((resolve) => setTimeout(resolve, 400));
          return runReport('inventory/stock-snapshot', extra);
        }),
        runReport('inventory/near-expiry', { ...extra, expiryDays: 365 }).catch(() => null),
        runReport('inventory/movement-summary', { ...extra, from, to }).catch(() => null),
      ]);
      setStock(stockRes);
      setExpiryRows(expiryRes?.rows ?? []);
      const stale = new Set<string>();
      for (const row of movementRes?.rows ?? []) {
        const inQty = readReportFieldNumber(row, 'inQty');
        const outQty = readReportFieldNumber(row, 'outQty');
        const closing = readReportFieldNumber(row, 'closingQty');
        if (closing > 0 && inQty === 0 && outQty === 0) {
          stale.add(rowKey(readReportFieldString(row, 'productCode'), readReportFieldString(row, 'warehouseName')));
        }
      }
      setStaleKeys(stale);
      setSelectedKeys([]);
    } catch {
      setStock(null);
      setExpiryRows([]);
      setStaleKeys(new Set());
      setLoadError(t('loadError'));
    } finally {
      setLoading(false);
    }
  }, [warehouseId, categoryId, search, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const today = dayjs().startOf('day');
  const expiryByKey = useMemo(() => {
    const map = new Map<string, { expired: boolean; near7: boolean; near30: boolean }>();
    for (const row of expiryRows) {
      const expiry = parseDay(row.expiryDate ?? row.ExpiryDate);
      if (!expiry) continue;
      const key = rowKey(readReportFieldString(row, 'productCode'), readReportFieldString(row, 'warehouseName'));
      const current = map.get(key) ?? { expired: false, near7: false, near30: false };
      if (expiry.isBefore(today)) current.expired = true;
      else if (!expiry.isAfter(today.add(7, 'day'))) current.near7 = true;
      else if (!expiry.isAfter(today.add(30, 'day'))) current.near30 = true;
      map.set(key, current);
    }
    return map;
  }, [expiryRows, today]);

  const rows = useMemo<StockRow[]>(
    () =>
      (stock?.rows ?? []).map((row, index) => {
        const productCode = readReportFieldString(row, 'productCode');
        const warehouseName = readReportFieldString(row, 'warehouseName');
        const key = rowKey(productCode, warehouseName) || `row-${index}`;
        const flag = expiryByKey.get(key);
        const status: StockStatus = flag?.expired ? 'expired' : flag?.near7 || flag?.near30 ? 'soon' : 'ok';
        return {
          key,
          productCode,
          productName: readReportFieldString(row, 'productName'),
          categoryLabel: readReportFieldString(row, 'categoryLabel') || '—',
          warehouseName,
          unitName: readReportFieldString(row, 'unitName') || '—',
          updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
          totalQty: readReportFieldNumber(row, 'totalQty'),
          stockValue: readReportFieldNumber(row, 'stockValue'),
          status,
          stale: staleKeys.has(key),
        };
      }),
    [stock, expiryByKey, staleKeys],
  );

  const filteredRows = useMemo(() => {
    const q = tableQuery.trim().toLowerCase();
    return rows.filter((row) => {
      if (advanced === 'near' && row.status !== 'soon') return false;
      if (advanced === 'expired' && row.status !== 'expired') return false;
      if (advanced === 'stale' && !row.stale) return false;
      if (!q) return true;
      return (
        row.productCode.toLowerCase().includes(q) ||
        row.productName.toLowerCase().includes(q) ||
        row.categoryLabel.toLowerCase().includes(q) ||
        row.warehouseName.toLowerCase().includes(q)
      );
    });
  }, [rows, tableQuery, advanced]);

  const value = rows.reduce((sum, row) => sum + row.stockValue, 0);
  const qty = rows.reduce((sum, row) => sum + row.totalQty, 0);
  const skuCount = new Set(rows.map((row) => row.productCode)).size;
  const nearCount = new Set(rows.filter((row) => row.status === 'soon' || row.status === 'expired').map((r) => r.productCode)).size;

  const warehouseBars = useMemo(() => {
    const map = new Map<string, number>();
    for (const row of rows) map.set(row.warehouseName, (map.get(row.warehouseName) ?? 0) + row.stockValue);
    const list = [...map.entries()].map(([name, net]) => ({ name, net })).sort((a, b) => b.net - a.net);
    const max = Math.max(...list.map((item) => item.net), 1);
    return { list, max };
  }, [rows]);

  const slices = useMemo(
    () =>
      buildCategoryChartSlices(
        rows.map((row) => ({ categoryLabel: row.categoryLabel, netAmount: row.stockValue })),
        t('uncategorized'),
        7,
      ),
    [rows, t],
  );

  const alerts = useMemo(() => {
    const expired = new Set<string>();
    const d7 = new Set<string>();
    const d30 = new Set<string>();
    for (const [key, flag] of expiryByKey) {
      if (flag.expired) expired.add(key);
      if (flag.near7) d7.add(key);
      if (flag.near7 || flag.near30) d30.add(key);
    }
    return [
      { key: 'd7', label: t('alerts.d7'), count: d7.size, tone: 'warn' as const },
      { key: 'd30', label: t('alerts.d30'), count: d30.size, tone: 'warn' as const },
      { key: 'expired', label: t('alerts.expired'), count: expired.size, tone: 'danger' as const },
      { key: 'stale', label: t('alerts.idle'), count: staleKeys.size, tone: 'muted' as const },
    ];
  }, [expiryByKey, staleKeys, t]);

  const columnDefs = useMemo(
    () => [
      { key: 'productCode', title: t('cols.code') },
      { key: 'productName', title: t('cols.name') },
      { key: 'categoryLabel', title: t('cols.group') },
      { key: 'warehouseName', title: t('cols.warehouse') },
      { key: 'totalQty', title: t('cols.qty') },
      { key: 'unitName', title: t('cols.unit') },
      { key: 'stockValue', title: t('cols.value') },
      { key: 'status', title: t('cols.status') },
      { key: 'updatedAt', title: t('cols.updated') },
    ],
    [t],
  );

  const columns: ColumnsType<StockRow> = useMemo(
    () =>
      columnDefs
        .filter((col) => !hiddenCols.includes(col.key))
        .map((col) => ({
          title: col.title,
          dataIndex: col.key,
          key: col.key,
          align: col.key === 'totalQty' || col.key === 'stockValue' ? 'right' : 'left',
          render: (_: unknown, row: StockRow) => {
            if (col.key === 'totalQty') return formatDisplayQuantity(row.totalQty);
            if (col.key === 'stockValue') return formatDisplayMoney(row.stockValue);
            if (col.key === 'updatedAt') return formatDisplayDateTime(row.updatedAt);
            if (col.key === 'status') {
              return (
                <span className={`inv-stock__status inv-stock__status--${row.status}`}>
                  {t(`status.${row.status}`)}
                </span>
              );
            }
            return String(row[col.key as keyof StockRow] ?? '—');
          },
        })),
    [columnDefs, hiddenCols, t],
  );

  const exportResult = useMemo<ReportTableResult | null>(() => {
    if (!stock) return null;
    const source = selectedKeys.length ? filteredRows.filter((row) => selectedKeys.includes(row.key)) : filteredRows;
    const columns: ReportColumn[] = columnDefs.map((col) => ({
      key: col.key,
      title: col.title,
      format: 'text',
      align: col.key === 'totalQty' || col.key === 'stockValue' ? 'right' : 'left',
    }));
    return {
      ...stock,
      columns,
      rows: source.map((row) => ({
        ...row,
        totalQty: formatDisplayQuantity(row.totalQty),
        stockValue: formatDisplayMoney(row.stockValue),
        status: t(`status.${row.status}`),
        updatedAt: formatDisplayDateTime(row.updatedAt),
      })),
      totals: null,
    };
  }, [stock, selectedKeys, filteredRows, columnDefs, t]);

  const kpis = [
    {
      key: 'value' as Tone,
      label: t('kpi.value'),
      value: Math.abs(value) >= 1_000_000_000 ? compactMoney(value) : formatDisplayMoney(value),
      icon: <InboxOutlined />,
    },
    { key: 'qty' as Tone, label: t('kpi.qty'), value: formatDisplayQuantity(qty), icon: <AppstoreOutlined /> },
    { key: 'sku' as Tone, label: t('kpi.sku'), value: skuCount.toLocaleString('vi-VN'), icon: <AppstoreOutlined /> },
    { key: 'alert' as Tone, label: t('kpi.expiry'), value: nearCount.toLocaleString('vi-VN'), icon: <WarningOutlined /> },
  ];

  return (
    <div className="inv-stock">
      <div className="inv-stock__head">
        <div>
          <Typography.Title level={3} className="inv-stock__title">
            {t('title')}
            <span className="inv-stock__code">INV-01</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="inv-stock__desc">
            {t('subtitle')}
          </Typography.Paragraph>
          {value > 0 ? (
            <Typography.Paragraph type="secondary" className="inv-stock__desc">
              {t('kpi.valueNote')}
            </Typography.Paragraph>
          ) : null}
        </div>
        {canExport && exportResult ? (
          <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(exportResult)}>
            {t('export')}
          </Button>
        ) : null}
      </div>

      <div className="inv-stock__toolbar">
        <label className="inv-stock__field">
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
        <label className="inv-stock__field">
          <span>{t('category')}</span>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            style={{ minWidth: 200 }}
            placeholder={t('categoryAll')}
            value={categoryId}
            onChange={setCategoryId}
            options={categories.map((c) => ({ value: c.id, label: c.name }))}
          />
        </label>
        <label className="inv-stock__field inv-stock__field--grow">
          <span>{t('search')}</span>
          <Input
            allowClear
            prefix={<SearchOutlined />}
            placeholder={t('searchPh')}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            onPressEnter={() => setSearch(searchInput.trim())}
          />
        </label>
        <label className="inv-stock__field">
          <span>{t('advanced')}</span>
          <Select<AdvancedFilter>
            style={{ minWidth: 180 }}
            value={advanced}
            onChange={setAdvanced}
            suffixIcon={<FilterOutlined />}
            options={[
              { value: 'all', label: t('filters.all') },
              { value: 'near', label: t('filters.near') },
              { value: 'expired', label: t('filters.expired') },
              { value: 'stale', label: t('filters.stale') },
            ]}
          />
        </label>
        <Button
          type="primary"
          icon={<ReloadOutlined />}
          loading={loading}
          onClick={() => {
            setSearch(searchInput.trim());
            void load();
          }}
        >
          {t('run')}
        </Button>
      </div>

      {loadError ? (
        <Alert type="error" showIcon message={loadError} style={{ marginBottom: 12 }} />
      ) : null}

      <Spin spinning={loading}>
        <div className="inv-stock__kpis">
          {kpis.map((kpi) => (
            <div key={kpi.key} className={`inv-stock__kpi inv-stock__kpi--${kpi.key}`}>
              <span className="inv-stock__kpi-icon">{kpi.icon}</span>
              <span>
                <span className="inv-stock__kpi-label">{kpi.label}</span>
                <span className="inv-stock__kpi-value">{kpi.value}</span>
                <span className="inv-stock__kpi-hint">{t('asOf')}</span>
              </span>
            </div>
          ))}
        </div>

        <div className="inv-stock__charts">
          <section className="inv-stock__panel">
            <div className="inv-stock__panel-head">
              <span className="inv-stock__panel-icon">
                <InboxOutlined />
              </span>
              <h3>{t('charts.warehouse')}</h3>
              <span className="inv-stock__muted">{t('charts.byValue')}</span>
            </div>
            {warehouseBars.list.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              warehouseBars.list.map((item) => (
                <div key={item.name} className="inv-stock__hbar">
                  <span className="inv-stock__hbar-name" title={item.name}>
                    {item.name}
                  </span>
                  <span className="inv-stock__hbar-track">
                    <span
                      className="inv-stock__hbar-fill"
                      style={{ width: `${Math.max(4, (item.net / warehouseBars.max) * 100)}%` }}
                    />
                  </span>
                  <span className="inv-stock__hbar-val">{compactMoney(item.net)}</span>
                </div>
              ))
            )}
          </section>
          <section className="inv-stock__panel">
            <div className="inv-stock__panel-head">
              <span className="inv-stock__panel-icon">
                <PieChartOutlined />
              </span>
              <h3>{t('charts.category')}</h3>
            </div>
            {slices.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={t('empty')} />
            ) : (
              <div className="inv-stock__donut-wrap">
                <div className="inv-stock__donut" style={{ background: buildConicGradient(slices) }}>
                  <div className="inv-stock__donut-hole">
                    <strong>{compactMoney(value)}</strong>
                    <span>{t('charts.totalValue')}</span>
                  </div>
                </div>
                <ul className="inv-stock__cat-list">
                  {slices.map((slice) => (
                    <li key={slice.label}>
                      <span>
                        <i style={{ background: slice.color }} />
                        {slice.label}
                      </span>
                      <b>
                        {slice.sharePercent}%
                      </b>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>
          <section className="inv-stock__panel">
            <div className="inv-stock__panel-head">
              <span className="inv-stock__panel-icon">
                <AlertOutlined />
              </span>
              <h3>{t('charts.alerts')}</h3>
              <Link to="/reports/inventory/near-expiry" className="inv-stock__more">
                {t('viewAll')}
              </Link>
            </div>
            <div className="inv-stock__alerts">
              {alerts.map((item) => (
                <div key={item.key} className={`inv-stock__alert inv-stock__alert--${item.tone}`}>
                  <span>{item.label}</span>
                  <b>{item.count.toLocaleString('vi-VN')}</b>
                </div>
              ))}
            </div>
          </section>
        </div>

        <section className="inv-stock__panel inv-stock__table-panel">
          <div className="inv-stock__panel-head">
            <span className="inv-stock__panel-icon">
              <AppstoreOutlined />
            </span>
            <h3>{t('table.title')}</h3>
            <span className="inv-stock__muted">{t('table.count', { count: filteredRows.length })}</span>
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
                <div className="inv-stock__cols">
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
          <Table<StockRow>
            rowKey="key"
            size="middle"
            columns={columns}
            dataSource={filteredRows}
            rowSelection={{
              selectedRowKeys: selectedKeys,
              onChange: (keys) => setSelectedKeys(keys.map(String)),
            }}
            pagination={{ pageSize: 10, showSizeChanger: false, showTotal: (n) => t('rows', { count: n }) }}
            locale={{ emptyText: t('empty') }}
            scroll={{ x: true }}
          />
        </section>
      </Spin>
    </div>
  );
}
