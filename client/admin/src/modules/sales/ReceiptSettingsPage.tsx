import { useEffect, useState } from 'react';
import { App, Button, Card, Form, Input, Select, Space, Typography } from 'antd';
import {
  fetchBatchModeSettings,
  updateBatchModeSettings,
  updateReceiptSettings,
  type TenantBatchModeValue,
} from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import {
  clearReceiptSettingsCache,
  loadReceiptStoreSettings,
  type ReceiptStoreSettings,
} from '@/modules/sales/receipt-settings';
import { BATCH_MODE_HINTS, BATCH_MODE_OPTIONS } from '@/modules/sales/tenant-batch-mode';

type ReceiptForm = ReceiptStoreSettings;

export function ReceiptSettingsPage() {
  const { message } = App.useApp();
  const canWrite = useHasPermission('sales.write');
  const [receiptForm] = Form.useForm<ReceiptForm>();
  const [loading, setLoading] = useState(true);
  const [savingReceipt, setSavingReceipt] = useState(false);
  const [batchMode, setBatchMode] = useState<TenantBatchModeValue>('suggest');
  const [savingBatchMode, setSavingBatchMode] = useState(false);

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
        message.error(apiErrorMessage(error, 'Không tải được cài đặt bán hàng'));
      } finally {
        setLoading(false);
      }
    })();
  }, [receiptForm, message]);

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
      message.success('Đã lưu cài đặt phiếu in');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được cài đặt phiếu in'));
    } finally {
      setSavingReceipt(false);
    }
  };

  const onSaveBatchMode = async () => {
    setSavingBatchMode(true);
    try {
      const saved = await updateBatchModeSettings(batchMode);
      setBatchMode(saved);
      message.success('Đã lưu chế độ quản lý lô');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được chế độ lô'));
    } finally {
      setSavingBatchMode(false);
    }
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Card title="Phiếu in (POS)" loading={loading}>
        <Form form={receiptForm} layout="vertical" style={{ maxWidth: 520 }} disabled={!canWrite}>
          <Form.Item
            name="name"
            label="Tên cửa hàng"
            rules={[{ required: true, message: 'Nhập tên cửa hàng' }]}
          >
            <Input placeholder="NHÀ THUỐC NOVIXA" />
          </Form.Item>
          <Form.Item name="tagline" label="Slogan / dòng phụ">
            <Input placeholder="Chăm sóc sức khỏe cộng đồng" />
          </Form.Item>
          <Form.Item name="phone" label="Điện thoại">
            <Input placeholder="0984.660.399" />
          </Form.Item>
          <Form.Item name="address" label="Địa chỉ">
            <Input.TextArea rows={2} placeholder="Số nhà, phường, quận, tỉnh/thành" />
          </Form.Item>
          {canWrite ? (
            <Button type="primary" loading={savingReceipt} onClick={() => void onSaveReceipt()}>
              Lưu phiếu in
            </Button>
          ) : null}
        </Form>
      </Card>

      <Card title="Quản lý lô (FEFO)" loading={loading}>
        <Space direction="vertical" size={12} style={{ maxWidth: 520, width: '100%' }}>
          <Select
            style={{ width: '100%' }}
            disabled={!canWrite}
            value={batchMode}
            options={BATCH_MODE_OPTIONS}
            onChange={setBatchMode}
          />
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {BATCH_MODE_HINTS[batchMode]}
          </Typography.Text>
          {canWrite ? (
            <Button type="primary" loading={savingBatchMode} onClick={() => void onSaveBatchMode()}>
              Lưu chế độ lô
            </Button>
          ) : null}
        </Space>
      </Card>
    </Space>
  );
}
