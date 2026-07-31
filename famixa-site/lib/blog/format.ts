const vnDayFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: 'numeric',
  month: 'long',
  year: 'numeric',
  timeZone: 'Asia/Ho_Chi_Minh',
});

export function formatBlogDate(pubDate: string | Date) {
  const date = typeof pubDate === 'string' ? new Date(pubDate) : pubDate;
  return vnDayFormatter.format(date);
}

export function getReadingMinutes(body: string) {
  return Math.max(1, Math.ceil(body.split(/\s+/).filter(Boolean).length / 180));
}
