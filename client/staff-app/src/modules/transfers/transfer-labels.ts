export const TRANSFER_STATUS_LABEL: Record<number, string> = {
  1: 'Chờ gửi',
  2: 'Đang chuyển',
  3: 'Hoàn tất',
  4: 'Đã hủy',
};

export function transferStatusLabel(status: number, hasShortage = false): string {
  if (hasShortage && status === 3) return 'Hoàn tất · có nhận thiếu';
  return TRANSFER_STATUS_LABEL[status] ?? `Trạng thái ${status}`;
}

export function transferStatusColor(status: number, hasShortage = false): string {
  if (hasShortage && status === 3) return 'orange';
  if (status === 3) return 'green';
  if (status === 4) return 'default';
  if (status === 2) return 'processing';
  return 'gold';
}

export function canShipTransfer(status: number): boolean {
  return status === 1;
}

export function canReceiveTransfer(status: number): boolean {
  return status === 2;
}

export function canCompleteTransfer(status: number): boolean {
  return status === 1;
}

export function canCancelTransfer(status: number): boolean {
  return status === 1 || status === 2;
}
