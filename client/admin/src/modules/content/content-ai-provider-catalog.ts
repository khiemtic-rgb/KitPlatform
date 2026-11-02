/** Fixed Content AI provider catalog. Credentials only — pick engine at generate time. */

export type ContentAiProviderKind = 'video' | 'voice' | 'render';
export type ContentAiProviderId = 'runway' | 'fal' | 'veo' | 'elevenlabs' | 'f5' | 'creatomate';

export type ContentAiProviderRow = {
  id: ContentAiProviderId;
  kind: ContentAiProviderKind;
  title: string;
  hint: string;
};

export const CONTENT_AI_PROVIDER_KIND_LABEL: Record<ContentAiProviderKind, string> = {
  video: 'Video',
  voice: 'Giọng nói',
  render: 'Render',
};

export const CONTENT_AI_PROVIDER_CATALOG: ContentAiProviderRow[] = [
  { id: 'runway', kind: 'video', title: 'Runway Turbo', hint: 'I2V Series' },
  { id: 'fal', kind: 'video', title: 'Fal — Wan + khớp môi', hint: 'Wan I2V + F5 + lipsync' },
  { id: 'veo', kind: 'video', title: 'Google Veo 3', hint: 'File Join — không dùng key Gemini' },
  { id: 'elevenlabs', kind: 'voice', title: 'ElevenLabs', hint: 'TTS' },
  { id: 'f5', kind: 'voice', title: 'F5-TTS (Fal)', hint: 'Dùng key Fal — không có key riêng' },
  { id: 'creatomate', kind: 'render', title: 'Creatomate', hint: 'Render Factory' },
];

/** Video I2V rows stay on the grid — hide only voice/render extras. */
export const PINNED_CONTENT_AI_PROVIDERS: ContentAiProviderId[] = ['runway', 'fal', 'veo'];

const HIDDEN_KEY = 'kit.contentAi.providerList.hidden';

export function loadHiddenContentAiProviders(): ContentAiProviderId[] {
  try {
    const raw = localStorage.getItem(HIDDEN_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter((id): id is ContentAiProviderId =>
      CONTENT_AI_PROVIDER_CATALOG.some((row) => row.id === id),
    );
  } catch {
    return [];
  }
}

export function saveHiddenContentAiProviders(ids: ContentAiProviderId[]): void {
  try {
    localStorage.setItem(HIDDEN_KEY, JSON.stringify(ids));
  } catch {
    /* quota */
  }
}

export function visibleContentAiProviders(hidden: ContentAiProviderId[]): ContentAiProviderRow[] {
  const skip = new Set(hidden.filter((id) => !PINNED_CONTENT_AI_PROVIDERS.includes(id)));
  return CONTENT_AI_PROVIDER_CATALOG.filter((row) => !skip.has(row.id));
}

export function hiddenContentAiProviders(hidden: ContentAiProviderId[]): ContentAiProviderRow[] {
  const skip = new Set(hidden.filter((id) => !PINNED_CONTENT_AI_PROVIDERS.includes(id)));
  return CONTENT_AI_PROVIDER_CATALOG.filter((row) => skip.has(row.id));
}
