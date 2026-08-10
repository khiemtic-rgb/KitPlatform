import { http } from '@/shared/api/http';
import { useAuthStore } from '@/shared/auth/auth.store';

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
  monthSpendEstimateUsd: number;
  remainingBudgetUsd: number;
  updatedAt: string;
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
}>) {
  const { data } = await http.put<ContentOrgSettings>('/content/settings', body);
  return data;
}

export async function fetchContentBudget() {
  const { data } = await http.get<ContentBudgetSnapshot>('/content/budget');
  return data;
}

export async function fetchContentBrands(activeOnly = false) {
  const { data } = await http.get<ContentBrand[]>('/content/brands', { params: { activeOnly } });
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
    secretRef?: string;
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
    secretRef?: string;
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
  body?: { skipImages?: boolean; candidateCount?: number },
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
  },
) {
  const { data } = await http.post<{ jobs: ContentPublishJob[] }>(`/content/topics/${id}/publish`, body ?? {}, {
    timeout: 120_000,
  });
  return data;
}

export async function runContentPublishJob(id: string) {
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
