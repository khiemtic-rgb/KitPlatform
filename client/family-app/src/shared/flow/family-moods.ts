export const FAMILY_MOODS = [
  { code: 'mad', emoji: '😠', label: 'Giận' },
  { code: 'sad', emoji: '😟', label: 'Buồn' },
  { code: 'ok', emoji: '😐', label: 'Bình thường' },
  { code: 'happy', emoji: '😊', label: 'Vui vẻ' },
  { code: 'love', emoji: '🤩', label: 'Tuyệt vời' },
] as const;

export type FamilyMoodCode = (typeof FAMILY_MOODS)[number]['code'];

export function moodIndexFromCode(code?: string | null): number {
  const idx = FAMILY_MOODS.findIndex((m) => m.code === code);
  return idx >= 0 ? idx : 3;
}

export function moodFromCode(code?: string | null) {
  return FAMILY_MOODS[moodIndexFromCode(code)];
}
