/** Lightweight VN name → gender hint for kid avatars (no DB gender yet). */
export type AvatarGender = 'girl' | 'boy' | 'neutral';

const GIRL_TOKENS = [
  'nhi', 'linh', 'my', 'vy', 'chau', 'huong', 'lan', 'mai', 'ngoc', 'phuong',
  'quynh', 'thao', 'trang', 'uyen', 'yen', 'ha', 'hang', 'hanh', 'diem', 'duyen',
  'chi', 'tram', 'thuy', 'thu', 'nhu', 'an', 'anhthu', 'baoanh', 'baonhi',
  'kim', 'loan', 'nga', 'oanh', 'phuonganh', 'quynhchi', 'thanhha', 'tuyet',
];

const BOY_TOKENS = [
  'huy', 'duc', 'nam', 'tuan', 'hung', 'dung', 'phong', 'khoa', 'long', 'khang',
  'minh', 'quan', 'dat', 'son', 'tai', 'thinh', 'trung', 'viet', 'vu',
  'anhtuan', 'duchuy', 'hoang', 'khanh', 'lam', 'phuc', 'quang', 'thanh',
];

function stripDiacritics(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'd');
}

function tokensOf(name: string): string[] {
  const raw = stripDiacritics(name).toLowerCase().trim();
  const parts = raw.split(/[\s._-]+/).filter(Boolean);
  const joined = parts.join('');
  return [...parts, joined].filter(Boolean);
}

/** Prefer given name (last token) — VN: Họ Đệm Tên */
export function inferGenderFromName(name: string): AvatarGender {
  const tokens = tokensOf(name);
  if (tokens.length === 0) return 'neutral';

  const parts = stripDiacritics(name)
    .toLowerCase()
    .trim()
    .split(/[\s._-]+/)
    .filter(Boolean);
  const given = parts[parts.length - 1] ?? parts[0] ?? '';
  const ordered = [...new Set([given, ...tokens])];

  for (const t of ordered) {
    if (GIRL_TOKENS.includes(t)) return 'girl';
  }
  for (const t of ordered) {
    if (BOY_TOKENS.includes(t)) return 'boy';
  }

  if (/(nhi|linh|vy|my)$/.test(given)) return 'girl';
  return 'neutral';
}

export function avatarEmoji(gender: AvatarGender, roleCode?: string): string {
  if (roleCode && roleCode !== 'child') {
    return gender === 'girl' ? '👩' : gender === 'boy' ? '👨' : '🧑';
  }
  if (gender === 'girl') return '👧';
  if (gender === 'boy') return '👦';
  return '🧒';
}

export function avatarToneClass(gender: AvatarGender): string {
  if (gender === 'girl') return 'is-girl';
  if (gender === 'boy') return 'is-boy';
  return 'is-neutral';
}
