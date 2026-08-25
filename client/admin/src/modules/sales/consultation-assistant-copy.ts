/** Copy & helpers — AI Pharmacy Assistant (trợ lý tư vấn, không phải “chọn thuốc”) */

export const ASSISTANT_DISCLAIMER =
  'Đây là hỗ trợ sàng lọc và khai thác thông tin, không thay thế đánh giá của dược sĩ.';

export const PRELIMINARY_ASSESSMENT_TITLE = 'Nhận định sơ bộ';
export const WARNING_SIGNS_TITLE = 'Dấu hiệu cảnh báo';

export function preliminaryAssessmentAlertType(
  level: string,
): 'success' | 'info' | 'warning' | 'error' {
  switch (level) {
    case 'needs_evaluation':
      return 'error';
    case 'insufficient':
      return 'warning';
    default:
      return 'success';
  }
}

export function preliminaryAssessmentBadge(level: string): string {
  switch (level) {
    case 'needs_evaluation':
      return '🔴';
    case 'insufficient':
      return '🟡';
    default:
      return '🟢';
  }
}

export function hypothesisFitLabel(fitLevel: string, index: number): string {
  if (fitLevel === 'primary' || index === 0) return 'Khả năng 1';
  return 'Khả năng 2';
}

export function formatMissingInfoHints(hints: string[]): string {
  if (hints.length === 0) return '';
  return `Cần hỏi thêm: ${hints.join(', ')}.`;
}

export const SUPPORT_BASIS_ITEMS = [
  'Thông tin khách đã cung cấp',
  'Dữ liệu sản phẩm Novixa',
  'Tồn kho nhà thuốc',
  'Quy tắc tư vấn Novixa',
] as const;

export function consultationSafetyHeadline(level: string): string {
  switch (level) {
    case 'stop_sale':
    case 'refer_medical':
      return 'Cần chuyển khám';
    case 'refer_pharmacist':
      return 'Cần dược sĩ xem';
    case 'caution':
      return 'Cần lưu ý thêm';
    default:
      return 'Chưa phát hiện dấu hiệu cảnh báo';
  }
}

export function consultationSafetySubtext(level: string, hasFlags: boolean): string {
  switch (level) {
    case 'stop_sale':
    case 'refer_medical':
      return 'Có dấu hiệu cần được đánh giá y tế. Không tiếp tục gợi ý sản phẩm tự chăm sóc.';
    case 'refer_pharmacist':
      return 'Có yếu tố cần được dược sĩ đánh giá trước khi tiếp tục tư vấn.';
    case 'caution':
      return hasFlags
        ? 'Thông tin hiện có có điểm cần xác nhận thêm với khách hoặc dược sĩ.'
        : 'Thông tin hiện có chưa ghi nhận dấu hiệu cảnh báo từ phần đã khai thác.';
    default:
      return hasFlags
        ? 'Xem chi tiết cảnh báo bên dưới. Vẫn cần dược sĩ / nhân viên quyết định cuối.'
        : `Thông tin hiện có chưa ghi nhận dấu hiệu cảnh báo. ${ASSISTANT_DISCLAIMER}`;
  }
}

export function productSupportReason(stockAvailable: number, ruleReason: string): string {
  const stock =
    stockAvailable > 0 ? 'Đang có hàng' : 'Hết hàng tại kho đã chọn';
  const rule = ruleReason?.trim() || 'Thuộc nhóm phù hợp với thông tin đã khai thác';
  return `${stock} · ${rule}`;
}
