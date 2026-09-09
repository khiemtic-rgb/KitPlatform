import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
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
  message,
} from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  attachKitSalesLeadPsid,
  detachKitSalesLeadPsid,
  fetchKitSalesFacebookPsids,
  fetchKitSalesFacebookSettings,
  fetchKitSalesGeminiSettings,
  fetchKitSalesLeads,
  saveKitSalesFacebookSettings,
  saveKitSalesGeminiSettings,
  testKitSalesFacebookSettings,
  testKitSalesGeminiSettings,
  type KitSalesFacebookPsid,
  type KitSalesFacebookSettings,
  type KitSalesGeminiSettings,
  type KitSalesLead,
} from '@/shared/api/kit-sales.api';

export function KitSalesSettingsPage() {
  const { t } = useTranslation('kitSales');
  const navigate = useNavigate();
  const [form] = Form.useForm();
  const [geminiForm] = Form.useForm();
  const [settings, setSettings] = useState<KitSalesFacebookSettings | null>(null);
  const [gemini, setGemini] = useState<KitSalesGeminiSettings | null>(null);
  const [leads, setLeads] = useState<KitSalesLead[]>([]);
  const [psids, setPsids] = useState<KitSalesFacebookPsid[]>([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [savingGemini, setSavingGemini] = useState(false);
  const [testing, setTesting] = useState(false);
  const [testingGemini, setTestingGemini] = useState(false);
  const [attaching, setAttaching] = useState(false);
  const [leadId, setLeadId] = useState<string>();
  const [psid, setPsid] = useState('');

  const load = async () => {
    setLoading(true);
    try {
      const [next, ai, rows, identities] = await Promise.all([
        fetchKitSalesFacebookSettings(),
        fetchKitSalesGeminiSettings(),
        fetchKitSalesLeads({ limit: 200 }),
        fetchKitSalesFacebookPsids(),
      ]);
      setSettings(next);
      setGemini(ai);
      setLeads(rows);
      setPsids(identities);
      form.setFieldsValue({
        enabled: next.enabled,
        pageId: next.pageId ?? '',
        meLink: next.meLink,
        pageUrl: next.pageUrl,
        phcUrl: next.phcUrl,
        pageAccessToken: '',
        verifyToken: '',
        appSecret: '',
      });
      geminiForm.setFieldsValue({
        geminiApiKey: '',
        textModel: ai.textModel ?? '',
      });
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.loadFailed')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const next = await saveKitSalesFacebookSettings({
        enabled: Boolean(values.enabled),
        pageAccessToken: values.pageAccessToken?.trim() || undefined,
        verifyToken: values.verifyToken?.trim() || undefined,
        appSecret: values.appSecret?.trim() || undefined,
        pageId: values.pageId?.trim() || undefined,
        meLink: values.meLink?.trim() || undefined,
        pageUrl: values.pageUrl?.trim() || undefined,
        phcUrl: values.phcUrl?.trim() || undefined,
      });
      setSettings(next);
      form.setFieldsValue({ pageAccessToken: '', verifyToken: '', appSecret: '' });
      message.success(t('settings.saved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  const onSaveGemini = async () => {
    const values = await geminiForm.validateFields();
    setSavingGemini(true);
    try {
      const next = await saveKitSalesGeminiSettings({
        geminiApiKey: values.geminiApiKey?.trim() || undefined,
        textModel: values.textModel?.trim() || undefined,
      });
      setGemini(next);
      geminiForm.setFieldsValue({ geminiApiKey: '' });
      message.success(t('settings.geminiSaved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.geminiSaveFailed')));
    } finally {
      setSavingGemini(false);
    }
  };

  const onTestGemini = async () => {
    setTestingGemini(true);
    try {
      const result = await testKitSalesGeminiSettings();
      if (result.ok) {
        message.success(t('settings.geminiTestOk', { model: result.model ?? 'Gemini' }));
      } else {
        message.error(result.error || t('settings.geminiTestFailed'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.geminiTestFailed')));
    } finally {
      setTestingGemini(false);
    }
  };

  const onTest = async () => {
    setTesting(true);
    try {
      const result = await testKitSalesFacebookSettings();
      if (result.ok) {
        message.success(t('settings.testOk', { name: result.pageName ?? result.pageId ?? 'Page' }));
      } else {
        message.error(result.error || t('settings.testFailed'));
      }
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.testFailed')));
    } finally {
      setTesting(false);
    }
  };

  const onAttach = async () => {
    if (!leadId || !psid.trim()) return;
    setAttaching(true);
    try {
      await attachKitSalesLeadPsid(leadId, psid.trim());
      setPsid('');
      setPsids(await fetchKitSalesFacebookPsids());
      message.success(t('settings.psidSaved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.psidFailed')));
    } finally {
      setAttaching(false);
    }
  };

  const onDetach = async (id: string) => {
    try {
      await detachKitSalesLeadPsid(id);
      setPsids(await fetchKitSalesFacebookPsids());
      message.success(t('settings.psidRemoved'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.psidFailed')));
    }
  };

  const copy = async (value: string) => {
    await navigator.clipboard.writeText(value);
    message.success(t('chat.copied'));
  };

  return (
    <Space direction="vertical" size="middle" style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {t('settings.title')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
          {t('settings.blurb')}
        </Typography.Paragraph>
      </div>

      <Alert type="info" showIcon message={t('settings.warn')} />

      <Card
        size="small"
        title={t('settings.geminiTitle')}
        loading={loading}
        extra={
          gemini && (
            <Tag color={gemini.ready ? 'green' : 'default'}>
              {gemini.ready ? t('settings.geminiReady') : t('settings.geminiOff')}
            </Tag>
          )
        }
      >
        <Typography.Paragraph type="secondary">{t('settings.geminiHint')}</Typography.Paragraph>
        <Form form={geminiForm} layout="vertical">
          <Form.Item
            name="geminiApiKey"
            label={t('settings.geminiKey')}
            extra={
              gemini?.hasApiKey
                ? t('settings.tokenKept', { last4: gemini.apiKeyLast4 ?? '****' })
                : t('settings.geminiEmpty')
            }
          >
            <Input.Password autoComplete="new-password" placeholder={t('settings.geminiPlaceholder')} />
          </Form.Item>
          <Form.Item name="textModel" label={t('settings.geminiModel')} extra={t('settings.geminiModelHint')}>
            <Input placeholder="gemini-flash-latest" />
          </Form.Item>
        </Form>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          <Typography.Link href="https://aistudio.google.com/apikey" target="_blank" rel="noreferrer">
            {t('settings.geminiGetKey')}
          </Typography.Link>
        </Typography.Paragraph>
        <Space wrap>
          <Button type="primary" onClick={() => void onSaveGemini()} loading={savingGemini}>
            {t('settings.save')}
          </Button>
          <Button onClick={() => void onTestGemini()} loading={testingGemini} disabled={!gemini?.hasApiKey}>
            {t('settings.geminiTest')}
          </Button>
        </Space>
      </Card>

      <Card
        size="small"
        title={t('settings.pageTitle')}
        loading={loading}
        extra={
          settings && (
            <Tag color={settings.sendReady ? 'green' : 'default'}>
              {settings.sendReady ? t('settings.sendReady') : t('settings.sendOff')}
            </Tag>
          )
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item name="enabled" label={t('settings.enabled')} valuePropName="checked">
            <Switch />
          </Form.Item>
          <Form.Item
            name="pageAccessToken"
            label={t('settings.pageToken')}
            extra={
              settings?.hasPageToken
                ? t('settings.tokenKept', { last4: settings.pageTokenLast4 ?? '****' })
                : t('settings.tokenEmpty')
            }
          >
            <Input.Password autoComplete="new-password" placeholder={t('settings.tokenPlaceholder')} />
          </Form.Item>
          <Form.Item
            name="verifyToken"
            label={t('settings.verifyToken')}
            extra={settings?.hasVerifyToken ? t('settings.secretKept') : t('settings.secretEmpty')}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item
            name="appSecret"
            label={t('settings.appSecret')}
            extra={settings?.hasAppSecret ? t('settings.secretKept') : t('settings.secretEmpty')}
          >
            <Input.Password autoComplete="new-password" />
          </Form.Item>
          <Form.Item name="pageId" label={t('settings.pageId')}>
            <Input />
          </Form.Item>
          <Form.Item name="pageUrl" label={t('settings.pageUrl')}>
            <Input />
          </Form.Item>
          <Form.Item name="meLink" label={t('settings.meLink')}>
            <Input />
          </Form.Item>
          <Form.Item name="phcUrl" label={t('settings.phcUrl')}>
            <Input />
          </Form.Item>
        </Form>
        {settings && (
          <Space direction="vertical" style={{ width: '100%', marginBottom: 12 }}>
            <Typography.Text type="secondary">{t('settings.webhook')}</Typography.Text>
            <Space.Compact style={{ width: '100%' }}>
              <Input value={settings.webhookUrl} readOnly />
              <Button onClick={() => void copy(settings.webhookUrl)}>{t('settings.copy')}</Button>
            </Space.Compact>
          </Space>
        )}
        <Space wrap>
          <Button type="primary" onClick={() => void onSave()} loading={saving}>
            {t('settings.save')}
          </Button>
          <Button onClick={() => void onTest()} loading={testing} disabled={!settings?.hasPageToken}>
            {t('settings.test')}
          </Button>
        </Space>
      </Card>

      <Card size="small" title={t('settings.psidTitle')} loading={loading}>
        <Typography.Paragraph type="secondary">{t('settings.psidHint')}</Typography.Paragraph>
        <Space wrap style={{ marginBottom: 16, width: '100%' }}>
          <Select
            showSearch
            optionFilterProp="label"
            placeholder={t('settings.pickLead')}
            style={{ minWidth: 280 }}
            value={leadId}
            onChange={setLeadId}
            options={leads.map((lead) => ({ value: lead.id, label: lead.businessName }))}
          />
          <Input
            value={psid}
            onChange={(event) => setPsid(event.target.value)}
            placeholder={t('settings.psidPlaceholder')}
            style={{ minWidth: 220 }}
          />
          <Button
            type="primary"
            onClick={() => void onAttach()}
            loading={attaching}
            disabled={!leadId || !psid.trim()}
          >
            {t('settings.psidAttach')}
          </Button>
        </Space>
        <Table
          size="small"
          rowKey="leadId"
          dataSource={psids}
          pagination={false}
          columns={[
            {
              title: t('settings.lead'),
              dataIndex: 'businessName',
              render: (name: string, row) => (
                <Typography.Link onClick={() => navigate(`/kit-sales/leads/${row.leadId}`)}>
                  {name}
                </Typography.Link>
              ),
            },
            { title: 'PSID', dataIndex: 'psid' },
            {
              title: '',
              key: 'actions',
              render: (_, row) => (
                <Button size="small" danger onClick={() => void onDetach(row.leadId)}>
                  {t('settings.psidDetach')}
                </Button>
              ),
            },
          ]}
        />
      </Card>
    </Space>
  );
}
