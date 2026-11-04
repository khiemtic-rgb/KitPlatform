/** Category icons + AI suggest helpers for symptom picker modal. */

export const SYMPTOM_CATEGORY_ICONS: Record<string, string> = {
  respiratory: '🫁',
  fever_pain: '🌡️',
  digestive: '🍽️',
  ent: '👂',
  eye: '👁️',
  oral_dental: '🦷',
  skin_allergy: '🧴',
  musculoskeletal: '🦴',
  urinary: '💧',
  women_health: '♀️',
  men_health: '♂️',
  child_health: '👶',
  neuro_sleep: '🧠',
  nutrition_general: '🥗',
  chronic_condition: '📋',
  injury_first_aid: '🩹',
  cardiovascular: '❤️',
  other: '⋯',
};

/** Categories shown first in overview strip (mockup order). */
export const SYMPTOM_OVERVIEW_CATEGORY_CODES = [
  'respiratory',
  'digestive',
  'ent',
  'eye',
  'skin_allergy',
  'other',
] as const;

const RELATED_BY_SYMPTOM: Record<string, string[]> = {
  cough: ['fever', 'sore_throat', 'runny_nose', 'nasal_congestion'],
  cough_dry: ['sore_throat', 'fever', 'voice_hoarseness'],
  cough_phlegm: ['fever', 'sore_throat', 'cough_dry', 'runny_nose'],
  sore_throat: ['fever', 'cough', 'runny_nose'],
  runny_nose: ['sneezing', 'nasal_congestion', 'sore_throat', 'fever'],
  nasal_congestion: ['runny_nose', 'sneezing', 'headache'],
  sneezing: ['runny_nose', 'allergy', 'itchy_eye'],
  fever: ['headache', 'body_ache', 'cough'],
  headache: ['fever', 'cough', 'runny_nose', 'sore_throat', 'nausea', 'dizziness', 'nasal_congestion', 'body_ache'],
  dizziness: ['headache', 'nausea', 'vomiting', 'fever'],
  vomiting: ['nausea', 'diarrhea', 'abdominal_pain', 'fever'],
  diarrhea: ['nausea', 'abdominal_pain', 'dehydration_signs'],
  nausea: ['vomiting', 'abdominal_pain', 'diarrhea'],
  abdominal_pain: ['diarrhea', 'nausea', 'bloating'],
  bloating: ['abdominal_pain', 'constipation', 'heartburn'],
  constipation: ['bloating', 'abdominal_pain'],
  heartburn: ['reflux', 'bloating', 'nausea'],
  reflux: ['heartburn', 'bloating'],
  allergy: ['itchy_skin', 'rash', 'hives', 'sneezing'],
  itchy_skin: ['allergy', 'rash', 'hives'],
  rash: ['allergy', 'itchy_skin', 'hives'],
  hives: ['allergy', 'itchy_skin', 'rash'],
  toothache: ['fever', 'body_ache'],
  back_pain: ['joint_pain', 'body_ache'],
  joint_pain: ['back_pain', 'body_ache'],
  fatigue: ['dizziness', 'insomnia'],
  insomnia: ['fatigue', 'dizziness'],
  red_eye: ['itchy_eye', 'watery_eye', 'dry_eye', 'eye_discharge'],
  itchy_eye: ['red_eye', 'watery_eye', 'allergy', 'sneezing'],
  watery_eye: ['red_eye', 'itchy_eye', 'allergy', 'runny_nose'],
  dry_eye: ['itchy_eye', 'red_eye'],
  eye_pain: ['red_eye', 'watery_eye', 'blurred_vision'],
  eye_discharge: ['red_eye', 'itchy_eye'],
};

/**
 * Suggest up to `limit` related symptom codes not already selected.
 */
export function suggestRelatedSymptomCodes(
  selectedCodes: string[],
  limit = 3,
): string[] {
  const selected = new Set(selectedCodes.map((c) => c.toLowerCase()));
  const scores = new Map<string, number>();

  for (const code of selectedCodes) {
    for (const related of RELATED_BY_SYMPTOM[code] ?? []) {
      if (selected.has(related.toLowerCase())) continue;
      scores.set(related, (scores.get(related) ?? 0) + 1);
    }
  }

  return [...scores.entries()]
    .sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
    .slice(0, limit)
    .map(([code]) => code);
}
