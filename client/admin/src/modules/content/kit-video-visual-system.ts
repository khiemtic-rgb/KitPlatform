/** KIT Video Engine — generic Visual Style System. Project values, not a Famixa-only engine. */

export const KIT_VIDEO_VISUAL_SYSTEM = 'KIT-VIDEO-VISUAL-SYSTEM-V1';
export const VISUAL_STYLE_SYSTEM_CODE = 'VISUAL_STYLE_SYSTEM';
export const VISUAL_SYSTEM_V1 = 'V1';

export type VisualSystemStatus = 'draft' | 'proposed' | 'locked' | 'superseded';
export type VisualFrameKind = 'PRODUCTION_STILL' | 'CHARACTER_SHEET';
export type VisualLayer = 'SYSTEM_RULE' | 'CHARACTER_CANON' | 'LOCATION_CANON' | 'PROP_CANON' | 'WARDROBE_CANON' | 'SCENE_REQUIREMENT' | 'GENERATION_PROMPT';

export const PROMPT_HIERARCHY: VisualLayer[] = [
  'CHARACTER_CANON',
  'LOCATION_CANON',
  'PROP_CANON',
  'WARDROBE_CANON',
  'SCENE_REQUIREMENT',
  'GENERATION_PROMPT',
];

export type VisualSystemDoc = {
  documentId: string;
  systemCode: typeof VISUAL_STYLE_SYSTEM_CODE;
  version: typeof VISUAL_SYSTEM_V1;
  status: VisualSystemStatus;
  projectCode: string;
  providerIndependent: true;
  providers: [];
  purpose: string;
  principle: string;
  designDirection: string[];
  avoid: string[];
  axis: { realism: string[]; stylization: string[] };
  characterLanguage: string[];
  silhouette: string[];
  facialLanguage: string[];
  emotionalSignals: string[];
  cinematic: string[];
  shots: string[];
  lighting: string[];
  color: { base: string; scene: string; emotion: string };
  environment: string[];
  props: string[];
  wardrobe: string[];
  familyFraming: string[];
  negativeSpace: string[];
  era: { identityPersists: true; example: string };
  masterReference: { required: true; notI2vSource: true };
  productionStill: { oneFrame: true; forbidden: string[] };
  forbiddenDrift: string[];
  forbiddenGeneration: string[];
  promptHierarchy: VisualLayer[];
  qualityVsIdentity: string;
  distinctiveness: string;
  reusability: string[];
  versioning: string;
};

export const FAMIXA_VISUAL_STYLE_SYSTEM_V1: VisualSystemDoc = {
  documentId: 'FAMIXA_VISUAL_STYLE_SYSTEM_V1',
  systemCode: VISUAL_STYLE_SYSTEM_CODE,
  version: VISUAL_SYSTEM_V1,
  status: 'proposed',
  projectCode: 'FAMIXA',
  providerIndependent: true,
  providers: [],
  purpose:
    'Recognizable as Famixa, not merely recognizable as AI. Character identity, emotion, consistency, cinematic story, long-term reuse. Do not optimize for photorealism.',
  principle: 'Do not compete with generic photorealistic AI films. Build a recognizable visual world.',
  designDirection: [
    'cinematic',
    'emotionally expressive',
    'stylized',
    'warm but able to become heavy',
    'visually distinctive',
    'believable for serious story',
    'not generic live-action AI',
    'suitable for children and adults',
    'coherent fictional world',
  ],
  avoid: [
    'generic AI influencers',
    'generic photorealistic humans',
    'stock cinematic AI',
    'random anime',
    'children cartoon',
    'advertising CGI',
    'hyper-realistic virtual humans',
    'imitate a living artist / studio / franchise',
  ],
  axis: {
    realism: ['believable anatomy', 'believable lighting', 'believable environments'],
    stylization: ['recognizable character shapes', 'expressive faces', 'controlled proportions', 'distinctive identity'],
  },
  characterLanguage: [
    'face structure',
    'head shape',
    'eye structure',
    'nose structure',
    'mouth structure',
    'hair shape',
    'hairline',
    'body proportions',
    'height relationship',
    'silhouette',
    'age',
    'distinctive physical characteristics',
    'wardrobe system',
    'emotional expression language',
  ],
  silhouette: [
    'head-to-body proportion',
    'shoulder width',
    'body shape',
    'hairstyle silhouette',
    'posture',
    'relative height',
    'distinctive visual features',
  ],
  facialLanguage: [
    'neutral',
    'happiness',
    'sadness',
    'disappointment',
    'confusion',
    'fear',
    'frustration',
    'anger',
    'embarrassment',
    'loneliness',
    'affection',
    'relief',
    'hope',
  ],
  emotionalSignals: [
    'gaze direction',
    'physical distance',
    'body orientation',
    'posture',
    'hands',
    'facial tension',
    'framing',
    'negative space',
    'lighting',
    'environment',
  ],
  cinematic: [
    'intentional framing',
    'visual hierarchy',
    'meaningful negative space',
    'controlled depth',
    'motivated camera only',
    'close-up for emotion',
    'medium for relationship',
    'wide for isolation/context',
  ],
  shots: ['WIDE', 'MEDIUM', 'CLOSE', 'EXTREME CLOSE'],
  lighting: [
    'coherent with location',
    'coherent with time of day',
    'coherent with emotion',
    'no random neon',
    'no unmotivated flares',
    'no light-direction jump without story',
  ],
  color: {
    base: 'controlled Famixa palette',
    scene: 'scene palette inherits base',
    emotion: 'intentional modulation only',
  },
  environment: ['stable layout', 'stable furniture', 'stable architecture', 'stable spatial relationships', 'reusable Canon'],
  props: ['identity', 'appearance', 'state', 'location', 'ownership', 'continuity'],
  wardrobe: ['continuity asset', 'explicit change only', 'depends on character/scene/location/time'],
  familyFraming: ['distance', 'proximity', 'body orientation', 'shared frame', 'negative space as story'],
  negativeSpace: ['loneliness', 'distance', 'silence', 'isolation', 'waiting', 'uncertainty'],
  era: { identityPersists: true, example: 'CHAR-001 ERA-01/11 ERA-02/16 ERA-03/23 = one identity' },
  masterReference: { required: true, notI2vSource: true },
  productionStill: {
    oneFrame: true,
    forbidden: ['character sheet', 'collage', 'multiple panels', 'storyboard', 'reference board', 'text-heavy', 'logo', 'watermark'],
  },
  forbiddenDrift: [
    'face redesign',
    'hairstyle redesign',
    'body proportion drift',
    'age drift',
    'wardrobe drift',
    'location drift',
    'major prop drift',
    'unexpected character',
    'missing character',
    'random style change',
    'photorealism drift',
    'cartoon drift',
    'anime drift',
    'inconsistent lighting',
    'inconsistent rendering',
  ],
  forbiddenGeneration: [
    'invent characters',
    'invent family members',
    'invent important props',
    'invent locations',
    'change character identity',
    'character sheet when production still requested',
    'add text',
    'add logos',
    'add watermarks',
    'multiple panels',
    'change wardrobe without instruction',
    'change age without instruction',
    'change location without instruction',
  ],
  promptHierarchy: ['CHARACTER_CANON', 'LOCATION_CANON', 'PROP_CANON', 'WARDROBE_CANON', 'SCENE_REQUIREMENT', 'GENERATION_PROMPT'],
  qualityVsIdentity: 'Beautiful + wrong identity/location/extra person = FAIL. Beauty score is not a hard gate.',
  distinctiveness: 'Recognizable as Famixa via character + stylized anatomy + emotion + composition + world — not random effects.',
  reusability: ['episodes', 'characters', 'locations', 'props', 'stories', 'eras', 'future providers'],
  versioning: 'V1 is immutable once locked. Fundamental change = V2 with revision history. Do not silently edit V1.',
};

export function loadVisualSystem(projectCode: string, version = VISUAL_SYSTEM_V1) {
  if (projectCode !== 'FAMIXA' || version !== VISUAL_SYSTEM_V1) {
    throw new Error('VISUAL_SYSTEM: version/project not registered.');
  }
  return FAMIXA_VISUAL_STYLE_SYSTEM_V1;
}

export function visualSystemBelongsToProject(doc: VisualSystemDoc, projectCode: string) {
  return doc.projectCode === projectCode;
}

export function ensureVisualSystemImmutable(current: { status: string; version: string }, next: { version: string }) {
  if ((current.status || '').toLowerCase() === 'locked' && current.version === next.version) {
    throw new Error('VISUAL_SYSTEM_LOCKED: V1 không sửa im lặng. Tạo V2.');
  }
}

export function enforcePromptHierarchy(input: {
  prompt: string;
  lockedCharacter?: { hair?: string; face?: string; identity?: string };
  lockedLocation?: { name?: string };
  lockedWardrobe?: { id?: string };
}) {
  const blocked: string[] = [];
  const p = (input.prompt || '').toLowerCase();
  if (input.lockedCharacter?.hair && /blonde|nhuộm|đổi tóc|new hairstyle|long hair/i.test(input.prompt) && !p.includes((input.lockedCharacter.hair || '').toLowerCase().slice(0, 8))) {
    blocked.push('PROMPT_OVERRIDE: locked Character Canon (hair)');
  }
  if (input.lockedCharacter?.identity && /new character|đổi mặt|redesign face/i.test(input.prompt)) {
    blocked.push('PROMPT_OVERRIDE: locked Character Canon (identity)');
  }
  if (input.lockedLocation?.name && /beach villa|đổi phòng|new living room|another house/i.test(input.prompt)) {
    blocked.push('PROMPT_OVERRIDE: locked Location Canon');
  }
  if (input.lockedWardrobe?.id && /change (clothes|outfit|wardrobe)|đổi áo/i.test(input.prompt)) {
    blocked.push('PROMPT_OVERRIDE: locked Wardrobe Canon');
  }
  return { ok: blocked.length === 0, blocked };
}

export function distinguishFrameKind(kind: VisualFrameKind) {
  return {
    kind,
    isProductionStill: kind === 'PRODUCTION_STILL',
    isCharacterSheet: kind === 'CHARACTER_SHEET',
    i2vAllowed: kind === 'PRODUCTION_STILL',
    forbiddenInProduction: kind === 'CHARACTER_SHEET' ? FAMIXA_VISUAL_STYLE_SYSTEM_V1.productionStill.forbidden : [],
  };
}

export function eraBelongsToCharacterIdentity(characterId: string, era: string) {
  const id = (characterId || '').toUpperCase();
  const e = (era || '').toUpperCase();
  if (id === 'CHAR-001' && (e === 'ERA-01' || e === 'ERA-02' || e === 'ERA-03' || e === 'A11' || e === 'A16' || e === 'A23')) {
    return { ok: true as const, characterId: 'CHAR-001', era: e, newCharacter: false };
  }
  if (/^CHAR-00[5-9]/.test(id) || /ông|bà|grandfather/i.test(id)) {
    return { ok: false as const, newCharacter: true, blocked: 'Do not invent a new Character Identity for an era.' };
  }
  return { ok: true as const, characterId: id, era: e, newCharacter: false };
}

export function missingAssetIsBlocked(missing: string[]) {
  if (!missing.length) return { ok: true as const, blocked: [] as string[] };
  return { ok: false as const, blocked: missing.map((m) => `BLOCKED / CREATION REQUIRED: ${m}`) };
}

export function isProviderIndependent(doc: VisualSystemDoc) {
  const blob = JSON.stringify(doc);
  return doc.providerIndependent === true && doc.providers.length === 0 && !/gemini|runway|elevenlabs|\bfal\b/i.test(blob);
}

export function visualSystemRef(projectCode: string, version = VISUAL_SYSTEM_V1) {
  return { projectCode, systemCode: VISUAL_STYLE_SYSTEM_CODE, version, layer: 'SYSTEM_RULE' as const };
}
