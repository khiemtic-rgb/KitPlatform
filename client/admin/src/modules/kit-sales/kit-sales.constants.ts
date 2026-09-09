export const KIT_SALES_STATUSES = [
  'discovered',
  'research',
  'contact',
  'engage',
  'phc',
  'qualify',
  'demo',
  'pilot',
  'paid',
  'lost',
] as const;

export const KIT_SALES_PIPELINE_STATUSES = KIT_SALES_STATUSES.filter((code) => code !== 'lost');

export const KIT_SALES_TEMPERATURES = ['cold', 'warm', 'hot'] as const;

export const KIT_SALES_ACTION_CODES = [
  'research',
  'call',
  'visit',
  'follow_up',
  'phc',
  'demo',
  'proposal',
  'close',
] as const;

export const KIT_SALES_INTERACTION_TYPES = [
  'note',
  'call',
  'visit',
  'message',
  'meeting',
] as const;

export const KIT_SALES_CHANNELS = ['phone', 'field', 'zalo', 'facebook', 'email', 'other'] as const;

export const KIT_SALES_FACEBOOK_KINDS = ['profile', 'page'] as const;

/** Khu vực chọn khi nhập / lọc lead Facebook. Thái Nguyên đứng đầu (pilot hiện tại). */
export const KIT_SALES_PROVINCES = [
  'Thái Nguyên',
  'Hà Nội',
  'Bắc Giang',
  'Bắc Ninh',
  'Bắc Kạn',
  'Phú Thọ',
  'Vĩnh Phúc',
  'Lạng Sơn',
  'Cao Bằng',
  'Tuyên Quang',
  'Hà Giang',
  'Quảng Ninh',
  'Hải Phòng',
  'Hải Dương',
  'Hưng Yên',
  'Hòa Bình',
  'Sơn La',
  'Yên Bái',
  'Lào Cai',
  'Điện Biên',
  'Lai Châu',
  'Nam Định',
  'Ninh Bình',
  'Thái Bình',
  'Hà Nam',
  'Thanh Hóa',
  'Nghệ An',
  'Hà Tĩnh',
  'Quảng Bình',
  'Quảng Trị',
  'Thừa Thiên Huế',
  'Đà Nẵng',
  'Quảng Nam',
  'Quảng Ngãi',
  'Bình Định',
  'Phú Yên',
  'Khánh Hòa',
  'Ninh Thuận',
  'Bình Thuận',
  'Kon Tum',
  'Gia Lai',
  'Đắk Lắk',
  'Đắk Nông',
  'Lâm Đồng',
  'TP. Hồ Chí Minh',
  'Bình Dương',
  'Đồng Nai',
  'Bà Rịa - Vũng Tàu',
  'Tây Ninh',
  'Bình Phước',
  'Long An',
  'Tiền Giang',
  'Bến Tre',
  'Trà Vinh',
  'Vĩnh Long',
  'Đồng Tháp',
  'An Giang',
  'Kiên Giang',
  'Cần Thơ',
  'Hậu Giang',
  'Sóc Trăng',
  'Bạc Liêu',
  'Cà Mau',
] as const;

export const KIT_SALES_JOURNEY_STEP_KEYS: Record<string, string> = {
  hook: 'journey.stepHook',
  phc_ask: 'journey.stepPhc',
  report_ask: 'journey.stepReport',
  pilot_ask: 'journey.stepPilot',
};

export function kitSalesActionI18nKey(code: string): string {
  const step = code.split(':')[0];
  return KIT_SALES_JOURNEY_STEP_KEYS[step] ?? `action.${code}`;
}

export type KitSalesStatus = (typeof KIT_SALES_STATUSES)[number];

export function nextPipelineStatus(current: string): string | null {
  const i = KIT_SALES_PIPELINE_STATUSES.indexOf(current as (typeof KIT_SALES_PIPELINE_STATUSES)[number]);
  if (i < 0 || i >= KIT_SALES_PIPELINE_STATUSES.length - 1) return null;
  return KIT_SALES_PIPELINE_STATUSES[i + 1];
}

export function prevPipelineStatus(current: string): string | null {
  const i = KIT_SALES_PIPELINE_STATUSES.indexOf(current as (typeof KIT_SALES_PIPELINE_STATUSES)[number]);
  if (i <= 0) return null;
  return KIT_SALES_PIPELINE_STATUSES[i - 1];
}
