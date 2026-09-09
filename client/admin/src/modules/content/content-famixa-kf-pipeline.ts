/** Shot Visual Contract → narrative Prompt Compiler → labeled Reference Pack → FAIL edit. */

import {
  peopleCountForSpec,
  type ShotFraming,
  type VisualQa,
  type VisualSpec,
} from './content-famixa-visual-spec';

export type VisualContract = {
  story: string;
  shotIntent: string;
  primary: string;
  secondary: string;
  action: string;
  performance: string;
  gaze: string;
  face: string;
  prop: string;
  camera: string;
  composition: string;
  required: string[];
  forbidden: string[];
  continuity: string;
  framing: ShotFraming;
};

export type KfRefRole = 'scene' | 'continuity' | 'identity' | 'identity-secondary';

export type KfRefSlot = {
  name: string;
  role: KfRefRole;
  imageDataUrl: string;
  visualMode?: string;
  referenceStatus?: string;
  authorityStatus?: string;
  characterId?: string;
  referenceRole?: string;
};

export const EDIT_QUICK = [
  { id: 'wrong-face', label: 'Sửa mặt' },
  { id: 'wrong-framing', label: 'Sửa bố cục' },
  { id: 'missing-prop', label: 'Sửa đạo cụ' },
  { id: 'wrong-action', label: 'Sửa hành động' },
  { id: 'wrong-continuity', label: 'Sửa continuity' },
] as const;

export const FAIL_TICKS = [
  { id: 'wrong-face', label: 'Sai mặt', fail: 'MISSING_FACE', lock: 'FACE + WARDROBE + BACKGROUND' },
  { id: 'wrong-character', label: 'Sai nhân vật', fail: 'WRONG_CHARACTER', lock: 'BACKGROUND + LIGHTING' },
  { id: 'wrong-place', label: 'Sai vị trí', fail: 'WRONG_LOCATION', lock: 'FACE + WARDROBE' },
  { id: 'wrong-action', label: 'Sai hành động', fail: 'WRONG_ACTION', lock: 'FACE + WARDROBE + BACKGROUND' },
  { id: 'missing-prop', label: 'Thiếu đạo cụ', fail: 'MISSING_PROP', lock: 'FACE + WARDROBE + BACKGROUND + CAMERA' },
  { id: 'wrong-location', label: 'Sai bối cảnh', fail: 'WRONG_LOCATION', lock: 'FACE + WARDROBE' },
  { id: 'wrong-gaze', label: 'Sai ánh mắt', fail: 'WRONG_GAZE', lock: 'FACE + WARDROBE + BACKGROUND + COMPOSITION' },
  { id: 'wrong-framing', label: 'Sai khung hình', fail: 'WRONG_ACTION', lock: 'FACE + WARDROBE + BACKGROUND' },
  { id: 'wrong-emotion', label: 'Sai cảm xúc', fail: 'WRONG_ACTION', lock: 'FACE REGION + WARDROBE + BACKGROUND' },
  { id: 'wrong-continuity', label: 'Sai continuity', fail: 'WRONG_WARDROBE', lock: 'CAMERA changeable' },
] as const;

export function compileVisualContract(spec: VisualSpec): VisualContract {
  const sec = spec.secondary[0];
  const prop =
    spec.subjectKind === 'prop'
      ? spec.subjectName
      : spec.required.find((r) => /paper|prop|bài|điểm/i.test(`${r.id} ${r.label}`))?.label || '';
  return {
    story: spec.whyThisShot,
    shotIntent: spec.purpose,
    primary: spec.subjectKind === 'prop' ? spec.subjectName : spec.primary?.name || spec.subjectName,
    secondary: sec ? `${sec.name} — ${sec.body}, face ${sec.face}` : 'none',
    action: spec.shotAction,
    performance: spec.performance,
    gaze: spec.gaze || `${spec.primary?.name || spec.subjectName}: face visible, never the lens.`,
    face:
      spec.subjectKind === 'prop'
        ? 'Prop fills the frame. Faces only as the spec allows (eyes/hands).'
        : `${spec.primary?.name || spec.subjectName} entire face visible — forehead, both eyes, nose, mouth, chin.`,
    prop,
    camera: `${spec.framing}, ${spec.camera}, ${spec.lens}`,
    composition: spec.composition,
    required: spec.required.filter((r) => r.hard).map((r) => r.label),
    forbidden: spec.forbidden,
    continuity: spec.inheritFromPrev || 'Keep faces, hair, age, wardrobe, room, lighting, key props. Change pose, camera, framing, expression.',
    framing: spec.framing,
  };
}

export function formatVisualContract(c: VisualContract) {
  return [
    'VISUAL CONTRACT',
    `STORY: ${c.story}`,
    `SHOT INTENT: ${c.shotIntent}`,
    `PRIMARY: ${c.primary}`,
    `SECONDARY: ${c.secondary}`,
    `ACTION: ${c.action}`,
    `PERFORMANCE: ${c.performance}`,
    `GAZE: ${c.gaze}`,
    `FACE: ${c.face}`,
    c.prop ? `PROP: ${c.prop}` : '',
    `CAMERA: ${c.camera}`,
    `COMPOSITION: ${c.composition}`,
    `REQUIRED: ${c.required.join(' · ') || '—'}`,
    `FORBIDDEN: ${c.forbidden.join(' · ') || '—'}`,
    `CONTINUITY: ${c.continuity}`,
  ]
    .filter(Boolean)
    .join('\n');
}

/** One coherent English still — priority story → character → camera → objects → continuity → style. */
export function compileNarrativeStillPrompt(opts: {
  spec: VisualSpec;
  aspect: '9:16' | '16:9' | string;
  location?: string;
  lighting?: string;
  refs?: { name: string; role?: string }[];
  correction?: string;
  /** Identity + era + outfit + style. Never emotion / action. */
  characterSubset?: string;
}) {
  const spec = opts.spec;
  const c = compileVisualContract(spec);
  const count = peopleCountForSpec(spec);
  const who = spec.primary?.name || spec.subjectName;
  const sec = spec.secondary[0];
  const place = (opts.location || '').trim() || 'an authentic Vietnamese family dining room';
  const light = (opts.lighting || '').trim() || 'dim warm indoor evening after dinner';
  const vertical = opts.aspect === '9:16';
  const refLines = (opts.refs ?? []).map((r, i) => {
    const n = i + 1;
    if (r.role === 'scene') return `Use REFERENCE ${n} (${r.name}) as the exact environment. Do not redesign the room.`;
    if (r.role === 'continuity') {
      return `Use REFERENCE ${n} (${r.name}) as continuity. Keep faces, wardrobe, room, lighting and key props. Do not copy its crop, zoom, pose or camera distance.`;
    }
    if (r.role === 'identity-secondary') {
      return `Use REFERENCE ${n} (${r.name}) for identity only. They may appear only as a shoulder or partial face.`;
    }
    return `Use REFERENCE ${n} (${r.name}) as the identity reference for face, hair, age and clothes. Do not copy a character-sheet pose.`;
  });
  const draw =
    spec.subjectKind === 'prop'
      ? `This is a stylized cinematic INSERT. The ${spec.subjectName} fills the frame. ${sec ? `${sec.name} appears only as ${sec.body}.` : ''}`
      : count <= 1
        ? `${who} is the only complete person in frame.`
        : `${who} and ${sec?.name || 'the other person'} share the frame as a natural two-shot.`;
  const body =
    spec.subjectKind === 'prop'
      ? `The camera looks at the ${spec.subjectName}. ${c.shotIntent} ${c.action}`
      : `${who} ${c.action} ${sec ? `toward ${sec.name}` : ''}. ${c.performance} ${c.gaze} ${c.face}`;
  return [
    'Stylized cinematic Vietnamese family drama. One sharp designed-character film still — not photoreal, not a portrait, catalog, cartoon, or character sheet.',
    `PRIORITY 1 — STORY: ${c.story} ${c.shotIntent}`,
    `PRIORITY 2 — CHARACTER: ${(opts.characterSubset ?? '').trim()} ${draw} ${body}`.replace(/\s+/g, ' '),
    `PRIORITY 3 — COMPOSITION: ${c.camera}. ${c.composition}${
      vertical
        ? count <= 1
          ? ' Vertical 9:16. Primary fills the upper two-thirds. Not a cropped landscape pair.'
          : ' Vertical 9:16. Both heads in the upper two-thirds.'
        : ' Widescreen 16:9 cinematic frame.'
    }`,
    `PRIORITY 4 — REQUIRED: ${c.required.join('; ') || c.face}. ${c.prop ? `The ${c.prop} must be readable.` : ''}`,
    `PRIORITY 5 — CONTINUITY: ${c.continuity} ${refLines.join(' ')}`,
    `PRIORITY 6 — STYLE: ${place}. ${light}. Natural anatomy, subtle expression, cinematic depth of field. Dim evening — not a bright catalog living room.`,
    'Gaze toward the other person or the required prop — never into the lens. Face visible is not looking at camera.',
    `FORBIDDEN: ${c.forbidden.join('; ') || 'looking at camera, extra people, painted text, redesigned room, character sheet, hug.'}`,
    'HARD BAN: no readable letters, numbers, captions, subtitles or typography. Blank paper if a test sheet is needed — KIT may overlay marks later.',
    opts.correction?.trim() ? `CORRECTION (do not resubmit the failed frame unchanged): ${opts.correction.trim()}` : '',
  ]
    .filter(Boolean)
    .join('\n');
}

export function mergeReferencePack(opts: {
  scene?: KfRefSlot;
  prev?: KfRefSlot;
  identities: KfRefSlot[];
  max?: number;
}): KfRefSlot[] {
  const max = opts.max ?? 4;
  const sceneUrl = opts.scene?.imageDataUrl;
  const prevUrl = opts.prev?.imageDataUrl;
  const scene = sceneUrl && sceneUrl !== prevUrl ? opts.scene : undefined;
  const ordered = [...opts.identities, ...(opts.prev ? [opts.prev] : []), ...(scene ? [scene] : [])];
  const uniq: KfRefSlot[] = [];
  for (const row of ordered) {
    if (!row.imageDataUrl.startsWith('data:image')) continue;
    if (uniq.some((x) => x.imageDataUrl === row.imageDataUrl || (x.name === row.name && x.role === row.role))) continue;
    uniq.push(row);
    if (uniq.length >= max) break;
  }
  return uniq;
}

export function identityCanonIds(spec: VisualSpec, castIds: string[]) {
  if (spec.subjectKind === 'prop' || spec.framing === 'INSERT') {
    const id = spec.secondary[0]?.id || castIds[0];
    return id ? [id] : [];
  }
  const ids = [spec.primary?.id, spec.secondary[0]?.id].filter((id): id is string => Boolean(id));
  return ids.length ? ids : castIds.slice(0, spec.framing === 'WIDE' || spec.framing === 'MEDIUM' ? 3 : 2);
}

/** User note “chỉ có Linh” — rewrite cast, do not keep a second body. */
export function soloCastFromNote(note?: string) {
  const t = (note ?? '').trim();
  if (!t) return undefined;
  if (!/chỉ có|một mình|only |không có (minh|nam|ai khác|người thứ)/i.test(t)) return undefined;
  if (/\b(linh|mẹ)\b/i.test(t)) return 'Linh';
  if (/\b(minh|con)\b/i.test(t)) return 'Minh';
  if (/\b(nam|bố|ba)\b/i.test(t)) return 'Nam';
  return undefined;
}

export function applySoloCast(spec: VisualSpec, who: string): VisualSpec {
  const id = who === 'Linh' ? 'CHAR-003' : who === 'Nam' ? 'CHAR-002' : 'CHAR-001';
  const primary = spec.primary
    ? { ...spec.primary, id, name: who, role: 'primary' as const, face: 'full' as const }
    : { id, name: who, role: 'primary' as const, face: 'full' as const, body: 'upper body' };
  const phone = /phone|điện thoại/i.test(`${spec.shotAction} ${spec.gaze} ${spec.purpose}`);
  return {
    ...spec,
    primary,
    subjectName: who,
    secondary: [],
    gazeTarget: phone ? 'the phone' : spec.gazeTarget && !/minh|nam|linh/i.test(spec.gazeTarget) ? spec.gazeTarget : 'the table',
    gaze: `${who} looks at ${phone ? 'the phone' : 'the table'} — never the lens. Alone in frame.`,
    composition: `${who} is the only complete person in frame. No second body, no child's shoulder, no OTS of Minh.`,
    forbidden: [...spec.forbidden, 'Minh in frame', 'second person', 'OTS shoulder of a boy', 'two-shot'],
    shotAction: spec.shotAction.replace(/\b(toward|with|and|nhìn) Minh\b/gi, '').replace(/\s+/g, ' ').trim(),
  };
}

export function compileCorrectionPrompt(spec: VisualSpec, tickIds: string[], evidence?: string) {
  const ticks = FAIL_TICKS.filter((t) => tickIds.includes(t.id));
  const locks = [...new Set(ticks.map((t) => t.lock))];
  const c = compileVisualContract(spec);
  const focus = ticks.map((t) => t.label).join(', ') || evidence || 'the failed hard check';
  const solo = soloCastFromNote(evidence) || (ticks.some((t) => t.id === 'wrong-character') ? spec.primary?.name : undefined);
  if (solo || ticks.some((t) => t.id === 'wrong-character')) {
    const who = solo || c.primary;
    return [
      `Remove every extra person from the frame. Only ${who} remains.`,
      `Do not keep a shoulder, back, or blur of Minh or anyone else.`,
      `Do not copy the failed two-shot. Same room and lighting. ${who} full face, not looking at the camera.`,
      evidence ? `Operator: ${evidence}` : '',
      `LOCK: BACKGROUND + LIGHTING. CAST: ${who} only.`,
    ]
      .filter(Boolean)
      .join(' ');
  }
  return [
    'Keep the entire image unchanged. Do not generate a new unrelated picture.',
    `Preserve ${c.primary}'s face, hairstyle, clothing, body position, room, lighting, camera angle and composition exactly as they are.`,
    `Change only: ${focus}.`,
    evidence ? `QA evidence: ${evidence}` : '',
    `LOCK: ${locks.join(' · ') || 'FACE + WARDROBE + BACKGROUND + LIGHTING'}.`,
    spec.subjectKind === 'prop' || /prop|paper|bài/i.test(focus)
      ? 'The test paper / required prop in the hands must become clearly visible. Do not paint readable letters — blank sheet is enough.'
      : '',
    `Do not alter any other part of the image. ${c.gaze}`,
  ]
    .filter(Boolean)
    .join(' ');
}

export function classifyQaFailure(qa?: VisualQa): string[] {
  const fails = qa?.hardFails ?? [];
  const ticks: string[] = [];
  for (const t of FAIL_TICKS) {
    if (fails.includes(t.fail)) ticks.push(t.id);
  }
  if (!ticks.length && qa?.evidence) {
    if (/paper|prop|bài/i.test(qa.evidence)) ticks.push('missing-prop');
    else if (/face|mặt|crop/i.test(qa.evidence)) ticks.push('wrong-face');
    else if (/gaze|lens|camera/i.test(qa.evidence)) ticks.push('wrong-gaze');
  }
  return [...new Set(ticks)];
}

export function nextShotNeedingKf<T extends { id: string }>(
  shots: T[],
  needIds: string[],
  lockId?: string,
) {
  return shots.find((s) => needIds.includes(s.id) && s.id !== lockId);
}
