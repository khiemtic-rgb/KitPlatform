/** KIT Video Engine Phase 04 — Visual Contract + Prompt Compiler + Vision QA. Gemini is a provider, not the engine. No Runway. */

export const KIT_VIDEO_VISION = 'KIT-VIDEO-VISION-V1';
export const MAX_AUTO_ATTEMPTS = 3;

export type KitVideoRequirementLevel = 'MANDATORY' | 'OPTIONAL' | 'FORBIDDEN';
export type KitVideoQaPriority = 'P0' | 'P1' | 'P2';
export type KitVideoVisionStatus = 'PASS' | 'WARNING' | 'FAIL';
export type KitVideoRepairClass =
  | 'PROMPT_ERROR'
  | 'REFERENCE_ERROR'
  | 'ASSET_ERROR'
  | 'COMPOSITION_ERROR'
  | 'GENERATION_ERROR'
  | 'QA_ERROR';

export type KitVideoVisualItem = {
  id: string;
  label: string;
  level: KitVideoRequirementLevel;
  priority: KitVideoQaPriority;
};

export type KitVideoSpatialContract = {
  positions: Record<string, string>;
  facing: Record<string, string>;
  relative?: string;
  propPositions: Record<string, string>;
};

export type KitVideoCompositionContract = {
  shotSize: string;
  cameraAngle: string;
  cameraPosition: string;
  framing: string;
  headroom: string;
  actionArea: string;
  bothCharactersVisible?: boolean;
  handsVisible?: boolean;
  propVisible?: boolean;
};

export type KitVideoVisualContract = {
  shotCode: string;
  action: string;
  items: KitVideoVisualItem[];
  characters: { code: string; name: string; version: string; era: string; reference?: string; level: KitVideoRequirementLevel }[];
  props: { code: string; name: string; state: string; level: KitVideoRequirementLevel }[];
  location: { code: string; name: string; time?: string; lighting?: string; level: KitVideoRequirementLevel };
  wardrobe: Record<string, string>;
  spatial: KitVideoSpatialContract;
  composition: KitVideoCompositionContract;
  actionVisibility: string[];
  forbidden: string[];
  productionImage?: {
    imageType: 'PRODUCTION_STILL';
    singleFrame: true;
    singleScene: true;
    singleComposition: true;
    noPanels: true;
    noCharacterSheet: true;
    noCollage: true;
    noStoryboard: true;
    noWatermark: true;
    noLogo: true;
    noUnrequestedText: true;
  };
};

export type KitVideoVisionObservation = {
  characters: string[];
  props: { code: string; state: string }[];
  location: string;
  wardrobe: Record<string, string>;
  actionVisible: boolean;
  croppedOut: string[];
  missingHeads: string[];
  missingHandsRelevant: boolean;
  occludedFaces: string[];
  identityMatch: Record<string, boolean>;
  lightingDelta?: 'none' | 'minor' | 'major';
  imageType?: string;
  integrity: {
    readable: boolean;
    width: number;
    height: number;
    aspect: string;
    unexpectedText: boolean;
    watermark: boolean;
    artifact: boolean;
  };
};

export type KitVideoVisionQa = {
  status: KitVideoVisionStatus;
  scores: Record<string, number>;
  p0Fail: string[];
  warnings: string[];
  reasons: string[];
  allowI2v: boolean;
  canBeReference: boolean;
};

export type KitVideoGenerationRequest = {
  provider: 'GEMINI' | 'OPENAI' | 'FLUX' | 'OTHER';
  sections: Record<string, string>;
  prompt: string;
  references: { role: string; path: string; characterId?: string; version?: string; era?: string }[];
  i2vImageSource: 'APPROVED_KEYFRAME';
  fingerprint: string;
  hasDialogue: boolean;
};

export type KitVideoKeyframeAttempt = {
  attemptId: string;
  shotCode: string;
  attemptNo: number;
  fingerprint: string;
  status: 'GENERATED' | 'QA_PASS' | 'QA_FAIL' | 'REVIEW_REQUIRED' | 'REJECTED' | 'APPROVED';
  qa?: KitVideoVisionQa;
  repairClass?: KitVideoRepairClass;
  repair?: string;
  imagePath?: string;
};

export type KitVideoI2vReadyPackage = {
  shotCode: string;
  attemptId: string;
  source: 'APPROVED_KEYFRAME';
  ready: boolean;
  runwaySubmitted: false;
  blocked: string[];
};

const DIALOGUE_MARK = /[“"][^”"]+[”"]/;
const INTERNAL = /(voice_settings|lipsync|runway|idempotency|SELECT |INSERT |pack_content|tts|elevenlabs)/i;

export function compileVisualContract(input: {
  shotCode: string;
  action: string;
  characters: { code: string; name: string; version?: string; era?: string; reference?: string }[];
  props?: { code: string; name: string; state: string }[];
  location: { code: string; name: string; time?: string; lighting?: string };
  wardrobe?: Record<string, string>;
  purpose?: string;
  dialogue?: string;
}): KitVideoVisualContract {
  const twoShot = input.characters.length >= 2;
  const paper = input.props?.find((p) => p.code === 'PROP-001');
  const items: KitVideoVisualItem[] = [
    ...input.characters.map((c) => ({
      id: c.code,
      label: c.name,
      level: 'MANDATORY' as const,
      priority: 'P0' as const,
    })),
    ...(input.props || [])
      .filter((p) => p.state !== 'not_present')
      .map((p) => ({ id: p.code, label: p.name, level: 'MANDATORY' as const, priority: 'P0' as const })),
    {
      id: input.location.code,
      label: input.location.name,
      level: 'MANDATORY' as const,
      priority: 'P0' as const,
    },
    { id: 'WINDOW', label: 'Window', level: 'OPTIONAL' as const, priority: 'P2' as const },
    { id: 'EXTRA_PERSON', label: 'Extra person', level: 'FORBIDDEN' as const, priority: 'P0' as const },
    { id: 'EXTRA_PAPER', label: 'Extra test papers', level: 'FORBIDDEN' as const, priority: 'P1' as const },
    { id: 'DIFF_LOCATION', label: 'Different location', level: 'FORBIDDEN' as const, priority: 'P0' as const },
    { id: 'DIFF_WARDROBE', label: 'Different wardrobe', level: 'FORBIDDEN' as const, priority: 'P1' as const },
  ];
  if (paper && paper.state === 'held_by_minh') {
    items.push({
      id: 'HOLD_PROP-001',
      label: 'Minh holding test paper',
      level: 'MANDATORY',
      priority: 'P0',
    });
  }
  const minh = input.characters.find((c) => c.code === 'CHAR-001');
  const mother = input.characters.find((c) => c.code === 'CHAR-003');
  const actionVisibility = [
    ...input.characters.map((c) => c.name),
    ...(paper ? ['Test paper'] : []),
    ...(paper && /đưa|shows?|holds?|cầm|giơ/i.test(input.action) ? ["Minh's hand", 'Interaction'] : []),
    ...(mother && /đưa|shows?|faces?/i.test(input.action) ? ['Mother facing Minh'] : []),
  ];
  return {
    shotCode: input.shotCode,
    action: input.action,
    items,
    characters: input.characters.map((c) => ({
      code: c.code,
      name: c.name,
      version: c.version || 'V1',
      era: c.era || 'ERA-01',
      reference: c.reference,
      level: 'MANDATORY',
    })),
    props: (input.props || []).map((p) => ({ ...p, level: p.state === 'not_present' ? 'OPTIONAL' : 'MANDATORY' })),
    location: { ...input.location, level: 'MANDATORY' },
    wardrobe: input.wardrobe || {},
    spatial: {
      positions: {
        ...(minh ? { 'CHAR-001': 'left foreground' } : {}),
        ...(mother ? { 'CHAR-003': 'right midground' } : {}),
      },
      facing: {
        ...(minh && mother ? { 'CHAR-001': 'faces Mother', 'CHAR-003': 'faces Minh' } : {}),
      },
      relative: twoShot ? 'Minh near Mother, test paper between them' : undefined,
      propPositions: paper ? { 'PROP-001': paper.state === 'on_table' ? 'on table' : 'in Minh hands' } : {},
    },
    composition: {
      shotSize: twoShot ? 'Medium two-shot' : input.purpose === 'DETAIL' ? 'Close-up' : 'Medium',
      cameraAngle: 'Eye-level',
      cameraPosition: 'living room axis',
      framing: twoShot ? 'Both characters fully visible' : 'Subject fully visible',
      headroom: 'normal',
      actionArea: 'hands and prop in frame',
      bothCharactersVisible: twoShot,
      handsVisible: /đưa|cầm|holds?|shows?|giơ/i.test(input.action),
      propVisible: Boolean(paper),
    },
    actionVisibility,
    forbidden: [
      'Extra people',
      'Extra test papers',
      'Different location',
      'Different wardrobe',
      'Character sheet crop to I2V',
      'Character sheet',
      'Collage',
      'Multi-panel',
    ],
    productionImage: {
      imageType: 'PRODUCTION_STILL',
      singleFrame: true,
      singleScene: true,
      singleComposition: true,
      noPanels: true,
      noCharacterSheet: true,
      noCollage: true,
      noStoryboard: true,
      noWatermark: true,
      noLogo: true,
      noUnrequestedText: true,
    },
  };
}

export function compileVisualPrompt(
  contract: KitVideoVisualContract,
  extras?: { projectStyle?: string; dialogue?: string; continuityNote?: string },
): KitVideoGenerationRequest {
  const sections: Record<string, string> = {
    SUBJECT: contract.characters
      .map((c) => `${c.name} CHARACTER_ID=${c.code} CHARACTER_VERSION=${c.version} ERA=${c.era}${c.reference ? ` REFERENCE=${c.reference}` : ''}`)
      .join('; '),
    ACTION: contract.action,
    ENVIRONMENT: `${contract.location.name} ${contract.location.time || ''} ${contract.location.lighting || ''}`.trim(),
    COMPOSITION: `${contract.composition.shotSize}; ${contract.composition.framing}; ${contract.composition.actionArea}`,
    CAMERA: `${contract.composition.cameraAngle}; ${contract.composition.cameraPosition}`,
    LIGHTING: contract.location.lighting || 'warm indoor',
    STYLE: extras?.projectStyle || 'cinematic still, production-safe, no on-image text',
    CONTINUITY: extras?.continuityNote || Object.entries(contract.wardrobe)
      .map(([k, v]) => `${k} wardrobe ${v}`)
      .join('; '),
    NEGATIVE: contract.forbidden.join('; '),
    SPATIAL: [
      ...Object.entries(contract.spatial.positions).map(([k, v]) => `${k}=${v}`),
      ...Object.entries(contract.spatial.facing).map(([k, v]) => `${k} ${v}`),
      contract.spatial.relative || '',
    ]
      .filter(Boolean)
      .join('; '),
  };
  const prompt = Object.entries(sections)
    .map(([k, v]) => `${k}\n${v}`)
    .join('\n\n');
  if (extras?.dialogue && prompt.includes(extras.dialogue)) {
    throw new Error('Prompt không chứa thoại.');
  }
  if (DIALOGUE_MARK.test(prompt) || INTERNAL.test(prompt)) {
    throw new Error('Prompt chứa thoại hoặc instruction nội bộ.');
  }
  const references = contract.characters
    .filter((c) => c.reference)
    .map((c) => ({
      role: c.code,
      path: c.reference!,
      characterId: c.code,
      version: c.version,
      era: c.era,
    }));
  return {
    provider: 'GEMINI',
    sections,
    prompt,
    references,
    i2vImageSource: 'APPROVED_KEYFRAME',
    fingerprint: fingerprintOf(prompt, references.map((r) => r.path)),
    hasDialogue: false,
  };
}

export function evaluateVisionQa(contract: KitVideoVisualContract, obs: KitVideoVisionObservation): KitVideoVisionQa {
  const requiredType = contract.productionImage?.imageType ?? 'PRODUCTION_STILL';
  if (obs.imageType) {
    const got = obs.imageType.trim().toUpperCase().replace(/\s+/g, '_');
    const sheet = /CHARACTER_SHEET|COLLAGE|MULTI_PANEL|REFERENCE_BOARD|TEXT_HEAVY|INVALID_COMPOSITION|STORYBOARD/.test(got);
    if (requiredType === 'PRODUCTION_STILL' && (sheet || got !== 'PRODUCTION_STILL')) {
      return {
        status: 'FAIL',
        scores: { imageType: 0, character: 0, action: 0, location: 0, props: 0, composition: 0, continuity: 0, integrity: 0 },
        p0Fail: [`IMAGE_TYPE: ${got} required ${requiredType}`],
        warnings: [],
        reasons: [`IMAGE_TYPE: ${got} required ${requiredType}`],
        allowI2v: false,
        canBeReference: false,
      };
    }
  }

  const p0: string[] = [];
  const warnings: string[] = [];
  const reasons: string[] = [];
  const expected = contract.characters.map((c) => c.code);
  const detected = obs.characters;

  if (detected.length !== expected.length) {
    p0.push(`Character count: Expected ${expected.length} Detected ${detected.length}`);
  }
  for (const code of expected) {
    if (!detected.includes(code)) p0.push(`Missing: ${code}`);
  }
  for (const extra of detected.filter((d) => !expected.includes(d))) {
    p0.push(`Extra Character: ${extra}`);
  }
  if (!obs.actionVisible) p0.push('Action visibility: FAIL');

  for (const prop of contract.props.filter((p) => p.level === 'MANDATORY')) {
    const hit = obs.props.find((p) => p.code === prop.code);
    if (!hit) p0.push(`Missing: ${prop.code} ${prop.name}`);
    else if (hit.state !== prop.state) p0.push(`Wrong: ${prop.code} state ${hit.state} required ${prop.state}`);
  }

  if (obs.location && obs.location !== contract.location.code && obs.location !== contract.location.name) {
    p0.push(`Wrong: Location ${obs.location}`);
  }

  if (contract.composition.bothCharactersVisible) {
    for (const code of expected) {
      if (obs.croppedOut.includes(code)) p0.push(`Action crop: ${code} fully cropped`);
    }
  }
  if (contract.composition.shotSize !== 'Close-up') {
    for (const code of obs.missingHeads) p0.push(`Missing head: ${code}`);
  }
  if (contract.composition.handsVisible && obs.missingHandsRelevant) {
    p0.push("Important hand missing");
  }

  for (const [code, required] of Object.entries(contract.wardrobe)) {
    const got = obs.wardrobe[code];
    if (got && got !== required) reasons.push(`Wrong: ${code} wardrobe ${got}`);
  }
  const wardrobeFail = reasons.some((r) => r.includes('wardrobe'));
  if (wardrobeFail) p0.push(...reasons.filter((r) => r.includes('wardrobe')));

  for (const [code, ok] of Object.entries(obs.identityMatch)) {
    if (ok === false) p0.push(`Wrong: ${code} face identity`);
  }

  if (obs.lightingDelta === 'minor') warnings.push('Minor lighting variation');
  if (obs.lightingDelta === 'major') p0.push('Wrong: lighting continuity');

  if (!obs.integrity.readable) p0.push('Image integrity: not readable');
  if (obs.integrity.width < 512 || obs.integrity.height < 512) p0.push('Image integrity: resolution');
  if (obs.integrity.aspect && obs.integrity.aspect !== '16:9') warnings.push(`Aspect ${obs.integrity.aspect}`);
  if (obs.integrity.unexpectedText) p0.push('Image integrity: unexpected text');
  if (obs.integrity.watermark) p0.push('Image integrity: watermark');
  if (obs.integrity.artifact) warnings.push('Minor artifact');

  const scores = {
    character: clamp(100 - detected.filter((d) => !expected.includes(d)).length * 40 - expected.filter((e) => !detected.includes(e)).length * 40),
    location: obs.location === contract.location.code || obs.location === contract.location.name ? 95 : 20,
    composition: obs.croppedOut.length ? 40 : 91,
    action: obs.actionVisible ? 100 : 10,
    props: contract.props.every((p) => p.level !== 'MANDATORY' || obs.props.some((x) => x.code === p.code && x.state === p.state))
      ? 100
      : 15,
    wardrobe: wardrobeFail ? 20 : 97,
    continuity: Object.values(obs.identityMatch).some((v) => v === false) ? 10 : obs.lightingDelta === 'minor' ? 88 : 97,
    integrity: obs.integrity.readable && !obs.integrity.watermark && !obs.integrity.unexpectedText ? 96 : 20,
  };

  const status: KitVideoVisionStatus = p0.length ? 'FAIL' : warnings.length ? 'WARNING' : 'PASS';
  return {
    status,
    scores,
    p0Fail: p0,
    warnings,
    reasons: [...p0, ...warnings],
    allowI2v: status === 'PASS',
    canBeReference: status === 'PASS',
  };
}

export function diagnoseFailure(qa: KitVideoVisionQa): { repairClass: KitVideoRepairClass; repair: string } {
  const text = qa.p0Fail.join(' | ');
  if (/Missing: PROP|state /.test(text)) {
    return {
      repairClass: 'ASSET_ERROR',
      repair: 'Increase prop visibility and specify: "Minh is visibly holding the test paper in both hands."',
    };
  }
  if (/Extra Character|Character count/.test(text)) {
    return { repairClass: 'PROMPT_ERROR', repair: 'Tighten NEGATIVE: no extra people. Keep exact character count.' };
  }
  if (/Missing: CHAR|fully cropped/.test(text)) {
    return { repairClass: 'COMPOSITION_ERROR', repair: 'Widen framing so every required character is fully visible.' };
  }
  if (/face identity/.test(text)) {
    return { repairClass: 'REFERENCE_ERROR', repair: 'Re-lock character REFERENCE (FRONT) and previous Approved KF. Do not invent a new face.' };
  }
  if (/wardrobe/.test(text)) {
    return { repairClass: 'REFERENCE_ERROR', repair: 'Lock wardrobe from Continuity Snapshot. Do not redraw outfit from prompt.' };
  }
  if (/Location/.test(text)) {
    return { repairClass: 'REFERENCE_ERROR', repair: 'Re-attach Scene Master + Location REFERENCE. Do not change room identity.' };
  }
  if (/integrity|text|watermark/.test(text)) {
    return { repairClass: 'GENERATION_ERROR', repair: 'Regenerate without on-image text, logo, or watermark.' };
  }
  if (/Action visibility/.test(text)) {
    return { repairClass: 'PROMPT_ERROR', repair: 'Restate ACTION so the interaction is readable: both bodies, hands, and required prop in frame.' };
  }
  return { repairClass: 'QA_ERROR', repair: 'Director review required — diagnosis incomplete.' };
}

export function canRetry(prev: { fingerprint: string; failCode?: string }, nextFingerprint: string, strategyChanged: boolean) {
  if (prev.fingerprint === nextFingerprint && !strategyChanged) {
    return { ok: false as const, blocked: 'DO_NOT_BLIND_RETRY: same prompt + same references.' };
  }
  return { ok: true as const };
}

export function nextAttemptNo(existing: KitVideoKeyframeAttempt[], shotCode: string) {
  const n = existing.filter((a) => a.shotCode === shotCode).length;
  if (n >= MAX_AUTO_ATTEMPTS) return { attemptNo: n + 1, status: 'REVIEW_REQUIRED' as const };
  return { attemptNo: n + 1, status: 'GENERATED' as const };
}

export function recordAttempt(
  existing: KitVideoKeyframeAttempt[],
  shotCode: string,
  fingerprint: string,
  qa?: KitVideoVisionQa,
): KitVideoKeyframeAttempt {
  const last = [...existing].reverse().find((a) => a.shotCode === shotCode && a.qa?.status === 'FAIL');
  if (last && last.fingerprint === fingerprint) {
    throw new Error('DO_NOT_BLIND_RETRY: same prompt + same references.');
  }
  const next = nextAttemptNo(existing, shotCode);
  const diagnosed = qa && qa.status === 'FAIL' ? diagnoseFailure(qa) : undefined;
  return {
    attemptId: `att-${shotCode}-${String(next.attemptNo).padStart(2, '0')}`,
    shotCode,
    attemptNo: next.attemptNo,
    fingerprint,
    status: qa?.status === 'FAIL' ? (next.status === 'REVIEW_REQUIRED' ? 'REVIEW_REQUIRED' : 'QA_FAIL') : next.status,
    qa,
    repairClass: diagnosed?.repairClass,
    repair: diagnosed?.repair,
  };
}

export function directorFeedbackToRevision(note: string) {
  const t = note.trim();
  if (!t) throw new Error('Director feedback trống.');
  if (/già|old|age/i.test(t)) return 'Keep CHAR-001 ERA-01 age 11. Do not age the face.';
  if (/bài|paper|kiểm tra/i.test(t)) return 'Make PROP-001 clearly visible in Minh hands.';
  if (/nhìn|facing|mẹ chưa/i.test(t)) return 'Mother faces Minh. Eyeline toward the test paper.';
  if (/rộng|wide/i.test(t)) return 'Tighten framing to medium two-shot. Keep both bodies and the prop.';
  return `Visual Revision Instruction: ${t}`;
}

export function applyDirectorDecision(
  attempt: KitVideoKeyframeAttempt,
  decision: 'APPROVE' | 'REJECT',
): KitVideoKeyframeAttempt {
  if (decision === 'APPROVE') {
    if (attempt.qa?.status !== 'PASS') throw new Error('Chỉ APPROVE khi Vision QA PASS.');
    return { ...attempt, status: 'APPROVED' };
  }
  return { ...attempt, status: 'REJECTED', qa: attempt.qa ? { ...attempt.qa, allowI2v: false, canBeReference: false } : attempt.qa };
}

export function buildI2vReadyPackage(attempt: KitVideoKeyframeAttempt): KitVideoI2vReadyPackage {
  const blocked: string[] = [];
  if (attempt.status !== 'APPROVED') blocked.push('Director not APPROVED');
  if (attempt.qa?.status !== 'PASS') blocked.push('Vision QA not PASS');
  if (!attempt.qa?.allowI2v) blocked.push('I2V blocked');
  return {
    shotCode: attempt.shotCode,
    attemptId: attempt.attemptId,
    source: 'APPROVED_KEYFRAME',
    ready: blocked.length === 0,
    runwaySubmitted: false,
    blocked,
  };
}

export function referencePackForNextShot(attempts: KitVideoKeyframeAttempt[]) {
  return attempts.filter((a) => a.status === 'APPROVED' && a.qa?.canBeReference);
}

export function fingerprintOf(prompt: string, refs: string[]) {
  const raw = `${prompt}||${[...refs].sort().join('|')}`;
  let h = 0;
  for (let i = 0; i < raw.length; i += 1) h = (h * 31 + raw.charCodeAt(i)) >>> 0;
  return `vis-${h.toString(16)}`;
}

function clamp(n: number) {
  return Math.max(0, Math.min(100, n));
}

export function formatVisionBoard(contract: KitVideoVisualContract, qa?: KitVideoVisionQa) {
  return [
    contract.shotCode,
    '',
    'ACTION',
    contract.action,
    '',
    'MANDATORY',
    ...contract.items.filter((i) => i.level === 'MANDATORY').map((i) => `• ${i.label}`),
    '',
    'FORBIDDEN',
    ...contract.forbidden.map((f) => `• ${f}`),
    '',
    'VISION QA',
    qa ? `${qa.status}${qa.p0Fail.length ? `\n${qa.p0Fail.join('\n')}` : ''}` : '—',
    '',
    'I2V',
    qa?.allowI2v ? 'PACKAGE READY · Runway not called' : 'BLOCK',
  ].join('\n');
}
