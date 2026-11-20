import { http } from '@/shared/api/http';

export type TenantAiCapability = 'TEXT' | 'IMAGE' | 'VIDEO' | 'VOICE' | 'LIPSYNC';

export type TenantAiCatalogEntry = {
  providerId: string;
  modelId: string;
  displayName: string;
  isPaid: boolean;
  requiresCredential: boolean;
};

export type TenantAiCredentialOption = {
  credentialId: string;
  displayName: string;
  providerId: string;
  modelId: string;
  version: number;
};

export type TenantAiConfig = {
  providerId: string;
  modelId: string;
  credentialId: string | null;
  enabled: boolean;
  updatedBy: string;
  updatedAt: string;
};

export type TenantAiCapabilitySettings = {
  capability: TenantAiCapability;
  config: TenantAiConfig | null;
  catalog: TenantAiCatalogEntry[];
  credentials: TenantAiCredentialOption[];
};

export type SaveTenantAiConfigInput = {
  providerId: string;
  modelId: string;
  credentialId: string | null;
  enabled: boolean;
};

export async function fetchTenantAiConfig(): Promise<TenantAiCapabilitySettings[]> {
  const { data } = await http.get<{ capabilities: TenantAiCapabilitySettings[] }>(
    '/content/production/tenant-ai-config',
  );
  return data.capabilities ?? [];
}

export async function saveTenantAiConfig(
  capability: TenantAiCapability,
  input: SaveTenantAiConfigInput,
): Promise<TenantAiConfig> {
  const { data } = await http.put<TenantAiConfig>(`/content/production/tenant-ai-config/${capability}`, {
    providerId: input.providerId,
    modelId: input.modelId,
    credentialId: input.credentialId,
    enabled: input.enabled,
  });
  return data;
}

export async function clearTenantAiConfig(capability: TenantAiCapability): Promise<void> {
  await http.delete(`/content/production/tenant-ai-config/${capability}`);
}

/** Credentials usable for one catalog model. Only the caller tenant's credentials are ever returned by the API. */
export function credentialsForModel(
  settings: Pick<TenantAiCapabilitySettings, 'credentials'>,
  model: Pick<TenantAiCatalogEntry, 'providerId' | 'modelId'> | null | undefined,
): TenantAiCredentialOption[] {
  if (!model) return [];
  const p = model.providerId.toLowerCase();
  const m = model.modelId.toLowerCase();
  return settings.credentials.filter((c) => c.providerId.toLowerCase() === p && c.modelId.toLowerCase() === m);
}

export function modelKey(model: Pick<TenantAiCatalogEntry, 'providerId' | 'modelId'>): string {
  return `${model.providerId}::${model.modelId}`;
}
