export const CONTENT_BRIEF_OBJECTIVES = [
  { value: 'awareness', label: 'Nhận biết' },
  { value: 'traffic', label: 'Traffic' },
  { value: 'trust', label: 'Niềm tin' },
  { value: 'educate', label: 'Giáo dục' },
  { value: 'lead', label: 'Lead' },
  { value: 'convert', label: 'Chuyển đổi' },
  { value: 'recruit', label: 'Tuyển / mời' },
] as const;

export const CONTENT_BRIEF_EMOTIONS = [
  { value: 'calm', label: 'Bình tĩnh' },
  { value: 'hope', label: 'Hy vọng' },
  { value: 'curiosity', label: 'Tò mò' },
  { value: 'urgency', label: 'Khẩn' },
  { value: 'authority', label: 'Uy tín' },
  { value: 'warmth', label: 'Ấm' },
  { value: 'pride', label: 'Tự hào' },
] as const;

export const CONTENT_BRIEF_FORMATS = [
  { value: 'web_article', label: 'Bài web' },
  { value: 'mini_story', label: 'Mini story' },
  { value: 'before_after', label: 'Before → After' },
  { value: 'fefo', label: 'FEFO / quy trình' },
  { value: 'vision', label: 'Vision / product' },
  { value: 'emotional', label: 'Emotional' },
  { value: 'documentary', label: 'Documentary' },
  { value: 'cinematic', label: 'Cinematic' },
  { value: 'social_caption', label: 'Caption MXH' },
] as const;

export const CONTENT_BRIEF_DURATIONS = [15, 30, 45, 60, 90] as const;

export function briefLabel(
  options: readonly { value: string; label: string }[],
  value?: string | null,
): string {
  if (!value) return '';
  return options.find((o) => o.value === value)?.label ?? value;
}
