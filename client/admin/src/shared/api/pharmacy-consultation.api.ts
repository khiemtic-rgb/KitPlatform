import { http } from '@/shared/api/http';

export type ConsultationSymptomOption = {
  code: string;
  label: string;
};

export type ConsultationSymptomGroup = {
  code: string;
  label: string;
  items: ConsultationSymptomOption[];
};

export type ConsultationSymptomCatalog = {
  groups: ConsultationSymptomGroup[];
  flat: ConsultationSymptomOption[];
  catalogProfile?: string;
  aliasesByCode?: Record<string, string[]>;
};

export type ConsultationFacts = {
  ageYears?: number | null;
  ageMonths?: number | null;
  gender?: string | null;
  symptoms: string[];
  durationDays?: number | null;
  hasFever?: boolean | null;
  isPregnant?: boolean | null;
  isBreastfeeding?: boolean | null;
  redFlags: string[];
  notes?: string | null;
};

export type ConsultationSafetyFlag = {
  code: string;
  level: string;
  message: string;
};

export type ConsultationPreliminaryHypothesis = {
  code: string;
  fitLevel: string;
  labelVi: string;
  rationaleVi: string;
};

export type ConsultationPreliminaryAssessment = {
  level: 'likely' | 'insufficient' | 'needs_evaluation' | string;
  headlineVi: string;
  summaryVi: string;
  disclaimerVi: string;
  supportingFactLines: string[];
  hypotheses: ConsultationPreliminaryHypothesis[];
  missingInfoHints: string[];
  advisoryVi?: string | null;
};

export type ConsultationSessionSummary = {
  id: string;
  confirmedAt: string;
  status: string;
  salesOrderId?: string | null;
  symptomCodes: string[];
  preliminaryHeadlineVi?: string | null;
  safetyLevel: string;
  naturalLanguageExcerpt?: string | null;
  orderLinked: boolean;
  purchasedProductNames: string[];
};

export type ConsultationCustomerProfileSnapshot = {
  customerId?: string | null;
  fullName?: string | null;
  customerCode?: string | null;
  ageYears?: number | null;
  ageMonths?: number | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  clinicalNotes?: string | null;
  capturedAt?: string | null;
};

export type ConsultationExtractResult = {
  proposedFacts: ConsultationFacts;
  safetyFlags: ConsultationSafetyFlag[];
  safetyLevel: string;
  extractionSource: string;
  aiModel?: string | null;
  geminiConfigured: boolean;
  preliminaryAssessment?: ConsultationPreliminaryAssessment | null;
};

export type ConsultationProductSuggestion = {
  productId: string;
  productCode: string;
  productName: string;
  genericName?: string | null;
  lookupCode: string;
  productUnitId: string;
  unitName: string;
  unitPrice: number;
  stockAvailable: number;
  reason: string;
  matchSource: string;
};

export type ConsultationSuggestResult = {
  blocked: boolean;
  blockReason?: string | null;
  safetyLevel: string;
  safetyFlags: ConsultationSafetyFlag[];
  suggestions: ConsultationProductSuggestion[];
};

export type ConsultationSession = {
  id: string;
  customerId?: string | null;
  salesOrderId?: string | null;
  consultationLevel: number;
  status: string;
  naturalLanguage?: string | null;
  quickSymptoms: string[];
  confirmedFacts: ConsultationFacts;
  safetyFlags: ConsultationSafetyFlag[];
  safetyLevel: string;
  extractionSource: string;
  aiModel?: string | null;
  createdAt: string;
  confirmedAt: string;
  preliminaryAssessment?: ConsultationPreliminaryAssessment | null;
  customerProfileSnapshot?: ConsultationCustomerProfileSnapshot | null;
};

export type PharmacyConsultationAiSettings = {
  geminiApiKeySecretRef?: string | null;
  geminiApiKeyConfigured: boolean;
  textModel: string;
  envFallbackAvailable: boolean;
  contentFallbackAvailable: boolean;
};

function normalizeOption(row: Record<string, unknown>): ConsultationSymptomOption {
  return {
    code: String(row.code ?? row.Code ?? ''),
    label: String(row.label ?? row.Label ?? ''),
  };
}

function normalizeGroup(row: Record<string, unknown>): ConsultationSymptomGroup {
  const itemsRaw = (row.items ?? row.Items ?? []) as Record<string, unknown>[];
  return {
    code: String(row.code ?? row.Code ?? ''),
    label: String(row.label ?? row.Label ?? ''),
    items: itemsRaw.map(normalizeOption),
  };
}

function normalizeFacts(raw: Record<string, unknown>): ConsultationFacts {
  return {
    ageYears: (raw.ageYears ?? raw.AgeYears) as number | null | undefined,
    ageMonths: (raw.ageMonths ?? raw.AgeMonths) as number | null | undefined,
    gender: (raw.gender ?? raw.Gender) as string | null | undefined,
    symptoms: ((raw.symptoms ?? raw.Symptoms) as string[] | undefined) ?? [],
    durationDays: (raw.durationDays ?? raw.DurationDays) as number | null | undefined,
    hasFever: (raw.hasFever ?? raw.HasFever) as boolean | null | undefined,
    isPregnant: (raw.isPregnant ?? raw.IsPregnant) as boolean | null | undefined,
    isBreastfeeding: (raw.isBreastfeeding ?? raw.IsBreastfeeding) as boolean | null | undefined,
    redFlags: ((raw.redFlags ?? raw.RedFlags) as string[] | undefined) ?? [],
    notes: (raw.notes ?? raw.Notes) as string | null | undefined,
  };
}

function normalizeHypothesis(row: Record<string, unknown>): ConsultationPreliminaryHypothesis {
  return {
    code: String(row.code ?? row.Code ?? ''),
    fitLevel: String(row.fitLevel ?? row.FitLevel ?? ''),
    labelVi: String(row.labelVi ?? row.LabelVi ?? ''),
    rationaleVi: String(row.rationaleVi ?? row.RationaleVi ?? ''),
  };
}

function normalizePreliminaryAssessment(
  raw: Record<string, unknown> | null | undefined,
): ConsultationPreliminaryAssessment | null | undefined {
  if (!raw) return null;
  const hypothesesRaw = (raw.hypotheses ?? raw.Hypotheses ?? []) as Record<string, unknown>[];
  const supportingRaw = (raw.supportingFactLines ?? raw.SupportingFactLines ?? []) as string[];
  const missingRaw = (raw.missingInfoHints ?? raw.MissingInfoHints ?? []) as string[];
  return {
    level: String(raw.level ?? raw.Level ?? 'likely'),
    headlineVi: String(raw.headlineVi ?? raw.HeadlineVi ?? ''),
    summaryVi: String(raw.summaryVi ?? raw.SummaryVi ?? ''),
    disclaimerVi: String(raw.disclaimerVi ?? raw.DisclaimerVi ?? ''),
    supportingFactLines: supportingRaw.map(String),
    hypotheses: hypothesesRaw.map(normalizeHypothesis),
    missingInfoHints: missingRaw.map(String),
    advisoryVi: (raw.advisoryVi ?? raw.AdvisoryVi) as string | null | undefined,
  };
}

function normalizeExtract(raw: Record<string, unknown>): ConsultationExtractResult {
  const proposed = (raw.proposedFacts ?? raw.ProposedFacts ?? {}) as Record<string, unknown>;
  const flagsRaw = (raw.safetyFlags ?? raw.SafetyFlags ?? []) as Record<string, unknown>[];
  const assessmentRaw = (raw.preliminaryAssessment ?? raw.PreliminaryAssessment) as
    | Record<string, unknown>
    | null
    | undefined;
  return {
    proposedFacts: normalizeFacts(proposed),
    safetyFlags: flagsRaw.map((f) => ({
      code: String(f.code ?? f.Code ?? ''),
      level: String(f.level ?? f.Level ?? ''),
      message: String(f.message ?? f.Message ?? ''),
    })),
    safetyLevel: String(raw.safetyLevel ?? raw.SafetyLevel ?? 'none'),
    extractionSource: String(raw.extractionSource ?? raw.ExtractionSource ?? 'manual'),
    aiModel: (raw.aiModel ?? raw.AiModel) as string | null | undefined,
    geminiConfigured: Boolean(raw.geminiConfigured ?? raw.GeminiConfigured),
    preliminaryAssessment: normalizePreliminaryAssessment(assessmentRaw),
  };
}

function normalizeProfileSnapshot(
  raw: Record<string, unknown> | null | undefined,
): ConsultationCustomerProfileSnapshot | null | undefined {
  if (!raw) return null;
  return {
    customerId: (raw.customerId ?? raw.CustomerId) as string | null | undefined,
    fullName: (raw.fullName ?? raw.FullName) as string | null | undefined,
    customerCode: (raw.customerCode ?? raw.CustomerCode) as string | null | undefined,
    ageYears: (raw.ageYears ?? raw.AgeYears) as number | null | undefined,
    ageMonths: (raw.ageMonths ?? raw.AgeMonths) as number | null | undefined,
    gender: (raw.gender ?? raw.Gender) as string | null | undefined,
    dateOfBirth: (raw.dateOfBirth ?? raw.DateOfBirth) as string | null | undefined,
    clinicalNotes: (raw.clinicalNotes ?? raw.ClinicalNotes) as string | null | undefined,
    capturedAt: (raw.capturedAt ?? raw.CapturedAt) as string | null | undefined,
  };
}

function normalizeSession(raw: Record<string, unknown>): ConsultationSession {
  const facts = (raw.confirmedFacts ?? raw.ConfirmedFacts ?? {}) as Record<string, unknown>;
  const flagsRaw = (raw.safetyFlags ?? raw.SafetyFlags ?? []) as Record<string, unknown>[];
  const assessmentRaw = (raw.preliminaryAssessment ?? raw.PreliminaryAssessment) as
    | Record<string, unknown>
    | null
    | undefined;
  const profileRaw = (raw.customerProfileSnapshot ?? raw.CustomerProfileSnapshot) as
    | Record<string, unknown>
    | null
    | undefined;
  return {
    id: String(raw.id ?? raw.Id),
    customerId: (raw.customerId ?? raw.CustomerId) as string | null | undefined,
    salesOrderId: (raw.salesOrderId ?? raw.SalesOrderId) as string | null | undefined,
    consultationLevel: Number(raw.consultationLevel ?? raw.ConsultationLevel ?? 1),
    status: String(raw.status ?? raw.Status ?? 'confirmed'),
    naturalLanguage: (raw.naturalLanguage ?? raw.NaturalLanguage) as string | null | undefined,
    quickSymptoms: ((raw.quickSymptoms ?? raw.QuickSymptoms) as string[] | undefined) ?? [],
    confirmedFacts: normalizeFacts(facts),
    safetyFlags: flagsRaw.map((f) => ({
      code: String(f.code ?? f.Code ?? ''),
      level: String(f.level ?? f.Level ?? ''),
      message: String(f.message ?? f.Message ?? ''),
    })),
    safetyLevel: String(raw.safetyLevel ?? raw.SafetyLevel ?? 'none'),
    extractionSource: String(raw.extractionSource ?? raw.ExtractionSource ?? 'manual'),
    aiModel: (raw.aiModel ?? raw.AiModel) as string | null | undefined,
    createdAt: String(raw.createdAt ?? raw.CreatedAt ?? ''),
    confirmedAt: String(raw.confirmedAt ?? raw.ConfirmedAt ?? ''),
    preliminaryAssessment: normalizePreliminaryAssessment(assessmentRaw),
    customerProfileSnapshot: normalizeProfileSnapshot(profileRaw),
  };
}

export async function fetchConsultationSymptomCatalog(): Promise<ConsultationSymptomCatalog> {
  const { data } = await http.get<Record<string, unknown>>('/sales/pos/consultation/symptom-options');
  const groupsRaw = (data.groups ?? data.Groups ?? []) as Record<string, unknown>[];
  const flatRaw = (data.flat ?? data.Flat ?? []) as Record<string, unknown>[];
  const aliasesRaw = (data.aliasesByCode ?? data.AliasesByCode ?? {}) as Record<string, unknown>;
  const aliasesByCode: Record<string, string[]> = {};
  for (const [code, aliases] of Object.entries(aliasesRaw)) {
    aliasesByCode[code] = Array.isArray(aliases) ? aliases.map(String) : [];
  }
  return {
    groups: groupsRaw.map(normalizeGroup),
    flat: flatRaw.map(normalizeOption),
    catalogProfile: String(data.catalogProfile ?? data.CatalogProfile ?? 'novixa_base'),
    aliasesByCode,
  };
}

export async function extractConsultation(payload: {
  naturalLanguage?: string;
  quickSymptoms?: string[];
  confirmedFacts?: ConsultationFacts;
}): Promise<ConsultationExtractResult> {
  const { data } = await http.post<Record<string, unknown>>('/sales/pos/consultation/extract', payload, {
    timeout: 25_000,
  });
  return normalizeExtract(data ?? {});
}

function normalizeSuggestion(row: Record<string, unknown>): ConsultationProductSuggestion {
  return {
    productId: String(row.productId ?? row.ProductId ?? ''),
    productCode: String(row.productCode ?? row.ProductCode ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    genericName: (row.genericName ?? row.GenericName) as string | null | undefined,
    lookupCode: String(row.lookupCode ?? row.LookupCode ?? row.productCode ?? row.ProductCode ?? ''),
    productUnitId: String(row.productUnitId ?? row.ProductUnitId ?? ''),
    unitName: String(row.unitName ?? row.UnitName ?? ''),
    unitPrice: Number(row.unitPrice ?? row.UnitPrice ?? 0),
    stockAvailable: Number(row.stockAvailable ?? row.StockAvailable ?? 0),
    reason: String(row.reason ?? row.Reason ?? ''),
    matchSource: String(row.matchSource ?? row.MatchSource ?? 'rule'),
  };
}

function normalizeSuggest(raw: Record<string, unknown>): ConsultationSuggestResult {
  const flagsRaw = (raw.safetyFlags ?? raw.SafetyFlags ?? []) as Record<string, unknown>[];
  const suggestionsRaw = (raw.suggestions ?? raw.Suggestions ?? []) as Record<string, unknown>[];
  return {
    blocked: Boolean(raw.blocked ?? raw.Blocked),
    blockReason: (raw.blockReason ?? raw.BlockReason) as string | null | undefined,
    safetyLevel: String(raw.safetyLevel ?? raw.SafetyLevel ?? 'none'),
    safetyFlags: flagsRaw.map((f) => ({
      code: String(f.code ?? f.Code ?? ''),
      level: String(f.level ?? f.Level ?? ''),
      message: String(f.message ?? f.Message ?? ''),
    })),
    suggestions: suggestionsRaw.map(normalizeSuggestion),
  };
}

export async function suggestConsultation(payload: {
  confirmedFacts: ConsultationFacts;
  warehouseId: string;
  limit?: number;
}): Promise<ConsultationSuggestResult> {
  const { data } = await http.post<Record<string, unknown>>('/sales/pos/consultation/suggest', payload);
  return normalizeSuggest(data ?? {});
}

export async function fetchRecentConsultationSessions(
  customerId: string,
  limit = 5,
): Promise<ConsultationSessionSummary[]> {
  const { data } = await http.get<Record<string, unknown>[]>('/sales/pos/consultation/sessions/recent', {
    params: { customerId, limit },
  });
  return (data ?? []).map((row) => ({
    id: String(row.id ?? row.Id),
    confirmedAt: String(row.confirmedAt ?? row.ConfirmedAt ?? ''),
    status: String(row.status ?? row.Status ?? ''),
    salesOrderId: (row.salesOrderId ?? row.SalesOrderId) as string | null | undefined,
    symptomCodes: ((row.symptomCodes ?? row.SymptomCodes) as string[] | undefined) ?? [],
    preliminaryHeadlineVi: (row.preliminaryHeadlineVi ?? row.PreliminaryHeadlineVi) as string | null | undefined,
    safetyLevel: String(row.safetyLevel ?? row.SafetyLevel ?? 'none'),
    naturalLanguageExcerpt: (row.naturalLanguageExcerpt ?? row.NaturalLanguageExcerpt) as string | null | undefined,
    orderLinked: Boolean(row.orderLinked ?? row.OrderLinked),
    purchasedProductNames: ((row.purchasedProductNames ?? row.PurchasedProductNames) as string[] | undefined) ?? [],
  }));
}

export async function confirmConsultation(payload: {
  customerId?: string;
  consultationLevel?: number;
  naturalLanguage?: string;
  quickSymptoms?: string[];
  confirmedFacts: ConsultationFacts;
  extractionSource?: string;
  aiModel?: string | null;
  preliminaryAssessment?: ConsultationPreliminaryAssessment | null;
  customerProfileSnapshot?: ConsultationCustomerProfileSnapshot | null;
}): Promise<ConsultationSession> {
  const { data } = await http.post<Record<string, unknown>>('/sales/pos/consultation/sessions', payload);
  return normalizeSession(data ?? {});
}

export async function linkConsultationOrder(sessionId: string, salesOrderId: string): Promise<ConsultationSession> {
  const { data } = await http.post<Record<string, unknown>>(
    `/sales/pos/consultation/sessions/${sessionId}/link-order`,
    { salesOrderId },
  );
  return normalizeSession(data ?? {});
}

export async function fetchPharmacyConsultationAiSettings(): Promise<PharmacyConsultationAiSettings> {
  const { data } = await http.get<Record<string, unknown>>('/sales/settings/consultation-ai');
  return {
    geminiApiKeySecretRef: (data.geminiApiKeySecretRef ?? data.GeminiApiKeySecretRef) as string | null | undefined,
    geminiApiKeyConfigured: Boolean(data.geminiApiKeyConfigured ?? data.GeminiApiKeyConfigured),
    textModel: String(data.textModel ?? data.TextModel ?? 'gemini-2.5-flash-lite'),
    envFallbackAvailable: Boolean(data.envFallbackAvailable ?? data.EnvFallbackAvailable),
    contentFallbackAvailable: Boolean(data.contentFallbackAvailable ?? data.ContentFallbackAvailable),
  };
}

export async function updatePharmacyConsultationAiSettings(payload: {
  geminiApiKeySecretRef?: string;
  geminiApiKey?: string | null;
  textModel?: string;
}): Promise<PharmacyConsultationAiSettings> {
  const { data } = await http.put<Record<string, unknown>>('/sales/settings/consultation-ai', payload);
  return {
    geminiApiKeySecretRef: (data.geminiApiKeySecretRef ?? data.GeminiApiKeySecretRef) as string | null | undefined,
    geminiApiKeyConfigured: Boolean(data.geminiApiKeyConfigured ?? data.GeminiApiKeyConfigured),
    textModel: String(data.textModel ?? data.TextModel ?? payload.textModel ?? 'gemini-2.5-flash-lite'),
    envFallbackAvailable: Boolean(data.envFallbackAvailable ?? data.EnvFallbackAvailable),
    contentFallbackAvailable: Boolean(data.contentFallbackAvailable ?? data.ContentFallbackAvailable),
  };
}

export function consultationSafetyAlertType(level: string): 'success' | 'info' | 'warning' | 'error' {
  switch (level) {
    case 'stop_sale':
    case 'refer_medical':
      return 'error';
    case 'refer_pharmacist':
      return 'warning';
    case 'caution':
      return 'info';
    default:
      return 'success';
  }
}

export function consultationSafetyLevelLabel(level: string): string {
  switch (level) {
    case 'stop_sale':
      return 'Dừng bán — cấp cứu';
    case 'refer_medical':
      return 'Chuyển BS / cấp cứu';
    case 'refer_pharmacist':
      return 'Chuyển dược sĩ';
    case 'caution':
      return 'Cần lưu ý';
    default:
      return 'Chưa phát hiện dấu hiệu cảnh báo';
  }
}

export type ConsultationQuestion = {
  code: string;
  questionVi: string;
  answerType: string;
  required: boolean;
  priority: number;
};

export async function fetchConsultationQuestions(symptomCodes: string[]): Promise<ConsultationQuestion[]> {
  if (symptomCodes.length === 0) return [];
  const { data } = await http.get<Record<string, unknown>[]>('/sales/pos/consultation/questions', {
    params: { symptoms: symptomCodes.join(',') },
  });
  return (data ?? []).map((row) => ({
    code: String(row.code ?? row.Code ?? ''),
    questionVi: String(row.questionVi ?? row.QuestionVi ?? ''),
    answerType: String(row.answerType ?? row.AnswerType ?? 'text'),
    required: Boolean(row.required ?? row.Required ?? false),
    priority: Number(row.priority ?? row.Priority ?? 0),
  }));
}
