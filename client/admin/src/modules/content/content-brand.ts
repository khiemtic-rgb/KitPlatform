const PALETTE: Array<{ test: RegExp; color: string }> = [
  { test: /novixa/i, color: '#16a34a' },
  { test: /famixa/i, color: '#7c3aed' },
  { test: /kit/i, color: '#2563eb' },
  { test: /vandinh|van.?dinh|tra/i, color: '#d97706' },
  { test: /xuanhoa|xuan.?hoa/i, color: '#db2777' },
  { test: /thai.?nguyen|tn.?life|local/i, color: '#0d9488' },
];

const FALLBACK = ['#0f766e', '#b45309', '#4338ca', '#be123c', '#0369a1'];

export function brandColor(code: string): string {
  const hit = PALETTE.find((p) => p.test.test(code));
  if (hit) return hit.color;
  let hash = 0;
  for (const ch of code) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  return FALLBACK[Math.abs(hash) % FALLBACK.length];
}

export function channelLabel(channel?: string | null): string {
  if (!channel) return '';
  const c = channel.toLowerCase();
  if (c.includes('group')) return 'Nhóm';
  if (c.includes('facebook') || c === 'fb') return 'FB';
  if (c.includes('wordpress') || c === 'wp') return 'Web';
  if (c.includes('astro') || c.includes('git')) return 'Web';
  if (c.includes('tiktok')) return 'TikTok';
  if (c.includes('instagram') || c === 'ig') return 'IG';
  if (c.includes('linkedin')) return 'LI';
  if (c.includes('manual') || c.includes('export')) return 'Tay';
  return channel;
}

export function fitMark(verdict?: string | null): string {
  if (verdict === 'fit') return '✓';
  if (verdict === 'maybe') return '△';
  if (verdict === 'skip') return '✗';
  return '·';
}
