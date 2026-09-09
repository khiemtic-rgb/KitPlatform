/** FAMIXA Cost Single Source of Truth V1. Estimate only. No vendor ledger. No HTTP. */

export const FAMIXA_COST_SOT_ID = 'FAMIXA_AI_PROVIDER_COST_SINGLE_SOURCE_OF_TRUTH_V1';

export type FamixaCostKindV1 = 'ESTIMATE' | 'UNKNOWN';
export type FamixaCostCurrency = 'credit' | 'usd' | 'fal';
export type FamixaCostBillingUnit = 'second' | 'clip' | 'unknown';
export type FamixaCostBillingBasis =
  | 'RUNWAY_CREDIT_PER_SEC'
  | 'FAL_BILLED_ESTIMATE'
  | 'FAL_USD_PER_MIN'
  | 'FAL_LATENTSYNC_CLIP'
  | 'UNKNOWN';

export type FamixaCostQuoteInput = {
  providerId: string;
  modelId?: string;
  /** Provider / performance billing quantity. Editorial duration is ignored. */
  quantity?: number;
  billingUnit?: FamixaCostBillingUnit;
  /** Never used for I2V generation cost. */
  editorialDurationSec?: number;
};

export type FamixaCostQuote = {
  providerId: string;
  modelId?: string;
  estimatedAmount?: number;
  currency?: FamixaCostCurrency;
  costKind: FamixaCostKindV1;
  billingBasis: FamixaCostBillingBasis;
  billingUnit: FamixaCostBillingUnit;
  billedQuantity: number;
  minimumBillableQuantity: number;
  /** Phase 2 compatibility: FAL_BILLED_ESTIMATE is estimate-class, never ACTUAL. */
  kind: 'ESTIMATE' | 'UNKNOWN' | 'FAL_BILLED_ESTIMATE';
};

export const FAMIXA_COST_CATALOG = {
  runway: {
    providerId: 'runway',
    modelId: 'gen4_turbo',
    creditsPerSecond: 5,
    currency: 'credit' as const,
    costKind: 'ESTIMATE' as const,
    billingBasis: 'RUNWAY_CREDIT_PER_SEC' as const,
    billingUnit: 'second' as const,
    minimumBillableQuantity: 5,
    mapBilledSeconds: (quantity?: number) => (quantity != null && quantity >= 8 ? 10 : 5),
  },
  wan: {
    providerId: 'wan',
    modelId: 'wan-2.1',
    currency: 'fal' as const,
    costKind: 'ESTIMATE' as const,
    billingBasis: 'FAL_BILLED_ESTIMATE' as const,
    billingUnit: 'second' as const,
    minimumBillableQuantity: 5,
  },
  fal19: {
    providerId: 'fal',
    modelId: '1.9',
    usdPerMinute: 0.7,
    currency: 'usd' as const,
    costKind: 'ESTIMATE' as const,
    billingBasis: 'FAL_USD_PER_MIN' as const,
    billingUnit: 'second' as const,
    minimumBillableQuantity: 5,
  },
  falV3: {
    providerId: 'fal',
    modelId: 'v3',
    usdPerMinute: 8,
    currency: 'usd' as const,
    costKind: 'ESTIMATE' as const,
    billingBasis: 'FAL_USD_PER_MIN' as const,
    billingUnit: 'second' as const,
    minimumBillableQuantity: 5,
  },
  falLs: {
    providerId: 'fal',
    modelId: 'ls',
    usdAtOrBelow40: 0.2,
    usdPerSecondOver40: 0.005,
    currency: 'usd' as const,
    costKind: 'ESTIMATE' as const,
    billingBasis: 'FAL_LATENTSYNC_CLIP' as const,
    billingUnit: 'clip' as const,
    minimumBillableQuantity: 5,
  },
  gemini: {
    providerId: 'gemini',
    modelId: 'gemini-2.5-flash-image',
    costKind: 'UNKNOWN' as const,
    billingBasis: 'UNKNOWN' as const,
    billingUnit: 'unknown' as const,
    minimumBillableQuantity: 0,
  },
  elevenlabs: {
    providerId: 'elevenlabs',
    modelId: 'eleven_v3',
    costKind: 'UNKNOWN' as const,
    billingBasis: 'UNKNOWN' as const,
    billingUnit: 'unknown' as const,
    minimumBillableQuantity: 0,
  },
} as const;

export const FAMIXA_COST_COPY = {
  runwayI2v: '5s = 25 cr · 10s = 50 cr',
  falSettings:
    'Khớp môi: Chuẩn v3 (~$8/phút) · Vừa 1.9 (~$0.70/phút) · Rẻ LatentSync (~$0.20/clip). Wan dùng chung.',
  wanEstimate: 'Fal billed estimate · Wan I2V (không free)',
  wanDurationHint: 'Wan không làm 10s — tối đa ~6s.',
  kitCreditEstimate: 'KIT_CREDIT_ESTIMATE',
  inferredEstimate: 'INFERRED_ESTIMATE',
} as const;

export const FAL_LIPSYNC_USD_PER_MIN_SOT = {
  '1.9': FAMIXA_COST_CATALOG.fal19.usdPerMinute,
  v3: FAMIXA_COST_CATALOG.falV3.usdPerMinute,
} as const;

function money2(n: number) {
  return Math.round(n * 100) / 100;
}

function normalizeCostLipsyncModel(raw?: string): '1.9' | 'v3' | 'ls' {
  if (raw === 'v3') return 'v3';
  if (raw === 'ls' || raw === 'latentsync') return 'ls';
  return '1.9';
}

function unknownQuote(providerId: string, modelId?: string): FamixaCostQuote {
  return {
    providerId,
    modelId,
    costKind: 'UNKNOWN',
    billingBasis: 'UNKNOWN',
    billingUnit: 'unknown',
    billedQuantity: 0,
    minimumBillableQuantity: 0,
    kind: 'UNKNOWN',
  };
}

/** Provider generation cost. Editorial duration is accepted and ignored. */
export function quoteFamixaProviderCost(input: FamixaCostQuoteInput): FamixaCostQuote {
  const providerId = (input.providerId || '').trim().toLowerCase();
  const modelId = (input.modelId || '').trim();
  void input.editorialDurationSec;
  void input.billingUnit;

  if (providerId === 'runway') {
    const row = FAMIXA_COST_CATALOG.runway;
    const billed = row.mapBilledSeconds(input.quantity);
    return {
      providerId: row.providerId,
      modelId: modelId || row.modelId,
      estimatedAmount: billed * row.creditsPerSecond,
      currency: row.currency,
      costKind: row.costKind,
      billingBasis: row.billingBasis,
      billingUnit: row.billingUnit,
      billedQuantity: billed,
      minimumBillableQuantity: row.minimumBillableQuantity,
      kind: 'ESTIMATE',
    };
  }

  if (providerId === 'wan') {
    const row = FAMIXA_COST_CATALOG.wan;
    const billed = input.quantity && input.quantity > 0 ? input.quantity : row.minimumBillableQuantity;
    return {
      providerId: row.providerId,
      modelId: modelId || row.modelId,
      estimatedAmount: undefined,
      currency: row.currency,
      costKind: row.costKind,
      billingBasis: row.billingBasis,
      billingUnit: row.billingUnit,
      billedQuantity: billed,
      minimumBillableQuantity: row.minimumBillableQuantity,
      kind: 'FAL_BILLED_ESTIMATE',
    };
  }

  if (providerId === 'fal') {
    const model = normalizeCostLipsyncModel(modelId || '1.9');
    if (model === 'v3') {
      const row = FAMIXA_COST_CATALOG.falV3;
      const billed = Math.max(row.minimumBillableQuantity, input.quantity && input.quantity > 0 ? input.quantity : row.minimumBillableQuantity);
      return {
        providerId: row.providerId,
        modelId: row.modelId,
        estimatedAmount: money2((billed / 60) * row.usdPerMinute),
        currency: row.currency,
        costKind: row.costKind,
        billingBasis: row.billingBasis,
        billingUnit: row.billingUnit,
        billedQuantity: billed,
        minimumBillableQuantity: row.minimumBillableQuantity,
        kind: 'ESTIMATE',
      };
    }
    if (model === 'ls') {
      const row = FAMIXA_COST_CATALOG.falLs;
      const billed = Math.max(row.minimumBillableQuantity, input.quantity && input.quantity > 0 ? input.quantity : row.minimumBillableQuantity);
      return {
        providerId: row.providerId,
        modelId: row.modelId,
        estimatedAmount: billed <= 40 ? row.usdAtOrBelow40 : money2(billed * row.usdPerSecondOver40),
        currency: row.currency,
        costKind: row.costKind,
        billingBasis: row.billingBasis,
        billingUnit: row.billingUnit,
        billedQuantity: billed,
        minimumBillableQuantity: row.minimumBillableQuantity,
        kind: 'ESTIMATE',
      };
    }
    const row = FAMIXA_COST_CATALOG.fal19;
    const billed = Math.max(row.minimumBillableQuantity, input.quantity && input.quantity > 0 ? input.quantity : row.minimumBillableQuantity);
    return {
      providerId: row.providerId,
      modelId: row.modelId,
      estimatedAmount: money2((billed / 60) * row.usdPerMinute),
      currency: row.currency,
      costKind: row.costKind,
      billingBasis: row.billingBasis,
      billingUnit: row.billingUnit,
      billedQuantity: billed,
      minimumBillableQuantity: row.minimumBillableQuantity,
      kind: 'ESTIMATE',
    };
  }

  if (providerId === 'gemini') {
    const row = FAMIXA_COST_CATALOG.gemini;
    return {
      providerId: row.providerId,
      modelId: modelId || row.modelId,
      costKind: row.costKind,
      billingBasis: row.billingBasis,
      billingUnit: row.billingUnit,
      billedQuantity: 0,
      minimumBillableQuantity: row.minimumBillableQuantity,
      kind: 'UNKNOWN',
    };
  }

  if (providerId === 'elevenlabs') {
    const row = FAMIXA_COST_CATALOG.elevenlabs;
    return {
      providerId: row.providerId,
      modelId: modelId || row.modelId,
      costKind: row.costKind,
      billingBasis: row.billingBasis,
      billingUnit: row.billingUnit,
      billedQuantity: 0,
      minimumBillableQuantity: row.minimumBillableQuantity,
      kind: 'UNKNOWN',
    };
  }

  return unknownQuote(providerId || 'unknown', modelId || undefined);
}

export function formatFamixaCostQuote(quote: FamixaCostQuote): string {
  if (quote.billingBasis === 'FAL_BILLED_ESTIMATE' || quote.kind === 'FAL_BILLED_ESTIMATE') {
    return FAMIXA_COST_COPY.wanEstimate;
  }
  if (quote.costKind === 'UNKNOWN' || quote.estimatedAmount == null) return 'UNKNOWN';
  if (quote.currency === 'credit') return `Runway Turbo · ${quote.estimatedAmount} cr`;
  if (quote.currency === 'usd') return `≈ $${quote.estimatedAmount.toFixed(2)}`;
  return String(quote.estimatedAmount);
}

export function famixaMotionGenerateCost(engine: 'turbo' | 'wan', seconds: number) {
  const quote = quoteFamixaProviderCost({
    providerId: engine === 'wan' ? 'wan' : 'runway',
    modelId: engine === 'wan' ? 'wan-2.1' : 'gen4_turbo',
    quantity: seconds,
  });
  const label =
    engine === 'wan' ? `${formatFamixaCostQuote(quote)}. ${FAMIXA_COST_COPY.wanDurationHint}` : formatFamixaCostQuote(quote);
  return {
    credits: quote.estimatedAmount ?? null,
    label,
    quote,
  };
}

export function famixaMotionBatchCost(engine: 'turbo' | 'wan', secondsList: number[]) {
  if (engine === 'wan') {
    return {
      credits: null as number | null,
      label: `${FAMIXA_COST_COPY.wanEstimate} · ${secondsList.length} clip`,
    };
  }
  const credits = secondsList.reduce((n, sec) => n + (famixaMotionGenerateCost('turbo', sec).credits ?? 0), 0);
  return { credits, label: `ước ${credits} cr` };
}

export function famixaLipsyncEstimateUsd(seconds: number, model?: string) {
  return quoteFamixaProviderCost({ providerId: 'fal', modelId: model, quantity: seconds }).estimatedAmount;
}
