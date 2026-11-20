import { http } from '@/shared/api/http';

export type ProductionAiCatalogModel = {
  providerId: string;
  modelId: string;
  displayName: string;
  capability: string;
  action: string;
  enabled: boolean;
  isPaid: boolean;
  credentialStatus: 'ACTIVE' | 'NOT_CONFIGURED' | 'AMBIGUOUS';
};

export type ProductionAiCatalog = {
  video: ProductionAiCatalogModel[];
  voice: ProductionAiCatalogModel[];
  lipSync: ProductionAiCatalogModel[];
  image: ProductionAiCatalogModel[];
};

export type RotateProductionCredentialInput = {
  providerId: string;
  modelId: string;
  secret: string;
};

export type RotateProductionCredentialResult = {
  providerId: string;
  modelId: string;
  status: string;
  version: number;
};

export type ProductionCredentialBackupGate = {
  writeGate: string;
  restoreVerification: string;
  encryptionKeyId: string | null;
  blockCode: string | null;
  evidenceCode: string | null;
  evidenceKeyId: string | null;
};

export async function fetchProductionCredentialBackupGate(): Promise<ProductionCredentialBackupGate> {
  const { data } = await http.get<Partial<ProductionCredentialBackupGate>>('/content/production/credentials/backup-gate');
  return {
    writeGate: data.writeGate ?? '',
    restoreVerification: data.restoreVerification ?? '',
    encryptionKeyId: data.encryptionKeyId ?? null,
    blockCode: data.blockCode ?? null,
    evidenceCode: data.evidenceCode ?? null,
    evidenceKeyId: data.evidenceKeyId ?? null,
  };
}

export async function fetchProductionAiCatalog() {
  const { data } = await http.get<Partial<ProductionAiCatalog>>('/content/production/ai-catalog');
  return {
    video: data.video ?? [],
    voice: data.voice ?? [],
    lipSync: data.lipSync ?? [],
    image: data.image ?? [],
  };
}

export async function rotateProductionCredential(
  input: RotateProductionCredentialInput,
): Promise<RotateProductionCredentialResult> {
  const { data } = await http.post<RotateProductionCredentialResult>('/content/production/credentials/rotate', {
    providerId: input.providerId,
    modelId: input.modelId,
    secret: input.secret,
  });
  return {
    providerId: data.providerId,
    modelId: data.modelId,
    status: data.status,
    version: data.version,
  };
}

export type ModelAiCredentialClient = {
  register: (body: { providerId: string; modelId: string; secret: string; displayName?: string }) => Promise<unknown>;
  rotate: (body: RotateProductionCredentialInput) => Promise<RotateProductionCredentialResult>;
  reload: () => Promise<void>;
};

export type ModelAiCredentialOutcome =
  | { kind: 'configured' }
  | { kind: 'rotated'; status: string; version: number };

/** AMBIGUOUS has several ACTIVE credentials; no write is allowed from Model AI. */
export async function submitModelAiCredential(
  model: Pick<ProductionAiCatalogModel, 'providerId' | 'modelId' | 'displayName' | 'credentialStatus'>,
  values: { secret: string; displayName?: string },
  client: ModelAiCredentialClient,
): Promise<ModelAiCredentialOutcome> {
  if (model.credentialStatus === 'NOT_CONFIGURED') {
    await client.register({
      providerId: model.providerId,
      modelId: model.modelId,
      secret: values.secret,
      displayName: values.displayName?.trim() || model.displayName,
    });
    await client.reload();
    return { kind: 'configured' };
  }
  if (model.credentialStatus === 'ACTIVE') {
    const result = await client.rotate({
      providerId: model.providerId,
      modelId: model.modelId,
      secret: values.secret,
    });
    await client.reload();
    return { kind: 'rotated', status: result.status, version: result.version };
  }
  throw new Error('PRODUCTION_CREDENTIAL_AMBIGUOUS');
}
