import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { BlogArticlePage } from '@/components/blog/BlogArticlePage';
import { getBlogArticle, getBlogArticles, getBlogRouteSlugs } from '@/lib/blog/content';
import { getLandingContent } from '@/lib/cms/getLanding';
import { absoluteUrl } from '@/lib/site';

type Props = {
  params: Promise<{ slug: string }>;
};

export function generateStaticParams() {
  return getBlogRouteSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const article = getBlogArticle(slug);

  if (!article) return {};

  return {
    title: article.title,
    description: article.description ?? article.excerpt,
    openGraph: {
      title: article.title,
      description: article.description ?? article.excerpt,
      images: article.image ? [absoluteUrl(article.image)] : undefined,
      type: 'article',
      publishedTime: article.pubDate,
    },
    alternates: {
      canonical: absoluteUrl(`${article.slug}/`),
    },
  };
}

export default async function Page({ params }: Props) {
  const { slug } = await params;
  const article = getBlogArticle(slug);

  if (!article) notFound();

  const landing = await getLandingContent('vi');
  const related = getBlogArticles()
    .filter((item) => item.routeSlug !== article.routeSlug && item.category === article.category)
    .slice(0, 2);

  return <BlogArticlePage article={article} appUrl={landing.appUrl} related={related} />;
}
