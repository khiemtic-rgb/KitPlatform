import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Button,
  Card,
  Form,
  Input,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd';
import { ApiOutlined, CloudSyncOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchCsdlDuocSyncLog,
  fetchTenantCsdlDuocLink,
  retryCsdlDuocStockIn,
  retryCsdlDuocStockOut,
  testTenantCsdlDuocLink,
  updateTenantCsdlDuocLink,
  type CsdlDuocSyncLogRow,
  type TenantCsdlDuocLink,
  type UpdateTenantCsdlDuocLinkRequest,
} from '@/shared/api/csdl-duoc-link.api';
import { useCanInventoryWrite } from '@/shared/auth/usePermission';

type FormValues = {
  enabled: boolean;
  environment: 'sandbox' | 'live';
  username: string;
  password: string;
  practiceLicenseCode: string;
  enableStockOutSync: boolean;
  enableStockInSync: boolean;
};

function statusColor(status: string): string {
  switch (status) {
    case 'Connected':
      return 'success';
    case 'Configured':
      return 'processing';
    case 'Error':
      return 'error';
    case 'Disabled':
      return 'default';
    default:
      return 'warning';
  }
}

function syncStatusColor(status: string): string {
  switch (status) {
    case 'submitted':
      return 'success';
    case 'pending':
      return 'processing';
    case 'skipped':
      return 'default';
    case 'error':
      return 'error';
    default:
      return 'warning';
  }
}

export function CsdlDuocSettingsPage() {
  const { t } = useTranslation('system', { keyPrefix: 'csdlSettings' });
  const { message } = App.useApp();
  const navigate = useNavigate();
  const canWrite = useCanInventoryWrite();
  const [form] = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [link, setLink] = useState<TenantCsdlDuocLink | null>(null);
  const [syncLog, setSyncLog] = useState<CsdlDuocSyncLogRow[]>([]);
  const [syncLoading, setSyncLoading] = useState(false);
  const [retryingId, setRetryingId] = useState<string | null>(null);

  const applyLink = (data: TenantCsdlDuocLink) => {
    setLink(data);
    form.setFieldsValue({
      enabled: data.enabled,
      environment: data.environment === 'live' ? 'live' : 'sandbox',
      username: data.username ?? '',
      password: '',
      practiceLicenseCode: data.practiceLicenseCode ?? '',
      enableStockOutSync: data.enableStockOutSync,
      enableStockInSync: data.enableStockInSync,
    });
  };

  const loadSyncLog = useCallback(async () => {
    setSyncLoading(true);
    try {
      setSyncLog(await fetchCsdlDuocSyncLog(40));
    } catch (error) {
      message.error(apiErrorMessage(error, t('loadFailed')));
    } finally {
      setSyncLoading(false);
    }
  }, [message, t]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    void fetchTenantCsdlDuocLink()
      .then((data) => {
        if (!cancelled) applyLink(data);
      })
      .catch((error) => {
        if (!cancelled) message.error(apiErrorMessage(error, t('loadFailed')));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    void loadSyncLog();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const buildPayload = (values: FormValues): UpdateTenantCsdlDuocLinkRequest => ({
    enabled: values.enabled,
    environment: values.environment,
    username: values.username.trim() || null,
    password: values.password.trim() ? values.password : null,
    practiceLicenseCode: values.practiceLicenseCode.trim() || null,
    enableStockOutSync: values.enableStockOutSync,
    enableStockInSync: values.enableStockInSync,
  });

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const saved = await updateTenantCsdlDuocLink(buildPayload(values));
      applyLink(saved);
      message.success(t('saveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onTest = async () => {
    setTesting(true);
    try {
      const tested = await testTenantCsdlDuocLink();
      applyLink(tested);
      if (tested.status === 'Connected') message.success(t('testSuccess'));
      else message.warning(tested.lastError || t('testFailed'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('testFailed')));
    } finally {
      setTesting(false);
    }
  };

  const onRetry = async (row: CsdlDuocSyncLogRow) => {
    setRetryingId(row.id);
    try {
      if (row.direction === 'stock-in') {
        await retryCsdlDuocStockIn(row.salesOrderId);
      } else {
        await retryCsdlDuocStockOut(row.salesOrderId);
      }
      message.success(t('syncLogRetryOk'));
      await loadSyncLog();
    } catch (error) {
      message.error(apiErrorMessage(error, t('syncLogRetryFail')));
    } finally {
      setRetryingId(null);
    }
  };

  const columns: ColumnsType<CsdlDuocSyncLogRow> = [
    {
      title: t('syncLogColTime'),
      dataIndex: 'createdAt',
      width: 160,
      render: (v: string) => (v ? new Date(v).toLocaleString() : '—'),
    },
    {
      title: t('syncLogColDir'),
      dataIndex: 'direction',
      width: 100,
    },
    {
      title: t('syncLogColDoc'),
      dataIndex: 'orderNumber',
      ellipsis: true,
      render: (v: string | undefined, row) => v || row.salesOrderId.slice(0, 8),
    },
    {
      title: t('syncLogColStatus'),
      dataIndex: 'status',
      width: 110,
      render: (v: string) => <Tag color={syncStatusColor(v)}>{v}</Tag>,
    },
    {
      title: t('syncLogColLines'),
      width: 90,
      render: (_, row) => `${row.lineCount}/${row.skippedLineCount}`,
    },
    {
      title: t('syncLogColRemote'),
      dataIndex: 'remoteTransactionId',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: t('syncLogColError'),
      dataIndex: 'errorMessage',
      ellipsis: true,
      render: (v?: string) => v || '—',
    },
    {
      title: '',
      width: 100,
      render: (_, row) =>
        canWrite && (row.status === 'error' || row.status === 'skipped') ? (
          <Button
            size="small"
            loading={retryingId === row.id}
            onClick={() => void onRetry(row)}
          >
            {t('syncLogRetry')}
          </Button>
        ) : null,
    },
  ];

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <Alert type="info" showIcon message={t('introTitle')} description={t('introBody')} />

      {link && (
        <Card size="small" title={t('activeSourceTitle')}>
          <Space wrap>
            <Typography.Text>{t('status')}:</Typography.Text>
            <Tag color={statusColor(link.status)}>{t(`statusValues.${link.status}`, link.status)}</Tag>
            <Typography.Text type="secondary">
              {t('activeSource')}: {link.activeAccountLabel}
              {link.activeAccountUsername ? ` (${link.activeAccountUsername})` : ''}
            </Typography.Text>
          </Space>
          {link.lastError && (
            <Alert style={{ marginTop: 12 }} type="error" showIcon message={link.lastError} />
          )}
          {link.lastCheckAt && (
            <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
              {t('lastCheck')}: {new Date(link.lastCheckAt).toLocaleString()}
            </Typography.Paragraph>
          )}
        </Card>
      )}

      <Card title={t('title')} loading={loading}>
        <Form form={form} layout="vertical" disabled={!canWrite}>
          <Form.Item name="enabled" label={t('enabled')} valuePropName="checked">
            <Switch checkedChildren={t('on')} unCheckedChildren={t('off')} />
          </Form.Item>
          <Form.Item name="environment" label={t('environment')} rules={[{ required: true }]}>
            <Select
              options={[
                { value: 'sandbox', label: t('envSandbox') },
                { value: 'live', label: t('envLive') },
              ]}
            />
          </Form.Item>
          <Form.Item name="username" label={t('username')}>
            <Input autoComplete="off" placeholder={t('usernamePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="password"
            label={t('password')}
            extra={
              link?.passwordConfigured ? t('passwordKeepHint') : t('passwordRequiredHint')
            }
          >
            <Input.Password
              autoComplete="new-password"
              placeholder={link?.passwordConfigured ? '••••••••' : t('passwordPlaceholder')}
            />
          </Form.Item>
          <Form.Item name="practiceLicenseCode" label={t('practiceLicense')}>
            <Input placeholder={t('practiceLicensePlaceholder')} />
          </Form.Item>
          <Form.Item
            name="enableStockOutSync"
            label={t('stockOutSync')}
            valuePropName="checked"
            extra={t('stockOutSyncHint')}
          >
            <Switch />
          </Form.Item>
          <Form.Item
            name="enableStockInSync"
            label={t('stockInSync')}
            valuePropName="checked"
            extra={t('stockInSyncHint')}
          >
            <Switch />
          </Form.Item>
          <Space wrap>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              loading={saving}
              disabled={!canWrite}
              onClick={() => void onSave()}
            >
              {t('save')}
            </Button>
            <Button
              icon={<ApiOutlined />}
              loading={testing}
              disabled={!canWrite || !link?.passwordConfigured}
              onClick={() => void onTest()}
            >
              {t('test')}
            </Button>
            <Button
              icon={<CloudSyncOutlined />}
              onClick={() => navigate('/catalog/national-drugs')}
            >
              {t('openLookup')}
            </Button>
          </Space>
        </Form>
      </Card>

      <Card
        title={t('syncLogTitle')}
        extra={
          <Button
            icon={<ReloadOutlined />}
            loading={syncLoading}
            onClick={() => void loadSyncLog()}
          >
            {t('syncLogRefresh')}
          </Button>
        }
      >
        <Table
          size="small"
          rowKey="id"
          loading={syncLoading}
          columns={columns}
          dataSource={syncLog}
          pagination={false}
          locale={{ emptyText: t('syncLogEmpty') }}
          scroll={{ x: 900 }}
        />
      </Card>
    </Space>
  );
}
