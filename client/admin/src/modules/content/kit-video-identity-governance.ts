/** Display metadata only. Identity rules live in C# CharacterIdentityGovernanceRules. */

export const IDENTITY_GOVERNANCE_ID = 'CHARACTER_IDENTITY_GOVERNANCE_V1_1';
export const IDENTITY_HIERARCHY = [
  'MASTER',
  'CHARACTER_DNA',
  'PRODUCTION_REFERENCE_PACK',
  'SHOT_SPECIFICATION',
  'USER_PROMPT',
  'GENERATION_MODEL',
] as const;

export const GOVERNANCE_GATES = [
  'MASTER_GATE',
  'DNA_GATE',
  'PRP_GATE',
  'IDENTITY_GATE',
  'STRESS_GATE',
  'CONTINUITY_GATE',
  'REGRESSION_GATE',
  'SHOT_GATE',
  'PROMPT_GATE',
] as const;
