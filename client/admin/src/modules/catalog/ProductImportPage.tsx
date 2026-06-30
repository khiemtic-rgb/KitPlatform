import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
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
import { catalogT } from '@/shared/i18n';
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
    saleUnitName:
      pickRowValue(row, 'sale_unit_name', 'dvt', 'đvt', 'unit') || catalogT()('shared.defaultSaleUnit'),
    retailPrice: parseDecimal(pickRowValue(row, 'retail_price', 'gia_ban', 'giá_bán', 'price')),
    minStockQty: parseDecimal(pickRowValue(row, 'min_stock_qty', 'ton_toi_thieu', 'tồn_tối_thiểu')),
  }));
}

export function ProductImportPage() {
  const { t } = useTranslation('catalog', { keyPrefix: 'import' });
  const [preview, setPreview] = useState<PreviewRow[]>([]);
  const [result, setResult] = useState<ProductImportResult | null>(null);
  const [importing, setImporting] = useState(false);
  const [importBatch, setImportBatch] = useState<{ current: number; total: number } | null>(null);
  const [fileName, setFileName] = useState<string>();

  const handleFile = useCallback(
    async (file: File) => {
      try {
        const rows = await parseSpreadsheetFile(file);
        const mapped = mapProductRows(rows).filter((r) => r.productName.length >= 2);
        if (mapped.length === 0) {
          message.warning(t('messages.noValidRows'));
          return;
        }
        setPreview(mapped);
        setResult(null);
        setFileName(file.name);
        message.success(t('messages.readSuccess', { count: mapped.length, fileName: file.name }));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.readFailed')));
      }
    },
    [t],
  );

  const runImport = async () => {
    if (preview.length === 0) return;
    setImporting(true);
    setImportBatch(null);
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
      const res = await importProducts(payload, (current, total) => {
        setImportBatch({ current, total });
      });
      setResult(res);
      message.success(
        t('messages.importSuccess', {
          created: res.created,
          skipped: res.skipped,
          failed: res.failed,
        }),
      );
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.importFailed')));
    } finally {
      setImporting(false);
      setImportBatch(null);
    }
  };

  const previewColumns: ColumnsType<PreviewRow> = useMemo(
    () => [
      { title: t('columns.row'), dataIndex: 'rowNumber', width: 70 },
      { title: t('columns.productCode'), dataIndex: 'productCode', width: 110 },
      { title: t('columns.productName'), dataIndex: 'productName' },
      { title: t('columns.barcode'), dataIndex: 'barcode', width: 130 },
      { title: t('columns.unit'), dataIndex: 'saleUnitName', width: 80 },
      { title: t('columns.retailPrice'), dataIndex: 'retailPrice', width: 100 },
      { title: t('columns.minStockQty'), dataIndex: 'minStockQty', width: 90 },
    ],
    [t],
  );

  const errorColumns: ColumnsType<ProductImportError> = useMemo(
    () => [
      { title: t('columns.row'), dataIndex: 'rowNumber', width: 70 },
      { title: t('columns.error'), dataIndex: 'message' },
    ],
    [t],
  );

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={4} style={{ marginBottom: 4 }}>
          {t('title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('intro')}
        </Typography.Paragraph>
      </div>

      <Card size="small">
        <Space wrap>
          <Button
            icon={<DownloadOutlined />}
            onClick={() => downloadCsvTemplate('mau-import-san-pham.csv', PRODUCT_IMPORT_TEMPLATE_HEADERS)}
          >
            {t('downloadTemplate')}
          </Button>
          <Upload
            accept=".xlsx,.xls,.csv"
            showUploadList={false}
            customRequest={(options: UploadRequestOption) => {
              const file = options.file as File;
              void handleFile(file).then(() => options.onSuccess?.({}, file));
            }}
          >
            <Button icon={<UploadOutlined />}>{t('chooseFile')}</Button>
          </Upload>
          <Button
            type="primary"
            icon={<InboxOutlined />}
            disabled={preview.length === 0}
            loading={importing}
            onClick={() => void runImport()}
          >
            {preview.length > 0
              ? t('importButtonWithCount', { count: preview.length })
              : t('importButton')}
          </Button>
        </Space>
        {importBatch && (
          <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
            {t('batchProgress', { current: importBatch.current, total: importBatch.total })}
          </Typography.Text>
        )}
        {fileName && (
          <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
            {t('fileLabel', { name: fileName })}
          </Typography.Text>
        )}
      </Card>

      {result && (
        <Alert
          type={result.failed > 0 ? 'warning' : 'success'}
          showIcon
          message={t('resultSummary', {
            created: result.created,
            skipped: result.skipped,
            failed: result.failed,
          })}
        />
      )}

      {result && result.errors.length > 0 && (
        <Card size="small" title={t('errorDetailsTitle')}>
          <Table
            rowKey="rowNumber"
            size="small"
            pagination={{ pageSize: 20 }}
            columns={errorColumns}
            dataSource={result.errors}
          />
        </Card>
      )}

      {preview.length > 0 && (
        <Card size="small" title={t('previewTitle', { count: preview.length })}>
          <Table
            rowKey="rowNumber"
            size="small"
            pagination={{ pageSize: 15 }}
            columns={previewColumns}
            dataSource={preview}
            scroll={{ x: 800 }}
          />
        </Card>
      )}
    </Space>
  );
}
