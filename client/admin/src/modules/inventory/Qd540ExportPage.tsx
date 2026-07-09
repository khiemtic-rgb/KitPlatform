import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Button, Card, DatePicker, Form, Select, Space, Table, message } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons';
import dayjs, { type Dayjs } from 'dayjs';
import { fetchBranches } from '@/shared/api/identity-admin.api';
import type { BranchListItem } from '@/shared/api/identity-admin.types';
import { exportQd540Table1, previewQd540Table1, type Qd540Table1Row } from '@/shared/api/qd540.api';
import { apiErrorMessage } from '@/shared/api/api-error';

function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

type FormValues = {
  range: [Dayjs, Dayjs];
  branchId?: string;
};

export function Qd540ExportPage() {
  const { t } = useTranslation('inventory', { keyPrefix: 'qd540Export' });
  const [form] = Form.useForm<FormValues>();
  const [branches, setBranches] = useState<BranchListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [rows, setRows] = useState<Qd540Table1Row[]>([]);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [skippedRows, setSkippedRows] = useState(0);

  useEffect(() => {
    void fetchBranches()
      .then(setBranches)
      .catch(() => message.error(t('messages.loadBranchesFailed')));
    form.setFieldsValue({
      range: [dayjs().startOf('month'), dayjs()],
    });
  }, [form, t]);

  const branchOptions = useMemo(
    () => branches.map((b) => ({ value: b.id, label: `${b.branchCode} — ${b.branchName}` })),
    [branches],
  );

  const buildQuery = useCallback(async () => {
    const values = await form.validateFields();
    const [from, to] = values.range;
    return {
      from: from.format('YYYY-MM-DD'),
      to: to.format('YYYY-MM-DD'),
      branchId: values.branchId,
    };
  }, [form]);

  const handlePreview = async () => {
    try {
      setLoading(true);
      const query = await buildQuery();
      const result = await previewQd540Table1(query);
      setRows(result.rows);
      setWarnings(result.warnings);
      setSkippedRows(result.skippedRows);
      message.success(t('messages.previewOk', { count: result.rows.length }));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.previewFailed')));
    } finally {
      setLoading(false);
    }
  };

  const handleExportCsv = async () => {
    try {
      setExporting(true);
      const query = await buildQuery();
      const blob = await exportQd540Table1(query);
      downloadBlob(blob, `qd540-table1-${query.from}-${query.to}.csv`);
      message.success(t('messages.exportOk'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.exportFailed')));
    } finally {
      setExporting(false);
    }
  };

  const columns: ColumnsType<Qd540Table1Row> = useMemo(
    () => [
      { title: t('columns.maThuoc'), dataIndex: 'maThuoc', width: 120, ellipsis: true },
      { title: t('columns.tenThuoc'), dataIndex: 'tenThuoc', ellipsis: true },
      { title: t('columns.soLo'), dataIndex: 'soLo', width: 90 },
      { title: t('columns.soLuongNhap'), dataIndex: 'soLuongNhap', width: 90, align: 'right' },
      { title: t('columns.soLuongBan'), dataIndex: 'soLuongBan', width: 90, align: 'right' },
      { title: t('columns.maCoSoBanLe'), dataIndex: 'maCoSoBanLe', width: 110 },
    ],
    [t],
  );

  return (
    <Card title={t('title')} extra={<span style={{ fontSize: 12, color: '#888' }}>{t('subtitle')}</span>}>
      <Alert type="info" showIcon message={t('hint')} style={{ marginBottom: 16 }} />
      <Form form={form} layout="inline" style={{ marginBottom: 16, flexWrap: 'wrap', gap: 8 }}>
        <Form.Item
          name="range"
          label={t('form.range')}
          rules={[{ required: true, message: t('form.rangeRequired') }]}
        >
          <DatePicker.RangePicker allowClear={false} />
        </Form.Item>
        <Form.Item name="branchId" label={t('form.branch')}>
          <Select allowClear showSearch optionFilterProp="label" style={{ minWidth: 220 }} options={branchOptions} />
        </Form.Item>
        <Form.Item>
          <Space>
            <Button icon={<ReloadOutlined />} loading={loading} onClick={() => void handlePreview()}>
              {t('actions.preview')}
            </Button>
            <Button
              type="primary"
              icon={<DownloadOutlined />}
              loading={exporting}
              onClick={() => void handleExportCsv()}
            >
              {t('actions.exportCsv')}
            </Button>
          </Space>
        </Form.Item>
      </Form>

      {warnings.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={t('warningsTitle', { count: warnings.length, skipped: skippedRows })}
          description={
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {warnings.slice(0, 8).map((w) => (
                <li key={w}>{w}</li>
              ))}
              {warnings.length > 8 ? <li>{t('warningsMore', { count: warnings.length - 8 })}</li> : null}
            </ul>
          }
          style={{ marginBottom: 12 }}
        />
      )}

      <Table
        rowKey={(row, index) => `${row.maThuoc}-${row.soLo}-${index}`}
        size="small"
        loading={loading}
        columns={columns}
        dataSource={rows}
        pagination={{ pageSize: 20, showSizeChanger: true }}
        locale={{ emptyText: t('empty') }}
      />
    </Card>
  );
}
