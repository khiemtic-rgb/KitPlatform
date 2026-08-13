import { http } from '@/shared/api/http';
import { useAuthStore } from '@/shared/auth/auth.store';

export type ContentAiConfig = {
  provider: string;
  textModel: string;
  imageModel?: string | null;
  imagesEnabled: boolean;
  geminiApiKeySecretRef?: string | null;
  apiKeyConfigured: boolean;
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
  displayAt?: string | null;
  variantCount: number;
  createdAt: string;
  updatedAt: string;
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
}>) {
  const { data } = await http.put<ContentOrgSettings>('/content/settings', body);
  return data;
}

export async function testContentAi() {
  const { data } = await http.post<ContentAiTestResult>('/content/ai/test');
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

export async function fetchContentPackages(params?: { brandId?: string; status?: string }) {
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
}) {
  const { data } = await http.post<ContentPackage>('/content/packages', body);
  return data;
}

export async function updateContentPackage(id: string, body: Parameters<typeof createContentPackage>[0]) {
  const { data } = await http.put<ContentPackage>(`/content/packages/${id}`, body);
  return data;
}

export async function generateContentPackage(
  id: string,
  body?: { skipImages?: boolean; candidateCount?: number; imagesOnly?: boolean },
) {
  const { data } = await http.post<GenerateContentResult>(`/content/packages/${id}/generate`, body ?? {}, {
    timeout: 300_000,
  });
  return data;
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

export async function renderContentVideoJob(id: string) {
  const { data } = await http.post<ContentVideoJob>(`/content/video/jobs/${id}/render`);
  return data;
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
  const { data } = await http.post<GenerateContentResult>(`/content/topics/${id}/generate`, body ?? {}, {
    timeout: 300_000,
  });
  return data;
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
    const { data } = await http.post<{ jobs: ContentPublishJob[] }>(`/content/topics/${id}/publish`, form, {
      timeout: 180_000,
      maxBodyLength: Infinity,
      maxContentLength: Infinity,
    });
    return data;
  }

  const { data } = await http.post<{ jobs: ContentPublishJob[] }>(`/content/topics/${id}/publish`, body ?? {}, {
    timeout: 180_000,
  });
  return data;
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
