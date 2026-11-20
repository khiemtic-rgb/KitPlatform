/** Draft packages are the default. A higher package is sent only after the user selects it. */

export type FamixaQualityCapability = 'IMAGE' | 'VIDEO' | 'VOICE' | 'LIPSYNC';

export type FamixaQualityTier = {
  id: string;
  label: string;
  detail: string;
  /** False means the package is listed but this build cannot call it. */
  live: boolean;
};

const KEY = 'famixa-quality-tier:v1';

export const FAMIXA_QUALITY_TIERS: Record<FamixaQualityCapability, FamixaQualityTier[]> = {
  IMAGE: [
    { id: '0.5K', label: 'Nháp 0.5K', detail: 'ước tính 0,045 USD', live: true },
    { id: '1K', label: '1K', detail: 'ước tính 0,067 USD', live: true },
    { id: '2K', label: '2K', detail: 'ước tính 0,101 USD', live: true },
    { id: '4K', label: 'Cao 4K', detail: 'ước tính 0,151 USD', live: true },
  ],
  VIDEO: [
    { id: '5', label: 'Nháp 5 giây', detail: 'ước tính 25 credit', live: true },
    { id: '10', label: 'Dài 10 giây', detail: 'ước tính 50 credit', live: true },
  ],
  VOICE: [
    { id: 'eleven_v3', label: 'ElevenLabs V3', detail: 'một gói đang chạy', live: true },
  ],
  LIPSYNC: [
    { id: 'sync-lipsync-v3', label: 'Fal Sync V3', detail: 'một gói đang chạy', live: true },
  ],
};

const CAPABILITY_LABEL: Record<FamixaQualityCapability, string> = {
  IMAGE: 'Hình',
  VIDEO: 'Video',
  VOICE: 'Giọng',
  LIPSYNC: 'Khớp môi',
};

export function qualityCapabilityLabel(capability: FamixaQualityCapability) {
  return CAPABILITY_LABEL[capability];
}

const memory: Record<string, string> = {};

function storage(): Record<string, string> {
  if (typeof localStorage === 'undefined') return { ...memory };
  try {
    const raw = localStorage.getItem(KEY);
    const parsed = raw ? JSON.parse(raw) : {};
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function save(next: Record<string, string>) {
  if (typeof localStorage === 'undefined') {
    for (const key of Object.keys(memory)) delete memory[key];
    Object.assign(memory, next);
    return;
  }
  localStorage.setItem(KEY, JSON.stringify(next));
}

export function defaultQualityTier(capability: FamixaQualityCapability) {
  return FAMIXA_QUALITY_TIERS[capability][0].id;
}

export function readQualityTier(shotId: string, capability: FamixaQualityCapability) {
  const saved = storage()[`${shotId}:${capability}`];
  const known = FAMIXA_QUALITY_TIERS[capability].find((row) => row.live && row.id === saved);
  return known?.id ?? defaultQualityTier(capability);
}

export function writeQualityTier(shotId: string, capability: FamixaQualityCapability, tierId: string) {
  const known = FAMIXA_QUALITY_TIERS[capability].find((row) => row.live && row.id === tierId);
  if (!known) return;
  const next = storage();
  next[`${shotId}:${capability}`] = known.id;
  save(next);
}

export function qualityTierOf(capability: FamixaQualityCapability, tierId: string) {
  return FAMIXA_QUALITY_TIERS[capability].find((row) => row.id === tierId)
    ?? FAMIXA_QUALITY_TIERS[capability][0];
}
