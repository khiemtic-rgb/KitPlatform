import { http } from '@/shared/api/http';

export interface KitSalesHealth {
  pack: string;
  version: string;
  ok: boolean;
}

export interface KitSalesProduct {
  code: string;
  displayName: string;
  status: string;
}

export interface KitSalesBusiness {
  id: string;
  name: string;
  businessType: string;
  province?: string | null;
  phone?: string | null;
  status: string;
  source?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface KitSalesLead {
  id: string;
  businessId: string;
  businessName: string;
  productCode: string;
  leadStatus: string;
  leadTemperature: string;
  totalScore: number;
  source?: string | null;
  province?: string | null;
  phone?: string | null;
  notes?: string | null;
  ownerUserId?: string | null;
  nextActionCode?: string | null;
  nextActionAt?: string | null;
  lastInteractionAt?: string | null;
  createdAt: string;
  updatedAt: string;
  facebookKind?: string | null;
  facebookUrl?: string | null;
}

export interface KitSalesPipelineBucket {
  status: string;
  count: number;
}

export interface KitSalesPipelineSummary {
  totalLeads: number;
  byStatus: KitSalesPipelineBucket[];
}

export interface CreateKitSalesProspectBody {
  businessName: string;
  productCode?: string;
  businessType?: string;
  province?: string;
  phone?: string;
  source?: string;
  notes?: string;
  facebookKind?: string;
}

export interface UpdateKitSalesLeadBody {
  businessName?: string;
  province?: string;
  phone?: string;
  source?: string;
  leadStatus?: string;
  leadTemperature?: string;
  notes?: string;
  nextActionCode?: string;
  nextActionAt?: string | null;
  clearNextAction?: boolean;
  facebookKind?: string;
}

export interface KitSalesInteraction {
  id: string;
  leadId: string;
  channel: string;
  direction: string;
  interactionType: string;
  content?: string | null;
  outcome?: string | null;
  occurredAt: string;
  reviewStatus?: string;
  aiGenerated?: boolean;
  approvedByUserId?: string | null;
  sentAt?: string | null;
  classification?: string | null;
  sentByName?: string | null;
  approvedByName?: string | null;
}

export interface CreateKitSalesInteractionBody {
  channel?: string;
  direction?: string;
  interactionType?: string;
  content?: string;
  outcome?: string;
}

export interface KitSalesTask {
  id: string;
  leadId: string;
  actionCode: string;
  title: string;
  status: string;
  dueAt?: string | null;
  completedAt?: string | null;
  createdAt: string;
}

export interface CreateKitSalesTaskBody {
  actionCode: string;
  title?: string;
  dueAt?: string | null;
}

export interface KitSalesLeadDetail {
  lead: KitSalesLead;
  interactions: KitSalesInteraction[];
  tasks: KitSalesTask[];
}

export async function fetchKitSalesHealth(): Promise<KitSalesHealth> {
  const { data } = await http.get<KitSalesHealth>('/kit-sales/health');
  return data;
}

export async function fetchKitSalesProducts(): Promise<KitSalesProduct[]> {
  const { data } = await http.get<KitSalesProduct[]>('/kit-sales/products');
  return data;
}

export interface KitSalesLeadFilters {
  provinces: string[];
  byFacebookKind: KitSalesPipelineBucket[];
}

export async function fetchKitSalesLeads(params?: {
  status?: string;
  limit?: number;
  province?: string;
  facebookKind?: string;
}): Promise<KitSalesLead[]> {
  const { data } = await http.get<KitSalesLead[]>('/kit-sales/leads', { params });
  return data;
}

export async function fetchKitSalesLeadFilters(): Promise<KitSalesLeadFilters> {
  const { data } = await http.get<KitSalesLeadFilters>('/kit-sales/leads/filters');
  return data;
}

export async function fetchKitSalesPipelineSummary(): Promise<KitSalesPipelineSummary> {
  const { data } = await http.get<KitSalesPipelineSummary>('/kit-sales/pipeline/summary');
  return data;
}

export async function createKitSalesProspect(
  body: CreateKitSalesProspectBody,
): Promise<KitSalesLead> {
  const { data } = await http.post<KitSalesLead>('/kit-sales/prospects', body);
  return data;
}

export interface ImportKitSalesProspectsBody {
  text: string;
  province?: string;
  facebookKind?: string;
}

export interface KitSalesImportResult {
  created: number;
  skipped: number;
  errors: string[];
  leads: KitSalesLead[];
}

export async function importKitSalesProspects(
  body: ImportKitSalesProspectsBody,
): Promise<KitSalesImportResult> {
  const { data } = await http.post<KitSalesImportResult>('/kit-sales/prospects/import', body);
  return data;
}

export async function updateKitSalesLead(
  leadId: string,
  body: UpdateKitSalesLeadBody,
): Promise<KitSalesLead> {
  const { data } = await http.put<KitSalesLead>(`/kit-sales/leads/${leadId}`, body);
  return data;
}

export async function deleteKitSalesLead(leadId: string): Promise<void> {
  await http.delete(`/kit-sales/leads/${leadId}`);
}

export async function fetchKitSalesLeadDetail(leadId: string): Promise<KitSalesLeadDetail> {
  const { data } = await http.get<KitSalesLeadDetail>(`/kit-sales/leads/${leadId}`);
  return data;
}

export async function createKitSalesInteraction(
  leadId: string,
  body: CreateKitSalesInteractionBody,
): Promise<KitSalesInteraction> {
  const { data } = await http.post<KitSalesInteraction>(
    `/kit-sales/leads/${leadId}/interactions`,
    body,
  );
  return data;
}

export async function createKitSalesTask(
  leadId: string,
  body: CreateKitSalesTaskBody,
): Promise<KitSalesTask> {
  const { data } = await http.post<KitSalesTask>(`/kit-sales/leads/${leadId}/tasks`, body);
  return data;
}

export async function completeKitSalesTask(taskId: string): Promise<KitSalesTask> {
  const { data } = await http.post<KitSalesTask>(`/kit-sales/tasks/${taskId}/complete`);
  return data;
}

export interface KitSalesChatChannel {
  facebookConfigured: boolean;
  facebookSendReady: boolean;
  meLink: string;
  phcUrl: string;
  pageUrl: string;
}

export interface KitSalesChatPreview {
  intent: string;
  reply: string;
  escalate: boolean;
  citations: string[];
  facebookSendReady: boolean;
  meLink: string;
  phcUrl: string;
}

export interface KitSalesAssist {
  leadId: string;
  businessName: string;
  headline: string;
  summary: string;
  goal: string;
  context: string;
  classification?: string | null;
  classificationLabel: string;
  nextHint: string;
  nextActionCode: string;
  nextActionLabel: string;
  suggestedAt: string;
  windowLabel: string;
  whyNow: string;
  lastInboundContent?: string | null;
  needsReply?: boolean;
}

export interface KitSalesChatCompose extends KitSalesChatPreview {
  draftId?: string | null;
  reviewStatus: string;
  classificationLabel: string;
  nextHint: string;
  assist?: KitSalesAssist | null;
  geminiUsed?: boolean;
  autoReplied?: boolean;
}

export async function fetchKitSalesChatChannel(): Promise<KitSalesChatChannel> {
  const { data } = await http.get<KitSalesChatChannel>('/kit-sales/chat/channel');
  return data;
}

export async function previewKitSalesChat(text: string): Promise<KitSalesChatPreview> {
  const { data } = await http.post<KitSalesChatPreview>('/kit-sales/chat/preview', { text });
  return data;
}

export async function replyKitSalesChat(
  text: string,
  leadId?: string,
  log = true,
): Promise<KitSalesChatPreview> {
  const { data } = await http.post<KitSalesChatPreview>('/kit-sales/chat/reply', {
    text,
    leadId,
    log,
  });
  return data;
}

export async function fetchKitSalesAssist(leadId: string): Promise<KitSalesAssist> {
  const { data } = await http.get<KitSalesAssist>(`/kit-sales/leads/${leadId}/assist`);
  return data;
}

export async function composeKitSalesChat(
  text: string,
  leadId?: string,
  autoReply = false,
): Promise<KitSalesChatCompose> {
  const { data } = await http.post<KitSalesChatCompose>(
    '/kit-sales/chat/compose',
    {
      text,
      leadId,
      autoReply,
    },
    { timeout: 50_000 },
  );
  return data;
}

export async function sendKitSalesDraft(
  draftId: string,
  content?: string,
): Promise<KitSalesInteraction> {
  const { data } = await http.post<KitSalesInteraction>(`/kit-sales/chat/drafts/${draftId}/send`, {
    content,
  });
  return data;
}

export async function approveKitSalesDraft(
  draftId: string,
  content?: string,
): Promise<KitSalesInteraction> {
  const { data } = await http.post<KitSalesInteraction>(
    `/kit-sales/chat/drafts/${draftId}/approve`,
    { content },
  );
  return data;
}

export interface KitSalesJourneyPain {
  code: string;
  label: string;
  source: string;
  category?: string;
  categoryLabel?: string;
}

export interface KitSalesJourneyPlan {
  leadId: string;
  businessName: string;
  step: string;
  painCode: string;
  painLabel: string;
  draft: string;
  citations: string[];
  suggestedAt: string;
  windowLabel: string;
  whyNow: string;
  isDue: boolean;
  facebookSendReady: boolean;
  painCategory?: string | null;
  hook?: string | null;
  outreach?: string | null;
  question?: string | null;
  cta?: string | null;
  solutionDirection?: string | null;
  whyThisPain?: string | null;
  discoveryMode?: string;
  facebookUrl?: string | null;
  geminiUsed?: boolean;
}

export interface KitSalesPain {
  code: string;
  category: string;
  categoryLabel: string;
  name: string;
  description: string;
  symptoms: string[];
  detectionSignals: string[];
  hook: string;
  outreach: string;
  question: string;
  solutionDirection: string;
  solutionMapping: string;
  cta: string;
  objections: string[];
  phcMapping: string;
}

export interface KitSalesPainMarketBucket {
  category: string;
  categoryLabel: string;
  approached: number;
  replied: number;
  interested: number;
  signalRate: number;
  exploreShare: number;
}

export interface KitSalesPainMarket {
  mode: string;
  touchedLeads: number;
  byCategory: KitSalesPainMarketBucket[];
}

export interface KitSalesLeadPainScore {
  painCode: string;
  category: string;
  name: string;
  confidence: number;
  responseCode?: string | null;
  approachedCount: number;
  confirmed: boolean;
}

export interface KitSalesLeadPainProfile {
  leadId: string;
  recommendedPainCode: string;
  discoveryMode: string;
  whyThisPain: string;
  scores: KitSalesLeadPainScore[];
  categories: KitSalesPainMarketBucket[];
}

export async function fetchKitSalesJourneyPains(): Promise<KitSalesJourneyPain[]> {
  const { data } = await http.get<KitSalesJourneyPain[]>('/kit-sales/journey/pains');
  return data;
}

export async function fetchKitSalesJourneyDue(limit = 20): Promise<KitSalesJourneyPlan[]> {
  const { data } = await http.get<KitSalesJourneyPlan[]>('/kit-sales/journey/due', {
    params: { limit },
  });
  return data;
}

export async function fetchKitSalesLeadJourney(
  leadId: string,
  pain?: string,
): Promise<KitSalesJourneyPlan> {
  const { data } = await http.get<KitSalesJourneyPlan>(`/kit-sales/leads/${leadId}/journey`, {
    params: pain ? { pain } : undefined,
  });
  return data;
}

export async function scheduleKitSalesJourney(
  leadId: string,
  body?: { painCode?: string; step?: string; content?: string },
): Promise<KitSalesJourneyPlan> {
  const { data } = await http.post<KitSalesJourneyPlan>(
    `/kit-sales/leads/${leadId}/journey/schedule`,
    body ?? {},
  );
  return data;
}

export async function fetchKitSalesPainLibrary(): Promise<KitSalesPain[]> {
  const { data } = await http.get<KitSalesPain[]>('/kit-sales/pains');
  return data;
}

export async function fetchKitSalesPainMarket(): Promise<KitSalesPainMarket> {
  const { data } = await http.get<KitSalesPainMarket>('/kit-sales/pains/market');
  return data;
}

export async function fetchKitSalesLeadPains(leadId: string): Promise<KitSalesLeadPainProfile> {
  const { data } = await http.get<KitSalesLeadPainProfile>(`/kit-sales/leads/${leadId}/pains`);
  return data;
}

export async function recordKitSalesPainResponse(
  leadId: string,
  painCode: string,
  responseCode: string,
  note?: string,
): Promise<void> {
  await http.post(`/kit-sales/leads/${leadId}/pains/${encodeURIComponent(painCode)}/response`, {
    responseCode,
    note,
  });
}

export async function sendKitSalesJourney(
  leadId: string,
  body?: { painCode?: string; step?: string; content?: string },
): Promise<KitSalesJourneyPlan> {
  const { data } = await http.post<KitSalesJourneyPlan>(
    `/kit-sales/leads/${leadId}/journey/send`,
    body ?? {},
  );
  return data;
}

export async function rewriteKitSalesJourney(
  leadId: string,
  body?: { painCode?: string; step?: string },
): Promise<KitSalesJourneyPlan> {
  const { data } = await http.post<KitSalesJourneyPlan>(
    `/kit-sales/leads/${leadId}/journey/rewrite`,
    body ?? {},
    { timeout: 50_000 },
  );
  return data;
}

export interface KitSalesFacebookSettings {
  enabled: boolean;
  hasPageToken: boolean;
  pageTokenLast4?: string | null;
  hasVerifyToken: boolean;
  hasAppSecret: boolean;
  pageId?: string | null;
  meLink: string;
  pageUrl: string;
  phcUrl: string;
  webhookUrl: string;
  sendReady: boolean;
}

export interface SaveKitSalesFacebookSettingsBody {
  enabled: boolean;
  pageAccessToken?: string;
  clearPageToken?: boolean;
  verifyToken?: string;
  clearVerifyToken?: boolean;
  appSecret?: string;
  clearAppSecret?: boolean;
  pageId?: string;
  meLink?: string;
  pageUrl?: string;
  phcUrl?: string;
}

export interface KitSalesFacebookTest {
  ok: boolean;
  pageId?: string | null;
  pageName?: string | null;
  error?: string | null;
}

export interface KitSalesFacebookPsid {
  leadId: string;
  businessId: string;
  businessName: string;
  psid: string;
  facebookUrl?: string | null;
  createdAt: string;
}

export async function fetchKitSalesFacebookSettings(): Promise<KitSalesFacebookSettings> {
  const { data } = await http.get<KitSalesFacebookSettings>('/kit-sales/settings/facebook');
  return data;
}

export async function saveKitSalesFacebookSettings(
  body: SaveKitSalesFacebookSettingsBody,
): Promise<KitSalesFacebookSettings> {
  const { data } = await http.put<KitSalesFacebookSettings>('/kit-sales/settings/facebook', body);
  return data;
}

export async function testKitSalesFacebookSettings(): Promise<KitSalesFacebookTest> {
  const { data } = await http.post<KitSalesFacebookTest>('/kit-sales/settings/facebook/test');
  return data;
}

export async function fetchKitSalesFacebookPsids(): Promise<KitSalesFacebookPsid[]> {
  const { data } = await http.get<KitSalesFacebookPsid[]>('/kit-sales/settings/facebook/psids');
  return data;
}

export async function fetchKitSalesLeadPsid(leadId: string): Promise<KitSalesFacebookPsid | null> {
  const { data, status } = await http.get<KitSalesFacebookPsid | ''>(`/kit-sales/leads/${leadId}/psid`, {
    validateStatus: (code) => code === 200 || code === 204,
  });
  return status === 204 || !data ? null : data;
}

export async function attachKitSalesLeadPsid(
  leadId: string,
  psid: string,
): Promise<KitSalesFacebookPsid> {
  const { data } = await http.post<KitSalesFacebookPsid>(`/kit-sales/leads/${leadId}/psid`, { psid });
  return data;
}

export async function detachKitSalesLeadPsid(leadId: string): Promise<void> {
  await http.delete(`/kit-sales/leads/${leadId}/psid`);
}

export interface KitSalesGeminiSettings {
  hasApiKey: boolean;
  apiKeyLast4?: string | null;
  textModel?: string | null;
  ready: boolean;
  source: string;
}

export interface SaveKitSalesGeminiSettingsBody {
  geminiApiKey?: string;
  clearApiKey?: boolean;
  textModel?: string;
}

export interface KitSalesGeminiTest {
  ok: boolean;
  model?: string | null;
  error?: string | null;
}

export async function fetchKitSalesGeminiSettings(): Promise<KitSalesGeminiSettings> {
  const { data } = await http.get<KitSalesGeminiSettings>('/kit-sales/settings/gemini');
  return data;
}

export async function saveKitSalesGeminiSettings(
  body: SaveKitSalesGeminiSettingsBody,
): Promise<KitSalesGeminiSettings> {
  const { data } = await http.put<KitSalesGeminiSettings>('/kit-sales/settings/gemini', body);
  return data;
}

export async function testKitSalesGeminiSettings(): Promise<KitSalesGeminiTest> {
  const { data } = await http.post<KitSalesGeminiTest>('/kit-sales/settings/gemini/test');
  return data;
}
