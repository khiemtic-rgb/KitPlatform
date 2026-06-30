import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, App, Button, Space, Typography, Upload } from 'antd';
import { InboxOutlined, UploadOutlined } from '@ant-design/icons';
import type { UploadRequestOption } from 'rc-upload/lib/interface';
import { importSuppliers, type SupplierImportResult } from '@/shared/api/procurement.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { parseDecimal, parseSpreadsheetFile, pickRowValue } from '@/shared/utils/spreadsheet-import';

type MappedRow = {
  rowNumber: number;
  supplierCode: string;
  supplierName: string;
  phone?: string;
  email?: string;
  address?: string;
  contactName?: string;
  taxCode?: string;
  paymentTerms?: number;
};

function mapSupplierRows(rows: Record<string, string>[]): MappedRow[] {
  return rows
    .map((row, index) => ({
      rowNumber: index + 2,
      supplierCode: pickRowValue(
        row,
        'supplier_code',
        'ma_ncc',
        'mã_ncc',
        'ma_nha_cung_cap',
        'mã_nha_cung_cấp',
      ),
      supplierName: pickRowValue(
        row,
        'supplier_name',
        'ten_ncc',
        'tên_ncc',
        'ten_nha_cung_cap',
        'tên_nhà_cung_cấp*',
        'tên_nhà_cung_cấp',
      ),
      phone: pickRowValue(row, 'phone', 'dien_thoai', 'điện_thoại') || undefined,
      email: pickRowValue(row, 'email') || undefined,
      address: pickRowValue(row, 'address', 'dia_chi', 'địa_chỉ', 'địa_chỉ_1*', 'địa_chỉ_1') || undefined,
      contactName: pickRowValue(row, 'contact_name', 'nguoi_lien_he', 'người_liên_hệ') || undefined,
      taxCode: pickRowValue(row, 'tax_code', 'mst') || undefined,
      paymentTerms: parseDecimal(pickRowValue(row, 'payment_terms', 'han_tt')),
    }))
    .filter((r) => r.supplierCode && r.supplierName.length >= 2);
}

export function SupplierImportCard({ onImported }: { onImported?: () => void }) {
  const { t } = useTranslation('procurement', { keyPrefix: 'suppliers.import' });
  const { message } = App.useApp();
  const [preview, setPreview] = useState<MappedRow[]>([]);
  const [result, setResult] = useState<SupplierImportResult | null>(null);
  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [importBatch, setImportBatch] = useState<{ current: number; total: number } | null>(null);
  const [fileName, setFileName] = useState<string>();

  const handleFile = useCallback(
    async (file: File) => {
      try {
        const rows = await parseSpreadsheetFile(file);
        const mapped = mapSupplierRows(rows);
        if (mapped.length === 0) {
          message.warning(t('noValidRows'));
          return;
        }
        setPreview(mapped);
        setResult(null);
        setImportError(null);
        setFileName(file.name);
        message.success(t('readSuccess', { count: mapped.length, fileName: file.name }));
      } catch (error) {
        message.error(apiErrorMessage(error, t('readFailed')));
      }
    },
    [message, t],
  );

  const runImport = async () => {
    if (preview.length === 0) return;
    setImporting(true);
    setImportBatch(null);
    setImportError(null);
    setResult(null);
    try {
      const res = await importSuppliers(
        preview.map((row) => ({
          rowNumber: row.rowNumber,
          supplierCode: row.supplierCode,
          supplierName: row.supplierName,
          taxCode: row.taxCode,
          contactName: row.contactName,
          phone: row.phone,
          email: row.email,
          address: row.address,
          paymentTerms: row.paymentTerms ?? 30,
        })),
        (current, total) => setImportBatch({ current, total }),
      );
      setResult(res);
      message.success(t('success', { created: res.created, skipped: res.skipped, failed: res.failed }));
      await onImported?.();
    } catch (error) {
      const text = apiErrorMessage(error, t('failed'));
      setImportError(text);
      message.error(text);
    } finally {
      setImporting(false);
      setImportBatch(null);
    }
  };

  return (
    <Space direction="vertical" size="small" style={{ width: '100%' }}>
      <Typography.Text type="secondary">{t('hint')}</Typography.Text>
      <Space wrap>
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
          {t('importButton')} {preview.length > 0 ? `(${preview.length})` : ''}
        </Button>
      </Space>
      {fileName && (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('fileLabel', { name: fileName })}
        </Typography.Text>
      )}
      {importing && !importBatch && (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('sending', { count: preview.length })}
        </Typography.Text>
      )}
      {importBatch && (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('batchProgress', { current: importBatch.current, total: importBatch.total })}
        </Typography.Text>
      )}
      {importError && <Alert type="error" showIcon message={importError} />}
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
    </Space>
  );
}
