import type { APIRoute } from 'astro';
import { hrefFor, listListings, OFFERS_PUBLIC } from '../lib/api';
import { GUIDES, PILLARS, PLACES, guideHref, placeHref, pillarHref } from '../lib/discover';

export const prerender = false;

const FALLBACK = 'https://thainguyenlife.vn';

type Row = { path: string; changefreq: string; priority: string; lastmod?: string };

function day(raw?: string | null): string | undefined {
  if (!raw) return undefined;
  const t = new Date(raw).getTime();
  if (Number.isNaN(t)) return undefined;
  return new Date(t).toISOString().slice(0, 10);
}

function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function abs(origin: string, path: string): string {
  return new URL(path, `${origin}/`).href;
}

export const GET: APIRoute = async ({ site }) => {
  const origin = (site ?? new URL(FALLBACK)).origin.replace(/\/$/, '');
  const today = new Date().toISOString().slice(0, 10);
  const rows: Row[] = [
    { path: '/', changefreq: 'daily', priority: '1.0', lastmod: today },
    { path: '/kham-pha', changefreq: 'weekly', priority: '0.9', lastmod: today },
    { path: '/kham-pha/cam-nang', changefreq: 'daily', priority: '0.85', lastmod: today },
    { path: '/kham-pha/cam-nang/tuan-nay', changefreq: 'daily', priority: '0.8', lastmod: today },
    { path: '/viec', changefreq: 'daily', priority: '0.8', lastmod: today },
    { path: '/su-kien', changefreq: 'daily', priority: '0.8', lastmod: today },
    { path: '/tin', changefreq: 'daily', priority: '0.8', lastmod: today },
    { path: '/tro', changefreq: 'daily', priority: '0.8', lastmod: today },
    { path: '/dang-tin', changefreq: 'monthly', priority: '0.4' },
    { path: '/thong-tin/gioi-thieu', changefreq: 'monthly', priority: '0.4' },
    { path: '/thong-tin/quy-dinh', changefreq: 'yearly', priority: '0.3' },
    { path: '/thong-tin/bao-mat', changefreq: 'yearly', priority: '0.3' },
    { path: '/thong-tin/dieu-khoan', changefreq: 'yearly', priority: '0.3' },
    { path: '/thong-tin/lien-he', changefreq: 'yearly', priority: '0.3' },
  ];
  if (OFFERS_PUBLIC) rows.push({ path: '/uu-dai', changefreq: 'weekly', priority: '0.5' });

  for (const p of PILLARS) {
    rows.push({ path: pillarHref(p.id), changefreq: 'weekly', priority: '0.7' });
  }
  for (const p of PLACES) {
    rows.push({ path: placeHref(p.slug), changefreq: 'monthly', priority: '0.6' });
  }
  for (const g of GUIDES) {
    rows.push({ path: guideHref(g.slug), changefreq: 'monthly', priority: '0.6' });
  }

  const listings = await listListings();
  for (const item of listings) {
    if (item.kind === 'offer' && !OFFERS_PUBLIC) continue;
    rows.push({
      path: hrefFor(item),
      changefreq: 'weekly',
      priority: item.kind === 'job' ? '0.7' : '0.5',
      lastmod: day(item.lastCheckedAt) ?? day(item.publishedAt),
    });
  }

  const seen = new Set<string>();
  const urls = rows
    .filter((r) => {
      if (seen.has(r.path)) return false;
      seen.add(r.path);
      return true;
    })
    .map((r) => {
      const last = r.lastmod ? `\n    <lastmod>${r.lastmod}</lastmod>` : '';
      return `  <url>\n    <loc>${esc(abs(origin, r.path))}</loc>${last}\n    <changefreq>${r.changefreq}</changefreq>\n    <priority>${r.priority}</priority>\n  </url>`;
    });

  const xml = `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.join('\n')}\n</urlset>\n`;
  return new Response(xml, {
    headers: {
      'Content-Type': 'application/xml; charset=utf-8',
      'Cache-Control': 'public, max-age=3600',
    },
  });
};
