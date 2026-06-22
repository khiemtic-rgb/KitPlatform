import { isAxiosError } from 'axios';

export function apiErrorMessage(error: unknown, fallback: string) {
  if (isAxiosError(error)) {
    if (!error.response) {
      return 'Không kết nối được API. Kiểm tra PharmaCore.Api đang chạy (port 5290).';
    }
    const detail = error.response.data;
    if (typeof detail === 'string' && detail.trim()) return detail;
    if (detail && typeof detail === 'object' && 'message' in detail) {
      const msg = String((detail as { message?: string }).message ?? '');
      if (msg.trim()) return msg;
    }
    if (detail && typeof detail === 'object' && 'detail' in detail) {
      const devDetail = String((detail as { detail?: string }).detail ?? '');
      const firstLine = devDetail.split('\n').find((line) => line.trim())?.trim();
      if (firstLine) return `${fallback}: ${firstLine}`;
    }
    if (detail && typeof detail === 'object' && 'title' in detail) {
      return String((detail as { title?: string }).title ?? fallback);
    }
    return `${fallback} (HTTP ${error.response.status})`;
  }
  return fallback;
}
