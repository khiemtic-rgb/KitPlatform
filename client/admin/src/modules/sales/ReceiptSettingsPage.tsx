import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { App, Alert, Button, Card, Form, Input, Select, Space, Switch, Tag, Typography } from 'antd';
import { MedicineBoxOutlined, PrinterOutlined } from '@ant-design/icons';
import {
  fetchPharmacyConsultationAiSettings,
  updatePharmacyConsultationAiSettings,
  type PharmacyConsultationAiSettings,
} from '@/shared/api/pharmacy-consultation.api';
import {
  fetchBatchModeSettings,
  updateBatchModeSettings,
  fetchRxSettings,
  updateRxSettings,
  updateReceiptSettings,
  type TenantBatchModeValue,
  type TenantRxSettings,
} from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import { useBatchModeLabels } from '@/shared/i18n/use-batch-mode-labels';
import {
  clearReceiptSettingsCache,
  loadReceiptStoreSettings,
  type ReceiptStoreSettings,
} from '@/modules/sales/receipt-settings';
import { printReceiptTestPage } from '@/modules/sales/receipt-test-print';

type ReceiptForm = ReceiptStoreSettings;

export function ReceiptSettingsPage() {
  const { t } = useTranslation('sales', { keyPrefix: 'receiptSettings' });
  const { message } = App.useApp();
  const { batchModeOptions, batchModeHint } = useBatchModeLabels();
  const canWrite = useHasPermission('sales.write');
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [receiptForm] = Form.useForm<ReceiptForm>();
  const [loading, setLoading] = useState(true);
  const [savingReceipt, setSavingReceipt] = useState(false);
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');
  const [savingBatchMode, setSavingBatchMode] = useState(false);
  const [rxSettings, setRxSettings] = useState<TenantRxSettings>({ enforcementMode: 'off', posBlockedAudit: true });
  const [savingRxSettings, setSavingRxSettings] = useState(false);
  const [consultationAi, setConsultationAi] = useState<PharmacyConsultationAiSettings>({
    geminiApiKeySecretRef: 'GEMINI_API_KEY',
    textModel: 'gemini-2.5-flash-lite',
    geminiApiKeyConfigured: false,
    envFallbackAvailable: false,
    contentFallbackAvailable: false,
  });
  const [consultationAiKey, setConsultationAiKey] = useState('');
  const [clearConsultationAiKey, setClearConsultationAiKey] = useState(false);
  const [savingConsultationAi, setSavingConsultationAi] = useState(false);
  const [testPrinting, setTestPrinting] = useState(false);

  useEffect(() => {
    if (searchParams.get('tab') === 'customer-app') {
      navigate('/system/customer-app-settings', { replace: true });
    }
  }, [navigate, searchParams]);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const [receipt, mode, rx, consultationAiSettings] = await Promise.all([
          loadReceiptStoreSettings(true),
          fetchBatchModeSettings(),
          fetchRxSettings(),
          fetchPharmacyConsultationAiSettings(),
        ]);
        receiptForm.setFieldsValue(receipt);
        setBatchMode(mode);
        setRxSettings(rx);
        setConsultationAi(consultationAiSettings);
        setConsultationAiKey('');
        setClearConsultationAiKey(false);
      } catch (error) {
        message.error(apiErrorMessage(error, t('messages.loadFailed')));
      } finally {
        setLoading(false);
      }
    })();
  }, [receiptForm, message, t]);

  const onSaveReceipt = async () => {
    const values = await receiptForm.validateFields();
    setSavingReceipt(true);
    try {
      const saved = await updateReceiptSettings({
        name: values.name.trim(),
        tagline: values.tagline?.trim() || undefined,
        phone: values.phone?.trim() || undefined,
        address: values.address?.trim() || undefined,
      });
      clearReceiptSettingsCache();
      await loadReceiptStoreSettings(true);
      receiptForm.setFieldsValue(saved);
      message.success(t('messages.receiptSaveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.receiptSaveFailed')));
    } finally {
      setSavingReceipt(false);
    }
  };

  const onSaveBatchMode = async () => {
    setSavingBatchMode(true);
    try {
      const saved = await updateBatchModeSettings(batchMode);
      setBatchMode(saved);
      message.success(t('messages.batchSaveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('messages.batchSaveFailed')));
    } finally {
      setSavingBatchMode(false);
    }
  };

  const onTestPrint = async () => {
    setTestPrinting(true);
    try {
      const ok = await printReceiptTestPage();
      if (!ok) message.warning(t('printGuide.popupBlocked'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('printGuide.testFailed')));
    } finally {
      setTestPrinting(false);
    }
  };

  const onSaveRxSettings = async () => {
    setSavingRxSettings(true);
    try {
      const saved = await updateRxSettings(rxSettings);
      setRxSettings(saved);
      message.success(t('rxCard.saveSuccess'));
    } catch (error) {
      message.error(apiErrorMessage(error, t('rxCard.saveFailed')));
    } finally {
      setSavingRxSettings(false);
    }
  };

  const onSaveConsultationAi = async () => {
    setSavingConsultationAi(true);
    try {
      const saved = await updatePharmacyConsultationAiSettings({
        geminiApiKeySecretRef: consultationAi.geminiApiKeySecretRef?.trim() || 'GEMINI_API_KEY',
        textModel: consultationAi.textModel.trim() || 'gemini-2.5-flash-lite',
        geminiApiKey: clearConsultationAiKey ? '' : consultationAiKey.trim() || undefined,
      });
      setConsultationAi(saved);
      setConsultationAiKey('');
      setClearConsultationAiKey(false);
      message.success('Đã lưu cấu hình AI tư vấn quầy');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được cấu hình AI'));
    } finally {
      setSavingConsultationAi(false);
    }
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Card title={t('receiptCard.title')} loading={loading}>
        <Form form={receiptForm} layout="vertical" style={{ maxWidth: 520 }} disabled={!canWrite}>
          <Form.Item
            name="name"
            label={t('receiptCard.storeName')}
            rules={[{ required: true, message: t('receiptCard.storeNameRequired') }]}
          >
            <Input placeholder={t('receiptCard.placeholders.storeName')} />
          </Form.Item>
          <Form.Item name="tagline" label={t('receiptCard.tagline')}>
            <Input placeholder={t('receiptCard.placeholders.tagline')} />
          </Form.Item>
          <Form.Item name="phone" label={t('receiptCard.phone')}>
            <Input placeholder={t('receiptCard.placeholders.phone')} />
          </Form.Item>
          <Form.Item name="address" label={t('receiptCard.address')}>
            <Input.TextArea rows={2} placeholder={t('receiptCard.placeholders.address')} />
          </Form.Item>
          {canWrite ? (
            <Button type="primary" loading={savingReceipt} onClick={() => void onSaveReceipt()}>
              {t('receiptCard.save')}
            </Button>
          ) : null}
        </Form>
      </Card>

      <Card title={t('printGuide.title')} loading={loading}>
        <Space direction="vertical" size={12} style={{ maxWidth: 640, width: '100%' }}>
          <Typography.Paragraph style={{ marginBottom: 0 }}>{t('printGuide.intro')}</Typography.Paragraph>
          <Typography.Text type="secondary" style={{ fontSize: 13, whiteSpace: 'pre-line' }}>
            {t('printGuide.steps')}
          </Typography.Text>
          <Alert type="info" showIcon message={t('printGuide.noteTitle')} description={t('printGuide.noteBody')} />
          <Button icon={<PrinterOutlined />} loading={testPrinting} onClick={() => void onTestPrint()}>
            {t('printGuide.testButton')}
          </Button>
        </Space>
      </Card>

      <Card title={t('batchCard.title')} loading={loading}>
        <Space direction="vertical" size={12} style={{ maxWidth: 520, width: '100%' }}>
          <Select
            style={{ width: '100%' }}
            disabled={!canWrite}
            value={batchMode}
            options={batchModeOptions}
            onChange={setBatchMode}
          />
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {batchModeHint(batchMode)}
          </Typography.Text>
          {canWrite ? (
            <Button type="primary" loading={savingBatchMode} onClick={() => void onSaveBatchMode()}>
              {t('batchCard.save')}
            </Button>
          ) : null}
        </Space>
      </Card>

      <Card title={t('rxCard.title')} loading={loading}>
        <Space direction="vertical" size={12} style={{ maxWidth: 520, width: '100%' }}>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {t('rxCard.hint')}
          </Typography.Text>
          <Select
            style={{ width: '100%' }}
            disabled={!canWrite}
            value={rxSettings.enforcementMode}
            options={[
              { value: 'off', label: t('rxCard.modeOff') },
              { value: 'strict', label: t('rxCard.modeStrict') },
              { value: 'warn', label: t('rxCard.modeWarn') },
            ]}
            onChange={(enforcementMode: TenantRxSettings['enforcementMode']) =>
              setRxSettings((prev: TenantRxSettings) => ({ ...prev, enforcementMode }))
            }
          />
          <Space>
            <Switch
              checked={rxSettings.posBlockedAudit}
              disabled={!canWrite}
              onChange={(posBlockedAudit: boolean) =>
                setRxSettings((prev: TenantRxSettings) => ({ ...prev, posBlockedAudit }))
              }
            />
            <Typography.Text>{t('rxCard.auditLabel')}</Typography.Text>
          </Space>
          {canWrite ? (
            <Button type="primary" loading={savingRxSettings} onClick={() => void onSaveRxSettings()}>
              {t('rxCard.save')}
            </Button>
          ) : null}
        </Space>
      </Card>

      <Card
        title={
          <Space>
            <MedicineBoxOutlined />
            <span>AI tư vấn quầy (POS)</span>
          </Space>
        }
        loading={loading}
      >
        <Space direction="vertical" size={12} style={{ maxWidth: 520, width: '100%' }}>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            Dùng cho nút <strong>Tư vấn</strong> trên POS — trích xuất triệu chứng bằng Gemini. Key lưu theo
            từng nhà thuốc (tenant). Không hiển thị lại key đã lưu.
          </Typography.Text>
          <div>
            <Typography.Text type="secondary">Trạng thái</Typography.Text>
            <div style={{ marginTop: 4 }}>
              {consultationAi.geminiApiKeyConfigured ? (
                <Tag color="success">Đã có API key</Tag>
              ) : (
                <Tag>Chưa cấu hình key</Tag>
              )}
              {consultationAi.envFallbackAvailable ? (
                <Tag color="blue">Có fallback env</Tag>
              ) : null}
              {consultationAi.contentFallbackAvailable ? (
                <Tag color="purple">Dùng key Content Park</Tag>
              ) : null}
            </div>
          </div>
          <div>
            <Typography.Text type="secondary">Secret ref (env trên server)</Typography.Text>
            <Input
              style={{ marginTop: 4 }}
              disabled={!canWrite}
              value={consultationAi.geminiApiKeySecretRef ?? 'GEMINI_API_KEY'}
              onChange={(e) =>
                setConsultationAi((prev) => ({ ...prev, geminiApiKeySecretRef: e.target.value }))
              }
              placeholder="GEMINI_API_KEY"
            />
          </div>
          <div>
            <Typography.Text type="secondary">Gemini API key (lưu DB tenant)</Typography.Text>
            <Input.Password
              style={{ marginTop: 4 }}
              disabled={!canWrite || clearConsultationAiKey}
              value={consultationAiKey}
              onChange={(e) => setConsultationAiKey(e.target.value)}
              placeholder={
                consultationAi.geminiApiKeyConfigured
                  ? 'Để trống = giữ key cũ'
                  : 'Dán API key Google AI Studio'
              }
            />
          </div>
          {canWrite ? (
            <Space>
              <Switch
                checked={clearConsultationAiKey}
                onChange={(checked) => {
                  setClearConsultationAiKey(checked);
                  if (checked) setConsultationAiKey('');
                }}
              />
              <Typography.Text>Xóa key đã lưu trên DB</Typography.Text>
            </Space>
          ) : null}
          <div>
            <Typography.Text type="secondary">Model text</Typography.Text>
            <Select
              style={{ width: '100%', marginTop: 4 }}
              disabled={!canWrite}
              value={consultationAi.textModel}
              options={[
                { value: 'gemini-2.5-flash-lite', label: 'gemini-2.5-flash-lite (nhanh)' },
                { value: 'gemini-flash-latest', label: 'gemini-flash-latest' },
              ]}
              onChange={(value) => setConsultationAi((prev) => ({ ...prev, textModel: value }))}
            />
          </div>
          <Alert
            type="info"
            showIcon
            message="Fallback"
            description="Nếu chưa lưu key ở đây, hệ thống dùng key Gemini của Content Park (Cài đặt Content → AI), rồi secret ref / GEMINI_API_KEY."
          />
          {canWrite ? (
            <Button type="primary" loading={savingConsultationAi} onClick={() => void onSaveConsultationAi()}>
              Lưu cấu hình AI
            </Button>
          ) : null}
        </Space>
      </Card>
    </Space>
  );
}
