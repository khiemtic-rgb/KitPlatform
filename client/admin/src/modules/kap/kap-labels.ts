/** Nhãn hiển thị KAP — dùng chung Admin (Leads / Partners). */
export const KAP_PIPELINE_LABELS: Record<string, string> = {
  new: 'Mới',
  contacted: 'Đã liên hệ',
  demo_scheduled: 'Đã hẹn tư vấn',
  demo_done: 'Đã tư vấn',
  won: 'Đã chốt',
  lost: 'Không thành công',
  nurturing: 'Đang chăm sóc',
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
  lead_captured: 'Đã gửi thông tin',
  report_ready: 'Có báo cáo',
};

export const KAP_PARTNER_STATUS_LABELS: Record<string, string> = {
  active: 'Hoạt động',
  suspended: 'Tạm khoá',
  archived: 'Lưu trữ',
};

export const KAP_PARTNER_TYPE_LABELS: Record<string, string> = {
  ctv: 'Cộng tác viên',
  consultant: 'Chuyên viên đánh giá',
  tdv: 'Trình dược viên',
  agency: 'Đại lý',
};

export function kapLabel(map: Record<string, string>, value?: string | null, fallback = '—'): string {
  if (!value) return fallback;
  return map[value] ?? value;
}

export const KAP_PIPELINE_OPTIONS = Object.entries(KAP_PIPELINE_LABELS).map(([value, label]) => ({
  value,
  label,
}));

export const KAP_COMMISSION_OPTIONS = Object.entries(KAP_COMMISSION_LABELS).map(([value, label]) => ({
  value,
  label,
}));
