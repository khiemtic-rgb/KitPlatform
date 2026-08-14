import { useEffect, useState } from 'react';
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
  Tag,
  Typography,
} from 'antd';
import { ApiOutlined, CloudSyncOutlined, SaveOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchTenantCsdlDuocLink,
  testTenantCsdlDuocLink,
  updateTenantCsdlDuocLink,
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
    </Space>
  );
}
