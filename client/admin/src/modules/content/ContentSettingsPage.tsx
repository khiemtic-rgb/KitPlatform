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
      message.error(apiErrorMessage(e, 'Không tải được tuỳ chọn'));
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
      message.success('Đã lưu tuỳ chọn');
      await load();
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được tuỳ chọn'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card loading={loading}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Tuỳ chọn nâng cao
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Chỉ cần chỉnh khi muốn đổi trần chi phí, số ảnh mỗi bài, hoặc danh sách loại bản viết / kênh đăng.
        Việc hàng ngày nằm ở tab <strong>Làm bài</strong>.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" style={{ maxWidth: 720 }}>
        <Form.Item
          name="monthlyCeilingUsd"
          label="Trần chi phí AI toàn hệ thống (USD / tháng)"
          extra="Ví dụ 120 = tối đa khoảng 120 USD/tháng cho gen chữ + ảnh."
          rules={[{ required: true }]}
        >
          <InputNumber min={0} step={10} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item name="defaultImageTier" label="Chất lượng ảnh mặc định" rules={[{ required: true }]}>
          <Select
            options={[
              { value: 'lean', label: 'Tiết kiệm — rẻ, đủ dùng' },
              { value: 'balanced', label: 'Cân bằng — khuyến nghị' },
              { value: 'premium', label: 'Cao cấp — đẹp hơn, đắt hơn' },
            ]}
          />
        </Form.Item>
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 8 }}>
          Đơn giá ước tính mỗi ảnh (USD) — dùng để tính trần, không phải hoá đơn Google thật.
        </Typography.Text>
        <Space size="large" wrap>
          <Form.Item name="leanRate" label="Giá ảnh Tiết kiệm" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
          <Form.Item name="balancedRate" label="Giá ảnh Cân bằng" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
          <Form.Item name="premiumRate" label="Giá ảnh Cao cấp" rules={[{ required: true }]}>
            <InputNumber min={0} step={0.01} />
          </Form.Item>
        </Space>
        <Space size="large" wrap>
          <Form.Item
            name="maxImageCandidatesPerItem"
            label="Số ảnh AI tạo mỗi bài"
            extra="Thường 2–3 ảnh để chọn."
            rules={[{ required: true }]}
          >
            <InputNumber min={1} max={10} />
          </Form.Item>
          <Form.Item
            name="regenMultiplier"
            label="Dự phòng chi phí gen lại"
            extra="1.2 = cộng thêm ~20% khi ước tính."
            rules={[{ required: true }]}
          >
            <InputNumber min={1} max={3} step={0.1} />
          </Form.Item>
          <Form.Item
            name="textPackEstimateUsd"
            label="Ước phí viết chữ / bài (USD)"
            rules={[{ required: true }]}
          >
            <InputNumber min={0} step={0.01} />
          </Form.Item>
        </Space>
        <Form.Item
          name="variantKinds"
          label="Các bản viết AI tạo (cách nhau bằng dấu phẩy)"
          extra="Ví dụ: bài web dài, bản Facebook, mô tả SEO…"
          rules={[{ required: true }]}
        >
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item
          name="connectorTypes"
          label="Kiểu kết nối đăng bài (nâng cao)"
          extra="astro_git, wordpress_rest, facebook_page, manual…"
          rules={[{ required: true }]}
        >
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.Item
          name="channelTypes"
          label="Loại kênh mạng xã hội (nâng cao)"
          rules={[{ required: true }]}
        >
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
