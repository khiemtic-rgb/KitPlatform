export const CHARACTER_AUTHORITY_INITIALIZATION_V1_ID = 'FAMIXA_CHARACTER_AUTHORITY_INITIALIZATION_V1';

export const AUTHORITY_PROVIDERS = [
  { id: '', label: 'Chưa chọn', available: false },
  { id: 'GEMINI', label: 'Gemini', available: true },
  { id: 'RUNWAY', label: 'Runway', available: false },
  { id: 'VEO', label: 'Veo', available: false },
] as const;

export const STAFF_NEED_MASTER = 'Cần tạo Master Reference';
export const STAFF_MASTER_REVIEW = 'Master đang chờ duyệt';
export const STAFF_MASTER_LOCKED = 'Master đã khóa';
export const STAFF_NEED_DNA = 'Cần tạo DNA';
export const STAFF_DNA_REVIEW = 'DNA đang chờ duyệt';
export const STAFF_NEED_PRP = 'Cần tạo hồ sơ sản xuất';
export const STAFF_READY = 'Nhân vật đã sẵn sàng tạo bộ ảnh tham chiếu';
export const STAFF_CREATE_MASTER = 'Tạo Master';
export const STAFF_CREATE_DNA = 'Tạo DNA';
export const STAFF_CREATE_PRP = 'Tạo hồ sơ sản xuất';
export const STAFF_APPROVE_MASTER = 'Duyệt Master';
export const STAFF_APPROVE_DNA = 'Duyệt DNA';
export const STAFF_APPROVE_PRP = 'Duyệt hồ sơ sản xuất';
export const STAFF_REJECT = 'Không đạt';
export const STAFF_LOCK_MASTER = 'Khóa Master';
export const STAFF_LOCK_DNA = 'Khóa DNA';
export const STAFF_LOCK_PRP = 'Khóa hồ sơ sản xuất';
export const STAFF_START = 'Bắt đầu tạo';
export const STAFF_CANCEL = 'Hủy';
export const STAFF_DETAILS = 'Chi tiết kỹ thuật';
export const STAFF_MISSING = 'Chưa có';
export const STAFF_WAIT_MASTER = 'Chờ Master';
export const STAFF_WAIT_DNA = 'Chờ DNA';
export const STAFF_WAIT_AUTHORITY = 'Chờ Authority';

export function providerLabel(id?: string | null) {
  return AUTHORITY_PROVIDERS.find((x) => x.id === (id || ''))?.label || 'Chưa chọn';
}

export function providerAvailable(id?: string | null) {
  return AUTHORITY_PROVIDERS.some((x) => x.id === id && x.available);
}

export function canOfferMaster(input: {
  authorityLocked: boolean;
  masterStatus: string;
  provider: string;
}) {
  return (
    !input.authorityLocked
    && (input.masterStatus === 'MISSING' || input.masterStatus === 'REJECTED' || input.masterStatus === 'FAILED')
    && input.provider === 'GEMINI'
  );
}

export function masterConfirmCopy(input: { name: string; eraId: string; provider: string }) {
  return {
    title: STAFF_CREATE_MASTER,
    body: [
      `Bạn sắp tạo Master Reference cho ${input.name}.`,
      '',
      `Character: ${input.name}`,
      `Era: ${input.eraId}`,
      `Provider: ${input.provider}`,
      'Mục đích: xây dựng Character Authority',
    ].join('\n'),
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}

export function dnaConfirmCopy(input: { name: string; eraId: string }) {
  return {
    title: STAFF_CREATE_DNA,
    body: [
      `Bạn sắp tạo Character DNA cho ${input.name}.`,
      '',
      `Character: ${input.name}`,
      `Era: ${input.eraId}`,
      'DNA được suy ra từ Character Identity và Master đã khóa.',
      'Famixa không tự duyệt và không tự khóa.',
    ].join('\n'),
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}

export function prpConfirmCopy(input: { name: string; eraId: string }) {
  return {
    title: STAFF_CREATE_PRP,
    body: [
      `Bạn sắp tạo hồ sơ sản xuất cho ${input.name}.`,
      '',
      `Character: ${input.name}`,
      `Era: ${input.eraId}`,
      'Hồ sơ mô tả cách dùng Master và DNA đã khóa.',
      'Famixa không tự duyệt và không tự khóa.',
    ].join('\n'),
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}

export function stageDot(status?: string) {
  if (status === 'LOCKED') return '✓';
  if (status === 'READY_FOR_DIRECTOR' || status === 'APPROVED') return '●';
  return '○';
}
