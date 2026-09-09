export const PRODUCTION_SHOT_CONTRACT_ID = 'PRODUCTION_SHOT_CONTRACT_V1';

export const CONTRACT_STATUSES = ['DRAFT', 'VALIDATED', 'DIRECTOR_APPROVED', 'REJECTED', 'SUPERSEDED'] as const;

export const CONTRACT_SECTIONS = [
  'identity',
  'scene',
  'story',
  'character',
  'wardrobe',
  'props',
  'composition',
  'lighting',
  'motion',
  'timing',
  'continuity',
  'constraints',
  'dialogue',
  'production',
] as const;
