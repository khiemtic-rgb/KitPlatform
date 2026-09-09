/** KIT Video Engine Phase 02 — generic Asset / Canon / Reference. Famixa = Project FAMIXA. */

export const KIT_VIDEO_ASSET = 'KIT-VIDEO-ASSET-V1';

export const ASSET_KINDS = [
  'CHARACTER',
  'LOCATION',
  'PRODUCT',
  'PROP',
  'VEHICLE',
  'ANIMAL',
  'OTHER',
  'WARDROBE',
] as const;

export type KitVideoAssetKind = (typeof ASSET_KINDS)[number];

export const ASSET_LIFECYCLE = ['DRAFT', 'CREATING', 'REVIEW', 'APPROVED', 'LOCKED', 'ARCHIVED'] as const;
export type KitVideoAssetLifecycle = (typeof ASSET_LIFECYCLE)[number];

export const REFERENCE_PRIORITY = [
  'LOCKED_CANON',
  'LOCKED_MASTER_REFERENCE',
  'SCENE_MASTER',
  'PREVIOUS_APPROVED_SHOT',
  'SHOT_CONTEXT',
  'PROMPT',
] as const;

export type KitVideoAssetCanon = {
  identity?: { name?: string; gender?: string; age?: number; era?: string; visual?: string };
  face?: Record<string, string>;
  hair?: Record<string, string>;
  body?: Record<string, string>;
  skin?: Record<string, string>;
  wardrobe?: { defaultId?: string };
  personality?: string[];
  visualStyle?: string;
  voice?: Record<string, string>;
  architecture?: string;
  layout?: string;
  furniture?: string[];
  lighting?: string;
  objects?: string[];
};

export type KitVideoAssetRef = {
  kind: string;
  path: string;
  isPrimary?: boolean;
  isSecondary?: boolean;
  qaStatus?: string;
};

export type KitVideoAsset = {
  assetCode: string;
  assetKind: KitVideoAssetKind | string;
  name: string;
  lifecycle: KitVideoAssetLifecycle | string;
  version: string;
  era: string;
  canon: KitVideoAssetCanon;
  references: KitVideoAssetRef[];
  eras?: { era: string; age?: number; status: string }[];
};

export type KitVideoShotSpec = {
  shotCode: string;
  sceneCode: string;
  era?: string;
  requiredCharacters: string[];
  requiredLocation?: string;
  requiredProps?: string[];
  requiredWardrobe?: Record<string, string>;
  action?: string;
  camera?: string;
  composition?: string;
  speaker?: string;
  emotion?: string;
  dialogue?: string;
  i2vImageSource?: 'APPROVED_KEYFRAME' | 'CHARACTER_CROP' | 'CHARACTER_SHEET';
};

export type KitVideoPreviousState = {
  shotCode: string;
  approved: boolean;
  characters: Record<string, { wardrobe?: string; position?: string; emotion?: string }>;
  location?: string;
  lighting?: string;
  props?: string[];
};

export type KitVideoSceneMaster = {
  sceneCode: string;
  status: 'DRAFT' | 'LOCKED';
  locationCode: string;
  lighting?: string;
  color?: string;
  layout?: string;
  visualStyle?: string;
  presence: { characterCode: string; position?: string; wardrobe?: string; emotion?: string; actionState?: string }[];
};

export type KitVideoShotPackage = {
  shotCode: string;
  sceneCode: string;
  characters: KitVideoAsset[];
  locations: KitVideoAsset[];
  props: KitVideoAsset[];
  wardrobe: KitVideoAsset[];
  references: { role: string; path: string; source: string }[];
  previous?: KitVideoPreviousState | null;
  sceneMaster?: KitVideoSceneMaster | null;
  blocked: string[];
  missing: { name: string; reason: string }[];
};

export function canUseInProduction(life: string) {
  const s = (life || '').toUpperCase();
  return s === 'APPROVED' || s === 'LOCKED';
}

export function holdLockedCanon(life: string) {
  return (life || '').toUpperCase() === 'LOCKED';
}

export function nextAssetVersion(current: string) {
  const n = Number((current || 'V1').replace(/\D/g, '')) || 1;
  return `V${n + 1}`;
}

export function sameCharacterIdentity(a: KitVideoAsset, b: KitVideoAsset) {
  return a.assetCode === b.assetCode && a.assetKind === 'CHARACTER';
}

export function eraAge(asset: KitVideoAsset, era: string) {
  const row = asset.eras?.find((e) => e.era === era);
  if (row?.age != null) return row.age;
  if (asset.era === era) return asset.canon.identity?.age;
  return undefined;
}

export function inheritWardrobe(prev: KitVideoPreviousState | undefined, code: string, scriptChange?: string) {
  if (scriptChange) return scriptChange;
  return prev?.characters[code]?.wardrobe;
}

export function evaluateReferenceQa(input: {
  readable?: boolean;
  width?: number;
  height?: number;
  faceVisible?: boolean;
  occlusion?: boolean;
  watermark?: boolean;
  unexpectedText?: boolean;
  aspectOk?: boolean;
}) {
  const reasons: string[] = [];
  if (input.readable === false) reasons.push('Not readable.');
  if ((input.width ?? 64) < 64 || (input.height ?? 64) < 64) reasons.push('Resolution too low.');
  if (input.faceVisible === false) reasons.push('Face not visible.');
  if (input.occlusion) reasons.push('Severe occlusion.');
  if (input.watermark) reasons.push('Watermark.');
  if (input.unexpectedText) reasons.push('Unexpected text.');
  if (input.aspectOk === false) reasons.push('Wrong aspect.');
  return { ok: reasons.length === 0, status: reasons.length ? 'BLOCK' : 'PASS', reasons };
}

export function resolveShotPackage(input: {
  catalog: KitVideoAsset[];
  spec: KitVideoShotSpec;
  sceneMaster?: KitVideoSceneMaster | null;
  previous?: KitVideoPreviousState | null;
  aliases?: Record<string, string>;
}): KitVideoShotPackage {
  const blocked: string[] = [];
  const missing: { name: string; reason: string }[] = [];
  const aliases = input.aliases || {};
  const find = (codeOrName: string) => {
    const mapped = aliases[codeOrName] || codeOrName;
    return input.catalog.find(
      (a) =>
        a.assetCode === mapped ||
        a.name.toLowerCase() === mapped.toLowerCase() ||
        a.assetCode === codeOrName,
    );
  };

  const characters: KitVideoAsset[] = [];
  for (const raw of input.spec.requiredCharacters) {
    const hit = find(raw);
    if (!hit) {
      missing.push({ name: raw, reason: 'MISSING ASSET → CREATION REQUIRED' });
      blocked.push(`MISSING:${raw}`);
      continue;
    }
    if (!canUseInProduction(hit.lifecycle)) {
      blocked.push(`NOT_APPROVED:${hit.assetCode}`);
      continue;
    }
    const era = input.spec.era || hit.era;
    if (era && hit.era && era !== hit.era && !hit.eras?.some((e) => e.era === era && e.status !== 'DRAFT')) {
      if (hit.eras?.some((e) => e.era === era && e.status === 'DRAFT')) {
        blocked.push(`ERA_NOT_READY:${hit.assetCode}:${era}`);
      }
    }
    if (input.spec.era === 'ERA-01' && (hit.canon.identity?.age ?? 11) !== 11 && hit.assetCode === 'CHAR-001') {
      blocked.push('ERA_DRIFT:CHAR-001');
    }
    characters.push(hit);
  }

  const locations: KitVideoAsset[] = [];
  if (input.spec.requiredLocation) {
    const loc = find(input.spec.requiredLocation);
    if (!loc) {
      missing.push({ name: input.spec.requiredLocation, reason: 'MISSING LOCATION' });
      blocked.push(`MISSING:${input.spec.requiredLocation}`);
    } else if (!canUseInProduction(loc.lifecycle)) blocked.push(`NOT_APPROVED:${loc.assetCode}`);
    else locations.push(loc);
  }

  const props: KitVideoAsset[] = [];
  for (const raw of input.spec.requiredProps || []) {
    const hit = find(raw);
    if (!hit) {
      missing.push({ name: raw, reason: 'MISSING PROP' });
      blocked.push(`MISSING:${raw}`);
    } else if (!canUseInProduction(hit.lifecycle)) blocked.push(`NOT_APPROVED:${hit.assetCode}`);
    else props.push(hit);
  }

  const wardrobe: KitVideoAsset[] = [];
  const wardrobeCodes = new Set<string>();
  for (const ch of characters) {
    const fromScript = input.spec.requiredWardrobe?.[ch.assetCode];
    const inherited = inheritWardrobe(input.previous || undefined, ch.assetCode, fromScript);
    const code = inherited || ch.canon.wardrobe?.defaultId;
    if (code) wardrobeCodes.add(code);
  }
  for (const code of wardrobeCodes) {
    const hit = find(code);
    if (!hit) {
      missing.push({ name: code, reason: 'MISSING WARDROBE' });
      blocked.push(`MISSING:${code}`);
    } else wardrobe.push(hit);
  }

  if (input.sceneMaster && input.sceneMaster.status !== 'LOCKED' && input.sceneMaster.status !== 'DRAFT') {
    blocked.push('SCENE_MASTER');
  }
  if (!input.sceneMaster) blocked.push('SCENE_MASTER_MISSING');

  const refs: { role: string; path: string; source: string }[] = [];
  for (const ch of characters) {
    const primary = ch.references.find((r) => r.isPrimary) || ch.references[0];
    if (primary) refs.push({ role: ch.assetCode, path: primary.path, source: 'LOCKED_MASTER_REFERENCE' });
  }
  if (input.sceneMaster) refs.push({ role: 'SCENE_MASTER', path: input.sceneMaster.locationCode, source: 'SCENE_MASTER' });
  if (input.previous?.approved) refs.push({ role: 'PREVIOUS', path: input.previous.shotCode, source: 'PREVIOUS_APPROVED_SHOT' });

  return {
    shotCode: input.spec.shotCode,
    sceneCode: input.spec.sceneCode,
    characters,
    locations,
    props,
    wardrobe,
    references: refs,
    previous: input.previous || null,
    sceneMaster: input.sceneMaster || null,
    blocked,
    missing,
  };
}

export function assetPreflight(pkg: KitVideoShotPackage, spec: KitVideoShotSpec) {
  const checks: { id: string; ok: boolean }[] = [
    { id: 'character_exists', ok: spec.requiredCharacters.every((c) => pkg.characters.some((x) => x.assetCode === c || x.name === c) || pkg.missing.some((m) => m.name === c)) && pkg.missing.length === 0 },
    { id: 'character_approved', ok: pkg.characters.every((c) => canUseInProduction(c.lifecycle)) },
    { id: 'reference_exists', ok: pkg.characters.every((c) => c.assetKind !== 'CHARACTER' || c.references.some((r) => r.path)) },
    { id: 'location_exists', ok: !spec.requiredLocation || pkg.locations.length > 0 },
    { id: 'location_approved', ok: pkg.locations.every((l) => canUseInProduction(l.lifecycle)) },
    { id: 'props_resolved', ok: (spec.requiredProps || []).every((p) => pkg.props.some((x) => x.assetCode === p || x.name === p)) },
    { id: 'wardrobe_resolved', ok: pkg.characters.every((c) => !!inheritWardrobe(pkg.previous || undefined, c.assetCode, spec.requiredWardrobe?.[c.assetCode]) || !!c.canon.wardrobe?.defaultId) },
    { id: 'scene_master', ok: !!pkg.sceneMaster },
    { id: 'previous_if_required', ok: spec.shotCode.endsWith('-01') || !!pkg.previous?.approved },
  ];
  const blocked = pkg.blocked.length > 0 || checks.some((c) => !c.ok) || pkg.missing.length > 0;
  return { ok: !blocked, blocked, checks, missing: pkg.missing };
}

export function compileImagePrompt(pkg: KitVideoShotPackage, spec: KitVideoShotSpec, projectStyle?: string) {
  if (spec.i2vImageSource && spec.i2vImageSource !== 'APPROVED_KEYFRAME') {
    return { ok: false as const, blocked: 'I2V must use APPROVED KEYFRAME — not character crop/sheet.', prompt: '' };
  }
  const layers = [
    `ProjectStyle: ${projectStyle || 'project visual'}`,
    ...pkg.characters.map((c) => `CharacterCanon ${c.assetCode}: age=${c.canon.identity?.age} face=${c.canon.face?.shape || ''} hair=${c.canon.hair?.style || ''}`),
    ...pkg.references.filter((r) => r.source !== 'PROMPT').map((r) => `Ref ${r.source} ${r.role}=${r.path}`),
    ...pkg.locations.map((l) => `LocationCanon ${l.assetCode}: ${l.canon.layout || ''} furniture=${(l.canon.furniture || []).join(',')}`),
    pkg.sceneMaster ? `SceneMaster ${pkg.sceneMaster.sceneCode} light=${pkg.sceneMaster.lighting || ''}` : '',
    pkg.previous?.approved ? `PreviousShot ${pkg.previous.shotCode}` : '',
    spec.action ? `ShotAction ${spec.action}` : '',
    spec.camera ? `Camera ${spec.camera}` : '',
    spec.composition ? `Composition ${spec.composition}` : '',
    spec.speaker ? `Speaker ${spec.speaker}` : '',
    spec.emotion ? `Emotion ${spec.emotion}` : '',
  ].filter(Boolean);
  const prompt = layers.join('\n');
  const hasDialogue = !!(spec.dialogue && prompt.includes(spec.dialogue));
  return { ok: true as const, prompt, hasDialogue, layers, i2vSource: 'APPROVED_KEYFRAME' as const };
}

export function evaluateKeyframeQa(input: {
  requiredCharacters: string[];
  detectedCharacters: string[];
  requiredProps?: string[];
  detectedProps?: string[];
  actionNeeds?: string[];
  visible?: { halfFace?: boolean; cutOffHead?: boolean; missingHands?: boolean; propOutsideFrame?: boolean };
}) {
  const reasons: string[] = [];
  if (input.detectedCharacters.length !== input.requiredCharacters.length) {
    reasons.push(`Detected Characters = ${input.detectedCharacters.length}, required ${input.requiredCharacters.length}.`);
  }
  for (const c of input.requiredCharacters) {
    if (!input.detectedCharacters.includes(c)) reasons.push(`Missing character ${c}.`);
  }
  for (const p of input.requiredProps || []) {
    if (!(input.detectedProps || []).includes(p)) reasons.push(`Missing detail ${p}.`);
  }
  for (const need of input.actionNeeds || []) {
    const hit =
      input.detectedCharacters.includes(need) ||
      (input.detectedProps || []).includes(need) ||
      need === 'interaction';
    if (!hit && need !== 'interaction') reasons.push(`Action missing ${need}.`);
  }
  if (input.actionNeeds?.includes('interaction') && input.detectedCharacters.length < 2) {
    reasons.push('Action interaction not visible.');
  }
  const v = input.visible || {};
  if (v.halfFace) reasons.push('half face');
  if (v.cutOffHead) reasons.push('cut-off head');
  if (v.missingHands) reasons.push('missing hands');
  if (v.propOutsideFrame) reasons.push('important prop outside frame');
  return { ok: reasons.length === 0, status: reasons.length ? 'FAIL' : 'PASS', reasons, allowI2v: reasons.length === 0 };
}

export function canonChangeImpact(usageCount: number) {
  return {
    warn: usageCount > 0,
    message:
      usageCount > 0
        ? `Asset này đang được sử dụng trong ${usageCount} Shot. Không được âm thầm thay đổi toàn bộ production.`
        : 'Asset chưa gắn shot.',
  };
}

export function mutateApprovedSnapshot(
  snapshots: Record<string, KitVideoPreviousState>,
  regenerateShot: string,
): Record<string, KitVideoPreviousState> {
  const next = { ...snapshots };
  if (!next[regenerateShot]) return next;
  next[regenerateShot] = { ...next[regenerateShot], approved: false };
  return next;
}

export function formatAssetLine(asset: Pick<KitVideoAsset, 'assetCode' | 'name' | 'lifecycle' | 'version'>) {
  return `${asset.assetCode} ${asset.name} ${asset.version} ${asset.lifecycle}`;
}
