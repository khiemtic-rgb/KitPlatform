import dayjs from 'dayjs';

/** ISO / API date → dd-mm-yyyy cho danh sách & báo cáo */
export function formatDisplayDate(iso?: string | null): string {
  if (!iso) return '—';
  const normalized = iso.length >= 10 ? iso.slice(0, 10) : iso;
  const parsed = dayjs(normalized, 'YYYY-MM-DD', true);
  return parsed.isValid() ? parsed.format('DD-MM-YYYY') : normalized;
}
