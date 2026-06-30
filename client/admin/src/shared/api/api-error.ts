import { isAxiosError } from 'axios';
import { commonT } from '@/shared/i18n';

export function apiErrorMessage(error: unknown, fallback: string) {
  if (isAxiosError(error)) {
    if (!error.response) {
      return commonT()('errors.apiOffline');
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
        if (msg.trim()) return msg;
      }
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
