/** Soft weekly calibration + progressive context chips (RE P1.5 / P1.15). */

export type SoftCalQuestion = {
  id: string;
  questionVi: string;
  options: Array<{ code: string; labelVi: string }>;
  /** JSON path under layers */
  layerPath: 'child.preferences.praiseStyle' | 'child.preferences.motivateStyle' | 'context.chips';
};

export const SOFT_CAL_QUESTIONS: SoftCalQuestion[] = [
  {
    id: 'praise_style',
    questionVi: 'Con thích được khen riêng hơn trước mặt anh chị?',
    options: [
      { code: 'private', labelVi: 'Khen riêng' },
      { code: 'open', labelVi: 'Khen trước cả nhà' },
      { code: 'unsure', labelVi: 'Chưa rõ' },
    ],
    layerPath: 'child.preferences.praiseStyle',
  },
  {
    id: 'motivate_style',
    questionVi: 'Khi con nản, cách nào thường hiệu quả hơn?',
    options: [
      { code: 'effort', labelVi: 'Khen nỗ lực' },
      { code: 'presence', labelVi: 'Ở cạnh / ôm' },
      { code: 'team', labelVi: 'Nhắc cả đội' },
    ],
    layerPath: 'child.preferences.motivateStyle',
  },
];

export const CONTEXT_CHIP_OPTIONS: Array<{ code: string; labelVi: string }> = [
  { code: 'exam_season', labelVi: 'Đang mùa thi' },
  { code: 'busy_evenings', labelVi: 'Tối hay bận' },
  { code: 'grandparents_help', labelVi: 'Ông bà cùng chăm' },
  { code: 'one_parent_primary', labelVi: 'Một người chăm chính' },
  { code: 'siblings_close', labelVi: 'Anh chị em thân' },
];

export const CHILD_PRAISE_OPTIONS: Array<{ code: string; labelVi: string }> = [
  { code: 'private', labelVi: 'Thích khen riêng' },
  { code: 'open', labelVi: 'Thích khen trước nhà' },
  { code: 'effort', labelVi: 'Thích khen nỗ lực' },
];

function weekKey(d = new Date()): string {
  const day = new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
  const dayNum = day.getUTCDay() || 7;
  day.setUTCDate(day.getUTCDate() + 4 - dayNum);
  const yearStart = new Date(Date.UTC(day.getUTCFullYear(), 0, 1));
  const weekNo = Math.ceil(((day.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
  return `${day.getUTCFullYear()}-W${weekNo}`;
}

export function softCalStorageKey(familyId: string): string {
  return `famixa.softCal.v1:${familyId}:${weekKey()}`;
}

export function hasSoftCalAnsweredThisWeek(familyId: string): boolean {
  try {
    return localStorage.getItem(softCalStorageKey(familyId)) === '1';
  } catch {
    return false;
  }
}

export function markSoftCalAnsweredThisWeek(familyId: string): void {
  try {
    localStorage.setItem(softCalStorageKey(familyId), '1');
  } catch {
    /* ignore */
  }
}

/** Rotate question by week index. */
export function softCalQuestionForWeek(d = new Date()): SoftCalQuestion {
  const wk = weekKey(d);
  const n = Number(wk.replace(/\D/g, '')) || 0;
  return SOFT_CAL_QUESTIONS[n % SOFT_CAL_QUESTIONS.length];
}

export function parseLayersJson(raw: string | undefined | null): Record<string, unknown> {
  if (!raw) return {};
  try {
    const o = JSON.parse(raw) as unknown;
    return o && typeof o === 'object' && !Array.isArray(o) ? (o as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

export function readContextChips(layers: Record<string, unknown>): string[] {
  const ctx = layers.context;
  if (!ctx || typeof ctx !== 'object') return [];
  const chips = (ctx as { chips?: unknown }).chips;
  return Array.isArray(chips) ? chips.map(String) : [];
}

export function readMemberPraiseStyle(
  layers: Record<string, unknown>,
  memberId: string,
): string | null {
  const members = layers.members;
  if (!members || typeof members !== 'object') return null;
  const m = (members as Record<string, { praiseStyle?: string }>)[memberId];
  return m?.praiseStyle ?? null;
}

export function readChildPreferences(layers: Record<string, unknown>): {
  praiseStyle?: string;
  motivateStyle?: string;
} {
  const child = layers.child;
  if (!child || typeof child !== 'object') return {};
  const prefs = (child as { preferences?: Record<string, string> }).preferences;
  if (!prefs || typeof prefs !== 'object') return {};
  return {
    praiseStyle: prefs.praiseStyle,
    motivateStyle: prefs.motivateStyle,
  };
}

/** Deep-merge objects (arrays replace). Server MergeJson is top-level shallow. */
export function deepMergeLayers(
  base: Record<string, unknown>,
  patch: Record<string, unknown>,
): Record<string, unknown> {
  const out: Record<string, unknown> = { ...base };
  for (const [k, v] of Object.entries(patch)) {
    const prev = base[k];
    if (
      v &&
      typeof v === 'object' &&
      !Array.isArray(v) &&
      prev &&
      typeof prev === 'object' &&
      !Array.isArray(prev)
    ) {
      out[k] = deepMergeLayers(
        prev as Record<string, unknown>,
        v as Record<string, unknown>,
      );
    } else {
      out[k] = v;
    }
  }
  return out;
}

export function softCalAnswerPatch(
  question: SoftCalQuestion,
  answerCode: string,
): Record<string, unknown> {
  const at = new Date().toISOString();
  if (question.layerPath === 'child.preferences.praiseStyle') {
    return {
      child: {
        preferences: {
          praiseStyle: answerCode,
          updatedAt: at,
          lastQuestionId: question.id,
        },
      },
    };
  }
  if (question.layerPath === 'child.preferences.motivateStyle') {
    return {
      child: {
        preferences: {
          motivateStyle: answerCode,
          updatedAt: at,
          lastQuestionId: question.id,
        },
      },
    };
  }
  return {
    context: { chips: [answerCode], updatedAt: at },
  };
}

export function contextChipsPatch(chips: string[]): Record<string, unknown> {
  return {
    context: {
      chips,
      updatedAt: new Date().toISOString(),
    },
  };
}

export function memberPraisePatch(
  memberId: string,
  praiseStyle: string,
): Record<string, unknown> {
  return {
    members: {
      [memberId]: {
        praiseStyle,
        updatedAt: new Date().toISOString(),
      },
    },
  };
}

/** Soft "Vì nhà bạn…" from progressive prefs (alongside DNA). */
export function becauseFromSoftPrefs(layers: Record<string, unknown>): string | null {
  const prefs = readChildPreferences(layers);
  if (prefs.praiseStyle === 'private') {
    return 'Vì nhà bạn: con thích được khen riêng hơn trước mặt anh chị';
  }
  if (prefs.praiseStyle === 'open') {
    return 'Vì nhà bạn: con thích được khen trước cả nhà';
  }
  if (prefs.motivateStyle === 'effort') {
    return 'Vì nhà bạn: con thường đáp ứng tốt khi được khen nỗ lực';
  }
  if (prefs.motivateStyle === 'presence') {
    return 'Vì nhà bạn: khi nản, con cần bố mẹ ở cạnh hơn là nhắc việc';
  }
  if (prefs.motivateStyle === 'team') {
    return 'Vì nhà bạn: con được kéo theo khi cả đội cùng nhắc';
  }
  const chips = readContextChips(layers);
  if (chips.includes('exam_season')) {
    return 'Vì nhà bạn đang mùa thi — ưu tiên lời ấm và nhịp nhẹ';
  }
  if (chips.includes('busy_evenings')) {
    return 'Vì nhà bạn tối hay bận — một câu ngắn vẫn đủ ấm';
  }
  return null;
}
