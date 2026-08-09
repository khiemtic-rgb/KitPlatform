import axios from 'axios';

const api = axios.create({
  baseURL: '/api/public/assessment',
  withCredentials: true,
});

const PARTNER_REF_KEY = 'kap_partner_ref';

export function rememberPartnerRef(code: string | null | undefined) {
  const trimmed = code?.trim();
  if (!trimmed) return;
  try {
    sessionStorage.setItem(PARTNER_REF_KEY, trimmed.toUpperCase());
  } catch {
    /* ignore */
  }
}

export function getPartnerRef(): string | null {
  try {
    return sessionStorage.getItem(PARTNER_REF_KEY);
  } catch {
    return null;
  }
}

export type AssessmentOption = {
  id: string;
  code: string;
  label: string;
  score: number | null;
  sortOrder: number;
};

export type AssessmentQuestion = {
  id: string;
  code: string;
  title: string;
  helpText?: string | null;
  questionType: string;
  scorable: boolean;
  required: boolean;
  sortOrder: number;
  options: AssessmentOption[];
  metadata?: Record<string, unknown> | null;
};

export type AssessmentCategory = {
  code: string;
  name: string;
  sortOrder: number;
  dimensions: { code: string; name: string; questions: AssessmentQuestion[] }[];
};

export type AssessmentTemplate = {
  id: string;
  code: string;
  name: string;
  version: string;
  categories: AssessmentCategory[];
};

export type CreateSubmissionResult = {
  id: string;
  sessionToken: string;
  status: string;
  templateCode: string;
  templateVersion: string;
  expiresAt: string;
};

export type SubmissionDetail = {
  id: string;
  status: string;
  templateCode: string;
  templateVersion: string;
  startedAt: string;
  completedAt?: string | null;
  responses: Record<string, { optionId?: string | null; textValue?: string | null }>;
  overallScore?: number | null;
  overallPct?: number | null;
  categoryScores?: CategoryScore[];
  previewInsights?: { title: string; body: string; severity: string }[];
};

export type CategoryScore = {
  code: string;
  name: string;
  score: number;
  scorePct: number;
};

export type CompleteResult = {
  status: string;
  overallScore: number;
  overallPct: number;
  categoryScores: CategoryScore[];
  previewInsights: { title: string; body: string; severity: string }[];
  reportLocked: boolean;
  leadCaptureRequired: boolean;
};

export type FullReport = {
  submissionId: string;
  templateCode: string;
  completedAt?: string | null;
  overallScore: number;
  overallPct: number;
  categoryScores: CategoryScore[];
  dimensionScores: { code: string; name: string; categoryCode: string; score: number; scorePct: number }[];
  insights: { title: string; body: string; severity: string }[];
  recommendations: {
    title: string;
    body: string;
    priority: number;
    productArea?: string | null;
    estimateHint?: string | null;
  }[];
  qualitativeTags: { painPoint?: string | null; priorityNeed?: string | null };
  pdf: { available: boolean; downloadUrl?: string | null };
  intelligence?: ReportIntelligence | null;
};

export type ReportIntelligence = {
  schemaVersion: string;
  maturity?: { level: number; code: string; name: string; description: string } | null;
  swot?: {
    strengths: { title: string; body: string }[];
    weaknesses: { title: string; body: string }[];
    opportunities: { title: string; body: string }[];
    threats: { title: string; body: string }[];
  } | null;
  rootCauses: { code: string; title: string; body: string; evidence: string[] }[];
  benchmark?: {
    cohortCode: string;
    sampleSize: number;
    overallVsMean?: number | null;
    narrative?: string | null;
    estimatedPercentile?: number | null;
    percentileLabel?: string | null;
    categories: { code: string; name: string; score: number; cohortMean?: number | null; delta?: number | null }[];
    tiers?: { label: string; scorePct: number; note?: string | null }[];
  } | null;
  opportunities?: { area: string; title: string; body: string; impactHint?: string | null }[];
  risks: { area: string; level: string; title: string; body: string }[];
  roadmap?: {
    days30: { title: string; body: string }[];
    days60: { title: string; body: string }[];
    days90: { title: string; body: string }[];
    days180?: { title: string; body: string }[];
  } | null;
  kpis: { name: string; target: string; deadlineDays: number }[];
  priorityMatrix?: {
    highImpactHighPriority: { title: string; body: string }[];
    quickWins: { title: string; body: string }[];
    longTerm: { title: string; body: string }[];
    optional: { title: string; body: string }[];
  } | null;
  executiveSummary?: {
    headline: string;
    paragraphs: string[];
    openingContext?: string | null;
    analysis?: string | null;
    assessment?: string | null;
    conclusion?: string | null;
    recommendations?: string | null;
  } | null;
  consultingBrief?: {
    diagnosisHeadline: string;
    costOfInaction: string;
    businessImpacts: { area: string; title: string; impactStatement: string; costHint: string }[];
    moduleFits: {
      moduleName: string;
      painResolved: string;
      outcome30Days: string;
      outcome90Days: string;
      priority: number;
    }[];
    roiStory: { summary: string; beforeState: string[]; afterState: string[] };
    urgencyStatement: string;
    nextStepCta: string;
  } | null;
  aiNarrative?: {
    source: string;
    model?: string | null;
    generatedAt: string;
    personalizedInsights: string[];
    aiConclusion?: string | null;
  } | null;
  executiveDashboard?: {
    topProblems: string[];
    topRisks: string[];
    topOpportunities: string[];
    digitalReadinessPct: number;
    novixaFitPct: number;
    aiAssessmentLine: string;
  } | null;
  novixaReadiness?: {
    overallPct: number;
    statusLabel: string;
    dimensions: { code: string; name: string; scorePct: number }[];
  } | null;
  gapAnalysis?: {
    narrative: string;
    items: { currentState: string; targetState: string; novixaModule: string; featureHint?: string | null }[];
  } | null;
  roiMetrics?: { label: string; range: string; description: string }[];
  moduleRecommendations?: { moduleCode: string; moduleName: string; stars: number; rationale: string }[];
  transformationRoadmap?: {
    narrative: string;
    phases: { phase: number; title: string; description: string; module: string }[];
  } | null;
  actionPlan?: {
    narrative: string;
    items: {
      title: string;
      body: string;
      priority: string;
      owner: string;
      timeline: string;
      expectedOutcome: string;
    }[];
  } | null;
  crossCategoryInsight?: {
    headline: string;
    analysis: string;
    implications: string[];
  } | null;
  transformationReadiness?: {
    narrative: string;
    bars: { label: string; pct: number }[];
  } | null;
  inactionCascade?: {
    summary: string;
    steps: { horizon: string; outcome: string }[];
  } | null;
  implementationJourney?: {
    summary: string;
    steps: { horizon: string; outcome: string }[];
  } | null;
  whyNovixa?: {
    intro: string;
    rows: { problem: string; module: string; benefit: string; kpiTarget: string }[];
  } | null;
  ownerPack?: OwnerPack | null;
};

export type OwnerPack = {
  overallHeadline: string;
  maturityLabel: string;
  overallScorePct: number;
  strengths: { title: string; body: string; areaCode?: string | null }[];
  pains: {
    title: string;
    businessConsequence: string;
    areaCode?: string | null;
    sourceHint?: string | null;
  }[];
  oneThingFirst: string;
  actions30Days: { title: string; who: string; when: string; doneWhen: string }[];
  pilotHinge: {
    recommendedFocus: string;
    focusOptions: string[];
    howToTalk: string;
  };
  nextStepCta: string;
};

function sessionStorageKey(submissionId: string) {
  return `assessment_session_${submissionId}`;
}

function rememberSession(submissionId: string, sessionToken: string) {
  sessionStorage.setItem(sessionStorageKey(submissionId), sessionToken);
  sessionStorage.setItem('assessment_session', sessionToken);
}

function sessionHeader(submissionId?: string, token?: string) {
  const stored =
    token ??
    (submissionId ? sessionStorage.getItem(sessionStorageKey(submissionId)) : null) ??
    sessionStorage.getItem('assessment_session') ??
    undefined;
  return stored ? { 'X-Assessment-Session': stored } : {};
}

function isPdfBlob(blob: Blob) {
  return blob.size >= 512 && blob.type !== 'application/json';
}

async function readBlobJsonMessage(blob: Blob): Promise<string | null> {
  try {
    const text = await blob.text();
    const parsed = JSON.parse(text) as { message?: string };
    return parsed.message ?? null;
  } catch {
    return null;
  }
}

export function triggerPdfDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();

  // iOS Safari often ignores programmatic download — open in a new tab as fallback.
  const isIos = /iPad|iPhone|iPod/.test(navigator.userAgent);
  if (isIos) {
    window.open(url, '_blank', 'noopener,noreferrer');
  }

  window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

export async function fetchTemplate(code = 'PHARMACY_V1') {
  const { data } = await api.get<AssessmentTemplate>(`/templates/${code}`);
  return data;
}

export async function createSubmission() {
  const partnerCode = getPartnerRef();
  const { data } = await api.post<CreateSubmissionResult>('/submissions', {
    templateCode: 'PHARMACY_V1',
    templateVersion: '1.1',
    source: partnerCode ? 'partner' : 'public_web',
    locale: 'vi-VN',
    partnerCode: partnerCode || undefined,
  });
  rememberSession(data.id, data.sessionToken);
  return data;
}

export async function getSubmission(id: string) {
  const { data } = await api.get<SubmissionDetail>(`/submissions/${id}`, {
    headers: sessionHeader(id),
  });
  return data;
}

export async function saveResponses(
  id: string,
  responses: { questionId: string; optionId?: string; textValue?: string }[],
) {
  const { data } = await api.put(`/submissions/${id}/responses`, { responses }, { headers: sessionHeader(id) });
  return data as { saved: number; answeredRequired: number; totalRequired: number };
}

export async function completeSubmission(id: string) {
  const { data } = await api.post<CompleteResult>(`/submissions/${id}/complete`, {}, { headers: sessionHeader(id) });
  return data;
}

export async function captureLead(
  id: string,
  payload: {
    respondentName: string;
    respondentPhone: string;
    respondentEmail: string;
    respondentOrgName?: string;
    respondentNote?: string;
    consentMarketing: boolean;
    orgScale?: string;
  },
) {
  const { data } = await api.post(`/submissions/${id}/capture-lead`, payload, { headers: sessionHeader(id) });
  return data;
}

export async function fetchReport(id: string) {
  const { data } = await api.get<FullReport>(`/submissions/${id}/report`, { headers: sessionHeader(id) });
  return data;
}

export async function fetchReportPdf(
  id: string,
  kind: 'owner' | 'consulting' | 'executive' | 'appendix' = 'owner',
): Promise<Blob> {
  try {
    const { data, headers } = await api.get<Blob>(`/submissions/${id}/report.pdf`, {
      headers: sessionHeader(id),
      params: { kind },
      responseType: 'blob',
      timeout: 300_000,
    });

    const contentType = String(headers['content-type'] ?? data.type ?? '');
    if (contentType.includes('json')) {
      const message = (await readBlobJsonMessage(data)) ?? 'Không tải được PDF.';
      throw new Error(message);
    }

    if (!isPdfBlob(data)) {
      throw new Error('File PDF không hợp lệ. Vui lòng thử lại.');
    }

    return data;
  } catch (err: unknown) {
    if (axios.isAxiosError(err)) {
      if (err.code === 'ECONNABORTED' || err.message === 'Network Error') {
        throw new Error('Tạo PDF mất quá nhiều thời gian hoặc mất kết nối. Vui lòng thử lại sau 1–2 phút.');
      }

      if (err.response?.data instanceof Blob) {
        const message = await readBlobJsonMessage(err.response.data);
        if (message) throw new Error(message);

        if (err.response.status === 401) {
          throw new Error('Phiên làm việc hết hạn. Vui lòng mở khóa báo cáo lại.');
        }
        if (err.response.status === 429) {
          throw new Error('Quá nhiều yêu cầu. Vui lòng đợi 1 phút rồi thử lại.');
        }
        if (err.response.status === 504) {
          throw new Error('Tạo PDF quá lâu. Vui lòng thử lại sau 1–2 phút.');
        }
      }
    }

    throw err;
  }
}

export function flattenQuestions(template: AssessmentTemplate): AssessmentQuestion[] {
  const items: AssessmentQuestion[] = [];
  for (const cat of template.categories) {
    for (const dim of cat.dimensions) {
      items.push(...dim.questions);
    }
  }
  return items.sort((a, b) => a.sortOrder - b.sortOrder);
}

export function groupQuestionsByCategory(template: AssessmentTemplate) {
  return template.categories.map((cat) => ({
    code: cat.code,
    name: cat.name,
    questions: cat.dimensions.flatMap((d) => d.questions).sort((a, b) => a.sortOrder - b.sortOrder),
  }));
}
