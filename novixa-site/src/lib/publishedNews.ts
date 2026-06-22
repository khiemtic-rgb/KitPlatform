/** Bài đã tới ngày đăng (theo giờ Việt Nam, so sánh theo ngày). */
export function isNewsPublished(pubDate: Date, now = new Date()): boolean {
  const day = (d: Date) =>
    d.toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
  return day(pubDate) <= day(now);
}

export function filterPublishedPosts<T extends { data: { pubDate: Date } }>(
  posts: T[],
  now = new Date(),
): T[] {
  return posts.filter((p) => isNewsPublished(p.data.pubDate, now));
}
