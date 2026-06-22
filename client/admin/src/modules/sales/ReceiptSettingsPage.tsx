import { useEffect, useState } from 'react';
import { App, Button, Card, Form, Input, Space } from 'antd';
import { updateReceiptSettings } from '@/shared/api/sales.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { useHasPermission } from '@/shared/auth/usePermission';
import {
  clearReceiptSettingsCache,
  loadReceiptStoreSettings,
  type ReceiptStoreSettings,
} from '@/modules/sales/receipt-settings';

export function ReceiptSettingsPage() {
  const { message } = App.useApp();
  const canWrite = useHasPermission('sales.write');
  const [form] = Form.useForm<ReceiptStoreSettings>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const settings = await loadReceiptStoreSettings(true);
        form.setFieldsValue(settings);
      } catch (error) {
        message.error(apiErrorMessage(error, 'Không tải được cài đặt phiếu in'));
      } finally {
        setLoading(false);
      }
    })();
  }, [form, message]);

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      const saved = await updateReceiptSettings({
        name: values.name.trim(),
        tagline: values.tagline?.trim() || undefined,
        phone: values.phone?.trim() || undefined,
        address: values.address?.trim() || undefined,
      });
      clearReceiptSettingsCache();
      await loadReceiptStoreSettings(true);
      form.setFieldsValue(saved);
      message.success('Đã lưu cài đặt phiếu in');
    } catch (error) {
      message.error(apiErrorMessage(error, 'Không lưu được cài đặt'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card title="Cài đặt phiếu in (POS)" loading={loading}>
      <Form form={form} layout="vertical" style={{ maxWidth: 520 }} disabled={!canWrite}>
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
          <Space>
            <Button type="primary" loading={saving} onClick={() => void onSave()}>
              Lưu cài đặt
            </Button>
          </Space>
        ) : null}
      </Form>
    </Card>
  );
}
