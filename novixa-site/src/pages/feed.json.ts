import type { APIRoute } from 'astro';
import { getCollection } from 'astro:content';
import { articleOgImageUrl } from '../lib/articleSeo';
import { sortPublishedNews } from '../lib/newsDate';

export const prerender = true;

export const GET: APIRoute = async () => {
  const posts = sortPublishedNews(await getCollection('tinTuc')).slice(0, 12);

  const items = posts.map((post) => ({
    title: post.data.title,
    description: post.data.description || '',
    href: `https://novixa.vn/vi/tin-tuc/${post.id}/`,
    image: articleOgImageUrl(post.id, post.data.image),
    pubDate: post.data.pubDate.toISOString(),
    source: 'novixa' as const,
    badge: 'Novixa',
  }));

  return new Response(
    JSON.stringify({
      source: 'novixa',
      generatedAt: new Date().toISOString(),
      items,
    }),
    {
      headers: {
        'Content-Type': 'application/json; charset=utf-8',
        'Access-Control-Allow-Origin': '*',
        'Cache-Control': 'public, max-age=300',
      },
    },
  );
};
