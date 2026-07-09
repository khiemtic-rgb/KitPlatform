import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { App, Alert, Button, Card, Form, Input, Select, Space, Typography } from 'antd';
import { PrinterOutlined } from '@ant-design/icons';import {
  fetchBatchModeSettings,
  updateBatchModeSettings,
  updateReceiptSettings,
  type TenantBatchModeValue,
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
        const [receipt, mode] = await Promise.all([
          loadReceiptStoreSettings(true),
          fetchBatchModeSettings(),
        ]);
        receiptForm.setFieldsValue(receipt);
        setBatchMode(mode);
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
    </Space>
  );
}
