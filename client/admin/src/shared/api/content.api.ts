import { http } from '@/shared/api/http';
import { useAuthStore } from '@/shared/auth/auth.store';
import { isAxiosError } from 'axios';

export type ContentAiConfig = {
  provider: string;
  textModel: string;
  imageModel?: string | null;
  imagesEnabled: boolean;
  geminiApiKeySecretRef?: string | null;
  apiKeyConfigured: boolean;
};

export type ContentVideoConfig = {
  creatomateApiKeySecretRef?: string | null;
  creatomateConfigured: boolean;
  elevenLabsApiKeySecretRef?: string | null;
  elevenLabsConfigured: boolean;
  elevenLabsVoiceId?: string | null;
  publicMediaBaseUrl?: string | null;
  creatomateTemplateId?: string | null;
  runwayApiKeySecretRef?: string | null;
  runwayConfigured: boolean;
  falApiKeySecretRef?: string | null;
  falConfigured: boolean;
};

export type ContentFacebookConfig = {
  appId?: string | null;
  appSecretConfigured: boolean;
  appIdSecretRef?: string | null;
  appSecretSecretRef?: string | null;
  redirectUri?: string | null;
};

export type ContentOrgSettings = {
  id: string;
  monthlyCeilingUsd: number;
  maxImageCandidatesPerItem: number;
  regenMultiplier: number;
  defaultImageTier: string;
  imageRateUsd: Record<string, number>;
  textPackEstimateUsd: number;
  variantKinds: string[];
  connectorTypes: string[];
  channelTypes: string[];
  ai: ContentAiConfig;
  video?: ContentVideoConfig;
  facebook?: ContentFacebookConfig;
  monthSpendEstimateUsd: number;
  remainingBudgetUsd: number;
  updatedAt: string;
};

export type ContentAiTestResult = {
  ok: boolean;
  message?: string | null;
  apiKeyConfigured: boolean;
  textModel?: string | null;
};

export type ContentVideoTestResult = {
  creatomateOk: boolean;
  creatomateMessage?: string | null;
  creatomateConfigured: boolean;
  elevenLabsOk: boolean;
  elevenLabsMessage?: string | null;
  elevenLabsConfigured: boolean;
  voiceId?: string | null;
  runwayOk?: boolean;
  runwayMessage?: string | null;
  runwayConfigured?: boolean;
  falOk?: boolean;
  falMessage?: string | null;
  falConfigured?: boolean;
};

export type ContentSeriesTurboTask = {
  taskId: string;
  status: string;
  videoUrl?: string | null;
  error?: string | null;
  usedPlaceholderImage: boolean;
  model: string;
  seconds: number;
  failureCode?: string | null;
  videoBytes?: number | null;
  videoMime?: string | null;
  videoVerified?: boolean;
};

export type ContentBudgetSnapshot = {
  globalCeilingUsd: number;
  globalSpendUsd: number;
  globalRemainingUsd: number;
  defaultImageTier: string;
  brands: Array<{
    brandId: string;
    brandCode: string;
    brandName: string;
    effectiveCeilingUsd: number;
    spendUsd: number;
    remainingUsd: number;
    effectiveImageTier: string;
    pauseWhenExceeded: boolean;
  }>;
};

export type ContentBrandKnowledge = {
  positioning?: string | null;
  audience?: string | null;
  tone: string[];
  forbiddenTopics: string[];
  preferredTerms: string[];
  avoidTerms: string[];
  hashtags: string[];
  ctaStyle?: string | null;
  voiceNotes?: string | null;
  visualStyle?: string | null;
  visualColors?: string | null;
  imageNotes?: string | null;
  problems: string[];
  needs: string[];
  desires: string[];
  contentPillars: string[];
  claimsAllowed: string[];
  claimsForbidden: string[];
  products: string[];
  services: string[];
  differentiators: string[];
  proofPoints: string[];
  competitors: string[];
  goodExamples: string[];
  badExamples: string[];
};

export type ContentBrand = {
  id: string;
  code: string;
  name: string;
  defaultCtaUrl?: string | null;
  defaultCtaLabel?: string | null;
  monthlyCeilingUsd?: number | null;
  imageTier?: string | null;
  pauseWhenExceeded: boolean;
  isActive: boolean;
  sortOrder: number;
  operationalBrief?: string | null;
  knowledge: ContentBrandKnowledge;
  monthSpendEstimateUsd: number;
  updatedAt: string;
  brainReady?: boolean;
  brainMissing?: string[] | null;
};

export type ContentSiteTarget = {
  id: string;
  brandId: string;
  code: string;
  name: string;
  connectorType: string;
  baseUrl?: string | null;
  configJson: string;
  secretRef?: string | null;
  secretConfigured: boolean;
  isActive: boolean;
  sortOrder: number;
};

export type ContentChannelTarget = {
  id: string;
  brandId: string;
  code: string;
  name: string;
  channelType: string;
  externalId?: string | null;
  configJson: string;
  secretRef?: string | null;
  secretConfigured: boolean;
  isActive: boolean;
  sortOrder: number;
};

export type ContentWriteSlot = {
  key: string;
  label: string;
  destType: string;
  variantKinds: string[];
};

export type ContentWritePlan = {
  brandId: string;
  brandCode: string;
  brandName: string;
  slots: ContentWriteSlot[];
  variantKinds: string[];
  summary: string;
};

export type ContentTopic = {
  id: string;
  brandId: string;
  brandCode: string;
  brandName: string;
  title: string;
  pillar?: string | null;
  goal: string;
  ctaUrl?: string | null;
  utmCampaign?: string | null;
  priority: string;
  status: string;
  bodyOutline?: string | null;
  displayAt?: string | null;
  createdAt: string;
  updatedAt: string;
  variantCount?: number;
  corePackageId?: string | null;
  coreTitle?: string | null;
};

export type ContentCoreIdea = {
  insight?: string | null;
  problem?: string | null;
  coreMessage?: string | null;
  keywords: string[];
  source?: string | null;
  sourceUrl?: string | null;
  sourceType?: string | null;
  evidence?: string | null;
  factOrOpinion?: string | null;
};

export type ContentQualityGate = {
  passed: boolean;
  issues: string[];
  checkedAt: string;
  blockingIssues?: string[] | null;
  approveIssues?: string[] | null;
  canPublish?: boolean;
  canApprove?: boolean;
};

export type ContentCreativeBrief = {
  objective?: string | null;
  emotion?: string | null;
  format?: string | null;
  visualDirection?: string | null;
  durationSec?: number | null;
};

export type ContentPerformance = {
  id: string;
  packageId: string;
  topicId: string;
  brandId: string;
  brandCode: string;
  brandName: string;
  channel: string;
  metricDate: string;
  impressions?: number | null;
  views?: number | null;
  clicks?: number | null;
  engagements?: number | null;
  comments?: number | null;
  shares?: number | null;
  utmCampaign?: string | null;
  utmSource?: string | null;
  utmMedium?: string | null;
  notes?: string | null;
  createdAt: string;
};

export type ContentBrandFit = {
  brandId: string;
  brandCode: string;
  brandName: string;
  verdict: string;
  score: number;
  reason?: string | null;
  title?: string | null;
  angle?: string | null;
  audience?: string | null;
  cta?: string | null;
  packageId?: string | null;
  outline?: string | null;
};

export type ContentPackage = {
  id: string;
  brandId: string;
  brandCode: string;
  brandName: string;
  topicId: string;
  title: string;
  angle?: string | null;
  audience?: string | null;
  contentType: string;
  pillar?: string | null;
  goal: string;
  priority: string;
  status: string;
  sourcePackageId?: string | null;
  sourceTitle?: string | null;
  displayAt?: string | null;
  variantCount: number;
  createdAt: string;
  updatedAt: string;
  coreIdea?: ContentCoreIdea | null;
  brandFits?: ContentBrandFit[] | null;
  adaptationCount?: number;
  qualityGate?: ContentQualityGate | null;
  creativeBrief?: ContentCreativeBrief | null;
};

export type ContentVariant = {
  id: string;
  topicId: string;
  kind: string;
  title?: string | null;
  bodyMarkdown: string;
  metaJson: string;
  updatedAt: string;
};

export type ContentAsset = {
  id: string;
  topicId: string;
  kind: string;
  fileName: string;
  contentType: string;
  prompt?: string | null;
  model?: string | null;
  imageTier?: string | null;
  estimateUsd: number;
  isSelected: boolean;
  createdAt: string;
};

export type ContentPublishJob = {
  id: string;
  topicId: string;
  brandId: string;
  targetKind: string;
  siteTargetId?: string | null;
  channelTargetId?: string | null;
  connectorType: string;
  status: string;
  publishAt?: string | null;
  externalRef?: string | null;
  lastError?: string | null;
  resultJson: string;
  createdAt: string;
  updatedAt: string;
};

export type ContentTopicDetail = {
  topic: ContentTopic;
  variants: ContentVariant[];
  assets: ContentAsset[];
  jobs: ContentPublishJob[];
};

export type ContentPackageDetail = {
  package: ContentPackage;
  topicDetail: ContentTopicDetail;
  adaptations: ContentPackage[];
};

export type ContentVideoTemplate = {
  id: string;
  code: string;
  name: string;
  provider: string;
  externalTemplateId?: string | null;
  aspectRatio: string;
  durationSec: number;
  description?: string | null;
  configJson: string;
  isActive: boolean;
  sortOrder: number;
};

export type ContentVideoJob = {
  id: string;
  brandId: string;
  brandCode: string;
  brandName: string;
  packageId?: string | null;
  topicId?: string | null;
  templateId: string;
  templateCode: string;
  templateName: string;
  title: string;
  scriptBody: string;
  status: string;
  provider: string;
  externalRenderId?: string | null;
  previewUrl?: string | null;
  outputUrl?: string | null;
  errorMessage?: string | null;
  storyboardJson: string;
  configJson: string;
  createdAt: string;
  updatedAt: string;
  renderedAt?: string | null;
};

export type GenerateContentResult = {
  topic: ContentTopic;
  variants: ContentVariant[];
  assets: ContentAsset[];
  estimatedSpendUsd: number;
  budgetBlocked: boolean;
  message?: string | null;
};

export async function fetchContentSettings() {
  const { data } = await http.get<ContentOrgSettings>('/content/settings');
  return data;
}

export async function updateContentSettings(body: Partial<{
  monthlyCeilingUsd: number;
  maxImageCandidatesPerItem: number;
  regenMultiplier: number;
  defaultImageTier: string;
  imageRateUsd: Record<string, number>;
  textPackEstimateUsd: number;
  variantKinds: string[];
  connectorTypes: string[];
  channelTypes: string[];
  ai: Partial<{
    provider: string;
    textModel: string;
    imageModel: string | null;
    imagesEnabled: boolean;
    geminiApiKeySecretRef: string | null;
    geminiApiKey: string | null;
  }>;
  video: Partial<{
    creatomateApiKeySecretRef: string | null;
    creatomateApiKey: string | null;
    elevenLabsApiKeySecretRef: string | null;
    elevenLabsApiKey: string | null;
    elevenLabsVoiceId: string | null;
    publicMediaBaseUrl: string | null;
    creatomateTemplateId: string | null;
    runwayApiKeySecretRef: string | null;
    runwayApiKey: string | null;
    falApiKeySecretRef: string | null;
    falApiKey: string | null;
  }>;
  facebook: Partial<{
    appId: string | null;
    appIdSecretRef: string | null;
    appSecretSecretRef: string | null;
    appSecret: string | null;
    redirectUri: string | null;
  }>;
}>) {
  const { data } = await http.put<ContentOrgSettings>('/content/settings', body);
  return data;
}

export async function testContentAi() {
  const { data } = await http.post<ContentAiTestResult>('/content/ai/test');
  return data;
}

export async function testContentVideo() {
  const { data } = await http.post<ContentVideoTestResult>('/content/video/test');
  return data;
}

export async function startContentSeriesTurbo(body: {
  clipId: string;
  prompt: string;
  negativePrompt?: string;
  imageDataUrl?: string;
  seconds: number;
  ratio: string;
  engine?: 'turbo' | 'wan';
}) {
  const { data } = await http.post<ContentSeriesTurboTask>('/content/series/turbo', body, { timeout: 180_000 });
  return data;
}

export async function rewriteContentSeriesKfNote(body: {
  note: string;
  action?: string;
  location?: string;
}) {
  const { data } = await http.post<{
    instruction: string;
    place: boolean;
    lighting: boolean;
    wardrobe: boolean;
    camera: boolean;
    inherit: boolean;
  }>('/content/series/kf-note', body, { timeout: 60_000 });
  return data;
}

export async function qaContentSeriesStill(body: { imageDataUrl: string; specJson: string }) {
  const { data } = await http.post<{
    status: string;
    total?: number;
    axes?: Record<string, number>;
    hardFails: string[];
    notes?: string;
    hardChecks?: Record<string, string>;
    evidence?: string;
    confidence?: number;
  }>('/content/series/still-qa', body, { timeout: 90_000 });
  return data;
}

export async function generateContentSeriesStill(body: {
  prompt: string;
  aspect: string;
  references: { name: string; imageDataUrl: string; role?: string }[];
}) {
  const { data } = await http.post<{ imageDataUrl: string; model: string; aspect: string }>(
    '/content/series/still',
    body,
    { timeout: 180_000 },
  );
  return data;
}

export async function getContentSeriesTurbo(taskId: string) {
  const { data } = await http.get<ContentSeriesTurboTask>('/content/series/turbo', {
    params: { taskId },
  });
  return data;
}

export async function startContentSeriesLipsync(body: {
  clipId: string;
  videoUrl: string;
  audioBase64: string;
  mime?: string;
  syncMode?: 'cut_off' | 'silence' | 'loop' | 'bounce' | 'remap';
  model?: '1.9' | 'v3' | 'ls';
}) {
  const { data } = await http.post<ContentSeriesTurboTask>('/content/series/lipsync', body, { timeout: 360_000 });
  return data;
}

async function fetchTakeViaBrowser(src: string) {
  const res = await fetch(src, { mode: 'cors' });
  if (!res.ok) throw new Error(`Link take HTTP ${res.status}.`);
  const blob = await res.blob();
  if (blob.size < 80) throw new Error('Link take trống.');
  const type = (blob.type || '').toLowerCase();
  if (type.includes('json') || type.startsWith('text/')) throw new Error('URL không trả file video.');
  return blob;
}

export async function assembleContentSeriesCut(body: {
  fileStem: string;
  aspect?: '16:9' | '9:16';
  clips: {
    code: string;
    videoUrl?: string;
    seconds: number;
    usableStart?: number;
    usableEnd?: number;
    voices: { lineId: string; startSec: number; audioBase64: string; mime?: string }[];
    useVideoAudio?: boolean;
    requireVoice?: boolean;
    stillBase64?: string;
  }[];
}) {
  const { data, headers } = await http.post<Blob>('/content/series/assemble', body, {
    responseType: 'blob',
    timeout: 600_000,
  });
  const type = String(headers['content-type'] ?? data.type ?? '');
  if (type.includes('json') || data.size < 80) {
    throw new Error(await readBlobMessage(data, 'Không ghép được tập.'));
  }
  return data;
}

export async function probeContentSeriesTake(url: string) {
  const src = (url ?? '').trim();
  if (!src) throw new Error('Chưa có link take.');
  const { data } = await http.post<{
    ok: boolean;
    mime?: string | null;
    bytes?: number | null;
    error?: string | null;
  }>('/content/series/take-probe', { url: src }, { timeout: 30_000 });
  return data;
}

export async function fetchContentSeriesTake(url: string) {
  const src = (url ?? '').trim();
  if (!src) throw new Error('Chưa có link take.');
  if (/^(blob:|data:)/i.test(src)) {
    const res = await fetch(src);
    if (!res.ok) throw new Error('Không đọc được take trên máy.');
    return res.blob();
  }
  try {
    const { data, headers } = await http.post<Blob>(
      '/content/series/take-proxy',
      { url: src },
      { responseType: 'blob', timeout: 120_000 },
    );
    const type = String(headers['content-type'] ?? data.type ?? '');
    if (type.includes('json') || (data.size < 80 && !type.includes('video'))) {
      throw new Error(await readBlobMessage(data, 'Không tải được take.'));
    }
    return data;
  } catch (e) {
    const status = isAxiosError(e) ? e.response?.status : undefined;
    let msg = 'Không tải được take.';
    if (isAxiosError(e) && e.response?.data instanceof Blob) {
      msg = await readBlobMessage(e.response.data, msg);
    } else if (e instanceof Error && e.message.trim()) {
      msg = e.message.trim();
    }
    if (status === 404) {
      msg = 'API chưa có take-proxy. Restart KitPlatform.Api (:5290) rồi Tải lại.';
    }
    try {
      return await fetchTakeViaBrowser(src);
    } catch {
      throw new Error(msg);
    }
  }
}

export type ContentSeriesScriptDraft = {
  pack: string;
  model: string;
  estimatedUsd: number;
  costNote: string;
  usedBrandBrain?: boolean;
  brandCode?: string | null;
};

export async function draftContentSeriesScript(body: {
  seed: string;
  charactersHint?: string;
  episodeHint?: string;
  brandId?: string;
}) {
  const { data } = await http.post<ContentSeriesScriptDraft>('/content/series/script-draft', body, {
    timeout: 90_000,
  });
  return data;
}

export type ContentSeriesPilot = {
  seriesCode: string;
  graph: Record<string, unknown>;
  updatedAt: string;
};

export type ContentSeriesVoice = {
  voiceId: string;
  name: string;
  category?: string | null;
  cloned?: boolean;
  vietnamese?: boolean;
  publicOwnerId?: string | null;
  gender?: string | null;
  age?: string | null;
  accent?: string | null;
};

export async function fetchContentSeriesPilot(code = 'FAMIXA') {
  const { data } = await http.get<ContentSeriesPilot>('/content/series/pilot', { params: { code } });
  return data;
}

export async function putContentSeriesPilot(body: { seriesCode?: string; graph: Record<string, unknown> }) {
  const { data } = await http.put<ContentSeriesPilot>('/content/series/pilot', {
    seriesCode: body.seriesCode ?? 'FAMIXA',
    graph: body.graph,
  });
  return data;
}

export type ContentSeriesBuildSummary = {
  id: string;
  seriesCode: string;
  episodeCode: string;
  title: string;
  status: string;
  shotCount: number;
  voiceLines: number;
  kfCount: number;
  videoCount: number;
  createdAt: string;
  updatedAt: string;
};

export type ContentSeriesBuild = ContentSeriesBuildSummary & {
  graph: Record<string, unknown>;
};

export async function fetchContentSeriesBuilds(code = 'FAMIXA') {
  const { data } = await http.get<ContentSeriesBuildSummary[]>('/content/series/builds', { params: { code } });
  return data ?? [];
}

export async function fetchContentSeriesBuild(id: string) {
  const { data } = await http.get<ContentSeriesBuild>(`/content/series/builds/${id}`);
  return data;
}

export async function putContentSeriesBuild(body: {
  id?: string;
  seriesCode?: string;
  graph: Record<string, unknown>;
}) {
  const { data } = await http.put<ContentSeriesBuild>('/content/series/builds', {
    id: body.id,
    seriesCode: body.seriesCode ?? 'FAMIXA',
    graph: body.graph,
  });
  return data;
}

export async function deleteContentSeriesBuild(id: string) {
  await http.delete(`/content/series/builds/${id}`);
}

export async function fetchContentSeriesVoices() {
  const { data } = await http.get<ContentSeriesVoice[]>('/content/series/voices', { timeout: 60_000 });
  return data;
}

function looksLikeScreenplayTts(text: string) {
  const s = text.trim();
  if (s.split(/\r?\n/).filter(Boolean).length >= 4) return true;
  if (/VIDEO ID:|07\.\s*SCRIPT/i.test(s)) return true;
  if (/(?:^|\n)(?:SC|SCENE)\s*0*\d+\b/i.test(s) && s.length > 40) return true;
  if (/\n(?:MINH|NAM|LINH|BỐ|MẸ)\s*:/i.test(s) && s.length > 60) return true;
  return false;
}

export async function previewContentSeriesTts(body: {
  voiceId: string;
  text: string;
  publicOwnerId?: string;
  voiceName?: string;
  accent?: string;
  stability?: number;
  similarityBoost?: number;
  style?: number;
  speed?: number;
}) {
  const spoken = (body.text ?? '').replace(/\[[^\]]+\]\s*/g, '').replace(/\s+/g, ' ').trim();
  if (spoken.length < 1) throw new Error('Câu thoại quá ngắn để đọc.');
  if (looksLikeScreenplayTts(spoken)) {
    throw new Error('TTS chỉ nhận Voice Script (thoại CHAR). Không gửi heading/cảnh/action.');
  }
  const voiceId = (body.voiceId ?? '').trim();
  if (voiceId.length < 8) throw new Error('Chưa gán Voice Canon ElevenLabs.');
  try {
    const { data, headers } = await http.post<Blob>(
      '/content/series/tts',
      {
        voiceId,
        text: spoken,
        publicOwnerId: body.publicOwnerId,
        voiceName: body.voiceName,
        accent: body.accent,
        voiceSettings: {
          stability: body.stability,
          similarityBoost: body.similarityBoost,
          style: body.style,
          speed: body.speed,
        },
      },
      {
        responseType: 'blob',
        timeout: 60_000,
      },
    );
    const type = String(headers['content-type'] ?? data.type ?? '');
    if (type.includes('json') || (data.size < 80 && !type.includes('audio'))) {
      throw new Error(await readBlobMessage(data, 'Không tạo được tiếng.'));
    }
    return data;
  } catch (e) {
    if (isAxiosError(e) && e.response?.data instanceof Blob) {
      throw new Error(await readBlobMessage(e.response.data, 'Không tạo được tiếng.'));
    }
    throw e;
  }
}

async function readBlobMessage(blob: Blob, fallback = 'Không đọc được phản hồi.') {
  const raw = await blob.text();
  let message = raw || fallback;
  try {
    const parsed = JSON.parse(raw) as { message?: string; title?: string };
    message = (parsed.message || parsed.title || message).trim();
  } catch {
    /* keep raw */
  }
  return message;
}

export type ContentFacebookTestResult = {
  ok: boolean;
  message?: string | null;
  appSecretConfigured: boolean;
  appId?: string | null;
};

export type ContentFacebookPageOption = { id: string; name: string };

export type ContentFacebookPending = {
  sessionId: string;
  brandId: string;
  pages: ContentFacebookPageOption[];
};

export type ContentFacebookVerify = {
  ok: boolean;
  status: string;
  pageId?: string | null;
  pageName?: string | null;
  message?: string | null;
  lastVerifiedAt?: string | null;
};

export async function testContentFacebook() {
  const { data } = await http.post<ContentFacebookTestResult>('/content/facebook/test');
  return data;
}

export async function startFacebookOAuth(brandId: string) {
  const { data } = await http.get<{ url: string; state: string }>('/content/facebook/oauth/start', {
    params: { brandId },
  });
  return data;
}

export async function completeFacebookOAuth(code: string, state: string) {
  const { data } = await http.post<ContentFacebookPending>('/content/facebook/oauth/complete', { code, state });
  return data;
}

export async function fetchFacebookPending(sessionId: string) {
  const { data } = await http.get<ContentFacebookPending>(`/content/facebook/oauth/pending/${sessionId}`);
  return data;
}

export async function selectFacebookPage(sessionId: string, pageId: string) {
  const { data } = await http.post<ContentChannelTarget>('/content/facebook/oauth/select', { sessionId, pageId });
  return data;
}

export async function verifyFacebookChannel(channelId: string) {
  const { data } = await http.post<ContentFacebookVerify>(`/content/channels/${channelId}/facebook/verify`);
  return data;
}

export async function disconnectFacebookChannel(channelId: string) {
  const { data } = await http.post<ContentChannelTarget>(`/content/channels/${channelId}/facebook/disconnect`);
  return data;
}

export async function fetchContentBudget() {
  const { data } = await http.get<ContentBudgetSnapshot>('/content/budget');
  return data;
}

export async function fetchContentBrands(activeOnly?: boolean) {
  const { data } = await http.get<ContentBrand[]>('/content/brands', {
    // Omit param = list tất cả. true = chỉ đang dùng. false = chỉ đã tắt (không dùng cho “xem hết”).
    params: activeOnly === undefined ? undefined : { activeOnly },
  });
  return data;
}

export async function createContentBrand(body: {
  code: string;
  name: string;
  defaultCtaUrl?: string;
  defaultCtaLabel?: string;
  monthlyCeilingUsd?: number | null;
  imageTier?: string | null;
  pauseWhenExceeded?: boolean;
  isActive?: boolean;
  sortOrder?: number;
  operationalBrief?: string | null;
  knowledge?: Partial<ContentBrandKnowledge> | null;
}) {
  const { data } = await http.post<ContentBrand>('/content/brands', body);
  return data;
}

export async function updateContentBrand(id: string, body: Parameters<typeof createContentBrand>[0]) {
  const { data } = await http.put<ContentBrand>(`/content/brands/${id}`, body);
  return data;
}

export async function fetchContentSites(brandId: string) {
  const { data } = await http.get<ContentSiteTarget[]>(`/content/brands/${brandId}/sites`);
  return data;
}

export async function deleteContentSite(brandId: string, siteId: string) {
  await http.delete(`/content/brands/${brandId}/sites/${siteId}`);
}

export async function upsertContentSite(
  brandId: string,
  body: {
    code: string;
    name: string;
    connectorType: string;
    baseUrl?: string;
    configJson?: string;
    secretRef?: string | null;
    /** Write-only. Omit to keep existing; "" to clear. */
    secret?: string | null;
    isActive?: boolean;
    sortOrder?: number;
  },
) {
  const { data } = await http.put<ContentSiteTarget>(`/content/brands/${brandId}/sites`, body);
  return data;
}

export async function fetchContentChannels(brandId: string) {
  const { data } = await http.get<ContentChannelTarget[]>(`/content/brands/${brandId}/channels`);
  return data;
}

export async function fetchContentWritePlans(brandId?: string) {
  const { data } = await http.get<ContentWritePlan[]>('/content/write-plans', {
    params: brandId ? { brandId } : undefined,
  });
  return data;
}

export async function deleteContentChannel(brandId: string, channelId: string) {
  await http.delete(`/content/brands/${brandId}/channels/${channelId}`);
}

export async function upsertContentChannel(
  brandId: string,
  body: {
    code: string;
    name: string;
    channelType: string;
    externalId?: string;
    configJson?: string;
    secretRef?: string | null;
    /** Write-only. Omit to keep existing; "" to clear. */
    secret?: string | null;
    isActive?: boolean;
    sortOrder?: number;
  },
) {
  const { data } = await http.put<ContentChannelTarget>(`/content/brands/${brandId}/channels`, body);
  return data;
}

export async function fetchContentTopics(params?: { brandId?: string; status?: string }) {
  const { data } = await http.get<ContentTopic[]>('/content/topics', { params });
  return data;
}

export async function fetchContentPackages(params?: {
  brandId?: string;
  status?: string;
  coresOnly?: boolean;
}) {
  const { data } = await http.get<ContentPackage[]>('/content/packages', { params });
  return data;
}

export async function fetchContentPackageDetail(id: string) {
  const { data } = await http.get<ContentPackageDetail>(`/content/packages/${id}/detail`);
  return data;
}

export async function createContentPackage(body: {
  brandId: string;
  title: string;
  angle?: string;
  audience?: string;
  contentType?: string;
  pillar?: string;
  goal?: string;
  priority?: string;
  bodyOutline?: string;
  displayAt?: string | null;
  ctaUrl?: string;
  insight?: string;
  problem?: string;
  coreMessage?: string;
  keywords?: string[];
  source?: string;
  sourceUrl?: string;
  sourceType?: string;
  evidence?: string;
  factOrOpinion?: string;
  creativeBrief?: ContentCreativeBrief;
}) {
  const { data } = await http.post<ContentPackage>('/content/packages', body);
  return data;
}

export async function updateContentPackage(id: string, body: Parameters<typeof createContentPackage>[0]) {
  const { data } = await http.put<ContentPackage>(`/content/packages/${id}`, body);
  return data;
}

export type ContentWorkJob = {
  id: string;
  kind: string;
  status: string;
  brandId?: string | null;
  brandCode?: string | null;
  brandName?: string | null;
  topicId?: string | null;
  packageId?: string | null;
  videoJobId?: string | null;
  title?: string | null;
  errorMessage?: string | null;
  retryCount: number;
  maxRetries: number;
  availableAt: string;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  message?: string | null;
};

export type ContentWorkEnqueueResult = {
  job: ContentWorkJob;
  message: string;
};

export type ContentCalendarItem = {
  at: string;
  kind: string;
  packageId?: string | null;
  topicId?: string | null;
  publishJobId?: string | null;
  brandId: string;
  brandCode: string;
  brandName: string;
  title: string;
  channel?: string | null;
  status: string;
};

export type ContentOpsSnapshot = {
  reviewCount: number;
  generatingCount: number;
  scheduledCount: number;
  publishedTodayCount: number;
  errorCount: number;
  monthSpendUsd: number;
  monthCeilingUsd: number;
  brands: Array<{
    brandId: string;
    brandCode: string;
    brandName: string;
    reviewCount: number;
    scheduledCount: number;
    publishedMonthCount: number;
    spendUsd: number;
  }>;
  activeJobs: ContentWorkJob[];
  coreIdeaCount: number;
  coreDraftCount: number;
  coreUnscoredCount: number;
  adaptationCount: number;
  scheduledThisWeek: number;
  publishedThisWeek: number;
  coreIdeas: ContentPackage[];
  weekItems: ContentCalendarItem[];
  recentErrors: ContentWorkJob[];
  budgetBlockedCount: number;
  facebookAppConfigured: boolean;
  failedPublishJobs: ContentOpsFailedPublish[];
};

export type ContentOpsFailedPublish = {
  jobId: string;
  topicId: string;
  topicTitle: string;
  connectorType: string;
  lastError?: string | null;
  updatedAt: string;
};

export async function fetchContentOps() {
  const { data } = await http.get<ContentOpsSnapshot>('/content/ops');
  return data;
}

export async function fetchContentCalendar(params: { from: string; to: string; brandId?: string }) {
  const { data } = await http.get<ContentCalendarItem[]>('/content/calendar', { params });
  return data;
}

export async function fetchContentWorkJob(id: string) {
  const { data } = await http.get<ContentWorkJob>(`/content/work/${id}`);
  return data;
}

export async function fetchContentPublishJobs(topicId?: string) {
  const { data } = await http.get<ContentPublishJob[]>('/content/jobs', { params: { topicId } });
  return data;
}

function workFailedError(job: ContentWorkJob) {
  const msg = job.errorMessage || job.message || 'Job thất bại';
  return Object.assign(new Error(msg), { response: { data: { message: msg } } });
}

export async function waitForContentWork(
  id: string,
  opts?: { intervalMs?: number; timeoutMs?: number },
) {
  const interval = opts?.intervalMs ?? 2000;
  const timeout = opts?.timeoutMs ?? 300_000;
  const start = Date.now();
  for (;;) {
    const job = await fetchContentWorkJob(id);
    if (job.status === 'Succeeded' || job.status === 'Failed' || job.status === 'Cancelled') {
      return job;
    }
    if (Date.now() - start > timeout) {
      throw Object.assign(new Error('Hết thời gian chờ job nền'), {
        response: { data: { message: 'Hết thời gian chờ job nền' } },
      });
    }
    await new Promise((r) => setTimeout(r, interval));
  }
}

async function enqueueAndWait(
  path: string,
  body: unknown,
  timeoutMs: number,
) {
  const { data } = await http.post<ContentWorkEnqueueResult>(path, body ?? {}, { timeout: 30_000 });
  const job = await waitForContentWork(data.job.id, { timeoutMs });
  if (job.status === 'Failed' || job.status === 'Cancelled') throw workFailedError(job);
  return job;
}

export async function generateContentPackage(
  id: string,
  body?: {
    skipImages?: boolean;
    candidateCount?: number;
    imagesOnly?: boolean;
    variantKinds?: string[];
  },
) {
  return enqueueAndWait(`/content/packages/${id}/generate`, body ?? {}, 300_000);
}

export async function downloadContentPackageExport(id: string) {
  const { data, headers } = await http.get<Blob>(`/content/packages/${id}/export`, {
    responseType: 'blob',
    timeout: 60_000,
  });
  const match = /filename="?([^"]+)"?/i.exec(String(headers['content-disposition'] ?? ''));
  const name = match?.[1] ?? `content-pack-${id.slice(0, 8)}.zip`;
  const url = URL.createObjectURL(data);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

export async function updateContentPackageBrief(id: string, body: ContentCreativeBrief) {
  const { data } = await http.put<ContentPackage>(`/content/packages/${id}/brief`, body);
  return data;
}

export async function fetchContentPackagePerformance(id: string) {
  const { data } = await http.get<ContentPerformance[]>(`/content/packages/${id}/performance`);
  return data;
}

export async function ingestContentPackagePerformance(
  id: string,
  body: {
    channel: string;
    metricDate: string;
    impressions?: number | null;
    views?: number | null;
    clicks?: number | null;
    engagements?: number | null;
    comments?: number | null;
    shares?: number | null;
    utmCampaign?: string;
    utmSource?: string;
    utmMedium?: string;
    notes?: string;
  },
) {
  const { data } = await http.post<ContentPerformance>(`/content/packages/${id}/performance`, body);
  return data;
}

export async function adaptContentPackageMulti(
  id: string,
  body?: { brandIds?: string[]; includeMaybe?: boolean; generateFits?: boolean },
) {
  return enqueueAndWait(`/content/packages/${id}/adapt-multi`, body ?? {}, 180_000);
}

export async function createContentPool(body: {
  homeBrandId?: string;
  ideas: Array<{
    title: string;
    insight?: string;
    problem?: string;
    coreMessage?: string;
    angle?: string;
    audience?: string;
    goal?: string;
    source?: string;
    sourceUrl?: string;
    sourceType?: string;
    evidence?: string;
    factOrOpinion?: string;
  }>;
}) {
  const { data } = await http.post<{ packages: ContentPackage[]; message?: string | null }>(
    '/content/packages/pool',
    body,
  );
  return data;
}

export type ContentPoolSuggestion = {
  title: string;
  insight?: string | null;
  problem?: string | null;
  coreMessage?: string | null;
  whyNext?: string | null;
  fromTitle?: string | null;
  fromPackageId?: string | null;
  gap?: string | null;
  suggestedBrands?: string | null;
  factOrOpinion?: string | null;
};

export async function suggestContentPool(body?: { limit?: number; packageIds?: string[] }) {
  const { data } = await http.post<{ ideas: ContentPoolSuggestion[]; message?: string | null }>(
    '/content/packages/pool/suggest',
    body ?? { limit: 4 },
    { timeout: 180_000 },
  );
  return data;
}

export async function analyzeContentPool(body: {
  packageIds: string[];
  brandIds?: string[];
  includeMaybe?: boolean;
}) {
  const { data } = await http.post<{ jobs: ContentWorkEnqueueResult[]; message?: string | null }>(
    '/content/packages/pool/analyze',
    body,
    { timeout: 30_000 },
  );
  const ids = (data.jobs ?? [])
    .map((j) => j.job?.id)
    .filter((id): id is string => Boolean(id));
  if (ids.length === 0) {
    const msg = data.message || 'Không tạo được job chấm Brand Fit';
    throw Object.assign(new Error(msg), { response: { data: { message: msg } } });
  }
  await waitForContentWorkMany(ids, { timeoutMs: 12 * 60_000 });
  return data;
}

export async function applyContentPoolFits(body: {
  items: Array<{ packageId: string; brandId: string }>;
  generateFits?: boolean;
  variantKinds?: string[];
}) {
  const { data } = await http.post<{
    requested: number;
    created: number;
    skipped: number;
    fits: ContentBrandFit[];
    message?: string | null;
  }>('/content/packages/pool/apply', body);
  return data;
}

async function waitForContentWorkMany(ids: string[], opts?: { timeoutMs?: number; intervalMs?: number }) {
  const timeout = opts?.timeoutMs ?? 180_000;
  const interval = opts?.intervalMs ?? 2_000;
  const start = Date.now();
    const pending = new Set(ids);
    const failed: ContentWorkJob[] = [];
    while (pending.size > 0) {
      if (Date.now() - start > timeout) {
        throw Object.assign(new Error('Hết thời gian chờ chấm Brand Fit'), {
          response: { data: { message: 'Hết thời gian chờ chấm Brand Fit' } },
        });
      }
      for (const id of [...pending]) {
        const job = await fetchContentWorkJob(id);
        if (job.status === 'Succeeded') pending.delete(id);
        else if (job.status === 'Failed' || job.status === 'Cancelled') {
          pending.delete(id);
          failed.push(job);
        }
      }
      if (pending.size > 0) await new Promise((r) => setTimeout(r, interval));
    }
    if (failed.length > 0) {
      const first = workFailedError(failed[0]);
      if (failed.length === 1) throw first;
      throw Object.assign(
        new Error(`${failed.length} job Brand Fit thất bại. ${first.message}`),
        { response: { data: { message: `${failed.length} job Brand Fit thất bại. ${first.message}` } } },
      );
    }
}

export async function adaptContentPackage(
  id: string,
  body: { targetBrandId: string; title?: string; angle?: string; bodyOutline?: string; displayAt?: string | null },
) {
  const { data } = await http.post<ContentPackage>(`/content/packages/${id}/adapt`, body);
  return data;
}

export async function approveContentPackage(id: string) {
  const { data } = await http.post<ContentPackage>(`/content/packages/${id}/approve`);
  return data;
}

export async function approveContentPackagesBatch(packageIds: string[]) {
  const { data } = await http.post<{
    requested: number;
    approved: number;
    failedIds: string[];
    message?: string | null;
  }>('/content/packages/approve-batch', { packageIds });
  return data;
}

export async function fetchContentVideoTemplates(activeOnly = true) {
  const { data } = await http.get<ContentVideoTemplate[]>('/content/video/templates', {
    params: { activeOnly },
  });
  return data;
}

export async function fetchContentVideoJobs(params?: { brandId?: string; status?: string }) {
  const { data } = await http.get<ContentVideoJob[]>('/content/video/jobs', { params });
  return data;
}

export async function fetchContentVideoJob(id: string) {
  const { data } = await http.get<ContentVideoJob>(`/content/video/jobs/${id}`);
  return data;
}

export async function createContentVideoJobFromPackage(body: {
  packageId: string;
  templateId?: string;
  templateCode?: string;
}) {
  const { data } = await http.post<ContentVideoJob>('/content/video/jobs/from-package', body);
  return data;
}

export async function updateContentVideoJobScript(id: string, scriptBody: string) {
  const { data } = await http.put<ContentVideoJob>(`/content/video/jobs/${id}/script`, { scriptBody });
  return data;
}

export async function prepareContentVideoStoryboard(id: string) {
  const { data } = await http.post<ContentVideoJob>(`/content/video/jobs/${id}/storyboard`);
  return data;
}

export async function runContentVideoMvpPipeline(
  id: string,
  body?: { generateImages?: boolean; generateVoice?: boolean; render?: boolean },
) {
  await enqueueAndWait(
    `/content/video/jobs/${id}/mvp-pipeline`,
    {
      generateImages: body?.generateImages ?? true,
      generateVoice: body?.generateVoice ?? true,
      render: body?.render ?? true,
    },
    300_000,
  );
  return fetchContentVideoJob(id);
}

export async function renderContentVideoJob(id: string) {
  await enqueueAndWait(`/content/video/jobs/${id}/render`, {}, 180_000);
  return fetchContentVideoJob(id);
}

export async function refreshContentVideoJob(id: string) {
  const { data } = await http.post<ContentVideoJob>(`/content/video/jobs/${id}/refresh`);
  return data;
}

export async function approveContentVideoJob(id: string) {
  const { data } = await http.post<ContentVideoJob>(`/content/video/jobs/${id}/approve`);
  return data;
}

export async function fetchContentTopicDetail(id: string) {
  const { data } = await http.get<ContentTopicDetail>(`/content/topics/${id}/detail`);
  return data;
}

export async function createContentTopic(body: {
  brandId: string;
  title: string;
  pillar?: string;
  goal?: string;
  ctaUrl?: string;
  utmCampaign?: string;
  priority?: string;
  status?: string;
  bodyOutline?: string;
  displayAt?: string | null;
}) {
  const { data } = await http.post<ContentTopic>('/content/topics', body);
  return data;
}

export async function updateContentTopic(id: string, body: Parameters<typeof createContentTopic>[0]) {
  const { data } = await http.put<ContentTopic>(`/content/topics/${id}`, body);
  return data;
}

export async function generateContentTopic(
  id: string,
  body?: { skipImages?: boolean; candidateCount?: number; imagesOnly?: boolean },
) {
  return enqueueAndWait(`/content/topics/${id}/generate`, body ?? {}, 300_000);
}

export async function approveContentTopic(id: string) {
  const { data } = await http.post<ContentTopic>(`/content/topics/${id}/approve`);
  return data;
}

export async function deleteContentTopic(id: string) {
  await http.delete(`/content/topics/${id}`);
}

export async function selectContentAsset(topicId: string, assetId: string) {
  await http.post(`/content/topics/${topicId}/assets/${assetId}/select`);
}

export async function publishContentTopic(
  id: string,
  body?: {
    siteTargetIds?: string[];
    channelTargetIds?: string[];
    includeManualExport?: boolean;
    runImmediately?: boolean;
    publishAt?: string | null;
    /** Preferred: binary image via multipart (reliable). */
    imageBlob?: Blob;
    imageBase64?: string;
    imageFileName?: string;
    imageContentType?: string;
  },
) {
  const hasImage = !!(body?.imageBlob || body?.imageBase64);
  if (hasImage) {
    const form = new FormData();
    form.append('includeManualExport', String(body?.includeManualExport ?? true));
    form.append('runImmediately', String(body?.runImmediately ?? true));
    if (body?.publishAt) form.append('publishAt', body.publishAt);
    if (body!.imageBlob) {
      form.append(
        'image',
        body!.imageBlob,
        body!.imageFileName || 'cover.jpg',
      );
    } else if (body!.imageBase64) {
      const bin = atob(body!.imageBase64);
      const arr = new Uint8Array(bin.length);
      for (let i = 0; i < bin.length; i++) arr[i] = bin.charCodeAt(i);
      form.append(
        'image',
        new Blob([arr], { type: body!.imageContentType || 'image/jpeg' }),
        body!.imageFileName || 'cover.jpg',
      );
    }
    const { data } = await http.post<ContentWorkEnqueueResult>(`/content/topics/${id}/publish`, form, {
      timeout: 60_000,
      maxBodyLength: Infinity,
      maxContentLength: Infinity,
    });
    const job = await waitForContentWork(data.job.id, { timeoutMs: 180_000 });
    if (job.status === 'Failed' || job.status === 'Cancelled') throw workFailedError(job);
    return { jobs: await fetchContentPublishJobs(id), work: job };
  }

  const { data } = await http.post<ContentWorkEnqueueResult>(`/content/topics/${id}/publish`, body ?? {}, {
    timeout: 30_000,
  });
  const job = await waitForContentWork(data.job.id, { timeoutMs: 180_000 });
  if (job.status === 'Failed' || job.status === 'Cancelled') throw workFailedError(job);
  return { jobs: await fetchContentPublishJobs(id), work: job };
}

export async function runContentPublishJob(
  id: string,
  body?: {
    imageBlob?: Blob;
    imageFileName?: string;
    imageContentType?: string;
    publishAt?: string | null;
  },
) {
  if (body?.imageBlob) {
    const form = new FormData();
    form.append('image', body.imageBlob, body.imageFileName || 'cover.jpg');
    form.append('includeManualExport', 'false');
    form.append('runImmediately', 'true');
    if (body.publishAt) form.append('publishAt', body.publishAt);
    const { data } = await http.post<ContentPublishJob>(`/content/jobs/${id}/run`, form, {
      timeout: 120_000,
      maxBodyLength: Infinity,
      maxContentLength: Infinity,
    });
    return data;
  }
  const { data } = await http.post<ContentPublishJob>(`/content/jobs/${id}/run`, {}, { timeout: 120_000 });
  return data;
}

/** Fetch asset image as blob URL (auth required). Caller should revoke when done. */
export async function fetchContentAssetObjectUrl(assetId: string): Promise<string> {
  const { data } = await http.get<Blob>(`/content/assets/${assetId}/file`, { responseType: 'blob' });
  return URL.createObjectURL(data);
}

export function contentAssetAuthHeaders(): Record<string, string> {
  const token = useAuthStore.getState().accessToken;
  return token ? { Authorization: `Bearer ${token}` } : {};
}
