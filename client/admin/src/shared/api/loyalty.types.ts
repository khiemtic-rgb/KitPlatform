export interface LoyaltyTierAdmin {
  id?: string;
  tierCode: string;
  tierName: string;
  minPoints: number;
  discountPercent: number;
  sortOrder: number;
}

export interface LoyaltyProgramAdmin {
  id?: string;
  programCode: string;
  programName: string;
  pointsPerAmount: number;
  amountPerPoint: number;
  maxRedeemPercent: number;
  status: number;
  tiers: LoyaltyTierAdmin[];
}

export interface LoyaltyAdminSettings {
  loyaltyEnabled: boolean;
  program: LoyaltyProgramAdmin | null;
}

export interface UpdateLoyaltyAdminSettings {
  loyaltyEnabled: boolean;
  program: LoyaltyProgramAdmin;
}

export const DEFAULT_LOYALTY_PROGRAM: LoyaltyProgramAdmin = {
  programCode: 'LOYALTY_DEFAULT',
  programName: 'Tích điểm Pharmar',
  pointsPerAmount: 10000,
  amountPerPoint: 100,
  maxRedeemPercent: 5,
  status: 1,
  tiers: [
    { tierCode: 'BRONZE', tierName: 'Đồng', minPoints: 0, discountPercent: 0, sortOrder: 1 },
    { tierCode: 'SILVER', tierName: 'Bạc', minPoints: 500, discountPercent: 2, sortOrder: 2 },
    { tierCode: 'GOLD', tierName: 'Vàng', minPoints: 2000, discountPercent: 5, sortOrder: 3 },
  ],
};

function normalizeTier(row: Record<string, unknown>): LoyaltyTierAdmin {
  return {
    id: optionalString(row.id ?? row.Id),
    tierCode: String(row.tierCode ?? row.TierCode ?? ''),
    tierName: String(row.tierName ?? row.TierName ?? ''),
    minPoints: Number(row.minPoints ?? row.MinPoints ?? 0),
    discountPercent: Number(row.discountPercent ?? row.DiscountPercent ?? 0),
    sortOrder: Number(row.sortOrder ?? row.SortOrder ?? 0),
  };
}

function normalizeProgram(row: Record<string, unknown>): LoyaltyProgramAdmin {
  const tiersRaw = (row.tiers ?? row.Tiers ?? []) as Record<string, unknown>[];
  return {
    id: optionalString(row.id ?? row.Id),
    programCode: String(row.programCode ?? row.ProgramCode ?? ''),
    programName: String(row.programName ?? row.ProgramName ?? ''),
    pointsPerAmount: Number(row.pointsPerAmount ?? row.PointsPerAmount ?? 0),
    amountPerPoint: Number(row.amountPerPoint ?? row.AmountPerPoint ?? 1),
    maxRedeemPercent: Number(row.maxRedeemPercent ?? row.MaxRedeemPercent ?? 100),
    status: Number(row.status ?? row.Status ?? 1),
    tiers: tiersRaw.map(normalizeTier),
  };
}

function optionalString(value: unknown): string | undefined {
  if (value == null || value === '') return undefined;
  return String(value);
}

export function normalizeLoyaltySettings(row: Record<string, unknown>): LoyaltyAdminSettings {
  const programRaw = row.program ?? row.Program;
  return {
    loyaltyEnabled: Boolean(row.loyaltyEnabled ?? row.LoyaltyEnabled ?? false),
    program: programRaw ? normalizeProgram(programRaw as Record<string, unknown>) : null,
  };
}
