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

export function compileRunwayPromptV1(opts: {
  action?: string;
  diagnostic?: boolean;
}) {
  const warnings: string[] = [];
  const cleaned = stripSpokenAndContract(opts.action);
  if (opts.action && cleaned !== opts.action.trim()) warnings.push('Stripped spoken line / contract from I2V prompt.');
  if (opts.diagnostic) {
    const text = 'Subtle natural movement, realistic cinematic family drama.';
    return { text, version: RUNWAY_PROMPT_COMPILER, warnings, chars: text.length };
  }
  const acting = actingEn(`${opts.action || ''} ${cleaned}`);
  const text = [
    `Subtle body movement, ${acting}.`,
    `Blink and breathe.`,
    `Camera remains steady.`,
  ].join(' ');
  const clipped = text.length <= RUNWAY_PROMPT_MAX ? text : text.slice(0, RUNWAY_PROMPT_MAX);
  if (promptHasTextRisk(clipped)) warnings.push('Prompt still looks like on-screen text — review before send.');
  return { text: clipped, version: RUNWAY_PROMPT_COMPILER, warnings, chars: clipped.length };
}

export const GOLDEN_SH01_04_PROMPT_A = 'Subtle natural movement, realistic cinematic family drama.';
