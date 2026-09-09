export const VISUAL_MODE_V1_ID = 'FAMIXA_VISUAL_MODE_AUTHORITY_V1';
export const VISUAL_MODE_SUITE = 'FAMIXA_VISUAL_MODE_AUTHORITY_V1_REGRESSION';

export const VISUAL_MODE_3D = '3D_STYLIZED_REALISM';
export const VISUAL_MODE_PHOTOREAL = 'PHOTOREALISTIC';
export const VISUAL_MODE_LIVE = 'LIVE_ACTION';
export const VISUAL_MODE_ANIME = 'ANIME';
export const VISUAL_MODE_ILLUSTRATION = 'ILLUSTRATION';
export const VISUAL_MODE_CUSTOM = 'CUSTOM';

export const VISUAL_MODES = [
  {
    id: VISUAL_MODE_3D,
    label: '3D Stylized Realism',
    hint: 'Nhân vật 3D, cinematic stylized, nhất quán Character / Scene / Video',
  },
  {
    id: VISUAL_MODE_PHOTOREAL,
    label: 'Photorealistic',
    hint: 'Người thật / hình ảnh chân thực',
  },
  {
    id: VISUAL_MODE_LIVE,
    label: 'Live Action',
    hint: 'Phong cách quay người thật',
  },
  {
    id: VISUAL_MODE_ANIME,
    label: 'Anime',
    hint: 'Phong cách anime',
  },
  {
    id: VISUAL_MODE_ILLUSTRATION,
    label: 'Illustration',
    hint: 'Minh họa',
  },
  {
    id: VISUAL_MODE_CUSTOM,
    label: 'Custom',
    hint: 'Tùy chỉnh — cần authority revision',
  },
] as const;

export type VisualModeId = (typeof VISUAL_MODES)[number]['id'];

export type ProjectVisualModeContract = {
  projectId: string;
  visualMode: string;
  visualUniverse: string;
  visualStyle: string;
  photorealismCeiling: string;
  characterRenderingMode: string;
  sceneRenderingMode: string;
  videoRenderingMode: string;
  referencePolicy: string;
  version: string;
  sha256: string;
  status: string;
};

export type VisualReferenceClaim = {
  projectId?: string;
  characterId: string;
  name: string;
  visualMode: string;
  visualUniverse?: string;
  visualStyle?: string;
  referenceRole: string;
  authorityStatus: string;
  referenceStatus: string;
  source: 'CHARACTER_STUDIO' | 'CANON_SEED' | 'SCENE' | string;
  artifactSha256?: string | null;
};

export const FAMIXA_VISUAL_MODE: ProjectVisualModeContract = {
  projectId: 'FAMIXA',
  visualMode: VISUAL_MODE_3D,
  visualUniverse: 'FAMIXA',
  visualStyle: '3D Stylized Realism',
  photorealismCeiling: 'LOW',
  characterRenderingMode: '3D_CHARACTER',
  sceneRenderingMode: '3D_STYLIZED_SCENE',
  videoRenderingMode: '3D_STYLIZED_VIDEO',
  referencePolicy: 'LOCKED_STUDIO_ONLY',
  version: 'V1',
  sha256: 'project-visual-mode-famixa-v1',
  status: 'ACTIVE',
};

export function visualModeLabel(mode?: string | null) {
  const id = (mode || '').toUpperCase();
  return VISUAL_MODES.find((m) => m.id === id)?.label || mode || 'Chưa chọn';
}

export function visualModesCompatible(projectMode?: string | null, referenceMode?: string | null) {
  const a = (projectMode || '').toUpperCase();
  const b = (referenceMode || '').toUpperCase();
  return Boolean(a && b && a === b);
}

export function classifyVisualReference(
  claim: VisualReferenceClaim,
  projectMode = FAMIXA_VISUAL_MODE.visualMode,
) {
  const legacy = claim.source === 'CANON_SEED'
    || claim.referenceStatus === 'LEGACY'
    || claim.referenceStatus === 'HISTORICAL'
    || claim.referenceRole === 'LEGACY_CANON';
  if (legacy && !visualModesCompatible(projectMode, claim.visualMode)) {
    return {
      eligible: false,
      status: 'INELIGIBLE_FOR_PROJECT',
      code: 'LEGACY_VISUAL_MODE_CONFLICT',
      claim,
    };
  }
  if (!visualModesCompatible(projectMode, claim.visualMode)) {
    return {
      eligible: false,
      status: 'INELIGIBLE',
      code: 'VISUAL_MODE_CONFLICT',
      claim,
    };
  }
  if (claim.referenceRole === 'IDENTITY_ANCHOR' && (claim.authorityStatus || '').toUpperCase() !== 'LOCKED') {
    return {
      eligible: false,
      status: 'INELIGIBLE',
      code: 'CHARACTER_IDENTITY_NOT_AVAILABLE',
      claim,
    };
  }
  return { eligible: true, status: 'ACTIVE', code: undefined as string | undefined, claim };
}

export function resolveSceneVisualReferences(
  candidates: VisualReferenceClaim[],
  projectMode = FAMIXA_VISUAL_MODE.visualMode,
) {
  const judged = candidates.map((c) => classifyVisualReference(c, projectMode));
  const eligible = judged.filter((j) => j.eligible);
  const blocked = judged.filter((j) => !j.eligible);
  return {
    allowed: blocked.length === 0 && eligible.length > 0,
    code: blocked[0]?.code as string | undefined,
    eligible,
    blocked,
    attachable: eligible.map((e) => e.claim),
    geminiCalled: false,
    generation: false,
  };
}

export function legacyPhotorealCanonClaim(characterId: string, name: string): VisualReferenceClaim {
  return {
    projectId: 'FAMIXA',
    characterId,
    name,
    visualMode: VISUAL_MODE_PHOTOREAL,
    visualUniverse: 'FAMIXA',
    visualStyle: 'Photoreal Master Reference v1.0',
    referenceRole: 'LEGACY_CANON',
    authorityStatus: 'HISTORICAL',
    referenceStatus: 'LEGACY',
    source: 'CANON_SEED',
  };
}

export function lockedStudioClaim(
  characterId: string,
  name: string,
  sha?: string | null,
): VisualReferenceClaim {
  return {
    projectId: 'FAMIXA',
    characterId,
    name,
    visualMode: VISUAL_MODE_3D,
    visualUniverse: 'FAMIXA',
    visualStyle: '3D Stylized Realism',
    referenceRole: 'IDENTITY_ANCHOR',
    authorityStatus: 'LOCKED',
    referenceStatus: 'ACTIVE',
    source: 'CHARACTER_STUDIO',
    artifactSha256: sha,
  };
}

export function classifyExistingScenePipeline(
  used: VisualReferenceClaim[],
  projectMode = FAMIXA_VISUAL_MODE.visualMode,
) {
  const resolution = resolveSceneVisualReferences(used, projectMode);
  return resolution.allowed ? 'ACTIVE' : 'INVALID_REFERENCE_PIPELINE';
}

export function visualGenerationPreflight(input: {
  contract?: ProjectVisualModeContract | null;
  visualUniverse?: string | null;
  characterVisualMode?: string | null;
  characterIdentityAvailable?: boolean;
  references: VisualReferenceClaim[];
  compilerVisualMode?: string | null;
  providerPayloadVisualMode?: string | null;
}) {
  const contract = input.contract || FAMIXA_VISUAL_MODE;
  const checks: { id: string; ok: boolean; code?: string }[] = [];
  const push = (id: string, ok: boolean, code?: string) => checks.push({ id, ok, code });
  push('ProjectVisualModeExists', Boolean(contract.visualMode));
  push('VisualUniverseExists', Boolean(input.visualUniverse || contract.visualUniverse));
  push(
    'CharacterVisualModeCompatible',
    !input.characterVisualMode || visualModesCompatible(contract.visualMode, input.characterVisualMode),
    'VISUAL_MODE_CONFLICT',
  );
  push('CharacterIdentityAvailable', input.characterIdentityAvailable !== false, 'CHARACTER_IDENTITY_NOT_AVAILABLE');
  const resolved = resolveSceneVisualReferences(input.references, contract.visualMode);
  push('ReferenceVisualModeCompatible', resolved.blocked.length === 0, resolved.code);
  push(
    'ReferenceAuthorityValid',
    !input.references.some((r) => r.referenceRole === 'IDENTITY_ANCHOR' && r.authorityStatus !== 'LOCKED'),
    'CHARACTER_IDENTITY_NOT_AVAILABLE',
  );
  push('NoLegacyConflict', !resolved.blocked.some((b) => b.code === 'LEGACY_VISUAL_MODE_CONFLICT'), 'LEGACY_VISUAL_MODE_CONFLICT');
  push(
    'CompilerVisualModeMatchesProject',
    !input.compilerVisualMode || visualModesCompatible(contract.visualMode, input.compilerVisualMode),
    'COMPILER_VISUAL_MODE_MISMATCH',
  );
  push(
    'ProviderPayloadMatchesProject',
    !input.providerPayloadVisualMode || visualModesCompatible(contract.visualMode, input.providerPayloadVisualMode),
    'PROVIDER_PAYLOAD_VISUAL_MODE_MISMATCH',
  );
  const fail = checks.find((c) => !c.ok);
  return {
    allowed: !fail && resolved.allowed,
    code: fail?.code || resolved.code,
    checks,
    providerCalled: false,
    geminiCalled: false,
    generation: false,
    resolved,
  };
}

export function mayChangeVisualModeDirectly() {
  return false;
}

export function pvsKeyOfVisualMode(mode?: string | null) {
  switch ((mode || '').toUpperCase()) {
    case VISUAL_MODE_PHOTOREAL:
      return 'PHOTOREALISTIC';
    case VISUAL_MODE_LIVE:
      return 'CINEMATIC_REALISM';
    case VISUAL_MODE_ANIME:
      return 'ANIMATION';
    case VISUAL_MODE_ILLUSTRATION:
      return '3D_CARTOON';
    default:
      return VISUAL_MODE_3D;
  }
}

export function existingScenePipelineStatus(run?: {
  keyframeDataUrl?: string;
  visualMode?: string;
  visualPipelineStatus?: string;
} | null) {
  if (!run?.keyframeDataUrl) return undefined;
  if (
    run.visualPipelineStatus === 'ACTIVE'
    && visualModesCompatible(FAMIXA_VISUAL_MODE.visualMode, run.visualMode)
  ) {
    return 'ACTIVE';
  }
  return 'INVALID_REFERENCE_PIPELINE';
}
