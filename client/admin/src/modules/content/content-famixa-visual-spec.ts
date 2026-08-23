/** Shot Director → Visual Spec → prompt compile → Image QA. Script is SoT. Does not invent plot. */

export type ShotFraming = 'ESTABLISHING' | 'WIDE' | 'MEDIUM' | 'MCU' | 'CU' | 'ECU' | 'INSERT' | 'OTS';

export type VisualRequired = {
  id: string;
  label: string;
  hard: boolean;
};

export type VisualPerson = {
  id: string;
  name: string;
  role: 'primary' | 'secondary';
  face: 'full' | 'partial' | 'none';
  body: string;
};

export type VisualSpec = {
  shotId: string;
  /** Story: why this shot exists — from Script Action, not a speak-line template. */
  whyThisShot: string;
  intent: string;
  purpose: string;
  framing: ShotFraming;
  shotType: string;
  camera: string;
  lens: string;
  subjectKind: 'character' | 'prop';
  subjectName: string;
  focus: string;
  primary?: VisualPerson;
  secondary: VisualPerson[];
  required: VisualRequired[];
  notRequired: string[];
  forbidden: string[];
  overlay?: { kind: 'score'; text: string };
  composition: string;
  /** Cinematic action (what the body does). */
  shotAction: string;
  /** How the face is played — not an Action. */
  performance: string;
  gaze?: string;
  gazeTarget?: string;
  emotion?: string;
  startState?: string;
  endState?: string;
  inheritFromPrev?: string;
  hardContinuity: string[];
  softContinuity: string[];
};

export type VisualQaAxis = 'character' | 'face' | 'action' | 'prop' | 'composition' | 'continuity' | 'emotion';

export type VisualQa = {
  status: 'NONE' | 'PENDING' | 'REVIEW' | 'PASS' | 'REJECT';
  total?: number;
  axes?: Partial<Record<VisualQaAxis, number>>;
  hardFails: string[];
  hardChecks?: Record<string, 'PASS' | 'FAIL' | 'PARTIAL'>;
  evidence?: string;
  confidence?: number;
  checks: Record<string, boolean>;
  notes?: string;
};

/** Soft cinematic polish only. Hard fail none → PASS. Score never BLOCKs. */
export const QA_REVIEW_MIN = 85;
export const QA_APPROVE_MIN = 85;

/** Hard fails BLOCK video even at 99. Score is not required. */
export const HARD_FAIL_CODES = [
  'MISSING_FACE',
  'WRONG_CHARACTER',
  'WRONG_COUNT',
  'WRONG_LOCATION',
  'MISSING_PROP',
  'WRONG_ACTION',
  'WRONG_WARDROBE',
  'WRONG_GAZE',
] as const;

const HARD_CONT = ['face', 'hair', 'age', 'wardrobe', 'people-count', 'location', 'time-of-day', 'key-prop'];
const SOFT_CONT = ['expression', 'pose', 'camera-distance', 'angle', 'gaze', 'hands'];

const SHOT_TYPE: Record<ShotFraming, string> = {
  ESTABLISHING: 'Establishing wide',
  WIDE: 'Wide',
  MEDIUM: 'Medium',
  MCU: 'Medium close-up',
  CU: 'Close-up',
  ECU: 'Extreme close-up',
  INSERT: 'Insert',
  OTS: 'Over-the-shoulder',
};

function packHeading(text: string) {
  return /^(tone|thời lượng|gấu nước|cold bucket|heading)\b/i.test(text.trim()) || /^[A-ZÀ-Ỹ0-9 \-]{6,}$/.test(text.trim());
}

export function framingFromAction(
  action: string,
  spoken = '',
  speakers: string[] = [],
  prev?: ShotFraming,
): ShotFraming {
  const t = `${action} ${spoken}`.replace(/\s+/g, ' ').trim();
  let next: ShotFraming = 'MEDIUM';
  if (/liếc|con số|nhìn xuống.*điểm|nhìn con số|9\/10/i.test(t)) next = 'INSERT';
  else if (/đặt xuống|cận tờ|cận bài|insert|tờ bài kiểm/i.test(t) && !/đưa|khoe|chia bài/i.test(t)) next = 'INSERT';
  else if (/bước vào|chạy vào|vào nhà|về nhà|toàn cảnh|establishing|cửa chính/i.test(t)) next = 'WIDE';
  else if (/quay đi|bước ra|rời khỏi/i.test(t)) next = 'WIDE';
  else if (/không đổi biểu cảm|giữ nét|không phản ứng/i.test(t)) next = 'CU';
  else if (/gấu nước|im lặng|không nói|nụ cười tắt|tổn thương|hụt hẫng|chờ phản|chờ được|reaction/i.test(t)) next = 'CU';
  else if (/cận mặt|close-?up/i.test(t)) next = 'CU';
  else if (/hai má|đối ứng|tranh|cãi|ots|over.?shoulder/i.test(t)) next = 'OTS';
  else if (speakers.length >= 2) next = 'OTS';
  else if (/đưa|chia bài|cầm bài|sát mặt/i.test(t)) next = 'MEDIUM';
  else if (speakers.length === 1) next = 'MCU';
  else if (/nhìn mẹ|nhìn bài|nhìn điểm/i.test(t)) next = 'CU';
  if (next === 'MCU' && prev === 'MCU') return 'OTS';
  if (next === 'MCU' && prev === 'OTS') return 'CU';
  return next;
}

/** Same camera size may copy PREV pixels. CU / INSERT / size jump = new camera — do not zoom the last still. */
export function shouldAttachPrevKf(prev?: ShotFraming, next?: ShotFraming) {
  if (!prev || !next) return false;
  if (next === 'CU' || next === 'ECU' || next === 'INSERT') return false;
  if (prev === 'CU' || prev === 'ECU' || prev === 'INSERT') return false;
  return prev === next;
}

function person(id: string, name: string, role: VisualPerson['role'], face: VisualPerson['face'], body: string): VisualPerson {
  return { id, name, role, face, body };
}

export function deriveVisualSpec(opts: {
  shotId: string;
  action?: string;
  spoken?: string;
  location?: string;
  lighting?: string;
  names: string[];
  ids?: string[];
  speakers?: string[];
  camera?: string;
  lens?: string;
  prevAction?: string;
  prevFraming?: ShotFraming;
}): VisualSpec {
  const rawAction = (opts.action || '').replace(/\s+/g, ' ').trim();
  const action = packHeading(rawAction) ? '' : rawAction;
  const spoken = (opts.spoken || '').replace(/\s+/g, ' ').trim();
  const names = opts.names.map((n) => n.trim()).filter(Boolean);
  const ids = opts.ids ?? [];
  const speakers = (opts.speakers ?? []).map((s) => s.trim()).filter(Boolean);
  const framing = framingFromAction(action || rawAction, spoken, speakers, opts.prevFraming);
  const insertScore = framing === 'INSERT' && /liếc|con số|9\/10|điểm|bài kiểm/i.test(`${action} ${spoken} ${rawAction}`);
  const holdFace = /không đổi biểu cảm|giữ nét|không phản ứng/i.test(`${rawAction} ${action}`);
  const speakerName = speakers[0] || names.find((n) => new RegExp(n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i').test(`${action} ${spoken}`));
  const subjectName = insertScore
    ? 'Test paper'
    : speakerName || names.find((n) => new RegExp(n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i').test(action)) || names[0] || 'Minh';
  const subjectIdx = Math.max(0, names.findIndex((n) => n === subjectName));
  const subjectKind: 'character' | 'prop' = insertScore ? 'prop' : 'character';

  let primary: VisualPerson | undefined;
  let secondary: VisualPerson[] = [];
  if (insertScore) {
    const minh = names.find((n) => /minh/i.test(n));
    if (minh) {
      secondary.push(
        person(ids[names.indexOf(minh)] || 'CHAR-001', minh, 'secondary', 'partial', 'eyes / lower face only'),
      );
    }
  } else if (subjectName && names.includes(subjectName)) {
    primary = person(
      ids[subjectIdx] || `CHAR-00${subjectIdx + 1}`,
      subjectName,
      'primary',
      framing === 'WIDE' || framing === 'ESTABLISHING' ? 'full' : 'full',
      framing === 'WIDE' || framing === 'ESTABLISHING' ? 'full / three-quarter' : framing === 'CU' || framing === 'ECU' ? 'face' : 'upper body',
    );
    secondary = names
      .filter((n) => n !== subjectName)
      .map((n, i) => {
        const speaking = speakers.some((s) => s.toLowerCase() === n.toLowerCase());
        const tight = framing === 'CU' || framing === 'ECU' || framing === 'MCU';
        return person(
          ids[names.indexOf(n)] || `CHAR-00${i + 2}`,
          n,
          'secondary',
          speaking && !tight ? 'full' : 'partial',
          speaking ? 'upper body' : tight ? 'partial foreground / shoulder' : 'background / partial',
        );
      });
  }

  const required: VisualRequired[] = [];
  if (insertScore) {
    required.push({ id: 'paper', label: 'Test paper fills the frame', hard: true });
    required.push({ id: 'score', label: 'Score 9 visible', hard: true });
    required.push({ id: 'gaze', label: 'Eyes or hands relate to the paper', hard: true });
  } else if (primary) {
    required.push({ id: 'primary-face', label: `${primary.name} full face`, hard: true });
    required.push({ id: 'primary-eyes', label: `${primary.name} eyes visible`, hard: true });
  }
  if (opts.location && framing !== 'INSERT') required.push({ id: 'place', label: opts.location.slice(0, 48), hard: true });
  if ((opts.lighting || /tối|dim|evening/i.test(action)) && framing !== 'INSERT') {
    required.push({ id: 'light', label: opts.lighting || 'dim warm evening', hard: true });
  }

  const other =
    names.find((n) => n !== subjectName && /linh|^mẹ$/i.test(n)) ||
    names.find((n) => n !== subjectName) ||
    '';
  const gazeTarget = insertScore ? 'the score on the paper' : other || undefined;
  const gaze = insertScore
    ? 'Toward the score on the paper — never the lens'
    : gazeTarget
      ? `${subjectName} looks toward ${gazeTarget}. Face visible. NEVER look into the lens.`
      : `${subjectName}: face visible, eyes off-lens.`;
  const shotAction = holdFace
    ? `${subjectName} đứng im, nhìn ${other || 'the other person'}.`
    : insertScore
      ? 'Eyes / hands find the score on the paper.'
      : action || (spoken ? `${subjectName} speaks to ${other || 'the other person'}.` : rawAction);
  const performance = holdFace
    ? 'Giữ nét mặt, không đổi biểu cảm, không phản ứng lớn.'
    : insertScore
      ? 'No acted smile. Paper is the performance.'
      : spoken
        ? 'Mouth mid-speech. Face visible — not a product look into camera. Never paint the line.'
        : 'Face follows this Action only.';
  const blob = `${action} ${spoken} ${rawAction}`;
  const emotion = holdFace
    ? 'Confused / held — from “không đổi biểu cảm”, not a reset'
    : /tổn thương|nụ cười tắt|hụt/i.test(blob)
      ? 'Hurt — from this Action, not a reset'
      : /lạnh|sắc|gấu nước/i.test(blob)
        ? 'Cold / flat — from this Action'
        : /háo hức|khoe|chờ|chín điểm/i.test(blob)
          ? 'Eager / waiting — from this Action'
          : undefined;
  const whyThisShot = insertScore
    ? 'This shot exists so the audience can read the score.'
    : holdFace
      ? `This shot exists to hold ${subjectName}'s face after the last beat — a still look, not a new plot.`
      : framing === 'WIDE'
        ? `This shot exists to show ${subjectName} arrive / the room.`
        : action
          ? `This shot exists for: ${action.slice(0, 100)}`
          : spoken
            ? `This shot exists for ${subjectName} talking to ${other || 'the other person'}.`
            : `This shot exists for the locked Action.`;
  const purpose = insertScore
    ? 'Audience must notice the 9 on the test paper. If the 9 is missing, the shot fails.'
    : holdFace
      ? `Audience must see ${subjectName} stand still and look at ${other || 'mother'} — face visible, gaze off-lens.`
      : framing === 'WIDE'
        ? `Audience must see ${subjectName} in the room — doorway and space, not a portrait into camera.`
        : framing === 'OTS'
          ? `Audience must see ${subjectName} over ${other || 'the other'}'s shoulder — ${subjectName} face visible, looking at them.`
          : framing === 'CU' || framing === 'ECU'
            ? `Audience must read ${subjectName}'s face on this beat — gaze toward ${gazeTarget || 'the other person'}, never the lens.`
            : `Audience must see ${shotAction.slice(0, 110)} Face visible. Gaze toward ${gazeTarget || 'the other person'}, not the camera.`;
  const intent = purpose;
  const focus = insertScore ? 'Test paper / score 9' : subjectName;
  const forbidden = [
    'New character',
    'New background',
    'Different wardrobe',
    ...(framing === 'INSERT' || framing === 'CU' || framing === 'ECU'
      ? ['Full-body wide shot', 'Two-shot of both people', 'Whole dining room']
      : framing === 'MCU' || framing === 'OTS'
        ? ['Full body both people', 'Wide dining table']
        : []),
  ];
  const prev = (opts.prevAction || '').replace(/\s+/g, ' ').trim();
  const inheritFromPrev = prev
    ? /cầm|tay phải|tay trái|bài kiểm|tờ giấy/i.test(prev)
      ? `START inherits previous END: keep the same hand / paper unless this Action changes it.`
      : `START inherits previous END. Soft: expression, angle, gaze may change with Action.`
    : undefined;
  if (inheritFromPrev && /cầm|tay|bài kiểm|tờ giấy/i.test(prev)) {
    required.push({ id: 'inherit-prop', label: 'Same key prop / hand as previous END', hard: true });
  }
  const composition = insertScore
    ? 'INSERT: test paper dominates the frame. Score 9 readable. Minh only as eyes / hands at the edge. Do not pull back to a two-shot.'
    : framing === 'CU' || framing === 'ECU'
      ? `${subjectName} fills the frame. Full face. 10% headroom. Other person only as a blur/shoulder if needed.`
      : framing === 'WIDE' || framing === 'ESTABLISHING'
        ? 'Wide room. Bodies readable. Do not crop heads. Show entry / space.'
      : framing === 'OTS'
        ? `Camera behind ${other || 'secondary'}'s shoulder. ${subjectName} face visible beyond it, looking at ${other || 'them'}. ${other || 'Secondary'} face not required. Do not flatten into an even two-shot.`
        : `${subjectName} occupies approximately 60–70% of frame, upper body, full face visible. ${other ? `${other}: foreground shoulder / partial — face not required.` : ''} Gaze toward ${gazeTarget || other || 'the other person'}, never the lens.`;
  const lens =
    opts.lens ||
    (framing === 'INSERT' || framing === 'ECU' ? '85mm' : framing === 'CU' || framing === 'MCU' ? '50mm' : framing === 'WIDE' ? '35mm' : '40mm');
  return {
    shotId: opts.shotId,
    whyThisShot,
    intent,
    purpose,
    framing,
    shotType: SHOT_TYPE[framing],
    camera: opts.camera || 'Eye level',
    lens,
    subjectKind,
    subjectName,
    focus,
    primary,
    secondary,
    required,
    notRequired: forbidden,
    forbidden,
    overlay: insertScore ? { kind: 'score', text: '9' } : undefined,
    composition,
    shotAction,
    performance,
    gaze,
    gazeTarget,
    emotion,
    startState: prev ? `Previous END: ${prev.slice(0, 90)}` : undefined,
    endState: `After this Action: ${shotAction.slice(0, 90)}`,
    inheritFromPrev,
    hardContinuity: HARD_CONT,
    softContinuity: SOFT_CONT,
  };
}

/** Full bodies Gemini must draw. Tight shots = 1. INSERT = 0. */
export function peopleCountForSpec(spec: VisualSpec) {
  if (spec.subjectKind === 'prop' || spec.framing === 'INSERT') return 0;
  if (spec.framing === 'CU' || spec.framing === 'ECU' || spec.framing === 'MCU' || spec.framing === 'OTS') return 1;
  return 1 + spec.secondary.filter((p) => p.face === 'full').length;
}

/** Identity refs (not body count). MCU may include Linh Canon as identity-only. */
export function canonIdsForSpec(spec: VisualSpec, castIds: string[]) {
  if (spec.subjectKind === 'prop' || spec.framing === 'INSERT') {
    const id = spec.secondary[0]?.id || castIds[0];
    return id ? [id] : [];
  }
  const ids = [spec.primary?.id, spec.secondary[0]?.id].filter((id): id is string => Boolean(id));
  if (ids.length) return ids;
  if (spec.framing === 'WIDE' || spec.framing === 'MEDIUM' || spec.framing === 'ESTABLISHING') return castIds;
  return castIds.slice(0, 1);
}

/** Short English order Gemini can draw. Goes first in the still prompt. */
export function compileGeminiStillBrief(spec: VisualSpec) {
  const sec = spec.secondary[0];
  const draw =
    spec.subjectKind === 'prop'
      ? `DRAW: photoreal INSERT. The ${spec.subjectName} fills the frame. ${sec ? `${sec.name} only as ${sec.body}.` : ''}`
      : `DRAW: photoreal ${spec.shotType}. ${spec.primary?.name || spec.subjectName} is the only full person.`;
  const must =
    spec.subjectKind === 'prop'
      ? `MUST SEE: ${spec.purpose}`
      : `MUST SEE: ${spec.primary?.name || spec.subjectName} full face (forehead to chin) + this Action: ${spec.shotAction}`;
  const secLine = spec.subjectKind === 'prop'
    ? ''
    : sec
      ? `SECONDARY: ${sec.name} — ${sec.body}. Face ${sec.face}. Do not force a two-shot.`
      : 'SECONDARY: none.';
  return [
    'GEMINI STILL — ONE FRAME (follow this first)',
    draw,
    `ACTION: ${spec.shotAction}`,
    `PERFORMANCE: ${spec.performance}`,
    spec.gaze ? `GAZE: ${spec.gaze}` : '',
    must,
    secLine,
    `FRAME: ${spec.framing}. Camera ${spec.camera}. Lens ${spec.lens}. ${spec.composition}`,
    spec.emotion ? `MOOD (script only): ${spec.emotion}` : '',
    `MUST NOT: looking into the lens, painted words/numbers, extra people, hug, bright catalog room, character sheet.`,
    spec.forbidden.length ? `FORBIDDEN: ${spec.forbidden.join('; ')}.` : '',
  ]
    .filter(Boolean)
    .join('\n');
}

export function compileVisualPrompt(spec: VisualSpec) {
  const lines = [
    `STORY: ${spec.whyThisShot}`,
    `SHOT INTENT: ${spec.purpose}`,
    `ACTION: ${spec.shotAction}`,
    `PERFORMANCE: ${spec.performance}`,
    spec.emotion ? `EMOTIONAL STATE (from Script only): ${spec.emotion}.` : '',
    `FRAMING LOCK: ${spec.shotType} (${spec.framing}). Camera ${spec.camera}. Lens ${spec.lens}.`,
    `FOCUS: ${spec.focus}.`,
    spec.subjectKind === 'prop'
      ? `PRIMARY: ${spec.subjectName} (prop). SECONDARY: ${spec.secondary.map((p) => `${p.name} — ${p.body}`).join('; ') || 'none'}.`
      : spec.primary
        ? `PRIMARY: ${spec.primary.name} — ${spec.primary.face} face VISIBLE, ${spec.primary.body}. Do not crop this face.`
        : '',
    spec.subjectKind !== 'prop' && spec.secondary.length
      ? `SECONDARY: ${spec.secondary.map((p) => `${p.name} — ${p.body}, face ${p.face}${p.face === 'partial' || p.face === 'none' ? ' (face not required)' : ''}`).join('; ')}.`
      : '',
    spec.gaze ? `GAZE: ${spec.gaze}` : '',
    'GAZE LAW: face visible ≠ look at camera. Characters look at each other or the prop. Forbidden: looking into the lens, eyeline to audience, product-ad stare.',
    spec.startState || '',
    spec.endState || '',
    `REQUIRED VISUAL: ${spec.required.map((r) => r.label).join('; ')}.`,
    `FORBIDDEN: ${spec.forbidden.join('; ')}.`,
    `COMPOSITION LOCK: ${spec.composition}`,
    spec.inheritFromPrev || '',
    spec.framing === 'INSERT'
      ? 'INSERT: do not reframe as a two-shot. The required prop is the subject. Faces may be cropped to eyes/hands.'
      : spec.framing !== 'WIDE' && spec.framing !== 'ESTABLISHING'
        ? 'Do not force a full-body two-shot. Keep the primary subject; secondary may be partial.'
        : '',
    'FACE VISIBLE: if a character face is required, reject a still with a missing, cropped, back-turned, or unreadable face. They must not look into the lens.',
    'HARD CONTINUITY: same faces, hair, age, wardrobe, people count, place, time, key prop. SOFT: expression, pose, camera, gaze may change with Action.',
    spec.overlay ? `Narrative mark "${spec.overlay.text}" may be added by KIT overlay — do not paint warped letters.` : '',
  ];
  return lines.filter(Boolean).join('\n');
}

export function emptyVisualQa(): VisualQa {
  return { status: 'NONE', hardFails: [], checks: {} };
}

export function qaLane(qa?: VisualQa, spec?: VisualSpec): 'NONE' | 'PENDING' | 'BLOCK' | 'REVIEW' | 'PASS' {
  if (!qa || qa.status === 'NONE') return 'NONE';
  const fails = spec ? sanitizeHardFails(spec, qa.hardFails) : qa.hardFails;
  if (fails.length) return 'BLOCK';
  if (qa.status === 'PENDING') return 'PENDING';
  if (typeof qa.total === 'number' || qa.status === 'PASS' || qa.status === 'REVIEW' || qa.status === 'REJECT') {
    return 'PASS';
  }
  return 'PENDING';
}

export function visualQaAllowsApprove(qa?: VisualQa) {
  return qaLane(qa) === 'PASS';
}

export function visualQaAllowsVideo(qa?: VisualQa) {
  return visualQaAllowsApprove(qa);
}

export function seedQaChecks(spec: VisualSpec): VisualQa {
  const checks: Record<string, boolean> = {};
  for (const r of spec.required.filter((x) => x.hard)) checks[r.id] = false;
  return { status: 'PENDING', hardFails: [], checks };
}

export function applyOperatorCheck(qa: VisualQa, id: string, ok: boolean): VisualQa {
  const checks = { ...qa.checks, [id]: ok };
  const pending = Object.values(checks).some((v) => !v);
  if (qa.hardFails.length) return { ...qa, checks, status: 'REJECT' };
  if (pending) return { ...qa, checks, status: 'PENDING' };
  if (typeof qa.total === 'number' || qa.status === 'PASS' || qa.status === 'REVIEW' || qa.status === 'REJECT') {
    return { ...qa, checks, status: 'PASS' };
  }
  return { ...qa, checks, status: 'PENDING' };
}

/** OTS/CU/INSERT: secondary shoulder is allowed. Scene Master lock: image defines the room — do not fail action/count/place. */
export function sanitizeHardFails(spec: VisualSpec, fails: string[], opts?: { sceneMaster?: boolean }) {
  let next = [...new Set(fails)];
  if (spec.subjectKind === 'prop' || spec.framing === 'INSERT') {
    next = next.filter((f) => f !== 'MISSING_FACE');
  }
  const tight = spec.framing === 'OTS' || spec.framing === 'MCU' || spec.framing === 'CU' || spec.framing === 'ECU' || spec.framing === 'INSERT';
  if (tight) next = next.filter((f) => f !== 'WRONG_COUNT');
  if (spec.framing === 'WIDE' || spec.framing === 'ESTABLISHING' || spec.framing === 'MEDIUM') {
    next = next.filter((f) => f !== 'WRONG_COUNT');
  }
  if (tight && spec.secondary.some((p) => p.face === 'partial' || p.face === 'none')) {
    next = next.filter((f) => f !== 'MISSING_FACE');
  }
  if (opts?.sceneMaster) {
    next = next.filter((f) => f !== 'WRONG_ACTION' && f !== 'WRONG_COUNT' && f !== 'WRONG_LOCATION');
  }
  if (/^subject$/i.test(spec.subjectName) || /speaks? to the other person/i.test(spec.shotAction)) {
    next = next.filter((f) => f !== 'WRONG_ACTION');
  }
  next = next.filter((f) => f !== 'LOOKING_AT_CAMERA');
  return next;
}

export function parseVisionQa(raw: unknown, spec: VisualSpec, opts?: { sceneMaster?: boolean }): VisualQa {
  const o = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const axes = (o.axes && typeof o.axes === 'object' ? o.axes : {}) as Record<string, number>;
  const hardFails = Array.isArray(o.hardFails)
    ? o.hardFails.map((x) => {
        const raw = String(x).trim().toUpperCase().replace(/\s+/g, '_');
        if (/FACE_MISSING|MISSING_FACE|NO_FACE/.test(raw)) return 'MISSING_FACE';
        if (/WRONG_CHAR|WRONG_PERSON/.test(raw)) return 'WRONG_CHARACTER';
        if (/WRONG_COUNT|WRONG_NUMBER|PEOPLE_COUNT/.test(raw)) return 'WRONG_COUNT';
        if (/WRONG_LOC|WRONG_PLACE/.test(raw)) return 'WRONG_LOCATION';
        if (/MISSING_PROP|PROP_MISSING/.test(raw)) return 'MISSING_PROP';
        if (/WRONG_ACTION/.test(raw)) return 'WRONG_ACTION';
        if (/WRONG_WARDROBE|WRONG_CLOTH/.test(raw)) return 'WRONG_WARDROBE';
        if (/WRONG_GAZE|LOOKING_AT_CAMERA|INTO_THE_LENS/.test(raw)) return 'WRONG_GAZE';
        return String(x).trim();
      })
    : [];
  const total = typeof o.total === 'number' ? o.total : average(Object.values(axes));
  const checks: Record<string, boolean> = {};
  for (const r of spec.required.filter((x) => x.hard)) {
    checks[r.id] = !hardFails.some((f) => f.toLowerCase().includes(r.id) || f.toLowerCase().includes(r.label.split(' ')[0]!.toLowerCase()));
  }
  const uniqueFails = sanitizeHardFails(spec, hardFails, opts).filter((f) => {
    if (f === 'WRONG_GAZE' && /never the lens|toward|off-lens/i.test(`${spec.gaze || ''}`)) {
      return /lens|camera|audience/i.test(String(o.notes || o.evidence || ''));
    }
    return true;
  });
  const hardChecksRaw = o.hardChecks && typeof o.hardChecks === 'object' ? (o.hardChecks as Record<string, string>) : {};
  const hardChecks: VisualQa['hardChecks'] = {};
  for (const [k, v] of Object.entries(hardChecksRaw)) {
    const lane = String(v).toUpperCase();
    if (lane === 'PASS' || lane === 'FAIL' || lane === 'PARTIAL') hardChecks[k] = lane;
  }
  let status: VisualQa['status'] = 'PENDING';
  if (uniqueFails.length) status = 'REJECT';
  else if (typeof total === 'number' || Object.keys(hardChecks).length) status = 'PASS';
  if (status === 'PASS') {
    for (const r of spec.required.filter((x) => x.hard)) checks[r.id] = true;
  }
  return {
    status,
    total,
    axes: {
      character: num(axes.character),
      face: num(axes.face),
      action: num(axes.action),
      prop: num(axes.prop),
      composition: num(axes.composition),
      continuity: num(axes.continuity),
      emotion: num(axes.emotion),
    },
    hardFails: uniqueFails,
    hardChecks: Object.keys(hardChecks).length ? hardChecks : undefined,
    evidence: typeof o.evidence === 'string' ? o.evidence : undefined,
    confidence: typeof o.confidence === 'number' ? o.confidence : undefined,
    checks,
    notes: typeof o.notes === 'string' ? o.notes : undefined,
  };
}

export function approveBlockReason(qa?: VisualQa) {
  const lane = qaLane(qa);
  if (lane === 'NONE') return 'Chưa Image QA — không duyệt KF / không gửi Video.';
  if (qa?.hardFails.length) {
    return `HARD FAIL: ${qa.hardFails.join(', ')}${qa.evidence ? ` — ${qa.evidence}` : ''} — BLOCK. Điểm không cứu.`;
  }
  if (lane === 'PENDING') return 'Image QA PENDING — Chấm lại, đừng tạo ảnh mới.';
  if (!visualQaAllowsApprove(qa)) return 'Image QA chưa PASS — cần hard check, không cần điểm 95.';
  return undefined;
}

export async function overlayNarrativeMark(
  dataUrl: string,
  mark: { kind: 'score'; text: string },
): Promise<string> {
  if (typeof document === 'undefined' || !dataUrl.startsWith('data:image')) return dataUrl;
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = img.naturalWidth || img.width;
      canvas.height = img.naturalHeight || img.height;
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        resolve(dataUrl);
        return;
      }
      ctx.drawImage(img, 0, 0);
      const boxW = Math.round(canvas.width * 0.22);
      const boxH = Math.round(canvas.height * 0.16);
      const x = Math.round(canvas.width * 0.39);
      const y = Math.round(canvas.height * 0.42);
      ctx.fillStyle = 'rgba(248, 246, 240, 0.94)';
      ctx.fillRect(x, y, boxW, boxH);
      ctx.strokeStyle = 'rgba(40, 32, 24, 0.55)';
      ctx.lineWidth = 1;
      ctx.strokeRect(x + 0.5, y + 0.5, boxW - 1, boxH - 1);
      ctx.fillStyle = '#1c1916';
      ctx.font = `700 ${Math.round(boxH * 0.55)}px "Times New Roman", serif`;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(mark.text, x + boxW / 2, y + boxH / 2);
      resolve(canvas.toDataURL('image/jpeg', 0.9));
    };
    img.onerror = () => resolve(dataUrl);
    img.src = dataUrl;
  });
}

function num(v: unknown) {
  return typeof v === 'number' && Number.isFinite(v) ? v : undefined;
}

function average(xs: number[]) {
  const n = xs.filter((x) => typeof x === 'number');
  if (!n.length) return undefined;
  return Math.round(n.reduce((a, b) => a + b, 0) / n.length);
}

export function storyboardLabel(spec?: VisualSpec) {
  return spec?.framing || 'MEDIUM';
}

export function coverageRepeatWarning(framings: ShotFraming[]) {
  if (framings.length < 4) return undefined;
  const medium = framings.filter((f) => f === 'MEDIUM').length;
  const same = framings.every((f) => f === framings[0]);
  if (same) return `Cả ${framings.length} shot cùng ${framings[0]} — không phải coverage. Tạo lại KF theo Shot Director.`;
  if (medium >= Math.ceil(framings.length * 0.6)) {
    return `${medium}/${framings.length} shot MEDIUM — Shot Director phải đổi INSERT / CU / WIDE theo Action.`;
  }
  return undefined;
}
