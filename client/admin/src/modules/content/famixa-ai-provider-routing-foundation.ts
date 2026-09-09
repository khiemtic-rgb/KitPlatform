/** FAMIXA Provider Routing Foundation V1 — one Series selection point. Not a Router. 0 HTTP. */

import { quoteFamixaProviderCost } from './famixa-ai-provider-cost';
import {
  FAMIXA_PROVIDER_CATALOG,
  describeFamixaProvider,
  mapFamixaProviderPolicy,
  normalizeFamixaLipsyncModel,
  type FamixaCostKind,
  type FamixaProviderCapability,
  type FamixaProviderPolicy,
  type FamixaProviderResolveResult,
  type FamixaProviderSelection,
} from './famixa-ai-provider-orchestration';

export const FAMIXA_ROUTING_FOUNDATION_ID = 'FAMIXA_AI_PROVIDER_ROUTING_FOUNDATION_V1';
export const FAMIXA_ROUTER_ID = 'FAMIXA_AI_PROVIDER_ROUTER_V1';

export const FAMIXA_ROUTER_OUTCOME = {
  EXPLICIT: 'EXPLICIT',
  ROUTED: 'ROUTED',
  NO_PROVIDER: 'NO_PROVIDER',
  OVER_BUDGET: 'OVER_BUDGET',
  NO_SAFE_CANDIDATE: 'NO_SAFE_CANDIDATE',
} as const;

export const FAMIXA_REJECT_CODE = {
  NOT_CONFIGURED: 'NOT_CONFIGURED',
  UNAVAILABLE: 'UNAVAILABLE',
  UNKNOWN_AVAILABILITY: 'UNKNOWN_AVAILABILITY',
  OVER_BUDGET: 'OVER_BUDGET',
  UNKNOWN_COST: 'UNKNOWN_COST',
  CAPABILITY_MISMATCH: 'CAPABILITY_MISMATCH',
  MODEL_UNAVAILABLE: 'MODEL_UNAVAILABLE',
  POLICY_MISMATCH: 'POLICY_MISMATCH',
  ELIGIBLE: 'ELIGIBLE',
} as const;

/** Stable order per capability. Not a quality ranking. */
export const FAMIXA_ROUTER_PRIORITY = {
  PICTURE: ['gemini'],
  MOTION: ['runway', 'wan'],
  VOICE: ['elevenlabs'],
  LIPSYNC: ['fal'],
} as const;

export const FAMIXA_PROVIDER_EVIDENCE = { quality: 'UNKNOWN', speed: 'UNKNOWN' } as const;

export type FamixaProviderSelectionMode = 'EXPLICIT' | 'ROUTED' | 'POLICY' | 'LEGACY';
export type FamixaProviderAvailabilityState =
  | 'CONFIGURED'
  | 'NOT_CONFIGURED'
  | 'DISABLED'
  | 'UNKNOWN'
  | 'AVAILABLE'
  | 'UNAVAILABLE';
export type FamixaBudgetFit = 'WITHIN_BUDGET' | 'OVER_BUDGET' | 'UNKNOWN';

export type FamixaProviderSelectionRequirements = {
  capability: FamixaProviderCapability;
  maxCost?: number | null;
  currency?: string | null;
  qualityPreference?: string | null;
  speedPreference?: string | null;
  availabilityRequirement?: string | null;
  language?: string | null;
  durationSec?: number | null;
  outputConstraints?: string | null;
  tenantOrg?: string | null;
  planTier?: string | null;
  policy?: FamixaProviderPolicy | null;
  explicitProviderId?: string | null;
  explicitModelId?: string | null;
  engine?: string | null;
  lipsyncModel?: string | null;
  voiceProvider?: string | null;
};

export type FamixaProviderSelectionDecision = {
  decisionId: string;
  capability: FamixaProviderCapability;
  providerId: string;
  modelId?: string;
  policy: FamixaProviderPolicy;
  reason: string;
  estimatedCost: number | null;
  costKind: FamixaCostKind;
  currency?: string;
  billingBasis: string;
  billedQuantity: number;
  selectionMode: FamixaProviderSelectionMode;
  outcome?: string;
  candidateCount?: number;
  rejectedCount?: number;
  primaryRejectionReason?: string | null;
};

export type FamixaProviderRouterContext = {
  configured?: FamixaProviderConfiguredSnapshot | null;
  unavailableProviderIds?: string[];
};

export type FamixaProviderCandidateEvaluation = {
  providerId: string;
  modelId?: string;
  eligible: boolean;
  reasonCode: string;
  reason: string;
};

export type FamixaProviderRoutingDiagnostic = {
  candidateCount: number;
  rejectedCount: number;
  primaryRejectionReason?: string | null;
  evaluations: FamixaProviderCandidateEvaluation[];
};

export type FamixaProviderSelectionResult = {
  outcome: string;
  decision?: FamixaProviderSelectionDecision;
  diagnostic: FamixaProviderRoutingDiagnostic;
};

export type FamixaProviderAvailabilityResult = {
  providerId: string;
  modelId?: string;
  state: FamixaProviderAvailabilityState;
  reason: string;
  configuration: FamixaProviderAvailabilityState;
  isAvailable: boolean;
};

export type FamixaProviderConfiguredSnapshot = {
  runwayConfigured: boolean;
  falConfigured: boolean;
  elevenLabsConfigured: boolean;
  geminiConfigured: boolean;
};

export type FamixaProviderPolicyPreference = {
  costPreference: string;
  qualityPreference: string;
  speedPreference: string;
};

export function famixaProviderPolicyPreferenceOf(policy: FamixaProviderPolicy): FamixaProviderPolicyPreference {
  if (policy === 'ECONOMY') return { costPreference: 'HIGH', qualityPreference: 'NORMAL', speedPreference: 'NORMAL' };
  if (policy === 'PREMIUM') return { costPreference: 'NORMAL', qualityPreference: 'HIGH', speedPreference: 'NORMAL' };
  return { costPreference: 'NORMAL', qualityPreference: 'NORMAL', speedPreference: 'NORMAL' };
}

/** null amount is UNKNOWN — never within budget, never zero. */
export function isKnownNumericCostWithinBudget(
  amount: number | null | undefined,
  maxCost: number | null | undefined,
): FamixaBudgetFit {
  if (amount == null || maxCost == null) return 'UNKNOWN';
  return amount <= maxCost ? 'WITHIN_BUDGET' : 'OVER_BUDGET';
}

export function classifyFamixaProviderAvailability(
  providerId: string,
  snapshot: FamixaProviderConfiguredSnapshot,
  modelId?: string,
): FamixaProviderAvailabilityResult {
  const id = (providerId || '').trim().toLowerCase();
  const configured =
    id === 'runway'
      ? snapshot.runwayConfigured
      : id === 'wan' || id === 'fal'
        ? snapshot.falConfigured
        : id === 'elevenlabs'
          ? snapshot.elevenLabsConfigured
          : id === 'gemini'
            ? snapshot.geminiConfigured
            : false;
  if (!configured) {
    return {
      providerId: id,
      modelId,
      state: 'NOT_CONFIGURED',
      reason: 'no key — NOT_CONFIGURED',
      configuration: 'NOT_CONFIGURED',
      isAvailable: false,
    };
  }
  return {
    providerId: id,
    modelId,
    state: 'UNKNOWN',
    reason: 'key configured; health unknown — not AVAILABLE',
    configuration: 'CONFIGURED',
    isAvailable: false,
  };
}

/** Legacy capability map used only inside Selection Foundation. */
export function resolveFamixaProviderLegacyMap(
  capability: FamixaProviderCapability,
  selection?: FamixaProviderSelection,
): FamixaProviderResolveResult {
  const explicit = (selection?.explicitProviderId || '').trim();
  if (explicit) {
    const desc = describeFamixaProvider(explicit);
    if (!desc.capabilities.includes(capability)) {
      throw Object.assign(new Error(`UNSUPPORTED: ${explicit} không có ${capability}`), {
        code: 'UNSUPPORTED',
      });
    }
    return {
      providerId: desc.providerId,
      modelId: capability === 'LIPSYNC' ? normalizeFamixaLipsyncModel(selection?.lipsyncModel) : desc.models[0],
    };
  }
  if (capability === 'MOTION') {
    const engine = (selection?.engine || '').trim();
    if (engine.toLowerCase() === 'wan') return { providerId: 'wan', modelId: 'wan-2.1' };
    if (engine) return { providerId: 'runway', modelId: 'gen4_turbo' };
  }
  if (capability === 'LIPSYNC' && (selection?.lipsyncModel || '').trim()) {
    return { providerId: 'fal', modelId: normalizeFamixaLipsyncModel(selection?.lipsyncModel) };
  }
  if (capability === 'VOICE') {
    const v = (selection?.voiceProvider || '').trim().toLowerCase();
    if (v === 'f5') {
      throw Object.assign(new Error('UNSUPPORTED: F5-TTS chưa có runtime.'), { code: 'UNSUPPORTED' });
    }
  }
  return mapFamixaProviderPolicy(capability, selection?.policy || 'STANDARD');
}

export function validateFamixaRuntimeMaxCost(maxCost?: number | null) {
  if (maxCost == null) return null;
  if (maxCost < 0) return 'MAXCOST_INVALID';
  return null;
}

/** Request → SelectionRequirements. Does not read monthly budget or localStorage. */
export function seriesStartToSelectionRequirements(request: {
  maxCost?: number | null;
  currency?: string | null;
  seconds?: number | null;
  engine?: string | null;
  explicitProviderId?: string | null;
}): FamixaProviderSelectionRequirements {
  const invalid = validateFamixaRuntimeMaxCost(request.maxCost);
  if (invalid) {
    throw Object.assign(new Error('MAXCOST_INVALID: MaxCost must be >= 0.'), { code: invalid });
  }
  return {
    capability: 'MOTION',
    maxCost: request.maxCost ?? null,
    currency: request.currency ?? null,
    durationSec: request.seconds ?? null,
    engine: request.engine,
    explicitProviderId: request.explicitProviderId,
  };
}

export function requirementsFromSelection(
  capability: FamixaProviderCapability,
  selection?: FamixaProviderSelection,
  maxCost?: number | null,
  durationSec?: number | null,
): FamixaProviderSelectionRequirements {
  return {
    capability,
    maxCost,
    policy: selection?.policy,
    explicitProviderId: selection?.explicitProviderId,
    engine: selection?.engine,
    lipsyncModel: selection?.lipsyncModel,
    voiceProvider: selection?.voiceProvider,
    durationSec,
  };
}

export function defaultRouterModel(capability: FamixaProviderCapability, providerId: string) {
  const id = (providerId || '').trim().toLowerCase();
  if (capability === 'MOTION' && id === 'wan') return 'wan-2.1';
  if (capability === 'MOTION') return 'gen4_turbo';
  if (capability === 'VOICE') return 'eleven_v3';
  if (capability === 'LIPSYNC') return '1.9';
  if (capability === 'PICTURE') return 'gemini-2.5-flash-image';
  return undefined;
}

export function routerPriorityIndex(capability: FamixaProviderCapability, providerId: string) {
  const order = FAMIXA_ROUTER_PRIORITY[capability] as readonly string[];
  const i = order.indexOf((providerId || '').trim().toLowerCase());
  return i < 0 ? 1000 : i;
}

export function evaluateFamixaProviderCandidate(
  req: FamixaProviderSelectionRequirements,
  providerId: string,
  ctx: FamixaProviderRouterContext = {},
): FamixaProviderCandidateEvaluation {
  const desc = FAMIXA_PROVIDER_CATALOG.find((d) => d.providerId === providerId);
  if (!desc) {
    return { providerId, eligible: false, reasonCode: FAMIXA_REJECT_CODE.CAPABILITY_MISMATCH, reason: `${providerId} rejected: unknown provider.` };
  }
  if (!desc.capabilities.includes(req.capability)) {
    return {
      providerId: desc.providerId,
      eligible: false,
      reasonCode: FAMIXA_REJECT_CODE.CAPABILITY_MISMATCH,
      reason: `${desc.displayName} rejected: capability mismatch.`,
    };
  }
  const model =
    req.capability === 'LIPSYNC'
      ? normalizeFamixaLipsyncModel(req.explicitModelId || req.lipsyncModel || undefined)
      : (req.explicitModelId || '').trim() || defaultRouterModel(req.capability, desc.providerId);
  if (model && desc.models.length > 0 && !desc.models.some((m) => m.toLowerCase() === model.toLowerCase())) {
    return {
      providerId: desc.providerId,
      modelId: model,
      eligible: false,
      reasonCode: FAMIXA_REJECT_CODE.MODEL_UNAVAILABLE,
      reason: `${desc.displayName} rejected: model unavailable.`,
    };
  }
  const unavailable = (ctx.unavailableProviderIds || []).map((x) => x.toLowerCase());
  if (unavailable.includes(desc.providerId)) {
    return {
      providerId: desc.providerId,
      modelId: model,
      eligible: false,
      reasonCode: FAMIXA_REJECT_CODE.UNAVAILABLE,
      reason: `${desc.displayName} rejected: UNAVAILABLE.`,
    };
  }
  if (ctx.configured) {
    const avail = classifyFamixaProviderAvailability(desc.providerId, ctx.configured, model);
    if (avail.state === 'NOT_CONFIGURED' || avail.configuration === 'NOT_CONFIGURED') {
      return {
        providerId: desc.providerId,
        modelId: model,
        eligible: false,
        reasonCode: FAMIXA_REJECT_CODE.NOT_CONFIGURED,
        reason: `${desc.displayName} rejected: NOT_CONFIGURED.`,
      };
    }
  }
  const quote = quoteFamixaProviderCost({ providerId: desc.providerId, modelId: model, quantity: req.durationSec ?? undefined });
  if (req.maxCost != null) {
    if (quote.estimatedAmount == null) {
      return {
        providerId: desc.providerId,
        modelId: model,
        eligible: false,
        reasonCode: FAMIXA_REJECT_CODE.UNKNOWN_COST,
        reason: `${desc.displayName} rejected: billed amount unknown; cannot prove budget fit.`,
      };
    }
    if (quote.estimatedAmount > req.maxCost) {
      return {
        providerId: desc.providerId,
        modelId: model,
        eligible: false,
        reasonCode: FAMIXA_REJECT_CODE.OVER_BUDGET,
        reason: `${desc.displayName} rejected: OVER_BUDGET.`,
      };
    }
  }
  const availNote = ctx.configured ? 'CONFIGURED' : 'UNKNOWN_AVAILABILITY';
  const budgetNote = req.maxCost == null ? 'NO_MAX_COST' : quote.estimatedAmount != null ? 'WITHIN_BUDGET' : 'UNKNOWN_COST';
  return {
    providerId: desc.providerId,
    modelId: model,
    eligible: true,
    reasonCode: FAMIXA_REJECT_CODE.ELIGIBLE,
    reason: `${desc.displayName} eligible (${availNote}, ${budgetNote}). Quality evidence unavailable. Speed evidence unavailable.`,
  };
}

function primaryReject(rejected: FamixaProviderCandidateEvaluation[]) {
  const rank = [
    FAMIXA_REJECT_CODE.UNKNOWN_COST,
    FAMIXA_REJECT_CODE.OVER_BUDGET,
    FAMIXA_REJECT_CODE.NOT_CONFIGURED,
    FAMIXA_REJECT_CODE.UNAVAILABLE,
    FAMIXA_REJECT_CODE.MODEL_UNAVAILABLE,
    FAMIXA_REJECT_CODE.POLICY_MISMATCH,
  ];
  for (const code of rank) {
    if (rejected.some((r) => r.reasonCode === code)) return code;
  }
  return rejected[0]?.reasonCode;
}

function outcomeWhenEmpty(rejected: FamixaProviderCandidateEvaluation[]) {
  const codes = new Set(rejected.map((r) => r.reasonCode));
  const over = codes.has(FAMIXA_REJECT_CODE.OVER_BUDGET);
  const unknownCost = codes.has(FAMIXA_REJECT_CODE.UNKNOWN_COST);
  if (over && unknownCost) return FAMIXA_ROUTER_OUTCOME.NO_SAFE_CANDIDATE;
  if (unknownCost) return FAMIXA_ROUTER_OUTCOME.NO_SAFE_CANDIDATE;
  if (over) return FAMIXA_ROUTER_OUTCOME.OVER_BUDGET;
  return FAMIXA_ROUTER_OUTCOME.NO_PROVIDER;
}

function engineRank(req: FamixaProviderSelectionRequirements, providerId: string) {
  if (req.capability !== 'MOTION' || !(req.engine || '').trim()) return 0;
  const wan = (req.engine || '').trim().toLowerCase() === 'wan';
  if (wan) return providerId === 'wan' ? 0 : 1;
  return providerId === 'runway' ? 0 : 1;
}

function costRank(
  req: FamixaProviderSelectionRequirements,
  policy: FamixaProviderPolicy,
  eligible: FamixaProviderCandidateEvaluation[],
  current: FamixaProviderCandidateEvaluation,
) {
  if (policy !== 'ECONOMY') return 0;
  const quotes = eligible.map((e) =>
    quoteFamixaProviderCost({ providerId: e.providerId, modelId: e.modelId, quantity: req.durationSec ?? undefined }),
  );
  if (quotes.some((q) => q.estimatedAmount == null)) return 0;
  return quoteFamixaProviderCost({
    providerId: current.providerId,
    modelId: current.modelId,
    quantity: req.durationSec ?? undefined,
  }).estimatedAmount ?? 0;
}

function selectedReason(req: FamixaProviderSelectionRequirements, ctx: FamixaProviderRouterContext, providerId: string, amount: number | null) {
  const name = FAMIXA_PROVIDER_CATALOG.find((d) => d.providerId === providerId)?.displayName || providerId;
  const config = ctx.configured ? 'configured' : 'availability UNKNOWN';
  const budget = req.maxCost == null ? 'no maxCost requirement' : amount != null ? 'known estimate within budget' : 'no proven budget comparison';
  return `${name} selected: ${config}, capability compatible, ${budget}; quality and speed evidence unavailable.`;
}

export function routeFamixaProvider(
  req: FamixaProviderSelectionRequirements,
  ctx: FamixaProviderRouterContext = {},
): FamixaProviderSelectionResult {
  const policy = req.policy || 'STANDARD';
  if ((req.explicitProviderId || '').trim()) {
    const resolved = resolveFamixaProviderLegacyMap(req.capability, {
      explicitProviderId: req.explicitProviderId || undefined,
      lipsyncModel: req.explicitModelId || req.lipsyncModel || undefined,
      voiceProvider: req.voiceProvider || undefined,
      policy,
    });
    const decision = decide(
      req,
      resolved.providerId,
      resolved.modelId,
      policy,
      'EXPLICIT',
      'explicit provider selected by Director',
      FAMIXA_ROUTER_OUTCOME.EXPLICIT,
    );
    return { outcome: FAMIXA_ROUTER_OUTCOME.EXPLICIT, decision, diagnostic: { candidateCount: 0, rejectedCount: 0, evaluations: [] } };
  }

  const evaluations = [...FAMIXA_PROVIDER_CATALOG]
    .sort((a, b) => a.providerId.localeCompare(b.providerId))
    .map((d) => evaluateFamixaProviderCandidate(req, d.providerId, ctx));
  const considered = evaluations.filter((e) => e.reasonCode !== FAMIXA_REJECT_CODE.CAPABILITY_MISMATCH);
  const eligible = considered.filter((e) => e.eligible);
  const rejected = considered.filter((e) => !e.eligible);
  const diagnostic: FamixaProviderRoutingDiagnostic = {
    candidateCount: considered.length,
    rejectedCount: rejected.length,
    primaryRejectionReason: primaryReject(rejected),
    evaluations,
  };
  if (eligible.length === 0) {
    return { outcome: outcomeWhenEmpty(rejected), diagnostic };
  }
  const chosen = [...eligible].sort((a, b) => {
    const e = engineRank(req, a.providerId) - engineRank(req, b.providerId);
    if (e) return e;
    const c = costRank(req, policy, eligible, a) - costRank(req, policy, eligible, b);
    if (c) return c;
    const p = routerPriorityIndex(req.capability, a.providerId) - routerPriorityIndex(req.capability, b.providerId);
    if (p) return p;
    const id = a.providerId.localeCompare(b.providerId);
    if (id) return id;
    return (a.modelId || '').localeCompare(b.modelId || '');
  })[0]!;
  const quote = quoteFamixaProviderCost({
    providerId: chosen.providerId,
    modelId: chosen.modelId,
    quantity: req.durationSec ?? undefined,
  });
  const decision = decide(
    req,
    chosen.providerId,
    chosen.modelId,
    policy,
    'ROUTED',
    selectedReason(req, ctx, chosen.providerId, quote.estimatedAmount ?? null),
    FAMIXA_ROUTER_OUTCOME.ROUTED,
    diagnostic,
  );
  return { outcome: FAMIXA_ROUTER_OUTCOME.ROUTED, decision, diagnostic };
}

/** Canonical Series selection entry point. Router V1 after explicit pin. */
export function selectFamixaProvider(
  req: FamixaProviderSelectionRequirements,
  ctx: FamixaProviderRouterContext = {},
): FamixaProviderSelectionDecision {
  const result = routeFamixaProvider(req, ctx);
  if (result.decision) return result.decision;
  throw Object.assign(new Error(`${result.outcome}: ${result.diagnostic.primaryRejectionReason || 'no eligible provider'}.`), {
    outcome: result.outcome,
    diagnostic: result.diagnostic,
  });
}

function decide(
  req: FamixaProviderSelectionRequirements,
  providerId: string,
  modelId: string | undefined,
  policy: FamixaProviderPolicy,
  mode: FamixaProviderSelectionMode,
  reason: string,
  outcome?: string,
  diagnostic?: FamixaProviderRoutingDiagnostic,
): FamixaProviderSelectionDecision {
  const quote = quoteFamixaProviderCost({
    providerId,
    modelId,
    quantity: req.durationSec ?? undefined,
  });
  return {
    decisionId: crypto.randomUUID().replace(/-/g, ''),
    capability: req.capability,
    providerId,
    modelId,
    policy,
    reason,
    estimatedCost: quote.estimatedAmount ?? null,
    costKind: quote.kind,
    currency: quote.currency,
    billingBasis: quote.billingBasis,
    billedQuantity: quote.billedQuantity,
    selectionMode: mode,
    outcome,
    candidateCount: diagnostic?.candidateCount ?? 0,
    rejectedCount: diagnostic?.rejectedCount ?? 0,
    primaryRejectionReason: diagnostic?.primaryRejectionReason ?? null,
  };
}
