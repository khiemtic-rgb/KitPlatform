import { useCallback, useEffect, useState } from 'react';
import { Alert, App, Button, Form, Input, InputNumber, Select, Space, Switch, Tag, Typography } from 'antd';
import { ReloadOutlined, SaveOutlined, SendOutlined } from '@ant-design/icons';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentBrands,
  fetchContentTelegramSettings,
  saveContentTelegramSettings,
  testContentTelegram,
  type ContentBrand,
  type ContentTelegramSettings,
} from '@/shared/api/content.api';

type TelegramFormValues = {
  enabled: boolean;
  chatId?: string;
  notifySuccess: boolean;
  notifyFailure: boolean;
  notifyLowStock: boolean;
  notifyProposal: boolean;
  lowStockRemaining: number;
  lowStockEmptyDays: number;
  quietFromHour: number;
  quietToHour: number;
  botTokenSecretRef?: string;
  botToken?: string;
};

const CHAT_ID = /^-?\d{5,20}$/;

function toForm(s: ContentTelegramSettings): TelegramFormValues {
  return {
    enabled: s.enabled,
    chatId: s.chatId ?? '',
    notifySuccess: s.notifySuccess,
    notifyFailure: s.notifyFailure,
    notifyLowStock: s.notifyLowStock,
    notifyProposal: s.notifyProposal,
    lowStockRemaining: s.lowStockRemaining,
    lowStockEmptyDays: s.lowStockEmptyDays,
    quietFromHour: s.quietFromHour,
    quietToHour: s.quietToHour,
    botTokenSecretRef: s.botTokenSecretRef || 'TELEGRAM_BOT_TOKEN',
    // Token chỉ ghi: không bao giờ đổ lại giá trị cũ.
    botToken: '',
  };
}

/**
 * Thông báo Telegram: một bot dùng cho mọi thương hiệu, mỗi thương hiệu một chat_id.
 * Bot token chỉ ghi — API không trả lại token.
 */
export function ContentTelegramSettingsCard({
  hidden,
  onBotConfigured,
}: {
  hidden?: boolean;
  onBotConfigured?: (configured: boolean) => void;
}) {
  const { message } = App.useApp();
  const [form] = Form.useForm<TelegramFormValues>();
  const [brands, setBrands] = useState<ContentBrand[]>([]);
  const [brandId, setBrandId] = useState<string | undefined>();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [clearing, setClearing] = useState(false);
  const [botConfigured, setBotConfigured] = useState(false);
  const [hint, setHint] = useState<{ ok: boolean; text: string } | null>(null);

  const applyStatus = useCallback(
    (s: ContentTelegramSettings) => {
      setBotConfigured(s.botTokenConfigured);
      onBotConfigured?.(s.botTokenConfigured);
    },
    [onBotConfigured],
  );

  useEffect(() => {
    let alive = true;
    fetchContentBrands(true)
      .then((list) => {
        if (!alive) return;
        setBrands(list);
        setBrandId((cur) => cur ?? list[0]?.id);
      })
      .catch((e) => message.error(apiErrorMessage(e, 'Không tải được danh sách thương hiệu')));
    return () => {
      alive = false;
    };
  }, [message]);

  const load = useCallback(
    async (id: string) => {
      setLoading(true);
      setHint(null);
      try {
        const s = await fetchContentTelegramSettings(id);
        form.setFieldsValue(toForm(s));
        applyStatus(s);
      } catch (e) {
        message.error(apiErrorMessage(e, 'Không tải được cài đặt Telegram'));
      } finally {
        setLoading(false);
      }
    },
    [form, message, applyStatus],
  );

  useEffect(() => {
    if (brandId) void load(brandId);
  }, [brandId, load]);

  const onSave = async () => {
    if (!brandId) return;
    try {
      const v = await form.validateFields();
      setSaving(true);
      const token = v.botToken?.trim();
      const updated = await saveContentTelegramSettings(brandId, {
        enabled: v.enabled,
        chatId: v.chatId?.trim() || null,
        notifySuccess: v.notifySuccess,
        notifyFailure: v.notifyFailure,
        notifyLowStock: v.notifyLowStock,
        notifyProposal: v.notifyProposal,
        lowStockRemaining: v.lowStockRemaining,
        lowStockEmptyDays: v.lowStockEmptyDays,
        quietFromHour: v.quietFromHour,
        quietToHour: v.quietToHour,
        botTokenSecretRef: v.botTokenSecretRef?.trim() || null,
        // Để trống = giữ token cũ (không gửi field).
        ...(token ? { botToken: token } : {}),
      });
      form.setFieldsValue(toForm(updated));
      applyStatus(updated);
      message.success('Đã lưu cài đặt Telegram');
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được cài đặt Telegram'));
    } finally {
      setSaving(false);
    }
  };

  const onClearToken = async () => {
    if (!brandId) return;
    try {
      const v = form.getFieldsValue();
      setClearing(true);
      const updated = await saveContentTelegramSettings(brandId, {
        enabled: v.enabled,
        chatId: v.chatId?.trim() || null,
        notifySuccess: v.notifySuccess,
        notifyFailure: v.notifyFailure,
        notifyLowStock: v.notifyLowStock,
        notifyProposal: v.notifyProposal,
        lowStockRemaining: v.lowStockRemaining,
        lowStockEmptyDays: v.lowStockEmptyDays,
        quietFromHour: v.quietFromHour,
        quietToHour: v.quietToHour,
        botToken: '',
      });
      form.setFieldsValue(toForm(updated));
      applyStatus(updated);
      message.success(
        updated.botTokenConfigured
          ? 'Đã xoá token lưu trong admin. Máy chủ vẫn còn token ở biến môi trường.'
          : 'Đã xoá bot token',
      );
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không xoá được bot token'));
    } finally {
      setClearing(false);
    }
  };

  const onTest = async () => {
    if (!brandId) return;
    setTesting(true);
    setHint(null);
    try {
      const r = await testContentTelegram(brandId);
      setHint({ ok: r.ok, text: r.message });
      if (r.ok) message.success(r.message);
      else message.warning(r.message);
    } catch (e) {
      const text = apiErrorMessage(e, 'Không gửi được tin thử');
      setHint({ ok: false, text });
      message.error(text);
    } finally {
      setTesting(false);
    }
  };

  const brandName = brands.find((b) => b.id === brandId)?.name;
  const hours = Array.from({ length: 24 }, (_, h) => ({ value: h, label: `${String(h).padStart(2, '0')}:00` }));

  return (
    <div style={{ maxWidth: 720, display: hidden ? 'none' : undefined }}>
      <Alert
        type={botConfigured ? 'success' : 'info'}
        showIcon
        style={{ marginBottom: 16 }}
        message={
          <Space wrap>
            <span>Bot Telegram:</span>
            {botConfigured ? <Tag color="success">Đã có token</Tag> : <Tag color="warning">Chưa có token</Tag>}
          </Space>
        }
        description={
          <>
            Một bot dùng cho mọi thương hiệu. chat_id là của thương hiệu đang chọn. Token chỉ ghi, không hiện lại sau khi
            lưu; có thể để trống ô token và đặt biến môi trường trên máy chủ.
            <br />
            Giờ im lặng (giờ Việt Nam): tin lỗi vẫn gửi nhưng không kêu; các tin khác gom thành một tin tóm tắt lúc hết giờ
            im.
            {hint ? (
              <div style={{ marginTop: 8 }}>
                <Tag color={hint.ok ? 'success' : 'error'}>{hint.ok ? 'Tin thử' : 'Lỗi'}</Tag>
                {hint.text}
              </div>
            ) : null}
          </>
        }
      />

      <Form.Item label="Thương hiệu" style={{ marginBottom: 16 }}>
        <Select
          value={brandId}
          onChange={setBrandId}
          options={brands.map((b) => ({ value: b.id, label: b.name }))}
          placeholder="Chọn thương hiệu"
          showSearch
          optionFilterProp="label"
        />
      </Form.Item>

      <Form
        form={form}
        layout="vertical"
        disabled={!brandId || loading}
        initialValues={{
          enabled: false,
          notifySuccess: true,
          notifyFailure: true,
          notifyLowStock: true,
          notifyProposal: true,
          lowStockRemaining: 3,
          lowStockEmptyDays: 2,
          quietFromHour: 22,
          quietToHour: 7,
          botTokenSecretRef: 'TELEGRAM_BOT_TOKEN',
        }}
      >
        <Form.Item
          name="enabled"
          label={brandName ? `Bật thông báo cho ${brandName}` : 'Bật thông báo'}
          valuePropName="checked"
          extra="Tắt hoặc chưa có chat_id thì không xếp tin cho thương hiệu này. Bật xong chỉ báo việc mới, không gửi lại việc cũ."
        >
          <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
        </Form.Item>
        <Form.Item
          name="chatId"
          label="chat_id"
          extra="Thêm bot vào nhóm rồi lấy id nhóm (thường bắt đầu bằng -100)."
          rules={[
            {
              validator: (_, v?: string) =>
                !v || CHAT_ID.test(v.trim())
                  ? Promise.resolve()
                  : Promise.reject(new Error('chat_id chỉ gồm số, nhóm thường bắt đầu bằng -100')),
            },
          ]}
        >
          <Input placeholder="-1001234567890" autoComplete="off" />
        </Form.Item>

        <Typography.Text strong>Loại tin</Typography.Text>
        <Space wrap size="large" style={{ margin: '8px 0 16px' }}>
          <Form.Item name="notifySuccess" valuePropName="checked" noStyle>
            <Switch checkedChildren="Đăng thành công" unCheckedChildren="Đăng thành công" />
          </Form.Item>
          <Form.Item name="notifyFailure" valuePropName="checked" noStyle>
            <Switch checkedChildren="Đăng lỗi" unCheckedChildren="Đăng lỗi" />
          </Form.Item>
          <Form.Item name="notifyLowStock" valuePropName="checked" noStyle>
            <Switch checkedChildren="Sắp hết bài" unCheckedChildren="Sắp hết bài" />
          </Form.Item>
          <Form.Item name="notifyProposal" valuePropName="checked" noStyle>
            <Switch checkedChildren="Agent cần duyệt" unCheckedChildren="Agent cần duyệt" />
          </Form.Item>
        </Space>

        <Space wrap size="large">
          <Form.Item
            name="lowStockRemaining"
            label="Báo khi còn dưới (bài đã xếp lịch)"
            rules={[{ required: true }]}
          >
            <InputNumber min={1} max={50} />
          </Form.Item>
          <Form.Item name="lowStockEmptyDays" label="hoặc trống lịch trong (ngày tới)" rules={[{ required: true }]}>
            <InputNumber min={1} max={30} />
          </Form.Item>
        </Space>

        <Space wrap size="large">
          <Form.Item name="quietFromHour" label="Giờ im lặng từ" rules={[{ required: true }]}>
            <Select options={hours} style={{ width: 110 }} />
          </Form.Item>
          <Form.Item name="quietToHour" label="đến" extra="Từ = đến nghĩa là không im lặng." rules={[{ required: true }]}>
            <Select options={hours} style={{ width: 110 }} />
          </Form.Item>
        </Space>

        <Form.Item
          name="botTokenSecretRef"
          label="Tên biến môi trường chứa token (tuỳ chọn)"
          extra="Phải bắt đầu bằng TELEGRAM_. Dùng khi không muốn dán token vào admin."
        >
          <Input placeholder="TELEGRAM_BOT_TOKEN" autoComplete="off" />
        </Form.Item>
        <Form.Item name="botToken" label="Bot token (chỉ ghi — để trống nếu giữ nguyên)">
          <Input.Password placeholder="Để trống nếu giữ nguyên" autoComplete="new-password" />
        </Form.Item>

        <Space wrap>
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={() => void onSave()}>
            Lưu Telegram
          </Button>
          <Button icon={<SendOutlined />} loading={testing} onClick={() => void onTest()}>
            Gửi tin thử
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => brandId && void load(brandId)}>
            Tải lại
          </Button>
          <Button danger loading={clearing} disabled={!botConfigured} onClick={() => void onClearToken()}>
            Xoá token đã lưu
          </Button>
        </Space>
      </Form>
    </div>
  );
}
