import type { AssessmentQuestion } from '@/shared/api/assessment.api';

type ShowIf = {
  questionCode?: string;
  optionCode?: string;
};

export function parseShowIf(metadata?: Record<string, unknown> | null): ShowIf | null {
  if (!metadata?.showIf || typeof metadata.showIf !== 'object') return null;
  const raw = metadata.showIf as Record<string, unknown>;
  return {
    questionCode: typeof raw.questionCode === 'string' ? raw.questionCode : undefined,
    optionCode: typeof raw.optionCode === 'string' ? raw.optionCode : undefined,
  };
}

export function isQuestionVisible(
  question: AssessmentQuestion,
  answers: Record<string, string>,
  codeToId: Map<string, string>,
  idToOptionCode: Map<string, string>,
): boolean {
  const showIf = parseShowIf(question.metadata ?? null);
  if (!showIf?.questionCode || !showIf.optionCode) return true;

  const depId = codeToId.get(showIf.questionCode);
  if (!depId) return true;

  const selectedOptionId = answers[depId];
  if (!selectedOptionId) return false;

  const selectedCode = idToOptionCode.get(selectedOptionId);
  return selectedCode === showIf.optionCode;
}

export function buildQuestionMaps(questions: AssessmentQuestion[]) {
  const codeToId = new Map<string, string>();
  const idToOptionCode = new Map<string, string>();

  for (const q of questions) {
    codeToId.set(q.code, q.id);
    for (const opt of q.options) {
      idToOptionCode.set(opt.id, opt.code);
    }
  }

  return { codeToId, idToOptionCode };
}

/**
 * Tổng số câu người làm sẽ thấy. Với mỗi câu điều kiện chưa trả lời mà có câu rẽ nhánh phụ thuộc,
 * cộng thêm 1 (mỗi phương án mở đúng một câu nhánh). Nhờ vậy bộ đếm hiện "/31" ngay từ câu 1
 * thay vì "/30" rồi nhảy lên. Bộ câu hỏi không có rẽ nhánh (V1) không đổi.
 */
export function expectedQuestionCount(
  questions: AssessmentQuestion[],
  answers: Record<string, string>,
): number {
  const visible = visibleQuestions(questions, answers);
  const { codeToId } = buildQuestionMaps(questions);
  const pendingDeps = new Set<string>();
  for (const q of questions) {
    const showIf = parseShowIf(q.metadata ?? null);
    if (!showIf?.questionCode || !showIf.optionCode) continue;
    const depId = codeToId.get(showIf.questionCode);
    if (depId && !answers[depId]) pendingDeps.add(depId);
  }
  return visible.length + pendingDeps.size;
}

export function visibleQuestions(
  questions: AssessmentQuestion[],
  answers: Record<string, string>,
): AssessmentQuestion[] {
  const { codeToId, idToOptionCode } = buildQuestionMaps(questions);
  return questions.filter((q) => isQuestionVisible(q, answers, codeToId, idToOptionCode));
}
