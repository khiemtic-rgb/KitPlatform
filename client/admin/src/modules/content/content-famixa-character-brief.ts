/** CHAR-001 Minh Character Brief V1 — human design before Visual DNA. Not Master. Not locked face. */

export const MINH_BRIEF_ID = 'FAMIXA-CHAR-001-BRIEF-V1';
export const MINH_BRIEF_NEXT = 'FAMIXA-CHAR-001-VISUAL-DNA-SPEC-V1';

export const MINH_BRIEF_NOT_LOCKED = [
  'face',
  'hair',
  'skin',
  'height',
  'weight',
  'outfit',
  'voiceId',
  'masterReference',
  'expressionReference',
] as const;

export type FamixaCharacterBrief = {
  documentId: string;
  characterId: string;
  status: 'BRIEF';
  readyFor: 'VISUAL_DESIGN';
  statement: string;
  oneLine: string;
  emotionalCore: string;
  notA: string[];
  notLocked: string[];
  nextStep: string;
  visualDirection?: string;
  familyNote?: string;
};

export const MINH_CHARACTER_STATEMENT =
  'Minh is an ordinary Vietnamese boy growing up, curious, confident, mildly stubborn, sensitive to adults\' words, responsible, and deeply wanting to be trusted. He is not weak or overly dramatic. His emotions are subtle and expressed primarily through his eyes, mouth, posture, silence, and small behavioral changes.';

export const MINH_EMOTIONAL_DNA =
  'Một cậu bé đang lớn, muốn được tin tưởng — nhưng vẫn chưa biết cách nói ra điều đó.';

export const CHAR_001_MINH_BRIEF: FamixaCharacterBrief = {
  documentId: MINH_BRIEF_ID,
  characterId: 'CHAR-001',
  status: 'BRIEF',
  readyFor: 'VISUAL_DESIGN',
  statement: MINH_CHARACTER_STATEMENT,
  oneLine: MINH_EMOTIONAL_DNA,
  emotionalCore: 'Minh muốn được tin tưởng.',
  notA: [
    'gifted child',
    'perfect child',
    'cartoon comedy kid',
    'con nhà người ta',
    'always pitiful',
    'always crying / head-down / weak',
  ],
  notLocked: [...MINH_BRIEF_NOT_LOCKED],
  nextStep: MINH_BRIEF_NEXT,
  visualDirection:
    'Famixa look: stylized-human cinematic family drama. Everyday Vietnamese boy with a recognizable face — not kid-model, K-drama, fashion, cartoon, anime, or generic any-boy.',
  familyNote:
    'Ông / Bà / Em gái chưa có Character ID. CHAR-002 = Nam (Bố), CHAR-003 = Linh (Mẹ). CHAR-004 = An (bạn, mention-only) — không phải em gái. Không gộp gia đình vào CHAR-001. Không tự cấp CHAR-005+.',
};

export const MINH_BRIEF_PERSONALITY = {
  core: ['curious', 'confident', 'mildly stubborn', 'sensitive', 'responsible', 'wants independence'],
  strengths: ['thinks for himself', 'observes', 'learns from experience', 'can change', 'can love', 'can take responsibility', 'gets back up after disappointment', 'has agency'],
  weaknesses: [
    'mildly stubborn',
    'easily hurt by words',
    'reacts before thinking',
    'cannot yet name feelings',
    'tries to prove himself too fast',
  ],
  fears: [
    'disappointing parents',
    'being looked down on',
    'being compared',
    'being told he is not enough',
    'speaking and not being heard',
    'trying and not being recognized',
  ],
  needs: ['to be trusted', 'to be seen as growing up'],
  desires: ['parents see that I am growing'],
  values: ['trust', 'recognition', 'independence without abandoning family'],
  triggers: [
    'parents\' words',
    'a look',
    'comparison',
    'disappointment',
    'being doubted',
    'being treated as too young',
  ],
  emotionalPatterns: [
    'EXCITED / BRIGHT',
    'DISAPPOINTED',
    'CLOSED / ALONE',
    'DETERMINED',
    'VULNERABLE',
    'UNDERSTANDING',
    'OPEN / WARM',
  ],
};

export const MINH_BRIEF_BEHAVIOR = [
  { when: 'happy', does: 'eyes look for the adult; body open; real child smile — not ad smile' },
  { when: 'hurt', does: 'eyes look away; stands still; smile fades; may fold the paper; becomes quiet rather than crying' },
  { when: 'angry', does: 'holds a longer look; blinks less; body stiffer; speaks less' },
  { when: 'sad', does: 'eyes lose energy; may press lips; swallows the feeling; says không sao' },
  { when: 'wants recognition', does: 'looks at the other person and waits' },
  { when: 'refused', does: 'shoulders pull in; may go quiet, close the door, or pretend nothing happened' },
  { when: 'determined', does: 'stance firmer; not only endures — acts' },
  { when: 'embarrassed', does: 'looks down; may hesitate mid-sentence' },
];

export const MINH_BRIEF_EVOLUTION = {
  initialEra: 'A11',
  eras: [
    { era: 'A11', status: 'active' as const, note: 'child; soft face; child body; child voice' },
    { era: 'A13', status: 'planned' as const, note: 'learning to express himself' },
    { era: 'A16', status: 'planned' as const, note: 'same Minh, teen; still recognizable from A11; teen voice' },
    { era: 'A18', status: 'planned' as const, note: 'transition' },
    { era: 'A23', status: 'planned' as const, note: 'adult Minh, not a new man; deeper eyes; adult voice' },
  ],
};

/** Planned family roles — no Character ID until created. */
export const MINH_PLANNED_FAMILY = [
  { role: 'grandfather', label: 'Ông', characterCode: undefined },
  { role: 'grandmother', label: 'Bà', characterCode: undefined },
  { role: 'sister', label: 'Em gái', characterCode: undefined },
] as const;

export function isMinhBrief(brief?: { documentId?: string; characterId?: string } | null) {
  return brief?.documentId === MINH_BRIEF_ID && brief.characterId === 'CHAR-001';
}

export function briefDoesNotLockVisual(brief?: FamixaCharacterBrief | null) {
  if (!brief) return true;
  return MINH_BRIEF_NOT_LOCKED.every((k) => brief.notLocked.includes(k));
}

/** Merge Brief into Canon. Never writes face/hair/voiceId/master refs. Never invents family IDs. */
export function mergeMinhBriefIntoCanon(canon: {
  identity: Record<string, unknown>;
  personality?: unknown;
  personalityDna?: string[];
  behaviorDna?: { when: string; does: string }[];
  relationships?: { to: string; type?: string }[];
  evolution?: unknown;
  continuityRules?: string[];
  brief?: unknown;
  plannedFamily?: unknown;
}) {
  return {
    ...canon,
    identity: {
      ...canon.identity,
      characterId: 'CHAR-001',
      name: 'Minh',
      role: 'Main Child',
      familyRole: 'Son',
      characterType: 'CORE',
      currentAge: 11,
      initialAge: 11,
      currentEra: 'A11',
      gender: canon.identity.gender || 'male',
      biography:
        'Ordinary Vietnamese boy, 11, growing up. Emotional center of Famixa. Wants to be trusted. Not a perfect child, not a pitiful child.',
    },
    brief: CHAR_001_MINH_BRIEF,
    personality: MINH_BRIEF_PERSONALITY,
    personalityDna: MINH_BRIEF_PERSONALITY.core,
    behaviorDna: MINH_BRIEF_BEHAVIOR,
    plannedFamily: MINH_PLANNED_FAMILY.map((r) => ({ role: r.role, label: r.label })),
    evolution: {
      initialEra: 'A11',
      eras: MINH_BRIEF_EVOLUTION.eras.map((e) => ({ era: e.era, status: e.status })),
    },
    relationships: [
      { to: 'CHAR-003', type: 'mother' },
      { to: 'CHAR-002', type: 'father' },
      { to: 'CHAR-004', type: 'friend' },
    ],
    continuityRules: [
      ...(canon.continuityRules ?? []).filter((r) => !/brief|trusted|not weak/i.test(r)),
      MINH_EMOTIONAL_DNA,
      'Do not play Minh as always sad, crying, or pitiful.',
      'Emotion is Shot State — do not write Minh rất buồn into Identity.',
      'A16 / A23 are CHAR-001 eras, not a new Character ID.',
      'Sister / Ông / Bà have no Character ID yet. An is CHAR-004 friend, not sister.',
    ],
  };
}
