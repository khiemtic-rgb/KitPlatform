import { notFound } from 'next/navigation';
import type { Metadata } from 'next';
import { GuideArticlePage } from '@/components/guide/GuideArticlePage';
import { getGuideArticle, getGuideHubArticles, getGuideRouteSlugs } from '@/lib/guide/content';
import { absoluteUrl } from '@/lib/site';

type Props = {
  params: Promise<{ slug: string }>;
};

export function generateStaticParams() {
  return getGuideRouteSlugs().map((slug) => ({ slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const article = getGuideArticle(slug);

  if (!article) return {};

  return {
    title: article.title,
    description: article.metaDescription,
    keywords: article.keywords,
    alternates: {
      canonical: absoluteUrl(article.slug),
    },
  };
}

export default async function Page({ params }: Props) {
  const { slug } = await params;
  const article = getGuideArticle(slug);

  if (!article) notFound();

  const articles = getGuideHubArticles();
  const index = articles.findIndex((item) => item.routeSlug === article.routeSlug);

  return <GuideArticlePage article={article} nextArticle={articles[index + 1]} />;
}
