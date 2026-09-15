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
  decisionId?: string | null;
  providerId?: string | null;
  selectionMode?: string | null;
  selectionReason?: string | null;
  estimatedCost?: number | null;
  costKind?: string | null;
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
  confirm?: boolean;
  lastFrameFromUrl?: string;
  /** Per-shot Director cap. Not monthly ContentBudget. */
  maxCost?: number | null;
  currency?: string | null;
}) {
  const { data } = await http.post<ContentSeriesTurboTask>(
    '/content/series/turbo',
    { ...body, confirm: body.confirm === true },
    { timeout: 180_000 },
  );
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
  references: {
    name: string;
    imageDataUrl: string;
    role?: string;
    visualMode?: string;
    referenceStatus?: string;
    authorityStatus?: string;
    characterId?: string;
    referenceRole?: string;
  }[];
}) {
  const { data } = await http.post<{
    imageDataUrl: string;
    model: string;
    aspect: string;
    decisionId?: string | null;
    providerId?: string | null;
  }>(
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
  voices?: { audioBase64: string; startSec: number; mime?: string }[];
  productionDurationSec?: number;
  performanceDurationSec?: number;
  takeTaskId?: string;
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
  mix?: {
    room?: boolean;
    foley?: boolean;
    music?: boolean;
    loudnorm?: boolean;
    roomId?: string;
    musicId?: string;
    sfx?: { assetId: string; startSec: number; gainDb: number }[];
    grade?: boolean;
    colorMatch?: boolean;
    interpolate?: boolean;
  };
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
  previewUrl?: string | null;
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
  sceneCount?: number;
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

export type FamixaCharacterRef = { kind: string; path: string; label?: string | null };

export type FamixaCharacterRow = {
  id: string;
  characterCode: string;
  name: string;
  role: string;
  universe: string;
  visual: string;
  lifecycle: string;
  currentVersionId?: string | null;
  currentEra: string;
  version: string;
  isCurrentCanon: boolean;
  approvedAt?: string | null;
  approvedBy?: string | null;
  canon: Record<string, unknown>;
  references: FamixaCharacterRef[];
  updatedAt: string;
};

export type FamixaCharacterGuardResult = {
  ok: boolean;
  blocked: string[];
  resolved: {
    characterCode: string;
    lifecycle: string;
    era: string;
    version: string;
    wardrobeId?: string | null;
    refIds: string[];
    frontOk: boolean;
  }[];
  compilerVersion: string;
};

export async function fetchFamixaCharacters() {
  const { data } = await http.get<FamixaCharacterRow[]>('/content/series/characters');
  return data ?? [];
}

export async function fetchFamixaCharacter(code: string) {
  const { data } = await http.get<FamixaCharacterRow>(`/content/series/characters/${encodeURIComponent(code)}`);
  return data;
}

export async function approveFamixaCharacter(code: string) {
  const { data } = await http.post<FamixaCharacterRow>(`/content/series/characters/${encodeURIComponent(code)}/approve`);
  return data;
}

export async function lockFamixaCharacter(code: string) {
  const { data } = await http.post<FamixaCharacterRow>(`/content/series/characters/${encodeURIComponent(code)}/lock`);
  return data;
}

export async function unlockFamixaCharacter(code: string) {
  const { data } = await http.post<FamixaCharacterRow>(`/content/series/characters/${encodeURIComponent(code)}/unlock`);
  return data;
}

export async function putFamixaCharacterCanon(code: string, canon: Record<string, unknown>, unlock = false) {
  const { data } = await http.put<FamixaCharacterRow>(`/content/series/characters/${encodeURIComponent(code)}/canon`, {
    canon,
    unlock,
  });
  return data;
}

export async function guardFamixaCharacters(body: {
  characterIds: string[];
  hasDialogue?: Record<string, boolean>;
  voiceIds?: Record<string, string>;
  unknownNames?: string[];
}) {
  const { data } = await http.post<FamixaCharacterGuardResult>('/content/series/characters/guard', body);
  return data;
}

export async function createFamixaCharacter(body: {
  characterCode?: string;
  name: string;
  role?: string;
  gender?: string;
  initialAge?: number;
  universe?: string;
  visual?: string;
  description?: string;
  familyRole?: string;
  allowNew?: boolean;
  forceCreate?: boolean;
}) {
  const { data } = await http.post<FamixaCharacterRow>('/content/series/characters', body);
  return data;
}

export async function detectFamixaCharacterDuplicate(body: { name: string; role?: string; familyRole?: string }) {
  const { data } = await http.post<{
    duplicate: boolean;
    preferred: string;
    message: string;
    existing: { characterCode: string; name: string; role: string; lifecycle: string }[];
  }>('/content/series/characters/detect-duplicate', body);
  return data;
}

export async function createFamixaCharacterVersion(code: string) {
  const { data } = await http.post<FamixaCharacterRow>(
    `/content/series/characters/${encodeURIComponent(code)}/versions`,
  );
  return data;
}

export async function fetchFamixaCharacterVersions(code: string) {
  const { data } = await http.get<
    { id: string; version: string; era: string; status: string; isCurrentCanon: boolean; createdAt: string }[]
  >(`/content/series/characters/${encodeURIComponent(code)}/versions`);
  return data ?? [];
}

export async function fetchFamixaCharacterAudit(code: string) {
  const { data } = await http.get<
    {
      id: string;
      characterCode: string;
      version: string;
      fieldChanged: string;
      oldValue?: string | null;
      newValue?: string | null;
      changedBy?: string | null;
      changedAt: string;
      reason?: string | null;
      approval?: string | null;
    }[]
  >(`/content/series/characters/${encodeURIComponent(code)}/audit`);
  return data ?? [];
}

export type KitVideoProjectRow = {
  id: string;
  projectCode: string;
  name: string;
  brandCode: string;
  status: string;
  visual: Record<string, unknown>;
  rules: Record<string, unknown>;
  universes: { id: string; universeCode: string; name: string; status: string; world: Record<string, unknown> }[];
};

export type KitVideoProductionRow = {
  id: string;
  projectCode: string;
  universeCode: string;
  productionCode: string;
  title: string;
  state: string;
  runStatus?: string;
  seriesBuildId?: string | null;
  shots: {
    shotCode: string;
    state: string;
    failed: boolean;
    lastProvider?: string | null;
    lastFailureCode?: string | null;
  }[];
  updatedAt: string;
};

export type KitVideoJobRow = {
  jobId: string;
  projectId: string;
  productionId: string;
  sceneId: string;
  shotId: string;
  provider: string;
  operation: string;
  status: string;
  idempotencyKey: string;
  confirmed: boolean;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  attempts: {
    id: string;
    attemptNo: number;
    status: string;
    error?: string | null;
    createdAt: string;
    providerTasks: {
      id: string;
      provider: string;
      providerTaskId: string;
      providerStatus: string;
      failureCode?: string | null;
      outputUrl?: string | null;
    }[];
  }[];
};

export async function fetchKitVideoProjects() {
  const { data } = await http.get<KitVideoProjectRow[]>('/content/video-engine/projects');
  return data ?? [];
}

export async function fetchKitVideoProductions(project?: string) {
  const { data } = await http.get<KitVideoProductionRow[]>('/content/video-engine/productions', {
    params: project ? { project } : undefined,
  });
  return data ?? [];
}

export async function ensureKitVideoProduction(body: {
  projectCode: string;
  universeCode: string;
  productionCode: string;
  title?: string;
  seriesBuildId?: string;
}) {
  const { data } = await http.post<KitVideoProductionRow>('/content/video-engine/productions', body);
  return data;
}

export async function transitionKitVideoProduction(id: string, toState: string, reason?: string) {
  const { data } = await http.post<KitVideoProductionRow>(
    `/content/video-engine/productions/${encodeURIComponent(id)}/transition`,
    { toState, reason },
  );
  return data;
}

export async function upsertKitVideoShot(
  productionId: string,
  shotCode: string,
  toState: string,
  failed?: boolean,
  extra?: { lastProvider?: string; lastFailureCode?: string },
) {
  const { data } = await http.put<KitVideoProductionRow>(
    `/content/video-engine/productions/${encodeURIComponent(productionId)}/shots/${encodeURIComponent(shotCode)}`,
    { toState, failed, lastProvider: extra?.lastProvider, lastFailureCode: extra?.lastFailureCode },
  );
  return data;
}

export async function fetchKitVideoJobs(productionId: string) {
  const { data } = await http.get<KitVideoJobRow[]>('/content/video-engine/jobs', {
    params: { productionId },
  });
  return data ?? [];
}

export type KitVideoAssetRow = {
  id: string;
  projectCode: string;
  assetCode: string;
  assetKind: string;
  name: string;
  lifecycle: string;
  version: string;
  era: string;
  canon: Record<string, unknown>;
  references: { kind: string; path: string; isPrimary: boolean; isSecondary: boolean; qaStatus: string }[];
  eras: { era: string; age?: number | null; status: string }[];
};

export async function fetchKitVideoAssets(project?: string) {
  const { data } = await http.get<KitVideoAssetRow[]>('/content/video-engine/assets', {
    params: project ? { project } : undefined,
  });
  return data ?? [];
}

export type KitVideoStoryGraphRow = {
  productionId: string;
  scriptHash: string;
  graph: {
    scenes: {
      sceneId: string;
      sceneOrder: number;
      location: string;
      time: string;
      characters: string[];
      objective: string;
      beats: { beatId: string; order: number; text: string; action: string }[];
    }[];
    shots: {
      shotId: string;
      displayCode: string;
      displayOrder: number;
      action: string;
      purpose: string;
      estimatedDuration: number;
      characters: string[];
      dialogueSegmentIds: string[];
      requiredProps: string[];
      status: string;
      continuity: string;
      continuityNotes: string[];
    }[];
    dialogues: { segmentId: string; text: string; status: string; shotId?: string | null }[];
    unassigned: string[];
  };
  unassigned: string[];
  overrides: { shotId: string; reason: string; approvedBy: string; createdAt: string }[];
};

export async function fetchKitVideoStory(productionId: string) {
  const { data } = await http.get<KitVideoStoryGraphRow>('/content/video-engine/story', {
    params: { productionId },
  });
  return data;
}

export async function compileKitVideoStory(productionId: string, script: string) {
  const { data } = await http.post<KitVideoStoryGraphRow>('/content/video-engine/story/compile', {
    productionId,
    script,
  });
  return data;
}

export async function gateKitVideoStory(shot: unknown, detectedCharacters?: string[]) {
  const { data } = await http.post<{ ok: boolean; allowKf: boolean; reasons: string[] }>(
    '/content/video-engine/story/gate',
    { shot, detectedCharacters },
  );
  return data;
}

export async function compileKitVideoVisual(contract: unknown, projectStyle?: string) {
  const { data } = await http.post<{ prompt: string; fingerprint: string; hasDialogue: boolean; i2vImageSource: string }>(
    '/content/video-engine/visual/compile',
    { contract, projectStyle },
  );
  return data;
}

export async function evaluateKitVideoVision(contract: unknown, observation: unknown) {
  const { data } = await http.post<{
    status: string;
    scores: Record<string, number>;
    p0Fail: string[];
    allowI2v: boolean;
    canBeReference: boolean;
  }>('/content/video-engine/visual/qa', { contract, observation });
  return data;
}

export type KitVideoProviderStatus = {
  imageProvider: string;
  imageModel: string;
  apiKeyConfigured: boolean;
  visionProvider: string;
  generatorProvider: string;
  runwayCalled: boolean;
};

export type KitVideoPixelGenerateRow = {
  attemptId: string;
  productionId: string;
  shotCode: string;
  attemptNo: number;
  status: string;
  jobState: string;
  fingerprint: string;
  qa?: {
    status: string;
    scores: Record<string, number>;
    p0Fail: string[];
    warnings?: string[];
    allowI2v: boolean;
    canBeReference: boolean;
    artifactHash?: string;
    imageType?: string;
  } | null;
  repair?: { repairClass: string; repair: string } | null;
  i2v?: {
    shotCode: string;
    attemptId: string;
    source: string;
    ready: boolean;
    runwaySubmitted: boolean;
    blocked: string[];
    sourceArtifactHash?: string;
    imageType?: string;
  } | null;
  imagePath?: string | null;
  provider: string;
  model: string;
  costUnknown: boolean;
  failureClass: string;
  visionJson?: string | null;
  runwayCalled: boolean;
  artifactHash?: string;
  imageType?: string;
  persistStatus?: string;
};

export async function fetchKitVideoVisualProvider() {
  const { data } = await http.get<KitVideoProviderStatus>('/content/video-engine/visual/provider');
  return data;
}

export async function generateKitVideoKeyframe(body: {
  productionId: string;
  shotCode: string;
  contract: unknown;
  confirmed: boolean;
  idempotencyKey: string;
  strategyChanged?: boolean;
  projectStyle?: string;
}) {
  const { data } = await http.post<KitVideoPixelGenerateRow>('/content/video-engine/visual/generate', body, {
    timeout: 180_000,
  });
  return data;
}

export async function analyzeKitVideoKeyframe(body: {
  productionId: string;
  shotCode: string;
  contract: unknown;
  imageBase64: string;
}) {
  const { data } = await http.post<KitVideoPixelGenerateRow>('/content/video-engine/visual/analyze', body, {
    timeout: 180_000,
  });
  return data;
}

export async function decideKitVideoKeyframe(body: {
  productionId: string;
  shotCode: string;
  attemptId: string;
  decision: 'APPROVE' | 'REJECT';
}) {
  const { data } = await http.post<KitVideoPixelGenerateRow>('/content/video-engine/visual/decide', body);
  return data;
}

export async function revalidateKitVideoKeyframe(attemptId: string, contract: unknown) {
  const { data } = await http.post<KitVideoPixelGenerateRow>('/content/video-engine/visual/revalidate', {
    attemptId,
    contract,
  }, { timeout: 180_000 });
  return data;
}

export async function fetchKitVideoI2vReady(attemptId: string) {
  const { data } = await http.get<{
    ready: boolean;
    runwaySubmitted: boolean;
    blocked: string[];
    sourceArtifactHash?: string;
    imageType?: string;
  }>('/content/video-engine/visual/i2v-ready', { params: { attemptId } });
  return data;
}

export type KitVideoVisualSystemRow = {
  id: string;
  projectId: string;
  projectCode: string;
  systemCode: string;
  version: string;
  status: string;
  rules: Record<string, unknown>;
  lockedAt?: string | null;
  lockedBy?: string | null;
};

export async function fetchKitVideoVisualSystem(projectCode = 'FAMIXA') {
  const { data } = await http.get<KitVideoVisualSystemRow>('/content/video-engine/visual-system', {
    params: { projectCode },
  });
  return data;
}

export async function lockKitVideoVisualSystem(id: string) {
  const { data } = await http.post<KitVideoVisualSystemRow>(`/content/video-engine/visual-system/${id}/lock`);
  return data;
}

export type ProjectVisualStyleRow = {
  documentId: string;
  id?: string | null;
  projectId: string;
  styleKey?: string | null;
  styleName?: string | null;
  description?: string | null;
  renderingStyle?: string | null;
  version: string;
  status: string;
  sha?: string | null;
  authority: string;
  preview: string[];
  prompt?: string | null;
  canonical?: string | null;
  ready: boolean;
  gateCode?: string | null;
  staffMessage: string;
  presets: { styleKey: string; styleName: string; description: string; preview: string[] }[];
  revision?: ProjectVisualStyleRevisionCard | null;
};

export type ProjectVisualStyleRevisionCard = {
  status: string;
  version: string;
  styleName?: string | null;
  styleIntent?: string | null;
  stylePromptBlock?: string | null;
  negativeStyleBlock?: string | null;
  humanRealismBoundary?: string | null;
  faceStyle?: string | null;
  eyeStyle?: string | null;
  skinStyle?: string | null;
  hairStyle?: string | null;
  bodyStyle?: string | null;
  lightingStyle?: string | null;
  materialStyle?: string | null;
  backgroundStyle?: string | null;
  cameraStyle?: string | null;
  candidateSha?: string | null;
  currentSha?: string | null;
  currentVersion?: string | null;
  mayRequest?: boolean;
  mayApprove?: boolean;
  mayReject?: boolean;
  mayLock?: boolean;
  currentIsAuthority?: boolean;
  candidateIsAuthority?: boolean;
  fingerprint?: string | null;
  gateCode?: string | null;
  canonicalStyleDescription?: string | null;
  stylizationLevel?: string | null;
  photorealismLevel?: string | null;
  characterReadability?: string | null;
  clothingStyle?: string | null;
  environmentStyle?: string | null;
  characterDesignLanguage?: string | null;
};

export type ProjectVisualStyleV2Definition = {
  documentId: string;
  version: string;
  styleName: string;
  displayName: string;
  styleIntent: string;
  canonicalStyleDescription: string;
  stylizationLevel: string;
  photorealismLevel: string;
  characterReadability: string;
  sha: string;
  status: string;
  canonical: string;
  prompt: string;
  negativeStyleBlock: string;
  currentAuthorityVersion?: string | null;
  currentAuthoritySha?: string | null;
  currentIsAuthority: boolean;
  candidateIsAuthority: boolean;
  providerCalled: boolean;
  generationExecuted: boolean;
  revision?: ProjectVisualStyleRevisionCard | null;
};

export type ProjectVisualStyleImpact = {
  currentVersion?: string | null;
  currentSha?: string | null;
  candidateVersion?: string | null;
  candidateSha?: string | null;
  candidateStatus?: string | null;
  lockedCount: number;
  wouldStaleCount: number;
  characters: {
    characterId: string;
    characterName?: string | null;
    officialLocked: boolean;
    mutationForbidden: boolean;
    masterSha?: string | null;
    dnaSha?: string | null;
    prpSha?: string | null;
    crpSha?: string | null;
    masterStale: boolean;
    dnaStale: boolean;
    prpStale: boolean;
    crpStale: boolean;
  }[];
};

export type ProjectVisualStyleRevisionResult = {
  documentId: string;
  projectId: string;
  status: string;
  gateCode?: string | null;
  staffMessage: string;
  confirmRequired: boolean;
  providerCalled: boolean;
  generationExecuted: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
  currentSha?: string | null;
  candidateSha?: string | null;
  currentIsAuthority: boolean;
  candidateIsAuthority: boolean;
  style?: ProjectVisualStyleRow | null;
  revision?: ProjectVisualStyleRevisionCard | null;
  impact?: ProjectVisualStyleImpact | null;
};

export async function fetchProjectVisualStyle(projectId = 'FAMIXA') {
  const { data } = await http.get<ProjectVisualStyleRow>('/content/project-visual-style', { params: { projectId } });
  return data;
}

export async function initializeProjectVisualStyle(body: {
  projectId?: string;
  presetKey: string;
  confirm: boolean;
  activate?: boolean;
}) {
  const { data } = await http.post<ProjectVisualStyleRow>('/content/project-visual-style/initialize', body);
  return data;
}

export async function fetchProjectVisualStyleRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
  }>('/content/project-visual-style/regression');
  return data;
}

export async function requestProjectVisualStyleRevision(
  projectId: string,
  body: { confirm: boolean; notes?: string },
) {
  const { data } = await http.post<ProjectVisualStyleRevisionResult>(
    `/content/project-visual-style/${encodeURIComponent(projectId)}/revision`,
    body,
  );
  return data;
}

export async function approveProjectVisualStyleRevision(projectId: string, notes?: string) {
  const { data } = await http.post<ProjectVisualStyleRevisionResult>(
    `/content/project-visual-style/${encodeURIComponent(projectId)}/revision/approve`,
    { confirm: true, notes },
  );
  return data;
}

export async function rejectProjectVisualStyleRevision(
  projectId: string,
  rejectionReason: string,
) {
  const { data } = await http.post<ProjectVisualStyleRevisionResult>(
    `/content/project-visual-style/${encodeURIComponent(projectId)}/revision/reject`,
    { confirm: true, rejectionReason },
  );
  return data;
}

export async function lockProjectVisualStyleRevision(projectId: string, notes?: string) {
  const { data } = await http.post<ProjectVisualStyleRevisionResult>(
    `/content/project-visual-style/${encodeURIComponent(projectId)}/revision/lock`,
    { confirm: true, notes },
  );
  return data;
}

export async function fetchProjectVisualStyleRevisionImpact(projectId = 'FAMIXA') {
  const { data } = await http.get<ProjectVisualStyleImpact>(
    `/content/project-visual-style/${encodeURIComponent(projectId)}/revision/impact`,
  );
  return data;
}

export async function fetchProjectVisualStyleV2(projectId = 'FAMIXA') {
  const { data } = await http.get<ProjectVisualStyleV2Definition>(
    '/content/character-studio/project-visual-style/v2',
    { params: { projectId } },
  );
  return data;
}

export type CharacterDesignLanguageDefinition = {
  documentId: string;
  version: string;
  name: string;
  status: string;
  corePrinciple: string;
  faceLanguage: string;
  eyeLanguage: string;
  skinLanguage: string;
  hairLanguage: string;
  bodyLanguage: string;
  expressionLanguage: string;
  genderLanguage: string;
  ageAdaptiveStylization: boolean;
  characterSpecificBranching: boolean;
  positivePromptBlock: string;
  negativePromptBlock: string;
  stylizationLevel: string;
  photorealismLevel: string;
  sha: string;
  canonical: string;
  providerCalled: boolean;
  generationExecuted: boolean;
  geminiCalled: boolean;
  autoApprove: boolean;
  autoLock: boolean;
  persisted: boolean;
};

export type CharacterDesignLanguageCompile = {
  documentId: string;
  definitionSha: string;
  chronologicalAge: number;
  targetAppearanceAgeMin: number;
  targetAppearanceAgeMax: number;
  lifeStage: string;
  stylizationBand: string;
  ageAdaptiveBlock: string;
  positivePromptBlock: string;
  negativePromptBlock: string;
  combinedWithAgeAppearance: string;
  providerCalled: boolean;
  generationExecuted: boolean;
  geminiCalled: boolean;
  persisted: boolean;
};

export async function fetchCharacterDesignLanguage() {
  const { data } = await http.get<CharacterDesignLanguageDefinition>(
    '/content/character-studio/character-design-language',
  );
  return data;
}

export async function previewCharacterDesignLanguage(body: {
  chronologicalAge: number;
  gender?: string;
  ageAppearanceProfile?: string;
}) {
  const { data } = await http.post<CharacterDesignLanguageCompile>(
    '/content/character-studio/character-design-language/compile-preview',
    body,
  );
  return data;
}

export async function fetchCharacterDesignLanguageRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
    geminiCalled: boolean;
    generationExecuted: boolean;
  }>('/content/character-studio/character-design-language/regression');
  return data;
}

export type CharacterDesignLanguageV2Definition = {
  documentId: string;
  version: string;
  name: string;
  status: string;
  currentAuthority: boolean;
  sha256: string;
  stylizationLevel: string;
  stylizationTarget: string;
  stylizationScale: number;
  photorealismCeiling: string;
  cartoonFloor: string;
  faceLanguage: string;
  eyeLanguage: string;
  noseMouthLanguage: string;
  hairLanguage: string;
  bodyLanguage: string;
  handFootLanguage: string;
  skinLanguage: string;
  clothingLanguage: string;
  lightingLanguage: string;
  cameraLanguage: string;
  realismCeilingLanguage: string;
  cartoonFloorLanguage: string;
  crossCharacterInvariants: string;
  promptBlock: string;
  negativePromptBlock: string;
  canonical: string;
  providerCalled: boolean;
  generationExecuted: boolean;
  geminiCalled: boolean;
  autoApprove: boolean;
  autoLock: boolean;
  persisted: boolean;
};

export async function fetchCharacterDesignLanguageV2() {
  const { data } = await http.get<CharacterDesignLanguageV2Definition>(
    '/content/character-studio/character-design-language/v2',
  );
  return data;
}

export async function fetchCharacterDesignLanguageV2Regression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
    geminiCalled: boolean;
    generationExecuted: boolean;
    sha256: string;
    currentAuthority: boolean;
  }>('/content/character-studio/character-design-language/v2/regression');
  return data;
}

export type VisualUniverseAuthority = {
  documentId: string;
  authorityId: string;
  version: string;
  projectId: string;
  styleId: string;
  styleName: string;
  status: string;
  currentAuthority: boolean;
  sha256: string;
  characterDesignLanguageSha: string;
  styleReferencePackSha: string;
  styleCalibrationPackSha: string;
  projectVisualStyleSha: string;
  stylizationLevel: string;
  realismCeiling: string;
  photorealismCeiling: string;
  styleIntent: string;
  faceLanguage: string;
  eyeLanguage: string;
  hairLanguage: string;
  bodyLanguage: string;
  materialLanguage: string;
  lightingLanguage: string;
  positiveConstraints: string;
  negativeConstraints: string;
  mayRequest: boolean;
  mayCalibrate: boolean;
  mayApprove: boolean;
  mayReject: boolean;
  mayLock: boolean;
  gateCode?: string | null;
  staffMessage?: string | null;
  calibration?: VisualUniverseCalibrationSlot[];
  providerCalled: boolean;
  geminiCalled: boolean;
};

export type VisualUniverseCalibrationSlot = {
  code: string;
  label: string;
  chronologicalAge: number;
  targetAppearanceAgeMin: number;
  targetAppearanceAgeMax: number;
  view: string;
  path?: string | null;
  sha256?: string | null;
  prompt: string;
};

export type VisualUniverseCalibration = {
  documentId: string;
  sha256: string;
  status: string;
  slots: VisualUniverseCalibrationSlot[];
  providerCalled: boolean;
  geminiCalled: boolean;
  generationExecuted: boolean;
};

export async function fetchVisualUniverseAuthority(projectId = 'FAMIXA') {
  const { data } = await http.get<VisualUniverseAuthority>(
    '/content/character-studio/project-visual-universe',
    { params: { projectId } },
  );
  return data;
}

export async function requestVisualUniverseRevision(body: { confirm: boolean; notes?: string; projectId?: string }) {
  const { data } = await http.post<VisualUniverseAuthority>(
    '/content/character-studio/project-visual-universe/revision',
    body,
  );
  return data;
}

export async function fetchVisualUniverseCalibration(projectId = 'FAMIXA') {
  const { data } = await http.get<VisualUniverseCalibration>(
    '/content/character-studio/project-visual-universe/calibration',
    { params: { projectId } },
  );
  return data;
}

export async function requestVisualUniverseCalibration(body: {
  confirm: boolean;
  generate?: boolean;
  projectId?: string;
}) {
  const { data } = await http.post<VisualUniverseCalibration>(
    '/content/character-studio/project-visual-universe/calibration',
    body,
  );
  return data;
}

export async function approveVisualUniverse(body: { confirm?: boolean; projectId?: string } = {}) {
  const { data } = await http.post<VisualUniverseAuthority>(
    '/content/character-studio/project-visual-universe/approve',
    body,
  );
  return data;
}

export async function rejectVisualUniverse(body: { confirm?: boolean; rejectionReason?: string; projectId?: string } = {}) {
  const { data } = await http.post<VisualUniverseAuthority>(
    '/content/character-studio/project-visual-universe/reject',
    body,
  );
  return data;
}

export async function lockVisualUniverse(body: { confirm?: boolean; projectId?: string } = {}) {
  const { data } = await http.post<VisualUniverseAuthority>(
    '/content/character-studio/project-visual-universe/lock',
    body,
  );
  return data;
}

export async function fetchVisualUniverseAuthorityRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    sha256: string;
    geminiCalled: boolean;
  }>('/content/character-studio/project-visual-universe/regression');
  return data;
}

export type VisualCalibrationCoverage = {
  required: number;
  generated: number;
  valid: number;
  missing: number;
};

export type VisualCalibrationPack = {
  documentId: string;
  packId: string;
  version: string;
  status: string;
  sha256: string;
  visualUniverseSha: string;
  projectVisualStyleSha: string;
  characterDesignLanguageSha: string;
  currentAuthority: boolean;
  directorPass?: boolean | null;
  photorealismLevel: string;
  rejectionReason?: string | null;
  subjects: VisualCalibrationSubject[];
  impact: { charactersAffected: number; lockedCharacters: number; potentiallyStale: number };
  gateCode?: string | null;
  providerCalled: boolean;
  generationExecuted: boolean;
  geminiCalled: boolean;
  autoApprove: boolean;
  autoLock: boolean;
  staffMessage?: string | null;
  coverage?: VisualCalibrationCoverage | null;
  authorityTransitioned?: boolean;
  lockedAt?: string | null;
  lockedBy?: string | null;
  generationExecutionId?: string | null;
  calibrationRunId?: string | null;
};

export type VisualCalibrationArtifact = {
  subjectId: string;
  viewType: string;
  path?: string | null;
  sha256?: string | null;
  kind?: string | null;
  slotId?: string | null;
  imageUrl?: string | null;
  generationStatus?: string | null;
  generatedAt?: string | null;
};

export type VisualCalibrationSubject = {
  calibrationSubjectId: string;
  label: string;
  gender: string;
  chronologicalAge: number;
  artifacts: VisualCalibrationArtifact[];
};

export async function fetchVisualCalibrationPack(packId = 'FAMIXA-VISUAL-CALIBRATION-V1') {
  const { data } = await http.get<VisualCalibrationPack>(`/content/visual-calibration/${packId}`);
  return data;
}

export async function createVisualCalibrationPack(body: { confirm: boolean; packId?: string } = { confirm: false }) {
  const { data } = await http.post<VisualCalibrationPack>('/content/visual-calibration/packs', body);
  return data;
}

export async function generateVisualCalibrationPack(
  packId: string,
  body: { confirm: boolean; generate?: boolean } = { confirm: false },
) {
  const { data } = await http.post<VisualCalibrationPack>(
    `/content/visual-calibration/${packId}/generate`,
    body,
    { timeout: 900_000 },
  );
  return data;
}

export async function approveVisualCalibrationPack(
  packId: string,
  body: { confirm: boolean; directorPass?: boolean } = { confirm: false },
) {
  const { data } = await http.post<VisualCalibrationPack>(`/content/visual-calibration/${packId}/approve`, body);
  return data;
}

export async function rejectVisualCalibrationPack(
  packId: string,
  body: { confirm: boolean; rejectionReason?: string } = { confirm: false },
) {
  const { data } = await http.post<VisualCalibrationPack>(`/content/visual-calibration/${packId}/reject`, body);
  return data;
}

export async function lockVisualCalibrationPack(packId: string, body: { confirm: boolean } = { confirm: false }) {
  const { data } = await http.post<VisualCalibrationPack>(`/content/visual-calibration/${packId}/lock`, body);
  return data;
}

export async function fetchVisualCalibrationCoverage(packId = 'FAMIXA-VISUAL-CALIBRATION-V1') {
  const { data } = await http.get<VisualCalibrationCoverage>(`/content/visual-calibration/${packId}/coverage`);
  return data;
}

export async function fetchVisualCalibrationHardeningRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    providerCalled: boolean;
    generationExecuted: boolean;
  }>('/content/visual-calibration/hardening/regression');
  return data;
}

export async function fetchVisualCalibrationRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    providerCalled: boolean;
    generationExecuted: boolean;
  }>('/content/visual-calibration/regression');
  return data;
}

export async function fetchVisualCalibrationLiveGenerationReadinessRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    providerCalled: boolean;
    generationExecuted: boolean;
    geminiCalled: boolean;
  }>('/content/visual-calibration/live-generation-readiness/regression');
  return data;
}

export async function fetchVisualCalibrationLiveGenerationRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    providerCalled: boolean;
    generationExecuted: boolean;
    geminiCalled: boolean;
  }>('/content/visual-calibration/live-generation/regression');
  return data;
}

export type IdentityConditionedCalibrationVisualGates = {
  identityGate: string;
  ageGate: string;
  appearanceGate: string;
  faceConsistencyGate: string;
  viewConsistencyGate: string;
  wardrobeConsistencyGate: string;
  visualUniverseGate: string;
  stylizationGate: string;
  crossCharacterGate: string;
  photorealismGate: string;
};

export type IdentityConditionedCalibrationDirectorReview = {
  reviewId: string;
  calibrationRunId: string;
  packId: string;
  subjectId: string;
  subjectType: string;
  view: string;
  artifactId?: string | null;
  artifactPath?: string | null;
  artifactSha256?: string | null;
  referenceRole?: string | null;
  referenceArtifactId?: string | null;
  referenceArtifactSha256?: string | null;
  artifactIntegrity: string;
  identityAnchorValid: string;
  referenceBindingValid: string;
  snapshotBindingValid: string;
  authorityBindingValid: string;
  compilerBindingValid: string;
  technicalStatus: string;
  identityGate: string;
  ageGate: string;
  appearanceGate: string;
  faceConsistencyGate: string;
  viewConsistencyGate: string;
  wardrobeConsistencyGate: string;
  visualUniverseGate: string;
  stylizationGate: string;
  crossCharacterGate: string;
  photorealismGate: string;
  decision: string;
  directorNote?: string | null;
  failureReason?: string | null;
  isIdentityAnchor: boolean;
  isIdentityConditioned: boolean;
};

export type IdentityConditionedCalibrationSubjectReview = {
  subjectId: string;
  label: string;
  subjectType: string;
  chronologicalAge: number;
  targetAppearanceAgeMin: number;
  targetAppearanceAgeMax: number;
  technicalStatus: string;
  subjectReviewStatus: string;
  views: IdentityConditionedCalibrationDirectorReview[];
  gates: IdentityConditionedCalibrationVisualGates;
  decision: string;
  directorNote?: string | null;
  failureReason?: string | null;
};

export type IdentityConditionedCalibrationPackReview = {
  documentId: string;
  status: string;
  calibrationRunId?: string | null;
  packId: string;
  subjectsFound: number;
  subjectsExpected: number;
  viewsFound: number;
  viewsExpected: number;
  frontAnchorsValid: number;
  frontAnchorsExpected: number;
  identityConditionedValid: number;
  identityConditionedExpected: number;
  technicalIntegrityValid: number;
  technicalIntegrityExpected: number;
  directorReviewCount: number;
  directorReviewExpected: number;
  subjectPass: number;
  subjectExpected: number;
  fail: number;
  reviewRequired: number;
  visualPass: boolean;
  packDecision: string;
  gateCode?: string | null;
  geminiCalled: boolean;
  generationExecuted: boolean;
  providerCalled: boolean;
  pixelArtifactCreated: number;
  videoArtifactCreated: number;
  identityConditionedCalibrationArchitecture: boolean;
  identityConditionedCalibrationLive: boolean;
  directorReviewInfrastructure: boolean;
  identityConditionedCalibrationVisualPass: boolean;
  foundationComplete: boolean;
  subjects: IdentityConditionedCalibrationSubjectReview[];
  historical: { subjectId: string; view: string; path?: string | null; sha256?: string | null; label: string }[];
};

export async function fetchIdentityConditionedCalibrationDirectorReview(runId?: string) {
  const path = runId
    ? `/content/identity-conditioned-calibration/director-review/${encodeURIComponent(runId)}`
    : '/content/identity-conditioned-calibration/director-review';
  const { data } = await http.get<IdentityConditionedCalibrationPackReview>(path);
  return data;
}

export async function saveIdentityConditionedCalibrationDirectorReview(
  runId: string,
  subjectId: string,
  body: {
    identityGate?: string;
    ageGate?: string;
    appearanceGate?: string;
    faceConsistencyGate?: string;
    viewConsistencyGate?: string;
    wardrobeConsistencyGate?: string;
    visualUniverseGate?: string;
    stylizationGate?: string;
    crossCharacterGate?: string;
    photorealismGate?: string;
    decision?: string;
    directorNote?: string;
    failureReason?: string;
    markVisualPass?: boolean;
  },
) {
  const { data } = await http.post<IdentityConditionedCalibrationPackReview>(
    `/content/identity-conditioned-calibration/director-review/${encodeURIComponent(runId)}/subject/${encodeURIComponent(subjectId)}`,
    body,
  );
  return data;
}

export async function markIdentityConditionedCalibrationVisualPass(runId: string) {
  const { data } = await http.post<IdentityConditionedCalibrationPackReview>(
    `/content/identity-conditioned-calibration/director-review/${encodeURIComponent(runId)}/visual-pass`,
    { confirm: true },
  );
  return data;
}

export async function fetchProjectVisualStyleV2Regression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
    providerCalled: boolean;
    generationExecuted: boolean;
    geminiCalled: boolean;
  }>('/content/character-studio/project-visual-style/v2/regression');
  return data;
}

export type KitVideoMasterCandidateRow = {
  id: string;
  candidateCode: string;
  role: string;
  generationAttempt: number;
  artifactPath: string;
  sha256: string;
  visualStyleVersion: string;
  characterId: string;
  eraId: string;
  imageType: string;
  status: string;
  qaStatus: string;
  qa: Record<string, unknown>;
  fingerprint?: string;
  diagnosis?: string;
  canonEligible?: boolean;
  score?: number;
  recommendation?: string;
  lifecycle?: string;
  eligible?: boolean;
  frontRunner?: boolean;
  parentCandidateId?: string | null;
  sourceCandidateId?: string | null;
};

export type KitVideoMasterGenerateRow = {
  package: KitVideoMasterReferenceRow;
  candidateId?: string | null;
  candidateCode: string;
  artifactValid: boolean;
  qaStatus: string;
  provider: string;
  live: boolean;
  canonEligible: boolean;
  blocked?: string | null;
  sha256?: string | null;
};

export type KitVideoMasterReferenceRow = {
  assetId: string;
  versionId: string;
  projectCode: string;
  assetCode: string;
  assetLifecycle: string;
  documentId: string;
  status: string;
  era: string;
  age: number;
  version: string;
  generateEnabled: boolean;
  candidates: KitVideoMasterCandidateRow[];
  spec: Record<string, unknown>;
  recommendedCandidateId?: string | null;
  autoSelected?: boolean;
};

export type KitVideoMasterCompareRow = {
  package: KitVideoMasterReferenceRow;
  ranks: {
    id: string;
    candidateCode: string;
    eligible: boolean;
    lifecycle: string;
    score: number;
    recommendation: string;
    p0: string[];
  }[];
  recommendedCandidateId?: string | null;
  autoSelected: boolean;
  visualDnaVersion: string;
};

export async function fetchKitVideoMasterReference(projectCode = 'FAMIXA', assetCode = 'CHAR-001') {
  const { data } = await http.get<KitVideoMasterReferenceRow>('/content/video-engine/master-reference', {
    params: { projectCode, assetCode },
  });
  return data;
}

export async function compileKitVideoMasterReference(role = 'FRONT', dnaApproved = false, styleReady = false) {
  const { data } = await http.post<{ ok: boolean; prompt: string; provider: string; blocked?: string | null }>(
    '/content/video-engine/master-reference/compile',
    null,
    { params: { role, dnaApproved, styleReady } },
  );
  return data;
}

export async function generateKitVideoMasterCandidate(body: {
  projectCode?: string;
  assetCode?: string;
  view?: string;
  expression?: string;
  confirmed: boolean;
  controlledTest?: boolean;
  diagnosis?: string;
  parentCandidateId?: string;
}) {
  const { data } = await http.post<KitVideoMasterGenerateRow>('/content/video-engine/master-reference/generate', {
    projectCode: body.projectCode || 'FAMIXA',
    assetCode: body.assetCode || 'CHAR-001',
    view: body.view || 'FRONT',
    expression: body.expression || 'NEUTRAL',
    confirmed: body.confirmed,
    controlledTest: body.controlledTest === true,
    diagnosis: body.diagnosis || null,
    parentCandidateId: body.parentCandidateId || null,
  }, { timeout: 180_000 });
  return data;
}

export async function analyzeKitVideoMasterCandidate(id: string) {
  const { data } = await http.post<KitVideoMasterReferenceRow>(
    `/content/video-engine/master-reference/candidates/${encodeURIComponent(id)}/analyze`,
    null,
    { timeout: 120_000 },
  );
  return data;
}

export async function approveKitVideoMasterReference(input: {
  projectCode?: string;
  assetCode?: string;
  candidateId?: string;
  decision: 'APPROVE' | 'REJECT' | 'REQUEST_REVISION';
}) {
  const { data } = await http.post<KitVideoMasterReferenceRow>('/content/video-engine/master-reference/approve', null, {
    params: {
      projectCode: input.projectCode || 'FAMIXA',
      assetCode: input.assetCode || 'CHAR-001',
      candidateId: input.candidateId,
      decision: input.decision,
    },
  });
  return data;
}

export async function lockKitVideoMasterReference(projectCode = 'FAMIXA', assetCode = 'CHAR-001') {
  const { data } = await http.post<KitVideoMasterReferenceRow>('/content/video-engine/master-reference/lock', null, {
    params: { projectCode, assetCode },
  });
  return data;
}

export async function compareKitVideoMasterCandidates() {
  const { data } = await http.post<KitVideoMasterCompareRow>('/content/video-engine/master-reference/compare');
  return data;
}

export async function selectKitVideoMasterCandidate(candidateId: string) {
  const { data } = await http.post<KitVideoMasterReferenceRow>('/content/video-engine/master-reference/select', null, {
    params: { candidateId },
  });
  return data;
}

export async function markKitVideoMasterFrontRunner(candidateId: string) {
  const { data } = await http.post<KitVideoMasterReferenceRow>(
    `/content/video-engine/master-reference/candidates/${encodeURIComponent(candidateId)}/front-runner`,
  );
  return data;
}

export async function resolveKitVideoMasterReference() {
  const { data } = await http.get<{ ok: boolean; blocked?: string | null; status: string; sha256?: string | null }>(
    '/content/video-engine/master-reference/resolve',
  );
  return data;
}

export async function fetchKitVideoMasterEvents() {
  const { data } = await http.get<{ eventType: string; actor?: string; sha256?: string; createdAt: string }[]>(
    '/content/video-engine/master-reference/events',
  );
  return data;
}

export async function fetchKitVideoMasterCandidateObjectUrl(id: string) {
  const { data } = await http.get<Blob>(
    `/content/video-engine/master-reference/candidates/${encodeURIComponent(id)}/image`,
    { responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export type KitVideoIdentityTestArtifactRow = {
  id: string;
  testId: string;
  testType: string;
  testVariant: string;
  attempt: number;
  artifactPath: string;
  sha256: string;
  fingerprint: string;
  imageType: string;
  qaStatus: string;
  qa: Record<string, unknown>;
  provider: string;
  model: string;
};

export type KitVideoIdentityTestRow = {
  id: string;
  projectCode: string;
  characterId: string;
  eraId: string;
  candidateId: string;
  candidateCode: string;
  sourceSha256: string;
  status: string;
  documentId: string;
  have: number;
  required: number;
  complete: boolean;
  autoSelected: boolean;
  canon: boolean;
  artifacts: KitVideoIdentityTestArtifactRow[];
  viewStability?: string | null;
  emotionStability?: string | null;
  identityStability?: string | null;
  directorDecision?: string | null;
  blocked?: string | null;
};

export async function listKitVideoIdentityTests(candidateId?: string) {
  const { data } = await http.get<KitVideoIdentityTestRow[]>('/content/video-engine/identity-tests', {
    params: candidateId ? { candidateId } : undefined,
  });
  return data;
}

export async function createKitVideoIdentityTest(candidateId: string, designated = false) {
  const { data } = await http.post<KitVideoIdentityTestRow>('/content/video-engine/identity-tests', {
    candidateId,
    designated,
    projectCode: 'FAMIXA',
    characterId: 'CHAR-001',
    eraId: 'ERA-01',
  });
  return data;
}

export async function runKitVideoIdentityTest(id: string, variant?: string, regenerate = false) {
  const { data } = await http.post<KitVideoIdentityTestRow>(
    `/content/video-engine/identity-tests/${encodeURIComponent(id)}/run`,
    { variant: variant || null, confirmed: true, regenerate },
    { timeout: 180_000 },
  );
  return data;
}

export async function analyzeKitVideoIdentityTest(id: string) {
  const { data } = await http.post<KitVideoIdentityTestRow>(
    `/content/video-engine/identity-tests/${encodeURIComponent(id)}/analyze`,
    null,
    { timeout: 120_000 },
  );
  return data;
}

export async function compareKitVideoIdentityTest(id: string) {
  const { data } = await http.post<KitVideoIdentityTestRow>(`/content/video-engine/identity-tests/${encodeURIComponent(id)}/compare`);
  return data;
}

export async function decideKitVideoIdentityTest(id: string, decision: string, reason?: string) {
  const { data } = await http.post<KitVideoIdentityTestRow>(
    `/content/video-engine/identity-tests/${encodeURIComponent(id)}/director-decision`,
    { decision, reason: reason || '' },
  );
  return data;
}

export async function fetchKitVideoIdentityArtifactObjectUrl(artifactId: string) {
  const { data } = await http.get<Blob>(
    `/content/video-engine/identity-tests/artifacts/${encodeURIComponent(artifactId)}/image`,
    { responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export type KitVideoIdentityStressArtifactRow = {
  id: string;
  testId: string;
  testGroup: string;
  testCase: string;
  attempt: number;
  artifactPath: string;
  sha256: string;
  fingerprint: string;
  imageType: string;
  qaStatus: string;
  qa: Record<string, unknown>;
  provider: string;
  model: string;
};

export type KitVideoIdentityStressRow = {
  id: string;
  projectCode: string;
  characterId: string;
  eraId: string;
  candidateId: string;
  candidateCode: string;
  sourceSha256: string;
  status: string;
  documentId: string;
  have: number;
  required: number;
  complete: boolean;
  p0: number;
  p1: number;
  p2: number;
  autoSelected: boolean;
  canon: boolean;
  artifacts: KitVideoIdentityStressArtifactRow[];
  environment?: string | null;
  lighting?: string | null;
  camera?: string | null;
  emotion?: string | null;
  wardrobe?: string | null;
  identity?: string | null;
  age?: string | null;
  hair?: string | null;
  face?: string | null;
  directorDecision?: string | null;
  blocked?: string | null;
  identityScore?: number;
  facialStructureScore?: number;
  hairScore?: number;
  eyeScore?: number;
  ageConsistencyScore?: number;
  styleConsistencyScore?: number;
  sceneComplianceScore?: number;
  allowMasterReview?: boolean;
};

export async function listKitVideoIdentityStress(candidateId?: string) {
  const { data } = await http.get<KitVideoIdentityStressRow[]>('/content/video-engine/identity/stress-test', {
    params: candidateId ? { candidateId } : undefined,
  });
  return data;
}

export async function createKitVideoIdentityStress(candidateId: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>('/content/video-engine/identity/stress-test', {
    candidateId,
    projectCode: 'FAMIXA',
    characterId: 'CHAR-001',
    eraId: 'ERA-01',
  });
  return data;
}

export async function runKitVideoIdentityStress(id: string, caseCode?: string, regenerate = false, diagnosis?: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>(
    `/content/video-engine/identity/stress-test/${encodeURIComponent(id)}/run`,
    { caseCode: caseCode || null, confirmed: true, regenerate, diagnosis: diagnosis || null },
    { timeout: 300_000 },
  );
  return data;
}

export async function analyzeKitVideoIdentityStress(id: string, caseCode?: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>(
    `/content/video-engine/identity/stress-test/${encodeURIComponent(id)}/analyze`,
    null,
    { timeout: 180_000, params: caseCode ? { caseCode } : undefined },
  );
  return data;
}

export async function passKitVideoIdentityStress(id: string, reason?: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>(
    `/content/video-engine/identity/stress-test/${encodeURIComponent(id)}/director-pass`,
    { decision: 'PASS', reason: reason || '' },
  );
  return data;
}

export async function rejectKitVideoIdentityStress(id: string, reason?: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>(
    `/content/video-engine/identity/stress-test/${encodeURIComponent(id)}/reject`,
    { decision: 'REJECT', reason: reason || '' },
  );
  return data;
}

export async function repairKitVideoIdentityStress(id: string, caseCode: string, diagnosis: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>(
    `/content/video-engine/identity/stress-test/${encodeURIComponent(id)}/repair`,
    { caseCode, diagnosis, confirmed: true },
    { timeout: 180_000 },
  );
  return data;
}

export async function promoteKitVideoIdentityStress(id: string) {
  const { data } = await http.post<KitVideoIdentityStressRow>(
    `/content/video-engine/identity/stress-test/${encodeURIComponent(id)}/promote`,
    { decision: 'PROMOTE_TO_MASTER_REVIEW', reason: '' },
  );
  return data;
}

export async function fetchKitVideoIdentityStressArtifactObjectUrl(artifactId: string) {
  const { data } = await http.get<Blob>(
    `/content/video-engine/identity/stress-test/artifacts/${encodeURIComponent(artifactId)}/image`,
    { responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export type KitVideoMasterReviewShotRow = {
  id: string;
  code: string;
  label: string;
  kind: string;
  attempt: number;
  qaStatus: string;
  p0: number;
  identityScore: number;
  sha256: string;
};

export type KitVideoMasterReviewRow = {
  id: string;
  projectCode: string;
  characterId: string;
  eraId: string;
  candidateId: string;
  candidateCode: string;
  variation: string;
  parentCode: string;
  sourceSha256: string;
  status: string;
  documentId: string;
  masterRefCode: string;
  note: string;
  identityTestId?: string | null;
  stressTestId?: string | null;
  identityHave: number;
  identityRequired: number;
  identityP0: number;
  identityPass: boolean;
  stressHave: number;
  stressRequired: number;
  stressP0: number;
  stressPass: boolean;
  st10Pass: boolean;
  dnaApproved: boolean;
  canDirectorPass: boolean;
  canSelect: boolean;
  canApprove: boolean;
  canLock: boolean;
  autoSelected: boolean;
  canon: boolean;
  directorDecision?: string | null;
  blocked?: string | null;
  identityShots: KitVideoMasterReviewShotRow[];
  stressShots: KitVideoMasterReviewShotRow[];
  lock?: KitVideoMasterLockRow | null;
};

export type KitVideoMasterLockRow = {
  masterReferenceId: string;
  masterCode: string;
  characterCode: string;
  characterName: string;
  eraCode: string;
  sourceCandidateId: string;
  sourceCandidateCode: string;
  sourceVariation: string;
  artifactPath: string;
  sha256: string;
  visionFingerprint: string;
  dnaVersion: string;
  identityTestResult: string;
  stressTestResult: string;
  masterReviewResult: string;
  lockedBy: string;
  lockedAt: string;
  lockReason: string;
  version: string;
  status: string;
  canonPointerId?: string | null;
  immutable: boolean;
};

export async function listKitVideoMasterReview() {
  const { data } = await http.get<KitVideoMasterReviewRow[]>('/content/video-engine/master-review');
  return data;
}

export async function openKitVideoMasterReview(candidateId?: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>('/content/video-engine/master-review', {
    candidateId: candidateId || null,
    projectCode: 'FAMIXA',
    characterId: 'CHAR-001',
    eraId: 'ERA-01',
  });
  return data;
}

export async function passKitVideoMasterReview(id: string, note?: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>(
    `/content/video-engine/master-review/${encodeURIComponent(id)}/director-pass`,
    { decision: 'PASS', note: note || '' },
  );
  return data;
}

export async function conditionalKitVideoMasterReview(id: string, note?: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>(
    `/content/video-engine/master-review/${encodeURIComponent(id)}/conditional`,
    { decision: 'CONDITIONAL', note: note || '' },
  );
  return data;
}

export async function rejectKitVideoMasterReview(id: string, note?: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>(
    `/content/video-engine/master-review/${encodeURIComponent(id)}/reject`,
    { decision: 'REJECT', note: note || '' },
  );
  return data;
}

export async function selectKitVideoMasterReview(id: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>(
    `/content/video-engine/master-review/${encodeURIComponent(id)}/select`,
  );
  return data;
}

export async function approveKitVideoMasterReview(id: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>(
    `/content/video-engine/master-review/${encodeURIComponent(id)}/approve`,
  );
  return data;
}

export async function lockKitVideoMasterReview(id: string) {
  const { data } = await http.post<KitVideoMasterReviewRow>(
    `/content/video-engine/master-review/${encodeURIComponent(id)}/lock`,
  );
  return data;
}

export async function fetchKitVideoMasterCanon(characterId = 'CHAR-001', eraId = 'ERA-01') {
  const { data } = await http.get<KitVideoMasterLockRow | null>('/content/video-engine/master-reference/canon', {
    params: { characterId, eraId },
  });
  return data;
}

export type KitVideoCharacterDnaRow = {
  id: string;
  dnaCode: string;
  characterId: string;
  characterName: string;
  eraId: string;
  masterReferenceId: string;
  masterCode: string;
  masterSha256: string;
  dnaVersion: string;
  status: string;
  documentId: string;
  createdAt: string;
  approvedAt?: string | null;
  approvedBy?: string | null;
  lockedAt?: string | null;
  lockedBy?: string | null;
  note: string;
  spec: Record<string, unknown>;
  canApprove: boolean;
  canReject: boolean;
  immutable: boolean;
  blocked?: string | null;
  master?: KitVideoMasterLockRow | null;
  dnaSha256?: string;
  identityPass?: boolean;
  identityHave?: number;
  stressPass?: boolean;
  stressHave?: number;
  p0?: number;
  canEdit?: boolean;
  canReturn?: boolean;
  analysis?: string | null;
  selfCheck?: { code: string; label: string; pass: boolean; reason?: string | null }[];
  gatePass?: boolean;
};

export type KitVideoCharacterDnaGetRow = {
  dna?: KitVideoCharacterDnaRow | null;
  master?: KitVideoMasterLockRow | null;
  canCreate: boolean;
  blocked?: string | null;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
};

export async function fetchKitVideoCharacterDna(characterId = 'CHAR-001') {
  const { data } = await http.get<KitVideoCharacterDnaGetRow>('/content/video-engine/character-dna', {
    params: { characterId },
  });
  return data;
}

export async function fetchKitVideoCharacterDnaVersion(characterId: string, dnaVersion: string) {
  const { data } = await http.get<KitVideoCharacterDnaRow>(
    `/content/video-engine/character-dna/${encodeURIComponent(dnaVersion)}`,
    { params: { characterId } },
  );
  return data;
}

export async function createKitVideoCharacterDna(characterId = 'CHAR-001') {
  const { data } = await http.post<KitVideoCharacterDnaRow>('/content/video-engine/character-dna', null, {
    params: { characterId },
  });
  return data;
}

export async function approveKitVideoCharacterDna(characterId = 'CHAR-001', note?: string) {
  const { data } = await http.post<KitVideoCharacterDnaRow>(
    '/content/video-engine/character-dna/approve',
    { note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export async function rejectKitVideoCharacterDna(characterId = 'CHAR-001', note?: string) {
  const { data } = await http.post<KitVideoCharacterDnaRow>(
    '/content/video-engine/character-dna/reject',
    { note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export async function analyzeKitVideoCharacterDna(characterId = 'CHAR-001') {
  const { data } = await http.post<KitVideoCharacterDnaRow>(
    '/content/video-engine/character-dna/analyze',
    null,
    { params: { characterId } },
  );
  return data;
}

export async function editKitVideoCharacterDna(characterId: string, spec: Record<string, unknown>, note?: string) {
  const { data } = await http.post<KitVideoCharacterDnaRow>(
    '/content/video-engine/character-dna/edit',
    { spec, note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export async function returnKitVideoCharacterDna(characterId = 'CHAR-001', note?: string) {
  const { data } = await http.post<KitVideoCharacterDnaRow>(
    '/content/video-engine/character-dna/return',
    { note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export type CharacterReferenceCheckRow = { code: string; label: string; pass: boolean; reason?: string | null };

export type CharacterReferenceItemRow = {
  id: string;
  type: string;
  required: boolean;
  artifactPath: string;
  artifactSha256: string;
  status: string;
  metadata: Record<string, unknown>;
};

export type CharacterReferenceIdentityRow = {
  attribute: string;
  verdict: string;
  lockedValue: string;
  referenceValue: string;
  message: string;
};

export type CharacterReferencePackRow = {
  id: string;
  packCode: string;
  characterId: string;
  characterName: string;
  eraId: string;
  packVersion: string;
  status: string;
  statusLabel: string;
  masterId: string;
  masterSha256: string;
  masterLocked: boolean;
  dnaId: string;
  dnaSha256: string;
  dnaLocked: boolean;
  requiredReady: number;
  requiredTotal: number;
  coverageReady: boolean;
  identityPass: boolean;
  artifactPass: boolean;
  readyForDirector: boolean;
  productionReady: boolean;
  immutable: boolean;
  blocked?: string | null;
  packSha256?: string | null;
  notes: string;
  createdAt: string;
  approvedAt?: string | null;
  approvedBy?: string | null;
  lockedAt?: string | null;
  lockedBy?: string | null;
  items: CharacterReferenceItemRow[];
  coverage: CharacterReferenceCheckRow[];
  identity: CharacterReferenceIdentityRow[];
  authority: CharacterReferenceCheckRow[];
  spec?: unknown;
  gates?: CharacterReferenceCheckRow[];
  conflicts?: CharacterReferenceIdentityRow[];
  prpId?: string | null;
  prpSha256?: string;
  prpLocked?: boolean;
  canUse?: boolean;
  missingTypes?: string[];
  referenceAssetMissing?: string | null;
};

export type CharacterReferencePackGetRow = {
  pack?: CharacterReferencePackRow | null;
  canCreate: boolean;
  blocked?: string | null;
  masterLocked: boolean;
  dnaLocked: boolean;
  characterName: string;
  authority: CharacterReferenceCheckRow[];
  masterCandidateId?: string | null;
  characterAge?: string;
  characterSummary?: string;
  prpLocked?: boolean;
  prpSha256?: string;
  canUse?: boolean;
  missingTypes?: string[];
};

function requireCharacterId(characterId: string) {
  const id = characterId.trim();
  if (!id) throw new Error('Thiếu mã nhân vật.');
  return id;
}

export async function fetchCharacterReferencePack(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterReferencePackGetRow>('/content/video-engine/character-reference-pack', {
    params: { characterId: id, eraId },
  });
  return data;
}

export async function createCharacterReferencePack(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>('/content/video-engine/character-reference-pack', null, {
    params: { characterId: id, eraId },
  });
  return data;
}

export async function upsertCharacterReferenceItem(
  characterId: string,
  packId: string,
  body: { type: string; artifactPath?: string; artifactSha256?: string; metadata?: Record<string, unknown> },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.put<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}`,
    body,
    { params: { characterId: id } },
  );
  return data;
}

export async function registerCharacterReferenceItem(
  characterId: string,
  packId: string,
  type: string,
  artifactPath: string,
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/items/${encodeURIComponent(type)}/register`,
    { artifactPath },
    { params: { characterId: id } },
  );
  return data;
}

export async function attachCharacterReferenceItem(characterId: string, packId: string, type: string, file: File) {
  const id = requireCharacterId(characterId);
  const form = new FormData();
  form.append('file', file, file.name);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/items/${encodeURIComponent(type)}/attach`,
    form,
    { params: { characterId: id }, timeout: 60_000, maxBodyLength: Infinity },
  );
  return data;
}

export async function fetchCharacterReferenceItemObjectUrl(characterId: string, packId: string, itemId: string, sha?: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<Blob>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/items/${encodeURIComponent(itemId)}/image`,
    { params: { characterId: id, sha: sha || undefined }, responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export async function validateCharacterReferencePack(characterId: string, packId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/validate`,
    null,
    { params: { characterId: id } },
  );
  return data;
}

export async function approveCharacterReferencePack(characterId: string, packId: string, note?: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/approve`,
    { note: note || '' },
    { params: { characterId: id } },
  );
  return data;
}

export async function rejectCharacterReferencePack(characterId: string, packId: string, note: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/reject`,
    { note },
    { params: { characterId: id } },
  );
  return data;
}

export async function lockCharacterReferencePack(characterId: string, packId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/lock`,
    null,
    { params: { characterId: id } },
  );
  return data;
}

export async function supersedeCharacterReferencePack(characterId: string, packId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferencePackRow>(
    `/content/video-engine/character-reference-pack/${encodeURIComponent(packId)}/supersede`,
    null,
    { params: { characterId: id } },
  );
  return data;
}

export type CharacterLibraryViewRow = {
  characterId: string;
  name: string;
  displayName: string;
  role: string;
  readiness: string;
  readinessCode: string;
  readinessLabel: string;
  readinessReason: string;
  nextAction: string;
  canUse: boolean;
  masterLocked: boolean;
  dnaLocked: boolean;
  productionReferenceLocked: boolean;
  referencePackStatus?: string | null;
  referencePackVersion?: string | null;
  requiredReady: number;
  requiredTotal: number;
  sceneCount: number;
  frontPackId?: string | null;
  frontItemId?: string | null;
  missingTypes?: string[] | null;
};

export type CharacterLibraryDetailRow = {
  character: CharacterLibraryViewRow;
  views: { type: string; present: boolean; itemId?: string | null }[];
  scenes: { shotId: string; shotCode: string; title: string; shotSeq: number }[];
  technical?: unknown;
  generate?: boolean;
};

export async function fetchCharacterLibrary(params?: { q?: string; filter?: string; sort?: string }) {
  const { data } = await http.get<{ items: CharacterLibraryViewRow[]; total: number; generate?: boolean }>(
    '/content/video-engine/character-library',
    { params },
  );
  return data;
}

export async function fetchCharacterLibraryDetail(characterId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterLibraryDetailRow>(
    `/content/video-engine/character-library/${encodeURIComponent(id)}`,
  );
  return data;
}

export type ProductionProgressStage = { id: string; label: string; done: boolean; detail: string };

export type ProductionProgressShot = {
  shotId: string;
  sceneId: string;
  shotNumber: number;
  characterIds: string[];
  characterNames: string[];
  currentStep: string;
  nextAction: string;
  staffStatus: string;
  blockingReason?: string | null;
  imageMade: boolean;
  imageApproved: boolean;
  videoMade: boolean;
  videoApproved: boolean;
};

export type ProductionProgressScene = {
  sceneId: string;
  sceneName: string;
  shotCount: number;
  characterCount: number;
  characterNames: string[];
  imageProgress: number;
  imageApprovalProgress: number;
  videoProgress: number;
  videoApprovalProgress: number;
  nextAction: string;
  blockingReason?: string | null;
  shots: ProductionProgressShot[];
};

export type ProductionProgressRow = {
  buildId: string;
  seriesCode: string;
  episodeCode: string;
  title: string;
  sceneCount: number;
  shotCount: number;
  characterCount: number;
  imageProgress: number;
  imageApprovalProgress: number;
  videoProgress: number;
  videoApprovalProgress: number;
  finalization: string;
  publication: string;
  currentStep: string;
  nextAction: string;
  blockingReason?: string | null;
  storyLine: string;
  completedStages: number;
  totalStages: number;
  tone: string;
  stages: ProductionProgressStage[];
  scenes: ProductionProgressScene[];
  generate?: boolean;
};

export async function fetchProductionProgressList(seriesCode = 'FAMIXA') {
  const { data } = await http.get<{ items: ProductionProgressRow[]; total: number; generate?: boolean }>(
    '/content/video-engine/production-progress',
    { params: { seriesCode } },
  );
  return data;
}

export async function fetchProductionProgress(buildId: string) {
  const { data } = await http.get<ProductionProgressRow>(
    `/content/video-engine/series/builds/${encodeURIComponent(buildId)}/production-progress`,
  );
  return data;
}

export async function fetchCharacterLibraryRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
    generate: boolean;
    geminiCalled: boolean;
    runwayCalled: boolean;
    veoCalled: boolean;
  }>('/content/video-engine/character-library/regression');
  return data;
}

export type CharacterStudioSlot = {
  type: string;
  label: string;
  verdict: string;
  score: number;
  present: boolean;
  artifactPath?: string | null;
  sha256?: string | null;
};

export type CharacterStudioProgress = {
  step: string;
  label: string;
  done: boolean;
  active: boolean;
  failed: boolean;
};

export type CharacterStudioRow = {
  documentId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  styleId: string;
  styleLabel: string;
  age?: number | null;
  gender?: string | null;
  description?: string | null;
  status: string;
  statusLabel: string;
  staffMessage: string;
  nextAction: string;
  gateCode?: string | null;
  coverage: number;
  requiredTotal: number;
  canUse: boolean;
  officialLocked: boolean;
  authorityLocked: boolean;
  mayCreate: boolean;
  mayGenerate: boolean;
  mayApprove: boolean;
  mayReject: boolean;
  mayLock: boolean;
  mayRegenerate: boolean;
  mayScoreIdentity?: boolean;
  confirmRequired: boolean;
  generate?: boolean;
  providerCalled?: boolean;
  autoApproved?: boolean;
  autoLocked?: boolean;
  provider?: string | null;
  version?: string | null;
  masterSha256?: string | null;
  dnaSha256?: string | null;
  prpSha256?: string | null;
  crpSha256?: string | null;
  rejectReasonCode?: string | null;
  rejectReasonText?: string | null;
  slots: CharacterStudioSlot[];
  progress: CharacterStudioProgress[];
  history?: { version: string; status: string; statusLabel: string; coverage: number; provider?: string | null; rejectedAt?: string | null }[];
  projectVisualStyleId?: string | null;
  projectVisualStyleSha?: string | null;
  projectVisualStyleKey?: string | null;
  projectVisualStyleName?: string | null;
  visualStyleInherit?: string;
  visualStyleGate?: string;
  role?: string | null;
  personality?: string | null;
  extraDescription?: string | null;
  identitySha256?: string | null;
  generationExecutionId?: string | null;
  referenceSetId?: string | null;
  artifactIds?: string[] | null;
  logicalGenerationCount?: number;
  consistencyPass?: boolean | null;
  providerRequestId?: string | null;
  duplicateBlocked?: boolean;
  ageExpressionMinYears?: number | null;
  ageExpressionMaxYears?: number | null;
  ageLifeStage?: string | null;
  ageConsistencyStatus?: string | null;
  ageConsistencyScore?: number | null;
  ageConsistencyReason?: string | null;
  ageArtifacts?: CharacterAgeArtifact[] | null;
  ageProfile?: {
    chronologicalAge?: number | null;
    targetAppearanceAgeMin?: number | null;
    targetAppearanceAgeMax?: number | null;
    consistencyStatus?: string | null;
    lifeStage?: string | null;
    appearanceProfile?: string | null;
    pass?: boolean | null;
    score?: number | null;
    evaluatedAgeMin?: number | null;
    evaluatedAgeMax?: number | null;
    reason?: string | null;
    evaluator?: string | null;
    evaluatedAt?: string | null;
    gateCode?: string | null;
  } | null;
  appearanceProfile?: {
    chronologicalAge?: number | null;
    targetAppearanceAgeMin?: number | null;
    targetAppearanceAgeMax?: number | null;
    facialMaturityLabel?: string | null;
    lifestyleProfile?: string | null;
    energyProfile?: string | null;
    groomingProfile?: string | null;
    consistencyStatus?: string | null;
    profileSha?: string | null;
    role?: string | null;
    clothingProfile?: string | null;
    gateCode?: string | null;
  } | null;
  appearanceConsistencyStatus?: string | null;
  masterRevision?: {
    status?: string | null;
    revisionReason?: string | null;
    currentMasterSha?: string | null;
    candidateMasterSha?: string | null;
    candidateVersion?: string | null;
    mayRequest?: boolean;
    mayApprove?: boolean;
    mayReject?: boolean;
    mayLock?: boolean;
    gateCode?: string | null;
    fingerprint?: string | null;
    currentAuthority?: boolean;
    candidateIsAuthority?: boolean;
    currentVersion?: string | null;
    ageTarget?: string | null;
    appearanceSummary?: string | null;
    rejectionReason?: string | null;
    reviewDecision?: string | null;
    reviewedBy?: string | null;
    reviewNotes?: string | null;
    historicalMasterSha?: string | null;
    historicalMasterVersion?: string | null;
    crpStale?: boolean;
    dnaStale?: boolean;
    prpStale?: boolean;
  } | null;
  mayRequestMasterRevision?: boolean;
  styleConformanceStatus?: string | null;
  visualUniverseGate?: string | null;
  visualUniverseAuthoritySha?: string | null;
  visualUniverseVersion?: string | null;
  styleStale?: boolean;
  visualUniversePvsSha?: string | null;
  visualUniverseCdlSha?: string | null;
  visualCompiler?: string | null;
  visualCompilerStatus?: string | null;
};

export type CharacterMasterRevisionResult = {
  documentId: string;
  revisionId?: string | null;
  characterId: string;
  currentMasterId?: string | null;
  currentMasterSha?: string | null;
  candidateMasterId?: string | null;
  candidateMasterSha?: string | null;
  status: string;
  revisionReason?: string | null;
  revisionNotes?: string | null;
  provider?: string | null;
  generationExecuted: boolean;
  providerCalled: boolean;
  geminiCalled: boolean;
  providerRequestId?: string | null;
  fingerprint?: string | null;
  gateCode?: string | null;
  staffMessage: string;
  character?: CharacterStudioRow | null;
};

export type CharacterAgeArtifact = {
  type: string;
  apparentAge?: number | null;
  status: string;
  score?: number | null;
  reason?: string | null;
};

export type CharacterAgeConsistency = {
  documentId: string;
  characterId: string;
  canonicalAge: number;
  expressionMinAge: number;
  expressionMaxAge: number;
  lifeStage: string;
  status: string;
  score?: number | null;
  reason?: string | null;
  artifacts: CharacterAgeArtifact[];
  chronologicalAge?: number | null;
  targetAppearanceAgeMin?: number | null;
  targetAppearanceAgeMax?: number | null;
  appearanceProfile?: string | null;
  pass?: boolean | null;
  evaluatedAgeMin?: number | null;
  evaluatedAgeMax?: number | null;
  evaluator?: string | null;
  evaluatedAt?: string | null;
  gateCode?: string | null;
};

export type CharacterStudioReferenceGenerationResult = {
  documentId: string;
  status: string;
  characterId: string;
  referenceSetId?: string | null;
  generationExecutionId?: string | null;
  artifactIds: string[];
  authorityShas: {
    projectVisualStyleSha?: string | null;
    identitySha?: string | null;
    masterSha?: string | null;
    dnaSha?: string | null;
    prpSha?: string | null;
    crpSha?: string | null;
    fingerprint?: string | null;
  };
  duplicate: boolean;
  duplicateStatus?: string | null;
  providerCalled: boolean;
  geminiCalled: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
  productionGeneration: boolean;
  videoGeneration: boolean;
  logicalGenerationCount: number;
  consistencyPass?: boolean | null;
  providerRequestId?: string | null;
  gateCode?: string | null;
  staffMessage: string;
  character: CharacterStudioRow;
};

export type CharacterStudioStyle = {
  styleId: string;
  label: string;
  visual: string;
  lighting: string;
  material: string;
  camera: string;
  negative: string;
};

export async function fetchCharacterStudioStyles() {
  const { data } = await http.get<{ items: CharacterStudioStyle[] }>('/content/character-studio/styles');
  return data.items ?? [];
}

export async function fetchCharacterStudioList() {
  const { data } = await http.get<{ items: CharacterStudioRow[]; total: number }>(
    '/content/character-studio/characters',
  );
  return data;
}

export async function fetchCharacterStudio(characterId: string, eraId = 'ERA-01') {
  const { data } = await http.get<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}`,
    { params: { eraId } },
  );
  return data;
}

export async function reviewCharacterAgeConsistency(
  characterId: string,
  body: { result: 'PASS' | 'FAIL'; reasonCode?: string; note?: string },
) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/${encodeURIComponent(characterId)}/age-consistency`,
    body,
  );
  return data;
}

export async function fetchProjectVisualMode(projectId = 'FAMIXA') {
  const { data } = await http.get<{
    projectId: string;
    visualMode: string;
    visualUniverse: string;
    visualStyle: string;
    photorealismCeiling: string;
    characterRenderingMode: string;
    sceneRenderingMode: string;
    videoRenderingMode: string;
    referencePolicy: string;
    version: string;
    sha256: string;
    status: string;
  }>(`/content/project-visual-mode/${encodeURIComponent(projectId)}`);
  return data;
}

export async function reviewCharacterAppearanceConsistency(
  characterId: string,
  body: { result: 'PASS' | 'FAIL'; reasonCode?: string; note?: string },
) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/${encodeURIComponent(characterId)}/appearance-consistency`,
    body,
  );
  return data;
}

export async function fetchCharacterAgeConsistency(characterId: string, eraId = 'ERA-01') {
  const { data } = await http.get<CharacterAgeConsistency>(
    `/content/character-studio/${encodeURIComponent(characterId)}/age-consistency`,
    { params: { eraId } },
  );
  return data;
}

export async function createCharacterStudio(body: {
  name: string;
  age: number;
  gender: string;
  styleId?: string;
  description: string;
  role?: string;
  personality?: string;
  extraDescription?: string;
  confirm?: boolean;
  generate?: boolean;
  provider?: string;
}) {
  const { data } = await http.post<CharacterStudioRow>('/content/character-studio/characters', body);
  return data;
}

export async function generateCharacterStudio(
  characterId: string,
  body: { confirm: boolean; provider?: string; eraId?: string },
) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/generate`,
    body,
  );
  return data;
}

export async function generateCharacterStudioReferenceSet(
  characterId: string,
  body: { provider?: string; confirm: boolean; projectId?: string; eraId?: string } = { confirm: true, provider: 'GEMINI' },
) {
  const { data } = await http.post<CharacterStudioReferenceGenerationResult>(
    `/content/character-studio/${encodeURIComponent(characterId)}/reference-generation`,
    body,
    { timeout: 1_080_000 },
  );
  return data;
}

export async function scoreCharacterStudioIdentity(
  characterId: string,
  body: { confirm: boolean; eraId?: string } = { confirm: true },
) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/score-identity`,
    body,
    { timeout: 300_000 },
  );
  return data;
}

export async function regenerateCharacterStudio(
  characterId: string,
  body: { confirm: boolean; provider?: string; regenerateAll?: boolean; eraId?: string },
) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/regenerate`,
    body,
  );
  return data;
}

export async function approveCharacterStudio(characterId: string) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/approve`,
  );
  return data;
}

export async function rejectCharacterStudio(
  characterId: string,
  body: { rejectReasonCode: string; rejectReasonText: string },
) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/reject`,
    body,
  );
  return data;
}

export async function lockCharacterStudio(characterId: string) {
  const { data } = await http.post<CharacterStudioRow>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/lock`,
  );
  return data;
}

export async function requestCharacterStudioMasterRevision(
  characterId: string,
  body: { revisionReason: string; revisionNotes?: string; provider?: string; confirm: boolean },
) {
  const { data } = await http.post<CharacterMasterRevisionResult>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/master/revision`,
    body,
  );
  return data;
}

export async function approveCharacterStudioMasterRevision(
  characterId: string,
  body?: { notes?: string; eraId?: string },
) {
  const { data } = await http.post<CharacterMasterRevisionResult>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/master/revision/approve`,
    body ?? {},
  );
  return data;
}

export async function rejectCharacterStudioMasterRevision(
  characterId: string,
  body: { rejectionReason: string; notes?: string; eraId?: string },
) {
  const { data } = await http.post<CharacterMasterRevisionResult>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/master/revision/reject`,
    body,
  );
  return data;
}

export async function lockCharacterStudioMasterRevision(
  characterId: string,
  body?: { notes?: string; eraId?: string },
) {
  const { data } = await http.post<CharacterMasterRevisionResult>(
    `/content/character-studio/characters/${encodeURIComponent(characterId)}/master/revision/lock`,
    body ?? {},
  );
  return data;
}

function withCacheBust(url: string, token?: string | null) {
  if (!token) return url;
  return `${url}${url.includes('?') ? '&' : '?'}v=${encodeURIComponent(token)}`;
}

export function characterStudioReferenceImageUrl(
  characterId: string,
  type: string,
  cacheBust?: string | null,
) {
  return withCacheBust(
    `/api/content/character-studio/characters/${encodeURIComponent(characterId)}/references/${encodeURIComponent(type)}/image`,
    cacheBust,
  );
}

export function characterStudioMasterImageUrl(characterId: string, cacheBust?: string | null) {
  return withCacheBust(
    `/api/content/character-studio/characters/${encodeURIComponent(characterId)}/master/image`,
    cacheBust,
  );
}

export async function fetchCharacterStudioImageObjectUrl(url: string) {
  const { data } = await http.get<Blob>(url.replace(/^\/api/, ''), { responseType: 'blob' });
  return URL.createObjectURL(data);
}

export function characterStudioMasterRevisionCandidateImageUrl(
  characterId: string,
  cacheBust?: string | null,
) {
  return withCacheBust(
    `/api/content/character-studio/characters/${encodeURIComponent(characterId)}/master/revision/candidate/image`,
    cacheBust,
  );
}

export function characterStudioMasterRevisionPreviousImageUrl(
  characterId: string,
  cacheBust?: string | null,
) {
  return withCacheBust(
    `/api/content/character-studio/characters/${encodeURIComponent(characterId)}/master/revision/previous/image`,
    cacheBust,
  );
}

export async function fetchCharacterStudioRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
  }>('/content/character-studio/regression');
  return data;
}

export async function fetchCharacterReferencePackRegression() {
  const { data } = await http.get<{ suite: string; status: string; fail: number; p0: number; failures: string[] }>(
    '/content/video-engine/character-reference-pack/regression',
  );
  return data;
}

export type CharacterReferenceGenerationRow = {
  documentId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  referenceType: string;
  slotState: string;
  gateStatus: string;
  gateCode?: string | null;
  staffMessage: string;
  nextAction: string;
  authorityValid: boolean;
  providerSelected: boolean;
  capabilityReady: boolean;
  generationAllowed: boolean;
  mayCallProvider: boolean;
  confirmRequired: boolean;
  generate: boolean;
  geminiCalled: boolean;
  runwayCalled: boolean;
  veoCalled: boolean;
  shotGenerated: boolean;
  firstRealProduction: boolean;
  provider?: string | null;
  capabilityLevel?: string | null;
  intentSha256?: string | null;
  masterSha256?: string | null;
  dnaSha256?: string | null;
  prpSha256?: string | null;
  fingerprint?: string | null;
  executionId?: string | null;
  artifactId?: string | null;
  artifactPath?: string | null;
  providerRequestId?: string | null;
  candidateStatus?: string | null;
  reviewStatus?: string | null;
  requiredReady: number;
  requiredTotal: number;
  crpStatus?: string | null;
  canUse: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
  technical?: Record<string, unknown> | null;
};

export async function fetchCharacterReferenceGeneration(
  characterId: string,
  opts?: { eraId?: string; referenceType?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation`,
    { params: { eraId: opts?.eraId ?? 'ERA-01', referenceType: opts?.referenceType, provider: opts?.provider } },
  );
  return data;
}

export async function prepareCharacterReferenceGeneration(
  characterId: string,
  body?: { eraId?: string; referenceType?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation/prepare`,
    body ?? {},
  );
  return data;
}

export async function executeCharacterReferenceGeneration(
  characterId: string,
  body: { confirm: boolean; provider?: string; eraId?: string; referenceType?: string; regenerate?: boolean },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation/execute`,
    body,
    { timeout: 180_000 },
  );
  return data;
}

export async function acceptCharacterReferenceGeneration(characterId: string, executionId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation/${encodeURIComponent(executionId)}/accept`,
    {},
  );
  return data;
}

export async function rejectCharacterReferenceGeneration(characterId: string, executionId: string, reason: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation/${encodeURIComponent(executionId)}/reject`,
    { reason },
  );
  return data;
}

export async function registerCharacterReferenceGeneration(characterId: string, executionId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-pack/register`,
    { executionId },
  );
  return data;
}

export async function validateCharacterReferenceGenerationPack(characterId: string, packId: string, executionId?: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceGenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-pack/validate`,
    { packId, executionId: executionId || undefined },
  );
  return data;
}

export type CharacterReferenceSetRow = {
  documentId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  gateStatus: string;
  gateCode?: string | null;
  staffMessage: string;
  nextAction: string;
  authorityValid: boolean;
  providerSelected: boolean;
  capabilityReady: boolean;
  generationAllowed: boolean;
  mayCallProvider: boolean;
  confirmRequired: boolean;
  generate: boolean;
  providerCalled: boolean;
  geminiCalled: boolean;
  runwayCalled: boolean;
  veoCalled: boolean;
  productionGeneration: boolean;
  videoGeneration: boolean;
  provider?: string | null;
  capabilityLevel?: string | null;
  referenceSetId?: string | null;
  packId?: string | null;
  crpStatus?: string | null;
  coverage: number;
  requiredTotal: number;
  missingTypes: string[];
  assets: { type: string; present: boolean }[];
  directorReview: string;
  approved: boolean;
  locked: boolean;
  canUse: boolean;
  consistency: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
};

export async function fetchCharacterReferenceSet(
  characterId: string,
  opts?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterReferenceSetRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation-set`,
    { params: { eraId: opts?.eraId ?? 'ERA-01', provider: opts?.provider } },
  );
  return data;
}

export async function prepareCharacterReferenceSet(
  characterId: string,
  body?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceSetRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation-set/prepare`,
    body ?? {},
  );
  return data;
}

export async function executeCharacterReferenceSet(
  characterId: string,
  body: { confirm: boolean; provider?: string; eraId?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceSetRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation-set/execute`,
    body,
    { timeout: 720_000 },
  );
  return data;
}

export type CharacterReferenceHistoryItemRow = {
  version: string;
  status: string;
  statusLabel: string;
  coverage: number;
  canUse: boolean;
  sha256?: string | null;
  rejectReasonCode?: string | null;
  rejectReasonText?: string | null;
  rejectedBy?: string | null;
  rejectedAt?: string | null;
  provider?: string | null;
  setId?: string | null;
};

export type CharacterReferenceRegenerationRow = {
  documentId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  version: string;
  status: string;
  statusLabel: string;
  staffMessage: string;
  nextAction: string;
  gateCode?: string | null;
  mayReject: boolean;
  mayRegenerate: boolean;
  mayCallProvider: boolean;
  confirmRequired: boolean;
  generate: boolean;
  providerCalled: boolean;
  geminiCalled: boolean;
  runwayCalled: boolean;
  veoCalled: boolean;
  productionGeneration: boolean;
  videoGeneration: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
  canUse: boolean;
  approved: boolean;
  locked: boolean;
  coverage: number;
  requiredTotal: number;
  presentTypes: string[];
  missingTypes: string[];
  rejectReasonCode?: string | null;
  rejectReasonText?: string | null;
  rejectedBy?: string | null;
  rejectedAt?: string | null;
  provider?: string | null;
  referenceSetId?: string | null;
  fingerprint?: string | null;
  masterSha256?: string | null;
  dnaSha256?: string | null;
  prpSha256?: string | null;
  crpSha256?: string | null;
  history: CharacterReferenceHistoryItemRow[];
};

export async function fetchCharacterReferenceRegeneration(
  characterId: string,
  opts?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterReferenceRegenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-pipeline/crp`,
    { params: { eraId: opts?.eraId ?? 'ERA-01', provider: opts?.provider } },
  );
  return data;
}

export async function rejectCharacterReferenceSetReview(
  characterId: string,
  body: { rejectReasonCode?: string; rejectReasonText: string; eraId?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceRegenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-pipeline/crp/reject`,
    body,
  );
  return data;
}

export async function prepareCharacterReferenceRegeneration(
  characterId: string,
  body?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceRegenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-pipeline/crp/regenerate/prepare`,
    body ?? {},
  );
  return data;
}

export async function executeCharacterReferenceRegeneration(
  characterId: string,
  body: { confirm: boolean; provider?: string; eraId?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterReferenceRegenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-pipeline/crp/regenerate`,
    body,
    { timeout: 720_000 },
  );
  return data;
}

export async function fetchCharacterReferenceHistory(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterReferenceRegenerationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-pipeline/crp/history`,
    { params: { eraId } },
  );
  return data;
}

export type CharacterAuthorityStageRow = {
  stage: string;
  status: string;
  staffLabel: string;
  canPrepare: boolean;
  canExecute: boolean;
  canApprove: boolean;
  canReject: boolean;
  canLock: boolean;
  sha256?: string | null;
  artifactId?: string | null;
};

export type CharacterAuthorityInitializationRow = {
  documentId: string;
  characterId: string;
  characterName: string;
  role: string;
  eraId: string;
  identityReady: boolean;
  authorityLocked: boolean;
  gateStatus: string;
  gateCode?: string | null;
  staffMessage: string;
  nextAction: string;
  authorityReady: boolean;
  providerSelected: boolean;
  capabilityReady: boolean;
  generationAllowed: boolean;
  mayCallProvider: boolean;
  confirmRequired: boolean;
  generate: boolean;
  providerCalled: boolean;
  geminiCalled: boolean;
  runwayCalled: boolean;
  veoCalled: boolean;
  productionGeneration: boolean;
  videoGeneration: boolean;
  crpGeneration: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
  provider?: string | null;
  master: CharacterAuthorityStageRow;
  dna: CharacterAuthorityStageRow;
  prp: CharacterAuthorityStageRow;
  technical?: unknown;
};

export type CharacterAuthorityPipelineRow = {
  documentId: string;
  characterId: string;
  characterName: string;
  role: string;
  eraId: string;
  pipelineState: string;
  gateCode?: string | null;
  staffMessage: string;
  nextAction: string;
  workspaceCreated: boolean;
  profileReady: boolean;
  authorityReady: boolean;
  productionReady: boolean;
  authorityLocked: boolean;
  photorealisticBlocked: boolean;
  autoApproved: boolean;
  autoLocked: boolean;
  generate: boolean;
  providerCalled: boolean;
  geminiCalled: boolean;
  runwayCalled: boolean;
  veoCalled: boolean;
  productionGeneration: boolean;
  videoGeneration: boolean;
  master: CharacterAuthorityStageRow;
  dna: CharacterAuthorityStageRow;
  prp: CharacterAuthorityStageRow;
  crpStatus?: string | null;
  coverage: number;
  canUse: boolean;
  authority: {
    characterId: string;
    eraId: string;
    profileVersion?: string | null;
    masterSha?: string | null;
    dnaSha?: string | null;
    prpSha?: string | null;
    crpSha?: string | null;
    status: string;
    canUse: boolean;
  };
  technical?: unknown;
};

export async function fetchCharacterAuthorityPipeline(
  characterId: string,
  opts?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterAuthorityPipelineRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-pipeline`,
    { params: { eraId: opts?.eraId ?? 'ERA-01', provider: opts?.provider } },
  );
  return data;
}

export async function fetchCharacterAuthorityInitialization(
  characterId: string,
  opts?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization`,
    { params: { eraId: opts?.eraId ?? 'ERA-01', provider: opts?.provider } },
  );
  return data;
}

export async function prepareCharacterAuthorityMaster(
  characterId: string,
  body?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/master/prepare`,
    body ?? {},
  );
  return data;
}

export async function executeCharacterAuthorityMaster(
  characterId: string,
  body: { confirm: boolean; provider?: string; eraId?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/master/execute`,
    body,
    { timeout: 720_000 },
  );
  return data;
}

export async function approveCharacterAuthorityMaster(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/master/approve`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function rejectCharacterAuthorityMaster(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/master/reject`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function lockCharacterAuthorityMaster(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/master/lock`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function prepareCharacterAuthorityDna(
  characterId: string,
  body?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/dna/prepare`,
    body ?? {},
  );
  return data;
}

export async function executeCharacterAuthorityDna(
  characterId: string,
  body: { confirm: boolean; eraId?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/dna/execute`,
    body,
  );
  return data;
}

export async function approveCharacterAuthorityDna(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/dna/approve`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function rejectCharacterAuthorityDna(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/dna/reject`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function lockCharacterAuthorityDna(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/dna/lock`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function prepareCharacterAuthorityPrp(
  characterId: string,
  body?: { eraId?: string; provider?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/prp/prepare`,
    body ?? {},
  );
  return data;
}

export async function executeCharacterAuthorityPrp(
  characterId: string,
  body: { confirm: boolean; eraId?: string },
) {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/prp/execute`,
    body,
  );
  return data;
}

export async function approveCharacterAuthorityPrp(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/prp/approve`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function rejectCharacterAuthorityPrp(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/prp/reject`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function lockCharacterAuthorityPrp(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.post<CharacterAuthorityInitializationRow>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/prp/lock`,
    null,
    { params: { eraId } },
  );
  return data;
}

export async function fetchCharacterAuthorityMasterObjectUrl(characterId: string, eraId = 'ERA-01') {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<Blob>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/authority-initialization/master/image`,
    { params: { eraId }, responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export async function fetchCharacterReferenceGenerationImageUrl(characterId: string, executionId: string) {
  const id = requireCharacterId(characterId);
  const { data } = await http.get<Blob>(
    `/content/video-engine/characters/${encodeURIComponent(id)}/reference-generation/${encodeURIComponent(executionId)}/image`,
    { responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export async function fetchProductionOsRegression() {
  const { data } = await http.get<{
    architecture: string;
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
    generate: boolean;
    geminiCalled: boolean;
    runwayCalled: boolean;
    veoCalled: boolean;
  }>('/content/video-engine/production-os/regression');
  return data;
}

export async function fetchProductionOsCatalog() {
  const { data } = await http.get<{
    architecture: string;
    generate: boolean;
    autoSelect: boolean;
    providers: { providerId: string; kind: string; capabilities: Record<string, string> }[];
  }>('/content/video-engine/production-os/catalog');
  return data;
}

export type KitVideoProductionReferencePackRow = {
  id: string;
  packCode: string;
  characterId: string;
  characterName: string;
  eraId: string;
  masterReferenceId: string;
  masterCode: string;
  masterSha256: string;
  characterDnaId: string;
  dnaCode: string;
  dnaSha256: string;
  packVersion: string;
  status: string;
  documentId: string;
  createdAt: string;
  approvedAt?: string | null;
  approvedBy?: string | null;
  lockedAt?: string | null;
  lockedBy?: string | null;
  note: string;
  spec: Record<string, unknown>;
  canApprove: boolean;
  canReject: boolean;
  immutable: boolean;
  blocked?: string | null;
  canEdit?: boolean;
  canReturn?: boolean;
  analysis?: string | null;
  selfCheck?: { code: string; label: string; pass: boolean; reason?: string | null }[];
  gatePass?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  productionReady?: boolean;
  prpSha256?: string | null;
  directorGatePass?: boolean;
};

export type KitVideoProductionReferencePackGetRow = {
  pack?: KitVideoProductionReferencePackRow | null;
  master?: KitVideoMasterLockRow | null;
  dna?: KitVideoCharacterDnaRow | null;
  canCreate: boolean;
  blocked?: string | null;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  selfCheck?: { code: string; label: string; pass: boolean; reason?: string | null }[];
  gatePass?: boolean;
};

export async function fetchKitVideoProductionReferencePack(characterId = 'CHAR-001') {
  try {
    const { data } = await http.get<KitVideoProductionReferencePackGetRow>(
      '/content/video-engine/production-reference-pack',
      { params: { characterId } },
    );
    return data;
  } catch (e) {
    const { data } = await http.get<KitVideoProductionReferencePackGetRow>(
      `/characters/${encodeURIComponent(characterId)}/production-reference-pack`,
    );
    return data;
  }
}

export async function createKitVideoProductionReferencePack(characterId = 'CHAR-001') {
  const { data } = await http.post<KitVideoProductionReferencePackRow>(
    '/content/video-engine/production-reference-pack',
    null,
    { params: { characterId } },
  );
  return data;
}

export async function approveKitVideoProductionReferencePack(characterId = 'CHAR-001', note?: string) {
  const { data } = await http.post<KitVideoProductionReferencePackRow>(
    '/content/video-engine/production-reference-pack/approve',
    { note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export async function rejectKitVideoProductionReferencePack(characterId = 'CHAR-001', note?: string) {
  const { data } = await http.post<KitVideoProductionReferencePackRow>(
    '/content/video-engine/production-reference-pack/reject',
    { note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export async function analyzeKitVideoProductionReferencePack(characterId = 'CHAR-001') {
  const { data } = await http.post<KitVideoProductionReferencePackRow>(
    '/content/video-engine/production-reference-pack/analyze',
    null,
    { params: { characterId } },
  );
  return data;
}

export async function editKitVideoProductionReferencePack(
  characterId: string,
  spec: Record<string, unknown>,
  note?: string,
) {
  const { data } = await http.post<KitVideoProductionReferencePackRow>(
    '/content/video-engine/production-reference-pack/edit',
    { spec, note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export async function returnKitVideoProductionReferencePack(characterId = 'CHAR-001', note?: string) {
  const { data } = await http.post<KitVideoProductionReferencePackRow>(
    '/content/video-engine/production-reference-pack/return',
    { note: note || '' },
    { params: { characterId } },
  );
  return data;
}

export type KitVideoProductionShotRow = {
  id: string;
  shotCode: string;
  characterId: string;
  characterName: string;
  eraId: string;
  shotSeq: number;
  shotVersion: string;
  shotStatus: string;
  documentId: string;
  masterReferenceId: string;
  masterCode: string;
  masterSha256: string;
  characterDnaId: string;
  dnaCode: string;
  dnaSha256: string;
  productionPackId: string;
  prpCode: string;
  prpSha256: string;
  createdAt: string;
  createdBy?: string | null;
  approvedAt?: string | null;
  approvedBy?: string | null;
  lockedAt?: string | null;
  lockedBy?: string | null;
  note: string;
  spec: Record<string, unknown>;
  canApprove: boolean;
  canReject: boolean;
  immutable: boolean;
  blocked?: string | null;
  canEdit?: boolean;
  canReturn?: boolean;
  analysis?: string | null;
  selfCheck?: { code: string; label: string; pass: boolean; reason?: string | null }[];
  gatePass?: boolean;
  directorGatePass?: boolean;
  identityCheckPass?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  shotReady?: boolean;
  shotSha256?: string | null;
};

export type KitVideoProductionShotGetRow = {
  shots: KitVideoProductionShotRow[];
  master?: KitVideoMasterLockRow | null;
  dna?: KitVideoCharacterDnaRow | null;
  pack?: KitVideoProductionReferencePackRow | null;
  canCreate: boolean;
  blocked?: string | null;
  identityPass?: boolean;
  stressPass?: boolean;
  selfCheck?: { code: string; label: string; pass: boolean; reason?: string | null }[];
  gatePass?: boolean;
};

export async function fetchKitVideoProductionShots(characterId = 'CHAR-001') {
  try {
    const { data } = await http.get<KitVideoProductionShotGetRow>('/content/video-engine/production-shots', {
      params: { characterId },
    });
    return data;
  } catch {
    const { data } = await http.get<KitVideoProductionShotGetRow>(
      `/characters/${encodeURIComponent(characterId)}/production-shots`,
    );
    return data;
  }
}

export async function createKitVideoProductionShot(characterId = 'CHAR-001') {
  const { data } = await http.post<KitVideoProductionShotRow>('/content/video-engine/production-shots', null, {
    params: { characterId },
  });
  return data;
}

export async function analyzeKitVideoProductionShot(id: string) {
  const { data } = await http.post<KitVideoProductionShotRow>(`/content/video-engine/production-shots/${id}/analyze`);
  return data;
}

export async function identityCheckKitVideoProductionShot(id: string) {
  const { data } = await http.post<KitVideoProductionShotRow>(
    `/content/video-engine/production-shots/${id}/identity-check`,
  );
  return data;
}

export async function editKitVideoProductionShot(id: string, spec: Record<string, unknown>, note?: string) {
  const { data } = await http.post<KitVideoProductionShotRow>(`/content/video-engine/production-shots/${id}/edit`, {
    spec,
    note: note || '',
  });
  return data;
}

export async function approveKitVideoProductionShot(id: string, note?: string) {
  const { data } = await http.post<KitVideoProductionShotRow>(`/content/video-engine/production-shots/${id}/approve`, {
    note: note || '',
  });
  return data;
}

export async function rejectKitVideoProductionShot(id: string, note?: string) {
  const { data } = await http.post<KitVideoProductionShotRow>(`/content/video-engine/production-shots/${id}/reject`, {
    note: note || '',
  });
  return data;
}

export async function returnKitVideoProductionShot(id: string, note?: string) {
  const { data } = await http.post<KitVideoProductionShotRow>(`/content/video-engine/production-shots/${id}/return`, {
    note: note || '',
  });
  return data;
}

export type CharacterIdentityConflictRow = {
  status: string;
  code: string;
  source: string;
  attribute: string;
  requestedValue?: string | null;
  authoritativeValue?: string | null;
  message: string;
};

export type CharacterIdentityGovernanceRow = {
  status: string;
  characterId: string;
  master: string;
  dna: string;
  prp: string;
  identity: string;
  stress: string;
  continuity: string;
  regression: string;
  p0: number;
  productionAllowed: boolean;
  generate: boolean;
  code?: string | null;
  source?: string | null;
  attribute?: string | null;
  requestedValue?: string | null;
  authoritativeValue?: string | null;
  message?: string | null;
  masterId?: string | null;
  masterSha256?: string | null;
  dnaId?: string | null;
  dnaSha256?: string | null;
  prpId?: string | null;
  prpSha256?: string | null;
  shotId?: string | null;
  gates?: { code: string; label: string; pass: boolean; reason?: string | null }[];
  conflicts?: CharacterIdentityConflictRow[];
  promptContract?: {
    allowed: boolean;
    characterId: string;
    masterId?: string | null;
    masterSha256?: string | null;
    dnaId?: string | null;
    dnaSha256?: string | null;
    prpId?: string | null;
    prpSha256?: string | null;
    shotId?: string | null;
    mode: string;
    rule: string;
  };
  autoFix?: boolean;
  autoApprove?: boolean;
  autoLock?: boolean;
  directorApproval?: string;
  governanceEngine?: string;
  stressState?: string;
};

export type CharacterIdentityGovernanceAuditRow = {
  id: string;
  characterId: string;
  eraId: string;
  masterId?: string | null;
  dnaId?: string | null;
  prpId?: string | null;
  shotId?: string | null;
  gate: string;
  result: string;
  code?: string | null;
  source?: string | null;
  attribute?: string | null;
  requestedValue?: string | null;
  authoritativeValue?: string | null;
  reason: string;
  actor?: string | null;
  createdAt: string;
};

export async function fetchCharacterIdentityGovernance(characterId = 'CHAR-001') {
  try {
    const { data } = await http.get<CharacterIdentityGovernanceRow>('/content/video-engine/identity-governance', {
      params: { characterId },
    });
    return data;
  } catch {
    const { data } = await http.get<CharacterIdentityGovernanceRow>(
      `/characters/${encodeURIComponent(characterId)}/identity-governance`,
    );
    return data;
  }
}

export async function runCharacterIdentityGovernanceRegression() {
  const { data } = await http.get<{ suite: string; status: string; fail: number; p0: number; failures: string[] }>(
    '/content/video-engine/identity-governance/regression',
  );
  return data;
}

export async function checkCharacterIdentityGovernance(
  characterId: string,
  body: { shotSpec?: Record<string, unknown>; userPrompt?: string; shotId?: string },
) {
  const { data } = await http.post<CharacterIdentityGovernanceRow>(
    '/content/video-engine/identity-governance/check',
    { shotSpec: body.shotSpec, userPrompt: body.userPrompt || '', shotId: body.shotId },
    { params: { characterId } },
  );
  return data;
}

export type ProductionShotContractRow = {
  id?: string | null;
  shotId: string;
  seriesId: string;
  characterId: string;
  eraId: string;
  contractVersion: string;
  status: string;
  documentId: string;
  payload: Record<string, unknown>;
  canonicalJson: string;
  contractSha256: string;
  masterId?: string | null;
  masterSha256: string;
  dnaId?: string | null;
  dnaSha256: string;
  prpId?: string | null;
  prpSha256: string;
  createdAt?: string | null;
  createdBy?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
  validatedAt?: string | null;
  validatedBy?: string | null;
  approvedAt?: string | null;
  approvedBy?: string | null;
  immutable: boolean;
  generate: boolean;
  governanceEngine: string;
  contractValidation: string;
  directorApproval: string;
  master: string;
  dna: string;
  prp: string;
  issues: { code: string; label: string; pass: boolean; reason?: string | null }[];
  governance?: CharacterIdentityGovernanceRow | null;
};

export async function fetchProductionShotContract(shotId: string) {
  const { data } = await http.get<ProductionShotContractRow>(
    `/content/video-engine/production-shots/${shotId}/contract`,
  );
  return data;
}

export async function saveProductionShotContract(shotId: string, payload: Record<string, unknown>) {
  const { data } = await http.put<ProductionShotContractRow>(
    `/content/video-engine/production-shots/${shotId}/contract`,
    { payload },
  );
  return data;
}

export async function validateProductionShotContract(shotId: string, payload?: Record<string, unknown>) {
  const { data } = await http.post<ProductionShotContractRow>(
    `/content/video-engine/production-shots/${shotId}/contract/validate`,
    { payload },
  );
  return data;
}

export async function approveProductionShotContract(shotId: string, note?: string) {
  const { data } = await http.post<ProductionShotContractRow>(
    `/content/video-engine/production-shots/${shotId}/contract/approve`,
    { note: note || '' },
  );
  return data;
}

export async function rejectProductionShotContract(shotId: string, note?: string) {
  const { data } = await http.post<ProductionShotContractRow>(
    `/content/video-engine/production-shots/${shotId}/contract/reject`,
    { note: note || '' },
  );
  return data;
}

export async function runProductionShotContractRegression() {
  const { data } = await http.get<{ suite: string; status: string; fail: number; p0: number; failures: string[] }>(
    '/content/video-engine/production-shot-contract/regression',
  );
  return data;
}

export type ProductionPromptCompilerBlock = {
  status: string;
  code: string;
  source: string;
  attribute: string;
  requested?: string | null;
  authoritative?: string | null;
  message: string;
};

export type ProductionPromptCompilerRow = {
  id?: string | null;
  shotId: string;
  contractId?: string | null;
  seriesId: string;
  characterId: string;
  eraId: string;
  contractVersion: string;
  promptVersion: string;
  status: string;
  documentId: string;
  prompt?: string | null;
  negativeConstraints: string[];
  promptSha256: string;
  contractSha256: string;
  masterId?: string | null;
  masterSha256: string;
  dnaId?: string | null;
  dnaSha256: string;
  prpId?: string | null;
  prpSha256: string;
  createdAt?: string | null;
  createdBy?: string | null;
  immutable: boolean;
  generation: boolean;
  master: string;
  dna: string;
  prp: string;
  governanceEngine: string;
  shotContract: string;
  directorApproval: string;
  provenance: {
    masterSha256?: string | null;
    dnaSha256?: string | null;
    prpSha256?: string | null;
    contractSha256?: string | null;
    promptSha256?: string | null;
    promptVersion?: string | null;
    contractVersion?: string | null;
    contractId?: string | null;
    shotId?: string | null;
    characterId: string;
  };
  blocks: ProductionPromptCompilerBlock[];
  governance?: CharacterIdentityGovernanceRow | null;
};

export async function fetchProductionPrompt(shotId: string) {
  const { data } = await http.get<ProductionPromptCompilerRow>(`/content/video-engine/shots/${shotId}/prompt`);
  return data;
}

export async function compileProductionPrompt(shotId: string) {
  const { data } = await http.post<ProductionPromptCompilerRow>(`/content/video-engine/shots/${shotId}/prompt/compile`);
  return data;
}

export async function runProductionPromptCompilerRegression() {
  const { data } = await http.get<{ suite: string; status: string; fail: number; p0: number; failures: string[] }>(
    '/content/video-engine/production-prompt-compiler/regression',
  );
  return data;
}

export type ImageGenerationContractRow = {
  id?: string | null;
  shotId: string;
  shotContractId?: string | null;
  promptId?: string | null;
  seriesId: string;
  characterId: string;
  eraId: string;
  contractVersion: string;
  status: string;
  documentId: string;
  payload: Record<string, unknown>;
  canonicalJson: string;
  contractSha256: string;
  masterId?: string | null;
  masterSha256: string;
  dnaId?: string | null;
  dnaSha256: string;
  prpId?: string | null;
  prpSha256: string;
  shotContractSha256: string;
  promptSha256: string;
  createdAt?: string | null;
  createdBy?: string | null;
  approvedAt?: string | null;
  approvedBy?: string | null;
  immutable: boolean;
  generation: boolean;
  master: string;
  dna: string;
  prp: string;
  governanceEngine: string;
  shotContract: string;
  prompt: string;
  directorApproval: string;
  blocks: { status: string; code: string; source: string; attribute: string; requested?: string | null; authoritative?: string | null; message: string }[];
  governance?: CharacterIdentityGovernanceRow | null;
};

export async function fetchImageGenerationContract(shotId: string) {
  const { data } = await http.get<ImageGenerationContractRow>(
    `/content/video-engine/shots/${shotId}/image-generation-contract`,
  );
  return data;
}

export async function saveImageGenerationContract(
  shotId: string,
  body?: { qualityPolicy?: string; backgroundPolicy?: string; outputFormat?: string },
) {
  const { data } = await http.put<ImageGenerationContractRow>(
    `/content/video-engine/shots/${shotId}/image-generation-contract`,
    body || {},
  );
  return data;
}

export async function validateImageGenerationContract(
  shotId: string,
  body?: { qualityPolicy?: string; backgroundPolicy?: string; outputFormat?: string },
) {
  const { data } = await http.post<ImageGenerationContractRow>(
    `/content/video-engine/shots/${shotId}/image-generation-contract/validate`,
    body || {},
  );
  return data;
}

export async function approveImageGenerationContract(shotId: string, note?: string) {
  const { data } = await http.post<ImageGenerationContractRow>(
    `/content/video-engine/shots/${shotId}/image-generation-contract/approve`,
    { note: note || '' },
  );
  return data;
}

export async function rejectImageGenerationContract(shotId: string, note?: string) {
  const { data } = await http.post<ImageGenerationContractRow>(
    `/content/video-engine/shots/${shotId}/image-generation-contract/reject`,
    { note: note || '' },
  );
  return data;
}

export type ImageGenerationExecutionRow = {
  id?: string | null;
  shotId: string;
  igcId?: string | null;
  status: string;
  provider: string;
  providerStatus: string;
  providerRequestId?: string | null;
  executionFingerprint: string;
  masterSha256: string;
  dnaSha256: string;
  prpSha256: string;
  shotContractSha256: string;
  promptSha256: string;
  igcSha256: string;
  artifactPath?: string | null;
  artifactSha256: string;
  artifactMime: string;
  qa?: {
    technical: string;
    character: string;
    identity: string;
    continuity: string;
    composition: string;
    p0: number;
    overall: string;
    reasons: string[];
  } | null;
  creditStatus: string;
  creditValue?: number | null;
  preflightPass: boolean;
  runGemini: boolean;
  generation: boolean;
  immutable: boolean;
  master: string;
  dna: string;
  prp: string;
  governanceEngine: string;
  shotContract: string;
  prompt: string;
  imageGenerationContract: string;
  directorApproval: string;
  blocks: { status: string; code: string; source: string; attribute: string; message: string }[];
  crpUsable?: boolean;
  crpStatus?: string;
  staffBlock?: string | null;
  generationAllowed?: boolean;
};

export type FirstRealProductionRow = {
  documentId: string;
  shotId: string;
  shotCode: string;
  characterId: string;
  characterName: string;
  location: string;
  action: string;
  durationLabel: string;
  crpStatus: string;
  crpUsable: boolean;
  provider: string;
  capabilityStatus: string;
  intentStatus: string;
  intentSha256?: string | null;
  canonicalStatus: string;
  authorityValid: boolean;
  generationAllowed: boolean;
  mayCallProvider: boolean;
  staffMessage: string;
  nextAction: string;
  generate: boolean;
  geminiCalled: boolean;
  runwayCalled: boolean;
  veoCalled: boolean;
  masterSha256: string;
  dnaSha256: string;
  referenceSha256: string;
  shotContractSha256: string;
  executionFingerprint?: string | null;
};

export async function fetchImageGenerationExecution(shotId: string) {
  const { data } = await http.get<ImageGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/image-generation-execution`,
  );
  return data;
}

export async function preflightImageGenerationExecution(shotId: string) {
  const { data } = await http.post<ImageGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/image-generation-execution/preflight`,
  );
  return data;
}

export async function executeImageGenerationExecution(
  shotId: string,
  body?: { confirm?: boolean; provider?: string | null },
) {
  const { data } = await http.post<ImageGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/image-generation-execution/execute`,
    { confirm: body?.confirm === true, provider: body?.provider || null },
  );
  return data;
}

export async function fetchFirstRealProduction(shotId: string, provider?: string) {
  const { data } = await http.get<FirstRealProductionRow>(
    `/content/video-engine/shots/${shotId}/first-real-production`,
    { params: provider ? { provider } : undefined },
  );
  return data;
}

export function imageGenerationExecutionArtifactUrl(shotId: string, executionId: string) {
  return `/api/content/video-engine/shots/${shotId}/image-generation-execution/${executionId}/image`;
}

export async function fetchImageGenerationExecutionArtifactBlob(shotId: string, executionId: string) {
  const { data } = await http.get<Blob>(
    `/content/video-engine/shots/${shotId}/image-generation-execution/${executionId}/image`,
    { responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export type ImageGenerationDirectorReviewRow = {
  id?: string | null;
  shotId: string;
  executionId?: string | null;
  seriesId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  shotCode: string;
  reviewVersion: string;
  status: string;
  documentId: string;
  directorApproval: string;
  directorId?: string | null;
  directorAt?: string | null;
  rejectionReason?: string | null;
  master: string;
  dna: string;
  prp: string;
  governanceEngine: string;
  shotContract: string;
  prompt: string;
  imageGenerationContract: string;
  executionStatus: string;
  providerStatus: string;
  qa?: ImageGenerationExecutionRow['qa'];
  masterSha256: string;
  dnaSha256: string;
  prpSha256: string;
  shotContractSha256: string;
  promptSha256: string;
  igcSha256: string;
  artifactSha256: string;
  artifactPath?: string | null;
  artifactUrl: string;
  artifactReadable: boolean;
  canApprove: boolean;
  canReject: boolean;
  generation: boolean;
  immutable: boolean;
  blocks: { status: string; code: string; source: string; attribute: string; message: string }[];
};

export async function fetchImageDirectorReview(shotId: string) {
  const { data } = await http.get<ImageGenerationDirectorReviewRow>(
    `/content/video-engine/shots/${shotId}/image-director-review`,
  );
  return data;
}

export async function openImageDirectorReview(shotId: string) {
  const { data } = await http.post<ImageGenerationDirectorReviewRow>(
    `/content/video-engine/shots/${shotId}/image-director-review`,
  );
  return data;
}

export async function approveImageDirectorReview(shotId: string, note?: string) {
  const { data } = await http.post<ImageGenerationDirectorReviewRow>(
    `/content/video-engine/shots/${shotId}/image-director-review/approve`,
    { note: note || '' },
  );
  return data;
}

export async function rejectImageDirectorReview(shotId: string, reason: string) {
  const { data } = await http.post<ImageGenerationDirectorReviewRow>(
    `/content/video-engine/shots/${shotId}/image-director-review/reject`,
    { reason },
  );
  return data;
}

export type ProductionVideoContractRow = {
  id?: string | null;
  shotId: string;
  shotContractId?: string | null;
  stillExecutionId?: string | null;
  seriesId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  shotCode: string;
  contractVersion: string;
  status: string;
  documentId: string;
  payload: Record<string, unknown>;
  canonicalJson: string;
  contractSha256: string;
  masterId?: string | null;
  masterSha256: string;
  dnaId?: string | null;
  dnaSha256: string;
  prpId?: string | null;
  prpSha256: string;
  shotContractSha256: string;
  stillArtifactSha256: string;
  stillStatus: string;
  directorReviewStatus: string;
  stillApproved: boolean;
  artifactUrl: string;
  artifactReadable: boolean;
  rejectionReason?: string | null;
  createdAt?: string | null;
  createdBy?: string | null;
  validatedAt?: string | null;
  validatedBy?: string | null;
  approvedAt?: string | null;
  approvedBy?: string | null;
  immutable: boolean;
  generation: boolean;
  canCreate: boolean;
  canValidate: boolean;
  canApprove: boolean;
  canReject: boolean;
  master: string;
  dna: string;
  prp: string;
  governanceEngine: string;
  shotContract: string;
  directorApproval: string;
  blocks: { status: string; code: string; source: string; attribute: string; requested?: string | null; authoritative?: string | null; message: string }[];
};

export type ProductionVideoContractWrite = {
  contractId?: string;
  durationSeconds?: number;
  cameraMovementType?: string;
  cameraDirection?: string;
  cameraIntensity?: string;
  headMovementType?: string;
  headDirection?: string;
  headIntensity?: string;
  startingExpression?: string;
  endingExpression?: string;
  hairMotion?: string;
  clothMotion?: string;
  note?: string;
  reason?: string;
};

export async function fetchProductionVideoContract(shotId: string) {
  const { data } = await http.get<ProductionVideoContractRow>(
    `/content/video-engine/shots/${shotId}/video-contract`,
  );
  return data;
}

export async function saveProductionVideoContract(shotId: string, body?: ProductionVideoContractWrite) {
  const { data } = await http.post<ProductionVideoContractRow>(
    `/content/video-engine/shots/${shotId}/video-contract`,
    body || {},
  );
  return data;
}

export async function updateProductionVideoContract(
  shotId: string,
  contractId: string,
  body?: ProductionVideoContractWrite,
) {
  const { data } = await http.put<ProductionVideoContractRow>(
    `/content/video-engine/shots/${shotId}/video-contract/${contractId}`,
    body || {},
  );
  return data;
}

export async function validateProductionVideoContract(
  shotId: string,
  contractId?: string,
  body?: ProductionVideoContractWrite,
) {
  const path = contractId
    ? `/content/video-engine/shots/${shotId}/video-contract/${contractId}/validate`
    : `/content/video-engine/shots/${shotId}/video-contract/validate`;
  const { data } = await http.post<ProductionVideoContractRow>(path, body || {});
  return data;
}

export async function approveProductionVideoContract(shotId: string, contractId: string, note?: string) {
  const { data } = await http.post<ProductionVideoContractRow>(
    `/content/video-engine/shots/${shotId}/video-contract/${contractId}/approve`,
    { note: note || '' },
  );
  return data;
}

export async function rejectProductionVideoContract(shotId: string, contractId: string, reason: string) {
  const { data } = await http.post<ProductionVideoContractRow>(
    `/content/video-engine/shots/${shotId}/video-contract/${contractId}/reject`,
    { reason },
  );
  return data;
}

export async function fetchProductionVideoContractRegression() {
  const { data } = await http.get<{
    suite: string;
    status: string;
    fail: number;
    p0: number;
    failures: string[];
    generate: boolean;
    geminiCalled: boolean;
    runwayCalled: boolean;
  }>('/content/video-engine/video-contract/regression');
  return data;
}

export type VideoGenerationExecutionRow = {
  id?: string | null;
  shotId: string;
  videoContractId?: string | null;
  stillExecutionId?: string | null;
  seriesId: string;
  characterId: string;
  characterName: string;
  eraId: string;
  shotCode: string;
  documentId: string;
  status: string;
  provider: string;
  providerStatus: string;
  providerRequestId?: string | null;
  providerConfigVersion: string;
  executionFingerprint: string;
  masterSha256: string;
  dnaSha256: string;
  prpSha256: string;
  shotContractSha256: string;
  promptSha256: string;
  igcSha256: string;
  videoContractSha256: string;
  stillArtifactSha256: string;
  durationSeconds: number;
  resolution: string;
  fps: string;
  aspectRatio: string;
  artifactPath?: string | null;
  artifactSha256: string;
  artifactMime: string;
  artifactUrl: string;
  artifactReadable: boolean;
  qa?: {
    technical: string;
    artifact: string;
    identity: string;
    continuity: string;
    duration: string;
    resolution: string;
    fps: string;
    aspect: string;
    p0: number;
    overall: string;
    reasons: string[];
  } | null;
  creditStatus: string;
  preflightPass: boolean;
  runProvider: boolean;
  generation: boolean;
  immutable: boolean;
  canExecute: boolean;
  canApprove: boolean;
  canReject: boolean;
  requestedAt?: string | null;
  master: string;
  dna: string;
  prp: string;
  governanceEngine: string;
  shotContract: string;
  prompt: string;
  imageGenerationContract: string;
  imageDirectorApproval: string;
  videoContract: string;
  directorApproval: string;
  blocks: { status: string; code: string; source: string; attribute: string; message: string }[];
};

export async function fetchVideoGenerationExecution(shotId: string) {
  const { data } = await http.get<VideoGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/video-generation-execution`,
  );
  return data;
}

export async function preflightVideoGenerationExecution(shotId: string) {
  const { data } = await http.post<VideoGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/video-generation-execution/preflight`,
  );
  return data;
}

export async function executeVideoGenerationExecution(shotId: string) {
  const { data } = await http.post<VideoGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/video-generation-execution/execute`,
    {},
    { timeout: 360_000 },
  );
  return data;
}

export async function approveVideoGenerationExecution(shotId: string, note?: string) {
  const { data } = await http.post<VideoGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/video-generation-execution/approve`,
    { note: note || '' },
  );
  return data;
}

export async function rejectVideoGenerationExecution(shotId: string, reason: string) {
  const { data } = await http.post<VideoGenerationExecutionRow>(
    `/content/video-engine/shots/${shotId}/video-generation-execution/reject`,
    { reason },
  );
  return data;
}

export function videoGenerationExecutionArtifactUrl(shotId: string, executionId: string) {
  return `/api/content/video-engine/shots/${shotId}/video-generation-execution/${executionId}/video`;
}

export async function fetchVideoGenerationExecutionArtifactBlob(shotId: string, executionId: string) {
  const { data } = await http.get<Blob>(
    `/content/video-engine/shots/${shotId}/video-generation-execution/${executionId}/video`,
    { responseType: 'blob' },
  );
  return URL.createObjectURL(data);
}

export async function fetchCharacterIdentityGovernanceAudit(characterId = 'CHAR-001') {
  const { data } = await http.get<CharacterIdentityGovernanceAuditRow[]>(
    '/content/video-engine/identity-governance/audit',
    { params: { characterId } },
  );
  return data;
}

export type KitVideoMotionTakeRow = {
  takeId: string;
  productionId: string;
  shotCode: string;
  keyframeAttemptId: string;
  attemptNo: number;
  status: string;
  model: string;
  durationSec: number;
  prompt: string;
  fingerprint: string;
  sourceArtifactHash: string;
  videoHash?: string | null;
  runwayTaskId?: string | null;
  outputUrl?: string | null;
  videoPath?: string | null;
  failureClass: string;
  failureCode: string;
  retryReason: string;
  creditState: string;
  preflight?: { ok: boolean; blocked: string[] } | null;
  qa?: { status: string; p0Fail: string[]; warnings?: string[]; allowApprove: boolean } | null;
  diagnose: string;
  runwayCalled: boolean;
  videoReady: boolean;
  runwayAccepted?: boolean;
  estimatedCredit?: string;
  actualCredit?: string;
};

export async function preflightKitVideoMotion(body: {
  keyframeAttemptId: string;
  motionContract: unknown;
  confirmed: boolean;
  idempotencyKey: string;
}) {
  const { data } = await http.post<KitVideoMotionTakeRow>('/content/video-engine/motion/preflight', body);
  return data;
}

export async function submitKitVideoMotion(body: {
  keyframeAttemptId: string;
  motionContract: unknown;
  confirmed: boolean;
  idempotencyKey: string;
  retryReason?: string;
}) {
  const { data } = await http.post<KitVideoMotionTakeRow>('/content/video-engine/motion/submit', body, {
    timeout: 180_000,
  });
  return data;
}

export async function pollKitVideoMotion(takeId: string) {
  const { data } = await http.post<KitVideoMotionTakeRow>(`/content/video-engine/motion/${takeId}/poll`, {}, { timeout: 180_000 });
  return data;
}

export async function fetchKitVideoMotion(takeId: string) {
  const { data } = await http.get<KitVideoMotionTakeRow>(`/content/video-engine/motion/${takeId}`);
  return data;
}

export async function fetchLatestKitVideoMotion(attemptId: string) {
  const { data } = await http.get<KitVideoMotionTakeRow>('/content/video-engine/motion/latest', {
    params: { attemptId },
  });
  return data;
}

export async function decideKitVideoMotion(takeId: string, decision: 'APPROVE' | 'REJECT') {
  const { data } = await http.post<KitVideoMotionTakeRow>(`/content/video-engine/motion/${takeId}/decide`, { decision });
  return data;
}

export async function fetchKitVideoMotionVideoBlob(takeId: string) {
  const { data } = await http.get<Blob>(`/content/video-engine/motion/${takeId}/video`, { responseType: 'blob' });
  return data;
}

export function kitVideoMotionVideoUrl(takeId: string) {
  return `/api/content/video-engine/motion/${encodeURIComponent(takeId)}/video`;
}

export async function reviseKitVideoKeyframe(note: string) {
  const { data } = await http.post<{ instruction: string }>('/content/video-engine/visual/revision', { note });
  return data;
}

export function kitVideoAttemptImageUrl(attemptId: string) {
  return `/api/content/video-engine/visual/attempts/${encodeURIComponent(attemptId)}/image`;
}

export async function fetchKitVideoAttemptImageBlob(attemptId: string) {
  const { data } = await http.get<Blob>(
    `/content/video-engine/visual/attempts/${encodeURIComponent(attemptId)}/image`,
    { responseType: 'blob' },
  );
  return data;
}

export async function fetchContentSeriesVoices() {
  const { data } = await http.get<ContentSeriesVoice[]>('/content/series/voices', { timeout: 60_000 });
  return data;
}

/** Library preview_url only. Not TTS. */
export async function fetchContentSeriesVoicePreview(voiceId: string) {
  const { data } = await http.get<Blob>(
    `/content/series/voices/${encodeURIComponent(voiceId)}/preview`,
    { responseType: 'blob', timeout: 45_000 },
  );
  if (!data || data.size === 0 || (data.type || '').includes('json')) {
    throw new Error('Giọng này chưa có file mẫu thư viện.');
  }
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
    const decisionId = String(headers['x-famixa-decision-id'] ?? '').trim();
    const providerId = String(headers['x-famixa-provider-id'] ?? '').trim();
    return {
      blob: data,
      selectionSnapshot: decisionId && providerId
        ? {
            decisionId,
            providerId,
            modelId: String(headers['x-famixa-model-id'] ?? '').trim() || undefined,
            selectionMode: '',
          }
        : undefined,
    };
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

/** Staff upload cover image from computer (JPG/PNG/WEBP/GIF). Auto-selected. */
export async function uploadContentTopicAsset(topicId: string, file: File) {
  const form = new FormData();
  form.append('file', file);
  const { data } = await http.post<ContentAsset>(`/content/topics/${topicId}/assets`, form, {
    timeout: 60_000,
    maxBodyLength: Infinity,
    maxContentLength: Infinity,
  });
  return data;
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
