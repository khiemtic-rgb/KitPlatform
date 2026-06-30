import { useCallback, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, App, Button, Space, Typography, Upload } from 'antd';
import { InboxOutlined, UploadOutlined } from '@ant-design/icons';
import type { UploadRequestOption } from 'rc-upload/lib/interface';
import { importCustomers, type CustomerImportResult } from '@/shared/api/customer-admin.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  parseOptionalDate,
  parseSpreadsheetFile,
  pickRowValue,
} from '@/shared/utils/spreadsheet-import';

type MappedRow = {
  rowNumber: number;
  customerCode: string;
  fullName: string;
  phone: string;
  email?: string;
  dateOfBirth?: string;
  gender?: number;
};

function parseGender(raw: string): number | undefined {
  const v = raw.trim().toLowerCase();
  if (v === '1' || v === 'nam') return 1;
  if (v === '2' || v === 'nữ' || v === 'nu') return 2;
  return undefined;
}

function mapCustomerRows(rows: Record<string, string>[]): MappedRow[] {
  const phonesSeen = new Set<string>();
  const codesSeen = new Set<string>();
  const mapped: MappedRow[] = [];

  for (let index = 0; index < rows.length; index++) {
    const row = rows[index];
    const rowNumber = index + 2;
    const customerCode = pickRowValue(
      row,
      'customer_code',
      'ma_kh',
      'mã_kh',
      'ma_khach_hang',
      'mã_khách_hàng',
    ).toUpperCase();
    const fullName = pickRowValue(
      row,
      'full_name',
      'ten_kh',
      'tên_kh',
      'ten_khach_hang',
      'tên_khách_hàng*',
      'tên_khách_hàng',
    );
    let phone = pickRowValue(row, 'phone', 'dien_thoai', 'điện_thoại', 'sdt', 'sđt').replace(/\s+/g, '');
    const email = pickRowValue(row, 'email') || undefined;
    const dateOfBirth = parseOptionalDate(
      pickRowValue(row, 'date_of_birth', 'ngay_sinh', 'ngày_sinh'),
    );
    const genderRaw = pickRowValue(row, 'gender', 'gioi_tinh', 'giới_tính');
    const gender = genderRaw ? parseGender(genderRaw) : undefined;

    if (!customerCode || fullName.length < 2) continue;
    if (codesSeen.has(customerCode)) continue;
    codesSeen.add(customerCode);

    if (!phone) {
      phone = `S-${customerCode}`;
    }
    if (phonesSeen.has(phone)) continue;
    phonesSeen.add(phone);

    mapped.push({
      rowNumber,
      customerCode,
      fullName,
      phone,
      email,
      dateOfBirth,
      gender,
    });
  }

  return mapped;
}

export function CustomerImportCard({ onImported }: { onImported?: () => void }) {
  const { t } = useTranslation('customer', { keyPrefix: 'importCard' });
  const { message } = App.useApp();
  const [preview, setPreview] = useState<MappedRow[]>([]);
  const [result, setResult] = useState<CustomerImportResult | null>(null);
  const [importing, setImporting] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [importBatch, setImportBatch] = useState<{ current: number; total: number } | null>(null);
  const [fileName, setFileName] = useState<string>();

  const handleFile = useCallback(
    async (file: File) => {
      try {
        const rows = await parseSpreadsheetFile(file);
        const mapped = mapCustomerRows(rows);
        if (mapped.length === 0) {
          message.warning(t('messages.noValidRows'));
          return;
        }
        setPreview(mapped);
        setResult(null);
        setImportError(null);
        setFileName(file.name);
        message.success(t('messages.readSuccess', { count: mapped.length, fileName: file.name }));
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.readFailed')));
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
      const res = await importCustomers(
        preview.map((row) => ({
          rowNumber: row.rowNumber,
          customerCode: row.customerCode,
          fullName: row.fullName,
          phone: row.phone,
          email: row.email,
          dateOfBirth: row.dateOfBirth,
          gender: row.gender,
        })),
        (current, total) => setImportBatch({ current, total }),
      );
      setResult(res);
      message.success(
        t('messages.importSuccess', {
          created: res.created,
          skipped: res.skipped,
          failed: res.failed,
        }),
      );
      await onImported?.();
    } catch (error) {
      const text = apiErrorMessage(error, t('messages.importFailed'));
      setImportError(text);
      message.error(text);
    } finally {
      setImporting(false);
      setImportBatch(null);
    }
  };

  return (
    <Space direction="vertical" size="small" style={{ width: '100%' }}>
      <Typography.Text type="secondary">
        {t('description', { fileName: t('sampleFileName') })}
      </Typography.Text>
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
          {preview.length > 0
            ? t('importButtonWithCount', { count: preview.length })
            : t('importButton')}
        </Button>
      </Space>
      {fileName && (
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          {t('fileLabel', { fileName })}
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
