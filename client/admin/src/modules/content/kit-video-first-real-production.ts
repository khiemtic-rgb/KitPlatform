export const FIRST_REAL_PRODUCTION_ID = 'FAMIXA_FIRST_REAL_PRODUCTION_V1';

export const FIRST_REAL_PROVIDERS = [
  { id: '', label: 'Chưa chọn' },
  { id: 'GEMINI', label: 'Gemini' },
  { id: 'RUNWAY', label: 'Runway' },
  { id: 'VEO', label: 'Veo' },
] as const;

export const STAFF_CRP_BLOCK = 'Bộ ảnh chuẩn của nhân vật chưa hoàn tất. Không thể tạo hình production.';

export function staffShotLabel(shotCode?: string) {
  const match = (shotCode || '').match(/SHOT-0*(\d+)/i);
  return match ? `Shot ${String(match[1]).padStart(2, '0')}` : shotCode || 'Shot';
}

export function pickShot001<T extends { shotCode: string }>(shots: T[]) {
  return shots.find((row) => /SHOT-001\b/i.test(row.shotCode)) ?? shots[0];
}

export function crpReadyLabel(usable: boolean) {
  return usable ? 'Đã sẵn sàng' : 'Chưa hoàn tất';
}

export function canOfferGenerate(input: {
  crpUsable: boolean;
  provider: string;
  alreadyGenerated?: boolean;
  shotCode?: string;
}) {
  if (input.shotCode) {
    const match = input.shotCode.toUpperCase().match(/SHOT-(\d+)/);
    if (!match || Number(match[1]) !== 1) return false;
  }
  return input.crpUsable && input.provider === 'GEMINI' && !input.alreadyGenerated;
}

export function confirmCopy(input: {
  shotLabel: string;
  character: string;
  location: string;
  action: string;
  provider: string;
}) {
  return {
    title: `Tạo hình cho ${input.shotLabel}?`,
    character: input.character,
    location: input.location,
    action: input.action,
    provider: input.provider,
    note: `Famixa sẽ tạo 01 hình ảnh cho ${input.shotLabel} bằng ${input.provider} dựa trên bộ ảnh chuẩn và yêu cầu sản xuất đã khóa.\n\nĐây là lần tạo hình production đầu tiên.\n\nSau khi tạo, hình ảnh sẽ được đưa vào bước Duyệt hình.`,
  };
}
