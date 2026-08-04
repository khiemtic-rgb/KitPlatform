import type { APIRoute } from 'astro';
import { getCollection } from 'astro:content';
import { articleOgImageUrl } from '../lib/articleSeo';
import { sortPublishedNews } from '../lib/newsDate';

export const prerender = true;

function escapeXml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;');
}

export const GET: APIRoute = async () => {
  const posts = sortPublishedNews(await getCollection('tinTuc')).slice(0, 20);
  const items = posts
    .map((post) => {
      const link = `https://novixa.vn/vi/tin-tuc/${post.id}/`;
      const image = articleOgImageUrl(post.id, post.data.image);
      return `
    <item>
      <title>${escapeXml(post.data.title)}</title>
      <link>${link}</link>
      <guid isPermaLink="true">${link}</guid>
      <pubDate>${post.data.pubDate.toUTCString()}</pubDate>
      <description>${escapeXml(post.data.description || '')}</description>
      <enclosure url="${escapeXml(image)}" type="image/png" />
    </item>`;
    })
    .join('');

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Novixa — Kiến thức nhà thuốc</title>
    <link>https://novixa.vn/vi/tin-tuc/</link>
    <description>Bài viết mới nhất từ Novixa</description>
    <language>vi</language>${items}
  </channel>
</rss>`;

  return new Response(xml, {
    headers: {
      'Content-Type': 'application/rss+xml; charset=utf-8',
      'Access-Control-Allow-Origin': '*',
      'Cache-Control': 'public, max-age=300',
    },
  });
};
