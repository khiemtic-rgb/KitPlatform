import type { ConsultationFacts } from '@/shared/api/pharmacy-consultation.api';

export type WizardQuestion = {
  code: string;
  questionVi: string;
  answerType: string;
  required: boolean;
  priority: number;
};

export type ChoiceOption = { value: string; label: string };

export type SupplementalQuestion = WizardQuestion & {
  options: ChoiceOption[];
  appliesWhen: (symptomCodes: string[]) => boolean;
  isPending: (facts: ConsultationFacts) => boolean;
};

const COUGH_CLUSTER = new Set(['cough', 'cough_dry', 'cough_phlegm']);

export const SUPPLEMENTAL_QUESTIONS: SupplementalQuestion[] = [
  {
    code: 'Q_COUGH_TYPE',
    questionVi: 'Ho khan hay có đờm?',
    answerType: 'choice',
    required: true,
    priority: 15,
    options: [
      { value: 'cough_dry', label: 'Ho khan' },
      { value: 'cough_phlegm', label: 'Có đờm' },
      { value: 'unknown', label: 'Chưa rõ / chưa hỏi' },
    ],
    appliesWhen: (symptoms) => symptoms.some((s) => COUGH_CLUSTER.has(s)),
    isPending: (facts) => {
      const hasCough = facts.symptoms.some((s) => COUGH_CLUSTER.has(s));
      if (!hasCough) return false;
      return !facts.symptoms.includes('cough_dry') && !facts.symptoms.includes('cough_phlegm');
    },
  },
  {
    code: 'Q_CHEST_PAIN',
    questionVi: 'Có đau ngực không?',
    answerType: 'boolean',
    required: true,
    priority: 45,
    options: [
      { value: 'no', label: 'Không' },
      { value: 'yes', label: 'Có' },
      { value: 'unknown', label: 'Chưa hỏi' },
    ],
    appliesWhen: (symptoms) =>
      symptoms.some((s) => COUGH_CLUSTER.has(s) || s === 'shortness_of_breath' || s === 'chest_tightness'),
    isPending: (facts) =>
      !facts.redFlags.includes('chest_pain') && !facts.notes?.includes('chest_pain:no'),
  },
];

export function isDbQuestionPending(code: string, facts: ConsultationFacts): boolean {
  switch (code) {
    case 'Q_AGE':
      return facts.ageYears == null && facts.ageMonths == null;
    case 'Q_DURATION':
      return facts.durationDays == null;
    case 'Q_FEVER':
      return facts.hasFever == null;
    case 'Q_BREATHING':
      return (
        !facts.redFlags.some((f) => f === 'difficulty_breathing' || f === 'shortness_of_breath') &&
        !facts.notes?.includes('breathing:no')
      );
    case 'Q_PREGNANCY':
      return facts.isPregnant == null && facts.gender !== 'male';
    case 'Q_BREASTFEEDING':
      return facts.isBreastfeeding == null;
    case 'Q_SEVERITY':
      return !facts.notes?.includes('severity:');
    case 'Q_MEDICATION':
      return !facts.notes?.includes('medication:');
    default:
      return false;
  }
}

export function mergePendingQuestions(
  dbQuestions: WizardQuestion[],
  symptomCodes: string[],
  facts: ConsultationFacts,
): WizardQuestion[] {
  const map = new Map<string, WizardQuestion>();

  for (const q of dbQuestions) {
    if (isDbQuestionPending(q.code, facts)) map.set(q.code, q);
  }

  for (const q of SUPPLEMENTAL_QUESTIONS) {
    if (q.appliesWhen(symptomCodes) && q.isPending(facts)) {
      map.set(q.code, q);
    }
  }

  return [...map.values()].sort((a, b) => a.priority - b.priority);
}

export function applyQuestionAnswer(facts: ConsultationFacts, code: string, raw: string): ConsultationFacts {
  const next: ConsultationFacts = {
    ...facts,
    symptoms: [...facts.symptoms],
    redFlags: [...facts.redFlags],
  };

  switch (code) {
    case 'Q_AGE': {
      const n = Number.parseInt(raw, 10);
      if (Number.isFinite(n) && n >= 0 && n <= 120) next.ageYears = n;
      break;
    }
    case 'Q_DURATION': {
      const n = Number.parseInt(raw, 10);
      if (Number.isFinite(n)) next.durationDays = n;
      break;
    }
    case 'Q_FEVER':
      next.hasFever = raw === 'yes' ? true : raw === 'no' ? false : next.hasFever;
      break;
    case 'Q_BREATHING':
      if (raw === 'yes') {
        if (!next.redFlags.includes('shortness_of_breath')) next.redFlags.push('shortness_of_breath');
      } else if (raw === 'no') {
        next.notes = appendNote(next.notes, 'breathing:no');
      }
      break;
    case 'Q_PREGNANCY':
      next.isPregnant = raw === 'yes' ? true : raw === 'no' ? false : next.isPregnant;
      break;
    case 'Q_BREASTFEEDING':
      next.isBreastfeeding = raw === 'yes' ? true : raw === 'no' ? false : next.isBreastfeeding;
      break;
    case 'Q_SEVERITY':
      next.notes = appendNote(next.notes, `severity:${raw}`);
      break;
    case 'Q_MEDICATION':
      next.notes = appendNote(next.notes, `medication:${raw}`);
      break;
    case 'Q_COUGH_TYPE':
      if (raw === 'cough_dry' || raw === 'cough_phlegm') {
        if (!next.symptoms.includes(raw)) next.symptoms.push(raw);
      }
      break;
    case 'Q_CHEST_PAIN':
      if (raw === 'yes') {
        if (!next.redFlags.includes('chest_pain')) next.redFlags.push('chest_pain');
      } else if (raw === 'no') {
        next.notes = appendNote(next.notes, 'chest_pain:no');
      }
      break;
    default:
      break;
  }

  return next;
}

function appendNote(existing: string | null | undefined, piece: string): string {
  const base = existing?.trim() ?? '';
  if (!base) return piece;
  if (base.includes(piece)) return base;
  return `${base}; ${piece}`;
}

export function durationBucketOptions(): ChoiceOption[] {
  return [
    { value: '2', label: 'Dưới 3 ngày' },
    { value: '5', label: '3–7 ngày' },
    { value: '10', label: 'Trên 7 ngày' },
  ];
}

export function yesNoOptions(): ChoiceOption[] {
  return [
    { value: 'yes', label: 'Có' },
    { value: 'no', label: 'Không' },
    { value: 'unknown', label: 'Chưa hỏi / chưa rõ' },
  ];
}

export function supplementalOptions(code: string): ChoiceOption[] | undefined {
  return SUPPLEMENTAL_QUESTIONS.find((q) => q.code === code)?.options;
}
