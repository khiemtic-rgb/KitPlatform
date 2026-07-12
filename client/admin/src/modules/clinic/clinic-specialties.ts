/** Danh mục chuyên khoa chuẩn (VN) — dùng chung form BS phòng khám / Connect Team. */
export const CLINIC_SPECIALTIES = [
  'Đa khoa',
  'Nội tổng quát',
  'Nhi',
  'Sản phụ khoa',
  'Ngoại tổng quát',
  'Tai Mũi Họng',
  'Răng Hàm Mặt',
  'Mắt',
  'Da liễu',
  'Cơ xương khớp',
  'Tim mạch',
  'Hô hấp',
  'Tiêu hóa',
  'Thần kinh',
  'Nội tiết',
  'Thận - Tiết niệu',
  'Ung bướu',
  'Tâm thần',
  'Phục hồi chức năng',
  'Y học cổ truyền',
  'Dinh dưỡng',
  'Chẩn đoán hình ảnh',
  'Xét nghiệm',
  'Gây mê hồi sức',
  'Cấp cứu',
] as const;

export type ClinicSpecialty = (typeof CLINIC_SPECIALTIES)[number];

/**
 * Alias / gõ thiếu dấu / ASCII legacy → nhãn chuẩn.
 * Key so sánh: bỏ dấu, lower, gộp khoảng trắng.
 */
const SPECIALTY_ALIAS_BY_KEY: Record<string, ClinicSpecialty> = {
  dakhoa: 'Đa khoa',
  'da khoa': 'Đa khoa',
  general: 'Đa khoa',
  gp: 'Đa khoa',
  noi: 'Nội tổng quát',
  'noi tong quat': 'Nội tổng quát',
  'noi tongquat': 'Nội tổng quát',
  internal: 'Nội tổng quát',
  'internal medicine': 'Nội tổng quát',
  nhi: 'Nhi',
  pediatrics: 'Nhi',
  san: 'Sản phụ khoa',
  'san phu khoa': 'Sản phụ khoa',
  obgyn: 'Sản phụ khoa',
  ngoai: 'Ngoại tổng quát',
  'ngoai tong quat': 'Ngoại tổng quát',
  surgery: 'Ngoại tổng quát',
  tmh: 'Tai Mũi Họng',
  'tai mui hong': 'Tai Mũi Họng',
  ent: 'Tai Mũi Họng',
  rhm: 'Răng Hàm Mặt',
  'rang ham mat': 'Răng Hàm Mặt',
  dental: 'Răng Hàm Mặt',
  mat: 'Mắt',
  eye: 'Mắt',
  ophthalmology: 'Mắt',
  'da lieu': 'Da liễu',
  dermatology: 'Da liễu',
  'co xuong khop': 'Cơ xương khớp',
  'tim mach': 'Tim mạch',
  cardiology: 'Tim mạch',
  'ho hap': 'Hô hấp',
  'tieu hoa': 'Tiêu hóa',
  'than kinh': 'Thần kinh',
  'noi tiet': 'Nội tiết',
  'than tiet nieu': 'Thận - Tiết niệu',
  'than - tiet nieu': 'Thận - Tiết niệu',
  'ung buou': 'Ung bướu',
  'tam than': 'Tâm thần',
  'phuc hoi chuc nang': 'Phục hồi chức năng',
  'y hoc co truyen': 'Y học cổ truyền',
  'dinh duong': 'Dinh dưỡng',
  'chan doan hinh anh': 'Chẩn đoán hình ảnh',
  'xet nghiem': 'Xét nghiệm',
  'gay me hoi suc': 'Gây mê hồi sức',
  'cap cuu': 'Cấp cứu',
  emergency: 'Cấp cứu',
};

function stripDiacritics(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D');
}

function specialtyLookupKey(raw: string): string {
  return stripDiacritics(raw)
    .trim()
    .toLowerCase()
    .replace(/[_/|]+/g, ' ')
    .replace(/\s+/g, ' ');
}

/** Chuẩn hóa nhãn chuyên khoa (Việt hóa / sửa thiếu dấu). */
export function normalizeClinicSpecialty(raw: string | null | undefined): string | undefined {
  const trimmed = raw?.trim();
  if (!trimmed) return undefined;

  const exact = CLINIC_SPECIALTIES.find((s) => s === trimmed);
  if (exact) return exact;

  const byFold = CLINIC_SPECIALTIES.find(
    (s) => specialtyLookupKey(s) === specialtyLookupKey(trimmed),
  );
  if (byFold) return byFold;

  const aliased = SPECIALTY_ALIAS_BY_KEY[specialtyLookupKey(trimmed)];
  if (aliased) return aliased;

  return trimmed;
}

/** Options Select: danh mục chuẩn + legacy (đã Việt hóa), không nhân đôi alias. */
export function clinicSpecialtySelectOptions(
  extras: Array<string | null | undefined> = [],
): Array<{ value: string; label: string }> {
  const set = new Set<string>(CLINIC_SPECIALTIES);
  for (const raw of extras) {
    const normalized = normalizeClinicSpecialty(raw);
    if (normalized) set.add(normalized);
  }
  return [...set]
    .sort((a, b) => a.localeCompare(b, 'vi'))
    .map((value) => ({ value, label: value }));
}
