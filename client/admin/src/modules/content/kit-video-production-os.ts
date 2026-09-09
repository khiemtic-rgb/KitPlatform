export const PRODUCTION_OS_ARCHITECTURE_ID = 'PRODUCTION_OS_ARCHITECTURE_V1';
export const PRODUCTION_OS_ID = 'FAMIXA_VIDEO_PRODUCTION_OS_ARCHITECTURE_V1';
export const PRODUCTION_OS_SUITE = 'FAMIXA_PROVIDER_AGNOSTIC_ARCHITECTURE_REGRESSION';

export const FAMIXA_LAYERS = ['STORY', 'WORLD', 'PRODUCTION', 'GOVERNANCE', 'ORCHESTRATION'] as const;
export const PROVIDER_LAYER = 'AI_PROVIDERS';

export const AUTHORITY_CHAIN = [
  'MASTER',
  'DNA',
  'CHARACTER_REFERENCE',
  'PRODUCTION_REFERENCE',
  'SHOT_CONTRACT',
  'PROMPT_COMPILER',
  'GENERATION_CONTRACT',
  'GENERATION_EXECUTION',
  'ARTIFACT',
] as const;

/** Business labels only. Not wired over SERIES_STAFF_TABS. */
export const PRODUCTION_OS_STAFF_STEPS = [
  'Kịch bản',
  'Chia cảnh',
  'Nhân vật & bối cảnh',
  'Tạo hình',
  'Duyệt hình',
  'Tạo video',
  'Duyệt video',
  'Hoàn tất',
] as const;
