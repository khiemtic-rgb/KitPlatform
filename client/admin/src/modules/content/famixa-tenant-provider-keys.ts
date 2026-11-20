import type { TenantAiCapability, TenantAiCapabilitySettings } from '@/shared/api/tenant-ai-config.api';

/** Presence only. Never a secret, ciphertext, or credential id. */
export type FamixaProviderKeyFlags = {
  runway: boolean;
  fal: boolean;
  elevenLabs: boolean;
  gemini: boolean;
  veo: boolean;
};

const EMPTY_FLAGS: FamixaProviderKeyFlags = {
  runway: false,
  fal: false,
  elevenLabs: false,
  gemini: false,
  veo: false,
};

function configured(
  rows: TenantAiCapabilitySettings[],
  capability: TenantAiCapability,
  providerId: string,
  modelId: string,
) {
  const row = rows.find((item) => item.capability === capability);
  const config = row?.config;
  if (!config?.enabled || !config.credentialId) return false;
  return config.providerId.toLowerCase() === providerId.toLowerCase()
    && config.modelId.toLowerCase() === modelId.toLowerCase();
}

/** Tenant Production credentials. Content settings are a separate store and are merged by the caller. */
export function tenantAiKeyFlags(rows: TenantAiCapabilitySettings[]): FamixaProviderKeyFlags {
  return {
    gemini: configured(rows, 'IMAGE', 'GEMINI', 'gemini-3.1-flash-image'),
    runway: configured(rows, 'VIDEO', 'runway', 'gen4_turbo'),
    elevenLabs: configured(rows, 'VOICE', 'elevenlabs', 'eleven_v3'),
    fal: configured(rows, 'LIPSYNC', 'fal', 'sync-lipsync-v3'),
    veo: configured(rows, 'VIDEO', 'google', 'veo-3.1'),
  };
}

export function providerKeyFlagsFromSources(
  settings: {
    ai?: { apiKeyConfigured?: boolean } | null;
    video?: {
      runwayConfigured?: boolean;
      falConfigured?: boolean;
      elevenLabsConfigured?: boolean;
      veoJoinConfigured?: boolean;
    } | null;
  } | null,
  tenant: TenantAiCapabilitySettings[],
): FamixaProviderKeyFlags {
  return mergeProviderKeyFlags(
    {
      runway: Boolean(settings?.video?.runwayConfigured),
      fal: Boolean(settings?.video?.falConfigured),
      elevenLabs: Boolean(settings?.video?.elevenLabsConfigured),
      gemini: Boolean(settings?.ai?.apiKeyConfigured),
      veo: Boolean(settings?.video?.veoJoinConfigured),
    },
    tenantAiKeyFlags(tenant),
  );
}

export function mergeProviderKeyFlags(
  contentSettings: FamixaProviderKeyFlags,
  tenant: FamixaProviderKeyFlags,
): FamixaProviderKeyFlags {
  return {
    runway: contentSettings.runway || tenant.runway,
    fal: contentSettings.fal || tenant.fal,
    elevenLabs: contentSettings.elevenLabs || tenant.elevenLabs,
    gemini: contentSettings.gemini || tenant.gemini,
    veo: contentSettings.veo || tenant.veo,
  };
}

export function emptyProviderKeyFlags(): FamixaProviderKeyFlags {
  return { ...EMPTY_FLAGS };
}
