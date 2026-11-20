import { isAxiosError } from 'axios';
import { apiOfflineMessage, apiServerErrorMessage } from '@/shared/api/api-network';

function isGenericHttpAuthTitle(text: string): boolean {
  return /^(Forbidden|Unauthorized|Access Denied)$/i.test(text.trim());
}

export function isUnauthorizedError(error: unknown) {
  return isAxiosError(error) && error.response?.status === 401;
}

export function isGenericAxiosStatusMessage(text?: string) {
  const raw = String(text || '').trim();
  if (!raw) return false;
  if (/^Request failed with status code \d+$/i.test(raw)) return true;
  return /^Requestfailedwithstatuscode\d+$/i.test(raw.replace(/\s+/g, ''));
}

export function deskToastContent(content: unknown, fallback = 'Không tải được. Ở lại bàn này — không gửi Fal / Veo.') {
  const text =
    typeof content === 'string'
      ? content
      : content && typeof content === 'object' && 'content' in content
        ? String((content as { content?: unknown }).content ?? '')
        : '';
  if (isGenericAxiosStatusMessage(text)) return fallback;
  return text.trim() || fallback;
}

export function apiErrorMessage(error: unknown, fallback = '') {
  if (isAxiosError(error)) {
    if (isGenericAxiosStatusMessage(error.message || '')) {
      const status = error.response?.status;
      return fallback?.trim()
        ? `${fallback.trim()}${status ? ` (HTTP ${status})` : ''}`
        : status
          ? `HTTP ${status}`
          : fallback;
    }
    if (error.code === 'ECONNABORTED' || /timeout/i.test(error.message)) {
      return fallback?.trim()
        ? `${fallback.trim()} Hỏi lại · 0$ — đừng Confirm job mới.`
        : 'Yêu cầu quá lâu. Bấm lại và đợi tới khi xong.';
    }
    if (!error.response) {
      return apiOfflineMessage();
    }
    const status = error.response.status;
    if (status >= 502) {
      return apiServerErrorMessage();
    }
    const detail = error.response.data;
    if (typeof Blob !== 'undefined' && detail instanceof Blob) {
      if (error.message && !isGenericAxiosStatusMessage(error.message)) {
        return error.message.trim().slice(0, 280);
      }
      return `${fallback} (HTTP ${status})`;
    }
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
        if (/NO_PROVIDER/i.test(msg)) {
          return 'Chưa có Gemini API key. Tạo hình dùng Gemini — file Join Veo không thay thế được.';
        }
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
    const raw = error.message.trim();
    if (isGenericAxiosStatusMessage(raw)) return fallback;
    return raw.slice(0, 280);
  }
  const nested = (error as { response?: { data?: { message?: string } } })?.response?.data?.message;
  if (typeof nested === 'string' && nested.trim()) return nested.trim().slice(0, 280);
  return fallback;
}
