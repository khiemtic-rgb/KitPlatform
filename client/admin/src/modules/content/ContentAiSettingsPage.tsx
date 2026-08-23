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
  Tabs,
  Tag,
  Typography,
} from 'antd';
import {
  ApiOutlined,
  FacebookOutlined,
  PictureOutlined,
  ReloadOutlined,
  SaveOutlined,
  VideoCameraOutlined,
} from '@ant-design/icons';
import { useLocation, useNavigate } from 'react-router-dom';
import { FB_RETURN_KEY } from '@/modules/content/ContentFacebookCallbackPage';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchContentSettings,
  testContentAi,
  testContentFacebook,
  testContentVideo,
  updateContentSettings,
  type ContentAiConfig,
  type ContentFacebookConfig,
  type ContentVideoConfig,
} from '@/shared/api/content.api';

type AiFormValues = {
  provider: string;
  textModel: string;
  imageModel?: string;
  imagesEnabled: boolean;
  geminiApiKeySecretRef?: string;
  geminiApiKey?: string;
};

type FacebookFormValues = {
  appId?: string;
  appIdSecretRef?: string;
  appSecretSecretRef?: string;
  appSecret?: string;
  redirectUri?: string;
};

type VideoFormValues = {
  creatomateApiKeySecretRef?: string;
  creatomateApiKey?: string;
  elevenLabsApiKeySecretRef?: string;
  elevenLabsApiKey?: string;
  elevenLabsVoiceId?: string;
  publicMediaBaseUrl?: string;
  creatomateTemplateId?: string;
  runwayApiKeySecretRef?: string;
  runwayApiKey?: string;
  falApiKeySecretRef?: string;
  falApiKey?: string;
};

const TEXT_MODEL_OPTS = [
  { value: 'gemini-3.6-flash', label: 'gemini-3.6-flash (khuyên dùng)' },
  { value: 'gemini-flash-latest', label: 'gemini-flash-latest' },
  { value: 'gemini-2.0-flash', label: 'gemini-2.0-flash' },
];

const IMAGE_MODEL_OPTS = [
  { value: 'pollinations:flux', label: 'Pollinations Flux (miễn phí — khuyến nghị khi hết quota Gemini)' },
  { value: 'pollinations:turbo', label: 'Pollinations Turbo (miễn phí, nhanh hơn)' },
  { value: '', label: 'Tự chọn Gemini rồi fallback Pollinations' },
  { value: 'gemini-2.5-flash-image', label: 'gemini-2.5-flash-image (cần billing Gemini)' },
  { value: 'gemini-3.1-flash-lite-image', label: 'gemini-3.1-flash-lite-image (cần billing)' },
  { value: 'gemini-3.1-flash-image', label: 'gemini-3.1-flash-image (cần billing)' },
];

function hashToTab(hash: string): string {
  const key = hash.replace(/^#/, '').toLowerCase();
  if (key === 'video' || key === 'image' || key === 'write' || key === 'facebook') return key;
  return 'write';
}

function toAiForm(ai: ContentAiConfig): AiFormValues {
  return {
    provider: ai.provider || 'gemini',
    textModel: ai.textModel || 'gemini-3.6-flash',
    imageModel: ai.imageModel ?? '',
    imagesEnabled: ai.imagesEnabled,
    geminiApiKeySecretRef: ai.geminiApiKeySecretRef ?? 'GEMINI_API_KEY',
    geminiApiKey: '',
  };
}

function toFacebookForm(fb?: ContentFacebookConfig): FacebookFormValues {
  return {
    appId: fb?.appId ?? '',
    appIdSecretRef: fb?.appIdSecretRef ?? 'FACEBOOK_APP_ID',
    appSecretSecretRef: fb?.appSecretSecretRef ?? 'FACEBOOK_APP_SECRET',
    appSecret: '',
    redirectUri: fb?.redirectUri ?? 'http://localhost:5173/content/facebook/callback',
  };
}

function toVideoForm(video: ContentVideoConfig): VideoFormValues {
  return {
    creatomateApiKeySecretRef: video.creatomateApiKeySecretRef ?? 'CREATOMATE_API_KEY',
    creatomateApiKey: '',
    elevenLabsApiKeySecretRef: video.elevenLabsApiKeySecretRef ?? 'ELEVENLABS_API_KEY',
    elevenLabsApiKey: '',
    elevenLabsVoiceId: video.elevenLabsVoiceId ?? '',
    publicMediaBaseUrl: video.publicMediaBaseUrl ?? '',
    creatomateTemplateId: video.creatomateTemplateId ?? '',
    runwayApiKeySecretRef: video.runwayApiKeySecretRef ?? 'RUNWAY_API_KEY',
    runwayApiKey: '',
    falApiKeySecretRef: video.falApiKeySecretRef ?? 'FAL_KEY',
    falApiKey: '',
  };
}

export function ContentAiSettingsPage() {
  const { message } = App.useApp();
  const location = useLocation();
  const navigate = useNavigate();
  const [aiForm] = Form.useForm<AiFormValues>();
  const [videoForm] = Form.useForm<VideoFormValues>();
  const [facebookForm] = Form.useForm<FacebookFormValues>();
  const [tab, setTab] = useState(() => hashToTab(window.location.hash));
  const [loading, setLoading] = useState(true);
  const [savingAi, setSavingAi] = useState(false);
  const [savingVideo, setSavingVideo] = useState(false);
  const [savingFacebook, setSavingFacebook] = useState(false);
  const [testingAi, setTestingAi] = useState(false);
  const [testingVideo, setTestingVideo] = useState(false);
  const [testingFacebook, setTestingFacebook] = useState(false);
  const [apiKeyConfigured, setApiKeyConfigured] = useState(false);
  const [creatomateConfigured, setCreatomateConfigured] = useState(false);
  const [elevenLabsConfigured, setElevenLabsConfigured] = useState(false);
  const [runwayConfigured, setRunwayConfigured] = useState(false);
  const [falConfigured, setFalConfigured] = useState(false);
  const [i2vChoice, setI2vChoice] = useState<'turbo' | 'wan'>(() =>
    typeof localStorage !== 'undefined' && localStorage.getItem('kit.famixaSeries.engine') === 'wan' ? 'wan' : 'turbo',
  );
  const [voiceChoice, setVoiceChoice] = useState<'elevenlabs' | 'f5'>(() =>
    typeof localStorage !== 'undefined' && localStorage.getItem('kit.famixaSeries.voice') === 'f5' ? 'f5' : 'elevenlabs',
  );
  const [renderChoice, setRenderChoice] = useState<'creatomate'>('creatomate');
  const [facebookConfigured, setFacebookConfigured] = useState(false);
  const [aiHint, setAiHint] = useState<string | null>(null);
  const [videoHint, setVideoHint] = useState<string | null>(null);
  const [facebookHint, setFacebookHint] = useState<string | null>(null);
  const [fbReturnTo] = useState<string | null>(() => sessionStorage.getItem(FB_RETURN_KEY));

  const load = useCallback(async () => {
    setLoading(true);
    setAiHint(null);
    setVideoHint(null);
    try {
      const s = await fetchContentSettings();
      aiForm.setFieldsValue(toAiForm(s.ai));
      videoForm.setFieldsValue(
        toVideoForm(
          s.video ?? {
            creatomateConfigured: false,
            elevenLabsConfigured: false,
            runwayConfigured: false,
            falConfigured: false,
          },
        ),
      );
      facebookForm.setFieldsValue(toFacebookForm(s.facebook));
      setApiKeyConfigured(s.ai.apiKeyConfigured);
      setCreatomateConfigured(s.video?.creatomateConfigured ?? false);
      setElevenLabsConfigured(s.video?.elevenLabsConfigured ?? false);
      setRunwayConfigured(s.video?.runwayConfigured ?? false);
      setFalConfigured(s.video?.falConfigured ?? false);
      setFacebookConfigured(s.facebook?.appSecretConfigured ?? false);
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không tải được cấu hình AI'));
    } finally {
      setLoading(false);
    }
  }, [aiForm, videoForm, facebookForm, message]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    setTab(hashToTab(location.hash));
  }, [location.hash]);

  const onTab = (key: string) => {
    setTab(key);
    navigate({ pathname: location.pathname, hash: key === 'write' ? '' : key }, { replace: true });
  };

  const onSaveAi = async () => {
    try {
      const v = await aiForm.validateFields();
      setSavingAi(true);
      const key = v.geminiApiKey?.trim();
      const updated = await updateContentSettings({
        ai: {
          provider: v.provider,
          textModel: v.textModel,
          imageModel: v.imageModel?.trim() ? v.imageModel.trim() : null,
          imagesEnabled: v.imagesEnabled,
          geminiApiKeySecretRef: v.geminiApiKeySecretRef?.trim() || null,
          ...(key ? { geminiApiKey: key } : {}),
        },
      });
      aiForm.setFieldsValue(toAiForm(updated.ai));
      setApiKeyConfigured(updated.ai.apiKeyConfigured);
      message.success('Đã lưu cấu hình AI');
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được cấu hình AI'));
    } finally {
      setSavingAi(false);
    }
  };

  const onSaveVideo = async () => {
    try {
      const v = await videoForm.validateFields();
      setSavingVideo(true);
      const creatomateKey = v.creatomateApiKey?.trim();
      const elevenKey = v.elevenLabsApiKey?.trim();
      const runwayKey = v.runwayApiKey?.trim();
      const falKey = v.falApiKey?.trim();
      const updated = await updateContentSettings({
        video: {
          creatomateApiKeySecretRef: v.creatomateApiKeySecretRef?.trim() || null,
          elevenLabsApiKeySecretRef: v.elevenLabsApiKeySecretRef?.trim() || null,
          elevenLabsVoiceId: v.elevenLabsVoiceId?.trim() || null,
          publicMediaBaseUrl: v.publicMediaBaseUrl?.trim() || null,
          creatomateTemplateId: v.creatomateTemplateId?.trim() || null,
          runwayApiKeySecretRef: v.runwayApiKeySecretRef?.trim() || null,
          falApiKeySecretRef: v.falApiKeySecretRef?.trim() || null,
          ...(creatomateKey ? { creatomateApiKey: creatomateKey } : {}),
          ...(elevenKey ? { elevenLabsApiKey: elevenKey } : {}),
          ...(runwayKey ? { runwayApiKey: runwayKey } : {}),
          ...(falKey ? { falApiKey: falKey } : {}),
        },
      });
      videoForm.setFieldsValue(
        toVideoForm(
          updated.video ?? {
            creatomateConfigured: false,
            elevenLabsConfigured: false,
            runwayConfigured: false,
            falConfigured: false,
          },
        ),
      );
      setCreatomateConfigured(updated.video?.creatomateConfigured ?? false);
      setElevenLabsConfigured(updated.video?.elevenLabsConfigured ?? false);
      setRunwayConfigured(updated.video?.runwayConfigured ?? false);
      setFalConfigured(updated.video?.falConfigured ?? false);
      message.success('Đã lưu cấu hình video');
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được cấu hình video'));
    } finally {
      setSavingVideo(false);
    }
  };

  const onTestAi = async () => {
    setTestingAi(true);
    setAiHint(null);
    try {
      const r = await testContentAi();
      setApiKeyConfigured(r.apiKeyConfigured);
      setAiHint(r.message ?? (r.ok ? 'OK' : 'Thất bại'));
      if (r.ok) message.success(r.message ?? 'Kết nối OK');
      else message.warning(r.message ?? 'Không kết nối được');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không test được kết nối AI'));
    } finally {
      setTestingAi(false);
    }
  };

  const onTestVideo = async () => {
    setTestingVideo(true);
    setVideoHint(null);
    try {
      const r = await testContentVideo();
      setCreatomateConfigured(r.creatomateConfigured);
      setElevenLabsConfigured(r.elevenLabsConfigured);
      setRunwayConfigured(r.runwayConfigured ?? false);
      setFalConfigured(r.falConfigured ?? false);
      const parts = [r.creatomateMessage, r.elevenLabsMessage, r.runwayMessage, r.falMessage].filter(Boolean);
      setVideoHint(parts.join(' · ') || null);
      if (r.creatomateOk && r.elevenLabsOk && r.runwayOk && r.falOk)
        message.success('Creatomate + ElevenLabs + Runway + Fal OK');
      else if (r.creatomateOk || r.elevenLabsOk || r.runwayOk || r.falOk) message.warning(parts.join(' · '));
      else message.warning(parts.join(' · ') || 'Chưa cấu hình video');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không test được Creatomate / giọng nói'));
    } finally {
      setTestingVideo(false);
    }
  };

  const onSaveFacebook = async () => {
    try {
      const v = await facebookForm.validateFields();
      setSavingFacebook(true);
      const secret = v.appSecret?.trim();
      const updated = await updateContentSettings({
        facebook: {
          appId: v.appId?.trim() || null,
          appIdSecretRef: v.appIdSecretRef?.trim() || null,
          appSecretSecretRef: v.appSecretSecretRef?.trim() || null,
          redirectUri: v.redirectUri?.trim() || null,
          ...(secret ? { appSecret: secret } : {}),
        },
      });
      facebookForm.setFieldsValue(toFacebookForm(updated.facebook));
      setFacebookConfigured(updated.facebook?.appSecretConfigured ?? false);
      message.success(
        updated.facebook?.appSecretConfigured
          ? 'Đã lưu Facebook App. Quay lại bài viết và bấm Kết nối lại.'
          : 'Đã lưu Facebook App',
      );
    } catch (e) {
      if (e && typeof e === 'object' && 'errorFields' in e) return;
      message.error(apiErrorMessage(e, 'Không lưu được Facebook App'));
    } finally {
      setSavingFacebook(false);
    }
  };

  const onTestFacebook = async () => {
    setTestingFacebook(true);
    setFacebookHint(null);
    try {
      const r = await testContentFacebook();
      setFacebookConfigured(r.appSecretConfigured);
      setFacebookHint(r.message ?? null);
      if (r.ok) message.success(r.message ?? 'App Meta OK');
      else message.warning(r.message ?? 'Chưa cấu hình Facebook App');
    } catch (e) {
      message.error(apiErrorMessage(e, 'Không test được Facebook App'));
    } finally {
      setTestingFacebook(false);
    }
  };

  const aiActions = (
    <Space wrap>
      <Button type="primary" icon={<SaveOutlined />} loading={savingAi} onClick={() => void onSaveAi()}>
        Lưu
      </Button>
      <Button icon={<ApiOutlined />} loading={testingAi} onClick={() => void onTestAi()}>
        Test kết nối
      </Button>
      <Button icon={<ReloadOutlined />} onClick={() => void load()}>
        Tải lại
      </Button>
    </Space>
  );

  return (
    <Card loading={loading}>
      <Typography.Title level={4} style={{ marginTop: 0 }}>
        Cấu hình AI
      </Typography.Title>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
        Key chỉ ghi một chiều — GET không trả lại. Ưu tiên Secret ref (tên biến môi trường).
      </Typography.Paragraph>

      <style>{`.content-ai-settings-tabs .ant-tabs-content-holder{display:none}`}</style>
      <Tabs
        className="content-ai-settings-tabs"
        activeKey={tab}
        onChange={onTab}
        items={[
          {
            key: 'write',
            label: (
              <Space size={6}>
                Viết
                {apiKeyConfigured ? <Tag color="success">Có key</Tag> : <Tag color="warning">Chưa key</Tag>}
              </Space>
            ),
          },
          {
            key: 'image',
            label: (
              <Space size={6}>
                <PictureOutlined />
                Ảnh
              </Space>
            ),
          },
          {
            key: 'video',
            label: (
              <Space size={6}>
                <VideoCameraOutlined />
                Video
                {creatomateConfigured && elevenLabsConfigured ? (
                  <Tag color="success">OK</Tag>
                ) : (
                  <Tag color="warning">Thiếu key</Tag>
                )}
              </Space>
            ),
          },
          {
            key: 'facebook',
            label: (
              <Space size={6}>
                <FacebookOutlined />
                Facebook
                {facebookConfigured ? <Tag color="success">Có app</Tag> : <Tag color="warning">Chưa app</Tag>}
              </Space>
            ),
          },
        ]}
      />

      <Form
        form={aiForm}
        layout="vertical"
        style={{ maxWidth: 720, display: tab === 'write' || tab === 'image' ? undefined : 'none' }}
        initialValues={{ provider: 'gemini', imagesEnabled: true }}
      >
        <div style={{ display: tab === 'write' ? undefined : 'none' }}>
          <Alert
            type={apiKeyConfigured ? 'success' : 'warning'}
            showIcon
            style={{ marginBottom: 16 }}
            message={
              <Space wrap>
                <span>Gemini:</span>
                {apiKeyConfigured ? (
                  <Tag color="success">Đã cấu hình</Tag>
                ) : (
                  <Tag color="warning">Chưa có key</Tag>
                )}
              </Space>
            }
            description={
              aiHint ??
              'Secret ref mặc định: GEMINI_API_KEY. Có thể fallback Content:GeminiApiKey trong appsettings.'
            }
          />
          <Form.Item name="provider" label="Nhà cung cấp" rules={[{ required: true }]}>
            <Select options={[{ value: 'gemini', label: 'Google Gemini' }]} />
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
        </div>
        <div style={{ display: tab === 'image' ? undefined : 'none' }}>
          <Typography.Paragraph type="secondary">
            Model ảnh dùng chung key Gemini ở tab Viết. Tắt gen ảnh nếu chỉ cần chữ.
          </Typography.Paragraph>
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
        </div>
        {tab !== 'video' ? aiActions : null}
      </Form>

      <Form
        form={videoForm}
        layout="vertical"
        style={{ maxWidth: 720, display: tab === 'video' ? undefined : 'none' }}
      >
        {videoHint ? (
          <Alert type="info" showIcon style={{ marginBottom: 16 }} message={videoHint} />
        ) : null}

        <Card
          size="small"
          title="I2V Series"
          extra={
            <Tag color={runwayConfigured ? 'success' : 'warning'}>
              {runwayConfigured ? 'Đã có key Runway' : 'Chưa có key Runway'}
            </Tag>
          }
          style={{ marginBottom: 16 }}
        >
          <Form.Item label="Nhà cung cấp">
            <Select
              value={i2vChoice}
              onChange={(v) => {
                setI2vChoice(v);
                try {
                  localStorage.setItem('kit.famixaSeries.engine', v);
                } catch {
                  /* quota */
                }
              }}
              options={[
                { value: 'turbo', label: 'Runway Turbo' },
                { value: 'wan', label: 'Wan 2.1 (Fal)' },
              ]}
            />
          </Form.Item>
          {i2vChoice === 'turbo' ? (
            <>
              <Form.Item name="runwayApiKeySecretRef" label="Secret ref">
                <Input placeholder="RUNWAY_API_KEY" autoComplete="off" />
              </Form.Item>
              <Form.Item name="runwayApiKey" label="API key (chỉ ghi — để trống nếu giữ nguyên)">
                <Input.Password placeholder="Dán key Runway" autoComplete="new-password" />
              </Form.Item>
            </>
          ) : (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 0 }}
              message="Wan dùng key Fal ở thẻ dưới — không dán vào ô Runway."
            />
          )}
        </Card>

        <Card
          size="small"
          title="Fal — Wan + khớp môi"
          extra={<Tag color={falConfigured ? 'success' : 'warning'}>{falConfigured ? 'Đã có key Fal' : 'Chưa có key Fal'}</Tag>}
          style={{ marginBottom: 16 }}
        >
          <p style={{ marginTop: 0, color: 'rgba(0,0,0,0.65)' }}>
            Key từ fal.ai (Key / API). Khớp môi = sync-lipsync 1.9 (~$0.70/phút). Wan dùng chung. Không dán vào Runway / ElevenLabs / Creatomate.
          </p>
          <Form.Item name="falApiKeySecretRef" label="Secret ref">
            <Input placeholder="FAL_KEY" autoComplete="off" />
          </Form.Item>
          <Form.Item name="falApiKey" label="API key (chỉ ghi — để trống nếu giữ nguyên)">
            <Input.Password placeholder="Dán key Fal" autoComplete="new-password" />
          </Form.Item>
        </Card>

        <Card
          size="small"
          title="Giọng nói"
          extra={
            <Tag color={elevenLabsConfigured ? 'success' : 'warning'}>
              {elevenLabsConfigured ? 'Đã có key ElevenLabs' : 'Chưa có key ElevenLabs'}
            </Tag>
          }
          style={{ marginBottom: 16 }}
        >
          <Form.Item label="Nhà cung cấp">
            <Select
              value={voiceChoice}
              onChange={(v) => {
                setVoiceChoice(v);
                try {
                  localStorage.setItem('kit.famixaSeries.voice', v);
                } catch {
                  /* quota */
                }
              }}
              options={[
                { value: 'elevenlabs', label: 'ElevenLabs' },
                { value: 'f5', label: 'F5-TTS (Fal)' },
              ]}
            />
          </Form.Item>
          {voiceChoice === 'elevenlabs' ? (
            <>
              <Form.Item name="elevenLabsApiKeySecretRef" label="Secret ref">
                <Input placeholder="ELEVENLABS_API_KEY" autoComplete="off" />
              </Form.Item>
              <Form.Item name="elevenLabsApiKey" label="API key (chỉ ghi — để trống nếu giữ nguyên)">
                <Input.Password placeholder="Dán key ElevenLabs" autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="elevenLabsVoiceId" label="Voice ID">
                <Input placeholder="Voice ID" autoComplete="off" />
              </Form.Item>
            </>
          ) : (
            <Alert type="info" showIcon message="F5-TTS dùng key Fal ở thẻ Fal phía trên." />
          )}
        </Card>

        <Card
          size="small"
          title="Render Factory"
          extra={
            <Tag color={creatomateConfigured ? 'success' : 'warning'}>
              {creatomateConfigured ? 'Đã có key' : 'Chưa có key'}
            </Tag>
          }
          style={{ marginBottom: 16 }}
        >
          <Form.Item label="Nhà cung cấp">
            <Select
              value={renderChoice}
              onChange={setRenderChoice}
              options={[{ value: 'creatomate', label: 'Creatomate' }]}
            />
          </Form.Item>
          {renderChoice === 'creatomate' ? (
            <>
              <Form.Item name="creatomateApiKeySecretRef" label="Secret ref">
                <Input placeholder="CREATOMATE_API_KEY" autoComplete="off" />
              </Form.Item>
              <Form.Item name="creatomateApiKey" label="API key (chỉ ghi — để trống nếu giữ nguyên)">
                <Input.Password placeholder="Dán key Creatomate" autoComplete="new-password" />
              </Form.Item>
              <Form.Item name="creatomateTemplateId" label="Template UUID">
                <Input placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autoComplete="off" />
              </Form.Item>
              <Form.Item name="publicMediaBaseUrl" label="Public media URL">
                <Input placeholder="https://…" autoComplete="off" />
              </Form.Item>
            </>
          ) : null}
        </Card>
        <Space wrap>
          <Button type="primary" icon={<SaveOutlined />} loading={savingVideo} onClick={() => void onSaveVideo()}>
            Lưu video
          </Button>
          <Button icon={<ApiOutlined />} loading={testingVideo} onClick={() => void onTestVideo()}>
            Test Creatomate / giọng
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
        </Space>
      </Form>

      <Form
        form={facebookForm}
        layout="vertical"
        style={{ maxWidth: 720, display: tab === 'facebook' ? undefined : 'none' }}
      >
        {fbReturnTo ? (
          <Alert
            type="warning"
            showIcon
            style={{ marginBottom: 16 }}
            message="Thiếu Facebook App — chưa login được"
            description="Lấy App ID + App Secret trên developers.facebook.com (cùng app bạn dùng trong Graph Explorer), dán vào đây, Lưu, rồi quay lại bài viết bấm Kết nối lại. Không dán token Explorer."
            action={
              <Button size="small" onClick={() => navigate(fbReturnTo)}>
                Quay lại bài viết
              </Button>
            }
          />
        ) : null}
        <Alert
          type={facebookConfigured ? 'success' : 'info'}
          showIcon
          style={{ marginBottom: 16 }}
          message="Facebook Login cho Fanpage — không dán token Explorer"
          description={
            <>
              Tạo app trên developers.facebook.com, bật Facebook Login, thêm Redirect URI trùng ô dưới.
              Quyền: pages_show_list, pages_manage_posts, pages_read_engagement, business_management.
              App Dev mode chỉ admin/tester của app login được. App Secret chỉ ghi, không hiện lại.
              {facebookHint ? <div style={{ marginTop: 8 }}>{facebookHint}</div> : null}
            </>
          }
        />
        <Form.Item name="appId" label="App ID">
          <Input placeholder="Meta App ID" autoComplete="off" />
        </Form.Item>
        <Form.Item name="appSecret" label="App Secret (chỉ ghi — để trống nếu giữ nguyên)">
          <Input.Password placeholder="Dán App Secret" autoComplete="new-password" />
        </Form.Item>
        <Form.Item
          name="redirectUri"
          label="OAuth Redirect URI"
          extra="Phải khai đúng chuỗi này trong Valid OAuth Redirect URIs của Meta app."
        >
          <Input placeholder="http://localhost:5173/content/facebook/callback" autoComplete="off" />
        </Form.Item>
        <Form.Item name="appIdSecretRef" label="Secret ref App ID (tuỳ chọn)">
          <Input placeholder="FACEBOOK_APP_ID" autoComplete="off" />
        </Form.Item>
        <Form.Item name="appSecretSecretRef" label="Secret ref App Secret (tuỳ chọn)">
          <Input placeholder="FACEBOOK_APP_SECRET" autoComplete="off" />
        </Form.Item>
        <Space wrap>
          <Button type="primary" icon={<SaveOutlined />} loading={savingFacebook} onClick={() => void onSaveFacebook()}>
            Lưu Facebook
          </Button>
          <Button icon={<ApiOutlined />} loading={testingFacebook} onClick={() => void onTestFacebook()}>
            Test App Meta
          </Button>
          <Button icon={<ReloadOutlined />} onClick={() => void load()}>
            Tải lại
          </Button>
        </Space>
      </Form>
    </Card>
  );
}
