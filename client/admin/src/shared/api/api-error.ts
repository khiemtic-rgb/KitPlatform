import { isAxiosError } from 'axios';
import { apiOfflineMessage, apiServerErrorMessage } from '@/shared/api/api-network';

function isGenericHttpAuthTitle(text: string): boolean {
  return /^(Forbidden|Unauthorized|Access Denied)$/i.test(text.trim());
}

export function apiErrorMessage(error: unknown, fallback: string) {
  if (isAxiosError(error)) {
    if (error.code === 'ECONNABORTED' || /timeout/i.test(error.message)) {
      return 'Yêu cầu quá lâu. Bộ 4 ảnh có thể mất vài phút — bấm lại và đợi tới khi xong.';
    }
    if (!error.response) {
      return apiOfflineMessage();
    }
    const status = error.response.status;
    if (status >= 502) {
      return apiServerErrorMessage();
    }
    const detail = error.response.data;
    if (typeof detail === 'string' && detail.trim()) return detail;
    if (detail && typeof detail === 'object') {
      const errors = (detail as { errors?: Record<string, string[] | string> }).errors;
      if (errors && typeof errors === 'object') {
        const lines = Object.entries(errors).flatMap(([field, msgs]) => {
          const list = Array.isArray(msgs) ? msgs : [String(msgs)];
          return list.filter(Boolean).map((msg) => `${field}: ${msg}`);
        });
        if (lines.length > 0) {
          return lines.slice(0, 3).join(' · ');
        }
      }
      if ('message' in detail) {
        const msg = String((detail as { message?: string }).message ?? '');
        if (msg.trim() && !isGenericHttpAuthTitle(msg)) return msg;
      }
    }
    if (status === 401) {
      return 'Phiên đăng nhập hết hạn hoặc chưa đăng nhập. Hãy đăng nhập lại.';
    }
    if (status === 403) {
      return 'Bạn không có quyền thực hiện thao tác này. Liên hệ quản trị viên để được cấp quyền phù hợp.';
    }
    if (detail && typeof detail === 'object' && 'detail' in detail) {
      const devDetail = String((detail as { detail?: string }).detail ?? '');
      const firstLine = devDetail.split('\n').find((line) => line.trim())?.trim();
      if (firstLine) return `${fallback}: ${firstLine}`;
    }
    if (detail && typeof detail === 'object' && 'title' in detail) {
      const title = String((detail as { title?: string }).title ?? '');
      if (title.trim() && !isGenericHttpAuthTitle(title)) return title;
    }
    return `${fallback} (HTTP ${status})`;
  }
  if (error instanceof Error && error.message.trim()) {
    return error.message.trim().slice(0, 280);
  }
  const nested = (error as { response?: { data?: { message?: string } } })?.response?.data?.message;
  if (typeof nested === 'string' && nested.trim()) return nested.trim().slice(0, 280);
  return fallback;
}
