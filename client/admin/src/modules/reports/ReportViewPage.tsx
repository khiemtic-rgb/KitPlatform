import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLocation } from 'react-router-dom';
import {
  Alert,
  AutoComplete,
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Input,
  InputNumber,
  Row,
  Select,
  Space,
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
import { fetchWarehouses } from '@/shared/api/inventory.api';
import type { Warehouse } from '@/shared/api/inventory.types';
import { runReport } from '@/shared/api/reports.api';
import type { ReportTableResult } from '@/shared/api/reports.types';
import { apiErrorMessage } from '@/shared/api/api-error';
import { findReportByPath } from '@/modules/reports/reports-catalog';
import { buildReportFilterDisplayEntries, filterHintsForReport } from '@/modules/reports/report-filter-ui';
import { exportReportCsv, formatReportCell, printReportElement } from '@/modules/reports/report-export';

const { RangePicker } = DatePicker;

function defaultRange(): [Dayjs, Dayjs] {
  return [dayjs().startOf('month'), dayjs().endOf('day')];
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
  const definition = useMemo(
    () => findReportByPath(location.pathname),
    [location.pathname, i18n.language],
  );

  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange);
  const [groupBy, setGroupBy] = useState<string>('day');
  const [warehouseId, setWarehouseId] = useState<string>();
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
  }, [definition, range, groupBy, warehouseId, supplierId, searchInput, suggestionProducts, expiryDays, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: ColumnsType<Record<string, unknown>> = useMemo(() => {
    if (!result) return [];
    return result.columns.map((col) => ({
      title: col.title,
      dataIndex: col.key,
      key: col.key,
      align: col.align,
      render: (value: unknown) => (
        <span style={{ fontVariantNumeric: col.align === 'right' ? 'tabular-nums' : undefined }}>
          {formatReportCell(value, col.format)}
        </span>
      ),
    }));
  }, [result]);

  const filterHints = useMemo(() => (definition ? filterHintsForReport(definition) : []), [definition]);

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
    );
  }, [result, definition, warehouses, suppliers, searchInput, suggestionProducts]);

  const selectSuggestedProduct = useCallback(
    (productId: string) => {
      const product = suggestionProducts.find((p) => p.id === productId);
      if (!product) return;
      setSearchInput(`${product.productCode} — ${product.productName}`);
    },
    [suggestionProducts],
  );

  if (!definition) {
    return <Typography.Text type="danger">{t('notFound')}</Typography.Text>;
  }

  return (
    <div>
      <Space direction="vertical" size="middle" style={{ width: '100%' }}>
        <div>
          <Typography.Text type="secondary">{definition.code}</Typography.Text>
          <Typography.Title level={4} style={{ margin: '4px 0' }}>
            {definition.name}
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {definition.description}
          </Typography.Paragraph>
          {filterHints.length > 0 && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('filterCriteria')} {filterHints.join(' · ')}
            </Typography.Text>
          )}
        </div>

        <Card size="small">
          <Row gutter={[12, 12]} align="middle">
            {definition.supportsDateRange && (
              <Col xs={24} md={10}>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                  {t('period')}
                </Typography.Text>
                <RangePicker
                  value={range}
                  onChange={(vals) => vals?.[0] && vals[1] && setRange([vals[0], vals[1]])}
                  format="DD/MM/YYYY"
                  style={{ width: '100%' }}
                />
              </Col>
            )}
            {definition.supportsGroupBy && definition.supportsGroupBy.length > 0 && (
              <Col xs={24} sm={12} md={6}>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                  {t('groupBy')}
                </Typography.Text>
                <Select
                  style={{ width: '100%' }}
                  value={groupBy}
                  onChange={setGroupBy}
                  options={definition.supportsGroupBy.map((g) => ({
                    value: g,
                    label: tg(g),
                  }))}
                />
              </Col>
            )}
            {definition.supportsWarehouse && (
              <Col xs={24} sm={12} md={6}>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                  {t('warehouse')}
                </Typography.Text>
                <Select
                  allowClear
                  style={{ width: '100%' }}
                  placeholder={t('warehouseAll')}
                  value={warehouseId}
                  onChange={setWarehouseId}
                  options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
                />
              </Col>
            )}
            {definition.supportsSupplier && (
              <Col xs={24} sm={12} md={6}>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                  {t('supplier')}
                </Typography.Text>
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
              </Col>
            )}
            {definition.supportsSearch && (
              <Col xs={24} sm={12} md={8}>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                  {t('productSearch')}
                </Typography.Text>
                <AutoComplete
                  style={{ width: '100%' }}
                  options={productSuggestions}
                  value={searchInput}
                  onSelect={(value) => selectSuggestedProduct(String(value))}
                  onChange={(value) => {
                    setSearchInput(value);
                  }}
                >
                  <Input
                    placeholder={t('productSearchPlaceholder')}
                    prefix={<SearchOutlined />}
                    allowClear
                    onPressEnter={() => void load()}
                  />
                </AutoComplete>
              </Col>
            )}
            {definition.supportsExpiryDays && (
              <Col xs={24} sm={12} md={4}>
                <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 4 }}>
                  {t('expiryDays')}
                </Typography.Text>
                <InputNumber min={1} max={365} value={expiryDays} onChange={(v) => setExpiryDays(Number(v) || 30)} />
              </Col>
            )}
            <Col xs={24}>
              <Space wrap>
                <Button type="primary" icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
                  {t('runReport')}
                </Button>
                {result && (
                  <>
                    <Button icon={<DownloadOutlined />} onClick={() => exportReportCsv(result)}>
                      {t('exportCsv')}
                    </Button>
                    <Button
                      icon={<PrinterOutlined />}
                      onClick={() => printReportElement('report-print-area', definition.name)}
                    >
                      {t('print')}
                    </Button>
                  </>
                )}
              </Space>
            </Col>
          </Row>
        </Card>

        {loadError && (
          <Alert
            type="error"
            showIcon
            message={t('loadFailed')}
            description={
              <>
                {loadError}
                {loadError.includes('404') || loadError.toLowerCase().includes('not found') ? (
                  <div style={{ marginTop: 8 }}>
                    {t('loadFailedHint')}{' '}
                    <Typography.Text code>.\scripts\restart-api.ps1</Typography.Text>{' '}
                    {t('loadFailedHintAfter')}
                  </div>
                ) : null}
              </>
            }
          />
        )}

        {result && (
          <div id="report-print-area">
            <Typography.Title level={5}>{result.title}</Typography.Title>
            <div className="meta" style={{ marginBottom: 12, color: '#555' }}>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px 20px', alignItems: 'baseline' }}>
                {displayFilterEntries.map(({ key, value }) => (
                  <span key={key}>
                    {key}: <Typography.Text>{value}</Typography.Text>
                  </span>
                ))}
              </div>
              <div style={{ marginTop: 4 }}>
                {t('generatedAt')} {dayjs(result.generatedAtUtc).format('DD/MM/YYYY HH:mm')}
              </div>
            </div>
            <Table
              rowKey={(_, index) => String(index)}
              loading={loading}
              columns={columns}
              dataSource={result.rows}
              locale={{ emptyText: t('emptyData') }}
              pagination={{ pageSize: 50, showTotal: (total) => t('paginationTotal', { count: total }) }}
              scroll={{ x: true }}
              summary={() =>
                result.totals ? (
                  <Table.Summary fixed>
                    <Table.Summary.Row>
                      {result.columns.map((col, index) => (
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
          </div>
        )}

        {!loading && loadedOnce && !loadError && !result && (
          <Empty description={t('noResult')} />
        )}
      </Space>
    </div>
  );
}
