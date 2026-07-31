const VN_OFFSET_MS = 7 * 60 * 60 * 1000;

function dayKey(date: Date) {
  return Math.floor((date.getTime() + VN_OFFSET_MS) / 86_400_000);
}

/** Bài có pubDate sau hôm nay (giờ VN) sẽ ẩn trên web. */
export function isBlogPublished(pubDate: string | Date, now = new Date()): boolean {
  const published = typeof pubDate === 'string' ? new Date(pubDate) : pubDate;
  if (Number.isNaN(published.getTime())) return false;
  return dayKey(published) <= dayKey(now);
}
