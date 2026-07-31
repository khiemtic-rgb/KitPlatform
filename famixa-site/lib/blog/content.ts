import fs from 'node:fs';
import path from 'node:path';
import matter from 'gray-matter';
import type { BlogCategoryId } from '@/lib/blog/categories';
import { isBlogPublished } from '@/lib/blog/published';

const BLOG_DIRECTORY = path.join(process.cwd(), 'content', 'blog');
const HUB_SLUG = '/vi/goi-cha-me';
const ARTICLE_FILENAME = /^[a-z0-9]+(?:-[a-z0-9]+)*\.md$/;

export type BlogFrontmatter = {
  title: string;
  description?: string;
  category: BlogCategoryId;
  image: string;
  pubDate: string;
  lang: 'vi';
  draft?: boolean;
};

export type BlogDocument = BlogFrontmatter & {
  body: string;
  routeSlug: string;
  slug: string;
  excerpt: string;
};

function readBlogFile(filename: string): BlogDocument {
  const routeSlug = filename.replace(/\.md$/, '');
  const source = fs.readFileSync(path.join(BLOG_DIRECTORY, filename), 'utf8');
  const { data, content } = matter(source);
  const frontmatter = data as BlogFrontmatter;
  const plain = content.replace(/^# .+\n+/, '').replace(/\n## .+\n[\s\S]*$/, '').trim();
  const excerpt =
    frontmatter.description?.trim() ||
    plain
      .replace(/[#>*_\[\]()!`]/g, '')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 160);

  return {
    ...frontmatter,
    routeSlug,
    slug: `${HUB_SLUG}/${routeSlug}`,
    body: content.replace(/^# .+\n+/, '').trim(),
    excerpt,
  };
}

function getArticleFilenames() {
  if (!fs.existsSync(BLOG_DIRECTORY)) return [];
  return fs
    .readdirSync(BLOG_DIRECTORY)
    .filter((filename) => ARTICLE_FILENAME.test(filename))
    .sort();
}

export function getBlogArticles(options?: { includeDrafts?: boolean; includeScheduled?: boolean }) {
  const includeDrafts = options?.includeDrafts ?? false;
  const includeScheduled = options?.includeScheduled ?? false;

  return getArticleFilenames()
    .map(readBlogFile)
    .filter((article) => {
      if (!includeDrafts && article.draft) return false;
      if (!includeScheduled && !isBlogPublished(article.pubDate)) return false;
      return true;
    })
    .sort((a, b) => new Date(b.pubDate).valueOf() - new Date(a.pubDate).valueOf());
}

export function getBlogArticle(routeSlug: string, options?: { includeDrafts?: boolean; includeScheduled?: boolean }) {
  return getBlogArticles(options).find((article) => article.routeSlug === routeSlug);
}

export function getBlogRouteSlugs(options?: { includeDrafts?: boolean; includeScheduled?: boolean }) {
  return getBlogArticles(options).map((article) => article.routeSlug);
}

export function getBlogHubMeta() {
  return {
    title: 'Góc cha mẹ',
    description:
      'Bài viết ngắn về nuôi dạy, nhịp sinh hoạt và cách dùng Famixa — để chia sẻ mỗi ngày, không áp lực.',
    slug: `${HUB_SLUG}/`,
  };
}
