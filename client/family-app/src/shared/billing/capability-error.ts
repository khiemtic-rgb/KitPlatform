import { isAxiosError } from 'axios';

/** Extract API validation message (Family OS capability / packaging). */
export function getApiErrorMessage(err: unknown): string {
  if (isAxiosError(err)) {
    const data = err.response?.data as { message?: string; Message?: string } | undefined;
    const msg = data?.message ?? data?.Message;
    if (msg?.trim()) return msg.trim();
    if (err.message) return err.message;
  }
  if (err instanceof Error && err.message) return err.message;
  return 'Có lỗi xảy ra.';
}

/** True when server blocked a capability — open PaywallSheet instead of generic toast. */
export function isCapabilityPaywallError(err: unknown): boolean {
  const msg = getApiErrorMessage(err).toLowerCase();
  return (
    msg.includes('chưa gồm tính năng') ||
    msg.includes('family peace') ||
    msg.includes('nâng gói') ||
    msg.includes('nâng family') ||
    msg.includes('gói free') ||
    (msg.includes('tối đa') && msg.includes('trẻ')) ||
    msg.includes('gói trả phí') ||
    msg.includes('trial đã hết')
  );
}
