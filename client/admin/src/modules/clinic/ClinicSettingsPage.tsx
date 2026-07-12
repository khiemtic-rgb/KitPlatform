import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, Form, Input, Space, Typography } from 'antd';
import { ReloadOutlined, SaveOutlined, SettingOutlined } from '@ant-design/icons';
import { useTranslation } from 'react-i18next';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchClinicSettings,
  updateClinicSettings,
  type ClinicTenantSettings,
} from '@/shared/api/clinic.api';

type FormValues = {
  name: string;
  address?: string;
  phone?: string;
  workingHours?: string;
};

export function ClinicSettingsPage() {
  const { t } = useTranslation('clinic');
  const { message } = App.useApp();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<FormValues>();

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const s: ClinicTenantSettings = await fetchClinicSettings();
      form.setFieldsValue({
        name: s.name,
        address: s.address,
        phone: s.phone,
        workingHours: s.workingHours,
      });
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.loadFailed')));
    } finally {
      setLoading(false);
    }
  }, [form, message, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const onSave = async () => {
    const values = await form.validateFields();
    setSaving(true);
    try {
      await updateClinicSettings({
        name: values.name,
        address: values.address,
        phone: values.phone,
        workingHours: values.workingHours,
      });
      message.success(t('settings.saveSuccess'));
      await load();
    } catch (error) {
      message.error(apiErrorMessage(error, t('settings.saveFailed')));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <Typography.Title level={4} style={{ margin: 0 }}>
            <Space size={8}>
              <SettingOutlined />
              {t('settings.title')}
            </Space>
          </Typography.Title>
          <Typography.Text type="secondary">{t('settings.subtitle')}</Typography.Text>
        </div>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
            {t('settings.refresh')}
          </Button>
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void onSave()}>
            {t('settings.save')}
          </Button>
        </Space>
      </div>

      <Card loading={loading}>
        <Form form={form} layout="vertical" style={{ maxWidth: 520 }}>
          <Form.Item
            name="name"
            label={t('settings.name')}
            rules={[{ required: true, message: t('settings.nameRequired') }]}
          >
            <Input placeholder={t('settings.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="address" label={t('settings.address')}>
            <Input.TextArea rows={2} placeholder={t('settings.addressPlaceholder')} />
          </Form.Item>
          <Form.Item name="phone" label={t('settings.phone')}>
            <Input placeholder={t('settings.phonePlaceholder')} />
          </Form.Item>
          <Form.Item name="workingHours" label={t('settings.workingHours')}>
            <Input placeholder={t('settings.workingHoursPlaceholder')} />
          </Form.Item>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            {t('settings.pdfHint')}
          </Typography.Paragraph>
        </Form>
      </Card>
    </Space>
  );
}
