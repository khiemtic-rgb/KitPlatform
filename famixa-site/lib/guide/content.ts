import fs from 'node:fs';
import path from 'node:path';
import matter from 'gray-matter';

const GUIDE_DIRECTORY = path.join(process.cwd(), 'content', 'guide');
const SEO_SECTION_PATTERN = /\n## (?:Từ khóa SEO|Meta description|URL)\n[\s\S]*$/;

export type GuideStatus = 'draft' | 'verified';

export type GuideFrontmatter = {
  title: string;
  slug: string;
  status: GuideStatus;
  /** false = ẩn khỏi hub / next-step; trang vẫn truy cập trực tiếp nếu cần */
  hub?: boolean;
  persona: 'parent';
  plan_note: string;
  last_verified: string;
};

export type GuideDocument = GuideFrontmatter & {
  body: string;
  metaDescription?: string;
  keywords: string[];
  routeSlug?: string;
};

function getSection(source: string, heading: string) {
  const match = source.match(new RegExp(`\\n## ${heading}\\n([\\s\\S]*?)(?=\\n## |$)`));
  return match?.[1].trim();
}

function readGuideFile(filename: string): GuideDocument {
  const source = fs.readFileSync(path.join(GUIDE_DIRECTORY, filename), 'utf8');
  const { data, content } = matter(source);
  const frontmatter = data as GuideFrontmatter;
  const metaDescription = getSection(content, 'Meta description');
  const keywords = (getSection(content, 'Từ khóa SEO') ?? '')
    .split(',')
    .map((keyword) => keyword.trim())
    .filter(Boolean);
  const routeSlug = frontmatter.slug === '/vi/huong-dan' ? undefined : frontmatter.slug.split('/').filter(Boolean).at(-1);

  return {
    ...frontmatter,
    routeSlug,
    body: content
      .replace(/^# .+\n+/, '')
      .replace(SEO_SECTION_PATTERN, '')
      .trim(),
    metaDescription,
    keywords,
  };
}

function getArticleFilenames() {
  return fs
    .readdirSync(GUIDE_DIRECTORY)
    .filter((filename) => /^0[1-9]-.*\.md$/.test(filename))
    .sort();
}

export function getGuideHub() {
  return readGuideFile('README.md');
}

export function getGuideArticles() {
  return getArticleFilenames().map(readGuideFile);
}

export function getGuideHubArticles() {
  return getGuideArticles().filter((article) => article.hub !== false);
}

export function getGuideArticle(routeSlug: string) {
  return getGuideArticles().find((article) => article.routeSlug === routeSlug);
}

export function getGuideRouteSlugs() {
  return getGuideArticles().flatMap((article) => (article.routeSlug ? [article.routeSlug] : []));
}
