import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import {
  Alert,
  AutoComplete,
  Button,
  Card,
  DatePicker,
  Empty,
  Input,
  InputNumber,
  Segmented,
  Select,
  Table,
  Typography,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, PrinterOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { fetchSuppliers } from '@/shared/api/procurement.api';
import type { Supplier } from '@/shared/api/procurement.types';
import { fetchEmployees } from '@/shared/api/identity-admin.api';
import type { EmployeeLookup } from '@/shared/api/identity-admin.types';
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportTableResult } from '@/shared/api/reports.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { findReportByPath } from '@/modules/reports/reports-catalog';
import { buildReportFilterDisplayEntries, filterHintsForReport } from '@/modules/reports/report-filter-ui';
import { exportReportCsv, formatReportCell, printReportElement } from '@/modules/reports/report-export';
import { useCanReportsExport } from '@/shared/auth/usePermission';
import { useAuditSlimNav } from '@/shared/platform/audit-slim-nav';
import { useTenantPlatformStore } from '@/shared/platform/tenant-platform.store';
import './report-view.css';

const { RangePicker } = DatePicker;

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('month'), dayjs().endOf('day')];
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

function staffReportQuery(opts: {
  employeeId?: string;
  from?: Dayjs;
  to?: Dayjs;
  warehouseId?: string;
}): string {
  const q = new URLSearchParams();
  if (opts.employeeId) q.set('employeeId', opts.employeeId);
  if (opts.from) q.set('from', opts.from.format('YYYY-MM-DD'));
  if (opts.to) q.set('to', opts.to.format('YYYY-MM-DD'));
  if (opts.warehouseId) q.set('warehouseId', opts.warehouseId);
  const qs = q.toString();
  return qs ? `?${qs}` : '';
}

function asNumber(value: unknown): number {
  const n = Number(value);
  return Number.isFinite(n) ? n : 0;
}

function resolveProductSearchTerm(input: string, products: ProductListItem[]): { term?: string; label?: string } {
  const raw = input.trim();
  if (!raw) return {};
  const selected = products.find((p) => raw.startsWith(`${p.productCode} —`));
  if (selected) {
    return { term: selected.productCode, label: `${selected.productCode} — ${selected.productName}` };
  }
  const exact = products.find(
    (p) =>
      p.productCode.toLowerCase() === raw.toLowerCase() || p.productName.toLowerCase() === raw.toLowerCase(),
  );
  if (exact) {
    return { term: exact.productCode, label: `${exact.productCode} — ${exact.productName}` };
  }
  return { term: raw, label: raw };
}

export function ReportViewPage() {
  const { t, i18n } = useTranslation('reports', { keyPrefix: 'view' });
  const { t: tg } = useTranslation('reports', { keyPrefix: 'groupBy' });
  const location = useLocation();
  const navigate = useNavigate();
  const auditSlimNav = useAuditSlimNav();
  const connectEnabled = useTenantPlatformStore(
    (s) => s.loaded && s.isModuleEnabled('novixa_connect'),
  );
  const canExportReports = useCanReportsExport();
  const [searchParams, setSearchParams] = useSearchParams();
  const definition = useMemo(
    () => findReportByPath(location.pathname, { auditSlimNav, connectEnabled }),
    [location.pathname, i18n.language, auditSlimNav, connectEnabled],
  );
  const isStaffReport = definition?.code === 'SALES-06' || definition?.code === 'SALES-07';
  const isStaffProduct = definition?.code === 'SALES-07';

  const [range, setRange] = useState<[Dayjs, Dayjs]>(() => rangeFromSearch(searchParams));
  const [groupBy, setGroupBy] = useState<string>('day');
  const [warehouseId, setWarehouseId] = useState<string | undefined>(
    () => searchParams.get('warehouseId') || undefined,
  );
  const [employeeId, setEmployeeId] = useState<string | undefined>(
    () => searchParams.get('employeeId') || undefined,
  );
  const [employees, setEmployees] = useState<EmployeeLookup[]>([]);
  const [supplierId, setSupplierId] = useState<string>();
  const [searchInput, setSearchInput] = useState('');
  const [suggestionProducts, setSuggestionProducts] = useState<ProductListItem[]>([]);
  const [expiryDays, setExpiryDays] = useState(30);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ReportTableResult | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadedOnce, setLoadedOnce] = useState(false);

  useEffect(() => {
    if (!definition) return;
    if (definition.supportsGroupBy?.[0]) setGroupBy(definition.supportsGroupBy[0]);
    void (async () => {
      try {
        const wh = await fetchWarehouses();
        setWarehouses(wh);
      } catch {
        /* optional */
      }
      if (definition.supportsSupplier) {
        try {
          setSuppliers(await fetchSuppliers(true));
        } catch {
          /* optional */
        }
      }
      if (definition.supportsSearch) {
        try {
          const catalog = await fetchProducts({ page: 1, pageSize: 200 });
          setSuggestionProducts(catalog.items ?? []);
        } catch {
          /* optional */
        }
      }
      if (definition.supportsEmployee) {
        try {
          setEmployees(await fetchEmployees());
        } catch {
          /* optional */
        }
      }
    })();
  }, [definition]);

  const load = useCallback(async () => {
    if (!definition) return;
    setLoading(true);
    setLoadError(null);
    try {
      const params: Record<string, string | number> = {};
      if (definition.supportsDateRange) {
        params.from = range[0].startOf('day').toISOString();
        params.to = range[1].add(1, 'day').startOf('day').toISOString();
      }
      if (definition.supportsGroupBy?.length) params.groupBy = groupBy;
      if (definition.supportsWarehouse && warehouseId) params.warehouseId = warehouseId;
      if (definition.supportsEmployee && employeeId) params.employeeId = employeeId;
      if (definition.supportsSupplier && supplierId) params.supplierId = supplierId;
      if (definition.supportsSearch) {
        const { term } = resolveProductSearchTerm(searchInput, suggestionProducts);
        if (term) params.search = term;
      }
      if (definition.supportsExpiryDays) params.expiryDays = expiryDays;
      setResult(await runReport(definition.apiPath, params));
      setLoadedOnce(true);
    } catch (error) {
      setResult(null);
      const msg = apiErrorMessage(error, t('loadFailed'));
      setLoadError(msg);
      message.error(msg);
    } finally {
      setLoading(false);
    }
  }, [definition, range, groupBy, warehouseId, employeeId, supplierId, searchInput, suggestionProducts, expiryDays, t]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    const fromUrl = searchParams.get('employeeId') || undefined;
    setEmployeeId((current) => (current === fromUrl ? current : fromUrl));
  }, [searchParams]);

  const goStaffView = useCallback(
    (view: 'summary' | 'product', nextEmployeeId = employeeId) => {
      const qs = staffReportQuery({
        employeeId: nextEmployeeId,
        from: range[0],
        to: range[1],
        warehouseId,
      });
      navigate(
        view === 'product'
          ? `/reports/sales/revenue-by-employee-product${qs}`
          : `/reports/sales/revenue-by-employee${qs}`,
      );
    },
    [employeeId, navigate, range, warehouseId],
  );

  const syncEmployeeParam = (id?: string) => {
    setEmployeeId(id);
    const next = new URLSearchParams(searchParams);
    if (id) next.set('employeeId', id);
    else next.delete('employeeId');
    setSearchParams(next, { replace: true });
  };

  const visibleResultColumns = useMemo(() => {
    if (!result) return [];
    const hideEmployeeCol =
      definition?.code === 'SALES-07' &&
      (Boolean(employeeId) ||
        (result.rows.length > 0 &&
          result.rows.every((row) => row.employeeId === result.rows[0]?.employeeId)));
    return result.columns.filter((col) => !(hideEmployeeCol && col.key === 'employeeName'));
  }, [result, definition, employeeId]);

  const columns: ColumnsType<Record<string, unknown>> = useMemo(
    () =>
      visibleResultColumns.map((col) => ({
        title: col.title,
        dataIndex: col.key,
        key: col.key,
        align: col.align,
        render: (value: unknown, row: Record<string, unknown>) => {
          if (definition?.code === 'SALES-06' && col.key === 'employeeName' && row.employeeId) {
            return (
              <Link
                to={`/reports/sales/revenue-by-employee-product${staffReportQuery({
                  employeeId: String(row.employeeId),
                  from: range[0],
                  to: range[1],
                  warehouseId,
                })}`}
              >
                {formatReportCell(value, col.format)}
              </Link>
            );
          }
          return (
            <span style={{ fontVariantNumeric: col.align === 'right' ? 'tabular-nums' : undefined }}>
              {formatReportCell(value, col.format)}
            </span>
          );
        },
      })),
    [visibleResultColumns, definition, range, warehouseId],
  );

  const productSuggestions = useMemo(() => {
    const q = searchInput.trim().toLowerCase();
    return suggestionProducts
      .filter((p) => {
        if (!q) return true;
        return (
          p.productCode.toLowerCase().includes(q) ||
          p.productName.toLowerCase().includes(q) ||
          (p.primaryBarcode?.toLowerCase().includes(q) ?? false)
        );
      })
      .slice(0, 12)
      .map((p) => ({
        value: p.id,
        label: `${p.productCode} — ${p.productName}${p.primaryBarcode ? ` · ${p.primaryBarcode}` : ''}`,
      }));
  }, [searchInput, suggestionProducts]);

  const displayFilterEntries = useMemo(() => {
    if (!result || !definition) return [];
    const { label: productSearchLabel } = resolveProductSearchTerm(searchInput, suggestionProducts);
    return buildReportFilterDisplayEntries(
      definition,
      result.filterLabels,
      warehouses,
      suppliers,
      productSearchLabel,
    )
      .filter((entry) => entry.key !== 'Ghi chú')
      .map((entry) => {
        if (entry.key !== t('employee') && entry.key !== 'Nhân viên') return entry;
        const emp = employees.find((e) => e.id === entry.value);
        return emp ? { ...entry, value: `${emp.employeeCode} — ${emp.fullName}` } : entry;
      });
  }, [result, definition, warehouses, suppliers, searchInput, suggestionProducts, employees, t]);

  const kpiItems = useMemo(() => {
    if (!result?.totals) return [];
    const totals = result.totals;
    const orders = asNumber(totals.orderCount);
    const net = asNumber(totals.netAmount);
    const items: Array<{ key: string; label: string; value: string }> = [
      { key: 'net', label: t('kpiNet'), value: formatReportCell(net, 'money') },
      { key: 'orders', label: t('kpiOrders'), value: formatReportCell(orders, 'integer') },
    ];
    if (totals.netQty != null) {
      items.push({ key: 'qty', label: t('kpiQty'), value: formatReportCell(totals.netQty, 'qty') });
    } else if (orders > 0) {
      items.push({ key: 'aov', label: t('kpiAov'), value: formatReportCell(Math.round(net / orders), 'money') });
    }
    if (asNumber(totals.refundAmount) > 0) {
      items.push({
        key: 'refund',
        label: t('kpiRefund'),
        value: formatReportCell(totals.refundAmount, 'money'),
      });
    }
    return items;
  }, [result, t]);

  const selectSuggestedProduct = useCallback(
    (productId: string) => {
      const product = suggestionProducts.find((p) => p.id === productId);
      if (!product) return;
      setSearchInput(`${product.productCode} — ${product.productName}`);
    },
    [suggestionProducts],
  );

  if (
    location.pathname.startsWith('/reports/sales/revenue-by-clinic-doctor') &&
    (auditSlimNav || !connectEnabled)
  ) {
    return <Navigate to="/reports" replace />;
  }

  if (!definition) {
    return <Typography.Text type="danger">{t('notFound')}</Typography.Text>;
  }

  const pageTitle = isStaffReport ? t('staffSalesTitle') : definition.name;
  const pageDesc = isStaffReport ? t('staffSalesDesc') : definition.description;
  const filterHints = !isStaffReport && definition ? filterHintsForReport(definition) : [];

  return (
    <div className="report-view">
      <div className="report-view__head">
        <div>
          <Typography.Title level={4} className="report-view__title">
            {pageTitle}
            <span className="report-view__code">{isStaffReport ? 'SALES-06' : definition.code}</span>
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="report-view__desc">
            {pageDesc}
          </Typography.Paragraph>
          {filterHints.length > 0 && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('filterCriteria')} {filterHints.join(' · ')}
            </Typography.Text>
          )}
        </div>
        {result && (
          <div className="report-view__actions">
            {canExportReports ? (
              <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(result)}>
                {t('exportCsv')}
              </Button>
            ) : null}
            <Button icon={<PrinterOutlined />} onClick={() => printReportElement('report-print-area', pageTitle)}>
              {t('print')}
            </Button>
          </div>
        )}
      </div>

      <Card size="small" className="report-view__toolbar-card" style={{ marginBottom: 16 }}>
        {isStaffReport && (
          <div className="report-view__mode">
            <Segmented
              value={isStaffProduct ? 'product' : 'summary'}
              onChange={(value) => goStaffView(value === 'product' ? 'product' : 'summary')}
              options={[
                { label: t('summary'), value: 'summary' },
                { label: t('byProduct'), value: 'product' },
              ]}
            />
          </div>
        )}
        <div className="report-view__filters">
          {definition.supportsDateRange && (
            <div className="report-view__field report-view__field--period">
              <span className="report-view__field-label">{t('period')}</span>
              <RangePicker
                value={range}
                onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
                format="DD/MM/YYYY"
                style={{ width: '100%' }}
              />
            </div>
          )}
          {definition.supportsGroupBy && definition.supportsGroupBy.length > 0 && (
            <div className="report-view__field">
              <span className="report-view__field-label">{t('groupBy')}</span>
              <Select
                style={{ width: '100%' }}
                value={groupBy}
                onChange={setGroupBy}
                options={definition.supportsGroupBy.map((g) => ({
                  value: g,
                  label: tg(g),
                }))}
              />
            </div>
          )}
          {definition.supportsWarehouse && (
            <div className="report-view__field">
              <span className="report-view__field-label">{t('warehouse')}</span>
              <Select
                allowClear
                style={{ width: '100%' }}
                placeholder={t('warehouseAll')}
                value={warehouseId}
                onChange={setWarehouseId}
                options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              />
            </div>
          )}
          {definition.supportsEmployee && (
            <div className="report-view__field">
              <span className="report-view__field-label">{t('employee')}</span>
              <Select
                allowClear
                showSearch
                optionFilterProp="label"
                style={{ width: '100%' }}
                placeholder={t('employeeAll')}
                value={employeeId}
                onChange={syncEmployeeParam}
                options={employees.map((e) => ({
                  value: e.id,
                  label: `${e.employeeCode} — ${e.fullName}`,
                }))}
              />
            </div>
          )}
          {definition.supportsSupplier && (
            <div className="report-view__field">
              <span className="report-view__field-label">{t('supplier')}</span>
              <Select
                allowClear
                showSearch
                optionFilterProp="label"
                style={{ width: '100%' }}
                placeholder={t('supplierAll')}
                value={supplierId}
                onChange={setSupplierId}
                options={suppliers.map((s) => ({
                  value: s.id,
                  label: `${s.supplierCode} — ${s.supplierName}`,
                }))}
              />
            </div>
          )}
          {definition.supportsSearch && (
            <div className="report-view__field report-view__field--search">
              <span className="report-view__field-label">{t('productSearch')}</span>
              <AutoComplete
                style={{ width: '100%' }}
                options={productSuggestions}
                value={searchInput}
                onSelect={(value) => selectSuggestedProduct(String(value))}
                onChange={(value) => setSearchInput(value)}
              >
                <Input
                  placeholder={t('productSearchPlaceholder')}
                  prefix={<SearchOutlined />}
                  allowClear
                  onPressEnter={() => void load()}
                />
              </AutoComplete>
            </div>
          )}
          {definition.supportsExpiryDays && (
            <div className="report-view__field" style={{ maxWidth: 140 }}>
              <span className="report-view__field-label">{t('expiryDays')}</span>
              <InputNumber min={1} max={365} value={expiryDays} onChange={(v) => setExpiryDays(Number(v) || 30)} />
            </div>
          )}
          <div className="report-view__actions">
            <Button type="primary" icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
              {t('runReport')}
            </Button>
          </div>
        </div>
      </Card>

      {loadError && (
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 16 }}
          message={t('loadFailed')}
          description={
            <>
              {loadError}
              {loadError.includes('404') || loadError.toLowerCase().includes('not found') ? (
                <div style={{ marginTop: 8 }}>{t('loadFailedHint')}</div>
              ) : null}
            </>
          }
        />
      )}

      {result && (
        <div id="report-print-area">
          {kpiItems.length > 0 && (
            <div className="report-view__kpis">
              {kpiItems.map((item) => (
                <div key={item.key} className="report-view__kpi">
                  <span className="report-view__kpi-label">{item.label}</span>
                  <span className="report-view__kpi-value">{item.value}</span>
                </div>
              ))}
            </div>
          )}
          <Card
            size="small"
            className="report-view__table-card"
            title={
              <div>
                <Typography.Text strong>
                  {isStaffProduct ? t('byProduct') : isStaffReport ? t('summary') : result.title}
                </Typography.Text>
                {displayFilterEntries.length > 0 && (
                  <div className="report-view__meta">
                    {displayFilterEntries.map(({ key, value }) => `${key} ${value}`).join(' · ')}
                    {' · '}
                    {t('generatedAt')} {dayjs(result.generatedAtUtc).format('DD/MM/YYYY HH:mm')}
                  </div>
                )}
              </div>
            }
          >
            <Table
              rowKey={(_, index) => String(index)}
              loading={loading}
              columns={columns}
              dataSource={result.rows}
              size="middle"
              locale={{
                emptyText: definition?.code === 'SALES-05' ? t('emptyDataConnectSales') : t('emptyData'),
              }}
              pagination={{
                pageSize: 50,
                showSizeChanger: false,
                showTotal: (total) => t('paginationTotal', { count: total }),
              }}
              scroll={{ x: true }}
              summary={() =>
                result.totals ? (
                  <Table.Summary fixed>
                    <Table.Summary.Row>
                      {visibleResultColumns.map((col, index) => (
                        <Table.Summary.Cell key={col.key} index={index} align={col.align}>
                          <Typography.Text strong>
                            {formatReportCell(result.totals?.[col.key], col.format)}
                          </Typography.Text>
                        </Table.Summary.Cell>
                      ))}
                    </Table.Summary.Row>
                  </Table.Summary>
                ) : null
              }
            />
          </Card>
        </div>
      )}

      {!loading && loadedOnce && !loadError && !result && <Empty description={t('noResult')} />}
    </div>
  );
}
