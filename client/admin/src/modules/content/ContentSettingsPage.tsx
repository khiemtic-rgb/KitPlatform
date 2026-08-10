import { useCallback, useEffect, useState } from 'react';
import { App, Button, Card, Form, Input, InputNumber, Select, Space, Typography } from 'antd';
import { ReloadOutlined, SaveOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentSettings,
  updateContentSettings,
  type ContentOrgSettings,
} from '@/shared/api/content.api';

type FormValues = {
  monthlyCeilingUsd: number;
  maxImageCandidatesPerItem: number;
  regenMultiplier: number;
  defaultImageTier: string;
  textPackEstimateUsd: number;
  leanRate: number;
  balancedRate: number;
  premiumRate: number;
  variantKinds: string;
  connectorTypes: string;
  channelTypes: string;
};

function toForm(s: ContentOrgSettings): FormValues {
  return {
    monthlyCeilingUsd: s.monthlyCeilingUsd,
    maxImageCandidatesPerItem: s.maxImageCandidatesPerItem,
    regenMultiplier: s.regenMultiplier,
    defaultImageTier: s.defaultImageTier,
    textPackEstimateUsd: s.textPackEstimateUsd,
    leanRate: s.imageRateUsd.lean ?? 0.02,
    balancedRate: s.imageRateUsd.balanced ?? 0.05,
    premiumRate: s.imageRateUsd.premium ?? 0.14,
    variantKinds: (s.variantKinds ?? []).join(', '),
    connectorTypes: (s.connectorTypes ?? []).join(', '),
    channelTypes: (s.channelTypes ?? []).join(', '),
  };
}

function splitCsv(raw: string) {
  return raw
    .split(',')
    .map((x) => x.trim())
    .filter(Boolean);
}

export function ContentSettingsPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const s = await fetchContentSettings();
      form.setFieldsValue(toForm(s));
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được cài đặt Content'));
    } finally {
      setLoading(false);
    }
  }, [form, message]);

  useEffect(() => {
    void load();
  }, [load]);

  const onSave = async () => {
    try {
      const v = await form.validateFields();
      setSaving(true);
      await updateContentSettings({
        monthlyCeilingUsd: v.monthlyCeilingUsd,
        maxImageCandidatesPerItem: v.maxImageCandidatesPerItem,
        regenMultiplier: v.regenMultiplier,
        defaultImageTier: v.defaultImageTier,
        textPackEstimateUsd: v.textPackEstimateUsd,
        imageRateUsd: {
          lean: v.leanRate,
          balanced: v.balancedRate,
          premium: v.premiumRate,
        },
        variantKinds: splitCsv(v.variantKinds),
        connectorTypes: splitCsv(v.connectorTypes),
        channelTypes: splitCsv(v.channelTypes),
      });
      message.success('Đã lưu cài đặt Content');
      await load();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được cài đặt'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card loading={loading}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Cài đặt động Content Park
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Thêm connector/channel/variant kind bằng danh sách CSV — không cần migration mới.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" style={{ maxWidth: 720 }}>
        <Form.Item name="monthlyCeilingUsd" label="Trần ngân sách global (USD/tháng)" rules={[{ required: true }]}>
          <InputNumber min={0} step={10} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="defaultImageTier" label="Tier ảnh mặc định" rules={[{ required: true }]}>
          <Select
            options={[
              { value: 'lean', label: 'Lean' },
              { value: 'balanced', label: 'Balanced' },
              { value: 'premium', label: 'Premium' },
            ]}
          />
        </Form.Item>
        <Space size="large" wrap>
          <Form.Item name="leanRate" label="RATE lean $/ảnh" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
          <Form.Item name="balancedRate" label="RATE balanced $/ảnh" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
          <Form.Item name="premiumRate" label="RATE premium $/ảnh" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
        </Space>
        <Space size="large" wrap>
          <Form.Item name="maxImageCandidatesPerItem" label="Số ảnh/bài" rules={[{ required: true }]}>
            <InputNumber min={1} max={10} />
          </Form.Item>
          <Form.Item name="regenMultiplier" label="Hệ số regen" rules={[{ required: true }]}>
            <InputNumber min={1} max={3} step={0.1} />
          </Form.Item>
          <Form.Item name="textPackEstimateUsd" label="Ước text pack $/topic" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
        </Space>
        <Form.Item name="variantKinds" label="Variant kinds (CSV)" rules={[{ required: true }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item name="connectorTypes" label="Connector types (CSV)" rules={[{ required: true }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item name="channelTypes" label="Channel types (CSV)" rules={[{ required: true }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
        <Space>
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void onSave()}>
            Lưu
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
