import { useCallback, useState } from 'react';
import {
  Alert,
  Button,
  Card,
  Space,
  Table,
  Typography,
  Upload,
  message,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, InboxOutlined, UploadOutlined } from '@ant-design/icons';
import type { UploadRequestOption } from 'rc-upload/lib/interface';
import { importProducts, type ProductImportError, type ProductImportResult } from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  PRODUCT_IMPORT_TEMPLATE_HEADERS,
  downloadCsvTemplate,
  parseDecimal,
  parseSpreadsheetFile,
  pickRowValue,
} from '@/shared/utils/spreadsheet-import';

type PreviewRow = {
  rowNumber: number;
  productCode: string;
  productName: string;
  barcode: string;
  saleUnitName: string;
  retailPrice?: number;
  minStockQty?: number;
};

function mapProductRows(rows: Record<string, string>[]): PreviewRow[] {
  return rows.map((row, index) => ({
    rowNumber: index + 2,
    productCode: pickRowValue(row, 'product_code', 'ma_sp', 'mã_sp', 'code'),
    productName: pickRowValue(row, 'product_name', 'ten_sp', 'tên_sp', 'name'),
    barcode: pickRowValue(row, 'barcode', 'ma_vach', 'mã_vạch'),
    saleUnitName: pickRowValue(row, 'sale_unit_name', 'dvt', 'đvt', 'unit') || 'Viên',
    retailPrice: parseDecimal(pickRowValue(row, 'retail_price', 'gia_ban', 'giá_bán', 'price')),
    minStockQty: parseDecimal(pickRowValue(row, 'min_stock_qty', 'ton_toi_thieu', 'tồn_tối_thiểu')),
  }));
}

export function ProductImportPage() {
  const [preview, setPreview] = useState<PreviewRow[]>([]);
  const [result, setResult] = useState<ProductImportResult | null>(null);
  const [importing, setImporting] = useState(false);
  const [fileName, setFileName] = useState<string>();

  const handleFile = useCallback(async (file: File) => {
    try {
      const rows = await parseSpreadsheetFile(file);
      const mapped = mapProductRows(rows).filter((r) => r.productName.length >= 2);
      if (mapped.length === 0) {
        message.warning('Không tìm thấy dòng hợp lệ. Kiểm tra header file Excel/CSV.');
        return;
      }
      setPreview(mapped);
      setResult(null);
      setFileName(file.name);
      message.success(`Đã đọc ${mapped.length} dòng từ «${file.name}»`);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không đọc được file'));
    }
  }, []);

  const runImport = async () => {
    if (preview.length === 0) return;
    setImporting(true);
    try {
      const payload = preview.map((row) => ({
        rowNumber: row.rowNumber,
        productCode: row.productCode || undefined,
        productName: row.productName,
        barcode: row.barcode || undefined,
        saleUnitName: row.saleUnitName,
        retailPrice: row.retailPrice,
        minStockQty: row.minStockQty,
        categoryCode: undefined,
        brandCode: undefined,
        drugType: 1 as const,
      }));
      const res = await importProducts(payload);
      setResult(res);
      message.success(`Import xong: ${res.created} tạo mới, ${res.skipped} bỏ qua, ${res.failed} lỗi`);
    } catch (error) {
      message.error(apiErrorMessage(error, 'Import thất bại'));
    } finally {
      setImporting(false);
    }
  };

  const previewColumns: ColumnsType<PreviewRow> = [
    { title: 'Dòng', dataIndex: 'rowNumber', width: 70 },
    { title: 'Mã SP', dataIndex: 'productCode', width: 110 },
    { title: 'Tên SP', dataIndex: 'productName' },
    { title: 'Barcode', dataIndex: 'barcode', width: 130 },
    { title: 'ĐVT', dataIndex: 'saleUnitName', width: 80 },
    { title: 'Giá bán', dataIndex: 'retailPrice', width: 100 },
    { title: 'Tồn TT', dataIndex: 'minStockQty', width: 90 },
  ];

  const errorColumns: ColumnsType<ProductImportError> = [
    { title: 'Dòng', dataIndex: 'rowNumber', width: 70 },
    { title: 'Lỗi', dataIndex: 'message' },
  ];

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          Import sản phẩm (Excel/CSV)
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          Cột tối thiểu: <code>product_name</code>, khuyến nghị thêm <code>barcode</code>,{' '}
          <code>retail_price</code>, <code>min_stock_qty</code>. Mã SP trùng sẽ bỏ qua.
        </Typography.Paragraph>
      </div>

      <Card size="small">
        <Space wrap>
          <Button
            icon={<DownloadOutlined />}
            onClick={() => downloadCsvTemplate('mau-import-san-pham.csv', PRODUCT_IMPORT_TEMPLATE_HEADERS)}
          >
            Tải mẫu CSV
          </Button>
          <Upload
            accept=".xlsx,.xls,.csv"
            showUploadList={false}
            customRequest={(options: UploadRequestOption) => {
              const file = options.file as File;
              void handleFile(file).then(() => options.onSuccess?.({}, file));
            }}
          >
            <Button icon={<UploadOutlined />}>Chọn file Excel/CSV</Button>
          </Upload>
          <Button
            type="primary"
            icon={<InboxOutlined />}
            disabled={preview.length === 0}
            loading={importing}
            onClick={() => void runImport()}
          >
            Import {preview.length > 0 ? `(${preview.length} dòng)` : ''}
          </Button>
        </Space>
        {fileName && (
          <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
            File: {fileName}
          </Typography.Text>
        )}
      </Card>

      {result && (
        <Alert
          type={result.failed > 0 ? 'warning' : 'success'}
          showIcon
          message={`Tạo mới: ${result.created} · Bỏ qua (trùng mã): ${result.skipped} · Lỗi: ${result.failed}`}
        />
      )}

      {result && result.errors.length > 0 && (
        <Card size="small" title="Chi tiết lỗi">
          <Table rowKey="rowNumber" size="small" pagination={{ pageSize: 20 }} columns={errorColumns} dataSource={result.errors} />
        </Card>
      )}

      {preview.length > 0 && (
        <Card size="small" title={`Xem trước (${preview.length} dòng)`}>
          <Table rowKey="rowNumber" size="small" pagination={{ pageSize: 15 }} columns={previewColumns} dataSource={preview} scroll={{ x: 800 }} />
        </Card>
      )}
    </Space>
  );
}
