import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
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
  message,
} from 'antd';
import type { UploadRequestOption } from 'rc-upload/lib/interface';
import { DeleteOutlined, DownloadOutlined, PlusOutlined, ReloadOutlined, SearchOutlined, UploadOutlined } from '@ant-design/icons';
import { Link } from 'react-router-dom';
import {
  createOpeningBalance,
  fetchOpeningBalanceBatches,
  fetchStockBatches,
  fetchWarehouses,
  importOpeningBalance,
  voidOpeningBalanceBatch,
} from '@/shared/api/inventory.api';
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

function renderProductCell(code: string, name: string) {
  return (
    <div>
      <Typography.Text type="secondary" style={{ fontSize: 11, display: 'block', lineHeight: 1.3 }}>
        Mã SP: {code}
      </Typography.Text>
      <span>{name}</span>
    </div>
  );
}

export function OpeningBalancePage() {
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [warehouseId, setWarehouseId] = useState<string>();
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineRow[]>([{ key: '1' }]);
  const [saving, setSaving] = useState(false);
  const [recentImports, setRecentImports] = useState<SavedImport[]>([]);
  const [excelImporting, setExcelImporting] = useState(false);
  const [lastSavedId, setLastSavedId] = useState<string | null>(null);
  const [openingBatches, setOpeningBatches] = useState<OpeningBalanceBatch[]>([]);
  const [openingLoading, setOpeningLoading] = useState(false);
  const [voidingId, setVoidingId] = useState<string | null>(null);
  const [stockBatches, setStockBatches] = useState<StockBatch[]>([]);
  const [stockLoading, setStockLoading] = useState(false);
  const [stockTotal, setStockTotal] = useState(0);
  const [listSearchInput, setListSearchInput] = useState('');
  const [listSearch, setListSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<OpeningStatusFilter>('all');
  const [productFilterId, setProductFilterId] = useState<string | undefined>();

  const selectedWarehouse = warehouses.find((w) => w.id === warehouseId);

  const loadStock = useCallback(async (whId?: string) => {
    if (!whId) {
      setStockBatches([]);
      setStockTotal(0);
      return;
    }
    setStockLoading(true);
    try {
      const result = await fetchStockBatches({ warehouseId: whId, page: 1, pageSize: 50 });
      setStockBatches(result.items);
      setStockTotal(result.total);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được tồn kho'));
    } finally {
      setStockLoading(false);
    }
  }, []);

  const loadOpeningBatches = useCallback(async (whId?: string) => {
    if (!whId) {
      setOpeningBatches([]);
      return;
    }
    setOpeningLoading(true);
    try {
      setOpeningBatches(await fetchOpeningBalanceBatches(whId));
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không tải được danh sách tồn đầu kỳ'));
    } finally {
      setOpeningLoading(false);
    }
  }, []);

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
      message.error(apiErrorMessage(error, 'Không tải được dữ liệu tham chiếu'));
    }
  }, [warehouseId]);

  useEffect(() => {
    loadLookups();
  }, [loadLookups]);

  useEffect(() => {
    if (warehouseId) {
      loadOpeningBatches(warehouseId);
      loadStock(warehouseId);
    }
  }, [warehouseId, loadOpeningBatches, loadStock]);

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
      message.success(`Đã xóa lô ${batch.batchNumber}`);
      await Promise.all([loadOpeningBatches(warehouseId), loadStock(warehouseId)]);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không xóa được lô tồn đầu kỳ'));
    } finally {
      setVoidingId(null);
    }
  };

  const handleSubmit = async () => {
    if (!warehouseId) {
      message.warning('Chọn kho nhập tồn');
      return;
    }

    const validLines = lines.filter(
      (l) => l.productId && l.batchNumber && l.quantity && l.quantity > 0,
    );
    if (validLines.length === 0) {
      message.warning('Thêm ít nhất một dòng hợp lệ');
      return;
    }

    for (const line of validLines) {
      if ((line.unitCost ?? 0) < 0) {
        message.warning('Giá vốn không hợp lệ');
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

      message.success(`Đã lưu ${result.linesProcessed} dòng tồn đầu kỳ`);
      setLines([{ key: String(Date.now()) }]);
      setNotes('');
      await Promise.all([loadOpeningBatches(warehouseId), loadStock(warehouseId)]);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không nhập được tồn đầu kỳ'));
    } finally {
      setSaving(false);
    }
  };

  const columns: ColumnsType<LineRow> = [
    {
      title: 'Sản phẩm',
      dataIndex: 'productId',
      width: 260,
      render: (_, row) => (
        <Select
          showSearch
          optionFilterProp="label"
          style={{ width: '100%' }}
          placeholder="Chọn SP"
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
      title: 'Số lô',
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
      title: 'HSD',
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
      title: 'Giá vốn',
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
      title: 'SL',
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
      title: 'Tên SP',
      key: 'productName',
      render: (_, row) => renderProductCell(row.productCode, row.productName),
    },
    { title: 'Số lô', dataIndex: 'batchNumber', width: 120 },
    {
      title: 'HSD',
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => formatDisplayDate(v),
    },
    {
      title: 'ĐVT',
      dataIndex: 'saleUnitName',
      width: 64,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Giá vốn',
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'SL nhập',
      dataIndex: 'quantity',
      width: 90,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
  ];

  const openingBatchColumns: ColumnsType<OpeningBalanceBatch> = [
    {
      title: 'Tên SP',
      key: 'productName',
      render: (_, row) => renderProductCell(row.productCode, row.productName),
    },
    { title: 'Số lô', dataIndex: 'batchNumber', width: 120 },
    {
      title: 'HSD',
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    {
      title: 'ĐVT',
      dataIndex: 'saleUnitName',
      width: 64,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Giá vốn',
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'SL nhập',
      dataIndex: 'openingQuantity',
      width: 90,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
    {
      title: 'Tồn',
      dataIndex: 'quantityAvailable',
      width: 80,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
    {
      title: 'Trạng thái',
      key: 'status',
      width: 100,
      render: (_, row) =>
        row.canVoid ? (
          <Tag color="green" style={{ margin: 0, fontSize: 12 }}>
            Chưa phát sinh
          </Tag>
        ) : (
          <Tooltip title={row.voidBlockReason ?? 'Đã phát sinh giao dịch'}>
            <Tag color="orange" style={{ margin: 0, fontSize: 12 }}>
              Đã phát sinh
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
            title="Xóa lô tồn đầu kỳ?"
            description={`${row.productCode} / ${row.batchNumber} · ${row.quantityAvailable.toLocaleString('vi-VN')} đơn vị tại ${row.warehouseName}`}
            okText="Xóa"
            cancelText="Hủy"
            okButtonProps={{ danger: true }}
            onConfirm={() => handleVoid(row)}
          >
            <Button
              type="text"
              size="small"
              danger
              loading={voidingId === row.batchId}
              icon={<DeleteOutlined />}
              aria-label="Xóa lô"
            />
          </Popconfirm>
        ) : (
          <Tooltip title={row.voidBlockReason ?? 'Không thể xóa — dùng Kiểm kê để điều chỉnh'}>
            <Button type="text" size="small" disabled icon={<DeleteOutlined />} aria-label="Không thể xóa" />
          </Tooltip>
        ),
    },
  ];

  const stockColumns: ColumnsType<StockBatch> = [
    {
      title: 'Tên SP',
      key: 'productName',
      render: (_, row) => renderProductCell(row.productCode, row.productName),
    },
    { title: 'Số lô', dataIndex: 'batchNumber', width: 120 },
    {
      title: 'HSD',
      dataIndex: 'expiryDate',
      width: 110,
      render: (v?: string) => (v ? formatDisplayDate(v) : '—'),
    },
    {
      title: 'ĐVT',
      dataIndex: 'saleUnitName',
      width: 64,
      render: (v?: string) => v ?? '—',
    },
    {
      title: 'Giá vốn',
      dataIndex: 'unitCost',
      width: 110,
      align: 'right',
      render: (v: number) => (
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{formatDisplayMoney(v)}</span>
      ),
    },
    {
      title: 'Tồn',
      dataIndex: 'quantityAvailable',
      width: 90,
      align: 'right',
      render: (v: number) => v.toLocaleString('vi-VN'),
    },
  ];

  const latestImport = recentImports.find((item) => item.id === lastSavedId) ?? recentImports[0];
  const voidableCount = openingBatches.filter((b) => b.canVoid).length;

  const filteredOpeningBatches = useMemo(() => {
    const q = listSearch.trim().toLowerCase();
    return openingBatches.filter((row) => {
      if (statusFilter === 'voidable' && !row.canVoid) return false;
      if (statusFilter === 'locked' && row.canVoid) return false;
      if (productFilterId && row.productId !== productFilterId) return false;
      if (!q) return true;
      return (
        row.productCode.toLowerCase().includes(q) ||
        row.productName.toLowerCase().includes(q) ||
        row.batchNumber.toLowerCase().includes(q)
      );
    });
  }, [openingBatches, listSearch, statusFilter, productFilterId]);

  const listSearchSuggestions = useMemo(() => {
    const q = listSearchInput.trim().toLowerCase();
    return openingBatches
      .filter((row) => {
        if (!q) return true;
        return (
          row.productCode.toLowerCase().includes(q) ||
          row.productName.toLowerCase().includes(q) ||
          row.batchNumber.toLowerCase().includes(q)
        );
      })
      .slice(0, 15)
      .map((row) => ({
        value: row.batchNumber,
        label: `${row.productCode} — ${row.productName} · Lô ${row.batchNumber}`,
      }));
  }, [openingBatches, listSearchInput]);

  const applyListSearch = (value?: string) => {
    const text = (value ?? listSearchInput).trim();
    setListSearchInput(text);
    setListSearch(text);
  };

  const handleExcelImport = async (file: File) => {
    if (!warehouseId) {
      message.warning('Chọn kho trước khi import.');
      return;
    }
    setExcelImporting(true);
    try {
      const rows = await parseSpreadsheetFile(file);
      const payload = rows
        .map((row, index) => ({
          rowNumber: index + 2,
          productKey: pickRowValue(row, 'product_key', 'ma_sp', 'mã_sp', 'barcode', 'ma_vach'),
          batchNumber: pickRowValue(row, 'batch_number', 'so_lo', 'số_lô', 'lot'),
          expiryDate: parseOptionalDate(pickRowValue(row, 'expiry_date', 'hsd', 'han_dung')),
          quantity: parseDecimal(pickRowValue(row, 'quantity', 'so_luong', 'số_lượng', 'sl')) ?? 0,
          unitCost: parseDecimal(pickRowValue(row, 'unit_cost', 'gia_von', 'giá_vốn', 'cost')) ?? 0,
        }))
        .filter((r) => r.productKey && r.batchNumber && r.quantity > 0);

      if (payload.length === 0) {
        message.warning('Không có dòng hợp lệ trong file.');
        return;
      }

      const result = await importOpeningBalance({ warehouseId, notes, rows: payload });
      message.success(`Import xong: ${result.linesProcessed} lô, ${result.errors.length} lỗi`);
      if (result.errors.length > 0) {
        message.warning(result.errors.slice(0, 3).map((e) => `Dòng ${e.rowNumber}: ${e.message}`).join(' · '));
      }
      await loadOpeningBatches(warehouseId);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Import thất bại'));
    } finally {
      setExcelImporting(false);
    }
  };

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card
        size="small"
        title="Import Excel tồn đầu kỳ"
        extra={
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => downloadCsvTemplate('mau-ton-dau-ky.csv', OPENING_BALANCE_TEMPLATE_HEADERS)}
          >
            Tải mẫu
          </Button>
        }
      >
        <Space wrap>
          <Upload
            accept=".xlsx,.xls,.csv"
            showUploadList={false}
            disabled={!warehouseId || excelImporting}
            customRequest={(options: UploadRequestOption) => {
              const file = options.file as File;
              void handleExcelImport(file).then(() => options.onSuccess?.({}, file));
            }}
          >
            <Button icon={<UploadOutlined />} loading={excelImporting} disabled={!warehouseId}>
              Chọn file Excel/CSV
            </Button>
          </Upload>
          <Typography.Text type="secondary">
            Cột: product_key (mã SP/barcode), batch_number, quantity, unit_cost, expiry_date
          </Typography.Text>
        </Space>
      </Card>

      <Card title="Nhập tồn đầu kỳ">
        <div style={{ display: 'flex', gap: 12, alignItems: 'flex-start', marginBottom: 16, maxWidth: 960 }}>
          <div style={{ flex: '0 0 260px' }}>
            <Typography.Text type="secondary">
              Kho nhập <Typography.Text type="danger">*</Typography.Text>
            </Typography.Text>
            <Select
              style={{ width: '100%', marginTop: 4 }}
              value={warehouseId}
              onChange={setWarehouseId}
              options={warehouses.map((w) => ({ value: w.id, label: w.warehouseName }))}
            />
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <Typography.Text type="secondary">Ghi chú</Typography.Text>
            <Input
              style={{ marginTop: 4 }}
              placeholder="VD: Go-live chi nhánh mới..."
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
            Thêm dòng
          </Button>
          <Button type="primary" loading={saving} onClick={handleSubmit}>
            Lưu nhập tồn
          </Button>
        </Space>
      </Card>

      {latestImport && (
        <Card title="Kết quả lần nhập vừa lưu">
          <Alert
            type="success"
            showIcon
            message={`Đã lưu ${latestImport.lines.length} dòng vào kho «${latestImport.warehouseName}»`}
            description={
              <>
                Thời gian: {new Date(latestImport.savedAt).toLocaleString('vi-VN')}
                {latestImport.notes ? ` · Ghi chú: ${latestImport.notes}` : ''}
                {' · '}
                <Typography.Text type="success">Các lô vừa nhập: Chưa phát sinh — có thể xóa nếu nhập nhầm.</Typography.Text>
              </>
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
        title={`Danh sách tồn đầu kỳ${selectedWarehouse ? `: ${selectedWarehouse.warehouseName}` : ''}`}
      >
        <Space style={{ marginBottom: 16 }} wrap>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Lọc sản phẩm"
            style={{ width: 240 }}
            value={productFilterId}
            onChange={setProductFilterId}
            options={products.map((p) => ({
              value: p.id,
              label: `${p.productCode} — ${p.productName}`,
            }))}
          />
          <Select
            style={{ width: 180 }}
            value={statusFilter}
            onChange={setStatusFilter}
            options={[
              { value: 'all', label: 'Mọi trạng thái' },
              { value: 'voidable', label: 'Chưa phát sinh' },
              { value: 'locked', label: 'Đã phát sinh' },
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
                placeholder="Tìm SP / mã / số lô"
                prefix={<SearchOutlined />}
                allowClear
                onPressEnter={() => applyListSearch()}
              />
            </AutoComplete>
            <Button type="primary" icon={<SearchOutlined />} onClick={() => applyListSearch()}>
              Lọc
            </Button>
          </Space.Compact>
          <Button
            type="primary"
            ghost
            icon={<ReloadOutlined />}
            onClick={() => loadOpeningBatches(warehouseId)}
            loading={openingLoading}
          >
            Tải lại
          </Button>
        </Space>
        {openingBatches.length === 0 && !openingLoading && (
          <Alert
            type="info"
            showIcon
            style={{ marginBottom: 16 }}
            message="Chưa có lô nhập từ màn này"
            description={
              <>
                Bảng dưới chỉ liệt kê hàng bạn <strong>Lưu nhập tồn</strong> tại đây (INV-008).
                Tồn demo từ seed (Paracetamol, …) <strong>không</strong> hiện ở đây — xem tab{' '}
                <Link to="/inventory/stock">Tồn kho</Link> hoặc bảng &quot;Tồn thực tế tại kho&quot; bên dưới.
                Sau khi bấm <strong>Lưu nhập tồn</strong>, lô mới sẽ xuất hiện trong bảng này.
              </>
            }
          />
        )}
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          Chỉ hiển thị lô nhập từ <strong>Tồn đầu kỳ</strong> còn tồn &gt; 0.
          {openingBatches.length > 0 && (
            <>
              {' '}
              {voidableCount} lô <Tag color="green">Chưa phát sinh</Tag> (có thể xóa) ·{' '}
              {openingBatches.length - voidableCount} lô <Tag color="orange">Đã phát sinh</Tag>
            </>
          )}
        </Typography.Paragraph>
        <Table
          rowKey="batchId"
          size="small"
          loading={openingLoading}
          columns={openingBatchColumns}
          dataSource={filteredOpeningBatches}
          pagination={false}
          locale={{
            emptyText: warehouseId
              ? listSearch || statusFilter !== 'all' || productFilterId
                ? 'Không có lô khớp bộ lọc'
                : 'Chưa có lô tồn đầu kỳ — hãy nhập và bấm Lưu nhập tồn'
              : 'Chọn kho để xem danh sách',
          }}
        />
      </Card>

      <Card
        title={`Tồn thực tế tại kho${selectedWarehouse ? `: ${selectedWarehouse.warehouseName}` : ''}`}
        extra={
          <Space>
            <Link to="/inventory/stock">Mở tab Tồn kho</Link>
            <Button
              type="primary"
              ghost
              icon={<ReloadOutlined />}
              onClick={() => loadStock(warehouseId)}
              loading={stockLoading}
            >
              Tải lại
            </Button>
          </Space>
        }
      >
        <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
          Mọi lô còn tồn tại kho (gồm cả tồn demo seed và tồn vừa nhập đầu kỳ).
          {stockTotal > stockBatches.length && ` Hiển thị ${stockBatches.length}/${stockTotal} lô.`}
        </Typography.Paragraph>
        <Table
          rowKey="id"
          size="small"
          loading={stockLoading}
          columns={stockColumns}
          dataSource={stockBatches}
          pagination={false}
          locale={{
            emptyText: warehouseId
              ? 'Kho này chưa có tồn — kiểm tra API đã chạy và đã chạy migration/seed'
              : 'Chọn kho',
          }}
        />
      </Card>
    </Space>
  );
}
