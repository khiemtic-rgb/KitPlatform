/** Lý do bỏ liều — soft accountability (vay pattern Family OS, nội dung care). */
export const MED_SKIP_REASON_OPTIONS = [
  { value: 'forgot', labelKey: 'reminders.skipReasons.forgot' },
  { value: 'away', labelKey: 'reminders.skipReasons.away' },
  { value: 'unwell', labelKey: 'reminders.skipReasons.unwell' },
  { value: 'out_of_stock', labelKey: 'reminders.skipReasons.outOfStock' },
  { value: 'other', labelKey: 'reminders.skipReasons.other' },
] as const;

export type MedSkipReasonCode = (typeof MED_SKIP_REASON_OPTIONS)[number]['value'];
