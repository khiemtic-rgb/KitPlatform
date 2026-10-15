/** Mirrors server ConsultationSafetyRules.BlocksAutomaticOtcSuggestions, plus assessment red-flag hold. */
export function blocksAutomaticOtcSuggestions(
  safetyLevel: string,
  assessmentLevel?: string | null,
): boolean {
  return safetyLevel === 'stop_sale'
    || safetyLevel === 'refer_medical'
    || assessmentLevel === 'needs_evaluation';
}
