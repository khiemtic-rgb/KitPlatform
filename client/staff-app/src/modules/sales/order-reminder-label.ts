/** Default label for order-level refill reminder on staff POS (VN time UTC+7). */
export function defaultOrderReminderLabel(date = new Date()): string {
  const vn = new Date(date.getTime() + 7 * 60 * 60 * 1000);
  const day = String(vn.getUTCDate()).padStart(2, '0');
  const month = String(vn.getUTCMonth() + 1).padStart(2, '0');
  const year = vn.getUTCFullYear();
  return `Đơn thuốc ngày ${day}/${month}/${year}`;
}
