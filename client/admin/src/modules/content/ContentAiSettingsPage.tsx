import { useCallback, useEffect, useState } from 'react';
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
import { ApiOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentSettings,
  testContentAi,
  updateContentSettings,
  type ContentAiConfig,
} from '@/shared/api/content.api';

type FormValues = {
  provider: string;
  textModel: string;
  imageModel?: string;
  imagesEnabled: boolean;
  geminiApiKeySecretRef?: string;
  geminiApiKey?: string;
};

const TEXT_MODEL_OPTS = [
  { value: 'gemini-flash-latest', label: 'gemini-flash-latest (khuyên dùng)' },
  { value: 'gemini-2.0-flash', label: 'gemini-2.0-flash' },
  { value: 'gemini-2.5-flash', label: 'gemini-2.5-flash' },
];

const IMAGE_MODEL_OPTS = [
  { value: 'pollinations:flux', label: 'Pollinations Flux (miễn phí — khuyến nghị khi hết quota Gemini)' },
  { value: 'pollinations:turbo', label: 'Pollinations Turbo (miễn phí, nhanh hơn)' },
  { value: '', label: 'Tự chọn Gemini rồi fallback Pollinations' },
  { value: 'gemini-2.5-flash-image', label: 'gemini-2.5-flash-image (cần billing Gemini)' },
  { value: 'gemini-3.1-flash-lite-image', label: 'gemini-3.1-flash-lite-image (cần billing)' },
  { value: 'gemini-3.1-flash-image', label: 'gemini-3.1-flash-image (cần billing)' },
];

function toForm(ai: ContentAiConfig): FormValues {
  return {
    provider: ai.provider || 'gemini',
    textModel: ai.textModel || 'gemini-flash-latest',
    imageModel: ai.imageModel ?? '',
    imagesEnabled: ai.imagesEnabled,
    geminiApiKeySecretRef: ai.geminiApiKeySecretRef ?? 'GEMINI_API_KEY',
    geminiApiKey: '',
  };
}

export function ContentAiSettingsPage() {
  const { message } = App.useApp();
  const [form] = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [apiKeyConfigured, setApiKeyConfigured] = useState(false);
  const [testHint, setTestHint] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setTestHint(null);
    try {
      const s = await fetchContentSettings();
      form.setFieldsValue(toForm(s.ai));
      setApiKeyConfigured(s.ai.apiKeyConfigured);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được cấu hình AI'));
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
      const key = v.geminiApiKey?.trim();
      const updated = await updateContentSettings({
        ai: {
          provider: v.provider,
          textModel: v.textModel,
          imageModel: v.imageModel?.trim() ? v.imageModel.trim() : null,
          imagesEnabled: v.imagesEnabled,
          geminiApiKeySecretRef: v.geminiApiKeySecretRef?.trim() || null,
          // only send key when user typed something (leave null = keep existing)
          ...(key ? { geminiApiKey: key } : {}),
        },
      });
      form.setFieldsValue(toForm(updated.ai));
      setApiKeyConfigured(updated.ai.apiKeyConfigured);
      message.success('Đã lưu cấu hình AI');
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được cấu hình AI'));
    } finally {
      setSaving(false);
    }
  };

  const onTest = async () => {
    setTesting(true);
    setTestHint(null);
    try {
      const r = await testContentAi();
      setApiKeyConfigured(r.apiKeyConfigured);
      setTestHint(r.message ?? (r.ok ? 'OK' : 'Thất bại'));
      if (r.ok) message.success(r.message ?? 'Kết nối OK');
      else message.warning(r.message ?? 'Không kết nối được');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không test được kết nối AI'));
    } finally {
      setTesting(false);
    }
  };

  return (
    <Card loading={loading}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Cấu hình AI
      </Typography.Title>
      <Typography.Paragraph type="secondary">
        Chọn model viết / ảnh và cách lấy API key. Ưu tiên tên biến môi trường (Secret ref) — không cần dán key vào form.
        Key dán vào đây chỉ ghi một chiều, GET không trả lại.
      </Typography.Paragraph>

      <Alert
        type={apiKeyConfigured ? 'success' : 'warning'}
        showIcon
        style={{ marginBottom: 16 }}
        message={
          <Space wrap>
            <span>Trạng thái key:</span>
            {apiKeyConfigured ? (
              <Tag color="success">Đã cấu hình</Tag>
            ) : (
              <Tag color="warning">Chưa có key</Tag>
            )}
          </Space>
        }
        description={
          testHint ??
          'Secret ref mặc định: GEMINI_API_KEY. Có thể fallback thêm Content:GeminiApiKey trong appsettings.'
        }
      />

      <Form form={form} layout="vertical" style={{ maxWidth: 720 }} initialValues={{ provider: 'gemini', imagesEnabled: true }}>
        <Form.Item name="provider" label="Nhà cung cấp" rules={[{ required: true }]}>
          <Select
            options={[{ value: 'gemini', label: 'Google Gemini' }]}
          />
        </Form.Item>
        <Form.Item
          name="textModel"
          label="Model viết chữ"
          rules={[{ required: true }]}
          extra="Dùng khi bấm Nhờ AI tạo bản viết."
        >
          <Select options={TEXT_MODEL_OPTS} showSearch allowClear={false} />
        </Form.Item>
        <Form.Item
          name="imageModel"
          label="Model ảnh (tuỳ chọn)"
          extra="Để trống = thử lần lượt các model fallback."
        >
          <Select options={IMAGE_MODEL_OPTS} showSearch allowClear />
        </Form.Item>
        <Form.Item
          name="imagesEnabled"
          label="Bật gen ảnh khi Nhờ AI"
          valuePropName="checked"
          extra="Tắt nếu chỉ muốn chữ (tiết kiệm chi phí)."
        >
          <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
        </Form.Item>
        <Form.Item
          name="geminiApiKeySecretRef"
          label="Secret ref (tên env / vault)"
          extra="Ví dụ GEMINI_API_KEY — API đọc từ biến môi trường máy chạy."
        >
          <Input placeholder="GEMINI_API_KEY" autoComplete="off" />
        </Form.Item>
        <Form.Item
          name="geminiApiKey"
          label="API key (chỉ ghi — để trống nếu giữ nguyên)"
          extra="Chỉ dán khi muốn ghi đè key lưu DB. Không hiện lại sau khi lưu."
        >
          <Input.Password placeholder="AIza…" autoComplete="new-password" />
        </Form.Item>
        <Space wrap>
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void onSave()}>
            Lưu
          </Button>
          <Button icon={<ApiOutlined />} loading={testing} onClick={() => void onTest()}>
            Test kết nối
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
