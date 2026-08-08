/** Build a mailto URL; optional BCC is never shown in page copy. */
export function buildMailto(
  to: string,
  options?: {
    bcc?: string;
    subject?: string;
    body?: string;
  },
): string {
  const params = new URLSearchParams();
  if (options?.bcc) params.set('bcc', options.bcc);
  if (options?.subject) params.set('subject', options.subject);
  if (options?.body) params.set('body', options.body);
  const query = params.toString();
  return `mailto:${to}${query ? `?${query}` : ''}`;
}
