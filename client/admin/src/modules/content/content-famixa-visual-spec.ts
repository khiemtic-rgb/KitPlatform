/** Shot Director → Visual Spec → prompt compile → Image QA. Script is SoT. Does not invent plot. */

export type ShotFraming = 'ESTABLISHING' | 'WIDE' | 'MEDIUM' | 'MCU' | 'CU' | 'ECU' | 'INSERT';

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
  intent: string;
  framing: ShotFraming;
  shotType: string;
  camera: string;
  lens: string;
  primary?: VisualPerson;
  secondary: VisualPerson[];
  required: VisualRequired[];
  notRequired: string[];
  overlay?: { kind: 'score'; text: string };
  composition: string;
  inheritFromPrev?: string;
  hardContinuity: string[];
  softContinuity: string[];
};

export type VisualQaAxis = 'character' | 'face' | 'action' | 'prop' | 'composition' | 'continuity' | 'emotion';

export type VisualQa = {
  status: 'NONE' | 'PENDING' | 'PASS' | 'REJECT';
  total?: number;
  axes?: Partial<Record<VisualQaAxis, number>>;
  hardFails: string[];
  checks: Record<string, boolean>;
  notes?: string;
};

const HARD_CONT = ['face', 'hair', 'age', 'wardrobe', 'people-count', 'location', 'time-of-day', 'key-prop'];
const SOFT_CONT = ['expression', 'pose', 'camera-distance', 'angle', 'gaze', 'hands'];

export function deriveVisualSpec(opts: {
  shotId: string;
  action?: string;
  location?: string;
  lighting?: string;
  names: string[];
  ids?: string[];
  speakers?: string[];
  camera?: string;
  lens?: string;
  prevAction?: string;
}): VisualSpec {
  const action = (opts.action || '').replace(/\s+/g, ' ').trim();
  const names = opts.names.map((n) => n.trim()).filter(Boolean);
  const ids = opts.ids ?? [];
  const speakers = (opts.speakers ?? []).map((s) => s.trim()).filter(Boolean);
  const glance = /liếc|nhìn xuống|con số|bài kiểm|điểm|9\/10|chín điểm/i.test(action);
  const enter = /bước vào|toàn cảnh|phòng ăn|establishing/i.test(action);
  const tight = /cận|mặt|im lặng|không nói|reaction/i.test(action);
  const framing: ShotFraming = glance ? 'MCU' : enter ? 'WIDE' : tight ? 'CU' : speakers.length ? 'MEDIUM' : 'MEDIUM';
  const subjectName =
    (glance ? names.find((n) => !/linh|^mẹ$/i.test(n)) : undefined) ||
    names.find((n) => new RegExp(n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i').test(action)) ||
    speakers[0] ||
    names[0];
  const subjectIdx = Math.max(0, names.findIndex((n) => n === subjectName));
  const primary: VisualPerson | undefined = subjectName
    ? {
        id: ids[subjectIdx] || `CHAR-00${subjectIdx + 1}`,
        name: subjectName,
        role: 'primary',
        face: 'full',
        body: framing === 'WIDE' || framing === 'ESTABLISHING' ? 'full / three-quarter' : 'upper body',
      }
    : undefined;
  const secondary: VisualPerson[] = names
    .filter((n) => n !== subjectName)
    .map((n, i) => {
      const idx = names.indexOf(n);
      const speaking = speakers.some((s) => s.toLowerCase() === n.toLowerCase());
      return {
        id: ids[idx] || `CHAR-00${i + 2}`,
        name: n,
        role: 'secondary' as const,
        face: speaking ? ('full' as const) : ('partial' as const),
        body: speaking ? 'upper body' : 'background / partial',
      };
    });
  const required: VisualRequired[] = [];
  if (primary) {
    required.push({ id: 'primary-face', label: `${primary.name} full face`, hard: true });
    required.push({ id: 'primary-eyes', label: `${primary.name} eyes visible`, hard: true });
  }
  if (glance) {
    required.push({ id: 'paper', label: 'Test paper visible', hard: true });
    required.push({ id: 'score', label: 'Score 9 visible', hard: true });
    required.push({ id: 'gaze', label: `${primary?.name || 'Subject'} looking toward paper`, hard: true });
  }
  if (opts.location) required.push({ id: 'place', label: opts.location.slice(0, 48), hard: true });
  if (opts.lighting || /tối|dim|evening/i.test(action)) {
    required.push({ id: 'light', label: opts.lighting || 'dim warm evening', hard: true });
  }
  const intent = glance
    ? `Khán giả thấy ${primary?.name || 'nhân vật'} liếc điểm trên bài — không cần cả phòng.`
    : action
      ? `Khán giả thấy: ${action.slice(0, 120)}`
      : 'Khán giả thấy đúng Action đã lọc.';
  const notRequired = glance
    ? ['Full body both people', 'Wide dining table', 'Mother full face unless she speaks']
    : framing === 'CU' || framing === 'MCU'
      ? ['Full body', 'Both characters full body']
      : [];
  const prev = (opts.prevAction || '').replace(/\s+/g, ' ').trim();
  const inheritFromPrev = prev
    ? /cầm|tay phải|tay trái|bài kiểm|tờ giấy/i.test(prev)
      ? `START inherits previous END: keep the same hand / paper unless this Action changes it. Previous: ${prev.slice(0, 80)}`
      : `START inherits previous END pose only as needed. Soft: expression, angle, gaze may change.`
    : undefined;
  if (inheritFromPrev && /cầm|tay|bài kiểm|tờ giấy/i.test(prev)) {
    required.push({ id: 'inherit-prop', label: 'Same key prop / hand as previous END', hard: true });
  }
  const composition = glance
    ? `${primary?.name || 'Subject'} right third, full face, 10% headroom, eyes visible. Paper bottom third. ${secondary[0]?.name || 'Other'} background left, partial. No primary face crop.`
    : framing === 'CU' || framing === 'ECU' || framing === 'INSERT'
      ? `${primary?.name || 'Subject'} fills frame. Full face. 10% headroom. No body required.`
      : framing === 'WIDE' || framing === 'ESTABLISHING'
        ? 'Wide room. Bodies readable. Do not crop heads.'
        : `${primary?.name || 'Subject'} center-left third, upper body, full face. Secondary may be partial unless speaking.`;
  return {
    shotId: opts.shotId,
    intent,
    framing,
    shotType: framing === 'MCU' ? 'Medium close-up' : framing === 'CU' ? 'Close-up' : framing === 'WIDE' ? 'Wide' : 'Medium',
    camera: opts.camera || 'Eye level',
    lens: opts.lens || (glance || framing === 'CU' ? '50mm' : framing === 'WIDE' ? '35mm' : '40mm'),
    primary,
    secondary,
    required,
    notRequired,
    overlay: glance ? { kind: 'score', text: '9' } : undefined,
    composition,
    inheritFromPrev,
    hardContinuity: HARD_CONT,
    softContinuity: SOFT_CONT,
  };
}

export function compileVisualPrompt(spec: VisualSpec) {
  const who = [spec.primary, ...spec.secondary].filter(Boolean);
  const lines = [
    `SHOT INTENT: ${spec.intent}`,
    `FRAMING LOCK: ${spec.shotType} (${spec.framing}). Camera ${spec.camera}. Lens ${spec.lens}.`,
    spec.primary
      ? `PRIMARY: ${spec.primary.name} — ${spec.primary.face} face, ${spec.primary.body}. FACE SAFE: do not crop this face.`
      : '',
    spec.secondary.length
      ? `SECONDARY: ${spec.secondary.map((p) => `${p.name} — ${p.body}, face ${p.face}`).join('; ')}.`
      : '',
    `REQUIRED VISUAL: ${spec.required.map((r) => r.label).join('; ')}.`,
    spec.notRequired.length ? `NOT REQUIRED: ${spec.notRequired.join('; ')}.` : '',
    `COMPOSITION LOCK: ${spec.composition}`,
    spec.inheritFromPrev || '',
    who.length >= 2 && spec.framing !== 'WIDE' && spec.framing !== 'ESTABLISHING'
      ? 'Do not force a full-body two-shot. Keep the primary face; secondary may be partial.'
      : '',
    'FACE SAFE: if FACE_REQUIRED, reject a still with a missing, cropped, back-turned, or unreadable primary face.',
    'HARD CONTINUITY: same faces, hair, age, wardrobe, people count, place, time, key prop. SOFT: expression, pose, camera, gaze may change with Action.',
    spec.overlay ? `Narrative mark "${spec.overlay.text}" may be added by KIT overlay — do not paint warped letters.` : '',
  ];
  return lines.filter(Boolean).join('\n');
}

export function emptyVisualQa(): VisualQa {
  return { status: 'NONE', hardFails: [], checks: {} };
}

export function visualQaAllowsApprove(qa?: VisualQa) {
  if (!qa) return false;
  if (qa.hardFails.length) return false;
  if (qa.status === 'REJECT') return false;
  if (qa.status === 'PASS') return true;
  const hard = Object.entries(qa.checks);
  return hard.length > 0 && hard.every(([, ok]) => ok);
}

export function seedQaChecks(spec: VisualSpec): VisualQa {
  const checks: Record<string, boolean> = {};
  for (const r of spec.required.filter((x) => x.hard)) checks[r.id] = false;
  return { status: 'PENDING', hardFails: [], checks };
}

export function applyOperatorCheck(qa: VisualQa, id: string, ok: boolean): VisualQa {
  const checks = { ...qa.checks, [id]: ok };
  const pending = Object.values(checks).some((v) => !v);
  return {
    ...qa,
    checks,
    status: qa.hardFails.length ? 'REJECT' : pending ? 'PENDING' : 'PASS',
  };
}

export function parseVisionQa(raw: unknown, spec: VisualSpec): VisualQa {
  const o = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const axes = (o.axes && typeof o.axes === 'object' ? o.axes : {}) as Record<string, number>;
  const hardFails = Array.isArray(o.hardFails) ? o.hardFails.map((x) => String(x)) : [];
  const total = typeof o.total === 'number' ? o.total : average(Object.values(axes));
  const checks: Record<string, boolean> = {};
  for (const r of spec.required.filter((x) => x.hard)) {
    checks[r.id] = !hardFails.some((f) => f.toLowerCase().includes(r.id) || f.toLowerCase().includes(r.label.split(' ')[0]!.toLowerCase()));
  }
  if ((axes.face ?? 100) < 70) hardFails.push('MISSING_FACE');
  const uniqueFails = [...new Set(hardFails)];
  const status = uniqueFails.length || (typeof total === 'number' && total < 80) ? 'REJECT' : 'PASS';
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
    checks,
    notes: typeof o.notes === 'string' ? o.notes : undefined,
  };
}

export function approveBlockReason(qa?: VisualQa) {
  if (!qa || qa.status === 'NONE') return 'Chưa Image QA — không duyệt KF.';
  if (qa.hardFails.length) return `HARD FAIL: ${qa.hardFails.join(', ')}`;
  if (qa.status === 'REJECT') return qa.notes || 'Image QA REJECT.';
  if (qa.status === 'PENDING') return 'Tick đủ REQUIRED trên drawer rồi mới duyệt.';
  if (!visualQaAllowsApprove(qa)) return 'Image QA chưa PASS.';
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
      const boxW = Math.round(canvas.width * 0.18);
      const boxH = Math.round(canvas.height * 0.12);
      const x = Math.round(canvas.width * 0.62);
      const y = Math.round(canvas.height * 0.72);
      ctx.fillStyle = 'rgba(248, 246, 240, 0.92)';
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
