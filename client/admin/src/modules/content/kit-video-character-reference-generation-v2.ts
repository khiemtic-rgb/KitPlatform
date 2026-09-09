export const CHARACTER_REFERENCE_AUTO_GENERATION_V2_ID = 'FAMIXA_CHARACTER_REFERENCE_AUTO_GENERATION_V2';

export const SET_PROVIDERS = [
  { id: '', label: 'Chưa chọn', available: false },
  { id: 'GEMINI', label: 'Gemini', available: true },
  { id: 'RUNWAY', label: 'Runway', available: false },
  { id: 'VEO', label: 'Veo', available: false },
] as const;

export const STAFF_CREATE_SET = 'Tạo bộ ảnh tham chiếu';
export const STAFF_START = 'Bắt đầu tạo';
export const STAFF_CANCEL = 'Hủy';
export const STAFF_PENDING = 'Đang chờ duyệt';
export const STAFF_APPROVE = 'Duyệt bộ ảnh';
export const STAFF_REJECT = 'Không đạt';
export const STAFF_DETAILS = 'Chi tiết kỹ thuật';
export const STAFF_BLOCKED = 'Cần nhân vật và nhận diện đã khóa. Hệ thống không tự sửa.';

export function providerLabel(id?: string | null) {
  return SET_PROVIDERS.find((x) => x.id === (id || ''))?.label || 'Chưa chọn';
}

export function providerAvailable(id?: string | null) {
  return SET_PROVIDERS.some((x) => x.id === id && x.available);
}

export function canOfferSet(input: { authorityReady: boolean; provider: string; locked: boolean; alreadyComplete: boolean }) {
  return input.authorityReady && !input.locked && !input.alreadyComplete && input.provider === 'GEMINI';
}

export function setConfirmCopy(input: { name: string; characterId: string; eraId: string; provider: string }) {
  return {
    title: STAFF_CREATE_SET,
    character: `Nhân vật: ${input.name}`,
    id: `Character ID: ${input.characterId}`,
    era: `Era: ${input.eraId}`,
    body: [
      'Famixa sẽ tạo:',
      '• Trước mặt',
      '• 3/4',
      '• Nghiêng',
      '• Toàn thân',
      '',
      'Nguồn identity:',
      'Master + DNA + PRP đã khóa',
      '',
      `Nhà cung cấp: ${input.provider}`,
      '',
      'Sau khi tạo:',
      'Bộ ảnh sẽ ở trạng thái Chờ duyệt.',
      'Famixa không tự duyệt và không tự khóa.',
    ].join('\n'),
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}

export function coverageLine(ready: number, total = 4) {
  return `${ready}/${total}`;
}

export function pendingSetLabel(ready: number, total = 4) {
  return `Bộ ảnh chuẩn: ${ready}/${total} · ${STAFF_PENDING}`;
}
