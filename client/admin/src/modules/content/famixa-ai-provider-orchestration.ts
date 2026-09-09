/** Famixa Provider Orchestration Core V1 — HOW only. No HTTP. No ShotExecutionState. */

import { quoteFamixaProviderCost } from './famixa-ai-provider-cost';
import { selectFamixaProvider } from './famixa-ai-provider-routing-foundation';

export const FAMIXA_PROVIDER_ORCHESTRATION_ID = 'FAMIXA_AI_PROVIDER_ORCHESTRATION_CORE_V1';

export const FAMIXA_PROVIDER_IDS = {
  gemini: 'gemini',
  runway: 'runway',
  wan: 'wan',
  elevenlabs: 'elevenlabs',
  fal: 'fal',
} as const;

export type FamixaProviderId = (typeof FAMIXA_PROVIDER_IDS)[keyof typeof FAMIXA_PROVIDER_IDS];
export type FamixaProviderCapability = 'PICTURE' | 'MOTION' | 'VOICE' | 'LIPSYNC';
export type FamixaProviderPolicy = 'ECONOMY' | 'STANDARD' | 'PREMIUM';
export type FamixaProviderStatus = 'PENDING' | 'RUNNING' | 'SUCCEEDED' | 'FAILED';
export type FamixaCostKind = 'ESTIMATE' | 'UNKNOWN' | 'FAL_BILLED_ESTIMATE';

export const FAMIXA_PROVIDER_ERROR = {
  PROVIDER_UNAVAILABLE: 'PROVIDER_UNAVAILABLE',
  AUTH_FAILED: 'AUTH_FAILED',
  RATE_LIMITED: 'RATE_LIMITED',
  INVALID_INPUT: 'INVALID_INPUT',
  PROVIDER_ERROR: 'PROVIDER_ERROR',
  TIMEOUT: 'TIMEOUT',
  UNSUPPORTED: 'UNSUPPORTED',
  UNKNOWN: 'UNKNOWN',
} as const;

export type FamixaProviderDescriptor = {
  providerId: FamixaProviderId;
  displayName: string;
  capabilities: FamixaProviderCapability[];
  models: string[];
  qualityTier: string;
  costInfo: string;
  available: boolean;
};

export type FamixaProviderSelection = {
  engine?: string;
  lipsyncModel?: string;
  voiceProvider?: string;
  explicitProviderId?: string;
  policy?: FamixaProviderPolicy;
};

export type FamixaProviderResolveResult = { providerId: FamixaProviderId; modelId?: string };

export type FamixaProviderResult = {
  providerId: string;
  modelId?: string;
  providerRequestId?: string;
  status: FamixaProviderStatus;
  mimeType?: string;
  outputUrl?: string;
  bytes?: Uint8Array;
  costKind: FamixaCostKind;
  costEstimate?: number;
  costUnit?: string;
};

export type FamixaPictureProviderRequest = {
  prompt: string;
  references: { mime: string; base64: string; label: string }[];
  aspectRatio?: string;
};

export type FamixaMotionProviderRequest = {
  imageDataUrl: string;
  prompt: string;
  negativePrompt?: string;
  seconds: 5 | 10;
  ratio: string;
  lastFrameDataUrl?: string;
};

export type FamixaVoiceProviderRequest = {
  voiceId: string;
  text: string;
};

export type FamixaLipSyncProviderRequest = {
  videoUrl: string;
  audioUrl: string;
  syncMode?: string;
  modelKind?: string;
};

export interface IPictureProvider {
  readonly providerId: string;
  generate(request: FamixaPictureProviderRequest): Promise<FamixaProviderResult>;
}

export type FamixaProviderTaskSource = 'EXPLICIT' | 'LEGACY_PREFIX' | 'LEGACY_UNVERIFIED';

export type FamixaProviderTaskRef = {
  providerId: FamixaProviderId;
  capability: FamixaProviderCapability;
  source: FamixaProviderTaskSource;
  modelId?: string;
};

export interface IMotionProvider {
  readonly providerId: string;
  canHandleTask(taskId?: string): boolean;
  start(request: FamixaMotionProviderRequest): Promise<FamixaProviderResult>;
  get(taskId: string): Promise<FamixaProviderResult>;
  recover(taskId: string): Promise<FamixaProviderResult>;
}

export interface IVoiceProvider {
  readonly providerId: string;
  synthesize(request: FamixaVoiceProviderRequest): Promise<FamixaProviderResult>;
}

export interface ILipSyncProvider {
  readonly providerId: string;
  canHandleTask(taskId?: string): boolean;
  start(request: FamixaLipSyncProviderRequest): Promise<FamixaProviderResult>;
  get(taskId: string): Promise<FamixaProviderResult>;
  recover(taskId: string): Promise<FamixaProviderResult>;
}

export const FAMIXA_PROVIDER_CATALOG: FamixaProviderDescriptor[] = [
  {
    providerId: 'gemini',
    displayName: 'Gemini',
    capabilities: ['PICTURE'],
    models: ['gemini-2.5-flash-image'],
    qualityTier: 'standard',
    costInfo: 'UNKNOWN',
    available: true,
  },
  {
    providerId: 'runway',
    displayName: 'Runway',
    capabilities: ['MOTION'],
    models: ['gen4_turbo'],
    qualityTier: 'standard',
    costInfo: 'ESTIMATE 5 cr/s',
    available: true,
  },
  {
    providerId: 'wan',
    displayName: 'Wan 2.1',
    capabilities: ['MOTION'],
    models: ['wan-2.1'],
    qualityTier: 'economy',
    costInfo: 'FAL_BILLED_ESTIMATE',
    available: true,
  },
  {
    providerId: 'elevenlabs',
    displayName: 'ElevenLabs',
    capabilities: ['VOICE'],
    models: ['eleven_v3'],
    qualityTier: 'standard',
    costInfo: 'UNKNOWN',
    available: true,
  },
  {
    providerId: 'fal',
    displayName: 'Fal LipSync',
    capabilities: ['LIPSYNC'],
    models: ['1.9', 'v3', 'ls'],
    qualityTier: 'standard',
    costInfo: 'ESTIMATE',
    available: true,
  },
];

const FORBIDDEN_PICTURE = [
  'videoApproved',
  'acceptedTake',
  'sameFailedInput',
  'nextShotProductionCommand',
  'canon',
] as const;
const FORBIDDEN_MOTION = ['acceptedTake', 'ACCEPT_EXISTING', 'videoApproved', 'sameFailedInput'] as const;
const FORBIDDEN_VOICE = ['ShotExecutionState', 'videoApproved', 'acceptedTake'] as const;
const FORBIDDEN_LIPSYNC = ['videoApproved', 'acceptedTake', 'kfApproved'] as const;

export function pictureInputIsContractOnly(request: FamixaPictureProviderRequest) {
  return FORBIDDEN_PICTURE.every((k) => !(k in request));
}

export function motionInputIsContractOnly(request: FamixaMotionProviderRequest) {
  return FORBIDDEN_MOTION.every((k) => !(k in request));
}

export function voiceInputIsContractOnly(request: FamixaVoiceProviderRequest) {
  return FORBIDDEN_VOICE.every((k) => !(k in request));
}

export function lipSyncInputIsContractOnly(request: FamixaLipSyncProviderRequest) {
  return FORBIDDEN_LIPSYNC.every((k) => !(k in request));
}

export function describeFamixaProvider(providerId: string): FamixaProviderDescriptor {
  const hit = FAMIXA_PROVIDER_CATALOG.find((d) => d.providerId === providerId.trim());
  if (!hit) throw Object.assign(new Error(`PROVIDER_UNAVAILABLE: ${providerId}`), { code: FAMIXA_PROVIDER_ERROR.PROVIDER_UNAVAILABLE });
  return hit;
}

export function famixaProviderHasCapability(providerId: string, capability: FamixaProviderCapability) {
  const hit = FAMIXA_PROVIDER_CATALOG.find((d) => d.providerId === providerId.trim());
  return Boolean(hit?.capabilities.includes(capability));
}

export function normalizeFamixaLipsyncModel(raw?: string) {
  const m = (raw || '').trim().toLowerCase();
  if (m === 'v3' || m === 'sync-lipsync-v3') return 'v3';
  if (m === 'ls' || m === 'latentsync' || m === 'latent') return 'ls';
  return '1.9';
}

export function mapFamixaProviderPolicy(
  capability: FamixaProviderCapability,
  policy: FamixaProviderPolicy,
): FamixaProviderResolveResult {
  if (capability === 'MOTION' && policy === 'ECONOMY') return { providerId: 'wan', modelId: 'wan-2.1' };
  if (capability === 'LIPSYNC' && policy === 'PREMIUM') return { providerId: 'fal', modelId: 'v3' };
  if (capability === 'LIPSYNC' && policy === 'ECONOMY') return { providerId: 'fal', modelId: 'ls' };
  if (capability === 'PICTURE') return { providerId: 'gemini' };
  if (capability === 'MOTION') return { providerId: 'runway', modelId: 'gen4_turbo' };
  if (capability === 'VOICE') return { providerId: 'elevenlabs', modelId: 'eleven_v3' };
  return { providerId: 'fal', modelId: '1.9' };
}

export function famixaLipsyncModelFromTask(taskId?: string) {
  const t = (taskId || '').trim().toLowerCase();
  if (t.startsWith('lipsync_v3_')) return 'v3';
  if (t.startsWith('lipsync_ls_')) return 'ls';
  return normalizeFamixaLipsyncModel(taskId);
}

export function isLegacyWanTaskPrefix(taskId?: string) {
  return (taskId || '').toLowerCase().startsWith('wan_');
}

export function isLegacyLipsyncTaskPrefix(taskId?: string) {
  const t = taskId || '';
  return t.startsWith('lipsync_') || t.startsWith('lipsync_v3_') || t.startsWith('lipsync_v1_') || t.startsWith('lipsync_ls_');
}

/** LEGACY prefix / unverified UUID. Explicit providerId is canonical. */
export function resolveFamixaProviderTask(
  taskId?: string,
  providerId?: string,
  capability?: FamixaProviderCapability,
): FamixaProviderTaskRef {
  const explicit = (providerId || '').trim();
  if (explicit) {
    const desc = describeFamixaProvider(explicit);
    const cap = capability || desc.capabilities[0]!;
    if (!desc.capabilities.includes(cap)) {
      throw Object.assign(new Error(`UNSUPPORTED: ${explicit} không có ${cap}`), { code: FAMIXA_PROVIDER_ERROR.UNSUPPORTED });
    }
    return {
      providerId: desc.providerId,
      capability: cap,
      source: 'EXPLICIT',
      modelId: cap === 'LIPSYNC' ? famixaLipsyncModelFromTask(taskId) : desc.models[0],
    };
  }
  const raw = (taskId || '').trim();
  if (isLegacyLipsyncTaskPrefix(raw)) {
    return { providerId: 'fal', capability: 'LIPSYNC', source: 'LEGACY_PREFIX', modelId: famixaLipsyncModelFromTask(raw) };
  }
  if (isLegacyWanTaskPrefix(raw)) {
    return { providerId: 'wan', capability: 'MOTION', source: 'LEGACY_PREFIX', modelId: 'wan-2.1' };
  }
  if (!raw) {
    throw Object.assign(new Error('PROVIDER_UNAVAILABLE: thiếu task id'), { code: FAMIXA_PROVIDER_ERROR.PROVIDER_UNAVAILABLE });
  }
  if (capability === 'LIPSYNC') {
    return { providerId: 'fal', capability: 'LIPSYNC', source: 'LEGACY_UNVERIFIED', modelId: '1.9' };
  }
  return { providerId: 'runway', capability: 'MOTION', source: 'LEGACY_UNVERIFIED', modelId: 'gen4_turbo' };
}

export function famixaProviderCanHandleTask(
  providerId: string,
  capability: FamixaProviderCapability,
  taskId?: string,
) {
  const raw = (taskId || '').trim();
  if (!raw) return false;
  if (capability === 'LIPSYNC') return isLegacyLipsyncTaskPrefix(raw) && providerId === 'fal';
  if (capability === 'MOTION' && providerId === 'wan') return isLegacyWanTaskPrefix(raw);
  if (capability === 'MOTION' && providerId === 'runway') return !isLegacyWanTaskPrefix(raw) && !isLegacyLipsyncTaskPrefix(raw);
  return false;
}

/** Compatibility wrapper. Series selection SoT is selectFamixaProvider. */
export function resolveFamixaProvider(
  capability: FamixaProviderCapability,
  selection?: FamixaProviderSelection,
): FamixaProviderResolveResult {
  const decision = selectFamixaProvider({
    capability,
    policy: selection?.policy,
    explicitProviderId: selection?.explicitProviderId,
    engine: selection?.engine,
    lipsyncModel: selection?.lipsyncModel,
    voiceProvider: selection?.voiceProvider,
  });
  return { providerId: decision.providerId as FamixaProviderId, modelId: decision.modelId };
}

export function estimateFamixaProviderCost(
  providerId: string,
  modelId?: string,
  seconds?: number,
): { kind: FamixaCostKind; amount?: number; unit?: string } {
  const quote = quoteFamixaProviderCost({ providerId, modelId, quantity: seconds });
  return { kind: quote.kind, amount: quote.estimatedAmount, unit: quote.currency };
}

export class FamixaProviderRegistry {
  constructor(
    private readonly picture: Record<string, IPictureProvider> = {},
    private readonly motion: Record<string, IMotionProvider> = {},
    private readonly voice: Record<string, IVoiceProvider> = {},
    private readonly lipsync: Record<string, ILipSyncProvider> = {},
  ) {}

  list() {
    return FAMIXA_PROVIDER_CATALOG;
  }

  describe(providerId: string) {
    return describeFamixaProvider(providerId);
  }

  hasCapability(providerId: string, capability: FamixaProviderCapability) {
    return famixaProviderHasCapability(providerId, capability);
  }

  getPicture(providerId: string) {
    const hit = this.picture[providerId];
    if (!hit) throw Object.assign(new Error(`UNSUPPORTED: ${providerId} không có PICTURE`), { code: FAMIXA_PROVIDER_ERROR.UNSUPPORTED });
    return hit;
  }

  getMotion(providerId: string) {
    const hit = this.motion[providerId];
    if (!hit) throw Object.assign(new Error(`UNSUPPORTED: ${providerId} không có MOTION`), { code: FAMIXA_PROVIDER_ERROR.UNSUPPORTED });
    return hit;
  }

  getVoice(providerId: string) {
    const hit = this.voice[providerId];
    if (!hit) throw Object.assign(new Error(`UNSUPPORTED: ${providerId} không có VOICE`), { code: FAMIXA_PROVIDER_ERROR.UNSUPPORTED });
    return hit;
  }

  getLipSync(providerId: string) {
    const hit = this.lipsync[providerId];
    if (!hit) throw Object.assign(new Error(`UNSUPPORTED: ${providerId} không có LIPSYNC`), { code: FAMIXA_PROVIDER_ERROR.UNSUPPORTED });
    return hit;
  }

  describeTask(taskId?: string, providerId?: string, capability?: FamixaProviderCapability) {
    return resolveFamixaProviderTask(taskId, providerId, capability);
  }

  getMotionForTask(taskId?: string, providerId?: string) {
    const identity = resolveFamixaProviderTask(taskId, providerId, 'MOTION');
    if (identity.capability !== 'MOTION') {
      throw Object.assign(new Error(`UNSUPPORTED: ${taskId} không phải MOTION`), { code: FAMIXA_PROVIDER_ERROR.UNSUPPORTED });
    }
    return this.getMotion(identity.providerId);
  }

  getLipSyncForTask(taskId?: string, providerId?: string) {
    const identity = resolveFamixaProviderTask(taskId, providerId, 'LIPSYNC');
    return this.getLipSync(identity.providerId);
  }
}

export class FakePictureProvider implements IPictureProvider {
  readonly providerId = 'gemini';
  async generate(request: FamixaPictureProviderRequest): Promise<FamixaProviderResult> {
    if (!pictureInputIsContractOnly(request)) throw new Error('PICTURE_INPUT_LEAK');
    return { providerId: this.providerId, modelId: 'fake-still', status: 'SUCCEEDED', mimeType: 'image/jpeg', costKind: 'UNKNOWN' };
  }
}

export class FakeMotionProvider implements IMotionProvider {
  readonly calls: { kind: 'start' | 'get' | 'recover'; taskId?: string }[] = [];
  constructor(readonly providerId: string) {}
  canHandleTask(taskId?: string) {
    return famixaProviderCanHandleTask(this.providerId, 'MOTION', taskId);
  }
  async start(request: FamixaMotionProviderRequest): Promise<FamixaProviderResult> {
    if (!motionInputIsContractOnly(request)) throw new Error('MOTION_INPUT_LEAK');
    this.calls.push({ kind: 'start' });
    return {
      providerId: this.providerId,
      modelId: this.providerId === 'wan' ? 'wan-2.1' : 'gen4_turbo',
      providerRequestId: this.providerId === 'wan' ? 'wan_fake_1' : 'runway-task-1',
      status: 'PENDING',
      costKind: this.providerId === 'wan' ? 'FAL_BILLED_ESTIMATE' : 'ESTIMATE',
      costEstimate: this.providerId === 'wan' ? undefined : 25,
    };
  }
  async get(taskId: string): Promise<FamixaProviderResult> {
    this.calls.push({ kind: 'get', taskId });
    return {
      providerId: this.providerId,
      modelId: this.providerId === 'wan' ? 'wan-2.1' : 'gen4_turbo',
      providerRequestId: taskId,
      status: 'SUCCEEDED',
      outputUrl: `https://fake/${this.providerId}.mp4`,
      costKind: this.providerId === 'wan' ? 'FAL_BILLED_ESTIMATE' : 'ESTIMATE',
    };
  }
  async recover(taskId: string): Promise<FamixaProviderResult> {
    this.calls.push({ kind: 'recover', taskId });
    return this.get(taskId);
  }
}

export class FakeVoiceProvider implements IVoiceProvider {
  readonly providerId = 'elevenlabs';
  async synthesize(request: FamixaVoiceProviderRequest): Promise<FamixaProviderResult> {
    if (!voiceInputIsContractOnly(request)) throw new Error('VOICE_INPUT_LEAK');
    return { providerId: this.providerId, modelId: 'eleven_v3', status: 'SUCCEEDED', mimeType: 'audio/mpeg', costKind: 'UNKNOWN' };
  }
}

export class FakeLipSyncProvider implements ILipSyncProvider {
  readonly providerId = 'fal';
  readonly calls: { kind: 'start' | 'get' | 'recover'; taskId?: string }[] = [];
  canHandleTask(taskId?: string) {
    return famixaProviderCanHandleTask(this.providerId, 'LIPSYNC', taskId);
  }
  async start(request: FamixaLipSyncProviderRequest): Promise<FamixaProviderResult> {
    if (!lipSyncInputIsContractOnly(request)) throw new Error('LIPSYNC_INPUT_LEAK');
    this.calls.push({ kind: 'start' });
    return {
      providerId: this.providerId,
      modelId: normalizeFamixaLipsyncModel(request.modelKind),
      providerRequestId: 'lipsync_fake',
      status: 'PENDING',
      costKind: 'ESTIMATE',
    };
  }
  async get(taskId: string): Promise<FamixaProviderResult> {
    this.calls.push({ kind: 'get', taskId });
    return {
      providerId: this.providerId,
      modelId: famixaLipsyncModelFromTask(taskId),
      providerRequestId: taskId,
      status: 'SUCCEEDED',
      outputUrl: 'https://fake/lipsync.mp4',
      costKind: 'ESTIMATE',
    };
  }
  async recover(taskId: string): Promise<FamixaProviderResult> {
    this.calls.push({ kind: 'recover', taskId });
    return this.get(taskId);
  }
}

export function famixaProviderRegistryForTests() {
  return new FamixaProviderRegistry(
    { gemini: new FakePictureProvider() },
    { runway: new FakeMotionProvider('runway'), wan: new FakeMotionProvider('wan') },
    { elevenlabs: new FakeVoiceProvider() },
    { fal: new FakeLipSyncProvider() },
  );
}
