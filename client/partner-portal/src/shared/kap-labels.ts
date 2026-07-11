/** Nhãn hiển thị KAP — Partner Portal. */
export const KAP_PIPELINE_LABELS: Record<string, string> = {
  new: 'Mới',
  contacted: 'Đã liên hệ',
  demo_scheduled: 'Đã hẹn demo',
  demo_done: 'Đã demo',
  won: 'Đã chốt',
  lost: 'Không thành công',
  nurturing: 'Nuôi dưỡng',
};

export const KAP_COMMISSION_LABELS: Record<string, string> = {
  none: 'Chưa có',
  pending: 'Chờ duyệt',
  approved: 'Đã duyệt',
  paid: 'Đã thanh toán',
  void: 'Huỷ / không tính',
};

export const KAP_SUBMISSION_STATUS_LABELS: Record<string, string> = {
  draft: 'Đang làm',
  completed: 'Hoàn thành',
  lead_captured: 'Đã gửi lead',
  report_ready: 'Có báo cáo',
};

export function kapLabel(map: Record<string, string>, value?: string | null, fallback = '—'): string {
  if (!value) return fallback;
  return map[value] ?? value;
}
