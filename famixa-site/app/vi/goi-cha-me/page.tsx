import type { Metadata } from 'next';
import { BlogHubPage } from '@/components/blog/BlogHubPage';
import { getBlogHubMeta } from '@/lib/blog/content';
import { absoluteUrl } from '@/lib/site';

export function generateMetadata(): Metadata {
  const hub = getBlogHubMeta();
  return {
    title: hub.title,
    description: hub.description,
    alternates: {
      canonical: absoluteUrl(hub.slug),
    },
  };
}

export default function Page() {
  return <BlogHubPage />;
}
