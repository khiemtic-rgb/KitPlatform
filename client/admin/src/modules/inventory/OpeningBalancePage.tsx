import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation, Trans } from 'react-i18next';
import {
  Alert,
  App,
  AutoComplete,
  Button,
  Card,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
} from 'antd';
import type { UploadRequestOption } from 'rc-upload/lib/interface';
import {
  DeleteOutlined,
  DownloadOutlined,
  InboxOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  UploadOutlined,
} from '@ant-design/icons';
import { Link } from 'react-router-dom';
import {
  createOpeningBalance,
  fetchOpeningBalanceBatches,
  fetchStockBatches,
  fetchWarehouses,
  importOpeningBalanceBatched,
  voidOpeningBalanceBatch,
} from '@/shared/api/inventory.api';
import type { OpeningBalanceImportResult } from '@/shared/api/inventory.types';
import { fetchProducts } from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import type { OpeningBalanceBatch, StockBatch, Warehouse } from '@/shared/api/inventory.types';
import type { ProductListItem } from '@/shared/api/catalog.types';
import { PharmaExpiryPicker } from '@/shared/ui/PharmaDatePicker';
import { formatDisplayDate } from '@/shared/utils/date';
import { formatDisplayMoney, moneyInputNumberPropsAllowZero, moneyInputNumberStyle } from '@/shared/utils/money';
import {
  OPENING_BALANCE_TEMPLATE_HEADERS,
  downloadCsvTemplate,
  parseDecimal,
  parseOptionalDate,
  parseSpreadsheetFile,
  pickRowValue,
} from '@/shared/utils/spreadsheet-import';
import type { ColumnsType } from 'antd/es/table';

type OpeningStatusFilter = 'all' | 'voidable' | 'locked';

interface LineRow {
  key: string;
  productId?: string;
  batchNumber?: string;
  expiryDate?: string;
  unitCost?: number;
  quantity?: number;
}

interface ExcelImportRow {
  rowNumber: number;
  productKey: string;
  batchNumber: string;
  expiryDate?: string;
  quantity: number;
  unitCost: number;
}

interface SavedImportLine {
  productCode: string;
  productName: string;
  saleUnitName?: string;
  batchNumber: string;
  expiryDate?: string;
  unitCost: number;
  quantity: number;
}

interface SavedImport {
  id: string;
  savedAt: string;
  warehouseName: string;
  notes?: string;
  lines: SavedImportLine[];
}

function renderProductCell(code: string, name: string, codeLabel: (code: string) => string) {
  return (
    <div>
      <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', lineHeight: 1.3 }}>
        {codeLabel(code)}
      </Typography.Text>
      <span>{name}</span>
    </div>
  );
}

function mapOpeningBalanceRows(rows: Record<string, string>[]): ExcelImportRow[] {
  return rows
    .map((row, index) => ({
      rowNumber: index + 2,
      productKey: pickRowValue(row, 'product_key', 'ma_sp', 'mã_sp', 'barcode', 'ma_vach'),
      batchNumber: pickRowValue(row, 'batch_number', 'so_lo', 'số_lô', 'lot'),
      expiryDate: parseOptionalDate(pickRowValue(row, 'expiry_date', 'hsd', 'han_dung')),
      quantity: parseDecimal(pickRowValue(row, 'quantity', 'so_luong', 'số_lượng', 'sl')) ?? 0,
      unitCost: Math.max(0, parseDecimal(pickRowValue(row, 'unit_cost', 'gia_von', 'giá_vốn', 'cost')) ?? 0),
    }))
    .filter((r) => r.productKey && r.batchNumber && r.quantity > 0);
}

export function OpeningBalancePage() {
  const { message } = App.useApp();
  const { t } = useTranslation('inventory', { keyPrefix: 'openingBalance' });
  const { t: ts } = useTranslation('inventory', { keyPrefix: 'shared' });
  const { t: tc } = useTranslation('common');
  const productCodeLabel = (code: string) => ts('productCodeLabel', { code });
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineRow[]>([{ key: '1' }]);
  const [saving, setSaving] = useState(false);
  const [recentImports, setRecentImports] = useState<SavedImport[]>([]);
  const [excelPreview, setExcelPreview] = useState<ExcelImportRow[]>([]);
  const [excelFileName, setExcelFileName] = useState<string>();
  const [excelImporting, setExcelImporting] = useState(false);
  const [excelImportError, setExcelImportError] = useState<string | null>(null);
  const [excelImportResult, setExcelImportResult] = useState<OpeningBalanceImportResult | null>(null);
  const [excelImportBatch, setExcelImportBatch] = useState<{ current: number; total: number } | null>(null);
  const [lastSavedId, setLastSavedId] = useState<string | null>(null);
  const [openingBatches, setOpeningBatches] = useState<OpeningBalanceBatch[]>([]);
  const [openingLoading, setOpeningLoading] = useState(false);
  const [openingPage, setOpeningPage] = useState(1);
  const [openingPageSize, setOpeningPageSize] = useState(50);
  const [openingTotal, setOpeningTotal] = useState(0);
  const [openingSummaryTotal, setOpeningSummaryTotal] = useState(0);
  const [openingSummaryVoidable, setOpeningSummaryVoidable] = useState(0);
  const [voidingId, setVoidingId] = useState<string | null>(null);
  const [stockBatches, setStockBatches] = useState<StockBatch[]>([]);
  const [stockLoading, setStockLoading] = useState(false);
  const [stockTotal, setStockTotal] = useState(0);
  const [listSearchInput, setListSearchInput] = useState('');
  const [listSearch, setListSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<OpeningStatusFilter>('all');
  const [productFilterId, setProductFilterId] = useState<string | undefined>();

  const selectedWarehouse = warehouses.find((w) => w.id === warehouseId);

  const loadStock = useCallback(async () => {
    if (!warehouseId) {
      setStockBatches([]);
      setStockTotal(0);
      return;
    }
    setStockLoading(true);
    try {
      const result = await fetchStockBatches({
        warehouseId,
        page: openingPage,
        pageSize: openingPageSize,
      });
      setStockBatches(result.items);
      setStockTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.stockLoadFailed')));
    } finally {
      setStockLoading(false);
    }
  }, [warehouseId, openingPage, openingPageSize, t]);

  const loadOpeningBatches = useCallback(async () => {
    if (!warehouseId) {
      setOpeningBatches([]);
      setOpeningTotal(0);
      setOpeningSummaryTotal(0);
      setOpeningSummaryVoidable(0);
      return;
    }
    setOpeningLoading(true);
    try {
      const result = await fetchOpeningBalanceBatches({
        warehouseId,
        productId: productFilterId,
        search: listSearch || undefined,
        status: statusFilter,
        page: openingPage,
        pageSize: openingPageSize,
      });
      setOpeningBatches(result.items);
      setOpeningTotal(result.total);
      setOpeningSummaryTotal(result.summaryTotal);
      setOpeningSummaryVoidable(result.summaryVoidableCount);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.listLoadFailed')));
    } finally {
      setOpeningLoading(false);
    }
  }, [warehouseId, productFilterId, listSearch, statusFilter, openingPage, openingPageSize, t]);

  const loadLookups = useCallback(async () => {
    try {
      const [wh, prodPage] = await Promise.all([
        fetchWarehouses(),
        fetchProducts({ page: 1, pageSize: 100, status: 1 }),
      ]);
      setWarehouses(wh);
      setProducts(prodPage.items);
      if (!warehouseId && wh.length > 0) {
        const defaultWh = wh.find((w) => w.isDefault) ?? wh[0];
        setWarehouseId(defaultWh.id);
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.lookupsLoadFailed')));
    }
  }, [warehouseId, t]);

  useEffect(() => {
    loadLookups();
  }, [loadLookups]);

  useEffect(() => {
    setOpeningPage(1);
  }, [warehouseId]);

  useEffect(() => {
    void loadOpeningBatches();
  }, [loadOpeningBatches]);

  useEffect(() => {
    void loadStock();
  }, [loadStock]);

  const addLine = () => {
    setLines((prev) => [...prev, { key: String(Date.now()) }]);
  };

  const removeLine = (key: string) => {
    setLines((prev) => (prev.length <= 1 ? prev : prev.filter((l) => l.key !== key)));
  };

  const updateLine = (key: string, patch: Partial<LineRow>) => {
    setLines((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  };

  const handleVoid = async (batch: OpeningBalanceBatch) => {
    setVoidingId(batch.batchId);
    try {
      await voidOpeningBalanceBatch(batch.batchId);
      message.success(t('messages.voidSuccess', { batch: batch.batchNumber }));
      await Promise.all([loadOpeningBatches(), loadStock()]);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.voidFailed')));
    } finally {
      setVoidingId(null);
    }
  };

  const handleSubmit = async () => {
    if (!warehouseId) {
      message.warning(t('messages.selectWarehouse'));
      return;
    }

    const validLines = lines.filter(
      (l) => l.productId && l.batchNumber && l.quantity && l.quantity > 0,
    );
    if (validLines.length === 0) {
      message.warning(t('messages.addValidLine'));
      return;
    }

    for (const line of validLines) {
      if ((line.unitCost ?? 0) < 0) {
        message.warning(t('messages.invalidUnitCost'));
        return;
      }
    }

    setSaving(true);
    try {
      const result = await createOpeningBalance({
        warehouseId,
        notes: notes || undefined,
        lines: validLines.map((l) => ({
          productId: l.productId!,
          batchNumber: l.batchNumber!.trim(),
          expiryDate: l.expiryDate,
          unitCost: l.unitCost ?? 0,
          quantity: l.quantity!,
        })),
      });

      const savedId = String(Date.now());
      const savedLines: SavedImportLine[] = validLines.map((l) => {
        const product = products.find((p) => p.id === l.productId);
        return {
          productCode: product?.productCode ?? '—',
          productName: product?.productName ?? '—',
          saleUnitName: product?.saleUnitName,
          batchNumber: l.batchNumber!.trim(),
          expiryDate: l.expiryDate,
          unitCost: l.unitCost ?? 0,
          quantity: l.quantity!,
        };
      });

      setRecentImports((prev) => [
        {
          id: savedId,
          savedAt: new Date().toISOString(),
          warehouseName: selectedWarehouse?.warehouseName ?? '—',
          notes: notes || undefined,
          lines: savedLines,
        },
        ...prev,
      ].slice(0, 10));
      setLastSavedId(savedId);

      message.success(t('messages.saveSuccess', { count: result.linesProcessed }));
      setLines([{ key: String(Date.now()) }]);
      setNotes('');
      await Promise.all([loadOpeningBatches(), loadStock()]);
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<LineRow> = [
    {
      title: ts('productName'),
      dataIndex: 'productId',
      width: 260,
      render: (_, row) => (
        <Select
          showSearch
          optionFilterProp="label"
          style={{ width: '100%' }}
          placeholder={t('selectProduct')}
          value={row.productId}
          onChange={(v) => updateLine(row.key, { productId: v })}
          options={products.map((p) => ({
            value: p.id,
            label: `${p.productCode} — ${p.productName}`,
          }))}
        />
      ),
    },
    {
      title: t('batchNumber'),
      dataIndex: 'batchNumber',
      width: 130,
      render: (_, row) => (
        <Input
          value={row.batchNumber}
          onChange={(e) => updateLine(row.key, { batchNumber: e.target.value })}
        />
      ),
    },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 140,
      render: (_, row) => (
        <PharmaExpiryPicker
          style={{ width: 130 }}
          inTable
          value={row.expiryDate}
          onChange={(value) => updateLine(row.key, { expiryDate: value || undefined })}
        />
      ),
    },
    {
      title: ts('unitCost'),
      dataIndex: 'unitCost',
      width: 120,
      align: 'right',
      render: (_, row) => (
        <InputNumber
          {...moneyInputNumberPropsAllowZero}
          style={moneyInputNumberStyle}
          value={row.unitCost}
          onChange={(v) => updateLine(row.key, { unitCost: v ?? 0 })}
        />
      ),
    },
    {
      title: ts('quantityAbbr'),
      dataIndex: 'quantity',
      width: 100,
      render: (_, row) => (
        <InputNumber
          min={0.001}
          style={{ width: '100%' }}
          value={row.quantity}
          onChange={(v) => updateLine(row.key, { quantity: v ?? 0 })}
        />
      ),
    },
    {
      title: '',
      key: 'actions',
      width: 50,
      render: (_, row) => (
        <Button type="text" danger icon={<DeleteOutlined />} onClick={() => removeLine(row.key)} />
      ),
    },
  ];

  const savedLineColumns: ColumnsType<SavedImportLine> = [
    {
      title: ts('productName'),
      key: 'productName',
      render: (_, row) => renderProductCell(row.productCode, row.productName, productCodeLabel),
    },
    { title: t('batchNumber'), dataIndex: 'batchNumber', width: 120 },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => formatDisplayDate(v),
    },
    {
      title: ts('unit'),
      dataIndex: 'saleUnitName',
      width: 64,
      render: (v?: string) => v ?? '—',
    },
    {
      title: ts('unitCost'),
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: t('importQuantity'),
      dataIndex: 'quantity',
      width: 90,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
  ];

  const openingBatchColumns: ColumnsType<OpeningBalanceBatch> = [
    {
      title: ts('productName'),
      key: 'productName',
      render: (_, row) => renderProductCell(row.productCode, row.productName, productCodeLabel),
    },
    { title: t('batchNumber'), dataIndex: 'batchNumber', width: 120 },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    {
      title: ts('unit'),
      dataIndex: 'saleUnitName',
      width: 64,
      render: (v?: string) => v ?? '—',
    },
    {
      title: ts('unitCost'),
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: t('importQuantity'),
      dataIndex: 'openingQuantity',
      width: 90,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
    {
      title: ts('stockQty'),
      dataIndex: 'quantityAvailable',
      width: 80,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
    {
      title: tc('fields.status'),
      key: 'status',
      width: 100,
      render: (_, row) =>
        row.canVoid ? (
          <Tag color="green" style={{ margin: 0, fontSize: 12 }}>
            {t('status.voidable')}
          </Tag>
        ) : (
          <Tooltip title={row.voidBlockReason ?? t('status.lockedTooltip')}>
            <Tag color="orange" style={{ margin: 0, fontSize: 12 }}>
              {t('status.locked')}
            </Tag>
          </Tooltip>
        ),
    },
    {
      title: '',
      key: 'actions',
      width: 44,
      align: 'center',
      render: (_, row) =>
        row.canVoid ? (
          <Popconfirm
            title={t('voidConfirm.title')}
            description={t('voidConfirm.description', {
              code: row.productCode,
              batch: row.batchNumber,
              qty: row.quantityAvailable.toLocaleString('vi-VN'),
              warehouse: row.warehouseName,
            })}
            okText={tc('actions.delete')}
            cancelText={tc('actions.cancel')}
            okButtonProps={{ danger: true }}
            onConfirm={() => handleVoid(row)}
          >
            <Button
              type="text"
              size="small"
              danger
              loading={voidingId === row.batchId}
              icon={<DeleteOutlined />}
              aria-label={t('voidConfirm.ariaDelete')}
            />
          </Popconfirm>
        ) : (
          <Tooltip title={row.voidBlockReason ?? t('voidConfirm.cannotDeleteTooltip')}>
            <Button
              type="text"
              size="small"
              disabled
              icon={<DeleteOutlined />}
              aria-label={t('voidConfirm.ariaCannotDelete')}
            />
          </Tooltip>
        ),
    },
  ];

  const stockColumns: ColumnsType<StockBatch> = [
    {
      title: ts('productName'),
      key: 'productName',
      render: (_, row) => renderProductCell(row.productCode, row.productName, productCodeLabel),
    },
    { title: t('batchNumber'), dataIndex: 'batchNumber', width: 120 },
    {
      title: ts('expiryAbbr'),
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    {
      title: ts('unit'),
      dataIndex: 'saleUnitName',
      width: 64,
      render: (v?: string) => v ?? '—',
    },
    {
      title: ts('unitCost'),
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: ts('stockQty'),
      dataIndex: 'quantityAvailable',
      width: 90,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
  ];

  const latestImport = recentImports.find((item) => item.id === lastSavedId) ?? recentImports[0];

  const listSearchSuggestions = useMemo(() => {
    const q = listSearchInput.trim().toLowerCase();
    return products
      .filter((p) => {
        if (!q) return true;
        return (
          p.productCode.toLowerCase().includes(q) ||
          p.productName.toLowerCase().includes(q) ||
          (p.primaryBarcode?.toLowerCase().includes(q) ?? false)
        );
      })
      .slice(0, 15)
      .map((p) => ({
        value: p.productCode,
        label: `${p.productCode} — ${p.productName}`,
      }));
  }, [products, listSearchInput]);

  const applyListSearch = (value?: string) => {
    const text = (value ?? listSearchInput).trim();
    setListSearchInput(text);
    setListSearch(text);
    setOpeningPage(1);
  };

  const handleExcelFile = async (file: File) => {
    try {
      const rows = await parseSpreadsheetFile(file);
      const mapped = mapOpeningBalanceRows(rows);
      if (mapped.length === 0) {
        message.warning(t('messages.noValidRows'));
        return;
      }
      setExcelPreview(mapped);
      setExcelFileName(file.name);
      setExcelImportError(null);
      setExcelImportResult(null);
      message.success(t('messages.fileReadSuccess', { count: mapped.length, name: file.name }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.fileReadFailed')));
    }
  };

  const runExcelImport = async () => {
    if (!warehouseId) {
      message.warning(t('messages.selectWarehouseForImport'));
      return;
    }
    if (excelPreview.length === 0) return;

    setExcelImporting(true);
    setExcelImportBatch(null);
    setExcelImportError(null);
    setExcelImportResult(null);
    try {
      const result = await importOpeningBalanceBatched(
        warehouseId,
        notes,
        excelPreview,
        (current, total) => setExcelImportBatch({ current, total }),
      );
      setExcelImportResult(result);
      message.success(
        t('messages.importSuccess', { processed: result.linesProcessed, errors: result.errors.length }),
      );
      if (result.errors.length > 0) {
        message.warning(
          result.errors
            .slice(0, 3)
            .map((e) => t('importRowError', { row: e.rowNumber, message: e.message }))
            .join(' · '),
        );
      }
      setExcelPreview([]);
      setExcelFileName(undefined);
      await loadOpeningBatches();
    } catch (error) {
      const text = apiErrorMessage(error, t('messages.importFailed'));
      setExcelImportError(text);
      message.error(text);
    } finally {
      setExcelImporting(false);
      setExcelImportBatch(null);
    }
  };

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card
        size="small"
        title={t('excelImportTitle')}
        extra={
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => downloadCsvTemplate('mau-ton-dau-ky.csv', OPENING_BALANCE_TEMPLATE_HEADERS)}
          >
            {t('downloadTemplate')}
          </Button>
        }
      >
        <Space direction="vertical" size="small" style={{ width: '100%' }}>
          <Typography.Text type="secondary">
            <Trans i18nKey="excelHint" ns="inventory" t={t} />
          </Typography.Text>
          <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start', maxWidth: 720 }}>
            <div style={{ flex: '0 0 260px' }}>
              <Typography.Text type="secondary">
                {t('importWarehouse')} <Typography.Text type="danger">*</Typography.Text>
              </Typography.Text>
              <Select
                style={{ width: '100%', marginTop: 4 }}
                value={warehouseId}
                onChange={setWarehouseId}
                placeholder={t('selectBranchWarehouse')}
                options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
              />
            </div>
          </div>
          <Space wrap>
            <Upload
              accept=".xlsx,.xls,.csv"
              showUploadList={false}
              disabled={excelImporting}
              customRequest={(options: UploadRequestOption) => {
                const file = options.file as File;
                void handleExcelFile(file).then(() => options.onSuccess?.({}, file));
              }}
            >
              <Button icon={<UploadOutlined />}>{t('chooseExcelFile')}</Button>
            </Upload>
            <Button
              type="primary"
              icon={<InboxOutlined />}
              disabled={!warehouseId || excelPreview.length === 0}
              loading={excelImporting}
              onClick={() => void runExcelImport()}
            >
              {excelPreview.length > 0
                ? t('importButtonWithCount', { count: excelPreview.length })
                : t('importButton')}
            </Button>
          </Space>
          {excelFileName && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('fileReady', {
                name: excelFileName,
                ready: excelPreview.length > 0
                  ? t('fileReadySuffix', { count: excelPreview.length })
                  : '',
              })}
            </Typography.Text>
          )}
          {!warehouseId && excelPreview.length > 0 && (
            <Alert type="warning" showIcon message={t('selectWarehouseBeforeImport')} />
          )}
          {excelImporting && !excelImportBatch && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('uploading', { count: excelPreview.length })}
            </Typography.Text>
          )}
          {excelImportBatch && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {t('importingBatch', {
                current: excelImportBatch.current,
                total: excelImportBatch.total,
              })}
            </Typography.Text>
          )}
          {excelImportError && <Alert type="error" showIcon message={excelImportError} />}
          {excelImportResult && (
            <Alert
              type={excelImportResult.errors.length > 0 ? 'warning' : 'success'}
              showIcon
              message={t('importResult', {
                processed: excelImportResult.linesProcessed,
                errors: excelImportResult.errors.length,
              })}
              description={
                excelImportResult.errors.length > 0
                  ? excelImportResult.errors
                      .slice(0, 5)
                      .map((e) => t('importRowError', { row: e.rowNumber, message: e.message }))
                      .join(' · ')
                  : undefined
              }
            />
          )}
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {t('columnsHint')}
          </Typography.Text>
        </Space>
      </Card>

      <Card title={t('manualEntryTitle')}>
        <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start', marginBottom: 16, maxWidth: 960 }}>
          <div style={{ flex: '0 0 260px' }}>
            <Typography.Text type="secondary">
              {t('importWarehouse')} <Typography.Text type="danger">*</Typography.Text>
            </Typography.Text>
            <Select
              style={{ width: '100%', marginTop: 4 }}
              value={warehouseId}
              onChange={setWarehouseId}
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
            />
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Typography.Text type="secondary">{ts('notes')}</Typography.Text>
            <Input
              style={{ marginTop: 4 }}
              placeholder={t('notesPlaceholder')}
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              allowClear
            />
          </div>
        </div>

        <Table
          rowKey="key"
          className="grn-lines-table"
          columns={columns}
          dataSource={lines}
          pagination={false}
          scroll={{ x: 900 }}
          style={{ marginBottom: 16 }}
        />

        <Space>
          <Button icon={<PlusOutlined />} onClick={addLine}>
            {ts('addLine')}
          </Button>
          <Button type="primary" loading={saving} onClick={handleSubmit}>
            {t('saveOpeningBalance')}
          </Button>
        </Space>
      </Card>

      {latestImport && (
        <Card title={t('latestResultTitle')}>
          <Alert
            type="success"
            showIcon
            message={t('latestResultMessage', {
              count: latestImport.lines.length,
              warehouse: latestImport.warehouseName,
            })}
            description={
              <Trans
                i18nKey="latestResultDescription"
                ns="inventory"
                t={t}
                values={{
                  time: new Date(latestImport.savedAt).toLocaleString('vi-VN'),
                  notes: latestImport.notes ? ` · ${ts('notes')}: ${latestImport.notes}` : '',
                }}
                components={{ success: <Typography.Text type="success" /> }}
              />
            }
            style={{ marginBottom: 16 }}
          />
          <Table
            rowKey={(row) => `${row.productCode}-${row.batchNumber}`}
            size="small"
            pagination={false}
            columns={savedLineColumns}
            dataSource={latestImport.lines}
          />
        </Card>
      )}

      <Card
        title={
          selectedWarehouse
            ? t('listTitleWithWarehouse', { warehouse: selectedWarehouse.warehouseName })
            : t('listTitle')
        }
      >
        <Space style={{ marginBottom: 16 }} wrap>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder={t('filterProduct')}
            style={{ width: 240 }}
            value={productFilterId}
            onChange={(value) => {
              setProductFilterId(value);
              setOpeningPage(1);
            }}
            options={products.map((p) => ({
              value: p.id,
              label: `${p.productCode} — ${p.productName}`,
            }))}
          />
          <Select
            style={{ width: 180 }}
            value={statusFilter}
            onChange={(value) => {
              setStatusFilter(value);
              setOpeningPage(1);
            }}
            options={[
              { value: 'all', label: t('statusFilter.all') },
              { value: 'voidable', label: t('statusFilter.voidable') },
              { value: 'locked', label: t('statusFilter.locked') },
            ]}
          />
          <Space.Compact>
            <AutoComplete
              style={{ width: 260 }}
              options={listSearchSuggestions}
              value={listSearchInput}
              onSelect={(value) => applyListSearch(String(value))}
              onChange={(value) => {
                setListSearchInput(value);
                if (!value) setListSearch('');
              }}
            >
              <Input
                placeholder={t('searchPlaceholder')}
                prefix={<SearchOutlined />}
                allowClear
                onPressEnter={() => applyListSearch()}
              />
            </AutoComplete>
            <Button type="primary" icon={<SearchOutlined />} onClick={() => applyListSearch()}>
              {tc('actions.filter')}
            </Button>
          </Space.Compact>
          <Button
            type="primary"
            ghost
            icon={<ReloadOutlined />}
            onClick={() => void loadOpeningBatches()}
            loading={openingLoading}
          >
            {tc('actions.reload')}
          </Button>
        </Space>
        {openingSummaryTotal === 0 && !openingLoading && (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message={t('emptyListHint')}
            description={
              <Trans
                i18nKey="emptyListDescription"
                ns="inventory"
                t={t}
                components={{
                  strong: <strong />,
                  stockLink: <Link to="/inventory/stock" />,
                }}
              />
            }
          />
        )}
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          <Trans i18nKey="listSummary" ns="inventory" t={t} components={{ strong: <strong /> }} />
          {openingSummaryTotal > 0 && (
            <>
              {' '}
              {openingSummaryVoidable} · {t('status.voidable')}{' '}
              <Tag color="green">{t('status.voidable')}</Tag> ·{' '}
              {openingSummaryTotal - openingSummaryVoidable} ·{' '}
              <Tag color="orange">{t('status.locked')}</Tag>
            </>
          )}
        </Typography.Paragraph>
        <Table
          rowKey="batchId"
          size="small"
          loading={openingLoading}
          columns={openingBatchColumns}
          dataSource={openingBatches}
          pagination={{
            current: openingPage,
            pageSize: openingPageSize,
            total: openingTotal,
            showSizeChanger: true,
            pageSizeOptions: [25, 50, 100],
            showTotal: (total) => t('paginationTotal', { count: total.toLocaleString('vi-VN') }),
            onChange: (page, pageSize) => {
              setOpeningPage(page);
              setOpeningPageSize(pageSize);
            },
          }}
          locale={{
            emptyText: warehouseId
              ? listSearch || statusFilter !== 'all' || productFilterId
                ? t('empty.noMatch')
                : t('empty.noOpeningBalance')
              : t('empty.selectWarehouse'),
          }}
        />
      </Card>

      <Card
        title={
          selectedWarehouse
            ? t('actualStockTitleWithWarehouse', { warehouse: selectedWarehouse.warehouseName })
            : t('actualStockTitle')
        }
        extra={
          <Space>
            <Link to="/inventory/stock">{t('openStockTab')}</Link>
            <Button
              type="primary"
              ghost
              icon={<ReloadOutlined />}
              onClick={() => void loadStock()}
              loading={stockLoading}
            >
              {tc('actions.reload')}
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          {t('actualStockDescription')}
        </Typography.Paragraph>
        <Table
          rowKey="id"
          size="small"
          loading={stockLoading}
          columns={stockColumns}
          dataSource={stockBatches}
          pagination={{
            current: openingPage,
            pageSize: openingPageSize,
            total: stockTotal,
            showSizeChanger: true,
            pageSizeOptions: [25, 50, 100],
            showTotal: (total) => t('paginationTotal', { count: total.toLocaleString('vi-VN') }),
            onChange: (page, pageSize) => {
              setOpeningPage(page);
              setOpeningPageSize(pageSize);
            },
          }}
          locale={{
            emptyText: warehouseId ? t('empty.noStockAtWarehouse') : t('empty.selectWarehouseShort'),
          }}
        />
      </Card>
    </Space>
  );
}
