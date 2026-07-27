/** Giới tính người thân: 1 = nam, 2 = nữ (khớp API Customer Family). */
export const FAMILY_GENDER = {
  male: 1,
  female: 2,
} as const;

export type FamilyGenderCode = (typeof FAMILY_GENDER)[keyof typeof FAMILY_GENDER];

/** Suy gender khi API không có — giữ ổn định theo seed (id). */
export function resolveFamilyGender(
  gender: number | null | undefined,
  seed?: string,
): number | null {
  if (gender != null) return Number(gender);
  if (!seed) return null;
  return (Array.from(seed).reduce((acc, ch) => acc + ch.charCodeAt(0), 0) % 2) + 1;
}

/**
 * Nhãn quan hệ có giới tính (Cha/Mẹ/Con trai…) — dùng Home + danh sách Gia đình.
 * `t` nhận key dạng `home.familyRoles.father`.
 */
export function familyRoleLabel(
  relationship: string,
  gender: number | null | undefined,
  t: (key: string) => string,
  fallback: (key: string) => string,
  seed?: string,
): string {
  const g = resolveFamilyGender(gender, seed);
  const key =
    relationship === 'parent' && g === FAMILY_GENDER.male
      ? 'father'
      : relationship === 'parent' && g === FAMILY_GENDER.female
        ? 'mother'
        : relationship === 'child' && g === FAMILY_GENDER.male
          ? 'son'
          : relationship === 'child' && g === FAMILY_GENDER.female
            ? 'daughter'
            : relationship === 'spouse' && g === FAMILY_GENDER.male
              ? 'husband'
              : relationship === 'spouse' && g === FAMILY_GENDER.female
                ? 'wife'
                : relationship === 'sibling' && g === FAMILY_GENDER.male
                  ? 'brother'
                  : relationship === 'sibling' && g === FAMILY_GENDER.female
                    ? 'sister'
                    : null;
  if (key) {
    const specific = t(`home.familyRoles.${key}`);
    if (specific && specific !== `home.familyRoles.${key}`) return specific;
  }
  return fallback(relationship);
}
