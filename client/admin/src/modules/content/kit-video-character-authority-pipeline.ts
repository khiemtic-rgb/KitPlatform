export const CHARACTER_AUTHORITY_PIPELINE_V1_ID = 'FAMIXA_CHARACTER_AUTHORITY_PIPELINE_V1';

export const PIPELINE_STATES = [
  'PROFILE_CREATED',
  'MASTER_GENERATING',
  'MASTER_PENDING_REVIEW',
  'MASTER_APPROVED',
  'MASTER_LOCKED',
  'DNA_READY',
  'PRP_READY',
  'CRP_GENERATING',
  'CRP_PENDING_REVIEW',
  'CRP_APPROVED',
  'CRP_LOCKED',
  'CHARACTER_READY',
] as const;

export const CRP_SET_TYPES = ['FRONT', 'THREE_QUARTER', 'SIDE', 'FULL_BODY'] as const;

export const STAFF_CREATE_AUTHORITY = 'Tạo bộ nhân vật';
export const STAFF_APPROVE_MASTER = 'Duyệt Master';
export const STAFF_APPROVE_CRP = 'Duyệt bộ ảnh';
export const STAFF_REJECT = 'Không đạt';
export const STAFF_SYSTEM = 'Xem thông tin hệ thống';
export const STAFF_READY = 'Nhân vật đã sẵn sàng';
export const STAFF_MASTER_REVIEW = 'Chờ duyệt nhân vật';
export const STAFF_BUILDING = 'Đang tạo bộ nhân vật';
export const STAFF_CRP_BUILDING = 'Bộ ảnh chuẩn đang được tạo';
export const STAFF_CRP_REVIEW = 'Cần duyệt bộ ảnh chuẩn';
export const STAFF_CRP_CREATE = 'Tạo bộ ảnh tham chiếu';
export const STAFF_LOCK_MASTER = 'Khóa Master';
export const STAFF_START = 'Bắt đầu tạo';
export const STAFF_CANCEL = 'Hủy';
export const STAFF_DNA_AUTO = 'DNA được tạo tự động sau khi khóa Master';
export const STAFF_PRP_AUTO = 'Hồ sơ sản xuất được tạo tự động sau DNA';

export function pipelineStaff(state?: string | null) {
  switch (state) {
    case 'CHARACTER_READY':
      return `✓ ${STAFF_READY}`;
    case 'CRP_REJECTED':
      return 'Không đạt';
    case 'CRP_PENDING_REVIEW':
    case 'CRP_APPROVED':
      return STAFF_CRP_REVIEW;
    case 'CRP_GENERATING':
      return STAFF_CRP_BUILDING;
    case 'MASTER_PENDING_REVIEW':
    case 'MASTER_APPROVED':
      return STAFF_MASTER_REVIEW;
    case 'PRP_READY':
    case 'DNA_READY':
    case 'MASTER_LOCKED':
      return 'Sẵn sàng tạo bộ ảnh tham chiếu';
    default:
      return STAFF_BUILDING;
  }
}

export function hideDnaPrpCtas() {
  return true;
}

export function mayShowMasterGenerate(input: {
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

export function authorityConfirmCopy(input: { name: string; eraId: string; provider: string }) {
  return {
    title: STAFF_CREATE_AUTHORITY,
    body: [
      `Bạn sắp khởi tạo bộ nhân vật cho ${input.name}.`,
      '',
      `Nhân vật: ${input.name}`,
      `Era: ${input.eraId}`,
      `Nhà cung cấp: ${input.provider}`,
      'Famixa tạo Master theo phong cách chung, rồi chờ bạn duyệt.',
      'Không tự duyệt. Không tự khóa.',
    ].join('\n'),
    cancel: STAFF_CANCEL,
    start: STAFF_START,
  };
}
