/** RUNWAY_PROMPT_V1 — motion only. No Visual Contract, no spoken line, no UI dump. */

export const RUNWAY_PROMPT_COMPILER = 'RUNWAY_PROMPT_V1';
export const RUNWAY_PROMPT_MAX = 900;
export const RUNWAY_PROMPT_API_MAX = 1000;

const DIALOGUE_LINE =
  /(?:^|[;\n—–-])\s*[A-Za-zÀ-ỹ][A-Za-zÀ-ỹ'’.\s]{0,24}:\s*["“]?[^"”\n]{2,}["”]?/gu;
const QUOTED = /["“][^"”]{2,}["”]/g;
const BANNED =
  /\b(display the text|show the words|on-?screen text|write a prompt|subtitle|caption|watermark|logo|says?:|đọc chữ|hiện chữ)\b/gi;
const CONTRACT_HEAD =
  /\b(STORY|INTENT|CHARACTER|PERFORMANCE|QA|FAIL CONDITIONS|REFERENCE PACK|VOICE|VISUAL CONTRACT)\s*:/gi;

export function stripSpokenAndContract(raw?: string) {
  return (raw ?? '')
    .replace(CONTRACT_HEAD, ' ')
    .replace(DIALOGUE_LINE, ' ')
    .replace(QUOTED, ' ')
    .replace(BANNED, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

export function promptHasTextRisk(raw?: string) {
  const t = raw ?? '';
  return (
    /[A-Za-zÀ-ỹ][A-Za-zÀ-ỹ'’.\s]{0,24}:\s*["“]/.test(t) ||
    /\b(says?|display the text|show the words|write a prompt|on-?screen text)\b/i.test(t)
  );
}

/** Runway I2V law: motion + camera only. No still dump, no negatives (docs). */
export const RUNWAY_I2V_LAW_FAIL =
  /\b(no text|no captions?|no logo|watermark|do not |don't |mute take|preserve the (characters|wardrobe|room)|visual contract|fail conditions|same wardrobe|vietnamese family drama|stand in )\b/i;

export function promptViolatesRunwayI2vLaw(raw?: string) {
  const t = (raw ?? '').trim();
  if (!t) return false;
  return promptHasTextRisk(t) || RUNWAY_I2V_LAW_FAIL.test(t);
}

function actingEn(action?: string) {
  const a = (action ?? '').toLowerCase();
  if (/ức|giận|bức|frustrat|annoy/.test(a)) return 'restrained frustration';
  if (/sợ|uneasy|căng/.test(a)) return 'quiet tension';
  if (/đỏ má|xấu hổ|hurt/.test(a)) return 'flushed, held-in emotion';
  if (/mềm|thương|soft/.test(a)) return 'soft, careful delivery';
  return 'natural, contained emotion';
}

const CAMERA_HOLD = [
  'Camera remains steady.',
  'Camera eases in slightly.',
  'Camera holds, then a small push-in.',
  'Camera drifts a few centimeters right.',
  'Camera holds on the face, then eases back.',
  'Camera eases a few centimeters left.',
  'Camera holds, then a slight tilt down.',
] as const;

export const CAMERA_RETRY_POOL = CAMERA_HOLD.length;

/** retry 0 keeps coverage. retry N>0 never wraps back to that first camera line. */
export function cameraLineForRetry(retryN: number, coverage?: string) {
  const hold = (coverage ?? '').trim() || CAMERA_HOLD[0];
  if (retryN <= 0) return hold;
  const pool = CAMERA_HOLD.filter((line) => line !== hold);
  const variants = pool.length ? pool : CAMERA_HOLD;
  return variants[(retryN - 1) % variants.length];
}

export type RunwayMotionIntent = {
  action?: string;
  blocking?: string;
  prop?: string;
  gaze?: string;
  acting?: string;
  timing?: string;
  body?: string;
  room?: string;
  camera?: string;
};

function motionFromAction(action?: string): RunwayMotionIntent {
  const t = (action || '').toLowerCase();
  const out: RunwayMotionIntent = {};
  if (/đứng ở cửa|cửa phòng khách|living-room doorway|doorway/.test(t)) {
    out.action = 'Standing at the living-room doorway.';
    out.blocking = 'At the doorway, body turned toward the room.';
  }
  if (/cầm tờ giấy|hai tay cầm|sheet of paper|tờ giấy/.test(t)) {
    out.prop = 'Holds a sheet of paper with both hands.';
  }
  if (/nhìn mẹ|toward (?:his )?mother|toward linh/.test(t)) {
    out.gaze = 'Looks toward their mother, not the camera.';
  }
  if (/uneasy|mong manh|căng/.test(t)) out.acting = 'The performance is uneasy and contained.';
  return out;
}

function mergeMotion(fromAction: RunwayMotionIntent, explicit?: RunwayMotionIntent): RunwayMotionIntent {
  return {
    action: explicit?.action || fromAction.action,
    blocking: explicit?.blocking || fromAction.blocking,
    prop: explicit?.prop || fromAction.prop,
    gaze: explicit?.gaze || fromAction.gaze,
    acting: explicit?.acting || fromAction.acting,
    timing: explicit?.timing || fromAction.timing,
    body: explicit?.body || fromAction.body,
    room: explicit?.room || fromAction.room,
    camera: explicit?.camera || fromAction.camera,
  };
}

export function compileRunwayPromptV1(opts: {
  action?: string;
  diagnostic?: boolean;
  retry?: number;
  motion?: RunwayMotionIntent;
  directed?: boolean;
}) {
  const warnings: string[] = [];
  const cleaned = stripSpokenAndContract(opts.action);
  if (opts.action && cleaned !== opts.action.trim()) warnings.push('Stripped spoken line / contract from I2V prompt.');
  if (opts.diagnostic) {
    const text = 'Subtle natural movement, realistic cinematic family drama.';
    return { text, version: RUNWAY_PROMPT_COMPILER, warnings, chars: text.length };
  }
  const acting = actingEn(`${opts.action || ''} ${cleaned} ${opts.motion?.acting || ''}`);
  const retryN = Math.abs(opts.retry ?? 0);
  const camera = cameraLineForRetry(retryN, opts.motion?.camera);
  const motion = mergeMotion(motionFromAction(`${opts.action || ''} ${cleaned}`), opts.motion);
  const directed = Boolean(opts.directed);
  const bodyLine = (motion.body ?? '').trim() || (directed ? 'Performs the action in one continuous move.' : `Subtle body movement, ${acting}.`);
  const text = [
    motion.action,
    motion.blocking,
    motion.prop,
    motion.gaze,
    motion.acting || `The performance is ${acting}.`,
    motion.timing,
    bodyLine,
    motion.room,
    directed ? undefined : `Blink and breathe.`,
    /camera/i.test(camera) ? (camera.endsWith('.') ? camera : `${camera}.`) : `Camera remains steady.`,
  ]
    .filter((line) => Boolean(line && String(line).trim()))
    .join(' ');
  const clipped = text.length <= RUNWAY_PROMPT_MAX ? text : text.slice(0, RUNWAY_PROMPT_MAX);
  if (promptHasTextRisk(clipped)) warnings.push('Prompt still looks like on-screen text — review before send.');
  return { text: clipped, version: RUNWAY_PROMPT_COMPILER, warnings, chars: clipped.length };
}

export const GOLDEN_SH01_04_PROMPT_A = 'Subtle natural movement, realistic cinematic family drama.';
