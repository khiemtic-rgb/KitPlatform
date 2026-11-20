import type { ConsultationSuggestResult } from '@/shared/api/pharmacy-consultation.api';

/** Câu hỏi bắt buộc trước khi giao thuốc có hoạt chất cần sàng lọc (backend gắn mã `screen_*`). */
export function screeningQuestions(result: ConsultationSuggestResult | null | undefined): string[] {
  if (!result) return [];
  return result.safetyFlags
    .filter((flag) => flag.code.startsWith('screen_'))
    .map((flag) => flag.message)
    .filter((message) => message.trim().length > 0);
}
