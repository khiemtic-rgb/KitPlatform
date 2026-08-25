/** Triệu chứng phổ biến tại quầy — hiển thị nhanh; full taxonomy qua drawer. */
export const QUICK_SYMPTOM_CODES = [
  'cough',
  'sore_throat',
  'fever',
  'runny_nose',
  'headache',
  'diarrhea',
  'nausea',
  'abdominal_pain',
  'nasal_congestion',
  'sneezing',
  'body_ache',
  'allergy',
] as const;

export type QuickSymptomCode = (typeof QUICK_SYMPTOM_CODES)[number];

export const QUICK_SYMPTOM_ICONS: Partial<Record<QuickSymptomCode, string>> = {
  cough: '🤧',
  sore_throat: '😷',
  fever: '🌡️',
  runny_nose: '💧',
  headache: '🤕',
  diarrhea: '💊',
  nausea: '🤢',
  abdominal_pain: '🫃',
  nasal_congestion: '😤',
  sneezing: '🤧',
  body_ache: '💪',
  allergy: '🌸',
};
