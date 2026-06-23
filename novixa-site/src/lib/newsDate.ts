import { filterPublishedPosts, isNewsPublished } from './publishedNews';

const vnDayFormatter = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
});

/** Hiển thị ngày đăng theo giờ VN, ví dụ: 1 thg 7, 2026 */
export function formatNewsDate(pubDate: Date): string {
  return vnDayFormatter.format(pubDate);
}

export function sortPublishedNews<T extends { data: { pubDate: Date; lang?: string } }>(
  posts: T[],
  lang = 'vi',
  now = new Date(),
): T[] {
  return filterPublishedPosts(posts, now)
    .filter((p) => p.data.lang === lang)
    .sort((a, b) => b.data.pubDate.valueOf() - a.data.pubDate.valueOf());
}

export { filterPublishedPosts, isNewsPublished };
