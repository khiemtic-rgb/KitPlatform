/** Quy trình kiểm kê chuẩn — 4 bước nghiệp vụ. */

export const INVENTORY_COUNT_WORKFLOW_STEPS = [
  {
    title: 'Chuẩn bị',
    description: 'Mở phiên kiểm kê, ghi lý do. Hạn chế bán/nhập xuất tại kho trong lúc đếm.',
  },
  {
    title: 'Ghi nhận đếm',
    description: 'Quét barcode hoặc chọn SP + lô, nhập số lượng thực tế. Ghi nhận từng đợt.',
  },
  {
    title: 'Đối chiếu lệch',
    description: 'Xem HT · Đếm · Lệch. Ưu tiên sửa GRN/POS/chuyển kho nếu sai chứng từ.',
  },
  {
    title: 'Duyệt & cập nhật tồn',
    description: 'Người duyệt xác nhận — hệ thống điều chỉnh tồn theo lô và khóa phiên.',
  },
] as const;

export const COUNT_REASON_PRESETS = [
  { value: 'periodic', label: 'Kiểm kê định kỳ (tháng/quý)' },
  { value: 'ad_hoc', label: 'Kiểm kê đột xuất' },
  { value: 'incident', label: 'Sau sự cố (mất mát, hư hỏng…)' },
  { value: 'audit', label: 'Phục vụ kiểm toán / thanh tra' },
] as const;

export function buildCountReason(preset: string, note?: string): string {
  const labels: Record<string, string> = {
    periodic: 'Kiểm kê định kỳ',
    ad_hoc: 'Kiểm kê đột xuất',
    incident: 'Kiểm kê sau sự cố',
    audit: 'Kiểm kê phục vụ kiểm toán',
  };
  const base = labels[preset] ?? preset;
  const trimmed = note?.trim();
  return trimmed ? `${base} — ${trimmed}` : base;
}

export const APPROVE_COUNT_CHECKLIST = [
  'Đã đối chiếu lệch lớn với chứng từ (GRN, đơn bán, chuyển kho) — sửa nghiệp vụ gốc nếu cần',
  'Lệch còn lại phản ánh đúng thực tế sau khi đếm (theo từng lô)',
  'Người duyệt khác người đếm (nếu quy mô cho phép) và đã ghi lý do trên phiên',
] as const;

export function resolveCountWorkflowStep(input: {
  status: number;
  entryCount: number;
  canApprove: boolean;
}): number {
  if (input.status === 3) return 4;
  if (input.status !== 2) return 0;
  if (input.canApprove) return 3;
  if (input.entryCount > 0) return 2;
  return 1;
}

export function countVarianceSummary(
  lines: Array<{ differenceQuantity: number }>,
): { totalLines: number; varianceLines: number; surplus: number; shortage: number } {
  let varianceLines = 0;
  let surplus = 0;
  let shortage = 0;
  for (const line of lines) {
    if (line.differenceQuantity === 0) continue;
    varianceLines += 1;
    if (line.differenceQuantity > 0) surplus += line.differenceQuantity;
    else shortage += Math.abs(line.differenceQuantity);
  }
  return { totalLines: lines.length, varianceLines, surplus, shortage };
}
